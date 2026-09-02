using System;

namespace Odyssey.Rules.Character
{
    /// <summary>
    /// ODY-S04-106: mirrors <see cref="AttributeCostRules"/>'s own exact
    /// reasoning and disclaimer for a different mechanics entry kind.
    ///
    /// TEST FIXTURE ONLY -- NOT production Ruleset balance data. No
    /// skill-cost catalog exists anywhere in this codebase (confirmed by
    /// search before writing this file). 10_Characters_And_Progression
    /// section 14.1 states plainly that skill cost varies per skill and is
    /// Ruleset/`SkillDefinition`-driven, with no universal technical
    /// maximum -- this class hard-codes one flat cost and one ordinary-
    /// purchase ceiling anyway, as the smallest fixture needed to prove the
    /// purchase/reservation mechanism end-to-end (backlog section 2.3). A
    /// future Ruleset-catalog task must replace this file's body with a
    /// real per-<c>SkillDefinitionId</c> content-driven lookup without
    /// changing <c>PurchaseSkillLevel</c>/<c>RequestSkillAdvancedRecommendation</c>'s
    /// own call sites, since this is the only place the values are read.
    /// </summary>
    public static class SkillCostRules
    {
        public const long CostPerSkillPoint = 3;

        /// <summary>Product sections 14.2/14.3: levels below 5 are an ordinary immediate purchase; level 5 and above requires the recommendation/reservation pipeline (ADR-024 section 6.1). This fixture fixes that boundary at level 4.</summary>
        public const long MaxOrdinaryPurchaseLevel = 4;

        public static long CostForIncrease(long fromLevel, long toLevel)
        {
            if (toLevel <= fromLevel) throw new ArgumentOutOfRangeException(nameof(toLevel), "ToLevel must exceed FromLevel for an increase.");
            return (toLevel - fromLevel) * CostPerSkillPoint;
        }

        public static bool RequiresRecommendation(long toLevel) => toLevel > MaxOrdinaryPurchaseLevel;
    }
}
