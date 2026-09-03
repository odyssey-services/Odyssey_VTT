using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S05-101: real, non-stubbed tests for
    /// <see cref="SqliteContentCatalogRepository"/> against a real
    /// temp-directory campaign and a real SQLite database -- mirroring
    /// <c>SqliteCharacterTemplateRepositoryTests</c>-style sibling fixture
    /// conventions already used throughout this test project. Content
    /// Catalog Foundation only: no authoring business rules, no publish/
    /// archive/delete workflow, no per-type validation, no typed Weapon/
    /// Armor/Ammo/Ability/Effect properties, and (proven directly by this
    /// file's own imports/usages) no dependency whatsoever on any
    /// Inventory/ItemInstance/ItemStack/Equipment/ActiveEffect type.
    /// </summary>
    public sealed class SqliteContentCatalogRepositoryTests
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
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s05-101-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_campaignDir, "Content Catalog Foundation Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = _campaignRepository.Create(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _catalogRepository = new SqliteContentCatalogRepository(Clock);
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaignRepository.Close(_campaign, TestCorrelationId); }
            catch (IOException) { }

            try { if (Directory.Exists(_campaignDir)) Directory.Delete(_campaignDir, recursive: true); }
            catch (IOException) { }
        }

        private CreateDraftContentDefinitionRequest NewDraftRequest(ContentDefinitionType type = ContentDefinitionType.Item, string name = "Test Definition")
            => new CreateDraftContentDefinitionRequest(_campaign, type, name, "A foundation-level test fixture.", NewUserId());

        // ---- Create / read-back --------------------------------------------------

        [Test]
        public void CreateDraftContentDefinition_Succeeds_AndIsStoredAsDraftWithNoPublishedVersion()
        {
            Result<ContentDefinitionRecord> created = _catalogRepository.CreateDraftContentDefinition(NewDraftRequest(), NewCommandId(), TestCorrelationId);

            Assert.That(created.IsSuccess, Is.True);
            Assert.That(created.Value.Status, Is.EqualTo(ContentDefinitionStatus.Draft));
            Assert.That(created.Value.Version, Is.EqualTo(0), "Version 0 means 'never published' -- Foundation never writes a Published version itself");
            Assert.That(created.Value.Revision, Is.EqualTo(1));
            Assert.That(created.Value.Origin, Is.EqualTo(ContentDefinitionOrigin.RulesetPackage), "base/Ruleset catalog only for this MVP block -- no campaign-specific origin is ever produced");
        }

        [Test]
        public void CreateDraftContentDefinition_ThenGetContentDefinition_ReturnsIdenticalRecord()
        {
            Result<ContentDefinitionRecord> created = _catalogRepository.CreateDraftContentDefinition(NewDraftRequest(ContentDefinitionType.Weapon, "Iron Sword"), NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);

            Result<ContentDefinitionRecord> fetched = _catalogRepository.GetContentDefinition(_campaign, created.Value.ContentDefinitionId, TestCorrelationId);

            Assert.That(fetched.IsSuccess, Is.True);
            Assert.That(fetched.Value.ContentDefinitionId, Is.EqualTo(created.Value.ContentDefinitionId));
            Assert.That(fetched.Value.DefinitionType, Is.EqualTo(ContentDefinitionType.Weapon));
            Assert.That(fetched.Value.Name, Is.EqualTo("Iron Sword"));
            Assert.That(fetched.Value.Status, Is.EqualTo(ContentDefinitionStatus.Draft));
            Assert.That(fetched.Value.Revision, Is.EqualTo(1));
        }

        [Test]
        public void CreateDraftContentDefinition_ThenNewRepositoryInstance_StillReadsIdenticalRecord()
        {
            // Proves storage is real, on-disk persistence, not an in-memory
            // fixture -- a brand-new repository instance against the same
            // campaign.db must see exactly what the first instance wrote.
            Result<ContentDefinitionRecord> created = _catalogRepository.CreateDraftContentDefinition(NewDraftRequest(ContentDefinitionType.Ability, "Fireball"), NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);

            var reconnected = new SqliteContentCatalogRepository(Clock);
            Result<ContentDefinitionRecord> fetched = reconnected.GetContentDefinition(_campaign, created.Value.ContentDefinitionId, TestCorrelationId);

            Assert.That(fetched.IsSuccess, Is.True);
            Assert.That(fetched.Value.Name, Is.EqualTo("Fireball"));
            Assert.That(fetched.Value.DefinitionType, Is.EqualTo(ContentDefinitionType.Ability));
        }

        [Test]
        public void GetContentDefinition_OnUnknownId_ReturnsNotFound()
        {
            ContentDefinitionId unknownId = ContentDefinitionId.NewId(Clock.GetUtcNow());

            Result<ContentDefinitionRecord> fetched = _catalogRepository.GetContentDefinition(_campaign, unknownId, TestCorrelationId);

            Assert.That(fetched.IsFailure, Is.True);
            Assert.That(fetched.Error.Code, Is.EqualTo(ErrorCodes.PersistenceContentDefinitionNotFound));
        }

        [Test]
        public void CreateDraftContentDefinition_DuplicateCommandId_DoesNotCreateASecondDefinition()
        {
            CommandId commandId = NewCommandId();
            CreateDraftContentDefinitionRequest request = NewDraftRequest();

            Result<ContentDefinitionRecord> first = _catalogRepository.CreateDraftContentDefinition(request, commandId, TestCorrelationId);
            Result<ContentDefinitionRecord> replay = _catalogRepository.CreateDraftContentDefinition(request, commandId, TestCorrelationId);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.ContentDefinitionId, Is.EqualTo(first.Value.ContentDefinitionId));

            Result<IReadOnlyList<ContentDefinitionRecord>> all = _catalogRepository.ListContentDefinitions(_campaign, null, TestCorrelationId);
            Assert.That(all.Value.Count(d => d.ContentDefinitionId.Equals(first.Value.ContentDefinitionId)), Is.EqualTo(1));
        }

        // ---- List -------------------------------------------------------------

        [Test]
        public void ListContentDefinitions_WithoutFilter_ReturnsEveryDefinition()
        {
            _catalogRepository.CreateDraftContentDefinition(NewDraftRequest(ContentDefinitionType.Weapon, "Sword"), NewCommandId(), TestCorrelationId);
            _catalogRepository.CreateDraftContentDefinition(NewDraftRequest(ContentDefinitionType.Armor, "Shield"), NewCommandId(), TestCorrelationId);

            Result<IReadOnlyList<ContentDefinitionRecord>> all = _catalogRepository.ListContentDefinitions(_campaign, null, TestCorrelationId);

            Assert.That(all.IsSuccess, Is.True);
            Assert.That(all.Value.Count, Is.EqualTo(2));
        }

        [Test]
        public void ListContentDefinitions_WithStatusFilter_ReturnsOnlyMatchingStatus()
        {
            Result<ContentDefinitionRecord> draft = _catalogRepository.CreateDraftContentDefinition(NewDraftRequest(ContentDefinitionType.Weapon, "Draft Sword"), NewCommandId(), TestCorrelationId);
            Assert.That(draft.IsSuccess, Is.True);
            MarkStatusDirectly(draft.Value.ContentDefinitionId, ContentDefinitionStatus.Archived);

            _catalogRepository.CreateDraftContentDefinition(NewDraftRequest(ContentDefinitionType.Armor, "Still Draft Shield"), NewCommandId(), TestCorrelationId);

            Result<IReadOnlyList<ContentDefinitionRecord>> draftsOnly = _catalogRepository.ListContentDefinitions(_campaign, ContentDefinitionStatus.Draft, TestCorrelationId);
            Result<IReadOnlyList<ContentDefinitionRecord>> archivedOnly = _catalogRepository.ListContentDefinitions(_campaign, ContentDefinitionStatus.Archived, TestCorrelationId);

            Assert.That(draftsOnly.Value.Count, Is.EqualTo(1));
            Assert.That(draftsOnly.Value.Single().Name, Is.EqualTo("Still Draft Shield"));
            Assert.That(archivedOnly.Value.Count, Is.EqualTo(1));
            Assert.That(archivedOnly.Value.Single().Name, Is.EqualTo("Draft Sword"));
        }

        // ---- Update / revision --------------------------------------------------

        [Test]
        public void UpdateDraftContentDefinition_Succeeds_AndIncrementsRevision()
        {
            Result<ContentDefinitionRecord> created = _catalogRepository.CreateDraftContentDefinition(NewDraftRequest(), NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);

            Result<ContentDefinitionRecord> updated = _catalogRepository.UpdateDraftContentDefinition(_campaign, created.Value.ContentDefinitionId, "Renamed Definition", "Updated description.", "{\"note\":\"updated\"}", created.Value.Revision, NewCommandId(), TestCorrelationId);

            Assert.That(updated.IsSuccess, Is.True);
            Assert.That(updated.Value.Name, Is.EqualTo("Renamed Definition"));
            Assert.That(updated.Value.Revision, Is.EqualTo(created.Value.Revision + 1));
            Assert.That(updated.Value.PropertiesJson, Is.EqualTo("{\"note\":\"updated\"}"));
        }

        [Test]
        public void UpdateDraftContentDefinition_WithStaleExpectedRevision_IsRejected_NoStateChange()
        {
            Result<ContentDefinitionRecord> created = _catalogRepository.CreateDraftContentDefinition(NewDraftRequest(), NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);

            Result<ContentDefinitionRecord> updated = _catalogRepository.UpdateDraftContentDefinition(_campaign, created.Value.ContentDefinitionId, "Changed Name", null, "{}", created.Value.Revision + 1, NewCommandId(), TestCorrelationId);

            Assert.That(updated.IsFailure, Is.True);
            Assert.That(updated.Error.Code, Is.EqualTo(ErrorCodes.PersistenceContentDefinitionRevisionConflict));

            Result<ContentDefinitionRecord> reRead = _catalogRepository.GetContentDefinition(_campaign, created.Value.ContentDefinitionId, TestCorrelationId);
            Assert.That(reRead.Value.Name, Is.EqualTo(created.Value.Name), "no state change on a rejected stale-revision update");
            Assert.That(reRead.Value.Revision, Is.EqualTo(created.Value.Revision));
        }

        [Test]
        public void UpdateDraftContentDefinition_DuplicateCommandId_DoesNotIncrementRevisionTwice()
        {
            Result<ContentDefinitionRecord> created = _catalogRepository.CreateDraftContentDefinition(NewDraftRequest(), NewCommandId(), TestCorrelationId);
            CommandId updateCommandId = NewCommandId();

            Result<ContentDefinitionRecord> first = _catalogRepository.UpdateDraftContentDefinition(_campaign, created.Value.ContentDefinitionId, "Once", null, "{}", created.Value.Revision, updateCommandId, TestCorrelationId);
            Result<ContentDefinitionRecord> replay = _catalogRepository.UpdateDraftContentDefinition(_campaign, created.Value.ContentDefinitionId, "Once", null, "{}", created.Value.Revision, updateCommandId, TestCorrelationId);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.Revision, Is.EqualTo(first.Value.Revision), "a replayed duplicate CommandId must not increment the revision a second time");
        }

        [Test]
        public void UpdateDraftContentDefinition_OnUnknownId_ReturnsNotFound()
        {
            ContentDefinitionId unknownId = ContentDefinitionId.NewId(Clock.GetUtcNow());

            Result<ContentDefinitionRecord> updated = _catalogRepository.UpdateDraftContentDefinition(_campaign, unknownId, "Name", null, "{}", 1, NewCommandId(), TestCorrelationId);

            Assert.That(updated.IsFailure, Is.True);
            Assert.That(updated.Error.Code, Is.EqualTo(ErrorCodes.PersistenceContentDefinitionNotFound));
        }

        // ---- Published immutability (ADR-027 section 4.1), enforced at foundation level ----

        [Test]
        public void UpdateDraftContentDefinition_OnPublishedDefinition_IsRejected_NoStateChange()
        {
            // ODY-S05-103 (not this task) will own the real PublishDefinition
            // workflow; this test proves the foundation-level immutability
            // guard already exists by seeding a Published-status row
            // directly at the SQL level (there is no public API to publish
            // yet, deliberately).
            Result<ContentDefinitionRecord> created = _catalogRepository.CreateDraftContentDefinition(NewDraftRequest(), NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            MarkStatusDirectly(created.Value.ContentDefinitionId, ContentDefinitionStatus.Published);

            Result<ContentDefinitionRecord> updated = _catalogRepository.UpdateDraftContentDefinition(_campaign, created.Value.ContentDefinitionId, "Should Not Apply", null, "{}", created.Value.Revision, NewCommandId(), TestCorrelationId);

            Assert.That(updated.IsFailure, Is.True);
            Assert.That(updated.Error.Code, Is.EqualTo(ErrorCodes.PersistenceContentDefinitionNotDraft));

            Result<ContentDefinitionRecord> reRead = _catalogRepository.GetContentDefinition(_campaign, created.Value.ContentDefinitionId, TestCorrelationId);
            Assert.That(reRead.Value.Name, Is.EqualTo(created.Value.Name));
            Assert.That(reRead.Value.Status, Is.EqualTo(ContentDefinitionStatus.Published));
        }

        [Test]
        public void UpdateDraftContentDefinition_OnArchivedDefinition_IsRejected()
        {
            Result<ContentDefinitionRecord> created = _catalogRepository.CreateDraftContentDefinition(NewDraftRequest(), NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            MarkStatusDirectly(created.Value.ContentDefinitionId, ContentDefinitionStatus.Archived);

            Result<ContentDefinitionRecord> updated = _catalogRepository.UpdateDraftContentDefinition(_campaign, created.Value.ContentDefinitionId, "Should Not Apply", null, "{}", created.Value.Revision, NewCommandId(), TestCorrelationId);

            Assert.That(updated.IsFailure, Is.True);
            Assert.That(updated.Error.Code, Is.EqualTo(ErrorCodes.PersistenceContentDefinitionNotDraft));
        }

        // ---- Exact-version reference round-trip ---------------------------------

        [Test]
        public void CreateDraftContentDefinition_WithDependencyRefs_RoundTripsExactVersionReferences()
        {
            var dependencyTarget = new ContentDefinitionRef(ContentDefinitionId.NewId(Clock.GetUtcNow()), 4);
            var request = new CreateDraftContentDefinitionRequest(
                _campaign, ContentDefinitionType.Weapon, "Needs Ammo", "Requires a specific ammo version.", NewUserId(),
                dependencyRefs: new[] { dependencyTarget });

            Result<ContentDefinitionRecord> created = _catalogRepository.CreateDraftContentDefinition(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            Assert.That(created.Value.DependencyRefs.Single(), Is.EqualTo(dependencyTarget));

            var reconnected = new SqliteContentCatalogRepository(Clock);
            Result<ContentDefinitionRecord> fetched = reconnected.GetContentDefinition(_campaign, created.Value.ContentDefinitionId, TestCorrelationId);

            Assert.That(fetched.IsSuccess, Is.True);
            Assert.That(fetched.Value.DependencyRefs.Single(), Is.EqualTo(dependencyTarget));
            Assert.That(fetched.Value.DependencyRefs.Single().DefinitionId, Is.EqualTo(dependencyTarget.DefinitionId));
            Assert.That(fetched.Value.DependencyRefs.Single().Version, Is.EqualTo(4));
        }

        [Test]
        public void CreateDraftContentDefinition_WithRulesetCompatibilityAndTags_RoundTrips()
        {
            var request = new CreateDraftContentDefinitionRequest(
                _campaign, ContentDefinitionType.Effect, "Poison", "A damage-over-time effect.", NewUserId(),
                rulesetCompatibility: new[] { "ruleset.core@1.0.0" },
                tags: new[] { "damage", "dot" });

            Result<ContentDefinitionRecord> created = _catalogRepository.CreateDraftContentDefinition(request, NewCommandId(), TestCorrelationId);

            Assert.That(created.IsSuccess, Is.True);
            Assert.That(created.Value.RulesetCompatibility, Is.EquivalentTo(new[] { "ruleset.core@1.0.0" }));
            Assert.That(created.Value.Tags, Is.EquivalentTo(new[] { "damage", "dot" }));
        }

        // ---- Catalog stores definitions only, no runtime item/equipment/effect state ----

        [Test]
        public void ContentDefinitionTable_HasNoRuntimeItemInventoryEquipmentOrActiveEffectColumns()
        {
            // ADR-027 section 4: "Content Catalog owns definitions only.
            // Runtime instances are separate authoritative state." This
            // asserts the real, physical table schema directly -- not just
            // the C# type shape -- has no such column, proving no runtime
            // item/equipment/effect state was smuggled into the catalog
            // table itself.
            Result<ContentDefinitionRecord> created = _catalogRepository.CreateDraftContentDefinition(NewDraftRequest(), NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);

            using var connection = new SqliteConnection("Data Source=" + Path.Combine(_campaignDir, "campaign.db"));
            connection.Open();

            using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA table_info(ContentDefinition);";
            using SqliteDataReader reader = pragma.ExecuteReader();
            var columnNames = new System.Collections.Generic.List<string>();
            while (reader.Read()) columnNames.Add(reader.GetString(1));

            string[] forbiddenSubstrings = { "Inventory", "ItemInstance", "ItemStack", "Equipment", "ActiveEffect" };
            foreach (string column in columnNames)
            {
                foreach (string forbidden in forbiddenSubstrings)
                {
                    Assert.That(column, Does.Not.Contain(forbidden), $"ContentDefinition column '{column}' must not reference runtime item/inventory/equipment/effect state ('{forbidden}')");
                }
            }
        }

        [Test]
        public void CampaignDatabase_HasNoInventoryItemInstanceItemStackOrEquipmentOrActiveEffectTable()
        {
            // Direct proof at the schema level that this task introduced no
            // runtime aggregate whatsoever -- Inventory/ItemInstance/
            // ItemStack/Equipment/ActiveEffect tables simply do not exist
            // anywhere in the campaign database after this task's own
            // repository has run.
            _catalogRepository.CreateDraftContentDefinition(NewDraftRequest(), NewCommandId(), TestCorrelationId);

            using var connection = new SqliteConnection("Data Source=" + Path.Combine(_campaignDir, "campaign.db"));
            connection.Open();
            using var select = connection.CreateCommand();
            select.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
            using SqliteDataReader reader = select.ExecuteReader();
            var tableNames = new System.Collections.Generic.List<string>();
            while (reader.Read()) tableNames.Add(reader.GetString(0));

            string[] forbiddenTableNames = { "Inventory", "ItemInstance", "ItemStack", "Equipment", "ActiveEffect" };
            foreach (string forbidden in forbiddenTableNames)
            {
                Assert.That(tableNames, Has.None.Contain(forbidden), $"no table containing '{forbidden}' may exist -- this task implements Content Catalog Foundation only");
            }
        }

        private void MarkStatusDirectly(ContentDefinitionId definitionId, ContentDefinitionStatus status)
        {
            using var connection = new SqliteConnection("Data Source=" + Path.Combine(_campaignDir, "campaign.db"));
            connection.Open();
            using var update = connection.CreateCommand();
            update.CommandText = "UPDATE ContentDefinition SET Status = $status WHERE ContentDefinitionId = $id;";
            update.Parameters.AddWithValue("$status", status.ToString());
            update.Parameters.AddWithValue("$id", definitionId.ToString());
            update.ExecuteNonQuery();
        }
    }
}
