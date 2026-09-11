using System;
using NUnit.Framework;
using Odyssey.Domain.Character;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;

namespace Odyssey.Tests.Domain.Inventory
{
    /// <summary>
    /// ODY-S05-301: real construction/validation/invariant tests for
    /// <see cref="EquippedEntry"/>. No persistence, no Equip/Unequip command,
    /// no rule-4 body-part-existence check, and no `RemoveBodyPart`
    /// dependency check are exercised here -- those belong to
    /// `ODY-S05-302`-`305`.
    /// </summary>
    public sealed class EquippedEntryTests
    {
        private static readonly UtcInstant Now = UtcInstant.Parse("2026-09-11T00:00:00.0000000Z");
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        [Test] // TC-INVENTORY-095
        public void EquippedEntry_ConstructsWithValidFields_AndExposesThemUnchanged()
        {
            InventoryId inventoryId = InventoryId.NewId(Now);
            InventoryItemRef itemRef = InventoryItemRef.ForStack(ItemStackId.NewId(Now));
            UserId equippedByUserId = NewUserId();
            var bodyPartRefs = new[] { BodyPartId.Parse("Torso"), BodyPartId.Parse("LeftArm") };

            var entry = new EquippedEntry(inventoryId, itemRef, "chest_slot", bodyPartRefs, equippedByUserId, Now, 1);

            Assert.That(entry.InventoryId, Is.EqualTo(inventoryId));
            Assert.That(entry.ItemRef, Is.EqualTo(itemRef));
            Assert.That(entry.EquipmentSlotRef, Is.EqualTo("chest_slot"));
            Assert.That(entry.BodyPartRefs, Is.EqualTo(bodyPartRefs));
            Assert.That(entry.EquippedByUserId, Is.EqualTo(equippedByUserId));
            Assert.That(entry.EquippedAt, Is.EqualTo(Now));
            Assert.That(entry.Revision, Is.EqualTo(1));
        }

        [Test] // TC-INVENTORY-096
        public void EquippedEntry_AllowsEmptyBodyPartRefs_ForSlotsThatDoNotRequireBodyPartSpecificity()
        {
            InventoryId inventoryId = InventoryId.NewId(Now);
            InventoryItemRef itemRef = InventoryItemRef.ForInstance(ItemInstanceId.NewId(Now));

            var entry = new EquippedEntry(inventoryId, itemRef, "held_slot", Array.Empty<BodyPartId>(), NewUserId(), Now, 1);

            Assert.That(entry.BodyPartRefs, Is.Empty, "ADR-027 section 7 names body-part references as optional");
        }

        [Test] // TC-INVENTORY-097
        public void EquippedEntry_RejectsInvalidFields()
        {
            InventoryId inventoryId = InventoryId.NewId(Now);
            InventoryItemRef itemRef = InventoryItemRef.ForStack(ItemStackId.NewId(Now));
            UserId equippedByUserId = NewUserId();
            var validBodyPartRefs = new[] { BodyPartId.Parse("Torso") };

            Assert.Throws<ArgumentException>(new Action(() =>
                new EquippedEntry(default, itemRef, "chest_slot", validBodyPartRefs, equippedByUserId, Now, 1)));
            Assert.Throws<ArgumentException>(new Action(() =>
                new EquippedEntry(inventoryId, default, "chest_slot", validBodyPartRefs, equippedByUserId, Now, 1)));
            Assert.Throws<ArgumentException>(new Action(() =>
                new EquippedEntry(inventoryId, itemRef, "Not Canonical", validBodyPartRefs, equippedByUserId, Now, 1)));
            Assert.Throws<ArgumentException>(new Action(() =>
                new EquippedEntry(inventoryId, itemRef, "", validBodyPartRefs, equippedByUserId, Now, 1)));
            Assert.Throws<ArgumentNullException>(new Action(() =>
                new EquippedEntry(inventoryId, itemRef, "chest_slot", null!, equippedByUserId, Now, 1)));
            Assert.Throws<ArgumentException>(new Action(() =>
                new EquippedEntry(inventoryId, itemRef, "chest_slot", new[] { default(BodyPartId) }, equippedByUserId, Now, 1)));
            Assert.Throws<ArgumentException>(new Action(() =>
                new EquippedEntry(inventoryId, itemRef, "chest_slot", validBodyPartRefs, default, Now, 1)));
            Assert.Throws<ArgumentOutOfRangeException>(new Action(() =>
                new EquippedEntry(inventoryId, itemRef, "chest_slot", validBodyPartRefs, equippedByUserId, Now, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(new Action(() =>
                new EquippedEntry(inventoryId, itemRef, "chest_slot", validBodyPartRefs, equippedByUserId, Now, -1)));
        }

        [Test] // TC-INVENTORY-098
        public void EquippedEntry_RejectsDuplicateBodyPartRefs()
        {
            InventoryId inventoryId = InventoryId.NewId(Now);
            InventoryItemRef itemRef = InventoryItemRef.ForStack(ItemStackId.NewId(Now));
            var duplicateBodyPartRefs = new[] { BodyPartId.Parse("Torso"), BodyPartId.Parse("Torso") };

            Assert.Throws<ArgumentException>(new Action(() =>
                new EquippedEntry(inventoryId, itemRef, "chest_slot", duplicateBodyPartRefs, NewUserId(), Now, 1)));
        }

        [Test] // TC-INVENTORY-099
        public void ToLocationRef_IsTheCheckableFormOfRuleOne_OnePlaceAtExactlyOneSlot()
        {
            InventoryId inventoryId = InventoryId.NewId(Now);
            InventoryId otherInventoryId = InventoryId.NewId(Now);
            InventoryItemRef itemRef = InventoryItemRef.ForStack(ItemStackId.NewId(Now));
            var entry = new EquippedEntry(inventoryId, itemRef, "chest_slot", Array.Empty<BodyPartId>(), NewUserId(), Now, 1);

            InventoryLocationRef expected = InventoryLocationRef.Equipped(inventoryId, "chest_slot");
            Assert.That(entry.ToLocationRef(), Is.EqualTo(expected), "an item's own stored LocationRef and its EquippedEntry must agree on the single place the item is in");

            Assert.That(entry.ToLocationRef(), Is.Not.EqualTo(InventoryLocationRef.Equipped(inventoryId, "belt_slot")), "a different slot is a different place");
            Assert.That(entry.ToLocationRef(), Is.Not.EqualTo(InventoryLocationRef.Equipped(otherInventoryId, "chest_slot")), "a different inventory is a different place");
            Assert.That(entry.ToLocationRef(), Is.Not.EqualTo(InventoryLocationRef.Contained(inventoryId, "chest_slot")), "a different location kind is a different place");
        }

        [Test] // TC-INVENTORY-100
        public void EquippedEntry_ReferencesEitherAnItemInstanceOrAnItemStack_NotBoth()
        {
            InventoryId inventoryId = InventoryId.NewId(Now);
            InventoryItemRef instanceRef = InventoryItemRef.ForInstance(ItemInstanceId.NewId(Now));
            InventoryItemRef stackRef = InventoryItemRef.ForStack(ItemStackId.NewId(Now));

            var instanceEntry = new EquippedEntry(inventoryId, instanceRef, "chest_slot", Array.Empty<BodyPartId>(), NewUserId(), Now, 1);
            var stackEntry = new EquippedEntry(inventoryId, stackRef, "belt_slot", Array.Empty<BodyPartId>(), NewUserId(), Now, 1);

            Assert.That(instanceEntry.ItemRef.Kind, Is.EqualTo(InventoryItemRefKind.ItemInstance));
            Assert.That(stackEntry.ItemRef.Kind, Is.EqualTo(InventoryItemRefKind.ItemStack));
        }
    }
}
