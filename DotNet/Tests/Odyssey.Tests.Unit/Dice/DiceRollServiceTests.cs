using System;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Dice;
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
    /// </summary>
    public sealed class DiceRollServiceTests
    {
        private static readonly IWallClock Clock = new SystemWallClock();
        private static readonly CampaignId TestCampaignId = CampaignId.Parse("camp_0123456789abcdef0123456789abcdef");
        private static readonly RulesetVersion TestRulesetVersion = RulesetVersion.Parse("1.0.0");
        private static readonly RngKeyEpochId TestEpoch = RngKeyEpochId.Parse("epoch-001");

        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
        private static CorrelationId NewCorrelationId() => CorrelationId.Parse("corr_" + Guid.NewGuid().ToString("N"));

        private static IAuthoritativeRandomStreamFactory NewRngFactory()
        {
            byte[] key = new byte[32];
            for (int index = 0; index < key.Length; index++) key[index] = (byte)(index + 1);
            return new DeterministicRandomStreamFactory(CampaignRngKey.FromBytes(key));
        }

        [Test]
        public void SubmitRoll_ByAuthorizedActor_GeneratesResult_HostOnly()
        {
            // TC-DICE-005: correct formula result, exit criterion 3 ("бросок рассчитывается только host").
            var store = new DiceRollStore();
            var request = new SubmitRollRequest(NewUserId(), actorCanCreateRoll: true, "AttributeCheck", "1d100", TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId());

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
            var request = new SubmitRollRequest(NewUserId(), actorCanCreateRoll: true, "AttackRoll", "2d6+3", TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId());

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
            var request = new SubmitRollRequest(NewUserId(), actorCanCreateRoll: false, "AttributeCheck", "1d20", TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId());

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
            var request = new SubmitRollRequest(NewUserId(), actorCanCreateRoll: true, "AttributeCheck", "(2d6+3)*2", TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId());

            Result<DiceRoll> result = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.DiceInvalidFormula));
        }

        [Test]
        public void ProposeThenDecideModifier_AcceptedValue_AppearsInFinalTotal_AsVisibleSourcedEntry()
        {
            // TC-DICE-008: modifier proposed and accepted as two separate, visible steps (section 12.2/12.3).
            var store = new DiceRollStore();
            UserId actor = NewUserId();
            UserId gm = NewUserId();
            DiceRoll roll = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, new SubmitRollRequest(actor, true, "SkillCheckPlayer", "1d20", TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;

            Result<DiceRoll> proposed = DiceRollService.ProposeModifier(store, new ProposeModifierRequest(roll.RollId, actor, "Terrain", "Высокая позиция", 5, NewCorrelationId()));
            Assert.That(proposed.IsSuccess, Is.True);
            Assert.That(proposed.Value.ModifierEntries.Count, Is.EqualTo(1));
            Assert.That(proposed.Value.ModifierEntries[0].Decision, Is.EqualTo(ModifierDecision.Proposed));
            Assert.That(proposed.Value.FinalTotal, Is.EqualTo(roll.BaseTotal), "a merely-proposed modifier must not yet count toward FinalTotal");

            string modifierEntryId = proposed.Value.ModifierEntries[0].ModifierEntryId;
            Result<DiceRoll> decided = DiceRollService.DecideModifier(store, new DecideModifierRequest(roll.RollId, modifierEntryId, gm, decidedByUserIsMainGm: true, ModifierDecision.Accepted, changedValue: null, reason: null, NewCorrelationId()));

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
            UserId actor = NewUserId();
            UserId gm = NewUserId();
            DiceRoll roll = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, new SubmitRollRequest(actor, true, "SkillCheckPlayer", "1d20", TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;
            DiceRoll withProposal = DiceRollService.ProposeModifier(store, new ProposeModifierRequest(roll.RollId, actor, "Ally", "Помощь союзника", 5, NewCorrelationId())).Value;
            string modifierEntryId = withProposal.ModifierEntries[0].ModifierEntryId;

            Result<DiceRoll> rejectedWithoutReason = DiceRollService.DecideModifier(store, new DecideModifierRequest(roll.RollId, modifierEntryId, gm, true, ModifierDecision.Rejected, null, reason: null, NewCorrelationId()));

            Assert.That(rejectedWithoutReason.IsFailure, Is.True);
            Assert.That(rejectedWithoutReason.Error.Code, Is.EqualTo(ErrorCodes.DiceModifierDecisionReasonRequired));

            DiceRoll unchanged = store.TryGet(roll.RollId, out DiceRoll current) ? current : null!;
            Assert.That(unchanged.ModifierEntries[0].Decision, Is.EqualTo(ModifierDecision.Proposed), "the modifier must remain Proposed, not silently rejected");
        }

        [Test]
        public void DecideModifier_ByNonMainGm_IsRejected()
        {
            // TC-DICE-010
            var store = new DiceRollStore();
            UserId actor = NewUserId();
            UserId otherPlayer = NewUserId();
            DiceRoll roll = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, new SubmitRollRequest(actor, true, "SkillCheckPlayer", "1d20", TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;
            DiceRoll withProposal = DiceRollService.ProposeModifier(store, new ProposeModifierRequest(roll.RollId, actor, "Ally", "Помощь союзника", 5, NewCorrelationId())).Value;
            string modifierEntryId = withProposal.ModifierEntries[0].ModifierEntryId;

            Result<DiceRoll> result = DiceRollService.DecideModifier(store, new DecideModifierRequest(roll.RollId, modifierEntryId, otherPlayer, decidedByUserIsMainGm: false, ModifierDecision.Accepted, null, null, NewCorrelationId()));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.DiceModifierDecisionDenied));
        }

        [Test]
        public void ApplyOverride_WithoutReason_IsRejected()
        {
            // TC-DICE-011 (exit criterion 6: "GM Override всегда оставляет audit trail" -- mandatory reason is part of that trail)
            var store = new DiceRollStore();
            UserId actor = NewUserId();
            UserId gm = NewUserId();
            DiceRoll roll = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, new SubmitRollRequest(actor, true, "SkillCheckPlayer", "1d20", TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;

            Result<RollOverride> result = DiceRollService.ApplyOverride(store, Clock, new ApplyOverrideRequest(roll.RollId, gm, actorIsMainGm: true, "Failure", "Success", reason: null, NewCorrelationId()));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.DiceOverrideReasonRequired));
        }

        [Test]
        public void ApplyOverride_ByNonMainGm_IsRejected()
        {
            // TC-DICE-012
            var store = new DiceRollStore();
            UserId actor = NewUserId();
            DiceRoll roll = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, new SubmitRollRequest(actor, true, "SkillCheckPlayer", "1d20", TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;

            Result<RollOverride> result = DiceRollService.ApplyOverride(store, Clock, new ApplyOverrideRequest(roll.RollId, actor, actorIsMainGm: false, "Failure", "Success", "story reason", NewCorrelationId()));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.DiceOverrideDenied));
        }

        [Test]
        public void ApplyOverride_WithReason_Succeeds_OriginalRollUnchanged_OverrideIsSeparateRecord()
        {
            // TC-DICE-013 (section 19.2: original roll is never edited; exit criterion 6)
            var store = new DiceRollStore();
            UserId actor = NewUserId();
            UserId gm = NewUserId();
            DiceRoll original = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, new SubmitRollRequest(actor, true, "SkillCheckPlayer", "1d20", TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;
            int originalNatural = original.NaturalResults[0].Value;
            int originalFinalTotal = original.FinalTotal;

            Result<RollOverride> overrideResult = DiceRollService.ApplyOverride(store, Clock, new ApplyOverrideRequest(original.RollId, gm, actorIsMainGm: true, "Failure", "Success", "сюжетное решение", NewCorrelationId()));

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

        [Test]
        public void RequestFullReroll_CreatesNewRoll_OriginalPreservedAsSuperseded()
        {
            // TC-DICE-014 (roadmap section 12.6 step 10: "original event remains after reroll/cancel"; section 17.4)
            var store = new DiceRollStore();
            UserId actor = NewUserId();
            var rngFactory = NewRngFactory();
            DiceRoll original = DiceRollService.SubmitRoll(store, rngFactory, Clock, new SubmitRollRequest(actor, true, "AttackRoll", "1d20", TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;

            var rerollRequest = new RequestFullRerollRequest(original.RollId, actor, actorIsMainGm: false, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId());
            Result<DiceRoll> rerollResult = DiceRollService.RequestFullReroll(store, rngFactory, Clock, rerollRequest);

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
            UserId actor = NewUserId();
            UserId other = NewUserId();
            var rngFactory = NewRngFactory();
            DiceRoll original = DiceRollService.SubmitRoll(store, rngFactory, Clock, new SubmitRollRequest(actor, true, "AttackRoll", "1d20", TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;

            var rerollRequest = new RequestFullRerollRequest(original.RollId, other, actorIsMainGm: false, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId());
            Result<DiceRoll> result = DiceRollService.RequestFullReroll(store, rngFactory, Clock, rerollRequest);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.DiceRerollDenied));

            DiceRoll unchanged = store.TryGet(original.RollId, out DiceRoll current) ? current : null!;
            Assert.That(unchanged.Status, Is.EqualTo(DiceRollStatus.Resolved), "a rejected reroll must not supersede the original");
        }

        [Test]
        public void CancelRoll_ResolvedRoll_WithoutReason_IsRejected()
        {
            // TC-DICE-016 (section 18.3: mandatory reason for a resolved roll)
            var store = new DiceRollStore();
            UserId actor = NewUserId();
            DiceRoll roll = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, new SubmitRollRequest(actor, true, "AttributeCheck", "1d20", TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;

            Result<DiceRoll> result = DiceRollService.CancelRoll(store, new CancelRollRequest(roll.RollId, actor, actorIsMainGm: false, reason: null, NewCorrelationId()));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.DiceCancelReasonRequired));
        }

        [Test]
        public void CancelRoll_WithReason_Succeeds_OriginalDataPreserved()
        {
            // TC-DICE-017 (roadmap section 12.6 step 10: "...cancel"; original event remains)
            var store = new DiceRollStore();
            UserId actor = NewUserId();
            DiceRoll roll = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, new SubmitRollRequest(actor, true, "AttributeCheck", "1d20", TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;
            int naturalValue = roll.NaturalResults[0].Value;

            Result<DiceRoll> result = DiceRollService.CancelRoll(store, new CancelRollRequest(roll.RollId, actor, actorIsMainGm: false, "player disconnected", NewCorrelationId()));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Status, Is.EqualTo(DiceRollStatus.Cancelled));
            Assert.That(result.Value.NaturalResults[0].Value, Is.EqualTo(naturalValue), "cancellation must not delete or rewrite the roll's own data");
        }

        [Test]
        public void CancelRoll_ByNonActorNonMainGm_IsRejected()
        {
            // TC-DICE-018
            var store = new DiceRollStore();
            UserId actor = NewUserId();
            UserId other = NewUserId();
            DiceRoll roll = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, new SubmitRollRequest(actor, true, "AttributeCheck", "1d20", TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId())).Value;

            Result<DiceRoll> result = DiceRollService.CancelRoll(store, new CancelRollRequest(roll.RollId, other, actorIsMainGm: false, "trying to cancel someone else's roll", NewCorrelationId()));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.DiceCancelDenied));
        }

        private sealed class SystemWallClock : IWallClock
        {
            public UtcInstant GetUtcNow() => UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow);
        }
    }
}
