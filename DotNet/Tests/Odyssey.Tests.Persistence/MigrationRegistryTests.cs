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
    /// <summary>
    /// ODY-S01-010: migration registry baseline -- SchemaHistory row creation on
    /// Create(), non-duplication on Open(), and internal well-formedness of the
    /// registered-migrations list (SLICE-01_IMPLEMENTATION_BACKLOG.md section
    /// 2.1: "a test proving the registry itself is well-formed and versioned").
    /// Not a test of a migration runner -- none exists yet in this task's scope.
    /// </summary>
    public sealed class MigrationRegistryTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private string _workDir = null!;

        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));

        [SetUp]
        public void SetUp()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "ody-s01-010-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true); } catch (IOException) { }
        }

        [Test]
        public void Registry_IsWellFormed_NoDuplicateIds_MonotonicOrder_ChecksumPresent()
        {
            IReadOnlyList<MigrationDescriptor> registered = MigrationRegistry.Registered;
            Assert.That(registered.Count, Is.GreaterThanOrEqualTo(1));

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            string? previousId = null;
            foreach (MigrationDescriptor descriptor in registered)
            {
                Assert.That(string.IsNullOrWhiteSpace(descriptor.MigrationId), Is.False, "MigrationId must be present");
                Assert.That(string.IsNullOrWhiteSpace(descriptor.FromVersion), Is.False, "FromVersion must be present");
                Assert.That(string.IsNullOrWhiteSpace(descriptor.ToVersion), Is.False, "ToVersion must be present");
                Assert.That(string.IsNullOrWhiteSpace(descriptor.CodeChecksum), Is.False, "CodeChecksum must be present");
                Assert.That(descriptor.CodeChecksum, Has.Length.EqualTo(64), "CodeChecksum is a SHA-256 hex digest");

                Assert.That(seenIds.Add(descriptor.MigrationId), Is.True, "MigrationId must not be duplicated: " + descriptor.MigrationId);

                if (previousId != null)
                {
                    Assert.That(string.CompareOrdinal(previousId, descriptor.MigrationId), Is.LessThan(0),
                        "registered migrations must be in strictly increasing order: " + previousId + " then " + descriptor.MigrationId);
                }

                previousId = descriptor.MigrationId;
            }
        }

        [Test]
        public void Registry_InitialEntry_IsIdentityMigration_FromEqualsToEqualsCurrentSchemaVersion()
        {
            MigrationDescriptor initial = MigrationRegistry.Initial;
            Assert.That(initial.MigrationId, Is.EqualTo("0001_Initial"));
            Assert.That(initial.FromVersion, Is.EqualTo(initial.ToVersion), "the identity migration does not change schema, so From/To must match");
        }

        [Test]
        public void Create_InsertsExactlyOneSchemaHistoryRow_MatchingDatabaseSchemaVersionInManifest()
        {
            var repository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_workDir, "Migration Registry Test", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = repository.Create(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            CampaignHandle handle = created.Value;

            using var connection = new SqliteConnection("Data Source=" + Path.Combine(_workDir, "campaign.db") + ";Mode=ReadOnly");
            connection.Open();

            using (var count = connection.CreateCommand())
            {
                count.CommandText = "SELECT COUNT(*) FROM SchemaHistory;";
                Assert.That(Convert.ToInt64(count.ExecuteScalar()), Is.EqualTo(1));
            }

            using (var select = connection.CreateCommand())
            {
                select.CommandText = "SELECT MigrationId, FromVersion, ToVersion, CodeChecksum, Status, BackupId, FailureCode FROM SchemaHistory LIMIT 1;";
                using SqliteDataReader reader = select.ExecuteReader();
                Assert.That(reader.Read(), Is.True);
                Assert.That(reader.GetString(0), Is.EqualTo("0001_Initial"));
                Assert.That(reader.GetString(1), Is.EqualTo(handle.Manifest.DatabaseSchemaVersion));
                Assert.That(reader.GetString(2), Is.EqualTo(handle.Manifest.DatabaseSchemaVersion));
                Assert.That(reader.GetString(3), Is.EqualTo(MigrationRegistry.Initial.CodeChecksum));
                Assert.That(reader.GetString(4), Is.EqualTo("Completed"));
                Assert.That(reader.IsDBNull(5), Is.True, "BackupId must be null for the identity migration -- no pre-migration snapshot exists for a brand-new campaign");
                Assert.That(reader.IsDBNull(6), Is.True);
            }

            repository.Close(handle, TestCorrelationId);
        }

        [Test]
        public void Open_DoesNotDuplicateOrRewriteInitialSchemaHistoryRow()
        {
            var repository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_workDir, "Reopen Test", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = repository.Create(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            Assert.That(repository.Close(created.Value, TestCorrelationId).IsSuccess, Is.True);

            string dbPath = Path.Combine(_workDir, "campaign.db");
            string startedAtBeforeReopen;
            using (var connection = new SqliteConnection("Data Source=" + dbPath + ";Mode=ReadOnly"))
            {
                connection.Open();
                using var select = connection.CreateCommand();
                select.CommandText = "SELECT StartedAt FROM SchemaHistory WHERE MigrationId = '0001_Initial';";
                startedAtBeforeReopen = (string)select.ExecuteScalar()!;
            }

            var reopenRepository = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> reopened = reopenRepository.Open(_workDir, TestCorrelationId);
            Assert.That(reopened.IsSuccess, Is.True);
            reopenRepository.Close(reopened.Value, TestCorrelationId);

            using var verifyConnection = new SqliteConnection("Data Source=" + dbPath + ";Mode=ReadOnly");
            verifyConnection.Open();
            using (var count = verifyConnection.CreateCommand())
            {
                count.CommandText = "SELECT COUNT(*) FROM SchemaHistory;";
                Assert.That(Convert.ToInt64(count.ExecuteScalar()), Is.EqualTo(1), "Open must not insert a second SchemaHistory row");
            }

            using (var select = verifyConnection.CreateCommand())
            {
                select.CommandText = "SELECT StartedAt FROM SchemaHistory WHERE MigrationId = '0001_Initial';";
                string startedAtAfterReopen = (string)select.ExecuteScalar()!;
                Assert.That(startedAtAfterReopen, Is.EqualTo(startedAtBeforeReopen), "Open must not rewrite the existing 0001_Initial row");
            }
        }
    }
}
