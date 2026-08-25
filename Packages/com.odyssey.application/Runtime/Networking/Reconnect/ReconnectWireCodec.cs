using System;
using Odyssey.Application.Results;
using Odyssey.Application.Serialization;

namespace Odyssey.Application.Networking.Reconnect
{
    /// <summary>
    /// ODY-S02-012's wire messages for the reconnect/catch-up exchange,
    /// carried as NetworkEnvelope payload bytes (ADR-015 section 6.1) over
    /// the reliable channel. The full-snapshot fallback path reuses
    /// ODY-S02-010's ProjectionSnapshotWireCodec directly, unmodified -- only
    /// the reconnect request and buffered-delta catch-up messages are new
    /// here. Hand-written canonical JSON per ADR-003 section 3, following
    /// ODY-S02-009/011's established flat JsonObjectReader/CanonicalJsonWriter
    /// pattern; position fields are string-encoded doubles for the same
    /// reason ODY-S02-011 already established (JsonObjectReader has no Float
    /// token support).
    /// </summary>
    public sealed class ReconnectRequestMessage
    {
        public ReconnectRequestMessage(string requestingUserId)
        {
            RequestingUserId = requestingUserId ?? throw new ArgumentNullException(nameof(requestingUserId));
        }

        public string RequestingUserId { get; }
    }

    public sealed class BufferedDeltaMessage
    {
        public BufferedDeltaMessage(long bufferSequence, string entityId, string x, string y, long entityRevision)
        {
            BufferSequence = bufferSequence;
            EntityId = entityId ?? throw new ArgumentNullException(nameof(entityId));
            X = x ?? throw new ArgumentNullException(nameof(x));
            Y = y ?? throw new ArgumentNullException(nameof(y));
            EntityRevision = entityRevision;
        }

        public long BufferSequence { get; }
        public string EntityId { get; }
        public string X { get; }
        public string Y { get; }
        public long EntityRevision { get; }
    }

    public static class ReconnectWireCodec
    {
        private const int MaxBytes = 4096;

        public static Result<byte[]> WriteReconnectRequest(ReconnectRequestMessage message)
        {
            byte[] bytes = new CanonicalJsonWriter().StartObject()
                .String("contractType", "odyssey.reconnect.request")
                .Int32("contractVersion", 1)
                .String("requestingUserId", message.RequestingUserId)
                .EndObject()
                .ToPayload()
                .Bytes;
            return Result<byte[]>.Success(bytes);
        }

        public static Result<ReconnectRequestMessage> ReadReconnectRequest(byte[] utf8Json)
        {
            Result<JsonObjectReader> reader = JsonObjectReader.Read(utf8Json, MaxBytes);
            if (reader.IsFailure) return Result<ReconnectRequestMessage>.Failure(reader.Error);
            Result schema = reader.Value.EnsureOnly("contractType", "contractVersion", "requestingUserId");
            if (schema.IsFailure) return Result<ReconnectRequestMessage>.Failure(schema.Error);
            Result<string> requestingUserId = reader.Value.RequiredString("requestingUserId");
            if (requestingUserId.IsFailure) return Result<ReconnectRequestMessage>.Failure(requestingUserId.Error);
            return Result<ReconnectRequestMessage>.Success(new ReconnectRequestMessage(requestingUserId.Value));
        }

        public static Result<byte[]> WriteBufferedDelta(BufferedDeltaMessage message)
        {
            byte[] bytes = new CanonicalJsonWriter().StartObject()
                .String("contractType", "odyssey.reconnect.buffered_delta")
                .Int32("contractVersion", 1)
                .Int64("bufferSequence", message.BufferSequence)
                .String("entityId", message.EntityId)
                .String("x", message.X)
                .String("y", message.Y)
                .Int64("entityRevision", message.EntityRevision)
                .EndObject()
                .ToPayload()
                .Bytes;
            return Result<byte[]>.Success(bytes);
        }

        public static Result<BufferedDeltaMessage> ReadBufferedDelta(byte[] utf8Json)
        {
            Result<JsonObjectReader> reader = JsonObjectReader.Read(utf8Json, MaxBytes);
            if (reader.IsFailure) return Result<BufferedDeltaMessage>.Failure(reader.Error);
            Result schema = reader.Value.EnsureOnly("contractType", "contractVersion", "bufferSequence", "entityId", "x", "y", "entityRevision");
            if (schema.IsFailure) return Result<BufferedDeltaMessage>.Failure(schema.Error);

            Result<long> bufferSequence = reader.Value.RequiredInt64("bufferSequence");
            Result<string> entityId = reader.Value.RequiredString("entityId");
            Result<string> x = reader.Value.RequiredString("x");
            Result<string> y = reader.Value.RequiredString("y");
            Result<long> entityRevision = reader.Value.RequiredInt64("entityRevision");
            if (bufferSequence.IsFailure) return Result<BufferedDeltaMessage>.Failure(bufferSequence.Error);
            if (entityId.IsFailure) return Result<BufferedDeltaMessage>.Failure(entityId.Error);
            if (x.IsFailure) return Result<BufferedDeltaMessage>.Failure(x.Error);
            if (y.IsFailure) return Result<BufferedDeltaMessage>.Failure(y.Error);
            if (entityRevision.IsFailure) return Result<BufferedDeltaMessage>.Failure(entityRevision.Error);
            return Result<BufferedDeltaMessage>.Success(new BufferedDeltaMessage(bufferSequence.Value, entityId.Value, x.Value, y.Value, entityRevision.Value));
        }
    }
}
