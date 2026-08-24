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
    /// export-manifest.json content (05_Persistence section 27.1/27.2).
    /// Mirrors BackupManifest's shape -- an export is, at its database-copy
    /// core, the same kind of consistent snapshot a backup is (ODY-S01-012
    /// reuses SqliteSnapshotCopy, the same helper ODY-S01-011's CreateBackup
    /// uses) -- plus the application version that produced the export, which a
    /// future importer can use for diagnostics even though this task does not
    /// implement any migration-on-import behavior.
    /// </summary>
    public sealed class ExportManifest
    {
        public const int CurrentExportManifestSchemaVersion = 1;

        public ExportManifest(
            CampaignId campaignId,
            long campaignRevision,
            long eventSequence,
            string databaseSchemaVersion,
            string campaignFormatVersion,
            string rulesetRef,
            UtcInstant createdAt,
            string applicationVersion,
            string databaseHash,
            long sizeBytes,
            int exportManifestSchemaVersion = CurrentExportManifestSchemaVersion)
        {
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (string.IsNullOrWhiteSpace(databaseSchemaVersion)) throw new ArgumentException("DatabaseSchemaVersion is required.", nameof(databaseSchemaVersion));
            if (string.IsNullOrWhiteSpace(campaignFormatVersion)) throw new ArgumentException("CampaignFormatVersion is required.", nameof(campaignFormatVersion));
            if (string.IsNullOrWhiteSpace(rulesetRef)) throw new ArgumentException("RulesetRef is required.", nameof(rulesetRef));
            if (string.IsNullOrWhiteSpace(applicationVersion)) throw new ArgumentException("ApplicationVersion is required.", nameof(applicationVersion));
            if (string.IsNullOrWhiteSpace(databaseHash)) throw new ArgumentException("DatabaseHash is required.", nameof(databaseHash));
            if (sizeBytes < 0) throw new ArgumentOutOfRangeException(nameof(sizeBytes));
            if (exportManifestSchemaVersion < 1) throw new ArgumentOutOfRangeException(nameof(exportManifestSchemaVersion));

            CampaignId = campaignId;
            CampaignRevision = campaignRevision;
            EventSequence = eventSequence;
            DatabaseSchemaVersion = databaseSchemaVersion;
            CampaignFormatVersion = campaignFormatVersion;
            RulesetRef = rulesetRef;
            CreatedAt = createdAt;
            ApplicationVersion = applicationVersion;
            DatabaseHash = databaseHash;
            SizeBytes = sizeBytes;
            ExportManifestSchemaVersion = exportManifestSchemaVersion;
        }

        public CampaignId CampaignId { get; }
        public long CampaignRevision { get; }
        public long EventSequence { get; }
        public string DatabaseSchemaVersion { get; }
        public string CampaignFormatVersion { get; }
        public string RulesetRef { get; }
        public UtcInstant CreatedAt { get; }
        public string ApplicationVersion { get; }
        public string DatabaseHash { get; }
        public long SizeBytes { get; }
        public int ExportManifestSchemaVersion { get; }
    }

    /// <summary>
    /// Hand-written explicit codec per ADR-003 section 3, matching the
    /// CampaignManifestV1Codec/BackupManifestV1Codec pattern.
    /// </summary>
    public sealed class ExportManifestV1Codec : IJsonContractCodec<ExportManifest>
    {
        public static readonly ContractType Type = ContractType.Parse("odyssey.persistence.exportmanifest");
        public JsonContractKey Key { get; } = new JsonContractKey(SerializationProfile.InterchangeJson, Type, ContractVersion.Create(1));

        public Result<JsonPayload> Write(ExportManifest value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var stringWriter = new StringWriter(CultureInfo.InvariantCulture);
            using (JsonTextWriter writer = CreateWriter(stringWriter))
            {
                writer.WriteStartObject();
                WriteString(writer, "contractType", Type.ToString());
                WriteInt(writer, "contractVersion", 1);
                WriteInt(writer, "exportManifestSchemaVersion", value.ExportManifestSchemaVersion);
                WriteString(writer, "campaignId", value.CampaignId.ToString());
                WriteLong(writer, "campaignRevision", value.CampaignRevision);
                WriteLong(writer, "eventSequence", value.EventSequence);
                WriteString(writer, "databaseSchemaVersion", value.DatabaseSchemaVersion);
                WriteString(writer, "campaignFormatVersion", value.CampaignFormatVersion);
                WriteString(writer, "rulesetRef", value.RulesetRef);
                WriteString(writer, "createdAt", value.CreatedAt.ToString());
                WriteString(writer, "applicationVersion", value.ApplicationVersion);
                WriteString(writer, "databaseHash", value.DatabaseHash);
                WriteLong(writer, "sizeBytes", value.SizeBytes);
                writer.WriteEndObject();
            }

            return Result<JsonPayload>.Success(new JsonPayload(CanonicalJson.ToUtf8Bytes(stringWriter.ToString())));
        }

        public Result<ExportManifest> Read(byte[] utf8Json)
        {
            try
            {
                if (utf8Json == null) throw new ArgumentNullException(nameof(utf8Json));
                Result structural = JsonObjectReader.ValidateJson(utf8Json, JsonPayloadLimits.ManifestBytes);
                if (structural.IsFailure) return Result<ExportManifest>.Failure(structural.Error);

                ReadModel model = ReadModelFrom(utf8Json);
                if (model.ContractType != Type.ToString() || model.ContractVersion != 1)
                {
                    return Result<ExportManifest>.Failure(PersistenceFailures.ManifestInvalid());
                }

                if (model.CampaignId == null || model.DatabaseSchemaVersion == null || model.CampaignFormatVersion == null ||
                    model.RulesetRef == null || model.CreatedAt == null || model.ApplicationVersion == null || model.DatabaseHash == null ||
                    !model.CampaignRevision.HasValue || !model.EventSequence.HasValue || !model.SizeBytes.HasValue || !model.ExportManifestSchemaVersion.HasValue)
                {
                    return Result<ExportManifest>.Failure(PersistenceFailures.ManifestInvalid());
                }

                var manifest = new ExportManifest(
                    CampaignId.Parse(model.CampaignId),
                    model.CampaignRevision.Value,
                    model.EventSequence.Value,
                    model.DatabaseSchemaVersion,
                    model.CampaignFormatVersion,
                    model.RulesetRef,
                    UtcInstant.Parse(model.CreatedAt),
                    model.ApplicationVersion,
                    model.DatabaseHash,
                    model.SizeBytes.Value,
                    model.ExportManifestSchemaVersion.Value);

                return Result<ExportManifest>.Success(manifest);
            }
            catch (DecoderFallbackException) { return Result<ExportManifest>.Failure(PersistenceFailures.ManifestInvalid()); }
            catch (FormatException) { return Result<ExportManifest>.Failure(PersistenceFailures.ManifestInvalid()); }
            catch (JsonException) { return Result<ExportManifest>.Failure(PersistenceFailures.ManifestInvalid()); }
            catch (ArgumentException) { return Result<ExportManifest>.Failure(PersistenceFailures.ManifestInvalid()); }
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

        private static void WriteLong(JsonTextWriter writer, string name, long value)
        {
            writer.WritePropertyName(name);
            writer.WriteValue(value);
        }

        private sealed class ReadModel
        {
            public string? ContractType;
            public int ContractVersion;
            public int? ExportManifestSchemaVersion;
            public string? CampaignId;
            public long? CampaignRevision;
            public long? EventSequence;
            public string? DatabaseSchemaVersion;
            public string? CampaignFormatVersion;
            public string? RulesetRef;
            public string? CreatedAt;
            public string? ApplicationVersion;
            public string? DatabaseHash;
            public long? SizeBytes;
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
                    case "exportManifestSchemaVersion": model.ExportManifestSchemaVersion = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture); break;
                    case "campaignId": model.CampaignId = (string?)reader.Value; break;
                    case "campaignRevision": model.CampaignRevision = Convert.ToInt64(reader.Value, CultureInfo.InvariantCulture); break;
                    case "eventSequence": model.EventSequence = Convert.ToInt64(reader.Value, CultureInfo.InvariantCulture); break;
                    case "databaseSchemaVersion": model.DatabaseSchemaVersion = (string?)reader.Value; break;
                    case "campaignFormatVersion": model.CampaignFormatVersion = (string?)reader.Value; break;
                    case "rulesetRef": model.RulesetRef = (string?)reader.Value; break;
                    case "createdAt": model.CreatedAt = (string?)reader.Value; break;
                    case "applicationVersion": model.ApplicationVersion = (string?)reader.Value; break;
                    case "databaseHash": model.DatabaseHash = (string?)reader.Value; break;
                    case "sizeBytes": model.SizeBytes = Convert.ToInt64(reader.Value, CultureInfo.InvariantCulture); break;
                    default: reader.Skip(); break;
                }
            }

            return model;
        }
    }
}
