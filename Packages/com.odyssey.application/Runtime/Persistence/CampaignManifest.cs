using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Odyssey.Application.Identity;
using Odyssey.Application.Results;
using Odyssey.Application.Serialization;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// ADR-011 section 5.2 mandatory manifest.json fields, plus ManifestSchemaVersion
    /// (section 5.3, mandatory as the only recommended field this task implements).
    /// </summary>
    public sealed class CampaignManifest
    {
        public const int CurrentManifestSchemaVersion = 1;

        public CampaignManifest(
            CampaignId campaignId,
            string campaignName,
            string campaignFormatVersion,
            string databaseSchemaVersion,
            string rulesetId,
            string rulesetVersion,
            UtcInstant createdAt,
            UtcInstant lastModifiedAt,
            string applicationVersionLastOpened,
            int assetManifestVersion,
            bool isTemplate,
            CampaignId? cloneSourceCampaignId = null,
            UtcInstant? lastSuccessfulBackupAt = null,
            int manifestSchemaVersion = CurrentManifestSchemaVersion)
        {
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (string.IsNullOrWhiteSpace(campaignName) || campaignName.Length > 128) throw new ArgumentException("CampaignName is not safe.", nameof(campaignName));
            if (string.IsNullOrWhiteSpace(campaignFormatVersion)) throw new ArgumentException("CampaignFormatVersion is required.", nameof(campaignFormatVersion));
            if (string.IsNullOrWhiteSpace(databaseSchemaVersion)) throw new ArgumentException("DatabaseSchemaVersion is required.", nameof(databaseSchemaVersion));
            if (string.IsNullOrWhiteSpace(rulesetId)) throw new ArgumentException("RulesetId is required.", nameof(rulesetId));
            if (string.IsNullOrWhiteSpace(rulesetVersion)) throw new ArgumentException("RulesetVersion is required.", nameof(rulesetVersion));
            if (string.IsNullOrWhiteSpace(applicationVersionLastOpened)) throw new ArgumentException("ApplicationVersionLastOpened is required.", nameof(applicationVersionLastOpened));
            if (assetManifestVersion < 1) throw new ArgumentOutOfRangeException(nameof(assetManifestVersion));
            if (manifestSchemaVersion < 1) throw new ArgumentOutOfRangeException(nameof(manifestSchemaVersion));

            CampaignId = campaignId;
            CampaignName = campaignName;
            CampaignFormatVersion = campaignFormatVersion;
            DatabaseSchemaVersion = databaseSchemaVersion;
            RulesetId = rulesetId;
            RulesetVersion = rulesetVersion;
            CreatedAt = createdAt;
            LastModifiedAt = lastModifiedAt;
            ApplicationVersionLastOpened = applicationVersionLastOpened;
            AssetManifestVersion = assetManifestVersion;
            IsTemplate = isTemplate;
            CloneSourceCampaignId = cloneSourceCampaignId;
            LastSuccessfulBackupAt = lastSuccessfulBackupAt;
            ManifestSchemaVersion = manifestSchemaVersion;
        }

        public CampaignId CampaignId { get; }
        public string CampaignName { get; }
        public string CampaignFormatVersion { get; }
        public string DatabaseSchemaVersion { get; }
        public string RulesetId { get; }
        public string RulesetVersion { get; }
        public UtcInstant CreatedAt { get; }
        public UtcInstant LastModifiedAt { get; }
        public string ApplicationVersionLastOpened { get; }
        public int AssetManifestVersion { get; }
        public bool IsTemplate { get; }
        public CampaignId? CloneSourceCampaignId { get; }
        public UtcInstant? LastSuccessfulBackupAt { get; }
        public int ManifestSchemaVersion { get; }

        public CampaignManifest WithLastModifiedAt(UtcInstant lastModifiedAt) => new CampaignManifest(
            CampaignId, CampaignName, CampaignFormatVersion, DatabaseSchemaVersion, RulesetId, RulesetVersion,
            CreatedAt, lastModifiedAt, ApplicationVersionLastOpened, AssetManifestVersion, IsTemplate,
            CloneSourceCampaignId, LastSuccessfulBackupAt, ManifestSchemaVersion);
    }

    /// <summary>
    /// Hand-written explicit codec per ADR-003 section 3 (no reflection/auto-mapping on
    /// authoritative JSON paths). manifest.json is not an event/command payload, but
    /// ADR-011 section 8.4 requires the same explicit, versioned DTO/codec discipline
    /// for any JSON persisted under this ADR or its children.
    /// </summary>
    public sealed class CampaignManifestV1Codec : IJsonContractCodec<CampaignManifest>
    {
        public static readonly ContractType Type = ContractType.Parse("odyssey.persistence.campaignmanifest");
        public JsonContractKey Key { get; } = new JsonContractKey(SerializationProfile.InterchangeJson, Type, ContractVersion.Create(1));

        public Result<JsonPayload> Write(CampaignManifest value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var stringWriter = new StringWriter(CultureInfo.InvariantCulture);
            using (JsonTextWriter writer = CreateWriter(stringWriter))
            {
                writer.WriteStartObject();
                WriteString(writer, "contractType", Type.ToString());
                WriteInt(writer, "contractVersion", 1);
                WriteInt(writer, "manifestSchemaVersion", value.ManifestSchemaVersion);
                WriteString(writer, "campaignId", value.CampaignId.ToString());
                WriteString(writer, "campaignName", value.CampaignName);
                WriteString(writer, "campaignFormatVersion", value.CampaignFormatVersion);
                WriteString(writer, "databaseSchemaVersion", value.DatabaseSchemaVersion);
                WriteString(writer, "rulesetId", value.RulesetId);
                WriteString(writer, "rulesetVersion", value.RulesetVersion);
                WriteString(writer, "createdAt", value.CreatedAt.ToString());
                WriteString(writer, "lastModifiedAt", value.LastModifiedAt.ToString());
                WriteString(writer, "applicationVersionLastOpened", value.ApplicationVersionLastOpened);
                WriteInt(writer, "assetManifestVersion", value.AssetManifestVersion);
                writer.WritePropertyName("isTemplate");
                writer.WriteValue(value.IsTemplate);
                if (value.CloneSourceCampaignId.HasValue) WriteString(writer, "cloneSourceCampaignId", value.CloneSourceCampaignId.Value.ToString());
                if (value.LastSuccessfulBackupAt.HasValue) WriteString(writer, "lastSuccessfulBackupAt", value.LastSuccessfulBackupAt.Value.ToString());
                writer.WriteEndObject();
            }

            return Result<JsonPayload>.Success(new JsonPayload(CanonicalJson.ToUtf8Bytes(stringWriter.ToString())));
        }

        public Result<CampaignManifest> Read(byte[] utf8Json)
        {
            try
            {
                if (utf8Json == null) throw new ArgumentNullException(nameof(utf8Json));
                Result structural = JsonObjectReader.ValidateJson(utf8Json, JsonPayloadLimits.ManifestBytes);
                if (structural.IsFailure) return Result<CampaignManifest>.Failure(structural.Error);

                ReadModel model = ReadModelFrom(utf8Json);
                if (model.ContractType != Type.ToString() || model.ContractVersion != 1)
                {
                    return Result<CampaignManifest>.Failure(PersistenceFailures.ManifestInvalid());
                }

                if (model.CampaignId == null || model.CampaignName == null || model.CampaignFormatVersion == null ||
                    model.DatabaseSchemaVersion == null || model.RulesetId == null || model.RulesetVersion == null ||
                    model.CreatedAt == null || model.LastModifiedAt == null || model.ApplicationVersionLastOpened == null ||
                    !model.AssetManifestVersion.HasValue || !model.IsTemplate.HasValue || !model.ManifestSchemaVersion.HasValue)
                {
                    return Result<CampaignManifest>.Failure(PersistenceFailures.ManifestInvalid());
                }

                var manifest = new CampaignManifest(
                    CampaignId.Parse(model.CampaignId),
                    model.CampaignName,
                    model.CampaignFormatVersion,
                    model.DatabaseSchemaVersion,
                    model.RulesetId,
                    model.RulesetVersion,
                    UtcInstant.Parse(model.CreatedAt),
                    UtcInstant.Parse(model.LastModifiedAt),
                    model.ApplicationVersionLastOpened,
                    model.AssetManifestVersion.Value,
                    model.IsTemplate.Value,
                    model.CloneSourceCampaignId == null ? (CampaignId?)null : CampaignId.Parse(model.CloneSourceCampaignId),
                    model.LastSuccessfulBackupAt == null ? (UtcInstant?)null : UtcInstant.Parse(model.LastSuccessfulBackupAt),
                    model.ManifestSchemaVersion.Value);

                return Result<CampaignManifest>.Success(manifest);
            }
            catch (DecoderFallbackException) { return Result<CampaignManifest>.Failure(PersistenceFailures.ManifestInvalid()); }
            catch (FormatException) { return Result<CampaignManifest>.Failure(PersistenceFailures.ManifestInvalid()); }
            catch (JsonException) { return Result<CampaignManifest>.Failure(PersistenceFailures.ManifestInvalid()); }
            catch (ArgumentException) { return Result<CampaignManifest>.Failure(PersistenceFailures.ManifestInvalid()); }
        }

        private static JsonTextWriter CreateWriter(TextWriter textWriter) => new JsonTextWriter(textWriter)
        {
            Formatting = Formatting.None,
            Culture = CultureInfo.InvariantCulture,
            FloatFormatHandling = FloatFormatHandling.Symbol,
            StringEscapeHandling = StringEscapeHandling.Default
        };

        private static void WriteString(JsonTextWriter writer, string name, string value)
        {
            writer.WritePropertyName(name);
            writer.WriteValue(value);
        }

        private static void WriteInt(JsonTextWriter writer, string name, int value)
        {
            writer.WritePropertyName(name);
            writer.WriteValue(value);
        }

        private sealed class ReadModel
        {
            public string? ContractType;
            public int ContractVersion;
            public int? ManifestSchemaVersion;
            public string? CampaignId;
            public string? CampaignName;
            public string? CampaignFormatVersion;
            public string? DatabaseSchemaVersion;
            public string? RulesetId;
            public string? RulesetVersion;
            public string? CreatedAt;
            public string? LastModifiedAt;
            public string? ApplicationVersionLastOpened;
            public int? AssetManifestVersion;
            public bool? IsTemplate;
            public string? CloneSourceCampaignId;
            public string? LastSuccessfulBackupAt;
        }

        private static ReadModel ReadModelFrom(byte[] utf8Json)
        {
            var reader = new JsonTextReader(new StringReader(new UTF8Encoding(false, true).GetString(utf8Json)))
            {
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Decimal,
                MaxDepth = JsonPayloadLimits.MaxDepth
            };
            var model = new ReadModel();
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            if (!reader.Read() || reader.TokenType != JsonToken.StartObject) throw new JsonSerializationException("Expected object.");
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject) break;
                if (reader.TokenType != JsonToken.PropertyName) throw new JsonSerializationException("Expected property name.");
                string name = (string)reader.Value!;
                if (!seen.Add(name)) throw new JsonSerializationException("Duplicate property: " + name);
                reader.Read();
                switch (name)
                {
                    case "contractType": model.ContractType = (string?)reader.Value; break;
                    case "contractVersion": model.ContractVersion = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture); break;
                    case "manifestSchemaVersion": model.ManifestSchemaVersion = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture); break;
                    case "campaignId": model.CampaignId = (string?)reader.Value; break;
                    case "campaignName": model.CampaignName = (string?)reader.Value; break;
                    case "campaignFormatVersion": model.CampaignFormatVersion = (string?)reader.Value; break;
                    case "databaseSchemaVersion": model.DatabaseSchemaVersion = (string?)reader.Value; break;
                    case "rulesetId": model.RulesetId = (string?)reader.Value; break;
                    case "rulesetVersion": model.RulesetVersion = (string?)reader.Value; break;
                    case "createdAt": model.CreatedAt = (string?)reader.Value; break;
                    case "lastModifiedAt": model.LastModifiedAt = (string?)reader.Value; break;
                    case "applicationVersionLastOpened": model.ApplicationVersionLastOpened = (string?)reader.Value; break;
                    case "assetManifestVersion": model.AssetManifestVersion = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture); break;
                    case "isTemplate": model.IsTemplate = Convert.ToBoolean(reader.Value, CultureInfo.InvariantCulture); break;
                    case "cloneSourceCampaignId": model.CloneSourceCampaignId = (string?)reader.Value; break;
                    case "lastSuccessfulBackupAt": model.LastSuccessfulBackupAt = (string?)reader.Value; break;
                    default: reader.Skip(); break;
                }
            }

            return model;
        }
    }
}
