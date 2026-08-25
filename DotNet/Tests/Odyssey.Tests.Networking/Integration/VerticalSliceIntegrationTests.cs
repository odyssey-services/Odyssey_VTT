using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Networking;
using Odyssey.Application.Networking.Command;
using Odyssey.Application.Networking.Projection;
using Odyssey.Application.Networking.Reconnect;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Networking.Command;
using Odyssey.Networking.InProcess;
using Odyssey.Networking.Projection;
using Odyssey.Networking.Reconnect;
using Odyssey.Networking.Session;
using Odyssey.Tests.Networking;

namespace Odyssey.Tests.Networking.Integration
{
    /// <summary>
    /// ODY-S02-013: roadmap 11.6's ten-step "Первая сеть" vertical slice,
    /// run end-to-end, in order, as one automated test -- the same
    /// "integration proof, not a new feature" role ODY-S01-013 played for
    /// SLICE-01. Composes ODY-S02-009 (SessionAdmissionService/Channels),
    /// ODY-S02-010 (Scene/VisibilityPolicy/ProjectionSnapshot),
    /// ODY-S02-011 (MoveTokenService/TokenMoveHostChannel), and ODY-S02-012
    /// (ContinuityBroadcastPlanner/ReconnectPlanner) purely through each
    /// task's own already-public API -- no new production code exists
    /// anywhere in this task's diff.
    ///
    /// Three participants over real InProcessSessionTransport: MainGM
    /// (host, local authority, no transport pair of its own), Player
    /// (assigned a HiddenGameplay entity, the actor for steps 4-10), and
    /// Observer (default admission preset, used to cross-check redaction at
    /// step 4 and convergence at step 7 -- the same three-role pattern
    /// ODY-S02-010/011/012's own test suites already established).
    ///
    /// Step 5-10's moved entity is "token_marker" (Public), not Player's own
    /// HiddenGameplay "token_hero" -- deliberately, so that step 7's "both
    /// clients see the same result" and step 10's reconnect catch-up can be
    /// demonstrated using the two actually-connected clients (Player,
    /// Observer) without fabricating a third transport pair to represent
    /// the host as its own network audience. "token_hero" still proves
    /// step 4's redaction (Observer's snapshot excludes it; Player's does
    /// not).
    /// </summary>
    public sealed class VerticalSliceIntegrationTests
    {
        private static readonly IWallClock Clock = new SystemWallClock();
        private static readonly UserId HostUser = UserId.Parse("user_00000000000000000000000000000001");
        private static readonly UserId PlayerUser = UserId.Parse("user_00000000000000000000000000000002");
        private static readonly UserId ObserverUser = UserId.Parse("user_00000000000000000000000000000003");

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

        [Test]
        public async Task TenStepSlice_HostStartsSession_ThroughReconnectWithoutReplay_AllStepsSucceed()
        {
            // ---- Step 1: host starts a session (ODY-S02-009, local action, no wire message) ----
            (SessionAdmissionState admission, JoinCode joinCode) = SessionAdmissionService.CreateSession(HostUser, Clock);
            Assert.That(admission.Directory.HostUserId, Is.EqualTo(HostUser));
            Assert.That(admission.Members[HostUser.ToString()].Role, Is.EqualTo(BaselineRole.MainGM));

            (ISessionTransport hostSidePlayer, ISessionTransport clientSidePlayer, ConnectionHandle hostHandlePlayer, ConnectionHandle clientHandlePlayer) = await ConnectPairAsync();
            (ISessionTransport hostSideObserver, ISessionTransport clientSideObserver, ConnectionHandle hostHandleObserver, ConnectionHandle clientHandleObserver) = await ConnectPairAsync();

            // ---- Step 2: player (and, for redaction cross-checking, an observer) joins by code (ODY-S02-009, over the wire) ----
            await SessionAdmissionClientChannel.SendJoinRequestAsync(clientSidePlayer, clientHandlePlayer, joinCode, PlayerUser, Clock, CancellationToken.None);
            Result<int> joinProcessed = await SessionAdmissionHostChannel.ProcessPendingRequestsAsync(hostSidePlayer, hostHandlePlayer, admission, Clock, CancellationToken.None);
            Assert.That(joinProcessed.IsSuccess, Is.True);
            Assert.That(joinProcessed.Value, Is.EqualTo(1));
            Result<IReadOnlyList<AdmissionOutcomeMessage>> playerJoinOutcome = SessionAdmissionClientChannel.DrainOutcomes(clientSidePlayer, clientHandlePlayer);
            Assert.That(playerJoinOutcome.Value.Count, Is.EqualTo(1));
            Assert.That(playerJoinOutcome.Value[0].Success, Is.True);
            Assert.That(playerJoinOutcome.Value[0].Role, Is.EqualTo(BaselineRole.Observer.ToString())); // default preset, 06_Networking section 37.1

            await SessionAdmissionClientChannel.SendJoinRequestAsync(clientSideObserver, clientHandleObserver, joinCode, ObserverUser, Clock, CancellationToken.None);
            Result<int> observerJoinProcessed = await SessionAdmissionHostChannel.ProcessPendingRequestsAsync(hostSideObserver, hostHandleObserver, admission, Clock, CancellationToken.None);
            Assert.That(observerJoinProcessed.Value, Is.EqualTo(1));
            SessionAdmissionClientChannel.DrainOutcomes(clientSideObserver, clientHandleObserver);

            // ---- Step 3: GM assigns Player's role (ODY-S02-009, over the wire, issued from the host's own connection per established precedent) ----
            await SessionAdmissionClientChannel.SendRoleAssignmentRequestAsync(hostSidePlayer, hostHandlePlayer, HostUser, PlayerUser, BaselineRole.Player, Clock, CancellationToken.None);
            Result<int> roleProcessed = await SessionAdmissionHostChannel.ProcessPendingRequestsAsync(clientSidePlayer, clientHandlePlayer, admission, Clock, CancellationToken.None);
            Assert.That(roleProcessed.Value, Is.EqualTo(1));
            Result<IReadOnlyList<AdmissionOutcomeMessage>> roleOutcome = SessionAdmissionClientChannel.DrainOutcomes(hostSidePlayer, hostHandlePlayer);
            Assert.That(roleOutcome.Value[0].Success, Is.True);
            Assert.That(roleOutcome.Value[0].Role, Is.EqualTo(BaselineRole.Player.ToString()));
            Assert.That(admission.Members[PlayerUser.ToString()].Role, Is.EqualTo(BaselineRole.Player));

            // ---- Step 4: player receives the permitted scene, redacted (ODY-S02-010, over the wire) ----
            Scene scene = new Scene(admission.Directory.SessionId.ToString());
            scene.AddEntity(new SceneEntity("token_marker", "Player Marker (public)", SceneEntityVisibility.Public, PlayerUser));
            scene.AddEntity(new SceneEntity("token_hero", "Player Hero", SceneEntityVisibility.HiddenGameplay, PlayerUser));

            ProjectionSnapshot playerSnapshot = SceneProjectionBuilder.BuildSnapshot(admission.Directory.SessionId, scene, new ActorVisibilityContext(PlayerUser, BaselineRole.Player), 1, 1, 1, Clock);
            await SceneProjectionHostChannel.SendSnapshotAsync(hostSidePlayer, hostHandlePlayer, playerSnapshot, Clock, CancellationToken.None);
            Result<IReadOnlyList<ProjectionSnapshot>> playerReceivedSnapshot = SceneProjectionClientChannel.DrainSnapshots(clientSidePlayer, clientHandlePlayer);
            Assert.That(playerReceivedSnapshot.Value.Count, Is.EqualTo(1));
            Assert.That(playerReceivedSnapshot.Value[0].VisibleEntities.Count, Is.EqualTo(2)); // loc_flag (public) + token_hero (assigned)

            ProjectionSnapshot observerSnapshot = SceneProjectionBuilder.BuildSnapshot(admission.Directory.SessionId, scene, new ActorVisibilityContext(ObserverUser, BaselineRole.Observer), 1, 1, 1, Clock);
            await SceneProjectionHostChannel.SendSnapshotAsync(hostSideObserver, hostHandleObserver, observerSnapshot, Clock, CancellationToken.None);
            Result<IReadOnlyList<ProjectionSnapshot>> observerReceivedSnapshot = SceneProjectionClientChannel.DrainSnapshots(clientSideObserver, clientHandleObserver);
            Assert.That(observerReceivedSnapshot.Value[0].VisibleEntities.Count, Is.EqualTo(1)); // loc_flag only -- token_hero correctly redacted

            // ---- Step 5: player moves a token (ODY-S02-011, over the wire) ----
            // Moves the Public "token_marker", not Player's own Hidden "token_hero",
            // so step 7's convergence can be shown between the two actually
            // connected clients (Player, Observer) without a third transport
            // pair standing in for the host as its own network audience.
            TokenMoveSessionState moveState = new TokenMoveSessionState(scene, new SceneMutableState(scene));
            MoveTokenCommand moveCommand = new MoveTokenCommand(CommandId.Parse("cmd_00000000000000000000000000000030"), admission.Directory.SessionId, PlayerUser, "token_marker", new TokenPosition(4, 5), expectedRevision: 1);
            await TokenMoveClientChannel.SendMoveRequestAsync(clientSidePlayer, clientHandlePlayer, moveCommand, Clock, CancellationToken.None);

            // ---- Step 6: host validates the command (ODY-S02-011) ----
            Result<IReadOnlyList<TokenMoveOutcome>> moveProcessed = await TokenMoveHostChannel.ProcessPendingRequestsAsync(hostSidePlayer, hostHandlePlayer, moveState, admission, Clock, CancellationToken.None);
            Assert.That(moveProcessed.IsSuccess, Is.True);
            Assert.That(moveProcessed.Value.Count, Is.EqualTo(1));
            TokenMoveOutcome acceptedMove = moveProcessed.Value[0];
            Assert.That(acceptedMove.Revision, Is.EqualTo(2));
            Result<IReadOnlyList<MoveTokenOutcomeMessage>> moveOutcomeMessage = TokenMoveClientChannel.DrainOutcomes(clientSidePlayer, clientHandlePlayer);
            Assert.That(moveOutcomeMessage.Value[0].Success, Is.True);

            // ---- Step 7: both clients converge on the same result (ODY-S02-011/012 composed) ----
            // ODY-S02-011's TokenMoveOutcome has no adapter into ODY-S02-012's
            // ContinuityBroadcastPlanner (which takes raw entityId/position/
            // revision, not a TokenMoveOutcome) -- see the task contract's
            // "found gap" note. Fields are unpacked manually here.
            ReconnectSessionState reconnectState = new ReconnectSessionState(scene, new SessionDeltaBuffer());
            var connections = new Dictionary<UserId, (ISessionTransport Transport, ConnectionHandle Handle)>
            {
                [PlayerUser] = (hostSidePlayer, hostHandlePlayer),
                [ObserverUser] = (hostSideObserver, hostHandleObserver)
            };
            var liveTargets = ContinuityBroadcastPlanner.RecordAndPlanImmediateBroadcast(reconnectState, admission, acceptedMove.EntityId, acceptedMove.Position, acceptedMove.Revision, new HashSet<UserId>(connections.Keys), Clock);
            Assert.That(liveTargets.Count, Is.EqualTo(2)); // loc_flag is Public -- both Player and Observer are entitled
            await ContinuityHostChannel.BroadcastLiveMoveAsync(reconnectState, connections, liveTargets, Clock, CancellationToken.None);

            var playerDrain1 = ContinuityClientChannel.DrainReconnectPayloads(clientSidePlayer, clientHandlePlayer);
            var observerDrain1 = ContinuityClientChannel.DrainReconnectPayloads(clientSideObserver, clientHandleObserver);
            Assert.That(playerDrain1.Value.Deltas.Count, Is.EqualTo(1));
            Assert.That(observerDrain1.Value.Deltas.Count, Is.EqualTo(1));
            Assert.That(playerDrain1.Value.Deltas[0].X, Is.EqualTo(observerDrain1.Value.Deltas[0].X));
            Assert.That(playerDrain1.Value.Deltas[0].Y, Is.EqualTo(observerDrain1.Value.Deltas[0].Y));
            Assert.That(playerDrain1.Value.Deltas[0].EntityRevision, Is.EqualTo(observerDrain1.Value.Deltas[0].EntityRevision));
            Assert.That(playerDrain1.Value.Deltas[0].X, Is.EqualTo("4"));

            // ---- Step 8: player loses connection (ODY-S02-012 -- removed from the connected-audience set) ----
            connections.Remove(PlayerUser);

            // While offline, MainGM moves the same entity again (host-local
            // action, no network round trip needed for the host's own move --
            // ADR-002 section 23.1) -- this is what step 10 must catch up on.
            MoveTokenCommand hostMoveCommand = new MoveTokenCommand(CommandId.Parse("cmd_00000000000000000000000000000031"), admission.Directory.SessionId, HostUser, "token_marker", new TokenPosition(9, 9), expectedRevision: 2);
            Result<TokenMoveOutcome> hostMoveResult = MoveTokenService.Execute(moveState, admission, hostMoveCommand);
            Assert.That(hostMoveResult.IsSuccess, Is.True);
            var offlineTargets = ContinuityBroadcastPlanner.RecordAndPlanImmediateBroadcast(reconnectState, admission, hostMoveResult.Value.EntityId, hostMoveResult.Value.Position, hostMoveResult.Value.Revision, new HashSet<UserId>(connections.Keys), Clock);
            Assert.That(offlineTargets.Select(target => target.Audience), Does.Not.Contain(PlayerUser)); // Player is offline, not targeted for live delivery
            await ContinuityHostChannel.BroadcastLiveMoveAsync(reconnectState, connections, offlineTargets, Clock, CancellationToken.None);
            ContinuityClientChannel.DrainReconnectPayloads(clientSideObserver, clientHandleObserver); // Observer, still connected, receives it live -- not this test's focus, just drained so it doesn't leak into a later assertion

            // ---- Step 9: player reconnects, on a brand-new transport pair, same UserId (ODY-S02-012) ----
            (ISessionTransport hostSidePlayer2, ISessionTransport clientSidePlayer2, ConnectionHandle hostHandlePlayer2, ConnectionHandle clientHandlePlayer2) = await ConnectPairAsync();
            await ContinuityClientChannel.SendReconnectRequestAsync(clientSidePlayer2, clientHandlePlayer2, PlayerUser, Clock, CancellationToken.None);
            Result<int> reconnectProcessed = await ContinuityHostChannel.ProcessReconnectRequestsAsync(hostSidePlayer2, hostHandlePlayer2, reconnectState, admission, admission.Directory.SessionId, Clock, CancellationToken.None);
            Assert.That(reconnectProcessed.IsSuccess, Is.True);
            Assert.That(reconnectProcessed.Value, Is.EqualTo(1));

            // ---- Step 10: player resumes at the current state, without the original command replaying (ODY-S02-012) ----
            var playerReconnectDrain = ContinuityClientChannel.DrainReconnectPayloads(clientSidePlayer2, clientHandlePlayer2);
            Assert.That(playerReconnectDrain.IsSuccess, Is.True);
            Assert.That(playerReconnectDrain.Value.Snapshots.Count, Is.EqualTo(0)); // within the buffer -- catch-up, not fallback
            Assert.That(playerReconnectDrain.Value.Deltas.Count, Is.EqualTo(1)); // exactly the one move that happened while offline -- not a replay of step 5's already-applied move
            Assert.That(playerReconnectDrain.Value.Deltas[0].EntityRevision, Is.EqualTo(3));
            Assert.That(playerReconnectDrain.Value.Deltas[0].X, Is.EqualTo("9"));

            moveState.MutableState.TryGetState("token_marker", out TokenPosition authoritativePosition, out long authoritativeRevision);
            Assert.That(authoritativeRevision, Is.EqualTo(3));
            Assert.That(playerReconnectDrain.Value.Deltas[0].X, Is.EqualTo(authoritativePosition.X.ToString("R", CultureInfo.InvariantCulture)));
            Assert.That(playerReconnectDrain.Value.Deltas[0].Y, Is.EqualTo(authoritativePosition.Y.ToString("R", CultureInfo.InvariantCulture)));
        }
    }
}
