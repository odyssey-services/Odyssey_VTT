using System;
using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Inventory;
using Odyssey.Application.Results;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// ODY-S05-202: persistence foundation for runtime Inventory records.
    /// This is a storage port only. It accepts already-built records from
    /// future Application commands and deliberately does not inspect catalog
    /// status, decode typed definitions, check permissions, create snapshots,
    /// move items, split/merge stacks, equip items, run attacks, or migrate
    /// ItemDefinitions.
    /// </summary>
    public interface IInventoryRepository
    {
        Result<InventoryRecord> CreateInventory(CampaignHandle campaign, InventoryRecord record, CommandId commandId, CorrelationId correlationId);
        Result<InventoryRecord> GetInventory(CampaignHandle campaign, InventoryId inventoryId, CorrelationId correlationId);
        Result<ItemInstanceRecord> CreateItemInstance(CampaignHandle campaign, ItemInstanceRecord record, CommandId commandId, CorrelationId correlationId);
        Result<InventoryCreateReplay<ItemInstanceRecord>> TryReplayCreateItemInstance(CampaignHandle campaign, CommandId commandId, ItemInstanceId itemInstanceId, CorrelationId correlationId);
        Result<ItemInstanceRecord> GetItemInstance(CampaignHandle campaign, ItemInstanceId itemInstanceId, CorrelationId correlationId);
        Result<ItemStackRecord> CreateItemStack(CampaignHandle campaign, ItemStackRecord record, CommandId commandId, CorrelationId correlationId);
        Result<InventoryCreateReplay<ItemStackRecord>> TryReplayCreateItemStack(CampaignHandle campaign, CommandId commandId, ItemStackId itemStackId, CorrelationId correlationId);
        Result<ItemStackRecord> GetItemStack(CampaignHandle campaign, ItemStackId itemStackId, CorrelationId correlationId);
        Result<IReadOnlyList<ItemInstanceRecord>> ListItemInstances(CampaignHandle campaign, CampaignId campaignId, InventoryId inventoryId, CorrelationId correlationId);
        Result<IReadOnlyList<ItemStackRecord>> ListItemStacks(CampaignHandle campaign, CampaignId campaignId, InventoryId inventoryId, CorrelationId correlationId);
    }

    public sealed class InventoryCreateReplay<TRecord>
        where TRecord : class
    {
        private InventoryCreateReplay(TRecord? record)
        {
            Record = record;
        }

        public TRecord? Record { get; }
        public bool HasReplay => Record != null;

        public static InventoryCreateReplay<TRecord> None() => new InventoryCreateReplay<TRecord>(null);

        public static InventoryCreateReplay<TRecord> Found(TRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            return new InventoryCreateReplay<TRecord>(record);
        }
    }
}
