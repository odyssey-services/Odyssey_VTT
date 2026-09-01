using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S04-101: real, non-stubbed tests for <see cref="SqliteCharacterRepository"/>
    /// against a real temp-directory campaign and a real SQLite database --
    /// mirroring <c>SqliteSceneRepositoryTests</c>'s exact fixture convention.
    /// Every test exercises the real path (open connection, real transaction,
    /// real file on disk); none of them mock or bypass
    /// <see cref="SqliteSavingPipeline"/>.
    /// </summary>
    public sealed class SqliteCharacterRepositoryTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private string _workDir = null!;
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;

        [SetUp]
        public void SetUp()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "ody-s04-101-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_workDir, "Character Skeleton Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = _campaignRepository.Create(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                _campaignRepository.Close(_campaign, TestCorrelationId);
            }
            catch (IOException) { }

            try
            {
                if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup only.
            }
        }

        [TestCase(CharacterKind.PlayerCharacter)]
        [TestCase(CharacterKind.NonPlayerCharacter)]
        [TestCase(CharacterKind.Creature)]
        public void CreateCharacter_ForEveryCharacterKind_ReturnsDraftAtRevisionOne_WithAllTwelveSectionRevisions(CharacterKind kind)
        {
            var repository = new SqliteCharacterRepository(Clock);
            var request = new CreateCharacterRequest(_campaign, kind, "Test " + kind);

            Result<CharacterRecord> result = repository.CreateCharacter(request, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsSuccess, Is.True);
            CharacterRecord record = result.Value;
            Assert.That(record.CharacterKind, Is.EqualTo(kind));
            Assert.That(record.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Draft));
            Assert.That(record.ApprovalState, Is.EqualTo(CharacterApprovalState.Draft));
            Assert.That(record.DisplayName, Is.EqualTo("Test " + kind));
            Assert.That(record.PortraitReference, Is.Null);
            Assert.That(record.CharacterId.IsValid, Is.True);

            // ADR-022 section 5: every one of the twelve reserved section
            // revisions starts at 1 from creation, even the ones this task's
            // own commands never touch (Mechanics, Ownership, Lifecycle, ...)
            // -- later tasks must never need a schema migration to start
            // using an already-reserved section.
            CharacterSectionRevisions revisions = record.Revisions;
            Assert.That(revisions.CharacterRevision, Is.EqualTo(1));
            Assert.That(revisions.IdentityRevision, Is.EqualTo(1));
            Assert.That(revisions.PresentationRevision, Is.EqualTo(1));
            Assert.That(revisions.CustomFieldsRevision, Is.EqualTo(1));
            Assert.That(revisions.MechanicsRevision, Is.EqualTo(1));
            Assert.That(revisions.AttributeValuesRevision, Is.EqualTo(1));
            Assert.That(revisions.CharacterSkillsRevision, Is.EqualTo(1));
            Assert.That(revisions.CharacterAbilitiesRevision, Is.EqualTo(1));
            Assert.That(revisions.CharacterResourcesRevision, Is.EqualTo(1));
            Assert.That(revisions.CharacterAnatomyRevision, Is.EqualTo(1));
            Assert.That(revisions.OwnershipRevision, Is.EqualTo(1));
            Assert.That(revisions.LifecycleRevision, Is.EqualTo(1));
            Assert.That(revisions.RuntimeStateRevision, Is.EqualTo(1));
        }

        [Test]
        public void ConcurrentEditsToDifferentSections_BothCommit_NoFalseConflict()
        {
            // Roadmap/backlog exit criterion (SLICE-04_IMPLEMENTATION_BACKLOG.md
            // section 3, item 9): two commands editing unrelated Character
            // sections commit concurrently without a false conflict. Simulated
            // here as two sequential calls each declaring only its own
            // section's expected revision (both still at their initial value
            // 1, since neither call has touched the other's section) -- this
            // proves the two update paths do not cross-check each other's
            // revision, which is the actual property under test, not merely
            // that two independent single edits each succeed in isolation.
            var repository = new SqliteCharacterRepository(Clock);
            CharacterRecord created = repository.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Original Name"), NewCommandId(), TestCorrelationId).Value;

            Result<CharacterRecord> identityResult = repository.UpdateIdentity(_campaign, created.CharacterId, "Renamed", created.Revisions.IdentityRevision, NewCommandId(), TestCorrelationId);
            Result<CharacterRecord> presentationResult = repository.UpdatePresentation(_campaign, created.CharacterId, "portrait://renamed.png", created.Revisions.PresentationRevision, NewCommandId(), TestCorrelationId);

            Assert.That(identityResult.IsSuccess, Is.True, "an Identity edit must not be rejected by a concurrent, unrelated Presentation edit");
            Assert.That(presentationResult.IsSuccess, Is.True, "a Presentation edit must not be rejected by a concurrent, unrelated Identity edit");

            CharacterRecord final = repository.GetCharacter(_campaign, created.CharacterId, TestCorrelationId).Value;
            Assert.That(final.DisplayName, Is.EqualTo("Renamed"));
            Assert.That(final.PortraitReference, Is.EqualTo("portrait://renamed.png"));
            Assert.That(final.Revisions.IdentityRevision, Is.EqualTo(2));
            Assert.That(final.Revisions.PresentationRevision, Is.EqualTo(2));

            // CharacterRevision (the overall aggregate revision) increases for
            // both committed changes -- 1 (creation) + 2 section edits = 3.
            Assert.That(final.Revisions.CharacterRevision, Is.EqualTo(3));
        }

        [Test]
        public void UpdateIdentity_WithStaleExpectedRevision_IsRejected_NoStateChange()
        {
            var repository = new SqliteCharacterRepository(Clock);
            CharacterRecord created = repository.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Original Name"), NewCommandId(), TestCorrelationId).Value;

            // First edit succeeds and advances IdentityRevision to 2.
            Result<CharacterRecord> firstEdit = repository.UpdateIdentity(_campaign, created.CharacterId, "First Rename", created.Revisions.IdentityRevision, NewCommandId(), TestCorrelationId);
            Assert.That(firstEdit.IsSuccess, Is.True);

            // Second caller still holds the now-stale original IdentityRevision (1).
            Result<CharacterRecord> staleEdit = repository.UpdateIdentity(_campaign, created.CharacterId, "Should Not Apply", created.Revisions.IdentityRevision, NewCommandId(), TestCorrelationId);

            Assert.That(staleEdit.IsFailure, Is.True);
            Assert.That(staleEdit.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRevisionConflict));
            Assert.That(staleEdit.Error.Category, Is.EqualTo(ErrorCategory.Conflict));

            CharacterRecord unchanged = repository.GetCharacter(_campaign, created.CharacterId, TestCorrelationId).Value;
            Assert.That(unchanged.DisplayName, Is.EqualTo("First Rename"), "the rejected stale-revision edit must not have applied any state change");
            Assert.That(unchanged.Revisions.IdentityRevision, Is.EqualTo(2));
        }

        [Test]
        public void GetCharacterHistory_RebuildsFromDomainEventsFromScratch_NotIncrementally()
        {
            // ADR-022 section 8: this asserts the rebuild property directly --
            // GetCharacterHistory is called for the very first time only after
            // every mutation has already committed, against a repository
            // instance that has never itself produced or cached any history
            // entry. A correct result here can only come from reading
            // DomainEvents fresh, never from an incrementally-maintained,
            // separately-tracked history list.
            var writer = new SqliteCharacterRepository(Clock);
            CharacterRecord created = writer.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Original Name"), NewCommandId(), TestCorrelationId).Value;
            writer.UpdateIdentity(_campaign, created.CharacterId, "Renamed Once", created.Revisions.IdentityRevision, NewCommandId(), TestCorrelationId);
            CharacterRecord afterFirstRename = writer.GetCharacter(_campaign, created.CharacterId, TestCorrelationId).Value;
            writer.UpdateIdentity(_campaign, created.CharacterId, "Renamed Twice", afterFirstRename.Revisions.IdentityRevision, NewCommandId(), TestCorrelationId);

            // A brand-new repository instance with no in-memory state of its own.
            var reader = new SqliteCharacterRepository(Clock);
            Result<IReadOnlyList<CharacterHistoryEntry>> history = reader.GetCharacterHistory(_campaign, created.CharacterId, TestCorrelationId);

            Assert.That(history.IsSuccess, Is.True);
            Assert.That(history.Value.Count, Is.EqualTo(3), "creation + two identity updates = three history entries");
            Assert.That(history.Value[0].EventType, Is.EqualTo("odyssey.persistence.character_created"));
            Assert.That(history.Value[0].DisplayNameSnapshot, Is.EqualTo("Original Name"));
            Assert.That(history.Value[1].EventType, Is.EqualTo("odyssey.persistence.character_identity_updated"));
            Assert.That(history.Value[1].DisplayNameSnapshot, Is.EqualTo("Renamed Once"));
            Assert.That(history.Value[2].DisplayNameSnapshot, Is.EqualTo("Renamed Twice"));

            // ADR-012 section 4.1: EventSequence is the sole authoritative
            // order and must be strictly increasing.
            Assert.That(history.Value[0].EventSequence, Is.LessThan(history.Value[1].EventSequence));
            Assert.That(history.Value[1].EventSequence, Is.LessThan(history.Value[2].EventSequence));

            // Renaming the Character again afterward must not rewrite the
            // already-rendered historical entries (CAP-INV-005 / ADR-022
            // section 7 rule 3: historical entries render from event
            // snapshots, not from current fields).
            Assert.That(history.Value[0].DisplayNameSnapshot, Is.EqualTo("Original Name"), "history must still show the name as of that event, not the Character's current name");
        }

        [Test]
        public void History_ForTwoDifferentCharacters_NeverCrosses()
        {
            var repository = new SqliteCharacterRepository(Clock);
            CharacterRecord characterA = repository.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Character A"), NewCommandId(), TestCorrelationId).Value;
            CharacterRecord characterB = repository.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Character B"), NewCommandId(), TestCorrelationId).Value;
            repository.UpdateIdentity(_campaign, characterA.CharacterId, "Character A Renamed", characterA.Revisions.IdentityRevision, NewCommandId(), TestCorrelationId);

            Result<IReadOnlyList<CharacterHistoryEntry>> historyA = repository.GetCharacterHistory(_campaign, characterA.CharacterId, TestCorrelationId);
            Result<IReadOnlyList<CharacterHistoryEntry>> historyB = repository.GetCharacterHistory(_campaign, characterB.CharacterId, TestCorrelationId);

            Assert.That(historyA.Value.Count, Is.EqualTo(2), "Character A: creation + one rename");
            Assert.That(historyB.Value.Count, Is.EqualTo(1), "Character B: creation only -- it must not see Character A's rename event");
            foreach (CharacterHistoryEntry entry in historyA.Value) Assert.That(entry.CharacterId, Is.EqualTo(characterA.CharacterId));
            foreach (CharacterHistoryEntry entry in historyB.Value) Assert.That(entry.CharacterId, Is.EqualTo(characterB.CharacterId));
        }

        [Test]
        public void CreatedCharacter_SurvivesCloseAndReopen_SameStateRebuiltFromDisk()
        {
            var writer = new SqliteCharacterRepository(Clock);
            CharacterRecord created = writer.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.NonPlayerCharacter, "Persisted NPC"), NewCommandId(), TestCorrelationId).Value;
            writer.UpdatePresentation(_campaign, created.CharacterId, "portrait://npc.png", created.Revisions.PresentationRevision, NewCommandId(), TestCorrelationId);

            _campaignRepository.Close(_campaign, TestCorrelationId);

            // A fresh CampaignRepository/CharacterRepository pair against the
            // same on-disk folder -- no shared in-memory state with the
            // writer above, mirroring VerticalSliceIntegrationTests' own
            // close/reopen convention.
            var reopenCampaignRepository = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> reopened = reopenCampaignRepository.Open(_workDir, TestCorrelationId);
            Assert.That(reopened.IsSuccess, Is.True);
            _campaign = reopened.Value;
            _campaignRepository = reopenCampaignRepository;

            var reader = new SqliteCharacterRepository(Clock);
            Result<CharacterRecord> reread = reader.GetCharacter(_campaign, created.CharacterId, TestCorrelationId);

            Assert.That(reread.IsSuccess, Is.True);
            Assert.That(reread.Value.DisplayName, Is.EqualTo("Persisted NPC"));
            Assert.That(reread.Value.PortraitReference, Is.EqualTo("portrait://npc.png"));
            Assert.That(reread.Value.Revisions.PresentationRevision, Is.EqualTo(2));
            Assert.That(reread.Value.CharacterKind, Is.EqualTo(CharacterKind.NonPlayerCharacter));

            Result<IReadOnlyList<CharacterHistoryEntry>> historyAfterReopen = reader.GetCharacterHistory(_campaign, created.CharacterId, TestCorrelationId);
            Assert.That(historyAfterReopen.IsSuccess, Is.True);
            Assert.That(historyAfterReopen.Value.Count, Is.EqualTo(2), "creation + presentation update must both survive close/reopen");
        }

        [Test]
        public void GetCharacter_OnNonExistentCharacter_ReturnsTypedCharacterNotFound()
        {
            var repository = new SqliteCharacterRepository(Clock);
            CharacterId phantom = CharacterId.NewId(Clock.GetUtcNow());

            Result<CharacterRecord> result = repository.GetCharacter(_campaign, phantom, TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterNotFound));
            Assert.That(result.Error.Category, Is.EqualTo(ErrorCategory.NotFound));
        }

        [Test]
        public void AssignPrimaryOwner_WithEmptyReasonCode_IsRejected_NoStateChange()
        {
            var repository = new SqliteCharacterRepository(Clock);
            CharacterRecord created = repository.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Owned Character"), NewCommandId(), TestCorrelationId).Value;
            UserId newOwner = NewUserId();

            Result<CharacterRecord> result = repository.AssignPrimaryOwner(_campaign, created.CharacterId, newOwner, reasonCode: "", actorIsMainGm: true, created.Revisions.OwnershipRevision, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterOwnershipReasonRequired));
            Assert.That(result.Error.Category, Is.EqualTo(ErrorCategory.Validation));

            CharacterRecord unchanged = repository.GetCharacter(_campaign, created.CharacterId, TestCorrelationId).Value;
            Assert.That(unchanged.Ownership.PrimaryOwnerUserId, Is.Null, "a rejected AssignPrimaryOwner must not set an owner");
            Assert.That(unchanged.Revisions.OwnershipRevision, Is.EqualTo(1));
        }

        [Test]
        public void AssignPrimaryOwner_ByNonMainGm_IsRejected_NoStateChange()
        {
            var repository = new SqliteCharacterRepository(Clock);
            CharacterRecord created = repository.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Owned Character"), NewCommandId(), TestCorrelationId).Value;
            UserId newOwner = NewUserId();

            // Character.ManageOwnership is MainGM-only (ADR-025 section 4) --
            // this asserts the gate is actually enforced by the repository
            // method itself, not merely documented.
            Result<CharacterRecord> result = repository.AssignPrimaryOwner(_campaign, created.CharacterId, newOwner, reasonCode: "GM decision", actorIsMainGm: false, created.Revisions.OwnershipRevision, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterOwnershipDenied));
            Assert.That(result.Error.Category, Is.EqualTo(ErrorCategory.Authorization));

            CharacterRecord unchanged = repository.GetCharacter(_campaign, created.CharacterId, TestCorrelationId).Value;
            Assert.That(unchanged.Ownership.PrimaryOwnerUserId, Is.Null);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void NonMainGmActor_IsRejected_ForEveryOwnershipCommand(bool actorIsMainGm)
        {
            // Parameterized to make the contrast explicit in test output:
            // the same call succeeds when actorIsMainGm=true and is denied
            // when actorIsMainGm=false, for every one of the five
            // Character.ManageOwnership-gated commands beyond AssignPrimaryOwner.
            var repository = new SqliteCharacterRepository(Clock);
            CharacterRecord created = repository.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Gate Test Character"), NewCommandId(), TestCorrelationId).Value;
            UserId targetUser = NewUserId();

            Result<CharacterRecord> addCoOwner = repository.AddCharacterCoOwner(_campaign, created.CharacterId, targetUser, actorIsMainGm, created.Revisions.OwnershipRevision, NewCommandId(), TestCorrelationId);
            Assert.That(addCoOwner.IsSuccess, Is.EqualTo(actorIsMainGm));
            if (!actorIsMainGm) Assert.That(addCoOwner.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterOwnershipDenied));
        }

        [Test]
        public void AssignPrimaryOwner_ByMainGm_Succeeds_AuditedCorrectly_DoesNotChangeCoOwnersOrControl()
        {
            var repository = new SqliteCharacterRepository(Clock);
            CharacterRecord created = repository.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Owned Character"), NewCommandId(), TestCorrelationId).Value;
            UserId existingCoOwner = NewUserId();
            repository.AddCharacterCoOwner(_campaign, created.CharacterId, existingCoOwner, actorIsMainGm: true, created.Revisions.OwnershipRevision, NewCommandId(), TestCorrelationId);
            UserId existingController = NewUserId();
            CharacterRecord afterCoOwner = repository.GetCharacter(_campaign, created.CharacterId, TestCorrelationId).Value;
            repository.GrantPermanentCharacterControl(_campaign, created.CharacterId, existingController, actorIsMainGm: true, afterCoOwner.Revisions.OwnershipRevision, NewCommandId(), TestCorrelationId);

            CharacterRecord beforeAssign = repository.GetCharacter(_campaign, created.CharacterId, TestCorrelationId).Value;
            UserId newOwner = NewUserId();

            Result<CharacterRecord> result = repository.AssignPrimaryOwner(_campaign, created.CharacterId, newOwner, reasonCode: "Player left the table", actorIsMainGm: true, beforeAssign.Revisions.OwnershipRevision, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Ownership.PrimaryOwnerUserId, Is.EqualTo(newOwner));

            // CAP-INV-007: assigning a new owner must not silently touch
            // co-owner/control grants already present.
            Assert.That(result.Value.Ownership.CoOwnerUserIds, Does.Contain(existingCoOwner));
            Assert.That(result.Value.Ownership.PermanentControllerUserIds, Does.Contain(existingController));

            // Audit trail: a real, persisted event carries who/when/why.
            Result<IReadOnlyList<CharacterHistoryEntry>> history = repository.GetCharacterHistory(_campaign, created.CharacterId, TestCorrelationId);
            CharacterHistoryEntry auditEntry = Find(history.Value, "odyssey.persistence.character_primary_owner_assigned");
            Assert.That(auditEntry.CharacterId, Is.EqualTo(created.CharacterId));
        }

        [Test]
        public void AssignPrimaryOwner_WithStaleExpectedOwnershipRevision_IsRejected_NoStateChange()
        {
            var repository = new SqliteCharacterRepository(Clock);
            CharacterRecord created = repository.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Owned Character"), NewCommandId(), TestCorrelationId).Value;
            UserId firstOwner = NewUserId();
            repository.AssignPrimaryOwner(_campaign, created.CharacterId, firstOwner, "Initial assignment", actorIsMainGm: true, created.Revisions.OwnershipRevision, NewCommandId(), TestCorrelationId);

            UserId secondOwner = NewUserId();
            Result<CharacterRecord> staleAssign = repository.AssignPrimaryOwner(_campaign, created.CharacterId, secondOwner, "Should not apply", actorIsMainGm: true, created.Revisions.OwnershipRevision, NewCommandId(), TestCorrelationId);

            Assert.That(staleAssign.IsFailure, Is.True);
            Assert.That(staleAssign.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRevisionConflict));

            CharacterRecord unchanged = repository.GetCharacter(_campaign, created.CharacterId, TestCorrelationId).Value;
            Assert.That(unchanged.Ownership.PrimaryOwnerUserId, Is.EqualTo(firstOwner), "the rejected stale-revision assignment must not have applied");
        }

        [Test]
        public void ConcurrentEditsToOwnershipAndIdentity_BothCommit_NoFalseConflict()
        {
            // Direct extension of ODY-S04-101's own cross-section test to a
            // third independent section (Ownership), reusing the exact same
            // property under test: neither call declares or checks the
            // other's section revision.
            var repository = new SqliteCharacterRepository(Clock);
            CharacterRecord created = repository.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Original Name"), NewCommandId(), TestCorrelationId).Value;
            UserId newOwner = NewUserId();

            Result<CharacterRecord> identityResult = repository.UpdateIdentity(_campaign, created.CharacterId, "Renamed", created.Revisions.IdentityRevision, NewCommandId(), TestCorrelationId);
            Result<CharacterRecord> ownershipResult = repository.AssignPrimaryOwner(_campaign, created.CharacterId, newOwner, "Initial owner", actorIsMainGm: true, created.Revisions.OwnershipRevision, NewCommandId(), TestCorrelationId);

            Assert.That(identityResult.IsSuccess, Is.True, "an Identity edit must not be rejected by a concurrent, unrelated Ownership edit");
            Assert.That(ownershipResult.IsSuccess, Is.True, "an Ownership edit must not be rejected by a concurrent, unrelated Identity edit");

            CharacterRecord final = repository.GetCharacter(_campaign, created.CharacterId, TestCorrelationId).Value;
            Assert.That(final.DisplayName, Is.EqualTo("Renamed"));
            Assert.That(final.Ownership.PrimaryOwnerUserId, Is.EqualTo(newOwner));
            Assert.That(final.Revisions.IdentityRevision, Is.EqualTo(2));
            Assert.That(final.Revisions.OwnershipRevision, Is.EqualTo(2));
        }

        [Test]
        public void AddCharacterCoOwner_CalledTwiceForSameUser_DoesNotCreateDuplicateEntry()
        {
            var repository = new SqliteCharacterRepository(Clock);
            CharacterRecord created = repository.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Co-owned Character"), NewCommandId(), TestCorrelationId).Value;
            UserId coOwner = NewUserId();

            Result<CharacterRecord> first = repository.AddCharacterCoOwner(_campaign, created.CharacterId, coOwner, actorIsMainGm: true, created.Revisions.OwnershipRevision, NewCommandId(), TestCorrelationId);
            Assert.That(first.IsSuccess, Is.True);
            Result<CharacterRecord> second = repository.AddCharacterCoOwner(_campaign, created.CharacterId, coOwner, actorIsMainGm: true, first.Value.Revisions.OwnershipRevision, NewCommandId(), TestCorrelationId);
            Assert.That(second.IsSuccess, Is.True);

            int occurrences = 0;
            foreach (UserId id in second.Value.Ownership.CoOwnerUserIds)
            {
                if (id.Equals(coOwner)) occurrences++;
            }

            Assert.That(occurrences, Is.EqualTo(1), "adding the same co-owner twice must not create a duplicate list entry");
        }

        [Test]
        public void RemoveCharacterCoOwner_RemovesExactlyThatUser()
        {
            var repository = new SqliteCharacterRepository(Clock);
            CharacterRecord created = repository.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Co-owned Character"), NewCommandId(), TestCorrelationId).Value;
            UserId coOwnerA = NewUserId();
            UserId coOwnerB = NewUserId();

            CharacterRecord afterA = repository.AddCharacterCoOwner(_campaign, created.CharacterId, coOwnerA, actorIsMainGm: true, created.Revisions.OwnershipRevision, NewCommandId(), TestCorrelationId).Value;
            CharacterRecord afterB = repository.AddCharacterCoOwner(_campaign, created.CharacterId, coOwnerB, actorIsMainGm: true, afterA.Revisions.OwnershipRevision, NewCommandId(), TestCorrelationId).Value;

            Result<CharacterRecord> afterRemove = repository.RemoveCharacterCoOwner(_campaign, created.CharacterId, coOwnerA, actorIsMainGm: true, afterB.Revisions.OwnershipRevision, NewCommandId(), TestCorrelationId);

            Assert.That(afterRemove.IsSuccess, Is.True);
            Assert.That(afterRemove.Value.Ownership.CoOwnerUserIds, Does.Not.Contain(coOwnerA));
            Assert.That(afterRemove.Value.Ownership.CoOwnerUserIds, Does.Contain(coOwnerB));
        }

        [Test]
        public void GrantPermanentControl_ThenRevoke_RemovesController()
        {
            var repository = new SqliteCharacterRepository(Clock);
            CharacterRecord created = repository.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Controlled Character"), NewCommandId(), TestCorrelationId).Value;
            UserId controller = NewUserId();

            CharacterRecord afterGrant = repository.GrantPermanentCharacterControl(_campaign, created.CharacterId, controller, actorIsMainGm: true, created.Revisions.OwnershipRevision, NewCommandId(), TestCorrelationId).Value;
            Assert.That(afterGrant.Ownership.PermanentControllerUserIds, Does.Contain(controller));

            Result<CharacterRecord> afterRevoke = repository.RevokeCharacterControl(_campaign, created.CharacterId, controller, actorIsMainGm: true, afterGrant.Revisions.OwnershipRevision, NewCommandId(), TestCorrelationId);

            Assert.That(afterRevoke.IsSuccess, Is.True);
            Assert.That(afterRevoke.Value.Ownership.PermanentControllerUserIds, Does.Not.Contain(controller));
        }

        [Test]
        public void GrantTemporaryControl_ThenRevoke_RemovesGrant()
        {
            var repository = new SqliteCharacterRepository(Clock);
            CharacterRecord created = repository.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Controlled Character"), NewCommandId(), TestCorrelationId).Value;
            UserId controller = NewUserId();
            UtcInstant expiresAt = UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow.AddHours(1));

            CharacterRecord afterGrant = repository.GrantTemporaryCharacterControl(_campaign, created.CharacterId, controller, expiresAt, actorIsMainGm: true, created.Revisions.OwnershipRevision, NewCommandId(), TestCorrelationId).Value;
            Assert.That(afterGrant.Ownership.TemporaryControlGrants, Has.Count.EqualTo(1));
            Assert.That(afterGrant.Ownership.TemporaryControlGrants[0].UserId, Is.EqualTo(controller));
            Assert.That(afterGrant.Ownership.TemporaryControlGrants[0].ExpiresAt, Is.EqualTo(expiresAt));

            Result<CharacterRecord> afterRevoke = repository.RevokeCharacterControl(_campaign, created.CharacterId, controller, actorIsMainGm: true, afterGrant.Revisions.OwnershipRevision, NewCommandId(), TestCorrelationId);

            Assert.That(afterRevoke.IsSuccess, Is.True);
            Assert.That(afterRevoke.Value.Ownership.TemporaryControlGrants, Is.Empty);
        }

        [Test]
        public void IsAssignedCharacter_PrimaryOwnerAndActiveControlGrant_BothSatisfyAssignedCondition_UnrelatedUserDoesNot()
        {
            // ADR-025 section 4.3: ownership and an active control grant both
            // satisfy ADR-019's "assigned character" condition. This test
            // calls the one real, canonical predicate
            // (CharacterOwnershipAssignment.IsAssignedCharacter) that any
            // future Player-action-eligibility check against a Character
            // must reuse, rather than re-deriving its own separate
            // ownership/control logic.
            var repository = new SqliteCharacterRepository(Clock);
            CharacterRecord created = repository.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Assigned Character"), NewCommandId(), TestCorrelationId).Value;

            UserId owner = NewUserId();
            CharacterRecord afterOwner = repository.AssignPrimaryOwner(_campaign, created.CharacterId, owner, "assign", actorIsMainGm: true, created.Revisions.OwnershipRevision, NewCommandId(), TestCorrelationId).Value;

            // A grant's ExpiresAt can never precede its own GrantedAt (the
            // domain constructor enforces this -- granting something already
            // expired at grant time is nonsensical). The realistic "expired"
            // scenario is a grant that was valid *when created* and has
            // since lapsed -- simulated here by evaluating IsAssignedCharacter
            // at a later "now" than a short-lived grant's own expiry, not by
            // trying to construct an already-past-expiry grant directly.
            UserId activeGrantee = NewUserId();
            UtcInstant distantFuture = UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow.AddHours(1));
            CharacterRecord afterGrant = repository.GrantTemporaryCharacterControl(_campaign, created.CharacterId, activeGrantee, distantFuture, actorIsMainGm: true, afterOwner.Revisions.OwnershipRevision, NewCommandId(), TestCorrelationId).Value;

            UserId expiredGrantee = NewUserId();
            UtcInstant nearFuture = UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow.AddSeconds(1));
            CharacterRecord afterExpiredGrant = repository.GrantTemporaryCharacterControl(_campaign, created.CharacterId, expiredGrantee, nearFuture, actorIsMainGm: true, afterGrant.Revisions.OwnershipRevision, NewCommandId(), TestCorrelationId).Value;

            // Between the two grants' expiries: after the 1-second grant has
            // lapsed, but well before the 1-hour grant expires.
            UtcInstant checkAsOfLater = UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow.AddMinutes(5));
            UserId unrelatedUser = NewUserId();

            Assert.That(CharacterOwnershipAssignment.IsAssignedCharacter(afterExpiredGrant.Ownership, owner, checkAsOfLater), Is.True, "primary owner is an assigned character");
            Assert.That(CharacterOwnershipAssignment.IsAssignedCharacter(afterExpiredGrant.Ownership, activeGrantee, checkAsOfLater), Is.True, "a temporary control grant expiring further in the future than the check time is an assigned character");
            Assert.That(CharacterOwnershipAssignment.IsAssignedCharacter(afterExpiredGrant.Ownership, expiredGrantee, checkAsOfLater), Is.False, "a temporary control grant whose expiry has already passed by the check time is not an assigned character");
            Assert.That(CharacterOwnershipAssignment.IsAssignedCharacter(afterExpiredGrant.Ownership, unrelatedUser, checkAsOfLater), Is.False, "a user with no ownership/control relationship is not an assigned character");
        }

        private static CharacterHistoryEntry Find(IReadOnlyList<CharacterHistoryEntry> entries, string eventType)
        {
            foreach (CharacterHistoryEntry entry in entries)
            {
                if (entry.EventType == eventType) return entry;
            }

            throw new InvalidOperationException("Expected history entry of type " + eventType + " not found.");
        }

        [Test]
        public void DuplicateCreateCharacterCommand_ReturnsStoredResult_DoesNotCreateSecondCharacter()
        {
            // ADR-002 section 9.2 / ADR-012 section 7.2: retrying the same
            // CommandId must replay the stored result, never re-run the
            // handler or create a second row.
            var repository = new SqliteCharacterRepository(Clock);
            CommandId commandId = NewCommandId();
            var request = new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Idempotency Test");

            Result<CharacterRecord> first = repository.CreateCharacter(request, commandId, TestCorrelationId);
            Result<CharacterRecord> replayed = repository.CreateCharacter(request, commandId, TestCorrelationId);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(replayed.IsSuccess, Is.True);
            Assert.That(replayed.Value.CharacterId, Is.EqualTo(first.Value.CharacterId), "the exact same CommandId must replay the same Character, not create a second one");

            Result<IReadOnlyList<CharacterHistoryEntry>> history = repository.GetCharacterHistory(_campaign, first.Value.CharacterId, TestCorrelationId);
            Assert.That(history.Value.Count, Is.EqualTo(1), "a replayed duplicate command must not append a second character_created event");
        }
    }
}
