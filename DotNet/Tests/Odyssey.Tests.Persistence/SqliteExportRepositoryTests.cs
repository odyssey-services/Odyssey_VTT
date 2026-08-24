using System;
using System.IO;
using System.IO.Compression;
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
    /// <summary>
    /// ODY-S01-012: `.odcamp` export/import baseline (ADR-011 section 3.2,
    /// 05_Persistence section 27). Export reuses SqliteSnapshotCopy (ODY-S01-011),
    /// not a second database-copy path -- TC-PERSIST-024 confirms the exported
    /// database passes the same PRAGMA quick_check a backup does.
    /// </summary>
    public sealed class SqliteExportRepositoryTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private string _workDir = null!;
        private string _odcampParentDir = null!;

        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));

        [SetUp]
        public void SetUp()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "ody-s01-012-" + Guid.NewGuid().ToString("N"));
            _odcampParentDir = Path.Combine(Path.GetTempPath(), "ody-s01-012-odcamp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_odcampParentDir);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true); } catch (IOException) { }
            try { if (Directory.Exists(_odcampParentDir)) Directory.Delete(_odcampParentDir, recursive: true); } catch (IOException) { }
        }

        private static CampaignHandle CreateCampaign(SqliteCampaignRepository repository, string rootPath, IWallClock clock)
        {
            var request = new CreateCampaignRequest(rootPath, "Export Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = repository.Create(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        [Test]
        public void ExportThenImport_IntoNewDirectory_DataMatchesOriginal_OriginalUntouched()
        {
            var clock = new SystemWallClock();
            var campaignRepository = new SqliteCampaignRepository(clock);
            CampaignHandle campaign = CreateCampaign(campaignRepository, _workDir, clock);

            var sceneRepository = new SqliteSceneRepository(clock);
            SceneId sceneId = sceneRepository.CreateScene(campaign, "Export Scene", NewCommandId(), TestCorrelationId).Value.SceneId;
            TokenId tokenId = sceneRepository.CreateToken(campaign, sceneId, new TokenPosition(3, 4), NewCommandId(), TestCorrelationId).Value.TokenId;

            var exportRepository = new SqliteExportRepository(clock);
            string odcampPath = Path.Combine(_odcampParentDir, "Campaign.odcamp");
            Result<string> exported = exportRepository.ExportCampaign(campaign, odcampPath, TestCorrelationId);
            Assert.That(exported.IsSuccess, Is.True);
            Assert.That(File.Exists(odcampPath), Is.True);

            campaignRepository.Close(campaign, TestCorrelationId);

            Result<string> imported = exportRepository.ImportCampaign(odcampPath, _odcampParentDir, TestCorrelationId);
            Assert.That(imported.IsSuccess, Is.True);
            Assert.That(imported.Value, Is.Not.EqualTo(_workDir));

            var importedRepository = new SqliteCampaignRepository(clock);
            Result<CampaignHandle> importedHandle = importedRepository.Open(imported.Value, TestCorrelationId);
            Assert.That(importedHandle.IsSuccess, Is.True);

            using (var connection = new SqliteConnection("Data Source=" + Path.Combine(imported.Value, "campaign.db") + ";Mode=ReadOnly"))
            {
                connection.Open();
                using var sceneCmd = connection.CreateCommand();
                sceneCmd.CommandText = "SELECT COUNT(*) FROM Scene WHERE SceneId = $id;";
                sceneCmd.Parameters.AddWithValue("$id", sceneId.ToString());
                Assert.That(Convert.ToInt64(sceneCmd.ExecuteScalar()), Is.EqualTo(1));

                using var tokenCmd = connection.CreateCommand();
                tokenCmd.CommandText = "SELECT COUNT(*) FROM Token WHERE TokenId = $id;";
                tokenCmd.Parameters.AddWithValue("$id", tokenId.ToString());
                Assert.That(Convert.ToInt64(tokenCmd.ExecuteScalar()), Is.EqualTo(1));
            }

            importedRepository.Close(importedHandle.Value, TestCorrelationId);

            // The original working campaign must be untouched by export/import.
            var reopenOriginal = new SqliteCampaignRepository(clock);
            Result<CampaignHandle> reopened = reopenOriginal.Open(_workDir, TestCorrelationId);
            Assert.That(reopened.IsSuccess, Is.True);
            reopenOriginal.Close(reopened.Value, TestCorrelationId);
        }

        [Test]
        public void ExportedDatabase_PassesTheSameQuickCheckABackupDoes()
        {
            var clock = new SystemWallClock();
            var campaignRepository = new SqliteCampaignRepository(clock);
            CampaignHandle campaign = CreateCampaign(campaignRepository, _workDir, clock);

            var exportRepository = new SqliteExportRepository(clock);
            string odcampPath = Path.Combine(_odcampParentDir, "Campaign.odcamp");
            Result<string> exported = exportRepository.ExportCampaign(campaign, odcampPath, TestCorrelationId);
            Assert.That(exported.IsSuccess, Is.True);

            string extractDir = Path.Combine(_odcampParentDir, "extracted-for-check");
            ZipFile.ExtractToDirectory(odcampPath, extractDir);

            using var connection = new SqliteConnection("Data Source=" + Path.Combine(extractDir, "campaign.db") + ";Mode=ReadOnly;Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check;";
            Assert.That((string)command.ExecuteScalar()!, Is.EqualTo("ok"));

            campaignRepository.Close(campaign, TestCorrelationId);
        }

        [Test]
        public void Export_ArchiveContainsAllFiveRequiredEntries()
        {
            var clock = new SystemWallClock();
            var campaignRepository = new SqliteCampaignRepository(clock);
            CampaignHandle campaign = CreateCampaign(campaignRepository, _workDir, clock);

            var exportRepository = new SqliteExportRepository(clock);
            string odcampPath = Path.Combine(_odcampParentDir, "Campaign.odcamp");
            Result<string> exported = exportRepository.ExportCampaign(campaign, odcampPath, TestCorrelationId);
            Assert.That(exported.IsSuccess, Is.True);

            using (var archive = ZipFile.OpenRead(odcampPath))
            {
                Assert.That(archive.GetEntry("manifest.json"), Is.Not.Null);
                Assert.That(archive.GetEntry("campaign.db"), Is.Not.Null);
                Assert.That(archive.GetEntry("checksums.json"), Is.Not.Null);
                Assert.That(archive.GetEntry("export-manifest.json"), Is.Not.Null);
            }

            campaignRepository.Close(campaign, TestCorrelationId);
        }

        [Test]
        public void Import_TargetDirectoryAlreadyExistsAndNonEmpty_ReturnsTypedError_NoAutomaticMerge()
        {
            var clock = new SystemWallClock();
            var campaignRepository = new SqliteCampaignRepository(clock);
            CampaignHandle campaign = CreateCampaign(campaignRepository, _workDir, clock);

            var exportRepository = new SqliteExportRepository(clock);
            string odcampPath = Path.Combine(_odcampParentDir, "Campaign.odcamp");
            Result<string> exported = exportRepository.ExportCampaign(campaign, odcampPath, TestCorrelationId);
            Assert.That(exported.IsSuccess, Is.True);
            campaignRepository.Close(campaign, TestCorrelationId);

            // Pre-create the exact target directory the import would use, with
            // unrelated content, to prove import refuses to merge into it.
            string collidingDir = Path.Combine(_odcampParentDir, "imported-" + campaign.CampaignId);
            Directory.CreateDirectory(collidingDir);
            File.WriteAllText(Path.Combine(collidingDir, "unexpected.txt"), "pre-existing content");

            Result<string> imported = exportRepository.ImportCampaign(odcampPath, _odcampParentDir, TestCorrelationId);

            Assert.That(imported.IsFailure, Is.True);
            Assert.That(imported.Error.Code, Is.EqualTo(ErrorCodes.PersistenceExportImportFailed));
            Assert.That(File.Exists(Path.Combine(collidingDir, "unexpected.txt")), Is.True, "the pre-existing content must be untouched -- proves no merge attempt happened");
        }

        [Test]
        public void Import_CorruptManifestInsideArchive_ReturnsTypedManifestInvalid_NoRawException()
        {
            var clock = new SystemWallClock();
            var campaignRepository = new SqliteCampaignRepository(clock);
            CampaignHandle campaign = CreateCampaign(campaignRepository, _workDir, clock);

            var exportRepository = new SqliteExportRepository(clock);
            string odcampPath = Path.Combine(_odcampParentDir, "Campaign.odcamp");
            Result<string> exported = exportRepository.ExportCampaign(campaign, odcampPath, TestCorrelationId);
            Assert.That(exported.IsSuccess, Is.True);
            campaignRepository.Close(campaign, TestCorrelationId);

            // Rebuild the archive with a structurally-valid-JSON but semantically
            // invalid manifest.json entry (missing required manifest fields) --
            // inscenating a corrupted .odcamp rather than a healthy one. This
            // specifically exercises the domain-level ManifestInvalid path, not
            // the earlier raw-JSON-structure SerializationInvalidPayload path
            // (also a typed error, but a different one, covered implicitly by
            // CampaignManifestV1Codec's own tests).
            string extractDir = Path.Combine(_odcampParentDir, "corrupt-source");
            ZipFile.ExtractToDirectory(odcampPath, extractDir);
            File.WriteAllText(Path.Combine(extractDir, "manifest.json"), "{\"contractType\":\"odyssey.persistence.campaignmanifest\",\"contractVersion\":1}");

            string corruptOdcampPath = Path.Combine(_odcampParentDir, "Corrupt.odcamp");
            ZipFile.CreateFromDirectory(extractDir, corruptOdcampPath, CompressionLevel.Optimal, includeBaseDirectory: false);

            Result<string> imported = exportRepository.ImportCampaign(corruptOdcampPath, _odcampParentDir, TestCorrelationId);

            Assert.That(imported.IsFailure, Is.True);
            Assert.That(imported.Error.Code, Is.EqualTo(ErrorCodes.PersistenceManifestInvalid));
        }

        [Test]
        public void Import_UnsafePathTraversalEntry_ReturnsTypedError_NoExtraction()
        {
            string maliciousOdcampPath = Path.Combine(_odcampParentDir, "Malicious.odcamp");
            string stagingDir = Path.Combine(_odcampParentDir, "malicious-staging");
            Directory.CreateDirectory(stagingDir);
            File.WriteAllText(Path.Combine(stagingDir, "manifest.json"), "{}");

            using (var archive = ZipFile.Open(maliciousOdcampPath, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(Path.Combine(stagingDir, "manifest.json"), "manifest.json");
                ZipArchiveEntry traversalEntry = archive.CreateEntry("../escaped.txt");
                using var writer = new StreamWriter(traversalEntry.Open());
                writer.Write("escaped content");
            }

            var clock = new SystemWallClock();
            var exportRepository = new SqliteExportRepository(clock);
            Result<string> imported = exportRepository.ImportCampaign(maliciousOdcampPath, _odcampParentDir, TestCorrelationId);

            Assert.That(imported.IsFailure, Is.True);
            Assert.That(imported.Error.Code, Is.EqualTo(ErrorCodes.PersistenceExportImportFailed));

            string escapedPath = Path.Combine(Path.GetFullPath(_odcampParentDir), "..", "escaped.txt");
            Assert.That(File.Exists(Path.GetFullPath(escapedPath)), Is.False, "a path-traversal entry must never be written outside the extraction directory");
        }

        [Test]
        public void Export_DatabaseSchemaVersionMismatch_ReturnsTypedError_NoMigrationAttempted()
        {
            var clock = new SystemWallClock();
            var campaignRepository = new SqliteCampaignRepository(clock);
            CampaignHandle campaign = CreateCampaign(campaignRepository, _workDir, clock);

            var exportRepository = new SqliteExportRepository(clock);
            string odcampPath = Path.Combine(_odcampParentDir, "Campaign.odcamp");
            Result<string> exported = exportRepository.ExportCampaign(campaign, odcampPath, TestCorrelationId);
            Assert.That(exported.IsSuccess, Is.True);
            campaignRepository.Close(campaign, TestCorrelationId);

            // Rebuild the archive with manifest.json's databaseSchemaVersion bumped
            // to a version this application does not support.
            string extractDir = Path.Combine(_odcampParentDir, "future-version-source");
            ZipFile.ExtractToDirectory(odcampPath, extractDir);
            string manifestPath = Path.Combine(extractDir, "manifest.json");
            string manifestText = File.ReadAllText(manifestPath);
            string tamperedManifest = manifestText.Replace("\"databaseSchemaVersion\":\"1.0.0\"", "\"databaseSchemaVersion\":\"99.0.0\"");
            Assert.That(tamperedManifest, Is.Not.EqualTo(manifestText), "fixture assumption: the manifest must actually contain databaseSchemaVersion 1.0.0 to tamper with");
            File.WriteAllText(manifestPath, tamperedManifest);

            string futureOdcampPath = Path.Combine(_odcampParentDir, "FutureVersion.odcamp");
            ZipFile.CreateFromDirectory(extractDir, futureOdcampPath, CompressionLevel.Optimal, includeBaseDirectory: false);

            Result<string> imported = exportRepository.ImportCampaign(futureOdcampPath, _odcampParentDir, TestCorrelationId);

            Assert.That(imported.IsFailure, Is.True);
            Assert.That(imported.Error.Code, Is.EqualTo(ErrorCodes.PersistenceExportImportFailed));
        }

        [Test]
        public void Export_DestinationAlreadyExists_ReturnsTypedCreateFailed_NoOverwrite()
        {
            var clock = new SystemWallClock();
            var campaignRepository = new SqliteCampaignRepository(clock);
            CampaignHandle campaign = CreateCampaign(campaignRepository, _workDir, clock);

            string odcampPath = Path.Combine(_odcampParentDir, "Campaign.odcamp");
            File.WriteAllText(odcampPath, "pre-existing file, not a real archive");

            var exportRepository = new SqliteExportRepository(clock);
            Result<string> result = exportRepository.ExportCampaign(campaign, odcampPath, TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceExportCreateFailed));
            Assert.That(File.ReadAllText(odcampPath), Is.EqualTo("pre-existing file, not a real archive"));

            campaignRepository.Close(campaign, TestCorrelationId);
        }
    }
}
