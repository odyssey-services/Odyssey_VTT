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

        /// <summary>
        /// SLICE-10 Block 6 part 2: the bulk-read counterpart of <see cref="IsPointExplored"/> -- every
        /// reveal ever recorded for the target user in the given Scene, not one point at a time. Exists
        /// only so a UI can render a whole fog-of-war overlay (sampling the board point-by-point until it
        /// looks smooth is not workable); the raw <see cref="IFogOfWarRepository.ListReveals"/> port
        /// performs no authorization of its own (by design -- it is a storage port, not a decision-maker,
        /// exactly like every other repository in this track), so a caller reaching it directly would skip
        /// the self-scoped-or-MainGm check entirely. This method is the only sanctioned path: the exact
        /// same <see cref="CheckSelfScopedOrMainGm"/>/<see cref="CampaignMembershipAuthorization.IsMainGm"/>
        /// gate every other read in this class already uses, then a thin pass-through to
        /// <see cref="IFogOfWarRepository.ListReveals"/>. A MainGm target's reveals are read and returned
        /// as-is (not synthesized/short-circuited to "everything") -- unlike <see cref="IsPointExplored"/>'s
        /// own <c>true</c> shortcut, there is no single "the whole Scene, unconditionally" circle to
        /// substitute for an arbitrary bulk read; the caller (this task's own client code) is expected to
        /// simply not call this method at all for a MainGm viewer, the same way it already skips
        /// <see cref="ComputeVisibleTokens"/> for one.
        /// </summary>
        public static Result<IReadOnlyList<FogRevealRecord>> ListExploredReveals(IFogOfWarRepository fogRepository, ICampaignRepository campaignRepository, ListExploredRevealsRequest request)
        {
            if (fogRepository == null) throw new ArgumentNullException(nameof(fogRepository));
            if (campaignRepository == null) throw new ArgumentNullException(nameof(campaignRepository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            Result selfScoped = CheckSelfScopedOrMainGm(campaignRepository, request.Campaign, request.RequestingUserId, request.TargetUserId, request.CorrelationId);
            if (selfScoped.IsFailure)
            {
                return Result<IReadOnlyList<FogRevealRecord>>.Failure(selfScoped.Error);
            }

            return fogRepository.ListReveals(request.Campaign, request.SceneId, request.TargetUserId, request.CorrelationId);
        }

        /// <summary>
        /// SLICE-10 Block 6 follow-up (ODY-S10-115): the same fog-of-war filtering
        /// <see cref="ComputeVisibleTokens"/> already applies to tokens, applied to obstacle geometry --
        /// closing a gap found at Block 6's own close-out (the client previously drew every obstacle
        /// unconditionally, relying only on the fog overlay's own 82%-opacity darkening to hide an
        /// unexplored door's existence/open-closed state, which a determined player could still read
        /// through the translucency). <see cref="IObstacleRepository.ListObstacles"/> itself is
        /// deliberately left untouched and unfiltered -- <c>TokenVisionService.ComputeLineOfSight</c>/
        /// <c>CoverSuggestionService.SuggestCover</c> both need the true, complete obstacle set
        /// regardless of any one player's own map memory (an obstacle blocks vision/gives cover whether
        /// or not that specific player has ever seen it), so this filtering exists only as a second,
        /// additive read for a client's own render pass, exactly the same relationship
        /// <see cref="ComputeVisibleTokens"/> already has with the unfiltered <c>ISceneRepository.ListTokens</c>.
        ///
        /// A MainGm target bypasses filtering entirely, receiving the same unfiltered
        /// <see cref="IObstacleRepository.ListObstacles"/> result as any other internal caller -- the
        /// same "MainGm sees everything, unconditionally" principle <see cref="ComputeVisibleTokens"/>/
        /// <see cref="IsPointExplored"/> already establish. For everyone else, an obstacle is "known" if
        /// any of three sample points along its segment -- both endpoints and the midpoint -- falls
        /// within any of the target's own explored reveal circles (reusing <see cref="ListExploredReveals"/>
        /// rather than a second, duplicate read of <see cref="IFogOfWarRepository.ListReveals"/> --
        /// the same authorization-hole lesson <see cref="ListExploredReveals"/> itself was introduced to
        /// close). Three points along a segment is this task's own sampling decision, by direct analogy
        /// to <c>CoverGeometry</c>'s own four-sample-point technique for a target's cover degree -- not a
        /// reuse of that specific code, just the same "a few representative points, not a full boundary
        /// scan" shape.
        ///
        /// Deliberate simplification, disclosed here as an accepted design decision rather than a
        /// missed requirement: the obstacle's *current, live* state (including <see cref="ObstacleRecord.IsOpen"/>)
        /// is returned, not a snapshot frozen at the moment any sample point first entered explored
        /// territory. A player who has ever seen a door will see its real-time state even while it is
        /// currently out of that player's own live sight -- this is not the same kind of persistent
        /// memory <see cref="FogRevealRecord"/> itself provides for terrain (which never changes once
        /// explored); a "frozen at last observation" model would need obstacle-specific memory storage
        /// this task does not add. A future task's own scope if ever needed.
        /// </summary>
        public static Result<IReadOnlyList<ObstacleRecord>> ListExploredObstacles(IObstacleRepository obstacleRepository, IFogOfWarRepository fogRepository, ICampaignRepository campaignRepository, ListExploredObstaclesRequest request)
        {
            if (obstacleRepository == null) throw new ArgumentNullException(nameof(obstacleRepository));
            if (fogRepository == null) throw new ArgumentNullException(nameof(fogRepository));
            if (campaignRepository == null) throw new ArgumentNullException(nameof(campaignRepository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            Result selfScoped = CheckSelfScopedOrMainGm(campaignRepository, request.Campaign, request.RequestingUserId, request.TargetUserId, request.CorrelationId);
            if (selfScoped.IsFailure)
            {
                return Result<IReadOnlyList<ObstacleRecord>>.Failure(selfScoped.Error);
            }

            Result<IReadOnlyList<ObstacleRecord>> allObstacles = obstacleRepository.ListObstacles(request.Campaign, request.SceneId, request.CorrelationId);
            if (allObstacles.IsFailure)
            {
                return allObstacles;
            }

            Result<bool> targetIsMainGm = CampaignMembershipAuthorization.IsMainGm(campaignRepository, request.Campaign, request.TargetUserId, request.CorrelationId);
            if (targetIsMainGm.IsFailure)
            {
                return Result<IReadOnlyList<ObstacleRecord>>.Failure(targetIsMainGm.Error);
            }

            if (targetIsMainGm.Value)
            {
                return allObstacles;
            }

            var revealsRequest = new ListExploredRevealsRequest(request.Campaign, request.SceneId, request.TargetUserId, request.TargetUserId, request.CorrelationId);
            Result<IReadOnlyList<FogRevealRecord>> reveals = ListExploredReveals(fogRepository, campaignRepository, revealsRequest);
            if (reveals.IsFailure)
            {
                return Result<IReadOnlyList<ObstacleRecord>>.Failure(reveals.Error);
            }

            var known = new List<ObstacleRecord>();
            foreach (ObstacleRecord obstacle in allObstacles.Value)
            {
                if (IsAnySamplePointExplored(obstacle, reveals.Value))
                {
                    known.Add(obstacle);
                }
            }

            return Result<IReadOnlyList<ObstacleRecord>>.Success(known);
        }

        private static bool IsAnySamplePointExplored(ObstacleRecord obstacle, IReadOnlyList<FogRevealRecord> reveals)
        {
            double midX = (obstacle.X1 + obstacle.X2) / 2.0;
            double midY = (obstacle.Y1 + obstacle.Y2) / 2.0;

            return IsPointWithinAnyReveal(obstacle.X1, obstacle.Y1, reveals)
                || IsPointWithinAnyReveal(obstacle.X2, obstacle.Y2, reveals)
                || IsPointWithinAnyReveal(midX, midY, reveals);
        }

        private static bool IsPointWithinAnyReveal(double x, double y, IReadOnlyList<FogRevealRecord> reveals)
        {
            foreach (FogRevealRecord reveal in reveals)
            {
                double distance = BoardGeometry.EuclideanDistance(x, y, reveal.CenterX, reveal.CenterY);
                if (distance <= reveal.Radius + BoardGeometry.GeometryEpsilonV1)
                {
                    return true;
                }
            }

            return false;
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

    /// <summary>Same shape as <see cref="IsPointExploredRequest"/>, minus the single point -- a bulk read over a whole Scene rather than a single-point check.</summary>
    public sealed class ListExploredRevealsRequest
    {
        public ListExploredRevealsRequest(CampaignHandle campaign, SceneId sceneId, UserId requestingUserId, UserId targetUserId, CorrelationId correlationId)
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

    /// <summary>Same shape as <see cref="ListExploredRevealsRequest"/> -- a bulk, self-scoped-or-MainGm-gated read, this time over obstacle geometry instead of fog reveals.</summary>
    public sealed class ListExploredObstaclesRequest
    {
        public ListExploredObstaclesRequest(CampaignHandle campaign, SceneId sceneId, UserId requestingUserId, UserId targetUserId, CorrelationId correlationId)
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
}
