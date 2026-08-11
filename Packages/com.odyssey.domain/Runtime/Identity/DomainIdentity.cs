using System;

namespace Odyssey.Domain.Identity
{
    public readonly struct CampaignId : IEquatable<CampaignId>
    {
        private const string Prefix = "camp_";
        private const int HexLength = 32;
        private readonly string _value;

        private CampaignId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out CampaignId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new CampaignId(v));
        public static CampaignId Parse(string value) => TryParse(value, out CampaignId id) ? id : throw new FormatException("CampaignId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(CampaignId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is CampaignId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(CampaignId left, CampaignId right) => left.Equals(right);
        public static bool operator !=(CampaignId left, CampaignId right) => !left.Equals(right);
    }

    public readonly struct CorrelationId : IEquatable<CorrelationId>
    {
        private const string Prefix = "corr_";
        private const int HexLength = 32;
        private readonly string _value;

        private CorrelationId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out CorrelationId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new CorrelationId(v));
        public static CorrelationId Parse(string value) => TryParse(value, out CorrelationId id) ? id : throw new FormatException("CorrelationId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(CorrelationId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is CorrelationId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(CorrelationId left, CorrelationId right) => left.Equals(right);
        public static bool operator !=(CorrelationId left, CorrelationId right) => !left.Equals(right);
    }

    public readonly struct SessionId : IEquatable<SessionId>
    {
        private const string Prefix = "sess_";
        private const int HexLength = 32;
        private readonly string _value;

        private SessionId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out SessionId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new SessionId(v));
        public static SessionId Parse(string value) => TryParse(value, out SessionId id) ? id : throw new FormatException("SessionId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(SessionId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is SessionId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
    }

    public readonly struct UserId : IEquatable<UserId>
    {
        private const string Prefix = "user_";
        private const int HexLength = 32;
        private readonly string _value;

        private UserId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out UserId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new UserId(v));
        public static UserId Parse(string value) => TryParse(value, out UserId id) ? id : throw new FormatException("UserId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(UserId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is UserId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
    }

    public readonly struct CharacterId : IEquatable<CharacterId>
    {
        private const string Prefix = "char_";
        private const int HexLength = 32;
        private readonly string _value;

        private CharacterId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out CharacterId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new CharacterId(v));
        public static CharacterId Parse(string value) => TryParse(value, out CharacterId id) ? id : throw new FormatException("CharacterId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(CharacterId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is CharacterId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
    }

    public readonly struct AggregateType : IEquatable<AggregateType>
    {
        private readonly string _value;

        private AggregateType(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out AggregateType type)
        {
            if (CanonicalText.IsDottedLowerIdentifier(value, 96, 2))
            {
                type = new AggregateType(value!);
                return true;
            }

            type = default;
            return false;
        }

        public static AggregateType Parse(string value) => TryParse(value, out AggregateType type) ? type : throw new FormatException("AggregateType is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(AggregateType other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is AggregateType other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
    }

    public readonly struct AggregateId : IEquatable<AggregateId>
    {
        private readonly string _value;

        private AggregateId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out AggregateId id)
        {
            if (CanonicalText.IsLowerToken(value, 96))
            {
                id = new AggregateId(value!);
                return true;
            }

            id = default;
            return false;
        }

        public static AggregateId Parse(string value) => TryParse(value, out AggregateId id) ? id : throw new FormatException("AggregateId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(AggregateId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is AggregateId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
    }

    internal static class CanonicalId
    {
        internal static bool TryParse<T>(string? value, string prefix, int hexLength, out T result, Func<string, T> factory)
        {
            if (value == null || value.Length != prefix.Length + hexLength || !value.StartsWith(prefix, StringComparison.Ordinal))
            {
                result = default!;
                return false;
            }

            for (int index = prefix.Length; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                {
                    result = default!;
                    return false;
                }
            }

            result = factory(value);
            return true;
        }
    }

    internal static class CanonicalText
    {
        internal static bool IsLowerToken(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > maxLength || value.Trim() != value)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-'))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool IsDottedLowerIdentifier(string? value, int maxLength, int minSegments)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > maxLength || value.Trim() != value)
            {
                return false;
            }

            int segments = 1;
            bool segmentHasCharacter = false;
            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (c == '.')
                {
                    if (!segmentHasCharacter) return false;
                    segments++;
                    segmentHasCharacter = false;
                    continue;
                }

                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
                {
                    return false;
                }

                segmentHasCharacter = true;
            }

            return segmentHasCharacter && segments >= minSegments;
        }
    }
}
