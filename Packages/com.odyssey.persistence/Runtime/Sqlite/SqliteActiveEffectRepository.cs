using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Odyssey.Application.Commands;
using Odyssey.Application.Content;
using Odyssey.Application.Effects;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Content;
using Odyssey.Domain.Character;
using Odyssey.Domain.Effects;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;

namespace Odyssey.Persistence.Sqlite
{
    /// <summary>
    /// ODY-S05-502: `ADR-028` §6's own standalone `IActiveEffectRepository`
    /// implementation -- not an extension of `SqliteInventoryRepository` or
    /// `SqliteCharacterRepository`. Structural precedent is
    /// `SqliteSceneRepository` (a self-owned aggregate, not nested under
    /// another one, with its own SQLite table(s) and its own
    /// <see cref="SqliteSavingPipeline"/> usage) -- not `SqliteInventoryRepository`,
    /// whose own tables/idempotency ledgers exist for Inventory-owned state
    /// this aggregate deliberately is not (`ADR-028` §8.2 rule 2).
    ///
    /// `ODY-S05-502` implemented creation and basic persistence only.
    /// `ODY-S05-504` added <see cref="ExpireActiveEffect"/> (`Status → Expired`);
    /// `ODY-S05-505` added <see cref="SetItemEffectEquipped"/> (`Status ↔
    /// Suspended`/`Active` for `WhileItemEquipped`); `ODY-S05-506` adds
    /// <see cref="RemoveActiveEffect"/> (`Status → Removed`, MainGM-only,
    /// `ADR-028` §10 rule 3) -- together the four exhaust `ADR-028` §6 rule
    /// 2's own minimum contract ("transition `Status` (expire/suspend/resume/
    /// remove)"). No stacking-policy mutation exists on this class
    /// (`ODY-S05-503`, implemented separately as a pure decision layer with
    /// no repository change). Direct (non-item) creation (`ADR-028` §10 rule
    /// 2, also `ODY-S05-506`'s own territory) needs no change here at all --
    /// it is a MainGM-gated Application-layer wrapper
    /// (`ActiveEffectDirectCommandService`) over <see cref="CreateActiveEffect"/>,
    /// unmodified.
    /// </summary>
    public sealed class SqliteActiveEffectRepository : IActiveEffectRepository
    {
        private readonly IWallClock _clock;
        private readonly SqliteSavingPipeline _pipeline;

        public SqliteActiveEffectRepository(IWallClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _pipeline = new SqliteSavingPipeline(clock);
        }

        public Result<ActiveEffectRecord> CreateActiveEffect(CampaignHandle campaign, ActiveEffectRecord record, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (!TryValidateCampaignBoundary(campaign, record.CampaignId, correlationId, out Error campaignError))
            {
                return Result<ActiveEffectRecord>.Failure(campaignError);
            }

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureActiveEffectTables(connection);
                ActiveEffect effect = record.Effect;

                return _pipeline.Execute(
                    connection,
                    campaign.CampaignId,
                    commandId,
                    correlationId,
                    tryReplay: transaction => ReplayByCommandId(connection, transaction, campaign.CampaignId, commandId, correlationId),
                    apply: transaction =>
                    {
                        UtcInstant now = _clock.GetUtcNow();
                        using (var insert = connection.CreateCommand())
                        {
                            insert.Transaction = transaction;
                            insert.CommandText = InsertColumns + " VALUES (" +
                                "$activeEffectId, $campaignId, $effectDefinitionRef, " +
                                "$mechanicsSourceDefinitionRef, $mechanicsDefinitionSnapshotVersion, $mechanicsContentType, $mechanicsPayload, " +
                                "$sourceKind, $sourceItemRefKind, $sourceItemRefId, " +
                                "$targetKind, $targetCharacterId, $targetItemInstanceId, " +
                                "$status, $stackCount, $appliedByUserId, $appliedAt, $expiresAt, $revision, $updatedAt, $lastCommandId);";
                            AddParameters(insert, record, now);
                            insert.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            insert.ExecuteNonQuery();
                        }

                        string payloadJson = "{\"activeEffectId\":\"" + effect.ActiveEffectId + "\",\"effectDefinitionRef\":\"" + effect.EffectDefinitionRef + "\"}";
                        return Result<PipelineWrite<ActiveEffectRecord>>.Success(new PipelineWrite<ActiveEffectRecord>(
                            record, "odyssey.persistence.active_effect_applied", payloadJson, effect.ActiveEffectId.ToString(),
                            aggregateType: "active_effect", aggregateId: effect.ActiveEffectId.ToString(), aggregateRevision: effect.Revision));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<ActiveEffectRecord>.Failure(PersistenceFailures.ActiveEffectIoFailed(correlationId));
            }
        }

        public Result<ActiveEffectRecord> GetActiveEffect(CampaignHandle campaign, ActiveEffectId activeEffectId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!activeEffectId.IsValid) throw new ArgumentException("ActiveEffectId is required.", nameof(activeEffectId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureActiveEffectTables(connection);

                using var select = connection.CreateCommand();
                select.CommandText = SelectColumns + " FROM ActiveEffect WHERE ActiveEffectId = $activeEffectId LIMIT 1;";
                select.Parameters.AddWithValue("$activeEffectId", activeEffectId.ToString());
                using SqliteDataReader reader = select.ExecuteReader();
                if (!reader.Read())
                {
                    return Result<ActiveEffectRecord>.Failure(PersistenceFailures.ActiveEffectNotFound(correlationId));
                }

                ActiveEffectRecord record = ReadRecord(reader);
                if (!record.CampaignId.Equals(campaign.CampaignId))
                {
                    return Result<ActiveEffectRecord>.Failure(PersistenceFailures.ActiveEffectNotFound(correlationId));
                }

                return Result<ActiveEffectRecord>.Success(record);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<ActiveEffectRecord>.Failure(PersistenceFailures.ActiveEffectIoFailed(correlationId));
            }
        }

        public Result<IReadOnlyList<ActiveEffectRecord>> ListActiveEffectsByTarget(CampaignHandle campaign, CampaignId campaignId, ActiveEffectTargetRef targetRef, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!targetRef.IsValid) throw new ArgumentException("TargetRef is required.", nameof(targetRef));
            if (!TryValidateCampaignBoundary(campaign, campaignId, correlationId, out Error campaignError))
            {
                return Result<IReadOnlyList<ActiveEffectRecord>>.Failure(campaignError);
            }

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureActiveEffectTables(connection);

                var results = new List<ActiveEffectRecord>();
                using (var select = connection.CreateCommand())
                {
                    select.CommandText = SelectColumns + " FROM ActiveEffect WHERE CampaignId = $campaignId AND TargetKind = $targetKind AND " +
                        "TargetCharacterId IS $targetCharacterId AND TargetItemInstanceId IS $targetItemInstanceId ORDER BY AppliedAt, ActiveEffectId;";
                    select.Parameters.AddWithValue("$campaignId", campaignId.ToString());
                    select.Parameters.AddWithValue("$targetKind", targetRef.Kind.ToString());
                    select.Parameters.AddWithValue("$targetCharacterId", targetRef.Kind == ActiveEffectTargetKind.Character ? targetRef.CharacterId.ToString() : (object)DBNull.Value);
                    select.Parameters.AddWithValue("$targetItemInstanceId", targetRef.Kind == ActiveEffectTargetKind.ItemInstance ? targetRef.ItemInstanceId.ToString() : (object)DBNull.Value);
                    using SqliteDataReader reader = select.ExecuteReader();
                    while (reader.Read()) results.Add(ReadRecord(reader));
                }

                return Result<IReadOnlyList<ActiveEffectRecord>>.Success(results);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<IReadOnlyList<ActiveEffectRecord>>.Failure(PersistenceFailures.ActiveEffectIoFailed(correlationId));
            }
        }

        public Result<IReadOnlyList<ActiveEffectRecord>> ListActiveEffectsBySource(CampaignHandle campaign, CampaignId campaignId, ActiveEffectSourceRef sourceRef, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!sourceRef.IsValid) throw new ArgumentException("SourceRef is required.", nameof(sourceRef));
            if (!TryValidateCampaignBoundary(campaign, campaignId, correlationId, out Error campaignError))
            {
                return Result<IReadOnlyList<ActiveEffectRecord>>.Failure(campaignError);
            }

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureActiveEffectTables(connection);

                bool hasItemRef = sourceRef.Kind == ActiveEffectSourceKind.Item || sourceRef.Kind == ActiveEffectSourceKind.EquippedItem;
                var results = new List<ActiveEffectRecord>();
                using (var select = connection.CreateCommand())
                {
                    select.CommandText = SelectColumns + " FROM ActiveEffect WHERE CampaignId = $campaignId AND SourceKind = $sourceKind AND SourceItemRefId IS $sourceItemRefId ORDER BY AppliedAt, ActiveEffectId;";
                    select.Parameters.AddWithValue("$campaignId", campaignId.ToString());
                    select.Parameters.AddWithValue("$sourceKind", sourceRef.Kind.ToString());
                    select.Parameters.AddWithValue("$sourceItemRefId", hasItemRef ? ItemRefIdOf(sourceRef.ItemRef) : (object)DBNull.Value);
                    using SqliteDataReader reader = select.ExecuteReader();
                    while (reader.Read()) results.Add(ReadRecord(reader));
                }

                return Result<IReadOnlyList<ActiveEffectRecord>>.Success(results);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<IReadOnlyList<ActiveEffectRecord>>.Failure(PersistenceFailures.ActiveEffectIoFailed(correlationId));
            }
        }

        public Result<ActiveEffectRecord> ExpireActiveEffect(CampaignHandle campaign, CampaignId campaignId, ActiveEffectId activeEffectId, long expectedRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!activeEffectId.IsValid) throw new ArgumentException("ActiveEffectId is required.", nameof(activeEffectId));
            if (expectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (!TryValidateCampaignBoundary(campaign, campaignId, correlationId, out Error campaignError))
            {
                return Result<ActiveEffectRecord>.Failure(campaignError);
            }

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureActiveEffectTables(connection);

                return _pipeline.Execute(
                    connection,
                    campaignId,
                    commandId,
                    correlationId,
                    tryReplay: transaction => ReplayByCommandId(connection, transaction, campaignId, commandId, correlationId),
                    apply: transaction =>
                    {
                        UtcInstant now = _clock.GetUtcNow();
                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText = "UPDATE ActiveEffect SET Status=$status, Revision=Revision+1, UpdatedAt=$now, LastCommandId=$commandId " +
                                "WHERE ActiveEffectId=$id AND CampaignId=$campaign AND Revision=$expectedRevision;";
                            update.Parameters.AddWithValue("$status", ActiveEffectStatus.Expired.ToString());
                            update.Parameters.AddWithValue("$now", now.ToString());
                            update.Parameters.AddWithValue("$commandId", commandId.ToString());
                            update.Parameters.AddWithValue("$id", activeEffectId.ToString());
                            update.Parameters.AddWithValue("$campaign", campaignId.ToString());
                            update.Parameters.AddWithValue("$expectedRevision", expectedRevision);
                            if (update.ExecuteNonQuery() != 1)
                            {
                                return Result<PipelineWrite<ActiveEffectRecord>>.Failure(PersistenceFailures.ActiveEffectRevisionConflict(correlationId));
                            }
                        }

                        ActiveEffectRecord expired = ReadOneById(connection, transaction, activeEffectId);
                        string payloadJson = "{\"activeEffectId\":\"" + activeEffectId + "\"}";
                        return Result<PipelineWrite<ActiveEffectRecord>>.Success(new PipelineWrite<ActiveEffectRecord>(
                            expired, "odyssey.persistence.active_effect_expired", payloadJson, activeEffectId.ToString(),
                            aggregateType: "active_effect", aggregateId: activeEffectId.ToString(), aggregateRevision: expired.Effect.Revision));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<ActiveEffectRecord>.Failure(PersistenceFailures.ActiveEffectIoFailed(correlationId));
            }
        }

        public Result<long> SetItemEffectEquipped(CampaignHandle campaign, CampaignId campaignId, ActiveEffectId activeEffectId, bool equipped, long expectedRevision, UserId actorUserId, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!activeEffectId.IsValid) throw new ArgumentException("ActiveEffectId is required.", nameof(activeEffectId));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (expectedRevision < 1 || expectedRevision == long.MaxValue) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            if (!TryValidateCampaignBoundary(campaign, campaignId, correlationId, out Error boundaryError))
                return Result<long>.Failure(boundaryError);

            ActiveEffectStatus from = equipped ? ActiveEffectStatus.Suspended : ActiveEffectStatus.Active;
            ActiveEffectStatus to = equipped ? ActiveEffectStatus.Active : ActiveEffectStatus.Suspended;
            string payload = EquipmentTransitionPayload(campaignId, activeEffectId, actorUserId, expectedRevision, to);
            long revision = expectedRevision + 1;
            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureActiveEffectTables(connection);
                return _pipeline.Execute(connection, campaignId, commandId, correlationId,
                    tryReplay: transaction =>
                    {
                        using var replay = connection.CreateCommand();
                        replay.Transaction = transaction;
                        replay.CommandText = "SELECT ResultSummary FROM AppliedCommands WHERE CommandId=$commandId;";
                        replay.Parameters.AddWithValue("$commandId", commandId.ToString());
                        return string.Equals(replay.ExecuteScalar() as string, payload, StringComparison.Ordinal)
                            ? Result<long>.Success(revision)
                            : Result<long>.Failure(PersistenceFailures.CommandReplayFailed(correlationId));
                    },
                    apply: transaction =>
                    {
                        ActiveEffectRecord current;
                        using (var select = connection.CreateCommand())
                        {
                            select.Transaction = transaction;
                            select.CommandText = SelectColumns + " FROM ActiveEffect WHERE ActiveEffectId=$id AND CampaignId=$campaign;";
                            select.Parameters.AddWithValue("$id", activeEffectId.ToString());
                            select.Parameters.AddWithValue("$campaign", campaignId.ToString());
                            using var reader = select.ExecuteReader();
                            if (!reader.Read()) return Result<PipelineWrite<long>>.Failure(PersistenceFailures.ActiveEffectNotFound(correlationId));
                            current = ReadRecord(reader);
                        }

                        ActiveEffect effect = current.Effect;
                        var definition = TypedDefinitionCodec.DecodeEffect(effect.EffectMechanicsSnapshot.ContentType, effect.EffectMechanicsSnapshot.Payload, correlationId);
                        if (definition.IsFailure) return Result<PipelineWrite<long>>.Failure(definition.Error);
                        if (definition.Value.DurationType != EffectDurationType.WhileItemEquipped ||
                            (effect.SourceRef.Kind != ActiveEffectSourceKind.Item && effect.SourceRef.Kind != ActiveEffectSourceKind.EquippedItem))
                            return Result<PipelineWrite<long>>.Failure(ItemEffectLifecycleFailures.Invalid(correlationId));
                        if (effect.Revision != expectedRevision || effect.Status != from)
                            return Result<PipelineWrite<long>>.Failure(PersistenceFailures.ActiveEffectRevisionConflict(correlationId));

                        using var update = connection.CreateCommand();
                        update.Transaction = transaction;
                        update.CommandText = "UPDATE ActiveEffect SET Status=$status, Revision=Revision+1, UpdatedAt=$now, LastCommandId=$command " +
                            "WHERE ActiveEffectId=$id AND CampaignId=$campaign AND Revision=$revision AND Status=$from;";
                        update.Parameters.AddWithValue("$status", to.ToString());
                        update.Parameters.AddWithValue("$now", _clock.GetUtcNow().ToString());
                        update.Parameters.AddWithValue("$command", commandId.ToString());
                        update.Parameters.AddWithValue("$id", activeEffectId.ToString());
                        update.Parameters.AddWithValue("$campaign", campaignId.ToString());
                        update.Parameters.AddWithValue("$revision", expectedRevision);
                        update.Parameters.AddWithValue("$from", from.ToString());
                        if (update.ExecuteNonQuery() != 1)
                            return Result<PipelineWrite<long>>.Failure(PersistenceFailures.ActiveEffectRevisionConflict(correlationId));
                        return Result<PipelineWrite<long>>.Success(new PipelineWrite<long>(revision,
                            equipped ? "odyssey.persistence.active_effect_resumed" : "odyssey.persistence.active_effect_suspended",
                            payload, payload, aggregateType: "active_effect", aggregateId: activeEffectId.ToString(), aggregateRevision: revision));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<long>.Failure(PersistenceFailures.ActiveEffectIoFailed(correlationId));
            }
        }

        // Versioned, fixed-order event and command summary; no reflection serialization.
        private static string EquipmentTransitionPayload(CampaignId campaignId, ActiveEffectId effectId, UserId actor, long expectedRevision, ActiveEffectStatus status)
        {
            using var text = new StringWriter(CultureInfo.InvariantCulture);
            using var writer = new JsonTextWriter(text);
            writer.WriteStartObject();
            writer.WritePropertyName("version"); writer.WriteValue(1);
            writer.WritePropertyName("operation"); writer.WriteValue("item-effect-equipment");
            writer.WritePropertyName("campaignId"); writer.WriteValue(campaignId.ToString());
            writer.WritePropertyName("activeEffectId"); writer.WriteValue(effectId.ToString());
            writer.WritePropertyName("actorUserId"); writer.WriteValue(actor.ToString());
            writer.WritePropertyName("expectedRevision"); writer.WriteValue(expectedRevision);
            writer.WritePropertyName("status"); writer.WriteValue(status.ToString());
            writer.WriteEndObject();
            writer.Flush();
            return text.ToString();
        }

        public Result<ActiveEffectRecord> RemoveActiveEffect(CampaignHandle campaign, CampaignId campaignId, ActiveEffectId activeEffectId, UserId actorUserId, bool actorIsMainGm, long expectedRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!activeEffectId.IsValid) throw new ArgumentException("ActiveEffectId is required.", nameof(activeEffectId));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (expectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            // ADR-028 section 10 rule 3: MainGM-only, checked before touching
            // the database at all -- matching every other MainGM-only gate's
            // own convention (SqliteCharacterRepository.DeleteCharacterPermanently).
            if (!actorIsMainGm)
            {
                return Result<ActiveEffectRecord>.Failure(PersistenceFailures.ActiveEffectOperationDenied(correlationId));
            }

            if (!TryValidateCampaignBoundary(campaign, campaignId, correlationId, out Error campaignError))
            {
                return Result<ActiveEffectRecord>.Failure(campaignError);
            }

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureActiveEffectTables(connection);

                return _pipeline.Execute(
                    connection,
                    campaignId,
                    commandId,
                    correlationId,
                    tryReplay: transaction => ReplayByCommandId(connection, transaction, campaignId, commandId, correlationId),
                    apply: transaction =>
                    {
                        ActiveEffectRecord current;
                        using (var select = connection.CreateCommand())
                        {
                            select.Transaction = transaction;
                            select.CommandText = SelectColumns + " FROM ActiveEffect WHERE ActiveEffectId=$id AND CampaignId=$campaign;";
                            select.Parameters.AddWithValue("$id", activeEffectId.ToString());
                            select.Parameters.AddWithValue("$campaign", campaignId.ToString());
                            using var reader = select.ExecuteReader();
                            if (!reader.Read()) return Result<PipelineWrite<ActiveEffectRecord>>.Failure(PersistenceFailures.ActiveEffectNotFound(correlationId));
                            current = ReadRecord(reader);
                        }

                        if (current.Effect.Status != ActiveEffectStatus.Active && current.Effect.Status != ActiveEffectStatus.Suspended)
                        {
                            // Already terminal (Expired/Removed) -- an early-removal
                            // command has nothing left to end; treated as a CAS
                            // conflict, mirroring SetItemEffectEquipped's own
                            // rejection of a transition from a terminal status.
                            return Result<PipelineWrite<ActiveEffectRecord>>.Failure(PersistenceFailures.ActiveEffectRevisionConflict(correlationId));
                        }

                        UtcInstant now = _clock.GetUtcNow();
                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText = "UPDATE ActiveEffect SET Status=$status, Revision=Revision+1, UpdatedAt=$now, LastCommandId=$commandId " +
                                "WHERE ActiveEffectId=$id AND CampaignId=$campaign AND Revision=$expectedRevision;";
                            update.Parameters.AddWithValue("$status", ActiveEffectStatus.Removed.ToString());
                            update.Parameters.AddWithValue("$now", now.ToString());
                            update.Parameters.AddWithValue("$commandId", commandId.ToString());
                            update.Parameters.AddWithValue("$id", activeEffectId.ToString());
                            update.Parameters.AddWithValue("$campaign", campaignId.ToString());
                            update.Parameters.AddWithValue("$expectedRevision", expectedRevision);
                            if (update.ExecuteNonQuery() != 1)
                            {
                                return Result<PipelineWrite<ActiveEffectRecord>>.Failure(PersistenceFailures.ActiveEffectRevisionConflict(correlationId));
                            }
                        }

                        ActiveEffectRecord removed = ReadOneById(connection, transaction, activeEffectId);
                        string payloadJson = "{\"activeEffectId\":\"" + activeEffectId + "\",\"actorUserId\":\"" + actorUserId + "\"}";
                        return Result<PipelineWrite<ActiveEffectRecord>>.Success(new PipelineWrite<ActiveEffectRecord>(
                            removed, "odyssey.persistence.active_effect_removed", payloadJson, activeEffectId.ToString(),
                            aggregateType: "active_effect", aggregateId: activeEffectId.ToString(), aggregateRevision: removed.Effect.Revision));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<ActiveEffectRecord>.Failure(PersistenceFailures.ActiveEffectIoFailed(correlationId));
            }
        }

        private static ActiveEffectRecord ReadOneById(SqliteConnection connection, SqliteTransaction transaction, ActiveEffectId activeEffectId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = SelectColumns + " FROM ActiveEffect WHERE ActiveEffectId = $activeEffectId LIMIT 1;";
            select.Parameters.AddWithValue("$activeEffectId", activeEffectId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidOperationException("ActiveEffect row disappeared inside its own update transaction.");
            }

            return ReadRecord(reader);
        }

        private static Result<ActiveEffectRecord> ReplayByCommandId(SqliteConnection connection, SqliteTransaction transaction, CampaignId campaignId, CommandId commandId, CorrelationId correlationId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = SelectColumns + " FROM ActiveEffect WHERE LastCommandId = $commandId LIMIT 1;";
            select.Parameters.AddWithValue("$commandId", commandId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read())
            {
                return Result<ActiveEffectRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId));
            }

            return Result<ActiveEffectRecord>.Success(ReadRecord(reader));
        }

        private static void AddParameters(SqliteCommand insert, ActiveEffectRecord record, UtcInstant now)
        {
            ActiveEffect effect = record.Effect;
            bool hasItemRef = effect.SourceRef.Kind == ActiveEffectSourceKind.Item || effect.SourceRef.Kind == ActiveEffectSourceKind.EquippedItem;

            insert.Parameters.AddWithValue("$activeEffectId", effect.ActiveEffectId.ToString());
            insert.Parameters.AddWithValue("$campaignId", record.CampaignId.ToString());
            insert.Parameters.AddWithValue("$effectDefinitionRef", effect.EffectDefinitionRef.ToString());
            insert.Parameters.AddWithValue("$mechanicsSourceDefinitionRef", effect.EffectMechanicsSnapshot.SourceDefinitionRef.ToString());
            insert.Parameters.AddWithValue("$mechanicsDefinitionSnapshotVersion", effect.EffectMechanicsSnapshot.DefinitionSnapshotVersion);
            insert.Parameters.AddWithValue("$mechanicsContentType", effect.EffectMechanicsSnapshot.ContentType.ToString());
            insert.Parameters.AddWithValue("$mechanicsPayload", effect.EffectMechanicsSnapshot.Payload);
            insert.Parameters.AddWithValue("$sourceKind", effect.SourceRef.Kind.ToString());
            insert.Parameters.AddWithValue("$sourceItemRefKind", hasItemRef ? effect.SourceRef.ItemRef.Kind.ToString() : (object)DBNull.Value);
            insert.Parameters.AddWithValue("$sourceItemRefId", hasItemRef ? ItemRefIdOf(effect.SourceRef.ItemRef) : (object)DBNull.Value);
            insert.Parameters.AddWithValue("$targetKind", effect.TargetRef.Kind.ToString());
            insert.Parameters.AddWithValue("$targetCharacterId", effect.TargetRef.Kind == ActiveEffectTargetKind.Character ? effect.TargetRef.CharacterId.ToString() : (object)DBNull.Value);
            insert.Parameters.AddWithValue("$targetItemInstanceId", effect.TargetRef.Kind == ActiveEffectTargetKind.ItemInstance ? effect.TargetRef.ItemInstanceId.ToString() : (object)DBNull.Value);
            insert.Parameters.AddWithValue("$status", effect.Status.ToString());
            insert.Parameters.AddWithValue("$stackCount", effect.StackCount);
            insert.Parameters.AddWithValue("$appliedByUserId", effect.AppliedByUserId.ToString());
            insert.Parameters.AddWithValue("$appliedAt", effect.AppliedAt.ToString());
            insert.Parameters.AddWithValue("$expiresAt", effect.ExpiresAt.HasValue ? effect.ExpiresAt.Value.ToString() : (object)DBNull.Value);
            insert.Parameters.AddWithValue("$revision", effect.Revision);
            insert.Parameters.AddWithValue("$updatedAt", now.ToString());
        }

        /// <summary>ODY-S05-502: shared column-order contract for every INSERT into <c>ActiveEffect</c>. The trailing <c>$lastCommandId</c> parameter is bound by each caller separately (see <see cref="CreateActiveEffect"/>), since it is not part of <see cref="AddParameters"/>'s own record-derived values.</summary>
        private const string InsertColumns =
            "INSERT INTO ActiveEffect (ActiveEffectId, CampaignId, EffectDefinitionRef, " +
            "MechanicsSourceDefinitionRef, MechanicsDefinitionSnapshotVersion, MechanicsContentType, MechanicsPayload, " +
            "SourceKind, SourceItemRefKind, SourceItemRefId, " +
            "TargetKind, TargetCharacterId, TargetItemInstanceId, " +
            "Status, StackCount, AppliedByUserId, AppliedAt, ExpiresAt, Revision, UpdatedAt, LastCommandId)";

        /// <summary>ODY-S05-502: shared column-order contract for every SELECT against <c>ActiveEffect</c> that returns a full row -- <see cref="ReadRecord"/> uses this exact column list/order.</summary>
        private const string SelectColumns =
            "SELECT ActiveEffectId, CampaignId, EffectDefinitionRef, " +
            "MechanicsSourceDefinitionRef, MechanicsDefinitionSnapshotVersion, MechanicsContentType, MechanicsPayload, " +
            "SourceKind, SourceItemRefKind, SourceItemRefId, " +
            "TargetKind, TargetCharacterId, TargetItemInstanceId, " +
            "Status, StackCount, AppliedByUserId, AppliedAt, ExpiresAt, Revision";

        private static ActiveEffectRecord ReadRecord(SqliteDataReader reader)
        {
            ActiveEffectId activeEffectId = ActiveEffectId.Parse(reader.GetString(0));
            CampaignId campaignId = CampaignId.Parse(reader.GetString(1));
            ContentDefinitionRef effectDefinitionRef = ContentDefinitionRef.TryParse(reader.GetString(2), out ContentDefinitionRef parsedRef) ? parsedRef : throw new FormatException("EffectDefinitionRef is not canonical.");

            ContentDefinitionRef mechanicsSourceRef = ContentDefinitionRef.TryParse(reader.GetString(3), out ContentDefinitionRef parsedMechanicsRef) ? parsedMechanicsRef : throw new FormatException("MechanicsSourceDefinitionRef is not canonical.");
            long mechanicsSnapshotVersion = reader.GetInt64(4);
            var mechanicsContentType = Enum.Parse<ContentDefinitionType>(reader.GetString(5));
            string mechanicsPayload = reader.GetString(6);
            var mechanicsSnapshot = new EffectMechanicsSnapshot(mechanicsSourceRef, mechanicsSnapshotVersion, mechanicsContentType, mechanicsPayload);

            var sourceKind = Enum.Parse<ActiveEffectSourceKind>(reader.GetString(7));
            ActiveEffectSourceRef sourceRef = sourceKind switch
            {
                ActiveEffectSourceKind.Item => ActiveEffectSourceRef.ForItem(ParseInventoryItemRef(reader.GetString(8), reader.GetString(9))),
                ActiveEffectSourceKind.EquippedItem => ActiveEffectSourceRef.ForEquippedItem(ParseInventoryItemRef(reader.GetString(8), reader.GetString(9))),
                ActiveEffectSourceKind.Action => ActiveEffectSourceRef.ForAction(),
                ActiveEffectSourceKind.GMDirect => ActiveEffectSourceRef.ForGMDirect(),
                _ => throw new FormatException("SourceKind is not recognized.")
            };

            var targetKind = Enum.Parse<ActiveEffectTargetKind>(reader.GetString(10));
            ActiveEffectTargetRef targetRef = targetKind switch
            {
                ActiveEffectTargetKind.Character => ActiveEffectTargetRef.ForCharacter(CharacterId.Parse(reader.GetString(11))),
                ActiveEffectTargetKind.ItemInstance => ActiveEffectTargetRef.ForItemInstance(ItemInstanceId.Parse(reader.GetString(12))),
                _ => throw new FormatException("TargetKind is not recognized.")
            };

            var status = Enum.Parse<ActiveEffectStatus>(reader.GetString(13));
            long stackCount = reader.GetInt64(14);
            UserId appliedByUserId = UserId.Parse(reader.GetString(15));
            UtcInstant appliedAt = UtcInstant.Parse(reader.GetString(16));
            UtcInstant? expiresAt = reader.IsDBNull(17) ? (UtcInstant?)null : UtcInstant.Parse(reader.GetString(17));
            long revision = reader.GetInt64(18);

            var effect = new ActiveEffect(activeEffectId, effectDefinitionRef, mechanicsSnapshot, sourceRef, targetRef, status, stackCount, appliedByUserId, appliedAt, expiresAt, revision);
            return new ActiveEffectRecord(campaignId, effect);
        }

        private static InventoryItemRef ParseInventoryItemRef(string kind, string id)
        {
            return kind == InventoryItemRefKind.ItemInstance.ToString()
                ? InventoryItemRef.ForInstance(ItemInstanceId.Parse(id))
                : InventoryItemRef.ForStack(ItemStackId.Parse(id));
        }

        private static string ItemRefIdOf(InventoryItemRef itemRef)
        {
            return itemRef.Kind == InventoryItemRefKind.ItemInstance ? itemRef.ItemInstanceId.ToString() : itemRef.ItemStackId.ToString();
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

        private static void EnsureActiveEffectTables(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS ActiveEffect (
    ActiveEffectId TEXT PRIMARY KEY,
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
    LastCommandId TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_ActiveEffect_Campaign_Target ON ActiveEffect (CampaignId, TargetKind, TargetCharacterId, TargetItemInstanceId);
CREATE INDEX IF NOT EXISTS IX_ActiveEffect_Campaign_Source ON ActiveEffect (CampaignId, SourceKind, SourceItemRefId);";
            command.ExecuteNonQuery();
        }

        private static bool TryValidateCampaignBoundary(CampaignHandle campaign, CampaignId campaignId, CorrelationId correlationId, out Error error)
        {
            if (campaign.CampaignId.Equals(campaignId))
            {
                error = default!;
                return true;
            }

            error = PersistenceFailures.ActiveEffectCampaignMismatch(correlationId);
            return false;
        }
    }
}
