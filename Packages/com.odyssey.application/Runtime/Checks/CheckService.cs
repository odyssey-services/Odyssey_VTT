using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Odyssey.Application.Commands;
using Odyssey.Application.Dice;
using Odyssey.Application.Persistence;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Checks;
using Odyssey.Domain.Identity;
using Odyssey.Rules.Checks;
using Odyssey.Rules.Combat;
using Odyssey.Rules.Versions;

namespace Odyssey.Application.Checks
{
    /// <summary>
    /// ODY-S07-102: `ADR-031`'s own standalone check/contest command, NOT bound to `CombatEncounter`
    /// (`ADR-031` section 9 -- no turn/round/current-participant check anywhere in this class, extending
    /// `ADR-030` section 9.3's own identical argument for `ActivateAbility`). Mirrors
    /// `ActivateAbilityService.ActivateAbility`'s own combined shape (idempotency-first, then read, then
    /// roll, then evaluate, then apply) -- but routes its own roll through the real, unmodified
    /// `DiceRollService.SubmitRoll` (`ADR-031` section 5), never a direct `IAuthoritativeRandomStreamFactory`
    /// draw, since this is the one Rules-Engine-adjacent command this codebase decided should reuse
    /// `DiceRollService`'s own already-built GM-adjudication machinery rather than bypass it.
    /// </summary>
    public static class CheckService
    {
        /// <summary>`SubmitRollRequest.Purpose`'s own free-text, descriptive label -- unrelated to `RngPurpose`, which `DiceRollService` fixes internally as `"dice.roll"` regardless of this value (`ADR-031` section 11, confirmed by direct code read).</summary>
        private const string RollPurposeLabel = "check.roll";

        public static Result<CheckOutcomeRecord> PerformCheck(
            ICheckStateReader reader,
            ICheckRepository apply,
            ICharacterRepository characters,
            DiceRollStore diceRollStore,
            IAuthoritativeRandomStreamFactory random,
            IWallClock clock,
            CampaignHandle campaign,
            RulesetVersion rulesetVersion,
            RngKeyEpochId keyEpochId,
            CheckRequest request)
        {
            if (reader == null || apply == null || characters == null || diceRollStore == null || random == null || clock == null) throw new ArgumentNullException(nameof(reader));
            if (campaign == null || request == null) throw new ArgumentNullException(nameof(campaign));

            // ADR-008 rule 14 / ADR-031 section 5.1's own disclosed requirement: DiceRollService.SubmitRoll
            // itself performs no CommandId-keyed replay (confirmed by direct code read -- there is no
            // AppliedCommands-style check anywhere inside it), so THIS check is the only thing standing
            // between a retried CommandId and a second real dice roll. It must run before anything else.
            Result<CheckOutcomeRecord> existing = apply.GetCheckOutcome(campaign, request.CommandId, request.CorrelationId);
            if (existing.IsSuccess)
            {
                return existing;
            }

            Result<CheckParticipantState> state = reader.Read(campaign, request.Intent.ActorId, request.CorrelationId);
            if (state.IsFailure)
            {
                return Result<CheckOutcomeRecord>.Failure(state.Error);
            }

            if (!AttackDamageFormulaParser.TryParse(request.Intent.Formula, out AttackDamageFormula formula, out AttackDamageFormulaParseError _))
            {
                return Result<CheckOutcomeRecord>.Failure(FormulaInvalid(request.CorrelationId));
            }

            var evaluator = new CoreCheckRulesEvaluator();
            if (!evaluator.TryResolveFormula(formula, state.Value, out CheckFormulaResolution resolution, out CheckFormulaValidationError validationError))
            {
                return Result<CheckOutcomeRecord>.Failure(ValidationFailure(validationError, request.CorrelationId));
            }

            var automaticModifiers = new List<AutomaticModifierRequest>();
            if (resolution.ConstantSum != 0)
            {
                automaticModifiers.Add(new AutomaticModifierRequest("FormulaConstant", "constant", resolution.ConstantSum));
            }

            if (resolution.ReferenceValue.HasValue)
            {
                string sourceKind = resolution.ResolvedSkillId.HasValue ? "SkillModifier" : "AttributeModifier";
                automaticModifiers.Add(new AutomaticModifierRequest(sourceKind, resolution.ReferenceName!, resolution.ReferenceValue.Value));
            }

            var submitRequest = new SubmitRollRequest(
                request.ActorUserId, actorCanCreateRoll: true, RollPurposeLabel, resolution.DiceRollFormula,
                request.Intent.Audience, campaign.CampaignId, request.CommandId, rulesetVersion, keyEpochId,
                request.CorrelationId, automaticModifiers);

            Result<DiceRoll> rollResult = DiceRollService.SubmitRoll(diceRollStore, random, clock, submitRequest);
            if (rollResult.IsFailure)
            {
                return Result<CheckOutcomeRecord>.Failure(rollResult.Error);
            }

            DiceRoll roll = rollResult.Value;
            int[] dieValues = new int[roll.NaturalResults.Count];
            int dieSides = roll.NaturalResults.Count > 0 ? roll.NaturalResults[0].Sides : 0;
            for (int index = 0; index < roll.NaturalResults.Count; index++)
            {
                dieValues[index] = roll.NaturalResults[index].Value;
            }

            CheckOutcome outcome = evaluator.Evaluate(resolution, dieValues, dieSides, roll.FinalTotal, request.Intent.DifficultyClass);

            // ADR-031 section 10: real critical success on a real skill check calls RecordCriticalSuccessEvidence
            // with the real DiceRoll.RollId -- never a synthetic value, and never for an attribute-only check
            // (section 10's own explicit scope limit -- ResolvedSkillId is null for those).
            if (outcome.IsNaturalMaximum && resolution.ResolvedSkillId.HasValue)
            {
                Result<CriticalSuccessEvidenceRecord> evidence = characters.RecordCriticalSuccessEvidence(
                    campaign, request.Intent.ActorId, resolution.ResolvedSkillId.Value,
                    sourceDiceRollId: roll.RollId, sourceActionId: request.CommandId.ToString(),
                    StableSubCommandId(request.CommandId), request.CorrelationId);
                if (evidence.IsFailure)
                {
                    return Result<CheckOutcomeRecord>.Failure(evidence.Error);
                }
            }

            return apply.RecordCheckOutcome(
                campaign, request.Intent.ActorId, request.Intent.Formula, request.Intent.DifficultyClass,
                roll.RollId, outcome.Result, outcome.IsNaturalMaximum, resolution.ResolvedSkillId,
                request.CommandId, request.CorrelationId);
        }

        /// <summary>Mirrors `ActivateAbilityService.StableSubCommandId`'s own established pattern for deriving a deterministic sub-`CommandId` from a root `CommandId` plus a stable purpose string -- lets a retry of a check whose `RecordCriticalSuccessEvidence` call already succeeded replay idempotently through `ICharacterRepository`'s own existing ledger, even in the narrow window before this command's own `RecordCheckOutcome` has durably landed.</summary>
        private static CommandId StableSubCommandId(CommandId rootCommandId)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes("ody-s07-102/v1/critical-success/" + rootCommandId));
            var text = new StringBuilder(32);
            for (int i = 0; i < 16; i++) text.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return CommandId.Parse("cmd_" + text);
        }

        private static Error FormulaInvalid(CorrelationId id) => Error.Create(
            ErrorCodes.CheckFormulaInvalid, ErrorCategory.Validation, SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.check.formula_invalid"), RetryDirective.DoNotRetry, id);

        private static Error ValidationFailure(CheckFormulaValidationError error, CorrelationId id) => error switch
        {
            CheckFormulaValidationError.RequiresExactlyOneDiceGroup => Error.Create(ErrorCodes.CheckFormulaRequiresExactlyOneDiceGroup, ErrorCategory.Validation, SafeReasonCode.InvalidRequest, UserMessageKey.Parse("errors.check.requires_exactly_one_dice_group"), RetryDirective.DoNotRetry, id),
            CheckFormulaValidationError.RequiresAtMostOneAttributeReference => Error.Create(ErrorCodes.CheckFormulaRequiresAtMostOneAttributeReference, ErrorCategory.Validation, SafeReasonCode.InvalidRequest, UserMessageKey.Parse("errors.check.requires_at_most_one_attribute_reference"), RetryDirective.DoNotRetry, id),
            CheckFormulaValidationError.AmbiguousReference => Error.Create(ErrorCodes.CheckFormulaAmbiguousReference, ErrorCategory.Validation, SafeReasonCode.InvalidRequest, UserMessageKey.Parse("errors.check.ambiguous_reference"), RetryDirective.DoNotRetry, id),
            CheckFormulaValidationError.UnresolvedReference => Error.Create(ErrorCodes.CheckFormulaUnresolvedReference, ErrorCategory.Validation, SafeReasonCode.InvalidRequest, UserMessageKey.Parse("errors.check.unresolved_reference"), RetryDirective.DoNotRetry, id),
            _ => Error.Create(ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Validation, SafeReasonCode.InvalidRequest, UserMessageKey.Parse("errors.application.validation_invalid"), RetryDirective.DoNotRetry, id),
        };
    }
}
