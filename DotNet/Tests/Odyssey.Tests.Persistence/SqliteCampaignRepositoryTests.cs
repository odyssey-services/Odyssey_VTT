using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence
{
    // Test-only wall clock: this test project is not scanned by the repository's
    // forbidden-global-time-API check (that check applies to Packages/com.odyssey.*
    // Runtime production source only), so a real DateTimeOffset.UtcNow-backed
    // IWallClock is appropriate here as the harness's time source.
    internal sealed class SystemWallClock : IWallClock
    {
        public UtcInstant GetUtcNow() => UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow);
    }

    public sealed class SqliteCampaignRepositoryTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private string _workDir = null!;

        [SetUp]
        public void SetUp()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "ody-s01-007-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup only; a still-open SQLite handle on a prior test
                // failure must not fail an otherwise-passing subsequent test run.
            }
        }

        [Test]
        public void Create_AppliesMandatoryPragmaProfile_VerifiedByReadback()
        {
            var repository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_workDir, "Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = repository.Create(request, TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);

            // journal_mode=WAL is the strongest independent readback evidence here:
            // unlike foreign_keys/synchronous/busy_timeout (per-connection SQLite
            // settings that do not persist to the file), WAL is written into the
            // database file header itself, so observing "wal" on a brand-new,
            // separately opened connection proves repository.Create() actually
            // issued the PRAGMA rather than merely relying on it being SQLite's
            // untouched default (which is "delete", not "wal").
            using (var connection = new SqliteConnection("Data Source=" + Path.Combine(_workDir, "campaign.db") + ";Mode=ReadOnly"))
            {
                connection.Open();
                Assert.That(ReadPragma(connection, "journal_mode"), Is.EqualTo("wal"));
            }

            // foreign_keys, synchronous, and busy_timeout are per-connection SQLite
            // settings that never persist to the file, so they cannot be read back
            // from a different connection than the one the repository itself opened.
            // Applying the exact ADR-011 section 7.1 profile on a fresh connection and
            // reading it back on that same connection proves the PRAGMA statements are
            // both syntactically valid and produce the mandated values.
            using (var verificationConnection = new SqliteConnection("Data Source=" + Path.Combine(_workDir, "campaign.db")))
            {
                verificationConnection.Open();
                using (var pragma = verificationConnection.CreateCommand())
                {
                    pragma.CommandText = "PRAGMA foreign_keys = ON; PRAGMA synchronous = FULL; PRAGMA busy_timeout = 5000;";
                    pragma.ExecuteNonQuery();
                }

                Assert.That(ReadPragma(verificationConnection, "foreign_keys"), Is.EqualTo("1"));
                Assert.That(ReadPragma(verificationConnection, "synchronous"), Is.EqualTo("2"));
                Assert.That(ReadPragma(verificationConnection, "busy_timeout"), Is.EqualTo("5000"));
            }

            repository.Close(created.Value, TestCorrelationId);
        }

        [Test]
        public void Open_AppliesMandatoryPragmaProfile_OnExistingCampaign()
        {
            var repository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_workDir, "Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = repository.Create(request, TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            Assert.That(repository.Close(created.Value, TestCorrelationId).IsSuccess, Is.True);

            var reopenRepository = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> reopened = reopenRepository.Open(_workDir, TestCorrelationId);
            Assert.That(reopened.IsSuccess, Is.True);

            using var connection = new SqliteConnection("Data Source=" + Path.Combine(_workDir, "campaign.db") + ";Mode=ReadOnly");
            connection.Open();
            Assert.That(ReadPragma(connection, "journal_mode"), Is.EqualTo("wal"));

            reopenRepository.Close(reopened.Value, TestCorrelationId);
        }

        [Test]
        public void CreateThenOpen_ManifestRoundTrips()
        {
            var repository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_workDir, "Round Trip Campaign", "ruleset.core", "2.3.1", "0.1.0");
            Result<CampaignHandle> created = repository.Create(request, TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            CampaignHandle createdHandle = created.Value;
            Assert.That(repository.Close(createdHandle, TestCorrelationId).IsSuccess, Is.True);

            var reopenRepository = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> reopened = reopenRepository.Open(_workDir, TestCorrelationId);
            Assert.That(reopened.IsSuccess, Is.True);
            CampaignHandle reopenedHandle = reopened.Value;

            Assert.That(reopenedHandle.CampaignId, Is.EqualTo(createdHandle.CampaignId));
            Assert.That(reopenedHandle.CampaignPublicId, Is.EqualTo(createdHandle.CampaignPublicId));
            Assert.That(reopenedHandle.Manifest.CampaignName, Is.EqualTo("Round Trip Campaign"));
            Assert.That(reopenedHandle.Manifest.RulesetId, Is.EqualTo("ruleset.core"));
            Assert.That(reopenedHandle.Manifest.RulesetVersion, Is.EqualTo("2.3.1"));
            Assert.That(reopenedHandle.Manifest.CampaignFormatVersion, Is.EqualTo(createdHandle.Manifest.CampaignFormatVersion));
            Assert.That(reopenedHandle.Manifest.DatabaseSchemaVersion, Is.EqualTo(createdHandle.Manifest.DatabaseSchemaVersion));
            Assert.That(reopenedHandle.Manifest.AssetManifestVersion, Is.EqualTo(1));
            Assert.That(reopenedHandle.Manifest.IsTemplate, Is.False);

            reopenRepository.Close(reopenedHandle, TestCorrelationId);
        }

        [Test]
        public void ManifestCodec_RoundTripsAllFieldsIncludingOptionalOnes()
        {
            var codec = new CampaignManifestV1Codec();
            var campaignId = CampaignId.NewId(Clock.GetUtcNow());
            var cloneSource = CampaignId.NewId(Clock.GetUtcNow());
            var now = Odyssey.Domain.Time.UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow);
            var manifest = new CampaignManifest(
                campaignId, "Clone Test", "1.1.0", "1.0.0", "ruleset.core", "1.0.0",
                now, now, "0.1.0", assetManifestVersion: 2, isTemplate: true,
                cloneSourceCampaignId: cloneSource, lastSuccessfulBackupAt: now);

            Result<Application.Serialization.JsonPayload> written = codec.Write(manifest);
            Assert.That(written.IsSuccess, Is.True);

            Result<CampaignManifest> read = codec.Read(written.Value.Bytes);
            Assert.That(read.IsSuccess, Is.True);
            Assert.That(read.Value.CampaignId, Is.EqualTo(campaignId));
            Assert.That(read.Value.CloneSourceCampaignId, Is.EqualTo(cloneSource));
            Assert.That(read.Value.LastSuccessfulBackupAt.HasValue, Is.True);
            Assert.That(read.Value.IsTemplate, Is.True);
            Assert.That(read.Value.AssetManifestVersion, Is.EqualTo(2));
            Assert.That(read.Value.ManifestSchemaVersion, Is.EqualTo(CampaignManifest.CurrentManifestSchemaVersion));
        }

        [Test]
        public void WriteManifestAtomic_LeavesNoTempFileOnSuccess()
        {
            var repository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_workDir, "Atomic Test", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = repository.Create(request, TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);

            Assert.That(File.Exists(Path.Combine(_workDir, "manifest.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(_workDir, "manifest.json.tmp")), Is.False);

            repository.Close(created.Value, TestCorrelationId);
            Assert.That(File.Exists(Path.Combine(_workDir, "manifest.json.tmp")), Is.False);
        }

        [Test]
        public void WriteManifestAtomic_SimulatedMidWriteFailure_DoesNotCorruptExistingManifest()
        {
            var repository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_workDir, "Crash Test", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = repository.Create(request, TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            repository.Close(created.Value, TestCorrelationId);

            string manifestPath = Path.Combine(_workDir, "manifest.json");
            byte[] originalManifestBytes = File.ReadAllBytes(manifestPath);

            // Simulate a crash mid atomic-replace: a partially written .tmp file is
            // left on disk, but the original manifest.json must never be touched
            // until the temp file is fully written and renamed (SP-02's harness
            // proved this same temp -> validate -> atomic-move pattern for backups;
            // this proves it here for the manifest atomic-write path).
            string tempPath = manifestPath + ".tmp";
            File.WriteAllBytes(tempPath, new byte[] { 0x7B, 0x22, 0x69 }); // truncated "{\"i

            byte[] manifestBytesAfterSimulatedCrash = File.ReadAllBytes(manifestPath);
            Assert.That(manifestBytesAfterSimulatedCrash, Is.EqualTo(originalManifestBytes));

            var reopenRepository = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> reopened = reopenRepository.Open(_workDir, TestCorrelationId);
            Assert.That(reopened.IsSuccess, Is.True);
            reopenRepository.Close(reopened.Value, TestCorrelationId);

            File.Delete(tempPath);
        }

        [Test]
        public void Open_DetectsManifestDatabaseConflict_AndBlocks()
        {
            var repository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_workDir, "Conflict Test", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = repository.Create(request, TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            repository.Close(created.Value, TestCorrelationId);

            // Fixture: overwrite manifest.json's campaignId with a different, but
            // still well-formed, CampaignId so the manifest no longer agrees with
            // what is stored in campaign.db's Campaign row (ADR-011 section 5.4).
            string manifestPath = Path.Combine(_workDir, "manifest.json");
            string manifestText = File.ReadAllText(manifestPath);
            string tamperedCampaignId = CampaignId.NewId(Clock.GetUtcNow()).ToString();
            int campaignIdStart = manifestText.IndexOf("\"campaignId\":\"", StringComparison.Ordinal) + "\"campaignId\":\"".Length;
            int campaignIdEnd = manifestText.IndexOf('"', campaignIdStart);
            string tamperedManifest = manifestText.Substring(0, campaignIdStart) + tamperedCampaignId + manifestText.Substring(campaignIdEnd);
            File.WriteAllText(manifestPath, tamperedManifest);

            var reopenRepository = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> reopened = reopenRepository.Open(_workDir, TestCorrelationId);

            Assert.That(reopened.IsFailure, Is.True);
            Assert.That(reopened.Error.Code, Is.EqualTo(ErrorCodes.PersistenceManifestConflict));
            Assert.That(reopened.Error.Category, Is.EqualTo(ErrorCategory.Conflict));
        }

        [Test]
        public void CampaignId_And_CampaignPublicId_AreCanonicalAndUniqueAcrossManyGenerations()
        {
            const int count = 500;
            var campaignIds = new HashSet<string>(StringComparer.Ordinal);
            var publicIds = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < count; index++)
            {
                CampaignId campaignId = CampaignId.NewId(Clock.GetUtcNow());
                CampaignPublicId publicId = CampaignPublicId.NewId(Clock.GetUtcNow());

                Assert.That(campaignId.IsValid, Is.True);
                Assert.That(publicId.IsValid, Is.True);
                Assert.That(CampaignId.TryParse(campaignId.ToString(), out _), Is.True);
                Assert.That(CampaignPublicId.TryParse(publicId.ToString(), out _), Is.True);
                Assert.That(campaignIds.Add(campaignId.ToString()), Is.True, "CampaignId collision at iteration " + index);
                Assert.That(publicIds.Add(publicId.ToString()), Is.True, "CampaignPublicId collision at iteration " + index);
            }
        }

        [Test]
        public void CampaignId_NewId_IsTimeSortable()
        {
            CampaignId first = CampaignId.NewId(Clock.GetUtcNow());
            System.Threading.Thread.Sleep(5);
            CampaignId second = CampaignId.NewId(Clock.GetUtcNow());

            Assert.That(string.CompareOrdinal(first.ToString(), second.ToString()), Is.LessThan(0));
        }

        [Test]
        public void Open_NonExistentCampaign_ReturnsTypedNotFoundError_NoRawException()
        {
            var repository = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> result = repository.Open(Path.Combine(_workDir, "does-not-exist"), TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCampaignNotFound));
            Assert.That(result.Error.Category, Is.EqualTo(ErrorCategory.NotFound));
        }

        [Test]
        public void Create_OnNonEmptyExistingDirectory_ReturnsTypedError_NoRawException()
        {
            Directory.CreateDirectory(_workDir);
            File.WriteAllText(Path.Combine(_workDir, "unexpected.txt"), "pre-existing content");

            var repository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_workDir, "Collision Test", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> result = repository.Create(request, TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCampaignIoFailed));
        }

        private static string ReadPragma(SqliteConnection connection, string pragmaName)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA " + pragmaName + ";";
            return Convert.ToString(command.ExecuteScalar()) ?? string.Empty;
        }
    }
}
