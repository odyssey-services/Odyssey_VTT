using System;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Geometry;
using Odyssey.Domain.Identity;
using Odyssey.Persistence.Sqlite;
using Odyssey.Unity.Client;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Tests.Unity.EditMode
{
    /// <summary>
    /// ODY-S10-115: real, end-to-end tests confirming obstacles are filtered by the player's own map
    /// memory on <see cref="BoardScreenPresenter"/>, the same way tokens already are (`ODY-S10-113`) --
    /// closing the gap where an unexplored door's existence/open-closed state was readable through the
    /// fog overlay's own 82%-opacity darkening. Real <see cref="SqliteObstacleRepository"/>/
    /// <see cref="SqliteFogOfWarRepository"/> against a temp-directory campaign, never a mock, by exact
    /// precedent of <see cref="BoardScreenPresenterFogOfWarTests"/>.
    /// </summary>
    public sealed class BoardScreenPresenterObstacleFogTests
    {
        private static CorrelationId TestCorrelationId => CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly UnityWallClock Clock = new UnityWallClock();

        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
        private static VisualElement? ObstacleElement(UIDocument document, ObstacleId obstacleId) => document.rootVisualElement.Q<VisualElement>("obstacle-" + obstacleId);

        private sealed class Fixture : IDisposable
        {
            private readonly TemporaryDirectoryHandle _directory = new TemporaryDirectoryHandle();
            public readonly SqliteCampaignRepository CampaignRepository;
            public readonly SqliteSceneRepository SceneRepository;
            public readonly SqliteObstacleRepository ObstacleRepository;
            public readonly SqliteTokenVisionRepository VisionRepository;
            public readonly SqliteFogOfWarRepository FogRepository;
            public readonly CampaignHandle Campaign;
            public readonly SceneId SceneId;
            public readonly UserId MainGmActor;
            public readonly GameObject GameObject;
            public readonly UIDocument Document;

            public Fixture()
            {
                CampaignRepository = new SqliteCampaignRepository(Clock);
                MainGmActor = global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost();
                var createRequest = new CreateCampaignRequest(_directory.Path, "Board Obstacle Fog Test Campaign", "ruleset.core", "1.0.0", "0.1.0", MainGmActor);
                Campaign = CampaignRepository.Create(createRequest, NewCommandId(), TestCorrelationId).Value;
                SceneRepository = new SqliteSceneRepository(Clock);
                ObstacleRepository = new SqliteObstacleRepository(Clock);
                VisionRepository = new SqliteTokenVisionRepository(Clock);
                FogRepository = new SqliteFogOfWarRepository(Clock);
                SceneId = SceneRepository.CreateScene(Campaign, "Test Scene", NewCommandId(), TestCorrelationId).Value.SceneId;

                GameObject = new GameObject("Board Obstacle Fog Document");
                Document = GameObject.AddComponent<UIDocument>();
            }

            public BoardScreenPresenter NewPresenter(UserId localActor, bool isMainGm)
            {
                return new BoardScreenPresenter(Document, SceneRepository, Campaign, CampaignRepository, ObstacleRepository, VisionRepository, FogRepository, SceneId, localActor) { LocalActorIsMainGm = isMainGm };
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(GameObject);
                CampaignRepository.Close(Campaign, TestCorrelationId);
                _directory.Dispose();
            }
        }

        private sealed class TemporaryDirectoryHandle : IDisposable
        {
            public string Path { get; }

            public TemporaryDirectoryHandle()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ody-slice10-obstacle-fog-" + Guid.NewGuid().ToString("N"));
            }

            public void Dispose()
            {
                try { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); } catch (IOException) { }
            }
        }

        [Test] // TC-BOARD-135
        public void NonMainGmActor_UnexploredObstacle_IsNotRendered_MainGmActor_SeesIt()
        {
            using var fixture = new Fixture();
            UserId player = NewUserId();
            Assert.That(fixture.CampaignRepository.AddMember(fixture.Campaign, player, CampaignMembershipRole.Player, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);
            ObstacleRecord obstacle = fixture.ObstacleRepository.CreateObstacle(fixture.Campaign, fixture.SceneId, ObstacleKind.Wall, 100, 0, 120, 0, NewCommandId(), TestCorrelationId).Value;

            using var playerPresenter = fixture.NewPresenter(player, isMainGm: false);
            Assert.That(playerPresenter.Initialize().IsSuccess, Is.True);
            Assert.That(ObstacleElement(fixture.Document, obstacle.ObstacleId), Is.Null, "an obstacle none of whose sample points is inside any explored reveal must not be rendered for a non-MainGm actor");

            using var mainGmPresenter = fixture.NewPresenter(fixture.MainGmActor, isMainGm: true);
            Assert.That(mainGmPresenter.Initialize().IsSuccess, Is.True);
            Assert.That(ObstacleElement(fixture.Document, obstacle.ObstacleId), Is.Not.Null, "the MainGm must see every obstacle unconditionally, regardless of any player's own exploration");
        }

        [Test] // TC-BOARD-136
        public void ObstacleBecomesExplored_AfterRecordExploration_AppearsOnNextRefresh()
        {
            using var fixture = new Fixture();
            UserId player = NewUserId();
            Assert.That(fixture.CampaignRepository.AddMember(fixture.Campaign, player, CampaignMembershipRole.Player, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);
            ObstacleRecord obstacle = fixture.ObstacleRepository.CreateObstacle(fixture.Campaign, fixture.SceneId, ObstacleKind.Wall, 5, 0, 6, 0, NewCommandId(), TestCorrelationId).Value;
            TokenRecord token = fixture.SceneRepository.CreateToken(fixture.Campaign, fixture.SceneId, new TokenPosition(0, 0), player, NewCommandId(), TestCorrelationId).Value;

            using var presenter = fixture.NewPresenter(player, isMainGm: false);
            Assert.That(presenter.Initialize().IsSuccess, Is.True);
            Assert.That(ObstacleElement(fixture.Document, obstacle.ObstacleId), Is.Null, "not yet explored, must not be rendered");

            Result<TokenRecord> moved = presenter.TryMoveTokenTo(token.TokenId, new TokenPosition(5, 0));
            Assert.That(moved.IsSuccess, Is.True);

            Assert.That(ObstacleElement(fixture.Document, obstacle.ObstacleId), Is.Not.Null, "after the move's own best-effort RecordExploration covers the obstacle's own position, the following Refresh() must render it");
        }
    }
}
