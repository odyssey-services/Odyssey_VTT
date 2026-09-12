using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Effects;
using Odyssey.Application.Results;
using Odyssey.Domain.Effects;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// ODY-S05-502: `ADR-028` §6's own explicit mandate -- a standalone
    /// repository contract, never an extension of `IInventoryRepository` or
    /// `ICharacterRepository` (`ADR-028` §8.2 rule 2: `ActiveEffect` is not
    /// owned by Inventory or Character). Conventions mirror every other
    /// repository contract in this namespace exactly (`ADR-028` §6 rule 3):
    /// `CampaignHandle` first, `CorrelationId` last, `Result&lt;T&gt;` return,
    /// `CommandId`-keyed idempotency for the one mutating method -- no new
    /// persistence idiom is invented.
    ///
    /// This is `ODY-S05-502`'s own minimum contract only: create, read-by-id,
    /// list-by-`TargetRef`, list-by-`SourceRef`. No stacking-policy mutation
    /// (`ODY-S05-503`), no duration/expiry transition (`ODY-S05-504`/`505`),
    /// and no removal (`ODY-S05-506`) exist on this interface -- those are
    /// each their own later task's own contract addition, not decided here.
    /// </summary>
    public interface IActiveEffectRepository
    {
        /// <summary>Creates a new <see cref="ActiveEffectRecord"/>. Idempotent by <paramref name="commandId"/>, routed through the shared `SqliteSavingPipeline` (`ADR-028` §12) -- a replayed call with the same <paramref name="commandId"/> returns the original record without creating a second row.</summary>
        Result<ActiveEffectRecord> CreateActiveEffect(CampaignHandle campaign, ActiveEffectRecord record, CommandId commandId, CorrelationId correlationId);

        /// <summary>Reads one <see cref="ActiveEffectRecord"/> by its own id. A plain read; no pipeline/transaction is needed for it (`ADR-028` §6's own precedent, mirroring `SqliteSceneRepository.GetToken`).</summary>
        Result<ActiveEffectRecord> GetActiveEffect(CampaignHandle campaign, ActiveEffectId activeEffectId, CorrelationId correlationId);

        /// <summary>Lists every <see cref="ActiveEffectRecord"/> whose `TargetRef` matches, campaign-wide -- needed by every future stacking/duration/removal check that must find "what is currently affecting this target."</summary>
        Result<IReadOnlyList<ActiveEffectRecord>> ListActiveEffectsByTarget(CampaignHandle campaign, CampaignId campaignId, ActiveEffectTargetRef targetRef, CorrelationId correlationId);

        /// <summary>Lists every <see cref="ActiveEffectRecord"/> whose `SourceRef` matches, campaign-wide -- needed when an item is unequipped/consumed/destroyed and every effect it sourced must be found (`ADR-027` §8.1 rule 2's own analogous `CharacterAbility` cleanup requirement, applied here to effects).</summary>
        Result<IReadOnlyList<ActiveEffectRecord>> ListActiveEffectsBySource(CampaignHandle campaign, CampaignId campaignId, ActiveEffectSourceRef sourceRef, CorrelationId correlationId);
    }
}
