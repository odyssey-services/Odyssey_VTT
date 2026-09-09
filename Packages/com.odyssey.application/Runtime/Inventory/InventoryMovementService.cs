using System;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;

namespace Odyssey.Application.Inventory
{
    public static class InventoryMovementService
    {
        public static Result<ItemInstanceRecord> MoveItemInstance(IInventoryRepository repository, MoveItemInstanceRequest request)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            return request.ActorIsMainGm
                ? repository.MoveItemInstance(request.Campaign, request.Move, request.CorrelationId)
                : Result<ItemInstanceRecord>.Failure(InventoryMovementFailures.Denied(request.CorrelationId));
        }

        public static Result<ItemStackRecord> MoveItemStack(IInventoryRepository repository, MoveItemStackRequest request)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            return request.ActorIsMainGm
                ? repository.MoveItemStack(request.Campaign, request.Move, request.CorrelationId)
                : Result<ItemStackRecord>.Failure(InventoryMovementFailures.Denied(request.CorrelationId));
        }
    }

    public sealed class InventoryMove
    {
        public InventoryMove(InventoryItemRef target, InventoryId sourceInventoryId, long expectedTargetRevision, long expectedSourceRevision, InventoryId destinationInventoryId, long expectedDestinationRevision, string destinationContainerKey, CommandId commandId)
        {
            if (!target.IsValid || !sourceInventoryId.IsValid || !destinationInventoryId.IsValid || expectedTargetRevision < 1 || expectedSourceRevision < 1 || expectedDestinationRevision < 1 || !commandId.IsValid) throw new ArgumentException("A valid move is required.");
            _ = InventoryLocationRef.Contained(destinationInventoryId, destinationContainerKey);
            Target = target; SourceInventoryId = sourceInventoryId; ExpectedTargetRevision = expectedTargetRevision; ExpectedSourceRevision = expectedSourceRevision; DestinationInventoryId = destinationInventoryId; ExpectedDestinationRevision = expectedDestinationRevision; DestinationContainerKey = destinationContainerKey; CommandId = commandId;
        }
        public InventoryItemRef Target { get; }
        public InventoryId SourceInventoryId { get; }
        public long ExpectedTargetRevision { get; }
        public long ExpectedSourceRevision { get; }
        public InventoryId DestinationInventoryId { get; }
        public long ExpectedDestinationRevision { get; }
        public string DestinationContainerKey { get; }
        public CommandId CommandId { get; }
    }

    public sealed class MoveItemInstanceRequest
    {
        public MoveItemInstanceRequest(CampaignHandle campaign, ItemInstanceId targetId, long expectedTargetRevision, InventoryId sourceInventoryId, long expectedSourceRevision, InventoryId destinationInventoryId, long expectedDestinationRevision, string destinationContainerKey, UserId actorUserId, bool actorIsMainGm, CommandId commandId, CorrelationId correlationId)
        { if (campaign == null || !targetId.IsValid || !actorUserId.IsValid) throw new ArgumentException("Campaign, target, and actor are required."); Campaign = campaign; ActorIsMainGm = actorIsMainGm; CorrelationId = correlationId; Move = new InventoryMove(InventoryItemRef.ForInstance(targetId), sourceInventoryId, expectedTargetRevision, expectedSourceRevision, destinationInventoryId, expectedDestinationRevision, destinationContainerKey, commandId); }
        public CampaignHandle Campaign { get; }
        public bool ActorIsMainGm { get; }
        public CorrelationId CorrelationId { get; }
        public InventoryMove Move { get; }
    }

    public sealed class MoveItemStackRequest
    {
        public MoveItemStackRequest(CampaignHandle campaign, ItemStackId targetId, long expectedTargetRevision, InventoryId sourceInventoryId, long expectedSourceRevision, InventoryId destinationInventoryId, long expectedDestinationRevision, string destinationContainerKey, UserId actorUserId, bool actorIsMainGm, CommandId commandId, CorrelationId correlationId)
        { if (campaign == null || !targetId.IsValid || !actorUserId.IsValid) throw new ArgumentException("Campaign, target, and actor are required."); Campaign = campaign; ActorIsMainGm = actorIsMainGm; CorrelationId = correlationId; Move = new InventoryMove(InventoryItemRef.ForStack(targetId), sourceInventoryId, expectedTargetRevision, expectedSourceRevision, destinationInventoryId, expectedDestinationRevision, destinationContainerKey, commandId); }
        public CampaignHandle Campaign { get; }
        public bool ActorIsMainGm { get; }
        public CorrelationId CorrelationId { get; }
        public InventoryMove Move { get; }
    }

    public static class InventoryMovementFailures
    {
        public static Error Denied(CorrelationId id) => Error.Create(ErrorCodes.InventoryMoveDenied, ErrorCategory.Authorization, SafeReasonCode.PermissionDenied, UserMessageKey.Parse("errors.inventory.move_denied"), RetryDirective.DoNotRetry, id);
        public static Error SourceInvalid(CorrelationId id) => Error.Create(ErrorCodes.InventoryMoveSourceInvalid, ErrorCategory.Validation, SafeReasonCode.InvalidRequest, UserMessageKey.Parse("errors.inventory.move_source_invalid"), RetryDirective.DoNotRetry, id);
        public static Error DestinationUnchanged(CorrelationId id) => Error.Create(ErrorCodes.InventoryMoveDestinationUnchanged, ErrorCategory.Conflict, SafeReasonCode.ActionNotAllowed, UserMessageKey.Parse("errors.inventory.move_destination_unchanged"), RetryDirective.DoNotRetry, id);
        public static Error ItemRevisionConflict(CorrelationId id) => Error.Create(ErrorCodes.PersistenceInventoryItemRevisionConflict, ErrorCategory.Conflict, SafeReasonCode.StateChanged, UserMessageKey.Parse("errors.persistence.inventory_item_revision_conflict"), RetryDirective.DoNotRetry, id);
        public static Error InventoryRevisionConflict(CorrelationId id) => Error.Create(ErrorCodes.PersistenceInventoryRevisionConflict, ErrorCategory.Conflict, SafeReasonCode.StateChanged, UserMessageKey.Parse("errors.persistence.inventory_revision_conflict"), RetryDirective.DoNotRetry, id);
    }
}
