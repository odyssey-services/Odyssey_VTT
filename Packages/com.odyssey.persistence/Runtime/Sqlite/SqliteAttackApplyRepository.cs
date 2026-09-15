using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using Odyssey.Application.Commands;
using Odyssey.Application.Dice;
using Odyssey.Application.Effects;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Content;
using Odyssey.Domain.Effects;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;

namespace Odyssey.Persistence.Sqlite
{
    /// <summary>
    /// ODY-S05-604: the sole SQLite implementation of <see cref="IAttackApplyRepository"/>.
    /// Owns a new, standalone <c>AttackOutcome</c> table -- it never appends a
    /// method or column to <see cref="ICombatEncounterRepository"/> or
    /// <see cref="IInventoryRepository"/> (beyond what already exists),
    /// mirroring <see cref="SqliteActiveEffectRepository"/>'s own standalone-table
    /// idiom. Every mutating method commits through the shared
    /// <see cref="SqliteSavingPipeline"/> (ADR-012 section 5): the AttackOutcome
    /// row, the committed Game Log entry (only for an Accepted outcome), the
    /// DomainEvent, and the AppliedCommands idempotency row land in one SQLite
    /// transaction, or none of them do.
    ///
    /// ODY-S05-606 adds combat `ActiveEffect` application: an accepted
    /// attack's own `Apply` candidates are inserted directly into the
    /// existing `ActiveEffect` table, in this same transaction, by reusing
    /// `SqliteActiveEffectRepository`'s own internal SQL helpers
    /// (<see cref="SqliteActiveEffectRepository.InsertColumns"/>/
    /// <see cref="SqliteActiveEffectRepository.InsertPlaceholders"/>/
    /// <see cref="SqliteActiveEffectRepository.AddParameters"/>) -- never by
    /// calling <see cref="IActiveEffectRepository.CreateActiveEffect"/>
    /// itself, which would open its own separate connection/transaction and
    /// break ADR-029 section 8's own "as one participant in the attack's
    /// atomic apply transaction" requirement. See the ODY-S05-606 task
    /// contract's decision log for the full reasoning.
    ///
    /// ODY-S05-609 closes `604`'s own disclosed gap: an accepted attack's
    /// `DamageDeltas`/`CostDeltas` are resolved (via the `character:{id}:{resourceKind}`
    /// `TargetRef` convention -- see this class's own `ApplyAttackDelta`) and
    /// applied directly against `Character.ResourcesJson`, in this same
    /// transaction, by making `SqliteCharacterRepository.SerializeResources`/
    /// `DeserializeResources` `internal` for reuse -- never by calling the
    /// public `ICharacterRepository.SetResourceCurrentValue`, which (a) opens
    /// its own separate connection/transaction via `MutateResources`, and (b)
    /// hard-requires `actorIsMainGm == true`, which an immediate (non-
    /// intervention) attack accept is not necessarily. See the ODY-S05-609
    /// task contract's decision log for the full reasoning, including why an
    /// `item:`-targeted delta is a disclosed, escalated blocker rather than a
    /// silently-invented Domain field.
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

        public Result<AttackOutcomeRecord> RecordAttackOutcome(CampaignHandle campaign, AttackIntent intent, AttackRandomSample randomSample, bool interventionRequired, IReadOnlyList<AttackEffectCandidate> effectCandidates, IReadOnlyList<AttackDelta> damageDeltas, IReadOnlyList<AttackDelta> costDeltas, UserId actorUserId, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            if (effectCandidates == null) throw new ArgumentNullException(nameof(effectCandidates));
            if (damageDeltas == null) throw new ArgumentNullException(nameof(damageDeltas));
            if (costDeltas == null) throw new ArgumentNullException(nameof(costDeltas));
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
                                "INSERT INTO AttackOutcome (CommandId, CampaignId, EncounterId, ActorId, TargetIds, ActionItemInstanceId, ExpectedEncounterRevision, RandomSampleValue, InterventionRequired, EffectCandidatesJson, DamageDeltasJson, CostDeltasJson, OutcomeKind, GameLogEntryId, Revision, CreatedAt, ResolvedAt, ResolvedByCommandId) " +
                                "VALUES ($commandId, $campaignId, $encounterId, $actorId, $targetIds, $itemInstanceId, $expectedRevision, $randomSample, $interventionRequired, $effectCandidates, $damageDeltas, $costDeltas, $outcomeKind, NULL, 1, $createdAt, NULL, NULL);";
                            insert.Parameters.AddWithValue("$commandId", commandId.ToString());
                            insert.Parameters.AddWithValue("$campaignId", campaign.CampaignId.ToString());
                            insert.Parameters.AddWithValue("$encounterId", intent.EncounterId.ToString());
                            insert.Parameters.AddWithValue("$actorId", intent.ActorId.ToString());
                            insert.Parameters.AddWithValue("$targetIds", SerializeTargetIds(intent.TargetIds));
                            insert.Parameters.AddWithValue("$itemInstanceId", intent.ActionItemInstanceId.ToString());
                            insert.Parameters.AddWithValue("$expectedRevision", intent.ExpectedEncounterRevision);
                            insert.Parameters.AddWithValue("$randomSample", randomSample.Value);
                            insert.Parameters.AddWithValue("$interventionRequired", interventionRequired ? 1 : 0);
                            insert.Parameters.AddWithValue("$effectCandidates", SerializeEffectCandidates(effectCandidates));
                            insert.Parameters.AddWithValue("$damageDeltas", SerializeDeltas(damageDeltas));
                            insert.Parameters.AddWithValue("$costDeltas", SerializeDeltas(costDeltas));
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
                            ApplyEffectCandidates(connection, transaction, campaign, actorUserId, effectCandidates, includeRequiresIntervention: false, commandId, now);
                            Result deltaResult = ApplyAttackDeltas(connection, transaction, damageDeltas, costDeltas, commandId, now, correlationId);
                            if (deltaResult.IsFailure)
                            {
                                return Result<PipelineWrite<AttackOutcomeRecord>>.Failure(deltaResult.Error);
                            }
                        }

                        AttackOutcomeRecord result = new AttackOutcomeRecord(commandId, campaign.CampaignId, intent.EncounterId, intent.ActorId, intent.TargetIds, intent.ActionItemInstanceId, intent.ExpectedEncounterRevision, randomSample.Value, interventionRequired, effectCandidates, damageDeltas, costDeltas, outcomeKind, gameLogEntryId, now, null, null);
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
                            // Approving the intervention approves the whole attack at today's
                            // single attack-level granularity (604's own Approve/Reject/Cancel
                            // shape has no per-candidate resolution) -- so RequiresIntervention
                            // candidates are now applied alongside any plain Apply candidates.
                            // See the ODY-S05-606 task contract's decision log.
                            ApplyEffectCandidates(connection, transaction, campaign, actorUserId, pending.EffectCandidates, includeRequiresIntervention: true, pendingCommandId, now);
                            Result deltaResult = ApplyAttackDeltas(connection, transaction, pending.DamageDeltas, pending.CostDeltas, pendingCommandId, now, correlationId);
                            if (deltaResult.IsFailure)
                            {
                                return Result<PipelineWrite<AttackOutcomeRecord>>.Failure(deltaResult.Error);
                            }
                        }

                        AttackOutcomeRecord resolved = new AttackOutcomeRecord(pending.ResolveAttackCommandId, pending.CampaignId, pending.EncounterId, pending.ActorId, pending.TargetIds, pending.ActionItemInstanceId, pending.ExpectedEncounterRevision, pending.RandomSampleValue, pending.InterventionRequired, pending.EffectCandidates, pending.DamageDeltas, pending.CostDeltas, newKind, gameLogEntryId, pending.CreatedAt, now, commandId);
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

        /// <summary>
        /// ODY-S05-607: `ADR-029` §1 rule 7's own compensating root command,
        /// by direct structural analogy to <c>SqliteCharacterRepository.RevertCharacterRulesetMigration</c>
        /// (`ODY-S04-113`): MainGM-only gate as this method's own first
        /// statement, a required non-empty reason code, a CAS guard against
        /// compensating the same original committing event twice, and a
        /// brand-new <c>SqliteSavingPipeline.Execute</c> call/transaction --
        /// never a reopening or edit of the original <c>RecordAttackOutcome</c>/
        /// <c>ResolveAttackIntervention</c> transaction. Only corrects the
        /// Game Log/`AttackOutcome` bookkeeping this block itself wrote (a
        /// new, causally-linked `GameLogEntries`/`DiceRolls` row pair); it
        /// never touches `ICharacterRepository`/`IInventoryRepository` or any
        /// Character/Item resource state (`ODY-S05-609`'s own territory).
        /// </summary>
        public Result<AttackCompensationRecord> CompensateAttackOutcome(CampaignHandle campaign, CommandId resolveAttackCommandId, string reasonCode, string correctedSummaryPayload, UserId actorUserId, bool actorIsMainGm, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!resolveAttackCommandId.IsValid) throw new ArgumentException("ResolveAttackCommandId is required.", nameof(resolveAttackCommandId));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            // ADR-029 section 1 rule 7: a compensating command is a separate,
            // authorized action -- MainGM-only, checked first, mirroring
            // RevertCharacterRulesetMigration's own exact placement.
            if (!actorIsMainGm)
            {
                return Result<AttackCompensationRecord>.Failure(PersistenceFailures.AttackOutcomeOperationDenied(correlationId));
            }

            if (string.IsNullOrWhiteSpace(reasonCode) || string.IsNullOrWhiteSpace(correctedSummaryPayload))
            {
                return Result<AttackCompensationRecord>.Failure(PersistenceFailures.AttackOutcomeCompensationReasonRequired(correlationId));
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
                    tryReplay: transaction => ReplayCompensation(connection, transaction, commandId, correlationId),
                    apply: transaction =>
                    {
                        AttackOutcomeRecord? original = ReadByCommandId(connection, transaction, resolveAttackCommandId);
                        if (original == null)
                        {
                            return Result<PipelineWrite<AttackCompensationRecord>>.Failure(PersistenceFailures.AttackOutcomeNotFound(correlationId));
                        }

                        if (original.OutcomeKind != AttackOutcomeKind.Accepted || original.GameLogEntryId == null)
                        {
                            return Result<PipelineWrite<AttackCompensationRecord>>.Failure(PersistenceFailures.AttackOutcomeNotAccepted(correlationId));
                        }

                        CommandId committingCommandId = original.ResolvedByCommandId ?? original.ResolveAttackCommandId;
                        long? originalEventSequence = FindCommittingEventSequence(connection, transaction, campaign.CampaignId, committingCommandId);
                        if (originalEventSequence == null)
                        {
                            return Result<PipelineWrite<AttackCompensationRecord>>.Failure(PersistenceFailures.AttackOutcomeNotFound(correlationId));
                        }

                        if (IsAlreadyCompensated(connection, transaction, originalEventSequence.Value))
                        {
                            return Result<PipelineWrite<AttackCompensationRecord>>.Failure(PersistenceFailures.AttackOutcomeAlreadyCompensated(correlationId));
                        }

                        UtcInstant now = _clock.GetUtcNow();
                        string compensationGroupId = commandId.ToString();
                        string newLogEntryId = "log_" + Guid.NewGuid().ToString("N");
                        (DiceRollAudienceKind audienceKind, string audienceUsersJson, string audienceGroupsJson) = ComputeEncounterAudience(connection, transaction, original.EncounterId);
                        WriteCompensatingGameLogEntry(connection, transaction, campaign.CampaignId, newLogEntryId, commandId, actorUserId, original, correctedSummaryPayload, audienceKind, audienceUsersJson, audienceGroupsJson, now);

                        var payload = new JObject
                        {
                            ["resolveAttackCommandId"] = resolveAttackCommandId.ToString(),
                            ["reasonCode"] = reasonCode,
                            ["correctedSummaryPayload"] = correctedSummaryPayload,
                            ["gameLogEntryId"] = newLogEntryId,
                            ["actorUserId"] = actorUserId.ToString(),
                            ["compensationGroupId"] = compensationGroupId,
                        };

                        var result = new AttackCompensationRecord(commandId, resolveAttackCommandId, reasonCode, correctedSummaryPayload, newLogEntryId, now);
                        return Result<PipelineWrite<AttackCompensationRecord>>.Success(new PipelineWrite<AttackCompensationRecord>(
                            result, "odyssey.persistence.attack_outcome_compensated", payload.ToString(Newtonsoft.Json.Formatting.None), newLogEntryId,
                            aggregateType: "attack_outcome_compensation", aggregateId: newLogEntryId, aggregateRevision: 1,
                            onEventSequenceAssigned: (txn, sequence) => UpdateGameLogAuthoritativeSequence(connection, txn, newLogEntryId, sequence),
                            originalEventId: originalEventSequence.Value, compensationGroupId: compensationGroupId, isCompensating: true));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<AttackCompensationRecord>.Failure(PersistenceFailures.AttackOutcomeIoFailed(correlationId));
            }
        }

        private static Result<AttackCompensationRecord> ReplayCompensation(SqliteConnection connection, SqliteTransaction transaction, CommandId commandId, CorrelationId correlationId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT PayloadJson, CreatedAtHost FROM DomainEvents WHERE CommandId = $commandId AND EventType = 'odyssey.persistence.attack_outcome_compensated' LIMIT 1;";
            select.Parameters.AddWithValue("$commandId", commandId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read())
            {
                return Result<AttackCompensationRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId));
            }

            var payload = JObject.Parse(reader.GetString(0));
            CommandId originalCommandId = CommandId.Parse((string)payload["resolveAttackCommandId"]!);
            string reasonCode = (string)payload["reasonCode"]!;
            string correctedSummary = (string)payload["correctedSummaryPayload"]!;
            string gameLogEntryId = (string)payload["gameLogEntryId"]!;
            UtcInstant createdAt = UtcInstant.Parse(reader.GetString(1));
            return Result<AttackCompensationRecord>.Success(new AttackCompensationRecord(commandId, originalCommandId, reasonCode, correctedSummary, gameLogEntryId, createdAt));
        }

        private static long? FindCommittingEventSequence(SqliteConnection connection, SqliteTransaction transaction, CampaignId campaignId, CommandId committingCommandId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT EventSequence FROM DomainEvents WHERE CampaignId = $campaignId AND CommandId = $commandId AND EventType IN ('odyssey.persistence.attack_outcome_recorded','odyssey.persistence.attack_outcome_resolved') LIMIT 1;";
            select.Parameters.AddWithValue("$campaignId", campaignId.ToString());
            select.Parameters.AddWithValue("$commandId", committingCommandId.ToString());
            object? value = select.ExecuteScalar();
            return value == null ? (long?)null : Convert.ToInt64(value);
        }

        private static bool IsAlreadyCompensated(SqliteConnection connection, SqliteTransaction transaction, long originalEventSequence)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT COUNT(*) FROM DomainEvents WHERE OriginalEventId = $originalEventId AND EventType = 'odyssey.persistence.attack_outcome_compensated';";
            select.Parameters.AddWithValue("$originalEventId", originalEventSequence);
            return Convert.ToInt64(select.ExecuteScalar()) > 0;
        }

        private static void WriteCompensatingGameLogEntry(SqliteConnection connection, SqliteTransaction transaction, CampaignId campaignId, string logEntryId, CommandId compensatingCommandId, UserId actorUserId, AttackOutcomeRecord original, string correctedSummaryPayload, DiceRollAudienceKind audienceKind, string audienceUsersJson, string audienceGroupsJson, UtcInstant now)
        {
            string rollId = "roll_" + Guid.NewGuid().ToString("N");
            using (var insertRoll = connection.CreateCommand())
            {
                insertRoll.Transaction = transaction;
                insertRoll.CommandText =
                    "INSERT INTO DiceRolls (RollId, CampaignId, ActorUserId, Purpose, FormulaOriginal, FormulaNormalized, FormulaParserVersion, NaturalResultsJson, ModifierEntriesJson, BaseTotal, RngAlgorithmVersion, Status, PreviousRollId, CreatedAt, AudienceKind, AudienceSelectedUserIdsJson, AudienceSelectedGroupIdsJson, LastCommandId) " +
                    "VALUES ($rollId, $campaignId, $actorUserId, 'combat.attack.roll', '1d100', '1d100', 1, $naturalResults, '[]', $baseTotal, 1, 'Resolved', NULL, $createdAt, $audienceKind, $audienceUsers, $audienceGroups, $lastCommandId);";
                insertRoll.Parameters.AddWithValue("$rollId", rollId);
                insertRoll.Parameters.AddWithValue("$campaignId", campaignId.ToString());
                insertRoll.Parameters.AddWithValue("$actorUserId", actorUserId.ToString());
                // Reuses the original committed roll value verbatim -- compensation is
                // bookkeeping correction only, never a re-roll (ADR-008; governing ТЗ
                // section 3's own explicit invariant).
                insertRoll.Parameters.AddWithValue("$naturalResults", "[{\"dieIndex\":0,\"groupIndex\":0,\"sides\":100,\"value\":" + original.RandomSampleValue.ToString(CultureInfo.InvariantCulture) + "}]");
                insertRoll.Parameters.AddWithValue("$baseTotal", original.RandomSampleValue);
                insertRoll.Parameters.AddWithValue("$createdAt", now.ToString());
                insertRoll.Parameters.AddWithValue("$audienceKind", audienceKind.ToString());
                insertRoll.Parameters.AddWithValue("$audienceUsers", audienceUsersJson);
                insertRoll.Parameters.AddWithValue("$audienceGroups", audienceGroupsJson);
                insertRoll.Parameters.AddWithValue("$lastCommandId", compensatingCommandId.ToString());
                insertRoll.ExecuteNonQuery();
            }

            using (var insertEntry = connection.CreateCommand())
            {
                insertEntry.Transaction = transaction;
                insertEntry.CommandText =
                    "INSERT INTO GameLogEntries (LogEntryId, CampaignId, RootCommandId, EntryType, SummaryPayload, ActorUserId, DiceRollId, CreatedAt, AuthoritativeSequence, LastCommandId) " +
                    "VALUES ($logEntryId, $campaignId, $rootCommandId, 'AttackCompensated', $summaryPayload, $actorUserId, $diceRollId, $createdAt, 0, $lastCommandId);";
                insertEntry.Parameters.AddWithValue("$logEntryId", logEntryId);
                insertEntry.Parameters.AddWithValue("$campaignId", campaignId.ToString());
                insertEntry.Parameters.AddWithValue("$rootCommandId", compensatingCommandId.ToString());
                insertEntry.Parameters.AddWithValue("$summaryPayload", correctedSummaryPayload);
                insertEntry.Parameters.AddWithValue("$actorUserId", actorUserId.ToString());
                insertEntry.Parameters.AddWithValue("$diceRollId", rollId);
                insertEntry.Parameters.AddWithValue("$createdAt", now.ToString());
                insertEntry.Parameters.AddWithValue("$lastCommandId", compensatingCommandId.ToString());
                insertEntry.ExecuteNonQuery();
            }
        }

        private static void WriteAcceptedAttackGameLogEntry(SqliteConnection connection, SqliteTransaction transaction, CampaignId campaignId, string logEntryId, CommandId rootCommandId, UserId actorUserId, AttackIntent intent, int randomSampleValue, UtcInstant now)
        {
            string rollId = "roll_" + Guid.NewGuid().ToString("N");
            (DiceRollAudienceKind audienceKind, string audienceUsersJson, string audienceGroupsJson) = ComputeEncounterAudience(connection, transaction, intent.EncounterId);
            using (var insertRoll = connection.CreateCommand())
            {
                insertRoll.Transaction = transaction;
                insertRoll.CommandText =
                    "INSERT INTO DiceRolls (RollId, CampaignId, ActorUserId, Purpose, FormulaOriginal, FormulaNormalized, FormulaParserVersion, NaturalResultsJson, ModifierEntriesJson, BaseTotal, RngAlgorithmVersion, Status, PreviousRollId, CreatedAt, AudienceKind, AudienceSelectedUserIdsJson, AudienceSelectedGroupIdsJson, LastCommandId) " +
                    "VALUES ($rollId, $campaignId, $actorUserId, 'combat.attack.roll', '1d100', '1d100', 1, $naturalResults, '[]', $baseTotal, 1, 'Resolved', NULL, $createdAt, $audienceKind, $audienceUsers, $audienceGroups, $lastCommandId);";
                insertRoll.Parameters.AddWithValue("$rollId", rollId);
                insertRoll.Parameters.AddWithValue("$campaignId", campaignId.ToString());
                insertRoll.Parameters.AddWithValue("$actorUserId", actorUserId.ToString());
                insertRoll.Parameters.AddWithValue("$naturalResults", "[{\"dieIndex\":0,\"groupIndex\":0,\"sides\":100,\"value\":" + randomSampleValue.ToString(CultureInfo.InvariantCulture) + "}]");
                insertRoll.Parameters.AddWithValue("$baseTotal", randomSampleValue);
                insertRoll.Parameters.AddWithValue("$createdAt", now.ToString());
                insertRoll.Parameters.AddWithValue("$audienceKind", audienceKind.ToString());
                insertRoll.Parameters.AddWithValue("$audienceUsers", audienceUsersJson);
                insertRoll.Parameters.AddWithValue("$audienceGroups", audienceGroupsJson);
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

        /// <summary>
        /// ODY-S05-607: `ADR-029` §1's own attack-participants-see-the-result
        /// expectation, computed as "every current combat encounter participant's
        /// owning user(s) + MainGM (unconditional via `DiceRollVisibilityPolicy`,
        /// unmodified)" -- reusing the already-existing `DiceRollAudienceKind.SelectedParticipants`
        /// shape rather than a new enum value. Reads the already-existing
        /// `CombatEncounterParticipant`/`Character` tables directly on this same
        /// connection/transaction (both tables are guaranteed to already exist:
        /// this method only ever runs after `AttackEvaluationService` already
        /// read this same encounter/actor successfully earlier in the same call).
        /// No method is appended to `ICombatEncounterRepository`/`ICharacterRepository`
        /// to obtain this -- a plain read against their own existing tables.
        /// Falls back to `GMOnly` only when no participant has any owning user at
        /// all (e.g. an all-NPC encounter) -- `SelectedParticipants` itself
        /// requires at least one selected user/group and MainGM already sees
        /// everything unconditionally, so `GMOnly` is the honest, narrowest
        /// audience for that edge case, not a widening.
        /// </summary>
        private static (DiceRollAudienceKind Kind, string UsersJson, string GroupsJson) ComputeEncounterAudience(SqliteConnection connection, SqliteTransaction transaction, CombatEncounterId encounterId)
        {
            var userIds = new List<string>();
            using (var select = connection.CreateCommand())
            {
                select.Transaction = transaction;
                select.CommandText =
                    "SELECT c.PrimaryOwnerUserId, c.CoOwnerUserIdsJson FROM CombatEncounterParticipant p " +
                    "JOIN Character c ON c.CharacterId = p.CharacterId WHERE p.EncounterId = $encounterId;";
                select.Parameters.AddWithValue("$encounterId", encounterId.ToString());
                using SqliteDataReader reader = select.ExecuteReader();
                while (reader.Read())
                {
                    if (!reader.IsDBNull(0)) AddDistinct(userIds, reader.GetString(0));
                    foreach (string coOwner in DeserializeStringArray(reader.GetString(1))) AddDistinct(userIds, coOwner);
                }
            }

            if (userIds.Count == 0)
            {
                return (DiceRollAudienceKind.GMOnly, "[]", "[]");
            }

            return (DiceRollAudienceKind.SelectedParticipants, SerializeStringArray(userIds), "[]");
        }

        private static void AddDistinct(List<string> values, string value)
        {
            if (!values.Contains(value, StringComparer.Ordinal)) values.Add(value);
        }

        private static string SerializeStringArray(IReadOnlyList<string> values)
        {
            var array = new JArray();
            foreach (string value in values) array.Add(value);
            return array.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static IReadOnlyList<string> DeserializeStringArray(string json)
        {
            var array = JArray.Parse(json);
            var result = new List<string>(array.Count);
            foreach (JToken token in array) result.Add((string)token!);
            return result;
        }

        /// <summary>
        /// ODY-S05-606: applies every candidate whose decision is `Apply` (and,
        /// when <paramref name="includeRequiresIntervention"/> is true --
        /// only from <see cref="ResolveAttackIntervention"/>'s own Approve
        /// path -- every `RequiresIntervention` candidate too) by routing it
        /// through the existing `ADR-028` `ActiveEffectStackingRules` decision
        /// layer against the same connection/transaction, so the resulting
        /// `ActiveEffect` row/mutation lands in this same atomic-apply
        /// transaction. `DoNotApply` candidates, and `RequiresIntervention`
        /// candidates when <paramref name="includeRequiresIntervention"/> is
        /// false, are skipped entirely -- never a row.
        /// </summary>
        private static void ApplyEffectCandidates(SqliteConnection connection, SqliteTransaction transaction, CampaignHandle campaign, UserId actorUserId, IReadOnlyList<AttackEffectCandidate> candidates, bool includeRequiresIntervention, CommandId commandId, UtcInstant now)
        {
            if (candidates.Count == 0)
            {
                return;
            }

            SqliteActiveEffectRepository.EnsureActiveEffectTables(connection);

            foreach (AttackEffectCandidate candidate in candidates)
            {
                bool shouldApply = candidate.Decision == EffectApplicationDecision.Apply
                    || (includeRequiresIntervention && candidate.Decision == EffectApplicationDecision.RequiresIntervention);
                if (!shouldApply)
                {
                    // ADR-029 section 8: only a final Apply decision creates/stacks an
                    // ActiveEffect. DoNotApply, and an unresolved RequiresIntervention,
                    // never do.
                    continue;
                }

                ActiveEffectTargetRef targetRef = ActiveEffectTargetRef.ForCharacter(candidate.TargetId);
                ActiveEffect candidateEffect = new ActiveEffect(
                    ActiveEffectId.NewId(now), candidate.EffectRef, candidate.MechanicsSnapshot,
                    ActiveEffectSourceRef.ForAction(), targetRef, ActiveEffectStatus.Active, 1,
                    actorUserId, now, null, 1, candidate.DurationBinding);
                ActiveEffectRecord candidateRecord = new ActiveEffectRecord(campaign.CampaignId, candidateEffect);

                ActiveEffectRecord? existing = FindConflictingActiveEffect(connection, transaction, campaign.CampaignId, targetRef, candidate.EffectRef);
                ActiveEffectStackDecision decision = ActiveEffectStackingRules.ResolveStacking(candidate.StackPolicy, existing, candidateRecord, now);

                if (decision.Kind == ActiveEffectStackDecisionKind.RequestGmResolution)
                {
                    // ODY-S05-610: closes 606's own disclosed gap -- a durable
                    // pending record is now written in this SAME atomic-apply
                    // transaction, so a MainGM can later resolve it via
                    // ResolveStackConflict. The rest of the attack's own
                    // atomic apply still commits regardless (ADR-028 section
                    // 7 rule 6's own "silent success" convention extends to
                    // an unresolved conflict too).
                    RecordStackConflict(connection, transaction, campaign.CampaignId, commandId, decision.Conflict!, now);
                    continue;
                }

                ApplyStackDecisionMutation(connection, transaction, decision, commandId, now);
            }
        }

        /// <summary>
        /// ODY-S05-606/610: the shared mutation this codebase's own two
        /// producers of an `ActiveEffectStackDecision` (`ApplyEffectCandidates`'s
        /// own immediate `ResolveStacking` call, and `ResolveStackConflict`'s
        /// own `ResolveActiveEffectStackConflict` call) both apply -- extracted
        /// so the create/replace/refresh/increase/ignore logic is written and
        /// tested exactly once, per this task's own explicit "reuse the
        /// switch, do not duplicate it" requirement. `RequestGmResolution` is
        /// deliberately NOT handled here: `ApplyEffectCandidates` intercepts
        /// that kind itself (to persist a new pending conflict, not mutate an
        /// `ActiveEffect` row), and `ResolveActiveEffectStackConflict` can
        /// never itself produce that kind again -- reaching it here is a
        /// caller error, not a reachable runtime state.
        /// </summary>
        private static void ApplyStackDecisionMutation(SqliteConnection connection, SqliteTransaction transaction, ActiveEffectStackDecision decision, CommandId commandId, UtcInstant now)
        {
            switch (decision.Kind)
            {
                case ActiveEffectStackDecisionKind.CreateNewEffect:
                    InsertActiveEffectRow(connection, transaction, decision.EffectToCreate!, commandId, now);
                    break;
                case ActiveEffectStackDecisionKind.ReplaceExistingEffect:
                    UpdateActiveEffectStatus(connection, transaction, decision.ExistingActiveEffectId!.Value, ActiveEffectStatus.Removed, now);
                    InsertActiveEffectRow(connection, transaction, decision.EffectToCreate!, commandId, now);
                    break;
                case ActiveEffectStackDecisionKind.RefreshExistingDuration:
                    RefreshActiveEffectDuration(connection, transaction, decision.ExistingActiveEffectId!.Value, decision.RefreshedAppliedAt!.Value, decision.RefreshedExpiresAt, now);
                    break;
                case ActiveEffectStackDecisionKind.IncreaseExistingStack:
                    IncreaseActiveEffectStack(connection, transaction, decision.ExistingActiveEffectId!.Value, now);
                    break;
                case ActiveEffectStackDecisionKind.IgnoreNewApplication:
                    // ADR-028 section 7 rule 6's own "silent success" convention: the
                    // triggering command still commits; only this decision's own
                    // stacking outcome is a no-op.
                    break;
                case ActiveEffectStackDecisionKind.RequestGmResolution:
                    throw new InvalidOperationException("RequestGmResolution must be intercepted by the caller (to persist a pending conflict) before reaching ApplyStackDecisionMutation.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(decision), decision.Kind, "Unrecognized ActiveEffectStackDecisionKind.");
            }
        }

        /// <summary>
        /// ODY-S05-610: `ADR-028` §7 rule 7's own durable pending-conflict
        /// write -- inside the SAME atomic-apply transaction as the rest of
        /// the triggering attack's own commit (never a second connection/
        /// transaction). Embeds <paramref name="conflict"/>'s own
        /// `CandidateApplication` using the exact same column shape/order
        /// <see cref="SqliteActiveEffectRepository.InsertPlaceholders"/>/
        /// <see cref="SqliteActiveEffectRepository.AddParameters"/> already
        /// define for the real `ActiveEffect` table -- reusing that
        /// serialization logic verbatim rather than duplicating it, even
        /// though this row lands in a different, standalone table.
        /// </summary>
        private static void RecordStackConflict(SqliteConnection connection, SqliteTransaction transaction, CampaignId campaignId, CommandId raisingCommandId, ActiveEffectStackConflict conflict, UtcInstant now)
        {
            using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText =
                    "INSERT INTO CombatStackConflict (CommandId, ConflictingActiveEffectId, " +
                    "ActiveEffectId, CampaignId, EffectDefinitionRef, " +
                    "MechanicsSourceDefinitionRef, MechanicsDefinitionSnapshotVersion, MechanicsContentType, MechanicsPayload, " +
                    "SourceKind, SourceItemRefKind, SourceItemRefId, " +
                    "TargetKind, TargetCharacterId, TargetItemInstanceId, " +
                    "Status, StackCount, AppliedByUserId, AppliedAt, ExpiresAt, Revision, UpdatedAt, LastCommandId, " +
                    "CombatEncounterId, CombatSourceCombatantId, CombatTargetCombatantId, CombatAppliedRoundOrdinal, CombatAppliedLifecycleEventId, CombatRequiredCount, " +
                    "RaisedAt, ConflictStatus, ResolvedAt, ResolvedByCommandId, Resolution) VALUES " +
                    "($commandId, $conflictingActiveEffectId, " +
                    "$activeEffectId, $campaignId, $effectDefinitionRef, " +
                    "$mechanicsSourceDefinitionRef, $mechanicsDefinitionSnapshotVersion, $mechanicsContentType, $mechanicsPayload, " +
                    "$sourceKind, $sourceItemRefKind, $sourceItemRefId, " +
                    "$targetKind, $targetCharacterId, $targetItemInstanceId, " +
                    "$status, $stackCount, $appliedByUserId, $appliedAt, $expiresAt, $revision, $updatedAt, $lastCommandId, " +
                    "$combatEncounterId, $combatSourceCombatantId, $combatTargetCombatantId, $combatAppliedRoundOrdinal, $combatAppliedLifecycleEventId, $combatRequiredCount, " +
                    "$raisedAt, 'Pending', NULL, NULL, NULL);";
                insert.Parameters.AddWithValue("$commandId", raisingCommandId.ToString());
                insert.Parameters.AddWithValue("$conflictingActiveEffectId", conflict.ConflictingActiveEffectId.ToString());
                SqliteActiveEffectRepository.AddParameters(insert, conflict.CandidateApplication, now);
                insert.Parameters.AddWithValue("$lastCommandId", raisingCommandId.ToString());
                insert.Parameters.AddWithValue("$raisedAt", conflict.RaisedAt.ToString());
                insert.ExecuteNonQuery();
            }

            // Governing ТЗ section 2 decision 4: a new, distinct DomainEvent
            // type for the raise side, in addition to the triggering
            // ResolveAttack/ResolveAttackIntervention command's own
            // attack_outcome_recorded/resolved event -- appended directly via
            // SqliteSavingPipeline's own internal AppendDomainEvent (ODY-S04-107's
            // own "more than one event per transaction" precedent), inside this
            // SAME transaction, not a separate commit.
            var payload = new JObject
            {
                ["raisingCommandId"] = raisingCommandId.ToString(),
                ["conflictingActiveEffectId"] = conflict.ConflictingActiveEffectId.ToString(),
                ["targetRef"] = conflict.CandidateApplication.Effect.TargetRef.Kind.ToString(),
            };
            SqliteSavingPipeline.AppendDomainEvent(connection, transaction, campaignId, raisingCommandId, "odyssey.persistence.combat_stack_conflict_raised", payload.ToString(Newtonsoft.Json.Formatting.None), now);
        }

        private static CombatStackConflictRecord? ReadStackConflict(SqliteConnection connection, SqliteTransaction? transaction, CommandId raisingCommandId, ActiveEffectId conflictingActiveEffectId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = SqliteActiveEffectRepository.SelectColumns +
                ", RaisedAt, ConflictStatus, ResolvedAt, ResolvedByCommandId, Resolution FROM CombatStackConflict WHERE CommandId = $commandId AND ConflictingActiveEffectId = $conflictingActiveEffectId LIMIT 1;";
            select.Parameters.AddWithValue("$commandId", raisingCommandId.ToString());
            select.Parameters.AddWithValue("$conflictingActiveEffectId", conflictingActiveEffectId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            ActiveEffectRecord candidate = SqliteActiveEffectRepository.ReadRecord(reader);
            UtcInstant raisedAt = UtcInstant.Parse(reader.GetString(25));
            bool isResolved = reader.GetString(26) == "Resolved";
            UtcInstant? resolvedAt = reader.IsDBNull(27) ? (UtcInstant?)null : UtcInstant.Parse(reader.GetString(27));
            CommandId? resolvedByCommandId = reader.IsDBNull(28) ? (CommandId?)null : CommandId.Parse(reader.GetString(28));
            ActiveEffectStackConflictResolution? resolution = reader.IsDBNull(29) ? (ActiveEffectStackConflictResolution?)null : Enum.Parse<ActiveEffectStackConflictResolution>(reader.GetString(29));
            return new CombatStackConflictRecord(raisingCommandId, candidate.CampaignId, candidate, conflictingActiveEffectId, raisedAt, isResolved, resolvedAt, resolvedByCommandId, resolution);
        }

        /// <summary>
        /// ODY-S05-610: `ADR-028` §7 rule 7's own resolution root command --
        /// by direct structural analogy to `ResolveAttackIntervention`
        /// (`ODY-S05-604`): MainGM-only gate as this method's own first
        /// statement, a CAS guard against resolving the same pending conflict
        /// twice, and a brand-new `SqliteSavingPipeline.Execute` call/
        /// transaction -- never a reopening of the original triggering
        /// attack's own transaction, which has long since committed by the
        /// time a MainGM resolves this. Reuses the existing, unmodified
        /// `ActiveEffectStackingRules.ResolveActiveEffectStackConflict` and
        /// `ApplyStackDecisionMutation` (this same class) to apply the
        /// result -- never a duplicated create/replace/ignore implementation,
        /// and never the public `IActiveEffectRepository.CreateActiveEffect`.
        /// </summary>
        public Result<CombatStackConflictRecord> ResolveStackConflict(CampaignHandle campaign, CommandId raisingCommandId, ActiveEffectId conflictingActiveEffectId, ActiveEffectStackConflictResolution resolution, UserId actorUserId, bool actorIsMainGm, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!raisingCommandId.IsValid) throw new ArgumentException("RaisingCommandId is required.", nameof(raisingCommandId));
            if (!conflictingActiveEffectId.IsValid) throw new ArgumentException("ConflictingActiveEffectId is required.", nameof(conflictingActiveEffectId));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            // ADR-028 section 7 rule 7: a MainGM resolves a pending stacking
            // conflict -- checked as this method's own first statement,
            // mirroring ResolveAttackIntervention's exact placement.
            if (!actorIsMainGm)
            {
                return Result<CombatStackConflictRecord>.Failure(PersistenceFailures.CombatStackConflictOperationDenied(correlationId));
            }

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureAttackApplyTables(connection);
                SqliteActiveEffectRepository.EnsureActiveEffectTables(connection);

                return _pipeline.Execute(
                    connection,
                    campaign.CampaignId,
                    commandId,
                    correlationId,
                    tryReplay: transaction => ReplayStackConflictResolution(connection, transaction, commandId, correlationId),
                    apply: transaction =>
                    {
                        CombatStackConflictRecord? pending = ReadStackConflict(connection, transaction, raisingCommandId, conflictingActiveEffectId);
                        if (pending == null)
                        {
                            return Result<PipelineWrite<CombatStackConflictRecord>>.Failure(PersistenceFailures.CombatStackConflictNotFound(correlationId));
                        }

                        if (pending.IsResolved)
                        {
                            // CAS guard: an already-resolved conflict is a typed conflict,
                            // not a silent no-op.
                            return Result<PipelineWrite<CombatStackConflictRecord>>.Failure(PersistenceFailures.CombatStackConflictAlreadyResolved(correlationId));
                        }

                        UtcInstant now = _clock.GetUtcNow();
                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText =
                                "UPDATE CombatStackConflict SET ConflictStatus='Resolved', ResolvedAt=$resolvedAt, ResolvedByCommandId=$resolvedBy, Resolution=$resolution " +
                                "WHERE CommandId=$raisingCommandId AND ConflictingActiveEffectId=$conflictingActiveEffectId AND ConflictStatus='Pending';";
                            update.Parameters.AddWithValue("$resolvedAt", now.ToString());
                            update.Parameters.AddWithValue("$resolvedBy", commandId.ToString());
                            update.Parameters.AddWithValue("$resolution", resolution.ToString());
                            update.Parameters.AddWithValue("$raisingCommandId", raisingCommandId.ToString());
                            update.Parameters.AddWithValue("$conflictingActiveEffectId", conflictingActiveEffectId.ToString());
                            if (update.ExecuteNonQuery() != 1)
                            {
                                // Lost a race against a concurrent resolution between the
                                // read above and this guarded UPDATE.
                                return Result<PipelineWrite<CombatStackConflictRecord>>.Failure(PersistenceFailures.CombatStackConflictAlreadyResolved(correlationId));
                            }
                        }

                        var conflict = new ActiveEffectStackConflict(pending.CandidateApplication, conflictingActiveEffectId, pending.RaisedAt);
                        ActiveEffectStackDecision decision = ActiveEffectStackingRules.ResolveActiveEffectStackConflict(conflict, resolution);
                        ApplyStackDecisionMutation(connection, transaction, decision, commandId, now);

                        var resolvedRecord = new CombatStackConflictRecord(raisingCommandId, pending.CampaignId, pending.CandidateApplication, conflictingActiveEffectId, pending.RaisedAt, isResolved: true, now, commandId, resolution);
                        var payload = new JObject
                        {
                            ["raisingCommandId"] = raisingCommandId.ToString(),
                            ["conflictingActiveEffectId"] = conflictingActiveEffectId.ToString(),
                            ["resolution"] = resolution.ToString(),
                            ["actorUserId"] = actorUserId.ToString(),
                        };

                        string aggregateId = raisingCommandId + ":" + conflictingActiveEffectId;
                        return Result<PipelineWrite<CombatStackConflictRecord>>.Success(new PipelineWrite<CombatStackConflictRecord>(
                            resolvedRecord, "odyssey.persistence.combat_stack_conflict_resolved", payload.ToString(Newtonsoft.Json.Formatting.None), aggregateId,
                            aggregateType: "combat_stack_conflict", aggregateId: aggregateId, aggregateRevision: 2));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CombatStackConflictRecord>.Failure(PersistenceFailures.CombatStackConflictIoFailed(correlationId));
            }
        }

        private static Result<CombatStackConflictRecord> ReplayStackConflictResolution(SqliteConnection connection, SqliteTransaction transaction, CommandId commandId, CorrelationId correlationId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT PayloadJson FROM DomainEvents WHERE CommandId = $commandId AND EventType = 'odyssey.persistence.combat_stack_conflict_resolved' LIMIT 1;";
            select.Parameters.AddWithValue("$commandId", commandId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read())
            {
                return Result<CombatStackConflictRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId));
            }

            var payload = JObject.Parse(reader.GetString(0));
            CommandId raisingCommandId = CommandId.Parse((string)payload["raisingCommandId"]!);
            ActiveEffectId conflictingActiveEffectId = ActiveEffectId.Parse((string)payload["conflictingActiveEffectId"]!);
            CombatStackConflictRecord? resolved = ReadStackConflict(connection, transaction, raisingCommandId, conflictingActiveEffectId);
            return resolved == null
                ? Result<CombatStackConflictRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId))
                : Result<CombatStackConflictRecord>.Success(resolved);
        }

        private static ActiveEffectRecord? FindConflictingActiveEffect(SqliteConnection connection, SqliteTransaction transaction, CampaignId campaignId, ActiveEffectTargetRef targetRef, ContentDefinitionRef effectDefinitionRef)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = SqliteActiveEffectRepository.SelectColumns +
                " FROM ActiveEffect WHERE CampaignId=$campaignId AND TargetKind=$targetKind AND TargetCharacterId IS $targetCharacterId AND TargetItemInstanceId IS $targetItemInstanceId AND EffectDefinitionRef=$effectDefinitionRef AND Status='Active' LIMIT 1;";
            select.Parameters.AddWithValue("$campaignId", campaignId.ToString());
            select.Parameters.AddWithValue("$targetKind", targetRef.Kind.ToString());
            select.Parameters.AddWithValue("$targetCharacterId", targetRef.Kind == ActiveEffectTargetKind.Character ? targetRef.CharacterId.ToString() : (object)DBNull.Value);
            select.Parameters.AddWithValue("$targetItemInstanceId", targetRef.Kind == ActiveEffectTargetKind.ItemInstance ? targetRef.ItemInstanceId.ToString() : (object)DBNull.Value);
            select.Parameters.AddWithValue("$effectDefinitionRef", effectDefinitionRef.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            return reader.Read() ? SqliteActiveEffectRepository.ReadRecord(reader) : null;
        }

        private static void InsertActiveEffectRow(SqliteConnection connection, SqliteTransaction transaction, ActiveEffectRecord record, CommandId commandId, UtcInstant now)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = SqliteActiveEffectRepository.InsertColumns + " VALUES " + SqliteActiveEffectRepository.InsertPlaceholders + ";";
            SqliteActiveEffectRepository.AddParameters(insert, record, now);
            insert.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
            insert.ExecuteNonQuery();
        }

        private static void UpdateActiveEffectStatus(SqliteConnection connection, SqliteTransaction transaction, ActiveEffectId activeEffectId, ActiveEffectStatus status, UtcInstant now)
        {
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE ActiveEffect SET Status=$status, UpdatedAt=$now WHERE ActiveEffectId=$id;";
            update.Parameters.AddWithValue("$status", status.ToString());
            update.Parameters.AddWithValue("$now", now.ToString());
            update.Parameters.AddWithValue("$id", activeEffectId.ToString());
            update.ExecuteNonQuery();
        }

        private static void RefreshActiveEffectDuration(SqliteConnection connection, SqliteTransaction transaction, ActiveEffectId activeEffectId, UtcInstant appliedAt, UtcInstant? expiresAt, UtcInstant now)
        {
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE ActiveEffect SET AppliedAt=$appliedAt, ExpiresAt=$expiresAt, UpdatedAt=$now WHERE ActiveEffectId=$id;";
            update.Parameters.AddWithValue("$appliedAt", appliedAt.ToString());
            update.Parameters.AddWithValue("$expiresAt", expiresAt.HasValue ? expiresAt.Value.ToString() : (object)DBNull.Value);
            update.Parameters.AddWithValue("$now", now.ToString());
            update.Parameters.AddWithValue("$id", activeEffectId.ToString());
            update.ExecuteNonQuery();
        }

        private static void IncreaseActiveEffectStack(SqliteConnection connection, SqliteTransaction transaction, ActiveEffectId activeEffectId, UtcInstant now)
        {
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE ActiveEffect SET StackCount=StackCount+1, UpdatedAt=$now WHERE ActiveEffectId=$id;";
            update.Parameters.AddWithValue("$now", now.ToString());
            update.Parameters.AddWithValue("$id", activeEffectId.ToString());
            update.ExecuteNonQuery();
        }

        /// <summary>
        /// ODY-S05-609: `ADR-029` §1 rule 5/§6 stage 13 -- resolves and applies
        /// every already-computed `AttackDelta` (`damageDeltas` then
        /// `costDeltas`, in order) against the target identity its own
        /// `TargetRef` names, inside this same atomic-apply transaction.
        /// Stops and returns the first failure encountered (a typed,
        /// no-partial-commit rejection -- the caller returns it from the
        /// `apply:` lambda itself, so `SqliteSavingPipeline` rolls back the
        /// whole transaction, exactly like any other guard failure in this
        /// class). Multiple deltas targeting the SAME character in one
        /// attack (e.g. a `DamageDeltas` and a `CostDeltas` entry on the same
        /// actor) are applied strictly sequentially, each re-reading
        /// `Character.ResourcesJson`/`CharacterResourcesRevision` fresh from
        /// this same transaction -- so the second delta's own revision
        /// increment is always correct, with no risk of the two conflicting
        /// the way two independent calls to the public `SetResourceCurrentValue`
        /// (each with its own separately-read `expectedCharacterResourcesRevision`)
        /// would.
        /// </summary>
        private static Result ApplyAttackDeltas(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<AttackDelta> damageDeltas, IReadOnlyList<AttackDelta> costDeltas, CommandId commandId, UtcInstant now, CorrelationId correlationId)
        {
            foreach (AttackDelta delta in damageDeltas)
            {
                Result result = ApplyAttackDelta(connection, transaction, delta, commandId, now, correlationId);
                if (result.IsFailure)
                {
                    return result;
                }
            }

            foreach (AttackDelta delta in costDeltas)
            {
                Result result = ApplyAttackDelta(connection, transaction, delta, commandId, now, correlationId);
                if (result.IsFailure)
                {
                    return result;
                }
            }

            return Result.Success();
        }

        /// <summary>
        /// ODY-S05-609 section 3's own product-owner-approved `TargetRef`
        /// convention: `"character:{characterId}:{resourceKind}"` or
        /// `"item:{itemInstanceId}:{itemResourceKind}"` -- exactly three
        /// colon-separated segments, the first segment selecting the
        /// resolution path. This is identity resolution only: `delta.Value`
        /// itself is never recomputed, chosen, or clamped upward/downward by
        /// any Ruleset formula here.
        /// </summary>
        private static Result ApplyAttackDelta(SqliteConnection connection, SqliteTransaction transaction, AttackDelta delta, CommandId commandId, UtcInstant now, CorrelationId correlationId)
        {
            string[] segments = delta.TargetRef.Split(':');
            if (segments.Length != 3)
            {
                return Result.Failure(PersistenceFailures.AttackOutcomeDeltaTargetRefInvalid(correlationId));
            }

            switch (segments[0])
            {
                case "character":
                    if (!CharacterId.TryParse(segments[1], out CharacterId characterId) || !ResourceDefinitionId.TryParse(segments[2], out ResourceDefinitionId resourceDefinitionId))
                    {
                        return Result.Failure(PersistenceFailures.AttackOutcomeDeltaTargetRefInvalid(correlationId));
                    }

                    return ApplyCharacterResourceDelta(connection, transaction, characterId, resourceDefinitionId, delta.Value, commandId, now, correlationId);

                case "item":
                    // ODY-S05-609 section 4/decision log: ItemInstanceRecord
                    // carries only an opaque RuntimeState string -- no
                    // numeric mutable field exists at the Domain level to
                    // write a resource delta into (no durability/charges
                    // field anywhere in Odyssey.Domain.Inventory). Escalated
                    // as a disclosed blocker rather than silently inventing a
                    // new Domain field within this task's own scope.
                    return Result.Failure(PersistenceFailures.AttackOutcomeDeltaItemTargetUnsupported(correlationId));

                default:
                    return Result.Failure(PersistenceFailures.AttackOutcomeDeltaTargetRefInvalid(correlationId));
            }
        }

        /// <summary>
        /// ODY-S05-609: applies one already-computed delta to one
        /// <c>CharacterResource</c>'s <c>CurrentValue</c>, reading and
        /// writing <c>Character.ResourcesJson</c>/<c>CharacterResourcesRevision</c>/
        /// <c>CharacterRevision</c> directly via a minimal, targeted raw SQL
        /// SELECT/UPDATE against the already-open connection/transaction --
        /// never the public <c>ICharacterRepository.SetResourceCurrentValue</c>
        /// (see this class's own doc comment for why). Reuses
        /// <c>SqliteCharacterRepository.SerializeResources</c>/<c>DeserializeResources</c>
        /// (`internal`, ODY-S05-609) rather than duplicating that JSON shape;
        /// does not read or write any other Character column, so the full
        /// 36+-column <c>SelectForUpdate</c>/<c>CharacterRecord</c>
        /// round trip that public method uses is deliberately not needed
        /// here. Rejects (never clamps) a resolved value outside
        /// <c>[MinimumValue, EffectiveMaximum]</c>, mirroring
        /// <c>SetResourceCurrentValue</c>'s own behavior exactly.
        /// </summary>
        private static Result ApplyCharacterResourceDelta(SqliteConnection connection, SqliteTransaction transaction, CharacterId characterId, ResourceDefinitionId resourceDefinitionId, int deltaValue, CommandId commandId, UtcInstant now, CorrelationId correlationId)
        {
            string resourcesJson;
            long resourcesRevision;
            long characterRevision;
            using (var select = connection.CreateCommand())
            {
                select.Transaction = transaction;
                select.CommandText = "SELECT ResourcesJson, CharacterResourcesRevision, CharacterRevision FROM Character WHERE CharacterId = $characterId;";
                select.Parameters.AddWithValue("$characterId", characterId.ToString());
                using SqliteDataReader reader = select.ExecuteReader();
                if (!reader.Read())
                {
                    return Result.Failure(PersistenceFailures.AttackOutcomeDeltaResourceNotFound(correlationId));
                }

                resourcesJson = reader.GetString(0);
                resourcesRevision = reader.GetInt64(1);
                characterRevision = reader.GetInt64(2);
            }

            IReadOnlyList<CharacterResource> resources = SqliteCharacterRepository.DeserializeResources(resourcesJson);
            CharacterResource? existing = null;
            foreach (CharacterResource candidate in resources)
            {
                if (candidate.ResourceDefinitionId.Equals(resourceDefinitionId))
                {
                    existing = candidate;
                    break;
                }
            }

            if (existing == null)
            {
                return Result.Failure(PersistenceFailures.AttackOutcomeDeltaResourceNotFound(correlationId));
            }

            long newValue = existing.CurrentValue + deltaValue;
            if (newValue < existing.MinimumValue || newValue > existing.EffectiveMaximum)
            {
                return Result.Failure(PersistenceFailures.AttackOutcomeDeltaValueOutOfRange(correlationId));
            }

            var updated = new CharacterResource(existing.CharacterResourceId, existing.ResourceDefinitionId, newValue, existing.BaseMaximum, existing.PermanentMaximumAdjustment, existing.MinimumValue, existing.RecoveryRule, existing.Revision + 1);
            var newResources = new List<CharacterResource>(resources.Count);
            foreach (CharacterResource candidate in resources)
            {
                newResources.Add(candidate.ResourceDefinitionId.Equals(resourceDefinitionId) ? updated : candidate);
            }

            using (var update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = "UPDATE Character SET ResourcesJson = $resourcesJson, CharacterResourcesRevision = $resourcesRevision, CharacterRevision = $characterRevision, UpdatedAt = $updatedAt, LastCommandId = $lastCommandId WHERE CharacterId = $characterId;";
                update.Parameters.AddWithValue("$resourcesJson", SqliteCharacterRepository.SerializeResources(newResources));
                update.Parameters.AddWithValue("$resourcesRevision", resourcesRevision + 1);
                update.Parameters.AddWithValue("$characterRevision", characterRevision + 1);
                update.Parameters.AddWithValue("$updatedAt", now.ToString());
                update.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                update.Parameters.AddWithValue("$characterId", characterId.ToString());
                update.ExecuteNonQuery();
            }

            return Result.Success();
        }

        private static string SerializeDeltas(IReadOnlyList<AttackDelta> deltas)
        {
            var array = new JArray();
            foreach (AttackDelta delta in deltas)
            {
                array.Add(new JObject { ["targetRef"] = delta.TargetRef, ["value"] = delta.Value });
            }

            return array.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static IReadOnlyList<AttackDelta> DeserializeDeltas(string json)
        {
            var array = JArray.Parse(json);
            var result = new List<AttackDelta>(array.Count);
            foreach (JToken token in array)
            {
                var obj = (JObject)token;
                result.Add(new AttackDelta((string)obj["targetRef"]!, (int)obj["value"]!));
            }

            return result;
        }

        private static string SerializeEffectCandidates(IReadOnlyList<AttackEffectCandidate> candidates)
        {
            var array = new JArray();
            foreach (AttackEffectCandidate candidate in candidates)
            {
                var json = new JObject
                {
                    ["targetId"] = candidate.TargetId.ToString(),
                    ["effectRef"] = candidate.EffectRef.ToString(),
                    ["mechanicsRef"] = candidate.MechanicsRef.ToString(),
                    ["decision"] = candidate.Decision.ToString(),
                    ["reasonCategory"] = candidate.ReasonCategory.ToString(),
                    ["hostOnlyReasonDetail"] = candidate.HostOnlyReasonDetail,
                    ["stackPolicy"] = candidate.StackPolicy.ToString(),
                    ["mechanicsSnapshot"] = new JObject
                    {
                        ["sourceDefinitionRef"] = candidate.MechanicsSnapshot.SourceDefinitionRef.ToString(),
                        ["definitionSnapshotVersion"] = candidate.MechanicsSnapshot.DefinitionSnapshotVersion,
                        ["contentType"] = candidate.MechanicsSnapshot.ContentType.ToString(),
                        ["payload"] = candidate.MechanicsSnapshot.Payload,
                    },
                };

                if (candidate.DurationBinding.HasValue)
                {
                    CombatDurationBinding binding = candidate.DurationBinding.Value;
                    json["durationBinding"] = new JObject
                    {
                        ["encounterId"] = binding.EncounterId.ToString(),
                        ["sourceCombatantId"] = binding.SourceCombatantId.HasValue ? binding.SourceCombatantId.Value.ToString() : null,
                        ["targetCombatantId"] = binding.TargetCombatantId.HasValue ? binding.TargetCombatantId.Value.ToString() : null,
                        ["appliedRoundOrdinal"] = binding.AppliedRoundOrdinal,
                        ["appliedLifecycleEventId"] = binding.AppliedLifecycleEventId,
                        ["requiredCount"] = binding.RequiredCount,
                    };
                }

                array.Add(json);
            }

            return array.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static IReadOnlyList<AttackEffectCandidate> DeserializeEffectCandidates(string json)
        {
            var array = JArray.Parse(json);
            var result = new List<AttackEffectCandidate>(array.Count);
            foreach (JToken token in array)
            {
                var obj = (JObject)token;
                CharacterId targetId = CharacterId.Parse((string)obj["targetId"]!);
                ContentDefinitionRef effectRef = ContentDefinitionRef.TryParse((string)obj["effectRef"]!, out ContentDefinitionRef parsedEffectRef) ? parsedEffectRef : throw new FormatException("EffectRef is not canonical.");
                ContentDefinitionRef mechanicsRef = ContentDefinitionRef.TryParse((string)obj["mechanicsRef"]!, out ContentDefinitionRef parsedMechanicsRef) ? parsedMechanicsRef : throw new FormatException("MechanicsRef is not canonical.");
                var decision = Enum.Parse<EffectApplicationDecision>((string)obj["decision"]!);
                var reasonCategory = Enum.Parse<EffectApplicationReasonCategory>((string)obj["reasonCategory"]!);
                string hostOnlyReasonDetail = (string)obj["hostOnlyReasonDetail"]!;
                var stackPolicy = Enum.Parse<EffectStackPolicy>((string)obj["stackPolicy"]!);

                JObject mechanicsSnapshotJson = (JObject)obj["mechanicsSnapshot"]!;
                ContentDefinitionRef snapshotSourceRef = ContentDefinitionRef.TryParse((string)mechanicsSnapshotJson["sourceDefinitionRef"]!, out ContentDefinitionRef parsedSnapshotRef) ? parsedSnapshotRef : throw new FormatException("MechanicsSnapshot.SourceDefinitionRef is not canonical.");
                long snapshotVersion = (long)mechanicsSnapshotJson["definitionSnapshotVersion"]!;
                var contentType = Enum.Parse<ContentDefinitionType>((string)mechanicsSnapshotJson["contentType"]!);
                string payload = (string)mechanicsSnapshotJson["payload"]!;
                var mechanicsSnapshot = new EffectMechanicsSnapshot(snapshotSourceRef, snapshotVersion, contentType, payload);

                CombatDurationBinding? durationBinding = null;
                if (obj["durationBinding"] is JObject bindingJson)
                {
                    CombatEncounterId encounterId = CombatEncounterId.Parse((string)bindingJson["encounterId"]!);
                    CharacterId? sourceCombatantId = bindingJson["sourceCombatantId"] != null && bindingJson["sourceCombatantId"]!.Type != JTokenType.Null ? CharacterId.Parse((string)bindingJson["sourceCombatantId"]!) : (CharacterId?)null;
                    CharacterId? targetCombatantId = bindingJson["targetCombatantId"] != null && bindingJson["targetCombatantId"]!.Type != JTokenType.Null ? CharacterId.Parse((string)bindingJson["targetCombatantId"]!) : (CharacterId?)null;
                    long appliedRoundOrdinal = (long)bindingJson["appliedRoundOrdinal"]!;
                    long appliedLifecycleEventId = (long)bindingJson["appliedLifecycleEventId"]!;
                    int requiredCount = (int)bindingJson["requiredCount"]!;
                    durationBinding = new CombatDurationBinding(encounterId, sourceCombatantId, targetCombatantId, appliedRoundOrdinal, appliedLifecycleEventId, requiredCount);
                }

                result.Add(new AttackEffectCandidate(targetId, effectRef, mechanicsRef, decision, reasonCategory, hostOnlyReasonDetail, durationBinding, stackPolicy, mechanicsSnapshot));
            }

            return result;
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

        private const string AttackOutcomeSelectColumns = "SELECT CommandId, CampaignId, EncounterId, ActorId, TargetIds, ActionItemInstanceId, ExpectedEncounterRevision, RandomSampleValue, InterventionRequired, EffectCandidatesJson, DamageDeltasJson, CostDeltasJson, OutcomeKind, GameLogEntryId, CreatedAt, ResolvedAt, ResolvedByCommandId";

        private static AttackOutcomeRecord? ReadByCommandId(SqliteConnection connection, SqliteTransaction? transaction, CommandId commandId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = AttackOutcomeSelectColumns + " FROM AttackOutcome WHERE CommandId = $commandId LIMIT 1;";
            select.Parameters.AddWithValue("$commandId", commandId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            return reader.Read() ? ReadRecord(reader) : null;
        }

        private static AttackOutcomeRecord? ReadByResolvedCommandId(SqliteConnection connection, SqliteTransaction? transaction, CommandId resolvedByCommandId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = AttackOutcomeSelectColumns + " FROM AttackOutcome WHERE ResolvedByCommandId = $resolvedBy LIMIT 1;";
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
            IReadOnlyList<AttackEffectCandidate> effectCandidates = DeserializeEffectCandidates(reader.GetString(9));
            IReadOnlyList<AttackDelta> damageDeltas = DeserializeDeltas(reader.GetString(10));
            IReadOnlyList<AttackDelta> costDeltas = DeserializeDeltas(reader.GetString(11));
            AttackOutcomeKind outcomeKind = (AttackOutcomeKind)Enum.Parse(typeof(AttackOutcomeKind), reader.GetString(12));
            string? gameLogEntryId = reader.IsDBNull(13) ? null : reader.GetString(13);
            UtcInstant createdAt = UtcInstant.Parse(reader.GetString(14));
            UtcInstant? resolvedAt = reader.IsDBNull(15) ? (UtcInstant?)null : UtcInstant.Parse(reader.GetString(15));
            CommandId? resolvedByCommandId = reader.IsDBNull(16) ? (CommandId?)null : CommandId.Parse(reader.GetString(16));
            return new AttackOutcomeRecord(commandId, campaignId, encounterId, actorId, targetIds, itemInstanceId, expectedRevision, randomSampleValue, interventionRequired, effectCandidates, damageDeltas, costDeltas, outcomeKind, gameLogEntryId, createdAt, resolvedAt, resolvedByCommandId);
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
    EffectCandidatesJson TEXT NOT NULL,
    DamageDeltasJson TEXT NOT NULL DEFAULT '[]',
    CostDeltasJson TEXT NOT NULL DEFAULT '[]',
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
);
CREATE TABLE IF NOT EXISTS CombatStackConflict (
    CommandId TEXT NOT NULL,
    ConflictingActiveEffectId TEXT NOT NULL,
    ActiveEffectId TEXT NOT NULL,
    CampaignId TEXT NOT NULL,
    EffectDefinitionRef TEXT NOT NULL,
    MechanicsSourceDefinitionRef TEXT NOT NULL,
    MechanicsDefinitionSnapshotVersion INTEGER NOT NULL,
    MechanicsContentType TEXT NOT NULL,
    MechanicsPayload TEXT NOT NULL,
    SourceKind TEXT NOT NULL,
    SourceItemRefKind TEXT,
    SourceItemRefId TEXT,
    TargetKind TEXT NOT NULL,
    TargetCharacterId TEXT,
    TargetItemInstanceId TEXT,
    Status TEXT NOT NULL,
    StackCount INTEGER NOT NULL,
    AppliedByUserId TEXT NOT NULL,
    AppliedAt TEXT NOT NULL,
    ExpiresAt TEXT,
    Revision INTEGER NOT NULL,
    UpdatedAt TEXT NOT NULL,
    LastCommandId TEXT NOT NULL,
    CombatEncounterId TEXT,
    CombatSourceCombatantId TEXT,
    CombatTargetCombatantId TEXT,
    CombatAppliedRoundOrdinal INTEGER,
    CombatAppliedLifecycleEventId INTEGER,
    CombatRequiredCount INTEGER,
    RaisedAt TEXT NOT NULL,
    ConflictStatus TEXT NOT NULL,
    ResolvedAt TEXT,
    ResolvedByCommandId TEXT,
    Resolution TEXT,
    PRIMARY KEY (CommandId, ConflictingActiveEffectId)
);
CREATE INDEX IF NOT EXISTS IX_CombatStackConflict_CampaignId ON CombatStackConflict(CampaignId);";
            command.ExecuteNonQuery();
        }
    }
}
