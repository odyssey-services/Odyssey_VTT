using System;

namespace Odyssey.Application.Time
{
    public readonly struct UtcInstant : IEquatable<UtcInstant>, IComparable<UtcInstant>
    {
        private readonly long _unixMilliseconds;
        private readonly bool _isValid;

        private UtcInstant(long unixMilliseconds)
        {
            _unixMilliseconds = unixMilliseconds;
            _isValid = true;
        }

        public bool IsValid => _isValid;
        public long UnixMilliseconds => IsValid ? _unixMilliseconds : throw new InvalidOperationException("UtcInstant is invalid.");
        public static UtcInstant FromUnixMilliseconds(long unixMilliseconds) => new UtcInstant(unixMilliseconds);

        public static UtcInstant FromDateTimeOffset(DateTimeOffset value)
        {
            if (value.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException("UTC offset must be zero.", nameof(value));
            }

            return new UtcInstant(value.ToUnixTimeMilliseconds());
        }

        public DateTimeOffset ToDateTimeOffset() => DateTimeOffset.FromUnixTimeMilliseconds(UnixMilliseconds);
        public int CompareTo(UtcInstant other) => _unixMilliseconds.CompareTo(other._unixMilliseconds);
        public bool Equals(UtcInstant other) => _isValid == other._isValid && _unixMilliseconds == other._unixMilliseconds;
        public override bool Equals(object? obj) => obj is UtcInstant other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(_unixMilliseconds, _isValid);
        public override string ToString() => IsValid ? ToDateTimeOffset().ToString("O", System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        public static bool operator ==(UtcInstant left, UtcInstant right) => left.Equals(right);
        public static bool operator !=(UtcInstant left, UtcInstant right) => !left.Equals(right);
    }

    public readonly struct MonotonicInstant : IEquatable<MonotonicInstant>, IComparable<MonotonicInstant>
    {
        private readonly long _ticks;
        private readonly bool _isValid;

        private MonotonicInstant(long ticks)
        {
            _ticks = ticks;
            _isValid = true;
        }

        public bool IsValid => _isValid;
        public long Ticks => IsValid ? _ticks : throw new InvalidOperationException("MonotonicInstant is invalid.");
        public static MonotonicInstant FromTicks(long ticks) => ticks >= 0 ? new MonotonicInstant(ticks) : throw new ArgumentOutOfRangeException(nameof(ticks));
        public MonotonicInstant AddTicks(long ticks) => ticks >= 0 ? new MonotonicInstant(Ticks + ticks) : throw new ArgumentOutOfRangeException(nameof(ticks));
        public int CompareTo(MonotonicInstant other) => _ticks.CompareTo(other._ticks);
        public bool Equals(MonotonicInstant other) => _isValid == other._isValid && _ticks == other._ticks;
        public override bool Equals(object? obj) => obj is MonotonicInstant other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(_ticks, _isValid);
        public override string ToString() => IsValid ? _ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        public static bool operator ==(MonotonicInstant left, MonotonicInstant right) => left.Equals(right);
        public static bool operator !=(MonotonicInstant left, MonotonicInstant right) => !left.Equals(right);
        public static bool operator <(MonotonicInstant left, MonotonicInstant right) => left.CompareTo(right) < 0;
        public static bool operator >(MonotonicInstant left, MonotonicInstant right) => left.CompareTo(right) > 0;
    }

    public interface IWallClock
    {
        UtcInstant GetUtcNow();
    }

    public interface IMonotonicClock
    {
        MonotonicInstant GetCurrentInstant();
    }

    public interface IDelayScheduler
    {
        void Schedule(MonotonicInstant dueAt, Action callback);
    }
}
