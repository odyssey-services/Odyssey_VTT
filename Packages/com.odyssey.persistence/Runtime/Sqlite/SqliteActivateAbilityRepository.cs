using System;
using System.Collections.Generic;
using System.IO;
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
    ///
    /// `ApplyEffect` primitives are NOT applied by this class -- see `ActivateAbilityService`'s own doc
    /// comment for why `IActiveEffectRepository.CreateActiveEffect` cannot join this transaction, and this
    /// task's own contract §18 for the disclosed cross-repository atomicity limit this implies.
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

        public Result<AbilityActivationRecord> RecordAbilityActivation(CampaignHandle campaign, CharacterId actorId, CharacterAbilityId characterAbilityId, IReadOnlyList<CharacterId> targetIds, IReadOnlyList<AttackDelta> resourceDeltas, IReadOnlyList<ContentDefinitionRef> effectsToApply, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

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
                        for (int index = 0; index < resourceDeltas.Count; index++)
                        {
                            Result deltaResult = SqliteAttackApplyRepository.ApplyAttackDelta(connection, transaction, resourceDeltas[index], commandId, now, correlationId);
                            if (deltaResult.IsFailure)
                            {
                                return Result<PipelineWrite<AbilityActivationRecord>>.Failure(deltaResult.Error);
                            }
                        }

                        var record = new AbilityActivationRecord(commandId, campaign.CampaignId, actorId, characterAbilityId, targetIds, resourceDeltas, effectsToApply, now);

                        using (var insert = connection.CreateCommand())
                        {
                            insert.Transaction = transaction;
                            insert.CommandText = "INSERT INTO AbilityActivation (CommandId, CampaignId, ActorId, CharacterAbilityId, TargetIdsJson, ResourceDeltasJson, AppliedEffectRefsJson, CreatedAt) " +
                                                  "VALUES ($commandId, $campaignId, $actorId, $characterAbilityId, $targetIdsJson, $resourceDeltasJson, $appliedEffectRefsJson, $createdAt);";
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

        private static AbilityActivationRecord? ReadByCommandId(SqliteConnection connection, SqliteTransaction? transaction, CommandId commandId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT CommandId, CampaignId, ActorId, CharacterAbilityId, TargetIdsJson, ResourceDeltasJson, AppliedEffectRefsJson, CreatedAt FROM AbilityActivation WHERE CommandId = $commandId LIMIT 1;";
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
                UtcInstant.Parse(reader.GetString(7)));
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
    CreatedAt TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_AbilityActivation_CampaignId ON AbilityActivation(CampaignId);";
            command.ExecuteNonQuery();
        }
    }
}
