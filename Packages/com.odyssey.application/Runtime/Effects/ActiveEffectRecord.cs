using System;
using Odyssey.Domain.Effects;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Effects
{
    /// <summary>
    /// ODY-S05-502: persistence-facing wrapper around the Domain
    /// <see cref="ActiveEffect"/>, composing rather than duplicating --
    /// <see cref="ActiveEffect"/>'s own constructor already validates its
    /// fields, so this record adds only what the Domain type deliberately
    /// does not carry: a repository-facing <see cref="CampaignId"/> for
    /// campaign-boundary scoping. This is the exact same split
    /// `EquippedEntry`/`EquippedEntryRecord` (`ODY-S05-302`) already
    /// established for an analogous reason -- see that record's own doc
    /// comment and this task's own ExecPlan for the full reasoning.
    /// </summary>
    public sealed class ActiveEffectRecord
    {
        public ActiveEffectRecord(CampaignId campaignId, ActiveEffect effect)
        {
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (effect == null) throw new ArgumentNullException(nameof(effect));

            CampaignId = campaignId;
            Effect = effect;
        }

        public CampaignId CampaignId { get; }
        public ActiveEffect Effect { get; }
    }
}
