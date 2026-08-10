using System;

namespace Odyssey.Application.Results
{
    public readonly struct SafeReasonCode : IEquatable<SafeReasonCode>
    {
        public const int MaxLength = 64;
        private readonly string _value;

        private SafeReasonCode(string value)
        {
            _value = value;
        }

        public bool IsValid => _value != null;
        public static SafeReasonCode PermissionDenied => Parse("PermissionDenied");
        public static SafeReasonCode InvalidInput => Parse("InvalidInput");
        public static SafeReasonCode UnexpectedError => Parse("UnexpectedError");

        public static bool TryParse(string? value, out SafeReasonCode code)
        {
            if (IsCanonical(value))
            {
                code = new SafeReasonCode(value!);
                return true;
            }

            code = default;
            return false;
        }

        public static SafeReasonCode Parse(string value)
        {
            if (!TryParse(value, out SafeReasonCode code))
            {
                throw new FormatException("SafeReasonCode is not canonical.");
            }

            return code;
        }

        public override string ToString() => _value ?? string.Empty;
        public bool Equals(SafeReasonCode other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is SafeReasonCode other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(SafeReasonCode left, SafeReasonCode right) => left.Equals(right);
        public static bool operator !=(SafeReasonCode left, SafeReasonCode right) => !left.Equals(right);

        private static bool IsCanonical(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > MaxLength || value[0] < 'A' || value[0] > 'Z')
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
