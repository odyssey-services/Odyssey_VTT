using System;

namespace Odyssey.Application.Results
{
    public readonly struct SafeMessageArgument : IEquatable<SafeMessageArgument>
    {
        public const int MaxLength = 64;
        private readonly string _value;

        private SafeMessageArgument(string value)
        {
            _value = value;
        }

        public bool IsValid => _value != null;

        public static bool TryCreate(string? value, out SafeMessageArgument argument)
        {
            if (IsSafe(value, MaxLength))
            {
                argument = new SafeMessageArgument(value!);
                return true;
            }

            argument = default;
            return false;
        }

        public static SafeMessageArgument Create(string value)
        {
            if (!TryCreate(value, out SafeMessageArgument argument))
            {
                throw new ArgumentException("Safe message argument is not allowlisted.", nameof(value));
            }

            return argument;
        }

        public override string ToString() => _value ?? string.Empty;
        public bool Equals(SafeMessageArgument other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is SafeMessageArgument other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

        internal static bool IsSafe(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > maxLength || value.Trim() != value)
            {
                return false;
            }

            string lower = value.ToLowerInvariant();
            if (lower.Contains("secret") || lower.Contains("token") || lower.Contains("password") ||
                lower.Contains("exception") || lower.Contains("stack") || lower.Contains("select ") ||
                lower.Contains("insert ") || lower.Contains("update ") || lower.Contains("delete ") ||
                value.Contains(":\\") || value.Contains("/") || value.Contains("\\"))
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                    (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.' || c == ' '))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
