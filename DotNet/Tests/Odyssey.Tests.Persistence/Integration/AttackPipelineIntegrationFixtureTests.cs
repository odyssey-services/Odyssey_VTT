using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Odyssey.Application.Audience;
using Odyssey.Application.Combat;
using Odyssey.Application.Commands;
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
using Odyssey.Domain.Effects;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;
using Odyssey.Rules.Combat;

namespace Odyssey.Tests.Persistence.Integration
{
    /// <summary>
    /// ODY-S05-608: end-to-end integration fixture for the full attack
    /// pipeline block (`ODY-S05-602`-`607`, `609`). Mirrors `ODY-S05-404`'s/
    /// `507`'s own precedent exactly: no new production code, only a
    /// composition of already-accepted public services into coherent
    /// sequences that reads as a real MainGM session. `602`-`607`/`609` each
    /// already have their own isolated unit tests covering every individual
    /// `ADR-029` §12 Definition-of-Done item; this file proves the one thing
    /// none of them do -- several of those items exercised TOGETHER, against
    /// one real SQLite database, in one connected scenario, not reset between
    /// steps the way each predecessor's own `SetUp()` does.
    ///
    /// Honest scope limitation (see this task's own contract §18): no
    /// production implementation of `IAttackRulesEvaluator` exists anywhere
    /// in this codebase -- only hand-written test fixtures, in every
    /// predecessor task and here. This file cannot and does not prove "a real
    /// Ruleset produces this decision" (`ADR-029` §10's own explicit
    /// non-goal for this whole block); it proves that `AttackEvaluationService`/
    /// `AttackApplyService`/`SqliteAttackApplyRepository`/`CombatEffectExpiryService`/
    /// `GameLogReconnectService` genuinely compose together against a real
    /// database when a Rules decision is supplied, whoever supplies it.
    /// </summary>
    public sealed class AttackPipelineIntegrationFixtureTests
    {
        private static readonly CorrelationId Corr = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly RngKeyEpochId Epoch = RngKeyEpochId.Parse("epoch-001");
        private static readonly ResourceDefinitionId Health = ResourceDefinitionId.Parse("health");
        private string _root = null!;
        private CampaignHandle _campaign = null!;
        private IWallClock _clock = null!;
        private SqliteCharacterRepository _characters = null!;
        private SqliteCombatEncounterRepository _encounters = null!;
        private SqliteInventoryRepository _inventory = null!;
        private SqliteActiveEffectRepository _activeEffects = null!;
        private SqliteAttackStateReader _reader = null!;
        private SqliteAttackApplyRepository _apply = null!;
        private SqliteGameLogRepository _gameLog = null!;
        private SqliteCombatEncounterLifecycleReader _lifecycleReader = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "ody-s05-608-" + Guid.NewGuid().ToString("N"));
            _clock = new SystemWallClock();
            Result<CampaignHandle> campaign = new SqliteCampaignRepository(_clock).Create(new CreateCampaignRequest(_root, "attack-pipeline-integration", "ruleset.core", "1.0.0", "0.1.0"), Command(), Corr);
            Assert.That(campaign.IsSuccess, Is.True);
            _campaign = campaign.Value;
            _characters = new SqliteCharacterRepository(_clock);
            _encounters = new SqliteCombatEncounterRepository(_clock);
            _inventory = new SqliteInventoryRepository(_clock);
            _activeEffects = new SqliteActiveEffectRepository(_clock);
            _reader = new SqliteAttackStateReader(_encounters, _inventory, _characters, _clock);
            _apply = new SqliteAttackApplyRepository(_clock);
            _gameLog = new SqliteGameLogRepository(_clock);
            _lifecycleReader = new SqliteCombatEncounterLifecycleReader(_encounters);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch (IOException) { }
        }

        [Test] // TC-ATTACK-076
        public void Full_happy_path_without_intervention_commits_outcome_gamelog_effect_and_delta_atomically()
        {
            // ADR-029 section 12 items 1, 4, 7.
            CharacterId actor = Active("actor"), target = Active("target");
            InitResource(target, Health);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            string targetRef = CharacterTargetRef(target, Health);
            var rules = new Rules(requiresIntervention: false, effectDecision: EffectApplicationDecision.Apply, durationBinding: null, damageDeltas: new[] { new AttackDelta(targetRef, -3) }, costDeltas: Array.Empty<AttackDelta>());
            AttackRequest request = Request(encounter, actor, target, item);
            var random = new CountingRandomFactory();

            // Item 1: PreviewAttack is genuinely pure -- no RNG draw, no
            // persisted outcome, no Game Log entry, repeatable.
            long attackOutcomeBefore = Count("AttackOutcome");
            long gameLogBefore = Count("GameLogEntries");
            Result<ProposedAttackResolution> preview1 = AttackEvaluationService.PreviewAttack(_reader, rules, _campaign, request);
            Result<ProposedAttackResolution> preview2 = AttackEvaluationService.PreviewAttack(_reader, rules, _campaign, request);
            Assert.That(preview1.IsSuccess, Is.True);
            Assert.That(preview2.IsSuccess, Is.True, "Preview is repeatable without side effects.");
            Assert.That(random.CreateCalls, Is.EqualTo(0), "Preview never draws RNG.");
            Assert.That(Count("AttackOutcome"), Is.EqualTo(attackOutcomeBefore));
            Assert.That(Count("GameLogEntries"), Is.EqualTo(gameLogBefore));
            Assert.That(_apply.GetOutcome(_campaign, request.CommandId, Corr).IsFailure, Is.True, "No persisted outcome from a preview.");

            // Item 4/7: real EvaluateAttack + real ResolveAttack commits the
            // AttackOutcome row, the DomainEvent, the Game Log entry, the
            // ActiveEffect row (Apply decision), and the resource delta --
            // all in the one atomic-apply transaction SqliteAttackApplyRepository
            // itself owns.
            Result<AttackOutcomeRecord> result = AttackApplyService.ResolveAttack(_reader, rules, random, _apply, _campaign, Epoch, request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.OutcomeKind, Is.EqualTo(AttackOutcomeKind.Accepted));
            Assert.That(random.CreateCalls, Is.EqualTo(1));
            Assert.That(result.Value.GameLogEntryId, Is.Not.Null);
            Assert.That(Count("AttackOutcome"), Is.EqualTo(attackOutcomeBefore + 1));
            Assert.That(Count("GameLogEntries"), Is.EqualTo(gameLogBefore + 1));
            IReadOnlyList<ActiveEffectRecord> effects = _activeEffects.ListActiveEffectsByTarget(_campaign, _campaign.CampaignId, ActiveEffectTargetRef.ForCharacter(target), Corr).Value;
            Assert.That(effects.Count, Is.EqualTo(1), "The Apply-decided candidate created a real ActiveEffect row.");
            Assert.That(CurrentValue(target, Health), Is.EqualTo(7), "The damage delta was applied to the real CharacterResource.");
        }

        [Test] // TC-ATTACK-077
        public void Full_path_with_intervention_defers_delta_and_effect_until_approval_without_a_reroll()
        {
            // ADR-029 section 12 items 4, 5, 7.
            CharacterId actor = Active("actor"), target = Active("target");
            InitResource(target, Health);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            string targetRef = CharacterTargetRef(target, Health);
            var rules = new Rules(requiresIntervention: true, effectDecision: EffectApplicationDecision.RequiresIntervention, durationBinding: null, damageDeltas: new[] { new AttackDelta(targetRef, -4) }, costDeltas: Array.Empty<AttackDelta>());
            AttackRequest request = Request(encounter, actor, target, item);
            var random = new CountingRandomFactory();

            Result<AttackOutcomeRecord> pending = AttackApplyService.ResolveAttack(_reader, rules, random, _apply, _campaign, Epoch, request);
            Assert.That(pending.IsSuccess, Is.True);
            Assert.That(pending.Value.OutcomeKind, Is.EqualTo(AttackOutcomeKind.Pending), "A durable pending resolution, not an immediate commit.");
            Assert.That(random.CreateCalls, Is.EqualTo(1));
            Assert.That(CurrentValue(target, Health), Is.EqualTo(10), "Nothing applied yet on the pending step.");
            Assert.That(_activeEffects.ListActiveEffectsByTarget(_campaign, _campaign.CampaignId, ActiveEffectTargetRef.ForCharacter(target), Corr).Value, Is.Empty);

            Result<AttackOutcomeRecord> approved = AttackApplyService.ResolveAttackIntervention(_apply, _campaign, request.CommandId, AttackInterventionResolution.Approve, User(), true, Command(), Corr);

            Assert.That(approved.IsSuccess, Is.True);
            Assert.That(approved.Value.OutcomeKind, Is.EqualTo(AttackOutcomeKind.Accepted));
            Assert.That(random.CreateCalls, Is.EqualTo(1), "Approving the intervention never re-draws RNG -- the pending step's own sample is reused verbatim.");
            Assert.That(CurrentValue(target, Health), Is.EqualTo(6), "The deferred approval step applies the delta, not the original pending step.");
            Assert.That(_activeEffects.ListActiveEffectsByTarget(_campaign, _campaign.CampaignId, ActiveEffectTargetRef.ForCharacter(target), Corr).Value.Count, Is.EqualTo(1));
        }

        [Test] // TC-ATTACK-078
        public void Retrying_ResolveAttack_with_the_same_CommandId_never_duplicates_the_mutation()
        {
            // ADR-029 section 12 item 2.
            CharacterId actor = Active("actor"), target = Active("target");
            InitResource(target, Health);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            var rules = new Rules(requiresIntervention: false, effectDecision: EffectApplicationDecision.Apply, durationBinding: null, damageDeltas: new[] { new AttackDelta(CharacterTargetRef(target, Health), -3) }, costDeltas: Array.Empty<AttackDelta>());
            AttackRequest request = Request(encounter, actor, target, item);
            var random = new CountingRandomFactory();

            Result<AttackOutcomeRecord> first = AttackApplyService.ResolveAttack(_reader, rules, random, _apply, _campaign, Epoch, request);
            Assert.That(first.IsSuccess, Is.True);
            long attackOutcomeAfterFirst = Count("AttackOutcome");
            long gameLogAfterFirst = Count("GameLogEntries");

            Result<AttackOutcomeRecord> retry = AttackApplyService.ResolveAttack(_reader, rules, random, _apply, _campaign, Epoch, request);

            Assert.That(retry.IsSuccess, Is.True);
            Assert.That(retry.Value.ResolveAttackCommandId, Is.EqualTo(first.Value.ResolveAttackCommandId));
            Assert.That(random.CreateCalls, Is.EqualTo(1), "The retry never re-queries the authoritative random stream -- checked BEFORE any RNG derivation.");
            Assert.That(Count("AttackOutcome"), Is.EqualTo(attackOutcomeAfterFirst), "No duplicate AttackOutcome row.");
            Assert.That(Count("GameLogEntries"), Is.EqualTo(gameLogAfterFirst), "No duplicate Game Log entry.");
            Assert.That(CurrentValue(target, Health), Is.EqualTo(7), "The delta was applied exactly once, not twice.");
        }

        [Test] // TC-ATTACK-079
        public void A_stale_preview_is_rejected_at_apply_time_and_never_applies_stale_data()
        {
            // ADR-029 section 12 item 3.
            CharacterId actor = Active("actor"), target = Active("target");
            InitResource(target, Health);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            var rules = new Rules(requiresIntervention: false, effectDecision: EffectApplicationDecision.Apply, durationBinding: null, damageDeltas: new[] { new AttackDelta(CharacterTargetRef(target, Health), -3) }, costDeltas: Array.Empty<AttackDelta>());
            // Build the request against the encounter's ORIGINAL revision, then
            // advance the encounter for real before ever calling ResolveAttack --
            // exactly the "preview, then the world moved on" scenario.
            AttackRequest staleRequest = Request(encounter, actor, target, item);
            Result<ProposedAttackResolution> stalePreview = AttackEvaluationService.PreviewAttack(_reader, rules, _campaign, staleRequest);
            Assert.That(stalePreview.IsSuccess, Is.True, "The preview itself succeeded against the revision current at preview time.");
            encounter = Advance(Advance(encounter));
            long attackOutcomeBefore = Count("AttackOutcome");

            Result<AttackOutcomeRecord> result = AttackApplyService.ResolveAttack(_reader, rules, new CountingRandomFactory(), _apply, _campaign, Epoch, staleRequest);

            Assert.That(result.IsFailure, Is.True, "Apply-time re-authorization rejects the now-stale ExpectedEncounterRevision -- never silently applies preview-time data.");
            Assert.That(Count("AttackOutcome"), Is.EqualTo(attackOutcomeBefore));
            Assert.That(CurrentValue(target, Health), Is.EqualTo(10));
        }

        [Test] // TC-ATTACK-080
        public void A_ForRounds_effect_created_by_the_real_attack_apply_expires_via_the_real_service_and_stays_active_before_its_boundary()
        {
            // ADR-029 section 12 item 6.
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            var binding = new CombatDurationBinding(encounter.EncounterId, actor, target, encounter.RoundOrdinal, 0, 1);
            var rules = new Rules(requiresIntervention: false, effectDecision: EffectApplicationDecision.Apply, durationBinding: binding, damageDeltas: Array.Empty<AttackDelta>(), costDeltas: Array.Empty<AttackDelta>());

            Result<AttackOutcomeRecord> result = AttackApplyService.ResolveAttack(_reader, rules, new CountingRandomFactory(), _apply, _campaign, Epoch, Request(encounter, actor, target, item));
            Assert.That(result.IsSuccess, Is.True);
            ActiveEffectRecord candidate = _activeEffects.ListActiveEffectsByTarget(_campaign, _campaign.CampaignId, ActiveEffectTargetRef.ForCharacter(target), Corr).Value[0];

            // Boundary round = appliedRoundOrdinal(1) + requiredCount(1) + 1 = 3.
            // Direct call composition (this task's own contract §3/§18): no
            // automatic wiring point exists anywhere in production code for
            // this call -- 605 explicitly deferred it, 606 did not add one,
            // and this task does not add one either.
            Result<ActiveEffectRecord> beforeBoundary = CombatEffectExpiryService.EvaluateAndExpireIfDue(_activeEffects, _encounters, _lifecycleReader, _campaign, candidate, EffectDurationType.ForRounds, Command(), Corr);
            Assert.That(beforeBoundary.IsSuccess, Is.True);
            Assert.That(beforeBoundary.Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Active), "Fail-closed: inconclusive/pre-boundary expiry input leaves the effect Active.");

            encounter = Advance(Advance(Advance(Advance(encounter))));
            ActiveEffectRecord stillCandidate = _activeEffects.ListActiveEffectsByTarget(_campaign, _campaign.CampaignId, ActiveEffectTargetRef.ForCharacter(target), Corr).Value[0];
            Result<ActiveEffectRecord> afterBoundary = CombatEffectExpiryService.EvaluateAndExpireIfDue(_activeEffects, _encounters, _lifecycleReader, _campaign, stillCandidate, EffectDurationType.ForRounds, Command(), Corr);

            Assert.That(afterBoundary.IsSuccess, Is.True);
            Assert.That(afterBoundary.Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Expired));
        }

        [Test] // TC-ATTACK-081
        public void Compensating_a_completed_full_cycle_appends_corrective_history_without_mutating_it()
        {
            // ADR-029 section 12 item 8.
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            var rules = new Rules(requiresIntervention: false, effectDecision: EffectApplicationDecision.DoNotApply, durationBinding: null, damageDeltas: Array.Empty<AttackDelta>(), costDeltas: Array.Empty<AttackDelta>());
            Result<AttackOutcomeRecord> accepted = AttackApplyService.ResolveAttack(_reader, rules, new CountingRandomFactory(), _apply, _campaign, Epoch, Request(encounter, actor, target, item));
            Assert.That(accepted.IsSuccess, Is.True);
            string originalSummary = GameLogSummary(accepted.Value.GameLogEntryId!);
            long gameLogBefore = Count("GameLogEntries");

            Result<AttackCompensationRecord> compensation = AttackApplyService.CompensateAttackOutcome(_apply, _campaign, accepted.Value.ResolveAttackCommandId, "mis-logged roll summary", "corrected: attack actually missed", User(), true, Command(), Corr);

            Assert.That(compensation.IsSuccess, Is.True);
            Assert.That(Count("GameLogEntries"), Is.EqualTo(gameLogBefore + 1), "A new, causally-linked row -- the original is never deleted.");
            Assert.That(GameLogSummary(accepted.Value.GameLogEntryId!), Is.EqualTo(originalSummary), "The original Game Log entry is never mutated.");
            Assert.That(GameLogSummary(compensation.Value.GameLogEntryId), Is.EqualTo("corrected: attack actually missed"));
        }

        [Test] // TC-ATTACK-082
        public void The_real_committed_gamelog_entry_is_visible_to_encounter_participants_and_hidden_from_an_outsider()
        {
            // ADR-029 section 12 item 9.
            CharacterId actor = Active("actor"), target = Active("target"), observer = Active("observer");
            UserId actorOwner = User(), targetOwner = User(), observerOwner = User(), outsider = User();
            AssignOwner(actor, actorOwner);
            AssignOwner(target, targetOwner);
            AssignOwner(observer, observerOwner);
            CombatEncounterRecord encounter = CombatEncounterService.Create(_encounters, _campaign, new CreateCombatEncounterRequest(new[] { actor, target, observer }, User(), true, Command()), Corr).Value;
            ItemInstanceRecord item = ItemFor(actor);
            var rules = new Rules(requiresIntervention: false, effectDecision: EffectApplicationDecision.DoNotApply, durationBinding: null, damageDeltas: Array.Empty<AttackDelta>(), costDeltas: Array.Empty<AttackDelta>());

            Result<AttackOutcomeRecord> result = AttackApplyService.ResolveAttack(_reader, rules, new CountingRandomFactory(), _apply, _campaign, Epoch, Request(encounter, actor, target, item));
            Assert.That(result.IsSuccess, Is.True);

            IReadOnlyList<GameLogEntryRecord> entries = _gameLog.ListGameLog(_campaign, Corr).Value;
            var groups = new InMemoryCampaignUserGroupDirectory();
            IReadOnlyList<GameLogEntryRecord> visibleToObserver = GameLogReconnectService.GetVisibleEntries(entries, observerOwner, BaselineRole.Player, groups);
            IReadOnlyList<GameLogEntryRecord> visibleToOutsider = GameLogReconnectService.GetVisibleEntries(entries, outsider, BaselineRole.Player, groups);

            Assert.That(visibleToObserver, Has.Some.Matches<GameLogEntryRecord>(e => e.LogEntryId == result.Value.GameLogEntryId), "A combat participant who is neither the attacker nor the target still sees the entry -- the expanded audience.");
            Assert.That(visibleToOutsider, Has.None.Matches<GameLogEntryRecord>(e => e.LogEntryId == result.Value.GameLogEntryId), "An outsider never present in the encounter does not -- fail-closed.");
        }

        [Test] // TC-ATTACK-083
        public void An_item_targeted_delta_fails_the_whole_ResolveAttack_call_with_no_partial_commit()
        {
            // ADR-029 section 12 item 4's own "or commit none" half.
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            var rules = new Rules(requiresIntervention: false, effectDecision: EffectApplicationDecision.DoNotApply, durationBinding: null, damageDeltas: new[] { new AttackDelta("item:" + item.ItemInstanceId + ":durability", -1) }, costDeltas: Array.Empty<AttackDelta>());
            long before = TotalRowCount();

            Result<AttackOutcomeRecord> result = AttackApplyService.ResolveAttack(_reader, rules, new CountingRandomFactory(), _apply, _campaign, Epoch, Request(encounter, actor, target, item));

            Assert.That(result.IsFailure, Is.True, "609's own disclosed, escalated blocker -- item-targeted deltas are always a typed rejection, never a successful application; 608 does not attempt to close this gap.");
            Assert.That(TotalRowCount(), Is.EqualTo(before), "No partial commit -- not even the AttackOutcome row itself.");
        }

        // ---- helpers (composition only, no new production type) ----

        private static string CharacterTargetRef(CharacterId characterId, ResourceDefinitionId resourceKind) => "character:" + characterId + ":" + resourceKind;

        private void InitResource(CharacterId characterId, ResourceDefinitionId resourceKind)
        {
            CharacterRecord current = _characters.GetCharacter(_campaign, characterId, Corr).Value;
            Result<CharacterRecord> result = _characters.InitializeCharacterResource(_campaign, characterId, resourceKind, User(), true, current.Revisions.CharacterResourcesRevision, Command(), Corr);
            Assert.That(result.IsSuccess, Is.True);
        }

        private long CurrentValue(CharacterId characterId, ResourceDefinitionId resourceKind)
        {
            CharacterRecord current = _characters.GetCharacter(_campaign, characterId, Corr).Value;
            foreach (CharacterResource resource in current.Resources)
            {
                if (resource.ResourceDefinitionId.Equals(resourceKind))
                {
                    return resource.CurrentValue;
                }
            }

            Assert.Fail("Resource " + resourceKind + " not found on " + characterId + ".");
            return -1;
        }

        private void AssignOwner(CharacterId characterId, UserId owner)
        {
            CharacterRecord current = _characters.GetCharacter(_campaign, characterId, Corr).Value;
            Result<CharacterRecord> assigned = _characters.AssignPrimaryOwner(_campaign, characterId, owner, "test ownership", actorIsMainGm: true, current.Revisions.OwnershipRevision, Command(), Corr);
            Assert.That(assigned.IsSuccess, Is.True);
        }

        private CombatEncounterRecord CreateEncounter(CharacterId actor, CharacterId target)
            => CombatEncounterService.Create(_encounters, _campaign, new CreateCombatEncounterRequest(new[] { actor, target }, User(), true, Command()), Corr).Value;

        private CombatEncounterRecord Advance(CombatEncounterRecord encounter)
            => CombatEncounterService.Advance(_encounters, _campaign, new AdvanceCombatEncounterRequest(encounter.EncounterId, encounter.Revision, User(), true, Command()), Corr).Value;

        private static AttackRequest Request(CombatEncounterRecord encounter, CharacterId actor, CharacterId target, ItemInstanceRecord item)
            => new AttackRequest(new AttackIntent(encounter.EncounterId, actor, new[] { target }, item.ItemInstanceId, encounter.Revision), User(), true, Command(), Corr);

        private CharacterId Active(string name)
        {
            CharacterId id = _characters.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, name), Command(), Corr).Value.CharacterId;
            CharacterRecord current = _characters.GetCharacter(_campaign, id, Corr).Value;
            return _characters.ApproveCharacterDraft(_campaign, id, true, current.Revisions.LifecycleRevision, Command(), Corr).Value.CharacterId;
        }

        private ItemInstanceRecord ItemFor(CharacterId owner)
        {
            UtcInstant now = _clock.GetUtcNow();
            InventoryId inventoryId = InventoryId.NewId(now);
            InventoryRecord inventory = new InventoryRecord(inventoryId, _campaign.CampaignId, InventoryOwnerRef.ForCharacter(owner), 1, now, now);
            Assert.That(_inventory.CreateInventory(_campaign, inventory, Command(), Corr).IsSuccess, Is.True);
            ContentDefinitionRef source = ContentDefinitionRef.Parse("cdef_0123456789abcdef0123456789abcdef/1");
            ItemInstanceRecord item = new ItemInstanceRecord(ItemInstanceId.NewId(now), _campaign.CampaignId, inventoryId, InventoryOwnerRef.ForCharacter(owner), InventoryLocationRef.Contained(inventoryId, "main"), source, new ItemMechanicsSnapshot(source, 1, ContentDefinitionType.Item, "{}"), "{}", 1, now, now);
            return _inventory.CreateItemInstance(_campaign, item, Command(), Corr).Value;
        }

        private long Count(string table)
        {
            using SqliteConnection connection = OpenRawConnection();
            using (var exists = connection.CreateCommand())
            {
                exists.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
                exists.Parameters.AddWithValue("$name", table);
                // AttackOutcome/GameLogEntries are created lazily on first use
                // by SqliteAttackApplyRepository -- a count taken before the
                // very first apply-repository call (e.g. around a PreviewAttack
                // that never touches it) must not throw "no such table".
                if (Convert.ToInt64(exists.ExecuteScalar()) == 0) return 0;
            }

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM " + table + ";";
            return Convert.ToInt64(command.ExecuteScalar());
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

        private string GameLogSummary(string logEntryId)
        {
            using SqliteConnection connection = OpenRawConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT SummaryPayload FROM GameLogEntries WHERE LogEntryId = $id;";
            command.Parameters.AddWithValue("$id", logEntryId);
            object? value = command.ExecuteScalar();
            Assert.That(value, Is.Not.Null);
            return (string)value!;
        }

        private SqliteConnection OpenRawConnection()
        {
            var connection = new SqliteConnection("Data Source=" + Path.Combine(_root, "campaign.db"));
            connection.Open();
            return connection;
        }

        private static CommandId Command() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId User() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        /// <summary>
        /// Hand-written test fixture -- there is no production implementation
        /// of <see cref="IAttackRulesEvaluator"/> anywhere in this codebase
        /// (confirmed by direct search), matching every predecessor task's
        /// own fixture. This is not "a real Ruleset" -- it is a stand-in that
        /// lets the real host services (`603`/`604`/`605`/`607`/`609`) be
        /// exercised together against a real database, exactly the honest
        /// limitation this task's own contract discloses.
        /// </summary>
        private sealed class Rules : IAttackRulesEvaluator
        {
            private readonly bool _requiresIntervention;
            private readonly EffectApplicationDecision _effectDecision;
            private readonly CombatDurationBinding? _durationBinding;
            private readonly IReadOnlyList<AttackDelta> _damageDeltas;
            private readonly IReadOnlyList<AttackDelta> _costDeltas;

            public Rules(bool requiresIntervention, EffectApplicationDecision effectDecision, CombatDurationBinding? durationBinding, IReadOnlyList<AttackDelta> damageDeltas, IReadOnlyList<AttackDelta> costDeltas)
            {
                _requiresIntervention = requiresIntervention;
                _effectDecision = effectDecision;
                _durationBinding = durationBinding;
                _damageDeltas = damageDeltas;
                _costDeltas = costDeltas;
            }

            public ProposedAttackResolution Preview(AttackIntent intent, AttackEvaluationSnapshot snapshot) => Resolution(intent, snapshot, null);
            public ProposedAttackResolution Evaluate(AttackIntent intent, AttackEvaluationSnapshot snapshot, AttackRandomSample randomSample) => Resolution(intent, snapshot, randomSample);

            private ProposedAttackResolution Resolution(AttackIntent intent, AttackEvaluationSnapshot snapshot, AttackRandomSample? sample)
            {
                EffectApplicationDecision decision = _requiresIntervention ? EffectApplicationDecision.RequiresIntervention : _effectDecision;
                var effectCandidates = new[] { new AttackEffectCandidate(intent.TargetIds[0], snapshot.ActionSourceRef, snapshot.ActionSourceRef, decision, EffectApplicationReasonCategory.TriggerConditionMet, "fixture", _durationBinding, EffectStackPolicy.IndependentInstances, new EffectMechanicsSnapshot(snapshot.ActionSourceRef, snapshot.ActionSourceRef.Version, ContentDefinitionType.Effect, "{}")) };
                return new ProposedAttackResolution(intent, snapshot, sample, new AttackRangeResult(true, "in-range"), Array.AsReadOnly(new[] { new AttackModifierEntry("fixture", 1) }), new AttackHitResult(true, "hit"), new AttackBodyPartProposal("body"), new AttackArmorProposal("armor", 0), _damageDeltas, _costDeltas, Array.AsReadOnly(effectCandidates));
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
