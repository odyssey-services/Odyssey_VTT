using System;
using Odyssey.Application.Commands;
using Odyssey.Application.Identity;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;

namespace Odyssey.Application.Inventory
{
    public static class InventoryStackOperationService
    {
        public static Result<ItemStackRecord> Split(IInventoryRepository repository, ICampaignRepository campaignRepository, SplitItemStackRequest request)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (campaignRepository == null) throw new ArgumentNullException(nameof(campaignRepository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            Result<bool> mainGmCheck = CampaignMembershipAuthorization.IsMainGm(campaignRepository, request.Campaign, request.ActorUserId, request.CorrelationId);
            if (mainGmCheck.IsFailure)
            {
                return Result<ItemStackRecord>.Failure(mainGmCheck.Error);
            }

            return mainGmCheck.Value ? repository.SplitItemStack(request.Campaign, request.Operation, request.CorrelationId) : Result<ItemStackRecord>.Failure(InventoryMovementFailures.Denied(request.CorrelationId));
        }

        public static Result<ItemStackRecord> Merge(IInventoryRepository repository, ICampaignRepository campaignRepository, MergeItemStacksRequest request)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (campaignRepository == null) throw new ArgumentNullException(nameof(campaignRepository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            Result<bool> mainGmCheck = CampaignMembershipAuthorization.IsMainGm(campaignRepository, request.Campaign, request.ActorUserId, request.CorrelationId);
            if (mainGmCheck.IsFailure)
            {
                return Result<ItemStackRecord>.Failure(mainGmCheck.Error);
            }

            return mainGmCheck.Value ? repository.MergeItemStacks(request.Campaign, request.Operation, request.CorrelationId) : Result<ItemStackRecord>.Failure(InventoryMovementFailures.Denied(request.CorrelationId));
        }
    }

    public sealed class InventoryStackOperation
    {
        public InventoryStackOperation(ItemStackId sourceId, ItemStackId resultId, InventoryId inventoryId, long quantity, long expectedSourceRevision, long expectedResultRevision, long expectedInventoryRevision, CommandId commandId, bool isMerge)
        {
            if (!sourceId.IsValid || !resultId.IsValid || !inventoryId.IsValid || quantity < 0 || expectedSourceRevision < 1 || expectedInventoryRevision < 1 || !commandId.IsValid) throw new ArgumentException("A valid stack operation is required.");
            if (!isMerge && quantity < 1) throw new ArgumentException("Split quantity must be a positive number of units.");
            if (isMerge && quantity != 0) throw new ArgumentException("Merge consumes its whole source and does not take a quantity.");
            if (isMerge && sourceId.Equals(resultId)) throw new ArgumentException("Merge source and destination must differ.");
            if (!isMerge && expectedResultRevision != 0) throw new ArgumentException("Split result has no expected revision.");
            if (isMerge && expectedResultRevision < 1) throw new ArgumentException("Merge destination revision is required.");
            SourceId = sourceId; ResultId = resultId; InventoryId = inventoryId; Quantity = quantity; ExpectedSourceRevision = expectedSourceRevision; ExpectedResultRevision = expectedResultRevision; ExpectedInventoryRevision = expectedInventoryRevision; CommandId = commandId; IsMerge = isMerge;
        }
        public ItemStackId SourceId { get; }
        public ItemStackId ResultId { get; }
        public InventoryId InventoryId { get; }
        public long Quantity { get; }
        public long ExpectedSourceRevision { get; }
        public long ExpectedResultRevision { get; }
        public long ExpectedInventoryRevision { get; }
        public CommandId CommandId { get; }
        public bool IsMerge { get; }
    }

    public sealed class SplitItemStackRequest
    {
        public SplitItemStackRequest(CampaignHandle campaign, ItemStackId sourceId, ItemStackId splitResultId, InventoryId inventoryId, long quantity, long expectedSourceRevision, long expectedInventoryRevision, UserId actorUserId, CommandId commandId, CorrelationId correlationId)
        { Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign)); if (!actorUserId.IsValid) throw new ArgumentException("Actor is required."); ActorUserId = actorUserId; CorrelationId = correlationId; Operation = new InventoryStackOperation(sourceId, splitResultId, inventoryId, quantity, expectedSourceRevision, 0, expectedInventoryRevision, commandId, false); }
        public CampaignHandle Campaign { get; }
        public UserId ActorUserId { get; }
        public CorrelationId CorrelationId { get; }
        public InventoryStackOperation Operation { get; }
    }

    public sealed class MergeItemStacksRequest
    {
        public MergeItemStacksRequest(CampaignHandle campaign, ItemStackId sourceId, ItemStackId destinationId, InventoryId inventoryId, long expectedSourceRevision, long expectedDestinationRevision, long expectedInventoryRevision, UserId actorUserId, CommandId commandId, CorrelationId correlationId)
        { Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign)); if (!actorUserId.IsValid) throw new ArgumentException("Actor is required."); ActorUserId = actorUserId; CorrelationId = correlationId; Operation = new InventoryStackOperation(sourceId, destinationId, inventoryId, 0, expectedSourceRevision, expectedDestinationRevision, expectedInventoryRevision, commandId, true); }
        public CampaignHandle Campaign { get; }
        public UserId ActorUserId { get; }
        public CorrelationId CorrelationId { get; }
        public InventoryStackOperation Operation { get; }
    }
}
