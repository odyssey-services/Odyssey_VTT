using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Odyssey.Application.Checks;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Checks;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Persistence.Sqlite
{
    /// <summary>
    /// ODY-S07-102: the sole SQLite implementation of <see cref="ICheckRepository"/>. Owns a new, standalone
    /// <c>CheckOutcome</c> table -- mirrors <see cref="SqliteActivateAbilityRepository"/>'s own standalone-
    /// table idiom exactly (a root-command apply pipeline, not general Character CRUD). Every write commits
    /// through the shared <see cref="SqliteSavingPipeline"/> (`ADR-012` section 5): the durable
    /// <c>CheckOutcome</c> row, the DomainEvent, and the AppliedCommands idempotency row land in one SQLite
    /// transaction, or none of them do. No compensation logic -- unlike <see cref="SqliteActivateAbilityRepository"/>,
    /// a check has no resource delta or created effect to reverse (`ADR-031` imposes no such requirement).
    /// </summary>
    public sealed class SqliteCheckRepository : ICheckRepository
    {
        private readonly IWallClock _clock;
        private readonly SqliteSavingPipeline _pipeline;

        public SqliteCheckRepository(IWallClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _pipeline = new SqliteSavingPipeline(clock);
        }

        public Result<CheckOutcomeRecord> GetCheckOutcome(CampaignHandle campaign, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureCheckOutcomeTable(connection);
                CheckOutcomeRecord? found = ReadByCommandId(connection, null, commandId);
                return found == null
                    ? Result<CheckOutcomeRecord>.Failure(PersistenceFailures.AttackOutcomeNotFound(correlationId))
                    : Result<CheckOutcomeRecord>.Success(found);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CheckOutcomeRecord>.Failure(PersistenceFailures.AttackOutcomeIoFailed(correlationId));
            }
        }

        public Result<CheckOutcomeRecord> RecordCheckOutcome(CampaignHandle campaign, CharacterId actorId, string formula, long difficultyClass, string diceRollId, CheckResultKind result, bool isNaturalMaximum, SkillDefinitionId? resolvedSkill, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (string.IsNullOrWhiteSpace(formula)) throw new ArgumentException("Formula is required.", nameof(formula));
            if (string.IsNullOrWhiteSpace(diceRollId)) throw new ArgumentException("DiceRollId is required.", nameof(diceRollId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureCheckOutcomeTable(connection);
                UtcInstant now = _clock.GetUtcNow();

                return _pipeline.Execute(
                    connection,
                    campaign.CampaignId,
                    commandId,
                    correlationId,
                    tryReplay: transaction =>
                    {
                        CheckOutcomeRecord? replayed = ReadByCommandId(connection, transaction, commandId);
                        return replayed == null
                            ? Result<CheckOutcomeRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId))
                            : Result<CheckOutcomeRecord>.Success(replayed);
                    },
                    apply: transaction =>
                    {
                        var record = new CheckOutcomeRecord(commandId, campaign.CampaignId, actorId, formula, difficultyClass, diceRollId, result, isNaturalMaximum, resolvedSkill, now);

                        using (var insert = connection.CreateCommand())
                        {
                            insert.Transaction = transaction;
                            insert.CommandText = "INSERT INTO CheckOutcome (CommandId, CampaignId, ActorId, Formula, DifficultyClass, DiceRollId, Result, IsNaturalMaximum, ResolvedSkill, CreatedAt) " +
                                                  "VALUES ($commandId, $campaignId, $actorId, $formula, $difficultyClass, $diceRollId, $result, $isNaturalMaximum, $resolvedSkill, $createdAt);";
                            insert.Parameters.AddWithValue("$commandId", commandId.ToString());
                            insert.Parameters.AddWithValue("$campaignId", campaign.CampaignId.ToString());
                            insert.Parameters.AddWithValue("$actorId", actorId.ToString());
                            insert.Parameters.AddWithValue("$formula", formula);
                            insert.Parameters.AddWithValue("$difficultyClass", difficultyClass);
                            insert.Parameters.AddWithValue("$diceRollId", diceRollId);
                            insert.Parameters.AddWithValue("$result", result.ToString());
                            insert.Parameters.AddWithValue("$isNaturalMaximum", isNaturalMaximum ? 1 : 0);
                            insert.Parameters.AddWithValue("$resolvedSkill", (object?)resolvedSkill?.ToString() ?? DBNull.Value);
                            insert.Parameters.AddWithValue("$createdAt", now.ToString());
                            insert.ExecuteNonQuery();
                        }

                        string payloadJson = "{\"commandId\":\"" + commandId + "\",\"actorId\":\"" + actorId + "\",\"result\":\"" + result + "\"}";
                        return Result<PipelineWrite<CheckOutcomeRecord>>.Success(new PipelineWrite<CheckOutcomeRecord>(
                            record, "odyssey.persistence.check_performed", payloadJson, commandId.ToString(),
                            aggregateType: "check_outcome", aggregateId: commandId.ToString(), aggregateRevision: 1));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CheckOutcomeRecord>.Failure(PersistenceFailures.AttackOutcomeIoFailed(correlationId));
            }
        }

        private static CheckOutcomeRecord? ReadByCommandId(SqliteConnection connection, SqliteTransaction? transaction, CommandId commandId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT CommandId, CampaignId, ActorId, Formula, DifficultyClass, DiceRollId, Result, IsNaturalMaximum, ResolvedSkill, CreatedAt FROM CheckOutcome WHERE CommandId = $commandId LIMIT 1;";
            select.Parameters.AddWithValue("$commandId", commandId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read()) return null;

            return new CheckOutcomeRecord(
                CommandId.Parse(reader.GetString(0)),
                CampaignId.Parse(reader.GetString(1)),
                CharacterId.Parse(reader.GetString(2)),
                reader.GetString(3),
                reader.GetInt64(4),
                reader.GetString(5),
                Enum.Parse<CheckResultKind>(reader.GetString(6)),
                reader.GetInt64(7) != 0,
                reader.IsDBNull(8) ? (SkillDefinitionId?)null : SkillDefinitionId.Parse(reader.GetString(8)),
                UtcInstant.Parse(reader.GetString(9)));
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

        private static void EnsureCheckOutcomeTable(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS CheckOutcome (
    CommandId TEXT PRIMARY KEY,
    CampaignId TEXT NOT NULL,
    ActorId TEXT NOT NULL,
    Formula TEXT NOT NULL,
    DifficultyClass INTEGER NOT NULL,
    DiceRollId TEXT NOT NULL,
    Result TEXT NOT NULL,
    IsNaturalMaximum INTEGER NOT NULL,
    ResolvedSkill TEXT,
    CreatedAt TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_CheckOutcome_CampaignId ON CheckOutcome(CampaignId);";
            command.ExecuteNonQuery();
        }
    }
}
