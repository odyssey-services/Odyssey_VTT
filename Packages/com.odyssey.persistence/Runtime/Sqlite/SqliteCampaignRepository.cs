using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
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
    /// Does not implement: Domain Event Store semantics (ODY-S01-009/ADR-012),
    /// migration runner (ODY-S01-010/ADR-013), backup/snapshot (ODY-S01-011/ADR-012
    /// section 8), or the .odcamp export container (ODY-S01-012). The section 8.2
    /// system tables created here are the minimal columns needed to prove the table
    /// exists per ADR-011 section 8.2's own allowance ("их полный DDL... определяются
    /// реализующей задачей и... последующими ADR"); their full contract is owned by
    /// those later tasks.
    /// </summary>
    public sealed class SqliteCampaignRepository : ICampaignRepository
    {
        private const string ManifestFileName = "manifest.json";
        private const string DatabaseFileName = "campaign.db";
        private const string CampaignFormatVersion = "1.1.0";
        private const string DatabaseSchemaVersion = "1.0.0";

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

        public SqliteCampaignRepository(IWallClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public Result<CampaignHandle> Create(CreateCampaignRequest request, CorrelationId correlationId)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
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

                InsertCampaignRow(connection, campaignId, campaignPublicId, now, settings);

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
            var connection = new SqliteConnection("Data Source=" + dbPath);
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
    SettingsJson TEXT NOT NULL
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
    EventSequence INTEGER PRIMARY KEY,
    CampaignId TEXT NOT NULL,
    PayloadJson TEXT NOT NULL,
    PayloadHash TEXT NOT NULL,
    CreatedAtHost TEXT NOT NULL
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
    BackupKind TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    RelativePath TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS MigrationRecords (
    MigrationId TEXT PRIMARY KEY,
    AppliedAt TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS AssetManifestEntries (
    AssetId TEXT PRIMARY KEY,
    RelativePath TEXT NOT NULL,
    Hash TEXT,
    SizeBytes INTEGER
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

        private static void InsertCampaignRow(SqliteConnection connection, CampaignId campaignId, CampaignPublicId campaignPublicId, UtcInstant now, CampaignSettings settings)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO Campaign (CampaignId, CampaignPublicId, Revision, Status, CreatedAt, UpdatedAt, SettingsJson) " +
                                   "VALUES ($campaignId, $campaignPublicId, $revision, $status, $createdAt, $updatedAt, $settingsJson);";
            command.Parameters.AddWithValue("$campaignId", campaignId.ToString());
            command.Parameters.AddWithValue("$campaignPublicId", campaignPublicId.ToString());
            command.Parameters.AddWithValue("$revision", 1L);
            command.Parameters.AddWithValue("$status", "Active");
            command.Parameters.AddWithValue("$createdAt", now.ToString());
            command.Parameters.AddWithValue("$updatedAt", now.ToString());
            command.Parameters.AddWithValue("$settingsJson", "{\"settingsSchemaVersion\":" + settings.SettingsSchemaVersion.ToString(CultureInfo.InvariantCulture) + "}");
            command.ExecuteNonQuery();
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
