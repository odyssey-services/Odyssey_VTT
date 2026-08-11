using System;
using System.Text;
using Odyssey.Application.Commands;
using Odyssey.Application.Diagnostics;
using Odyssey.Application.Results;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class DeveloperShellPresenter : MonoBehaviour
    {
        private UIDocument? _document;
        private AppRuntime? _runtime;
        private PresentationRuntime? _presentationRuntime;
        private Label? _state;
        private Label? _result;
        private Label? _diagnostics;

        public bool IsBound => _runtime != null;
        public PresentationRuntime? PresentationRuntime => _presentationRuntime;

        public void Bind(AppRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _presentationRuntime = new PresentationRuntime();
            _runtime.AttachPresentationRuntime(_presentationRuntime);
            BuildView();
            Refresh();
        }

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            BuildView();
            SetStateText(OdysseyRuntimeState.Starting);
        }

        private void OnDestroy()
        {
            _presentationRuntime?.Dispose();
            _presentationRuntime = null;
        }

        private void BuildView()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
            VisualElement root = _document.rootVisualElement;
            VisualElement appRoot = root.Q<VisualElement>("odyssey-root") ?? root;
            appRoot.Clear();
            appRoot.AddToClassList("app-root");

            Label title = new Label("Odyssey Developer Shell");
            title.AddToClassList("shell-title");
            appRoot.Add(title);

            _state = new Label();
            _state.AddToClassList("shell-state");
            appRoot.Add(_state);

            VisualElement actions = new VisualElement();
            actions.AddToClassList("shell-actions");
            Button probe = new Button(ExecuteProbe) { text = "Probe" };
            Button diagnostic = new Button(EmitDiagnostic) { text = "Diagnostic" };
            Button shutdown = new Button(RequestShutdown) { text = "Shutdown" };
            actions.Add(probe);
            actions.Add(diagnostic);
            actions.Add(shutdown);
            appRoot.Add(actions);

            _result = new Label();
            _result.AddToClassList("shell-result");
            appRoot.Add(_result);

            _diagnostics = new Label();
            _diagnostics.AddToClassList("shell-diagnostics");
            appRoot.Add(_diagnostics);
        }

        private void ExecuteProbe()
        {
            if (_runtime == null) return;
            Result<CommandResult> result = _runtime.ExecuteDeveloperProbe();
            if (result.IsSuccess)
            {
                _result!.text = "Probe: " + result.Value.Status;
            }
            else
            {
                _result!.text = "Probe: Rejected " + result.Error.SafeReasonCode;
            }

            Refresh();
        }

        private void EmitDiagnostic()
        {
            _runtime?.EmitDiagnosticProbe();
            Refresh();
        }

        private void RequestShutdown()
        {
            if (_runtime == null) return;
            SetStateText(OdysseyRuntimeState.ShuttingDown);
            _runtime.Shutdown();
            Refresh();
        }

        private void Refresh()
        {
            if (_runtime == null)
            {
                SetStateText(OdysseyRuntimeState.Starting);
                return;
            }

            SetStateText(_runtime.State);
            StringBuilder builder = new StringBuilder();
            foreach (LogEventV1 logEvent in _runtime.RingBuffer.Snapshot())
            {
                builder.Append(logEvent.Level).Append("  ").Append(logEvent.EventCode).Append('\n');
            }

            _diagnostics!.text = builder.Length == 0 ? "Diagnostics: empty" : builder.ToString();
        }

        private void SetStateText(OdysseyRuntimeState state)
        {
            if (_state != null)
            {
                _state.text = "State: " + state;
            }
        }
    }
}
