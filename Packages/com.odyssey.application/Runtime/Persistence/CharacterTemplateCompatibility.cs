using System;
using Odyssey.Rules.Versions;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// ODY-S04-103: ADR-023 section 6.1's "deterministic, rules-catalog-driven
    /// check" performed synchronously at <c>BindDraftToCampaign</c>. No
    /// Ruleset-catalog/compatibility-rule mechanism exists anywhere yet in
    /// this codebase (confirmed by search across <c>Odyssey.Rules</c> --
    /// only <see cref="RulesetVersion"/>/<c>SemVerValue</c> exist, no
    /// compatibility predicate of any kind) -- this class is this task's own
    /// minimal, deterministic engineering decision for what "usable with"
    /// concretely means, not an ADR-023 decision: the template's ruleset must
    /// be the exact same <c>RulesetId</c> as the campaign's, and (when both
    /// versions parse as canonical SemVer) share the same major version line,
    /// the same "same major line is compatible" convention
    /// <c>Odyssey.Application.Versions.CompatibilityRange</c> already uses
    /// elsewhere in this codebase for a different kind of version pair. A
    /// future Ruleset-catalog task may replace this with a real
    /// content-driven check without changing <c>BindDraftToCampaign</c>'s own
    /// call site, since this is the only place the rule is applied.
    /// </summary>
    public static class CharacterTemplateCompatibility
    {
        public static bool IsCompatible(string templateRulesetId, string templateRulesetVersion, string campaignRulesetId, string campaignRulesetVersion)
        {
            if (string.IsNullOrWhiteSpace(templateRulesetId)) throw new ArgumentException("TemplateRulesetId is required.", nameof(templateRulesetId));
            if (string.IsNullOrWhiteSpace(campaignRulesetId)) throw new ArgumentException("CampaignRulesetId is required.", nameof(campaignRulesetId));

            if (!string.Equals(templateRulesetId, campaignRulesetId, StringComparison.Ordinal))
            {
                return false;
            }

            if (RulesetVersion.TryParse(templateRulesetVersion, out RulesetVersion templateVersion)
                && RulesetVersion.TryParse(campaignRulesetVersion, out RulesetVersion campaignVersion))
            {
                return templateVersion.Major == campaignVersion.Major;
            }

            // Neither version is canonical SemVer (or only one is) -- fall
            // back to an exact string match rather than guessing at partial
            // compatibility from an unparseable value.
            return string.Equals(templateRulesetVersion, campaignRulesetVersion, StringComparison.Ordinal);
        }
    }
}
