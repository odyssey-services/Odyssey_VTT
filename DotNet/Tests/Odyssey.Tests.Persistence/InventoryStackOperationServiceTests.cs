using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Inventory;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S05-205: SQLite-backed tests for MainGM-only atomic stack split and
    /// merge. Split preserves total quantity and every pinned mechanic; merge
    /// accepts only mechanically identical contained stacks and removes its
    /// consumed source inside the same transaction. No equipment, drop/pickup,
    /// item use, ActiveEffect, attack, or generic delete surface is exercised.
    /// </summary>
    public sealed class InventoryStackOperationServiceTests
    {
        private static readonly CorrelationId CorrelationId = Odyssey.Domain.Identity.CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly UserId Actor = UserId.Parse("user_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();

        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaigns = null!;
        private SqliteInventoryRepository _repository = null!;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s05-205-" + Guid.NewGuid().ToString("N"));
            _campaigns = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> created = _campaigns.Create(new CreateCampaignRequest(_campaignDir, "Inventory Stack Split/Merge Test", "ruleset.core", "1.0.0", "0.1.0"), NewCommandId(), CorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _repository = new SqliteInventoryRepository(Clock);
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaigns.Close(_campaign, CorrelationId); } catch (IOException) { }
            try { if (Directory.Exists(_campaignDir)) Directory.Delete(_campaignDir, true); } catch (IOException) { }
        }

        // ---------- split ----------

        [Test] // TC-INVENTORY-061
        public void Split_ProperSubset_PreservesTotalQuantityAndPinnedMechanics()
        {
            InventoryRecord inventory = CreateInventory();
            ItemStackRecord source = CreateStack(inventory, "main", quantity: 10);

            ItemStackId resultId = ItemStackId.NewId(Clock.GetUtcNow());
            Result<ItemStackRecord> split = InventoryStackOperationService.Split(_repository, SplitRequest(inventory, source, resultId, quantity: 3));

            Assert.That(split.IsSuccess, Is.True);
            ItemStackRecord newStack = split.Value;
            ItemStackRecord remaining = _repository.GetItemStack(_campaign, source.ItemStackId, CorrelationId).Value;

            Assert.That(newStack.ItemStackId, Is.EqualTo(resultId));
            Assert.That(newStack.Quantity.Value, Is.EqualTo(3));
            Assert.That(remaining.Quantity.Value, Is.EqualTo(7));
            Assert.That(newStack.Quantity.Value + remaining.Quantity.Value, Is.EqualTo(source.Quantity.Value));
            Assert.That(remaining.Revision, Is.EqualTo(2));
            Assert.That(newStack.Revision, Is.EqualTo(1));

            Assert.That(newStack.SourceItemDefinitionRef, Is.EqualTo(source.SourceItemDefinitionRef));
            Assert.That(newStack.MechanicsSnapshot, Is.EqualTo(source.MechanicsSnapshot));
            Assert.That(newStack.StackState, Is.EqualTo(source.StackState));
            Assert.That(newStack.OwnerRef, Is.EqualTo(source.OwnerRef));
            Assert.That(newStack.InventoryId, Is.EqualTo(source.InventoryId));
            Assert.That(newStack.LocationRef, Is.EqualTo(source.LocationRef));
            Assert.That(GetInventoryRevision(inventory), Is.EqualTo(1), "split does not move an item between inventories, so it does not bump Inventory.Revision");
        }

        [Test] // TC-INVENTORY-062
        public void Split_DeniedForNonMainGm_BeforeAnyRepositoryCall_AndNullArgumentsThrow()
        {
            var probe = new ThrowingInventoryRepository();
            SplitItemStackRequest request = new SplitItemStackRequest(_campaign, ItemStackId.NewId(Clock.GetUtcNow()), ItemStackId.NewId(Clock.GetUtcNow()), InventoryId.NewId(Clock.GetUtcNow()), 1, 1, 1, Actor, actorIsMainGm: false, NewCommandId(), CorrelationId);

            Result<ItemStackRecord> denied = InventoryStackOperationService.Split(probe, request);

            Assert.That(denied.IsFailure, Is.True);
            Assert.That(denied.Error.Code, Is.EqualTo(ErrorCodes.InventoryMoveDenied));
            Assert.That(probe.CallCount, Is.EqualTo(0));
            Assert.Throws<ArgumentNullException>(new Action(() => InventoryStackOperationService.Split(null!, request)));
            Assert.Throws<ArgumentNullException>(new Action(() => InventoryStackOperationService.Split(probe, null!)));
        }

        [Test] // TC-INVENTORY-063
        public void Split_QuantityEqualToWholeSource_IsRejectedWithoutMutation()
        {
            InventoryRecord inventory = CreateInventory();
            ItemStackRecord source = CreateStack(inventory, "main", quantity: 5);

            Result<ItemStackRecord> split = InventoryStackOperationService.Split(_repository, SplitRequest(inventory, source, ItemStackId.NewId(Clock.GetUtcNow()), quantity: 5));

            Assert.That(split.IsFailure, Is.True);
            Assert.That(split.Error.Code, Is.EqualTo(ErrorCodes.InventoryStackSplitQuantityInvalid));
            AssertStackUnchanged(source);
            Assert.That(CountRows("InventoryStackCommandLedger"), Is.EqualTo(0));
        }

        [Test] // TC-INVENTORY-064
        public void Split_QuantityGreaterThanSource_IsRejectedWithoutMutation()
        {
            InventoryRecord inventory = CreateInventory();
            ItemStackRecord source = CreateStack(inventory, "main", quantity: 4);

            Result<ItemStackRecord> split = InventoryStackOperationService.Split(_repository, SplitRequest(inventory, source, ItemStackId.NewId(Clock.GetUtcNow()), quantity: 9));

            Assert.That(split.IsFailure, Is.True);
            Assert.That(split.Error.Code, Is.EqualTo(ErrorCodes.InventoryStackSplitQuantityInvalid));
            AssertStackUnchanged(source);
        }

        [Test] // TC-INVENTORY-065
        public void Split_StaleSourceRevision_IsRejectedWithoutMutation()
        {
            InventoryRecord inventory = CreateInventory();
            ItemStackRecord source = CreateStack(inventory, "main", quantity: 6);

            Result<ItemStackRecord> split = InventoryStackOperationService.Split(_repository, SplitRequest(inventory, source, ItemStackId.NewId(Clock.GetUtcNow()), quantity: 2, expectedSourceRevision: 2));

            Assert.That(split.IsFailure, Is.True);
            Assert.That(split.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryItemRevisionConflict));
            AssertStackUnchanged(source);
            Assert.That(CountRows("InventoryStackCommandLedger"), Is.EqualTo(0));
        }

        [Test] // TC-INVENTORY-066
        public void Split_MissingSourceStack_IsRejected()
        {
            InventoryRecord inventory = CreateInventory();
            ItemStackId missing = ItemStackId.NewId(Clock.GetUtcNow());

            SplitItemStackRequest request = new SplitItemStackRequest(_campaign, missing, ItemStackId.NewId(Clock.GetUtcNow()), inventory.InventoryId, 1, 1, 1, Actor, actorIsMainGm: true, NewCommandId(), CorrelationId);
            Result<ItemStackRecord> split = InventoryStackOperationService.Split(_repository, request);

            Assert.That(split.IsFailure, Is.True);
            Assert.That(split.Error.Code, Is.EqualTo(ErrorCodes.PersistenceItemStackNotFound));
        }

        [Test] // TC-INVENTORY-067
        public void Split_StaleInventoryRevision_IsRejectedWithoutMutation()
        {
            InventoryRecord inventory = CreateInventory();
            ItemStackRecord source = CreateStack(inventory, "main", quantity: 6);

            Result<ItemStackRecord> split = InventoryStackOperationService.Split(_repository, SplitRequest(inventory, source, ItemStackId.NewId(Clock.GetUtcNow()), quantity: 2, expectedInventoryRevision: 2));

            Assert.That(split.IsFailure, Is.True);
            Assert.That(split.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryRevisionConflict));
            AssertStackUnchanged(source);
        }

        [Test] // TC-INVENTORY-068
        public void Split_ReplayWithSameCommandIdAndParameters_ReturnsStoredResultWithoutMutatingTwice()
        {
            InventoryRecord inventory = CreateInventory();
            ItemStackRecord source = CreateStack(inventory, "main", quantity: 10);
            ItemStackId resultId = ItemStackId.NewId(Clock.GetUtcNow());
            CommandId commandId = NewCommandId();

            Result<ItemStackRecord> first = InventoryStackOperationService.Split(_repository, SplitRequest(inventory, source, resultId, quantity: 4, commandId: commandId));
            Result<ItemStackRecord> replay = InventoryStackOperationService.Split(_repository, SplitRequest(inventory, source, resultId, quantity: 4, commandId: commandId));

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.ItemStackId, Is.EqualTo(resultId));
            Assert.That(replay.Value.Quantity.Value, Is.EqualTo(4));
            Assert.That(_repository.GetItemStack(_campaign, source.ItemStackId, CorrelationId).Value.Quantity.Value, Is.EqualTo(6), "the source quantity is reduced exactly once");
            Assert.That(CountRows("ItemStack"), Is.EqualTo(2));
            Assert.That(CountRows("InventoryStackCommandLedger"), Is.EqualTo(1));
            Assert.That(GetPrimaryKeyColumn("InventoryStackCommandLedger"), Is.EqualTo("CommandId"));
        }

        [Test] // TC-INVENTORY-069
        public void Split_ReplayWithSameCommandIdButDifferentParameters_IsRejected()
        {
            InventoryRecord inventory = CreateInventory();
            ItemStackRecord source = CreateStack(inventory, "main", quantity: 10);
            CommandId commandId = NewCommandId();

            Result<ItemStackRecord> first = InventoryStackOperationService.Split(_repository, SplitRequest(inventory, source, ItemStackId.NewId(Clock.GetUtcNow()), quantity: 4, commandId: commandId));
            Result<ItemStackRecord> mismatch = InventoryStackOperationService.Split(_repository, SplitRequest(inventory, source, ItemStackId.NewId(Clock.GetUtcNow()), quantity: 5, commandId: commandId));

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(mismatch.IsFailure, Is.True);
            Assert.That(mismatch.Error.Code, Is.EqualTo(ErrorCodes.CommandIdentityMismatch));
            Assert.That(_repository.GetItemStack(_campaign, source.ItemStackId, CorrelationId).Value.Quantity.Value, Is.EqualTo(6));
            Assert.That(CountRows("ItemStack"), Is.EqualTo(2));
        }

        // ---------- merge ----------

        [Test] // TC-INVENTORY-070
        public void Merge_TwoIdenticalStacks_SumsIntoSurvivorAndRemovesConsumedSource()
        {
            InventoryRecord inventory = CreateInventory();
            (ItemStackRecord survivor, ItemStackRecord consumed) = CreateIdenticalPair(inventory, "main", survivorQuantity: 8, consumedQuantity: 5);

            Result<ItemStackRecord> merge = InventoryStackOperationService.Merge(_repository, MergeRequest(inventory, consumed, survivor));

            Assert.That(merge.IsSuccess, Is.True);
            Assert.That(merge.Value.ItemStackId, Is.EqualTo(survivor.ItemStackId));
            Assert.That(merge.Value.Quantity.Value, Is.EqualTo(13));
            Assert.That(merge.Value.Revision, Is.EqualTo(2));
            Result<ItemStackRecord> consumedLookup = _repository.GetItemStack(_campaign, consumed.ItemStackId, CorrelationId);
            Assert.That(consumedLookup.IsFailure, Is.True);
            Assert.That(consumedLookup.Error.Code, Is.EqualTo(ErrorCodes.PersistenceItemStackNotFound));
            Assert.That(CountRows("ItemStack"), Is.EqualTo(1));
            Assert.That(GetInventoryRevision(inventory), Is.EqualTo(1));
        }

        [Test] // TC-INVENTORY-071
        public void Merge_RecordsDedicatedStackLedgerRowForTheSurvivor()
        {
            InventoryRecord inventory = CreateInventory();
            (ItemStackRecord survivor, ItemStackRecord consumed) = CreateIdenticalPair(inventory, "main", survivorQuantity: 2, consumedQuantity: 2);

            Assert.That(InventoryStackOperationService.Merge(_repository, MergeRequest(inventory, consumed, survivor)).IsSuccess, Is.True);

            Assert.That(CountRows("InventoryStackCommandLedger"), Is.EqualTo(1));
            Assert.That(ReadLedgerScalar("OperationKind"), Is.EqualTo("Merge"));
            Assert.That(ReadLedgerScalar("ResultStackId"), Is.EqualTo(survivor.ItemStackId.ToString()));
        }

        [Test] // TC-INVENTORY-072
        public void Merge_DivergentMechanicsSnapshot_IsRejectedWithoutMutation()
        {
            InventoryRecord inventory = CreateInventory();
            ItemStackRecord survivor = CreateStack(inventory, "main", quantity: 3);
            ItemStackRecord consumed = CreateStack(inventory, "main", quantity: 3, payload: "{\"damage\":\"divergent\"}");

            Result<ItemStackRecord> merge = InventoryStackOperationService.Merge(_repository, MergeRequest(inventory, consumed, survivor));

            Assert.That(merge.IsFailure, Is.True);
            Assert.That(merge.Error.Code, Is.EqualTo(ErrorCodes.InventoryStackMergeMismatch));
            AssertStackUnchanged(survivor);
            AssertStackUnchanged(consumed);
            Assert.That(CountRows("InventoryStackCommandLedger"), Is.EqualTo(0));
        }

        [Test] // TC-INVENTORY-073
        public void Merge_DivergentContainedLocation_IsRejectedWithoutMutation()
        {
            InventoryRecord inventory = CreateInventory();
            ItemStackRecord survivor = CreateStack(inventory, "main", quantity: 3);
            ItemStackRecord consumed = CreateStack(inventory, "pack", quantity: 3);

            Result<ItemStackRecord> merge = InventoryStackOperationService.Merge(_repository, MergeRequest(inventory, consumed, survivor));

            Assert.That(merge.IsFailure, Is.True);
            Assert.That(merge.Error.Code, Is.EqualTo(ErrorCodes.InventoryStackMergeMismatch));
            AssertStackUnchanged(survivor);
            AssertStackUnchanged(consumed);
        }

        [Test] // TC-INVENTORY-074
        public void Merge_StaleSourceRevision_IsRejectedWithoutMutation()
        {
            InventoryRecord inventory = CreateInventory();
            (ItemStackRecord survivor, ItemStackRecord consumed) = CreateIdenticalPair(inventory, "main", survivorQuantity: 4, consumedQuantity: 4);

            Result<ItemStackRecord> merge = InventoryStackOperationService.Merge(_repository, MergeRequest(inventory, consumed, survivor, expectedSourceRevision: 2));

            Assert.That(merge.IsFailure, Is.True);
            Assert.That(merge.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryItemRevisionConflict));
            AssertStackUnchanged(survivor);
            AssertStackUnchanged(consumed);
        }

        [Test] // TC-INVENTORY-075
        public void Merge_StaleDestinationRevision_IsRejectedWithoutMutation()
        {
            InventoryRecord inventory = CreateInventory();
            (ItemStackRecord survivor, ItemStackRecord consumed) = CreateIdenticalPair(inventory, "main", survivorQuantity: 4, consumedQuantity: 4);

            Result<ItemStackRecord> merge = InventoryStackOperationService.Merge(_repository, MergeRequest(inventory, consumed, survivor, expectedDestinationRevision: 2));

            Assert.That(merge.IsFailure, Is.True);
            Assert.That(merge.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryItemRevisionConflict));
            AssertStackUnchanged(survivor);
            AssertStackUnchanged(consumed);
        }

        [Test] // TC-INVENTORY-076
        public void Merge_CombinedQuantityNotRepresentable_IsRejectedWithoutMutation()
        {
            InventoryRecord inventory = CreateInventory();
            (ItemStackRecord survivor, ItemStackRecord consumed) = CreateIdenticalPair(inventory, "main", survivorQuantity: 5, consumedQuantity: 5);
            SetStackQuantityDirectly(survivor.ItemStackId, long.MaxValue);

            Result<ItemStackRecord> merge = InventoryStackOperationService.Merge(_repository, MergeRequest(inventory, consumed, survivor));

            Assert.That(merge.IsFailure, Is.True);
            Assert.That(merge.Error.Code, Is.EqualTo(ErrorCodes.InventoryStackMergeExceedsMaxQuantity));
            Assert.That(_repository.GetItemStack(_campaign, survivor.ItemStackId, CorrelationId).Value.Quantity.Value, Is.EqualTo(long.MaxValue));
            AssertStackUnchanged(consumed);
            Assert.That(CountRows("InventoryStackCommandLedger"), Is.EqualTo(0));
        }

        [Test] // TC-INVENTORY-077
        public void Merge_ReplayWithSameCommandId_ReturnsSurvivorWithoutMergingTwice()
        {
            InventoryRecord inventory = CreateInventory();
            (ItemStackRecord survivor, ItemStackRecord consumed) = CreateIdenticalPair(inventory, "main", survivorQuantity: 6, consumedQuantity: 4);
            CommandId commandId = NewCommandId();

            Result<ItemStackRecord> first = InventoryStackOperationService.Merge(_repository, MergeRequest(inventory, consumed, survivor, commandId: commandId));
            Result<ItemStackRecord> replay = InventoryStackOperationService.Merge(_repository, MergeRequest(inventory, consumed, survivor, commandId: commandId));

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.ItemStackId, Is.EqualTo(survivor.ItemStackId));
            Assert.That(replay.Value.Quantity.Value, Is.EqualTo(10), "the survivor is not summed a second time");
            Assert.That(CountRows("ItemStack"), Is.EqualTo(1));
            Assert.That(CountRows("InventoryStackCommandLedger"), Is.EqualTo(1));
        }

        [Test] // TC-INVENTORY-078
        public void Merge_WhenSourceDeleteFails_RollsBackSurvivorSourceAndLedger()
        {
            InventoryRecord inventory = CreateInventory();
            (ItemStackRecord survivor, ItemStackRecord consumed) = CreateIdenticalPair(inventory, "main", survivorQuantity: 7, consumedQuantity: 3);
            using (SqliteConnection connection = Open())
            using (var trigger = connection.CreateCommand())
            {
                trigger.CommandText = "CREATE TRIGGER FailStackMerge BEFORE DELETE ON ItemStack BEGIN SELECT RAISE(ABORT, 'test failure'); END;";
                trigger.ExecuteNonQuery();
            }

            Result<ItemStackRecord> merge = InventoryStackOperationService.Merge(_repository, MergeRequest(inventory, consumed, survivor));

            Assert.That(merge.IsFailure, Is.True);
            Assert.That(merge.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryIoFailed));
            Assert.That(_repository.GetItemStack(_campaign, survivor.ItemStackId, CorrelationId).Value.Quantity.Value, Is.EqualTo(7));
            Assert.That(_repository.GetItemStack(_campaign, survivor.ItemStackId, CorrelationId).Value.Revision, Is.EqualTo(1));
            Assert.That(_repository.GetItemStack(_campaign, consumed.ItemStackId, CorrelationId).IsSuccess, Is.True);
            Assert.That(CountRows("InventoryStackCommandLedger"), Is.EqualTo(0));
        }

        // ---------- helpers ----------

        private InventoryRecord CreateInventory()
        {
            UtcInstant now = Clock.GetUtcNow();
            var record = new InventoryRecord(InventoryId.NewId(now), _campaign.CampaignId, InventoryOwnerRef.ForCharacter(CharacterId.NewId(now)), 1, now, now);
            Result<InventoryRecord> created = _repository.CreateInventory(_campaign, record, NewCommandId(), CorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        private ItemStackRecord CreateStack(InventoryRecord inventory, string containerKey, long quantity, string payload = "{\"damage\":\"copied\"}")
        {
            UtcInstant now = Clock.GetUtcNow();
            ContentDefinitionRef sourceRef = new ContentDefinitionRef(ContentDefinitionId.NewId(now), 2);
            var snapshot = new ItemMechanicsSnapshot(sourceRef, 2, ContentDefinitionType.Ammo, payload);
            var record = new ItemStackRecord(
                ItemStackId.NewId(now),
                _campaign.CampaignId,
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
            Assert.That(_repository.CreateItemStack(_campaign, record, NewCommandId(), CorrelationId).IsSuccess, Is.True);
            return record;
        }

        private (ItemStackRecord Survivor, ItemStackRecord Consumed) CreateIdenticalPair(InventoryRecord inventory, string containerKey, long survivorQuantity, long consumedQuantity)
        {
            UtcInstant now = Clock.GetUtcNow();
            ContentDefinitionRef sourceRef = new ContentDefinitionRef(ContentDefinitionId.NewId(now), 2);
            var snapshot = new ItemMechanicsSnapshot(sourceRef, 2, ContentDefinitionType.Ammo, "{\"damage\":\"shared\"}");
            InventoryLocationRef location = InventoryLocationRef.Contained(inventory.InventoryId, containerKey);

            ItemStackRecord Build(long quantity) => new ItemStackRecord(
                ItemStackId.NewId(Clock.GetUtcNow()),
                _campaign.CampaignId,
                inventory.InventoryId,
                inventory.OwnerRef,
                location,
                sourceRef,
                snapshot,
                ItemStackQuantity.Create(quantity),
                "{\"stack\":\"opaque\"}",
                1,
                now,
                now);

            ItemStackRecord survivor = Build(survivorQuantity);
            ItemStackRecord consumed = Build(consumedQuantity);
            Assert.That(_repository.CreateItemStack(_campaign, survivor, NewCommandId(), CorrelationId).IsSuccess, Is.True);
            Assert.That(_repository.CreateItemStack(_campaign, consumed, NewCommandId(), CorrelationId).IsSuccess, Is.True);
            return (survivor, consumed);
        }

        private SplitItemStackRequest SplitRequest(InventoryRecord inventory, ItemStackRecord source, ItemStackId resultId, long quantity, long expectedSourceRevision = 1, long expectedInventoryRevision = 1, CommandId? commandId = null)
            => new SplitItemStackRequest(_campaign, source.ItemStackId, resultId, inventory.InventoryId, quantity, expectedSourceRevision, expectedInventoryRevision, Actor, actorIsMainGm: true, commandId ?? NewCommandId(), CorrelationId);

        private MergeItemStacksRequest MergeRequest(InventoryRecord inventory, ItemStackRecord source, ItemStackRecord destination, long expectedSourceRevision = 1, long expectedDestinationRevision = 1, long expectedInventoryRevision = 1, CommandId? commandId = null)
            => new MergeItemStacksRequest(_campaign, source.ItemStackId, destination.ItemStackId, inventory.InventoryId, expectedSourceRevision, expectedDestinationRevision, expectedInventoryRevision, Actor, actorIsMainGm: true, commandId ?? NewCommandId(), CorrelationId);

        private void AssertStackUnchanged(ItemStackRecord stack)
        {
            ItemStackRecord stored = _repository.GetItemStack(_campaign, stack.ItemStackId, CorrelationId).Value;
            Assert.That(stored.Quantity.Value, Is.EqualTo(stack.Quantity.Value));
            Assert.That(stored.Revision, Is.EqualTo(stack.Revision));
            Assert.That(stored.LocationRef, Is.EqualTo(stack.LocationRef));
        }

        private long GetInventoryRevision(InventoryRecord inventory)
            => _repository.GetInventory(_campaign, inventory.InventoryId, CorrelationId).Value.Revision;

        private void SetStackQuantityDirectly(ItemStackId id, long quantity)
        {
            using SqliteConnection c = Open();
            using var update = c.CreateCommand();
            update.CommandText = "UPDATE ItemStack SET Quantity = $quantity WHERE ItemStackId = $id;";
            update.Parameters.AddWithValue("$quantity", quantity);
            update.Parameters.AddWithValue("$id", id.ToString());
            update.ExecuteNonQuery();
        }

        private string ReadLedgerScalar(string column)
        {
            using SqliteConnection c = Open();
            using var q = c.CreateCommand();
            q.CommandText = "SELECT " + column + " FROM InventoryStackCommandLedger LIMIT 1;";
            return (string)q.ExecuteScalar()!;
        }

        private long CountRows(string table)
        {
            using SqliteConnection c = Open();
            using var q = c.CreateCommand();
            q.CommandText = "SELECT COUNT(*) FROM " + table + ";";
            return (long)q.ExecuteScalar()!;
        }

        private string GetPrimaryKeyColumn(string table)
        {
            using SqliteConnection c = Open();
            using var q = c.CreateCommand();
            q.CommandText = "PRAGMA table_info(" + table + ");";
            using SqliteDataReader r = q.ExecuteReader();
            while (r.Read())
            {
                if (r.GetInt64(5) == 1) return r.GetString(1);
            }

            return string.Empty;
        }

        private SqliteConnection Open()
        {
            var c = new SqliteConnection("Data Source=" + Path.Combine(_campaignDir, "campaign.db"));
            c.Open();
            return c;
        }

        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));

        private sealed class ThrowingInventoryRepository : IInventoryRepository
        {
            public int CallCount { get; private set; }

            private Exception Reached()
            {
                CallCount++;
                return new InvalidOperationException("The repository must not be reached when authorization fails.");
            }

            public Result<ItemDefinitionMigrationApplyResult> ApplyItemDefinitionMigration(CampaignHandle campaign, ItemDefinitionMigrationTransition transition, UserId actorUserId, bool actorIsMainGm, CorrelationId correlationId) => throw Reached();
            public Result<InventoryRecord> CreateInventory(CampaignHandle campaign, InventoryRecord record, CommandId commandId, CorrelationId correlationId) => throw Reached();
            public Result<InventoryRecord> GetInventory(CampaignHandle campaign, InventoryId inventoryId, CorrelationId correlationId) => throw Reached();
            public Result<ItemInstanceRecord> CreateItemInstance(CampaignHandle campaign, ItemInstanceRecord record, CommandId commandId, CorrelationId correlationId) => throw Reached();
            public Result<ItemInstanceRecord> MoveItemInstance(CampaignHandle campaign, InventoryMove move, CorrelationId correlationId) => throw Reached();
            public Result<InventoryCreateReplay<ItemInstanceRecord>> TryReplayCreateItemInstance(CampaignHandle campaign, CommandId commandId, ItemInstanceId itemInstanceId, CorrelationId correlationId) => throw Reached();
            public Result<ItemInstanceRecord> GetItemInstance(CampaignHandle campaign, ItemInstanceId itemInstanceId, CorrelationId correlationId) => throw Reached();
            public Result<ItemStackRecord> CreateItemStack(CampaignHandle campaign, ItemStackRecord record, CommandId commandId, CorrelationId correlationId) => throw Reached();
            public Result<ItemStackRecord> MoveItemStack(CampaignHandle campaign, InventoryMove move, CorrelationId correlationId) => throw Reached();
            public Result<ItemStackRecord> SplitItemStack(CampaignHandle campaign, InventoryStackOperation operation, CorrelationId correlationId) => throw Reached();
            public Result<ItemStackRecord> MergeItemStacks(CampaignHandle campaign, InventoryStackOperation operation, CorrelationId correlationId) => throw Reached();
            public Result<InventoryCreateReplay<ItemStackRecord>> TryReplayCreateItemStack(CampaignHandle campaign, CommandId commandId, ItemStackId itemStackId, CorrelationId correlationId) => throw Reached();
            public Result<ItemStackRecord> GetItemStack(CampaignHandle campaign, ItemStackId itemStackId, CorrelationId correlationId) => throw Reached();
            public Result<IReadOnlyList<ItemInstanceRecord>> ListItemInstances(CampaignHandle campaign, CampaignId campaignId, InventoryId inventoryId, CorrelationId correlationId) => throw Reached();
            public Result<IReadOnlyList<ItemStackRecord>> ListItemStacks(CampaignHandle campaign, CampaignId campaignId, InventoryId inventoryId, CorrelationId correlationId) => throw Reached();
            public Result<bool> HasAnyItemOwnedByCharacter(CampaignHandle campaign, CampaignId campaignId, CharacterId characterId, CorrelationId correlationId) => throw Reached();
            public Result<bool> HasAnyRuntimeReferenceToDefinition(CampaignHandle campaign, CampaignId campaignId, ContentDefinitionId definitionId, CorrelationId correlationId) => throw Reached();
            public Result<IReadOnlyList<ItemInstanceRecord>> ListItemInstancesBySourceDefinitionId(CampaignHandle campaign, CampaignId campaignId, ContentDefinitionId definitionId, CorrelationId correlationId) => throw Reached();
            public Result<IReadOnlyList<ItemStackRecord>> ListItemStacksBySourceDefinitionId(CampaignHandle campaign, CampaignId campaignId, ContentDefinitionId definitionId, CorrelationId correlationId) => throw Reached();
            public Result<EquippedEntryRecord> CreateEquippedEntry(CampaignHandle campaign, EquippedEntryRecord record, CommandId commandId, CorrelationId correlationId) => throw Reached();
            public Result<EquippedEntryRecord> GetEquippedEntry(CampaignHandle campaign, InventoryItemRef itemRef, CorrelationId correlationId) => throw Reached();
            public Result<EquippedEntryRecord> ReplaceEquippedEntry(CampaignHandle campaign, EquippedEntryRecord record, long expectedRevision, CommandId commandId, CorrelationId correlationId) => throw Reached();
            public Result<bool> DeleteEquippedEntry(CampaignHandle campaign, InventoryItemRef itemRef, long expectedRevision, CommandId commandId, CorrelationId correlationId) => throw Reached();
            public Result<IReadOnlyList<EquippedEntryRecord>> ListEquippedEntries(CampaignHandle campaign, CampaignId campaignId, InventoryId inventoryId, CorrelationId correlationId) => throw Reached();
            public Result<EquippedEntryRecord> EquipItem(CampaignHandle campaign, EquipTransition transition, CorrelationId correlationId) => throw Reached();
            public Result<bool> UnequipItem(CampaignHandle campaign, UnequipTransition transition, CorrelationId correlationId) => throw Reached();
            public Result<bool> HasAnyEquippedEntryReferencingBodyPart(CampaignHandle campaign, CampaignId campaignId, CharacterId characterId, BodyPartId bodyPartId, CorrelationId correlationId) => throw Reached();
        }
    }
}
