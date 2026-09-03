using System;
using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Content
{
    /// <summary>
    /// ODY-S05-102: GM Catalog Authoring MVP. Follows
    /// <see cref="Odyssey.Application.Board.BoardMovementService"/>'s exact
    /// structural precedent for an Application-layer service sitting above a
    /// repository: authorization is checked here, before the repository is
    /// ever called, so a denied request causes no repository state change
    /// and does not consume a `CommandId` in the
    /// `ContentDefinitionCommandLedger` (`ODY-S05-101`). `CommandId`-keyed
    /// idempotency/replay remains the repository's own concern -- not
    /// duplicated here, exactly as `BoardMovementService`'s own doc comment
    /// already establishes for its own sibling case.
    ///
    /// This is MainGM catalog authoring only: create/edit a Draft, and
    /// create the next Draft version from a Published definition. It does
    /// not implement `PublishDefinition`/`ArchiveDefinition`/physical
    /// delete/Archived-list query (`ODY-S05-103`), per-type usability
    /// validation (`ODY-S05-104`), typed Weapon/Armor/Ammo/Ability/Effect
    /// properties (`ODY-S05-105`), or any runtime Inventory/`ItemInstance`/
    /// `ItemStack`/Equipment/`ActiveEffect` behavior. It does not add a new
    /// role or extend `ADR-027` section 12's permission baseline --
    /// MainGM-only, exactly as that section already fixes for catalog
    /// authoring/publish.
    /// </summary>
    public static class ContentCatalogAuthoringService
    {
        public static Result<ContentDefinitionRecord> CreateDraftDefinition(IContentCatalogRepository repository, CreateDraftDefinitionRequest request)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (!request.ActorIsMainGm)
            {
                return Result<ContentDefinitionRecord>.Failure(ContentCatalogAuthoringFailures.NotMainGm(request.CorrelationId));
            }

            var repositoryRequest = new CreateDraftContentDefinitionRequest(
                request.Campaign, request.DefinitionType, request.Name, request.Description, request.ActorUserId,
                request.RulesetCompatibility, request.Tags, request.PropertiesJson, request.DependencyRefs);

            return repository.CreateDraftContentDefinition(repositoryRequest, request.CommandId, request.CorrelationId);
        }

        public static Result<ContentDefinitionRecord> UpdateDraftDefinition(IContentCatalogRepository repository, UpdateDraftDefinitionRequest request)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (!request.ActorIsMainGm)
            {
                return Result<ContentDefinitionRecord>.Failure(ContentCatalogAuthoringFailures.NotMainGm(request.CorrelationId));
            }

            return repository.UpdateDraftContentDefinition(request.Campaign, request.DefinitionId, request.Name, request.Description, request.PropertiesJson, request.ExpectedRevision, request.CommandId, request.CorrelationId);
        }

        /// <summary>
        /// ODY-S05-102: MainGM creates a new Draft from an already-Published
        /// definition; the Published source is never edited in place
        /// (`ADR-027` section 4.1's Published-immutability rule) -- see
        /// <see cref="Odyssey.Persistence.Sqlite.SqliteContentCatalogRepository.CreateNextDraftVersionFromPublished"/>
        /// for the copy semantics.
        /// </summary>
        public static Result<ContentDefinitionRecord> CreateNextDraftVersionFromPublished(IContentCatalogRepository repository, CreateNextDraftVersionFromPublishedRequest request)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (!request.ActorIsMainGm)
            {
                return Result<ContentDefinitionRecord>.Failure(ContentCatalogAuthoringFailures.NotMainGm(request.CorrelationId));
            }

            return repository.CreateNextDraftVersionFromPublished(request.Campaign, request.PublishedDefinitionId, request.ActorUserId, request.CommandId, request.CorrelationId);
        }
    }

    public sealed class CreateDraftDefinitionRequest
    {
        public CreateDraftDefinitionRequest(
            CampaignHandle campaign,
            ContentDefinitionType definitionType,
            string name,
            string? description,
            UserId actorUserId,
            bool actorIsMainGm,
            CommandId commandId,
            CorrelationId correlationId,
            IReadOnlyList<string>? rulesetCompatibility = null,
            IReadOnlyList<string>? tags = null,
            string? propertiesJson = null,
            IReadOnlyList<ContentDefinitionRef>? dependencyRefs = null)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            if (!Enum.IsDefined(typeof(ContentDefinitionType), definitionType)) throw new ArgumentOutOfRangeException(nameof(definitionType));
            if (string.IsNullOrWhiteSpace(name) || name.Length > 128) throw new ArgumentException("Name is not safe.", nameof(name));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            DefinitionType = definitionType;
            Name = name;
            Description = description;
            ActorUserId = actorUserId;
            ActorIsMainGm = actorIsMainGm;
            CommandId = commandId;
            CorrelationId = correlationId;
            RulesetCompatibility = rulesetCompatibility ?? Array.Empty<string>();
            Tags = tags ?? Array.Empty<string>();
            PropertiesJson = propertiesJson ?? "{}";
            DependencyRefs = dependencyRefs ?? Array.Empty<ContentDefinitionRef>();
        }

        public CampaignHandle Campaign { get; }
        public ContentDefinitionType DefinitionType { get; }
        public string Name { get; }
        public string? Description { get; }
        public UserId ActorUserId { get; }

        /// <summary>ODY-S05-102's own deliberate simplification, matching `BoardMovementService`/`DiceRollService`'s already-established convention: this task has no session/role model of its own (`ADR-019` scope, not reopened) -- the caller supplies whether the actor holds the MainGM baseline role.</summary>
        public bool ActorIsMainGm { get; }

        public CommandId CommandId { get; }
        public CorrelationId CorrelationId { get; }
        public IReadOnlyList<string> RulesetCompatibility { get; }
        public IReadOnlyList<string> Tags { get; }
        public string PropertiesJson { get; }
        public IReadOnlyList<ContentDefinitionRef> DependencyRefs { get; }
    }

    public sealed class UpdateDraftDefinitionRequest
    {
        public UpdateDraftDefinitionRequest(
            CampaignHandle campaign,
            ContentDefinitionId definitionId,
            string name,
            string? description,
            string propertiesJson,
            long expectedRevision,
            UserId actorUserId,
            bool actorIsMainGm,
            CommandId commandId,
            CorrelationId correlationId)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            if (!definitionId.IsValid) throw new ArgumentException("ContentDefinitionId is required.", nameof(definitionId));
            if (string.IsNullOrWhiteSpace(name) || name.Length > 128) throw new ArgumentException("Name is not safe.", nameof(name));
            if (propertiesJson == null) throw new ArgumentNullException(nameof(propertiesJson));
            if (expectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            DefinitionId = definitionId;
            Name = name;
            Description = description;
            PropertiesJson = propertiesJson;
            ExpectedRevision = expectedRevision;
            ActorUserId = actorUserId;
            ActorIsMainGm = actorIsMainGm;
            CommandId = commandId;
            CorrelationId = correlationId;
        }

        public CampaignHandle Campaign { get; }
        public ContentDefinitionId DefinitionId { get; }
        public string Name { get; }
        public string? Description { get; }
        public string PropertiesJson { get; }
        public long ExpectedRevision { get; }
        public UserId ActorUserId { get; }
        public bool ActorIsMainGm { get; }
        public CommandId CommandId { get; }
        public CorrelationId CorrelationId { get; }
    }

    public sealed class CreateNextDraftVersionFromPublishedRequest
    {
        public CreateNextDraftVersionFromPublishedRequest(
            CampaignHandle campaign,
            ContentDefinitionId publishedDefinitionId,
            UserId actorUserId,
            bool actorIsMainGm,
            CommandId commandId,
            CorrelationId correlationId)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            if (!publishedDefinitionId.IsValid) throw new ArgumentException("PublishedDefinitionId is required.", nameof(publishedDefinitionId));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            PublishedDefinitionId = publishedDefinitionId;
            ActorUserId = actorUserId;
            ActorIsMainGm = actorIsMainGm;
            CommandId = commandId;
            CorrelationId = correlationId;
        }

        public CampaignHandle Campaign { get; }
        public ContentDefinitionId PublishedDefinitionId { get; }
        public UserId ActorUserId { get; }
        public bool ActorIsMainGm { get; }
        public CommandId CommandId { get; }
        public CorrelationId CorrelationId { get; }
    }

    /// <summary>ODY-S05-102: authorization failures for the Content Catalog authoring service, mirroring <see cref="Odyssey.Application.Board.BoardFailures"/>'s exact convention -- a distinct class from <see cref="PersistenceFailures"/> because this check happens in the Application-layer service, before the repository is ever called, not inside the repository itself.</summary>
    public static class ContentCatalogAuthoringFailures
    {
        /// <summary>`ADR-027` section 12: MainGM-only for catalog authoring. No new role is introduced; AssistantGM/player callers are rejected the same way an ordinary user would be.</summary>
        public static Error NotMainGm(CorrelationId correlationId) => Error.Create(
            ErrorCodes.ContentCatalogAuthoringDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.content_catalog.authoring_denied"),
            RetryDirective.DoNotRetry,
            correlationId);
    }
}
