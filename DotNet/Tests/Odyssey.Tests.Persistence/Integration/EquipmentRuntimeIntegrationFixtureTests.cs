using System;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.CharacterAdvancement;
using Odyssey.Application.Commands;
using Odyssey.Application.Content;
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

namespace Odyssey.Tests.Persistence.Integration
{
    /// <summary>
    /// ODY-S05-306: end-to-end integration fixture for the Equipment runtime
    /// block (`ODY-S05-301`-`305`). Mirrors `ODY-S05-207`'s
    /// `InventoryRuntimeIntegrationFixtureTests`: no new production code, only
    /// a composition of already accepted public services into one coherent
    /// MainGM sequence that reads as a real session --
    ///   catalog authoring/publish (Armor or Weapon) -> create a runtime
    ///   <c>ItemInstance</c> from the Published definition -> Equip through
    ///   the real MainGM-gated <see cref="EquipmentService.Equip"/> -> a
    ///   RemoveBodyPart on the referenced body part is blocked -> Unequip
    ///   through <see cref="EquipmentService.Unequip"/> -> RemoveBodyPart then
    ///   succeeds.
    /// `ODY-S05-305`'s own unit tests construct `EquippedEntry` via a
    /// test-only `EquipDirectly` helper that bypasses the MainGM gate; this
    /// file is what actually proves the fully-gated chain end-to-end. No
    /// weapon/armor mechanical effect (protection, damage) is asserted --
    /// `ADR-027` section 7 rule 6 keeps those on the item, not Equipment
    /// placement.
    /// </summary>
    public sealed class EquipmentRuntimeIntegrationFixtureTests
    {
        private const string ActiveRuleset = "ruleset.core@1.0.0";
        private static readonly AnatomyProfileDefinitionId Humanoid = AnatomyProfileDefinitionId.Parse("Humanoid");

        private static readonly CorrelationId Corr = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaigns = null!;
        private SqliteContentCatalogRepository _catalog = null!;
        private SqliteInventoryRepository _inventory = null!;
        private SqliteCharacterRepository _characters = null!;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s05-306-" + Guid.NewGuid().ToString("N"));
            _campaigns = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> created = _campaigns.Create(new CreateCampaignRequest(_campaignDir, "Equipment Runtime Integration Fixture", "ruleset.core", "1.0.0", "0.1.0"), NewCommandId(), Corr);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _catalog = new SqliteContentCatalogRepository(Clock);
            _inventory = new SqliteInventoryRepository(Clock);
            _characters = new SqliteCharacterRepository(Clock,
                deletionDependencyCheckers: new ICharacterDeletionDependencyChecker[] { new InventoryCharacterDeletionDependencyChecker(_inventory) },
                bodyPartRemovalDependencyCheckers: new IBodyPartRemovalDependencyChecker[] { new InventoryBodyPartRemovalDependencyChecker(_inventory) });
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaigns.Close(_campaign, Corr); } catch (IOException) { }
            try { if (Directory.Exists(_campaignDir)) Directory.Delete(_campaignDir, true); } catch (IOException) { }
        }

        [Test] // TC-INVENTORY-153
        public void EquipmentRuntimeBlock_ComposesEndToEnd_WithArmor()
        {
            ContentDefinitionRecord published = PublishArmor(EncodeArmorFixture());
            CharacterRecord character = CreateInitializedCharacter();
            InventoryRecord inventory = CreateInventory(character.CharacterId);
            ItemInstanceRecord instance = CreateRuntimeItemInstance(published, inventory);

            // Head has no internal Character-only dependent (unlike Torso,
            // which LeftArm/RightArm both attach to and so can never be
            // removed regardless of equipment state) -- using it isolates
            // this round trip to the equipment dependency alone.
            RunFullEquipRoundTrip(character, inventory, instance, BodyPartId.Parse("Head"));
        }

        [Test] // TC-INVENTORY-154
        public void EquipmentRuntimeBlock_ComposesEndToEnd_WithWeapon_NotArmorSpecific()
        {
            ContentDefinitionRecord published = PublishWeapon(EncodeWeaponFixture());
            CharacterRecord character = CreateInitializedCharacter();
            InventoryRecord inventory = CreateInventory(character.CharacterId);
            ItemInstanceRecord instance = CreateRuntimeItemInstance(published, inventory);

            RunFullEquipRoundTrip(character, inventory, instance, BodyPartId.Parse("RightArm"));
        }

        [Test] // TC-INVENTORY-155
        public void RemoveBodyPart_OnAnUnrelatedBodyPart_SucceedsWhileTheReferencedOneIsStillEquipped()
        {
            ContentDefinitionRecord published = PublishArmor(EncodeArmorFixture());
            CharacterRecord character = CreateInitializedCharacter();
            InventoryRecord inventory = CreateInventory(character.CharacterId);
            ItemInstanceRecord instance = CreateRuntimeItemInstance(published, inventory);

            Result<EquippedEntryRecord> equipped = EquipmentService.Equip(_inventory, _characters, new EquipRequest(
                _campaign, InventoryItemRef.ForInstance(instance.ItemInstanceId), inventory.InventoryId, instance.Revision,
                "chest_slot", new[] { BodyPartId.Parse("Head") }, NewUserId(), Clock.GetUtcNow(),
                NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(equipped.IsSuccess, Is.True);

            // LeftArm has no internal Character-only dependent (nothing
            // attaches to LeftArm itself -- it attaches to Torso, not the
            // other way around) and is not referenced by the equipped item's
            // BodyPartRefs (Head), so it must succeed even while Head remains
            // equipment-blocked -- proving the real
            // InventoryBodyPartRemovalDependencyChecker, reached through the
            // full EquipmentService.Equip-created EquippedEntry, is scoped to
            // the specific body part.
            Result<CharacterRecord> leftArmRemoved = _characters.RemoveBodyPart(
                _campaign, character.CharacterId, BodyPartId.Parse("LeftArm"), NewUserId(), actorIsMainGm: true,
                character.Revisions.CharacterAnatomyRevision, NewCommandId(), Corr);
            Assert.That(leftArmRemoved.IsSuccess, Is.True);

            Result<CharacterRecord> headRemoved = _characters.RemoveBodyPart(
                _campaign, character.CharacterId, BodyPartId.Parse("Head"), NewUserId(), actorIsMainGm: true,
                leftArmRemoved.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), Corr);
            Assert.That(headRemoved.IsFailure, Is.True);
            Assert.That(headRemoved.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterBodyPartHasDependent));
        }

        [Test] // TC-INVENTORY-156
        public void RoundTrip_LeavesTheItemContainedAndPhysicallyPresent()
        {
            ContentDefinitionRecord published = PublishArmor(EncodeArmorFixture());
            CharacterRecord character = CreateInitializedCharacter();
            InventoryRecord inventory = CreateInventory(character.CharacterId);
            ItemInstanceRecord instance = CreateRuntimeItemInstance(published, inventory);
            BodyPartId bodyPart = BodyPartId.Parse("Head");

            Result<EquippedEntryRecord> equipped = EquipmentService.Equip(_inventory, _characters, new EquipRequest(
                _campaign, InventoryItemRef.ForInstance(instance.ItemInstanceId), inventory.InventoryId, instance.Revision,
                "chest_slot", new[] { bodyPart }, NewUserId(), Clock.GetUtcNow(),
                NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(equipped.IsSuccess, Is.True);

            Result<bool> unequipped = EquipmentService.Unequip(_inventory, new UnequipRequest(
                _campaign, InventoryItemRef.ForInstance(instance.ItemInstanceId), inventory.InventoryId,
                instance.Revision + 1, equipped.Value.Entry.Revision, "main",
                NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(unequipped.IsSuccess, Is.True);

            Result<ItemInstanceRecord> reread = _inventory.GetItemInstance(_campaign, instance.ItemInstanceId, Corr);
            Assert.That(reread.IsSuccess, Is.True, "the item must still physically exist after Unequip");
            Assert.That(reread.Value.LocationRef, Is.EqualTo(InventoryLocationRef.Contained(inventory.InventoryId, "main")));
        }

        // ---- shared round-trip sequence: Equip -> blocked RemoveBodyPart -> Unequip -> successful RemoveBodyPart ----

        private void RunFullEquipRoundTrip(CharacterRecord character, InventoryRecord inventory, ItemInstanceRecord instance, BodyPartId bodyPart)
        {
            Result<EquippedEntryRecord> equipped = EquipmentService.Equip(_inventory, _characters, new EquipRequest(
                _campaign, InventoryItemRef.ForInstance(instance.ItemInstanceId), inventory.InventoryId, instance.Revision,
                "chest_slot", new[] { bodyPart }, NewUserId(), Clock.GetUtcNow(),
                NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(equipped.IsSuccess, Is.True, "Equip must succeed through the real MainGM-gated EquipmentService");
            Assert.That(equipped.Value.Entry.BodyPartRefs, Is.EqualTo(new[] { bodyPart }));

            Result<CharacterRecord> blocked = _characters.RemoveBodyPart(
                _campaign, character.CharacterId, bodyPart, NewUserId(), actorIsMainGm: true,
                character.Revisions.CharacterAnatomyRevision, NewCommandId(), Corr);
            Assert.That(blocked.IsFailure, Is.True, "RemoveBodyPart must be blocked while the item is equipped");
            Assert.That(blocked.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterBodyPartHasDependent));
            bool stillHasBodyPart = false;
            foreach (BodyPart bp in _characters.GetCharacter(_campaign, character.CharacterId, Corr).Value.Anatomy!.BodyParts)
            {
                if (bp.BodyPartId.Equals(bodyPart)) stillHasBodyPart = true;
            }
            Assert.That(stillHasBodyPart, Is.True, "a blocked RemoveBodyPart must not remove the body part");

            Result<bool> unequipped = EquipmentService.Unequip(_inventory, new UnequipRequest(
                _campaign, InventoryItemRef.ForInstance(instance.ItemInstanceId), inventory.InventoryId,
                instance.Revision + 1, equipped.Value.Entry.Revision, "main",
                NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(unequipped.IsSuccess, Is.True, "Unequip must succeed through the real MainGM-gated EquipmentService");

            Result<CharacterRecord> removed = _characters.RemoveBodyPart(
                _campaign, character.CharacterId, bodyPart, NewUserId(), actorIsMainGm: true,
                character.Revisions.CharacterAnatomyRevision, NewCommandId(), Corr);
            Assert.That(removed.IsSuccess, Is.True, "RemoveBodyPart must succeed once the item is unequipped");
            bool stillPresent = false;
            foreach (BodyPart bp in removed.Value.Anatomy!.BodyParts)
            {
                if (bp.BodyPartId.Equals(bodyPart)) stillPresent = true;
            }
            Assert.That(stillPresent, Is.False, "the body part must actually be gone after a successful RemoveBodyPart");
        }

        // ---- helpers (private methods, no new production type) ----

        private static string EncodeArmorFixture()
        {
            var item = new ItemDefinition(
                ItemCategory.Generic, isStackable: false, maxStackSize: null, weight: 3,
                hasDurability: false, maxDurability: null, hasCharges: false, maxCharges: null,
                Array.Empty<ContentDefinitionRef>(), Array.Empty<ContentDefinitionRef>());
            var armor = new ArmorDefinition(item, "chest_slot", new[] { BodyPartId.Parse("Head") }, protection: 5);
            return TypedDefinitionCodec.EncodeArmor(armor);
        }

        private static string EncodeWeaponFixture()
        {
            var item = new ItemDefinition(
                ItemCategory.Generic, isStackable: false, maxStackSize: null, weight: 2,
                hasDurability: false, maxDurability: null, hasCharges: false, maxCharges: null,
                Array.Empty<ContentDefinitionRef>(), Array.Empty<ContentDefinitionRef>());
            var weapon = new WeaponDefinition(item, "1d6", range: 1, WeaponAttackMode.Melee, actionCost: 1, AmmoRequirement.None, Array.Empty<string>());
            return TypedDefinitionCodec.EncodeWeapon(weapon);
        }

        private ContentDefinitionRecord PublishArmor(string propertiesJson)
        {
            Result<ContentDefinitionRecord> draft = ContentCatalogAuthoringService.CreateDraftDefinition(
                _catalog, new CreateDraftDefinitionRequest(
                    _campaign, ContentDefinitionType.Armor, "Integration Fixture Chestplate", "ODY-S05-306 integration fixture", NewUserId(), actorIsMainGm: true, NewCommandId(), Corr,
                    rulesetCompatibility: new[] { ActiveRuleset }, propertiesJson: propertiesJson));
            Assert.That(draft.IsSuccess, Is.True, draft.IsFailure ? draft.Error.Code.ToString() : string.Empty);

            Result<ContentDefinitionRecord> published = ContentCatalogLifecycleService.PublishDefinition(
                _catalog, new PublishDefinitionRequest(_campaign, draft.Value.ContentDefinitionId, draft.Value.Revision, NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(published.IsSuccess, Is.True, published.IsFailure ? published.Error.Code.ToString() : string.Empty);
            return published.Value;
        }

        private ContentDefinitionRecord PublishWeapon(string propertiesJson)
        {
            Result<ContentDefinitionRecord> draft = ContentCatalogAuthoringService.CreateDraftDefinition(
                _catalog, new CreateDraftDefinitionRequest(
                    _campaign, ContentDefinitionType.Weapon, "Integration Fixture Shortsword", "ODY-S05-306 integration fixture", NewUserId(), actorIsMainGm: true, NewCommandId(), Corr,
                    rulesetCompatibility: new[] { ActiveRuleset }, propertiesJson: propertiesJson));
            Assert.That(draft.IsSuccess, Is.True, draft.IsFailure ? draft.Error.Code.ToString() : string.Empty);

            Result<ContentDefinitionRecord> published = ContentCatalogLifecycleService.PublishDefinition(
                _catalog, new PublishDefinitionRequest(_campaign, draft.Value.ContentDefinitionId, draft.Value.Revision, NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(published.IsSuccess, Is.True, published.IsFailure ? published.Error.Code.ToString() : string.Empty);
            return published.Value;
        }

        private ItemInstanceRecord CreateRuntimeItemInstance(ContentDefinitionRecord published, InventoryRecord inventory)
        {
            Result<ItemInstanceRecord> created = InventoryCreationService.CreateItemInstanceFromDefinition(
                _catalog, _inventory, Clock,
                new CreateItemInstanceFromDefinitionRequest(
                    _campaign, ItemInstanceId.NewId(Clock.GetUtcNow()), inventory.InventoryId, inventory.OwnerRef,
                    InventoryLocationRef.Contained(inventory.InventoryId, "main"), published.ContentDefinitionId,
                    NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(created.IsSuccess, Is.True, created.IsFailure ? created.Error.Code.ToString() : string.Empty);
            return created.Value;
        }

        private CharacterRecord CreateInitializedCharacter()
        {
            var request = new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Equipment Integration Fixture Character");
            Result<CharacterRecord> created = _characters.CreateCharacter(request, NewCommandId(), Corr);
            Assert.That(created.IsSuccess, Is.True);
            Result<CharacterRecord> initialized = CharacterAdvancementService.InitializeAnatomyWithDefaults(_characters, _campaign, created.Value.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, created.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), Corr);
            Assert.That(initialized.IsSuccess, Is.True);
            return initialized.Value;
        }

        private InventoryRecord CreateInventory(CharacterId ownerCharacterId)
        {
            UtcInstant now = Clock.GetUtcNow();
            var record = new InventoryRecord(InventoryId.NewId(now), _campaign.CampaignId, InventoryOwnerRef.ForCharacter(ownerCharacterId), 1, now, now);
            Assert.That(_inventory.CreateInventory(_campaign, record, NewCommandId(), Corr).IsSuccess, Is.True);
            return record;
        }
    }
}
