using System;

namespace Odyssey.Application.Results
{
    public readonly struct UserMessageKey : IEquatable<UserMessageKey>
    {
        public const int MaxLength = 128;
        private readonly string _value;

        private UserMessageKey(string value)
        {
            _value = value;
        }

        public bool IsValid => _value != null;

        public static bool TryParse(string? value, out UserMessageKey key)
        {
            if (IsCanonical(value))
            {
                key = new UserMessageKey(value!);
                return true;
            }

            key = default;
            return false;
        }

        public static UserMessageKey Parse(string value)
        {
            if (!TryParse(value, out UserMessageKey key))
            {
                throw new FormatException("UserMessageKey is not canonical.");
            }

            return key;
        }

        public override string ToString() => _value ?? string.Empty;
        public bool Equals(UserMessageKey other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is UserMessageKey other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(UserMessageKey left, UserMessageKey right) => left.Equals(right);
        public static bool operator !=(UserMessageKey left, UserMessageKey right) => !left.Equals(right);

        internal static bool IsCanonical(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > MaxLength || !value.StartsWith("errors.", StringComparison.Ordinal) || value.Trim() != value)
            {
                return false;
            }

            bool segmentHasCharacter = false;
            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (c == '.')
                {
                    if (!segmentHasCharacter)
                    {
                        return false;
                    }

                    segmentHasCharacter = false;
                    continue;
                }

                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
                {
                    return false;
                }

                segmentHasCharacter = true;
            }

            return segmentHasCharacter;
        }
    }
}
