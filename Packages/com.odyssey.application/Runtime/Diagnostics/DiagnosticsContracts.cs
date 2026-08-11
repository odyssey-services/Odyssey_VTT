using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Identity;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Diagnostics
{
    public enum LogLevel
    {
        Trace = 1,
        Debug = 2,
        Information = 3,
        Warning = 4,
        Error = 5,
        Critical = 6
    }

    public enum EventCodeStatus
    {
        Active = 1,
        Deprecated = 2,
        Reserved = 3
    }

    public enum SafePropertyClassification
    {
        Operational = 1,
        TechnicalIdentifier = 2,
        SanitizedPath = 3,
        SanitizedEndpoint = 4,
        Count = 5,
        Duration = 6,
        Timestamp = 7,
        ByteCount = 8
    }

    public enum BuildIdAvailability
    {
        UnavailableNotYetComposed = 1,
        Available = 2
    }

    public readonly struct ProcessInstanceId : IEquatable<ProcessInstanceId>
    {
        private const string Prefix = "proc_";
        private const int HexLength = 32;
        private readonly string _value;

        private ProcessInstanceId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out ProcessInstanceId id) => DiagnosticText.TryParsePrefixedHex(value, Prefix, HexLength, out id, static v => new ProcessInstanceId(v));
        public static ProcessInstanceId Parse(string value) => TryParse(value, out ProcessInstanceId id) ? id : throw new FormatException("ProcessInstanceId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(ProcessInstanceId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is ProcessInstanceId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(ProcessInstanceId left, ProcessInstanceId right) => left.Equals(right);
        public static bool operator !=(ProcessInstanceId left, ProcessInstanceId right) => !left.Equals(right);
    }

    public readonly struct EventCode : IEquatable<EventCode>
    {
        private readonly string _value;

        private EventCode(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out EventCode code)
        {
            if (DiagnosticText.IsDottedLowerIdentifier(value, 96, 2))
            {
                code = new EventCode(value!);
                return true;
            }

            code = default;
            return false;
        }

        public static EventCode Parse(string value) => TryParse(value, out EventCode code) ? code : throw new FormatException("EventCode is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(EventCode other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is EventCode other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(EventCode left, EventCode right) => left.Equals(right);
        public static bool operator !=(EventCode left, EventCode right) => !left.Equals(right);
    }

    public readonly struct MessageTemplateKey : IEquatable<MessageTemplateKey>
    {
        private readonly string _value;

        private MessageTemplateKey(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out MessageTemplateKey key)
        {
            if (DiagnosticText.IsDottedLowerIdentifier(value, 128, 2))
            {
                key = new MessageTemplateKey(value!);
                return true;
            }

            key = default;
            return false;
        }

        public static MessageTemplateKey Parse(string value) => TryParse(value, out MessageTemplateKey key) ? key : throw new FormatException("MessageTemplateKey is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(MessageTemplateKey other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is MessageTemplateKey other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
    }

    public readonly struct SubsystemName : IEquatable<SubsystemName>
    {
        private readonly string _value;

        private SubsystemName(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out SubsystemName subsystem)
        {
            if (DiagnosticText.IsDottedLowerIdentifier(value, 96, 1))
            {
                subsystem = new SubsystemName(value!);
                return true;
            }

            subsystem = default;
            return false;
        }

        public static SubsystemName Parse(string value) => TryParse(value, out SubsystemName subsystem) ? subsystem : throw new FormatException("SubsystemName is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(SubsystemName other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is SubsystemName other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
    }

    public readonly struct SafePropertyKey : IEquatable<SafePropertyKey>
    {
        private readonly string _value;

        private SafePropertyKey(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out SafePropertyKey key)
        {
            if (DiagnosticText.IsLowerToken(value, 64))
            {
                key = new SafePropertyKey(value!);
                return true;
            }

            key = default;
            return false;
        }

        public static SafePropertyKey Parse(string value) => TryParse(value, out SafePropertyKey key) ? key : throw new FormatException("SafePropertyKey is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(SafePropertyKey other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is SafePropertyKey other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
    }

    public readonly struct SessionReference : IEquatable<SessionReference>
    {
        private readonly string _value;

        private SessionReference(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out SessionReference reference)
        {
            if (DiagnosticText.IsLowerToken(value, 96))
            {
                reference = new SessionReference(value!);
                return true;
            }

            reference = default;
            return false;
        }

        public static SessionReference Parse(string value) => TryParse(value, out SessionReference reference) ? reference : throw new FormatException("SessionReference is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(SessionReference other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is SessionReference other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
    }

    public readonly struct ExceptionSummary
    {
        public ExceptionSummary(string exceptionType, string safeSummary)
        {
            if (!DiagnosticText.IsDottedUpperOrLowerIdentifier(exceptionType, 128, 1)) throw new ArgumentException("Exception type is not safe.", nameof(exceptionType));
            if (!DiagnosticText.IsSafeBoundedText(safeSummary, 256)) throw new ArgumentException("Exception summary is not safe.", nameof(safeSummary));
            ExceptionType = exceptionType;
            SafeSummary = safeSummary;
        }

        public string ExceptionType { get; }
        public string SafeSummary { get; }
        public bool IsValid => ExceptionType != null && SafeSummary != null;
    }

    public sealed class SafeLogValue
    {
        private SafeLogValue(SafePropertyClassification classification, string renderedValue, int logicalSize)
        {
            Classification = classification;
            RenderedValue = renderedValue;
            LogicalSize = logicalSize;
        }

        public SafePropertyClassification Classification { get; }
        public string RenderedValue { get; }
        public int LogicalSize { get; }
        public bool WasTruncated { get; private set; }

        public static SafeLogValue BoundedText(string value, int maxScalars = 256)
        {
            if (string.IsNullOrEmpty(value)) throw new ArgumentException("Safe text is required.", nameof(value));
            if (maxScalars <= 0 || maxScalars > 256) throw new ArgumentOutOfRangeException(nameof(maxScalars));
            string safe = SanitizeControlCharacters(value);
            bool truncated = safe.Length > maxScalars;
            if (truncated)
            {
                safe = safe.Substring(0, Math.Max(0, maxScalars - 12)) + "[truncated]";
            }

            SafeLogValue result = new SafeLogValue(SafePropertyClassification.Operational, safe, safe.Length);
            result.WasTruncated = truncated;
            return result;
        }

        public static SafeLogValue Code(string value)
        {
            if (!DiagnosticText.IsDottedLowerIdentifier(value, 96, 1)) throw new ArgumentException("Code is not safe.", nameof(value));
            return new SafeLogValue(SafePropertyClassification.Operational, value, value.Length);
        }

        public static SafeLogValue Count(long value) => new SafeLogValue(SafePropertyClassification.Count, value.ToString(System.Globalization.CultureInfo.InvariantCulture), 8);
        public static SafeLogValue Duration(TimeSpan value) => new SafeLogValue(SafePropertyClassification.Duration, value.TotalMilliseconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "ms", 16);
        public static SafeLogValue Timestamp(UtcInstant value) => value.IsValid ? new SafeLogValue(SafePropertyClassification.Timestamp, value.ToString(), 32) : throw new ArgumentException("Timestamp is required.", nameof(value));
        public static SafeLogValue ByteCount(long value) => value >= 0 ? new SafeLogValue(SafePropertyClassification.ByteCount, value.ToString(System.Globalization.CultureInfo.InvariantCulture), 8) : throw new ArgumentOutOfRangeException(nameof(value));
        public static SafeLogValue TechnicalIdentifier(string value) => DiagnosticText.IsSafeFingerprint(value, 128) ? new SafeLogValue(SafePropertyClassification.TechnicalIdentifier, value, value.Length) : throw new ArgumentException("Identifier is not safe.", nameof(value));
        public static SafeLogValue SanitizedPath(string value) => new SafeLogValue(SafePropertyClassification.SanitizedPath, PathSanitizer.Sanitize(value), 48);
        public static SafeLogValue SanitizedEndpoint(string value) => new SafeLogValue(SafePropertyClassification.SanitizedEndpoint, EndpointSanitizer.Sanitize(value), 48);

        private static string SanitizeControlCharacters(string value)
        {
            char[] chars = value.ToCharArray();
            for (int index = 0; index < chars.Length; index++)
            {
                if (char.IsControl(chars[index])) chars[index] = '?';
            }

            return new string(chars);
        }
    }

    public readonly struct SafeLogProperty
    {
        public SafeLogProperty(SafePropertyKey key, SafeLogValue value)
        {
            if (!key.IsValid) throw new ArgumentException("Property key is required.", nameof(key));
            Value = value ?? throw new ArgumentNullException(nameof(value));
            Key = key;
        }

        public SafePropertyKey Key { get; }
        public SafeLogValue Value { get; }
        public bool IsValid => Key.IsValid && Value != null;
    }

    public sealed class DiagnosticContext
    {
        public DiagnosticContext(ProcessInstanceId processInstanceId, CorrelationId? correlationId = null, DiagnosticId? diagnosticId = null, CommandId? commandId = null, SessionReference? sessionReference = null)
        {
            if (!processInstanceId.IsValid) throw new ArgumentException("Process instance id is required.", nameof(processInstanceId));
            if (correlationId.HasValue && !correlationId.Value.IsValid) throw new ArgumentException("Correlation id must be valid.", nameof(correlationId));
            if (diagnosticId.HasValue && !diagnosticId.Value.IsValid) throw new ArgumentException("Diagnostic id must be valid.", nameof(diagnosticId));
            if (commandId.HasValue && !commandId.Value.IsValid) throw new ArgumentException("Command id must be valid.", nameof(commandId));
            if (sessionReference.HasValue && !sessionReference.Value.IsValid) throw new ArgumentException("Session reference must be valid.", nameof(sessionReference));
            ProcessInstanceId = processInstanceId;
            CorrelationId = correlationId;
            DiagnosticId = diagnosticId;
            CommandId = commandId;
            SessionReference = sessionReference;
        }

        public ProcessInstanceId ProcessInstanceId { get; }
        public CorrelationId? CorrelationId { get; }
        public DiagnosticId? DiagnosticId { get; }
        public CommandId? CommandId { get; }
        public SessionReference? SessionReference { get; }
    }

    public sealed class LogEventV1
    {
        public const int SchemaVersionValue = 1;
        private readonly ReadOnlyCollection<SafeLogProperty> _safeProperties;

        public LogEventV1(UtcInstant timestampUtc, LogLevel level, EventCode eventCode, SubsystemName subsystem, BuildIdAvailability buildIdAvailability, ProcessInstanceId processInstanceId, MessageTemplateKey messageTemplateKey, IReadOnlyList<SafeLogProperty>? safeProperties = null, CorrelationId? correlationId = null, DiagnosticId? diagnosticId = null, CommandId? commandId = null, SessionReference? sessionReference = null, ExceptionSummary? exceptionSummary = null)
        {
            if (!timestampUtc.IsValid) throw new ArgumentException("TimestampUtc is required.", nameof(timestampUtc));
            if (!Enum.IsDefined(typeof(LogLevel), level)) throw new ArgumentOutOfRangeException(nameof(level));
            if (!eventCode.IsValid) throw new ArgumentException("EventCode is required.", nameof(eventCode));
            if (!subsystem.IsValid) throw new ArgumentException("Subsystem is required.", nameof(subsystem));
            if (!Enum.IsDefined(typeof(BuildIdAvailability), buildIdAvailability)) throw new ArgumentOutOfRangeException(nameof(buildIdAvailability));
            if (buildIdAvailability != BuildIdAvailability.UnavailableNotYetComposed) throw new ArgumentException("BuildId is not composed until ODY-S00-008.", nameof(buildIdAvailability));
            if (!processInstanceId.IsValid) throw new ArgumentException("ProcessInstanceId is required.", nameof(processInstanceId));
            if (!messageTemplateKey.IsValid) throw new ArgumentException("MessageTemplateKey is required.", nameof(messageTemplateKey));
            if (correlationId.HasValue && !correlationId.Value.IsValid) throw new ArgumentException("Correlation id must be valid.", nameof(correlationId));
            if (diagnosticId.HasValue && !diagnosticId.Value.IsValid) throw new ArgumentException("Diagnostic id must be valid.", nameof(diagnosticId));
            if (commandId.HasValue && !commandId.Value.IsValid) throw new ArgumentException("Command id must be valid.", nameof(commandId));
            if (sessionReference.HasValue && !sessionReference.Value.IsValid) throw new ArgumentException("Session reference must be valid.", nameof(sessionReference));
            if (exceptionSummary.HasValue && !exceptionSummary.Value.IsValid) throw new ArgumentException("Exception summary must be valid.", nameof(exceptionSummary));
            SchemaVersion = SchemaVersionValue;
            TimestampUtc = timestampUtc;
            Level = level;
            EventCode = eventCode;
            Subsystem = subsystem;
            BuildIdAvailability = buildIdAvailability;
            ProcessInstanceId = processInstanceId;
            CorrelationId = correlationId;
            DiagnosticId = diagnosticId;
            CommandId = commandId;
            SessionReference = sessionReference;
            MessageTemplateKey = messageTemplateKey;
            _safeProperties = CopyProperties(safeProperties);
            ExceptionSummary = exceptionSummary;
        }

        public int SchemaVersion { get; }
        public UtcInstant TimestampUtc { get; }
        public LogLevel Level { get; }
        public EventCode EventCode { get; }
        public SubsystemName Subsystem { get; }
        public BuildIdAvailability BuildIdAvailability { get; }
        public ProcessInstanceId ProcessInstanceId { get; }
        public CorrelationId? CorrelationId { get; }
        public DiagnosticId? DiagnosticId { get; }
        public CommandId? CommandId { get; }
        public SessionReference? SessionReference { get; }
        public MessageTemplateKey MessageTemplateKey { get; }
        public IReadOnlyList<SafeLogProperty> SafeProperties => _safeProperties;
        public ExceptionSummary? ExceptionSummary { get; }
        public int EstimatedLogicalSize => 96 + SumPropertySize(_safeProperties) + (ExceptionSummary.HasValue ? 128 : 0);

        private static ReadOnlyCollection<SafeLogProperty> CopyProperties(IReadOnlyList<SafeLogProperty>? source)
        {
            if (source == null || source.Count == 0) return Array.AsReadOnly(Array.Empty<SafeLogProperty>());
            if (source.Count > 20) throw new ArgumentOutOfRangeException(nameof(source));
            SafeLogProperty[] copy = new SafeLogProperty[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                if (!source[index].IsValid) throw new ArgumentException("Safe property is required.", nameof(source));
                copy[index] = source[index];
            }

            return Array.AsReadOnly(copy);
        }

        private static int SumPropertySize(IReadOnlyList<SafeLogProperty> properties)
        {
            int total = 0;
            for (int index = 0; index < properties.Count; index++)
            {
                total += properties[index].Key.ToString().Length + properties[index].Value.LogicalSize;
            }

            return total;
        }
    }

    public sealed class EventCodeDefinition
    {
        public EventCodeDefinition(EventCode eventCode, SubsystemName ownerSubsystem, LogLevel defaultLevel, IReadOnlyList<SafePropertyKey> allowedPropertyKeys, IReadOnlyDictionary<SafePropertyKey, SafePropertyClassification> propertyClassifications, string purpose, EventCodeStatus status)
        {
            if (!eventCode.IsValid) throw new ArgumentException("EventCode is required.", nameof(eventCode));
            if (!ownerSubsystem.IsValid) throw new ArgumentException("Owner subsystem is required.", nameof(ownerSubsystem));
            if (!Enum.IsDefined(typeof(LogLevel), defaultLevel)) throw new ArgumentOutOfRangeException(nameof(defaultLevel));
            if (!DiagnosticText.IsSafeBoundedText(purpose, 256)) throw new ArgumentException("Purpose is not safe.", nameof(purpose));
            if (!Enum.IsDefined(typeof(EventCodeStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            EventCode = eventCode;
            OwnerSubsystem = ownerSubsystem;
            DefaultLevel = defaultLevel;
            AllowedPropertyKeys = CopyKeys(allowedPropertyKeys);
            PropertyClassifications = new ReadOnlyDictionary<SafePropertyKey, SafePropertyClassification>(new Dictionary<SafePropertyKey, SafePropertyClassification>(propertyClassifications ?? throw new ArgumentNullException(nameof(propertyClassifications))));
            Purpose = purpose;
            Status = status;
            ValidateParity();
        }

        public EventCode EventCode { get; }
        public SubsystemName OwnerSubsystem { get; }
        public LogLevel DefaultLevel { get; }
        public IReadOnlyList<SafePropertyKey> AllowedPropertyKeys { get; }
        public IReadOnlyDictionary<SafePropertyKey, SafePropertyClassification> PropertyClassifications { get; }
        public string Purpose { get; }
        public EventCodeStatus Status { get; }

        public bool Allows(SafeLogProperty property)
        {
            return property.IsValid && PropertyClassifications.TryGetValue(property.Key, out SafePropertyClassification classification) && classification == property.Value.Classification;
        }

        private void ValidateParity()
        {
            if (AllowedPropertyKeys.Count != PropertyClassifications.Count) throw new ArgumentException("Property keys and classifications must match.");
            for (int index = 0; index < AllowedPropertyKeys.Count; index++)
            {
                if (!PropertyClassifications.ContainsKey(AllowedPropertyKeys[index])) throw new ArgumentException("Property classification is missing.");
            }
        }

        private static ReadOnlyCollection<SafePropertyKey> CopyKeys(IReadOnlyList<SafePropertyKey>? source)
        {
            if (source == null) return Array.AsReadOnly(Array.Empty<SafePropertyKey>());
            SafePropertyKey[] copy = new SafePropertyKey[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                if (!source[index].IsValid) throw new ArgumentException("Property key is required.", nameof(source));
                copy[index] = source[index];
            }

            return Array.AsReadOnly(copy);
        }
    }

    public sealed class EventCodeRegistry
    {
        private static readonly CorrelationId RegistryFailureCorrelationId = CorrelationId.Parse("corr_00000000000000000000000000000000");
        private readonly ReadOnlyDictionary<EventCode, EventCodeDefinition> _definitions;

        public EventCodeRegistry(IReadOnlyList<EventCodeDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            Dictionary<EventCode, EventCodeDefinition> copy = new Dictionary<EventCode, EventCodeDefinition>();
            for (int index = 0; index < definitions.Count; index++)
            {
                EventCodeDefinition definition = definitions[index] ?? throw new ArgumentException("EventCode definition is required.", nameof(definitions));
                if (definition.Status != EventCodeStatus.Active) throw new ArgumentException("Only active EventCodes may be used by the runtime.", nameof(definitions));
                if (copy.ContainsKey(definition.EventCode)) throw new ArgumentException("Duplicate EventCode definition.", nameof(definitions));
                copy.Add(definition.EventCode, definition);
            }

            _definitions = new ReadOnlyDictionary<EventCode, EventCodeDefinition>(copy);
        }

        public IReadOnlyDictionary<EventCode, EventCodeDefinition> Definitions => _definitions;

        public Result Validate(LogEventV1 logEvent)
        {
            if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));
            CorrelationId correlationId = logEvent.CorrelationId ?? RegistryFailureCorrelationId;
            if (!_definitions.TryGetValue(logEvent.EventCode, out EventCodeDefinition definition) || definition.Status != EventCodeStatus.Active)
            {
                return Result.Failure(DiagnosticContractErrors.UnknownEventCode(correlationId));
            }

            for (int index = 0; index < logEvent.SafeProperties.Count; index++)
            {
                if (!definition.Allows(logEvent.SafeProperties[index]))
                {
                    return Result.Failure(DiagnosticContractErrors.UnregisteredProperty(correlationId));
                }
            }

            return Result.Success();
        }

        public static EventCodeRegistry CreateDefault()
        {
            return new EventCodeRegistry(new[]
            {
                Define(OdysseyEventCodes.RuntimeStarting, "runtime", LogLevel.Information, "Runtime startup began.", Props(("phase", SafePropertyClassification.Operational))),
                Define(OdysseyEventCodes.RuntimeReady, "runtime", LogLevel.Information, "Runtime reached Ready.", Props(("state", SafePropertyClassification.Operational), ("duration_ms", SafePropertyClassification.Duration))),
                Define(OdysseyEventCodes.RuntimeStartupFailed, "runtime", LogLevel.Error, "Runtime startup failed safely.", Props(("phase", SafePropertyClassification.Operational), ("reason", SafePropertyClassification.Operational))),
                Define(OdysseyEventCodes.RuntimeShutdownRequested, "runtime", LogLevel.Information, "Runtime shutdown was requested.", Props(("state", SafePropertyClassification.Operational))),
                Define(OdysseyEventCodes.RuntimeShutdownCompleted, "runtime", LogLevel.Information, "Runtime shutdown completed.", Props(("duration_ms", SafePropertyClassification.Duration))),
                Define(OdysseyEventCodes.DiagnosticsDroppedEvents, "diagnostics", LogLevel.Warning, "Diagnostics queue dropped lower-priority events under pressure.", Props(("dropped_count", SafePropertyClassification.Count), ("level", SafePropertyClassification.Operational))),
                Define(OdysseyEventCodes.DiagnosticsSinkFailed, "diagnostics", LogLevel.Warning, "A diagnostics sink failed and fallback handled the event.", Props(("sink", SafePropertyClassification.Operational), ("diagnostic_id", SafePropertyClassification.TechnicalIdentifier))),
                Define(OdysseyEventCodes.DiagnosticsProbe, "diagnostics", LogLevel.Information, "Developer Shell emitted a safe diagnostic probe.", Props(("probe", SafePropertyClassification.Operational))),
                Define(OdysseyEventCodes.CrashMarkerDetected, "diagnostics", LogLevel.Warning, "A previous unfinished crash marker was detected.", Props(("marker", SafePropertyClassification.SanitizedPath))),
                Define(OdysseyEventCodes.CrashMarkerCompleted, "diagnostics", LogLevel.Information, "The crash marker was completed during clean shutdown.", Props(("marker", SafePropertyClassification.SanitizedPath))),
                Define(OdysseyEventCodes.DeveloperProbeAccepted, "developer", LogLevel.Information, "DeveloperShell probe command was accepted.", Props(("command_id", SafePropertyClassification.TechnicalIdentifier), ("result_status", SafePropertyClassification.Operational))),
                Define(OdysseyEventCodes.DeveloperProbeRejected, "developer", LogLevel.Warning, "DeveloperShell probe command was rejected.", Props(("command_id", SafePropertyClassification.TechnicalIdentifier), ("result_status", SafePropertyClassification.Operational)))
            });
        }

        private static EventCodeDefinition Define(EventCode code, string subsystem, LogLevel level, string purpose, IReadOnlyDictionary<SafePropertyKey, SafePropertyClassification> properties)
        {
            List<SafePropertyKey> keys = new List<SafePropertyKey>(properties.Keys);
            return new EventCodeDefinition(code, SubsystemName.Parse(subsystem), level, keys, properties, purpose, EventCodeStatus.Active);
        }

        private static IReadOnlyDictionary<SafePropertyKey, SafePropertyClassification> Props(params (string Key, SafePropertyClassification Classification)[] properties)
        {
            Dictionary<SafePropertyKey, SafePropertyClassification> result = new Dictionary<SafePropertyKey, SafePropertyClassification>();
            for (int index = 0; index < properties.Length; index++)
            {
                result.Add(SafePropertyKey.Parse(properties[index].Key), properties[index].Classification);
            }

            return result;
        }
    }

    internal static class DiagnosticContractErrors
    {
        internal static Error UnknownEventCode(CorrelationId correlationId) => Error.Create(
            ErrorCodes.ApplicationValidationInvalid,
            ErrorCategory.Validation,
            SafeReasonCode.ActionNotAllowed,
            UserMessageKey.Parse("errors.diagnostics.event_code_unknown"),
            RetryDirective.DoNotRetry,
            correlationId);

        internal static Error UnregisteredProperty(CorrelationId correlationId) => Error.Create(
            ErrorCodes.ApplicationValidationInvalid,
            ErrorCategory.Validation,
            SafeReasonCode.ActionNotAllowed,
            UserMessageKey.Parse("errors.diagnostics.property_not_registered"),
            RetryDirective.DoNotRetry,
            correlationId);
    }

    public interface IProcessInstanceIdGenerator
    {
        ProcessInstanceId Create();
    }

    public interface IDiagnosticIdGenerator
    {
        DiagnosticId Create();
    }

    public interface IOdysseyLogger
    {
        bool IsEnabled(LogLevel level);
        void Write(LogEventV1 logEvent);
        void Write(LogLevel level, EventCode eventCode, SubsystemName subsystem, MessageTemplateKey messageTemplateKey, DiagnosticContext context, Func<IReadOnlyList<SafeLogProperty>>? safeProperties = null, ExceptionSummary? exceptionSummary = null);
    }

    public static class OdysseyEventCodes
    {
        public static readonly EventCode RuntimeStarting = EventCode.Parse("runtime.starting");
        public static readonly EventCode RuntimeReady = EventCode.Parse("runtime.ready");
        public static readonly EventCode RuntimeStartupFailed = EventCode.Parse("runtime.startup_failed");
        public static readonly EventCode RuntimeShutdownRequested = EventCode.Parse("runtime.shutdown_requested");
        public static readonly EventCode RuntimeShutdownCompleted = EventCode.Parse("runtime.shutdown_completed");
        public static readonly EventCode DiagnosticsDroppedEvents = EventCode.Parse("diagnostics.dropped_events");
        public static readonly EventCode DiagnosticsSinkFailed = EventCode.Parse("diagnostics.sink_failed");
        public static readonly EventCode DiagnosticsProbe = EventCode.Parse("diagnostics.probe");
        public static readonly EventCode CrashMarkerDetected = EventCode.Parse("diagnostics.crash_marker_detected");
        public static readonly EventCode CrashMarkerCompleted = EventCode.Parse("diagnostics.crash_marker_completed");
        public static readonly EventCode DeveloperProbeAccepted = EventCode.Parse("developer.probe_accepted");
        public static readonly EventCode DeveloperProbeRejected = EventCode.Parse("developer.probe_rejected");

        public static IReadOnlyList<EventCode> ActiveCodes { get; } = Array.AsReadOnly(new[]
        {
            RuntimeStarting,
            RuntimeReady,
            RuntimeStartupFailed,
            RuntimeShutdownRequested,
            RuntimeShutdownCompleted,
            DiagnosticsDroppedEvents,
            DiagnosticsSinkFailed,
            DiagnosticsProbe,
            CrashMarkerDetected,
            CrashMarkerCompleted,
            DeveloperProbeAccepted,
            DeveloperProbeRejected
        });
    }

    public static class DiagnosticSanitizers
    {
        public static string SanitizePath(string value) => PathSanitizer.Sanitize(value);
        public static string SanitizeEndpoint(string value) => EndpointSanitizer.Sanitize(value);
    }

    internal static class PathSanitizer
    {
        internal static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Path is required.", nameof(value));
            string normalized = value.Replace('\\', '/');
            int last = normalized.LastIndexOf('/');
            string file = last >= 0 ? normalized.Substring(last + 1) : normalized;
            if (string.IsNullOrWhiteSpace(file)) file = "path";
            return "path:" + SafeLogValue.BoundedText(file, 80).RenderedValue;
        }
    }

    internal static class EndpointSanitizer
    {
        internal static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Endpoint is required.", nameof(value));
            int separator = value.IndexOf(':');
            string host = separator >= 0 ? value.Substring(0, separator) : value;
            if (host.Length > 12) host = host.Substring(0, 12);
            return "endpoint:" + host.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + StableHash(value).ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static uint StableHash(string value)
        {
            uint hash = 2166136261u;
            for (int index = 0; index < value.Length; index++)
            {
                hash ^= value[index];
                hash *= 16777619u;
            }

            return hash;
        }
    }

    internal static class DiagnosticText
    {
        internal static bool TryParsePrefixedHex<T>(string? value, string prefix, int hexLength, out T result, Func<string, T> factory)
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

        internal static bool IsLowerToken(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > maxLength || value.Trim() != value) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-')) return false;
            }

            return true;
        }

        internal static bool IsDottedLowerIdentifier(string? value, int maxLength, int minSegments)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > maxLength || value.Trim() != value) return false;
            string[] segments = value.Split('.');
            if (segments.Length < minSegments) return false;
            for (int index = 0; index < segments.Length; index++)
            {
                if (!IsLowerToken(segments[index], maxLength)) return false;
            }

            return true;
        }

        internal static bool IsDottedUpperOrLowerIdentifier(string? value, int maxLength, int minSegments)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > maxLength || value.Trim() != value) return false;
            string[] segments = value.Split('.');
            if (segments.Length < minSegments) return false;
            for (int index = 0; index < segments.Length; index++)
            {
                if (segments[index].Length == 0) return false;
                for (int charIndex = 0; charIndex < segments[index].Length; charIndex++)
                {
                    char c = segments[index][charIndex];
                    if (!char.IsLetterOrDigit(c) && c != '_' && c != '-') return false;
                }
            }

            return true;
        }

        internal static bool IsSafeBoundedText(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > maxLength || value.Trim() != value) return false;
            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsControl(value[index])) return false;
            }

            return true;
        }

        internal static bool IsSafeFingerprint(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > maxLength || value.Trim() != value) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == ':')) return false;
            }

            return true;
        }
    }
}
