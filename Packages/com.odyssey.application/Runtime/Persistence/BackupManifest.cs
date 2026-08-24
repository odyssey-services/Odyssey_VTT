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
    /// backup-manifest.json content (ADR-012 section 8.6 Fast/Full composition).
    /// Mirrors the BackupRecord fields that matter for a human/tool inspecting a
    /// backup folder directly, without needing the campaign's own database open.
    /// </summary>
    public sealed class BackupManifest
    {
        public const int CurrentBackupManifestSchemaVersion = 1;

        public BackupManifest(
            BackupId backupId,
            CampaignId campaignId,
            string backupKind,
            string reason,
            long campaignRevision,
            long eventSequence,
            string databaseSchemaVersion,
            string campaignFormatVersion,
            string rulesetRef,
            UtcInstant createdAt,
            string databaseHash,
            long sizeBytes,
            int backupManifestSchemaVersion = CurrentBackupManifestSchemaVersion)
        {
            if (!backupId.IsValid) throw new ArgumentException("BackupId is required.", nameof(backupId));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (string.IsNullOrWhiteSpace(backupKind)) throw new ArgumentException("BackupKind is required.", nameof(backupKind));
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason is required.", nameof(reason));
            if (string.IsNullOrWhiteSpace(databaseSchemaVersion)) throw new ArgumentException("DatabaseSchemaVersion is required.", nameof(databaseSchemaVersion));
            if (string.IsNullOrWhiteSpace(campaignFormatVersion)) throw new ArgumentException("CampaignFormatVersion is required.", nameof(campaignFormatVersion));
            if (string.IsNullOrWhiteSpace(rulesetRef)) throw new ArgumentException("RulesetRef is required.", nameof(rulesetRef));
            if (string.IsNullOrWhiteSpace(databaseHash)) throw new ArgumentException("DatabaseHash is required.", nameof(databaseHash));
            if (sizeBytes < 0) throw new ArgumentOutOfRangeException(nameof(sizeBytes));
            if (backupManifestSchemaVersion < 1) throw new ArgumentOutOfRangeException(nameof(backupManifestSchemaVersion));

            BackupId = backupId;
            CampaignId = campaignId;
            BackupKind = backupKind;
            Reason = reason;
            CampaignRevision = campaignRevision;
            EventSequence = eventSequence;
            DatabaseSchemaVersion = databaseSchemaVersion;
            CampaignFormatVersion = campaignFormatVersion;
            RulesetRef = rulesetRef;
            CreatedAt = createdAt;
            DatabaseHash = databaseHash;
            SizeBytes = sizeBytes;
            BackupManifestSchemaVersion = backupManifestSchemaVersion;
        }

        public BackupId BackupId { get; }
        public CampaignId CampaignId { get; }
        public string BackupKind { get; }
        public string Reason { get; }
        public long CampaignRevision { get; }
        public long EventSequence { get; }
        public string DatabaseSchemaVersion { get; }
        public string CampaignFormatVersion { get; }
        public string RulesetRef { get; }
        public UtcInstant CreatedAt { get; }
        public string DatabaseHash { get; }
        public long SizeBytes { get; }
        public int BackupManifestSchemaVersion { get; }
    }

    /// <summary>
    /// Hand-written explicit codec per ADR-003 section 3, matching the
    /// CampaignManifestV1Codec pattern (no reflection/auto-mapping).
    /// </summary>
    public sealed class BackupManifestV1Codec : IJsonContractCodec<BackupManifest>
    {
        public static readonly ContractType Type = ContractType.Parse("odyssey.persistence.backupmanifest");
        public JsonContractKey Key { get; } = new JsonContractKey(SerializationProfile.InterchangeJson, Type, ContractVersion.Create(1));

        public Result<JsonPayload> Write(BackupManifest value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var stringWriter = new StringWriter(CultureInfo.InvariantCulture);
            using (JsonTextWriter writer = CreateWriter(stringWriter))
            {
                writer.WriteStartObject();
                WriteString(writer, "contractType", Type.ToString());
                WriteInt(writer, "contractVersion", 1);
                WriteInt(writer, "backupManifestSchemaVersion", value.BackupManifestSchemaVersion);
                WriteString(writer, "backupId", value.BackupId.ToString());
                WriteString(writer, "campaignId", value.CampaignId.ToString());
                WriteString(writer, "backupKind", value.BackupKind);
                WriteString(writer, "reason", value.Reason);
                WriteLong(writer, "campaignRevision", value.CampaignRevision);
                WriteLong(writer, "eventSequence", value.EventSequence);
                WriteString(writer, "databaseSchemaVersion", value.DatabaseSchemaVersion);
                WriteString(writer, "campaignFormatVersion", value.CampaignFormatVersion);
                WriteString(writer, "rulesetRef", value.RulesetRef);
                WriteString(writer, "createdAt", value.CreatedAt.ToString());
                WriteString(writer, "databaseHash", value.DatabaseHash);
                WriteLong(writer, "sizeBytes", value.SizeBytes);
                writer.WriteEndObject();
            }

            return Result<JsonPayload>.Success(new JsonPayload(CanonicalJson.ToUtf8Bytes(stringWriter.ToString())));
        }

        public Result<BackupManifest> Read(byte[] utf8Json)
        {
            try
            {
                if (utf8Json == null) throw new ArgumentNullException(nameof(utf8Json));
                Result structural = JsonObjectReader.ValidateJson(utf8Json, JsonPayloadLimits.ManifestBytes);
                if (structural.IsFailure) return Result<BackupManifest>.Failure(structural.Error);

                ReadModel model = ReadModelFrom(utf8Json);
                if (model.ContractType != Type.ToString() || model.ContractVersion != 1)
                {
                    return Result<BackupManifest>.Failure(PersistenceFailures.ManifestInvalid());
                }

                if (model.BackupId == null || model.CampaignId == null || model.BackupKind == null || model.Reason == null ||
                    model.DatabaseSchemaVersion == null || model.CampaignFormatVersion == null || model.RulesetRef == null ||
                    model.CreatedAt == null || model.DatabaseHash == null || !model.CampaignRevision.HasValue ||
                    !model.EventSequence.HasValue || !model.SizeBytes.HasValue || !model.BackupManifestSchemaVersion.HasValue)
                {
                    return Result<BackupManifest>.Failure(PersistenceFailures.ManifestInvalid());
                }

                var manifest = new BackupManifest(
                    BackupId.Parse(model.BackupId),
                    CampaignId.Parse(model.CampaignId),
                    model.BackupKind,
                    model.Reason,
                    model.CampaignRevision.Value,
                    model.EventSequence.Value,
                    model.DatabaseSchemaVersion,
                    model.CampaignFormatVersion,
                    model.RulesetRef,
                    UtcInstant.Parse(model.CreatedAt),
                    model.DatabaseHash,
                    model.SizeBytes.Value,
                    model.BackupManifestSchemaVersion.Value);

                return Result<BackupManifest>.Success(manifest);
            }
            catch (DecoderFallbackException) { return Result<BackupManifest>.Failure(PersistenceFailures.ManifestInvalid()); }
            catch (FormatException) { return Result<BackupManifest>.Failure(PersistenceFailures.ManifestInvalid()); }
            catch (JsonException) { return Result<BackupManifest>.Failure(PersistenceFailures.ManifestInvalid()); }
            catch (ArgumentException) { return Result<BackupManifest>.Failure(PersistenceFailures.ManifestInvalid()); }
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
            public int? BackupManifestSchemaVersion;
            public string? BackupId;
            public string? CampaignId;
            public string? BackupKind;
            public string? Reason;
            public long? CampaignRevision;
            public long? EventSequence;
            public string? DatabaseSchemaVersion;
            public string? CampaignFormatVersion;
            public string? RulesetRef;
            public string? CreatedAt;
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
                    case "backupManifestSchemaVersion": model.BackupManifestSchemaVersion = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture); break;
                    case "backupId": model.BackupId = (string?)reader.Value; break;
                    case "campaignId": model.CampaignId = (string?)reader.Value; break;
                    case "backupKind": model.BackupKind = (string?)reader.Value; break;
                    case "reason": model.Reason = (string?)reader.Value; break;
                    case "campaignRevision": model.CampaignRevision = Convert.ToInt64(reader.Value, CultureInfo.InvariantCulture); break;
                    case "eventSequence": model.EventSequence = Convert.ToInt64(reader.Value, CultureInfo.InvariantCulture); break;
                    case "databaseSchemaVersion": model.DatabaseSchemaVersion = (string?)reader.Value; break;
                    case "campaignFormatVersion": model.CampaignFormatVersion = (string?)reader.Value; break;
                    case "rulesetRef": model.RulesetRef = (string?)reader.Value; break;
                    case "createdAt": model.CreatedAt = (string?)reader.Value; break;
                    case "databaseHash": model.DatabaseHash = (string?)reader.Value; break;
                    case "sizeBytes": model.SizeBytes = Convert.ToInt64(reader.Value, CultureInfo.InvariantCulture); break;
                    default: reader.Skip(); break;
                }
            }

            return model;
        }
    }
}
