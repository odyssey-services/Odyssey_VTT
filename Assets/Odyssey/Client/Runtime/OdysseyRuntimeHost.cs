using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Odyssey.Unity.Client
{
    public sealed class OdysseyRuntimeHost : MonoBehaviour
    {
        private readonly OdysseyRuntimeCompositionRoot _compositionRoot = new OdysseyRuntimeCompositionRoot();
        private AppRuntime? _runtime;

        public AppRuntime? Runtime => _runtime;

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
            string markerDirectory = Path.Combine(UnityEngine.Application.temporaryCachePath, "OdysseyDiagnostics");
            var result = _compositionRoot.Start(OdysseyRuntimeConfiguration.DeveloperShell(markerDirectory));
            if (result.IsFailure)
            {
                Debug.LogError(result.Error.UserMessageKey.ToString());
                return;
            }

            _runtime = result.Value;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.LoadSceneAsync("Assets/Odyssey/Client/Scenes/AppShell.unity", LoadSceneMode.Additive);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_runtime == null || scene.name != "AppShell") return;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                DeveloperShellPresenter[] presenters = root.GetComponentsInChildren<DeveloperShellPresenter>(true);
                for (int index = 0; index < presenters.Length; index++)
                {
                    presenters[index].Bind(_runtime);
                }
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_runtime != null)
            {
                _runtime.Shutdown();
                _runtime = null;
            }
        }
    }
}
