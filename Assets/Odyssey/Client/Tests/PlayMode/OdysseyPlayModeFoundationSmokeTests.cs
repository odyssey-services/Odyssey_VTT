using System.Collections;
using NUnit.Framework;
using Odyssey.Unity.Client;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Odyssey.Tests.Unity.PlayMode
{
    public sealed class OdysseyPlayModeFoundationSmokeTests
    {
        [UnityTest]
        public IEnumerator DeveloperShellBootstrapsAndRunsTechnicalActions()
        {
            const string bootstrapPath = "Assets/Odyssey/Client/Scenes/Bootstrap.unity";

            yield return SceneManager.LoadSceneAsync(bootstrapPath, LoadSceneMode.Single);
            yield return WaitUntil(() => FindAcceptedHosts() == 1);
            yield return WaitUntil(() => SceneManager.GetSceneByName("AppShell").isLoaded);
            yield return WaitUntil(() => FindEntryPoint() != null && FindEntryPoint()!.IsInitialized);

            AppShellEntryPoint entryPoint = FindEntryPoint()!;
            UIDocument document = entryPoint.GetComponent<UIDocument>();
            yield return WaitUntil(() => Text(document, "runtime-state") == "State: Ready");
            Assert.That(FindAcceptedHosts(), Is.EqualTo(1));
            Assert.That(FindEntryPointCount(), Is.EqualTo(1));
            Assert.That(Text(document, "runtime-state"), Is.EqualTo("State: Ready"));
            Assert.That(Text(document, "runtime-profile"), Is.EqualTo("Runtime profile: DeveloperShell"));
            Assert.That(Text(document, "build-identity"), Is.EqualTo("Build identity: unavailable"));

            Click(document, "accepted-probe-button");
            yield return null;
            Assert.That(Text(document, "shell-result"), Does.Contain("Accepted Probe: Accepted"));

            Click(document, "diagnostic-button");
            yield return null;
            Assert.That(Text(document, "shell-diagnostics"), Does.Contain("diagnostics.probe.emitted"));

            Click(document, "rejected-probe-button");
            yield return null;
            Assert.That(Text(document, "shell-result"), Does.Contain("Rejected Probe: Rejected"));

            Click(document, "shutdown-button");
            yield return null;
            Assert.That(Text(document, "runtime-state"), Is.EqualTo("State: Stopped"));
            Assert.That(FindAcceptedHosts(), Is.EqualTo(1));
        }

        private static IEnumerator WaitUntil(System.Func<bool> predicate)
        {
            float started = Time.realtimeSinceStartup;
            while (!predicate())
            {
                Assert.That(Time.realtimeSinceStartup - started, Is.LessThan(10f));
                yield return null;
            }
        }

        private static int FindAcceptedHosts()
        {
            int count = 0;
            foreach (OdysseyRuntimeHost host in Object.FindObjectsByType<OdysseyRuntimeHost>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (host.IsAcceptedHost) count++;
            }

            return count;
        }

        private static int FindEntryPointCount()
        {
            int count = 0;
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    count += root.GetComponentsInChildren<AppShellEntryPoint>(true).Length;
                }
            }

            return count;
        }

        private static AppShellEntryPoint? FindEntryPoint()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    AppShellEntryPoint[] entryPoints = root.GetComponentsInChildren<AppShellEntryPoint>(true);
                    if (entryPoints.Length > 0) return entryPoints[0];
                }
            }

            return null;
        }

        private static string Text(UIDocument document, string name)
        {
            return document.rootVisualElement.Q<Label>(name)!.text;
        }

        private static void Click(UIDocument document, string name)
        {
            Button button = document.rootVisualElement.Q<Button>(name)!;
            if (button.userData is System.Action action)
            {
                action();
                return;
            }

            using ClickEvent click = ClickEvent.GetPooled();
            click.target = button;
            button.SendEvent(click);
        }
    }
}
