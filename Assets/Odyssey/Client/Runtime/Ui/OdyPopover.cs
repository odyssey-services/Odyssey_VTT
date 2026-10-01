using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    public enum OdyPopoverHorizontal
    {
        Left = 1,
        Center = 2,
        Right = 3
    }

    public enum OdyPopoverVertical
    {
        Top = 1,
        Center = 2,
        Bottom = 3
    }

    /// <summary>A point of a rectangle, named by its horizontal and vertical position (e.g. Left/Bottom = bottom-left corner).</summary>
    public readonly struct OdyPopoverOrigin
    {
        public OdyPopoverOrigin(OdyPopoverHorizontal horizontal, OdyPopoverVertical vertical)
        {
            Horizontal = horizontal;
            Vertical = vertical;
        }

        public OdyPopoverHorizontal Horizontal { get; }
        public OdyPopoverVertical Vertical { get; }

        public static OdyPopoverOrigin TopLeft => new OdyPopoverOrigin(OdyPopoverHorizontal.Left, OdyPopoverVertical.Top);
        public static OdyPopoverOrigin TopRight => new OdyPopoverOrigin(OdyPopoverHorizontal.Right, OdyPopoverVertical.Top);
        public static OdyPopoverOrigin BottomLeft => new OdyPopoverOrigin(OdyPopoverHorizontal.Left, OdyPopoverVertical.Bottom);
        public static OdyPopoverOrigin Center => new OdyPopoverOrigin(OdyPopoverHorizontal.Center, OdyPopoverVertical.Center);

        internal float FractionX => Horizontal == OdyPopoverHorizontal.Left ? 0f : Horizontal == OdyPopoverHorizontal.Center ? 0.5f : 1f;
        internal float FractionY => Vertical == OdyPopoverVertical.Top ? 0f : Vertical == OdyPopoverVertical.Center ? 0.5f : 1f;
    }

    /// <summary>Plain rectangle in the popover host's local coordinates (keeps the placement math free of engine types).</summary>
    public readonly struct OdyRect
    {
        public OdyRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        public float Right => X + Width;
        public float Bottom => Y + Height;

        public bool IsUsable => !float.IsNaN(X) && !float.IsNaN(Y) && !float.IsNaN(Width) && !float.IsNaN(Height) && Width >= 0f && Height >= 0f;

        public override string ToString() => "(" + X + ", " + Y + ", " + Width + " x " + Height + ")";
    }

    /// <summary>
    /// What a popover is attached to: an element (a point of its rectangle) or fixed coordinates in the host. Either way
    /// an optional pixel offset is added.
    /// </summary>
    public sealed class OdyPopoverAnchor
    {
        private OdyPopoverAnchor(VisualElement? element, OdyPopoverOrigin origin, float x, float y)
        {
            Element = element;
            Origin = origin;
            X = x;
            Y = y;
        }

        public VisualElement? Element { get; }

        /// <summary>For an element anchor: which point of the element's rectangle the popover attaches to.</summary>
        public OdyPopoverOrigin Origin { get; }

        /// <summary>For a point anchor: host-local coordinates.</summary>
        public float X { get; }
        public float Y { get; }

        public float OffsetX { get; set; }
        public float OffsetY { get; set; }

        public static OdyPopoverAnchor ToElement(VisualElement element, OdyPopoverOrigin origin)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            return new OdyPopoverAnchor(element, origin, 0f, 0f);
        }

        public static OdyPopoverAnchor ToPoint(float x, float y) => new OdyPopoverAnchor(null, OdyPopoverOrigin.TopLeft, x, y);
    }

    public enum OdyPopoverCloseReason
    {
        Programmatic = 1,
        ClickAway = 2,

        /// <summary>ODY-S11-225: the Escape key (the <c>UI/Cancel</c> action) closed the topmost popover.</summary>
        Escape = 3
    }

    /// <summary>Options of an <see cref="OdyPopover"/>.</summary>
    public sealed class OdyPopoverOptions
    {
        public OdyPopoverOptions(OdyPopoverAnchor anchor)
        {
            Anchor = anchor ?? throw new ArgumentNullException(nameof(anchor));
        }

        public OdyPopoverAnchor Anchor { get; }

        /// <summary>
        /// Which point of the popover sits on the anchor point -- i.e. the growth direction. Left/Top grows right and
        /// down, Right/Top grows left and down, Center/Center is centered on the anchor.
        /// </summary>
        public OdyPopoverOrigin Pivot { get; set; } = OdyPopoverOrigin.TopLeft;

        /// <summary>Fixed size in pixels; <c>null</c> = sized by its content (and the classes on the paper).</summary>
        public float? Width { get; set; }
        public float? Height { get; set; }

        /// <summary>Height runs from the placed top to the viewport's bottom edge minus <see cref="EdgeMargin"/> (side drawers).</summary>
        public bool FillToBottomEdge { get; set; }

        /// <summary>No default surface (background/border/padding): the content or the extra classes style it.</summary>
        public bool HidePaper { get; set; }

        /// <summary>Clicks outside the popover do not close it.</summary>
        public bool DisableClickAway { get; set; }

        /// <summary>A scrim covers the host behind the popover and blocks the screen below (confirmations).</summary>
        public bool Modal { get; set; }

        /// <summary>Minimum distance in pixels between the popover and every viewport edge; it is moved/shrunk to keep it.</summary>
        public float EdgeMargin { get; set; } = OdyPopover.DefaultEdgeMargin;

        /// <summary>Remove the elements from the host on close (one-shot popovers) instead of hiding them (persistent drawers).</summary>
        public bool RemoveOnClose { get; set; } = true;

        /// <summary>
        /// ODY-S11-225: Escape closes the popover when it is the topmost open one (default). Modal confirmations treat it
        /// as Cancel. Independent of <see cref="DisableClickAway"/>: an explicit key press is not an accidental click.
        /// </summary>
        public bool CloseOnEscape { get; set; } = true;

        /// <summary>
        /// Where a press counts as "away". Default: the host. Drawers use the board layer so the top bar and the other
        /// side's drawer do not close them.
        /// </summary>
        public VisualElement? ClickAwayScope { get; set; }

        /// <summary>Name of the outer element (the scrim for modal popovers, otherwise the paper itself).</summary>
        public string? Name { get; set; }

        /// <summary>Name of the paper when the popover is modal (otherwise the paper takes <see cref="Name"/>).</summary>
        public string? PaperName { get; set; }

        public IReadOnlyList<string> PaperClasses { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Pure placement: where the popover goes for a given anchor rectangle, size and viewport. Shared by
    /// <see cref="OdyPopover"/> and its tests.
    /// </summary>
    public static class OdyPopoverLayout
    {
        /// <summary>
        /// Puts the popover's <paramref name="pivot"/> on the anchor point, then shrinks it to at most the viewport minus
        /// the margin on both sides and moves it inside so it is never clipped. Width/height must be known (&gt;= 0).
        /// </summary>
        public static OdyRect Place(float anchorX, float anchorY, OdyPopoverOrigin pivot, float width, float height, OdyRect viewport, float margin)
        {
            float safeMargin = Math.Max(0f, margin);
            float maxWidth = Math.Max(0f, viewport.Width - 2f * safeMargin);
            float maxHeight = Math.Max(0f, viewport.Height - 2f * safeMargin);
            float w = Math.Min(Math.Max(0f, width), maxWidth);
            float h = Math.Min(Math.Max(0f, height), maxHeight);

            float left = anchorX - w * pivot.FractionX;
            float top = anchorY - h * pivot.FractionY;
            left = Clamp(left, viewport.X + safeMargin, viewport.Right - safeMargin - w);
            top = Clamp(top, viewport.Y + safeMargin, viewport.Bottom - safeMargin - h);
            return new OdyRect(left, top, w, h);
        }

        public static (float X, float Y) AnchorPoint(OdyRect anchorRect, OdyPopoverOrigin origin, float offsetX, float offsetY) =>
            (anchorRect.X + anchorRect.Width * origin.FractionX + offsetX, anchorRect.Y + anchorRect.Height * origin.FractionY + offsetY);

        private static float Clamp(float value, float min, float max) => max < min ? min : value < min ? min : value > max ? max : value;
    }

    /// <summary>
    /// ODY-S11-210 (polish P0): the one popover primitive of the client -- side drawers, confirmation dialogs and
    /// dropdown selectors are all built on it. Anchored to an element or coordinates, grows in the direction given by
    /// its pivot, fixed or content size (changeable while open), optional paper/scrim, optional click-away, and never
    /// clipped by the viewport (edge margin). Plain object owned by the presenter that creates it; it registers its
    /// pointer/geometry callbacks only while open and removes them on close.
    /// </summary>
    public sealed class OdyPopover
    {
        public const float DefaultEdgeMargin = 8f;

        private readonly VisualElement _host;
        private readonly OdyPopoverOptions _options;
        private readonly VisualElement _paper;
        private bool _callbacksRegistered;
        private VisualElement? _escapeScope;

        public OdyPopover(VisualElement host, VisualElement content, OdyPopoverOptions options)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (content == null) throw new ArgumentNullException(nameof(content));

            Width = options.Width;
            Height = options.Height;

            _paper = new VisualElement();
            _paper.AddToClassList(OdyClasses.PopoverPaper);
            if (options.HidePaper) _paper.AddToClassList(OdyClasses.PopoverPaperHidden);
            foreach (string paperClass in options.PaperClasses) _paper.AddToClassList(paperClass);
            _paper.style.position = Position.Absolute;
            _paper.Add(content);

            if (options.Modal)
            {
                Element = new VisualElement { name = options.Name ?? "ody-popover" };
                Element.AddToClassList(OdyClasses.Popover);
                Element.AddToClassList(OdyClasses.ModalScrim);
                _paper.name = options.PaperName ?? Element.name + "-paper";
                Element.Add(_paper);
            }
            else
            {
                _paper.name = options.Name ?? "ody-popover";
                _paper.AddToClassList(OdyClasses.Popover);
                Element = _paper;
            }

            // ODY-S11-225: lets the Escape handling find which open popover is on top (owned by the element itself).
            Element.userData = this;
            ApplySize();
        }

        /// <summary>Raised once per close, with the reason.</summary>
        public event Action<OdyPopoverCloseReason>? Closed;

        /// <summary>The outer element (scrim for modal popovers, otherwise the paper).</summary>
        public VisualElement Element { get; }

        public VisualElement Paper => _paper;
        public VisualElement Host => _host;
        public OdyPopoverOptions Options => _options;
        public bool IsOpen { get; private set; }
        public float? Width { get; private set; }
        public float? Height { get; private set; }

        /// <summary>The last applied placement (host-local), or <c>null</c> before the first layout.</summary>
        public OdyRect? Placement { get; private set; }

        public static OdyPopover Show(VisualElement host, VisualElement content, OdyPopoverOptions options)
        {
            var popover = new OdyPopover(host, content, options);
            popover.Open();
            return popover;
        }

        /// <summary>
        /// The host for popovers opened from inside a panel: the nearest ancestor marked <c>.ody-popover-host</c> (the
        /// game screen), else the topmost ancestor. Walks the element's own parents only -- no registry, no lookup.
        /// </summary>
        public static VisualElement FindHost(VisualElement from)
        {
            if (from == null) throw new ArgumentNullException(nameof(from));
            VisualElement current = from;
            while (true)
            {
                if (current.ClassListContains(OdyClasses.PopoverHost)) return current;
                if (current.parent == null) return current;
                current = current.parent;
            }
        }

        /// <summary>Adds the (closed, hidden) popover to its host so it can be found before it is first opened.</summary>
        public void Mount()
        {
            if (Element.parent == _host) return;
            _host.Add(Element);
            if (!IsOpen) OdyUi.SetVisible(Element, false);
        }

        public void Open()
        {
            if (Element.parent != _host) _host.Add(Element);
            OdyUi.SetVisible(Element, true);
            Element.BringToFront();
            if (!IsOpen) RegisterCallbacks();
            IsOpen = true;
            Reposition();
        }

        public void Close() => Close(OdyPopoverCloseReason.Programmatic);

        public void SetWidth(float? width)
        {
            Width = width;
            ApplySize();
            if (IsOpen) Reposition();
        }

        public void SetHeight(float? height)
        {
            Height = height;
            ApplySize();
            if (IsOpen) Reposition();
        }

        /// <summary>
        /// A press at <paramref name="target"/> (<c>null</c> = somewhere outside). Closes the popover unless click-away is
        /// disabled or the press is inside the popover or its anchor element. Public for tests; real presses arrive
        /// through the click-away scope's callback.
        /// </summary>
        public bool HandleClickAway(VisualElement? target)
        {
            if (!IsOpen || _options.DisableClickAway) return false;
            if (target != null && (IsWithin(_paper, target) || (_options.Anchor.Element != null && IsWithin(_options.Anchor.Element, target)))) return false;
            Close(OdyPopoverCloseReason.ClickAway);
            return true;
        }

        /// <summary>Re-places the popover from the current layout. Does nothing until the host has been laid out.</summary>
        public void Reposition()
        {
            if (!IsOpen) return;
            OdyRect viewport = ToRect(_host.worldBound);
            if (!viewport.IsUsable) return;
            OdyRect anchorRect = _options.Anchor.Element == null
                ? new OdyRect(_options.Anchor.X, _options.Anchor.Y, 0f, 0f)
                : ToHostLocal(ToRect(_options.Anchor.Element.worldBound), viewport);
            if (!anchorRect.IsUsable) return;
            UnityEngine.Rect measured = _paper.layout;
            float measuredWidth = float.IsNaN(measured.width) ? 0f : measured.width;
            float measuredHeight = float.IsNaN(measured.height) ? 0f : measured.height;
            // A fixed size can still be narrowed by a class (e.g. a drawer's max-width): place what is really drawn.
            float width = Width.HasValue ? (measuredWidth > 0f ? Math.Min(Width.Value, measuredWidth) : Width.Value) : measuredWidth;
            float height = Height.HasValue ? (measuredHeight > 0f ? Math.Min(Height.Value, measuredHeight) : Height.Value) : measuredHeight;
            PlaceWithin(new OdyRect(0f, 0f, viewport.Width, viewport.Height), anchorRect, width, height);
        }

        /// <summary>
        /// Applies a placement for known geometry (host-local viewport and anchor rectangle, popover content size).
        /// Used by <see cref="Reposition"/> and by tests, which have no layout pass.
        /// </summary>
        public OdyRect PlaceWithin(OdyRect viewport, OdyRect anchorRect, float width, float height)
        {
            (float x, float y) = OdyPopoverLayout.AnchorPoint(anchorRect, _options.Anchor.Element == null ? OdyPopoverOrigin.TopLeft : _options.Anchor.Origin, _options.Anchor.OffsetX, _options.Anchor.OffsetY);
            float effectiveHeight = height;
            if (_options.FillToBottomEdge) effectiveHeight = Math.Max(0f, viewport.Bottom - _options.EdgeMargin - y);
            OdyRect placed = OdyPopoverLayout.Place(x, y, _options.Pivot, width, effectiveHeight, viewport, _options.EdgeMargin);
            _paper.style.left = placed.X;
            _paper.style.top = placed.Y;
            // Content-sized popovers are capped instead of pinned, so they never outgrow the viewport and still resize
            // with their content (no feedback loop with the geometry callback).
            float margins = 2f * Math.Max(0f, _options.EdgeMargin);
            if (Width.HasValue) _paper.style.width = placed.Width;
            else _paper.style.maxWidth = Math.Max(0f, viewport.Width - margins);
            if (Height.HasValue || _options.FillToBottomEdge) _paper.style.height = placed.Height;
            else _paper.style.maxHeight = Math.Max(0f, viewport.Height - margins);
            Placement = placed;
            return placed;
        }

        /// <summary>
        /// ODY-S11-225: the Escape key. Closes this popover if it is the topmost open one under
        /// <paramref name="scopeRoot"/> and allows it; returns whether it closed. Public for tests; at runtime it is
        /// called from the <c>NavigationCancelEvent</c> handler.
        /// </summary>
        public bool HandleEscape(VisualElement scopeRoot)
        {
            if (!IsOpen || !_options.CloseOnEscape || !ReferenceEquals(TopmostOpen(scopeRoot), this)) return false;
            Close(OdyPopoverCloseReason.Escape);
            return true;
        }

        /// <summary>
        /// ODY-S11-225: the open popover painted on top under <paramref name="scopeRoot"/> -- the last visible popover in
        /// tree order (later siblings and children paint over earlier ones). Walks the tree; no registry.
        /// </summary>
        public static OdyPopover? TopmostOpen(VisualElement scopeRoot)
        {
            if (scopeRoot == null) throw new ArgumentNullException(nameof(scopeRoot));
            OdyPopover? topmost = null;
            var pending = new Stack<VisualElement>();
            pending.Push(scopeRoot);
            while (pending.Count > 0)
            {
                VisualElement current = pending.Pop();
                if (!OdyUi.IsVisible(current)) continue;
                if (current.userData is OdyPopover popover && popover.IsOpen && ReferenceEquals(popover.Element, current)) topmost = popover;
                var children = new List<VisualElement>(current.Children());
                for (int index = children.Count - 1; index >= 0; index--) pending.Push(children[index]);
            }

            return topmost;
        }

        private static VisualElement RootOf(VisualElement element)
        {
            VisualElement current = element;
            while (current.parent != null) current = current.parent;
            return current;
        }

        private void Close(OdyPopoverCloseReason reason)
        {
            if (!IsOpen) return;
            IsOpen = false;
            UnregisterCallbacks();
            if (_options.RemoveOnClose) Element.RemoveFromHierarchy();
            else OdyUi.SetVisible(Element, false);
            Closed?.Invoke(reason);
        }

        private void ApplySize()
        {
            if (Width.HasValue) _paper.style.width = Width.Value;
            else _paper.style.width = StyleKeyword.Null;
            if (Height.HasValue) _paper.style.height = Height.Value;
            else if (!_options.FillToBottomEdge) _paper.style.height = StyleKeyword.Null;
        }

        private VisualElement ClickAwayScope => _options.ClickAwayScope ?? _host;

        private void RegisterCallbacks()
        {
            if (_callbacksRegistered) return;
            if (_options.Modal) Element.RegisterCallback<PointerDownEvent>(OnScrimPointerDown);
            else ClickAwayScope.RegisterCallback<PointerDownEvent>(OnScopePointerDown, TrickleDown.TrickleDown);
            _host.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _paper.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            // ODY-S11-225: Escape arrives as NavigationCancelEvent (UI/Cancel); listen at the top of the tree so it is
            // seen wherever focus is.
            _escapeScope = RootOf(_host);
            _escapeScope.RegisterCallback<NavigationCancelEvent>(OnNavigationCancel, TrickleDown.TrickleDown);
            _callbacksRegistered = true;
        }

        private void UnregisterCallbacks()
        {
            if (!_callbacksRegistered) return;
            if (_options.Modal) Element.UnregisterCallback<PointerDownEvent>(OnScrimPointerDown);
            else ClickAwayScope.UnregisterCallback<PointerDownEvent>(OnScopePointerDown, TrickleDown.TrickleDown);
            _host.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _paper.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _escapeScope?.UnregisterCallback<NavigationCancelEvent>(OnNavigationCancel, TrickleDown.TrickleDown);
            _escapeScope = null;
            _callbacksRegistered = false;
        }

        // The scrim only receives presses that miss the paper.
        private void OnScrimPointerDown(PointerDownEvent evt)
        {
            if (evt.target == Element) HandleClickAway(null);
        }

        // Trickle-down and never stopped: the press still reaches whatever is below (the map keeps panning/selecting).
        private void OnScopePointerDown(PointerDownEvent evt) => HandleClickAway(evt.target as VisualElement);

        private void OnGeometryChanged(GeometryChangedEvent evt) => Reposition();

        // Every open popover hears the same event; only the topmost closes, and it stops the event so that no other
        // popover (which would be topmost now) closes on the same key press.
        private void OnNavigationCancel(NavigationCancelEvent evt)
        {
            if (evt.isPropagationStopped || _escapeScope == null) return;
            if (HandleEscape(_escapeScope)) evt.StopPropagation();
        }

        private static bool IsWithin(VisualElement container, VisualElement target)
        {
            for (VisualElement? current = target; current != null; current = current.parent)
            {
                if (current == container) return true;
            }

            return false;
        }

        private static OdyRect ToRect(UnityEngine.Rect rect) => new OdyRect(rect.x, rect.y, rect.width, rect.height);

        private static OdyRect ToHostLocal(OdyRect world, OdyRect hostWorld) => new OdyRect(world.X - hostWorld.X, world.Y - hostWorld.Y, world.Width, world.Height);
    }

    /// <summary>
    /// ODY-S11-210: a dropdown selector whose option list is an <see cref="OdyPopover"/> anchored below the field
    /// (replaces <see cref="DropdownField"/> in new code). Raises <see cref="ValueChanged"/> itself, so callers and tests
    /// do not depend on UI Toolkit change-event dispatch.
    /// </summary>
    public sealed class OdySelect : VisualElement
    {
        private readonly List<string> _choices;
        private readonly Button _button;
        private OdyPopover? _menu;
        private string _value;

        public OdySelect(string label, IReadOnlyList<string> choices, int index, string name)
        {
            if (choices == null) throw new ArgumentNullException(nameof(choices));
            this.name = name ?? throw new ArgumentNullException(nameof(name));
            _choices = new List<string>(choices);
            _value = _choices.Count == 0 ? string.Empty : _choices[Math.Max(0, Math.Min(index, _choices.Count - 1))];

            AddToClassList(OdyClasses.Field);
            AddToClassList(OdyClasses.Select);
            LabelText = label ?? string.Empty;
            if (LabelText.Length > 0) Add(OdyUi.Text(LabelText, OdyClasses.TextLabel, OdyClasses.SelectLabel));
            _button = new Button(ToggleMenu) { name = name + "-button" };
            _button.AddToClassList(OdyClasses.SelectButton);
            Add(_button);
            UpdateButtonText();
        }

        public event Action<string>? ValueChanged;

        public string LabelText { get; }
        public IReadOnlyList<string> Choices => _choices;
        public string Value => _value;
        public int Index => _choices.IndexOf(_value);
        public bool IsMenuOpen => _menu != null && _menu.IsOpen;
        public OdyPopover? Menu => _menu;

        /// <summary>Selects a choice (as if picked from the list). Unknown values are ignored; returns whether it changed.</summary>
        public bool Choose(string choice)
        {
            CloseMenu();
            if (!_choices.Contains(choice) || string.Equals(choice, _value, StringComparison.Ordinal)) return false;
            _value = choice;
            UpdateButtonText();
            ValueChanged?.Invoke(choice);
            return true;
        }

        public void SetValueWithoutNotify(string choice)
        {
            if (!_choices.Contains(choice)) return;
            _value = choice;
            UpdateButtonText();
        }

        /// <summary>Opens the option list below the field (no-op while disabled or without choices).</summary>
        public OdyPopover? OpenMenu()
        {
            if (!enabledInHierarchy || _choices.Count == 0) return null;
            if (IsMenuOpen) return _menu;
            var list = new ScrollView(ScrollViewMode.Vertical) { name = name + "-options" };
            list.AddToClassList(OdyClasses.SelectOptions);
            for (int i = 0; i < _choices.Count; i++)
            {
                string choice = _choices[i];
                var option = new Button(() => Choose(choice)) { name = name + "-option-" + i, text = choice };
                option.AddToClassList(OdyClasses.SelectOption);
                option.EnableInClassList(OdyClasses.SelectOptionActive, string.Equals(choice, _value, StringComparison.Ordinal));
                list.Add(option);
            }

            float buttonWidth = _button.layout.width;
            var options = new OdyPopoverOptions(OdyPopoverAnchor.ToElement(_button, OdyPopoverOrigin.BottomLeft))
            {
                Pivot = OdyPopoverOrigin.TopLeft,
                Width = float.IsNaN(buttonWidth) || buttonWidth <= 0f ? (float?)null : buttonWidth,
                Name = name + "-menu",
                PaperClasses = new[] { OdyClasses.SelectMenu }
            };
            _menu = OdyPopover.Show(OdyPopover.FindHost(this), list, options);
            return _menu;
        }

        public void CloseMenu()
        {
            if (_menu != null && _menu.IsOpen) _menu.Close();
            _menu = null;
        }

        private void ToggleMenu()
        {
            if (IsMenuOpen) CloseMenu();
            else OpenMenu();
        }

        // Temporary glyph until the art pass (design system section 3).
        private void UpdateButtonText() => _button.text = _value + "  ▾";
    }
}
