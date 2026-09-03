using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence.Integration
{
    /// <summary>
    /// ODY-S04-114: roadmap section 13.8's eleven-step "Персонаж и развитие"
    /// scenario, run literally in order in one test method (the same
    /// "one sequence, not isolated steps" structural choice
    /// ODY-S01-013/ODY-S02-013/ODY-S03-008 already established for their own
    /// slices), over already-merged ODY-S04-101..113 public APIs. No new
    /// production code exists to support this test.
    ///
    /// Real infrastructure throughout: <see cref="SqliteCampaignRepository"/>/
    /// <see cref="SqliteLocalCharacterDraftRepository"/>/
    /// <see cref="SqliteCharacterTemplateRepository"/>/
    /// <see cref="SqliteCharacterRepository"/> against a real temp-directory
    /// local-profile store and a real SQLite <c>campaign.db</c> (mirroring
    /// <c>TC-CHAR-*</c>'s own fixture pattern) -- no repository-level mock.
    ///
    /// Step 8 ("critical skill check creates evidence") calls
    /// <see cref="ICharacterRepository.RecordCriticalSuccessEvidence"/>
    /// directly with a synthetic <c>sourceDiceRollId</c>, exactly as
    /// <c>ODY-S04-106</c>'s own test suite already does -- no real
    /// <c>SLICE-03</c> <c>DiceRollService</c> integration exists or is added
    /// (this task's own explicit exclusion, section 5).
    ///
    /// Discovered fact (not a defect to fix here, per this task's own
    /// section 18/acceptance criterion 11 -- reported, not silently worked
    /// around): <c>SqliteCharacterRepository.HistoryEventTypes</c>, the
    /// hand-maintained whitelist <c>GetCharacterHistory</c> filters against,
    /// does not include a skill-purchase, critical-evidence, or
    /// advancement-recommendation event type -- only
    /// <c>character_attribute_increased</c> is tracked among mechanics
    /// events. Step 10's own history assertions below are therefore scoped
    /// to the event types this codebase's own history projection actually
    /// tracks (draft-bound/submitted/approved/points-granted/attribute-
    /// increased); the recommendation/evidence half of "authoritative state"
    /// is instead proven via a fresh <see cref="ICharacterRepository.GetCharacter"/>
    /// read on the reopened repository, which does reflect every section's
    /// real current values regardless of this narrower event-type list.
    /// </summary>
    public sealed class CharacterVerticalSliceIntegrationTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        private sealed class SystemWallClock : IWallClock
        {
            public UtcInstant GetUtcNow() => UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow);
        }

        private string _campaignDir = null!;
        private string _profileDir = null!;
        private string _exportBundleDir = null!;

        [SetUp]
        public void SetUp()
        {
            string root = Path.Combine(Path.GetTempPath(), "ody-s04-114-" + Guid.NewGuid().ToString("N"));
            _campaignDir = Path.Combine(root, "campaign");
            _profileDir = Path.Combine(root, "profile");
            _exportBundleDir = Path.Combine(root, "export");
            Directory.CreateDirectory(_profileDir);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(Path.GetDirectoryName(_campaignDir))) Directory.Delete(Path.GetDirectoryName(_campaignDir)!, recursive: true); } catch (IOException) { }
        }

        [Test]
        public void ElevenStepSlice_DraftThroughOdcharImport_AllStepsSucceedInOrder()
        {
            var campaignRepository = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> created = campaignRepository.Create(new CreateCampaignRequest(_campaignDir, "SLICE-04 Vertical Slice Campaign", "ruleset.core", "1.0.0", "0.1.0"), NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True, "campaign creation must succeed before the scenario begins");
            CampaignHandle campaign = created.Value;

            UserId player = NewUserId();
            UserId mainGm = NewUserId();
            var localProfile = new LocalProfileHandle(player, _profileDir);

            var draftRepository = new SqliteLocalCharacterDraftRepository(Clock);
            var templateRepository = new SqliteCharacterTemplateRepository(Clock);
            var characterRepository = new SqliteCharacterRepository(Clock);

            // ---- Step 1: Player creates local Draft. ----
            // A local Draft has no CampaignId/CharacterId until bound
            // (ADR-023 section 4.1) -- proven directly by LocalCharacterDraftRecord's
            // own shape carrying neither field at all.
            var createDraftRequest = new CreateLocalCharacterDraftRequest(CharacterKind.PlayerCharacter, "Vertical Slice Character", "Humanoid", personalTemplateId: null);
            Result<LocalCharacterDraftRecord> draftCreated = draftRepository.CreateLocalCharacterDraft(localProfile, createDraftRequest, NewCommandId(), TestCorrelationId);
            Assert.That(draftCreated.IsSuccess, Is.True, "step 1 (create local Draft) must succeed");
            LocalCharacterDraftRecord localDraft = draftCreated.Value;

            // ---- Step 2: selects campaign template. ----
            // A Campaign-scope template is authored for the campaign's own
            // pinned Ruleset (ADR-023 section 5.2) and applied at bind time
            // via CharacterCreationSeed.FromTemplate -- the actual moment
            // template *selection* takes effect for a Campaign template.
            var seedItem = new CharacterTemplateSeedItem(TemplateSeedItemId.NewId(Clock.GetUtcNow()), "note", "origin", "vertical-slice-fixture");
            var templateSeed = new CharacterTemplateSeed(new[] { seedItem });
            Result<CharacterTemplateRecord> templateCreated = templateRepository.CreateCampaignCharacterTemplate(campaign, "Vertical Slice Template", CharacterKind.PlayerCharacter, "Humanoid", templateSeed, NewCommandId(), TestCorrelationId);
            Assert.That(templateCreated.IsSuccess, Is.True, "step 2 (author the campaign template the player selects) must succeed");
            CharacterTemplateRecord campaignTemplate = templateCreated.Value;

            CharacterCreationSeed seed = CharacterCreationSeed.FromTemplate(campaignTemplate.TemplateId, campaignTemplate.Revision, campaignTemplate.Seed, Clock.GetUtcNow());

            // ---- Step 3: host validates submit. ----
            // The negative case first: an incompatible RulesetId must be
            // rejected by BindDraftToCampaign's own compatibility gate
            // (ADR-023 section 6.1) before any Character row is created --
            // this is "host validates" proven for real, not merely assumed.
            var incompatibleBindRequest = new BindDraftToCampaignRequest(campaign, localDraft.CharacterKind, localDraft.Name, localDraft.AnatomyProfileRef, player, seed, "ruleset.incompatible", "9.0.0");
            Result<CharacterRecord> incompatibleBind = characterRepository.BindDraftToCampaign(incompatibleBindRequest, NewCommandId(), TestCorrelationId);
            Assert.That(incompatibleBind.IsFailure, Is.True, "step 3: an incompatible Ruleset must be rejected by the host's own validation");
            Assert.That(incompatibleBind.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterDraftRulesetIncompatible));

            var bindRequest = new BindDraftToCampaignRequest(campaign, localDraft.CharacterKind, localDraft.Name, localDraft.AnatomyProfileRef, player, seed, campaignTemplate.RulesetId, campaignTemplate.RulesetVersion);
            Result<CharacterRecord> bound = characterRepository.BindDraftToCampaign(bindRequest, NewCommandId(), TestCorrelationId);
            Assert.That(bound.IsSuccess, Is.True, "step 3: a compatible Ruleset must be accepted, creating the campaign Character");
            CharacterId characterId = bound.Value.CharacterId;
            Assert.That(bound.Value.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Draft));
            Assert.That(bound.Value.ApprovalState, Is.EqualTo(CharacterApprovalState.Draft));

            // Roadmap section 13.9's own "created Character is independent
            // from template" exit criterion: mutate the source template
            // AFTER binding, then confirm the already-bound Character's own
            // copied AnatomyProfileRef is unaffected.
            Result<CharacterTemplateRecord> templateMutated = templateRepository.UpdateCharacterTemplate(TemplateStorageHandle.ForCampaign(campaign), campaignTemplate.TemplateId, "Vertical Slice Template (mutated after bind)", "MutatedAnatomyProfile", templateSeed, campaignTemplate.Revision, NewCommandId(), TestCorrelationId);
            Assert.That(templateMutated.IsSuccess, Is.True);
            Result<CharacterRecord> unaffectedByTemplateEdit = characterRepository.GetCharacter(campaign, characterId, TestCorrelationId);
            Assert.That(unaffectedByTemplateEdit.Value.AnatomyProfileRef, Is.EqualTo("Humanoid"), "step 2/3 invariant: a later template edit must never retroactively change an already-bound Character");

            Result<CharacterRecord> submitted = characterRepository.SubmitCharacterDraft(campaign, characterId, bound.Value.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);
            Assert.That(submitted.IsSuccess, Is.True, "step 3: submit must succeed");
            Assert.That(submitted.Value.ApprovalState, Is.EqualTo(CharacterApprovalState.Draft), "step 3 invariant: submit alone must not approve");

            // ---- Steps 4-5: GM approves; Character becomes Active. ----
            // One call realizes both roadmap steps: ApproveCharacterDraft
            // transitions ApprovalState AND LifecycleStatus together
            // (ODY-S04-104's own established shape).
            Result<CharacterRecord> approved = characterRepository.ApproveCharacterDraft(campaign, characterId, actorIsMainGm: true, submitted.Value.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);
            Assert.That(approved.IsSuccess, Is.True, "step 4: MainGM approval must succeed");
            Assert.That(approved.Value.ApprovalState, Is.EqualTo(CharacterApprovalState.Approved), "step 4: approval state must flip to Approved");
            Assert.That(approved.Value.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Active), "step 5: the Character must become Active");

            // ---- Step 6: MainGM grants development points. ----
            Result<CharacterRecord> deniedGrant = characterRepository.GrantDevelopmentPoints(campaign, characterId, 50, "vertical slice grant", player, actorIsMainGm: false, approved.Value.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(deniedGrant.IsFailure, Is.True, "step 6: only MainGM may grant development points (roadmap section 13.9)");

            Result<CharacterRecord> granted = characterRepository.GrantDevelopmentPoints(campaign, characterId, 50, "vertical slice grant", mainGm, actorIsMainGm: true, approved.Value.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(granted.IsSuccess, Is.True, "step 6: a MainGM-issued grant must succeed");
            Assert.That(granted.Value.DevelopmentPool.Earned, Is.EqualTo(50));

            // ---- Step 7: Player purchases an attribute immediately, no GM-approval step. ----
            var strength = AttributeDefinitionId.Parse("Strength");
            CommandId purchaseCommandId = NewCommandId();
            Result<CharacterRecord> purchased = characterRepository.PurchaseAttributeIncrease(campaign, characterId, strength, toValue: 2, player, actorIsMainGm: false, granted.Value.Revisions.MechanicsRevision, expectedAttributeRevision: 0, purchaseCommandId, TestCorrelationId);
            Assert.That(purchased.IsSuccess, Is.True, "step 7: an ordinary valid purchase by the owner must succeed without a separate GM-approval step (roadmap section 13.9)");
            long spentAfterFirstPurchase = purchased.Value.DevelopmentPool.Spent;

            // Roadmap section 13.9's own "duplicate command does not spend
            // twice" exit criterion, using the SAME CommandId again.
            Result<CharacterRecord> duplicatePurchase = characterRepository.PurchaseAttributeIncrease(campaign, characterId, strength, toValue: 2, player, actorIsMainGm: false, granted.Value.Revisions.MechanicsRevision, expectedAttributeRevision: 0, purchaseCommandId, TestCorrelationId);
            Assert.That(duplicatePurchase.IsSuccess, Is.True);
            Assert.That(duplicatePurchase.Value.DevelopmentPool.Spent, Is.EqualTo(spentAfterFirstPurchase), "step 7: a replayed duplicate CommandId must not spend a second time");

            // ---- Step 8: a critical skill check creates evidence. ----
            var stealth = SkillDefinitionId.Parse("Stealth");
            Result<CriticalSuccessEvidenceRecord> evidence = characterRepository.RecordCriticalSuccessEvidence(campaign, characterId, stealth, sourceDiceRollId: "roll_vertical_slice_001", sourceActionId: null, NewCommandId(), TestCorrelationId);
            Assert.That(evidence.IsSuccess, Is.True, "step 8: recording a critical-success evidence fact must succeed");
            Assert.That(evidence.Value.UsedByAdvancementId, Is.Null, "step 8: freshly recorded evidence must be unconsumed");

            // ---- Step 9: GM resolves a skill 5+ recommendation, consuming the evidence. ----
            Result<CharacterRecord> beforeRecommendation = characterRepository.GetCharacter(campaign, characterId, TestCorrelationId);
            Result<AdvancementRecommendationRecord> recommendation = characterRepository.RequestSkillAdvancedRecommendation(campaign, characterId, stealth, targetLevel: 5, new[] { evidence.Value.EvidenceId }, mainGm, actorIsMainGm: true, beforeRecommendation.Value.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(recommendation.IsSuccess, Is.True, "step 9: requesting the skill 5+ recommendation must succeed and reserve points");

            Result<CharacterRecord> afterRequest = characterRepository.GetCharacter(campaign, characterId, TestCorrelationId);
            Assert.That(afterRequest.Value.DevelopmentPool.Reserved, Is.GreaterThan(0), "step 9: requesting must reserve, not spend, points");

            Result<CharacterRecord> resolved = characterRepository.ResolveAdvancementRecommendation(campaign, characterId, recommendation.Value.RecommendationId, approve: true, spendReservedPoints: true, mainGm, actorIsMainGm: true, afterRequest.Value.Revisions.MechanicsRevision, recommendation.Value.Revision, NewCommandId(), TestCorrelationId);
            Assert.That(resolved.IsSuccess, Is.True, "step 9: GM approval with spend must succeed");
            Assert.That(resolved.Value.Skills.Single(s => s.SkillDefinitionId.Equals(stealth)).Level, Is.EqualTo(5), "step 9: the approved recommendation must apply the target skill level");

            Result<IReadOnlyList<CriticalSuccessEvidenceRecord>> evidenceAfterResolve = characterRepository.GetCriticalSuccessEvidence(campaign, characterId, TestCorrelationId);
            Assert.That(evidenceAfterResolve.Value.Single().UsedByAdvancementId, Is.EqualTo(recommendation.Value.RecommendationId), "step 9: the resolved recommendation must consume its own referenced evidence");

            // Roadmap section 13.9's own "critical evidence cannot be
            // reused" exit criterion: a second recommendation referencing
            // the SAME already-consumed evidence must fail at resolve time
            // (mirroring ODY-S04-106's own established regression shape --
            // the request itself is accepted as a candidate reference; the
            // resolve is where reuse is actually rejected).
            var otherSkill = SkillDefinitionId.Parse("Perception");
            Result<AdvancementRecommendationRecord> reuseRequest = characterRepository.RequestSkillAdvancedRecommendation(campaign, characterId, otherSkill, targetLevel: 5, new[] { evidence.Value.EvidenceId }, mainGm, actorIsMainGm: true, resolved.Value.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(reuseRequest.IsSuccess, Is.True);
            Result<CharacterRecord> afterReuseRequest = characterRepository.GetCharacter(campaign, characterId, TestCorrelationId);
            Result<CharacterRecord> reuseResolve = characterRepository.ResolveAdvancementRecommendation(campaign, characterId, reuseRequest.Value.RecommendationId, approve: true, spendReservedPoints: true, mainGm, actorIsMainGm: true, afterReuseRequest.Value.Revisions.MechanicsRevision, reuseRequest.Value.Revision, NewCommandId(), TestCorrelationId);
            Assert.That(reuseResolve.IsFailure, Is.True, "step 9: already-consumed evidence must not be reusable by a second recommendation");

            // ---- Step 10: history and reconnect show authoritative state. ----
            // "Reconnect" here is this revision's own campaign-persistence
            // sense (ODY-S03-008's own precedent): a brand-new repository
            // instance reopening the same campaign.db file, never a
            // networked reconnect protocol (no real network exists in this
            // revision).
            var reconnectedCharacterRepository = new SqliteCharacterRepository(Clock);
            Result<IReadOnlyList<CharacterHistoryEntry>> historyAtReconnect = reconnectedCharacterRepository.GetCharacterHistory(campaign, characterId, TestCorrelationId);
            Assert.That(historyAtReconnect.IsSuccess, Is.True, "step 10: history must be readable after reconnect");

            // ODY-S04-115a widened SqliteCharacterRepository.HistoryEventTypes
            // to also track ODY-S04-106's own event types (skill purchase,
            // critical evidence, advancement recommendation) -- steps 8/9
            // now reappear here too, where before this fix they did not.
            string[] expectedEventTypesInOrder =
            {
                "odyssey.persistence.character_draft_bound",
                "odyssey.persistence.character_draft_submitted",
                "odyssey.persistence.character_approved",
                "odyssey.persistence.character_development_points_granted",
                "odyssey.persistence.character_attribute_increased",
                "odyssey.persistence.character_critical_success_evidence_recorded",
                "odyssey.persistence.character_skill_advancement_recommendation_created",
                "odyssey.persistence.character_skill_level_purchased",
                "odyssey.persistence.character_skill_advancement_recommendation_created",
            };
            string[] actualEventTypesInOrder = historyAtReconnect.Value.Select(e => e.EventType).ToArray();
            Assert.That(actualEventTypesInOrder, Is.EqualTo(expectedEventTypesInOrder), "step 10: every tracked event from steps 3-9 must reappear, in EventSequence order, after reconnect (only the event types SqliteCharacterRepository.HistoryEventTypes actually tracks -- see this class's own remarks)");

            Result<CharacterRecord> characterAtReconnect = reconnectedCharacterRepository.GetCharacter(campaign, characterId, TestCorrelationId);
            Assert.That(characterAtReconnect.IsSuccess, Is.True);
            Assert.That(characterAtReconnect.Value.DevelopmentPool.Earned, Is.EqualTo(50), "step 10: the reconnected read must reflect step 6's authoritative grant");
            Assert.That(characterAtReconnect.Value.Skills.Single(s => s.SkillDefinitionId.Equals(stealth)).Level, Is.EqualTo(5), "step 10: the reconnected read must reflect step 9's authoritative recommendation outcome");

            // ---- Step 11: .odchar export/import creates a new Draft. ----
            var exportActorContext = new ExportActorContext(mainGm, actorIsMainGm: true);
            Result<CharacterExportBundle> exported = reconnectedCharacterRepository.ExportCharacter(campaign, characterId, _exportBundleDir, exportActorContext, TestCorrelationId);
            Assert.That(exported.IsSuccess, Is.True, "step 11: export must succeed");

            UserId newOwner = NewUserId();
            var importRequest = new ImportCharacterRequest(campaign, _exportBundleDir, newOwner);
            Result<CharacterRecord> imported = reconnectedCharacterRepository.ImportCharacter(importRequest, NewCommandId(), NewCommandId(), TestCorrelationId);
            Assert.That(imported.IsSuccess, Is.True, "step 11: import must succeed");
            Assert.That(imported.Value.CharacterId, Is.Not.EqualTo(characterId), "step 11: import must create a fresh CharacterId (roadmap section 13.9's own \"import creates new ID and Draft\" exit criterion)");
            Assert.That(imported.Value.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Draft), "step 11: the imported Character must land as a new Draft");
            Assert.That(imported.Value.ApprovalState, Is.EqualTo(CharacterApprovalState.Draft), "step 11: the imported Draft must require fresh approval");
            Assert.That(imported.Value.Skills.Single(s => s.SkillDefinitionId.Equals(stealth)).Level, Is.EqualTo(5), "step 11: the round trip must preserve the mechanics state produced by steps 6-9");
        }
    }
}
