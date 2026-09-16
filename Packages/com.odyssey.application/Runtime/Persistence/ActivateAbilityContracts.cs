using System;
using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Content;
using Odyssey.Domain.Effects;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// ODY-S06-106: `ADR-030` §9's own `ActivateAbility` root command -- read-only authoritative state seam,
    /// mirroring `IAttackStateReader` (`ODY-S05-603`) exactly. Implementations must not write.
    /// </summary>
    public interface IActivateAbilityStateReader
    {
        Result<ActivateAbilityState> Read(CampaignHandle campaign, ActivateAbilityIntent intent, CorrelationId correlationId);
        Result<bool> CanControlActor(CampaignHandle campaign, CharacterId actorId, UserId userId, CorrelationId correlationId);
    }

    /// <summary>
    /// The command's own public surface -- `CharacterAbilityId` (which ability to activate) plus the
    /// actor's own already-chosen targets (validated against the `AbilityDefinition`'s own `TargetRule` at
    /// read time). Deliberately not scene/encounter-scoped (`ADR-030` §9.3: `ActivateAbility` is not bound
    /// to `CombatEncounter`).
    /// </summary>
    public sealed class ActivateAbilityIntent
    {
        /// <summary>
        /// ODY-S06-106 doработка (product-owner-ordered fix): <paramref name="expectedCharacterAbilitiesRevision"/>/
        /// <paramref name="expectedCharacterResourcesRevision"/> are `ADR-022` §5 rule 2's own required
        /// optimistic-concurrency declaration, by direct precedent of `AcquireAbilityViaProgressionPurchase`'s
        /// own `expectedMechanicsRevision`/`expectedCharacterAbilitiesRevision` pair -- both sections
        /// `ActivateAbility` actually reads/mutates (`CharacterAbilities` for the ability being activated,
        /// `CharacterResources` for the cost charge/`AdjustResource` deltas) must be declared and re-checked
        /// fresh inside `SqliteActivateAbilityRepository.RecordAbilityActivation`'s own transaction -- a
        /// stale caller-declared value is a real, honest `Result` rejection, not a silent apply over
        /// out-of-date state.
        /// </summary>
        public ActivateAbilityIntent(CharacterId actorId, CharacterAbilityId characterAbilityId, IReadOnlyList<CharacterId> targetIds, long expectedCharacterAbilitiesRevision, long expectedCharacterResourcesRevision)
        {
            if (!actorId.IsValid) throw new ArgumentException("ActorId is required.", nameof(actorId));
            if (!characterAbilityId.IsValid) throw new ArgumentException("CharacterAbilityId is required.", nameof(characterAbilityId));
            if (targetIds == null || targetIds.Count == 0) throw new ArgumentException("At least one target is required.", nameof(targetIds));
            if (expectedCharacterAbilitiesRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedCharacterAbilitiesRevision));
            if (expectedCharacterResourcesRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedCharacterResourcesRevision));
            CharacterId[] copy = new CharacterId[targetIds.Count];
            for (int index = 0; index < targetIds.Count; index++)
            {
                if (!targetIds[index].IsValid) throw new ArgumentException("Target identity is required.", nameof(targetIds));
                copy[index] = targetIds[index];
            }

            ActorId = actorId;
            CharacterAbilityId = characterAbilityId;
            TargetIds = Array.AsReadOnly(copy);
            ExpectedCharacterAbilitiesRevision = expectedCharacterAbilitiesRevision;
            ExpectedCharacterResourcesRevision = expectedCharacterResourcesRevision;
        }

        public CharacterId ActorId { get; }
        public CharacterAbilityId CharacterAbilityId { get; }
        public IReadOnlyList<CharacterId> TargetIds { get; }
        public long ExpectedCharacterAbilitiesRevision { get; }
        public long ExpectedCharacterResourcesRevision { get; }
    }

    /// <summary>
    /// ODY-S06-106: everything `ActivateAbilityService` needs, already resolved by the reader --
    /// `CharacterAbility -> AbilityDefinitionId -> AbilityDefinition -> MechanicsPrimitiveEnvelope`, the
    /// actor's own already-loaded `CharacterRecord` (attributes, resources, ownership), all in one read.
    /// </summary>
    public sealed class ActivateAbilityState
    {
        public ActivateAbilityState(CharacterRecord actor, CharacterAbility ability, AbilityDefinition definition, MechanicsPrimitiveEnvelope primitives)
        {
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
            Ability = ability ?? throw new ArgumentNullException(nameof(ability));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Primitives = primitives ?? throw new ArgumentNullException(nameof(primitives));
        }

        public CharacterRecord Actor { get; }
        public CharacterAbility Ability { get; }
        public AbilityDefinition Definition { get; }
        public MechanicsPrimitiveEnvelope Primitives { get; }
    }

    public sealed class ActivateAbilityRequest
    {
        public ActivateAbilityRequest(ActivateAbilityIntent intent, UserId actorUserId, bool actorIsMainGm, CommandId commandId, CorrelationId correlationId)
        {
            Intent = intent ?? throw new ArgumentNullException(nameof(intent));
            if (!actorUserId.IsValid || !commandId.IsValid || !correlationId.IsValid) throw new ArgumentException("Actor, command and correlation identities are required.");
            ActorUserId = actorUserId; ActorIsMainGm = actorIsMainGm; CommandId = commandId; CorrelationId = correlationId;
        }

        public ActivateAbilityIntent Intent { get; }
        public UserId ActorUserId { get; }
        public bool ActorIsMainGm { get; }
        public CommandId CommandId { get; }
        public CorrelationId CorrelationId { get; }
    }

    /// <summary>
    /// ODY-S06-106: the durable record of one `ActivateAbility` command's own already-applied outcome --
    /// mirrors `AttackOutcomeRecord`'s own role exactly (the idempotency read `ActivateAbilityService`
    /// checks BEFORE deriving any RNG sample, `ADR-008` rule 14).
    /// </summary>
    public sealed class AbilityActivationRecord
    {
        public AbilityActivationRecord(CommandId commandId, CampaignId campaignId, CharacterId actorId, CharacterAbilityId characterAbilityId, IReadOnlyList<CharacterId> targetIds, IReadOnlyList<AttackDelta> resourceDeltas, IReadOnlyList<ContentDefinitionRef> appliedEffectRefs, UtcInstant occurredAt, UtcInstant? compensatedAt)
        {
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!actorId.IsValid) throw new ArgumentException("ActorId is required.", nameof(actorId));
            if (!characterAbilityId.IsValid) throw new ArgumentException("CharacterAbilityId is required.", nameof(characterAbilityId));

            CommandId = commandId;
            CampaignId = campaignId;
            ActorId = actorId;
            CharacterAbilityId = characterAbilityId;
            TargetIds = Copy(targetIds ?? throw new ArgumentNullException(nameof(targetIds)));
            ResourceDeltas = Copy(resourceDeltas ?? throw new ArgumentNullException(nameof(resourceDeltas)));
            AppliedEffectRefs = Copy(appliedEffectRefs ?? throw new ArgumentNullException(nameof(appliedEffectRefs)));
            OccurredAt = occurredAt;
            CompensatedAt = compensatedAt;
        }

        public CommandId CommandId { get; }
        public CampaignId CampaignId { get; }
        public CharacterId ActorId { get; }
        public CharacterAbilityId CharacterAbilityId { get; }
        public IReadOnlyList<CharacterId> TargetIds { get; }
        public IReadOnlyList<AttackDelta> ResourceDeltas { get; }
        public IReadOnlyList<ContentDefinitionRef> AppliedEffectRefs { get; }
        public UtcInstant OccurredAt { get; }

        /// <summary>
        /// ODY-S06-106 doработка (product-owner-ordered fix for the disclosed `ApplyEffect`
        /// cross-repository-atomicity gap independent verification rejected as an unfixed "accepted design
        /// decision"): non-null means this activation's own `ResourceDeltas` were genuinely reversed after
        /// at least one `ApplyEffect` primitive failed to apply -- the activation as a whole did NOT
        /// succeed, and this is never a silent success. A replay of the same `CommandId` after compensation
        /// permanently returns failure (see `ActivateAbilityService`'s own idempotency-check step) --
        /// exactly like the `AppliedCommands` ledger's own "a `CommandId`'s outcome is fixed forever" rule,
        /// just with a compensated/failed outcome instead of a successful one.
        /// </summary>
        public UtcInstant? CompensatedAt { get; }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source) { T[] copy = new T[source.Count]; for (int index = 0; index < copy.Length; index++) copy[index] = source[index]; return Array.AsReadOnly(copy); }
    }

    /// <summary>
    /// ODY-S06-106: the write side of `ActivateAbility` -- a standalone repository, by the same precedent
    /// `IAttackApplyRepository` already established as separate from `ICharacterRepository`'s own general
    /// CRUD surface (closer in shape to a root-command apply pipeline than to Character CRUD).
    /// </summary>
    public interface IActivateAbilityRepository
    {
        /// <summary>Idempotency read, called BEFORE any RNG derivation -- see `IAttackApplyRepository.GetOutcome`'s own identical role.</summary>
        Result<AbilityActivationRecord> GetActivation(CampaignHandle campaign, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// Atomically applies every resource delta (the ability's own cost, negative, plus every
        /// `AdjustResource` primitive's own resolved amount) in one SQLite transaction -- all or nothing,
        /// reusing `SqliteAttackApplyRepository`'s own already-accepted `ApplyAttackDelta`/
        /// `ApplyCharacterResourceDelta` logic verbatim (`ODY-S05-609`, made `internal` for this reuse, not
        /// modified). The SAME transaction also re-checks <paramref name="expectedCharacterAbilitiesRevision"/>/
        /// <paramref name="expectedCharacterResourcesRevision"/> against a fresh read of the actor's own
        /// `Character` row (`ADR-022` §5 rule 2, ODY-S06-106 doработка) -- a stale value rejects the whole
        /// command before any delta is applied. Only once every delta succeeds does this method record the
        /// durable `AbilityActivationRecord` and return success -- `effectsToApply` travels through
        /// unapplied; the caller (`ActivateAbilityService`) applies each one afterward via the existing,
        /// unmodified `IActiveEffectRepository.CreateActiveEffect` (a separate repository with its own
        /// separate transaction lifecycle). If any one of those applications fails, the caller invokes
        /// <see cref="CompensateAbilityActivation"/> to reverse this method's own already-committed deltas --
        /// see that method's own doc comment.
        /// </summary>
        Result<AbilityActivationRecord> RecordAbilityActivation(CampaignHandle campaign, CharacterId actorId, CharacterAbilityId characterAbilityId, IReadOnlyList<CharacterId> targetIds, IReadOnlyList<AttackDelta> resourceDeltas, IReadOnlyList<ContentDefinitionRef> effectsToApply, long expectedCharacterAbilitiesRevision, long expectedCharacterResourcesRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S06-106 doработка (product-owner-ordered fix for the `ApplyEffect` cross-repository-
        /// atomicity gap, extended by a SECOND доработка after independent verification found the first fix
        /// still left already-created `ActiveEffect` rows behind on a later-effect/later-target failure):
        /// reverses EVERYTHING an already-recorded `AbilityActivationRecord` (identified by its own original
        /// <paramref name="originalCommandId"/>) genuinely applied before the failure point --
        /// <paramref name="createdEffectIds"/> (every `ActiveEffectId` the caller's own `ApplyEffect` loop
        /// actually succeeded in creating before hitting the failure, passed in by `ActivateAbilityService`
        /// since only it tracks that in-flight list) are each removed via the existing, unmodified
        /// `IActiveEffectRepository.RemoveActiveEffect` FIRST; only once every removal succeeds does this
        /// method reverse `ResourceDeltas` -- in one new SQLite transaction, atomically -- by re-applying each
        /// one negated through the same `SqliteAttackApplyRepository.ApplyAttackDelta`, then marks
        /// `CompensatedAt`. `RemoveActiveEffect` uses `actorIsMainGm: true` regardless of the original
        /// activation actor's own permission level -- this is an internal system rollback of the SAME
        /// activation attempt that actor themselves initiated and which failed, not a new capability granted
        /// to them (the same "internal operations bypass the public command's own user-facing authorization"
        /// precedent `SqliteAttackApplyRepository.ApplyCharacterResourceDelta` already established by writing
        /// directly to `Character.ResourcesJson` rather than through `SetResourceCurrentValue`). Each removal
        /// uses its own deterministic sub-`CommandId`, distinct from the creation-time one (a different hash
        /// input), so `RemoveActiveEffect`'s own idempotency ledger entry never collides with `CreateActiveEffect`'s
        /// own -- a same-`CommandId` collision would make `SqliteSavingPipeline` treat the removal as a replay
        /// of the CREATE command and silently no-op instead of actually removing anything. Idempotent overall:
        /// a second call for an already-compensated activation is a no-op success (checked via the durable
        /// row's own `CompensatedAt` column) and a second removal call for an already-`Removed` effect is
        /// itself a safe, idempotent no-further-op via `RemoveActiveEffect`'s own `CommandId`-keyed replay.
        /// If ANY effect removal fails, this method returns that failure immediately WITHOUT attempting
        /// resource reversal or setting `CompensatedAt` -- retryable later, since every removal attempted so
        /// far is itself already idempotent.
        /// </summary>
        Result<AbilityActivationRecord> CompensateAbilityActivation(CampaignHandle campaign, CommandId originalCommandId, IReadOnlyList<ActiveEffectId> createdEffectIds, UserId actorUserId, CorrelationId correlationId);
    }
}
