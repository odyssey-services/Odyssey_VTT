using System;

namespace Odyssey.Content.Versions
{
    public readonly struct ContentPackageVersion : IEquatable<ContentPackageVersion>, IComparable<ContentPackageVersion>
    {
        private readonly SemVerValue _value;

        private ContentPackageVersion(SemVerValue value)
        {
            _value = value;
        }

        public int Major => _value.Major;
        public int Minor => _value.Minor;
        public int Patch => _value.Patch;
        public bool IsValid => _value.IsValid;

        public static bool TryParse(string? value, out ContentPackageVersion version)
        {
            if (SemVerValue.TryParse(value, out SemVerValue parsed))
            {
                version = new ContentPackageVersion(parsed);
                return true;
            }

            version = default;
            return false;
        }

        public static ContentPackageVersion Parse(string value)
        {
            if (!TryParse(value, out ContentPackageVersion version))
            {
                throw new FormatException("ContentPackageVersion is not canonical SemVer.");
            }

            return version;
        }

        public int CompareTo(ContentPackageVersion other) => _value.CompareTo(other._value);
        public bool Equals(ContentPackageVersion other) => _value.Equals(other._value);
        public override bool Equals(object? obj) => obj is ContentPackageVersion other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public override string ToString() => _value.ToString();

        public static bool operator ==(ContentPackageVersion left, ContentPackageVersion right) => left.Equals(right);
        public static bool operator !=(ContentPackageVersion left, ContentPackageVersion right) => !left.Equals(right);
        public static bool operator <(ContentPackageVersion left, ContentPackageVersion right) => left.CompareTo(right) < 0;
        public static bool operator >(ContentPackageVersion left, ContentPackageVersion right) => left.CompareTo(right) > 0;
    }
}
