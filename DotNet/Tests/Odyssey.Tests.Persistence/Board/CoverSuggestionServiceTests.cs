using System;
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
    /// SLICE-10 Block 5: full-integration tests for <see cref="CoverSuggestionService.SuggestCover"/>
    /// against real <see cref="SqliteSceneRepository"/>/<see cref="SqliteObstacleRepository"/> data.
    /// No authorization to test here -- <c>SuggestCover</c> performs none, by design (a read-only hint,
    /// same precedent as `ListObstacles`/`ComputeLineOfSight`). Continues TC-PERSIST numbering from
    /// <c>CoverTests</c> (which ends at TC-PERSIST-125).
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
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaignRepository.Close(_campaign, TestCorrelationId); } catch (IOException) { }
            try { if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true); } catch (IOException) { }
        }

        private TokenRecord CreateToken(double x, double y, SceneId? sceneId = null)
        {
            return _scenes.CreateToken(_campaign, sceneId ?? _sceneId, new TokenPosition(x, y), Host, NewCommandId(), TestCorrelationId).Value;
        }

        [Test] // TC-PERSIST-126
        public void SuggestCover_MatchesDirectGeometryCall_NoObstacles_IsNone()
        {
            TokenRecord attacker = CreateToken(0, 0);
            TokenRecord target = CreateToken(10, 0);

            var request = new SuggestCoverRequest(_campaign, attacker.TokenId, target.TokenId, TestCorrelationId);
            Result<CoverDegree> result = CoverSuggestionService.SuggestCover(_scenes, _obstacles, request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(CoverDegree.None));
        }

        [Test] // TC-PERSIST-127
        public void SuggestCover_MatchesDirectGeometryCall_WallBlocksAllSamples_IsFull()
        {
            TokenRecord attacker = CreateToken(0, 0);
            TokenRecord target = CreateToken(10, 0);
            _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 5, -5, 5, 5, NewCommandId(), TestCorrelationId);

            var request = new SuggestCoverRequest(_campaign, attacker.TokenId, target.TokenId, TestCorrelationId);
            Result<CoverDegree> result = CoverSuggestionService.SuggestCover(_scenes, _obstacles, request);

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

            var request = new SuggestCoverRequest(_campaign, attacker.TokenId, target.TokenId, TestCorrelationId);
            Result<CoverDegree> result = CoverSuggestionService.SuggestCover(_scenes, _obstacles, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.CoverSuggestionAttackerAndTargetNotInSameScene));
        }

        [Test] // TC-PERSIST-129
        public void SuggestCover_PerformsNoMutation_OfTokensOrObstacles()
        {
            TokenRecord attacker = CreateToken(0, 0);
            TokenRecord target = CreateToken(10, 0);
            _obstacles.CreateObstacle(_campaign, _sceneId, ObstacleKind.Wall, 5, -5, 5, 5, NewCommandId(), TestCorrelationId);

            var request = new SuggestCoverRequest(_campaign, attacker.TokenId, target.TokenId, TestCorrelationId);
            CoverSuggestionService.SuggestCover(_scenes, _obstacles, request);
            CoverSuggestionService.SuggestCover(_scenes, _obstacles, request);

            Result<TokenRecord> attackerAfter = _scenes.GetToken(_campaign, attacker.TokenId, TestCorrelationId);
            Result<TokenRecord> targetAfter = _scenes.GetToken(_campaign, target.TokenId, TestCorrelationId);
            Assert.That(attackerAfter.Value.Revision, Is.EqualTo(attacker.Revision), "a read-only hint must never bump a token's revision");
            Assert.That(targetAfter.Value.Revision, Is.EqualTo(target.Revision));
            Assert.That(_obstacles.ListObstacles(_campaign, _sceneId, TestCorrelationId).Value.Count, Is.EqualTo(1));
        }
    }
}
