using System;
using System.Collections.Generic;
using System.Text;
using Odyssey.Application.Commands;
using Odyssey.Application.Diagnostics;
using Odyssey.Application.Results;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    public interface IDeveloperShellFacade
    {
        OdysseyRuntimeState RuntimeState { get; }
        OdysseyRuntimeProfile RuntimeProfile { get; }
        BuildIdAvailability BuildIdentityAvailability { get; }
        Result<CommandResult> RunAcceptedProbe();
        Result<CommandResult> RunRejectedProbe();
        void EmitDiagnosticProbe();
        IReadOnlyList<LogEventV1> GetRecentDiagnostics();
        void RequestShutdown();
    }

    public sealed class DeveloperShellPresenter : IDisposable
    {
        private readonly UIDocument _document;
        private readonly IDeveloperShellFacade _facade;
        private readonly PresentationRuntime _presentationRuntime;
        private Label? _state;
        private Label? _profile;
        private Label? _buildIdentity;
        private Label? _result;
        private Label? _diagnostics;
        private bool _disposed;

        public DeveloperShellPresenter(UIDocument document, IDeveloperShellFacade facade, PresentationRuntime presentationRuntime)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _facade = facade ?? throw new ArgumentNullException(nameof(facade));
            _presentationRuntime = presentationRuntime ?? throw new ArgumentNullException(nameof(presentationRuntime));
        }

        public Result Initialize()
        {
            try
            {
                BuildView();
                Refresh();
                return Result.Success();
            }
            catch
            {
                return Result.Failure(RuntimeErrors.CompositionInvalid());
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }

        private void BuildView()
        {
            VisualElement root = _document.rootVisualElement;
            VisualElement appRoot = root.Q<VisualElement>("odyssey-root") ?? root;
            appRoot.Clear();
            appRoot.AddToClassList("app-root");

            Label title = new Label("Odyssey Developer Shell");
            title.name = "shell-title";
            title.AddToClassList("shell-title");
            appRoot.Add(title);

            _state = new Label { name = "runtime-state" };
            _state.AddToClassList("shell-state");
            appRoot.Add(_state);

            _profile = new Label { name = "runtime-profile" };
            appRoot.Add(_profile);
            _buildIdentity = new Label { name = "build-identity" };
            appRoot.Add(_buildIdentity);

            VisualElement actions = new VisualElement();
            actions.AddToClassList("shell-actions");
            AddButton(actions, "accepted-probe-button", "Run Accepted Probe", ExecuteAcceptedProbe);
            AddButton(actions, "rejected-probe-button", "Run Rejected Probe", ExecuteRejectedProbe);
            AddButton(actions, "diagnostic-button", "Emit Diagnostic", EmitDiagnostic);
            AddButton(actions, "shutdown-button", "Shutdown", RequestShutdown);
            appRoot.Add(actions);

            _result = new Label { name = "shell-result" };
            _result.AddToClassList("shell-result");
            appRoot.Add(_result);

            _diagnostics = new Label { name = "shell-diagnostics" };
            _diagnostics.AddToClassList("shell-diagnostics");
            appRoot.Add(_diagnostics);
        }

        private void AddButton(VisualElement parent, string name, string text, Action handler)
        {
            Button button = new Button { name = name, text = text };
            button.userData = handler;
            button.clicked += handler;
            _presentationRuntime.AddSubscription(new ButtonSubscription(button, handler));
            parent.Add(button);
        }

        private void ExecuteAcceptedProbe()
        {
            Result<CommandResult> result = _facade.RunAcceptedProbe();
            _result!.text = result.IsSuccess ? "Accepted Probe: " + result.Value.Status : "Accepted Probe: " + result.Error.SafeReasonCode;
            Refresh();
        }

        private void ExecuteRejectedProbe()
        {
            Result<CommandResult> result = _facade.RunRejectedProbe();
            _result!.text = result.IsSuccess ? "Rejected Probe: " + result.Value.Status : "Rejected Probe: " + result.Error.SafeReasonCode;
            Refresh();
        }

        private void EmitDiagnostic()
        {
            _facade.EmitDiagnosticProbe();
            _result!.text = "Diagnostic: emitted";
            Refresh();
        }

        private void RequestShutdown()
        {
            SetStateText(OdysseyRuntimeState.ShuttingDown);
            _facade.RequestShutdown();
            Refresh();
        }

        public void Refresh()
        {
            SetStateText(_facade.RuntimeState);
            _profile!.text = "Runtime profile: " + _facade.RuntimeProfile;
            _buildIdentity!.text = "Build identity: " + (_facade.BuildIdentityAvailability == BuildIdAvailability.Available ? "available" : "unavailable");
            StringBuilder builder = new StringBuilder();
            foreach (LogEventV1 logEvent in _facade.GetRecentDiagnostics())
            {
                builder.Append(logEvent.Level).Append("  ").Append(logEvent.EventCode).Append('\n');
            }

            _diagnostics!.text = builder.Length == 0 ? "Diagnostics: empty" : builder.ToString();
        }

        private void SetStateText(OdysseyRuntimeState state)
        {
            if (_state != null) _state.text = "State: " + state;
        }

        private sealed class ButtonSubscription : IDisposable
        {
            private readonly Button _button;
            private readonly Action _handler;
            private bool _disposed;

            public ButtonSubscription(Button button, Action handler)
            {
                _button = button;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _button.clicked -= _handler;
                _disposed = true;
            }
        }
    }
}
