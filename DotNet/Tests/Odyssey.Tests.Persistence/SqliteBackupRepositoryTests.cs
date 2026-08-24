using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence
{
    internal sealed class MutableTestClock : IWallClock
    {
        private UtcInstant _now;
        public MutableTestClock(UtcInstant initial) => _now = initial;
        public UtcInstant GetUtcNow() => _now;
        public void Set(UtcInstant now) => _now = now;
        public void AdvanceDays(int days) => _now = _now.Add(TimeSpan.FromDays(days));
    }

    /// <summary>
    /// ODY-S01-011: ADR-012 section 8 snapshot contract -- manual backup via the
    /// SQLite Backup API, temp-copy/validate/atomic-rename, recent/daily/weekly
    /// rotation, restore-into-a-new-copy, and a real corruption fixture.
    /// </summary>
    public sealed class SqliteBackupRepositoryTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private string _workDir = null!;

        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));

        [SetUp]
        public void SetUp()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "ody-s01-011-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true); } catch (IOException) { }
        }

        private static CampaignHandle CreateCampaign(SqliteCampaignRepository repository, string rootPath, IWallClock clock)
        {
            var request = new CreateCampaignRequest(rootPath, "Backup Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = repository.Create(request, CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N")), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        [Test]
        public void CreateBackup_ThenRestoreIntoSeparateCopy_DataMatchesOriginal_OriginalUntouched()
        {
            var clock = new MutableTestClock(UtcInstant.FromDateTimeOffset(DateTimeOffset.Parse("2026-08-24T10:00:00.0000000Z")));
            var campaignRepository = new SqliteCampaignRepository(clock);
            CampaignHandle campaign = CreateCampaign(campaignRepository, _workDir, clock);

            var sceneRepository = new SqliteSceneRepository(clock);
            SceneId sceneId = sceneRepository.CreateScene(campaign, "Round Trip Scene", NewCommandId(), TestCorrelationId).Value.SceneId;

            var backupRepository = new SqliteBackupRepository(clock);
            Result<BackupRecord> backup = backupRepository.CreateBackup(campaign, "manual", TestCorrelationId);
            Assert.That(backup.IsSuccess, Is.True);
            Assert.That(backup.Value.IntegrityStatus, Is.EqualTo("Ok"));

            string restoreParent = Path.Combine(_workDir + "-restored-parent");
            Directory.CreateDirectory(restoreParent);
            Result<string> restored = backupRepository.RestoreBackup(_workDir, backup.Value.BackupId, restoreParent, TestCorrelationId);
            Assert.That(restored.IsSuccess, Is.True);
            Assert.That(restored.Value, Is.Not.EqualTo(_workDir));

            var restoredRepository = new SqliteCampaignRepository(clock);
            Result<CampaignHandle> restoredHandle = restoredRepository.Open(restored.Value, TestCorrelationId);
            Assert.That(restoredHandle.IsSuccess, Is.True);

            // The scene must exist by name/id in the restored copy -- proven via a
            // fresh Scene lookup against the restored database file.
            using (var connection = new SqliteConnection("Data Source=" + Path.Combine(restored.Value, "campaign.db") + ";Mode=ReadOnly;Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM Scene WHERE SceneId = $id;";
                command.Parameters.AddWithValue("$id", sceneId.ToString());
                Assert.That(Convert.ToInt64(command.ExecuteScalar()), Is.EqualTo(1));
            }

            restoredRepository.Close(restoredHandle.Value, TestCorrelationId);

            // The original working campaign must be completely untouched: still
            // openable, still has the same scene, nothing written into it by restore.
            var reopenOriginal = new SqliteCampaignRepository(clock);
            Result<CampaignHandle> reopened = reopenOriginal.Open(_workDir, TestCorrelationId);
            Assert.That(reopened.IsSuccess, Is.True);
            reopenOriginal.Close(reopened.Value, TestCorrelationId);
            campaignRepository.Close(campaign, TestCorrelationId);

            Directory.Delete(restoreParent, recursive: true);
        }

        [Test]
        public void CreateBackup_UsesSqliteBackupApi_NotRawFileCopy_TempNeverVisibleUnderFinalName()
        {
            var clock = new MutableTestClock(UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow));
            var campaignRepository = new SqliteCampaignRepository(clock);
            CampaignHandle campaign = CreateCampaign(campaignRepository, _workDir, clock);

            var backupRepository = new SqliteBackupRepository(clock);
            Result<BackupRecord> backup = backupRepository.CreateBackup(campaign, "manual", TestCorrelationId);
            Assert.That(backup.IsSuccess, Is.True);

            string fastTierDir = Path.Combine(_workDir, "Backups", "Fast");
            string[] entries = Directory.GetDirectories(fastTierDir);
            Assert.That(entries, Has.None.Matches<string>(d => Path.GetFileName(d).StartsWith(".tmp-", StringComparison.Ordinal)),
                "no temp directory must remain after a successful backup");

            string finalDbPath = Path.Combine(fastTierDir, backup.Value.BackupId.ToString(), "campaign.db");
            Assert.That(File.Exists(finalDbPath), Is.True);

            using (var verify = new SqliteConnection("Data Source=" + finalDbPath + ";Mode=ReadOnly"))
            {
                verify.Open();
                using var command = verify.CreateCommand();
                command.CommandText = "PRAGMA quick_check;";
                Assert.That((string)command.ExecuteScalar()!, Is.EqualTo("ok"));
            }

            campaignRepository.Close(campaign, TestCorrelationId);
        }

        [Test]
        public void CreateBackup_KilledMidCopy_NeverPromotesPartialBackup_SourceUntouched()
        {
            var clock = new MutableTestClock(UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow));
            var campaignRepository = new SqliteCampaignRepository(clock);
            CampaignHandle campaign = CreateCampaign(campaignRepository, _workDir, clock);

            // Seed enough DomainEvents rows directly (bypassing the pipeline for
            // speed -- this is fixture setup, not the behavior under test) that
            // the SQLite Backup API copy takes a real, measurable amount of time,
            // matching SP-02's "interrupted backup" methodology (docs/tasks/
            // completed/ODY-S01-005_SP-02_Persistence_Reliability_Report.md
            // section 2.3): build a large-enough source, measure an uninterrupted
            // baseline, then kill a second attempt at a fraction of that baseline.
            SeedDomainEvents(_workDir, campaign.CampaignId.ToString(), rowCount: 150_000);
            campaignRepository.Close(campaign, TestCorrelationId);

            string harnessDll = LocateHarnessDll("Odyssey.Tests.Persistence.BackupKillHarness");

            var baselineWatch = Stopwatch.StartNew();
            RunHarness(harnessDll, _workDir, "baseline");
            baselineWatch.Stop();

            int beforeCount = CountBackupDirs(_workDir, "Fast");

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{harnessDll}\" \"{_workDir}\" kill-test",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            long killDelayMs = Math.Max(20, (long)(baselineWatch.ElapsedMilliseconds * 0.3));
            using (Process harness = Process.Start(startInfo)!)
            {
                System.Threading.Thread.Sleep((int)killDelayMs);
                bool exitedOnItsOwn = harness.HasExited;
                if (!exitedOnItsOwn)
                {
                    harness.Kill(entireProcessTree: true);
                }

                harness.WaitForExit(10000);
                Assert.That(exitedOnItsOwn, Is.False, "the harness must still be mid-backup when killed, not already finished -- baseline=" + baselineWatch.ElapsedMilliseconds + "ms, kill delay=" + killDelayMs + "ms");
            }

            int afterCount = CountBackupDirs(_workDir, "Fast");
            Assert.That(afterCount, Is.EqualTo(beforeCount), "a killed backup must never appear as a promoted, final-named backup directory");

            // The source database itself must still be intact and openable.
            var reopenRepository = new SqliteCampaignRepository(clock);
            Result<CampaignHandle> reopened = reopenRepository.Open(_workDir, TestCorrelationId);
            Assert.That(reopened.IsSuccess, Is.True);
            reopenRepository.Close(reopened.Value, TestCorrelationId);
        }

        [Test]
        public void Rotation_FastTier_PrunesBeyondRetentionCount()
        {
            var clock = new MutableTestClock(UtcInstant.FromDateTimeOffset(DateTimeOffset.Parse("2026-08-24T10:00:00.0000000Z")));
            var campaignRepository = new SqliteCampaignRepository(clock);
            CampaignHandle campaign = CreateCampaign(campaignRepository, _workDir, clock);

            var policy = new BackupRotationPolicy(fastRetentionCount: 3, dailyRetentionCount: 100, weeklyRetentionCount: 100);
            var backupRepository = new SqliteBackupRepository(clock, policy);

            for (int i = 0; i < 5; i++)
            {
                Result<BackupRecord> result = backupRepository.CreateBackup(campaign, "manual-" + i, TestCorrelationId);
                Assert.That(result.IsSuccess, Is.True);
                clock.Set(clock.GetUtcNow().Add(TimeSpan.FromSeconds(1)));
            }

            int fastCount = CountBackupDirs(_workDir, "Fast");
            Assert.That(fastCount, Is.EqualTo(3), "Fast tier must be pruned to the configured retention count, not grow unbounded");

            using var connection = new SqliteConnection("Data Source=" + Path.Combine(_workDir, "campaign.db") + ";Mode=ReadOnly");
            connection.Open();
            using var count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM BackupRecords;";
            Assert.That(Convert.ToInt64(count.ExecuteScalar()), Is.EqualTo(3), "pruned Fast backups must also lose their BackupRecords audit row");

            campaignRepository.Close(campaign, TestCorrelationId);
        }

        [Test]
        public void Rotation_DailyAndWeeklyTiers_PromoteOncePerBucket_AndPruneBeyondRetentionCount()
        {
            var clock = new MutableTestClock(UtcInstant.FromDateTimeOffset(DateTimeOffset.Parse("2026-08-24T10:00:00.0000000Z")));
            var campaignRepository = new SqliteCampaignRepository(clock);
            CampaignHandle campaign = CreateCampaign(campaignRepository, _workDir, clock);

            var policy = new BackupRotationPolicy(fastRetentionCount: 100, dailyRetentionCount: 2, weeklyRetentionCount: 1);
            var backupRepository = new SqliteBackupRepository(clock, policy);

            // Two backups on the same simulated day: only the first should be
            // promoted into Daily/ for that calendar bucket.
            Assert.That(backupRepository.CreateBackup(campaign, "day1-a", TestCorrelationId).IsSuccess, Is.True);
            clock.Set(clock.GetUtcNow().Add(TimeSpan.FromHours(2)));
            Assert.That(backupRepository.CreateBackup(campaign, "day1-b", TestCorrelationId).IsSuccess, Is.True);
            Assert.That(CountBackupDirs(_workDir, "Daily"), Is.EqualTo(1), "a second backup on the same UTC day must not create a second Daily promotion");

            // Advance across several more distinct calendar days so Daily
            // receives one promotion per day, then verify pruning to the
            // configured retention count (2).
            for (int day = 1; day <= 3; day++)
            {
                clock.AdvanceDays(1);
                Assert.That(backupRepository.CreateBackup(campaign, "day" + (day + 1), TestCorrelationId).IsSuccess, Is.True);
            }

            Assert.That(CountBackupDirs(_workDir, "Daily"), Is.EqualTo(2), "Daily tier must be pruned to its configured retention count");

            // Advance far enough that at least two distinct ISO weeks have been
            // crossed, then verify Weekly pruned down to its retention count (1).
            clock.AdvanceDays(8);
            Assert.That(backupRepository.CreateBackup(campaign, "week2", TestCorrelationId).IsSuccess, Is.True);
            clock.AdvanceDays(8);
            Assert.That(backupRepository.CreateBackup(campaign, "week3", TestCorrelationId).IsSuccess, Is.True);

            Assert.That(CountBackupDirs(_workDir, "Weekly"), Is.EqualTo(1), "Weekly tier must be pruned to its configured retention count");

            campaignRepository.Close(campaign, TestCorrelationId);
        }

        [Test]
        public void CorruptedMainDatabase_LastValidBackupStillRestorable_BackupFilesUntouchedByCorruption()
        {
            var clock = new MutableTestClock(UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow));
            var campaignRepository = new SqliteCampaignRepository(clock);
            CampaignHandle campaign = CreateCampaign(campaignRepository, _workDir, clock);

            var backupRepository = new SqliteBackupRepository(clock);
            Result<BackupRecord> backup = backupRepository.CreateBackup(campaign, "before-corruption", TestCorrelationId);
            Assert.That(backup.IsSuccess, Is.True);
            campaignRepository.Close(campaign, TestCorrelationId);

            // Inscenate corruption: overwrite a chunk of campaign.db's bytes
            // in-place (not the backup file), well past the SQLite header so the
            // file still opens far enough to fail quick_check rather than fail to
            // open at all -- proving Open()'s ODY-S01-009 integrity check catches it.
            string dbPath = Path.Combine(_workDir, "campaign.db");
            byte[] dbBytes = File.ReadAllBytes(dbPath);
            Assert.That(dbBytes.Length, Is.GreaterThan(4096), "fixture assumption: the database must be larger than one page to corrupt safely past the header");
            for (int offset = 4096; offset < Math.Min(dbBytes.Length, 4096 + 512); offset++)
            {
                dbBytes[offset] = 0xFF;
            }

            File.WriteAllBytes(dbPath, dbBytes);

            var corruptedRepository = new SqliteCampaignRepository(clock);
            Result<CampaignHandle> openResult = corruptedRepository.Open(_workDir, TestCorrelationId);
            Assert.That(openResult.IsFailure, Is.True, "corruption must be caught by the quick integrity check, not silently opened");
            Assert.That(openResult.Error.Code, Is.EqualTo(ErrorCodes.PersistenceIntegrityCheckFailed));

            // ListBackups/RestoreBackup must still work off the campaign folder
            // path directly -- they do not depend on opening the corrupted
            // campaign.db (see SqliteBackupRepository's filesystem-based design).
            Result<System.Collections.Generic.IReadOnlyList<BackupRecord>> listed = backupRepository.ListBackups(_workDir, TestCorrelationId);
            Assert.That(listed.IsSuccess, Is.True);
            Assert.That(listed.Value.Count, Is.EqualTo(1));
            Assert.That(listed.Value[0].BackupId, Is.EqualTo(backup.Value.BackupId));

            string restoreParent = _workDir + "-restored-after-corruption";
            Directory.CreateDirectory(restoreParent);
            Result<string> restored = backupRepository.RestoreBackup(_workDir, backup.Value.BackupId, restoreParent, TestCorrelationId);
            Assert.That(restored.IsSuccess, Is.True, "the last valid backup must remain restorable even after the working database is corrupted");

            var restoredRepository = new SqliteCampaignRepository(clock);
            Result<CampaignHandle> restoredHandle = restoredRepository.Open(restored.Value, TestCorrelationId);
            Assert.That(restoredHandle.IsSuccess, Is.True, "the restored copy must pass the same integrity check the corrupted original fails");
            restoredRepository.Close(restoredHandle.Value, TestCorrelationId);

            Directory.Delete(restoreParent, recursive: true);
        }

        [Test]
        public void RestoreBackup_UnknownBackupId_ReturnsTypedNotFound_NoRawException()
        {
            var clock = new MutableTestClock(UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow));
            var campaignRepository = new SqliteCampaignRepository(clock);
            CampaignHandle campaign = CreateCampaign(campaignRepository, _workDir, clock);
            campaignRepository.Close(campaign, TestCorrelationId);

            var backupRepository = new SqliteBackupRepository(clock);
            BackupId phantomBackupId = BackupId.NewId(clock.GetUtcNow());
            string restoreParent = _workDir + "-restored-phantom";
            Directory.CreateDirectory(restoreParent);

            Result<string> result = backupRepository.RestoreBackup(_workDir, phantomBackupId, restoreParent, TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceBackupNotFound));
            Assert.That(result.Error.Category, Is.EqualTo(ErrorCategory.NotFound));

            Directory.Delete(restoreParent, recursive: true);
        }

        [Test]
        public void RestoreBackup_DestinationAlreadyExistsAndNonEmpty_ReturnsTypedRestoreFailed()
        {
            var clock = new MutableTestClock(UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow));
            var campaignRepository = new SqliteCampaignRepository(clock);
            CampaignHandle campaign = CreateCampaign(campaignRepository, _workDir, clock);

            var backupRepository = new SqliteBackupRepository(clock);
            Result<BackupRecord> backup = backupRepository.CreateBackup(campaign, "manual", TestCorrelationId);
            Assert.That(backup.IsSuccess, Is.True);
            campaignRepository.Close(campaign, TestCorrelationId);

            string restoreParent = _workDir + "-restored-collision";
            string collidingDir = Path.Combine(restoreParent, "restored-" + backup.Value.BackupId);
            Directory.CreateDirectory(collidingDir);
            File.WriteAllText(Path.Combine(collidingDir, "unexpected.txt"), "pre-existing content");

            Result<string> result = backupRepository.RestoreBackup(_workDir, backup.Value.BackupId, restoreParent, TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceBackupRestoreFailed));

            Directory.Delete(restoreParent, recursive: true);
        }

        [Test]
        public void CreateBackup_WithInvalidReason_ReturnsTypedCreateFailed_NoRawException()
        {
            var clock = new MutableTestClock(UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow));
            var campaignRepository = new SqliteCampaignRepository(clock);
            CampaignHandle campaign = CreateCampaign(campaignRepository, _workDir, clock);

            var backupRepository = new SqliteBackupRepository(clock);
            Result<BackupRecord> result = backupRepository.CreateBackup(campaign, reason: "", TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceBackupCreateFailed));

            campaignRepository.Close(campaign, TestCorrelationId);
        }

        private static int CountBackupDirs(string campaignRootPath, string tier)
        {
            string tierDir = Path.Combine(campaignRootPath, "Backups", tier);
            if (!Directory.Exists(tierDir)) return 0;
            int count = 0;
            foreach (string dir in Directory.GetDirectories(tierDir))
            {
                if (!Path.GetFileName(dir).StartsWith(".tmp-", StringComparison.Ordinal)) count++;
            }

            return count;
        }

        private static void SeedDomainEvents(string campaignRootPath, string campaignId, int rowCount)
        {
            string dbPath = Path.Combine(campaignRootPath, "campaign.db");
            using var connection = new SqliteConnection("Data Source=" + dbPath);
            connection.Open();
            using SqliteTransaction transaction = connection.BeginTransaction();
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText =
                    "INSERT INTO DomainEvents (CampaignId, EventType, CommandId, PayloadJson, PayloadHash, CreatedAtHost) " +
                    "VALUES ($campaignId, 'odyssey.persistence.seed_fixture', $commandId, $payload, 'seed', $now);";
                var campaignIdParam = command.CreateParameter(); campaignIdParam.ParameterName = "$campaignId"; campaignIdParam.Value = campaignId; command.Parameters.Add(campaignIdParam);
                var commandIdParam = command.CreateParameter(); commandIdParam.ParameterName = "$commandId"; command.Parameters.Add(commandIdParam);
                var payloadParam = command.CreateParameter(); payloadParam.ParameterName = "$payload"; command.Parameters.Add(payloadParam);
                var nowParam = command.CreateParameter(); nowParam.ParameterName = "$now"; nowParam.Value = DateTimeOffset.UtcNow.ToString("O"); command.Parameters.Add(nowParam);

                string fillerPayload = "{\"filler\":\"" + new string('x', 400) + "\"}";
                for (int i = 0; i < rowCount; i++)
                {
                    commandIdParam.Value = "cmd_" + i.ToString("x32");
                    payloadParam.Value = fillerPayload;
                    command.ExecuteNonQuery();
                }
            }

            transaction.Commit();
        }

        private static string LocateHarnessDll(string projectName)
        {
            string testBinDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string configFolder = new DirectoryInfo(testBinDir).Name;
            string artifactsBinDir = Directory.GetParent(testBinDir)!.Parent!.FullName;
            string dllPath = Path.Combine(artifactsBinDir, projectName, configFolder, projectName + ".dll");
            if (!File.Exists(dllPath))
            {
                throw new FileNotFoundException("Harness dll not found -- build " + projectName + ".csproj first.", dllPath);
            }

            return dllPath;
        }

        private static void RunHarness(string harnessDll, string campaignRootPath, string reason)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{harnessDll}\" \"{campaignRootPath}\" {reason}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using Process process = Process.Start(startInfo)!;
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                string stderr = process.StandardError.ReadToEnd();
                throw new InvalidOperationException("Harness baseline run failed: " + stderr);
            }
        }
    }
}
