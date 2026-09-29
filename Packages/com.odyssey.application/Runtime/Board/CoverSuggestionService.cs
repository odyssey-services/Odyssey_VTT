using System;
using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Geometry;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Board
{
    /// <summary>
    /// SLICE-10 Block 5: a graduated cover *hint* over Block 2/3's own obstacle/geometry data --
    /// product decision (2026-09-29): a suggestion the caller (UI, or a future, separate decision)
    /// may act on, not an automatic combat modifier. `CoreAttackRulesEvaluator`/`AttackEvaluationService`/
    /// `AttackApplyService` are not touched by this task, at all -- <c>Modifiers</c> stays
    /// <c>Array.Empty&lt;AttackModifierEntry&gt;()</c> and <c>Hit</c> stays exactly `Range`, unchanged.
    ///
    /// Performs no authorization at all, by exact precedent of <c>ObstacleAuthoringService.ListObstacles</c>/
    /// <c>TokenVisionService.ComputeLineOfSight</c>: reading scene geometry to compute a cover hint is
    /// no more sensitive than reading it to compute line of sight, both already open to any caller.
    /// </summary>
    public static class CoverSuggestionService
    {
        public static Result<CoverDegree> SuggestCover(ISceneRepository sceneRepository, IObstacleRepository obstacleRepository, SuggestCoverRequest request)
        {
            if (sceneRepository == null) throw new ArgumentNullException(nameof(sceneRepository));
            if (obstacleRepository == null) throw new ArgumentNullException(nameof(obstacleRepository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            Result<TokenRecord> attackerToken = sceneRepository.GetToken(request.Campaign, request.AttackerTokenId, request.CorrelationId);
            if (attackerToken.IsFailure)
            {
                return Result<CoverDegree>.Failure(attackerToken.Error);
            }

            Result<TokenRecord> targetToken = sceneRepository.GetToken(request.Campaign, request.TargetTokenId, request.CorrelationId);
            if (targetToken.IsFailure)
            {
                return Result<CoverDegree>.Failure(targetToken.Error);
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
    }

    public sealed class SuggestCoverRequest
    {
        public SuggestCoverRequest(CampaignHandle campaign, TokenId attackerTokenId, TokenId targetTokenId, CorrelationId correlationId)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            if (!attackerTokenId.IsValid) throw new ArgumentException("AttackerTokenId is required.", nameof(attackerTokenId));
            if (!targetTokenId.IsValid) throw new ArgumentException("TargetTokenId is required.", nameof(targetTokenId));

            AttackerTokenId = attackerTokenId;
            TargetTokenId = targetTokenId;
            CorrelationId = correlationId;
        }

        public CampaignHandle Campaign { get; }
        public TokenId AttackerTokenId { get; }
        public TokenId TargetTokenId { get; }
        public CorrelationId CorrelationId { get; }
    }

    public static class CoverSuggestionFailures
    {
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
