using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Application.Inventory;
using Odyssey.Application.Persistence;
using Odyssey.Application.Time;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S05-401: pure unit tests for <see cref="ItemDefinitionMigrationRules"/>.
    /// Covers `ADR-027` section 10 steps 1/2/4 only -- preview construction,
    /// the narrow "mechanically identical" stack-grouping rule, and
    /// <c>PreviewRevision</c> determinism. No blocking-incompatibility
    /// computation, `ADR-012` backup call, or apply logic is exercised or
    /// introduced here; no SQLite connection is needed since the builder is
    /// a pure calculation over caller-supplied records.
    /// </summary>
    public sealed class ItemDefinitionMigrationRulesTests
    {
        private static readonly IWallClock Clock = new SystemWallClock();
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        [Test] // TC-INVENTORY-157
        public void BuildPreview_WithOneAffectedInstance_ReportsBeforeAndAfterSnapshots()
        {
            ContentDefinitionId definitionId = ContentDefinitionId.NewId(Clock.GetUtcNow());
            ContentDefinitionRecord source = NewPublishedDefinition(definitionId, version: 1, ContentDefinitionType.Item, "{\"dmg\":1}");
            ContentDefinitionRecord target = NewPublishedDefinition(definitionId, version: 2, ContentDefinitionType.Item, "{\"dmg\":2}");
            InventoryRecord inventory = NewInventoryRecord(revision: 5);
            ItemInstanceRecord instance = NewInstance(inventory, source, revision: 3);

            ItemDefinitionMigrationPreview preview = ItemDefinitionMigrationRules.BuildPreview(
                source, target, new[] { instance }, Array.Empty<ItemStackRecord>(), new[] { inventory });

            Assert.That(preview.AffectedInstances.Count, Is.EqualTo(1));
            ItemDefinitionMigrationAffectedInstance affected = preview.AffectedInstances[0];
            Assert.That(affected.ItemInstanceId, Is.EqualTo(instance.ItemInstanceId));
            Assert.That(affected.ExpectedRevision, Is.EqualTo(3));
            Assert.That(affected.BeforeSnapshot, Is.EqualTo(instance.MechanicsSnapshot));
            Assert.That(affected.AfterSnapshot.Payload, Is.EqualTo("{\"dmg\":2}"));
            Assert.That(affected.AfterSnapshot.SourceDefinitionRef, Is.EqualTo(new ContentDefinitionRef(definitionId, 2)));
            Assert.That(preview.SourceDefinitionRef, Is.EqualTo(new ContentDefinitionRef(definitionId, 1)));
            Assert.That(preview.TargetDefinitionRef, Is.EqualTo(new ContentDefinitionRef(definitionId, 2)));
            Assert.That(preview.ExpectedSourceDefinitionRevision, Is.EqualTo(source.Revision));
        }

        [Test] // TC-INVENTORY-158
        public void BuildPreview_WithOneAffectedStack_ReportsBeforeAndAfterSnapshots()
        {
            ContentDefinitionId definitionId = ContentDefinitionId.NewId(Clock.GetUtcNow());
            ContentDefinitionRecord source = NewPublishedDefinition(definitionId, version: 1, ContentDefinitionType.Ammo, "{\"dmg\":1}");
            ContentDefinitionRecord target = NewPublishedDefinition(definitionId, version: 2, ContentDefinitionType.Ammo, "{\"dmg\":2}");
            InventoryRecord inventory = NewInventoryRecord(revision: 1);
            ItemStackRecord stack = NewStack(inventory, source, quantity: 5, stackState: "normal", revision: 1);

            ItemDefinitionMigrationPreview preview = ItemDefinitionMigrationRules.BuildPreview(
                source, target, Array.Empty<ItemInstanceRecord>(), new[] { stack }, new[] { inventory });

            Assert.That(preview.AffectedStacks.Count, Is.EqualTo(1));
            ItemDefinitionMigrationAffectedStackGroup group = preview.AffectedStacks[0];
            Assert.That(group.StackState, Is.EqualTo("normal"));
            Assert.That(group.BeforeSnapshot, Is.EqualTo(stack.MechanicsSnapshot));
            Assert.That(group.AfterSnapshot.Payload, Is.EqualTo("{\"dmg\":2}"));
            Assert.That(group.Members.Count, Is.EqualTo(1));
            Assert.That(group.Members[0].ItemStackId, Is.EqualTo(stack.ItemStackId));
            Assert.That(group.Members[0].Quantity, Is.EqualTo(stack.Quantity));
        }

        [Test] // TC-INVENTORY-159
        public void BuildPreview_GroupsStacksSharingMechanicsRegardlessOfInventoryOwnerOrLocation()
        {
            ContentDefinitionId definitionId = ContentDefinitionId.NewId(Clock.GetUtcNow());
            ContentDefinitionRecord source = NewPublishedDefinition(definitionId, version: 1, ContentDefinitionType.Ammo, "{\"dmg\":1}");
            ContentDefinitionRecord target = NewPublishedDefinition(definitionId, version: 2, ContentDefinitionType.Ammo, "{\"dmg\":2}");
            InventoryRecord inventoryA = NewInventoryRecord(revision: 1);
            InventoryRecord inventoryB = NewInventoryRecord(revision: 1);
            ItemStackRecord stackA = NewStack(inventoryA, source, quantity: 3, stackState: "normal", revision: 1);
            ItemStackRecord stackB = NewStack(inventoryB, source, quantity: 9, stackState: "normal", revision: 1);

            ItemDefinitionMigrationPreview preview = ItemDefinitionMigrationRules.BuildPreview(
                source, target, Array.Empty<ItemInstanceRecord>(), new[] { stackA, stackB }, new[] { inventoryA, inventoryB });

            Assert.That(preview.AffectedStacks.Count, Is.EqualTo(1), "stacks differing only by InventoryId/OwnerRef/LocationRef must join the same group");
            Assert.That(preview.AffectedStacks[0].Members.Count, Is.EqualTo(2));
        }

        [Test] // TC-INVENTORY-160
        public void BuildPreview_DoesNotGroupStacksWithDivergentMechanicsSnapshot()
        {
            ContentDefinitionId definitionId = ContentDefinitionId.NewId(Clock.GetUtcNow());
            ContentDefinitionRecord source = NewPublishedDefinition(definitionId, version: 1, ContentDefinitionType.Ammo, "{\"dmg\":1}");
            ContentDefinitionRecord target = NewPublishedDefinition(definitionId, version: 2, ContentDefinitionType.Ammo, "{\"dmg\":2}");
            InventoryRecord inventory = NewInventoryRecord(revision: 1);
            var sourceRef = new ContentDefinitionRef(definitionId, 1);
            var snapshotA = new ItemMechanicsSnapshot(sourceRef, 1, ContentDefinitionType.Ammo, "{\"variant\":\"a\"}");
            var snapshotB = new ItemMechanicsSnapshot(sourceRef, 1, ContentDefinitionType.Ammo, "{\"variant\":\"b\"}");
            ItemStackRecord stackA = NewStack(inventory, source, quantity: 3, stackState: "normal", revision: 1, snapshotOverride: snapshotA);
            ItemStackRecord stackB = NewStack(inventory, source, quantity: 9, stackState: "normal", revision: 1, snapshotOverride: snapshotB);

            ItemDefinitionMigrationPreview preview = ItemDefinitionMigrationRules.BuildPreview(
                source, target, Array.Empty<ItemInstanceRecord>(), new[] { stackA, stackB }, new[] { inventory });

            Assert.That(preview.AffectedStacks.Count, Is.EqualTo(2), "a MechanicsSnapshot divergence must always split the group");
        }

        [Test] // TC-INVENTORY-161
        public void BuildPreview_ListsOneInventoryRevisionPerDistinctReferencedInventory()
        {
            ContentDefinitionId definitionId = ContentDefinitionId.NewId(Clock.GetUtcNow());
            ContentDefinitionRecord source = NewPublishedDefinition(definitionId, version: 1, ContentDefinitionType.Item, "{\"dmg\":1}");
            ContentDefinitionRecord target = NewPublishedDefinition(definitionId, version: 2, ContentDefinitionType.Item, "{\"dmg\":2}");
            InventoryRecord inventoryA = NewInventoryRecord(revision: 4);
            InventoryRecord inventoryB = NewInventoryRecord(revision: 9);
            ItemInstanceRecord instanceA = NewInstance(inventoryA, source, revision: 1);
            ItemInstanceRecord instanceB = NewInstance(inventoryB, source, revision: 1);

            ItemDefinitionMigrationPreview preview = ItemDefinitionMigrationRules.BuildPreview(
                source, target, new[] { instanceA, instanceB }, Array.Empty<ItemStackRecord>(), new[] { inventoryA, inventoryB });

            Assert.That(preview.AffectedInventoryRevisions.Count, Is.EqualTo(2));
            Dictionary<InventoryId, long> byId = new();
            foreach (var entry in preview.AffectedInventoryRevisions) byId[entry.InventoryId] = entry.ExpectedRevision;
            Assert.That(byId[inventoryA.InventoryId], Is.EqualTo(4));
            Assert.That(byId[inventoryB.InventoryId], Is.EqualTo(9));
        }

        [Test] // TC-INVENTORY-162
        public void BuildPreview_WithMissingInventoryRecordForAnAffectedInstance_Throws()
        {
            ContentDefinitionId definitionId = ContentDefinitionId.NewId(Clock.GetUtcNow());
            ContentDefinitionRecord source = NewPublishedDefinition(definitionId, version: 1, ContentDefinitionType.Item, "{\"dmg\":1}");
            ContentDefinitionRecord target = NewPublishedDefinition(definitionId, version: 2, ContentDefinitionType.Item, "{\"dmg\":2}");
            InventoryRecord inventory = NewInventoryRecord(revision: 1);
            ItemInstanceRecord instance = NewInstance(inventory, source, revision: 1);

            Action act = () => ItemDefinitionMigrationRules.BuildPreview(
                source, target, new[] { instance }, Array.Empty<ItemStackRecord>(), Array.Empty<InventoryRecord>());
            Assert.Throws<ArgumentException>(act);
        }

        [Test] // TC-INVENTORY-166
        public void ComputePreviewRevision_IsDeterministicForIdenticalInputs()
        {
            ContentDefinitionId definitionId = ContentDefinitionId.NewId(Clock.GetUtcNow());
            ContentDefinitionRecord source = NewPublishedDefinition(definitionId, version: 1, ContentDefinitionType.Item, "{\"dmg\":1}");
            ContentDefinitionRecord target = NewPublishedDefinition(definitionId, version: 2, ContentDefinitionType.Item, "{\"dmg\":2}");
            InventoryRecord inventory = NewInventoryRecord(revision: 1);
            ItemInstanceRecord instance = NewInstance(inventory, source, revision: 1);

            ItemDefinitionMigrationPreview first = ItemDefinitionMigrationRules.BuildPreview(source, target, new[] { instance }, Array.Empty<ItemStackRecord>(), new[] { inventory });
            ItemDefinitionMigrationPreview second = ItemDefinitionMigrationRules.BuildPreview(source, target, new[] { instance }, Array.Empty<ItemStackRecord>(), new[] { inventory });

            Assert.That(second.PreviewRevision, Is.EqualTo(first.PreviewRevision));
        }

        [Test] // TC-INVENTORY-167
        public void ComputePreviewRevision_ChangesWhenAnAffectedSnapshotChanges()
        {
            ContentDefinitionId definitionId = ContentDefinitionId.NewId(Clock.GetUtcNow());
            ContentDefinitionRecord source = NewPublishedDefinition(definitionId, version: 1, ContentDefinitionType.Item, "{\"dmg\":1}");
            ContentDefinitionRecord targetA = NewPublishedDefinition(definitionId, version: 2, ContentDefinitionType.Item, "{\"dmg\":2}");
            ContentDefinitionRecord targetB = NewPublishedDefinition(definitionId, version: 2, ContentDefinitionType.Item, "{\"dmg\":99}");
            InventoryRecord inventory = NewInventoryRecord(revision: 1);
            ItemInstanceRecord instance = NewInstance(inventory, source, revision: 1);

            ItemDefinitionMigrationPreview first = ItemDefinitionMigrationRules.BuildPreview(source, targetA, new[] { instance }, Array.Empty<ItemStackRecord>(), new[] { inventory });
            ItemDefinitionMigrationPreview second = ItemDefinitionMigrationRules.BuildPreview(source, targetB, new[] { instance }, Array.Empty<ItemStackRecord>(), new[] { inventory });

            Assert.That(second.PreviewRevision, Is.Not.EqualTo(first.PreviewRevision));
        }

        [Test] // TC-INVENTORY-168
        public void ComputePreviewRevision_ChangesWhenAnExpectedInventoryRevisionChanges()
        {
            ContentDefinitionId definitionId = ContentDefinitionId.NewId(Clock.GetUtcNow());
            ContentDefinitionRecord source = NewPublishedDefinition(definitionId, version: 1, ContentDefinitionType.Item, "{\"dmg\":1}");
            ContentDefinitionRecord target = NewPublishedDefinition(definitionId, version: 2, ContentDefinitionType.Item, "{\"dmg\":2}");
            InventoryRecord inventoryRevisionOne = NewInventoryRecord(revision: 1);
            InventoryRecord inventoryRevisionTwo = new InventoryRecord(inventoryRevisionOne.InventoryId, inventoryRevisionOne.CampaignId, inventoryRevisionOne.OwnerRef, 2, inventoryRevisionOne.CreatedAt, inventoryRevisionOne.UpdatedAt);
            ItemInstanceRecord instance = NewInstance(inventoryRevisionOne, source, revision: 1);

            ItemDefinitionMigrationPreview first = ItemDefinitionMigrationRules.BuildPreview(source, target, new[] { instance }, Array.Empty<ItemStackRecord>(), new[] { inventoryRevisionOne });
            ItemDefinitionMigrationPreview second = ItemDefinitionMigrationRules.BuildPreview(source, target, new[] { instance }, Array.Empty<ItemStackRecord>(), new[] { inventoryRevisionTwo });

            Assert.That(second.PreviewRevision, Is.Not.EqualTo(first.PreviewRevision));
        }

        private static ContentDefinitionRecord NewPublishedDefinition(ContentDefinitionId id, long version, ContentDefinitionType type, string propertiesJson)
        {
            UtcInstant now = Clock.GetUtcNow();
            UserId user = NewUserId();
            return new ContentDefinitionRecord(
                id,
                ContentDefinitionOrigin.Campaign,
                type,
                "Test Definition",
                null,
                ContentDefinitionStatus.Published,
                version,
                1,
                Array.Empty<string>(),
                Array.Empty<string>(),
                propertiesJson,
                Array.Empty<ContentDefinitionRef>(),
                user,
                user,
                now,
                null,
                null,
                now,
                now);
        }

        private static InventoryRecord NewInventoryRecord(long revision)
        {
            UtcInstant now = Clock.GetUtcNow();
            return new InventoryRecord(InventoryId.NewId(now), CampaignId.NewId(now), InventoryOwnerRef.ForCharacter(CharacterId.NewId(now)), revision, now, now);
        }

        private static ItemInstanceRecord NewInstance(InventoryRecord inventory, ContentDefinitionRecord source, long revision)
        {
            UtcInstant now = Clock.GetUtcNow();
            var sourceRef = new ContentDefinitionRef(source.ContentDefinitionId, source.Version);
            var snapshot = new ItemMechanicsSnapshot(sourceRef, source.Version, source.DefinitionType, source.PropertiesJson);
            return new ItemInstanceRecord(
                ItemInstanceId.NewId(now),
                inventory.CampaignId,
                inventory.InventoryId,
                inventory.OwnerRef,
                InventoryLocationRef.Contained(inventory.InventoryId, "main"),
                sourceRef,
                snapshot,
                "{}",
                revision,
                now,
                now);
        }

        private static ItemStackRecord NewStack(InventoryRecord inventory, ContentDefinitionRecord source, long quantity, string stackState, long revision, ItemMechanicsSnapshot? snapshotOverride = null)
        {
            UtcInstant now = Clock.GetUtcNow();
            var sourceRef = new ContentDefinitionRef(source.ContentDefinitionId, source.Version);
            ItemMechanicsSnapshot snapshot = snapshotOverride ?? new ItemMechanicsSnapshot(sourceRef, source.Version, source.DefinitionType, source.PropertiesJson);
            return new ItemStackRecord(
                ItemStackId.NewId(now),
                inventory.CampaignId,
                inventory.InventoryId,
                inventory.OwnerRef,
                InventoryLocationRef.Contained(inventory.InventoryId, "main"),
                sourceRef,
                snapshot,
                ItemStackQuantity.Create(quantity),
                stackState,
                revision,
                now,
                now);
        }
    }
}
