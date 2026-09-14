using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;

namespace Odyssey.Persistence.Sqlite
{
    /// <summary>
    /// ODY-S05-604: the sole SQLite implementation of <see cref="IAttackApplyRepository"/>.
    /// Owns a new, standalone <c>AttackOutcome</c> table -- it never appends a
    /// method or column to <see cref="ICombatEncounterRepository"/>,
    /// <see cref="IInventoryRepository"/>, or <see cref="ICharacterRepository"/>,
    /// mirroring <see cref="SqliteActiveEffectRepository"/>'s own standalone-table
    /// idiom. Every mutating method commits through the shared
    /// <see cref="SqliteSavingPipeline"/> (ADR-012 section 5): the AttackOutcome
    /// row, the committed Game Log entry (only for an Accepted outcome), the
    /// DomainEvent, and the AppliedCommands idempotency row land in one SQLite
    /// transaction, or none of them do.
    ///
    /// Deliberately does NOT write any Character/Item/ActiveEffect state delta:
    /// no accepted Ruleset formula exists to interpret an <c>AttackDelta</c>'s
    /// opaque <c>TargetRef</c> string into a specific repository row (that
    /// would be exactly the "choose a Ruleset formula" decision ADR-029
    /// section 10 forbids this task from making) -- see the ODY-S05-604 task
    /// contract's decision log for the full reasoning, an honest scope
    /// narrowing in the same spirit as ODY-S05-504's documented
    /// <c>EffectConditionRules.Evaluate</c> no-op.
    /// </summary>
    public sealed class SqliteAttackApplyRepository : IAttackApplyRepository
    {
        private readonly IWallClock _clock;
        private readonly SqliteSavingPipeline _pipeline;

        public SqliteAttackApplyRepository(IWallClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _pipeline = new SqliteSavingPipeline(clock);
        }

        public Result<AttackOutcomeRecord> GetOutcome(CampaignHandle campaign, CommandId resolveAttackCommandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!resolveAttackCommandId.IsValid) throw new ArgumentException("ResolveAttackCommandId is required.", nameof(resolveAttackCommandId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureAttackApplyTables(connection);
                AttackOutcomeRecord? found = ReadByCommandId(connection, null, resolveAttackCommandId);
                return found == null
                    ? Result<AttackOutcomeRecord>.Failure(PersistenceFailures.AttackOutcomeNotFound(correlationId))
                    : Result<AttackOutcomeRecord>.Success(found);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<AttackOutcomeRecord>.Failure(PersistenceFailures.AttackOutcomeIoFailed(correlationId));
            }
        }

        public Result<AttackOutcomeRecord> RecordAttackOutcome(CampaignHandle campaign, AttackIntent intent, AttackRandomSample randomSample, bool interventionRequired, UserId actorUserId, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureAttackApplyTables(connection);

                return _pipeline.Execute(
                    connection,
                    campaign.CampaignId,
                    commandId,
                    correlationId,
                    tryReplay: transaction =>
                    {
                        AttackOutcomeRecord? replayed = ReadByCommandId(connection, transaction, commandId);
                        return replayed == null
                            ? Result<AttackOutcomeRecord>.Failure(PersistenceFailures.AttackOutcomeNotFound(correlationId))
                            : Result<AttackOutcomeRecord>.Success(replayed);
                    },
                    apply: transaction =>
                    {
                        UtcInstant now = _clock.GetUtcNow();
                        AttackOutcomeKind outcomeKind = interventionRequired ? AttackOutcomeKind.Pending : AttackOutcomeKind.Accepted;
                        string? gameLogEntryId = null;

                        using (var insert = connection.CreateCommand())
                        {
                            insert.Transaction = transaction;
                            insert.CommandText =
                                "INSERT INTO AttackOutcome (CommandId, CampaignId, EncounterId, ActorId, TargetIds, ActionItemInstanceId, ExpectedEncounterRevision, RandomSampleValue, InterventionRequired, OutcomeKind, GameLogEntryId, Revision, CreatedAt, ResolvedAt, ResolvedByCommandId) " +
                                "VALUES ($commandId, $campaignId, $encounterId, $actorId, $targetIds, $itemInstanceId, $expectedRevision, $randomSample, $interventionRequired, $outcomeKind, NULL, 1, $createdAt, NULL, NULL);";
                            insert.Parameters.AddWithValue("$commandId", commandId.ToString());
                            insert.Parameters.AddWithValue("$campaignId", campaign.CampaignId.ToString());
                            insert.Parameters.AddWithValue("$encounterId", intent.EncounterId.ToString());
                            insert.Parameters.AddWithValue("$actorId", intent.ActorId.ToString());
                            insert.Parameters.AddWithValue("$targetIds", SerializeTargetIds(intent.TargetIds));
                            insert.Parameters.AddWithValue("$itemInstanceId", intent.ActionItemInstanceId.ToString());
                            insert.Parameters.AddWithValue("$expectedRevision", intent.ExpectedEncounterRevision);
                            insert.Parameters.AddWithValue("$randomSample", randomSample.Value);
                            insert.Parameters.AddWithValue("$interventionRequired", interventionRequired ? 1 : 0);
                            insert.Parameters.AddWithValue("$outcomeKind", outcomeKind.ToString());
                            insert.Parameters.AddWithValue("$createdAt", now.ToString());
                            insert.ExecuteNonQuery();
                        }

                        Action<SqliteTransaction, long>? onSequenceAssigned = null;
                        if (outcomeKind == AttackOutcomeKind.Accepted)
                        {
                            gameLogEntryId = "log_" + Guid.NewGuid().ToString("N");
                            string capturedLogEntryId = gameLogEntryId;
                            WriteAcceptedAttackGameLogEntry(connection, transaction, campaign.CampaignId, capturedLogEntryId, commandId, actorUserId, intent, randomSample.Value, now);
                            SetOutcomeGameLogEntryId(connection, transaction, commandId, capturedLogEntryId);
                            onSequenceAssigned = (txn, sequence) => UpdateGameLogAuthoritativeSequence(connection, txn, capturedLogEntryId, sequence);
                        }

                        AttackOutcomeRecord result = new AttackOutcomeRecord(commandId, campaign.CampaignId, intent.EncounterId, intent.ActorId, intent.TargetIds, intent.ActionItemInstanceId, intent.ExpectedEncounterRevision, randomSample.Value, interventionRequired, outcomeKind, gameLogEntryId, now, null, null);
                        string payloadJson = "{\"commandId\":\"" + commandId + "\",\"outcomeKind\":\"" + outcomeKind + "\"}";
                        return Result<PipelineWrite<AttackOutcomeRecord>>.Success(new PipelineWrite<AttackOutcomeRecord>(
                            result, "odyssey.persistence.attack_outcome_recorded", payloadJson, commandId.ToString(),
                            aggregateType: "attack_outcome", aggregateId: commandId.ToString(), aggregateRevision: 1,
                            onEventSequenceAssigned: onSequenceAssigned));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<AttackOutcomeRecord>.Failure(PersistenceFailures.AttackOutcomeIoFailed(correlationId));
            }
        }

        public Result<AttackOutcomeRecord> ResolveAttackIntervention(CampaignHandle campaign, CommandId pendingCommandId, AttackInterventionResolution resolution, UserId actorUserId, bool actorIsMainGm, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!pendingCommandId.IsValid) throw new ArgumentException("PendingCommandId is required.", nameof(pendingCommandId));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            // ADR-029 section 10: MainGM resolves an ambiguous/contested/expired
            // intervention. A Ruleset-specific eligible-controller-choice payload
            // remains out of this task's own scope (section 1 rule 3 / section 14
            // item 3), so this task gates the whole command on MainGM, mirroring
            // RemoveActiveEffect's own literal-first-statement placement.
            if (!actorIsMainGm)
            {
                return Result<AttackOutcomeRecord>.Failure(PersistenceFailures.AttackOutcomeOperationDenied(correlationId));
            }

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureAttackApplyTables(connection);

                return _pipeline.Execute(
                    connection,
                    campaign.CampaignId,
                    commandId,
                    correlationId,
                    tryReplay: transaction =>
                    {
                        AttackOutcomeRecord? replayed = ReadByResolvedCommandId(connection, transaction, commandId);
                        return replayed == null
                            ? Result<AttackOutcomeRecord>.Failure(PersistenceFailures.AttackOutcomeNotFound(correlationId))
                            : Result<AttackOutcomeRecord>.Success(replayed);
                    },
                    apply: transaction =>
                    {
                        AttackOutcomeRecord? pending = ReadByCommandId(connection, transaction, pendingCommandId);
                        if (pending == null)
                        {
                            return Result<PipelineWrite<AttackOutcomeRecord>>.Failure(PersistenceFailures.AttackOutcomeNotFound(correlationId));
                        }

                        if (pending.OutcomeKind != AttackOutcomeKind.Pending)
                        {
                            // CAS guard: an already-resolved/cancelled pending outcome is a
                            // typed conflict, not a silent no-op -- and never a chance to
                            // re-derive a random sample.
                            return Result<PipelineWrite<AttackOutcomeRecord>>.Failure(PersistenceFailures.AttackOutcomeNotPending(correlationId));
                        }

                        UtcInstant now = _clock.GetUtcNow();
                        AttackOutcomeKind newKind = resolution switch
                        {
                            AttackInterventionResolution.Approve => AttackOutcomeKind.Accepted,
                            AttackInterventionResolution.Reject => AttackOutcomeKind.Rejected,
                            AttackInterventionResolution.Cancel => AttackOutcomeKind.Cancelled,
                            _ => throw new ArgumentOutOfRangeException(nameof(resolution)),
                        };

                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText =
                                "UPDATE AttackOutcome SET OutcomeKind=$newKind, Revision=Revision+1, ResolvedAt=$resolvedAt, ResolvedByCommandId=$resolvedBy " +
                                "WHERE CommandId=$pendingCommandId AND OutcomeKind='Pending';";
                            update.Parameters.AddWithValue("$newKind", newKind.ToString());
                            update.Parameters.AddWithValue("$resolvedAt", now.ToString());
                            update.Parameters.AddWithValue("$resolvedBy", commandId.ToString());
                            update.Parameters.AddWithValue("$pendingCommandId", pendingCommandId.ToString());
                            if (update.ExecuteNonQuery() != 1)
                            {
                                // Lost a race against a concurrent resolution between the
                                // read above and this guarded UPDATE.
                                return Result<PipelineWrite<AttackOutcomeRecord>>.Failure(PersistenceFailures.AttackOutcomeNotPending(correlationId));
                            }
                        }

                        Action<SqliteTransaction, long>? onSequenceAssigned = null;
                        string? gameLogEntryId = null;
                        if (newKind == AttackOutcomeKind.Accepted)
                        {
                            gameLogEntryId = "log_" + Guid.NewGuid().ToString("N");
                            string capturedLogEntryId = gameLogEntryId;
                            AttackIntent intent = new AttackIntent(pending.EncounterId, pending.ActorId, pending.TargetIds, pending.ActionItemInstanceId, pending.ExpectedEncounterRevision);
                            // Reuses pending.RandomSampleValue verbatim -- never re-derives a sample.
                            WriteAcceptedAttackGameLogEntry(connection, transaction, campaign.CampaignId, capturedLogEntryId, pendingCommandId, actorUserId, intent, pending.RandomSampleValue, now);
                            SetOutcomeGameLogEntryId(connection, transaction, pendingCommandId, capturedLogEntryId);
                            onSequenceAssigned = (txn, sequence) => UpdateGameLogAuthoritativeSequence(connection, txn, capturedLogEntryId, sequence);
                        }

                        AttackOutcomeRecord resolved = new AttackOutcomeRecord(pending.ResolveAttackCommandId, pending.CampaignId, pending.EncounterId, pending.ActorId, pending.TargetIds, pending.ActionItemInstanceId, pending.ExpectedEncounterRevision, pending.RandomSampleValue, pending.InterventionRequired, newKind, gameLogEntryId, pending.CreatedAt, now, commandId);
                        string payloadJson = "{\"pendingCommandId\":\"" + pendingCommandId + "\",\"resolution\":\"" + resolution + "\",\"outcomeKind\":\"" + newKind + "\"}";
                        return Result<PipelineWrite<AttackOutcomeRecord>>.Success(new PipelineWrite<AttackOutcomeRecord>(
                            resolved, "odyssey.persistence.attack_outcome_resolved", payloadJson, pendingCommandId.ToString(),
                            aggregateType: "attack_outcome", aggregateId: pendingCommandId.ToString(), aggregateRevision: 2,
                            onEventSequenceAssigned: onSequenceAssigned));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<AttackOutcomeRecord>.Failure(PersistenceFailures.AttackOutcomeIoFailed(correlationId));
            }
        }

        private static void WriteAcceptedAttackGameLogEntry(SqliteConnection connection, SqliteTransaction transaction, CampaignId campaignId, string logEntryId, CommandId rootCommandId, UserId actorUserId, AttackIntent intent, int randomSampleValue, UtcInstant now)
        {
            string rollId = "roll_" + Guid.NewGuid().ToString("N");
            using (var insertRoll = connection.CreateCommand())
            {
                insertRoll.Transaction = transaction;
                insertRoll.CommandText =
                    "INSERT INTO DiceRolls (RollId, CampaignId, ActorUserId, Purpose, FormulaOriginal, FormulaNormalized, FormulaParserVersion, NaturalResultsJson, ModifierEntriesJson, BaseTotal, RngAlgorithmVersion, Status, PreviousRollId, CreatedAt, AudienceKind, AudienceSelectedUserIdsJson, AudienceSelectedGroupIdsJson, LastCommandId) " +
                    "VALUES ($rollId, $campaignId, $actorUserId, 'combat.attack.roll', '1d100', '1d100', 1, $naturalResults, '[]', $baseTotal, 1, 'Resolved', NULL, $createdAt, 'PlayerAndGM', '[]', '[]', $lastCommandId);";
                insertRoll.Parameters.AddWithValue("$rollId", rollId);
                insertRoll.Parameters.AddWithValue("$campaignId", campaignId.ToString());
                insertRoll.Parameters.AddWithValue("$actorUserId", actorUserId.ToString());
                insertRoll.Parameters.AddWithValue("$naturalResults", "[{\"dieIndex\":0,\"groupIndex\":0,\"sides\":100,\"value\":" + randomSampleValue.ToString(CultureInfo.InvariantCulture) + "}]");
                insertRoll.Parameters.AddWithValue("$baseTotal", randomSampleValue);
                insertRoll.Parameters.AddWithValue("$createdAt", now.ToString());
                insertRoll.Parameters.AddWithValue("$lastCommandId", rootCommandId.ToString());
                insertRoll.ExecuteNonQuery();
            }

            string summaryPayload = "Attack " + intent.ActorId + " -> " + string.Join(",", intent.TargetIds) + " (roll " + randomSampleValue.ToString(CultureInfo.InvariantCulture) + ")";
            using (var insertEntry = connection.CreateCommand())
            {
                insertEntry.Transaction = transaction;
                insertEntry.CommandText =
                    "INSERT INTO GameLogEntries (LogEntryId, CampaignId, RootCommandId, EntryType, SummaryPayload, ActorUserId, DiceRollId, CreatedAt, AuthoritativeSequence, LastCommandId) " +
                    "VALUES ($logEntryId, $campaignId, $rootCommandId, 'AttackResolved', $summaryPayload, $actorUserId, $diceRollId, $createdAt, 0, $lastCommandId);";
                insertEntry.Parameters.AddWithValue("$logEntryId", logEntryId);
                insertEntry.Parameters.AddWithValue("$campaignId", campaignId.ToString());
                insertEntry.Parameters.AddWithValue("$rootCommandId", rootCommandId.ToString());
                insertEntry.Parameters.AddWithValue("$summaryPayload", summaryPayload);
                insertEntry.Parameters.AddWithValue("$actorUserId", actorUserId.ToString());
                insertEntry.Parameters.AddWithValue("$diceRollId", rollId);
                insertEntry.Parameters.AddWithValue("$createdAt", now.ToString());
                insertEntry.Parameters.AddWithValue("$lastCommandId", rootCommandId.ToString());
                insertEntry.ExecuteNonQuery();
            }
        }

        private static void SetOutcomeGameLogEntryId(SqliteConnection connection, SqliteTransaction transaction, CommandId outcomeCommandId, string logEntryId)
        {
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE AttackOutcome SET GameLogEntryId=$logEntryId WHERE CommandId=$commandId;";
            update.Parameters.AddWithValue("$logEntryId", logEntryId);
            update.Parameters.AddWithValue("$commandId", outcomeCommandId.ToString());
            update.ExecuteNonQuery();
        }

        private static void UpdateGameLogAuthoritativeSequence(SqliteConnection connection, SqliteTransaction transaction, string logEntryId, long sequence)
        {
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE GameLogEntries SET AuthoritativeSequence = $sequence WHERE LogEntryId = $logEntryId;";
            update.Parameters.AddWithValue("$sequence", sequence);
            update.Parameters.AddWithValue("$logEntryId", logEntryId);
            update.ExecuteNonQuery();
        }

        private static AttackOutcomeRecord? ReadByCommandId(SqliteConnection connection, SqliteTransaction? transaction, CommandId commandId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT CommandId, CampaignId, EncounterId, ActorId, TargetIds, ActionItemInstanceId, ExpectedEncounterRevision, RandomSampleValue, InterventionRequired, OutcomeKind, GameLogEntryId, CreatedAt, ResolvedAt, ResolvedByCommandId FROM AttackOutcome WHERE CommandId = $commandId LIMIT 1;";
            select.Parameters.AddWithValue("$commandId", commandId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            return reader.Read() ? ReadRecord(reader) : null;
        }

        private static AttackOutcomeRecord? ReadByResolvedCommandId(SqliteConnection connection, SqliteTransaction? transaction, CommandId resolvedByCommandId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT CommandId, CampaignId, EncounterId, ActorId, TargetIds, ActionItemInstanceId, ExpectedEncounterRevision, RandomSampleValue, InterventionRequired, OutcomeKind, GameLogEntryId, CreatedAt, ResolvedAt, ResolvedByCommandId FROM AttackOutcome WHERE ResolvedByCommandId = $resolvedBy LIMIT 1;";
            select.Parameters.AddWithValue("$resolvedBy", resolvedByCommandId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            return reader.Read() ? ReadRecord(reader) : null;
        }

        private static AttackOutcomeRecord ReadRecord(SqliteDataReader reader)
        {
            CommandId commandId = CommandId.Parse(reader.GetString(0));
            CampaignId campaignId = CampaignId.Parse(reader.GetString(1));
            CombatEncounterId encounterId = CombatEncounterId.Parse(reader.GetString(2));
            CharacterId actorId = CharacterId.Parse(reader.GetString(3));
            IReadOnlyList<CharacterId> targetIds = DeserializeTargetIds(reader.GetString(4));
            ItemInstanceId itemInstanceId = ItemInstanceId.Parse(reader.GetString(5));
            long expectedRevision = reader.GetInt64(6);
            int randomSampleValue = reader.GetInt32(7);
            bool interventionRequired = reader.GetInt64(8) != 0;
            AttackOutcomeKind outcomeKind = (AttackOutcomeKind)Enum.Parse(typeof(AttackOutcomeKind), reader.GetString(9));
            string? gameLogEntryId = reader.IsDBNull(10) ? null : reader.GetString(10);
            UtcInstant createdAt = UtcInstant.Parse(reader.GetString(11));
            UtcInstant? resolvedAt = reader.IsDBNull(12) ? (UtcInstant?)null : UtcInstant.Parse(reader.GetString(12));
            CommandId? resolvedByCommandId = reader.IsDBNull(13) ? (CommandId?)null : CommandId.Parse(reader.GetString(13));
            return new AttackOutcomeRecord(commandId, campaignId, encounterId, actorId, targetIds, itemInstanceId, expectedRevision, randomSampleValue, interventionRequired, outcomeKind, gameLogEntryId, createdAt, resolvedAt, resolvedByCommandId);
        }

        private static string SerializeTargetIds(IReadOnlyList<CharacterId> targetIds) => string.Join(",", targetIds.Select(id => id.ToString()));

        private static IReadOnlyList<CharacterId> DeserializeTargetIds(string value) => Array.AsReadOnly(value.Split(',').Select(CharacterId.Parse).ToArray());

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

        private static void EnsureAttackApplyTables(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText =
@"
CREATE TABLE IF NOT EXISTS AttackOutcome (
    CommandId TEXT PRIMARY KEY,
    CampaignId TEXT NOT NULL,
    EncounterId TEXT NOT NULL,
    ActorId TEXT NOT NULL,
    TargetIds TEXT NOT NULL,
    ActionItemInstanceId TEXT NOT NULL,
    ExpectedEncounterRevision INTEGER NOT NULL,
    RandomSampleValue INTEGER NOT NULL,
    InterventionRequired INTEGER NOT NULL,
    OutcomeKind TEXT NOT NULL,
    GameLogEntryId TEXT,
    Revision INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    ResolvedAt TEXT,
    ResolvedByCommandId TEXT
);
CREATE INDEX IF NOT EXISTS IX_AttackOutcome_CampaignId ON AttackOutcome(CampaignId);
CREATE INDEX IF NOT EXISTS IX_AttackOutcome_ResolvedByCommandId ON AttackOutcome(ResolvedByCommandId);
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
    }
}
