using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Combat;
using Odyssey.Application.Commands;
using Odyssey.Application.Content;
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
    /// ODY-S06-105: real, end-to-end tests for the first production `IAttackRulesEvaluator` implementation,
    /// `CoreAttackRulesEvaluator` -- every predecessor task (`ODY-S05-601`-`611`, `ODY-S06-101`-`104`) proved
    /// only that hand-written test fixtures compose correctly; nothing before this task ever computed a real
    /// damage value from a real formula, a real attribute, real armor, or a real distance. Every test here
    /// goes through the real `AttackEvaluationService`/`AttackApplyService` production entry points with a
    /// real `CoreAttackRulesEvaluator` instance, against a real temp-directory SQLite campaign -- never a
    /// direct unit call to a private method.
    /// </summary>
    public sealed class CoreAttackRulesEvaluatorIntegrationTests
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
            _root = Path.Combine(Path.GetTempPath(), "ody-s06-105-" + Guid.NewGuid().ToString("N"));
            _clock = new SystemWallClock();
            Result<CampaignHandle> campaign = new SqliteCampaignRepository(_clock).Create(new CreateCampaignRequest(_root, "core-evaluator", "ruleset.core", "1.0.0", "0.1.0"), Command(), Corr);
            Assert.That(campaign.IsSuccess, Is.True);
            _campaign = campaign.Value;
            _characters = new SqliteCharacterRepository(_clock);
            _encounters = new SqliteCombatEncounterRepository(_clock);
            _inventory = new SqliteInventoryRepository(_clock);
            _scenes = new SqliteSceneRepository(_clock);
            _reader = new SqliteAttackStateReader(_encounters, _inventory, _characters, _clock, _scenes);
            _apply = new SqliteAttackApplyRepository(_clock);
        }

        [Test] // TC-ATTACK-117
        public void EvaluateAttack_SimpleDiceFormula_TargetInRange_NoArmor_RealDamageInDieRange()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            InitResource(target, Health);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord weapon = WeaponFor(actor, "1d6", range: 100);

            Result<ProposedAttackResolution> evaluated = AttackEvaluationService.EvaluateAttack(_reader, new CoreAttackRulesEvaluator(), new FixedRandomStreamFactory(10, 20, 30, 40), _campaign, Epoch, Request(encounter, actor, target, weapon));

            Assert.That(evaluated.IsSuccess, Is.True);
            Assert.That(evaluated.Value.Range.IsInRange, Is.True);
            Assert.That(evaluated.Value.Hit.IsHit, Is.True);
            Assert.That(evaluated.Value.DamageDeltas.Count, Is.EqualTo(1));
            // raw=10 mapped onto a d6: ((10-1)%6)+1 = 4.
            Assert.That(evaluated.Value.DamageDeltas[0].Value, Is.EqualTo(-4));
            Assert.That(evaluated.Value.DamageDeltas[0].TargetRef, Is.EqualTo("character:" + target + ":" + Health));
        }

        [Test] // TC-ATTACK-118
        public void EvaluateAttack_TargetBeyondWeaponRange_MissesWithNoDamage()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            InitResource(target, Health);
            SceneId scene = CreateScene();
            LinkToken(scene, actor, 0, 0);
            LinkToken(scene, target, 10, 0);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord weapon = WeaponFor(actor, "1d6", range: 5);

            Result<ProposedAttackResolution> evaluated = AttackEvaluationService.EvaluateAttack(_reader, new CoreAttackRulesEvaluator(), new FixedRandomStreamFactory(10, 20, 30, 40), _campaign, Epoch, Request(encounter, actor, target, weapon));

            Assert.That(evaluated.IsSuccess, Is.True);
            Assert.That(evaluated.Value.Range.IsInRange, Is.False);
            Assert.That(evaluated.Value.Hit.IsHit, Is.False);
            Assert.That(evaluated.Value.DamageDeltas, Is.Empty);
            Assert.That(CurrentValue(target, Health), Is.EqualTo(10), "A miss must not touch the target's own resource.");
        }

        [Test] // TC-ATTACK-119
        public void EvaluateAttack_NoTokenLinked_RangeIsUnconditional_BehaviorUnchangedFromBeforeThisTask()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            InitResource(target, Health);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            // A deliberately tiny range -- if Topology's own "missing data" default were somehow NOT
            // unconditional, this attack would incorrectly miss.
            ItemInstanceRecord weapon = WeaponFor(actor, "1d6", range: 1);

            Result<ProposedAttackResolution> evaluated = AttackEvaluationService.EvaluateAttack(_reader, new CoreAttackRulesEvaluator(), new FixedRandomStreamFactory(10, 20, 30, 40), _campaign, Epoch, Request(encounter, actor, target, weapon));

            Assert.That(evaluated.IsSuccess, Is.True);
            Assert.That(evaluated.Value.Range.IsInRange, Is.True);
            Assert.That(evaluated.Value.Hit.IsHit, Is.True);
            Assert.That(evaluated.Value.DamageDeltas.Count, Is.EqualTo(1));
        }

        [Test] // TC-ATTACK-120
        public void EvaluateAttack_FormulaReferencesActorAttribute_RealAttributeValueIsIncluded()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            InitResource(target, Health);
            GrantAttribute(actor, "Strength", 10);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord weapon = WeaponFor(actor, "1d6+Strength", range: 100);

            Result<ProposedAttackResolution> evaluated = AttackEvaluationService.EvaluateAttack(_reader, new CoreAttackRulesEvaluator(), new FixedRandomStreamFactory(10, 20, 30, 40), _campaign, Epoch, Request(encounter, actor, target, weapon));

            Assert.That(evaluated.IsSuccess, Is.True);
            // raw=10 mapped onto a d6 is 4; +10 Strength => 14.
            Assert.That(evaluated.Value.DamageDeltas[0].Value, Is.EqualTo(-14));
        }

        [Test] // TC-ATTACK-121
        public void EvaluateAttack_TargetWithOneArmorPiece_DamageReducedByProtection_NeverNegative()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            InitResource(target, Health);
            EquipArmor(target, "chest_slot", protection: 3, "Torso");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord weapon = WeaponFor(actor, "5", range: 100);

            Result<ProposedAttackResolution> evaluated = AttackEvaluationService.EvaluateAttack(_reader, new CoreAttackRulesEvaluator(), new FixedRandomStreamFactory(10, 20, 30, 40), _campaign, Epoch, Request(encounter, actor, target, weapon));

            Assert.That(evaluated.IsSuccess, Is.True);
            // Constant formula "5" minus 3 Protection = 2, never negative.
            Assert.That(evaluated.Value.DamageDeltas[0].Value, Is.EqualTo(-2));
            Assert.That(evaluated.Value.Armor, Is.Not.Null);
            Assert.That(evaluated.Value.Armor!.Value.Absorbed, Is.EqualTo(3));
        }

        [Test] // TC-ATTACK-122
        public void EvaluateAttack_TargetWithTwoArmorPieces_ProtectionIsSummed_NotJustOne()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            InitResource(target, Health);
            EquipArmor(target, "chest_slot", protection: 3, "Torso");
            EquipArmor(target, "head_slot", protection: 4, "Head");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord weapon = WeaponFor(actor, "10", range: 100);

            Result<ProposedAttackResolution> evaluated = AttackEvaluationService.EvaluateAttack(_reader, new CoreAttackRulesEvaluator(), new FixedRandomStreamFactory(10, 20, 30, 40), _campaign, Epoch, Request(encounter, actor, target, weapon));

            Assert.That(evaluated.IsSuccess, Is.True);
            // Constant formula "10" minus (3+4)=7 summed Protection = 3 -- not 10-3=7 or 10-4=6.
            Assert.That(evaluated.Value.DamageDeltas[0].Value, Is.EqualTo(-3));
        }

        [Test] // TC-ATTACK-123
        public void EvaluateAttack_TwoDiceTermsInOneFormula_EachConsumesAnIndependentRandomValue()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            InitResource(target, Health);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord weapon = WeaponFor(actor, "2d6+3", range: 100);

            Result<ProposedAttackResolution> evaluated = AttackEvaluationService.EvaluateAttack(_reader, new CoreAttackRulesEvaluator(), new FixedRandomStreamFactory(10, 20, 30, 40), _campaign, Epoch, Request(encounter, actor, target, weapon));

            Assert.That(evaluated.IsSuccess, Is.True);
            // raw=10 -> ((10-1)%6)+1=4; raw=20 -> ((20-1)%6)+1=2 -- two DIFFERENT draws, not the same value
            // reused twice (which would give 4+4=8, not 6). 4+2+3=9.
            Assert.That(evaluated.Value.DamageDeltas[0].Value, Is.EqualTo(-9));
        }

        [Test] // TC-ATTACK-124
        public void PreviewAttack_IsDeterministic_RequiresNoRandomSample_DoesNotMutateState()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            InitResource(target, Health);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord weapon = WeaponFor(actor, "1d6", range: 100);

            Result<ProposedAttackResolution> first = AttackEvaluationService.PreviewAttack(_reader, new CoreAttackRulesEvaluator(), _campaign, Request(encounter, actor, target, weapon));
            Result<ProposedAttackResolution> second = AttackEvaluationService.PreviewAttack(_reader, new CoreAttackRulesEvaluator(), _campaign, Request(encounter, actor, target, weapon));

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(second.IsSuccess, Is.True);
            Assert.That(first.Value.RandomSample.HasValue, Is.False);
            Assert.That(first.Value.DamageDeltas[0].Value, Is.EqualTo(second.Value.DamageDeltas[0].Value), "Preview must be deterministic across repeated calls.");
            Assert.That(CurrentValue(target, Health), Is.EqualTo(10), "Preview must never mutate any persisted state.");
        }

        [Test] // TC-ATTACK-125
        public void ResolveAttack_RealDamageReachesCharacterResourceCurrentValue_ThroughTheExistingApplyPipeline()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            InitResource(target, Health);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord weapon = WeaponFor(actor, "5", range: 100);

            Result<AttackOutcomeRecord> resolved = AttackApplyService.ResolveAttack(_reader, new CoreAttackRulesEvaluator(), new FixedRandomStreamFactory(10, 20, 30, 40), _apply, _campaign, Epoch, Request(encounter, actor, target, weapon));

            Assert.That(resolved.IsSuccess, Is.True);
            Assert.That(CurrentValue(target, Health), Is.EqualTo(5), "Constant formula '5' with no armor: 10 - 5 = 5.");
        }

        // ---- helpers ----

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
                if (resource.ResourceDefinitionId.Equals(resourceKind)) return resource.CurrentValue;
            }

            Assert.Fail("Resource " + resourceKind + " not found on " + characterId + ".");
            return -1;
        }

        private CharacterRecord GrantAttribute(CharacterId characterId, string attributeName, long value)
        {
            CharacterRecord current = _characters.GetCharacter(_campaign, characterId, Corr).Value;
            Result<CharacterRecord> granted = _characters.GrantDevelopmentPoints(_campaign, characterId, 100, "test", User(), actorIsMainGm: true, current.Revisions.MechanicsRevision, Command(), Corr);
            Assert.That(granted.IsSuccess, Is.True);
            Result<CharacterRecord> purchased = _characters.PurchaseAttributeIncrease(_campaign, characterId, AttributeDefinitionId.Parse(attributeName), value, User(), actorIsMainGm: true, granted.Value.Revisions.MechanicsRevision, expectedAttributeRevision: 0, Command(), Corr);
            Assert.That(purchased.IsSuccess, Is.True);
            return purchased.Value;
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

        private SceneId CreateScene() => _scenes.CreateScene(_campaign, "Battle Map " + Guid.NewGuid().ToString("N"), Command(), Corr).Value.SceneId;

        private void LinkToken(SceneId scene, CharacterId characterId, double x, double y)
        {
            Result<TokenRecord> created = _scenes.CreateToken(_campaign, scene, new TokenPosition(x, y), User(), Command(), Corr, characterId);
            Assert.That(created.IsSuccess, Is.True);
        }

        /// <summary>A real, decodable weapon (`TypedDefinitionCodec.EncodeWeapon`), created owned AND
        /// equipped (`ODY-S06-103`'s own equip-status gate requires this for the attack read to succeed).</summary>
        private ItemInstanceRecord WeaponFor(CharacterId owner, string damageExpression, long range)
        {
            var itemDefinition = new ItemDefinition(ItemCategory.Generic, false, null, 1, false, null, false, null, Array.Empty<ContentDefinitionRef>(), Array.Empty<ContentDefinitionRef>());
            var weapon = new WeaponDefinition(itemDefinition, damageExpression, range, WeaponAttackMode.Melee, 1, AmmoRequirement.None, Array.Empty<string>());
            return CreateAndEquipItem(owner, ContentDefinitionType.Weapon, TypedDefinitionCodec.EncodeWeapon(weapon), "main_hand");
        }

        private ItemInstanceRecord CreateAndEquipArmor(CharacterId owner, string equipmentSlotKey, long protection, string coveredBodyPart)
        {
            var itemDefinition = new ItemDefinition(ItemCategory.Generic, false, null, 1, false, null, false, null, Array.Empty<ContentDefinitionRef>(), Array.Empty<ContentDefinitionRef>());
            var armor = new ArmorDefinition(itemDefinition, equipmentSlotKey, new[] { BodyPartId.Parse(coveredBodyPart) }, protection);
            return CreateAndEquipItem(owner, ContentDefinitionType.Armor, TypedDefinitionCodec.EncodeArmor(armor), equipmentSlotKey);
        }

        private void EquipArmor(CharacterId owner, string equipmentSlotKey, long protection, string coveredBodyPart) => CreateAndEquipArmor(owner, equipmentSlotKey, protection, coveredBodyPart);

        private ItemInstanceRecord CreateAndEquipItem(CharacterId owner, ContentDefinitionType contentType, string payload, string equipmentSlotKey)
        {
            UtcInstant now = _clock.GetUtcNow();
            InventoryId inventoryId = InventoryId.NewId(now);
            InventoryRecord inventory = new InventoryRecord(inventoryId, _campaign.CampaignId, InventoryOwnerRef.ForCharacter(owner), 1, now, now);
            Assert.That(_inventory.CreateInventory(_campaign, inventory, Command(), Corr).IsSuccess, Is.True);
            ContentDefinitionRef source = ContentDefinitionRef.Parse("cdef_" + Guid.NewGuid().ToString("N") + "/1");
            ItemInstanceRecord item = new ItemInstanceRecord(ItemInstanceId.NewId(now), _campaign.CampaignId, inventoryId, InventoryOwnerRef.ForCharacter(owner), InventoryLocationRef.Contained(inventoryId, "main"), source, new ItemMechanicsSnapshot(source, 1, contentType, payload), "{}", 1, now, now);
            ItemInstanceRecord created = _inventory.CreateItemInstance(_campaign, item, Command(), Corr).Value;
            var entry = new EquippedEntry(created.InventoryId, InventoryItemRef.ForInstance(created.ItemInstanceId), equipmentSlotKey, Array.Empty<BodyPartId>(), User(), now, 1);
            Result<EquippedEntryRecord> equipped = _inventory.EquipItem(_campaign, new EquipTransition(new EquippedEntryRecord(_campaign.CampaignId, entry), created.Revision, Command()), Corr);
            Assert.That(equipped.IsSuccess, Is.True);
            return _inventory.GetItemInstance(_campaign, created.ItemInstanceId, Corr).Value;
        }

        private static CommandId Command() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId User() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        /// <summary>A test-only, fully deterministic RNG stream returning prescribed raw values by draw index -- lets a test assert an EXACT expected damage value rather than only a plausible range.</summary>
        private sealed class FixedRandomStream : IAuthoritativeRandomStream
        {
            private readonly int[] _values;
            public FixedRandomStream(int[] values) { _values = values; }
            public RandomStreamIdentity Identity => default;
            public Result<RandomSample> NextInclusive(int minInclusive, int maxInclusive, int drawIndex) => Result<RandomSample>.Success(new RandomSample(_values[drawIndex], default!));
        }

        private sealed class FixedRandomStreamFactory : IAuthoritativeRandomStreamFactory
        {
            private readonly int[] _values;
            public FixedRandomStreamFactory(params int[] values) { _values = values; }
            public Result<IAuthoritativeRandomStream> Create(RandomDecisionContext context) => Result<IAuthoritativeRandomStream>.Success(new FixedRandomStream(_values));
        }
    }
}
