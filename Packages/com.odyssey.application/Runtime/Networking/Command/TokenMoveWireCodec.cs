using System;
using System.Globalization;
using Odyssey.Application.Results;
using Odyssey.Application.Serialization;

namespace Odyssey.Application.Networking.Command
{
    /// <summary>
    /// ODY-S02-011's wire messages for the token-move request/outcome/delta
    /// exchange, carried as NetworkEnvelope payload bytes (ADR-015 section
    /// 6.1) over the reliable channel. Hand-written canonical JSON per
    /// ADR-003 section 3 -- the same JsonObjectReader/CanonicalJsonWriter
    /// primitives ODY-S02-009's SessionAdmissionWireCodecs.cs already
    /// establishes. Position fields are written as strings (via
    /// CultureInfo.InvariantCulture round-trip format), not raw JSON
    /// numbers: JsonObjectReader's flat reader only recognizes String/
    /// Integer/Boolean/Null tokens, not Float -- the same constraint that
    /// pushed ODY-S02-010's array payload to a hand-rolled reader; here a
    /// string-encoded double avoids that gap without needing one.
    /// </summary>
    public sealed class MoveTokenRequestMessage
    {
        public MoveTokenRequestMessage(string commandId, string sessionId, string actorUserId, string entityId, string x, string y, long expectedRevision)
        {
            CommandId = commandId ?? throw new ArgumentNullException(nameof(commandId));
            SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
            ActorUserId = actorUserId ?? throw new ArgumentNullException(nameof(actorUserId));
            EntityId = entityId ?? throw new ArgumentNullException(nameof(entityId));
            X = x ?? throw new ArgumentNullException(nameof(x));
            Y = y ?? throw new ArgumentNullException(nameof(y));
            ExpectedRevision = expectedRevision;
        }

        public string CommandId { get; }
        public string SessionId { get; }
        public string ActorUserId { get; }
        public string EntityId { get; }
        public string X { get; }
        public string Y { get; }
        public long ExpectedRevision { get; }
    }

    public sealed class MoveTokenOutcomeMessage
    {
        public MoveTokenOutcomeMessage(string commandId, bool success, string? entityId, string? x, string? y, long? revision, string? errorCode, string? safeReasonCode)
        {
            CommandId = commandId ?? throw new ArgumentNullException(nameof(commandId));
            Success = success;
            EntityId = entityId;
            X = x;
            Y = y;
            Revision = revision;
            ErrorCode = errorCode;
            SafeReasonCode = safeReasonCode;
        }

        public string CommandId { get; }
        public bool Success { get; }
        public string? EntityId { get; }
        public string? X { get; }
        public string? Y { get; }
        public long? Revision { get; }
        public string? ErrorCode { get; }
        public string? SafeReasonCode { get; }

        public static MoveTokenOutcomeMessage FromSuccess(string commandId, TokenMoveOutcome outcome) => new MoveTokenOutcomeMessage(
            commandId, true, outcome.EntityId,
            outcome.Position.X.ToString("R", CultureInfo.InvariantCulture), outcome.Position.Y.ToString("R", CultureInfo.InvariantCulture),
            outcome.Revision, null, null);

        public static MoveTokenOutcomeMessage FromFailure(string commandId, Error error) => new MoveTokenOutcomeMessage(
            commandId, false, null, null, null, null, error.Code.ToString(), error.SafeReasonCode.ToString());
    }

    public sealed class TokenMovedDeltaMessage
    {
        public TokenMovedDeltaMessage(string sessionId, string audienceUserId, long sequenceFrom, long sequenceTo, string entityId, string x, string y, long entityRevision)
        {
            SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
            AudienceUserId = audienceUserId ?? throw new ArgumentNullException(nameof(audienceUserId));
            SequenceFrom = sequenceFrom;
            SequenceTo = sequenceTo;
            EntityId = entityId ?? throw new ArgumentNullException(nameof(entityId));
            X = x ?? throw new ArgumentNullException(nameof(x));
            Y = y ?? throw new ArgumentNullException(nameof(y));
            EntityRevision = entityRevision;
        }

        public string SessionId { get; }
        public string AudienceUserId { get; }
        public long SequenceFrom { get; }
        public long SequenceTo { get; }
        public string EntityId { get; }
        public string X { get; }
        public string Y { get; }
        public long EntityRevision { get; }
    }

    public static class TokenMoveWireCodec
    {
        private const int MaxBytes = 4096;

        public static Result<byte[]> WriteMoveRequest(MoveTokenRequestMessage message)
        {
            byte[] bytes = new CanonicalJsonWriter().StartObject()
                .String("contractType", "odyssey.command.move_token_request")
                .Int32("contractVersion", 1)
                .String("commandId", message.CommandId)
                .String("sessionId", message.SessionId)
                .String("actorUserId", message.ActorUserId)
                .String("entityId", message.EntityId)
                .String("x", message.X)
                .String("y", message.Y)
                .Int64("expectedRevision", message.ExpectedRevision)
                .EndObject()
                .ToPayload()
                .Bytes;
            return Result<byte[]>.Success(bytes);
        }

        public static Result<MoveTokenRequestMessage> ReadMoveRequest(byte[] utf8Json)
        {
            Result<JsonObjectReader> reader = JsonObjectReader.Read(utf8Json, MaxBytes);
            if (reader.IsFailure) return Result<MoveTokenRequestMessage>.Failure(reader.Error);
            Result schema = reader.Value.EnsureOnly("contractType", "contractVersion", "commandId", "sessionId", "actorUserId", "entityId", "x", "y", "expectedRevision");
            if (schema.IsFailure) return Result<MoveTokenRequestMessage>.Failure(schema.Error);

            Result<string> commandId = reader.Value.RequiredString("commandId");
            Result<string> sessionId = reader.Value.RequiredString("sessionId");
            Result<string> actorUserId = reader.Value.RequiredString("actorUserId");
            Result<string> entityId = reader.Value.RequiredString("entityId");
            Result<string> x = reader.Value.RequiredString("x");
            Result<string> y = reader.Value.RequiredString("y");
            Result<long> expectedRevision = reader.Value.RequiredInt64("expectedRevision");
            if (commandId.IsFailure) return Result<MoveTokenRequestMessage>.Failure(commandId.Error);
            if (sessionId.IsFailure) return Result<MoveTokenRequestMessage>.Failure(sessionId.Error);
            if (actorUserId.IsFailure) return Result<MoveTokenRequestMessage>.Failure(actorUserId.Error);
            if (entityId.IsFailure) return Result<MoveTokenRequestMessage>.Failure(entityId.Error);
            if (x.IsFailure) return Result<MoveTokenRequestMessage>.Failure(x.Error);
            if (y.IsFailure) return Result<MoveTokenRequestMessage>.Failure(y.Error);
            if (expectedRevision.IsFailure) return Result<MoveTokenRequestMessage>.Failure(expectedRevision.Error);
            return Result<MoveTokenRequestMessage>.Success(new MoveTokenRequestMessage(commandId.Value, sessionId.Value, actorUserId.Value, entityId.Value, x.Value, y.Value, expectedRevision.Value));
        }

        public static Result<byte[]> WriteOutcome(MoveTokenOutcomeMessage message)
        {
            CanonicalJsonWriter writer = new CanonicalJsonWriter().StartObject()
                .String("contractType", "odyssey.command.move_token_outcome")
                .Int32("contractVersion", 1)
                .String("commandId", message.CommandId)
                .Boolean("success", message.Success)
                .NullableString("entityId", message.EntityId)
                .NullableString("x", message.X)
                .NullableString("y", message.Y)
                .NullableInt64("revision", message.Revision)
                .NullableString("errorCode", message.ErrorCode)
                .NullableString("safeReasonCode", message.SafeReasonCode);
            byte[] bytes = writer.EndObject().ToPayload().Bytes;
            return Result<byte[]>.Success(bytes);
        }

        public static Result<MoveTokenOutcomeMessage> ReadOutcome(byte[] utf8Json)
        {
            Result<JsonObjectReader> reader = JsonObjectReader.Read(utf8Json, MaxBytes);
            if (reader.IsFailure) return Result<MoveTokenOutcomeMessage>.Failure(reader.Error);
            Result schema = reader.Value.EnsureOnly("contractType", "contractVersion", "commandId", "success", "entityId", "x", "y", "revision", "errorCode", "safeReasonCode");
            if (schema.IsFailure) return Result<MoveTokenOutcomeMessage>.Failure(schema.Error);

            Result<string> commandId = reader.Value.RequiredString("commandId");
            Result<bool> success = reader.Value.RequiredBoolean("success");
            if (commandId.IsFailure) return Result<MoveTokenOutcomeMessage>.Failure(commandId.Error);
            if (success.IsFailure) return Result<MoveTokenOutcomeMessage>.Failure(success.Error);
            reader.Value.TryGetString("entityId", out string? entityId);
            reader.Value.TryGetString("x", out string? x);
            reader.Value.TryGetString("y", out string? y);
            reader.Value.TryGetString("revision", out string? revisionText);
            reader.Value.TryGetString("errorCode", out string? errorCode);
            reader.Value.TryGetString("safeReasonCode", out string? safeReasonCode);
            long? revision = revisionText == null ? (long?)null : long.Parse(revisionText, NumberStyles.None, CultureInfo.InvariantCulture);
            return Result<MoveTokenOutcomeMessage>.Success(new MoveTokenOutcomeMessage(commandId.Value, success.Value, entityId, x, y, revision, errorCode, safeReasonCode));
        }

        public static Result<byte[]> WriteDelta(TokenMovedDeltaMessage message)
        {
            byte[] bytes = new CanonicalJsonWriter().StartObject()
                .String("contractType", "odyssey.command.token_moved_delta")
                .Int32("contractVersion", 1)
                .String("sessionId", message.SessionId)
                .String("audienceUserId", message.AudienceUserId)
                .Int64("sequenceFrom", message.SequenceFrom)
                .Int64("sequenceTo", message.SequenceTo)
                .String("entityId", message.EntityId)
                .String("x", message.X)
                .String("y", message.Y)
                .Int64("entityRevision", message.EntityRevision)
                .EndObject()
                .ToPayload()
                .Bytes;
            return Result<byte[]>.Success(bytes);
        }

        public static Result<TokenMovedDeltaMessage> ReadDelta(byte[] utf8Json)
        {
            Result<JsonObjectReader> reader = JsonObjectReader.Read(utf8Json, MaxBytes);
            if (reader.IsFailure) return Result<TokenMovedDeltaMessage>.Failure(reader.Error);
            Result schema = reader.Value.EnsureOnly("contractType", "contractVersion", "sessionId", "audienceUserId", "sequenceFrom", "sequenceTo", "entityId", "x", "y", "entityRevision");
            if (schema.IsFailure) return Result<TokenMovedDeltaMessage>.Failure(schema.Error);

            Result<string> sessionId = reader.Value.RequiredString("sessionId");
            Result<string> audienceUserId = reader.Value.RequiredString("audienceUserId");
            Result<long> sequenceFrom = reader.Value.RequiredInt64("sequenceFrom");
            Result<long> sequenceTo = reader.Value.RequiredInt64("sequenceTo");
            Result<string> entityId = reader.Value.RequiredString("entityId");
            Result<string> x = reader.Value.RequiredString("x");
            Result<string> y = reader.Value.RequiredString("y");
            Result<long> entityRevision = reader.Value.RequiredInt64("entityRevision");
            if (sessionId.IsFailure) return Result<TokenMovedDeltaMessage>.Failure(sessionId.Error);
            if (audienceUserId.IsFailure) return Result<TokenMovedDeltaMessage>.Failure(audienceUserId.Error);
            if (sequenceFrom.IsFailure) return Result<TokenMovedDeltaMessage>.Failure(sequenceFrom.Error);
            if (sequenceTo.IsFailure) return Result<TokenMovedDeltaMessage>.Failure(sequenceTo.Error);
            if (entityId.IsFailure) return Result<TokenMovedDeltaMessage>.Failure(entityId.Error);
            if (x.IsFailure) return Result<TokenMovedDeltaMessage>.Failure(x.Error);
            if (y.IsFailure) return Result<TokenMovedDeltaMessage>.Failure(y.Error);
            if (entityRevision.IsFailure) return Result<TokenMovedDeltaMessage>.Failure(entityRevision.Error);
            return Result<TokenMovedDeltaMessage>.Success(new TokenMovedDeltaMessage(sessionId.Value, audienceUserId.Value, sequenceFrom.Value, sequenceTo.Value, entityId.Value, x.Value, y.Value, entityRevision.Value));
        }
    }
}
