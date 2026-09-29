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
    /// SLICE-10 Block 4: real authorization and full-integration tests for
    /// <see cref="PlayerVisibilityService"/> -- live token visibility (a thin composition over
    /// Block 3's own <see cref="TokenVisionService.ComputeLineOfSight"/>, no new storage), persistent
    /// map memory (<see cref="IFogOfWarRepository"/>, INSERT-only), and the self-scoped-or-MainGM
    /// read authorization introduced by this task. Continues TC-PERSIST numbering from
    /// <c>TokenVisionServiceTests</c> (which ends at TC-PERSIST-108).
    /// </summary>
    public sealed class PlayerVisibilityServiceTests
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
        private SqliteFogOfWarRepository _fog = null!;
        private SceneId _sceneId;

        [SetUp]
        public void SetUp()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "ody-slice10-block4-visibility-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_workDir, "Player Visibility Test Campaign", "ruleset.core", "1.0.0", "0.1.0", Host);
            Result<CampaignHandle> created = _campaignRepository.Create(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _scenes = new SqliteSceneRepository(Clock);
            _sceneId = _scenes.CreateScene(_campaign, "Battle Map", NewCommandId(), TestCorrelationId).Value.SceneId;
            _obstacles = new SqliteObstacleRepository(Clock);
            _vision = new SqliteTokenVisionRepository(Clock);
            _fog = new SqliteFogOfWarRepository(Clock);
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

        [Test] // TC-PERSIST-109
        public void ComputeVisibleTokens_UsesLiveLineOfSight_OwnTokenAlwaysVisible_WallBlocks()
        {
            UserId player = NewUserId();
            Assert.That(_campaignRepository.AddMember(_campaign, player, CampaignMembershipRole.Player, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);
            TokenRecord own = CreateToken(player, 0, 0);
            TokenRecord clearlyVisible = CreateToken(Host, 5, 0);
            TokenRecord behindWall = CreateToken(Host, 20, 0);
            _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 10, -5, 10, 5, NewCommandId(), TestCorrelationId);

            var request = new ComputeVisibleTokensRequest(_campaign, _sceneId, player, player, TestCorrelationId);
            Result<IReadOnlyCollection<TokenId>> result = PlayerVisibilityService.ComputeVisibleTokens(_scenes, _vision, _obstacles, _campaignRepository, request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Does.Contain(own.TokenId), "a player's own token must always be visible to itself");
            Assert.That(result.Value, Does.Contain(clearlyVisible.TokenId), "an unobstructed token within range/FOV must be visible");
            Assert.That(result.Value, Does.Not.Contain(behindWall.TokenId), "a token behind a wall must not be visible");
        }

        [Test] // TC-PERSIST-110
        public void ComputeVisibleTokens_NoOwnTokenInScene_IsEmpty_NotAnError()
        {
            UserId player = NewUserId();
            Assert.That(_campaignRepository.AddMember(_campaign, player, CampaignMembershipRole.Player, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);
            CreateToken(Host, 0, 0);

            var request = new ComputeVisibleTokensRequest(_campaign, _sceneId, player, player, TestCorrelationId);
            Result<IReadOnlyCollection<TokenId>> result = PlayerVisibilityService.ComputeVisibleTokens(_scenes, _vision, _obstacles, _campaignRepository, request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Empty);
        }

        [Test] // TC-PERSIST-111
        public void ComputeVisibleTokens_ForMainGmTarget_SeesEveryToken_UnconditionallyBypassingLos()
        {
            UserId mainGm = NewUserId();
            Assert.That(_campaignRepository.AddMember(_campaign, mainGm, CampaignMembershipRole.MainGm, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);
            TokenRecord farAway = CreateToken(Host, 10000, 10000);
            _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, -5, -5, 5, 5, NewCommandId(), TestCorrelationId);

            var request = new ComputeVisibleTokensRequest(_campaign, _sceneId, mainGm, mainGm, TestCorrelationId);
            Result<IReadOnlyCollection<TokenId>> result = PlayerVisibilityService.ComputeVisibleTokens(_scenes, _vision, _obstacles, _campaignRepository, request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Does.Contain(farAway.TokenId), "MainGm must see every token in the scene, including one far outside any live LOS/range, without touching stored fog data");
        }

        [Test] // TC-PERSIST-112
        public void RecordExploration_FirstCallInserts_SecondCallInsideCoveredAreaDoesNotDuplicate()
        {
            UserId player = NewUserId();
            TokenRecord observer = CreateToken(player, 0, 0);

            var first = new RecordExplorationRequest(_campaign, observer.TokenId, NewCommandId(), TestCorrelationId);
            Result firstResult = PlayerVisibilityService.RecordExploration(_fog, _scenes, _vision, first);
            Assert.That(firstResult.IsSuccess, Is.True);

            Result<IReadOnlyList<FogRevealRecord>> afterFirst = _fog.ListReveals(_campaign, _sceneId, player, TestCorrelationId);
            Assert.That(afterFirst.Value.Count, Is.EqualTo(1), "the first exploration at a fresh position must create exactly one reveal");

            var second = new RecordExplorationRequest(_campaign, observer.TokenId, NewCommandId(), TestCorrelationId);
            Result secondResult = PlayerVisibilityService.RecordExploration(_fog, _scenes, _vision, second);
            Assert.That(secondResult.IsSuccess, Is.True);

            Result<IReadOnlyList<FogRevealRecord>> afterSecond = _fog.ListReveals(_campaign, _sceneId, player, TestCorrelationId);
            Assert.That(afterSecond.Value.Count, Is.EqualTo(1), "a second call from the same, already-covered position must not insert a duplicate row -- map memory is INSERT-only but not duplicate-insert-only");
        }

        [Test] // TC-PERSIST-113
        public void RecordExploration_MovingBeyondCoveredArea_InsertsANewReveal()
        {
            UserId player = NewUserId();
            TokenRecord observer = CreateToken(player, 0, 0);
            PlayerVisibilityService.RecordExploration(_fog, _scenes, _vision, new RecordExplorationRequest(_campaign, observer.TokenId, NewCommandId(), TestCorrelationId));

            _scenes.MoveToken(_campaign, observer.TokenId, new TokenPosition(1000, 1000), observer.Revision, NewCommandId(), TestCorrelationId);
            Result moved = PlayerVisibilityService.RecordExploration(_fog, _scenes, _vision, new RecordExplorationRequest(_campaign, observer.TokenId, NewCommandId(), TestCorrelationId));
            Assert.That(moved.IsSuccess, Is.True);

            Result<IReadOnlyList<FogRevealRecord>> reveals = _fog.ListReveals(_campaign, _sceneId, player, TestCorrelationId);
            Assert.That(reveals.Value.Count, Is.EqualTo(2), "a genuinely new, uncovered position must insert a second reveal, not be silently dropped");
        }

        [Test] // TC-PERSIST-114
        public void IsPointExplored_InsideAReveal_True_OutsideAllReveals_False()
        {
            UserId player = NewUserId();
            TokenRecord observer = CreateToken(player, 0, 0);
            PlayerVisibilityService.RecordExploration(_fog, _scenes, _vision, new RecordExplorationRequest(_campaign, observer.TokenId, NewCommandId(), TestCorrelationId));

            var inside = new IsPointExploredRequest(_campaign, _sceneId, 1, 1, player, player, TestCorrelationId);
            Result<bool> insideResult = PlayerVisibilityService.IsPointExplored(_fog, _campaignRepository, inside);
            Assert.That(insideResult.IsSuccess, Is.True);
            Assert.That(insideResult.Value, Is.True);

            var outside = new IsPointExploredRequest(_campaign, _sceneId, 100000, 100000, player, player, TestCorrelationId);
            Result<bool> outsideResult = PlayerVisibilityService.IsPointExplored(_fog, _campaignRepository, outside);
            Assert.That(outsideResult.IsSuccess, Is.True);
            Assert.That(outsideResult.Value, Is.False);
        }

        [Test] // TC-PERSIST-115
        public void IsPointExplored_ForMainGmTarget_IsAlwaysTrue_WithoutTouchingStoredReveals()
        {
            UserId mainGm = NewUserId();
            Assert.That(_campaignRepository.AddMember(_campaign, mainGm, CampaignMembershipRole.MainGm, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);

            var request = new IsPointExploredRequest(_campaign, _sceneId, 999999, -999999, mainGm, mainGm, TestCorrelationId);
            Result<bool> result = PlayerVisibilityService.IsPointExplored(_fog, _campaignRepository, request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.True, "MainGm's own map memory must be everything, unconditionally, with zero reveals ever recorded");
        }

        [Test] // TC-PERSIST-116
        public void SelfScopedRead_OrdinaryPlayerAskingAboutAnotherUser_IsDenied_ForBothQueries()
        {
            UserId askingPlayer = NewUserId();
            UserId otherPlayer = NewUserId();
            Assert.That(_campaignRepository.AddMember(_campaign, askingPlayer, CampaignMembershipRole.Player, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);
            Assert.That(_campaignRepository.AddMember(_campaign, otherPlayer, CampaignMembershipRole.Player, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);

            var visibleRequest = new ComputeVisibleTokensRequest(_campaign, _sceneId, askingPlayer, otherPlayer, TestCorrelationId);
            Result<IReadOnlyCollection<TokenId>> visibleResult = PlayerVisibilityService.ComputeVisibleTokens(_scenes, _vision, _obstacles, _campaignRepository, visibleRequest);
            Assert.That(visibleResult.IsFailure, Is.True, "an ordinary player must not read another ordinary player's live visibility");
            Assert.That(visibleResult.Error.Code, Is.EqualTo(ErrorCodes.PlayerVisibilityTargetUserDenied));

            var exploredRequest = new IsPointExploredRequest(_campaign, _sceneId, 0, 0, askingPlayer, otherPlayer, TestCorrelationId);
            Result<bool> exploredResult = PlayerVisibilityService.IsPointExplored(_fog, _campaignRepository, exploredRequest);
            Assert.That(exploredResult.IsFailure, Is.True, "an ordinary player must not read another ordinary player's map memory either");
            Assert.That(exploredResult.Error.Code, Is.EqualTo(ErrorCodes.PlayerVisibilityTargetUserDenied));
        }

        [Test] // TC-PERSIST-117
        public void SelfScopedRead_OrdinaryPlayerAskingAboutTheMainGm_IsDenied()
        {
            UserId askingPlayer = NewUserId();
            Assert.That(_campaignRepository.AddMember(_campaign, askingPlayer, CampaignMembershipRole.Player, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);

            var request = new ComputeVisibleTokensRequest(_campaign, _sceneId, askingPlayer, Host, TestCorrelationId);
            Result<IReadOnlyCollection<TokenId>> result = PlayerVisibilityService.ComputeVisibleTokens(_scenes, _vision, _obstacles, _campaignRepository, request);

            Assert.That(result.IsFailure, Is.True, "asking about the MainGm's own state is still a different-target read -- the requester being an ordinary player is what is checked, not who the target is");
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PlayerVisibilityTargetUserDenied));
        }

        [Test] // TC-PERSIST-118
        public void SelfScopedRead_MainGmAskingAboutAnotherUser_Succeeds()
        {
            UserId player = NewUserId();
            Assert.That(_campaignRepository.AddMember(_campaign, player, CampaignMembershipRole.Player, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);
            CreateToken(player, 3, 3);

            var request = new ComputeVisibleTokensRequest(_campaign, _sceneId, Host, player, TestCorrelationId);
            Result<IReadOnlyCollection<TokenId>> result = PlayerVisibilityService.ComputeVisibleTokens(_scenes, _vision, _obstacles, _campaignRepository, request);

            Assert.That(result.IsSuccess, Is.True, "the registered MainGm must be able to read another participant's own visibility state, e.g. for support/debugging");
        }

        [Test] // TC-PERSIST-119
        public void SelfScopedRead_FailsClosed_WhenTheMembershipLookupFails()
        {
            var poisoned = PoisonedCampaignRepository.FailsOnLookup();
            UserId askingUser = NewUserId();
            UserId otherUser = NewUserId();

            var request = new ComputeVisibleTokensRequest(_campaign, _sceneId, askingUser, otherUser, TestCorrelationId);
            Result<IReadOnlyCollection<TokenId>> result = PlayerVisibilityService.ComputeVisibleTokens(_scenes, _vision, _obstacles, poisoned, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCampaignIoFailed), "an unreadable membership must never fall back to a pass or a fake denial");
        }

        private sealed class PoisonedCampaignRepository : ICampaignRepository
        {
            public static PoisonedCampaignRepository FailsOnLookup() => new PoisonedCampaignRepository();

            public Result<CampaignMemberLookup> GetMemberRole(CampaignHandle campaign, UserId userId, CorrelationId correlationId)
            {
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
