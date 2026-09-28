using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            var request = new CreateCampaignRequest(_workDir, "Scene Test Campaign", "ruleset.core", "1.0.0", "0.1.0", global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost());
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

        // ---- Content-addressable storage: same-original-name collision fix --------

        private string WriteSourceFile(string originalFileName, byte[] content)
        {
            string sourceFile = Path.Combine(_workDir, Guid.NewGuid().ToString("N") + "-" + originalFileName);
            File.WriteAllBytes(sourceFile, content);
            return sourceFile;
        }

        [Test] // TC-BOARD-048
        public void RegisterAsset_TwoDifferentFilesWithTheSameOriginalName_GetDifferentAssetIds_AndBothReadBackTheirOwnDistinctContent()
        {
            var repository = new SqliteSceneRepository(Clock);
            byte[] contentA = System.Text.Encoding.UTF8.GetBytes("map content A " + Guid.NewGuid().ToString("N"));
            byte[] contentB = System.Text.Encoding.UTF8.GetBytes("map content B " + Guid.NewGuid().ToString("N"));
            string sourceA = WriteSourceFile("map.png", contentA);
            string sourceB = WriteSourceFile("map.png", contentB);

            try
            {
                Result<AssetManifestEntryRecord> registeredA = repository.RegisterAsset(_campaign, sourceA, NewCommandId(), TestCorrelationId);
                Result<AssetManifestEntryRecord> registeredB = repository.RegisterAsset(_campaign, sourceB, NewCommandId(), TestCorrelationId);

                Assert.That(registeredA.IsSuccess, Is.True);
                Assert.That(registeredB.IsSuccess, Is.True);
                Assert.That(registeredA.Value.AssetId, Is.Not.EqualTo(registeredB.Value.AssetId));
                Assert.That(registeredA.Value.RelativePath, Is.Not.EqualTo(registeredB.Value.RelativePath), "different content under the same original name must not land on the same destination path");
                Assert.That(registeredA.Value.Sha256Hash, Is.Not.EqualTo(registeredB.Value.Sha256Hash));

                Result<byte[]> readA = repository.ReadAssetContent(_campaign, registeredA.Value.AssetId, TestCorrelationId);
                Result<byte[]> readB = repository.ReadAssetContent(_campaign, registeredB.Value.AssetId, TestCorrelationId);

                Assert.That(readA.IsSuccess, Is.True);
                Assert.That(readB.IsSuccess, Is.True);
                Assert.That(readA.Value, Is.EqualTo(contentA), "the first map's AssetId must read back its own bytes, not the second map's");
                Assert.That(readB.Value, Is.EqualTo(contentB), "the second map's AssetId must read back its own bytes, not the first map's");
                Assert.That(readA.Value, Is.Not.EqualTo(readB.Value));
            }
            finally
            {
                File.Delete(sourceA);
                File.Delete(sourceB);
            }
        }

        [Test] // TC-BOARD-049
        public void RegisterAsset_ByteIdenticalContentRegisteredTwice_DoesNotCreateASecondCopyOnDisk()
        {
            var repository = new SqliteSceneRepository(Clock);
            byte[] content = System.Text.Encoding.UTF8.GetBytes("identical map content " + Guid.NewGuid().ToString("N"));
            string sourceFirst = WriteSourceFile("first-name.png", content);
            string sourceSecond = WriteSourceFile("second-name.png", content);

            try
            {
                Result<AssetManifestEntryRecord> first = repository.RegisterAsset(_campaign, sourceFirst, NewCommandId(), TestCorrelationId);
                Assert.That(first.IsSuccess, Is.True);

                string objectsDirectory = Path.Combine(_campaign.RootPath, "Assets", "Objects");
                int fileCountBefore = Directory.GetFiles(objectsDirectory).Length;

                Result<AssetManifestEntryRecord> second = repository.RegisterAsset(_campaign, sourceSecond, NewCommandId(), TestCorrelationId);

                Assert.That(second.IsSuccess, Is.True);
                Assert.That(second.Value.RelativePath, Is.EqualTo(first.Value.RelativePath), "byte-identical content deduplicates to the same content-addressed path regardless of the original file name");
                Assert.That(second.Value.Sha256Hash, Is.EqualTo(first.Value.Sha256Hash));
                Assert.That(second.Value.AssetId, Is.Not.EqualTo(first.Value.AssetId), "each registration still gets its own manifest row/AssetId");

                int fileCountAfter = Directory.GetFiles(objectsDirectory).Length;
                Assert.That(fileCountAfter, Is.EqualTo(fileCountBefore), "no second copy of the same content is written to disk");

                Result<byte[]> readSecond = repository.ReadAssetContent(_campaign, second.Value.AssetId, TestCorrelationId);
                Assert.That(readSecond.IsSuccess, Is.True);
                Assert.That(readSecond.Value, Is.EqualTo(content));
            }
            finally
            {
                File.Delete(sourceFirst);
                File.Delete(sourceSecond);
            }
        }

        [Test] // TC-BOARD-050
        public void RegisterAsset_DestinationFileName_IsContentAddressed_NotTheOriginalFileName()
        {
            var repository = new SqliteSceneRepository(Clock);
            byte[] content = System.Text.Encoding.UTF8.GetBytes("addressed by hash " + Guid.NewGuid().ToString("N"));
            string sourceFile = WriteSourceFile("original-name.png", content);

            try
            {
                Result<AssetManifestEntryRecord> registered = repository.RegisterAsset(_campaign, sourceFile, NewCommandId(), TestCorrelationId);

                Assert.That(registered.IsSuccess, Is.True);
                string expectedRelativePath = "Assets/Objects/" + registered.Value.Sha256Hash + ".png";
                Assert.That(registered.Value.RelativePath, Is.EqualTo(expectedRelativePath));
                Assert.That(registered.Value.RelativePath, Does.Not.Contain("original-name"), "the on-disk name must not carry the caller's original file name");
            }
            finally
            {
                File.Delete(sourceFile);
            }
        }

        [Test] // TC-BOARD-065
        public void ListAssets_ReturnsEveryAssetRegisteredForThisCampaign_OldestFirst_AndIsCampaignScoped()
        {
            var repository = new SqliteSceneRepository(Clock);
            string sourceA = WriteSourceFile("a.png", System.Text.Encoding.UTF8.GetBytes("asset A " + Guid.NewGuid().ToString("N")));
            string sourceB = WriteSourceFile("b.png", System.Text.Encoding.UTF8.GetBytes("asset B " + Guid.NewGuid().ToString("N")));

            try
            {
                Result<AssetManifestEntryRecord> registeredA = repository.RegisterAsset(_campaign, sourceA, NewCommandId(), TestCorrelationId);
                Result<AssetManifestEntryRecord> registeredB = repository.RegisterAsset(_campaign, sourceB, NewCommandId(), TestCorrelationId);
                Assert.That(registeredA.IsSuccess, Is.True);
                Assert.That(registeredB.IsSuccess, Is.True);

                Result<IReadOnlyList<AssetManifestEntryRecord>> listed = repository.ListAssets(_campaign, TestCorrelationId);

                Assert.That(listed.IsSuccess, Is.True);
                Assert.That(listed.Value.Select(e => e.AssetId), Is.EqualTo(new[] { registeredA.Value.AssetId, registeredB.Value.AssetId }), "oldest-first, i.e. registration order");
                Assert.That(listed.Value.Select(e => e.RelativePath), Is.EquivalentTo(new[] { registeredA.Value.RelativePath, registeredB.Value.RelativePath }));

                string otherWorkDir = Path.Combine(Path.GetTempPath(), "ody-s08-103-other-" + Guid.NewGuid().ToString("N"));
                var otherCampaignRepository = new SqliteCampaignRepository(Clock);
                Result<CampaignHandle> otherCreated = otherCampaignRepository.Create(new CreateCampaignRequest(otherWorkDir, "Other Campaign", "ruleset.core", "1.0.0", "0.1.0", global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost()), NewCommandId(), TestCorrelationId);
                Assert.That(otherCreated.IsSuccess, Is.True);
                try
                {
                    Result<IReadOnlyList<AssetManifestEntryRecord>> otherListed = repository.ListAssets(otherCreated.Value, TestCorrelationId);
                    Assert.That(otherListed.IsSuccess, Is.True);
                    Assert.That(otherListed.Value, Is.Empty, "each campaign is a separate database file -- a fresh campaign lists none of the first one's assets");
                }
                finally
                {
                    otherCampaignRepository.Close(otherCreated.Value, TestCorrelationId);
                    try { if (Directory.Exists(otherWorkDir)) Directory.Delete(otherWorkDir, recursive: true); }
                    catch (IOException) { /* best-effort cleanup only, matching this file's other fixtures */ }
                }
            }
            finally
            {
                File.Delete(sourceA);
                File.Delete(sourceB);
            }
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
            var otherRequest = new CreateCampaignRequest(otherWorkDir, "Other Campaign", "ruleset.core", "1.0.0", "0.1.0", global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost());
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
            Result<CampaignHandle> otherCreated = otherCampaignRepository.Create(new CreateCampaignRequest(otherWorkDir, "Other Campaign", "ruleset.core", "1.0.0", "0.1.0", global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost()), NewCommandId(), TestCorrelationId);
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
            Result<CampaignHandle> otherCreated = otherCampaignRepository.Create(new CreateCampaignRequest(otherWorkDir, "Other Campaign", "ruleset.core", "1.0.0", "0.1.0", global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost()), NewCommandId(), TestCorrelationId);
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

        // ---- ODY-S08-105: token z-order and scale ----------------------------------

        [Test] // TC-BOARD-078
        public void CreateToken_AssignsZOrderAsSceneMaxPlusOne_AndDefaultScaleOne_PerScene()
        {
            var repository = new SqliteSceneRepository(Clock);
            SceneId sceneA = repository.CreateScene(_campaign, "A", NewCommandId(), TestCorrelationId).Value.SceneId;
            SceneId sceneB = repository.CreateScene(_campaign, "B", NewCommandId(), TestCorrelationId).Value.SceneId;

            TokenRecord a1 = repository.CreateToken(_campaign, sceneA, new TokenPosition(1, 1), NewUserId(), NewCommandId(), TestCorrelationId).Value;
            TokenRecord a2 = repository.CreateToken(_campaign, sceneA, new TokenPosition(2, 2), NewUserId(), NewCommandId(), TestCorrelationId).Value;
            TokenRecord b1 = repository.CreateToken(_campaign, sceneB, new TokenPosition(1, 1), NewUserId(), NewCommandId(), TestCorrelationId).Value;

            Assert.That(a1.ZOrder, Is.EqualTo(1));
            Assert.That(a2.ZOrder, Is.EqualTo(2));
            Assert.That(b1.ZOrder, Is.EqualTo(1), "z-order is per scene");
            Assert.That(a1.Scale, Is.EqualTo(1.0));
            Assert.That(repository.GetToken(_campaign, a2.TokenId, TestCorrelationId).Value.ZOrder, Is.EqualTo(2), "the stored value matches the returned record");

            long bumped = repository.SetTokenZOrder(_campaign, a1.TokenId, 3, a1.Revision, NewCommandId(), TestCorrelationId).Value.ZOrder;
            TokenRecord a3 = repository.CreateToken(_campaign, sceneA, new TokenPosition(3, 3), NewUserId(), NewCommandId(), TestCorrelationId).Value;
            Assert.That(bumped, Is.EqualTo(3));
            Assert.That(a3.ZOrder, Is.EqualTo(4), "a new token goes above the current maximum, wherever it came from");
        }

        [Test] // TC-BOARD-079
        public void SetTokenZOrder_AndSetTokenScale_AreRevisionGated_Idempotent_AndSurviveOtherWrites()
        {
            var repository = new SqliteSceneRepository(Clock);
            TokenRecord token = CreatePortraitTestToken(repository);

            CommandId zCommand = NewCommandId();
            Result<TokenRecord> z = repository.SetTokenZOrder(_campaign, token.TokenId, 7, token.Revision, zCommand, TestCorrelationId);
            Assert.That(z.IsSuccess, Is.True);
            Assert.That(z.Value.ZOrder, Is.EqualTo(7));
            Assert.That(z.Value.Revision, Is.EqualTo(token.Revision + 1));
            Assert.That(repository.SetTokenZOrder(_campaign, token.TokenId, 7, token.Revision, zCommand, TestCorrelationId).Value.Revision, Is.EqualTo(z.Value.Revision), "same command id replays, no second bump");

            Result<TokenRecord> stale = repository.SetTokenScale(_campaign, token.TokenId, 2.0, token.Revision, NewCommandId(), TestCorrelationId);
            Assert.That(stale.IsFailure, Is.True);
            Assert.That(stale.Error.Code, Is.EqualTo(ErrorCodes.PersistenceTokenRevisionConflict));

            Result<TokenRecord> s = repository.SetTokenScale(_campaign, token.TokenId, 2.0, z.Value.Revision, NewCommandId(), TestCorrelationId);
            Assert.That(s.IsSuccess, Is.True);
            Assert.That(s.Value.Scale, Is.EqualTo(2.0));
            Assert.That(s.Value.ZOrder, Is.EqualTo(7), "scale write keeps z-order");

            AssetId assetId = RegisterTestAsset(_campaign, repository);
            TokenRecord withPortrait = repository.SetTokenPortrait(_campaign, token.TokenId, assetId, s.Value.Revision, NewCommandId(), TestCorrelationId).Value;
            TokenRecord moved = repository.MoveToken(_campaign, token.TokenId, new TokenPosition(5, 5), withPortrait.Revision, NewCommandId(), TestCorrelationId).Value;
            Assert.That(withPortrait.ZOrder, Is.EqualTo(7));
            Assert.That(withPortrait.Scale, Is.EqualTo(2.0));
            Assert.That(moved.ZOrder, Is.EqualTo(7));
            Assert.That(moved.Scale, Is.EqualTo(2.0));
            TokenRecord stored = repository.GetToken(_campaign, token.TokenId, TestCorrelationId).Value;
            Assert.That(stored.ZOrder, Is.EqualTo(7));
            Assert.That(stored.Scale, Is.EqualTo(2.0));

            Assert.That(repository.SetTokenScale(_campaign, TokenId.NewId(Clock.GetUtcNow()), 1.0, 1, NewCommandId(), TestCorrelationId).Error.Code, Is.EqualTo(ErrorCodes.PersistenceTokenNotFound));
        }

        [Test] // TC-BOARD-080
        public void SetTokenScale_RejectsOutOfBoundsAndNonFiniteValues_AndAcceptsTheBounds()
        {
            var repository = new SqliteSceneRepository(Clock);
            TokenRecord token = CreatePortraitTestToken(repository);

            Assert.Throws<ArgumentOutOfRangeException>((Action)(() => repository.SetTokenScale(_campaign, token.TokenId, 0.0, token.Revision, NewCommandId(), TestCorrelationId)));
            Assert.Throws<ArgumentOutOfRangeException>((Action)(() => repository.SetTokenScale(_campaign, token.TokenId, -1.0, token.Revision, NewCommandId(), TestCorrelationId)));
            Assert.Throws<ArgumentOutOfRangeException>((Action)(() => repository.SetTokenScale(_campaign, token.TokenId, TokenRecord.MinScale - 0.01, token.Revision, NewCommandId(), TestCorrelationId)));
            Assert.Throws<ArgumentOutOfRangeException>((Action)(() => repository.SetTokenScale(_campaign, token.TokenId, TokenRecord.MaxScale + 0.01, token.Revision, NewCommandId(), TestCorrelationId)));
            Assert.Throws<ArgumentOutOfRangeException>((Action)(() => repository.SetTokenScale(_campaign, token.TokenId, double.NaN, token.Revision, NewCommandId(), TestCorrelationId)));
            Assert.Throws<ArgumentOutOfRangeException>((Action)(() => repository.SetTokenScale(_campaign, token.TokenId, double.PositiveInfinity, token.Revision, NewCommandId(), TestCorrelationId)));
            Assert.That(repository.GetToken(_campaign, token.TokenId, TestCorrelationId).Value.Revision, Is.EqualTo(token.Revision), "rejected values write nothing");

            TokenRecord atMin = repository.SetTokenScale(_campaign, token.TokenId, TokenRecord.MinScale, token.Revision, NewCommandId(), TestCorrelationId).Value;
            TokenRecord atMax = repository.SetTokenScale(_campaign, token.TokenId, TokenRecord.MaxScale, atMin.Revision, NewCommandId(), TestCorrelationId).Value;
            Assert.That(atMin.Scale, Is.EqualTo(TokenRecord.MinScale));
            Assert.That(atMax.Scale, Is.EqualTo(TokenRecord.MaxScale));
        }
    }
}
