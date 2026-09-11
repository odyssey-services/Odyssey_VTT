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
using Odyssey.Domain.Character;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S05-302: real SQLite tests for the Equipment persistence
    /// foundation only. No Equip/Unequip command service, MainGM/authorization
    /// check, rule-4 body-part-existence check, or `RemoveBodyPart`
    /// dependency behavior is introduced here.
    /// </summary>
    public sealed class SqliteEquipmentRepositoryTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;
        private SqliteInventoryRepository _inventoryRepository = null!;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s05-302-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> created = _campaignRepository.Create(new CreateCampaignRequest(_campaignDir, "Equipment Persistence Test Campaign", "ruleset.core", "1.0.0", "0.1.0"), NewCommandId(), TestCorrelationId);
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

        [Test] // TC-INVENTORY-101
        public void CreateEquippedEntry_ForItemInstance_ThenGetEquippedEntry_RoundTrips()
        {
            InventoryRecord inventory = CreateInventory();
            EquippedEntryRecord entry = NewEquippedEntryRecord(inventory, InventoryItemRef.ForInstance(ItemInstanceId.NewId(Clock.GetUtcNow())));

            Result<EquippedEntryRecord> created = _inventoryRepository.CreateEquippedEntry(_campaign, entry, NewCommandId(), TestCorrelationId);
            Result<EquippedEntryRecord> fetched = _inventoryRepository.GetEquippedEntry(_campaign, entry.Entry.ItemRef, TestCorrelationId);

            Assert.That(created.IsSuccess, Is.True);
            Assert.That(fetched.IsSuccess, Is.True);
            AssertEquippedEntryEquals(entry, fetched.Value);
        }

        [Test] // TC-INVENTORY-102
        public void CreateEquippedEntry_ForItemStack_ThenGetEquippedEntry_RoundTrips()
        {
            InventoryRecord inventory = CreateInventory();
            EquippedEntryRecord entry = NewEquippedEntryRecord(inventory, InventoryItemRef.ForStack(ItemStackId.NewId(Clock.GetUtcNow())), bodyPartRefs: Array.Empty<BodyPartId>());

            Result<EquippedEntryRecord> created = _inventoryRepository.CreateEquippedEntry(_campaign, entry, NewCommandId(), TestCorrelationId);
            Result<EquippedEntryRecord> fetched = _inventoryRepository.GetEquippedEntry(_campaign, entry.Entry.ItemRef, TestCorrelationId);

            Assert.That(created.IsSuccess, Is.True);
            Assert.That(fetched.IsSuccess, Is.True);
            AssertEquippedEntryEquals(entry, fetched.Value);
        }

        [Test] // TC-INVENTORY-103
        public void CreateEquippedEntry_WithMismatchedCampaignId_IsRejected()
        {
            InventoryRecord inventory = CreateInventory();
            var mismatched = new EquippedEntryRecord(OtherCampaignId(), NewEquippedEntryRecord(inventory).Entry);

            Result<EquippedEntryRecord> created = _inventoryRepository.CreateEquippedEntry(_campaign, mismatched, NewCommandId(), TestCorrelationId);

            Assert.That(created.IsFailure, Is.True);
            Assert.That(created.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryCampaignMismatch));
            Assert.That(CountRows("EquippedEntry", "ItemRefId", ItemRefIdOf(mismatched.Entry.ItemRef)), Is.EqualTo(0));
        }

        [Test] // TC-INVENTORY-104
        public void CreateEquippedEntry_WithMissingParentInventory_IsRejected()
        {
            InventoryRecord inventory = NewInventoryRecord();
            EquippedEntryRecord entry = NewEquippedEntryRecord(inventory);

            Result<EquippedEntryRecord> created = _inventoryRepository.CreateEquippedEntry(_campaign, entry, NewCommandId(), TestCorrelationId);

            Assert.That(created.IsFailure, Is.True);
            Assert.That(created.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryNotFound));
            Assert.That(CountRows("EquippedEntry", "ItemRefId", ItemRefIdOf(entry.Entry.ItemRef)), Is.EqualTo(0));
        }

        [Test] // TC-INVENTORY-105
        public void CreateEquippedEntry_ForAlreadyEquippedItem_WithDifferentCommandId_IsRejected()
        {
            InventoryRecord inventory = CreateInventory();
            InventoryItemRef itemRef = InventoryItemRef.ForInstance(ItemInstanceId.NewId(Clock.GetUtcNow()));
            EquippedEntryRecord first = NewEquippedEntryRecord(inventory, itemRef, slotRef: "chest_slot");
            EquippedEntryRecord second = NewEquippedEntryRecord(inventory, itemRef, slotRef: "belt_slot");

            Result<EquippedEntryRecord> firstResult = _inventoryRepository.CreateEquippedEntry(_campaign, first, NewCommandId(), TestCorrelationId);
            Result<EquippedEntryRecord> secondResult = _inventoryRepository.CreateEquippedEntry(_campaign, second, NewCommandId(), TestCorrelationId);

            Assert.That(firstResult.IsSuccess, Is.True);
            Assert.That(secondResult.IsFailure, Is.True);
            Assert.That(secondResult.Error.Code, Is.EqualTo(ErrorCodes.PersistenceEquipmentEntryAlreadyEquipped));
            Assert.That(CountRows("EquippedEntry", "ItemRefId", ItemRefIdOf(itemRef)), Is.EqualTo(1));

            Result<EquippedEntryRecord> stillFirst = _inventoryRepository.GetEquippedEntry(_campaign, itemRef, TestCorrelationId);
            Assert.That(stillFirst.Value.Entry.EquipmentSlotRef, Is.EqualTo("chest_slot"), "the rejected create must not overwrite the existing row");
        }

        [Test] // TC-INVENTORY-106
        public void CreateEquippedEntry_ReplayWithSameCommandId_ReturnsCurrentRecordWithoutDuplicate()
        {
            InventoryRecord inventory = CreateInventory();
            EquippedEntryRecord entry = NewEquippedEntryRecord(inventory);
            CommandId commandId = NewCommandId();

            Result<EquippedEntryRecord> first = _inventoryRepository.CreateEquippedEntry(_campaign, entry, commandId, TestCorrelationId);
            Result<EquippedEntryRecord> replay = _inventoryRepository.CreateEquippedEntry(_campaign, entry, commandId, TestCorrelationId);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(replay.IsSuccess, Is.True);
            AssertEquippedEntryEquals(entry, replay.Value);
            Assert.That(CountRows("EquippedEntry", "ItemRefId", ItemRefIdOf(entry.Entry.ItemRef)), Is.EqualTo(1));
        }

        [Test] // TC-INVENTORY-107
        public void CreateEquippedEntry_WithSameCommandIdForDifferentTarget_IsRejected()
        {
            InventoryRecord inventory = CreateInventory();
            EquippedEntryRecord first = NewEquippedEntryRecord(inventory, InventoryItemRef.ForInstance(ItemInstanceId.NewId(Clock.GetUtcNow())));
            EquippedEntryRecord second = NewEquippedEntryRecord(inventory, InventoryItemRef.ForInstance(ItemInstanceId.NewId(Clock.GetUtcNow())));
            CommandId commandId = NewCommandId();

            Result<EquippedEntryRecord> firstResult = _inventoryRepository.CreateEquippedEntry(_campaign, first, commandId, TestCorrelationId);
            Result<EquippedEntryRecord> secondResult = _inventoryRepository.CreateEquippedEntry(_campaign, second, commandId, TestCorrelationId);

            Assert.That(firstResult.IsSuccess, Is.True);
            Assert.That(secondResult.IsFailure, Is.True);
            Assert.That(secondResult.Error.Code, Is.EqualTo(ErrorCodes.CommandIdentityMismatch));
            Assert.That(CountRows("EquippedEntry", "ItemRefId", ItemRefIdOf(second.Entry.ItemRef)), Is.EqualTo(0));
        }

        [Test] // TC-INVENTORY-108
        public void ReplaceEquippedEntry_UpdatesSlotAndBodyPartRefs_WithCorrectRevision()
        {
            InventoryRecord inventory = CreateInventory();
            EquippedEntryRecord original = CreateEquippedEntry(inventory, slotRef: "chest_slot", bodyPartRefs: new[] { BodyPartId.Parse("Torso") });
            EquippedEntryRecord replacement = new EquippedEntryRecord(
                original.CampaignId,
                new EquippedEntry(inventory.InventoryId, original.Entry.ItemRef, "belt_slot", new[] { BodyPartId.Parse("LeftArm") }, original.Entry.EquippedByUserId, original.Entry.EquippedAt, original.Entry.Revision));

            Result<EquippedEntryRecord> replaced = _inventoryRepository.ReplaceEquippedEntry(_campaign, replacement, original.Entry.Revision, NewCommandId(), TestCorrelationId);

            Assert.That(replaced.IsSuccess, Is.True);
            Assert.That(replaced.Value.Entry.EquipmentSlotRef, Is.EqualTo("belt_slot"));
            Assert.That(replaced.Value.Entry.BodyPartRefs, Is.EqualTo(new[] { BodyPartId.Parse("LeftArm") }));
            Assert.That(replaced.Value.Entry.Revision, Is.EqualTo(original.Entry.Revision + 1));
        }

        [Test] // TC-INVENTORY-109
        public void ReplaceEquippedEntry_WithStaleRevision_IsRejectedAsRevisionConflict()
        {
            InventoryRecord inventory = CreateInventory();
            EquippedEntryRecord original = CreateEquippedEntry(inventory, slotRef: "chest_slot");
            EquippedEntryRecord replacement = new EquippedEntryRecord(
                original.CampaignId,
                new EquippedEntry(inventory.InventoryId, original.Entry.ItemRef, "belt_slot", Array.Empty<BodyPartId>(), original.Entry.EquippedByUserId, original.Entry.EquippedAt, original.Entry.Revision));

            Result<EquippedEntryRecord> replaced = _inventoryRepository.ReplaceEquippedEntry(_campaign, replacement, original.Entry.Revision + 41, NewCommandId(), TestCorrelationId);

            Assert.That(replaced.IsFailure, Is.True);
            Assert.That(replaced.Error.Code, Is.EqualTo(ErrorCodes.PersistenceEquipmentEntryRevisionConflict));
            Result<EquippedEntryRecord> unchanged = _inventoryRepository.GetEquippedEntry(_campaign, original.Entry.ItemRef, TestCorrelationId);
            Assert.That(unchanged.Value.Entry.EquipmentSlotRef, Is.EqualTo("chest_slot"));
            Assert.That(unchanged.Value.Entry.Revision, Is.EqualTo(original.Entry.Revision));
        }

        [Test] // TC-INVENTORY-110
        public void ReplaceEquippedEntry_ReplayWithSameCommandId_ReturnsSameOutcomeWithoutDoubleApplying()
        {
            InventoryRecord inventory = CreateInventory();
            EquippedEntryRecord original = CreateEquippedEntry(inventory, slotRef: "chest_slot");
            EquippedEntryRecord replacement = new EquippedEntryRecord(
                original.CampaignId,
                new EquippedEntry(inventory.InventoryId, original.Entry.ItemRef, "belt_slot", Array.Empty<BodyPartId>(), original.Entry.EquippedByUserId, original.Entry.EquippedAt, original.Entry.Revision));
            CommandId commandId = NewCommandId();

            Result<EquippedEntryRecord> first = _inventoryRepository.ReplaceEquippedEntry(_campaign, replacement, original.Entry.Revision, commandId, TestCorrelationId);
            Result<EquippedEntryRecord> replay = _inventoryRepository.ReplaceEquippedEntry(_campaign, replacement, original.Entry.Revision, commandId, TestCorrelationId);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.Entry.Revision, Is.EqualTo(first.Value.Entry.Revision), "replay must not apply the transition a second time");
        }

        [Test] // TC-INVENTORY-111
        public void DeleteEquippedEntry_RemovesRow_WithCorrectRevision()
        {
            InventoryRecord inventory = CreateInventory();
            EquippedEntryRecord entry = CreateEquippedEntry(inventory);

            Result<bool> deleted = _inventoryRepository.DeleteEquippedEntry(_campaign, entry.Entry.ItemRef, entry.Entry.Revision, NewCommandId(), TestCorrelationId);

            Assert.That(deleted.IsSuccess, Is.True);
            Assert.That(deleted.Value, Is.True);
            Assert.That(CountRows("EquippedEntry", "ItemRefId", ItemRefIdOf(entry.Entry.ItemRef)), Is.EqualTo(0));
        }

        [Test] // TC-INVENTORY-112
        public void DeleteEquippedEntry_WithStaleRevision_IsRejectedAsRevisionConflict()
        {
            InventoryRecord inventory = CreateInventory();
            EquippedEntryRecord entry = CreateEquippedEntry(inventory);

            Result<bool> deleted = _inventoryRepository.DeleteEquippedEntry(_campaign, entry.Entry.ItemRef, entry.Entry.Revision + 7, NewCommandId(), TestCorrelationId);

            Assert.That(deleted.IsFailure, Is.True);
            Assert.That(deleted.Error.Code, Is.EqualTo(ErrorCodes.PersistenceEquipmentEntryRevisionConflict));
            Assert.That(CountRows("EquippedEntry", "ItemRefId", ItemRefIdOf(entry.Entry.ItemRef)), Is.EqualTo(1));
        }

        [Test] // TC-INVENTORY-113
        public void DeleteEquippedEntry_ReplayWithSameCommandId_ReturnsSuccessWithoutSecondDeleteAttempt()
        {
            InventoryRecord inventory = CreateInventory();
            EquippedEntryRecord entry = CreateEquippedEntry(inventory);
            CommandId commandId = NewCommandId();

            Result<bool> first = _inventoryRepository.DeleteEquippedEntry(_campaign, entry.Entry.ItemRef, entry.Entry.Revision, commandId, TestCorrelationId);
            Result<bool> replay = _inventoryRepository.DeleteEquippedEntry(_campaign, entry.Entry.ItemRef, entry.Entry.Revision, commandId, TestCorrelationId);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value, Is.True);
        }

        [Test] // TC-INVENTORY-114
        public void ListEquippedEntries_ReturnsOnlyRequestedCampaignInventory()
        {
            InventoryRecord requested = CreateInventory();
            InventoryRecord other = CreateInventory();
            EquippedEntryRecord expected = CreateEquippedEntry(requested, slotRef: "chest_slot");
            CreateEquippedEntry(other, slotRef: "chest_slot");

            Result<IReadOnlyList<EquippedEntryRecord>> listed = _inventoryRepository.ListEquippedEntries(_campaign, requested.CampaignId, requested.InventoryId, TestCorrelationId);

            Assert.That(listed.IsSuccess, Is.True);
            Assert.That(listed.Value.Select(e => ItemRefIdOf(e.Entry.ItemRef)), Is.EquivalentTo(new[] { ItemRefIdOf(expected.Entry.ItemRef) }));
        }

        [Test] // TC-INVENTORY-115
        public void GetEquippedEntry_ForUnknownItemRef_IsNotFound()
        {
            InventoryItemRef unknown = InventoryItemRef.ForInstance(ItemInstanceId.NewId(Clock.GetUtcNow()));

            Result<EquippedEntryRecord> fetched = _inventoryRepository.GetEquippedEntry(_campaign, unknown, TestCorrelationId);

            Assert.That(fetched.IsFailure, Is.True);
            Assert.That(fetched.Error.Code, Is.EqualTo(ErrorCodes.PersistenceEquipmentEntryNotFound));
        }

        [Test] // TC-INVENTORY-116
        public void EquipmentSchema_ContainsOnlyAllowedEquipmentTablesAndIndex()
        {
            CreateEquippedEntry(CreateInventory());

            using SqliteConnection connection = OpenRawConnection();
            var names = new List<(string Type, string Name)>();
            using (var select = connection.CreateCommand())
            {
                select.CommandText = "SELECT type, name FROM sqlite_master WHERE name NOT LIKE 'sqlite_autoindex_%' AND (name LIKE '%Equip%') ORDER BY type, name;";
                using SqliteDataReader reader = select.ExecuteReader();
                while (reader.Read()) names.Add((reader.GetString(0), reader.GetString(1)));
            }

            string[] allowed = { "EquippedEntry", "EquipmentCommandLedger", "IX_EquippedEntry_Campaign_Inventory" };
            Assert.That(names.Select(n => n.Name), Is.EquivalentTo(allowed));
            Assert.That(names.Where(n => n.Type == "table").Select(n => n.Name), Is.EquivalentTo(new[] { "EquipmentCommandLedger", "EquippedEntry" }));
        }

        [Test] // TC-INVENTORY-117
        public void EquipmentPersistence_IntroducesNoEquipCommandOrRemoveBodyPartBehavior()
        {
            string[] forbiddenTypeFragments = { "EquipCommand", "UnequipCommand", "RemoveBodyPart", "ActiveEffect", "Attack" };
            IEnumerable<string> persistenceTypeNames = typeof(SqliteInventoryRepository).Assembly.GetTypes().Select(t => t.Name);
            foreach (string typeName in persistenceTypeNames)
            {
                foreach (string forbidden in forbiddenTypeFragments)
                {
                    Assert.That(typeName, Does.Not.Contain(forbidden));
                }
            }
        }

        private InventoryRecord CreateInventory()
        {
            InventoryRecord record = NewInventoryRecord();
            Result<InventoryRecord> created = _inventoryRepository.CreateInventory(_campaign, record, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        private EquippedEntryRecord CreateEquippedEntry(InventoryRecord inventory, string slotRef = "chest_slot", IReadOnlyList<BodyPartId>? bodyPartRefs = null)
        {
            EquippedEntryRecord record = NewEquippedEntryRecord(inventory, slotRef: slotRef, bodyPartRefs: bodyPartRefs);
            Result<EquippedEntryRecord> created = _inventoryRepository.CreateEquippedEntry(_campaign, record, NewCommandId(), TestCorrelationId);
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

        private static CampaignId OtherCampaignId() => CampaignId.NewId(Clock.GetUtcNow());

        private EquippedEntryRecord NewEquippedEntryRecord(InventoryRecord inventory, InventoryItemRef? itemRef = null, string slotRef = "chest_slot", IReadOnlyList<BodyPartId>? bodyPartRefs = null)
        {
            UtcInstant now = Clock.GetUtcNow();
            var entry = new EquippedEntry(
                inventory.InventoryId,
                itemRef ?? InventoryItemRef.ForInstance(ItemInstanceId.NewId(now)),
                slotRef,
                bodyPartRefs ?? new[] { BodyPartId.Parse("Torso") },
                NewUserId(),
                now,
                1);
            return new EquippedEntryRecord(inventory.CampaignId, entry);
        }

        private static string ItemRefIdOf(InventoryItemRef itemRef) =>
            itemRef.Kind == InventoryItemRefKind.ItemInstance ? itemRef.ItemInstanceId.ToString() : itemRef.ItemStackId.ToString();

        private static void AssertEquippedEntryEquals(EquippedEntryRecord expected, EquippedEntryRecord actual)
        {
            Assert.That(actual.CampaignId, Is.EqualTo(expected.CampaignId));
            Assert.That(actual.Entry.InventoryId, Is.EqualTo(expected.Entry.InventoryId));
            Assert.That(actual.Entry.ItemRef, Is.EqualTo(expected.Entry.ItemRef));
            Assert.That(actual.Entry.EquipmentSlotRef, Is.EqualTo(expected.Entry.EquipmentSlotRef));
            Assert.That(actual.Entry.BodyPartRefs, Is.EqualTo(expected.Entry.BodyPartRefs));
            Assert.That(actual.Entry.EquippedByUserId, Is.EqualTo(expected.Entry.EquippedByUserId));
            Assert.That(actual.Entry.EquippedAt, Is.EqualTo(expected.Entry.EquippedAt));
            Assert.That(actual.Entry.Revision, Is.EqualTo(expected.Entry.Revision));
        }

        private long CountRows(string tableName, string idColumn, string id)
        {
            using SqliteConnection connection = OpenRawConnection();
            using var count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM " + tableName + " WHERE " + idColumn + " = $id;";
            count.Parameters.AddWithValue("$id", id);
            return (long)count.ExecuteScalar()!;
        }

        private SqliteConnection OpenRawConnection()
        {
            var connection = new SqliteConnection("Data Source=" + Path.Combine(_campaignDir, "campaign.db"));
            connection.Open();
            return connection;
        }
    }
}
