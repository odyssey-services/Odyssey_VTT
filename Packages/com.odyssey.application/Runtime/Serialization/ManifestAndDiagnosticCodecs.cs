using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Odyssey.Application.Commands;
using Odyssey.Application.Diagnostics;
using Odyssey.Application.Identity;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Serialization
{
    public sealed class OdcampManifestV1
    {
        public OdcampManifestV1(string manifestId, string displayName, string relativeAssetPath)
        {
            if (!SerializationText.IsLowerToken(manifestId, 64)) throw new ArgumentException("ManifestId is not canonical.", nameof(manifestId));
            if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 96 || displayName.Trim() != displayName) throw new ArgumentException("DisplayName is not safe.", nameof(displayName));
            if (!IsSafeRelativePath(relativeAssetPath)) throw new ArgumentException("Relative asset path is not safe.", nameof(relativeAssetPath));
            ManifestId = manifestId;
            DisplayName = displayName;
            RelativeAssetPath = relativeAssetPath.Replace('\\', '/');
        }

        public string ManifestId { get; }
        public string DisplayName { get; }
        public string RelativeAssetPath { get; }

        public static bool IsSafeRelativePath(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            string path = value!.Replace('\\', '/');
            if (path.Length > 160 || path.StartsWith("/", StringComparison.Ordinal) || path.StartsWith("~", StringComparison.Ordinal)) return false;
            if (path.Contains("://") || path.Contains("../") || path.Contains("/..") || path == "..") return false;
            if (path.Length >= 2 && ((path[0] >= 'A' && path[0] <= 'Z') || (path[0] >= 'a' && path[0] <= 'z')) && path[1] == ':') return false;
            string[] segments = path.Split('/');
            for (int index = 0; index < segments.Length; index++)
            {
                if (!SerializationText.IsLowerToken(segments[index], 64)) return false;
            }

            return true;
        }
    }

    public sealed class OdcampManifestV1Codec : IJsonContractCodec<OdcampManifestV1>
    {
        public static readonly ContractType Type = ContractType.Parse("odyssey.odcamp.manifest");
        public JsonContractKey Key { get; } = new JsonContractKey(SerializationProfile.InterchangeJson, Type, ContractVersion.Create(1));

        public Result<JsonPayload> Write(OdcampManifestV1 value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            JsonPayload payload = new CanonicalJsonWriter().StartObject()
                .String("contractType", Type.ToString())
                .Int32("contractVersion", 1)
                .String("manifestId", value.ManifestId)
                .String("displayName", value.DisplayName)
                .String("relativeAssetPath", value.RelativeAssetPath)
                .EndObject()
                .ToPayload();
            return Result<JsonPayload>.Success(payload);
        }

        public Result<OdcampManifestV1> Read(byte[] utf8Json)
        {
            Result<JsonObjectReader> reader = JsonObjectReader.Read(utf8Json, JsonPayloadLimits.ManifestBytes);
            if (reader.IsFailure) return Result<OdcampManifestV1>.Failure(reader.Error);
            Result schema = reader.Value.EnsureOnly("contractType", "contractVersion", "manifestId", "displayName", "relativeAssetPath");
            if (schema.IsFailure) return Result<OdcampManifestV1>.Failure(schema.Error);
            Result<string> type = reader.Value.RequiredString("contractType");
            Result<int> version = reader.Value.RequiredInt32("contractVersion");
            Result<string> manifestId = reader.Value.RequiredString("manifestId");
            Result<string> displayName = reader.Value.RequiredString("displayName");
            Result<string> relativePath = reader.Value.RequiredString("relativeAssetPath");
            if (type.IsFailure) return Result<OdcampManifestV1>.Failure(type.Error);
            if (version.IsFailure) return Result<OdcampManifestV1>.Failure(version.Error);
            if (manifestId.IsFailure) return Result<OdcampManifestV1>.Failure(manifestId.Error);
            if (displayName.IsFailure) return Result<OdcampManifestV1>.Failure(displayName.Error);
            if (relativePath.IsFailure) return Result<OdcampManifestV1>.Failure(relativePath.Error);
            if (type.Value != Type.ToString() || version.Value != 1) return Result<OdcampManifestV1>.Failure(SerializationFailures.UnsupportedContract());
            try
            {
                return Result<OdcampManifestV1>.Success(new OdcampManifestV1(manifestId.Value, displayName.Value, relativePath.Value));
            }
            catch (ArgumentException)
            {
                return Result<OdcampManifestV1>.Failure(SerializationFailures.InvalidPayload());
            }
        }
    }

    public sealed class LogEventV1JsonCodec : IJsonContractCodec<LogEventV1>
    {
        public static readonly ContractType Type = ContractType.Parse("odyssey.diagnostics.log.event");
        public JsonContractKey Key { get; } = new JsonContractKey(SerializationProfile.DiagnosticJson, Type, ContractVersion.Create(1));

        public Result<JsonPayload> Write(LogEventV1 value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            StringWriter stringWriter = new StringWriter(CultureInfo.InvariantCulture);
            using (JsonTextWriter writer = CreateWriter(stringWriter))
            {
                writer.WriteStartObject();
                WriteString(writer, "contractType", Type.ToString());
                WriteInt(writer, "contractVersion", LogEventV1.SchemaVersionValue);
                WriteString(writer, "timestampUtc", value.TimestampUtc.ToString());
                WriteString(writer, "level", ToLogLevelToken(value.Level));
                WriteString(writer, "eventCode", value.EventCode.ToString());
                WriteString(writer, "subsystem", value.Subsystem.ToString());
                WriteString(writer, "buildIdAvailability", ToBuildIdAvailabilityToken(value.BuildIdAvailability));
                WriteString(writer, "processInstanceId", value.ProcessInstanceId.ToString());
                if (value.CorrelationId.HasValue) WriteString(writer, "correlationId", value.CorrelationId.Value.ToString());
                if (value.DiagnosticId.HasValue) WriteString(writer, "diagnosticId", value.DiagnosticId.Value.ToString());
                if (value.CommandId.HasValue) WriteString(writer, "commandId", value.CommandId.Value.ToString());
                if (value.SessionReference.HasValue) WriteString(writer, "sessionReference", value.SessionReference.Value.ToString());
                WriteString(writer, "messageTemplateKey", value.MessageTemplateKey.ToString());
                WriteProperties(writer, value.SafeProperties);
                if (value.ExceptionSummary.HasValue) WriteException(writer, value.ExceptionSummary.Value);
                writer.WriteEndObject();
            }

            return Result<JsonPayload>.Success(new JsonPayload(CanonicalJson.ToUtf8Bytes(stringWriter.ToString())));
        }

        public Result<LogEventV1> Read(byte[] utf8Json)
        {
            try
            {
                if (utf8Json == null) throw new ArgumentNullException(nameof(utf8Json));
                Result structural = JsonObjectReader.ValidateJson(utf8Json, JsonPayloadLimits.DiagnosticRecordBytes);
                if (structural.IsFailure) return Result<LogEventV1>.Failure(structural.Error);
                DiagnosticReadModel model = ReadDiagnosticModel(utf8Json);
                if (model.ContractType != Type.ToString() || model.ContractVersion != LogEventV1.SchemaVersionValue) return Result<LogEventV1>.Failure(SerializationFailures.UnsupportedContract());

                LogEventV1 logEvent = new LogEventV1(
                    UtcInstant.Parse(model.TimestampUtc!),
                    ParseLogLevelToken(model.Level!),
                    EventCode.Parse(model.EventCode!),
                    SubsystemName.Parse(model.Subsystem!),
                    ParseBuildIdAvailabilityToken(model.BuildIdAvailability!),
                    ProcessInstanceId.Parse(model.ProcessInstanceId!),
                    MessageTemplateKey.Parse(model.MessageTemplateKey!),
                    model.SafeProperties,
                    model.CorrelationId == null ? (CorrelationId?)null : CorrelationId.Parse(model.CorrelationId),
                    model.DiagnosticId == null ? (DiagnosticId?)null : DiagnosticId.Parse(model.DiagnosticId),
                    model.CommandId == null ? (CommandId?)null : CommandId.Parse(model.CommandId),
                    model.SessionReference == null ? (SessionReference?)null : SessionReference.Parse(model.SessionReference),
                    model.ExceptionSummary);
                Result registry = EventCodeRegistry.CreateDefault().Validate(logEvent);
                return registry.IsFailure ? Result<LogEventV1>.Failure(registry.Error) : Result<LogEventV1>.Success(logEvent);
            }
            catch (DecoderFallbackException)
            {
                return Result<LogEventV1>.Failure(SerializationFailures.InvalidPayload());
            }
            catch (FormatException)
            {
                return Result<LogEventV1>.Failure(SerializationFailures.InvalidPayload());
            }
            catch (JsonException)
            {
                return Result<LogEventV1>.Failure(SerializationFailures.InvalidPayload());
            }
            catch (ArgumentException)
            {
                return Result<LogEventV1>.Failure(SerializationFailures.InvalidPayload());
            }
        }

        private static JsonTextWriter CreateWriter(TextWriter textWriter)
        {
            return new JsonTextWriter(textWriter)
            {
                Formatting = Formatting.None,
                Culture = CultureInfo.InvariantCulture,
                FloatFormatHandling = FloatFormatHandling.Symbol,
                StringEscapeHandling = StringEscapeHandling.Default
            };
        }

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

        private static void WriteProperties(JsonTextWriter writer, IReadOnlyList<SafeLogProperty> properties)
        {
            writer.WritePropertyName("safeProperties");
            writer.WriteStartArray();
            for (int index = 0; index < properties.Count; index++)
            {
                SafeLogProperty property = properties[index];
                writer.WriteStartObject();
                WriteString(writer, "key", property.Key.ToString());
                WriteString(writer, "classification", ToClassificationToken(property.Value.Classification));
                WriteString(writer, "valueKind", ToValueKindToken(property.Value.ValueKind));
                WriteString(writer, "renderedValue", property.Value.RenderedValue);
                WriteInt(writer, "logicalSize", property.Value.LogicalSize);
                WriteInt(writer, "originalScalarCount", property.Value.OriginalScalarCount);
                writer.WritePropertyName("wasTruncated");
                writer.WriteValue(property.Value.WasTruncated);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteException(JsonTextWriter writer, ExceptionSummary exception)
        {
            writer.WritePropertyName("exceptionSummary");
            writer.WriteStartObject();
            WriteString(writer, "category", ToExceptionCategoryToken(exception.Category));
            WriteString(writer, "subsystem", exception.Subsystem.ToString());
            WriteInt(writer, "innerExceptionCount", exception.InnerExceptionCount);
            writer.WritePropertyName("isTransient");
            writer.WriteValue(exception.IsTransient);
            WriteString(writer, "diagnosticId", exception.DiagnosticId.ToString());
            writer.WriteEndObject();
        }

        private static DiagnosticReadModel ReadDiagnosticModel(byte[] utf8Json)
        {
            JsonTextReader reader = new JsonTextReader(new StringReader(new UTF8Encoding(false, true).GetString(utf8Json)))
            {
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Decimal,
                MaxDepth = JsonPayloadLimits.MaxDepth
            };
            DiagnosticReadModel model = new DiagnosticReadModel();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            if (!reader.Read() || reader.TokenType != JsonToken.StartObject) throw new JsonSerializationException("Expected object.");
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                {
                    if (reader.Read()) throw new JsonSerializationException("Trailing token.");
                    model.ValidateRequired();
                    return model;
                }

                if (reader.TokenType != JsonToken.PropertyName) throw new JsonSerializationException("Expected property.");
                string name = (string)reader.Value!;
                if (!seen.Add(name)) throw new JsonSerializationException("Duplicate property.");
                if (!reader.Read()) throw new JsonSerializationException("Missing value.");
                switch (name)
                {
                    case "contractType": model.ContractType = ReadStringValue(reader); break;
                    case "contractVersion": model.ContractVersion = ReadInt32Value(reader); break;
                    case "timestampUtc": model.TimestampUtc = ReadStringValue(reader); break;
                    case "level": model.Level = ReadStringValue(reader); break;
                    case "eventCode": model.EventCode = ReadStringValue(reader); break;
                    case "subsystem": model.Subsystem = ReadStringValue(reader); break;
                    case "buildIdAvailability": model.BuildIdAvailability = ReadStringValue(reader); break;
                    case "processInstanceId": model.ProcessInstanceId = ReadStringValue(reader); break;
                    case "correlationId": model.CorrelationId = ReadStringValue(reader); break;
                    case "diagnosticId": model.DiagnosticId = ReadStringValue(reader); break;
                    case "commandId": model.CommandId = ReadStringValue(reader); break;
                    case "sessionReference": model.SessionReference = ReadStringValue(reader); break;
                    case "messageTemplateKey": model.MessageTemplateKey = ReadStringValue(reader); break;
                    case "safeProperties": model.SafeProperties = ReadProperties(reader); break;
                    case "exceptionSummary": model.ExceptionSummary = ReadException(reader); break;
                    default: throw new JsonSerializationException("Unexpected property.");
                }
            }

            throw new JsonSerializationException("Unclosed object.");
        }

        private static IReadOnlyList<SafeLogProperty> ReadProperties(JsonTextReader reader)
        {
            if (reader.TokenType != JsonToken.StartArray) throw new JsonSerializationException("Expected safeProperties array.");
            List<SafeLogProperty> properties = new List<SafeLogProperty>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndArray) return properties;
                if (reader.TokenType != JsonToken.StartObject) throw new JsonSerializationException("Expected property object.");
                properties.Add(ReadProperty(reader));
            }

            throw new JsonSerializationException("Unclosed safeProperties array.");
        }

        private static SafeLogProperty ReadProperty(JsonTextReader reader)
        {
            string? key = null;
            string? classification = null;
            string? kind = null;
            string? renderedValue = null;
            int? logicalSize = null;
            int? originalScalarCount = null;
            bool? wasTruncated = null;
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                {
                    if (key == null || classification == null || kind == null || renderedValue == null || !logicalSize.HasValue || !originalScalarCount.HasValue || !wasTruncated.HasValue) throw new JsonSerializationException("Missing safe property field.");
                    return new SafeLogProperty(
                        SafePropertyKey.Parse(key),
                        SafeLogValue.Rehydrate(ParseClassificationToken(classification), ParseValueKindToken(kind), renderedValue, logicalSize.Value, originalScalarCount.Value, wasTruncated.Value));
                }

                if (reader.TokenType != JsonToken.PropertyName) throw new JsonSerializationException("Expected safe property field.");
                string name = (string)reader.Value!;
                if (!seen.Add(name)) throw new JsonSerializationException("Duplicate safe property field.");
                if (!reader.Read()) throw new JsonSerializationException("Missing safe property value.");
                switch (name)
                {
                    case "key": key = ReadStringValue(reader); break;
                    case "classification": classification = ReadStringValue(reader); break;
                    case "valueKind": kind = ReadStringValue(reader); break;
                    case "renderedValue": renderedValue = ReadStringValue(reader); break;
                    case "logicalSize": logicalSize = ReadInt32Value(reader); break;
                    case "originalScalarCount": originalScalarCount = ReadInt32Value(reader); break;
                    case "wasTruncated": wasTruncated = ReadBooleanValue(reader); break;
                    default: throw new JsonSerializationException("Unexpected safe property field.");
                }
            }

            throw new JsonSerializationException("Unclosed safe property.");
        }

        private static ExceptionSummary ReadException(JsonTextReader reader)
        {
            if (reader.TokenType != JsonToken.StartObject) throw new JsonSerializationException("Expected exceptionSummary object.");
            string? category = null;
            string? subsystem = null;
            int? innerExceptionCount = null;
            bool? isTransient = null;
            string? diagnosticId = null;
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                {
                    if (category == null || subsystem == null || !innerExceptionCount.HasValue || !isTransient.HasValue || diagnosticId == null) throw new JsonSerializationException("Missing exceptionSummary field.");
                    return ExceptionSummary.Rehydrate(ParseExceptionCategoryToken(category), SubsystemName.Parse(subsystem), innerExceptionCount.Value, isTransient.Value, DiagnosticId.Parse(diagnosticId));
                }

                if (reader.TokenType != JsonToken.PropertyName) throw new JsonSerializationException("Expected exceptionSummary field.");
                string name = (string)reader.Value!;
                if (!seen.Add(name)) throw new JsonSerializationException("Duplicate exceptionSummary field.");
                if (!reader.Read()) throw new JsonSerializationException("Missing exceptionSummary value.");
                switch (name)
                {
                    case "category": category = ReadStringValue(reader); break;
                    case "subsystem": subsystem = ReadStringValue(reader); break;
                    case "innerExceptionCount": innerExceptionCount = ReadInt32Value(reader); break;
                    case "isTransient": isTransient = ReadBooleanValue(reader); break;
                    case "diagnosticId": diagnosticId = ReadStringValue(reader); break;
                    default: throw new JsonSerializationException("Unexpected exceptionSummary field.");
                }
            }

            throw new JsonSerializationException("Unclosed exceptionSummary.");
        }

        private static string ReadStringValue(JsonTextReader reader)
        {
            if (reader.TokenType != JsonToken.String) throw new JsonSerializationException("Expected string.");
            return (string)reader.Value!;
        }

        private static int ReadInt32Value(JsonTextReader reader)
        {
            if (reader.TokenType != JsonToken.Integer) throw new JsonSerializationException("Expected integer.");
            return Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
        }

        private static bool ReadBooleanValue(JsonTextReader reader)
        {
            if (reader.TokenType != JsonToken.Boolean) throw new JsonSerializationException("Expected boolean.");
            return (bool)reader.Value!;
        }

        private static string ToLogLevelToken(LogLevel value)
        {
            switch (value)
            {
                case LogLevel.Trace: return "trace";
                case LogLevel.Debug: return "debug";
                case LogLevel.Information: return "information";
                case LogLevel.Warning: return "warning";
                case LogLevel.Error: return "error";
                case LogLevel.Critical: return "critical";
                default: throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        private static LogLevel ParseLogLevelToken(string value)
        {
            switch (value)
            {
                case "trace": return LogLevel.Trace;
                case "debug": return LogLevel.Debug;
                case "information": return LogLevel.Information;
                case "warning": return LogLevel.Warning;
                case "error": return LogLevel.Error;
                case "critical": return LogLevel.Critical;
                default: throw new FormatException("Unknown LogLevel token.");
            }
        }

        private static string ToBuildIdAvailabilityToken(BuildIdAvailability value)
        {
            switch (value)
            {
                case BuildIdAvailability.UnavailableNotYetComposed: return "unavailable_not_yet_composed";
                case BuildIdAvailability.Available: return "available";
                default: throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        private static BuildIdAvailability ParseBuildIdAvailabilityToken(string value)
        {
            switch (value)
            {
                case "unavailable_not_yet_composed": return BuildIdAvailability.UnavailableNotYetComposed;
                case "available": return BuildIdAvailability.Available;
                default: throw new FormatException("Unknown BuildIdAvailability token.");
            }
        }

        private static string ToClassificationToken(DiagnosticDataClassification value)
        {
            switch (value)
            {
                case DiagnosticDataClassification.Public: return "public";
                case DiagnosticDataClassification.OperationalSafe: return "operational_safe";
                default: throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        private static DiagnosticDataClassification ParseClassificationToken(string value)
        {
            switch (value)
            {
                case "public": return DiagnosticDataClassification.Public;
                case "operational_safe": return DiagnosticDataClassification.OperationalSafe;
                default: throw new FormatException("Unsafe or unknown diagnostic classification.");
            }
        }

        private static string ToValueKindToken(SafeLogValueKind value)
        {
            switch (value)
            {
                case SafeLogValueKind.Boolean: return "boolean";
                case SafeLogValueKind.Integer: return "integer";
                case SafeLogValueKind.Decimal: return "decimal";
                case SafeLogValueKind.Code: return "code";
                case SafeLogValueKind.Duration: return "duration";
                case SafeLogValueKind.Timestamp: return "timestamp";
                case SafeLogValueKind.ByteCount: return "byte_count";
                case SafeLogValueKind.TechnicalIdentifier: return "technical_identifier";
                case SafeLogValueKind.Fingerprint: return "fingerprint";
                case SafeLogValueKind.BoundedText: return "bounded_text";
                case SafeLogValueKind.SanitizedPath: return "sanitized_path";
                case SafeLogValueKind.SanitizedEndpoint: return "sanitized_endpoint";
                default: throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        private static SafeLogValueKind ParseValueKindToken(string value)
        {
            switch (value)
            {
                case "boolean": return SafeLogValueKind.Boolean;
                case "integer": return SafeLogValueKind.Integer;
                case "decimal": return SafeLogValueKind.Decimal;
                case "code": return SafeLogValueKind.Code;
                case "duration": return SafeLogValueKind.Duration;
                case "timestamp": return SafeLogValueKind.Timestamp;
                case "byte_count": return SafeLogValueKind.ByteCount;
                case "technical_identifier": return SafeLogValueKind.TechnicalIdentifier;
                case "fingerprint": return SafeLogValueKind.Fingerprint;
                case "bounded_text": return SafeLogValueKind.BoundedText;
                case "sanitized_path": return SafeLogValueKind.SanitizedPath;
                case "sanitized_endpoint": return SafeLogValueKind.SanitizedEndpoint;
                default: throw new FormatException("Unknown SafeLogValueKind token.");
            }
        }

        private static string ToExceptionCategoryToken(ExceptionCategory value)
        {
            switch (value)
            {
                case ExceptionCategory.InvalidOperation: return "invalid_operation";
                case ExceptionCategory.IoFailure: return "io_failure";
                case ExceptionCategory.AccessDenied: return "access_denied";
                case ExceptionCategory.Cancelled: return "cancelled";
                case ExceptionCategory.Unexpected: return "unexpected";
                default: throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        private static ExceptionCategory ParseExceptionCategoryToken(string value)
        {
            switch (value)
            {
                case "invalid_operation": return ExceptionCategory.InvalidOperation;
                case "io_failure": return ExceptionCategory.IoFailure;
                case "access_denied": return ExceptionCategory.AccessDenied;
                case "cancelled": return ExceptionCategory.Cancelled;
                case "unexpected": return ExceptionCategory.Unexpected;
                default: throw new FormatException("Unknown ExceptionCategory token.");
            }
        }

        private sealed class DiagnosticReadModel
        {
            public string? ContractType { get; set; }
            public int ContractVersion { get; set; }
            public string? TimestampUtc { get; set; }
            public string? Level { get; set; }
            public string? EventCode { get; set; }
            public string? Subsystem { get; set; }
            public string? BuildIdAvailability { get; set; }
            public string? ProcessInstanceId { get; set; }
            public string? CorrelationId { get; set; }
            public string? DiagnosticId { get; set; }
            public string? CommandId { get; set; }
            public string? SessionReference { get; set; }
            public string? MessageTemplateKey { get; set; }
            public IReadOnlyList<SafeLogProperty> SafeProperties { get; set; } = Array.Empty<SafeLogProperty>();
            public ExceptionSummary? ExceptionSummary { get; set; }

            public void ValidateRequired()
            {
                if (ContractType == null || ContractVersion == 0 || TimestampUtc == null || Level == null || EventCode == null || Subsystem == null || BuildIdAvailability == null || ProcessInstanceId == null || MessageTemplateKey == null || SafeProperties == null)
                {
                    throw new JsonSerializationException("Missing required diagnostic field.");
                }
            }
        }
    }
}
