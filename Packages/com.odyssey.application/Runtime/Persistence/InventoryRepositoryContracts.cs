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

        /// <summary>
        /// ODY-S05-302: creates the equipped-state storage row for an item.
        /// Idempotent by <paramref name="commandId"/>; rejects a second create
        /// for an already-equipped item (rule 1) with a dedicated conflict, not
        /// a silent duplicate. No Equip command semantics, MainGM/authorization
        /// check, or rule-4 body-part-existence check are performed here.
        /// </summary>
        Result<EquippedEntryRecord> CreateEquippedEntry(CampaignHandle campaign, EquippedEntryRecord record, CommandId commandId, CorrelationId correlationId);

        /// <summary>ODY-S05-302: reads the equipped-state row for an item, if any.</summary>
        Result<EquippedEntryRecord> GetEquippedEntry(CampaignHandle campaign, InventoryItemRef itemRef, CorrelationId correlationId);

        /// <summary>
        /// ODY-S05-302: CAS-protected replacement of an existing equipped-state
        /// row's mutable fields (slot, body-part refs, equipped-by, equipped-at),
        /// guarded by <paramref name="expectedRevision"/>. A physical transition
        /// primitive only -- <c>ODY-S05-303</c>/<c>304</c> own the Equip/Unequip
        /// business rules that call it.
        /// </summary>
        Result<EquippedEntryRecord> ReplaceEquippedEntry(CampaignHandle campaign, EquippedEntryRecord record, long expectedRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S05-302: CAS-protected removal of an equipped-state row, guarded
        /// by <paramref name="expectedRevision"/>. The physical unequip
        /// primitive; no destination/ownership validation is performed here.
        /// </summary>
        Result<bool> DeleteEquippedEntry(CampaignHandle campaign, InventoryItemRef itemRef, long expectedRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>ODY-S05-302: lists equipped-state rows scoped to one campaign inventory.</summary>
        Result<IReadOnlyList<EquippedEntryRecord>> ListEquippedEntries(CampaignHandle campaign, CampaignId campaignId, InventoryId inventoryId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S05-303: the atomic Equip transition. In one SQLite transaction:
        /// verifies the target item/stack is currently `Contained` in
        /// <see cref="EquipTransition.Record"/>'s own <c>InventoryId</c> at
        /// <see cref="EquipTransition.ExpectedTargetRevision"/> (CAS), verifies
        /// no <see cref="EquippedEntryRecord"/> already exists for it (rule 1),
        /// updates its `LocationRef` to `Equipped`, and inserts the
        /// <see cref="EquippedEntryRecord"/> row -- keeping the item's own
        /// `LocationRef` and `EquippedEntry.ToLocationRef()` consistent by
        /// construction. No MainGM/authorization check or rule-4
        /// body-part-existence check is performed here -- <c>EquipmentService</c>
        /// owns those before calling this primitive.
        /// </summary>
        Result<EquippedEntryRecord> EquipItem(CampaignHandle campaign, EquipTransition transition, CorrelationId correlationId);
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
