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

        /// <summary>
        /// ODY-S04-101: added alongside SceneId/TokenId/AssetId/BackupId's own
        /// existing NewId factory -- Character is now an aggregate root minting
        /// its own identity at creation time (ADR-022 section 4), the same
        /// pattern those sibling identifiers already established.
        /// </summary>
        public static CharacterId NewId(Odyssey.Domain.Time.UtcInstant now) => new CharacterId(Prefix + Uuid7.NewHex32(now));
        public static bool TryParse(string? value, out CharacterId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new CharacterId(v));
        public static CharacterId Parse(string value) => TryParse(value, out CharacterId id) ? id : throw new FormatException("CharacterId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(CharacterId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is CharacterId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(CharacterId left, CharacterId right) => left.Equals(right);
        public static bool operator !=(CharacterId left, CharacterId right) => !left.Equals(right);
    }

    /// <summary>
    /// ODY-S04-103: identifies one <c>CharacterTemplate</c> aggregate row
    /// (ADR-023 section 5.1) -- the single aggregate type shared by
    /// <c>PersonalCharacterTemplate</c>/<c>CampaignCharacterTemplate</c>,
    /// distinguished only by <c>TemplateScope</c>, not by identifier prefix.
    /// </summary>
    public readonly struct CharacterTemplateId : IEquatable<CharacterTemplateId>
    {
        private const string Prefix = "tmpl_";
        private const int HexLength = 32;
        private readonly string _value;

        private CharacterTemplateId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static CharacterTemplateId NewId(Odyssey.Domain.Time.UtcInstant now) => new CharacterTemplateId(Prefix + Uuid7.NewHex32(now));
        public static bool TryParse(string? value, out CharacterTemplateId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new CharacterTemplateId(v));
        public static CharacterTemplateId Parse(string value) => TryParse(value, out CharacterTemplateId id) ? id : throw new FormatException("CharacterTemplateId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(CharacterTemplateId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is CharacterTemplateId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(CharacterTemplateId left, CharacterTemplateId right) => left.Equals(right);
        public static bool operator !=(CharacterTemplateId left, CharacterTemplateId right) => !left.Equals(right);
    }

    /// <summary>
    /// ODY-S04-103: identifies a local, pre-campaign-binding Draft (ADR-023
    /// section 4.1). Deliberately a distinct prefix/identifier type from
    /// <see cref="CharacterId"/> -- a local Draft is never an ADR-022
    /// Character aggregate instance and never carries a <see cref="CharacterId"/>
    /// before <c>BindDraftToCampaign</c>.
    /// </summary>
    public readonly struct LocalCharacterDraftId : IEquatable<LocalCharacterDraftId>
    {
        private const string Prefix = "draft_";
        private const int HexLength = 32;
        private readonly string _value;

        private LocalCharacterDraftId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static LocalCharacterDraftId NewId(Odyssey.Domain.Time.UtcInstant now) => new LocalCharacterDraftId(Prefix + Uuid7.NewHex32(now));
        public static bool TryParse(string? value, out LocalCharacterDraftId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new LocalCharacterDraftId(v));
        public static LocalCharacterDraftId Parse(string value) => TryParse(value, out LocalCharacterDraftId id) ? id : throw new FormatException("LocalCharacterDraftId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(LocalCharacterDraftId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is LocalCharacterDraftId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(LocalCharacterDraftId left, LocalCharacterDraftId right) => left.Equals(right);
        public static bool operator !=(LocalCharacterDraftId left, LocalCharacterDraftId right) => !left.Equals(right);
    }

    /// <summary>
    /// ODY-S04-103: identifies one nested seed entry inside a
    /// <c>CharacterTemplate</c>'s seed data (template-scoped), or one freshly
    /// minted nested instance produced by
    /// <c>CharacterTemplateSeedCopier.CopyWithFreshIdentifiers</c>
    /// (Character/Draft-scoped) -- ADR-023 section 5.3's "mints a fresh
    /// identifier for every nested instance." The same identifier shape is
    /// reused for both the source and the copy; only the value differs.
    /// </summary>
    public readonly struct TemplateSeedItemId : IEquatable<TemplateSeedItemId>
    {
        private const string Prefix = "seed_";
        private const int HexLength = 32;
        private readonly string _value;

        private TemplateSeedItemId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static TemplateSeedItemId NewId(Odyssey.Domain.Time.UtcInstant now) => new TemplateSeedItemId(Prefix + Uuid7.NewHex32(now));
        public static bool TryParse(string? value, out TemplateSeedItemId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new TemplateSeedItemId(v));
        public static TemplateSeedItemId Parse(string value) => TryParse(value, out TemplateSeedItemId id) ? id : throw new FormatException("TemplateSeedItemId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(TemplateSeedItemId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is TemplateSeedItemId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(TemplateSeedItemId left, TemplateSeedItemId right) => left.Equals(right);
        public static bool operator !=(TemplateSeedItemId left, TemplateSeedItemId right) => !left.Equals(right);
    }

    /// <summary>
    /// ODY-S04-104: identifies one <c>CharacterReviewComment</c> (ADR-023
    /// section 7.1/product section 8.4). A conflict-free append -- the
    /// aggregate whose thread it belongs to is addressed by
    /// <see cref="CharacterId"/>, not this identifier.
    /// </summary>
    public readonly struct CharacterReviewCommentId : IEquatable<CharacterReviewCommentId>
    {
        private const string Prefix = "cmnt_";
        private const int HexLength = 32;
        private readonly string _value;

        private CharacterReviewCommentId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static CharacterReviewCommentId NewId(Odyssey.Domain.Time.UtcInstant now) => new CharacterReviewCommentId(Prefix + Uuid7.NewHex32(now));
        public static bool TryParse(string? value, out CharacterReviewCommentId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new CharacterReviewCommentId(v));
        public static CharacterReviewCommentId Parse(string value) => TryParse(value, out CharacterReviewCommentId id) ? id : throw new FormatException("CharacterReviewCommentId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(CharacterReviewCommentId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is CharacterReviewCommentId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(CharacterReviewCommentId left, CharacterReviewCommentId right) => left.Equals(right);
        public static bool operator !=(CharacterReviewCommentId left, CharacterReviewCommentId right) => !left.Equals(right);
    }

    /// <summary>
    /// ODY-S04-105: identifies one <c>DevelopmentTransaction</c> ledger row
    /// (ADR-024 section 3.2/4.3, product section 12.1). The row is a
    /// rebuildable ledger projection, not a <c>DomainEvent</c> itself -- this
    /// identifier only names one such row, not an event.
    /// </summary>
    public readonly struct DevelopmentTransactionId : IEquatable<DevelopmentTransactionId>
    {
        private const string Prefix = "dtxn_";
        private const int HexLength = 32;
        private readonly string _value;

        private DevelopmentTransactionId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static DevelopmentTransactionId NewId(Odyssey.Domain.Time.UtcInstant now) => new DevelopmentTransactionId(Prefix + Uuid7.NewHex32(now));
        public static bool TryParse(string? value, out DevelopmentTransactionId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new DevelopmentTransactionId(v));
        public static DevelopmentTransactionId Parse(string value) => TryParse(value, out DevelopmentTransactionId id) ? id : throw new FormatException("DevelopmentTransactionId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(DevelopmentTransactionId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is DevelopmentTransactionId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(DevelopmentTransactionId left, DevelopmentTransactionId right) => left.Equals(right);
        public static bool operator !=(DevelopmentTransactionId left, DevelopmentTransactionId right) => !left.Equals(right);
    }

    /// <summary>
    /// ODY-S01-008 minimal domain model. Full Scene aggregate fields (BoardId,
    /// LayerDefinitions, FogSettings, PermissionOverrides, etc. per
    /// 03_Domain_Model section 10.1) are not implemented by this identifier or the
    /// task that introduces it -- only identity, per ADR-011 section 9.1.
    /// </summary>
    public readonly struct SceneId : IEquatable<SceneId>
    {
        private const string Prefix = "scn_";
        private const int HexLength = 32;
        private readonly string _value;

        private SceneId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static SceneId NewId(Odyssey.Domain.Time.UtcInstant now) => new SceneId(Prefix + Uuid7.NewHex32(now));
        public static bool TryParse(string? value, out SceneId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new SceneId(v));
        public static SceneId Parse(string value) => TryParse(value, out SceneId id) ? id : throw new FormatException("SceneId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(SceneId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is SceneId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(SceneId left, SceneId right) => left.Equals(right);
        public static bool operator !=(SceneId left, SceneId right) => !left.Equals(right);
    }

    /// <summary>
    /// ODY-S01-008 minimal Token identity. Full SceneObject/TokenComponent fields
    /// (footprint, facing, layer, components, per 03_Domain_Model sections 10.6-10.8)
    /// are not implemented by this identifier or the task that introduces it --
    /// only identity and a bare position, per ADR-011 section 9.1.
    /// </summary>
    public readonly struct TokenId : IEquatable<TokenId>
    {
        private const string Prefix = "tok_";
        private const int HexLength = 32;
        private readonly string _value;

        private TokenId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static TokenId NewId(Odyssey.Domain.Time.UtcInstant now) => new TokenId(Prefix + Uuid7.NewHex32(now));
        public static bool TryParse(string? value, out TokenId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new TokenId(v));
        public static TokenId Parse(string value) => TryParse(value, out TokenId id) ? id : throw new FormatException("TokenId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(TokenId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is TokenId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(TokenId left, TokenId right) => left.Equals(right);
        public static bool operator !=(TokenId left, TokenId right) => !left.Equals(right);
    }

    /// <summary>
    /// Identifies a row in the AssetManifestEntries system table (ADR-011 section
    /// 8.2, created by ODY-S01-007). Not the full asset pipeline (staging,
    /// quarantine, thumbnails) -- just identity for the minimal "one imported test
    /// map" need of ODY-S01-008.
    /// </summary>
    public readonly struct AssetId : IEquatable<AssetId>
    {
        private const string Prefix = "asst_";
        private const int HexLength = 32;
        private readonly string _value;

        private AssetId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static AssetId NewId(Odyssey.Domain.Time.UtcInstant now) => new AssetId(Prefix + Uuid7.NewHex32(now));
        public static bool TryParse(string? value, out AssetId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new AssetId(v));
        public static AssetId Parse(string value) => TryParse(value, out AssetId id) ? id : throw new FormatException("AssetId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(AssetId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is AssetId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(AssetId left, AssetId right) => left.Equals(right);
        public static bool operator !=(AssetId left, AssetId right) => !left.Equals(right);
    }

    /// <summary>
    /// ODY-S01-011 Backups: identifies one BackupRecord (ADR-012 section 8.7).
    /// </summary>
    public readonly struct BackupId : IEquatable<BackupId>
    {
        private const string Prefix = "bkup_";
        private const int HexLength = 32;
        private readonly string _value;

        private BackupId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static BackupId NewId(Odyssey.Domain.Time.UtcInstant now) => new BackupId(Prefix + Uuid7.NewHex32(now));
        public static bool TryParse(string? value, out BackupId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new BackupId(v));
        public static BackupId Parse(string value) => TryParse(value, out BackupId id) ? id : throw new FormatException("BackupId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(BackupId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is BackupId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(BackupId left, BackupId right) => left.Equals(right);
        public static bool operator !=(BackupId left, BackupId right) => !left.Equals(right);
    }

    /// <summary>
    /// ODY-S02-001 Transport Abstraction: identifies one NetworkEnvelope
    /// (06_Networking_and_Session_Sync section 11.1). Generated fresh by the
    /// sender at send time, unlike SessionId/UserId which are externally assigned.
    /// </summary>
    public readonly struct MessageId : IEquatable<MessageId>
    {
        private const string Prefix = "msg_";
        private const int HexLength = 32;
        private readonly string _value;

        private MessageId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static MessageId NewId(Odyssey.Domain.Time.UtcInstant now) => new MessageId(Prefix + Uuid7.NewHex32(now));
        public static bool TryParse(string? value, out MessageId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new MessageId(v));
        public static MessageId Parse(string value) => TryParse(value, out MessageId id) ? id : throw new FormatException("MessageId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(MessageId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is MessageId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(MessageId left, MessageId right) => left.Equals(right);
        public static bool operator !=(MessageId left, MessageId right) => !left.Equals(right);
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
