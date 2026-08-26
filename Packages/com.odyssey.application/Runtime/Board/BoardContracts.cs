using System;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Board
{
    /// <summary>
    /// Roadmap section 12.6 step 1 ("Player selects own token") plus exit
    /// criteria 2/7 (08_Scenes_And_Board section 12.4's validation pipeline,
    /// narrowed to points 2, 6, 7, 12 -- this task's actual scope, not the full
    /// 13-point list: membership/scene/lock/traversal are SLICE-02/future
    /// concerns not reopened here). A move request is a persisted-campaign
    /// authoritative command over <see cref="ISceneRepository"/>, following the
    /// same shape ADR-002 section 26's <c>board.token.move v1</c> example and
    /// ODY-S02-011's in-memory <c>MoveTokenCommand</c> already established --
    /// caller-supplied <see cref="CommandId"/>, actor identity, expected
    /// revision -- adapted here to a real, durable <see cref="ISceneRepository"/>
    /// backing instead of an in-memory session.
    /// </summary>
    public sealed class MoveTokenRequest
    {
        public MoveTokenRequest(CampaignHandle campaign, UserId actorUserId, bool actorIsMainGm, TokenId tokenId, TokenPosition destination, long expectedRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (!tokenId.IsValid) throw new ArgumentException("TokenId is required.", nameof(tokenId));
            if (expectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            Campaign = campaign;
            ActorUserId = actorUserId;
            ActorIsMainGm = actorIsMainGm;
            TokenId = tokenId;
            Destination = destination;
            ExpectedRevision = expectedRevision;
            CommandId = commandId;
            CorrelationId = correlationId;
        }

        public CampaignHandle Campaign { get; }
        public UserId ActorUserId { get; }

        /// <summary>
        /// ODY-S03-004's deliberate simplification (see task contract section
        /// 3): this task has no session/role model of its own (that remains
        /// ADR-019/SLICE-02 scope, not reopened here) -- the caller supplies
        /// whether the actor holds the MainGM baseline role, the same
        /// information ODY-S02-011's <c>SessionAdmissionState</c> would
        /// eventually provide in a networked context.
        /// </summary>
        public bool ActorIsMainGm { get; }
        public TokenId TokenId { get; }
        public TokenPosition Destination { get; }
        public long ExpectedRevision { get; }
        public CommandId CommandId { get; }
        public CorrelationId CorrelationId { get; }
    }

    public static class BoardFailures
    {
        /// <summary>
        /// BOARD-INV-027 (08_Scenes_And_Board section 3): a user without
        /// Object.Move control cannot get authoritative movement. Not
        /// distinguished from "token not found" in a way that would leak
        /// existence of a hidden token -- callers that already know the
        /// token exists (this task has no hidden-token/fog model yet) use
        /// this error directly.
        /// </summary>
        public static Error MoveDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.BoardTokenMoveDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.board.token_move_denied"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>
        /// ADR-020 section 4.2/08_Scenes_And_Board section 6.1: a destination
        /// with a non-finite (NaN/Infinity) coordinate is rejected before any
        /// persistence call -- 08_Scenes_And_Board section 24.5's "Invalid
        /// geometry отклоняется до Commit" principle.
        /// </summary>
        public static Error InvalidDestination(CorrelationId correlationId) => Error.Create(
            ErrorCodes.BoardTokenDestinationInvalid,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.board.token_destination_invalid"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>
        /// BOARD-INV-009 (08_Scenes_And_Board section 3): committed token
        /// footprints do not overlap -- interpreted here (no footprint/grid-
        /// cell model yet, see <c>Odyssey.Domain.Geometry.BoardGeometry</c>'s
        /// own doc comment) as "another token already occupies this exact
        /// world position." Matches 08_Scenes_And_Board section 24.1's
        /// <c>DestinationOccupied</c> safe code name.
        /// </summary>
        public static Error DestinationOccupied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.BoardTokenDestinationOccupied,
            ErrorCategory.Conflict,
            SafeReasonCode.ActionNotAllowed,
            UserMessageKey.Parse("errors.board.token_destination_occupied"),
            RetryDirective.DoNotRetry,
            correlationId);
    }
}
