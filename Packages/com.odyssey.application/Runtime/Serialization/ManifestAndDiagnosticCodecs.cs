using System;
using System.Collections.Generic;
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
        public static readonly ContractType Type = ContractType.Parse("odyssey.diagnostics.log_event");
        public JsonContractKey Key { get; } = new JsonContractKey(SerializationProfile.DiagnosticJson, Type, ContractVersion.Create(1));

        public Result<JsonPayload> Write(LogEventV1 value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            JsonPayload payload = new CanonicalJsonWriter().StartObject()
                .String("contractType", Type.ToString())
                .Int32("contractVersion", LogEventV1.SchemaVersionValue)
                .String("timestampUtc", value.TimestampUtc.ToString())
                .String("level", value.Level.ToString())
                .String("eventCode", value.EventCode.ToString())
                .String("subsystem", value.Subsystem.ToString())
                .String("buildIdAvailability", value.BuildIdAvailability.ToString())
                .String("processInstanceId", value.ProcessInstanceId.ToString())
                .NullableString("correlationId", value.CorrelationId?.ToString())
                .NullableString("diagnosticId", value.DiagnosticId?.ToString())
                .NullableString("commandId", value.CommandId?.ToString())
                .NullableString("sessionReference", value.SessionReference?.ToString())
                .String("messageTemplateKey", value.MessageTemplateKey.ToString())
                .String("safeProperties", EncodeProperties(value.SafeProperties))
                .NullableString("exceptionSummary", EncodeException(value.ExceptionSummary))
                .EndObject()
                .ToPayload();
            return Result<JsonPayload>.Success(payload);
        }

        public Result<LogEventV1> Read(byte[] utf8Json)
        {
            Result<JsonObjectReader> reader = JsonObjectReader.Read(utf8Json, JsonPayloadLimits.DiagnosticRecordBytes);
            if (reader.IsFailure) return Result<LogEventV1>.Failure(reader.Error);
            Result<string> type = reader.Value.RequiredString("contractType");
            Result<int> version = reader.Value.RequiredInt32("contractVersion");
            if (type.IsFailure) return Result<LogEventV1>.Failure(type.Error);
            if (version.IsFailure) return Result<LogEventV1>.Failure(version.Error);
            if (type.Value != Type.ToString() || version.Value != LogEventV1.SchemaVersionValue) return Result<LogEventV1>.Failure(SerializationFailures.UnsupportedContract());

            try
            {
                Result<string> timestamp = reader.Value.RequiredString("timestampUtc");
                Result<string> level = reader.Value.RequiredString("level");
                Result<string> eventCode = reader.Value.RequiredString("eventCode");
                Result<string> subsystem = reader.Value.RequiredString("subsystem");
                Result<string> buildIdAvailability = reader.Value.RequiredString("buildIdAvailability");
                Result<string> processInstanceId = reader.Value.RequiredString("processInstanceId");
                Result<string> messageTemplateKey = reader.Value.RequiredString("messageTemplateKey");
                Result<string> safeProperties = reader.Value.RequiredString("safeProperties");
                if (timestamp.IsFailure || level.IsFailure || eventCode.IsFailure || subsystem.IsFailure || buildIdAvailability.IsFailure || processInstanceId.IsFailure || messageTemplateKey.IsFailure || safeProperties.IsFailure) return Result<LogEventV1>.Failure(SerializationFailures.InvalidPayload());
                reader.Value.TryGetString("correlationId", out string? correlation);
                reader.Value.TryGetString("diagnosticId", out string? diagnostic);
                reader.Value.TryGetString("commandId", out string? command);
                reader.Value.TryGetString("sessionReference", out string? session);
                reader.Value.TryGetString("exceptionSummary", out string? exception);

                LogEventV1 logEvent = new LogEventV1(
                    UtcInstant.Parse(timestamp.Value),
                    (LogLevel)Enum.Parse(typeof(LogLevel), level.Value, false),
                    EventCode.Parse(eventCode.Value),
                    SubsystemName.Parse(subsystem.Value),
                    (BuildIdAvailability)Enum.Parse(typeof(BuildIdAvailability), buildIdAvailability.Value, false),
                    ProcessInstanceId.Parse(processInstanceId.Value),
                    MessageTemplateKey.Parse(messageTemplateKey.Value),
                    DecodeProperties(safeProperties.Value),
                    correlation == null ? (CorrelationId?)null : CorrelationId.Parse(correlation),
                    diagnostic == null ? (DiagnosticId?)null : DiagnosticId.Parse(diagnostic),
                    command == null ? (CommandId?)null : CommandId.Parse(command),
                    session == null ? (SessionReference?)null : SessionReference.Parse(session),
                    DecodeException(exception));
                Result registry = EventCodeRegistry.CreateDefault().Validate(logEvent);
                return registry.IsFailure ? Result<LogEventV1>.Failure(registry.Error) : Result<LogEventV1>.Success(logEvent);
            }
            catch (ArgumentException)
            {
                return Result<LogEventV1>.Failure(SerializationFailures.InvalidPayload());
            }
            catch (FormatException)
            {
                return Result<LogEventV1>.Failure(SerializationFailures.InvalidPayload());
            }
        }

        private static string EncodeProperties(IReadOnlyList<SafeLogProperty> properties)
        {
            string[] rows = new string[properties.Count];
            for (int index = 0; index < properties.Count; index++)
            {
                SafeLogProperty property = properties[index];
                rows[index] = property.Key + "," + property.Value.Classification + "," + property.Value.ValueKind + "," + property.Value.RenderedValue.Replace("|", "%7C").Replace(",", "%2C");
            }

            return string.Join("|", rows);
        }

        private static IReadOnlyList<SafeLogProperty> DecodeProperties(string encoded)
        {
            if (encoded.Length == 0) return Array.Empty<SafeLogProperty>();
            string[] rows = encoded.Split('|');
            SafeLogProperty[] properties = new SafeLogProperty[rows.Length];
            for (int index = 0; index < rows.Length; index++)
            {
                string[] parts = rows[index].Split(',');
                if (parts.Length != 4) throw new FormatException("Diagnostic property row is not canonical.");
                DiagnosticDataClassification classification = (DiagnosticDataClassification)Enum.Parse(typeof(DiagnosticDataClassification), parts[1], false);
                SafeLogValueKind kind = (SafeLogValueKind)Enum.Parse(typeof(SafeLogValueKind), parts[2], false);
                string value = parts[3].Replace("%2C", ",").Replace("%7C", "|");
                properties[index] = new SafeLogProperty(SafePropertyKey.Parse(parts[0]), CreateValue(classification, kind, value));
            }

            return properties;
        }

        private static SafeLogValue CreateValue(DiagnosticDataClassification classification, SafeLogValueKind kind, string value)
        {
            switch (kind)
            {
                case SafeLogValueKind.Boolean: return SafeLogValue.Boolean(value == "true");
                case SafeLogValueKind.Integer: return SafeLogValue.Count(long.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
                case SafeLogValueKind.Code: return SafeLogValue.Code(value);
                case SafeLogValueKind.Duration:
                case SafeLogValueKind.Timestamp:
                case SafeLogValueKind.ByteCount:
                case SafeLogValueKind.TechnicalIdentifier:
                case SafeLogValueKind.Fingerprint:
                case SafeLogValueKind.BoundedText:
                case SafeLogValueKind.SanitizedPath:
                case SafeLogValueKind.SanitizedEndpoint:
                    return SafeLogValue.BoundedText(value, 256, classification);
                default:
                    throw new FormatException("Unsupported safe property kind.");
            }
        }

        private static string? EncodeException(ExceptionSummary? exception)
        {
            if (!exception.HasValue) return null;
            ExceptionSummary value = exception.Value;
            return value.Category + "," + value.Subsystem + "," + value.InnerExceptionCount.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + (value.IsTransient ? "true" : "false") + "," + value.DiagnosticId;
        }

        private static ExceptionSummary? DecodeException(string? encoded)
        {
            if (encoded == null) return null;
            string[] parts = encoded.Split(',');
            if (parts.Length != 5) throw new FormatException("ExceptionSummary is not canonical.");
            Exception ex = new InvalidOperationException("safe");
            if (parts[0] == nameof(ExceptionCategory.IoFailure)) ex = new System.IO.IOException("safe");
            if (parts[0] == nameof(ExceptionCategory.Cancelled)) ex = new OperationCanceledException();
            if (parts[0] == nameof(ExceptionCategory.AccessDenied)) ex = new UnauthorizedAccessException("safe");
            return ExceptionSummary.FromException(ex, SubsystemName.Parse(parts[1]), DiagnosticId.Parse(parts[4]));
        }
    }
}
