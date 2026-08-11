using System;
using System.Collections.Generic;
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
            if (string.IsNullOrWhiteSpace(token) || token.Length > 64) throw new ArgumentException("Emergency token is required.", nameof(token));
            TimestampUtc = timestampUtc;
            EventCode = eventCode;
            DiagnosticId = diagnosticId;
            Token = token;
        }

        public UtcInstant TimestampUtc { get; }
        public EventCode EventCode { get; }
        public DiagnosticId? DiagnosticId { get; }
        public string Token { get; }
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

    public sealed class UnityConsoleDiagnosticSink : IDiagnosticSink
    {
        public string Name => "unity_console";

        public bool TryWrite(LogEventV1 logEvent)
        {
            if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));
            string line = logEvent.EventCode + " " + logEvent.MessageTemplateKey;
            if (logEvent.Level >= LogLevel.Error) Debug.LogError(line);
            else if (logEvent.Level == LogLevel.Warning) Debug.LogWarning(line);
            else Debug.Log(line);
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
            FlushUntil(budget);
            _isDisposed = true;
        }

        public void Dispose()
        {
            Shutdown(TimeSpan.FromSeconds(2));
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
                    if (logEvent.Level <= LogLevel.Information)
                    {
                        CountDrop(logEvent.Level);
                        return;
                    }

                    DropLowestPriorityVictimsUntilFits(logEvent);
                    if (WouldExceed(logEvent))
                    {
                        WriteEmergency(logEvent.EventCode, logEvent.DiagnosticId, "queue_full");
                        return;
                    }
                }

                EmitDropCounterIfRecovered(logEvent.ProcessInstanceId);
                _queue.Enqueue(logEvent);
                _logicalBytes += logEvent.EstimatedLogicalSize;
            }
        }

        private bool WouldExceed(LogEventV1 logEvent)
        {
            return _queue.Count + 1 > _maxEvents || _logicalBytes + logEvent.EstimatedLogicalSize > _maxBytes;
        }

        private void DropLowestPriorityVictimsUntilFits(LogEventV1 incoming)
        {
            while (WouldExceed(incoming) && DropOne(LogLevel.Trace)) { }
            while (WouldExceed(incoming) && DropOne(LogLevel.Debug)) { }
            while (WouldExceed(incoming) && DropOne(LogLevel.Information)) { }
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

        private void EmitDropCounterIfRecovered(ProcessInstanceId processInstanceId)
        {
            if ((_droppedTrace + _droppedDebug + _droppedInformation) <= 0) return;
            LogEventV1 dropEvent = new LogEventV1(
                _clock.GetUtcNow(),
                LogLevel.Warning,
                OdysseyEventCodes.DiagnosticsQueueEventsDropped,
                SubsystemName.Parse("diagnostics"),
                BuildIdAvailability.UnavailableNotYetComposed,
                processInstanceId,
                MessageTemplateKey.Parse("log.diagnostics.queue.events_dropped"),
                new[]
                {
                    new SafeLogProperty(SafePropertyKey.Parse("trace_count"), SafeLogValue.Count(_droppedTrace)),
                    new SafeLogProperty(SafePropertyKey.Parse("debug_count"), SafeLogValue.Count(_droppedDebug)),
                    new SafeLogProperty(SafePropertyKey.Parse("information_count"), SafeLogValue.Count(_droppedInformation))
                });
            if (WouldExceed(dropEvent)) return;
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
        private readonly int _started = Environment.TickCount;
        public MonotonicTimestamp GetTimestamp()
        {
            long ticks = unchecked(Environment.TickCount - _started);
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
                return TimeSpan.FromMilliseconds(_ticks[end] - _ticks[start]);
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
