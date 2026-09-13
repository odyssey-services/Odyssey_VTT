using System;
using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Combat
{
    public sealed class CreateCombatEncounterRequest
    {
        public CreateCombatEncounterRequest(IReadOnlyList<CharacterId> participantOrder, UserId actorUserId, bool actorIsMainGm, CommandId commandId)
        {
            ParticipantOrder = participantOrder ?? throw new ArgumentNullException(nameof(participantOrder));
            if (!actorUserId.IsValid || !commandId.IsValid) throw new ArgumentException("Actor and command identity are required.");
            ActorUserId = actorUserId; ActorIsMainGm = actorIsMainGm; CommandId = commandId;
        }
        public IReadOnlyList<CharacterId> ParticipantOrder { get; }
        public UserId ActorUserId { get; }
        public bool ActorIsMainGm { get; }
        public CommandId CommandId { get; }
    }

    public sealed class AdvanceCombatEncounterRequest
    {
        public AdvanceCombatEncounterRequest(CombatEncounterId encounterId, long expectedRevision, UserId actorUserId, bool actorIsMainGm, CommandId commandId)
        {
            if (!encounterId.IsValid || expectedRevision < 1 || !actorUserId.IsValid || !commandId.IsValid) throw new ArgumentException("Valid encounter, revision, actor and command are required.");
            EncounterId = encounterId; ExpectedRevision = expectedRevision; ActorUserId = actorUserId; ActorIsMainGm = actorIsMainGm; CommandId = commandId;
        }
        public CombatEncounterId EncounterId { get; }
        public long ExpectedRevision { get; }
        public UserId ActorUserId { get; }
        public bool ActorIsMainGm { get; }
        public CommandId CommandId { get; }
    }

    public sealed class CombatEncounterRecord
    {
        public CombatEncounterRecord(CombatEncounterId encounterId, CampaignId campaignId, string rulesetId, string rulesetVersion, IReadOnlyList<CombatParticipant> participants, long revision, long roundOrdinal, long turnOrdinal, CombatEncounterStatus status, CombatPhase phase, CharacterId? currentParticipantId, UtcInstant createdAt, UtcInstant updatedAt)
        {
            EncounterId = encounterId; CampaignId = campaignId; RulesetId = rulesetId; RulesetVersion = rulesetVersion; Participants = participants; Revision = revision; RoundOrdinal = roundOrdinal; TurnOrdinal = turnOrdinal; Status = status; Phase = phase; CurrentParticipantId = currentParticipantId; CreatedAt = createdAt; UpdatedAt = updatedAt;
        }
        public CombatEncounterId EncounterId { get; }
        public CampaignId CampaignId { get; }
        public string RulesetId { get; }
        public string RulesetVersion { get; }
        public IReadOnlyList<CombatParticipant> Participants { get; }
        public long Revision { get; }
        public long RoundOrdinal { get; }
        public long TurnOrdinal { get; }
        public CombatEncounterStatus Status { get; }
        public CombatPhase Phase { get; }
        public CharacterId? CurrentParticipantId { get; }
        public UtcInstant CreatedAt { get; }
        public UtcInstant UpdatedAt { get; }
    }

    /// <summary>Persistence-ready data: authorization remains in <see cref="CombatEncounterService"/>.</summary>
    public sealed class CreateCombatEncounterCommand
    {
        public CreateCombatEncounterCommand(IReadOnlyList<CharacterId> participantOrder, CommandId commandId)
        {
            ParticipantOrder = participantOrder ?? throw new ArgumentNullException(nameof(participantOrder));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            CommandId = commandId;
        }
        public IReadOnlyList<CharacterId> ParticipantOrder { get; }
        public CommandId CommandId { get; }
    }

    public sealed class AdvanceCombatEncounterCommand
    {
        public AdvanceCombatEncounterCommand(CombatEncounterId encounterId, long expectedRevision, CommandId commandId)
        {
            if (!encounterId.IsValid || expectedRevision < 1 || !commandId.IsValid) throw new ArgumentException("Valid encounter, revision and command are required.");
            EncounterId = encounterId; ExpectedRevision = expectedRevision; CommandId = commandId;
        }
        public CombatEncounterId EncounterId { get; }
        public long ExpectedRevision { get; }
        public CommandId CommandId { get; }
    }

    public interface ICombatEncounterRepository
    {
        Result<CombatEncounterRecord> Create(CampaignHandle campaign, CreateCombatEncounterCommand command, CorrelationId correlationId);
        Result<CombatEncounterRecord> Advance(CampaignHandle campaign, AdvanceCombatEncounterCommand command, CorrelationId correlationId);
        Result<CombatEncounterRecord> Get(CampaignHandle campaign, CombatEncounterId encounterId, CorrelationId correlationId);
    }

    public static class CombatEncounterService
    {
        public static Result<CombatEncounterRecord> Create(ICombatEncounterRepository repository, CampaignHandle campaign, CreateCombatEncounterRequest request, CorrelationId correlationId)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (campaign == null || request == null) throw new ArgumentNullException(campaign == null ? nameof(campaign) : nameof(request));
            if (!request.ActorIsMainGm) return Result<CombatEncounterRecord>.Failure(Denied(correlationId));
            return repository.Create(campaign, new CreateCombatEncounterCommand(request.ParticipantOrder, request.CommandId), correlationId);
        }

        public static Result<CombatEncounterRecord> Advance(ICombatEncounterRepository repository, CampaignHandle campaign, AdvanceCombatEncounterRequest request, CorrelationId correlationId)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (campaign == null || request == null) throw new ArgumentNullException(campaign == null ? nameof(campaign) : nameof(request));
            if (!request.ActorIsMainGm) return Result<CombatEncounterRecord>.Failure(Denied(correlationId));
            return repository.Advance(campaign, new AdvanceCombatEncounterCommand(request.EncounterId, request.ExpectedRevision, request.CommandId), correlationId);
        }

        private static Error Denied(CorrelationId correlationId) => Error.Create(ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Authorization, SafeReasonCode.PermissionDenied, UserMessageKey.Parse("errors.combat.denied"), RetryDirective.DoNotRetry, correlationId);
    }
}
