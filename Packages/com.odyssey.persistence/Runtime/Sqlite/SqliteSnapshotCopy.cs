using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Odyssey.Persistence.Sqlite
{
    /// <summary>
    /// ODY-S01-011/012: the one shared "produce a validated, consistent SQLite
    /// snapshot" primitive both <see cref="SqliteBackupRepository"/> (ODY-S01-011)
    /// and <see cref="SqliteExportRepository"/> (ODY-S01-012) build on -- ADR-012
    /// section 8.4's steps 2-5 (SQLite Backup API copy, open read-only, integrity
    /// validation, compute hash/size), extracted here so export does not
    /// reimplement its own parallel database-copy path. Both callers still own
    /// their own temp-directory/atomic-rename/manifest steps, which differ
    /// (backup writes into a rotation tier; export writes into an archive
    /// staging directory) -- only the database-copy-and-validate core is shared.
    /// </summary>
    internal static class SqliteSnapshotCopy
    {
        internal readonly struct SnapshotInfo
        {
            internal SnapshotInfo(long campaignRevision, long eventSequence, string databaseHash, long sizeBytes)
            {
                CampaignRevision = campaignRevision;
                EventSequence = eventSequence;
                DatabaseHash = databaseHash;
                SizeBytes = sizeBytes;
            }

            internal long CampaignRevision { get; }
            internal long EventSequence { get; }
            internal string DatabaseHash { get; }
            internal long SizeBytes { get; }
        }

        /// <summary>
        /// Thrown when the copy fails ADR-012 section 8.4 step 4's integrity
        /// validation. Callers add this to their existing IOException/
        /// SqliteException catch filters and map it to their own typed
        /// PersistenceFailures error (BackupCreateFailed vs ExportCreateFailed) --
        /// this class deliberately does not know which caller's error to build.
        /// </summary>
        internal sealed class SnapshotIntegrityException : InvalidOperationException
        {
        }

        internal static SnapshotInfo CreateValidated(string sourceDbPath, string destinationDbPath)
        {
            // ADR-012 section 8.4 steps 1-2 (BackupId is the caller's concern) --
            // copy via the SQLite Backup API, never a raw file copy of a live WAL
            // database (section 8.4's explicit prohibition).
            using (var source = new SqliteConnection("Data Source=" + sourceDbPath + ";Mode=ReadOnly;Pooling=False"))
            using (var destination = new SqliteConnection("Data Source=" + destinationDbPath + ";Pooling=False"))
            {
                source.Open();
                destination.Open();
                source.BackupDatabase(destination);
            }

            // Steps 3-4: open the copy read-only, run integrity validation.
            long campaignRevision;
            long eventSequence;
            using (var verify = new SqliteConnection("Data Source=" + destinationDbPath + ";Mode=ReadOnly;Pooling=False"))
            {
                verify.Open();
                using (var quickCheck = verify.CreateCommand())
                {
                    quickCheck.CommandText = "PRAGMA quick_check;";
                    object? result = quickCheck.ExecuteScalar();
                    if (!(result is string status) || !string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new SnapshotIntegrityException();
                    }
                }

                using (var revisionCmd = verify.CreateCommand())
                {
                    revisionCmd.CommandText = "SELECT Revision FROM Campaign LIMIT 1;";
                    object? revisionResult = revisionCmd.ExecuteScalar();
                    campaignRevision = revisionResult == null ? 0L : Convert.ToInt64(revisionResult, CultureInfo.InvariantCulture);
                }

                using (var sequenceCmd = verify.CreateCommand())
                {
                    sequenceCmd.CommandText = "SELECT COALESCE(MAX(EventSequence), 0) FROM DomainEvents;";
                    eventSequence = Convert.ToInt64(sequenceCmd.ExecuteScalar(), CultureInfo.InvariantCulture);
                }
            }

            // Step 5: compute hash and size.
            byte[] dbBytes = File.ReadAllBytes(destinationDbPath);
            string databaseHash = ComputeSha256Hex(dbBytes);
            return new SnapshotInfo(campaignRevision, eventSequence, databaseHash, dbBytes.LongLength);
        }

        internal static string ComputeSha256Hex(byte[] bytes)
        {
            using var sha = SHA256.Create();
            byte[] hashBytes = sha.ComputeHash(bytes);
            var builder = new StringBuilder(hashBytes.Length * 2);
            for (int index = 0; index < hashBytes.Length; index++)
            {
                builder.Append(hashBytes[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
