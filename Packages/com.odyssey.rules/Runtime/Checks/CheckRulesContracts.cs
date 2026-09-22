using System.Collections.Generic;
using Odyssey.Domain.Character;
using Odyssey.Domain.Checks;
using Odyssey.Rules.Combat;

namespace Odyssey.Rules.Checks
{
    /// <summary>
    /// `ADR-031` section 5.2's own algorithm (fixed by `ODY-S07-102`'s own governing ТЗ section 1.3/1.4, not
    /// left to this task's discretion): parsing `AttackDamageFormula.Terms` into (a) exactly one `DiceGroup`
    /// term reconstructed into `DiceRollService.SubmitRoll`'s own dice-only formula string, (b) zero or one
    /// `AttributeReference` term resolved against `CheckParticipantState`, and (c) every `Constant` term
    /// summed as-is -- can fail four distinct, fail-closed ways. `None` is never itself a failure value.
    /// </summary>
    public enum CheckFormulaValidationError
    {
        None = 0,

        /// <summary>`ADR-031` section 5.2's own last paragraph: a check formula with zero or more than one `DiceGroup` term is rejected whole, never a silent "pick the first"/"sum them all" choice.</summary>
        RequiresExactlyOneDiceGroup,

        /// <summary>Closes the ambiguity `ADR-031` itself did not resolve -- which of several `AttributeReference` terms would be "the" skill `RecordCriticalSuccessEvidence` records against. Fixed here: at most one is allowed.</summary>
        RequiresAtMostOneAttributeReference,

        /// <summary>`ADR-031` section 6's own fail-closed requirement: a name that resolves against BOTH `AttributeValues` and `SkillValues` simultaneously is never silently resolved to one of the two.</summary>
        AmbiguousReference,

        /// <summary>The direct continuation of `ADR-030` section 6.2's own fail-closed convention for an unresolved `attributeReference` term -- never a silent zero.</summary>
        UnresolvedReference,
    }

    /// <summary>
    /// `ADR-031` section 5.2's own required output: the dice-only sub-formula for `SubmitRollRequest.Formula`,
    /// every `Constant` term's own signed sum, and (at most one) resolved `AttributeReference` term's own
    /// signed value plus its name and, when it resolved against a skill rather than an attribute, the
    /// `SkillDefinitionId` `CheckService` needs for a real `RecordCriticalSuccessEvidence` call.
    /// </summary>
    public readonly struct CheckFormulaResolution
    {
        internal CheckFormulaResolution(string diceRollFormula, int constantSum, int? referenceValue, string? referenceName, SkillDefinitionId? resolvedSkillId)
        {
            DiceRollFormula = diceRollFormula;
            ConstantSum = constantSum;
            ReferenceValue = referenceValue;
            ReferenceName = referenceName;
            ResolvedSkillId = resolvedSkillId;
        }

        /// <summary>`ADR-031` section 5.2 step 5: `"{sign}{Count}d{Sides}"`, e.g. `"1d20"`/`"-1d20"` -- the exact string submitted as `SubmitRollRequest.Formula`.</summary>
        public string DiceRollFormula { get; }
        public int ConstantSum { get; }
        public int? ReferenceValue { get; }
        public string? ReferenceName { get; }

        /// <summary>Non-null only when the single `AttributeReference` term (if any) resolved against `CheckParticipantState.SkillValues` rather than `AttributeValues` -- `ADR-031` section 10's own gate for whether `RecordCriticalSuccessEvidence` may be called at all.</summary>
        public SkillDefinitionId? ResolvedSkillId { get; }
    }

    /// <summary>
    /// `ADR-031` section 8.1: structurally parallel to `IAttackRulesEvaluator`/`CoreAttackRulesEvaluator`
    /// (two pure methods, no I/O/RNG/database access anywhere -- `ADR-001` section 6.2) but NOT an extension
    /// of either -- a check has no `AttackIntent`/`AttackEvaluationSnapshot`/`AttackRandomSample`-shaped
    /// inputs, and forcing it through that interface would require inventing meaningless placeholder values
    /// for every attack-specific field (`ADR-031` section 4's own verified reasoning).
    /// </summary>
    public interface ICheckRulesEvaluator
    {
        /// <summary>`ADR-031` section 5.2's own algorithm, run BEFORE `DiceRollService.SubmitRoll` is ever called -- a `false` return means the whole check is rejected with zero RNG consumed and zero durable writes (`ODY-S07-102`'s own governing ТЗ section 2's own explicit fail-closed invariant).</summary>
        bool TryResolveFormula(AttackDamageFormula formula, CheckParticipantState participant, out CheckFormulaResolution resolution, out CheckFormulaValidationError error);

        /// <summary>`ADR-031` section 8.1 points 2-3: compares the real, already-rolled `finalTotal` (from a real `DiceRollService.SubmitRoll` call the caller already made) against `difficultyClass`, and determines `CheckOutcome.IsNaturalMaximum` from the real natural die values/sides the caller already has (`dieValues`/`dieSides`, all belonging to the check's own single dice-group term, `ADR-031` section 5.1's own confirmed groupIndex-0 shape) -- never derives or consumes any RNG stream itself.</summary>
        CheckOutcome Evaluate(CheckFormulaResolution resolution, IReadOnlyList<int> dieValues, int dieSides, int finalTotal, long difficultyClass);
    }
}
