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

        public Result<ObstacleRecord> CreateObstacle(CampaignHandle campaign, SceneId sceneId, ObstacleKind kind, double x1, double y1, double x2, double y2, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));
            if (!BoardGeometry.IsFinite(x1, y1) || !BoardGeometry.IsFinite(x2, y2)) throw new ArgumentException("Obstacle endpoints must be finite.");
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureObstacleTable(connection);
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

                using var select = connection.CreateCommand();
                select.CommandText = "SELECT ObstacleId, Kind, X1, Y1, X2, Y2, IsOpen, Revision, CreatedAt, UpdatedAt FROM Obstacle WHERE SceneId = $sceneId ORDER BY CreatedAt ASC;";
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
    }
}
