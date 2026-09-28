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
    /// `CommandId`-keyed idempotency for every mutating method -- no new
    /// persistence idiom is invented.
    ///
    /// `ODY-S05-502`'s own minimum contract was create, read-by-id,
    /// list-by-`TargetRef`, list-by-`SourceRef` only. `ODY-S05-504` added
    /// <see cref="ExpireActiveEffect"/> (`Status → Expired`) and `ODY-S05-505`
    /// added <see cref="SetItemEffectEquipped"/> (`Status ↔ Suspended`/`Active`
    /// for `WhileItemEquipped`) -- by exclusion, `ODY-S05-506` is the only
    /// remaining task in this range that could own `Status → Removed`, per
    /// `ADR-028` §6 rule 2's own minimum contract ("transition `Status`
    /// (expire/suspend/resume/remove)"), and adds
    /// <see cref="RemoveActiveEffect"/> for exactly that. No stacking-policy
    /// mutation (`ODY-S05-503`, a pure decision layer with no repository
    /// change) exists on this interface. Direct (non-item) creation
    /// (`ADR-028` §10 rule 2, also `ODY-S05-506`'s own territory) needs no
    /// new method here at all -- it is a MainGM-gated Application-layer
    /// wrapper (`ActiveEffectDirectCommandService`) over the already-existing
    /// <see cref="CreateActiveEffect"/>, unmodified.
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

        /// <summary>
        /// ODY-S05-504: transitions an `ActiveEffect` row's own `Status` to
        /// `Expired`, `Revision`-CAS-guarded against <paramref name="expectedRevision"/>,
        /// routed through the shared `SqliteSavingPipeline` (`ADR-028` §12).
        /// Idempotent by <paramref name="commandId"/> -- a replayed call with
        /// the same <paramref name="commandId"/> returns the already-expired
        /// record without a second transition. Never physically deletes the
        /// row (`ADR-012`'s append-only-history discipline, `ADR-028` §9's
        /// own analogous rule for `RemoveActiveEffect`). Callers decide
        /// *whether* to expire (`ActiveEffectExpiryRules`, `Odyssey.Rules.Effects.EffectConditionRules`)
        /// -- this method only *applies* that decision, mirroring how
        /// `ODY-S05-403`'s own `ApplyItemDefinitionMigration` was the
        /// separate "apply" half of `ODY-S05-402`'s own "decide" half.
        /// </summary>
        Result<ActiveEffectRecord> ExpireActiveEffect(CampaignHandle campaign, CampaignId campaignId, ActiveEffectId activeEffectId, long expectedRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// Suspends or resumes an item-sourced WhileItemEquipped effect using its
        /// pinned snapshot and a revision CAS. Returns the committed revision.
        /// Replaying the same command returns that revision even after later writes;
        /// changing the effect, actor, revision or direction is a replay conflict.
        /// Only Active to Suspended and Suspended to Active are allowed.
        /// Authorization is inherited from the successful host equipment operation.
        /// </summary>
        Result<long> SetItemEffectEquipped(CampaignHandle campaign, CampaignId campaignId, ActiveEffectId activeEffectId, bool equipped, long expectedRevision, UserId actorUserId, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S05-506: `ADR-028` §9's own explicit early-removal command --
        /// transitions an `Active`/`Suspended` `ActiveEffect` row's own
        /// `Status` to `Removed`, `Revision`-CAS-guarded against
        /// <paramref name="expectedRevision"/>, routed through the shared
        /// `SqliteSavingPipeline` (`ADR-028` §12). Never physically deletes
        /// the row (`ADR-012`'s append-only-history discipline). Idempotent
        /// by <paramref name="commandId"/>. `ADR-028` §10 rule 3's own
        /// MainGM-only gate is checked as this method's own first statement,
        /// before any I/O -- the same repository-level placement
        /// `SqliteCharacterRepository.DeleteCharacterPermanently` already
        /// established for an analogous explicit lifecycle-ending command,
        /// not a separate Application-layer wrapper.
        /// </summary>
        Result<ActiveEffectRecord> RemoveActiveEffect(CampaignHandle campaign, CampaignId campaignId, ActiveEffectId activeEffectId, UserId actorUserId, long expectedRevision, CommandId commandId, CorrelationId correlationId);
    }

    /// <summary>
    /// ODY-S10-103 follow-up (independent review finding): <see cref="RemoveActiveEffectAsSystemRollback"/> was
    /// first added directly to the public <see cref="IActiveEffectRepository"/> -- reachable, with no
    /// authorization check at all, by any code holding that (very widely held) interface. Splitting it into its
    /// own <c>internal</c> interface is the fix: only the two legitimate callers
    /// (<c>SqliteActivateAbilityRepository</c>, <c>SqliteUseItemRepository</c>), the implementation
    /// (<c>SqliteActiveEffectRepository</c>), and the assemblies granted access via
    /// <c>[assembly: InternalsVisibleTo]</c> (<c>AssemblyInfo.cs</c> in this same folder) can see it at all --
    /// not merely "should not call it by convention". A same-class <c>internal</c> method was considered and
    /// rejected: both callers hold their effect collaborator as <see cref="IActiveEffectRepository"/> (for DI and
    /// so tests can substitute a fake), so a member that exists only on the concrete class would be unreachable
    /// through that reference without an upcast the callers do not otherwise need; a separate interface keeps the
    /// public contract intact while still requiring an explicit, visible cast at each of the two call sites.
    /// </summary>
    internal interface IActiveEffectSystemRollback
    {
        /// <summary>
        /// ODY-S10-103: the SYSTEM ROLLBACK counterpart of <see cref="IActiveEffectRepository.RemoveActiveEffect"/>:
        /// it ends an effect that a failed ability activation / item use created, when the host compensates that
        /// failed attempt. It performs exactly the same removal (same validation, same revision compare-and-set,
        /// same idempotency ledger) but no MainGM check, because compensating the actor's own failed attempt is not
        /// a privilege the actor gains: a Player whose activation failed must still be rolled back. It deliberately
        /// takes NO role/flag parameter (a flag would just be a second way to claim MainGM); it is a separate
        /// operation for persistence-layer compensation code only and is not mapped to any user command. Kept off
        /// <see cref="IActiveEffectRepository"/> on purpose -- see this interface's own remarks.
        /// </summary>
        Result<ActiveEffectRecord> RemoveActiveEffectAsSystemRollback(CampaignHandle campaign, CampaignId campaignId, ActiveEffectId activeEffectId, UserId actorUserId, long expectedRevision, CommandId commandId, CorrelationId correlationId);
    }
}
