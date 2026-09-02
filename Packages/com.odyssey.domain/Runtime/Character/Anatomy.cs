using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Domain.Character
{
    /// <summary>
    /// ODY-S04-109: product section 18's <c>AnatomyProfileDefinition</c> id
    /// -- a stable, Ruleset/Content-Block-System-published catalog key,
    /// mirroring <see cref="AttributeDefinitionId"/>'s own reasoning.
    /// </summary>
    public readonly struct AnatomyProfileDefinitionId : IEquatable<AnatomyProfileDefinitionId>
    {
        private static readonly Regex ValidPattern = new Regex("^[A-Za-z][A-Za-z0-9_.]{0,63}$", RegexOptions.Compiled);
        private readonly string _value;

        private AnatomyProfileDefinitionId(string value) => _value = value;
        public bool IsValid => _value != null;

        public static bool TryParse(string? value, out AnatomyProfileDefinitionId id)
        {
            if (value != null && ValidPattern.IsMatch(value))
            {
                id = new AnatomyProfileDefinitionId(value);
                return true;
            }

            id = default;
            return false;
        }

        public static AnatomyProfileDefinitionId Parse(string value) => TryParse(value, out AnatomyProfileDefinitionId id) ? id : throw new FormatException("AnatomyProfileDefinitionId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(AnatomyProfileDefinitionId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is AnatomyProfileDefinitionId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(AnatomyProfileDefinitionId left, AnatomyProfileDefinitionId right) => left.Equals(right);
        public static bool operator !=(AnatomyProfileDefinitionId left, AnatomyProfileDefinitionId right) => !left.Equals(right);
    }

    /// <summary>
    /// ODY-S04-109: identifies one named body-part slot within a single
    /// <c>CharacterAnatomy</c> snapshot (product section 18's <c>BodyParts</c>).
    /// A body part is a stable structural slot ("Head", "LeftArm"), not a
    /// randomly-created purchase instance -- this is a validated string
    /// identifier, mirroring <see cref="AttributeDefinitionId"/>'s own
    /// catalog-key shape rather than <see cref="CharacterResourceId"/>'s
    /// canonical-instance shape. Unique only within the one
    /// <c>CharacterAnatomy</c> it belongs to (not globally, unlike a real
    /// catalog key) -- enforced by the persistence layer, not this type.
    /// </summary>
    public readonly struct BodyPartId : IEquatable<BodyPartId>
    {
        private static readonly Regex ValidPattern = new Regex("^[A-Za-z][A-Za-z0-9_]{0,63}$", RegexOptions.Compiled);
        private readonly string _value;

        private BodyPartId(string value) => _value = value;
        public bool IsValid => _value != null;

        public static bool TryParse(string? value, out BodyPartId id)
        {
            if (value != null && ValidPattern.IsMatch(value))
            {
                id = new BodyPartId(value);
                return true;
            }

            id = default;
            return false;
        }

        public static BodyPartId Parse(string value) => TryParse(value, out BodyPartId id) ? id : throw new FormatException("BodyPartId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(BodyPartId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is BodyPartId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(BodyPartId left, BodyPartId right) => left.Equals(right);
        public static bool operator !=(BodyPartId left, BodyPartId right) => !left.Equals(right);
    }

    /// <summary>
    /// ODY-S04-109: one entry of product section 18's <c>CharacterAnatomy.BodyParts</c>.
    /// <see cref="AttachedToBodyPartId"/> is this task's own minimal, real,
    /// internally-checkable dependency model (section 1.3 of this task's own
    /// ТЗ): a part attached to another part (e.g. a hand attached to a
    /// forearm) cannot be removed while its parent still exists without an
    /// explicit decision -- see <c>RemoveBodyPart</c>'s own doc comment for
    /// the full dependency-preview boundary. <c>Properties</c> is a minimal
    /// opaque string -- no real body-part property schema exists anywhere in
    /// this codebase (confirmed by search); this task's own explicitly-flagged
    /// test fixture, mirroring <see cref="CharacterAbility.Configuration"/>'s
    /// own "{}" placeholder convention.
    /// </summary>
    public sealed class BodyPart
    {
        public BodyPart(BodyPartId bodyPartId, string name, long damageLimit, BodyPartId? attachedToBodyPartId, string properties)
        {
            if (!bodyPartId.IsValid) throw new ArgumentException("BodyPartId is required.", nameof(bodyPartId));
            if (string.IsNullOrWhiteSpace(name) || name.Length > 128) throw new ArgumentException("Name is not safe.", nameof(name));
            if (damageLimit < 0) throw new ArgumentOutOfRangeException(nameof(damageLimit));
            if (attachedToBodyPartId.HasValue && attachedToBodyPartId.Value.Equals(bodyPartId)) throw new ArgumentException("A body part cannot be attached to itself.", nameof(attachedToBodyPartId));
            if (properties == null) throw new ArgumentNullException(nameof(properties));

            BodyPartId = bodyPartId;
            Name = name;
            DamageLimit = damageLimit;
            AttachedToBodyPartId = attachedToBodyPartId;
            Properties = properties;
        }

        public BodyPartId BodyPartId { get; }
        public string Name { get; }

        /// <summary>Product section 18: "изменить пределы повреждений" -- the maximum damage this part can sustain before it is destroyed/severed. No damage-tracking/current-damage mechanism exists yet (no combat/anatomy-damage system) -- this is the limit value only, per this task's own scope.</summary>
        public long DamageLimit { get; }

        /// <summary>This task's own minimal internal dependency model -- see this class's own doc comment.</summary>
        public BodyPartId? AttachedToBodyPartId { get; }
        public string Properties { get; }
    }

    /// <summary>
    /// ODY-S04-109: product section 18's "применить протез, мутацию или
    /// постоянную модификацию" -- one generic record for all three, since
    /// product itself groups them together with no separate schema per kind.
    /// <see cref="AttachedToBodyPartId"/> is this task's own real,
    /// internally-checkable dependency target for <c>RemoveBodyPart</c>'s
    /// dependency preview (section 1.3).
    /// </summary>
    public sealed class PermanentModification
    {
        public PermanentModification(PermanentModificationId permanentModificationId, BodyPartId attachedToBodyPartId, string kind, string description, UtcInstant appliedAt)
        {
            if (!permanentModificationId.IsValid) throw new ArgumentException("PermanentModificationId is required.", nameof(permanentModificationId));
            if (!attachedToBodyPartId.IsValid) throw new ArgumentException("AttachedToBodyPartId is required.", nameof(attachedToBodyPartId));
            if (string.IsNullOrWhiteSpace(kind) || kind.Length > 64) throw new ArgumentException("Kind is not safe.", nameof(kind));
            if (string.IsNullOrWhiteSpace(description) || description.Length > 512) throw new ArgumentException("Description is not safe.", nameof(description));

            PermanentModificationId = permanentModificationId;
            AttachedToBodyPartId = attachedToBodyPartId;
            Kind = kind;
            Description = description;
            AppliedAt = appliedAt;
        }

        public PermanentModificationId PermanentModificationId { get; }
        public BodyPartId AttachedToBodyPartId { get; }

        /// <summary>E.g. "Prosthetic"/"Mutation"/"PermanentModification" -- product section 18's own three named cases, an opaque string since no typed catalog of modification kinds exists.</summary>
        public string Kind { get; }
        public string Description { get; }
        public UtcInstant AppliedAt { get; }
    }

    /// <summary>
    /// ODY-S04-109: one append-only journal entry of product section 18's
    /// <c>CharacterAnatomy.MigrationHistory</c> -- "индивидуальные изменения
    /// анатомии журналируются" (requirement 38/product section 18's own
    /// text). Appended by every anatomy-mutating command in the same
    /// transaction as the command's own effect and <c>DomainEvent</c> --
    /// never a separately-committed side channel.
    /// </summary>
    public sealed class AnatomyMigrationEntry
    {
        public AnatomyMigrationEntry(string actionKind, string description, UtcInstant occurredAt)
        {
            if (string.IsNullOrWhiteSpace(actionKind) || actionKind.Length > 64) throw new ArgumentException("ActionKind is not safe.", nameof(actionKind));
            if (string.IsNullOrWhiteSpace(description) || description.Length > 512) throw new ArgumentException("Description is not safe.", nameof(description));

            ActionKind = actionKind;
            Description = description;
            OccurredAt = occurredAt;
        }

        public string ActionKind { get; }
        public string Description { get; }
        public UtcInstant OccurredAt { get; }
    }

    /// <summary>
    /// ODY-S04-109 (section 1.2 of this task's own ТЗ): product section 18's
    /// <c>CharacterAnatomy</c> -- a SINGLE snapshot per Character, not a
    /// collection of independently-revisioned entries the way
    /// <c>CharacterSkill</c>/<c>CharacterAbility</c>/<c>CharacterResource</c>
    /// are. The entire snapshot changes together under ONE
    /// <c>CharacterAnatomyRevision</c> per command -- the same shape
    /// <c>CharacterOwnership</c>/<c>CharacterLifecycle</c> (ODY-S04-102/104)
    /// already use for their own single-object sections, deliberately NOT
    /// the entry-level-lock-key shape (ADR-022 section 6 reserves exactly
    /// the un-parameterized <c>CharacterAnatomy</c> lock key, not
    /// <c>CharacterAnatomy:&lt;id&gt;</c>, confirming this is the correct
    /// shape rather than an oversight).
    ///
    /// <see cref="AnatomyProfileVersion"/> is captured once at
    /// initialization time and never re-read from the source
    /// <c>AnatomyProfileDefinition</c> fixture afterward -- mirrors
    /// <c>CharacterRecord.TemplateVersionAtCopyTime</c>'s own
    /// deep-copy-with-a-pinned-version convention from ODY-S04-103
    /// (product requirement 49: "Изменение profile definition не меняет
    /// Character без migration").
    /// </summary>
    public sealed class CharacterAnatomy
    {
        public CharacterAnatomy(
            AnatomyProfileDefinitionId anatomyProfileDefinitionId,
            string anatomyProfileVersion,
            IReadOnlyList<BodyPart> bodyParts,
            IReadOnlyList<PermanentModification> permanentModifications,
            IReadOnlyList<AnatomyMigrationEntry> migrationHistory,
            long revision)
        {
            if (!anatomyProfileDefinitionId.IsValid) throw new ArgumentException("AnatomyProfileDefinitionId is required.", nameof(anatomyProfileDefinitionId));
            if (string.IsNullOrWhiteSpace(anatomyProfileVersion)) throw new ArgumentException("AnatomyProfileVersion is required.", nameof(anatomyProfileVersion));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));

            AnatomyProfileDefinitionId = anatomyProfileDefinitionId;
            AnatomyProfileVersion = anatomyProfileVersion;
            BodyParts = bodyParts ?? throw new ArgumentNullException(nameof(bodyParts));
            PermanentModifications = permanentModifications ?? throw new ArgumentNullException(nameof(permanentModifications));
            MigrationHistory = migrationHistory ?? throw new ArgumentNullException(nameof(migrationHistory));
            Revision = revision;
        }

        public AnatomyProfileDefinitionId AnatomyProfileDefinitionId { get; }

        /// <summary>Pinned at initialization/replace time -- see this class's own doc comment. Never a live reference back to the source fixture/definition.</summary>
        public string AnatomyProfileVersion { get; }
        public IReadOnlyList<BodyPart> BodyParts { get; }
        public IReadOnlyList<PermanentModification> PermanentModifications { get; }
        public IReadOnlyList<AnatomyMigrationEntry> MigrationHistory { get; }

        /// <summary>ADR-022 section 6's single, un-parameterized <c>CharacterAnatomy</c> lock key -- the ONLY revision gating every anatomy command; there is no entry-level revision for individual body parts/modifications (section 1.2 of this task's own ТЗ).</summary>
        public long Revision { get; }
    }
}
