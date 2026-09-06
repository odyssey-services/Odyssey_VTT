using System;
using NUnit.Framework;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;

namespace Odyssey.Tests.Domain.Inventory
{
    public sealed class InventoryRuntimeFoundationTests
    {
        private static readonly UtcInstant Now = UtcInstant.Parse("2026-09-06T00:00:00.0000000Z");

        [Test]
        public void RuntimeIds_MintParseAndRejectInvalidInput()
        {
            InventoryId inventoryId = InventoryId.NewId(Now);
            ItemInstanceId itemInstanceId = ItemInstanceId.NewId(Now);
            ItemStackId itemStackId = ItemStackId.NewId(Now);

            Assert.That(InventoryId.Parse(inventoryId.ToString()), Is.EqualTo(inventoryId));
            Assert.That(ItemInstanceId.Parse(itemInstanceId.ToString()), Is.EqualTo(itemInstanceId));
            Assert.That(ItemStackId.Parse(itemStackId.ToString()), Is.EqualTo(itemStackId));

            Assert.That(InventoryId.TryParse(itemInstanceId.ToString(), out InventoryId wrongInventory), Is.False);
            Assert.That(ItemInstanceId.TryParse(itemStackId.ToString(), out ItemInstanceId wrongInstance), Is.False);
            Assert.That(ItemStackId.TryParse(inventoryId.ToString(), out ItemStackId wrongStack), Is.False);
            Assert.That(InventoryId.TryParse(null, out InventoryId nullInventory), Is.False);
            Assert.That(ItemInstanceId.TryParse(string.Empty, out ItemInstanceId emptyInstance), Is.False);

            Assert.That(wrongInventory.IsValid, Is.False);
            Assert.That(wrongInstance.IsValid, Is.False);
            Assert.That(wrongStack.IsValid, Is.False);
            Assert.That(nullInventory.IsValid, Is.False);
            Assert.That(emptyInstance.IsValid, Is.False);
        }

        [Test]
        public void InventoryOwnerRef_SupportsCharacterAndSceneOwnersAndRejectsInvalidTargets()
        {
            CharacterId characterId = CharacterId.NewId(Now);
            SceneId sceneId = SceneId.NewId(Now);

            InventoryOwnerRef characterOwner = InventoryOwnerRef.ForCharacter(characterId);
            InventoryOwnerRef sceneOwner = InventoryOwnerRef.ForScene(sceneId, "locker_a");

            Assert.That(characterOwner.Kind, Is.EqualTo(InventoryOwnerKind.Character));
            Assert.That(characterOwner.TargetRef, Is.EqualTo(characterId.ToString()));
            Assert.That(characterOwner.LocationKey, Is.Null);
            Assert.That(sceneOwner.Kind, Is.EqualTo(InventoryOwnerKind.Scene));
            Assert.That(sceneOwner.TargetRef, Is.EqualTo(sceneId.ToString()));
            Assert.That(sceneOwner.LocationKey, Is.EqualTo("locker_a"));

            Assert.Throws<ArgumentException>(new Action(() => InventoryOwnerRef.ForCharacter(default)));
            Assert.Throws<ArgumentException>(new Action(() => InventoryOwnerRef.ForScene(default, "locker_a")));
            Assert.Throws<ArgumentException>(new Action(() => InventoryOwnerRef.ForScene(sceneId, "Not Canonical")));
        }

        [Test]
        public void InventoryLocationRef_ModelsLocationVocabularyWithoutEquipBehavior()
        {
            InventoryId inventoryId = InventoryId.NewId(Now);
            SceneId sceneId = SceneId.NewId(Now);

            InventoryLocationRef contained = InventoryLocationRef.Contained(inventoryId, "main");
            InventoryLocationRef equipped = InventoryLocationRef.Equipped(inventoryId, "body_slot");
            InventoryLocationRef dropped = InventoryLocationRef.SceneDropped(sceneId, "floor_01");
            InventoryLocationRef other = InventoryLocationRef.Other("vehicle", "veh_01");

            Assert.That(contained.Kind, Is.EqualTo(InventoryLocationKind.Contained));
            Assert.That(contained.TargetRef, Is.EqualTo(inventoryId.ToString()));
            Assert.That(equipped.Kind, Is.EqualTo(InventoryLocationKind.Equipped));
            Assert.That(equipped.TargetRef, Is.EqualTo(inventoryId.ToString()));
            Assert.That(equipped.DetailRef, Is.EqualTo("body_slot"));
            Assert.That(dropped.Kind, Is.EqualTo(InventoryLocationKind.SceneDropped));
            Assert.That(dropped.TargetRef, Is.EqualTo(sceneId.ToString()));
            Assert.That(other.Kind, Is.EqualTo(InventoryLocationKind.Other));
            Assert.That(other.TargetRef, Is.EqualTo("vehicle"));
            Assert.That(other.DetailRef, Is.EqualTo("veh_01"));

            Assert.Throws<ArgumentException>(new Action(() => InventoryLocationRef.Contained(default, "main")));
            Assert.Throws<ArgumentException>(new Action(() => InventoryLocationRef.Equipped(inventoryId, "Body Slot")));
            Assert.Throws<ArgumentException>(new Action(() => InventoryLocationRef.SceneDropped(sceneId, "")));
            Assert.Throws<ArgumentException>(new Action(() => InventoryLocationRef.Other("vehicle", " ")));
        }

        [Test]
        public void InventoryItemRef_ReferencesEitherInstanceOrStackAndRejectsMissingOrAmbiguousRefs()
        {
            ItemInstanceId instanceId = ItemInstanceId.NewId(Now);
            ItemStackId stackId = ItemStackId.NewId(Now);

            InventoryItemRef instanceRef = InventoryItemRef.ForInstance(instanceId);
            InventoryItemRef stackRef = InventoryItemRef.ForStack(stackId);

            Assert.That(instanceRef.Kind, Is.EqualTo(InventoryItemRefKind.ItemInstance));
            Assert.That(instanceRef.ItemInstanceId, Is.EqualTo(instanceId));
            Assert.That(instanceRef.ItemStackId.IsValid, Is.False);
            Assert.That(stackRef.Kind, Is.EqualTo(InventoryItemRefKind.ItemStack));
            Assert.That(stackRef.ItemStackId, Is.EqualTo(stackId));
            Assert.That(stackRef.ItemInstanceId.IsValid, Is.False);

            Assert.Throws<ArgumentException>(new Action(() => InventoryItemRef.ForInstance(default)));
            Assert.Throws<ArgumentException>(new Action(() => InventoryItemRef.ForStack(default)));
            Assert.Throws<ArgumentException>(new Action(() => new InventoryItemRef(InventoryItemRefKind.ItemInstance, instanceId, stackId)));
            Assert.Throws<ArgumentException>(new Action(() => new InventoryItemRef(InventoryItemRefKind.ItemStack, instanceId, stackId)));
        }

        [Test]
        public void ItemMechanicsSnapshot_RequiresExactDefinitionRefPositiveVersionAndOpaquePayload()
        {
            ContentDefinitionRef sourceRef = new ContentDefinitionRef(ContentDefinitionId.NewId(Now), 2);

            var snapshot = new ItemMechanicsSnapshot(sourceRef, 1, ContentDefinitionType.Weapon, "{\"damage\":\"1d6\"}");

            Assert.That(snapshot.SourceDefinitionRef, Is.EqualTo(sourceRef));
            Assert.That(snapshot.DefinitionSnapshotVersion, Is.EqualTo(1));
            Assert.That(snapshot.ContentType, Is.EqualTo(ContentDefinitionType.Weapon));
            Assert.That(snapshot.Payload, Is.EqualTo("{\"damage\":\"1d6\"}"));
            Assert.That(typeof(ItemMechanicsSnapshot).GetProperty("LatestDefinitionRef"), Is.Null);

            Assert.Throws<ArgumentException>(new Action(() => new ItemMechanicsSnapshot(default, 1, ContentDefinitionType.Item, "{}")));
            Assert.Throws<ArgumentOutOfRangeException>(new Action(() => new ItemMechanicsSnapshot(sourceRef, 0, ContentDefinitionType.Item, "{}")));
            Assert.Throws<ArgumentOutOfRangeException>(new Action(() => new ItemMechanicsSnapshot(sourceRef, 1, (ContentDefinitionType)999, "{}")));
            Assert.Throws<ArgumentException>(new Action(() => new ItemMechanicsSnapshot(sourceRef, 1, ContentDefinitionType.Item, "")));
        }

        [Test]
        public void ItemStackQuantity_AcceptsPositiveAndRejectsZeroOrNegativeLiveQuantity()
        {
            ItemStackQuantity quantity = ItemStackQuantity.Create(3);

            Assert.That(quantity.Value, Is.EqualTo(3));
            Assert.That(quantity.IsValid, Is.True);
            Assert.That(quantity.ToString(), Is.EqualTo("3"));

            Assert.Throws<ArgumentOutOfRangeException>(new Action(() => ItemStackQuantity.Create(0)));
            Assert.Throws<ArgumentOutOfRangeException>(new Action(() => ItemStackQuantity.Create(-1)));
        }
    }
}
