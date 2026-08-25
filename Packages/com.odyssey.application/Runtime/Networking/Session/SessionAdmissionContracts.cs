using System;
using System.Security.Cryptography;
using System.Text;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Networking.Session
{
    /// <summary>
    /// ODY-S02-009: the three ADR-019 section 5 baseline roles. Not the full
    /// 07_Permissions BaseRoleKind set (no AssistantGM, per ADR-019 section 5 /
    /// SLICE-02_IMPLEMENTATION_BACKLOG section 4's explicit narrowing).
    /// </summary>
    public enum BaselineRole
    {
        MainGM = 1,
        Player = 2,
        Observer = 3
    }

    /// <summary>
    /// ODY-S02-009's Lobby state machine, per member (roadmap 11.6 steps 2-3):
    /// a joining actor is Admitted (join code validated, default Observer
    /// preset per 06_Networking section 37.1's "новый approved user получает
    /// Observer preset"), then optionally RoleAssigned by the host to a
    /// different baseline role. Scene delivery (step 4) is ODY-S02-010's
    /// scope, not represented here.
    /// </summary>
    public enum MemberAdmissionState
    {
        Admitted = 1,
        RoleAssigned = 2
    }

    public enum SessionStatus
    {
        /// <summary>06_Networking section 7.1's Lobby state -- the only state this task's narrow admission scope produces or consumes.</summary>
        Lobby = 1
    }

    /// <summary>
    /// A short, human-typeable join code (06_Networking section 6.2's
    /// "короткий join code"). Never stored in plaintext in the session
    /// directory (section 3 below) -- only its hash is.
    /// </summary>
    public readonly struct JoinCode : IEquatable<JoinCode>
    {
        private const int Length = 6;
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // excludes 0/O and 1/I to avoid human transcription errors

        private readonly string _value;

        private JoinCode(string value) => _value = value;

        public bool IsValid => _value != null;

        public static bool TryParse(string? value, out JoinCode code)
        {
            if (value != null && value.Length == Length)
            {
                for (int index = 0; index < value.Length; index++)
                {
                    if (Alphabet.IndexOf(value[index]) < 0)
                    {
                        code = default;
                        return false;
                    }
                }

                code = new JoinCode(value);
                return true;
            }

            code = default;
            return false;
        }

        public static JoinCode Parse(string value) => TryParse(value, out JoinCode code) ? code : throw new FormatException("JoinCode is not canonical.");

        /// <summary>
        /// Generates a fresh, random join code. Uses a cryptographically
        /// secure RNG directly, not the ADR-008 authoritative/deterministic
        /// gameplay RNG stream (Odyssey.Application.Random): a join code is a
        /// local, opaque, non-gameplay access token -- the same "local opaque
        /// identifier, not a gameplay RNG result" exemption ADR-008 already
        /// grants Guid-derived identifiers elsewhere in this codebase.
        /// </summary>
        public static JoinCode Generate()
        {
            Span<byte> randomBytes = stackalloc byte[Length];
            RandomNumberGenerator.Fill(randomBytes);
            Span<char> chars = stackalloc char[Length];
            for (int index = 0; index < Length; index++)
            {
                chars[index] = Alphabet[randomBytes[index] % Alphabet.Length];
            }

            return new JoinCode(new string(chars));
        }

        public override string ToString() => _value ?? string.Empty;
        public bool Equals(JoinCode other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is JoinCode other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
    }

    /// <summary>SHA-256 hash of a JoinCode -- the only form persisted in a SessionDirectoryEntry (06_Networking section 6.2/6.3).</summary>
    public readonly struct JoinCodeHash : IEquatable<JoinCodeHash>
    {
        private readonly string _value;

        private JoinCodeHash(string value) => _value = value;

        public bool IsValid => _value != null;

        public static JoinCodeHash Of(JoinCode code)
        {
            if (!code.IsValid) throw new ArgumentException("JoinCode is required.", nameof(code));
            using SHA256 sha = SHA256.Create();
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(code.ToString()));
            StringBuilder builder = new StringBuilder(digest.Length * 2);
            for (int index = 0; index < digest.Length; index++)
            {
                builder.Append(digest[index].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return new JoinCodeHash(builder.ToString());
        }

        public override string ToString() => _value ?? string.Empty;
        public bool Equals(JoinCodeHash other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is JoinCodeHash other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
    }

    /// <summary>
    /// 06_Networking section 6.3's session directory entry, narrowed to
    /// exactly the field subset SLICE-02_IMPLEMENTATION_BACKLOG section 5
    /// names as needed for this task (SessionId, HostUserId, JoinCodeHash,
    /// Status), plus MaxParticipants (06_Networking section 6.4's MVP cap of
    /// 12) -- required to make the "session full" rejection scenario this
    /// task's own instruction asks for meaningful, not an invented field.
    /// </summary>
    public sealed class SessionDirectoryEntry
    {
        public const int DefaultMaxParticipants = 12;

        public SessionDirectoryEntry(SessionId sessionId, UserId hostUserId, JoinCodeHash joinCodeHash, int maxParticipants, UtcInstant createdAt)
        {
            if (!sessionId.IsValid) throw new ArgumentException("SessionId is required.", nameof(sessionId));
            if (!hostUserId.IsValid) throw new ArgumentException("HostUserId is required.", nameof(hostUserId));
            if (!joinCodeHash.IsValid) throw new ArgumentException("JoinCodeHash is required.", nameof(joinCodeHash));
            if (maxParticipants < 1 || maxParticipants > DefaultMaxParticipants) throw new ArgumentOutOfRangeException(nameof(maxParticipants));
            if (!createdAt.IsValid) throw new ArgumentException("CreatedAt is required.", nameof(createdAt));

            SessionId = sessionId;
            HostUserId = hostUserId;
            JoinCodeHash = joinCodeHash;
            Status = SessionStatus.Lobby;
            MaxParticipants = maxParticipants;
            CreatedAt = createdAt;
        }

        public SessionId SessionId { get; }
        public UserId HostUserId { get; }
        public JoinCodeHash JoinCodeHash { get; }
        public SessionStatus Status { get; }
        public int MaxParticipants { get; }
        public UtcInstant CreatedAt { get; }
    }

    public sealed class SessionMember
    {
        public SessionMember(UserId userId, MemberAdmissionState state, BaselineRole role)
        {
            if (!userId.IsValid) throw new ArgumentException("UserId is required.", nameof(userId));
            UserId = userId;
            State = state;
            Role = role;
        }

        public UserId UserId { get; }
        public MemberAdmissionState State { get; }
        public BaselineRole Role { get; }

        public SessionMember WithRoleAssigned(BaselineRole role) => new SessionMember(UserId, MemberAdmissionState.RoleAssigned, role);
    }

    public static class SessionAdmissionFailures
    {
        public static Error JoinCodeInvalid(CorrelationId correlationId) => Error.Create(
            ErrorCodes.NetworkingSessionJoinCodeInvalid,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.networking.session_join_code_invalid"),
            RetryDirective.DoNotRetry,
            correlationId);

        public static Error CapacityReached(CorrelationId correlationId) => Error.Create(
            ErrorCodes.NetworkingSessionCapacityReached,
            ErrorCategory.Capacity,
            SafeReasonCode.CapacityReached,
            UserMessageKey.Parse("errors.networking.session_capacity_reached"),
            RetryDirective.DoNotRetry,
            correlationId);

        public static Error RoleAssignmentDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.NetworkingSessionRoleAssignmentDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.networking.session_role_assignment_denied"),
            RetryDirective.DoNotRetry,
            correlationId);

        public static Error MemberNotFound(CorrelationId correlationId) => Error.Create(
            ErrorCodes.NetworkingSessionMemberNotFound,
            ErrorCategory.NotFound,
            SafeReasonCode.TargetUnavailable,
            UserMessageKey.Parse("errors.networking.session_member_not_found"),
            RetryDirective.DoNotRetry,
            correlationId);
    }
}
