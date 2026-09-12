using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S05-403: real-SQLite tests for
    /// <see cref="SqliteInventoryRepository.ApplyItemDefinitionMigration"/>
    /// -- `ADR-027` section 10 steps 5-9 only. Covers: MainGM gate before any
    /// I/O, the mandatory pre-transaction backup and its failure path, the
    /// triple host-authoritative revision recheck, the host-authoritative
    /// re-run of `ODY-S05-402`'s `ComputeBlockingIssues` immediately before
    /// commit, atomic multi-row snapshot update (all-or-nothing), and
    /// `CommandId`-keyed idempotent replay. Does not re-implement or
    /// re-verify preview construction (`ODY-S05-401`) or blocking-rule
    /// computation (`ODY-S05-402`) themselves -- only that this method calls
    /// them host-authoritatively at the right points.
    /// </summary>
    public sealed class ItemDefinitionMigrationApplyTests
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
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s05-403-" + Guid.NewGuid().ToString("N"));
            _campaigns = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> created = _campaigns.Create(new CreateCampaignRequest(_campaignDir, "Migration Apply Test Campaign", "ruleset.core", "1.0.0", "0.1.0"), NewCommandId(), Corr);
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

        [Test] // TC-INVENTORY-179
        public void ApplyItemDefinitionMigration_Success_UpdatesEveryAffectedInstanceAndStack()
        {
            ContentDefinitionRecord source = PublishStackableItem("A");
            ContentDefinitionRecord target = PublishStackableItem("B");
            InventoryRecord inventory = CreateInventory();
            ItemInstanceRecord instance = CreateInstance(source, inventory);
            ItemStackRecord stack = CreateStack(source, inventory, quantity: 3);

            ItemDefinitionMigrationPreview preview = BuildRealPreview(source, target);
            Result<ItemDefinitionMigrationApplyResult> applied = Apply(preview, NewCommandId());

            Assert.That(applied.IsSuccess, Is.True, applied.IsFailure ? applied.Error.Code.ToString() : string.Empty);
            Assert.That(applied.Value.UpdatedInstanceCount, Is.EqualTo(1));
            Assert.That(applied.Value.UpdatedStackCount, Is.EqualTo(1));
            Assert.That(applied.Value.BackupId.IsValid, Is.True);

            ItemInstanceRecord rereadInstance = _inventory.GetItemInstance(_campaign, instance.ItemInstanceId, Corr).Value;
            Assert.That(rereadInstance.SourceItemDefinitionRef, Is.EqualTo(new ContentDefinitionRef(target.ContentDefinitionId, target.Version)));
            Assert.That(rereadInstance.MechanicsSnapshot.Payload, Is.EqualTo(target.PropertiesJson));
            Assert.That(rereadInstance.Revision, Is.EqualTo(instance.Revision + 1));

            ItemStackRecord rereadStack = _inventory.GetItemStack(_campaign, stack.ItemStackId, Corr).Value;
            Assert.That(rereadStack.SourceItemDefinitionRef, Is.EqualTo(new ContentDefinitionRef(target.ContentDefinitionId, target.Version)));
            Assert.That(rereadStack.MechanicsSnapshot.Payload, Is.EqualTo(target.PropertiesJson));
            Assert.That(rereadStack.Revision, Is.EqualTo(stack.Revision + 1));
            Assert.That(rereadStack.Quantity, Is.EqualTo(stack.Quantity), "migration must not alter quantity");

            Assert.That(BackupCount(), Is.EqualTo(1));
        }

        [Test] // TC-INVENTORY-180
        public void ApplyItemDefinitionMigration_NonMainGmActor_IsDeniedBeforeAnyIo()
        {
            ContentDefinitionRecord source = PublishArmor("A");
            ContentDefinitionRecord target = PublishArmor("B");
            InventoryRecord inventory = CreateInventory();
            ItemInstanceRecord instance = CreateInstance(source, inventory);
            ItemDefinitionMigrationPreview preview = BuildRealPreview(source, target);

            Result<ItemDefinitionMigrationApplyResult> applied = Apply(preview, NewCommandId(), actorIsMainGm: false);

            Assert.That(applied.IsFailure, Is.True);
            Assert.That(applied.Error.Code, Is.EqualTo(ErrorCodes.InventoryMigrationDenied));
            Assert.That(BackupCount(), Is.EqualTo(0), "a denied actor must never trigger a backup");
            ItemInstanceRecord unchanged = _inventory.GetItemInstance(_campaign, instance.ItemInstanceId, Corr).Value;
            Assert.That(unchanged.SourceItemDefinitionRef, Is.EqualTo(instance.SourceItemDefinitionRef));
            Assert.That(unchanged.Revision, Is.EqualTo(instance.Revision));
        }

        [Test] // TC-INVENTORY-181
        public void ApplyItemDefinitionMigration_StaleSourceDefinitionRevision_IsRejectedWithoutMutation()
        {
            ContentDefinitionRecord source = PublishArmor("A");
            ContentDefinitionRecord target = PublishArmor("B");
            InventoryRecord inventory = CreateInventory();
            ItemInstanceRecord instance = CreateInstance(source, inventory);
            ItemDefinitionMigrationPreview preview = BuildRealPreview(source, target);

            // Simulates a concurrent catalog-side change to the source definition's
            // own Revision between preview build and confirmation -- the same
            // "someone changed it meanwhile" simulation technique already used by
            // this test project's own SetInventoryRevisionDirectly precedent.
            BumpContentDefinitionRevisionDirectly(source.ContentDefinitionId);

            Result<ItemDefinitionMigrationApplyResult> applied = Apply(preview, NewCommandId());

            Assert.That(applied.IsFailure, Is.True);
            Assert.That(applied.Error.Code, Is.EqualTo(ErrorCodes.PersistenceContentDefinitionRevisionConflict));
            Assert.That(BackupCount(), Is.EqualTo(0), "a stale-revision rejection must happen before backup");
            ItemInstanceRecord unchanged = _inventory.GetItemInstance(_campaign, instance.ItemInstanceId, Corr).Value;
            Assert.That(unchanged.Revision, Is.EqualTo(instance.Revision));
        }

        [Test] // TC-INVENTORY-182
        public void ApplyItemDefinitionMigration_StaleAffectedInventoryRevision_IsRejectedWholesale()
        {
            ContentDefinitionRecord source = PublishArmor("A");
            ContentDefinitionRecord target = PublishArmor("B");
            InventoryRecord inventoryA = CreateInventory();
            InventoryRecord inventoryB = CreateInventory();
            ItemInstanceRecord instanceA = CreateInstance(source, inventoryA);
            ItemInstanceRecord instanceB = CreateInstance(source, inventoryB);
            ItemDefinitionMigrationPreview preview = BuildRealPreview(source, target);

            // ADR-027 section 10's own "Inventory.Revision only increases via
            // Move" fact means this is the realistic drift case: someone moved
            // an item into/out of inventoryB between preview and confirmation.
            BumpInventoryRevisionDirectly(inventoryB.InventoryId);

            Result<ItemDefinitionMigrationApplyResult> applied = Apply(preview, NewCommandId());

            Assert.That(applied.IsFailure, Is.True);
            Assert.That(applied.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryRevisionConflict));
            Assert.That(BackupCount(), Is.EqualTo(0));
            Assert.That(_inventory.GetItemInstance(_campaign, instanceA.ItemInstanceId, Corr).Value.Revision, Is.EqualTo(instanceA.Revision), "rejection must be wholesale, not partial -- instanceA in the unaffected inventory must not be updated either");
            Assert.That(_inventory.GetItemInstance(_campaign, instanceB.ItemInstanceId, Corr).Value.Revision, Is.EqualTo(instanceB.Revision));
        }

        [Test] // TC-INVENTORY-183
        public void ApplyItemDefinitionMigration_StaleAffectedItemRevision_IsRejectedWholesaleWithNoPartialWrite()
        {
            ContentDefinitionRecord source = PublishArmor("A");
            ContentDefinitionRecord target = PublishArmor("B");
            InventoryRecord inventory = CreateInventory();
            ItemInstanceRecord instanceA = CreateInstance(source, inventory);
            ItemInstanceRecord instanceB = CreateInstance(source, inventory);
            ItemDefinitionMigrationPreview preview = BuildRealPreview(source, target);

            BumpItemInstanceRevisionDirectly(instanceB.ItemInstanceId);

            Result<ItemDefinitionMigrationApplyResult> applied = Apply(preview, NewCommandId());

            Assert.That(applied.IsFailure, Is.True);
            Assert.That(applied.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryItemRevisionConflict));
            Assert.That(BackupCount(), Is.EqualTo(0));
            Assert.That(_inventory.GetItemInstance(_campaign, instanceA.ItemInstanceId, Corr).Value.SourceItemDefinitionRef, Is.EqualTo(instanceA.SourceItemDefinitionRef), "instanceA must not be partially migrated even though only instanceB's revision drifted");
        }

        [Test] // TC-INVENTORY-184
        public void ApplyItemDefinitionMigration_BlockingIssueAppearingAfterPreview_IsCaughtHostAuthoritatively()
        {
            ContentDefinitionRecord source = PublishArmor("A", slot: "torso");
            ContentDefinitionRecord target = PublishArmor("B", slot: "torso");
            CharacterRecord character = CreateInitializedCharacter();
            InventoryRecord inventory = CreateInventory(character.CharacterId);
            ItemInstanceRecord instance = CreateInstance(source, inventory);

            // Equip while the slot ("torso") still matches the target -- no
            // blocking issue exists yet, so the preview built now is clean.
            EquippedEntryRecord equipped = EquipReal(character, inventory, instance, "torso", BodyPartId.Parse("Torso"));
            ItemDefinitionMigrationPreview preview = BuildRealPreview(source, target);
            Assert.That(preview.AffectedInstances.Count, Is.EqualTo(1), "preview must see the item at its current (equipped) revision");

            // Between preview and confirmation, the slot is changed to one the
            // target no longer defines. ReplaceEquippedEntry touches only the
            // EquippedEntry row's own revision, not the ItemInstance's, so the
            // preview's own ItemInstance.ExpectedRevision is untouched -- this
            // is exactly the gap the host-authoritative ComputeBlockingIssues
            // re-run (not merely trusting the client-supplied preview) exists
            // to close.
            ReplaceEquipSlot(equipped, "head");

            Result<ItemDefinitionMigrationApplyResult> applied = Apply(preview, NewCommandId());

            Assert.That(applied.IsFailure, Is.True);
            Assert.That(applied.Error.Code, Is.EqualTo(ErrorCodes.InventoryMigrationBlocked));
            // ComputeBlockingIssues is host-authoritatively recomputed both before
            // the backup call and again inside the apply transaction (the second
            // run guards the narrower window while the backup itself is being
            // taken) -- here the issue already exists by the time Apply is
            // called, so the pre-backup check is what rejects it, and no backup
            // is taken for a migration that was never going to succeed.
            Assert.That(BackupCount(), Is.EqualTo(0));
            Assert.That(_inventory.GetItemInstance(_campaign, instance.ItemInstanceId, Corr).Value.SourceItemDefinitionRef, Is.EqualTo(instance.SourceItemDefinitionRef));
        }

        [Test] // TC-INVENTORY-185
        public void ApplyItemDefinitionMigration_ReplayWithSameCommandId_IsIdempotentWithNoSecondBackupOrWrite()
        {
            ContentDefinitionRecord source = PublishArmor("A");
            ContentDefinitionRecord target = PublishArmor("B");
            InventoryRecord inventory = CreateInventory();
            ItemInstanceRecord instance = CreateInstance(source, inventory);
            ItemDefinitionMigrationPreview preview = BuildRealPreview(source, target);
            CommandId commandId = NewCommandId();
            UserId actor = NewUserId();

            Result<ItemDefinitionMigrationApplyResult> first = _inventory.ApplyItemDefinitionMigration(_campaign, new ItemDefinitionMigrationTransition(preview, commandId), actor, actorIsMainGm: true, Corr);
            Assert.That(first.IsSuccess, Is.True, first.IsFailure ? first.Error.Code.ToString() : string.Empty);
            Assert.That(BackupCount(), Is.EqualTo(1));

            // A real replay is the same actor resubmitting the same CommandId --
            // a different actor reusing a CommandId is a distinct case (identity
            // mismatch), not idempotent replay.
            Result<ItemDefinitionMigrationApplyResult> replay = _inventory.ApplyItemDefinitionMigration(_campaign, new ItemDefinitionMigrationTransition(preview, commandId), actor, actorIsMainGm: true, Corr);

            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.BackupId, Is.EqualTo(first.Value.BackupId));
            Assert.That(replay.Value.UpdatedInstanceCount, Is.EqualTo(first.Value.UpdatedInstanceCount));
            Assert.That(BackupCount(), Is.EqualTo(1), "a replay must not create a second backup");
            Assert.That(_inventory.GetItemInstance(_campaign, instance.ItemInstanceId, Corr).Value.Revision, Is.EqualTo(instance.Revision + 1), "a replay must not re-apply the write a second time");
        }

        [Test] // TC-INVENTORY-186
        public void ApplyItemDefinitionMigration_BackupFailure_AbortsBeforeAnyTransaction()
        {
            ContentDefinitionRecord source = PublishArmor("A");
            ContentDefinitionRecord target = PublishArmor("B");
            InventoryRecord inventory = CreateInventory();
            ItemInstanceRecord instance = CreateInstance(source, inventory);
            ItemDefinitionMigrationPreview preview = BuildRealPreview(source, target);
            var failingBackupInventory = new SqliteInventoryRepository(Clock, new AlwaysFailingBackupRepository());

            Result<ItemDefinitionMigrationApplyResult> applied = failingBackupInventory.ApplyItemDefinitionMigration(
                _campaign, new ItemDefinitionMigrationTransition(preview, NewCommandId()), NewUserId(), actorIsMainGm: true, Corr);

            Assert.That(applied.IsFailure, Is.True);
            Assert.That(BackupCount(), Is.EqualTo(0));
            Assert.That(_inventory.GetItemInstance(_campaign, instance.ItemInstanceId, Corr).Value.Revision, Is.EqualTo(instance.Revision), "the apply transaction must never open when backup fails");
        }

        [Test] // TC-INVENTORY-187
        public void ApplyItemDefinitionMigration_BlockingIssueIntroducedDuringBackupWindow_IsCaughtByTheInTransactionRecheckAndNothingIsWritten()
        {
            ContentDefinitionRecord source = PublishArmor("A", slot: "torso");
            ContentDefinitionRecord target = PublishArmor("B", slot: "torso");
            CharacterRecord character = CreateInitializedCharacter();
            InventoryRecord inventory = CreateInventory(character.CharacterId);
            ItemInstanceRecord blockedInstance = CreateInstance(source, inventory);
            ItemInstanceRecord cleanInstance = CreateInstance(source, inventory);

            // Equipped in a slot the target still defines -- both the pre-backup
            // and (absent interference) the in-transaction ComputeBlockingIssues
            // recheck would see a clean preview at this point.
            EquippedEntryRecord equipped = EquipReal(character, inventory, blockedInstance, "torso", BodyPartId.Parse("Torso"));
            ItemDefinitionMigrationPreview preview = BuildRealPreview(source, target);
            Assert.That(preview.AffectedInstances.Count, Is.EqualTo(2));

            // Simulates ADR-027 section 10 step 6's own concern directly: a
            // change landing in the narrow window while the mandatory backup is
            // being taken, strictly AFTER the pre-backup check already passed.
            // ReplaceEquippedEntry only touches the EquippedEntry row's own
            // revision, never blockedInstance's, so this could not be caught by
            // any revision CAS check -- only by re-running ComputeBlockingIssues
            // itself inside the transaction, which is exactly what this proves
            // is not decorative: it independently catches what the earlier,
            // already-passed pre-backup check could not have seen.
            var raceRepository = new SqliteInventoryRepository(Clock, new BackupThenMutate(new SqliteBackupRepository(Clock), () => ReplaceEquipSlot(equipped, "head")));

            Result<ItemDefinitionMigrationApplyResult> applied = raceRepository.ApplyItemDefinitionMigration(
                _campaign, new ItemDefinitionMigrationTransition(preview, NewCommandId()), NewUserId(), actorIsMainGm: true, Corr);

            Assert.That(applied.IsFailure, Is.True);
            Assert.That(applied.Error.Code, Is.EqualTo(ErrorCodes.InventoryMigrationBlocked));
            Assert.That(BackupCount(), Is.EqualTo(1), "the backup itself succeeded before the race was injected; only the migration write is rejected");
            // Neither affected instance was written -- the in-transaction
            // recheck runs before the per-row update loop, so a blocking issue
            // on ONE of two affected instances rejects the whole transaction
            // before either row (not just the blocked one) is touched.
            Assert.That(_inventory.GetItemInstance(_campaign, cleanInstance.ItemInstanceId, Corr).Value.SourceItemDefinitionRef, Is.EqualTo(cleanInstance.SourceItemDefinitionRef));
            Assert.That(_inventory.GetItemInstance(_campaign, cleanInstance.ItemInstanceId, Corr).Value.Revision, Is.EqualTo(cleanInstance.Revision));
            Assert.That(_inventory.GetItemInstance(_campaign, blockedInstance.ItemInstanceId, Corr).Value.SourceItemDefinitionRef, Is.EqualTo(blockedInstance.SourceItemDefinitionRef));
        }

        [Test] // TC-INVENTORY-188
        public void ApplyItemDefinitionMigration_TamperedPreviewRevision_IsRejectedAsASanityCheckDistinctFromRevisionCas()
        {
            ContentDefinitionRecord source = PublishArmor("A");
            ContentDefinitionRecord target = PublishArmor("B");
            InventoryRecord inventory = CreateInventory();
            CreateInstance(source, inventory);
            ItemDefinitionMigrationPreview real = BuildRealPreview(source, target);
            // A preview whose PreviewRevision does not match its own content --
            // simulating a client sending back a hand-edited or corrupted
            // preview object, not a real database CAS conflict. This checks the
            // sanity digest independently of the three live revision rechecks.
            var tampered = new ItemDefinitionMigrationPreview(real.SourceDefinitionRef, real.TargetDefinitionRef, real.ExpectedSourceDefinitionRevision,
                real.AffectedInstances, real.AffectedStacks, real.AffectedInventoryRevisions, "0000000000000000000000000000000000000000000000000000000000000000");

            Result<ItemDefinitionMigrationApplyResult> applied = Apply(tampered, NewCommandId());

            Assert.That(applied.IsFailure, Is.True);
            Assert.That(applied.Error.Code, Is.EqualTo(ErrorCodes.InventoryMigrationPreviewConflict));
            Assert.That(BackupCount(), Is.EqualTo(0));
        }

        // ---- helpers (private methods, no new production type) ----

        private Result<ItemDefinitionMigrationApplyResult> Apply(ItemDefinitionMigrationPreview preview, CommandId commandId, bool actorIsMainGm = true)
        {
            return _inventory.ApplyItemDefinitionMigration(_campaign, new ItemDefinitionMigrationTransition(preview, commandId), NewUserId(), actorIsMainGm, Corr);
        }

        private ItemDefinitionMigrationPreview BuildRealPreview(ContentDefinitionRecord source, ContentDefinitionRecord target)
        {
            ContentDefinitionRecord freshSource = _catalog.GetContentDefinition(_campaign, source.ContentDefinitionId, Corr).Value;
            ContentDefinitionRecord freshTarget = _catalog.GetContentDefinition(_campaign, target.ContentDefinitionId, Corr).Value;
            IReadOnlyList<ItemInstanceRecord> instances = _inventory.ListItemInstancesBySourceDefinitionId(_campaign, _campaign.CampaignId, source.ContentDefinitionId, Corr).Value;
            IReadOnlyList<ItemStackRecord> stacks = _inventory.ListItemStacksBySourceDefinitionId(_campaign, _campaign.CampaignId, source.ContentDefinitionId, Corr).Value;
            var inventoryIds = instances.Select(i => i.InventoryId).Concat(stacks.Select(s => s.InventoryId)).Distinct();
            var inventories = inventoryIds.Select(id => _inventory.GetInventory(_campaign, id, Corr).Value).ToList();
            return ItemDefinitionMigrationRules.BuildPreview(freshSource, freshTarget, instances, stacks, inventories);
        }

        private EquippedEntryRecord EquipReal(CharacterRecord character, InventoryRecord inventory, ItemInstanceRecord instance, string slot, BodyPartId bodyPart)
        {
            Result<EquippedEntryRecord> equipped = EquipmentService.Equip(_inventory, _characters, new EquipRequest(
                _campaign, InventoryItemRef.ForInstance(instance.ItemInstanceId), inventory.InventoryId, instance.Revision,
                slot, new[] { bodyPart }, NewUserId(), Clock.GetUtcNow(), NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(equipped.IsSuccess, Is.True, equipped.IsFailure ? equipped.Error.Code.ToString() : string.Empty);
            return equipped.Value;
        }

        private void ReplaceEquipSlot(EquippedEntryRecord equipped, string newSlot)
        {
            var replacement = new EquippedEntryRecord(equipped.CampaignId, new EquippedEntry(
                equipped.Entry.InventoryId, equipped.Entry.ItemRef, newSlot, equipped.Entry.BodyPartRefs,
                equipped.Entry.EquippedByUserId, equipped.Entry.EquippedAt, equipped.Entry.Revision));
            Result<EquippedEntryRecord> replaced = _inventory.ReplaceEquippedEntry(_campaign, replacement, equipped.Entry.Revision, NewCommandId(), Corr);
            Assert.That(replaced.IsSuccess, Is.True, replaced.IsFailure ? replaced.Error.Code.ToString() : string.Empty);
        }

        private ContentDefinitionRecord PublishStackableItem(string tag)
        {
            var item = new ItemDefinition(ItemCategory.Generic, isStackable: true, maxStackSize: 50, weight: 1, hasDurability: false, maxDurability: null, hasCharges: false, maxCharges: null, Array.Empty<ContentDefinitionRef>(), Array.Empty<ContentDefinitionRef>());
            string propertiesJson = TypedDefinitionCodec.EncodeItem(item);
            Result<ContentDefinitionRecord> draft = ContentCatalogAuthoringService.CreateDraftDefinition(_catalog, new CreateDraftDefinitionRequest(
                _campaign, ContentDefinitionType.Item, "Migration Apply Fixture " + tag, "ODY-S05-403 fixture", NewUserId(), actorIsMainGm: true, NewCommandId(), Corr,
                rulesetCompatibility: new[] { ActiveRuleset }, propertiesJson: propertiesJson));
            Assert.That(draft.IsSuccess, Is.True, draft.IsFailure ? draft.Error.Code.ToString() : string.Empty);
            Result<ContentDefinitionRecord> published = ContentCatalogLifecycleService.PublishDefinition(_catalog, new PublishDefinitionRequest(
                _campaign, draft.Value.ContentDefinitionId, draft.Value.Revision, NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(published.IsSuccess, Is.True, published.IsFailure ? published.Error.Code.ToString() : string.Empty);
            return published.Value;
        }

        private ContentDefinitionRecord PublishArmor(string tag, long protection = 5, string slot = "chest_slot")
        {
            var item = new ItemDefinition(ItemCategory.Generic, isStackable: false, maxStackSize: null, weight: 3, hasDurability: false, maxDurability: null, hasCharges: false, maxCharges: null, Array.Empty<ContentDefinitionRef>(), Array.Empty<ContentDefinitionRef>());
            var armor = new ArmorDefinition(item, slot, new[] { BodyPartId.Parse("Torso") }, protection);
            string propertiesJson = TypedDefinitionCodec.EncodeArmor(armor);
            Result<ContentDefinitionRecord> draft = ContentCatalogAuthoringService.CreateDraftDefinition(_catalog, new CreateDraftDefinitionRequest(
                _campaign, ContentDefinitionType.Armor, "Migration Apply Fixture " + tag, "ODY-S05-403 fixture", NewUserId(), actorIsMainGm: true, NewCommandId(), Corr,
                rulesetCompatibility: new[] { ActiveRuleset }, propertiesJson: propertiesJson));
            Assert.That(draft.IsSuccess, Is.True, draft.IsFailure ? draft.Error.Code.ToString() : string.Empty);
            Result<ContentDefinitionRecord> published = ContentCatalogLifecycleService.PublishDefinition(_catalog, new PublishDefinitionRequest(
                _campaign, draft.Value.ContentDefinitionId, draft.Value.Revision, NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(published.IsSuccess, Is.True, published.IsFailure ? published.Error.Code.ToString() : string.Empty);
            return published.Value;
        }

        private CharacterRecord CreateInitializedCharacter()
        {
            Result<CharacterRecord> created = _characters.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Migration Apply Fixture Character"), NewCommandId(), Corr);
            Assert.That(created.IsSuccess, Is.True);
            Result<CharacterRecord> initialized = _characters.InitializeCharacterAnatomy(_campaign, created.Value.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, created.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), Corr);
            Assert.That(initialized.IsSuccess, Is.True);
            return initialized.Value;
        }

        private InventoryRecord CreateInventory(CharacterId? ownerCharacterId = null)
        {
            UtcInstant now = Clock.GetUtcNow();
            var record = new InventoryRecord(InventoryId.NewId(now), _campaign.CampaignId, InventoryOwnerRef.ForCharacter(ownerCharacterId ?? CharacterId.NewId(now)), 1, now, now);
            Result<InventoryRecord> created = _inventory.CreateInventory(_campaign, record, NewCommandId(), Corr);
            Assert.That(created.IsSuccess, Is.True);
            return record;
        }

        private ItemInstanceRecord CreateInstance(ContentDefinitionRecord published, InventoryRecord inventory)
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

        private void BumpContentDefinitionRevisionDirectly(ContentDefinitionId definitionId)
        {
            using SqliteConnection connection = OpenRawConnection();
            using var update = connection.CreateCommand();
            update.CommandText = "UPDATE ContentDefinition SET Revision = Revision + 1 WHERE ContentDefinitionId = $id;";
            update.Parameters.AddWithValue("$id", definitionId.ToString());
            Assert.That(update.ExecuteNonQuery(), Is.EqualTo(1));
        }

        private void BumpInventoryRevisionDirectly(InventoryId inventoryId)
        {
            using SqliteConnection connection = OpenRawConnection();
            using var update = connection.CreateCommand();
            update.CommandText = "UPDATE Inventory SET Revision = Revision + 1 WHERE InventoryId = $id;";
            update.Parameters.AddWithValue("$id", inventoryId.ToString());
            Assert.That(update.ExecuteNonQuery(), Is.EqualTo(1));
        }

        private void BumpItemInstanceRevisionDirectly(ItemInstanceId itemInstanceId)
        {
            using SqliteConnection connection = OpenRawConnection();
            using var update = connection.CreateCommand();
            update.CommandText = "UPDATE ItemInstance SET Revision = Revision + 1 WHERE ItemInstanceId = $id;";
            update.Parameters.AddWithValue("$id", itemInstanceId.ToString());
            Assert.That(update.ExecuteNonQuery(), Is.EqualTo(1));
        }

        private long BackupCount()
        {
            using SqliteConnection connection = OpenRawConnection();
            using var count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM BackupRecords WHERE CampaignId = $campaignId;";
            count.Parameters.AddWithValue("$campaignId", _campaign.CampaignId.ToString());
            return (long)count.ExecuteScalar()!;
        }

        private SqliteConnection OpenRawConnection()
        {
            var connection = new SqliteConnection("Data Source=" + Path.Combine(_campaignDir, "campaign.db"));
            connection.Open();
            return connection;
        }

        /// <summary>Test-only fake proving the backup-failure abort path; never touches disk.</summary>
        private sealed class AlwaysFailingBackupRepository : IBackupRepository
        {
            public Result<BackupRecord> CreateBackup(CampaignHandle campaign, string reason, CorrelationId correlationId)
                => Result<BackupRecord>.Failure(PersistenceFailures.InventoryIoFailed(correlationId));

            public Result<IReadOnlyList<BackupRecord>> ListBackups(string campaignFolderPath, CorrelationId correlationId)
                => throw new InvalidOperationException("Not reached by this fixture.");

            public Result<string> RestoreBackup(string campaignFolderPath, BackupId backupId, string destinationParentDirectory, CorrelationId correlationId)
                => throw new InvalidOperationException("Not reached by this fixture.");
        }

        /// <summary>
        /// Test-only wrapper around a real <see cref="IBackupRepository"/> that
        /// injects a caller-supplied mutation immediately after a real, successful
        /// backup completes -- simulating a concurrent change landing in the
        /// narrow window between the pre-backup check and the in-transaction
        /// recheck (ADR-027 section 10 step 6), which no revision CAS check can
        /// see if the mutation does not touch the affected record's own revision.
        /// </summary>
        private sealed class BackupThenMutate : IBackupRepository
        {
            private readonly IBackupRepository _inner;
            private readonly Action _duringBackupWindow;

            public BackupThenMutate(IBackupRepository inner, Action duringBackupWindow)
            {
                _inner = inner;
                _duringBackupWindow = duringBackupWindow;
            }

            public Result<BackupRecord> CreateBackup(CampaignHandle campaign, string reason, CorrelationId correlationId)
            {
                Result<BackupRecord> result = _inner.CreateBackup(campaign, reason, correlationId);
                if (result.IsSuccess) _duringBackupWindow();
                return result;
            }

            public Result<IReadOnlyList<BackupRecord>> ListBackups(string campaignFolderPath, CorrelationId correlationId)
                => _inner.ListBackups(campaignFolderPath, correlationId);

            public Result<string> RestoreBackup(string campaignFolderPath, BackupId backupId, string destinationParentDirectory, CorrelationId correlationId)
                => _inner.RestoreBackup(campaignFolderPath, backupId, destinationParentDirectory, correlationId);
        }
    }
}
