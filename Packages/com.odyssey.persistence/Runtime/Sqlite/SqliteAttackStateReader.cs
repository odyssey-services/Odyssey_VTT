using System;
using System.Collections.Generic;
using Odyssey.Application.Combat;
using Odyssey.Application.Inventory;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;

namespace Odyssey.Persistence.Sqlite
{
    /// <summary>Read-only composition of existing SQLite-backed authoritative stores for attack evaluation.</summary>
    public sealed class SqliteAttackStateReader : IAttackStateReader
    {
        private readonly ICombatEncounterRepository _encounters;
        private readonly IInventoryRepository _inventory;
        private readonly ICharacterRepository _characters;
        private readonly IWallClock _clock;

        public SqliteAttackStateReader(ICombatEncounterRepository encounters, IInventoryRepository inventory, ICharacterRepository characters, IWallClock clock)
        {
            _encounters = encounters ?? throw new ArgumentNullException(nameof(encounters));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _characters = characters ?? throw new ArgumentNullException(nameof(characters));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public Result<AttackEvaluationState> Read(CampaignHandle campaign, AttackIntent intent, CorrelationId correlationId)
        {
            Result<CombatEncounterRecord> encounter = _encounters.Get(campaign, intent.EncounterId, correlationId);
            if (encounter.IsFailure) return Result<AttackEvaluationState>.Failure(encounter.Error);
            if (encounter.Value.Status != CombatEncounterStatus.Open || encounter.Value.Phase != CombatPhase.TurnOpen || !encounter.Value.CurrentParticipantId.HasValue || encounter.Value.CurrentParticipantId.Value != intent.ActorId || encounter.Value.Revision != intent.ExpectedEncounterRevision || !Contains(encounter.Value.Participants, intent.ActorId) || !ContainsAll(encounter.Value.Participants, intent.TargetIds)) return Result<AttackEvaluationState>.Failure(Rejected(correlationId));
            Result<ItemInstanceRecord> item = _inventory.GetItemInstance(campaign, intent.ActionItemInstanceId, correlationId);
            if (item.IsFailure) return Result<AttackEvaluationState>.Failure(item.Error);
            if (item.Value.CampaignId != campaign.CampaignId || item.Value.OwnerRef.Kind != InventoryOwnerKind.Character || item.Value.OwnerRef.TargetRef != intent.ActorId.ToString() || !item.Value.MechanicsSnapshot.SourceDefinitionRef.Equals(item.Value.SourceItemDefinitionRef)) return Result<AttackEvaluationState>.Failure(Rejected(correlationId));
            var snapshot = new AttackEvaluationSnapshot(Fingerprint(encounter.Value, item.Value), encounter.Value.RulesetId, encounter.Value.RulesetVersion, encounter.Value.Revision, item.Value.SourceItemDefinitionRef, item.Value.MechanicsSnapshot);
            return Result<AttackEvaluationState>.Success(new AttackEvaluationState(encounter.Value, snapshot));
        }

        public Result<bool> CanControlActor(CampaignHandle campaign, CharacterId actorId, UserId userId, CorrelationId correlationId)
        {
            Result<CharacterRecord> character = _characters.GetCharacter(campaign, actorId, correlationId);
            if (character.IsFailure) return Result<bool>.Failure(character.Error);
            return Result<bool>.Success(character.Value.CampaignId == campaign.CampaignId && CharacterOwnershipAssignment.IsAssignedCharacter(character.Value.Ownership, userId, _clock.GetUtcNow()));
        }

        private static bool Contains(IReadOnlyList<CombatParticipant> participants, CharacterId id) { for (int index = 0; index < participants.Count; index++) if (participants[index].CharacterId == id) return true; return false; }
        private static bool ContainsAll(IReadOnlyList<CombatParticipant> participants, IReadOnlyList<CharacterId> ids) { for (int index = 0; index < ids.Count; index++) if (!Contains(participants, ids[index])) return false; return true; }
        private static string Fingerprint(CombatEncounterRecord encounter, ItemInstanceRecord item) => encounter.EncounterId + ":" + encounter.Revision + ":" + item.ItemInstanceId + ":" + item.Revision + ":" + item.MechanicsSnapshot.SourceDefinitionRef + ":" + item.MechanicsSnapshot.DefinitionSnapshotVersion;
        private static Error Rejected(CorrelationId correlationId) => Error.Create(ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Precondition, SafeReasonCode.ActionNotAllowed, UserMessageKey.Parse("errors.attack.invalid_state"), RetryDirective.DoNotRetry, correlationId);
    }
}
