using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Content
{
    /// <summary>
    /// ODY-S05-105: the explicit, versioned mapping between a typed catalog
    /// definition (`Odyssey.Domain.Content`) and the generic
    /// `ContentDefinitionRecord.PropertiesJson` blob `ODY-S05-101`'s
    /// foundation already stores/round-trips as an opaque string --
    /// deliberately not ad-hoc string parsing (`ADR-003`'s explicit-contract
    /// rule). Lives in `Odyssey.Application.Content`, alongside
    /// `ODY-S05-102`'s own `ContentCatalogAuthoringService`, rather than in
    /// the still-largely-empty `Odyssey.Content` project `ADR-027` section
    /// 14 eventually reserves for "ContentDefinition contracts" -- recorded
    /// explicitly as a scope decision in this task's own contract (section
    /// 18), not a silent architectural choice: `Odyssey.Application`
    /// already references `Newtonsoft.Json`, and every prior `SLICE-05`
    /// task (`ODY-S05-101`/`102`) already built directly in
    /// Domain/Application/Persistence without touching `Odyssey.Content`.
    ///
    /// Every encoded payload embeds a `schemaVersion` integer (currently
    /// `1` for all six typed shapes) so a future incompatible shape change
    /// has an explicit upcaster hook to check against, per `ADR-003`'s own
    /// forward-compatibility convention -- this task introduces no
    /// upcaster itself, since only one schema version exists yet.
    ///
    /// Each `DecodeX` method takes the `ContentDefinition`'s own actual
    /// `ContentDefinitionType` and refuses to decode when it does not match
    /// the requested typed shape (`TypedDefinitionCodecFailures.WrongDefinitionType`)
    /// -- a Weapon-shaped payload can never be misread as an Ability, even
    /// if the raw JSON happened to parse. Any malformed JSON or missing
    /// required field is caught and returned as a safe `Result.Failure`
    /// (`TypedDefinitionCodecFailures.MalformedPayload`), never a raw
    /// exception leaking to the caller.
    /// </summary>
    public static class TypedDefinitionCodec
    {
        private const int SchemaVersion = 1;

        public static string EncodeItem(ItemDefinition item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            return WriteItemPayload(item).ToString(Formatting.None);
        }

        public static Result<ItemDefinition> DecodeItem(ContentDefinitionType actualType, string propertiesJson, CorrelationId correlationId)
        {
            if (actualType != ContentDefinitionType.Item)
            {
                return Result<ItemDefinition>.Failure(TypedDefinitionCodecFailures.WrongDefinitionType(correlationId));
            }

            try
            {
                JObject root = JObject.Parse(propertiesJson);
                return Result<ItemDefinition>.Success(ReadItemPayload(root));
            }
            catch (Exception ex) when (IsMalformedPayloadException(ex))
            {
                return Result<ItemDefinition>.Failure(TypedDefinitionCodecFailures.MalformedPayload(correlationId));
            }
        }

        public static string EncodeWeapon(WeaponDefinition weapon)
        {
            if (weapon == null) throw new ArgumentNullException(nameof(weapon));

            var root = WriteItemPayload(weapon.Item);
            root["schemaVersion"] = SchemaVersion;
            root["damageExpression"] = weapon.DamageExpression;
            root["range"] = weapon.Range;
            root["attackMode"] = weapon.AttackMode.ToString();
            root["actionCost"] = weapon.ActionCost;
            root["ammoRequirement"] = weapon.AmmoRequirement.ToString();
            root["compatibleAmmoKeys"] = ToJArray(weapon.CompatibleAmmoKeys);
            return root.ToString(Formatting.None);
        }

        public static Result<WeaponDefinition> DecodeWeapon(ContentDefinitionType actualType, string propertiesJson, CorrelationId correlationId)
        {
            if (actualType != ContentDefinitionType.Weapon)
            {
                return Result<WeaponDefinition>.Failure(TypedDefinitionCodecFailures.WrongDefinitionType(correlationId));
            }

            try
            {
                JObject root = JObject.Parse(propertiesJson);
                ItemDefinition item = ReadItemPayload(root);
                string damageExpression = (string)root["damageExpression"]!;
                long range = (long)root["range"]!;
                var attackMode = (WeaponAttackMode)Enum.Parse(typeof(WeaponAttackMode), (string)root["attackMode"]!);
                long actionCost = (long)root["actionCost"]!;
                var ammoRequirement = (AmmoRequirement)Enum.Parse(typeof(AmmoRequirement), (string)root["ammoRequirement"]!);
                IReadOnlyList<string> compatibleAmmoKeys = ReadStringArray(root["compatibleAmmoKeys"]);

                var weapon = new WeaponDefinition(item, damageExpression, range, attackMode, actionCost, ammoRequirement, compatibleAmmoKeys);
                return Result<WeaponDefinition>.Success(weapon);
            }
            catch (Exception ex) when (IsMalformedPayloadException(ex))
            {
                return Result<WeaponDefinition>.Failure(TypedDefinitionCodecFailures.MalformedPayload(correlationId));
            }
        }

        public static string EncodeArmor(ArmorDefinition armor)
        {
            if (armor == null) throw new ArgumentNullException(nameof(armor));

            var root = WriteItemPayload(armor.Item);
            root["schemaVersion"] = SchemaVersion;
            root["equipmentSlotKey"] = armor.EquipmentSlotKey;
            var bodyPartArray = new JArray();
            foreach (BodyPartId bodyPartId in armor.CoveredBodyPartIds) bodyPartArray.Add(bodyPartId.ToString());
            root["coveredBodyPartIds"] = bodyPartArray;
            root["protection"] = armor.Protection;
            return root.ToString(Formatting.None);
        }

        public static Result<ArmorDefinition> DecodeArmor(ContentDefinitionType actualType, string propertiesJson, CorrelationId correlationId)
        {
            if (actualType != ContentDefinitionType.Armor)
            {
                return Result<ArmorDefinition>.Failure(TypedDefinitionCodecFailures.WrongDefinitionType(correlationId));
            }

            try
            {
                JObject root = JObject.Parse(propertiesJson);
                ItemDefinition item = ReadItemPayload(root);
                string equipmentSlotKey = (string)root["equipmentSlotKey"]!;
                var bodyPartIds = new List<BodyPartId>();
                foreach (JToken token in (JArray)root["coveredBodyPartIds"]!) bodyPartIds.Add(BodyPartId.Parse((string)token!));
                long protection = (long)root["protection"]!;

                var armor = new ArmorDefinition(item, equipmentSlotKey, bodyPartIds, protection);
                return Result<ArmorDefinition>.Success(armor);
            }
            catch (Exception ex) when (IsMalformedPayloadException(ex))
            {
                return Result<ArmorDefinition>.Failure(TypedDefinitionCodecFailures.MalformedPayload(correlationId));
            }
        }

        public static string EncodeAmmo(AmmoDefinition ammo)
        {
            if (ammo == null) throw new ArgumentNullException(nameof(ammo));

            var root = WriteItemPayload(ammo.Item);
            root["schemaVersion"] = SchemaVersion;
            root["compatibilityKeys"] = ToJArray(ammo.CompatibilityKeys);
            root["damageContribution"] = ammo.DamageContribution;
            root["effectContributionRefs"] = ToJArray(RefsToStrings(ammo.EffectContributionRefs));
            return root.ToString(Formatting.None);
        }

        public static Result<AmmoDefinition> DecodeAmmo(ContentDefinitionType actualType, string propertiesJson, CorrelationId correlationId)
        {
            if (actualType != ContentDefinitionType.Ammo)
            {
                return Result<AmmoDefinition>.Failure(TypedDefinitionCodecFailures.WrongDefinitionType(correlationId));
            }

            try
            {
                JObject root = JObject.Parse(propertiesJson);
                ItemDefinition item = ReadItemPayload(root);
                IReadOnlyList<string> compatibilityKeys = ReadStringArray(root["compatibilityKeys"]);
                string? damageContribution = root["damageContribution"] == null || root["damageContribution"]!.Type == JTokenType.Null ? null : (string)root["damageContribution"]!;
                IReadOnlyList<ContentDefinitionRef> effectContributionRefs = ReadRefArray(root["effectContributionRefs"]);

                var ammo = new AmmoDefinition(item, compatibilityKeys, damageContribution, effectContributionRefs);
                return Result<AmmoDefinition>.Success(ammo);
            }
            catch (Exception ex) when (IsMalformedPayloadException(ex))
            {
                return Result<AmmoDefinition>.Failure(TypedDefinitionCodecFailures.MalformedPayload(correlationId));
            }
        }

        public static string EncodeAbility(AbilityDefinition ability)
        {
            if (ability == null) throw new ArgumentNullException(nameof(ability));

            var root = new JObject
            {
                ["schemaVersion"] = SchemaVersion,
                ["entryPointType"] = ability.EntryPointType.ToString(),
                ["trigger"] = ability.Trigger,
                ["actionCost"] = ability.ActionCost,
                ["targetRule"] = WriteTargetRule(ability.TargetRule),
                ["mechanicsPayloadRef"] = ability.MechanicsPayloadRef,
            };

            var resourceCosts = new JArray();
            foreach (AbilityResourceCost cost in ability.ResourceCosts)
            {
                resourceCosts.Add(new JObject
                {
                    ["resourceDefinitionId"] = cost.ResourceDefinitionId.ToString(),
                    ["amount"] = cost.Amount,
                });
            }

            root["resourceCosts"] = resourceCosts;
            return root.ToString(Formatting.None);
        }

        public static Result<AbilityDefinition> DecodeAbility(ContentDefinitionType actualType, string propertiesJson, CorrelationId correlationId)
        {
            if (actualType != ContentDefinitionType.Ability)
            {
                return Result<AbilityDefinition>.Failure(TypedDefinitionCodecFailures.WrongDefinitionType(correlationId));
            }

            try
            {
                JObject root = JObject.Parse(propertiesJson);
                var entryPointType = (AbilityEntryPointType)Enum.Parse(typeof(AbilityEntryPointType), (string)root["entryPointType"]!);
                string trigger = (string)root["trigger"]!;
                long actionCost = (long)root["actionCost"]!;
                ContentTargetRule targetRule = ReadTargetRule((JObject)root["targetRule"]!);
                string? mechanicsPayloadRef = root["mechanicsPayloadRef"] == null || root["mechanicsPayloadRef"]!.Type == JTokenType.Null ? null : (string)root["mechanicsPayloadRef"]!;

                var resourceCosts = new List<AbilityResourceCost>();
                foreach (JToken token in (JArray)root["resourceCosts"]!)
                {
                    var costObject = (JObject)token;
                    ResourceDefinitionId resourceDefinitionId = ResourceDefinitionId.Parse((string)costObject["resourceDefinitionId"]!);
                    long amount = (long)costObject["amount"]!;
                    resourceCosts.Add(new AbilityResourceCost(resourceDefinitionId, amount));
                }

                var ability = new AbilityDefinition(entryPointType, trigger, actionCost, resourceCosts, targetRule, mechanicsPayloadRef);
                return Result<AbilityDefinition>.Success(ability);
            }
            catch (Exception ex) when (IsMalformedPayloadException(ex))
            {
                return Result<AbilityDefinition>.Failure(TypedDefinitionCodecFailures.MalformedPayload(correlationId));
            }
        }

        public static string EncodeEffect(EffectDefinition effect)
        {
            if (effect == null) throw new ArgumentNullException(nameof(effect));

            var root = new JObject
            {
                ["schemaVersion"] = SchemaVersion,
                ["targetRule"] = WriteTargetRule(effect.TargetRule),
                ["durationType"] = effect.DurationType.ToString(),
                ["durationValue"] = effect.DurationValue,
                ["stackPolicy"] = effect.StackPolicy.ToString(),
                ["mechanicsPayloadRef"] = effect.MechanicsPayloadRef,
            };

            return root.ToString(Formatting.None);
        }

        public static Result<EffectDefinition> DecodeEffect(ContentDefinitionType actualType, string propertiesJson, CorrelationId correlationId)
        {
            if (actualType != ContentDefinitionType.Effect)
            {
                return Result<EffectDefinition>.Failure(TypedDefinitionCodecFailures.WrongDefinitionType(correlationId));
            }

            try
            {
                JObject root = JObject.Parse(propertiesJson);
                ContentTargetRule targetRule = ReadTargetRule((JObject)root["targetRule"]!);
                var durationType = (EffectDurationType)Enum.Parse(typeof(EffectDurationType), (string)root["durationType"]!);
                long? durationValue = root["durationValue"] == null || root["durationValue"]!.Type == JTokenType.Null ? (long?)null : (long)root["durationValue"]!;
                var stackPolicy = (EffectStackPolicy)Enum.Parse(typeof(EffectStackPolicy), (string)root["stackPolicy"]!);
                string? mechanicsPayloadRef = root["mechanicsPayloadRef"] == null || root["mechanicsPayloadRef"]!.Type == JTokenType.Null ? null : (string)root["mechanicsPayloadRef"]!;

                var effect = new EffectDefinition(targetRule, durationType, durationValue, stackPolicy, mechanicsPayloadRef);
                return Result<EffectDefinition>.Success(effect);
            }
            catch (Exception ex) when (IsMalformedPayloadException(ex))
            {
                return Result<EffectDefinition>.Failure(TypedDefinitionCodecFailures.MalformedPayload(correlationId));
            }
        }

        private static JObject WriteItemPayload(ItemDefinition item)
        {
            return new JObject
            {
                ["schemaVersion"] = SchemaVersion,
                ["category"] = item.Category.ToString(),
                ["isStackable"] = item.IsStackable,
                ["maxStackSize"] = item.MaxStackSize,
                ["weight"] = item.Weight,
                ["hasDurability"] = item.HasDurability,
                ["maxDurability"] = item.MaxDurability,
                ["hasCharges"] = item.HasCharges,
                ["maxCharges"] = item.MaxCharges,
                ["builtInAbilityRefs"] = ToJArray(RefsToStrings(item.BuiltInAbilityRefs)),
                ["builtInEffectRefs"] = ToJArray(RefsToStrings(item.BuiltInEffectRefs)),
            };
        }

        private static ItemDefinition ReadItemPayload(JObject root)
        {
            var category = (ItemCategory)Enum.Parse(typeof(ItemCategory), (string)root["category"]!);
            bool isStackable = (bool)root["isStackable"]!;
            long? maxStackSize = root["maxStackSize"] == null || root["maxStackSize"]!.Type == JTokenType.Null ? (long?)null : (long)root["maxStackSize"]!;
            long weight = (long)root["weight"]!;
            bool hasDurability = (bool)root["hasDurability"]!;
            long? maxDurability = root["maxDurability"] == null || root["maxDurability"]!.Type == JTokenType.Null ? (long?)null : (long)root["maxDurability"]!;
            bool hasCharges = (bool)root["hasCharges"]!;
            long? maxCharges = root["maxCharges"] == null || root["maxCharges"]!.Type == JTokenType.Null ? (long?)null : (long)root["maxCharges"]!;
            IReadOnlyList<ContentDefinitionRef> builtInAbilityRefs = ReadRefArray(root["builtInAbilityRefs"]);
            IReadOnlyList<ContentDefinitionRef> builtInEffectRefs = ReadRefArray(root["builtInEffectRefs"]);

            return new ItemDefinition(category, isStackable, maxStackSize, weight, hasDurability, maxDurability, hasCharges, maxCharges, builtInAbilityRefs, builtInEffectRefs);
        }

        private static JObject WriteTargetRule(ContentTargetRule rule) => new JObject
        {
            ["targetSource"] = rule.TargetSource.ToString(),
            ["minimumCount"] = rule.MinimumCount,
            ["maximumCount"] = rule.MaximumCount,
            ["allowSelf"] = rule.AllowSelf,
        };

        private static ContentTargetRule ReadTargetRule(JObject root)
        {
            var targetSource = (ContentTargetSource)Enum.Parse(typeof(ContentTargetSource), (string)root["targetSource"]!);
            long minimumCount = (long)root["minimumCount"]!;
            long maximumCount = (long)root["maximumCount"]!;
            bool allowSelf = (bool)root["allowSelf"]!;
            return new ContentTargetRule(targetSource, minimumCount, maximumCount, allowSelf);
        }

        private static JArray ToJArray(IReadOnlyList<string> values)
        {
            var array = new JArray();
            foreach (string value in values) array.Add(value);
            return array;
        }

        private static IReadOnlyList<string> ReadStringArray(JToken? token)
        {
            var values = new List<string>();
            if (token == null) return values;
            foreach (JToken item in (JArray)token) values.Add((string)item!);
            return values;
        }

        private static IReadOnlyList<string> RefsToStrings(IReadOnlyList<ContentDefinitionRef> refs)
        {
            var values = new List<string>(refs.Count);
            foreach (ContentDefinitionRef reference in refs) values.Add(reference.ToString());
            return values;
        }

        private static IReadOnlyList<ContentDefinitionRef> ReadRefArray(JToken? token)
        {
            var refs = new List<ContentDefinitionRef>();
            if (token == null) return refs;
            foreach (JToken item in (JArray)token) refs.Add(ContentDefinitionRef.Parse((string)item!));
            return refs;
        }

        /// <summary>
        /// Every exception a malformed/hostile JSON payload or an unexpected
        /// missing/mistyped field can throw during decode -- caught here so
        /// no raw exception (or its message, which could embed arbitrary
        /// caller-controlled JSON content) ever leaks past this codec.
        /// </summary>
        private static bool IsMalformedPayloadException(Exception ex) =>
            ex is JsonException
            || ex is FormatException
            || ex is ArgumentException
            || ex is InvalidCastException
            || ex is NullReferenceException
            || ex is KeyNotFoundException
            || ex is IndexOutOfRangeException;
    }

    /// <summary>ODY-S05-105: codec-level failures, mirroring <see cref="Odyssey.Application.Content.ContentCatalogAuthoringFailures"/>'s exact convention -- a distinct class from `PersistenceFailures` since decoding happens entirely in-memory, with no repository call at all.</summary>
    public static class TypedDefinitionCodecFailures
    {
        /// <summary>The caller asked to decode a typed shape (e.g. Weapon) against a `ContentDefinitionRecord` whose actual `DefinitionType` does not match (e.g. Ability) -- refused before any JSON parsing is attempted.</summary>
        public static Error WrongDefinitionType(CorrelationId correlationId) => Error.Create(
            ErrorCodes.ContentCatalogTypedDefinitionWrongType,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.content_catalog.typed_definition_wrong_type"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>`PropertiesJson` is not valid JSON for the requested typed shape (malformed JSON, missing required field, wrong field type, invalid enum value) -- caught and returned safely, never a raw exception/its message.</summary>
        public static Error MalformedPayload(CorrelationId correlationId) => Error.Create(
            ErrorCodes.ContentCatalogTypedDefinitionMalformedPayload,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.content_catalog.typed_definition_malformed_payload"),
            RetryDirective.DoNotRetry,
            correlationId);
    }
}
