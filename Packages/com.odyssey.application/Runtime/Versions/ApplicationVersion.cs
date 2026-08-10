using System;

namespace Odyssey.Application.Versions
{
    public readonly struct ApplicationVersion : IEquatable<ApplicationVersion>, IComparable<ApplicationVersion>
    {
        private readonly SemVerValue _value;

        private ApplicationVersion(SemVerValue value)
        {
            _value = value;
        }

        public int Major => _value.Major;
        public int Minor => _value.Minor;
        public int Patch => _value.Patch;
        public bool IsValid => _value.IsValid;

        public static bool TryParse(string? value, out ApplicationVersion version)
        {
            if (SemVerValue.TryParse(value, out SemVerValue parsed))
            {
                version = new ApplicationVersion(parsed);
                return true;
            }

            version = default;
            return false;
        }

        public static ApplicationVersion Parse(string value)
        {
            if (!TryParse(value, out ApplicationVersion version))
            {
                throw new FormatException("ApplicationVersion is not canonical SemVer.");
            }

            return version;
        }

        public int CompareTo(ApplicationVersion other)
        {
            return _value.CompareTo(other._value);
        }

        public bool Equals(ApplicationVersion other)
        {
            return _value.Equals(other._value);
        }

        public override bool Equals(object? obj)
        {
            return obj is ApplicationVersion other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        public override string ToString()
        {
            return _value.ToString();
        }

        public static bool operator ==(ApplicationVersion left, ApplicationVersion right) => left.Equals(right);
        public static bool operator !=(ApplicationVersion left, ApplicationVersion right) => !left.Equals(right);
        public static bool operator <(ApplicationVersion left, ApplicationVersion right) => left.CompareTo(right) < 0;
        public static bool operator >(ApplicationVersion left, ApplicationVersion right) => left.CompareTo(right) > 0;
    }
}
