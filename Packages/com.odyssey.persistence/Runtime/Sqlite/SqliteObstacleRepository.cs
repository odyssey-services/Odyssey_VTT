using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Geometry;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Persistence.Sqlite
{
    /// <summary>
    /// SLICE-10 Block 2: SQLite implementation of <see cref="IObstacleRepository"/>, a separate file/
    /// table by exact precedent of <see cref="SqliteInventoryRepository"/>/<see cref="SqliteActiveEffectRepository"/>
    /// (one aggregate, one file -- not folded into <see cref="SqliteSceneRepository"/>, which is not
    /// touched by this task). Each method opens its own short-lived connection (ADR-011 section 7.1
    /// PRAGMA profile) and commits through <see cref="SqliteSavingPipeline"/> (ADR-012 section 5),
    /// exactly as every other repository here does. No <see cref="Odyssey.Application.Persistence.ICampaignRepository"/>
    /// dependency -- authorization lives entirely in
    /// <see cref="Odyssey.Application.Board.ObstacleAuthoringService"/>, by exact precedent of
    /// <c>SqliteContentCatalogRepository</c> (confirmed by ODY-S10-104 recon: catalog reads authorize
    /// nowhere in the Persistence layer, and this repository follows the same shape).
    /// </summary>
    public sealed class SqliteObstacleRepository : IObstacleRepository
    {
        private readonly IWallClock _clock;
        private readonly SqliteSavingPipeline _pipeline;

        public SqliteObstacleRepository(IWallClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _pipeline = new SqliteSavingPipeline(clock);
        }

        public Result<ObstacleRecord> CreateObstacle(CampaignHandle campaign, SceneId sceneId, ObstacleKind kind, double x1, double y1, double x2, double y2, CommandId commandId, CorrelationId correlationId, long? maxHp = null, long protection = 0)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));
            if (!BoardGeometry.IsFinite(x1, y1) || !BoardGeometry.IsFinite(x2, y2)) throw new ArgumentException("Obstacle endpoints must be finite.");
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (maxHp.HasValue && maxHp.Value <= 0) throw new ArgumentOutOfRangeException(nameof(maxHp));
            if (!maxHp.HasValue && protection != 0) throw new ArgumentException("Protection is only meaningful together with MaxHp.", nameof(protection));
            if (protection < 0) throw new ArgumentOutOfRangeException(nameof(protection));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureObstacleTable(connection);
                EnsureObstacleDurabilityTable(connection);
                UtcInstant now = _clock.GetUtcNow();

                return _pipeline.Execute(
                    connection,
                    campaign.CampaignId,
                    commandId,
                    correlationId,
                    tryReplay: transaction => ReplayObstacle(connection, transaction, "ObstacleId = (SELECT ObstacleId FROM Obstacle WHERE LastCommandId = $commandId LIMIT 1)", campaign.CampaignId, commandId, correlationId),
                    apply: transaction =>
                    {
                        ObstacleId obstacleId = ObstacleId.NewId(now);
                        const long revision = 1L;
                        // Door starts closed (IsOpen = false); Wall/Window store NULL -- IsOpen is not meaningful for them (ObstacleGeometry).
                        object isOpenParam = kind == ObstacleKind.Door ? (object)0 : DBNull.Value;

                        using (var insert = connection.CreateCommand())
                        {
                            insert.Transaction = transaction;
                            insert.CommandText = "INSERT INTO Obstacle (ObstacleId, SceneId, CampaignId, Kind, X1, Y1, X2, Y2, IsOpen, Revision, CreatedAt, UpdatedAt, LastCommandId) " +
                                                  "VALUES ($obstacleId, $sceneId, $campaignId, $kind, $x1, $y1, $x2, $y2, $isOpen, $revision, $createdAt, $updatedAt, $lastCommandId);";
                            insert.Parameters.AddWithValue("$obstacleId", obstacleId.ToString());
                            insert.Parameters.AddWithValue("$sceneId", sceneId.ToString());
                            insert.Parameters.AddWithValue("$campaignId", campaign.CampaignId.ToString());
                            insert.Parameters.AddWithValue("$kind", kind.ToString());
                            insert.Parameters.AddWithValue("$x1", x1);
                            insert.Parameters.AddWithValue("$y1", y1);
                            insert.Parameters.AddWithValue("$x2", x2);
                            insert.Parameters.AddWithValue("$y2", y2);
                            insert.Parameters.AddWithValue("$isOpen", isOpenParam);
                            insert.Parameters.AddWithValue("$revision", revision);
                            insert.Parameters.AddWithValue("$createdAt", now.ToString());
                            insert.Parameters.AddWithValue("$updatedAt", now.ToString());
                            insert.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            insert.ExecuteNonQuery();
                        }

                        if (maxHp.HasValue)
                        {
                            using var insertDurability = connection.CreateCommand();
                            insertDurability.Transaction = transaction;
                            insertDurability.CommandText = "INSERT INTO ObstacleDurability (ObstacleId, CampaignId, MaxHp, CurrentHp, Protection, IsDestroyed, Revision, CreatedAt, UpdatedAt, LastCommandId) " +
                                                            "VALUES ($obstacleId, $campaignId, $maxHp, $maxHp, $protection, 0, 1, $createdAt, $updatedAt, $lastCommandId);";
                            insertDurability.Parameters.AddWithValue("$obstacleId", obstacleId.ToString());
                            insertDurability.Parameters.AddWithValue("$campaignId", campaign.CampaignId.ToString());
                            insertDurability.Parameters.AddWithValue("$maxHp", maxHp.Value);
                            insertDurability.Parameters.AddWithValue("$protection", protection);
                            insertDurability.Parameters.AddWithValue("$createdAt", now.ToString());
                            insertDurability.Parameters.AddWithValue("$updatedAt", now.ToString());
                            insertDurability.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            insertDurability.ExecuteNonQuery();
                        }

                        bool? isOpen = kind == ObstacleKind.Door ? (bool?)false : null;
                        var record = new ObstacleRecord(obstacleId, sceneId, campaign.CampaignId, kind, x1, y1, x2, y2, isOpen, revision, now, now);
                        string payloadJson = "{\"obstacleId\":\"" + obstacleId + "\",\"sceneId\":\"" + sceneId + "\",\"kind\":\"" + kind + "\"}";
                        return Result<PipelineWrite<ObstacleRecord>>.Success(new PipelineWrite<ObstacleRecord>(
                            record, "odyssey.persistence.obstacle_created", payloadJson, obstacleId.ToString(),
                            aggregateType: "obstacle", aggregateId: obstacleId.ToString(), aggregateRevision: revision));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<ObstacleRecord>.Failure(ObstacleFailures.IoFailed(correlationId));
            }
        }

        public Result<ObstacleRecord> ToggleDoorState(CampaignHandle campaign, ObstacleId obstacleId, bool isOpen, long expectedRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!obstacleId.IsValid) throw new ArgumentException("ObstacleId is required.", nameof(obstacleId));
            if (expectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureObstacleTable(connection);

                return _pipeline.Execute(
                    connection,
                    campaign.CampaignId,
                    commandId,
                    correlationId,
                    tryReplay: transaction => ReplayObstacle(connection, transaction, "ObstacleId = $obstacleId", campaign.CampaignId, commandId, correlationId, obstacleId),
                    apply: transaction =>
                    {
                        SceneId sceneId;
                        string kindText;
                        long previousRevision;
                        UtcInstant createdAt;
                        double x1, y1, x2, y2;
                        using (var select = connection.CreateCommand())
                        {
                            select.Transaction = transaction;
                            select.CommandText = "SELECT SceneId, Kind, X1, Y1, X2, Y2, Revision, CreatedAt FROM Obstacle WHERE ObstacleId = $obstacleId LIMIT 1;";
                            select.Parameters.AddWithValue("$obstacleId", obstacleId.ToString());
                            using SqliteDataReader reader = select.ExecuteReader();
                            if (!reader.Read())
                            {
                                return Result<PipelineWrite<ObstacleRecord>>.Failure(ObstacleFailures.NotFound(correlationId));
                            }

                            if (!SceneId.TryParse(reader.GetString(0), out sceneId))
                            {
                                return Result<PipelineWrite<ObstacleRecord>>.Failure(ObstacleFailures.IoFailed(correlationId));
                            }

                            kindText = reader.GetString(1);
                            x1 = reader.GetDouble(2);
                            y1 = reader.GetDouble(3);
                            x2 = reader.GetDouble(4);
                            y2 = reader.GetDouble(5);
                            previousRevision = reader.GetInt64(6);
                            createdAt = UtcInstant.Parse(reader.GetString(7));
                        }

                        if (!Enum.TryParse(kindText, out ObstacleKind kind) || kind != ObstacleKind.Door)
                        {
                            return Result<PipelineWrite<ObstacleRecord>>.Failure(ObstacleFailures.ToggleNotADoor(correlationId));
                        }

                        // ADR-002 section 10.2: the final, atomic optimistic-concurrency guard --
                        // independent of any Application-layer pre-check, mirroring SqliteSceneRepository.MoveToken.
                        if (previousRevision != expectedRevision)
                        {
                            return Result<PipelineWrite<ObstacleRecord>>.Failure(ObstacleFailures.RevisionConflict(correlationId));
                        }

                        UtcInstant now = _clock.GetUtcNow();
                        long newRevision = previousRevision + 1;

                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText = "UPDATE Obstacle SET IsOpen = $isOpen, Revision = $revision, UpdatedAt = $updatedAt, LastCommandId = $lastCommandId WHERE ObstacleId = $obstacleId;";
                            update.Parameters.AddWithValue("$isOpen", isOpen ? 1 : 0);
                            update.Parameters.AddWithValue("$revision", newRevision);
                            update.Parameters.AddWithValue("$updatedAt", now.ToString());
                            update.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            update.Parameters.AddWithValue("$obstacleId", obstacleId.ToString());
                            update.ExecuteNonQuery();
                        }

                        var record = new ObstacleRecord(obstacleId, sceneId, campaign.CampaignId, kind, x1, y1, x2, y2, isOpen, newRevision, createdAt, now);
                        string payloadJson = "{\"obstacleId\":\"" + obstacleId + "\",\"isOpen\":" + (isOpen ? "true" : "false") + "}";
                        return Result<PipelineWrite<ObstacleRecord>>.Success(new PipelineWrite<ObstacleRecord>(
                            record, "odyssey.persistence.obstacle_door_toggled", payloadJson, obstacleId.ToString(),
                            aggregateType: "obstacle", aggregateId: obstacleId.ToString(), aggregateRevision: newRevision));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<ObstacleRecord>.Failure(ObstacleFailures.IoFailed(correlationId));
            }
        }

        public Result<IReadOnlyList<ObstacleRecord>> ListObstacles(CampaignHandle campaign, SceneId sceneId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureObstacleTable(connection);
                EnsureObstacleDurabilityTable(connection);

                using var select = connection.CreateCommand();
                // SLICE-10 Block 5 Part B: LEFT JOIN against the optional durability table and exclude a
                // destroyed obstacle -- an obstacle with no durability row (od.ObstacleId IS NULL) is
                // indestructible and always included, by construction.
                select.CommandText = "SELECT o.ObstacleId, o.Kind, o.X1, o.Y1, o.X2, o.Y2, o.IsOpen, o.Revision, o.CreatedAt, o.UpdatedAt " +
                                      "FROM Obstacle o LEFT JOIN ObstacleDurability od ON od.ObstacleId = o.ObstacleId " +
                                      "WHERE o.SceneId = $sceneId AND (od.ObstacleId IS NULL OR od.IsDestroyed = 0) " +
                                      "ORDER BY o.CreatedAt ASC;";
                select.Parameters.AddWithValue("$sceneId", sceneId.ToString());
                using SqliteDataReader reader = select.ExecuteReader();
                var results = new List<ObstacleRecord>();
                while (reader.Read())
                {
                    results.Add(ReadObstacleRecord(reader, sceneId, campaign.CampaignId));
                }

                return Result<IReadOnlyList<ObstacleRecord>>.Success(results);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<IReadOnlyList<ObstacleRecord>>.Failure(ObstacleFailures.IoFailed(correlationId));
            }
        }

        public Result<ObstacleDurabilityRecord> ApplyObstacleDamage(CampaignHandle campaign, ObstacleId obstacleId, long damageAmount, long expectedRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!obstacleId.IsValid) throw new ArgumentException("ObstacleId is required.", nameof(obstacleId));
            if (damageAmount < 0) throw new ArgumentOutOfRangeException(nameof(damageAmount));
            if (expectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureObstacleTable(connection);
                EnsureObstacleDurabilityTable(connection);

                return _pipeline.Execute(
                    connection,
                    campaign.CampaignId,
                    commandId,
                    correlationId,
                    tryReplay: transaction => ReplayDurability(connection, transaction, "ObstacleId = $obstacleId AND LastCommandId = $commandId", campaign.CampaignId, commandId, correlationId, obstacleId),
                    apply: transaction =>
                    {
                        ObstacleDurabilityRecord? current = ReadDurability(connection, transaction, obstacleId, campaign.CampaignId);
                        if (current == null)
                        {
                            return Result<PipelineWrite<ObstacleDurabilityRecord>>.Failure(ObstacleFailures.DurabilityNotConfigured(correlationId));
                        }

                        if (current.Revision != expectedRevision)
                        {
                            return Result<PipelineWrite<ObstacleDurabilityRecord>>.Failure(ObstacleFailures.RevisionConflict(correlationId));
                        }

                        long effectiveDamage = Math.Max(0, damageAmount - current.Protection);
                        long newCurrentHp = Math.Max(0, current.CurrentHp - effectiveDamage);
                        bool isDestroyed = newCurrentHp == 0;
                        long newRevision = current.Revision + 1;
                        UtcInstant now = _clock.GetUtcNow();

                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText = "UPDATE ObstacleDurability SET CurrentHp = $currentHp, IsDestroyed = $isDestroyed, Revision = $revision, UpdatedAt = $updatedAt, LastCommandId = $lastCommandId WHERE ObstacleId = $obstacleId;";
                            update.Parameters.AddWithValue("$currentHp", newCurrentHp);
                            update.Parameters.AddWithValue("$isDestroyed", isDestroyed ? 1 : 0);
                            update.Parameters.AddWithValue("$revision", newRevision);
                            update.Parameters.AddWithValue("$updatedAt", now.ToString());
                            update.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            update.Parameters.AddWithValue("$obstacleId", obstacleId.ToString());
                            update.ExecuteNonQuery();
                        }

                        var record = new ObstacleDurabilityRecord(obstacleId, campaign.CampaignId, current.MaxHp, newCurrentHp, current.Protection, isDestroyed, newRevision, current.CreatedAt, now);
                        string payloadJson = "{\"obstacleId\":\"" + obstacleId + "\",\"currentHp\":" + newCurrentHp + ",\"isDestroyed\":" + (isDestroyed ? "true" : "false") + "}";
                        return Result<PipelineWrite<ObstacleDurabilityRecord>>.Success(new PipelineWrite<ObstacleDurabilityRecord>(
                            record, "odyssey.persistence.obstacle_damaged", payloadJson, obstacleId.ToString(),
                            aggregateType: "obstacle_durability", aggregateId: obstacleId.ToString(), aggregateRevision: newRevision));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<ObstacleDurabilityRecord>.Failure(ObstacleFailures.IoFailed(correlationId));
            }
        }

        public Result<ObstacleDurabilityRecord> GetObstacleDurability(CampaignHandle campaign, ObstacleId obstacleId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!obstacleId.IsValid) throw new ArgumentException("ObstacleId is required.", nameof(obstacleId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureObstacleTable(connection);
                EnsureObstacleDurabilityTable(connection);

                ObstacleDurabilityRecord? record = ReadDurability(connection, null, obstacleId, campaign.CampaignId);
                return record == null
                    ? Result<ObstacleDurabilityRecord>.Failure(ObstacleFailures.DurabilityNotConfigured(correlationId))
                    : Result<ObstacleDurabilityRecord>.Success(record);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<ObstacleDurabilityRecord>.Failure(ObstacleFailures.IoFailed(correlationId));
            }
        }

        private static Result<ObstacleRecord> ReplayObstacle(SqliteConnection connection, SqliteTransaction transaction, string whereClause, CampaignId campaignId, CommandId commandId, CorrelationId correlationId, ObstacleId? knownObstacleId = null)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT ObstacleId, SceneId, Kind, X1, Y1, X2, Y2, IsOpen, Revision, CreatedAt, UpdatedAt FROM Obstacle WHERE " + whereClause + " LIMIT 1;";
            if (knownObstacleId.HasValue)
            {
                select.Parameters.AddWithValue("$obstacleId", knownObstacleId.Value.ToString());
            }
            else
            {
                select.Parameters.AddWithValue("$commandId", commandId.ToString());
            }

            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read())
            {
                return Result<ObstacleRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId));
            }

            SceneId sceneId = SceneId.Parse(reader.GetString(1));
            return Result<ObstacleRecord>.Success(new ObstacleRecord(
                ObstacleId.Parse(reader.GetString(0)), sceneId, campaignId,
                Enum.Parse<ObstacleKind>(reader.GetString(2)),
                reader.GetDouble(3), reader.GetDouble(4), reader.GetDouble(5), reader.GetDouble(6),
                reader.IsDBNull(7) ? (bool?)null : reader.GetInt64(7) != 0,
                reader.GetInt64(8), UtcInstant.Parse(reader.GetString(9)), UtcInstant.Parse(reader.GetString(10))));
        }

        private static ObstacleDurabilityRecord? ReadDurability(SqliteConnection connection, SqliteTransaction? transaction, ObstacleId obstacleId, CampaignId campaignId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT MaxHp, CurrentHp, Protection, IsDestroyed, Revision, CreatedAt, UpdatedAt FROM ObstacleDurability WHERE ObstacleId = $obstacleId LIMIT 1;";
            select.Parameters.AddWithValue("$obstacleId", obstacleId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            return new ObstacleDurabilityRecord(
                obstacleId, campaignId, reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3) != 0,
                reader.GetInt64(4), UtcInstant.Parse(reader.GetString(5)), UtcInstant.Parse(reader.GetString(6)));
        }

        private static Result<ObstacleDurabilityRecord> ReplayDurability(SqliteConnection connection, SqliteTransaction transaction, string whereClause, CampaignId campaignId, CommandId commandId, CorrelationId correlationId, ObstacleId obstacleId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT MaxHp, CurrentHp, Protection, IsDestroyed, Revision, CreatedAt, UpdatedAt FROM ObstacleDurability WHERE " + whereClause + " LIMIT 1;";
            select.Parameters.AddWithValue("$obstacleId", obstacleId.ToString());
            select.Parameters.AddWithValue("$commandId", commandId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read())
            {
                return Result<ObstacleDurabilityRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId));
            }

            return Result<ObstacleDurabilityRecord>.Success(new ObstacleDurabilityRecord(
                obstacleId, campaignId, reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3) != 0,
                reader.GetInt64(4), UtcInstant.Parse(reader.GetString(5)), UtcInstant.Parse(reader.GetString(6))));
        }

        private static ObstacleRecord ReadObstacleRecord(SqliteDataReader reader, SceneId sceneId, CampaignId campaignId)
        {
            return new ObstacleRecord(
                ObstacleId.Parse(reader.GetString(0)), sceneId, campaignId,
                Enum.Parse<ObstacleKind>(reader.GetString(1)),
                reader.GetDouble(2), reader.GetDouble(3), reader.GetDouble(4), reader.GetDouble(5),
                reader.IsDBNull(6) ? (bool?)null : reader.GetInt64(6) != 0,
                reader.GetInt64(7), UtcInstant.Parse(reader.GetString(8)), UtcInstant.Parse(reader.GetString(9)));
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

        /// <summary>
        /// SLICE-10 Block 2: `CREATE TABLE IF NOT EXISTS`, the same unversioned-schema convention every
        /// other `Ensure*Tables` method here already uses (safe for a brand-new table applied to an
        /// existing campaign database -- see the task contract section 0 for why a new column on an
        /// existing table would NOT be safe the same way, and why this task never does that).
        /// </summary>
        private static void EnsureObstacleTable(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS Obstacle (
    ObstacleId TEXT PRIMARY KEY,
    SceneId TEXT NOT NULL,
    CampaignId TEXT NOT NULL,
    Kind TEXT NOT NULL,
    X1 REAL NOT NULL,
    Y1 REAL NOT NULL,
    X2 REAL NOT NULL,
    Y2 REAL NOT NULL,
    IsOpen INTEGER,
    Revision INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    LastCommandId TEXT NOT NULL
);";
            command.ExecuteNonQuery();
        }

        /// <summary>SLICE-10 Block 5 Part B: `CREATE TABLE IF NOT EXISTS`, one row per obstacle that was created with a `maxHp` -- optional, not a column on `Obstacle` itself, the same "a new table, never `ALTER TABLE`" convention every prior SLICE-10 table already follows. No foreign key to `Obstacle` (this codebase scopes tables by plain id fields, not database foreign keys, by `EnsureSceneTokenTables`'s own established precedent).</summary>
        private static void EnsureObstacleDurabilityTable(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS ObstacleDurability (
    ObstacleId TEXT PRIMARY KEY,
    CampaignId TEXT NOT NULL,
    MaxHp INTEGER NOT NULL,
    CurrentHp INTEGER NOT NULL,
    Protection INTEGER NOT NULL,
    IsDestroyed INTEGER NOT NULL,
    Revision INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    LastCommandId TEXT NOT NULL
);";
            command.ExecuteNonQuery();
        }
    }
}
