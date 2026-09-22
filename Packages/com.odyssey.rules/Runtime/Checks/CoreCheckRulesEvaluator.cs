using System.Collections.Generic;
using System.Globalization;
using Odyssey.Domain.Character;
using Odyssey.Domain.Checks;
using Odyssey.Rules.Combat;

namespace Odyssey.Rules.Checks
{
    /// <summary>
    /// ODY-S07-102: the first, and so far only, `ICheckRulesEvaluator` implementation -- `ADR-031` section
    /// 8.1's own architecture, built exactly to this task's own governing ТЗ sections 1.3/1.4/1.5's fixed
    /// algorithm (not left to independent judgment). Pure, no I/O/RNG/database access anywhere (`ADR-001`
    /// section 6.2) -- every input arrives as an explicit parameter, exactly as `CoreAttackRulesEvaluator`'s
    /// own `Evaluate(intent, snapshot, randomSample)` already models for the attack pipeline.
    /// </summary>
    public sealed class CoreCheckRulesEvaluator : ICheckRulesEvaluator
    {
        public bool TryResolveFormula(AttackDamageFormula formula, CheckParticipantState participant, out CheckFormulaResolution resolution, out CheckFormulaValidationError error)
        {
            resolution = default;
            error = CheckFormulaValidationError.None;

            int diceGroupCount = 0;
            int attributeReferenceCount = 0;
            AttackFormulaTerm diceTerm = default;
            AttackFormulaTerm referenceTerm = default;
            int constantSum = 0;

            foreach (AttackFormulaTerm term in formula.Terms)
            {
                switch (term.Kind)
                {
                    case AttackFormulaTermKind.DiceGroup:
                        diceGroupCount++;
                        diceTerm = term;
                        break;
                    case AttackFormulaTermKind.Constant:
                        constantSum += term.Sign * term.ConstantValue!.Value;
                        break;
                    case AttackFormulaTermKind.AttributeReference:
                        attributeReferenceCount++;
                        referenceTerm = term;
                        break;
                }
            }

            // ADR-031 section 5.2's own last paragraph: exactly one DiceGroup term is required for v1 -- a
            // multi-dice-group check formula (e.g. "1d20+1d4+Strength") is an explicit, disclosed out-of-
            // scope edge case, rejected whole, never silently summed or truncated to the first group.
            if (diceGroupCount != 1)
            {
                error = CheckFormulaValidationError.RequiresExactlyOneDiceGroup;
                return false;
            }

            // ADR-031 section 6's own closed ambiguity: more than one AttributeReference term would leave
            // no single, unambiguous "the" skill for RecordCriticalSuccessEvidence to record against.
            if (attributeReferenceCount > 1)
            {
                error = CheckFormulaValidationError.RequiresAtMostOneAttributeReference;
                return false;
            }

            string diceRollFormula = (diceTerm.Sign < 0 ? "-" : string.Empty)
                + diceTerm.Count!.Value.ToString(CultureInfo.InvariantCulture)
                + "d"
                + diceTerm.Sides!.Value.ToString(CultureInfo.InvariantCulture);

            int? referenceValue = null;
            string? referenceName = null;
            SkillDefinitionId? resolvedSkillId = null;

            if (attributeReferenceCount == 1)
            {
                string name = referenceTerm.AttributeName!;
                bool isSkill = SkillDefinitionId.TryParse(name, out SkillDefinitionId skillId) && participant.SkillValues.ContainsKey(skillId);
                bool isAttribute = AttributeDefinitionId.TryParse(name, out AttributeDefinitionId attributeId) && participant.AttributeValues.ContainsKey(attributeId);

                // ADR-031 section 6's own fail-closed requirement: a name resolving against BOTH dictionaries
                // simultaneously is never silently resolved to one -- the whole check is rejected instead.
                if (isSkill && isAttribute)
                {
                    error = CheckFormulaValidationError.AmbiguousReference;
                    return false;
                }

                // The direct continuation of ADR-030 section 6.2's own fail-closed convention: an unresolved
                // reference is a typed rejection, never a silent zero.
                if (!isSkill && !isAttribute)
                {
                    error = CheckFormulaValidationError.UnresolvedReference;
                    return false;
                }

                if (isSkill)
                {
                    referenceValue = referenceTerm.Sign * (int)participant.SkillValues[skillId];
                    referenceName = name;
                    resolvedSkillId = skillId;
                }
                else
                {
                    referenceValue = referenceTerm.Sign * (int)participant.AttributeValues[attributeId];
                    referenceName = name;
                }
            }

            resolution = new CheckFormulaResolution(diceRollFormula, constantSum, referenceValue, referenceName, resolvedSkillId);
            return true;
        }

        public CheckOutcome Evaluate(CheckFormulaResolution resolution, IReadOnlyList<int> dieValues, int dieSides, int finalTotal, long difficultyClass)
        {
            int resolvedModifier = resolution.ConstantSum + (resolution.ReferenceValue ?? 0);

            // ADR-031 section 5.1's own confirmed shape: a check's own SubmitRollRequest.Formula is always
            // exactly one dice-group term, so every entry in dieValues shares the same dieSides -- "any die
            // in the group reaching its own maximum face value counts as a natural maximum for the whole
            // check" (section 1.5 of this task's own governing ТЗ), not only the first die.
            bool isNaturalMaximum = false;
            for (int index = 0; index < dieValues.Count; index++)
            {
                if (dieValues[index] == dieSides)
                {
                    isNaturalMaximum = true;
                    break;
                }
            }

            CheckResultKind result = finalTotal >= difficultyClass ? CheckResultKind.Pass : CheckResultKind.Fail;
            return new CheckOutcome(result, isNaturalMaximum, resolvedModifier, finalTotal, difficultyClass);
        }
    }
}
