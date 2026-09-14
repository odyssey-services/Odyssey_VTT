using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Inventory;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S05-202: real SQLite tests for the Inventory persistence
    /// foundation only. No command service, catalog validation, equipment
    /// behavior, attack behavior, ActiveEffect runtime, or ItemDefinition
    /// migration workflow is introduced here.
    /// </summary>
    public sealed class SqliteInventoryRepositoryTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));

        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;
        private SqliteInventoryRepository _inventoryRepository = null!;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s05-202-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> created = _campaignRepository.Create(new CreateCampaignRequest(_campaignDir, "Inventory Persistence Test Campaign", "ruleset.core", "1.0.0", "0.1.0"), NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _inventoryRepository = new SqliteInventoryRepository(Clock);
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaignRepository.Close(_campaign, TestCorrelationId); }
            catch (IOException) { }

            try { if (Directory.Exists(_campaignDir)) Directory.Delete(_campaignDir, recursive: true); }
            catch (IOException) { }
        }

        [Test]
        public void CreateInventory_ThenGetInventory_RoundTrips()
        {
            InventoryRecord inventory = NewInventoryRecord();

            Result<InventoryRecord> created = _inventoryRepository.CreateInventory(_campaign, inventory, NewCommandId(), TestCorrelationId);
            Result<InventoryRecord> fetched = _inventoryRepository.GetInventory(_campaign, inventory.InventoryId, TestCorrelationId);

            Assert.That(created.IsSuccess, Is.True);
            Assert.That(fetched.IsSuccess, Is.True);
            AssertInventoryEquals(inventory, fetched.Value);
        }

        [Test]
        public void CreateItemInstance_ThenGetItemInstance_RoundTrips()
        {
            InventoryRecord inventory = CreateInventory();
            ItemInstanceRecord instance = NewItemInstanceRecord(inventory);

            Result<ItemInstanceRecord> created = _inventoryRepository.CreateItemInstance(_campaign, instance, NewCommandId(), TestCorrelationId);
            Result<ItemInstanceRecord> fetched = _inventoryRepository.GetItemInstance(_campaign, instance.ItemInstanceId, TestCorrelationId);

            Assert.That(created.IsSuccess, Is.True);
            Assert.That(fetched.IsSuccess, Is.True);
            AssertItemInstanceEquals(instance, fetched.Value);
        }

        [Test]
        public void CreateItemStack_ThenGetItemStack_RoundTrips()
        {
            InventoryRecord inventory = CreateInventory();
            ItemStackRecord stack = NewItemStackRecord(inventory, quantity: 7);

            Result<ItemStackRecord> created = _inventoryRepository.CreateItemStack(_campaign, stack, NewCommandId(), TestCorrelationId);
            Result<ItemStackRecord> fetched = _inventoryRepository.GetItemStack(_campaign, stack.ItemStackId, TestCorrelationId);

            Assert.That(created.IsSuccess, Is.True);
            Assert.That(fetched.IsSuccess, Is.True);
            AssertItemStackEquals(stack, fetched.Value);
        }

        [Test]
        public void ListItemInstances_ReturnsOnlyRequestedCampaignInventory()
        {
            InventoryRecord requested = CreateInventory();
            InventoryRecord other = CreateInventory();
            ItemInstanceRecord expected = CreateItemInstance(requested, "expected");
            CreateItemInstance(other, "other");

            Result<IReadOnlyList<ItemInstanceRecord>> listed = _inventoryRepository.ListItemInstances(_campaign, requested.CampaignId, requested.InventoryId, TestCorrelationId);

            Assert.That(listed.IsSuccess, Is.True);
            Assert.That(listed.Value.Select(i => i.ItemInstanceId), Is.EquivalentTo(new[] { expected.ItemInstanceId }));
        }

        [Test]
        public void ListItemStacks_ReturnsOnlyRequestedCampaignInventory()
        {
            InventoryRecord requested = CreateInventory();
            InventoryRecord other = CreateInventory();
            ItemStackRecord expected = CreateItemStack(requested, "expected");
            CreateItemStack(other, "other");

            Result<IReadOnlyList<ItemStackRecord>> listed = _inventoryRepository.ListItemStacks(_campaign, requested.CampaignId, requested.InventoryId, TestCorrelationId);

            Assert.That(listed.IsSuccess, Is.True);
            Assert.That(listed.Value.Select(s => s.ItemStackId), Is.EquivalentTo(new[] { expected.ItemStackId }));
        }

        [Test] // TC-INVENTORY-163
        public void ListItemInstancesBySourceDefinitionId_MatchesAcrossAllPublishedVersionsAndInventories()
        {
            InventoryRecord inventoryA = CreateInventory();
            InventoryRecord inventoryB = CreateInventory();
            ContentDefinitionId definitionId = ContentDefinitionId.NewId(Clock.GetUtcNow());
            ItemInstanceRecord fromV1 = CreateItemInstanceWithDefinition(inventoryA, new ContentDefinitionRef(definitionId, 1));
            ItemInstanceRecord fromV2 = CreateItemInstanceWithDefinition(inventoryB, new ContentDefinitionRef(definitionId, 2));
            ItemInstanceRecord unrelated = CreateItemInstance(inventoryA, "unrelated");

            Result<IReadOnlyList<ItemInstanceRecord>> listed = _inventoryRepository.ListItemInstancesBySourceDefinitionId(_campaign, _campaign.CampaignId, definitionId, TestCorrelationId);

            Assert.That(listed.IsSuccess, Is.True);
            Assert.That(listed.Value.Select(i => i.ItemInstanceId), Is.EquivalentTo(new[] { fromV1.ItemInstanceId, fromV2.ItemInstanceId }));
            Assert.That(listed.Value.Select(i => i.ItemInstanceId), Does.Not.Contain(unrelated.ItemInstanceId));
        }

        [Test] // TC-INVENTORY-164
        public void ListItemStacksBySourceDefinitionId_MatchesAcrossMultipleInventoriesInTheSameCampaign()
        {
            InventoryRecord inventoryA = CreateInventory();
            InventoryRecord inventoryB = CreateInventory();
            ContentDefinitionId definitionId = ContentDefinitionId.NewId(Clock.GetUtcNow());
            ItemStackRecord stackA = CreateItemStackWithDefinition(inventoryA, new ContentDefinitionRef(definitionId, 1));
            ItemStackRecord stackB = CreateItemStackWithDefinition(inventoryB, new ContentDefinitionRef(definitionId, 3));

            Result<IReadOnlyList<ItemStackRecord>> listed = _inventoryRepository.ListItemStacksBySourceDefinitionId(_campaign, _campaign.CampaignId, definitionId, TestCorrelationId);

            Assert.That(listed.IsSuccess, Is.True);
            Assert.That(listed.Value.Select(s => s.ItemStackId), Is.EquivalentTo(new[] { stackA.ItemStackId, stackB.ItemStackId }));
        }

        [Test] // TC-INVENTORY-165
        public void ListItemRecordsBySourceDefinitionId_IsScopedToTheRequestedCampaign()
        {
            InventoryRecord inventory = CreateInventory();
            ContentDefinitionId definitionId = ContentDefinitionId.NewId(Clock.GetUtcNow());
            CreateItemInstanceWithDefinition(inventory, new ContentDefinitionRef(definitionId, 1));
            CampaignId otherCampaignId = OtherCampaignId();

            Result<IReadOnlyList<ItemInstanceRecord>> listed = _inventoryRepository.ListItemInstancesBySourceDefinitionId(_campaign, otherCampaignId, definitionId, TestCorrelationId);

            Assert.That(listed.IsFailure, Is.True);
            Assert.That(listed.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryCampaignMismatch));
        }

        [Test]
        public void CreateInventory_WithMismatchedCampaignId_IsRejected()
        {
            CreateInventory();
            InventoryRecord inventory = NewInventoryRecord(OtherCampaignId());

            Result<InventoryRecord> created = _inventoryRepository.CreateInventory(_campaign, inventory, NewCommandId(), TestCorrelationId);

            Assert.That(created.IsFailure, Is.True);
            Assert.That(created.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryCampaignMismatch));
            Assert.That(CountRows("Inventory", "InventoryId", inventory.InventoryId.ToString()), Is.EqualTo(0));
        }

        [Test]
        public void CreateItemInstance_WithMismatchedCampaignId_IsRejected()
        {
            InventoryRecord inventory = CreateInventory();
            var mismatchedInventory = new InventoryRecord(
                inventory.InventoryId,
                OtherCampaignId(),
                inventory.OwnerRef,
                inventory.Revision,
                inventory.CreatedAt,
                inventory.UpdatedAt);
            ItemInstanceRecord instance = NewItemInstanceRecord(mismatchedInventory);

            Result<ItemInstanceRecord> created = _inventoryRepository.CreateItemInstance(_campaign, instance, NewCommandId(), TestCorrelationId);

            Assert.That(created.IsFailure, Is.True);
            Assert.That(created.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryCampaignMismatch));
            Assert.That(CountRows("ItemInstance", "ItemInstanceId", instance.ItemInstanceId.ToString()), Is.EqualTo(0));
        }

        [Test]
        public void CreateItemStack_WithMismatchedCampaignId_IsRejected()
        {
            InventoryRecord inventory = CreateInventory();
            var mismatchedInventory = new InventoryRecord(
                inventory.InventoryId,
                OtherCampaignId(),
                inventory.OwnerRef,
                inventory.Revision,
                inventory.CreatedAt,
                inventory.UpdatedAt);
            ItemStackRecord stack = NewItemStackRecord(mismatchedInventory);

            Result<ItemStackRecord> created = _inventoryRepository.CreateItemStack(_campaign, stack, NewCommandId(), TestCorrelationId);

            Assert.That(created.IsFailure, Is.True);
            Assert.That(created.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryCampaignMismatch));
            Assert.That(CountRows("ItemStack", "ItemStackId", stack.ItemStackId.ToString()), Is.EqualTo(0));
        }

        [Test]
        public void ListItemRecords_WithMismatchedCampaignId_AreRejected()
        {
            InventoryRecord inventory = CreateInventory();
            CampaignId otherCampaignId = OtherCampaignId();

            Result<IReadOnlyList<ItemInstanceRecord>> instances = _inventoryRepository.ListItemInstances(_campaign, otherCampaignId, inventory.InventoryId, TestCorrelationId);
            Result<IReadOnlyList<ItemStackRecord>> stacks = _inventoryRepository.ListItemStacks(_campaign, otherCampaignId, inventory.InventoryId, TestCorrelationId);

            Assert.That(instances.IsFailure, Is.True);
            Assert.That(instances.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryCampaignMismatch));
            Assert.That(stacks.IsFailure, Is.True);
            Assert.That(stacks.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryCampaignMismatch));
        }

        [Test]
        public void CreateItemInstance_WithMissingInventoryId_IsRejected()
        {
            InventoryRecord inventory = NewInventoryRecord();
            ItemInstanceRecord instance = NewItemInstanceRecord(inventory);

            Result<ItemInstanceRecord> created = _inventoryRepository.CreateItemInstance(_campaign, instance, NewCommandId(), TestCorrelationId);

            Assert.That(created.IsFailure, Is.True);
            Assert.That(created.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryNotFound));
            Assert.That(CountRows("ItemInstance", "ItemInstanceId", instance.ItemInstanceId.ToString()), Is.EqualTo(0));
        }

        [Test]
        public void CreateItemStack_WithMissingInventoryId_IsRejected()
        {
            InventoryRecord inventory = NewInventoryRecord();
            ItemStackRecord stack = NewItemStackRecord(inventory);

            Result<ItemStackRecord> created = _inventoryRepository.CreateItemStack(_campaign, stack, NewCommandId(), TestCorrelationId);

            Assert.That(created.IsFailure, Is.True);
            Assert.That(created.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryNotFound));
            Assert.That(CountRows("ItemStack", "ItemStackId", stack.ItemStackId.ToString()), Is.EqualTo(0));
        }

        [Test]
        public void CreateReplay_WithSameCommandIdAndTarget_ReturnsCurrentRecordWithoutDuplicate()
        {
            InventoryRecord inventory = NewInventoryRecord();
            CommandId commandId = NewCommandId();

            Result<InventoryRecord> first = _inventoryRepository.CreateInventory(_campaign, inventory, commandId, TestCorrelationId);
            Assert.That(first.IsSuccess, Is.True);
            SetInventoryRevisionDirectly(inventory.InventoryId, 5);

            Result<InventoryRecord> replay = _inventoryRepository.CreateInventory(_campaign, inventory, commandId, TestCorrelationId);

            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.InventoryId, Is.EqualTo(inventory.InventoryId));
            Assert.That(replay.Value.Revision, Is.EqualTo(5), "replay returns the current stored target row, not the original in-memory argument");
            Assert.That(CountRows("Inventory", "InventoryId", inventory.InventoryId.ToString()), Is.EqualTo(1));
        }

        [Test]
        public void CreateReplay_WithSameCommandIdForDifferentTarget_IsRejected()
        {
            InventoryRecord firstInventory = NewInventoryRecord();
            InventoryRecord secondInventory = NewInventoryRecord();
            CommandId commandId = NewCommandId();

            Result<InventoryRecord> first = _inventoryRepository.CreateInventory(_campaign, firstInventory, commandId, TestCorrelationId);
            Result<InventoryRecord> second = _inventoryRepository.CreateInventory(_campaign, secondInventory, commandId, TestCorrelationId);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(second.IsFailure, Is.True);
            Assert.That(second.Error.Code, Is.EqualTo(ErrorCodes.CommandIdentityMismatch));
            Assert.That(CountRows("Inventory", "InventoryId", secondInventory.InventoryId.ToString()), Is.EqualTo(0));
        }

        [Test]
        public void InventorySchema_ContainsOnlyAllowedInventoryRuntimeTablesAndIndexes()
        {
            CreateInventory();

            using SqliteConnection connection = OpenRawConnection();
            var names = new List<(string Type, string Name)>();
            using (var select = connection.CreateCommand())
            {
                select.CommandText = "SELECT type, name FROM sqlite_master WHERE name NOT LIKE 'sqlite_autoindex_%' AND (name LIKE '%Inventory%' OR name LIKE '%ItemInstance%' OR name LIKE '%ItemStack%') ORDER BY type, name;";
                using SqliteDataReader reader = select.ExecuteReader();
                while (reader.Read()) names.Add((reader.GetString(0), reader.GetString(1)));
            }

            string[] allowed =
            {
                "Inventory",
                "ItemInstance",
                "ItemStack",
                "InventoryCommandLedger",
                "InventoryMoveCommandLedger",
                "InventoryStackCommandLedger",
                "IX_ItemInstance_Campaign_Inventory",
                "IX_ItemStack_Campaign_Inventory",
                // ODY-S05-302: the Equipment table's own list index happens to match
                // this guard's "%Inventory%" filter because it is scoped by InventoryId;
                // the EquippedEntry/EquipmentCommandLedger tables themselves do not
                // match this filter and are covered by their own schema guard instead
                // (SqliteEquipmentRepositoryTests.EquipmentSchema_ContainsOnlyAllowedEquipmentTablesAndIndex).
                "IX_EquippedEntry_Campaign_Inventory"
            };
            Assert.That(names.Select(n => n.Name), Is.SubsetOf(allowed));
            Assert.That(names.Where(n => n.Type == "table").Select(n => n.Name), Is.EquivalentTo(new[] { "Inventory", "InventoryCommandLedger", "InventoryMoveCommandLedger", "InventoryStackCommandLedger", "ItemInstance", "ItemStack" }));
        }

        [Test]
        public void InventorySchema_DefinesItemInventoryForeignKeys()
        {
            CreateInventory();

            Assert.That(HasForeignKey("ItemInstance", "InventoryId", "Inventory", "InventoryId"), Is.True);
            Assert.That(HasForeignKey("ItemStack", "InventoryId", "Inventory", "InventoryId"), Is.True);
        }

        [Test]
        public void InventoryPersistence_IntroducesNoEquipmentActiveEffectAttackOrItemDefinitionMigrationTablesOrClasses()
        {
            CreateInventory();

            using SqliteConnection connection = OpenRawConnection();
            using (var select = connection.CreateCommand())
            {
                select.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
                using SqliteDataReader reader = select.ExecuteReader();
                var tableNames = new List<string>();
                while (reader.Read()) tableNames.Add(reader.GetString(0));

                // ODY-S05-302 may introduce exactly its own EquipmentCommandLedger
                // table name (the EquippedEntry table itself does not contain the
                // substring "Equipment"); no Equip/Unequip command, ActiveEffect,
                // Attack, or ItemDefinitionMigration table may exist.
                string[] allowedEquipmentTables = { "EquipmentCommandLedger", "ItemDefinitionMigrationCommandLedger" };
                string[] forbidden = { "Equipment", "ActiveEffect", "Attack", "ItemDefinitionMigration" };
                foreach (string tableName in tableNames)
                {
                    if (allowedEquipmentTables.Contains(tableName)) continue;

                    foreach (string forbiddenName in forbidden)
                    {
                        Assert.That(tableName, Does.Not.Contain(forbiddenName));
                    }
                }
            }

            // No type in this assembly is named with the substring "Equipment" (the
            // ODY-S05-302 table name is a private SQL string literal, not a type), so
            // this check still forbids a future Equip/Unequip command class by name.
            // ODY-S05-502 legitimately introduces the standalone ActiveEffect
            // aggregate/repository elsewhere in this assembly (not owned by
            // Inventory, ADR-028 section 8.2 rule 2) -- every other forbidden
            // fragment still applies to every type name in this assembly.
            string[] forbiddenTypeFragments = { "Equipment", "Attack", "ItemDefinitionMigration" };
            IEnumerable<string> persistenceTypeNames = typeof(SqliteInventoryRepository).Assembly.GetTypes().Select(t => t.Name);
            foreach (string typeName in persistenceTypeNames)
            {
                if (typeName == "SqliteAttackStateReader") continue; // ODY-S05-603 read-only adapter; it owns no command or mutation path.
                if (typeName == "SqliteAttackApplyRepository") continue; // ODY-S05-604 standalone attack-outcome repository; not an Equipment/Inventory command.
                foreach (string forbidden in forbiddenTypeFragments)
                {
                    Assert.That(typeName, Does.Not.Contain(forbidden));
                }
            }
        }

        [Test]
        public void CreateItemInstance_DoesNotInspectCatalogStatusOrTypedDefinitionValidity()
        {
            InventoryRecord inventory = CreateInventory();
            ContentDefinitionRef missingSource = new ContentDefinitionRef(ContentDefinitionId.NewId(Clock.GetUtcNow()), 99);
            var invalidTypedSnapshot = new ItemMechanicsSnapshot(missingSource, 1, ContentDefinitionType.Weapon, "not-a-typed-definition-json");
            var record = new ItemInstanceRecord(
                ItemInstanceId.NewId(Clock.GetUtcNow()),
                inventory.CampaignId,
                inventory.InventoryId,
                inventory.OwnerRef,
                InventoryLocationRef.Contained(inventory.InventoryId, "main"),
                missingSource,
                invalidTypedSnapshot,
                "{\"runtime\":\"opaque\"}",
                1,
                Clock.GetUtcNow(),
                Clock.GetUtcNow());

            Result<ItemInstanceRecord> created = _inventoryRepository.CreateItemInstance(_campaign, record, NewCommandId(), TestCorrelationId);
            Result<ItemInstanceRecord> fetched = _inventoryRepository.GetItemInstance(_campaign, record.ItemInstanceId, TestCorrelationId);

            Assert.That(created.IsSuccess, Is.True);
            Assert.That(fetched.IsSuccess, Is.True);
            Assert.That(fetched.Value.SourceItemDefinitionRef, Is.EqualTo(missingSource));
            Assert.That(fetched.Value.MechanicsSnapshot.Payload, Is.EqualTo("not-a-typed-definition-json"));
            Assert.That(TableExists("ContentDefinition"), Is.False, "Inventory persistence must not create or inspect the catalog table");
        }

        private InventoryRecord CreateInventory()
        {
            InventoryRecord record = NewInventoryRecord();
            Result<InventoryRecord> created = _inventoryRepository.CreateInventory(_campaign, record, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        private ItemInstanceRecord CreateItemInstance(InventoryRecord inventory, string containerKey)
        {
            ItemInstanceRecord record = NewItemInstanceRecord(inventory, containerKey);
            Result<ItemInstanceRecord> created = _inventoryRepository.CreateItemInstance(_campaign, record, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        private ItemStackRecord CreateItemStack(InventoryRecord inventory, string containerKey)
        {
            ItemStackRecord record = NewItemStackRecord(inventory, containerKey: containerKey);
            Result<ItemStackRecord> created = _inventoryRepository.CreateItemStack(_campaign, record, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        private ItemInstanceRecord CreateItemInstanceWithDefinition(InventoryRecord inventory, ContentDefinitionRef sourceRef)
        {
            UtcInstant now = Clock.GetUtcNow();
            var snapshot = new ItemMechanicsSnapshot(sourceRef, sourceRef.Version, ContentDefinitionType.Item, "{\"mechanics\":\"copied\"}");
            var record = new ItemInstanceRecord(
                ItemInstanceId.NewId(now),
                inventory.CampaignId,
                inventory.InventoryId,
                inventory.OwnerRef,
                InventoryLocationRef.Contained(inventory.InventoryId, "main"),
                sourceRef,
                snapshot,
                "{}",
                1,
                now,
                now);
            Result<ItemInstanceRecord> created = _inventoryRepository.CreateItemInstance(_campaign, record, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        private ItemStackRecord CreateItemStackWithDefinition(InventoryRecord inventory, ContentDefinitionRef sourceRef)
        {
            UtcInstant now = Clock.GetUtcNow();
            var snapshot = new ItemMechanicsSnapshot(sourceRef, sourceRef.Version, ContentDefinitionType.Ammo, "{\"damage\":\"copied\"}");
            var record = new ItemStackRecord(
                ItemStackId.NewId(now),
                inventory.CampaignId,
                inventory.InventoryId,
                inventory.OwnerRef,
                InventoryLocationRef.Contained(inventory.InventoryId, "main"),
                sourceRef,
                snapshot,
                ItemStackQuantity.Create(1),
                "{\"stack\":\"opaque\"}",
                1,
                now,
                now);
            Result<ItemStackRecord> created = _inventoryRepository.CreateItemStack(_campaign, record, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        private InventoryRecord NewInventoryRecord(CampaignId? campaignId = null)
        {
            UtcInstant now = Clock.GetUtcNow();
            return new InventoryRecord(
                InventoryId.NewId(now),
                campaignId ?? _campaign.CampaignId,
                InventoryOwnerRef.ForCharacter(CharacterId.NewId(now)),
                1,
                now,
                now);
        }

        private static CampaignId OtherCampaignId()
        {
            return CampaignId.NewId(Clock.GetUtcNow());
        }

        private static ItemInstanceRecord NewItemInstanceRecord(InventoryRecord inventory, string containerKey = "main")
        {
            UtcInstant now = Clock.GetUtcNow();
            ContentDefinitionRef sourceRef = new ContentDefinitionRef(ContentDefinitionId.NewId(now), 3);
            var snapshot = new ItemMechanicsSnapshot(sourceRef, 3, ContentDefinitionType.Item, "{\"mechanics\":\"copied\"}");
            return new ItemInstanceRecord(
                ItemInstanceId.NewId(now),
                inventory.CampaignId,
                inventory.InventoryId,
                inventory.OwnerRef,
                InventoryLocationRef.Contained(inventory.InventoryId, containerKey),
                sourceRef,
                snapshot,
                "{\"durability\":10}",
                1,
                now,
                now);
        }

        private static ItemStackRecord NewItemStackRecord(InventoryRecord inventory, long quantity = 4, string containerKey = "main")
        {
            UtcInstant now = Clock.GetUtcNow();
            ContentDefinitionRef sourceRef = new ContentDefinitionRef(ContentDefinitionId.NewId(now), 2);
            var snapshot = new ItemMechanicsSnapshot(sourceRef, 2, ContentDefinitionType.Ammo, "{\"damage\":\"copied\"}");
            return new ItemStackRecord(
                ItemStackId.NewId(now),
                inventory.CampaignId,
                inventory.InventoryId,
                inventory.OwnerRef,
                InventoryLocationRef.Contained(inventory.InventoryId, containerKey),
                sourceRef,
                snapshot,
                ItemStackQuantity.Create(quantity),
                "{\"stack\":\"opaque\"}",
                1,
                now,
                now);
        }

        private static void AssertInventoryEquals(InventoryRecord expected, InventoryRecord actual)
        {
            Assert.That(actual.InventoryId, Is.EqualTo(expected.InventoryId));
            Assert.That(actual.CampaignId, Is.EqualTo(expected.CampaignId));
            Assert.That(actual.OwnerRef, Is.EqualTo(expected.OwnerRef));
            Assert.That(actual.Revision, Is.EqualTo(expected.Revision));
            Assert.That(actual.CreatedAt, Is.EqualTo(expected.CreatedAt));
            Assert.That(actual.UpdatedAt, Is.EqualTo(expected.UpdatedAt));
        }

        private static void AssertItemInstanceEquals(ItemInstanceRecord expected, ItemInstanceRecord actual)
        {
            Assert.That(actual.ItemInstanceId, Is.EqualTo(expected.ItemInstanceId));
            Assert.That(actual.CampaignId, Is.EqualTo(expected.CampaignId));
            Assert.That(actual.InventoryId, Is.EqualTo(expected.InventoryId));
            Assert.That(actual.OwnerRef, Is.EqualTo(expected.OwnerRef));
            Assert.That(actual.LocationRef, Is.EqualTo(expected.LocationRef));
            Assert.That(actual.SourceItemDefinitionRef, Is.EqualTo(expected.SourceItemDefinitionRef));
            Assert.That(actual.MechanicsSnapshot, Is.EqualTo(expected.MechanicsSnapshot));
            Assert.That(actual.RuntimeState, Is.EqualTo(expected.RuntimeState));
            Assert.That(actual.Revision, Is.EqualTo(expected.Revision));
            Assert.That(actual.CreatedAt, Is.EqualTo(expected.CreatedAt));
            Assert.That(actual.UpdatedAt, Is.EqualTo(expected.UpdatedAt));
        }

        private static void AssertItemStackEquals(ItemStackRecord expected, ItemStackRecord actual)
        {
            Assert.That(actual.ItemStackId, Is.EqualTo(expected.ItemStackId));
            Assert.That(actual.CampaignId, Is.EqualTo(expected.CampaignId));
            Assert.That(actual.InventoryId, Is.EqualTo(expected.InventoryId));
            Assert.That(actual.OwnerRef, Is.EqualTo(expected.OwnerRef));
            Assert.That(actual.LocationRef, Is.EqualTo(expected.LocationRef));
            Assert.That(actual.SourceItemDefinitionRef, Is.EqualTo(expected.SourceItemDefinitionRef));
            Assert.That(actual.MechanicsSnapshot, Is.EqualTo(expected.MechanicsSnapshot));
            Assert.That(actual.Quantity, Is.EqualTo(expected.Quantity));
            Assert.That(actual.StackState, Is.EqualTo(expected.StackState));
            Assert.That(actual.Revision, Is.EqualTo(expected.Revision));
            Assert.That(actual.CreatedAt, Is.EqualTo(expected.CreatedAt));
            Assert.That(actual.UpdatedAt, Is.EqualTo(expected.UpdatedAt));
        }

        private void SetInventoryRevisionDirectly(InventoryId inventoryId, long revision)
        {
            using SqliteConnection connection = OpenRawConnection();
            using var update = connection.CreateCommand();
            update.CommandText = "UPDATE Inventory SET Revision = $revision WHERE InventoryId = $inventoryId;";
            update.Parameters.AddWithValue("$revision", revision);
            update.Parameters.AddWithValue("$inventoryId", inventoryId.ToString());
            update.ExecuteNonQuery();
        }

        private long CountRows(string tableName, string idColumn, string id)
        {
            using SqliteConnection connection = OpenRawConnection();
            using var count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM " + tableName + " WHERE " + idColumn + " = $id;";
            count.Parameters.AddWithValue("$id", id);
            return (long)count.ExecuteScalar()!;
        }

        private bool TableExists(string tableName)
        {
            using SqliteConnection connection = OpenRawConnection();
            using var select = connection.CreateCommand();
            select.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";
            select.Parameters.AddWithValue("$name", tableName);
            return select.ExecuteScalar() != null;
        }

        private bool HasForeignKey(string tableName, string fromColumn, string referencedTable, string referencedColumn)
        {
            using SqliteConnection connection = OpenRawConnection();
            using var select = connection.CreateCommand();
            select.CommandText = "PRAGMA foreign_key_list(" + tableName + ");";
            using SqliteDataReader reader = select.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(2), referencedTable, StringComparison.Ordinal) &&
                    string.Equals(reader.GetString(3), fromColumn, StringComparison.Ordinal) &&
                    string.Equals(reader.GetString(4), referencedColumn, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private SqliteConnection OpenRawConnection()
        {
            var connection = new SqliteConnection("Data Source=" + Path.Combine(_campaignDir, "campaign.db"));
            connection.Open();
            return connection;
        }
    }
}
