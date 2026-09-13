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

    public interface ICombatEncounterRepository
    {
        Result<CombatEncounterRecord> Create(CampaignHandle campaign, CreateCombatEncounterRequest request, CorrelationId correlationId);
        Result<CombatEncounterRecord> Advance(CampaignHandle campaign, AdvanceCombatEncounterRequest request, CorrelationId correlationId);
        Result<CombatEncounterRecord> Get(CampaignHandle campaign, CombatEncounterId encounterId, CorrelationId correlationId);
    }
}
