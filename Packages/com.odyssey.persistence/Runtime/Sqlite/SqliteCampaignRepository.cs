using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Persistence.Sqlite
{
    /// <summary>
    /// ADR-011 Local Campaign Format v1.1 implementation: physical folder tree
    /// (section 4.1), campaign.db under the mandatory PRAGMA profile (section 7.1),
    /// manifest.json atomic write/read and manifest-vs-database conflict detection
    /// (section 5), the minimal mandatory system table set (section 8.2), and
    /// CampaignId/CampaignPublicId generation (section 9.1). Uses Microsoft.Data.Sqlite
    /// with SQLitePCLRaw.bundle_e_sqlite3 >= 3.0.3 per ADR-011 v1.1 section 1.
    ///
    /// ODY-S01-009: <see cref="Create"/> now routes its Campaign row through
    /// <see cref="SqliteSavingPipeline"/> (ADR-012 section 5 single-transaction
    /// journal-projection commit), and <see cref="Open"/> runs the ADR-011/
    /// 05_Persistence section 22.1 quick integrity check before handing out a
    /// handle. Migration runner (ODY-S01-010/ADR-013), backup/snapshot
    /// (ODY-S01-011/ADR-012 section 8), and the .odcamp export container
    /// (ODY-S01-012) remain out of scope.
    /// </summary>
    public sealed class SqliteCampaignRepository : ICampaignRepository
    {
        private const string ManifestFileName = "manifest.json";
        private const string DatabaseFileName = "campaign.db";
        private const string CampaignFormatVersion = "1.1.0";
        internal const string DatabaseSchemaVersion = "1.0.0";

        private static readonly string[] DirectoryTree =
        {
            "Assets/Objects", "Assets/Staging", "Assets/Trash", "Assets/Quarantine",
            "Backups/Fast", "Backups/Daily", "Backups/Weekly", "Backups/Full", "Backups/Emergency",
            "Logs/Archive", "Logs/Diagnostics", "Logs/Migration",
            "Temp",
        };

        private readonly ConcurrentDictionary<string, SqliteConnection> _openConnections = new ConcurrentDictionary<string, SqliteConnection>(StringComparer.OrdinalIgnoreCase);
        private readonly CampaignManifestV1Codec _manifestCodec = new CampaignManifestV1Codec();
        private readonly IWallClock _clock;
        private readonly SqliteSavingPipeline _pipeline;

        public SqliteCampaignRepository(IWallClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _pipeline = new SqliteSavingPipeline(clock);
        }

        public Result<CampaignHandle> Create(CreateCampaignRequest request, CommandId commandId, CorrelationId correlationId)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            string rootPath = Path.GetFullPath(request.CampaignFolderPath);

            try
            {
                if (Directory.Exists(rootPath) && Directory.GetFileSystemEntries(rootPath).Length > 0)
                {
                    return Result<CampaignHandle>.Failure(PersistenceFailures.CampaignIoFailed(correlationId));
                }

                Directory.CreateDirectory(rootPath);
                foreach (string relative in DirectoryTree)
                {
                    Directory.CreateDirectory(Path.Combine(rootPath, relative.Replace('/', Path.DirectorySeparatorChar)));
                }

                string dbPath = Path.Combine(rootPath, DatabaseFileName);
                SqliteConnection connection = OpenConnectionWithPragmaProfile(dbPath);
                CreateSystemTables(connection);

                UtcInstant now = _clock.GetUtcNow();
                CampaignId campaignId = CampaignId.NewId(now);
                CampaignPublicId campaignPublicId = CampaignPublicId.NewId(now);
                var settings = new CampaignSettings();

                Result<CampaignId> pipelineResult = _pipeline.Execute(
                    connection,
                    campaignId,
                    commandId,
                    correlationId,
                    // The empty-directory precondition above already
                    // rejects a redelivered Create against an already-created
                    // campaign folder before the pipeline ever runs, so a genuine
                    // AppliedCommands hit here would mean a campaign.db was manually
                    // seeded with a colliding CommandId -- not a real retry path.
                    tryReplay: _ => Result<CampaignId>.Failure(PersistenceFailures.CommandReplayFailed(correlationId)),
                    apply: transaction =>
                    {
                        InsertCampaignRow(connection, transaction, campaignId, campaignPublicId, now, settings, commandId);
                        InsertInitialSchemaHistoryRow(connection, transaction, now, request.ApplicationVersion);
                        string payloadJson = "{\"campaignId\":\"" + campaignId + "\",\"campaignPublicId\":\"" + campaignPublicId + "\"}";
                        return Result<PipelineWrite<CampaignId>>.Success(new PipelineWrite<CampaignId>(
                            campaignId, "odyssey.persistence.campaign_created", payloadJson, campaignId.ToString(),
                            aggregateType: "campaign", aggregateId: campaignId.ToString(), aggregateRevision: 1));
                    });

                if (pipelineResult.IsFailure)
                {
                    connection.Dispose();
                    return Result<CampaignHandle>.Failure(pipelineResult.Error);
                }

                var manifest = new CampaignManifest(
                    campaignId,
                    request.CampaignName,
                    CampaignFormatVersion,
                    DatabaseSchemaVersion,
                    request.RulesetId,
                    request.RulesetVersion,
                    now,
                    now,
                    request.ApplicationVersion,
                    assetManifestVersion: 1,
                    isTemplate: false);

                Result writeResult = WriteManifestAtomic(rootPath, manifest);
                if (writeResult.IsFailure)
                {
                    connection.Dispose();
                    return Result<CampaignHandle>.Failure(writeResult.Error);
                }

                _openConnections[NormalizeKey(rootPath)] = connection;
                return Result<CampaignHandle>.Success(new CampaignHandle(campaignId, campaignPublicId, rootPath, manifest));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CampaignHandle>.Failure(PersistenceFailures.CampaignIoFailed(correlationId));
            }
        }

        public Result<CampaignHandle> Open(string campaignFolderPath, CorrelationId correlationId)
        {
            if (string.IsNullOrWhiteSpace(campaignFolderPath)) throw new ArgumentException("Campaign folder path is required.", nameof(campaignFolderPath));
            string rootPath = Path.GetFullPath(campaignFolderPath);
            string manifestPath = Path.Combine(rootPath, ManifestFileName);
            string dbPath = Path.Combine(rootPath, DatabaseFileName);

            if (!File.Exists(manifestPath) || !File.Exists(dbPath))
            {
                return Result<CampaignHandle>.Failure(PersistenceFailures.CampaignNotFound(correlationId));
            }

            try
            {
                byte[] manifestBytes = File.ReadAllBytes(manifestPath);
                Result<CampaignManifest> manifestResult = _manifestCodec.Read(manifestBytes);
                if (manifestResult.IsFailure)
                {
                    return Result<CampaignHandle>.Failure(manifestResult.Error);
                }

                CampaignManifest manifest = manifestResult.Value;
                SqliteConnection connection = OpenConnectionWithPragmaProfile(dbPath);

                // 05_Persistence section 22.1/22.2 quick check, run on every Open
                // (covers the "open after unclean shutdown" trigger -- Open has no
                // way to know in advance whether the previous session closed
                // cleanly, so it always runs). Lock validation from the same list is
                // not implemented: no campaign.lock mechanism exists yet in this
                // codebase (ADR-011 section 4.1 reserves the file, ADR-014 owner-key
                // work does not create it), so there is nothing to validate here.
                Result quickCheckResult = RunQuickIntegrityCheck(connection, correlationId);
                if (quickCheckResult.IsFailure)
                {
                    connection.Dispose();
                    return Result<CampaignHandle>.Failure(quickCheckResult.Error);
                }

                Result<(CampaignId CampaignId, CampaignPublicId CampaignPublicId)> identityResult = ReadCampaignIdentity(connection);
                if (identityResult.IsFailure)
                {
                    connection.Dispose();
                    return Result<CampaignHandle>.Failure(identityResult.Error);
                }

                (CampaignId dbCampaignId, CampaignPublicId dbCampaignPublicId) = identityResult.Value;

                // ADR-011 section 5.4: manifest/database CampaignId disagreement is a
                // diagnosable conflict; the database is never opened for write, and
                // neither side is silently trusted over the other.
                if (dbCampaignId != manifest.CampaignId)
                {
                    connection.Dispose();
                    return Result<CampaignHandle>.Failure(PersistenceFailures.ManifestConflict(correlationId));
                }

                _openConnections[NormalizeKey(rootPath)] = connection;
                return Result<CampaignHandle>.Success(new CampaignHandle(dbCampaignId, dbCampaignPublicId, rootPath, manifest));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CampaignHandle>.Failure(PersistenceFailures.CampaignIoFailed(correlationId));
            }
        }

        public Result Close(CampaignHandle handle, CorrelationId correlationId)
        {
            if (handle == null) throw new ArgumentNullException(nameof(handle));
            string key = NormalizeKey(handle.RootPath);

            if (!_openConnections.TryRemove(key, out SqliteConnection? connection))
            {
                return Result.Failure(PersistenceFailures.CampaignNotFound(correlationId));
            }

            try
            {
                // ADR-011 section 7.4: WAL checkpoint on clean close.
                using (var checkpoint = connection.CreateCommand())
                {
                    checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                    checkpoint.ExecuteNonQuery();
                }

                connection.Dispose();

                // ADR-011 section 5.5: LastModifiedAt updates at clean-close checkpoints.
                CampaignManifest updated = handle.Manifest.WithLastModifiedAt(_clock.GetUtcNow());
                return WriteManifestAtomic(handle.RootPath, updated);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result.Failure(PersistenceFailures.CampaignIoFailed(correlationId));
            }
        }

        private static SqliteConnection OpenConnectionWithPragmaProfile(string dbPath)
        {
            // ODY-S01-011: Pooling=False so a subsequent raw file-level read of
            // campaign.db (backup, corruption-fixture inspection) right after
            // Close() never races Microsoft.Data.Sqlite's connection pool holding
            // the native handle open a little longer than Dispose() -- the same
            // convention SP-02's harness and the ODY-S01-009/011 kill-test
            // harnesses already use for exactly this reason.
            var connection = new SqliteConnection("Data Source=" + dbPath + ";Pooling=False");
            connection.Open();
            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText =
                    "PRAGMA journal_mode = WAL; " +
                    "PRAGMA foreign_keys = ON; " +
                    "PRAGMA synchronous = FULL; " +
                    "PRAGMA busy_timeout = 5000;";
                pragma.ExecuteNonQuery();
            }

            return connection;
        }

        private static void CreateSystemTables(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS Campaign (
    CampaignId TEXT PRIMARY KEY,
    CampaignPublicId TEXT NOT NULL UNIQUE,
    Revision INTEGER NOT NULL,
    Status TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    SettingsJson TEXT NOT NULL,
    LastCommandId TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS PersistenceMetadata (
    Key TEXT PRIMARY KEY,
    Value TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS SchemaHistory (
    MigrationId TEXT PRIMARY KEY,
    FromVersion TEXT NOT NULL,
    ToVersion TEXT NOT NULL,
    CodeChecksum TEXT NOT NULL,
    StartedAt TEXT NOT NULL,
    CompletedAt TEXT,
    Status TEXT NOT NULL,
    ApplicationVersion TEXT NOT NULL,
    BackupId TEXT,
    FailureCode TEXT
);
CREATE TABLE IF NOT EXISTS AppliedCommands (
    CommandId TEXT PRIMARY KEY,
    Status TEXT NOT NULL,
    ResultEventSequenceFrom INTEGER,
    ResultEventSequenceTo INTEGER,
    ResultSummary TEXT,
    FailureCode TEXT,
    CreatedAt TEXT NOT NULL,
    CompletedAt TEXT
);
CREATE TABLE IF NOT EXISTS DomainEvents (
    EventSequence INTEGER PRIMARY KEY AUTOINCREMENT,
    CampaignId TEXT NOT NULL,
    EventType TEXT NOT NULL,
    CommandId TEXT NOT NULL,
    PayloadJson TEXT NOT NULL,
    PayloadHash TEXT NOT NULL,
    CreatedAtHost TEXT NOT NULL,
    OriginalEventId INTEGER,
    CompensationGroupId TEXT,
    IsCompensating INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS AggregateRevisions (
    AggregateType TEXT NOT NULL,
    AggregateId TEXT NOT NULL,
    Revision INTEGER NOT NULL,
    PRIMARY KEY (AggregateType, AggregateId)
);
CREATE TABLE IF NOT EXISTS PendingInteractions (
    InteractionId TEXT PRIMARY KEY,
    Status TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    ExpiresAt TEXT
);
CREATE TABLE IF NOT EXISTS NetworkOutbox (
    OutboxId TEXT PRIMARY KEY,
    Status TEXT NOT NULL,
    PayloadJson TEXT NOT NULL,
    CreatedAt TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS AdministrativeAudit (
    AuditId TEXT PRIMARY KEY,
    Action TEXT NOT NULL,
    ActorUserId TEXT,
    CreatedAt TEXT NOT NULL,
    DetailJson TEXT
);
CREATE TABLE IF NOT EXISTS DiagnosticRecords (
    DiagnosticRecordId TEXT PRIMARY KEY,
    Severity TEXT NOT NULL,
    Category TEXT NOT NULL,
    CreatedAt TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS BackupRecords (
    BackupId TEXT PRIMARY KEY,
    CampaignId TEXT NOT NULL,
    BackupKind TEXT NOT NULL,
    Reason TEXT NOT NULL,
    CampaignRevision INTEGER NOT NULL,
    EventSequence INTEGER NOT NULL,
    DatabaseSchemaVersion TEXT NOT NULL,
    CampaignFormatVersion TEXT NOT NULL,
    RulesetRef TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    CreatedByUserId TEXT,
    RelativePath TEXT NOT NULL,
    DatabaseHash TEXT NOT NULL,
    AssetsManifestHash TEXT,
    SizeBytes INTEGER NOT NULL,
    IntegrityStatus TEXT NOT NULL,
    SourceOperationId TEXT
);
CREATE TABLE IF NOT EXISTS MigrationRecords (
    MigrationId TEXT PRIMARY KEY,
    AppliedAt TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS AssetManifestEntries (
    AssetId TEXT PRIMARY KEY,
    RelativePath TEXT NOT NULL,
    Hash TEXT,
    SizeBytes INTEGER,
    LastCommandId TEXT NOT NULL DEFAULT ''
);
CREATE TABLE IF NOT EXISTS AssetReferences (
    AssetId TEXT NOT NULL,
    ReferencedByType TEXT NOT NULL,
    ReferencedById TEXT NOT NULL,
    PRIMARY KEY (AssetId, ReferencedByType, ReferencedById)
);
CREATE TABLE IF NOT EXISTS SessionArchiveIndex (
    SessionId TEXT PRIMARY KEY,
    ArchivedAt TEXT NOT NULL,
    RelativePath TEXT NOT NULL
);";
            command.ExecuteNonQuery();
        }

        private static void InsertCampaignRow(SqliteConnection connection, SqliteTransaction transaction, CampaignId campaignId, CampaignPublicId campaignPublicId, UtcInstant now, CampaignSettings settings, CommandId commandId)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO Campaign (CampaignId, CampaignPublicId, Revision, Status, CreatedAt, UpdatedAt, SettingsJson, LastCommandId) " +
                                   "VALUES ($campaignId, $campaignPublicId, $revision, $status, $createdAt, $updatedAt, $settingsJson, $lastCommandId);";
            command.Parameters.AddWithValue("$campaignId", campaignId.ToString());
            command.Parameters.AddWithValue("$campaignPublicId", campaignPublicId.ToString());
            command.Parameters.AddWithValue("$revision", 1L);
            command.Parameters.AddWithValue("$status", "Active");
            command.Parameters.AddWithValue("$createdAt", now.ToString());
            command.Parameters.AddWithValue("$updatedAt", now.ToString());
            command.Parameters.AddWithValue("$settingsJson", "{\"settingsSchemaVersion\":" + settings.SettingsSchemaVersion.ToString(CultureInfo.InvariantCulture) + "}");
            command.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
            command.ExecuteNonQuery();
        }

        private static void InsertInitialSchemaHistoryRow(SqliteConnection connection, SqliteTransaction transaction, UtcInstant now, string applicationVersion)
        {
            // ODY-S01-010: the 0001_Initial identity migration does not change any
            // schema (the campaign is created directly on DatabaseSchemaVersion) --
            // this row is the formal SchemaHistory record that the campaign's
            // history began on that version (ADR-013 section 8; backlog section
            // 2.1's "migration registry baseline"). BackupId is null: there is no
            // pre-migration snapshot for a brand-new campaign, since nothing is
            // being migrated from an older version (ADR-013 section 8's BackupId
            // traceability requirement applies to a real migration's pre-migration
            // snapshot, which does not exist here).
            MigrationDescriptor initial = MigrationRegistry.Initial;
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "INSERT INTO SchemaHistory (MigrationId, FromVersion, ToVersion, CodeChecksum, StartedAt, CompletedAt, Status, ApplicationVersion, BackupId, FailureCode) " +
                "VALUES ($migrationId, $fromVersion, $toVersion, $checksum, $startedAt, $completedAt, 'Completed', $applicationVersion, NULL, NULL);";
            command.Parameters.AddWithValue("$migrationId", initial.MigrationId);
            command.Parameters.AddWithValue("$fromVersion", initial.FromVersion);
            command.Parameters.AddWithValue("$toVersion", initial.ToVersion);
            command.Parameters.AddWithValue("$checksum", initial.CodeChecksum);
            command.Parameters.AddWithValue("$startedAt", now.ToString());
            command.Parameters.AddWithValue("$completedAt", now.ToString());
            command.Parameters.AddWithValue("$applicationVersion", applicationVersion);
            command.ExecuteNonQuery();
        }

        private static Result RunQuickIntegrityCheck(SqliteConnection connection, CorrelationId correlationId)
        {
            // 05_Persistence section 22.1 quick check: SQLite quick_check, plus "no
            // incomplete migration state" (a SchemaHistory row whose Status is not
            // a terminal one). Manifest parse, CampaignId match, and campaign.db
            // presence are already checked by the caller (Open); schema-metadata
            // match reduces to the manifest's own DatabaseSchemaVersion field, which
            // ODY-S01-007 already parses/validates on read.
            using (var quickCheck = connection.CreateCommand())
            {
                quickCheck.CommandText = "PRAGMA quick_check;";
                object? result = quickCheck.ExecuteScalar();
                if (!(result is string status) || !string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    return Result.Failure(PersistenceFailures.IntegrityCheckFailed(correlationId));
                }
            }

            using (var migrationCheck = connection.CreateCommand())
            {
                migrationCheck.CommandText = "SELECT COUNT(*) FROM SchemaHistory WHERE Status NOT IN ('Completed', 'RolledBack');";
                long incomplete = Convert.ToInt64(migrationCheck.ExecuteScalar(), CultureInfo.InvariantCulture);
                if (incomplete > 0)
                {
                    return Result.Failure(PersistenceFailures.IntegrityCheckFailed(correlationId));
                }
            }

            return Result.Success();
        }

        private static Result<(CampaignId, CampaignPublicId)> ReadCampaignIdentity(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT CampaignId, CampaignPublicId FROM Campaign LIMIT 1;";
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return Result<(CampaignId, CampaignPublicId)>.Failure(PersistenceFailures.ManifestInvalid());
            }

            if (!CampaignId.TryParse(reader.GetString(0), out CampaignId campaignId) ||
                !CampaignPublicId.TryParse(reader.GetString(1), out CampaignPublicId campaignPublicId))
            {
                return Result<(CampaignId, CampaignPublicId)>.Failure(PersistenceFailures.ManifestInvalid());
            }

            return Result<(CampaignId, CampaignPublicId)>.Success((campaignId, campaignPublicId));
        }

        private Result WriteManifestAtomic(string rootPath, CampaignManifest manifest)
        {
            Result<JsonPayloadHolder> writeResult = WriteManifestJson(manifest);
            if (writeResult.IsFailure)
            {
                return Result.Failure(writeResult.Error);
            }

            string finalPath = Path.Combine(rootPath, ManifestFileName);
            string tempPath = finalPath + ".tmp";

            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(writeResult.Value.Bytes, 0, writeResult.Value.Bytes.Length);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }

            File.Move(tempPath, finalPath);
            return Result.Success();
        }

        private Result<JsonPayloadHolder> WriteManifestJson(CampaignManifest manifest)
        {
            Result<Application.Serialization.JsonPayload> codecResult = _manifestCodec.Write(manifest);
            if (codecResult.IsFailure)
            {
                return Result<JsonPayloadHolder>.Failure(codecResult.Error);
            }

            return Result<JsonPayloadHolder>.Success(new JsonPayloadHolder(codecResult.Value.Bytes));
        }

        private sealed class JsonPayloadHolder
        {
            public JsonPayloadHolder(byte[] bytes) => Bytes = bytes;
            public byte[] Bytes { get; }
        }

        private static string NormalizeKey(string rootPath) => Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
