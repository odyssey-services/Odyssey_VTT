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
    /// ODY-S05-203: SQLite-backed Application tests for creating runtime
    /// Inventory records from Published catalog definitions. No movement,
    /// split/merge, equipment, attack, ActiveEffect, migration, Unity/UI,
    /// or balanced content behavior is introduced here.
    /// </summary>
    public sealed class InventoryCreationServiceTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;
        private SqliteContentCatalogRepository _catalogRepository = null!;
        private SqliteInventoryRepository _inventoryRepository = null!;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s05-203-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> created = _campaignRepository.Create(new CreateCampaignRequest(_campaignDir, "Inventory Creation Test Campaign", "ruleset.core", "1.0.0", "0.1.0"), NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _catalogRepository = new SqliteContentCatalogRepository(Clock);
            _inventoryRepository = new SqliteInventoryRepository(Clock);
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaignRepository.Close(_campaign, TestCorrelationId); } catch (IOException) { }
            try { if (Directory.Exists(_campaignDir)) Directory.Delete(_campaignDir, recursive: true); } catch (IOException) { }
        }

        [Test]
        public void CreateItemInstanceFromDefinition_ByMainGm_OnPublishedItem_Succeeds()
        {
            InventoryRecord inventory = CreateInventory();
            ContentDefinitionRecord item = PublishDefinition(ContentDefinitionType.Item, EncodeItem(isStackable: false));

            Result<ItemInstanceRecord> created = InventoryCreationService.CreateItemInstanceFromDefinition(_catalogRepository, _inventoryRepository, Clock, InstanceRequest(inventory, item));

            Assert.That(created.IsSuccess, Is.True);
            Assert.That(created.Value.InventoryId, Is.EqualTo(inventory.InventoryId));
            Assert.That(created.Value.SourceItemDefinitionRef, Is.EqualTo(new ContentDefinitionRef(item.ContentDefinitionId, item.Version)));
        }

        [Test]
        public void CreateItemInstanceFromDefinition_ByMainGm_OnPublishedWeapon_Succeeds()
        {
            InventoryRecord inventory = CreateInventory();
            ContentDefinitionRecord weapon = PublishDefinition(ContentDefinitionType.Weapon, EncodeWeapon());

            Result<ItemInstanceRecord> created = InventoryCreationService.CreateItemInstanceFromDefinition(_catalogRepository, _inventoryRepository, Clock, InstanceRequest(inventory, weapon));

            Assert.That(created.IsSuccess, Is.True);
            Assert.That(created.Value.MechanicsSnapshot.ContentType, Is.EqualTo(ContentDefinitionType.Weapon));
        }

        [Test]
        public void CreateItemStackFromDefinition_ByMainGm_OnPublishedAmmo_Succeeds()
        {
            InventoryRecord inventory = CreateInventory();
            ContentDefinitionRecord ammo = PublishDefinition(ContentDefinitionType.Ammo, EncodeAmmo());

            Result<ItemStackRecord> created = InventoryCreationService.CreateItemStackFromDefinition(_catalogRepository, _inventoryRepository, Clock, StackRequest(inventory, ammo, quantity: 7));

            Assert.That(created.IsSuccess, Is.True);
            Assert.That(created.Value.Quantity, Is.EqualTo(ItemStackQuantity.Create(7)));
            Assert.That(created.Value.MechanicsSnapshot.ContentType, Is.EqualTo(ContentDefinitionType.Ammo));
        }

        [Test]
        public void CreatedRuntimeRecord_PinsExactPublishedDefinitionRef()
        {
            InventoryRecord inventory = CreateInventory();
            ContentDefinitionRecord item = PublishDefinition(ContentDefinitionType.Item, EncodeItem(isStackable: false));

            Result<ItemInstanceRecord> created = InventoryCreationService.CreateItemInstanceFromDefinition(_catalogRepository, _inventoryRepository, Clock, InstanceRequest(inventory, item));

            Assert.That(created.IsSuccess, Is.True);
            Assert.That(created.Value.SourceItemDefinitionRef.DefinitionId, Is.EqualTo(item.ContentDefinitionId));
            Assert.That(created.Value.SourceItemDefinitionRef.Version, Is.EqualTo(item.Version));
            Assert.That(created.Value.MechanicsSnapshot.SourceDefinitionRef, Is.EqualTo(created.Value.SourceItemDefinitionRef));
            Assert.That(created.Value.MechanicsSnapshot.DefinitionSnapshotVersion, Is.EqualTo(item.Version));
        }

        [Test]
        public void CreatedRuntimeRecord_CopiesPublishedPropertiesJsonIntoMechanicsSnapshot()
        {
            InventoryRecord inventory = CreateInventory();
            string propertiesJson = EncodeItem(isStackable: false);
            ContentDefinitionRecord item = PublishDefinition(ContentDefinitionType.Item, propertiesJson);

            Result<ItemInstanceRecord> created = InventoryCreationService.CreateItemInstanceFromDefinition(_catalogRepository, _inventoryRepository, Clock, InstanceRequest(inventory, item));

            Assert.That(created.IsSuccess, Is.True);
            Assert.That(created.Value.MechanicsSnapshot.Payload, Is.EqualTo(propertiesJson));
        }

        [Test]
        public void CreateFromDefinition_OnDraftDefinition_IsRejectedWithoutRuntimeRow()
        {
            InventoryRecord inventory = CreateInventory();
            ContentDefinitionRecord draft = CreateDraft(ContentDefinitionType.Item, EncodeItem(isStackable: false));

            Result<ItemInstanceRecord> created = InventoryCreationService.CreateItemInstanceFromDefinition(_catalogRepository, _inventoryRepository, Clock, InstanceRequest(inventory, draft));

            Assert.That(created.IsFailure, Is.True);
            Assert.That(created.Error.Code, Is.EqualTo(ErrorCodes.InventoryCreateDefinitionNotPublished));
            Assert.That(CountRows("ItemInstance"), Is.EqualTo(0));
        }

        [Test]
        public void CreateFromDefinition_OnArchivedDefinition_IsRejectedWithoutRuntimeRow()
        {
            InventoryRecord inventory = CreateInventory();
            ContentDefinitionRecord published = PublishDefinition(ContentDefinitionType.Item, EncodeItem(isStackable: false));
            Result<ContentDefinitionRecord> archived = ContentCatalogLifecycleService.ArchiveDefinition(_catalogRepository, new ArchiveDefinitionRequest(_campaign, published.ContentDefinitionId, "retired", actorIsMainGm: true, NewCommandId(), TestCorrelationId));
            Assert.That(archived.IsSuccess, Is.True);

            Result<ItemInstanceRecord> created = InventoryCreationService.CreateItemInstanceFromDefinition(_catalogRepository, _inventoryRepository, Clock, InstanceRequest(inventory, archived.Value));

            Assert.That(created.IsFailure, Is.True);
            Assert.That(created.Error.Code, Is.EqualTo(ErrorCodes.InventoryCreateDefinitionNotPublished));
            Assert.That(CountRows("ItemInstance"), Is.EqualTo(0));
        }

        [Test]
        public void CreateFromDefinition_OnMissingDefinition_IsRejected()
        {
            InventoryRecord inventory = CreateInventory();
            ContentDefinitionId missingId = ContentDefinitionId.NewId(Clock.GetUtcNow());

            Result<ItemInstanceRecord> created = InventoryCreationService.CreateItemInstanceFromDefinition(_catalogRepository, _inventoryRepository, Clock, InstanceRequest(inventory, missingId));

            Assert.That(created.IsFailure, Is.True);
            Assert.That(created.Error.Code, Is.EqualTo(ErrorCodes.PersistenceContentDefinitionNotFound));
            Assert.That(CountRows("ItemInstance"), Is.EqualTo(0));
        }

        [Test]
        public void CreateFromDefinition_OnWrongType_IsRejectedForInstanceAndStack()
        {
            InventoryRecord inventory = CreateInventory();
            ContentDefinitionRecord effect = PublishDefinition(ContentDefinitionType.Effect, EncodeEffect());

            Result<ItemInstanceRecord> instance = InventoryCreationService.CreateItemInstanceFromDefinition(_catalogRepository, _inventoryRepository, Clock, InstanceRequest(inventory, effect));
            Result<ItemStackRecord> stack = InventoryCreationService.CreateItemStackFromDefinition(_catalogRepository, _inventoryRepository, Clock, StackRequest(inventory, effect));

            Assert.That(instance.IsFailure, Is.True);
            Assert.That(instance.Error.Code, Is.EqualTo(ErrorCodes.InventoryCreateDefinitionTypeUnsupported));
            Assert.That(stack.IsFailure, Is.True);
            Assert.That(stack.Error.Code, Is.EqualTo(ErrorCodes.InventoryCreateDefinitionTypeUnsupported));
            Assert.That(CountRows("ItemInstance"), Is.EqualTo(0));
            Assert.That(CountRows("ItemStack"), Is.EqualTo(0));
        }

        [Test]
        public void CreateFromDefinition_WhenCatalogValidationFails_IsRejectedWithoutRuntimeRow()
        {
            InventoryRecord inventory = CreateInventory();
            ContentDefinitionRecord draft = CreateDraft(ContentDefinitionType.Item, EncodeItem(isStackable: false), rulesetCompatibility: new[] { "other.ruleset@9.9.9" });
            MarkPublishedDirectly(draft.ContentDefinitionId, version: 1);

            Result<ItemInstanceRecord> created = InventoryCreationService.CreateItemInstanceFromDefinition(_catalogRepository, _inventoryRepository, Clock, InstanceRequest(inventory, draft));

            Assert.That(created.IsFailure, Is.True);
            Assert.That(created.Error.Code, Is.EqualTo(ErrorCodes.InventoryCreateDefinitionValidationFailed));
            Assert.That(CountRows("ItemInstance"), Is.EqualTo(0));
        }

        [Test]
        public void CreateFromDefinition_ByNonMainGm_IsRejectedBeforeMutation()
        {
            InventoryRecord inventory = CreateInventory();
            ContentDefinitionRecord item = PublishDefinition(ContentDefinitionType.Item, EncodeItem(isStackable: false));

            Result<ItemInstanceRecord> created = InventoryCreationService.CreateItemInstanceFromDefinition(_catalogRepository, _inventoryRepository, Clock, InstanceRequest(inventory, item, actorIsMainGm: false));

            Assert.That(created.IsFailure, Is.True);
            Assert.That(created.Error.Code, Is.EqualTo(ErrorCodes.InventoryCreateDenied));
            Assert.That(CountRows("ItemInstance"), Is.EqualTo(0));
        }

        [Test]
        public void CreateFromDefinition_ReplayWithSameCommandId_ReturnsStoredRecordWithoutDuplicate()
        {
            InventoryRecord inventory = CreateInventory();
            ContentDefinitionRecord item = PublishDefinition(ContentDefinitionType.Item, EncodeItem(isStackable: false));
            CommandId commandId = NewCommandId();
            ItemInstanceId itemInstanceId = ItemInstanceId.NewId(Clock.GetUtcNow());
            CreateItemInstanceFromDefinitionRequest request = InstanceRequest(inventory, item, commandId: commandId, itemInstanceId: itemInstanceId);

            Result<ItemInstanceRecord> first = InventoryCreationService.CreateItemInstanceFromDefinition(_catalogRepository, _inventoryRepository, Clock, request);
            Result<ItemInstanceRecord> replay = InventoryCreationService.CreateItemInstanceFromDefinition(_catalogRepository, _inventoryRepository, Clock, request);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.ItemInstanceId, Is.EqualTo(first.Value.ItemInstanceId));
            Assert.That(CountRows("ItemInstance"), Is.EqualTo(1));
        }

        [Test]
        public void CreateFromDefinition_ReusedCommandIdForDifferentTarget_IsRejectedByInventoryLedger()
        {
            InventoryRecord inventory = CreateInventory();
            ContentDefinitionRecord item = PublishDefinition(ContentDefinitionType.Item, EncodeItem(isStackable: false));
            CommandId commandId = NewCommandId();

            Result<ItemInstanceRecord> first = InventoryCreationService.CreateItemInstanceFromDefinition(_catalogRepository, _inventoryRepository, Clock, InstanceRequest(inventory, item, commandId: commandId));
            Result<ItemInstanceRecord> second = InventoryCreationService.CreateItemInstanceFromDefinition(_catalogRepository, _inventoryRepository, Clock, InstanceRequest(inventory, item, commandId: commandId));

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(second.IsFailure, Is.True);
            Assert.That(second.Error.Code, Is.EqualTo(ErrorCodes.CommandIdentityMismatch));
            Assert.That(CountRows("ItemInstance"), Is.EqualTo(1));
        }

        [Test]
        public void CreateItemInstanceFromDefinition_ReplayAfterSourceArchived_ReturnsStoredRecordWithoutDuplicate()
        {
            InventoryRecord inventory = CreateInventory();
            ContentDefinitionRecord published = PublishDefinition(ContentDefinitionType.Item, EncodeItem(isStackable: false));
            CreateItemInstanceFromDefinitionRequest request = InstanceRequest(inventory, published, commandId: NewCommandId(), itemInstanceId: ItemInstanceId.NewId(Clock.GetUtcNow()));
            Result<ItemInstanceRecord> first = InventoryCreationService.CreateItemInstanceFromDefinition(_catalogRepository, _inventoryRepository, Clock, request);
            Result<ContentDefinitionRecord> archived = ContentCatalogLifecycleService.ArchiveDefinition(_catalogRepository, new ArchiveDefinitionRequest(_campaign, published.ContentDefinitionId, "retired", actorIsMainGm: true, NewCommandId(), TestCorrelationId));
            Result<ItemInstanceRecord> replay = InventoryCreationService.CreateItemInstanceFromDefinition(_catalogRepository, _inventoryRepository, Clock, request);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(archived.IsSuccess, Is.True);
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.ItemInstanceId, Is.EqualTo(first.Value.ItemInstanceId));
            Assert.That(replay.Value.InventoryId, Is.EqualTo(first.Value.InventoryId));
            Assert.That(replay.Value.CampaignId, Is.EqualTo(first.Value.CampaignId));
            Assert.That(replay.Value.SourceItemDefinitionRef, Is.EqualTo(first.Value.SourceItemDefinitionRef));
            Assert.That(replay.Value.MechanicsSnapshot.SourceDefinitionRef.DefinitionId, Is.EqualTo(first.Value.MechanicsSnapshot.SourceDefinitionRef.DefinitionId));
            Assert.That(replay.Value.MechanicsSnapshot.DefinitionSnapshotVersion, Is.EqualTo(first.Value.MechanicsSnapshot.DefinitionSnapshotVersion));
            Assert.That(replay.Value.MechanicsSnapshot.ContentType, Is.EqualTo(first.Value.MechanicsSnapshot.ContentType));
            Assert.That(CountRows("ItemInstance"), Is.EqualTo(1));
        }

        [Test]
        public void CreateItemStackFromDefinition_ReplayAfterSourceArchived_ReturnsStoredRecordWithoutDuplicate()
        {
            InventoryRecord inventory = CreateInventory();
            ContentDefinitionRecord published = PublishDefinition(ContentDefinitionType.Ammo, EncodeAmmo());
            CreateItemStackFromDefinitionRequest request = StackRequest(inventory, published, commandId: NewCommandId(), itemStackId: ItemStackId.NewId(Clock.GetUtcNow()));
            Result<ItemStackRecord> first = InventoryCreationService.CreateItemStackFromDefinition(_catalogRepository, _inventoryRepository, Clock, request);
            Result<ContentDefinitionRecord> archived = ContentCatalogLifecycleService.ArchiveDefinition(_catalogRepository, new ArchiveDefinitionRequest(_campaign, published.ContentDefinitionId, "retired", actorIsMainGm: true, NewCommandId(), TestCorrelationId));
            Result<ItemStackRecord> replay = InventoryCreationService.CreateItemStackFromDefinition(_catalogRepository, _inventoryRepository, Clock, request);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(archived.IsSuccess, Is.True);
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.ItemStackId, Is.EqualTo(first.Value.ItemStackId));
            Assert.That(replay.Value.InventoryId, Is.EqualTo(first.Value.InventoryId));
            Assert.That(replay.Value.CampaignId, Is.EqualTo(first.Value.CampaignId));
            Assert.That(replay.Value.SourceItemDefinitionRef, Is.EqualTo(first.Value.SourceItemDefinitionRef));
            Assert.That(replay.Value.MechanicsSnapshot.SourceDefinitionRef.DefinitionId, Is.EqualTo(first.Value.MechanicsSnapshot.SourceDefinitionRef.DefinitionId));
            Assert.That(replay.Value.MechanicsSnapshot.DefinitionSnapshotVersion, Is.EqualTo(first.Value.MechanicsSnapshot.DefinitionSnapshotVersion));
            Assert.That(replay.Value.MechanicsSnapshot.ContentType, Is.EqualTo(first.Value.MechanicsSnapshot.ContentType));
            Assert.That(replay.Value.Quantity, Is.EqualTo(first.Value.Quantity));
            Assert.That(CountRows("ItemStack"), Is.EqualTo(1));
        }

        [Test]
        public void CreateItemInstanceFromDefinition_ReusedCommandIdForDifferentTargetAfterSourceArchived_IsRejected()
        {
            InventoryRecord inventory = CreateInventory();
            ContentDefinitionRecord published = PublishDefinition(ContentDefinitionType.Item, EncodeItem(isStackable: false));
            CommandId commandId = NewCommandId();
            Result<ItemInstanceRecord> first = InventoryCreationService.CreateItemInstanceFromDefinition(_catalogRepository, _inventoryRepository, Clock, InstanceRequest(inventory, published, commandId: commandId));
            Result<ContentDefinitionRecord> archived = ContentCatalogLifecycleService.ArchiveDefinition(_catalogRepository, new ArchiveDefinitionRequest(_campaign, published.ContentDefinitionId, "retired", actorIsMainGm: true, NewCommandId(), TestCorrelationId));
            Result<ItemInstanceRecord> mismatch = InventoryCreationService.CreateItemInstanceFromDefinition(_catalogRepository, _inventoryRepository, Clock, InstanceRequest(inventory, published, commandId: commandId));

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(archived.IsSuccess, Is.True);
            Assert.That(mismatch.IsFailure, Is.True);
            Assert.That(mismatch.Error.Code, Is.EqualTo(ErrorCodes.CommandIdentityMismatch));
            Assert.That(CountRows("ItemInstance"), Is.EqualTo(1));
        }

        [Test]
        public void CreateItemStackFromDefinition_OnNonStackableItem_IsRejected()
        {
            InventoryRecord inventory = CreateInventory();
            ContentDefinitionRecord item = PublishDefinition(ContentDefinitionType.Item, EncodeItem(isStackable: false));

            Result<ItemStackRecord> created = InventoryCreationService.CreateItemStackFromDefinition(_catalogRepository, _inventoryRepository, Clock, StackRequest(inventory, item));

            Assert.That(created.IsFailure, Is.True);
            Assert.That(created.Error.Code, Is.EqualTo(ErrorCodes.InventoryCreateDefinitionTypeUnsupported));
            Assert.That(CountRows("ItemStack"), Is.EqualTo(0));
        }

        [Test]
        public void CreateItemStackFromDefinition_WhenQuantityExceedsMaxStack_IsRejected()
        {
            InventoryRecord inventory = CreateInventory();
            ContentDefinitionRecord item = PublishDefinition(ContentDefinitionType.Item, EncodeItem(isStackable: true, maxStackSize: 2));

            Result<ItemStackRecord> created = InventoryCreationService.CreateItemStackFromDefinition(_catalogRepository, _inventoryRepository, Clock, StackRequest(inventory, item, quantity: 3));

            Assert.That(created.IsFailure, Is.True);
            Assert.That(created.Error.Code, Is.EqualTo(ErrorCodes.InventoryCreateDefinitionTypeUnsupported));
            Assert.That(CountRows("ItemStack"), Is.EqualTo(0));
        }

        [Test]
        public void InventoryCreationScope_DoesNotIntroduceLaterInventoryCapabilities()
        {
            CreateInventory();

            using SqliteConnection connection = OpenRawConnection();
            using (var select = connection.CreateCommand())
            {
                select.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
                using SqliteDataReader reader = select.ExecuteReader();
                var tableNames = new List<string>();
                while (reader.Read()) tableNames.Add(reader.GetString(0));
                // ODY-S05-302 legitimately owns EquipmentCommandLedger (Equipment
                // persistence, not command semantics); ODY-S05-403 legitimately owns
                // ItemDefinitionMigrationCommandLedger (migration apply idempotency,
                // not blocking-rule computation); every other forbidden fragment
                // still applies to every table name.
                string[] allowedMigrationTables = { "EquipmentCommandLedger", "ItemDefinitionMigrationCommandLedger" };
                AssertForbiddenFragments(tableNames.Where(name => !allowedMigrationTables.Contains(name)));
            }

            // ODY-S05-303 legitimately owns the Equip transition's orchestrator
            // types (EquipmentService/EquipmentFailures); ODY-S05-401 legitimately
            // owns the migration preview type/builder (preview construction only --
            // no blocking-incompatibility computation, backup call, or apply logic);
            // every other forbidden fragment still applies to every other type name
            // in this namespace.
            string[] allowedEquipmentTypes = { "EquipmentService", "EquipmentFailures" };
            string[] allowedItemDefinitionMigrationTypes =
            {
                "ItemDefinitionMigrationPreview",
                "ItemDefinitionMigrationAffectedInstance",
                "ItemDefinitionMigrationAffectedStackMember",
                "ItemDefinitionMigrationAffectedStackGroup",
                "ItemDefinitionMigrationInventoryRevision",
                "ItemDefinitionMigrationRules",
                "ItemDefinitionMigrationBlockingIssue",
                "ItemDefinitionMigrationBlockingIssueCode",
                "ItemDefinitionMigrationIncompatibilityReport",
                "ItemDefinitionMigrationTransition",
                "ItemDefinitionMigrationApplyResult"
            };
            IEnumerable<string> inventoryTypeNames = typeof(InventoryCreationService).Assembly.GetTypes()
                .Where(t => string.Equals(t.Namespace, "Odyssey.Application.Inventory", StringComparison.Ordinal))
                .Select(t => t.Name)
                .Where(name => !allowedEquipmentTypes.Contains(name))
                .Where(name => !allowedItemDefinitionMigrationTypes.Contains(name));
            AssertForbiddenFragments(inventoryTypeNames);
        }

        private InventoryRecord CreateInventory()
        {
            UtcInstant now = Clock.GetUtcNow();
            var record = new InventoryRecord(InventoryId.NewId(now), _campaign.CampaignId, InventoryOwnerRef.ForCharacter(CharacterId.NewId(now)), 1, now, now);
            Result<InventoryRecord> created = _inventoryRepository.CreateInventory(_campaign, record, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        private ContentDefinitionRecord CreateDraft(ContentDefinitionType type, string propertiesJson, IReadOnlyList<string>? rulesetCompatibility = null)
        {
            var request = new CreateDraftContentDefinitionRequest(_campaign, type, "Runtime Creation Fixture", "Test fixture.", NewUserId(), rulesetCompatibility, propertiesJson: propertiesJson);
            Result<ContentDefinitionRecord> created = _catalogRepository.CreateDraftContentDefinition(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        private ContentDefinitionRecord PublishDefinition(ContentDefinitionType type, string propertiesJson)
        {
            ContentDefinitionRecord draft = CreateDraft(type, propertiesJson);
            var request = new PublishDefinitionRequest(_campaign, draft.ContentDefinitionId, draft.Revision, NewUserId(), actorIsMainGm: true, NewCommandId(), TestCorrelationId);
            Result<ContentDefinitionRecord> published = ContentCatalogLifecycleService.PublishDefinition(_catalogRepository, request);
            Assert.That(published.IsSuccess, Is.True);
            return published.Value;
        }

        private CreateItemInstanceFromDefinitionRequest InstanceRequest(
            InventoryRecord inventory,
            ContentDefinitionRecord definition,
            bool actorIsMainGm = true,
            CommandId? commandId = null,
            ItemInstanceId? itemInstanceId = null)
            => InstanceRequest(inventory, definition.ContentDefinitionId, actorIsMainGm, commandId, itemInstanceId);

        private CreateItemInstanceFromDefinitionRequest InstanceRequest(
            InventoryRecord inventory,
            ContentDefinitionId definitionId,
            bool actorIsMainGm = true,
            CommandId? commandId = null,
            ItemInstanceId? itemInstanceId = null)
        {
            ItemInstanceId targetId = itemInstanceId ?? ItemInstanceId.NewId(Clock.GetUtcNow());
            return new CreateItemInstanceFromDefinitionRequest(
                _campaign,
                targetId,
                inventory.InventoryId,
                inventory.OwnerRef,
                InventoryLocationRef.Contained(inventory.InventoryId, "main"),
                definitionId,
                NewUserId(),
                actorIsMainGm,
                commandId ?? NewCommandId(),
                TestCorrelationId);
        }

        private CreateItemStackFromDefinitionRequest StackRequest(
            InventoryRecord inventory,
            ContentDefinitionRecord definition,
            long quantity = 4,
            bool actorIsMainGm = true,
            CommandId? commandId = null,
            ItemStackId? itemStackId = null)
        {
            ItemStackId targetId = itemStackId ?? ItemStackId.NewId(Clock.GetUtcNow());
            return new CreateItemStackFromDefinitionRequest(
                _campaign,
                targetId,
                inventory.InventoryId,
                inventory.OwnerRef,
                InventoryLocationRef.Contained(inventory.InventoryId, "main"),
                definition.ContentDefinitionId,
                ItemStackQuantity.Create(quantity),
                NewUserId(),
                actorIsMainGm,
                commandId ?? NewCommandId(),
                TestCorrelationId);
        }

        private static string EncodeItem(bool isStackable, long? maxStackSize = null)
        {
            var item = new ItemDefinition(ItemCategory.Generic, isStackable, maxStackSize, weight: 1, false, null, false, null, Array.Empty<ContentDefinitionRef>(), Array.Empty<ContentDefinitionRef>());
            return TypedDefinitionCodec.EncodeItem(item);
        }

        private static string EncodeWeapon()
        {
            var item = new ItemDefinition(ItemCategory.Generic, false, null, weight: 2, false, null, false, null, Array.Empty<ContentDefinitionRef>(), Array.Empty<ContentDefinitionRef>());
            var weapon = new WeaponDefinition(item, "1d6", range: 1, WeaponAttackMode.Melee, actionCost: 1, AmmoRequirement.None, Array.Empty<string>());
            return TypedDefinitionCodec.EncodeWeapon(weapon);
        }

        private static string EncodeAmmo()
        {
            var item = new ItemDefinition(ItemCategory.Generic, true, 50, weight: 1, false, null, false, null, Array.Empty<ContentDefinitionRef>(), Array.Empty<ContentDefinitionRef>());
            var ammo = new AmmoDefinition(item, new[] { "9mm" }, damageContribution: null, Array.Empty<ContentDefinitionRef>());
            return TypedDefinitionCodec.EncodeAmmo(ammo);
        }

        private static string EncodeEffect()
        {
            var targetRule = new ContentTargetRule(ContentTargetSource.ManualSelection, minimumCount: 0, maximumCount: 1, allowSelf: true);
            var effect = new EffectDefinition(targetRule, EffectDurationType.Instant, durationValue: null, EffectStackPolicy.IndependentInstances, mechanicsPayloadRef: null);
            return TypedDefinitionCodec.EncodeEffect(effect);
        }

        private void MarkPublishedDirectly(ContentDefinitionId definitionId, long version)
        {
            using SqliteConnection connection = OpenRawConnection();
            using var update = connection.CreateCommand();
            UtcInstant now = Clock.GetUtcNow();
            update.CommandText = "UPDATE ContentDefinition SET Status = $status, Version = $version, Revision = Revision + 1, PublishedByUserId = $publishedByUserId, PublishedAt = $publishedAt, UpdatedAt = $updatedAt WHERE ContentDefinitionId = $id;";
            update.Parameters.AddWithValue("$status", ContentDefinitionStatus.Published.ToString());
            update.Parameters.AddWithValue("$version", version);
            update.Parameters.AddWithValue("$publishedByUserId", NewUserId().ToString());
            update.Parameters.AddWithValue("$publishedAt", now.ToString());
            update.Parameters.AddWithValue("$updatedAt", now.ToString());
            update.Parameters.AddWithValue("$id", definitionId.ToString());
            update.ExecuteNonQuery();
        }

        private long CountRows(string tableName)
        {
            using SqliteConnection connection = OpenRawConnection();
            using var count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM " + tableName + ";";
            return (long)count.ExecuteScalar()!;
        }

        private SqliteConnection OpenRawConnection()
        {
            var connection = new SqliteConnection("Data Source=" + Path.Combine(_campaignDir, "campaign.db"));
            connection.Open();
            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA foreign_keys = ON;";
                pragma.ExecuteNonQuery();
            }

            return connection;
        }

        private static void AssertForbiddenFragments(IEnumerable<string> names)
        {
            // "Split"/"Merge" were forbidden here while ODY-S05-205 was still Proposed;
            // that task now legitimately owns stack split/merge, so they are no longer
            // out-of-scope fragments for the Inventory runtime surface.
            string[] forbidden = { "Transfer", "Equipment", "Attack", "ActiveEffect", "ItemDefinitionMigration" };
            foreach (string name in names)
            {
                foreach (string fragment in forbidden)
                {
                    Assert.That(name, Does.Not.Contain(fragment));
                }
            }
        }
    }
}
