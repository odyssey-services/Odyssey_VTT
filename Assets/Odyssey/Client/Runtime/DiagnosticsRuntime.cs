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
            lock (_gate)
            {
                _events.Enqueue(logEvent);
                _logicalBytes += logEvent.EstimatedLogicalSize;
                EvictUntilWithinLimit();
                return true;
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

    public sealed class EmergencyDiagnosticSink : IDiagnosticSink
    {
        private readonly object _gate = new object();
        private readonly List<LogEventV1> _events = new List<LogEventV1>();

        public string Name => "emergency";
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
            lock (_gate)
            {
                _events.Add(logEvent);
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
            if (logEvent.Level >= LogLevel.Error)
            {
                Debug.LogError(line);
            }
            else if (logEvent.Level == LogLevel.Warning)
            {
                Debug.LogWarning(line);
            }
            else
            {
                Debug.Log(line);
            }

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
        private readonly EventCode _dropEventCode = OdysseyEventCodes.DiagnosticsDroppedEvents;
        private readonly SubsystemName _diagnosticsSubsystem = SubsystemName.Parse("diagnostics");
        private readonly MessageTemplateKey _dropMessage = MessageTemplateKey.Parse("diagnostics.dropped_events");
        private readonly int _maxEvents;
        private readonly int _maxBytes;
        private readonly List<IDiagnosticSink> _sinks;
        private readonly EmergencyDiagnosticSink _emergencySink;
        private int _logicalBytes;
        private long _droppedLowerPriority;
        private bool _isDraining;
        private bool _isDisposed;

        public BoundedDiagnosticRuntime(EventCodeRegistry registry, IWallClock clock, IReadOnlyList<IDiagnosticSink> sinks, EmergencyDiagnosticSink emergencySink, int maxEvents = DefaultQueueMaxEvents, int maxBytes = DefaultQueueMaxBytes, bool autoFlush = true)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
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
        public long DroppedLowerPriorityCount { get { lock (_gate) return _droppedLowerPriority; } }

        public bool IsEnabled(LogLevel level)
        {
            return !_isDisposed && level >= MinimumLevel;
        }

        public void Write(LogEventV1 logEvent)
        {
            if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));
            if (!IsEnabled(logEvent.Level)) return;

            if (_registry.Validate(logEvent).IsFailure)
            {
                throw new InvalidOperationException("Diagnostic event is not registered.");
            }

            Enqueue(logEvent);
            if (AutoFlush)
            {
                Flush();
            }
        }

        public void Write(LogLevel level, EventCode eventCode, SubsystemName subsystem, MessageTemplateKey messageTemplateKey, DiagnosticContext context, Func<IReadOnlyList<SafeLogProperty>>? safeProperties = null, ExceptionSummary? exceptionSummary = null)
        {
            if (!IsEnabled(level)) return;
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
            while (true)
            {
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

        public void Shutdown(TimeSpan budget)
        {
            Flush();
            _isDisposed = true;
        }

        public void Dispose()
        {
            Shutdown(TimeSpan.FromSeconds(2));
        }

        private void Enqueue(LogEventV1 logEvent)
        {
            lock (_gate)
            {
                if (WouldExceed(logEvent))
                {
                    if (logEvent.Level <= LogLevel.Information)
                    {
                        _droppedLowerPriority++;
                        return;
                    }

                    TryDropLowerPriority();
                    if (WouldExceed(logEvent))
                    {
                        _emergencySink.TryWrite(logEvent);
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

        private void TryDropLowerPriority()
        {
            if (_queue.Count == 0) return;
            LogEventV1[] copy = _queue.ToArray();
            _queue.Clear();
            _logicalBytes = 0;
            bool removed = false;
            for (int index = 0; index < copy.Length; index++)
            {
                if (!removed && copy[index].Level <= LogLevel.Information)
                {
                    removed = true;
                    _droppedLowerPriority++;
                    continue;
                }

                _queue.Enqueue(copy[index]);
                _logicalBytes += copy[index].EstimatedLogicalSize;
            }
        }

        private void EmitDropCounterIfRecovered(ProcessInstanceId processInstanceId)
        {
            if (_droppedLowerPriority <= 0 || WouldExceedDropCounter()) return;
            long dropped = _droppedLowerPriority;
            _droppedLowerPriority = 0;
            LogEventV1 dropEvent = new LogEventV1(
                _clock.GetUtcNow(),
                LogLevel.Warning,
                _dropEventCode,
                _diagnosticsSubsystem,
                BuildIdAvailability.UnavailableNotYetComposed,
                processInstanceId,
                _dropMessage,
                new[]
                {
                    new SafeLogProperty(SafePropertyKey.Parse("dropped_count"), SafeLogValue.Count(dropped)),
                    new SafeLogProperty(SafePropertyKey.Parse("level"), SafeLogValue.Code("information"))
                });
            _queue.Enqueue(dropEvent);
            _logicalBytes += dropEvent.EstimatedLogicalSize;
        }

        private bool WouldExceedDropCounter()
        {
            return _queue.Count + 1 > _maxEvents;
        }

        private void Dispatch(LogEventV1 logEvent)
        {
            for (int index = 0; index < _sinks.Count; index++)
            {
                try
                {
                    if (!_sinks[index].TryWrite(logEvent))
                    {
                        _emergencySink.TryWrite(logEvent);
                    }
                }
                catch
                {
                    _emergencySink.TryWrite(logEvent);
                }
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
