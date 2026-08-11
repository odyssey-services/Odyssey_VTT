using System;
using System.IO;
using Odyssey.Application.Diagnostics;

namespace Odyssey.Unity.Client
{
    public interface ICrashMarkerStore : IDisposable
    {
        string SanitizedMarkerPath { get; }
        bool PreviousMarkerWasUnfinished { get; }
        bool PreviousMarkerWasMalformed { get; }
        void Start(ProcessInstanceId processInstanceId);
        bool TryComplete();
    }

    public interface ICrashMarkerStoreFactory
    {
        ICrashMarkerStore Create(string directory);
    }

    public sealed class DefaultCrashMarkerStoreFactory : ICrashMarkerStoreFactory
    {
        public ICrashMarkerStore Create(string directory) => new CrashMarkerStore(directory);
    }

    public sealed class CrashMarkerStore : ICrashMarkerStore
    {
        public const string MarkerFileName = "process-started.json";
        private readonly string _directory;
        private readonly string _markerPath;
        private bool _completed;

        public CrashMarkerStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Crash marker directory is required.", nameof(directory));
            _directory = directory;
            _markerPath = Path.Combine(directory, MarkerFileName);
        }

        public string SanitizedMarkerPath => DiagnosticSanitizers.SanitizePath(_markerPath);
        public bool PreviousMarkerWasUnfinished { get; private set; }
        public bool PreviousMarkerWasMalformed { get; private set; }

        public void Start(ProcessInstanceId processInstanceId)
        {
            if (!processInstanceId.IsValid) throw new ArgumentException("ProcessInstanceId is required.", nameof(processInstanceId));
            Directory.CreateDirectory(_directory);
            MarkerState previous = ReadPreviousMarker();
            PreviousMarkerWasUnfinished = previous == MarkerState.Started;
            PreviousMarkerWasMalformed = previous == MarkerState.Malformed;
            File.WriteAllText(_markerPath, "{\"state\":\"started\",\"process\":\"" + processInstanceId + "\"}");
            _completed = false;
        }

        public bool TryComplete()
        {
            if (_completed) return true;
            try
            {
                Directory.CreateDirectory(_directory);
                File.WriteAllText(_markerPath, "{\"state\":\"completed\"}");
                File.Delete(_markerPath);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }

            _completed = true;
            return true;
        }

        public void Dispose()
        {
            TryComplete();
        }

        private MarkerState ReadPreviousMarker()
        {
            if (!File.Exists(_markerPath)) return MarkerState.Missing;
            string text;
            try
            {
                text = File.ReadAllText(_markerPath);
            }
            catch (IOException)
            {
                return MarkerState.Malformed;
            }
            catch (UnauthorizedAccessException)
            {
                return MarkerState.Malformed;
            }

            if (text.Length > 128) return MarkerState.Malformed;
            if (TryReadStarted(text)) return MarkerState.Started;
            if (string.Equals(text, "{\"state\":\"completed\"}", StringComparison.Ordinal)) return MarkerState.Completed;
            return MarkerState.Malformed;
        }

        private static bool TryReadStarted(string text)
        {
            const string prefix = "{\"state\":\"started\",\"process\":\"";
            const string suffix = "\"}";
            if (!text.StartsWith(prefix, StringComparison.Ordinal) || !text.EndsWith(suffix, StringComparison.Ordinal)) return false;
            string process = text.Substring(prefix.Length, text.Length - prefix.Length - suffix.Length);
            return ProcessInstanceId.TryParse(process, out _);
        }

        private enum MarkerState
        {
            Missing = 0,
            Started = 1,
            Completed = 2,
            Malformed = 3
        }
    }
}
