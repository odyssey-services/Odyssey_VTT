using System;
using System.Collections.Generic;
using Odyssey.Domain.Character;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Domain.Inventory
{
    /// <summary>
    /// ODY-S05-301: `ADR-027` section 7's own "Minimum equipment record" --
    /// Inventory-owned location state over an exact item/stack reference, an
    /// equipment slot token, and optional body-part references
    /// (<see cref="BodyPartId"/>, reused directly from `ODY-S04-109`, not a
    /// duplicate type). This is domain vocabulary only: no persistence
    /// (`ODY-S05-302`), no Equip/Unequip command (`ODY-S05-303`/`304`), no
    /// check that a referenced body part currently exists on the owning
    /// Character (rule 4, `ODY-S05-303`'s own job), and no `RemoveBodyPart`
    /// dependency check (rule 5, `ODY-S05-305`'s own job) are implemented
    /// here.
    ///
    /// Deliberately a standalone type, not a typed extension of
    /// <see cref="InventoryLocationRef.Equipped"/>: that struct is also used
    /// by `Contained`/`SceneDropped`/`Other`, where `BodyPartRefs`/
    /// `EquippedByUserId`/`EquippedAt`/`Revision` would be meaningless
    /// dead fields. `ADR-027` section 14 assigns "equipment placement" and
    /// "one-place item rules" to `Odyssey.Domain` ownership directly, and
    /// <c>CharacterAnatomy</c> (`Odyssey.Domain.Character`) is the existing
    /// precedent for a Domain-owned sealed class carrying an
    /// <see cref="IReadOnlyList{T}"/> field and a <c>Revision</c> guard, so
    /// this type follows that shape rather than living only as an
    /// Application-layer "record" the way `InventoryRecord`/`ItemStackRecord`
    /// do (those exist mainly to add a repository-facing `CampaignId`, which
    /// `ADR-027` section 7's own minimum record does not name; `CampaignId`,
    /// if a persistence contract needs one, is `ODY-S05-302`'s own decision).
    ///
    /// Rule 1 ("one item or stack is in exactly one place") is expressed
    /// here as a checkable, type-level invariant through
    /// <see cref="ToLocationRef"/>: every <c>EquippedEntry</c> deterministically
    /// maps to exactly one <see cref="InventoryLocationRef"/> value (Equipped,
    /// at this <see cref="InventoryId"/>/<see cref="EquipmentSlotRef"/>). A
    /// future task holding both an item's own stored `LocationRef` and its
    /// `EquippedEntry` can assert the two agree by plain equality, without
    /// this type needing to know about `ItemInstanceRecord`/`ItemStackRecord`
    /// or any persistence store.
    /// </summary>
    public sealed class EquippedEntry
    {
        public EquippedEntry(
            InventoryId inventoryId,
            InventoryItemRef itemRef,
            string equipmentSlotRef,
            IReadOnlyList<BodyPartId> bodyPartRefs,
            UserId equippedByUserId,
            UtcInstant equippedAt,
            long revision)
        {
            if (!inventoryId.IsValid) throw new ArgumentException("InventoryId is required.", nameof(inventoryId));
            if (!itemRef.IsValid) throw new ArgumentException("ItemRef is required.", nameof(itemRef));
            if (!InventoryOwnerRef.IsToken(equipmentSlotRef)) throw new ArgumentException("EquipmentSlotRef is required and must be canonical.", nameof(equipmentSlotRef));
            if (bodyPartRefs == null) throw new ArgumentNullException(nameof(bodyPartRefs));

            // ADR-027 section 7: body-part references are optional (a slot
            // may not require body-part specificity), but every reference
            // that IS present must be a valid, distinct BodyPartId -- two
            // entries for the same body part on one EquippedEntry cannot mean
            // anything real and most likely signals a caller defect.
            var seenBodyPartRefs = new HashSet<BodyPartId>();
            foreach (BodyPartId bodyPartId in bodyPartRefs)
            {
                if (!bodyPartId.IsValid) throw new ArgumentException("BodyPartRefs must all be valid.", nameof(bodyPartRefs));
                if (!seenBodyPartRefs.Add(bodyPartId)) throw new ArgumentException("BodyPartRefs must not contain duplicates.", nameof(bodyPartRefs));
            }

            if (!equippedByUserId.IsValid) throw new ArgumentException("EquippedByUserId is required.", nameof(equippedByUserId));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));

            InventoryId = inventoryId;
            ItemRef = itemRef;
            EquipmentSlotRef = equipmentSlotRef;
            BodyPartRefs = bodyPartRefs;
            EquippedByUserId = equippedByUserId;
            EquippedAt = equippedAt;
            Revision = revision;
        }

        public InventoryId InventoryId { get; }
        public InventoryItemRef ItemRef { get; }
        public string EquipmentSlotRef { get; }
        public IReadOnlyList<BodyPartId> BodyPartRefs { get; }
        public UserId EquippedByUserId { get; }
        public UtcInstant EquippedAt { get; }
        public long Revision { get; }

        /// <summary>ADR-027 section 7 rule 1's checkable form: the single `InventoryLocationRef` this equipped entry corresponds to. A caller with an item's own stored `LocationRef` can compare it against this value to confirm the two agree on exactly one place.</summary>
        public InventoryLocationRef ToLocationRef() => InventoryLocationRef.Equipped(InventoryId, EquipmentSlotRef);
    }
}
