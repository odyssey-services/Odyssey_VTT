using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Diagnostics;
using Odyssey.Application.Identity;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Events;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Unity.Client;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Tests.Unity.EditMode
{
    public sealed class RuntimeCompositionAndDiagnosticsTests
    {
        [Test]
        public void RuntimeCompositionStartsInStartingAndRejectsUnsupportedProfiles()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            Result<AppRuntime> result = new OdysseyRuntimeCompositionRoot().Start(OdysseyRuntimeConfiguration.DeveloperShell(directory.Path), DeterministicSettings());
            Result<AppRuntime> unsupported = new OdysseyRuntimeCompositionRoot().Start(
                new OdysseyRuntimeConfiguration(OdysseyRuntimeProfile.Production, DevelopmentAdapterMode.ExplicitDeveloperShell, directory.Path),
                DeterministicSettings());

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.State, Is.EqualTo(OdysseyRuntimeState.Starting));
            Assert.That(result.Value.Profile, Is.EqualTo(OdysseyRuntimeProfile.DeveloperShell));
            Assert.That(unsupported.IsFailure, Is.True);
            Assert.That(unsupported.Error.Code, Is.EqualTo(ErrorCodes.ApplicationBootstrapConfigurationInvalid));
            result.Value.Shutdown();
        }

        [Test]
        public void CancellationReturnsPreciseSafeFailureWithoutPublishingRuntime()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Result<AppRuntime> cancelled = new OdysseyRuntimeCompositionRoot().Start(
                OdysseyRuntimeConfiguration.DeveloperShell(directory.Path),
                DeterministicSettings(),
                cancellation.Token);

            Assert.That(cancelled.IsFailure, Is.True);
            Assert.That(cancelled.Error.Code, Is.EqualTo(ErrorCodes.ApplicationBootstrapInitializationCancelled));
            Assert.That(cancelled.Error.SafeReasonCode, Is.EqualTo(SafeReasonCode.OperationCancelled));
        }

        [Test]
        public void PresentationSuccessTransitionsRuntimeReadyAndShutdownIsIdempotent()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            Result<AppRuntime> result = new OdysseyRuntimeCompositionRoot().Start(OdysseyRuntimeConfiguration.DeveloperShell(directory.Path), DeterministicSettings());
            Assert.That(result.IsSuccess, Is.True);
            PresentationRuntime presentation = new PresentationRuntime();
            Assert.That(result.Value.AttachPresentationRuntime(presentation).IsSuccess, Is.True);
            Assert.That(result.Value.State, Is.EqualTo(OdysseyRuntimeState.Ready));
            result.Value.Shutdown();
            result.Value.Shutdown();
            Assert.That(result.Value.State, Is.EqualTo(OdysseyRuntimeState.Stopped));
            Assert.That(result.Value.ShutdownSideEffects, Is.EqualTo(1));
            Assert.That(presentation.IsDisposed, Is.True);
        }

        [Test]
        public void DeveloperShellProbeRecordsAcceptedBatchAndSafeRejectedResult()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            Result<AppRuntime> result = new OdysseyRuntimeCompositionRoot().Start(OdysseyRuntimeConfiguration.DeveloperShell(directory.Path), DeterministicSettings());
            Assert.That(result.IsSuccess, Is.True);

            Result<CommandResult> accepted = result.Value.RunAcceptedProbe();
            Result<CommandResult> rejected = result.Value.RunRejectedProbe();

            Assert.That(accepted.IsSuccess, Is.True);
            Assert.That(accepted.Value.Status, Is.EqualTo(CommandResultStatus.Accepted));
            Assert.That(rejected.IsSuccess, Is.True);
            Assert.That(rejected.Value.Status, Is.EqualTo(CommandResultStatus.Rejected));
            Assert.That(result.Value.DeveloperProbe.AcceptedCommitCount, Is.EqualTo(1));
            Assert.That(result.Value.DeveloperProbe.RejectedCommitCount, Is.EqualTo(1));
            Assert.That(result.Value.DeveloperProbe.EventBatchCommitCount, Is.EqualTo(1));
            Assert.That(accepted.Value.CommandId, Is.Not.EqualTo(rejected.Value.CommandId));
            result.Value.Shutdown();
        }

        [Test]
        public void DiagnosticsRuntimeHonorsLazyFilteringPriorityPressureAndEmergencyFallback()
        {
            TestClock clock = new TestClock();
            TestMonotonicClock monotonic = new TestMonotonicClock();
            EmergencyDiagnosticSink emergency = new EmergencyDiagnosticSink();
            InMemoryDiagnosticRingBuffer ring = new InMemoryDiagnosticRingBuffer(10, 4096);
            BoundedDiagnosticRuntime diagnostics = new BoundedDiagnosticRuntime(EventCodeRegistry.CreateDefault(), clock, monotonic, new IDiagnosticSink[] { new FailingSink(), ring }, emergency, maxEvents: 2, maxBytes: 4096, autoFlush: false);
            diagnostics.MinimumLevel = LogLevel.Warning;
            bool evaluated = false;
            diagnostics.Write(LogLevel.Debug, OdysseyEventCodes.DiagnosticsProbeEmitted, SubsystemName.Parse("diagnostics"), MessageTemplateKey.Parse("log.diagnostics.probe.emitted"), new DiagnosticContext(TestIds.Process), () =>
            {
                evaluated = true;
                return Array.Empty<SafeLogProperty>();
            });
            Assert.That(evaluated, Is.False);

            diagnostics.MinimumLevel = LogLevel.Trace;
            diagnostics.Write(CreateDiagnosticsProbe(LogLevel.Trace, clock));
            diagnostics.Write(CreateDiagnosticsProbe(LogLevel.Information, clock));
            diagnostics.Write(CreateShutdown(LogLevel.Warning, clock));
            diagnostics.Write(CreateDiagnosticsProbe(LogLevel.Warning, clock));
            diagnostics.Flush();
            IReadOnlyList<LogEventV1> flushed = ring.Snapshot();
            Assert.That(flushed, Has.Some.Matches<LogEventV1>(entry => entry.EventCode == OdysseyEventCodes.DiagnosticsQueueEventsDropped));
            Assert.That(flushed, Has.Some.Matches<LogEventV1>(entry => entry.Level == LogLevel.Warning));
            Assert.That(diagnostics.DroppedTraceCount + diagnostics.DroppedInformationCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(emergency.Snapshot().Count, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void RingBufferEnforcesCountAndByteLimitsAndRejectsOversizedEvent()
        {
            InMemoryDiagnosticRingBuffer ring = new InMemoryDiagnosticRingBuffer(10, 1200);
            TestClock clock = new TestClock();
            Assert.That(ring.TryWrite(CreateDiagnosticsProbe(LogLevel.Information, clock, SafeLogValue.BoundedText(new string('a', 256)))), Is.True);
            Assert.That(ring.TryWrite(CreateDiagnosticsProbe(LogLevel.Information, clock, SafeLogValue.BoundedText(new string('b', 256)))), Is.True);
            Assert.That(ring.LogicalBytes, Is.LessThanOrEqualTo(1200));
            InMemoryDiagnosticRingBuffer tinyRing = new InMemoryDiagnosticRingBuffer(10, 64);
            Assert.That(tinyRing.TryWrite(CreateDiagnosticsProbe(LogLevel.Information, clock, SafeLogValue.BoundedText(new string('c', 256)))), Is.False);
        }

        [Test]
        public void CrashMarkerParsesStartedCompletedAndMalformedStatesSafely()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            File.WriteAllText(Path.Combine(directory.Path, CrashMarkerStore.MarkerFileName), "state=started\nprocess=proc_0123456789abcdef0123456789abcdef\n");
            using (CrashMarkerStore marker = new CrashMarkerStore(directory.Path))
            {
                marker.Start(TestIds.Process);
                Assert.That(marker.PreviousMarkerWasUnfinished, Is.True);
            }

            File.WriteAllText(Path.Combine(directory.Path, CrashMarkerStore.MarkerFileName), "state=completed\n");
            using (CrashMarkerStore marker = new CrashMarkerStore(directory.Path))
            {
                marker.Start(TestIds.Process);
                Assert.That(marker.PreviousMarkerWasUnfinished, Is.False);
            }

            File.WriteAllText(Path.Combine(directory.Path, CrashMarkerStore.MarkerFileName), "not-json-not-marker");
            using (CrashMarkerStore marker = new CrashMarkerStore(directory.Path))
            {
                marker.Start(TestIds.Process);
                Assert.That(marker.PreviousMarkerWasUnfinished, Is.False);
                Assert.That(marker.PreviousMarkerWasMalformed, Is.True);
                marker.Complete();
                marker.Complete();
            }
        }

        [Test]
        public void AppShellEntryPointInitializesNarrowFacadeAndDisposesSubscriptions()
        {
            GameObject gameObject = new GameObject("AppShell Entry Test");
            try
            {
                gameObject.AddComponent<UIDocument>();
                AppShellEntryPoint entryPoint = gameObject.AddComponent<AppShellEntryPoint>();
                RecordingFacade facade = new RecordingFacade();
                Result<PresentationRuntime> result = entryPoint.Initialize(facade);
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(entryPoint.IsInitialized, Is.True);
                Assert.That(result.Value.IsDisposed, Is.False);
                result.Value.Dispose();
                Assert.That(result.Value.IsDisposed, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static RuntimeCompositionSettings DeterministicSettings()
        {
            return new RuntimeCompositionSettings(
                new TestClock(),
                new TestMonotonicClock(),
                new TestProcessIds(),
                new TestDiagnosticIds(),
                new TestTechnicalIds(),
                Array.Empty<IDiagnosticSink>(),
                false);
        }

        private static LogEventV1 CreateDiagnosticsProbe(LogLevel level, IWallClock clock, SafeLogValue? value = null, int payloadBytes = 0)
        {
            SafeLogValue probe = value ?? SafeLogValue.Code("developer_shell");
            if (payloadBytes > 0) probe = SafeLogValue.BoundedText(new string('x', Math.Min(payloadBytes, 256)));
            return new LogEventV1(
                clock.GetUtcNow(),
                level,
                OdysseyEventCodes.DiagnosticsProbeEmitted,
                SubsystemName.Parse("diagnostics"),
                BuildIdAvailability.UnavailableNotYetComposed,
                TestIds.Process,
                MessageTemplateKey.Parse("log.diagnostics.probe.emitted"),
                new[] { new SafeLogProperty(SafePropertyKey.Parse("probe"), probe) });
        }

        private static LogEventV1 CreateShutdown(LogLevel level, IWallClock clock)
        {
            return new LogEventV1(
                clock.GetUtcNow(),
                level,
                OdysseyEventCodes.AppShutdownRequested,
                SubsystemName.Parse("app"),
                BuildIdAvailability.UnavailableNotYetComposed,
                TestIds.Process,
                MessageTemplateKey.Parse("log.app.shutdown.requested"),
                new[] { new SafeLogProperty(SafePropertyKey.Parse("state"), SafeLogValue.Code("shutting_down")) });
        }

        private sealed class RecordingFacade : IDeveloperShellFacade
        {
            public OdysseyRuntimeState RuntimeState => OdysseyRuntimeState.Ready;
            public OdysseyRuntimeProfile RuntimeProfile => OdysseyRuntimeProfile.DeveloperShell;
            public BuildIdAvailability BuildIdentityAvailability => BuildIdAvailability.UnavailableNotYetComposed;
            public Result<CommandResult> RunAcceptedProbe() => throw new NotSupportedException();
            public Result<CommandResult> RunRejectedProbe() => throw new NotSupportedException();
            public void EmitDiagnosticProbe() { }
            public IReadOnlyList<LogEventV1> GetRecentDiagnostics() => Array.Empty<LogEventV1>();
            public void RequestShutdown() { }
        }

        private sealed class TestClock : IWallClock
        {
            public UtcInstant GetUtcNow() => UtcInstant.Parse("2026-08-11T00:00:00.0000000Z");
        }

        private sealed class TestMonotonicClock : IMonotonicClock
        {
            private long _ticks;
            public MonotonicTimestamp GetTimestamp() => MonotonicTimestamp.FromTestTicks(_ticks);
            public TimeSpan GetElapsedTime(MonotonicTimestamp start, MonotonicTimestamp end) => TimeSpan.FromMilliseconds(_ticks);
            public void Advance(TimeSpan value) => _ticks += (long)value.TotalMilliseconds;
        }

        private sealed class TestProcessIds : IProcessInstanceIdGenerator
        {
            public ProcessInstanceId Create() => TestIds.Process;
        }

        private sealed class TestDiagnosticIds : IDiagnosticIdGenerator
        {
            public DiagnosticId Create() => DiagnosticId.Parse("diag_0123456789abcdef0123456789abcdef");
        }

        private sealed class TestTechnicalIds : ITechnicalIdGenerator
        {
            private int _next = 1;
            public CommandId CreateCommandId() => CommandId.Parse("cmd_" + (_next++).ToString("x32"));
            public CorrelationId CreateCorrelationId() => CorrelationId.Parse("corr_" + (_next++).ToString("x32"));
            public DomainEventId CreateDomainEventId() => DomainEventId.Parse("evt_" + (_next++).ToString("x32"));
            public TransactionId CreateTransactionId() => TransactionId.Parse("tx_" + (_next++).ToString("x32"));
        }

        private sealed class FailingSink : IDiagnosticSink
        {
            public string Name => "failing";
            public bool TryWrite(LogEventV1 logEvent) => throw new InvalidOperationException("sink_failed");
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "odyssey-runtime-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public void Dispose()
            {
                if (Directory.Exists(Path)) Directory.Delete(Path, true);
            }
        }

        private static class TestIds
        {
            public static readonly ProcessInstanceId Process = ProcessInstanceId.Parse("proc_0123456789abcdef0123456789abcdef");
        }
    }
}
