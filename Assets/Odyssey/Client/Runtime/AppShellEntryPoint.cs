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

        public Result<PresentationRuntime> Initialize(IDeveloperShellFacade facade)
        {
            if (facade == null) throw new ArgumentNullException(nameof(facade));
            _document = GetComponent<UIDocument>();
            PresentationRuntime presentationRuntime = new PresentationRuntime();
            _presenter = new DeveloperShellPresenter(_document, facade, presentationRuntime);
            Result result = _presenter.Initialize();
            if (result.IsFailure)
            {
                presentationRuntime.Dispose();
                return Result<PresentationRuntime>.Failure(result.Error);
            }

            return Result<PresentationRuntime>.Success(presentationRuntime);
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
