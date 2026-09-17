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
using Odyssey.Application.Inventory;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Content;
using Odyssey.Domain.Effects;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;

namespace Odyssey.Persistence.Sqlite
{
    /// <summary>
    /// ODY-S06-107: the sole SQLite implementation of <see cref="IUseItemRepository"/>. Owns a new,
    /// standalone <c>ItemUsage</c> table, by the same precedent <see cref="SqliteActivateAbilityRepository"/>
    /// already established for <c>AbilityActivation</c> (`ODY-S06-106`) -- a root-command apply pipeline, not
    /// general Inventory or Character CRUD.
    ///
    /// Reuses <see cref="SqliteInventoryRepository.ConsumeItemUnitInTransaction"/>/<see cref="SqliteInventoryRepository.RestoreConsumedItemUnitInTransaction"/>
    /// (both `internal`, same assembly, made reusable for this exact cross-repository purpose) and
    /// <see cref="SqliteAttackApplyRepository.ApplyAttackDelta"/> (`internal`, `ODY-S06-106`) verbatim inside
    /// this class's own transactions -- neither is modified.
    ///
    /// Built from the start to `ODY-S06-106`'s FINAL, thrice-revised compensation shape (per this task's own
    /// governing ТЗ §0): <see cref="CompensateItemUsage"/> reverses ALL THREE side-effect categories --
    /// already-created `ActiveEffect`s, resource deltas, AND the consumed item unit -- and durably marks
    /// `CompensationStartedAt`/the durable created-effect-id and consumed-item snapshots BEFORE attempting
    /// any reversal, so a later retry can distinguish "never attempted" from "attempted, incomplete" and
    /// safely resume this exact method, exactly like `SqliteActivateAbilityRepository`'s own final form.
    /// </summary>
    public sealed class SqliteUseItemRepository : IUseItemRepository
    {
        private readonly IWallClock _clock;
        private readonly SqliteSavingPipeline _pipeline;
        private readonly IActiveEffectRepository _effects;

        public SqliteUseItemRepository(IWallClock clock, IActiveEffectRepository effects)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _effects = effects ?? throw new ArgumentNullException(nameof(effects));
            _pipeline = new SqliteSavingPipeline(clock);
        }

        public Result<ItemUsageRecord> GetUsage(CampaignHandle campaign, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureItemUsageTable(connection);
                ItemUsageRecord? found = ReadByCommandId(connection, null, commandId);
                return found == null
                    ? Result<ItemUsageRecord>.Failure(PersistenceFailures.AttackOutcomeNotFound(correlationId))
                    : Result<ItemUsageRecord>.Success(found);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<ItemUsageRecord>.Failure(PersistenceFailures.AttackOutcomeIoFailed(correlationId));
            }
        }

        public Result<ItemUsageRecord> RecordItemUsage(CampaignHandle campaign, CharacterId actorId, InventoryItemRef item, long expectedItemRevision, long expectedInventoryRevision, long expectedCharacterResourcesRevision, IReadOnlyList<AttackDelta> resourceDeltas, IReadOnlyList<ContentDefinitionRef> effectsToApply, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (expectedItemRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedItemRevision));
            if (expectedInventoryRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedInventoryRevision));
            if (expectedCharacterResourcesRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedCharacterResourcesRevision));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureItemUsageTable(connection);
                SqliteInventoryRepository.EnsureInventoryTables(connection);
                UtcInstant now = _clock.GetUtcNow();

                return _pipeline.Execute(
                    connection,
                    campaign.CampaignId,
                    commandId,
                    correlationId,
                    tryReplay: transaction =>
                    {
                        ItemUsageRecord? replayed = ReadByCommandId(connection, transaction, commandId);
                        return replayed == null
                            ? Result<ItemUsageRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId))
                            : Result<ItemUsageRecord>.Success(replayed);
                    },
                    apply: transaction =>
                    {
                        // ADR-022 section 5 rule 2, mirroring ODY-S06-106's own RecordAbilityActivation
                        // pattern exactly: re-read every declared section revision FRESH, inside this same
                        // transaction, immediately before applying anything.
                        Result<(long ItemRevision, InventoryId InventoryId, CampaignId ItemCampaignId)> itemState = ReadItemRevisionAndInventory(connection, transaction, item, correlationId);
                        if (itemState.IsFailure) return Result<PipelineWrite<ItemUsageRecord>>.Failure(itemState.Error);
                        if (!itemState.Value.ItemCampaignId.Equals(campaign.CampaignId)) return Result<PipelineWrite<ItemUsageRecord>>.Failure(PersistenceFailures.InventoryCampaignMismatch(correlationId));
                        if (itemState.Value.ItemRevision != expectedItemRevision) return Result<PipelineWrite<ItemUsageRecord>>.Failure(InventoryMovementFailures.ItemRevisionConflict(correlationId));

                        Result<long> inventoryRevision = ReadInventoryRevision(connection, transaction, itemState.Value.InventoryId, correlationId);
                        if (inventoryRevision.IsFailure) return Result<PipelineWrite<ItemUsageRecord>>.Failure(inventoryRevision.Error);
                        if (inventoryRevision.Value != expectedInventoryRevision) return Result<PipelineWrite<ItemUsageRecord>>.Failure(InventoryMovementFailures.InventoryRevisionConflict(correlationId));

                        Result<long> resourcesRevision = ReadCharacterResourcesRevision(connection, transaction, actorId, correlationId);
                        if (resourcesRevision.IsFailure) return Result<PipelineWrite<ItemUsageRecord>>.Failure(resourcesRevision.Error);
                        if (resourcesRevision.Value != expectedCharacterResourcesRevision) return Result<PipelineWrite<ItemUsageRecord>>.Failure(PersistenceFailures.CharacterRevisionConflict(correlationId));

                        Result<ConsumedItemUnit> consumed = SqliteInventoryRepository.ConsumeItemUnitInTransaction(connection, transaction, item, expectedItemRevision, now, correlationId);
                        if (consumed.IsFailure) return Result<PipelineWrite<ItemUsageRecord>>.Failure(consumed.Error);

                        for (int index = 0; index < resourceDeltas.Count; index++)
                        {
                            Result deltaResult = SqliteAttackApplyRepository.ApplyAttackDelta(connection, transaction, resourceDeltas[index], commandId, now, correlationId);
                            if (deltaResult.IsFailure) return Result<PipelineWrite<ItemUsageRecord>>.Failure(deltaResult.Error);
                        }

                        var record = new ItemUsageRecord(commandId, campaign.CampaignId, actorId, item, resourceDeltas, effectsToApply, now, compensatedAt: null, compensationStartedAt: null);

                        using (var insert = connection.CreateCommand())
                        {
                            insert.Transaction = transaction;
                            insert.CommandText = "INSERT INTO ItemUsage (CommandId, CampaignId, ActorId, ItemRefKind, ItemRefId, ResourceDeltasJson, AppliedEffectRefsJson, CreatedAt, CompensatedAt, CompensationStartedAt, CreatedEffectIdsJson, ConsumedItemUnitJson) " +
                                                  "VALUES ($commandId, $campaignId, $actorId, $itemRefKind, $itemRefId, $resourceDeltasJson, $appliedEffectRefsJson, $createdAt, NULL, NULL, NULL, $consumedItemUnitJson);";
                            insert.Parameters.AddWithValue("$commandId", commandId.ToString());
                            insert.Parameters.AddWithValue("$campaignId", campaign.CampaignId.ToString());
                            insert.Parameters.AddWithValue("$actorId", actorId.ToString());
                            insert.Parameters.AddWithValue("$itemRefKind", item.Kind.ToString());
                            insert.Parameters.AddWithValue("$itemRefId", item.Kind == InventoryItemRefKind.ItemStack ? item.ItemStackId.ToString() : item.ItemInstanceId.ToString());
                            insert.Parameters.AddWithValue("$resourceDeltasJson", SerializeDeltas(resourceDeltas));
                            insert.Parameters.AddWithValue("$appliedEffectRefsJson", SerializeEffectRefs(effectsToApply));
                            insert.Parameters.AddWithValue("$createdAt", now.ToString());
                            insert.Parameters.AddWithValue("$consumedItemUnitJson", SerializeConsumedItemUnit(consumed.Value));
                            insert.ExecuteNonQuery();
                        }

                        string payloadJson = "{\"commandId\":\"" + commandId + "\",\"actorId\":\"" + actorId + "\"}";
                        return Result<PipelineWrite<ItemUsageRecord>>.Success(new PipelineWrite<ItemUsageRecord>(
                            record, "odyssey.persistence.item_used", payloadJson, commandId.ToString(),
                            aggregateType: "item_usage", aggregateId: commandId.ToString(), aggregateRevision: 1));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<ItemUsageRecord>.Failure(PersistenceFailures.AttackOutcomeIoFailed(correlationId));
            }
        }

        /// <summary>
        /// ODY-S06-107, built to `ODY-S06-106`'s final, thrice-revised compensation shape from the start:
        /// (1) removes every already-created `ActiveEffect` (durably marking `CompensationStartedAt`/the
        /// created-effect-id list on the FIRST call, before any reversal, so a resumed call finishes the
        /// SAME set); (2) reverses every resource delta; (3) restores the consumed item unit to its exact
        /// prior state (same id, same every field -- via `SqliteInventoryRepository.RestoreConsumedItemUnitInTransaction`).
        /// If any single step fails, returns that failure immediately without attempting the remaining
        /// steps or setting `CompensatedAt` -- retryable later, since every step already attempted is itself
        /// idempotent.
        /// </summary>
        public Result<ItemUsageRecord> CompensateItemUsage(CampaignHandle campaign, CommandId originalCommandId, IReadOnlyList<ActiveEffectId> createdEffectIds, UserId actorUserId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!originalCommandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(originalCommandId));
            if (createdEffectIds == null) throw new ArgumentNullException(nameof(createdEffectIds));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureItemUsageTable(connection);
                SqliteInventoryRepository.EnsureInventoryTables(connection);

                ItemUsageRecord? existing = ReadByCommandId(connection, null, originalCommandId);
                if (existing == null) return Result<ItemUsageRecord>.Failure(PersistenceFailures.AttackOutcomeNotFound(correlationId));
                if (existing.CompensatedAt != null) return Result<ItemUsageRecord>.Success(existing);

                // ODY-S06-107 (built from the start to ODY-S06-106's third-доработка resolution): mark
                // CompensationStartedAt and persist the effect-id list durably on the FIRST call only (the
                // WHERE clause makes this a no-op on a retry); then always read the DURABLE list back, so a
                // resumed call finishes the SAME set the original attempt started.
                EnsureCompensationStarted(connection, originalCommandId, createdEffectIds, _clock.GetUtcNow());
                IReadOnlyList<ActiveEffectId> effectIdsToRemove = ReadDurableCreatedEffectIds(connection, originalCommandId);

                foreach (ActiveEffectId effectId in effectIdsToRemove)
                {
                    CommandId removalCommandId = StableEffectRemovalCommandId(originalCommandId, effectId);
                    Result<ActiveEffectRecord> removed = _effects.RemoveActiveEffect(campaign, campaign.CampaignId, effectId, actorUserId, actorIsMainGm: true, expectedRevision: 1, removalCommandId, correlationId);
                    if (removed.IsFailure) return Result<ItemUsageRecord>.Failure(removed.Error);
                }

                ConsumedItemUnit? consumed = ReadDurableConsumedItemUnit(connection, originalCommandId);
                CommandId compensationCommandId = StableCompensationCommandId(originalCommandId);
                UtcInstant now = _clock.GetUtcNow();

                return _pipeline.Execute(
                    connection,
                    campaign.CampaignId,
                    compensationCommandId,
                    correlationId,
                    tryReplay: transaction =>
                    {
                        ItemUsageRecord? replayed = ReadByCommandId(connection, transaction, originalCommandId);
                        return replayed != null && replayed.CompensatedAt != null
                            ? Result<ItemUsageRecord>.Success(replayed)
                            : Result<ItemUsageRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId));
                    },
                    apply: transaction =>
                    {
                        foreach (AttackDelta original in existing.ResourceDeltas)
                        {
                            var reversed = new AttackDelta(original.TargetRef, -original.Value);
                            Result deltaResult = SqliteAttackApplyRepository.ApplyAttackDelta(connection, transaction, reversed, compensationCommandId, now, correlationId);
                            if (deltaResult.IsFailure) return Result<PipelineWrite<ItemUsageRecord>>.Failure(deltaResult.Error);
                        }

                        if (consumed != null)
                        {
                            Result restored = SqliteInventoryRepository.RestoreConsumedItemUnitInTransaction(connection, transaction, consumed, now, correlationId);
                            if (restored.IsFailure) return Result<PipelineWrite<ItemUsageRecord>>.Failure(restored.Error);
                        }

                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText = "UPDATE ItemUsage SET CompensatedAt = $compensatedAt WHERE CommandId = $commandId;";
                            update.Parameters.AddWithValue("$compensatedAt", now.ToString());
                            update.Parameters.AddWithValue("$commandId", originalCommandId.ToString());
                            update.ExecuteNonQuery();
                        }

                        var compensated = new ItemUsageRecord(existing.CommandId, existing.CampaignId, existing.ActorId, existing.Item, existing.ResourceDeltas, existing.AppliedEffectRefs, existing.OccurredAt, compensatedAt: now, compensationStartedAt: existing.CompensationStartedAt ?? now);
                        string payloadJson = "{\"originalCommandId\":\"" + originalCommandId + "\"}";
                        return Result<PipelineWrite<ItemUsageRecord>>.Success(new PipelineWrite<ItemUsageRecord>(
                            compensated, "odyssey.persistence.item_usage_compensated", payloadJson, compensationCommandId.ToString(),
                            aggregateType: "item_usage", aggregateId: originalCommandId.ToString(), aggregateRevision: 2));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<ItemUsageRecord>.Failure(PersistenceFailures.AttackOutcomeIoFailed(correlationId));
            }
        }

        private static Result<(long ItemRevision, InventoryId InventoryId, CampaignId ItemCampaignId)> ReadItemRevisionAndInventory(SqliteConnection connection, SqliteTransaction transaction, InventoryItemRef item, CorrelationId correlationId)
        {
            if (item.Kind == InventoryItemRefKind.ItemStack)
            {
                Application.Inventory.ItemStackRecord? stack = SqliteInventoryRepository.SelectItemStack(connection, transaction, item.ItemStackId.ToString());
                return stack == null
                    ? Result<(long, InventoryId, CampaignId)>.Failure(PersistenceFailures.ItemStackNotFound(correlationId))
                    : Result<(long, InventoryId, CampaignId)>.Success((stack.Revision, stack.InventoryId, stack.CampaignId));
            }

            Application.Inventory.ItemInstanceRecord? instance = SqliteInventoryRepository.SelectItemInstance(connection, transaction, item.ItemInstanceId.ToString());
            return instance == null
                ? Result<(long, InventoryId, CampaignId)>.Failure(PersistenceFailures.ItemInstanceNotFound(correlationId))
                : Result<(long, InventoryId, CampaignId)>.Success((instance.Revision, instance.InventoryId, instance.CampaignId));
        }

        private static Result<long> ReadInventoryRevision(SqliteConnection connection, SqliteTransaction transaction, InventoryId inventoryId, CorrelationId correlationId)
        {
            Application.Inventory.InventoryRecord? inventory = SqliteInventoryRepository.SelectInventory(connection, transaction, inventoryId.ToString());
            return inventory == null
                ? Result<long>.Failure(PersistenceFailures.InventoryNotFound(correlationId))
                : Result<long>.Success(inventory.Revision);
        }

        /// <summary>ADR-022 section 5 rule 2's own fresh-read requirement, mirroring `SqliteActivateAbilityRepository.ReadCharacterSectionRevisions` -- reads straight from the `Character` row, not from any already-loaded `CharacterRecord`.</summary>
        private static Result<long> ReadCharacterResourcesRevision(SqliteConnection connection, SqliteTransaction transaction, CharacterId characterId, CorrelationId correlationId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT CharacterResourcesRevision FROM Character WHERE CharacterId = $characterId;";
            select.Parameters.AddWithValue("$characterId", characterId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read()) return Result<long>.Failure(PersistenceFailures.CharacterNotFound(correlationId));
            return Result<long>.Success(reader.GetInt64(0));
        }

        private static void EnsureCompensationStarted(SqliteConnection connection, CommandId originalCommandId, IReadOnlyList<ActiveEffectId> createdEffectIds, UtcInstant now)
        {
            using var update = connection.CreateCommand();
            update.CommandText = "UPDATE ItemUsage SET CompensationStartedAt = $startedAt, CreatedEffectIdsJson = $effectIdsJson WHERE CommandId = $commandId AND CompensationStartedAt IS NULL;";
            update.Parameters.AddWithValue("$startedAt", now.ToString());
            update.Parameters.AddWithValue("$effectIdsJson", SerializeActiveEffectIds(createdEffectIds));
            update.Parameters.AddWithValue("$commandId", originalCommandId.ToString());
            update.ExecuteNonQuery();
        }

        private static IReadOnlyList<ActiveEffectId> ReadDurableCreatedEffectIds(SqliteConnection connection, CommandId originalCommandId)
        {
            using var select = connection.CreateCommand();
            select.CommandText = "SELECT CreatedEffectIdsJson FROM ItemUsage WHERE CommandId = $commandId LIMIT 1;";
            select.Parameters.AddWithValue("$commandId", originalCommandId.ToString());
            object? result = select.ExecuteScalar();
            return result == null || result == DBNull.Value ? Array.Empty<ActiveEffectId>() : DeserializeActiveEffectIds((string)result);
        }

        private static ConsumedItemUnit? ReadDurableConsumedItemUnit(SqliteConnection connection, CommandId originalCommandId)
        {
            using var select = connection.CreateCommand();
            select.CommandText = "SELECT ConsumedItemUnitJson FROM ItemUsage WHERE CommandId = $commandId LIMIT 1;";
            select.Parameters.AddWithValue("$commandId", originalCommandId.ToString());
            object? result = select.ExecuteScalar();
            return result == null || result == DBNull.Value ? null : DeserializeConsumedItemUnit((string)result);
        }

        /// <summary>Mirrors `SqliteActivateAbilityRepository.StableCompensationCommandId`'s own SHA256-derivation pattern -- a deterministic compensation `CommandId`, always the same for a given original `CommandId`.</summary>
        private static CommandId StableCompensationCommandId(CommandId originalCommandId)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes("ody-s06-107/compensate/v1/" + originalCommandId));
            var text = new StringBuilder(32);
            for (int i = 0; i < 16; i++) text.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return CommandId.Parse("cmd_" + text);
        }

        /// <summary>Mirrors `SqliteActivateAbilityRepository.StableEffectRemovalCommandId`'s own DIFFERENT-hash-input pattern -- deliberately distinct from the creation-time sub-`CommandId` (`UseItemService.StableSubCommandId`, prefix `ody-s06-107/v1/`) so `RemoveActiveEffect`'s own idempotency-ledger entry never collides with `CreateActiveEffect`'s own.</summary>
        private static CommandId StableEffectRemovalCommandId(CommandId originalCommandId, ActiveEffectId activeEffectId)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes("ody-s06-107/compensate-effect/v1/" + originalCommandId + "/" + activeEffectId));
            var text = new StringBuilder(32);
            for (int i = 0; i < 16; i++) text.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return CommandId.Parse("cmd_" + text);
        }

        private static ItemUsageRecord? ReadByCommandId(SqliteConnection connection, SqliteTransaction? transaction, CommandId commandId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT CommandId, CampaignId, ActorId, ItemRefKind, ItemRefId, ResourceDeltasJson, AppliedEffectRefsJson, CreatedAt, CompensatedAt, CompensationStartedAt FROM ItemUsage WHERE CommandId = $commandId LIMIT 1;";
            select.Parameters.AddWithValue("$commandId", commandId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read()) return null;

            var kind = (InventoryItemRefKind)Enum.Parse(typeof(InventoryItemRefKind), reader.GetString(3));
            InventoryItemRef item = kind == InventoryItemRefKind.ItemStack
                ? InventoryItemRef.ForStack(ItemStackId.Parse(reader.GetString(4)))
                : InventoryItemRef.ForInstance(ItemInstanceId.Parse(reader.GetString(4)));

            return new ItemUsageRecord(
                CommandId.Parse(reader.GetString(0)),
                CampaignId.Parse(reader.GetString(1)),
                CharacterId.Parse(reader.GetString(2)),
                item,
                DeserializeDeltas(reader.GetString(5)),
                DeserializeEffectRefs(reader.GetString(6)),
                UtcInstant.Parse(reader.GetString(7)),
                reader.IsDBNull(8) ? (UtcInstant?)null : UtcInstant.Parse(reader.GetString(8)),
                reader.IsDBNull(9) ? (UtcInstant?)null : UtcInstant.Parse(reader.GetString(9)));
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

        /// <summary>
        /// ODY-S06-107: durably captures everything `RestoreConsumedItemUnitInTransaction` needs to reverse
        /// `ConsumeItemUnitInTransaction` -- since a later compensation call cannot rely on the original,
        /// now-abandoned in-memory `ConsumedItemUnit` object (mirroring exactly why `CreatedEffectIdsJson`
        /// exists for effect ids).
        /// </summary>
        private static string SerializeConsumedItemUnit(ConsumedItemUnit consumed)
        {
            var json = new JObject
            {
                ["itemRefKind"] = consumed.Item.Kind.ToString(),
                ["itemRefId"] = consumed.Item.Kind == InventoryItemRefKind.ItemStack ? consumed.Item.ItemStackId.ToString() : consumed.Item.ItemInstanceId.ToString(),
                ["wasDeleted"] = consumed.WasDeleted,
                ["revisionAfterConsumption"] = consumed.RevisionAfterConsumption,
            };

            if (consumed.PriorStack != null)
            {
                Application.Inventory.ItemStackRecord prior = consumed.PriorStack;
                json["priorStack"] = new JObject
                {
                    ["itemStackId"] = prior.ItemStackId.ToString(),
                    ["campaignId"] = prior.CampaignId.ToString(),
                    ["inventoryId"] = prior.InventoryId.ToString(),
                    ["ownerKind"] = prior.OwnerRef.Kind.ToString(),
                    ["ownerTargetRef"] = prior.OwnerRef.TargetRef,
                    ["ownerLocationKey"] = prior.OwnerRef.LocationKey,
                    ["locationKind"] = prior.LocationRef.Kind.ToString(),
                    ["locationTargetRef"] = prior.LocationRef.TargetRef,
                    ["locationDetailRef"] = prior.LocationRef.DetailRef,
                    ["sourceItemDefinitionRef"] = prior.SourceItemDefinitionRef.ToString(),
                    ["mechanicsSourceDefinitionRef"] = prior.MechanicsSnapshot.SourceDefinitionRef.ToString(),
                    ["mechanicsDefinitionSnapshotVersion"] = prior.MechanicsSnapshot.DefinitionSnapshotVersion,
                    ["mechanicsContentType"] = prior.MechanicsSnapshot.ContentType.ToString(),
                    ["mechanicsPayload"] = prior.MechanicsSnapshot.Payload,
                    ["quantity"] = prior.Quantity.Value,
                    ["stackState"] = prior.StackState,
                    ["revision"] = prior.Revision,
                    ["createdAt"] = prior.CreatedAt.ToString(),
                };
            }
            else if (consumed.PriorInstance != null)
            {
                Application.Inventory.ItemInstanceRecord prior = consumed.PriorInstance;
                json["priorInstance"] = new JObject
                {
                    ["itemInstanceId"] = prior.ItemInstanceId.ToString(),
                    ["campaignId"] = prior.CampaignId.ToString(),
                    ["inventoryId"] = prior.InventoryId.ToString(),
                    ["ownerKind"] = prior.OwnerRef.Kind.ToString(),
                    ["ownerTargetRef"] = prior.OwnerRef.TargetRef,
                    ["ownerLocationKey"] = prior.OwnerRef.LocationKey,
                    ["locationKind"] = prior.LocationRef.Kind.ToString(),
                    ["locationTargetRef"] = prior.LocationRef.TargetRef,
                    ["locationDetailRef"] = prior.LocationRef.DetailRef,
                    ["sourceItemDefinitionRef"] = prior.SourceItemDefinitionRef.ToString(),
                    ["mechanicsSourceDefinitionRef"] = prior.MechanicsSnapshot.SourceDefinitionRef.ToString(),
                    ["mechanicsDefinitionSnapshotVersion"] = prior.MechanicsSnapshot.DefinitionSnapshotVersion,
                    ["mechanicsContentType"] = prior.MechanicsSnapshot.ContentType.ToString(),
                    ["mechanicsPayload"] = prior.MechanicsSnapshot.Payload,
                    ["runtimeState"] = prior.RuntimeState,
                    ["revision"] = prior.Revision,
                    ["createdAt"] = prior.CreatedAt.ToString(),
                };
            }

            return json.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static ConsumedItemUnit DeserializeConsumedItemUnit(string json)
        {
            // Newtonsoft's default parser auto-detects ISO-8601-looking strings and silently reformats them
            // on read-back (e.g. via a culture-specific DateTime.ToString()), corrupting UtcInstant.Parse's
            // own strict format -- SqliteCharacterRepository.ParseJsonPreservingStrings (internal, same
            // assembly) already exists specifically to disable that auto-detection; reused here rather than
            // re-deriving the same fix.
            JObject obj = (JObject)SqliteCharacterRepository.ParseJsonPreservingStrings(json);
            var kind = (InventoryItemRefKind)Enum.Parse(typeof(InventoryItemRefKind), (string)obj["itemRefKind"]!);
            InventoryItemRef item = kind == InventoryItemRefKind.ItemStack
                ? InventoryItemRef.ForStack(ItemStackId.Parse((string)obj["itemRefId"]!))
                : InventoryItemRef.ForInstance(ItemInstanceId.Parse((string)obj["itemRefId"]!));
            bool wasDeleted = (bool)obj["wasDeleted"]!;

            JObject? stackJson = (JObject?)obj["priorStack"];
            if (stackJson != null)
            {
                var snapshot = new ItemMechanicsSnapshot(
                    ContentDefinitionRef.Parse((string)stackJson["mechanicsSourceDefinitionRef"]!),
                    (long)stackJson["mechanicsDefinitionSnapshotVersion"]!,
                    (ContentDefinitionType)Enum.Parse(typeof(ContentDefinitionType), (string)stackJson["mechanicsContentType"]!),
                    (string)stackJson["mechanicsPayload"]!);
                var ownerRef = ReadOwnerRef((string)stackJson["ownerKind"]!, (string)stackJson["ownerTargetRef"]!, (string?)stackJson["ownerLocationKey"]);
                var locationRef = ReadLocationRef((string)stackJson["locationKind"]!, (string)stackJson["locationTargetRef"]!, (string)stackJson["locationDetailRef"]!);
                var prior = new Application.Inventory.ItemStackRecord(
                    ItemStackId.Parse((string)stackJson["itemStackId"]!),
                    CampaignId.Parse((string)stackJson["campaignId"]!),
                    InventoryId.Parse((string)stackJson["inventoryId"]!),
                    ownerRef,
                    locationRef,
                    ContentDefinitionRef.Parse((string)stackJson["sourceItemDefinitionRef"]!),
                    snapshot,
                    Domain.Inventory.ItemStackQuantity.Create((long)stackJson["quantity"]!),
                    (string)stackJson["stackState"]!,
                    (long)stackJson["revision"]!,
                    UtcInstant.Parse((string)stackJson["createdAt"]!),
                    UtcInstant.Parse((string)stackJson["createdAt"]!));
                return wasDeleted ? ConsumedItemUnit.ForDeletedStack(item, prior) : ConsumedItemUnit.ForDecrementedStack(item, prior);
            }

            JObject instanceJson = (JObject)obj["priorInstance"]!;
            var instanceSnapshot = new ItemMechanicsSnapshot(
                ContentDefinitionRef.Parse((string)instanceJson["mechanicsSourceDefinitionRef"]!),
                (long)instanceJson["mechanicsDefinitionSnapshotVersion"]!,
                (ContentDefinitionType)Enum.Parse(typeof(ContentDefinitionType), (string)instanceJson["mechanicsContentType"]!),
                (string)instanceJson["mechanicsPayload"]!);
            var instanceOwnerRef = ReadOwnerRef((string)instanceJson["ownerKind"]!, (string)instanceJson["ownerTargetRef"]!, (string?)instanceJson["ownerLocationKey"]);
            var instanceLocationRef = ReadLocationRef((string)instanceJson["locationKind"]!, (string)instanceJson["locationTargetRef"]!, (string)instanceJson["locationDetailRef"]!);
            var priorInstance = new Application.Inventory.ItemInstanceRecord(
                ItemInstanceId.Parse((string)instanceJson["itemInstanceId"]!),
                CampaignId.Parse((string)instanceJson["campaignId"]!),
                InventoryId.Parse((string)instanceJson["inventoryId"]!),
                instanceOwnerRef,
                instanceLocationRef,
                ContentDefinitionRef.Parse((string)instanceJson["sourceItemDefinitionRef"]!),
                instanceSnapshot,
                (string)instanceJson["runtimeState"]!,
                (long)instanceJson["revision"]!,
                UtcInstant.Parse((string)instanceJson["createdAt"]!),
                UtcInstant.Parse((string)instanceJson["createdAt"]!));
            return ConsumedItemUnit.ForDeletedInstance(item, priorInstance);
        }

        private static InventoryOwnerRef ReadOwnerRef(string kind, string targetRef, string? locationKey)
        {
            var ownerKind = (InventoryOwnerKind)Enum.Parse(typeof(InventoryOwnerKind), kind);
            if (ownerKind == InventoryOwnerKind.Character) return InventoryOwnerRef.ForCharacter(CharacterId.Parse(targetRef));
            if (ownerKind == InventoryOwnerKind.Scene) return InventoryOwnerRef.ForScene(SceneId.Parse(targetRef), locationKey!);
            throw new FormatException("Unknown inventory owner kind.");
        }

        private static InventoryLocationRef ReadLocationRef(string kind, string targetRef, string detailRef)
        {
            var locationKind = (InventoryLocationKind)Enum.Parse(typeof(InventoryLocationKind), kind);
            if (locationKind == InventoryLocationKind.Contained) return InventoryLocationRef.Contained(InventoryId.Parse(targetRef), detailRef);
            if (locationKind == InventoryLocationKind.Equipped) return InventoryLocationRef.Equipped(InventoryId.Parse(targetRef), detailRef);
            if (locationKind == InventoryLocationKind.SceneDropped) return InventoryLocationRef.SceneDropped(SceneId.Parse(targetRef), detailRef);
            if (locationKind == InventoryLocationKind.Other) return InventoryLocationRef.Other(targetRef, detailRef);
            throw new FormatException("Unknown inventory location kind.");
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

        private static void EnsureItemUsageTable(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS ItemUsage (
    CommandId TEXT PRIMARY KEY,
    CampaignId TEXT NOT NULL,
    ActorId TEXT NOT NULL,
    ItemRefKind TEXT NOT NULL,
    ItemRefId TEXT NOT NULL,
    ResourceDeltasJson TEXT NOT NULL,
    AppliedEffectRefsJson TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    CompensatedAt TEXT,
    CompensationStartedAt TEXT,
    CreatedEffectIdsJson TEXT,
    ConsumedItemUnitJson TEXT
);
CREATE INDEX IF NOT EXISTS IX_ItemUsage_CampaignId ON ItemUsage(CampaignId);";
            command.ExecuteNonQuery();
        }
    }
}
