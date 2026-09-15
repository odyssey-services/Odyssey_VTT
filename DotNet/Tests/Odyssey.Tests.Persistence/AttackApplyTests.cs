using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Odyssey.Application.Combat;
using Odyssey.Application.Commands;
using Odyssey.Application.Inventory;
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
    public sealed class AttackApplyTests
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

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "ody-s05-604-" + Guid.NewGuid().ToString("N"));
            _clock = new SystemWallClock();
            Result<CampaignHandle> campaign = new SqliteCampaignRepository(_clock).Create(new CreateCampaignRequest(_root, "attack-apply", "ruleset.core", "1.0.0", "0.1.0"), Command(), Corr);
            Assert.That(campaign.IsSuccess, Is.True);
            _campaign = campaign.Value;
            _characters = new SqliteCharacterRepository(_clock);
            _encounters = new SqliteCombatEncounterRepository(_clock);
            _inventory = new SqliteInventoryRepository(_clock);
            _reader = new SqliteAttackStateReader(_encounters, _inventory, _characters, _clock);
            _apply = new SqliteAttackApplyRepository(_clock);
        }

        [Test] // TC-ATTACK-029
        public void Immediate_acceptance_commits_outcome_and_gamelog_atomically_in_one_transaction()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            AttackRequest request = Request(encounter, actor, target, item);
            var random = new CountingRandomFactory();
            long domainEventsBefore = Count("DomainEvents");
            long appliedCommandsBefore = Count("AppliedCommands");

            Result<AttackOutcomeRecord> result = AttackApplyService.ResolveAttack(_reader, new Rules(requiresIntervention: false), random, _apply, _campaign, Epoch, request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.OutcomeKind, Is.EqualTo(AttackOutcomeKind.Accepted));
            Assert.That(result.Value.GameLogEntryId, Is.Not.Null);
            Assert.That(random.CreateCalls, Is.EqualTo(1));
            Assert.That(Count("AttackOutcome"), Is.EqualTo(1));
            Assert.That(Count("GameLogEntries"), Is.EqualTo(1));
            Assert.That(Count("DiceRolls"), Is.EqualTo(1));
            // ResolveAttack's own atomic-apply transaction commits exactly one
            // DomainEvent and one AppliedCommands row -- both the AttackOutcome
            // row and the GameLogEntries/DiceRolls rows it writes land in that
            // same transaction, not a separate one.
            Assert.That(Count("DomainEvents"), Is.EqualTo(domainEventsBefore + 1));
            Assert.That(Count("AppliedCommands"), Is.EqualTo(appliedCommandsBefore + 1));
            Assert.That(GameLogEntryFor(result.Value.GameLogEntryId!).AuthoritativeSequence, Is.GreaterThan(0));
        }

        [Test] // TC-ATTACK-030
        public void Intervention_required_creates_durable_pending_outcome_with_saved_random_sample_and_no_gamelog_entry()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            AttackRequest request = Request(encounter, actor, target, item);
            var random = new CountingRandomFactory();

            Result<AttackOutcomeRecord> result = AttackApplyService.ResolveAttack(_reader, new Rules(requiresIntervention: true), random, _apply, _campaign, Epoch, request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.OutcomeKind, Is.EqualTo(AttackOutcomeKind.Pending));
            Assert.That(result.Value.InterventionRequired, Is.True);
            Assert.That(result.Value.RandomSampleValue, Is.InRange(1, 100));
            Assert.That(result.Value.GameLogEntryId, Is.Null);
            Assert.That(random.CreateCalls, Is.EqualTo(1));
            Assert.That(Count("AttackOutcome"), Is.EqualTo(1));
            Assert.That(Count("GameLogEntries"), Is.EqualTo(0));
        }

        [Test] // TC-ATTACK-031
        public void ResolveAttackIntervention_reuses_saved_random_sample_and_is_mainGm_only()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            AttackRequest request = Request(encounter, actor, target, item);
            var random = new CountingRandomFactory();
            Result<AttackOutcomeRecord> pending = AttackApplyService.ResolveAttack(_reader, new Rules(requiresIntervention: true), random, _apply, _campaign, Epoch, request);
            Assert.That(pending.IsSuccess, Is.True);
            int savedSample = pending.Value.RandomSampleValue;

            Result<AttackOutcomeRecord> denied = AttackApplyService.ResolveAttackIntervention(_apply, _campaign, request.CommandId, AttackInterventionResolution.Approve, User(), actorIsMainGm: false, Command(), Corr);
            Assert.That(denied.IsFailure, Is.True);
            Assert.That(Count("GameLogEntries"), Is.EqualTo(0));

            Result<AttackOutcomeRecord> approved = AttackApplyService.ResolveAttackIntervention(_apply, _campaign, request.CommandId, AttackInterventionResolution.Approve, User(), actorIsMainGm: true, Command(), Corr);
            Assert.That(approved.IsSuccess, Is.True);
            Assert.That(approved.Value.OutcomeKind, Is.EqualTo(AttackOutcomeKind.Accepted));
            Assert.That(approved.Value.RandomSampleValue, Is.EqualTo(savedSample));
            Assert.That(approved.Value.GameLogEntryId, Is.Not.Null);
            Assert.That(random.CreateCalls, Is.EqualTo(1), "ResolveAttackIntervention must never derive a new RNG sample.");
            Assert.That(Count("GameLogEntries"), Is.EqualTo(1));
        }

        [Test] // TC-ATTACK-032
        public void Retry_of_the_same_ResolveAttack_command_does_not_duplicate_mutation_or_reroll()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            AttackRequest request = Request(encounter, actor, target, item);
            var random = new CountingRandomFactory();
            long appliedCommandsBefore = Count("AppliedCommands");

            Result<AttackOutcomeRecord> first = AttackApplyService.ResolveAttack(_reader, new Rules(requiresIntervention: false), random, _apply, _campaign, Epoch, request);
            Assert.That(first.IsSuccess, Is.True);
            Result<AttackOutcomeRecord> retry = AttackApplyService.ResolveAttack(_reader, new Rules(requiresIntervention: false), random, _apply, _campaign, Epoch, request);

            Assert.That(retry.IsSuccess, Is.True);
            Assert.That(retry.Value.RandomSampleValue, Is.EqualTo(first.Value.RandomSampleValue));
            Assert.That(retry.Value.GameLogEntryId, Is.EqualTo(first.Value.GameLogEntryId));
            Assert.That(random.CreateCalls, Is.EqualTo(1), "A retry with the same CommandId must never query the RNG factory again.");
            Assert.That(Count("AttackOutcome"), Is.EqualTo(1));
            Assert.That(Count("GameLogEntries"), Is.EqualTo(1));
            Assert.That(Count("AppliedCommands"), Is.EqualTo(appliedCommandsBefore + 1));
        }

        [Test] // TC-ATTACK-033
        public void Stale_revision_at_apply_time_is_a_typed_rejection_with_no_partial_commit()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            AttackIntent staleIntent = new AttackIntent(encounter.EncounterId, actor, new[] { target }, item.ItemInstanceId, encounter.Revision + 1);
            AttackRequest request = new AttackRequest(staleIntent, User(), true, Command(), Corr);
            var random = new CountingRandomFactory();
            long before = TotalRowCount();

            Result<AttackOutcomeRecord> result = AttackApplyService.ResolveAttack(_reader, new Rules(requiresIntervention: false), random, _apply, _campaign, Epoch, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(random.CreateCalls, Is.Zero, "A guard failure inside the unmodified reader must reject before any RNG draw.");
            Assert.That(TotalRowCount(), Is.EqualTo(before));
        }

        [Test] // TC-ATTACK-034
        public void ResolveAttackIntervention_cannot_resolve_an_already_resolved_pending_outcome_twice()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            AttackRequest request = Request(encounter, actor, target, item);
            var random = new CountingRandomFactory();
            Assert.That(AttackApplyService.ResolveAttack(_reader, new Rules(requiresIntervention: true), random, _apply, _campaign, Epoch, request).IsSuccess, Is.True);
            Assert.That(AttackApplyService.ResolveAttackIntervention(_apply, _campaign, request.CommandId, AttackInterventionResolution.Reject, User(), true, Command(), Corr).IsSuccess, Is.True);
            Assert.That(Count("GameLogEntries"), Is.EqualTo(0), "A Rejected outcome commits no Game Log entry.");

            Result<AttackOutcomeRecord> secondResolution = AttackApplyService.ResolveAttackIntervention(_apply, _campaign, request.CommandId, AttackInterventionResolution.Approve, User(), true, Command(), Corr);

            Assert.That(secondResolution.IsFailure, Is.True);
            Assert.That(Count("GameLogEntries"), Is.EqualTo(0));
        }

        private CombatEncounterRecord CreateEncounter(CharacterId actor, CharacterId target)
            => CombatEncounterService.Create(_encounters, _campaign, new CreateCombatEncounterRequest(new[] { actor, target }, User(), true, Command()), Corr).Value;

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

        private int Count(string table)
        {
            using SqliteConnection connection = OpenRawConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM " + table + ";";
            return Convert.ToInt32(command.ExecuteScalar());
        }

        private (long AuthoritativeSequence, string EntryType) GameLogEntryFor(string logEntryId)
        {
            using SqliteConnection connection = OpenRawConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT AuthoritativeSequence, EntryType FROM GameLogEntries WHERE LogEntryId = $id;";
            command.Parameters.AddWithValue("$id", logEntryId);
            using SqliteDataReader reader = command.ExecuteReader();
            Assert.That(reader.Read(), Is.True);
            return (reader.GetInt64(0), reader.GetString(1));
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
                // ODY-S05-609: this fixture's own tests exercise attack apply
                // atomicity/idempotency/intervention, not delta application --
                // empty delta lists avoid the new `TargetRef` addressing
                // convention entirely, rather than passing an unaddressable
                // placeholder value that would now be genuinely resolved and
                // rejected.
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
