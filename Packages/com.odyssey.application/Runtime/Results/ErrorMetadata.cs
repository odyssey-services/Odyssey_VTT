using System;

namespace Odyssey.Application.Results
{
    public readonly struct ErrorMetadata : IEquatable<ErrorMetadata>
    {
        public const int MaxMetadataPerError = 8;
        public const int MaxKeyLength = 48;
        public const int MaxValueLength = 96;
        private readonly string _key;
        private readonly string _value;

        private ErrorMetadata(string key, string value)
        {
            _key = key;
            _value = value;
        }

        public string Key => _key ?? string.Empty;
        public string Value => _value ?? string.Empty;
        public bool IsValid => _key != null && _value != null;

        public static ErrorMetadata Create(string key, string value)
        {
            if (!IsSafeKey(key))
            {
                throw new ArgumentException("Metadata key is not allowlisted.", nameof(key));
            }

            if (!SafeMessageArgument.IsSafe(value, MaxValueLength))
            {
                throw new ArgumentException("Metadata value is not allowlisted.", nameof(value));
            }

            return new ErrorMetadata(key, value);
        }

        public bool Equals(ErrorMetadata other)
        {
            return string.Equals(_key, other._key, StringComparison.Ordinal) &&
                string.Equals(_value, other._value, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj) => obj is ErrorMetadata other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Key, Value);

        private static bool IsSafeKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key) || key!.Length > MaxKeyLength || key.Trim() != key)
            {
                return false;
            }

            for (int index = 0; index < key.Length; index++)
            {
                char c = key[index];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '.'))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
