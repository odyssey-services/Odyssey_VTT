using System;
using System.Text.RegularExpressions;

namespace Odyssey.Domain.Character
{
    /// <summary>
    /// ODY-S04-106: product section 14/15's <c>CharacterSkill.SkillDefinitionId</c>
    /// -- a stable, Ruleset/campaign-authored catalog key, exactly mirroring
    /// <see cref="AttributeDefinitionId"/>'s own reasoning for why this is not
    /// a canonical <c>Prefix + Uuid7.NewHex32</c> random instance identifier.
    /// </summary>
    public readonly struct SkillDefinitionId : IEquatable<SkillDefinitionId>
    {
        private static readonly Regex ValidPattern = new Regex("^[A-Za-z][A-Za-z0-9_]{0,63}$", RegexOptions.Compiled);
        private readonly string _value;

        private SkillDefinitionId(string value) => _value = value;
        public bool IsValid => _value != null;

        public static bool TryParse(string? value, out SkillDefinitionId id)
        {
            if (value != null && ValidPattern.IsMatch(value))
            {
                id = new SkillDefinitionId(value);
                return true;
            }

            id = default;
            return false;
        }

        public static SkillDefinitionId Parse(string value) => TryParse(value, out SkillDefinitionId id) ? id : throw new FormatException("SkillDefinitionId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(SkillDefinitionId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is SkillDefinitionId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(SkillDefinitionId left, SkillDefinitionId right) => left.Equals(right);
        public static bool operator !=(SkillDefinitionId left, SkillDefinitionId right) => !left.Equals(right);
    }

    /// <summary>
    /// ODY-S04-106: product section 14's <c>CharacterSkill</c>, narrowed to
    /// this task's own scope -- "отсутствующий навык представлен
    /// отсутствием CharacterSkill" (no row for an unpossessed skill),
    /// enforced by callers never persisting one until the first purchase.
    /// <c>TemporaryModifierRefs</c>/<c>CriticalSuccessEvidenceRefs</c>/
    /// <c>AdvancementState</c> are not implemented -- no effect mechanism
    /// exists yet, and evidence/recommendation linkage is tracked on the
    /// Application-layer <c>CriticalSuccessEvidenceRecord</c>/
    /// <c>AdvancementRecommendationRecord</c> rows themselves rather than
    /// duplicated back onto this Domain-owned row.
    /// </summary>
    public sealed class CharacterSkill
    {
        public CharacterSkill(SkillDefinitionId skillDefinitionId, long level, long permanentAdjustment, long spentDevelopmentPoints, long revision)
        {
            if (!skillDefinitionId.IsValid) throw new ArgumentException("SkillDefinitionId is required.", nameof(skillDefinitionId));
            if (level < 0) throw new ArgumentOutOfRangeException(nameof(level));
            if (spentDevelopmentPoints < 0) throw new ArgumentOutOfRangeException(nameof(spentDevelopmentPoints));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));

            SkillDefinitionId = skillDefinitionId;
            Level = level;
            PermanentAdjustment = permanentAdjustment;
            SpentDevelopmentPoints = spentDevelopmentPoints;
            Revision = revision;
        }

        public SkillDefinitionId SkillDefinitionId { get; }
        public long Level { get; }
        public long PermanentAdjustment { get; }

        /// <summary>Computed only, mirroring <see cref="AttributeValue.EffectiveValue"/>'s own doc comment -- never stored or settable directly.</summary>
        public long EffectiveLevel => Level + PermanentAdjustment;
        public long SpentDevelopmentPoints { get; }

        /// <summary>ADR-024 section 4.2's entry-level revision for the <c>CharacterSkill:&lt;SkillDefinitionId&gt;</c> lock key -- independent of the aggregate-wide <c>MechanicsRevision</c>.</summary>
        public long Revision { get; }
    }

    /// <summary>ODY-S04-106: product section 14.3's minimal recommendation lifecycle -- MainGM either approves or dismisses; no other terminal states exist.</summary>
    public enum AdvancementRecommendationStatus
    {
        Pending = 1,
        Approved = 2,
        Dismissed = 3
    }
}
