using System;

namespace Odyssey.Rules.Versions
{
    public readonly struct RulesetVersion : IEquatable<RulesetVersion>, IComparable<RulesetVersion>
    {
        private readonly SemVerValue _value;

        private RulesetVersion(SemVerValue value)
        {
            _value = value;
        }

        public int Major => _value.Major;
        public int Minor => _value.Minor;
        public int Patch => _value.Patch;
        public bool IsValid => _value.IsValid;

        public static bool TryParse(string? value, out RulesetVersion version)
        {
            if (SemVerValue.TryParse(value, out SemVerValue parsed))
            {
                version = new RulesetVersion(parsed);
                return true;
            }

            version = default;
            return false;
        }

        public static RulesetVersion Parse(string value)
        {
            if (!TryParse(value, out RulesetVersion version))
            {
                throw new FormatException("RulesetVersion is not canonical SemVer.");
            }

            return version;
        }

        public int CompareTo(RulesetVersion other) => _value.CompareTo(other._value);
        public bool Equals(RulesetVersion other) => _value.Equals(other._value);
        public override bool Equals(object? obj) => obj is RulesetVersion other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public override string ToString() => _value.ToString();

        public static bool operator ==(RulesetVersion left, RulesetVersion right) => left.Equals(right);
        public static bool operator !=(RulesetVersion left, RulesetVersion right) => !left.Equals(right);
        public static bool operator <(RulesetVersion left, RulesetVersion right) => left.CompareTo(right) < 0;
        public static bool operator >(RulesetVersion left, RulesetVersion right) => left.CompareTo(right) > 0;
    }
}
