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
        None = 1,
        ExplicitDeveloperShell = 2
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

    public sealed class OdysseyRuntimeOverrides
    {
        public IWallClock? Clock { get; set; }
        public IProcessInstanceIdGenerator? ProcessInstanceIds { get; set; }
        public IDiagnosticIdGenerator? DiagnosticIds { get; set; }
        public bool FailAfterDiagnostics { get; set; }
        public bool DisableConsoleSink { get; set; }
    }

    public sealed class OdysseyRuntimeCompositionRoot
    {
        private bool _started;

        public Result<AppRuntime> Start(OdysseyRuntimeConfiguration configuration, OdysseyRuntimeOverrides? overrides = null, CancellationToken cancellationToken = default)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (_started)
            {
                return Result<AppRuntime>.Failure(RuntimeErrors.InvalidConfiguration("errors.runtime.duplicate_start"));
            }

            _started = true;
            List<IDisposable> owned = new List<IDisposable>();
            ProcessInstanceId processInstanceId = default;
            IDiagnosticIdGenerator? diagnosticIds = null;
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Result<AppRuntime>.Failure(RuntimeErrors.Cancelled());
                }

                if (configuration.Profile != OdysseyRuntimeProfile.DeveloperShell &&
                    configuration.DevelopmentAdapters == DevelopmentAdapterMode.ExplicitDeveloperShell)
                {
                    return Result<AppRuntime>.Failure(RuntimeErrors.InvalidConfiguration("errors.runtime.developer_adapters_rejected"));
                }

                IWallClock clock = overrides?.Clock ?? new UnityWallClock();
                IProcessInstanceIdGenerator processIds = overrides?.ProcessInstanceIds ?? new GuidProcessInstanceIdGenerator();
                diagnosticIds = overrides?.DiagnosticIds ?? new GuidDiagnosticIdGenerator();
                processInstanceId = processIds.Create();
                DiagnosticContext context = new DiagnosticContext(processInstanceId);
                EventCodeRegistry registry = EventCodeRegistry.CreateDefault();
                InMemoryDiagnosticRingBuffer ring = new InMemoryDiagnosticRingBuffer();
                EmergencyDiagnosticSink emergency = new EmergencyDiagnosticSink();
                List<IDiagnosticSink> sinks = new List<IDiagnosticSink> { ring, emergency };
                if (overrides == null || !overrides.DisableConsoleSink)
                {
                    sinks.Add(new UnityConsoleDiagnosticSink());
                }

                BoundedDiagnosticRuntime diagnostics = new BoundedDiagnosticRuntime(registry, clock, sinks, emergency);
                owned.Add(diagnostics);
                CrashMarkerStore crashMarker = new CrashMarkerStore(configuration.CrashMarkerDirectory);
                owned.Add(crashMarker);
                crashMarker.Start(processInstanceId);

                diagnostics.Write(LogLevel.Information, OdysseyEventCodes.RuntimeStarting, SubsystemName.Parse("runtime"), MessageTemplateKey.Parse("runtime.starting"), context, () => new[]
                {
                    new SafeLogProperty(SafePropertyKey.Parse("phase"), SafeLogValue.Code("diagnostics"))
                });

                if (crashMarker.PreviousMarkerWasUnfinished)
                {
                    diagnostics.Write(LogLevel.Warning, OdysseyEventCodes.CrashMarkerDetected, SubsystemName.Parse("diagnostics"), MessageTemplateKey.Parse("diagnostics.crash_marker_detected"), context, () => new[]
                    {
                        new SafeLogProperty(SafePropertyKey.Parse("marker"), SafeLogValue.SanitizedPath(crashMarker.SanitizedMarkerPath))
                    });
                }

                if (overrides != null && overrides.FailAfterDiagnostics)
                {
                    diagnostics.Write(LogLevel.Error, OdysseyEventCodes.RuntimeStartupFailed, SubsystemName.Parse("runtime"), MessageTemplateKey.Parse("runtime.startup_failed"), context, () => new[]
                    {
                        new SafeLogProperty(SafePropertyKey.Parse("phase"), SafeLogValue.Code("application_graph")),
                        new SafeLogProperty(SafePropertyKey.Parse("reason"), SafeLogValue.Code("startup_phase_failed"))
                    });
                    throw new InvalidOperationException("startup_phase_failed");
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    CleanupOwned(owned);
                    return Result<AppRuntime>.Failure(RuntimeErrors.Cancelled());
                }

                DeveloperShellProbe? probe = configuration.Profile == OdysseyRuntimeProfile.DeveloperShell
                    ? new DeveloperShellProbe(clock)
                    : null;
                AppRuntime runtime = new AppRuntime(configuration.Profile, processInstanceId, clock, diagnostics, ring, emergency, crashMarker, probe, diagnosticIds, owned);
                diagnostics.Write(LogLevel.Information, OdysseyEventCodes.RuntimeReady, SubsystemName.Parse("runtime"), MessageTemplateKey.Parse("runtime.ready"), context, () => new[]
                {
                    new SafeLogProperty(SafePropertyKey.Parse("state"), SafeLogValue.Code("ready")),
                    new SafeLogProperty(SafePropertyKey.Parse("duration_ms"), SafeLogValue.Duration(TimeSpan.Zero))
                });
                return Result<AppRuntime>.Success(runtime);
            }
            catch (Exception ex)
            {
                CleanupOwned(owned);
                DiagnosticId? diagnosticId = null;
                if (diagnosticIds != null)
                {
                    diagnosticId = diagnosticIds.Create();
                }

                return Result<AppRuntime>.Failure(RuntimeErrors.Unexpected(diagnosticId, ex));
            }
        }

        private static void CleanupOwned(IReadOnlyList<IDisposable> owned)
        {
            for (int index = owned.Count - 1; index >= 0; index--)
            {
                owned[index].Dispose();
            }
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
            EmergencyDiagnosticSink emergencySink,
            CrashMarkerStore crashMarker,
            DeveloperShellProbe? developerProbe,
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
            State = OdysseyRuntimeState.Ready;
        }

        public OdysseyRuntimeProfile Profile { get; }
        public ProcessInstanceId ProcessInstanceId { get; }
        public IWallClock Clock { get; }
        public BoundedDiagnosticRuntime Diagnostics { get; }
        public InMemoryDiagnosticRingBuffer RingBuffer { get; }
        public EmergencyDiagnosticSink EmergencySink { get; }
        public CrashMarkerStore CrashMarker { get; }
        public DeveloperShellProbe? DeveloperProbe { get; }
        public IDiagnosticIdGenerator DiagnosticIds { get; }
        public OdysseyRuntimeState State { get; private set; }
        public int ShutdownSideEffects { get; private set; }
        public bool HasPresentationRuntime => _presentationRuntime != null;

        public void AttachPresentationRuntime(PresentationRuntime presentationRuntime)
        {
            if (presentationRuntime == null) throw new ArgumentNullException(nameof(presentationRuntime));
            lock (_gate)
            {
                _presentationRuntime?.Dispose();
                _presentationRuntime = presentationRuntime;
            }
        }

        public Result<CommandResult> ExecuteDeveloperProbe(bool mismatchFingerprint = false)
        {
            if (DeveloperProbe == null)
            {
                return Result<CommandResult>.Failure(RuntimeErrors.InvalidConfiguration("errors.runtime.developer_probe_unavailable"));
            }

            Result<CommandResult> result = DeveloperProbe.Execute(mismatchFingerprint);
            DiagnosticContext context = new DiagnosticContext(ProcessInstanceId, result.IsSuccess ? result.Value.CorrelationId : (CorrelationId?)null, null, result.IsSuccess ? result.Value.CommandId : (CommandId?)null);
            if (result.IsSuccess && result.Value.Status == CommandResultStatus.Accepted)
            {
                Diagnostics.Write(LogLevel.Information, OdysseyEventCodes.DeveloperProbeAccepted, SubsystemName.Parse("developer"), MessageTemplateKey.Parse("developer.probe_accepted"), context, () => new[]
                {
                    new SafeLogProperty(SafePropertyKey.Parse("command_id"), SafeLogValue.TechnicalIdentifier(result.Value.CommandId.ToString())),
                    new SafeLogProperty(SafePropertyKey.Parse("result_status"), SafeLogValue.Code("accepted"))
                });
            }
            else
            {
                Diagnostics.Write(LogLevel.Warning, OdysseyEventCodes.DeveloperProbeRejected, SubsystemName.Parse("developer"), MessageTemplateKey.Parse("developer.probe_rejected"), new DiagnosticContext(ProcessInstanceId), () => new[]
                {
                    new SafeLogProperty(SafePropertyKey.Parse("command_id"), SafeLogValue.TechnicalIdentifier("cmd_unavailable")),
                    new SafeLogProperty(SafePropertyKey.Parse("result_status"), SafeLogValue.Code("rejected"))
                });
            }

            return result;
        }

        public void EmitDiagnosticProbe()
        {
            Diagnostics.Write(LogLevel.Information, OdysseyEventCodes.DiagnosticsProbe, SubsystemName.Parse("diagnostics"), MessageTemplateKey.Parse("diagnostics.probe"), new DiagnosticContext(ProcessInstanceId), () => new[]
            {
                new SafeLogProperty(SafePropertyKey.Parse("probe"), SafeLogValue.Code("developer_shell"))
            });
        }

        public void Shutdown()
        {
            lock (_gate)
            {
                if (_shutdownCompleted) return;
                State = OdysseyRuntimeState.ShuttingDown;
                ShutdownSideEffects++;
                Diagnostics.Write(LogLevel.Information, OdysseyEventCodes.RuntimeShutdownRequested, SubsystemName.Parse("runtime"), MessageTemplateKey.Parse("runtime.shutdown_requested"), new DiagnosticContext(ProcessInstanceId), () => new[]
                {
                    new SafeLogProperty(SafePropertyKey.Parse("state"), SafeLogValue.Code("shutting_down"))
                });
                _presentationRuntime?.Dispose();
                _presentationRuntime = null;
                Diagnostics.Write(LogLevel.Information, OdysseyEventCodes.RuntimeShutdownCompleted, SubsystemName.Parse("runtime"), MessageTemplateKey.Parse("runtime.shutdown_completed"), new DiagnosticContext(ProcessInstanceId), () => new[]
                {
                    new SafeLogProperty(SafePropertyKey.Parse("duration_ms"), SafeLogValue.Duration(TimeSpan.Zero))
                });
                for (int index = _owned.Count - 1; index >= 0; index--)
                {
                    _owned[index].Dispose();
                }

                State = OdysseyRuntimeState.Stopped;
                _shutdownCompleted = true;
            }
        }

        public void Dispose()
        {
            Shutdown();
        }
    }

    public sealed class PresentationRuntime : IDisposable
    {
        private bool _disposed;

        public bool IsDisposed => _disposed;
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            if (_disposed) return;
            DisposeCount++;
            _disposed = true;
        }
    }

    internal static class RuntimeErrors
    {
        private static readonly CorrelationId RuntimeCorrelationId = CorrelationId.Parse("corr_00000000000000000000000000000000");

        internal static Error InvalidConfiguration(string messageKey)
        {
            return Error.Create(
                ErrorCodes.ApplicationValidationInvalid,
                ErrorCategory.Validation,
                SafeReasonCode.InvalidRequest,
                UserMessageKey.Parse(messageKey),
                RetryDirective.DoNotRetry,
                RuntimeCorrelationId);
        }

        internal static Error Cancelled()
        {
            return Error.Create(
                ErrorCodes.ApplicationValidationInvalid,
                ErrorCategory.Cancelled,
                SafeReasonCode.OperationCancelled,
                UserMessageKey.Parse("errors.runtime.startup_cancelled"),
                RetryDirective.DoNotRetry,
                RuntimeCorrelationId);
        }

        internal static Error Unexpected(DiagnosticId? diagnosticId, Exception exception)
        {
            _ = exception;
            return Error.Create(
                ErrorCodes.ApplicationInternalUnexpected,
                ErrorCategory.Internal,
                SafeReasonCode.UnexpectedError,
                UserMessageKey.Parse("errors.runtime.unexpected_startup_failure"),
                RetryDirective.DoNotRetry,
                RuntimeCorrelationId,
                diagnosticId: diagnosticId);
        }
    }
}
