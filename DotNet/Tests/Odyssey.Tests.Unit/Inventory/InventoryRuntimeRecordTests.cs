using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Odyssey.Application.Inventory;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;

namespace Odyssey.Tests.Unit.Inventory
{
    public sealed class InventoryRuntimeRecordTests
    {
        private static readonly UtcInstant Now = UtcInstant.Parse("2026-09-06T00:00:00.0000000Z");

        [Test]
        public void InventoryApplicationRecords_ValidateRequiredIdsRefsRevisionsAndTimestamps()
        {
            InventoryId inventoryId = InventoryId.NewId(Now);
            CampaignId campaignId = CampaignId.NewId(Now);
            InventoryOwnerRef ownerRef = InventoryOwnerRef.ForCharacter(CharacterId.NewId(Now));
            InventoryLocationRef locationRef = InventoryLocationRef.Contained(inventoryId, "main");
            ContentDefinitionRef sourceRef = new ContentDefinitionRef(ContentDefinitionId.NewId(Now), 1);
            var snapshot = new ItemMechanicsSnapshot(sourceRef, 1, ContentDefinitionType.Item, "{}");

            var inventory = new InventoryRecord(inventoryId, campaignId, ownerRef, 1, Now, Now);
            var instance = new ItemInstanceRecord(ItemInstanceId.NewId(Now), campaignId, inventoryId, ownerRef, locationRef, sourceRef, snapshot, "{}", 1, Now, Now);
            var stack = new ItemStackRecord(ItemStackId.NewId(Now), campaignId, inventoryId, ownerRef, locationRef, sourceRef, snapshot, ItemStackQuantity.Create(2), "{}", 1, Now, Now);

            Assert.That(inventory.InventoryId, Is.EqualTo(inventoryId));
            Assert.That(inventory.CampaignId, Is.EqualTo(campaignId));
            Assert.That(inventory.OwnerRef, Is.EqualTo(ownerRef));
            Assert.That(instance.SourceItemDefinitionRef, Is.EqualTo(sourceRef));
            Assert.That(instance.MechanicsSnapshot, Is.EqualTo(snapshot));
            Assert.That(instance.RuntimeState, Is.EqualTo("{}"));
            Assert.That(stack.SourceItemDefinitionRef, Is.EqualTo(sourceRef));
            Assert.That(stack.Quantity.Value, Is.EqualTo(2));
            Assert.That(stack.StackState, Is.EqualTo("{}"));

            Assert.Throws<ArgumentException>(new Action(() => new InventoryRecord(default, campaignId, ownerRef, 1, Now, Now)));
            Assert.Throws<ArgumentOutOfRangeException>(new Action(() => new InventoryRecord(inventoryId, campaignId, ownerRef, 0, Now, Now)));
            Assert.Throws<ArgumentException>(new Action(() => new ItemInstanceRecord(default, campaignId, inventoryId, ownerRef, locationRef, sourceRef, snapshot, "{}", 1, Now, Now)));
            Assert.Throws<ArgumentNullException>(new Action(() => new ItemInstanceRecord(ItemInstanceId.NewId(Now), campaignId, inventoryId, ownerRef, locationRef, sourceRef, snapshot, null!, 1, Now, Now)));
            Assert.Throws<ArgumentException>(new Action(() => new ItemStackRecord(default, campaignId, inventoryId, ownerRef, locationRef, sourceRef, snapshot, ItemStackQuantity.Create(1), "{}", 1, Now, Now)));
            Assert.Throws<ArgumentNullException>(new Action(() => new ItemStackRecord(ItemStackId.NewId(Now), campaignId, inventoryId, ownerRef, locationRef, sourceRef, snapshot, ItemStackQuantity.Create(1), null!, 1, Now, Now)));
        }

        [Test]
        public void ItemInstanceRecord_RejectsContainedLocationPointingToAnotherInventory()
        {
            InventoryId inventoryId = InventoryId.NewId(Now);
            InventoryId otherInventoryId = InventoryId.NewId(Now);
            CampaignId campaignId = CampaignId.NewId(Now);
            InventoryOwnerRef ownerRef = InventoryOwnerRef.ForCharacter(CharacterId.NewId(Now));
            ContentDefinitionRef sourceRef = new ContentDefinitionRef(ContentDefinitionId.NewId(Now), 1);
            var snapshot = new ItemMechanicsSnapshot(sourceRef, 1, ContentDefinitionType.Item, "{}");

            var sameInventoryInstance = new ItemInstanceRecord(ItemInstanceId.NewId(Now), campaignId, inventoryId, ownerRef, InventoryLocationRef.Contained(inventoryId, "main"), sourceRef, snapshot, "{}", 1, Now, Now);

            Assert.That(sameInventoryInstance.InventoryId, Is.EqualTo(inventoryId));
            Assert.That(sameInventoryInstance.LocationRef.TargetRef, Is.EqualTo(inventoryId.ToString()));

            Assert.Throws<ArgumentException>(new Action(() => new ItemInstanceRecord(ItemInstanceId.NewId(Now), campaignId, inventoryId, ownerRef, InventoryLocationRef.Contained(otherInventoryId, "main"), sourceRef, snapshot, "{}", 1, Now, Now)));
        }

        [Test]
        public void ItemStackRecord_RejectsEquippedLocationPointingToAnotherInventory()
        {
            InventoryId inventoryId = InventoryId.NewId(Now);
            InventoryId otherInventoryId = InventoryId.NewId(Now);
            CampaignId campaignId = CampaignId.NewId(Now);
            InventoryOwnerRef ownerRef = InventoryOwnerRef.ForCharacter(CharacterId.NewId(Now));
            ContentDefinitionRef sourceRef = new ContentDefinitionRef(ContentDefinitionId.NewId(Now), 1);
            var snapshot = new ItemMechanicsSnapshot(sourceRef, 1, ContentDefinitionType.Item, "{}");

            var sameInventoryStack = new ItemStackRecord(ItemStackId.NewId(Now), campaignId, inventoryId, ownerRef, InventoryLocationRef.Equipped(inventoryId, "belt"), sourceRef, snapshot, ItemStackQuantity.Create(1), "{}", 1, Now, Now);

            Assert.That(sameInventoryStack.InventoryId, Is.EqualTo(inventoryId));
            Assert.That(sameInventoryStack.LocationRef.TargetRef, Is.EqualTo(inventoryId.ToString()));

            Assert.Throws<ArgumentException>(new Action(() => new ItemStackRecord(ItemStackId.NewId(Now), campaignId, inventoryId, ownerRef, InventoryLocationRef.Equipped(otherInventoryId, "belt"), sourceRef, snapshot, ItemStackQuantity.Create(1), "{}", 1, Now, Now)));
        }

        [Test]
        public void InventoryFoundationRecords_DoNotIntroduceCommandEquipmentAttackActiveEffectOrMigrationBehavior()
        {
            Type[] contractTypes =
            {
                typeof(InventoryRecord),
                typeof(ItemInstanceRecord),
                typeof(ItemStackRecord),
                typeof(InventoryId),
                typeof(ItemInstanceId),
                typeof(ItemStackId)
            };

            Assert.That(contractTypes.Select(t => t.Name), Has.None.Contains("Repository"));
            Assert.That(contractTypes.Select(t => t.Name), Has.None.Contains("Command"));
            Assert.That(contractTypes.Select(t => t.Name), Has.None.Contains("Service"));

            string root = FindRepositoryRoot();
            string[] inventoryRuntimeFiles = Directory.GetFiles(Path.Combine(root, "Packages"), "*.cs", SearchOption.AllDirectories)
                .Where(path => path.IndexOf("Inventory", StringComparison.OrdinalIgnoreCase) >= 0)
                .Where(path => path.IndexOf(Path.Combine("Runtime", "Inventory"), StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();

            Assert.That(inventoryRuntimeFiles.Any(path => Path.GetFileName(path).IndexOf("Command", StringComparison.OrdinalIgnoreCase) >= 0), Is.False, "ODY-S05-201 must not add Inventory commands.");
            Assert.That(inventoryRuntimeFiles.Any(path => Path.GetFileName(path).IndexOf("Service", StringComparison.OrdinalIgnoreCase) >= 0), Is.False, "ODY-S05-201 must not add Inventory services.");

            string characterLifecycle = File.ReadAllText(Path.Combine(root, "Packages", "com.odyssey.domain", "Runtime", "Character", "CharacterLifecycle.cs"));
            Assert.That(characterLifecycle, Does.Not.Contain("InventoryRevision"));
            Assert.That(characterLifecycle, Does.Not.Contain("InventoryId"));
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) && Directory.Exists(Path.Combine(directory.FullName, "Packages")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Repository root was not found.");
        }
    }
}
