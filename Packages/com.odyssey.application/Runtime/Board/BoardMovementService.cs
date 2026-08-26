using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Geometry;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Board
{
    /// <summary>
    /// ODY-S03-004: host-authoritative token movement over a real, durable
    /// <see cref="ISceneRepository"/> -- the persisted-campaign counterpart of
    /// ODY-S02-011's in-memory <c>MoveTokenService</c> (network prototype).
    /// Deliberately not a reuse of that class: it operates on
    /// <c>SceneMutableState</c>, an in-memory, non-durable stand-in ODY-S02-011
    /// itself documented as appropriate only because that prototype had no
    /// persisted campaign to write through -- the opposite of this task's own
    /// premise (see task contract section 3 for the full comparison against
    /// both ODY-S02-011 and <c>SqliteSceneRepository</c>).
    ///
    /// Follows ADR-002 section 11's ordering, narrowed to what this task
    /// needs: submission-time authorization check -&gt; destination validation
    /// (ADR-020) -&gt; occupancy check (BOARD-INV-009) -&gt; pre-commit
    /// authorization check (ADR-019 section 6.1's two-point rule) -&gt;
    /// <see cref="ISceneRepository.MoveToken"/>, which re-validates
    /// <c>expectedRevision</c> atomically inside its own transaction as the
    /// final concurrency guard (ADR-002 section 10.2). CommandId-keyed
    /// idempotency/replay is <see cref="ISceneRepository.MoveToken"/>'s own
    /// concern (ADR-012 section 7 via <c>SqliteSavingPipeline</c>), not
    /// duplicated here.
    /// </summary>
    public static class BoardMovementService
    {
        public static Result<TokenRecord> MoveToken(ISceneRepository repository, MoveTokenRequest request)
        {
            if (repository == null) throw new System.ArgumentNullException(nameof(repository));
            if (request == null) throw new System.ArgumentNullException(nameof(request));

            if (!BoardGeometry.IsFinite(request.Destination.X, request.Destination.Y))
            {
                return Result<TokenRecord>.Failure(BoardFailures.InvalidDestination(request.CorrelationId));
            }

            Result<TokenRecord> current = repository.GetToken(request.Campaign, request.TokenId, request.CorrelationId);
            if (current.IsFailure)
            {
                return current;
            }

            TokenRecord token = current.Value;

            // Submission-time authorization check (ADR-019 section 6.1's first point).
            Result submissionCheck = CheckAuthorization(token, request);
            if (submissionCheck.IsFailure)
            {
                return Result<TokenRecord>.Failure(submissionCheck.Error);
            }

            Result occupancyCheck = CheckOccupancy(repository, request, token.SceneId);
            if (occupancyCheck.IsFailure)
            {
                return Result<TokenRecord>.Failure(occupancyCheck.Error);
            }

            // Pre-commit authorization check (ADR-019 section 6.1's second
            // point) -- repeated independently, the same structural discipline
            // ODY-S02-011's MoveTokenService already established, even though
            // this synchronous call has no real intervening concurrency window
            // of its own; the durable ExpectedRevision check inside
            // repository.MoveToken is the actual concurrency guard.
            Result preCommitCheck = CheckAuthorization(token, request);
            if (preCommitCheck.IsFailure)
            {
                return Result<TokenRecord>.Failure(preCommitCheck.Error);
            }

            return repository.MoveToken(request.Campaign, request.TokenId, request.Destination, request.ExpectedRevision, request.CommandId, request.CorrelationId);
        }

        /// <summary>
        /// 08_Scenes_And_Board section 21.5: Undo is a new compensating
        /// command with fresh permission/revision validation, not a rollback
        /// that bypasses the pipeline -- so this is not a distinct mechanism,
        /// it is <see cref="MoveToken"/> called again with the caller-supplied
        /// restore position and a fresh <see cref="CommandId"/>. If the actor
        /// lost control of the token, or someone else moved it, since the
        /// original move, this call is rejected exactly as any other
        /// unauthorized/stale-revision move would be (BOARD-INV-030: committed
        /// events are never deleted, only compensated).
        /// </summary>
        public static Result<TokenRecord> UndoMoveToken(ISceneRepository repository, MoveTokenRequest undoRequest) => MoveToken(repository, undoRequest);

        private static Result CheckAuthorization(TokenRecord token, MoveTokenRequest request)
        {
            if (request.ActorIsMainGm)
            {
                return Result.Success();
            }

            return token.ControllerUserId.Equals(request.ActorUserId)
                ? Result.Success()
                : Result.Failure(BoardFailures.MoveDenied(request.CorrelationId));
        }

        private static Result CheckOccupancy(ISceneRepository repository, MoveTokenRequest request, SceneId sceneId)
        {
            Result<IReadOnlyList<TokenRecord>> others = repository.ListTokens(request.Campaign, sceneId, request.CorrelationId);
            if (others.IsFailure)
            {
                return Result.Failure(others.Error);
            }

            foreach (TokenRecord other in others.Value)
            {
                if (other.TokenId.Equals(request.TokenId))
                {
                    continue;
                }

                if (BoardGeometry.SamePosition(other.Position.X, other.Position.Y, request.Destination.X, request.Destination.Y))
                {
                    return Result.Failure(BoardFailures.DestinationOccupied(request.CorrelationId));
                }
            }

            return Result.Success();
        }
    }
}
