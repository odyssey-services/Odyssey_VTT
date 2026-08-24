using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Persistence.Sqlite
{
    /// <summary>
    /// ODY-S01-008 implementation of ISceneRepository. Each method opens its own
    /// short-lived connection under the ADR-011 section 7.1 PRAGMA profile and
    /// commits its write in a single transaction. This task does not implement
    /// ADR-011 section 7.2's single logical write-queue-per-campaign serialization
    /// across repository types (Campaign/Scene both currently open independent
    /// connections) -- that full write-queue orchestration is ODY-S01-009 (Saving
    /// Pipeline) scope, which owns the command/transaction pipeline all writes will
    /// eventually route through.
    /// </summary>
    public sealed class SqliteSceneRepository : ISceneRepository
    {
        private const string AssetsObjectsRelativeDirectory = "Assets/Objects";
        private readonly IWallClock _clock;

        public SqliteSceneRepository(IWallClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public Result<SceneRecord> CreateScene(CampaignHandle campaign, string sceneName, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (string.IsNullOrWhiteSpace(sceneName) || sceneName.Length > 128)
            {
                return Result<SceneRecord>.Failure(PersistenceFailures.SceneIoFailed(correlationId));
            }

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureSceneTokenTables(connection);

                UtcInstant now = _clock.GetUtcNow();
                SceneId sceneId = SceneId.NewId(now);
                const long revision = 1L;
                const string status = "Draft";

                using (var insert = connection.CreateCommand())
                {
                    insert.CommandText = "INSERT INTO Scene (SceneId, CampaignId, Name, Status, Revision, CreatedAt, UpdatedAt) " +
                                          "VALUES ($sceneId, $campaignId, $name, $status, $revision, $createdAt, $updatedAt);";
                    insert.Parameters.AddWithValue("$sceneId", sceneId.ToString());
                    insert.Parameters.AddWithValue("$campaignId", campaign.CampaignId.ToString());
                    insert.Parameters.AddWithValue("$name", sceneName);
                    insert.Parameters.AddWithValue("$status", status);
                    insert.Parameters.AddWithValue("$revision", revision);
                    insert.Parameters.AddWithValue("$createdAt", now.ToString());
                    insert.Parameters.AddWithValue("$updatedAt", now.ToString());
                    insert.ExecuteNonQuery();
                }

                return Result<SceneRecord>.Success(new SceneRecord(sceneId, campaign.CampaignId, sceneName, status, revision, now, now));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<SceneRecord>.Failure(PersistenceFailures.SceneIoFailed(correlationId));
            }
        }

        public Result<TokenRecord> CreateToken(CampaignHandle campaign, SceneId sceneId, TokenPosition initialPosition, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureSceneTokenTables(connection);

                if (!SceneExists(connection, sceneId))
                {
                    return Result<TokenRecord>.Failure(PersistenceFailures.SceneNotFound(correlationId));
                }

                UtcInstant now = _clock.GetUtcNow();
                TokenId tokenId = TokenId.NewId(now);
                const long revision = 1L;

                using (var insert = connection.CreateCommand())
                {
                    insert.CommandText = "INSERT INTO Token (TokenId, SceneId, CampaignId, PositionX, PositionY, Revision, CreatedAt, UpdatedAt) " +
                                          "VALUES ($tokenId, $sceneId, $campaignId, $x, $y, $revision, $createdAt, $updatedAt);";
                    insert.Parameters.AddWithValue("$tokenId", tokenId.ToString());
                    insert.Parameters.AddWithValue("$sceneId", sceneId.ToString());
                    insert.Parameters.AddWithValue("$campaignId", campaign.CampaignId.ToString());
                    insert.Parameters.AddWithValue("$x", initialPosition.X);
                    insert.Parameters.AddWithValue("$y", initialPosition.Y);
                    insert.Parameters.AddWithValue("$revision", revision);
                    insert.Parameters.AddWithValue("$createdAt", now.ToString());
                    insert.Parameters.AddWithValue("$updatedAt", now.ToString());
                    insert.ExecuteNonQuery();
                }

                return Result<TokenRecord>.Success(new TokenRecord(tokenId, sceneId, campaign.CampaignId, initialPosition, revision, now, now));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<TokenRecord>.Failure(PersistenceFailures.SceneIoFailed(correlationId));
            }
        }

        public Result<TokenRecord> MoveToken(CampaignHandle campaign, TokenId tokenId, TokenPosition newPosition, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!tokenId.IsValid) throw new ArgumentException("TokenId is required.", nameof(tokenId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureSceneTokenTables(connection);

                SceneId sceneId;
                long previousRevision;
                UtcInstant createdAt;
                using (var select = connection.CreateCommand())
                {
                    select.CommandText = "SELECT SceneId, Revision, CreatedAt FROM Token WHERE TokenId = $tokenId LIMIT 1;";
                    select.Parameters.AddWithValue("$tokenId", tokenId.ToString());
                    using SqliteDataReader reader = select.ExecuteReader();
                    if (!reader.Read())
                    {
                        return Result<TokenRecord>.Failure(PersistenceFailures.TokenNotFound(correlationId));
                    }

                    if (!SceneId.TryParse(reader.GetString(0), out sceneId))
                    {
                        return Result<TokenRecord>.Failure(PersistenceFailures.SceneIoFailed(correlationId));
                    }

                    previousRevision = reader.GetInt64(1);
                    createdAt = UtcInstant.Parse(reader.GetString(2));
                }

                UtcInstant now = _clock.GetUtcNow();
                long newRevision = previousRevision + 1;

                using (var update = connection.CreateCommand())
                {
                    update.CommandText = "UPDATE Token SET PositionX = $x, PositionY = $y, Revision = $revision, UpdatedAt = $updatedAt WHERE TokenId = $tokenId;";
                    update.Parameters.AddWithValue("$x", newPosition.X);
                    update.Parameters.AddWithValue("$y", newPosition.Y);
                    update.Parameters.AddWithValue("$revision", newRevision);
                    update.Parameters.AddWithValue("$updatedAt", now.ToString());
                    update.Parameters.AddWithValue("$tokenId", tokenId.ToString());
                    update.ExecuteNonQuery();
                }

                return Result<TokenRecord>.Success(new TokenRecord(tokenId, sceneId, campaign.CampaignId, newPosition, newRevision, createdAt, now));
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
                    select.CommandText = "SELECT TokenId, PositionX, PositionY, Revision, CreatedAt, UpdatedAt FROM Token WHERE SceneId = $sceneId ORDER BY CreatedAt;";
                    select.Parameters.AddWithValue("$sceneId", sceneId.ToString());
                    using SqliteDataReader reader = select.ExecuteReader();
                    while (reader.Read())
                    {
                        TokenId tokenId = TokenId.Parse(reader.GetString(0));
                        var position = new TokenPosition(reader.GetDouble(1), reader.GetDouble(2));
                        long revision = reader.GetInt64(3);
                        UtcInstant createdAt = UtcInstant.Parse(reader.GetString(4));
                        UtcInstant updatedAt = UtcInstant.Parse(reader.GetString(5));
                        tokens.Add(new TokenRecord(tokenId, sceneId, campaign.CampaignId, position, revision, createdAt, updatedAt));
                    }
                }

                return Result<IReadOnlyList<TokenRecord>>.Success(tokens);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<IReadOnlyList<TokenRecord>>.Failure(PersistenceFailures.SceneIoFailed(correlationId));
            }
        }

        public Result<AssetManifestEntryRecord> RegisterAsset(CampaignHandle campaign, string sourceFilePath, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                return Result<AssetManifestEntryRecord>.Failure(PersistenceFailures.SceneIoFailed(correlationId));
            }

            try
            {
                // ADR-011 section 4.2: only the relative, in-campaign path is ever
                // persisted; the caller-supplied absolute source path exists only in
                // this local, transient import step and is never written to the
                // database or manifest.
                string objectsDirectory = Path.Combine(campaign.RootPath, AssetsObjectsRelativeDirectory.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(objectsDirectory);
                string fileName = Path.GetFileName(sourceFilePath);
                string destinationPath = Path.Combine(objectsDirectory, fileName);
                File.Copy(sourceFilePath, destinationPath, overwrite: false);

                string sha256Hash;
                using (var sha = SHA256.Create())
                using (var stream = File.OpenRead(destinationPath))
                {
                    byte[] hashBytes = sha.ComputeHash(stream);
                    sha256Hash = ToLowerHex(hashBytes);
                }

                long sizeBytes = new FileInfo(destinationPath).Length;
                string relativePath = (AssetsObjectsRelativeDirectory + "/" + fileName);

                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                UtcInstant now = _clock.GetUtcNow();
                AssetId assetId = AssetId.NewId(now);

                using (var insert = connection.CreateCommand())
                {
                    insert.CommandText = "INSERT INTO AssetManifestEntries (AssetId, RelativePath, Hash, SizeBytes) VALUES ($assetId, $relativePath, $hash, $size);";
                    insert.Parameters.AddWithValue("$assetId", assetId.ToString());
                    insert.Parameters.AddWithValue("$relativePath", relativePath);
                    insert.Parameters.AddWithValue("$hash", sha256Hash);
                    insert.Parameters.AddWithValue("$size", sizeBytes);
                    insert.ExecuteNonQuery();
                }

                return Result<AssetManifestEntryRecord>.Success(new AssetManifestEntryRecord(assetId, relativePath, sha256Hash, sizeBytes));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<AssetManifestEntryRecord>.Failure(PersistenceFailures.SceneIoFailed(correlationId));
            }
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
    UpdatedAt TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS Token (
    TokenId TEXT PRIMARY KEY,
    SceneId TEXT NOT NULL,
    CampaignId TEXT NOT NULL,
    PositionX REAL NOT NULL,
    PositionY REAL NOT NULL,
    Revision INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
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
