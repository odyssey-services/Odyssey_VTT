using System;
using System.Collections.Generic;
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
    /// SLICE-10 Block 6 part 1: real, end-to-end tests for obstacle drawing, door toggling, and manual
    /// damage on <see cref="BoardScreenPresenter"/> -- a real <see cref="SqliteObstacleRepository"/> against
    /// a temp-directory campaign, never a mock, by exact precedent of <see cref="BoardScreenPresenterTests"/>.
    /// The drawing gesture is exercised through the presenter's own public
    /// Begin/Move/EndObstacleDraw methods (never simulated UI Toolkit pointer events) -- see
    /// <c>RealMouseClick_DrawWall_...</c> in the PlayMode smoke test for the one test that drives the real
    /// event routing instead.
    /// </summary>
    public sealed class BoardScreenPresenterObstacleTests
    {
        private static CorrelationId TestCorrelationId => CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly UnityWallClock Clock = new UnityWallClock();

        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

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
            public readonly BoardScreenPresenter Presenter;

            public Fixture()
            {
                CampaignRepository = new SqliteCampaignRepository(Clock);
                MainGmActor = global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost();
                var createRequest = new CreateCampaignRequest(_directory.Path, "Board Obstacle Test Campaign", "ruleset.core", "1.0.0", "0.1.0", MainGmActor);
                Campaign = CampaignRepository.Create(createRequest, NewCommandId(), TestCorrelationId).Value;
                SceneRepository = new SqliteSceneRepository(Clock);
                ObstacleRepository = new SqliteObstacleRepository(Clock);
                VisionRepository = new SqliteTokenVisionRepository(Clock);
                FogRepository = new SqliteFogOfWarRepository(Clock);
                SceneId = SceneRepository.CreateScene(Campaign, "Test Scene", NewCommandId(), TestCorrelationId).Value.SceneId;

                GameObject = new GameObject("Board Obstacle Document");
                Document = GameObject.AddComponent<UIDocument>();
                Presenter = new BoardScreenPresenter(Document, SceneRepository, Campaign, CampaignRepository, ObstacleRepository, VisionRepository, FogRepository, SceneId, MainGmActor);
                Assert.That(Presenter.Initialize().IsSuccess, Is.True);
            }

            public void Dispose()
            {
                Presenter.Dispose();
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
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ody-slice10-block6-" + Guid.NewGuid().ToString("N"));
            }

            public void Dispose()
            {
                try { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); } catch (IOException) { }
            }
        }

        [Test] // TC-BOARD-113
        public void DrawWall_CommittedDrag_CreatesAWallObstacleAtTheDraggedWorldCoordinates()
        {
            using var fixture = new Fixture();
            Assert.That(fixture.Presenter.SetTool(BoardTool.DrawWall), Is.True);

            double startPixelX = fixture.Presenter.Camera.ToPixelsX(0);
            double startPixelY = fixture.Presenter.Camera.ToPixelsY(0);
            double endPixelX = fixture.Presenter.Camera.ToPixelsX(5);
            double endPixelY = fixture.Presenter.Camera.ToPixelsY(0);

            fixture.Presenter.BeginObstacleDraw(startPixelX, startPixelY);
            fixture.Presenter.MoveObstacleDraw(endPixelX, endPixelY);
            fixture.Presenter.EndObstacleDraw(endPixelX, endPixelY);

            Result<IReadOnlyList<ObstacleRecord>> obstacles = fixture.ObstacleRepository.ListObstacles(fixture.Campaign, fixture.SceneId, TestCorrelationId);
            Assert.That(obstacles.Value.Count, Is.EqualTo(1));
            Assert.That(obstacles.Value[0].Kind, Is.EqualTo(ObstacleKind.Wall));
            Assert.That(obstacles.Value[0].X1, Is.EqualTo(0).Within(1e-6));
            Assert.That(obstacles.Value[0].Y1, Is.EqualTo(0).Within(1e-6));
            Assert.That(obstacles.Value[0].X2, Is.EqualTo(5).Within(1e-6));
            Assert.That(obstacles.Value[0].Y2, Is.EqualTo(0).Within(1e-6));
            Assert.That(fixture.Presenter.CurrentTool, Is.EqualTo(BoardTool.DrawWall), "the tool must stay active after one obstacle, so several can be drawn in a row");
        }

        [Test] // TC-BOARD-114
        public void DrawDoor_CommittedDrag_CreatesADoorObstacle_ClosedByDefault()
        {
            using var fixture = new Fixture();
            Assert.That(fixture.Presenter.SetTool(BoardTool.DrawDoor), Is.True);

            double startPixelX = fixture.Presenter.Camera.ToPixelsX(0);
            double startPixelY = fixture.Presenter.Camera.ToPixelsY(0);
            double endPixelX = fixture.Presenter.Camera.ToPixelsX(2);
            double endPixelY = fixture.Presenter.Camera.ToPixelsY(0);

            fixture.Presenter.BeginObstacleDraw(startPixelX, startPixelY);
            fixture.Presenter.EndObstacleDraw(endPixelX, endPixelY);

            Result<IReadOnlyList<ObstacleRecord>> obstacles = fixture.ObstacleRepository.ListObstacles(fixture.Campaign, fixture.SceneId, TestCorrelationId);
            Assert.That(obstacles.Value.Count, Is.EqualTo(1));
            Assert.That(obstacles.Value[0].Kind, Is.EqualTo(ObstacleKind.Door));
            Assert.That(obstacles.Value[0].IsOpen, Is.False);
        }

        [Test] // TC-BOARD-115
        public void DrawWindow_CommittedDrag_CreatesAWindowObstacle()
        {
            using var fixture = new Fixture();
            Assert.That(fixture.Presenter.SetTool(BoardTool.DrawWindow), Is.True);

            double startPixelX = fixture.Presenter.Camera.ToPixelsX(1);
            double startPixelY = fixture.Presenter.Camera.ToPixelsY(1);
            double endPixelX = fixture.Presenter.Camera.ToPixelsX(4);
            double endPixelY = fixture.Presenter.Camera.ToPixelsY(1);

            fixture.Presenter.BeginObstacleDraw(startPixelX, startPixelY);
            fixture.Presenter.EndObstacleDraw(endPixelX, endPixelY);

            Result<IReadOnlyList<ObstacleRecord>> obstacles = fixture.ObstacleRepository.ListObstacles(fixture.Campaign, fixture.SceneId, TestCorrelationId);
            Assert.That(obstacles.Value.Count, Is.EqualTo(1));
            Assert.That(obstacles.Value[0].Kind, Is.EqualTo(ObstacleKind.Window));
        }

        [Test] // TC-BOARD-116
        public void DrawGesture_BelowThePixelDragThreshold_CreatesNothing()
        {
            using var fixture = new Fixture();
            Assert.That(fixture.Presenter.SetTool(BoardTool.DrawWall), Is.True);

            fixture.Presenter.BeginObstacleDraw(100, 100);
            fixture.Presenter.EndObstacleDraw(102, 101);

            Result<IReadOnlyList<ObstacleRecord>> obstacles = fixture.ObstacleRepository.ListObstacles(fixture.Campaign, fixture.SceneId, TestCorrelationId);
            Assert.That(obstacles.Value, Is.Empty, "an accidental short click-drag must not create an obstacle");
        }

        [Test] // TC-BOARD-117
        public void SelectMode_ClickOnAClosedDoor_TogglesItOpen()
        {
            using var fixture = new Fixture();
            ObstacleRecord door = fixture.ObstacleRepository.CreateObstacle(fixture.Campaign, fixture.SceneId, ObstacleKind.Door, 0, 0, 2, 0, NewCommandId(), TestCorrelationId).Value;
            fixture.Presenter.Refresh();

            double clickPixelX = fixture.Presenter.Camera.ToPixelsX(1);
            double clickPixelY = fixture.Presenter.Camera.ToPixelsY(0);
            fixture.Presenter.BeginBoardPointerGesture(clickPixelX, clickPixelY);
            fixture.Presenter.EndBoardPointerGesture(clickPixelX, clickPixelY);

            Result<IReadOnlyList<ObstacleRecord>> obstacles = fixture.ObstacleRepository.ListObstacles(fixture.Campaign, fixture.SceneId, TestCorrelationId);
            Assert.That(obstacles.Value[0].ObstacleId, Is.EqualTo(door.ObstacleId));
            Assert.That(obstacles.Value[0].IsOpen, Is.True, "clicking a closed door in Select mode must open it");
        }

        [Test] // TC-BOARD-122
        public void SelectMode_ClickOnADoor_RevisionChangedSinceTheLastRefresh_StillToggles_UsingTheFreshRevision()
        {
            using var fixture = new Fixture();
            ObstacleRecord door = fixture.ObstacleRepository.CreateObstacle(fixture.Campaign, fixture.SceneId, ObstacleKind.Door, 0, 0, 2, 0, NewCommandId(), TestCorrelationId).Value;
            fixture.Presenter.Refresh();

            // ODY-S10-112: another participant toggles the same door directly through the repository --
            // the presenter's own cached ObstacleRecord (from the Refresh() above) is now stale (Revision 1,
            // IsOpen false) while the real row is already Revision 2, IsOpen true. No presenter.Refresh() is
            // called here on purpose, so the click below can only succeed if ToggleObstacleDoor re-reads the
            // revision fresh rather than reusing the stale cached one.
            Result<ObstacleRecord> raced = fixture.ObstacleRepository.ToggleDoorState(fixture.Campaign, door.ObstacleId, true, door.Revision, NewCommandId(), TestCorrelationId);
            Assert.That(raced.IsSuccess, Is.True);
            Assert.That(raced.Value.Revision, Is.EqualTo(2));

            double clickPixelX = fixture.Presenter.Camera.ToPixelsX(1);
            double clickPixelY = fixture.Presenter.Camera.ToPixelsY(0);
            fixture.Presenter.BeginBoardPointerGesture(clickPixelX, clickPixelY);
            fixture.Presenter.EndBoardPointerGesture(clickPixelX, clickPixelY);

            Result<IReadOnlyList<ObstacleRecord>> obstacles = fixture.ObstacleRepository.ListObstacles(fixture.Campaign, fixture.SceneId, TestCorrelationId);
            Assert.That(obstacles.Value[0].Revision, Is.EqualTo(3), "the click must have gone through against the fresh Revision 2, not failed against the stale cached Revision 1");
            Assert.That(obstacles.Value[0].IsOpen, Is.False, "the door was already open (from the race) -- this click must close it, proving it used the door's real, current IsOpen, not the stale cached one");
        }

        [Test] // TC-BOARD-118
        public void NonMainGmActor_DrawingAWall_IsDenied_NoObstacleCreated()
        {
            using var fixture = new Fixture();
            UserId stranger = NewUserId();
            var presenter = new BoardScreenPresenter(fixture.Document, fixture.SceneRepository, fixture.Campaign, fixture.CampaignRepository, fixture.ObstacleRepository, fixture.VisionRepository, fixture.FogRepository, fixture.SceneId, stranger);
            Assert.That(presenter.Initialize().IsSuccess, Is.True);
            Assert.That(presenter.SetTool(BoardTool.DrawWall), Is.True);

            presenter.BeginObstacleDraw(0, 0);
            presenter.EndObstacleDraw(50, 0);

            Result<IReadOnlyList<ObstacleRecord>> obstacles = fixture.ObstacleRepository.ListObstacles(fixture.Campaign, fixture.SceneId, TestCorrelationId);
            Assert.That(obstacles.Value, Is.Empty, "a non-MainGm actor's draw must be denied by the server, creating nothing");
            presenter.Dispose();
        }

        [Test] // TC-BOARD-119
        public void ApplyObstacleDamage_ThroughThePresenter_ReducesCurrentHpByTheExpectedAmount()
        {
            using var fixture = new Fixture();
            ObstacleRecord wall = fixture.ObstacleRepository.CreateObstacle(fixture.Campaign, fixture.SceneId, ObstacleKind.Wall, 0, 0, 5, 0, NewCommandId(), TestCorrelationId, maxHp: 10, protection: 0).Value;
            fixture.Presenter.LocalActorIsMainGm = true;

            Result<ObstacleDurabilityRecord> result = fixture.Presenter.TryApplyObstacleDamage(wall.ObstacleId, 4);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.CurrentHp, Is.EqualTo(6));
            Result<ObstacleDurabilityRecord> persisted = fixture.ObstacleRepository.GetObstacleDurability(fixture.Campaign, wall.ObstacleId, TestCorrelationId);
            Assert.That(persisted.Value.CurrentHp, Is.EqualTo(6));
        }

        [Test] // TC-BOARD-120
        public void ObstacleWithoutDurability_SelectingIt_DoesNotExposeAHpBarOrDamagePanel()
        {
            using var fixture = new Fixture();
            ObstacleRecord wall = fixture.ObstacleRepository.CreateObstacle(fixture.Campaign, fixture.SceneId, ObstacleKind.Wall, 0, 0, 5, 0, NewCommandId(), TestCorrelationId).Value;
            fixture.Presenter.Refresh();
            fixture.Presenter.LocalActorIsMainGm = true;

            double clickPixelX = fixture.Presenter.Camera.ToPixelsX(2.5);
            double clickPixelY = fixture.Presenter.Camera.ToPixelsY(0);
            fixture.Presenter.BeginBoardPointerGesture(clickPixelX, clickPixelY);
            fixture.Presenter.EndBoardPointerGesture(clickPixelX, clickPixelY);

            Assert.That(fixture.Presenter.SelectedObstacleId, Is.EqualTo(wall.ObstacleId), "the obstacle is still selected");
            Result<ObstacleDurabilityRecord> durability = fixture.ObstacleRepository.GetObstacleDurability(fixture.Campaign, wall.ObstacleId, TestCorrelationId);
            Assert.That(durability.IsFailure, Is.True, "an obstacle created without maxHp must have no durability record -- the expected, non-error case");
        }
    }
}
