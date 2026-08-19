using System.IO;
using Odyssey.Application.Diagnostics;
using Odyssey.Application.Results;
using Odyssey.Application.Versions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    public sealed class OdysseyRuntimeHost : MonoBehaviour
    {
        private readonly OdysseyRuntimeCompositionRoot _compositionRoot = new OdysseyRuntimeCompositionRoot();
        private AppRuntime? _runtime;
        private Error? _startupFailure;
        private bool _leaseHeld;
        private PlayerSmokeMode? _smokeMode;

        public AppRuntime? Runtime => _runtime;
        public bool IsAcceptedHost => _leaseHeld;

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
            PlayerSmokeMode.TryCreateFromCommandLine(out _smokeMode);
            if (!RuntimeHostLease.TryAcquire())
            {
                Destroy(gameObject);
                return;
            }

            _leaseHeld = true;
            string markerDirectory = Path.Combine(UnityEngine.Application.persistentDataPath, "Diagnostics");
            Result<AppRuntime> result = _compositionRoot.Start(OdysseyRuntimeConfiguration.DeveloperShell(markerDirectory));
            if (result.IsFailure)
            {
                Debug.LogError(result.Error.UserMessageKey.ToString());
                _startupFailure = result.Error;
                SceneManager.sceneLoaded += OnFailureSceneLoaded;
                SceneManager.LoadSceneAsync("Assets/Odyssey/Client/Scenes/AppShell.unity", LoadSceneMode.Additive);
                RuntimeHostLease.Release();
                _leaseHeld = false;
                return;
            }

            _runtime = result.Value;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.LoadSceneAsync("Assets/Odyssey/Client/Scenes/AppShell.unity", LoadSceneMode.Additive);
        }

        private void Update()
        {
            if (_runtime == null || !_leaseHeld) return;
            if (!RuntimeHostLease.TryConsumeDuplicateAttempt(out int count)) return;
            _runtime.RecordDuplicateBootstrapRejected(count);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_runtime == null || scene.name != "AppShell") return;
            AppShellEntryPoint? entryPoint = FindSingleEntryPoint(scene);
            if (entryPoint == null)
            {
                Error error = RuntimeErrors.CompositionInvalid();
                _runtime.MarkStartupFailed(error);
                RenderStartupFailure(scene, error);
                Debug.LogError(error.UserMessageKey.ToString());
                return;
            }

            PresentationRuntime presentationRuntime = new PresentationRuntime();
            IDeveloperShellFacade facade = new DeveloperShellFacade(_runtime, RequestShutdownFromShell);
            Result initialized = entryPoint.Initialize(facade, presentationRuntime);
            if (initialized.IsFailure)
            {
                presentationRuntime.Dispose();
                entryPoint.ShowStartupFailed(initialized.Error);
                _runtime.MarkStartupFailed(initialized.Error);
                return;
            }

            Result attached = _runtime.AttachPresentationRuntime(presentationRuntime);
            if (attached.IsFailure)
            {
                presentationRuntime.Dispose();
                entryPoint.ShowStartupFailed(attached.Error);
                _runtime.MarkStartupFailed(attached.Error);
                return;
            }

            entryPoint.Refresh();
            if (_smokeMode != null)
            {
                StartCoroutine(_smokeMode.Run(_runtime, entryPoint));
            }
        }

        private void OnFailureSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_startupFailure == null || scene.name != "AppShell") return;
            RenderStartupFailure(scene, _startupFailure);
            SceneManager.sceneLoaded -= OnFailureSceneLoaded;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (_runtime == null || scene.name != "AppShell") return;
            _runtime.DetachPresentationRuntime();
        }

        private static AppShellEntryPoint? FindSingleEntryPoint(Scene scene)
        {
            AppShellEntryPoint? found = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                AppShellEntryPoint[] entryPoints = root.GetComponentsInChildren<AppShellEntryPoint>(true);
                for (int index = 0; index < entryPoints.Length; index++)
                {
                    if (found != null) return null;
                    found = entryPoints[index];
                }
            }

            return found;
        }

        internal static void RenderStartupFailure(Scene scene, Error error)
        {
            UIDocument? document = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                UIDocument[] documents = root.GetComponentsInChildren<UIDocument>(true);
                for (int index = 0; index < documents.Length; index++)
                {
                    if (document != null) return;
                    document = documents[index];
                }
            }

            if (document == null) return;
            VisualElement rootElement = document.rootVisualElement;
            rootElement.Clear();
            rootElement.Add(new Label("State: StartupFailed") { name = "runtime-state" });
            rootElement.Add(new Label("Failure: " + error.SafeReasonCode) { name = "shell-result" });
            if (error.DiagnosticId.HasValue)
            {
                rootElement.Add(new Label("DiagnosticId: " + error.DiagnosticId.Value) { name = "diagnostic-id" });
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded -= OnFailureSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            if (_runtime != null)
            {
                _runtime.Shutdown();
                _runtime = null;
            }

            if (_leaseHeld)
            {
                RuntimeHostLease.Release();
                _leaseHeld = false;
            }
        }

        private void RequestShutdownFromShell()
        {
            if (_runtime != null)
            {
                _runtime.Shutdown();
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded -= OnFailureSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            if (_leaseHeld)
            {
                RuntimeHostLease.Release();
                _leaseHeld = false;
            }
        }
    }

    internal static class RuntimeHostLease
    {
        private static bool _isHeld;
        private static int _pendingDuplicateAttempts;

        internal static bool TryAcquire()
        {
            if (_isHeld)
            {
                if (_pendingDuplicateAttempts < 16) _pendingDuplicateAttempts++;
                return false;
            }

            _isHeld = true;
            return true;
        }

        internal static void Release()
        {
            _isHeld = false;
            _pendingDuplicateAttempts = 0;
        }

        internal static bool TryConsumeDuplicateAttempt(out int count)
        {
            count = _pendingDuplicateAttempts;
            _pendingDuplicateAttempts = 0;
            return count > 0;
        }

        internal static bool IsHeld => _isHeld;
    }

    internal sealed class DeveloperShellFacade : IDeveloperShellFacade
    {
        private readonly AppRuntime _runtime;
        private readonly System.Action _requestShutdown;

        public DeveloperShellFacade(AppRuntime runtime, System.Action requestShutdown)
        {
            _runtime = runtime;
            _requestShutdown = requestShutdown;
        }

        public OdysseyRuntimeState RuntimeState => _runtime.State;
        public OdysseyRuntimeProfile RuntimeProfile => _runtime.Profile;
        public BuildIdAvailability BuildIdentityAvailability => _runtime.BuildIdentityAvailability;
        public BuildIdentity? BuildIdentity => _runtime.BuildIdentity;
        public Result<Odyssey.Application.Commands.CommandResult> RunAcceptedProbe() => _runtime.RunAcceptedProbe();
        public Result<Odyssey.Application.Commands.CommandResult> RunRejectedProbe() => _runtime.RunRejectedProbe();
        public void EmitDiagnosticProbe() => _runtime.EmitDiagnosticProbe();
        public System.Collections.Generic.IReadOnlyList<LogEventV1> GetRecentDiagnostics() => _runtime.GetRecentDiagnostics();
        public void RequestShutdown() => _requestShutdown();
    }
}
