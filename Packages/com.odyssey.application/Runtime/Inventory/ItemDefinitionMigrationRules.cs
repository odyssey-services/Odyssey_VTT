using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Odyssey.Application.Persistence;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;

namespace Odyssey.Application.Inventory
{
    /// <summary>
    /// ODY-S05-401: one <see cref="ItemInstanceRecord"/> a candidate
    /// migration would affect, plus its before/after
    /// <see cref="ItemMechanicsSnapshot"/>. <see cref="AfterSnapshot"/> is a
    /// real, fully-reconstructed snapshot against the target definition
    /// (this task's own ExecPlan section 4, Open Question 4) -- not a
    /// textual field-diff placeholder -- since `ODY-S05-403`'s own apply
    /// step needs exactly this value and would otherwise have to rebuild it
    /// from scratch anyway.
    /// </summary>
    public sealed class ItemDefinitionMigrationAffectedInstance
    {
        public ItemDefinitionMigrationAffectedInstance(ItemInstanceId itemInstanceId, InventoryId inventoryId, long expectedRevision, ItemMechanicsSnapshot beforeSnapshot, ItemMechanicsSnapshot afterSnapshot)
        {
            if (!itemInstanceId.IsValid) throw new ArgumentException("ItemInstanceId is required.", nameof(itemInstanceId));
            if (!inventoryId.IsValid) throw new ArgumentException("InventoryId is required.", nameof(inventoryId));
            if (expectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedRevision));

            ItemInstanceId = itemInstanceId;
            InventoryId = inventoryId;
            ExpectedRevision = expectedRevision;
            BeforeSnapshot = beforeSnapshot;
            AfterSnapshot = afterSnapshot;
        }

        public ItemInstanceId ItemInstanceId { get; }
        public InventoryId InventoryId { get; }
        public long ExpectedRevision { get; }
        public ItemMechanicsSnapshot BeforeSnapshot { get; }
        public ItemMechanicsSnapshot AfterSnapshot { get; }
    }

    /// <summary>
    /// ODY-S05-401: one <see cref="ItemStackRecord"/> that belongs to an
    /// <see cref="ItemDefinitionMigrationAffectedStackGroup"/>, carrying only
    /// the fields that vary per-stack (identity, location, quantity,
    /// revision); the group itself carries the shared before/after snapshot
    /// and <c>StackState</c> every member has in common.
    /// </summary>
    public sealed class ItemDefinitionMigrationAffectedStackMember
    {
        public ItemDefinitionMigrationAffectedStackMember(ItemStackId itemStackId, InventoryId inventoryId, ItemStackQuantity quantity, long expectedRevision)
        {
            if (!itemStackId.IsValid) throw new ArgumentException("ItemStackId is required.", nameof(itemStackId));
            if (!inventoryId.IsValid) throw new ArgumentException("InventoryId is required.", nameof(inventoryId));
            if (!quantity.IsValid) throw new ArgumentException("Quantity is required.", nameof(quantity));
            if (expectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedRevision));

            ItemStackId = itemStackId;
            InventoryId = inventoryId;
            Quantity = quantity;
            ExpectedRevision = expectedRevision;
        }

        public ItemStackId ItemStackId { get; }
        public InventoryId InventoryId { get; }
        public ItemStackQuantity Quantity { get; }
        public long ExpectedRevision { get; }
    }

    /// <summary>
    /// ODY-S05-401: a group of <see cref="ItemStackRecord"/>s the preview
    /// considers "mechanically identical" for migration purposes -- grouped
    /// by <c>SourceItemDefinitionRef</c> + <c>MechanicsSnapshot</c> +
    /// <c>StackState</c> ONLY (this task's own ExecPlan section 4, Open
    /// Question 2). This is deliberately narrower than
    /// <c>SqliteInventoryRepository.MergeItemStacks</c>'s own field set,
    /// which additionally requires equal <c>InventoryId</c>/<c>OwnerRef</c>/
    /// <c>LocationRef</c> -- those three exist there only for merge's own
    /// spatial/ownership precondition (two stacks must already coexist in
    /// the same place to merge), not because `ADR-027` section 6.2's own
    /// prose definition of "mechanically identical" mentions ownership or
    /// location. A migration preview reports "these stacks would end up in
    /// the same mechanical state," regardless of where each one physically
    /// sits -- this divergence from <c>MergeItemStacks</c> is intentional,
    /// not an oversight.
    /// </summary>
    public sealed class ItemDefinitionMigrationAffectedStackGroup
    {
        public ItemDefinitionMigrationAffectedStackGroup(string stackState, ItemMechanicsSnapshot beforeSnapshot, ItemMechanicsSnapshot afterSnapshot, IReadOnlyList<ItemDefinitionMigrationAffectedStackMember> members)
        {
            if (stackState == null) throw new ArgumentNullException(nameof(stackState));
            Members = members ?? throw new ArgumentNullException(nameof(members));
            if (Members.Count == 0) throw new ArgumentException("A stack group must have at least one member.", nameof(members));

            StackState = stackState;
            BeforeSnapshot = beforeSnapshot;
            AfterSnapshot = afterSnapshot;
        }

        public string StackState { get; }
        public ItemMechanicsSnapshot BeforeSnapshot { get; }
        public ItemMechanicsSnapshot AfterSnapshot { get; }
        public IReadOnlyList<ItemDefinitionMigrationAffectedStackMember> Members { get; }
    }

    /// <summary>
    /// ODY-S05-401: the CAS guard for one <see cref="InventoryId"/> referenced
    /// by any affected instance or stack. A single number cannot express a
    /// campaign-wide migration's CAS guard, since one migration can touch
    /// many Characters' inventories at once -- this task's own ExecPlan
    /// section 4, Open Question 3.
    /// </summary>
    public readonly struct ItemDefinitionMigrationInventoryRevision : IEquatable<ItemDefinitionMigrationInventoryRevision>
    {
        public ItemDefinitionMigrationInventoryRevision(InventoryId inventoryId, long expectedRevision)
        {
            if (!inventoryId.IsValid) throw new ArgumentException("InventoryId is required.", nameof(inventoryId));
            if (expectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedRevision));

            InventoryId = inventoryId;
            ExpectedRevision = expectedRevision;
        }

        public InventoryId InventoryId { get; }
        public long ExpectedRevision { get; }
        public bool Equals(ItemDefinitionMigrationInventoryRevision other) => InventoryId.Equals(other.InventoryId) && ExpectedRevision == other.ExpectedRevision;
        public override bool Equals(object? obj) => obj is ItemDefinitionMigrationInventoryRevision other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(InventoryId, ExpectedRevision);
    }

    /// <summary>
    /// ODY-S05-401: `ADR-027` section 10 steps 1/2/4 -- the raw
    /// affected-record list and before/after snapshots for a candidate
    /// source-&gt;target ItemDefinition migration. Carries
    /// ZERO blocking-incompatibility computation (`ODY-S05-402`'s own job),
    /// no `ADR-012` backup reference, and no apply/commit state
    /// (`ODY-S05-403`'s own job) -- this type only answers "what would this
    /// migration touch, and what would it become."
    /// </summary>
    public sealed class ItemDefinitionMigrationPreview
    {
        public ItemDefinitionMigrationPreview(
            ContentDefinitionRef sourceDefinitionRef,
            ContentDefinitionRef targetDefinitionRef,
            long expectedSourceDefinitionRevision,
            IReadOnlyList<ItemDefinitionMigrationAffectedInstance> affectedInstances,
            IReadOnlyList<ItemDefinitionMigrationAffectedStackGroup> affectedStacks,
            IReadOnlyList<ItemDefinitionMigrationInventoryRevision> affectedInventoryRevisions,
            string previewRevision)
        {
            if (!sourceDefinitionRef.IsValid) throw new ArgumentException("Source definition ref is required.", nameof(sourceDefinitionRef));
            if (!targetDefinitionRef.IsValid) throw new ArgumentException("Target definition ref is required.", nameof(targetDefinitionRef));
            if (expectedSourceDefinitionRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedSourceDefinitionRevision));
            if (string.IsNullOrWhiteSpace(previewRevision)) throw new ArgumentException("PreviewRevision is required.", nameof(previewRevision));

            SourceDefinitionRef = sourceDefinitionRef;
            TargetDefinitionRef = targetDefinitionRef;
            ExpectedSourceDefinitionRevision = expectedSourceDefinitionRevision;
            AffectedInstances = affectedInstances ?? throw new ArgumentNullException(nameof(affectedInstances));
            AffectedStacks = affectedStacks ?? throw new ArgumentNullException(nameof(affectedStacks));
            AffectedInventoryRevisions = affectedInventoryRevisions ?? throw new ArgumentNullException(nameof(affectedInventoryRevisions));
            PreviewRevision = previewRevision;
        }

        public ContentDefinitionRef SourceDefinitionRef { get; }
        public ContentDefinitionRef TargetDefinitionRef { get; }
        public long ExpectedSourceDefinitionRevision { get; }
        public IReadOnlyList<ItemDefinitionMigrationAffectedInstance> AffectedInstances { get; }
        public IReadOnlyList<ItemDefinitionMigrationAffectedStackGroup> AffectedStacks { get; }
        public IReadOnlyList<ItemDefinitionMigrationInventoryRevision> AffectedInventoryRevisions { get; }

        /// <summary>`ADR-027` section 10 step 5's own exact field name -- a CAS guard for a later confirm/apply step (`ODY-S05-403`), not part of product's own preview tree.</summary>
        public string PreviewRevision { get; }
    }

    /// <summary>
    /// ODY-S05-401: `ADR-027` section 14's assignment of preview/confirm
    /// orchestration to <c>Odyssey.Application</c> -- not
    /// <c>Odyssey.Rules</c>, despite <c>RulesetMigrationRules</c>
    /// (`ODY-S04-113`) being this class's cited FORM precedent only. This
    /// builder must consume <see cref="ContentDefinitionRecord"/>/
    /// <see cref="ItemInstanceRecord"/>/<see cref="ItemStackRecord"/>, all
    /// three of which are themselves <c>Odyssey.Application</c> types that
    /// <c>Odyssey.Rules</c> is not permitted to depend on (`ADR-001`'s
    /// module dependency direction) -- unlike <c>RulesetMigrationRules</c>,
    /// which only ever consumes pure <c>Odyssey.Domain.Character</c> types.
    /// This class never resolves source/target automatically -- both are
    /// always caller-supplied, already-fetched records (this task's own
    /// ExecPlan section 4, Open Question 1); no version-lineage mechanism
    /// exists anywhere in the codebase to resolve them from.
    /// </summary>
    public static class ItemDefinitionMigrationRules
    {
        public static ItemDefinitionMigrationPreview BuildPreview(
            ContentDefinitionRecord sourceDefinition,
            ContentDefinitionRecord targetDefinition,
            IReadOnlyList<ItemInstanceRecord> affectedInstances,
            IReadOnlyList<ItemStackRecord> affectedStacks,
            IReadOnlyList<InventoryRecord> affectedInventories)
        {
            if (sourceDefinition == null) throw new ArgumentNullException(nameof(sourceDefinition));
            if (targetDefinition == null) throw new ArgumentNullException(nameof(targetDefinition));
            if (affectedInstances == null) throw new ArgumentNullException(nameof(affectedInstances));
            if (affectedStacks == null) throw new ArgumentNullException(nameof(affectedStacks));
            if (affectedInventories == null) throw new ArgumentNullException(nameof(affectedInventories));
            RequirePublished(sourceDefinition, nameof(sourceDefinition));
            RequirePublished(targetDefinition, nameof(targetDefinition));

            var sourceDefinitionRef = new ContentDefinitionRef(sourceDefinition.ContentDefinitionId, sourceDefinition.Version);
            var targetDefinitionRef = new ContentDefinitionRef(targetDefinition.ContentDefinitionId, targetDefinition.Version);

            var inventoryRevisionsById = new Dictionary<InventoryId, long>();
            foreach (InventoryRecord inventory in affectedInventories)
            {
                if (inventory == null) throw new ArgumentException("Affected inventories must not contain null entries.", nameof(affectedInventories));
                inventoryRevisionsById[inventory.InventoryId] = inventory.Revision;
            }

            var instanceEntries = new List<ItemDefinitionMigrationAffectedInstance>();
            foreach (ItemInstanceRecord instance in affectedInstances)
            {
                if (instance == null) throw new ArgumentException("Affected instances must not contain null entries.", nameof(affectedInstances));
                if (!instance.SourceItemDefinitionRef.DefinitionId.Equals(sourceDefinitionRef.DefinitionId))
                {
                    throw new ArgumentException("Every affected ItemInstance must be sourced from the source definition id.", nameof(affectedInstances));
                }
                if (!inventoryRevisionsById.ContainsKey(instance.InventoryId))
                {
                    throw new ArgumentException($"No InventoryRecord was supplied for InventoryId '{instance.InventoryId}' referenced by ItemInstance '{instance.ItemInstanceId}'.", nameof(affectedInventories));
                }

                ItemMechanicsSnapshot afterSnapshot = BuildAfterSnapshot(targetDefinition, targetDefinitionRef);
                instanceEntries.Add(new ItemDefinitionMigrationAffectedInstance(instance.ItemInstanceId, instance.InventoryId, instance.Revision, instance.MechanicsSnapshot, afterSnapshot));
            }

            var stackGroups = new List<ItemDefinitionMigrationAffectedStackGroup>();
            foreach (var group in GroupStacks(affectedStacks, sourceDefinitionRef))
            {
                if (group.Any(stack => !inventoryRevisionsById.ContainsKey(stack.InventoryId)))
                {
                    ItemStackRecord missing = group.First(stack => !inventoryRevisionsById.ContainsKey(stack.InventoryId));
                    throw new ArgumentException($"No InventoryRecord was supplied for InventoryId '{missing.InventoryId}' referenced by ItemStack '{missing.ItemStackId}'.", nameof(affectedInventories));
                }

                ItemMechanicsSnapshot afterSnapshot = BuildAfterSnapshot(targetDefinition, targetDefinitionRef);
                var members = group
                    .Select(stack => new ItemDefinitionMigrationAffectedStackMember(stack.ItemStackId, stack.InventoryId, stack.Quantity, stack.Revision))
                    .ToList();
                stackGroups.Add(new ItemDefinitionMigrationAffectedStackGroup(group.Key.StackState, group.Key.MechanicsSnapshot, afterSnapshot, members));
            }

            var affectedInventoryRevisions = inventoryRevisionsById
                .Where(pair => IsReferenced(pair.Key, affectedInstances, affectedStacks))
                .OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
                .Select(pair => new ItemDefinitionMigrationInventoryRevision(pair.Key, pair.Value))
                .ToList();

            string previewRevision = ComputePreviewRevision(sourceDefinitionRef, targetDefinitionRef, sourceDefinition.Revision, instanceEntries, stackGroups, affectedInventoryRevisions);

            return new ItemDefinitionMigrationPreview(sourceDefinitionRef, targetDefinitionRef, sourceDefinition.Revision, instanceEntries, stackGroups, affectedInventoryRevisions, previewRevision);
        }

        /// <summary>
        /// CAP-INV-004-style CAS guard: a deterministic digest over every
        /// field this preview's own correctness depends on, so
        /// `ODY-S05-403`'s own confirm/apply step can detect a stale or
        /// tampered preview before acting on it. Named <c>PreviewRevision</c>
        /// to match `ADR-027` section 10 step 5's own exact terminology;
        /// the algorithm itself is the same SHA-256-hex digest
        /// <c>RulesetMigrationRules.ComputePreviewHash</c> already uses.
        /// </summary>
        public static string ComputePreviewRevision(
            ContentDefinitionRef sourceDefinitionRef,
            ContentDefinitionRef targetDefinitionRef,
            long expectedSourceDefinitionRevision,
            IReadOnlyList<ItemDefinitionMigrationAffectedInstance> affectedInstances,
            IReadOnlyList<ItemDefinitionMigrationAffectedStackGroup> affectedStacks,
            IReadOnlyList<ItemDefinitionMigrationInventoryRevision> affectedInventoryRevisions)
        {
            if (affectedInstances == null) throw new ArgumentNullException(nameof(affectedInstances));
            if (affectedStacks == null) throw new ArgumentNullException(nameof(affectedStacks));
            if (affectedInventoryRevisions == null) throw new ArgumentNullException(nameof(affectedInventoryRevisions));

            var builder = new StringBuilder();
            builder.Append(sourceDefinitionRef).Append('|').Append(targetDefinitionRef).Append('|').Append(expectedSourceDefinitionRevision).Append('|');

            foreach (ItemDefinitionMigrationInventoryRevision inventoryRevision in affectedInventoryRevisions)
            {
                builder.Append('R').Append(inventoryRevision.InventoryId).Append(':').Append(inventoryRevision.ExpectedRevision).Append(';');
            }

            foreach (ItemDefinitionMigrationAffectedInstance instance in affectedInstances)
            {
                builder.Append('I').Append(instance.ItemInstanceId).Append(':').Append(instance.InventoryId).Append(':').Append(instance.ExpectedRevision).Append(':')
                    .Append(instance.BeforeSnapshot.Payload).Append("->").Append(instance.AfterSnapshot.Payload).Append(';');
            }

            foreach (ItemDefinitionMigrationAffectedStackGroup group in affectedStacks)
            {
                builder.Append('G').Append(group.StackState).Append(':').Append(group.BeforeSnapshot.Payload).Append("->").Append(group.AfterSnapshot.Payload).Append(':');
                foreach (ItemDefinitionMigrationAffectedStackMember member in group.Members)
                {
                    builder.Append(member.ItemStackId).Append('/').Append(member.InventoryId).Append('/').Append(member.Quantity).Append('/').Append(member.ExpectedRevision).Append(',');
                }
                builder.Append(';');
            }

            byte[] bytes = Encoding.UTF8.GetBytes(builder.ToString());
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            byte[] hash = sha256.ComputeHash(bytes);
            var hex = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) hex.Append(b.ToString("x2"));
            return hex.ToString();
        }

        private static ItemMechanicsSnapshot BuildAfterSnapshot(ContentDefinitionRecord targetDefinition, ContentDefinitionRef targetDefinitionRef)
        {
            return new ItemMechanicsSnapshot(targetDefinitionRef, targetDefinition.Version, targetDefinition.DefinitionType, targetDefinition.PropertiesJson);
        }

        private static void RequirePublished(ContentDefinitionRecord definition, string parameterName)
        {
            if (definition.Status != ContentDefinitionStatus.Published || definition.Version < 1)
            {
                throw new ArgumentException("A migration source/target definition must be Published with a pinned version.", parameterName);
            }
        }

        private static bool IsReferenced(InventoryId inventoryId, IReadOnlyList<ItemInstanceRecord> instances, IReadOnlyList<ItemStackRecord> stacks)
        {
            return instances.Any(instance => instance.InventoryId.Equals(inventoryId)) || stacks.Any(stack => stack.InventoryId.Equals(inventoryId));
        }

        private static IEnumerable<IGrouping<StackGroupKey, ItemStackRecord>> GroupStacks(IReadOnlyList<ItemStackRecord> affectedStacks, ContentDefinitionRef sourceDefinitionRef)
        {
            foreach (ItemStackRecord stack in affectedStacks)
            {
                if (stack == null) throw new ArgumentException("Affected stacks must not contain null entries.", nameof(affectedStacks));
                if (!stack.SourceItemDefinitionRef.DefinitionId.Equals(sourceDefinitionRef.DefinitionId))
                {
                    throw new ArgumentException("Every affected ItemStack must be sourced from the source definition id.", nameof(affectedStacks));
                }
            }

            return affectedStacks.GroupBy(stack => new StackGroupKey(stack.SourceItemDefinitionRef, stack.MechanicsSnapshot, stack.StackState));
        }

        /// <summary>
        /// Grouping key for "mechanically identical" stacks (this class's own
        /// doc comment / Open Question 2): exactly
        /// <c>SourceItemDefinitionRef</c> + <c>MechanicsSnapshot</c> +
        /// <c>StackState</c>. Deliberately excludes <c>InventoryId</c>/
        /// <c>OwnerRef</c>/<c>LocationRef</c> so stacks group together
        /// regardless of where they physically sit.
        /// </summary>
        private readonly struct StackGroupKey : IEquatable<StackGroupKey>
        {
            public StackGroupKey(ContentDefinitionRef sourceItemDefinitionRef, ItemMechanicsSnapshot mechanicsSnapshot, string stackState)
            {
                SourceItemDefinitionRef = sourceItemDefinitionRef;
                MechanicsSnapshot = mechanicsSnapshot;
                StackState = stackState;
            }

            public ContentDefinitionRef SourceItemDefinitionRef { get; }
            public ItemMechanicsSnapshot MechanicsSnapshot { get; }
            public string StackState { get; }

            public bool Equals(StackGroupKey other) => SourceItemDefinitionRef.Equals(other.SourceItemDefinitionRef) && MechanicsSnapshot.Equals(other.MechanicsSnapshot) && string.Equals(StackState, other.StackState, StringComparison.Ordinal);
            public override bool Equals(object? obj) => obj is StackGroupKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(SourceItemDefinitionRef, MechanicsSnapshot, StackState == null ? 0 : StringComparer.Ordinal.GetHashCode(StackState));
        }
    }
}
