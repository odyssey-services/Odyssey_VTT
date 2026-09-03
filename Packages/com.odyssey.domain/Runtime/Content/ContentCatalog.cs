using System;
using System.Globalization;

namespace Odyssey.Domain.Content
{
    /// <summary>
    /// ODY-S05-101: a real, minted `ContentDefinition` catalog-row identity
    /// (`11_Content_Block_System` section 5.3's <c>DefinitionId</c>), following
    /// the same canonical <c>Prefix + Uuid7.NewHex32</c> aggregate-identity
    /// pattern <see cref="Odyssey.Domain.Identity.CharacterId"/>/
    /// <see cref="Odyssey.Domain.Identity.CharacterTemplateId"/> already
    /// established -- deliberately NOT the lightweight, human-authored
    /// string-key convention `SkillDefinitionId`/`AttributeDefinitionId`/
    /// `AbilityDefinitionId` use (ODY-S04-106/108's own fixture-only Ruleset
    /// keys with no real backing catalog table). This ADR-027 Content Catalog
    /// is a genuine new aggregate root with its own persisted row, lifecycle,
    /// and audit fields, so it mints its own opaque identity the same way
    /// every other real aggregate root in this codebase does.
    /// </summary>
    public readonly struct ContentDefinitionId : IEquatable<ContentDefinitionId>
    {
        private const string Prefix = "cdef_";
        private const int HexLength = 32;
        private readonly string _value;

        private ContentDefinitionId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static ContentDefinitionId NewId(Odyssey.Domain.Time.UtcInstant now) => new ContentDefinitionId(Prefix + Odyssey.Domain.Identity.Uuid7.NewHex32(now));
        public static bool TryParse(string? value, out ContentDefinitionId id) => Odyssey.Domain.Identity.CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new ContentDefinitionId(v));
        public static ContentDefinitionId Parse(string value) => TryParse(value, out ContentDefinitionId id) ? id : throw new FormatException("ContentDefinitionId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(ContentDefinitionId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is ContentDefinitionId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(ContentDefinitionId left, ContentDefinitionId right) => left.Equals(right);
        public static bool operator !=(ContentDefinitionId left, ContentDefinitionId right) => !left.Equals(right);
    }

    /// <summary>
    /// ODY-S05-101: `11_Content_Block_System` section 6.1's lifecycle status,
    /// verbatim. `ADR-027` section 4.1 fixes the archive/physical-delete rules
    /// this status drives; `ODY-S05-103` implements the actual publish/archive
    /// transition commands. This task only establishes the structural values
    /// and stores/reads them -- it does not implement any transition rule.
    /// </summary>
    public enum ContentDefinitionStatus
    {
        Draft = 1,
        Published = 2,
        Archived = 3
    }

    /// <summary>
    /// ODY-S05-101: `11_Content_Block_System` section 5.3's <c>Origin</c>.
    /// `SLICE-05_IMPLEMENTATION_BACKLOG.md` section 3.2's explicit
    /// product-owner MVP scope decision means this task's own repository
    /// surface only ever produces/accepts <see cref="RulesetPackage"/> --
    /// <see cref="Campaign"/> is named here because the product document
    /// already fixes it as part of the shared vocabulary, but no code path
    /// in `SLICE-05`'s Content Catalog MVP block creates or reads a
    /// Campaign-origin definition. Introducing real campaign-specific
    /// catalog/override behavior is explicitly out of scope until a future,
    /// separately-scoped decision.
    /// </summary>
    public enum ContentDefinitionOrigin
    {
        RulesetPackage = 1,
        Campaign = 2
    }

    /// <summary>
    /// ODY-S05-101: `11_Content_Block_System` sections 5.1 (mechanical
    /// definitions) and 5.2 (structural definitions) vocabulary, used here
    /// purely as an identity/discriminator placeholder on the generic
    /// envelope (`SLICE-05_IMPLEMENTATION_BACKLOG.md`'s own `ODY-S05-101`
    /// boundary: "definition type placeholder/discriminator allowed, but do
    /// not implement full typed Weapon/Armor/Ammo/Ability/Effect properties
    /// here"). No typed property shape for any of these values is
    /// implemented by this task -- that is `ODY-S05-105`'s own job.
    /// </summary>
    public enum ContentDefinitionType
    {
        // Mechanical definitions (11_Content_Block_System section 5.1).
        Perk = 1,
        Item = 2,
        Weapon = 3,
        Armor = 4,
        Ammo = 5,
        Ability = 6,
        Effect = 7,
        Action = 8,
        Mechanic = 9,

        // Structural definitions (11_Content_Block_System section 5.2).
        Attribute = 10,
        Skill = 11,
        BodyPart = 12,
        Resource = 13,
        NpcTemplateData = 14
    }

    /// <summary>
    /// ODY-S05-101: `ADR-027` section 4 rule 2's exact-version reference
    /// shape -- "Runtime entities reference exact definition versions for
    /// origin, UI, audit, dependency analysis, and migration." Deliberately
    /// has no `LatestCompatible`/"latest" concept: a reference always pins
    /// one already-published <see cref="Version"/> of one
    /// <see cref="ContentDefinitionId"/>, never a floating pointer to
    /// whatever the catalog's current Published row happens to be. This is
    /// the only mechanism this task implements for `ADR-027`'s own
    /// dependency/reference tracking (section 4 rule 5's `ContentDependency`
    /// records/full missing-reference validation are `ODY-S05-104`'s job;
    /// this type only proves the reference shape itself exists and
    /// round-trips).
    /// </summary>
    public readonly struct ContentDefinitionRef : IEquatable<ContentDefinitionRef>
    {
        public ContentDefinitionRef(ContentDefinitionId definitionId, long version)
        {
            if (!definitionId.IsValid) throw new ArgumentException("DefinitionId is required.", nameof(definitionId));
            if (version < 1) throw new ArgumentOutOfRangeException(nameof(version), "A ContentDefinitionRef always pins an exact, already-published version (>= 1) -- never version 0 (no Published version yet) and never a floating 'latest' sentinel.");

            DefinitionId = definitionId;
            Version = version;
        }

        public ContentDefinitionId DefinitionId { get; }
        public long Version { get; }
        public bool IsValid => DefinitionId.IsValid && Version >= 1;

        /// <summary>Canonical round-trip form, e.g. <c>cdef_0123.../3</c>.</summary>
        public override string ToString() => DefinitionId.ToString() + "/" + Version.ToString(CultureInfo.InvariantCulture);

        public static bool TryParse(string? value, out ContentDefinitionRef reference)
        {
            reference = default;
            if (string.IsNullOrEmpty(value)) return false;

            int separator = value.LastIndexOf('/');
            if (separator <= 0 || separator == value.Length - 1) return false;

            if (!ContentDefinitionId.TryParse(value.Substring(0, separator), out ContentDefinitionId id)) return false;
            if (!long.TryParse(value.Substring(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out long version) || version < 1) return false;

            reference = new ContentDefinitionRef(id, version);
            return true;
        }

        public static ContentDefinitionRef Parse(string value) => TryParse(value, out ContentDefinitionRef reference) ? reference : throw new FormatException("ContentDefinitionRef is not canonical.");

        public bool Equals(ContentDefinitionRef other) => DefinitionId.Equals(other.DefinitionId) && Version == other.Version;
        public override bool Equals(object? obj) => obj is ContentDefinitionRef other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(DefinitionId, Version);
        public static bool operator ==(ContentDefinitionRef left, ContentDefinitionRef right) => left.Equals(right);
        public static bool operator !=(ContentDefinitionRef left, ContentDefinitionRef right) => !left.Equals(right);
    }
}
