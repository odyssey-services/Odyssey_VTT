using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Odyssey.Application.Networking;
using Odyssey.Application.Networking.Projection;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;

namespace Odyssey.Networking.Projection
{
    /// <summary>
    /// ODY-S02-010: the real (not test-only) Networking-layer adapter that
    /// carries a host-built ProjectionSnapshot
    /// (Odyssey.Application.Networking.Projection) over ISessionTransport's
    /// reliable channel -- ADR-015 section 5.1/6.1, ADR-017 section 11,
    /// ADR-019 section 6.2/section 11. This module makes no
    /// visibility/redaction decision itself: the snapshot handed to
    /// SendSnapshotAsync is already fully redacted by the caller via
    /// SceneProjectionBuilder.BuildSnapshot before it ever reaches
    /// Odyssey.Networking (ADR-001 section 6.6).
    /// </summary>
    public static class SceneProjectionHostChannel
    {
        public static async Task<Result> SendSnapshotAsync(ISessionTransport transport, ConnectionHandle handle, ProjectionSnapshot snapshot, IWallClock clock, CancellationToken cancellationToken)
        {
            Result<byte[]> payload = ProjectionSnapshotWireCodec.Write(snapshot);
            if (payload.IsFailure) return Result.Failure(payload.Error);

            NetworkEnvelope envelope = new NetworkEnvelope(
                MessageId.NewId(clock.GetUtcNow()),
                handle.SessionId,
                senderUserId: null,
                senderClientInstanceId: null,
                NetworkMessageKind.ApplicationPayload,
                handle.NegotiatedProtocolVersion,
                correlationId: null,
                causationId: null,
                sentAtHostTime: clock.GetUtcNow(),
                ProjectionSnapshotWireCodec.ContractType,
                payloadVersion: 1,
                payload: payload.Value);
            return await transport.SendReliableAsync(handle, envelope, cancellationToken);
        }
    }

    public static class SceneProjectionClientChannel
    {
        /// <summary>
        /// Drains and decodes every ProjectionSnapshot received since the
        /// last drain, in arrival order. Empty list (inside a success) means
        /// nothing arrived yet -- the same never-null/empty-means-nothing-yet
        /// contract as ISessionTransport.DrainReliable and ODY-S02-009's
        /// SessionAdmissionClientChannel.DrainOutcomes.
        /// </summary>
        public static Result<IReadOnlyList<ProjectionSnapshot>> DrainSnapshots(ISessionTransport transport, ConnectionHandle handle)
        {
            Result<IReadOnlyList<NetworkEnvelope>> drained = transport.DrainReliable(handle);
            if (drained.IsFailure) return Result<IReadOnlyList<ProjectionSnapshot>>.Failure(drained.Error);

            List<ProjectionSnapshot> snapshots = new List<ProjectionSnapshot>();
            foreach (NetworkEnvelope envelope in drained.Value)
            {
                if (envelope.PayloadType != ProjectionSnapshotWireCodec.ContractType) continue;
                Result<ProjectionSnapshot> snapshot = ProjectionSnapshotWireCodec.Read(envelope.Payload);
                if (snapshot.IsFailure) return Result<IReadOnlyList<ProjectionSnapshot>>.Failure(snapshot.Error);
                snapshots.Add(snapshot.Value);
            }

            return Result<IReadOnlyList<ProjectionSnapshot>>.Success(snapshots);
        }
    }
}
