using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Odyssey.Application.Commands;
using Odyssey.Application.Inventory;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;

namespace Odyssey.Persistence.Sqlite
{
    /// <summary>
    /// ODY-S05-202: SQLite storage foundation for Inventory runtime records.
    /// Stores already-built records only; create-from-catalog commands and
    /// all inventory gameplay semantics belong to later tasks.
    /// </summary>
    public sealed class SqliteInventoryRepository : IInventoryRepository
    {
        private const string TargetInventory = "Inventory";
        private const string TargetItemInstance = "ItemInstance";
        private const string TargetItemStack = "ItemStack";
        private readonly IWallClock _clock;

        public SqliteInventoryRepository(IWallClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public Result<InventoryRecord> CreateInventory(CampaignHandle campaign, InventoryRecord record, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (!TryValidateCampaignBoundary(campaign, record.CampaignId, correlationId, out Error campaignError))
            {
                return Result<InventoryRecord>.Failure(campaignError);
            }

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureInventoryTables(connection);
                using SqliteTransaction transaction = connection.BeginTransaction();

                Result<InventoryRecord>? replay = TryReplay(connection, transaction, commandId, TargetInventory, record.InventoryId.ToString(), SelectInventory, correlationId);
                if (replay != null)
                {
                    transaction.Commit();
                    return replay.Value;
                }

                using (var insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = "INSERT INTO Inventory (InventoryId, CampaignId, OwnerKind, OwnerTargetRef, OwnerLocationKey, Revision, CreatedAt, UpdatedAt) VALUES ($inventoryId, $campaignId, $ownerKind, $ownerTargetRef, $ownerLocationKey, $revision, $createdAt, $updatedAt);";
                    insert.Parameters.AddWithValue("$inventoryId", record.InventoryId.ToString());
                    insert.Parameters.AddWithValue("$campaignId", record.CampaignId.ToString());
                    insert.Parameters.AddWithValue("$ownerKind", record.OwnerRef.Kind.ToString());
                    insert.Parameters.AddWithValue("$ownerTargetRef", record.OwnerRef.TargetRef);
                    insert.Parameters.AddWithValue("$ownerLocationKey", (object?)record.OwnerRef.LocationKey ?? DBNull.Value);
                    insert.Parameters.AddWithValue("$revision", record.Revision);
                    insert.Parameters.AddWithValue("$createdAt", record.CreatedAt.ToString());
                    insert.Parameters.AddWithValue("$updatedAt", record.UpdatedAt.ToString());
                    insert.ExecuteNonQuery();
                }

                InsertLedgerEntry(connection, transaction, commandId, TargetInventory, record.InventoryId.ToString(), _clock.GetUtcNow());
                transaction.Commit();
                return Result<InventoryRecord>.Success(record);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<InventoryRecord>.Failure(PersistenceFailures.InventoryIoFailed(correlationId));
            }
        }

        public Result<InventoryRecord> GetInventory(CampaignHandle campaign, InventoryId inventoryId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!inventoryId.IsValid) throw new ArgumentException("InventoryId is required.", nameof(inventoryId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureInventoryTables(connection);
                InventoryRecord? record = SelectInventory(connection, null, inventoryId.ToString());
                return record == null
                    ? Result<InventoryRecord>.Failure(PersistenceFailures.InventoryNotFound(correlationId))
                    : Result<InventoryRecord>.Success(record);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<InventoryRecord>.Failure(PersistenceFailures.InventoryIoFailed(correlationId));
            }
        }

        public Result<ItemInstanceRecord> CreateItemInstance(CampaignHandle campaign, ItemInstanceRecord record, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (!TryValidateCampaignBoundary(campaign, record.CampaignId, correlationId, out Error campaignError))
            {
                return Result<ItemInstanceRecord>.Failure(campaignError);
            }

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureInventoryTables(connection);
                using SqliteTransaction transaction = connection.BeginTransaction();

                Result<ItemInstanceRecord>? replay = TryReplay(connection, transaction, commandId, TargetItemInstance, record.ItemInstanceId.ToString(), SelectItemInstance, correlationId);
                if (replay != null)
                {
                    transaction.Commit();
                    return replay.Value;
                }

                if (!InventoryExists(connection, transaction, record.CampaignId, record.InventoryId))
                {
                    transaction.Commit();
                    return Result<ItemInstanceRecord>.Failure(PersistenceFailures.InventoryNotFound(correlationId));
                }

                using (var insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = "INSERT INTO ItemInstance (" +
                        "ItemInstanceId, CampaignId, InventoryId, OwnerKind, OwnerTargetRef, OwnerLocationKey, " +
                        "LocationKind, LocationTargetRef, LocationDetailRef, SourceItemDefinitionRef, " +
                        "MechanicsSourceDefinitionRef, MechanicsDefinitionSnapshotVersion, MechanicsContentType, MechanicsPayload, " +
                        "RuntimeState, Revision, CreatedAt, UpdatedAt) VALUES (" +
                        "$itemInstanceId, $campaignId, $inventoryId, $ownerKind, $ownerTargetRef, $ownerLocationKey, " +
                        "$locationKind, $locationTargetRef, $locationDetailRef, $sourceItemDefinitionRef, " +
                        "$mechanicsSourceDefinitionRef, $mechanicsDefinitionSnapshotVersion, $mechanicsContentType, $mechanicsPayload, " +
                        "$runtimeState, $revision, $createdAt, $updatedAt);";
                    AddItemCommonParameters(insert, record.CampaignId, record.InventoryId, record.OwnerRef, record.LocationRef, record.SourceItemDefinitionRef, record.MechanicsSnapshot, record.Revision, record.CreatedAt, record.UpdatedAt);
                    insert.Parameters.AddWithValue("$itemInstanceId", record.ItemInstanceId.ToString());
                    insert.Parameters.AddWithValue("$runtimeState", record.RuntimeState);
                    insert.ExecuteNonQuery();
                }

                InsertLedgerEntry(connection, transaction, commandId, TargetItemInstance, record.ItemInstanceId.ToString(), _clock.GetUtcNow());
                transaction.Commit();
                return Result<ItemInstanceRecord>.Success(record);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<ItemInstanceRecord>.Failure(PersistenceFailures.InventoryIoFailed(correlationId));
            }
        }

        public Result<ItemInstanceRecord> GetItemInstance(CampaignHandle campaign, ItemInstanceId itemInstanceId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!itemInstanceId.IsValid) throw new ArgumentException("ItemInstanceId is required.", nameof(itemInstanceId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureInventoryTables(connection);
                ItemInstanceRecord? record = SelectItemInstance(connection, null, itemInstanceId.ToString());
                return record == null
                    ? Result<ItemInstanceRecord>.Failure(PersistenceFailures.ItemInstanceNotFound(correlationId))
                    : Result<ItemInstanceRecord>.Success(record);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<ItemInstanceRecord>.Failure(PersistenceFailures.InventoryIoFailed(correlationId));
            }
        }

        public Result<ItemInstanceRecord> MoveItemInstance(CampaignHandle campaign, InventoryMove move, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (move == null) throw new ArgumentNullException(nameof(move));
            return MoveItem(campaign, move, TargetItemInstance, move.Target.ItemInstanceId.ToString(), "ItemInstance", "ItemInstanceId", SelectItemInstance, r => r.InventoryId, r => r.LocationRef, r => r.Revision, correlationId);
        }

        public Result<InventoryCreateReplay<ItemInstanceRecord>> TryReplayCreateItemInstance(CampaignHandle campaign, CommandId commandId, ItemInstanceId itemInstanceId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (!itemInstanceId.IsValid) throw new ArgumentException("ItemInstanceId is required.", nameof(itemInstanceId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureInventoryTables(connection);
                return ProbeCreateReplay(connection, commandId, TargetItemInstance, itemInstanceId.ToString(), SelectItemInstance, correlationId);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<InventoryCreateReplay<ItemInstanceRecord>>.Failure(PersistenceFailures.InventoryIoFailed(correlationId));
            }
        }

        public Result<ItemStackRecord> CreateItemStack(CampaignHandle campaign, ItemStackRecord record, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (!TryValidateCampaignBoundary(campaign, record.CampaignId, correlationId, out Error campaignError))
            {
                return Result<ItemStackRecord>.Failure(campaignError);
            }

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureInventoryTables(connection);
                using SqliteTransaction transaction = connection.BeginTransaction();

                Result<ItemStackRecord>? replay = TryReplay(connection, transaction, commandId, TargetItemStack, record.ItemStackId.ToString(), SelectItemStack, correlationId);
                if (replay != null)
                {
                    transaction.Commit();
                    return replay.Value;
                }

                if (!InventoryExists(connection, transaction, record.CampaignId, record.InventoryId))
                {
                    transaction.Commit();
                    return Result<ItemStackRecord>.Failure(PersistenceFailures.InventoryNotFound(correlationId));
                }

                using (var insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = "INSERT INTO ItemStack (" +
                        "ItemStackId, CampaignId, InventoryId, OwnerKind, OwnerTargetRef, OwnerLocationKey, " +
                        "LocationKind, LocationTargetRef, LocationDetailRef, SourceItemDefinitionRef, " +
                        "MechanicsSourceDefinitionRef, MechanicsDefinitionSnapshotVersion, MechanicsContentType, MechanicsPayload, " +
                        "Quantity, StackState, Revision, CreatedAt, UpdatedAt) VALUES (" +
                        "$itemStackId, $campaignId, $inventoryId, $ownerKind, $ownerTargetRef, $ownerLocationKey, " +
                        "$locationKind, $locationTargetRef, $locationDetailRef, $sourceItemDefinitionRef, " +
                        "$mechanicsSourceDefinitionRef, $mechanicsDefinitionSnapshotVersion, $mechanicsContentType, $mechanicsPayload, " +
                        "$quantity, $stackState, $revision, $createdAt, $updatedAt);";
                    AddItemCommonParameters(insert, record.CampaignId, record.InventoryId, record.OwnerRef, record.LocationRef, record.SourceItemDefinitionRef, record.MechanicsSnapshot, record.Revision, record.CreatedAt, record.UpdatedAt);
                    insert.Parameters.AddWithValue("$itemStackId", record.ItemStackId.ToString());
                    insert.Parameters.AddWithValue("$quantity", record.Quantity.Value);
                    insert.Parameters.AddWithValue("$stackState", record.StackState);
                    insert.ExecuteNonQuery();
                }

                InsertLedgerEntry(connection, transaction, commandId, TargetItemStack, record.ItemStackId.ToString(), _clock.GetUtcNow());
                transaction.Commit();
                return Result<ItemStackRecord>.Success(record);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<ItemStackRecord>.Failure(PersistenceFailures.InventoryIoFailed(correlationId));
            }
        }

        public Result<ItemStackRecord> GetItemStack(CampaignHandle campaign, ItemStackId itemStackId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!itemStackId.IsValid) throw new ArgumentException("ItemStackId is required.", nameof(itemStackId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureInventoryTables(connection);
                ItemStackRecord? record = SelectItemStack(connection, null, itemStackId.ToString());
                return record == null
                    ? Result<ItemStackRecord>.Failure(PersistenceFailures.ItemStackNotFound(correlationId))
                    : Result<ItemStackRecord>.Success(record);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<ItemStackRecord>.Failure(PersistenceFailures.InventoryIoFailed(correlationId));
            }
        }

        public Result<ItemStackRecord> MoveItemStack(CampaignHandle campaign, InventoryMove move, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (move == null) throw new ArgumentNullException(nameof(move));
            return MoveItem(campaign, move, TargetItemStack, move.Target.ItemStackId.ToString(), "ItemStack", "ItemStackId", SelectItemStack, r => r.InventoryId, r => r.LocationRef, r => r.Revision, correlationId);
        }

        public Result<ItemStackRecord> SplitItemStack(CampaignHandle campaign, InventoryStackOperation operation, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (operation.IsMerge) throw new ArgumentException("SplitItemStack requires a split operation.", nameof(operation));
            return RunStackOperation(campaign, operation, correlationId);
        }

        public Result<ItemStackRecord> MergeItemStacks(CampaignHandle campaign, InventoryStackOperation operation, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (!operation.IsMerge) throw new ArgumentException("MergeItemStacks requires a merge operation.", nameof(operation));
            return RunStackOperation(campaign, operation, correlationId);
        }

        public Result<InventoryCreateReplay<ItemStackRecord>> TryReplayCreateItemStack(CampaignHandle campaign, CommandId commandId, ItemStackId itemStackId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (!itemStackId.IsValid) throw new ArgumentException("ItemStackId is required.", nameof(itemStackId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureInventoryTables(connection);
                return ProbeCreateReplay(connection, commandId, TargetItemStack, itemStackId.ToString(), SelectItemStack, correlationId);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<InventoryCreateReplay<ItemStackRecord>>.Failure(PersistenceFailures.InventoryIoFailed(correlationId));
            }
        }

        public Result<IReadOnlyList<ItemInstanceRecord>> ListItemInstances(CampaignHandle campaign, CampaignId campaignId, InventoryId inventoryId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!inventoryId.IsValid) throw new ArgumentException("InventoryId is required.", nameof(inventoryId));
            if (!TryValidateCampaignBoundary(campaign, campaignId, correlationId, out Error campaignError))
            {
                return Result<IReadOnlyList<ItemInstanceRecord>>.Failure(campaignError);
            }

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureInventoryTables(connection);
                var results = new List<ItemInstanceRecord>();
                using (var select = connection.CreateCommand())
                {
                    select.CommandText = ItemInstanceSelectColumns + " FROM ItemInstance WHERE CampaignId = $campaignId AND InventoryId = $inventoryId ORDER BY CreatedAt, ItemInstanceId;";
                    select.Parameters.AddWithValue("$campaignId", campaignId.ToString());
                    select.Parameters.AddWithValue("$inventoryId", inventoryId.ToString());
                    using SqliteDataReader reader = select.ExecuteReader();
                    while (reader.Read()) results.Add(ReadItemInstance(reader));
                }

                return Result<IReadOnlyList<ItemInstanceRecord>>.Success(results);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<IReadOnlyList<ItemInstanceRecord>>.Failure(PersistenceFailures.InventoryIoFailed(correlationId));
            }
        }

        public Result<IReadOnlyList<ItemStackRecord>> ListItemStacks(CampaignHandle campaign, CampaignId campaignId, InventoryId inventoryId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!inventoryId.IsValid) throw new ArgumentException("InventoryId is required.", nameof(inventoryId));
            if (!TryValidateCampaignBoundary(campaign, campaignId, correlationId, out Error campaignError))
            {
                return Result<IReadOnlyList<ItemStackRecord>>.Failure(campaignError);
            }

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureInventoryTables(connection);
                var results = new List<ItemStackRecord>();
                using (var select = connection.CreateCommand())
                {
                    select.CommandText = ItemStackSelectColumns + " FROM ItemStack WHERE CampaignId = $campaignId AND InventoryId = $inventoryId ORDER BY CreatedAt, ItemStackId;";
                    select.Parameters.AddWithValue("$campaignId", campaignId.ToString());
                    select.Parameters.AddWithValue("$inventoryId", inventoryId.ToString());
                    using SqliteDataReader reader = select.ExecuteReader();
                    while (reader.Read()) results.Add(ReadItemStack(reader));
                }

                return Result<IReadOnlyList<ItemStackRecord>>.Success(results);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<IReadOnlyList<ItemStackRecord>>.Failure(PersistenceFailures.InventoryIoFailed(correlationId));
            }
        }

        public Result<bool> HasAnyItemOwnedByCharacter(CampaignHandle campaign, CampaignId campaignId, CharacterId characterId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (!TryValidateCampaignBoundary(campaign, campaignId, correlationId, out Error campaignError))
            {
                return Result<bool>.Failure(campaignError);
            }

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureInventoryTables(connection);
                using var select = connection.CreateCommand();
                select.CommandText =
                    "SELECT 1 WHERE EXISTS (SELECT 1 FROM ItemInstance WHERE CampaignId = $campaignId AND OwnerKind = $ownerKind AND OwnerTargetRef = $characterId) " +
                    "OR EXISTS (SELECT 1 FROM ItemStack WHERE CampaignId = $campaignId AND OwnerKind = $ownerKind AND OwnerTargetRef = $characterId);";
                select.Parameters.AddWithValue("$campaignId", campaignId.ToString());
                select.Parameters.AddWithValue("$ownerKind", InventoryOwnerKind.Character.ToString());
                select.Parameters.AddWithValue("$characterId", characterId.ToString());
                return Result<bool>.Success(select.ExecuteScalar() != null);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<bool>.Failure(PersistenceFailures.InventoryIoFailed(correlationId));
            }
        }

        public Result<bool> HasAnyRuntimeReferenceToDefinition(CampaignHandle campaign, CampaignId campaignId, ContentDefinitionId definitionId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!definitionId.IsValid) throw new ArgumentException("ContentDefinitionId is required.", nameof(definitionId));
            if (!TryValidateCampaignBoundary(campaign, campaignId, correlationId, out Error campaignError))
            {
                return Result<bool>.Failure(campaignError);
            }

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureInventoryTables(connection);
                // SourceItemDefinitionRef is stored as ContentDefinitionRef.ToString()
                // "<definitionId>/<version>"; match any pinned version by the id
                // prefix. The canonical id contains '_' (a LIKE wildcard), so the
                // pattern is escaped.
                string pattern = EscapeLike(definitionId.ToString()) + "/%";
                using var select = connection.CreateCommand();
                select.CommandText =
                    "SELECT 1 WHERE EXISTS (SELECT 1 FROM ItemInstance WHERE CampaignId = $campaignId AND SourceItemDefinitionRef LIKE $pattern ESCAPE '\\') " +
                    "OR EXISTS (SELECT 1 FROM ItemStack WHERE CampaignId = $campaignId AND SourceItemDefinitionRef LIKE $pattern ESCAPE '\\');";
                select.Parameters.AddWithValue("$campaignId", campaignId.ToString());
                select.Parameters.AddWithValue("$pattern", pattern);
                return Result<bool>.Success(select.ExecuteScalar() != null);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<bool>.Failure(PersistenceFailures.InventoryIoFailed(correlationId));
            }
        }

        private static string EscapeLike(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
        }

        private static bool TryValidateCampaignBoundary(CampaignHandle campaign, CampaignId campaignId, CorrelationId correlationId, out Error error)
        {
            if (campaign.CampaignId.Equals(campaignId))
            {
                error = default!;
                return true;
            }

            error = PersistenceFailures.InventoryCampaignMismatch(correlationId);
            return false;
        }

        private Result<T> MoveItem<T>(CampaignHandle campaign, InventoryMove move, string targetKind, string targetId, string table, string idColumn, Func<SqliteConnection, SqliteTransaction?, string, T?> select, Func<T, InventoryId> inventoryOf, Func<T, InventoryLocationRef> locationOf, Func<T, long> revisionOf, CorrelationId correlationId) where T : class
        {
            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureInventoryTables(connection);
                using SqliteTransaction transaction = connection.BeginTransaction();
                if (CommandExists(connection, transaction, "InventoryCommandLedger", move.CommandId))
                {
                    transaction.Commit(); return Result<T>.Failure(PersistenceFailures.InventoryCommandIdentityMismatch(correlationId));
                }
                Result<T>? replay = TryMoveReplay(connection, transaction, move, targetKind, targetId, select, correlationId);
                if (replay != null) { transaction.Commit(); return replay.Value; }
                T? target = select(connection, transaction, targetId);
                InventoryRecord? source = SelectInventory(connection, transaction, move.SourceInventoryId.ToString());
                InventoryRecord? destination = SelectInventory(connection, transaction, move.DestinationInventoryId.ToString());
                if (target == null) { transaction.Commit(); return Result<T>.Failure(targetKind == TargetItemInstance ? PersistenceFailures.ItemInstanceNotFound(correlationId) : PersistenceFailures.ItemStackNotFound(correlationId)); }
                if (source == null || destination == null) { transaction.Commit(); return Result<T>.Failure(PersistenceFailures.InventoryNotFound(correlationId)); }
                if (!inventoryOf(target).Equals(move.SourceInventoryId) || locationOf(target).Kind != InventoryLocationKind.Contained) { transaction.Commit(); return Result<T>.Failure(InventoryMovementFailures.SourceInvalid(correlationId)); }
                if (revisionOf(target) != move.ExpectedTargetRevision) { transaction.Commit(); return Result<T>.Failure(InventoryMovementFailures.ItemRevisionConflict(correlationId)); }
                if (source.Revision != move.ExpectedSourceRevision || destination.Revision != move.ExpectedDestinationRevision) { transaction.Commit(); return Result<T>.Failure(InventoryMovementFailures.InventoryRevisionConflict(correlationId)); }
                if (move.SourceInventoryId.Equals(move.DestinationInventoryId) && locationOf(target).DetailRef == move.DestinationContainerKey) { transaction.Commit(); return Result<T>.Failure(InventoryMovementFailures.DestinationUnchanged(correlationId)); }
                UtcInstant now = _clock.GetUtcNow();
                if (!TryUpdateInventoryForMove(connection, transaction, source.InventoryId, move.ExpectedSourceRevision, now))
                {
                    transaction.Rollback();
                    return Result<T>.Failure(InventoryMovementFailures.InventoryRevisionConflict(correlationId));
                }

                if (!source.InventoryId.Equals(destination.InventoryId) && !TryUpdateInventoryForMove(connection, transaction, destination.InventoryId, move.ExpectedDestinationRevision, now))
                {
                    transaction.Rollback();
                    return Result<T>.Failure(InventoryMovementFailures.InventoryRevisionConflict(correlationId));
                }

                using (var update = connection.CreateCommand())
                {
                    update.Transaction = transaction;
                    update.CommandText = "UPDATE " + table + " SET InventoryId=$inventoryId,OwnerKind=$ownerKind,OwnerTargetRef=$ownerTargetRef,OwnerLocationKey=$ownerLocationKey,LocationKind=$locationKind,LocationTargetRef=$locationTargetRef,LocationDetailRef=$locationDetailRef,Revision=Revision+1,UpdatedAt=$updatedAt WHERE " + idColumn + "=$id AND InventoryId=$sourceInventoryId AND Revision=$expectedRevision;";
                    update.Parameters.AddWithValue("$inventoryId", destination.InventoryId.ToString()); update.Parameters.AddWithValue("$ownerKind", destination.OwnerRef.Kind.ToString()); update.Parameters.AddWithValue("$ownerTargetRef", destination.OwnerRef.TargetRef); update.Parameters.AddWithValue("$ownerLocationKey", (object?)destination.OwnerRef.LocationKey ?? DBNull.Value); update.Parameters.AddWithValue("$locationKind", InventoryLocationKind.Contained.ToString()); update.Parameters.AddWithValue("$locationTargetRef", destination.InventoryId.ToString()); update.Parameters.AddWithValue("$locationDetailRef", move.DestinationContainerKey); update.Parameters.AddWithValue("$updatedAt", now.ToString()); update.Parameters.AddWithValue("$id", targetId);
                    update.Parameters.AddWithValue("$sourceInventoryId", move.SourceInventoryId.ToString());
                    update.Parameters.AddWithValue("$expectedRevision", move.ExpectedTargetRevision);
                    if (update.ExecuteNonQuery() != 1)
                    {
                        transaction.Rollback();
                        return Result<T>.Failure(InventoryMovementFailures.ItemRevisionConflict(correlationId));
                    }
                }
                InsertMoveLedger(connection, transaction, move, targetKind, targetId, now);
                T? moved = select(connection, transaction, targetId); transaction.Commit(); return moved == null ? Result<T>.Failure(PersistenceFailures.CommandReplayFailed(correlationId)) : Result<T>.Success(moved);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException) { return Result<T>.Failure(PersistenceFailures.InventoryIoFailed(correlationId)); }
        }

        private static bool CommandExists(SqliteConnection c, SqliteTransaction t, string table, CommandId id) { using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = "SELECT 1 FROM " + table + " WHERE CommandId=$id LIMIT 1;"; q.Parameters.AddWithValue("$id", id.ToString()); return q.ExecuteScalar() != null; }
        private static Result<T>? TryMoveReplay<T>(SqliteConnection c, SqliteTransaction t, InventoryMove m, string kind, string target, Func<SqliteConnection, SqliteTransaction?, string, T?> select, CorrelationId id) where T : class { using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = "SELECT TargetKind,TargetId,SourceInventoryId,DestinationInventoryId,DestinationContainerKey,TargetRevision,SourceRevision,DestinationRevision FROM InventoryMoveCommandLedger WHERE CommandId=$id;"; q.Parameters.AddWithValue("$id", m.CommandId.ToString()); using var r = q.ExecuteReader(); if (!r.Read()) return null; bool same = r.GetString(0) == kind && r.GetString(1) == target && r.GetString(2) == m.SourceInventoryId.ToString() && r.GetString(3) == m.DestinationInventoryId.ToString() && r.GetString(4) == m.DestinationContainerKey && r.GetInt64(5) == m.ExpectedTargetRevision && r.GetInt64(6) == m.ExpectedSourceRevision && r.GetInt64(7) == m.ExpectedDestinationRevision; return !same ? Result<T>.Failure(PersistenceFailures.InventoryCommandIdentityMismatch(id)) : select(c, t, target) is T record ? Result<T>.Success(record) : Result<T>.Failure(PersistenceFailures.CommandReplayFailed(id)); }
        private static bool TryUpdateInventoryForMove(SqliteConnection c, SqliteTransaction t, InventoryId id, long expectedRevision, UtcInstant now) { using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = "UPDATE Inventory SET Revision=$newRevision,UpdatedAt=$updatedAt WHERE InventoryId=$inventoryId AND Revision=$expectedRevision;"; q.Parameters.AddWithValue("$newRevision", expectedRevision + 1); q.Parameters.AddWithValue("$updatedAt", now.ToString()); q.Parameters.AddWithValue("$inventoryId", id.ToString()); q.Parameters.AddWithValue("$expectedRevision", expectedRevision); return q.ExecuteNonQuery() == 1; }
        private static void InsertMoveLedger(SqliteConnection c, SqliteTransaction t, InventoryMove m, string kind, string target, UtcInstant now) { using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = "INSERT INTO InventoryMoveCommandLedger (CommandId,TargetKind,TargetId,SourceInventoryId,DestinationInventoryId,DestinationContainerKey,TargetRevision,SourceRevision,DestinationRevision,CreatedAt) VALUES ($commandId,$kind,$target,$source,$destination,$key,$targetRevision,$sourceRevision,$destinationRevision,$now);"; q.Parameters.AddWithValue("$commandId", m.CommandId.ToString()); q.Parameters.AddWithValue("$kind", kind); q.Parameters.AddWithValue("$target", target); q.Parameters.AddWithValue("$source", m.SourceInventoryId.ToString()); q.Parameters.AddWithValue("$destination", m.DestinationInventoryId.ToString()); q.Parameters.AddWithValue("$key", m.DestinationContainerKey); q.Parameters.AddWithValue("$targetRevision", m.ExpectedTargetRevision); q.Parameters.AddWithValue("$sourceRevision", m.ExpectedSourceRevision); q.Parameters.AddWithValue("$destinationRevision", m.ExpectedDestinationRevision); q.Parameters.AddWithValue("$now", now.ToString()); q.ExecuteNonQuery(); }

        // ODY-S05-205: MainGM-only atomic stack split/merge. One CAS-protected SQLite
        // transaction per call; a dedicated InventoryStackCommandLedger carries replay
        // identity without touching creation/movement ledger semantics.
        private const string OperationKindSplit = "Split";
        private const string OperationKindMerge = "Merge";

        private Result<ItemStackRecord> RunStackOperation(CampaignHandle campaign, InventoryStackOperation operation, CorrelationId correlationId)
        {
            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureInventoryTables(connection);
                using SqliteTransaction transaction = connection.BeginTransaction();

                if (CommandExists(connection, transaction, "InventoryCommandLedger", operation.CommandId))
                {
                    transaction.Commit();
                    return Result<ItemStackRecord>.Failure(PersistenceFailures.InventoryCommandIdentityMismatch(correlationId));
                }

                Result<ItemStackRecord>? replay = TryStackReplay(connection, transaction, operation, correlationId);
                if (replay != null)
                {
                    transaction.Commit();
                    return replay.Value;
                }

                return operation.IsMerge
                    ? MergeInTransaction(connection, transaction, operation, correlationId)
                    : SplitInTransaction(connection, transaction, operation, correlationId);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<ItemStackRecord>.Failure(PersistenceFailures.InventoryIoFailed(correlationId));
            }
        }

        private Result<ItemStackRecord> SplitInTransaction(SqliteConnection connection, SqliteTransaction transaction, InventoryStackOperation operation, CorrelationId correlationId)
        {
            ItemStackRecord? source = SelectItemStack(connection, transaction, operation.SourceId.ToString());
            if (source == null)
            {
                transaction.Commit();
                return Result<ItemStackRecord>.Failure(PersistenceFailures.ItemStackNotFound(correlationId));
            }

            if (!source.InventoryId.Equals(operation.InventoryId) || source.LocationRef.Kind != InventoryLocationKind.Contained)
            {
                transaction.Commit();
                return Result<ItemStackRecord>.Failure(InventoryMovementFailures.SourceInvalid(correlationId));
            }

            if (source.Revision != operation.ExpectedSourceRevision)
            {
                transaction.Commit();
                return Result<ItemStackRecord>.Failure(InventoryMovementFailures.ItemRevisionConflict(correlationId));
            }

            InventoryRecord? inventory = SelectInventory(connection, transaction, operation.InventoryId.ToString());
            if (inventory == null)
            {
                transaction.Commit();
                return Result<ItemStackRecord>.Failure(PersistenceFailures.InventoryNotFound(correlationId));
            }

            if (inventory.Revision != operation.ExpectedInventoryRevision)
            {
                transaction.Commit();
                return Result<ItemStackRecord>.Failure(InventoryMovementFailures.InventoryRevisionConflict(correlationId));
            }

            // Split must take a positive proper subset: 0 < quantity < source quantity.
            // The lower bound is guaranteed by InventoryStackOperation's constructor.
            if (operation.Quantity >= source.Quantity.Value)
            {
                transaction.Commit();
                return Result<ItemStackRecord>.Failure(InventoryStackFailures.SplitQuantityInvalid(correlationId));
            }

            UtcInstant now = _clock.GetUtcNow();
            long remainingSourceQuantity = source.Quantity.Value - operation.Quantity;

            using (var update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = "UPDATE ItemStack SET Quantity=$quantity,Revision=Revision+1,UpdatedAt=$updatedAt WHERE ItemStackId=$id AND Revision=$expectedRevision;";
                update.Parameters.AddWithValue("$quantity", remainingSourceQuantity);
                update.Parameters.AddWithValue("$updatedAt", now.ToString());
                update.Parameters.AddWithValue("$id", operation.SourceId.ToString());
                update.Parameters.AddWithValue("$expectedRevision", operation.ExpectedSourceRevision);
                if (update.ExecuteNonQuery() != 1)
                {
                    transaction.Rollback();
                    return Result<ItemStackRecord>.Failure(InventoryMovementFailures.ItemRevisionConflict(correlationId));
                }
            }

            using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = "INSERT INTO ItemStack (" +
                    "ItemStackId, CampaignId, InventoryId, OwnerKind, OwnerTargetRef, OwnerLocationKey, " +
                    "LocationKind, LocationTargetRef, LocationDetailRef, SourceItemDefinitionRef, " +
                    "MechanicsSourceDefinitionRef, MechanicsDefinitionSnapshotVersion, MechanicsContentType, MechanicsPayload, " +
                    "Quantity, StackState, Revision, CreatedAt, UpdatedAt) VALUES (" +
                    "$itemStackId, $campaignId, $inventoryId, $ownerKind, $ownerTargetRef, $ownerLocationKey, " +
                    "$locationKind, $locationTargetRef, $locationDetailRef, $sourceItemDefinitionRef, " +
                    "$mechanicsSourceDefinitionRef, $mechanicsDefinitionSnapshotVersion, $mechanicsContentType, $mechanicsPayload, " +
                    "$quantity, $stackState, $revision, $createdAt, $updatedAt);";
                AddItemCommonParameters(insert, source.CampaignId, source.InventoryId, source.OwnerRef, source.LocationRef, source.SourceItemDefinitionRef, source.MechanicsSnapshot, 1, now, now);
                insert.Parameters.AddWithValue("$itemStackId", operation.ResultId.ToString());
                insert.Parameters.AddWithValue("$quantity", operation.Quantity);
                insert.Parameters.AddWithValue("$stackState", source.StackState);
                insert.ExecuteNonQuery();
            }

            InsertStackLedger(connection, transaction, operation, OperationKindSplit, operation.ResultId, now);
            ItemStackRecord? created = SelectItemStack(connection, transaction, operation.ResultId.ToString());
            transaction.Commit();
            return created == null
                ? Result<ItemStackRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId))
                : Result<ItemStackRecord>.Success(created);
        }

        private Result<ItemStackRecord> MergeInTransaction(SqliteConnection connection, SqliteTransaction transaction, InventoryStackOperation operation, CorrelationId correlationId)
        {
            ItemStackRecord? source = SelectItemStack(connection, transaction, operation.SourceId.ToString());
            ItemStackRecord? destination = SelectItemStack(connection, transaction, operation.ResultId.ToString());
            if (source == null || destination == null)
            {
                transaction.Commit();
                return Result<ItemStackRecord>.Failure(PersistenceFailures.ItemStackNotFound(correlationId));
            }

            if (source.Revision != operation.ExpectedSourceRevision || destination.Revision != operation.ExpectedResultRevision)
            {
                transaction.Commit();
                return Result<ItemStackRecord>.Failure(InventoryMovementFailures.ItemRevisionConflict(correlationId));
            }

            InventoryRecord? inventory = SelectInventory(connection, transaction, operation.InventoryId.ToString());
            if (inventory == null)
            {
                transaction.Commit();
                return Result<ItemStackRecord>.Failure(PersistenceFailures.InventoryNotFound(correlationId));
            }

            if (inventory.Revision != operation.ExpectedInventoryRevision)
            {
                transaction.Commit();
                return Result<ItemStackRecord>.Failure(InventoryMovementFailures.InventoryRevisionConflict(correlationId));
            }

            if (source.LocationRef.Kind != InventoryLocationKind.Contained || destination.LocationRef.Kind != InventoryLocationKind.Contained)
            {
                transaction.Commit();
                return Result<ItemStackRecord>.Failure(InventoryMovementFailures.SourceInvalid(correlationId));
            }

            // ADR-027 section 6.2: merge only mechanically identical stacks -- exact
            // definition ref, stored mechanics snapshot, stack runtime state, owner,
            // inventory, and contained location. Stored snapshots, never the current
            // catalog definition, decide this.
            bool mechanicallyIdentical =
                source.InventoryId.Equals(operation.InventoryId) &&
                destination.InventoryId.Equals(operation.InventoryId) &&
                source.SourceItemDefinitionRef.Equals(destination.SourceItemDefinitionRef) &&
                source.MechanicsSnapshot.Equals(destination.MechanicsSnapshot) &&
                string.Equals(source.StackState, destination.StackState, StringComparison.Ordinal) &&
                source.OwnerRef.Equals(destination.OwnerRef) &&
                source.LocationRef.Equals(destination.LocationRef);
            if (!mechanicallyIdentical)
            {
                transaction.Commit();
                return Result<ItemStackRecord>.Failure(InventoryStackFailures.MergeMismatch(correlationId));
            }

            // Stored snapshots carry no decodable max-stack-size at this layer (the
            // mechanics payload is opaque here by ODY-S05-202/203 design), so the only
            // hard ceiling enforced is long-range representability. Catalog-defined
            // MaxStackSize enforcement is a documented follow-up, see the task contract.
            if (source.Quantity.Value > long.MaxValue - destination.Quantity.Value)
            {
                transaction.Commit();
                return Result<ItemStackRecord>.Failure(InventoryStackFailures.MergeExceedsMaxQuantity(correlationId));
            }

            long mergedQuantity = destination.Quantity.Value + source.Quantity.Value;
            UtcInstant now = _clock.GetUtcNow();

            using (var update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = "UPDATE ItemStack SET Quantity=$quantity,Revision=Revision+1,UpdatedAt=$updatedAt WHERE ItemStackId=$id AND Revision=$expectedRevision;";
                update.Parameters.AddWithValue("$quantity", mergedQuantity);
                update.Parameters.AddWithValue("$updatedAt", now.ToString());
                update.Parameters.AddWithValue("$id", operation.ResultId.ToString());
                update.Parameters.AddWithValue("$expectedRevision", operation.ExpectedResultRevision);
                if (update.ExecuteNonQuery() != 1)
                {
                    transaction.Rollback();
                    return Result<ItemStackRecord>.Failure(InventoryMovementFailures.ItemRevisionConflict(correlationId));
                }
            }

            using (var delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM ItemStack WHERE ItemStackId=$id AND Revision=$expectedRevision;";
                delete.Parameters.AddWithValue("$id", operation.SourceId.ToString());
                delete.Parameters.AddWithValue("$expectedRevision", operation.ExpectedSourceRevision);
                if (delete.ExecuteNonQuery() != 1)
                {
                    transaction.Rollback();
                    return Result<ItemStackRecord>.Failure(InventoryMovementFailures.ItemRevisionConflict(correlationId));
                }
            }

            InsertStackLedger(connection, transaction, operation, OperationKindMerge, operation.ResultId, now);
            ItemStackRecord? survivor = SelectItemStack(connection, transaction, operation.ResultId.ToString());
            transaction.Commit();
            return survivor == null
                ? Result<ItemStackRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId))
                : Result<ItemStackRecord>.Success(survivor);
        }

        private static Result<ItemStackRecord>? TryStackReplay(SqliteConnection connection, SqliteTransaction transaction, InventoryStackOperation operation, CorrelationId correlationId)
        {
            using var lookup = connection.CreateCommand();
            lookup.Transaction = transaction;
            lookup.CommandText = "SELECT OperationKind,SourceId,ResultId,InventoryId,Quantity,ExpectedSourceRevision,ExpectedResultRevision,ExpectedInventoryRevision,ResultStackId FROM InventoryStackCommandLedger WHERE CommandId=$commandId;";
            lookup.Parameters.AddWithValue("$commandId", operation.CommandId.ToString());
            string resultStackId;
            using (SqliteDataReader reader = lookup.ExecuteReader())
            {
                if (!reader.Read()) return null;
                bool same =
                    reader.GetString(0) == (operation.IsMerge ? OperationKindMerge : OperationKindSplit) &&
                    reader.GetString(1) == operation.SourceId.ToString() &&
                    reader.GetString(2) == operation.ResultId.ToString() &&
                    reader.GetString(3) == operation.InventoryId.ToString() &&
                    reader.GetInt64(4) == operation.Quantity &&
                    reader.GetInt64(5) == operation.ExpectedSourceRevision &&
                    reader.GetInt64(6) == operation.ExpectedResultRevision &&
                    reader.GetInt64(7) == operation.ExpectedInventoryRevision;
                if (!same) return Result<ItemStackRecord>.Failure(PersistenceFailures.InventoryCommandIdentityMismatch(correlationId));
                resultStackId = reader.GetString(8);
            }

            ItemStackRecord? record = SelectItemStack(connection, transaction, resultStackId);
            return record == null
                ? Result<ItemStackRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId))
                : Result<ItemStackRecord>.Success(record);
        }

        private static void InsertStackLedger(SqliteConnection connection, SqliteTransaction transaction, InventoryStackOperation operation, string operationKind, ItemStackId resultStackId, UtcInstant now)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO InventoryStackCommandLedger (CommandId,OperationKind,SourceId,ResultId,InventoryId,Quantity,ExpectedSourceRevision,ExpectedResultRevision,ExpectedInventoryRevision,ResultStackId,CreatedAt) VALUES ($commandId,$operationKind,$sourceId,$resultId,$inventoryId,$quantity,$expectedSourceRevision,$expectedResultRevision,$expectedInventoryRevision,$resultStackId,$createdAt);";
            insert.Parameters.AddWithValue("$commandId", operation.CommandId.ToString());
            insert.Parameters.AddWithValue("$operationKind", operationKind);
            insert.Parameters.AddWithValue("$sourceId", operation.SourceId.ToString());
            insert.Parameters.AddWithValue("$resultId", operation.ResultId.ToString());
            insert.Parameters.AddWithValue("$inventoryId", operation.InventoryId.ToString());
            insert.Parameters.AddWithValue("$quantity", operation.Quantity);
            insert.Parameters.AddWithValue("$expectedSourceRevision", operation.ExpectedSourceRevision);
            insert.Parameters.AddWithValue("$expectedResultRevision", operation.ExpectedResultRevision);
            insert.Parameters.AddWithValue("$expectedInventoryRevision", operation.ExpectedInventoryRevision);
            insert.Parameters.AddWithValue("$resultStackId", resultStackId.ToString());
            insert.Parameters.AddWithValue("$createdAt", now.ToString());
            insert.ExecuteNonQuery();
        }

        // ODY-S05-302: Equipment persistence foundation. `EquippedEntry` rows are
        // keyed by the item's own canonical id (ItemInstanceId/ItemStackId use
        // disjoint prefixes, so one column is a safe cross-kind identity key),
        // physically enforcing rule 1 ("one item is in exactly one place"). A
        // dedicated EquipmentCommandLedger carries idempotency for Create and
        // CAS-replay identity for Replace/Delete. No Equip/Unequip business
        // rule, authorization check, or rule-4 body-part-existence check is
        // implemented here -- ODY-S05-303/304 own those.
        private const string EquipmentOperationCreate = "Create";
        private const string EquipmentOperationReplace = "Replace";
        private const string EquipmentOperationDelete = "Delete";

        public Result<EquippedEntryRecord> CreateEquippedEntry(CampaignHandle campaign, EquippedEntryRecord record, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (!TryValidateCampaignBoundary(campaign, record.CampaignId, correlationId, out Error campaignError))
            {
                return Result<EquippedEntryRecord>.Failure(campaignError);
            }

            string itemRefId = ItemRefIdOf(record.Entry.ItemRef);

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureInventoryTables(connection);
                using SqliteTransaction transaction = connection.BeginTransaction();

                bool ledgerExists = TryReadEquipmentLedger(connection, transaction, commandId, EquipmentOperationCreate, itemRefId, 0, out bool identityMismatch);
                if (ledgerExists)
                {
                    transaction.Commit();
                    if (identityMismatch)
                    {
                        return Result<EquippedEntryRecord>.Failure(PersistenceFailures.InventoryCommandIdentityMismatch(correlationId));
                    }

                    EquippedEntryRecord? replayed = SelectEquippedEntry(connection, null, itemRefId, record.CampaignId);
                    return replayed == null
                        ? Result<EquippedEntryRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId))
                        : Result<EquippedEntryRecord>.Success(replayed);
                }

                if (!InventoryExists(connection, transaction, record.CampaignId, record.Entry.InventoryId))
                {
                    transaction.Commit();
                    return Result<EquippedEntryRecord>.Failure(PersistenceFailures.InventoryNotFound(correlationId));
                }

                if (SelectEquippedEntry(connection, transaction, itemRefId, record.CampaignId) != null)
                {
                    transaction.Commit();
                    return Result<EquippedEntryRecord>.Failure(PersistenceFailures.EquipmentEntryAlreadyEquipped(correlationId));
                }

                using (var insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = "INSERT INTO EquippedEntry (" +
                        "ItemRefId, ItemRefKind, CampaignId, InventoryId, EquipmentSlotRef, BodyPartRefs, " +
                        "EquippedByUserId, EquippedAt, Revision, CreatedAt, UpdatedAt) VALUES (" +
                        "$itemRefId, $itemRefKind, $campaignId, $inventoryId, $equipmentSlotRef, $bodyPartRefs, " +
                        "$equippedByUserId, $equippedAt, $revision, $createdAt, $updatedAt);";
                    AddEquippedEntryParameters(insert, itemRefId, record.CampaignId, record.Entry, record.Entry.EquippedAt, record.Entry.EquippedAt);
                    insert.ExecuteNonQuery();
                }

                InsertEquipmentLedger(connection, transaction, commandId, EquipmentOperationCreate, itemRefId, 0, _clock.GetUtcNow());
                transaction.Commit();
                return Result<EquippedEntryRecord>.Success(record);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<EquippedEntryRecord>.Failure(PersistenceFailures.InventoryIoFailed(correlationId));
            }
        }

        public Result<EquippedEntryRecord> GetEquippedEntry(CampaignHandle campaign, InventoryItemRef itemRef, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!itemRef.IsValid) throw new ArgumentException("ItemRef is required.", nameof(itemRef));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureInventoryTables(connection);
                EquippedEntryRecord? record = SelectEquippedEntry(connection, null, ItemRefIdOf(itemRef), null);
                return record == null
                    ? Result<EquippedEntryRecord>.Failure(PersistenceFailures.EquipmentEntryNotFound(correlationId))
                    : Result<EquippedEntryRecord>.Success(record);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<EquippedEntryRecord>.Failure(PersistenceFailures.InventoryIoFailed(correlationId));
            }
        }

        public Result<EquippedEntryRecord> ReplaceEquippedEntry(CampaignHandle campaign, EquippedEntryRecord record, long expectedRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (expectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            if (!TryValidateCampaignBoundary(campaign, record.CampaignId, correlationId, out Error campaignError))
            {
                return Result<EquippedEntryRecord>.Failure(campaignError);
            }

            string itemRefId = ItemRefIdOf(record.Entry.ItemRef);

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureInventoryTables(connection);
                using SqliteTransaction transaction = connection.BeginTransaction();

                bool ledgerExists = TryReadEquipmentLedger(connection, transaction, commandId, EquipmentOperationReplace, itemRefId, expectedRevision, out bool identityMismatch);
                if (ledgerExists)
                {
                    transaction.Commit();
                    if (identityMismatch)
                    {
                        return Result<EquippedEntryRecord>.Failure(PersistenceFailures.InventoryCommandIdentityMismatch(correlationId));
                    }

                    EquippedEntryRecord? replayed = SelectEquippedEntry(connection, null, itemRefId, record.CampaignId);
                    return replayed == null
                        ? Result<EquippedEntryRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId))
                        : Result<EquippedEntryRecord>.Success(replayed);
                }

                EquippedEntryRecord? current = SelectEquippedEntry(connection, transaction, itemRefId, record.CampaignId);
                if (current == null)
                {
                    transaction.Commit();
                    return Result<EquippedEntryRecord>.Failure(PersistenceFailures.EquipmentEntryNotFound(correlationId));
                }

                if (!current.Entry.InventoryId.Equals(record.Entry.InventoryId))
                {
                    throw new ArgumentException("ReplaceEquippedEntry cannot change the equipped item's InventoryId.", nameof(record));
                }

                if (current.Entry.Revision != expectedRevision)
                {
                    transaction.Commit();
                    return Result<EquippedEntryRecord>.Failure(PersistenceFailures.EquipmentEntryRevisionConflict(correlationId));
                }

                UtcInstant now = _clock.GetUtcNow();
                using (var update = connection.CreateCommand())
                {
                    update.Transaction = transaction;
                    update.CommandText = "UPDATE EquippedEntry SET EquipmentSlotRef=$equipmentSlotRef, BodyPartRefs=$bodyPartRefs, " +
                        "EquippedByUserId=$equippedByUserId, EquippedAt=$equippedAt, Revision=Revision+1, UpdatedAt=$updatedAt " +
                        "WHERE ItemRefId=$itemRefId AND Revision=$expectedRevision;";
                    update.Parameters.AddWithValue("$equipmentSlotRef", record.Entry.EquipmentSlotRef);
                    update.Parameters.AddWithValue("$bodyPartRefs", JoinBodyPartRefs(record.Entry.BodyPartRefs));
                    update.Parameters.AddWithValue("$equippedByUserId", record.Entry.EquippedByUserId.ToString());
                    update.Parameters.AddWithValue("$equippedAt", record.Entry.EquippedAt.ToString());
                    update.Parameters.AddWithValue("$updatedAt", now.ToString());
                    update.Parameters.AddWithValue("$itemRefId", itemRefId);
                    update.Parameters.AddWithValue("$expectedRevision", expectedRevision);
                    if (update.ExecuteNonQuery() != 1)
                    {
                        transaction.Rollback();
                        return Result<EquippedEntryRecord>.Failure(PersistenceFailures.EquipmentEntryRevisionConflict(correlationId));
                    }
                }

                InsertEquipmentLedger(connection, transaction, commandId, EquipmentOperationReplace, itemRefId, expectedRevision, now);
                EquippedEntryRecord? updated = SelectEquippedEntry(connection, transaction, itemRefId, record.CampaignId);
                transaction.Commit();
                return updated == null
                    ? Result<EquippedEntryRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId))
                    : Result<EquippedEntryRecord>.Success(updated);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<EquippedEntryRecord>.Failure(PersistenceFailures.InventoryIoFailed(correlationId));
            }
        }

        public Result<bool> DeleteEquippedEntry(CampaignHandle campaign, InventoryItemRef itemRef, long expectedRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!itemRef.IsValid) throw new ArgumentException("ItemRef is required.", nameof(itemRef));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (expectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedRevision));

            string itemRefId = ItemRefIdOf(itemRef);

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureInventoryTables(connection);
                using SqliteTransaction transaction = connection.BeginTransaction();

                bool ledgerExists = TryReadEquipmentLedger(connection, transaction, commandId, EquipmentOperationDelete, itemRefId, expectedRevision, out bool identityMismatch);
                if (ledgerExists)
                {
                    transaction.Commit();
                    return identityMismatch
                        ? Result<bool>.Failure(PersistenceFailures.InventoryCommandIdentityMismatch(correlationId))
                        : Result<bool>.Success(true);
                }

                EquippedEntryRecord? current = SelectEquippedEntry(connection, transaction, itemRefId, null);
                if (current == null)
                {
                    transaction.Commit();
                    return Result<bool>.Failure(PersistenceFailures.EquipmentEntryNotFound(correlationId));
                }

                if (current.Entry.Revision != expectedRevision)
                {
                    transaction.Commit();
                    return Result<bool>.Failure(PersistenceFailures.EquipmentEntryRevisionConflict(correlationId));
                }

                using (var delete = connection.CreateCommand())
                {
                    delete.Transaction = transaction;
                    delete.CommandText = "DELETE FROM EquippedEntry WHERE ItemRefId=$itemRefId AND Revision=$expectedRevision;";
                    delete.Parameters.AddWithValue("$itemRefId", itemRefId);
                    delete.Parameters.AddWithValue("$expectedRevision", expectedRevision);
                    if (delete.ExecuteNonQuery() != 1)
                    {
                        transaction.Rollback();
                        return Result<bool>.Failure(PersistenceFailures.EquipmentEntryRevisionConflict(correlationId));
                    }
                }

                InsertEquipmentLedger(connection, transaction, commandId, EquipmentOperationDelete, itemRefId, expectedRevision, _clock.GetUtcNow());
                transaction.Commit();
                return Result<bool>.Success(true);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<bool>.Failure(PersistenceFailures.InventoryIoFailed(correlationId));
            }
        }

        public Result<IReadOnlyList<EquippedEntryRecord>> ListEquippedEntries(CampaignHandle campaign, CampaignId campaignId, InventoryId inventoryId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!inventoryId.IsValid) throw new ArgumentException("InventoryId is required.", nameof(inventoryId));
            if (!TryValidateCampaignBoundary(campaign, campaignId, correlationId, out Error campaignError))
            {
                return Result<IReadOnlyList<EquippedEntryRecord>>.Failure(campaignError);
            }

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureInventoryTables(connection);

                var results = new List<EquippedEntryRecord>();
                using (var select = connection.CreateCommand())
                {
                    select.CommandText = EquippedEntrySelectColumns + " FROM EquippedEntry WHERE CampaignId = $campaignId AND InventoryId = $inventoryId;";
                    select.Parameters.AddWithValue("$campaignId", campaignId.ToString());
                    select.Parameters.AddWithValue("$inventoryId", inventoryId.ToString());
                    using SqliteDataReader reader = select.ExecuteReader();
                    while (reader.Read())
                    {
                        results.Add(ReadEquippedEntry(reader));
                    }
                }

                return Result<IReadOnlyList<EquippedEntryRecord>>.Success(results);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<IReadOnlyList<EquippedEntryRecord>>.Failure(PersistenceFailures.InventoryIoFailed(correlationId));
            }
        }

        // ODY-S05-303: the atomic Equip transition. Reuses EquipmentCommandLedger
        // (ODY-S05-302) with a new "Equip" operation kind, whose ExpectedRevision
        // column means the item's own expected revision for this operation kind --
        // the same column already carries a different meaning per operation kind
        // (0 for Create; the EquippedEntry's own revision for Replace/Delete).
        private const string EquipmentOperationEquip = "Equip";

        public Result<EquippedEntryRecord> EquipItem(CampaignHandle campaign, EquipTransition transition, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (transition == null) throw new ArgumentNullException(nameof(transition));
            if (!TryValidateCampaignBoundary(campaign, transition.Record.CampaignId, correlationId, out Error campaignError))
            {
                return Result<EquippedEntryRecord>.Failure(campaignError);
            }

            return transition.Record.Entry.ItemRef.Kind == InventoryItemRefKind.ItemInstance
                ? EquipItemCore(campaign, transition, TargetItemInstance, "ItemInstance", "ItemInstanceId", SelectItemInstance, r => r.CampaignId, r => r.InventoryId, r => r.LocationRef, r => r.Revision, correlationId)
                : EquipItemCore(campaign, transition, TargetItemStack, "ItemStack", "ItemStackId", SelectItemStack, r => r.CampaignId, r => r.InventoryId, r => r.LocationRef, r => r.Revision, correlationId);
        }

        private Result<EquippedEntryRecord> EquipItemCore<T>(
            CampaignHandle campaign,
            EquipTransition transition,
            string targetKind,
            string table,
            string idColumn,
            Func<SqliteConnection, SqliteTransaction?, string, T?> select,
            Func<T, CampaignId> campaignOf,
            Func<T, InventoryId> inventoryOf,
            Func<T, InventoryLocationRef> locationOf,
            Func<T, long> revisionOf,
            CorrelationId correlationId)
            where T : class
        {
            string itemRefId = ItemRefIdOf(transition.Record.Entry.ItemRef);
            InventoryId inventoryId = transition.Record.Entry.InventoryId;

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureInventoryTables(connection);
                using SqliteTransaction transaction = connection.BeginTransaction();

                bool ledgerExists = TryReadEquipmentLedger(connection, transaction, transition.CommandId, EquipmentOperationEquip, itemRefId, transition.ExpectedTargetRevision, out bool identityMismatch);
                if (ledgerExists)
                {
                    transaction.Commit();
                    if (identityMismatch)
                    {
                        return Result<EquippedEntryRecord>.Failure(PersistenceFailures.InventoryCommandIdentityMismatch(correlationId));
                    }

                    EquippedEntryRecord? replayed = SelectEquippedEntry(connection, null, itemRefId, transition.Record.CampaignId);
                    return replayed == null
                        ? Result<EquippedEntryRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId))
                        : Result<EquippedEntryRecord>.Success(replayed);
                }

                T? target = select(connection, transaction, itemRefId);
                if (target == null)
                {
                    transaction.Commit();
                    return Result<EquippedEntryRecord>.Failure(targetKind == TargetItemInstance ? PersistenceFailures.ItemInstanceNotFound(correlationId) : PersistenceFailures.ItemStackNotFound(correlationId));
                }

                if (!inventoryOf(target).Equals(inventoryId) || locationOf(target).Kind != InventoryLocationKind.Contained)
                {
                    transaction.Commit();
                    return Result<EquippedEntryRecord>.Failure(InventoryMovementFailures.SourceInvalid(correlationId));
                }

                if (revisionOf(target) != transition.ExpectedTargetRevision)
                {
                    transaction.Commit();
                    return Result<EquippedEntryRecord>.Failure(InventoryMovementFailures.ItemRevisionConflict(correlationId));
                }

                if (SelectEquippedEntry(connection, transaction, itemRefId, campaignOf(target)) != null)
                {
                    transaction.Commit();
                    return Result<EquippedEntryRecord>.Failure(PersistenceFailures.EquipmentEntryAlreadyEquipped(correlationId));
                }

                UtcInstant now = _clock.GetUtcNow();
                using (var update = connection.CreateCommand())
                {
                    update.Transaction = transaction;
                    update.CommandText = "UPDATE " + table + " SET LocationKind=$locationKind, LocationTargetRef=$locationTargetRef, LocationDetailRef=$locationDetailRef, Revision=Revision+1, UpdatedAt=$updatedAt WHERE " + idColumn + "=$id AND InventoryId=$inventoryId AND Revision=$expectedRevision;";
                    update.Parameters.AddWithValue("$locationKind", InventoryLocationKind.Equipped.ToString());
                    update.Parameters.AddWithValue("$locationTargetRef", inventoryId.ToString());
                    update.Parameters.AddWithValue("$locationDetailRef", transition.Record.Entry.EquipmentSlotRef);
                    update.Parameters.AddWithValue("$updatedAt", now.ToString());
                    update.Parameters.AddWithValue("$id", itemRefId);
                    update.Parameters.AddWithValue("$inventoryId", inventoryId.ToString());
                    update.Parameters.AddWithValue("$expectedRevision", transition.ExpectedTargetRevision);
                    if (update.ExecuteNonQuery() != 1)
                    {
                        transaction.Rollback();
                        return Result<EquippedEntryRecord>.Failure(InventoryMovementFailures.ItemRevisionConflict(correlationId));
                    }
                }

                using (var insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = "INSERT INTO EquippedEntry (" +
                        "ItemRefId, ItemRefKind, CampaignId, InventoryId, EquipmentSlotRef, BodyPartRefs, " +
                        "EquippedByUserId, EquippedAt, Revision, CreatedAt, UpdatedAt) VALUES (" +
                        "$itemRefId, $itemRefKind, $campaignId, $inventoryId, $equipmentSlotRef, $bodyPartRefs, " +
                        "$equippedByUserId, $equippedAt, $revision, $createdAt, $updatedAt);";
                    AddEquippedEntryParameters(insert, itemRefId, campaignOf(target), transition.Record.Entry, now, now);
                    insert.ExecuteNonQuery();
                }

                InsertEquipmentLedger(connection, transaction, transition.CommandId, EquipmentOperationEquip, itemRefId, transition.ExpectedTargetRevision, now);
                EquippedEntryRecord? created = SelectEquippedEntry(connection, transaction, itemRefId, campaignOf(target));
                transaction.Commit();
                return created == null
                    ? Result<EquippedEntryRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId))
                    : Result<EquippedEntryRecord>.Success(created);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<EquippedEntryRecord>.Failure(PersistenceFailures.InventoryIoFailed(correlationId));
            }
        }

        // ODY-S05-304: the atomic Unequip transition, the symmetric reverse of
        // EquipItemCore<T>. EquippedEntry is the authority for "is this item
        // equipped" (read and CAS-checked first); the item's own LocationRef is
        // then defensively cross-checked against it, not blindly trusted. Reuses
        // EquipmentCommandLedger with a new "Unequip" operation kind whose
        // ExpectedRevision means the EquippedEntry's own revision (matching
        // Replace/Delete's convention, not Equip's item-revision convention),
        // since EquippedEntry is the record being deleted and is this
        // direction's chosen identity anchor.
        private const string EquipmentOperationUnequip = "Unequip";

        public Result<bool> UnequipItem(CampaignHandle campaign, UnequipTransition transition, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (transition == null) throw new ArgumentNullException(nameof(transition));

            return transition.ItemRef.Kind == InventoryItemRefKind.ItemInstance
                ? UnequipItemCore(campaign, transition, TargetItemInstance, "ItemInstance", "ItemInstanceId", SelectItemInstance, r => r.InventoryId, r => r.LocationRef, r => r.Revision, correlationId)
                : UnequipItemCore(campaign, transition, TargetItemStack, "ItemStack", "ItemStackId", SelectItemStack, r => r.InventoryId, r => r.LocationRef, r => r.Revision, correlationId);
        }

        private Result<bool> UnequipItemCore<T>(
            CampaignHandle campaign,
            UnequipTransition transition,
            string targetKind,
            string table,
            string idColumn,
            Func<SqliteConnection, SqliteTransaction?, string, T?> select,
            Func<T, InventoryId> inventoryOf,
            Func<T, InventoryLocationRef> locationOf,
            Func<T, long> revisionOf,
            CorrelationId correlationId)
            where T : class
        {
            string itemRefId = ItemRefIdOf(transition.ItemRef);

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureInventoryTables(connection);
                using SqliteTransaction transaction = connection.BeginTransaction();

                bool ledgerExists = TryReadEquipmentLedger(connection, transaction, transition.CommandId, EquipmentOperationUnequip, itemRefId, transition.ExpectedEquippedEntryRevision, out bool identityMismatch);
                if (ledgerExists)
                {
                    transaction.Commit();
                    return identityMismatch
                        ? Result<bool>.Failure(PersistenceFailures.InventoryCommandIdentityMismatch(correlationId))
                        : Result<bool>.Success(true);
                }

                EquippedEntryRecord? entry = SelectEquippedEntry(connection, transaction, itemRefId, null);
                if (entry == null)
                {
                    transaction.Commit();
                    return Result<bool>.Failure(PersistenceFailures.EquipmentEntryNotFound(correlationId));
                }

                if (entry.Entry.Revision != transition.ExpectedEquippedEntryRevision)
                {
                    transaction.Commit();
                    return Result<bool>.Failure(PersistenceFailures.EquipmentEntryRevisionConflict(correlationId));
                }

                if (!entry.Entry.InventoryId.Equals(transition.InventoryId))
                {
                    transaction.Commit();
                    return Result<bool>.Failure(InventoryMovementFailures.SourceInvalid(correlationId));
                }

                T? target = select(connection, transaction, itemRefId);
                if (target == null)
                {
                    transaction.Commit();
                    return Result<bool>.Failure(targetKind == TargetItemInstance ? PersistenceFailures.ItemInstanceNotFound(correlationId) : PersistenceFailures.ItemStackNotFound(correlationId));
                }

                if (!inventoryOf(target).Equals(transition.InventoryId) || locationOf(target).Kind != InventoryLocationKind.Equipped || !locationOf(target).Equals(entry.Entry.ToLocationRef()))
                {
                    // Defensive: this should be unreachable given EquipItemCore<T> is
                    // the only writer keeping both records in sync; a mismatch here is
                    // an inconsistency to surface loudly, not silently repair.
                    transaction.Commit();
                    return Result<bool>.Failure(PersistenceFailures.InventoryIoFailed(correlationId));
                }

                if (revisionOf(target) != transition.ExpectedTargetRevision)
                {
                    transaction.Commit();
                    return Result<bool>.Failure(InventoryMovementFailures.ItemRevisionConflict(correlationId));
                }

                UtcInstant now = _clock.GetUtcNow();
                using (var update = connection.CreateCommand())
                {
                    update.Transaction = transaction;
                    update.CommandText = "UPDATE " + table + " SET LocationKind=$locationKind, LocationTargetRef=$locationTargetRef, LocationDetailRef=$locationDetailRef, Revision=Revision+1, UpdatedAt=$updatedAt WHERE " + idColumn + "=$id AND InventoryId=$inventoryId AND Revision=$expectedRevision;";
                    update.Parameters.AddWithValue("$locationKind", InventoryLocationKind.Contained.ToString());
                    update.Parameters.AddWithValue("$locationTargetRef", transition.InventoryId.ToString());
                    update.Parameters.AddWithValue("$locationDetailRef", transition.DestinationContainerKey);
                    update.Parameters.AddWithValue("$updatedAt", now.ToString());
                    update.Parameters.AddWithValue("$id", itemRefId);
                    update.Parameters.AddWithValue("$inventoryId", transition.InventoryId.ToString());
                    update.Parameters.AddWithValue("$expectedRevision", transition.ExpectedTargetRevision);
                    if (update.ExecuteNonQuery() != 1)
                    {
                        transaction.Rollback();
                        return Result<bool>.Failure(InventoryMovementFailures.ItemRevisionConflict(correlationId));
                    }
                }

                using (var delete = connection.CreateCommand())
                {
                    delete.Transaction = transaction;
                    delete.CommandText = "DELETE FROM EquippedEntry WHERE ItemRefId=$itemRefId AND Revision=$expectedRevision;";
                    delete.Parameters.AddWithValue("$itemRefId", itemRefId);
                    delete.Parameters.AddWithValue("$expectedRevision", transition.ExpectedEquippedEntryRevision);
                    if (delete.ExecuteNonQuery() != 1)
                    {
                        transaction.Rollback();
                        return Result<bool>.Failure(PersistenceFailures.EquipmentEntryRevisionConflict(correlationId));
                    }
                }

                InsertEquipmentLedger(connection, transaction, transition.CommandId, EquipmentOperationUnequip, itemRefId, transition.ExpectedEquippedEntryRevision, now);
                transaction.Commit();
                return Result<bool>.Success(true);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<bool>.Failure(PersistenceFailures.InventoryIoFailed(correlationId));
            }
        }

        private static string ItemRefIdOf(InventoryItemRef itemRef)
        {
            return itemRef.Kind == InventoryItemRefKind.ItemInstance ? itemRef.ItemInstanceId.ToString() : itemRef.ItemStackId.ToString();
        }

        private static string JoinBodyPartRefs(IReadOnlyList<BodyPartId> bodyPartRefs)
        {
            return string.Join(",", bodyPartRefs.Select(id => id.ToString()));
        }

        private static IReadOnlyList<BodyPartId> SplitBodyPartRefs(string bodyPartRefs)
        {
            return string.IsNullOrEmpty(bodyPartRefs)
                ? Array.Empty<BodyPartId>()
                : bodyPartRefs.Split(',').Select(BodyPartId.Parse).ToArray();
        }

        private static void AddEquippedEntryParameters(SqliteCommand command, string itemRefId, CampaignId campaignId, EquippedEntry entry, UtcInstant createdAt, UtcInstant updatedAt)
        {
            command.Parameters.AddWithValue("$itemRefId", itemRefId);
            command.Parameters.AddWithValue("$itemRefKind", entry.ItemRef.Kind.ToString());
            command.Parameters.AddWithValue("$campaignId", campaignId.ToString());
            command.Parameters.AddWithValue("$inventoryId", entry.InventoryId.ToString());
            command.Parameters.AddWithValue("$equipmentSlotRef", entry.EquipmentSlotRef);
            command.Parameters.AddWithValue("$bodyPartRefs", JoinBodyPartRefs(entry.BodyPartRefs));
            command.Parameters.AddWithValue("$equippedByUserId", entry.EquippedByUserId.ToString());
            command.Parameters.AddWithValue("$equippedAt", entry.EquippedAt.ToString());
            command.Parameters.AddWithValue("$revision", entry.Revision);
            command.Parameters.AddWithValue("$createdAt", createdAt.ToString());
            command.Parameters.AddWithValue("$updatedAt", updatedAt.ToString());
        }

        private const string EquippedEntrySelectColumns =
            "SELECT ItemRefId, ItemRefKind, CampaignId, InventoryId, EquipmentSlotRef, BodyPartRefs, " +
            "EquippedByUserId, EquippedAt, Revision, CreatedAt, UpdatedAt";

        private static EquippedEntryRecord? SelectEquippedEntry(SqliteConnection connection, SqliteTransaction? transaction, string itemRefId, CampaignId? expectedCampaignId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = EquippedEntrySelectColumns + " FROM EquippedEntry WHERE ItemRefId = $itemRefId LIMIT 1;";
            select.Parameters.AddWithValue("$itemRefId", itemRefId);
            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read()) return null;

            EquippedEntryRecord record = ReadEquippedEntry(reader);
            if (expectedCampaignId.HasValue && !record.CampaignId.Equals(expectedCampaignId.Value)) return null;
            return record;
        }

        private static EquippedEntryRecord ReadEquippedEntry(SqliteDataReader reader)
        {
            string itemRefKind = reader.GetString(1);
            InventoryItemRef itemRef = itemRefKind == InventoryItemRefKind.ItemInstance.ToString()
                ? InventoryItemRef.ForInstance(ItemInstanceId.Parse(reader.GetString(0)))
                : InventoryItemRef.ForStack(ItemStackId.Parse(reader.GetString(0)));

            var entry = new EquippedEntry(
                InventoryId.Parse(reader.GetString(3)),
                itemRef,
                reader.GetString(4),
                SplitBodyPartRefs(reader.GetString(5)),
                UserId.Parse(reader.GetString(6)),
                UtcInstant.Parse(reader.GetString(7)),
                reader.GetInt64(8));

            return new EquippedEntryRecord(CampaignId.Parse(reader.GetString(2)), entry);
        }

        private static bool TryReadEquipmentLedger(SqliteConnection connection, SqliteTransaction transaction, CommandId commandId, string expectedOperationKind, string expectedItemRefId, long expectedRevision, out bool identityMismatch)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT OperationKind, ItemRefId, ExpectedRevision FROM EquipmentCommandLedger WHERE CommandId = $commandId LIMIT 1;";
            select.Parameters.AddWithValue("$commandId", commandId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read())
            {
                identityMismatch = false;
                return false;
            }

            bool same = string.Equals(reader.GetString(0), expectedOperationKind, StringComparison.Ordinal) &&
                string.Equals(reader.GetString(1), expectedItemRefId, StringComparison.Ordinal) &&
                reader.GetInt64(2) == expectedRevision;
            identityMismatch = !same;
            return true;
        }

        private static void InsertEquipmentLedger(SqliteConnection connection, SqliteTransaction transaction, CommandId commandId, string operationKind, string itemRefId, long expectedRevision, UtcInstant now)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO EquipmentCommandLedger (CommandId, OperationKind, ItemRefId, ExpectedRevision, CreatedAt, AppliedAt) VALUES ($commandId, $operationKind, $itemRefId, $expectedRevision, $createdAt, $appliedAt);";
            insert.Parameters.AddWithValue("$commandId", commandId.ToString());
            insert.Parameters.AddWithValue("$operationKind", operationKind);
            insert.Parameters.AddWithValue("$itemRefId", itemRefId);
            insert.Parameters.AddWithValue("$expectedRevision", expectedRevision);
            insert.Parameters.AddWithValue("$createdAt", now.ToString());
            insert.Parameters.AddWithValue("$appliedAt", now.ToString());
            insert.ExecuteNonQuery();
        }

        private static bool InventoryExists(SqliteConnection connection, SqliteTransaction transaction, CampaignId campaignId, InventoryId inventoryId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT 1 FROM Inventory WHERE CampaignId = $campaignId AND InventoryId = $inventoryId LIMIT 1;";
            select.Parameters.AddWithValue("$campaignId", campaignId.ToString());
            select.Parameters.AddWithValue("$inventoryId", inventoryId.ToString());
            return select.ExecuteScalar() != null;
        }

        private static Result<TRecord>? TryReplay<TRecord>(
            SqliteConnection connection,
            SqliteTransaction? transaction,
            CommandId commandId,
            string expectedTargetKind,
            string expectedTargetId,
            Func<SqliteConnection, SqliteTransaction?, string, TRecord?> select,
            CorrelationId correlationId)
            where TRecord : class
        {
            using var lookup = connection.CreateCommand();
            lookup.Transaction = transaction;
            lookup.CommandText = "SELECT TargetKind, TargetId FROM InventoryCommandLedger WHERE CommandId = $commandId LIMIT 1;";
            lookup.Parameters.AddWithValue("$commandId", commandId.ToString());
            using SqliteDataReader reader = lookup.ExecuteReader();
            if (!reader.Read()) return null;

            string targetKind = reader.GetString(0);
            string targetId = reader.GetString(1);
            if (!string.Equals(targetKind, expectedTargetKind, StringComparison.Ordinal) ||
                !string.Equals(targetId, expectedTargetId, StringComparison.Ordinal))
            {
                return Result<TRecord>.Failure(PersistenceFailures.InventoryCommandIdentityMismatch(correlationId));
            }

            TRecord? record = select(connection, transaction, targetId);
            return record == null
                ? Result<TRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId))
                : Result<TRecord>.Success(record);
        }

        private static Result<InventoryCreateReplay<TRecord>> ProbeCreateReplay<TRecord>(
            SqliteConnection connection,
            CommandId commandId,
            string expectedTargetKind,
            string expectedTargetId,
            Func<SqliteConnection, SqliteTransaction?, string, TRecord?> select,
            CorrelationId correlationId)
            where TRecord : class
        {
            Result<TRecord>? replay = TryReplay(connection, null, commandId, expectedTargetKind, expectedTargetId, select, correlationId);
            if (replay == null)
            {
                return Result<InventoryCreateReplay<TRecord>>.Success(InventoryCreateReplay<TRecord>.None());
            }

            return replay.Value.IsFailure
                ? Result<InventoryCreateReplay<TRecord>>.Failure(replay.Value.Error)
                : Result<InventoryCreateReplay<TRecord>>.Success(InventoryCreateReplay<TRecord>.Found(replay.Value.Value));
        }

        private static void InsertLedgerEntry(SqliteConnection connection, SqliteTransaction transaction, CommandId commandId, string targetKind, string targetId, UtcInstant now)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO InventoryCommandLedger (CommandId, TargetKind, TargetId, CreatedAt, AppliedAt) VALUES ($commandId, $targetKind, $targetId, $createdAt, $appliedAt);";
            insert.Parameters.AddWithValue("$commandId", commandId.ToString());
            insert.Parameters.AddWithValue("$targetKind", targetKind);
            insert.Parameters.AddWithValue("$targetId", targetId);
            insert.Parameters.AddWithValue("$createdAt", now.ToString());
            insert.Parameters.AddWithValue("$appliedAt", now.ToString());
            insert.ExecuteNonQuery();
        }

        private static void AddItemCommonParameters(
            SqliteCommand command,
            CampaignId campaignId,
            InventoryId inventoryId,
            InventoryOwnerRef ownerRef,
            InventoryLocationRef locationRef,
            ContentDefinitionRef sourceRef,
            ItemMechanicsSnapshot snapshot,
            long revision,
            UtcInstant createdAt,
            UtcInstant updatedAt)
        {
            command.Parameters.AddWithValue("$campaignId", campaignId.ToString());
            command.Parameters.AddWithValue("$inventoryId", inventoryId.ToString());
            command.Parameters.AddWithValue("$ownerKind", ownerRef.Kind.ToString());
            command.Parameters.AddWithValue("$ownerTargetRef", ownerRef.TargetRef);
            command.Parameters.AddWithValue("$ownerLocationKey", (object?)ownerRef.LocationKey ?? DBNull.Value);
            command.Parameters.AddWithValue("$locationKind", locationRef.Kind.ToString());
            command.Parameters.AddWithValue("$locationTargetRef", locationRef.TargetRef);
            command.Parameters.AddWithValue("$locationDetailRef", locationRef.DetailRef);
            command.Parameters.AddWithValue("$sourceItemDefinitionRef", sourceRef.ToString());
            command.Parameters.AddWithValue("$mechanicsSourceDefinitionRef", snapshot.SourceDefinitionRef.ToString());
            command.Parameters.AddWithValue("$mechanicsDefinitionSnapshotVersion", snapshot.DefinitionSnapshotVersion);
            command.Parameters.AddWithValue("$mechanicsContentType", snapshot.ContentType.ToString());
            command.Parameters.AddWithValue("$mechanicsPayload", snapshot.Payload);
            command.Parameters.AddWithValue("$revision", revision);
            command.Parameters.AddWithValue("$createdAt", createdAt.ToString());
            command.Parameters.AddWithValue("$updatedAt", updatedAt.ToString());
        }

        private static InventoryRecord? SelectInventory(SqliteConnection connection, SqliteTransaction? transaction, string inventoryId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = InventorySelectColumns + " FROM Inventory WHERE InventoryId = $inventoryId LIMIT 1;";
            select.Parameters.AddWithValue("$inventoryId", inventoryId);
            using SqliteDataReader reader = select.ExecuteReader();
            return reader.Read() ? ReadInventory(reader) : null;
        }

        private static ItemInstanceRecord? SelectItemInstance(SqliteConnection connection, SqliteTransaction? transaction, string itemInstanceId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = ItemInstanceSelectColumns + " FROM ItemInstance WHERE ItemInstanceId = $itemInstanceId LIMIT 1;";
            select.Parameters.AddWithValue("$itemInstanceId", itemInstanceId);
            using SqliteDataReader reader = select.ExecuteReader();
            return reader.Read() ? ReadItemInstance(reader) : null;
        }

        private static ItemStackRecord? SelectItemStack(SqliteConnection connection, SqliteTransaction? transaction, string itemStackId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = ItemStackSelectColumns + " FROM ItemStack WHERE ItemStackId = $itemStackId LIMIT 1;";
            select.Parameters.AddWithValue("$itemStackId", itemStackId);
            using SqliteDataReader reader = select.ExecuteReader();
            return reader.Read() ? ReadItemStack(reader) : null;
        }

        private const string InventorySelectColumns = "SELECT InventoryId, CampaignId, OwnerKind, OwnerTargetRef, OwnerLocationKey, Revision, CreatedAt, UpdatedAt";

        private const string ItemInstanceSelectColumns =
            "SELECT ItemInstanceId, CampaignId, InventoryId, OwnerKind, OwnerTargetRef, OwnerLocationKey, " +
            "LocationKind, LocationTargetRef, LocationDetailRef, SourceItemDefinitionRef, " +
            "MechanicsSourceDefinitionRef, MechanicsDefinitionSnapshotVersion, MechanicsContentType, MechanicsPayload, " +
            "RuntimeState, Revision, CreatedAt, UpdatedAt";

        private const string ItemStackSelectColumns =
            "SELECT ItemStackId, CampaignId, InventoryId, OwnerKind, OwnerTargetRef, OwnerLocationKey, " +
            "LocationKind, LocationTargetRef, LocationDetailRef, SourceItemDefinitionRef, " +
            "MechanicsSourceDefinitionRef, MechanicsDefinitionSnapshotVersion, MechanicsContentType, MechanicsPayload, " +
            "Quantity, StackState, Revision, CreatedAt, UpdatedAt";

        private static InventoryRecord ReadInventory(SqliteDataReader reader)
        {
            return new InventoryRecord(
                InventoryId.Parse(reader.GetString(0)),
                CampaignId.Parse(reader.GetString(1)),
                ReadOwnerRef(reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4)),
                reader.GetInt64(5),
                UtcInstant.Parse(reader.GetString(6)),
                UtcInstant.Parse(reader.GetString(7)));
        }

        private static ItemInstanceRecord ReadItemInstance(SqliteDataReader reader)
        {
            return new ItemInstanceRecord(
                ItemInstanceId.Parse(reader.GetString(0)),
                CampaignId.Parse(reader.GetString(1)),
                InventoryId.Parse(reader.GetString(2)),
                ReadOwnerRef(reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5)),
                ReadLocationRef(reader.GetString(6), reader.GetString(7), reader.GetString(8)),
                ContentDefinitionRef.Parse(reader.GetString(9)),
                ReadMechanicsSnapshot(reader.GetString(10), reader.GetInt64(11), reader.GetString(12), reader.GetString(13)),
                reader.GetString(14),
                reader.GetInt64(15),
                UtcInstant.Parse(reader.GetString(16)),
                UtcInstant.Parse(reader.GetString(17)));
        }

        private static ItemStackRecord ReadItemStack(SqliteDataReader reader)
        {
            return new ItemStackRecord(
                ItemStackId.Parse(reader.GetString(0)),
                CampaignId.Parse(reader.GetString(1)),
                InventoryId.Parse(reader.GetString(2)),
                ReadOwnerRef(reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5)),
                ReadLocationRef(reader.GetString(6), reader.GetString(7), reader.GetString(8)),
                ContentDefinitionRef.Parse(reader.GetString(9)),
                ReadMechanicsSnapshot(reader.GetString(10), reader.GetInt64(11), reader.GetString(12), reader.GetString(13)),
                ItemStackQuantity.Create(reader.GetInt64(14)),
                reader.GetString(15),
                reader.GetInt64(16),
                UtcInstant.Parse(reader.GetString(17)),
                UtcInstant.Parse(reader.GetString(18)));
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

        private static ItemMechanicsSnapshot ReadMechanicsSnapshot(string sourceRef, long snapshotVersion, string contentType, string payload)
        {
            return new ItemMechanicsSnapshot(
                ContentDefinitionRef.Parse(sourceRef),
                snapshotVersion,
                (ContentDefinitionType)Enum.Parse(typeof(ContentDefinitionType), contentType),
                payload);
        }

        private static SqliteConnection OpenConnection(string rootPath)
        {
            string dbPath = Path.Combine(rootPath, "campaign.db");
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

        internal static void EnsureInventoryTables(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS Inventory (
    InventoryId TEXT PRIMARY KEY,
    CampaignId TEXT NOT NULL,
    OwnerKind TEXT NOT NULL,
    OwnerTargetRef TEXT NOT NULL,
    OwnerLocationKey TEXT,
    Revision INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS ItemInstance (
    ItemInstanceId TEXT PRIMARY KEY,
    CampaignId TEXT NOT NULL,
    InventoryId TEXT NOT NULL,
    OwnerKind TEXT NOT NULL,
    OwnerTargetRef TEXT NOT NULL,
    OwnerLocationKey TEXT,
    LocationKind TEXT NOT NULL,
    LocationTargetRef TEXT NOT NULL,
    LocationDetailRef TEXT NOT NULL,
    SourceItemDefinitionRef TEXT NOT NULL,
    MechanicsSourceDefinitionRef TEXT NOT NULL,
    MechanicsDefinitionSnapshotVersion INTEGER NOT NULL,
    MechanicsContentType TEXT NOT NULL,
    MechanicsPayload TEXT NOT NULL,
    RuntimeState TEXT NOT NULL,
    Revision INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    FOREIGN KEY (InventoryId) REFERENCES Inventory(InventoryId)
);
CREATE TABLE IF NOT EXISTS ItemStack (
    ItemStackId TEXT PRIMARY KEY,
    CampaignId TEXT NOT NULL,
    InventoryId TEXT NOT NULL,
    OwnerKind TEXT NOT NULL,
    OwnerTargetRef TEXT NOT NULL,
    OwnerLocationKey TEXT,
    LocationKind TEXT NOT NULL,
    LocationTargetRef TEXT NOT NULL,
    LocationDetailRef TEXT NOT NULL,
    SourceItemDefinitionRef TEXT NOT NULL,
    MechanicsSourceDefinitionRef TEXT NOT NULL,
    MechanicsDefinitionSnapshotVersion INTEGER NOT NULL,
    MechanicsContentType TEXT NOT NULL,
    MechanicsPayload TEXT NOT NULL,
    Quantity INTEGER NOT NULL,
    StackState TEXT NOT NULL,
    Revision INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    FOREIGN KEY (InventoryId) REFERENCES Inventory(InventoryId)
);
CREATE TABLE IF NOT EXISTS InventoryCommandLedger (
    CommandId TEXT PRIMARY KEY,
    TargetKind TEXT NOT NULL,
    TargetId TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    AppliedAt TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS InventoryMoveCommandLedger (
    CommandId TEXT PRIMARY KEY,
    TargetKind TEXT NOT NULL,
    TargetId TEXT NOT NULL,
    SourceInventoryId TEXT NOT NULL,
    DestinationInventoryId TEXT NOT NULL,
    DestinationContainerKey TEXT NOT NULL,
    TargetRevision INTEGER NOT NULL,
    SourceRevision INTEGER NOT NULL,
    DestinationRevision INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS InventoryStackCommandLedger (
    CommandId TEXT PRIMARY KEY,
    OperationKind TEXT NOT NULL,
    SourceId TEXT NOT NULL,
    ResultId TEXT NOT NULL,
    InventoryId TEXT NOT NULL,
    Quantity INTEGER NOT NULL,
    ExpectedSourceRevision INTEGER NOT NULL,
    ExpectedResultRevision INTEGER NOT NULL,
    ExpectedInventoryRevision INTEGER NOT NULL,
    ResultStackId TEXT NOT NULL,
    CreatedAt TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS EquippedEntry (
    ItemRefId TEXT PRIMARY KEY,
    ItemRefKind TEXT NOT NULL,
    CampaignId TEXT NOT NULL,
    InventoryId TEXT NOT NULL,
    EquipmentSlotRef TEXT NOT NULL,
    BodyPartRefs TEXT NOT NULL,
    EquippedByUserId TEXT NOT NULL,
    EquippedAt TEXT NOT NULL,
    Revision INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    FOREIGN KEY (InventoryId) REFERENCES Inventory(InventoryId)
);
CREATE TABLE IF NOT EXISTS EquipmentCommandLedger (
    CommandId TEXT PRIMARY KEY,
    OperationKind TEXT NOT NULL,
    ItemRefId TEXT NOT NULL,
    ExpectedRevision INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    AppliedAt TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_ItemInstance_Campaign_Inventory ON ItemInstance (CampaignId, InventoryId);
CREATE INDEX IF NOT EXISTS IX_ItemStack_Campaign_Inventory ON ItemStack (CampaignId, InventoryId);
CREATE INDEX IF NOT EXISTS IX_EquippedEntry_Campaign_Inventory ON EquippedEntry (CampaignId, InventoryId);";
            command.ExecuteNonQuery();
        }
    }
}
