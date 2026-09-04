using System;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Content
{
    /// <summary>
    /// ODY-S05-103: Publish/Archive/Delete Lifecycle. Follows
    /// <see cref="ContentCatalogAuthoringService"/>'s exact structural
    /// precedent for an Application-layer service sitting above a
    /// repository: MainGM authorization is checked here, before any
    /// repository mutation, so a denied request causes no repository state
    /// change and consumes no `CommandId` in the `ContentDefinitionCommandLedger`.
    /// Reuses <see cref="ContentCatalogAuthoringFailures.NotMainGm"/> directly
    /// rather than minting a duplicate authorization error -- the same
    /// `ADR-027` section 12 MainGM-only baseline governs every catalog
    /// authoring/lifecycle command, not a separate rule per command.
    ///
    /// <see cref="PublishDefinition"/> is the one place this task
    /// integrates `ODY-S05-104`'s own <see cref="CatalogValidationService.ValidateDraftForPublish"/>
    /// as a real, server-side publish gate -- never trusting a client-side
    /// validation result. Publishing a Draft that fails validation never
    /// calls <see cref="IContentCatalogRepository.PublishDefinition"/> at
    /// all, so it can never mutate the row.
    /// </summary>
    public static class ContentCatalogLifecycleService
    {
        /// <summary>
        /// Validates a Draft server-side via `ODY-S05-104`'s own
        /// <see cref="CatalogValidationService.ValidateDraftForPublish"/>
        /// before publishing it. Validation is skipped when the target is
        /// no longer a Draft: at that point the request is either a safe
        /// `CommandId` replay of an already-applied publish (the repository's
        /// own `ContentDefinitionCommandLedger` recognizes it and returns
        /// the existing Published record unchanged) or a genuinely invalid
        /// attempt to publish a non-Draft target (the repository's own
        /// `PersistenceContentDefinitionNotDraft` check rejects it) --
        /// re-running Draft-only validation against an already-Published
        /// record would otherwise always report `DefinitionNotDraft` and
        /// incorrectly block a legitimate replay.
        /// </summary>
        public static Result<ContentDefinitionRecord> PublishDefinition(IContentCatalogRepository repository, PublishDefinitionRequest request)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (!request.ActorIsMainGm)
            {
                return Result<ContentDefinitionRecord>.Failure(ContentCatalogAuthoringFailures.NotMainGm(request.CorrelationId));
            }

            Result<ContentDefinitionRecord> current = repository.GetContentDefinition(request.Campaign, request.DefinitionId, request.CorrelationId);
            if (current.IsFailure)
            {
                return current;
            }

            if (current.Value.Status == ContentDefinitionStatus.Draft)
            {
                var validationRequest = new ValidateContentDefinitionRequest(request.Campaign, request.DefinitionId, request.CorrelationId);
                Result<CatalogValidationResult> validated = CatalogValidationService.ValidateDraftForPublish(repository, validationRequest);
                if (validated.IsFailure)
                {
                    return Result<ContentDefinitionRecord>.Failure(validated.Error);
                }

                if (!validated.Value.IsValid)
                {
                    return Result<ContentDefinitionRecord>.Failure(ContentCatalogLifecycleFailures.PublishValidationFailed(request.CorrelationId));
                }
            }

            return repository.PublishDefinition(request.Campaign, request.DefinitionId, request.ActorUserId, request.ExpectedRevision, request.CommandId, request.CorrelationId);
        }

        /// <summary>
        /// ODY-S05-103: archives a Published definition. The row is never
        /// physically removed and remains loadable through
        /// <see cref="IContentCatalogRepository.GetContentDefinition"/>/<see cref="IContentCatalogRepository.ListContentDefinitions"/>
        /// afterward.
        /// </summary>
        public static Result<ContentDefinitionRecord> ArchiveDefinition(IContentCatalogRepository repository, ArchiveDefinitionRequest request)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (!request.ActorIsMainGm)
            {
                return Result<ContentDefinitionRecord>.Failure(ContentCatalogAuthoringFailures.NotMainGm(request.CorrelationId));
            }

            return repository.ArchiveDefinition(request.Campaign, request.DefinitionId, request.ArchiveReason, request.CommandId, request.CorrelationId);
        }

        /// <summary>
        /// ODY-S05-103: physically removes an unused Draft. The repository
        /// itself re-checks Draft status and the catalog-dependency
        /// reference scan atomically -- this service does not duplicate
        /// those checks, matching every sibling lifecycle method's own
        /// division of labor (authorization here, structural/business
        /// invariants in the repository).
        /// </summary>
        public static Result DeleteDraftDefinition(IContentCatalogRepository repository, DeleteDraftDefinitionRequest request)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (!request.ActorIsMainGm)
            {
                return Result.Failure(ContentCatalogAuthoringFailures.NotMainGm(request.CorrelationId));
            }

            return repository.DeleteDraftDefinition(request.Campaign, request.DefinitionId, request.CommandId, request.CorrelationId);
        }

        /// <summary>
        /// ODY-S05-103: the dedicated Archived-list query
        /// `SLICE-05_IMPLEMENTATION_BACKLOG.md` section 3.5 requires --
        /// data/API-level only, no UI. A thin, MainGM-gated wrapper over
        /// the existing generic <see cref="IContentCatalogRepository.ListContentDefinitions"/>
        /// primitive (`ODY-S05-101`) filtered to <see cref="ContentDefinitionStatus.Archived"/>
        /// -- no new repository method or persistence shape was needed.
        /// </summary>
        public static Result<System.Collections.Generic.IReadOnlyList<ContentDefinitionRecord>> ListArchivedDefinitions(IContentCatalogRepository repository, ListArchivedDefinitionsRequest request)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (!request.ActorIsMainGm)
            {
                return Result<System.Collections.Generic.IReadOnlyList<ContentDefinitionRecord>>.Failure(ContentCatalogAuthoringFailures.NotMainGm(request.CorrelationId));
            }

            return repository.ListContentDefinitions(request.Campaign, ContentDefinitionStatus.Archived, request.CorrelationId);
        }
    }

    public sealed class PublishDefinitionRequest
    {
        public PublishDefinitionRequest(
            CampaignHandle campaign,
            ContentDefinitionId definitionId,
            long expectedRevision,
            UserId actorUserId,
            bool actorIsMainGm,
            CommandId commandId,
            CorrelationId correlationId)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            if (!definitionId.IsValid) throw new ArgumentException("ContentDefinitionId is required.", nameof(definitionId));
            if (expectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            DefinitionId = definitionId;
            ExpectedRevision = expectedRevision;
            ActorUserId = actorUserId;
            ActorIsMainGm = actorIsMainGm;
            CommandId = commandId;
            CorrelationId = correlationId;
        }

        public CampaignHandle Campaign { get; }
        public ContentDefinitionId DefinitionId { get; }
        public long ExpectedRevision { get; }
        public UserId ActorUserId { get; }
        public bool ActorIsMainGm { get; }
        public CommandId CommandId { get; }
        public CorrelationId CorrelationId { get; }
    }

    public sealed class ArchiveDefinitionRequest
    {
        public ArchiveDefinitionRequest(
            CampaignHandle campaign,
            ContentDefinitionId definitionId,
            string? archiveReason,
            bool actorIsMainGm,
            CommandId commandId,
            CorrelationId correlationId)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            if (!definitionId.IsValid) throw new ArgumentException("ContentDefinitionId is required.", nameof(definitionId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            DefinitionId = definitionId;
            ArchiveReason = archiveReason;
            ActorIsMainGm = actorIsMainGm;
            CommandId = commandId;
            CorrelationId = correlationId;
        }

        public CampaignHandle Campaign { get; }
        public ContentDefinitionId DefinitionId { get; }
        public string? ArchiveReason { get; }
        public bool ActorIsMainGm { get; }
        public CommandId CommandId { get; }
        public CorrelationId CorrelationId { get; }
    }

    public sealed class DeleteDraftDefinitionRequest
    {
        public DeleteDraftDefinitionRequest(
            CampaignHandle campaign,
            ContentDefinitionId definitionId,
            bool actorIsMainGm,
            CommandId commandId,
            CorrelationId correlationId)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            if (!definitionId.IsValid) throw new ArgumentException("ContentDefinitionId is required.", nameof(definitionId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            DefinitionId = definitionId;
            ActorIsMainGm = actorIsMainGm;
            CommandId = commandId;
            CorrelationId = correlationId;
        }

        public CampaignHandle Campaign { get; }
        public ContentDefinitionId DefinitionId { get; }
        public bool ActorIsMainGm { get; }
        public CommandId CommandId { get; }
        public CorrelationId CorrelationId { get; }
    }

    /// <summary>A pure read -- no <see cref="CommandId"/> is needed since nothing is mutated.</summary>
    public sealed class ListArchivedDefinitionsRequest
    {
        public ListArchivedDefinitionsRequest(CampaignHandle campaign, bool actorIsMainGm, CorrelationId correlationId)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            ActorIsMainGm = actorIsMainGm;
            CorrelationId = correlationId;
        }

        public CampaignHandle Campaign { get; }
        public bool ActorIsMainGm { get; }
        public CorrelationId CorrelationId { get; }
    }

    /// <summary>ODY-S05-103: lifecycle-service-level failures distinct from <see cref="PersistenceFailures"/> because the check happens in the Application-layer service, before (or instead of) the repository mutation.</summary>
    public static class ContentCatalogLifecycleFailures
    {
        /// <summary>`ODY-S05-104`'s own `CatalogValidationService.ValidateDraftForPublish` returned at least one `Error`-severity issue -- the Draft is not usable/publishable yet. The repository's own `PublishDefinition` is never called, so no row mutation occurs.</summary>
        public static Error PublishValidationFailed(CorrelationId correlationId) => Error.Create(
            ErrorCodes.ContentCatalogPublishValidationFailed,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.content_catalog.publish_validation_failed"),
            RetryDirective.DoNotRetry,
            correlationId);
    }
}
