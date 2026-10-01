using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    /// <summary>
    /// ODY-S11-219 (polish P1 item 9): the universal rule for cut-off text. A label made with
    /// <see cref="OdyUi.TruncatedText"/> stays on one line with an ellipsis; hovering it shows the full text in a small
    /// <see cref="OdyPopover"/> -- only when the text really is cut (it is measured on hover, so no tooltip appears for
    /// text that fits). Runtime UI Toolkit does not display the built-in <c>tooltip</c> property, hence the popover.
    /// Technique from Owlbear Rodeo 2.1 (full asset name on hover over an elided name); built anew here.
    /// The instance is kept in the label's <c>userData</c> (owned by the label; no registry).
    /// </summary>
    public sealed class OdyTruncationTooltip
    {
        /// <summary>Gap between the label and its tooltip, in pixels (executor's choice).</summary>
        public const float Offset = 4f;

        private readonly Label _label;
        private OdyPopover? _popover;

        private OdyTruncationTooltip(Label label)
        {
            _label = label;
            _label.AddToClassList(OdyClasses.TextTruncate);
            _label.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            _label.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            // A list re-render removes the label; its tooltip must not stay behind on the host.
            _label.RegisterCallback<DetachFromPanelEvent>(OnDetach);
            _label.userData = this;
        }

        /// <summary>Whether the label's text was cut the last time it was measured.</summary>
        public bool IsTruncated { get; private set; }

        public bool IsShowing => _popover != null && _popover.IsOpen;

        /// <summary>The open tooltip popover, if any. Exposed for tests.</summary>
        public OdyPopover? Popover => _popover;

        public static OdyTruncationTooltip Attach(Label label)
        {
            if (label == null) throw new ArgumentNullException(nameof(label));
            return label.userData as OdyTruncationTooltip ?? new OdyTruncationTooltip(label);
        }

        /// <summary>The tooltip attached to <paramref name="label"/> by <see cref="Attach"/>, or <c>null</c>.</summary>
        public static OdyTruncationTooltip? Of(Label label) => label?.userData as OdyTruncationTooltip;

        /// <summary>Text wider than the space it has (half a pixel of slack for rounding) is cut.</summary>
        public static bool WouldTruncate(float textWidth, float availableWidth) =>
            !float.IsNaN(textWidth) && !float.IsNaN(availableWidth) && availableWidth > 0f && textWidth > availableWidth + 0.5f;

        /// <summary>Records a measurement (used by the hover handler, and by tests, which have no layout).</summary>
        public void UpdateTruncation(float textWidth, float availableWidth)
        {
            IsTruncated = WouldTruncate(textWidth, availableWidth);
            if (!IsTruncated) Hide();
        }

        /// <summary>Shows the full text below the label if it is cut; returns whether a tooltip is showing.</summary>
        public bool Show()
        {
            if (!IsTruncated || string.IsNullOrEmpty(_label.text)) return false;
            if (IsShowing) return true;
            var text = new Label(_label.text) { name = "ody-tooltip-text", pickingMode = PickingMode.Ignore };
            var anchor = OdyPopoverAnchor.ToElement(_label, OdyPopoverOrigin.BottomLeft);
            anchor.OffsetY = Offset;
            _popover = OdyPopover.Show(OdyPopover.FindHost(_label), text, new OdyPopoverOptions(anchor)
            {
                Pivot = OdyPopoverOrigin.TopLeft,
                HidePaper = true,
                Name = "ody-tooltip",
                PaperClasses = new[] { OdyClasses.Tooltip }
            });
            // Never under the pointer's feet: the tooltip must not steal the hover from the label.
            _popover.Paper.pickingMode = PickingMode.Ignore;
            return true;
        }

        public void Hide()
        {
            if (_popover != null && _popover.IsOpen) _popover.Close();
            _popover = null;
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            Vector2 measured = _label.MeasureTextSize(_label.text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined);
            UpdateTruncation(measured.x, _label.contentRect.width);
            Show();
        }

        private void OnPointerLeave(PointerLeaveEvent evt) => Hide();

        private void OnDetach(DetachFromPanelEvent evt) => Hide();
    }
}
