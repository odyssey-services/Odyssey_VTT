using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Content;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence.Content
{
    /// <summary>
    /// ODY-S05-103: real, SQLite-backed tests for
    /// <see cref="ContentCatalogLifecycleService"/> against the real
    /// <see cref="SqliteContentCatalogRepository"/> built by
    /// `ODY-S05-101`/`102`, gated by `ODY-S05-104`'s own
    /// <see cref="CatalogValidationService"/>. Publish/Archive/Delete
    /// Lifecycle only: no runtime Inventory/ItemInstance/ItemStack/
    /// Equipment/ActiveEffect, no attack pipeline, no Unity/UI, no
    /// balanced content fixtures.
    /// </summary>
    public sealed class ContentCatalogLifecycleServiceTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;
        private SqliteContentCatalogRepository _catalogRepository = null!;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s05-103-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_campaignDir, "Lifecycle Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = _campaignRepository.Create(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _catalogRepository = new SqliteContentCatalogRepository(Clock);
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaignRepository.Close(_campaign, TestCorrelationId); } catch (IOException) { }
            try { if (Directory.Exists(_campaignDir)) Directory.Delete(_campaignDir, recursive: true); } catch (IOException) { }
        }

        // ---- fixture helpers ------------------------------------------------------

        private static string EncodeValidItem()
        {
            var item = new ItemDefinition(ItemCategory.Generic, false, null, 1, false, null, false, null, Array.Empty<ContentDefinitionRef>(), Array.Empty<ContentDefinitionRef>());
            return TypedDefinitionCodec.EncodeItem(item);
        }

        private ContentDefinitionRecord CreateValidDraft(IReadOnlyList<ContentDefinitionRef>? dependencyRefs = null, string name = "Test Definition")
        {
            var request = new CreateDraftContentDefinitionRequest(_campaign, ContentDefinitionType.Item, name, "A lifecycle test fixture.", NewUserId(), propertiesJson: EncodeValidItem(), dependencyRefs: dependencyRefs);
            Result<ContentDefinitionRecord> created = _catalogRepository.CreateDraftContentDefinition(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True, "fixture setup must itself succeed");
            return created.Value;
        }

        private ContentDefinitionRecord CreateInvalidDraft()
        {
            // Weapon requiring ammo with no compatible keys -- a genuine
            // ODY-S05-104 usability gap, not just malformed JSON.
            var item = new ItemDefinition(ItemCategory.Generic, false, null, 1, false, null, false, null, Array.Empty<ContentDefinitionRef>(), Array.Empty<ContentDefinitionRef>());
            var weapon = new WeaponDefinition(item, "1d6", 5, WeaponAttackMode.Melee, 1, AmmoRequirement.Required, Array.Empty<string>());
            string weaponJson = TypedDefinitionCodec.EncodeWeapon(weapon);
            var request = new CreateDraftContentDefinitionRequest(_campaign, ContentDefinitionType.Weapon, "Invalid Weapon", "Deliberately unpublishable.", NewUserId(), propertiesJson: weaponJson);
            Result<ContentDefinitionRecord> created = _catalogRepository.CreateDraftContentDefinition(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        private PublishDefinitionRequest PublishRequest(ContentDefinitionId id, long expectedRevision, bool actorIsMainGm = true) =>
            new PublishDefinitionRequest(_campaign, id, expectedRevision, NewUserId(), actorIsMainGm, NewCommandId(), TestCorrelationId);

        private ArchiveDefinitionRequest ArchiveRequest(ContentDefinitionId id, bool actorIsMainGm = true, string? reason = "no longer needed") =>
            new ArchiveDefinitionRequest(_campaign, id, reason, actorIsMainGm, NewCommandId(), TestCorrelationId);

        private DeleteDraftDefinitionRequest DeleteRequest(ContentDefinitionId id, bool actorIsMainGm = true) =>
            new DeleteDraftDefinitionRequest(_campaign, id, actorIsMainGm, NewCommandId(), TestCorrelationId);

        private ContentDefinitionRecord Publish(ContentDefinitionId id, long expectedRevision = 1)
        {
            Result<ContentDefinitionRecord> result = ContentCatalogLifecycleService.PublishDefinition(_catalogRepository, PublishRequest(id, expectedRevision));
            Assert.That(result.IsSuccess, Is.True, "fixture setup must itself succeed");
            return result.Value;
        }

        // ---- 1/5/6. Publish ---------------------------------------------------------

        [Test]
        public void PublishDefinition_ByMainGm_OnValidDraft_Succeeds()
        {
            ContentDefinitionRecord draft = CreateValidDraft();

            Result<ContentDefinitionRecord> result = ContentCatalogLifecycleService.PublishDefinition(_catalogRepository, PublishRequest(draft.ContentDefinitionId, draft.Revision));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Status, Is.EqualTo(ContentDefinitionStatus.Published));
            Assert.That(result.Value.Version, Is.EqualTo(1));
            Assert.That(result.Value.Revision, Is.EqualTo(draft.Revision + 1));
            Assert.That(result.Value.PublishedByUserId, Is.Not.Null);
            Assert.That(result.Value.PublishedAt, Is.Not.Null);
        }

        [Test]
        public void PublishDefinition_OnInvalidDraft_FailsWithoutMutation()
        {
            ContentDefinitionRecord draft = CreateInvalidDraft();

            Result<ContentDefinitionRecord> result = ContentCatalogLifecycleService.PublishDefinition(_catalogRepository, PublishRequest(draft.ContentDefinitionId, draft.Revision));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.ContentCatalogPublishValidationFailed));

            Result<ContentDefinitionRecord> reread = _catalogRepository.GetContentDefinition(_campaign, draft.ContentDefinitionId, TestCorrelationId);
            Assert.That(reread.IsSuccess, Is.True);
            Assert.That(reread.Value.Status, Is.EqualTo(ContentDefinitionStatus.Draft));
            Assert.That(reread.Value.Revision, Is.EqualTo(draft.Revision));
            Assert.That(reread.Value.Version, Is.EqualTo(0));
        }

        [Test]
        public void PublishDefinition_ByNonMainGm_FailsWithoutStateChange()
        {
            ContentDefinitionRecord draft = CreateValidDraft();

            Result<ContentDefinitionRecord> result = ContentCatalogLifecycleService.PublishDefinition(_catalogRepository, PublishRequest(draft.ContentDefinitionId, draft.Revision, actorIsMainGm: false));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.ContentCatalogAuthoringDenied));

            Result<ContentDefinitionRecord> reread = _catalogRepository.GetContentDefinition(_campaign, draft.ContentDefinitionId, TestCorrelationId);
            Assert.That(reread.Value.Status, Is.EqualTo(ContentDefinitionStatus.Draft));
            Assert.That(reread.Value.Revision, Is.EqualTo(draft.Revision));
        }

        [Test]
        public void PublishedDefinition_CannotBeUpdatedThroughDraftUpdatePath()
        {
            ContentDefinitionRecord draft = CreateValidDraft();
            ContentDefinitionRecord published = Publish(draft.ContentDefinitionId, draft.Revision);

            Result<ContentDefinitionRecord> updateResult = _catalogRepository.UpdateDraftContentDefinition(_campaign, published.ContentDefinitionId, "New Name", "New Description", EncodeValidItem(), published.Revision, NewCommandId(), TestCorrelationId);

            Assert.That(updateResult.IsFailure, Is.True);
            Assert.That(updateResult.Error.Code, Is.EqualTo(ErrorCodes.PersistenceContentDefinitionNotDraft));
        }

        [Test]
        public void PublishDefinition_OnNonDraft_Fails()
        {
            ContentDefinitionRecord draft = CreateValidDraft();
            ContentDefinitionRecord published = Publish(draft.ContentDefinitionId, draft.Revision);

            Result<ContentDefinitionRecord> result = ContentCatalogLifecycleService.PublishDefinition(_catalogRepository, PublishRequest(published.ContentDefinitionId, published.Revision));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceContentDefinitionNotDraft));
        }

        [Test]
        public void PublishDefinition_CommandReplay_ReturnsStableResult_NoDoubleIncrement()
        {
            ContentDefinitionRecord draft = CreateValidDraft();
            var request = PublishRequest(draft.ContentDefinitionId, draft.Revision);

            Result<ContentDefinitionRecord> first = ContentCatalogLifecycleService.PublishDefinition(_catalogRepository, request);
            Assert.That(first.IsSuccess, Is.True);

            // Replay the exact same CommandId directly against the
            // repository (the service layer skips validation once the
            // record is no longer Draft, precisely so this replay reaches
            // the repository's own ledger-based idempotency check).
            Result<ContentDefinitionRecord> replay = _catalogRepository.PublishDefinition(_campaign, draft.ContentDefinitionId, request.ActorUserId, draft.Revision, request.CommandId, TestCorrelationId);

            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.Version, Is.EqualTo(first.Value.Version));
            Assert.That(replay.Value.Revision, Is.EqualTo(first.Value.Revision));
        }

        // ---- 7/8/9/10. Archive ------------------------------------------------------

        [Test]
        public void ArchiveDefinition_ByMainGm_OnPublished_Succeeds()
        {
            ContentDefinitionRecord draft = CreateValidDraft();
            ContentDefinitionRecord published = Publish(draft.ContentDefinitionId, draft.Revision);

            Result<ContentDefinitionRecord> result = ContentCatalogLifecycleService.ArchiveDefinition(_catalogRepository, ArchiveRequest(published.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Status, Is.EqualTo(ContentDefinitionStatus.Archived));
            Assert.That(result.Value.ArchivedAt, Is.Not.Null);
            Assert.That(result.Value.ArchiveReason, Is.EqualTo("no longer needed"));
        }

        [Test]
        public void ArchivedDefinition_RemainsLoadableThroughGetContentDefinition()
        {
            ContentDefinitionRecord draft = CreateValidDraft();
            ContentDefinitionRecord published = Publish(draft.ContentDefinitionId, draft.Revision);
            ContentCatalogLifecycleService.ArchiveDefinition(_catalogRepository, ArchiveRequest(published.ContentDefinitionId));

            Result<ContentDefinitionRecord> reread = _catalogRepository.GetContentDefinition(_campaign, published.ContentDefinitionId, TestCorrelationId);

            Assert.That(reread.IsSuccess, Is.True);
            Assert.That(reread.Value.Status, Is.EqualTo(ContentDefinitionStatus.Archived));
        }

        [Test]
        public void ListArchivedDefinitions_ReturnsOnlyArchived_SeparateFromDraftAndPublished()
        {
            ContentDefinitionRecord stillDraft = CreateValidDraft(name: "Still Draft");
            ContentDefinitionRecord stillPublished = Publish(CreateValidDraft(name: "Still Published").ContentDefinitionId);
            ContentDefinitionRecord archived = Publish(CreateValidDraft(name: "Will Be Archived").ContentDefinitionId);
            ContentCatalogLifecycleService.ArchiveDefinition(_catalogRepository, ArchiveRequest(archived.ContentDefinitionId));

            Result<IReadOnlyList<ContentDefinitionRecord>> listed = ContentCatalogLifecycleService.ListArchivedDefinitions(_catalogRepository, new ListArchivedDefinitionsRequest(_campaign, actorIsMainGm: true, TestCorrelationId));

            Assert.That(listed.IsSuccess, Is.True);
            Assert.That(listed.Value.Select(r => r.ContentDefinitionId), Does.Contain(archived.ContentDefinitionId));
            Assert.That(listed.Value.Select(r => r.ContentDefinitionId), Does.Not.Contain(stillDraft.ContentDefinitionId));
            Assert.That(listed.Value.Select(r => r.ContentDefinitionId), Does.Not.Contain(stillPublished.ContentDefinitionId));
            Assert.That(listed.Value, Has.All.Matches<ContentDefinitionRecord>(r => r.Status == ContentDefinitionStatus.Archived));
        }

        [Test]
        public void ArchiveDefinition_ByNonMainGm_FailsWithoutStateChange()
        {
            ContentDefinitionRecord draft = CreateValidDraft();
            ContentDefinitionRecord published = Publish(draft.ContentDefinitionId, draft.Revision);

            Result<ContentDefinitionRecord> result = ContentCatalogLifecycleService.ArchiveDefinition(_catalogRepository, ArchiveRequest(published.ContentDefinitionId, actorIsMainGm: false));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.ContentCatalogAuthoringDenied));

            Result<ContentDefinitionRecord> reread = _catalogRepository.GetContentDefinition(_campaign, published.ContentDefinitionId, TestCorrelationId);
            Assert.That(reread.Value.Status, Is.EqualTo(ContentDefinitionStatus.Published));
        }

        [Test]
        public void ArchiveDefinition_OnDraft_Fails()
        {
            ContentDefinitionRecord draft = CreateValidDraft();

            Result<ContentDefinitionRecord> result = ContentCatalogLifecycleService.ArchiveDefinition(_catalogRepository, ArchiveRequest(draft.ContentDefinitionId));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceContentDefinitionNotPublished));
        }

        // ---- 11/12/13/14/15. Physical delete -----------------------------------------

        [Test]
        public void DeleteDraftDefinition_OnUnusedDraft_Succeeds()
        {
            ContentDefinitionRecord draft = CreateValidDraft();

            Result result = ContentCatalogLifecycleService.DeleteDraftDefinition(_catalogRepository, DeleteRequest(draft.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Result<ContentDefinitionRecord> reread = _catalogRepository.GetContentDefinition(_campaign, draft.ContentDefinitionId, TestCorrelationId);
            Assert.That(reread.IsFailure, Is.True);
            Assert.That(reread.Error.Code, Is.EqualTo(ErrorCodes.PersistenceContentDefinitionNotFound));
        }

        [Test]
        public void DeleteDraftDefinition_OnPublished_Fails()
        {
            ContentDefinitionRecord draft = CreateValidDraft();
            ContentDefinitionRecord published = Publish(draft.ContentDefinitionId, draft.Revision);

            Result result = ContentCatalogLifecycleService.DeleteDraftDefinition(_catalogRepository, DeleteRequest(published.ContentDefinitionId));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceContentDefinitionNotDraft));

            Result<ContentDefinitionRecord> reread = _catalogRepository.GetContentDefinition(_campaign, published.ContentDefinitionId, TestCorrelationId);
            Assert.That(reread.IsSuccess, Is.True);
        }

        [Test]
        public void DeleteDraftDefinition_OnArchived_Fails()
        {
            ContentDefinitionRecord draft = CreateValidDraft();
            ContentDefinitionRecord published = Publish(draft.ContentDefinitionId, draft.Revision);
            ContentCatalogLifecycleService.ArchiveDefinition(_catalogRepository, ArchiveRequest(published.ContentDefinitionId));

            Result result = ContentCatalogLifecycleService.DeleteDraftDefinition(_catalogRepository, DeleteRequest(published.ContentDefinitionId));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceContentDefinitionNotDraft));
        }

        [Test]
        public void DeleteDraftDefinition_ReferencedByAnotherCatalogDefinition_Fails()
        {
            // A ContentDefinitionRef requires Version >= 1 and can
            // therefore never legitimately target a genuine Draft
            // (Version == 0) through the public API -- so this scenario is
            // constructed with a direct-SQL-seeded DependencyRefsJson,
            // proving the defensive scan itself works (ODY-S05-103's own
            // contract section 18 records this as an intentional,
            // forward-compatible safety net, not a reachable production
            // path today).
            ContentDefinitionRecord target = CreateValidDraft(name: "Referenced Draft");
            ContentDefinitionRecord referencer = CreateValidDraft(name: "Referencer");
            SeedDependencyRefDirectly(referencer.ContentDefinitionId, target.ContentDefinitionId, version: 1);

            Result result = ContentCatalogLifecycleService.DeleteDraftDefinition(_catalogRepository, DeleteRequest(target.ContentDefinitionId));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceContentDefinitionReferenced));

            Result<ContentDefinitionRecord> reread = _catalogRepository.GetContentDefinition(_campaign, target.ContentDefinitionId, TestCorrelationId);
            Assert.That(reread.IsSuccess, Is.True);
        }

        [Test]
        public void DeleteDraftDefinition_NotReferenced_StillSucceeds_WhenAnUnrelatedReferenceExistsElsewhere()
        {
            ContentDefinitionRecord unrelatedTarget = CreateValidDraft(name: "Unrelated Referenced Draft");
            ContentDefinitionRecord referencer = CreateValidDraft(name: "Referencer");
            SeedDependencyRefDirectly(referencer.ContentDefinitionId, unrelatedTarget.ContentDefinitionId, version: 1);

            ContentDefinitionRecord actualTarget = CreateValidDraft(name: "Truly Unused Draft");
            Result result = ContentCatalogLifecycleService.DeleteDraftDefinition(_catalogRepository, DeleteRequest(actualTarget.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
        }

        [Test]
        public void DeleteDraftDefinition_ByNonMainGm_FailsWithoutStateChange()
        {
            ContentDefinitionRecord draft = CreateValidDraft();

            Result result = ContentCatalogLifecycleService.DeleteDraftDefinition(_catalogRepository, DeleteRequest(draft.ContentDefinitionId, actorIsMainGm: false));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.ContentCatalogAuthoringDenied));

            Result<ContentDefinitionRecord> reread = _catalogRepository.GetContentDefinition(_campaign, draft.ContentDefinitionId, TestCorrelationId);
            Assert.That(reread.IsSuccess, Is.True);
        }

        [Test]
        public void DeleteDraftDefinition_CommandReplay_IsSafeAfterRowIsGone()
        {
            ContentDefinitionRecord draft = CreateValidDraft();
            var request = DeleteRequest(draft.ContentDefinitionId);

            Result first = ContentCatalogLifecycleService.DeleteDraftDefinition(_catalogRepository, request);
            Assert.That(first.IsSuccess, Is.True);

            Result replay = _catalogRepository.DeleteDraftDefinition(_campaign, draft.ContentDefinitionId, request.CommandId, TestCorrelationId);

            Assert.That(replay.IsSuccess, Is.True, "a replay of an already-applied delete command must succeed even though the row itself no longer exists");
        }

        [Test]
        public void DeleteDraftDefinition_ReusingCommandIdFromANonDeleteOperation_FailsWithIdentityMismatch_AndLeavesDraftReadable()
        {
            // Second amendment regression guard: a CommandId already
            // recorded by the *shared* ContentDefinitionCommandLedger
            // (Create/Update/Publish/Archive/CreateNextDraftVersionFromPublished)
            // was never actually used for a delete -- reusing it here is
            // always a genuine CommandId identity violation, never a
            // legitimate delete replay, even when the target row still
            // exists as a Draft. The first amendment's own delete-only
            // ledger alone did not catch this, since a CommandId recorded
            // only in the shared ledger would simply never appear there,
            // and the method would incorrectly fall through to a real
            // delete.
            var request = new CreateDraftContentDefinitionRequest(_campaign, ContentDefinitionType.Item, "Reused CommandId Target", "fixture", NewUserId(), propertiesJson: EncodeValidItem());
            CommandId reusedCommandId = NewCommandId();
            Result<ContentDefinitionRecord> created = _catalogRepository.CreateDraftContentDefinition(request, reusedCommandId, TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);

            Result deleteResult = _catalogRepository.DeleteDraftDefinition(_campaign, created.Value.ContentDefinitionId, reusedCommandId, TestCorrelationId);

            Assert.That(deleteResult.IsFailure, Is.True, "a CommandId already used for a non-delete operation must never be accepted as a delete -- replay or otherwise");
            Assert.That(deleteResult.Error.Code, Is.EqualTo(ErrorCodes.CommandIdentityMismatch));
            Result<ContentDefinitionRecord> reread = _catalogRepository.GetContentDefinition(_campaign, created.Value.ContentDefinitionId, TestCorrelationId);
            Assert.That(reread.IsSuccess, Is.True, "the Draft must remain untouched -- this CommandId was never a legitimate delete command for it");
            Assert.That(reread.Value.Status, Is.EqualTo(ContentDefinitionStatus.Draft));
        }

        [Test]
        public void DeleteDraftDefinition_ReusingCommandIdFromAnotherDefinitionsDelete_FailsWithIdentityMismatch_AndDoesNotDeleteEither()
        {
            ContentDefinitionRecord alreadyDeletedElsewhere = CreateValidDraft(name: "Already Deleted Elsewhere");
            CommandId reusedCommandId = NewCommandId();
            Result firstDelete = _catalogRepository.DeleteDraftDefinition(_campaign, alreadyDeletedElsewhere.ContentDefinitionId, reusedCommandId, TestCorrelationId);
            Assert.That(firstDelete.IsSuccess, Is.True);

            ContentDefinitionRecord unrelatedTarget = CreateValidDraft(name: "Unrelated Target");
            Result secondDelete = _catalogRepository.DeleteDraftDefinition(_campaign, unrelatedTarget.ContentDefinitionId, reusedCommandId, TestCorrelationId);

            Assert.That(secondDelete.IsFailure, Is.True);
            Assert.That(secondDelete.Error.Code, Is.EqualTo(ErrorCodes.CommandIdentityMismatch));

            Result<ContentDefinitionRecord> rereadUnrelated = _catalogRepository.GetContentDefinition(_campaign, unrelatedTarget.ContentDefinitionId, TestCorrelationId);
            Assert.That(rereadUnrelated.IsSuccess, Is.True, "the unrelated target must not be deleted by a CommandId that actually belongs to a different definition's own delete");
        }

        private void SeedDependencyRefDirectly(ContentDefinitionId referencerId, ContentDefinitionId targetId, long version)
        {
            using var connection = new SqliteConnection("Data Source=" + Path.Combine(_campaignDir, "campaign.db"));
            connection.Open();
            using var update = connection.CreateCommand();
            update.CommandText = "UPDATE ContentDefinition SET DependencyRefsJson = $deps WHERE ContentDefinitionId = $id;";
            update.Parameters.AddWithValue("$deps", "[\"" + targetId + "/" + version + "\"]");
            update.Parameters.AddWithValue("$id", referencerId.ToString());
            update.ExecuteNonQuery();
        }

        // ---- 16/17. Schema and namespace guards --------------------------------------

        [Test]
        public void LifecycleLayer_IntroducesNoRuntimeItemInventoryEquipmentOrActiveEffectType()
        {
            System.Reflection.Assembly applicationAssembly = typeof(ContentCatalogLifecycleService).Assembly;
            var typesToCheck = applicationAssembly.GetTypes().Where(t => t.Namespace == "Odyssey.Application.Content").ToArray();

            string[] forbiddenSubstrings = { "Inventory", "ItemInstance", "ItemStack", "Equipment", "ActiveEffect" };
            foreach (System.Type type in typesToCheck)
            {
                foreach (string forbidden in forbiddenSubstrings)
                {
                    Assert.That(type.Name, Does.Not.Contain(forbidden), $"type '{type.Name}' must not reference runtime item/inventory/equipment/effect state ('{forbidden}')");
                }
            }
        }

        [Test]
        public void LifecycleLayer_IntroducesNoRuntimeItemInventoryEquipmentOrActiveEffectTable()
        {
            CreateValidDraft(); // ensures ContentDefinition/-CommandLedger/-DeleteLedger tables exist (created lazily on first use)

            using var connection = new SqliteConnection("Data Source=" + Path.Combine(_campaignDir, "campaign.db"));
            connection.Open();
            using var select = connection.CreateCommand();
            select.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
            using SqliteDataReader reader = select.ExecuteReader();
            var tableNames = new List<string>();
            while (reader.Read()) tableNames.Add(reader.GetString(0));

            string[] forbiddenTableNames = { "Inventory", "ItemInstance", "ItemStack", "Equipment", "ActiveEffect" };
            foreach (string forbidden in forbiddenTableNames)
            {
                Assert.That(tableNames, Has.None.Contain(forbidden), $"no table containing '{forbidden}' may exist -- ODY-S05-103 implements catalog lifecycle only, no new runtime item/inventory/equipment/effect persistence table");
            }

            // The catalog lifecycle's own tables (generic envelope plus its
            // two idempotency ledgers -- the second added by this task's
            // own amendment fixing DeleteDraftDefinition's idempotency) are
            // exactly what is allowed to exist; their presence is asserted
            // here, not just their absence-of-forbidden-names, so this
            // guard stays meaningful if a future change accidentally drops
            // one of them.
            Assert.That(tableNames, Does.Contain("ContentDefinition"));
            Assert.That(tableNames, Does.Contain("ContentDefinitionCommandLedger"));
            Assert.That(tableNames, Does.Contain("ContentDefinitionDeleteLedger"));
        }
    }
}
