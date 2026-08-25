using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Networking.Session
{
    /// <summary>
    /// ODY-S02-009: host-side authoritative session state -- the "Lobby" this
    /// task's own instruction asks for. Mutable, in-memory only (no
    /// persistence -- scene/campaign state is ODY-S02-010+ scope); one
    /// instance per active session, owned by the host process.
    /// </summary>
    public sealed class SessionAdmissionState
    {
        private readonly Dictionary<string, SessionMember> _members = new(StringComparer.Ordinal);

        internal SessionAdmissionState(SessionDirectoryEntry directory, SessionMember host)
        {
            Directory = directory;
            _members[host.UserId.ToString()] = host;
        }

        public SessionDirectoryEntry Directory { get; }
        public IReadOnlyDictionary<string, SessionMember> Members => _members;

        internal void Upsert(SessionMember member) => _members[member.UserId.ToString()] = member;
        internal bool TryGet(UserId userId, out SessionMember member) => _members.TryGetValue(userId.ToString(), out member!);
    }

    /// <summary>
    /// ODY-S02-009: pure, transport-independent admission logic -- roadmap
    /// 11.6 steps 1-3 (host starts a session, a player joins by code, the
    /// host assigns a role). No I/O, no persistence, fully deterministic
    /// given its inputs; directly testable without InProcessSessionTransport
    /// (the transport-carried path lives in Odyssey.Networking, see
    /// SessionAdmissionChannels.cs).
    /// </summary>
    public static class SessionAdmissionService
    {
        private static readonly CorrelationId PlaceholderCorrelationId = CorrelationId.Parse("corr_00000000000000000000000000000000");

        /// <summary>
        /// Roadmap 11.6 step 1: the host starts a session. The host is
        /// immediately and permanently MainGM (ADR-019 section 5.1/PERM-INV-001
        /// -- protected, never reassignable by AssignRole below). Returns the
        /// plaintext JoinCode once, for the host to share out-of-band
        /// (06_Networking section 6.2) -- only its hash is ever stored.
        /// </summary>
        public static (SessionAdmissionState State, JoinCode PlaintextJoinCode) CreateSession(UserId hostUserId, IWallClock clock, int maxParticipants = SessionDirectoryEntry.DefaultMaxParticipants)
        {
            if (!hostUserId.IsValid) throw new ArgumentException("HostUserId is required.", nameof(hostUserId));
            if (clock == null) throw new ArgumentNullException(nameof(clock));

            UtcInstant now = clock.GetUtcNow();
            JoinCode joinCode = JoinCode.Generate();
            SessionId sessionId = SessionId.Parse("sess_" + DeterministicHex32(hostUserId.ToString(), now));
            SessionDirectoryEntry directory = new SessionDirectoryEntry(sessionId, hostUserId, JoinCodeHash.Of(joinCode), maxParticipants, now);
            SessionMember host = new SessionMember(hostUserId, MemberAdmissionState.RoleAssigned, BaselineRole.MainGM);
            return (new SessionAdmissionState(directory, host), joinCode);
        }

        /// <summary>
        /// Roadmap 11.6 step 2: a player joins by code. Idempotent for an
        /// already-admitted UserId (returns the existing member as-is,
        /// including any role the host already assigned) rather than an error
        /// or a duplicate entry -- a real second connection attempt by the
        /// same dev/mock actor (e.g. a client retry) must not fork session
        /// state. This is not ODY-S02-012's reconnect flow (no delta/state
        /// resume here) -- only "don't duplicate membership."
        /// </summary>
        public static Result<SessionMember> TryJoin(SessionAdmissionState state, JoinCode attemptedCode, UserId joiningUserId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!attemptedCode.IsValid) throw new ArgumentException("JoinCode is required.", nameof(attemptedCode));
            if (!joiningUserId.IsValid) throw new ArgumentException("JoiningUserId is required.", nameof(joiningUserId));

            if (state.TryGet(joiningUserId, out SessionMember existing))
            {
                return Result<SessionMember>.Success(existing);
            }

            if (!JoinCodeHash.Of(attemptedCode).Equals(state.Directory.JoinCodeHash))
            {
                return Result<SessionMember>.Failure(SessionAdmissionFailures.JoinCodeInvalid(PlaceholderCorrelationId));
            }

            if (state.Members.Count >= state.Directory.MaxParticipants)
            {
                return Result<SessionMember>.Failure(SessionAdmissionFailures.CapacityReached(PlaceholderCorrelationId));
            }

            // 06_Networking section 37.1: "Новый approved user получает Observer preset."
            SessionMember member = new SessionMember(joiningUserId, MemberAdmissionState.Admitted, BaselineRole.Observer);
            state.Upsert(member);
            return Result<SessionMember>.Success(member);
        }

        /// <summary>
        /// Roadmap 11.6 step 3: the host assigns a role. Only the session's
        /// own host may assign roles; only Player/Observer are assignable
        /// (never MainGM -- ADR-019 section 5.1/PERM-INV-001 section 7.2: "не
        /// может назначить другого MainGM"); the target must already be an
        /// admitted member.
        /// </summary>
        public static Result<SessionMember> AssignRole(SessionAdmissionState state, UserId requestingUserId, UserId targetUserId, BaselineRole role)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!requestingUserId.IsValid) throw new ArgumentException("RequestingUserId is required.", nameof(requestingUserId));
            if (!targetUserId.IsValid) throw new ArgumentException("TargetUserId is required.", nameof(targetUserId));

            if (!requestingUserId.Equals(state.Directory.HostUserId))
            {
                return Result<SessionMember>.Failure(SessionAdmissionFailures.RoleAssignmentDenied(PlaceholderCorrelationId));
            }

            if (role == BaselineRole.MainGM)
            {
                return Result<SessionMember>.Failure(SessionAdmissionFailures.RoleAssignmentDenied(PlaceholderCorrelationId));
            }

            if (!state.TryGet(targetUserId, out SessionMember target))
            {
                return Result<SessionMember>.Failure(SessionAdmissionFailures.MemberNotFound(PlaceholderCorrelationId));
            }

            SessionMember updated = target.WithRoleAssigned(role);
            state.Upsert(updated);
            return Result<SessionMember>.Success(updated);
        }

        private static string DeterministicHex32(string seed, UtcInstant now)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(seed + "|" + now));
            StringBuilder builder = new StringBuilder(32);
            for (int index = 0; index < 16; index++)
            {
                builder.Append(hash[index].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
