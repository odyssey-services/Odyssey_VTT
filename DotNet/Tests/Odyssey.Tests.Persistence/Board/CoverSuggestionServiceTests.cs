using System;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Board;
using Odyssey.Application.Commands;
using Odyssey.Application.Identity;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Geometry;
using Odyssey.Domain.Identity;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence.Board
{
    /// <summary>
    /// SLICE-10 Block 5: full-integration tests for <see cref="CoverSuggestionService.SuggestCover"/>
    /// against real <see cref="SqliteSceneRepository"/>/<see cref="SqliteObstacleRepository"/> data.
    /// The pre-existing TC-PERSIST-126..129 run as the MainGm (unrestricted, unchanged behaviour). ODY-S11-222 added
    /// the visibility check for every other requester (TC-COVERVIS-001..004 below). Continues TC-PERSIST numbering
    /// from <c>CoverTests</c> (which ends at TC-PERSIST-125).
    /// </summary>
    public sealed class CoverSuggestionServiceTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static readonly UserId Host = global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));

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
            _workDir = Path.Combine(Path.GetTempPath(), "ody-slice10-block5-cover-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_workDir, "Cover Suggestion Test Campaign", "ruleset.core", "1.0.0", "0.1.0", Host);
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

        private TokenRecord CreateToken(double x, double y, SceneId? sceneId = null, UserId? controller = null)
        {
            return _scenes.CreateToken(_campaign, sceneId ?? _sceneId, new TokenPosition(x, y), controller ?? Host, NewCommandId(), TestCorrelationId).Value;
        }

        // ODY-S11-222: a real Player member. Their own token at the origin sees within the default 100-unit view distance.
        private UserId AddPlayer()
        {
            UserId player = UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
            Assert.That(_campaignRepository.AddMember(_campaign, player, CampaignMembershipRole.Player, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);
            return player;
        }

        private Result<CoverDegree> Cover(UserId requester, TokenId attacker, TokenId target) =>
            CoverSuggestionService.SuggestCover(_scenes, _obstacles, _vision, _campaignRepository, new SuggestCoverRequest(_campaign, requester, attacker, target, TestCorrelationId));

        [Test] // TC-COVERVIS-001
        public void SuggestCover_PlayerAskingAboutAHiddenToken_IsRefused_WithTheSameErrorAsForAMissingOne()
        {
            UserId player = AddPlayer();
            TokenRecord own = CreateToken(0, 0, controller: player);
            TokenRecord hidden = CreateToken(500, 0); // beyond the player's view distance
            _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 250, -5, 250, 5, NewCommandId(), TestCorrelationId);

            Result<CoverDegree> asTarget = Cover(player, own.TokenId, hidden.TokenId);
            Assert.That(asTarget.IsFailure, Is.True, "no cover answer about a token the player cannot see");
            Assert.That(asTarget.Error.Code, Is.EqualTo(ErrorCodes.CoverSuggestionTokenUnavailable));
            Assert.That(asTarget.Error.SafeReasonCode, Is.EqualTo(SafeReasonCode.TargetUnavailable));

            Result<CoverDegree> asAttacker = Cover(player, hidden.TokenId, own.TokenId);
            Assert.That(asAttacker.IsFailure, Is.True, "nor with the hidden token as the attacker");
            Assert.That(asAttacker.Error.Code, Is.EqualTo(ErrorCodes.CoverSuggestionTokenUnavailable));

            // No oracle: a token id that does not exist at all, and a token on another Scene, get exactly the same refusal.
            Result<CoverDegree> missing = Cover(player, own.TokenId, TokenId.Parse("tok_" + Guid.NewGuid().ToString("N")));
            SceneId otherScene = _scenes.CreateScene(_campaign, "Second Map", NewCommandId(), TestCorrelationId).Value.SceneId;
            Result<CoverDegree> elsewhere = Cover(player, own.TokenId, CreateToken(1, 0, otherScene).TokenId);
            foreach (Result<CoverDegree> refusal in new[] { missing, elsewhere })
            {
                Assert.That(refusal.IsFailure, Is.True);
                Assert.That(refusal.Error.Code, Is.EqualTo(asTarget.Error.Code));
                Assert.That(refusal.Error.SafeReasonCode, Is.EqualTo(asTarget.Error.SafeReasonCode));
                Assert.That(refusal.Error.UserMessageKey, Is.EqualTo(asTarget.Error.UserMessageKey));
                Assert.That(refusal.Error.Category, Is.EqualTo(asTarget.Error.Category));
            }
        }

        [Test] // TC-COVERVIS-002
        public void SuggestCover_MainGmAskingAboutTheSamePair_WorksAsBefore()
        {
            UserId player = AddPlayer();
            TokenRecord own = CreateToken(0, 0, controller: player);
            TokenRecord hidden = CreateToken(500, 0);
            _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 250, -5, 250, 5, NewCommandId(), TestCorrelationId);

            Result<CoverDegree> result = Cover(Host, own.TokenId, hidden.TokenId);
            Assert.That(result.IsSuccess, Is.True, "the MainGm is unrestricted");
            Assert.That(result.Value, Is.EqualTo(CoverDegree.Full));
        }

        [Test] // TC-COVERVIS-003
        public void SuggestCover_PlayerAskingAboutTokensTheyCanSee_GetsTheAnswer()
        {
            UserId player = AddPlayer();
            TokenRecord own = CreateToken(0, 0, controller: player);
            TokenRecord near = CreateToken(10, 0);
            _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Window, 5, -5, 5, 5, NewCommandId(), TestCorrelationId);

            Result<CoverDegree> result = Cover(player, own.TokenId, near.TokenId);
            Assert.That(result.IsSuccess, Is.True, "visible tokens are not over-restricted");
            CoverDegree direct = CoverGeometry.ComputeCoverDegree(0, 0, 10, 0, new[] { new ObstacleSegment(ObstacleKind.Window, null, 5, -5, 5, 5) });
            Assert.That(result.Value, Is.EqualTo(direct));
        }

        [Test] // TC-COVERVIS-004
        public void SuggestCover_NonMemberRequester_IsRefused_WithoutAnyTokenInformation()
        {
            TokenRecord a = CreateToken(0, 0);
            TokenRecord b = CreateToken(10, 0);
            UserId stranger = UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

            Result<CoverDegree> result = Cover(stranger, a.TokenId, b.TokenId);
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.Not.EqualTo(ErrorCodes.CoverSuggestionAttackerAndTargetNotInSameScene));
        }

        [Test] // TC-PERSIST-126
        public void SuggestCover_MatchesDirectGeometryCall_NoObstacles_IsNone()
        {
            TokenRecord attacker = CreateToken(0, 0);
            TokenRecord target = CreateToken(10, 0);

            var request = new SuggestCoverRequest(_campaign, Host, attacker.TokenId, target.TokenId, TestCorrelationId);
            Result<CoverDegree> result = CoverSuggestionService.SuggestCover(_scenes, _obstacles, _vision, _campaignRepository, request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(CoverDegree.None));
        }

        [Test] // TC-PERSIST-127
        public void SuggestCover_MatchesDirectGeometryCall_WallBlocksAllSamples_IsFull()
        {
            TokenRecord attacker = CreateToken(0, 0);
            TokenRecord target = CreateToken(10, 0);
            _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 5, -5, 5, 5, NewCommandId(), TestCorrelationId);

            var request = new SuggestCoverRequest(_campaign, Host, attacker.TokenId, target.TokenId, TestCorrelationId);
            Result<CoverDegree> result = CoverSuggestionService.SuggestCover(_scenes, _obstacles, _vision, _campaignRepository, request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(CoverDegree.Full));

            // Must match a direct call to the same pure Domain function on the same coordinates/obstacles.
            var segments = new[] { new ObstacleSegment(ObstacleKind.Wall, null, 5, -5, 5, 5) };
            CoverDegree direct = CoverGeometry.ComputeCoverDegree(attacker.Position.X, attacker.Position.Y, target.Position.X, target.Position.Y, segments);
            Assert.That(result.Value, Is.EqualTo(direct));
        }

        [Test] // TC-PERSIST-128
        public void SuggestCover_AttackerAndTargetInDifferentScenes_IsTypedError()
        {
            TokenRecord attacker = CreateToken(0, 0);
            SceneId otherSceneId = _scenes.CreateScene(_campaign, "Second Map", NewCommandId(), TestCorrelationId).Value.SceneId;
            TokenRecord target = CreateToken(5, 5, otherSceneId);

            var request = new SuggestCoverRequest(_campaign, Host, attacker.TokenId, target.TokenId, TestCorrelationId);
            Result<CoverDegree> result = CoverSuggestionService.SuggestCover(_scenes, _obstacles, _vision, _campaignRepository, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.CoverSuggestionAttackerAndTargetNotInSameScene));
        }

        [Test] // TC-PERSIST-129
        public void SuggestCover_PerformsNoMutation_OfTokensOrObstacles()
        {
            TokenRecord attacker = CreateToken(0, 0);
            TokenRecord target = CreateToken(10, 0);
            _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 5, -5, 5, 5, NewCommandId(), TestCorrelationId);

            var request = new SuggestCoverRequest(_campaign, Host, attacker.TokenId, target.TokenId, TestCorrelationId);
            CoverSuggestionService.SuggestCover(_scenes, _obstacles, _vision, _campaignRepository, request);
            CoverSuggestionService.SuggestCover(_scenes, _obstacles, _vision, _campaignRepository, request);

            Result<TokenRecord> attackerAfter = _scenes.GetToken(_campaign, attacker.TokenId, TestCorrelationId);
            Result<TokenRecord> targetAfter = _scenes.GetToken(_campaign, target.TokenId, TestCorrelationId);
            Assert.That(attackerAfter.Value.Revision, Is.EqualTo(attacker.Revision), "a read-only hint must never bump a token's revision");
            Assert.That(targetAfter.Value.Revision, Is.EqualTo(target.Revision));
            Assert.That(_obstacles.ListObstacles(_campaign, _sceneId, TestCorrelationId).Value.Count, Is.EqualTo(1));
        }
    }
}
