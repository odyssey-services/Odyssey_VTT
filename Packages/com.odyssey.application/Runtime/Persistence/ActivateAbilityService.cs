using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Odyssey.Application.Commands;
using Odyssey.Application.Content;
using Odyssey.Application.Effects;
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
    /// ODY-S06-106: `ADR-030` §9's own `ActivateAbility` root command -- a new, standalone root command
    /// (`ADR-002`), NOT bound to `CombatEncounter` (`ADR-030` §9.3: no turn/round/current-participant/
    /// action-slot check anywhere in this class). Mirrors `AttackEvaluationService`/`AttackApplyService`'s
    /// own combined shape (idempotency-first, then authorize, then read, then roll if needed, then apply)
    /// rather than splitting into a separate Preview/Evaluate pair -- no intervention/GM-approval concept
    /// exists for ability activation the way `ADR-029` §6's own stage 12 exists for attacks.
    ///
    /// Placed under `Runtime/Persistence/` (not a new `Runtime/Abilities/` folder, the way
    /// `AttackEvaluationService`/`AttackApplyService` live under `Runtime/Combat/`) because this task's own
    /// governing ТЗ §6 allowed-paths list names `Packages/com.odyssey.application/Runtime/Persistence/**`
    /// as the one wildcard-allowed Application directory and does not separately name a location for this
    /// orchestrating service -- an omission, not a deliberate exclusion (§2.5 explicitly requires building
    /// it) -- recorded explicitly here and in this task's own contract §18, not a silent deviation.
    /// </summary>
    public static class ActivateAbilityService
    {
        private static readonly RngPurpose AbilityActivationRollPurpose = RngPurpose.Parse("ability.activation.roll");

        // Same practical MVP cap ODY-S06-105's own AttackEvaluationService.EvaluateAttack already
        // established for the identical reason: a formula is not decoded until after these draws happen.
        private const int MaxRandomDraws = 4;

        public static Result<AbilityActivationRecord> ActivateAbility(IActivateAbilityStateReader reader, IActivateAbilityRepository apply, IContentCatalogRepository catalog, IActiveEffectRepository effects, IAuthoritativeRandomStreamFactory random, IWallClock clock, CampaignHandle campaign, RngKeyEpochId keyEpochId, ActivateAbilityRequest request)
        {
            if (reader == null || apply == null || catalog == null || effects == null || random == null || clock == null) throw new ArgumentNullException(nameof(reader));
            if (campaign == null || request == null) throw new ArgumentNullException(nameof(campaign));

            // ADR-008 rule 14: a retry never re-queries the authoritative random stream -- checked BEFORE
            // any RNG derivation, mirroring AttackApplyService.ResolveAttack's own identical ordering.
            Result<AbilityActivationRecord> existing = apply.GetActivation(campaign, request.CommandId, request.CorrelationId);
            if (existing.IsSuccess) return existing;

            Result<bool> authorized = Authorize(reader, campaign, request);
            if (authorized.IsFailure) return Result<AbilityActivationRecord>.Failure(authorized.Error);
            if (!authorized.Value) return Result<AbilityActivationRecord>.Failure(Denied(request.CorrelationId));

            Result<ActivateAbilityState> state = reader.Read(campaign, request.Intent, request.CorrelationId);
            if (state.IsFailure) return Result<AbilityActivationRecord>.Failure(state.Error);

            Result targetsValid = ValidateTargets(state.Value.Definition.TargetRule, request.Intent, request.CorrelationId);
            if (targetsValid.IsFailure) return Result<AbilityActivationRecord>.Failure(targetsValid.Error);

            bool needsRandomDraw = RequiresDice(state.Value.Primitives);
            AttackRandomSample? randomSample = null;
            if (needsRandomDraw)
            {
                RulesetVersion version = RulesetVersion.Parse(campaign.Manifest.RulesetVersion);
                RandomDecisionContext context = RandomDecisionContext.Create(campaign.CampaignId, request.CommandId, 0, AbilityActivationRollPurpose, version, keyEpochId, request.CorrelationId);
                Result<IAuthoritativeRandomStream> stream = random.Create(context);
                if (stream.IsFailure) return Result<AbilityActivationRecord>.Failure(stream.Error);
                int[] values = new int[MaxRandomDraws];
                for (int drawIndex = 0; drawIndex < MaxRandomDraws; drawIndex++)
                {
                    Result<RandomSample> sample = stream.Value.NextInclusive(1, 100, drawIndex);
                    if (sample.IsFailure) return Result<AbilityActivationRecord>.Failure(sample.Error);
                    values[drawIndex] = sample.Value.Value;
                }

                randomSample = new AttackRandomSample(Array.AsReadOnly(values));
            }

            MechanicsPrimitiveInterpretationResult interpreted = MechanicsPrimitiveInterpreter.Interpret(state.Value.Primitives, AttributeValues(state.Value.Actor), randomSample);

            var deltas = new List<AttackDelta>();
            foreach (AbilityResourceCost cost in state.Value.Definition.ResourceCosts)
            {
                deltas.Add(new AttackDelta(TargetRef(request.Intent.ActorId, cost.ResourceDefinitionId), (int)-cost.Amount));
            }

            // ODY-S06-106's own MVP decision (this task's own contract §18): each AdjustResource
            // primitive's own formula is evaluated ONCE (not re-rolled per target) and the same resolved
            // amount is applied to every chosen target -- a common real convention (e.g. an area effect
            // rolling once for everyone), and avoids exhausting the fixed 4-draw pool across multiple
            // targets.
            foreach (ResolvedResourceAdjustment adjustment in interpreted.ResourceAdjustments)
            {
                foreach (CharacterId targetId in request.Intent.TargetIds)
                {
                    deltas.Add(new AttackDelta(TargetRef(targetId, adjustment.ResourceKind), (int)adjustment.Amount));
                }
            }

            Result<AbilityActivationRecord> recorded = apply.RecordAbilityActivation(campaign, request.Intent.ActorId, request.Intent.CharacterAbilityId, request.Intent.TargetIds, deltas, interpreted.EffectsToApply, request.CommandId, request.CorrelationId);
            if (recorded.IsFailure) return recorded;

            // ODY-S06-106's own disclosed limit (task contract §18): IActiveEffectRepository.
            // CreateActiveEffect manages its own separate SQLite transaction -- it cannot be modified to
            // join the transaction above (this task's own governing ТЗ requires it stay unmodified), so
            // the resource-delta commit above is genuinely atomic on its own, but not further atomic WITH
            // these effect applications. Each is independently idempotent (a stable, deterministic
            // sub-CommandId derived from the root CommandId), so a retry after a partial failure here
            // re-applies only what did not already succeed.
            for (int effectIndex = 0; effectIndex < interpreted.EffectsToApply.Count; effectIndex++)
            {
                ContentDefinitionRef effectRef = interpreted.EffectsToApply[effectIndex];
                for (int targetIndex = 0; targetIndex < request.Intent.TargetIds.Count; targetIndex++)
                {
                    CharacterId targetId = request.Intent.TargetIds[targetIndex];
                    Result<ActiveEffectRecord> applied = ApplyEffect(catalog, effects, clock, campaign, effectRef, targetId, request.ActorUserId, StableSubCommandId(request.CommandId, effectIndex, targetIndex), request.CorrelationId);
                    if (applied.IsFailure) return Result<AbilityActivationRecord>.Failure(applied.Error);
                }
            }

            return Result<AbilityActivationRecord>.Success(recorded.Value);
        }

        private static Result<bool> Authorize(IActivateAbilityStateReader reader, CampaignHandle campaign, ActivateAbilityRequest request)
        {
            if (request.ActorIsMainGm) return Result<bool>.Success(true);
            return reader.CanControlActor(campaign, request.Intent.ActorId, request.ActorUserId, request.CorrelationId);
        }

        /// <summary>
        /// ODY-S06-106 section 2.6: `ContentTargetRule`'s own fields have never had a real runtime
        /// validator anywhere in this codebase (confirmed by direct code read: `CatalogValidationContracts.
        /// ValidateAbility`'s own doc comment records "target rule shape... already ctor-guaranteed... no
        /// live registry to check against yet" as an explicit, disclosed MVP boundary, not a skipped check)
        /// -- so this is this task's own first real consumer, deliberately as narrow as the type's own
        /// structural fields: target count within `[MinimumCount, MaximumCount]`, and self-targeting only
        /// when `AllowSelf` is true. No new targeting vocabulary is invented.
        /// </summary>
        private static Result ValidateTargets(ContentTargetRule rule, ActivateAbilityIntent intent, CorrelationId correlationId)
        {
            long count = intent.TargetIds.Count;
            if (count < rule.MinimumCount || count > rule.MaximumCount) return Result.Failure(InvalidTarget(correlationId));
            if (!rule.AllowSelf)
            {
                foreach (CharacterId targetId in intent.TargetIds)
                {
                    if (targetId.Equals(intent.ActorId)) return Result.Failure(InvalidTarget(correlationId));
                }
            }

            return Result.Success();
        }

        /// <summary>ODY-S06-106: parses every `AdjustResource` primitive's own formula and checks for a real dice-group term -- a constant-only formula (e.g. "5") must not trigger an RNG stream derivation at all (ADR-008 rule 14's own "no needless draw" convention, mirrored from `AttackEvaluationService.PreviewAttack`'s own deliberate no-RNG path).</summary>
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

        /// <summary>ODY-S06-102's own established pattern: fold a `CharacterRecord`'s already-loaded `Attributes` into a plain lookup, keyed by the same `AttributeDefinitionId` catalog key the formula grammar's own `attributeReference` term resolves against.</summary>
        private static IReadOnlyDictionary<AttributeDefinitionId, long> AttributeValues(CharacterRecord actor)
        {
            var values = new Dictionary<AttributeDefinitionId, long>(actor.Attributes.Count);
            foreach (AttributeValue attribute in actor.Attributes) values[attribute.AttributeDefinitionId] = attribute.EffectiveValue;
            return values;
        }

        /// <summary>Mirrors `ItemEffectLifecycleService`'s own established "resolve id through the catalog, do not trust a cache" pattern verbatim: `GetContentDefinition` by the ref's own `DefinitionId`, then validate `Version`/`Status`/`DefinitionType` before decoding, before ever constructing a real `ActiveEffect`.</summary>
        private static Result<ActiveEffectRecord> ApplyEffect(IContentCatalogRepository catalog, IActiveEffectRepository effects, IWallClock clock, CampaignHandle campaign, ContentDefinitionRef effectRef, CharacterId targetId, UserId appliedByUserId, CommandId commandId, CorrelationId correlationId)
        {
            Result<ContentDefinitionRecord> fetched = catalog.GetContentDefinition(campaign, effectRef.DefinitionId, correlationId);
            if (fetched.IsFailure) return Result<ActiveEffectRecord>.Failure(fetched.Error);
            ContentDefinitionRecord content = fetched.Value;
            if (content.Version != effectRef.Version || content.Status != ContentDefinitionStatus.Published || content.DefinitionType != ContentDefinitionType.Effect)
            {
                return Result<ActiveEffectRecord>.Failure(InvalidTarget(correlationId));
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

        private static string TargetRef(CharacterId characterId, ResourceDefinitionId resourceKind) => "character:" + characterId + ":" + resourceKind;

        /// <summary>Mirrors `ItemEffectLifecycleService.StableId`'s own established pattern for deriving a deterministic sub-`CommandId` from a root `CommandId` plus a stable purpose string -- lets each individually-idempotent `CreateActiveEffect` call replay correctly on a retry of the whole `ActivateAbility` command.</summary>
        private static CommandId StableSubCommandId(CommandId rootCommandId, int effectIndex, int targetIndex)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes("ody-s06-106/v1/" + rootCommandId + "/" + effectIndex.ToString(CultureInfo.InvariantCulture) + "/" + targetIndex.ToString(CultureInfo.InvariantCulture)));
            var text = new StringBuilder(32);
            for (int i = 0; i < 16; i++) text.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return CommandId.Parse("cmd_" + text);
        }

        private static Error Denied(CorrelationId id) => Error.Create(ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Authorization, SafeReasonCode.PermissionDenied, UserMessageKey.Parse("errors.ability.denied"), RetryDirective.DoNotRetry, id);
        private static Error InvalidTarget(CorrelationId id) => Error.Create(ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Validation, SafeReasonCode.InvalidRequest, UserMessageKey.Parse("errors.ability.invalid_target"), RetryDirective.DoNotRetry, id);
    }
}
