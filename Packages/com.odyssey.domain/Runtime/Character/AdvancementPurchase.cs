using System;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Domain.Character
{
    /// <summary>
    /// ODY-S04-107 (pkt 0 gap fix): product section 13.2's
    /// <c>AdvancementPurchase.OperationKind</c> discriminator for the single
    /// generic <see cref="AdvancementPurchase.TargetDefinitionId"/> field --
    /// product's own schema already resolves the "attribute or skill"
    /// question with one string field plus this enum, rather than two
    /// near-duplicate purchase-record types or an interface/union. Reused
    /// verbatim rather than inventing a different shape.
    /// </summary>
    public enum AdvancementOperationKind
    {
        AttributeIncrease = 1,
        SkillLevelPurchase = 2,

        /// <summary>
        /// ODY-S04-108: ADR-024 section 5.1's <c>AcquireAbility</c> purchase
        /// pipeline for <c>SourceKind=ProgressionPurchase</c> also creates an
        /// <c>AdvancementPurchase</c>. This value is deliberately NOT
        /// accepted by <c>RevertAdvancementPurchase</c>/
        /// <c>ApplyCharacterRespec</c>/<c>ComputeRespecPlan</c> (both
        /// explicitly reject it with <c>CharacterAdvancementOperationKindNotSupported</c>
        /// rather than mis-parsing <see cref="AdvancementPurchase.TargetDefinitionId"/>
        /// as a <c>SkillDefinitionId</c>) -- reverting/respeccing an ability
        /// acquisition is explicitly out of scope for ODY-S04-107/108.
        /// </summary>
        AbilityAcquisition = 3
    }

    /// <summary>ODY-S04-107: product section 13.2's <c>AdvancementPurchase.Status</c>.</summary>
    public enum AdvancementPurchaseStatus
    {
        Applied = 1,
        Reverted = 2,
        SupersededByRespec = 3
    }

    /// <summary>
    /// ODY-S04-107 (pkt 0 gap fix): ADR-024 section 3.3/5.1 step 4's
    /// <c>AdvancementPurchase</c> -- the historical-snapshot-bearing record
    /// every successful <c>PurchaseAttributeIncrease</c>/<c>PurchaseSkillLevel</c>/
    /// approved <c>ResolveAdvancementRecommendation</c> must create in the
    /// same transaction (ADR-024 section 5.1 step 4), and that
    /// <c>RevertAdvancementPurchase</c>/<c>ApplyCharacterRespec</c> reference
    /// to know what to undo. This is a value row co-committed with the
    /// causing <c>DomainEvent</c> -- like <see cref="DevelopmentTransaction"/>,
    /// it is not itself an event and carries no independent authority beyond
    /// recording the historical fact of one purchase.
    /// </summary>
    public sealed class AdvancementPurchase
    {
        public AdvancementPurchase(
            AdvancementPurchaseId purchaseId,
            CharacterId characterId,
            AdvancementOperationKind operationKind,
            string targetDefinitionId,
            long fromValue,
            long toValue,
            long cost,
            string requirementsSnapshot,
            string rulesetVersion,
            UserId actorUserId,
            UtcInstant createdAt,
            AdvancementPurchaseStatus status)
        {
            if (!purchaseId.IsValid) throw new ArgumentException("PurchaseId is required.", nameof(purchaseId));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (!Enum.IsDefined(typeof(AdvancementOperationKind), operationKind)) throw new ArgumentOutOfRangeException(nameof(operationKind));
            if (string.IsNullOrWhiteSpace(targetDefinitionId)) throw new ArgumentException("TargetDefinitionId is required.", nameof(targetDefinitionId));
            if (toValue <= fromValue) throw new ArgumentOutOfRangeException(nameof(toValue), "ToValue must exceed FromValue -- a purchase is always an increase.");
            // ADR-024 section 6.1 branch 3: an advancement approved without
            // spending Reserved points (fully funded by consumed evidence)
            // still produces an AdvancementPurchase -- Cost is legitimately
            // 0 for that case, not necessarily positive.
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
            if (requirementsSnapshot == null) throw new ArgumentNullException(nameof(requirementsSnapshot));
            if (string.IsNullOrWhiteSpace(rulesetVersion)) throw new ArgumentException("RulesetVersion is required.", nameof(rulesetVersion));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (!Enum.IsDefined(typeof(AdvancementPurchaseStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));

            PurchaseId = purchaseId;
            CharacterId = characterId;
            OperationKind = operationKind;
            TargetDefinitionId = targetDefinitionId;
            FromValue = fromValue;
            ToValue = toValue;
            Cost = cost;
            RequirementsSnapshot = requirementsSnapshot;
            RulesetVersion = rulesetVersion;
            ActorUserId = actorUserId;
            CreatedAt = createdAt;
            Status = status;
        }

        public AdvancementPurchaseId PurchaseId { get; }
        public CharacterId CharacterId { get; }
        public AdvancementOperationKind OperationKind { get; }
        public string TargetDefinitionId { get; }
        public long FromValue { get; }
        public long ToValue { get; }
        public long Cost { get; }

        /// <summary>
        /// ADR-024 does not specify a concrete format (section 3.3's own
        /// text: "the exact dependency graph is a Rules Engine/ruleset
        /// concern"). This task's own minimal fixture: an opaque string,
        /// <c>"{}"</c> when no requirements engine exists to snapshot
        /// (the case for every purchase this codebase can currently
        /// produce) -- a future Rules Engine task may populate it with a
        /// real JSON requirements snapshot without changing this field's
        /// own type.
        /// </summary>
        public string RequirementsSnapshot { get; }
        public string RulesetVersion { get; }
        public UserId ActorUserId { get; }
        public UtcInstant CreatedAt { get; }
        public AdvancementPurchaseStatus Status { get; }
    }
}
