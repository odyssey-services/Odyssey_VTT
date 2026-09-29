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
    /// SLICE-10 Block 3: token facing/vision-parameter commands and the line-of-sight query, over
    /// real, durable <see cref="ITokenVisionRepository"/>/<see cref="IObstacleRepository"/>/
    /// <see cref="ISceneRepository"/> ports. Written from the start with `ODY-S10-101`'s stored-
    /// membership authorization (no client-supplied flag ever existed here, same as `ODY-S10-107`'s
    /// own `ObstacleAuthoringService`).
    ///
    /// <see cref="SetTokenFacing"/> is owner-or-MainGM, by exact precedent of
    /// <see cref="BoardMovementService.MoveToken"/> -- turning a token to face a direction is part
    /// of controlling it, the same class of action as moving it. <see cref="SetTokenVisionParameters"/>
    /// is MainGM-only, by exact precedent of <c>ObstacleAuthoringService.CreateObstacle</c> --
    /// configuring a token's own field-of-view/range is a game-master parameter, not something an
    /// ordinary player sets on their own token. <see cref="ComputeLineOfSight"/> performs no
    /// authorization at all, by exact precedent of <c>ObstacleAuthoringService.ListObstacles</c>: a
    /// pure, read-only computation over data (obstacle geometry, token positions) already open to
    /// any caller through <c>ListObstacles</c>/<c>ListTokens</c>.
    /// </summary>
    public static class TokenVisionService
    {
        public static Result<TokenVisionSettingsRecord> SetTokenFacing(ITokenVisionRepository visionRepository, ISceneRepository sceneRepository, ICampaignRepository campaignRepository, SetTokenFacingRequest request)
        {
            if (visionRepository == null) throw new ArgumentNullException(nameof(visionRepository));
            if (sceneRepository == null) throw new ArgumentNullException(nameof(sceneRepository));
            if (campaignRepository == null) throw new ArgumentNullException(nameof(campaignRepository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            Result<TokenRecord> token = sceneRepository.GetToken(request.Campaign, request.TokenId, request.CorrelationId);
            if (token.IsFailure)
            {
                return Result<TokenVisionSettingsRecord>.Failure(token.Error);
            }

            Result authorization = CheckControllerOrMainGm(campaignRepository, request.Campaign, token.Value, request.ActorUserId, request.CorrelationId, TokenVisionFailures.SetFacingDenied(request.CorrelationId));
            if (authorization.IsFailure)
            {
                return Result<TokenVisionSettingsRecord>.Failure(authorization.Error);
            }

            return visionRepository.SetFacing(request.Campaign, request.TokenId, request.FacingDegrees, request.ExpectedRevision, request.CommandId, request.CorrelationId);
        }

        public static Result<TokenVisionSettingsRecord> SetTokenVisionParameters(ITokenVisionRepository visionRepository, ICampaignRepository campaignRepository, SetTokenVisionParametersRequest request)
        {
            if (visionRepository == null) throw new ArgumentNullException(nameof(visionRepository));
            if (campaignRepository == null) throw new ArgumentNullException(nameof(campaignRepository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            Result<bool> mainGmCheck = CampaignMembershipAuthorization.IsMainGm(campaignRepository, request.Campaign, request.ActorUserId, request.CorrelationId);
            if (mainGmCheck.IsFailure)
            {
                return Result<TokenVisionSettingsRecord>.Failure(mainGmCheck.Error);
            }

            if (!mainGmCheck.Value)
            {
                return Result<TokenVisionSettingsRecord>.Failure(TokenVisionFailures.SetVisionParametersDenied(request.CorrelationId));
            }

            return visionRepository.SetVisionParameters(request.Campaign, request.TokenId, request.FovAngleDegrees, request.ViewDistance, request.ExpectedRevision, request.CommandId, request.CorrelationId);
        }

        public static Result<bool> ComputeLineOfSight(ISceneRepository sceneRepository, ITokenVisionRepository visionRepository, IObstacleRepository obstacleRepository, ComputeLineOfSightRequest request)
        {
            if (sceneRepository == null) throw new ArgumentNullException(nameof(sceneRepository));
            if (visionRepository == null) throw new ArgumentNullException(nameof(visionRepository));
            if (obstacleRepository == null) throw new ArgumentNullException(nameof(obstacleRepository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            Result<TokenRecord> observerToken = sceneRepository.GetToken(request.Campaign, request.ObserverTokenId, request.CorrelationId);
            if (observerToken.IsFailure)
            {
                return Result<bool>.Failure(observerToken.Error);
            }

            Result<TokenRecord> targetToken = sceneRepository.GetToken(request.Campaign, request.TargetTokenId, request.CorrelationId);
            if (targetToken.IsFailure)
            {
                return Result<bool>.Failure(targetToken.Error);
            }

            if (!observerToken.Value.SceneId.Equals(targetToken.Value.SceneId))
            {
                return Result<bool>.Failure(TokenVisionFailures.ObserverAndTargetNotInSameScene(request.CorrelationId));
            }

            Result<TokenVisionSettingsRecord> observerVision = visionRepository.GetVisionSettings(request.Campaign, request.ObserverTokenId, request.CorrelationId);
            if (observerVision.IsFailure)
            {
                return Result<bool>.Failure(observerVision.Error);
            }

            Result<IReadOnlyList<ObstacleRecord>> obstacles = obstacleRepository.ListObstacles(request.Campaign, observerToken.Value.SceneId, request.CorrelationId);
            if (obstacles.IsFailure)
            {
                return Result<bool>.Failure(obstacles.Error);
            }

            var segments = new ObstacleSegment[obstacles.Value.Count];
            for (int index = 0; index < obstacles.Value.Count; index++)
            {
                ObstacleRecord obstacle = obstacles.Value[index];
                segments[index] = new ObstacleSegment(obstacle.Kind, obstacle.IsOpen, obstacle.X1, obstacle.Y1, obstacle.X2, obstacle.Y2);
            }

            bool canSee = LineOfSight.CanSee(
                observerToken.Value.Position.X, observerToken.Value.Position.Y,
                observerVision.Value.FacingDegrees, observerVision.Value.FovAngleDegrees, observerVision.Value.ViewDistance,
                targetToken.Value.Position.X, targetToken.Value.Position.Y,
                segments);

            return Result<bool>.Success(canSee);
        }

        // Mirrors BoardMovementService.CheckAuthorization's exact shape: the token's own controller may always act; anyone else must be the campaign's stored MainGM.
        private static Result CheckControllerOrMainGm(ICampaignRepository campaignRepository, CampaignHandle campaign, TokenRecord token, UserId actorUserId, CorrelationId correlationId, Error deniedError)
        {
            if (token.ControllerUserId.Equals(actorUserId))
            {
                return Result.Success();
            }

            Result<bool> isMainGm = CampaignMembershipAuthorization.IsMainGm(campaignRepository, campaign, actorUserId, correlationId);
            return isMainGm.IsFailure ? Result.Failure(isMainGm.Error) : isMainGm.Value ? Result.Success() : Result.Failure(deniedError);
        }
    }

    public sealed class SetTokenFacingRequest
    {
        public SetTokenFacingRequest(CampaignHandle campaign, TokenId tokenId, double facingDegrees, long expectedRevision, UserId actorUserId, CommandId commandId, CorrelationId correlationId)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            if (!tokenId.IsValid) throw new ArgumentException("TokenId is required.", nameof(tokenId));
            if (!double.IsFinite(facingDegrees)) throw new ArgumentException("FacingDegrees must be finite.", nameof(facingDegrees));
            if (expectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            TokenId = tokenId;
            FacingDegrees = facingDegrees;
            ExpectedRevision = expectedRevision;
            ActorUserId = actorUserId;
            CommandId = commandId;
            CorrelationId = correlationId;
        }

        public CampaignHandle Campaign { get; }
        public TokenId TokenId { get; }
        public double FacingDegrees { get; }
        public long ExpectedRevision { get; }
        public UserId ActorUserId { get; }
        public CommandId CommandId { get; }
        public CorrelationId CorrelationId { get; }
    }

    public sealed class SetTokenVisionParametersRequest
    {
        public SetTokenVisionParametersRequest(CampaignHandle campaign, TokenId tokenId, double fovAngleDegrees, double viewDistance, long expectedRevision, UserId actorUserId, CommandId commandId, CorrelationId correlationId)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            if (!tokenId.IsValid) throw new ArgumentException("TokenId is required.", nameof(tokenId));
            if (!double.IsFinite(fovAngleDegrees) || fovAngleDegrees < 0) throw new ArgumentException("FovAngleDegrees must be finite and non-negative.", nameof(fovAngleDegrees));
            if (!double.IsFinite(viewDistance) || viewDistance < 0) throw new ArgumentException("ViewDistance must be finite and non-negative.", nameof(viewDistance));
            if (expectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            TokenId = tokenId;
            FovAngleDegrees = fovAngleDegrees;
            ViewDistance = viewDistance;
            ExpectedRevision = expectedRevision;
            ActorUserId = actorUserId;
            CommandId = commandId;
            CorrelationId = correlationId;
        }

        public CampaignHandle Campaign { get; }
        public TokenId TokenId { get; }
        public double FovAngleDegrees { get; }
        public double ViewDistance { get; }
        public long ExpectedRevision { get; }
        public UserId ActorUserId { get; }
        public CommandId CommandId { get; }
        public CorrelationId CorrelationId { get; }
    }

    public sealed class ComputeLineOfSightRequest
    {
        public ComputeLineOfSightRequest(CampaignHandle campaign, TokenId observerTokenId, TokenId targetTokenId, CorrelationId correlationId)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            if (!observerTokenId.IsValid) throw new ArgumentException("ObserverTokenId is required.", nameof(observerTokenId));
            if (!targetTokenId.IsValid) throw new ArgumentException("TargetTokenId is required.", nameof(targetTokenId));

            ObserverTokenId = observerTokenId;
            TargetTokenId = targetTokenId;
            CorrelationId = correlationId;
        }

        public CampaignHandle Campaign { get; }
        public TokenId ObserverTokenId { get; }
        public TokenId TargetTokenId { get; }
        public CorrelationId CorrelationId { get; }
    }
}
