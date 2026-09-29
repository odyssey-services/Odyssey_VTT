using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Board;
using Odyssey.Application.Commands;
using Odyssey.Application.Identity;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Geometry;
using Odyssey.Domain.Identity;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence.Board
{
    /// <summary>
    /// SLICE-10 Block 2: real authorization tests for <see cref="ObstacleAuthoringService"/> -- the
    /// first service in this codebase written from the start with `ODY-S10-101`'s stored-membership
    /// check (no transitional client-flag period ever existed here).
    /// </summary>
    public sealed class ObstacleAuthoringServiceTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static readonly UserId Host = global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        private string _workDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;
        private SqliteObstacleRepository _obstacles = null!;
        private SceneId _sceneId;

        [SetUp]
        public void SetUp()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "ody-slice10-block2-authoring-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_workDir, "Obstacle Authoring Test Campaign", "ruleset.core", "1.0.0", "0.1.0", Host);
            Result<CampaignHandle> created = _campaignRepository.Create(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            var sceneRepository = new SqliteSceneRepository(Clock);
            _sceneId = sceneRepository.CreateScene(_campaign, "Battle Map", NewCommandId(), TestCorrelationId).Value.SceneId;
            _obstacles = new SqliteObstacleRepository(Clock);
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaignRepository.Close(_campaign, TestCorrelationId); } catch (IOException) { }
            try { if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true); } catch (IOException) { }
        }

        [Test] // TC-PERSIST-073
        public void CreateObstacle_MainGmOnly_ByTheStoredMembership_ForEveryKind()
        {
            UserId stranger = NewUserId();
            UserId player = NewUserId();
            Assert.That(_campaignRepository.AddMember(_campaign, player, CampaignMembershipRole.Player, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);
            UserId secondGm = NewUserId();
            Assert.That(_campaignRepository.AddMember(_campaign, secondGm, CampaignMembershipRole.MainGm, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);

            foreach (UserId denied in new[] { stranger, player })
            {
                foreach (ObstacleKind kind in new[] { ObstacleKind.Wall, ObstacleKind.Door, ObstacleKind.Window })
                {
                    var request = new CreateObstacleRequest(_campaign, _sceneId, kind, 0, 0, 5, 0, denied, NewCommandId(), TestCorrelationId);
                    Result<ObstacleRecord> result = ObstacleAuthoringService.CreateObstacle(_obstacles, _campaignRepository, request);
                    Assert.That(result.IsFailure, Is.True, $"kind={kind}, actor is not MainGm and must be denied");
                    Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.ObstacleAuthoringDenied));
                }
            }

            Result<IReadOnlyList<ObstacleRecord>> noneCreated = ObstacleAuthoringService.ListObstacles(_obstacles, new ListObstaclesRequest(_campaign, _sceneId, TestCorrelationId));
            Assert.That(noneCreated.Value, Is.Empty, "every denied CreateObstacle above must have caused no repository state change");

            foreach (UserId gm in new[] { Host, secondGm })
            {
                foreach (ObstacleKind kind in new[] { ObstacleKind.Wall, ObstacleKind.Door, ObstacleKind.Window })
                {
                    var request = new CreateObstacleRequest(_campaign, _sceneId, kind, 0, 0, 5, 0, gm, NewCommandId(), TestCorrelationId);
                    Result<ObstacleRecord> result = ObstacleAuthoringService.CreateObstacle(_obstacles, _campaignRepository, request);
                    Assert.That(result.IsSuccess, Is.True, $"kind={kind}, a genuinely registered MainGm must be authorized");
                    Assert.That(result.Value.Kind, Is.EqualTo(kind));
                }
            }
        }

        [Test] // TC-PERSIST-074
        public void CreateObstacle_FailsClosed_WhenTheMembershipLookupFails()
        {
            var poisoned = PoisonedCampaignRepository.FailsOnLookup();
            var request = new CreateObstacleRequest(_campaign, _sceneId, ObstacleKind.Wall, 0, 0, 5, 0, Host, NewCommandId(), TestCorrelationId);

            Result<ObstacleRecord> result = ObstacleAuthoringService.CreateObstacle(_obstacles, poisoned, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCampaignIoFailed), "an unreadable membership is the lookup's own failure, not a pass and not a fake denial -- even for the host");
            Assert.That(poisoned.LookupCalls, Is.EqualTo(1));
        }

        [Test] // TC-PERSIST-075
        public void ToggleDoorState_AnyRegisteredParticipant_Succeeds_UnregisteredIsDenied()
        {
            UserId player = NewUserId();
            Assert.That(_campaignRepository.AddMember(_campaign, player, CampaignMembershipRole.Player, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);
            UserId observer = NewUserId();
            Assert.That(_campaignRepository.AddMember(_campaign, observer, CampaignMembershipRole.Observer, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);
            UserId stranger = NewUserId();

            ObstacleRecord door = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Door, 0, 0, 2, 0, NewCommandId(), TestCorrelationId).Value;

            var deniedRequest = new ToggleDoorStateRequest(_campaign, door.ObstacleId, true, door.Revision, stranger, NewCommandId(), TestCorrelationId);
            Result<ObstacleRecord> denied = ObstacleAuthoringService.ToggleDoorState(_obstacles, _campaignRepository, deniedRequest);
            Assert.That(denied.IsFailure, Is.True, "an unregistered actor must be denied, fail-closed");
            Assert.That(denied.Error.Code, Is.EqualTo(ErrorCodes.ObstacleToggleDenied));

            var byPlayer = new ToggleDoorStateRequest(_campaign, door.ObstacleId, true, door.Revision, player, NewCommandId(), TestCorrelationId);
            Result<ObstacleRecord> playerResult = ObstacleAuthoringService.ToggleDoorState(_obstacles, _campaignRepository, byPlayer);
            Assert.That(playerResult.IsSuccess, Is.True, "a registered Player (not MainGm) must be authorized to toggle a door");
            Assert.That(playerResult.Value.IsOpen, Is.True);

            var byObserver = new ToggleDoorStateRequest(_campaign, door.ObstacleId, false, playerResult.Value.Revision, observer, NewCommandId(), TestCorrelationId);
            Result<ObstacleRecord> observerResult = ObstacleAuthoringService.ToggleDoorState(_obstacles, _campaignRepository, byObserver);
            Assert.That(observerResult.IsSuccess, Is.True, "a registered Observer must also be authorized -- any registered role, not just Player/MainGm");

            var byHost = new ToggleDoorStateRequest(_campaign, door.ObstacleId, true, observerResult.Value.Revision, Host, NewCommandId(), TestCorrelationId);
            Result<ObstacleRecord> hostResult = ObstacleAuthoringService.ToggleDoorState(_obstacles, _campaignRepository, byHost);
            Assert.That(hostResult.IsSuccess, Is.True, "the MainGm must also be authorized -- ToggleDoorState is not MainGM-exclusive, but MainGM is still a registered participant");
        }

        [Test] // TC-PERSIST-076
        public void ToggleDoorState_FailsClosed_WhenTheMembershipLookupFails()
        {
            ObstacleRecord door = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Door, 0, 0, 2, 0, NewCommandId(), TestCorrelationId).Value;
            var poisoned = PoisonedCampaignRepository.FailsOnLookup();
            var request = new ToggleDoorStateRequest(_campaign, door.ObstacleId, true, door.Revision, Host, NewCommandId(), TestCorrelationId);

            Result<ObstacleRecord> result = ObstacleAuthoringService.ToggleDoorState(_obstacles, poisoned, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCampaignIoFailed), "an unreadable membership is the lookup's own failure, not a pass and not a fake denial -- even for the host");
            Assert.That(poisoned.LookupCalls, Is.EqualTo(1));
            Assert.That(_obstacles.ListObstacles(_campaign, _sceneId, TestCorrelationId).Value[0].IsOpen, Is.False, "the door must remain closed -- the poisoned lookup must never reach the repository write");
        }

        [Test] // TC-PERSIST-077
        public void ToggleDoorState_OnWallOrWindow_IsTypedRejection_ForARegisteredMainGm()
        {
            ObstacleRecord wall = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 0, 0, 5, 0, NewCommandId(), TestCorrelationId).Value;
            var request = new ToggleDoorStateRequest(_campaign, wall.ObstacleId, true, wall.Revision, Host, NewCommandId(), TestCorrelationId);

            Result<ObstacleRecord> result = ObstacleAuthoringService.ToggleDoorState(_obstacles, _campaignRepository, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.ObstacleToggleNotADoor));
        }

        [Test] // TC-PERSIST-078
        public void ListObstacles_PerformsNoAuthorization_ByPrecedentOfCatalogReads()
        {
            _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 0, 0, 5, 0, NewCommandId(), TestCorrelationId);

            Result<IReadOnlyList<ObstacleRecord>> result = ObstacleAuthoringService.ListObstacles(_obstacles, new ListObstaclesRequest(_campaign, _sceneId, TestCorrelationId));

            Assert.That(result.IsSuccess, Is.True, "ListObstacles takes no ICampaignRepository at all -- reads are deliberately open, by ODY-S10-104 precedent");
            Assert.That(result.Value.Count, Is.EqualTo(1));
        }

        [Test] // TC-PERSIST-136
        public void ApplyObstacleDamage_MainGmOnly_ByTheStoredMembership()
        {
            UserId stranger = NewUserId();
            UserId player = NewUserId();
            Assert.That(_campaignRepository.AddMember(_campaign, player, CampaignMembershipRole.Player, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);
            UserId secondGm = NewUserId();
            Assert.That(_campaignRepository.AddMember(_campaign, secondGm, CampaignMembershipRole.MainGm, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);
            ObstacleRecord wall = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 0, 0, 5, 0, NewCommandId(), TestCorrelationId, maxHp: 10).Value;

            foreach (UserId denied in new[] { stranger, player })
            {
                var request = new ApplyObstacleDamageRequest(_campaign, wall.ObstacleId, 5, 1, denied, NewCommandId(), TestCorrelationId);
                Result<ObstacleDurabilityRecord> result = ObstacleAuthoringService.ApplyObstacleDamage(_obstacles, _campaignRepository, request);
                Assert.That(result.IsFailure, Is.True, "an actor who is not MainGm must be denied");
                Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.ObstacleApplyDamageDenied));
            }

            Result<ObstacleDurabilityRecord> unchanged = _obstacles.GetObstacleDurability(_campaign, wall.ObstacleId, TestCorrelationId);
            Assert.That(unchanged.Value.CurrentHp, Is.EqualTo(10), "every denied ApplyObstacleDamage above must have caused no repository state change");

            var byMainGm = new ApplyObstacleDamageRequest(_campaign, wall.ObstacleId, 5, 1, secondGm, NewCommandId(), TestCorrelationId);
            Result<ObstacleDurabilityRecord> mainGmResult = ObstacleAuthoringService.ApplyObstacleDamage(_obstacles, _campaignRepository, byMainGm);
            Assert.That(mainGmResult.IsSuccess, Is.True, "a genuinely registered MainGm must be authorized");
            Assert.That(mainGmResult.Value.CurrentHp, Is.EqualTo(5));
        }

        [Test] // TC-PERSIST-137
        public void ApplyObstacleDamage_FailsClosed_WhenTheMembershipLookupFails()
        {
            var poisoned = PoisonedCampaignRepository.FailsOnLookup();
            ObstacleRecord wall = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 0, 0, 5, 0, NewCommandId(), TestCorrelationId, maxHp: 10).Value;
            var request = new ApplyObstacleDamageRequest(_campaign, wall.ObstacleId, 5, 1, Host, NewCommandId(), TestCorrelationId);

            Result<ObstacleDurabilityRecord> result = ObstacleAuthoringService.ApplyObstacleDamage(_obstacles, poisoned, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCampaignIoFailed), "an unreadable membership is the lookup's own failure, not a pass and not a fake denial -- even for the host");
            Assert.That(poisoned.LookupCalls, Is.EqualTo(1));
            Assert.That(_obstacles.GetObstacleDurability(_campaign, wall.ObstacleId, TestCorrelationId).Value.CurrentHp, Is.EqualTo(10), "the poisoned lookup must never reach the repository write");
        }

        private sealed class PoisonedCampaignRepository : ICampaignRepository
        {
            public static PoisonedCampaignRepository FailsOnLookup() => new PoisonedCampaignRepository();

            public int LookupCalls { get; private set; }

            public Result<CampaignMemberLookup> GetMemberRole(CampaignHandle campaign, UserId userId, CorrelationId correlationId)
            {
                LookupCalls++;
                return Result<CampaignMemberLookup>.Failure(PersistenceFailures.CampaignIoFailed(correlationId));
            }

            public Result<CampaignHandle> Create(CreateCampaignRequest request, CommandId commandId, CorrelationId correlationId) => throw new NotSupportedException();
            public Result<CampaignHandle> Open(string campaignFolderPath, CorrelationId correlationId) => throw new NotSupportedException();
            public Result Close(CampaignHandle handle, CorrelationId correlationId) => throw new NotSupportedException();
            public Result<CampaignMembership> AddMember(CampaignHandle campaign, UserId userId, CampaignMembershipRole role, CommandId commandId, CorrelationId correlationId) => throw new NotSupportedException();
            public Result<IReadOnlyList<CampaignMembership>> ListMembers(CampaignHandle campaign, CorrelationId correlationId) => throw new NotSupportedException();
        }
    }
}
