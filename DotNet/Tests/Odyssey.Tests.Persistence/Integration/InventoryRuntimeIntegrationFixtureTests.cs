using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
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
    /// ODY-S05-207: end-to-end integration fixture for the Inventory runtime
    /// block (`ODY-S05-201`-`206`). Mirrors `ODY-S05-106`'s Minimal Test
    /// Catalog Fixtures: no new production code, only a composition of already
    /// accepted public services into one coherent MainGM sequence that reads
    /// as a real session --
    ///   catalog authoring/publish -> create a runtime <c>ItemStack</c> from
    ///   the Published definition with a full copied mechanics snapshot ->
    ///   publish a new version and confirm the snapshot does NOT drift
    ///   (`ADR-027` §6.1) -> move the stack between inventories -> split then
    ///   merge it -> block permanent deletion of the character that still owns
    ///   it, allow it for the one that no longer does -> reject creating a
    ///   stack from a still-Draft definition.
    /// Each of `ODY-S05-201`-`206` already has its own isolated tests; this
    /// file proves they compose across their boundaries, which none of them
    /// checks on its own.
    /// </summary>
    public sealed class InventoryRuntimeIntegrationFixtureTests
    {
        private const string ActiveRuleset = "ruleset.core@1.0.0";

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
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s05-207-" + Guid.NewGuid().ToString("N"));
            _campaigns = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> created = _campaigns.Create(new CreateCampaignRequest(_campaignDir, "Inventory Runtime Integration Fixture", "ruleset.core", "1.0.0", "0.1.0"), NewCommandId(), Corr);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _catalog = new SqliteContentCatalogRepository(Clock);
            _inventory = new SqliteInventoryRepository(Clock);
            _characters = new SqliteCharacterRepository(Clock, deletionDependencyCheckers: new ICharacterDeletionDependencyChecker[] { new InventoryCharacterDeletionDependencyChecker(_inventory) });
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaigns.Close(_campaign, Corr); } catch (IOException) { }
            try { if (Directory.Exists(_campaignDir)) Directory.Delete(_campaignDir, true); } catch (IOException) { }
        }

        // ---- 1 + 3 + 4: catalog -> runtime stack -> move -> split -> merge ----

        [Test] // TC-INVENTORY-091
        public void InventoryRuntimeBlock_ComposesFromCatalogToStackOperations()
        {
            // 1. Catalog -> runtime: publish a stackable consumable, create an
            //    ItemStack in character A's inventory from the Published version.
            ContentDefinitionRecord published = PublishItem(EncodeStackableConsumable(maxStackSize: 20));
            var expectedRef = new ContentDefinitionRef(published.ContentDefinitionId, published.Version);

            CharacterRecord characterA = CreateCharacter("Owner A");
            CharacterRecord characterB = CreateCharacter("Owner B");
            InventoryRecord inventoryA = CreateInventory(characterA.CharacterId);
            InventoryRecord inventoryB = CreateInventory(characterB.CharacterId);

            Result<ItemStackRecord> createdStack = InventoryCreationService.CreateItemStackFromDefinition(
                _catalog, _inventory, Clock,
                new CreateItemStackFromDefinitionRequest(
                    _campaign, ItemStackId.NewId(Clock.GetUtcNow()), inventoryA.InventoryId, inventoryA.OwnerRef,
                    InventoryLocationRef.Contained(inventoryA.InventoryId, "main"), published.ContentDefinitionId,
                    ItemStackQuantity.Create(8), NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));

            Assert.That(createdStack.IsSuccess, Is.True);
            ItemStackRecord stack = createdStack.Value;
            Assert.That(stack.Quantity.Value, Is.EqualTo(8));
            Assert.That(stack.SourceItemDefinitionRef, Is.EqualTo(expectedRef));
            Assert.That(stack.MechanicsSnapshot.SourceDefinitionRef, Is.EqualTo(expectedRef));
            Assert.That(stack.MechanicsSnapshot.DefinitionSnapshotVersion, Is.EqualTo(1));
            Assert.That(stack.MechanicsSnapshot.ContentType, Is.EqualTo(ContentDefinitionType.Item));
            Assert.That(stack.MechanicsSnapshot.Payload, Is.EqualTo(published.PropertiesJson), "the runtime snapshot is a full copy of the definition at publish time");

            // 3. Move the stack from A's inventory to B's inventory.
            Result<ItemStackRecord> moved = InventoryMovementService.MoveItemStack(
                _inventory,
                new MoveItemStackRequest(
                    _campaign, stack.ItemStackId, expectedTargetRevision: 1, inventoryA.InventoryId, expectedSourceRevision: 1,
                    inventoryB.InventoryId, expectedDestinationRevision: 1, "main", NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));

            Assert.That(moved.IsSuccess, Is.True);
            Assert.That(moved.Value.InventoryId, Is.EqualTo(inventoryB.InventoryId));
            Assert.That(moved.Value.Revision, Is.EqualTo(2), "the moved stack's revision increments");
            Assert.That(_inventory.GetInventory(_campaign, inventoryA.InventoryId, Corr).Value.Revision, Is.EqualTo(2), "the source inventory revision increments");
            Assert.That(_inventory.GetInventory(_campaign, inventoryB.InventoryId, Corr).Value.Revision, Is.EqualTo(2), "the destination inventory revision increments");

            // 4a. Split the stack (now in inventory B) into a 3-unit part.
            ItemStackId splitOffId = ItemStackId.NewId(Clock.GetUtcNow());
            Result<ItemStackRecord> split = InventoryStackOperationService.Split(
                _inventory,
                new SplitItemStackRequest(
                    _campaign, stack.ItemStackId, splitOffId, inventoryB.InventoryId, quantity: 3,
                    expectedSourceRevision: 2, expectedInventoryRevision: 2, NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));

            Assert.That(split.IsSuccess, Is.True);
            ItemStackRecord splitOff = split.Value;
            ItemStackRecord remainder = _inventory.GetItemStack(_campaign, stack.ItemStackId, Corr).Value;
            Assert.That(splitOff.Quantity.Value + remainder.Quantity.Value, Is.EqualTo(8), "split conserves total quantity");
            Assert.That(splitOff.Quantity.Value, Is.EqualTo(3));
            Assert.That(splitOff.MechanicsSnapshot, Is.EqualTo(remainder.MechanicsSnapshot), "both parts keep the identical pinned snapshot");
            Assert.That(splitOff.StackState, Is.EqualTo(remainder.StackState));
            Assert.That(splitOff.OwnerRef, Is.EqualTo(remainder.OwnerRef));
            Assert.That(splitOff.LocationRef, Is.EqualTo(remainder.LocationRef));

            // 4b. Merge the split-off part back into the original stack.
            Result<ItemStackRecord> merged = InventoryStackOperationService.Merge(
                _inventory,
                new MergeItemStacksRequest(
                    _campaign, splitOffId, stack.ItemStackId, inventoryB.InventoryId,
                    expectedSourceRevision: 1, expectedDestinationRevision: 3, expectedInventoryRevision: 2,
                    NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));

            Assert.That(merged.IsSuccess, Is.True);
            Assert.That(merged.Value.ItemStackId, Is.EqualTo(stack.ItemStackId));
            Assert.That(merged.Value.Quantity.Value, Is.EqualTo(8), "merge restores the original total quantity");
            Result<ItemStackRecord> consumed = _inventory.GetItemStack(_campaign, splitOffId, Corr);
            Assert.That(consumed.IsFailure, Is.True);
            Assert.That(consumed.Error.Code, Is.EqualTo(ErrorCodes.PersistenceItemStackNotFound), "the consumed split-off stack is physically removed");
        }

        // ---- 2: snapshot immutability when a successor definition version is published ----

        [Test] // TC-INVENTORY-092
        public void RuntimeSnapshot_DoesNotDrift_WhenASuccessorDefinitionVersionIsPublished()
        {
            ContentDefinitionRecord original = PublishItem(EncodeStackableConsumable(maxStackSize: 10));
            string originalProperties = original.PropertiesJson;

            CharacterRecord character = CreateCharacter("Snapshot Owner");
            InventoryRecord inventory = CreateInventory(character.CharacterId);
            Result<ItemStackRecord> created = InventoryCreationService.CreateItemStackFromDefinition(
                _catalog, _inventory, Clock,
                new CreateItemStackFromDefinitionRequest(
                    _campaign, ItemStackId.NewId(Clock.GetUtcNow()), inventory.InventoryId, inventory.OwnerRef,
                    InventoryLocationRef.Contained(inventory.InventoryId, "main"), original.ContentDefinitionId,
                    ItemStackQuantity.Create(4), NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(created.IsSuccess, Is.True);
            ItemStackId stackId = created.Value.ItemStackId;

            // Branch a successor Draft from the Published definition, change its
            // properties, and publish it. (In this MVP CreateNextDraftVersionFromPublished
            // mints a new ContentDefinitionId; every publish is Version 1.)
            Result<ContentDefinitionRecord> nextDraft = ContentCatalogAuthoringService.CreateNextDraftVersionFromPublished(
                _catalog, new CreateNextDraftVersionFromPublishedRequest(_campaign, original.ContentDefinitionId, NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(nextDraft.IsSuccess, Is.True);
            Assert.That(nextDraft.Value.ContentDefinitionId, Is.Not.EqualTo(original.ContentDefinitionId));

            Result<ContentDefinitionRecord> updatedDraft = ContentCatalogAuthoringService.UpdateDraftDefinition(
                _catalog, new UpdateDraftDefinitionRequest(
                    _campaign, nextDraft.Value.ContentDefinitionId, "Stackable Consumable (successor)", "changed properties",
                    EncodeStackableConsumable(maxStackSize: 99), nextDraft.Value.Revision, NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(updatedDraft.IsSuccess, Is.True);
            Assert.That(updatedDraft.Value.PropertiesJson, Is.Not.EqualTo(originalProperties));

            Result<ContentDefinitionRecord> successorPublished = ContentCatalogLifecycleService.PublishDefinition(
                _catalog, new PublishDefinitionRequest(_campaign, updatedDraft.Value.ContentDefinitionId, updatedDraft.Value.Revision, NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(successorPublished.IsSuccess, Is.True);

            // The already-created stack's snapshot must still pin the original
            // definition and its original properties -- publishing a successor
            // does not alter an existing runtime item (ADR-027 section 6.1).
            ItemStackRecord reread = _inventory.GetItemStack(_campaign, stackId, Corr).Value;
            Assert.That(reread.SourceItemDefinitionRef.DefinitionId, Is.EqualTo(original.ContentDefinitionId));
            Assert.That(reread.SourceItemDefinitionRef.Version, Is.EqualTo(1));
            Assert.That(reread.MechanicsSnapshot.SourceDefinitionRef.DefinitionId, Is.EqualTo(original.ContentDefinitionId));
            Assert.That(reread.MechanicsSnapshot.DefinitionSnapshotVersion, Is.EqualTo(1));
            Assert.That(reread.MechanicsSnapshot.Payload, Is.EqualTo(originalProperties), "publishing a successor definition must not alter an existing runtime item's snapshot");
        }

        // ---- 5: character deletion gate through the full catalog->inventory path ----

        [Test] // TC-INVENTORY-093
        public void PermanentCharacterDeletion_IsBlockedForTheItemOwner_AndAllowedForTheEmptiedCharacter()
        {
            ContentDefinitionRecord published = PublishItem(EncodeStackableConsumable(maxStackSize: 20));

            CharacterRecord characterA = CreateCharacter("Emptied Owner");
            CharacterRecord characterB = CreateCharacter("Item Owner");
            InventoryRecord inventoryA = CreateInventory(characterA.CharacterId);
            InventoryRecord inventoryB = CreateInventory(characterB.CharacterId);

            Result<ItemStackRecord> created = InventoryCreationService.CreateItemStackFromDefinition(
                _catalog, _inventory, Clock,
                new CreateItemStackFromDefinitionRequest(
                    _campaign, ItemStackId.NewId(Clock.GetUtcNow()), inventoryA.InventoryId, inventoryA.OwnerRef,
                    InventoryLocationRef.Contained(inventoryA.InventoryId, "main"), published.ContentDefinitionId,
                    ItemStackQuantity.Create(5), NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(created.IsSuccess, Is.True);

            // The whole stack moves from A to B, so its OwnerRef becomes B's.
            Result<ItemStackRecord> moved = InventoryMovementService.MoveItemStack(
                _inventory,
                new MoveItemStackRequest(
                    _campaign, created.Value.ItemStackId, 1, inventoryA.InventoryId, 1, inventoryB.InventoryId, 1, "main",
                    NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(moved.IsSuccess, Is.True);

            // Character B still owns the stack -> permanent deletion is blocked.
            Result blockedB = _characters.DeleteCharacterPermanently(
                _campaign, characterB.CharacterId, "cleanup", NewUserId(), actorIsMainGm: true, characterB.Revisions.LifecycleRevision, NewCommandId(), Corr);
            Assert.That(blockedB.IsFailure, Is.True);
            Assert.That(blockedB.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterDeletionHasDependent));
            Assert.That(_characters.GetCharacter(_campaign, characterB.CharacterId, Corr).IsSuccess, Is.True);

            // Character A was emptied by the move -> permanent deletion proceeds.
            Result allowedA = _characters.DeleteCharacterPermanently(
                _campaign, characterA.CharacterId, "cleanup", NewUserId(), actorIsMainGm: true, characterA.Revisions.LifecycleRevision, NewCommandId(), Corr);
            Assert.That(allowedA.IsSuccess, Is.True);
            Assert.That(_characters.GetCharacter(_campaign, characterA.CharacterId, Corr).IsFailure, Is.True);
        }

        // ---- 6: safe rejection of a still-Draft definition ----

        [Test] // TC-INVENTORY-094
        public void CreateItemStackFromDefinition_OnADraftDefinition_IsRejectedByTheExistingNotPublishedError()
        {
            Result<ContentDefinitionRecord> draft = ContentCatalogAuthoringService.CreateDraftDefinition(
                _catalog, new CreateDraftDefinitionRequest(
                    _campaign, ContentDefinitionType.Item, "Unpublished Consumable", "ODY-S05-207 fixture", NewUserId(), actorIsMainGm: true, NewCommandId(), Corr,
                    rulesetCompatibility: new[] { ActiveRuleset }, propertiesJson: EncodeStackableConsumable(maxStackSize: 10)));
            Assert.That(draft.IsSuccess, Is.True);

            CharacterRecord character = CreateCharacter("Draft Rejection Owner");
            InventoryRecord inventory = CreateInventory(character.CharacterId);

            Result<ItemStackRecord> result = InventoryCreationService.CreateItemStackFromDefinition(
                _catalog, _inventory, Clock,
                new CreateItemStackFromDefinitionRequest(
                    _campaign, ItemStackId.NewId(Clock.GetUtcNow()), inventory.InventoryId, inventory.OwnerRef,
                    InventoryLocationRef.Contained(inventory.InventoryId, "main"), draft.Value.ContentDefinitionId,
                    ItemStackQuantity.Create(1), NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.InventoryCreateDefinitionNotPublished));
            Assert.That(CountRows("SELECT COUNT(*) FROM ItemStack;"), Is.EqualTo(0), "no runtime row is written for a rejected creation");
        }

        // ---- helpers (private methods, no new production type) ----

        private static string EncodeStackableConsumable(long maxStackSize)
        {
            var item = new ItemDefinition(
                ItemCategory.Consumable, isStackable: true, maxStackSize: maxStackSize, weight: 1,
                hasDurability: false, maxDurability: null, hasCharges: false, maxCharges: null,
                Array.Empty<ContentDefinitionRef>(), Array.Empty<ContentDefinitionRef>());
            return TypedDefinitionCodec.EncodeItem(item);
        }

        private ContentDefinitionRecord PublishItem(string propertiesJson)
        {
            Result<ContentDefinitionRecord> draft = ContentCatalogAuthoringService.CreateDraftDefinition(
                _catalog, new CreateDraftDefinitionRequest(
                    _campaign, ContentDefinitionType.Item, "Stackable Consumable", "ODY-S05-207 integration fixture", NewUserId(), actorIsMainGm: true, NewCommandId(), Corr,
                    rulesetCompatibility: new[] { ActiveRuleset }, propertiesJson: propertiesJson));
            Assert.That(draft.IsSuccess, Is.True, draft.IsFailure ? draft.Error.Code.ToString() : string.Empty);

            Result<ContentDefinitionRecord> published = ContentCatalogLifecycleService.PublishDefinition(
                _catalog, new PublishDefinitionRequest(_campaign, draft.Value.ContentDefinitionId, draft.Value.Revision, NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(published.IsSuccess, Is.True, published.IsFailure ? published.Error.Code.ToString() : string.Empty);
            return published.Value;
        }

        private CharacterRecord CreateCharacter(string name)
        {
            Result<CharacterRecord> created = _characters.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, name), NewCommandId(), Corr);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        private InventoryRecord CreateInventory(CharacterId ownerCharacterId)
        {
            UtcInstant now = Clock.GetUtcNow();
            var record = new InventoryRecord(InventoryId.NewId(now), _campaign.CampaignId, InventoryOwnerRef.ForCharacter(ownerCharacterId), 1, now, now);
            Assert.That(_inventory.CreateInventory(_campaign, record, NewCommandId(), Corr).IsSuccess, Is.True);
            return record;
        }

        private long CountRows(string sql)
        {
            using var c = new SqliteConnection("Data Source=" + Path.Combine(_campaignDir, "campaign.db"));
            c.Open();
            using var q = c.CreateCommand();
            q.CommandText = sql;
            return (long)q.ExecuteScalar()!;
        }
    }
}
