using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using Odyssey.Application.Commands;
using Odyssey.Application.Inventory;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
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
                UpdateInventoryForMove(connection, transaction, source.InventoryId, source.Revision + 1, now);
                if (!source.InventoryId.Equals(destination.InventoryId)) UpdateInventoryForMove(connection, transaction, destination.InventoryId, destination.Revision + 1, now);
                using (var update = connection.CreateCommand())
                {
                    update.Transaction = transaction;
                    update.CommandText = "UPDATE " + table + " SET InventoryId=$inventoryId,OwnerKind=$ownerKind,OwnerTargetRef=$ownerTargetRef,OwnerLocationKey=$ownerLocationKey,LocationKind=$locationKind,LocationTargetRef=$locationTargetRef,LocationDetailRef=$locationDetailRef,Revision=Revision+1,UpdatedAt=$updatedAt WHERE " + idColumn + "=$id;";
                    update.Parameters.AddWithValue("$inventoryId", destination.InventoryId.ToString()); update.Parameters.AddWithValue("$ownerKind", destination.OwnerRef.Kind.ToString()); update.Parameters.AddWithValue("$ownerTargetRef", destination.OwnerRef.TargetRef); update.Parameters.AddWithValue("$ownerLocationKey", (object?)destination.OwnerRef.LocationKey ?? DBNull.Value); update.Parameters.AddWithValue("$locationKind", InventoryLocationKind.Contained.ToString()); update.Parameters.AddWithValue("$locationTargetRef", destination.InventoryId.ToString()); update.Parameters.AddWithValue("$locationDetailRef", move.DestinationContainerKey); update.Parameters.AddWithValue("$updatedAt", now.ToString()); update.Parameters.AddWithValue("$id", targetId); update.ExecuteNonQuery();
                }
                InsertMoveLedger(connection, transaction, move, targetKind, targetId, now);
                T? moved = select(connection, transaction, targetId); transaction.Commit(); return moved == null ? Result<T>.Failure(PersistenceFailures.CommandReplayFailed(correlationId)) : Result<T>.Success(moved);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException) { return Result<T>.Failure(PersistenceFailures.InventoryIoFailed(correlationId)); }
        }

        private static bool CommandExists(SqliteConnection c, SqliteTransaction t, string table, CommandId id) { using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = "SELECT 1 FROM " + table + " WHERE CommandId=$id LIMIT 1;"; q.Parameters.AddWithValue("$id", id.ToString()); return q.ExecuteScalar() != null; }
        private static Result<T>? TryMoveReplay<T>(SqliteConnection c, SqliteTransaction t, InventoryMove m, string kind, string target, Func<SqliteConnection, SqliteTransaction?, string, T?> select, CorrelationId id) where T : class { using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = "SELECT TargetKind,TargetId,SourceInventoryId,DestinationInventoryId,DestinationContainerKey,TargetRevision,SourceRevision,DestinationRevision FROM InventoryMoveCommandLedger WHERE CommandId=$id;"; q.Parameters.AddWithValue("$id", m.CommandId.ToString()); using var r = q.ExecuteReader(); if (!r.Read()) return null; bool same = r.GetString(0) == kind && r.GetString(1) == target && r.GetString(2) == m.SourceInventoryId.ToString() && r.GetString(3) == m.DestinationInventoryId.ToString() && r.GetString(4) == m.DestinationContainerKey && r.GetInt64(5) == m.ExpectedTargetRevision && r.GetInt64(6) == m.ExpectedSourceRevision && r.GetInt64(7) == m.ExpectedDestinationRevision; return !same ? Result<T>.Failure(PersistenceFailures.InventoryCommandIdentityMismatch(id)) : select(c, t, target) is T record ? Result<T>.Success(record) : Result<T>.Failure(PersistenceFailures.CommandReplayFailed(id)); }
        private static void UpdateInventoryForMove(SqliteConnection c, SqliteTransaction t, InventoryId id, long revision, UtcInstant now) { using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = "UPDATE Inventory SET Revision=$revision,UpdatedAt=$now WHERE InventoryId=$id;"; q.Parameters.AddWithValue("$revision", revision); q.Parameters.AddWithValue("$now", now.ToString()); q.Parameters.AddWithValue("$id", id.ToString()); q.ExecuteNonQuery(); }
        private static void InsertMoveLedger(SqliteConnection c, SqliteTransaction t, InventoryMove m, string kind, string target, UtcInstant now) { using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = "INSERT INTO InventoryMoveCommandLedger (CommandId,TargetKind,TargetId,SourceInventoryId,DestinationInventoryId,DestinationContainerKey,TargetRevision,SourceRevision,DestinationRevision,CreatedAt) VALUES ($commandId,$kind,$target,$source,$destination,$key,$targetRevision,$sourceRevision,$destinationRevision,$now);"; q.Parameters.AddWithValue("$commandId", m.CommandId.ToString()); q.Parameters.AddWithValue("$kind", kind); q.Parameters.AddWithValue("$target", target); q.Parameters.AddWithValue("$source", m.SourceInventoryId.ToString()); q.Parameters.AddWithValue("$destination", m.DestinationInventoryId.ToString()); q.Parameters.AddWithValue("$key", m.DestinationContainerKey); q.Parameters.AddWithValue("$targetRevision", m.ExpectedTargetRevision); q.Parameters.AddWithValue("$sourceRevision", m.ExpectedSourceRevision); q.Parameters.AddWithValue("$destinationRevision", m.ExpectedDestinationRevision); q.Parameters.AddWithValue("$now", now.ToString()); q.ExecuteNonQuery(); }

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
CREATE INDEX IF NOT EXISTS IX_ItemInstance_Campaign_Inventory ON ItemInstance (CampaignId, InventoryId);
CREATE INDEX IF NOT EXISTS IX_ItemStack_Campaign_Inventory ON ItemStack (CampaignId, InventoryId);";
            command.ExecuteNonQuery();
        }
    }
}
