using Odyssey.Application.Results;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Inventory
{
    /// <summary>
    /// ODY-S05-205: public-safe error factories for stack split/merge rejections.
    /// Kept separate from <see cref="InventoryMovementFailures"/> because stack
    /// quantity and mechanical-identity rules are a different responsibility from
    /// item movement across inventory locations.
    /// </summary>
    public static class InventoryStackFailures
    {
        public static Error SplitQuantityInvalid(CorrelationId id) => Error.Create(
            ErrorCodes.InventoryStackSplitQuantityInvalid,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.inventory.stack_split_quantity_invalid"),
            RetryDirective.DoNotRetry,
            id);

        public static Error MergeMismatch(CorrelationId id) => Error.Create(
            ErrorCodes.InventoryStackMergeMismatch,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.inventory.stack_merge_mismatch"),
            RetryDirective.DoNotRetry,
            id);

        public static Error MergeExceedsMaxQuantity(CorrelationId id) => Error.Create(
            ErrorCodes.InventoryStackMergeExceedsMaxQuantity,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.inventory.stack_merge_exceeds_max_quantity"),
            RetryDirective.DoNotRetry,
            id);
    }
}
