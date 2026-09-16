using System;
using System.Collections.Generic;
using Odyssey.Domain.Character;

namespace Odyssey.Domain.Content
{
    /// <summary>
    /// ODY-S06-106: `ADR-030` §7's own minimal, closed mechanics-primitive schema for
    /// `AbilityDefinition.MechanicsPayloadRef`/`EffectDefinition.MechanicsPayloadRef` -- exactly two
    /// primitives, deliberately not a general-purpose executable program or the future
    /// `11_Content_Block_System` `ContentBlockGraph`. The first real implementation of this schema
    /// (`ODY-S06-105`'s own `CoreAttackRulesEvaluator`/`AttackDamageFormulaParser` implemented the
    /// weapon-damage formula grammar only -- it never decoded or interpreted a mechanics-primitive
    /// payload at all, confirmed by direct code read before writing this file).
    /// </summary>
    public abstract class MechanicsPrimitive
    {
    }

    /// <summary>`ADR-030` §7.2: adjusts a Character resource by a signed formula-valued amount (the same grammar `ODY-S06-105`'s own `AttackDamageFormulaParser` already defines, reused by reference, not duplicated).</summary>
    public sealed class AdjustResourcePrimitive : MechanicsPrimitive
    {
        public AdjustResourcePrimitive(ResourceDefinitionId resourceKind, string amountFormula)
        {
            if (!resourceKind.IsValid) throw new ArgumentException("ResourceKind is required.", nameof(resourceKind));
            if (string.IsNullOrWhiteSpace(amountFormula)) throw new ArgumentException("AmountFormula is required.", nameof(amountFormula));
            ResourceKind = resourceKind;
            AmountFormula = amountFormula;
        }

        public ResourceDefinitionId ResourceKind { get; }
        public string AmountFormula { get; }
    }

    /// <summary>`ADR-030` §7.2: applies an already-published `ActiveEffect`, referenced by an exact-version `ContentDefinitionRef` -- this primitive does not itself resolve/validate the reference; that is the interpreting caller's own job (mirroring `ItemEffectLifecycleService`'s own established "resolve id through the catalog, do not trust a cache" pattern).</summary>
    public sealed class ApplyEffectPrimitive : MechanicsPrimitive
    {
        public ApplyEffectPrimitive(ContentDefinitionRef effectDefinitionRef)
        {
            if (!effectDefinitionRef.IsValid) throw new ArgumentException("EffectDefinitionRef is required.", nameof(effectDefinitionRef));
            EffectDefinitionRef = effectDefinitionRef;
        }

        public ContentDefinitionRef EffectDefinitionRef { get; }
    }

    /// <summary>`ADR-030` §7.1's own versioned JSON envelope shape, decoded (`MechanicsPayloadCodec`, `Odyssey.Application.Content`) into this Domain-layer value object -- deliberately not folded into `TypedDefinitionCodec.DecodeAbility`/`DecodeEffect`'s own catalog-shape decode paths (see `MechanicsPayloadCodec`'s own doc comment for why).</summary>
    public sealed class MechanicsPrimitiveEnvelope
    {
        public MechanicsPrimitiveEnvelope(int schemaVersion, IReadOnlyList<MechanicsPrimitive> primitives)
        {
            if (schemaVersion < 1) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            if (primitives == null) throw new ArgumentNullException(nameof(primitives));
            SchemaVersion = schemaVersion;
            MechanicsPrimitive[] copy = new MechanicsPrimitive[primitives.Count];
            for (int index = 0; index < copy.Length; index++) copy[index] = primitives[index] ?? throw new ArgumentException("Primitives must not contain null entries.", nameof(primitives));
            Primitives = Array.AsReadOnly(copy);
        }

        public int SchemaVersion { get; }
        public IReadOnlyList<MechanicsPrimitive> Primitives { get; }
    }
}
