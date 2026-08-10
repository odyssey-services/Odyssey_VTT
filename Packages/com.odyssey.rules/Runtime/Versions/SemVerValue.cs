using System;

namespace Odyssey.Rules.Versions
{
    internal readonly struct SemVerValue : IEquatable<SemVerValue>, IComparable<SemVerValue>
    {
        private readonly bool _valid;

        private SemVerValue(int major, int minor, int patch)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            _valid = true;
        }

        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public bool IsValid => _valid;

        public static bool TryParse(string? value, out SemVerValue version)
        {
            version = default;
            if (string.IsNullOrWhiteSpace(value) || value!.Trim() != value)
            {
                return false;
            }

            string[] parts = value.Split('.');
            if (parts.Length != 3)
            {
                return false;
            }

            if (!TryParsePart(parts[0], out int major) ||
                !TryParsePart(parts[1], out int minor) ||
                !TryParsePart(parts[2], out int patch))
            {
                return false;
            }

            version = new SemVerValue(major, minor, patch);
            return true;
        }

        public int CompareTo(SemVerValue other)
        {
            if (!IsValid || !other.IsValid)
            {
                throw new InvalidOperationException("Invalid SemVerValue cannot be compared.");
            }

            int major = Major.CompareTo(other.Major);
            if (major != 0) return major;
            int minor = Minor.CompareTo(other.Minor);
            if (minor != 0) return minor;
            return Patch.CompareTo(other.Patch);
        }

        public bool Equals(SemVerValue other)
        {
            return _valid == other._valid && Major == other.Major && Minor == other.Minor && Patch == other.Patch;
        }

        public override bool Equals(object? obj) => obj is SemVerValue other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(_valid, Major, Minor, Patch);
        public override string ToString() => IsValid ? Major + "." + Minor + "." + Patch : string.Empty;

        private static bool TryParsePart(string part, out int value)
        {
            value = 0;
            if (part.Length == 0 || (part.Length > 1 && part[0] == '0'))
            {
                return false;
            }

            for (int index = 0; index < part.Length; index++)
            {
                char c = part[index];
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }

            return int.TryParse(part, out value);
        }
    }
}
