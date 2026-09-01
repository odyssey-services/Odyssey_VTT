using System;
using Odyssey.Application.Commands;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// ODY-S04-103: ADR-023 section 5.2's personal-profile storage boundary --
    /// the same boundary a local Draft lives in. Application-safe handle,
    /// mirroring <see cref="CampaignHandle"/>'s own shape for a different
    /// storage root: identity plus a root path only, never a live connection
    /// (ADR-001 section 6.5).
    /// </summary>
    public sealed class LocalProfileHandle
    {
        public LocalProfileHandle(UserId ownerUserId, string rootPath)
        {
            if (!ownerUserId.IsValid) throw new ArgumentException("OwnerUserId is required.", nameof(ownerUserId));
            if (string.IsNullOrWhiteSpace(rootPath)) throw new ArgumentException("Root path is required.", nameof(rootPath));

            OwnerUserId = ownerUserId;
            RootPath = rootPath;
        }

        public UserId OwnerUserId { get; }
        public string RootPath { get; }
    }

    /// <summary>
    /// ODY-S04-103: routes a <see cref="ICharacterTemplateRepository"/> call to
    /// the correct physical storage root for the template's own
    /// <see cref="TemplateScope"/> -- a <c>Personal</c> template lives in a
    /// <see cref="LocalProfileHandle"/>'s root (ADR-023 section 5.2), a
    /// <c>Campaign</c> template lives inside its owning campaign's own
    /// authoritative storage (the same <c>campaign.db</c> file
    /// <see cref="SceneRepositoryContracts"/>/Character use, as its own
    /// sibling table -- ADR-023 section 5.2's "a sibling of Character, not a
    /// section of it"). This routing value is what keeps
    /// <c>UpdateCharacterTemplate</c>/<c>ArchiveCharacterTemplate</c> single,
    /// scope-agnostic commands (matching this task's own named command list)
    /// while still respecting the two-storage-boundary decision -- it is not
    /// a new architectural concept, only a small Application-layer routing
    /// value.
    /// </summary>
    public sealed class TemplateStorageHandle
    {
        private TemplateStorageHandle(TemplateScope scope, string rootPath, UserId? ownerUserId, CampaignId? campaignId)
        {
            Scope = scope;
            RootPath = rootPath;
            OwnerUserId = ownerUserId;
            CampaignId = campaignId;
        }

        public static TemplateStorageHandle ForPersonal(LocalProfileHandle profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            return new TemplateStorageHandle(TemplateScope.Personal, profile.RootPath, profile.OwnerUserId, null);
        }

        public static TemplateStorageHandle ForCampaign(CampaignHandle campaign)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            return new TemplateStorageHandle(TemplateScope.Campaign, campaign.RootPath, null, campaign.CampaignId);
        }

        public TemplateScope Scope { get; }
        public string RootPath { get; }
        public UserId? OwnerUserId { get; }
        public CampaignId? CampaignId { get; }
    }

    /// <summary>
    /// ODY-S04-103: ADR-023 section 5's <c>CharacterTemplate</c> aggregate
    /// port -- one aggregate type, distinguished by <see cref="TemplateScope"/>,
    /// never two independent aggregate types (ADR-023 section 5.1). Exactly
    /// the four named commands this task's own contract lists, plus a read.
    /// <c>UpdateCharacterTemplate</c>/<c>ArchiveCharacterTemplate</c> are
    /// scope-agnostic -- see <see cref="TemplateStorageHandle"/>'s own doc
    /// comment for how that is reconciled with the two-storage-boundary rule.
    /// </summary>
    public interface ICharacterTemplateRepository
    {
        Result<CharacterTemplateRecord> CreatePersonalCharacterTemplate(LocalProfileHandle profile, string name, CharacterKind characterKind, string rulesetId, string rulesetVersion, string? anatomyProfileRef, CharacterTemplateSeed seed, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// A Campaign-scope template is authored for its own campaign's
        /// already-pinned ruleset (<see cref="CampaignHandle.Manifest"/>) --
        /// unlike a Personal template, the caller does not supply a separate
        /// RulesetId/RulesetVersion because a template scoped to one campaign
        /// is never meant to target a different ruleset than that campaign's
        /// own.
        /// </summary>
        Result<CharacterTemplateRecord> CreateCampaignCharacterTemplate(CampaignHandle campaign, string name, CharacterKind characterKind, string? anatomyProfileRef, CharacterTemplateSeed seed, CommandId commandId, CorrelationId correlationId);

        Result<CharacterTemplateRecord> UpdateCharacterTemplate(TemplateStorageHandle storage, CharacterTemplateId templateId, string name, string? anatomyProfileRef, CharacterTemplateSeed seed, long expectedRevision, CommandId commandId, CorrelationId correlationId);

        Result<CharacterTemplateRecord> ArchiveCharacterTemplate(TemplateStorageHandle storage, CharacterTemplateId templateId, long expectedRevision, CommandId commandId, CorrelationId correlationId);

        Result<CharacterTemplateRecord> GetCharacterTemplate(TemplateStorageHandle storage, CharacterTemplateId templateId, CorrelationId correlationId);
    }

    /// <summary>
    /// ODY-S04-103: the <c>CharacterTemplate</c> aggregate's current-state
    /// projection row (product section 9.1, narrowed to this task's own
    /// scope -- <c>VisibilityAudience</c>/<c>ProgressionConstraints</c>/
    /// <c>RequiredFieldRules</c> are not implemented; nothing in this task
    /// needs them to prove ADR-023's own architectural requirements).
    /// </summary>
    public sealed class CharacterTemplateRecord
    {
        public CharacterTemplateRecord(
            CharacterTemplateId templateId,
            TemplateScope scope,
            UserId? ownerUserId,
            CampaignId? campaignId,
            string name,
            CharacterKind characterKind,
            string rulesetId,
            string rulesetVersion,
            string? anatomyProfileRef,
            CharacterTemplateSeed seed,
            CharacterTemplateStatus status,
            long revision,
            UtcInstant createdAt,
            UtcInstant updatedAt)
        {
            if (!templateId.IsValid) throw new ArgumentException("TemplateId is required.", nameof(templateId));
            if (!Enum.IsDefined(typeof(TemplateScope), scope)) throw new ArgumentOutOfRangeException(nameof(scope));
            if (string.IsNullOrWhiteSpace(name) || name.Length > 128) throw new ArgumentException("Name is not safe.", nameof(name));
            if (!Enum.IsDefined(typeof(CharacterKind), characterKind)) throw new ArgumentOutOfRangeException(nameof(characterKind));
            if (string.IsNullOrWhiteSpace(rulesetId)) throw new ArgumentException("RulesetId is required.", nameof(rulesetId));
            if (string.IsNullOrWhiteSpace(rulesetVersion)) throw new ArgumentException("RulesetVersion is required.", nameof(rulesetVersion));
            if (!Enum.IsDefined(typeof(CharacterTemplateStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));

            TemplateId = templateId;
            Scope = scope;
            OwnerUserId = ownerUserId;
            CampaignId = campaignId;
            Name = name;
            CharacterKind = characterKind;
            RulesetId = rulesetId;
            RulesetVersion = rulesetVersion;
            AnatomyProfileRef = anatomyProfileRef;
            Seed = seed ?? throw new ArgumentNullException(nameof(seed));
            Status = status;
            Revision = revision;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        public CharacterTemplateId TemplateId { get; }
        public TemplateScope Scope { get; }
        public UserId? OwnerUserId { get; }
        public CampaignId? CampaignId { get; }
        public string Name { get; }
        public CharacterKind CharacterKind { get; }
        public string RulesetId { get; }
        public string RulesetVersion { get; }
        public string? AnatomyProfileRef { get; }
        public CharacterTemplateSeed Seed { get; }
        public CharacterTemplateStatus Status { get; }

        /// <summary>
        /// Serves both as product section 9.1's optimistic-concurrency
        /// <c>Revision</c> and as the "template's Version at copy time"
        /// provenance ADR-023 section 5.3 calls <c>TemplateVersion</c> --
        /// product names these as two separate fields without giving them
        /// distinct semantics; this task's own minimal engineering decision
        /// is to use one counter for both rather than inventing an
        /// undifferentiated second one.
        /// </summary>
        public long Revision { get; }
        public UtcInstant CreatedAt { get; }
        public UtcInstant UpdatedAt { get; }
    }
}
