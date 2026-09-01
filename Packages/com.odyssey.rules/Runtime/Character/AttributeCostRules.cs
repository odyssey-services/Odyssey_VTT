using System;

namespace Odyssey.Rules.Character
{
    /// <summary>
    /// ODY-S04-105: ADR-024 section 9 assigns cost/cap calculation to
    /// <c>Odyssey.Rules</c> ("cost/cap/requirement calculations used by
    /// purchase validation. It does not commit state or write history.").
    ///
    /// TEST FIXTURE ONLY -- NOT production Ruleset balance data. No
    /// Ruleset-catalog/cost-table mechanism exists anywhere in this codebase
    /// (confirmed by search across <c>Odyssey.Rules</c>/<c>Odyssey.Content</c>
    /// before writing this file). 10_Characters_And_Progression section 11.2
    /// names "1 attribute point = 2 development points" as the *current*
    /// Ruleset's own cost while explicitly warning the interface must not
    /// hard-code it as a constant; section 11.3 names <c>NormalDevelopmentCap
    /// = 15</c> the same way. This class hard-codes both anyway, as the
    /// smallest fixture needed to prove the purchase mechanism end-to-end
    /// (backlog section 2.3's "smallest test-fixture content" convention) --
    /// it is not this task's own numeric balance decision, and a future
    /// Ruleset-catalog task must replace this file's body with a real
    /// content-driven lookup without changing <c>PurchaseAttributeIncrease</c>'s
    /// own call site, since this is the only place the values are read.
    /// </summary>
    public static class AttributeCostRules
    {
        public const long CostPerAttributePoint = 2;
        public const long NormalDevelopmentCap = 15;

        public static long CostForIncrease(long fromValue, long toValue)
        {
            if (toValue <= fromValue) throw new ArgumentOutOfRangeException(nameof(toValue), "ToValue must exceed FromValue for an increase.");
            return (toValue - fromValue) * CostPerAttributePoint;
        }

        /// <summary>Product section 11.3: "Повышение BaseValue выше 15 требует явного правила, способности или GM Override" -- none of those exist yet in this codebase, so this fixture enforces the plain cap unconditionally.</summary>
        public static bool ExceedsNormalCap(long toValue) => toValue > NormalDevelopmentCap;
    }
}
