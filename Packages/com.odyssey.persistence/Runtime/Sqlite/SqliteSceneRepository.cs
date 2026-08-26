using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
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
    /// ODY-S01-008 implementation of ISceneRepository. Each method opens its own
    /// short-lived connection under the ADR-011 section 7.1 PRAGMA profile.
    ///
    /// ODY-S01-009: every mutating method now commits its projection write through
    /// <see cref="SqliteSavingPipeline"/> -- the projection row, the corresponding
    /// DomainEvent, and the AppliedCommands idempotency record land in one SQLite
    /// transaction (ADR-012 section 5). This task still does not implement ADR-011
    /// section 7.2's single logical write-queue-per-campaign serialization across
    /// repository types (Campaign/Scene still open independent connections) -- the
    /// pipeline's own transaction is the only serialization boundary introduced
    /// here, scoped to what ADR-012 section 5 requires, not the broader write-queue
    /// infrastructure (see the ODY-S01-009 task contract section 5, "не входит").
    /// </summary>
    public sealed class SqliteSceneRepository : ISceneRepository
    {
        private const string AssetsObjectsRelativeDirectory = "Assets/Objects";
        private readonly IWallClock _clock;
        private readonly SqliteSavingPipeline _pipeline;

        public SqliteSceneRepository(IWallClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _pipeline = new SqliteSavingPipeline(clock);
        }

        public Result<SceneRecord> CreateScene(CampaignHandle campaign, string sceneName, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (string.IsNullOrWhiteSpace(sceneName) || sceneName.Length > 128)
            {
                return Result<SceneRecord>.Failure(PersistenceFailures.SceneIoFailed(correlationId));
            }

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureSceneTokenTables(connection);
                UtcInstant now = _clock.GetUtcNow();

                return _pipeline.Execute(
                    connection,
                    campaign.CampaignId,
                    commandId,
                    correlationId,
                    tryReplay: transaction => ReplayScene(connection, transaction, campaign.CampaignId, commandId, correlationId),
                    apply: transaction =>
                    {
                        SceneId sceneId = SceneId.NewId(now);
                        const long revision = 1L;
                        const string status = "Draft";

                        using (var insert = connection.CreateCommand())
                        {
                            insert.Transaction = transaction;
                            insert.CommandText = "INSERT INTO Scene (SceneId, CampaignId, Name, Status, Revision, CreatedAt, UpdatedAt, LastCommandId) " +
                                                  "VALUES ($sceneId, $campaignId, $name, $status, $revision, $createdAt, $updatedAt, $lastCommandId);";
                            insert.Parameters.AddWithValue("$sceneId", sceneId.ToString());
                            insert.Parameters.AddWithValue("$campaignId", campaign.CampaignId.ToString());
                            insert.Parameters.AddWithValue("$name", sceneName);
                            insert.Parameters.AddWithValue("$status", status);
                            insert.Parameters.AddWithValue("$revision", revision);
                            insert.Parameters.AddWithValue("$createdAt", now.ToString());
                            insert.Parameters.AddWithValue("$updatedAt", now.ToString());
                            insert.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            insert.ExecuteNonQuery();
                        }

                        var record = new SceneRecord(sceneId, campaign.CampaignId, sceneName, status, revision, now, now);
                        string payloadJson = "{\"sceneId\":\"" + sceneId + "\",\"name\":" + JsonString(sceneName) + "}";
                        return Result<PipelineWrite<SceneRecord>>.Success(new PipelineWrite<SceneRecord>(
                            record, "odyssey.persistence.scene_created", payloadJson, sceneId.ToString(),
                            aggregateType: "scene", aggregateId: sceneId.ToString(), aggregateRevision: revision));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<SceneRecord>.Failure(PersistenceFailures.SceneIoFailed(correlationId));
            }
        }

        public Result<TokenRecord> CreateToken(CampaignHandle campaign, SceneId sceneId, TokenPosition initialPosition, UserId controllerUserId, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));
            if (!controllerUserId.IsValid) throw new ArgumentException("ControllerUserId is required.", nameof(controllerUserId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureSceneTokenTables(connection);

                if (!SceneExists(connection, sceneId))
                {
                    return Result<TokenRecord>.Failure(PersistenceFailures.SceneNotFound(correlationId));
                }

                UtcInstant now = _clock.GetUtcNow();

                return _pipeline.Execute(
                    connection,
                    campaign.CampaignId,
                    commandId,
                    correlationId,
                    tryReplay: transaction => ReplayToken(connection, transaction, "TokenId = (SELECT TokenId FROM Token WHERE LastCommandId = $commandId LIMIT 1)", campaign.CampaignId, commandId, correlationId),
                    apply: transaction =>
                    {
                        TokenId tokenId = TokenId.NewId(now);
                        const long revision = 1L;

                        using (var insert = connection.CreateCommand())
                        {
                            insert.Transaction = transaction;
                            insert.CommandText = "INSERT INTO Token (TokenId, SceneId, CampaignId, PositionX, PositionY, ControllerUserId, Revision, CreatedAt, UpdatedAt, LastCommandId) " +
                                                  "VALUES ($tokenId, $sceneId, $campaignId, $x, $y, $controllerUserId, $revision, $createdAt, $updatedAt, $lastCommandId);";
                            insert.Parameters.AddWithValue("$tokenId", tokenId.ToString());
                            insert.Parameters.AddWithValue("$sceneId", sceneId.ToString());
                            insert.Parameters.AddWithValue("$campaignId", campaign.CampaignId.ToString());
                            insert.Parameters.AddWithValue("$x", initialPosition.X);
                            insert.Parameters.AddWithValue("$y", initialPosition.Y);
                            insert.Parameters.AddWithValue("$controllerUserId", controllerUserId.ToString());
                            insert.Parameters.AddWithValue("$revision", revision);
                            insert.Parameters.AddWithValue("$createdAt", now.ToString());
                            insert.Parameters.AddWithValue("$updatedAt", now.ToString());
                            insert.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            insert.ExecuteNonQuery();
                        }

                        var record = new TokenRecord(tokenId, sceneId, campaign.CampaignId, initialPosition, controllerUserId, revision, now, now);
                        string payloadJson = "{\"tokenId\":\"" + tokenId + "\",\"sceneId\":\"" + sceneId + "\",\"controllerUserId\":\"" + controllerUserId + "\",\"x\":" +
                                              initialPosition.X.ToString(CultureInfo.InvariantCulture) + ",\"y\":" + initialPosition.Y.ToString(CultureInfo.InvariantCulture) + "}";
                        return Result<PipelineWrite<TokenRecord>>.Success(new PipelineWrite<TokenRecord>(
                            record, "odyssey.persistence.token_created", payloadJson, tokenId.ToString(),
                            aggregateType: "token", aggregateId: tokenId.ToString(), aggregateRevision: revision));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<TokenRecord>.Failure(PersistenceFailures.SceneIoFailed(correlationId));
            }
        }

        public Result<TokenRecord> GetToken(CampaignHandle campaign, TokenId tokenId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!tokenId.IsValid) throw new ArgumentException("TokenId is required.", nameof(tokenId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureSceneTokenTables(connection);

                using var select = connection.CreateCommand();
                select.CommandText = "SELECT TokenId, SceneId, PositionX, PositionY, ControllerUserId, Revision, CreatedAt, UpdatedAt FROM Token WHERE TokenId = $tokenId LIMIT 1;";
                select.Parameters.AddWithValue("$tokenId", tokenId.ToString());
                using SqliteDataReader reader = select.ExecuteReader();
                if (!reader.Read())
                {
                    return Result<TokenRecord>.Failure(PersistenceFailures.TokenNotFound(correlationId));
                }

                return Result<TokenRecord>.Success(ReadTokenRecord(reader, campaign.CampaignId));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<TokenRecord>.Failure(PersistenceFailures.SceneIoFailed(correlationId));
            }
        }

        public Result<TokenRecord> MoveToken(CampaignHandle campaign, TokenId tokenId, TokenPosition newPosition, long expectedRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!tokenId.IsValid) throw new ArgumentException("TokenId is required.", nameof(tokenId));
            if (expectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureSceneTokenTables(connection);

                return _pipeline.Execute(
                    connection,
                    campaign.CampaignId,
                    commandId,
                    correlationId,
                    tryReplay: transaction => ReplayToken(connection, transaction, "TokenId = $tokenId", campaign.CampaignId, commandId, correlationId, tokenId),
                    apply: transaction =>
                    {
                        SceneId sceneId;
                        UserId controllerUserId;
                        long previousRevision;
                        UtcInstant createdAt;
                        using (var select = connection.CreateCommand())
                        {
                            select.Transaction = transaction;
                            select.CommandText = "SELECT SceneId, ControllerUserId, Revision, CreatedAt FROM Token WHERE TokenId = $tokenId LIMIT 1;";
                            select.Parameters.AddWithValue("$tokenId", tokenId.ToString());
                            using SqliteDataReader reader = select.ExecuteReader();
                            if (!reader.Read())
                            {
                                return Result<PipelineWrite<TokenRecord>>.Failure(PersistenceFailures.TokenNotFound(correlationId));
                            }

                            if (!SceneId.TryParse(reader.GetString(0), out sceneId))
                            {
                                return Result<PipelineWrite<TokenRecord>>.Failure(PersistenceFailures.SceneIoFailed(correlationId));
                            }

                            controllerUserId = UserId.Parse(reader.GetString(1));
                            previousRevision = reader.GetInt64(2);
                            createdAt = UtcInstant.Parse(reader.GetString(3));
                        }

                        // ADR-002 section 10.2: the final, atomic optimistic-
                        // concurrency guard -- independent of any Application-
                        // layer pre-check (Odyssey.Application.Board.
                        // BoardMovementService), which runs outside this
                        // transaction and cannot itself close the race window.
                        if (previousRevision != expectedRevision)
                        {
                            return Result<PipelineWrite<TokenRecord>>.Failure(PersistenceFailures.TokenRevisionConflict(correlationId));
                        }

                        UtcInstant now = _clock.GetUtcNow();
                        long newRevision = previousRevision + 1;

                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText = "UPDATE Token SET PositionX = $x, PositionY = $y, Revision = $revision, UpdatedAt = $updatedAt, LastCommandId = $lastCommandId WHERE TokenId = $tokenId;";
                            update.Parameters.AddWithValue("$x", newPosition.X);
                            update.Parameters.AddWithValue("$y", newPosition.Y);
                            update.Parameters.AddWithValue("$revision", newRevision);
                            update.Parameters.AddWithValue("$updatedAt", now.ToString());
                            update.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            update.Parameters.AddWithValue("$tokenId", tokenId.ToString());
                            update.ExecuteNonQuery();
                        }

                        var record = new TokenRecord(tokenId, sceneId, campaign.CampaignId, newPosition, controllerUserId, newRevision, createdAt, now);
                        string payloadJson = "{\"tokenId\":\"" + tokenId + "\",\"x\":" + newPosition.X.ToString(CultureInfo.InvariantCulture) +
                                              ",\"y\":" + newPosition.Y.ToString(CultureInfo.InvariantCulture) + "}";
                        return Result<PipelineWrite<TokenRecord>>.Success(new PipelineWrite<TokenRecord>(
                            record, "odyssey.persistence.token_moved", payloadJson, tokenId.ToString(),
                            aggregateType: "token", aggregateId: tokenId.ToString(), aggregateRevision: newRevision));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<TokenRecord>.Failure(PersistenceFailures.SceneIoFailed(correlationId));
            }
        }

        public Result<IReadOnlyList<TokenRecord>> ListTokens(CampaignHandle campaign, SceneId sceneId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureSceneTokenTables(connection);

                if (!SceneExists(connection, sceneId))
                {
                    return Result<IReadOnlyList<TokenRecord>>.Failure(PersistenceFailures.SceneNotFound(correlationId));
                }

                var tokens = new List<TokenRecord>();
                using (var select = connection.CreateCommand())
                {
                    select.CommandText = "SELECT TokenId, SceneId, PositionX, PositionY, ControllerUserId, Revision, CreatedAt, UpdatedAt FROM Token WHERE SceneId = $sceneId ORDER BY CreatedAt;";
                    select.Parameters.AddWithValue("$sceneId", sceneId.ToString());
                    using SqliteDataReader reader = select.ExecuteReader();
                    while (reader.Read())
                    {
                        tokens.Add(ReadTokenRecord(reader, campaign.CampaignId));
                    }
                }

                return Result<IReadOnlyList<TokenRecord>>.Success(tokens);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<IReadOnlyList<TokenRecord>>.Failure(PersistenceFailures.SceneIoFailed(correlationId));
            }
        }

        public Result<AssetManifestEntryRecord> RegisterAsset(CampaignHandle campaign, string sourceFilePath, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                return Result<AssetManifestEntryRecord>.Failure(PersistenceFailures.SceneIoFailed(correlationId));
            }

            try
            {
                // ADR-011 section 4.2: only the relative, in-campaign path is ever
                // persisted; the caller-supplied absolute source path exists only in
                // this local, transient import step and is never written to the
                // database or manifest. The file copy itself is outside the SQLite
                // transaction (it is a filesystem operation, not part of the ADR-012
                // section 5 journal/projection group); a redelivered RegisterAsset
                // with the same CommandId after a first successful run is replayed
                // from AppliedCommands without copying again.
                string objectsDirectory = Path.Combine(campaign.RootPath, AssetsObjectsRelativeDirectory.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(objectsDirectory);
                string fileName = Path.GetFileName(sourceFilePath);
                string destinationPath = Path.Combine(objectsDirectory, fileName);

                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                UtcInstant now = _clock.GetUtcNow();

                return _pipeline.Execute(
                    connection,
                    campaign.CampaignId,
                    commandId,
                    correlationId,
                    tryReplay: transaction => ReplayAsset(connection, transaction, commandId, correlationId),
                    apply: transaction =>
                    {
                        if (!File.Exists(destinationPath))
                        {
                            File.Copy(sourceFilePath, destinationPath, overwrite: false);
                        }

                        string sha256Hash;
                        using (var sha = SHA256.Create())
                        using (var stream = File.OpenRead(destinationPath))
                        {
                            byte[] hashBytes = sha.ComputeHash(stream);
                            sha256Hash = ToLowerHex(hashBytes);
                        }

                        long sizeBytes = new FileInfo(destinationPath).Length;
                        string relativePath = AssetsObjectsRelativeDirectory + "/" + fileName;
                        AssetId assetId = AssetId.NewId(now);

                        using (var insert = connection.CreateCommand())
                        {
                            insert.Transaction = transaction;
                            insert.CommandText = "INSERT INTO AssetManifestEntries (AssetId, RelativePath, Hash, SizeBytes, LastCommandId) VALUES ($assetId, $relativePath, $hash, $size, $lastCommandId);";
                            insert.Parameters.AddWithValue("$assetId", assetId.ToString());
                            insert.Parameters.AddWithValue("$relativePath", relativePath);
                            insert.Parameters.AddWithValue("$hash", sha256Hash);
                            insert.Parameters.AddWithValue("$size", sizeBytes);
                            insert.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            insert.ExecuteNonQuery();
                        }

                        var record = new AssetManifestEntryRecord(assetId, relativePath, sha256Hash, sizeBytes);
                        string payloadJson = "{\"assetId\":\"" + assetId + "\",\"relativePath\":" + JsonString(relativePath) + ",\"hash\":" + JsonString(sha256Hash) + "}";
                        return Result<PipelineWrite<AssetManifestEntryRecord>>.Success(new PipelineWrite<AssetManifestEntryRecord>(
                            record, "odyssey.persistence.asset_registered", payloadJson, assetId.ToString()));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<AssetManifestEntryRecord>.Failure(PersistenceFailures.SceneIoFailed(correlationId));
            }
        }

        private static Result<SceneRecord> ReplayScene(SqliteConnection connection, SqliteTransaction transaction, CampaignId campaignId, CommandId commandId, CorrelationId correlationId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT SceneId, Name, Status, Revision, CreatedAt, UpdatedAt FROM Scene WHERE LastCommandId = $commandId LIMIT 1;";
            select.Parameters.AddWithValue("$commandId", commandId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read())
            {
                return Result<SceneRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId));
            }

            SceneId sceneId = SceneId.Parse(reader.GetString(0));
            return Result<SceneRecord>.Success(new SceneRecord(
                sceneId, campaignId, reader.GetString(1), reader.GetString(2), reader.GetInt64(3),
                UtcInstant.Parse(reader.GetString(4)), UtcInstant.Parse(reader.GetString(5))));
        }

        private static Result<TokenRecord> ReplayToken(SqliteConnection connection, SqliteTransaction transaction, string whereClause, CampaignId campaignId, CommandId commandId, CorrelationId correlationId, TokenId? knownTokenId = null)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT TokenId, SceneId, PositionX, PositionY, ControllerUserId, Revision, CreatedAt, UpdatedAt FROM Token WHERE " + whereClause + " LIMIT 1;";
            if (knownTokenId.HasValue)
            {
                select.Parameters.AddWithValue("$tokenId", knownTokenId.Value.ToString());
            }
            else
            {
                select.Parameters.AddWithValue("$commandId", commandId.ToString());
            }

            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read())
            {
                return Result<TokenRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId));
            }

            return Result<TokenRecord>.Success(ReadTokenRecord(reader, campaignId));
        }

        /// <summary>
        /// ODY-S03-004: shared column-order contract for every SELECT against
        /// <c>Token</c> that returns a full row -- TokenId, SceneId, PositionX,
        /// PositionY, ControllerUserId, Revision, CreatedAt, UpdatedAt, in that
        /// order. Every caller (<see cref="GetToken"/>, <see cref="ListTokens"/>,
        /// <see cref="ReplayToken"/>) uses this exact column list.
        /// </summary>
        private static TokenRecord ReadTokenRecord(SqliteDataReader reader, CampaignId campaignId)
        {
            TokenId tokenId = TokenId.Parse(reader.GetString(0));
            SceneId sceneId = SceneId.Parse(reader.GetString(1));
            var position = new TokenPosition(reader.GetDouble(2), reader.GetDouble(3));
            UserId controllerUserId = UserId.Parse(reader.GetString(4));
            long revision = reader.GetInt64(5);
            UtcInstant createdAt = UtcInstant.Parse(reader.GetString(6));
            UtcInstant updatedAt = UtcInstant.Parse(reader.GetString(7));
            return new TokenRecord(tokenId, sceneId, campaignId, position, controllerUserId, revision, createdAt, updatedAt);
        }

        private static Result<AssetManifestEntryRecord> ReplayAsset(SqliteConnection connection, SqliteTransaction transaction, CommandId commandId, CorrelationId correlationId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT AssetId, RelativePath, Hash, SizeBytes FROM AssetManifestEntries WHERE LastCommandId = $commandId LIMIT 1;";
            select.Parameters.AddWithValue("$commandId", commandId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read())
            {
                return Result<AssetManifestEntryRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId));
            }

            AssetId assetId = AssetId.Parse(reader.GetString(0));
            return Result<AssetManifestEntryRecord>.Success(new AssetManifestEntryRecord(assetId, reader.GetString(1), reader.GetString(2), reader.GetInt64(3)));
        }

        private static SqliteConnection OpenConnection(string campaignRootPath)
        {
            string dbPath = Path.Combine(campaignRootPath, "campaign.db");
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

        private static void EnsureSceneTokenTables(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS Scene (
    SceneId TEXT PRIMARY KEY,
    CampaignId TEXT NOT NULL,
    Name TEXT NOT NULL,
    Status TEXT NOT NULL,
    Revision INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    LastCommandId TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS Token (
    TokenId TEXT PRIMARY KEY,
    SceneId TEXT NOT NULL,
    CampaignId TEXT NOT NULL,
    PositionX REAL NOT NULL,
    PositionY REAL NOT NULL,
    ControllerUserId TEXT NOT NULL,
    Revision INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    LastCommandId TEXT NOT NULL
);";
            command.ExecuteNonQuery();
        }

        private static string ToLowerHex(byte[] bytes)
        {
            var builder = new System.Text.StringBuilder(bytes.Length * 2);
            for (int index = 0; index < bytes.Length; index++)
            {
                builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static string JsonString(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static bool SceneExists(SqliteConnection connection, SceneId sceneId)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM Scene WHERE SceneId = $sceneId LIMIT 1;";
            command.Parameters.AddWithValue("$sceneId", sceneId.ToString());
            object? result = command.ExecuteScalar();
            return result != null;
        }
    }
}
