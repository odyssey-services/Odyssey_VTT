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
    /// <summary>
    /// ODY-S01-013: a single, automated, reproducible end-to-end check of
    /// roadmap section 10.5's nine-step SLICE-01 scenario, run literally in
    /// order in one test method (not nine independent tests) so the guarantee
    /// under test is the full sequence, not each step in isolation --
    /// ODY-S01-007/008/009/010/011/012 already have their own module-level
    /// tests for isolated behavior; this test does not repeat those cases.
    ///
    /// No new production code exists to support this test -- every step below
    /// calls an already-merged ODY-S01-007..012 API exactly as documented.
    /// Step 2 ("import one test map") uses ISceneRepository.RegisterAsset:
    /// SceneRepositoryContracts.cs's own XML doc already names RegisterAsset as
    /// the API for "roadmap section 10.5 steps 2-5 (import one test map...)" --
    /// IExportRepository.ImportCampaign (ODY-S01-012) imports a whole .odcamp
    /// archive into a new campaign, a different operation from importing one
    /// map asset into an existing, already-open campaign.
    ///
    /// The backup for step 9 is created right after step 5 (move tokens) and
    /// before step 6 (close) -- this captures exactly the moved-token state
    /// that step 8 independently verifies after reopening, so step 9's restore
    /// assertion can compare the restored copy directly against the same
    /// state step 8 already confirmed, not an earlier, less meaningful point.
    /// </summary>
    public sealed class VerticalSliceIntegrationTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private string _workDir = null!;
        private string _restoreParentDir = null!;
        private string _testMapFilePath = null!;

        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        [SetUp]
        public void SetUp()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "ody-s01-013-" + Guid.NewGuid().ToString("N"));
            _restoreParentDir = Path.Combine(Path.GetTempPath(), "ody-s01-013-restore-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_restoreParentDir);

            // A minimal, self-contained fixture file standing in for "one test
            // map" -- not a real image asset, but real bytes on disk that
            // RegisterAsset copies, hashes, and records exactly as it would any
            // other imported file. No external resource is required.
            _testMapFilePath = Path.Combine(Path.GetTempPath(), "ody-s01-013-test-map-" + Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(_testMapFilePath, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03 });
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true); } catch (IOException) { }
            try { if (Directory.Exists(_restoreParentDir)) Directory.Delete(_restoreParentDir, recursive: true); } catch (IOException) { }
            try { if (File.Exists(_testMapFilePath)) File.Delete(_testMapFilePath); } catch (IOException) { }
        }

        [Test]
        public void NineStepSlice_CreateImportSceneTokensMoveCloseReopenVerifyRestore_AllStepsSucceed()
        {
            IWallClock clock = new SystemWallClock();
            var campaignRepository = new SqliteCampaignRepository(clock);
            var sceneRepository = new SqliteSceneRepository(clock);
            var backupRepository = new SqliteBackupRepository(clock);

            // Step 1: create campaign.
            var createRequest = new CreateCampaignRequest(_workDir, "SLICE-01 Vertical Slice Campaign", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = campaignRepository.Create(createRequest, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True, "step 1 (create campaign) must succeed");
            CampaignHandle campaign = created.Value;

            // Step 2: import one test map (ISceneRepository.RegisterAsset -- see
            // class remarks for why this API, not IExportRepository.ImportCampaign).
            Result<AssetManifestEntryRecord> registeredMap = sceneRepository.RegisterAsset(campaign, _testMapFilePath, NewCommandId(), TestCorrelationId);
            Assert.That(registeredMap.IsSuccess, Is.True, "step 2 (import one test map) must succeed");
            Assert.That(registeredMap.Value.RelativePath, Does.StartWith("Assets/Objects/"));
            Assert.That(registeredMap.Value.RelativePath, Does.Not.Contain(_testMapFilePath), "only the relative in-campaign path may be recorded, never the absolute import source path (ADR-011 section 4.2)");

            // Step 3: create a scene.
            Result<SceneRecord> sceneResult = sceneRepository.CreateScene(campaign, "Vertical Slice Scene", NewCommandId(), TestCorrelationId);
            Assert.That(sceneResult.IsSuccess, Is.True, "step 3 (create a scene) must succeed");
            SceneId sceneId = sceneResult.Value.SceneId;

            // Step 4: place two tokens.
            Result<TokenRecord> tokenAResult = sceneRepository.CreateToken(campaign, sceneId, new TokenPosition(1, 1), NewUserId(), NewCommandId(), TestCorrelationId);
            Result<TokenRecord> tokenBResult = sceneRepository.CreateToken(campaign, sceneId, new TokenPosition(2, 2), NewUserId(), NewCommandId(), TestCorrelationId);
            Assert.That(tokenAResult.IsSuccess, Is.True, "step 4 (place token A) must succeed");
            Assert.That(tokenBResult.IsSuccess, Is.True, "step 4 (place token B) must succeed");
            TokenId tokenAId = tokenAResult.Value.TokenId;
            TokenId tokenBId = tokenBResult.Value.TokenId;
            Assert.That(tokenAId, Is.Not.EqualTo(tokenBId), "the two placed tokens must be distinct");

            // Step 5: move them -- both positions must actually change, and
            // must not collide with each other.
            var tokenAMovedPosition = new TokenPosition(10, 20);
            var tokenBMovedPosition = new TokenPosition(30, 40);
            Result<TokenRecord> tokenAMoved = sceneRepository.MoveToken(campaign, tokenAId, tokenAMovedPosition, tokenAResult.Value.Revision, NewCommandId(), TestCorrelationId);
            Result<TokenRecord> tokenBMoved = sceneRepository.MoveToken(campaign, tokenBId, tokenBMovedPosition, tokenBResult.Value.Revision, NewCommandId(), TestCorrelationId);
            Assert.That(tokenAMoved.IsSuccess, Is.True, "step 5 (move token A) must succeed");
            Assert.That(tokenBMoved.IsSuccess, Is.True, "step 5 (move token B) must succeed");
            Assert.That(tokenAMoved.Value.Position, Is.Not.EqualTo(new TokenPosition(1, 1)), "token A's position must have actually changed");
            Assert.That(tokenBMoved.Value.Position, Is.Not.EqualTo(new TokenPosition(2, 2)), "token B's position must have actually changed");
            Assert.That(tokenAMoved.Value.Position, Is.Not.EqualTo(tokenBMoved.Value.Position), "the two moved positions must not collide");

            // Backup checkpoint for step 9, taken here (post-move, pre-close) --
            // see class remarks for why this point in the sequence.
            Result<BackupRecord> backupResult = backupRepository.CreateBackup(campaign, "vertical-slice-integration-checkpoint", TestCorrelationId);
            Assert.That(backupResult.IsSuccess, Is.True, "backup checkpoint for step 9 must succeed");
            BackupId backupId = backupResult.Value.BackupId;

            // Step 6: close application.
            Result closeResult = campaignRepository.Close(campaign, TestCorrelationId);
            Assert.That(closeResult.IsSuccess, Is.True, "step 6 (close application) must succeed");

            // Step 7: reopen campaign -- a fresh ICampaignRepository instance,
            // not the same one Close() was just called on, so nothing in-memory
            // from the original session can leak through and mask a real gap.
            var reopenCampaignRepository = new SqliteCampaignRepository(clock);
            Result<CampaignHandle> reopened = reopenCampaignRepository.Open(_workDir, TestCorrelationId);
            Assert.That(reopened.IsSuccess, Is.True, "step 7 (reopen campaign) must succeed");
            CampaignHandle reopenedCampaign = reopened.Value;
            Assert.That(reopenedCampaign.CampaignId, Is.EqualTo(campaign.CampaignId));

            // Step 8: verify saved state -- scene, both tokens (at their moved
            // positions), and the registered map asset all survive the
            // close/reopen cycle exactly as they were before closing.
            var reopenedSceneRepository = new SqliteSceneRepository(clock);
            Result<IReadOnlyList<TokenRecord>> tokensAfterReopen = reopenedSceneRepository.ListTokens(reopenedCampaign, sceneId, TestCorrelationId);
            Assert.That(tokensAfterReopen.IsSuccess, Is.True, "step 8 (verify saved state -- list tokens) must succeed");
            Assert.That(tokensAfterReopen.Value.Count, Is.EqualTo(2), "both placed tokens must survive the close/reopen cycle");

            TokenRecord persistedTokenA = FindToken(tokensAfterReopen.Value, tokenAId);
            TokenRecord persistedTokenB = FindToken(tokensAfterReopen.Value, tokenBId);
            Assert.That(persistedTokenA.Position, Is.EqualTo(tokenAMovedPosition), "step 8: token A's post-move position must survive close/reopen");
            Assert.That(persistedTokenB.Position, Is.EqualTo(tokenBMovedPosition), "step 8: token B's post-move position must survive close/reopen");

            Result<AssetManifestEntryRecord> assetStillPresent = VerifyAssetRegistryContains(reopenedCampaign.RootPath, registeredMap.Value.AssetId);
            Assert.That(assetStillPresent.IsSuccess, Is.True, "step 8: the imported test map's asset record must survive close/reopen");
            Assert.That(assetStillPresent.Value.Sha256Hash, Is.EqualTo(registeredMap.Value.Sha256Hash), "step 8: the asset's recorded hash must be unchanged after close/reopen");

            reopenCampaignRepository.Close(reopenedCampaign, TestCorrelationId);

            // Step 9: restore state from backup -- into a brand-new, separate
            // copy (never overwriting the working campaign), then confirm the
            // restored copy holds exactly the state the backup checkpoint (and
            // step 8's verification) captured.
            var restoreBackupRepository = new SqliteBackupRepository(clock);
            Result<string> restoredRootPath = restoreBackupRepository.RestoreBackup(_workDir, backupId, _restoreParentDir, TestCorrelationId);
            Assert.That(restoredRootPath.IsSuccess, Is.True, "step 9 (restore from backup) must succeed");
            Assert.That(restoredRootPath.Value, Is.Not.EqualTo(_workDir), "restore must never write into the original working campaign directory");

            var restoredCampaignRepository = new SqliteCampaignRepository(clock);
            Result<CampaignHandle> restoredHandle = restoredCampaignRepository.Open(restoredRootPath.Value, TestCorrelationId);
            Assert.That(restoredHandle.IsSuccess, Is.True, "step 9: the restored copy must open successfully");

            var restoredSceneRepository = new SqliteSceneRepository(clock);
            Result<IReadOnlyList<TokenRecord>> tokensInRestoredCopy = restoredSceneRepository.ListTokens(restoredHandle.Value, sceneId, TestCorrelationId);
            Assert.That(tokensInRestoredCopy.IsSuccess, Is.True, "step 9: listing tokens in the restored copy must succeed");
            Assert.That(tokensInRestoredCopy.Value.Count, Is.EqualTo(2), "step 9: both tokens must be present in the restored copy");

            TokenRecord restoredTokenA = FindToken(tokensInRestoredCopy.Value, tokenAId);
            TokenRecord restoredTokenB = FindToken(tokensInRestoredCopy.Value, tokenBId);
            Assert.That(restoredTokenA.Position, Is.EqualTo(tokenAMovedPosition), "step 9: token A's position in the restored copy must match the backup checkpoint");
            Assert.That(restoredTokenB.Position, Is.EqualTo(tokenBMovedPosition), "step 9: token B's position in the restored copy must match the backup checkpoint");

            restoredCampaignRepository.Close(restoredHandle.Value, TestCorrelationId);
        }

        private static TokenRecord FindToken(IReadOnlyList<TokenRecord> tokens, TokenId id)
        {
            foreach (TokenRecord token in tokens)
            {
                if (token.TokenId == id) return token;
            }

            throw new InvalidOperationException("Token not found in list: " + id);
        }

        /// <summary>
        /// ISceneRepository has no direct "get asset by id" method, so this
        /// reads the AssetManifestEntries row directly -- a read-only query
        /// against an already-open campaign's own database, not new production
        /// logic (this method lives in the test file, not in Packages/).
        /// </summary>
        private static Result<AssetManifestEntryRecord> VerifyAssetRegistryContains(string campaignRootPath, AssetId assetId)
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + Path.Combine(campaignRootPath, "campaign.db") + ";Mode=ReadOnly;Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT AssetId, RelativePath, Hash, SizeBytes FROM AssetManifestEntries WHERE AssetId = $id LIMIT 1;";
            command.Parameters.AddWithValue("$id", assetId.ToString());
            using Microsoft.Data.Sqlite.SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return Result<AssetManifestEntryRecord>.Failure(PersistenceFailures.CampaignIoFailed(TestCorrelationId));
            }

            var record = new AssetManifestEntryRecord(AssetId.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2), reader.GetInt64(3));
            return Result<AssetManifestEntryRecord>.Success(record);
        }
    }
}
