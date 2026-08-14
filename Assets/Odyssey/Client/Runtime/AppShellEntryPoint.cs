using System;
using Odyssey.Application.Results;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class AppShellEntryPoint : MonoBehaviour
    {
        private UIDocument? _document;
        private DeveloperShellPresenter? _presenter;

        public bool IsInitialized => _presenter != null;
        internal bool HasDisplayedUiRoot => _document != null && _document.rootVisualElement.panel != null && _document.rootVisualElement.childCount > 0;

        public Result Initialize(IDeveloperShellFacade facade, PresentationRuntime presentationRuntime)
        {
            if (facade == null) throw new ArgumentNullException(nameof(facade));
            if (presentationRuntime == null) throw new ArgumentNullException(nameof(presentationRuntime));
            _document = GetComponent<UIDocument>();
            _presenter = new DeveloperShellPresenter(_document, facade, presentationRuntime);
            Result result = _presenter.Initialize();
            if (result.IsFailure)
            {
                _presenter.Dispose();
                _presenter = null;
                return result;
            }

            return Result.Success();
        }

        public void ShowStartupFailed(Error error)
        {
            if (_document == null) _document = GetComponent<UIDocument>();
            VisualElement root = _document.rootVisualElement;
            root.Clear();
            Label label = new Label("State: StartupFailed") { name = "runtime-state" };
            root.Add(label);
            Label reason = new Label("Failure: " + error.SafeReasonCode) { name = "shell-result" };
            root.Add(reason);
        }

        public void Refresh()
        {
            _presenter?.Refresh();
        }

        internal PlayerSmokeInputResult RunPlayerSmokeInputProbe()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
            return PlayerSmokeInputProbe.Run(_document);
        }

        private void OnDestroy()
        {
            _presenter?.Dispose();
            _presenter = null;
        }
    }

    internal readonly struct PlayerSmokeInputResult
    {
        internal PlayerSmokeInputResult(bool submitPerformed, bool cancelPerformed)
        {
            SubmitPerformed = submitPerformed;
            CancelPerformed = cancelPerformed;
        }

        internal bool SubmitPerformed { get; }
        internal bool CancelPerformed { get; }
    }

    internal static class PlayerSmokeInputProbe
    {
        private const string InputActionsJson = @"{
  ""version"": 1,
  ""name"": ""Odyssey"",
  ""maps"": [
    {
      ""name"": ""UI"",
      ""id"": ""7bda6e7f-9a43-4f8b-8d78-3ef60bbf79a1"",
      ""actions"": [
        { ""name"": ""Submit"", ""type"": ""Button"", ""id"": ""26335f6f-99c9-401a-8ec9-a9f5427a2f7b"", ""expectedControlType"": ""Button"", ""processors"": """", ""interactions"": """", ""initialStateCheck"": false },
        { ""name"": ""Cancel"", ""type"": ""Button"", ""id"": ""d0b64a77-3f16-40a2-8d95-76a5d6574379"", ""expectedControlType"": ""Button"", ""processors"": """", ""interactions"": """", ""initialStateCheck"": false }
      ],
      ""bindings"": [
        { ""name"": """", ""id"": ""2a2b197d-021d-43f0-998f-b641917e8c5d"", ""path"": ""<Keyboard>/enter"", ""interactions"": """", ""processors"": """", ""groups"": ""Keyboard&Mouse"", ""action"": ""Submit"", ""isComposite"": false, ""isPartOfComposite"": false },
        { ""name"": """", ""id"": ""65acfa0d-624d-4961-96d5-dc65f26c66ef"", ""path"": ""<Keyboard>/escape"", ""interactions"": """", ""processors"": """", ""groups"": ""Keyboard&Mouse"", ""action"": ""Cancel"", ""isComposite"": false, ""isPartOfComposite"": false }
      ]
    }
  ],
  ""controlSchemes"": []
}";

        internal static PlayerSmokeInputResult Run(UIDocument document)
        {
            bool submitPerformed = false;
            bool cancelPerformed = false;
            InputActionAsset asset = InputActionAsset.FromJson(InputActionsJson);
            Keyboard keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            try
            {
                InputAction submit = asset.FindAction("UI/Submit", true);
                InputAction cancel = asset.FindAction("UI/Cancel", true);
                submit.performed += _ =>
                {
                    InvokeButton(document, "accepted-probe-button");
                    submitPerformed = true;
                };
                cancel.performed += _ =>
                {
                    InvokeButton(document, "shutdown-button");
                    cancelPerformed = true;
                };
                asset.Enable();

                PressAndRelease(keyboard, Key.Enter);
                PressAndRelease(keyboard, Key.Escape);
                return new PlayerSmokeInputResult(submitPerformed, cancelPerformed);
            }
            finally
            {
                asset.Disable();
                UnityEngine.Object.Destroy(asset);
            }
        }

        private static void InvokeButton(UIDocument document, string name)
        {
            Button button = document.rootVisualElement.Q<Button>(name);
            if (button == null) return;
            if (button.userData is Action action)
            {
                action();
                return;
            }

            using ClickEvent click = ClickEvent.GetPooled();
            click.target = button;
            button.SendEvent(click);
        }

        private static void PressAndRelease(Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
        }
    }
}
