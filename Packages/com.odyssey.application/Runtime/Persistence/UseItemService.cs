using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Odyssey.Application.Commands;
using Odyssey.Application.Content;
using Odyssey.Application.Effects;
using Odyssey.Application.Mechanics;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Content;
using Odyssey.Domain.Effects;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Rules.Combat;
using Odyssey.Rules.Mechanics;
using Odyssey.Rules.Versions;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// ODY-S06-107: `ADR-030` §10's own `UseItem` root command -- block D of the MVP roadmap, applying an
    /// item's own `Instant`-duration built-in effects and consuming exactly one unit of it, atomically.
    /// Deliberately built to `ODY-S06-106`'s FINAL, thrice-revised shape from the start (full idempotency,
    /// full compensation of every side-effect category including item consumption, resumable-after-partial-
    /// compensation-failure), per this task's own governing ТЗ §0's explicit instruction -- not a simpler
    /// version left for a future doработка round.
    ///
    /// Self-only: an item's `Instant` effects always target the actor who used it (the item's own holder) --
    /// see `UseItemIntent`'s own doc comment for why (no `ContentTargetRule` concept exists for items
    /// anywhere in this codebase). This makes `UseItemService` simpler than `ActivateAbilityService` in one
    /// respect: no per-target loop, since there is exactly one target (the actor) always.
    /// </summary>
    public static class UseItemService
    {
        private static readonly RngPurpose ItemUsageRollPurpose = RngPurpose.Parse("item.usage.roll");

        // Same practical MVP cap ODY-S06-106's own ActivateAbilityService already established for the
        // identical reason: formulas are not decoded until after these draws happen.
        private const int MaxRandomDraws = 4;

        public static Result<ItemUsageRecord> UseItem(IUseItemStateReader reader, IUseItemRepository apply, IContentCatalogRepository catalog, IActiveEffectRepository effects, IAuthoritativeRandomStreamFactory random, IWallClock clock, CampaignHandle campaign, RngKeyEpochId keyEpochId, UseItemRequest request)
        {
            if (reader == null || apply == null || catalog == null || effects == null || random == null || clock == null) throw new ArgumentNullException(nameof(reader));
            if (campaign == null || request == null) throw new ArgumentNullException(nameof(campaign));

            // ADR-008 rule 14 + ODY-S06-106's own final, thrice-revised idempotency shape, built here from
            // the start: (1) CompensatedAt != null -> a permanent, honest failure on replay, never success;
            // (2) CompensationStartedAt != null but CompensatedAt == null -> a PRIOR attempt is known to have
            // failed and its own compensation did not finish -- resume it (CompensateItemUsage reads its own
            // durable created-effect-id/consumed-item state back, so the empty list passed here on a resume
            // is safely ignored) and report failure regardless of whether the resumed attempt now finishes or
            // fails again; (3) otherwise, an existing record reflects a genuinely successful usage -- return
            // it as-is.
            Result<ItemUsageRecord> existing = apply.GetUsage(campaign, request.CommandId, request.CorrelationId);
            if (existing.IsSuccess)
            {
                if (existing.Value.CompensatedAt != null) return Result<ItemUsageRecord>.Failure(PreviouslyCompensated(request.CorrelationId));
                if (existing.Value.CompensationStartedAt != null)
                {
                    Result<ItemUsageRecord> resumed = apply.CompensateItemUsage(campaign, request.CommandId, Array.Empty<ActiveEffectId>(), request.ActorUserId, request.CorrelationId);
                    return Result<ItemUsageRecord>.Failure(resumed.IsFailure ? resumed.Error : PreviouslyCompensated(request.CorrelationId));
                }

                return existing;
            }

            Result<bool> authorized = Authorize(reader, campaign, request);
            if (authorized.IsFailure) return Result<ItemUsageRecord>.Failure(authorized.Error);
            if (!authorized.Value) return Result<ItemUsageRecord>.Failure(Denied(request.CorrelationId));

            Result<UseItemState> state = reader.Read(campaign, request.Intent, request.CorrelationId);
            if (state.IsFailure) return Result<ItemUsageRecord>.Failure(state.Error);

            bool needsRandomDraw = false;
            foreach (MechanicsPrimitiveEnvelope envelope in state.Value.InstantEffectPrimitives)
            {
                if (RequiresDice(envelope)) { needsRandomDraw = true; break; }
            }

            AttackRandomSample? randomSample = null;
            if (needsRandomDraw)
            {
                RulesetVersion version = RulesetVersion.Parse(campaign.Manifest.RulesetVersion);
                RandomDecisionContext context = RandomDecisionContext.Create(campaign.CampaignId, request.CommandId, 0, ItemUsageRollPurpose, version, keyEpochId, request.CorrelationId);
                Result<IAuthoritativeRandomStream> stream = random.Create(context);
                if (stream.IsFailure) return Result<ItemUsageRecord>.Failure(stream.Error);
                int[] values = new int[MaxRandomDraws];
                for (int drawIndex = 0; drawIndex < MaxRandomDraws; drawIndex++)
                {
                    Result<RandomSample> sample = stream.Value.NextInclusive(1, 100, drawIndex);
                    if (sample.IsFailure) return Result<ItemUsageRecord>.Failure(sample.Error);
                    values[drawIndex] = sample.Value.Value;
                }

                randomSample = new AttackRandomSample(Array.AsReadOnly(values));
            }

            // ODY-S06-107 section 2.4: each Instant built-in effect's own MechanicsPrimitiveEnvelope is
            // interpreted independently (its own formula(s) resolved against the SAME actor attributes and
            // the SAME random sample), and the results are summed -- an item with several Instant effects
            // applies all of them, not just the first.
            var resourceAdjustments = new List<ResolvedResourceAdjustment>();
            var effectsToApply = new List<ContentDefinitionRef>();
            foreach (MechanicsPrimitiveEnvelope envelope in state.Value.InstantEffectPrimitives)
            {
                MechanicsPrimitiveInterpretationResult interpreted = MechanicsPrimitiveInterpreter.Interpret(envelope, AttributeValues(state.Value.Actor), randomSample);
                resourceAdjustments.AddRange(interpreted.ResourceAdjustments);
                effectsToApply.AddRange(interpreted.EffectsToApply);
            }

            var deltas = new List<AttackDelta>();
            foreach (ResolvedResourceAdjustment adjustment in resourceAdjustments)
            {
                deltas.Add(new AttackDelta(TargetRef(request.Intent.ActorId, adjustment.ResourceKind), (int)adjustment.Amount));
            }

            Result<ItemUsageRecord> recorded = apply.RecordItemUsage(campaign, request.Intent.ActorId, request.Intent.Item, request.Intent.ExpectedItemRevision, request.Intent.ExpectedInventoryRevision, request.Intent.ExpectedCharacterResourcesRevision, deltas, effectsToApply, request.CommandId, request.CorrelationId);
            if (recorded.IsFailure) return recorded;

            // ODY-S06-107, built from the start to ODY-S06-106's own final (post-three-доработка) shape:
            // IActiveEffectRepository.CreateActiveEffect manages its own separate SQLite transaction and
            // cannot join RecordItemUsage's own transaction (this task's own governing ТЗ forbids modifying
            // that repository). If ANY application below fails, apply.CompensateItemUsage reverses
            // EVERYTHING RecordItemUsage already committed -- the consumed item unit, every already-created
            // effect, and the resource deltas -- not merely the resource deltas.
            var createdEffectIds = new List<ActiveEffectId>();
            for (int effectIndex = 0; effectIndex < effectsToApply.Count; effectIndex++)
            {
                ContentDefinitionRef effectRef = effectsToApply[effectIndex];
                Result<ActiveEffectRecord> applied = EffectApplicationHelper.ApplyEffect(catalog, effects, clock, campaign, effectRef, request.Intent.ActorId, request.ActorUserId, StableSubCommandId(request.CommandId, effectIndex), request.CorrelationId);
                if (applied.IsFailure)
                {
                    Result<ItemUsageRecord> compensated = apply.CompensateItemUsage(campaign, request.CommandId, createdEffectIds, request.ActorUserId, request.CorrelationId);
                    return compensated.IsFailure
                        ? Result<ItemUsageRecord>.Failure(compensated.Error)
                        : Result<ItemUsageRecord>.Failure(applied.Error);
                }

                createdEffectIds.Add(applied.Value.Effect.ActiveEffectId);
            }

            return Result<ItemUsageRecord>.Success(recorded.Value);
        }

        private static Result<bool> Authorize(IUseItemStateReader reader, CampaignHandle campaign, UseItemRequest request)
        {
            if (request.ActorIsMainGm) return Result<bool>.Success(true);
            return reader.CanControlActor(campaign, request.Intent.ActorId, request.ActorUserId, request.CorrelationId);
        }

        /// <summary>Mirrors `ActivateAbilityService.RequiresDice` verbatim: a constant-only formula must not trigger an RNG stream derivation at all (ADR-008 rule 14's own "no needless draw" convention).</summary>
        private static bool RequiresDice(MechanicsPrimitiveEnvelope envelope)
        {
            foreach (MechanicsPrimitive primitive in envelope.Primitives)
            {
                if (primitive is not AdjustResourcePrimitive adjust) continue;
                AttackDamageFormula formula = AttackDamageFormulaParser.Parse(adjust.AmountFormula);
                foreach (AttackFormulaTerm term in formula.Terms)
                {
                    if (term.Kind == AttackFormulaTermKind.DiceGroup) return true;
                }
            }

            return false;
        }

        /// <summary>Mirrors `ActivateAbilityService.AttributeValues` verbatim.</summary>
        private static IReadOnlyDictionary<AttributeDefinitionId, long> AttributeValues(CharacterRecord actor)
        {
            var values = new Dictionary<AttributeDefinitionId, long>(actor.Attributes.Count);
            foreach (AttributeValue attribute in actor.Attributes) values[attribute.AttributeDefinitionId] = attribute.EffectiveValue;
            return values;
        }

        private static string TargetRef(CharacterId characterId, ResourceDefinitionId resourceKind) => "character:" + characterId + ":" + resourceKind;

        /// <summary>Mirrors `ActivateAbilityService.StableSubCommandId`'s own SHA256-derivation pattern, minus the per-target index -- `UseItem` is always self-targeted, so only the effect index varies.</summary>
        private static CommandId StableSubCommandId(CommandId rootCommandId, int effectIndex)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes("ody-s06-107/v1/" + rootCommandId + "/" + effectIndex.ToString(CultureInfo.InvariantCulture)));
            var text = new StringBuilder(32);
            for (int i = 0; i < 16; i++) text.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return CommandId.Parse("cmd_" + text);
        }

        private static Error Denied(CorrelationId id) => Error.Create(ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Authorization, SafeReasonCode.PermissionDenied, UserMessageKey.Parse("errors.item.denied"), RetryDirective.DoNotRetry, id);

        /// <summary>Mirrors `ActivateAbilityService.PreviouslyCompensated`'s own exact semantics: this `CommandId` already ran, failed downstream, and had its own side effects genuinely reversed -- a permanent, honest failure on replay, never a silent success.</summary>
        private static Error PreviouslyCompensated(CorrelationId id) => Error.Create(ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Precondition, SafeReasonCode.ActionNotAllowed, UserMessageKey.Parse("errors.item.previously_compensated"), RetryDirective.DoNotRetry, id);
    }
}
