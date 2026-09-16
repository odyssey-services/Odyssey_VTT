using System;
using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Content;
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
        public ActivateAbilityIntent(CharacterId actorId, CharacterAbilityId characterAbilityId, IReadOnlyList<CharacterId> targetIds)
        {
            if (!actorId.IsValid) throw new ArgumentException("ActorId is required.", nameof(actorId));
            if (!characterAbilityId.IsValid) throw new ArgumentException("CharacterAbilityId is required.", nameof(characterAbilityId));
            if (targetIds == null || targetIds.Count == 0) throw new ArgumentException("At least one target is required.", nameof(targetIds));
            CharacterId[] copy = new CharacterId[targetIds.Count];
            for (int index = 0; index < targetIds.Count; index++)
            {
                if (!targetIds[index].IsValid) throw new ArgumentException("Target identity is required.", nameof(targetIds));
                copy[index] = targetIds[index];
            }

            ActorId = actorId;
            CharacterAbilityId = characterAbilityId;
            TargetIds = Array.AsReadOnly(copy);
        }

        public CharacterId ActorId { get; }
        public CharacterAbilityId CharacterAbilityId { get; }
        public IReadOnlyList<CharacterId> TargetIds { get; }
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
        public AbilityActivationRecord(CommandId commandId, CampaignId campaignId, CharacterId actorId, CharacterAbilityId characterAbilityId, IReadOnlyList<CharacterId> targetIds, IReadOnlyList<AttackDelta> resourceDeltas, IReadOnlyList<ContentDefinitionRef> appliedEffectRefs, UtcInstant occurredAt)
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
        }

        public CommandId CommandId { get; }
        public CampaignId CampaignId { get; }
        public CharacterId ActorId { get; }
        public CharacterAbilityId CharacterAbilityId { get; }
        public IReadOnlyList<CharacterId> TargetIds { get; }
        public IReadOnlyList<AttackDelta> ResourceDeltas { get; }
        public IReadOnlyList<ContentDefinitionRef> AppliedEffectRefs { get; }
        public UtcInstant OccurredAt { get; }

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
        /// modified). Only once every delta succeeds does this method record the durable
        /// `AbilityActivationRecord` and return success -- `effectsToApply` travels through unapplied; the
        /// caller (`ActivateAbilityService`) applies each one afterward via the existing, unmodified
        /// `IActiveEffectRepository.CreateActiveEffect` (a separate repository with its own separate
        /// transaction lifecycle -- see this method's own task contract §18 for the disclosed limit on
        /// cross-repository atomicity this implies).
        /// </summary>
        Result<AbilityActivationRecord> RecordAbilityActivation(CampaignHandle campaign, CharacterId actorId, CharacterAbilityId characterAbilityId, IReadOnlyList<CharacterId> targetIds, IReadOnlyList<AttackDelta> resourceDeltas, IReadOnlyList<ContentDefinitionRef> effectsToApply, CommandId commandId, CorrelationId correlationId);
    }
}
