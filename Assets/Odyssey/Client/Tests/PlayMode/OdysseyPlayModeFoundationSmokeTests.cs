using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Odyssey.Application.Diagnostics;
using Odyssey.Unity.Client;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

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
            Assert.That(Text(document, "build-identity"), Does.Contain("0.1.0-local."));
            Assert.That(Text(document, "build-identity"), Does.Contain("odyssey-local-"));

            GameObject duplicateHostObject = new GameObject("Duplicate Odyssey Runtime Host");
            OdysseyRuntimeHost duplicateHost = duplicateHostObject.AddComponent<OdysseyRuntimeHost>();
            yield return null;
            yield return null;
            yield return WaitUntil(() => FindAcceptedHost()!.Runtime!.GetRecentDiagnostics().Any(entry => entry.EventCode == OdysseyEventCodes.AppBootstrapDuplicateRejected));
            Assert.That(FindAcceptedHosts(), Is.EqualTo(1));
            Assert.That(duplicateHost == null || !duplicateHost.IsAcceptedHost, Is.True);
            Assert.That(FindAcceptedHost()!.Runtime!.GetRecentDiagnostics(), Has.Some.Matches<LogEventV1>(entry => entry.EventCode == OdysseyEventCodes.AppBootstrapDuplicateRejected));
            if (duplicateHostObject != null) Object.Destroy(duplicateHostObject);

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
            Assert.That(FindAcceptedHosts(), Is.EqualTo(0));
            Assert.That(RuntimeHostLease.IsHeld, Is.False);
        }

        [UnityTest]
        public IEnumerator AppShellSceneUnloadDetachesPresentationRuntime()
        {
            const string bootstrapPath = "Assets/Odyssey/Client/Scenes/Bootstrap.unity";

            yield return SceneManager.LoadSceneAsync(bootstrapPath, LoadSceneMode.Single);
            yield return WaitUntil(() => FindAcceptedHosts() == 1);
            yield return WaitUntil(() => SceneManager.GetSceneByName("AppShell").isLoaded);
            yield return WaitUntil(() => FindEntryPoint() != null && FindEntryPoint()!.IsInitialized);

            OdysseyRuntimeHost host = FindAcceptedHost()!;
            Assert.That(host.Runtime, Is.Not.Null);
            Assert.That(host.Runtime!.HasPresentationRuntime, Is.True);
            Scene appShell = SceneManager.GetSceneByName("AppShell");

            yield return SceneManager.UnloadSceneAsync(appShell);
            yield return null;

            Assert.That(host.Runtime.HasPresentationRuntime, Is.False);
            Assert.That(host.Runtime.State, Is.EqualTo(OdysseyRuntimeState.StartupFailed));
            host.Runtime.Shutdown();
            Object.Destroy(host.gameObject);
            yield return null;
            Assert.That(RuntimeHostLease.IsHeld, Is.False);
        }

        [UnityTest]
        public IEnumerator DeveloperShellLaunchesTrialScreen()
        {
            const string bootstrapPath = "Assets/Odyssey/Client/Scenes/Bootstrap.unity";

            yield return SceneManager.LoadSceneAsync(bootstrapPath, LoadSceneMode.Single);
            yield return WaitUntil(() => FindAcceptedHosts() == 1);
            yield return WaitUntil(() => SceneManager.GetSceneByName("AppShell").isLoaded);
            yield return WaitUntil(() => FindEntryPoint() != null && FindEntryPoint()!.IsInitialized);

            AppShellEntryPoint entryPoint = FindEntryPoint()!;
            UIDocument document = entryPoint.GetComponent<UIDocument>();
            Click(document, "trial-ui-button");
            yield return WaitUntil(() => document.rootVisualElement.Q<VisualElement>("trial-screen") != null);

            Assert.That(document.rootVisualElement.Q<VisualElement>("board-area"), Is.Not.Null);
            Assert.That(document.rootVisualElement.Q<VisualElement>("role-selector"), Is.Not.Null);
            Assert.That(document.rootVisualElement.Q<VisualElement>("roll-panel"), Is.Not.Null);
            Assert.That(document.rootVisualElement.Q<VisualElement>("game-log"), Is.Not.Null);

            OdysseyRuntimeHost host = FindAcceptedHost()!;
            host.Runtime!.Shutdown();
            Object.Destroy(host.gameObject);
            yield return null;
            Assert.That(RuntimeHostLease.IsHeld, Is.False);
        }

        [UnityTest]
        public IEnumerator RealMouseClick_RoutesThroughUiToolkitInputToDeveloperShellAndTrialScreenElements()
        {
            const string bootstrapPath = "Assets/Odyssey/Client/Scenes/Bootstrap.unity";

            AssertUiInputActionsConfigured();
            InputTestFixture input = new();
            Mouse mouse = null;
            try
            {
                input.Setup();
                mouse = InputSystem.AddDevice<Mouse>();

                yield return SceneManager.LoadSceneAsync(bootstrapPath, LoadSceneMode.Single);
                yield return WaitUntil(() => FindAcceptedHosts() == 1);
                yield return WaitUntil(() => SceneManager.GetSceneByName("AppShell").isLoaded);
                yield return WaitUntil(() => FindEntryPoint() != null && FindEntryPoint()!.IsInitialized);

                AppShellEntryPoint entryPoint = FindEntryPoint()!;
                UIDocument document = entryPoint.GetComponent<UIDocument>();
                yield return WaitUntil(() => ButtonReady(document, "accepted-probe-button"));
                yield return ClickWithMouse(document, input, mouse, "accepted-probe-button");
                yield return WaitUntil(() => Text(document, "shell-result").Contains("Accepted Probe: Accepted"));

                yield return WaitUntil(() => ButtonReady(document, "trial-ui-button"));

                yield return ClickWithMouse(document, input, mouse, "trial-ui-button");
                yield return WaitUntil(() => document.rootVisualElement.Q<VisualElement>("trial-screen") != null);
                yield return WaitUntil(() => FirstToken(document) != null && ElementReady(FirstToken(document)!));

                yield return ClickWithMouse(document, input, mouse, FirstToken(document)!);
                yield return WaitUntil(() => Text(document, "board-status").StartsWith("Selected token", StringComparison.Ordinal));

                OdysseyRuntimeHost host = FindAcceptedHost()!;
                host.Runtime!.Shutdown();
                Object.Destroy(host.gameObject);
                yield return null;
                Assert.That(RuntimeHostLease.IsHeld, Is.False);
            }
            finally
            {
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                input.TearDown();
            }
        }

        [UnityTest]
        public IEnumerator RealMouseClick_TrialControlsColumnRowsDoNotOverlap()
        {
            const string bootstrapPath = "Assets/Odyssey/Client/Scenes/Bootstrap.unity";

            InputTestFixture input = new();
            Mouse mouse = null;
            try
            {
                input.Setup();
                mouse = InputSystem.AddDevice<Mouse>();

                yield return SceneManager.LoadSceneAsync(bootstrapPath, LoadSceneMode.Single);
                yield return WaitUntil(() => FindAcceptedHosts() == 1);
                yield return WaitUntil(() => SceneManager.GetSceneByName("AppShell").isLoaded);
                yield return WaitUntil(() => FindEntryPoint() != null && FindEntryPoint()!.IsInitialized);

                UIDocument document = FindEntryPoint()!.GetComponent<UIDocument>();
                yield return WaitUntil(() => ButtonReady(document, "trial-ui-button"));
                yield return ClickWithMouse(document, input, mouse, "trial-ui-button");
                yield return WaitUntil(() => document.rootVisualElement.Q<VisualElement>("trial-controls-column") != null);
                yield return WaitUntil(() => ControlsColumnRowsReady(document));

                AssertRowsDoNotOverlap(document,
                    "roll-audience",
                    "roll-formula",
                    "roll-button",
                    "modifier-row",
                    "modifier-decision-row",
                    "override-row",
                    "roll-result",
                    "roll-status",
                    "roll-lifecycle-row",
                    "game-log-save-reopen-button",
                    "game-log-list",
                    "game-log-status");

                OdysseyRuntimeHost host = FindAcceptedHost()!;
                host.Runtime!.Shutdown();
                Object.Destroy(host.gameObject);
                yield return null;
                Assert.That(RuntimeHostLease.IsHeld, Is.False);
            }
            finally
            {
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                input.TearDown();
            }
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

        private static OdysseyRuntimeHost? FindAcceptedHost()
        {
            foreach (OdysseyRuntimeHost host in Object.FindObjectsByType<OdysseyRuntimeHost>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (host.IsAcceptedHost) return host;
            }

            return null;
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

        private static bool ButtonReady(UIDocument document, string name)
        {
            Button button = document.rootVisualElement.Q<Button>(name);
            return button != null && ElementReady(button);
        }

        private static bool ElementReady(VisualElement element)
        {
            return element.panel != null && element.worldBound.width > 0f && element.worldBound.height > 0f;
        }

        private static bool ControlsColumnRowsReady(UIDocument document)
        {
            return new[]
            {
                "roll-audience",
                "roll-formula",
                "roll-button",
                "modifier-row",
                "modifier-decision-row",
                "override-row",
                "roll-result",
                "roll-status",
                "roll-lifecycle-row",
                "game-log-save-reopen-button",
                "game-log-list",
                "game-log-status",
            }.All(name =>
            {
                VisualElement element = document.rootVisualElement.Q<VisualElement>(name);
                return element != null && ElementReady(element);
            });
        }

        private static void AssertRowsDoNotOverlap(UIDocument document, params string[] names)
        {
            for (int index = 0; index < names.Length - 1; index++)
            {
                VisualElement current = document.rootVisualElement.Q<VisualElement>(names[index])!;
                VisualElement next = document.rootVisualElement.Q<VisualElement>(names[index + 1])!;
                Rect currentBounds = current.worldBound;
                Rect nextBounds = next.worldBound;

                Assert.That(currentBounds.width, Is.GreaterThan(0f), names[index] + " width");
                Assert.That(currentBounds.height, Is.GreaterThan(0f), names[index] + " height");
                Assert.That(nextBounds.yMin, Is.GreaterThanOrEqualTo(currentBounds.yMin), names[index + 1] + " should be below " + names[index]);
                Assert.That(currentBounds.Overlaps(nextBounds), Is.False, names[index] + " overlaps " + names[index + 1]);
            }
        }

        private static VisualElement? FirstToken(UIDocument document)
        {
            return document.rootVisualElement.Query<VisualElement>().Where(element => element.name != null && element.name.StartsWith("token-", StringComparison.Ordinal)).First();
        }

        private static void AssertUiInputActionsConfigured()
        {
            InputActionAsset actions = InputSystem.actions;
            Assert.That(actions, Is.Not.Null, "Project-wide input actions are not loaded.");
            InputAction click = actions.FindAction("UI/Click", true);
            InputAction rightClick = actions.FindAction("UI/RightClick", true);
            InputAction middleClick = actions.FindAction("UI/MiddleClick", true);
            Assert.That(click.type, Is.EqualTo(InputActionType.PassThrough));
            Assert.That(rightClick.type, Is.EqualTo(InputActionType.PassThrough));
            Assert.That(middleClick.type, Is.EqualTo(InputActionType.PassThrough));
        }

        private static IEnumerator ClickWithMouse(UIDocument document, InputTestFixture input, Mouse mouse, string name)
        {
            return ClickWithMouse(document, input, mouse, document.rootVisualElement.Q<VisualElement>(name)!);
        }

        private static IEnumerator ClickWithMouse(UIDocument document, InputTestFixture input, Mouse mouse, VisualElement element)
        {
            Vector2 panelPosition = element.worldBound.center;
            Vector2 bottomLeftPosition = new(panelPosition.x, Screen.height - panelPosition.y);
            Vector2 screenPosition = bottomLeftPosition.y < 0f ? panelPosition : bottomLeftPosition;
            Vector2 startPosition = screenPosition + new Vector2(4f, 4f);

            mouse.MakeCurrent();
            input.Move(mouse.position, startPosition);
            yield return null;

            input.Move(mouse.position, screenPosition);
            yield return null;

            input.Press(mouse.leftButton);
            yield return null;

            input.Release(mouse.leftButton);
            yield return null;
            yield return null;
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
