using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Odyssey.Application.Audience;
using Odyssey.Application.Combat;
using Odyssey.Application.Commands;
using Odyssey.Application.Dice;
using Odyssey.Application.Effects;
using Odyssey.Application.GameLog;
using Odyssey.Application.Inventory;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Persistence;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;
using Odyssey.Rules.Combat;

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S05-607: real tests for `ADR-029` §1 rule 7's compensating root
    /// command (`CompensateAttackOutcome`) and the expanded combat-encounter
    /// Game Log audience (all current participants + MainGM, via the
    /// existing `DiceRollAudienceKind.SelectedParticipants`/`DiceRollVisibilityPolicy`/
    /// `GameLogReconnectService` machinery, unmodified). No `605`/`606`
    /// expiry/effect-application logic, and no `609`/`610` territory, is
    /// exercised here.
    /// </summary>
    public sealed class AttackCompensationAndAudienceTests
    {
        private static readonly CorrelationId Corr = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly RngKeyEpochId Epoch = RngKeyEpochId.Parse("epoch-001");
        private string _root = null!;
        private CampaignHandle _campaign = null!;
        private IWallClock _clock = null!;
        private SqliteCharacterRepository _characters = null!;
        private SqliteCombatEncounterRepository _encounters = null!;
        private SqliteInventoryRepository _inventory = null!;
        private SqliteAttackStateReader _reader = null!;
        private SqliteAttackApplyRepository _apply = null!;
        private SqliteGameLogRepository _gameLog = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "ody-s05-607-" + Guid.NewGuid().ToString("N"));
            _clock = new SystemWallClock();
            Result<CampaignHandle> campaign = new SqliteCampaignRepository(_clock).Create(new CreateCampaignRequest(_root, "attack-compensation-audience", "ruleset.core", "1.0.0", "0.1.0"), Command(), Corr);
            Assert.That(campaign.IsSuccess, Is.True);
            _campaign = campaign.Value;
            _characters = new SqliteCharacterRepository(_clock);
            _encounters = new SqliteCombatEncounterRepository(_clock);
            _inventory = new SqliteInventoryRepository(_clock);
            _reader = new SqliteAttackStateReader(_encounters, _inventory, _characters, _clock);
            _apply = new SqliteAttackApplyRepository(_clock);
            _gameLog = new SqliteGameLogRepository(_clock);
        }

        // ---- Compensation ----

        [Test] // TC-ATTACK-056
        public void Compensation_creates_a_new_correcting_row_without_mutating_or_deleting_the_original()
        {
            (AttackOutcomeRecord accepted, CharacterId actor, CharacterId target) = AcceptedAttack();
            GameLogEntryRow originalBefore = ReadGameLogEntryRow(accepted.GameLogEntryId!);
            long attackOutcomeBefore = Count("AttackOutcome");
            long gameLogEntriesBefore = Count("GameLogEntries");

            Result<AttackCompensationRecord> result = AttackApplyService.CompensateAttackOutcome(_apply, _campaign, accepted.ResolveAttackCommandId, "logged wrong summary", "corrected: no damage was actually dealt", User(), actorIsMainGm: true, Command(), Corr);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.GameLogEntryId, Is.Not.EqualTo(accepted.GameLogEntryId));
            GameLogEntryRow originalAfter = ReadGameLogEntryRow(accepted.GameLogEntryId!);
            Assert.That(originalAfter.SummaryPayload, Is.EqualTo(originalBefore.SummaryPayload), "The original entry's own summary must never change.");
            Assert.That(originalAfter.EntryType, Is.EqualTo(originalBefore.EntryType));
            Assert.That(Count("AttackOutcome"), Is.EqualTo(attackOutcomeBefore), "Compensation never touches the AttackOutcome row itself.");
            Assert.That(Count("GameLogEntries"), Is.EqualTo(gameLogEntriesBefore + 1), "Exactly one new, causally-linked row.");
            GameLogEntryRow compensating = ReadGameLogEntryRow(result.Value.GameLogEntryId);
            Assert.That(compensating.EntryType, Is.EqualTo("AttackCompensated"));
            Assert.That(compensating.SummaryPayload, Is.EqualTo("corrected: no damage was actually dealt"));
        }

        [Test] // TC-ATTACK-057
        public void Non_mainGm_compensation_attempt_is_denied_without_any_mutation()
        {
            (AttackOutcomeRecord accepted, _, _) = AcceptedAttack();
            long before = TotalRowCount();

            Result<AttackCompensationRecord> result = AttackApplyService.CompensateAttackOutcome(_apply, _campaign, accepted.ResolveAttackCommandId, "reason", "corrected summary", User(), actorIsMainGm: false, Command(), Corr);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(TotalRowCount(), Is.EqualTo(before));
        }

        [Test] // TC-ATTACK-058
        public void Empty_reason_code_or_corrected_summary_is_rejected()
        {
            (AttackOutcomeRecord accepted, _, _) = AcceptedAttack();

            Assert.That(AttackApplyService.CompensateAttackOutcome(_apply, _campaign, accepted.ResolveAttackCommandId, "", "corrected summary", User(), true, Command(), Corr).IsFailure, Is.True);
            Assert.That(AttackApplyService.CompensateAttackOutcome(_apply, _campaign, accepted.ResolveAttackCommandId, "reason", "  ", User(), true, Command(), Corr).IsFailure, Is.True);
        }

        [Test] // TC-ATTACK-059
        public void A_second_compensation_of_the_same_committing_event_is_rejected()
        {
            (AttackOutcomeRecord accepted, _, _) = AcceptedAttack();
            Assert.That(AttackApplyService.CompensateAttackOutcome(_apply, _campaign, accepted.ResolveAttackCommandId, "first reason", "first correction", User(), true, Command(), Corr).IsSuccess, Is.True);

            Result<AttackCompensationRecord> second = AttackApplyService.CompensateAttackOutcome(_apply, _campaign, accepted.ResolveAttackCommandId, "second reason", "second correction", User(), true, Command(), Corr);

            Assert.That(second.IsFailure, Is.True);
            Assert.That(Count("GameLogEntries"), Is.EqualTo(2), "Original attack entry + exactly one compensation entry, not two.");
        }

        [Test] // TC-ATTACK-060
        public void Compensation_is_a_separate_transaction_and_CommandId_and_is_itself_idempotent()
        {
            (AttackOutcomeRecord accepted, _, _) = AcceptedAttack();
            CommandId compensatingCommandId = Command();

            Result<AttackCompensationRecord> first = AttackApplyService.CompensateAttackOutcome(_apply, _campaign, accepted.ResolveAttackCommandId, "reason", "corrected", User(), true, compensatingCommandId, Corr);
            Assert.That(first.IsSuccess, Is.True);
            Assert.That(first.Value.CompensatingCommandId, Is.Not.EqualTo(accepted.ResolveAttackCommandId), "The compensating command has its own identity, distinct from the original attack's.");

            Result<AttackCompensationRecord> retry = AttackApplyService.CompensateAttackOutcome(_apply, _campaign, accepted.ResolveAttackCommandId, "reason", "corrected", User(), true, compensatingCommandId, Corr);
            Assert.That(retry.IsSuccess, Is.True);
            Assert.That(retry.Value.GameLogEntryId, Is.EqualTo(first.Value.GameLogEntryId), "A retry with the same compensating CommandId returns the original correction, not a second one.");
            Assert.That(Count("GameLogEntries"), Is.EqualTo(2));
        }

        [Test] // TC-ATTACK-064
        public void Compensating_a_pending_outcome_is_rejected()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            var rules = new Rules(requiresIntervention: true);
            Result<AttackOutcomeRecord> pending = AttackApplyService.ResolveAttack(_reader, rules, new CountingRandomFactory(), _apply, _campaign, Epoch, Request(encounter, actor, target, item));
            Assert.That(pending.IsSuccess, Is.True);
            Assert.That(pending.Value.OutcomeKind, Is.EqualTo(AttackOutcomeKind.Pending));

            Result<AttackCompensationRecord> result = AttackApplyService.CompensateAttackOutcome(_apply, _campaign, pending.Value.ResolveAttackCommandId, "reason", "corrected", User(), true, Command(), Corr);

            Assert.That(result.IsFailure, Is.True);
        }

        // ---- Expanded audience ----

        [Test] // TC-ATTACK-061
        public void A_combat_participant_who_is_not_the_attacker_sees_the_attack_gamelog_entry()
        {
            (AttackOutcomeRecord accepted, CharacterId actor, CharacterId target) = AcceptedAttack(out UserId actorOwner, out UserId targetOwner);

            IReadOnlyList<GameLogEntryRecord> entries = _gameLog.ListGameLog(_campaign, Corr).Value;
            IReadOnlyList<GameLogEntryRecord> visibleToTarget = GameLogReconnectService.GetVisibleEntries(entries, targetOwner, BaselineRole.Player, new InMemoryCampaignUserGroupDirectory());

            Assert.That(visibleToTarget, Has.Some.Matches<GameLogEntryRecord>(e => e.LogEntryId == accepted.GameLogEntryId), "The target's own owning user -- a combat participant, not the attacker -- must see the entry.");
        }

        [Test] // TC-ATTACK-062
        public void An_outsider_never_present_in_the_encounter_does_not_see_the_entry()
        {
            (AttackOutcomeRecord accepted, _, _) = AcceptedAttack();
            UserId outsider = User();

            IReadOnlyList<GameLogEntryRecord> entries = _gameLog.ListGameLog(_campaign, Corr).Value;
            IReadOnlyList<GameLogEntryRecord> visibleToOutsider = GameLogReconnectService.GetVisibleEntries(entries, outsider, BaselineRole.Player, new InMemoryCampaignUserGroupDirectory());

            Assert.That(visibleToOutsider, Has.None.Matches<GameLogEntryRecord>(e => e.LogEntryId == accepted.GameLogEntryId), "Fail-closed: an outsider never in the encounter must not see the entry.");
        }

        [Test] // TC-ATTACK-065
        public void An_all_npc_encounter_with_no_owning_user_falls_back_to_gmOnly_audience()
        {
            CharacterId actor = Active("actor"), target = Active("target"); // deliberately never assigned an owner
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            var rules = new Rules(requiresIntervention: false);
            Result<AttackOutcomeRecord> result = AttackApplyService.ResolveAttack(_reader, rules, new CountingRandomFactory(), _apply, _campaign, Epoch, Request(encounter, actor, target, item));
            Assert.That(result.IsSuccess, Is.True);

            IReadOnlyList<GameLogEntryRecord> entries = _gameLog.ListGameLog(_campaign, Corr).Value;
            GameLogEntryRecord entry = entries[0];
            Assert.That(entry.Roll.Audience.Kind, Is.EqualTo(DiceRollAudienceKind.GMOnly));
            IReadOnlyList<GameLogEntryRecord> visibleToRandomPlayer = GameLogReconnectService.GetVisibleEntries(entries, User(), BaselineRole.Player, new InMemoryCampaignUserGroupDirectory());
            Assert.That(visibleToRandomPlayer, Is.Empty);
            IReadOnlyList<GameLogEntryRecord> visibleToGm = GameLogReconnectService.GetVisibleEntries(entries, User(), BaselineRole.MainGM, new InMemoryCampaignUserGroupDirectory());
            Assert.That(visibleToGm, Has.Count.EqualTo(1));
        }

        // ---- Architecture guard ----

        [Test] // TC-ATTACK-063
        public void Compensation_never_touches_character_or_item_resource_state()
        {
            DirectoryInfo? root = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Packages"))) root = root.Parent;
            Assert.That(root, Is.Not.Null);
            string source = File.ReadAllText(Path.Combine(root!.FullName, "Packages", "com.odyssey.persistence", "Runtime", "Sqlite", "SqliteAttackApplyRepository.cs"));
            int compensateStart = source.IndexOf("public Result<AttackCompensationRecord> CompensateAttackOutcome", StringComparison.Ordinal);
            Assert.That(compensateStart, Is.GreaterThanOrEqualTo(0));
            int nextMethodStart = source.IndexOf("private static void WriteAcceptedAttackGameLogEntry", compensateStart, StringComparison.Ordinal);
            Assert.That(nextMethodStart, Is.GreaterThan(compensateStart));
            string compensationRegion = source.Substring(compensateStart, nextMethodStart - compensateStart);
            foreach (string forbidden in new[] { "ICharacterRepository", "SetResourceCurrentValue", "IInventoryRepository", "AttackDelta" })
            {
                Assert.That(compensationRegion, Does.Not.Contain(forbidden));
            }
        }

        // ---- helpers ----

        private (AttackOutcomeRecord Accepted, CharacterId Actor, CharacterId Target) AcceptedAttack()
            => AcceptedAttack(out _, out _);

        private (AttackOutcomeRecord Accepted, CharacterId Actor, CharacterId Target) AcceptedAttack(out UserId actorOwner, out UserId targetOwner)
        {
            CharacterId actor = Active("actor"), target = Active("target");
            actorOwner = User();
            targetOwner = User();
            AssignOwner(actor, actorOwner);
            AssignOwner(target, targetOwner);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            var rules = new Rules(requiresIntervention: false);
            Result<AttackOutcomeRecord> result = AttackApplyService.ResolveAttack(_reader, rules, new CountingRandomFactory(), _apply, _campaign, Epoch, Request(encounter, actor, target, item));
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.GameLogEntryId, Is.Not.Null);
            return (result.Value, actor, target);
        }

        private void AssignOwner(CharacterId characterId, UserId owner)
        {
            CharacterRecord current = _characters.GetCharacter(_campaign, characterId, Corr).Value;
            Result<CharacterRecord> assigned = _characters.AssignPrimaryOwner(_campaign, characterId, owner, "test ownership", actorIsMainGm: true, current.Revisions.OwnershipRevision, Command(), Corr);
            Assert.That(assigned.IsSuccess, Is.True);
        }

        private CombatEncounterRecord CreateEncounter(CharacterId a, CharacterId b)
            => CombatEncounterService.Create(_encounters, _campaign, new CreateCombatEncounterRequest(new[] { a, b }, User(), true, Command()), Corr).Value;

        private static AttackRequest Request(CombatEncounterRecord encounter, CharacterId actor, CharacterId target, ItemInstanceRecord item)
            => new AttackRequest(new AttackIntent(encounter.EncounterId, actor, new[] { target }, item.ItemInstanceId, encounter.Revision), User(), true, Command(), Corr);

        private CharacterId Active(string name)
        {
            CharacterId id = _characters.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, name), Command(), Corr).Value.CharacterId;
            CharacterRecord current = _characters.GetCharacter(_campaign, id, Corr).Value;
            return _characters.ApproveCharacterDraft(_campaign, id, true, current.Revisions.LifecycleRevision, Command(), Corr).Value.CharacterId;
        }

        // ODY-S06-103: the attack action item must now be currently Equipped, not merely owned -- every
        // caller of this fixture helper wants the still-supported (owned AND equipped) legitimate path.
        private ItemInstanceRecord ItemFor(CharacterId owner)
        {
            UtcInstant now = _clock.GetUtcNow();
            InventoryId inventoryId = InventoryId.NewId(now);
            InventoryRecord inventory = new InventoryRecord(inventoryId, _campaign.CampaignId, InventoryOwnerRef.ForCharacter(owner), 1, now, now);
            Assert.That(_inventory.CreateInventory(_campaign, inventory, Command(), Corr).IsSuccess, Is.True);
            ContentDefinitionRef source = ContentDefinitionRef.Parse("cdef_0123456789abcdef0123456789abcdef/1");
            ItemInstanceRecord item = new ItemInstanceRecord(ItemInstanceId.NewId(now), _campaign.CampaignId, inventoryId, InventoryOwnerRef.ForCharacter(owner), InventoryLocationRef.Contained(inventoryId, "main"), source, new ItemMechanicsSnapshot(source, 1, ContentDefinitionType.Item, "{}"), "{}", 1, now, now);
            ItemInstanceRecord created = _inventory.CreateItemInstance(_campaign, item, Command(), Corr).Value;
            var entry = new EquippedEntry(created.InventoryId, InventoryItemRef.ForInstance(created.ItemInstanceId), "main_hand", Array.Empty<BodyPartId>(), User(), now, 1);
            Result<EquippedEntryRecord> equipped = _inventory.EquipItem(_campaign, new EquipTransition(new EquippedEntryRecord(_campaign.CampaignId, entry), created.Revision, Command()), Corr);
            Assert.That(equipped.IsSuccess, Is.True);
            return _inventory.GetItemInstance(_campaign, created.ItemInstanceId, Corr).Value;
        }

        private long TotalRowCount()
        {
            using SqliteConnection connection = OpenRawConnection();
            var tables = new List<string>();
            using (SqliteCommand list = connection.CreateCommand())
            {
                list.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
                using SqliteDataReader reader = list.ExecuteReader();
                while (reader.Read()) tables.Add(reader.GetString(0));
            }
            long total = 0;
            foreach (string table in tables)
            {
                using SqliteCommand count = connection.CreateCommand();
                count.CommandText = "SELECT COUNT(*) FROM \"" + table + "\";";
                total += Convert.ToInt64(count.ExecuteScalar());
            }
            return total;
        }

        private long Count(string table)
        {
            using SqliteConnection connection = OpenRawConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM " + table + ";";
            return Convert.ToInt64(command.ExecuteScalar());
        }

        private readonly struct GameLogEntryRow
        {
            public GameLogEntryRow(string entryType, string summaryPayload) { EntryType = entryType; SummaryPayload = summaryPayload; }
            public string EntryType { get; }
            public string SummaryPayload { get; }
        }

        private GameLogEntryRow ReadGameLogEntryRow(string logEntryId)
        {
            using SqliteConnection connection = OpenRawConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT EntryType, SummaryPayload FROM GameLogEntries WHERE LogEntryId = $id;";
            command.Parameters.AddWithValue("$id", logEntryId);
            using SqliteDataReader reader = command.ExecuteReader();
            Assert.That(reader.Read(), Is.True);
            return new GameLogEntryRow(reader.GetString(0), reader.GetString(1));
        }

        private SqliteConnection OpenRawConnection()
        {
            var connection = new SqliteConnection("Data Source=" + Path.Combine(_root, "campaign.db"));
            connection.Open();
            return connection;
        }

        private static CommandId Command() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId User() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        private sealed class Rules : IAttackRulesEvaluator
        {
            private readonly bool _requiresIntervention;
            public Rules(bool requiresIntervention) { _requiresIntervention = requiresIntervention; }
            public ProposedAttackResolution Preview(AttackIntent intent, AttackEvaluationSnapshot snapshot) => Resolution(intent, snapshot, null);
            public ProposedAttackResolution Evaluate(AttackIntent intent, AttackEvaluationSnapshot snapshot, AttackRandomSample randomSample) => Resolution(intent, snapshot, randomSample);

            private ProposedAttackResolution Resolution(AttackIntent intent, AttackEvaluationSnapshot snapshot, AttackRandomSample? sample)
            {
                EffectApplicationDecision decision = _requiresIntervention ? EffectApplicationDecision.RequiresIntervention : EffectApplicationDecision.Apply;
                var effectCandidates = new[] { new AttackEffectCandidate(intent.TargetIds[0], snapshot.ActionSourceRef, snapshot.ActionSourceRef, decision) };
                // ODY-S05-609: this fixture's own tests exercise compensation/
                // audience projection, not delta application -- empty delta
                // lists avoid the new `TargetRef` addressing convention
                // entirely, rather than passing an unaddressable placeholder
                // value that would now be genuinely resolved and rejected.
                return new ProposedAttackResolution(intent, snapshot, sample, new AttackRangeResult(true, "in-range"), Array.AsReadOnly(new[] { new AttackModifierEntry("fixture", 1) }), new AttackHitResult(true, "hit"), new AttackBodyPartProposal("body"), new AttackArmorProposal("armor", 0), Array.Empty<AttackDelta>(), Array.Empty<AttackDelta>(), Array.AsReadOnly(effectCandidates));
            }
        }

        private sealed class CountingRandomFactory : IAuthoritativeRandomStreamFactory
        {
            private readonly IAuthoritativeRandomStreamFactory _inner = new DeterministicRandomStreamFactory(CampaignRngKey.FromBytes(new byte[32] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }));
            public int CreateCalls;
            public Result<IAuthoritativeRandomStream> Create(RandomDecisionContext context) { CreateCalls++; return _inner.Create(context); }
        }
    }
}
