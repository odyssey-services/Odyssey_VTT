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

    public sealed class DomainEvent
    {
        private DomainEvent(DomainEventId id, DomainEventType type, DomainEventVersion version, TransactionId transactionId, CausationCommandId causationCommandId, long sequence)
        {
            Id = id;
            Type = type;
            Version = version;
            TransactionId = transactionId;
            CausationCommandId = causationCommandId;
            Sequence = sequence;
        }

        public DomainEventId Id { get; }
        public DomainEventType Type { get; }
        public DomainEventVersion Version { get; }
        public TransactionId TransactionId { get; }
        public CausationCommandId CausationCommandId { get; }
        public long Sequence { get; }

        public static DomainEvent Create(DomainEventId id, DomainEventType type, DomainEventVersion version, TransactionId transactionId, CausationCommandId causationCommandId, long sequence)
        {
            if (!id.IsValid) throw new ArgumentException("Domain event id is required.", nameof(id));
            if (!type.IsValid) throw new ArgumentException("Domain event type is required.", nameof(type));
            if (!version.IsValid) throw new ArgumentException("Domain event version is required.", nameof(version));
            if (!transactionId.IsValid) throw new ArgumentException("Transaction id is required.", nameof(transactionId));
            if (!causationCommandId.IsValid) throw new ArgumentException("Causation command id is required.", nameof(causationCommandId));
            if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            return new DomainEvent(id, type, version, transactionId, causationCommandId, sequence);
        }
    }

    public sealed class DomainEventBatch
    {
        private readonly ReadOnlyCollection<DomainEvent> _events;

        private DomainEventBatch(TransactionId transactionId, ReadOnlyCollection<DomainEvent> events)
        {
            TransactionId = transactionId;
            _events = events;
        }

        public TransactionId TransactionId { get; }
        public IReadOnlyList<DomainEvent> Events => _events;

        public static DomainEventBatch Create(TransactionId transactionId, IReadOnlyList<DomainEvent> events)
        {
            if (!transactionId.IsValid) throw new ArgumentException("Transaction id is required.", nameof(transactionId));
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (events.Count == 0) throw new ArgumentException("Event batch must contain at least one event.", nameof(events));

            DomainEvent[] copy = new DomainEvent[events.Count];
            long previousSequence = -1;
            for (int index = 0; index < events.Count; index++)
            {
                DomainEvent current = events[index] ?? throw new ArgumentException("Domain event is required.", nameof(events));
                if (current.TransactionId != transactionId) throw new ArgumentException("All events must share the batch transaction id.", nameof(events));
                if (current.Sequence <= previousSequence) throw new ArgumentException("Events must be in strictly increasing sequence order.", nameof(events));
                previousSequence = current.Sequence;
                copy[index] = current;
            }

            return new DomainEventBatch(transactionId, Array.AsReadOnly(copy));
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
