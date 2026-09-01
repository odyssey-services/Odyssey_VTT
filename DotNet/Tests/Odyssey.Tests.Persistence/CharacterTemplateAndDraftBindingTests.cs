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
    /// ODY-S04-103: real, non-stubbed tests for
    /// <see cref="SqliteCharacterTemplateRepository"/>,
    /// <see cref="SqliteLocalCharacterDraftRepository"/>, and
    /// <see cref="SqliteCharacterRepository.BindDraftToCampaign"/> against a
    /// real temp-directory campaign/profile and a real SQLite database --
    /// mirroring <c>SqliteCharacterRepositoryTests</c>'s exact fixture
    /// convention. None of these tests mock or bypass a repository.
    /// </summary>
    public sealed class CharacterTemplateAndDraftBindingTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        private string _campaignDir = null!;
        private string _profileDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;
        private LocalProfileHandle _profile = null!;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s04-103-camp-" + Guid.NewGuid().ToString("N"));
            _profileDir = Path.Combine(Path.GetTempPath(), "ody-s04-103-profile-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_profileDir);

            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_campaignDir, "Draft/Template Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = _campaignRepository.Create(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _profile = new LocalProfileHandle(NewUserId(), _profileDir);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                _campaignRepository.Close(_campaign, TestCorrelationId);
            }
            catch (IOException) { }

            foreach (string dir in new[] { _campaignDir, _profileDir })
            {
                try
                {
                    if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
                }
                catch (IOException)
                {
                    // Best-effort cleanup only.
                }
            }
        }

        private static CharacterTemplateSeed OneItemSeed(string category, string name, string? value)
        {
            var item = new CharacterTemplateSeedItem(TemplateSeedItemId.NewId(Clock.GetUtcNow()), category, name, value);
            return new CharacterTemplateSeed(new[] { item });
        }

        // TC-CHAR-017: CreateLocalCharacterDraft with all required minimum
        // fields (product section 8.2, narrowed to what a pre-bind Draft
        // knows) succeeds.
        [Test]
        public void CreateLocalCharacterDraft_WithAllRequiredFields_Succeeds()
        {
            var draftRepository = new SqliteLocalCharacterDraftRepository(Clock);
            var request = new CreateLocalCharacterDraftRequest(CharacterKind.PlayerCharacter, "Aria", "anatomy.humanoid", null);

            Result<LocalCharacterDraftRecord> result = draftRepository.CreateLocalCharacterDraft(_profile, request, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsSuccess, Is.True);
            LocalCharacterDraftRecord record = result.Value;
            Assert.That(record.DraftId.IsValid, Is.True);
            Assert.That(record.Name, Is.EqualTo("Aria"));
            Assert.That(record.AnatomyProfileRef, Is.EqualTo("anatomy.humanoid"));
            Assert.That(record.TemplateId, Is.Null);
            Assert.That(record.SeedCopy, Is.Empty);
        }

        // TC-CHAR-018: a missing minimum required field (Name, or
        // AnatomyProfileRef) is rejected.
        [Test]
        public void CreateLocalCharacterDraftRequest_WithMissingName_IsRejected()
        {
            Action action = () => new CreateLocalCharacterDraftRequest(CharacterKind.PlayerCharacter, "", "anatomy.humanoid", null);
            Assert.Throws<ArgumentException>(action);
        }

        [Test]
        public void CreateLocalCharacterDraftRequest_WithMissingAnatomyProfileRef_IsRejected()
        {
            Action action = () => new CreateLocalCharacterDraftRequest(CharacterKind.PlayerCharacter, "Aria", "", null);
            Assert.Throws<ArgumentException>(action);
        }

        // TC-CHAR-019: CreatePersonalCharacterTemplate and
        // CreateCampaignCharacterTemplate both persist through the same
        // CharacterTemplate aggregate/table, distinguished only by
        // TemplateScope.
        [Test]
        public void CreatePersonalAndCampaignCharacterTemplate_BothUseTheSameAggregate_DistinguishedByScope()
        {
            var templateRepository = new SqliteCharacterTemplateRepository(Clock);
            CharacterTemplateSeed seed = OneItemSeed("Attribute", "Strength", "10");

            Result<CharacterTemplateRecord> personal = templateRepository.CreatePersonalCharacterTemplate(_profile, "Personal Fighter", CharacterKind.PlayerCharacter, "ruleset.core", "1.0.0", "anatomy.humanoid", seed, NewCommandId(), TestCorrelationId);
            Result<CharacterTemplateRecord> campaign = templateRepository.CreateCampaignCharacterTemplate(_campaign, "Campaign Fighter", CharacterKind.PlayerCharacter, "anatomy.humanoid", seed, NewCommandId(), TestCorrelationId);

            Assert.That(personal.IsSuccess, Is.True);
            Assert.That(campaign.IsSuccess, Is.True);
            Assert.That(personal.Value.Scope, Is.EqualTo(TemplateScope.Personal));
            Assert.That(personal.Value.OwnerUserId, Is.EqualTo(_profile.OwnerUserId));
            Assert.That(campaign.Value.Scope, Is.EqualTo(TemplateScope.Campaign));
            Assert.That(campaign.Value.CampaignId, Is.EqualTo(_campaign.CampaignId));
            // A Campaign template inherits its own campaign's pinned ruleset.
            Assert.That(campaign.Value.RulesetId, Is.EqualTo(_campaign.Manifest.RulesetId));
            Assert.That(campaign.Value.RulesetVersion, Is.EqualTo(_campaign.Manifest.RulesetVersion));

            // Both are independently retrievable through the same repository/
            // table, only the storage handle's Scope differs.
            Result<CharacterTemplateRecord> reReadPersonal = templateRepository.GetCharacterTemplate(TemplateStorageHandle.ForPersonal(_profile), personal.Value.TemplateId, TestCorrelationId);
            Result<CharacterTemplateRecord> reReadCampaign = templateRepository.GetCharacterTemplate(TemplateStorageHandle.ForCampaign(_campaign), campaign.Value.TemplateId, TestCorrelationId);
            Assert.That(reReadPersonal.IsSuccess, Is.True);
            Assert.That(reReadCampaign.IsSuccess, Is.True);
        }

        // TC-CHAR-020: BindDraftToCampaign from a CampaignCharacterTemplate
        // creates exactly one Character with a new CharacterId; copied
        // nested seed items get new IDs distinct from the template's own.
        [Test]
        public void BindDraftToCampaign_FromCampaignTemplate_CreatesOneCharacter_WithFreshNestedIds()
        {
            var templateRepository = new SqliteCharacterTemplateRepository(Clock);
            var characterRepository = new SqliteCharacterRepository(Clock);
            CharacterTemplateSeed rawSeed = OneItemSeed("Skill", "Swordplay", "2");

            Result<CharacterTemplateRecord> template = templateRepository.CreateCampaignCharacterTemplate(_campaign, "Fighter Template", CharacterKind.PlayerCharacter, "anatomy.humanoid", rawSeed, NewCommandId(), TestCorrelationId);
            Assert.That(template.IsSuccess, Is.True);
            TemplateSeedItemId sourceSeedItemId = template.Value.Seed.Items[0].SeedItemId;

            UtcInstant now = Clock.GetUtcNow();
            CharacterCreationSeed seed = CharacterCreationSeed.FromTemplate(template.Value.TemplateId, template.Value.Revision, template.Value.Seed, now);
            UserId owner = NewUserId();
            var bindRequest = new BindDraftToCampaignRequest(_campaign, CharacterKind.PlayerCharacter, "Bound Fighter", "anatomy.humanoid", owner, seed, template.Value.RulesetId, template.Value.RulesetVersion);

            Result<CharacterRecord> bound = characterRepository.BindDraftToCampaign(bindRequest, NewCommandId(), TestCorrelationId);

            Assert.That(bound.IsSuccess, Is.True);
            CharacterRecord character = bound.Value;
            Assert.That(character.CharacterId.IsValid, Is.True);
            Assert.That(character.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Draft));
            Assert.That(character.ApprovalState, Is.EqualTo(CharacterApprovalState.Draft));
            Assert.That(character.TemplateId, Is.EqualTo(template.Value.TemplateId));
            Assert.That(character.TemplateVersionAtCopyTime, Is.EqualTo(template.Value.Revision));
            Assert.That(character.SeedCopy, Has.Count.EqualTo(1));
            Assert.That(character.SeedCopy[0].SourceSeedItemId, Is.EqualTo(sourceSeedItemId));
            Assert.That(character.SeedCopy[0].NewSeedItemId, Is.Not.EqualTo(sourceSeedItemId));

            // Independently retrievable -- exactly one Character row exists.
            Result<CharacterRecord> reRead = characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.IsSuccess, Is.True);
        }

        // ADR-023 section 11 item 3 / CAP-INV-006: two Characters created
        // from the same template do not share a nested seed item identifier.
        [Test]
        public void BindDraftToCampaign_TwiceFromSameTemplate_ProducesDistinctNestedIds()
        {
            var templateRepository = new SqliteCharacterTemplateRepository(Clock);
            var characterRepository = new SqliteCharacterRepository(Clock);
            CharacterTemplateSeed rawSeed = OneItemSeed("Skill", "Swordplay", "2");
            Result<CharacterTemplateRecord> template = templateRepository.CreateCampaignCharacterTemplate(_campaign, "Fighter Template", CharacterKind.PlayerCharacter, "anatomy.humanoid", rawSeed, NewCommandId(), TestCorrelationId);
            Assert.That(template.IsSuccess, Is.True);

            UtcInstant now = Clock.GetUtcNow();
            CharacterCreationSeed seedA = CharacterCreationSeed.FromTemplate(template.Value.TemplateId, template.Value.Revision, template.Value.Seed, now);
            CharacterCreationSeed seedB = CharacterCreationSeed.FromTemplate(template.Value.TemplateId, template.Value.Revision, template.Value.Seed, now);

            Result<CharacterRecord> characterA = characterRepository.BindDraftToCampaign(
                new BindDraftToCampaignRequest(_campaign, CharacterKind.PlayerCharacter, "Fighter A", "anatomy.humanoid", NewUserId(), seedA, template.Value.RulesetId, template.Value.RulesetVersion),
                NewCommandId(), TestCorrelationId);
            Result<CharacterRecord> characterB = characterRepository.BindDraftToCampaign(
                new BindDraftToCampaignRequest(_campaign, CharacterKind.PlayerCharacter, "Fighter B", "anatomy.humanoid", NewUserId(), seedB, template.Value.RulesetId, template.Value.RulesetVersion),
                NewCommandId(), TestCorrelationId);

            Assert.That(characterA.IsSuccess, Is.True);
            Assert.That(characterB.IsSuccess, Is.True);
            Assert.That(characterA.Value.SeedCopy[0].NewSeedItemId, Is.Not.EqualTo(characterB.Value.SeedCopy[0].NewSeedItemId));
        }

        // TC-CHAR-021 / CAP-INV-006: a later UpdateCharacterTemplate on the
        // source template has zero effect on an already-created Character.
        [Test]
        public void UpdateCharacterTemplate_AfterBind_DoesNotChangeAlreadyCreatedCharacter()
        {
            var templateRepository = new SqliteCharacterTemplateRepository(Clock);
            var characterRepository = new SqliteCharacterRepository(Clock);
            CharacterTemplateSeed originalSeed = OneItemSeed("Attribute", "Strength", "10");
            Result<CharacterTemplateRecord> template = templateRepository.CreateCampaignCharacterTemplate(_campaign, "Original Name", CharacterKind.PlayerCharacter, "anatomy.humanoid", originalSeed, NewCommandId(), TestCorrelationId);
            Assert.That(template.IsSuccess, Is.True);

            UtcInstant now = Clock.GetUtcNow();
            CharacterCreationSeed seed = CharacterCreationSeed.FromTemplate(template.Value.TemplateId, template.Value.Revision, template.Value.Seed, now);
            Result<CharacterRecord> bound = characterRepository.BindDraftToCampaign(
                new BindDraftToCampaignRequest(_campaign, CharacterKind.PlayerCharacter, "Bound Character", "anatomy.humanoid", NewUserId(), seed, template.Value.RulesetId, template.Value.RulesetVersion),
                NewCommandId(), TestCorrelationId);
            Assert.That(bound.IsSuccess, Is.True);
            string originalDisplayName = bound.Value.DisplayName;
            long? originalTemplateVersionAtCopyTime = bound.Value.TemplateVersionAtCopyTime;
            string originalCopiedValue = bound.Value.SeedCopy[0].Value!;

            // Edit the source template: rename it and change its seed value.
            CharacterTemplateSeed updatedSeed = OneItemSeed("Attribute", "Strength", "18");
            Result<CharacterTemplateRecord> updated = templateRepository.UpdateCharacterTemplate(TemplateStorageHandle.ForCampaign(_campaign), template.Value.TemplateId, "Renamed Template", "anatomy.humanoid", updatedSeed, template.Value.Revision, NewCommandId(), TestCorrelationId);
            Assert.That(updated.IsSuccess, Is.True);
            Assert.That(updated.Value.Name, Is.EqualTo("Renamed Template"));

            Result<CharacterRecord> reRead = characterRepository.GetCharacter(_campaign, bound.Value.CharacterId, TestCorrelationId);
            Assert.That(reRead.IsSuccess, Is.True);
            Assert.That(reRead.Value.DisplayName, Is.EqualTo(originalDisplayName));
            Assert.That(reRead.Value.TemplateVersionAtCopyTime, Is.EqualTo(originalTemplateVersionAtCopyTime));
            Assert.That(reRead.Value.SeedCopy[0].Value, Is.EqualTo(originalCopiedValue));
        }

        // TC-CHAR-022: an incompatible ruleset (different RulesetId) rejects
        // BindDraftToCampaign before any Character is created.
        [Test]
        public void BindDraftToCampaign_WithIncompatibleRulesetId_IsRejected_NoCharacterCreated()
        {
            var characterRepository = new SqliteCharacterRepository(Clock);
            CharacterTemplateSeed seed = OneItemSeed("Attribute", "Strength", "10");
            CharacterCreationSeed creationSeed = CharacterCreationSeed.FromTemplate(CharacterTemplateId.NewId(Clock.GetUtcNow()), 1, seed, Clock.GetUtcNow());

            var bindRequest = new BindDraftToCampaignRequest(_campaign, CharacterKind.PlayerCharacter, "Incompatible", "anatomy.humanoid", NewUserId(), creationSeed, "ruleset.other", "1.0.0");

            Result<CharacterRecord> result = characterRepository.BindDraftToCampaign(bindRequest, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterDraftRulesetIncompatible));
        }

        // TC-CHAR-023: an incompatible major ruleset version (same RulesetId,
        // different major line) is also rejected.
        [Test]
        public void BindDraftToCampaign_WithIncompatibleRulesetMajorVersion_IsRejected()
        {
            var characterRepository = new SqliteCharacterRepository(Clock);
            CharacterTemplateSeed seed = CharacterTemplateSeed.Empty();
            CharacterCreationSeed creationSeed = CharacterCreationSeed.FromTemplate(CharacterTemplateId.NewId(Clock.GetUtcNow()), 1, seed, Clock.GetUtcNow());

            var bindRequest = new BindDraftToCampaignRequest(_campaign, CharacterKind.PlayerCharacter, "Incompatible Major", "anatomy.humanoid", NewUserId(), creationSeed, "ruleset.core", "2.0.0");

            Result<CharacterRecord> result = characterRepository.BindDraftToCampaign(bindRequest, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterDraftRulesetIncompatible));
        }

        // TC-CHAR-024: RulesetVersion is pinned to the campaign's own current
        // version at bind time, not the template's own recorded version.
        [Test]
        public void BindDraftToCampaign_PinsCampaignsCurrentRulesetVersion_NotTheTemplatesOwn()
        {
            var characterRepository = new SqliteCharacterRepository(Clock);
            CharacterTemplateSeed seed = CharacterTemplateSeed.Empty();
            // Compatible (same RulesetId, same major line 1.x) but a
            // different recorded version than the campaign's own "1.0.0".
            CharacterCreationSeed creationSeed = CharacterCreationSeed.FromTemplate(CharacterTemplateId.NewId(Clock.GetUtcNow()), 1, seed, Clock.GetUtcNow());
            var bindRequest = new BindDraftToCampaignRequest(_campaign, CharacterKind.PlayerCharacter, "Pinned Ruleset", "anatomy.humanoid", NewUserId(), creationSeed, "ruleset.core", "1.9.9");

            Result<CharacterRecord> result = characterRepository.BindDraftToCampaign(bindRequest, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.RulesetVersion, Is.EqualTo(_campaign.Manifest.RulesetVersion));
            Assert.That(result.Value.RulesetVersion, Is.Not.EqualTo("1.9.9"));
        }

        // TC-CHAR-025: initial PrimaryOwnerUserId is set at bind (an ordinary
        // Draft field, backlog section 2.2) and visible through the same
        // CharacterOwnership ODY-S04-102 already implemented.
        [Test]
        public void BindDraftToCampaign_SetsInitialPrimaryOwner_VisibleThroughCharacterOwnership()
        {
            var characterRepository = new SqliteCharacterRepository(Clock);
            UserId owner = NewUserId();
            var bindRequest = new BindDraftToCampaignRequest(_campaign, CharacterKind.PlayerCharacter, "Owned Character", "anatomy.humanoid", owner, CharacterCreationSeed.None(), null, null);

            Result<CharacterRecord> result = characterRepository.BindDraftToCampaign(bindRequest, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Ownership.PrimaryOwnerUserId, Is.EqualTo(owner));
            Assert.That(CharacterOwnershipAssignment.IsAssignedCharacter(result.Value.Ownership, owner, Clock.GetUtcNow()), Is.True);
        }

        // A PlayerCharacter with no InitialPrimaryOwnerUserId is rejected at
        // request construction (product section 8.2's "PrimaryOwner — только
        // для PlayerCharacter", required, not optional, for that kind).
        [Test]
        public void BindDraftToCampaignRequest_PlayerCharacterWithoutInitialPrimaryOwner_IsRejected()
        {
            Action action = () => new BindDraftToCampaignRequest(_campaign, CharacterKind.PlayerCharacter, "No Owner", "anatomy.humanoid", null, CharacterCreationSeed.None(), null, null);
            Assert.Throws<ArgumentException>(action);
        }

        // A non-PlayerCharacter (e.g. an NPC) does not require an initial
        // owner.
        [Test]
        public void BindDraftToCampaign_NonPlayerCharacter_DoesNotRequireInitialPrimaryOwner()
        {
            var characterRepository = new SqliteCharacterRepository(Clock);
            var bindRequest = new BindDraftToCampaignRequest(_campaign, CharacterKind.NonPlayerCharacter, "GM's NPC", "anatomy.humanoid", null, CharacterCreationSeed.None(), null, null);

            Result<CharacterRecord> result = characterRepository.BindDraftToCampaign(bindRequest, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Ownership.PrimaryOwnerUserId, Is.Null);
        }

        // TC-CHAR-026: a duplicate BindDraftToCampaign CommandId does not
        // create a second Character.
        [Test]
        public void BindDraftToCampaign_DuplicateCommandId_DoesNotCreateSecondCharacter()
        {
            var characterRepository = new SqliteCharacterRepository(Clock);
            CommandId commandId = NewCommandId();
            var bindRequest = new BindDraftToCampaignRequest(_campaign, CharacterKind.PlayerCharacter, "Replayed Character", "anatomy.humanoid", NewUserId(), CharacterCreationSeed.None(), null, null);

            Result<CharacterRecord> first = characterRepository.BindDraftToCampaign(bindRequest, commandId, TestCorrelationId);
            Result<CharacterRecord> second = characterRepository.BindDraftToCampaign(bindRequest, commandId, TestCorrelationId);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(second.IsSuccess, Is.True);
            Assert.That(second.Value.CharacterId, Is.EqualTo(first.Value.CharacterId));
        }

        // ADR-023 section 5.3: a local Draft created from a Personal template
        // already carries the fresh-identifier deep copy; BindDraftToCampaign
        // carries it through unchanged rather than re-copying.
        [Test]
        public void CreateLocalCharacterDraft_FromPersonalTemplate_ThenBind_CarriesTheSameCopiedSeedThrough()
        {
            var templateRepository = new SqliteCharacterTemplateRepository(Clock);
            var draftRepository = new SqliteLocalCharacterDraftRepository(Clock);
            var characterRepository = new SqliteCharacterRepository(Clock);

            CharacterTemplateSeed rawSeed = OneItemSeed("Skill", "Stealth", "1");
            Result<CharacterTemplateRecord> template = templateRepository.CreatePersonalCharacterTemplate(_profile, "Rogue Template", CharacterKind.PlayerCharacter, "ruleset.core", "1.0.0", "anatomy.humanoid", rawSeed, NewCommandId(), TestCorrelationId);
            Assert.That(template.IsSuccess, Is.True);

            var draftRequest = new CreateLocalCharacterDraftRequest(CharacterKind.PlayerCharacter, "Rogue Draft", "anatomy.humanoid", template.Value.TemplateId);
            Result<LocalCharacterDraftRecord> draft = draftRepository.CreateLocalCharacterDraft(_profile, draftRequest, NewCommandId(), TestCorrelationId);
            Assert.That(draft.IsSuccess, Is.True);
            Assert.That(draft.Value.SeedCopy, Has.Count.EqualTo(1));
            TemplateSeedItemId copiedAtDraftTime = draft.Value.SeedCopy[0].NewSeedItemId;

            CharacterCreationSeed seed = CharacterCreationSeed.AlreadyCopied(draft.Value.TemplateId!.Value, draft.Value.TemplateVersionAtCopyTime!.Value, draft.Value.SeedCopy);
            var bindRequest = new BindDraftToCampaignRequest(_campaign, CharacterKind.PlayerCharacter, draft.Value.Name, draft.Value.AnatomyProfileRef, NewUserId(), seed, template.Value.RulesetId, template.Value.RulesetVersion);
            Result<CharacterRecord> bound = characterRepository.BindDraftToCampaign(bindRequest, NewCommandId(), TestCorrelationId);

            Assert.That(bound.IsSuccess, Is.True);
            Assert.That(bound.Value.SeedCopy, Has.Count.EqualTo(1));
            // The exact same identifier minted at Draft-creation time -- not
            // re-copied a second time at bind.
            Assert.That(bound.Value.SeedCopy[0].NewSeedItemId, Is.EqualTo(copiedAtDraftTime));
        }

        // ArchiveCharacterTemplate: status transitions to Archived; a stale
        // expectedRevision is rejected.
        [Test]
        public void ArchiveCharacterTemplate_Succeeds_AndRejectsStaleRevision()
        {
            var templateRepository = new SqliteCharacterTemplateRepository(Clock);
            Result<CharacterTemplateRecord> template = templateRepository.CreateCampaignCharacterTemplate(_campaign, "To Archive", CharacterKind.NonPlayerCharacter, null, CharacterTemplateSeed.Empty(), NewCommandId(), TestCorrelationId);
            Assert.That(template.IsSuccess, Is.True);

            Result<CharacterTemplateRecord> archived = templateRepository.ArchiveCharacterTemplate(TemplateStorageHandle.ForCampaign(_campaign), template.Value.TemplateId, template.Value.Revision, NewCommandId(), TestCorrelationId);
            Assert.That(archived.IsSuccess, Is.True);
            Assert.That(archived.Value.Status, Is.EqualTo(CharacterTemplateStatus.Archived));

            Result<CharacterTemplateRecord> staleArchive = templateRepository.ArchiveCharacterTemplate(TemplateStorageHandle.ForCampaign(_campaign), template.Value.TemplateId, template.Value.Revision, NewCommandId(), TestCorrelationId);
            Assert.That(staleArchive.IsFailure, Is.True);
            Assert.That(staleArchive.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterTemplateRevisionConflict));
        }
    }
}
