using System;
using System.Collections.Generic;
using Odyssey.Domain.Character;

namespace Odyssey.Domain.Content
{
    /// <summary>
    /// ODY-S05-105: Base Definition Types only. Pure Domain value types --
    /// no serializer, no persistence, no Unity dependency (`ADR-001`).
    /// Structurally validated only (enum defined, required field non-null,
    /// obviously-impossible negative numbers rejected) -- deliberately no
    /// game-usability validation ("weapon has a valid ammo reference",
    /// "armor's body part exists", "ability cost is payable", ContentBlock
    /// cycle detection, Ruleset compatibility). That is `ODY-S05-104`'s own
    /// job; this task only answers "what fields exist."
    ///
    /// Reuses `ODY-S04-108`/`109`'s own already-established
    /// <see cref="ResourceDefinitionId"/>/<see cref="BodyPartId"/> catalog
    /// keys directly rather than inventing incompatible parallel types --
    /// per this task's own explicit instruction not to duplicate existing
    /// `SLICE-04` concepts.
    /// </summary>
    public enum ItemCategory
    {
        Generic = 1,
        Consumable = 2,
        Container = 3,
        QuestItem = 4,
        Misc = 5
    }

    /// <summary>
    /// ODY-S05-105: `ADR-027` section 3.2 -- "`ItemDefinition` is the common
    /// content definition for item mechanics." Every item-shaped
    /// `ContentDefinition` (`DefinitionType` = `Item`/`Weapon`/`Armor`/`Ammo`)
    /// carries these common fields; <see cref="WeaponDefinition"/>/
    /// <see cref="ArmorDefinition"/>/<see cref="AmmoDefinition"/> each embed
    /// one <see cref="ItemDefinition"/> plus their own typed-specific fields
    /// -- composition, not a separate independent runtime aggregate kind
    /// (`ADR-027` section 3.2's own explicit statement). Display/name-level
    /// data intentionally stays on the generic `ContentDefinition` envelope
    /// (`ODY-S05-101`'s own `Name`/`Description`), not duplicated here.
    /// `Durability`/`Charges` here are definition-level *capability*
    /// declarations only (does this item have durability/charges at all,
    /// and what is its maximum) -- no runtime current-durability/
    /// current-charge state exists anywhere in this type (`ADR-027`
    /// section 4's catalog/runtime boundary).
    /// </summary>
    public sealed class ItemDefinition
    {
        public ItemDefinition(
            ItemCategory category,
            bool isStackable,
            long? maxStackSize,
            long weight,
            bool hasDurability,
            long? maxDurability,
            bool hasCharges,
            long? maxCharges,
            IReadOnlyList<ContentDefinitionRef> builtInAbilityRefs,
            IReadOnlyList<ContentDefinitionRef> builtInEffectRefs)
        {
            if (!Enum.IsDefined(typeof(ItemCategory), category)) throw new ArgumentOutOfRangeException(nameof(category));
            if (isStackable && (!maxStackSize.HasValue || maxStackSize.Value < 1)) throw new ArgumentException("MaxStackSize is required and must be >= 1 when IsStackable is true.", nameof(maxStackSize));
            if (!isStackable && maxStackSize.HasValue) throw new ArgumentException("MaxStackSize must be null when IsStackable is false.", nameof(maxStackSize));
            if (weight < 0) throw new ArgumentOutOfRangeException(nameof(weight));
            if (hasDurability && (!maxDurability.HasValue || maxDurability.Value < 1)) throw new ArgumentException("MaxDurability is required and must be >= 1 when HasDurability is true.", nameof(maxDurability));
            if (!hasDurability && maxDurability.HasValue) throw new ArgumentException("MaxDurability must be null when HasDurability is false.", nameof(maxDurability));
            if (hasCharges && (!maxCharges.HasValue || maxCharges.Value < 1)) throw new ArgumentException("MaxCharges is required and must be >= 1 when HasCharges is true.", nameof(maxCharges));
            if (!hasCharges && maxCharges.HasValue) throw new ArgumentException("MaxCharges must be null when HasCharges is false.", nameof(maxCharges));

            Category = category;
            IsStackable = isStackable;
            MaxStackSize = maxStackSize;
            Weight = weight;
            HasDurability = hasDurability;
            MaxDurability = maxDurability;
            HasCharges = hasCharges;
            MaxCharges = maxCharges;
            BuiltInAbilityRefs = builtInAbilityRefs ?? throw new ArgumentNullException(nameof(builtInAbilityRefs));
            BuiltInEffectRefs = builtInEffectRefs ?? throw new ArgumentNullException(nameof(builtInEffectRefs));
        }

        public ItemCategory Category { get; }
        public bool IsStackable { get; }
        public long? MaxStackSize { get; }

        /// <summary>Weight/bulk carrying metric. No existing product vocabulary fixes a unit; this is a plain numeric capacity cost, matching every other unimplemented-balance numeric field in this codebase's own established "fixture value, not concrete balance" convention.</summary>
        public long Weight { get; }
        public bool HasDurability { get; }
        public long? MaxDurability { get; }
        public bool HasCharges { get; }
        public long? MaxCharges { get; }

        /// <summary>Exact-version references to `AbilityDefinition`s this item grants while owned/equipped -- `ADR-027` section 8.1's `CharacterAbility SourceKind=Item` integration point for a future task; this task only proves the reference itself exists and round-trips.</summary>
        public IReadOnlyList<ContentDefinitionRef> BuiltInAbilityRefs { get; }

        /// <summary>Exact-version references to `EffectDefinition`s this item can apply -- `ADR-027` section 8.2's future `ActiveEffect` creation integration point; this task only proves the reference itself exists and round-trips.</summary>
        public IReadOnlyList<ContentDefinitionRef> BuiltInEffectRefs { get; }
    }

    /// <summary>`11_Content_Block_System`-adjacent MVP attack-mode vocabulary; no attack resolution is implemented anywhere in this task.</summary>
    public enum WeaponAttackMode
    {
        Melee = 1,
        Ranged = 2,
        Thrown = 3
    }

    /// <summary>Whether a weapon needs ammo at all -- the actual compatibility/availability check is `ODY-S05-104`'s own validation job.</summary>
    public enum AmmoRequirement
    {
        None = 1,
        Required = 2,
        Optional = 3
    }

    /// <summary>
    /// ODY-S05-105: typed properties for a Weapon-shaped `ContentDefinition`.
    /// <see cref="DamageExpression"/> is an opaque formula string (no dice/
    /// damage-formula grammar exists for items yet, mirroring
    /// `09_Dice_And_Game_Log`'s own `DiceFormulaParser` grammar being a
    /// separate, already-implemented concern this task does not reuse or
    /// duplicate). <see cref="CompatibleAmmoKeys"/> is a plain string
    /// tag/category match (e.g. "9mm", "arrow"), not an exact-version
    /// `ContentDefinitionRef` -- a weapon is compatible with a *category* of
    /// ammo, not one specific published Ammo version; real compatibility
    /// checking is `ODY-S05-104`'s own job, this only stores the shape.
    /// </summary>
    public sealed class WeaponDefinition
    {
        public WeaponDefinition(
            ItemDefinition item,
            string damageExpression,
            long range,
            WeaponAttackMode attackMode,
            long actionCost,
            AmmoRequirement ammoRequirement,
            IReadOnlyList<string> compatibleAmmoKeys)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (string.IsNullOrWhiteSpace(damageExpression)) throw new ArgumentException("DamageExpression is required.", nameof(damageExpression));
            if (range < 0) throw new ArgumentOutOfRangeException(nameof(range));
            if (!Enum.IsDefined(typeof(WeaponAttackMode), attackMode)) throw new ArgumentOutOfRangeException(nameof(attackMode));
            if (actionCost < 0) throw new ArgumentOutOfRangeException(nameof(actionCost));
            if (!Enum.IsDefined(typeof(AmmoRequirement), ammoRequirement)) throw new ArgumentOutOfRangeException(nameof(ammoRequirement));
            if (compatibleAmmoKeys == null) throw new ArgumentNullException(nameof(compatibleAmmoKeys));
            if (ammoRequirement == AmmoRequirement.None && compatibleAmmoKeys.Count > 0) throw new ArgumentException("CompatibleAmmoKeys must be empty when AmmoRequirement is None.", nameof(compatibleAmmoKeys));

            Item = item;
            DamageExpression = damageExpression;
            Range = range;
            AttackMode = attackMode;
            ActionCost = actionCost;
            AmmoRequirement = ammoRequirement;
            CompatibleAmmoKeys = compatibleAmmoKeys;
        }

        public ItemDefinition Item { get; }
        public string DamageExpression { get; }
        public long Range { get; }
        public WeaponAttackMode AttackMode { get; }
        public long ActionCost { get; }
        public AmmoRequirement AmmoRequirement { get; }
        public IReadOnlyList<string> CompatibleAmmoKeys { get; }
    }

    /// <summary>
    /// ODY-S05-105: typed properties for an Armor-shaped `ContentDefinition`.
    /// <see cref="CoveredBodyPartIds"/> reuses `ODY-S04-109`'s own
    /// <see cref="BodyPartId"/> catalog key directly (not a duplicate type)
    /// -- whether the referenced body part actually exists on a given
    /// Character's own `CharacterAnatomy` is `ODY-S05-104`'s own validation
    /// job, this only stores the reference. <see cref="EquipmentSlotKey"/>
    /// is a plain string key -- no `EquipmentSlot` catalog type exists
    /// anywhere yet (that is a future Equipment-runtime concern, `ADR-027`
    /// section 7, not this task's own scope). Durability is the shared
    /// `ItemDefinition.HasDurability`/`MaxDurability` capability fields --
    /// not duplicated here.
    /// </summary>
    public sealed class ArmorDefinition
    {
        public ArmorDefinition(
            ItemDefinition item,
            string equipmentSlotKey,
            IReadOnlyList<BodyPartId> coveredBodyPartIds,
            long protection)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (string.IsNullOrWhiteSpace(equipmentSlotKey)) throw new ArgumentException("EquipmentSlotKey is required.", nameof(equipmentSlotKey));
            if (coveredBodyPartIds == null || coveredBodyPartIds.Count == 0) throw new ArgumentException("At least one covered BodyPartId is required.", nameof(coveredBodyPartIds));
            foreach (BodyPartId bodyPartId in coveredBodyPartIds)
            {
                if (!bodyPartId.IsValid) throw new ArgumentException("CoveredBodyPartIds must all be valid.", nameof(coveredBodyPartIds));
            }

            if (protection < 0) throw new ArgumentOutOfRangeException(nameof(protection));

            Item = item;
            EquipmentSlotKey = equipmentSlotKey;
            CoveredBodyPartIds = coveredBodyPartIds;
            Protection = protection;
        }

        public ItemDefinition Item { get; }
        public string EquipmentSlotKey { get; }
        public IReadOnlyList<BodyPartId> CoveredBodyPartIds { get; }
        public long Protection { get; }
    }

    /// <summary>
    /// ODY-S05-105: typed properties for an Ammo-shaped `ContentDefinition`.
    /// <see cref="CompatibilityKeys"/> mirrors <see cref="WeaponDefinition.CompatibleAmmoKeys"/>'s
    /// own plain-string tag/category match -- the two sides meet on a
    /// shared key vocabulary, not an exact-version pin (a weapon does not
    /// depend on one specific published Ammo version). <see cref="EffectContributionRefs"/>
    /// is where this task's own exact-version `ContentDefinitionRef` proof
    /// point lives for Ammo specifically (e.g. incendiary rounds
    /// referencing a specific published burn `EffectDefinition` version).
    /// </summary>
    public sealed class AmmoDefinition
    {
        public AmmoDefinition(
            ItemDefinition item,
            IReadOnlyList<string> compatibilityKeys,
            string? damageContribution,
            IReadOnlyList<ContentDefinitionRef> effectContributionRefs)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (compatibilityKeys == null || compatibilityKeys.Count == 0) throw new ArgumentException("At least one CompatibilityKey is required.", nameof(compatibilityKeys));

            Item = item;
            CompatibilityKeys = compatibilityKeys;
            DamageContribution = damageContribution;
            EffectContributionRefs = effectContributionRefs ?? throw new ArgumentNullException(nameof(effectContributionRefs));
        }

        public ItemDefinition Item { get; }
        public IReadOnlyList<string> CompatibilityKeys { get; }

        /// <summary>Optional opaque damage-formula contribution (e.g. "+1d4") this ammo adds to the firing weapon's own damage -- no formula grammar/combination logic exists yet; a plain string capability field only.</summary>
        public string? DamageContribution { get; }
        public IReadOnlyList<ContentDefinitionRef> EffectContributionRefs { get; }
    }

    /// <summary>`11_Content_Block_System` section 14's `TargetSource` vocabulary, narrowed to the MVP subset this task's own typed shape needs -- no `SelectTargetsBlock` execution (`FilterConditions`/`RangeRule`/`VisibilityRule`/etc.) is implemented.</summary>
    public enum ContentTargetSource
    {
        ManualSelection = 1,
        SourceEntity = 2,
        ActingCharacter = 3,
        AreaContents = 4,
        GMSelection = 5
    }

    /// <summary>ODY-S05-105: a minimal, non-executable target-rules shape shared by <see cref="AbilityDefinition"/> and <see cref="EffectDefinition"/>, per `11_Content_Block_System` section 14's own `SelectTargetsBlock` vocabulary narrowed to structural fields only.</summary>
    public sealed class ContentTargetRule
    {
        public ContentTargetRule(ContentTargetSource targetSource, long minimumCount, long maximumCount, bool allowSelf)
        {
            if (!Enum.IsDefined(typeof(ContentTargetSource), targetSource)) throw new ArgumentOutOfRangeException(nameof(targetSource));
            if (minimumCount < 0) throw new ArgumentOutOfRangeException(nameof(minimumCount));
            if (maximumCount < minimumCount) throw new ArgumentOutOfRangeException(nameof(maximumCount), "MaximumCount must be >= MinimumCount.");

            TargetSource = targetSource;
            MinimumCount = minimumCount;
            MaximumCount = maximumCount;
            AllowSelf = allowSelf;
        }

        public ContentTargetSource TargetSource { get; }
        public long MinimumCount { get; }
        public long MaximumCount { get; }
        public bool AllowSelf { get; }
    }

    /// <summary>`11_Content_Block_System` section 7's MVP `ContentEntryPoint` type vocabulary, verbatim (12 values).</summary>
    public enum AbilityEntryPointType
    {
        ActiveAction = 1,
        Passive = 2,
        Reaction = 3,
        OnApply = 4,
        OnRemove = 5,
        OnTurnStart = 6,
        OnTurnEnd = 7,
        OnDamageReceived = 8,
        OnDamageDealt = 9,
        OnCriticalSuccess = 10,
        OnCriticalFailure = 11,
        ManualGMTrigger = 12
    }

    /// <summary>ODY-S05-105: a minimal resource-cost line, reusing `ODY-S04-109`'s own <see cref="ResourceDefinitionId"/> directly rather than a duplicate type. No reservation/spend mechanism is implemented -- `11_Content_Block_System` section 15's own `CostBlock` execution is a future concern.</summary>
    public sealed class AbilityResourceCost
    {
        public AbilityResourceCost(ResourceDefinitionId resourceDefinitionId, long amount)
        {
            if (!resourceDefinitionId.IsValid) throw new ArgumentException("ResourceDefinitionId is required.", nameof(resourceDefinitionId));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));

            ResourceDefinitionId = resourceDefinitionId;
            Amount = amount;
        }

        public ResourceDefinitionId ResourceDefinitionId { get; }
        public long Amount { get; }
    }

    /// <summary>
    /// ODY-S05-105: typed properties for an Ability-shaped `ContentDefinition`.
    /// <see cref="MechanicsPayloadRef"/> is a deliberately opaque placeholder
    /// for a future `11_Content_Block_System` section 8 `ContentBlockGraph`
    /// reference -- implementing real block graphs/execution is explicitly
    /// out of this task's own scope ("не реализовывать выполнение
    /// способности").
    /// </summary>
    public sealed class AbilityDefinition
    {
        public AbilityDefinition(
            AbilityEntryPointType entryPointType,
            string trigger,
            long actionCost,
            IReadOnlyList<AbilityResourceCost> resourceCosts,
            ContentTargetRule targetRule,
            string? mechanicsPayloadRef)
        {
            if (!Enum.IsDefined(typeof(AbilityEntryPointType), entryPointType)) throw new ArgumentOutOfRangeException(nameof(entryPointType));
            if (string.IsNullOrWhiteSpace(trigger)) throw new ArgumentException("Trigger is required.", nameof(trigger));
            if (actionCost < 0) throw new ArgumentOutOfRangeException(nameof(actionCost));
            if (resourceCosts == null) throw new ArgumentNullException(nameof(resourceCosts));
            if (targetRule == null) throw new ArgumentNullException(nameof(targetRule));

            EntryPointType = entryPointType;
            Trigger = trigger;
            ActionCost = actionCost;
            ResourceCosts = resourceCosts;
            TargetRule = targetRule;
            MechanicsPayloadRef = mechanicsPayloadRef;
        }

        public AbilityEntryPointType EntryPointType { get; }
        public string Trigger { get; }
        public long ActionCost { get; }
        public IReadOnlyList<AbilityResourceCost> ResourceCosts { get; }
        public ContentTargetRule TargetRule { get; }
        public string? MechanicsPayloadRef { get; }
    }

    /// <summary>`11_Content_Block_System` section 22's effect-duration vocabulary, verbatim (15 values including `Instant`/`WhileItemEquipped`).</summary>
    public enum EffectDurationType
    {
        Instant = 1,
        Permanent = 2,
        UntilRemoved = 3,
        ForRounds = 4,
        ForTurns = 5,
        ForDuration = 6,
        UntilSceneChange = 7,
        UntilSessionEnd = 8,
        UntilSourceTurnStart = 9,
        UntilSourceTurnEnd = 10,
        UntilTargetTurnStart = 11,
        UntilTargetTurnEnd = 12,
        WhileCondition = 13,
        WhileSourceExists = 14,
        WhileItemEquipped = 15
    }

    /// <summary>`11_Content_Block_System` section 21.1's `StackPolicy` vocabulary, verbatim (7 values).</summary>
    public enum EffectStackPolicy
    {
        IndependentInstances = 1,
        RefreshDuration = 2,
        ReplaceIfStronger = 3,
        ReplaceExisting = 4,
        IncreaseStacks = 5,
        IgnoreNewApplication = 6,
        RequestGMResolution = 7
    }

    /// <summary>
    /// ODY-S05-105: typed properties for an Effect-shaped `ContentDefinition`.
    /// <see cref="MechanicsPayloadRef"/> is the snapshot-relevant mechanics
    /// placeholder `ADR-027` section 6's `DefinitionMechanicsSnapshot`/
    /// future `ActiveEffect.EffectMechanicsSnapshot` will eventually copy
    /// from -- no `ActiveEffect` aggregate or snapshot-copy mechanism is
    /// implemented by this task.
    /// </summary>
    public sealed class EffectDefinition
    {
        public EffectDefinition(
            ContentTargetRule targetRule,
            EffectDurationType durationType,
            long? durationValue,
            EffectStackPolicy stackPolicy,
            string? mechanicsPayloadRef)
        {
            if (targetRule == null) throw new ArgumentNullException(nameof(targetRule));
            if (!Enum.IsDefined(typeof(EffectDurationType), durationType)) throw new ArgumentOutOfRangeException(nameof(durationType));

            bool needsDurationValue = durationType == EffectDurationType.ForRounds || durationType == EffectDurationType.ForTurns || durationType == EffectDurationType.ForDuration;
            if (needsDurationValue && (!durationValue.HasValue || durationValue.Value < 1)) throw new ArgumentException("DurationValue is required and must be >= 1 for ForRounds/ForTurns/ForDuration.", nameof(durationValue));
            if (!needsDurationValue && durationValue.HasValue) throw new ArgumentException("DurationValue must be null for this DurationType.", nameof(durationValue));
            if (!Enum.IsDefined(typeof(EffectStackPolicy), stackPolicy)) throw new ArgumentOutOfRangeException(nameof(stackPolicy));

            TargetRule = targetRule;
            DurationType = durationType;
            DurationValue = durationValue;
            StackPolicy = stackPolicy;
            MechanicsPayloadRef = mechanicsPayloadRef;
        }

        public ContentTargetRule TargetRule { get; }
        public EffectDurationType DurationType { get; }
        public long? DurationValue { get; }
        public EffectStackPolicy StackPolicy { get; }
        public string? MechanicsPayloadRef { get; }
    }
}
