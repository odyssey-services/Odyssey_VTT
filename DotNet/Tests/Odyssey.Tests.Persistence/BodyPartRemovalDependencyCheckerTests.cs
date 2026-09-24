using System;
using System.IO;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Odyssey.Application.CharacterAdvancement;
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
    /// ODY-S05-305: real SQLite tests for the `RemoveBodyPart`
    /// item/equipment-dependency closure -- the new
    /// <see cref="IInventoryRepository.HasAnyEquippedEntryReferencingBodyPart"/>
    /// query primitive and the <see cref="InventoryBodyPartRemovalDependencyChecker"/>
    /// wired into <see cref="SqliteCharacterRepository.RemoveBodyPart"/>. No
    /// atomic auto-Unequip, no change to `EquipItem`/`UnequipItem`/
    /// `EquippedEntry.cs`, and no touch to `DeleteCharacterPermanently` or its
    /// existing checkers.
    /// </summary>
    public sealed class BodyPartRemovalDependencyCheckerTests
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

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s05-305-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> created = _campaignRepository.Create(new CreateCampaignRequest(_campaignDir, "RemoveBodyPart Dependency Test Campaign", "ruleset.core", "1.0.0", "0.1.0"), NewCommandId(), TestCorrelationId);
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

        // ---------- HasAnyEquippedEntryReferencingBodyPart ----------

        [Test] // TC-INVENTORY-143
        public void HasAnyEquippedEntryReferencingBodyPart_TrueForSingleValueMatch()
        {
            CharacterId characterId = CharacterId.NewId(Clock.GetUtcNow());
            InventoryRecord inventory = CreateInventory(characterId);
            ItemInstanceRecord instance = CreateItemInstance(inventory);
            EquipDirectly(inventory, InventoryItemRef.ForInstance(instance.ItemInstanceId), instance.Revision, new[] { BodyPartId.Parse("head") });

            Result<bool> result = _inventoryRepository.HasAnyEquippedEntryReferencingBodyPart(_campaign, _campaign.CampaignId, characterId, BodyPartId.Parse("head"), TestCorrelationId);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.True);
        }

        [Test] // TC-INVENTORY-144
        public void HasAnyEquippedEntryReferencingBodyPart_TrueWhenSearchedPartIsFirst()
        {
            CharacterId characterId = CharacterId.NewId(Clock.GetUtcNow());
            InventoryRecord inventory = CreateInventory(characterId);
            ItemInstanceRecord instance = CreateItemInstance(inventory);
            EquipDirectly(inventory, InventoryItemRef.ForInstance(instance.ItemInstanceId), instance.Revision, new[] { BodyPartId.Parse("head"), BodyPartId.Parse("leftHand"), BodyPartId.Parse("rightFoot") });

            Assert.That(_inventoryRepository.HasAnyEquippedEntryReferencingBodyPart(_campaign, _campaign.CampaignId, characterId, BodyPartId.Parse("head"), TestCorrelationId).Value, Is.True);
        }

        [Test] // TC-INVENTORY-145
        public void HasAnyEquippedEntryReferencingBodyPart_TrueWhenSearchedPartIsMiddle()
        {
            CharacterId characterId = CharacterId.NewId(Clock.GetUtcNow());
            InventoryRecord inventory = CreateInventory(characterId);
            ItemInstanceRecord instance = CreateItemInstance(inventory);
            EquipDirectly(inventory, InventoryItemRef.ForInstance(instance.ItemInstanceId), instance.Revision, new[] { BodyPartId.Parse("head"), BodyPartId.Parse("leftHand"), BodyPartId.Parse("rightFoot") });

            Assert.That(_inventoryRepository.HasAnyEquippedEntryReferencingBodyPart(_campaign, _campaign.CampaignId, characterId, BodyPartId.Parse("leftHand"), TestCorrelationId).Value, Is.True);
        }

        [Test] // TC-INVENTORY-146
        public void HasAnyEquippedEntryReferencingBodyPart_TrueWhenSearchedPartIsLast()
        {
            CharacterId characterId = CharacterId.NewId(Clock.GetUtcNow());
            InventoryRecord inventory = CreateInventory(characterId);
            ItemInstanceRecord instance = CreateItemInstance(inventory);
            EquipDirectly(inventory, InventoryItemRef.ForInstance(instance.ItemInstanceId), instance.Revision, new[] { BodyPartId.Parse("head"), BodyPartId.Parse("leftHand"), BodyPartId.Parse("rightFoot") });

            Assert.That(_inventoryRepository.HasAnyEquippedEntryReferencingBodyPart(_campaign, _campaign.CampaignId, characterId, BodyPartId.Parse("rightFoot"), TestCorrelationId).Value, Is.True);
        }

        [Test] // TC-INVENTORY-147
        public void HasAnyEquippedEntryReferencingBodyPart_FalseForPrefixSharingName()
        {
            CharacterId characterId = CharacterId.NewId(Clock.GetUtcNow());
            InventoryRecord inventory = CreateInventory(characterId);
            ItemInstanceRecord instance = CreateItemInstance(inventory);
            EquipDirectly(inventory, InventoryItemRef.ForInstance(instance.ItemInstanceId), instance.Revision, new[] { BodyPartId.Parse("headBackup") });

            Result<bool> result = _inventoryRepository.HasAnyEquippedEntryReferencingBodyPart(_campaign, _campaign.CampaignId, characterId, BodyPartId.Parse("head"), TestCorrelationId);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.False, "searching \"head\" must not false-match \"headBackup\"");
        }

        // ---------- RemoveBodyPart + real checker ----------

        [Test] // TC-INVENTORY-148
        public void RemoveBodyPart_BlockedByEquippedItem_WithWiredChecker_NoStateChange()
        {
            SqliteCharacterRepository characters = CharactersWithBodyPartChecker();
            CharacterRecord character = CreateInitializedCharacter(characters);
            InventoryRecord inventory = CreateInventory(character.CharacterId);
            ItemInstanceRecord instance = CreateItemInstance(inventory);
            EquipDirectly(inventory, InventoryItemRef.ForInstance(instance.ItemInstanceId), instance.Revision, new[] { BodyPartId.Parse("Head") });

            Result<CharacterRecord> removed = characters.RemoveBodyPart(_campaign, character.CharacterId, BodyPartId.Parse("Head"), NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Assert.That(removed.IsFailure, Is.True);
            Assert.That(removed.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterBodyPartHasDependent));
            Result<CharacterRecord> unchanged = characters.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(unchanged.Value.Anatomy!.Revision, Is.EqualTo(character.Anatomy!.Revision), "a blocked RemoveBodyPart must not advance the CharacterAnatomy revision");
            bool stillHasBodyPart = false;
            foreach (BodyPart bp in unchanged.Value.Anatomy!.BodyParts) { if (bp.BodyPartId.Equals(BodyPartId.Parse("Head"))) stillHasBodyPart = true; }
            Assert.That(stillHasBodyPart, Is.True);
        }

        [Test] // TC-INVENTORY-149
        public void RemoveBodyPart_SucceedsAfterUnequip()
        {
            SqliteCharacterRepository characters = CharactersWithBodyPartChecker();
            CharacterRecord character = CreateInitializedCharacter(characters);
            InventoryRecord inventory = CreateInventory(character.CharacterId);
            ItemInstanceRecord instance = CreateItemInstance(inventory);
            EquippedEntryRecord equipped = EquipDirectly(inventory, InventoryItemRef.ForInstance(instance.ItemInstanceId), instance.Revision, new[] { BodyPartId.Parse("Head") });

            Result<bool> unequipped = _inventoryRepository.UnequipItem(_campaign, new UnequipTransition(equipped.Entry.ItemRef, inventory.InventoryId, instance.Revision + 1, equipped.Entry.Revision, "main", NewCommandId()), TestCorrelationId);
            Assert.That(unequipped.IsSuccess, Is.True);

            Result<CharacterRecord> removed = characters.RemoveBodyPart(_campaign, character.CharacterId, BodyPartId.Parse("Head"), NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Assert.That(removed.IsSuccess, Is.True);
        }

        [Test] // TC-INVENTORY-150
        public void RemoveBodyPart_WithNoChecker_BehavesAsBefore()
        {
            var characters = new SqliteCharacterRepository(Clock);
            CharacterRecord character = CreateInitializedCharacter(characters);
            InventoryRecord inventory = CreateInventory(character.CharacterId);
            ItemInstanceRecord instance = CreateItemInstance(inventory);
            EquipDirectly(inventory, InventoryItemRef.ForInstance(instance.ItemInstanceId), instance.Revision, new[] { BodyPartId.Parse("Head") });

            // No IBodyPartRemovalDependencyChecker was passed to this repository
            // instance, so the new check is simply absent -- RemoveBodyPart
            // succeeds exactly as it did before ODY-S05-305, even though the
            // item is (unrealistically, for this test) still equipped.
            Result<CharacterRecord> removed = characters.RemoveBodyPart(_campaign, character.CharacterId, BodyPartId.Parse("Head"), NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Assert.That(removed.IsSuccess, Is.True);

            // The two pre-existing internal Character-only checks are unaffected:
            // LeftArm is attached to Torso in the default humanoid fixture, so
            // removing Torso is still blocked.
            Result<CharacterRecord> torsoRemoval = characters.RemoveBodyPart(_campaign, character.CharacterId, BodyPartId.Parse("Torso"), NewUserId(), actorIsMainGm: true, removed.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
            Assert.That(torsoRemoval.IsFailure, Is.True);
            Assert.That(torsoRemoval.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterBodyPartHasDependent));
        }

        [Test] // TC-INVENTORY-151
        public void RemoveBodyPart_WithWiredChecker_OnlyBlocksTheSpecificReferencedBodyPart()
        {
            SqliteCharacterRepository characters = CharactersWithBodyPartChecker();
            CharacterRecord character = CreateInitializedCharacter(characters);
            InventoryRecord inventory = CreateInventory(character.CharacterId);
            ItemInstanceRecord instance = CreateItemInstance(inventory);
            EquipDirectly(inventory, InventoryItemRef.ForInstance(instance.ItemInstanceId), instance.Revision, new[] { BodyPartId.Parse("LeftArm") });

            // RightArm is unrelated to the equipped item's BodyPartRefs and has
            // no internal dependent either, so it must succeed even while LeftArm
            // remains equipment-blocked -- proving the query is scoped to the
            // specific body part, not "this character has anything equipped."
            Result<CharacterRecord> rightArmRemoved = characters.RemoveBodyPart(_campaign, character.CharacterId, BodyPartId.Parse("RightArm"), NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
            Assert.That(rightArmRemoved.IsSuccess, Is.True);

            Result<CharacterRecord> leftArmRemoved = characters.RemoveBodyPart(_campaign, character.CharacterId, BodyPartId.Parse("LeftArm"), NewUserId(), actorIsMainGm: true, rightArmRemoved.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
            Assert.That(leftArmRemoved.IsFailure, Is.True);
            Assert.That(leftArmRemoved.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterBodyPartHasDependent));
        }

        [Test] // TC-INVENTORY-152
        public void RemoveBodyPart_FailsClosedWhenEquipmentStoreIsUnreadable()
        {
            SqliteCharacterRepository characters = CharactersWithBodyPartChecker();
            CharacterRecord character = CreateInitializedCharacter(characters);
            InventoryRecord inventory = CreateInventory(character.CharacterId);
            CreateItemInstance(inventory);
            BreakItemInstanceOwnerColumn();

            Result<CharacterRecord> removed = characters.RemoveBodyPart(_campaign, character.CharacterId, BodyPartId.Parse("Head"), NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Assert.That(removed.IsFailure, Is.True);
            Assert.That(removed.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterBodyPartHasDependent), "an unreadable Equipment store must block the removal, never silently allow it");
        }

        private SqliteCharacterRepository CharactersWithBodyPartChecker()
            => new SqliteCharacterRepository(Clock, bodyPartRemovalDependencyCheckers: new IBodyPartRemovalDependencyChecker[] { new InventoryBodyPartRemovalDependencyChecker(_inventoryRepository) });

        private CharacterRecord CreateInitializedCharacter(SqliteCharacterRepository characters)
        {
            var request = new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "RemoveBodyPart Dependency Test Character");
            Result<CharacterRecord> created = characters.CreateCharacter(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            Result<CharacterRecord> initialized = CharacterAdvancementService.InitializeAnatomyWithDefaults(characters, _campaign, created.Value.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, created.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
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

        private EquippedEntryRecord EquipDirectly(InventoryRecord inventory, InventoryItemRef itemRef, long expectedItemRevision, BodyPartId[] bodyPartRefs)
        {
            var entry = new EquippedEntry(inventory.InventoryId, itemRef, "chest_slot", bodyPartRefs, NewUserId(), Clock.GetUtcNow(), 1);
            var record = new EquippedEntryRecord(inventory.CampaignId, entry);
            Result<EquippedEntryRecord> equipped = _inventoryRepository.EquipItem(_campaign, new EquipTransition(record, expectedItemRevision, NewCommandId()), TestCorrelationId);
            Assert.That(equipped.IsSuccess, Is.True);
            return equipped.Value;
        }

        private void BreakItemInstanceOwnerColumn()
        {
            using SqliteConnection connection = Open();
            using var alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE ItemInstance RENAME COLUMN OwnerTargetRef TO OwnerTargetRef_broken;";
            alter.ExecuteNonQuery();
        }

        private SqliteConnection Open()
        {
            var connection = new SqliteConnection("Data Source=" + Path.Combine(_campaignDir, "campaign.db"));
            connection.Open();
            return connection;
        }
    }
}
