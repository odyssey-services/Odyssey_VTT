using System;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    /// <summary>
    /// ODY-S11-215: Tab / Shift+Tab move UI Toolkit focus forward / backward in tab order.
    /// <para>
    /// Why this exists: with an EventSystem + <c>InputSystemUIInputModule</c> driving UI Toolkit (our wiring, see
    /// <see cref="AppShellEntryPoint"/>), keyboard navigation comes only from the <c>UI/Navigate</c> action, and the
    /// module turns it into four spatial directions (Left/Right/Up/Down -- <c>InputSystemUIInputModule.ProcessNavigation</c>,
    /// Input System 1.19.0). It has no Next/Previous, so Tab never moved focus. Binding Tab into <c>Navigate</c> would
    /// only make Tab behave like an arrow key, with no way for Shift+Tab to go back.
    /// </para>
    /// <para>
    /// Same technique as Unity's own InputForUI provider (<c>InputSystemProvider.RegisterFixedActions</c>, used when no
    /// EventSystem is present): a code-created <c>&lt;Keyboard&gt;/tab</c> button action, Shift picks the direction, and
    /// the result is sent as a <see cref="NavigationMoveEvent"/> (Next/Previous) to the focused element, which UI
    /// Toolkit's focus ring turns into a tab-order focus change. Because it is a NavigationMoveEvent, the keyboard focus
    /// ring (<see cref="OdyFocusVisible"/>) reacts to it exactly as to arrow navigation.
    /// </para>
    /// Owned by <see cref="AppShellEntryPoint"/> (one per UI document); <see cref="Dispose"/> disables the action.
    /// One move per press (no key repeat while Tab is held).
    /// </summary>
    public sealed class UiTabNavigation : IDisposable
    {
        private readonly VisualElement _root;
        private readonly InputAction _tab;
        private bool _disposed;

        public UiTabNavigation(VisualElement root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _tab = new InputAction("odyssey-ui-tab-navigation", InputActionType.Button, "<Keyboard>/tab");
            _tab.performed += OnTabPerformed;
            _tab.Enable();
        }

        /// <summary>
        /// Moves focus one step (backwards for Shift+Tab). The event goes to the focused element, or to the document
        /// root when nothing is focused yet (UI Toolkit then focuses the first focusable element). Returns false when the
        /// root is not on a panel.
        /// </summary>
        public bool Navigate(bool backwards)
        {
            if (_disposed) return false;
            IPanel? panel = _root.panel;
            if (panel == null) return false;
            VisualElement target = panel.focusController.focusedElement as VisualElement ?? _root;
            using NavigationMoveEvent move = NavigationMoveEvent.GetPooled(
                backwards ? NavigationMoveEvent.Direction.Previous : NavigationMoveEvent.Direction.Next,
                backwards ? EventModifiers.Shift : EventModifiers.None);
            move.target = target;
            target.SendEvent(move);
            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _tab.performed -= OnTabPerformed;
            _tab.Disable();
            _tab.Dispose();
            _disposed = true;
        }

        private void OnTabPerformed(InputAction.CallbackContext context)
        {
            bool shift = context.control.device is Keyboard keyboard && keyboard.shiftKey.isPressed;
            Navigate(shift);
        }
    }
}
