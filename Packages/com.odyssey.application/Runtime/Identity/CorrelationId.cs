using System;

namespace Odyssey.Application.Identity
{
    public readonly struct CorrelationId : IEquatable<CorrelationId>
    {
        private const string Prefix = "corr_";
        private const int HexLength = 32;
        private readonly string _value;

        private CorrelationId(string value)
        {
            _value = value;
        }

        public bool IsValid => _value != null;

        public static bool TryParse(string? value, out CorrelationId correlationId)
        {
            if (IsCanonical(value))
            {
                correlationId = new CorrelationId(value!);
                return true;
            }

            correlationId = default;
            return false;
        }

        public static CorrelationId Parse(string value)
        {
            if (!TryParse(value, out CorrelationId correlationId))
            {
                throw new FormatException("CorrelationId is not canonical.");
            }

            return correlationId;
        }

        public override string ToString()
        {
            return _value ?? string.Empty;
        }

        public bool Equals(CorrelationId other)
        {
            return string.Equals(_value, other._value, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is CorrelationId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        }

        public static bool operator ==(CorrelationId left, CorrelationId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CorrelationId left, CorrelationId right)
        {
            return !left.Equals(right);
        }

        private static bool IsCanonical(string? value)
        {
            if (value == null || value.Length != Prefix.Length + HexLength)
            {
                return false;
            }

            if (!value.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return false;
            }

            for (int index = Prefix.Length; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
