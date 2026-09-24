using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
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
    /// ODY-S05-404: end-to-end integration fixture for the `ItemDefinition`
    /// migration block (`ODY-S05-401`-`403`). Mirrors `ODY-S05-207`'s
    /// <c>InventoryRuntimeIntegrationFixtureTests</c> and `ODY-S05-306`'s
    /// <c>EquipmentRuntimeIntegrationFixtureTests</c>: no new production
    /// code, only a composition of already accepted public services into one
    /// coherent MainGM sequence that reads as a real session --
    ///   publish a target `ItemDefinition` version through the real catalog
    ///   lifecycle -> build a preview through the real `BuildPreview` against
    ///   genuinely existing runtime records -> discover a real blocking
    ///   incompatibility through `ComputeBlockingIssues` (not a synthetic
    ///   `IncompatibilityReport`) -> confirm `ApplyItemDefinitionMigration`
    ///   refuses to proceed while it stands -> resolve it through the
    ///   existing Equipment runtime (`ODY-S05-304`'s `Unequip`) -> rebuild
    ///   the now-stale preview and confirm again -> apply succeeds
    ///   atomically -> read the updated snapshot columns back with a raw
    ///   SQL `SELECT`, the same way `ODY-S05-207`'s own fixture does.
    /// `ODY-S05-401`-`403` each already have their own isolated tests
    /// (including `ODY-S05-403`'s own `BuildRealPreview`-backed happy-path
    /// round trip); this file proves the one thing none of them do: a
    /// blocking incompatibility that `ComputeBlockingIssues` genuinely
    /// discovers against real data, and the resolve-then-reconfirm cycle
    /// `ADR-027` section 10 steps 5-7 require around it.
    /// </summary>
    public sealed class ItemDefinitionMigrationIntegrationFixtureTests
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
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s05-404-" + Guid.NewGuid().ToString("N"));
            _campaigns = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> created = _campaigns.Create(new CreateCampaignRequest(_campaignDir, "Migration Integration Fixture", "ruleset.core", "1.0.0", "0.1.0"), NewCommandId(), Corr);
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

        [Test] // TC-INVENTORY-189
        public void MigrationBlock_ComposesEndToEnd_PublishPreviewBlockResolveApply()
        {
            // 1. Publish source and target ARMOR versions through the real
            //    catalog lifecycle. No version-lineage mechanism exists
            //    (ODY-S05-401's own finding), so "migrating to a new version"
            //    is publishing an independent successor definition with a
            //    different equipment slot -- exactly the kind of change that
            //    makes an existing equip placement genuinely incompatible.
            ContentDefinitionRecord source = PublishArmor("Source", slot: "chest_slot");
            CharacterRecord character = CreateInitializedCharacter();
            InventoryRecord inventory = CreateInventory(character.CharacterId);
            ItemInstanceRecord instance = CreateArmorInstance(source, inventory);

            // 2. Equip it for real through ODY-S05-303's EquipmentService,
            //    into the slot the source (and, so far, only) definition
            //    defines.
            Result<EquippedEntryRecord> equipped = EquipmentService.Equip(_inventory, _characters, new EquipRequest(
                _campaign, InventoryItemRef.ForInstance(instance.ItemInstanceId), inventory.InventoryId, instance.Revision,
                "chest_slot", new[] { BodyPartId.Parse("Torso") }, NewUserId(), Clock.GetUtcNow(), NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(equipped.IsSuccess, Is.True, equipped.IsFailure ? equipped.Error.Code.ToString() : string.Empty);

            // 3. Publish a target version whose armor slot has moved --
            //    incompatible with the item's current, real equip placement.
            ContentDefinitionRecord target = PublishArmor("Target", slot: "head_slot");

            // 4. Build a real preview against the genuinely persisted
            //    ItemInstance/Inventory rows (ODY-S05-401's own BuildPreview,
            //    not a hand-built ItemDefinitionMigrationPreview).
            ItemDefinitionMigrationPreview blockedPreview = BuildRealPreview(source, target);
            Assert.That(blockedPreview.AffectedInstances.Count, Is.EqualTo(1));

            // 5. Discover the incompatibility through the real
            //    ComputeBlockingIssues (ODY-S05-402), against the item's
            //    real, currently-equipped EquippedEntry row -- not a
            //    synthetic IncompatibilityReport handed to the test.
            EquippedEntryRecord currentlyEquipped = _inventory.GetEquippedEntry(_campaign, InventoryItemRef.ForInstance(instance.ItemInstanceId), Corr).Value;
            ItemDefinitionMigrationIncompatibilityReport report = ItemDefinitionMigrationRules.ComputeBlockingIssues(blockedPreview, target, new[] { currentlyEquipped });
            Assert.That(report.HasBlockingIssues, Is.True, "the real equip placement in chest_slot must be genuinely incompatible with a target that only defines head_slot");
            Assert.That(report.Issues[0].IssueCode, Is.EqualTo(ItemDefinitionMigrationBlockingIssueCode.EquipmentSlotNoLongerDefined));

            long domainEventCountBeforeMigrationAttempts = CountRows("SELECT COUNT(*) FROM DomainEvents;");

            // 6. Confirming while the incompatibility stands must be
            //    refused -- ODY-S05-403's own host-authoritative
            //    ComputeBlockingIssues recheck, not merely this test
            //    skipping the attempt.
            Result<ItemDefinitionMigrationApplyResult> rejectedApply = _inventory.ApplyItemDefinitionMigration(
                _campaign, new ItemDefinitionMigrationTransition(blockedPreview, NewCommandId()), NewUserId(), actorIsMainGm: true, Corr);
            Assert.That(rejectedApply.IsFailure, Is.True);
            Assert.That(rejectedApply.Error.Code, Is.EqualTo(ErrorCodes.InventoryMigrationBlocked));
            Assert.That(_inventory.GetItemInstance(_campaign, instance.ItemInstanceId, Corr).Value.SourceItemDefinitionRef, Is.EqualTo(instance.SourceItemDefinitionRef), "a rejected apply attempt must not touch the item");

            // 7. Resolve it through the existing Equipment runtime
            //    (ODY-S05-304's Unequip) -- not by deleting/faking the
            //    EquippedEntry row directly.
            Result<bool> unequipped = EquipmentService.Unequip(_inventory, new UnequipRequest(
                _campaign, InventoryItemRef.ForInstance(instance.ItemInstanceId), inventory.InventoryId,
                instance.Revision + 1, equipped.Value.Entry.Revision, "main", NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(unequipped.IsSuccess, Is.True, unequipped.IsFailure ? unequipped.Error.Code.ToString() : string.Empty);

            // 8. Unequip changed the item's own Revision, so the original
            //    preview is now doubly stale (blocking issue AND revision);
            //    a real caller must rebuild it, exercising ODY-S05-403's own
            //    revision guard in a realistic scenario, not only a
            //    synthetic unit test.
            ItemDefinitionMigrationPreview freshPreview = BuildRealPreview(source, target);
            Result<EquippedEntryRecord> stillEquipped = _inventory.GetEquippedEntry(_campaign, InventoryItemRef.ForInstance(instance.ItemInstanceId), Corr);
            Assert.That(stillEquipped.IsFailure, Is.True, "the item must no longer be equipped after a real Unequip");
            ItemDefinitionMigrationIncompatibilityReport freshReport = ItemDefinitionMigrationRules.ComputeBlockingIssues(freshPreview, target, Array.Empty<EquippedEntryRecord>());
            Assert.That(freshReport.HasBlockingIssues, Is.False, "an unequipped item has no equipment-slot incompatibility left");

            // 9. Confirm and apply for real.
            Result<ItemDefinitionMigrationApplyResult> applied = _inventory.ApplyItemDefinitionMigration(
                _campaign, new ItemDefinitionMigrationTransition(freshPreview, NewCommandId()), NewUserId(), actorIsMainGm: true, Corr);
            Assert.That(applied.IsSuccess, Is.True, applied.IsFailure ? applied.Error.Code.ToString() : string.Empty);
            Assert.That(applied.Value.UpdatedInstanceCount, Is.EqualTo(1));

            // 10. Read the updated snapshot back with a raw SQL SELECT, the
            //     same way ODY-S05-207's own fixture verifies runtime state,
            //     not just through the method's own return value.
            (string sourceRef, string mechanicsSourceRef, string payload) row = ReadItemInstanceSnapshotColumns(instance.ItemInstanceId);
            var expectedTargetRef = new ContentDefinitionRef(target.ContentDefinitionId, target.Version);
            Assert.That(row.sourceRef, Is.EqualTo(expectedTargetRef.ToString()));
            Assert.That(row.mechanicsSourceRef, Is.EqualTo(expectedTargetRef.ToString()));
            Assert.That(row.payload, Is.EqualTo(target.PropertiesJson));

            long domainEventCountAfter = CountRows("SELECT COUNT(*) FROM DomainEvents;");
            Assert.That(domainEventCountAfter, Is.EqualTo(domainEventCountBeforeMigrationAttempts + 1),
                "only the one successful ApplyItemDefinitionMigration call appends a DomainEvents row -- the rejected attempt appends none, and no prior row is rewritten or removed");
        }

        [Test] // TC-INVENTORY-190
        public void ApplyItemDefinitionMigration_RejectedWhileBlockingIssueStands_DoesNotDeleteOrRewriteDomainEvents()
        {
            ContentDefinitionRecord source = PublishArmor("Source", slot: "chest_slot");
            CharacterRecord character = CreateInitializedCharacter();
            InventoryRecord inventory = CreateInventory(character.CharacterId);
            ItemInstanceRecord instance = CreateArmorInstance(source, inventory);
            EquipmentService.Equip(_inventory, _characters, new EquipRequest(
                _campaign, InventoryItemRef.ForInstance(instance.ItemInstanceId), inventory.InventoryId, instance.Revision,
                "chest_slot", new[] { BodyPartId.Parse("Torso") }, NewUserId(), Clock.GetUtcNow(), NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            ContentDefinitionRecord target = PublishArmor("Target", slot: "head_slot");
            ItemDefinitionMigrationPreview preview = BuildRealPreview(source, target);

            long before = CountRows("SELECT COUNT(*) FROM DomainEvents;");

            Result<ItemDefinitionMigrationApplyResult> firstAttempt = _inventory.ApplyItemDefinitionMigration(
                _campaign, new ItemDefinitionMigrationTransition(preview, NewCommandId()), NewUserId(), actorIsMainGm: true, Corr);
            Result<ItemDefinitionMigrationApplyResult> secondAttempt = _inventory.ApplyItemDefinitionMigration(
                _campaign, new ItemDefinitionMigrationTransition(preview, NewCommandId()), NewUserId(), actorIsMainGm: true, Corr);

            Assert.That(firstAttempt.IsFailure, Is.True);
            Assert.That(secondAttempt.IsFailure, Is.True);
            long after = CountRows("SELECT COUNT(*) FROM DomainEvents;");
            Assert.That(after, Is.EqualTo(before), "repeated rejected apply attempts must append zero DomainEvents rows, not merely one");
        }

        [Test] // TC-INVENTORY-191
        public void SourceDefinition_RemainsLoadable_AfterTargetIsPublishedAndMigrationApplied()
        {
            // ADR-027 section 16 item 2: archived/Published definitions stay
            // loadable for history/migration -- publishing a successor and
            // applying a migration must not remove or hide the source row.
            ContentDefinitionRecord source = PublishArmor("Source", slot: "chest_slot");
            InventoryRecord inventory = CreateInventory();
            CreateArmorInstance(source, inventory);
            ContentDefinitionRecord target = PublishArmor("Target", slot: "chest_slot");
            ItemDefinitionMigrationPreview preview = BuildRealPreview(source, target);
            Assert.That(ItemDefinitionMigrationRules.ComputeBlockingIssues(preview, target, Array.Empty<EquippedEntryRecord>()).HasBlockingIssues, Is.False);

            Result<ItemDefinitionMigrationApplyResult> applied = _inventory.ApplyItemDefinitionMigration(
                _campaign, new ItemDefinitionMigrationTransition(preview, NewCommandId()), NewUserId(), actorIsMainGm: true, Corr);
            Assert.That(applied.IsSuccess, Is.True, applied.IsFailure ? applied.Error.Code.ToString() : string.Empty);

            Result<ContentDefinitionRecord> reread = _catalog.GetContentDefinition(_campaign, source.ContentDefinitionId, Corr);
            Assert.That(reread.IsSuccess, Is.True, "the source definition must remain loadable after a migration moves runtime items off of it");
            Assert.That(reread.Value.Status, Is.EqualTo(ContentDefinitionStatus.Published));
        }

        [Test] // TC-INVENTORY-192
        public void MigrationBlock_ResolvesAStackCapacityBlockingIssue_ThroughTheExistingSplitOperation()
        {
            // Proves the composed path is not Armor/equipment-slot specific:
            // a stackable Item whose new version reduces max stack size below
            // one existing stack's own quantity blocks (ODY-S05-402's other
            // supported case), resolved here through the already-accepted
            // Split operation (ODY-S05-205/207), not equipment.
            ContentDefinitionRecord source = PublishStackableItem("Source", maxStackSize: 10);
            InventoryRecord inventory = CreateInventory();
            ItemStackRecord stack = CreateStack(source, inventory, quantity: 8);
            ContentDefinitionRecord target = PublishStackableItem("Target", maxStackSize: 5);

            ItemDefinitionMigrationPreview blockedPreview = BuildRealPreview(source, target);
            ItemDefinitionMigrationIncompatibilityReport blockedReport = ItemDefinitionMigrationRules.ComputeBlockingIssues(blockedPreview, target, Array.Empty<EquippedEntryRecord>());
            Assert.That(blockedReport.HasBlockingIssues, Is.True);
            Assert.That(blockedReport.Issues[0].IssueCode, Is.EqualTo(ItemDefinitionMigrationBlockingIssueCode.StackCapacityReducedBelowCurrentContent));

            Result<ItemDefinitionMigrationApplyResult> rejected = _inventory.ApplyItemDefinitionMigration(
                _campaign, new ItemDefinitionMigrationTransition(blockedPreview, NewCommandId()), NewUserId(), actorIsMainGm: true, Corr);
            Assert.That(rejected.IsFailure, Is.True);
            Assert.That(rejected.Error.Code, Is.EqualTo(ErrorCodes.InventoryMigrationBlocked));

            // Resolve by splitting 3 units off, leaving both resulting
            // stacks (5 and 3) at or under the new capacity of 5.
            Result<ItemStackRecord> split = InventoryStackOperationService.Split(
                _inventory, new SplitItemStackRequest(
                    _campaign, stack.ItemStackId, ItemStackId.NewId(Clock.GetUtcNow()), inventory.InventoryId, quantity: 3,
                    expectedSourceRevision: stack.Revision, expectedInventoryRevision: inventory.Revision, NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(split.IsSuccess, Is.True, split.IsFailure ? split.Error.Code.ToString() : string.Empty);

            ItemDefinitionMigrationPreview freshPreview = BuildRealPreview(source, target);
            Assert.That(freshPreview.AffectedStacks.Sum(group => group.Members.Count), Is.EqualTo(2), "both the remainder and the split-off part are still sourced from the definition being migrated");
            Assert.That(ItemDefinitionMigrationRules.ComputeBlockingIssues(freshPreview, target, Array.Empty<EquippedEntryRecord>()).HasBlockingIssues, Is.False);

            Result<ItemDefinitionMigrationApplyResult> applied = _inventory.ApplyItemDefinitionMigration(
                _campaign, new ItemDefinitionMigrationTransition(freshPreview, NewCommandId()), NewUserId(), actorIsMainGm: true, Corr);
            Assert.That(applied.IsSuccess, Is.True, applied.IsFailure ? applied.Error.Code.ToString() : string.Empty);
            Assert.That(applied.Value.UpdatedStackCount, Is.EqualTo(2));
        }

        // ---- helpers (private methods, no new production type) ----

        private ItemDefinitionMigrationPreview BuildRealPreview(ContentDefinitionRecord source, ContentDefinitionRecord target)
        {
            ContentDefinitionRecord freshSource = _catalog.GetContentDefinition(_campaign, source.ContentDefinitionId, Corr).Value;
            ContentDefinitionRecord freshTarget = _catalog.GetContentDefinition(_campaign, target.ContentDefinitionId, Corr).Value;
            var instances = _inventory.ListItemInstancesBySourceDefinitionId(_campaign, _campaign.CampaignId, source.ContentDefinitionId, Corr).Value;
            var stacks = _inventory.ListItemStacksBySourceDefinitionId(_campaign, _campaign.CampaignId, source.ContentDefinitionId, Corr).Value;
            var inventoryIds = instances.Select(i => i.InventoryId).Concat(stacks.Select(s => s.InventoryId)).Distinct();
            var inventories = inventoryIds.Select(id => _inventory.GetInventory(_campaign, id, Corr).Value).ToList();
            return ItemDefinitionMigrationRules.BuildPreview(freshSource, freshTarget, instances, stacks, inventories);
        }

        private CharacterRecord CreateInitializedCharacter()
        {
            Result<CharacterRecord> created = _characters.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Migration Integration Fixture Character"), NewCommandId(), Corr);
            Assert.That(created.IsSuccess, Is.True);
            Result<CharacterRecord> initialized = CharacterAdvancementService.InitializeAnatomyWithDefaults(_characters, _campaign, created.Value.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, created.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), Corr);
            Assert.That(initialized.IsSuccess, Is.True);
            return initialized.Value;
        }

        private InventoryRecord CreateInventory(CharacterId? ownerCharacterId = null)
        {
            UtcInstant now = Clock.GetUtcNow();
            var record = new InventoryRecord(InventoryId.NewId(now), _campaign.CampaignId, InventoryOwnerRef.ForCharacter(ownerCharacterId ?? CharacterId.NewId(now)), 1, now, now);
            Assert.That(_inventory.CreateInventory(_campaign, record, NewCommandId(), Corr).IsSuccess, Is.True);
            return record;
        }

        private ItemInstanceRecord CreateArmorInstance(ContentDefinitionRecord published, InventoryRecord inventory)
        {
            Result<ItemInstanceRecord> created = InventoryCreationService.CreateItemInstanceFromDefinition(_catalog, _inventory, Clock, new CreateItemInstanceFromDefinitionRequest(
                _campaign, ItemInstanceId.NewId(Clock.GetUtcNow()), inventory.InventoryId, inventory.OwnerRef,
                InventoryLocationRef.Contained(inventory.InventoryId, "main"), published.ContentDefinitionId, NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(created.IsSuccess, Is.True, created.IsFailure ? created.Error.Code.ToString() : string.Empty);
            return created.Value;
        }

        private ItemStackRecord CreateStack(ContentDefinitionRecord published, InventoryRecord inventory, long quantity)
        {
            Result<ItemStackRecord> created = InventoryCreationService.CreateItemStackFromDefinition(_catalog, _inventory, Clock, new CreateItemStackFromDefinitionRequest(
                _campaign, ItemStackId.NewId(Clock.GetUtcNow()), inventory.InventoryId, inventory.OwnerRef,
                InventoryLocationRef.Contained(inventory.InventoryId, "main"), published.ContentDefinitionId, ItemStackQuantity.Create(quantity), NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(created.IsSuccess, Is.True, created.IsFailure ? created.Error.Code.ToString() : string.Empty);
            return created.Value;
        }

        private ContentDefinitionRecord PublishArmor(string tag, string slot)
        {
            var item = new ItemDefinition(ItemCategory.Generic, isStackable: false, maxStackSize: null, weight: 3, hasDurability: false, maxDurability: null, hasCharges: false, maxCharges: null, Array.Empty<ContentDefinitionRef>(), Array.Empty<ContentDefinitionRef>());
            var armor = new ArmorDefinition(item, slot, new[] { BodyPartId.Parse("Torso") }, protection: 5);
            return PublishDefinition(ContentDefinitionType.Armor, "Migration Integration Fixture Armor " + tag, TypedDefinitionCodec.EncodeArmor(armor));
        }

        private ContentDefinitionRecord PublishStackableItem(string tag, long maxStackSize)
        {
            var item = new ItemDefinition(ItemCategory.Generic, isStackable: true, maxStackSize: maxStackSize, weight: 1, hasDurability: false, maxDurability: null, hasCharges: false, maxCharges: null, Array.Empty<ContentDefinitionRef>(), Array.Empty<ContentDefinitionRef>());
            return PublishDefinition(ContentDefinitionType.Item, "Migration Integration Fixture Item " + tag, TypedDefinitionCodec.EncodeItem(item));
        }

        private ContentDefinitionRecord PublishDefinition(ContentDefinitionType type, string name, string propertiesJson)
        {
            Result<ContentDefinitionRecord> draft = ContentCatalogAuthoringService.CreateDraftDefinition(_catalog, new CreateDraftDefinitionRequest(
                _campaign, type, name, "ODY-S05-404 integration fixture", NewUserId(), actorIsMainGm: true, NewCommandId(), Corr,
                rulesetCompatibility: new[] { ActiveRuleset }, propertiesJson: propertiesJson));
            Assert.That(draft.IsSuccess, Is.True, draft.IsFailure ? draft.Error.Code.ToString() : string.Empty);
            Result<ContentDefinitionRecord> published = ContentCatalogLifecycleService.PublishDefinition(_catalog, new PublishDefinitionRequest(
                _campaign, draft.Value.ContentDefinitionId, draft.Value.Revision, NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(published.IsSuccess, Is.True, published.IsFailure ? published.Error.Code.ToString() : string.Empty);
            return published.Value;
        }

        private (string SourceRef, string MechanicsSourceRef, string Payload) ReadItemInstanceSnapshotColumns(ItemInstanceId itemInstanceId)
        {
            using SqliteConnection connection = OpenRawConnection();
            using var select = connection.CreateCommand();
            select.CommandText = "SELECT SourceItemDefinitionRef, MechanicsSourceDefinitionRef, MechanicsPayload FROM ItemInstance WHERE ItemInstanceId = $id;";
            select.Parameters.AddWithValue("$id", itemInstanceId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            Assert.That(reader.Read(), Is.True);
            return (reader.GetString(0), reader.GetString(1), reader.GetString(2));
        }

        private long CountRows(string sql)
        {
            using SqliteConnection connection = OpenRawConnection();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return (long)command.ExecuteScalar()!;
        }

        private SqliteConnection OpenRawConnection()
        {
            var connection = new SqliteConnection("Data Source=" + Path.Combine(_campaignDir, "campaign.db"));
            connection.Open();
            return connection;
        }
    }
}
