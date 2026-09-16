using Odyssey.Application.Commands;
using Odyssey.Application.Content;
using Odyssey.Application.Effects;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Content;
using Odyssey.Domain.Effects;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Mechanics
{
    /// <summary>
    /// ODY-S06-107: `ActivateAbilityService.ApplyEffect`'s own body, extracted verbatim (no logic change --
    /// same catalog resolve/validate/decode steps, same `ActiveEffect` construction, same error) so both
    /// `ActivateAbilityService` and the new `UseItemService` share one "resolve effect ref -> decode ->
    /// create real `ActiveEffect`" step, rather than duplicating the exact code that already took `106`
    /// three rounds of fixes to get right. `ActivateAbilityService.cs`'s own call site now delegates here;
    /// its own behavior and every `TC-ABILITY-*` test are unaffected -- a pure extraction, not a rewrite.
    /// </summary>
    public static class EffectApplicationHelper
    {
        /// <summary>Mirrors `ItemEffectLifecycleService`'s own established "resolve id through the catalog, do not trust a cache" pattern verbatim: `GetContentDefinition` by the ref's own `DefinitionId`, then validate `Version`/`Status`/`DefinitionType` before decoding, before ever constructing a real `ActiveEffect`.</summary>
        public static Result<ActiveEffectRecord> ApplyEffect(IContentCatalogRepository catalog, IActiveEffectRepository effects, IWallClock clock, CampaignHandle campaign, ContentDefinitionRef effectRef, CharacterId targetId, UserId appliedByUserId, CommandId commandId, CorrelationId correlationId)
        {
            Result<ContentDefinitionRecord> fetched = catalog.GetContentDefinition(campaign, effectRef.DefinitionId, correlationId);
            if (fetched.IsFailure) return Result<ActiveEffectRecord>.Failure(fetched.Error);
            ContentDefinitionRecord content = fetched.Value;
            if (content.Version != effectRef.Version || content.Status != ContentDefinitionStatus.Published || content.DefinitionType != ContentDefinitionType.Effect)
            {
                return Result<ActiveEffectRecord>.Failure(InvalidEffect(correlationId));
            }

            Result<EffectDefinition> decoded = TypedDefinitionCodec.DecodeEffect(content.DefinitionType, content.PropertiesJson, correlationId);
            if (decoded.IsFailure) return Result<ActiveEffectRecord>.Failure(decoded.Error);

            UtcInstant now = clock.GetUtcNow();
            var mechanicsSnapshot = new EffectMechanicsSnapshot(effectRef, content.Version, content.DefinitionType, content.PropertiesJson);
            var activeEffect = new ActiveEffect(
                ActiveEffectId.NewId(now),
                effectRef,
                mechanicsSnapshot,
                ActiveEffectSourceRef.ForAction(),
                ActiveEffectTargetRef.ForCharacter(targetId),
                ActiveEffectStatus.Active,
                stackCount: 1,
                appliedByUserId,
                now,
                expiresAt: null,
                revision: 1);
            var record = new ActiveEffectRecord(campaign.CampaignId, activeEffect);
            return effects.CreateActiveEffect(campaign, record, commandId, correlationId);
        }

        // Same ErrorCode/Category/SafeReasonCode/RetryDirective the original ActivateAbilityService.InvalidTarget
        // produced -- the message key literal is kept identical too, so behavior (including the exact Error
        // any existing caller compares against) is bit-for-bit unchanged by this extraction.
        private static Error InvalidEffect(CorrelationId id) => Error.Create(ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Validation, SafeReasonCode.InvalidRequest, UserMessageKey.Parse("errors.ability.invalid_target"), RetryDirective.DoNotRetry, id);
    }
}
