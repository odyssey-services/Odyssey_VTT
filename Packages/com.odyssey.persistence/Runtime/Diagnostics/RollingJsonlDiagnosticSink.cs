using System;
using System.IO;
using System.Linq;
using System.Text;
using Odyssey.Application.Diagnostics;
using Odyssey.Application.Serialization;

namespace Odyssey.Persistence.Diagnostics
{
    public interface IDiagnosticSinkClock
    {
        DateTimeOffset GetUtcNow();
    }

    public sealed class RollingJsonlDiagnosticSink : IOdysseyLogger, IDisposable
    {
        public const long MaxFileBytes = 10 * 1024 * 1024;
        public const int MaxRetainedFiles = 10;
        public const int MaxRetainedAgeDays = 14;
        public const long MaxRetainedTotalBytes = 100 * 1024 * 1024;
        private readonly string _directory;
        private readonly IDiagnosticSinkClock _clock;
        private readonly LogEventV1JsonCodec _codec = new LogEventV1JsonCodec();
        private readonly object _gate = new object();
        private string? _activePath;
        private DateTime _activeDateUtc;
        private int _activeSequence;
        private bool _disposed;

        public RollingJsonlDiagnosticSink(string directory, IDiagnosticSinkClock clock)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Directory is required.", nameof(directory));
            _directory = directory;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Directory.CreateDirectory(_directory);
        }

        public bool IsEnabled(LogLevel level, EventCode eventCode)
        {
            return !_disposed && eventCode.IsValid && Enum.IsDefined(typeof(LogLevel), level);
        }

        public void Write(LogEventV1 logEvent)
        {
            if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));
            lock (_gate)
            {
                if (_disposed) return;
                DateTime utcDate = _clock.GetUtcNow().UtcDateTime.Date;
                EnsureActiveFile(utcDate);
                JsonPayload payload = _codec.Write(logEvent).Value;
                byte[] newline = Encoding.UTF8.GetBytes("\n");
                FileInfo activeFile = new FileInfo(_activePath!);
                long currentLength = activeFile.Exists ? activeFile.Length : 0;
                long projected = currentLength + payload.Bytes.Length + newline.Length;
                if (projected > MaxFileBytes)
                {
                    _activeSequence++;
                    _activePath = CreatePath(utcDate, _activeSequence);
                }

                using FileStream stream = new FileStream(_activePath!, FileMode.Append, FileAccess.Write, FileShare.Read);
                stream.Write(payload.Bytes, 0, payload.Bytes.Length);
                stream.Write(newline, 0, newline.Length);
                ApplyRetention();
            }
        }

        public void Write(LogLevel level, EventCode eventCode, SubsystemName subsystem, MessageTemplateKey messageTemplateKey, DiagnosticContext context, Func<System.Collections.Generic.IReadOnlyList<SafeLogProperty>>? safeProperties = null, ExceptionSummary? exceptionSummary = null)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!IsEnabled(level, eventCode)) return;
            Write(new LogEventV1(
                Odyssey.Domain.Time.UtcInstant.FromDateTimeOffset(_clock.GetUtcNow()),
                level,
                eventCode,
                subsystem,
                BuildIdAvailability.UnavailableNotYetComposed,
                context.ProcessInstanceId,
                messageTemplateKey,
                safeProperties == null ? Array.Empty<SafeLogProperty>() : safeProperties(),
                context.CorrelationId,
                context.DiagnosticId,
                context.CommandId,
                context.SessionReference,
                exceptionSummary));
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
            }
        }

        private void EnsureActiveFile(DateTime utcDate)
        {
            if (_activePath != null && _activeDateUtc == utcDate && File.Exists(_activePath)) return;
            _activeDateUtc = utcDate;
            _activeSequence = 0;
            _activePath = CreatePath(utcDate, _activeSequence);
        }

        private string CreatePath(DateTime utcDate, int sequence)
        {
            string fileName = sequence == 0
                ? utcDate.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture) + ".jsonl"
                : utcDate.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture) + "-" + sequence.ToString("000", System.Globalization.CultureInfo.InvariantCulture) + ".jsonl";
            return Path.Combine(_directory, fileName);
        }

        private void ApplyRetention()
        {
            FileInfo[] files = new DirectoryInfo(_directory).GetFiles("*.jsonl")
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToArray();
            DateTimeOffset cutoff = _clock.GetUtcNow().AddDays(-MaxRetainedAgeDays);
            long total = 0;
            int kept = 0;
            foreach (FileInfo file in files)
            {
                if (string.Equals(file.FullName, _activePath, StringComparison.OrdinalIgnoreCase))
                {
                    kept++;
                    total += file.Length;
                    continue;
                }

                bool delete = file.LastWriteTimeUtc < cutoff.UtcDateTime || kept >= MaxRetainedFiles || total + file.Length > MaxRetainedTotalBytes;
                if (delete)
                {
                    file.Delete();
                    continue;
                }

                kept++;
                total += file.Length;
            }
        }
    }
}
