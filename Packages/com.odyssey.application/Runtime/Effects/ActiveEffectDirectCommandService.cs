using System;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Content;
using Odyssey.Domain.Effects;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Effects
{
    /// <summary>
    /// ODY-S05-506: `ADR-028` §10 rule 2's own MainGM-only gate for creating
    /// an `ActiveEffect` directly, not through any item -- the exact opposite
    /// idiom from `ODY-S05-505`'s own `ItemEffectLifecycleService`, whose own
    /// doc comment states "this service adds no MainGM restriction" (rule 1,
    /// "inherit the existing item-use model"). This is rule 2/3's own "new,
    /// stricter-than-general restriction" pattern, the same one
    /// `ADR-025` §5.2 already used for `DeleteCharacterPermanently`.
    ///
    /// `IActiveEffectRepository.CreateActiveEffect` (`ODY-S05-502`) is already
    /// the correct low-level primitive for creating a row -- "who created it"
    /// is already structurally traceable via `SourceRef.Kind == GMDirect`
    /// (`ActiveEffectSourceRef.ForGMDirect()`, declared by `ODY-S05-502` as a
    /// forward reference this task wires). No new repository method is
    /// needed or added here; this service only gates and builds the record,
    /// then delegates to the unmodified `CreateActiveEffect`.
    /// </summary>
    public static class ActiveEffectDirectCommandService
    {
        /// <summary>
        /// Builds a new `ActiveEffect` sourced `ForGMDirect()` and creates it
        /// via the unmodified `CreateActiveEffect`. <paramref name="actorIsMainGm"/>
        /// is checked as this method's own first statement, before any
        /// repository access at all -- matching `RemoveActiveEffect`'s own
        /// placement of the identical gate.
        /// </summary>
        public static Result<ActiveEffectRecord> CreateDirectActiveEffect(
            IActiveEffectRepository effects,
            CampaignHandle campaign,
            CampaignId campaignId,
            ContentDefinitionRef effectDefinitionRef,
            EffectMechanicsSnapshot effectMechanicsSnapshot,
            ActiveEffectTargetRef target,
            UserId actorUserId,
            bool actorIsMainGm,
            UtcInstant appliedAt,
            UtcInstant? expiresAt,
            CommandId commandId,
            CorrelationId correlationId)
        {
            if (effects == null) throw new ArgumentNullException(nameof(effects));
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            // ADR-028 section 10 rule 2: MainGM-only, checked before any
            // repository access at all -- matching every other MainGM-only
            // gate's own convention (SqliteCharacterRepository.DeleteCharacterPermanently,
            // and this task's own RemoveActiveEffect).
            if (!actorIsMainGm)
            {
                return Result<ActiveEffectRecord>.Failure(PersistenceFailures.ActiveEffectOperationDenied(correlationId));
            }

            var activeEffectId = ActiveEffectId.NewId(appliedAt);
            var effect = new ActiveEffect(
                activeEffectId,
                effectDefinitionRef,
                effectMechanicsSnapshot,
                ActiveEffectSourceRef.ForGMDirect(),
                target,
                ActiveEffectStatus.Active,
                1,
                actorUserId,
                appliedAt,
                expiresAt,
                1);
            var record = new ActiveEffectRecord(campaignId, effect);

            return effects.CreateActiveEffect(campaign, record, commandId, correlationId);
        }
    }
}
