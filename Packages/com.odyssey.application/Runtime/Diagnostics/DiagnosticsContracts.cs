using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Text;
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

    public enum DiagnosticDataClassification
    {
        Public = 1,
        OperationalSafe = 2,
        Personal = 3,
        HiddenGameplay = 4,
        Secret = 5
    }

    public enum SafeLogValueKind
    {
        Boolean = 1,
        Integer = 2,
        Decimal = 3,
        Code = 4,
        Duration = 5,
        Timestamp = 6,
        ByteCount = 7,
        TechnicalIdentifier = 8,
        Fingerprint = 9,
        BoundedText = 10,
        SanitizedPath = 11,
        SanitizedEndpoint = 12
    }

    public enum BuildIdAvailability
    {
        UnavailableNotYetComposed = 1,
        Available = 2
    }

    public enum ExceptionCategory
    {
        InvalidOperation = 1,
        IoFailure = 2,
        AccessDenied = 3,
        Cancelled = 4,
        Unexpected = 5
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
            if (DiagnosticText.IsDottedLowerIdentifier(value, 96, 3))
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
            if (value != null && value.StartsWith("log.", StringComparison.Ordinal) && DiagnosticText.IsDottedLowerIdentifier(value, 128, 4))
            {
                key = new MessageTemplateKey(value);
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
        public static bool operator ==(MessageTemplateKey left, MessageTemplateKey right) => left.Equals(right);
        public static bool operator !=(MessageTemplateKey left, MessageTemplateKey right) => !left.Equals(right);
    }

    public readonly struct SubsystemName : IEquatable<SubsystemName>
    {
        private readonly string _value;

        private SubsystemName(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out SubsystemName subsystem)
        {
            if (DiagnosticText.IsLowerToken(value, 48))
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
        public static bool operator ==(SubsystemName left, SubsystemName right) => left.Equals(right);
        public static bool operator !=(SubsystemName left, SubsystemName right) => !left.Equals(right);
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
        public static bool operator ==(SafePropertyKey left, SafePropertyKey right) => left.Equals(right);
        public static bool operator !=(SafePropertyKey left, SafePropertyKey right) => !left.Equals(right);
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
        private ExceptionSummary(ExceptionCategory category, SubsystemName subsystem, int innerExceptionCount, bool transient, DiagnosticId diagnosticId)
        {
            if (!Enum.IsDefined(typeof(ExceptionCategory), category)) throw new ArgumentOutOfRangeException(nameof(category));
            if (!subsystem.IsValid) throw new ArgumentException("Subsystem is required.", nameof(subsystem));
            if (innerExceptionCount < 0 || innerExceptionCount > 16) throw new ArgumentOutOfRangeException(nameof(innerExceptionCount));
            if (!diagnosticId.IsValid) throw new ArgumentException("DiagnosticId is required.", nameof(diagnosticId));
            Category = category;
            Subsystem = subsystem;
            InnerExceptionCount = innerExceptionCount;
            IsTransient = transient;
            DiagnosticId = diagnosticId;
        }

        public ExceptionCategory Category { get; }
        public SubsystemName Subsystem { get; }
        public int InnerExceptionCount { get; }
        public bool IsTransient { get; }
        public DiagnosticId DiagnosticId { get; }
        public bool IsValid => Subsystem.IsValid && DiagnosticId.IsValid && InnerExceptionCount >= 0;

        public static ExceptionSummary FromException(Exception exception, SubsystemName subsystem, DiagnosticId diagnosticId)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            ExceptionCategory category = ExceptionCategory.Unexpected;
            bool transient = false;
            if (exception is InvalidOperationException) category = ExceptionCategory.InvalidOperation;
            else if (exception is OperationCanceledException) category = ExceptionCategory.Cancelled;
            else if (exception is UnauthorizedAccessException) category = ExceptionCategory.AccessDenied;
            else if (exception is System.IO.IOException)
            {
                category = ExceptionCategory.IoFailure;
                transient = true;
            }

            return new ExceptionSummary(category, subsystem, CountInnerExceptions(exception), transient, diagnosticId);
        }

        public static ExceptionSummary Rehydrate(ExceptionCategory category, SubsystemName subsystem, int innerExceptionCount, bool transient, DiagnosticId diagnosticId)
        {
            return new ExceptionSummary(category, subsystem, innerExceptionCount, transient, diagnosticId);
        }

        private static int CountInnerExceptions(Exception exception)
        {
            int count = 0;
            Exception? current = exception.InnerException;
            while (current != null && count < 16)
            {
                count++;
                current = current.InnerException;
            }

            return count;
        }
    }

    public sealed class SafeLogValue
    {
        private SafeLogValue(DiagnosticDataClassification classification, SafeLogValueKind valueKind, string renderedValue, int logicalSize, int originalScalarCount, bool wasTruncated)
        {
            if (!Enum.IsDefined(typeof(DiagnosticDataClassification), classification)) throw new ArgumentOutOfRangeException(nameof(classification));
            if (!Enum.IsDefined(typeof(SafeLogValueKind), valueKind)) throw new ArgumentOutOfRangeException(nameof(valueKind));
            Classification = classification;
            ValueKind = valueKind;
            RenderedValue = renderedValue ?? throw new ArgumentNullException(nameof(renderedValue));
            LogicalSize = logicalSize;
            OriginalScalarCount = originalScalarCount;
            WasTruncated = wasTruncated;
        }

        public DiagnosticDataClassification Classification { get; }
        public SafeLogValueKind ValueKind { get; }
        public string RenderedValue { get; }
        public int LogicalSize { get; }
        public int OriginalScalarCount { get; }
        public bool WasTruncated { get; }

        public static SafeLogValue Boolean(bool value) => Create(DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.Boolean, value ? "true" : "false");
        public static SafeLogValue Code(string value) => DiagnosticText.IsDottedLowerIdentifier(value, 96, 1) ? Create(DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.Code, value) : throw new ArgumentException("Code is not safe.", nameof(value));
        public static SafeLogValue Count(long value) => Create(DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.Integer, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        public static SafeLogValue Duration(TimeSpan value) => Create(DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.Duration, value.TotalMilliseconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "ms");
        public static SafeLogValue Timestamp(UtcInstant value) => value.IsValid ? Create(DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.Timestamp, value.ToString()) : throw new ArgumentException("Timestamp is required.", nameof(value));
        public static SafeLogValue ByteCount(long value) => value >= 0 ? Create(DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.ByteCount, value.ToString(System.Globalization.CultureInfo.InvariantCulture)) : throw new ArgumentOutOfRangeException(nameof(value));
        public static SafeLogValue TechnicalIdentifier(string value) => DiagnosticText.IsSafeFingerprint(value, 128) ? Create(DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.TechnicalIdentifier, value) : throw new ArgumentException("Identifier is not safe.", nameof(value));
        public static SafeLogValue Fingerprint(string value) => DiagnosticText.IsSafeFingerprint(value, 128) ? Create(DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.Fingerprint, value) : throw new ArgumentException("Fingerprint is not safe.", nameof(value));
        public static SafeLogValue SanitizedPath(string value) => Create(DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.SanitizedPath, PathSanitizer.Sanitize(value));
        public static SafeLogValue SanitizedEndpoint(string value) => Create(DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.SanitizedEndpoint, EndpointSanitizer.Sanitize(value));

        public static SafeLogValue Rehydrate(DiagnosticDataClassification classification, SafeLogValueKind kind, string renderedValue, int logicalSize, int originalScalarCount, bool wasTruncated)
        {
            if (renderedValue == null) throw new ArgumentNullException(nameof(renderedValue));
            if (classification != DiagnosticDataClassification.Public && classification != DiagnosticDataClassification.OperationalSafe) throw new ArgumentException("Only safe diagnostic classifications can be rehydrated.", nameof(classification));
            if (!Enum.IsDefined(typeof(SafeLogValueKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (logicalSize < 0 || originalScalarCount < 0) throw new ArgumentOutOfRangeException(nameof(logicalSize));
            if (ContainsControlCharacter(renderedValue)) throw new ArgumentException("Rendered value must not contain control characters.", nameof(renderedValue));
            return new SafeLogValue(classification, kind, renderedValue, logicalSize, originalScalarCount, wasTruncated);
        }

        public static SafeLogValue BoundedText(string value, int maxScalars = 256, DiagnosticDataClassification classification = DiagnosticDataClassification.OperationalSafe)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (maxScalars <= 0 || maxScalars > 256) throw new ArgumentOutOfRangeException(nameof(maxScalars));
            if (classification != DiagnosticDataClassification.Public && classification != DiagnosticDataClassification.OperationalSafe) throw new ArgumentException("Bounded text must be public or operational-safe.", nameof(classification));
            if (ContainsControlCharacter(value)) throw new ArgumentException("Bounded text must not contain control characters.", nameof(value));

            int originalScalars = CountScalars(value);
            bool truncated = originalScalars > maxScalars;
            string rendered = truncated ? TruncateScalars(value, maxScalars) : value;
            return new SafeLogValue(classification, SafeLogValueKind.BoundedText, rendered, Encoding.UTF8.GetByteCount(rendered), originalScalars, truncated);
        }

        private static SafeLogValue Create(DiagnosticDataClassification classification, SafeLogValueKind kind, string rendered)
        {
            return new SafeLogValue(classification, kind, rendered, Encoding.UTF8.GetByteCount(rendered), CountScalars(rendered), false);
        }

        private static bool ContainsControlCharacter(string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsControl(value[index])) return true;
            }

            return false;
        }

        private static int CountScalars(string value)
        {
            int count = 0;
            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsHighSurrogate(value[index]) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1])) index++;
                count++;
            }

            return count;
        }

        private static string TakeScalars(string value, int scalarCount)
        {
            if (scalarCount <= 0) return string.Empty;
            int index = 0;
            int count = 0;
            while (index < value.Length && count < scalarCount)
            {
                if (char.IsHighSurrogate(value[index]) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1])) index += 2;
                else index++;
                count++;
            }

            return value.Substring(0, index);
        }

        private static string TruncateScalars(string value, int maxScalars)
        {
            const string longMarker = "[truncated]";
            const string shortMarker = "~";
            if (maxScalars >= CountScalars(longMarker))
            {
                return TakeScalars(value, maxScalars - CountScalars(longMarker)) + longMarker;
            }

            return TakeScalars(value, maxScalars - 1) + shortMarker;
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
        public int EstimatedLogicalSize => 128 + SumPropertySize(_safeProperties) + (ExceptionSummary.HasValue ? 96 : 0);

        private static ReadOnlyCollection<SafeLogProperty> CopyProperties(IReadOnlyList<SafeLogProperty>? source)
        {
            if (source == null || source.Count == 0) return Array.AsReadOnly(Array.Empty<SafeLogProperty>());
            if (source.Count > 20) throw new ArgumentOutOfRangeException(nameof(source));
            SafeLogProperty[] copy = new SafeLogProperty[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                if (!source[index].IsValid) throw new ArgumentException("Safe property is required.", nameof(source));
                if (source[index].Value.Classification != DiagnosticDataClassification.Public && source[index].Value.Classification != DiagnosticDataClassification.OperationalSafe) throw new ArgumentException("Unsafe diagnostic property classification is not allowed.", nameof(source));
                copy[index] = source[index];
            }

            return Array.AsReadOnly(copy);
        }

        private static int SumPropertySize(IReadOnlyList<SafeLogProperty> properties)
        {
            int total = 0;
            for (int index = 0; index < properties.Count; index++)
            {
                total += Encoding.UTF8.GetByteCount(properties[index].Key.ToString()) + properties[index].Value.LogicalSize;
            }

            return total;
        }
    }

    public sealed class EventPropertyDefinition
    {
        public EventPropertyDefinition(SafePropertyKey key, DiagnosticDataClassification classification, SafeLogValueKind valueKind)
        {
            if (!key.IsValid) throw new ArgumentException("Property key is required.", nameof(key));
            if (!Enum.IsDefined(typeof(DiagnosticDataClassification), classification)) throw new ArgumentOutOfRangeException(nameof(classification));
            if (!Enum.IsDefined(typeof(SafeLogValueKind), valueKind)) throw new ArgumentOutOfRangeException(nameof(valueKind));
            if (classification != DiagnosticDataClassification.Public && classification != DiagnosticDataClassification.OperationalSafe) throw new ArgumentException("Registry may only allow public or operational-safe properties.", nameof(classification));
            Key = key;
            Classification = classification;
            ValueKind = valueKind;
        }

        public SafePropertyKey Key { get; }
        public DiagnosticDataClassification Classification { get; }
        public SafeLogValueKind ValueKind { get; }

        public bool Allows(SafeLogProperty property)
        {
            return property.IsValid && property.Key == Key && property.Value.Classification == Classification && property.Value.ValueKind == ValueKind;
        }
    }

    public sealed class EventCodeDefinition
    {
        private readonly ReadOnlyDictionary<SafePropertyKey, EventPropertyDefinition> _properties;

        public EventCodeDefinition(EventCode eventCode, SubsystemName ownerSubsystem, LogLevel defaultLevel, MessageTemplateKey messageTemplateKey, IReadOnlyList<EventPropertyDefinition> allowedProperties, string purpose, EventCodeStatus status)
        {
            if (!eventCode.IsValid) throw new ArgumentException("EventCode is required.", nameof(eventCode));
            if (!ownerSubsystem.IsValid) throw new ArgumentException("Owner subsystem is required.", nameof(ownerSubsystem));
            if (!Enum.IsDefined(typeof(LogLevel), defaultLevel)) throw new ArgumentOutOfRangeException(nameof(defaultLevel));
            if (!messageTemplateKey.IsValid) throw new ArgumentException("MessageTemplateKey is required.", nameof(messageTemplateKey));
            if (!DiagnosticText.IsSafePurposeText(purpose, 256)) throw new ArgumentException("Purpose is not safe.", nameof(purpose));
            if (!Enum.IsDefined(typeof(EventCodeStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            EventCode = eventCode;
            OwnerSubsystem = ownerSubsystem;
            DefaultLevel = defaultLevel;
            MessageTemplateKey = messageTemplateKey;
            Purpose = purpose;
            Status = status;
            _properties = CopyProperties(allowedProperties);
        }

        public EventCode EventCode { get; }
        public SubsystemName OwnerSubsystem { get; }
        public LogLevel DefaultLevel { get; }
        public MessageTemplateKey MessageTemplateKey { get; }
        public IReadOnlyDictionary<SafePropertyKey, EventPropertyDefinition> AllowedProperties => _properties;
        public string Purpose { get; }
        public EventCodeStatus Status { get; }

        public bool Allows(SafeLogProperty property)
        {
            return property.IsValid && _properties.TryGetValue(property.Key, out EventPropertyDefinition definition) && definition.Allows(property);
        }

        private static ReadOnlyDictionary<SafePropertyKey, EventPropertyDefinition> CopyProperties(IReadOnlyList<EventPropertyDefinition>? source)
        {
            Dictionary<SafePropertyKey, EventPropertyDefinition> copy = new Dictionary<SafePropertyKey, EventPropertyDefinition>();
            if (source != null)
            {
                for (int index = 0; index < source.Count; index++)
                {
                    EventPropertyDefinition definition = source[index] ?? throw new ArgumentException("Property definition is required.", nameof(source));
                    if (copy.ContainsKey(definition.Key)) throw new ArgumentException("Duplicate property definition.", nameof(source));
                    copy.Add(definition.Key, definition);
                }
            }

            return new ReadOnlyDictionary<SafePropertyKey, EventPropertyDefinition>(copy);
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

            if (definition.MessageTemplateKey != logEvent.MessageTemplateKey || definition.OwnerSubsystem != logEvent.Subsystem)
            {
                return Result.Failure(DiagnosticContractErrors.UnregisteredProperty(correlationId));
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
                Define(OdysseyEventCodes.AppStartupStarted, "app", LogLevel.Information, "log.app.startup.started", "Runtime startup began.", Props(("phase", DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.Code))),
                Define(OdysseyEventCodes.AppStartupCompleted, "app", LogLevel.Information, "log.app.startup.completed", "Runtime reached Ready.", Props(("state", DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.Code), ("duration_ms", DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.Duration))),
                Define(OdysseyEventCodes.AppStartupFailed, "app", LogLevel.Error, "log.app.startup.failed", "Runtime startup failed safely.", Props(("phase", DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.Code), ("reason", DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.Code), ("diagnostic_id", DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.TechnicalIdentifier))),
                Define(OdysseyEventCodes.AppShutdownRequested, "app", LogLevel.Information, "log.app.shutdown.requested", "Runtime shutdown was requested.", Props(("state", DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.Code))),
                Define(OdysseyEventCodes.AppShutdownCompleted, "app", LogLevel.Information, "log.app.shutdown.completed", "Runtime shutdown completed.", Props(("duration_ms", DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.Duration))),
                Define(OdysseyEventCodes.AppBootstrapDuplicateRejected, "app", LogLevel.Warning, "log.app.bootstrap.duplicate_rejected", "Duplicate bootstrap attempt was rejected.", Props(("state", DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.Code))),
                Define(OdysseyEventCodes.DiagnosticsQueueEventsDropped, "diagnostics", LogLevel.Warning, "log.diagnostics.queue.events_dropped", "Diagnostics queue dropped lower-priority events under pressure.", Props(("trace_count", DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.Integer), ("debug_count", DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.Integer), ("information_count", DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.Integer))),
                Define(OdysseyEventCodes.DiagnosticsSinkWriteFailed, "diagnostics", LogLevel.Warning, "log.diagnostics.sink.write_failed", "A diagnostics sink failed and fallback handled the event.", Props(("sink", DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.Code), ("diagnostic_id", DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.TechnicalIdentifier))),
                Define(OdysseyEventCodes.DiagnosticsProbeEmitted, "diagnostics", LogLevel.Information, "log.diagnostics.probe.emitted", "Developer Shell emitted a safe diagnostic probe.", Props(("probe", DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.Code))),
                Define(OdysseyEventCodes.DiagnosticsCrashPreviousUncleanDetected, "diagnostics", LogLevel.Warning, "log.diagnostics.crash.previous_unclean_detected", "A previous unfinished crash marker was detected.", Props(("marker", DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.SanitizedPath))),
                Define(OdysseyEventCodes.DiagnosticsCrashMarkerCompleted, "diagnostics", LogLevel.Information, "log.diagnostics.crash.marker_completed", "The crash marker was completed during clean shutdown.", Props(("marker", DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.SanitizedPath))),
                Define(OdysseyEventCodes.DiagnosticsIncidentUnexpected, "diagnostics", LogLevel.Error, "log.diagnostics.incident.unexpected", "An unexpected incident was recorded safely.", Props(("diagnostic_id", DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.TechnicalIdentifier), ("incident_category", DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.Code), ("repeat_count", DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.Integer))),
                Define(OdysseyEventCodes.DeveloperShellProbeAccepted, "developer", LogLevel.Information, "log.developer.shell.probe_accepted", "DeveloperShell probe command was accepted.", Props(("command_id", DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.TechnicalIdentifier), ("result_status", DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.Code))),
                Define(OdysseyEventCodes.DeveloperShellProbeRejected, "developer", LogLevel.Warning, "log.developer.shell.probe_rejected", "DeveloperShell probe command was rejected.", Props(("command_id", DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.TechnicalIdentifier), ("result_status", DiagnosticDataClassification.OperationalSafe, SafeLogValueKind.Code)))
            });
        }

        private static EventCodeDefinition Define(EventCode code, string subsystem, LogLevel level, string messageTemplateKey, string purpose, IReadOnlyList<EventPropertyDefinition> properties)
        {
            return new EventCodeDefinition(code, SubsystemName.Parse(subsystem), level, MessageTemplateKey.Parse(messageTemplateKey), properties, purpose, EventCodeStatus.Active);
        }

        private static IReadOnlyList<EventPropertyDefinition> Props(params (string Key, DiagnosticDataClassification Classification, SafeLogValueKind Kind)[] properties)
        {
            EventPropertyDefinition[] result = new EventPropertyDefinition[properties.Length];
            for (int index = 0; index < properties.Length; index++)
            {
                result[index] = new EventPropertyDefinition(SafePropertyKey.Parse(properties[index].Key), properties[index].Classification, properties[index].Kind);
            }

            return Array.AsReadOnly(result);
        }
    }

    internal static class DiagnosticContractErrors
    {
        internal static Error UnknownEventCode(CorrelationId correlationId) => Error.Create(
            ErrorCodes.ApplicationValidationInvalid,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.application.validation_invalid"),
            RetryDirective.DoNotRetry,
            correlationId);

        internal static Error UnregisteredProperty(CorrelationId correlationId) => Error.Create(
            ErrorCodes.ApplicationValidationInvalid,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.application.validation_invalid"),
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
        bool IsEnabled(LogLevel level, EventCode eventCode);
        void Write(LogEventV1 logEvent);
        void Write(LogLevel level, EventCode eventCode, SubsystemName subsystem, MessageTemplateKey messageTemplateKey, DiagnosticContext context, Func<IReadOnlyList<SafeLogProperty>>? safeProperties = null, ExceptionSummary? exceptionSummary = null);
    }

    public static class OdysseyEventCodes
    {
        public static readonly EventCode AppStartupStarted = EventCode.Parse("app.startup.started");
        public static readonly EventCode AppStartupCompleted = EventCode.Parse("app.startup.completed");
        public static readonly EventCode AppStartupFailed = EventCode.Parse("app.startup.failed");
        public static readonly EventCode AppShutdownRequested = EventCode.Parse("app.shutdown.requested");
        public static readonly EventCode AppShutdownCompleted = EventCode.Parse("app.shutdown.completed");
        public static readonly EventCode AppBootstrapDuplicateRejected = EventCode.Parse("app.bootstrap.duplicate_rejected");
        public static readonly EventCode DiagnosticsQueueEventsDropped = EventCode.Parse("diagnostics.queue.events_dropped");
        public static readonly EventCode DiagnosticsSinkWriteFailed = EventCode.Parse("diagnostics.sink.write_failed");
        public static readonly EventCode DiagnosticsProbeEmitted = EventCode.Parse("diagnostics.probe.emitted");
        public static readonly EventCode DiagnosticsCrashPreviousUncleanDetected = EventCode.Parse("diagnostics.crash.previous_unclean_detected");
        public static readonly EventCode DiagnosticsCrashMarkerCompleted = EventCode.Parse("diagnostics.crash.marker_completed");
        public static readonly EventCode DiagnosticsIncidentUnexpected = EventCode.Parse("diagnostics.incident.unexpected");
        public static readonly EventCode DeveloperShellProbeAccepted = EventCode.Parse("developer.shell.probe_accepted");
        public static readonly EventCode DeveloperShellProbeRejected = EventCode.Parse("developer.shell.probe_rejected");

        public static IReadOnlyList<EventCode> ActiveCodes { get; } = Array.AsReadOnly(new[]
        {
            AppStartupStarted,
            AppStartupCompleted,
            AppStartupFailed,
            AppShutdownRequested,
            AppShutdownCompleted,
            AppBootstrapDuplicateRejected,
            DiagnosticsQueueEventsDropped,
            DiagnosticsSinkWriteFailed,
            DiagnosticsProbeEmitted,
            DiagnosticsCrashPreviousUncleanDetected,
            DiagnosticsCrashMarkerCompleted,
            DiagnosticsIncidentUnexpected,
            DeveloperShellProbeAccepted,
            DeveloperShellProbeRejected
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
            string lower = value.Trim().ToLowerInvariant();
            if (lower.StartsWith("localhost", StringComparison.Ordinal) || lower.StartsWith("127.", StringComparison.Ordinal) || lower.StartsWith("[::1]", StringComparison.Ordinal) || lower.StartsWith("::1", StringComparison.Ordinal)) return "endpoint:loopback";
            if (lower.StartsWith("10.", StringComparison.Ordinal) || lower.StartsWith("192.168.", StringComparison.Ordinal) || lower.StartsWith("172.16.", StringComparison.Ordinal) || lower.StartsWith("172.17.", StringComparison.Ordinal) || lower.StartsWith("172.18.", StringComparison.Ordinal) || lower.StartsWith("172.19.", StringComparison.Ordinal) || lower.StartsWith("172.2", StringComparison.Ordinal) || lower.StartsWith("172.30.", StringComparison.Ordinal) || lower.StartsWith("172.31.", StringComparison.Ordinal)) return "endpoint:local";
            if (lower.Contains("relay")) return "endpoint:relay";
            return "endpoint:unknown";
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

        internal static bool IsSafePurposeText(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > maxLength || value.Trim() != value) return false;
            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsControl(value[index])) return false;
            }

            return true;
        }
    }
}
