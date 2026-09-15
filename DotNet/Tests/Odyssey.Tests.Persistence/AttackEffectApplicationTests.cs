using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Odyssey.Application.Combat;
using Odyssey.Application.Commands;
using Odyssey.Application.Effects;
using Odyssey.Application.Inventory;
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

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S05-606: real tests for `ADR-029` section 8's combat
    /// `ActiveEffect` application -- an accepted attack's `Apply` candidates
    /// (immediate or approved-via-intervention) are inserted into the
    /// existing `ActiveEffect` table inside the same atomic-apply
    /// transaction as `AttackOutcome`/Game Log, routed through the existing
    /// `ADR-028` `ActiveEffectStackingRules` decision layer. No
    /// `AttackDelta`/Character/Item state, `605`'s expiry mechanism, or
    /// `607`'s compensation/projection logic is exercised here.
    /// </summary>
    public sealed class AttackEffectApplicationTests
    {
        private static readonly CorrelationId Corr = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly RngKeyEpochId Epoch = RngKeyEpochId.Parse("epoch-001");
        private string _root = null!;
        private CampaignHandle _campaign = null!;
        private IWallClock _clock = null!;
        private SqliteCharacterRepository _characters = null!;
        private SqliteCombatEncounterRepository _encounters = null!;
        private SqliteInventoryRepository _inventory = null!;
        private SqliteActiveEffectRepository _activeEffects = null!;
        private SqliteAttackStateReader _reader = null!;
        private SqliteAttackApplyRepository _apply = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "ody-s05-606-" + Guid.NewGuid().ToString("N"));
            _clock = new SystemWallClock();
            Result<CampaignHandle> campaign = new SqliteCampaignRepository(_clock).Create(new CreateCampaignRequest(_root, "attack-effect-application", "ruleset.core", "1.0.0", "0.1.0"), Command(), Corr);
            Assert.That(campaign.IsSuccess, Is.True);
            _campaign = campaign.Value;
            _characters = new SqliteCharacterRepository(_clock);
            _encounters = new SqliteCombatEncounterRepository(_clock);
            _inventory = new SqliteInventoryRepository(_clock);
            _activeEffects = new SqliteActiveEffectRepository(_clock);
            _reader = new SqliteAttackStateReader(_encounters, _inventory, _characters, _clock);
            _apply = new SqliteAttackApplyRepository(_clock);
        }

        [Test] // TC-ATTACK-047
        public void Apply_candidate_creates_an_ActiveEffect_row_with_the_correct_fields_including_combat_binding()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            ContentDefinitionRef effectRef = EffectRef();
            var binding = new CombatDurationBinding(encounter.EncounterId, actor, target, encounter.RoundOrdinal, 0, 1);
            var rules = new Rules(_ => new[] { Candidate(target, effectRef, EffectApplicationDecision.Apply, binding) });

            Result<AttackOutcomeRecord> result = AttackApplyService.ResolveAttack(_reader, rules, new CountingRandomFactory(), _apply, _campaign, Epoch, Request(encounter, actor, target, item));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.OutcomeKind, Is.EqualTo(AttackOutcomeKind.Accepted));
            IReadOnlyList<ActiveEffectRecord> effects = ActiveEffectsFor(target);
            Assert.That(effects.Count, Is.EqualTo(1));
            ActiveEffect effect = effects[0].Effect;
            Assert.That(effect.EffectDefinitionRef, Is.EqualTo(effectRef));
            Assert.That(effect.SourceRef.Kind, Is.EqualTo(ActiveEffectSourceKind.Action));
            Assert.That(effect.TargetRef.CharacterId, Is.EqualTo(target));
            Assert.That(effect.Status, Is.EqualTo(ActiveEffectStatus.Active));
            Assert.That(effect.CombatBinding.HasValue, Is.True);
            Assert.That(effect.CombatBinding!.Value.EncounterId, Is.EqualTo(encounter.EncounterId));
            Assert.That(effect.CombatBinding.Value.SourceCombatantId, Is.EqualTo(actor));
            Assert.That(effect.CombatBinding.Value.TargetCombatantId, Is.EqualTo(target));
        }

        [Test] // TC-ATTACK-048
        public void DoNotApply_candidate_creates_no_ActiveEffect_row()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            var rules = new Rules(_ => new[] { Candidate(target, EffectRef(), EffectApplicationDecision.DoNotApply, null) });

            Result<AttackOutcomeRecord> result = AttackApplyService.ResolveAttack(_reader, rules, new CountingRandomFactory(), _apply, _campaign, Epoch, Request(encounter, actor, target, item));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.OutcomeKind, Is.EqualTo(AttackOutcomeKind.Accepted));
            Assert.That(ActiveEffectsFor(target).Count, Is.Zero);
        }

        [Test] // TC-ATTACK-049
        public void RequiresIntervention_candidate_creates_its_row_only_once_approved_via_ResolveAttackIntervention()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            ContentDefinitionRef effectRef = EffectRef();
            var rules = new Rules(_ => new[] { Candidate(target, effectRef, EffectApplicationDecision.RequiresIntervention, null) });
            AttackRequest request = Request(encounter, actor, target, item);

            Result<AttackOutcomeRecord> pending = AttackApplyService.ResolveAttack(_reader, rules, new CountingRandomFactory(), _apply, _campaign, Epoch, request);
            Assert.That(pending.IsSuccess, Is.True);
            Assert.That(pending.Value.OutcomeKind, Is.EqualTo(AttackOutcomeKind.Pending));
            Assert.That(ActiveEffectsFor(target).Count, Is.Zero, "No row on the original, pending step.");

            Result<AttackOutcomeRecord> approved = AttackApplyService.ResolveAttackIntervention(_apply, _campaign, request.CommandId, AttackInterventionResolution.Approve, User(), true, Command(), Corr);

            Assert.That(approved.IsSuccess, Is.True);
            Assert.That(ActiveEffectsFor(target).Count, Is.EqualTo(1), "The deferred step creates the row.");
        }

        [Test] // TC-ATTACK-050
        public void RequiresIntervention_candidate_rejected_creates_no_row()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            var rules = new Rules(_ => new[] { Candidate(target, EffectRef(), EffectApplicationDecision.RequiresIntervention, null) });
            AttackRequest request = Request(encounter, actor, target, item);
            Assert.That(AttackApplyService.ResolveAttack(_reader, rules, new CountingRandomFactory(), _apply, _campaign, Epoch, request).IsSuccess, Is.True);

            Result<AttackOutcomeRecord> rejected = AttackApplyService.ResolveAttackIntervention(_apply, _campaign, request.CommandId, AttackInterventionResolution.Reject, User(), true, Command(), Corr);

            Assert.That(rejected.IsSuccess, Is.True);
            Assert.That(rejected.Value.OutcomeKind, Is.EqualTo(AttackOutcomeKind.Rejected));
            Assert.That(ActiveEffectsFor(target).Count, Is.Zero);
        }

        [Test] // TC-ATTACK-051
        public void EffectCandidates_are_persisted_between_RecordAttackOutcome_and_ResolveAttackIntervention()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            ContentDefinitionRef effectRef = EffectRef();
            var rules = new Rules(_ => new[] { Candidate(target, effectRef, EffectApplicationDecision.RequiresIntervention, null) });
            AttackRequest request = Request(encounter, actor, target, item);
            Assert.That(AttackApplyService.ResolveAttack(_reader, rules, new CountingRandomFactory(), _apply, _campaign, Epoch, request).IsSuccess, Is.True);

            string json = EffectCandidatesJsonColumn(request.CommandId);

            Assert.That(json, Does.Contain(effectRef.ToString()));
            Assert.That(json, Does.Contain("RequiresIntervention"));
            Result<AttackOutcomeRecord> reread = _apply.GetOutcome(_campaign, request.CommandId, Corr);
            Assert.That(reread.IsSuccess, Is.True);
            Assert.That(reread.Value.EffectCandidates.Count, Is.EqualTo(1));
            Assert.That(reread.Value.EffectCandidates[0].EffectRef, Is.EqualTo(effectRef));
            Assert.That(reread.Value.EffectCandidates[0].Decision, Is.EqualTo(EffectApplicationDecision.RequiresIntervention));
        }

        [Test] // TC-ATTACK-052
        public void A_guard_failure_at_apply_time_creates_neither_an_AttackOutcome_nor_an_ActiveEffect_row()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            var rules = new Rules(_ => new[] { Candidate(target, EffectRef(), EffectApplicationDecision.Apply, null) });
            AttackIntent staleIntent = new AttackIntent(encounter.EncounterId, actor, new[] { target }, item.ItemInstanceId, encounter.Revision + 1);
            AttackRequest request = new AttackRequest(staleIntent, User(), true, Command(), Corr);
            long before = TotalRowCount();

            Result<AttackOutcomeRecord> result = AttackApplyService.ResolveAttack(_reader, rules, new CountingRandomFactory(), _apply, _campaign, Epoch, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(TotalRowCount(), Is.EqualTo(before));
            Assert.That(ActiveEffectsFor(target).Count, Is.Zero);
        }

        [Test] // TC-ATTACK-053
        public void Colliding_candidates_route_through_the_existing_EffectStackPolicy_decision_layer()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            ContentDefinitionRef effectRef = EffectRef();

            // First application: creates the row (nothing to stack against yet).
            var firstRules = new Rules(_ => new[] { Candidate(target, effectRef, EffectApplicationDecision.Apply, null, EffectStackPolicy.IncreaseStacks) });
            Assert.That(AttackApplyService.ResolveAttack(_reader, firstRules, new CountingRandomFactory(), _apply, _campaign, Epoch, Request(encounter, actor, target, item)).IsSuccess, Is.True);
            IReadOnlyList<ActiveEffectRecord> afterFirst = ActiveEffectsFor(target);
            Assert.That(afterFirst.Count, Is.EqualTo(1));
            Assert.That(afterFirst[0].Effect.StackCount, Is.EqualTo(1));

            // Second application of the SAME effect definition with IncreaseStacks: no
            // second row, existing StackCount increments -- ActiveEffectStackingRules
            // (ODY-S05-503), not a new/duplicated decision.
            encounter = Advance(Advance(encounter));
            var secondRules = new Rules(_ => new[] { Candidate(target, effectRef, EffectApplicationDecision.Apply, null, EffectStackPolicy.IncreaseStacks) });
            Assert.That(AttackApplyService.ResolveAttack(_reader, secondRules, new CountingRandomFactory(), _apply, _campaign, Epoch, Request(encounter, actor, target, item)).IsSuccess, Is.True);

            IReadOnlyList<ActiveEffectRecord> afterSecond = ActiveEffectsFor(target);
            Assert.That(afterSecond.Count, Is.EqualTo(1), "IncreaseStacks must not create a second row.");
            Assert.That(afterSecond[0].Effect.ActiveEffectId, Is.EqualTo(afterFirst[0].Effect.ActiveEffectId));
            Assert.That(afterSecond[0].Effect.StackCount, Is.EqualTo(2));
        }

        [Test] // TC-ATTACK-054
        public void RequestGMResolution_collision_creates_no_row_and_does_not_block_the_attack_outcome()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            ContentDefinitionRef effectRef = EffectRef();
            var firstRules = new Rules(_ => new[] { Candidate(target, effectRef, EffectApplicationDecision.Apply, null, EffectStackPolicy.RequestGMResolution) });
            Assert.That(AttackApplyService.ResolveAttack(_reader, firstRules, new CountingRandomFactory(), _apply, _campaign, Epoch, Request(encounter, actor, target, item)).IsSuccess, Is.True);
            Assert.That(ActiveEffectsFor(target).Count, Is.EqualTo(1));

            encounter = Advance(Advance(encounter));
            var secondRules = new Rules(_ => new[] { Candidate(target, effectRef, EffectApplicationDecision.Apply, null, EffectStackPolicy.RequestGMResolution) });
            Result<AttackOutcomeRecord> second = AttackApplyService.ResolveAttack(_reader, secondRules, new CountingRandomFactory(), _apply, _campaign, Epoch, Request(encounter, actor, target, item));

            Assert.That(second.IsSuccess, Is.True, "The overall attack outcome still commits even though this candidate's own stacking conflict is deferred.");
            Assert.That(ActiveEffectsFor(target).Count, Is.EqualTo(1), "No second row -- RequestGMResolution has no durable conflict persistence in this codebase (a disclosed limitation).");
        }

        // ---- helpers ----

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

        private static ContentDefinitionRef EffectRef() => new ContentDefinitionRef(ContentDefinitionId.NewId(new SystemWallClock().GetUtcNow()), 1);

        private static AttackEffectCandidate Candidate(CharacterId target, ContentDefinitionRef effectRef, EffectApplicationDecision decision, CombatDurationBinding? binding, EffectStackPolicy stackPolicy = EffectStackPolicy.IndependentInstances)
        {
            var snapshot = new EffectMechanicsSnapshot(effectRef, effectRef.Version, ContentDefinitionType.Effect, "{}");
            return new AttackEffectCandidate(target, effectRef, effectRef, decision, EffectApplicationReasonCategory.TriggerConditionMet, "fixture", binding, stackPolicy, snapshot);
        }

        private IReadOnlyList<ActiveEffectRecord> ActiveEffectsFor(CharacterId target)
            => _activeEffects.ListActiveEffectsByTarget(_campaign, _campaign.CampaignId, ActiveEffectTargetRef.ForCharacter(target), Corr).Value;

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

        private string EffectCandidatesJsonColumn(CommandId commandId)
        {
            using SqliteConnection connection = OpenRawConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT EffectCandidatesJson FROM AttackOutcome WHERE CommandId = $id;";
            command.Parameters.AddWithValue("$id", commandId.ToString());
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

        private sealed class Rules : IAttackRulesEvaluator
        {
            private readonly Func<AttackIntent, AttackEffectCandidate[]> _candidates;
            public Rules(Func<AttackIntent, AttackEffectCandidate[]> candidates) { _candidates = candidates; }
            public ProposedAttackResolution Preview(AttackIntent intent, AttackEvaluationSnapshot snapshot) => Resolution(intent, snapshot, null);
            public ProposedAttackResolution Evaluate(AttackIntent intent, AttackEvaluationSnapshot snapshot, AttackRandomSample randomSample) => Resolution(intent, snapshot, randomSample);

            private ProposedAttackResolution Resolution(AttackIntent intent, AttackEvaluationSnapshot snapshot, AttackRandomSample? sample)
            {
                AttackEffectCandidate[] effectCandidates = _candidates(intent);
                // ODY-S05-609: this fixture's own tests exercise ActiveEffect
                // application/stacking, not delta application -- empty delta
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
