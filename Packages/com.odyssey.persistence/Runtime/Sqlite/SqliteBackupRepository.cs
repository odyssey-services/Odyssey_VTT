using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Persistence.Sqlite
{
    /// <summary>
    /// ODY-S01-011: ADR-012 section 8 snapshot contract implementation. Every
    /// backup is created via the SQLite Backup API (never File.Copy on a live
    /// WAL database, section 8.4's explicit prohibition), through a
    /// temp-directory -> validate -> atomic-rename flow matching SP-02's
    /// empirically-proven pattern (docs/tasks/completed/ODY-S01-005_SP-02_
    /// Persistence_Reliability_Report.md section 2.3).
    ///
    /// Design decision: ListBackups/RestoreBackup read backup-manifest.json
    /// files directly from the Backups/ directory tree, not the campaign's own
    /// BackupRecords table -- a disaster-recovery listing method that depends on
    /// the very database a corrupted-database scenario would make unreadable
    /// defeats its own purpose (this is exactly the corruption-fixture scenario
    /// this task's tests exercise). BackupRecords is still written on every
    /// CreateBackup (ADR-012 section 8.4 step 8) as the in-app audit trail, but
    /// it is not the source of truth for finding backups to restore from.
    /// </summary>
    public sealed class SqliteBackupRepository : IBackupRepository
    {
        private const string ManifestFileName = "manifest.json";
        private const string DatabaseFileName = "campaign.db";
        private const string BackupManifestFileName = "backup-manifest.json";
        private const string FastTier = "Fast";
        private const string DailyTier = "Daily";
        private const string WeeklyTier = "Weekly";
        private static readonly string[] Tiers = { FastTier, DailyTier, WeeklyTier };

        private static readonly string[] DirectoryTree =
        {
            "Assets/Objects", "Assets/Staging", "Assets/Trash", "Assets/Quarantine",
            "Backups/Fast", "Backups/Daily", "Backups/Weekly", "Backups/Full", "Backups/Emergency",
            "Logs/Archive", "Logs/Diagnostics", "Logs/Migration",
            "Temp",
        };

        private readonly IWallClock _clock;
        private readonly BackupRotationPolicy _rotationPolicy;
        private readonly BackupManifestV1Codec _backupManifestCodec = new BackupManifestV1Codec();

        public SqliteBackupRepository(IWallClock clock, BackupRotationPolicy? rotationPolicy = null)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _rotationPolicy = rotationPolicy ?? BackupRotationPolicy.Default;
        }

        public Result<BackupRecord> CreateBackup(CampaignHandle campaign, string reason, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 96)
            {
                return Result<BackupRecord>.Failure(PersistenceFailures.BackupCreateFailed(correlationId));
            }

            BackupId backupId = BackupId.NewId(_clock.GetUtcNow());
            string fastTierDir = Path.Combine(campaign.RootPath, "Backups", FastTier);
            Directory.CreateDirectory(fastTierDir);
            string tempDir = Path.Combine(fastTierDir, ".tmp-" + backupId);

            try
            {
                Directory.CreateDirectory(tempDir);
                string tempDbPath = Path.Combine(tempDir, DatabaseFileName);
                string sourceDbPath = Path.Combine(campaign.RootPath, DatabaseFileName);

                // ADR-012 section 8.4 steps 1-5: allocate BackupId (above), copy via
                // the shared SqliteSnapshotCopy helper (SQLite Backup API, integrity
                // validation, hash/size) -- also used by SqliteExportRepository
                // (ODY-S01-012), so the database-copy path is not duplicated.
                SqliteSnapshotCopy.SnapshotInfo snapshot = SqliteSnapshotCopy.CreateValidated(sourceDbPath, tempDbPath);
                long campaignRevision = snapshot.CampaignRevision;
                long eventSequence = snapshot.EventSequence;
                string databaseHash = snapshot.DatabaseHash;
                long sizeBytes = snapshot.SizeBytes;

                File.Copy(Path.Combine(campaign.RootPath, ManifestFileName), Path.Combine(tempDir, ManifestFileName));

                UtcInstant now = _clock.GetUtcNow();
                string rulesetRef = campaign.Manifest.RulesetId + "@" + campaign.Manifest.RulesetVersion;

                // Step 6: write backup manifest.
                var backupManifest = new BackupManifest(
                    backupId, campaign.CampaignId, "Fast", reason, campaignRevision, eventSequence,
                    campaign.Manifest.DatabaseSchemaVersion, campaign.Manifest.CampaignFormatVersion, rulesetRef,
                    now, databaseHash, sizeBytes);
                Result<Application.Serialization.JsonPayload> manifestWrite = _backupManifestCodec.Write(backupManifest);
                if (manifestWrite.IsFailure)
                {
                    SafeDeleteDirectory(tempDir);
                    return Result<BackupRecord>.Failure(manifestWrite.Error);
                }

                File.WriteAllBytes(Path.Combine(tempDir, BackupManifestFileName), manifestWrite.Value.Bytes);

                // Step 7: atomic move to final backup path (Directory.Move is atomic
                // when source and destination are on the same volume, which they are
                // here -- both under the campaign's own Backups/Fast/ directory).
                string finalDir = Path.Combine(fastTierDir, backupId.ToString());
                Directory.Move(tempDir, finalDir);

                string relativePath = "Backups/" + FastTier + "/" + backupId;
                var record = new BackupRecord(
                    backupId, campaign.CampaignId, "Fast", reason, campaignRevision, eventSequence,
                    campaign.Manifest.DatabaseSchemaVersion, campaign.Manifest.CampaignFormatVersion, rulesetRef,
                    now, relativePath, databaseHash, sizeBytes, "Ok");

                // Step 8: persist BackupRecord in the campaign's own database.
                Result insertResult = InsertBackupRecordRow(campaign.RootPath, record);
                if (insertResult.IsFailure)
                {
                    return Result<BackupRecord>.Failure(insertResult.Error);
                }

                PromoteToCalendarTiers(campaign.RootPath, finalDir, backupId, now);
                PruneTier(campaign.RootPath, FastTier, _rotationPolicy.FastRetentionCount, deleteBackupRecordsRow: true);
                PruneTier(campaign.RootPath, DailyTier, _rotationPolicy.DailyRetentionCount, deleteBackupRecordsRow: false);
                PruneTier(campaign.RootPath, WeeklyTier, _rotationPolicy.WeeklyRetentionCount, deleteBackupRecordsRow: false);

                return Result<BackupRecord>.Success(record);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException || ex is SqliteSnapshotCopy.SnapshotIntegrityException)
            {
                SafeDeleteDirectory(tempDir);
                return Result<BackupRecord>.Failure(PersistenceFailures.BackupCreateFailed(correlationId));
            }
        }

        public Result<IReadOnlyList<BackupRecord>> ListBackups(string campaignFolderPath, CorrelationId correlationId)
        {
            if (string.IsNullOrWhiteSpace(campaignFolderPath)) throw new ArgumentException("Campaign folder path is required.", nameof(campaignFolderPath));
            string rootPath = Path.GetFullPath(campaignFolderPath);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var records = new List<BackupRecord>();

            try
            {
                foreach (string tier in Tiers)
                {
                    string tierDir = Path.Combine(rootPath, "Backups", tier);
                    if (!Directory.Exists(tierDir)) continue;

                    foreach (string backupDir in Directory.GetDirectories(tierDir))
                    {
                        string dirName = Path.GetFileName(backupDir);
                        if (dirName.StartsWith(".tmp-", StringComparison.Ordinal)) continue;
                        if (!seen.Add(dirName)) continue;

                        Result<BackupRecord> read = ReadBackupRecordFromManifest(rootPath, tier, backupDir);
                        if (read.IsSuccess)
                        {
                            records.Add(read.Value);
                        }
                    }
                }

                return Result<IReadOnlyList<BackupRecord>>.Success(
                    records.OrderByDescending(r => r.BackupId.ToString(), StringComparer.Ordinal).ToList());
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return Result<IReadOnlyList<BackupRecord>>.Failure(PersistenceFailures.BackupCreateFailed(correlationId));
            }
        }

        public Result<string> RestoreBackup(string campaignFolderPath, BackupId backupId, string destinationParentDirectory, CorrelationId correlationId)
        {
            if (string.IsNullOrWhiteSpace(campaignFolderPath)) throw new ArgumentException("Campaign folder path is required.", nameof(campaignFolderPath));
            if (!backupId.IsValid) throw new ArgumentException("BackupId is required.", nameof(backupId));
            if (string.IsNullOrWhiteSpace(destinationParentDirectory)) throw new ArgumentException("Destination parent directory is required.", nameof(destinationParentDirectory));

            string rootPath = Path.GetFullPath(campaignFolderPath);
            string? sourceBackupDir = null;
            foreach (string tier in Tiers)
            {
                string candidate = Path.Combine(rootPath, "Backups", tier, backupId.ToString());
                if (Directory.Exists(candidate))
                {
                    sourceBackupDir = candidate;
                    break;
                }
            }

            if (sourceBackupDir == null)
            {
                return Result<string>.Failure(PersistenceFailures.BackupNotFound(correlationId));
            }

            try
            {
                string sourceDbPath = Path.Combine(sourceBackupDir, DatabaseFileName);
                string sourceManifestPath = Path.Combine(sourceBackupDir, ManifestFileName);
                if (!File.Exists(sourceDbPath) || !File.Exists(sourceManifestPath))
                {
                    return Result<string>.Failure(PersistenceFailures.BackupNotFound(correlationId));
                }

                using (var verify = new SqliteConnection("Data Source=" + sourceDbPath + ";Mode=ReadOnly;Pooling=False"))
                {
                    verify.Open();
                    using var quickCheck = verify.CreateCommand();
                    quickCheck.CommandText = "PRAGMA quick_check;";
                    object? result = quickCheck.ExecuteScalar();
                    if (!(result is string status) || !string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
                    {
                        return Result<string>.Failure(PersistenceFailures.BackupRestoreFailed(correlationId));
                    }
                }

                // 05_Persistence restore-into-separate-copy principle: never write
                // into or overwrite the source campaign's own directory. A brand-new
                // directory (fresh UUIDv7-derived name) is always created here.
                string newRootPath = Path.Combine(Path.GetFullPath(destinationParentDirectory), "restored-" + backupId);
                if (Directory.Exists(newRootPath) && Directory.GetFileSystemEntries(newRootPath).Length > 0)
                {
                    return Result<string>.Failure(PersistenceFailures.BackupRestoreFailed(correlationId));
                }

                Directory.CreateDirectory(newRootPath);
                foreach (string relative in DirectoryTree)
                {
                    Directory.CreateDirectory(Path.Combine(newRootPath, relative.Replace('/', Path.DirectorySeparatorChar)));
                }

                File.Copy(sourceDbPath, Path.Combine(newRootPath, DatabaseFileName), overwrite: false);
                File.Copy(sourceManifestPath, Path.Combine(newRootPath, ManifestFileName), overwrite: false);

                return Result<string>.Success(newRootPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<string>.Failure(PersistenceFailures.BackupRestoreFailed(correlationId));
            }
        }

        private void PromoteToCalendarTiers(string campaignRootPath, string finalFastDir, BackupId backupId, UtcInstant now)
        {
            DateTimeOffset nowOffset = now.Value;
            PromoteIfMissing(campaignRootPath, finalFastDir, backupId, DailyTier, nowOffset.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                dir => ReadBackupManifestCreatedAt(dir).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

            int isoWeek = System.Globalization.ISOWeek.GetWeekOfYear(nowOffset.UtcDateTime);
            int isoYear = System.Globalization.ISOWeek.GetYear(nowOffset.UtcDateTime);
            string weekBucket = isoYear.ToString(CultureInfo.InvariantCulture) + "-W" + isoWeek.ToString("00", CultureInfo.InvariantCulture);
            PromoteIfMissing(campaignRootPath, finalFastDir, backupId, WeeklyTier, weekBucket,
                dir =>
                {
                    DateTimeOffset createdAt = ReadBackupManifestCreatedAt(dir);
                    int week = System.Globalization.ISOWeek.GetWeekOfYear(createdAt.UtcDateTime);
                    int year = System.Globalization.ISOWeek.GetYear(createdAt.UtcDateTime);
                    return year.ToString(CultureInfo.InvariantCulture) + "-W" + week.ToString("00", CultureInfo.InvariantCulture);
                });
        }

        private static void PromoteIfMissing(string campaignRootPath, string finalFastDir, BackupId backupId, string tier, string currentBucket, Func<string, string> bucketOf)
        {
            string tierDir = Path.Combine(campaignRootPath, "Backups", tier);
            Directory.CreateDirectory(tierDir);

            foreach (string existingDir in Directory.GetDirectories(tierDir))
            {
                if (Path.GetFileName(existingDir).StartsWith(".tmp-", StringComparison.Ordinal)) continue;
                if (!File.Exists(Path.Combine(existingDir, BackupManifestFileName))) continue;
                if (string.Equals(bucketOf(existingDir), currentBucket, StringComparison.Ordinal))
                {
                    return; // this calendar bucket already has a retained copy
                }
            }

            string destinationDir = Path.Combine(tierDir, backupId.ToString());
            if (Directory.Exists(destinationDir)) return;
            CopyDirectory(finalFastDir, destinationDir);
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(destinationDir, Path.GetFileName(file)), overwrite: false);
            }
        }

        private void PruneTier(string campaignRootPath, string tier, int retentionCount, bool deleteBackupRecordsRow)
        {
            string tierDir = Path.Combine(campaignRootPath, "Backups", tier);
            if (!Directory.Exists(tierDir)) return;

            List<string> backupDirs = Directory.GetDirectories(tierDir)
                .Where(d => !Path.GetFileName(d).StartsWith(".tmp-", StringComparison.Ordinal))
                .OrderByDescending(d => Path.GetFileName(d), StringComparer.Ordinal)
                .ToList();

            for (int index = retentionCount; index < backupDirs.Count; index++)
            {
                string toDelete = backupDirs[index];
                string idText = Path.GetFileName(toDelete);
                SafeDeleteDirectory(toDelete);

                if (deleteBackupRecordsRow && BackupId.TryParse(idText, out BackupId id))
                {
                    DeleteBackupRecordRow(campaignRootPath, id);
                }
            }
        }

        private static Result<BackupRecord> ReadBackupRecordFromManifest(string campaignRootPath, string tier, string backupDir)
        {
            string manifestPath = Path.Combine(backupDir, BackupManifestFileName);
            if (!File.Exists(manifestPath))
            {
                return Result<BackupRecord>.Failure(PersistenceFailures.ManifestInvalid());
            }

            var codec = new BackupManifestV1Codec();
            Result<BackupManifest> read = codec.Read(File.ReadAllBytes(manifestPath));
            if (read.IsFailure)
            {
                return Result<BackupRecord>.Failure(read.Error);
            }

            BackupManifest manifest = read.Value;
            string relativePath = "Backups/" + tier + "/" + manifest.BackupId;
            var record = new BackupRecord(
                manifest.BackupId, manifest.CampaignId, manifest.BackupKind, manifest.Reason,
                manifest.CampaignRevision, manifest.EventSequence, manifest.DatabaseSchemaVersion,
                manifest.CampaignFormatVersion, manifest.RulesetRef, manifest.CreatedAt, relativePath,
                manifest.DatabaseHash, manifest.SizeBytes, "Ok");
            return Result<BackupRecord>.Success(record);
        }

        private static DateTimeOffset ReadBackupManifestCreatedAt(string backupDir)
        {
            var codec = new BackupManifestV1Codec();
            Result<BackupManifest> read = codec.Read(File.ReadAllBytes(Path.Combine(backupDir, BackupManifestFileName)));
            return read.Value.CreatedAt.Value;
        }

        private static Result InsertBackupRecordRow(string campaignRootPath, BackupRecord record)
        {
            string dbPath = Path.Combine(campaignRootPath, DatabaseFileName);
            try
            {
                using var connection = new SqliteConnection("Data Source=" + dbPath + ";Pooling=False");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText =
                    "INSERT INTO BackupRecords (BackupId, CampaignId, BackupKind, Reason, CampaignRevision, EventSequence, DatabaseSchemaVersion, CampaignFormatVersion, RulesetRef, CreatedAt, CreatedByUserId, RelativePath, DatabaseHash, AssetsManifestHash, SizeBytes, IntegrityStatus, SourceOperationId) " +
                    "VALUES ($backupId, $campaignId, $backupKind, $reason, $campaignRevision, $eventSequence, $dbSchemaVersion, $campaignFormatVersion, $rulesetRef, $createdAt, NULL, $relativePath, $databaseHash, NULL, $sizeBytes, $integrityStatus, NULL);";
                command.Parameters.AddWithValue("$backupId", record.BackupId.ToString());
                command.Parameters.AddWithValue("$campaignId", record.CampaignId.ToString());
                command.Parameters.AddWithValue("$backupKind", record.BackupKind);
                command.Parameters.AddWithValue("$reason", record.Reason);
                command.Parameters.AddWithValue("$campaignRevision", record.CampaignRevision);
                command.Parameters.AddWithValue("$eventSequence", record.EventSequence);
                command.Parameters.AddWithValue("$dbSchemaVersion", record.DatabaseSchemaVersion);
                command.Parameters.AddWithValue("$campaignFormatVersion", record.CampaignFormatVersion);
                command.Parameters.AddWithValue("$rulesetRef", record.RulesetRef);
                command.Parameters.AddWithValue("$createdAt", record.CreatedAt.ToString());
                command.Parameters.AddWithValue("$relativePath", record.RelativePath);
                command.Parameters.AddWithValue("$databaseHash", record.DatabaseHash);
                command.Parameters.AddWithValue("$sizeBytes", record.SizeBytes);
                command.Parameters.AddWithValue("$integrityStatus", record.IntegrityStatus);
                command.ExecuteNonQuery();
                return Result.Success();
            }
            catch (Exception ex) when (ex is IOException || ex is SqliteException)
            {
                return Result.Failure(PersistenceFailures.BackupCreateFailed(CorrelationId.Parse("corr_00000000000000000000000000000000")));
            }
        }

        private static void DeleteBackupRecordRow(string campaignRootPath, BackupId backupId)
        {
            string dbPath = Path.Combine(campaignRootPath, DatabaseFileName);
            try
            {
                using var connection = new SqliteConnection("Data Source=" + dbPath + ";Pooling=False");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM BackupRecords WHERE BackupId = $id;";
                command.Parameters.AddWithValue("$id", backupId.ToString());
                command.ExecuteNonQuery();
            }
            catch (Exception ex) when (ex is IOException || ex is SqliteException)
            {
                // Best-effort: the physical backup directory is already gone by the
                // time this runs (PruneTier deletes the directory first), so a
                // failure here only leaves a stale audit-trail row behind, not a
                // dangling file. ListBackups/RestoreBackup never read this table.
            }
        }

        private static void SafeDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
