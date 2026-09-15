using System;
using Odyssey.Application.Commands;
using Odyssey.Application.Content;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Application.Persistence;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Content;
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

        // ODY-S06-105 section 3.4: a practical MVP cap, not DiceFormulaParser's own MaxDiceCount=100/
        // MaxDiceGroups=20 parser limit -- wide enough for every formula this slice's own content actually
        // uses (at most a couple of dice terms), independent of any single weapon's own real term count,
        // since this draws BEFORE the weapon's formula is known to have been decoded successfully.
        private const int MaxRandomDraws = 4;

        public static Result<ProposedAttackResolution> PreviewAttack(IAttackStateReader reader, IAttackRulesEvaluator rules, CampaignHandle campaign, AttackRequest request)
        {
            Result<AttackEvaluationState> state = AuthorizeAndRead(reader, campaign, request);
            return state.IsFailure ? Result<ProposedAttackResolution>.Failure(state.Error) : Result<ProposedAttackResolution>.Success(rules.Preview(request.Intent, WithDecodedWeapon(state.Value.Snapshot, request.CorrelationId)));
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
            int[] values = new int[MaxRandomDraws];
            for (int drawIndex = 0; drawIndex < MaxRandomDraws; drawIndex++)
            {
                // ODY-S06-105: a wide 1..100 range for every draw -- the evaluator cannot narrow this to a
                // specific die's own side count up front, since the weapon's formula is not decoded until
                // after these draws are already made; CoreAttackRulesEvaluator maps each raw value onto the
                // die it actually needs once the formula is known.
                Result<RandomSample> sample = stream.Value.NextInclusive(1, 100, drawIndex);
                if (sample.IsFailure) return Result<ProposedAttackResolution>.Failure(sample.Error);
                values[drawIndex] = sample.Value.Value;
            }

            return Result<ProposedAttackResolution>.Success(rules.Evaluate(request.Intent, WithDecodedWeapon(state.Value.Snapshot, request.CorrelationId), new AttackRandomSample(Array.AsReadOnly(values))));
        }

        /// <summary>
        /// ODY-S06-105: decodes `ActionMechanics` into a `WeaponDefinition` and returns an augmented copy of
        /// <paramref name="snapshot"/> carrying it as `ActionWeapon` -- `Odyssey.Rules` (where the real
        /// `IAttackRulesEvaluator`, `CoreAttackRulesEvaluator`, lives) cannot reference
        /// `Odyssey.Application.Content.TypedDefinitionCodec` (ADR-001 section 6.2's own dependency matrix;
        /// `Odyssey.Rules` references only `Odyssey.Domain`, confirmed by direct `.csproj`/`.asmdef` read),
        /// so this decode step must happen here, in the one Application-layer seam every real caller of
        /// `IAttackRulesEvaluator` already passes through. A decode failure (a non-Weapon action item, e.g.
        /// every pre-`ODY-S06-105` test fixture's own generic placeholder payload) is not an error here --
        /// it leaves `ActionWeapon` null and returns the original, unaugmented snapshot unchanged, exactly
        /// as `SqliteAttackStateReader.Read` (forbidden to modify by this task) already produced it; only a
        /// real `CoreAttackRulesEvaluator` call against a real Weapon-shaped action item ever looks at
        /// `ActionWeapon` at all.
        /// </summary>
        private static AttackEvaluationSnapshot WithDecodedWeapon(AttackEvaluationSnapshot snapshot, CorrelationId correlationId)
        {
            Result<WeaponDefinition> weapon = TypedDefinitionCodec.DecodeWeapon(snapshot.ActionMechanics.ContentType, snapshot.ActionMechanics.Payload, correlationId);
            if (weapon.IsFailure) return snapshot;
            return new AttackEvaluationSnapshot(snapshot.Fingerprint, snapshot.RulesetId, snapshot.RulesetVersion, snapshot.EncounterRevision, snapshot.ActionSourceRef, snapshot.ActionMechanics, snapshot.Actor, snapshot.Targets, snapshot.Topology, snapshot.ArmorAndEffects, weapon.Value);
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
            if (encounter.Status != CombatEncounterStatus.Open || encounter.Phase != CombatPhase.TurnOpen || !encounter.CurrentParticipantId.HasValue || encounter.CurrentParticipantId.Value != request.Intent.ActorId || encounter.Revision != request.Intent.ExpectedEncounterRevision || state.Value.Snapshot.EncounterRevision != encounter.Revision || !ContainsAll(encounter.Participants, request.Intent.TargetIds))
                return Result<AttackEvaluationState>.Failure(ClosedOrInactive(request.CorrelationId));
            return state;
        }

        private static bool ContainsAll(System.Collections.Generic.IReadOnlyList<CombatParticipant> participants, System.Collections.Generic.IReadOnlyList<CharacterId> targets)
        {
            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++) { bool found = false; for (int participantIndex = 0; participantIndex < participants.Count; participantIndex++) if (participants[participantIndex].CharacterId == targets[targetIndex]) { found = true; break; } if (!found) return false; }
            return true;
        }

        private static Error Denied(CorrelationId id) => Error.Create(ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Authorization, SafeReasonCode.PermissionDenied, UserMessageKey.Parse("errors.attack.denied"), RetryDirective.DoNotRetry, id);
        private static Error ClosedOrInactive(CorrelationId id) => Error.Create(ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Precondition, SafeReasonCode.ActionNotAllowed, UserMessageKey.Parse("errors.attack.not_current_turn"), RetryDirective.DoNotRetry, id);
    }
}
