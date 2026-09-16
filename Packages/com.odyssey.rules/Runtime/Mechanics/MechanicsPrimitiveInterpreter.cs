using System;
using System.Collections.Generic;
using Odyssey.Domain.Character;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Content;
using Odyssey.Rules.Combat;

namespace Odyssey.Rules.Mechanics
{
    /// <summary>`ADR-030` §7's own real, resolved output of an `AdjustResourcePrimitive` -- a plain resource kind plus a concrete signed amount, ready for a caller to turn into a real `AttackDelta`-shaped commit (`ODY-S05-609`'s own convention, reused by reference, not duplicated).</summary>
    public readonly struct ResolvedResourceAdjustment
    {
        public ResolvedResourceAdjustment(ResourceDefinitionId resourceKind, long amount)
        {
            if (!resourceKind.IsValid) throw new ArgumentException("ResourceKind is required.", nameof(resourceKind));
            ResourceKind = resourceKind;
            Amount = amount;
        }

        public ResourceDefinitionId ResourceKind { get; }
        public long Amount { get; }
    }

    /// <summary>The interpreter's own pure output: every `AdjustResource` primitive resolved to a real number, and every `ApplyEffect` primitive's own reference passed through unresolved (resolving/validating it is a content-catalog concern the interpreter itself has no access to -- `ADR-001` §6.2's own "Rules receives external state only through explicit input parameters").</summary>
    public sealed class MechanicsPrimitiveInterpretationResult
    {
        public MechanicsPrimitiveInterpretationResult(IReadOnlyList<ResolvedResourceAdjustment> resourceAdjustments, IReadOnlyList<ContentDefinitionRef> effectsToApply)
        {
            ResourceAdjustments = resourceAdjustments ?? throw new ArgumentNullException(nameof(resourceAdjustments));
            EffectsToApply = effectsToApply ?? throw new ArgumentNullException(nameof(effectsToApply));
        }

        public IReadOnlyList<ResolvedResourceAdjustment> ResourceAdjustments { get; }
        public IReadOnlyList<ContentDefinitionRef> EffectsToApply { get; }
    }

    /// <summary>
    /// ODY-S06-106: `ADR-030` §7's own first real implementation of the shared mechanics-primitive
    /// interpreter -- `ODY-S06-105`'s own `CoreAttackRulesEvaluator` never decoded or interpreted a
    /// mechanics-primitive payload at all (confirmed by direct code read before writing this file); it
    /// only ever evaluated `WeaponDefinition.DamageExpression`, a different (if grammatically identical)
    /// concern. A pure, deterministic function: input is already-decoded primitives plus already-loaded
    /// attributes plus an already-drawn `AttackRandomSample` (or none, for a dice-free preview); output is
    /// plain resolved numbers -- no repository call, no RNG stream of its own (`ADR-001` §6.2).
    ///
    /// Reuses `AttackDamageFormulaParser`/`AttackRandomSample` by direct reference (`ODY-S06-105`'s own
    /// grammar and RNG-carrier types, unmodified) -- the small "walk parsed terms and sum" evaluation loop
    /// itself is NOT reusable from `CoreAttackRulesEvaluator.cs` (that file's own equivalent helper is
    /// `private`, and this task is forbidden from modifying that file's content even to widen a method's
    /// visibility), so this loop is a narrow, deliberate, documented duplication of roughly a dozen lines
    /// -- not a second parser and not a fork of `AttackDamageFormulaParser` itself.
    /// </summary>
    public static class MechanicsPrimitiveInterpreter
    {
        public static MechanicsPrimitiveInterpretationResult Interpret(MechanicsPrimitiveEnvelope envelope, IReadOnlyDictionary<AttributeDefinitionId, long> actorAttributes, AttackRandomSample? randomSample)
        {
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));
            if (actorAttributes == null) throw new ArgumentNullException(nameof(actorAttributes));

            var resourceAdjustments = new List<ResolvedResourceAdjustment>();
            var effectsToApply = new List<ContentDefinitionRef>();
            int cursor = 0;

            foreach (MechanicsPrimitive primitive in envelope.Primitives)
            {
                switch (primitive)
                {
                    case AdjustResourcePrimitive adjust:
                        AttackDamageFormula formula = AttackDamageFormulaParser.Parse(adjust.AmountFormula);
                        Func<int, int> nextDie = randomSample.HasValue
                            ? sides => MapRawRollToDie(randomSample.Value.Values[cursor++ % randomSample.Value.Values.Count], sides)
                            : PreviewDieValue;
                        long amount = EvaluateFormula(formula, actorAttributes, nextDie);
                        resourceAdjustments.Add(new ResolvedResourceAdjustment(adjust.ResourceKind, amount));
                        break;

                    case ApplyEffectPrimitive applyEffect:
                        effectsToApply.Add(applyEffect.EffectDefinitionRef);
                        break;

                    default:
                        throw new InvalidOperationException("Unrecognized MechanicsPrimitive type: " + primitive.GetType());
                }
            }

            return new MechanicsPrimitiveInterpretationResult(Array.AsReadOnly(resourceAdjustments.ToArray()), Array.AsReadOnly(effectsToApply.ToArray()));
        }

        private static long EvaluateFormula(AttackDamageFormula formula, IReadOnlyDictionary<AttributeDefinitionId, long> actorAttributes, Func<int, int> nextDie)
        {
            long total = 0;
            foreach (AttackFormulaTerm term in formula.Terms)
            {
                long termValue;
                switch (term.Kind)
                {
                    case AttackFormulaTermKind.Constant:
                        termValue = term.ConstantValue!.Value;
                        break;

                    case AttackFormulaTermKind.DiceGroup:
                        long sum = 0;
                        for (int index = 0; index < term.Count!.Value; index++) sum += nextDie(term.Sides!.Value);
                        termValue = sum;
                        break;

                    case AttackFormulaTermKind.AttributeReference:
                        if (!AttributeDefinitionId.TryParse(term.AttributeName, out AttributeDefinitionId attributeId) || !actorAttributes.TryGetValue(attributeId, out long value))
                        {
                            throw new InvalidOperationException("Mechanics primitive formula references an unresolved attribute: " + term.AttributeName);
                        }

                        termValue = value;
                        break;

                    default:
                        throw new InvalidOperationException("Unrecognized attack formula term kind: " + term.Kind);
                }

                total += term.Sign * termValue;
            }

            return total;
        }

        private static int MapRawRollToDie(int raw, int sides) => ((raw - 1) % sides) + 1;
        private static int PreviewDieValue(int sides) => (sides + 1) / 2;
    }
}
