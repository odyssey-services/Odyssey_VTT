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
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S05-303/304: real SQLite tests for the Equip transition
    /// (<see cref="IInventoryRepository.EquipItem"/>), the MainGM/rule-4 gate
    /// (<see cref="EquipmentService.Equip"/>), the symmetric Unequip transition
    /// (<see cref="IInventoryRepository.UnequipItem"/>), and its MainGM gate
    /// (<see cref="EquipmentService.Unequip"/>). No weapon/armor mechanical
    /// effect or `RemoveBodyPart` dependency behavior is exercised here.
    /// </summary>
    public sealed class EquipmentServiceTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static readonly AnatomyProfileDefinitionId Humanoid = AnatomyProfileDefinitionId.Parse("Humanoid");
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;
        private SqliteInventoryRepository _inventoryRepository = null!;
        private SqliteCharacterRepository _characterRepository = null!;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s05-303-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> created = _campaignRepository.Create(new CreateCampaignRequest(_campaignDir, "Equip Command Test Campaign", "ruleset.core", "1.0.0", "0.1.0"), NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _inventoryRepository = new SqliteInventoryRepository(Clock);
            _characterRepository = new SqliteCharacterRepository(Clock);
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaignRepository.Close(_campaign, TestCorrelationId); }
            catch (IOException) { }

            try { if (Directory.Exists(_campaignDir)) Directory.Delete(_campaignDir, recursive: true); }
            catch (IOException) { }
        }

        // ---------- IInventoryRepository.EquipItem (direct) ----------

        [Test] // TC-INVENTORY-118
        public void EquipItem_ForItemInstance_Succeeds_LocationRefAndEquippedEntryConsistent()
        {
            CharacterRecord character = CreateInitializedCharacter();
            InventoryRecord inventory = CreateInventory(character.CharacterId);
            ItemInstanceRecord instance = CreateItemInstance(inventory);

            EquippedEntryRecord equipRecord = BuildEquippedEntryRecord(inventory, InventoryItemRef.ForInstance(instance.ItemInstanceId), new[] { BodyPartId.Parse("Torso") });
            Result<EquippedEntryRecord> result = _inventoryRepository.EquipItem(_campaign, new EquipTransition(equipRecord, instance.Revision, NewCommandId()), TestCorrelationId);

            Assert.That(result.IsSuccess, Is.True);
            Result<ItemInstanceRecord> updatedInstance = _inventoryRepository.GetItemInstance(_campaign, instance.ItemInstanceId, TestCorrelationId);
            Assert.That(updatedInstance.Value.LocationRef, Is.EqualTo(InventoryLocationRef.Equipped(inventory.InventoryId, "chest_slot")));
            Assert.That(updatedInstance.Value.Revision, Is.EqualTo(instance.Revision + 1));
            Assert.That(result.Value.Entry.ToLocationRef(), Is.EqualTo(updatedInstance.Value.LocationRef), "item LocationRef and EquippedEntry.ToLocationRef() must agree after a successful Equip");
        }

        [Test] // TC-INVENTORY-119
        public void EquipItem_ForItemStack_Succeeds_LocationRefAndEquippedEntryConsistent()
        {
            InventoryRecord inventory = CreateInventory(CharacterId.NewId(Clock.GetUtcNow()));
            ItemStackRecord stack = CreateItemStack(inventory);

            EquippedEntryRecord equipRecord = BuildEquippedEntryRecord(inventory, InventoryItemRef.ForStack(stack.ItemStackId), Array.Empty<BodyPartId>());
            Result<EquippedEntryRecord> result = _inventoryRepository.EquipItem(_campaign, new EquipTransition(equipRecord, stack.Revision, NewCommandId()), TestCorrelationId);

            Assert.That(result.IsSuccess, Is.True);
            Result<ItemStackRecord> updatedStack = _inventoryRepository.GetItemStack(_campaign, stack.ItemStackId, TestCorrelationId);
            Assert.That(updatedStack.Value.LocationRef, Is.EqualTo(InventoryLocationRef.Equipped(inventory.InventoryId, "chest_slot")));
            Assert.That(result.Value.Entry.ToLocationRef(), Is.EqualTo(updatedStack.Value.LocationRef));
        }

        [Test] // TC-INVENTORY-120
        public void EquipItem_RejectsRuleOneConflict_WhenAnEquippedEntryRowAlreadyExistsForAStillContainedItem()
        {
            // Constructs the rule-1 conflict in isolation from the (also-true) source-Contained
            // guard: the item's own LocationRef is left Contained (as if an earlier, unrelated
            // write created its EquippedEntry row without also updating the item -- a state this
            // repository's own EquipItem never produces, but the functional rule-1 existence check
            // must still catch it independently of the physical ItemRefId primary key).
            InventoryRecord inventory = CreateInventory(CharacterId.NewId(Clock.GetUtcNow()));
            ItemInstanceRecord instance = CreateItemInstance(inventory);
            InsertEquippedEntryRowDirectly(BuildEquippedEntryRecord(inventory, InventoryItemRef.ForInstance(instance.ItemInstanceId), Array.Empty<BodyPartId>()));

            Result<EquippedEntryRecord> result = _inventoryRepository.EquipItem(_campaign, new EquipTransition(BuildEquippedEntryRecord(inventory, InventoryItemRef.ForInstance(instance.ItemInstanceId), Array.Empty<BodyPartId>()), instance.Revision, NewCommandId()), TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceEquipmentEntryAlreadyEquipped));
            Result<ItemInstanceRecord> unchanged = _inventoryRepository.GetItemInstance(_campaign, instance.ItemInstanceId, TestCorrelationId);
            Assert.That(unchanged.Value.LocationRef.Kind, Is.EqualTo(InventoryLocationKind.Contained), "the rejected Equip must not touch the item's LocationRef");
        }

        [Test] // TC-INVENTORY-121
        public void EquipItem_RejectsSourceNotContained()
        {
            InventoryRecord inventory = CreateInventory(CharacterId.NewId(Clock.GetUtcNow()));
            ItemInstanceRecord instance = CreateItemInstance(inventory);
            InventoryItemRef itemRef = InventoryItemRef.ForInstance(instance.ItemInstanceId);
            Result<EquippedEntryRecord> equipped = _inventoryRepository.EquipItem(_campaign, new EquipTransition(BuildEquippedEntryRecord(inventory, itemRef, Array.Empty<BodyPartId>()), instance.Revision, NewCommandId()), TestCorrelationId);
            Assert.That(equipped.IsSuccess, Is.True);

            Result<EquippedEntryRecord> secondEquip = _inventoryRepository.EquipItem(_campaign, new EquipTransition(BuildEquippedEntryRecord(inventory, itemRef, Array.Empty<BodyPartId>()), equipped.Value.Entry.Revision, NewCommandId()), TestCorrelationId);

            Assert.That(secondEquip.IsFailure, Is.True);
            Assert.That(secondEquip.Error.Code, Is.EqualTo(ErrorCodes.InventoryMoveSourceInvalid), "an already-Equipped source is not Contained and must be rejected");
        }

        [Test] // TC-INVENTORY-122
        public void EquipItem_RejectsSourceContainedInDifferentInventory()
        {
            InventoryRecord actualInventory = CreateInventory(CharacterId.NewId(Clock.GetUtcNow()));
            InventoryRecord otherInventory = CreateInventory(CharacterId.NewId(Clock.GetUtcNow()));
            ItemInstanceRecord instance = CreateItemInstance(actualInventory);

            var wrongInventoryEntry = new EquippedEntry(otherInventory.InventoryId, InventoryItemRef.ForInstance(instance.ItemInstanceId), "chest_slot", Array.Empty<BodyPartId>(), NewUserId(), Clock.GetUtcNow(), 1);
            var record = new EquippedEntryRecord(otherInventory.CampaignId, wrongInventoryEntry);

            Result<EquippedEntryRecord> result = _inventoryRepository.EquipItem(_campaign, new EquipTransition(record, instance.Revision, NewCommandId()), TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.InventoryMoveSourceInvalid));
        }

        [Test] // TC-INVENTORY-123
        public void EquipItem_RejectsStaleExpectedRevision()
        {
            InventoryRecord inventory = CreateInventory(CharacterId.NewId(Clock.GetUtcNow()));
            ItemInstanceRecord instance = CreateItemInstance(inventory);

            Result<EquippedEntryRecord> result = _inventoryRepository.EquipItem(_campaign, new EquipTransition(BuildEquippedEntryRecord(inventory, InventoryItemRef.ForInstance(instance.ItemInstanceId), Array.Empty<BodyPartId>()), instance.Revision + 5, NewCommandId()), TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryItemRevisionConflict));
            Result<ItemInstanceRecord> unchanged = _inventoryRepository.GetItemInstance(_campaign, instance.ItemInstanceId, TestCorrelationId);
            Assert.That(unchanged.Value.LocationRef.Kind, Is.EqualTo(InventoryLocationKind.Contained));
        }

        [Test] // TC-INVENTORY-124
        public void EquipItem_ReplayWithSameCommandId_ReturnsSameOutcomeWithoutSecondMutation()
        {
            InventoryRecord inventory = CreateInventory(CharacterId.NewId(Clock.GetUtcNow()));
            ItemInstanceRecord instance = CreateItemInstance(inventory);
            EquippedEntryRecord equipRecord = BuildEquippedEntryRecord(inventory, InventoryItemRef.ForInstance(instance.ItemInstanceId), Array.Empty<BodyPartId>());
            CommandId commandId = NewCommandId();

            Result<EquippedEntryRecord> first = _inventoryRepository.EquipItem(_campaign, new EquipTransition(equipRecord, instance.Revision, commandId), TestCorrelationId);
            Result<EquippedEntryRecord> replay = _inventoryRepository.EquipItem(_campaign, new EquipTransition(equipRecord, instance.Revision, commandId), TestCorrelationId);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.Entry.Revision, Is.EqualTo(first.Value.Entry.Revision));
            Result<ItemInstanceRecord> item = _inventoryRepository.GetItemInstance(_campaign, instance.ItemInstanceId, TestCorrelationId);
            Assert.That(item.Value.Revision, Is.EqualTo(instance.Revision + 1), "replay must not advance the item's revision a second time");
        }

        [Test] // TC-INVENTORY-125
        public void EquipItem_WithSameCommandIdForDifferentTarget_IsRejected()
        {
            InventoryRecord inventory = CreateInventory(CharacterId.NewId(Clock.GetUtcNow()));
            ItemInstanceRecord first = CreateItemInstance(inventory);
            ItemInstanceRecord second = CreateItemInstance(inventory);
            CommandId commandId = NewCommandId();

            Result<EquippedEntryRecord> firstResult = _inventoryRepository.EquipItem(_campaign, new EquipTransition(BuildEquippedEntryRecord(inventory, InventoryItemRef.ForInstance(first.ItemInstanceId), Array.Empty<BodyPartId>()), first.Revision, commandId), TestCorrelationId);
            Result<EquippedEntryRecord> secondResult = _inventoryRepository.EquipItem(_campaign, new EquipTransition(BuildEquippedEntryRecord(inventory, InventoryItemRef.ForInstance(second.ItemInstanceId), Array.Empty<BodyPartId>()), second.Revision, commandId), TestCorrelationId);

            Assert.That(firstResult.IsSuccess, Is.True);
            Assert.That(secondResult.IsFailure, Is.True);
            Assert.That(secondResult.Error.Code, Is.EqualTo(ErrorCodes.CommandIdentityMismatch));
            Result<ItemInstanceRecord> secondItem = _inventoryRepository.GetItemInstance(_campaign, second.ItemInstanceId, TestCorrelationId);
            Assert.That(secondItem.Value.LocationRef.Kind, Is.EqualTo(InventoryLocationKind.Contained));
        }

        // ---------- EquipmentService.Equip (MainGM gate + rule 4) ----------

        [Test] // TC-INVENTORY-126
        public void EquipmentService_Equip_DeniesNonMainGmActor_BeforeAnyRepositoryCall()
        {
            InventoryId nonExistentInventoryId = InventoryId.NewId(Clock.GetUtcNow());
            EquipRequest request = new EquipRequest(_campaign, InventoryItemRef.ForInstance(ItemInstanceId.NewId(Clock.GetUtcNow())), nonExistentInventoryId, 1, "chest_slot", Array.Empty<BodyPartId>(), NewUserId(), Clock.GetUtcNow(), NewUserId(), actorIsMainGm: false, NewCommandId(), TestCorrelationId);

            Result<EquippedEntryRecord> result = EquipmentService.Equip(_inventoryRepository, _characterRepository, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.InventoryMoveDenied), "MainGM denial must precede the GetInventory call that would otherwise return InventoryNotFound for this nonexistent InventoryId");
        }

        [Test] // TC-INVENTORY-127
        public void EquipmentService_Equip_RejectsBodyPartNotFoundOnAnatomy()
        {
            CharacterRecord character = CreateInitializedCharacter();
            InventoryRecord inventory = CreateInventory(character.CharacterId);
            ItemInstanceRecord instance = CreateItemInstance(inventory);

            EquipRequest request = new EquipRequest(_campaign, InventoryItemRef.ForInstance(instance.ItemInstanceId), inventory.InventoryId, instance.Revision, "chest_slot", new[] { BodyPartId.Parse("Tail") }, NewUserId(), Clock.GetUtcNow(), NewUserId(), actorIsMainGm: true, NewCommandId(), TestCorrelationId);

            Result<EquippedEntryRecord> result = EquipmentService.Equip(_inventoryRepository, _characterRepository, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.InventoryEquipBodyPartNotFound));
            Result<ItemInstanceRecord> unchanged = _inventoryRepository.GetItemInstance(_campaign, instance.ItemInstanceId, TestCorrelationId);
            Assert.That(unchanged.Value.LocationRef.Kind, Is.EqualTo(InventoryLocationKind.Contained));
        }

        [Test] // TC-INVENTORY-128
        public void EquipmentService_Equip_RejectsWhenAnatomyNotInitialized()
        {
            var request = new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Uninitialized Anatomy Character");
            Result<CharacterRecord> character = _characterRepository.CreateCharacter(request, NewCommandId(), TestCorrelationId);
            Assert.That(character.IsSuccess, Is.True);
            InventoryRecord inventory = CreateInventory(character.Value.CharacterId);
            ItemInstanceRecord instance = CreateItemInstance(inventory);

            EquipRequest equipRequest = new EquipRequest(_campaign, InventoryItemRef.ForInstance(instance.ItemInstanceId), inventory.InventoryId, instance.Revision, "chest_slot", new[] { BodyPartId.Parse("Torso") }, NewUserId(), Clock.GetUtcNow(), NewUserId(), actorIsMainGm: true, NewCommandId(), TestCorrelationId);

            Result<EquippedEntryRecord> result = EquipmentService.Equip(_inventoryRepository, _characterRepository, equipRequest);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterAnatomyNotInitialized));
        }

        [Test] // TC-INVENTORY-129
        public void EquipmentService_Equip_RejectsNonCharacterOwnerWithBodyPartRefs()
        {
            InventoryRecord inventory = CreateSceneOwnedInventory();
            ItemInstanceRecord instance = CreateItemInstance(inventory);

            EquipRequest request = new EquipRequest(_campaign, InventoryItemRef.ForInstance(instance.ItemInstanceId), inventory.InventoryId, instance.Revision, "chest_slot", new[] { BodyPartId.Parse("Torso") }, NewUserId(), Clock.GetUtcNow(), NewUserId(), actorIsMainGm: true, NewCommandId(), TestCorrelationId);

            Result<EquippedEntryRecord> result = EquipmentService.Equip(_inventoryRepository, _characterRepository, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.InventoryEquipBodyPartRefsRequireCharacterOwner));
        }

        [Test] // TC-INVENTORY-130
        public void EquipmentService_Equip_SucceedsWithEmptyBodyPartRefs_RegardlessOfOwnerKind()
        {
            InventoryRecord inventory = CreateSceneOwnedInventory();
            ItemInstanceRecord instance = CreateItemInstance(inventory);

            EquipRequest request = new EquipRequest(_campaign, InventoryItemRef.ForInstance(instance.ItemInstanceId), inventory.InventoryId, instance.Revision, "held_slot", Array.Empty<BodyPartId>(), NewUserId(), Clock.GetUtcNow(), NewUserId(), actorIsMainGm: true, NewCommandId(), TestCorrelationId);

            Result<EquippedEntryRecord> result = EquipmentService.Equip(_inventoryRepository, _characterRepository, request);

            Assert.That(result.IsSuccess, Is.True, "empty BodyPartRefs must skip rule 4 entirely, even for a non-Character owner");
        }

        // ---------- IInventoryRepository.UnequipItem / EquipmentService.Unequip ----------

        [Test] // TC-INVENTORY-131
        public void UnequipItem_ForItemInstance_Succeeds_LocationRefBecomesContainedAndEquippedEntryRemoved()
        {
            InventoryRecord inventory = CreateInventory(CharacterId.NewId(Clock.GetUtcNow()));
            ItemInstanceRecord instance = CreateItemInstance(inventory);
            EquippedEntryRecord equipped = EquipItemDirectly(inventory, InventoryItemRef.ForInstance(instance.ItemInstanceId), instance.Revision);

            var transition = new UnequipTransition(equipped.Entry.ItemRef, inventory.InventoryId, instance.Revision + 1, equipped.Entry.Revision, "backpack", NewCommandId());
            Result<bool> result = _inventoryRepository.UnequipItem(_campaign, transition, TestCorrelationId);

            Assert.That(result.IsSuccess, Is.True);
            Result<ItemInstanceRecord> updated = _inventoryRepository.GetItemInstance(_campaign, instance.ItemInstanceId, TestCorrelationId);
            Assert.That(updated.Value.LocationRef, Is.EqualTo(InventoryLocationRef.Contained(inventory.InventoryId, "backpack")));
            Result<EquippedEntryRecord> stillEquipped = _inventoryRepository.GetEquippedEntry(_campaign, equipped.Entry.ItemRef, TestCorrelationId);
            Assert.That(stillEquipped.IsFailure, Is.True);
            Assert.That(stillEquipped.Error.Code, Is.EqualTo(ErrorCodes.PersistenceEquipmentEntryNotFound));
        }

        [Test] // TC-INVENTORY-132
        public void UnequipItem_ForItemStack_Succeeds_WithTheSameGuarantee()
        {
            InventoryRecord inventory = CreateInventory(CharacterId.NewId(Clock.GetUtcNow()));
            ItemStackRecord stack = CreateItemStack(inventory);
            EquippedEntryRecord equipped = EquipItemDirectly(inventory, InventoryItemRef.ForStack(stack.ItemStackId), stack.Revision);

            var transition = new UnequipTransition(equipped.Entry.ItemRef, inventory.InventoryId, stack.Revision + 1, equipped.Entry.Revision, "backpack", NewCommandId());
            Result<bool> result = _inventoryRepository.UnequipItem(_campaign, transition, TestCorrelationId);

            Assert.That(result.IsSuccess, Is.True);
            Result<ItemStackRecord> updated = _inventoryRepository.GetItemStack(_campaign, stack.ItemStackId, TestCorrelationId);
            Assert.That(updated.Value.LocationRef, Is.EqualTo(InventoryLocationRef.Contained(inventory.InventoryId, "backpack")));
        }

        [Test] // TC-INVENTORY-133
        public void UnequipItem_RejectsItemWithNoEquippedEntryRow()
        {
            InventoryRecord inventory = CreateInventory(CharacterId.NewId(Clock.GetUtcNow()));
            ItemInstanceRecord instance = CreateItemInstance(inventory);

            var transition = new UnequipTransition(InventoryItemRef.ForInstance(instance.ItemInstanceId), inventory.InventoryId, instance.Revision, 1, "backpack", NewCommandId());
            Result<bool> result = _inventoryRepository.UnequipItem(_campaign, transition, TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceEquipmentEntryNotFound));
        }

        [Test] // TC-INVENTORY-134
        public void UnequipItem_RejectsStaleEquippedEntryRevision()
        {
            InventoryRecord inventory = CreateInventory(CharacterId.NewId(Clock.GetUtcNow()));
            ItemInstanceRecord instance = CreateItemInstance(inventory);
            EquippedEntryRecord equipped = EquipItemDirectly(inventory, InventoryItemRef.ForInstance(instance.ItemInstanceId), instance.Revision);

            var transition = new UnequipTransition(equipped.Entry.ItemRef, inventory.InventoryId, instance.Revision + 1, equipped.Entry.Revision + 9, "backpack", NewCommandId());
            Result<bool> result = _inventoryRepository.UnequipItem(_campaign, transition, TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceEquipmentEntryRevisionConflict));
            Result<ItemInstanceRecord> unchanged = _inventoryRepository.GetItemInstance(_campaign, instance.ItemInstanceId, TestCorrelationId);
            Assert.That(unchanged.Value.LocationRef.Kind, Is.EqualTo(InventoryLocationKind.Equipped));
        }

        [Test] // TC-INVENTORY-135
        public void UnequipItem_RejectsStaleItemRevision()
        {
            InventoryRecord inventory = CreateInventory(CharacterId.NewId(Clock.GetUtcNow()));
            ItemInstanceRecord instance = CreateItemInstance(inventory);
            EquippedEntryRecord equipped = EquipItemDirectly(inventory, InventoryItemRef.ForInstance(instance.ItemInstanceId), instance.Revision);

            var transition = new UnequipTransition(equipped.Entry.ItemRef, inventory.InventoryId, instance.Revision + 41, equipped.Entry.Revision, "backpack", NewCommandId());
            Result<bool> result = _inventoryRepository.UnequipItem(_campaign, transition, TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryItemRevisionConflict));
            Result<EquippedEntryRecord> stillEquipped = _inventoryRepository.GetEquippedEntry(_campaign, equipped.Entry.ItemRef, TestCorrelationId);
            Assert.That(stillEquipped.IsSuccess, Is.True, "a rejected item-revision CAS must not remove the EquippedEntry row");
        }

        [Test] // TC-INVENTORY-136
        public void UnequipItem_RejectsInventoryIdMismatch()
        {
            InventoryRecord inventory = CreateInventory(CharacterId.NewId(Clock.GetUtcNow()));
            InventoryRecord otherInventory = CreateInventory(CharacterId.NewId(Clock.GetUtcNow()));
            ItemInstanceRecord instance = CreateItemInstance(inventory);
            EquippedEntryRecord equipped = EquipItemDirectly(inventory, InventoryItemRef.ForInstance(instance.ItemInstanceId), instance.Revision);

            var transition = new UnequipTransition(equipped.Entry.ItemRef, otherInventory.InventoryId, instance.Revision + 1, equipped.Entry.Revision, "backpack", NewCommandId());
            Result<bool> result = _inventoryRepository.UnequipItem(_campaign, transition, TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.InventoryMoveSourceInvalid));
        }

        [Test] // TC-INVENTORY-137
        public void UnequipItem_ReplayWithSameCommandId_ReturnsSuccessWithoutSecondMutation()
        {
            InventoryRecord inventory = CreateInventory(CharacterId.NewId(Clock.GetUtcNow()));
            ItemInstanceRecord instance = CreateItemInstance(inventory);
            EquippedEntryRecord equipped = EquipItemDirectly(inventory, InventoryItemRef.ForInstance(instance.ItemInstanceId), instance.Revision);
            CommandId commandId = NewCommandId();
            var transition = new UnequipTransition(equipped.Entry.ItemRef, inventory.InventoryId, instance.Revision + 1, equipped.Entry.Revision, "backpack", commandId);

            Result<bool> first = _inventoryRepository.UnequipItem(_campaign, transition, TestCorrelationId);
            Result<bool> replay = _inventoryRepository.UnequipItem(_campaign, transition, TestCorrelationId);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(replay.IsSuccess, Is.True);
            Result<ItemInstanceRecord> item = _inventoryRepository.GetItemInstance(_campaign, instance.ItemInstanceId, TestCorrelationId);
            Assert.That(item.Value.Revision, Is.EqualTo(instance.Revision + 2), "one Equip and one Unequip each advance the item's revision once; replay must not add a third");
        }

        [Test] // TC-INVENTORY-138
        public void UnequipItem_WithSameCommandIdForDifferentTarget_IsRejected()
        {
            InventoryRecord inventory = CreateInventory(CharacterId.NewId(Clock.GetUtcNow()));
            ItemInstanceRecord first = CreateItemInstance(inventory);
            ItemInstanceRecord second = CreateItemInstance(inventory);
            EquippedEntryRecord firstEquipped = EquipItemDirectly(inventory, InventoryItemRef.ForInstance(first.ItemInstanceId), first.Revision);
            EquippedEntryRecord secondEquipped = EquipItemDirectly(inventory, InventoryItemRef.ForInstance(second.ItemInstanceId), second.Revision);
            CommandId commandId = NewCommandId();

            Result<bool> firstResult = _inventoryRepository.UnequipItem(_campaign, new UnequipTransition(firstEquipped.Entry.ItemRef, inventory.InventoryId, first.Revision + 1, firstEquipped.Entry.Revision, "backpack", commandId), TestCorrelationId);
            Result<bool> secondResult = _inventoryRepository.UnequipItem(_campaign, new UnequipTransition(secondEquipped.Entry.ItemRef, inventory.InventoryId, second.Revision + 1, secondEquipped.Entry.Revision, "backpack", commandId), TestCorrelationId);

            Assert.That(firstResult.IsSuccess, Is.True);
            Assert.That(secondResult.IsFailure, Is.True);
            Assert.That(secondResult.Error.Code, Is.EqualTo(ErrorCodes.CommandIdentityMismatch));
            Result<EquippedEntryRecord> secondStillEquipped = _inventoryRepository.GetEquippedEntry(_campaign, secondEquipped.Entry.ItemRef, TestCorrelationId);
            Assert.That(secondStillEquipped.IsSuccess, Is.True);
        }

        [Test] // TC-INVENTORY-139
        public void EquipmentService_Unequip_DeniesNonMainGmActor_BeforeAnyRepositoryCall()
        {
            InventoryId nonExistentInventoryId = InventoryId.NewId(Clock.GetUtcNow());
            UnequipRequest request = new UnequipRequest(_campaign, InventoryItemRef.ForInstance(ItemInstanceId.NewId(Clock.GetUtcNow())), nonExistentInventoryId, 1, 1, "backpack", NewUserId(), actorIsMainGm: false, NewCommandId(), TestCorrelationId);

            Result<bool> result = EquipmentService.Unequip(_inventoryRepository, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.InventoryMoveDenied), "MainGM denial must precede any repository call that would otherwise return a different error for this nonexistent InventoryId/item");
        }

        [Test] // TC-INVENTORY-140
        public void EquipThenUnequip_RoundTrip_ItemIsContainedAndReEquippable()
        {
            InventoryRecord inventory = CreateInventory(CharacterId.NewId(Clock.GetUtcNow()));
            ItemInstanceRecord instance = CreateItemInstance(inventory);
            EquippedEntryRecord equipped = EquipItemDirectly(inventory, InventoryItemRef.ForInstance(instance.ItemInstanceId), instance.Revision);

            Result<bool> unequipped = _inventoryRepository.UnequipItem(_campaign, new UnequipTransition(equipped.Entry.ItemRef, inventory.InventoryId, instance.Revision + 1, equipped.Entry.Revision, "backpack", NewCommandId()), TestCorrelationId);
            Assert.That(unequipped.IsSuccess, Is.True);
            Result<ItemInstanceRecord> afterUnequip = _inventoryRepository.GetItemInstance(_campaign, instance.ItemInstanceId, TestCorrelationId);

            EquippedEntryRecord reequipped = EquipItemDirectly(inventory, InventoryItemRef.ForInstance(instance.ItemInstanceId), afterUnequip.Value.Revision);

            Assert.That(reequipped.Entry.ToLocationRef(), Is.EqualTo(InventoryLocationRef.Equipped(inventory.InventoryId, "chest_slot")));
        }

        [Test] // TC-INVENTORY-141
        public void UnequipItem_RejectsSourceThatIsStillContained()
        {
            InventoryRecord inventory = CreateInventory(CharacterId.NewId(Clock.GetUtcNow()));
            ItemInstanceRecord instance = CreateItemInstance(inventory);
            InsertEquippedEntryRowDirectly(BuildEquippedEntryRecord(inventory, InventoryItemRef.ForInstance(instance.ItemInstanceId), Array.Empty<BodyPartId>()));

            var transition = new UnequipTransition(InventoryItemRef.ForInstance(instance.ItemInstanceId), inventory.InventoryId, instance.Revision, 1, "backpack", NewCommandId());
            Result<bool> result = _inventoryRepository.UnequipItem(_campaign, transition, TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryIoFailed), "an EquippedEntry row for a still-Contained item is an inconsistency, not a normal business rejection");
        }

        [Test] // TC-INVENTORY-142
        public void EquipmentService_Unequip_TakesNoCharacterRepositoryDependency_AndIntroducesNoRemoveBodyPartOrWeaponArmorType()
        {
            System.Reflection.MethodInfo? unequip = typeof(EquipmentService).GetMethod("Unequip");
            Assert.That(unequip, Is.Not.Null);
            System.Reflection.ParameterInfo[] parameters = unequip!.GetParameters();
            Assert.That(parameters.Select(p => p.ParameterType.Name), Has.None.EqualTo(nameof(ICharacterRepository)), "Unequip must not depend on ICharacterRepository -- rule 4 is an Equip-only concern");

            // ODY-S05-502 legitimately introduces the standalone ActiveEffect
            // aggregate/repository elsewhere in this assembly/namespace (not
            // owned by Equipment, ADR-028 section 8.2 rule 2).
            string[] forbiddenTypeFragments = { "RemoveBodyPart", "WeaponMechanic", "ArmorMechanic", "Attack" };
            IEnumerable<string> inventoryTypeNames = typeof(EquipmentService).Assembly.GetTypes()
                .Where(t => string.Equals(t.Namespace, "Odyssey.Application.Inventory", StringComparison.Ordinal))
                .Select(t => t.Name);
            foreach (string typeName in inventoryTypeNames)
            {
                foreach (string forbidden in forbiddenTypeFragments)
                {
                    Assert.That(typeName, Does.Not.Contain(forbidden));
                }
            }
        }

        private EquippedEntryRecord EquipItemDirectly(InventoryRecord inventory, InventoryItemRef itemRef, long expectedItemRevision)
        {
            EquippedEntryRecord record = BuildEquippedEntryRecord(inventory, itemRef, Array.Empty<BodyPartId>());
            Result<EquippedEntryRecord> equipped = _inventoryRepository.EquipItem(_campaign, new EquipTransition(record, expectedItemRevision, NewCommandId()), TestCorrelationId);
            Assert.That(equipped.IsSuccess, Is.True);
            return equipped.Value;
        }

        private CharacterRecord CreateInitializedCharacter()
        {
            var request = new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Equip Command Test Character");
            Result<CharacterRecord> created = _characterRepository.CreateCharacter(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            Result<CharacterRecord> initialized = _characterRepository.InitializeCharacterAnatomy(_campaign, created.Value.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, created.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
            Assert.That(initialized.IsSuccess, Is.True);
            return initialized.Value;
        }

        private InventoryRecord CreateInventory(CharacterId characterId)
        {
            UtcInstant now = Clock.GetUtcNow();
            var record = new InventoryRecord(InventoryId.NewId(now), _campaign.CampaignId, InventoryOwnerRef.ForCharacter(characterId), 1, now, now);
            Result<InventoryRecord> created = _inventoryRepository.CreateInventory(_campaign, record, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        private InventoryRecord CreateSceneOwnedInventory()
        {
            UtcInstant now = Clock.GetUtcNow();
            var record = new InventoryRecord(InventoryId.NewId(now), _campaign.CampaignId, InventoryOwnerRef.ForScene(SceneId.NewId(now), "battle_map"), 1, now, now);
            Result<InventoryRecord> created = _inventoryRepository.CreateInventory(_campaign, record, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        private ItemInstanceRecord CreateItemInstance(InventoryRecord inventory)
        {
            UtcInstant now = Clock.GetUtcNow();
            ContentDefinitionRef sourceRef = new ContentDefinitionRef(ContentDefinitionId.NewId(now), 1);
            var snapshot = new ItemMechanicsSnapshot(sourceRef, 1, ContentDefinitionType.Item, "{}");
            var record = new ItemInstanceRecord(ItemInstanceId.NewId(now), inventory.CampaignId, inventory.InventoryId, inventory.OwnerRef, InventoryLocationRef.Contained(inventory.InventoryId, "main"), sourceRef, snapshot, "{}", 1, now, now);
            Result<ItemInstanceRecord> created = _inventoryRepository.CreateItemInstance(_campaign, record, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        private ItemStackRecord CreateItemStack(InventoryRecord inventory)
        {
            UtcInstant now = Clock.GetUtcNow();
            ContentDefinitionRef sourceRef = new ContentDefinitionRef(ContentDefinitionId.NewId(now), 1);
            var snapshot = new ItemMechanicsSnapshot(sourceRef, 1, ContentDefinitionType.Ammo, "{}");
            var record = new ItemStackRecord(ItemStackId.NewId(now), inventory.CampaignId, inventory.InventoryId, inventory.OwnerRef, InventoryLocationRef.Contained(inventory.InventoryId, "main"), sourceRef, snapshot, ItemStackQuantity.Create(3), "{}", 1, now, now);
            Result<ItemStackRecord> created = _inventoryRepository.CreateItemStack(_campaign, record, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        private EquippedEntryRecord BuildEquippedEntryRecord(InventoryRecord inventory, InventoryItemRef itemRef, System.Collections.Generic.IReadOnlyList<BodyPartId> bodyPartRefs)
        {
            var entry = new EquippedEntry(inventory.InventoryId, itemRef, "chest_slot", bodyPartRefs, NewUserId(), Clock.GetUtcNow(), 1);
            return new EquippedEntryRecord(inventory.CampaignId, entry);
        }

        private void InsertEquippedEntryRowDirectly(EquippedEntryRecord record)
        {
            string itemRefId = record.Entry.ItemRef.Kind == InventoryItemRefKind.ItemInstance
                ? record.Entry.ItemRef.ItemInstanceId.ToString()
                : record.Entry.ItemRef.ItemStackId.ToString();

            using var connection = new SqliteConnection("Data Source=" + Path.Combine(_campaignDir, "campaign.db"));
            connection.Open();
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO EquippedEntry (ItemRefId, ItemRefKind, CampaignId, InventoryId, EquipmentSlotRef, BodyPartRefs, EquippedByUserId, EquippedAt, Revision, CreatedAt, UpdatedAt) " +
                "VALUES ($itemRefId, $itemRefKind, $campaignId, $inventoryId, $equipmentSlotRef, '', $equippedByUserId, $equippedAt, 1, $now, $now);";
            insert.Parameters.AddWithValue("$itemRefId", itemRefId);
            insert.Parameters.AddWithValue("$itemRefKind", record.Entry.ItemRef.Kind.ToString());
            insert.Parameters.AddWithValue("$campaignId", record.CampaignId.ToString());
            insert.Parameters.AddWithValue("$inventoryId", record.Entry.InventoryId.ToString());
            insert.Parameters.AddWithValue("$equipmentSlotRef", record.Entry.EquipmentSlotRef);
            insert.Parameters.AddWithValue("$equippedByUserId", record.Entry.EquippedByUserId.ToString());
            insert.Parameters.AddWithValue("$equippedAt", record.Entry.EquippedAt.ToString());
            insert.Parameters.AddWithValue("$now", Clock.GetUtcNow().ToString());
            insert.ExecuteNonQuery();
        }
    }
}
