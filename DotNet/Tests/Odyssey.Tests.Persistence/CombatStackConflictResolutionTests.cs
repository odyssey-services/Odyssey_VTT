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
    /// ODY-S05-610: real tests for `ADR-028` §7 rule 7's own durable
    /// `ActiveEffectStackConflict` persistence and MainGM resolution command
    /// -- closes `606`'s own disclosed gap (`TC-ATTACK-054`): a combat effect
    /// candidate whose `EffectStackPolicy` resolves to `RequestGMResolution`
    /// now creates a durable pending record (in the same atomic-apply
    /// transaction as the rest of the attack) instead of creating no row and
    /// reaching no GM at all. No change to `ActiveEffectStackingRules`'s own
    /// pure decision layer (`ODY-S05-503`, reused exactly as-is); no
    /// `605`/`607`/`609` territory exercised here.
    /// </summary>
    public sealed class CombatStackConflictResolutionTests
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
            _root = Path.Combine(Path.GetTempPath(), "ody-s05-610-" + Guid.NewGuid().ToString("N"));
            _clock = new SystemWallClock();
            Result<CampaignHandle> campaign = new SqliteCampaignRepository(_clock).Create(new CreateCampaignRequest(_root, "combat-stack-conflict-resolution", "ruleset.core", "1.0.0", "0.1.0"), Command(), Corr);
            Assert.That(campaign.IsSuccess, Is.True);
            _campaign = campaign.Value;
            _characters = new SqliteCharacterRepository(_clock);
            _encounters = new SqliteCombatEncounterRepository(_clock);
            _inventory = new SqliteInventoryRepository(_clock);
            _activeEffects = new SqliteActiveEffectRepository(_clock);
            _reader = new SqliteAttackStateReader(_encounters, _inventory, _characters, _clock);
            _apply = new SqliteAttackApplyRepository(_clock);
        }

        [Test] // TC-ATTACK-084
        public void RequestGMResolution_candidate_creates_a_durable_pending_record_atomically_with_the_rest_of_apply()
        {
            (CommandId raisingCommandId, ActiveEffectId conflictingId, CharacterId target) = RaiseConflict();

            (string conflictStatus, string activeEffectId) = ReadStackConflictRow(raisingCommandId, conflictingId);
            Assert.That(conflictStatus, Is.EqualTo("Pending"));
            Assert.That(activeEffectId, Is.Not.Empty);
            Assert.That(Count("AttackOutcome"), Is.EqualTo(2), "The first attack creates the effect to conflict against; the second is the one that raises the conflict -- the pending conflict row lands in the SAME transaction as that second attack's own commit.");
        }

        [Test] // TC-ATTACK-085
        public void An_unresolved_conflict_still_commits_the_rest_of_the_attack_and_creates_the_pending_record()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            ContentDefinitionRef effectRef = EffectRef();
            var firstRules = new Rules(_ => new[] { Candidate(target, effectRef, EffectStackPolicy.RequestGMResolution) });
            Assert.That(AttackApplyService.ResolveAttack(_reader, firstRules, new CountingRandomFactory(), _apply, _campaign, Epoch, Request(encounter, actor, target, item)).IsSuccess, Is.True);

            encounter = Advance(Advance(encounter));
            var secondRules = new Rules(_ => new[] { Candidate(target, effectRef, EffectStackPolicy.RequestGMResolution) });
            AttackRequest secondRequest = Request(encounter, actor, target, item);
            Result<AttackOutcomeRecord> second = AttackApplyService.ResolveAttack(_reader, secondRules, new CountingRandomFactory(), _apply, _campaign, Epoch, secondRequest);

            Assert.That(second.IsSuccess, Is.True, "TC-ATTACK-054's own invariant: the overall attack outcome still commits even though this candidate's own stacking conflict is deferred.");
            IReadOnlyList<ActiveEffectRecord> effects = ActiveEffectsFor(target);
            Assert.That(effects.Count, Is.EqualTo(1), "Still no second ActiveEffect row -- only a pending conflict record now exists for it.");
            (string conflictStatus, _) = ReadStackConflictRow(secondRequest.CommandId, effects[0].Effect.ActiveEffectId);
            Assert.That(conflictStatus, Is.EqualTo("Pending"), "The second candidate's own conflict is now durably recorded, unlike before this task.");
        }

        [Test] // TC-ATTACK-086
        public void MainGM_resolution_with_ApplyAsIndependentInstance_creates_a_new_ActiveEffect()
        {
            (CommandId raisingCommandId, ActiveEffectId conflictingId, CharacterId target) = RaiseConflict();
            long effectsBefore = ActiveEffectsFor(target).Count;

            Result<CombatStackConflictRecord> result = AttackApplyService.ResolveStackConflict(_apply, _campaign, raisingCommandId, conflictingId, ActiveEffectStackConflictResolution.ApplyAsIndependentInstance, User(), true, Command(), Corr);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsResolved, Is.True);
            IReadOnlyList<ActiveEffectRecord> effects = ActiveEffectsFor(target);
            Assert.That(effects.Count, Is.EqualTo(effectsBefore + 1), "A brand-new, independent row -- the conflicting row is untouched.");
            Assert.That(effects, Has.Some.Matches<ActiveEffectRecord>(e => e.Effect.ActiveEffectId.Equals(conflictingId) && e.Effect.Status == ActiveEffectStatus.Active), "The original conflicting effect is left exactly as it was.");
        }

        [Test] // TC-ATTACK-087
        public void MainGM_resolution_with_Replace_removes_the_conflicting_effect_and_creates_the_new_one()
        {
            (CommandId raisingCommandId, ActiveEffectId conflictingId, CharacterId target) = RaiseConflict();

            Result<CombatStackConflictRecord> result = AttackApplyService.ResolveStackConflict(_apply, _campaign, raisingCommandId, conflictingId, ActiveEffectStackConflictResolution.Replace, User(), true, Command(), Corr);

            Assert.That(result.IsSuccess, Is.True);
            IReadOnlyList<ActiveEffectRecord> effects = ActiveEffectsFor(target);
            Assert.That(effects.Count, Is.EqualTo(2), "The old row (now Removed) survives as history; the new row is a separate record.");
            ActiveEffectRecord oldRow = Find(effects, conflictingId);
            Assert.That(oldRow.Effect.Status, Is.EqualTo(ActiveEffectStatus.Removed));
            Assert.That(effects, Has.Some.Matches<ActiveEffectRecord>(e => !e.Effect.ActiveEffectId.Equals(conflictingId) && e.Effect.Status == ActiveEffectStatus.Active), "A new, active row replaces it.");
        }

        [Test] // TC-ATTACK-088
        public void MainGM_resolution_with_Ignore_creates_nothing_and_marks_the_conflict_resolved()
        {
            (CommandId raisingCommandId, ActiveEffectId conflictingId, CharacterId target) = RaiseConflict();
            long effectsBefore = ActiveEffectsFor(target).Count;

            Result<CombatStackConflictRecord> result = AttackApplyService.ResolveStackConflict(_apply, _campaign, raisingCommandId, conflictingId, ActiveEffectStackConflictResolution.Ignore, User(), true, Command(), Corr);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(ActiveEffectsFor(target).Count, Is.EqualTo(effectsBefore), "Ignore creates or mutates no ActiveEffect row.");
            (string conflictStatus, _) = ReadStackConflictRow(raisingCommandId, conflictingId);
            Assert.That(conflictStatus, Is.EqualTo("Resolved"), "The pending record itself is marked resolved, not deleted.");
        }

        [Test] // TC-ATTACK-089
        public void A_non_MainGM_resolution_attempt_is_denied_with_no_mutation()
        {
            (CommandId raisingCommandId, ActiveEffectId conflictingId, CharacterId target) = RaiseConflict();
            long before = TotalRowCount();

            Result<CombatStackConflictRecord> result = AttackApplyService.ResolveStackConflict(_apply, _campaign, raisingCommandId, conflictingId, ActiveEffectStackConflictResolution.ApplyAsIndependentInstance, User(), false, Command(), Corr);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(TotalRowCount(), Is.EqualTo(before));
        }

        [Test] // TC-ATTACK-090
        public void Resolving_an_already_resolved_conflict_a_second_time_is_a_CAS_failure()
        {
            (CommandId raisingCommandId, ActiveEffectId conflictingId, CharacterId target) = RaiseConflict();
            Assert.That(AttackApplyService.ResolveStackConflict(_apply, _campaign, raisingCommandId, conflictingId, ActiveEffectStackConflictResolution.Ignore, User(), true, Command(), Corr).IsSuccess, Is.True);

            Result<CombatStackConflictRecord> second = AttackApplyService.ResolveStackConflict(_apply, _campaign, raisingCommandId, conflictingId, ActiveEffectStackConflictResolution.ApplyAsIndependentInstance, User(), true, Command(), Corr);

            Assert.That(second.IsFailure, Is.True);
            Assert.That(ActiveEffectsFor(target).Count, Is.EqualTo(1), "The second, different resolving command never applies -- no new row from the rejected ApplyAsIndependentInstance attempt.");
        }

        [Test] // TC-ATTACK-091
        public void Retrying_the_same_resolving_CommandId_is_idempotent_and_never_double_applies()
        {
            (CommandId raisingCommandId, ActiveEffectId conflictingId, CharacterId target) = RaiseConflict();
            CommandId resolvingCommandId = Command();

            Result<CombatStackConflictRecord> first = AttackApplyService.ResolveStackConflict(_apply, _campaign, raisingCommandId, conflictingId, ActiveEffectStackConflictResolution.ApplyAsIndependentInstance, User(), true, resolvingCommandId, Corr);
            Assert.That(first.IsSuccess, Is.True);
            long effectsAfterFirst = ActiveEffectsFor(target).Count;

            Result<CombatStackConflictRecord> retry = AttackApplyService.ResolveStackConflict(_apply, _campaign, raisingCommandId, conflictingId, ActiveEffectStackConflictResolution.ApplyAsIndependentInstance, User(), true, resolvingCommandId, Corr);

            Assert.That(retry.IsSuccess, Is.True);
            Assert.That(ActiveEffectsFor(target).Count, Is.EqualTo(effectsAfterFirst), "A retry with the same resolving CommandId never re-applies the decision.");
        }

        [Test] // TC-ATTACK-092
        public void Resolution_never_calls_the_public_CreateActiveEffect_method()
        {
            DirectoryInfo? root = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Packages"))) root = root.Parent;
            Assert.That(root, Is.Not.Null);
            string source = File.ReadAllText(Path.Combine(root!.FullName, "Packages", "com.odyssey.persistence", "Runtime", "Sqlite", "SqliteAttackApplyRepository.cs"));
            int start = source.IndexOf("public Result<CombatStackConflictRecord> ResolveStackConflict", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            int end = source.IndexOf("private static Result<CombatStackConflictRecord> ReplayStackConflictResolution", start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start));
            string region = StripLineComments(source.Substring(start, end - start));
            Assert.That(region, Does.Not.Contain(".CreateActiveEffect("), "Resolution must apply the decision via the shared internal SQL helpers only, never the public IActiveEffectRepository.CreateActiveEffect.");
        }

        // ---- helpers (composition only, no new production type) ----

        private (CommandId RaisingCommandId, ActiveEffectId ConflictingActiveEffectId, CharacterId Target) RaiseConflict()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            ContentDefinitionRef effectRef = EffectRef();
            var firstRules = new Rules(_ => new[] { Candidate(target, effectRef, EffectStackPolicy.RequestGMResolution) });
            Result<AttackOutcomeRecord> first = AttackApplyService.ResolveAttack(_reader, firstRules, new CountingRandomFactory(), _apply, _campaign, Epoch, Request(encounter, actor, target, item));
            Assert.That(first.IsSuccess, Is.True);
            ActiveEffectId firstActiveEffectId = ActiveEffectsFor(target)[0].Effect.ActiveEffectId;

            encounter = Advance(Advance(encounter));
            var secondRules = new Rules(_ => new[] { Candidate(target, effectRef, EffectStackPolicy.RequestGMResolution) });
            AttackRequest secondRequest = Request(encounter, actor, target, item);
            Result<AttackOutcomeRecord> second = AttackApplyService.ResolveAttack(_reader, secondRules, new CountingRandomFactory(), _apply, _campaign, Epoch, secondRequest);
            Assert.That(second.IsSuccess, Is.True);

            return (secondRequest.CommandId, firstActiveEffectId, target);
        }

        private static string StripLineComments(string source)
        {
            var lines = source.Split('\n');
            var builder = new System.Text.StringBuilder();
            foreach (string line in lines)
            {
                int index = line.IndexOf("//", StringComparison.Ordinal);
                builder.AppendLine(index >= 0 ? line.Substring(0, index) : line);
            }

            return builder.ToString();
        }

        private static ActiveEffectRecord Find(IReadOnlyList<ActiveEffectRecord> effects, ActiveEffectId id)
        {
            foreach (ActiveEffectRecord effect in effects)
            {
                if (effect.Effect.ActiveEffectId.Equals(id)) return effect;
            }

            Assert.Fail("ActiveEffect " + id + " not found.");
            return null!;
        }

        private IReadOnlyList<ActiveEffectRecord> ActiveEffectsFor(CharacterId target)
            => _activeEffects.ListActiveEffectsByTarget(_campaign, _campaign.CampaignId, ActiveEffectTargetRef.ForCharacter(target), Corr).Value;

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

        private static ContentDefinitionRef EffectRef() => new ContentDefinitionRef(ContentDefinitionId.NewId(new SystemWallClock().GetUtcNow()), 1);

        private static AttackEffectCandidate Candidate(CharacterId target, ContentDefinitionRef effectRef, EffectStackPolicy stackPolicy)
        {
            var snapshot = new EffectMechanicsSnapshot(effectRef, effectRef.Version, ContentDefinitionType.Effect, "{}");
            return new AttackEffectCandidate(target, effectRef, effectRef, EffectApplicationDecision.Apply, EffectApplicationReasonCategory.TriggerConditionMet, "fixture", null, stackPolicy, snapshot);
        }

        private long Count(string table)
        {
            using SqliteConnection connection = OpenRawConnection();
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

        private (string ConflictStatus, string ActiveEffectId) ReadStackConflictRow(CommandId raisingCommandId, ActiveEffectId conflictingActiveEffectId)
        {
            using SqliteConnection connection = OpenRawConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT ConflictStatus, ActiveEffectId FROM CombatStackConflict WHERE CommandId = $commandId AND ConflictingActiveEffectId = $conflictingActiveEffectId;";
            command.Parameters.AddWithValue("$commandId", raisingCommandId.ToString());
            command.Parameters.AddWithValue("$conflictingActiveEffectId", conflictingActiveEffectId.ToString());
            using SqliteDataReader reader = command.ExecuteReader();
            Assert.That(reader.Read(), Is.True, "No CombatStackConflict row found for the raising command/conflicting effect pair.");
            return (reader.GetString(0), reader.GetString(1));
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
