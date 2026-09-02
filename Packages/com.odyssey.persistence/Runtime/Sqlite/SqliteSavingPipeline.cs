using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
    /// ODY-S01-009: the ADR-012 section 5 single-transaction journal-projection
    /// commit pipeline. Every mutating repository method (SqliteCampaignRepository,
    /// SqliteSceneRepository) routes its projection write through
    /// <see cref="Execute{T}"/>, which commits the projection change, the
    /// corresponding DomainEvent, and the AppliedCommands idempotency record in one
    /// SQLite transaction -- either all three land, or none do.
    ///
    /// This intentionally does not route through Odyssey.Application.Commands'
    /// ApplicationCommand/ICommandHandler/ICommandCommitter object graph
    /// (CommandContracts.cs). That graph's CommandPayload type carries only a
    /// PayloadType marker string, not actual argument data -- it is a foundation
    /// for a future networked command-dispatch layer, not a fit for today's direct
    /// repository calls without redesigning CommandContracts.cs itself, which is
    /// out of this task's scope (see the ODY-S01-009 task contract section 5/6).
    /// This class does reuse the same <see cref="CommandId"/> type so the
    /// repository ports and the future command-dispatch layer share one
    /// idempotency-key type rather than two parallel ones.
    /// </summary>
    internal sealed class SqliteSavingPipeline
    {
        private readonly IWallClock _clock;

        internal SqliteSavingPipeline(IWallClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        internal Result<T> Execute<T>(
            SqliteConnection connection,
            CampaignId campaignId,
            CommandId commandId,
            CorrelationId correlationId,
            Func<SqliteTransaction, Result<T>> tryReplay,
            Func<SqliteTransaction, Result<PipelineWrite<T>>> apply)
            where T : notnull
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (tryReplay == null) throw new ArgumentNullException(nameof(tryReplay));
            if (apply == null) throw new ArgumentNullException(nameof(apply));

            using SqliteTransaction transaction = connection.BeginTransaction();

            if (TryReadAppliedCommandStatus(connection, transaction, commandId, out string status))
            {
                if (!string.Equals(status, "Completed", StringComparison.Ordinal))
                {
                    // Section 5: a pre-commit failure rolls back the whole group, so
                    // no AppliedCommands row is ever durable for anything but a
                    // completed command. A non-"Completed" row here means the data
                    // was corrupted out of band, not a normal retry.
                    return Result<T>.Failure(PersistenceFailures.CommandReplayFailed(correlationId));
                }

                Result<T> replayed = tryReplay(transaction);
                return replayed.IsFailure
                    ? Result<T>.Failure(PersistenceFailures.CommandReplayFailed(correlationId))
                    : replayed;
            }

            Result<PipelineWrite<T>> outcome = apply(transaction);
            if (outcome.IsFailure)
            {
                return Result<T>.Failure(outcome.Error);
            }

            PipelineWrite<T> write = outcome.Value;
            UtcInstant now = _clock.GetUtcNow();
            long eventSequence = AppendDomainEvent(connection, transaction, campaignId, commandId, write.EventType, write.EventPayloadJson, now, write.OriginalEventId, write.CompensationGroupId, write.IsCompensating);

            // ODY-S03-007: a caller whose own row must persist ADR-012 section
            // 4.1's EventSequence as a real column value (e.g. GameLogEntries.
            // AuthoritativeSequence) cannot know that value until this point --
            // OnEventSequenceAssigned lets it write that column inside this
            // same transaction, before commit, instead of a separate,
            // non-atomic follow-up write.
            write.OnEventSequenceAssigned?.Invoke(transaction, eventSequence);

            if (write.AggregateType != null)
            {
                UpsertAggregateRevision(connection, transaction, write.AggregateType, write.AggregateId!, write.AggregateRevision);
            }

            InsertAppliedCommand(connection, transaction, commandId, eventSequence, eventSequence, write.ResultSummary, now);
            transaction.Commit();

            // WithEventSequence lets the caller finalize its in-memory result
            // object (immutable records rebuilt with the now-known sequence)
            // post-commit. Existing callers that never set either hook
            // (SqliteSceneRepository) are unaffected.
            T finalResult = write.WithEventSequence != null ? write.WithEventSequence(eventSequence) : write.Result;
            return Result<T>.Success(finalResult);
        }

        private static bool TryReadAppliedCommandStatus(SqliteConnection connection, SqliteTransaction transaction, CommandId commandId, out string status)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT Status FROM AppliedCommands WHERE CommandId = $commandId LIMIT 1;";
            command.Parameters.AddWithValue("$commandId", commandId.ToString());
            object? result = command.ExecuteScalar();
            if (result == null)
            {
                status = string.Empty;
                return false;
            }

            status = (string)result;
            return true;
        }

        /// <summary>
        /// ODY-S04-107: <c>internal</c> (not <c>private</c>) so
        /// <c>SqliteCharacterRepository.ApplyCharacterRespec</c> can append
        /// several <c>DomainEvents</c> rows inside one transaction --
        /// <see cref="Execute{T}"/>'s own design commits exactly one event
        /// per call, which a compensating/forward batch needs to exceed;
        /// only the batch's own final grouping event goes through the
        /// normal <see cref="Execute{T}"/> path, and the rest are appended
        /// directly via this same method, so every event -- pipeline-issued
        /// or not -- is written by the identical code, never a duplicated
        /// INSERT. <paramref name="originalEventId"/>/<paramref name="compensationGroupId"/>/
        /// <paramref name="isCompensating"/> are ADR-012 section 6's
        /// compensating-event metadata (05_Persistence section 12.1's
        /// <c>DomainEvents</c> columns) -- optional, default to "not
        /// compensating," so every pre-existing caller of
        /// <see cref="Execute{T}"/> (Scene/Token/GameLog/Character's own
        /// ordinary commands) is unaffected.
        /// </summary>
        internal static long AppendDomainEvent(SqliteConnection connection, SqliteTransaction transaction, CampaignId campaignId, CommandId commandId, string eventType, string payloadJson, UtcInstant now, long? originalEventId = null, string? compensationGroupId = null, bool isCompensating = false)
        {
            string payloadHash = ComputeSha256Hex(payloadJson);
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                "INSERT INTO DomainEvents (CampaignId, EventType, CommandId, PayloadJson, PayloadHash, CreatedAtHost, OriginalEventId, CompensationGroupId, IsCompensating) " +
                "VALUES ($campaignId, $eventType, $commandId, $payloadJson, $payloadHash, $createdAt, $originalEventId, $compensationGroupId, $isCompensating); " +
                "SELECT last_insert_rowid();";
            insert.Parameters.AddWithValue("$campaignId", campaignId.ToString());
            insert.Parameters.AddWithValue("$eventType", eventType);
            insert.Parameters.AddWithValue("$commandId", commandId.ToString());
            insert.Parameters.AddWithValue("$payloadJson", payloadJson);
            insert.Parameters.AddWithValue("$payloadHash", payloadHash);
            insert.Parameters.AddWithValue("$createdAt", now.ToString());
            insert.Parameters.AddWithValue("$originalEventId", (object?)originalEventId ?? DBNull.Value);
            insert.Parameters.AddWithValue("$compensationGroupId", (object?)compensationGroupId ?? DBNull.Value);
            insert.Parameters.AddWithValue("$isCompensating", isCompensating ? 1 : 0);
            object? sequence = insert.ExecuteScalar();
            return Convert.ToInt64(sequence, CultureInfo.InvariantCulture);
        }

        private static void UpsertAggregateRevision(SqliteConnection connection, SqliteTransaction transaction, string aggregateType, string aggregateId, long revision)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "INSERT INTO AggregateRevisions (AggregateType, AggregateId, Revision) VALUES ($type, $id, $revision) " +
                "ON CONFLICT(AggregateType, AggregateId) DO UPDATE SET Revision = excluded.Revision;";
            command.Parameters.AddWithValue("$type", aggregateType);
            command.Parameters.AddWithValue("$id", aggregateId);
            command.Parameters.AddWithValue("$revision", revision);
            command.ExecuteNonQuery();
        }

        private static void InsertAppliedCommand(SqliteConnection connection, SqliteTransaction transaction, CommandId commandId, long eventSequenceFrom, long eventSequenceTo, string resultSummary, UtcInstant now)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "INSERT INTO AppliedCommands (CommandId, Status, ResultEventSequenceFrom, ResultEventSequenceTo, ResultSummary, FailureCode, CreatedAt, CompletedAt) " +
                "VALUES ($commandId, 'Completed', $from, $to, $summary, NULL, $createdAt, $completedAt);";
            command.Parameters.AddWithValue("$commandId", commandId.ToString());
            command.Parameters.AddWithValue("$from", eventSequenceFrom);
            command.Parameters.AddWithValue("$to", eventSequenceTo);
            command.Parameters.AddWithValue("$summary", resultSummary);
            command.Parameters.AddWithValue("$createdAt", now.ToString());
            command.Parameters.AddWithValue("$completedAt", now.ToString());
            command.ExecuteNonQuery();
        }

        internal static string ComputeSha256Hex(string payloadJson)
        {
            using var sha = SHA256.Create();
            byte[] hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(payloadJson));
            var builder = new StringBuilder(hashBytes.Length * 2);
            for (int index = 0; index < hashBytes.Length; index++)
            {
                builder.Append(hashBytes[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }

    internal sealed class PipelineWrite<T>
    {
        internal PipelineWrite(T result, string eventType, string eventPayloadJson, string resultSummary, string? aggregateType = null, string? aggregateId = null, long aggregateRevision = 0, Func<long, T>? withEventSequence = null, Action<SqliteTransaction, long>? onEventSequenceAssigned = null, long? originalEventId = null, string? compensationGroupId = null, bool isCompensating = false)
        {
            Result = result;
            EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
            EventPayloadJson = eventPayloadJson ?? throw new ArgumentNullException(nameof(eventPayloadJson));
            ResultSummary = resultSummary ?? string.Empty;
            AggregateType = aggregateType;
            AggregateId = aggregateId;
            AggregateRevision = aggregateRevision;
            WithEventSequence = withEventSequence;
            OnEventSequenceAssigned = onEventSequenceAssigned;
            OriginalEventId = originalEventId;
            CompensationGroupId = compensationGroupId;
            IsCompensating = isCompensating;
        }

        internal T Result { get; }
        internal string EventType { get; }
        internal string EventPayloadJson { get; }
        internal string ResultSummary { get; }
        internal string? AggregateType { get; }
        internal string? AggregateId { get; }
        internal long AggregateRevision { get; }

        /// <summary>ODY-S03-007: optional post-commit finalizer receiving the just-assigned EventSequence -- see the call site in <see cref="Execute{T}"/>.</summary>
        internal Func<long, T>? WithEventSequence { get; }

        /// <summary>ODY-S03-007: optional pre-commit, in-transaction callback receiving the just-assigned EventSequence -- lets a caller write it as a real column value before the transaction commits. See the call site in <see cref="Execute{T}"/>.</summary>
        internal Action<SqliteTransaction, long>? OnEventSequenceAssigned { get; }

        /// <summary>ODY-S04-107: ADR-012 section 6's compensating-event metadata -- optional, default "not compensating," so every pre-existing caller is unaffected.</summary>
        internal long? OriginalEventId { get; }
        internal string? CompensationGroupId { get; }
        internal bool IsCompensating { get; }
    }
}
