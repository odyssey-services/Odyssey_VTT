using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Odyssey.Application.Diagnostics;
using Odyssey.Application.Identity;
using Odyssey.Application.Time;
using Odyssey.Domain.Time;
using UnityEngine;

namespace Odyssey.Unity.Client
{
    public interface IDiagnosticSink
    {
        string Name { get; }
        bool TryWrite(LogEventV1 logEvent);
    }

    public interface IEmergencyDiagnosticSink
    {
        bool TryWrite(EmergencyDiagnosticRecord record);
    }

    public readonly struct EmergencyDiagnosticRecord
    {
        public EmergencyDiagnosticRecord(UtcInstant timestampUtc, EventCode eventCode, DiagnosticId? diagnosticId, string token)
        {
            if (!timestampUtc.IsValid) throw new ArgumentException("Timestamp is required.", nameof(timestampUtc));
            if (!eventCode.IsValid) throw new ArgumentException("EventCode is required.", nameof(eventCode));
            if (diagnosticId.HasValue && !diagnosticId.Value.IsValid) throw new ArgumentException("DiagnosticId must be valid.", nameof(diagnosticId));
            if (!IsSafeToken(token)) throw new ArgumentException("Emergency token is not safe.", nameof(token));
            TimestampUtc = timestampUtc;
            EventCode = eventCode;
            DiagnosticId = diagnosticId;
            Token = token;
        }

        public UtcInstant TimestampUtc { get; }
        public EventCode EventCode { get; }
        public DiagnosticId? DiagnosticId { get; }
        public string Token { get; }

        private static bool IsSafeToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token) || token!.Length > 64 || token.Trim() != token) return false;
            for (int index = 0; index < token.Length; index++)
            {
                char c = token[index];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '.' || c == '-')) return false;
            }

            return true;
        }
    }

    public sealed class InMemoryDiagnosticRingBuffer : IDiagnosticSink
    {
        public const int DefaultMaxEvents = 2000;
        public const int DefaultMaxBytes = 8 * 1024 * 1024;
        private readonly object _gate = new object();
        private readonly Queue<LogEventV1> _events = new Queue<LogEventV1>();
        private readonly int _maxEvents;
        private readonly int _maxBytes;
        private int _logicalBytes;

        public InMemoryDiagnosticRingBuffer(int maxEvents = DefaultMaxEvents, int maxBytes = DefaultMaxBytes)
        {
            if (maxEvents <= 0) throw new ArgumentOutOfRangeException(nameof(maxEvents));
            if (maxBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
            _maxEvents = maxEvents;
            _maxBytes = maxBytes;
        }

        public string Name => "memory_ring";
        public int Count { get { lock (_gate) return _events.Count; } }
        public int LogicalBytes { get { lock (_gate) return _logicalBytes; } }

        public IReadOnlyList<LogEventV1> Snapshot()
        {
            lock (_gate)
            {
                return _events.ToArray();
            }
        }

        public bool TryWrite(LogEventV1 logEvent)
        {
            if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));
            if (logEvent.EstimatedLogicalSize > _maxBytes) return false;
            lock (_gate)
            {
                _events.Enqueue(logEvent);
                _logicalBytes += logEvent.EstimatedLogicalSize;
                EvictUntilWithinLimit();
                return _events.Contains(logEvent);
            }
        }

        private void EvictUntilWithinLimit()
        {
            while (_events.Count > 0 && (_events.Count > _maxEvents || _logicalBytes > _maxBytes))
            {
                LogEventV1 removed = _events.Dequeue();
                _logicalBytes -= removed.EstimatedLogicalSize;
                if (_logicalBytes < 0) _logicalBytes = 0;
            }
        }
    }

    public sealed class EmergencyDiagnosticSink : IEmergencyDiagnosticSink
    {
        private readonly object _gate = new object();
        private readonly List<EmergencyDiagnosticRecord> _records = new List<EmergencyDiagnosticRecord>();

        public IReadOnlyList<EmergencyDiagnosticRecord> Snapshot()
        {
            lock (_gate)
            {
                return _records.ToArray();
            }
        }

        public bool TryWrite(EmergencyDiagnosticRecord record)
        {
            lock (_gate)
            {
                _records.Add(record);
                return true;
            }
        }
    }

    public sealed class FileEmergencyDiagnosticSink : IEmergencyDiagnosticSink
    {
        private readonly string _path;

        public FileEmergencyDiagnosticSink(string diagnosticsDirectory)
        {
            if (string.IsNullOrWhiteSpace(diagnosticsDirectory)) throw new ArgumentException("Diagnostics directory is required.", nameof(diagnosticsDirectory));
            _path = Path.Combine(diagnosticsDirectory, "emergency.log");
        }

        public bool TryWrite(EmergencyDiagnosticRecord record)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                string line = record.TimestampUtc + " " + record.EventCode + " " + (record.DiagnosticId.HasValue ? record.DiagnosticId.Value.ToString() : "diag_none") + " " + record.Token + Environment.NewLine;
                File.AppendAllText(_path, line);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public sealed class UnityConsoleDiagnosticSink : IDiagnosticSink
    {
        public string Name => "unity_console";

        public bool TryWrite(LogEventV1 logEvent)
        {
            if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));
            string line = logEvent.EventCode + " " + logEvent.MessageTemplateKey;
            if (logEvent.Level >= LogLevel.Error) UnityEngine.Debug.LogError(line);
            else if (logEvent.Level == LogLevel.Warning) UnityEngine.Debug.LogWarning(line);
            else UnityEngine.Debug.Log(line);
            return true;
        }
    }

    public sealed class BoundedDiagnosticRuntime : IOdysseyLogger, IDisposable
    {
        public const int DefaultQueueMaxEvents = 4096;
        public const int DefaultQueueMaxBytes = 16 * 1024 * 1024;
        private readonly object _gate = new object();
        private readonly Queue<LogEventV1> _queue = new Queue<LogEventV1>();
        private readonly EventCodeRegistry _registry;
        private readonly IWallClock _clock;
        private readonly IMonotonicClock _monotonicClock;
        private readonly int _maxEvents;
        private readonly int _maxBytes;
        private readonly List<IDiagnosticSink> _sinks;
        private readonly IEmergencyDiagnosticSink _emergencySink;
        private int _logicalBytes;
        private long _droppedTrace;
        private long _droppedDebug;
        private long _droppedInformation;
        private bool _isDraining;
        private bool _isDisposed;

        public BoundedDiagnosticRuntime(EventCodeRegistry registry, IWallClock clock, IMonotonicClock monotonicClock, IReadOnlyList<IDiagnosticSink> sinks, IEmergencyDiagnosticSink emergencySink, int maxEvents = DefaultQueueMaxEvents, int maxBytes = DefaultQueueMaxBytes, bool autoFlush = true)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _monotonicClock = monotonicClock ?? throw new ArgumentNullException(nameof(monotonicClock));
            if (sinks == null) throw new ArgumentNullException(nameof(sinks));
            _emergencySink = emergencySink ?? throw new ArgumentNullException(nameof(emergencySink));
            if (maxEvents <= 0) throw new ArgumentOutOfRangeException(nameof(maxEvents));
            if (maxBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
            _maxEvents = maxEvents;
            _maxBytes = maxBytes;
            AutoFlush = autoFlush;
            _sinks = new List<IDiagnosticSink>(sinks);
        }

        public bool AutoFlush { get; }
        public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;
        public int PendingCount { get { lock (_gate) return _queue.Count; } }
        public int PendingLogicalBytes { get { lock (_gate) return _logicalBytes; } }
        public long DroppedTraceCount { get { lock (_gate) return _droppedTrace; } }
        public long DroppedDebugCount { get { lock (_gate) return _droppedDebug; } }
        public long DroppedInformationCount { get { lock (_gate) return _droppedInformation; } }
        public long DroppedLowerPriorityCount => DroppedTraceCount + DroppedDebugCount + DroppedInformationCount;
        public IReadOnlyList<string> SinkNames
        {
            get
            {
                lock (_gate)
                {
                    string[] names = new string[_sinks.Count];
                    for (int index = 0; index < _sinks.Count; index++) names[index] = _sinks[index].Name;
                    return names;
                }
            }
        }

        public bool IsEnabled(LogLevel level, EventCode eventCode)
        {
            return !_isDisposed &&
                level >= MinimumLevel &&
                eventCode.IsValid &&
                _registry.Definitions.ContainsKey(eventCode);
        }

        public void Write(LogEventV1 logEvent)
        {
            if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));
            if (!IsEnabled(logEvent.Level, logEvent.EventCode)) return;
            if (_registry.Validate(logEvent).IsFailure)
            {
                throw new InvalidOperationException("Diagnostic event is not registered.");
            }

            Enqueue(logEvent);
            if (AutoFlush) Flush();
        }

        public void Write(LogLevel level, EventCode eventCode, SubsystemName subsystem, MessageTemplateKey messageTemplateKey, DiagnosticContext context, Func<IReadOnlyList<SafeLogProperty>>? safeProperties = null, ExceptionSummary? exceptionSummary = null)
        {
            if (!IsEnabled(level, eventCode)) return;
            if (context == null) throw new ArgumentNullException(nameof(context));
            IReadOnlyList<SafeLogProperty>? properties = safeProperties == null ? null : safeProperties();
            Write(new LogEventV1(
                _clock.GetUtcNow(),
                level,
                eventCode,
                subsystem,
                BuildIdAvailability.UnavailableNotYetComposed,
                context.ProcessInstanceId,
                messageTemplateKey,
                properties,
                context.CorrelationId,
                context.DiagnosticId,
                context.CommandId,
                context.SessionReference,
                exceptionSummary));
        }

        public void Flush()
        {
            FlushUntil(null);
        }

        public void Shutdown(TimeSpan budget)
        {
            if (_isDisposed) return;
            FlushUntil(budget);
            for (int index = _sinks.Count - 1; index >= 0; index--)
            {
                if (_sinks[index] is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch
                    {
                        WriteEmergency(OdysseyEventCodes.DiagnosticsSinkWriteFailed, null, "sink_exception");
                    }
                }
            }

            _isDisposed = true;
        }

        public void Dispose()
        {
            Shutdown(TimeSpan.FromSeconds(2));
        }

        internal void RecordEmergency(EventCode eventCode, DiagnosticId? diagnosticId, string token)
        {
            WriteEmergency(eventCode, diagnosticId, token);
        }

        private void FlushUntil(TimeSpan? budget)
        {
            MonotonicTimestamp started = _monotonicClock.GetTimestamp();
            while (true)
            {
                if (budget.HasValue && _monotonicClock.GetElapsedTime(started, _monotonicClock.GetTimestamp()) >= budget.Value)
                {
                    WriteEmergency(OdysseyEventCodes.DiagnosticsSinkWriteFailed, null, "drain_budget_exhausted");
                    return;
                }

                LogEventV1? next = null;
                lock (_gate)
                {
                    if (_isDraining || _queue.Count == 0) return;
                    _isDraining = true;
                    next = _queue.Dequeue();
                    _logicalBytes -= next.EstimatedLogicalSize;
                    if (_logicalBytes < 0) _logicalBytes = 0;
                }

                try
                {
                    Dispatch(next);
                }
                finally
                {
                    lock (_gate)
                    {
                        _isDraining = false;
                    }
                }
            }
        }

        private void Enqueue(LogEventV1 logEvent)
        {
            lock (_gate)
            {
                if (logEvent.EstimatedLogicalSize > _maxBytes)
                {
                    CountDrop(logEvent.Level);
                    WriteEmergency(logEvent.EventCode, logEvent.DiagnosticId, "event_too_large");
                    return;
                }

                if (WouldExceed(logEvent))
                {
                    DropLowerPriorityVictimsUntilFits(logEvent);
                    if (WouldExceed(logEvent) && logEvent.Level <= LogLevel.Information)
                    {
                        CountDrop(logEvent.Level);
                        return;
                    }

                    if (WouldExceed(logEvent))
                    {
                        WriteEmergency(logEvent.EventCode, logEvent.DiagnosticId, "queue_full");
                        return;
                    }
                }

                EmitDropCounterIfRecovered(logEvent);
                _queue.Enqueue(logEvent);
                _logicalBytes += logEvent.EstimatedLogicalSize;
            }
        }

        private bool WouldExceed(LogEventV1 logEvent)
        {
            return _queue.Count + 1 > _maxEvents || _logicalBytes + logEvent.EstimatedLogicalSize > _maxBytes;
        }

        private void DropLowerPriorityVictimsUntilFits(LogEventV1 incoming)
        {
            if (incoming.Level > LogLevel.Trace)
            {
                while (WouldExceed(incoming) && DropOne(LogLevel.Trace)) { }
            }

            if (incoming.Level > LogLevel.Debug)
            {
                while (WouldExceed(incoming) && DropOne(LogLevel.Debug)) { }
            }

            if (incoming.Level > LogLevel.Information)
            {
                while (WouldExceed(incoming) && DropOne(LogLevel.Information)) { }
            }
        }

        private bool DropOne(LogLevel level)
        {
            LogEventV1[] copy = _queue.ToArray();
            _queue.Clear();
            _logicalBytes = 0;
            bool removed = false;
            for (int index = 0; index < copy.Length; index++)
            {
                if (!removed && copy[index].Level == level)
                {
                    removed = true;
                    CountDrop(level);
                    continue;
                }

                _queue.Enqueue(copy[index]);
                _logicalBytes += copy[index].EstimatedLogicalSize;
            }

            return removed;
        }

        private void CountDrop(LogLevel level)
        {
            if (level == LogLevel.Trace) _droppedTrace++;
            else if (level == LogLevel.Debug) _droppedDebug++;
            else if (level == LogLevel.Information) _droppedInformation++;
        }

        private bool WouldExceed(LogEventV1 first, LogEventV1 second)
        {
            return _queue.Count + 2 > _maxEvents || _logicalBytes + first.EstimatedLogicalSize + second.EstimatedLogicalSize > _maxBytes;
        }

        private void EmitDropCounterIfRecovered(LogEventV1 incoming)
        {
            if ((_droppedTrace + _droppedDebug + _droppedInformation) <= 0) return;
            LogEventV1 dropEvent = new LogEventV1(
                _clock.GetUtcNow(),
                LogLevel.Warning,
                OdysseyEventCodes.DiagnosticsQueueEventsDropped,
                SubsystemName.Parse("diagnostics"),
                BuildIdAvailability.UnavailableNotYetComposed,
                incoming.ProcessInstanceId,
                MessageTemplateKey.Parse("log.diagnostics.queue.events_dropped"),
                new[]
                {
                    new SafeLogProperty(SafePropertyKey.Parse("trace_count"), SafeLogValue.Count(_droppedTrace)),
                    new SafeLogProperty(SafePropertyKey.Parse("debug_count"), SafeLogValue.Count(_droppedDebug)),
                    new SafeLogProperty(SafePropertyKey.Parse("information_count"), SafeLogValue.Count(_droppedInformation))
                });
            if (WouldExceed(dropEvent, incoming)) return;
            _droppedTrace = 0;
            _droppedDebug = 0;
            _droppedInformation = 0;
            _queue.Enqueue(dropEvent);
            _logicalBytes += dropEvent.EstimatedLogicalSize;
        }

        private void Dispatch(LogEventV1 logEvent)
        {
            for (int index = 0; index < _sinks.Count; index++)
            {
                try
                {
                    if (!_sinks[index].TryWrite(logEvent))
                    {
                        WriteEmergency(OdysseyEventCodes.DiagnosticsSinkWriteFailed, logEvent.DiagnosticId, "sink_write_failed");
                    }
                }
                catch
                {
                    WriteEmergency(OdysseyEventCodes.DiagnosticsSinkWriteFailed, logEvent.DiagnosticId, "sink_exception");
                }
            }
        }

        private void WriteEmergency(EventCode eventCode, DiagnosticId? diagnosticId, string token)
        {
            _emergencySink.TryWrite(new EmergencyDiagnosticRecord(_clock.GetUtcNow(), eventCode, diagnosticId, token));
        }
    }

    internal sealed class IncidentDeduplicator
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
        private readonly int _capacity;

        internal IncidentDeduplicator(int capacity = 64)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        internal bool Record(BoundedDiagnosticRuntime diagnostics, IWallClock clock, ProcessInstanceId processInstanceId, IDiagnosticIdGenerator ids, Exception exception, SubsystemName subsystem, out DiagnosticId diagnosticId)
        {
            if (diagnostics == null) throw new ArgumentNullException(nameof(diagnostics));
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            if (ids == null) throw new ArgumentNullException(nameof(ids));
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            DiagnosticId createdDiagnosticId = ids.Create();
            diagnosticId = createdDiagnosticId;
            ExceptionSummary summary = ExceptionSummary.FromException(exception, subsystem, createdDiagnosticId);
            string key = summary.Category + "|" + subsystem;
            int count;
            bool first;
            lock (_gate)
            {
                if (!_counts.ContainsKey(key) && _counts.Count >= _capacity) _counts.Clear();
                first = !_counts.TryGetValue(key, out count);
                count = first ? 1 : Math.Min(count + 1, 9999);
                _counts[key] = count;
            }

            diagnostics.Write(LogLevel.Error, OdysseyEventCodes.DiagnosticsIncidentUnexpected, SubsystemName.Parse("diagnostics"), MessageTemplateKey.Parse("log.diagnostics.incident.unexpected"), new DiagnosticContext(processInstanceId, diagnosticId: diagnosticId), () => new[]
            {
                new SafeLogProperty(SafePropertyKey.Parse("diagnostic_id"), SafeLogValue.TechnicalIdentifier(createdDiagnosticId.ToString())),
                new SafeLogProperty(SafePropertyKey.Parse("incident_category"), SafeLogValue.Code(ToIncidentCategory(summary.Category))),
                new SafeLogProperty(SafePropertyKey.Parse("repeat_count"), SafeLogValue.Count(count))
            }, first ? summary : (ExceptionSummary?)null);
            return first;
        }

        private static string ToIncidentCategory(ExceptionCategory category)
        {
            switch (category)
            {
                case ExceptionCategory.InvalidOperation: return "invalid_operation";
                case ExceptionCategory.IoFailure: return "io_failure";
                case ExceptionCategory.AccessDenied: return "access_denied";
                case ExceptionCategory.Cancelled: return "cancelled";
                default: return "unexpected";
            }
        }
    }

    internal interface IPlatformExceptionHookSource
    {
        event Action<Exception> UnhandledException;
        event Action<Exception> UnobservedTaskException;
    }

    internal sealed class DotNetPlatformExceptionHookSource : IPlatformExceptionHookSource, IDisposable
    {
        public event Action<Exception>? UnhandledException;
        public event Action<Exception>? UnobservedTaskException;

        public DotNetPlatformExceptionHookSource()
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            if (args.ExceptionObject is Exception exception) UnhandledException?.Invoke(exception);
            else UnhandledException?.Invoke(new InvalidOperationException("non_exception_unhandled"));
        }

        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs args)
        {
            UnobservedTaskException?.Invoke(args.Exception);
        }
    }

    internal sealed class PlatformFatalHookOwner : IDisposable
    {
        private readonly IPlatformExceptionHookSource _source;
        private readonly BoundedDiagnosticRuntime _diagnostics;
        private readonly IWallClock _clock;
        private readonly ProcessInstanceId _processInstanceId;
        private readonly IDiagnosticIdGenerator _diagnosticIds;
        private readonly IncidentDeduplicator _deduplicator;
        private bool _recording;
        private bool _disposed;

        public PlatformFatalHookOwner(IPlatformExceptionHookSource source, BoundedDiagnosticRuntime diagnostics, IWallClock clock, ProcessInstanceId processInstanceId, IDiagnosticIdGenerator diagnosticIds, IncidentDeduplicator deduplicator)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _processInstanceId = processInstanceId;
            _diagnosticIds = diagnosticIds ?? throw new ArgumentNullException(nameof(diagnosticIds));
            _deduplicator = deduplicator ?? throw new ArgumentNullException(nameof(deduplicator));
            _source.UnhandledException += Record;
            _source.UnobservedTaskException += Record;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _source.UnhandledException -= Record;
            _source.UnobservedTaskException -= Record;
            if (_source is IDisposable disposable) disposable.Dispose();
            _disposed = true;
        }

        private void Record(Exception exception)
        {
            if (_disposed || _recording) return;
            _recording = true;
            try
            {
                _deduplicator.Record(_diagnostics, _clock, _processInstanceId, _diagnosticIds, exception, SubsystemName.Parse("app"), out DiagnosticId diagnosticId);
                _diagnostics.RecordEmergency(OdysseyEventCodes.DiagnosticsIncidentUnexpected, diagnosticId, "platform_fatal_hook");
            }
            finally
            {
                _recording = false;
            }
        }
    }

    public sealed class UnityWallClock : IWallClock
    {
        public UtcInstant GetUtcNow()
        {
            return UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow);
        }
    }

    public sealed class UnityMonotonicClock : IMonotonicClock
    {
        private readonly object _gate = new object();
        private readonly Dictionary<MonotonicTimestamp, long> _ticks = new Dictionary<MonotonicTimestamp, long>();
        public MonotonicTimestamp GetTimestamp()
        {
            long ticks = Stopwatch.GetTimestamp();
            MonotonicTimestamp timestamp = MonotonicTimestamp.FromTestTicks(ticks);
            lock (_gate)
            {
                _ticks[timestamp] = ticks;
            }

            return timestamp;
        }

        public TimeSpan GetElapsedTime(MonotonicTimestamp start, MonotonicTimestamp end)
        {
            lock (_gate)
            {
                return TimeSpan.FromSeconds((double)(_ticks[end] - _ticks[start]) / Stopwatch.Frequency);
            }
        }
    }

    public sealed class GuidProcessInstanceIdGenerator : IProcessInstanceIdGenerator
    {
        public ProcessInstanceId Create()
        {
            return ProcessInstanceId.Parse("proc_" + Guid.NewGuid().ToString("N"));
        }
    }

    public sealed class GuidDiagnosticIdGenerator : IDiagnosticIdGenerator
    {
        public DiagnosticId Create()
        {
            return DiagnosticId.Parse("diag_" + Guid.NewGuid().ToString("N"));
        }
    }
}
