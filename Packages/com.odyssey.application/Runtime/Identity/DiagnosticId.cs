using System;

namespace Odyssey.Application.Identity
{
    public readonly struct DiagnosticId : IEquatable<DiagnosticId>
    {
        private const string Prefix = "diag_";
        private const int HexLength = 32;
        private readonly string _value;

        private DiagnosticId(string value)
        {
            _value = value;
        }

        public bool IsValid => _value != null;

        public static bool TryParse(string? value, out DiagnosticId diagnosticId)
        {
            if (IsCanonical(value))
            {
                diagnosticId = new DiagnosticId(value!);
                return true;
            }

            diagnosticId = default;
            return false;
        }

        public static DiagnosticId Parse(string value)
        {
            if (!TryParse(value, out DiagnosticId diagnosticId))
            {
                throw new FormatException("DiagnosticId is not canonical.");
            }

            return diagnosticId;
        }

        public override string ToString()
        {
            return _value ?? string.Empty;
        }

        public bool Equals(DiagnosticId other)
        {
            return string.Equals(_value, other._value, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is DiagnosticId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        }

        public static bool operator ==(DiagnosticId left, DiagnosticId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DiagnosticId left, DiagnosticId right)
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
