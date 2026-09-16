using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Persistence.Sqlite
{
    /// <summary>
    /// ODY-S06-106: the sole SQLite implementation of <see cref="IActivateAbilityRepository"/>. Owns a new,
    /// standalone <c>AbilityActivation</c> table -- it never appends a method or column to
    /// <see cref="ICharacterRepository"/>, mirroring <see cref="SqliteAttackApplyRepository"/>'s own
    /// standalone-table idiom for the identical reason (a root-command apply pipeline, not general
    /// Character CRUD). Every resource-delta write commits through the shared <see cref="SqliteSavingPipeline"/>
    /// (`ADR-012` §5): the durable <c>AbilityActivation</c> row, the DomainEvent, and the AppliedCommands
    /// idempotency row land in one SQLite transaction, or none of them do.
    ///
    /// Reuses <see cref="SqliteAttackApplyRepository.ApplyAttackDelta"/> verbatim (made `internal` by this
    /// task, logic unchanged) for every resource delta -- the ability's own cost, and every `AdjustResource`
    /// primitive's own resolved amount -- so insufficient balance for ANY delta rolls back the WHOLE
    /// transaction (the same `character:{characterId}:{resourceKind}` `TargetRef` convention, the same
    /// `AttackOutcomeDeltaValueOutOfRange` clamping/failure semantics, `ODY-S05-609`, unmodified).
    /// <see cref="RecordAbilityActivation"/>'s own transaction also re-checks the caller-declared expected
    /// `CharacterAbilities`/`CharacterResources` section revisions fresh, inside the same transaction
    /// (`ADR-022` §5 rule 2, ODY-S06-106 doработка) -- a stale value is rejected before any delta is applied.
    ///
    /// `ApplyEffect` primitives are NOT applied by this class -- see <see cref="ActivateAbilityService"/>'s
    /// own doc comment for why `IActiveEffectRepository.CreateActiveEffect` cannot join
    /// <see cref="RecordAbilityActivation"/>'s own transaction. ODY-S06-106 doработка (product-owner-ordered
    /// fix, after independent verification rejected leaving this an unfixed "accepted design decision"):
    /// <see cref="CompensateAbilityActivation"/> genuinely reverses that transaction's own already-committed
    /// deltas when an `ApplyEffect` application fails afterward -- the atomicity gap is closed by a real
    /// saga/compensation mechanism, not merely disclosed in prose.
    /// </summary>
    public sealed class SqliteActivateAbilityRepository : IActivateAbilityRepository
    {
        private readonly IWallClock _clock;
        private readonly SqliteSavingPipeline _pipeline;

        public SqliteActivateAbilityRepository(IWallClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _pipeline = new SqliteSavingPipeline(clock);
        }

        public Result<AbilityActivationRecord> GetActivation(CampaignHandle campaign, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureAbilityActivationTable(connection);
                AbilityActivationRecord? found = ReadByCommandId(connection, null, commandId);
                return found == null
                    ? Result<AbilityActivationRecord>.Failure(PersistenceFailures.AttackOutcomeNotFound(correlationId))
                    : Result<AbilityActivationRecord>.Success(found);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<AbilityActivationRecord>.Failure(PersistenceFailures.AttackOutcomeIoFailed(correlationId));
            }
        }

        public Result<AbilityActivationRecord> RecordAbilityActivation(CampaignHandle campaign, CharacterId actorId, CharacterAbilityId characterAbilityId, IReadOnlyList<CharacterId> targetIds, IReadOnlyList<AttackDelta> resourceDeltas, IReadOnlyList<ContentDefinitionRef> effectsToApply, long expectedCharacterAbilitiesRevision, long expectedCharacterResourcesRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (expectedCharacterAbilitiesRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedCharacterAbilitiesRevision));
            if (expectedCharacterResourcesRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedCharacterResourcesRevision));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureAbilityActivationTable(connection);
                UtcInstant now = _clock.GetUtcNow();

                return _pipeline.Execute(
                    connection,
                    campaign.CampaignId,
                    commandId,
                    correlationId,
                    tryReplay: transaction =>
                    {
                        AbilityActivationRecord? replayed = ReadByCommandId(connection, transaction, commandId);
                        return replayed == null
                            ? Result<AbilityActivationRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId))
                            : Result<AbilityActivationRecord>.Success(replayed);
                    },
                    apply: transaction =>
                    {
                        // ADR-022 section 5 rule 2, ODY-S06-106 doработка: re-read both declared section
                        // revisions FRESH, inside this same transaction, immediately before applying
                        // anything -- a stale caller-declared value (the character's own CharacterAbilities
                        // or CharacterResources section changed between ActivateAbilityService's own read
                        // step and this commit) rejects the whole command, exactly like
                        // AcquireAbilityViaProgressionPurchase's own established pattern.
                        Result<(long AbilitiesRevision, long ResourcesRevision)> currentRevisions = ReadCharacterSectionRevisions(connection, transaction, actorId, correlationId);
                        if (currentRevisions.IsFailure)
                        {
                            return Result<PipelineWrite<AbilityActivationRecord>>.Failure(currentRevisions.Error);
                        }

                        if (currentRevisions.Value.AbilitiesRevision != expectedCharacterAbilitiesRevision || currentRevisions.Value.ResourcesRevision != expectedCharacterResourcesRevision)
                        {
                            return Result<PipelineWrite<AbilityActivationRecord>>.Failure(PersistenceFailures.CharacterRevisionConflict(correlationId));
                        }

                        for (int index = 0; index < resourceDeltas.Count; index++)
                        {
                            Result deltaResult = SqliteAttackApplyRepository.ApplyAttackDelta(connection, transaction, resourceDeltas[index], commandId, now, correlationId);
                            if (deltaResult.IsFailure)
                            {
                                return Result<PipelineWrite<AbilityActivationRecord>>.Failure(deltaResult.Error);
                            }
                        }

                        var record = new AbilityActivationRecord(commandId, campaign.CampaignId, actorId, characterAbilityId, targetIds, resourceDeltas, effectsToApply, now, compensatedAt: null);

                        using (var insert = connection.CreateCommand())
                        {
                            insert.Transaction = transaction;
                            insert.CommandText = "INSERT INTO AbilityActivation (CommandId, CampaignId, ActorId, CharacterAbilityId, TargetIdsJson, ResourceDeltasJson, AppliedEffectRefsJson, CreatedAt, CompensatedAt) " +
                                                  "VALUES ($commandId, $campaignId, $actorId, $characterAbilityId, $targetIdsJson, $resourceDeltasJson, $appliedEffectRefsJson, $createdAt, NULL);";
                            insert.Parameters.AddWithValue("$commandId", commandId.ToString());
                            insert.Parameters.AddWithValue("$campaignId", campaign.CampaignId.ToString());
                            insert.Parameters.AddWithValue("$actorId", actorId.ToString());
                            insert.Parameters.AddWithValue("$characterAbilityId", characterAbilityId.ToString());
                            insert.Parameters.AddWithValue("$targetIdsJson", SerializeCharacterIds(targetIds));
                            insert.Parameters.AddWithValue("$resourceDeltasJson", SerializeDeltas(resourceDeltas));
                            insert.Parameters.AddWithValue("$appliedEffectRefsJson", SerializeEffectRefs(effectsToApply));
                            insert.Parameters.AddWithValue("$createdAt", now.ToString());
                            insert.ExecuteNonQuery();
                        }

                        string payloadJson = "{\"commandId\":\"" + commandId + "\",\"actorId\":\"" + actorId + "\",\"characterAbilityId\":\"" + characterAbilityId + "\"}";
                        return Result<PipelineWrite<AbilityActivationRecord>>.Success(new PipelineWrite<AbilityActivationRecord>(
                            record, "odyssey.persistence.ability_activated", payloadJson, commandId.ToString(),
                            aggregateType: "ability_activation", aggregateId: commandId.ToString(), aggregateRevision: 1));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<AbilityActivationRecord>.Failure(PersistenceFailures.AttackOutcomeIoFailed(correlationId));
            }
        }

        /// <summary>
        /// ODY-S06-106 doработка (product-owner-ordered fix for the disclosed `ApplyEffect`
        /// cross-repository-atomicity gap): reverses <paramref name="originalCommandId"/>'s own already
        /// durable <see cref="AbilityActivationRecord.ResourceDeltas"/> by re-applying each one negated,
        /// atomically, in one NEW SQLite transaction, then marks the original row's own `CompensatedAt`
        /// column. Idempotent by construction: a second call reads `CompensatedAt` already non-null and
        /// returns the already-compensated record as a no-op success -- never a double reversal.
        /// </summary>
        public Result<AbilityActivationRecord> CompensateAbilityActivation(CampaignHandle campaign, CommandId originalCommandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!originalCommandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(originalCommandId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureAbilityActivationTable(connection);

                AbilityActivationRecord? existing = ReadByCommandId(connection, null, originalCommandId);
                if (existing == null)
                {
                    return Result<AbilityActivationRecord>.Failure(PersistenceFailures.AttackOutcomeNotFound(correlationId));
                }

                if (existing.CompensatedAt != null)
                {
                    return Result<AbilityActivationRecord>.Success(existing);
                }

                CommandId compensationCommandId = StableCompensationCommandId(originalCommandId);
                UtcInstant now = _clock.GetUtcNow();

                return _pipeline.Execute(
                    connection,
                    campaign.CampaignId,
                    compensationCommandId,
                    correlationId,
                    tryReplay: transaction =>
                    {
                        AbilityActivationRecord? replayed = ReadByCommandId(connection, transaction, originalCommandId);
                        return replayed != null && replayed.CompensatedAt != null
                            ? Result<AbilityActivationRecord>.Success(replayed)
                            : Result<AbilityActivationRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId));
                    },
                    apply: transaction =>
                    {
                        foreach (AttackDelta original in existing.ResourceDeltas)
                        {
                            var reversed = new AttackDelta(original.TargetRef, -original.Value);
                            Result deltaResult = SqliteAttackApplyRepository.ApplyAttackDelta(connection, transaction, reversed, compensationCommandId, now, correlationId);
                            if (deltaResult.IsFailure)
                            {
                                return Result<PipelineWrite<AbilityActivationRecord>>.Failure(deltaResult.Error);
                            }
                        }

                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText = "UPDATE AbilityActivation SET CompensatedAt = $compensatedAt WHERE CommandId = $commandId;";
                            update.Parameters.AddWithValue("$compensatedAt", now.ToString());
                            update.Parameters.AddWithValue("$commandId", originalCommandId.ToString());
                            update.ExecuteNonQuery();
                        }

                        var compensated = new AbilityActivationRecord(existing.CommandId, existing.CampaignId, existing.ActorId, existing.CharacterAbilityId, existing.TargetIds, existing.ResourceDeltas, existing.AppliedEffectRefs, existing.OccurredAt, compensatedAt: now);
                        string payloadJson = "{\"originalCommandId\":\"" + originalCommandId + "\"}";
                        return Result<PipelineWrite<AbilityActivationRecord>>.Success(new PipelineWrite<AbilityActivationRecord>(
                            compensated, "odyssey.persistence.ability_activation_compensated", payloadJson, compensationCommandId.ToString(),
                            aggregateType: "ability_activation", aggregateId: originalCommandId.ToString(), aggregateRevision: 2));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<AbilityActivationRecord>.Failure(PersistenceFailures.AttackOutcomeIoFailed(correlationId));
            }
        }

        /// <summary>Mirrors `ActivateAbilityService.StableSubCommandId`'s own SHA256-derivation pattern -- a deterministic compensation `CommandId`, always the same for a given original `CommandId`, so `CompensateAbilityActivation`'s own idempotency-ledger replay behaves correctly on retry.</summary>
        private static CommandId StableCompensationCommandId(CommandId originalCommandId)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes("ody-s06-106/compensate/v1/" + originalCommandId));
            var text = new StringBuilder(32);
            for (int i = 0; i < 16; i++) text.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return CommandId.Parse("cmd_" + text);
        }

        /// <summary>ADR-022 section 5 rule 2's own fresh-read requirement -- reads straight from the `Character` row, not from any already-loaded `CharacterRecord`, so a concurrent mutation between `ActivateAbilityService`'s own read step and this commit is genuinely caught.</summary>
        private static Result<(long AbilitiesRevision, long ResourcesRevision)> ReadCharacterSectionRevisions(SqliteConnection connection, SqliteTransaction transaction, CharacterId characterId, CorrelationId correlationId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT CharacterAbilitiesRevision, CharacterResourcesRevision FROM Character WHERE CharacterId = $characterId;";
            select.Parameters.AddWithValue("$characterId", characterId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read())
            {
                return Result<(long, long)>.Failure(PersistenceFailures.CharacterNotFound(correlationId));
            }

            return Result<(long, long)>.Success((reader.GetInt64(0), reader.GetInt64(1)));
        }

        private static AbilityActivationRecord? ReadByCommandId(SqliteConnection connection, SqliteTransaction? transaction, CommandId commandId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT CommandId, CampaignId, ActorId, CharacterAbilityId, TargetIdsJson, ResourceDeltasJson, AppliedEffectRefsJson, CreatedAt, CompensatedAt FROM AbilityActivation WHERE CommandId = $commandId LIMIT 1;";
            select.Parameters.AddWithValue("$commandId", commandId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read()) return null;

            return new AbilityActivationRecord(
                CommandId.Parse(reader.GetString(0)),
                CampaignId.Parse(reader.GetString(1)),
                CharacterId.Parse(reader.GetString(2)),
                CharacterAbilityId.Parse(reader.GetString(3)),
                DeserializeCharacterIds(reader.GetString(4)),
                DeserializeDeltas(reader.GetString(5)),
                DeserializeEffectRefs(reader.GetString(6)),
                UtcInstant.Parse(reader.GetString(7)),
                reader.IsDBNull(8) ? (UtcInstant?)null : UtcInstant.Parse(reader.GetString(8)));
        }

        private static string SerializeCharacterIds(IReadOnlyList<CharacterId> ids)
        {
            var array = new JArray();
            foreach (CharacterId id in ids) array.Add(id.ToString());
            return array.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static IReadOnlyList<CharacterId> DeserializeCharacterIds(string json)
        {
            var array = JArray.Parse(json);
            var result = new List<CharacterId>(array.Count);
            foreach (JToken token in array) result.Add(CharacterId.Parse((string)token!));
            return result;
        }

        private static string SerializeDeltas(IReadOnlyList<AttackDelta> deltas)
        {
            var array = new JArray();
            foreach (AttackDelta delta in deltas) array.Add(new JObject { ["targetRef"] = delta.TargetRef, ["value"] = delta.Value });
            return array.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static IReadOnlyList<AttackDelta> DeserializeDeltas(string json)
        {
            var array = JArray.Parse(json);
            var result = new List<AttackDelta>(array.Count);
            foreach (JToken token in array) result.Add(new AttackDelta((string)token["targetRef"]!, (int)token["value"]!));
            return result;
        }

        private static string SerializeEffectRefs(IReadOnlyList<ContentDefinitionRef> refs)
        {
            var array = new JArray();
            foreach (ContentDefinitionRef reference in refs) array.Add(reference.ToString());
            return array.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static IReadOnlyList<ContentDefinitionRef> DeserializeEffectRefs(string json)
        {
            var array = JArray.Parse(json);
            var result = new List<ContentDefinitionRef>(array.Count);
            foreach (JToken token in array) result.Add(ContentDefinitionRef.Parse((string)token!));
            return result;
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

        private static void EnsureAbilityActivationTable(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS AbilityActivation (
    CommandId TEXT PRIMARY KEY,
    CampaignId TEXT NOT NULL,
    ActorId TEXT NOT NULL,
    CharacterAbilityId TEXT NOT NULL,
    TargetIdsJson TEXT NOT NULL,
    ResourceDeltasJson TEXT NOT NULL,
    AppliedEffectRefsJson TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    CompensatedAt TEXT
);
CREATE INDEX IF NOT EXISTS IX_AbilityActivation_CampaignId ON AbilityActivation(CampaignId);";
            command.ExecuteNonQuery();
        }
    }
}
