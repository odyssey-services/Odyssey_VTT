using System;

namespace Odyssey.Domain.Time
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

        public int CompareTo(UtcInstant other)
        {
            if (!IsValid || !other.IsValid) throw new InvalidOperationException("Cannot compare invalid UtcInstant values.");
            return _value.CompareTo(other._value);
        }

        public bool Equals(UtcInstant other)
        {
            if (!IsValid || !other.IsValid) return IsValid == other.IsValid;
            return _value.Equals(other._value);
        }

        public override bool Equals(object? obj) => obj is UtcInstant other && Equals(other);
        public override int GetHashCode() => IsValid ? HashCode.Combine(_value, _isValid) : 0;
        public override string ToString() => IsValid ? Value.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        public static bool operator ==(UtcInstant left, UtcInstant right) => left.Equals(right);
        public static bool operator !=(UtcInstant left, UtcInstant right) => !left.Equals(right);
    }
}
