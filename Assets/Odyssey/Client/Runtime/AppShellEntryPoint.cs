using System;
using Odyssey.Application.Results;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class AppShellEntryPoint : MonoBehaviour
    {
        private UIDocument? _document;
        private DeveloperShellPresenter? _presenter;

        public bool IsInitialized => _presenter != null;

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

        private void OnDestroy()
        {
            _presenter?.Dispose();
            _presenter = null;
        }
    }
}
