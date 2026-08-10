using System;

namespace Odyssey.Application.Results
{
    public readonly struct ErrorCode : IEquatable<ErrorCode>
    {
        public const int MaxLength = 96;
        private readonly string _value;

        private ErrorCode(string value)
        {
            _value = value;
        }

        public bool IsValid => _value != null;

        public static bool TryParse(string? value, out ErrorCode code)
        {
            if (IsCanonical(value))
            {
                code = new ErrorCode(value!);
                return true;
            }

            code = default;
            return false;
        }

        public static ErrorCode Parse(string value)
        {
            if (!TryParse(value, out ErrorCode code))
            {
                throw new FormatException("ErrorCode is not canonical.");
            }

            return code;
        }

        public override string ToString() => _value ?? string.Empty;
        public bool Equals(ErrorCode other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is ErrorCode other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(ErrorCode left, ErrorCode right) => left.Equals(right);
        public static bool operator !=(ErrorCode left, ErrorCode right) => !left.Equals(right);

        internal static bool IsCanonical(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > MaxLength)
            {
                return false;
            }

            string[] segments = value.Split('.');
            if (segments.Length < 3)
            {
                return false;
            }

            foreach (string segment in segments)
            {
                if (segment.Length == 0)
                {
                    return false;
                }

                for (int index = 0; index < segment.Length; index++)
                {
                    char c = segment[index];
                    if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
