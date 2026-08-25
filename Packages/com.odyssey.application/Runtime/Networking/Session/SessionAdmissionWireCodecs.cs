using System;
using Odyssey.Application.Results;
using Odyssey.Application.Serialization;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Networking.Session
{
    /// <summary>
    /// ODY-S02-009's wire messages for the admission/lobby exchange, carried
    /// as NetworkEnvelope payload bytes (ADR-015 section 6.1) over the
    /// reliable channel. Hand-written canonical JSON codecs, per ADR-003
    /// section 3's ban on reflection/auto-mapping serialization for
    /// production wire content -- the same JsonObjectReader/CanonicalJsonWriter
    /// primitives every other production codec in this repository uses, not
    /// System.Text.Json's default reflection-based serializer (acceptable
    /// only in the SP-04 harness's own explicitly test-only code, not here).
    /// </summary>
    public sealed class JoinRequestMessage
    {
        public JoinRequestMessage(string joinCode, string userId)
        {
            JoinCode = joinCode ?? throw new ArgumentNullException(nameof(joinCode));
            UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        }

        public string JoinCode { get; }
        public string UserId { get; }
    }

    public sealed class RoleAssignmentRequestMessage
    {
        public RoleAssignmentRequestMessage(string requestingUserId, string targetUserId, string role)
        {
            RequestingUserId = requestingUserId ?? throw new ArgumentNullException(nameof(requestingUserId));
            TargetUserId = targetUserId ?? throw new ArgumentNullException(nameof(targetUserId));
            Role = role ?? throw new ArgumentNullException(nameof(role));
        }

        public string RequestingUserId { get; }
        public string TargetUserId { get; }
        public string Role { get; }
    }

    /// <summary>
    /// Shared outcome shape for both admission operations: on success carries
    /// the resulting member's UserId/Role; on failure carries the typed
    /// ErrorCode/SafeReasonCode (ADR-004) -- never a raw exception message.
    /// </summary>
    public sealed class AdmissionOutcomeMessage
    {
        public AdmissionOutcomeMessage(bool success, string? userId, string? role, string? errorCode, string? safeReasonCode)
        {
            Success = success;
            UserId = userId;
            Role = role;
            ErrorCode = errorCode;
            SafeReasonCode = safeReasonCode;
        }

        public bool Success { get; }
        public string? UserId { get; }
        public string? Role { get; }
        public string? ErrorCode { get; }
        public string? SafeReasonCode { get; }

        public static AdmissionOutcomeMessage FromSuccess(SessionMember member) =>
            new AdmissionOutcomeMessage(true, member.UserId.ToString(), member.Role.ToString(), null, null);

        public static AdmissionOutcomeMessage FromFailure(Error error) =>
            new AdmissionOutcomeMessage(false, null, null, error.Code.ToString(), error.SafeReasonCode.ToString());
    }

    public static class SessionAdmissionWireCodec
    {
        private const int MaxBytes = 4096;

        public static Result<byte[]> WriteJoinRequest(JoinRequestMessage message)
        {
            byte[] bytes = new CanonicalJsonWriter().StartObject()
                .String("contractType", "odyssey.session.join_request")
                .Int32("contractVersion", 1)
                .String("joinCode", message.JoinCode)
                .String("userId", message.UserId)
                .EndObject()
                .ToPayload()
                .Bytes;
            return Result<byte[]>.Success(bytes);
        }

        public static Result<JoinRequestMessage> ReadJoinRequest(byte[] utf8Json)
        {
            Result<JsonObjectReader> reader = JsonObjectReader.Read(utf8Json, MaxBytes);
            if (reader.IsFailure) return Result<JoinRequestMessage>.Failure(reader.Error);
            Result schema = reader.Value.EnsureOnly("contractType", "contractVersion", "joinCode", "userId");
            if (schema.IsFailure) return Result<JoinRequestMessage>.Failure(schema.Error);
            Result<string> joinCode = reader.Value.RequiredString("joinCode");
            Result<string> userId = reader.Value.RequiredString("userId");
            if (joinCode.IsFailure) return Result<JoinRequestMessage>.Failure(joinCode.Error);
            if (userId.IsFailure) return Result<JoinRequestMessage>.Failure(userId.Error);
            return Result<JoinRequestMessage>.Success(new JoinRequestMessage(joinCode.Value, userId.Value));
        }

        public static Result<byte[]> WriteRoleAssignmentRequest(RoleAssignmentRequestMessage message)
        {
            byte[] bytes = new CanonicalJsonWriter().StartObject()
                .String("contractType", "odyssey.session.role_assignment_request")
                .Int32("contractVersion", 1)
                .String("requestingUserId", message.RequestingUserId)
                .String("targetUserId", message.TargetUserId)
                .String("role", message.Role)
                .EndObject()
                .ToPayload()
                .Bytes;
            return Result<byte[]>.Success(bytes);
        }

        public static Result<RoleAssignmentRequestMessage> ReadRoleAssignmentRequest(byte[] utf8Json)
        {
            Result<JsonObjectReader> reader = JsonObjectReader.Read(utf8Json, MaxBytes);
            if (reader.IsFailure) return Result<RoleAssignmentRequestMessage>.Failure(reader.Error);
            Result schema = reader.Value.EnsureOnly("contractType", "contractVersion", "requestingUserId", "targetUserId", "role");
            if (schema.IsFailure) return Result<RoleAssignmentRequestMessage>.Failure(schema.Error);
            Result<string> requestingUserId = reader.Value.RequiredString("requestingUserId");
            Result<string> targetUserId = reader.Value.RequiredString("targetUserId");
            Result<string> role = reader.Value.RequiredString("role");
            if (requestingUserId.IsFailure) return Result<RoleAssignmentRequestMessage>.Failure(requestingUserId.Error);
            if (targetUserId.IsFailure) return Result<RoleAssignmentRequestMessage>.Failure(targetUserId.Error);
            if (role.IsFailure) return Result<RoleAssignmentRequestMessage>.Failure(role.Error);
            return Result<RoleAssignmentRequestMessage>.Success(new RoleAssignmentRequestMessage(requestingUserId.Value, targetUserId.Value, role.Value));
        }

        public static Result<byte[]> WriteOutcome(AdmissionOutcomeMessage message)
        {
            CanonicalJsonWriter writer = new CanonicalJsonWriter().StartObject()
                .String("contractType", "odyssey.session.admission_outcome")
                .Int32("contractVersion", 1)
                .Boolean("success", message.Success)
                .NullableString("userId", message.UserId)
                .NullableString("role", message.Role)
                .NullableString("errorCode", message.ErrorCode)
                .NullableString("safeReasonCode", message.SafeReasonCode);
            byte[] bytes = writer.EndObject().ToPayload().Bytes;
            return Result<byte[]>.Success(bytes);
        }

        public static Result<AdmissionOutcomeMessage> ReadOutcome(byte[] utf8Json)
        {
            Result<JsonObjectReader> reader = JsonObjectReader.Read(utf8Json, MaxBytes);
            if (reader.IsFailure) return Result<AdmissionOutcomeMessage>.Failure(reader.Error);
            Result schema = reader.Value.EnsureOnly("contractType", "contractVersion", "success", "userId", "role", "errorCode", "safeReasonCode");
            if (schema.IsFailure) return Result<AdmissionOutcomeMessage>.Failure(schema.Error);
            Result<bool> success = reader.Value.RequiredBoolean("success");
            if (success.IsFailure) return Result<AdmissionOutcomeMessage>.Failure(success.Error);
            reader.Value.TryGetString("userId", out string? userId);
            reader.Value.TryGetString("role", out string? role);
            reader.Value.TryGetString("errorCode", out string? errorCode);
            reader.Value.TryGetString("safeReasonCode", out string? safeReasonCode);
            return Result<AdmissionOutcomeMessage>.Success(new AdmissionOutcomeMessage(success.Value, userId, role, errorCode, safeReasonCode));
        }
    }
}
