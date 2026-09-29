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
    /// SLICE-10 Block 4: two independent per-player states, both keyed directly on <see cref="UserId"/>
    /// (this codebase's established convention -- never an artificial "membership id"):
    ///
    /// (1) Live token visibility (<see cref="ComputeVisibleTokens"/>) -- a thin composition over Block
    /// 3's own <see cref="TokenVisionService.ComputeLineOfSight"/>, no new storage at all. Recomputed
    /// fresh every call; a token that leaves line of sight simply stops appearing, even on already-
    /// explored territory.
    ///
    /// (2) Persistent map memory (<see cref="RecordExploration"/>/<see cref="IsPointExplored"/>) -- an
    /// append-only (INSERT-only) set of circular "reveals" in a new <see cref="IFogOfWarRepository"/>
    /// store. Deliberately coarser than (1): a circle, ignoring obstacle shape -- "roughly had sight
    /// here at some point", not an exact shadow. Monotonic by product decision (2026-09-29): once
    /// explored, a region is never re-hidden.
    ///
    /// Both follow the same two rules throughout: a registered MainGm sees/has explored everything,
    /// unconditionally, without ever touching stored reveal data (by the same principle the
    /// unrelated <c>VisibilityPolicy.IsVisible</c> networking-projection model already uses for its own
    /// MainGM shortcut); and reads are self-scoped -- an ordinary participant may query only their own
    /// state, a MainGm may query anyone's (first proper "self-scoped" read check in this codebase,
    /// modeled directly on <see cref="CampaignMembershipAuthorization.IsMainGm"/>'s own fail-closed
    /// shape).
    /// </summary>
    public static class PlayerVisibilityService
    {
        public static Result<IReadOnlyCollection<TokenId>> ComputeVisibleTokens(ISceneRepository sceneRepository, ITokenVisionRepository visionRepository, IObstacleRepository obstacleRepository, ICampaignRepository campaignRepository, ComputeVisibleTokensRequest request)
        {
            if (sceneRepository == null) throw new ArgumentNullException(nameof(sceneRepository));
            if (visionRepository == null) throw new ArgumentNullException(nameof(visionRepository));
            if (obstacleRepository == null) throw new ArgumentNullException(nameof(obstacleRepository));
            if (campaignRepository == null) throw new ArgumentNullException(nameof(campaignRepository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            Result selfScoped = CheckSelfScopedOrMainGm(campaignRepository, request.Campaign, request.RequestingUserId, request.TargetUserId, request.CorrelationId);
            if (selfScoped.IsFailure)
            {
                return Result<IReadOnlyCollection<TokenId>>.Failure(selfScoped.Error);
            }

            Result<bool> targetIsMainGm = CampaignMembershipAuthorization.IsMainGm(campaignRepository, request.Campaign, request.TargetUserId, request.CorrelationId);
            if (targetIsMainGm.IsFailure)
            {
                return Result<IReadOnlyCollection<TokenId>>.Failure(targetIsMainGm.Error);
            }

            Result<IReadOnlyList<TokenRecord>> allTokens = sceneRepository.ListTokens(request.Campaign, request.SceneId, request.CorrelationId);
            if (allTokens.IsFailure)
            {
                return Result<IReadOnlyCollection<TokenId>>.Failure(allTokens.Error);
            }

            if (targetIsMainGm.Value)
            {
                // The MainGm's own view bypasses live LOS entirely -- every token in the Scene, unconditionally.
                var everyToken = new HashSet<TokenId>();
                foreach (TokenRecord token in allTokens.Value)
                {
                    everyToken.Add(token.TokenId);
                }

                return Result<IReadOnlyCollection<TokenId>>.Success(everyToken);
            }

            var observerTokens = new List<TokenRecord>();
            foreach (TokenRecord token in allTokens.Value)
            {
                if (token.ControllerUserId.Equals(request.TargetUserId))
                {
                    observerTokens.Add(token);
                }
            }

            // No own token in the Scene is not an error -- an empty result, per this task's own decision.
            var visible = new HashSet<TokenId>();
            foreach (TokenRecord observer in observerTokens)
            {
                foreach (TokenRecord candidate in allTokens.Value)
                {
                    if (visible.Contains(candidate.TokenId))
                    {
                        continue;
                    }

                    var losRequest = new ComputeLineOfSightRequest(request.Campaign, observer.TokenId, candidate.TokenId, request.CorrelationId);
                    Result<bool> canSee = TokenVisionService.ComputeLineOfSight(sceneRepository, visionRepository, obstacleRepository, losRequest);
                    if (canSee.IsFailure)
                    {
                        return Result<IReadOnlyCollection<TokenId>>.Failure(canSee.Error);
                    }

                    if (canSee.Value)
                    {
                        visible.Add(candidate.TokenId);
                    }
                }
            }

            return Result<IReadOnlyCollection<TokenId>>.Success(visible);
        }

        /// <summary>
        /// Not automatically wired into <c>BoardMovementService.MoveToken</c>/<c>TokenVisionService.SetTokenFacing</c>
        /// (Block 1/3 files, forbidden paths for this task) -- an explicit, separately-invoked step,
        /// called by whatever orchestrates token movement (UI/networking, out of this task's scope).
        /// Performs no authorization of its own: the exploring player is derived entirely from the
        /// observer token's own <see cref="TokenRecord.ControllerUserId"/>, not a separately supplied
        /// actor, so there is no separate "who is allowed to record for this token" decision to make
        /// here -- whoever legitimately controls the token (already enforced by Block 1/3's own
        /// authorization on the move/facing command that triggers this call) is the one recording.
        /// </summary>
        public static Result RecordExploration(IFogOfWarRepository fogRepository, ISceneRepository sceneRepository, ITokenVisionRepository visionRepository, RecordExplorationRequest request)
        {
            if (fogRepository == null) throw new ArgumentNullException(nameof(fogRepository));
            if (sceneRepository == null) throw new ArgumentNullException(nameof(sceneRepository));
            if (visionRepository == null) throw new ArgumentNullException(nameof(visionRepository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            Result<TokenRecord> observerToken = sceneRepository.GetToken(request.Campaign, request.ObserverTokenId, request.CorrelationId);
            if (observerToken.IsFailure)
            {
                return Result.Failure(observerToken.Error);
            }

            Result<TokenVisionSettingsRecord> observerVision = visionRepository.GetVisionSettings(request.Campaign, request.ObserverTokenId, request.CorrelationId);
            if (observerVision.IsFailure)
            {
                return Result.Failure(observerVision.Error);
            }

            double centerX = observerToken.Value.Position.X;
            double centerY = observerToken.Value.Position.Y;
            double radius = observerVision.Value.ViewDistance;
            UserId userId = observerToken.Value.ControllerUserId;
            SceneId sceneId = observerToken.Value.SceneId;

            Result<IReadOnlyList<FogRevealRecord>> existing = fogRepository.ListReveals(request.Campaign, sceneId, userId, request.CorrelationId);
            if (existing.IsFailure)
            {
                return Result.Failure(existing.Error);
            }

            foreach (FogRevealRecord reveal in existing.Value)
            {
                if (IsFullyCovered(centerX, centerY, radius, reveal.CenterX, reveal.CenterY, reveal.Radius))
                {
                    // Already covered by an existing reveal of the same user -- no new row, per the INSERT-only/no-duplicate-coverage rule.
                    return Result.Success();
                }
            }

            Result<FogRevealRecord> inserted = fogRepository.RecordReveal(request.Campaign, sceneId, userId, centerX, centerY, radius, request.CommandId, request.CorrelationId);
            return inserted.IsSuccess ? Result.Success() : Result.Failure(inserted.Error);
        }

        public static Result<bool> IsPointExplored(IFogOfWarRepository fogRepository, ICampaignRepository campaignRepository, IsPointExploredRequest request)
        {
            if (fogRepository == null) throw new ArgumentNullException(nameof(fogRepository));
            if (campaignRepository == null) throw new ArgumentNullException(nameof(campaignRepository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            Result selfScoped = CheckSelfScopedOrMainGm(campaignRepository, request.Campaign, request.RequestingUserId, request.TargetUserId, request.CorrelationId);
            if (selfScoped.IsFailure)
            {
                return Result<bool>.Failure(selfScoped.Error);
            }

            Result<bool> targetIsMainGm = CampaignMembershipAuthorization.IsMainGm(campaignRepository, request.Campaign, request.TargetUserId, request.CorrelationId);
            if (targetIsMainGm.IsFailure)
            {
                return Result<bool>.Failure(targetIsMainGm.Error);
            }

            if (targetIsMainGm.Value)
            {
                return Result<bool>.Success(true);
            }

            Result<IReadOnlyList<FogRevealRecord>> existing = fogRepository.ListReveals(request.Campaign, request.SceneId, request.TargetUserId, request.CorrelationId);
            if (existing.IsFailure)
            {
                return Result<bool>.Failure(existing.Error);
            }

            foreach (FogRevealRecord reveal in existing.Value)
            {
                double distance = BoardGeometry.EuclideanDistance(request.X, request.Y, reveal.CenterX, reveal.CenterY);
                if (distance <= reveal.Radius + BoardGeometry.GeometryEpsilonV1)
                {
                    return Result<bool>.Success(true);
                }
            }

            return Result<bool>.Success(false);
        }

        /// <summary>True when the circle (<paramref name="newCenterX"/>,<paramref name="newCenterY"/>,<paramref name="newRadius"/>) is fully contained within (<paramref name="existingCenterX"/>,<paramref name="existingCenterY"/>,<paramref name="existingRadius"/>) -- the standard circle-containment test (distance between centers plus the new radius does not exceed the existing radius), not a full union/merge of the two circles into a combined shape.</summary>
        private static bool IsFullyCovered(double newCenterX, double newCenterY, double newRadius, double existingCenterX, double existingCenterY, double existingRadius)
        {
            double distance = BoardGeometry.EuclideanDistance(newCenterX, newCenterY, existingCenterX, existingCenterY);
            return distance <= existingRadius - newRadius + BoardGeometry.GeometryEpsilonV1;
        }

        // First "self-scoped" authorization check in this codebase: a plain user may only ask about themselves; a registered MainGm may ask about anyone. Fail-closed on a lookup failure, by exact precedent of CampaignMembershipAuthorization.IsMainGm's own contract.
        private static Result CheckSelfScopedOrMainGm(ICampaignRepository campaignRepository, CampaignHandle campaign, UserId requestingUserId, UserId targetUserId, CorrelationId correlationId)
        {
            if (requestingUserId.Equals(targetUserId))
            {
                return Result.Success();
            }

            Result<bool> isMainGm = CampaignMembershipAuthorization.IsMainGm(campaignRepository, campaign, requestingUserId, correlationId);
            return isMainGm.IsFailure ? Result.Failure(isMainGm.Error) : isMainGm.Value ? Result.Success() : Result.Failure(FogOfWarFailures.TargetUserDenied(correlationId));
        }
    }

    public sealed class ComputeVisibleTokensRequest
    {
        public ComputeVisibleTokensRequest(CampaignHandle campaign, SceneId sceneId, UserId requestingUserId, UserId targetUserId, CorrelationId correlationId)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));
            if (!requestingUserId.IsValid) throw new ArgumentException("RequestingUserId is required.", nameof(requestingUserId));
            if (!targetUserId.IsValid) throw new ArgumentException("TargetUserId is required.", nameof(targetUserId));

            SceneId = sceneId;
            RequestingUserId = requestingUserId;
            TargetUserId = targetUserId;
            CorrelationId = correlationId;
        }

        public CampaignHandle Campaign { get; }
        public SceneId SceneId { get; }
        public UserId RequestingUserId { get; }
        public UserId TargetUserId { get; }
        public CorrelationId CorrelationId { get; }
    }

    /// <summary>No SceneId field: the observer's Scene is derived from the token itself, by exact precedent of Block 3's own <c>ComputeLineOfSightRequest</c> (which likewise derives Scene from the token records it reads, not a separately supplied parameter).</summary>
    public sealed class RecordExplorationRequest
    {
        public RecordExplorationRequest(CampaignHandle campaign, TokenId observerTokenId, CommandId commandId, CorrelationId correlationId)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            if (!observerTokenId.IsValid) throw new ArgumentException("ObserverTokenId is required.", nameof(observerTokenId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            ObserverTokenId = observerTokenId;
            CommandId = commandId;
            CorrelationId = correlationId;
        }

        public CampaignHandle Campaign { get; }
        public TokenId ObserverTokenId { get; }
        public CommandId CommandId { get; }
        public CorrelationId CorrelationId { get; }
    }

    public sealed class IsPointExploredRequest
    {
        public IsPointExploredRequest(CampaignHandle campaign, SceneId sceneId, double x, double y, UserId requestingUserId, UserId targetUserId, CorrelationId correlationId)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));
            if (!double.IsFinite(x) || !double.IsFinite(y)) throw new ArgumentException("X/Y must be finite.");
            if (!requestingUserId.IsValid) throw new ArgumentException("RequestingUserId is required.", nameof(requestingUserId));
            if (!targetUserId.IsValid) throw new ArgumentException("TargetUserId is required.", nameof(targetUserId));

            SceneId = sceneId;
            X = x;
            Y = y;
            RequestingUserId = requestingUserId;
            TargetUserId = targetUserId;
            CorrelationId = correlationId;
        }

        public CampaignHandle Campaign { get; }
        public SceneId SceneId { get; }
        public double X { get; }
        public double Y { get; }
        public UserId RequestingUserId { get; }
        public UserId TargetUserId { get; }
        public CorrelationId CorrelationId { get; }
    }
}
