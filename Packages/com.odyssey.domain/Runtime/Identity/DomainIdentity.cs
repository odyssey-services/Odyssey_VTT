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
        public static CampaignId NewId(Odyssey.Domain.Time.UtcInstant now) => new CampaignId(Prefix + Uuid7.NewHex32(now));
        public static bool TryParse(string? value, out CampaignId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new CampaignId(v));
        public static CampaignId Parse(string value) => TryParse(value, out CampaignId id) ? id : throw new FormatException("CampaignId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(CampaignId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is CampaignId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(CampaignId left, CampaignId right) => left.Equals(right);
        public static bool operator !=(CampaignId left, CampaignId right) => !left.Equals(right);
    }

    /// <summary>
    /// Local opaque identifier stored alongside CampaignId. Its full contract (public
    /// addressable identity for future networking) remains ADR-011 section 12.2 [OPEN];
    /// here it is only an identifier with CampaignId's format, per ODY-S01-007 scope.
    /// </summary>
    public readonly struct CampaignPublicId : IEquatable<CampaignPublicId>
    {
        private const string Prefix = "cpub_";
        private const int HexLength = 32;
        private readonly string _value;

        private CampaignPublicId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static CampaignPublicId NewId(Odyssey.Domain.Time.UtcInstant now) => new CampaignPublicId(Prefix + Uuid7.NewHex32(now));
        public static bool TryParse(string? value, out CampaignPublicId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new CampaignPublicId(v));
        public static CampaignPublicId Parse(string value) => TryParse(value, out CampaignPublicId id) ? id : throw new FormatException("CampaignPublicId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(CampaignPublicId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is CampaignPublicId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(CampaignPublicId left, CampaignPublicId right) => left.Equals(right);
        public static bool operator !=(CampaignPublicId left, CampaignPublicId right) => !left.Equals(right);
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

    /// <summary>
    /// ADR-011 section 9.1: domain identifiers are generated by the application before
    /// being written to the database, recommended as UUIDv7 or an equivalent
    /// time-sortable 128-bit identifier. This is a local opaque identifier generator,
    /// not a gameplay RNG result (ADR-008: Guid-derived generated identifiers are
    /// explicitly permitted; only gameplay random outcomes must route through the
    /// deterministic RNG contract). The timestamp component is supplied by the caller
    /// (via UtcInstant) rather than read here, so this stays a pure function and never
    /// calls a forbidden global wall-clock API directly (ADR-008; repository policy
    /// scans every Core module package for direct global-clock use) -- callers obtain
    /// UtcInstant from the approved IWallClock port.
    /// </summary>
    internal static class Uuid7
    {
        internal static string NewHex32(Odyssey.Domain.Time.UtcInstant now)
        {
            byte[] bytes = Guid.NewGuid().ToByteArray();
            long unixMs = now.Value.ToUnixTimeMilliseconds();

            bytes[0] = (byte)(unixMs >> 40);
            bytes[1] = (byte)(unixMs >> 32);
            bytes[2] = (byte)(unixMs >> 24);
            bytes[3] = (byte)(unixMs >> 16);
            bytes[4] = (byte)(unixMs >> 8);
            bytes[5] = (byte)unixMs;
            bytes[6] = (byte)((bytes[6] & 0x0F) | 0x70); // version 7
            bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 9562 variant

            var builder = new System.Text.StringBuilder(32);
            for (int index = 0; index < bytes.Length; index++)
            {
                builder.Append(bytes[index].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
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
