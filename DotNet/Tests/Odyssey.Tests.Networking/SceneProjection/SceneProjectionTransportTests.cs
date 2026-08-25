using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Odyssey.Application.Networking;
using Odyssey.Application.Networking.Projection;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Networking.InProcess;
using Odyssey.Networking.Projection;
using Odyssey.Networking.Session;
using Odyssey.Tests.Networking;

namespace Odyssey.Tests.Networking.SceneProjection
{
    /// <summary>
    /// ODY-S02-010: the scene-snapshot delivery flow carried for real over
    /// InProcessSessionTransport (ADR-015) -- SendReliableAsync/DrainReliable,
    /// not a mocked-out transport. Builds on ODY-S02-009's
    /// SessionAdmissionService to admit a Player before delivering the
    /// redacted scene, exercising the same host/client process split a real
    /// deployment would have.
    /// </summary>
    public sealed class SceneProjectionTransportTests
    {
        private static readonly IWallClock Clock = new SystemWallClock();
        private static readonly UserId HostUser = UserId.Parse("user_00000000000000000000000000000001");
        private static readonly UserId JoiningUser = UserId.Parse("user_00000000000000000000000000000002");

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

        private static Scene BuildTestScene()
        {
            Scene scene = new Scene("scene_town_square_00000000000000000000001");
            scene.AddEntity(new SceneEntity("loc_town_square", "Town Square", SceneEntityVisibility.Public, assignedToUserId: null));
            scene.AddEntity(new SceneEntity("npc_secret_villain", "Secret Villain", SceneEntityVisibility.HiddenGameplay, assignedToUserId: null));
            scene.AddEntity(new SceneEntity("token_player_hero", "Player Hero", SceneEntityVisibility.HiddenGameplay, assignedToUserId: JoiningUser));
            return scene;
        }

        [Test]
        public async Task NewlyAdmittedObserver_ReceivesRedactedSnapshot_NoHiddenEntities_OverRealTransport()
        {
            (SessionAdmissionState state, JoinCode joinCode) = SessionAdmissionService.CreateSession(HostUser, Clock);
            Result<SessionMember> joined = SessionAdmissionService.TryJoin(state, joinCode, JoiningUser);
            Assert.That(joined.IsSuccess, Is.True);
            Assert.That(joined.Value.Role, Is.EqualTo(BaselineRole.Observer));

            Scene scene = BuildTestScene();
            ActorVisibilityContext context = new ActorVisibilityContext(JoiningUser, joined.Value.Role);
            ProjectionSnapshot snapshot = SceneProjectionBuilder.BuildSnapshot(state.Directory.SessionId, scene, context, baseSessionSequence: 1, projectionRevision: 1, permissionRevision: 1, Clock);

            (ISessionTransport hostTransport, ISessionTransport clientTransport, ConnectionHandle hostHandle, ConnectionHandle clientHandle) = await ConnectPairAsync();

            Result send = await SceneProjectionHostChannel.SendSnapshotAsync(hostTransport, hostHandle, snapshot, Clock, CancellationToken.None);
            Assert.That(send.IsSuccess, Is.True);

            Result<System.Collections.Generic.IReadOnlyList<ProjectionSnapshot>> received = SceneProjectionClientChannel.DrainSnapshots(clientTransport, clientHandle);
            Assert.That(received.IsSuccess, Is.True);
            Assert.That(received.Value.Count, Is.EqualTo(1));

            ProjectionSnapshot delivered = received.Value[0];
            Assert.That(delivered.SnapshotId, Is.EqualTo(snapshot.SnapshotId));
            Assert.That(delivered.PayloadHash, Is.EqualTo(snapshot.PayloadHash));
            Assert.That(delivered.VisibleEntities.Count, Is.EqualTo(1));
            Assert.That(delivered.VisibleEntities[0].EntityId, Is.EqualTo("loc_town_square"));
        }

        [Test]
        public async Task MainGM_ReceivesFullSnapshot_ControlCase_OverRealTransport()
        {
            (SessionAdmissionState state, JoinCode _) = SessionAdmissionService.CreateSession(HostUser, Clock);

            Scene scene = BuildTestScene();
            ActorVisibilityContext context = new ActorVisibilityContext(HostUser, BaselineRole.MainGM);
            ProjectionSnapshot snapshot = SceneProjectionBuilder.BuildSnapshot(state.Directory.SessionId, scene, context, baseSessionSequence: 1, projectionRevision: 1, permissionRevision: 1, Clock);

            (ISessionTransport hostTransport, ISessionTransport clientTransport, ConnectionHandle hostHandle, ConnectionHandle clientHandle) = await ConnectPairAsync();

            await SceneProjectionHostChannel.SendSnapshotAsync(hostTransport, hostHandle, snapshot, Clock, CancellationToken.None);
            Result<System.Collections.Generic.IReadOnlyList<ProjectionSnapshot>> received = SceneProjectionClientChannel.DrainSnapshots(clientTransport, clientHandle);

            Assert.That(received.IsSuccess, Is.True);
            Assert.That(received.Value.Count, Is.EqualTo(1));
            Assert.That(received.Value[0].VisibleEntities.Count, Is.EqualTo(3));
        }

        [Test]
        public async Task RepeatedSnapshotDelivery_SameUnchangedState_YieldsSamePayloadHash_OverRealTransport()
        {
            (SessionAdmissionState state, JoinCode _) = SessionAdmissionService.CreateSession(HostUser, Clock);
            Scene scene = BuildTestScene();
            ActorVisibilityContext context = new ActorVisibilityContext(HostUser, BaselineRole.MainGM);

            ProjectionSnapshot firstBuild = SceneProjectionBuilder.BuildSnapshot(state.Directory.SessionId, scene, context, baseSessionSequence: 5, projectionRevision: 2, permissionRevision: 1, Clock);
            ProjectionSnapshot secondBuild = SceneProjectionBuilder.BuildSnapshot(state.Directory.SessionId, scene, context, baseSessionSequence: 5, projectionRevision: 2, permissionRevision: 1, Clock);

            (ISessionTransport hostTransport, ISessionTransport clientTransport, ConnectionHandle hostHandle, ConnectionHandle clientHandle) = await ConnectPairAsync();

            await SceneProjectionHostChannel.SendSnapshotAsync(hostTransport, hostHandle, firstBuild, Clock, CancellationToken.None);
            await SceneProjectionHostChannel.SendSnapshotAsync(hostTransport, hostHandle, secondBuild, Clock, CancellationToken.None);

            Result<System.Collections.Generic.IReadOnlyList<ProjectionSnapshot>> received = SceneProjectionClientChannel.DrainSnapshots(clientTransport, clientHandle);
            Assert.That(received.IsSuccess, Is.True);
            Assert.That(received.Value.Count, Is.EqualTo(2));
            Assert.That(received.Value[0].PayloadHash, Is.EqualTo(received.Value[1].PayloadHash));
        }
    }
}
