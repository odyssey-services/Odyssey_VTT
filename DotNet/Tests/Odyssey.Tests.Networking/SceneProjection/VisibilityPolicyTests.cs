using NUnit.Framework;
using Odyssey.Application.Networking.Projection;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;

namespace Odyssey.Tests.Networking.SceneProjection
{
    /// <summary>
    /// ODY-S02-010: pure, transport-independent VisibilityPolicy/
    /// SceneProjectionBuilder logic -- roadmap 11.6 step 4's "scene delivered,
    /// correctly redacted by role" without needing InProcessSessionTransport
    /// (the transport-carried path lives in SceneProjectionTransportTests.cs).
    /// </summary>
    public sealed class VisibilityPolicyTests
    {
        private static readonly IWallClock Clock = new SystemWallClock();
        private static readonly SessionId TestSessionId = SessionId.Parse("sess_00000000000000000000000000000001");
        private static readonly UserId MainGmUser = UserId.Parse("user_00000000000000000000000000000001");
        private static readonly UserId PlayerUser = UserId.Parse("user_00000000000000000000000000000002");
        private static readonly UserId OtherPlayerUser = UserId.Parse("user_00000000000000000000000000000003");

        private static Scene BuildTestScene()
        {
            Scene scene = new Scene("scene_town_square_00000000000000000000001");
            scene.AddEntity(new SceneEntity("loc_town_square", "Town Square", SceneEntityVisibility.Public, assignedToUserId: null));
            scene.AddEntity(new SceneEntity("npc_secret_villain", "Secret Villain", SceneEntityVisibility.HiddenGameplay, assignedToUserId: null));
            scene.AddEntity(new SceneEntity("token_player_hero", "Player Hero", SceneEntityVisibility.HiddenGameplay, assignedToUserId: PlayerUser));
            return scene;
        }

        [Test]
        public void ComputeVisibleEntities_MainGM_SeesAllEntities_IncludingHidden()
        {
            Scene scene = BuildTestScene();
            ActorVisibilityContext context = new ActorVisibilityContext(MainGmUser, BaselineRole.MainGM);

            var visible = VisibilityPolicy.ComputeVisibleEntities(scene, context);

            Assert.That(visible.Count, Is.EqualTo(3));
        }

        [Test]
        public void ComputeVisibleEntities_Observer_SeesOnlyPublicEntities_NoHiddenGmData()
        {
            Scene scene = BuildTestScene();
            ActorVisibilityContext context = new ActorVisibilityContext(PlayerUser, BaselineRole.Observer);

            var visible = VisibilityPolicy.ComputeVisibleEntities(scene, context);

            Assert.That(visible.Count, Is.EqualTo(1));
            Assert.That(visible[0].EntityId, Is.EqualTo("loc_town_square"));
        }

        [Test]
        public void ComputeVisibleEntities_Player_SeesOwnAssignedHiddenEntity_ButNotOthers()
        {
            Scene scene = BuildTestScene();
            ActorVisibilityContext context = new ActorVisibilityContext(PlayerUser, BaselineRole.Player);

            var visible = VisibilityPolicy.ComputeVisibleEntities(scene, context);
            var visibleIds = new[] { visible[0].EntityId, visible[1].EntityId };

            Assert.That(visible.Count, Is.EqualTo(2));
            Assert.That(visibleIds, Does.Contain("loc_town_square"));
            Assert.That(visibleIds, Does.Contain("token_player_hero"));
        }

        [Test]
        public void ComputeVisibleEntities_OtherPlayer_DoesNotSeeEntityAssignedToDifferentPlayer()
        {
            Scene scene = BuildTestScene();
            ActorVisibilityContext context = new ActorVisibilityContext(OtherPlayerUser, BaselineRole.Player);

            var visible = VisibilityPolicy.ComputeVisibleEntities(scene, context);

            Assert.That(visible.Count, Is.EqualTo(1));
            Assert.That(visible[0].EntityId, Is.EqualTo("loc_town_square"));
        }

        [Test]
        public void BuildSnapshot_RepeatedCallSameSceneAndContext_ProducesSamePayloadHash_WhenStateUnchanged()
        {
            Scene scene = BuildTestScene();
            ActorVisibilityContext context = new ActorVisibilityContext(MainGmUser, BaselineRole.MainGM);

            ProjectionSnapshot first = SceneProjectionBuilder.BuildSnapshot(TestSessionId, scene, context, baseSessionSequence: 10, projectionRevision: 1, permissionRevision: 1, Clock);
            ProjectionSnapshot second = SceneProjectionBuilder.BuildSnapshot(TestSessionId, scene, context, baseSessionSequence: 10, projectionRevision: 1, permissionRevision: 1, Clock);

            Assert.That(second.PayloadHash, Is.EqualTo(first.PayloadHash));
            Assert.That(second.SnapshotId, Is.Not.EqualTo(first.SnapshotId));
        }

        [Test]
        public void BuildSnapshot_DifferentAudience_ProducesDifferentPayloadHash()
        {
            Scene scene = BuildTestScene();
            ActorVisibilityContext mainGmContext = new ActorVisibilityContext(MainGmUser, BaselineRole.MainGM);
            ActorVisibilityContext observerContext = new ActorVisibilityContext(PlayerUser, BaselineRole.Observer);

            ProjectionSnapshot mainGmSnapshot = SceneProjectionBuilder.BuildSnapshot(TestSessionId, scene, mainGmContext, baseSessionSequence: 10, projectionRevision: 1, permissionRevision: 1, Clock);
            ProjectionSnapshot observerSnapshot = SceneProjectionBuilder.BuildSnapshot(TestSessionId, scene, observerContext, baseSessionSequence: 10, projectionRevision: 1, permissionRevision: 1, Clock);

            Assert.That(observerSnapshot.PayloadHash, Is.Not.EqualTo(mainGmSnapshot.PayloadHash));
        }
    }
}
