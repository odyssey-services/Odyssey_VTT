using System;
using System.Collections.Generic;
using Odyssey.Application.Content;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;

namespace Odyssey.Unity.Client
{
    /// <summary>One readable form-level problem, keyed by the form field it belongs to.</summary>
    public sealed class CatalogFieldError
    {
        public CatalogFieldError(string field, string message)
        {
            Field = field ?? throw new ArgumentNullException(nameof(field));
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public string Field { get; }
        public string Message { get; }
    }

    /// <summary>
    /// ODY-S11-202: the shared <see cref="ContentTargetRule"/> editor state (Ability and Effect both use it).
    /// </summary>
    public sealed class TargetRuleModel
    {
        public ContentTargetSource Source { get; set; } = ContentTargetSource.ManualSelection;
        public long MinimumCount { get; set; } = 1;
        public long MaximumCount { get; set; } = 1;
        public bool AllowSelf { get; set; }

        public static TargetRuleModel From(ContentTargetRule rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            return new TargetRuleModel { Source = rule.TargetSource, MinimumCount = rule.MinimumCount, MaximumCount = rule.MaximumCount, AllowSelf = rule.AllowSelf };
        }

        public void Validate(string prefix, List<CatalogFieldError> errors)
        {
            if (MinimumCount < 0) errors.Add(new CatalogFieldError(prefix + ".min", "Minimum targets cannot be negative."));
            if (MaximumCount < MinimumCount) errors.Add(new CatalogFieldError(prefix + ".max", "Maximum targets must be at least the minimum."));
        }

        public ContentTargetRule Build() => new ContentTargetRule(Source, MinimumCount, MaximumCount, AllowSelf);
    }

    /// <summary>One editable row of an Ability's resource costs.</summary>
    public sealed class ResourceCostModel
    {
        public ResourceCostModel(string resourceDefinitionId, long amount)
        {
            ResourceDefinitionId = resourceDefinitionId ?? string.Empty;
            Amount = amount;
        }

        public string ResourceDefinitionId { get; set; }
        public long Amount { get; set; }
    }

    /// <summary>
    /// ODY-S11-202: the editable state of one catalog definition of any of the seven supported types. It is only a
    /// form: every rule below mirrors the Domain constructor preconditions of the typed definitions
    /// (<c>Odyssey.Domain.Content.TypedDefinitions</c>) so the user sees a field-level message instead of an exception,
    /// and the JSON sent to the backend is always produced by the backend's own <see cref="TypedDefinitionCodec"/>
    /// (never hand-written). Publish-time catalog rules (ruleset compatibility, reference existence, weapon-ammo
    /// availability, cycles) stay authoritative in <see cref="CatalogValidationService"/>.
    /// </summary>
    public sealed class ContentDefinitionFormModel
    {
        public static readonly IReadOnlyList<ContentDefinitionType> SupportedTypes = new[]
        {
            ContentDefinitionType.Item,
            ContentDefinitionType.Weapon,
            ContentDefinitionType.Armor,
            ContentDefinitionType.Ammo,
            ContentDefinitionType.Ability,
            ContentDefinitionType.Effect,
            ContentDefinitionType.Skill,
        };

        private ContentDefinitionFormModel(ContentDefinitionType type)
        {
            Type = type;
        }

        public ContentDefinitionType Type { get; }

        // Common envelope
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TagsText { get; set; } = string.Empty;
        public string RulesetCompatibilityText { get; set; } = string.Empty;

        // Item base (Item / Weapon / Armor / Ammo)
        public ItemCategory Category { get; set; } = ItemCategory.Generic;
        public bool IsStackable { get; set; }
        public long MaxStackSize { get; set; } = 1;
        public long Weight { get; set; }
        public bool HasDurability { get; set; }
        public long MaxDurability { get; set; } = 1;
        public bool HasCharges { get; set; }
        public long MaxCharges { get; set; } = 1;
        public List<ContentDefinitionRef> BuiltInAbilityRefs { get; } = new List<ContentDefinitionRef>();
        public List<ContentDefinitionRef> BuiltInEffectRefs { get; } = new List<ContentDefinitionRef>();

        // Weapon
        public string DamageExpression { get; set; } = "1d6";
        public long Range { get; set; } = 1;
        public WeaponAttackMode AttackMode { get; set; } = WeaponAttackMode.Melee;
        public long WeaponActionCost { get; set; } = 1;
        public AmmoRequirement AmmoRequirement { get; set; } = AmmoRequirement.None;
        public string CompatibleAmmoKeysText { get; set; } = string.Empty;

        // Armor
        public string EquipmentSlotKey { get; set; } = string.Empty;
        public string CoveredBodyPartIdsText { get; set; } = string.Empty;
        public long Protection { get; set; }

        // Ammo
        public string AmmoCompatibilityKeysText { get; set; } = string.Empty;
        public string DamageContribution { get; set; } = string.Empty;
        public List<ContentDefinitionRef> EffectContributionRefs { get; } = new List<ContentDefinitionRef>();

        // Ability
        public AbilityEntryPointType EntryPointType { get; set; } = AbilityEntryPointType.ActiveAction;
        public string Trigger { get; set; } = string.Empty;
        public long AbilityActionCost { get; set; } = 1;
        public List<ResourceCostModel> ResourceCosts { get; } = new List<ResourceCostModel>();
        public TargetRuleModel AbilityTargetRule { get; } = new TargetRuleModel();
        public string AbilityMechanicsPayloadRef { get; set; } = string.Empty;

        // Effect
        public TargetRuleModel EffectTargetRule { get; } = new TargetRuleModel();
        public EffectDurationType DurationType { get; set; } = EffectDurationType.Instant;
        public long DurationValue { get; set; } = 1;
        public EffectStackPolicy StackPolicy { get; set; } = EffectStackPolicy.IndependentInstances;
        public string EffectMechanicsPayloadRef { get; set; } = string.Empty;

        public bool IsItemBased => IsItemBasedType(Type);

        /// <summary>Weapon: compatible ammo keys are only meaningful (and only shown) when ammunition is used.</summary>
        public bool ShowsCompatibleAmmoKeys => Type == ContentDefinitionType.Weapon && AmmoRequirement != AmmoRequirement.None;

        /// <summary>Effect: the duration value is only meaningful (and only shown) for the three counted durations.</summary>
        public bool ShowsDurationValue => Type == ContentDefinitionType.Effect && DurationNeedsValue(DurationType);

        public static bool IsSupported(ContentDefinitionType type)
        {
            foreach (ContentDefinitionType candidate in SupportedTypes)
            {
                if (candidate == type) return true;
            }

            return false;
        }

        public static bool IsItemBasedType(ContentDefinitionType type) =>
            type == ContentDefinitionType.Item || type == ContentDefinitionType.Weapon || type == ContentDefinitionType.Armor || type == ContentDefinitionType.Ammo;

        public static bool DurationNeedsValue(EffectDurationType durationType) =>
            durationType == EffectDurationType.ForRounds || durationType == EffectDurationType.ForTurns || durationType == EffectDurationType.ForDuration;

        /// <summary>A blank form; ruleset compatibility defaults to the campaign's active ruleset so a new draft can publish.</summary>
        public static ContentDefinitionFormModel CreateNew(ContentDefinitionType type, string activeRulesetKey)
        {
            if (!IsSupported(type)) throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported catalog type.");
            return new ContentDefinitionFormModel(type) { RulesetCompatibilityText = activeRulesetKey ?? string.Empty };
        }

        /// <summary>Loads a stored definition through the backend codec. Fails (typed error) for a malformed payload.</summary>
        public static Result<ContentDefinitionFormModel> Load(ContentDefinitionRecord record, CorrelationId correlationId)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (!IsSupported(record.DefinitionType)) return Result<ContentDefinitionFormModel>.Failure(TypedDefinitionCodecFailures.WrongDefinitionType(correlationId));

            var model = new ContentDefinitionFormModel(record.DefinitionType)
            {
                Name = record.Name,
                Description = record.Description ?? string.Empty,
                TagsText = OdyUi.JoinList(record.Tags),
                RulesetCompatibilityText = OdyUi.JoinList(record.RulesetCompatibility),
            };

            switch (record.DefinitionType)
            {
                case ContentDefinitionType.Item:
                    {
                        Result<ItemDefinition> item = TypedDefinitionCodec.DecodeItem(record.DefinitionType, record.PropertiesJson, correlationId);
                        if (item.IsFailure) return Result<ContentDefinitionFormModel>.Failure(item.Error);
                        model.LoadItem(item.Value);
                        break;
                    }
                case ContentDefinitionType.Weapon:
                    {
                        Result<WeaponDefinition> weapon = TypedDefinitionCodec.DecodeWeapon(record.DefinitionType, record.PropertiesJson, correlationId);
                        if (weapon.IsFailure) return Result<ContentDefinitionFormModel>.Failure(weapon.Error);
                        model.LoadItem(weapon.Value.Item);
                        model.DamageExpression = weapon.Value.DamageExpression;
                        model.Range = weapon.Value.Range;
                        model.AttackMode = weapon.Value.AttackMode;
                        model.WeaponActionCost = weapon.Value.ActionCost;
                        model.AmmoRequirement = weapon.Value.AmmoRequirement;
                        model.CompatibleAmmoKeysText = OdyUi.JoinList(weapon.Value.CompatibleAmmoKeys);
                        break;
                    }
                case ContentDefinitionType.Armor:
                    {
                        Result<ArmorDefinition> armor = TypedDefinitionCodec.DecodeArmor(record.DefinitionType, record.PropertiesJson, correlationId);
                        if (armor.IsFailure) return Result<ContentDefinitionFormModel>.Failure(armor.Error);
                        model.LoadItem(armor.Value.Item);
                        model.EquipmentSlotKey = armor.Value.EquipmentSlotKey;
                        var parts = new List<string>();
                        foreach (BodyPartId id in armor.Value.CoveredBodyPartIds) parts.Add(id.ToString());
                        model.CoveredBodyPartIdsText = OdyUi.JoinList(parts);
                        model.Protection = armor.Value.Protection;
                        break;
                    }
                case ContentDefinitionType.Ammo:
                    {
                        Result<AmmoDefinition> ammo = TypedDefinitionCodec.DecodeAmmo(record.DefinitionType, record.PropertiesJson, correlationId);
                        if (ammo.IsFailure) return Result<ContentDefinitionFormModel>.Failure(ammo.Error);
                        model.LoadItem(ammo.Value.Item);
                        model.AmmoCompatibilityKeysText = OdyUi.JoinList(ammo.Value.CompatibilityKeys);
                        model.DamageContribution = ammo.Value.DamageContribution ?? string.Empty;
                        model.EffectContributionRefs.AddRange(ammo.Value.EffectContributionRefs);
                        break;
                    }
                case ContentDefinitionType.Ability:
                    {
                        Result<AbilityDefinition> ability = TypedDefinitionCodec.DecodeAbility(record.DefinitionType, record.PropertiesJson, correlationId);
                        if (ability.IsFailure) return Result<ContentDefinitionFormModel>.Failure(ability.Error);
                        model.EntryPointType = ability.Value.EntryPointType;
                        model.Trigger = ability.Value.Trigger;
                        model.AbilityActionCost = ability.Value.ActionCost;
                        foreach (AbilityResourceCost cost in ability.Value.ResourceCosts) model.ResourceCosts.Add(new ResourceCostModel(cost.ResourceDefinitionId.ToString(), cost.Amount));
                        model.CopyTargetRule(ability.Value.TargetRule, model.AbilityTargetRule);
                        model.AbilityMechanicsPayloadRef = ability.Value.MechanicsPayloadRef ?? string.Empty;
                        break;
                    }
                case ContentDefinitionType.Effect:
                    {
                        Result<EffectDefinition> effect = TypedDefinitionCodec.DecodeEffect(record.DefinitionType, record.PropertiesJson, correlationId);
                        if (effect.IsFailure) return Result<ContentDefinitionFormModel>.Failure(effect.Error);
                        model.CopyTargetRule(effect.Value.TargetRule, model.EffectTargetRule);
                        model.DurationType = effect.Value.DurationType;
                        model.DurationValue = effect.Value.DurationValue ?? 1;
                        model.StackPolicy = effect.Value.StackPolicy;
                        model.EffectMechanicsPayloadRef = effect.Value.MechanicsPayloadRef ?? string.Empty;
                        break;
                    }
                case ContentDefinitionType.Skill:
                    {
                        Result<SkillDefinition> skill = TypedDefinitionCodec.DecodeSkill(record.DefinitionType, record.PropertiesJson, correlationId);
                        if (skill.IsFailure) return Result<ContentDefinitionFormModel>.Failure(skill.Error);
                        break;
                    }
            }

            return Result<ContentDefinitionFormModel>.Success(model);
        }

        public IReadOnlyList<string> Tags => OdyUi.ParseList(TagsText);
        public IReadOnlyList<string> RulesetCompatibility => OdyUi.ParseList(RulesetCompatibilityText);

        /// <summary>Every form-level problem, in field order. Empty means the payload can be built.</summary>
        public IReadOnlyList<CatalogFieldError> Validate()
        {
            var errors = new List<CatalogFieldError>();
            string name = Name.Trim();
            if (name.Length == 0) errors.Add(new CatalogFieldError("name", "Name is required."));
            else if (name.Length > 128) errors.Add(new CatalogFieldError("name", "Name must be at most 128 characters."));

            if (IsItemBased) ValidateItem(errors);

            switch (Type)
            {
                case ContentDefinitionType.Weapon:
                    if (string.IsNullOrWhiteSpace(DamageExpression)) errors.Add(new CatalogFieldError("weapon.damage", "Damage expression is required (for example 1d8+2)."));
                    if (Range < 0) errors.Add(new CatalogFieldError("weapon.range", "Range cannot be negative."));
                    if (WeaponActionCost < 0) errors.Add(new CatalogFieldError("weapon.actionCost", "Action cost cannot be negative."));
                    if (AmmoRequirement != AmmoRequirement.None && OdyUi.ParseList(CompatibleAmmoKeysText).Count == 0)
                    {
                        errors.Add(new CatalogFieldError("weapon.ammoKeys", "List at least one compatible ammo key when the weapon uses ammunition."));
                    }

                    break;
                case ContentDefinitionType.Armor:
                    if (string.IsNullOrWhiteSpace(EquipmentSlotKey)) errors.Add(new CatalogFieldError("armor.slot", "Equipment slot key is required."));
                    List<string> parts = OdyUi.ParseList(CoveredBodyPartIdsText);
                    if (parts.Count == 0) errors.Add(new CatalogFieldError("armor.bodyParts", "List at least one covered body part id."));
                    foreach (string part in parts)
                    {
                        if (!BodyPartId.TryParse(part, out _)) errors.Add(new CatalogFieldError("armor.bodyParts", "\"" + part + "\" is not a valid body part id (letter first, then letters, digits or _)."));
                    }

                    if (Protection < 0) errors.Add(new CatalogFieldError("armor.protection", "Protection cannot be negative."));
                    break;
                case ContentDefinitionType.Ammo:
                    if (OdyUi.ParseList(AmmoCompatibilityKeysText).Count == 0) errors.Add(new CatalogFieldError("ammo.keys", "List at least one compatibility key."));
                    break;
                case ContentDefinitionType.Ability:
                    if (string.IsNullOrWhiteSpace(Trigger)) errors.Add(new CatalogFieldError("ability.trigger", "Trigger is required."));
                    if (AbilityActionCost < 0) errors.Add(new CatalogFieldError("ability.actionCost", "Action cost cannot be negative."));
                    for (int index = 0; index < ResourceCosts.Count; index++)
                    {
                        ResourceCostModel cost = ResourceCosts[index];
                        if (!ResourceDefinitionId.TryParse(cost.ResourceDefinitionId.Trim(), out _))
                        {
                            errors.Add(new CatalogFieldError("ability.cost." + index, "Resource cost " + (index + 1) + ": \"" + cost.ResourceDefinitionId + "\" is not a valid resource id."));
                        }

                        if (cost.Amount < 0) errors.Add(new CatalogFieldError("ability.cost." + index, "Resource cost " + (index + 1) + ": amount cannot be negative."));
                    }

                    AbilityTargetRule.Validate("ability.target", errors);
                    break;
                case ContentDefinitionType.Effect:
                    EffectTargetRule.Validate("effect.target", errors);
                    if (ShowsDurationValue && DurationValue < 1) errors.Add(new CatalogFieldError("effect.durationValue", "Duration value must be at least 1 for " + DurationType + "."));
                    break;
            }

            return errors;
        }

        /// <summary>Builds the typed payload through the backend codec; returns the form errors instead when invalid.</summary>
        public bool TryBuildPropertiesJson(out string propertiesJson, out IReadOnlyList<CatalogFieldError> errors)
        {
            errors = Validate();
            propertiesJson = string.Empty;
            if (errors.Count > 0) return false;

            switch (Type)
            {
                case ContentDefinitionType.Item:
                    propertiesJson = TypedDefinitionCodec.EncodeItem(BuildItem());
                    break;
                case ContentDefinitionType.Weapon:
                    {
                        IReadOnlyList<string> keys = AmmoRequirement == AmmoRequirement.None ? Array.Empty<string>() : (IReadOnlyList<string>)OdyUi.ParseList(CompatibleAmmoKeysText);
                        propertiesJson = TypedDefinitionCodec.EncodeWeapon(new WeaponDefinition(BuildItem(), DamageExpression.Trim(), Range, AttackMode, WeaponActionCost, AmmoRequirement, keys));
                        break;
                    }
                case ContentDefinitionType.Armor:
                    {
                        var parts = new List<BodyPartId>();
                        foreach (string part in OdyUi.ParseList(CoveredBodyPartIdsText)) parts.Add(BodyPartId.Parse(part));
                        propertiesJson = TypedDefinitionCodec.EncodeArmor(new ArmorDefinition(BuildItem(), EquipmentSlotKey.Trim(), parts, Protection));
                        break;
                    }
                case ContentDefinitionType.Ammo:
                    propertiesJson = TypedDefinitionCodec.EncodeAmmo(new AmmoDefinition(BuildItem(), OdyUi.ParseList(AmmoCompatibilityKeysText), NullIfBlank(DamageContribution), new List<ContentDefinitionRef>(EffectContributionRefs)));
                    break;
                case ContentDefinitionType.Ability:
                    {
                        var costs = new List<AbilityResourceCost>();
                        foreach (ResourceCostModel cost in ResourceCosts) costs.Add(new AbilityResourceCost(ResourceDefinitionId.Parse(cost.ResourceDefinitionId.Trim()), cost.Amount));
                        propertiesJson = TypedDefinitionCodec.EncodeAbility(new AbilityDefinition(EntryPointType, Trigger.Trim(), AbilityActionCost, costs, AbilityTargetRule.Build(), NullIfBlank(AbilityMechanicsPayloadRef)));
                        break;
                    }
                case ContentDefinitionType.Effect:
                    propertiesJson = TypedDefinitionCodec.EncodeEffect(new EffectDefinition(EffectTargetRule.Build(), DurationType, DurationNeedsValue(DurationType) ? DurationValue : (long?)null, StackPolicy, NullIfBlank(EffectMechanicsPayloadRef)));
                    break;
                case ContentDefinitionType.Skill:
                    propertiesJson = TypedDefinitionCodec.EncodeSkill(new SkillDefinition());
                    break;
            }

            return true;
        }

        /// <summary>
        /// A purely advisory, client-side hint (never authoritative -- the publish validation decides): does any Ammo in
        /// <paramref name="catalog"/> share a key with this weapon's compatible ammo keys and list the active ruleset?
        /// </summary>
        public bool HasCompatibleAmmoIn(IEnumerable<ContentDefinitionRecord> catalog, string activeRulesetKey, CorrelationId correlationId)
        {
            List<string> weaponKeys = OdyUi.ParseList(CompatibleAmmoKeysText);
            if (weaponKeys.Count == 0) return false;
            foreach (ContentDefinitionRecord candidate in catalog)
            {
                if (candidate.DefinitionType != ContentDefinitionType.Ammo) continue;
                if (candidate.RulesetCompatibility.Count > 0 && !Contains(candidate.RulesetCompatibility, activeRulesetKey)) continue;
                Result<AmmoDefinition> ammo = TypedDefinitionCodec.DecodeAmmo(candidate.DefinitionType, candidate.PropertiesJson, correlationId);
                if (ammo.IsFailure) continue;
                foreach (string key in ammo.Value.CompatibilityKeys)
                {
                    if (weaponKeys.Contains(key)) return true;
                }
            }

            return false;
        }

        private void ValidateItem(List<CatalogFieldError> errors)
        {
            if (Weight < 0) errors.Add(new CatalogFieldError("item.weight", "Weight cannot be negative."));
            if (IsStackable && MaxStackSize < 1) errors.Add(new CatalogFieldError("item.maxStack", "Max stack size must be at least 1."));
            if (HasDurability && MaxDurability < 1) errors.Add(new CatalogFieldError("item.maxDurability", "Max durability must be at least 1."));
            if (HasCharges && MaxCharges < 1) errors.Add(new CatalogFieldError("item.maxCharges", "Max charges must be at least 1."));
        }

        private ItemDefinition BuildItem() => new ItemDefinition(
            Category,
            IsStackable,
            IsStackable ? MaxStackSize : (long?)null,
            Weight,
            HasDurability,
            HasDurability ? MaxDurability : (long?)null,
            HasCharges,
            HasCharges ? MaxCharges : (long?)null,
            new List<ContentDefinitionRef>(BuiltInAbilityRefs),
            new List<ContentDefinitionRef>(BuiltInEffectRefs));

        private void LoadItem(ItemDefinition item)
        {
            Category = item.Category;
            IsStackable = item.IsStackable;
            MaxStackSize = item.MaxStackSize ?? 1;
            Weight = item.Weight;
            HasDurability = item.HasDurability;
            MaxDurability = item.MaxDurability ?? 1;
            HasCharges = item.HasCharges;
            MaxCharges = item.MaxCharges ?? 1;
            BuiltInAbilityRefs.AddRange(item.BuiltInAbilityRefs);
            BuiltInEffectRefs.AddRange(item.BuiltInEffectRefs);
        }

        private void CopyTargetRule(ContentTargetRule rule, TargetRuleModel target)
        {
            TargetRuleModel loaded = TargetRuleModel.From(rule);
            target.Source = loaded.Source;
            target.MinimumCount = loaded.MinimumCount;
            target.MaximumCount = loaded.MaximumCount;
            target.AllowSelf = loaded.AllowSelf;
        }

        private static string? NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            foreach (string candidate in values)
            {
                if (string.Equals(candidate, value, StringComparison.Ordinal)) return true;
            }

            return false;
        }
    }
}
