using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Board;
using Odyssey.Application.Combat;
using Odyssey.Application.Commands;
using Odyssey.Application.Content;
using Odyssey.Application.Effects;
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
    /// ODY-S06-108: the final MVP proof -- one real, end-to-end scenario spanning every root command the
    /// MVP roadmap named (blocks A-F): real catalog content authored and published through
    /// `ContentCatalogAuthoringService`/`ContentCatalogLifecycleService` (which internally runs
    /// `CatalogValidationService.ValidateDraftForPublish` before any definition reaches `Published`), two
    /// full characters, real inventory/equipment, a real ability activation, a real item use, a real attack,
    /// a real token move, and a second real attack that the move alone decides. No new production class is
    /// introduced -- every step is a call into an already-accepted public service, exactly mirroring
    /// `ActivateAbilityIntegrationTests.cs`/`UseItemIntegrationTests.cs`/`CoreAttackRulesEvaluatorIntegrationTests.cs`'s
    /// own established "one test class, private helpers only" precedent (`ODY-S06-108`'s own governing ТЗ
    /// §0: no reusable cross-task content-authoring pattern exists anywhere in this codebase yet, and this
    /// task does not invent one).
    /// </summary>
    public sealed class MvpTwoCharacterCombatScenarioTests
    {
        private const string ActiveRuleset = "ruleset.core@1.0.0";
        private static readonly CorrelationId Corr = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly RngKeyEpochId Epoch = RngKeyEpochId.Parse("epoch-001");
        private static readonly ResourceDefinitionId Mana = ResourceDefinitionId.Parse("mana");
        private static readonly ResourceDefinitionId Health = ResourceDefinitionId.Parse("health");
        private static readonly AbilityDefinitionId PowerStrikeKey = AbilityDefinitionId.Parse("MvpPowerStrike");
        private static readonly AnatomyProfileDefinitionId Humanoid = AnatomyProfileDefinitionId.Parse("Humanoid");

        private string _root = null!;
        private CampaignHandle _campaign = null!;
        private IWallClock _clock = null!;
        private SqliteCharacterRepository _characters = null!;
        private SqliteContentCatalogRepository _catalog = null!;
        private SqliteActiveEffectRepository _effects = null!;
        private SqliteInventoryRepository _inventory = null!;
        private SqliteSceneRepository _scenes = null!;
        private SqliteCombatEncounterRepository _encounters = null!;
        private SqliteActivateAbilityStateReader _abilityReader = null!;
        private SqliteActivateAbilityRepository _abilityApply = null!;
        private SqliteUseItemStateReader _useItemReader = null!;
        private SqliteUseItemRepository _useItemApply = null!;
        private SqliteAttackStateReader _attackReader = null!;
        private SqliteAttackApplyRepository _attackApply = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "ody-s06-108-" + Guid.NewGuid().ToString("N"));
            _clock = new SystemWallClock();
            Result<CampaignHandle> campaign = new SqliteCampaignRepository(_clock).Create(new CreateCampaignRequest(_root, "mvp-scenario", "ruleset.core", "1.0.0", "0.1.0"), Command(), Corr);
            Assert.That(campaign.IsSuccess, Is.True);
            _campaign = campaign.Value;
            _characters = new SqliteCharacterRepository(_clock);
            _catalog = new SqliteContentCatalogRepository(_clock);
            _effects = new SqliteActiveEffectRepository(_clock);
            _inventory = new SqliteInventoryRepository(_clock);
            _scenes = new SqliteSceneRepository(_clock);
            _encounters = new SqliteCombatEncounterRepository(_clock);
            _abilityReader = new SqliteActivateAbilityStateReader(_characters, _catalog, _clock);
            _abilityApply = new SqliteActivateAbilityRepository(_clock, _effects);
            _useItemReader = new SqliteUseItemStateReader(_characters, _inventory, _catalog, _clock);
            _useItemApply = new SqliteUseItemRepository(_clock, _effects);
            _attackReader = new SqliteAttackStateReader(_encounters, _inventory, _characters, _clock, _scenes);
            _attackApply = new SqliteAttackApplyRepository(_clock);
        }

        /// <summary>
        /// The whole product-owner scenario in one run: create two characters, grant abilities/skills, add
        /// armor/weapons and a couple of active items, attack one with the other, cross the distance, attack
        /// again. Every intermediate value is asserted exactly (`ODY-S06-108` §1 item 13), not just the final
        /// state, and the numeric design (weapon damage, armor protection, ability/item deltas, weapon range
        /// versus the movement distance) is chosen to be visibly meaningful against the hard-fixed 10-point
        /// `InitializeCharacterResource` starting value, not decorative (`ODY-S06-108` §2).
        /// </summary>
        [Test] // TC-MVP-001
        public void TwoCharacterCombatScenario_FullMvpPath_RealContentRealCommands_ExactValuesAtEveryStep()
        {
            const string strengthAttribute = "Strength";
            CharacterId attacker = Active("Attacker");
            CharacterId defender = Active("Defender");

            InitResource(attacker, Mana);
            InitResource(attacker, Health);
            InitResource(defender, Health);
            GrantAttribute(attacker, strengthAttribute, 2);
            InitializeAnatomy(attacker);
            InitializeAnatomy(defender);

            // Real content, Draft -> Published, through the authoring/lifecycle services (which run
            // CatalogValidationService.ValidateDraftForPublish internally before Published is reachable).
            ContentDefinitionRecord weaponDefinition = PublishWeapon("1d6+" + strengthAttribute, range: 5);
            ContentDefinitionRecord armorDefinition = PublishArmor(protection: 2, "chest_slot", "Torso");
            ContentDefinitionRecord healEffect = PublishInstantEffect(Health, "2");
            ContentDefinitionRecord potionDefinition = PublishConsumableItem(new[] { new ContentDefinitionRef(healEffect.ContentDefinitionId, healEffect.Version) });
            ContentDefinitionRecord abilityDefinition = PublishSelfAdjustResourceAbility(costMana: 2, resourceKind: Health, amountFormula: "-3");

            InventoryRecord attackerInventory = CreateInventory(attacker);
            InventoryRecord defenderInventory = CreateInventory(defender);

            // Real runtime creation from Published definitions only -- never a hand-built ItemStackRecord/
            // ItemInstanceRecord (ODY-S06-108 §3's own explicit prohibition on the shortcut some prior test
            // helpers used for unrelated purposes).
            ItemInstanceRecord weapon = CreateItemInstance(attackerInventory, weaponDefinition);
            ItemInstanceRecord armor = CreateItemInstance(defenderInventory, armorDefinition);
            ItemStackRecord potionStack = CreateItemStack(attackerInventory, potionDefinition, quantity: 2);

            EquipInstance(weapon, attackerInventory, "main_hand", Array.Empty<BodyPartId>());
            EquipInstance(armor, defenderInventory, "chest_slot", new[] { BodyPartId.Parse("Torso") });

            CharacterAbility ability = GrantActivatableAbility(attacker, abilityDefinition);

            SceneId scene = CreateScene();
            LinkToken(scene, attacker, 0, 0);
            TokenRecord defenderToken = LinkToken(scene, defender, 3, 0);

            CombatEncounterRecord encounter = CreateEncounter(attacker, defender);

            // Attack #1: distance 3 <= weapon range 5 -- in range, real damage through the full apply pipeline.
            Result<AttackOutcomeRecord> firstAttack = AttackApplyService.ResolveAttack(_attackReader, new CoreAttackRulesEvaluator(), new FixedRandomStreamFactory(10, 20, 30, 40), _attackApply, _campaign, Epoch, BuildAttackRequest(encounter, attacker, defender, weapon));
            Assert.That(firstAttack.IsSuccess, Is.True, firstAttack.IsFailure ? firstAttack.Error.Code.ToString() : string.Empty);
            // raw=10 mapped onto a d6: ((10-1)%6)+1=4; +2 Strength = 6 weapon damage; -2 armor protection = 4.
            Assert.That(CurrentValue(defender, Health), Is.EqualTo(6), "10 - (6 weapon damage - 2 armor protection) = 6.");

            // Ability activation: self-targeted, deterministic (no dice), real Mana cost and real self-inflicted AdjustResource.
            Result<AbilityActivationRecord> activated = ActivateAbilityService.ActivateAbility(_abilityReader, _abilityApply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, BuildActivateAbilityRequest(attacker, ability.CharacterAbilityId, attacker));
            Assert.That(activated.IsSuccess, Is.True, activated.IsFailure ? activated.Error.Code.ToString() : string.Empty);
            Assert.That(CurrentValue(attacker, Mana), Is.EqualTo(8), "10 - 2 Mana cost = 8.");
            Assert.That(CurrentValue(attacker, Health), Is.EqualTo(7), "10 - 3 self-inflicted AdjustResource = 7.");
            CharacterRecord afterActivation = _characters.GetCharacter(_campaign, attacker, Corr).Value;
            int grantedCount = 0;
            foreach (CharacterAbility candidate in afterActivation.Abilities)
            {
                if (candidate.AbilityDefinitionId.Equals(PowerStrikeKey)) grantedCount++;
            }

            Assert.That(grantedCount, Is.EqualTo(1), "Activation must not duplicate or remove the character's own ability grant.");

            // Item use: heals the self-inflicted damage back up, without ever exceeding the 10-point maximum.
            Result<ItemUsageRecord> used = UseItemService.UseItem(_useItemReader, _useItemApply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, BuildUseItemRequest(attacker, InventoryItemRef.ForStack(potionStack.ItemStackId), potionStack.Revision, attackerInventory.Revision));
            Assert.That(used.IsSuccess, Is.True, used.IsFailure ? used.Error.Code.ToString() : string.Empty);
            Assert.That(CurrentValue(attacker, Health), Is.EqualTo(9), "7 + 2 AdjustResource heal = 9.");
            Result<ItemStackRecord> potionAfterUse = _inventory.GetItemStack(_campaign, potionStack.ItemStackId, Corr);
            Assert.That(potionAfterUse.IsSuccess, Is.True);
            Assert.That(potionAfterUse.Value.Quantity.Value, Is.EqualTo(1), "Exactly one unit of the two-unit stack must be consumed.");

            // Movement: the defender retreats from distance 3 to distance 10, crossing the weapon's own range of 5.
            Result<TokenRecord> moved = BoardMovementService.MoveToken(_scenes, new MoveTokenRequest(_campaign, User(), actorIsMainGm: true, defenderToken.TokenId, new TokenPosition(10, 0), defenderToken.Revision, Command(), Corr));
            Assert.That(moved.IsSuccess, Is.True, moved.IsFailure ? moved.Error.Code.ToString() : string.Empty);

            // Attack #2: same weapon, same encounter -- now out of range. The move alone must be what makes
            // this attack miss (proving movement genuinely changes the outcome, not a decorative distance).
            CombatEncounterRecord encounterAfterMove = _encounters.Get(_campaign, encounter.EncounterId, Corr).Value;
            Result<ProposedAttackResolution> secondAttack = AttackEvaluationService.EvaluateAttack(_attackReader, new CoreAttackRulesEvaluator(), new FixedRandomStreamFactory(10, 20, 30, 40), _campaign, Epoch, BuildAttackRequest(encounterAfterMove, attacker, defender, weapon));
            Assert.That(secondAttack.IsSuccess, Is.True, secondAttack.IsFailure ? secondAttack.Error.Code.ToString() : string.Empty);
            Assert.That(secondAttack.Value.Range.IsInRange, Is.False, "Distance 10 exceeds the weapon's own range of 5.");
            Assert.That(secondAttack.Value.Hit.IsHit, Is.False);
            Assert.That(secondAttack.Value.DamageDeltas, Is.Empty);
            Assert.That(CurrentValue(defender, Health), Is.EqualTo(6), "A miss beyond range must not touch the target's own resource -- unchanged from Attack #1's own result.");
        }

        // ---- helpers ----

        private ContentDefinitionRecord AuthorDraft(ContentDefinitionType type, string name, string propertiesJson, IReadOnlyList<ContentDefinitionRef>? dependencyRefs = null)
        {
            var request = new CreateDraftDefinitionRequest(_campaign, type, name, "ODY-S06-108 MVP fixture.", User(), actorIsMainGm: true, Command(), Corr, rulesetCompatibility: new[] { ActiveRuleset }, propertiesJson: propertiesJson, dependencyRefs: dependencyRefs);
            Result<ContentDefinitionRecord> result = ContentCatalogAuthoringService.CreateDraftDefinition(_catalog, request);
            Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error.Code.ToString() : string.Empty);
            return result.Value;
        }

        private ContentDefinitionRecord PublishFixture(ContentDefinitionRecord draft)
        {
            var request = new PublishDefinitionRequest(_campaign, draft.ContentDefinitionId, draft.Revision, User(), actorIsMainGm: true, Command(), Corr);
            Result<ContentDefinitionRecord> result = ContentCatalogLifecycleService.PublishDefinition(_catalog, request);
            Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error.Code.ToString() : string.Empty);
            return result.Value;
        }

        private ContentDefinitionRecord PublishWeapon(string damageExpression, long range)
        {
            var itemDefinition = new ItemDefinition(ItemCategory.Generic, false, null, weight: 3, false, null, false, null, Array.Empty<ContentDefinitionRef>(), Array.Empty<ContentDefinitionRef>());
            var weapon = new WeaponDefinition(itemDefinition, damageExpression, range, WeaponAttackMode.Melee, actionCost: 1, AmmoRequirement.None, Array.Empty<string>());
            return PublishFixture(AuthorDraft(ContentDefinitionType.Weapon, "MVP Sword " + Guid.NewGuid().ToString("N"), TypedDefinitionCodec.EncodeWeapon(weapon)));
        }

        private ContentDefinitionRecord PublishArmor(long protection, string equipmentSlotKey, string coveredBodyPart)
        {
            var itemDefinition = new ItemDefinition(ItemCategory.Generic, false, null, weight: 5, false, null, false, null, Array.Empty<ContentDefinitionRef>(), Array.Empty<ContentDefinitionRef>());
            var armor = new ArmorDefinition(itemDefinition, equipmentSlotKey, new[] { BodyPartId.Parse(coveredBodyPart) }, protection);
            return PublishFixture(AuthorDraft(ContentDefinitionType.Armor, "MVP Chainmail " + Guid.NewGuid().ToString("N"), TypedDefinitionCodec.EncodeArmor(armor)));
        }

        private ContentDefinitionRecord PublishInstantEffect(ResourceDefinitionId resourceKind, string amountFormula)
        {
            var envelope = new MechanicsPrimitiveEnvelope(1, new MechanicsPrimitive[] { new AdjustResourcePrimitive(resourceKind, amountFormula) });
            var targetRule = new ContentTargetRule(ContentTargetSource.SourceEntity, 1, 1, true);
            var effect = new EffectDefinition(targetRule, EffectDurationType.Instant, durationValue: null, EffectStackPolicy.RefreshDuration, MechanicsPayloadCodec.EncodePrimitives(envelope));
            return PublishFixture(AuthorDraft(ContentDefinitionType.Effect, "MVP Instant Effect " + Guid.NewGuid().ToString("N"), TypedDefinitionCodec.EncodeEffect(effect)));
        }

        private ContentDefinitionRecord PublishConsumableItem(IReadOnlyList<ContentDefinitionRef> builtInEffectRefs)
        {
            var itemDefinition = new ItemDefinition(ItemCategory.Consumable, true, 10, weight: 1, false, null, false, null, Array.Empty<ContentDefinitionRef>(), builtInEffectRefs);
            return PublishFixture(AuthorDraft(ContentDefinitionType.Item, "MVP Healing Draught " + Guid.NewGuid().ToString("N"), TypedDefinitionCodec.EncodeItem(itemDefinition), dependencyRefs: builtInEffectRefs));
        }

        private ContentDefinitionRecord PublishSelfAdjustResourceAbility(long costMana, ResourceDefinitionId resourceKind, string amountFormula)
        {
            var envelope = new MechanicsPrimitiveEnvelope(1, new MechanicsPrimitive[] { new AdjustResourcePrimitive(resourceKind, amountFormula) });
            IReadOnlyList<AbilityResourceCost> costs = costMana > 0 ? new[] { new AbilityResourceCost(Mana, costMana) } : Array.Empty<AbilityResourceCost>();
            var targetRule = new ContentTargetRule(ContentTargetSource.ActingCharacter, 1, 1, true);
            var ability = new AbilityDefinition(AbilityEntryPointType.ActiveAction, "OnUse", actionCost: 0, costs, targetRule, MechanicsPayloadCodec.EncodePrimitives(envelope));
            return PublishFixture(AuthorDraft(ContentDefinitionType.Ability, "MVP Power Strike " + Guid.NewGuid().ToString("N"), TypedDefinitionCodec.EncodeAbility(ability)));
        }

        private InventoryRecord CreateInventory(CharacterId owner)
        {
            UtcInstant now = _clock.GetUtcNow();
            var record = new InventoryRecord(InventoryId.NewId(now), _campaign.CampaignId, InventoryOwnerRef.ForCharacter(owner), 1, now, now);
            Result<InventoryRecord> created = _inventory.CreateInventory(_campaign, record, Command(), Corr);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        /// <summary>Real runtime creation from an already-Published definition -- `InventoryCreationService` is the only permitted path (`ODY-S06-108` §1 item 4 / §3), never a hand-built `ItemInstanceRecord`.</summary>
        private ItemInstanceRecord CreateItemInstance(InventoryRecord inventory, ContentDefinitionRecord definition)
        {
            var request = new CreateItemInstanceFromDefinitionRequest(_campaign, ItemInstanceId.NewId(_clock.GetUtcNow()), inventory.InventoryId, inventory.OwnerRef, InventoryLocationRef.Contained(inventory.InventoryId, "main"), definition.ContentDefinitionId, User(), actorIsMainGm: true, Command(), Corr);
            Result<ItemInstanceRecord> result = InventoryCreationService.CreateItemInstanceFromDefinition(_catalog, _inventory, _clock, request);
            Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error.Code.ToString() : string.Empty);
            return result.Value;
        }

        /// <summary>Real runtime creation from an already-Published definition -- `InventoryCreationService` is the only permitted path (`ODY-S06-108` §1 item 4 / §3), never a hand-built `ItemStackRecord`.</summary>
        private ItemStackRecord CreateItemStack(InventoryRecord inventory, ContentDefinitionRecord definition, long quantity)
        {
            var request = new CreateItemStackFromDefinitionRequest(_campaign, ItemStackId.NewId(_clock.GetUtcNow()), inventory.InventoryId, inventory.OwnerRef, InventoryLocationRef.Contained(inventory.InventoryId, "main"), definition.ContentDefinitionId, ItemStackQuantity.Create(quantity), User(), actorIsMainGm: true, Command(), Corr);
            Result<ItemStackRecord> result = InventoryCreationService.CreateItemStackFromDefinition(_catalog, _inventory, _clock, request);
            Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error.Code.ToString() : string.Empty);
            return result.Value;
        }

        private EquippedEntryRecord EquipInstance(ItemInstanceRecord item, InventoryRecord inventory, string equipmentSlotKey, IReadOnlyList<BodyPartId> bodyPartRefs)
        {
            var request = new EquipRequest(_campaign, InventoryItemRef.ForInstance(item.ItemInstanceId), inventory.InventoryId, item.Revision, equipmentSlotKey, bodyPartRefs, User(), _clock.GetUtcNow(), User(), actorIsMainGm: true, Command(), Corr);
            Result<EquippedEntryRecord> result = EquipmentService.Equip(_inventory, _characters, request);
            Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error.Code.ToString() : string.Empty);
            return result.Value;
        }

        private void InitializeAnatomy(CharacterId characterId)
        {
            CharacterRecord current = _characters.GetCharacter(_campaign, characterId, Corr).Value;
            Result<CharacterRecord> result = _characters.InitializeCharacterAnatomy(_campaign, characterId, Humanoid, User(), actorIsMainGm: true, current.Revisions.CharacterAnatomyRevision, Command(), Corr);
            Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error.Code.ToString() : string.Empty);
        }

        /// <summary>Grants the ability with `sourceRef: null` (`SourceKind.GMGrant`'s own documented default -- provenance, not activation), then links activation eligibility via the separate `LinkAbilityActivationSource` command -- the exact `ODY-S06-106` doработка precedent, never repurposing `SourceRef`.</summary>
        private CharacterAbility GrantActivatableAbility(CharacterId characterId, ContentDefinitionRecord published)
        {
            CharacterRecord current = _characters.GetCharacter(_campaign, characterId, Corr).Value;
            Result<CharacterRecord> acquired = _characters.AcquireAbility(_campaign, characterId, PowerStrikeKey, SourceKind.GMGrant, null, RankMode.None, null, null, "{}", User(), actorIsMainGm: true, null, current.Revisions.CharacterAbilitiesRevision, Command(), Corr);
            Assert.That(acquired.IsSuccess, Is.True, acquired.IsFailure ? acquired.Error.Code.ToString() : string.Empty);
            CharacterAbility? granted = null;
            foreach (CharacterAbility candidate in acquired.Value.Abilities)
            {
                if (candidate.AbilityDefinitionId.Equals(PowerStrikeKey) && candidate.SourceRef == null) { granted = candidate; break; }
            }

            if (granted == null)
            {
                Assert.Fail("Granted ability not found on the reloaded CharacterRecord.");
                return null!;
            }

            var activationRef = new ContentDefinitionRef(published.ContentDefinitionId, published.Version);
            Result<CharacterRecord> linked = _characters.LinkAbilityActivationSource(_campaign, characterId, granted.CharacterAbilityId, activationRef, User(), actorIsMainGm: true, acquired.Value.Revisions.CharacterAbilitiesRevision, Command(), Corr);
            Assert.That(linked.IsSuccess, Is.True, linked.IsFailure ? linked.Error.Code.ToString() : string.Empty);
            foreach (CharacterAbility candidate in linked.Value.Abilities)
            {
                if (candidate.CharacterAbilityId.Equals(granted.CharacterAbilityId)) return candidate;
            }

            Assert.Fail("Linked ability not found on the reloaded CharacterRecord.");
            return null!;
        }

        private ActivateAbilityRequest BuildActivateAbilityRequest(CharacterId actor, CharacterAbilityId characterAbilityId, params CharacterId[] targets)
        {
            CharacterRecord current = _characters.GetCharacter(_campaign, actor, Corr).Value;
            var intent = new ActivateAbilityIntent(actor, characterAbilityId, targets, current.Revisions.CharacterAbilitiesRevision, current.Revisions.CharacterResourcesRevision);
            return new ActivateAbilityRequest(intent, User(), actorIsMainGm: true, Command(), Corr);
        }

        private UseItemRequest BuildUseItemRequest(CharacterId actor, InventoryItemRef item, long expectedItemRevision, long expectedInventoryRevision)
        {
            CharacterRecord current = _characters.GetCharacter(_campaign, actor, Corr).Value;
            var intent = new UseItemIntent(actor, item, expectedItemRevision, expectedInventoryRevision, current.Revisions.CharacterResourcesRevision);
            return new UseItemRequest(intent, User(), actorIsMainGm: true, Command(), Corr);
        }

        private static AttackRequest BuildAttackRequest(CombatEncounterRecord encounter, CharacterId actor, CharacterId target, ItemInstanceRecord item)
            => new AttackRequest(new AttackIntent(encounter.EncounterId, actor, new[] { target }, item.ItemInstanceId, encounter.Revision), User(), true, Command(), Corr);

        private CombatEncounterRecord CreateEncounter(CharacterId actor, CharacterId target)
            => CombatEncounterService.Create(_encounters, _campaign, new CreateCombatEncounterRequest(new[] { actor, target }, User(), true, Command()), Corr).Value;

        private SceneId CreateScene() => _scenes.CreateScene(_campaign, "MVP Battle Map " + Guid.NewGuid().ToString("N"), Command(), Corr).Value.SceneId;

        private TokenRecord LinkToken(SceneId scene, CharacterId characterId, double x, double y)
        {
            Result<TokenRecord> created = _scenes.CreateToken(_campaign, scene, new TokenPosition(x, y), User(), Command(), Corr, characterId);
            Assert.That(created.IsSuccess, Is.True, created.IsFailure ? created.Error.Code.ToString() : string.Empty);
            return created.Value;
        }

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

        private CharacterId Active(string name)
        {
            CharacterId id = _characters.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, name), Command(), Corr).Value.CharacterId;
            CharacterRecord current = _characters.GetCharacter(_campaign, id, Corr).Value;
            return _characters.ApproveCharacterDraft(_campaign, id, true, current.Revisions.LifecycleRevision, Command(), Corr).Value.CharacterId;
        }

        private static CommandId Command() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId User() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        private sealed class ThrowingRandomFactory : IAuthoritativeRandomStreamFactory
        {
            public Result<IAuthoritativeRandomStream> Create(RandomDecisionContext context) => throw new AssertionException("This scenario must not derive an RNG stream at all.");
        }

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
