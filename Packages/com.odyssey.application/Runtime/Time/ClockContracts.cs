using System;
using System.Threading;
using System.Threading.Tasks;

namespace Odyssey.Application.Time
{
    public readonly struct UtcInstant : IEquatable<UtcInstant>, IComparable<UtcInstant>
    {
        private readonly DateTimeOffset _value;
        private readonly bool _isValid;

        private UtcInstant(DateTimeOffset value)
        {
            _value = value.ToUniversalTime();
            _isValid = true;
        }

        public bool IsValid => _isValid;
        public DateTimeOffset Value => IsValid ? _value : throw new InvalidOperationException("UtcInstant is invalid.");
        public static UtcInstant FromDateTimeOffset(DateTimeOffset value) => new UtcInstant(value);
        public static UtcInstant Parse(string value) => FromDateTimeOffset(DateTimeOffset.ParseExact(value, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal));
        public UtcInstant Add(TimeSpan value) => new UtcInstant(Value.Add(value));
        public int CompareTo(UtcInstant other) => Value.CompareTo(other.Value);
        public bool Equals(UtcInstant other) => _isValid == other._isValid && Value.Equals(other.Value);
        public override bool Equals(object? obj) => obj is UtcInstant other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Value, _isValid);
        public override string ToString() => IsValid ? Value.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        public static bool operator ==(UtcInstant left, UtcInstant right) => left.Equals(right);
        public static bool operator !=(UtcInstant left, UtcInstant right) => !left.Equals(right);
    }

    public readonly struct MonotonicTimestamp : IEquatable<MonotonicTimestamp>
    {
        private readonly long _opaqueTicks;
        private readonly bool _isValid;

        private MonotonicTimestamp(long opaqueTicks)
        {
            _opaqueTicks = opaqueTicks;
            _isValid = true;
        }

        public bool IsValid => _isValid;
        internal long OpaqueTicks => IsValid ? _opaqueTicks : throw new InvalidOperationException("MonotonicTimestamp is invalid.");
        public static MonotonicTimestamp FromTestTicks(long ticks) => ticks >= 0 ? new MonotonicTimestamp(ticks) : throw new ArgumentOutOfRangeException(nameof(ticks));
        public bool Equals(MonotonicTimestamp other) => _isValid == other._isValid && _opaqueTicks == other._opaqueTicks;
        public override bool Equals(object? obj) => obj is MonotonicTimestamp other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(_opaqueTicks, _isValid);
        public static bool operator ==(MonotonicTimestamp left, MonotonicTimestamp right) => left.Equals(right);
        public static bool operator !=(MonotonicTimestamp left, MonotonicTimestamp right) => !left.Equals(right);
    }

    public interface IWallClock
    {
        UtcInstant GetUtcNow();
    }

    public interface IMonotonicClock
    {
        MonotonicTimestamp GetTimestamp();
        TimeSpan GetElapsedTime(MonotonicTimestamp start, MonotonicTimestamp end);
    }

    public interface IDelayScheduler
    {
        ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
    }
}
