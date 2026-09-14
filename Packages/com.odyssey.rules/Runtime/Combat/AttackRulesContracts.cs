using System;
using Odyssey.Domain.Combat;

namespace Odyssey.Rules.Combat
{
    /// <summary>Pure ADR-029 stages 3–11; no repository, RNG factory, persistence or audience dependency.</summary>
    public interface IAttackRulesEvaluator
    {
        ProposedAttackResolution Preview(AttackIntent intent, AttackEvaluationSnapshot snapshot);
        ProposedAttackResolution Evaluate(AttackIntent intent, AttackEvaluationSnapshot snapshot, AttackRandomSample randomSample);
    }
}
