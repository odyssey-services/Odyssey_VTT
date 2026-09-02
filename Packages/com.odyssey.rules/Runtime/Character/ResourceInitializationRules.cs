using Odyssey.Domain.Character;

namespace Odyssey.Rules.Character
{
    /// <summary>
    /// ODY-S04-109: ADR-024 section 9 assigns cost/cap/initialization
    /// calculation to <c>Odyssey.Rules</c>, exactly mirroring
    /// <see cref="AttributeCostRules"/>/<see cref="SkillCostRules"/>/
    /// <see cref="AbilityCostRules"/>'s own reasoning.
    ///
    /// TEST FIXTURE ONLY -- NOT production Ruleset content. No
    /// <c>ResourceDefinition</c> catalog exists anywhere in this codebase
    /// (confirmed by search before writing this file). Product section 17
    /// itself names no concrete resource or numeric starting values --
    /// this fixture is the smallest content needed to prove
    /// <c>InitializeCharacterResource</c>'s own mechanism end-to-end
    /// (backlog section 2.3's "smallest test-fixture content" convention).
    /// A future Ruleset-catalog task must replace this file's body with a
    /// real per-<c>ResourceDefinitionId</c> content-driven lookup without
    /// changing <c>InitializeCharacterResource</c>'s own call site, since
    /// this is the only place the values are read.
    /// </summary>
    public static class ResourceInitializationRules
    {
        public const long DefaultBaseMaximum = 10;
        public const long DefaultMinimumValue = 0;
        public const RecoveryRule DefaultRecoveryRule = RecoveryRule.Manual;
    }
}
