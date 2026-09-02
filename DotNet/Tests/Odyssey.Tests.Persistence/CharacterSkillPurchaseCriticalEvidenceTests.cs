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

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S04-106: real, non-stubbed tests for
    /// <see cref="SqliteCharacterRepository.PurchaseSkillLevel"/>,
    /// <see cref="SqliteCharacterRepository.RecordCriticalSuccessEvidence"/>,
    /// <see cref="SqliteCharacterRepository.RequestSkillAdvancedRecommendation"/>,
    /// and <see cref="SqliteCharacterRepository.ResolveAdvancementRecommendation"/>
    /// against a real temp-directory campaign and a real SQLite database --
    /// mirroring <c>CharacterDevelopmentPoolAttributePurchaseTests</c>'s exact
    /// fixture convention. Cost values
    /// (<see cref="Odyssey.Rules.Character.SkillCostRules"/>) are this task's
    /// own explicitly-flagged test fixture, not production Ruleset balance
    /// data.
    /// </summary>
    public sealed class CharacterSkillPurchaseCriticalEvidenceTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
        private static readonly SkillDefinitionId Stealth = SkillDefinitionId.Parse("Stealth");

        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;
        private SqliteCharacterRepository _characterRepository = null!;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s04-106-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_campaignDir, "Skill Purchase Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = _campaignRepository.Create(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _characterRepository = new SqliteCharacterRepository(Clock);
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
                if (Directory.Exists(_campaignDir)) Directory.Delete(_campaignDir, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup only.
            }
        }

        private CharacterRecord CreateCharacter(string name = "Skill Character")
        {
            var request = new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, name);
            Result<CharacterRecord> created = _characterRepository.CreateCharacter(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        private CharacterRecord GrantPoints(CharacterRecord character, long amount)
        {
            Result<CharacterRecord> granted = _characterRepository.GrantDevelopmentPoints(_campaign, character.CharacterId, amount, "Grant", NewUserId(), actorIsMainGm: true, character.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(granted.IsSuccess, Is.True);
            return granted.Value;
        }

        // TC-CHAR-050: PurchaseSkillLevel for an unpossessed skill creates a
        // CharacterSkill starting from level 1.
        [Test]
        public void PurchaseSkillLevel_ForUnpossessedSkill_CreatesCharacterSkillFromLevelOne()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 10);
            Assert.That(granted.Skills, Is.Empty);

            Result<CharacterRecord> purchased = _characterRepository.PurchaseSkillLevel(_campaign, character.CharacterId, Stealth, toLevel: 1, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, expectedSkillRevision: 0, NewCommandId(), TestCorrelationId);

            Assert.That(purchased.IsSuccess, Is.True);
            CharacterSkill skill = purchased.Value.Skills.Single(s => s.SkillDefinitionId.Equals(Stealth));
            Assert.That(skill.Level, Is.EqualTo(1));
            Assert.That(skill.Revision, Is.EqualTo(1));
        }

        // TC-CHAR-051/052: sufficient balance succeeds, insufficient balance
        // is rejected with no state change.
        [Test]
        public void PurchaseSkillLevel_WithSufficientBalance_Succeeds()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 10);

            Result<CharacterRecord> purchased = _characterRepository.PurchaseSkillLevel(_campaign, character.CharacterId, Stealth, toLevel: 2, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, expectedSkillRevision: 0, NewCommandId(), TestCorrelationId);

            Assert.That(purchased.IsSuccess, Is.True);
            // Fixture cost: 3 dev points per skill point (SkillCostRules.CostPerSkillPoint).
            Assert.That(purchased.Value.DevelopmentPool.Spent, Is.EqualTo(6));
            Assert.That(purchased.Value.DevelopmentPool.Available, Is.EqualTo(4));
        }

        [Test]
        public void PurchaseSkillLevel_WithInsufficientBalance_IsRejected_NoStateChange()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 1);

            Result<CharacterRecord> purchased = _characterRepository.PurchaseSkillLevel(_campaign, character.CharacterId, Stealth, toLevel: 2, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, expectedSkillRevision: 0, NewCommandId(), TestCorrelationId);

            Assert.That(purchased.IsFailure, Is.True);
            Assert.That(purchased.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterDevelopmentInsufficientBalance));

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.IsSuccess, Is.True);
            Assert.That(reRead.Value.Skills, Is.Empty);
            Assert.That(reRead.Value.DevelopmentPool.Spent, Is.EqualTo(0));
        }

        // Level 5+ must go through the recommendation pipeline, not this
        // ordinary purchase command.
        [Test]
        public void PurchaseSkillLevel_AboveMaxOrdinaryLevel_IsRejected()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 1000);

            Result<CharacterRecord> purchased = _characterRepository.PurchaseSkillLevel(_campaign, character.CharacterId, Stealth, toLevel: 5, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, expectedSkillRevision: 0, NewCommandId(), TestCorrelationId);

            Assert.That(purchased.IsFailure, Is.True);
            Assert.That(purchased.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterSkillLevelRequiresRecommendation));
        }

        // TC-CHAR-053: RequestSkillAdvancedRecommendation reserves exactly the
        // right amount -- Available decreases, Reserved increases, Spent
        // unchanged.
        [Test]
        public void RequestSkillAdvancedRecommendation_ReservesExactAmount()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 20);
            Result<CriticalSuccessEvidenceRecord> evidence = _characterRepository.RecordCriticalSuccessEvidence(_campaign, character.CharacterId, Stealth, "roll_1", null, NewCommandId(), TestCorrelationId);
            Assert.That(evidence.IsSuccess, Is.True);

            Result<AdvancementRecommendationRecord> requested = _characterRepository.RequestSkillAdvancedRecommendation(_campaign, character.CharacterId, Stealth, targetLevel: 5, new[] { evidence.Value.EvidenceId }, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);

            Assert.That(requested.IsSuccess, Is.True);
            Assert.That(requested.Value.Status, Is.EqualTo(AdvancementRecommendationStatus.Pending));
            // Fixture cost: 5 levels * 3 per point = 15.
            Assert.That(requested.Value.ReservedAmount, Is.EqualTo(15));

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.IsSuccess, Is.True);
            Assert.That(reRead.Value.DevelopmentPool.Reserved, Is.EqualTo(15));
            Assert.That(reRead.Value.DevelopmentPool.Available, Is.EqualTo(5));
            Assert.That(reRead.Value.DevelopmentPool.Spent, Is.EqualTo(0));
        }

        // TC-CHAR-054: ResolveAdvancementRecommendation (approved + spend)
        // converts Reserved -> Spent, applies the skill level, and consumes
        // the evidence.
        [Test]
        public void ResolveAdvancementRecommendation_ApprovedWithSpend_ConvertsReservedToSpent_AppliesLevel_ConsumesEvidence()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 20);
            Result<CriticalSuccessEvidenceRecord> evidence = _characterRepository.RecordCriticalSuccessEvidence(_campaign, character.CharacterId, Stealth, "roll_1", null, NewCommandId(), TestCorrelationId);
            Assert.That(evidence.IsSuccess, Is.True);
            Result<AdvancementRecommendationRecord> requested = _characterRepository.RequestSkillAdvancedRecommendation(_campaign, character.CharacterId, Stealth, targetLevel: 5, new[] { evidence.Value.EvidenceId }, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(requested.IsSuccess, Is.True);
            Result<CharacterRecord> afterRequest = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(afterRequest.IsSuccess, Is.True);

            Result<CharacterRecord> resolved = _characterRepository.ResolveAdvancementRecommendation(_campaign, character.CharacterId, requested.Value.RecommendationId, approve: true, spendReservedPoints: true, NewUserId(), actorIsMainGm: true, afterRequest.Value.Revisions.MechanicsRevision, requested.Value.Revision, NewCommandId(), TestCorrelationId);

            Assert.That(resolved.IsSuccess, Is.True);
            Assert.That(resolved.Value.DevelopmentPool.Reserved, Is.EqualTo(0));
            Assert.That(resolved.Value.DevelopmentPool.Spent, Is.EqualTo(15));
            Assert.That(resolved.Value.DevelopmentPool.Available, Is.EqualTo(5));
            CharacterSkill skill = resolved.Value.Skills.Single(s => s.SkillDefinitionId.Equals(Stealth));
            Assert.That(skill.Level, Is.EqualTo(5));

            Result<IReadOnlyList<CriticalSuccessEvidenceRecord>> evidenceList = _characterRepository.GetCriticalSuccessEvidence(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(evidenceList.IsSuccess, Is.True);
            Assert.That(evidenceList.Value.Single().UsedByAdvancementId, Is.EqualTo(requested.Value.RecommendationId));

            Result<AdvancementRecommendationRecord> recommendationAfter = _characterRepository.GetAdvancementRecommendation(_campaign, character.CharacterId, requested.Value.RecommendationId, TestCorrelationId);
            Assert.That(recommendationAfter.IsSuccess, Is.True);
            Assert.That(recommendationAfter.Value.Status, Is.EqualTo(AdvancementRecommendationStatus.Approved));
        }

        // TC-CHAR-055: ResolveAdvancementRecommendation (dismissed) releases
        // the reservation back to Available; skill level is not applied;
        // evidence stays unused.
        [Test]
        public void ResolveAdvancementRecommendation_Dismissed_ReleasesReservation_NoLevelChange_EvidenceUnused()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 20);
            Result<CriticalSuccessEvidenceRecord> evidence = _characterRepository.RecordCriticalSuccessEvidence(_campaign, character.CharacterId, Stealth, "roll_1", null, NewCommandId(), TestCorrelationId);
            Assert.That(evidence.IsSuccess, Is.True);
            Result<AdvancementRecommendationRecord> requested = _characterRepository.RequestSkillAdvancedRecommendation(_campaign, character.CharacterId, Stealth, targetLevel: 5, new[] { evidence.Value.EvidenceId }, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(requested.IsSuccess, Is.True);
            Result<CharacterRecord> afterRequest = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);

            Result<CharacterRecord> resolved = _characterRepository.ResolveAdvancementRecommendation(_campaign, character.CharacterId, requested.Value.RecommendationId, approve: false, spendReservedPoints: false, NewUserId(), actorIsMainGm: true, afterRequest.Value.Revisions.MechanicsRevision, requested.Value.Revision, NewCommandId(), TestCorrelationId);

            Assert.That(resolved.IsSuccess, Is.True);
            Assert.That(resolved.Value.DevelopmentPool.Reserved, Is.EqualTo(0));
            Assert.That(resolved.Value.DevelopmentPool.Spent, Is.EqualTo(0));
            Assert.That(resolved.Value.DevelopmentPool.Available, Is.EqualTo(20));
            Assert.That(resolved.Value.Skills.Any(s => s.SkillDefinitionId.Equals(Stealth)), Is.False);

            Result<IReadOnlyList<CriticalSuccessEvidenceRecord>> evidenceList = _characterRepository.GetCriticalSuccessEvidence(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(evidenceList.IsSuccess, Is.True);
            Assert.That(evidenceList.Value.Single().UsedByAdvancementId, Is.Null);
        }

        // TC-CHAR-056: single-use evidence -- a second recommendation cannot
        // consume already-spent evidence; the evidence's own UsedByAdvancementId
        // is checked directly, not just the rejection.
        [Test]
        public void CriticalSuccessEvidence_AlreadyUsed_CannotBeConsumedTwice()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 100);
            Result<CriticalSuccessEvidenceRecord> evidence = _characterRepository.RecordCriticalSuccessEvidence(_campaign, character.CharacterId, Stealth, "roll_1", null, NewCommandId(), TestCorrelationId);
            Assert.That(evidence.IsSuccess, Is.True);

            Result<AdvancementRecommendationRecord> firstRequest = _characterRepository.RequestSkillAdvancedRecommendation(_campaign, character.CharacterId, Stealth, targetLevel: 5, new[] { evidence.Value.EvidenceId }, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(firstRequest.IsSuccess, Is.True);
            Result<CharacterRecord> afterFirstRequest = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Result<CharacterRecord> firstResolve = _characterRepository.ResolveAdvancementRecommendation(_campaign, character.CharacterId, firstRequest.Value.RecommendationId, approve: true, spendReservedPoints: true, NewUserId(), actorIsMainGm: true, afterFirstRequest.Value.Revisions.MechanicsRevision, firstRequest.Value.Revision, NewCommandId(), TestCorrelationId);
            Assert.That(firstResolve.IsSuccess, Is.True);

            // A second recommendation, on a different skill, referencing the
            // SAME already-consumed evidence.
            var otherSkill = SkillDefinitionId.Parse("Perception");
            Result<AdvancementRecommendationRecord> secondRequest = _characterRepository.RequestSkillAdvancedRecommendation(_campaign, character.CharacterId, otherSkill, targetLevel: 5, new[] { evidence.Value.EvidenceId }, NewUserId(), actorIsMainGm: true, firstResolve.Value.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(secondRequest.IsSuccess, Is.True);
            Result<CharacterRecord> afterSecondRequest = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);

            Result<CharacterRecord> secondResolve = _characterRepository.ResolveAdvancementRecommendation(_campaign, character.CharacterId, secondRequest.Value.RecommendationId, approve: true, spendReservedPoints: true, NewUserId(), actorIsMainGm: true, afterSecondRequest.Value.Revisions.MechanicsRevision, secondRequest.Value.Revision, NewCommandId(), TestCorrelationId);

            Assert.That(secondResolve.IsFailure, Is.True);
            Assert.That(secondResolve.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRevisionConflict));

            // Direct check of the real evidence state -- still points at the
            // FIRST recommendation, not corrupted or overwritten.
            Result<IReadOnlyList<CriticalSuccessEvidenceRecord>> evidenceList = _characterRepository.GetCriticalSuccessEvidence(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(evidenceList.IsSuccess, Is.True);
            Assert.That(evidenceList.Value.Single().UsedByAdvancementId, Is.EqualTo(firstRequest.Value.RecommendationId));
        }

        // TC-CHAR-057: duplicate CommandId for RequestSkillAdvancedRecommendation/
        // ResolveAdvancementRecommendation/PurchaseSkillLevel does not
        // duplicate the effect -- checked against the real Available/
        // Reserved/Spent values after replay.
        [Test]
        public void PurchaseSkillLevel_DuplicateCommandId_DoesNotDoubleSpend()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 10);
            CommandId commandId = NewCommandId();

            Result<CharacterRecord> first = _characterRepository.PurchaseSkillLevel(_campaign, character.CharacterId, Stealth, toLevel: 2, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, expectedSkillRevision: 0, commandId, TestCorrelationId);
            Assert.That(first.IsSuccess, Is.True);
            Result<CharacterRecord> second = _characterRepository.PurchaseSkillLevel(_campaign, character.CharacterId, Stealth, toLevel: 2, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, expectedSkillRevision: 0, commandId, TestCorrelationId);
            Assert.That(second.IsSuccess, Is.True);

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.IsSuccess, Is.True);
            Assert.That(reRead.Value.DevelopmentPool.Spent, Is.EqualTo(6));
            Assert.That(reRead.Value.DevelopmentPool.Available, Is.EqualTo(4));
        }

        [Test]
        public void RequestSkillAdvancedRecommendation_DuplicateCommandId_DoesNotDoubleReserve()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 20);
            Result<CriticalSuccessEvidenceRecord> evidence = _characterRepository.RecordCriticalSuccessEvidence(_campaign, character.CharacterId, Stealth, "roll_1", null, NewCommandId(), TestCorrelationId);
            CommandId commandId = NewCommandId();

            Result<AdvancementRecommendationRecord> first = _characterRepository.RequestSkillAdvancedRecommendation(_campaign, character.CharacterId, Stealth, targetLevel: 5, new[] { evidence.Value.EvidenceId }, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, commandId, TestCorrelationId);
            Assert.That(first.IsSuccess, Is.True);
            Result<AdvancementRecommendationRecord> second = _characterRepository.RequestSkillAdvancedRecommendation(_campaign, character.CharacterId, Stealth, targetLevel: 5, new[] { evidence.Value.EvidenceId }, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, commandId, TestCorrelationId);
            Assert.That(second.IsSuccess, Is.True);
            Assert.That(second.Value.RecommendationId, Is.EqualTo(first.Value.RecommendationId));

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.IsSuccess, Is.True);
            Assert.That(reRead.Value.DevelopmentPool.Reserved, Is.EqualTo(15));
            Assert.That(reRead.Value.DevelopmentPool.Available, Is.EqualTo(5));
        }

        [Test]
        public void ResolveAdvancementRecommendation_DuplicateCommandId_DoesNotDoubleApply()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 20);
            Result<CriticalSuccessEvidenceRecord> evidence = _characterRepository.RecordCriticalSuccessEvidence(_campaign, character.CharacterId, Stealth, "roll_1", null, NewCommandId(), TestCorrelationId);
            Result<AdvancementRecommendationRecord> requested = _characterRepository.RequestSkillAdvancedRecommendation(_campaign, character.CharacterId, Stealth, targetLevel: 5, new[] { evidence.Value.EvidenceId }, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Result<CharacterRecord> afterRequest = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            CommandId commandId = NewCommandId();

            Result<CharacterRecord> first = _characterRepository.ResolveAdvancementRecommendation(_campaign, character.CharacterId, requested.Value.RecommendationId, approve: true, spendReservedPoints: true, NewUserId(), actorIsMainGm: true, afterRequest.Value.Revisions.MechanicsRevision, requested.Value.Revision, commandId, TestCorrelationId);
            Assert.That(first.IsSuccess, Is.True);
            Result<CharacterRecord> second = _characterRepository.ResolveAdvancementRecommendation(_campaign, character.CharacterId, requested.Value.RecommendationId, approve: true, spendReservedPoints: true, NewUserId(), actorIsMainGm: true, afterRequest.Value.Revisions.MechanicsRevision, requested.Value.Revision, commandId, TestCorrelationId);
            Assert.That(second.IsSuccess, Is.True);

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.IsSuccess, Is.True);
            Assert.That(reRead.Value.DevelopmentPool.Spent, Is.EqualTo(15));
            Assert.That(reRead.Value.Skills.Single(s => s.SkillDefinitionId.Equals(Stealth)).Level, Is.EqualTo(5));
        }

        // TC-CHAR-058: a concurrent edit to Mechanics (skill purchase) and
        // Identity commits without a false conflict.
        [Test]
        public void SkillPurchase_AndIdentityEdit_BothCommit_NoFalseConflict()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 10);

            Result<CharacterRecord> purchaseResult = _characterRepository.PurchaseSkillLevel(_campaign, character.CharacterId, Stealth, toLevel: 1, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, expectedSkillRevision: 0, NewCommandId(), TestCorrelationId);
            Result<CharacterRecord> identityResult = _characterRepository.UpdateIdentity(_campaign, character.CharacterId, "Renamed Skill Character", character.Revisions.IdentityRevision, NewCommandId(), TestCorrelationId);

            Assert.That(purchaseResult.IsSuccess, Is.True);
            Assert.That(identityResult.IsSuccess, Is.True);

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.IsSuccess, Is.True);
            Assert.That(reRead.Value.DisplayName, Is.EqualTo("Renamed Skill Character"));
            Assert.That(reRead.Value.Skills.Single().Level, Is.EqualTo(1));
        }
    }
}
