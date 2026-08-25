using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Networking.Command;
using Odyssey.Application.Networking.Projection;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;

namespace Odyssey.Tests.Networking.TokenMove
{
    /// <summary>
    /// ODY-S02-011: pure, transport-independent MoveTokenService logic --
    /// roadmap 11.6 steps 5-6 (host validates a token-move command) without
    /// needing InProcessSessionTransport (the transport-carried path lives
    /// in TokenMoveTransportTests.cs).
    /// </summary>
    public sealed class TokenMoveServiceTests
    {
        private static readonly IWallClock Clock = new SystemWallClock();
        private static readonly UserId HostUser = UserId.Parse("user_00000000000000000000000000000001");
        private static readonly UserId PlayerUser = UserId.Parse("user_00000000000000000000000000000002");
        private static readonly UserId OtherPlayerUser = UserId.Parse("user_00000000000000000000000000000003");

        private static (SessionAdmissionState Admission, Scene Scene, TokenMoveSessionState State) BuildFixture()
        {
            (SessionAdmissionState admission, JoinCode joinCode) = SessionAdmissionService.CreateSession(HostUser, Clock);
            SessionAdmissionService.TryJoin(admission, joinCode, PlayerUser);
            SessionAdmissionService.AssignRole(admission, HostUser, PlayerUser, BaselineRole.Player);
            SessionAdmissionService.TryJoin(admission, joinCode, OtherPlayerUser);
            SessionAdmissionService.AssignRole(admission, HostUser, OtherPlayerUser, BaselineRole.Player);

            Scene scene = new Scene(admission.Directory.SessionId.ToString());
            scene.AddEntity(new SceneEntity("token_player_hero", "Player Hero", SceneEntityVisibility.HiddenGameplay, PlayerUser));
            scene.AddEntity(new SceneEntity("npc_villain", "Villain", SceneEntityVisibility.HiddenGameplay, assignedToUserId: null));

            TokenMoveSessionState state = new TokenMoveSessionState(scene, new SceneMutableState(scene));
            return (admission, scene, state);
        }

        private static CommandId NewCommandId(string seed) => CommandId.Parse("cmd_" + seed.PadLeft(32, '0'));

        [Test]
        public void ValidMove_ByAssignedPlayer_AppliesAndReturnsIncrementedRevision()
        {
            (SessionAdmissionState admission, _, TokenMoveSessionState state) = BuildFixture();
            MoveTokenCommand command = new MoveTokenCommand(NewCommandId("1"), admission.Directory.SessionId, PlayerUser, "token_player_hero", new TokenPosition(3, 4), expectedRevision: 1);

            Result<TokenMoveOutcome> result = MoveTokenService.Execute(state, admission, command);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Revision, Is.EqualTo(2));
            Assert.That(result.Value.Position.X, Is.EqualTo(3));
            Assert.That(result.Value.Position.Y, Is.EqualTo(4));
        }

        [Test]
        public void ValidMove_ByMainGM_ForAnyEntity_Succeeds()
        {
            (SessionAdmissionState admission, _, TokenMoveSessionState state) = BuildFixture();
            MoveTokenCommand command = new MoveTokenCommand(NewCommandId("2"), admission.Directory.SessionId, HostUser, "npc_villain", new TokenPosition(5, 5), expectedRevision: 1);

            Result<TokenMoveOutcome> result = MoveTokenService.Execute(state, admission, command);

            Assert.That(result.IsSuccess, Is.True);
        }

        [Test]
        public void Move_ByNonOwningPlayer_ReturnsTypedActionNotAllowed()
        {
            (SessionAdmissionState admission, _, TokenMoveSessionState state) = BuildFixture();
            MoveTokenCommand command = new MoveTokenCommand(NewCommandId("3"), admission.Directory.SessionId, OtherPlayerUser, "token_player_hero", new TokenPosition(1, 1), expectedRevision: 1);

            Result<TokenMoveOutcome> result = MoveTokenService.Execute(state, admission, command);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.NetworkingCommandTokenMoveDenied));
        }

        [Test]
        public void Move_UnknownEntity_ReturnsTypedTokenNotFound()
        {
            (SessionAdmissionState admission, _, TokenMoveSessionState state) = BuildFixture();
            MoveTokenCommand command = new MoveTokenCommand(NewCommandId("4"), admission.Directory.SessionId, HostUser, "does_not_exist", new TokenPosition(1, 1), expectedRevision: 1);

            Result<TokenMoveOutcome> result = MoveTokenService.Execute(state, admission, command);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.NetworkingCommandTokenNotFound));
        }

        [Test]
        public void Move_StaleExpectedRevision_ReturnsTypedRevisionConflict()
        {
            (SessionAdmissionState admission, _, TokenMoveSessionState state) = BuildFixture();
            MoveTokenCommand command = new MoveTokenCommand(NewCommandId("5"), admission.Directory.SessionId, HostUser, "npc_villain", new TokenPosition(1, 1), expectedRevision: 99);

            Result<TokenMoveOutcome> result = MoveTokenService.Execute(state, admission, command);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.NetworkingCommandTokenRevisionConflict));
        }

        [Test]
        public void Move_DuplicateCommandId_SameParams_ReplaysStoredResult_DoesNotDoubleApply()
        {
            (SessionAdmissionState admission, _, TokenMoveSessionState state) = BuildFixture();
            CommandId commandId = NewCommandId("6");
            MoveTokenCommand command = new MoveTokenCommand(commandId, admission.Directory.SessionId, HostUser, "npc_villain", new TokenPosition(2, 2), expectedRevision: 1);

            Result<TokenMoveOutcome> first = MoveTokenService.Execute(state, admission, command);
            Result<TokenMoveOutcome> second = MoveTokenService.Execute(state, admission, command);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(second.IsSuccess, Is.True);
            Assert.That(second.Value.Revision, Is.EqualTo(first.Value.Revision));
            state.MutableState.TryGetState("npc_villain", out _, out long finalRevision);
            Assert.That(finalRevision, Is.EqualTo(2));
        }

        [Test]
        public void Move_DuplicateCommandId_DifferentParams_ReturnsTypedCommandIdentityMismatch()
        {
            (SessionAdmissionState admission, _, TokenMoveSessionState state) = BuildFixture();
            CommandId commandId = NewCommandId("7");
            MoveTokenCommand first = new MoveTokenCommand(commandId, admission.Directory.SessionId, HostUser, "npc_villain", new TokenPosition(2, 2), expectedRevision: 1);
            MoveTokenCommand second = new MoveTokenCommand(commandId, admission.Directory.SessionId, HostUser, "npc_villain", new TokenPosition(9, 9), expectedRevision: 1);

            MoveTokenService.Execute(state, admission, first);
            Result<TokenMoveOutcome> mismatch = MoveTokenService.Execute(state, admission, second);

            Assert.That(mismatch.IsSuccess, Is.False);
            Assert.That(mismatch.Error.Code, Is.EqualTo(ErrorCodes.CommandIdentityMismatch));
        }
    }
}
