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
    /// SLICE-10 Block 3: real authorization tests for <see cref="TokenVisionService"/>, plus a full
    /// integration test of <see cref="TokenVisionService.ComputeLineOfSight"/> against real
    /// <see cref="SqliteSceneRepository"/>/<see cref="SqliteTokenVisionRepository"/>/
    /// <see cref="SqliteObstacleRepository"/> data. Continues TC-PERSIST numbering from
    /// <c>LineOfSightTests</c> (which ends at TC-PERSIST-100).
    /// </summary>
    public sealed class TokenVisionServiceTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static readonly UserId Host = global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        private string _workDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;
        private SqliteSceneRepository _scenes = null!;
        private SqliteObstacleRepository _obstacles = null!;
        private SqliteTokenVisionRepository _vision = null!;
        private SceneId _sceneId;

        [SetUp]
        public void SetUp()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "ody-slice10-block3-vision-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_workDir, "Token Vision Test Campaign", "ruleset.core", "1.0.0", "0.1.0", Host);
            Result<CampaignHandle> created = _campaignRepository.Create(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _scenes = new SqliteSceneRepository(Clock);
            _sceneId = _scenes.CreateScene(_campaign, "Battle Map", NewCommandId(), TestCorrelationId).Value.SceneId;
            _obstacles = new SqliteObstacleRepository(Clock);
            _vision = new SqliteTokenVisionRepository(Clock);
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaignRepository.Close(_campaign, TestCorrelationId); } catch (IOException) { }
            try { if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true); } catch (IOException) { }
        }

        private TokenRecord CreateToken(UserId controller, double x = 0, double y = 0)
        {
            return _scenes.CreateToken(_campaign, _sceneId, new TokenPosition(x, y), controller, NewCommandId(), TestCorrelationId).Value;
        }

        [Test] // TC-PERSIST-101
        public void SetTokenFacing_OwnerAndMainGmSucceed_UnrelatedParticipantDenied()
        {
            UserId owner = NewUserId();
            Assert.That(_campaignRepository.AddMember(_campaign, owner, CampaignMembershipRole.Player, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);
            UserId stranger = NewUserId();
            Assert.That(_campaignRepository.AddMember(_campaign, stranger, CampaignMembershipRole.Player, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);

            TokenRecord token = CreateToken(owner);

            var deniedRequest = new SetTokenFacingRequest(_campaign, token.TokenId, 90, token.Revision, stranger, NewCommandId(), TestCorrelationId);
            Result<TokenVisionSettingsRecord> denied = TokenVisionService.SetTokenFacing(_vision, _scenes, _campaignRepository, deniedRequest);
            Assert.That(denied.IsFailure, Is.True, "an unrelated registered participant, neither the token's controller nor MainGm, must be denied");
            Assert.That(denied.Error.Code, Is.EqualTo(ErrorCodes.TokenVisionSetFacingDenied));

            var byOwner = new SetTokenFacingRequest(_campaign, token.TokenId, 90, 1, owner, NewCommandId(), TestCorrelationId);
            Result<TokenVisionSettingsRecord> ownerResult = TokenVisionService.SetTokenFacing(_vision, _scenes, _campaignRepository, byOwner);
            Assert.That(ownerResult.IsSuccess, Is.True, "the token's own controller must be authorized to set its facing");
            Assert.That(ownerResult.Value.FacingDegrees, Is.EqualTo(90));

            var byMainGm = new SetTokenFacingRequest(_campaign, token.TokenId, 180, ownerResult.Value.Revision, Host, NewCommandId(), TestCorrelationId);
            Result<TokenVisionSettingsRecord> mainGmResult = TokenVisionService.SetTokenFacing(_vision, _scenes, _campaignRepository, byMainGm);
            Assert.That(mainGmResult.IsSuccess, Is.True, "the MainGm must also be authorized, even for a token it does not control");
            Assert.That(mainGmResult.Value.FacingDegrees, Is.EqualTo(180));
        }

        [Test] // TC-PERSIST-102
        public void SetTokenFacing_FailsClosed_WhenTheMembershipLookupFails()
        {
            UserId owner = NewUserId();
            TokenRecord token = CreateToken(owner);
            var poisoned = PoisonedCampaignRepository.FailsOnLookup();
            var request = new SetTokenFacingRequest(_campaign, token.TokenId, 45, token.Revision, NewUserId(), NewCommandId(), TestCorrelationId);

            Result<TokenVisionSettingsRecord> result = TokenVisionService.SetTokenFacing(_vision, _scenes, poisoned, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCampaignIoFailed), "an unreadable membership is the lookup's own failure -- and the token's own controller check must have already failed to short-circuit it, since the actor here is not the controller");
            Assert.That(poisoned.LookupCalls, Is.EqualTo(1));
        }

        [Test] // TC-PERSIST-103
        public void SetTokenVisionParameters_MainGmOnly_OwnerNotMainGmDenied_UnrelatedDenied()
        {
            UserId owner = NewUserId();
            Assert.That(_campaignRepository.AddMember(_campaign, owner, CampaignMembershipRole.Player, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);
            UserId stranger = NewUserId();

            TokenRecord token = CreateToken(owner);

            var byOwner = new SetTokenVisionParametersRequest(_campaign, token.TokenId, 120, 30, 1, owner, NewCommandId(), TestCorrelationId);
            Result<TokenVisionSettingsRecord> ownerResult = TokenVisionService.SetTokenVisionParameters(_vision, _campaignRepository, byOwner);
            Assert.That(ownerResult.IsFailure, Is.True, "owning the token is not enough -- SetTokenVisionParameters is MainGm-only, unlike SetTokenFacing");
            Assert.That(ownerResult.Error.Code, Is.EqualTo(ErrorCodes.TokenVisionSetVisionParametersDenied));

            var byStranger = new SetTokenVisionParametersRequest(_campaign, token.TokenId, 120, 30, 1, stranger, NewCommandId(), TestCorrelationId);
            Result<TokenVisionSettingsRecord> strangerResult = TokenVisionService.SetTokenVisionParameters(_vision, _campaignRepository, byStranger);
            Assert.That(strangerResult.IsFailure, Is.True);
            Assert.That(strangerResult.Error.Code, Is.EqualTo(ErrorCodes.TokenVisionSetVisionParametersDenied));

            var byMainGm = new SetTokenVisionParametersRequest(_campaign, token.TokenId, 120, 30, 1, Host, NewCommandId(), TestCorrelationId);
            Result<TokenVisionSettingsRecord> mainGmResult = TokenVisionService.SetTokenVisionParameters(_vision, _campaignRepository, byMainGm);
            Assert.That(mainGmResult.IsSuccess, Is.True, "a genuinely registered MainGm must be authorized");
            Assert.That(mainGmResult.Value.FovAngleDegrees, Is.EqualTo(120));
            Assert.That(mainGmResult.Value.ViewDistance, Is.EqualTo(30));
        }

        [Test] // TC-PERSIST-104
        public void SetTokenVisionParameters_FailsClosed_WhenTheMembershipLookupFails()
        {
            TokenRecord token = CreateToken(Host);
            var poisoned = PoisonedCampaignRepository.FailsOnLookup();
            var request = new SetTokenVisionParametersRequest(_campaign, token.TokenId, 120, 30, token.Revision, Host, NewCommandId(), TestCorrelationId);

            Result<TokenVisionSettingsRecord> result = TokenVisionService.SetTokenVisionParameters(_vision, poisoned, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCampaignIoFailed), "an unreadable membership must never fall back to a pass, even for the host");
            Assert.That(poisoned.LookupCalls, Is.EqualTo(1));
        }

        [Test] // TC-PERSIST-105
        public void ComputeLineOfSight_ObstacleBlocks_ThenOpeningTheDoorClearsIt()
        {
            TokenRecord observer = CreateToken(Host, 0, 0);
            TokenRecord target = CreateToken(Host, 10, 0);
            ObstacleRecord door = _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Door, 5, -5, 5, 5, NewCommandId(), TestCorrelationId).Value;

            var blockedRequest = new ComputeLineOfSightRequest(_campaign, observer.TokenId, target.TokenId, TestCorrelationId);
            Result<bool> blocked = TokenVisionService.ComputeLineOfSight(_scenes, _vision, _obstacles, blockedRequest);
            Assert.That(blocked.IsSuccess, Is.True);
            Assert.That(blocked.Value, Is.False, "a closed door in the direct line must block sight");

            Result<ObstacleRecord> opened = _obstacles.ToggleDoorState(_campaign, door.ObstacleId, true, door.Revision, NewCommandId(), TestCorrelationId);
            Assert.That(opened.IsSuccess, Is.True);

            var clearRequest = new ComputeLineOfSightRequest(_campaign, observer.TokenId, target.TokenId, TestCorrelationId);
            Result<bool> clear = TokenVisionService.ComputeLineOfSight(_scenes, _vision, _obstacles, clearRequest);
            Assert.That(clear.IsSuccess, Is.True);
            Assert.That(clear.Value, Is.True, "opening the door must restore line of sight, with no other state changed");
        }

        [Test] // TC-PERSIST-106
        public void ComputeLineOfSight_ObserverAndTargetInDifferentScenes_IsTypedError()
        {
            TokenRecord observer = CreateToken(Host, 0, 0);
            SceneId otherSceneId = _scenes.CreateScene(_campaign, "Second Map", NewCommandId(), TestCorrelationId).Value.SceneId;
            TokenRecord target = _scenes.CreateToken(_campaign, otherSceneId, new TokenPosition(5, 5), Host, NewCommandId(), TestCorrelationId).Value;

            var request = new ComputeLineOfSightRequest(_campaign, observer.TokenId, target.TokenId, TestCorrelationId);
            Result<bool> result = TokenVisionService.ComputeLineOfSight(_scenes, _vision, _obstacles, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.TokenVisionObserverAndTargetNotInSameScene));
        }

        [Test] // TC-PERSIST-107
        public void ComputeLineOfSight_PerformsNoMutation_OfTokensOrObstacles()
        {
            TokenRecord observer = CreateToken(Host, 0, 0);
            TokenRecord target = CreateToken(Host, 10, 0);
            _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 5, -5, 5, 5, NewCommandId(), TestCorrelationId);

            var request = new ComputeLineOfSightRequest(_campaign, observer.TokenId, target.TokenId, TestCorrelationId);
            TokenVisionService.ComputeLineOfSight(_scenes, _vision, _obstacles, request);
            TokenVisionService.ComputeLineOfSight(_scenes, _vision, _obstacles, request);

            Result<TokenRecord> observerAfter = _scenes.GetToken(_campaign, observer.TokenId, TestCorrelationId);
            Result<TokenRecord> targetAfter = _scenes.GetToken(_campaign, target.TokenId, TestCorrelationId);
            Assert.That(observerAfter.Value.Revision, Is.EqualTo(observer.Revision), "a read-only query must never bump a token's revision");
            Assert.That(targetAfter.Value.Revision, Is.EqualTo(target.Revision));
            Assert.That(_obstacles.ListObstacles(_campaign, _sceneId, TestCorrelationId).Value.Count, Is.EqualTo(1));
        }

        [Test] // TC-PERSIST-108
        public void CreateToken_SeedsADefaultOmnidirectionalVisionRow()
        {
            TokenRecord token = CreateToken(Host, 3, 4);

            Result<TokenVisionSettingsRecord> vision = _vision.GetVisionSettings(_campaign, token.TokenId, TestCorrelationId);

            Assert.That(vision.IsSuccess, Is.True, "every token must have a vision row from the moment it is created (Path A)");
            Assert.That(vision.Value.FacingDegrees, Is.EqualTo(0));
            Assert.That(vision.Value.FovAngleDegrees, Is.EqualTo(TokenVisionSettingsRecord.DefaultFovAngleDegrees));
            Assert.That(vision.Value.ViewDistance, Is.EqualTo(TokenVisionSettingsRecord.DefaultViewDistance));
            Assert.That(vision.Value.Revision, Is.EqualTo(1));
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
