using System;
using System.Text.RegularExpressions;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Domain.Character
{
    /// <summary>
    /// ODY-S04-105: 10_Characters_And_Progression section 11's
    /// <c>AttributeValue.AttributeDefinitionId</c> -- a stable, Ruleset-
    /// authored catalog key (e.g. "Strength"), not a randomly minted
    /// instance identifier. Deliberately not the canonical
    /// <c>Prefix + Uuid7.NewHex32</c> shape every aggregate-scoped ID in this
    /// codebase uses -- there is exactly one small, Ruleset-fixed catalog of
    /// these, matching how a definition key (as opposed to an instance id)
    /// is modeled elsewhere in this codebase's design vocabulary.
    /// </summary>
    public readonly struct AttributeDefinitionId : IEquatable<AttributeDefinitionId>
    {
        private static readonly Regex ValidPattern = new Regex("^[A-Za-z][A-Za-z0-9_]{0,63}$", RegexOptions.Compiled);
        private readonly string _value;

        private AttributeDefinitionId(string value) => _value = value;
        public bool IsValid => _value != null;

        public static bool TryParse(string? value, out AttributeDefinitionId id)
        {
            if (value != null && ValidPattern.IsMatch(value))
            {
                id = new AttributeDefinitionId(value);
                return true;
            }

            id = default;
            return false;
        }

        public static AttributeDefinitionId Parse(string value) => TryParse(value, out AttributeDefinitionId id) ? id : throw new FormatException("AttributeDefinitionId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(AttributeDefinitionId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is AttributeDefinitionId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(AttributeDefinitionId left, AttributeDefinitionId right) => left.Equals(right);
        public static bool operator !=(AttributeDefinitionId left, AttributeDefinitionId right) => !left.Equals(right);
    }

    /// <summary>
    /// ODY-S04-105: product section 12.1's <c>DevelopmentTransaction.Kind</c>.
    /// Only <see cref="Grant"/>/<see cref="Spend"/> are produced by this
    /// task's own commands (<c>GrantDevelopmentPoints</c>/
    /// <c>PurchaseAttributeIncrease</c>); the remaining values are product's
    /// own named vocabulary for later tasks (<c>Reserve</c>/
    /// <c>ReleaseReservation</c> -- ODY-S04-106; <c>Refund</c>/
    /// <c>RespecReturn</c>/<c>RespecSpend</c> -- ODY-S04-107;
    /// <c>Correction</c> -- not yet scoped to any task) -- defined here now
    /// so this enum's shape does not change under those later tasks.
    /// </summary>
    public enum DevelopmentTransactionKind
    {
        Grant = 1,
        Spend = 2,
        Reserve = 3,
        ReleaseReservation = 4,
        Refund = 5,
        Correction = 6,
        RespecReturn = 7,
        RespecSpend = 8
    }

    /// <summary>
    /// ODY-S04-105: ADR-024 section 3.1/4's <c>DevelopmentPool</c> -- current-
    /// state accounting living inside the Character aggregate's own
    /// <c>Mechanics</c> section (ADR-024 section 4.1), never an
    /// independently authoritative subordinate aggregate. <see cref="Reserved"/>
    /// is always zero in this task's own scope (ODY-S04-106 is the first to
    /// move it, ADR-024 section 6.1) but is modeled now so a later task does
    /// not need to widen this type's shape.
    /// </summary>
    public sealed class DevelopmentPool
    {
        public DevelopmentPool(long earned, long spent, long reserved)
        {
            if (earned < 0) throw new ArgumentOutOfRangeException(nameof(earned));
            if (spent < 0) throw new ArgumentOutOfRangeException(nameof(spent));
            if (reserved < 0) throw new ArgumentOutOfRangeException(nameof(reserved));
            if (earned - spent - reserved < 0) throw new ArgumentException("DevelopmentPool cannot go negative (Earned - Spent - Reserved < 0).");

            Earned = earned;
            Spent = spent;
            Reserved = reserved;
        }

        public static DevelopmentPool Empty() => new DevelopmentPool(0, 0, 0);

        public long Earned { get; }
        public long Spent { get; }
        public long Reserved { get; }

        /// <summary>Product section 12: <c>Available = Earned - Spent - Reserved</c> -- always computed, never stored independently.</summary>
        public long Available => Earned - Spent - Reserved;
    }

    /// <summary>
    /// ODY-S04-105: product section 11's <c>AttributeValue</c>, narrowed to
    /// this task's own scope -- <c>TemporaryModifierRefs</c>/active-effect
    /// resolution are not implemented (no effect mechanism exists yet
    /// anywhere in this codebase), so <see cref="EffectiveValue"/> is
    /// computed as <c>BaseValue + PermanentAdjustment</c> only. It is never
    /// stored or settable directly -- product section 11 and ADR-001's Rules
    /// Engine boundary both require effective values to be computed, never
    /// hand-edited.
    /// </summary>
    public sealed class AttributeValue
    {
        public AttributeValue(AttributeDefinitionId attributeDefinitionId, long baseValue, long permanentAdjustment, long spentDevelopmentPoints, long revision)
        {
            if (!attributeDefinitionId.IsValid) throw new ArgumentException("AttributeDefinitionId is required.", nameof(attributeDefinitionId));
            if (baseValue < 0) throw new ArgumentOutOfRangeException(nameof(baseValue));
            if (spentDevelopmentPoints < 0) throw new ArgumentOutOfRangeException(nameof(spentDevelopmentPoints));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));

            AttributeDefinitionId = attributeDefinitionId;
            BaseValue = baseValue;
            PermanentAdjustment = permanentAdjustment;
            SpentDevelopmentPoints = spentDevelopmentPoints;
            Revision = revision;
        }

        public AttributeDefinitionId AttributeDefinitionId { get; }
        public long BaseValue { get; }
        public long PermanentAdjustment { get; }

        /// <summary>Computed only -- see this class's own doc comment. Never persisted as an independently editable field.</summary>
        public long EffectiveValue => BaseValue + PermanentAdjustment;
        public long SpentDevelopmentPoints { get; }

        /// <summary>ADR-024 section 4.2's entry-level revision for the <c>AttributeValue:&lt;AttributeDefinitionId&gt;</c> lock key -- independent of the aggregate-wide <c>MechanicsRevision</c>, gated separately by <c>PurchaseAttributeIncrease</c>.</summary>
        public long Revision { get; }
    }

    /// <summary>
    /// ODY-S04-105: ADR-024 section 3.2/4.3's <c>DevelopmentTransaction</c> --
    /// a ledger/read-model row co-committed with the causing
    /// <c>DomainEvent</c> in the same transaction. It is not itself a
    /// <c>DomainEvent</c> and carries no independent authority (ADR-024
    /// section 4.3) -- if lost or corrupt it is rebuildable from
    /// <c>DomainEvents</c> plus the current authorized Character projection,
    /// the same recovery rule ADR-022 section 8 already gives
    /// <c>CharacterHistoryProjection</c>.
    /// </summary>
    public sealed class DevelopmentTransaction
    {
        public DevelopmentTransaction(
            DevelopmentTransactionId transactionId,
            CharacterId characterId,
            DevelopmentTransactionKind kind,
            long amount,
            string? sourceRef,
            string reason,
            UserId actorUserId,
            string rulesetVersion,
            UtcInstant createdAt,
            CorrelationId correlationId)
        {
            if (!transactionId.IsValid) throw new ArgumentException("TransactionId is required.", nameof(transactionId));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (!Enum.IsDefined(typeof(DevelopmentTransactionKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount is always a positive magnitude; direction is carried by Kind, matching product section 12.1's own schema.");
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason is required.", nameof(reason));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (string.IsNullOrWhiteSpace(rulesetVersion)) throw new ArgumentException("RulesetVersion is required.", nameof(rulesetVersion));

            TransactionId = transactionId;
            CharacterId = characterId;
            Kind = kind;
            Amount = amount;
            SourceRef = sourceRef;
            Reason = reason;
            ActorUserId = actorUserId;
            RulesetVersion = rulesetVersion;
            CreatedAt = createdAt;
            CorrelationId = correlationId;
        }

        public DevelopmentTransactionId TransactionId { get; }
        public CharacterId CharacterId { get; }
        public DevelopmentTransactionKind Kind { get; }

        /// <summary>Always a positive magnitude -- <see cref="Kind"/> (Grant/Spend/...) carries the accounting direction, matching product section 12.1's own schema exactly.</summary>
        public long Amount { get; }

        /// <summary>E.g. the addressed <see cref="AttributeDefinitionId"/> for a Spend -- optional, product section 12.1's own <c>SourceRef?</c>.</summary>
        public string? SourceRef { get; }
        public string Reason { get; }
        public UserId ActorUserId { get; }
        public string RulesetVersion { get; }
        public UtcInstant CreatedAt { get; }
        public CorrelationId CorrelationId { get; }
    }
}
