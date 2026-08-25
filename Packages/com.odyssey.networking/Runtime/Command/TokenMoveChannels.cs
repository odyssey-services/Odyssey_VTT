using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Odyssey.Application.Commands;
using Odyssey.Application.Networking;
using Odyssey.Application.Networking.Command;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;

namespace Odyssey.Networking.Command
{
    /// <summary>
    /// ODY-S02-011: the real (not test-only) Networking-layer adapter that
    /// carries move-token requests/outcomes/deltas
    /// (Odyssey.Application.Networking.Command) over ISessionTransport's
    /// reliable channel -- ADR-015 section 5.1, ADR-001 section 6.6. Never
    /// makes an authorization or visibility decision itself: the host handler
    /// below only encodes/sends what MoveTokenService/DeltaBroadcastPlanner
    /// already decided.
    /// </summary>
    public static class TokenMoveClientChannel
    {
        public static async Task<Result> SendMoveRequestAsync(ISessionTransport transport, ConnectionHandle handle, MoveTokenCommand command, IWallClock clock, CancellationToken cancellationToken)
        {
            MoveTokenRequestMessage message = new MoveTokenRequestMessage(
                command.CommandId.ToString(), command.SessionId.ToString(), command.ActorUserId.ToString(), command.EntityId,
                command.Destination.X.ToString("R", CultureInfo.InvariantCulture), command.Destination.Y.ToString("R", CultureInfo.InvariantCulture),
                command.ExpectedRevision);
            Result<byte[]> payload = TokenMoveWireCodec.WriteMoveRequest(message);
            if (payload.IsFailure) return Result.Failure(payload.Error);
            NetworkEnvelope envelope = MakeEnvelope(handle, "odyssey.command.move_token_request", payload.Value, clock);
            return await transport.SendReliableAsync(handle, envelope, cancellationToken);
        }

        public static Result<IReadOnlyList<MoveTokenOutcomeMessage>> DrainOutcomes(ISessionTransport transport, ConnectionHandle handle)
        {
            Result<IReadOnlyList<NetworkEnvelope>> drained = transport.DrainReliable(handle);
            if (drained.IsFailure) return Result<IReadOnlyList<MoveTokenOutcomeMessage>>.Failure(drained.Error);

            List<MoveTokenOutcomeMessage> outcomes = new List<MoveTokenOutcomeMessage>();
            foreach (NetworkEnvelope envelope in drained.Value)
            {
                if (envelope.PayloadType != "odyssey.command.move_token_outcome") continue;
                Result<MoveTokenOutcomeMessage> outcome = TokenMoveWireCodec.ReadOutcome(envelope.Payload);
                if (outcome.IsFailure) return Result<IReadOnlyList<MoveTokenOutcomeMessage>>.Failure(outcome.Error);
                outcomes.Add(outcome.Value);
            }

            return Result<IReadOnlyList<MoveTokenOutcomeMessage>>.Success(outcomes);
        }

        public static Result<IReadOnlyList<TokenMovedDeltaMessage>> DrainDeltas(ISessionTransport transport, ConnectionHandle handle)
        {
            Result<IReadOnlyList<NetworkEnvelope>> drained = transport.DrainReliable(handle);
            if (drained.IsFailure) return Result<IReadOnlyList<TokenMovedDeltaMessage>>.Failure(drained.Error);

            List<TokenMovedDeltaMessage> deltas = new List<TokenMovedDeltaMessage>();
            foreach (NetworkEnvelope envelope in drained.Value)
            {
                if (envelope.PayloadType != "odyssey.command.token_moved_delta") continue;
                Result<TokenMovedDeltaMessage> delta = TokenMoveWireCodec.ReadDelta(envelope.Payload);
                if (delta.IsFailure) return Result<IReadOnlyList<TokenMovedDeltaMessage>>.Failure(delta.Error);
                deltas.Add(delta.Value);
            }

            return Result<IReadOnlyList<TokenMovedDeltaMessage>>.Success(deltas);
        }

        private static NetworkEnvelope MakeEnvelope(ConnectionHandle handle, string payloadType, byte[] payload, IWallClock clock) =>
            new NetworkEnvelope(
                MessageId.NewId(clock.GetUtcNow()),
                handle.SessionId,
                senderUserId: null,
                senderClientInstanceId: null,
                NetworkMessageKind.ApplicationPayload,
                handle.NegotiatedProtocolVersion,
                correlationId: null,
                causationId: null,
                sentAtHostTime: clock.GetUtcNow(),
                payloadType,
                payloadVersion: 1,
                payload: payload);
    }

    public static class TokenMoveHostChannel
    {
        /// <summary>
        /// Drains every pending move-token request from one connection,
        /// validates it via MoveTokenService (the only place a move decision
        /// is made), and sends the typed outcome back to that same
        /// requester. Does not broadcast the resulting delta -- see
        /// <see cref="BroadcastDeltaAsync"/> for that, called separately by
        /// the caller once it has an accepted outcome and the full set of
        /// connections to consider.
        /// </summary>
        public static async Task<Result<IReadOnlyList<TokenMoveOutcome>>> ProcessPendingRequestsAsync(ISessionTransport transport, ConnectionHandle handle, TokenMoveSessionState state, SessionAdmissionState admission, IWallClock clock, CancellationToken cancellationToken)
        {
            Result<IReadOnlyList<NetworkEnvelope>> drained = transport.DrainReliable(handle);
            if (drained.IsFailure) return Result<IReadOnlyList<TokenMoveOutcome>>.Failure(drained.Error);

            List<TokenMoveOutcome> accepted = new List<TokenMoveOutcome>();
            foreach (NetworkEnvelope envelope in drained.Value)
            {
                if (envelope.PayloadType != "odyssey.command.move_token_request") continue;

                Result<MoveTokenRequestMessage> request = TokenMoveWireCodec.ReadMoveRequest(envelope.Payload);
                MoveTokenOutcomeMessage outcomeMessage;
                if (request.IsFailure)
                {
                    outcomeMessage = MoveTokenOutcomeMessage.FromFailure("cmd_00000000000000000000000000000000", request.Error);
                }
                else
                {
                    Result<TokenMoveOutcome> result = ExecuteFromMessage(state, admission, request.Value);
                    outcomeMessage = result.IsSuccess
                        ? MoveTokenOutcomeMessage.FromSuccess(request.Value.CommandId, result.Value)
                        : MoveTokenOutcomeMessage.FromFailure(request.Value.CommandId, result.Error);
                    if (result.IsSuccess) accepted.Add(result.Value);
                }

                Result<byte[]> encoded = TokenMoveWireCodec.WriteOutcome(outcomeMessage);
                if (encoded.IsFailure) return Result<IReadOnlyList<TokenMoveOutcome>>.Failure(encoded.Error);
                NetworkEnvelope response = MakeEnvelope(handle, "odyssey.command.move_token_outcome", encoded.Value, clock);
                Result sendResult = await transport.SendReliableAsync(handle, response, cancellationToken);
                if (sendResult.IsFailure) return Result<IReadOnlyList<TokenMoveOutcome>>.Failure(sendResult.Error);
            }

            return Result<IReadOnlyList<TokenMoveOutcome>>.Success(accepted);
        }

        /// <summary>
        /// ADR-019 section 6.2/section 7: sends the already-redacted delta
        /// plan (DeltaBroadcastPlanner, computed in Application) to each
        /// addressed connection. A connection whose audience cannot see the
        /// moved entity is simply absent from <paramref name="deltas"/> --
        /// this method never re-derives or overrides that decision.
        /// </summary>
        public static async Task<Result> BroadcastDeltaAsync(IReadOnlyDictionary<UserId, (ISessionTransport Transport, ConnectionHandle Handle)> connections, IReadOnlyList<TokenMovedDelta> deltas, IWallClock clock, CancellationToken cancellationToken)
        {
            foreach (TokenMovedDelta delta in deltas)
            {
                if (!connections.TryGetValue(delta.AudienceUserId, out (ISessionTransport Transport, ConnectionHandle Handle) connection)) continue;

                TokenMovedDeltaMessage message = new TokenMovedDeltaMessage(
                    delta.SessionId.ToString(), delta.AudienceUserId.ToString(), delta.SequenceFrom, delta.SequenceTo,
                    delta.EntityId, delta.Position.X.ToString("R", CultureInfo.InvariantCulture), delta.Position.Y.ToString("R", CultureInfo.InvariantCulture), delta.EntityRevision);
                Result<byte[]> encoded = TokenMoveWireCodec.WriteDelta(message);
                if (encoded.IsFailure) return Result.Failure(encoded.Error);

                NetworkEnvelope envelope = MakeEnvelope(connection.Handle, "odyssey.command.token_moved_delta", encoded.Value, clock);
                Result sendResult = await connection.Transport.SendReliableAsync(connection.Handle, envelope, cancellationToken);
                if (sendResult.IsFailure) return sendResult;
            }

            return Result.Success();
        }

        private static Result<TokenMoveOutcome> ExecuteFromMessage(TokenMoveSessionState state, SessionAdmissionState admission, MoveTokenRequestMessage message)
        {
            if (!CommandId.TryParse(message.CommandId, out CommandId commandId)) return Result<TokenMoveOutcome>.Failure(TokenMoveFailures.CommandIdentityMismatch(PlaceholderCorrelationId()));
            if (!SessionId.TryParse(message.SessionId, out SessionId sessionId)) return Result<TokenMoveOutcome>.Failure(TokenMoveFailures.ActionNotAllowed(PlaceholderCorrelationId()));
            if (!UserId.TryParse(message.ActorUserId, out UserId actorUserId)) return Result<TokenMoveOutcome>.Failure(TokenMoveFailures.ActionNotAllowed(PlaceholderCorrelationId()));
            if (!double.TryParse(message.X, NumberStyles.Float, CultureInfo.InvariantCulture, out double x) || !double.TryParse(message.Y, NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
            {
                return Result<TokenMoveOutcome>.Failure(TokenMoveFailures.ActionNotAllowed(PlaceholderCorrelationId()));
            }

            MoveTokenCommand command = new MoveTokenCommand(commandId, sessionId, actorUserId, message.EntityId, new TokenPosition(x, y), message.ExpectedRevision);
            return MoveTokenService.Execute(state, admission, command);
        }

        private static CorrelationId PlaceholderCorrelationId() => CorrelationId.Parse("corr_00000000000000000000000000000000");

        private static NetworkEnvelope MakeEnvelope(ConnectionHandle handle, string payloadType, byte[] payload, IWallClock clock) =>
            new NetworkEnvelope(
                MessageId.NewId(clock.GetUtcNow()),
                handle.SessionId,
                senderUserId: null,
                senderClientInstanceId: null,
                NetworkMessageKind.ApplicationPayload,
                handle.NegotiatedProtocolVersion,
                correlationId: null,
                causationId: null,
                sentAtHostTime: clock.GetUtcNow(),
                payloadType,
                payloadVersion: 1,
                payload: payload);
    }
}
