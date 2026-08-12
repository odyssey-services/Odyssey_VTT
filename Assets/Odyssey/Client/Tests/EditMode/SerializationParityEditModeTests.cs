using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Odyssey.Application.Diagnostics;
using Odyssey.Application.Serialization;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Diagnostics;
using Odyssey.Unity.Client;

namespace Odyssey.Tests.Unity.EditMode
{
    public sealed class SerializationParityEditModeTests
    {
        private const string ExpectedPayloadHash = "297210561a33067f767e70b9529e758bba64d4994519a8de02f33d2de9d9308b";
        private const string ExpectedFingerprint = "fp_34cb57ecc14fe9985455ed66e42a75e641c7dda3131274d1e8b15a6a0d1ba347";
        private const string ExpectedDiagnosticHash = "95a9b6007c2add9f0faf00f55519dd1abff72d41b19ae7469d07131324111c52";
        private const string ExpectedManifestHash = "ab596e69df0d4a59e3940d36006edf04782c4998e8c51e66b4facd5f2d4cbf92";

        [Test]
        public void SerializationSmokeVectorMatchesFrozenDotNetVectorInUnityMono()
        {
            SerializationSmokeResult result = SerializationSmoke.Run().Value;
            Assert.That(result.PayloadHash, Is.EqualTo(ExpectedPayloadHash));
            Assert.That(result.Fingerprint, Is.EqualTo(ExpectedFingerprint));
            Assert.That(result.DiagnosticHash, Is.EqualTo(ExpectedDiagnosticHash));
            Assert.That(result.ManifestHash, Is.EqualTo(ExpectedManifestHash));
        }

        [Test]
        public void RollingJsonlDiagnosticSinkCoversSecretRotationRetentionAndActiveFile()
        {
            string directory = Path.Combine(Path.GetTempPath(), "odyssey-jsonl-" + Guid.NewGuid().ToString("N"));
            try
            {
                FakeClock clock = new FakeClock(new DateTimeOffset(2026, 8, 12, 0, 0, 0, TimeSpan.Zero));
                using RollingJsonlDiagnosticSink sink = new RollingJsonlDiagnosticSink(directory, clock);
                sink.Write(CreateLogEvent(clock));

                string file = Directory.GetFiles(directory, "*.jsonl").Single();
                byte[] bytes = File.ReadAllBytes(file);
                Assert.That(bytes[0], Is.Not.EqualTo(0xEF));
                Assert.That(bytes[bytes.Length - 1], Is.EqualTo((byte)'\n'));
                string text = File.ReadAllText(file);
                Assert.That(text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Length, Is.EqualTo(1));
                Assert.That(text, Does.Contain("\"contractType\":\"odyssey.diagnostics.log.event\""));
                Assert.That(text, Does.Not.Contain("C:\\"));
                Assert.That(text, Does.Not.Contain("raw_secret_token"));

                clock.Set(new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.Zero));
                sink.Write(CreateLogEvent(clock));
                Assert.That(Directory.GetFiles(directory, "20260813*.jsonl").Length, Is.EqualTo(1));

                File.WriteAllText(Path.Combine(directory, "20260813-001.jsonl"), string.Empty);
                using (FileStream stream = new FileStream(Path.Combine(directory, "20260813-001.jsonl"), FileMode.Open, FileAccess.Write, FileShare.Read))
                {
                    stream.SetLength(RollingJsonlDiagnosticSink.MaxFileBytes - 1);
                }

                using RollingJsonlDiagnosticSink sizeSink = new RollingJsonlDiagnosticSink(directory, clock);
                sizeSink.Write(CreateLogEvent(clock));
                Assert.That(Directory.GetFiles(directory, "20260813-002.jsonl").Length, Is.EqualTo(1));

                for (int index = 0; index < RollingJsonlDiagnosticSink.MaxRetainedFiles + 3; index++)
                {
                    string old = Path.Combine(directory, "202607" + (10 + index).ToString("00") + ".jsonl");
                    File.WriteAllText(old, "{}\n");
                    File.SetLastWriteTimeUtc(old, new DateTime(2026, 7, 10 + index, 0, 0, 0, DateTimeKind.Utc));
                }

                File.WriteAllText(Path.Combine(directory, "20260701.jsonl"), "{}\n");
                File.SetLastWriteTimeUtc(Path.Combine(directory, "20260701.jsonl"), new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
                sink.Write(CreateLogEvent(clock));
                Assert.That(File.Exists(file), Is.True);
                Assert.That(File.Exists(Path.Combine(directory, "20260701.jsonl")), Is.False);
                Assert.That(Directory.GetFiles(directory, "*.jsonl").Any(path => Path.GetFileName(path).StartsWith("20260813", StringComparison.Ordinal)), Is.True);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void RollingJsonlDiagnosticSinkStartsNewFileForNewProcessSameDay()
        {
            string directory = Path.Combine(Path.GetTempPath(), "odyssey-jsonl-" + Guid.NewGuid().ToString("N"));
            try
            {
                FakeClock clock = new FakeClock(new DateTimeOffset(2026, 8, 12, 0, 0, 0, TimeSpan.Zero));
                using (RollingJsonlDiagnosticSink first = new RollingJsonlDiagnosticSink(directory, clock))
                {
                    first.Write(CreateLogEvent(clock));
                }

                using (RollingJsonlDiagnosticSink second = new RollingJsonlDiagnosticSink(directory, clock))
                {
                    second.Write(CreateLogEvent(clock));
                }

                Assert.That(File.Exists(Path.Combine(directory, "20260812.jsonl")), Is.True);
                Assert.That(File.Exists(Path.Combine(directory, "20260812-001.jsonl")), Is.True);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void ProductionRuntimeCompositionContainsRollingJsonlSinkOnceAndDisposesIt()
        {
            string directory = Path.Combine(Path.GetTempPath(), "odyssey-runtime-" + Guid.NewGuid().ToString("N"));
            try
            {
                var result = new OdysseyRuntimeCompositionRoot().Start(OdysseyRuntimeConfiguration.DeveloperShell(directory));
                Assert.That(result.IsSuccess, Is.True);
                AppRuntime runtime = result.Value;
                Assert.That(runtime.Diagnostics.SinkNames.Count(name => name == "memory_ring"), Is.EqualTo(1));
                Assert.That(runtime.Diagnostics.SinkNames.Count(name => name == "rolling_jsonl"), Is.EqualTo(1));
                Assert.That(runtime.Diagnostics.SinkNames.Count(name => name == "unity_console"), Is.EqualTo(1));

                runtime.EmitDiagnosticProbe();
                runtime.Shutdown();

                Assert.That(Directory.GetFiles(directory, "*.jsonl").Length, Is.GreaterThanOrEqualTo(1));
                Assert.That(runtime.State, Is.EqualTo(OdysseyRuntimeState.Stopped));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static LogEventV1 CreateLogEvent(FakeClock clock)
        {
            return new LogEventV1(
                UtcInstant.FromDateTimeOffset(clock.GetUtcNow()),
                LogLevel.Information,
                OdysseyEventCodes.DiagnosticsProbeEmitted,
                SubsystemName.Parse("diagnostics"),
                BuildIdAvailability.UnavailableNotYetComposed,
                ProcessInstanceId.Parse("proc_0123456789abcdef0123456789abcdef"),
                MessageTemplateKey.Parse("log.diagnostics.probe.emitted"),
                new[] { new SafeLogProperty(SafePropertyKey.Parse("probe"), SafeLogValue.Code("serialization")) },
                CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"));
        }

        private sealed class FakeClock : IDiagnosticSinkClock
        {
            private DateTimeOffset _now;

            public FakeClock(DateTimeOffset now)
            {
                _now = now;
            }

            public void Set(DateTimeOffset now)
            {
                _now = now;
            }

            public DateTimeOffset GetUtcNow() => _now;
        }
    }
}
