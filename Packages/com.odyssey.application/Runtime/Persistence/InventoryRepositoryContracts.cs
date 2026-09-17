using System;
using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Inventory;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
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
        /// <summary>MainGM-only confirmation with backup, live revision checks and atomic journal/snapshot commit. No post-success rollback command.</summary>
        Result<ItemDefinitionMigrationApplyResult> ApplyItemDefinitionMigration(CampaignHandle campaign, ItemDefinitionMigrationTransition transition, UserId actorUserId, bool actorIsMainGm, CorrelationId correlationId);

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
        /// ODY-S05-401: lists every <see cref="ItemInstanceRecord"/> in this
        /// campaign whose <c>SourceItemDefinitionRef</c> pins any published
        /// version of this <see cref="ContentDefinitionId"/>. Matches by the
        /// definition id prefix, ignoring the pinned version, and is
        /// campaign-wide (not scoped to one Inventory), so an ItemDefinition
        /// migration preview can see items created from any past published
        /// version of the definition across every Inventory in the campaign.
        /// </summary>
        Result<IReadOnlyList<ItemInstanceRecord>> ListItemInstancesBySourceDefinitionId(CampaignHandle campaign, CampaignId campaignId, ContentDefinitionId definitionId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S05-401: lists every <see cref="ItemStackRecord"/> in this
        /// campaign whose <c>SourceItemDefinitionRef</c> pins any published
        /// version of this <see cref="ContentDefinitionId"/>. Matches by the
        /// definition id prefix, ignoring the pinned version, and is
        /// campaign-wide (not scoped to one Inventory), so an ItemDefinition
        /// migration preview can see stacks created from any past published
        /// version of the definition across every Inventory in the campaign.
        /// </summary>
        Result<IReadOnlyList<ItemStackRecord>> ListItemStacksBySourceDefinitionId(CampaignHandle campaign, CampaignId campaignId, ContentDefinitionId definitionId, CorrelationId correlationId);

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

        /// <summary>
        /// ODY-S05-304: the atomic Unequip transition, the symmetric reverse of
        /// <see cref="EquipItem"/>. In one SQLite transaction: reads and
        /// CAS-checks the <see cref="EquippedEntryRecord"/> (the authority for
        /// "is this item currently equipped"), defensively cross-checks the
        /// item's own `LocationRef` against it, CAS-checks the item's own
        /// revision, updates its `LocationRef` back to
        /// `Contained(InventoryId, DestinationContainerKey)`, and removes the
        /// `EquippedEntry` row. No rule-4/`ICharacterRepository` dependency,
        /// `RemoveBodyPart` check, or non-`Contained` destination is supported.
        /// </summary>
        Result<bool> UnequipItem(CampaignHandle campaign, UnequipTransition transition, CorrelationId correlationId);

        /// <summary>
        /// ODY-S05-305: true if any <see cref="EquippedEntryRecord"/> owned by
        /// this character (via the joined <see cref="ItemInstanceRecord"/>/
        /// <see cref="ItemStackRecord"/>'s own <c>OwnerKind</c>/<c>OwnerTargetRef</c>,
        /// which do not change across Equip/Unequip) lists this
        /// <see cref="Odyssey.Domain.Character.BodyPartId"/> in its
        /// `BodyPartRefs`. A narrow existence query for
        /// `RemoveBodyPart` dependency checks (`ADR-027` section 7 rule 5) --
        /// not a general Equipment-by-body-part listing.
        /// </summary>
        Result<bool> HasAnyEquippedEntryReferencingBodyPart(CampaignHandle campaign, CampaignId campaignId, CharacterId characterId, BodyPartId bodyPartId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S06-103: lists every <see cref="EquippedEntryRecord"/> currently owned by this character (via
        /// the joined <see cref="ItemInstanceRecord"/>/<see cref="ItemStackRecord"/>'s own
        /// <c>OwnerKind</c>/<c>OwnerTargetRef</c>, which do not change across Equip/Unequip), by the same
        /// join <see cref="HasAnyEquippedEntryReferencingBodyPart"/> already proves is possible against this
        /// schema. Unlike that method, this returns the real rows, not a boolean existence check -- the
        /// narrow addition this task needs to read a target's own real equipped armor into the attack
        /// snapshot (`ADR-030`), without introducing a broader "inventory by character" abstraction.
        /// </summary>
        Result<IReadOnlyList<EquippedEntryRecord>> ListEquippedEntriesByCharacter(CampaignHandle campaign, CampaignId campaignId, CharacterId characterId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S06-107: consumes exactly one unit of a stackable/single-use item -- `UseItem`'s own
        /// resource-cost analogue, by the same narrow-addition precedent <see cref="ListEquippedEntriesByCharacter"/>
        /// already established for this port (a purpose-specific primitive, not a broader "adjust any
        /// quantity by any amount" abstraction nothing in this codebase has asked for yet). For a stack
        /// (<see cref="ItemStackRecord"/>), decrements <c>Quantity</c> by 1, or deletes the row entirely if
        /// that would leave 0 (<see cref="Odyssey.Domain.Inventory.ItemStackQuantity"/> cannot represent
        /// zero -- confirmed by direct code read before designing this method). For a non-stackable
        /// <see cref="ItemInstanceRecord"/>, deletes it outright -- a single use consumes the whole item.
        /// This port's own class doc comment ("deliberately does not... check permissions") is honored: this
        /// method accepts <paramref name="actorUserId"/>/<paramref name="actorIsMainGm"/> for interface parity
        /// with every other actor-carrying command in this codebase, but performs no permission gate itself
        /// -- the caller (`UseItemService`) is responsible for authorization before ever reaching this call.
        /// CAS-guarded against <paramref name="expectedRevision"/> (the stack/instance row's own `Revision`),
        /// idempotent by <paramref name="commandId"/> via a dedicated ledger (a re-select-based replay is not
        /// possible here, since a fully-consumed row's own record may no longer exist to re-select).
        /// </summary>
        Result<ConsumeItemUnitOutcome> ConsumeItemUnit(CampaignHandle campaign, InventoryItemRef item, UserId actorUserId, bool actorIsMainGm, long expectedRevision, CommandId commandId, CorrelationId correlationId);
    }

    /// <summary>ODY-S06-107: `IInventoryRepository.ConsumeItemUnit`'s own outcome -- <see cref="RemainingQuantity"/> is null when the row was deleted entirely (a non-stackable instance, or a stack's own last unit).</summary>
    public sealed class ConsumeItemUnitOutcome
    {
        public ConsumeItemUnitOutcome(InventoryItemRef item, bool wasFullyConsumed, long? remainingQuantity)
        {
            if (!item.IsValid) throw new ArgumentException("Item reference is required.", nameof(item));
            Item = item;
            WasFullyConsumed = wasFullyConsumed;
            RemainingQuantity = remainingQuantity;
        }

        public InventoryItemRef Item { get; }
        public bool WasFullyConsumed { get; }
        public long? RemainingQuantity { get; }
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
