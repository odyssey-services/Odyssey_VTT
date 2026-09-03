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
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence.Content
{
    /// <summary>
    /// ODY-S05-102: real, SQLite-backed tests for
    /// <see cref="ContentCatalogAuthoringService"/> against the real
    /// <see cref="SqliteContentCatalogRepository"/> built by `ODY-S05-101`
    /// -- mirroring <c>BoardMovementServiceTests</c>'s exact fixture
    /// convention (an Application-layer service tested against its own real
    /// repository, not a mock). GM Catalog Authoring MVP only: no publish/
    /// archive/delete, no validation, no typed properties.
    /// </summary>
    public sealed class ContentCatalogAuthoringServiceTests
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
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s05-102-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_campaignDir, "Catalog Authoring Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
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

        private CreateDraftDefinitionRequest NewCreateRequest(bool actorIsMainGm = true, string name = "Iron Sword", ContentDefinitionType type = ContentDefinitionType.Weapon)
            => new CreateDraftDefinitionRequest(_campaign, type, name, "A test fixture.", NewUserId(), actorIsMainGm, NewCommandId(), TestCorrelationId);

        // ---- CreateDraftDefinition ----------------------------------------------

        [Test]
        public void CreateDraftDefinition_ByMainGm_Succeeds_AndPersistsFoundationFields()
        {
            Result<ContentDefinitionRecord> result = ContentCatalogAuthoringService.CreateDraftDefinition(_catalogRepository, NewCreateRequest());

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Status, Is.EqualTo(ContentDefinitionStatus.Draft));
            Assert.That(result.Value.Version, Is.EqualTo(0));
            Assert.That(result.Value.Revision, Is.EqualTo(1));
            Assert.That(result.Value.Origin, Is.EqualTo(ContentDefinitionOrigin.RulesetPackage));
            Assert.That(result.Value.Name, Is.EqualTo("Iron Sword"));

            Result<ContentDefinitionRecord> reRead = _catalogRepository.GetContentDefinition(_campaign, result.Value.ContentDefinitionId, TestCorrelationId);
            Assert.That(reRead.IsSuccess, Is.True);
            Assert.That(reRead.Value.Name, Is.EqualTo("Iron Sword"));
        }

        [Test]
        public void CreateDraftDefinition_ByNonMainGm_IsRejected_NoStateChange()
        {
            Result<ContentDefinitionRecord> result = ContentCatalogAuthoringService.CreateDraftDefinition(_catalogRepository, NewCreateRequest(actorIsMainGm: false));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.ContentCatalogAuthoringDenied));

            Result<IReadOnlyList<ContentDefinitionRecord>> all = _catalogRepository.ListContentDefinitions(_campaign, null, TestCorrelationId);
            Assert.That(all.Value, Is.Empty, "a denied authoring request must cause no repository state change at all");
        }

        [Test]
        public void CreateDraftDefinition_ReplayOfSameCommandId_IsIdempotent()
        {
            CreateDraftDefinitionRequest request = NewCreateRequest();

            Result<ContentDefinitionRecord> first = ContentCatalogAuthoringService.CreateDraftDefinition(_catalogRepository, request);
            Result<ContentDefinitionRecord> replay = ContentCatalogAuthoringService.CreateDraftDefinition(_catalogRepository, request);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.ContentDefinitionId, Is.EqualTo(first.Value.ContentDefinitionId));

            Result<IReadOnlyList<ContentDefinitionRecord>> all = _catalogRepository.ListContentDefinitions(_campaign, null, TestCorrelationId);
            Assert.That(all.Value.Count, Is.EqualTo(1), "replaying the same CommandId must not create a second Draft");
        }

        // ---- UpdateDraftDefinition -----------------------------------------------

        [Test]
        public void UpdateDraftDefinition_ByMainGm_Succeeds_AndIncrementsRevisionOnce()
        {
            Result<ContentDefinitionRecord> created = ContentCatalogAuthoringService.CreateDraftDefinition(_catalogRepository, NewCreateRequest());
            Assert.That(created.IsSuccess, Is.True);

            var updateRequest = new UpdateDraftDefinitionRequest(_campaign, created.Value.ContentDefinitionId, "Steel Sword", "Renamed by MainGM.", "{}", created.Value.Revision, NewUserId(), actorIsMainGm: true, NewCommandId(), TestCorrelationId);
            Result<ContentDefinitionRecord> updated = ContentCatalogAuthoringService.UpdateDraftDefinition(_catalogRepository, updateRequest);

            Assert.That(updated.IsSuccess, Is.True);
            Assert.That(updated.Value.Name, Is.EqualTo("Steel Sword"));
            Assert.That(updated.Value.Revision, Is.EqualTo(created.Value.Revision + 1));
        }

        [Test]
        public void UpdateDraftDefinition_WithStaleRevision_IsRejected_NoStateChange()
        {
            Result<ContentDefinitionRecord> created = ContentCatalogAuthoringService.CreateDraftDefinition(_catalogRepository, NewCreateRequest());
            Assert.That(created.IsSuccess, Is.True);

            var updateRequest = new UpdateDraftDefinitionRequest(_campaign, created.Value.ContentDefinitionId, "Should Not Apply", null, "{}", created.Value.Revision + 1, NewUserId(), actorIsMainGm: true, NewCommandId(), TestCorrelationId);
            Result<ContentDefinitionRecord> updated = ContentCatalogAuthoringService.UpdateDraftDefinition(_catalogRepository, updateRequest);

            Assert.That(updated.IsFailure, Is.True);
            Assert.That(updated.Error.Code, Is.EqualTo(ErrorCodes.PersistenceContentDefinitionRevisionConflict));

            Result<ContentDefinitionRecord> reRead = _catalogRepository.GetContentDefinition(_campaign, created.Value.ContentDefinitionId, TestCorrelationId);
            Assert.That(reRead.Value.Name, Is.EqualTo(created.Value.Name));
            Assert.That(reRead.Value.Revision, Is.EqualTo(created.Value.Revision));
        }

        [Test]
        public void UpdateDraftDefinition_ByNonMainGm_IsRejected_NoStateChange()
        {
            Result<ContentDefinitionRecord> created = ContentCatalogAuthoringService.CreateDraftDefinition(_catalogRepository, NewCreateRequest());
            Assert.That(created.IsSuccess, Is.True);

            var updateRequest = new UpdateDraftDefinitionRequest(_campaign, created.Value.ContentDefinitionId, "Should Not Apply", null, "{}", created.Value.Revision, NewUserId(), actorIsMainGm: false, NewCommandId(), TestCorrelationId);
            Result<ContentDefinitionRecord> updated = ContentCatalogAuthoringService.UpdateDraftDefinition(_catalogRepository, updateRequest);

            Assert.That(updated.IsFailure, Is.True);
            Assert.That(updated.Error.Code, Is.EqualTo(ErrorCodes.ContentCatalogAuthoringDenied));

            Result<ContentDefinitionRecord> reRead = _catalogRepository.GetContentDefinition(_campaign, created.Value.ContentDefinitionId, TestCorrelationId);
            Assert.That(reRead.Value.Name, Is.EqualTo(created.Value.Name), "a denied authoring request must cause no repository state change at all");
        }

        [Test]
        public void UpdateDraftDefinition_OnPublishedOrArchivedDefinition_IsRejected()
        {
            Result<ContentDefinitionRecord> created = ContentCatalogAuthoringService.CreateDraftDefinition(_catalogRepository, NewCreateRequest());
            Assert.That(created.IsSuccess, Is.True);
            MarkStatusDirectly(created.Value.ContentDefinitionId, ContentDefinitionStatus.Published);

            var updateRequest = new UpdateDraftDefinitionRequest(_campaign, created.Value.ContentDefinitionId, "Should Not Apply", null, "{}", created.Value.Revision, NewUserId(), actorIsMainGm: true, NewCommandId(), TestCorrelationId);
            Result<ContentDefinitionRecord> updated = ContentCatalogAuthoringService.UpdateDraftDefinition(_catalogRepository, updateRequest);

            Assert.That(updated.IsFailure, Is.True);
            Assert.That(updated.Error.Code, Is.EqualTo(ErrorCodes.PersistenceContentDefinitionNotDraft));
        }

        [Test]
        public void UpdateDraftDefinition_ReplayOfSameCommandId_DoesNotIncrementRevisionTwice()
        {
            Result<ContentDefinitionRecord> created = ContentCatalogAuthoringService.CreateDraftDefinition(_catalogRepository, NewCreateRequest());
            var updateRequest = new UpdateDraftDefinitionRequest(_campaign, created.Value.ContentDefinitionId, "Renamed Once", null, "{}", created.Value.Revision, NewUserId(), actorIsMainGm: true, NewCommandId(), TestCorrelationId);

            Result<ContentDefinitionRecord> first = ContentCatalogAuthoringService.UpdateDraftDefinition(_catalogRepository, updateRequest);
            Result<ContentDefinitionRecord> replay = ContentCatalogAuthoringService.UpdateDraftDefinition(_catalogRepository, updateRequest);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.Revision, Is.EqualTo(first.Value.Revision), "replaying the same update CommandId must not increment Revision a second time");
        }

        // ---- CreateNextDraftVersionFromPublished ----------------------------------

        [Test]
        public void CreateNextDraftVersionFromPublished_ByMainGm_CopiesSourceFields_AsNewDraftIdentity()
        {
            Result<ContentDefinitionRecord> created = ContentCatalogAuthoringService.CreateDraftDefinition(_catalogRepository, NewCreateRequest(name: "Longsword"));
            Assert.That(created.IsSuccess, Is.True);
            MarkStatusDirectly(created.Value.ContentDefinitionId, ContentDefinitionStatus.Published);

            var nextRequest = new CreateNextDraftVersionFromPublishedRequest(_campaign, created.Value.ContentDefinitionId, NewUserId(), actorIsMainGm: true, NewCommandId(), TestCorrelationId);
            Result<ContentDefinitionRecord> nextDraft = ContentCatalogAuthoringService.CreateNextDraftVersionFromPublished(_catalogRepository, nextRequest);

            Assert.That(nextDraft.IsSuccess, Is.True);
            Assert.That(nextDraft.Value.ContentDefinitionId, Is.Not.EqualTo(created.Value.ContentDefinitionId), "the next Draft version must have its own new ContentDefinitionId");
            Assert.That(nextDraft.Value.Status, Is.EqualTo(ContentDefinitionStatus.Draft));
            Assert.That(nextDraft.Value.Version, Is.EqualTo(0));
            Assert.That(nextDraft.Value.Revision, Is.EqualTo(1));
            Assert.That(nextDraft.Value.Name, Is.EqualTo("Longsword"), "the new Draft must copy the Published source's own fields as its starting point");
            Assert.That(nextDraft.Value.DefinitionType, Is.EqualTo(created.Value.DefinitionType));
        }

        [Test]
        public void CreateNextDraftVersionFromPublished_DoesNotMutatePublishedSource()
        {
            Result<ContentDefinitionRecord> created = ContentCatalogAuthoringService.CreateDraftDefinition(_catalogRepository, NewCreateRequest(name: "Longsword"));
            MarkStatusDirectly(created.Value.ContentDefinitionId, ContentDefinitionStatus.Published);

            var nextRequest = new CreateNextDraftVersionFromPublishedRequest(_campaign, created.Value.ContentDefinitionId, NewUserId(), actorIsMainGm: true, NewCommandId(), TestCorrelationId);
            Result<ContentDefinitionRecord> nextDraft = ContentCatalogAuthoringService.CreateNextDraftVersionFromPublished(_catalogRepository, nextRequest);
            Assert.That(nextDraft.IsSuccess, Is.True);

            Result<ContentDefinitionRecord> sourceAfter = _catalogRepository.GetContentDefinition(_campaign, created.Value.ContentDefinitionId, TestCorrelationId);
            Assert.That(sourceAfter.IsSuccess, Is.True);
            Assert.That(sourceAfter.Value.Status, Is.EqualTo(ContentDefinitionStatus.Published), "the Published source must remain Published, never edited in place");
            Assert.That(sourceAfter.Value.Name, Is.EqualTo("Longsword"));
            Assert.That(sourceAfter.Value.Revision, Is.EqualTo(created.Value.Revision), "the Published source's own Revision must be untouched by branching a next Draft version from it");
        }

        [Test]
        public void CreateNextDraftVersionFromPublished_ByNonMainGm_IsRejected_NoStateChange()
        {
            Result<ContentDefinitionRecord> created = ContentCatalogAuthoringService.CreateDraftDefinition(_catalogRepository, NewCreateRequest());
            MarkStatusDirectly(created.Value.ContentDefinitionId, ContentDefinitionStatus.Published);

            var nextRequest = new CreateNextDraftVersionFromPublishedRequest(_campaign, created.Value.ContentDefinitionId, NewUserId(), actorIsMainGm: false, NewCommandId(), TestCorrelationId);
            Result<ContentDefinitionRecord> nextDraft = ContentCatalogAuthoringService.CreateNextDraftVersionFromPublished(_catalogRepository, nextRequest);

            Assert.That(nextDraft.IsFailure, Is.True);
            Assert.That(nextDraft.Error.Code, Is.EqualTo(ErrorCodes.ContentCatalogAuthoringDenied));

            Result<IReadOnlyList<ContentDefinitionRecord>> all = _catalogRepository.ListContentDefinitions(_campaign, null, TestCorrelationId);
            Assert.That(all.Value.Count, Is.EqualTo(1), "a denied create-next-draft request must not create any new row");
        }

        [Test]
        public void CreateNextDraftVersionFromPublished_OnNonPublishedSource_IsRejected()
        {
            Result<ContentDefinitionRecord> created = ContentCatalogAuthoringService.CreateDraftDefinition(_catalogRepository, NewCreateRequest());
            Assert.That(created.IsSuccess, Is.True);

            var nextRequest = new CreateNextDraftVersionFromPublishedRequest(_campaign, created.Value.ContentDefinitionId, NewUserId(), actorIsMainGm: true, NewCommandId(), TestCorrelationId);
            Result<ContentDefinitionRecord> nextDraft = ContentCatalogAuthoringService.CreateNextDraftVersionFromPublished(_catalogRepository, nextRequest);

            Assert.That(nextDraft.IsFailure, Is.True);
            Assert.That(nextDraft.Error.Code, Is.EqualTo(ErrorCodes.PersistenceContentDefinitionNotPublished));
        }

        [Test]
        public void CreateNextDraftVersionFromPublished_ReplayOfSameCommandId_IsIdempotent()
        {
            Result<ContentDefinitionRecord> created = ContentCatalogAuthoringService.CreateDraftDefinition(_catalogRepository, NewCreateRequest());
            MarkStatusDirectly(created.Value.ContentDefinitionId, ContentDefinitionStatus.Published);

            var nextRequest = new CreateNextDraftVersionFromPublishedRequest(_campaign, created.Value.ContentDefinitionId, NewUserId(), actorIsMainGm: true, NewCommandId(), TestCorrelationId);

            Result<ContentDefinitionRecord> first = ContentCatalogAuthoringService.CreateNextDraftVersionFromPublished(_catalogRepository, nextRequest);
            Result<ContentDefinitionRecord> replay = ContentCatalogAuthoringService.CreateNextDraftVersionFromPublished(_catalogRepository, nextRequest);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.ContentDefinitionId, Is.EqualTo(first.Value.ContentDefinitionId));

            Result<IReadOnlyList<ContentDefinitionRecord>> all = _catalogRepository.ListContentDefinitions(_campaign, null, TestCorrelationId);
            Assert.That(all.Value.Count, Is.EqualTo(2), "exactly one Published source plus one Draft copy -- a replayed create-next-draft must not mint a second copy");
        }

        // ---- No runtime item/inventory/equipment/effect implementation slipped in ----

        [Test]
        public void AuthoringLayer_IntroducesNoRuntimeItemInventoryEquipmentOrActiveEffectType()
        {
            // Direct proof, at the assembly level, that this task's own new
            // Application-layer authoring surface references no
            // Inventory/ItemInstance/ItemStack/Equipment/ActiveEffect type.
            // ContentCatalogAuthoringService/its request types live in
            // Odyssey.Application.Content -- inspected here by name, not by
            // behavior, to catch anything that would compile but shouldn't
            // exist in this task's own scope.
            System.Reflection.Assembly applicationAssembly = typeof(ContentCatalogAuthoringService).Assembly;
            var contentNamespaceTypes = applicationAssembly.GetTypes()
                .Where(t => t.Namespace == "Odyssey.Application.Content")
                .ToArray();

            string[] forbiddenSubstrings = { "Inventory", "ItemInstance", "ItemStack", "Equipment", "ActiveEffect" };
            foreach (Type type in contentNamespaceTypes)
            {
                foreach (string forbidden in forbiddenSubstrings)
                {
                    Assert.That(type.Name, Does.Not.Contain(forbidden), $"type '{type.Name}' in Odyssey.Application.Content must not reference runtime item/inventory/equipment/effect state ('{forbidden}')");
                }
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
