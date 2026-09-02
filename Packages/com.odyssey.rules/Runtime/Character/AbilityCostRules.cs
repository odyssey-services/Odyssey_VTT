namespace Odyssey.Rules.Character
{
    /// <summary>
    /// ODY-S04-108: ADR-024 section 9 assigns cost/cap calculation to
    /// <c>Odyssey.Rules</c>, exactly mirroring <see cref="AttributeCostRules"/>/
    /// <see cref="SkillCostRules"/>'s own reasoning.
    ///
    /// TEST FIXTURE ONLY -- NOT production Ruleset balance data. No
    /// ability-cost catalog/table mechanism exists anywhere in this codebase
    /// (confirmed by search before writing this file). An ability acquired
    /// via <c>SourceKind=ProgressionPurchase</c> either is or is not owned --
    /// there is no intermediate level in this task's own scope (product
    /// section 16 names no per-ability numeric cost or rank-based cost
    /// curve) -- so this fixture is a single flat cost, not a per-point
    /// formula like <see cref="AttributeCostRules"/>/<see cref="SkillCostRules"/>.
    /// A future Ruleset-catalog task must replace this file's body with a
    /// real per-<c>AbilityDefinitionId</c> content-driven lookup without
    /// changing <c>AcquireAbility</c>'s own call site, since this is the
    /// only place the value is read.
    /// </summary>
    public static class AbilityCostRules
    {
        public const long CostPerAbility = 5;

        public static long CostForAcquisition() => CostPerAbility;
    }
}
