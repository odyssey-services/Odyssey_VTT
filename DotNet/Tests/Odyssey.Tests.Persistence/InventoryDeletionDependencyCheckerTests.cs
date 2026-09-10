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
    /// <summary>
    /// ODY-S05-206: real SQLite tests for the runtime-reference dependency
    /// checks -- <c>InventoryCharacterDeletionDependencyChecker</c> on
    /// <c>DeleteCharacterPermanently</c>, and
    /// <c>InventoryContentDefinitionDependencyChecker</c> on
    /// <c>DeleteDraftDefinition</c> -- plus the two narrow
    /// <c>IInventoryRepository</c> existence primitives they use. No Equipment
    /// or ActiveEffect runtime is exercised (neither exists).
    /// </summary>
    public sealed class InventoryDeletionDependencyCheckerTests
    {
        private static readonly CorrelationId Corr = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaigns = null!;
        private SqliteCharacterRepository _characters = null!;
        private SqliteContentCatalogRepository _catalog = null!;
        private SqliteInventoryRepository _inventory = null!;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s05-206-" + Guid.NewGuid().ToString("N"));
            _campaigns = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> created = _campaigns.Create(new CreateCampaignRequest(_campaignDir, "Runtime Reference Dependency Test", "ruleset.core", "1.0.0", "0.1.0"), NewCommandId(), Corr);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _characters = new SqliteCharacterRepository(Clock);
            _catalog = new SqliteContentCatalogRepository(Clock);
            _inventory = new SqliteInventoryRepository(Clock);
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaigns.Close(_campaign, Corr); } catch (IOException) { }
            try { if (Directory.Exists(_campaignDir)) Directory.Delete(_campaignDir, true); } catch (IOException) { }
        }

        // ---------- HasAnyItemOwnedByCharacter ----------

        [Test] // TC-INVENTORY-079
        public void HasAnyItemOwnedByCharacter_FalseWhenNothingOwned_TrueForContainedItemInstance()
        {
            CharacterId characterId = CharacterId.NewId(Clock.GetUtcNow());
            InventoryRecord inv = CreateInventory(characterId);

            Assert.That(_inventory.HasAnyItemOwnedByCharacter(_campaign, _campaign.CampaignId, characterId, Corr).Value, Is.False);

            CreateItemInstance(inv, characterId, InventoryLocationRef.Contained(inv.InventoryId, "main"));

            Assert.That(_inventory.HasAnyItemOwnedByCharacter(_campaign, _campaign.CampaignId, characterId, Corr).Value, Is.True);
        }

        [Test] // TC-INVENTORY-080
        public void HasAnyItemOwnedByCharacter_TrueForContainedItemStack_FalseForADifferentCharacter()
        {
            CharacterId owner = CharacterId.NewId(Clock.GetUtcNow());
            CharacterId other = CharacterId.NewId(Clock.GetUtcNow());
            InventoryRecord inv = CreateInventory(owner);
            CreateItemStack(inv, owner, InventoryLocationRef.Contained(inv.InventoryId, "main"), "cdef_" + new string('a', 32) + "/1");

            Assert.That(_inventory.HasAnyItemOwnedByCharacter(_campaign, _campaign.CampaignId, owner, Corr).Value, Is.True);
            Assert.That(_inventory.HasAnyItemOwnedByCharacter(_campaign, _campaign.CampaignId, other, Corr).Value, Is.False);
        }

        [Test] // TC-INVENTORY-081
        public void HasAnyItemOwnedByCharacter_TrueForSceneDroppedButStillOwnedItem()
        {
            CharacterId characterId = CharacterId.NewId(Clock.GetUtcNow());
            InventoryRecord inv = CreateInventory(characterId);
            CreateItemStack(inv, characterId, InventoryLocationRef.SceneDropped(SceneId.NewId(Clock.GetUtcNow()), "floor"), "cdef_" + new string('b', 32) + "/2");

            Assert.That(_inventory.HasAnyItemOwnedByCharacter(_campaign, _campaign.CampaignId, characterId, Corr).Value, Is.True);
        }

        [Test] // TC-INVENTORY-082
        public void HasAnyItemOwnedByCharacter_RejectsMismatchedCampaignId()
        {
            Result<bool> result = _inventory.HasAnyItemOwnedByCharacter(_campaign, CampaignId.NewId(Clock.GetUtcNow()), CharacterId.NewId(Clock.GetUtcNow()), Corr);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryCampaignMismatch));
        }

        [Test] // TC-INVENTORY-083
        public void HasAnyItemOwnedByCharacter_OnUnreadableStore_ReturnsInventoryIoFailed()
        {
            CharacterId characterId = CharacterId.NewId(Clock.GetUtcNow());
            CreateInventory(characterId); // creates the inventory tables
            BreakItemInstanceOwnerColumn();

            Result<bool> result = _inventory.HasAnyItemOwnedByCharacter(_campaign, _campaign.CampaignId, characterId, Corr);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceInventoryIoFailed));
        }

        // ---------- DeleteCharacterPermanently + real checker ----------

        [Test] // TC-INVENTORY-084
        public void DeleteCharacterPermanently_BlockedWhenCharacterOwnsAContainedItem_NoStateChangeNoBackup()
        {
            SqliteCharacterRepository repo = CharactersWithInventoryChecker();
            CharacterRecord character = CreateCharacter(repo);
            InventoryRecord inv = CreateInventory(character.CharacterId);
            CreateItemStack(inv, character.CharacterId, InventoryLocationRef.Contained(inv.InventoryId, "main"), "cdef_" + new string('c', 32) + "/1");

            Result deleted = repo.DeleteCharacterPermanently(_campaign, character.CharacterId, "test cleanup", NewUserId(), actorIsMainGm: true, character.Revisions.LifecycleRevision, NewCommandId(), Corr);

            Assert.That(deleted.IsFailure, Is.True);
            Assert.That(deleted.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterDeletionHasDependent));
            Assert.That(repo.GetCharacter(_campaign, character.CharacterId, Corr).IsSuccess, Is.True);
            Assert.That(BackupCount(), Is.EqualTo(0), "a pre-check rejection must not create a campaign backup");
        }

        [Test] // TC-INVENTORY-085
        public void DeleteCharacterPermanently_BlockedWhenCharacterOwnsASceneDroppedItem()
        {
            SqliteCharacterRepository repo = CharactersWithInventoryChecker();
            CharacterRecord character = CreateCharacter(repo);
            InventoryRecord inv = CreateInventory(character.CharacterId);
            CreateItemInstance(inv, character.CharacterId, InventoryLocationRef.SceneDropped(SceneId.NewId(Clock.GetUtcNow()), "floor"));

            Result deleted = repo.DeleteCharacterPermanently(_campaign, character.CharacterId, "test cleanup", NewUserId(), actorIsMainGm: true, character.Revisions.LifecycleRevision, NewCommandId(), Corr);

            Assert.That(deleted.IsFailure, Is.True);
            Assert.That(deleted.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterDeletionHasDependent));
            Assert.That(repo.GetCharacter(_campaign, character.CharacterId, Corr).IsSuccess, Is.True);
        }

        [Test] // TC-INVENTORY-086
        public void DeleteCharacterPermanently_SucceedsWhenCharacterOwnsNothing()
        {
            SqliteCharacterRepository repo = CharactersWithInventoryChecker();
            CharacterRecord character = CreateCharacter(repo);
            CreateInventory(CharacterId.NewId(Clock.GetUtcNow())); // unrelated inventory so the in-transaction re-check does only a SELECT

            Result deleted = repo.DeleteCharacterPermanently(_campaign, character.CharacterId, "test cleanup", NewUserId(), actorIsMainGm: true, character.Revisions.LifecycleRevision, NewCommandId(), Corr);

            Assert.That(deleted.IsSuccess, Is.True);
            Assert.That(repo.GetCharacter(_campaign, character.CharacterId, Corr).IsFailure, Is.True);
        }

        [Test] // TC-INVENTORY-087
        public void DeleteCharacterPermanently_FailsClosedWhenInventoryStoreIsUnreadable()
        {
            SqliteCharacterRepository repo = CharactersWithInventoryChecker();
            CharacterRecord character = CreateCharacter(repo);
            CreateInventory(CharacterId.NewId(Clock.GetUtcNow()));
            BreakItemInstanceOwnerColumn();

            Result deleted = repo.DeleteCharacterPermanently(_campaign, character.CharacterId, "test cleanup", NewUserId(), actorIsMainGm: true, character.Revisions.LifecycleRevision, NewCommandId(), Corr);

            Assert.That(deleted.IsFailure, Is.True);
            Assert.That(deleted.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterDeletionHasDependent));
            Assert.That(repo.GetCharacter(_campaign, character.CharacterId, Corr).IsSuccess, Is.True, "an unreadable Inventory store must block the irreversible delete");
        }

        // ---------- HasAnyRuntimeReferenceToDefinition ----------

        [Test] // TC-INVENTORY-088
        public void HasAnyRuntimeReferenceToDefinition_FalseWithNoReference_TrueForAnyPinnedVersion()
        {
            ContentDefinitionId defId = ContentDefinitionId.NewId(Clock.GetUtcNow());
            InventoryRecord inv = CreateInventory(CharacterId.NewId(Clock.GetUtcNow()));

            Assert.That(_inventory.HasAnyRuntimeReferenceToDefinition(_campaign, _campaign.CampaignId, defId, Corr).Value, Is.False);

            SeedRawItemStackReference(inv, defId.ToString() + "/7");

            Assert.That(_inventory.HasAnyRuntimeReferenceToDefinition(_campaign, _campaign.CampaignId, defId, Corr).Value, Is.True);
            Assert.That(_inventory.HasAnyRuntimeReferenceToDefinition(_campaign, _campaign.CampaignId, ContentDefinitionId.NewId(Clock.GetUtcNow()), Corr).Value, Is.False);
        }

        [Test] // TC-INVENTORY-089
        public void HasAnyRuntimeReferenceToDefinition_EscapesLikeMetacharactersInTheDefinitionId()
        {
            ContentDefinitionId defId = ContentDefinitionId.NewId(Clock.GetUtcNow());
            InventoryRecord inv = CreateInventory(CharacterId.NewId(Clock.GetUtcNow()));

            // Same string as defId except the canonical prefix's '_' is a different
            // character. If the '_' were treated as a LIKE wildcard this would
            // false-match; with ESCAPE it must not.
            string lookAlike = "cdefX" + defId.ToString().Substring(5) + "/1";
            SeedRawItemStackReference(inv, lookAlike);

            Assert.That(_inventory.HasAnyRuntimeReferenceToDefinition(_campaign, _campaign.CampaignId, defId, Corr).Value, Is.False);
        }

        // ---------- DeleteDraftDefinition + real checker ----------

        [Test] // TC-INVENTORY-090
        public void DeleteDraftDefinition_BlockedByRuntimeReference_ThenDeletesNormallyWithoutOne()
        {
            var checker = new InventoryContentDefinitionDependencyChecker(_inventory);
            var catalogWithChecker = new SqliteContentCatalogRepository(Clock, runtimeDependencyCheckers: new IContentDefinitionDeletionDependencyChecker[] { checker });
            InventoryRecord inv = CreateInventory(CharacterId.NewId(Clock.GetUtcNow()));

            ContentDefinitionId referenced = CreateDraft(catalogWithChecker);
            SeedRawItemStackReference(inv, referenced.ToString() + "/1");

            Result blocked = catalogWithChecker.DeleteDraftDefinition(_campaign, referenced, NewCommandId(), Corr);

            Assert.That(blocked.IsFailure, Is.True);
            Assert.That(blocked.Error.Code, Is.EqualTo(ErrorCodes.PersistenceContentDefinitionRuntimeReferenced));
            Assert.That(CountRows("SELECT COUNT(*) FROM ContentDefinition WHERE ContentDefinitionId = '" + referenced + "';"), Is.EqualTo(1), "the referenced definition row must not be deleted");
            Assert.That(CountRows("SELECT COUNT(*) FROM ContentDefinitionDeleteLedger;"), Is.EqualTo(0), "a blocked delete writes no delete-ledger entry");

            ContentDefinitionId free = CreateDraft(catalogWithChecker);
            Result deleted = catalogWithChecker.DeleteDraftDefinition(_campaign, free, NewCommandId(), Corr);

            Assert.That(deleted.IsSuccess, Is.True);
            Assert.That(CountRows("SELECT COUNT(*) FROM ContentDefinition WHERE ContentDefinitionId = '" + free + "';"), Is.EqualTo(0));
        }

        // ---------- helpers ----------

        private SqliteCharacterRepository CharactersWithInventoryChecker()
            => new SqliteCharacterRepository(Clock, deletionDependencyCheckers: new ICharacterDeletionDependencyChecker[] { new InventoryCharacterDeletionDependencyChecker(_inventory) });

        private CharacterRecord CreateCharacter(SqliteCharacterRepository repo)
        {
            Result<CharacterRecord> created = repo.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Dependency Test Character"), NewCommandId(), Corr);
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

        private void CreateItemInstance(InventoryRecord inventory, CharacterId ownerCharacterId, InventoryLocationRef location, string sourceRef = "cdef_00000000000000000000000000000000/1")
        {
            UtcInstant now = Clock.GetUtcNow();
            ContentDefinitionRef reference = ParseRef(sourceRef);
            var snapshot = new ItemMechanicsSnapshot(reference, reference.Version, ContentDefinitionType.Item, "{\"m\":\"copied\"}");
            var record = new ItemInstanceRecord(ItemInstanceId.NewId(now), _campaign.CampaignId, inventory.InventoryId, InventoryOwnerRef.ForCharacter(ownerCharacterId), location, reference, snapshot, "{}", 1, now, now);
            Assert.That(_inventory.CreateItemInstance(_campaign, record, NewCommandId(), Corr).IsSuccess, Is.True);
        }

        private void CreateItemStack(InventoryRecord inventory, CharacterId ownerCharacterId, InventoryLocationRef location, string sourceRef)
        {
            UtcInstant now = Clock.GetUtcNow();
            ContentDefinitionRef reference = ParseRef(sourceRef);
            var snapshot = new ItemMechanicsSnapshot(reference, reference.Version, ContentDefinitionType.Ammo, "{\"d\":\"copied\"}");
            var record = new ItemStackRecord(ItemStackId.NewId(now), _campaign.CampaignId, inventory.InventoryId, InventoryOwnerRef.ForCharacter(ownerCharacterId), location, reference, snapshot, ItemStackQuantity.Create(3), "{}", 1, now, now);
            Assert.That(_inventory.CreateItemStack(_campaign, record, NewCommandId(), Corr).IsSuccess, Is.True);
        }

        private static ContentDefinitionRef ParseRef(string value)
        {
            int slash = value.LastIndexOf('/');
            ContentDefinitionId.TryParse(value.Substring(0, slash), out ContentDefinitionId id);
            return new ContentDefinitionRef(id, long.Parse(value.Substring(slash + 1)));
        }

        private ContentDefinitionId CreateDraft(SqliteContentCatalogRepository repo)
        {
            var request = new CreateDraftContentDefinitionRequest(_campaign, ContentDefinitionType.Item, "Dependency Draft", "Test fixture.", NewUserId());
            Result<ContentDefinitionRecord> created = repo.CreateDraftContentDefinition(request, NewCommandId(), Corr);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value.ContentDefinitionId;
        }

        /// <summary>
        /// Direct-insert a runtime reference the public API cannot produce (a
        /// Draft has Version 0; a ContentDefinitionRef requires Version >= 1).
        /// Section 6.3 sanctions this for an infrastructure test of the checker
        /// itself.
        /// </summary>
        private void SeedRawItemStackReference(InventoryRecord inventory, string sourceItemDefinitionRef)
        {
            using SqliteConnection c = Open();
            using var insert = c.CreateCommand();
            insert.CommandText =
                "INSERT INTO ItemStack (ItemStackId, CampaignId, InventoryId, OwnerKind, OwnerTargetRef, OwnerLocationKey, " +
                "LocationKind, LocationTargetRef, LocationDetailRef, SourceItemDefinitionRef, " +
                "MechanicsSourceDefinitionRef, MechanicsDefinitionSnapshotVersion, MechanicsContentType, MechanicsPayload, " +
                "Quantity, StackState, Revision, CreatedAt, UpdatedAt) VALUES (" +
                "$id, $campaignId, $inventoryId, 'Character', $owner, NULL, 'Contained', $inventoryId, 'main', $sourceRef, " +
                "$sourceRef, 1, 'Item', '{}', 1, '{}', 1, $now, $now);";
            insert.Parameters.AddWithValue("$id", "istack_" + Guid.NewGuid().ToString("N"));
            insert.Parameters.AddWithValue("$campaignId", _campaign.CampaignId.ToString());
            insert.Parameters.AddWithValue("$inventoryId", inventory.InventoryId.ToString());
            insert.Parameters.AddWithValue("$owner", CharacterId.NewId(Clock.GetUtcNow()).ToString());
            insert.Parameters.AddWithValue("$sourceRef", sourceItemDefinitionRef);
            insert.Parameters.AddWithValue("$now", Clock.GetUtcNow().ToString());
            insert.ExecuteNonQuery();
        }

        private void BreakItemInstanceOwnerColumn()
        {
            using SqliteConnection c = Open();
            using var alter = c.CreateCommand();
            alter.CommandText = "ALTER TABLE ItemInstance RENAME COLUMN OwnerTargetRef TO OwnerTargetRef_broken;";
            alter.ExecuteNonQuery();
        }

        private long BackupCount()
        {
            using SqliteConnection c = Open();
            using var exists = c.CreateCommand();
            exists.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'BackupRecords';";
            if ((long)exists.ExecuteScalar()! == 0) return 0;
            using var count = c.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM BackupRecords;";
            return (long)count.ExecuteScalar()!;
        }

        private long CountRows(string sql)
        {
            using SqliteConnection c = Open();
            using var q = c.CreateCommand();
            q.CommandText = sql;
            return (long)q.ExecuteScalar()!;
        }

        private SqliteConnection Open()
        {
            var c = new SqliteConnection("Data Source=" + Path.Combine(_campaignDir, "campaign.db"));
            c.Open();
            return c;
        }
    }
}
