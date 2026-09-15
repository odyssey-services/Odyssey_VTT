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
    /// <summary>
    /// ODY-S05-609: real tests for `ADR-029` §1 rule 5/§6 stage 13's own
    /// aggregate-delta commit -- resolving an already-computed `AttackDelta`'s
    /// `TargetRef` (the `character:{characterId}:{resourceKind}`/
    /// `item:{itemInstanceId}:{itemResourceKind}` convention this task
    /// introduces) and applying its already-computed `Value` against
    /// `Character.ResourcesJson`, inside the same atomic-apply transaction as
    /// `604`'s own outcome/Game Log commit. No Ruleset formula is chosen or
    /// evaluated here, and no `605`/`606`/`607`/`610` territory is exercised.
    /// </summary>
    public sealed class AttackAggregateDeltaCommitTests
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
        private SqliteSceneRepository _scenes = null!;
        private SqliteAttackStateReader _reader = null!;
        private SqliteAttackApplyRepository _apply = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "ody-s05-609-" + Guid.NewGuid().ToString("N"));
            _clock = new SystemWallClock();
            Result<CampaignHandle> campaign = new SqliteCampaignRepository(_clock).Create(new CreateCampaignRequest(_root, "attack-aggregate-delta-commit", "ruleset.core", "1.0.0", "0.1.0"), Command(), Corr);
            Assert.That(campaign.IsSuccess, Is.True);
            _campaign = campaign.Value;
            _characters = new SqliteCharacterRepository(_clock);
            _encounters = new SqliteCombatEncounterRepository(_clock);
            _inventory = new SqliteInventoryRepository(_clock);
            _scenes = new SqliteSceneRepository(_clock);
            _reader = new SqliteAttackStateReader(_encounters, _inventory, _characters, _clock, _scenes);
            _apply = new SqliteAttackApplyRepository(_clock);
        }

        [Test] // TC-ATTACK-066
        public void Deltas_are_persisted_between_RecordAttackOutcome_and_atomic_apply()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            InitResource(target, Health);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            string targetRef = CharacterTargetRef(target, Health);
            var rules = new Rules(requiresIntervention: true, damageDeltas: new[] { new AttackDelta(targetRef, -3) }, costDeltas: Array.Empty<AttackDelta>());
            AttackRequest request = Request(encounter, actor, target, item);

            Result<AttackOutcomeRecord> pending = AttackApplyService.ResolveAttack(_reader, rules, new CountingRandomFactory(), _apply, _campaign, Epoch, request);

            Assert.That(pending.IsSuccess, Is.True);
            Assert.That(pending.Value.OutcomeKind, Is.EqualTo(AttackOutcomeKind.Pending));
            string json = DamageDeltasJsonColumn(request.CommandId);
            Assert.That(json, Does.Contain(targetRef));
            Assert.That(json, Does.Contain("-3"));
            Result<AttackOutcomeRecord> reread = _apply.GetOutcome(_campaign, request.CommandId, Corr);
            Assert.That(reread.IsSuccess, Is.True);
            Assert.That(reread.Value.DamageDeltas.Count, Is.EqualTo(1));
            Assert.That(reread.Value.DamageDeltas[0].TargetRef, Is.EqualTo(targetRef));
            Assert.That(reread.Value.DamageDeltas[0].Value, Is.EqualTo(-3));
        }

        [Test] // TC-ATTACK-067
        public void Immediate_acceptance_applies_the_damage_delta_in_the_same_transaction()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            InitResource(target, Health);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            var rules = new Rules(requiresIntervention: false, damageDeltas: new[] { new AttackDelta(CharacterTargetRef(target, Health), -3) }, costDeltas: Array.Empty<AttackDelta>());

            Result<AttackOutcomeRecord> result = AttackApplyService.ResolveAttack(_reader, rules, new CountingRandomFactory(), _apply, _campaign, Epoch, Request(encounter, actor, target, item));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.OutcomeKind, Is.EqualTo(AttackOutcomeKind.Accepted));
            Assert.That(CurrentValue(target, Health), Is.EqualTo(7));
        }

        [Test] // TC-ATTACK-068
        public void RequiresIntervention_delta_applies_only_once_approved_via_ResolveAttackIntervention()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            InitResource(target, Health);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            var rules = new Rules(requiresIntervention: true, damageDeltas: new[] { new AttackDelta(CharacterTargetRef(target, Health), -4) }, costDeltas: Array.Empty<AttackDelta>());
            AttackRequest request = Request(encounter, actor, target, item);

            Result<AttackOutcomeRecord> pending = AttackApplyService.ResolveAttack(_reader, rules, new CountingRandomFactory(), _apply, _campaign, Epoch, request);
            Assert.That(pending.IsSuccess, Is.True);
            Assert.That(CurrentValue(target, Health), Is.EqualTo(10), "No delta on the original, pending step.");

            Result<AttackOutcomeRecord> approved = AttackApplyService.ResolveAttackIntervention(_apply, _campaign, request.CommandId, AttackInterventionResolution.Approve, User(), true, Command(), Corr);

            Assert.That(approved.IsSuccess, Is.True);
            Assert.That(CurrentValue(target, Health), Is.EqualTo(6), "The deferred approval step applies the delta.");
        }

        [Test] // TC-ATTACK-069
        public void RequiresIntervention_rejected_does_not_apply_the_delta()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            InitResource(target, Health);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            var rules = new Rules(requiresIntervention: true, damageDeltas: new[] { new AttackDelta(CharacterTargetRef(target, Health), -4) }, costDeltas: Array.Empty<AttackDelta>());
            AttackRequest request = Request(encounter, actor, target, item);
            Assert.That(AttackApplyService.ResolveAttack(_reader, rules, new CountingRandomFactory(), _apply, _campaign, Epoch, request).IsSuccess, Is.True);

            Result<AttackOutcomeRecord> rejected = AttackApplyService.ResolveAttackIntervention(_apply, _campaign, request.CommandId, AttackInterventionResolution.Reject, User(), true, Command(), Corr);

            Assert.That(rejected.IsSuccess, Is.True);
            Assert.That(rejected.Value.OutcomeKind, Is.EqualTo(AttackOutcomeKind.Rejected));
            Assert.That(CurrentValue(target, Health), Is.EqualTo(10));
        }

        [Test] // TC-ATTACK-070
        public void Multiple_deltas_on_the_same_character_apply_atomically_without_a_revision_conflict()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            InitResource(actor, Health);
            InitResource(target, Health);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            // A damage delta on the target AND a cost delta on the actor --
            // two different characters, but both mutate Character.ResourcesJson
            // inside the same apply transaction, proving no cross-character
            // interference either.
            var rules = new Rules(requiresIntervention: false,
                damageDeltas: new[] { new AttackDelta(CharacterTargetRef(target, Health), -3) },
                costDeltas: new[] { new AttackDelta(CharacterTargetRef(actor, Health), -2) });

            Result<AttackOutcomeRecord> result = AttackApplyService.ResolveAttack(_reader, rules, new CountingRandomFactory(), _apply, _campaign, Epoch, Request(encounter, actor, target, item));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(CurrentValue(target, Health), Is.EqualTo(7));
            Assert.That(CurrentValue(actor, Health), Is.EqualTo(8));
        }

        [Test] // TC-ATTACK-071
        public void Two_deltas_targeting_the_same_character_in_one_attack_both_apply_sequentially()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            InitResource(target, Health);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            string targetRef = CharacterTargetRef(target, Health);
            var rules = new Rules(requiresIntervention: false,
                damageDeltas: new[] { new AttackDelta(targetRef, -3) },
                costDeltas: new[] { new AttackDelta(targetRef, -2) });

            Result<AttackOutcomeRecord> result = AttackApplyService.ResolveAttack(_reader, rules, new CountingRandomFactory(), _apply, _campaign, Epoch, Request(encounter, actor, target, item));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(CurrentValue(target, Health), Is.EqualTo(5), "Both -3 and -2 apply sequentially to the same resource: 10-3-2=5.");
        }

        [Test] // TC-ATTACK-072
        public void An_item_targeted_delta_is_rejected_as_an_unsupported_disclosed_gap_with_no_partial_commit()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            long before = TotalRowCount();
            var rules = new Rules(requiresIntervention: false, damageDeltas: new[] { new AttackDelta("item:" + item.ItemInstanceId + ":durability", -1) }, costDeltas: Array.Empty<AttackDelta>());

            Result<AttackOutcomeRecord> result = AttackApplyService.ResolveAttack(_reader, rules, new CountingRandomFactory(), _apply, _campaign, Epoch, Request(encounter, actor, target, item));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(TotalRowCount(), Is.EqualTo(before), "No partial commit -- not even the AttackOutcome row itself.");
        }

        [Test] // TC-ATTACK-073
        public void An_unparseable_TargetRef_is_rejected_with_no_partial_commit()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            long before = TotalRowCount();
            var rules = new Rules(requiresIntervention: false, damageDeltas: new[] { new AttackDelta("not-a-recognized-target-ref", -1) }, costDeltas: Array.Empty<AttackDelta>());

            Result<AttackOutcomeRecord> result = AttackApplyService.ResolveAttack(_reader, rules, new CountingRandomFactory(), _apply, _campaign, Epoch, Request(encounter, actor, target, item));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(TotalRowCount(), Is.EqualTo(before));
        }

        [Test] // TC-ATTACK-074
        public void A_value_that_would_fall_outside_the_resource_range_rolls_back_the_whole_transaction()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            InitResource(target, Health);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            long before = TotalRowCount();
            // Health starts at 10 with MinimumValue 0 -- a -15 delta would push
            // CurrentValue below the floor.
            var rules = new Rules(requiresIntervention: false, damageDeltas: new[] { new AttackDelta(CharacterTargetRef(target, Health), -15) }, costDeltas: Array.Empty<AttackDelta>());

            Result<AttackOutcomeRecord> result = AttackApplyService.ResolveAttack(_reader, rules, new CountingRandomFactory(), _apply, _campaign, Epoch, Request(encounter, actor, target, item));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(TotalRowCount(), Is.EqualTo(before), "The AttackOutcome row itself (inserted before delta application in the same transaction) is also rolled back.");
            Assert.That(CurrentValue(target, Health), Is.EqualTo(10));
        }

        [Test] // TC-ATTACK-075
        public void Delta_application_never_calls_the_public_SetResourceCurrentValue_or_any_IInventoryRepository_write_method()
        {
            DirectoryInfo? root = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Packages"))) root = root.Parent;
            Assert.That(root, Is.Not.Null);
            string source = File.ReadAllText(Path.Combine(root!.FullName, "Packages", "com.odyssey.persistence", "Runtime", "Sqlite", "SqliteAttackApplyRepository.cs"));
            int deltasStart = source.IndexOf("private static Result ApplyAttackDeltas", StringComparison.Ordinal);
            Assert.That(deltasStart, Is.GreaterThanOrEqualTo(0));
            int deltasRegionEnd = source.IndexOf("private static string SerializeDeltas", deltasStart, StringComparison.Ordinal);
            Assert.That(deltasRegionEnd, Is.GreaterThan(deltasStart));
            string deltaRegion = source.Substring(deltasStart, deltasRegionEnd - deltasStart);
            string codeOnly = StripLineComments(deltaRegion);
            foreach (string forbidden in new[] { "SetResourceCurrentValue", "ICharacterRepository", "IInventoryRepository", ".CreateItemInstance(", ".UpdateItemInstance(" })
            {
                Assert.That(codeOnly, Does.Not.Contain(forbidden), "'" + forbidden + "' must not appear in executable code (comments are excluded from this scan since they explain, in prose, why the call is NOT made).");
            }
        }

        // ---- helpers ----

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

        private string DamageDeltasJsonColumn(CommandId commandId)
        {
            using SqliteConnection connection = OpenRawConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT DamageDeltasJson FROM AttackOutcome WHERE CommandId = $id;";
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
            private readonly bool _requiresIntervention;
            private readonly IReadOnlyList<AttackDelta> _damageDeltas;
            private readonly IReadOnlyList<AttackDelta> _costDeltas;

            public Rules(bool requiresIntervention, IReadOnlyList<AttackDelta> damageDeltas, IReadOnlyList<AttackDelta> costDeltas)
            {
                _requiresIntervention = requiresIntervention;
                _damageDeltas = damageDeltas;
                _costDeltas = costDeltas;
            }

            public ProposedAttackResolution Preview(AttackIntent intent, AttackEvaluationSnapshot snapshot) => Resolution(intent, snapshot, null);
            public ProposedAttackResolution Evaluate(AttackIntent intent, AttackEvaluationSnapshot snapshot, AttackRandomSample randomSample) => Resolution(intent, snapshot, randomSample);

            private ProposedAttackResolution Resolution(AttackIntent intent, AttackEvaluationSnapshot snapshot, AttackRandomSample? sample)
            {
                EffectApplicationDecision decision = _requiresIntervention ? EffectApplicationDecision.RequiresIntervention : EffectApplicationDecision.DoNotApply;
                var effectCandidates = new[] { new AttackEffectCandidate(intent.TargetIds[0], snapshot.ActionSourceRef, snapshot.ActionSourceRef, decision) };
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
