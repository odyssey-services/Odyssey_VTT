using NUnit.Framework;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Tests.Networking;

namespace Odyssey.Tests.Networking.SessionAdmission
{
    /// <summary>
    /// ODY-S02-009: pure, transport-independent tests of the admission/lobby
    /// logic (roadmap 11.6 steps 1-3) -- SessionAdmissionService itself, no
    /// InProcessSessionTransport involved (that is
    /// SessionAdmissionTransportTests.cs).
    /// </summary>
    public sealed class SessionAdmissionServiceTests
    {
        private static readonly IWallClock Clock = new SystemWallClock();
        private static readonly UserId HostUser = UserId.Parse("user_00000000000000000000000000000001");
        private static readonly UserId JoiningUser = UserId.Parse("user_00000000000000000000000000000002");
        private static readonly UserId AnotherJoiningUser = UserId.Parse("user_00000000000000000000000000000003");

        [Test]
        public void CreateSession_AssignsHostAsMainGM_Immediately()
        {
            (SessionAdmissionState state, JoinCode _) = SessionAdmissionService.CreateSession(HostUser, Clock);

            Assert.That(state.Directory.HostUserId, Is.EqualTo(HostUser));
            Assert.That(state.Members.TryGetValue(HostUser.ToString(), out SessionMember? host), Is.True);
            Assert.That(host!.Role, Is.EqualTo(BaselineRole.MainGM));
            Assert.That(host.State, Is.EqualTo(MemberAdmissionState.RoleAssigned));
        }

        [Test]
        public void TryJoin_WithCorrectCode_AdmitsAsObserverByDefault()
        {
            (SessionAdmissionState state, JoinCode joinCode) = SessionAdmissionService.CreateSession(HostUser, Clock);

            Result<SessionMember> result = SessionAdmissionService.TryJoin(state, joinCode, JoiningUser);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Role, Is.EqualTo(BaselineRole.Observer), "06_Networking section 37.1: a newly approved user gets the Observer preset by default");
            Assert.That(result.Value.State, Is.EqualTo(MemberAdmissionState.Admitted));
        }

        [Test]
        public void TryJoin_WithWrongCode_ReturnsTypedJoinCodeInvalid_NoException()
        {
            (SessionAdmissionState state, JoinCode correctCode) = SessionAdmissionService.CreateSession(HostUser, Clock);
            string correctText = correctCode.ToString();
            string wrongText = correctText[0] == 'A' ? "B" + correctText.Substring(1) : "A" + correctText.Substring(1);
            JoinCode wrongCode = JoinCode.Parse(wrongText);

            Result<SessionMember> result = SessionAdmissionService.TryJoin(state, wrongCode, JoiningUser);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.NetworkingSessionJoinCodeInvalid));
            Assert.That(result.Error.Category, Is.EqualTo(ErrorCategory.Validation));
        }

        [Test]
        public void TryJoin_WhenSessionFull_ReturnsTypedCapacityReached()
        {
            (SessionAdmissionState state, JoinCode joinCode) = SessionAdmissionService.CreateSession(HostUser, Clock, maxParticipants: 1);

            // Host already occupies the single slot (capacity 1 includes the host).
            Result<SessionMember> result = SessionAdmissionService.TryJoin(state, joinCode, JoiningUser);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.NetworkingSessionCapacityReached));
            Assert.That(result.Error.Category, Is.EqualTo(ErrorCategory.Capacity));
        }

        [Test]
        public void TryJoin_SameUserIdTwice_IsIdempotent_ReturnsExistingMemberNotAnError()
        {
            (SessionAdmissionState state, JoinCode joinCode) = SessionAdmissionService.CreateSession(HostUser, Clock);
            Result<SessionMember> first = SessionAdmissionService.TryJoin(state, joinCode, JoiningUser);
            Assert.That(first.IsSuccess, Is.True);

            SessionAdmissionService.AssignRole(state, HostUser, JoiningUser, BaselineRole.Player);

            Result<SessionMember> second = SessionAdmissionService.TryJoin(state, joinCode, JoiningUser);

            Assert.That(second.IsSuccess, Is.True, "re-joining with an already-admitted UserId must not error");
            Assert.That(second.Value.Role, Is.EqualTo(BaselineRole.Player), "re-join must return the existing member as-is, including its already-assigned role, not reset it back to Observer");
            Assert.That(state.Members.Count, Is.EqualTo(2), "re-join must not create a duplicate member entry (host + the one joining user)");
        }

        [Test]
        public void AssignRole_ByHost_UpgradesAdmittedMemberToPlayer()
        {
            (SessionAdmissionState state, JoinCode joinCode) = SessionAdmissionService.CreateSession(HostUser, Clock);
            SessionAdmissionService.TryJoin(state, joinCode, JoiningUser);

            Result<SessionMember> result = SessionAdmissionService.AssignRole(state, HostUser, JoiningUser, BaselineRole.Player);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Role, Is.EqualTo(BaselineRole.Player));
            Assert.That(result.Value.State, Is.EqualTo(MemberAdmissionState.RoleAssigned));
        }

        [Test]
        public void AssignRole_ByNonHost_ReturnsTypedRoleAssignmentDenied()
        {
            (SessionAdmissionState state, JoinCode joinCode) = SessionAdmissionService.CreateSession(HostUser, Clock);
            SessionAdmissionService.TryJoin(state, joinCode, JoiningUser);
            SessionAdmissionService.TryJoin(state, joinCode, AnotherJoiningUser);

            // JoiningUser (not the host) attempts to assign a role.
            Result<SessionMember> result = SessionAdmissionService.AssignRole(state, JoiningUser, AnotherJoiningUser, BaselineRole.Player);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.NetworkingSessionRoleAssignmentDenied));
            Assert.That(result.Error.Category, Is.EqualTo(ErrorCategory.Authorization));
        }

        [Test]
        public void AssignRole_ToMainGM_IsRejected_PERM_INV_001()
        {
            (SessionAdmissionState state, JoinCode joinCode) = SessionAdmissionService.CreateSession(HostUser, Clock);
            SessionAdmissionService.TryJoin(state, joinCode, JoiningUser);

            Result<SessionMember> result = SessionAdmissionService.AssignRole(state, HostUser, JoiningUser, BaselineRole.MainGM);

            Assert.That(result.IsFailure, Is.True, "ADR-019 section 5.1/PERM-INV-001 section 7.2: the host may never assign a second MainGM");
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.NetworkingSessionRoleAssignmentDenied));
        }

        [Test]
        public void AssignRole_UnknownTarget_ReturnsTypedMemberNotFound()
        {
            (SessionAdmissionState state, JoinCode _) = SessionAdmissionService.CreateSession(HostUser, Clock);

            Result<SessionMember> result = SessionAdmissionService.AssignRole(state, HostUser, JoiningUser, BaselineRole.Player);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.NetworkingSessionMemberNotFound));
            Assert.That(result.Error.Category, Is.EqualTo(ErrorCategory.NotFound));
        }
    }
}
