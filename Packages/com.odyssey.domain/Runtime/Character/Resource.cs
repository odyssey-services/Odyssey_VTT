using System;
using System.Text.RegularExpressions;
using Odyssey.Domain.Identity;

namespace Odyssey.Domain.Character
{
    /// <summary>
    /// ODY-S04-109: product section 17's <c>CharacterResource.ResourceDefinitionId</c>
    /// -- a stable, Ruleset/Content-Block-System-published catalog key,
    /// exactly mirroring <see cref="AttributeDefinitionId"/>/<see cref="SkillDefinitionId"/>/
    /// <see cref="AbilityDefinitionId"/>'s own reasoning for why this is not
    /// a canonical <c>Prefix + Uuid7.NewHex32</c> random instance identifier.
    /// </summary>
    public readonly struct ResourceDefinitionId : IEquatable<ResourceDefinitionId>
    {
        private static readonly Regex ValidPattern = new Regex("^[A-Za-z][A-Za-z0-9_]{0,63}$", RegexOptions.Compiled);
        private readonly string _value;

        private ResourceDefinitionId(string value) => _value = value;
        public bool IsValid => _value != null;

        public static bool TryParse(string? value, out ResourceDefinitionId id)
        {
            if (value != null && ValidPattern.IsMatch(value))
            {
                id = new ResourceDefinitionId(value);
                return true;
            }

            id = default;
            return false;
        }

        public static ResourceDefinitionId Parse(string value) => TryParse(value, out ResourceDefinitionId id) ? id : throw new FormatException("ResourceDefinitionId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(ResourceDefinitionId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is ResourceDefinitionId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(ResourceDefinitionId left, ResourceDefinitionId right) => left.Equals(right);
        public static bool operator !=(ResourceDefinitionId left, ResourceDefinitionId right) => !left.Equals(right);
    }

    /// <summary>ODY-S04-109: product section 17.2's <c>CharacterResource.RecoveryRule</c> -- reused verbatim (six values). Product section 17.2's own text: "Фактическое восстановление всегда проходит авторитетной командой" (requirement 46) -- every value here, including <see cref="None"/>, behaves identically operationally in this task's own scope: <c>CurrentValue</c> only ever changes via an explicit command, never a timer/scene/session-change subscription (those triggers are out of scope; a future task would wire them to call the same explicit command this task provides).</summary>
    public enum RecoveryRule
    {
        None = 1,
        Manual = 2,
        OnRest = 3,
        OnSceneChange = 4,
        OnSessionStart = 5,
        TriggeredByRule = 6
    }

    /// <summary>
    /// ODY-S04-109: product section 17's <c>CharacterResource</c>. Product
    /// section 17.1/requirement 44's own invariant --
    /// "если новый EffectiveMaximum ниже CurrentValue, CurrentValue
    /// немедленно ограничивается" -- is enforced structurally: this
    /// constructor rejects any attempt to build an instance whose
    /// <see cref="CurrentValue"/> falls outside
    /// <c>[MinimumValue, EffectiveMaximum]</c>, so the persistence layer's
    /// own command logic must clamp <see cref="CurrentValue"/> itself
    /// before constructing an updated instance -- the domain type makes the
    /// invalid state unconstructible rather than merely documenting the
    /// rule. <see cref="EffectiveMaximum"/> is computed only, mirroring
    /// <see cref="AttributeValue.EffectiveValue"/>'s own doc comment --
    /// never stored or settable directly.
    /// <c>TemporaryMaximumModifierRefs</c> (product's own schema field) is
    /// not implemented -- no active-effect mechanism exists yet, the same
    /// reasoning <see cref="AttributeValue"/>/<see cref="CharacterSkill"/>
    /// already used for their own omitted temporary-modifier fields.
    /// </summary>
    public sealed class CharacterResource
    {
        public CharacterResource(
            CharacterResourceId characterResourceId,
            ResourceDefinitionId resourceDefinitionId,
            long currentValue,
            long baseMaximum,
            long permanentMaximumAdjustment,
            long minimumValue,
            RecoveryRule recoveryRule,
            long revision)
        {
            if (!characterResourceId.IsValid) throw new ArgumentException("CharacterResourceId is required.", nameof(characterResourceId));
            if (!resourceDefinitionId.IsValid) throw new ArgumentException("ResourceDefinitionId is required.", nameof(resourceDefinitionId));
            if (!Enum.IsDefined(typeof(RecoveryRule), recoveryRule)) throw new ArgumentOutOfRangeException(nameof(recoveryRule));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));

            long effectiveMaximum = baseMaximum + permanentMaximumAdjustment;
            if (effectiveMaximum < minimumValue) throw new ArgumentException("EffectiveMaximum must be >= MinimumValue.", nameof(baseMaximum));
            if (currentValue < minimumValue || currentValue > effectiveMaximum)
            {
                throw new ArgumentOutOfRangeException(nameof(currentValue), "CurrentValue must be within [MinimumValue, EffectiveMaximum].");
            }

            CharacterResourceId = characterResourceId;
            ResourceDefinitionId = resourceDefinitionId;
            CurrentValue = currentValue;
            BaseMaximum = baseMaximum;
            PermanentMaximumAdjustment = permanentMaximumAdjustment;
            MinimumValue = minimumValue;
            RecoveryRule = recoveryRule;
            Revision = revision;
        }

        public CharacterResourceId CharacterResourceId { get; }
        public ResourceDefinitionId ResourceDefinitionId { get; }
        public long CurrentValue { get; }
        public long BaseMaximum { get; }
        public long PermanentMaximumAdjustment { get; }

        /// <summary>Computed only -- see this class's own doc comment. Never persisted as an independently editable field.</summary>
        public long EffectiveMaximum => BaseMaximum + PermanentMaximumAdjustment;
        public long MinimumValue { get; }
        public RecoveryRule RecoveryRule { get; }

        /// <summary>ADR-022 section 6's entry-level revision for the <c>CharacterResource:&lt;CharacterResourceId&gt;</c> lock key -- independent of the section-wide <c>CharacterResourcesRevision</c> (this task's own decision: not externally gated by callers, mirroring <c>CharacterAbility.Revision</c>'s own carried-but-not-externally-checked convention -- see this task's ExecPlan section 7 for the full justification).</summary>
        public long Revision { get; }
    }
}
