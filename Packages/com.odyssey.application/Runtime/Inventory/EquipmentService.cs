using System;
using System.Collections.Generic;
using System.Linq;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Inventory
{
    /// <summary>
    /// ODY-S05-303: MainGM-only Equip transition. Reads across
    /// <see cref="IInventoryRepository"/> and <see cref="ICharacterRepository"/>
    /// directly -- both are Application-layer collaborators of one orchestrator,
    /// the same shape <c>InventoryCreationService</c> already uses for
    /// <c>IContentCatalogRepository</c> alongside <see cref="IInventoryRepository"/>.
    /// No Unequip, weapon/armor mechanical effect, or `RemoveBodyPart` dependency
    /// check is performed here -- `ODY-S05-304`/`305`'s own jobs.
    /// </summary>
    public static class EquipmentService
    {
        public static Result<EquippedEntryRecord> Equip(IInventoryRepository inventoryRepository, ICharacterRepository characterRepository, EquipRequest request)
        {
            if (inventoryRepository == null) throw new ArgumentNullException(nameof(inventoryRepository));
            if (characterRepository == null) throw new ArgumentNullException(nameof(characterRepository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (!request.ActorIsMainGm)
            {
                return Result<EquippedEntryRecord>.Failure(InventoryMovementFailures.Denied(request.CorrelationId));
            }

            Result<InventoryRecord> inventoryResult = inventoryRepository.GetInventory(request.Campaign, request.InventoryId, request.CorrelationId);
            if (inventoryResult.IsFailure)
            {
                return Result<EquippedEntryRecord>.Failure(inventoryResult.Error);
            }

            InventoryRecord inventory = inventoryResult.Value;

            if (request.BodyPartRefs.Count > 0)
            {
                if (inventory.OwnerRef.Kind != InventoryOwnerKind.Character)
                {
                    return Result<EquippedEntryRecord>.Failure(EquipmentFailures.BodyPartRefsRequireCharacterOwner(request.CorrelationId));
                }

                CharacterId characterId = CharacterId.Parse(inventory.OwnerRef.TargetRef);
                Result<CharacterRecord> characterResult = characterRepository.GetCharacter(request.Campaign, characterId, request.CorrelationId);
                if (characterResult.IsFailure)
                {
                    return Result<EquippedEntryRecord>.Failure(characterResult.Error);
                }

                CharacterAnatomy? anatomy = characterResult.Value.Anatomy;
                if (anatomy == null)
                {
                    return Result<EquippedEntryRecord>.Failure(PersistenceFailures.CharacterAnatomyNotInitialized(request.CorrelationId));
                }

                foreach (BodyPartId bodyPartId in request.BodyPartRefs)
                {
                    if (!anatomy.BodyParts.Any(bodyPart => bodyPart.BodyPartId.Equals(bodyPartId)))
                    {
                        return Result<EquippedEntryRecord>.Failure(EquipmentFailures.BodyPartNotFound(request.CorrelationId));
                    }
                }
            }

            var entry = new EquippedEntry(request.InventoryId, request.ItemRef, request.EquipmentSlotRef, request.BodyPartRefs, request.EquippedByUserId, request.EquippedAt, 1);
            var record = new EquippedEntryRecord(inventory.CampaignId, entry);
            var transition = new EquipTransition(record, request.ExpectedTargetRevision, request.CommandId);
            return inventoryRepository.EquipItem(request.Campaign, transition, request.CorrelationId);
        }

        /// <summary>
        /// ODY-S05-304: the symmetric reverse of <see cref="Equip"/>. Does not
        /// take an <see cref="ICharacterRepository"/> -- rule 4 (a body part
        /// must currently exist) only matters when something is newly attached
        /// to it; removing an already-equipped item does not re-validate
        /// anatomy. No new Equip semantics beyond the reverse transition; no
        /// `RemoveBodyPart` check.
        /// </summary>
        public static Result<bool> Unequip(IInventoryRepository inventoryRepository, UnequipRequest request)
        {
            if (inventoryRepository == null) throw new ArgumentNullException(nameof(inventoryRepository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (!request.ActorIsMainGm)
            {
                return Result<bool>.Failure(InventoryMovementFailures.Denied(request.CorrelationId));
            }

            var transition = new UnequipTransition(request.ItemRef, request.InventoryId, request.ExpectedTargetRevision, request.ExpectedEquippedEntryRevision, request.DestinationContainerKey, request.CommandId);
            return inventoryRepository.UnequipItem(request.Campaign, transition, request.CorrelationId);
        }
    }

    /// <summary>
    /// ODY-S05-303: wraps an already-built, already-validated
    /// <see cref="EquippedEntryRecord"/> (the Domain <see cref="EquippedEntry"/>
    /// constructor validates its fields) plus the item's own expected revision
    /// and the command's idempotency key -- symmetric with
    /// <see cref="IInventoryRepository.CreateEquippedEntry"/>'s own
    /// <c>(campaign, record, commandId, correlationId)</c> shape.
    /// </summary>
    public sealed class EquipTransition
    {
        public EquipTransition(EquippedEntryRecord record, long expectedTargetRevision, CommandId commandId)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (expectedTargetRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedTargetRevision));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            Record = record;
            ExpectedTargetRevision = expectedTargetRevision;
            CommandId = commandId;
        }

        public EquippedEntryRecord Record { get; }
        public long ExpectedTargetRevision { get; }
        public CommandId CommandId { get; }
    }

    /// <summary>
    /// ODY-S05-304: the symmetric reverse of <see cref="EquipTransition"/>.
    /// <see cref="InventoryId"/> is the item's own current Inventory (Unequip
    /// never moves an item to a different Inventory); the repository verifies
    /// it against the <see cref="EquippedEntryRecord"/>'s own stored
    /// `InventoryId`. <see cref="DestinationContainerKey"/> is validated
    /// eagerly here via <see cref="InventoryLocationRef.Contained"/> the same
    /// way <c>InventoryMove</c> validates its own destination container key.
    /// </summary>
    public sealed class UnequipTransition
    {
        public UnequipTransition(InventoryItemRef itemRef, InventoryId inventoryId, long expectedTargetRevision, long expectedEquippedEntryRevision, string destinationContainerKey, CommandId commandId)
        {
            if (!itemRef.IsValid) throw new ArgumentException("ItemRef is required.", nameof(itemRef));
            if (!inventoryId.IsValid) throw new ArgumentException("InventoryId is required.", nameof(inventoryId));
            if (expectedTargetRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedTargetRevision));
            if (expectedEquippedEntryRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedEquippedEntryRevision));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            _ = InventoryLocationRef.Contained(inventoryId, destinationContainerKey);

            ItemRef = itemRef;
            InventoryId = inventoryId;
            ExpectedTargetRevision = expectedTargetRevision;
            ExpectedEquippedEntryRevision = expectedEquippedEntryRevision;
            DestinationContainerKey = destinationContainerKey;
            CommandId = commandId;
        }

        public InventoryItemRef ItemRef { get; }
        public InventoryId InventoryId { get; }
        public long ExpectedTargetRevision { get; }
        public long ExpectedEquippedEntryRevision { get; }
        public string DestinationContainerKey { get; }
        public CommandId CommandId { get; }
    }

    public sealed class UnequipRequest
    {
        public UnequipRequest(
            CampaignHandle campaign,
            InventoryItemRef itemRef,
            InventoryId inventoryId,
            long expectedTargetRevision,
            long expectedEquippedEntryRevision,
            string destinationContainerKey,
            UserId actorUserId,
            bool actorIsMainGm,
            CommandId commandId,
            CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!actorUserId.IsValid) throw new ArgumentException("Actor is required.", nameof(actorUserId));

            Campaign = campaign;
            ItemRef = itemRef;
            InventoryId = inventoryId;
            ExpectedTargetRevision = expectedTargetRevision;
            ExpectedEquippedEntryRevision = expectedEquippedEntryRevision;
            DestinationContainerKey = destinationContainerKey;
            ActorIsMainGm = actorIsMainGm;
            CommandId = commandId;
            CorrelationId = correlationId;
        }

        public CampaignHandle Campaign { get; }
        public InventoryItemRef ItemRef { get; }
        public InventoryId InventoryId { get; }
        public long ExpectedTargetRevision { get; }
        public long ExpectedEquippedEntryRevision { get; }
        public string DestinationContainerKey { get; }
        public bool ActorIsMainGm { get; }
        public CommandId CommandId { get; }
        public CorrelationId CorrelationId { get; }
    }

    public sealed class EquipRequest
    {
        public EquipRequest(
            CampaignHandle campaign,
            InventoryItemRef itemRef,
            InventoryId inventoryId,
            long expectedTargetRevision,
            string equipmentSlotRef,
            IReadOnlyList<BodyPartId> bodyPartRefs,
            UserId equippedByUserId,
            UtcInstant equippedAt,
            UserId actorUserId,
            bool actorIsMainGm,
            CommandId commandId,
            CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!actorUserId.IsValid) throw new ArgumentException("Actor is required.", nameof(actorUserId));

            Campaign = campaign;
            ItemRef = itemRef;
            InventoryId = inventoryId;
            ExpectedTargetRevision = expectedTargetRevision;
            EquipmentSlotRef = equipmentSlotRef;
            BodyPartRefs = bodyPartRefs ?? throw new ArgumentNullException(nameof(bodyPartRefs));
            EquippedByUserId = equippedByUserId;
            EquippedAt = equippedAt;
            ActorIsMainGm = actorIsMainGm;
            CommandId = commandId;
            CorrelationId = correlationId;
        }

        public CampaignHandle Campaign { get; }
        public InventoryItemRef ItemRef { get; }
        public InventoryId InventoryId { get; }
        public long ExpectedTargetRevision { get; }
        public string EquipmentSlotRef { get; }
        public IReadOnlyList<BodyPartId> BodyPartRefs { get; }
        public UserId EquippedByUserId { get; }
        public UtcInstant EquippedAt { get; }
        public bool ActorIsMainGm { get; }
        public CommandId CommandId { get; }
        public CorrelationId CorrelationId { get; }
    }

    /// <summary>ODY-S05-303: rule-4 failures with no existing error-code equivalent.</summary>
    public static class EquipmentFailures
    {
        /// <summary>A referenced `BodyPartId` does not exist on the owning Character's current, initialized `Anatomy`.</summary>
        public static Error BodyPartNotFound(CorrelationId correlationId) => Error.Create(
            ErrorCodes.InventoryEquipBodyPartNotFound,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.inventory.equip_body_part_not_found"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>Non-empty `BodyPartRefs` were supplied for an item equipping into a non-Character-owned Inventory, which rule 4 cannot evaluate.</summary>
        public static Error BodyPartRefsRequireCharacterOwner(CorrelationId correlationId) => Error.Create(
            ErrorCodes.InventoryEquipBodyPartRefsRequireCharacterOwner,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.inventory.equip_body_part_refs_require_character_owner"),
            RetryDirective.DoNotRetry,
            correlationId);
    }
}
