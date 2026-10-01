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
    /// SLICE-10 Block 5: a graduated cover *hint* over Block 2/3's own obstacle/geometry data --
    /// product decision (2026-09-29): a suggestion the caller (UI, or a separate decision) may act
    /// on, computed independently of this method's own return value. This method itself is not
    /// touched by, and does not call into, `CoreAttackRulesEvaluator`/`AttackEvaluationService`/
    /// `AttackApplyService` -- but those are no longer untouched by cover in general: SLICE-10 Block
    /// 5 Part C (ODY-S10-110 v3) wired the same `CoverGeometry.ComputeCoverDegree` this method also
    /// calls directly into `CoreAttackRulesEvaluator`'s own `CoverPenaltyTable`, so a real attack's
    /// `Modifiers`/`Hit` today can and do reflect cover -- computed there via `SqliteAttackStateReader`,
    /// not through a call to this method.
    ///
    /// ODY-S11-222 (security): visibility-checked. A cover answer reveals where a token is relative to the
    /// obstacles, so a requester who is not the campaign's registered MainGm only gets an answer when BOTH tokens
    /// are in their own live visible set -- the same <see cref="PlayerVisibilityService.ComputeVisibleTokens"/> rule
    /// that hides tokens from players everywhere else (reused, not re-implemented). For such a requester a token
    /// that is hidden, does not exist, or sits on another Scene is refused with one and the same
    /// <see cref="CoverSuggestionFailures.TokenUnavailable"/> error, checked before anything else about the pair,
    /// so the refusal cannot be used to probe which token ids exist or where they are. The MainGm is unrestricted,
    /// exactly as before.
    /// </summary>
    public static class CoverSuggestionService
    {
        public static Result<CoverDegree> SuggestCover(ISceneRepository sceneRepository, IObstacleRepository obstacleRepository, ITokenVisionRepository visionRepository, ICampaignRepository campaignRepository, SuggestCoverRequest request)
        {
            if (sceneRepository == null) throw new ArgumentNullException(nameof(sceneRepository));
            if (obstacleRepository == null) throw new ArgumentNullException(nameof(obstacleRepository));
            if (visionRepository == null) throw new ArgumentNullException(nameof(visionRepository));
            if (campaignRepository == null) throw new ArgumentNullException(nameof(campaignRepository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            Result<bool> requesterIsMainGm = CampaignMembershipAuthorization.IsMainGm(campaignRepository, request.Campaign, request.RequestingUserId, request.CorrelationId);
            if (requesterIsMainGm.IsFailure)
            {
                return Result<CoverDegree>.Failure(requesterIsMainGm.Error);
            }

            bool restricted = !requesterIsMainGm.Value;

            Result<TokenRecord> attackerToken = sceneRepository.GetToken(request.Campaign, request.AttackerTokenId, request.CorrelationId);
            if (attackerToken.IsFailure)
            {
                return Result<CoverDegree>.Failure(restricted && attackerToken.Error.Category == ErrorCategory.NotFound ? CoverSuggestionFailures.TokenUnavailable(request.CorrelationId) : attackerToken.Error);
            }

            Result<TokenRecord> targetToken = sceneRepository.GetToken(request.Campaign, request.TargetTokenId, request.CorrelationId);
            if (targetToken.IsFailure)
            {
                return Result<CoverDegree>.Failure(restricted && targetToken.Error.Category == ErrorCategory.NotFound ? CoverSuggestionFailures.TokenUnavailable(request.CorrelationId) : targetToken.Error);
            }

            if (restricted)
            {
                // Visibility is decided on the attacker's Scene: a target on another Scene is simply not visible there,
                // so it gets the same refusal as a hidden one (no "different scene" answer that would confirm it exists).
                var visibilityRequest = new ComputeVisibleTokensRequest(request.Campaign, attackerToken.Value.SceneId, request.RequestingUserId, request.RequestingUserId, request.CorrelationId);
                Result<IReadOnlyCollection<TokenId>> visible = PlayerVisibilityService.ComputeVisibleTokens(sceneRepository, visionRepository, obstacleRepository, campaignRepository, visibilityRequest);
                if (visible.IsFailure)
                {
                    return Result<CoverDegree>.Failure(visible.Error);
                }

                if (!Contains(visible.Value, request.AttackerTokenId) || !Contains(visible.Value, request.TargetTokenId))
                {
                    return Result<CoverDegree>.Failure(CoverSuggestionFailures.TokenUnavailable(request.CorrelationId));
                }
            }

            if (!attackerToken.Value.SceneId.Equals(targetToken.Value.SceneId))
            {
                return Result<CoverDegree>.Failure(CoverSuggestionFailures.AttackerAndTargetNotInSameScene(request.CorrelationId));
            }

            Result<IReadOnlyList<ObstacleRecord>> obstacles = obstacleRepository.ListObstacles(request.Campaign, attackerToken.Value.SceneId, request.CorrelationId);
            if (obstacles.IsFailure)
            {
                return Result<CoverDegree>.Failure(obstacles.Error);
            }

            var segments = new ObstacleSegment[obstacles.Value.Count];
            for (int index = 0; index < obstacles.Value.Count; index++)
            {
                ObstacleRecord obstacle = obstacles.Value[index];
                segments[index] = new ObstacleSegment(obstacle.Kind, obstacle.IsOpen, obstacle.X1, obstacle.Y1, obstacle.X2, obstacle.Y2);
            }

            CoverDegree degree = CoverGeometry.ComputeCoverDegree(
                attackerToken.Value.Position.X, attackerToken.Value.Position.Y,
                targetToken.Value.Position.X, targetToken.Value.Position.Y,
                segments);

            return Result<CoverDegree>.Success(degree);
        }

        private static bool Contains(IReadOnlyCollection<TokenId> tokenIds, TokenId tokenId)
        {
            foreach (TokenId candidate in tokenIds)
            {
                if (candidate.Equals(tokenId)) return true;
            }

            return false;
        }
    }

    public sealed class SuggestCoverRequest
    {
        public SuggestCoverRequest(CampaignHandle campaign, UserId requestingUserId, TokenId attackerTokenId, TokenId targetTokenId, CorrelationId correlationId)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            if (!requestingUserId.IsValid) throw new ArgumentException("RequestingUserId is required.", nameof(requestingUserId));
            if (!attackerTokenId.IsValid) throw new ArgumentException("AttackerTokenId is required.", nameof(attackerTokenId));
            if (!targetTokenId.IsValid) throw new ArgumentException("TargetTokenId is required.", nameof(targetTokenId));

            RequestingUserId = requestingUserId;
            AttackerTokenId = attackerTokenId;
            TargetTokenId = targetTokenId;
            CorrelationId = correlationId;
        }

        public CampaignHandle Campaign { get; }

        /// <summary>ODY-S11-222: who asks -- decides whether the answer is limited to tokens they can see.</summary>
        public UserId RequestingUserId { get; }
        public TokenId AttackerTokenId { get; }
        public TokenId TargetTokenId { get; }
        public CorrelationId CorrelationId { get; }
    }

    public static class CoverSuggestionFailures
    {
        /// <summary>
        /// ODY-S11-222 (security): a non-MainGm requester asked about a token they cannot see -- hidden from them, not on
        /// the attacker's Scene, or not existing at all. One error for all three on purpose, so the refusal reveals
        /// neither existence nor position.
        /// </summary>
        public static Error TokenUnavailable(CorrelationId correlationId) => Error.Create(
            ErrorCodes.CoverSuggestionTokenUnavailable,
            ErrorCategory.NotFound,
            SafeReasonCode.TargetUnavailable,
            UserMessageKey.Parse("errors.cover_suggestion.token_unavailable"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>`CoverSuggestionService.SuggestCover`: attacker and target tokens are not on the same Scene -- a cover hint across two different maps has no meaning, by exact precedent of `TokenVisionFailures.ObserverAndTargetNotInSameScene`.</summary>
        public static Error AttackerAndTargetNotInSameScene(CorrelationId correlationId) => Error.Create(
            ErrorCodes.CoverSuggestionAttackerAndTargetNotInSameScene,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.cover_suggestion.attacker_and_target_not_in_same_scene"),
            RetryDirective.DoNotRetry,
            correlationId);
    }
}
