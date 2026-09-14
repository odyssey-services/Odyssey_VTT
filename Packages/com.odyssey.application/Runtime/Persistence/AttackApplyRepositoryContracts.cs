using System;
using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// ODY-S05-604: the durable record of one <c>ResolveAttack</c> root command's
    /// outcome (ADR-029 section 3.3's "pending resolution", section 6 stages
    /// 12-13). Carries the already-derived <see cref="RandomSampleValue"/> so it
    /// is never re-rolled on retry or intervention resolution (ADR-008 rules
    /// 13-14). Does not carry a concretely-applied damage/cost/effect delta:
    /// no accepted Ruleset formula exists to interpret <c>AttackDelta</c>/
    /// <c>AttackEffectCandidate</c> values into specific Character/Item state
    /// (ADR-029 section 10's own non-goal), so this record captures the
    /// outcome/idempotency and Game Log bookkeeping this task owns, not a
    /// concrete state delta -- see the ODY-S05-604 task contract's decision log.
    /// </summary>
    public sealed class AttackOutcomeRecord
    {
        public AttackOutcomeRecord(
            CommandId resolveAttackCommandId,
            CampaignId campaignId,
            CombatEncounterId encounterId,
            CharacterId actorId,
            IReadOnlyList<CharacterId> targetIds,
            ItemInstanceId actionItemInstanceId,
            long expectedEncounterRevision,
            int randomSampleValue,
            bool interventionRequired,
            IReadOnlyList<AttackEffectCandidate> effectCandidates,
            AttackOutcomeKind outcomeKind,
            string? gameLogEntryId,
            UtcInstant createdAt,
            UtcInstant? resolvedAt,
            CommandId? resolvedByCommandId)
        {
            if (!resolveAttackCommandId.IsValid) throw new ArgumentException("ResolveAttackCommandId is required.", nameof(resolveAttackCommandId));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!encounterId.IsValid) throw new ArgumentException("EncounterId is required.", nameof(encounterId));
            if (!actorId.IsValid) throw new ArgumentException("ActorId is required.", nameof(actorId));
            if (targetIds == null || targetIds.Count == 0) throw new ArgumentException("At least one target is required.", nameof(targetIds));
            if (!actionItemInstanceId.IsValid) throw new ArgumentException("ActionItemInstanceId is required.", nameof(actionItemInstanceId));
            if (expectedEncounterRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedEncounterRevision));
            if (effectCandidates == null) throw new ArgumentNullException(nameof(effectCandidates));

            ResolveAttackCommandId = resolveAttackCommandId;
            CampaignId = campaignId;
            EncounterId = encounterId;
            ActorId = actorId;
            TargetIds = targetIds;
            ActionItemInstanceId = actionItemInstanceId;
            ExpectedEncounterRevision = expectedEncounterRevision;
            RandomSampleValue = randomSampleValue;
            InterventionRequired = interventionRequired;
            EffectCandidates = effectCandidates;
            OutcomeKind = outcomeKind;
            GameLogEntryId = gameLogEntryId;
            CreatedAt = createdAt;
            ResolvedAt = resolvedAt;
            ResolvedByCommandId = resolvedByCommandId;
        }

        public CommandId ResolveAttackCommandId { get; }
        public CampaignId CampaignId { get; }
        public CombatEncounterId EncounterId { get; }
        public CharacterId ActorId { get; }
        public IReadOnlyList<CharacterId> TargetIds { get; }
        public ItemInstanceId ActionItemInstanceId { get; }
        public long ExpectedEncounterRevision { get; }
        public int RandomSampleValue { get; }
        public bool InterventionRequired { get; }

        /// <summary>ODY-S05-606: `ADR-029` §8's own effect candidates, computed by Rules at stage 11 (`ODY-S05-603`, unmodified) and persisted here so `ResolveAttackIntervention` can act on them without re-evaluating Rules or losing them between the pending and resolved steps.</summary>
        public IReadOnlyList<AttackEffectCandidate> EffectCandidates { get; }

        public AttackOutcomeKind OutcomeKind { get; }
        public string? GameLogEntryId { get; }
        public UtcInstant CreatedAt { get; }
        public UtcInstant? ResolvedAt { get; }
        public CommandId? ResolvedByCommandId { get; }
    }

    /// <summary>
    /// ODY-S05-607: the durable, causally-linked correction record `ADR-029`
    /// §1 rule 7's own compensating-command requirement produces -- never a
    /// mutation of the original <see cref="AttackOutcomeRecord"/>/Game Log
    /// row, only a new row referencing it. By direct structural analogy to
    /// `ODY-S04-113`'s own `RevertCharacterRulesetMigration` precedent: this
    /// task corrects only the Game Log/`AttackOutcome` bookkeeping this
    /// block itself wrote (a mis-logged summary, a wrong audience at the
    /// time of writing) -- it never touches Character/Item resource state
    /// (`ODY-S05-609`'s own territory, untouched).
    /// </summary>
    public sealed class AttackCompensationRecord
    {
        public AttackCompensationRecord(
            CommandId compensatingCommandId,
            CommandId originalResolveAttackCommandId,
            string reasonCode,
            string correctedSummaryPayload,
            string gameLogEntryId,
            UtcInstant createdAt)
        {
            if (!compensatingCommandId.IsValid) throw new ArgumentException("CompensatingCommandId is required.", nameof(compensatingCommandId));
            if (!originalResolveAttackCommandId.IsValid) throw new ArgumentException("OriginalResolveAttackCommandId is required.", nameof(originalResolveAttackCommandId));
            if (string.IsNullOrWhiteSpace(reasonCode)) throw new ArgumentException("ReasonCode is required.", nameof(reasonCode));
            if (string.IsNullOrWhiteSpace(correctedSummaryPayload)) throw new ArgumentException("CorrectedSummaryPayload is required.", nameof(correctedSummaryPayload));
            if (string.IsNullOrWhiteSpace(gameLogEntryId)) throw new ArgumentException("GameLogEntryId is required.", nameof(gameLogEntryId));

            CompensatingCommandId = compensatingCommandId;
            OriginalResolveAttackCommandId = originalResolveAttackCommandId;
            ReasonCode = reasonCode;
            CorrectedSummaryPayload = correctedSummaryPayload;
            GameLogEntryId = gameLogEntryId;
            CreatedAt = createdAt;
        }

        public CommandId CompensatingCommandId { get; }
        public CommandId OriginalResolveAttackCommandId { get; }
        public string ReasonCode { get; }
        public string CorrectedSummaryPayload { get; }
        public string GameLogEntryId { get; }
        public UtcInstant CreatedAt { get; }
    }

    /// <summary>
    /// ODY-S05-604: the sole persistence seam for attack pending/intervention/
    /// atomic-apply state. A new, standalone contract -- by direct analogy to
    /// how <c>IActiveEffectRepository</c> (ADR-028/ADR-029 section 8) never
    /// appends methods to Inventory or Character repositories, this never
    /// appends a method to <c>ICombatEncounterRepository</c>, <c>IInventoryRepository</c>,
    /// or <c>ICharacterRepository</c> either.
    /// </summary>
    public interface IAttackApplyRepository
    {
        /// <summary>
        /// Idempotency read: returns the durable outcome already recorded for
        /// <paramref name="resolveAttackCommandId"/>, if any. The Application
        /// orchestrator (<c>AttackApplyService.ResolveAttack</c>) calls this
        /// BEFORE deriving any RNG sample, so a retry never re-queries the
        /// authoritative random stream (ADR-008 rule 14; independent
        /// verification checklist item 1).
        /// </summary>
        Result<AttackOutcomeRecord> GetOutcome(CampaignHandle campaign, CommandId resolveAttackCommandId, CorrelationId correlationId);

        /// <summary>
        /// Atomic apply (ADR-029 section 6 stage 13) for the immediate path:
        /// records a durable <c>Accepted</c> outcome (when
        /// <paramref name="interventionRequired"/> is false) or a durable
        /// <c>Pending</c> outcome (stage 12) carrying the already-derived
        /// <paramref name="randomSample"/>, in one <c>SqliteSavingPipeline</c>
        /// transaction together with the committed Game Log entry (Accepted
        /// only) and the DomainEvent/idempotency row. Idempotent by
        /// <paramref name="commandId"/>: an exact retry returns the original
        /// row and creates no second mutation.
        /// </summary>
        Result<AttackOutcomeRecord> RecordAttackOutcome(CampaignHandle campaign, AttackIntent intent, AttackRandomSample randomSample, bool interventionRequired, IReadOnlyList<AttackEffectCandidate> effectCandidates, UserId actorUserId, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// <c>ResolveAttackIntervention</c> (ADR-029 section 1 rule 6): a new
        /// root command under <c>ADR-002</c>, not a nested handler call. Re-reads
        /// the pending outcome by <paramref name="pendingCommandId"/>, requires
        /// it still be <c>Pending</c> (a CAS guard -- an already-resolved
        /// pending outcome is a typed conflict, not a silent no-op), requires
        /// <paramref name="actorIsMainGm"/> (ADR-029 section 10: MainGM
        /// resolves an ambiguous/contested/expired intervention; a
        /// Ruleset-specific eligible-controller-choice payload remains out of
        /// this task's scope), and atomically transitions it to
        /// <c>Accepted</c> (with a committed Game Log entry), <c>Rejected</c>,
        /// or <c>Cancelled</c> per <paramref name="resolution"/> -- reusing the
        /// pending row's own already-derived RandomSampleValue verbatim, never
        /// deriving a new one. Idempotent by <paramref name="commandId"/> (this
        /// command's own root identity, distinct from <paramref name="pendingCommandId"/>).
        /// </summary>
        Result<AttackOutcomeRecord> ResolveAttackIntervention(CampaignHandle campaign, CommandId pendingCommandId, AttackInterventionResolution resolution, UserId actorUserId, bool actorIsMainGm, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S05-607: `ADR-029` §1 rule 7's own compensating root command --
        /// never a nested handler call, never a rewrite of
        /// <paramref name="resolveAttackCommandId"/>'s own original
        /// `AttackOutcome`/Game Log/`DiceRolls` rows. MainGM-only (checked as
        /// this method's own first statement), requires a non-empty
        /// <paramref name="reasonCode"/>, and only applies to an already
        /// <c>Accepted</c> outcome (only an accepted attack has a committed
        /// Game Log entry to correct). CAS-guarded against compensating the
        /// same original committing event twice (an already-compensated
        /// outcome is a typed conflict, not a silent no-op or an implicit
        /// second correction). Never re-derives RNG -- this is bookkeeping
        /// correction only, never a re-roll. Idempotent by
        /// <paramref name="commandId"/> (this command's own root identity,
        /// distinct from <paramref name="resolveAttackCommandId"/>).
        /// </summary>
        Result<AttackCompensationRecord> CompensateAttackOutcome(CampaignHandle campaign, CommandId resolveAttackCommandId, string reasonCode, string correctedSummaryPayload, UserId actorUserId, bool actorIsMainGm, CommandId commandId, CorrelationId correlationId);
    }
}
