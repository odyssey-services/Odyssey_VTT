using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Odyssey.Application.Networking;
using Odyssey.Application.Networking.Projection;
using Odyssey.Application.Networking.Reconnect;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;

namespace Odyssey.Networking.Reconnect
{
    /// <summary>
    /// ODY-S02-012: the real (not test-only) Networking-layer adapter that
    /// carries reconnect requests, buffered-delta catch-up, and full-snapshot
    /// fallback (Odyssey.Application.Networking.Reconnect/.Projection) over
    /// ISessionTransport's reliable channel -- ADR-015 section 5.1, ADR-001
    /// section 6.6. Never decides the catch-up-vs-fallback path itself: that
    /// decision is entirely ReconnectPlanner's (Application layer).
    /// </summary>
    public static class ContinuityClientChannel
    {
        public static async Task<Result> SendReconnectRequestAsync(ISessionTransport transport, ConnectionHandle handle, UserId requestingUserId, IWallClock clock, CancellationToken cancellationToken)
        {
            Result<byte[]> payload = ReconnectWireCodec.WriteReconnectRequest(new ReconnectRequestMessage(requestingUserId.ToString()));
            if (payload.IsFailure) return Result.Failure(payload.Error);
            NetworkEnvelope envelope = MakeEnvelope(handle, "odyssey.reconnect.request", payload.Value, clock);
            return await transport.SendReliableAsync(handle, envelope, cancellationToken);
        }

        /// <summary>
        /// Drains every envelope received on this connection exactly once
        /// and partitions it into buffered-delta catch-up messages and full
        /// snapshots. A single combined drain is required here (not one
        /// Drain* call per payload type): ISessionTransport.DrainReliable
        /// empties the connection's inbox on every call, and a reconnect
        /// exchange may legitimately deliver either payload type (never
        /// both) to the same connection -- calling two single-purpose Drain
        /// methods back to back would silently discard whichever type the
        /// first call already consumed and filtered out.
        /// </summary>
        public static Result<(IReadOnlyList<BufferedDeltaMessage> Deltas, IReadOnlyList<ProjectionSnapshot> Snapshots)> DrainReconnectPayloads(ISessionTransport transport, ConnectionHandle handle)
        {
            Result<IReadOnlyList<NetworkEnvelope>> drained = transport.DrainReliable(handle);
            if (drained.IsFailure) return Result<(IReadOnlyList<BufferedDeltaMessage>, IReadOnlyList<ProjectionSnapshot>)>.Failure(drained.Error);

            List<BufferedDeltaMessage> deltas = new List<BufferedDeltaMessage>();
            List<ProjectionSnapshot> snapshots = new List<ProjectionSnapshot>();
            foreach (NetworkEnvelope envelope in drained.Value)
            {
                if (envelope.PayloadType == "odyssey.reconnect.buffered_delta")
                {
                    Result<BufferedDeltaMessage> delta = ReconnectWireCodec.ReadBufferedDelta(envelope.Payload);
                    if (delta.IsFailure) return Result<(IReadOnlyList<BufferedDeltaMessage>, IReadOnlyList<ProjectionSnapshot>)>.Failure(delta.Error);
                    deltas.Add(delta.Value);
                }
                else if (envelope.PayloadType == ProjectionSnapshotWireCodec.ContractType)
                {
                    Result<ProjectionSnapshot> snapshot = ProjectionSnapshotWireCodec.Read(envelope.Payload);
                    if (snapshot.IsFailure) return Result<(IReadOnlyList<BufferedDeltaMessage>, IReadOnlyList<ProjectionSnapshot>)>.Failure(snapshot.Error);
                    snapshots.Add(snapshot.Value);
                }
            }

            return Result<(IReadOnlyList<BufferedDeltaMessage>, IReadOnlyList<ProjectionSnapshot>)>.Success((deltas, snapshots));
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

    public static class ContinuityHostChannel
    {
        /// <summary>
        /// Sends every currently-connected, currently-entitled target a
        /// buffered delta for a just-committed move (ContinuityBroadcastPlanner's
        /// output), then records each successfully-sent audience's new
        /// LastAcknowledgedSequence -- host-tracked, never client-reported
        /// (ADR-002 section 6.5: client-provided fields are claims, not
        /// proof; here the host simply never needs to ask).
        /// </summary>
        public static async Task<Result> BroadcastLiveMoveAsync(ReconnectSessionState state, IReadOnlyDictionary<UserId, (ISessionTransport Transport, ConnectionHandle Handle)> connections, IReadOnlyList<(UserId Audience, BufferedDelta Entry)> targets, IWallClock clock, CancellationToken cancellationToken)
        {
            foreach ((UserId audience, BufferedDelta entry) in targets)
            {
                if (!connections.TryGetValue(audience, out (ISessionTransport Transport, ConnectionHandle Handle) connection)) continue;

                Result<byte[]> encoded = ReconnectWireCodec.WriteBufferedDelta(ToMessage(entry));
                if (encoded.IsFailure) return Result.Failure(encoded.Error);

                NetworkEnvelope envelope = MakeEnvelope(connection.Handle, "odyssey.reconnect.buffered_delta", encoded.Value, clock);
                Result sendResult = await connection.Transport.SendReliableAsync(connection.Handle, envelope, cancellationToken);
                if (sendResult.IsFailure) return sendResult;

                state.SetLastAcknowledged(audience, entry.BufferSequence);
            }

            return Result.Success();
        }

        /// <summary>
        /// Drains reconnect requests from one connection, computes each
        /// requester's ReconnectPlan (the only place that decision is made),
        /// and sends either the buffered catch-up deltas or a full
        /// ProjectionSnapshot back over the same connection -- never both.
        /// </summary>
        public static async Task<Result<int>> ProcessReconnectRequestsAsync(ISessionTransport transport, ConnectionHandle handle, ReconnectSessionState state, SessionAdmissionState admission, SessionId sessionId, IWallClock clock, CancellationToken cancellationToken)
        {
            Result<IReadOnlyList<NetworkEnvelope>> drained = transport.DrainReliable(handle);
            if (drained.IsFailure) return Result<int>.Failure(drained.Error);

            int processed = 0;
            foreach (NetworkEnvelope envelope in drained.Value)
            {
                if (envelope.PayloadType != "odyssey.reconnect.request") continue;

                Result<ReconnectRequestMessage> request = ReconnectWireCodec.ReadReconnectRequest(envelope.Payload);
                if (request.IsFailure) return Result<int>.Failure(request.Error);
                if (!UserId.TryParse(request.Value.RequestingUserId, out UserId requestingUserId)) return Result<int>.Failure(request.Error);

                Result<ReconnectPlan> plan = ReconnectPlanner.Plan(state, admission, requestingUserId, sessionId, clock);
                if (plan.IsFailure) return Result<int>.Failure(plan.Error);

                Result sendResult = plan.Value.Kind == ReconnectPathKind.DeltaCatchup
                    ? await SendCatchupAsync(transport, handle, plan.Value.CatchupEntries, clock, cancellationToken)
                    : await SendSnapshotAsync(transport, handle, plan.Value.FallbackSnapshot!, clock, cancellationToken);
                if (sendResult.IsFailure) return Result<int>.Failure(sendResult.Error);

                state.SetLastAcknowledged(requestingUserId, state.Buffer.LatestSequence);
                processed++;
            }

            return Result<int>.Success(processed);
        }

        private static async Task<Result> SendCatchupAsync(ISessionTransport transport, ConnectionHandle handle, IReadOnlyList<BufferedDelta> entries, IWallClock clock, CancellationToken cancellationToken)
        {
            foreach (BufferedDelta entry in entries)
            {
                Result<byte[]> encoded = ReconnectWireCodec.WriteBufferedDelta(ToMessage(entry));
                if (encoded.IsFailure) return Result.Failure(encoded.Error);
                NetworkEnvelope envelope = MakeEnvelope(handle, "odyssey.reconnect.buffered_delta", encoded.Value, clock);
                Result sendResult = await transport.SendReliableAsync(handle, envelope, cancellationToken);
                if (sendResult.IsFailure) return sendResult;
            }

            return Result.Success();
        }

        private static async Task<Result> SendSnapshotAsync(ISessionTransport transport, ConnectionHandle handle, ProjectionSnapshot snapshot, IWallClock clock, CancellationToken cancellationToken)
        {
            Result<byte[]> encoded = ProjectionSnapshotWireCodec.Write(snapshot);
            if (encoded.IsFailure) return Result.Failure(encoded.Error);
            NetworkEnvelope envelope = MakeEnvelope(handle, ProjectionSnapshotWireCodec.ContractType, encoded.Value, clock);
            return await transport.SendReliableAsync(handle, envelope, cancellationToken);
        }

        private static BufferedDeltaMessage ToMessage(BufferedDelta entry) => new BufferedDeltaMessage(
            entry.BufferSequence, entry.EntityId,
            entry.Position.X.ToString("R", CultureInfo.InvariantCulture), entry.Position.Y.ToString("R", CultureInfo.InvariantCulture),
            entry.EntityRevision);

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
