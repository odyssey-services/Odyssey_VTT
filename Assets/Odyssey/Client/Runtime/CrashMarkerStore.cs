using System;
using System.IO;
using Odyssey.Application.Diagnostics;

namespace Odyssey.Unity.Client
{
    public sealed class CrashMarkerStore : IDisposable
    {
        public const string MarkerFileName = "process-started.marker";
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
            File.WriteAllText(_markerPath, "state=started\nprocess=" + processInstanceId + "\n");
            _completed = false;
        }

        public void Complete()
        {
            if (_completed) return;
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_markerPath, "state=completed\n");
            try
            {
                File.Delete(_markerPath);
            }
            catch (IOException)
            {
                _completed = true;
                return;
            }
            catch (UnauthorizedAccessException)
            {
                _completed = true;
                return;
            }

            _completed = true;
        }

        public void Dispose()
        {
            Complete();
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

            if (text.StartsWith("state=started\n", StringComparison.Ordinal)) return MarkerState.Started;
            if (text.StartsWith("state=completed\n", StringComparison.Ordinal)) return MarkerState.Completed;
            return MarkerState.Malformed;
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
