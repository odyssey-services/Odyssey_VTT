using System;
using System.Collections.Generic;
using System.Threading;
using Odyssey.Application.Commands;
using Odyssey.Application.Diagnostics;
using Odyssey.Application.Identity;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;

namespace Odyssey.Unity.Client
{
    public enum OdysseyRuntimeProfile
    {
        DeveloperShell = 1,
        Production = 2
    }

    public enum DevelopmentAdapterMode
    {
        ExplicitDeveloperShell = 1
    }

    public enum OdysseyRuntimeState
    {
        Starting = 1,
        Ready = 2,
        StartupFailed = 3,
        ShuttingDown = 4,
        Stopped = 5
    }

    public sealed class OdysseyRuntimeConfiguration
    {
        public OdysseyRuntimeConfiguration(OdysseyRuntimeProfile profile, DevelopmentAdapterMode developmentAdapters, string crashMarkerDirectory)
        {
            if (!Enum.IsDefined(typeof(OdysseyRuntimeProfile), profile)) throw new ArgumentOutOfRangeException(nameof(profile));
            if (!Enum.IsDefined(typeof(DevelopmentAdapterMode), developmentAdapters)) throw new ArgumentOutOfRangeException(nameof(developmentAdapters));
            if (string.IsNullOrWhiteSpace(crashMarkerDirectory)) throw new ArgumentException("Crash marker directory is required.", nameof(crashMarkerDirectory));
            Profile = profile;
            DevelopmentAdapters = developmentAdapters;
            CrashMarkerDirectory = crashMarkerDirectory;
        }

        public OdysseyRuntimeProfile Profile { get; }
        public DevelopmentAdapterMode DevelopmentAdapters { get; }
        public string CrashMarkerDirectory { get; }

        public static OdysseyRuntimeConfiguration DeveloperShell(string crashMarkerDirectory)
        {
            return new OdysseyRuntimeConfiguration(OdysseyRuntimeProfile.DeveloperShell, DevelopmentAdapterMode.ExplicitDeveloperShell, crashMarkerDirectory);
        }
    }

    internal sealed class RuntimeCompositionSettings
    {
        internal RuntimeCompositionSettings(IWallClock clock, IMonotonicClock monotonicClock, IProcessInstanceIdGenerator processInstanceIds, IDiagnosticIdGenerator diagnosticIds, ITechnicalIdGenerator technicalIds, IReadOnlyList<IDiagnosticSink> extraSinks, Func<string, IEmergencyDiagnosticSink> emergencySinkFactory, ICrashMarkerStoreFactory crashMarkerStoreFactory, Func<IPlatformExceptionHookSource> platformHookSourceFactory, bool includeConsoleSink)
        {
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            MonotonicClock = monotonicClock ?? throw new ArgumentNullException(nameof(monotonicClock));
            ProcessInstanceIds = processInstanceIds ?? throw new ArgumentNullException(nameof(processInstanceIds));
            DiagnosticIds = diagnosticIds ?? throw new ArgumentNullException(nameof(diagnosticIds));
            TechnicalIds = technicalIds ?? throw new ArgumentNullException(nameof(technicalIds));
            ExtraSinks = extraSinks ?? throw new ArgumentNullException(nameof(extraSinks));
            EmergencySinkFactory = emergencySinkFactory ?? throw new ArgumentNullException(nameof(emergencySinkFactory));
            CrashMarkerStoreFactory = crashMarkerStoreFactory ?? throw new ArgumentNullException(nameof(crashMarkerStoreFactory));
            PlatformHookSourceFactory = platformHookSourceFactory ?? throw new ArgumentNullException(nameof(platformHookSourceFactory));
            IncludeConsoleSink = includeConsoleSink;
        }

        internal IWallClock Clock { get; }
        internal IMonotonicClock MonotonicClock { get; }
        internal IProcessInstanceIdGenerator ProcessInstanceIds { get; }
        internal IDiagnosticIdGenerator DiagnosticIds { get; }
        internal ITechnicalIdGenerator TechnicalIds { get; }
        internal IReadOnlyList<IDiagnosticSink> ExtraSinks { get; }
        internal Func<string, IEmergencyDiagnosticSink> EmergencySinkFactory { get; }
        internal ICrashMarkerStoreFactory CrashMarkerStoreFactory { get; }
        internal Func<IPlatformExceptionHookSource> PlatformHookSourceFactory { get; }
        internal bool IncludeConsoleSink { get; }

        internal static RuntimeCompositionSettings Production()
        {
            return new RuntimeCompositionSettings(
                new UnityWallClock(),
                new UnityMonotonicClock(),
                new GuidProcessInstanceIdGenerator(),
                new GuidDiagnosticIdGenerator(),
                new GuidTechnicalIdGenerator(),
                Array.Empty<IDiagnosticSink>(),
                directory => new FileEmergencyDiagnosticSink(directory),
                new DefaultCrashMarkerStoreFactory(),
                () => new DotNetPlatformExceptionHookSource(),
                true);
        }
    }

    public sealed class OdysseyRuntimeCompositionRoot
    {
        private bool _started;

        public Result<AppRuntime> Start(OdysseyRuntimeConfiguration configuration, CancellationToken cancellationToken = default)
        {
            return Start(configuration, RuntimeCompositionSettings.Production(), cancellationToken);
        }

        internal Result<AppRuntime> Start(OdysseyRuntimeConfiguration configuration, RuntimeCompositionSettings settings, CancellationToken cancellationToken = default)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (_started) return Result<AppRuntime>.Failure(RuntimeErrors.InvalidConfiguration());
            _started = true;

            List<IDisposable> owned = new List<IDisposable>();
            BoundedDiagnosticRuntime? diagnostics = null;
            IEmergencyDiagnosticSink? emergency = null;
            DiagnosticContext? context = null;
            IDiagnosticIdGenerator diagnosticIds = settings.DiagnosticIds;
            IncidentDeduplicator incidents = new IncidentDeduplicator();
            try
            {
                if (cancellationToken.IsCancellationRequested) return Result<AppRuntime>.Failure(RuntimeErrors.Cancelled());
                if (configuration.Profile != OdysseyRuntimeProfile.DeveloperShell)
                {
                    return Result<AppRuntime>.Failure(RuntimeErrors.UnsupportedProfile());
                }

                ProcessInstanceId processInstanceId = settings.ProcessInstanceIds.Create();
                context = new DiagnosticContext(processInstanceId);
                EventCodeRegistry registry = EventCodeRegistry.CreateDefault();
                InMemoryDiagnosticRingBuffer ring = new InMemoryDiagnosticRingBuffer();
                emergency = settings.EmergencySinkFactory(configuration.CrashMarkerDirectory);
                List<IDiagnosticSink> sinks = new List<IDiagnosticSink> { ring };
                sinks.AddRange(settings.ExtraSinks);
                if (settings.IncludeConsoleSink) sinks.Add(new UnityConsoleDiagnosticSink());

                diagnostics = new BoundedDiagnosticRuntime(registry, settings.Clock, settings.MonotonicClock, sinks, emergency);
                owned.Add(diagnostics);
                ICrashMarkerStore crashMarker = settings.CrashMarkerStoreFactory.Create(configuration.CrashMarkerDirectory);
                owned.Add(crashMarker);
                crashMarker.Start(processInstanceId);
                PlatformFatalHookOwner fatalHooks = new PlatformFatalHookOwner(settings.PlatformHookSourceFactory(), diagnostics, settings.Clock, processInstanceId, diagnosticIds, incidents);
                owned.Add(fatalHooks);

                diagnostics.Write(LogLevel.Information, OdysseyEventCodes.AppStartupStarted, SubsystemName.Parse("app"), MessageTemplateKey.Parse("log.app.startup.started"), context, () => new[]
                {
                    new SafeLogProperty(SafePropertyKey.Parse("phase"), SafeLogValue.Code("diagnostics"))
                });

                if (crashMarker.PreviousMarkerWasUnfinished)
                {
                    diagnostics.Write(LogLevel.Warning, OdysseyEventCodes.DiagnosticsCrashPreviousUncleanDetected, SubsystemName.Parse("diagnostics"), MessageTemplateKey.Parse("log.diagnostics.crash.previous_unclean_detected"), context, () => new[]
                    {
                        new SafeLogProperty(SafePropertyKey.Parse("marker"), SafeLogValue.SanitizedPath(crashMarker.SanitizedMarkerPath))
                    });
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    CleanupOwned(owned);
                    return Result<AppRuntime>.Failure(RuntimeErrors.Cancelled());
                }

                DeveloperShellProbe probe = new DeveloperShellProbe(settings.Clock, settings.TechnicalIds);
                AppRuntime runtime = new AppRuntime(configuration.Profile, processInstanceId, settings.Clock, diagnostics, ring, emergency, crashMarker, probe, diagnosticIds, owned);
                return Result<AppRuntime>.Success(runtime);
            }
            catch (Exception ex)
            {
                DiagnosticId diagnosticId = diagnosticIds.Create();
                if (diagnostics != null && context != null)
                {
                    incidents.Record(diagnostics, settings.Clock, context.ProcessInstanceId, diagnosticIds, ex, SubsystemName.Parse("app"), out diagnosticId);
                }
                else
                {
                    emergency?.TryWrite(new EmergencyDiagnosticRecord(settings.Clock.GetUtcNow(), OdysseyEventCodes.AppStartupFailed, diagnosticId, "startup_unexpected"));
                }

                CleanupOwned(owned);
                return Result<AppRuntime>.Failure(RuntimeErrors.Unexpected(diagnosticId));
            }
        }

        private static void CleanupOwned(IReadOnlyList<IDisposable> owned)
        {
            for (int index = owned.Count - 1; index >= 0; index--) owned[index].Dispose();
        }
    }

    public sealed class AppRuntime : IDisposable
    {
        private readonly object _gate = new object();
        private readonly List<IDisposable> _owned;
        private PresentationRuntime? _presentationRuntime;
        private bool _shutdownCompleted;

        internal AppRuntime(
            OdysseyRuntimeProfile profile,
            ProcessInstanceId processInstanceId,
            IWallClock clock,
            BoundedDiagnosticRuntime diagnostics,
            InMemoryDiagnosticRingBuffer ringBuffer,
            IEmergencyDiagnosticSink emergencySink,
            ICrashMarkerStore crashMarker,
            DeveloperShellProbe developerProbe,
            IDiagnosticIdGenerator diagnosticIds,
            List<IDisposable> owned)
        {
            Profile = profile;
            ProcessInstanceId = processInstanceId;
            Clock = clock;
            Diagnostics = diagnostics;
            RingBuffer = ringBuffer;
            EmergencySink = emergencySink;
            CrashMarker = crashMarker;
            DeveloperProbe = developerProbe;
            DiagnosticIds = diagnosticIds;
            _owned = owned;
            State = OdysseyRuntimeState.Starting;
        }

        public OdysseyRuntimeProfile Profile { get; }
        public ProcessInstanceId ProcessInstanceId { get; }
        public IWallClock Clock { get; }
        public BoundedDiagnosticRuntime Diagnostics { get; }
        public InMemoryDiagnosticRingBuffer RingBuffer { get; }
        public IEmergencyDiagnosticSink EmergencySink { get; }
        public ICrashMarkerStore CrashMarker { get; }
        public DeveloperShellProbe DeveloperProbe { get; }
        public IDiagnosticIdGenerator DiagnosticIds { get; }
        public OdysseyRuntimeState State { get; private set; }
        public int ShutdownSideEffects { get; private set; }
        public bool HasPresentationRuntime => _presentationRuntime != null;

        internal Result AttachPresentationRuntime(PresentationRuntime presentationRuntime)
        {
            if (presentationRuntime == null) throw new ArgumentNullException(nameof(presentationRuntime));
            lock (_gate)
            {
                if (State != OdysseyRuntimeState.Starting) return Result.Failure(RuntimeErrors.CompositionInvalid());
                _presentationRuntime?.Dispose();
                _presentationRuntime = presentationRuntime;
                State = OdysseyRuntimeState.Ready;
                Diagnostics.Write(LogLevel.Information, OdysseyEventCodes.AppStartupCompleted, SubsystemName.Parse("app"), MessageTemplateKey.Parse("log.app.startup.completed"), new DiagnosticContext(ProcessInstanceId), () => new[]
                {
                    new SafeLogProperty(SafePropertyKey.Parse("state"), SafeLogValue.Code("ready")),
                    new SafeLogProperty(SafePropertyKey.Parse("duration_ms"), SafeLogValue.Duration(TimeSpan.Zero))
                });
                return Result.Success();
            }
        }

        internal void MarkStartupFailed(Error error)
        {
            lock (_gate)
            {
                State = OdysseyRuntimeState.StartupFailed;
                Diagnostics.Write(LogLevel.Error, OdysseyEventCodes.AppStartupFailed, SubsystemName.Parse("app"), MessageTemplateKey.Parse("log.app.startup.failed"), new DiagnosticContext(ProcessInstanceId, diagnosticId: error.DiagnosticId), () => new[]
                {
                    new SafeLogProperty(SafePropertyKey.Parse("phase"), SafeLogValue.Code("presentation")),
                    new SafeLogProperty(SafePropertyKey.Parse("reason"), SafeLogValue.Code("startup_failed")),
                    new SafeLogProperty(SafePropertyKey.Parse("diagnostic_id"), SafeLogValue.TechnicalIdentifier(error.DiagnosticId.HasValue ? error.DiagnosticId.Value.ToString() : "diag_unavailable"))
                });
                _presentationRuntime?.Dispose();
                _presentationRuntime = null;
            }
        }

        public Result<CommandResult> RunAcceptedProbe()
        {
            Result<CommandResult> result = DeveloperProbe.ExecuteAccepted();
            WriteProbeDiagnostic(result, false);
            return result;
        }

        public Result<CommandResult> RunRejectedProbe()
        {
            Result<CommandResult> result = DeveloperProbe.ExecuteRejected();
            WriteProbeDiagnostic(result, true);
            return result;
        }

        public void EmitDiagnosticProbe()
        {
            Diagnostics.Write(LogLevel.Information, OdysseyEventCodes.DiagnosticsProbeEmitted, SubsystemName.Parse("diagnostics"), MessageTemplateKey.Parse("log.diagnostics.probe.emitted"), new DiagnosticContext(ProcessInstanceId), () => new[]
            {
                new SafeLogProperty(SafePropertyKey.Parse("probe"), SafeLogValue.Code("developer_shell"))
            });
        }

        public IReadOnlyList<LogEventV1> GetRecentDiagnostics()
        {
            return RingBuffer.Snapshot();
        }

        public void Shutdown()
        {
            lock (_gate)
            {
                if (_shutdownCompleted) return;
                State = OdysseyRuntimeState.ShuttingDown;
                ShutdownSideEffects++;
                Diagnostics.Write(LogLevel.Information, OdysseyEventCodes.AppShutdownRequested, SubsystemName.Parse("app"), MessageTemplateKey.Parse("log.app.shutdown.requested"), new DiagnosticContext(ProcessInstanceId), () => new[]
                {
                    new SafeLogProperty(SafePropertyKey.Parse("state"), SafeLogValue.Code("shutting_down"))
                });
                _presentationRuntime?.Dispose();
                _presentationRuntime = null;
                for (int index = _owned.Count - 1; index >= 0; index--)
                {
                    if (_owned[index] is BoundedDiagnosticRuntime) continue;
                    _owned[index].Dispose();
                }

                Diagnostics.Write(LogLevel.Information, OdysseyEventCodes.DiagnosticsCrashMarkerCompleted, SubsystemName.Parse("diagnostics"), MessageTemplateKey.Parse("log.diagnostics.crash.marker_completed"), new DiagnosticContext(ProcessInstanceId), () => new[]
                {
                    new SafeLogProperty(SafePropertyKey.Parse("marker"), SafeLogValue.SanitizedPath(CrashMarker.SanitizedMarkerPath))
                });
                Diagnostics.Write(LogLevel.Information, OdysseyEventCodes.AppShutdownCompleted, SubsystemName.Parse("app"), MessageTemplateKey.Parse("log.app.shutdown.completed"), new DiagnosticContext(ProcessInstanceId), () => new[]
                {
                    new SafeLogProperty(SafePropertyKey.Parse("duration_ms"), SafeLogValue.Duration(TimeSpan.Zero))
                });
                Diagnostics.Shutdown(TimeSpan.FromSeconds(2));
                State = OdysseyRuntimeState.Stopped;
                _shutdownCompleted = true;
            }
        }

        public void Dispose()
        {
            Shutdown();
        }

        private void WriteProbeDiagnostic(Result<CommandResult> result, bool rejectedPath)
        {
            CommandResult? commandResult = result.IsSuccess ? result.Value : null;
            bool rejected = rejectedPath || result.IsFailure || commandResult!.Status == CommandResultStatus.Rejected;
            Diagnostics.Write(rejected ? LogLevel.Warning : LogLevel.Information, rejected ? OdysseyEventCodes.DeveloperShellProbeRejected : OdysseyEventCodes.DeveloperShellProbeAccepted, SubsystemName.Parse("developer"), MessageTemplateKey.Parse(rejected ? "log.developer.shell.probe_rejected" : "log.developer.shell.probe_accepted"), new DiagnosticContext(ProcessInstanceId, commandResult?.CorrelationId, null, commandResult?.CommandId), () => new[]
            {
                new SafeLogProperty(SafePropertyKey.Parse("command_id"), SafeLogValue.TechnicalIdentifier(commandResult == null ? "cmd_unavailable" : commandResult.CommandId.ToString())),
                new SafeLogProperty(SafePropertyKey.Parse("result_status"), SafeLogValue.Code(rejected ? "rejected" : "accepted"))
            });
        }
    }

    public sealed class PresentationRuntime : IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private bool _disposed;

        public bool IsDisposed => _disposed;
        public int DisposeCount { get; private set; }

        public void AddSubscription(IDisposable subscription)
        {
            if (subscription == null) throw new ArgumentNullException(nameof(subscription));
            if (_disposed) throw new ObjectDisposedException(nameof(PresentationRuntime));
            _subscriptions.Add(subscription);
        }

        public void Dispose()
        {
            if (_disposed) return;
            DisposeCount++;
            for (int index = _subscriptions.Count - 1; index >= 0; index--) _subscriptions[index].Dispose();
            _subscriptions.Clear();
            _disposed = true;
        }
    }

    internal static class RuntimeErrors
    {
        private static readonly CorrelationId RuntimeCorrelationId = CorrelationId.Parse("corr_00000000000000000000000000000000");

        internal static Error InvalidConfiguration() => Error.Create(
            ErrorCodes.ApplicationBootstrapConfigurationInvalid,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.runtime.configuration_invalid"),
            RetryDirective.DoNotRetry,
            RuntimeCorrelationId);

        internal static Error UnsupportedProfile() => Error.Create(
            ErrorCodes.ApplicationBootstrapConfigurationInvalid,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.runtime.configuration_invalid"),
            RetryDirective.DoNotRetry,
            RuntimeCorrelationId);

        internal static Error Cancelled() => Error.Create(
            ErrorCodes.ApplicationBootstrapInitializationCancelled,
            ErrorCategory.Cancelled,
            SafeReasonCode.OperationCancelled,
            UserMessageKey.Parse("errors.runtime.startup_cancelled"),
            RetryDirective.DoNotRetry,
            RuntimeCorrelationId);

        internal static Error CompositionInvalid() => Error.Create(
            ErrorCodes.ApplicationBootstrapCompositionInvalid,
            ErrorCategory.Precondition,
            SafeReasonCode.ActionNotAllowed,
            UserMessageKey.Parse("errors.runtime.composition_invalid"),
            RetryDirective.DoNotRetry,
            RuntimeCorrelationId);

        internal static Error Unexpected(DiagnosticId diagnosticId) => Error.Create(
            ErrorCodes.ApplicationBootstrapUnexpected,
            ErrorCategory.Internal,
            SafeReasonCode.UnexpectedError,
            UserMessageKey.Parse("errors.runtime.unexpected_startup_failure"),
            RetryDirective.DoNotRetry,
            RuntimeCorrelationId,
            diagnosticId: diagnosticId);
    }
}
