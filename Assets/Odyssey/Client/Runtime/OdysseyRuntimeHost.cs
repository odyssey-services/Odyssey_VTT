using System.IO;
using Odyssey.Application.Diagnostics;
using Odyssey.Application.Results;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Odyssey.Unity.Client
{
    public sealed class OdysseyRuntimeHost : MonoBehaviour
    {
        private readonly OdysseyRuntimeCompositionRoot _compositionRoot = new OdysseyRuntimeCompositionRoot();
        private AppRuntime? _runtime;
        private bool _leaseHeld;

        public AppRuntime? Runtime => _runtime;
        public bool IsAcceptedHost => _leaseHeld;

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
            if (!RuntimeHostLease.TryAcquire())
            {
                Debug.LogWarning(OdysseyEventCodes.AppBootstrapDuplicateRejected.ToString());
                Destroy(gameObject);
                return;
            }

            _leaseHeld = true;
            string markerDirectory = Path.Combine(UnityEngine.Application.persistentDataPath, "Diagnostics");
            Result<AppRuntime> result = _compositionRoot.Start(OdysseyRuntimeConfiguration.DeveloperShell(markerDirectory));
            if (result.IsFailure)
            {
                Debug.LogError(result.Error.UserMessageKey.ToString());
                RuntimeHostLease.Release();
                _leaseHeld = false;
                return;
            }

            _runtime = result.Value;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.LoadSceneAsync("Assets/Odyssey/Client/Scenes/AppShell.unity", LoadSceneMode.Additive);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_runtime == null || scene.name != "AppShell") return;
            AppShellEntryPoint? entryPoint = FindSingleEntryPoint(scene);
            if (entryPoint == null)
            {
                Error error = RuntimeErrors.CompositionInvalid();
                _runtime.MarkStartupFailed(error);
                Debug.LogError(error.UserMessageKey.ToString());
                return;
            }

            IDeveloperShellFacade facade = new DeveloperShellFacade(_runtime);
            Result<PresentationRuntime> presentation = entryPoint.Initialize(facade);
            if (presentation.IsFailure)
            {
                entryPoint.ShowStartupFailed(presentation.Error);
                _runtime.MarkStartupFailed(presentation.Error);
                return;
            }

            Result attached = _runtime.AttachPresentationRuntime(presentation.Value);
            if (attached.IsFailure)
            {
                entryPoint.ShowStartupFailed(attached.Error);
                _runtime.MarkStartupFailed(attached.Error);
                return;
            }

            entryPoint.Refresh();
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

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
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
    }

    internal static class RuntimeHostLease
    {
        private static bool _isHeld;

        internal static bool TryAcquire()
        {
            if (_isHeld) return false;
            _isHeld = true;
            return true;
        }

        internal static void Release()
        {
            _isHeld = false;
        }
    }

    internal sealed class DeveloperShellFacade : IDeveloperShellFacade
    {
        private readonly AppRuntime _runtime;

        public DeveloperShellFacade(AppRuntime runtime)
        {
            _runtime = runtime;
        }

        public OdysseyRuntimeState RuntimeState => _runtime.State;
        public OdysseyRuntimeProfile RuntimeProfile => _runtime.Profile;
        public BuildIdAvailability BuildIdentityAvailability => BuildIdAvailability.UnavailableNotYetComposed;
        public Result<Odyssey.Application.Commands.CommandResult> RunAcceptedProbe() => _runtime.RunAcceptedProbe();
        public Result<Odyssey.Application.Commands.CommandResult> RunRejectedProbe() => _runtime.RunRejectedProbe();
        public void EmitDiagnosticProbe() => _runtime.EmitDiagnosticProbe();
        public System.Collections.Generic.IReadOnlyList<LogEventV1> GetRecentDiagnostics() => _runtime.GetRecentDiagnostics();
        public void RequestShutdown() => _runtime.Shutdown();
    }
}
