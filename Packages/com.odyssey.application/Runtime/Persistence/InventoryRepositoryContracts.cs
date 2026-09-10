using System;
using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Inventory;
using Odyssey.Application.Results;
using Odyssey.Domain.Content;
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
        Result<ItemInstanceRecord> MoveItemInstance(CampaignHandle campaign, InventoryMove move, CorrelationId correlationId);
        Result<InventoryCreateReplay<ItemInstanceRecord>> TryReplayCreateItemInstance(CampaignHandle campaign, CommandId commandId, ItemInstanceId itemInstanceId, CorrelationId correlationId);
        Result<ItemInstanceRecord> GetItemInstance(CampaignHandle campaign, ItemInstanceId itemInstanceId, CorrelationId correlationId);
        Result<ItemStackRecord> CreateItemStack(CampaignHandle campaign, ItemStackRecord record, CommandId commandId, CorrelationId correlationId);
        Result<ItemStackRecord> MoveItemStack(CampaignHandle campaign, InventoryMove move, CorrelationId correlationId);
        Result<ItemStackRecord> SplitItemStack(CampaignHandle campaign, InventoryStackOperation operation, CorrelationId correlationId);
        Result<ItemStackRecord> MergeItemStacks(CampaignHandle campaign, InventoryStackOperation operation, CorrelationId correlationId);
        Result<InventoryCreateReplay<ItemStackRecord>> TryReplayCreateItemStack(CampaignHandle campaign, CommandId commandId, ItemStackId itemStackId, CorrelationId correlationId);
        Result<ItemStackRecord> GetItemStack(CampaignHandle campaign, ItemStackId itemStackId, CorrelationId correlationId);
        Result<IReadOnlyList<ItemInstanceRecord>> ListItemInstances(CampaignHandle campaign, CampaignId campaignId, InventoryId inventoryId, CorrelationId correlationId);
        Result<IReadOnlyList<ItemStackRecord>> ListItemStacks(CampaignHandle campaign, CampaignId campaignId, InventoryId inventoryId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S05-206: true if any <see cref="ItemInstanceRecord"/> or
        /// <see cref="ItemStackRecord"/> in this campaign is currently owned by
        /// this character. Contained, raw-equipped-location, and
        /// scene-dropped-but-still-owned all count, since an item's
        /// <c>OwnerRef</c> does not change with its <c>LocationRef</c>. A narrow
        /// existence query for `DeleteCharacterPermanently` dependency checks --
        /// not a general Inventory-by-owner listing.
        /// </summary>
        Result<bool> HasAnyItemOwnedByCharacter(CampaignHandle campaign, CampaignId campaignId, CharacterId characterId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S05-206: true if any <see cref="ItemInstanceRecord"/> or
        /// <see cref="ItemStackRecord"/> in this campaign pins any published
        /// version of this <see cref="ContentDefinitionId"/> as its
        /// <c>SourceItemDefinitionRef</c>. Matches by the definition id prefix,
        /// ignoring the pinned version, since a runtime reference to any version
        /// blocks physically deleting that definition's row
        /// (`ADR-027` section 4.1 rule 4/5).
        /// </summary>
        Result<bool> HasAnyRuntimeReferenceToDefinition(CampaignHandle campaign, CampaignId campaignId, ContentDefinitionId definitionId, CorrelationId correlationId);
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
