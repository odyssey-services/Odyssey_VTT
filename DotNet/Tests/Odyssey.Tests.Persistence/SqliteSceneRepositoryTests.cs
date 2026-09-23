using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
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
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
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

            Result<TokenRecord> tokenA = sceneRepository.CreateToken(_campaign, sceneId, new TokenPosition(1, 1), NewUserId(), NewCommandId(), TestCorrelationId);
            Result<TokenRecord> tokenB = sceneRepository.CreateToken(_campaign, sceneId, new TokenPosition(2, 2), NewUserId(), NewCommandId(), TestCorrelationId);
            Assert.That(tokenA.IsSuccess, Is.True);
            Assert.That(tokenB.IsSuccess, Is.True);
            Assert.That(tokenA.Value.TokenId, Is.Not.EqualTo(tokenB.Value.TokenId));

            Result<TokenRecord> movedA = sceneRepository.MoveToken(_campaign, tokenA.Value.TokenId, new TokenPosition(5, 5), tokenA.Value.Revision, NewCommandId(), TestCorrelationId);
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

        [Test] // TC-BOARD-014
        public void CreateToken_WithCharacterId_LinksTokenToCharacter_GetTokenReturnsItBack()
        {
            var repository = new SqliteSceneRepository(Clock);
            SceneId sceneId = repository.CreateScene(_campaign, "Battle Map", NewCommandId(), TestCorrelationId).Value.SceneId;
            CharacterId characterId = CharacterId.Parse("char_0123456789abcdef0123456789abcdef");

            Result<TokenRecord> created = repository.CreateToken(_campaign, sceneId, new TokenPosition(3, 4), NewUserId(), NewCommandId(), TestCorrelationId, characterId);
            Assert.That(created.IsSuccess, Is.True);
            Assert.That(created.Value.CharacterId, Is.EqualTo(characterId));

            Result<TokenRecord> reread = repository.GetToken(_campaign, created.Value.TokenId, TestCorrelationId);
            Assert.That(reread.IsSuccess, Is.True);
            Assert.That(reread.Value.CharacterId, Is.EqualTo(characterId));
        }

        [Test] // TC-BOARD-015
        public void CreateToken_WithoutCharacterId_DefaultsToNull_ExistingBehaviorUnchanged()
        {
            var repository = new SqliteSceneRepository(Clock);
            SceneId sceneId = repository.CreateScene(_campaign, "Battle Map", NewCommandId(), TestCorrelationId).Value.SceneId;

            Result<TokenRecord> created = repository.CreateToken(_campaign, sceneId, new TokenPosition(0, 0), NewUserId(), NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            Assert.That(created.Value.CharacterId, Is.Null);

            Result<TokenRecord> reread = repository.GetToken(_campaign, created.Value.TokenId, TestCorrelationId);
            Assert.That(reread.IsSuccess, Is.True);
            Assert.That(reread.Value.CharacterId, Is.Null);
        }

        [Test]
        public void CreateToken_OnNonExistentScene_ReturnsTypedSceneNotFound()
        {
            var repository = new SqliteSceneRepository(Clock);
            SceneId phantomScene = SceneId.NewId(Clock.GetUtcNow());

            Result<TokenRecord> result = repository.CreateToken(_campaign, phantomScene, new TokenPosition(0, 0), NewUserId(), NewCommandId(), TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceSceneNotFound));
            Assert.That(result.Error.Category, Is.EqualTo(ErrorCategory.NotFound));
        }

        [Test]
        public void MoveToken_OnNonExistentToken_ReturnsTypedTokenNotFound()
        {
            var repository = new SqliteSceneRepository(Clock);
            TokenId phantomToken = TokenId.NewId(Clock.GetUtcNow());

            Result<TokenRecord> result = repository.MoveToken(_campaign, phantomToken, new TokenPosition(1, 1), 1, NewCommandId(), TestCorrelationId);

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

        // ---- ODY-S07-105: SetSceneBackground ---------------------------------------

        private AssetId RegisterTestAsset(CampaignHandle campaign, SqliteSceneRepository repository)
        {
            string sourceFile = Path.Combine(Path.GetTempPath(), "ody-s07-105-source-" + Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(sourceFile, System.Text.Encoding.UTF8.GetBytes("synthetic background map"));
            try
            {
                Result<AssetManifestEntryRecord> registered = repository.RegisterAsset(campaign, sourceFile, NewCommandId(), TestCorrelationId);
                Assert.That(registered.IsSuccess, Is.True, "test fixture asset registration must itself succeed");
                return registered.Value.AssetId;
            }
            finally
            {
                File.Delete(sourceFile);
            }
        }

        private int CountAssetReferences(CampaignHandle campaign, AssetId assetId, SceneId sceneId)
        {
            using var connection = new SqliteConnection("Data Source=" + Path.Combine(campaign.RootPath, "campaign.db"));
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM AssetReferences WHERE AssetId = $assetId AND ReferencedByType = 'Scene' AND ReferencedById = $sceneId;";
            command.Parameters.AddWithValue("$assetId", assetId.ToString());
            command.Parameters.AddWithValue("$sceneId", sceneId.ToString());
            return Convert.ToInt32(command.ExecuteScalar());
        }

        private int CountAssetReferencesForScene(CampaignHandle campaign, SceneId sceneId)
        {
            using var connection = new SqliteConnection("Data Source=" + Path.Combine(campaign.RootPath, "campaign.db"));
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM AssetReferences WHERE ReferencedByType = 'Scene' AND ReferencedById = $sceneId;";
            command.Parameters.AddWithValue("$sceneId", sceneId.ToString());
            return Convert.ToInt32(command.ExecuteScalar());
        }

        [Test] // TC-BOARD-016
        public void SetSceneBackground_WithValidAssetId_UpdatesRecordAndIncrementsRevision()
        {
            var repository = new SqliteSceneRepository(Clock);
            SceneRecord scene = repository.CreateScene(_campaign, "Battle Map", NewCommandId(), TestCorrelationId).Value;
            AssetId assetId = RegisterTestAsset(_campaign, repository);

            Result<SceneRecord> result = repository.SetSceneBackground(_campaign, scene.SceneId, assetId, scene.Revision, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.BackgroundAssetId, Is.EqualTo(assetId));
            Assert.That(result.Value.Revision, Is.EqualTo(scene.Revision + 1));
        }

        [Test] // TC-BOARD-017
        public void SetSceneBackground_WithNonExistentAssetId_ReturnsTypedError_SceneUnchanged()
        {
            var repository = new SqliteSceneRepository(Clock);
            SceneRecord scene = repository.CreateScene(_campaign, "Battle Map", NewCommandId(), TestCorrelationId).Value;
            AssetId phantomAsset = AssetId.NewId(Clock.GetUtcNow());

            Result<SceneRecord> result = repository.SetSceneBackground(_campaign, scene.SceneId, phantomAsset, scene.Revision, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceAssetNotFound));
        }

        [Test] // TC-BOARD-018
        public void SetSceneBackground_WithAssetIdRegisteredUnderADifferentCampaign_ReturnsTypedError()
        {
            string otherWorkDir = Path.Combine(Path.GetTempPath(), "ody-s07-105-other-" + Guid.NewGuid().ToString("N"));
            var otherCampaignRepository = new SqliteCampaignRepository(Clock);
            var otherRequest = new CreateCampaignRequest(otherWorkDir, "Other Campaign", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> otherCreated = otherCampaignRepository.Create(otherRequest, NewCommandId(), TestCorrelationId);
            Assert.That(otherCreated.IsSuccess, Is.True);
            CampaignHandle otherCampaign = otherCreated.Value;

            try
            {
                var repository = new SqliteSceneRepository(Clock);
                AssetId foreignAssetId = RegisterTestAsset(otherCampaign, repository);
                SceneRecord scene = repository.CreateScene(_campaign, "Battle Map", NewCommandId(), TestCorrelationId).Value;

                Result<SceneRecord> result = repository.SetSceneBackground(_campaign, scene.SceneId, foreignAssetId, scene.Revision, NewCommandId(), TestCorrelationId);

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceAssetNotFound));
            }
            finally
            {
                try { otherCampaignRepository.Close(otherCampaign, TestCorrelationId); } catch (IOException) { }
                try { if (Directory.Exists(otherWorkDir)) Directory.Delete(otherWorkDir, recursive: true); } catch (IOException) { }
            }
        }

        [Test] // TC-BOARD-019
        public void SetSceneBackground_WithNull_AfterPreviouslySet_ClearsBackground()
        {
            var repository = new SqliteSceneRepository(Clock);
            SceneRecord scene = repository.CreateScene(_campaign, "Battle Map", NewCommandId(), TestCorrelationId).Value;
            AssetId assetId = RegisterTestAsset(_campaign, repository);
            SceneRecord withBackground = repository.SetSceneBackground(_campaign, scene.SceneId, assetId, scene.Revision, NewCommandId(), TestCorrelationId).Value;

            Result<SceneRecord> cleared = repository.SetSceneBackground(_campaign, scene.SceneId, null, withBackground.Revision, NewCommandId(), TestCorrelationId);

            Assert.That(cleared.IsSuccess, Is.True);
            Assert.That(cleared.Value.BackgroundAssetId, Is.Null);
            Assert.That(cleared.Value.Revision, Is.EqualTo(withBackground.Revision + 1));
        }

        [Test] // TC-BOARD-020
        public void SetSceneBackground_WithMismatchedExpectedRevision_ReturnsTypedConflict()
        {
            var repository = new SqliteSceneRepository(Clock);
            SceneRecord scene = repository.CreateScene(_campaign, "Battle Map", NewCommandId(), TestCorrelationId).Value;
            AssetId assetId = RegisterTestAsset(_campaign, repository);

            Result<SceneRecord> result = repository.SetSceneBackground(_campaign, scene.SceneId, assetId, scene.Revision + 1, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceSceneRevisionConflict));
        }

        [Test] // TC-BOARD-021
        public void SetSceneBackground_RetriedWithSameCommandId_ReplaysIdempotently_NoDoubleWrite()
        {
            var repository = new SqliteSceneRepository(Clock);
            SceneRecord scene = repository.CreateScene(_campaign, "Battle Map", NewCommandId(), TestCorrelationId).Value;
            AssetId assetId = RegisterTestAsset(_campaign, repository);
            CommandId commandId = NewCommandId();

            Result<SceneRecord> first = repository.SetSceneBackground(_campaign, scene.SceneId, assetId, scene.Revision, commandId, TestCorrelationId);
            Result<SceneRecord> replay = repository.SetSceneBackground(_campaign, scene.SceneId, assetId, scene.Revision, commandId, TestCorrelationId);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.Revision, Is.EqualTo(first.Value.Revision), "a replayed command must not apply the effect a second time");
            Assert.That(CountAssetReferences(_campaign, assetId, scene.SceneId), Is.EqualTo(1), "replay must not insert a second AssetReferences row");
        }

        [Test] // TC-BOARD-022
        public void SetSceneBackground_OnSuccess_WritesAssetReferenceRow()
        {
            var repository = new SqliteSceneRepository(Clock);
            SceneRecord scene = repository.CreateScene(_campaign, "Battle Map", NewCommandId(), TestCorrelationId).Value;
            AssetId assetId = RegisterTestAsset(_campaign, repository);

            Result<SceneRecord> result = repository.SetSceneBackground(_campaign, scene.SceneId, assetId, scene.Revision, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(CountAssetReferences(_campaign, assetId, scene.SceneId), Is.EqualTo(1));
        }

        [Test] // TC-BOARD-023
        public void SetSceneBackground_ReplacingBackground_ReplacesAssetReferenceRow_NoDuplicate()
        {
            var repository = new SqliteSceneRepository(Clock);
            SceneRecord scene = repository.CreateScene(_campaign, "Battle Map", NewCommandId(), TestCorrelationId).Value;
            AssetId firstAssetId = RegisterTestAsset(_campaign, repository);
            AssetId secondAssetId = RegisterTestAsset(_campaign, repository);

            SceneRecord withFirst = repository.SetSceneBackground(_campaign, scene.SceneId, firstAssetId, scene.Revision, NewCommandId(), TestCorrelationId).Value;
            Result<SceneRecord> withSecond = repository.SetSceneBackground(_campaign, scene.SceneId, secondAssetId, withFirst.Revision, NewCommandId(), TestCorrelationId);

            Assert.That(withSecond.IsSuccess, Is.True);
            Assert.That(withSecond.Value.BackgroundAssetId, Is.EqualTo(secondAssetId));
            Assert.That(CountAssetReferences(_campaign, firstAssetId, scene.SceneId), Is.EqualTo(0), "the old AssetReferences row must not remain");
            Assert.That(CountAssetReferences(_campaign, secondAssetId, scene.SceneId), Is.EqualTo(1));
            Assert.That(CountAssetReferencesForScene(_campaign, scene.SceneId), Is.EqualTo(1), "exactly one current AssetReferences row for this scene, never accumulating");
        }

        // ---- ODY-S07-106: SetTokenPortrait -----------------------------------------

        private int CountTokenAssetReferences(CampaignHandle campaign, TokenId tokenId, AssetId? assetId = null)
        {
            using var connection = new SqliteConnection("Data Source=" + Path.Combine(campaign.RootPath, "campaign.db"));
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM AssetReferences WHERE ReferencedByType = 'Token' AND ReferencedById = $id" + (assetId.HasValue ? " AND AssetId = $asset" : string.Empty) + ";";
            command.Parameters.AddWithValue("$id", tokenId.ToString());
            if (assetId.HasValue) command.Parameters.AddWithValue("$asset", assetId.Value.ToString());
            return Convert.ToInt32(command.ExecuteScalar());
        }

        private TokenRecord CreatePortraitTestToken(SqliteSceneRepository repository, CharacterId? characterId = null)
        {
            SceneId sceneId = repository.CreateScene(_campaign, "Battle Map", NewCommandId(), TestCorrelationId).Value.SceneId;
            Result<TokenRecord> created = repository.CreateToken(_campaign, sceneId, new TokenPosition(1, 1), NewUserId(), NewCommandId(), TestCorrelationId, characterId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        [Test] // TC-BOARD-024
        public void SetTokenPortrait_WithValidAssetId_SetsIt_IncrementsRevision_AndSurvivesMoveToken()
        {
            var repository = new SqliteSceneRepository(Clock);
            TokenRecord token = CreatePortraitTestToken(repository);
            AssetId assetId = RegisterTestAsset(_campaign, repository);

            Result<TokenRecord> result = repository.SetTokenPortrait(_campaign, token.TokenId, assetId, token.Revision, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.PortraitAssetId, Is.EqualTo(assetId));
            Assert.That(result.Value.Revision, Is.EqualTo(token.Revision + 1));

            Result<TokenRecord> moved = repository.MoveToken(_campaign, token.TokenId, new TokenPosition(9, 9), result.Value.Revision, NewCommandId(), TestCorrelationId);
            Assert.That(moved.IsSuccess, Is.True);
            Assert.That(moved.Value.PortraitAssetId, Is.EqualTo(assetId), "MoveToken's returned record must carry the portrait");
            Assert.That(repository.GetToken(_campaign, token.TokenId, TestCorrelationId).Value.PortraitAssetId, Is.EqualTo(assetId), "and the database must still hold it");
        }

        [Test] // TC-BOARD-025
        public void SetTokenPortrait_WithNonExistentAssetId_ReturnsTypedError_TokenUnchanged()
        {
            var repository = new SqliteSceneRepository(Clock);
            TokenRecord token = CreatePortraitTestToken(repository);

            Result<TokenRecord> result = repository.SetTokenPortrait(_campaign, token.TokenId, AssetId.NewId(Clock.GetUtcNow()), token.Revision, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceAssetNotFound));
            TokenRecord reread = repository.GetToken(_campaign, token.TokenId, TestCorrelationId).Value;
            Assert.That(reread.PortraitAssetId, Is.Null);
            Assert.That(reread.Revision, Is.EqualTo(token.Revision));
        }

        [Test] // TC-BOARD-026
        public void SetTokenPortrait_WithAssetIdRegisteredUnderADifferentCampaign_ReturnsTypedError()
        {
            string otherWorkDir = Path.Combine(Path.GetTempPath(), "ody-s07-106-other-" + Guid.NewGuid().ToString("N"));
            var otherCampaignRepository = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> otherCreated = otherCampaignRepository.Create(new CreateCampaignRequest(otherWorkDir, "Other Campaign", "ruleset.core", "1.0.0", "0.1.0"), NewCommandId(), TestCorrelationId);
            Assert.That(otherCreated.IsSuccess, Is.True);

            try
            {
                var repository = new SqliteSceneRepository(Clock);
                AssetId foreignAssetId = RegisterTestAsset(otherCreated.Value, repository);
                TokenRecord token = CreatePortraitTestToken(repository);

                Result<TokenRecord> result = repository.SetTokenPortrait(_campaign, token.TokenId, foreignAssetId, token.Revision, NewCommandId(), TestCorrelationId);

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceAssetNotFound));
            }
            finally
            {
                try { otherCampaignRepository.Close(otherCreated.Value, TestCorrelationId); } catch (IOException) { }
                try { if (Directory.Exists(otherWorkDir)) Directory.Delete(otherWorkDir, recursive: true); } catch (IOException) { }
            }
        }

        [Test] // TC-BOARD-027
        public void SetTokenPortrait_WithNull_AfterPreviouslySet_ClearsPortrait()
        {
            var repository = new SqliteSceneRepository(Clock);
            TokenRecord token = CreatePortraitTestToken(repository);
            TokenRecord withPortrait = repository.SetTokenPortrait(_campaign, token.TokenId, RegisterTestAsset(_campaign, repository), token.Revision, NewCommandId(), TestCorrelationId).Value;

            Result<TokenRecord> cleared = repository.SetTokenPortrait(_campaign, token.TokenId, null, withPortrait.Revision, NewCommandId(), TestCorrelationId);

            Assert.That(cleared.IsSuccess, Is.True);
            Assert.That(cleared.Value.PortraitAssetId, Is.Null);
            Assert.That(repository.GetToken(_campaign, token.TokenId, TestCorrelationId).Value.PortraitAssetId, Is.Null);
            Assert.That(CountTokenAssetReferences(_campaign, token.TokenId), Is.EqualTo(0), "clearing must also remove the AssetReferences row");
        }

        [Test] // TC-BOARD-028
        public void SetTokenPortrait_WithMismatchedExpectedRevision_ReturnsTypedConflict()
        {
            var repository = new SqliteSceneRepository(Clock);
            TokenRecord token = CreatePortraitTestToken(repository);
            AssetId assetId = RegisterTestAsset(_campaign, repository);

            Result<TokenRecord> result = repository.SetTokenPortrait(_campaign, token.TokenId, assetId, token.Revision + 1, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceTokenRevisionConflict));
            Assert.That(repository.GetToken(_campaign, token.TokenId, TestCorrelationId).Value.PortraitAssetId, Is.Null);
        }

        [Test] // TC-BOARD-029
        public void SetTokenPortrait_RetriedWithSameCommandId_ReplaysIdempotently_NoDoubleWrite()
        {
            var repository = new SqliteSceneRepository(Clock);
            TokenRecord token = CreatePortraitTestToken(repository);
            AssetId assetId = RegisterTestAsset(_campaign, repository);
            CommandId commandId = NewCommandId();

            Result<TokenRecord> first = repository.SetTokenPortrait(_campaign, token.TokenId, assetId, token.Revision, commandId, TestCorrelationId);
            Result<TokenRecord> replay = repository.SetTokenPortrait(_campaign, token.TokenId, assetId, token.Revision, commandId, TestCorrelationId);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.Revision, Is.EqualTo(first.Value.Revision), "a replay must not advance the revision a second time");
            Assert.That(CountTokenAssetReferences(_campaign, token.TokenId), Is.EqualTo(1));
        }

        [Test] // TC-BOARD-030
        public void SetTokenPortrait_OnSuccess_WritesAssetReferenceRow()
        {
            var repository = new SqliteSceneRepository(Clock);
            TokenRecord token = CreatePortraitTestToken(repository);
            AssetId assetId = RegisterTestAsset(_campaign, repository);

            Assert.That(repository.SetTokenPortrait(_campaign, token.TokenId, assetId, token.Revision, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);

            Assert.That(CountTokenAssetReferences(_campaign, token.TokenId, assetId), Is.EqualTo(1));
        }

        [Test] // TC-BOARD-031
        public void SetTokenPortrait_ReplacingPortrait_ReplacesAssetReferenceRow_NoDuplicate()
        {
            var repository = new SqliteSceneRepository(Clock);
            TokenRecord token = CreatePortraitTestToken(repository);
            AssetId firstAsset = RegisterTestAsset(_campaign, repository);
            AssetId secondAsset = RegisterTestAsset(_campaign, repository);
            TokenRecord withFirst = repository.SetTokenPortrait(_campaign, token.TokenId, firstAsset, token.Revision, NewCommandId(), TestCorrelationId).Value;

            Result<TokenRecord> withSecond = repository.SetTokenPortrait(_campaign, token.TokenId, secondAsset, withFirst.Revision, NewCommandId(), TestCorrelationId);

            Assert.That(withSecond.IsSuccess, Is.True);
            Assert.That(withSecond.Value.PortraitAssetId, Is.EqualTo(secondAsset));
            Assert.That(CountTokenAssetReferences(_campaign, token.TokenId, firstAsset), Is.EqualTo(0), "the old row must not remain");
            Assert.That(CountTokenAssetReferences(_campaign, token.TokenId), Is.EqualTo(1), "exactly one current row, never accumulating");
        }

        [Test] // TC-BOARD-032
        public void CreateToken_LinkedToCharacterWithPortrait_DoesNotInheritCharacterPortrait()
        {
            // A token's portrait is an independent field: linking a token to a
            // Character that has its own PortraitAssetId must not copy it.
            var sceneRepository = new SqliteSceneRepository(Clock);
            var characterRepository = new SqliteCharacterRepository(Clock);
            CharacterRecord character = characterRepository.CreateCharacter(new CreateCharacterRequest(_campaign, Odyssey.Domain.Character.CharacterKind.PlayerCharacter, "Hero"), NewCommandId(), TestCorrelationId).Value;
            characterRepository.SetCharacterPortrait(_campaign, character.CharacterId, RegisterTestAsset(_campaign, sceneRepository), character.Revisions.PresentationRevision, NewCommandId(), TestCorrelationId);

            TokenRecord token = CreatePortraitTestToken(sceneRepository, character.CharacterId);

            Assert.That(token.CharacterId, Is.EqualTo(character.CharacterId));
            Assert.That(token.PortraitAssetId, Is.Null);
            Assert.That(sceneRepository.GetToken(_campaign, token.TokenId, TestCorrelationId).Value.PortraitAssetId, Is.Null);
        }

        // ---- ODY-S08-101: ReadAssetContent / GetScene ------------------------------

        private AssetManifestEntryRecord RegisterAssetWithContent(CampaignHandle campaign, SqliteSceneRepository repository, byte[] content)
        {
            string sourceFile = Path.Combine(Path.GetTempPath(), "ody-s08-101-source-" + Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(sourceFile, content);
            try
            {
                Result<AssetManifestEntryRecord> registered = repository.RegisterAsset(campaign, sourceFile, NewCommandId(), TestCorrelationId);
                Assert.That(registered.IsSuccess, Is.True, "test fixture asset registration must itself succeed");
                return registered.Value;
            }
            finally
            {
                File.Delete(sourceFile);
            }
        }

        private static string AbsoluteAssetPath(CampaignHandle campaign, AssetManifestEntryRecord entry) =>
            Path.Combine(campaign.RootPath, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar));

        [Test] // TC-BOARD-033
        public void ReadAssetContent_WithRegisteredAssetId_ReturnsTheExactBytes_MatchingTheStoredHash()
        {
            var repository = new SqliteSceneRepository(Clock);
            byte[] content = System.Text.Encoding.UTF8.GetBytes("synthetic image bytes " + Guid.NewGuid().ToString("N"));
            AssetManifestEntryRecord entry = RegisterAssetWithContent(_campaign, repository, content);

            Result<byte[]> result = repository.ReadAssetContent(_campaign, entry.AssetId, TestCorrelationId);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(content));
            using var sha = System.Security.Cryptography.SHA256.Create();
            Assert.That(BitConverter.ToString(sha.ComputeHash(result.Value)).Replace("-", string.Empty).ToLowerInvariant(), Is.EqualTo(entry.Sha256Hash));
        }

        [Test] // TC-BOARD-034
        public void ReadAssetContent_WithNonExistentAssetId_ReturnsTypedAssetNotFound()
        {
            var repository = new SqliteSceneRepository(Clock);

            Result<byte[]> result = repository.ReadAssetContent(_campaign, AssetId.NewId(Clock.GetUtcNow()), TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceAssetNotFound));
        }

        [Test] // TC-BOARD-035
        public void ReadAssetContent_WithAssetIdRegisteredUnderADifferentCampaign_ReturnsTypedAssetNotFound()
        {
            string otherWorkDir = Path.Combine(Path.GetTempPath(), "ody-s08-101-other-" + Guid.NewGuid().ToString("N"));
            var otherCampaignRepository = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> otherCreated = otherCampaignRepository.Create(new CreateCampaignRequest(otherWorkDir, "Other Campaign", "ruleset.core", "1.0.0", "0.1.0"), NewCommandId(), TestCorrelationId);
            Assert.That(otherCreated.IsSuccess, Is.True);

            try
            {
                var repository = new SqliteSceneRepository(Clock);
                AssetManifestEntryRecord foreign = RegisterAssetWithContent(otherCreated.Value, repository, System.Text.Encoding.UTF8.GetBytes("foreign campaign bytes"));

                Assert.That(repository.ReadAssetContent(otherCreated.Value, foreign.AssetId, TestCorrelationId).IsSuccess, Is.True, "sanity: readable in its own campaign");
                Result<byte[]> result = repository.ReadAssetContent(_campaign, foreign.AssetId, TestCorrelationId);

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceAssetNotFound));
            }
            finally
            {
                try { otherCampaignRepository.Close(otherCreated.Value, TestCorrelationId); } catch (IOException) { }
                try { if (Directory.Exists(otherWorkDir)) Directory.Delete(otherWorkDir, recursive: true); } catch (IOException) { }
            }
        }

        [Test] // TC-BOARD-036
        public void ReadAssetContent_WhenManifestRowExistsButFileIsMissingOnDisk_ReturnsTypedFileMissing()
        {
            var repository = new SqliteSceneRepository(Clock);
            AssetManifestEntryRecord entry = RegisterAssetWithContent(_campaign, repository, System.Text.Encoding.UTF8.GetBytes("about to vanish " + Guid.NewGuid().ToString("N")));
            File.Delete(AbsoluteAssetPath(_campaign, entry));

            Result<byte[]> result = repository.ReadAssetContent(_campaign, entry.AssetId, TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceAssetFileMissing));
        }

        [Test] // TC-BOARD-037
        public void ReadAssetContent_WhenBytesOnDiskNoLongerMatchTheStoredHash_ReturnsTypedIntegrityFailure()
        {
            var repository = new SqliteSceneRepository(Clock);
            AssetManifestEntryRecord entry = RegisterAssetWithContent(_campaign, repository, System.Text.Encoding.UTF8.GetBytes("original bytes " + Guid.NewGuid().ToString("N")));
            File.WriteAllBytes(AbsoluteAssetPath(_campaign, entry), System.Text.Encoding.UTF8.GetBytes("tampered bytes"));

            Result<byte[]> result = repository.ReadAssetContent(_campaign, entry.AssetId, TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceAssetIntegrityFailed));
        }

        [Test] // TC-BOARD-038
        public void ReadAssetContent_WhenManifestPathEscapesTheCampaignAssetDirectory_IsRejected_EvenIfTheHashWouldMatch()
        {
            var repository = new SqliteSceneRepository(Clock);
            byte[] content = System.Text.Encoding.UTF8.GetBytes("outside bytes " + Guid.NewGuid().ToString("N"));
            AssetManifestEntryRecord entry = RegisterAssetWithContent(_campaign, repository, content);

            // A byte-identical copy OUTSIDE Assets/Objects: the stored hash would match it,
            // so only the path-confinement check can reject this read.
            string outsideFile = Path.Combine(_workDir, "outside-" + Guid.NewGuid().ToString("N") + ".bin");
            File.WriteAllBytes(outsideFile, content);
            using (var connection = new SqliteConnection("Data Source=" + Path.Combine(_campaign.RootPath, "campaign.db")))
            {
                connection.Open();
                using var update = connection.CreateCommand();
                update.CommandText = "UPDATE AssetManifestEntries SET RelativePath = $path WHERE AssetId = $id;";
                update.Parameters.AddWithValue("$path", "Assets/Objects/../../" + Path.GetFileName(outsideFile));
                update.Parameters.AddWithValue("$id", entry.AssetId.ToString());
                update.ExecuteNonQuery();
            }

            Result<byte[]> result = repository.ReadAssetContent(_campaign, entry.AssetId, TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceAssetIntegrityFailed));
        }

        [Test] // TC-BOARD-039
        public void GetScene_ReturnsTheStoredScene_WithAndWithoutBackground_AndTypedNotFoundForUnknownScene()
        {
            var repository = new SqliteSceneRepository(Clock);
            SceneRecord scene = repository.CreateScene(_campaign, "Battle Map", NewCommandId(), TestCorrelationId).Value;

            Result<SceneRecord> withoutBackground = repository.GetScene(_campaign, scene.SceneId, TestCorrelationId);
            Assert.That(withoutBackground.IsSuccess, Is.True);
            Assert.That(withoutBackground.Value.BackgroundAssetId, Is.Null);
            Assert.That(withoutBackground.Value.Name, Is.EqualTo("Battle Map"));

            AssetId assetId = RegisterTestAsset(_campaign, repository);
            Assert.That(repository.SetSceneBackground(_campaign, scene.SceneId, assetId, scene.Revision, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);

            Result<SceneRecord> withBackground = repository.GetScene(_campaign, scene.SceneId, TestCorrelationId);
            Assert.That(withBackground.Value.BackgroundAssetId, Is.EqualTo(assetId));
            Assert.That(withBackground.Value.Revision, Is.EqualTo(scene.Revision + 1));

            Result<SceneRecord> unknown = repository.GetScene(_campaign, SceneId.NewId(Clock.GetUtcNow()), TestCorrelationId);
            Assert.That(unknown.IsFailure, Is.True);
            Assert.That(unknown.Error.Code, Is.EqualTo(ErrorCodes.PersistenceSceneNotFound));
        }
    }
}
