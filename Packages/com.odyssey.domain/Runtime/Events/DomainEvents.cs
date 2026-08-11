using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace Odyssey.Domain.Events
{
    public readonly struct DomainEventId : IEquatable<DomainEventId>
    {
        private const string Prefix = "evt_";
        private const int HexLength = 32;
        private readonly string _value;

        private DomainEventId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out DomainEventId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new DomainEventId(v));
        public static DomainEventId Parse(string value) => TryParse(value, out DomainEventId id) ? id : throw new FormatException("DomainEventId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(DomainEventId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is DomainEventId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(DomainEventId left, DomainEventId right) => left.Equals(right);
        public static bool operator !=(DomainEventId left, DomainEventId right) => !left.Equals(right);
    }

    public readonly struct TransactionId : IEquatable<TransactionId>
    {
        private const string Prefix = "tx_";
        private const int HexLength = 32;
        private readonly string _value;

        private TransactionId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out TransactionId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new TransactionId(v));
        public static TransactionId Parse(string value) => TryParse(value, out TransactionId id) ? id : throw new FormatException("TransactionId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(TransactionId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is TransactionId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(TransactionId left, TransactionId right) => left.Equals(right);
        public static bool operator !=(TransactionId left, TransactionId right) => !left.Equals(right);
    }

    public readonly struct CausationCommandId : IEquatable<CausationCommandId>
    {
        private const string Prefix = "cmd_";
        private const int HexLength = 32;
        private readonly string _value;

        private CausationCommandId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out CausationCommandId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new CausationCommandId(v));
        public static CausationCommandId Parse(string value) => TryParse(value, out CausationCommandId id) ? id : throw new FormatException("CausationCommandId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(CausationCommandId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is CausationCommandId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(CausationCommandId left, CausationCommandId right) => left.Equals(right);
        public static bool operator !=(CausationCommandId left, CausationCommandId right) => !left.Equals(right);
    }

    public readonly struct EventCorrelationId : IEquatable<EventCorrelationId>
    {
        private const string Prefix = "corr_";
        private const int HexLength = 32;
        private readonly string _value;

        private EventCorrelationId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out EventCorrelationId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new EventCorrelationId(v));
        public static EventCorrelationId Parse(string value) => TryParse(value, out EventCorrelationId id) ? id : throw new FormatException("EventCorrelationId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(EventCorrelationId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is EventCorrelationId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
    }

    public readonly struct DomainCampaignId : IEquatable<DomainCampaignId>
    {
        private const string Prefix = "camp_";
        private const int HexLength = 32;
        private readonly string _value;

        private DomainCampaignId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out DomainCampaignId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new DomainCampaignId(v));
        public static DomainCampaignId Parse(string value) => TryParse(value, out DomainCampaignId id) ? id : throw new FormatException("DomainCampaignId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(DomainCampaignId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is DomainCampaignId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
    }

    public readonly struct DomainEventType : IEquatable<DomainEventType>
    {
        public const int MaxLength = 96;
        private readonly string _value;

        private DomainEventType(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out DomainEventType type)
        {
            if (CanonicalText.IsDottedLowerIdentifier(value, MaxLength, 3))
            {
                type = new DomainEventType(value!);
                return true;
            }

            type = default;
            return false;
        }

        public static DomainEventType Parse(string value) => TryParse(value, out DomainEventType type) ? type : throw new FormatException("DomainEventType is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(DomainEventType other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is DomainEventType other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
    }

    public readonly struct DomainEventVersion : IEquatable<DomainEventVersion>, IComparable<DomainEventVersion>
    {
        private readonly int _value;

        private DomainEventVersion(int value) => _value = value;
        public bool IsValid => _value > 0;
        public int Value => IsValid ? _value : throw new InvalidOperationException("DomainEventVersion is invalid.");
        public static DomainEventVersion Create(int value) => value > 0 ? new DomainEventVersion(value) : throw new ArgumentOutOfRangeException(nameof(value));
        public int CompareTo(DomainEventVersion other) => _value.CompareTo(other._value);
        public bool Equals(DomainEventVersion other) => _value == other._value;
        public override bool Equals(object? obj) => obj is DomainEventVersion other && Equals(other);
        public override int GetHashCode() => _value;
        public override string ToString() => IsValid ? _value.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
    }

    public readonly struct DomainEventPayloadVersion
    {
        private readonly int _value;
        private DomainEventPayloadVersion(int value) => _value = value;
        public bool IsValid => _value > 0;
        public int Value => IsValid ? _value : throw new InvalidOperationException("DomainEventPayloadVersion is invalid.");
        public static DomainEventPayloadVersion Create(int value) => value > 0 ? new DomainEventPayloadVersion(value) : throw new ArgumentOutOfRangeException(nameof(value));
    }

    public readonly struct CampaignRevision : IEquatable<CampaignRevision>, IComparable<CampaignRevision>
    {
        private readonly long _value;
        private CampaignRevision(long value) => _value = value;
        public bool IsValid => _value > 0;
        public long Value => IsValid ? _value : throw new InvalidOperationException("CampaignRevision is invalid.");
        public static CampaignRevision Create(long value) => value > 0 ? new CampaignRevision(value) : throw new ArgumentOutOfRangeException(nameof(value));
        public int CompareTo(CampaignRevision other) => _value.CompareTo(other._value);
        public bool Equals(CampaignRevision other) => _value == other._value;
    }

    public readonly struct AggregateRevision : IEquatable<AggregateRevision>, IComparable<AggregateRevision>
    {
        private readonly long _value;
        private AggregateRevision(long value) => _value = value;
        public bool IsValid => _value > 0;
        public long Value => IsValid ? _value : throw new InvalidOperationException("AggregateRevision is invalid.");
        public static AggregateRevision Create(long value) => value > 0 ? new AggregateRevision(value) : throw new ArgumentOutOfRangeException(nameof(value));
        public int CompareTo(AggregateRevision other) => _value.CompareTo(other._value);
        public bool Equals(AggregateRevision other) => _value == other._value;
    }

    public readonly struct EventSequence : IEquatable<EventSequence>, IComparable<EventSequence>
    {
        private readonly long _value;
        private EventSequence(long value) => _value = value;
        public bool IsValid => _value > 0;
        public long Value => IsValid ? _value : throw new InvalidOperationException("EventSequence is invalid.");
        public static EventSequence Create(long value) => value > 0 ? new EventSequence(value) : throw new ArgumentOutOfRangeException(nameof(value));
        public int CompareTo(EventSequence other) => _value.CompareTo(other._value);
        public bool Equals(EventSequence other) => _value == other._value;
    }

    public readonly struct DomainUtcInstant : IEquatable<DomainUtcInstant>
    {
        private readonly DateTimeOffset _value;
        private readonly bool _isValid;

        private DomainUtcInstant(DateTimeOffset value)
        {
            _value = value.ToUniversalTime();
            _isValid = true;
        }

        public bool IsValid => _isValid;
        public DateTimeOffset Value => IsValid ? _value : throw new InvalidOperationException("DomainUtcInstant is invalid.");
        public static DomainUtcInstant FromDateTimeOffset(DateTimeOffset value) => new DomainUtcInstant(value);
        public override string ToString() => IsValid ? Value.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        public bool Equals(DomainUtcInstant other) => _isValid == other._isValid && Value.Equals(other.Value);
    }

    public readonly struct AggregateIdentity
    {
        public AggregateIdentity(string aggregateType, string aggregateId)
        {
            if (!CanonicalText.IsDottedLowerIdentifier(aggregateType, 96, 2)) throw new ArgumentException("Aggregate type is not canonical.", nameof(aggregateType));
            if (!CanonicalText.IsLowerToken(aggregateId, 96)) throw new ArgumentException("Aggregate id is not canonical.", nameof(aggregateId));
            AggregateType = aggregateType;
            AggregateId = aggregateId;
        }

        public string AggregateType { get; }
        public string AggregateId { get; }
    }

    public readonly struct DomainActor
    {
        public DomainActor(string issuerKind, string? actorUserId, string? actorCharacterId)
        {
            if (!CanonicalText.IsLowerToken(issuerKind, 32)) throw new ArgumentException("Issuer kind is not canonical.", nameof(issuerKind));
            if (actorUserId != null && !CanonicalText.IsLowerToken(actorUserId, 64)) throw new ArgumentException("Actor user id is not canonical.", nameof(actorUserId));
            if (actorCharacterId != null && !CanonicalText.IsLowerToken(actorCharacterId, 64)) throw new ArgumentException("Actor character id is not canonical.", nameof(actorCharacterId));
            IssuerKind = issuerKind;
            ActorUserId = actorUserId;
            ActorCharacterId = actorCharacterId;
        }

        public string IssuerKind { get; }
        public string? ActorUserId { get; }
        public string? ActorCharacterId { get; }
    }

    public readonly struct DomainEventPayload
    {
        public DomainEventPayload(string payloadType)
        {
            if (!CanonicalText.IsDottedLowerIdentifier(payloadType, 96, 3)) throw new ArgumentException("Payload type is not canonical.", nameof(payloadType));
            PayloadType = payloadType;
        }

        public string PayloadType { get; }
    }

    public sealed class DomainEvent
    {
        private DomainEvent(
            DomainEventId id,
            DomainEventType type,
            DomainEventVersion version,
            DomainCampaignId campaignId,
            AggregateIdentity aggregate,
            AggregateRevision aggregateRevision,
            CampaignRevision campaignRevision,
            EventSequence eventSequence,
            TransactionId transactionId,
            CausationCommandId rootCommandId,
            CausationCommandId causationCommandId,
            EventCorrelationId correlationId,
            DomainActor actor,
            DomainUtcInstant occurredAtHost,
            string visibilityPolicy,
            string audienceClassification,
            bool isCompensating,
            IReadOnlyList<DomainEventId> compensatesEventIds,
            string? reasonCode,
            DomainEventPayloadVersion payloadVersion,
            DomainEventPayload payload)
        {
            Id = id;
            Type = type;
            Version = version;
            CampaignId = campaignId;
            Aggregate = aggregate;
            AggregateRevision = aggregateRevision;
            CampaignRevision = campaignRevision;
            EventSequence = eventSequence;
            TransactionId = transactionId;
            RootCommandId = rootCommandId;
            CausationCommandId = causationCommandId;
            CorrelationId = correlationId;
            Actor = actor;
            OccurredAtHost = occurredAtHost;
            VisibilityPolicy = visibilityPolicy;
            AudienceClassification = audienceClassification;
            IsCompensating = isCompensating;
            CompensatesEventIds = Array.AsReadOnly(CopyEventIds(compensatesEventIds));
            ReasonCode = reasonCode;
            PayloadVersion = payloadVersion;
            Payload = payload;
        }

        public DomainEventId Id { get; }
        public DomainEventType Type { get; }
        public DomainEventVersion Version { get; }
        public DomainCampaignId CampaignId { get; }
        public AggregateIdentity Aggregate { get; }
        public AggregateRevision AggregateRevision { get; }
        public CampaignRevision CampaignRevision { get; }
        public EventSequence EventSequence { get; }
        public TransactionId TransactionId { get; }
        public CausationCommandId RootCommandId { get; }
        public CausationCommandId CausationCommandId { get; }
        public EventCorrelationId CorrelationId { get; }
        public DomainActor Actor { get; }
        public DomainUtcInstant OccurredAtHost { get; }
        public string VisibilityPolicy { get; }
        public string AudienceClassification { get; }
        public bool IsCompensating { get; }
        public IReadOnlyList<DomainEventId> CompensatesEventIds { get; }
        public string? ReasonCode { get; }
        public DomainEventPayloadVersion PayloadVersion { get; }
        public DomainEventPayload Payload { get; }

        public static DomainEvent Create(
            DomainEventId id,
            DomainEventType type,
            DomainEventVersion version,
            DomainCampaignId campaignId,
            AggregateIdentity aggregate,
            AggregateRevision aggregateRevision,
            CampaignRevision campaignRevision,
            EventSequence eventSequence,
            TransactionId transactionId,
            CausationCommandId rootCommandId,
            CausationCommandId causationCommandId,
            EventCorrelationId correlationId,
            DomainActor actor,
            DomainUtcInstant occurredAtHost,
            string visibilityPolicy,
            string audienceClassification,
            bool isCompensating,
            IReadOnlyList<DomainEventId> compensatesEventIds,
            string? reasonCode,
            DomainEventPayloadVersion payloadVersion,
            DomainEventPayload payload)
        {
            if (!id.IsValid) throw new ArgumentException("Domain event id is required.", nameof(id));
            if (!type.IsValid) throw new ArgumentException("Domain event type is required.", nameof(type));
            if (!version.IsValid) throw new ArgumentException("Domain event version is required.", nameof(version));
            if (!campaignId.IsValid) throw new ArgumentException("Campaign id is required.", nameof(campaignId));
            if (!aggregateRevision.IsValid) throw new ArgumentException("Aggregate revision is required.", nameof(aggregateRevision));
            if (!campaignRevision.IsValid) throw new ArgumentException("Campaign revision is required.", nameof(campaignRevision));
            if (!eventSequence.IsValid) throw new ArgumentException("Event sequence is required.", nameof(eventSequence));
            if (!transactionId.IsValid) throw new ArgumentException("Transaction id is required.", nameof(transactionId));
            if (!rootCommandId.IsValid) throw new ArgumentException("Root command id is required.", nameof(rootCommandId));
            if (!causationCommandId.IsValid) throw new ArgumentException("Causation command id is required.", nameof(causationCommandId));
            if (!correlationId.IsValid) throw new ArgumentException("Correlation id is required.", nameof(correlationId));
            if (!occurredAtHost.IsValid) throw new ArgumentException("OccurredAtHost is required.", nameof(occurredAtHost));
            if (!CanonicalText.IsLowerToken(visibilityPolicy, 64)) throw new ArgumentException("Visibility policy is not canonical.", nameof(visibilityPolicy));
            if (!CanonicalText.IsLowerToken(audienceClassification, 64)) throw new ArgumentException("Audience classification is not canonical.", nameof(audienceClassification));
            if (reasonCode != null && !CanonicalText.IsDottedLowerIdentifier(reasonCode, 96, 2)) throw new ArgumentException("Reason code is not canonical.", nameof(reasonCode));
            if (!payloadVersion.IsValid) throw new ArgumentException("Payload version is required.", nameof(payloadVersion));
            return new DomainEvent(id, type, version, campaignId, aggregate, aggregateRevision, campaignRevision, eventSequence, transactionId, rootCommandId, causationCommandId, correlationId, actor, occurredAtHost, visibilityPolicy, audienceClassification, isCompensating, compensatesEventIds ?? Array.Empty<DomainEventId>(), reasonCode, payloadVersion, payload);
        }

        private static DomainEventId[] CopyEventIds(IReadOnlyList<DomainEventId> source)
        {
            DomainEventId[] copy = new DomainEventId[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                if (!source[index].IsValid) throw new ArgumentException("Compensated event id is invalid.", nameof(source));
                copy[index] = source[index];
            }

            return copy;
        }
    }

    public sealed class DomainEventBatch
    {
        private readonly ReadOnlyCollection<DomainEvent> _events;

        private DomainEventBatch(TransactionId transactionId, CampaignRevision campaignRevision, EventSequence from, EventSequence to, ReadOnlyCollection<DomainEvent> events)
        {
            TransactionId = transactionId;
            CampaignRevision = campaignRevision;
            EventSequenceFrom = from;
            EventSequenceTo = to;
            _events = events;
        }

        public TransactionId TransactionId { get; }
        public CampaignRevision CampaignRevision { get; }
        public EventSequence EventSequenceFrom { get; }
        public EventSequence EventSequenceTo { get; }
        public IReadOnlyList<DomainEvent> Events => _events;

        public static DomainEventBatch Create(TransactionId transactionId, IReadOnlyList<DomainEvent> events)
        {
            if (!transactionId.IsValid) throw new ArgumentException("Transaction id is required.", nameof(transactionId));
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (events.Count == 0) throw new ArgumentException("Event batch must contain at least one event.", nameof(events));

            DomainEvent[] copy = new DomainEvent[events.Count];
            EventSequence? previousSequence = null;
            CampaignRevision? campaignRevision = null;
            for (int index = 0; index < events.Count; index++)
            {
                DomainEvent current = events[index] ?? throw new ArgumentException("Domain event is required.", nameof(events));
                if (current.TransactionId != transactionId) throw new ArgumentException("All events must share the batch transaction id.", nameof(events));
                if (campaignRevision.HasValue && !current.CampaignRevision.Equals(campaignRevision.Value)) throw new ArgumentException("All events must share campaign revision.", nameof(events));
                if (previousSequence.HasValue && current.EventSequence.Value != previousSequence.Value.Value + 1) throw new ArgumentException("Events must form a continuous event sequence range.", nameof(events));
                campaignRevision = current.CampaignRevision;
                previousSequence = current.EventSequence;
                copy[index] = current;
            }

            return new DomainEventBatch(transactionId, campaignRevision!.Value, copy[0].EventSequence, copy[copy.Length - 1].EventSequence, Array.AsReadOnly(copy));
        }
    }

    internal static class CanonicalId
    {
        internal static bool TryParse<T>(string? value, string prefix, int hexLength, out T result, Func<string, T> factory)
        {
            if (value == null || value.Length != prefix.Length + hexLength || !value.StartsWith(prefix, StringComparison.Ordinal))
            {
                result = default!;
                return false;
            }

            for (int index = prefix.Length; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                {
                    result = default!;
                    return false;
                }
            }

            result = factory(value);
            return true;
        }
    }

    internal static class CanonicalText
    {
        internal static bool IsLowerToken(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > maxLength || value.Trim() != value)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-'))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool IsDottedLowerIdentifier(string? value, int maxLength, int minSegments)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > maxLength || value.Trim() != value)
            {
                return false;
            }

            int segments = 1;
            bool segmentHasCharacter = false;
            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (c == '.')
                {
                    if (!segmentHasCharacter) return false;
                    segments++;
                    segmentHasCharacter = false;
                    continue;
                }

                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
                {
                    return false;
                }

                segmentHasCharacter = true;
            }

            return segmentHasCharacter && segments >= minSegments;
        }
    }
}
