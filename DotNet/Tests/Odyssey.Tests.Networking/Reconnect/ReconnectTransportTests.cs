using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Odyssey.Application.Networking;
using Odyssey.Application.Networking.Projection;
using Odyssey.Application.Networking.Reconnect;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Networking.InProcess;
using Odyssey.Networking.Reconnect;
using Odyssey.Networking.Session;
using Odyssey.Tests.Networking;

namespace Odyssey.Tests.Networking.Reconnect
{
    /// <summary>
    /// ODY-S02-012: the reconnect/catch-up/fallback/dedup flow carried for
    /// real over InProcessSessionTransport (ADR-015) -- roadmap 11.6 steps
    /// 8-10, satisfying roadmap 11.7 exit criteria 3 (duplicate delivery)
    /// and 4 (reconnect restores scene/role). A player "losing connection"
    /// is modeled by removing their UserId from the host's connected-audience
    /// set (not by any transport-level Disconnect callback, which this mock
    /// transport does not gate delivery on) and "reconnecting" by
    /// establishing a brand-new InProcessSessionTransport pair for the same
    /// stable UserId (ADR-018) -- never the same ConnectionHandle.
    /// </summary>
    public sealed class ReconnectTransportTests
    {
        private static readonly IWallClock Clock = new SystemWallClock();
        private static readonly UserId HostUser = UserId.Parse("user_00000000000000000000000000000001");
        private static readonly UserId PlayerUser = UserId.Parse("user_00000000000000000000000000000002");

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

        private static (SessionAdmissionState Admission, Scene Scene, ReconnectSessionState State) BuildFixture()
        {
            (SessionAdmissionState admission, JoinCode joinCode) = SessionAdmissionService.CreateSession(HostUser, Clock);
            SessionAdmissionService.TryJoin(admission, joinCode, PlayerUser);
            SessionAdmissionService.AssignRole(admission, HostUser, PlayerUser, BaselineRole.Player);

            Scene scene = new Scene(admission.Directory.SessionId.ToString());
            scene.AddEntity(new SceneEntity("loc_flag", "Capture Flag", SceneEntityVisibility.Public, assignedToUserId: null));
            scene.AddEntity(new SceneEntity("token_hero", "Player Hero", SceneEntityVisibility.HiddenGameplay, PlayerUser));

            ReconnectSessionState state = new ReconnectSessionState(scene, new SessionDeltaBuffer(capacity: 3));
            return (admission, scene, state);
        }

        [Test]
        public async Task Reconnect_WithinBuffer_ReceivesMissingDeltas_NoFullSnapshot()
        {
            (SessionAdmissionState admission, _, ReconnectSessionState state) = BuildFixture();
            (ISessionTransport hostSide1, ISessionTransport clientSide1, ConnectionHandle hostHandle1, ConnectionHandle clientHandle1) = await ConnectPairAsync();
            var connections = new Dictionary<UserId, (ISessionTransport Transport, ConnectionHandle Handle)> { [PlayerUser] = (hostSide1, hostHandle1) };

            // Player connected: one live move, delivered and applied normally.
            var live = ContinuityBroadcastPlanner.RecordAndPlanImmediateBroadcast(state, admission, "loc_flag", new TokenPosition(1, 1), 2, new HashSet<UserId>(connections.Keys), Clock);
            await ContinuityHostChannel.BroadcastLiveMoveAsync(state, connections, live, Clock, CancellationToken.None);
            ClientProjectionState client = new ClientProjectionState();
            var firstDrain = ContinuityClientChannel.DrainReconnectPayloads(clientSide1, clientHandle1);
            foreach (var message in firstDrain.Value.Deltas) client.TryApply(message.BufferSequence, message.EntityId, new TokenPosition(1, 1));
            Assert.That(client.LastAppliedSequence, Is.EqualTo(1));

            // Player disconnects: no longer in the connected set, so further moves are buffered but not delivered.
            connections.Remove(PlayerUser);
            var missed1 = ContinuityBroadcastPlanner.RecordAndPlanImmediateBroadcast(state, admission, "loc_flag", new TokenPosition(2, 2), 3, new HashSet<UserId>(connections.Keys), Clock);
            Assert.That(missed1.Count, Is.EqualTo(0)); // nobody connected to receive it live
            var missed2 = ContinuityBroadcastPlanner.RecordAndPlanImmediateBroadcast(state, admission, "loc_flag", new TokenPosition(3, 3), 4, new HashSet<UserId>(connections.Keys), Clock);
            Assert.That(missed2.Count, Is.EqualTo(0));

            // Player reconnects on a brand-new transport pair, same UserId.
            (ISessionTransport hostSide2, ISessionTransport clientSide2, ConnectionHandle hostHandle2, ConnectionHandle clientHandle2) = await ConnectPairAsync();
            await ContinuityClientChannel.SendReconnectRequestAsync(clientSide2, clientHandle2, PlayerUser, Clock, CancellationToken.None);
            Result<int> processed = await ContinuityHostChannel.ProcessReconnectRequestsAsync(hostSide2, hostHandle2, state, admission, admission.Directory.SessionId, Clock, CancellationToken.None);
            Assert.That(processed.IsSuccess, Is.True);
            Assert.That(processed.Value, Is.EqualTo(1));

            Result<(IReadOnlyList<BufferedDeltaMessage> Deltas, IReadOnlyList<ProjectionSnapshot> Snapshots)> drained = ContinuityClientChannel.DrainReconnectPayloads(clientSide2, clientHandle2);
            Assert.That(drained.IsSuccess, Is.True);
            Assert.That(drained.Value.Snapshots.Count, Is.EqualTo(0)); // no full-snapshot fallback expected
            Assert.That(drained.Value.Deltas.Count, Is.EqualTo(2)); // the two missed moves

            foreach (var message in drained.Value.Deltas) client.TryApply(message.BufferSequence, message.EntityId, new TokenPosition(3, 3));
            Assert.That(client.LastAppliedSequence, Is.EqualTo(3));
        }

        [Test]
        public async Task Reconnect_OutsideBuffer_ReceivesFullSnapshot_NoCatchupDeltas()
        {
            (SessionAdmissionState admission, _, ReconnectSessionState state) = BuildFixture();
            (ISessionTransport hostSide1, ISessionTransport clientSide1, ConnectionHandle hostHandle1, ConnectionHandle clientHandle1) = await ConnectPairAsync();
            var connections = new Dictionary<UserId, (ISessionTransport Transport, ConnectionHandle Handle)> { [PlayerUser] = (hostSide1, hostHandle1) };

            var live = ContinuityBroadcastPlanner.RecordAndPlanImmediateBroadcast(state, admission, "loc_flag", new TokenPosition(1, 1), 2, new HashSet<UserId>(connections.Keys), Clock);
            await ContinuityHostChannel.BroadcastLiveMoveAsync(state, connections, live, Clock, CancellationToken.None);
            ContinuityClientChannel.DrainReconnectPayloads(clientSide1, clientHandle1); // consume, not asserted here

            connections.Remove(PlayerUser);
            // Buffer capacity is 3 -- 5 more moves while offline guarantees eviction beyond the missed range.
            for (int index = 0; index < 5; index++)
            {
                ContinuityBroadcastPlanner.RecordAndPlanImmediateBroadcast(state, admission, "loc_flag", new TokenPosition(index, index), index + 3, new HashSet<UserId>(connections.Keys), Clock);
            }

            (ISessionTransport hostSide2, ISessionTransport clientSide2, ConnectionHandle hostHandle2, ConnectionHandle clientHandle2) = await ConnectPairAsync();
            await ContinuityClientChannel.SendReconnectRequestAsync(clientSide2, clientHandle2, PlayerUser, Clock, CancellationToken.None);
            Result<int> processed = await ContinuityHostChannel.ProcessReconnectRequestsAsync(hostSide2, hostHandle2, state, admission, admission.Directory.SessionId, Clock, CancellationToken.None);
            Assert.That(processed.IsSuccess, Is.True);
            Assert.That(processed.Value, Is.EqualTo(1));

            Result<(IReadOnlyList<BufferedDeltaMessage> Deltas, IReadOnlyList<ProjectionSnapshot> Snapshots)> drained = ContinuityClientChannel.DrainReconnectPayloads(clientSide2, clientHandle2);
            Assert.That(drained.IsSuccess, Is.True);
            Assert.That(drained.Value.Deltas.Count, Is.EqualTo(0));
            Assert.That(drained.Value.Snapshots.Count, Is.EqualTo(1));
            Assert.That(drained.Value.Snapshots[0].VisibleEntities.Count, Is.EqualTo(2)); // Player: loc_flag (public) + token_hero (assigned)
        }

        [Test]
        public async Task RedeliveredSameBufferedDelta_IsNotAppliedTwice_OverRealTransport()
        {
            (SessionAdmissionState admission, _, ReconnectSessionState state) = BuildFixture();
            (ISessionTransport hostSide, ISessionTransport clientSide, ConnectionHandle hostHandle, ConnectionHandle clientHandle) = await ConnectPairAsync();
            var connections = new Dictionary<UserId, (ISessionTransport Transport, ConnectionHandle Handle)> { [PlayerUser] = (hostSide, hostHandle) };

            var live = ContinuityBroadcastPlanner.RecordAndPlanImmediateBroadcast(state, admission, "loc_flag", new TokenPosition(7, 7), 2, new HashSet<UserId>(connections.Keys), Clock);
            await ContinuityHostChannel.BroadcastLiveMoveAsync(state, connections, live, Clock, CancellationToken.None);

            // Simulate an at-least-once network retry: the host resends the
            // exact same buffered entry a second time.
            await ContinuityHostChannel.BroadcastLiveMoveAsync(state, connections, live, Clock, CancellationToken.None);

            Result<(IReadOnlyList<BufferedDeltaMessage> Deltas, IReadOnlyList<ProjectionSnapshot> Snapshots)> drained = ContinuityClientChannel.DrainReconnectPayloads(clientSide, clientHandle);
            Assert.That(drained.IsSuccess, Is.True);
            Assert.That(drained.Value.Deltas.Count, Is.EqualTo(2)); // both bytes-on-the-wire arrive -- dedup happens client-side, not at delivery

            ClientProjectionState client = new ClientProjectionState();
            bool firstApplied = client.TryApply(drained.Value.Deltas[0].BufferSequence, drained.Value.Deltas[0].EntityId, new TokenPosition(7, 7));
            bool secondApplied = client.TryApply(drained.Value.Deltas[1].BufferSequence, drained.Value.Deltas[1].EntityId, new TokenPosition(999, 999));

            Assert.That(firstApplied, Is.True);
            Assert.That(secondApplied, Is.False);
            Assert.That(client.Positions["loc_flag"].X, Is.EqualTo(7));
            Assert.That(client.LastAppliedSequence, Is.EqualTo(1));
        }

        [Test]
        public async Task Reconnect_AfterRoleRevokedWhileDisconnected_DoesNotDeliverNowInvisibleEntity()
        {
            (SessionAdmissionState admission, _, ReconnectSessionState state) = BuildFixture();
            (ISessionTransport hostSide1, ISessionTransport clientSide1, ConnectionHandle hostHandle1, ConnectionHandle clientHandle1) = await ConnectPairAsync();
            var connections = new Dictionary<UserId, (ISessionTransport Transport, ConnectionHandle Handle)> { [PlayerUser] = (hostSide1, hostHandle1) };

            var live = ContinuityBroadcastPlanner.RecordAndPlanImmediateBroadcast(state, admission, "token_hero", new TokenPosition(0, 0), 2, new HashSet<UserId>(connections.Keys), Clock);
            await ContinuityHostChannel.BroadcastLiveMoveAsync(state, connections, live, Clock, CancellationToken.None);
            ContinuityClientChannel.DrainReconnectPayloads(clientSide1, clientHandle1);

            connections.Remove(PlayerUser);
            ContinuityBroadcastPlanner.RecordAndPlanImmediateBroadcast(state, admission, "token_hero", new TokenPosition(1, 1), 3, new HashSet<UserId>(connections.Keys), Clock);

            // Host revokes the Player role (downgrades to Observer) while offline.
            Result<SessionMember> revoked = SessionAdmissionService.AssignRole(admission, HostUser, PlayerUser, BaselineRole.Observer);
            Assert.That(revoked.IsSuccess, Is.True);

            (ISessionTransport hostSide2, ISessionTransport clientSide2, ConnectionHandle hostHandle2, ConnectionHandle clientHandle2) = await ConnectPairAsync();
            await ContinuityClientChannel.SendReconnectRequestAsync(clientSide2, clientHandle2, PlayerUser, Clock, CancellationToken.None);
            await ContinuityHostChannel.ProcessReconnectRequestsAsync(hostSide2, hostHandle2, state, admission, admission.Directory.SessionId, Clock, CancellationToken.None);

            Result<(IReadOnlyList<BufferedDeltaMessage> Deltas, IReadOnlyList<ProjectionSnapshot> Snapshots)> drained = ContinuityClientChannel.DrainReconnectPayloads(clientSide2, clientHandle2);
            Assert.That(drained.IsSuccess, Is.True);
            Assert.That(drained.Value.Deltas.Count, Is.EqualTo(0)); // token_hero is now Hidden-and-unassigned-to-Observer -- not delivered
            Assert.That(drained.Value.Snapshots.Count, Is.EqualTo(0)); // still within buffer -- catch-up path, not fallback
        }
    }
}
