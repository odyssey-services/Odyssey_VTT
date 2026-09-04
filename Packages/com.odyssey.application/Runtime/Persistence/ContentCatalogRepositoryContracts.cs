using System;
using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Results;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// ODY-S05-101: Content Catalog Foundation only. Implements storage/read
    /// operations for the generic `ContentDefinition` envelope
    /// (`ADR-027` section 4; `11_Content_Block_System` section 5.3/6) --
    /// nothing else. Deliberately does NOT include:
    /// <list type="bullet">
    /// <item>MainGM authoring business rules, permission checks, or a
    /// "create next Draft version from Published" operation --
    /// <c>ODY-S05-102</c>'s own job. <see cref="UpdateDraftContentDefinition"/>
    /// here is a bare, permission-free repository primitive that exists
    /// only to prove the Revision/optimistic-concurrency mechanism works at
    /// the foundation level; it is not the real authoring command.</item>
    /// <item><c>PublishDefinition</c>/<c>ArchiveDefinition</c>/physical
    /// delete/Archived-list query -- <c>ODY-S05-103</c>'s own job. This
    /// interface has no publish/archive method at all; Published-status
    /// immutability is enforced only as <see cref="UpdateDraftContentDefinition"/>
    /// refusing to touch a non-Draft row.</item>
    /// <item>Any validation beyond basic field-shape checks --
    /// <c>ODY-S05-104</c>'s own job.</item>
    /// <item>Any typed Weapon/Armor/Ammo/Ability/Effect property shape --
    /// <c>ODY-S05-105</c>'s own job. <see cref="ContentDefinitionRecord.PropertiesJson"/>
    /// stays an opaque blob here.</item>
    /// </list>
    /// </summary>
    public interface IContentCatalogRepository
    {
        Result<ContentDefinitionRecord> CreateDraftContentDefinition(CreateDraftContentDefinitionRequest request, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S05-101: a bare foundation-level primitive proving the
        /// Revision/optimistic-concurrency mechanism -- not the real GM
        /// authoring command (`ODY-S05-102`). Refuses to touch a definition
        /// whose <see cref="ContentDefinitionRecord.Status"/> is not
        /// <see cref="ContentDefinitionStatus.Draft"/>, enforcing Published
        /// immutability at the foundation level (`ADR-027` section 4.1) even
        /// though the full publish workflow does not exist yet.
        /// </summary>
        Result<ContentDefinitionRecord> UpdateDraftContentDefinition(CampaignHandle campaign, ContentDefinitionId definitionId, string name, string? description, string propertiesJson, long expectedRevision, CommandId commandId, CorrelationId correlationId);

        Result<ContentDefinitionRecord> GetContentDefinition(CampaignHandle campaign, ContentDefinitionId definitionId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S05-101: <paramref name="statusFilter"/> narrows to one
        /// <see cref="ContentDefinitionStatus"/> when supplied; <c>null</c>
        /// lists every definition in the catalog regardless of status. No
        /// Archived-list-specific query exists yet -- that surfacing
        /// requirement belongs to `ODY-S05-103`; this method is the single,
        /// generic list primitive it and `ODY-S05-102` will both build on.
        /// </summary>
        Result<IReadOnlyList<ContentDefinitionRecord>> ListContentDefinitions(CampaignHandle campaign, ContentDefinitionStatus? statusFilter, CorrelationId correlationId);

        /// <summary>
        /// ODY-S05-102: creates a brand-new Draft (`Status=Draft`,
        /// `Version=0`, `Revision=1`, its own new <see cref="ContentDefinitionId"/>)
        /// copying `DefinitionType`/`Name`/`Description`/`RulesetCompatibility`/
        /// `Tags`/`PropertiesJson`/`DependencyRefs` from the exact
        /// <paramref name="publishedDefinitionId"/> source at the moment of
        /// the call. The Published source is read only -- never updated --
        /// so this can never violate `ADR-027` section 4.1's
        /// Published-immutability rule. Fails with
        /// <c>PersistenceContentDefinitionNotFound</c> if the source does
        /// not exist, or <c>PersistenceContentDefinitionNotPublished</c> if
        /// it exists but its own `Status` is not `Published`.
        /// </summary>
        Result<ContentDefinitionRecord> CreateNextDraftVersionFromPublished(CampaignHandle campaign, ContentDefinitionId publishedDefinitionId, UserId createdByUserId, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S05-103: publishes a Draft, producing an immutable Published
        /// version (`ADR-027` section 4.1; `11_Content_Block_System`
        /// section 6.2/6.3). Fails with <c>PersistenceContentDefinitionNotFound</c>
        /// if the target does not exist, <c>PersistenceContentDefinitionNotDraft</c>
        /// if its <see cref="ContentDefinitionRecord.Status"/> is not
        /// <see cref="ContentDefinitionStatus.Draft"/>, or
        /// <c>PersistenceContentDefinitionRevisionConflict</c> if
        /// <paramref name="expectedRevision"/> is stale. Does NOT itself run
        /// `ODY-S05-104`'s own usability validation -- callers (the
        /// `ODY-S05-103` Application-layer lifecycle service) are expected
        /// to gate the call on <c>CatalogValidationService.ValidateDraftForPublish</c>
        /// returning a valid result first; this repository method only
        /// enforces the structural lifecycle transition itself.
        /// </summary>
        Result<ContentDefinitionRecord> PublishDefinition(CampaignHandle campaign, ContentDefinitionId definitionId, UserId publishedByUserId, long expectedRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S05-103: archives a Published definition -- the row is never
        /// physically removed and remains fully loadable through
        /// <see cref="GetContentDefinition"/> and <see cref="ListContentDefinitions"/>
        /// afterward (`ADR-027` section 4.1 rules 2/3). Fails with
        /// <c>PersistenceContentDefinitionNotFound</c> if the target does
        /// not exist, or <c>PersistenceContentDefinitionNotPublished</c> if
        /// its own <see cref="ContentDefinitionRecord.Status"/> is not
        /// <see cref="ContentDefinitionStatus.Published"/> -- this MVP only
        /// implements the Published-to-Archived transition; archiving a
        /// still-Draft-but-referenced definition (the ADR's own second
        /// archive trigger) does not arise in this codebase yet since
        /// nothing outside the catalog can reference a Draft (see this
        /// task's own contract section 18).
        /// </summary>
        Result<ContentDefinitionRecord> ArchiveDefinition(CampaignHandle campaign, ContentDefinitionId definitionId, string? archiveReason, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S05-103 (amended twice): physically removes an unused Draft
        /// row -- `ADR-027` section 4.1 rule 1's only allowed
        /// physical-delete case. Fails with <c>PersistenceContentDefinitionNotFound</c>
        /// if the target does not exist, <c>PersistenceContentDefinitionNotDraft</c>
        /// if its own <see cref="ContentDefinitionRecord.Status"/> is not
        /// <see cref="ContentDefinitionStatus.Draft"/>, or
        /// <c>PersistenceContentDefinitionReferenced</c> if another catalog
        /// definition's own `DependencyRefsJson`/`PropertiesJson` still
        /// references this <see cref="ContentDefinitionId"/> (section 4.1
        /// rule 4's "no catalog dependency" precondition -- checked and
        /// deleted atomically in the same transaction, so no other command
        /// can create a new reference between the check and the delete).
        /// Runtime-reference checks (`ItemInstance`/`ItemStack`/Inventory/
        /// equipment/`ActiveEffect`, section 4.1 rule 5) are an explicit,
        /// not-yet-implemented future extension boundary: no such runtime
        /// state exists anywhere in this codebase yet for this method to
        /// check against. Returns a bare <see cref="Result"/> (no
        /// remaining record to return).
        ///
        /// Idempotency/identity is checked against two distinct ledgers,
        /// in order: a dedicated delete-only ledger (a hit whose recorded
        /// target matches <paramref name="definitionId"/> is a genuine
        /// replay -- `Success` even though the row is gone; a hit whose
        /// recorded target differs is a `CommandId` reused across two
        /// different delete targets, rejected with `CommandIdentityMismatch`),
        /// then the *shared* `ContentDefinitionCommandLedger` every
        /// create/update/publish/archive/`CreateNextDraftVersionFromPublished`
        /// command also writes to (any hit there means this `CommandId`
        /// was already used for a *non-delete* operation and is therefore
        /// also rejected with `CommandIdentityMismatch`, never treated as
        /// a delete replay). Only when the `CommandId` appears in neither
        /// ledger does this method proceed to its normal checks and the
        /// physical delete itself.
        /// </summary>
        Result DeleteDraftDefinition(CampaignHandle campaign, ContentDefinitionId definitionId, CommandId commandId, CorrelationId correlationId);
    }

    public sealed class CreateDraftContentDefinitionRequest
    {
        public CreateDraftContentDefinitionRequest(
            CampaignHandle campaign,
            ContentDefinitionType definitionType,
            string name,
            string? description,
            UserId createdByUserId,
            IReadOnlyList<string>? rulesetCompatibility = null,
            IReadOnlyList<string>? tags = null,
            string? propertiesJson = null,
            IReadOnlyList<ContentDefinitionRef>? dependencyRefs = null)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            if (!Enum.IsDefined(typeof(ContentDefinitionType), definitionType)) throw new ArgumentOutOfRangeException(nameof(definitionType));
            if (string.IsNullOrWhiteSpace(name) || name.Length > 128) throw new ArgumentException("Name is not safe.", nameof(name));
            if (!createdByUserId.IsValid) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));

            DefinitionType = definitionType;
            Name = name;
            Description = description;
            CreatedByUserId = createdByUserId;
            RulesetCompatibility = rulesetCompatibility ?? Array.Empty<string>();
            Tags = tags ?? Array.Empty<string>();
            PropertiesJson = propertiesJson ?? "{}";
            DependencyRefs = dependencyRefs ?? Array.Empty<ContentDefinitionRef>();
        }

        public CampaignHandle Campaign { get; }
        public ContentDefinitionType DefinitionType { get; }
        public string Name { get; }
        public string? Description { get; }
        public UserId CreatedByUserId { get; }
        public IReadOnlyList<string> RulesetCompatibility { get; }
        public IReadOnlyList<string> Tags { get; }
        public string PropertiesJson { get; }
        public IReadOnlyList<ContentDefinitionRef> DependencyRefs { get; }
    }

    /// <summary>
    /// ODY-S05-101: the generic `ContentDefinition` envelope
    /// (`11_Content_Block_System` section 5.3) read shape. <see cref="Origin"/>
    /// is always <see cref="ContentDefinitionOrigin.RulesetPackage"/> for
    /// every row this task's own repository can produce (section 3.2's
    /// base/Ruleset-only MVP scope decision) -- the field exists because the
    /// product document's own envelope already fixes it, not because this
    /// task implements Campaign-origin behavior.
    /// </summary>
    public sealed class ContentDefinitionRecord
    {
        public ContentDefinitionRecord(
            ContentDefinitionId contentDefinitionId,
            ContentDefinitionOrigin origin,
            ContentDefinitionType definitionType,
            string name,
            string? description,
            ContentDefinitionStatus status,
            long version,
            long revision,
            IReadOnlyList<string> rulesetCompatibility,
            IReadOnlyList<string> tags,
            string propertiesJson,
            IReadOnlyList<ContentDefinitionRef> dependencyRefs,
            UserId createdByUserId,
            UserId? publishedByUserId,
            UtcInstant? publishedAt,
            UtcInstant? archivedAt,
            string? archiveReason,
            UtcInstant createdAt,
            UtcInstant updatedAt)
        {
            if (!contentDefinitionId.IsValid) throw new ArgumentException("ContentDefinitionId is required.", nameof(contentDefinitionId));
            if (!Enum.IsDefined(typeof(ContentDefinitionOrigin), origin)) throw new ArgumentOutOfRangeException(nameof(origin));
            if (!Enum.IsDefined(typeof(ContentDefinitionType), definitionType)) throw new ArgumentOutOfRangeException(nameof(definitionType));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
            if (!Enum.IsDefined(typeof(ContentDefinitionStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version), "Version 0 means 'never published'; it must never be negative.");
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
            if (!createdByUserId.IsValid) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
            if (propertiesJson == null) throw new ArgumentNullException(nameof(propertiesJson));

            ContentDefinitionId = contentDefinitionId;
            Origin = origin;
            DefinitionType = definitionType;
            Name = name;
            Description = description;
            Status = status;
            Version = version;
            Revision = revision;
            RulesetCompatibility = rulesetCompatibility ?? throw new ArgumentNullException(nameof(rulesetCompatibility));
            Tags = tags ?? throw new ArgumentNullException(nameof(tags));
            PropertiesJson = propertiesJson;
            DependencyRefs = dependencyRefs ?? throw new ArgumentNullException(nameof(dependencyRefs));
            CreatedByUserId = createdByUserId;
            PublishedByUserId = publishedByUserId;
            PublishedAt = publishedAt;
            ArchivedAt = archivedAt;
            ArchiveReason = archiveReason;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        public ContentDefinitionId ContentDefinitionId { get; }
        public ContentDefinitionOrigin Origin { get; }
        public ContentDefinitionType DefinitionType { get; }
        public string Name { get; }
        public string? Description { get; }
        public ContentDefinitionStatus Status { get; }

        /// <summary>0 means "never published yet" (still Draft). Becomes >= 1 only once a future `ODY-S05-103` `PublishDefinition` call exists; this task never writes a value other than 0.</summary>
        public long Version { get; }

        /// <summary>Draft edit revision, optimistic-concurrency counter (`11_Content_Block_System` section 6.2). Starts at 1, increments on every <see cref="IContentCatalogRepository.UpdateDraftContentDefinition"/> call.</summary>
        public long Revision { get; }

        public IReadOnlyList<string> RulesetCompatibility { get; }
        public IReadOnlyList<string> Tags { get; }

        /// <summary>Opaque JSON blob. No typed Weapon/Armor/Ammo/Ability/Effect shape is imposed by this task (`ODY-S05-105`'s own job).</summary>
        public string PropertiesJson { get; }

        /// <summary>Exact-version references to other catalog definitions this one depends on (`ADR-027` section 4 rule 5). Stored and round-tripped only -- no missing-reference validation exists yet (`ODY-S05-104`'s own job).</summary>
        public IReadOnlyList<ContentDefinitionRef> DependencyRefs { get; }

        public UserId CreatedByUserId { get; }
        public UserId? PublishedByUserId { get; }
        public UtcInstant? PublishedAt { get; }
        public UtcInstant? ArchivedAt { get; }
        public string? ArchiveReason { get; }
        public UtcInstant CreatedAt { get; }
        public UtcInstant UpdatedAt { get; }
    }
}
