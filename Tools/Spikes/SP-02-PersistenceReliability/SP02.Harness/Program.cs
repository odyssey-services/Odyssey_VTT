using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace SP02.Harness;

// SP-02 Persistence Reliability spike harness.
// Evidence-only tool for ODY-S01-005. Not production code. Not referenced by
// any Core/Persistence/Unity module. See README.md in this directory.
internal static class Program
{
    private const string PragmaProfile = @"
PRAGMA journal_mode = WAL;
PRAGMA foreign_keys = ON;
PRAGMA synchronous = FULL;
PRAGMA busy_timeout = 5000;
";

    private static int Main(string[] args)
    {
        if (args.Length >= 1 && args[0] == "crash-child")
        {
            return CrashChild.Run(args[1], int.Parse(args[2]));
        }

        if (args.Length >= 1 && args[0] == "backup-child")
        {
            return BackupChild.Run(args[1], args[2]);
        }

        string workDir = Path.Combine(Path.GetTempPath(), "sp02-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        Console.WriteLine($"# SP-02 Persistence Reliability — evidence run");
        Console.WriteLine($"# Work directory: {workDir}");
        Console.WriteLine($"# Started (UTC): {DateTime.UtcNow:O}");
        Console.WriteLine();

        try
        {
            Scenario1_WalTransactionMode.Run(workDir);
            Scenario2_CrashRecovery.Run(workDir);
            Scenario3_InterruptedBackup.Run(workDir);
            Scenario4_MigrationFailureRollback.Run(workDir);
            Scenario5_SnapshotSizeSpeed.Run(workDir);
            Scenario6_CorruptedDatabaseRecovery.Run(workDir);
        }
        finally
        {
            Console.WriteLine();
            Console.WriteLine($"# Finished (UTC): {DateTime.UtcNow:O}");
            Console.WriteLine($"# Work directory retained at: {workDir}");
        }

        return 0;
    }

    internal static void ApplyPragmaProfile(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = PragmaProfile;
        cmd.ExecuteNonQuery();
    }

    internal static string QueryScalarString(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        var result = cmd.ExecuteScalar();
        return result?.ToString() ?? string.Empty;
    }

    internal static long QueryScalarLong(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        var result = cmd.ExecuteScalar();
        return Convert.ToInt64(result);
    }

    internal static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    internal static string IntegrityCheck(string dbPath)
    {
        using var connection = new SqliteConnection($"Pooling=False;Data Source={dbPath};Mode=ReadOnly");
        connection.Open();
        return QueryScalarString(connection, "PRAGMA integrity_check;");
    }
}

// ---------------------------------------------------------------------------
// Scenario 1 — SQLite WAL/transaction mode under ADR-011 section 7 profile
// ---------------------------------------------------------------------------
internal static class Scenario1_WalTransactionMode
{
    public static void Run(string workDir)
    {
        Console.WriteLine("## Scenario 1 — WAL/transaction mode");
        string dbPath = Path.Combine(workDir, "s1-wal.db");

        using (var connection = new SqliteConnection($"Pooling=False;Data Source={dbPath}"))
        {
            connection.Open();
            Program.ApplyPragmaProfile(connection);

            string journalMode = Program.QueryScalarString(connection, "PRAGMA journal_mode;");
            string foreignKeys = Program.QueryScalarString(connection, "PRAGMA foreign_keys;");
            string synchronous = Program.QueryScalarString(connection, "PRAGMA synchronous;");
            string busyTimeout = Program.QueryScalarString(connection, "PRAGMA busy_timeout;");
            Console.WriteLine($"journal_mode={journalMode} foreign_keys={foreignKeys} synchronous={synchronous} busy_timeout={busyTimeout}");

            using (var create = connection.CreateCommand())
            {
                create.CommandText = "CREATE TABLE Rows (Id INTEGER PRIMARY KEY, Payload TEXT NOT NULL);";
                create.ExecuteNonQuery();
            }

            const int transactionCount = 100;
            const int insertsPerTransaction = 10;
            var sw = Stopwatch.StartNew();
            int nextId = 1;
            for (int t = 0; t < transactionCount; t++)
            {
                using var tx = connection.BeginTransaction();
                for (int i = 0; i < insertsPerTransaction; i++)
                {
                    using var insert = connection.CreateCommand();
                    insert.Transaction = tx;
                    insert.CommandText = "INSERT INTO Rows (Id, Payload) VALUES ($id, $payload);";
                    insert.Parameters.AddWithValue("$id", nextId);
                    insert.Parameters.AddWithValue("$payload", "row-" + nextId);
                    insert.ExecuteNonQuery();
                    nextId++;
                }
                tx.Commit();
            }
            sw.Stop();

            long total = Program.QueryScalarLong(connection, "SELECT COUNT(*) FROM Rows;");
            double txPerSec = transactionCount / sw.Elapsed.TotalSeconds;
            double rowsPerSec = (transactionCount * insertsPerTransaction) / sw.Elapsed.TotalSeconds;
            Console.WriteLine($"committed_transactions={transactionCount} rows_written={total} elapsed_ms={sw.ElapsedMilliseconds} tx_per_sec={txPerSec:F1} rows_per_sec={rowsPerSec:F1}");

            // WAL readers-not-blocked check: open a second read-only connection
            // while the writer holds an open (uncommitted) transaction.
            using var writerTx = connection.BeginTransaction();
            using (var pendingInsert = connection.CreateCommand())
            {
                pendingInsert.Transaction = writerTx;
                pendingInsert.CommandText = "INSERT INTO Rows (Id, Payload) VALUES ($id, $payload);";
                pendingInsert.Parameters.AddWithValue("$id", nextId);
                pendingInsert.Parameters.AddWithValue("$payload", "uncommitted");
                pendingInsert.ExecuteNonQuery();
            }

            var readSw = Stopwatch.StartNew();
            using (var reader = new SqliteConnection($"Pooling=False;Data Source={dbPath};Mode=ReadOnly"))
            {
                reader.Open();
                long readerCount = Program.QueryScalarLong(reader, "SELECT COUNT(*) FROM Rows;");
                readSw.Stop();
                Console.WriteLine($"reader_visible_rows_during_open_writer_tx={readerCount} (expected {total}, uncommitted row must not be visible) read_elapsed_ms={readSw.ElapsedMilliseconds}");
            }
            writerTx.Rollback();
        }

        Console.WriteLine();
    }
}

// ---------------------------------------------------------------------------
// Scenario 2 — crash during a critical operation
// ---------------------------------------------------------------------------
internal static class Scenario2_CrashRecovery
{
    private const int Iterations = 5;
    private const int TargetCommitsBeforeKill = 5;
    private const int SleepInsideTransactionMs = 150;

    public static void Run(string workDir)
    {
        Console.WriteLine("## Scenario 2 — crash during a critical operation");
        string exePath = Environment.ProcessPath ?? throw new InvalidOperationException("ProcessPath unavailable");

        int successCount = 0;
        double totalRecoveryMs = 0;

        for (int iter = 1; iter <= Iterations; iter++)
        {
            string dbPath = Path.Combine(workDir, $"s2-crash-{iter}.db");
            if (File.Exists(dbPath)) File.Delete(dbPath);

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("crash-child");
            psi.ArgumentList.Add(dbPath);
            psi.ArgumentList.Add((TargetCommitsBeforeKill + 3).ToString());

            using var process = Process.Start(psi)!;
            int lastSeenCommit = 0;
            bool killedMidTransaction = false;

            while (!process.HasExited)
            {
                string? line = process.StandardOutput.ReadLine();
                if (line is null) break;
                if (line.StartsWith("COMMITTED "))
                {
                    lastSeenCommit = int.Parse(line.Substring("COMMITTED ".Length));
                    if (lastSeenCommit == TargetCommitsBeforeKill)
                    {
                        // Kill mid-way through the NEXT transaction's simulated
                        // work window, i.e. after BEGIN but before COMMIT.
                        System.Threading.Thread.Sleep(SleepInsideTransactionMs / 2);
                        try { process.Kill(entireProcessTree: true); } catch { /* already exited */ }
                        killedMidTransaction = true;
                        break;
                    }
                }
            }

            process.WaitForExit(5000);

            var sw = Stopwatch.StartNew();
            string integrity;
            long rowCount;
            long maxId;
            bool opened = true;
            try
            {
                using var connection = new SqliteConnection($"Pooling=False;Data Source={dbPath}");
                connection.Open();
                integrity = Program.QueryScalarString(connection, "PRAGMA integrity_check;");
                rowCount = Program.QueryScalarLong(connection, "SELECT COUNT(*) FROM Rows;");
                maxId = Program.QueryScalarLong(connection, "SELECT IFNULL(MAX(Id), 0) FROM Rows;");
            }
            catch (Exception ex)
            {
                opened = false;
                integrity = "OPEN_FAILED: " + ex.Message;
                rowCount = -1;
                maxId = -1;
            }
            sw.Stop();

            bool pass = opened && integrity == "ok" && rowCount == TargetCommitsBeforeKill && maxId == TargetCommitsBeforeKill;
            if (pass) { successCount++; totalRecoveryMs += sw.Elapsed.TotalMilliseconds; }

            Console.WriteLine($"iter={iter} killed_mid_transaction={killedMidTransaction} last_seen_commit={lastSeenCommit} " +
                               $"reopen_integrity_check={integrity} rows_after_recovery={rowCount} max_id={maxId} " +
                               $"recovery_open_elapsed_ms={sw.Elapsed.TotalMilliseconds:F1} PASS={pass}");
        }

        double avgRecoveryMs = successCount > 0 ? totalRecoveryMs / successCount : -1;
        Console.WriteLine($"SUMMARY: {successCount}/{Iterations} crash-recovery runs recovered with exactly {TargetCommitsBeforeKill} committed rows and no partial/uncommitted row visible. avg_recovery_open_ms={avgRecoveryMs:F1}");
        Console.WriteLine();
    }
}

internal static class CrashChild
{
    public static int Run(string dbPath, int totalTransactions)
    {
        using var connection = new SqliteConnection($"Pooling=False;Data Source={dbPath}");
        connection.Open();
        Program.ApplyPragmaProfile(connection);
        using (var create = connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE Rows (Id INTEGER PRIMARY KEY, Payload TEXT NOT NULL);";
            create.ExecuteNonQuery();
        }

        for (int i = 1; i <= totalTransactions; i++)
        {
            using var tx = connection.BeginTransaction();
            using (var insert = connection.CreateCommand())
            {
                insert.Transaction = tx;
                insert.CommandText = "INSERT INTO Rows (Id, Payload) VALUES ($id, $payload);";
                insert.Parameters.AddWithValue("$id", i);
                insert.Parameters.AddWithValue("$payload", "row-" + i);
                insert.ExecuteNonQuery();
            }
            // Simulate in-flight critical-operation work before commit, so the
            // parent process has a reliable window in which to kill this
            // process mid-transaction.
            System.Threading.Thread.Sleep(150);
            tx.Commit();
            Console.WriteLine($"COMMITTED {i}");
            Console.Out.Flush();
        }

        return 0;
    }
}

// ---------------------------------------------------------------------------
// Scenario 3 — interrupted backup
// ---------------------------------------------------------------------------
internal static class Scenario3_InterruptedBackup
{
    public static void Run(string workDir)
    {
        Console.WriteLine("## Scenario 3 — interrupted backup");
        string exePath = Environment.ProcessPath ?? throw new InvalidOperationException("ProcessPath unavailable");
        string sourceDb = Path.Combine(workDir, "s3-source.db");
        BuildSourceDatabase(sourceDb, targetRowCount: 200_000);
        string sourceHashBefore = Program.Sha256File(sourceDb);
        long sourceSizeBytes = new FileInfo(sourceDb).Length;
        Console.WriteLine($"source_size_bytes={sourceSizeBytes} source_sha256={sourceHashBefore}");

        // Baseline: measure a full, uninterrupted backup duration first.
        string baselineTemp = Path.Combine(workDir, "s3-baseline.tmp");
        string baselineFinal = Path.Combine(workDir, "s3-baseline.confirmed.db");
        var baselineSw = Stopwatch.StartNew();
        RunBackupChildToCompletion(exePath, sourceDb, baselineTemp);
        baselineSw.Stop();
        PromoteIfIntegrityOk(baselineTemp, baselineFinal, out string baselineIntegrity);
        Console.WriteLine($"baseline_full_backup_elapsed_ms={baselineSw.ElapsedMilliseconds} baseline_confirmed_backup_integrity={baselineIntegrity} baseline_promoted={File.Exists(baselineFinal)}");

        // Interrupted run: kill the backup child partway through the measured
        // baseline duration, well before it can finish.
        string interruptedTemp = Path.Combine(workDir, "s3-interrupted.tmp");
        string interruptedFinal = Path.Combine(workDir, "s3-interrupted.confirmed.db");
        int killAfterMs = Math.Max(5, (int)(baselineSw.ElapsedMilliseconds * 0.3));

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("backup-child");
        psi.ArgumentList.Add(sourceDb);
        psi.ArgumentList.Add(interruptedTemp);

        using (var process = Process.Start(psi)!)
        {
            bool completedBeforeKill = process.WaitForExit(killAfterMs);
            bool killed = false;
            if (!completedBeforeKill)
            {
                try { process.Kill(entireProcessTree: true); killed = true; } catch { /* already exited */ }
                process.WaitForExit(5000);
            }
            Console.WriteLine($"kill_after_ms={killAfterMs} completed_before_kill_window={completedBeforeKill} process_was_killed={killed}");
        }

        bool tempExistsAfterKill = File.Exists(interruptedTemp);
        PromoteIfIntegrityOk(interruptedTemp, interruptedFinal, out string interruptedIntegrity);
        bool confirmedBackupCreated = File.Exists(interruptedFinal);
        string sourceHashAfter = Program.Sha256File(sourceDb);

        Console.WriteLine($"interrupted_temp_file_present={tempExistsAfterKill} interrupted_temp_integrity_check={interruptedIntegrity} " +
                           $"interrupted_confirmed_backup_created={confirmedBackupCreated} (expected False — partial backup must never be promoted) " +
                           $"source_unchanged={(sourceHashAfter == sourceHashBefore)}");

        bool pass = !confirmedBackupCreated && sourceHashAfter == sourceHashBefore;
        Console.WriteLine($"SUMMARY: interrupted backup never promoted to a confirmed/valid backup path (temp->validate->atomic-move pattern held): {pass}. Source database left untouched by the backup attempt: {sourceHashAfter == sourceHashBefore}.");
        Console.WriteLine();
    }

    private static void RunBackupChildToCompletion(string exePath, string sourceDb, string destPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("backup-child");
        psi.ArgumentList.Add(sourceDb);
        psi.ArgumentList.Add(destPath);
        using var process = Process.Start(psi)!;
        process.WaitForExit();
    }

    private static void PromoteIfIntegrityOk(string tempPath, string finalPath, out string integrity)
    {
        integrity = "N/A";
        if (!File.Exists(tempPath))
        {
            return;
        }
        try
        {
            integrity = Program.IntegrityCheck(tempPath);
        }
        catch (Exception ex)
        {
            integrity = "CHECK_FAILED: " + ex.Message;
            return;
        }
        if (integrity == "ok")
        {
            File.Move(tempPath, finalPath, overwrite: true);
        }
    }

    private static void BuildSourceDatabase(string path, int targetRowCount)
    {
        if (File.Exists(path)) File.Delete(path);
        using var connection = new SqliteConnection($"Pooling=False;Data Source={path}");
        connection.Open();
        Program.ApplyPragmaProfile(connection);
        using (var create = connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE DomainEvents (EventSequence INTEGER PRIMARY KEY, PayloadJson TEXT NOT NULL);";
            create.ExecuteNonQuery();
        }

        string payload = new string('x', 400); // approximate a realistic JSON event payload size
        const int batchSize = 2000;
        int written = 0;
        while (written < targetRowCount)
        {
            using var tx = connection.BeginTransaction();
            int batchEnd = Math.Min(written + batchSize, targetRowCount);
            for (int i = written; i < batchEnd; i++)
            {
                using var insert = connection.CreateCommand();
                insert.Transaction = tx;
                insert.CommandText = "INSERT INTO DomainEvents (EventSequence, PayloadJson) VALUES ($seq, $payload);";
                insert.Parameters.AddWithValue("$seq", i + 1);
                insert.Parameters.AddWithValue("$payload", payload);
                insert.ExecuteNonQuery();
            }
            tx.Commit();
            written = batchEnd;
        }
    }
}

internal static class BackupChild
{
    public static int Run(string sourcePath, string destPath)
    {
        using var source = new SqliteConnection($"Pooling=False;Data Source={sourcePath};Mode=ReadOnly");
        source.Open();
        using var dest = new SqliteConnection($"Pooling=False;Data Source={destPath}");
        dest.Open();
        source.BackupDatabase(dest);
        Console.WriteLine("BACKUP COMPLETE");
        return 0;
    }
}

// ---------------------------------------------------------------------------
// Scenario 4 — migration failure and rollback (temp-copy pattern, ADR-013 §7)
// ---------------------------------------------------------------------------
internal static class Scenario4_MigrationFailureRollback
{
    public static void Run(string workDir)
    {
        Console.WriteLine("## Scenario 4 — migration failure and rollback");
        string workingDb = Path.Combine(workDir, "s4-working.db");
        string snapshotDb = Path.Combine(workDir, "s4-pre-migration-snapshot.db");
        string tempCopyDb = Path.Combine(workDir, "s4-migration-temp-copy.db");

        BuildWorkingDatabase(workingDb);
        string workingHashBefore = Program.Sha256File(workingDb);

        // Pre-migration snapshot via SQLite Backup API, per ADR-012 §8.2 trigger 5.
        using (var src = new SqliteConnection($"Pooling=False;Data Source={workingDb};Mode=ReadOnly"))
        using (var dst = new SqliteConnection($"Pooling=False;Data Source={snapshotDb}"))
        {
            src.Open();
            dst.Open();
            src.BackupDatabase(dst);
        }
        string snapshotIntegrity = Program.IntegrityCheck(snapshotDb);
        Console.WriteLine($"pre_migration_snapshot_created=True snapshot_integrity_check={snapshotIntegrity}");

        // Migration temp copy, per ADR-013 §7.1 (migration never touches the
        // working database directly).
        using (var src = new SqliteConnection($"Pooling=False;Data Source={workingDb};Mode=ReadOnly"))
        using (var dst = new SqliteConnection($"Pooling=False;Data Source={tempCopyDb}"))
        {
            src.Open();
            dst.Open();
            src.BackupDatabase(dst);
        }

        bool migrationSucceeded = true;
        string? failureMessage = null;
        using (var connection = new SqliteConnection($"Pooling=False;Data Source={tempCopyDb}"))
        {
            connection.Open();
            try
            {
                using (var step1 = connection.CreateCommand())
                {
                    step1.CommandText = "ALTER TABLE Items ADD COLUMN Note TEXT;"; // 0001-style migration, succeeds
                    step1.ExecuteNonQuery();
                }
                Console.WriteLine("migration_step_1 (0001_AddItemNote) applied to temp copy: success");

                using (var step2 = connection.CreateCommand())
                {
                    step2.CommandText = "ALTER TABLE ThisTableDoesNotExist ADD COLUMN X TEXT;"; // 0002-style migration, deliberately invalid
                    step2.ExecuteNonQuery();
                }
                Console.WriteLine("migration_step_2 (0002_intentionally_invalid) applied to temp copy: success (unexpected)");
            }
            catch (SqliteException ex)
            {
                migrationSucceeded = false;
                failureMessage = ex.Message;
            }
        }

        Console.WriteLine($"migration_chain_succeeded={migrationSucceeded} failure_message=\"{failureMessage}\"");

        // Normative rule under test: on failure, the working database is
        // NEVER replaced by the temp copy, and the temp copy is discarded.
        if (!migrationSucceeded && File.Exists(tempCopyDb))
        {
            File.Delete(tempCopyDb);
        }

        string workingHashAfter = Program.Sha256File(workingDb);
        bool workingDbUntouched = workingHashBefore == workingHashAfter;
        bool tempCopyDiscarded = !File.Exists(tempCopyDb);
        string snapshotIntegrityAfter = Program.IntegrityCheck(snapshotDb);
        long snapshotColumnCountAfter = CountColumns(snapshotDb, "Items");
        long workingColumnCountAfter = CountColumns(workingDb, "Items");

        Console.WriteLine($"working_db_hash_before={workingHashBefore}");
        Console.WriteLine($"working_db_hash_after ={workingHashAfter}");
        Console.WriteLine($"working_db_untouched_by_failed_migration={workingDbUntouched} temp_copy_discarded={tempCopyDiscarded}");
        Console.WriteLine($"pre_migration_snapshot_still_valid_after_failure={(snapshotIntegrityAfter == "ok")} snapshot_Items_column_count={snapshotColumnCountAfter} (must equal pre-migration original, not include Note)");
        Console.WriteLine($"working_db_Items_column_count_after_failed_attempt={workingColumnCountAfter} (must be unchanged, not include Note)");

        bool pass = !migrationSucceeded && workingDbUntouched && tempCopyDiscarded && snapshotIntegrityAfter == "ok";
        Console.WriteLine($"SUMMARY: migration failure correctly rolled back per ADR-013 section 7 temp-copy pattern: {pass}");
        Console.WriteLine();
    }

    private static long CountColumns(string dbPath, string table)
    {
        using var connection = new SqliteConnection($"Pooling=False;Data Source={dbPath};Mode=ReadOnly");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}');";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static void BuildWorkingDatabase(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        using var connection = new SqliteConnection($"Pooling=False;Data Source={path}");
        connection.Open();
        Program.ApplyPragmaProfile(connection);
        using var create = connection.CreateCommand();
        create.CommandText = "CREATE TABLE Items (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL); INSERT INTO Items (Id, Name) VALUES (1, 'Torch'), (2, 'Rope');";
        create.ExecuteNonQuery();
    }
}

// ---------------------------------------------------------------------------
// Scenario 5 — snapshot size and speed
// ---------------------------------------------------------------------------
internal static class Scenario5_SnapshotSizeSpeed
{
    // MVP campaign volume assumption (no authoritative figure found in
    // 05_Persistence / 02_MVP_Scope): bracket around a "several-session,
    // several-thousand-event" local campaign using the roadmap's own
    // "200 active tokens per scene" scale reference as an order-of-magnitude
    // anchor, not an exact figure. See report for this explicit assumption.
    private static readonly int[] EventCounts = { 5_000, 50_000, 250_000 };

    public static void Run(string workDir)
    {
        Console.WriteLine("## Scenario 5 — snapshot size and speed");
        foreach (int eventCount in EventCounts)
        {
            string dbPath = Path.Combine(workDir, $"s5-{eventCount}.db");
            BuildDatabase(dbPath, eventCount);
            long sourceSize = new FileInfo(dbPath).Length;

            string backupPath = Path.Combine(workDir, $"s5-{eventCount}.backup.db");
            var sw = Stopwatch.StartNew();
            using (var src = new SqliteConnection($"Pooling=False;Data Source={dbPath};Mode=ReadOnly"))
            using (var dst = new SqliteConnection($"Pooling=False;Data Source={backupPath}"))
            {
                src.Open();
                dst.Open();
                src.BackupDatabase(dst);
            }
            sw.Stop();

            long backupSize = new FileInfo(backupPath).Length;
            string integrity = Program.IntegrityCheck(backupPath);
            double mbPerSec = (sourceSize / (1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds;

            Console.WriteLine($"event_count={eventCount} source_size_bytes={sourceSize} ({sourceSize / (1024.0 * 1024.0):F1} MB) " +
                               $"backup_size_bytes={backupSize} backup_elapsed_ms={sw.ElapsedMilliseconds} " +
                               $"throughput_MBps={mbPerSec:F1} backup_integrity_check={integrity}");
        }
        Console.WriteLine();
    }

    private static void BuildDatabase(string path, int eventCount)
    {
        if (File.Exists(path)) File.Delete(path);
        using var connection = new SqliteConnection($"Pooling=False;Data Source={path}");
        connection.Open();
        Program.ApplyPragmaProfile(connection);
        using (var create = connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE DomainEvents (EventSequence INTEGER PRIMARY KEY, PayloadJson TEXT NOT NULL);";
            create.ExecuteNonQuery();
        }

        string payload = new string('e', 400);
        const int batchSize = 2000;
        int written = 0;
        while (written < eventCount)
        {
            using var tx = connection.BeginTransaction();
            int batchEnd = Math.Min(written + batchSize, eventCount);
            for (int i = written; i < batchEnd; i++)
            {
                using var insert = connection.CreateCommand();
                insert.Transaction = tx;
                insert.CommandText = "INSERT INTO DomainEvents (EventSequence, PayloadJson) VALUES ($seq, $payload);";
                insert.Parameters.AddWithValue("$seq", i + 1);
                insert.Parameters.AddWithValue("$payload", payload);
                insert.ExecuteNonQuery();
            }
            tx.Commit();
            written = batchEnd;
        }

        using (var checkpoint = connection.CreateCommand())
        {
            checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            checkpoint.ExecuteNonQuery();
        }
    }
}

// ---------------------------------------------------------------------------
// Scenario 6 — corrupted main database recovery
// ---------------------------------------------------------------------------
internal static class Scenario6_CorruptedDatabaseRecovery
{
    public static void Run(string workDir)
    {
        Console.WriteLine("## Scenario 6 — corrupted main database recovery");
        // Reuse the smallest snapshot-size fixture and its backup from Scenario 5.
        string validDb = Path.Combine(workDir, "s5-5000.db");
        string validBackup = Path.Combine(workDir, "s5-5000.backup.db");
        if (!File.Exists(validDb) || !File.Exists(validBackup))
        {
            Console.WriteLine("SKIPPED: Scenario 5 fixtures not found (must run after Scenario 5).");
            return;
        }

        string corruptDb = Path.Combine(workDir, "s6-corrupt.db");
        File.Copy(validDb, corruptDb, overwrite: true);

        string preCorruptIntegrity = Program.IntegrityCheck(corruptDb);
        long fileLength = new FileInfo(corruptDb).Length;

        // Corrupt a block of bytes in the middle of the file (well past the
        // SQLite header, inside actual page data).
        const int corruptBytes = 4096;
        long offset = fileLength / 2;
        using (var stream = new FileStream(corruptDb, FileMode.Open, FileAccess.ReadWrite))
        {
            stream.Seek(offset, SeekOrigin.Begin);
            var junk = new byte[corruptBytes];
            new Random(12345).NextBytes(junk);
            stream.Write(junk, 0, junk.Length);
        }

        string postCorruptIntegrity;
        bool selectThrew = false;
        string? selectException = null;
        try
        {
            postCorruptIntegrity = Program.IntegrityCheck(corruptDb);
        }
        catch (Exception ex)
        {
            postCorruptIntegrity = "INTEGRITY_CHECK_THREW: " + ex.Message;
        }

        try
        {
            using var connection = new SqliteConnection($"Pooling=False;Data Source={corruptDb};Mode=ReadOnly");
            connection.Open();
            _ = Program.QueryScalarLong(connection, "SELECT COUNT(*) FROM DomainEvents;");
        }
        catch (Exception ex)
        {
            selectThrew = true;
            selectException = ex.GetType().Name + ": " + ex.Message;
        }

        Console.WriteLine($"pre_corruption_integrity_check={preCorruptIntegrity}");
        Console.WriteLine($"corrupted_bytes={corruptBytes} offset={offset} of file_length={fileLength}");
        Console.WriteLine($"post_corruption_integrity_check=\"{postCorruptIntegrity}\" (expected: not \"ok\")");
        Console.WriteLine($"select_on_corrupted_db_threw={selectThrew} exception=\"{selectException}\" (expected True — corruption must be surfaced, not silently tolerated)");

        // Recovery path: restore from backup into a SEPARATE copy; corrupted
        // original file must remain untouched (roadmap section 10.6 exit
        // criterion: "backup restores into a separate copy").
        string restoredDb = Path.Combine(workDir, "s6-restored.db");
        File.Copy(validBackup, restoredDb, overwrite: true);
        string restoredIntegrity = Program.IntegrityCheck(restoredDb);
        long restoredRowCount;
        using (var connection = new SqliteConnection($"Pooling=False;Data Source={restoredDb};Mode=ReadOnly"))
        {
            connection.Open();
            restoredRowCount = Program.QueryScalarLong(connection, "SELECT COUNT(*) FROM DomainEvents;");
        }

        bool corruptFileStillPresentAndUnmodifiedFurther = File.Exists(corruptDb);
        long corruptFileLengthAfterRecovery = new FileInfo(corruptDb).Length;

        Console.WriteLine($"restored_from_backup_into_separate_copy={restoredDb}");
        Console.WriteLine($"restored_copy_integrity_check={restoredIntegrity} restored_row_count={restoredRowCount}");
        Console.WriteLine($"original_corrupted_file_left_in_place={corruptFileStillPresentAndUnmodifiedFurther} unchanged_length={(corruptFileLengthAfterRecovery == fileLength)}");

        bool pass = postCorruptIntegrity != "ok" && selectThrew && restoredIntegrity == "ok" && restoredRowCount == 5000 && corruptFileStillPresentAndUnmodifiedFurther;
        Console.WriteLine($"SUMMARY: corruption detected (not silently accepted) and recovery restores a separate, integrity-checked copy without touching the corrupted original: {pass}");
        Console.WriteLine();
    }
}
