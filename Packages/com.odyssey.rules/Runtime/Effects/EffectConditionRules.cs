using Odyssey.Domain.Effects;

namespace Odyssey.Rules.Effects
{
    /// <summary>`ADR-028` §8's own `WhileCondition` result vocabulary, plus the fail-closed `Inconclusive` case §11 requires every duration mechanism to support.</summary>
    public enum EffectConditionEvaluationResult
    {
        /// <summary>The evaluation could not conclusively determine the condition (missing referenced state, a `Rules`-layer error, an unavailable dependency, or -- today -- simply no real condition language defined yet). `ADR-028` §11: an inconclusive evaluation always fails closed -- the effect remains `Active`, never `Expired` from this result.</summary>
        Inconclusive = 1,
        ConditionHolds = 2,
        ConditionFailed = 3
    }

    /// <summary>
    /// ODY-S05-504: `ADR-028` §8's own `WhileCondition` row -- "re-evaluated by
    /// `Odyssey.Rules`... this ADR does not fix the exact trigger cadence,
    /// only that the evaluator is `Odyssey.Rules`, never a direct
    /// Character/item mutation, and that an inconclusive evaluation fails
    /// closed." By direct structural analogy to `ODY-S05-503`'s own
    /// `EffectStackRules` (which was the first code of any kind added to
    /// `Odyssey.Rules` for effects), this is genuinely new code, not a reuse
    /// of an existing evaluator -- none existed before this task.
    ///
    /// TODAY'S IMPLEMENTATION IS DELIBERATELY, DOCUMENTEDLY A NO-OP THAT
    /// ALWAYS RETURNS <see cref="EffectConditionEvaluationResult.Inconclusive"/>.
    /// This is not an oversight: no Ruleset-defined condition language or
    /// schema exists anywhere in this codebase for
    /// <see cref="EffectMechanicsSnapshot.Payload"/>'s own opaque content
    /// (confirmed by direct search -- the same finding `ODY-S05-503`'s own
    /// `EffectStackRules.IsStronger` doc comment already recorded for
    /// `ReplaceIfStronger`'s analogous "opaque payload" comparison). `ADR-028`
    /// §14 explicitly places "balancing concrete `EffectDefinition` content or
    /// ... potency payload schema" outside this ADR's own authority, and by
    /// the same reasoning a condition-expression language is a
    /// Ruleset/content-authoring decision this task has no authority to
    /// invent. Always-`Inconclusive` is itself a fully honest, fail-closed-
    /// compliant mechanism per `ADR-028` §11's own text ("re-attempted on the
    /// next relevant trigger, never... 'expired by default'") -- exactly the
    /// same "mechanism exists, real semantics come later" pattern
    /// `ItemDefinitionMigrationRules.ComputeBlockingIssues`'s own doc comment
    /// already established for its own four unimplemented blocking-issue
    /// categories ("Later tasks must not assume an empty report proves all
    /// six cases safe").
    ///
    /// A future task that defines a real Ruleset condition language must
    /// replace this method's own body without changing its signature or
    /// `ActiveEffectExpiryRules.CheckWhileConditionExpiry`'s own call site.
    /// </summary>
    public static class EffectConditionRules
    {
        public static EffectConditionEvaluationResult Evaluate(EffectMechanicsSnapshot snapshot)
        {
            return EffectConditionEvaluationResult.Inconclusive;
        }
    }
}
