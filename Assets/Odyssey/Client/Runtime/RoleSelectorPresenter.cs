using System;
using System.Collections.Generic;
using Odyssey.Application.Networking.Session;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    public sealed class RoleSelectorPresenter : IDisposable
    {
        private static readonly List<string> Choices = new List<string> { "Player", "MainGM", "Observer" };

        private readonly RoleSelection _selection;
        private readonly PresentationRuntime _presentationRuntime;
        private DropdownField? _dropdown;
        private IDisposable? _roleSubscription;
        private bool _disposed;

        public RoleSelectorPresenter(RoleSelection selection, PresentationRuntime presentationRuntime)
        {
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _presentationRuntime = presentationRuntime ?? throw new ArgumentNullException(nameof(presentationRuntime));
        }

        public VisualElement BuildView()
        {
            VisualElement row = new VisualElement { name = "role-selector" };
            row.AddToClassList("role-selector");

            _dropdown = new DropdownField("Playing as", Choices, IndexOf(_selection.Role)) { name = "role-selector-dropdown" };
            _dropdown.RegisterValueChangedCallback(OnDropdownChanged);
            _presentationRuntime.AddSubscription(new DropdownSubscription(_dropdown, OnDropdownChanged));
            row.Add(_dropdown);

            _roleSubscription = _selection.Subscribe(OnRoleChanged);
            _presentationRuntime.AddSubscription(_roleSubscription);
            return row;
        }

        public void SelectRole(BaselineRole role)
        {
            _selection.SelectRole(role);
            Refresh();
        }

        public void Refresh()
        {
            if (_dropdown != null) _dropdown.SetValueWithoutNotify(Display(_selection.Role));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _roleSubscription?.Dispose();
            _disposed = true;
        }

        private void OnDropdownChanged(ChangeEvent<string> evt)
        {
            SelectRole(Parse(evt.newValue));
        }

        private void OnRoleChanged(RoleSelectionSnapshot snapshot)
        {
            if (_dropdown != null) _dropdown.SetValueWithoutNotify(Display(snapshot.Role));
        }

        private static int IndexOf(BaselineRole role) => Choices.IndexOf(Display(role));

        private static string Display(BaselineRole role)
        {
            switch (role)
            {
                case BaselineRole.MainGM:
                    return "MainGM";
                case BaselineRole.Player:
                    return "Player";
                case BaselineRole.Observer:
                    return "Observer";
                default:
                    throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown baseline role.");
            }
        }

        private static BaselineRole Parse(string value)
        {
            switch (value)
            {
                case "MainGM":
                    return BaselineRole.MainGM;
                case "Player":
                    return BaselineRole.Player;
                case "Observer":
                    return BaselineRole.Observer;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown baseline role.");
            }
        }

        private sealed class DropdownSubscription : IDisposable
        {
            private readonly DropdownField _dropdown;
            private readonly EventCallback<ChangeEvent<string>> _callback;
            private bool _disposed;

            public DropdownSubscription(DropdownField dropdown, EventCallback<ChangeEvent<string>> callback)
            {
                _dropdown = dropdown;
                _callback = callback;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _dropdown.UnregisterValueChangedCallback(_callback);
                _disposed = true;
            }
        }
    }
}
