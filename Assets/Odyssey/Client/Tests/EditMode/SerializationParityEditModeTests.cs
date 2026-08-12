using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Odyssey.Application.Diagnostics;
using Odyssey.Application.Serialization;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Diagnostics;

namespace Odyssey.Tests.Unity.EditMode
{
    public sealed class SerializationParityEditModeTests
    {
        [Test]
        public void SerializationSmokeVectorRunsInUnityMono()
        {
            SerializationSmokeResult result = SerializationSmoke.Run().Value;
            Assert.That(result.Fingerprint, Does.StartWith("fp_"));
            Assert.That(result.PayloadHash, Has.Length.EqualTo(64));
            Assert.That(result.DiagnosticHash, Has.Length.EqualTo(64));
            Assert.That(result.ManifestHash, Has.Length.EqualTo(64));
        }

        [Test]
        public void RollingJsonlDiagnosticSinkWritesUtf8LinesAndRetainsActiveFile()
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
                Assert.That(text, Does.Contain("\"contractType\":\"odyssey.diagnostics.log_event\""));
                Assert.That(text, Does.Not.Contain("C:\\"));

                File.WriteAllText(Path.Combine(directory, "20260701.jsonl"), "{}\n");
                File.SetLastWriteTimeUtc(Path.Combine(directory, "20260701.jsonl"), new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
                sink.Write(CreateLogEvent(clock));
                Assert.That(File.Exists(file), Is.True);
                Assert.That(File.Exists(Path.Combine(directory, "20260701.jsonl")), Is.False);
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
            private readonly DateTimeOffset _now;

            public FakeClock(DateTimeOffset now)
            {
                _now = now;
            }

            public DateTimeOffset GetUtcNow() => _now;
        }
    }
}
