using System;
using System.Collections.Generic;
using Odyssey.Domain.Character;
using Odyssey.Domain.Identity;

namespace Odyssey.Domain.Checks
{
    /// <summary>
    /// ODY-S07-102: `ADR-031` section 7's own check-time snapshot -- the direct structural precedent is
    /// `AttackParticipantState` (`ODY-S06-102`, `Packages/com.odyssey.domain/Runtime/Combat/AttackPipelineContracts.cs`),
    /// copied here rather than extended: a check is not a combat participant, so `LifecycleStatus`/
    /// `ApprovalState` (meaningful only in `AttackParticipantState`'s own combat-adjacent context) are
    /// deliberately absent (`ADR-031` section 16.3). Carries BOTH attribute and skill values, the one
    /// genuine extension over `AttackParticipantState`'s own attribute-only shape (`ADR-031` section 7's
    /// own explicit requirement), each copied from an already-loaded `CharacterRecord` with zero extra
    /// database reads, mirroring `SqliteAttackStateReader.Read`'s own established pattern exactly.
    /// </summary>
    public readonly struct CheckParticipantState
    {
        public CheckParticipantState(CharacterId characterId, IReadOnlyDictionary<AttributeDefinitionId, long> attributeValues, IReadOnlyDictionary<SkillDefinitionId, long> skillValues)
        {
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (attributeValues == null) throw new ArgumentNullException(nameof(attributeValues));
            if (skillValues == null) throw new ArgumentNullException(nameof(skillValues));

            CharacterId = characterId;
            AttributeValues = new Dictionary<AttributeDefinitionId, long>(attributeValues);
            SkillValues = new Dictionary<SkillDefinitionId, long>(skillValues);
        }

        public CharacterId CharacterId { get; }
        public IReadOnlyDictionary<AttributeDefinitionId, long> AttributeValues { get; }
        public IReadOnlyDictionary<SkillDefinitionId, long> SkillValues { get; }
    }

    /// <summary>`ADR-031` section 8.1: the minimum outcome shape this ADR requires -- plain pass/fail, degrees of success confirmed out of scope for v1 (`ADR-031` section 17).</summary>
    public enum CheckResultKind
    {
        Pass = 1,
        Fail = 2,
    }

    /// <summary>
    /// `ADR-031` section 8.1's own evaluator result -- produced by `Odyssey.Rules.Checks.ICheckRulesEvaluator.Evaluate`,
    /// living in `Odyssey.Domain` (not `Odyssey.Rules`) so it can flow into `Odyssey.Application`/
    /// `Odyssey.Persistence` without either layer needing a `Odyssey.Rules` reference for this shape alone --
    /// the same "Rules produces a Domain-shaped result" precedent `ProposedAttackResolution`/`AttackRandomSample`
    /// already establish for the attack pipeline.
    /// </summary>
    public readonly struct CheckOutcome
    {
        public CheckOutcome(CheckResultKind result, bool isNaturalMaximum, int resolvedModifier, int finalTotal, long difficultyClass)
        {
            Result = result;
            IsNaturalMaximum = isNaturalMaximum;
            ResolvedModifier = resolvedModifier;
            FinalTotal = finalTotal;
            DifficultyClass = difficultyClass;
        }

        public CheckResultKind Result { get; }

        /// <summary>`ADR-031` section 8.1 point 3 / section 10: true when any die in the check's own single dice-group term rolled its own maximum face value (`NaturalResult.Value == NaturalResult.Sides`) -- the one signal this ADR's own first version produces beyond plain pass/fail (section 8.2's own confirmed non-goal for graduated degrees of success).</summary>
        public bool IsNaturalMaximum { get; }
        public int ResolvedModifier { get; }
        public int FinalTotal { get; }
        public long DifficultyClass { get; }
    }
}
