using System;
using System.Collections.Generic;
using Odyssey.Domain.Character;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// ODY-S04-112: ADR-026 section 3.2's <c>ExportActorContext</c> --
    /// carries only the acting <see cref="ActorUserId"/> and whether that
    /// actor is MainGM. The actor's ownership/control relationship to the
    /// exported Character is derived from the Character's own already-loaded
    /// <see cref="CharacterOwnership"/> by <c>RedactCharacterForExport</c>
    /// itself, rather than duplicated as a second input here.
    /// </summary>
    public sealed class ExportActorContext
    {
        public ExportActorContext(UserId actorUserId, bool actorIsMainGm)
        {
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));

            ActorUserId = actorUserId;
            ActorIsMainGm = actorIsMainGm;
        }

        public UserId ActorUserId { get; }
        public bool ActorIsMainGm { get; }
    }

    /// <summary>
    /// ODY-S04-112: ADR-026 section 4's <c>manifest.json</c> -- the four
    /// minimum fields plus one additive field, <see cref="SourceRulesetId"/>
    /// (the exporting campaign's own <c>RulesetId</c>), needed alongside
    /// <see cref="SourceRulesetVersion"/> for <c>ImportCharacter</c>'s own
    /// Ruleset-compatibility check (section 4 of this task's own ExecPlan).
    /// An additive field is a patch bump per ADR-026 section 4's own rule --
    /// <see cref="FormatVersion"/> stays "1.0".
    /// </summary>
    public sealed class CharacterExportManifest
    {
        public CharacterExportManifest(string formatVersion, UtcInstant exportedAt, string exportedByRole, string sourceRulesetId, string sourceRulesetVersion)
        {
            if (string.IsNullOrWhiteSpace(formatVersion)) throw new ArgumentException("FormatVersion is required.", nameof(formatVersion));
            if (string.IsNullOrWhiteSpace(exportedByRole)) throw new ArgumentException("ExportedByRole is required.", nameof(exportedByRole));
            if (string.IsNullOrWhiteSpace(sourceRulesetId)) throw new ArgumentException("SourceRulesetId is required.", nameof(sourceRulesetId));
            if (string.IsNullOrWhiteSpace(sourceRulesetVersion)) throw new ArgumentException("SourceRulesetVersion is required.", nameof(sourceRulesetVersion));

            FormatVersion = formatVersion;
            ExportedAt = exportedAt;
            ExportedByRole = exportedByRole;
            SourceRulesetId = sourceRulesetId;
            SourceRulesetVersion = sourceRulesetVersion;
        }

        public string FormatVersion { get; }
        public UtcInstant ExportedAt { get; }
        public string ExportedByRole { get; }
        public string SourceRulesetId { get; }
        public string SourceRulesetVersion { get; }
    }

    /// <summary>ODY-S04-112: one exported attribute -- <see cref="AttributeValue"/> has no instance id of its own, keyed purely by <see cref="AttributeDefinitionId"/>.</summary>
    public sealed class ExportedAttributeValue
    {
        public ExportedAttributeValue(AttributeDefinitionId attributeDefinitionId, long baseValue, long permanentAdjustment, long spentDevelopmentPoints)
        {
            AttributeDefinitionId = attributeDefinitionId;
            BaseValue = baseValue;
            PermanentAdjustment = permanentAdjustment;
            SpentDevelopmentPoints = spentDevelopmentPoints;
        }

        public AttributeDefinitionId AttributeDefinitionId { get; }
        public long BaseValue { get; }
        public long PermanentAdjustment { get; }
        public long SpentDevelopmentPoints { get; }
    }

    /// <summary>ODY-S04-112: one exported skill -- <see cref="CharacterSkill"/> has no instance id of its own, keyed purely by <see cref="SkillDefinitionId"/>.</summary>
    public sealed class ExportedSkill
    {
        public ExportedSkill(SkillDefinitionId skillDefinitionId, long level, long permanentAdjustment, long spentDevelopmentPoints)
        {
            SkillDefinitionId = skillDefinitionId;
            Level = level;
            PermanentAdjustment = permanentAdjustment;
            SpentDevelopmentPoints = spentDevelopmentPoints;
        }

        public SkillDefinitionId SkillDefinitionId { get; }
        public long Level { get; }
        public long PermanentAdjustment { get; }
        public long SpentDevelopmentPoints { get; }
    }

    /// <summary>
    /// ODY-S04-112: one exported ability. <see cref="SourceCharacterAbilityId"/>
    /// is immutable, point-in-time provenance only -- import always mints a
    /// fresh <see cref="Character.CharacterAbilityId"/>, never reusing this
    /// value, mirroring ADR-023 section 5.3's own fresh-identifier rule for
    /// template-copied nested instances (CAP-INV-006's same spirit).
    /// </summary>
    public sealed class ExportedAbility
    {
        public ExportedAbility(CharacterAbilityId sourceCharacterAbilityId, AbilityDefinitionId abilityDefinitionId, SourceKind sourceKind, string? sourceRef, RankMode rankMode, long? numericRank, string? namedRankKey, bool isEnabled, string configuration, string? usesState)
        {
            SourceCharacterAbilityId = sourceCharacterAbilityId;
            AbilityDefinitionId = abilityDefinitionId;
            SourceKind = sourceKind;
            SourceRef = sourceRef;
            RankMode = rankMode;
            NumericRank = numericRank;
            NamedRankKey = namedRankKey;
            IsEnabled = isEnabled;
            Configuration = configuration;
            UsesState = usesState;
        }

        public CharacterAbilityId SourceCharacterAbilityId { get; }
        public AbilityDefinitionId AbilityDefinitionId { get; }
        public SourceKind SourceKind { get; }
        public string? SourceRef { get; }
        public RankMode RankMode { get; }
        public long? NumericRank { get; }
        public string? NamedRankKey { get; }
        public bool IsEnabled { get; }
        public string Configuration { get; }
        public string? UsesState { get; }
    }

    /// <summary>
    /// ODY-S04-112: one exported resource. <see cref="SourceCharacterResourceId"/>
    /// is provenance only -- import always mints a fresh
    /// <see cref="Character.CharacterResourceId"/>, same rationale as
    /// <see cref="ExportedAbility.SourceCharacterAbilityId"/>.
    /// </summary>
    public sealed class ExportedResource
    {
        public ExportedResource(CharacterResourceId sourceCharacterResourceId, ResourceDefinitionId resourceDefinitionId, long currentValue, long baseMaximum, long permanentMaximumAdjustment, long minimumValue, RecoveryRule recoveryRule)
        {
            SourceCharacterResourceId = sourceCharacterResourceId;
            ResourceDefinitionId = resourceDefinitionId;
            CurrentValue = currentValue;
            BaseMaximum = baseMaximum;
            PermanentMaximumAdjustment = permanentMaximumAdjustment;
            MinimumValue = minimumValue;
            RecoveryRule = recoveryRule;
        }

        public CharacterResourceId SourceCharacterResourceId { get; }
        public ResourceDefinitionId ResourceDefinitionId { get; }
        public long CurrentValue { get; }
        public long BaseMaximum { get; }
        public long PermanentMaximumAdjustment { get; }
        public long MinimumValue { get; }
        public RecoveryRule RecoveryRule { get; }
    }

    /// <summary>ODY-S04-112: one exported body part. <see cref="BodyPartId"/> is a catalog-style human-readable key (not a minted per-instance id) -- kept as-is on import, exactly like <c>ReplaceAnatomyProfile</c>'s own existing whole-list-replace convention.</summary>
    public sealed class ExportedBodyPart
    {
        public ExportedBodyPart(BodyPartId bodyPartId, string name, long damageLimit, BodyPartId? attachedToBodyPartId, string properties)
        {
            BodyPartId = bodyPartId;
            Name = name;
            DamageLimit = damageLimit;
            AttachedToBodyPartId = attachedToBodyPartId;
            Properties = properties;
        }

        public BodyPartId BodyPartId { get; }
        public string Name { get; }
        public long DamageLimit { get; }
        public BodyPartId? AttachedToBodyPartId { get; }
        public string Properties { get; }
    }

    /// <summary>ODY-S04-112: one exported permanent modification. <see cref="SourcePermanentModificationId"/> is provenance only -- import mints a fresh id, same rationale as <see cref="ExportedAbility.SourceCharacterAbilityId"/>.</summary>
    public sealed class ExportedPermanentModification
    {
        public ExportedPermanentModification(PermanentModificationId sourcePermanentModificationId, BodyPartId attachedToBodyPartId, string kind, string description, UtcInstant appliedAt)
        {
            SourcePermanentModificationId = sourcePermanentModificationId;
            AttachedToBodyPartId = attachedToBodyPartId;
            Kind = kind;
            Description = description;
            AppliedAt = appliedAt;
        }

        public PermanentModificationId SourcePermanentModificationId { get; }
        public BodyPartId AttachedToBodyPartId { get; }
        public string Kind { get; }
        public string Description { get; }
        public UtcInstant AppliedAt { get; }
    }

    /// <summary>ODY-S04-112: the exported anatomy snapshot, or null if the source Character never initialized <see cref="Character.CharacterAnatomy"/>.</summary>
    public sealed class ExportedAnatomy
    {
        public ExportedAnatomy(AnatomyProfileDefinitionId anatomyProfileDefinitionId, string anatomyProfileVersion, IReadOnlyList<ExportedBodyPart> bodyParts, IReadOnlyList<ExportedPermanentModification> permanentModifications)
        {
            AnatomyProfileDefinitionId = anatomyProfileDefinitionId;
            AnatomyProfileVersion = anatomyProfileVersion;
            BodyParts = bodyParts ?? throw new ArgumentNullException(nameof(bodyParts));
            PermanentModifications = permanentModifications ?? throw new ArgumentNullException(nameof(permanentModifications));
        }

        public AnatomyProfileDefinitionId AnatomyProfileDefinitionId { get; }
        public string AnatomyProfileVersion { get; }
        public IReadOnlyList<ExportedBodyPart> BodyParts { get; }
        public IReadOnlyList<ExportedPermanentModification> PermanentModifications { get; }
    }

    /// <summary>
    /// ODY-S04-112: ADR-026 section 4's <c>character.json</c> -- the
    /// redacted projection of <see cref="Character.CharacterRecord"/>
    /// produced by <c>RedactCharacterForExport</c>. Never carries
    /// <c>CharacterOwnership</c>, <c>CharacterId</c>, or <c>CampaignId</c>
    /// (ADR-026 section 4/section 8 rule 2) -- import assigns all three
    /// fresh via <c>BindDraftToCampaign</c>.
    /// </summary>
    public sealed class CharacterExportPayload
    {
        public CharacterExportPayload(
            CharacterKind characterKind,
            string displayName,
            string? portraitReference,
            string anatomyProfileRef,
            string rulesetVersion,
            long developmentPoolEarned,
            long developmentPoolSpent,
            IReadOnlyList<ExportedAttributeValue> attributes,
            IReadOnlyList<ExportedSkill> skills,
            IReadOnlyList<ExportedAbility> abilities,
            IReadOnlyList<ExportedResource> resources,
            ExportedAnatomy? anatomy)
        {
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("DisplayName is required.", nameof(displayName));
            if (string.IsNullOrWhiteSpace(anatomyProfileRef)) throw new ArgumentException("AnatomyProfileRef is required.", nameof(anatomyProfileRef));
            if (rulesetVersion == null) throw new ArgumentNullException(nameof(rulesetVersion));

            CharacterKind = characterKind;
            DisplayName = displayName;
            PortraitReference = portraitReference;
            AnatomyProfileRef = anatomyProfileRef;
            RulesetVersion = rulesetVersion;
            DevelopmentPoolEarned = developmentPoolEarned;
            DevelopmentPoolSpent = developmentPoolSpent;
            Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            Skills = skills ?? throw new ArgumentNullException(nameof(skills));
            Abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
            Resources = resources ?? throw new ArgumentNullException(nameof(resources));
            Anatomy = anatomy;
        }

        public CharacterKind CharacterKind { get; }
        public string DisplayName { get; }
        public string? PortraitReference { get; }
        public string AnatomyProfileRef { get; }

        /// <summary>The exported Character's own pinned RulesetVersion at export time -- informational provenance only; import never re-pins from this (ADR-025 section 7.6's own "never blindly carried over" rule).</summary>
        public string RulesetVersion { get; }

        /// <summary>Never <c>Reserved</c> -- see this task's own ExecPlan Decisions: Reserved has no meaning without its corresponding, non-exported pending AdvancementRecommendation rows.</summary>
        public long DevelopmentPoolEarned { get; }
        public long DevelopmentPoolSpent { get; }
        public IReadOnlyList<ExportedAttributeValue> Attributes { get; }
        public IReadOnlyList<ExportedSkill> Skills { get; }
        public IReadOnlyList<ExportedAbility> Abilities { get; }
        public IReadOnlyList<ExportedResource> Resources { get; }
        public ExportedAnatomy? Anatomy { get; }
    }

    /// <summary>ODY-S04-112: the in-memory result of a successful <c>ExportCharacter</c> call -- the same manifest/payload pair written to the bundle's own two JSON files, returned directly so a caller/test need not re-parse them.</summary>
    public sealed class CharacterExportBundle
    {
        public CharacterExportBundle(CharacterExportManifest manifest, CharacterExportPayload payload)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public CharacterExportManifest Manifest { get; }
        public CharacterExportPayload Payload { get; }
    }

    /// <summary>
    /// ODY-S04-112: ADR-025 section 7.6's own inputs to <c>ImportCharacter</c>.
    /// <see cref="CharacterKind"/>/<see cref="DisplayName"/>/<see cref="AnatomyProfileRef"/>
    /// are read from the imported bundle itself, not re-specified here --
    /// only <see cref="InitialPrimaryOwnerUserId"/> is caller-supplied, since
    /// ownership never crosses the file (ADR-026 section 4).
    /// </summary>
    public sealed class ImportCharacterRequest
    {
        public ImportCharacterRequest(CampaignHandle targetCampaign, string bundleDirectoryPath, UserId? initialPrimaryOwnerUserId)
        {
            TargetCampaign = targetCampaign ?? throw new ArgumentNullException(nameof(targetCampaign));
            if (string.IsNullOrWhiteSpace(bundleDirectoryPath)) throw new ArgumentException("BundleDirectoryPath is required.", nameof(bundleDirectoryPath));

            BundleDirectoryPath = bundleDirectoryPath;
            InitialPrimaryOwnerUserId = initialPrimaryOwnerUserId;
        }

        public CampaignHandle TargetCampaign { get; }
        public string BundleDirectoryPath { get; }
        public UserId? InitialPrimaryOwnerUserId { get; }
    }
}
