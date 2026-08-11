using System;
using System.IO;
using Odyssey.Application.Diagnostics;

namespace Odyssey.Unity.Client
{
    public sealed class CrashMarkerStore : IDisposable
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

        public void Start(ProcessInstanceId processInstanceId)
        {
            if (!processInstanceId.IsValid) throw new ArgumentException("ProcessInstanceId is required.", nameof(processInstanceId));
            Directory.CreateDirectory(_directory);
            PreviousMarkerWasUnfinished = File.Exists(_markerPath);
            File.WriteAllText(_markerPath, "{\"state\":\"started\",\"process\":\"" + processInstanceId + "\"}");
            _completed = false;
        }

        public void Complete()
        {
            if (_completed) return;
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_markerPath, "{\"state\":\"completed\"}");
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
    }
}
