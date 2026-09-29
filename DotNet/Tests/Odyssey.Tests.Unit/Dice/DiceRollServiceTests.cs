using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Dice;
using Odyssey.Application.Identity;
using Odyssey.Application.Persistence;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Rules.Versions;

namespace Odyssey.Tests.Unit.Dice
{
    /// <summary>
    /// ODY-S03-005: host-authoritative dice roll engine tests, real
    /// <see cref="DeterministicRandomStreamFactory"/>/<see cref="DiceRollStore"/>
    /// (not a fake RNG) -- this is the only production RNG path, per
    /// 09_Dice_And_Game_Log section 14.2.
    ///
    /// ODY-S10-105: this project has no reference to Odyssey.Persistence, so
    /// MainGM-ness is proven against a small in-memory <see cref="FakeCampaignRepository"/>
    /// (below) rather than the real SQLite-backed one every Persistence-layer test in this
    /// track uses -- same contract (<see cref="ICampaignRepository"/>), same
    /// <see cref="CampaignMembershipAuthorization.IsMainGm"/> call, just an in-memory store.
    /// </summary>
    public sealed class DiceRollServiceTests
    {
        private static readonly IWallClock Clock = new SystemWallClock();
        private static readonly CampaignId TestCampaignId = CampaignId.Parse("camp_0123456789abcdef0123456789abcdef");
        private static readonly CampaignHandle TestCampaign = NewCampaignHandle(TestCampaignId);
        private static readonly RulesetVersion TestRulesetVersion = RulesetVersion.Parse("1.0.0");
        private static readonly RngKeyEpochId TestEpoch = RngKeyEpochId.Parse("epoch-001");
        private static readonly UserId Host = global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost();

        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
        private static CorrelationId NewCorrelationId() => CorrelationId.Parse("corr_" + Guid.NewGuid().ToString("N"));

        private static IAuthoritativeRandomStreamFactory NewRngFactory()
        {
            byte[] key = new byte[32];
            for (int index = 0; index < key.Length; index++) key[index] = (byte)(index + 1);
            return new DeterministicRandomStreamFactory(CampaignRngKey.FromBytes(key));
        }

        private static CampaignHandle NewCampaignHandle(CampaignId campaignId)
        {
            var manifest = new CampaignManifest(campaignId, "Dice Roll Service Test Campaign", "1.0.0", "1.0.0", "ruleset.core", "1.0.0",
                UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow), UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow), "0.1.0", 1, isTemplate: false);
            return new CampaignHandle(campaignId, CampaignPublicId.NewId(UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow)), "unused-in-memory-root", manifest);
        }

        /// <summary>Host is registered as the campaign's stored MainGm by default -- every prior *_IsMainGm-only test in this file relied on the host as "a" MainGm, so this keeps that meaning real rather than claimed.</summary>
        private static FakeCampaignRepository NewCampaignRepository()
        {
            var repository = new FakeCampaignRepository();
            Assert.That(repository.AddMember(TestCampaign, Host, CampaignMembershipRole.MainGm, NewCommandId(), NewCorrelationId()).IsSuccess, Is.True);
            return repository;
        }

        [Test]
        public void SubmitRoll_ByAuthorizedActor_GeneratesResult_HostOnly()
        {
            // TC-DICE-005: correct formula result, exit criterion 3 ("бросок рассчитывается только host").
            var store = new DiceRollStore();
            var request = new SubmitRollRequest(NewUserId(), actorCanCreateRoll: true, "AttributeCheck", "1d100", DiceRollAudience.Public(), TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId());

            Result<DiceRoll> result = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.NaturalResults.Count, Is.EqualTo(1));
            Assert.That(result.Value.NaturalResults[0].Value, Is.InRange(1, 100));
            Assert.That(result.Value.FinalTotal, Is.EqualTo(result.Value.NaturalResults[0].Value));
            Assert.That(result.Value.Status, Is.EqualTo(DiceRollStatus.Resolved));
            Assert.That(result.Value.RngAlgorithmVersion, Is.EqualTo(RandomDecisionContext.RngAlgorithmVersion));
        }

        [Test]
        public void SubmitRoll_CompoundFormula_SumsAllTerms()
        {
            // TC-DICE-005
            var store = new DiceRollStore();
            var request = new SubmitRollRequest(NewUserId(), actorCanCreateRoll: true, "AttackRoll", "2d6+3", DiceRollAudience.Public(), TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId());

            Result<DiceRoll> result = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.NaturalResults.Count, Is.EqualTo(2));
            int expectedBase = result.Value.NaturalResults[0].Value + result.Value.NaturalResults[1].Value + 3;
            Assert.That(result.Value.BaseTotal, Is.EqualTo(expectedBase));
            Assert.That(result.Value.FinalTotal, Is.EqualTo(expectedBase));
        }

        [Test]
        public void SubmitRoll_WithoutPermission_IsRejected_NoRollGenerated()
        {
            // TC-DICE-006 (exit criterion 3's counterpart: no unauthorized generation)
            var store = new DiceRollStore();
            var request = new SubmitRollRequest(NewUserId(), actorCanCreateRoll: false, "AttributeCheck", "1d20", DiceRollAudience.Public(), TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId());

            Result<DiceRoll> result = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.DiceRollDenied));
            Assert.That(result.Error.SafeReasonCode, Is.EqualTo(SafeReasonCode.PermissionDenied));
        }

        [Test]
        public void SubmitRoll_InvalidFormula_IsRejected_BeforeRng()
        {
            // TC-DICE-007
            var store = new DiceRollStore();
            var request = new SubmitRollRequest(NewUserId(), actorCanCreateRoll: true, "AttributeCheck", "(2d6+3)*2", DiceRollAudience.Public(), TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId());

            Result<DiceRoll> result = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.DiceInvalidFormula));
        }

        [Test]
        public void ProposeThenDecideModifier_AcceptedValue_AppearsInFinalTotal_AsVisibleSourcedEntry()
        {
            // TC-DICE-008: modifier proposed and accepted as two separate, visible steps (section 12.2/12.3).
            var store = new DiceRollStore();
            var campaigns = NewCampaignRepository();
            UserId actor = NewUserId();
            DiceRoll roll = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, new SubmitRollRequest(actor, true, "SkillCheckPlayer", "1d20", DiceRollAudience.Public(), TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;

            Result<DiceRoll> proposed = DiceRollService.ProposeModifier(store, new ProposeModifierRequest(roll.RollId, actor, "Terrain", "Высокая позиция", 5, NewCorrelationId()));
            Assert.That(proposed.IsSuccess, Is.True);
            Assert.That(proposed.Value.ModifierEntries.Count, Is.EqualTo(1));
            Assert.That(proposed.Value.ModifierEntries[0].Decision, Is.EqualTo(ModifierDecision.Proposed));
            Assert.That(proposed.Value.FinalTotal, Is.EqualTo(roll.BaseTotal), "a merely-proposed modifier must not yet count toward FinalTotal");

            string modifierEntryId = proposed.Value.ModifierEntries[0].ModifierEntryId;
            Result<DiceRoll> decided = DiceRollService.DecideModifier(store, campaigns, new DecideModifierRequest(TestCampaign, roll.RollId, modifierEntryId, Host, ModifierDecision.Accepted, changedValue: null, reason: null, NewCorrelationId()));

            Assert.That(decided.IsSuccess, Is.True);
            Assert.That(decided.Value.ModifierEntries[0].Decision, Is.EqualTo(ModifierDecision.Accepted));
            Assert.That(decided.Value.ModifierEntries[0].AppliedValue, Is.EqualTo(5));
            Assert.That(decided.Value.FinalTotal, Is.EqualTo(roll.BaseTotal + 5));
        }

        [Test]
        public void DecideModifier_ChangedOrRejected_WithoutReason_IsRejected()
        {
            // TC-DICE-009 (section 12.2: GM must give a reason for Change/Reject)
            var store = new DiceRollStore();
            var campaigns = NewCampaignRepository();
            UserId actor = NewUserId();
            DiceRoll roll = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, new SubmitRollRequest(actor, true, "SkillCheckPlayer", "1d20", DiceRollAudience.Public(), TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;
            DiceRoll withProposal = DiceRollService.ProposeModifier(store, new ProposeModifierRequest(roll.RollId, actor, "Ally", "Помощь союзника", 5, NewCorrelationId())).Value;
            string modifierEntryId = withProposal.ModifierEntries[0].ModifierEntryId;

            Result<DiceRoll> rejectedWithoutReason = DiceRollService.DecideModifier(store, campaigns, new DecideModifierRequest(TestCampaign, roll.RollId, modifierEntryId, Host, ModifierDecision.Rejected, null, reason: null, NewCorrelationId()));

            Assert.That(rejectedWithoutReason.IsFailure, Is.True);
            Assert.That(rejectedWithoutReason.Error.Code, Is.EqualTo(ErrorCodes.DiceModifierDecisionReasonRequired));

            DiceRoll unchanged = store.TryGet(roll.RollId, out DiceRoll current) ? current : null!;
            Assert.That(unchanged.ModifierEntries[0].Decision, Is.EqualTo(ModifierDecision.Proposed), "the modifier must remain Proposed, not silently rejected");
        }

        [Test] // ODY-S10-105: TC-DICE-024 -- an unregistered user, a registered Player and a registered Observer are all denied; the host and a second, separately-registered MainGm are let through.
        public void DecideModifier_AreMainGmOnly_ByTheStoredMembership()
        {
            var store = new DiceRollStore();
            var campaigns = NewCampaignRepository();
            UserId stranger = NewUserId();
            UserId player = NewUserId();
            Assert.That(campaigns.AddMember(TestCampaign, player, CampaignMembershipRole.Player, NewCommandId(), NewCorrelationId()).IsSuccess, Is.True);
            UserId observer = NewUserId();
            Assert.That(campaigns.AddMember(TestCampaign, observer, CampaignMembershipRole.Observer, NewCommandId(), NewCorrelationId()).IsSuccess, Is.True);
            UserId secondGm = NewUserId();
            Assert.That(campaigns.AddMember(TestCampaign, secondGm, CampaignMembershipRole.MainGm, NewCommandId(), NewCorrelationId()).IsSuccess, Is.True);

            UserId actor = NewUserId();
            DiceRoll roll = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, new SubmitRollRequest(actor, true, "SkillCheckPlayer", "1d20", DiceRollAudience.Public(), TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;
            DiceRoll withProposal = DiceRollService.ProposeModifier(store, new ProposeModifierRequest(roll.RollId, actor, "Ally", "Помощь союзника", 5, NewCorrelationId())).Value;
            string modifierEntryId = withProposal.ModifierEntries[0].ModifierEntryId;

            foreach (UserId denied in new[] { stranger, player, observer })
            {
                Result<DiceRoll> result = DiceRollService.DecideModifier(store, campaigns, new DecideModifierRequest(TestCampaign, roll.RollId, modifierEntryId, denied, ModifierDecision.Accepted, null, null, NewCorrelationId()));
                Assert.That(result.IsFailure, Is.True, "DecideModifier must deny a user who is not a stored MainGm");
                Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.DiceModifierDecisionDenied));
            }

            Assert.That(DiceRollService.DecideModifier(store, campaigns, new DecideModifierRequest(TestCampaign, roll.RollId, modifierEntryId, secondGm, ModifierDecision.Accepted, null, null, NewCorrelationId())).IsSuccess, Is.True, "a genuinely registered MainGm must be authorized");
        }

        [Test] // ODY-S10-105: TC-DICE-025 -- fail-closed when the membership lookup itself cannot be read.
        public void DecideModifier_FailsClosed_WhenTheMembershipLookupFails()
        {
            var store = new DiceRollStore();
            var poisoned = PoisonedCampaignRepository.FailsOnLookup();
            UserId actor = NewUserId();
            DiceRoll roll = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, new SubmitRollRequest(actor, true, "SkillCheckPlayer", "1d20", DiceRollAudience.Public(), TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;
            DiceRoll withProposal = DiceRollService.ProposeModifier(store, new ProposeModifierRequest(roll.RollId, actor, "Ally", "Помощь союзника", 5, NewCorrelationId())).Value;
            string modifierEntryId = withProposal.ModifierEntries[0].ModifierEntryId;

            Result<DiceRoll> result = DiceRollService.DecideModifier(store, poisoned, new DecideModifierRequest(TestCampaign, roll.RollId, modifierEntryId, Host, ModifierDecision.Accepted, null, null, NewCorrelationId()));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCampaignIoFailed), "an unreadable membership is the lookup's own failure, not a pass and not a fake denial -- even for the host");
            Assert.That(poisoned.LookupCalls, Is.EqualTo(1));
            DiceRoll unchanged = store.TryGet(roll.RollId, out DiceRoll current) ? current : null!;
            Assert.That(unchanged.ModifierEntries[0].Decision, Is.EqualTo(ModifierDecision.Proposed));
        }

        [Test]
        public void ApplyOverride_WithoutReason_IsRejected()
        {
            // TC-DICE-011 (exit criterion 6: "GM Override всегда оставляет audit trail" -- mandatory reason is part of that trail)
            var store = new DiceRollStore();
            var campaigns = NewCampaignRepository();
            UserId actor = NewUserId();
            DiceRoll roll = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, new SubmitRollRequest(actor, true, "SkillCheckPlayer", "1d20", DiceRollAudience.Public(), TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;

            Result<RollOverride> result = DiceRollService.ApplyOverride(store, campaigns, Clock, new ApplyOverrideRequest(TestCampaign, roll.RollId, Host, "Failure", "Success", reason: null, NewCorrelationId()));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.DiceOverrideReasonRequired));
        }

        [Test]
        public void ApplyOverride_ByNonMainGm_IsRejected()
        {
            // TC-DICE-012
            var store = new DiceRollStore();
            var campaigns = NewCampaignRepository();
            UserId actor = NewUserId();
            DiceRoll roll = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, new SubmitRollRequest(actor, true, "SkillCheckPlayer", "1d20", DiceRollAudience.Public(), TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;

            Result<RollOverride> result = DiceRollService.ApplyOverride(store, campaigns, Clock, new ApplyOverrideRequest(TestCampaign, roll.RollId, actor, "Failure", "Success", "story reason", NewCorrelationId()));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.DiceOverrideDenied));
        }

        [Test]
        public void ApplyOverride_WithReason_Succeeds_OriginalRollUnchanged_OverrideIsSeparateRecord()
        {
            // TC-DICE-013 (section 19.2: original roll is never edited; exit criterion 6)
            var store = new DiceRollStore();
            var campaigns = NewCampaignRepository();
            UserId actor = NewUserId();
            DiceRoll original = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, new SubmitRollRequest(actor, true, "SkillCheckPlayer", "1d20", DiceRollAudience.Public(), TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;
            int originalNatural = original.NaturalResults[0].Value;
            int originalFinalTotal = original.FinalTotal;

            Result<RollOverride> overrideResult = DiceRollService.ApplyOverride(store, campaigns, Clock, new ApplyOverrideRequest(TestCampaign, original.RollId, Host, "Failure", "Success", "сюжетное решение", NewCorrelationId()));

            Assert.That(overrideResult.IsSuccess, Is.True);
            Assert.That(overrideResult.Value.Reason, Is.EqualTo("сюжетное решение"));
            Assert.That(overrideResult.Value.OverrideId, Is.Not.EqualTo(original.RollId), "the override is its own record, not the roll itself");

            DiceRoll afterOverride = store.TryGet(original.RollId, out DiceRoll current) ? current : null!;
            Assert.That(afterOverride.NaturalResults[0].Value, Is.EqualTo(originalNatural), "NaturalResults must never change");
            Assert.That(afterOverride.FinalTotal, Is.EqualTo(originalFinalTotal), "FinalTotal must never be rewritten by an override");
            Assert.That(afterOverride.Status, Is.EqualTo(DiceRollStatus.Overridden), "only the Status marker flips");

            var overrides = store.GetOverrides(original.RollId);
            Assert.That(overrides.Count, Is.EqualTo(1));
            Assert.That(overrides[0].OriginalInterpretation, Is.EqualTo("Failure"));
            Assert.That(overrides[0].AppliedInterpretation, Is.EqualTo("Success"));
        }

        [Test] // ODY-S10-105: TC-DICE-026 -- fail-closed when the membership lookup itself cannot be read.
        public void ApplyOverride_FailsClosed_WhenTheMembershipLookupFails()
        {
            var store = new DiceRollStore();
            var poisoned = PoisonedCampaignRepository.FailsOnLookup();
            UserId actor = NewUserId();
            DiceRoll roll = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, new SubmitRollRequest(actor, true, "SkillCheckPlayer", "1d20", DiceRollAudience.Public(), TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;

            Result<RollOverride> result = DiceRollService.ApplyOverride(store, poisoned, Clock, new ApplyOverrideRequest(TestCampaign, roll.RollId, Host, "Failure", "Success", "reason", NewCorrelationId()));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCampaignIoFailed), "an unreadable membership is the lookup's own failure, not a pass and not a fake denial -- even for the host");
            Assert.That(poisoned.LookupCalls, Is.EqualTo(1));
            Assert.That(store.GetOverrides(roll.RollId).Count, Is.EqualTo(0));
        }

        [Test]
        public void RequestFullReroll_CreatesNewRoll_OriginalPreservedAsSuperseded()
        {
            // TC-DICE-014 (roadmap section 12.6 step 10: "original event remains after reroll/cancel"; section 17.4)
            var store = new DiceRollStore();
            var campaigns = NewCampaignRepository();
            UserId actor = NewUserId();
            var rngFactory = NewRngFactory();
            DiceRoll original = DiceRollService.SubmitRoll(store, rngFactory, Clock, new SubmitRollRequest(actor, true, "AttackRoll", "1d20", DiceRollAudience.Public(), TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;

            var rerollRequest = new RequestFullRerollRequest(TestCampaign, original.RollId, actor, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId());
            Result<DiceRoll> rerollResult = DiceRollService.RequestFullReroll(store, campaigns, rngFactory, Clock, rerollRequest);

            Assert.That(rerollResult.IsSuccess, Is.True);
            Assert.That(rerollResult.Value.RollId, Is.Not.EqualTo(original.RollId), "reroll must be a new record");
            Assert.That(rerollResult.Value.PreviousRollId, Is.EqualTo(original.RollId));
            Assert.That(rerollResult.Value.FormulaOriginal, Is.EqualTo(original.FormulaOriginal));

            DiceRoll originalAfterReroll = store.TryGet(original.RollId, out DiceRoll current) ? current : null!;
            Assert.That(originalAfterReroll.Status, Is.EqualTo(DiceRollStatus.SupersededByReroll));
            Assert.That(originalAfterReroll.NaturalResults[0].Value, Is.EqualTo(original.NaturalResults[0].Value), "the original roll's own data is preserved, not deleted or rewritten");
        }

        [Test]
        public void RequestFullReroll_ByNonActorNonMainGm_IsRejected()
        {
            // TC-DICE-015
            var store = new DiceRollStore();
            var campaigns = NewCampaignRepository();
            UserId actor = NewUserId();
            UserId other = NewUserId();
            var rngFactory = NewRngFactory();
            DiceRoll original = DiceRollService.SubmitRoll(store, rngFactory, Clock, new SubmitRollRequest(actor, true, "AttackRoll", "1d20", DiceRollAudience.Public(), TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;

            var rerollRequest = new RequestFullRerollRequest(TestCampaign, original.RollId, other, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId());
            Result<DiceRoll> result = DiceRollService.RequestFullReroll(store, campaigns, rngFactory, Clock, rerollRequest);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.DiceRerollDenied));

            DiceRoll unchanged = store.TryGet(original.RollId, out DiceRoll current) ? current : null!;
            Assert.That(unchanged.Status, Is.EqualTo(DiceRollStatus.Resolved), "a rejected reroll must not supersede the original");
        }

        [Test] // ODY-S10-105: TC-DICE-027 -- a registered-but-not-MainGm Player is still denied for someone else's roll; the same real, separately-registered MainGm succeeds. Also proves the cheap "own roll" fast path is not skipped: no lookup at all is made for the roll's own actor.
        public void RequestFullReroll_RegisteredPlayerStillDenied_RealMainGmSucceeds_OwnActorNeedsNoLookup()
        {
            var store = new DiceRollStore();
            var campaigns = NewCampaignRepository();
            UserId actor = NewUserId();
            UserId player = NewUserId();
            Assert.That(campaigns.AddMember(TestCampaign, player, CampaignMembershipRole.Player, NewCommandId(), NewCorrelationId()).IsSuccess, Is.True);
            var rngFactory = NewRngFactory();
            DiceRoll original = DiceRollService.SubmitRoll(store, rngFactory, Clock, new SubmitRollRequest(actor, true, "AttackRoll", "1d20", DiceRollAudience.Public(), TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;

            Result<DiceRoll> deniedForPlayer = DiceRollService.RequestFullReroll(store, campaigns, rngFactory, Clock, new RequestFullRerollRequest(TestCampaign, original.RollId, player, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId()));
            Assert.That(deniedForPlayer.IsFailure, Is.True, "a registered Player is not a MainGm and must still be denied");
            Assert.That(deniedForPlayer.Error.Code, Is.EqualTo(ErrorCodes.DiceRerollDenied));

            var poisonedForOwnActor = PoisonedCampaignRepository.FailsOnLookup();
            Result<DiceRoll> ownActorResult = DiceRollService.RequestFullReroll(store, poisonedForOwnActor, rngFactory, Clock, new RequestFullRerollRequest(TestCampaign, original.RollId, actor, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId()));
            Assert.That(ownActorResult.IsSuccess, Is.True, "the roll's own actor must succeed via the cheap fast path, never reaching the (here, poisoned) membership lookup");
            Assert.That(poisonedForOwnActor.LookupCalls, Is.EqualTo(0), "requesting a reroll of one's own roll must never call the membership lookup at all");
        }

        [Test]
        public void CancelRoll_ResolvedRoll_WithoutReason_IsRejected()
        {
            // TC-DICE-016 (section 18.3: mandatory reason for a resolved roll)
            var store = new DiceRollStore();
            var campaigns = NewCampaignRepository();
            UserId actor = NewUserId();
            DiceRoll roll = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, new SubmitRollRequest(actor, true, "AttributeCheck", "1d20", DiceRollAudience.Public(), TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;

            Result<DiceRoll> result = DiceRollService.CancelRoll(store, campaigns, new CancelRollRequest(TestCampaign, roll.RollId, actor, reason: null, NewCorrelationId()));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.DiceCancelReasonRequired));
        }

        [Test]
        public void CancelRoll_WithReason_Succeeds_OriginalDataPreserved()
        {
            // TC-DICE-017 (roadmap section 12.6 step 10: "...cancel"; original event remains)
            var store = new DiceRollStore();
            var campaigns = NewCampaignRepository();
            UserId actor = NewUserId();
            DiceRoll roll = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, new SubmitRollRequest(actor, true, "AttributeCheck", "1d20", DiceRollAudience.Public(), TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;
            int naturalValue = roll.NaturalResults[0].Value;

            Result<DiceRoll> result = DiceRollService.CancelRoll(store, campaigns, new CancelRollRequest(TestCampaign, roll.RollId, actor, "player disconnected", NewCorrelationId()));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Status, Is.EqualTo(DiceRollStatus.Cancelled));
            Assert.That(result.Value.NaturalResults[0].Value, Is.EqualTo(naturalValue), "cancellation must not delete or rewrite the roll's own data");
        }

        [Test]
        public void CancelRoll_ByNonActorNonMainGm_IsRejected()
        {
            // TC-DICE-018
            var store = new DiceRollStore();
            var campaigns = NewCampaignRepository();
            UserId actor = NewUserId();
            UserId other = NewUserId();
            DiceRoll roll = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, new SubmitRollRequest(actor, true, "AttributeCheck", "1d20", DiceRollAudience.Public(), TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;

            Result<DiceRoll> result = DiceRollService.CancelRoll(store, campaigns, new CancelRollRequest(TestCampaign, roll.RollId, other, "trying to cancel someone else's roll", NewCorrelationId()));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.DiceCancelDenied));
        }

        [Test] // ODY-S10-105: TC-DICE-028 -- a registered-but-not-MainGm Player is still denied when cancelling someone else's roll; a real, separately-registered MainGm succeeds.
        public void CancelRoll_RegisteredPlayerStillDenied_RealMainGmSucceeds()
        {
            var store = new DiceRollStore();
            var campaigns = NewCampaignRepository();
            UserId actor = NewUserId();
            UserId player = NewUserId();
            Assert.That(campaigns.AddMember(TestCampaign, player, CampaignMembershipRole.Player, NewCommandId(), NewCorrelationId()).IsSuccess, Is.True);
            UserId secondGm = NewUserId();
            Assert.That(campaigns.AddMember(TestCampaign, secondGm, CampaignMembershipRole.MainGm, NewCommandId(), NewCorrelationId()).IsSuccess, Is.True);
            DiceRoll roll = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, new SubmitRollRequest(actor, true, "AttributeCheck", "1d20", DiceRollAudience.Public(), TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;

            Result<DiceRoll> denied = DiceRollService.CancelRoll(store, campaigns, new CancelRollRequest(TestCampaign, roll.RollId, player, "not my roll", NewCorrelationId()));
            Assert.That(denied.IsFailure, Is.True, "a registered Player is not a MainGm and must still be denied");
            Assert.That(denied.Error.Code, Is.EqualTo(ErrorCodes.DiceCancelDenied));

            Assert.That(DiceRollService.CancelRoll(store, campaigns, new CancelRollRequest(TestCampaign, roll.RollId, secondGm, "GM call", NewCorrelationId())).IsSuccess, Is.True, "a genuinely registered MainGm must be authorized");
        }

        private sealed class SystemWallClock : IWallClock
        {
            public UtcInstant GetUtcNow() => UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow);
        }

        /// <summary>A minimal in-memory <see cref="ICampaignRepository"/> -- this project has no reference to the real SQLite one.</summary>
        private sealed class FakeCampaignRepository : ICampaignRepository
        {
            private readonly Dictionary<string, CampaignMembershipRole> _members = new Dictionary<string, CampaignMembershipRole>();

            public Result<CampaignMembership> AddMember(CampaignHandle campaign, UserId userId, CampaignMembershipRole role, CommandId commandId, CorrelationId correlationId)
            {
                string key = userId.ToString();
                if (_members.ContainsKey(key))
                {
                    return Result<CampaignMembership>.Failure(PersistenceFailures.CampaignIoFailed(correlationId));
                }

                _members[key] = role;
                return Result<CampaignMembership>.Success(new CampaignMembership(userId, campaign.CampaignId, role, 1,
                    UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow), UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow)));
            }

            public Result<CampaignMemberLookup> GetMemberRole(CampaignHandle campaign, UserId userId, CorrelationId correlationId)
            {
                return _members.TryGetValue(userId.ToString(), out CampaignMembershipRole role)
                    ? Result<CampaignMemberLookup>.Success(CampaignMemberLookup.Member(role))
                    : Result<CampaignMemberLookup>.Success(CampaignMemberLookup.NotAMember);
            }

            public Result<CampaignHandle> Create(CreateCampaignRequest request, CommandId commandId, CorrelationId correlationId) => throw new NotSupportedException();
            public Result<CampaignHandle> Open(string campaignFolderPath, CorrelationId correlationId) => throw new NotSupportedException();
            public Result Close(CampaignHandle handle, CorrelationId correlationId) => throw new NotSupportedException();
            public Result<IReadOnlyList<CampaignMembership>> ListMembers(CampaignHandle campaign, CorrelationId correlationId) => throw new NotSupportedException();
        }

        /// <summary>A campaign repository whose membership lookup always fails -- everything else is unused by these checks.</summary>
        private sealed class PoisonedCampaignRepository : ICampaignRepository
        {
            public static PoisonedCampaignRepository FailsOnLookup() => new PoisonedCampaignRepository();

            public int LookupCalls { get; private set; }

            public Result<CampaignMemberLookup> GetMemberRole(CampaignHandle campaign, UserId userId, CorrelationId correlationId)
            {
                LookupCalls++;
                return Result<CampaignMemberLookup>.Failure(PersistenceFailures.CampaignIoFailed(correlationId));
            }

            public Result<CampaignHandle> Create(CreateCampaignRequest request, CommandId commandId, CorrelationId correlationId) => throw new NotSupportedException();
            public Result<CampaignHandle> Open(string campaignFolderPath, CorrelationId correlationId) => throw new NotSupportedException();
            public Result Close(CampaignHandle handle, CorrelationId correlationId) => throw new NotSupportedException();
            public Result<CampaignMembership> AddMember(CampaignHandle campaign, UserId userId, CampaignMembershipRole role, CommandId commandId, CorrelationId correlationId) => throw new NotSupportedException();
            public Result<IReadOnlyList<CampaignMembership>> ListMembers(CampaignHandle campaign, CorrelationId correlationId) => throw new NotSupportedException();
        }
    }
}
