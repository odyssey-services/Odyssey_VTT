using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using Odyssey.Application.Commands;
using Odyssey.Application.Effects;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Content;
using Odyssey.Domain.Effects;
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
        private readonly IActiveEffectRepository _effects;

        /// <summary>
        /// ODY-S06-106 doработка (second fix): takes <paramref name="effects"/> so
        /// <see cref="CompensateAbilityActivation"/> can genuinely remove already-created `ActiveEffect`
        /// rows via the existing, unmodified <see cref="IActiveEffectRepository.RemoveActiveEffect"/> --
        /// not an additive overload, since this class's own constructor has no other caller anywhere yet
        /// (this feature is still unmerged), so widening the required constructor signature directly is the
        /// minimal change.
        /// </summary>
        public SqliteActivateAbilityRepository(IWallClock clock, IActiveEffectRepository effects)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _effects = effects ?? throw new ArgumentNullException(nameof(effects));
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

                        var record = new AbilityActivationRecord(commandId, campaign.CampaignId, actorId, characterAbilityId, targetIds, resourceDeltas, effectsToApply, now, compensatedAt: null, compensationStartedAt: null);

                        using (var insert = connection.CreateCommand())
                        {
                            insert.Transaction = transaction;
                            insert.CommandText = "INSERT INTO AbilityActivation (CommandId, CampaignId, ActorId, CharacterAbilityId, TargetIdsJson, ResourceDeltasJson, AppliedEffectRefsJson, CreatedAt, CompensatedAt, CompensationStartedAt, CreatedEffectIdsJson) " +
                                                  "VALUES ($commandId, $campaignId, $actorId, $characterAbilityId, $targetIdsJson, $resourceDeltasJson, $appliedEffectRefsJson, $createdAt, NULL, NULL, NULL);";
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
        /// ODY-S06-106 doработка (second fix, after independent verification found the first compensation
        /// fix still left already-created `ActiveEffect` rows behind on a later-effect/later-target
        /// failure): removes every already-created `ActiveEffect` first (see below), THEN reverses
        /// <paramref name="originalCommandId"/>'s own already durable
        /// <see cref="AbilityActivationRecord.ResourceDeltas"/> by re-applying each one negated, atomically,
        /// in one NEW SQLite transaction, then marks the original row's own `CompensatedAt` column.
        /// Idempotent by construction: a second call reads `CompensatedAt` already non-null and returns the
        /// already-compensated record as a no-op success -- never a double reversal, never a second removal
        /// attempt.
        ///
        /// ODY-S06-106 doработка (THIRD fix, after independent verification found a retry after an
        /// INCOMPLETE compensation attempt -- this method itself failing partway through, e.g. one effect
        /// removal succeeding and the next one failing -- could be silently reported as `Success` by
        /// `ActivateAbilityService`'s own top-of-method idempotency check, since `CompensatedAt == null`
        /// alone cannot tell "never attempted" apart from "attempted, didn't finish"): the FIRST time this
        /// method runs for a given <paramref name="originalCommandId"/>, it durably persists
        /// <paramref name="createdEffectIds"/> into a new `CreatedEffectIdsJson` column and marks a new
        /// `CompensationStartedAt` column, unconditionally, BEFORE attempting any removal (`EnsureCompensationStarted`,
        /// a plain, self-guarded `UPDATE ... WHERE CompensationStartedAt IS NULL` -- idempotent by its own
        /// `WHERE` clause, no separate `CommandId`-ledger entry needed since it never mutates game state, only
        /// this method's own bookkeeping row). A LATER retry of this same method reads that DURABLE list back
        /// (`ReadDurableCreatedEffectIds`) and finishes removing THAT list, ignoring whatever
        /// <paramref name="createdEffectIds"/> the resuming caller happens to pass (typically empty, since
        /// `ActivateAbilityService`'s own in-memory list from the original, now-abandoned attempt no longer
        /// exists by the time a retry runs) -- so a resumed compensation call genuinely finishes the SAME
        /// removal set the original attempt started, not a possibly-different, incomplete one.
        /// </summary>
        public Result<AbilityActivationRecord> CompensateAbilityActivation(CampaignHandle campaign, CommandId originalCommandId, IReadOnlyList<ActiveEffectId> createdEffectIds, UserId actorUserId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!originalCommandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(originalCommandId));
            if (createdEffectIds == null) throw new ArgumentNullException(nameof(createdEffectIds));

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

                // ODY-S06-106 doработка (third fix): mark CompensationStartedAt and persist the effect-id
                // list durably on the FIRST call only (the WHERE clause makes this a no-op on a retry); then
                // always read the DURABLE list back, so a resumed call finishes the SAME set the original
                // attempt started, never a possibly-different one built from a resuming caller's own
                // (typically empty) in-memory list.
                EnsureCompensationStarted(connection, originalCommandId, createdEffectIds, _clock.GetUtcNow());
                IReadOnlyList<ActiveEffectId> effectIdsToRemove = ReadDurableCreatedEffectIds(connection, originalCommandId);

                // ODY-S06-106 doработка (second fix, after independent verification found the first
                // compensation fix still left already-created ActiveEffect rows behind on a later
                // effect/target failure): every already-created ActiveEffect for THIS activation attempt is
                // removed BEFORE any resource reversal is even attempted, via the existing, unmodified
                // IActiveEffectRepository.RemoveActiveEffect. actorIsMainGm: true regardless of the original
                // activation actor's own permission level -- an internal system rollback of this same
                // failed attempt, not a new capability granted to them (mirrors ApplyCharacterResourceDelta's
                // own established precedent of internal writes bypassing the public command's own
                // authorization). Each removal uses its own deterministic sub-CommandId, distinct from the
                // creation-time one, so RemoveActiveEffect's own idempotency ledger entry never collides
                // with CreateActiveEffect's own (a collision would make SqliteSavingPipeline treat the
                // removal as a replay of the CREATE command and silently no-op). If any single removal
                // fails, return immediately -- resource reversal/CompensatedAt are untouched, retryable
                // later since every removal attempted so far is itself already idempotent, and
                // CompensationStartedAt already durably marks this as "in progress, not a fresh success."
                foreach (ActiveEffectId effectId in effectIdsToRemove)
                {
                    CommandId removalCommandId = StableEffectRemovalCommandId(originalCommandId, effectId);
                    Result<ActiveEffectRecord> removed = _effects.RemoveActiveEffect(campaign, campaign.CampaignId, effectId, actorUserId, actorIsMainGm: true, expectedRevision: 1, removalCommandId, correlationId);
                    if (removed.IsFailure)
                    {
                        return Result<AbilityActivationRecord>.Failure(removed.Error);
                    }
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

                        var compensated = new AbilityActivationRecord(existing.CommandId, existing.CampaignId, existing.ActorId, existing.CharacterAbilityId, existing.TargetIds, existing.ResourceDeltas, existing.AppliedEffectRefs, existing.OccurredAt, compensatedAt: now, compensationStartedAt: existing.CompensationStartedAt ?? now);
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

        /// <summary>
        /// ODY-S06-106 doработка (second fix): a deterministic `CommandId` for removing one specific
        /// already-created `ActiveEffect`, derived from its own `ActiveEffectId` -- deliberately a DIFFERENT
        /// hash input (a distinct prefix) than `ActivateAbilityService.StableSubCommandId`'s own creation-time
        /// derivation, so `RemoveActiveEffect`'s own idempotency-ledger entry can never collide with
        /// `CreateActiveEffect`'s own entry for the same effect (a collision would make `SqliteSavingPipeline`
        /// treat the removal call as a replay of the CREATE command and silently no-op instead of actually
        /// removing anything).
        /// </summary>
        private static CommandId StableEffectRemovalCommandId(CommandId originalCommandId, ActiveEffectId activeEffectId)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes("ody-s06-106/compensate-effect/v1/" + originalCommandId + "/" + activeEffectId));
            var text = new StringBuilder(32);
            for (int i = 0; i < 16; i++) text.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return CommandId.Parse("cmd_" + text);
        }

        /// <summary>
        /// ODY-S06-106 doработка (third fix): durably marks `CompensationStartedAt`/`CreatedEffectIdsJson`
        /// the FIRST time compensation is attempted for a given `CommandId` -- the `WHERE CompensationStartedAt
        /// IS NULL` guard makes this a safe no-op on every subsequent (retry) call, so it never overwrites an
        /// already-durable list with a possibly-different one a resuming caller happens to pass. Deliberately
        /// NOT routed through `SqliteSavingPipeline`/`AppliedCommands` -- this is bookkeeping for THIS
        /// method's own resumability, not a game-state mutation needing its own idempotency-ledger entry.
        /// </summary>
        private static void EnsureCompensationStarted(SqliteConnection connection, CommandId originalCommandId, IReadOnlyList<ActiveEffectId> createdEffectIds, UtcInstant now)
        {
            using var update = connection.CreateCommand();
            update.CommandText = "UPDATE AbilityActivation SET CompensationStartedAt = $startedAt, CreatedEffectIdsJson = $effectIdsJson WHERE CommandId = $commandId AND CompensationStartedAt IS NULL;";
            update.Parameters.AddWithValue("$startedAt", now.ToString());
            update.Parameters.AddWithValue("$effectIdsJson", SerializeActiveEffectIds(createdEffectIds));
            update.Parameters.AddWithValue("$commandId", originalCommandId.ToString());
            update.ExecuteNonQuery();
        }

        /// <summary>ODY-S06-106 doработка (third fix): reads the durable `CreatedEffectIdsJson` column back -- the authoritative list a resumed `CompensateAbilityActivation` call finishes removing, regardless of what its own caller passed in.</summary>
        private static IReadOnlyList<ActiveEffectId> ReadDurableCreatedEffectIds(SqliteConnection connection, CommandId originalCommandId)
        {
            using var select = connection.CreateCommand();
            select.CommandText = "SELECT CreatedEffectIdsJson FROM AbilityActivation WHERE CommandId = $commandId LIMIT 1;";
            select.Parameters.AddWithValue("$commandId", originalCommandId.ToString());
            object? result = select.ExecuteScalar();
            return result == null || result == DBNull.Value ? Array.Empty<ActiveEffectId>() : DeserializeActiveEffectIds((string)result);
        }

        private static string SerializeActiveEffectIds(IReadOnlyList<ActiveEffectId> ids)
        {
            var array = new JArray();
            foreach (ActiveEffectId id in ids) array.Add(id.ToString());
            return array.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static IReadOnlyList<ActiveEffectId> DeserializeActiveEffectIds(string json)
        {
            var array = JArray.Parse(json);
            var result = new List<ActiveEffectId>(array.Count);
            foreach (JToken token in array) result.Add(ActiveEffectId.Parse((string)token!));
            return result;
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
            select.CommandText = "SELECT CommandId, CampaignId, ActorId, CharacterAbilityId, TargetIdsJson, ResourceDeltasJson, AppliedEffectRefsJson, CreatedAt, CompensatedAt, CompensationStartedAt FROM AbilityActivation WHERE CommandId = $commandId LIMIT 1;";
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
                reader.IsDBNull(8) ? (UtcInstant?)null : UtcInstant.Parse(reader.GetString(8)),
                reader.IsDBNull(9) ? (UtcInstant?)null : UtcInstant.Parse(reader.GetString(9)));
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
    CompensatedAt TEXT,
    CompensationStartedAt TEXT,
    CreatedEffectIdsJson TEXT
);
CREATE INDEX IF NOT EXISTS IX_AbilityActivation_CampaignId ON AbilityActivation(CampaignId);";
            command.ExecuteNonQuery();
        }
    }
}
