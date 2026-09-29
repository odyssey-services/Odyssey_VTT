using System;
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
    /// SLICE-10 Block 3: SQLite implementation of <see cref="ITokenVisionRepository"/>, a separate
    /// file/table by exact precedent of <see cref="SqliteObstacleRepository"/> (one aggregate, one
    /// file -- not folded into <see cref="SqliteSceneRepository"/>, which this task only point-edits
    /// at <see cref="SqliteSceneRepository.CreateToken"/> to seed a default row; see that method's
    /// own updated remarks). Each method opens its own short-lived connection (ADR-011 section 7.1
    /// PRAGMA profile) and commits through <see cref="SqliteSavingPipeline"/> (ADR-012 section 5).
    /// No <see cref="ICampaignRepository"/> dependency -- authorization lives entirely in
    /// <see cref="Odyssey.Application.Board.TokenVisionService"/>, by exact precedent of
    /// <see cref="SqliteObstacleRepository"/>.
    /// </summary>
    public sealed class SqliteTokenVisionRepository : ITokenVisionRepository
    {
        private readonly IWallClock _clock;
        private readonly SqliteSavingPipeline _pipeline;

        public SqliteTokenVisionRepository(IWallClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _pipeline = new SqliteSavingPipeline(clock);
        }

        public Result<TokenVisionSettingsRecord> GetVisionSettings(CampaignHandle campaign, TokenId tokenId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!tokenId.IsValid) throw new ArgumentException("TokenId is required.", nameof(tokenId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureTokenVisionSettingsTable(connection);

                using var select = connection.CreateCommand();
                select.CommandText = "SELECT TokenId, SceneId, FacingDegrees, FovAngleDegrees, ViewDistance, Revision, CreatedAt, UpdatedAt FROM TokenVisionSettings WHERE TokenId = $tokenId LIMIT 1;";
                select.Parameters.AddWithValue("$tokenId", tokenId.ToString());
                using SqliteDataReader reader = select.ExecuteReader();
                if (!reader.Read())
                {
                    return Result<TokenVisionSettingsRecord>.Failure(TokenVisionFailures.NotFound(correlationId));
                }

                return Result<TokenVisionSettingsRecord>.Success(ReadRecord(reader, campaign.CampaignId));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<TokenVisionSettingsRecord>.Failure(TokenVisionFailures.IoFailed(correlationId));
            }
        }

        public Result<TokenVisionSettingsRecord> SetFacing(CampaignHandle campaign, TokenId tokenId, double facingDegrees, long expectedRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (!double.IsFinite(facingDegrees)) throw new ArgumentException("FacingDegrees must be finite.", nameof(facingDegrees));
            return ApplyUpdate(campaign, tokenId, expectedRevision, commandId, correlationId,
                (current) => (facingDegrees, current.FovAngleDegrees, current.ViewDistance));
        }

        public Result<TokenVisionSettingsRecord> SetVisionParameters(CampaignHandle campaign, TokenId tokenId, double fovAngleDegrees, double viewDistance, long expectedRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (!double.IsFinite(fovAngleDegrees) || fovAngleDegrees < 0) throw new ArgumentException("FovAngleDegrees must be finite and non-negative.", nameof(fovAngleDegrees));
            if (!double.IsFinite(viewDistance) || viewDistance < 0) throw new ArgumentException("ViewDistance must be finite and non-negative.", nameof(viewDistance));
            return ApplyUpdate(campaign, tokenId, expectedRevision, commandId, correlationId,
                (current) => (current.FacingDegrees, fovAngleDegrees, viewDistance));
        }

        private Result<TokenVisionSettingsRecord> ApplyUpdate(CampaignHandle campaign, TokenId tokenId, long expectedRevision, CommandId commandId, CorrelationId correlationId, Func<TokenVisionSettingsRecord, (double Facing, double Fov, double Range)> next)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!tokenId.IsValid) throw new ArgumentException("TokenId is required.", nameof(tokenId));
            if (expectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureTokenVisionSettingsTable(connection);

                return _pipeline.Execute(
                    connection,
                    campaign.CampaignId,
                    commandId,
                    correlationId,
                    tryReplay: transaction => ReplayRecord(connection, transaction, tokenId, campaign.CampaignId, correlationId),
                    apply: transaction =>
                    {
                        TokenVisionSettingsRecord current;
                        using (var select = connection.CreateCommand())
                        {
                            select.Transaction = transaction;
                            select.CommandText = "SELECT TokenId, SceneId, FacingDegrees, FovAngleDegrees, ViewDistance, Revision, CreatedAt, UpdatedAt FROM TokenVisionSettings WHERE TokenId = $tokenId LIMIT 1;";
                            select.Parameters.AddWithValue("$tokenId", tokenId.ToString());
                            using SqliteDataReader reader = select.ExecuteReader();
                            if (!reader.Read())
                            {
                                return Result<PipelineWrite<TokenVisionSettingsRecord>>.Failure(TokenVisionFailures.NotFound(correlationId));
                            }

                            current = ReadRecord(reader, campaign.CampaignId);
                        }

                        // ADR-002 section 10.2: the final, atomic optimistic-concurrency guard, mirroring SqliteObstacleRepository.ToggleDoorState.
                        if (current.Revision != expectedRevision)
                        {
                            return Result<PipelineWrite<TokenVisionSettingsRecord>>.Failure(TokenVisionFailures.RevisionConflict(correlationId));
                        }

                        (double facing, double fov, double range) = next(current);
                        UtcInstant now = _clock.GetUtcNow();
                        long newRevision = current.Revision + 1;

                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText = "UPDATE TokenVisionSettings SET FacingDegrees = $facing, FovAngleDegrees = $fov, ViewDistance = $range, Revision = $revision, UpdatedAt = $updatedAt, LastCommandId = $lastCommandId WHERE TokenId = $tokenId;";
                            update.Parameters.AddWithValue("$facing", facing);
                            update.Parameters.AddWithValue("$fov", fov);
                            update.Parameters.AddWithValue("$range", range);
                            update.Parameters.AddWithValue("$revision", newRevision);
                            update.Parameters.AddWithValue("$updatedAt", now.ToString());
                            update.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            update.Parameters.AddWithValue("$tokenId", tokenId.ToString());
                            update.ExecuteNonQuery();
                        }

                        var record = new TokenVisionSettingsRecord(tokenId, current.SceneId, campaign.CampaignId, facing, fov, range, newRevision, current.CreatedAt, now);
                        string payloadJson = "{\"tokenId\":\"" + tokenId + "\",\"facingDegrees\":" + facing.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
                        return Result<PipelineWrite<TokenVisionSettingsRecord>>.Success(new PipelineWrite<TokenVisionSettingsRecord>(
                            record, "odyssey.persistence.token_vision_updated", payloadJson, tokenId.ToString(),
                            aggregateType: "token_vision", aggregateId: tokenId.ToString(), aggregateRevision: newRevision));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<TokenVisionSettingsRecord>.Failure(TokenVisionFailures.IoFailed(correlationId));
            }
        }

        private static Result<TokenVisionSettingsRecord> ReplayRecord(SqliteConnection connection, SqliteTransaction transaction, TokenId tokenId, CampaignId campaignId, CorrelationId correlationId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT TokenId, SceneId, FacingDegrees, FovAngleDegrees, ViewDistance, Revision, CreatedAt, UpdatedAt FROM TokenVisionSettings WHERE TokenId = $tokenId LIMIT 1;";
            select.Parameters.AddWithValue("$tokenId", tokenId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read())
            {
                return Result<TokenVisionSettingsRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId));
            }

            return Result<TokenVisionSettingsRecord>.Success(ReadRecord(reader, campaignId));
        }

        private static TokenVisionSettingsRecord ReadRecord(SqliteDataReader reader, CampaignId campaignId)
        {
            return new TokenVisionSettingsRecord(
                TokenId.Parse(reader.GetString(0)), SceneId.Parse(reader.GetString(1)), campaignId,
                reader.GetDouble(2), reader.GetDouble(3), reader.GetDouble(4),
                reader.GetInt64(5), UtcInstant.Parse(reader.GetString(6)), UtcInstant.Parse(reader.GetString(7)));
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

        /// <summary>`CREATE TABLE IF NOT EXISTS`, by exact precedent of `SqliteObstacleRepository.EnsureObstacleTable` -- safe for a brand-new table applied to an existing campaign database. `internal` (not `private`) so `SqliteSceneRepository.CreateToken`'s own point-edit (seeding a default row atomically in the same transaction) can call it -- see that method's own updated remarks for why.</summary>
        internal static void EnsureTokenVisionSettingsTable(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS TokenVisionSettings (
    TokenId TEXT PRIMARY KEY,
    SceneId TEXT NOT NULL,
    CampaignId TEXT NOT NULL,
    FacingDegrees REAL NOT NULL,
    FovAngleDegrees REAL NOT NULL,
    ViewDistance REAL NOT NULL,
    Revision INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    LastCommandId TEXT NOT NULL
);";
            command.ExecuteNonQuery();
        }
    }
}
