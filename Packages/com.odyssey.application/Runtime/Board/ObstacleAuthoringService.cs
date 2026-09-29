using System;
using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Identity;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Geometry;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Board
{
    /// <summary>
    /// SLICE-10 Block 2: obstacle (wall/door/window) authoring, over a real, durable
    /// <see cref="IObstacleRepository"/>. This is the first service written from the start with
    /// ODY-S10-101's stored-membership check -- no client-supplied MainGM flag ever existed here.
    ///
    /// <see cref="CreateObstacle"/> is MainGM-only, the same class of action as
    /// <see cref="Odyssey.Application.Content.ContentCatalogAuthoringService.CreateDraftDefinition"/>
    /// (map/content editing, not something an ordinary player does) -- checked before the repository
    /// is ever called, so a denied request causes no state change and consumes no <see cref="CommandId"/>.
    /// <see cref="ToggleDoorState"/> is open to any registered campaign participant (the product
    /// document's own "toggled by players/GM"), checked via a plain membership lookup, not MainGM-only.
    /// <see cref="ListObstacles"/> performs no authorization at all, by the same precedent
    /// <c>ContentCatalogAuthoringService.GetContentDefinition</c>/<c>ListContentDefinitions</c>
    /// (ODY-S10-104) already established for catalog reads -- a deliberate, disclosed decision, not an
    /// oversight.
    /// </summary>
    public static class ObstacleAuthoringService
    {
        public static Result<ObstacleRecord> CreateObstacle(IObstacleRepository repository, ICampaignRepository campaignRepository, CreateObstacleRequest request)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (campaignRepository == null) throw new ArgumentNullException(nameof(campaignRepository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            Result<bool> mainGmCheck = CampaignMembershipAuthorization.IsMainGm(campaignRepository, request.Campaign, request.ActorUserId, request.CorrelationId);
            if (mainGmCheck.IsFailure)
            {
                return Result<ObstacleRecord>.Failure(mainGmCheck.Error);
            }

            if (!mainGmCheck.Value)
            {
                return Result<ObstacleRecord>.Failure(ObstacleFailures.CreateDenied(request.CorrelationId));
            }

            return repository.CreateObstacle(request.Campaign, request.SceneId, request.Kind, request.X1, request.Y1, request.X2, request.Y2, request.CommandId, request.CorrelationId);
        }

        public static Result<ObstacleRecord> ToggleDoorState(IObstacleRepository repository, ICampaignRepository campaignRepository, ToggleDoorStateRequest request)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (campaignRepository == null) throw new ArgumentNullException(nameof(campaignRepository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            // Any registered campaign participant may toggle a door -- not MainGM-only, exactly as the
            // product document specifies ("переключаемым игроками/мастером"). A plain membership
            // lookup: IsSuccess means the actor has a row in this campaign in ANY role; IsFailure
            // (including "not a member") fails closed, never a silent pass.
            Result<CampaignMemberLookup> membership = campaignRepository.GetMemberRole(request.Campaign, request.ActorUserId, request.CorrelationId);
            if (membership.IsFailure)
            {
                return Result<ObstacleRecord>.Failure(membership.Error);
            }

            if (!membership.Value.IsMember)
            {
                return Result<ObstacleRecord>.Failure(ObstacleFailures.ToggleDenied(request.CorrelationId));
            }

            return repository.ToggleDoorState(request.Campaign, request.ObstacleId, request.IsOpen, request.ExpectedRevision, request.CommandId, request.CorrelationId);
        }

        public static Result<IReadOnlyList<ObstacleRecord>> ListObstacles(IObstacleRepository repository, ListObstaclesRequest request)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            return repository.ListObstacles(request.Campaign, request.SceneId, request.CorrelationId);
        }
    }

    public sealed class CreateObstacleRequest
    {
        public CreateObstacleRequest(CampaignHandle campaign, SceneId sceneId, ObstacleKind kind, double x1, double y1, double x2, double y2, UserId actorUserId, CommandId commandId, CorrelationId correlationId)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));
            if (!BoardGeometry.IsFinite(x1, y1) || !BoardGeometry.IsFinite(x2, y2)) throw new ArgumentException("Obstacle endpoints must be finite.");
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            SceneId = sceneId;
            Kind = kind;
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
            ActorUserId = actorUserId;
            CommandId = commandId;
            CorrelationId = correlationId;
        }

        public CampaignHandle Campaign { get; }
        public SceneId SceneId { get; }
        public ObstacleKind Kind { get; }
        public double X1 { get; }
        public double Y1 { get; }
        public double X2 { get; }
        public double Y2 { get; }
        public UserId ActorUserId { get; }
        public CommandId CommandId { get; }
        public CorrelationId CorrelationId { get; }
    }

    public sealed class ToggleDoorStateRequest
    {
        public ToggleDoorStateRequest(CampaignHandle campaign, ObstacleId obstacleId, bool isOpen, long expectedRevision, UserId actorUserId, CommandId commandId, CorrelationId correlationId)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            if (!obstacleId.IsValid) throw new ArgumentException("ObstacleId is required.", nameof(obstacleId));
            if (expectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            ObstacleId = obstacleId;
            IsOpen = isOpen;
            ExpectedRevision = expectedRevision;
            ActorUserId = actorUserId;
            CommandId = commandId;
            CorrelationId = correlationId;
        }

        public CampaignHandle Campaign { get; }
        public ObstacleId ObstacleId { get; }
        public bool IsOpen { get; }
        public long ExpectedRevision { get; }
        public UserId ActorUserId { get; }
        public CommandId CommandId { get; }
        public CorrelationId CorrelationId { get; }
    }

    public sealed class ListObstaclesRequest
    {
        public ListObstaclesRequest(CampaignHandle campaign, SceneId sceneId, CorrelationId correlationId)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));

            SceneId = sceneId;
            CorrelationId = correlationId;
        }

        public CampaignHandle Campaign { get; }
        public SceneId SceneId { get; }
        public CorrelationId CorrelationId { get; }
    }
}
