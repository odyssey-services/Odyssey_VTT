using System;
using Odyssey.Application.Commands;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Application.Persistence;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Identity;
using Odyssey.Rules.Combat;
using Odyssey.Rules.Versions;

namespace Odyssey.Application.Combat
{
    /// <summary>Read-only authoritative state seam for ODY-S05-603; implementations must not write.</summary>
    public interface IAttackStateReader
    {
        Result<AttackEvaluationState> Read(CampaignHandle campaign, AttackIntent intent, CorrelationId correlationId);
        Result<bool> CanControlActor(CampaignHandle campaign, CharacterId actorId, UserId userId, CorrelationId correlationId);
    }

    public sealed class AttackEvaluationState
    {
        public AttackEvaluationState(CombatEncounterRecord encounter, AttackEvaluationSnapshot snapshot)
        {
            Encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public CombatEncounterRecord Encounter { get; }
        public AttackEvaluationSnapshot Snapshot { get; }
    }

    public sealed class AttackRequest
    {
        public AttackRequest(AttackIntent intent, UserId actorUserId, bool actorIsMainGm, CommandId commandId, CorrelationId correlationId)
        {
            Intent = intent ?? throw new ArgumentNullException(nameof(intent));
            if (!actorUserId.IsValid || !commandId.IsValid || !correlationId.IsValid) throw new ArgumentException("Actor, command and correlation identities are required.");
            ActorUserId = actorUserId; ActorIsMainGm = actorIsMainGm; CommandId = commandId; CorrelationId = correlationId;
        }
        public AttackIntent Intent { get; }
        public UserId ActorUserId { get; }
        public bool ActorIsMainGm { get; }
        public CommandId CommandId { get; }
        public CorrelationId CorrelationId { get; }
    }

    public static class AttackEvaluationService
    {
        private static readonly RngPurpose CombatRollPurpose = RngPurpose.Parse("combat.attack.roll");

        public static Result<ProposedAttackResolution> PreviewAttack(IAttackStateReader reader, IAttackRulesEvaluator rules, CampaignHandle campaign, AttackRequest request)
        {
            Result<AttackEvaluationState> state = AuthorizeAndRead(reader, campaign, request);
            return state.IsFailure ? Result<ProposedAttackResolution>.Failure(state.Error) : Result<ProposedAttackResolution>.Success(rules.Preview(request.Intent, state.Value.Snapshot));
        }

        public static Result<ProposedAttackResolution> EvaluateAttack(IAttackStateReader reader, IAttackRulesEvaluator rules, IAuthoritativeRandomStreamFactory random, CampaignHandle campaign, RngKeyEpochId keyEpochId, AttackRequest request)
        {
            if (rules == null || random == null) throw new ArgumentNullException(rules == null ? nameof(rules) : nameof(random));
            Result<AttackEvaluationState> state = AuthorizeAndRead(reader, campaign, request);
            if (state.IsFailure) return Result<ProposedAttackResolution>.Failure(state.Error);
            RulesetVersion version = RulesetVersion.Parse(state.Value.Snapshot.RulesetVersion);
            RandomDecisionContext context = RandomDecisionContext.Create(campaign.CampaignId, request.CommandId, 0, CombatRollPurpose, version, keyEpochId, request.CorrelationId);
            Result<IAuthoritativeRandomStream> stream = random.Create(context);
            if (stream.IsFailure) return Result<ProposedAttackResolution>.Failure(stream.Error);
            Result<RandomSample> sample = stream.Value.NextInclusive(1, 100, 0);
            if (sample.IsFailure) return Result<ProposedAttackResolution>.Failure(sample.Error);
            return Result<ProposedAttackResolution>.Success(rules.Evaluate(request.Intent, state.Value.Snapshot, new AttackRandomSample(sample.Value.Value)));
        }

        private static Result<AttackEvaluationState> AuthorizeAndRead(IAttackStateReader reader, CampaignHandle campaign, AttackRequest request)
        {
            if (reader == null || campaign == null || request == null) throw new ArgumentNullException(reader == null ? nameof(reader) : campaign == null ? nameof(campaign) : nameof(request));
            if (!request.ActorIsMainGm)
            {
                Result<bool> control = reader.CanControlActor(campaign, request.Intent.ActorId, request.ActorUserId, request.CorrelationId);
                if (control.IsFailure) return Result<AttackEvaluationState>.Failure(control.Error);
                if (!control.Value) return Result<AttackEvaluationState>.Failure(Denied(request.CorrelationId));
            }
            Result<AttackEvaluationState> state = reader.Read(campaign, request.Intent, request.CorrelationId);
            if (state.IsFailure) return state;
            CombatEncounterRecord encounter = state.Value.Encounter;
            if (encounter.Status != CombatEncounterStatus.Open || encounter.Phase != CombatPhase.TurnOpen || !encounter.CurrentParticipantId.HasValue || encounter.CurrentParticipantId.Value != request.Intent.ActorId)
                return Result<AttackEvaluationState>.Failure(ClosedOrInactive(request.CorrelationId));
            return state;
        }

        private static Error Denied(CorrelationId id) => Error.Create(ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Authorization, SafeReasonCode.PermissionDenied, UserMessageKey.Parse("errors.attack.denied"), RetryDirective.DoNotRetry, id);
        private static Error ClosedOrInactive(CorrelationId id) => Error.Create(ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Precondition, SafeReasonCode.ActionNotAllowed, UserMessageKey.Parse("errors.attack.not_current_turn"), RetryDirective.DoNotRetry, id);
    }
}
