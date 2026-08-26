using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using Odyssey.Application.Commands;
using Odyssey.Application.Dice;
using Odyssey.Application.Persistence;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Persistence.Sqlite
{
    /// <summary>
    /// ODY-S03-007: durable counterpart to ODY-S03-005's in-memory-only
    /// <c>DiceRollStore</c>. Mirrors <see cref="SqliteSceneRepository"/>'s
    /// exact shape -- each method opens its own short-lived connection under
    /// the ADR-011 section 7.1 PRAGMA profile, every mutating method commits
    /// through the shared <see cref="SqliteSavingPipeline"/> (current-state
    /// rows + DomainEvent + AppliedCommands in one ADR-012 section 5
    /// transaction).
    ///
    /// Scope narrowing (task contract section 3): <see cref="DiceRoll.RngProofs"/>
    /// is deliberately NOT persisted by this task. It is non-secret
    /// diagnostic/audit evidence (RngContracts.cs's own doc comment), not
    /// data the game log needs to explain an outcome to a player (exit
    /// criterion 5 -- NaturalResults/ModifierEntries/FinalTotal already do
    /// that) -- persisting a full RandomDecisionContext round-trip for
    /// audit purposes is a materially separate concern left to a future task
    /// if durable RNG audit trail is ever required.
    /// </summary>
    public sealed class SqliteGameLogRepository : IGameLogRepository
    {
        private readonly IWallClock _clock;
        private readonly SqliteSavingPipeline _pipeline;

        public SqliteGameLogRepository(IWallClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _pipeline = new SqliteSavingPipeline(clock);
        }

        public Result<GameLogEntryRecord> SaveDiceRollEntry(CampaignHandle campaign, DiceRoll roll, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (roll == null) throw new ArgumentNullException(nameof(roll));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureGameLogTables(connection);
                UtcInstant now = _clock.GetUtcNow();

                return _pipeline.Execute(
                    connection,
                    campaign.CampaignId,
                    commandId,
                    correlationId,
                    tryReplay: transaction => ReplayGameLogEntry(connection, transaction, campaign.CampaignId, "LogEntryId = (SELECT LogEntryId FROM GameLogEntries WHERE LastCommandId = $commandId LIMIT 1)", commandId, correlationId),
                    apply: transaction =>
                    {
                        string logEntryId = "log_" + Guid.NewGuid().ToString("N");
                        string summaryPayload = roll.FormulaOriginal + " = " + roll.FinalTotal.ToString(CultureInfo.InvariantCulture);
                        const string entryType = "DiceRollResolved";

                        InsertDiceRoll(connection, transaction, campaign.CampaignId, roll, commandId);

                        using (var insert = connection.CreateCommand())
                        {
                            insert.Transaction = transaction;
                            insert.CommandText = "INSERT INTO GameLogEntries (LogEntryId, CampaignId, RootCommandId, EntryType, SummaryPayload, ActorUserId, DiceRollId, CreatedAt, AuthoritativeSequence, LastCommandId) " +
                                                  "VALUES ($logEntryId, $campaignId, $rootCommandId, $entryType, $summaryPayload, $actorUserId, $diceRollId, $createdAt, 0, $lastCommandId);";
                            insert.Parameters.AddWithValue("$logEntryId", logEntryId);
                            insert.Parameters.AddWithValue("$campaignId", campaign.CampaignId.ToString());
                            insert.Parameters.AddWithValue("$rootCommandId", commandId.ToString());
                            insert.Parameters.AddWithValue("$entryType", entryType);
                            insert.Parameters.AddWithValue("$summaryPayload", summaryPayload);
                            insert.Parameters.AddWithValue("$actorUserId", roll.ActorUserId.ToString());
                            insert.Parameters.AddWithValue("$diceRollId", roll.RollId);
                            insert.Parameters.AddWithValue("$createdAt", now.ToString());
                            insert.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            insert.ExecuteNonQuery();
                        }

                        // AuthoritativeSequence (ADR-012 section 4.1's EventSequence) is
                        // not known until the pipeline appends the DomainEvent -- the
                        // placeholder 0 above is corrected in-transaction by
                        // OnEventSequenceAssigned before commit (never left as 0).
                        var placeholderRecord = new GameLogEntryRecord(logEntryId, campaign.CampaignId, commandId, entryType, summaryPayload, roll.ActorUserId, roll.RollId, now, authoritativeSequence: 1, roll: roll);
                        string payloadJson = "{\"logEntryId\":\"" + logEntryId + "\",\"diceRollId\":" + JsonString(roll.RollId) + "}";

                        return Result<PipelineWrite<GameLogEntryRecord>>.Success(new PipelineWrite<GameLogEntryRecord>(
                            placeholderRecord, "odyssey.persistence.gamelog_entry_created", payloadJson, logEntryId,
                            aggregateType: "gamelog_entry", aggregateId: logEntryId, aggregateRevision: 1,
                            withEventSequence: sequence => new GameLogEntryRecord(logEntryId, campaign.CampaignId, commandId, entryType, summaryPayload, roll.ActorUserId, roll.RollId, now, sequence, roll),
                            onEventSequenceAssigned: (txn, sequence) =>
                            {
                                using var update = connection.CreateCommand();
                                update.Transaction = txn;
                                update.CommandText = "UPDATE GameLogEntries SET AuthoritativeSequence = $sequence WHERE LogEntryId = $logEntryId;";
                                update.Parameters.AddWithValue("$sequence", sequence);
                                update.Parameters.AddWithValue("$logEntryId", logEntryId);
                                update.ExecuteNonQuery();
                            }));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<GameLogEntryRecord>.Failure(PersistenceFailures.GameLogIoFailed(correlationId));
            }
        }

        public Result<IReadOnlyList<GameLogEntryRecord>> ListGameLog(CampaignHandle campaign, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureGameLogTables(connection);

                var entries = new List<GameLogEntryRecord>();
                using (var select = connection.CreateCommand())
                {
                    select.CommandText = "SELECT LogEntryId, RootCommandId, EntryType, SummaryPayload, ActorUserId, DiceRollId, CreatedAt, AuthoritativeSequence FROM GameLogEntries " +
                                          "WHERE CampaignId = $campaignId ORDER BY AuthoritativeSequence;";
                    select.Parameters.AddWithValue("$campaignId", campaign.CampaignId.ToString());
                    using SqliteDataReader reader = select.ExecuteReader();
                    while (reader.Read())
                    {
                        string diceRollId = reader.GetString(5);
                        DiceRoll? roll = SelectDiceRoll(connection, campaign.CampaignId, diceRollId);
                        if (roll == null)
                        {
                            return Result<IReadOnlyList<GameLogEntryRecord>>.Failure(PersistenceFailures.GameLogIoFailed(correlationId));
                        }

                        entries.Add(new GameLogEntryRecord(
                            reader.GetString(0), campaign.CampaignId, CommandId.Parse(reader.GetString(1)), reader.GetString(2), reader.GetString(3),
                            UserId.Parse(reader.GetString(4)), diceRollId, UtcInstant.Parse(reader.GetString(6)), reader.GetInt64(7), roll));
                    }
                }

                return Result<IReadOnlyList<GameLogEntryRecord>>.Success(entries);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<IReadOnlyList<GameLogEntryRecord>>.Failure(PersistenceFailures.GameLogIoFailed(correlationId));
            }
        }

        private static Result<GameLogEntryRecord> ReplayGameLogEntry(SqliteConnection connection, SqliteTransaction transaction, CampaignId campaignId, string whereClause, CommandId commandId, CorrelationId correlationId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT LogEntryId, RootCommandId, EntryType, SummaryPayload, ActorUserId, DiceRollId, CreatedAt, AuthoritativeSequence FROM GameLogEntries WHERE " + whereClause + " LIMIT 1;";
            select.Parameters.AddWithValue("$commandId", commandId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read())
            {
                return Result<GameLogEntryRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId));
            }

            string diceRollId = reader.GetString(5);
            DiceRoll? roll = SelectDiceRoll(connection, transaction, campaignId, diceRollId);
            if (roll == null)
            {
                return Result<GameLogEntryRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId));
            }

            return Result<GameLogEntryRecord>.Success(new GameLogEntryRecord(
                reader.GetString(0), campaignId, CommandId.Parse(reader.GetString(1)), reader.GetString(2), reader.GetString(3),
                UserId.Parse(reader.GetString(4)), diceRollId, UtcInstant.Parse(reader.GetString(6)), reader.GetInt64(7), roll));
        }

        private static void InsertDiceRoll(SqliteConnection connection, SqliteTransaction transaction, CampaignId campaignId, DiceRoll roll, CommandId commandId)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                "INSERT INTO DiceRolls (RollId, CampaignId, ActorUserId, Purpose, FormulaOriginal, FormulaNormalized, FormulaParserVersion, NaturalResultsJson, ModifierEntriesJson, BaseTotal, RngAlgorithmVersion, Status, PreviousRollId, CreatedAt, AudienceKind, AudienceSelectedUserIdsJson, AudienceSelectedGroupIdsJson, LastCommandId) " +
                "VALUES ($rollId, $campaignId, $actorUserId, $purpose, $formulaOriginal, $formulaNormalized, $formulaParserVersion, $naturalResults, $modifierEntries, $baseTotal, $rngAlgorithmVersion, $status, $previousRollId, $createdAt, $audienceKind, $audienceUsers, $audienceGroups, $lastCommandId);";
            insert.Parameters.AddWithValue("$rollId", roll.RollId);
            insert.Parameters.AddWithValue("$campaignId", campaignId.ToString());
            insert.Parameters.AddWithValue("$actorUserId", roll.ActorUserId.ToString());
            insert.Parameters.AddWithValue("$purpose", roll.Purpose);
            insert.Parameters.AddWithValue("$formulaOriginal", roll.FormulaOriginal);
            insert.Parameters.AddWithValue("$formulaNormalized", roll.FormulaNormalized);
            insert.Parameters.AddWithValue("$formulaParserVersion", roll.FormulaParserVersion);
            insert.Parameters.AddWithValue("$naturalResults", SerializeNaturalResults(roll.NaturalResults));
            insert.Parameters.AddWithValue("$modifierEntries", SerializeModifierEntries(roll.ModifierEntries));
            insert.Parameters.AddWithValue("$baseTotal", roll.BaseTotal);
            insert.Parameters.AddWithValue("$rngAlgorithmVersion", roll.RngAlgorithmVersion);
            insert.Parameters.AddWithValue("$status", roll.Status.ToString());
            insert.Parameters.AddWithValue("$previousRollId", (object?)roll.PreviousRollId ?? DBNull.Value);
            insert.Parameters.AddWithValue("$createdAt", roll.CreatedAt.ToString());
            insert.Parameters.AddWithValue("$audienceKind", roll.Audience.Kind.ToString());
            insert.Parameters.AddWithValue("$audienceUsers", SerializeUserIds(roll.Audience.SelectedUserIds));
            insert.Parameters.AddWithValue("$audienceGroups", SerializeStrings(roll.Audience.SelectedGroupIds));
            insert.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
            insert.ExecuteNonQuery();
        }

        private static DiceRoll? SelectDiceRoll(SqliteConnection connection, CampaignId campaignId, string rollId) => SelectDiceRoll(connection, null, campaignId, rollId);

        private static DiceRoll? SelectDiceRoll(SqliteConnection connection, SqliteTransaction? transaction, CampaignId campaignId, string rollId)
        {
            using var select = connection.CreateCommand();
            if (transaction != null) select.Transaction = transaction;
            select.CommandText = "SELECT RollId, ActorUserId, Purpose, FormulaOriginal, FormulaNormalized, FormulaParserVersion, NaturalResultsJson, ModifierEntriesJson, BaseTotal, RngAlgorithmVersion, Status, PreviousRollId, CreatedAt, AudienceKind, AudienceSelectedUserIdsJson, AudienceSelectedGroupIdsJson FROM DiceRolls WHERE RollId = $rollId LIMIT 1;";
            select.Parameters.AddWithValue("$rollId", rollId);
            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            UserId actorUserId = UserId.Parse(reader.GetString(1));
            IReadOnlyList<NaturalResult> naturalResults = DeserializeNaturalResults(reader.GetString(6));
            IReadOnlyList<ModifierEntry> modifierEntries = DeserializeModifierEntries(reader.GetString(7));
            DiceRollStatus status = (DiceRollStatus)Enum.Parse(typeof(DiceRollStatus), reader.GetString(10));
            string? previousRollId = reader.IsDBNull(11) ? null : reader.GetString(11);
            UtcInstant createdAt = UtcInstant.Parse(reader.GetString(12));
            DiceRollAudienceKind audienceKind = (DiceRollAudienceKind)Enum.Parse(typeof(DiceRollAudienceKind), reader.GetString(13));
            IReadOnlyList<UserId> selectedUserIds = DeserializeUserIds(reader.GetString(14));
            IReadOnlyList<string> selectedGroupIds = DeserializeStrings(reader.GetString(15));

            DiceRollAudience audience = audienceKind switch
            {
                DiceRollAudienceKind.Public => DiceRollAudience.Public(),
                DiceRollAudienceKind.PlayerAndGM => DiceRollAudience.PlayerAndGM(),
                DiceRollAudienceKind.GMOnly => DiceRollAudience.GMOnly(),
                DiceRollAudienceKind.SelectedParticipants => DiceRollAudience.SelectedParticipants(selectedUserIds, selectedGroupIds),
                _ => throw new InvalidOperationException("Unrecognized persisted DiceRollAudienceKind."),
            };

            return new DiceRoll(
                reader.GetString(0), actorUserId, reader.GetString(2), campaignId,
                reader.GetString(3), reader.GetString(4), reader.GetInt32(5),
                naturalResults, modifierEntries, reader.GetInt32(8), reader.GetInt32(9),
                Array.Empty<RngProofData>(), status, previousRollId, createdAt, audience);
        }

        private static string SerializeNaturalResults(IReadOnlyList<NaturalResult> results)
        {
            var array = new JArray();
            foreach (NaturalResult result in results)
            {
                array.Add(new JObject { ["dieIndex"] = result.DieIndex, ["groupIndex"] = result.GroupIndex, ["sides"] = result.Sides, ["value"] = result.Value });
            }

            return array.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static IReadOnlyList<NaturalResult> DeserializeNaturalResults(string json)
        {
            var array = JArray.Parse(json);
            var list = new List<NaturalResult>();
            foreach (JToken item in array)
            {
                list.Add(new NaturalResult((int)item["dieIndex"]!, (int)item["groupIndex"]!, (int)item["sides"]!, (int)item["value"]!));
            }

            return list;
        }

        private static string SerializeModifierEntries(IReadOnlyList<ModifierEntry> entries)
        {
            var array = new JArray();
            foreach (ModifierEntry entry in entries)
            {
                array.Add(new JObject
                {
                    ["modifierEntryId"] = entry.ModifierEntryId,
                    ["sourceKind"] = entry.SourceKind,
                    ["label"] = entry.Label,
                    ["value"] = entry.Value,
                    ["proposedByUserId"] = entry.ProposedByUserId?.ToString(),
                    ["decision"] = entry.Decision.ToString(),
                    ["decidedByUserId"] = entry.DecidedByUserId?.ToString(),
                    ["decisionReason"] = entry.DecisionReason,
                    ["appliedValue"] = entry.AppliedValue,
                });
            }

            return array.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static IReadOnlyList<ModifierEntry> DeserializeModifierEntries(string json)
        {
            var array = JArray.Parse(json);
            var list = new List<ModifierEntry>();
            foreach (JToken item in array)
            {
                UserId? proposedBy = item["proposedByUserId"]!.Type == JTokenType.Null ? (UserId?)null : UserId.Parse((string)item["proposedByUserId"]!);
                UserId? decidedBy = item["decidedByUserId"]!.Type == JTokenType.Null ? (UserId?)null : UserId.Parse((string)item["decidedByUserId"]!);
                string? decisionReason = item["decisionReason"]!.Type == JTokenType.Null ? null : (string)item["decisionReason"]!;
                var decision = (ModifierDecision)Enum.Parse(typeof(ModifierDecision), (string)item["decision"]!);

                list.Add(new ModifierEntry(
                    (string)item["modifierEntryId"]!, (string)item["sourceKind"]!, (string)item["label"]!, (int)item["value"]!,
                    proposedBy, decision, decidedBy, decisionReason, (int)item["appliedValue"]!));
            }

            return list;
        }

        private static string SerializeUserIds(IReadOnlyList<UserId> userIds) => SerializeStrings(ToStringList(userIds));

        private static IReadOnlyList<UserId> DeserializeUserIds(string json)
        {
            var strings = DeserializeStrings(json);
            var list = new List<UserId>(strings.Count);
            foreach (string value in strings)
            {
                list.Add(UserId.Parse(value));
            }

            return list;
        }

        private static IReadOnlyList<string> ToStringList(IReadOnlyList<UserId> userIds)
        {
            var list = new List<string>(userIds.Count);
            foreach (UserId userId in userIds)
            {
                list.Add(userId.ToString());
            }

            return list;
        }

        private static string SerializeStrings(IReadOnlyList<string> values)
        {
            var array = new JArray();
            foreach (string value in values)
            {
                array.Add(value);
            }

            return array.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static IReadOnlyList<string> DeserializeStrings(string json)
        {
            var array = JArray.Parse(json);
            var list = new List<string>(array.Count);
            foreach (JToken item in array)
            {
                list.Add((string)item!);
            }

            return list;
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

        private static void EnsureGameLogTables(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS DiceRolls (
    RollId TEXT PRIMARY KEY,
    CampaignId TEXT NOT NULL,
    ActorUserId TEXT NOT NULL,
    Purpose TEXT NOT NULL,
    FormulaOriginal TEXT NOT NULL,
    FormulaNormalized TEXT NOT NULL,
    FormulaParserVersion INTEGER NOT NULL,
    NaturalResultsJson TEXT NOT NULL,
    ModifierEntriesJson TEXT NOT NULL,
    BaseTotal INTEGER NOT NULL,
    RngAlgorithmVersion INTEGER NOT NULL,
    Status TEXT NOT NULL,
    PreviousRollId TEXT,
    CreatedAt TEXT NOT NULL,
    AudienceKind TEXT NOT NULL,
    AudienceSelectedUserIdsJson TEXT NOT NULL,
    AudienceSelectedGroupIdsJson TEXT NOT NULL,
    LastCommandId TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS GameLogEntries (
    LogEntryId TEXT PRIMARY KEY,
    CampaignId TEXT NOT NULL,
    RootCommandId TEXT NOT NULL,
    EntryType TEXT NOT NULL,
    SummaryPayload TEXT NOT NULL,
    ActorUserId TEXT NOT NULL,
    DiceRollId TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    AuthoritativeSequence INTEGER NOT NULL,
    LastCommandId TEXT NOT NULL
);";
            command.ExecuteNonQuery();
        }

        private static string JsonString(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
