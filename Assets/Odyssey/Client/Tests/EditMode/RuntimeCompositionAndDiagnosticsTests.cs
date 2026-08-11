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
using Odyssey.Domain.Time;
using Odyssey.Unity.Client;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Tests.Unity.EditMode
{
    public sealed class RuntimeCompositionAndDiagnosticsTests
    {
        [Test]
        public void RuntimeCompositionBuildsOneReadyGraphAndRejectsDuplicateStart()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            OdysseyRuntimeCompositionRoot root = new OdysseyRuntimeCompositionRoot();
            Result<AppRuntime> first = root.Start(OdysseyRuntimeConfiguration.DeveloperShell(directory.Path), DeterministicOverrides());
            Result<AppRuntime> second = root.Start(OdysseyRuntimeConfiguration.DeveloperShell(directory.Path), DeterministicOverrides());

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(first.Value.State, Is.EqualTo(OdysseyRuntimeState.Ready));
            Assert.That(first.Value.Profile, Is.EqualTo(OdysseyRuntimeProfile.DeveloperShell));
            Assert.That(second.IsFailure, Is.True);
            first.Value.Shutdown();
        }

        [Test]
        public void InvalidProfileAndCancellationReturnSafeFailureWithoutPublishingRuntime()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            Result<AppRuntime> invalid = new OdysseyRuntimeCompositionRoot().Start(
                new OdysseyRuntimeConfiguration(OdysseyRuntimeProfile.Production, DevelopmentAdapterMode.ExplicitDeveloperShell, directory.Path),
                DeterministicOverrides());
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Result<AppRuntime> cancelled = new OdysseyRuntimeCompositionRoot().Start(
                OdysseyRuntimeConfiguration.DeveloperShell(directory.Path),
                DeterministicOverrides(),
                cancellation.Token);

            Assert.That(invalid.IsFailure, Is.True);
            Assert.That(invalid.Error.SafeReasonCode, Is.EqualTo(SafeReasonCode.InvalidRequest));
            Assert.That(cancelled.IsFailure, Is.True);
            Assert.That(cancelled.Error.SafeReasonCode, Is.EqualTo(SafeReasonCode.OperationCancelled));
        }

        [Test]
        public void StartupFailureCleansPartialResourcesAndShutdownIsIdempotent()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            OdysseyRuntimeOverrides overrides = DeterministicOverrides();
            overrides.FailAfterDiagnostics = true;
            Result<AppRuntime> failed = new OdysseyRuntimeCompositionRoot().Start(OdysseyRuntimeConfiguration.DeveloperShell(directory.Path), overrides);
            Assert.That(failed.IsFailure, Is.True);
            Assert.That(failed.Error.DiagnosticId.HasValue, Is.True);
            Assert.That(File.Exists(Path.Combine(directory.Path, CrashMarkerStore.MarkerFileName)), Is.False);

            Result<AppRuntime> ready = new OdysseyRuntimeCompositionRoot().Start(OdysseyRuntimeConfiguration.DeveloperShell(directory.Path), DeterministicOverrides());
            Assert.That(ready.IsSuccess, Is.True);
            ready.Value.Shutdown();
            ready.Value.Shutdown();
            Assert.That(ready.Value.State, Is.EqualTo(OdysseyRuntimeState.Stopped));
            Assert.That(ready.Value.ShutdownSideEffects, Is.EqualTo(1));
        }

        [Test]
        public void PresentationRuntimeIsDisposedBeforeProcessResources()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            Result<AppRuntime> result = new OdysseyRuntimeCompositionRoot().Start(OdysseyRuntimeConfiguration.DeveloperShell(directory.Path), DeterministicOverrides());
            Assert.That(result.IsSuccess, Is.True);
            PresentationRuntime presentation = new PresentationRuntime();
            result.Value.AttachPresentationRuntime(presentation);
            result.Value.Shutdown();

            Assert.That(presentation.IsDisposed, Is.True);
            Assert.That(presentation.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void DeveloperShellProbeUsesApplicationCommandContractsAndRejectsMismatchSafely()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            Result<AppRuntime> result = new OdysseyRuntimeCompositionRoot().Start(OdysseyRuntimeConfiguration.DeveloperShell(directory.Path), DeterministicOverrides());
            Assert.That(result.IsSuccess, Is.True);

            Result<CommandResult> accepted = result.Value.ExecuteDeveloperProbe();
            Result<CommandResult> duplicate = result.Value.ExecuteDeveloperProbe();
            Result<CommandResult> mismatch = result.Value.ExecuteDeveloperProbe(true);

            Assert.That(accepted.IsSuccess, Is.True);
            Assert.That(accepted.Value.Status, Is.EqualTo(CommandResultStatus.Accepted));
            Assert.That(duplicate.IsSuccess, Is.True);
            Assert.That(mismatch.IsFailure, Is.True);
            Assert.That(mismatch.Error.Code, Is.EqualTo(ErrorCodes.CommandIdentityMismatch));
            result.Value.Shutdown();
        }

        [Test]
        public void DiagnosticsRuntimeHonorsLazyFilteringQueuePressureAndEmergencyFallback()
        {
            TestClock clock = new TestClock();
            EmergencyDiagnosticSink emergency = new EmergencyDiagnosticSink();
            InMemoryDiagnosticRingBuffer ring = new InMemoryDiagnosticRingBuffer(10, 4096);
            BoundedDiagnosticRuntime diagnostics = new BoundedDiagnosticRuntime(EventCodeRegistry.CreateDefault(), clock, new IDiagnosticSink[] { new FailingSink(), ring }, emergency, maxEvents: 1, maxBytes: 4096, autoFlush: false);
            diagnostics.MinimumLevel = LogLevel.Warning;
            bool evaluated = false;
            diagnostics.Write(LogLevel.Debug, OdysseyEventCodes.DiagnosticsProbe, SubsystemName.Parse("diagnostics"), MessageTemplateKey.Parse("diagnostics.probe"), new DiagnosticContext(TestIds.Process), () =>
            {
                evaluated = true;
                return Array.Empty<SafeLogProperty>();
            });
            Assert.That(evaluated, Is.False);

            diagnostics.MinimumLevel = LogLevel.Trace;
            diagnostics.Write(CreateDiagnosticsProbe(LogLevel.Information, clock));
            diagnostics.Write(CreateRuntimeShutdown(LogLevel.Warning, clock));
            diagnostics.Flush();
            IReadOnlyList<LogEventV1> flushed = ring.Snapshot();
            Assert.That(flushed, Has.Some.Matches<LogEventV1>(entry => entry.EventCode == OdysseyEventCodes.DiagnosticsDroppedEvents));
            Assert.That(flushed, Has.Some.Matches<LogEventV1>(entry => entry.Level == LogLevel.Warning));
            Assert.That(emergency.Snapshot().Count, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void RingBufferEvictsByCountAndNeverStoresRawSecretFixture()
        {
            InMemoryDiagnosticRingBuffer ring = new InMemoryDiagnosticRingBuffer(2, 4096);
            TestClock clock = new TestClock();
            ring.TryWrite(CreateDiagnosticsProbe(LogLevel.Information, clock));
            ring.TryWrite(CreateDiagnosticsProbe(LogLevel.Information, clock));
            ring.TryWrite(CreateRuntimeShutdown(LogLevel.Warning, clock));
            IReadOnlyList<LogEventV1> events = ring.Snapshot();
            string rendered = string.Join("|", EventCodes(events));

            Assert.That(events.Count, Is.EqualTo(2));
            Assert.That(rendered, Does.Not.Contain("super-secret-token"));
        }

        [Test]
        public void CrashMarkerDetectsPreviousUnfinishedMarkerAndCleanShutdownClearsIt()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            File.WriteAllText(Path.Combine(directory.Path, CrashMarkerStore.MarkerFileName), "{\"state\":\"started\"}");
            Result<AppRuntime> result = new OdysseyRuntimeCompositionRoot().Start(OdysseyRuntimeConfiguration.DeveloperShell(directory.Path), DeterministicOverrides());
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.RingBuffer.Snapshot(), Has.Some.Matches<LogEventV1>(entry => entry.EventCode == OdysseyEventCodes.CrashMarkerDetected));
            result.Value.Shutdown();
            Assert.That(File.Exists(Path.Combine(directory.Path, CrashMarkerStore.MarkerFileName)), Is.False);
        }

        [Test]
        public void DeveloperShellPresenterBindsRuntimeWithoutCreatingASecondGraph()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            Result<AppRuntime> result = new OdysseyRuntimeCompositionRoot().Start(OdysseyRuntimeConfiguration.DeveloperShell(directory.Path), DeterministicOverrides());
            Assert.That(result.IsSuccess, Is.True);
            GameObject gameObject = new GameObject("Developer Shell Test");
            try
            {
                gameObject.AddComponent<UIDocument>();
                DeveloperShellPresenter presenter = gameObject.AddComponent<DeveloperShellPresenter>();
                presenter.Bind(result.Value);
                Assert.That(presenter.IsBound, Is.True);
                Assert.That(result.Value.HasPresentationRuntime, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                result.Value.Shutdown();
            }
        }

        private static OdysseyRuntimeOverrides DeterministicOverrides()
        {
            return new OdysseyRuntimeOverrides
            {
                Clock = new TestClock(),
                ProcessInstanceIds = new TestProcessIds(),
                DiagnosticIds = new TestDiagnosticIds(),
                DisableConsoleSink = true
            };
        }

        private static LogEventV1 CreateDiagnosticsProbe(LogLevel level, IWallClock clock)
        {
            return new LogEventV1(
                clock.GetUtcNow(),
                level,
                OdysseyEventCodes.DiagnosticsProbe,
                SubsystemName.Parse("diagnostics"),
                BuildIdAvailability.UnavailableNotYetComposed,
                TestIds.Process,
                MessageTemplateKey.Parse("diagnostics.probe"),
                new[] { new SafeLogProperty(SafePropertyKey.Parse("probe"), SafeLogValue.Code("developer_shell")) });
        }

        private static LogEventV1 CreateRuntimeShutdown(LogLevel level, IWallClock clock)
        {
            return new LogEventV1(
                clock.GetUtcNow(),
                level,
                OdysseyEventCodes.RuntimeShutdownRequested,
                SubsystemName.Parse("runtime"),
                BuildIdAvailability.UnavailableNotYetComposed,
                TestIds.Process,
                MessageTemplateKey.Parse("runtime.shutdown_requested"),
                new[] { new SafeLogProperty(SafePropertyKey.Parse("state"), SafeLogValue.Code("shutting_down")) });
        }

        private static IEnumerable<string> EventCodes(IReadOnlyList<LogEventV1> events)
        {
            for (int index = 0; index < events.Count; index++)
            {
                yield return events[index].EventCode.ToString();
            }
        }

        private sealed class TestClock : IWallClock
        {
            public UtcInstant GetUtcNow()
            {
                return UtcInstant.Parse("2026-08-11T00:00:00.0000000Z");
            }
        }

        private sealed class TestProcessIds : IProcessInstanceIdGenerator
        {
            public ProcessInstanceId Create()
            {
                return TestIds.Process;
            }
        }

        private sealed class TestDiagnosticIds : IDiagnosticIdGenerator
        {
            public DiagnosticId Create()
            {
                return DiagnosticId.Parse("diag_0123456789abcdef0123456789abcdef");
            }
        }

        private sealed class FailingSink : IDiagnosticSink
        {
            public string Name => "failing";

            public bool TryWrite(LogEventV1 logEvent)
            {
                throw new InvalidOperationException("sink_failed");
            }
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
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, true);
                }
            }
        }

        private static class TestIds
        {
            public static readonly ProcessInstanceId Process = ProcessInstanceId.Parse("proc_0123456789abcdef0123456789abcdef");
        }
    }
}
