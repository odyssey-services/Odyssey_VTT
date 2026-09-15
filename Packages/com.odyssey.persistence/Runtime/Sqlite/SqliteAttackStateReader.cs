using System;
using System.Collections.Generic;
using Odyssey.Application.Combat;
using Odyssey.Application.Content;
using Odyssey.Application.Inventory;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Content;
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
            // ODY-S06-103: the action weapon must be currently equipped, not merely owned -- a formally
            // owned but Contained (not Equipped) weapon must no longer be usable as an attack's action item.
            Result<EquippedEntryRecord> equippedWeapon = _inventory.GetEquippedEntry(campaign, InventoryItemRef.ForInstance(intent.ActionItemInstanceId), correlationId);
            if (equippedWeapon.IsFailure || !equippedWeapon.Value.Entry.InventoryId.Equals(item.Value.InventoryId)) return Result<AttackEvaluationState>.Failure(Rejected(correlationId));
            Result<CharacterRecord> actor = _characters.GetCharacter(campaign, intent.ActorId, correlationId);
            if (actor.IsFailure || actor.Value.CampaignId != campaign.CampaignId) return Result<AttackEvaluationState>.Failure(actor.IsFailure ? actor.Error : Rejected(correlationId));
            var targets = new List<AttackParticipantState>();
            for (int index = 0; index < intent.TargetIds.Count; index++) { Result<CharacterRecord> target = _characters.GetCharacter(campaign, intent.TargetIds[index], correlationId); if (target.IsFailure || target.Value.CampaignId != campaign.CampaignId) return Result<AttackEvaluationState>.Failure(target.IsFailure ? target.Error : Rejected(correlationId)); targets.Add(State(target.Value)); }
            AttackParticipantState actorState = State(actor.Value);
            var topology = new AttackUnavailableInput(AttackInputAvailability.UnavailableNotBound, "Encounter has no tactical topology binding.");
            Result<AttackArmorInput> armorAndEffects = ReadArmor(campaign, intent.TargetIds, correlationId);
            if (armorAndEffects.IsFailure) return Result<AttackEvaluationState>.Failure(armorAndEffects.Error);
            var snapshot = new AttackEvaluationSnapshot(Fingerprint(encounter.Value, item.Value, actorState, targets), encounter.Value.RulesetId, encounter.Value.RulesetVersion, encounter.Value.Revision, item.Value.SourceItemDefinitionRef, item.Value.MechanicsSnapshot, actorState, targets, topology, armorAndEffects.Value);
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
        // ODY-S06-102: AttackParticipantState.AttributeValues -- record.Attributes is already loaded by
        // GetCharacter above; this copies its EffectiveValue readings into the snapshot without a second
        // database read, so ADR-030 section 6.2's attributeReference formula term can resolve against them.
        private static AttackParticipantState State(CharacterRecord record)
        {
            var attributeValues = new Dictionary<AttributeDefinitionId, long>(record.Attributes.Count);
            foreach (AttributeValue attribute in record.Attributes) attributeValues[attribute.AttributeDefinitionId] = attribute.EffectiveValue;
            return new AttackParticipantState(record.CharacterId, record.LifecycleStatus, record.ApprovalState, attributeValues);
        }
        private static string Fingerprint(CombatEncounterRecord encounter, ItemInstanceRecord item, AttackParticipantState actor, IReadOnlyList<AttackParticipantState> targets) { string value = encounter.EncounterId + ":" + encounter.Revision + ":" + item.ItemInstanceId + ":" + item.Revision + ":" + item.MechanicsSnapshot.SourceDefinitionRef + ":" + item.MechanicsSnapshot.DefinitionSnapshotVersion + ":" + actor.LifecycleStatus + ":" + actor.ApprovalState; for (int index = 0; index < targets.Count; index++) value += ":" + targets[index].CharacterId + ":" + targets[index].LifecycleStatus + ":" + targets[index].ApprovalState; return value; }

        // ODY-S06-103: aggregates every target's currently-equipped armor across the whole intent, tagged
        // by TargetId. Which piece (if any) a real hit actually consults is ODY-S06-105's own aggregation/
        // selection decision -- this only makes the real, decoded ArmorDefinition data reachable.
        private Result<AttackArmorInput> ReadArmor(CampaignHandle campaign, IReadOnlyList<CharacterId> targetIds, CorrelationId correlationId)
        {
            var entries = new List<AttackTargetArmorEntry>();
            for (int targetIndex = 0; targetIndex < targetIds.Count; targetIndex++)
            {
                CharacterId targetId = targetIds[targetIndex];
                Result<IReadOnlyList<EquippedEntryRecord>> equipped = _inventory.ListEquippedEntriesByCharacter(campaign, campaign.CampaignId, targetId, correlationId);
                if (equipped.IsFailure) return Result<AttackArmorInput>.Failure(equipped.Error);
                for (int entryIndex = 0; entryIndex < equipped.Value.Count; entryIndex++)
                {
                    Result<ItemMechanicsSnapshot> mechanics = ReadMechanicsSnapshot(campaign, equipped.Value[entryIndex].Entry.ItemRef, correlationId);
                    if (mechanics.IsFailure) return Result<AttackArmorInput>.Failure(mechanics.Error);
                    if (mechanics.Value.ContentType != ContentDefinitionType.Armor) continue;
                    Result<ArmorDefinition> armor = TypedDefinitionCodec.DecodeArmor(mechanics.Value.ContentType, mechanics.Value.Payload, correlationId);
                    if (armor.IsFailure) return Result<AttackArmorInput>.Failure(armor.Error);
                    entries.Add(new AttackTargetArmorEntry(targetId, armor.Value));
                }
            }

            return Result<AttackArmorInput>.Success(entries.Count == 0
                ? AttackArmorInput.Unavailable("No equipped armor found on any target.")
                : AttackArmorInput.Available(entries));
        }

        private Result<ItemMechanicsSnapshot> ReadMechanicsSnapshot(CampaignHandle campaign, InventoryItemRef itemRef, CorrelationId correlationId)
        {
            if (itemRef.Kind == InventoryItemRefKind.ItemInstance)
            {
                Result<ItemInstanceRecord> instance = _inventory.GetItemInstance(campaign, itemRef.ItemInstanceId, correlationId);
                return instance.IsFailure ? Result<ItemMechanicsSnapshot>.Failure(instance.Error) : Result<ItemMechanicsSnapshot>.Success(instance.Value.MechanicsSnapshot);
            }
            Result<ItemStackRecord> stack = _inventory.GetItemStack(campaign, itemRef.ItemStackId, correlationId);
            return stack.IsFailure ? Result<ItemMechanicsSnapshot>.Failure(stack.Error) : Result<ItemMechanicsSnapshot>.Success(stack.Value.MechanicsSnapshot);
        }
        private static Error Rejected(CorrelationId correlationId) => Error.Create(ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Precondition, SafeReasonCode.ActionNotAllowed, UserMessageKey.Parse("errors.attack.invalid_state"), RetryDirective.DoNotRetry, correlationId);
    }
}
