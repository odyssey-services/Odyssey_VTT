using System;
using System.Threading;
using System.Threading.Tasks;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Time
{
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
