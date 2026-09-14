using System;
using Odyssey.Domain.Character;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;

namespace Odyssey.Domain.Effects
{
    /// <summary>
    /// ODY-S05-502: canonical id for <see cref="ActiveEffect"/>, following the
    /// exact prefix/hex-32/`CanonicalId` pattern every other id type in this
    /// codebase already uses (<c>ItemInstanceId</c>, <c>EquippedEntry</c>'s
    /// own implicit identity, etc.) -- no new id convention is invented.
    /// </summary>
    public readonly struct ActiveEffectId : IEquatable<ActiveEffectId>
    {
        private const string Prefix = "aeff_";
        private const int HexLength = 32;
        private readonly string _value;

        private ActiveEffectId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static ActiveEffectId NewId(UtcInstant now) => new ActiveEffectId(Prefix + Uuid7.NewHex32(now));
        public static bool TryParse(string? value, out ActiveEffectId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new ActiveEffectId(v));
        public static ActiveEffectId Parse(string value) => TryParse(value, out ActiveEffectId id) ? id : throw new FormatException("ActiveEffectId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(ActiveEffectId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is ActiveEffectId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(ActiveEffectId left, ActiveEffectId right) => left.Equals(right);
        public static bool operator !=(ActiveEffectId left, ActiveEffectId right) => !left.Equals(right);
    }

    /// <summary>`ADR-028` §5.2 rule 3's own lifecycle-state vocabulary. `Suspended` exists only for `WhileItemEquipped` effects while the source item is unequipped but not yet destroyed/consumed (`ODY-S05-505`'s own job to transition); every other duration mechanism only ever moves `Active` to `Expired` or `Removed` directly.</summary>
    public enum ActiveEffectStatus
    {
        Active = 1,
        Expired = 2,
        Suspended = 3,
        Removed = 4
    }

    /// <summary>`ADR-028` §5.3's own `SourceRef` kind vocabulary: what created this effect. `Item`/`EquippedItem` carry an <see cref="InventoryItemRef"/> (itself already the established discriminated union over `ItemInstance`/`ItemStack`); `Action`/`GMDirect` carry no item reference at all.</summary>
    public enum ActiveEffectSourceKind
    {
        Item = 1,
        EquippedItem = 2,
        Action = 3,
        GMDirect = 4
    }

    /// <summary>
    /// ODY-S05-502: `ADR-028` §5.3's own extensible, kind-tagged `SourceRef` --
    /// a new discriminated-union struct written from scratch by direct analogy
    /// to <see cref="InventoryItemRef"/>'s own two-kind pattern (kind tag +
    /// conditionally-valid payload + factory methods + `IsValid` checking
    /// exactly the fields the kind requires), not a reuse of
    /// <see cref="InventoryItemRef"/> itself -- these are different reference
    /// domains (what created an effect vs. which runtime item/stack a
    /// location refers to) that happen to share one payload shape for two of
    /// their four kinds.
    /// </summary>
    public readonly struct ActiveEffectSourceRef : IEquatable<ActiveEffectSourceRef>
    {
        private ActiveEffectSourceRef(ActiveEffectSourceKind kind, InventoryItemRef itemRef)
        {
            Kind = kind;
            ItemRef = itemRef;
        }

        public ActiveEffectSourceKind Kind { get; }

        /// <summary>Valid and meaningful only for <see cref="ActiveEffectSourceKind.Item"/>/<see cref="ActiveEffectSourceKind.EquippedItem"/>; <c>default</c> (invalid) for <see cref="ActiveEffectSourceKind.Action"/>/<see cref="ActiveEffectSourceKind.GMDirect"/>, which name no item at all.</summary>
        public InventoryItemRef ItemRef { get; }

        public bool IsValid => Kind switch
        {
            ActiveEffectSourceKind.Item => ItemRef.IsValid,
            ActiveEffectSourceKind.EquippedItem => ItemRef.IsValid,
            ActiveEffectSourceKind.Action => true,
            ActiveEffectSourceKind.GMDirect => true,
            _ => false
        };

        public static ActiveEffectSourceRef ForItem(InventoryItemRef itemRef)
        {
            if (!itemRef.IsValid) throw new ArgumentException("ItemRef is required.", nameof(itemRef));
            return new ActiveEffectSourceRef(ActiveEffectSourceKind.Item, itemRef);
        }

        public static ActiveEffectSourceRef ForEquippedItem(InventoryItemRef itemRef)
        {
            if (!itemRef.IsValid) throw new ArgumentException("ItemRef is required.", nameof(itemRef));
            return new ActiveEffectSourceRef(ActiveEffectSourceKind.EquippedItem, itemRef);
        }

        /// <summary>`ADR-028` §5.3: a source with no item reference at all -- a future Block-3 action/ability trigger. `ODY-S05-502` only proves the reference shape round-trips; nothing yet constructs one through a real action.</summary>
        public static ActiveEffectSourceRef ForAction() => new ActiveEffectSourceRef(ActiveEffectSourceKind.Action, default);

        /// <summary>`ADR-028` §10 rule 2: MainGM applying an effect with no item cause at all. `ODY-S05-502` only proves the reference shape round-trips; `ODY-S05-506` wires the actual MainGM-only command that constructs one.</summary>
        public static ActiveEffectSourceRef ForGMDirect() => new ActiveEffectSourceRef(ActiveEffectSourceKind.GMDirect, default);

        public bool Equals(ActiveEffectSourceRef other) => Kind == other.Kind && ItemRef.Equals(other.ItemRef);
        public override bool Equals(object? obj) => obj is ActiveEffectSourceRef other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Kind, ItemRef);
    }

    /// <summary>`ADR-028` §5.3's own `TargetRef` kind vocabulary: what this effect affects.</summary>
    public enum ActiveEffectTargetKind
    {
        Character = 1,
        ItemInstance = 2,

        /// <summary>
        /// `ADR-027` §8.2's own text names a scene object as a supported
        /// `ActiveEffect` target, but no `SceneObject` domain type exists
        /// anywhere in the codebase today (confirmed by direct repository
        /// search during `ODY-S05-501`). This value is declared here so the
        /// enum matches `ADR-028` §5.3 in full, but it is deliberately
        /// <b>unconstructable</b>: <see cref="ActiveEffectTargetRef"/> has no
        /// <c>ForSceneObject</c> factory, and no code anywhere sets
        /// <see cref="ActiveEffectTargetRef.Kind"/> to this value. A future
        /// task that adds a real `SceneObject` type must add that factory
        /// and this kind's own id-carrying field at the same time -- the
        /// same "structurally accepted, not yet automatically producible"
        /// pattern <c>Ability.cs</c>'s own <c>SourceKind.ActiveEffect</c>/
        /// <c>CharacterTemplate</c> values already use for their own
        /// not-yet-wired cases.
        /// </summary>
        SceneObject = 3
    }

    /// <summary>
    /// ODY-S05-502: `ADR-028` §5.3's own extensible, kind-tagged `TargetRef`
    /// -- written from scratch by direct analogy to
    /// <see cref="InventoryItemRef"/>'s own pattern, not a reuse of it (a
    /// target is a Character or an item, never a stack, and the domains
    /// differ). See <see cref="ActiveEffectTargetKind.SceneObject"/>'s own
    /// doc comment for why that one kind has no constructing factory yet.
    /// </summary>
    public readonly struct ActiveEffectTargetRef : IEquatable<ActiveEffectTargetRef>
    {
        private ActiveEffectTargetRef(ActiveEffectTargetKind kind, CharacterId characterId, ItemInstanceId itemInstanceId)
        {
            Kind = kind;
            CharacterId = characterId;
            ItemInstanceId = itemInstanceId;
        }

        public ActiveEffectTargetKind Kind { get; }
        public CharacterId CharacterId { get; }
        public ItemInstanceId ItemInstanceId { get; }

        public bool IsValid => Kind switch
        {
            ActiveEffectTargetKind.Character => CharacterId.IsValid && !ItemInstanceId.IsValid,
            ActiveEffectTargetKind.ItemInstance => ItemInstanceId.IsValid && !CharacterId.IsValid,
            // SceneObject is declared but unconstructable -- see the enum's own doc comment. No id field backs it yet, so it can never be valid.
            _ => false
        };

        public static ActiveEffectTargetRef ForCharacter(CharacterId characterId)
        {
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            return new ActiveEffectTargetRef(ActiveEffectTargetKind.Character, characterId, default);
        }

        public static ActiveEffectTargetRef ForItemInstance(ItemInstanceId itemInstanceId)
        {
            if (!itemInstanceId.IsValid) throw new ArgumentException("ItemInstanceId is required.", nameof(itemInstanceId));
            return new ActiveEffectTargetRef(ActiveEffectTargetKind.ItemInstance, default, itemInstanceId);
        }

        public bool Equals(ActiveEffectTargetRef other) => Kind == other.Kind && CharacterId.Equals(other.CharacterId) && ItemInstanceId.Equals(other.ItemInstanceId);
        public override bool Equals(object? obj) => obj is ActiveEffectTargetRef other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Kind, CharacterId, ItemInstanceId);
    }

    /// <summary>
    /// ODY-S05-502: `ADR-028` §3.2's own `EffectMechanicsSnapshot` concept --
    /// an immutable copy of an `EffectDefinition`'s mechanics captured at
    /// application time, written by direct structural analogy to
    /// <see cref="Odyssey.Domain.Inventory.ItemMechanicsSnapshot"/> (same four
    /// fields, same validation), reinvented for effects rather than shared
    /// with it -- items and effects are different snapshot domains even
    /// though their shape happens to coincide today.
    /// </summary>
    public readonly struct EffectMechanicsSnapshot : IEquatable<EffectMechanicsSnapshot>
    {
        public EffectMechanicsSnapshot(ContentDefinitionRef sourceDefinitionRef, long definitionSnapshotVersion, ContentDefinitionType contentType, string payload)
        {
            if (!sourceDefinitionRef.IsValid) throw new ArgumentException("Source definition ref is required.", nameof(sourceDefinitionRef));
            if (definitionSnapshotVersion < 1) throw new ArgumentOutOfRangeException(nameof(definitionSnapshotVersion));
            if (!Enum.IsDefined(typeof(ContentDefinitionType), contentType)) throw new ArgumentOutOfRangeException(nameof(contentType));
            if (string.IsNullOrWhiteSpace(payload)) throw new ArgumentException("Payload is required.", nameof(payload));

            SourceDefinitionRef = sourceDefinitionRef;
            DefinitionSnapshotVersion = definitionSnapshotVersion;
            ContentType = contentType;
            Payload = payload;
        }

        public ContentDefinitionRef SourceDefinitionRef { get; }
        public long DefinitionSnapshotVersion { get; }
        public ContentDefinitionType ContentType { get; }
        public string Payload { get; }
        public bool Equals(EffectMechanicsSnapshot other) => SourceDefinitionRef.Equals(other.SourceDefinitionRef) && DefinitionSnapshotVersion == other.DefinitionSnapshotVersion && ContentType == other.ContentType && string.Equals(Payload, other.Payload, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is EffectMechanicsSnapshot other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(SourceDefinitionRef, DefinitionSnapshotVersion, ContentType, Payload == null ? 0 : StringComparer.Ordinal.GetHashCode(Payload));
    }

    /// <summary>
    /// ODY-S05-605: `ADR-029` §7's own combat-duration binding snapshot,
    /// captured once at application time for each of the six turn/round-based
    /// `EffectDurationType` values `ADR-028` §13 reserved. Mirrors
    /// <see cref="ActiveEffect.ExpiresAt"/>'s own "populated only for duration
    /// types that need it" idiom -- this is <see cref="ActiveEffect.CombatBinding"/>'s
    /// null-only-when-irrelevant field, not a parallel aggregate. Captures
    /// exactly what `ADR-029` §7's own closing paragraph requires ("the
    /// encounter ID, source/target bindings, relevant ordinal ... with the
    /// application snapshot"), plus <see cref="AppliedLifecycleEventId"/> (a
    /// `CombatEncounterLifecycleEvent` high-water-mark) and
    /// <see cref="RequiredCount"/> (the Ruleset-content-supplied `N` for
    /// `ForRounds`/`ForTurns`, unused/zero for the four boundary-only types)
    /// so a caller can determine "has the relevant boundary already happened"
    /// without re-deriving it from encounter participant order, which `ADR-029`
    /// §7's own closing paragraph explicitly allows to change over time
    /// (joins/leaves/skips) -- see `ODY-S05-605`'s own task contract for why
    /// ordinal arithmetic alone is not safe against that.
    /// </summary>
    public readonly struct CombatDurationBinding : IEquatable<CombatDurationBinding>
    {
        public CombatDurationBinding(CombatEncounterId encounterId, CharacterId? sourceCombatantId, CharacterId? targetCombatantId, long appliedRoundOrdinal, long appliedLifecycleEventId, int requiredCount)
        {
            if (!encounterId.IsValid) throw new ArgumentException("EncounterId is required.", nameof(encounterId));
            if (sourceCombatantId == null && targetCombatantId == null) throw new ArgumentException("At least one combat participant binding is required.");
            if (sourceCombatantId.HasValue && !sourceCombatantId.Value.IsValid) throw new ArgumentException("SourceCombatantId, when supplied, must be valid.", nameof(sourceCombatantId));
            if (targetCombatantId.HasValue && !targetCombatantId.Value.IsValid) throw new ArgumentException("TargetCombatantId, when supplied, must be valid.", nameof(targetCombatantId));
            if (appliedRoundOrdinal < 1) throw new ArgumentOutOfRangeException(nameof(appliedRoundOrdinal));
            if (appliedLifecycleEventId < 0) throw new ArgumentOutOfRangeException(nameof(appliedLifecycleEventId));
            if (requiredCount < 0) throw new ArgumentOutOfRangeException(nameof(requiredCount));

            EncounterId = encounterId;
            SourceCombatantId = sourceCombatantId;
            TargetCombatantId = targetCombatantId;
            AppliedRoundOrdinal = appliedRoundOrdinal;
            AppliedLifecycleEventId = appliedLifecycleEventId;
            RequiredCount = requiredCount;
        }

        public CombatEncounterId EncounterId { get; }

        /// <summary>Populated for `ForRounds` (both bindings required) and the two `UntilSourceTurn*` values; null for the two `UntilTargetTurn*` values and unused for `ForTurns` (target-only per `ADR-029` §7's own table).</summary>
        public CharacterId? SourceCombatantId { get; }

        /// <summary>Populated for `ForRounds` (both bindings required), `ForTurns`, and the two `UntilTargetTurn*` values; null for the two `UntilSourceTurn*` values.</summary>
        public CharacterId? TargetCombatantId { get; }

        /// <summary>The encounter's own `RoundOrdinal` at the moment this effect was committed -- `ForRounds`'s own anchor; captured for every combat duration type for traceability even though only `ForRounds` compares against it.</summary>
        public long AppliedRoundOrdinal { get; }

        /// <summary>The `CombatEncounterLifecycleEvent` table's own highest `EventId` at the moment this effect was committed -- the high-water-mark a caller filters "strictly after application" audit rows against for the four turn-boundary values and `ForTurns`.</summary>
        public long AppliedLifecycleEventId { get; }

        /// <summary>`ForRounds`/`ForTurns`'s own Ruleset-content-supplied `N`; `0` (unused) for the four boundary-only values.</summary>
        public int RequiredCount { get; }

        public bool Equals(CombatDurationBinding other) => EncounterId.Equals(other.EncounterId) && SourceCombatantId.Equals(other.SourceCombatantId) && TargetCombatantId.Equals(other.TargetCombatantId) && AppliedRoundOrdinal == other.AppliedRoundOrdinal && AppliedLifecycleEventId == other.AppliedLifecycleEventId && RequiredCount == other.RequiredCount;
        public override bool Equals(object? obj) => obj is CombatDurationBinding other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(EncounterId, SourceCombatantId, TargetCombatantId, AppliedRoundOrdinal, AppliedLifecycleEventId, RequiredCount);
    }

    /// <summary>
    /// ODY-S05-502: `ADR-028` §5's own `ActiveEffect` aggregate -- the pure
    /// Domain identity/value half of it (`ADR-028` §15/`ADR-001`: Domain owns
    /// pure identity/value invariants, campaign-scoping is an
    /// Application-layer concern layered on top). This mirrors exactly how
    /// `EquippedEntry` (Domain) and `EquippedEntryRecord` (Application, adds
    /// only `CampaignId`) are already split for an analogous reason -- see
    /// this task's own ExecPlan/task contract for the full reasoning. This
    /// class carries 11 of `ADR-028` §5.1's 12 fields; `CampaignId` is the
    /// twelfth, added by the Application-layer `ActiveEffectRecord` wrapper.
    ///
    /// `ODY-S05-502` implements creation and basic persistence only -- no
    /// stacking-policy resolution (`ODY-S05-503`), no duration/expiry
    /// mechanism (`ODY-S05-504`/`505`), and no removal command (`ODY-S05-506`).
    /// This class's own constructor therefore accepts whatever `Status`/
    /// `StackCount`/`ExpiresAt` the caller supplies without computing them --
    /// those decisions belong entirely to the later tasks that own them.
    /// </summary>
    public sealed class ActiveEffect
    {
        public ActiveEffect(
            ActiveEffectId activeEffectId,
            ContentDefinitionRef effectDefinitionRef,
            EffectMechanicsSnapshot effectMechanicsSnapshot,
            ActiveEffectSourceRef sourceRef,
            ActiveEffectTargetRef targetRef,
            ActiveEffectStatus status,
            long stackCount,
            UserId appliedByUserId,
            UtcInstant appliedAt,
            UtcInstant? expiresAt,
            long revision,
            CombatDurationBinding? combatBinding = null)
        {
            if (!activeEffectId.IsValid) throw new ArgumentException("ActiveEffectId is required.", nameof(activeEffectId));
            if (!effectDefinitionRef.IsValid) throw new ArgumentException("EffectDefinitionRef is required.", nameof(effectDefinitionRef));
            if (!effectMechanicsSnapshot.SourceDefinitionRef.Equals(effectDefinitionRef)) throw new ArgumentException("EffectMechanicsSnapshot must match EffectDefinitionRef.", nameof(effectMechanicsSnapshot));
            if (!sourceRef.IsValid) throw new ArgumentException("SourceRef is required.", nameof(sourceRef));
            if (!targetRef.IsValid) throw new ArgumentException("TargetRef is required.", nameof(targetRef));
            if (!Enum.IsDefined(typeof(ActiveEffectStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if (stackCount < 1) throw new ArgumentOutOfRangeException(nameof(stackCount), "StackCount starts at 1 and is never zero/negative -- ADR-028 section 5.2 rule 4.");
            if (!appliedByUserId.IsValid) throw new ArgumentException("AppliedByUserId is required.", nameof(appliedByUserId));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));

            ActiveEffectId = activeEffectId;
            EffectDefinitionRef = effectDefinitionRef;
            EffectMechanicsSnapshot = effectMechanicsSnapshot;
            SourceRef = sourceRef;
            TargetRef = targetRef;
            Status = status;
            StackCount = stackCount;
            AppliedByUserId = appliedByUserId;
            AppliedAt = appliedAt;
            ExpiresAt = expiresAt;
            Revision = revision;
            CombatBinding = combatBinding;
        }

        public ActiveEffectId ActiveEffectId { get; }
        public ContentDefinitionRef EffectDefinitionRef { get; }
        public EffectMechanicsSnapshot EffectMechanicsSnapshot { get; }
        public ActiveEffectSourceRef SourceRef { get; }
        public ActiveEffectTargetRef TargetRef { get; }
        public ActiveEffectStatus Status { get; }

        /// <summary>Starts at 1 (`ADR-028` §5.2 rule 4). Only `ODY-S05-503`'s own `IncreaseStacks` policy ever increments it; every other stacking policy leaves it untouched for the lifetime of this row.</summary>
        public long StackCount { get; }

        public UserId AppliedByUserId { get; }
        public UtcInstant AppliedAt { get; }

        /// <summary>Populated only for duration types with a computable absolute timestamp at application time (`ADR-028` §5.2 rule 6). Null is not itself a defect -- it means this effect's own expiry is event/condition-driven, a decision `ODY-S05-504`/`505` own, not this task.</summary>
        public UtcInstant? ExpiresAt { get; }

        public long Revision { get; }

        /// <summary>ODY-S05-605: populated only for the six turn/round-based `EffectDurationType` values `ADR-028` §13 reserved and `ADR-029` §7 specifies; null for every other duration type, mirroring <see cref="ExpiresAt"/>'s own "populated only when relevant" idiom.</summary>
        public CombatDurationBinding? CombatBinding { get; }
    }
}
