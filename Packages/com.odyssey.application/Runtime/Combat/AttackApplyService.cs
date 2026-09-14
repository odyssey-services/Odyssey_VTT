using System;
using System.Linq;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Identity;
using Odyssey.Rules.Combat;

namespace Odyssey.Application.Combat
{
    /// <summary>
    /// ODY-S05-604: orchestrates ADR-029 section 6 stages 12-13 (intervention,
    /// atomic apply) on top of the unmodified stage 1-11 evaluation
    /// (<see cref="AttackEvaluationService.EvaluateAttack"/>, ODY-S05-603). Never
    /// invokes another handler from within a handler (ADR-029 section 9):
    /// <see cref="ResolveAttack"/> and <see cref="ResolveAttackIntervention"/>
    /// are the two independent root-command entry points, each calling only
    /// <see cref="IAttackApplyRepository"/> and (for <see cref="ResolveAttack"/>
    /// only, on the non-replay path) the unmodified stage 1-11 evaluator.
    /// </summary>
    public static class AttackApplyService
    {
        /// <summary>
        /// <c>ResolveAttack</c> (ADR-029 section 5): the authoritative root
        /// command. Checks for an already-durable outcome under
        /// <paramref name="request"/>'s own <c>CommandId</c> FIRST -- before
        /// any RNG derivation -- so a retry never re-queries the authoritative
        /// random stream (ADR-008 rule 14). Only when no durable outcome
        /// exists does it revalidate/roll through the unmodified stage 1-11
        /// evaluator and then hand the freshly-derived, not-yet-persisted
        /// <see cref="AttackRandomSample"/> to <see cref="IAttackApplyRepository.RecordAttackOutcome"/>
        /// for the one atomic-apply transaction.
        /// </summary>
        public static Result<AttackOutcomeRecord> ResolveAttack(IAttackStateReader reader, IAttackRulesEvaluator rules, IAuthoritativeRandomStreamFactory random, IAttackApplyRepository apply, CampaignHandle campaign, RngKeyEpochId keyEpochId, AttackRequest request)
        {
            if (apply == null) throw new ArgumentNullException(nameof(apply));
            if (request == null) throw new ArgumentNullException(nameof(request));

            Result<AttackOutcomeRecord> existing = apply.GetOutcome(campaign, request.CommandId, request.CorrelationId);
            if (existing.IsSuccess)
            {
                return existing;
            }

            Result<ProposedAttackResolution> evaluated = AttackEvaluationService.EvaluateAttack(reader, rules, random, campaign, keyEpochId, request);
            if (evaluated.IsFailure)
            {
                // Guard/authorization/revision failed inside the unmodified
                // stage 1-11 evaluator, before any RNG draw was retained and
                // before IAttackApplyRepository is ever called -- no partial
                // commit is possible because no write was attempted.
                return Result<AttackOutcomeRecord>.Failure(evaluated.Error);
            }

            bool interventionRequired = evaluated.Value.EffectCandidates.Any(candidate => candidate.Decision == EffectApplicationDecision.RequiresIntervention);
            return apply.RecordAttackOutcome(campaign, request.Intent, evaluated.Value.RandomSample!.Value, interventionRequired, evaluated.Value.EffectCandidates, request.ActorUserId, request.CommandId, request.CorrelationId);
        }

        /// <summary>
        /// <c>ResolveAttackIntervention</c> (ADR-029 section 1 rule 6): a new
        /// root command, not a nested call into <see cref="ResolveAttack"/>'s
        /// own handler. Delegates directly to <see cref="IAttackApplyRepository.ResolveAttackIntervention"/>,
        /// which re-reads the pending row and reuses its already-derived
        /// random sample verbatim -- this method never touches
        /// <see cref="IAuthoritativeRandomStreamFactory"/> at all.
        /// </summary>
        public static Result<AttackOutcomeRecord> ResolveAttackIntervention(IAttackApplyRepository apply, CampaignHandle campaign, CommandId pendingCommandId, AttackInterventionResolution resolution, UserId actorUserId, bool actorIsMainGm, CommandId commandId, CorrelationId correlationId)
        {
            if (apply == null) throw new ArgumentNullException(nameof(apply));
            return apply.ResolveAttackIntervention(campaign, pendingCommandId, resolution, actorUserId, actorIsMainGm, commandId, correlationId);
        }
    }
}
