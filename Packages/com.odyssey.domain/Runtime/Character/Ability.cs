using System;
using System.Text.RegularExpressions;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Domain.Character
{
    /// <summary>
    /// ODY-S04-108: product section 16's <c>AbilityDefinition</c> id --
    /// a stable, Ruleset/Content-Block-System-published catalog key, exactly
    /// mirroring <see cref="AttributeDefinitionId"/>/<see cref="SkillDefinitionId"/>'s
    /// own reasoning for why this is not a canonical
    /// <c>Prefix + Uuid7.NewHex32</c> random instance identifier.
    /// </summary>
    public readonly struct AbilityDefinitionId : IEquatable<AbilityDefinitionId>
    {
        private static readonly Regex ValidPattern = new Regex("^[A-Za-z][A-Za-z0-9_]{0,63}$", RegexOptions.Compiled);
        private readonly string _value;

        private AbilityDefinitionId(string value) => _value = value;
        public bool IsValid => _value != null;

        public static bool TryParse(string? value, out AbilityDefinitionId id)
        {
            if (value != null && ValidPattern.IsMatch(value))
            {
                id = new AbilityDefinitionId(value);
                return true;
            }

            id = default;
            return false;
        }

        public static AbilityDefinitionId Parse(string value) => TryParse(value, out AbilityDefinitionId id) ? id : throw new FormatException("AbilityDefinitionId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(AbilityDefinitionId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is AbilityDefinitionId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(AbilityDefinitionId left, AbilityDefinitionId right) => left.Equals(right);
        public static bool operator !=(AbilityDefinitionId left, AbilityDefinitionId right) => !left.Equals(right);
    }

    /// <summary>
    /// ODY-S04-108: product section 16's <c>CharacterAbility.SourceKind</c> --
    /// reused verbatim (six values, not renamed/narrowed). Only
    /// <see cref="ProgressionPurchase"/> spends <c>DevelopmentPool</c> and
    /// creates an <c>AdvancementPurchase</c> (ADR-024 section 5.1);
    /// <see cref="GMGrant"/> is MainGM-only and touches only the
    /// <c>CharacterAbilities</c> section. <see cref="CharacterTemplate"/>/
    /// <see cref="Item"/>/<see cref="ActiveEffect"/>/<see cref="RulesetAdvancement"/>
    /// are structurally accepted by <c>AcquireAbility</c> (a future
    /// template-copy/Item/ActiveEffect system will call it with these values
    /// itself) but this task implements no automatic acquisition through
    /// them -- see <c>SqliteCharacterRepository.AcquireAbility</c>'s own doc
    /// comment.
    /// </summary>
    public enum SourceKind
    {
        ProgressionPurchase = 1,
        GMGrant = 2,
        CharacterTemplate = 3,
        Item = 4,
        ActiveEffect = 5,
        RulesetAdvancement = 6
    }

    /// <summary>ODY-S04-108: product section 16's <c>CharacterAbility.RankMode</c> -- validated independently per mode by <see cref="CharacterAbility"/>'s own constructor.</summary>
    public enum RankMode
    {
        None = 1,
        Numeric = 2,
        Named = 3
    }

    /// <summary>
    /// ODY-S04-108: product section 16's <c>CharacterAbility</c> -- the
    /// Character-owned instance of a versioned, published
    /// <c>AbilityDefinition</c> (not implemented here; out of this task's
    /// scope beyond the id it is pinned to). <see cref="Configuration"/> is
    /// a minimal opaque string -- no real ability-configuration format
    /// exists anywhere in this codebase yet (confirmed by search); this is
    /// this task's own explicitly-flagged test fixture, mirroring
    /// <see cref="AdvancementPurchase.RequirementsSnapshot"/>'s own "{}"
    /// placeholder convention.
    /// </summary>
    public sealed class CharacterAbility
    {
        public CharacterAbility(
            CharacterAbilityId characterAbilityId,
            AbilityDefinitionId abilityDefinitionId,
            SourceKind sourceKind,
            string? sourceRef,
            UtcInstant acquiredAt,
            RankMode rankMode,
            long? numericRank,
            string? namedRankKey,
            bool isEnabled,
            string configuration,
            string? usesState,
            long revision)
        {
            if (!characterAbilityId.IsValid) throw new ArgumentException("CharacterAbilityId is required.", nameof(characterAbilityId));
            if (!abilityDefinitionId.IsValid) throw new ArgumentException("AbilityDefinitionId is required.", nameof(abilityDefinitionId));
            if (!Enum.IsDefined(typeof(SourceKind), sourceKind)) throw new ArgumentOutOfRangeException(nameof(sourceKind));
            if (!Enum.IsDefined(typeof(RankMode), rankMode)) throw new ArgumentOutOfRangeException(nameof(rankMode));

            // Product section 16: RankMode validated independently per mode --
            // None carries neither rank field, Numeric carries only
            // NumericRank, Named carries only NamedRankKey. A mismatch is a
            // caller-contract violation, not a normal user-facing rejection.
            switch (rankMode)
            {
                case RankMode.None:
                    if (numericRank.HasValue) throw new ArgumentException("NumericRank must be null when RankMode is None.", nameof(numericRank));
                    if (namedRankKey != null) throw new ArgumentException("NamedRankKey must be null when RankMode is None.", nameof(namedRankKey));
                    break;
                case RankMode.Numeric:
                    if (!numericRank.HasValue) throw new ArgumentException("NumericRank is required when RankMode is Numeric.", nameof(numericRank));
                    if (namedRankKey != null) throw new ArgumentException("NamedRankKey must be null when RankMode is Numeric.", nameof(namedRankKey));
                    break;
                case RankMode.Named:
                    if (numericRank.HasValue) throw new ArgumentException("NumericRank must be null when RankMode is Named.", nameof(numericRank));
                    if (string.IsNullOrWhiteSpace(namedRankKey)) throw new ArgumentException("NamedRankKey is required when RankMode is Named.", nameof(namedRankKey));
                    break;
            }

            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));

            CharacterAbilityId = characterAbilityId;
            AbilityDefinitionId = abilityDefinitionId;
            SourceKind = sourceKind;
            SourceRef = sourceRef;
            AcquiredAt = acquiredAt;
            RankMode = rankMode;
            NumericRank = numericRank;
            NamedRankKey = namedRankKey;
            IsEnabled = isEnabled;
            Configuration = configuration;
            UsesState = usesState;
            Revision = revision;
        }

        public CharacterAbilityId CharacterAbilityId { get; }
        public AbilityDefinitionId AbilityDefinitionId { get; }
        public SourceKind SourceKind { get; }

        /// <summary>Provenance only -- e.g. the item/effect instance id this ability came from. Null for <see cref="Character.SourceKind.ProgressionPurchase"/>/<see cref="Character.SourceKind.GMGrant"/>.</summary>
        public string? SourceRef { get; }
        public UtcInstant AcquiredAt { get; }
        public RankMode RankMode { get; }
        public long? NumericRank { get; }
        public string? NamedRankKey { get; }
        public bool IsEnabled { get; }
        public string Configuration { get; }

        /// <summary>Opaque per-use runtime state (e.g. charges remaining) -- no real uses-tracking mechanism exists yet; carried for forward compatibility.</summary>
        public string? UsesState { get; }

        /// <summary>ADR-022 section 6's entry-level revision for the <c>CharacterAbility:&lt;CharacterAbilityId&gt;</c> lock key -- independent of the section-wide <c>CharacterAbilitiesRevision</c>.</summary>
        public long Revision { get; }
    }
}
