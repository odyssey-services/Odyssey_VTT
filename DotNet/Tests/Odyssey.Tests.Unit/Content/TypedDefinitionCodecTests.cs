using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Application.Content;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Tests.Unit.Content
{
    /// <summary>
    /// ODY-S05-105: pure, in-memory round-trip tests for
    /// <see cref="TypedDefinitionCodec"/> -- no SQLite, no repository, no
    /// campaign. Base Definition Types only: structural shape/round-trip
    /// proof, deliberately no game-usability validation (that is
    /// `ODY-S05-104`'s own job).
    /// </summary>
    public sealed class TypedDefinitionCodecTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly UtcInstant Now = UtcInstant.Parse("2026-09-04T00:00:00.0000000Z");

        private static ContentDefinitionRef NewRef(long version = 3) => new ContentDefinitionRef(ContentDefinitionId.NewId(Now), version);

        // ---- ItemDefinition -------------------------------------------------------

        [Test]
        public void ItemDefinition_RoundTripsThroughPropertiesJson()
        {
            var abilityRef = NewRef();
            var effectRef = NewRef(2);
            var item = new ItemDefinition(ItemCategory.Consumable, isStackable: true, maxStackSize: 20, weight: 1, hasDurability: false, maxDurability: null, hasCharges: true, maxCharges: 3, new[] { abilityRef }, new[] { effectRef });

            string json = TypedDefinitionCodec.EncodeItem(item);
            Result<ItemDefinition> decoded = TypedDefinitionCodec.DecodeItem(ContentDefinitionType.Item, json, TestCorrelationId);

            Assert.That(decoded.IsSuccess, Is.True);
            Assert.That(decoded.Value.Category, Is.EqualTo(ItemCategory.Consumable));
            Assert.That(decoded.Value.IsStackable, Is.True);
            Assert.That(decoded.Value.MaxStackSize, Is.EqualTo(20));
            Assert.That(decoded.Value.HasCharges, Is.True);
            Assert.That(decoded.Value.MaxCharges, Is.EqualTo(3));
            Assert.That(decoded.Value.BuiltInAbilityRefs.Single(), Is.EqualTo(abilityRef));
            Assert.That(decoded.Value.BuiltInEffectRefs.Single(), Is.EqualTo(effectRef));
        }

        // ---- WeaponDefinition -------------------------------------------------------

        [Test]
        public void WeaponDefinition_RoundTripsAllRequiredAttackFields()
        {
            var item = new ItemDefinition(ItemCategory.Generic, false, null, 5, true, 100, false, null, System.Array.Empty<ContentDefinitionRef>(), System.Array.Empty<ContentDefinitionRef>());
            var weapon = new WeaponDefinition(item, "1d8+2", range: 5, WeaponAttackMode.Melee, actionCost: 1, AmmoRequirement.None, System.Array.Empty<string>());

            string json = TypedDefinitionCodec.EncodeWeapon(weapon);
            Result<WeaponDefinition> decoded = TypedDefinitionCodec.DecodeWeapon(ContentDefinitionType.Weapon, json, TestCorrelationId);

            Assert.That(decoded.IsSuccess, Is.True);
            Assert.That(decoded.Value.DamageExpression, Is.EqualTo("1d8+2"));
            Assert.That(decoded.Value.Range, Is.EqualTo(5));
            Assert.That(decoded.Value.AttackMode, Is.EqualTo(WeaponAttackMode.Melee));
            Assert.That(decoded.Value.ActionCost, Is.EqualTo(1));
            Assert.That(decoded.Value.AmmoRequirement, Is.EqualTo(AmmoRequirement.None));
            Assert.That(decoded.Value.Item.MaxDurability, Is.EqualTo(100));
        }

        [Test]
        public void WeaponDefinition_WithAmmoRequired_RoundTripsCompatibilityKeys()
        {
            var item = new ItemDefinition(ItemCategory.Generic, false, null, 8, false, null, false, null, System.Array.Empty<ContentDefinitionRef>(), System.Array.Empty<ContentDefinitionRef>());
            var weapon = new WeaponDefinition(item, "2d6", range: 60, WeaponAttackMode.Ranged, actionCost: 1, AmmoRequirement.Required, new[] { "9mm", "9mm_subsonic" });

            string json = TypedDefinitionCodec.EncodeWeapon(weapon);
            Result<WeaponDefinition> decoded = TypedDefinitionCodec.DecodeWeapon(ContentDefinitionType.Weapon, json, TestCorrelationId);

            Assert.That(decoded.IsSuccess, Is.True);
            Assert.That(decoded.Value.AmmoRequirement, Is.EqualTo(AmmoRequirement.Required));
            Assert.That(decoded.Value.CompatibleAmmoKeys, Is.EquivalentTo(new[] { "9mm", "9mm_subsonic" }));
        }

        // ---- ArmorDefinition -------------------------------------------------------

        [Test]
        public void ArmorDefinition_RoundTripsSlotsBodyPartRefsProtectionDurability()
        {
            var item = new ItemDefinition(ItemCategory.Generic, false, null, 12, true, 50, false, null, System.Array.Empty<ContentDefinitionRef>(), System.Array.Empty<ContentDefinitionRef>());
            var armor = new ArmorDefinition(item, "chest_slot", new[] { BodyPartId.Parse("Torso"), BodyPartId.Parse("Shoulders") }, protection: 8);

            string json = TypedDefinitionCodec.EncodeArmor(armor);
            Result<ArmorDefinition> decoded = TypedDefinitionCodec.DecodeArmor(ContentDefinitionType.Armor, json, TestCorrelationId);

            Assert.That(decoded.IsSuccess, Is.True);
            Assert.That(decoded.Value.EquipmentSlotKey, Is.EqualTo("chest_slot"));
            Assert.That(decoded.Value.CoveredBodyPartIds, Is.EquivalentTo(new[] { BodyPartId.Parse("Torso"), BodyPartId.Parse("Shoulders") }));
            Assert.That(decoded.Value.Protection, Is.EqualTo(8));
            Assert.That(decoded.Value.Item.HasDurability, Is.True);
            Assert.That(decoded.Value.Item.MaxDurability, Is.EqualTo(50));
        }

        // ---- AmmoDefinition -------------------------------------------------------

        [Test]
        public void AmmoDefinition_RoundTripsCompatibilityShape()
        {
            var effectRef = NewRef(1);
            var item = new ItemDefinition(ItemCategory.Generic, true, 60, 1, false, null, false, null, System.Array.Empty<ContentDefinitionRef>(), System.Array.Empty<ContentDefinitionRef>());
            var ammo = new AmmoDefinition(item, new[] { "9mm" }, "+1", new[] { effectRef });

            string json = TypedDefinitionCodec.EncodeAmmo(ammo);
            Result<AmmoDefinition> decoded = TypedDefinitionCodec.DecodeAmmo(ContentDefinitionType.Ammo, json, TestCorrelationId);

            Assert.That(decoded.IsSuccess, Is.True);
            Assert.That(decoded.Value.CompatibilityKeys, Is.EquivalentTo(new[] { "9mm" }));
            Assert.That(decoded.Value.DamageContribution, Is.EqualTo("+1"));
            Assert.That(decoded.Value.EffectContributionRefs.Single(), Is.EqualTo(effectRef));
        }

        // ---- AbilityDefinition -------------------------------------------------------

        [Test]
        public void AbilityDefinition_RoundTripsTriggerCostAndTargetShape()
        {
            var targetRule = new ContentTargetRule(ContentTargetSource.ActingCharacter, minimumCount: 1, maximumCount: 1, allowSelf: true);
            var resourceCosts = new[] { new AbilityResourceCost(ResourceDefinitionId.Parse("Stamina"), 5) };
            var ability = new AbilityDefinition(AbilityEntryPointType.ActiveAction, "OnCommand", actionCost: 2, resourceCosts, targetRule, mechanicsPayloadRef: "block_ref_001");

            string json = TypedDefinitionCodec.EncodeAbility(ability);
            Result<AbilityDefinition> decoded = TypedDefinitionCodec.DecodeAbility(ContentDefinitionType.Ability, json, TestCorrelationId);

            Assert.That(decoded.IsSuccess, Is.True);
            Assert.That(decoded.Value.EntryPointType, Is.EqualTo(AbilityEntryPointType.ActiveAction));
            Assert.That(decoded.Value.Trigger, Is.EqualTo("OnCommand"));
            Assert.That(decoded.Value.ActionCost, Is.EqualTo(2));
            Assert.That(decoded.Value.ResourceCosts.Single().ResourceDefinitionId, Is.EqualTo(ResourceDefinitionId.Parse("Stamina")));
            Assert.That(decoded.Value.ResourceCosts.Single().Amount, Is.EqualTo(5));
            Assert.That(decoded.Value.TargetRule.TargetSource, Is.EqualTo(ContentTargetSource.ActingCharacter));
            Assert.That(decoded.Value.MechanicsPayloadRef, Is.EqualTo("block_ref_001"));
        }

        // ---- EffectDefinition -------------------------------------------------------

        [Test]
        public void EffectDefinition_RoundTripsTargetDurationStackingAndMechanicsShape()
        {
            var targetRule = new ContentTargetRule(ContentTargetSource.SourceEntity, 1, 1, false);
            var effect = new EffectDefinition(targetRule, EffectDurationType.ForRounds, durationValue: 3, EffectStackPolicy.RefreshDuration, mechanicsPayloadRef: "snapshot_ref_002");

            string json = TypedDefinitionCodec.EncodeEffect(effect);
            Result<EffectDefinition> decoded = TypedDefinitionCodec.DecodeEffect(ContentDefinitionType.Effect, json, TestCorrelationId);

            Assert.That(decoded.IsSuccess, Is.True);
            Assert.That(decoded.Value.DurationType, Is.EqualTo(EffectDurationType.ForRounds));
            Assert.That(decoded.Value.DurationValue, Is.EqualTo(3));
            Assert.That(decoded.Value.StackPolicy, Is.EqualTo(EffectStackPolicy.RefreshDuration));
            Assert.That(decoded.Value.TargetRule.TargetSource, Is.EqualTo(ContentTargetSource.SourceEntity));
            Assert.That(decoded.Value.MechanicsPayloadRef, Is.EqualTo("snapshot_ref_002"));
        }

        [Test]
        public void EffectDefinition_WithInstantDuration_RoundTripsWithNullDurationValue()
        {
            var targetRule = new ContentTargetRule(ContentTargetSource.ManualSelection, 1, 1, false);
            var effect = new EffectDefinition(targetRule, EffectDurationType.Instant, durationValue: null, EffectStackPolicy.IndependentInstances, mechanicsPayloadRef: null);

            string json = TypedDefinitionCodec.EncodeEffect(effect);
            Result<EffectDefinition> decoded = TypedDefinitionCodec.DecodeEffect(ContentDefinitionType.Effect, json, TestCorrelationId);

            Assert.That(decoded.IsSuccess, Is.True);
            Assert.That(decoded.Value.DurationValue, Is.Null);
            Assert.That(decoded.Value.MechanicsPayloadRef, Is.Null);
        }

        // ---- Wrong ContentDefinitionType cannot be decoded ----------------------

        [Test]
        public void DecodeWeapon_AgainstAbilityDefinitionType_IsRejected_WithoutParsingJson()
        {
            var item = new ItemDefinition(ItemCategory.Generic, false, null, 1, false, null, false, null, System.Array.Empty<ContentDefinitionRef>(), System.Array.Empty<ContentDefinitionRef>());
            var weapon = new WeaponDefinition(item, "1d6", 5, WeaponAttackMode.Melee, 1, AmmoRequirement.None, System.Array.Empty<string>());
            string json = TypedDefinitionCodec.EncodeWeapon(weapon);

            Result<WeaponDefinition> decoded = TypedDefinitionCodec.DecodeWeapon(ContentDefinitionType.Ability, json, TestCorrelationId);

            Assert.That(decoded.IsFailure, Is.True);
            Assert.That(decoded.Error.Code, Is.EqualTo(ErrorCodes.ContentCatalogTypedDefinitionWrongType));
        }

        [Test]
        public void DecodeAbility_AgainstEffectDefinitionType_IsRejected()
        {
            var targetRule = new ContentTargetRule(ContentTargetSource.ActingCharacter, 1, 1, true);
            var ability = new AbilityDefinition(AbilityEntryPointType.Passive, "Always", 0, System.Array.Empty<AbilityResourceCost>(), targetRule, null);
            string json = TypedDefinitionCodec.EncodeAbility(ability);

            Result<AbilityDefinition> decoded = TypedDefinitionCodec.DecodeAbility(ContentDefinitionType.Effect, json, TestCorrelationId);

            Assert.That(decoded.IsFailure, Is.True);
            Assert.That(decoded.Error.Code, Is.EqualTo(ErrorCodes.ContentCatalogTypedDefinitionWrongType));
        }

        // ---- Malformed JSON returns a safe failure, not a raw exception ----------

        [TestCase("{ this is not valid json")]
        [TestCase("null")]
        [TestCase("{}")]
        [TestCase("{\"category\":\"NotARealCategory\",\"isStackable\":false,\"weight\":1,\"hasDurability\":false,\"hasCharges\":false,\"builtInAbilityRefs\":[],\"builtInEffectRefs\":[]}")]
        public void DecodeItem_OnMalformedJson_ReturnsSafeFailure_NotRawException(string malformedJson)
        {
            Result<ItemDefinition> decoded = TypedDefinitionCodec.DecodeItem(ContentDefinitionType.Item, malformedJson, TestCorrelationId);

            Assert.That(decoded.IsFailure, Is.True);
            Assert.That(decoded.Error.Code, Is.EqualTo(ErrorCodes.ContentCatalogTypedDefinitionMalformedPayload));
        }

        [Test]
        public void DecodeWeapon_OnJsonMissingRequiredField_ReturnsSafeFailure()
        {
            // Valid ItemDefinition payload shape, but missing every
            // Weapon-specific field.
            var item = new ItemDefinition(ItemCategory.Generic, false, null, 1, false, null, false, null, System.Array.Empty<ContentDefinitionRef>(), System.Array.Empty<ContentDefinitionRef>());
            string itemOnlyJson = TypedDefinitionCodec.EncodeItem(item);

            Result<WeaponDefinition> decoded = TypedDefinitionCodec.DecodeWeapon(ContentDefinitionType.Weapon, itemOnlyJson, TestCorrelationId);

            Assert.That(decoded.IsFailure, Is.True);
            Assert.That(decoded.Error.Code, Is.EqualTo(ErrorCodes.ContentCatalogTypedDefinitionMalformedPayload));
        }

        // ---- Exact-version references remain exact, never "latest" ----------------

        [Test]
        public void ExactVersionReferences_InsideTypedProperties_RemainExactRefs_NotLatest()
        {
            var abilityRefV1 = new ContentDefinitionRef(ContentDefinitionId.NewId(Now), 1);
            var sameDefinitionRefV2 = new ContentDefinitionRef(abilityRefV1.DefinitionId, 2);
            var item = new ItemDefinition(ItemCategory.Generic, false, null, 1, false, null, false, null, new[] { abilityRefV1 }, System.Array.Empty<ContentDefinitionRef>());

            string json = TypedDefinitionCodec.EncodeItem(item);
            Result<ItemDefinition> decoded = TypedDefinitionCodec.DecodeItem(ContentDefinitionType.Item, json, TestCorrelationId);

            Assert.That(decoded.IsSuccess, Is.True);
            ContentDefinitionRef roundTrippedRef = decoded.Value.BuiltInAbilityRefs.Single();
            Assert.That(roundTrippedRef, Is.EqualTo(abilityRefV1));
            Assert.That(roundTrippedRef, Is.Not.EqualTo(sameDefinitionRefV2), "an exact-version reference round-tripped through typed PropertiesJson must still pin the exact version it was created with, not silently become a different version or a 'latest' pointer");
            Assert.That(roundTrippedRef.Version, Is.EqualTo(1));
        }

        // ---- No runtime item/inventory/equipment/effect implementation slipped in ----

        [Test]
        public void TypedDefinitionTypes_IntroduceNoRuntimeItemInventoryEquipmentOrActiveEffectType()
        {
            System.Reflection.Assembly domainAssembly = typeof(ItemDefinition).Assembly;
            System.Reflection.Assembly applicationAssembly = typeof(TypedDefinitionCodec).Assembly;

            var typesToCheck = domainAssembly.GetTypes()
                .Where(t => t.Namespace == "Odyssey.Domain.Content")
                .Concat(applicationAssembly.GetTypes().Where(t => t.Namespace == "Odyssey.Application.Content"))
                .ToArray();

            string[] forbiddenSubstrings = { "Inventory", "ItemInstance", "ItemStack", "Equipment", "ActiveEffect" };
            foreach (System.Type type in typesToCheck)
            {
                foreach (string forbidden in forbiddenSubstrings)
                {
                    Assert.That(type.Name, Does.Not.Contain(forbidden), $"type '{type.Name}' must not reference runtime item/inventory/equipment/effect state ('{forbidden}')");
                }
            }
        }
    }
}
