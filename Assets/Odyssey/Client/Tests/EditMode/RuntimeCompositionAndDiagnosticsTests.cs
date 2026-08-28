using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Diagnostics;
using Odyssey.Application.Identity;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Application.Versions;
using Odyssey.Domain.Events;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Unity.Client;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        public void MidStartupCancellationCleansOwnedResourcesAndClosesDiagnosticsLast()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            List<string> order = new List<string>();
            CapturingSink sink = new CapturingSink(order);
            RecordingCrashMarkerStore marker = new RecordingCrashMarkerStore(order);
            RuntimeCompositionSettings settings = DeterministicSettings(
                extraSinks: new IDiagnosticSink[] { sink },
                markerFactory: new FixedCrashMarkerStoreFactory(marker),
                processIds: new CancellingProcessIds(cancellation));

            Result<AppRuntime> cancelled = new OdysseyRuntimeCompositionRoot().Start(
                OdysseyRuntimeConfiguration.DeveloperShell(directory.Path),
                settings,
                cancellation.Token);

            Assert.That(cancelled.IsFailure, Is.True);
            Assert.That(cancelled.Error.Code, Is.EqualTo(ErrorCodes.ApplicationBootstrapInitializationCancelled));
            Assert.That(cancelled.Error.SafeReasonCode, Is.EqualTo(SafeReasonCode.OperationCancelled));
            Assert.That(cancelled.Error.UserMessageKey.ToString(), Is.EqualTo("errors.runtime.startup_cancelled"));
            Assert.That(cancelled.Error.RetryDirective, Is.EqualTo(RetryDirective.DoNotRetry));
            Assert.That(order, Does.Contain("marker"));
            Assert.That(order, Does.Contain("diagnostics"));
            Assert.That(order.IndexOf("marker"), Is.LessThan(order.IndexOf("diagnostics")));
            Assert.That(ContainsMarker(sink.Events, "raw"), Is.False);
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
            Assert.That(rejected.Value.Error!.Code, Is.EqualTo(ErrorCodes.ApplicationDeveloperProbeRejected));
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
            Assert.That(flushed, Has.Some.Matches<LogEventV1>(entry => entry.Level == LogLevel.Warning));
            Assert.That(diagnostics.DroppedTraceCount + diagnostics.DroppedInformationCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(emergency.Snapshot().Count, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void SecretRejectedBeforeAnyDiagnosticSinkReceivesMarker()
        {
            const string marker = "super-secret-token";
            TestClock clock = new TestClock();
            EmergencyDiagnosticSink emergency = new EmergencyDiagnosticSink();
            InMemoryDiagnosticRingBuffer ring = new InMemoryDiagnosticRingBuffer();
            CapturingSink sink = new CapturingSink();
            BoundedDiagnosticRuntime diagnostics = new BoundedDiagnosticRuntime(EventCodeRegistry.CreateDefault(), clock, new TestMonotonicClock(), new IDiagnosticSink[] { ring, sink }, emergency, autoFlush: false);

            Assert.Throws<ArgumentException>(() => SafeLogValue.BoundedText(marker, classification: DiagnosticDataClassification.Secret));
            diagnostics.Write(CreateDiagnosticsProbe(LogLevel.Information, clock));
            diagnostics.Flush();

            Assert.That(ContainsMarker(ring.Snapshot(), marker), Is.False);
            Assert.That(ContainsMarker(sink.Events, marker), Is.False);
            Assert.That(ContainsMarker(emergency.Snapshot(), marker), Is.False);
        }

        [Test]
        public void DiagnosticsPressureComparesIncomingPriorityAndEmitsExactDropCounters()
        {
            TestClock clock = new TestClock();
            AssertPressurePair(clock, LogLevel.Trace, LogLevel.Information, expectedSurvivor: LogLevel.Information, expectedTraceDrops: 1, expectedDebugDrops: 0, expectedInformationDrops: 0);
            AssertPressurePair(clock, LogLevel.Debug, LogLevel.Information, expectedSurvivor: LogLevel.Information, expectedTraceDrops: 0, expectedDebugDrops: 1, expectedInformationDrops: 0);
            AssertPressurePair(clock, LogLevel.Information, LogLevel.Debug, expectedSurvivor: LogLevel.Information, expectedTraceDrops: 0, expectedDebugDrops: 1, expectedInformationDrops: 0);
            AssertPressurePair(clock, LogLevel.Information, LogLevel.Trace, expectedSurvivor: LogLevel.Information, expectedTraceDrops: 1, expectedDebugDrops: 0, expectedInformationDrops: 0);

            CapturingSink sink = new CapturingSink();
            BoundedDiagnosticRuntime diagnostics = new BoundedDiagnosticRuntime(EventCodeRegistry.CreateDefault(), clock, new TestMonotonicClock(), new IDiagnosticSink[] { sink }, new EmergencyDiagnosticSink(), maxEvents: 3, maxBytes: 4096, autoFlush: false);
            diagnostics.Write(CreateDiagnosticsProbe(LogLevel.Trace, clock));
            diagnostics.Write(CreateDiagnosticsProbe(LogLevel.Debug, clock));
            diagnostics.Write(CreateDiagnosticsProbe(LogLevel.Information, clock));
            diagnostics.Write(CreateShutdown(LogLevel.Warning, clock));
            diagnostics.Flush();
            Assert.That(sink.Events, Has.Some.Matches<LogEventV1>(entry => entry.Level == LogLevel.Warning));
            Assert.That(sink.Events, Has.None.Matches<LogEventV1>(entry => entry.Level == LogLevel.Trace));
            Assert.That(diagnostics.DroppedTraceCount, Is.EqualTo(1));

            sink.Clear();
            diagnostics.Write(CreateShutdown(LogLevel.Warning, clock));
            diagnostics.Flush();
            Assert.That(sink.Events, Has.Some.Matches<LogEventV1>(entry => entry.EventCode == OdysseyEventCodes.DiagnosticsQueueEventsDropped && HasProperty(entry, "trace_count", "1") && HasProperty(entry, "debug_count", "0") && HasProperty(entry, "information_count", "0")));
            Assert.That(diagnostics.DroppedLowerPriorityCount, Is.EqualTo(0));
        }

        [Test]
        public void DiagnosticsPriorityFallbackKeepsExistingWarningAndUsesEmergencyForIncomingHighPriority()
        {
            TestClock clock = new TestClock();
            EmergencyDiagnosticSink emergency = new EmergencyDiagnosticSink();
            CapturingSink sink = new CapturingSink();
            BoundedDiagnosticRuntime diagnostics = new BoundedDiagnosticRuntime(EventCodeRegistry.CreateDefault(), clock, new TestMonotonicClock(), new IDiagnosticSink[] { sink }, emergency, maxEvents: 1, maxBytes: 4096, autoFlush: false);

            diagnostics.Write(CreateShutdown(LogLevel.Warning, clock));
            diagnostics.Write(CreateDiagnosticsProbe(LogLevel.Error, clock));
            diagnostics.Flush();

            Assert.That(sink.Events.Count, Is.EqualTo(1));
            Assert.That(sink.Events[0].Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(emergency.Snapshot(), Has.Some.Matches<EmergencyDiagnosticRecord>(record => record.Token == "queue_full"));
        }

        [Test]
        public void DiagnosticsQueueAcceptsConcurrentProducersWithoutCorruption()
        {
            TestClock clock = new TestClock();
            TestMonotonicClock monotonic = new TestMonotonicClock();
            EmergencyDiagnosticSink emergency = new EmergencyDiagnosticSink();
            CapturingSink sink = new CapturingSink();
            BoundedDiagnosticRuntime diagnostics = new BoundedDiagnosticRuntime(EventCodeRegistry.CreateDefault(), clock, monotonic, new IDiagnosticSink[] { sink }, emergency, maxEvents: 512, maxBytes: 1024 * 1024, autoFlush: false);
            Exception? failure = null;
            Task[] producers = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            {
                try
                {
                    for (int index = 0; index < 32; index++) diagnostics.Write(CreateDiagnosticsProbe(LogLevel.Information, clock));
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            })).ToArray();

            Task.WaitAll(producers);
            Assert.That(failure, Is.Null);
            Assert.That(diagnostics.PendingCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(diagnostics.PendingLogicalBytes, Is.GreaterThanOrEqualTo(0));
            long dropped = diagnostics.DroppedTraceCount + diagnostics.DroppedDebugCount + diagnostics.DroppedInformationCount;
            diagnostics.Flush();
            Assert.That(sink.Events.Count + dropped, Is.GreaterThanOrEqualTo(1));
            Assert.That(diagnostics.PendingLogicalBytes, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void DiagnosticsShutdownHonorsNormalAndFatalBudgets()
        {
            TestClock clock = new TestClock();
            TestMonotonicClock normalClock = new TestMonotonicClock();
            AdvancingSink normalSink = new AdvancingSink(normalClock, TimeSpan.FromMilliseconds(100));
            BoundedDiagnosticRuntime normal = new BoundedDiagnosticRuntime(EventCodeRegistry.CreateDefault(), clock, normalClock, new IDiagnosticSink[] { normalSink }, new EmergencyDiagnosticSink(), maxEvents: 8, maxBytes: 4096, autoFlush: false);
            normal.Write(CreateDiagnosticsProbe(LogLevel.Information, clock));
            normal.Write(CreateShutdown(LogLevel.Warning, clock));
            normal.Shutdown(TimeSpan.FromSeconds(2));
            Assert.That(normalSink.Count, Is.EqualTo(2));

            TestMonotonicClock fatalClock = new TestMonotonicClock();
            EmergencyDiagnosticSink emergency = new EmergencyDiagnosticSink();
            AdvancingSink fatalSink = new AdvancingSink(fatalClock, TimeSpan.FromMilliseconds(300));
            BoundedDiagnosticRuntime fatal = new BoundedDiagnosticRuntime(EventCodeRegistry.CreateDefault(), clock, fatalClock, new IDiagnosticSink[] { fatalSink }, emergency, maxEvents: 8, maxBytes: 4096, autoFlush: false);
            fatal.Write(CreateDiagnosticsProbe(LogLevel.Information, clock));
            fatal.Write(CreateDiagnosticsProbe(LogLevel.Information, clock));
            fatal.Write(CreateShutdown(LogLevel.Warning, clock));
            fatal.Shutdown(TimeSpan.FromMilliseconds(500));
            Assert.That(fatalSink.Count, Is.LessThan(3));
            Assert.That(emergency.Snapshot(), Has.Some.Matches<EmergencyDiagnosticRecord>(record => record.Token == "drain_budget_exhausted"));
        }

        [Test]
        public void DeveloperProbeResultSurvivesFailingDiagnosticSink()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            EmergencyDiagnosticSink emergency = new EmergencyDiagnosticSink();
            RuntimeCompositionSettings settings = DeterministicSettings(
                extraSinks: new IDiagnosticSink[] { new FailingSink() },
                emergencySinkFactory: _ => emergency);
            Result<AppRuntime> result = new OdysseyRuntimeCompositionRoot().Start(OdysseyRuntimeConfiguration.DeveloperShell(directory.Path), settings);
            Assert.That(result.IsSuccess, Is.True);

            Result<CommandResult> accepted = result.Value.RunAcceptedProbe();

            Assert.That(accepted.IsSuccess, Is.True);
            Assert.That(accepted.Value.Status, Is.EqualTo(CommandResultStatus.Accepted));
            Assert.That(result.Value.DeveloperProbe.AcceptedCommitCount, Is.EqualTo(1));
            Assert.That(result.Value.DeveloperProbe.EventBatchCommitCount, Is.EqualTo(1));
            Assert.That(emergency.Snapshot(), Has.Some.Matches<EmergencyDiagnosticRecord>(record => record.Token == "sink_exception"));
            result.Value.Shutdown();
        }

        [Test]
        public void IncidentDeduplicatorRecordsOnlyFirstFullExceptionSummary()
        {
            TestClock clock = new TestClock();
            TestMonotonicClock monotonic = new TestMonotonicClock();
            EmergencyDiagnosticSink emergency = new EmergencyDiagnosticSink();
            CapturingSink sink = new CapturingSink();
            BoundedDiagnosticRuntime diagnostics = new BoundedDiagnosticRuntime(EventCodeRegistry.CreateDefault(), clock, monotonic, new IDiagnosticSink[] { sink }, emergency);
            IncidentDeduplicator deduplicator = new IncidentDeduplicator();
            TestDiagnosticIds ids = new TestDiagnosticIds();

            bool first = deduplicator.Record(diagnostics, clock, TestIds.Process, ids, new InvalidOperationException("raw secret message"), SubsystemName.Parse("app"), out _);
            bool second = deduplicator.Record(diagnostics, clock, TestIds.Process, ids, new InvalidOperationException("different raw message"), SubsystemName.Parse("app"), out _);

            Assert.That(first, Is.True);
            Assert.That(second, Is.False);
            Assert.That(sink.Events.Count(entry => entry.ExceptionSummary.HasValue), Is.EqualTo(1));
            Assert.That(string.Join("|", sink.Events.Select(entry => entry.MessageTemplateKey.ToString())), Does.Not.Contain("raw secret message"));
        }

        [Test]
        public void StartupFailureDisposesPartialGraphAndRecordsSafeIncident()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            CapturingSink sink = new CapturingSink();
            ThrowingCrashMarkerStore marker = new ThrowingCrashMarkerStore();
            RuntimeCompositionSettings settings = DeterministicSettings(
                extraSinks: new IDiagnosticSink[] { sink },
                markerFactory: new FixedCrashMarkerStoreFactory(marker));

            Result<AppRuntime> result = new OdysseyRuntimeCompositionRoot().Start(OdysseyRuntimeConfiguration.DeveloperShell(directory.Path), settings);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.ApplicationBootstrapUnexpected));
            Assert.That(result.Error.SafeReasonCode, Is.EqualTo(SafeReasonCode.UnexpectedError));
            Assert.That(marker.Disposed, Is.True);
            Assert.That(sink.Events, Has.Some.Matches<LogEventV1>(entry => entry.EventCode == OdysseyEventCodes.DiagnosticsIncidentUnexpected));
            Assert.That(sink.Events, Has.None.Matches<LogEventV1>(entry => entry.MessageTemplateKey.ToString().Contains("raw startup secret")));
        }

        [Test]
        public void StartupCleanupContinuesAfterDisposeFailureAndKeepsOriginalSafeFailure()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            CapturingSink sink = new CapturingSink();
            EmergencyDiagnosticSink emergency = new EmergencyDiagnosticSink();
            List<string> order = new List<string>();
            ThrowingCrashMarkerStore marker = new ThrowingCrashMarkerStore(order, throwOnStart: true, throwOnDispose: true);
            RuntimeCompositionSettings settings = DeterministicSettings(
                extraSinks: new IDiagnosticSink[] { sink },
                emergencySinkFactory: _ => emergency,
                markerFactory: new FixedCrashMarkerStoreFactory(marker));

            Result<AppRuntime> result = new OdysseyRuntimeCompositionRoot().Start(OdysseyRuntimeConfiguration.DeveloperShell(directory.Path), settings);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.ApplicationBootstrapUnexpected));
            Assert.That(result.Error.SafeReasonCode, Is.EqualTo(SafeReasonCode.UnexpectedError));
            Assert.That(order, Does.Contain("marker_dispose"));
            Assert.That(emergency.Snapshot(), Has.Some.Matches<EmergencyDiagnosticRecord>(record => record.Token == "cleanup_failure"));
            Assert.That(ContainsMarker(sink.Events, "raw dispose secret"), Is.False);
        }

        [Test]
        public void RuntimeSettingsOverrideOnlyRequestedAdapter()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            CapturingSink sink = new CapturingSink();
            RuntimeCompositionSettings settings = DeterministicSettings(extraSinks: new IDiagnosticSink[] { sink });

            Result<AppRuntime> result = new OdysseyRuntimeCompositionRoot().Start(OdysseyRuntimeConfiguration.DeveloperShell(directory.Path), settings);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.ProcessInstanceId, Is.EqualTo(TestIds.Process));
            Assert.That(result.Value.Diagnostics, Is.Not.Null);
            Assert.That(result.Value.CrashMarker, Is.TypeOf<CrashMarkerStore>());
            Assert.That(result.Value.EmergencySink, Is.TypeOf<EmergencyDiagnosticSink>());
            Assert.That(sink.Events, Has.Some.Matches<LogEventV1>(entry => entry.EventCode == OdysseyEventCodes.AppStartupStarted));
            result.Value.Shutdown();
        }

        [Test]
        public void FatalHooksRecordSafeIncidentAndUnsubscribeBeforeDiagnosticsShutdown()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            CapturingSink sink = new CapturingSink();
            EmergencyDiagnosticSink emergency = new EmergencyDiagnosticSink();
            TestPlatformHookSource hooks = new TestPlatformHookSource();
            RuntimeCompositionSettings settings = DeterministicSettings(
                extraSinks: new IDiagnosticSink[] { sink },
                emergencySinkFactory: _ => emergency,
                hookSourceFactory: () => hooks);
            Result<AppRuntime> result = new OdysseyRuntimeCompositionRoot().Start(OdysseyRuntimeConfiguration.DeveloperShell(directory.Path), settings);
            Assert.That(result.IsSuccess, Is.True);

            hooks.RaiseUnhandled(new InvalidOperationException("raw hook secret"));
            Assert.That(sink.Events, Has.Some.Matches<LogEventV1>(entry => entry.EventCode == OdysseyEventCodes.DiagnosticsIncidentUnexpected && entry.ExceptionSummary.HasValue));
            int incidentsBeforeShutdown = sink.Events.Count(entry => entry.EventCode == OdysseyEventCodes.DiagnosticsIncidentUnexpected);

            result.Value.Shutdown();
            hooks.RaiseUnhandled(new InvalidOperationException("after shutdown"));

            Assert.That(hooks.ActiveSubscriptions, Is.EqualTo(0));
            Assert.That(sink.Events.Count(entry => entry.EventCode == OdysseyEventCodes.DiagnosticsIncidentUnexpected), Is.EqualTo(incidentsBeforeShutdown));
            Assert.That(emergency.Snapshot(), Has.Some.Matches<EmergencyDiagnosticRecord>(record => record.Token == "platform_fatal_hook"));
            Assert.That(sink.Events, Has.None.Matches<LogEventV1>(entry => entry.MessageTemplateKey.ToString().Contains("raw hook secret")));
        }

        [Test]
        public void RuntimeShutdownDisposesPresentationMarkerThenDiagnostics()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            List<string> order = new List<string>();
            CapturingSink sink = new CapturingSink(order);
            RecordingCrashMarkerStore marker = new RecordingCrashMarkerStore(order);
            RuntimeCompositionSettings settings = DeterministicSettings(
                extraSinks: new IDiagnosticSink[] { sink },
                markerFactory: new FixedCrashMarkerStoreFactory(marker));
            Result<AppRuntime> result = new OdysseyRuntimeCompositionRoot().Start(OdysseyRuntimeConfiguration.DeveloperShell(directory.Path), settings);
            Assert.That(result.IsSuccess, Is.True);
            PresentationRuntime presentation = new PresentationRuntime();
            presentation.AddSubscription(new RecordingDisposable(order, "presentation"));
            Assert.That(result.Value.AttachPresentationRuntime(presentation).IsSuccess, Is.True);

            result.Value.Shutdown();

            Assert.That(order.IndexOf("presentation"), Is.LessThan(order.IndexOf("marker")));
            Assert.That(order.IndexOf("marker"), Is.LessThan(order.IndexOf("diagnostics")));
            Assert.That(sink.Events, Has.Some.Matches<LogEventV1>(entry => entry.EventCode == OdysseyEventCodes.DiagnosticsCrashMarkerCompleted));
            Assert.That(sink.Events, Has.Some.Matches<LogEventV1>(entry => entry.EventCode == OdysseyEventCodes.AppShutdownCompleted));
        }

        [Test]
        public void RuntimeShutdownContinuesAfterOwnedDisposeFailureAndStops()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            List<string> order = new List<string>();
            CapturingSink sink = new CapturingSink(order);
            EmergencyDiagnosticSink emergency = new EmergencyDiagnosticSink();
            ThrowingCrashMarkerStore marker = new ThrowingCrashMarkerStore(order, throwOnStart: false, throwOnDispose: true);
            RuntimeCompositionSettings settings = DeterministicSettings(
                extraSinks: new IDiagnosticSink[] { sink },
                emergencySinkFactory: _ => emergency,
                markerFactory: new FixedCrashMarkerStoreFactory(marker));
            Result<AppRuntime> result = new OdysseyRuntimeCompositionRoot().Start(OdysseyRuntimeConfiguration.DeveloperShell(directory.Path), settings);
            Assert.That(result.IsSuccess, Is.True);
            PresentationRuntime presentation = new PresentationRuntime();
            presentation.AddSubscription(new RecordingDisposable(order, "presentation"));
            Assert.That(result.Value.AttachPresentationRuntime(presentation).IsSuccess, Is.True);

            result.Value.Shutdown();
            result.Value.Shutdown();

            Assert.That(result.Value.State, Is.EqualTo(OdysseyRuntimeState.Stopped));
            Assert.That(result.Value.ShutdownSideEffects, Is.EqualTo(1));
            Assert.That(order, Does.Contain("marker_dispose"));
            Assert.That(order, Does.Contain("diagnostics"));
            Assert.That(emergency.Snapshot(), Has.Some.Matches<EmergencyDiagnosticRecord>(record => record.Token == "cleanup_failure"));
            Assert.That(ContainsMarker(sink.Events, "raw dispose secret"), Is.False);
        }

        [Test]
        public void RuntimeShutdownDoesNotEmitMarkerCompletedWhenCrashMarkerCannotComplete()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            List<string> order = new List<string>();
            CapturingSink sink = new CapturingSink(order);
            EmergencyDiagnosticSink emergency = new EmergencyDiagnosticSink();
            FailingCompletionCrashMarkerStore marker = new FailingCompletionCrashMarkerStore(order);
            RuntimeCompositionSettings settings = DeterministicSettings(
                extraSinks: new IDiagnosticSink[] { sink },
                emergencySinkFactory: _ => emergency,
                markerFactory: new FixedCrashMarkerStoreFactory(marker));
            Result<AppRuntime> result = new OdysseyRuntimeCompositionRoot().Start(OdysseyRuntimeConfiguration.DeveloperShell(directory.Path), settings);
            Assert.That(result.IsSuccess, Is.True);

            result.Value.Shutdown();

            Assert.That(result.Value.State, Is.EqualTo(OdysseyRuntimeState.Stopped));
            Assert.That(order, Does.Contain("marker"));
            Assert.That(order, Does.Contain("diagnostics"));
            Assert.That(order.IndexOf("marker"), Is.LessThan(order.IndexOf("diagnostics")));
            Assert.That(sink.Events, Has.None.Matches<LogEventV1>(entry => entry.EventCode == OdysseyEventCodes.DiagnosticsCrashMarkerCompleted));
            Assert.That(emergency.Snapshot(), Has.Some.Matches<EmergencyDiagnosticRecord>(record => record.Token == "cleanup_failure"));
            Assert.That(ContainsMarker(sink.Events, "raw marker failure"), Is.False);
        }

        [Test]
        public void RuntimeUsesSuppliedBuildIdentityForStartupDiagnostics()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            CapturingSink sink = new CapturingSink();
            BuildIdentity identity = CreateBuildIdentity();
            RuntimeCompositionSettings settings = DeterministicSettings(
                extraSinks: new IDiagnosticSink[] { sink },
                buildIdentityProvider: new FixedBuildIdentityProvider(identity));

            Result<AppRuntime> result = new OdysseyRuntimeCompositionRoot().Start(OdysseyRuntimeConfiguration.DeveloperShell(directory.Path), settings);
            Assert.That(result.IsSuccess, Is.True);
            PresentationRuntime presentation = new PresentationRuntime();
            Assert.That(result.Value.AttachPresentationRuntime(presentation).IsSuccess, Is.True);

            Assert.That(result.Value.BuildIdentityAvailability, Is.EqualTo(BuildIdAvailability.Available));
            Assert.That(result.Value.BuildIdentity!.BuildId, Is.EqualTo(identity.BuildId));
            Assert.That(sink.Events, Has.Some.Matches<LogEventV1>(entry =>
                entry.EventCode == OdysseyEventCodes.AppStartupCompleted &&
                HasProperty(entry, "build_id", identity.BuildId)));
            result.Value.Shutdown();
        }

        [Test]
        public void DeveloperShellDisplaysBuildIdentityAndUnavailableFallback()
        {
            GameObject gameObject = new GameObject("Build Identity Document");
            try
            {
                UIDocument document = gameObject.AddComponent<UIDocument>();
                BuildIdentity identity = CreateBuildIdentity();
                RecordingFacade facade = new RecordingFacade(identity);
                using PresentationRuntime presentation = new PresentationRuntime();
                DeveloperShellPresenter presenter = new DeveloperShellPresenter(document, facade, presentation);

                Assert.That(presenter.Initialize().IsSuccess, Is.True);
                Assert.That(document.rootVisualElement.Q<Label>("build-identity")!.text, Does.Contain(identity.BuildId));
                Assert.That(document.rootVisualElement.Q<Label>("build-identity")!.text, Does.Contain(identity.DisplayVersion));

                facade.SetBuildIdentity(null);
                presenter.Refresh();
                Assert.That(document.rootVisualElement.Q<Label>("build-identity")!.text, Is.EqualTo("Build identity: unavailable"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void StartupFailureFallbackRendersSingleSceneDocumentWithSafeReasonOnly()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject gameObject = new GameObject("Fallback Document");
            try
            {
                UIDocument document = gameObject.AddComponent<UIDocument>();
                Error error = RuntimeErrors.Unexpected(DiagnosticId.Parse("diag_0123456789abcdef0123456789abcdef"));

                OdysseyRuntimeHost.RenderStartupFailure(scene, error);

                Assert.That(document.rootVisualElement.Q<Label>("runtime-state")!.text, Is.EqualTo("State: StartupFailed"));
                Assert.That(document.rootVisualElement.Q<Label>("shell-result")!.text, Is.EqualTo("Failure: UnexpectedError"));
                Assert.That(document.rootVisualElement.Q<Label>("diagnostic-id")!.text, Is.EqualTo("DiagnosticId: diag_0123456789abcdef0123456789abcdef"));
                Assert.That(document.rootVisualElement.Q<Label>("shell-result")!.text, Does.Not.Contain("composition_invalid"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void CompositionInvalidErrorUsesRegisteredSafeSemantics()
        {
            Error error = RuntimeErrors.CompositionInvalid();
            Assert.That(error.Code, Is.EqualTo(ErrorCodes.ApplicationBootstrapCompositionInvalid));
            Assert.That(error.Category, Is.EqualTo(ErrorCategory.Precondition));
            Assert.That(error.SafeReasonCode, Is.EqualTo(SafeReasonCode.ActionNotAllowed));
            Assert.That(error.UserMessageKey.ToString(), Is.EqualTo("errors.runtime.composition_invalid"));
            Assert.That(error.RetryDirective, Is.EqualTo(RetryDirective.DoNotRetry));
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
            File.WriteAllText(Path.Combine(directory.Path, CrashMarkerStore.MarkerFileName), "{\"state\":\"started\",\"process\":\"proc_0123456789abcdef0123456789abcdef\"}");
            using (CrashMarkerStore marker = new CrashMarkerStore(directory.Path))
            {
                marker.Start(TestIds.Process);
                Assert.That(marker.PreviousMarkerWasUnfinished, Is.True);
            }

            File.WriteAllText(Path.Combine(directory.Path, CrashMarkerStore.MarkerFileName), "{\"state\":\"completed\"}");
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
                Assert.That(marker.TryComplete(), Is.True);
                Assert.That(marker.TryComplete(), Is.True);
            }

            File.WriteAllText(Path.Combine(directory.Path, CrashMarkerStore.MarkerFileName), "{\"state\":\"started\",\"process\":\"proc_0123456789abcdef0123456789abcdef\"");
            using (CrashMarkerStore marker = new CrashMarkerStore(directory.Path))
            {
                marker.Start(TestIds.Process);
                Assert.That(marker.PreviousMarkerWasUnfinished, Is.False);
                Assert.That(marker.PreviousMarkerWasMalformed, Is.True);
            }

            File.WriteAllText(Path.Combine(directory.Path, CrashMarkerStore.MarkerFileName), "{\"state\":\"started\",\"process\":\"proc_notcanonical000000000000000000000\"}");
            using (CrashMarkerStore marker = new CrashMarkerStore(directory.Path))
            {
                marker.Start(TestIds.Process);
                Assert.That(marker.PreviousMarkerWasUnfinished, Is.False);
                Assert.That(marker.PreviousMarkerWasMalformed, Is.True);
            }

            File.WriteAllText(Path.Combine(directory.Path, CrashMarkerStore.MarkerFileName), "{\"state\":\"started\",\"process\":\"proc_0123456789abcdef0123456789abcdef\"}suffix");
            using (CrashMarkerStore marker = new CrashMarkerStore(directory.Path))
            {
                marker.Start(TestIds.Process);
                Assert.That(marker.PreviousMarkerWasUnfinished, Is.False);
                Assert.That(marker.PreviousMarkerWasMalformed, Is.True);
            }
        }

        [Test]
        public void EmergencyDiagnosticTokenRejectsInjectionAndPathLikeValues()
        {
            Assert.That(new EmergencyDiagnosticRecord(new TestClock().GetUtcNow(), OdysseyEventCodes.DiagnosticsSinkWriteFailed, null, "queue_full").Token, Is.EqualTo("queue_full"));
            Assert.Throws<ArgumentException>(() => new EmergencyDiagnosticRecord(new TestClock().GetUtcNow(), OdysseyEventCodes.DiagnosticsSinkWriteFailed, null, "queue\nfull"));
            Assert.Throws<ArgumentException>(() => new EmergencyDiagnosticRecord(new TestClock().GetUtcNow(), OdysseyEventCodes.DiagnosticsSinkWriteFailed, null, "queue\tfull"));
            Assert.Throws<ArgumentException>(() => new EmergencyDiagnosticRecord(new TestClock().GetUtcNow(), OdysseyEventCodes.DiagnosticsSinkWriteFailed, null, "C:/temp/secret"));
            Assert.Throws<ArgumentException>(() => new EmergencyDiagnosticRecord(new TestClock().GetUtcNow(), OdysseyEventCodes.DiagnosticsSinkWriteFailed, null, new string('a', 65)));
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
                PresentationRuntime presentationRuntime = new PresentationRuntime();
                Result result = entryPoint.Initialize(facade, presentationRuntime);
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(entryPoint.IsInitialized, Is.True);
                Assert.That(presentationRuntime.IsDisposed, Is.False);
                UnityEngine.Object.DestroyImmediate(gameObject);
                Assert.That(presentationRuntime.IsDisposed, Is.False);
                presentationRuntime.Dispose();
                Assert.That(presentationRuntime.IsDisposed, Is.True);
            }
            finally
            {
                if (gameObject != null) UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static RuntimeCompositionSettings DeterministicSettings(
            IReadOnlyList<IDiagnosticSink>? extraSinks = null,
            Func<string, IEmergencyDiagnosticSink>? emergencySinkFactory = null,
            ICrashMarkerStoreFactory? markerFactory = null,
            Func<IPlatformExceptionHookSource>? hookSourceFactory = null,
            IProcessInstanceIdGenerator? processIds = null,
            IBuildIdentityProvider? buildIdentityProvider = null)
        {
            return new RuntimeCompositionSettings(
                new TestClock(),
                new TestMonotonicClock(),
                processIds ?? new TestProcessIds(),
                new TestDiagnosticIds(),
                new TestTechnicalIds(),
                buildIdentityProvider ?? new FixedBuildIdentityProvider(null),
                extraSinks ?? Array.Empty<IDiagnosticSink>(),
                emergencySinkFactory ?? (_ => new EmergencyDiagnosticSink()),
                markerFactory ?? new DefaultCrashMarkerStoreFactory(),
                hookSourceFactory ?? (() => new TestPlatformHookSource()),
                false);
        }

        private static bool HasProperty(LogEventV1 logEvent, string key, string value)
        {
            return logEvent.SafeProperties.Any(property => property.Key == SafePropertyKey.Parse(key) && property.Value.RenderedValue == value);
        }

        private static BuildIdentity CreateBuildIdentity()
        {
            CompatibilityConfig compatibility = new CompatibilityConfig(
                new CompatibilityRange(1, 1),
                new CompatibilityRange(1, 1),
                new CompatibilityRange(1, 1),
                new CompatibilityRange(1, 1),
                new CompatibilityRange(1, 1),
                new CompatibilityRange(1, 1),
                new CompatibilityRange(1, 1),
                new ProtocolCompatibilityRange(1, 1, 1));
            return BuildIdentityCodec.Create(
                new VersionSource(ApplicationVersion.Parse("0.1.0")),
                compatibility,
                BuildChannel.Local,
                1,
                1,
                "0123456789abcdef0123456789abcdef01234567",
                "heads/local",
                WorkingTreeState.Clean,
                "20260812T120000Z",
                "6000.4.0f1",
                "8cf496087c8f",
                "10.0.302",
                "Development-Debug",
                "WindowsStandalone",
                "x86_64",
                "Mono",
                "NETStandard2.1");
        }

        private static bool ContainsMarker(IEnumerable<LogEventV1> events, string marker)
        {
            foreach (LogEventV1 logEvent in events)
            {
                if (logEvent.EventCode.ToString().Contains(marker)) return true;
                if (logEvent.MessageTemplateKey.ToString().Contains(marker)) return true;
                foreach (SafeLogProperty property in logEvent.SafeProperties)
                {
                    if (property.Key.ToString().Contains(marker) || property.Value.RenderedValue.Contains(marker)) return true;
                }
            }

            return false;
        }

        private static bool ContainsMarker(IEnumerable<EmergencyDiagnosticRecord> records, string marker)
        {
            foreach (EmergencyDiagnosticRecord record in records)
            {
                if (record.EventCode.ToString().Contains(marker)) return true;
                if (record.DiagnosticId.HasValue && record.DiagnosticId.Value.ToString().Contains(marker)) return true;
                if (record.Token.Contains(marker)) return true;
            }

            return false;
        }

        private static void AssertPressurePair(TestClock clock, LogLevel existing, LogLevel incoming, LogLevel expectedSurvivor, long expectedTraceDrops, long expectedDebugDrops, long expectedInformationDrops)
        {
            CapturingSink sink = new CapturingSink();
            BoundedDiagnosticRuntime diagnostics = new BoundedDiagnosticRuntime(EventCodeRegistry.CreateDefault(), clock, new TestMonotonicClock(), new IDiagnosticSink[] { sink }, new EmergencyDiagnosticSink(), maxEvents: 1, maxBytes: 4096, autoFlush: false);
            diagnostics.Write(CreateDiagnosticsProbe(existing, clock));
            diagnostics.Write(CreateDiagnosticsProbe(incoming, clock));
            diagnostics.Flush();

            Assert.That(sink.Events.Count(entry => entry.EventCode == OdysseyEventCodes.DiagnosticsProbeEmitted), Is.EqualTo(1));
            Assert.That(sink.Events, Has.Some.Matches<LogEventV1>(entry => entry.Level == expectedSurvivor));
            Assert.That(diagnostics.DroppedTraceCount, Is.EqualTo(expectedTraceDrops));
            Assert.That(diagnostics.DroppedDebugCount, Is.EqualTo(expectedDebugDrops));
            Assert.That(diagnostics.DroppedInformationCount, Is.EqualTo(expectedInformationDrops));
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
            private BuildIdentity? _identity;

            public RecordingFacade(BuildIdentity? identity = null)
            {
                _identity = identity;
            }

            public OdysseyRuntimeState RuntimeState => OdysseyRuntimeState.Ready;
            public OdysseyRuntimeProfile RuntimeProfile => OdysseyRuntimeProfile.DeveloperShell;
            public BuildIdAvailability BuildIdentityAvailability => _identity == null ? BuildIdAvailability.UnavailableNotYetComposed : BuildIdAvailability.Available;
            public BuildIdentity? BuildIdentity => _identity;
            public Result<CommandResult> RunAcceptedProbe() => throw new NotSupportedException();
            public Result<CommandResult> RunRejectedProbe() => throw new NotSupportedException();
            public void EmitDiagnosticProbe() { }
            public IReadOnlyList<LogEventV1> GetRecentDiagnostics() => Array.Empty<LogEventV1>();
            public Result OpenTrialScreen() => Result.Success();
            public void RequestShutdown() { }
            public void SetBuildIdentity(BuildIdentity? identity) => _identity = identity;
        }

        private sealed class FixedBuildIdentityProvider : IBuildIdentityProvider
        {
            public FixedBuildIdentityProvider(BuildIdentity? identity)
            {
                Current = identity;
            }

            public BuildIdentity? Current { get; }
        }

        private sealed class TestClock : IWallClock
        {
            public UtcInstant GetUtcNow() => UtcInstant.Parse("2026-08-11T00:00:00.0000000Z");
        }

        private sealed class TestMonotonicClock : IMonotonicClock
        {
            private long _ticks;
            private readonly Dictionary<MonotonicTimestamp, long> _captured = new Dictionary<MonotonicTimestamp, long>();
            public MonotonicTimestamp GetTimestamp()
            {
                MonotonicTimestamp timestamp = MonotonicTimestamp.FromTestTicks(_ticks);
                _captured[timestamp] = _ticks;
                return timestamp;
            }

            public TimeSpan GetElapsedTime(MonotonicTimestamp start, MonotonicTimestamp end) => TimeSpan.FromMilliseconds(_captured[end] - _captured[start]);
            public void Advance(TimeSpan value) => _ticks += (long)value.TotalMilliseconds;
        }

        private sealed class TestProcessIds : IProcessInstanceIdGenerator
        {
            public ProcessInstanceId Create() => TestIds.Process;
        }

        private sealed class CancellingProcessIds : IProcessInstanceIdGenerator
        {
            private readonly CancellationTokenSource _cancellation;
            public CancellingProcessIds(CancellationTokenSource cancellation)
            {
                _cancellation = cancellation;
            }

            public ProcessInstanceId Create()
            {
                _cancellation.Cancel();
                return TestIds.Process;
            }
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

        private sealed class CapturingSink : IDiagnosticSink, IDisposable
        {
            private readonly List<LogEventV1> _events = new List<LogEventV1>();
            private readonly List<string>? _order;
            public CapturingSink()
            {
            }

            public CapturingSink(List<string> order)
            {
                _order = order;
            }

            public string Name => "capturing";
            public IReadOnlyList<LogEventV1> Events => _events;
            public bool TryWrite(LogEventV1 logEvent)
            {
                _events.Add(logEvent);
                if (logEvent.EventCode == OdysseyEventCodes.AppShutdownCompleted) _order?.Add("diagnostics");
                return true;
            }

            public void Clear() => _events.Clear();
            public void Dispose() => _order?.Add("diagnostics");
        }

        private sealed class AdvancingSink : IDiagnosticSink
        {
            private readonly TestMonotonicClock _clock;
            private readonly TimeSpan _advanceBy;
            public AdvancingSink(TestMonotonicClock clock, TimeSpan advanceBy)
            {
                _clock = clock;
                _advanceBy = advanceBy;
            }

            public string Name => "advancing";
            public int Count { get; private set; }
            public bool TryWrite(LogEventV1 logEvent)
            {
                Count++;
                _clock.Advance(_advanceBy);
                return true;
            }
        }

        private sealed class TestPlatformHookSource : IPlatformExceptionHookSource
        {
            public event Action<Exception>? UnhandledException;
            public event Action<Exception>? UnobservedTaskException;
            public int ActiveSubscriptions { get; private set; }
            event Action<Exception> IPlatformExceptionHookSource.UnhandledException
            {
                add
                {
                    ActiveSubscriptions++;
                    UnhandledException += value;
                }
                remove
                {
                    ActiveSubscriptions--;
                    UnhandledException -= value;
                }
            }

            event Action<Exception> IPlatformExceptionHookSource.UnobservedTaskException
            {
                add
                {
                    ActiveSubscriptions++;
                    UnobservedTaskException += value;
                }
                remove
                {
                    ActiveSubscriptions--;
                    UnobservedTaskException -= value;
                }
            }

            public void RaiseUnhandled(Exception exception) => UnhandledException?.Invoke(exception);
            public void RaiseUnobserved(Exception exception) => UnobservedTaskException?.Invoke(exception);
        }

        private sealed class FixedCrashMarkerStoreFactory : ICrashMarkerStoreFactory
        {
            private readonly ICrashMarkerStore _marker;
            public FixedCrashMarkerStoreFactory(ICrashMarkerStore marker)
            {
                _marker = marker;
            }

            public ICrashMarkerStore Create(string directory) => _marker;
        }

        private sealed class RecordingCrashMarkerStore : ICrashMarkerStore
        {
            private readonly List<string> _order;
            public RecordingCrashMarkerStore(List<string> order)
            {
                _order = order;
            }

            public string SanitizedMarkerPath => "Diagnostics/process-started.json";
            public bool PreviousMarkerWasUnfinished => false;
            public bool PreviousMarkerWasMalformed => false;
            public void Start(ProcessInstanceId processInstanceId) { }
            public bool TryComplete()
            {
                _order.Add("marker");
                return true;
            }

            public void Dispose() => TryComplete();
        }

        private sealed class ThrowingCrashMarkerStore : ICrashMarkerStore
        {
            private readonly List<string>? _order;
            private readonly bool _throwOnStart;
            private readonly bool _throwOnDispose;
            public ThrowingCrashMarkerStore()
            {
                _throwOnStart = true;
            }

            public ThrowingCrashMarkerStore(List<string> order, bool throwOnStart, bool throwOnDispose)
            {
                _order = order;
                _throwOnStart = throwOnStart;
                _throwOnDispose = throwOnDispose;
            }

            public string SanitizedMarkerPath => "Diagnostics/process-started.json";
            public bool PreviousMarkerWasUnfinished => false;
            public bool PreviousMarkerWasMalformed => false;
            public bool Disposed { get; private set; }
            public void Start(ProcessInstanceId processInstanceId)
            {
                if (_throwOnStart) throw new InvalidOperationException("raw startup secret");
            }

            public bool TryComplete()
            {
                _order?.Add("marker_dispose");
                if (_throwOnDispose) throw new InvalidOperationException("raw dispose secret");
                return true;
            }

            public void Dispose()
            {
                Disposed = true;
                TryComplete();
            }
        }

        private sealed class FailingCompletionCrashMarkerStore : ICrashMarkerStore
        {
            private readonly List<string> _order;
            public FailingCompletionCrashMarkerStore(List<string> order)
            {
                _order = order;
            }

            public string SanitizedMarkerPath => "Diagnostics/process-started.json";
            public bool PreviousMarkerWasUnfinished => false;
            public bool PreviousMarkerWasMalformed => false;
            public void Start(ProcessInstanceId processInstanceId) { }
            public bool TryComplete()
            {
                _order.Add("marker");
                return false;
            }

            public void Dispose() => TryComplete();
        }

        private sealed class RecordingDisposable : IDisposable
        {
            private readonly List<string> _order;
            private readonly string _name;
            public RecordingDisposable(List<string> order, string name)
            {
                _order = order;
                _name = name;
            }

            public void Dispose() => _order.Add(_name);
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
