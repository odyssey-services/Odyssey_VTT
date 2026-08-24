using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence
{
    public sealed class SqliteSceneRepositoryTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private string _workDir = null!;
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;

        [SetUp]
        public void SetUp()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "ody-s01-008-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_workDir, "Scene Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = _campaignRepository.Create(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                _campaignRepository.Close(_campaign, TestCorrelationId);
            }
            catch (IOException) { }

            try
            {
                if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup only.
            }
        }

        [Test]
        public void CreateScene_ReturnsDraftScene_AtRevisionOne()
        {
            var repository = new SqliteSceneRepository(Clock);
            Result<SceneRecord> result = repository.CreateScene(_campaign, "Tavern", NewCommandId(), TestCorrelationId);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Name, Is.EqualTo("Tavern"));
            Assert.That(result.Value.Status, Is.EqualTo("Draft"));
            Assert.That(result.Value.Revision, Is.EqualTo(1));
            Assert.That(result.Value.CampaignId, Is.EqualTo(_campaign.CampaignId));
            Assert.That(result.Value.SceneId.IsValid, Is.True);
        }

        [Test]
        public void CreateTwoTokens_ThenMoveThem_PersistsIndependentPositions()
        {
            var sceneRepository = new SqliteSceneRepository(Clock);
            SceneId sceneId = sceneRepository.CreateScene(_campaign, "Battle Map", NewCommandId(), TestCorrelationId).Value.SceneId;

            Result<TokenRecord> tokenA = sceneRepository.CreateToken(_campaign, sceneId, new TokenPosition(1, 1), NewCommandId(), TestCorrelationId);
            Result<TokenRecord> tokenB = sceneRepository.CreateToken(_campaign, sceneId, new TokenPosition(2, 2), NewCommandId(), TestCorrelationId);
            Assert.That(tokenA.IsSuccess, Is.True);
            Assert.That(tokenB.IsSuccess, Is.True);
            Assert.That(tokenA.Value.TokenId, Is.Not.EqualTo(tokenB.Value.TokenId));

            Result<TokenRecord> movedA = sceneRepository.MoveToken(_campaign, tokenA.Value.TokenId, new TokenPosition(5, 5), NewCommandId(), TestCorrelationId);
            Assert.That(movedA.IsSuccess, Is.True);
            Assert.That(movedA.Value.Position.X, Is.EqualTo(5));
            Assert.That(movedA.Value.Position.Y, Is.EqualTo(5));
            Assert.That(movedA.Value.Revision, Is.EqualTo(2));

            Result<IReadOnlyList<TokenRecord>> tokens = sceneRepository.ListTokens(_campaign, sceneId, TestCorrelationId);
            Assert.That(tokens.IsSuccess, Is.True);
            Assert.That(tokens.Value.Count, Is.EqualTo(2));

            TokenRecord persistedA = Find(tokens.Value, tokenA.Value.TokenId);
            TokenRecord persistedB = Find(tokens.Value, tokenB.Value.TokenId);
            Assert.That(persistedA.Position.X, Is.EqualTo(5));
            Assert.That(persistedA.Position.Y, Is.EqualTo(5));
            Assert.That(persistedB.Position.X, Is.EqualTo(2));
            Assert.That(persistedB.Position.Y, Is.EqualTo(2));
        }

        [Test]
        public void CreateToken_OnNonExistentScene_ReturnsTypedSceneNotFound()
        {
            var repository = new SqliteSceneRepository(Clock);
            SceneId phantomScene = SceneId.NewId(Clock.GetUtcNow());

            Result<TokenRecord> result = repository.CreateToken(_campaign, phantomScene, new TokenPosition(0, 0), NewCommandId(), TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceSceneNotFound));
            Assert.That(result.Error.Category, Is.EqualTo(ErrorCategory.NotFound));
        }

        [Test]
        public void MoveToken_OnNonExistentToken_ReturnsTypedTokenNotFound()
        {
            var repository = new SqliteSceneRepository(Clock);
            TokenId phantomToken = TokenId.NewId(Clock.GetUtcNow());

            Result<TokenRecord> result = repository.MoveToken(_campaign, phantomToken, new TokenPosition(1, 1), NewCommandId(), TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceTokenNotFound));
        }

        [Test]
        public void RegisterAsset_CopiesFileIntoAssetsObjects_ComputesHashAndSize_StoresOnlyRelativePath()
        {
            string sourceFile = Path.Combine(Path.GetTempPath(), "ody-s01-008-source-" + Guid.NewGuid().ToString("N") + ".txt");
            byte[] content = System.Text.Encoding.UTF8.GetBytes("synthetic test map content");
            File.WriteAllBytes(sourceFile, content);

            try
            {
                var repository = new SqliteSceneRepository(Clock);
                Result<AssetManifestEntryRecord> result = repository.RegisterAsset(_campaign, sourceFile, NewCommandId(), TestCorrelationId);

                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value.RelativePath, Does.StartWith("Assets/Objects/"));
                Assert.That(result.Value.RelativePath, Does.Not.Contain(sourceFile));
                Assert.That(result.Value.SizeBytes, Is.EqualTo(content.LongLength));
                Assert.That(result.Value.Sha256Hash, Has.Length.EqualTo(64));

                string copiedPath = Path.Combine(_workDir, result.Value.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                Assert.That(File.Exists(copiedPath), Is.True);
                Assert.That(File.ReadAllBytes(copiedPath), Is.EqualTo(content));
            }
            finally
            {
                File.Delete(sourceFile);
            }
        }

        [Test]
        public void RegisterAsset_OnMissingSourceFile_ReturnsTypedError_NoRawException()
        {
            var repository = new SqliteSceneRepository(Clock);
            Result<AssetManifestEntryRecord> result = repository.RegisterAsset(_campaign, Path.Combine(_workDir, "does-not-exist.png"), NewCommandId(), TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceSceneIoFailed));
        }

        private static TokenRecord Find(IReadOnlyList<TokenRecord> tokens, TokenId id)
        {
            foreach (TokenRecord token in tokens)
            {
                if (token.TokenId == id) return token;
            }

            throw new InvalidOperationException("Token not found in list.");
        }
    }
}
