using System;
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
    public sealed class InventoryMovementServiceTests
    {
        private static readonly CorrelationId CorrelationId = Odyssey.Domain.Identity.CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaigns = null!;
        private SqliteInventoryRepository _repository = null!;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s05-204-" + Guid.NewGuid().ToString("N"));
            _campaigns = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> created = _campaigns.Create(new CreateCampaignRequest(_campaignDir, "Inventory Movement Test", "ruleset.core", "1.0.0", "0.1.0"), NewCommandId(), CorrelationId);
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

        [Test]
        public void MoveItemInstance_ContainedToContained_UpdatesRecordAndInventoryOnce()
        {
            InventoryRecord inventory = CreateInventory();
            ItemInstanceRecord item = CreateInstance(inventory, "main");

            Result<ItemInstanceRecord> moved = InventoryMovementService.MoveItemInstance(_repository, InstanceRequest(item, inventory, inventory, "pack"));

            Assert.That(moved.IsSuccess, Is.True);
            Assert.That(moved.Value.InventoryId, Is.EqualTo(inventory.InventoryId));
            Assert.That(moved.Value.LocationRef, Is.EqualTo(InventoryLocationRef.Contained(inventory.InventoryId, "pack")));
            Assert.That(moved.Value.Revision, Is.EqualTo(2));
            Assert.That(_repository.GetInventory(_campaign, inventory.InventoryId, CorrelationId).Value.Revision, Is.EqualTo(2));
        }

        [Test]
        public void MoveItemStack_CrossInventory_UsesPersistedDestinationOwner()
        {
            InventoryRecord source = CreateInventory();
            InventoryRecord destination = CreateInventory(sceneOwner: true);
            ItemStackRecord stack = CreateStack(source, "main");

            Result<ItemStackRecord> moved = InventoryMovementService.MoveItemStack(_repository, StackRequest(stack, source, destination, "crate"));

            Assert.That(moved.IsSuccess, Is.True);
            Assert.That(moved.Value.InventoryId, Is.EqualTo(destination.InventoryId));
            Assert.That(moved.Value.OwnerRef, Is.EqualTo(destination.OwnerRef));
            Assert.That(moved.Value.Quantity, Is.EqualTo(stack.Quantity));
            Assert.That(_repository.GetInventory(_campaign, source.InventoryId, CorrelationId).Value.Revision, Is.EqualTo(2));
            Assert.That(_repository.GetInventory(_campaign, destination.InventoryId, CorrelationId).Value.Revision, Is.EqualTo(2));
        }

        [Test]
        public void Move_DeniedForNonMainGm_BeforeRepositoryMutation()
        {
            InventoryRecord inventory = CreateInventory();
            ItemInstanceRecord item = CreateInstance(inventory, "main");
            MoveItemInstanceRequest request = InstanceRequest(item, inventory, inventory, "pack", mainGm: false);

            Result<ItemInstanceRecord> result = InventoryMovementService.MoveItemInstance(_repository, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.InventoryMoveDenied));
            Assert.That(_repository.GetItemInstance(_campaign, item.ItemInstanceId, CorrelationId).Value.Revision, Is.EqualTo(1));
            Assert.That(CountRows("InventoryMoveCommandLedger"), Is.EqualTo(0));
        }

        [Test]
        public void Move_RejectsExactDestinationAndStaleTargetRevisionWithoutMutation()
        {
            InventoryRecord inventory = CreateInventory();
            ItemInstanceRecord item = CreateInstance(inventory, "main");
            Result<ItemInstanceRecord> noOp = InventoryMovementService.MoveItemInstance(_repository, InstanceRequest(item, inventory, inventory, "main"));
            Result<ItemInstanceRecord> staleItem = InventoryMovementService.MoveItemInstance(_repository, InstanceRequest(item, inventory, inventory, "pack", targetRevision: 2));
            Result<ItemInstanceRecord> staleInventory = InventoryMovementService.MoveItemInstance(_repository, InstanceRequest(item, inventory, inventory, "pack", sourceRevision: 2, destinationRevision: 2));

            Assert.That(noOp.Error.Code, Is.EqualTo(ErrorCodes.InventoryMoveDestinationUnchanged));
            Assert.That(staleItem.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryItemRevisionConflict));
            Assert.That(staleInventory.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryRevisionConflict));
            Assert.That(CountRows("InventoryMoveCommandLedger"), Is.EqualTo(0));
        }

        [Test]
        public void Move_RejectsNonContainedSourceWithoutMutation()
        {
            InventoryRecord inventory = CreateInventory();
            ItemInstanceRecord item = NewInstance(inventory, "main", InventoryLocationRef.Equipped(inventory.InventoryId, "belt"));
            Assert.That(_repository.CreateItemInstance(_campaign, item, NewCommandId(), CorrelationId).IsSuccess, Is.True);

            Result<ItemInstanceRecord> result = InventoryMovementService.MoveItemInstance(_repository, InstanceRequest(item, inventory, inventory, "pack"));

            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.InventoryMoveSourceInvalid));
            AssertUnchanged(item, inventory);
        }

        [Test]
        public void Move_RejectsMissingDestinationWithoutMutation()
        {
            InventoryRecord source = CreateInventory();
            ItemInstanceRecord item = CreateInstance(source, "main");
            InventoryRecord missingDestination = NewInventoryRecord();

            Result<ItemInstanceRecord> result = InventoryMovementService.MoveItemInstance(_repository, InstanceRequest(item, source, missingDestination, "pack"));

            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryNotFound));
            AssertUnchanged(item, source);
        }

        [Test]
        public void Move_RejectsStaleSourceAndDestinationInventoryRevisionsWithoutMutation()
        {
            InventoryRecord source = CreateInventory();
            InventoryRecord destination = CreateInventory(sceneOwner: true);
            ItemInstanceRecord item = CreateInstance(source, "main");

            Result<ItemInstanceRecord> staleSource = InventoryMovementService.MoveItemInstance(_repository, InstanceRequest(item, source, destination, "crate", sourceRevision: 2));
            Result<ItemInstanceRecord> staleDestination = InventoryMovementService.MoveItemInstance(_repository, InstanceRequest(item, source, destination, "crate", destinationRevision: 2));

            Assert.That(staleSource.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryRevisionConflict));
            Assert.That(staleDestination.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryRevisionConflict));
            AssertUnchanged(item, source, destination);
        }

        [Test]
        public void Move_RollsBackAfterTargetUpdateFailure()
        {
            InventoryRecord source = CreateInventory();
            InventoryRecord destination = CreateInventory(sceneOwner: true);
            ItemInstanceRecord item = CreateInstance(source, "main");
            using (SqliteConnection connection = Open())
            using (var trigger = connection.CreateCommand())
            {
                trigger.CommandText = "CREATE TRIGGER FailInventoryMove BEFORE UPDATE ON ItemInstance BEGIN SELECT RAISE(ABORT, 'test failure'); END;";
                trigger.ExecuteNonQuery();
            }

            Result<ItemInstanceRecord> result = InventoryMovementService.MoveItemInstance(_repository, InstanceRequest(item, source, destination, "crate"));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryIoFailed));
            AssertUnchanged(item, source, destination);
        }

        [Test]
        public void Move_ReplayAndCreationLedgerCollision_AreIdempotentOrRejected()
        {
            InventoryRecord inventory = CreateInventory();
            ItemInstanceRecord item = CreateInstance(inventory, "main");
            CommandId moveId = NewCommandId();
            MoveItemInstanceRequest request = InstanceRequest(item, inventory, inventory, "pack", moveId);
            Result<ItemInstanceRecord> first = InventoryMovementService.MoveItemInstance(_repository, request);
            Result<ItemInstanceRecord> replay = InventoryMovementService.MoveItemInstance(_repository, request);
            ItemInstanceRecord another = NewInstance(inventory, "main");
            CommandId createId = NewCommandId();
            Result<ItemInstanceRecord> create = _repository.CreateItemInstance(_campaign, another, createId, CorrelationId);
            Result<ItemInstanceRecord> collision = InventoryMovementService.MoveItemInstance(_repository, InstanceRequest(another, inventory, inventory, "pack", createId, sourceRevision: 2, destinationRevision: 2));

            Assert.That(first.IsSuccess && replay.IsSuccess, Is.True);
            Assert.That(replay.Value.Revision, Is.EqualTo(2));
            Assert.That(CountRows("InventoryMoveCommandLedger"), Is.EqualTo(1));
            Assert.That(_repository.GetInventory(_campaign, inventory.InventoryId, CorrelationId).Value.Revision, Is.EqualTo(2));
            Assert.That(create.IsSuccess, Is.True);
            Assert.That(collision.Error.Code, Is.EqualTo(ErrorCodes.CommandIdentityMismatch));
        }

        [Test]
        public void MoveLedger_HasPrimaryKey_AndMismatchedReplayDoesNotMutate()
        {
            InventoryRecord inventory = CreateInventory();
            ItemInstanceRecord item = CreateInstance(inventory, "main");
            CommandId commandId = NewCommandId();
            Assert.That(InventoryMovementService.MoveItemInstance(_repository, InstanceRequest(item, inventory, inventory, "pack", commandId)).IsSuccess, Is.True);
            Result<ItemInstanceRecord> mismatch = InventoryMovementService.MoveItemInstance(_repository, InstanceRequest(item, inventory, inventory, "belt", commandId, targetRevision: 2, sourceRevision: 2, destinationRevision: 2));

            Assert.That(mismatch.IsFailure, Is.True);
            Assert.That(mismatch.Error.Code, Is.EqualTo(ErrorCodes.CommandIdentityMismatch));
            Assert.That(GetPrimaryKeyColumn("InventoryMoveCommandLedger"), Is.EqualTo("CommandId"));
            Assert.That(_repository.GetItemInstance(_campaign, item.ItemInstanceId, CorrelationId).Value.LocationRef.DetailRef, Is.EqualTo("pack"));
        }

        private InventoryRecord CreateInventory(bool sceneOwner = false)
        {
            UtcInstant now = Clock.GetUtcNow();
            InventoryOwnerRef owner = sceneOwner ? InventoryOwnerRef.ForScene(SceneId.NewId(now), "crate") : InventoryOwnerRef.ForCharacter(CharacterId.NewId(now));
            var record = new InventoryRecord(InventoryId.NewId(now), _campaign.CampaignId, owner, 1, now, now);
            Result<InventoryRecord> result = _repository.CreateInventory(_campaign, record, NewCommandId(), CorrelationId);
            Assert.That(result.IsSuccess, Is.True);
            return result.Value;
        }

        private ItemInstanceRecord CreateInstance(InventoryRecord inventory, string key)
        {
            ItemInstanceRecord record = NewInstance(inventory, key);
            Assert.That(_repository.CreateItemInstance(_campaign, record, NewCommandId(), CorrelationId).IsSuccess, Is.True);
            return record;
        }

        private ItemStackRecord CreateStack(InventoryRecord inventory, string key)
        {
            UtcInstant now = Clock.GetUtcNow();
            ContentDefinitionRef source = new ContentDefinitionRef(ContentDefinitionId.NewId(now), 1);
            var record = new ItemStackRecord(ItemStackId.NewId(now), _campaign.CampaignId, inventory.InventoryId, inventory.OwnerRef, InventoryLocationRef.Contained(inventory.InventoryId, key), source, new ItemMechanicsSnapshot(source, 1, ContentDefinitionType.Ammo, "{}"), ItemStackQuantity.Create(4), "{}", 1, now, now);
            Assert.That(_repository.CreateItemStack(_campaign, record, NewCommandId(), CorrelationId).IsSuccess, Is.True);
            return record;
        }

        private ItemInstanceRecord NewInstance(InventoryRecord inventory, string key, InventoryLocationRef? location = null)
        {
            UtcInstant now = Clock.GetUtcNow();
            ContentDefinitionRef source = new ContentDefinitionRef(ContentDefinitionId.NewId(now), 1);
            return new ItemInstanceRecord(ItemInstanceId.NewId(now), _campaign.CampaignId, inventory.InventoryId, inventory.OwnerRef, location ?? InventoryLocationRef.Contained(inventory.InventoryId, key), source, new ItemMechanicsSnapshot(source, 1, ContentDefinitionType.Item, "{}"), "{}", 1, now, now);
        }

        private InventoryRecord NewInventoryRecord()
        {
            UtcInstant now = Clock.GetUtcNow();
            return new InventoryRecord(InventoryId.NewId(now), _campaign.CampaignId, InventoryOwnerRef.ForCharacter(CharacterId.NewId(now)), 1, now, now);
        }

        private void AssertUnchanged(ItemInstanceRecord item, InventoryRecord source, InventoryRecord? destination = null)
        {
            ItemInstanceRecord storedItem = _repository.GetItemInstance(_campaign, item.ItemInstanceId, CorrelationId).Value;
            Assert.That(storedItem.InventoryId, Is.EqualTo(item.InventoryId));
            Assert.That(storedItem.LocationRef, Is.EqualTo(item.LocationRef));
            Assert.That(storedItem.Revision, Is.EqualTo(item.Revision));
            Assert.That(_repository.GetInventory(_campaign, source.InventoryId, CorrelationId).Value.Revision, Is.EqualTo(source.Revision));
            if (destination != null) Assert.That(_repository.GetInventory(_campaign, destination.InventoryId, CorrelationId).Value.Revision, Is.EqualTo(destination.Revision));
            Assert.That(CountRows("InventoryMoveCommandLedger"), Is.EqualTo(0));
        }

        private MoveItemInstanceRequest InstanceRequest(ItemInstanceRecord item, InventoryRecord source, InventoryRecord destination, string key, CommandId? commandId = null, long targetRevision = 1, long sourceRevision = 1, long destinationRevision = 1, bool mainGm = true)
            => new MoveItemInstanceRequest(_campaign, item.ItemInstanceId, targetRevision, source.InventoryId, sourceRevision, destination.InventoryId, destinationRevision, key, UserId.Parse("user_0123456789abcdef0123456789abcdef"), mainGm, commandId ?? NewCommandId(), CorrelationId);

        private MoveItemStackRequest StackRequest(ItemStackRecord stack, InventoryRecord source, InventoryRecord destination, string key)
            => new MoveItemStackRequest(_campaign, stack.ItemStackId, 1, source.InventoryId, 1, destination.InventoryId, 1, key, UserId.Parse("user_0123456789abcdef0123456789abcdef"), true, NewCommandId(), CorrelationId);

        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private long CountRows(string table) { using SqliteConnection c = Open(); using var q = c.CreateCommand(); q.CommandText = "SELECT COUNT(*) FROM " + table + ";"; return (long)q.ExecuteScalar()!; }
        private string GetPrimaryKeyColumn(string table) { using SqliteConnection c = Open(); using var q = c.CreateCommand(); q.CommandText = "PRAGMA table_info(" + table + ");"; using SqliteDataReader r = q.ExecuteReader(); while (r.Read()) if (r.GetInt64(5) == 1) return r.GetString(1); return string.Empty; }
        private SqliteConnection Open() { var c = new SqliteConnection("Data Source=" + Path.Combine(_campaignDir, "campaign.db")); c.Open(); return c; }
    }
}
