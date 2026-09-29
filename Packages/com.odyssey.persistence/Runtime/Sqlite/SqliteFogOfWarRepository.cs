using System;
using System.Collections.Generic;
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
    /// SLICE-10 Block 4: SQLite implementation of <see cref="IFogOfWarRepository"/>, a separate file/
    /// table by exact precedent of <see cref="SqliteObstacleRepository"/>/<see cref="SqliteTokenVisionRepository"/>
    /// (one aggregate, one file -- neither <see cref="SqliteSceneRepository"/> nor either of those two
    /// files is touched by this task). Each method opens its own short-lived connection (ADR-011
    /// section 7.1 PRAGMA profile) and commits through <see cref="SqliteSavingPipeline"/> (ADR-012
    /// section 5). No <see cref="Odyssey.Application.Persistence.ICampaignRepository"/> dependency --
    /// authorization (self-scoped-or-MainGM) lives entirely in
    /// <see cref="Odyssey.Application.Board.PlayerVisibilityService"/>, by exact precedent of the other
    /// two SLICE-10 repositories.
    ///
    /// <see cref="RecordReveal"/> is the only writer, and only ever INSERTs a brand-new row -- no
    /// method here ever runs an `UPDATE`/`DELETE` against the `FogReveal` table (persistent map memory
    /// is monotonic, by product decision 2026-09-29).
    /// </summary>
    public sealed class SqliteFogOfWarRepository : IFogOfWarRepository
    {
        private readonly IWallClock _clock;
        private readonly SqliteSavingPipeline _pipeline;

        public SqliteFogOfWarRepository(IWallClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _pipeline = new SqliteSavingPipeline(clock);
        }

        public Result<FogRevealRecord> RecordReveal(CampaignHandle campaign, SceneId sceneId, UserId userId, double centerX, double centerY, double radius, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));
            if (!userId.IsValid) throw new ArgumentException("UserId is required.", nameof(userId));
            if (!double.IsFinite(centerX) || !double.IsFinite(centerY)) throw new ArgumentException("CenterX/CenterY must be finite.");
            if (!double.IsFinite(radius) || radius < 0) throw new ArgumentException("Radius must be finite and non-negative.", nameof(radius));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureFogRevealTable(connection);
                UtcInstant now = _clock.GetUtcNow();

                return _pipeline.Execute(
                    connection,
                    campaign.CampaignId,
                    commandId,
                    correlationId,
                    tryReplay: transaction => ReplayReveal(connection, transaction, campaign.CampaignId, commandId, correlationId),
                    apply: transaction =>
                    {
                        FogRevealId revealId = FogRevealId.NewId(now);

                        using (var insert = connection.CreateCommand())
                        {
                            insert.Transaction = transaction;
                            insert.CommandText = "INSERT INTO FogReveal (RevealId, SceneId, CampaignId, UserId, CenterX, CenterY, Radius, CreatedAt, LastCommandId) " +
                                                  "VALUES ($revealId, $sceneId, $campaignId, $userId, $centerX, $centerY, $radius, $createdAt, $lastCommandId);";
                            insert.Parameters.AddWithValue("$revealId", revealId.ToString());
                            insert.Parameters.AddWithValue("$sceneId", sceneId.ToString());
                            insert.Parameters.AddWithValue("$campaignId", campaign.CampaignId.ToString());
                            insert.Parameters.AddWithValue("$userId", userId.ToString());
                            insert.Parameters.AddWithValue("$centerX", centerX);
                            insert.Parameters.AddWithValue("$centerY", centerY);
                            insert.Parameters.AddWithValue("$radius", radius);
                            insert.Parameters.AddWithValue("$createdAt", now.ToString());
                            insert.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            insert.ExecuteNonQuery();
                        }

                        var record = new FogRevealRecord(revealId, campaign.CampaignId, sceneId, userId, centerX, centerY, radius, now);
                        string payloadJson = "{\"revealId\":\"" + revealId + "\",\"sceneId\":\"" + sceneId + "\",\"userId\":\"" + userId + "\"}";
                        return Result<PipelineWrite<FogRevealRecord>>.Success(new PipelineWrite<FogRevealRecord>(
                            record, "odyssey.persistence.fog_reveal_recorded", payloadJson, revealId.ToString(),
                            aggregateType: "fog_reveal", aggregateId: revealId.ToString(), aggregateRevision: 1));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<FogRevealRecord>.Failure(FogOfWarFailures.IoFailed(correlationId));
            }
        }

        public Result<IReadOnlyList<FogRevealRecord>> ListReveals(CampaignHandle campaign, SceneId sceneId, UserId userId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));
            if (!userId.IsValid) throw new ArgumentException("UserId is required.", nameof(userId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureFogRevealTable(connection);

                using var select = connection.CreateCommand();
                select.CommandText = "SELECT RevealId, CenterX, CenterY, Radius, CreatedAt FROM FogReveal WHERE SceneId = $sceneId AND UserId = $userId ORDER BY CreatedAt ASC;";
                select.Parameters.AddWithValue("$sceneId", sceneId.ToString());
                select.Parameters.AddWithValue("$userId", userId.ToString());
                using SqliteDataReader reader = select.ExecuteReader();
                var results = new List<FogRevealRecord>();
                while (reader.Read())
                {
                    results.Add(new FogRevealRecord(
                        FogRevealId.Parse(reader.GetString(0)), campaign.CampaignId, sceneId, userId,
                        reader.GetDouble(1), reader.GetDouble(2), reader.GetDouble(3),
                        UtcInstant.Parse(reader.GetString(4))));
                }

                return Result<IReadOnlyList<FogRevealRecord>>.Success(results);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<IReadOnlyList<FogRevealRecord>>.Failure(FogOfWarFailures.IoFailed(correlationId));
            }
        }

        private static Result<FogRevealRecord> ReplayReveal(SqliteConnection connection, SqliteTransaction transaction, CampaignId campaignId, CommandId commandId, CorrelationId correlationId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT RevealId, SceneId, UserId, CenterX, CenterY, Radius, CreatedAt FROM FogReveal WHERE LastCommandId = $commandId LIMIT 1;";
            select.Parameters.AddWithValue("$commandId", commandId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read())
            {
                return Result<FogRevealRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId));
            }

            return Result<FogRevealRecord>.Success(new FogRevealRecord(
                FogRevealId.Parse(reader.GetString(0)), campaignId, SceneId.Parse(reader.GetString(1)), UserId.Parse(reader.GetString(2)),
                reader.GetDouble(3), reader.GetDouble(4), reader.GetDouble(5), UtcInstant.Parse(reader.GetString(6))));
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

        /// <summary>`CREATE TABLE IF NOT EXISTS`, the same unversioned-schema convention every other `Ensure*Tables` method in this codebase uses. No `Revision` column -- unlike every other SLICE-10 table, a `FogReveal` row is never updated after insertion, so there is nothing to optimistic-concurrency-gate.</summary>
        private static void EnsureFogRevealTable(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS FogReveal (
    RevealId TEXT PRIMARY KEY,
    SceneId TEXT NOT NULL,
    CampaignId TEXT NOT NULL,
    UserId TEXT NOT NULL,
    CenterX REAL NOT NULL,
    CenterY REAL NOT NULL,
    Radius REAL NOT NULL,
    CreatedAt TEXT NOT NULL,
    LastCommandId TEXT NOT NULL
);";
            command.ExecuteNonQuery();
        }
    }
}
