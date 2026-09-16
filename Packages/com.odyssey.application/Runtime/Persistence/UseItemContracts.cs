using System;
using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Content;
using Odyssey.Domain.Effects;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// ODY-S06-107: `ADR-030` §10's own `UseItem` root command -- read-only authoritative state seam,
    /// mirroring `IActivateAbilityStateReader` (`ODY-S06-106`) exactly.
    /// </summary>
    public interface IUseItemStateReader
    {
        Result<UseItemState> Read(CampaignHandle campaign, UseItemIntent intent, CorrelationId correlationId);
        Result<bool> CanControlActor(CampaignHandle campaign, CharacterId actorId, UserId userId, CorrelationId correlationId);
    }

    /// <summary>
    /// The command's own public surface -- which item, whose inventory it belongs to. Deliberately self-only
    /// (no target list): items have no `ContentTargetRule`/target concept anywhere in this codebase
    /// (confirmed by direct code read -- `ItemDefinition` carries no target rule, and `EffectDefinition.
    /// TargetRule` has zero runtime consumers even for abilities), so `UseItem`'s own MVP decision, recorded
    /// here explicitly rather than left implicit, is that a used item's `Instant` effects always target the
    /// actor who used it (the item's own holder). Carries expected revisions for all three sections this
    /// command reads/mutates -- with the first implementation, not as a later doработка, per this task's own
    /// governing ТЗ §0's explicit instruction to build to `ODY-S06-106`'s final, thrice-revised rigor from
    /// the start.
    /// </summary>
    public sealed class UseItemIntent
    {
        public UseItemIntent(CharacterId actorId, InventoryItemRef item, long expectedItemRevision, long expectedInventoryRevision, long expectedCharacterResourcesRevision)
        {
            if (!actorId.IsValid) throw new ArgumentException("ActorId is required.", nameof(actorId));
            if (!item.IsValid) throw new ArgumentException("Item reference is required.", nameof(item));
            if (expectedItemRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedItemRevision));
            if (expectedInventoryRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedInventoryRevision));
            if (expectedCharacterResourcesRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedCharacterResourcesRevision));

            ActorId = actorId;
            Item = item;
            ExpectedItemRevision = expectedItemRevision;
            ExpectedInventoryRevision = expectedInventoryRevision;
            ExpectedCharacterResourcesRevision = expectedCharacterResourcesRevision;
        }

        public CharacterId ActorId { get; }
        public InventoryItemRef Item { get; }
        public long ExpectedItemRevision { get; }
        public long ExpectedInventoryRevision { get; }
        public long ExpectedCharacterResourcesRevision { get; }
    }

    /// <summary>
    /// ODY-S06-107: everything `UseItemService` needs, already resolved by the reader -- the item's own
    /// `ItemDefinition` (decoded straight from its already-pinned `ItemMechanicsSnapshot`, the same "runtime
    /// behavior comes from the pinned snapshot, not today's catalog entry" pattern `ItemEffectLifecycleService.
    /// DecodeItem` already established -- no catalog round-trip is needed for the item itself, unlike
    /// `AbilityDefinition`), the actor's own already-loaded `CharacterRecord`, the item's own `InventoryId`
    /// (for the expected-inventory-revision re-check), and one already-decoded `MechanicsPrimitiveEnvelope`
    /// PER `Instant`-duration built-in effect (each interpreted independently and summed, per this task's
    /// own governing ТЗ §2.4 -- deliberately a list, not one merged envelope, since one item can carry
    /// several distinct `Instant` effects with their own independent formulas).
    /// </summary>
    public sealed class UseItemState
    {
        public UseItemState(CharacterRecord actor, InventoryId inventoryId, ItemDefinition definition, IReadOnlyList<MechanicsPrimitiveEnvelope> instantEffectPrimitives)
        {
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
            if (!inventoryId.IsValid) throw new ArgumentException("InventoryId is required.", nameof(inventoryId));
            InventoryId = inventoryId;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            InstantEffectPrimitives = instantEffectPrimitives ?? throw new ArgumentNullException(nameof(instantEffectPrimitives));
        }

        public CharacterRecord Actor { get; }
        public InventoryId InventoryId { get; }
        public ItemDefinition Definition { get; }
        public IReadOnlyList<MechanicsPrimitiveEnvelope> InstantEffectPrimitives { get; }
    }

    public sealed class UseItemRequest
    {
        public UseItemRequest(UseItemIntent intent, UserId actorUserId, bool actorIsMainGm, CommandId commandId, CorrelationId correlationId)
        {
            Intent = intent ?? throw new ArgumentNullException(nameof(intent));
            if (!actorUserId.IsValid || !commandId.IsValid || !correlationId.IsValid) throw new ArgumentException("Actor, command and correlation identities are required.");
            ActorUserId = actorUserId; ActorIsMainGm = actorIsMainGm; CommandId = commandId; CorrelationId = correlationId;
        }

        public UseItemIntent Intent { get; }
        public UserId ActorUserId { get; }
        public bool ActorIsMainGm { get; }
        public CommandId CommandId { get; }
        public CorrelationId CorrelationId { get; }
    }

    /// <summary>
    /// ODY-S06-107: the durable record of one `UseItem` command's own already-applied outcome -- mirrors
    /// `AbilityActivationRecord`'s own final, thrice-revised shape verbatim (`ODY-S06-106`): `CompensatedAt`
    /// (non-null once compensation genuinely completes -- a permanent failure on replay) and
    /// `CompensationStartedAt` (non-null the instant compensation is first attempted, distinguishing "never
    /// attempted" from "attempted, incomplete" for `UseItemService`'s own idempotency check) are both present
    /// from this task's very first version, not added by a later doработка.
    /// </summary>
    public sealed class ItemUsageRecord
    {
        public ItemUsageRecord(CommandId commandId, CampaignId campaignId, CharacterId actorId, InventoryItemRef item, IReadOnlyList<AttackDelta> resourceDeltas, IReadOnlyList<ContentDefinitionRef> appliedEffectRefs, UtcInstant occurredAt, UtcInstant? compensatedAt, UtcInstant? compensationStartedAt)
        {
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!actorId.IsValid) throw new ArgumentException("ActorId is required.", nameof(actorId));
            if (!item.IsValid) throw new ArgumentException("Item reference is required.", nameof(item));

            CommandId = commandId;
            CampaignId = campaignId;
            ActorId = actorId;
            Item = item;
            ResourceDeltas = Copy(resourceDeltas ?? throw new ArgumentNullException(nameof(resourceDeltas)));
            AppliedEffectRefs = Copy(appliedEffectRefs ?? throw new ArgumentNullException(nameof(appliedEffectRefs)));
            OccurredAt = occurredAt;
            CompensatedAt = compensatedAt;
            CompensationStartedAt = compensationStartedAt;
        }

        public CommandId CommandId { get; }
        public CampaignId CampaignId { get; }
        public CharacterId ActorId { get; }
        public InventoryItemRef Item { get; }
        public IReadOnlyList<AttackDelta> ResourceDeltas { get; }
        public IReadOnlyList<ContentDefinitionRef> AppliedEffectRefs { get; }
        public UtcInstant OccurredAt { get; }
        public UtcInstant? CompensatedAt { get; }
        public UtcInstant? CompensationStartedAt { get; }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source) { T[] copy = new T[source.Count]; for (int index = 0; index < copy.Length; index++) copy[index] = source[index]; return Array.AsReadOnly(copy); }
    }

    /// <summary>
    /// ODY-S06-107: the write side of `UseItem` -- a standalone repository, by the same precedent
    /// `IActivateAbilityRepository` already established as separate from `ICharacterRepository`/
    /// `IInventoryRepository`'s own general CRUD surfaces.
    /// </summary>
    public interface IUseItemRepository
    {
        /// <summary>Idempotency read, called BEFORE any RNG derivation -- see `IActivateAbilityRepository.GetActivation`'s own identical role.</summary>
        Result<ItemUsageRecord> GetUsage(CampaignHandle campaign, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// Atomically, in one SQLite transaction: re-checks <paramref name="expectedItemRevision"/>/
        /// <paramref name="expectedInventoryRevision"/>/<paramref name="expectedCharacterResourcesRevision"/>
        /// fresh; consumes exactly one unit of <paramref name="item"/> (via `SqliteInventoryRepository`'s own
        /// `ConsumeItemUnitInTransaction`, reused by reference); applies every resource delta (via the
        /// existing, unmodified `SqliteAttackApplyRepository.ApplyAttackDelta`); and records the durable
        /// `ItemUsageRecord`. Only once every one of those steps succeeds does this method return success --
        /// `effectsToApply` travels through unapplied; the caller (`UseItemService`) applies each one
        /// afterward via the existing, unmodified `IActiveEffectRepository.CreateActiveEffect`. If any of
        /// those applications fails, the caller invokes <see cref="CompensateItemUsage"/> to reverse this
        /// method's own already-committed changes -- see that method's own doc comment.
        /// </summary>
        Result<ItemUsageRecord> RecordItemUsage(CampaignHandle campaign, CharacterId actorId, InventoryItemRef item, long expectedItemRevision, long expectedInventoryRevision, long expectedCharacterResourcesRevision, IReadOnlyList<AttackDelta> resourceDeltas, IReadOnlyList<ContentDefinitionRef> effectsToApply, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S06-107: reverses EVERYTHING `RecordItemUsage` genuinely applied before an `ApplyEffect`
        /// failure -- the item's own consumption (via `SqliteInventoryRepository.RestoreConsumedItemUnitInTransaction`,
        /// reused by reference), every already-created `ActiveEffectId` in <paramref name="createdEffectIds"/>
        /// (via the existing, unmodified `IActiveEffectRepository.RemoveActiveEffect`), THEN the resource
        /// deltas -- all three side-effect categories, per this task's own governing ТЗ §3, built to
        /// `ODY-S06-106`'s final, thrice-revised compensation shape from the start: durably marks
        /// `CompensationStartedAt` (and the durable created-effect-id list) unconditionally the first time
        /// this method ever runs for a given `originalCommandId`, before any reversal is attempted, so a
        /// LATER retry of the whole `UseItem` command (via `UseItemService`'s own idempotency check) can tell
        /// "never attempted" apart from "attempted, incomplete" and safely resume this exact method. Idempotent
        /// throughout: a second call for an already-compensated usage is a no-op success.
        /// </summary>
        Result<ItemUsageRecord> CompensateItemUsage(CampaignHandle campaign, CommandId originalCommandId, IReadOnlyList<ActiveEffectId> createdEffectIds, UserId actorUserId, CorrelationId correlationId);
    }
}
