using System;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Inventory
{
    public sealed class InventoryRecord
    {
        public InventoryRecord(InventoryId inventoryId, CampaignId campaignId, InventoryOwnerRef ownerRef, long revision, UtcInstant createdAt, UtcInstant updatedAt)
        {
            if (!inventoryId.IsValid) throw new ArgumentException("InventoryId is required.", nameof(inventoryId));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!ownerRef.IsValid) throw new ArgumentException("OwnerRef is required.", nameof(ownerRef));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));

            InventoryId = inventoryId;
            CampaignId = campaignId;
            OwnerRef = ownerRef;
            Revision = revision;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        public InventoryId InventoryId { get; }
        public CampaignId CampaignId { get; }
        public InventoryOwnerRef OwnerRef { get; }
        public long Revision { get; }
        public UtcInstant CreatedAt { get; }
        public UtcInstant UpdatedAt { get; }
    }

    public sealed class ItemInstanceRecord
    {
        public ItemInstanceRecord(
            ItemInstanceId itemInstanceId,
            CampaignId campaignId,
            InventoryId inventoryId,
            InventoryOwnerRef ownerRef,
            InventoryLocationRef locationRef,
            ContentDefinitionRef sourceItemDefinitionRef,
            ItemMechanicsSnapshot mechanicsSnapshot,
            string runtimeState,
            long revision,
            UtcInstant createdAt,
            UtcInstant updatedAt)
        {
            if (!itemInstanceId.IsValid) throw new ArgumentException("ItemInstanceId is required.", nameof(itemInstanceId));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!inventoryId.IsValid) throw new ArgumentException("InventoryId is required.", nameof(inventoryId));
            if (!ownerRef.IsValid) throw new ArgumentException("OwnerRef is required.", nameof(ownerRef));
            if (!locationRef.IsValid) throw new ArgumentException("LocationRef is required.", nameof(locationRef));
            InventoryRecordGuards.RequireMatchingInventoryLocation(inventoryId, locationRef, nameof(locationRef));
            if (!sourceItemDefinitionRef.IsValid) throw new ArgumentException("Source item definition ref is required.", nameof(sourceItemDefinitionRef));
            if (!mechanicsSnapshot.SourceDefinitionRef.Equals(sourceItemDefinitionRef)) throw new ArgumentException("Mechanics snapshot must match the source item definition ref.", nameof(mechanicsSnapshot));
            if (runtimeState == null) throw new ArgumentNullException(nameof(runtimeState));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));

            ItemInstanceId = itemInstanceId;
            CampaignId = campaignId;
            InventoryId = inventoryId;
            OwnerRef = ownerRef;
            LocationRef = locationRef;
            SourceItemDefinitionRef = sourceItemDefinitionRef;
            MechanicsSnapshot = mechanicsSnapshot;
            RuntimeState = runtimeState;
            Revision = revision;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        public ItemInstanceId ItemInstanceId { get; }
        public CampaignId CampaignId { get; }
        public InventoryId InventoryId { get; }
        public InventoryOwnerRef OwnerRef { get; }
        public InventoryLocationRef LocationRef { get; }
        public ContentDefinitionRef SourceItemDefinitionRef { get; }
        public ItemMechanicsSnapshot MechanicsSnapshot { get; }
        public string RuntimeState { get; }
        public long Revision { get; }
        public UtcInstant CreatedAt { get; }
        public UtcInstant UpdatedAt { get; }
    }

    public sealed class ItemStackRecord
    {
        public ItemStackRecord(
            ItemStackId itemStackId,
            CampaignId campaignId,
            InventoryId inventoryId,
            InventoryOwnerRef ownerRef,
            InventoryLocationRef locationRef,
            ContentDefinitionRef sourceItemDefinitionRef,
            ItemMechanicsSnapshot mechanicsSnapshot,
            ItemStackQuantity quantity,
            string stackState,
            long revision,
            UtcInstant createdAt,
            UtcInstant updatedAt)
        {
            if (!itemStackId.IsValid) throw new ArgumentException("ItemStackId is required.", nameof(itemStackId));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!inventoryId.IsValid) throw new ArgumentException("InventoryId is required.", nameof(inventoryId));
            if (!ownerRef.IsValid) throw new ArgumentException("OwnerRef is required.", nameof(ownerRef));
            if (!locationRef.IsValid) throw new ArgumentException("LocationRef is required.", nameof(locationRef));
            InventoryRecordGuards.RequireMatchingInventoryLocation(inventoryId, locationRef, nameof(locationRef));
            if (!sourceItemDefinitionRef.IsValid) throw new ArgumentException("Source item definition ref is required.", nameof(sourceItemDefinitionRef));
            if (!mechanicsSnapshot.SourceDefinitionRef.Equals(sourceItemDefinitionRef)) throw new ArgumentException("Mechanics snapshot must match the source item definition ref.", nameof(mechanicsSnapshot));
            if (!quantity.IsValid) throw new ArgumentException("Quantity is required.", nameof(quantity));
            if (stackState == null) throw new ArgumentNullException(nameof(stackState));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));

            ItemStackId = itemStackId;
            CampaignId = campaignId;
            InventoryId = inventoryId;
            OwnerRef = ownerRef;
            LocationRef = locationRef;
            SourceItemDefinitionRef = sourceItemDefinitionRef;
            MechanicsSnapshot = mechanicsSnapshot;
            Quantity = quantity;
            StackState = stackState;
            Revision = revision;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        public ItemStackId ItemStackId { get; }
        public CampaignId CampaignId { get; }
        public InventoryId InventoryId { get; }
        public InventoryOwnerRef OwnerRef { get; }
        public InventoryLocationRef LocationRef { get; }
        public ContentDefinitionRef SourceItemDefinitionRef { get; }
        public ItemMechanicsSnapshot MechanicsSnapshot { get; }
        public ItemStackQuantity Quantity { get; }
        public string StackState { get; }
        public long Revision { get; }
        public UtcInstant CreatedAt { get; }
        public UtcInstant UpdatedAt { get; }
    }

    /// <summary>
    /// ODY-S05-302: persistence-facing wrapper around the Domain
    /// <see cref="EquippedEntry"/> (`ODY-S05-301`). Composes rather than
    /// duplicates -- <see cref="EquippedEntry"/>'s own constructor already
    /// validates its fields, so this record adds only what the Domain type
    /// deliberately does not carry: a repository-facing <see cref="CampaignId"/>
    /// for campaign-boundary scoping, the same reason <see cref="InventoryRecord"/>
    /// and <see cref="ItemStackRecord"/> carry one. See `EquippedEntry.cs`'s own
    /// doc-comment, which explicitly defers this decision to this task.
    /// </summary>
    public sealed class EquippedEntryRecord
    {
        public EquippedEntryRecord(CampaignId campaignId, EquippedEntry entry)
        {
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            CampaignId = campaignId;
            Entry = entry;
        }

        public CampaignId CampaignId { get; }
        public EquippedEntry Entry { get; }
    }

    internal static class InventoryRecordGuards
    {
        internal static void RequireMatchingInventoryLocation(InventoryId inventoryId, InventoryLocationRef locationRef, string parameterName)
        {
            if ((locationRef.Kind == InventoryLocationKind.Contained || locationRef.Kind == InventoryLocationKind.Equipped) &&
                !string.Equals(locationRef.TargetRef, inventoryId.ToString(), StringComparison.Ordinal))
            {
                throw new ArgumentException("Contained and equipped locations must target the record InventoryId.", parameterName);
            }
        }
    }
}
