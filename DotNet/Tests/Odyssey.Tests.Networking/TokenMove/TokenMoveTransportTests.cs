using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Odyssey.Application.Networking;
using Odyssey.Application.Networking.Command;
using Odyssey.Application.Networking.Projection;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Networking.Command;
using Odyssey.Networking.InProcess;
using Odyssey.Networking.Session;
using Odyssey.Tests.Networking;

namespace Odyssey.Tests.Networking.TokenMove
{
    /// <summary>
    /// ODY-S02-011: the token-move validate-and-broadcast flow carried for
    /// real over at least two connected InProcessSessionTransport sides
    /// (ADR-015) -- roadmap 11.6 steps 5-7: a player moves a token, the host
    /// validates it, and every entitled connection converges to the same
    /// authoritative result (roadmap 11.7 exit criterion 2, "host is the
    /// sole authority").
    /// </summary>
    public sealed class TokenMoveTransportTests
    {
        private static readonly IWallClock Clock = new SystemWallClock();
        private static readonly UserId HostUser = UserId.Parse("user_00000000000000000000000000000001");
        private static readonly UserId PlayerUser = UserId.Parse("user_00000000000000000000000000000002");
        private static readonly UserId OtherPlayerUser = UserId.Parse("user_00000000000000000000000000000003");
        private static readonly UserId ObserverUser = UserId.Parse("user_00000000000000000000000000000004");

        private static async Task<(ISessionTransport Host, ISessionTransport Client, ConnectionHandle HostHandle, ConnectionHandle ClientHandle)> ConnectPairAsync()
        {
            var range = new ProtocolVersionRange(ProtocolVersion.Create(1), ProtocolVersion.Create(1), ProtocolVersion.Create(1));
            (ISessionTransport host, ISessionTransport client) = InProcessSessionTransport.CreatePair(range, range, Clock);
            Result<ConnectionHandle> clientConnected = await client.ConnectAsync(new SessionEndpoint("host-1"), range, CancellationToken.None);
            Result<ConnectionHandle> hostConnected = await host.ConnectAsync(new SessionEndpoint("client-1"), range, CancellationToken.None);
            Assert.That(clientConnected.IsSuccess, Is.True);
            Assert.That(hostConnected.IsSuccess, Is.True);
            return (host, client, hostConnected.Value, clientConnected.Value);
        }

        private static (SessionAdmissionState Admission, JoinCode JoinCode, Scene Scene, TokenMoveSessionState State) BuildFixture()
        {
            (SessionAdmissionState admission, JoinCode joinCode) = SessionAdmissionService.CreateSession(HostUser, Clock);
            SessionAdmissionService.TryJoin(admission, joinCode, PlayerUser);
            SessionAdmissionService.AssignRole(admission, HostUser, PlayerUser, BaselineRole.Player);
            SessionAdmissionService.TryJoin(admission, joinCode, OtherPlayerUser);
            SessionAdmissionService.AssignRole(admission, HostUser, OtherPlayerUser, BaselineRole.Player);
            SessionAdmissionService.TryJoin(admission, joinCode, ObserverUser); // stays Observer (default preset)

            Scene scene = new Scene(admission.Directory.SessionId.ToString());
            scene.AddEntity(new SceneEntity("loc_flag", "Capture Flag", SceneEntityVisibility.Public, assignedToUserId: null));
            scene.AddEntity(new SceneEntity("token_player_hero", "Player Hero", SceneEntityVisibility.HiddenGameplay, PlayerUser));

            TokenMoveSessionState state = new TokenMoveSessionState(scene, new SceneMutableState(scene));
            return (admission, joinCode, scene, state);
        }

        [Test]
        public async Task ValidMove_OnPublicEntity_BothPlayerAndObserverClientsConverge_OverRealTransport()
        {
            (SessionAdmissionState admission, _, Scene scene, TokenMoveSessionState state) = BuildFixture();
            (ISessionTransport hostSidePlayer, ISessionTransport clientSidePlayer, ConnectionHandle hostHandlePlayer, ConnectionHandle clientHandlePlayer) = await ConnectPairAsync();
            (ISessionTransport hostSideObserver, ISessionTransport clientSideObserver, ConnectionHandle hostHandleObserver, ConnectionHandle clientHandleObserver) = await ConnectPairAsync();

            MoveTokenCommand command = new MoveTokenCommand(Odyssey.Application.Commands.CommandId.Parse("cmd_00000000000000000000000000000010"), admission.Directory.SessionId, HostUser, "loc_flag", new TokenPosition(7, 8), expectedRevision: 1);
            Result<TokenMoveOutcome> executed = MoveTokenService.Execute(state, admission, command);
            Assert.That(executed.IsSuccess, Is.True);

            IReadOnlyList<TokenMovedDelta> deltas = DeltaBroadcastPlanner.PlanBroadcast(scene, admission, state.MutableState, executed.Value, Clock);
            var connections = new Dictionary<UserId, (ISessionTransport Transport, ConnectionHandle Handle)>
            {
                [PlayerUser] = (hostSidePlayer, hostHandlePlayer),
                [ObserverUser] = (hostSideObserver, hostHandleObserver)
            };
            Result broadcast = await TokenMoveHostChannel.BroadcastDeltaAsync(connections, deltas, Clock, CancellationToken.None);
            Assert.That(broadcast.IsSuccess, Is.True);

            Result<IReadOnlyList<TokenMovedDeltaMessage>> playerDeltas = TokenMoveClientChannel.DrainDeltas(clientSidePlayer, clientHandlePlayer);
            Result<IReadOnlyList<TokenMovedDeltaMessage>> observerDeltas = TokenMoveClientChannel.DrainDeltas(clientSideObserver, clientHandleObserver);
            Assert.That(playerDeltas.IsSuccess, Is.True);
            Assert.That(observerDeltas.IsSuccess, Is.True);
            Assert.That(playerDeltas.Value.Count, Is.EqualTo(1));
            Assert.That(observerDeltas.Value.Count, Is.EqualTo(1));
            Assert.That(playerDeltas.Value[0].X, Is.EqualTo(observerDeltas.Value[0].X));
            Assert.That(playerDeltas.Value[0].Y, Is.EqualTo(observerDeltas.Value[0].Y));
            Assert.That(playerDeltas.Value[0].EntityRevision, Is.EqualTo(observerDeltas.Value[0].EntityRevision));
            Assert.That(playerDeltas.Value[0].EntityId, Is.EqualTo("loc_flag"));
        }

        [Test]
        public async Task InvalidMove_NotOwnToken_ReturnsTypedRejection_OverRealTransport_NoDeltaBroadcast()
        {
            (SessionAdmissionState admission, _, Scene scene, TokenMoveSessionState state) = BuildFixture();
            (ISessionTransport hostSideRequester, ISessionTransport clientSideRequester, ConnectionHandle hostHandleRequester, ConnectionHandle clientHandleRequester) = await ConnectPairAsync();
            (ISessionTransport hostSideObserver, ISessionTransport clientSideObserver, ConnectionHandle hostHandleObserver, ConnectionHandle clientHandleObserver) = await ConnectPairAsync();

            MoveTokenCommand command = new MoveTokenCommand(Odyssey.Application.Commands.CommandId.Parse("cmd_00000000000000000000000000000011"), admission.Directory.SessionId, OtherPlayerUser, "token_player_hero", new TokenPosition(1, 1), expectedRevision: 1);
            await TokenMoveClientChannel.SendMoveRequestAsync(clientSideRequester, clientHandleRequester, command, Clock, CancellationToken.None);

            Result<IReadOnlyList<TokenMoveOutcome>> processed = await TokenMoveHostChannel.ProcessPendingRequestsAsync(hostSideRequester, hostHandleRequester, state, admission, Clock, CancellationToken.None);
            Assert.That(processed.IsSuccess, Is.True);
            Assert.That(processed.Value.Count, Is.EqualTo(0));

            Result<IReadOnlyList<MoveTokenOutcomeMessage>> outcomes = TokenMoveClientChannel.DrainOutcomes(clientSideRequester, clientHandleRequester);
            Assert.That(outcomes.IsSuccess, Is.True);
            Assert.That(outcomes.Value.Count, Is.EqualTo(1));
            Assert.That(outcomes.Value[0].Success, Is.False);
            Assert.That(outcomes.Value[0].ErrorCode, Is.EqualTo(ErrorCodes.NetworkingCommandTokenMoveDenied.ToString()));

            // No accepted outcome -> no broadcast is even attempted; confirm no
            // connected side (not even an unrelated Observer) received a delta.
            Result<IReadOnlyList<TokenMovedDeltaMessage>> observerDeltas = TokenMoveClientChannel.DrainDeltas(clientSideObserver, clientHandleObserver);
            Assert.That(observerDeltas.IsSuccess, Is.True);
            Assert.That(observerDeltas.Value.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task ValidMove_OnHiddenEntity_ObserverWithoutVisibility_ReceivesNoDelta_OverRealTransport()
        {
            (SessionAdmissionState admission, _, Scene scene, TokenMoveSessionState state) = BuildFixture();
            (ISessionTransport hostSidePlayer, ISessionTransport clientSidePlayer, ConnectionHandle hostHandlePlayer, ConnectionHandle clientHandlePlayer) = await ConnectPairAsync();
            (ISessionTransport hostSideObserver, ISessionTransport clientSideObserver, ConnectionHandle hostHandleObserver, ConnectionHandle clientHandleObserver) = await ConnectPairAsync();

            MoveTokenCommand command = new MoveTokenCommand(Odyssey.Application.Commands.CommandId.Parse("cmd_00000000000000000000000000000012"), admission.Directory.SessionId, PlayerUser, "token_player_hero", new TokenPosition(2, 2), expectedRevision: 1);
            await TokenMoveClientChannel.SendMoveRequestAsync(clientSidePlayer, clientHandlePlayer, command, Clock, CancellationToken.None);

            Result<IReadOnlyList<TokenMoveOutcome>> processed = await TokenMoveHostChannel.ProcessPendingRequestsAsync(hostSidePlayer, hostHandlePlayer, state, admission, Clock, CancellationToken.None);
            Assert.That(processed.IsSuccess, Is.True);
            Assert.That(processed.Value.Count, Is.EqualTo(1));

            IReadOnlyList<TokenMovedDelta> deltas = DeltaBroadcastPlanner.PlanBroadcast(scene, admission, state.MutableState, processed.Value[0], Clock);
            var connections = new Dictionary<UserId, (ISessionTransport Transport, ConnectionHandle Handle)>
            {
                [PlayerUser] = (hostSidePlayer, hostHandlePlayer),
                [ObserverUser] = (hostSideObserver, hostHandleObserver)
            };
            Result broadcast = await TokenMoveHostChannel.BroadcastDeltaAsync(connections, deltas, Clock, CancellationToken.None);
            Assert.That(broadcast.IsSuccess, Is.True);

            Result<IReadOnlyList<TokenMovedDeltaMessage>> playerDeltas = TokenMoveClientChannel.DrainDeltas(clientSidePlayer, clientHandlePlayer);
            Result<IReadOnlyList<TokenMovedDeltaMessage>> observerDeltas = TokenMoveClientChannel.DrainDeltas(clientSideObserver, clientHandleObserver);
            Assert.That(playerDeltas.IsSuccess, Is.True);
            Assert.That(observerDeltas.IsSuccess, Is.True);
            Assert.That(playerDeltas.Value.Count, Is.EqualTo(1));
            Assert.That(observerDeltas.Value.Count, Is.EqualTo(0));
        }
    }
}
