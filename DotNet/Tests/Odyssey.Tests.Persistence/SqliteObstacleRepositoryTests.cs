using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Geometry;
using Odyssey.Domain.Identity;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence
{
    /// <summary>SLICE-10 Block 2: storage-layer tests for <see cref="SqliteObstacleRepository"/> -- no authorization is exercised here (the repository does not authorize, by precedent of `SqliteContentCatalogRepository`); see `ObstacleAuthoringServiceTests.cs` for the real MainGM/participant checks.</summary>
    public sealed class SqliteObstacleRepositoryTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private string _workDir = null!;
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;
        private SqliteSceneRepository _sceneRepository = null!;
        private SqliteObstacleRepository _obstacles = null!;
        private SceneId _sceneId;

        [SetUp]
        public void SetUp()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "ody-slice10-block2-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_workDir, "Obstacle Test Campaign", "ruleset.core", "1.0.0", "0.1.0", global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost());
            Result<CampaignHandle> created = _campaignRepository.Create(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _sceneRepository = new SqliteSceneRepository(Clock);
            _sceneId = _sceneRepository.CreateScene(_campaign, "Battle Map", NewCommandId(), TestCorrelationId).Value.SceneId;
            _obstacles = new SqliteObstacleRepository(Clock);
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaignRepository.Close(_campaign, TestCorrelationId); } catch (IOException) { }
            try { if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true); } catch (IOException) { }
        }

        [Test] // TC-PERSIST-065
        public void CreateObstacle_EachKind_RoundTrips_DoorDefaultsClosed_WallAndWindowHaveNoIsOpen()
        {
            Result<ObstacleRecord> wall = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 0, 0, 10, 0, NewCommandId(), TestCorrelationId);
            Result<ObstacleRecord> door = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Door, 10, 0, 12, 0, NewCommandId(), TestCorrelationId);
            Result<ObstacleRecord> window = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Window, 12, 0, 14, 0, NewCommandId(), TestCorrelationId);

            Assert.That(wall.IsSuccess, Is.True);
            Assert.That(wall.Value.Kind, Is.EqualTo(ObstacleKind.Wall));
            Assert.That(wall.Value.IsOpen, Is.Null);
            Assert.That(wall.Value.Revision, Is.EqualTo(1));
            Assert.That(wall.Value.X1, Is.EqualTo(0));
            Assert.That(wall.Value.X2, Is.EqualTo(10));

            Assert.That(door.IsSuccess, Is.True);
            Assert.That(door.Value.Kind, Is.EqualTo(ObstacleKind.Door));
            Assert.That(door.Value.IsOpen, Is.False, "a door starts closed");

            Assert.That(window.IsSuccess, Is.True);
            Assert.That(window.Value.Kind, Is.EqualTo(ObstacleKind.Window));
            Assert.That(window.Value.IsOpen, Is.Null);
        }

        [Test] // TC-PERSIST-066
        public void CreateObstacle_RetryWithSameCommandId_IsIdempotent_NoDuplicateRow()
        {
            CommandId commandId = NewCommandId();
            Result<ObstacleRecord> first = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 0, 0, 5, 0, commandId, TestCorrelationId);
            Result<ObstacleRecord> replay = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 0, 0, 5, 0, commandId, TestCorrelationId);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.ObstacleId, Is.EqualTo(first.Value.ObstacleId));

            Result<IReadOnlyList<ObstacleRecord>> listed = _obstacles.ListObstacles(_campaign, _sceneId, TestCorrelationId);
            Assert.That(listed.Value.Count, Is.EqualTo(1), "a replayed CommandId must not create a second row");
        }

        [Test] // TC-PERSIST-067
        public void ToggleDoorState_OpensAndCloses_RevisionIncrements()
        {
            ObstacleRecord door = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Door, 0, 0, 2, 0, NewCommandId(), TestCorrelationId).Value;

            Result<ObstacleRecord> opened = _obstacles.ToggleDoorState(_campaign, door.ObstacleId, isOpen: true, door.Revision, NewCommandId(), TestCorrelationId);
            Assert.That(opened.IsSuccess, Is.True);
            Assert.That(opened.Value.IsOpen, Is.True);
            Assert.That(opened.Value.Revision, Is.EqualTo(2));

            Result<ObstacleRecord> closed = _obstacles.ToggleDoorState(_campaign, door.ObstacleId, isOpen: false, opened.Value.Revision, NewCommandId(), TestCorrelationId);
            Assert.That(closed.IsSuccess, Is.True);
            Assert.That(closed.Value.IsOpen, Is.False);
            Assert.That(closed.Value.Revision, Is.EqualTo(3));
        }

        [Test] // TC-PERSIST-068
        public void ToggleDoorState_StaleExpectedRevision_IsRejectedWithoutMutation()
        {
            ObstacleRecord door = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Door, 0, 0, 2, 0, NewCommandId(), TestCorrelationId).Value;
            Assert.That(_obstacles.ToggleDoorState(_campaign, door.ObstacleId, true, door.Revision, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);

            Result<ObstacleRecord> stale = _obstacles.ToggleDoorState(_campaign, door.ObstacleId, false, door.Revision, NewCommandId(), TestCorrelationId);

            Assert.That(stale.IsFailure, Is.True);
            Assert.That(stale.Error.Code, Is.EqualTo(ErrorCodes.PersistenceObstacleRevisionConflict));
            Result<IReadOnlyList<ObstacleRecord>> listed = _obstacles.ListObstacles(_campaign, _sceneId, TestCorrelationId);
            Assert.That(listed.Value.Single().IsOpen, Is.True, "the stale/rejected toggle must not have mutated the row");
            Assert.That(listed.Value.Single().Revision, Is.EqualTo(2));
        }

        [Test] // TC-PERSIST-069
        public void ToggleDoorState_OnWallOrWindow_IsTypedRejection_NotException()
        {
            ObstacleRecord wall = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 0, 0, 5, 0, NewCommandId(), TestCorrelationId).Value;
            ObstacleRecord window = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Window, 5, 0, 8, 0, NewCommandId(), TestCorrelationId).Value;

            Result<ObstacleRecord> wallResult = _obstacles.ToggleDoorState(_campaign, wall.ObstacleId, true, wall.Revision, NewCommandId(), TestCorrelationId);
            Result<ObstacleRecord> windowResult = _obstacles.ToggleDoorState(_campaign, window.ObstacleId, true, window.Revision, NewCommandId(), TestCorrelationId);

            Assert.That(wallResult.IsFailure, Is.True);
            Assert.That(wallResult.Error.Code, Is.EqualTo(ErrorCodes.ObstacleToggleNotADoor));
            Assert.That(windowResult.IsFailure, Is.True);
            Assert.That(windowResult.Error.Code, Is.EqualTo(ErrorCodes.ObstacleToggleNotADoor));
        }

        [Test] // TC-PERSIST-070
        public void ToggleDoorState_UnknownObstacleId_ReturnsNotFound()
        {
            ObstacleId missing = ObstacleId.NewId(Clock.GetUtcNow());
            Result<ObstacleRecord> result = _obstacles.ToggleDoorState(_campaign, missing, true, 1, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceObstacleNotFound));
        }

        [Test] // TC-PERSIST-071
        public void ListObstacles_RoundTrips_MultipleKinds_ScopedToScene()
        {
            SceneId otherScene = _sceneRepository.CreateScene(_campaign, "Other Map", NewCommandId(), TestCorrelationId).Value.SceneId;
            _obstacles.CreateObstacle(_campaign, otherScene, ObstacleKind.Wall, 0, 0, 1, 1, NewCommandId(), TestCorrelationId);

            Result<ObstacleRecord> wall = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 0, 0, 10, 0, NewCommandId(), TestCorrelationId);
            Result<ObstacleRecord> door = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Door, 10, 0, 12, 0, NewCommandId(), TestCorrelationId);
            Result<ObstacleRecord> window = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Window, 12, 0, 14, 0, NewCommandId(), TestCorrelationId);

            Result<IReadOnlyList<ObstacleRecord>> listed = _obstacles.ListObstacles(_campaign, _sceneId, TestCorrelationId);

            Assert.That(listed.IsSuccess, Is.True);
            Assert.That(listed.Value.Count, Is.EqualTo(3), "only this scene's own obstacles, not the other scene's");
            Assert.That(listed.Value.Select(o => o.ObstacleId), Is.EquivalentTo(new[] { wall.Value.ObstacleId, door.Value.ObstacleId, window.Value.ObstacleId }));
        }

        [Test] // TC-PERSIST-072: regression -- the existing Scene/Token repository is unaffected by the new Obstacle table.
        public void ExistingSceneAndTokenOperations_AreUnaffectedByTheNewObstacleTable()
        {
            _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 0, 0, 5, 0, NewCommandId(), TestCorrelationId);

            Result<TokenRecord> token = _sceneRepository.CreateToken(_campaign, _sceneId, new TokenPosition(1, 1), UserId.Parse("user_" + Guid.NewGuid().ToString("N")), NewCommandId(), TestCorrelationId);
            Assert.That(token.IsSuccess, Is.True);
            Result<IReadOnlyList<TokenRecord>> tokens = _sceneRepository.ListTokens(_campaign, _sceneId, TestCorrelationId);
            Assert.That(tokens.Value.Count, Is.EqualTo(1));
        }

        [Test] // TC-PERSIST-130
        public void CreateObstacle_WithoutMaxHp_IsIndestructible_NoDurabilityRow()
        {
            ObstacleRecord wall = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 0, 0, 5, 0, NewCommandId(), TestCorrelationId).Value;

            Result<ObstacleDurabilityRecord> durability = _obstacles.GetObstacleDurability(_campaign, wall.ObstacleId, TestCorrelationId);

            Assert.That(durability.IsFailure, Is.True, "an obstacle created without maxHp has no durability row at all -- indestructible by omission");
            Assert.That(durability.Error.Code, Is.EqualTo(ErrorCodes.ObstacleDurabilityNotConfigured));
        }

        [Test] // TC-PERSIST-131
        public void CreateObstacle_WithMaxHp_SeedsADurabilityRowAtFullHealth()
        {
            ObstacleRecord wall = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 0, 0, 5, 0, NewCommandId(), TestCorrelationId, maxHp: 10, protection: 2).Value;

            Result<ObstacleDurabilityRecord> durability = _obstacles.GetObstacleDurability(_campaign, wall.ObstacleId, TestCorrelationId);

            Assert.That(durability.IsSuccess, Is.True);
            Assert.That(durability.Value.MaxHp, Is.EqualTo(10));
            Assert.That(durability.Value.CurrentHp, Is.EqualTo(10));
            Assert.That(durability.Value.Protection, Is.EqualTo(2));
            Assert.That(durability.Value.IsDestroyed, Is.False);
            Assert.That(durability.Value.Revision, Is.EqualTo(1));
        }

        [Test] // TC-PERSIST-132
        public void ApplyObstacleDamage_SubtractsProtectionFirst_ReducesCurrentHp_NotBelowZero()
        {
            ObstacleRecord wall = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 0, 0, 5, 0, NewCommandId(), TestCorrelationId, maxHp: 10, protection: 3).Value;

            Result<ObstacleDurabilityRecord> afterFirstHit = _obstacles.ApplyObstacleDamage(_campaign, wall.ObstacleId, damageAmount: 5, expectedRevision: 1, NewCommandId(), TestCorrelationId);
            Assert.That(afterFirstHit.IsSuccess, Is.True);
            // effectiveDamage = Max(0, 5 - 3) = 2; CurrentHp = 10 - 2 = 8.
            Assert.That(afterFirstHit.Value.CurrentHp, Is.EqualTo(8));
            Assert.That(afterFirstHit.Value.IsDestroyed, Is.False);
            Assert.That(afterFirstHit.Value.Revision, Is.EqualTo(2));

            Result<ObstacleDurabilityRecord> belowProtection = _obstacles.ApplyObstacleDamage(_campaign, wall.ObstacleId, damageAmount: 1, expectedRevision: 2, NewCommandId(), TestCorrelationId);
            Assert.That(belowProtection.IsSuccess, Is.True);
            // effectiveDamage = Max(0, 1 - 3) = 0 -- damage fully absorbed by Protection, HP unchanged.
            Assert.That(belowProtection.Value.CurrentHp, Is.EqualTo(8));

            Result<ObstacleDurabilityRecord> overkill = _obstacles.ApplyObstacleDamage(_campaign, wall.ObstacleId, damageAmount: 1000, expectedRevision: 3, NewCommandId(), TestCorrelationId);
            Assert.That(overkill.IsSuccess, Is.True);
            Assert.That(overkill.Value.CurrentHp, Is.EqualTo(0), "CurrentHp must clamp at 0, never go negative");
            Assert.That(overkill.Value.IsDestroyed, Is.True);
        }

        [Test] // TC-PERSIST-133
        public void ApplyObstacleDamage_StaleExpectedRevision_IsRejectedWithoutMutation()
        {
            ObstacleRecord wall = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 0, 0, 5, 0, NewCommandId(), TestCorrelationId, maxHp: 10).Value;
            Assert.That(_obstacles.ApplyObstacleDamage(_campaign, wall.ObstacleId, 5, 1, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);

            Result<ObstacleDurabilityRecord> stale = _obstacles.ApplyObstacleDamage(_campaign, wall.ObstacleId, 5, 1, NewCommandId(), TestCorrelationId);

            Assert.That(stale.IsFailure, Is.True);
            Assert.That(stale.Error.Code, Is.EqualTo(ErrorCodes.PersistenceObstacleRevisionConflict));
            Assert.That(_obstacles.GetObstacleDurability(_campaign, wall.ObstacleId, TestCorrelationId).Value.CurrentHp, Is.EqualTo(5), "the stale/rejected damage must not have mutated the row a second time");
        }

        [Test] // TC-PERSIST-134
        public void ApplyObstacleDamage_ObstacleWithoutDurabilityConfigured_IsTypedRejection()
        {
            ObstacleRecord wall = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 0, 0, 5, 0, NewCommandId(), TestCorrelationId).Value;

            Result<ObstacleDurabilityRecord> result = _obstacles.ApplyObstacleDamage(_campaign, wall.ObstacleId, 5, 1, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.ObstacleDurabilityNotConfigured));
        }

        [Test] // TC-PERSIST-135
        public void ListObstacles_ExcludesADestroyedObstacle_IncludesEverythingElse()
        {
            ObstacleRecord destructible = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 0, 0, 5, 0, NewCommandId(), TestCorrelationId, maxHp: 1).Value;
            ObstacleRecord indestructible = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 10, 0, 15, 0, NewCommandId(), TestCorrelationId).Value;
            ObstacleRecord notYetDestroyed = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 20, 0, 25, 0, NewCommandId(), TestCorrelationId, maxHp: 10).Value;

            Result<ObstacleDurabilityRecord> destroyed = _obstacles.ApplyObstacleDamage(_campaign, destructible.ObstacleId, 100, 1, NewCommandId(), TestCorrelationId);
            Assert.That(destroyed.IsSuccess, Is.True);
            Assert.That(destroyed.Value.IsDestroyed, Is.True);

            Result<IReadOnlyList<ObstacleRecord>> listed = _obstacles.ListObstacles(_campaign, _sceneId, TestCorrelationId);

            Assert.That(listed.IsSuccess, Is.True);
            Assert.That(listed.Value.Select(o => o.ObstacleId), Is.EquivalentTo(new[] { indestructible.ObstacleId, notYetDestroyed.ObstacleId }), "a destroyed obstacle must no longer be listed; an indestructible one and a not-yet-destroyed one must still be");
        }
    }
}
