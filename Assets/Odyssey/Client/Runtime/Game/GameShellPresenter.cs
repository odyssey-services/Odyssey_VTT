using System;
using System.Collections.Generic;
using Odyssey.Application.Networking.Session;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    public enum GameDrawerSide
    {
        Left = 1,
        Right = 2
    }

    /// <summary>
    /// ODY-S11-201: the Owlbear-style game screen layout. The board fills the whole screen (<c>.ody-board-layer</c>,
    /// bottom of the z-order); everything else floats above it in an overlay layer whose own container ignores picking,
    /// so the map stays usable wherever no panel is drawn:
    /// <list type="bullet">
    /// <item>a thin top bar -- scene title, role indicator/selector, one toggle per side panel;</item>
    /// <item>slide-in drawers on the left/right (one open per side, narrower than the screen) that close from their own
    /// close button, from their toggle, or from a click on the map outside them;</item>
    /// <item>a collapsible corner dock (roll panel + game log, like Owlbear's chat).</item>
    /// </list>
    /// Layout only: the presenters hosted inside (board, roll panel, game log, asset pool, later phases' panels) keep
    /// their own logic. Plain C# presenter, constructor-injected, like every other screen in this module (ADR-005).
    /// </summary>
    public sealed class GameShellPresenter : IDisposable
    {
        public const string LegacyPanelClass = "ody-legacy-panel";

        /// <summary>Drawer geometry (ODY-S11-210): below the top bar, inset from the screen edges.</summary>
        public const float DrawerTopOffset = 68f;
        public const float DrawerEdgeInset = 12f;
        public const float DrawerWidth = 380f;
        public const float DrawerWideWidth = 560f;

        private readonly VisualElement _appRoot;
        private readonly RoleSelection _selection;
        private readonly PresentationRuntime _presentationRuntime;
        private readonly string _sceneTitle;
        private readonly string _screenName;
        private readonly Dictionary<string, Drawer> _drawers = new Dictionary<string, Drawer>(StringComparer.Ordinal);
        private readonly List<string> _drawerOrder = new List<string>();
        private VisualElement? _topbarToggles;
        private VisualElement? _topbarSlot;
        private Label? _roleBadge;
        private VisualElement? _dock;
        private VisualElement? _dockBody;
        private Label? _dockTitle;
        private Button? _dockToggle;
        private IDisposable? _roleSubscription;
        private bool _boardCentered;
        private bool _disposed;

        public GameShellPresenter(VisualElement appRoot, RoleSelection selection, PresentationRuntime presentationRuntime, string sceneTitle, string screenName = "game-screen")
        {
            _screenName = string.IsNullOrWhiteSpace(screenName) ? "game-screen" : screenName;
            _appRoot = appRoot ?? throw new ArgumentNullException(nameof(appRoot));
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _presentationRuntime = presentationRuntime ?? throw new ArgumentNullException(nameof(presentationRuntime));
            _sceneTitle = string.IsNullOrWhiteSpace(sceneTitle) ? "Scene" : sceneTitle;
        }

        /// <summary>Raised after a drawer becomes visible, so its panel can reload fresh server state.</summary>
        public event Action<string>? DrawerOpened;

        public VisualElement Root => _appRoot;

        /// <summary>The full-screen container holding both layers (named by the caller, e.g. <c>trial-screen</c>).</summary>
        public VisualElement Screen { get; private set; } = new VisualElement();
        public VisualElement BoardLayer { get; private set; } = new VisualElement();
        public VisualElement OverlayLayer { get; private set; } = new VisualElement();

        /// <summary>Where modal dialogs mount: the screen container, above both layers.</summary>
        public VisualElement ModalHost => Screen;

        /// <summary>The dock's content container (the former "controls column").</summary>
        public VisualElement? DockContent => _dockBody;

        public bool IsDockCollapsed => _dock != null && _dock.ClassListContains(OdyClasses.DockCollapsed);
        public IReadOnlyList<string> DrawerIds => _drawerOrder;
        public string RoleBadgeText => _roleBadge?.text ?? string.Empty;

        public void Build()
        {
            _appRoot.Clear();
            _appRoot.RemoveFromClassList("app-root");
            _appRoot.AddToClassList(OdyClasses.GameRoot);

            Screen = new VisualElement { name = _screenName };
            Screen.AddToClassList(OdyClasses.GameScreen);
            // Popovers opened from inside a panel (e.g. OdySelect lists) mount here, above both layers.
            Screen.AddToClassList(OdyClasses.PopoverHost);
            _appRoot.Add(Screen);

            BoardLayer = new VisualElement { name = "game-board-layer" };
            BoardLayer.AddToClassList(OdyClasses.BoardLayer);
            Screen.Add(BoardLayer);

            OverlayLayer = new VisualElement { name = "game-overlay-layer", pickingMode = PickingMode.Ignore };
            OverlayLayer.AddToClassList(OdyClasses.OverlayLayer);
            Screen.Add(OverlayLayer);

            BuildTopbar();
            BuildDock();

            _roleSubscription = _selection.Subscribe(OnRoleChanged);
            _presentationRuntime.AddSubscription(_roleSubscription);
            OnRoleChanged(_selection.Current);
        }

        /// <summary>Puts an element (e.g. the role selector) into the top bar, left of the panel toggles.</summary>
        public void AddToTopbar(VisualElement element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            if (_topbarSlot == null) throw new InvalidOperationException("Build() first.");
            _topbarSlot.Add(element);
        }

        /// <summary>Adds a closed drawer with a matching top-bar toggle; returns the container its panel content goes into.</summary>
        public VisualElement AddDrawer(string id, string title, GameDrawerSide side, bool wide = false)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Drawer id is required.", nameof(id));
            if (_drawers.ContainsKey(id)) throw new ArgumentException("Duplicate drawer id.", nameof(id));
            if (_topbarToggles == null) throw new InvalidOperationException("Build() first.");

            var element = new VisualElement { name = "drawer-" + id + "-frame" };
            element.AddToClassList(OdyClasses.DrawerFrame);

            var header = new VisualElement();
            header.AddToClassList(OdyClasses.DrawerHeader);
            header.Add(OdyUi.Text(title, OdyClasses.DrawerTitle));
            // Temporary glyph until the art pass (design system section 3).
            Button close = OdyUi.Button("×", () => CloseDrawer(id), OdyButtonVariant.Ghost, "drawer-" + id + "-close");
            close.AddToClassList(OdyClasses.ButtonIcon);
            header.Add(close);
            element.Add(header);

            var body = new ScrollView(ScrollViewMode.Vertical) { name = "drawer-" + id + "-body" };
            body.AddToClassList(OdyClasses.DrawerBody);
            var content = new VisualElement { name = "drawer-" + id + "-content" };
            content.AddToClassList(OdyClasses.DrawerContent);
            body.Add(content);
            element.Add(body);

            // ODY-S11-210: a persistent OdyPopover anchored to the overlay's top corner on its side, growing inwards and
            // down to the bottom edge. "Click outside a panel closes it": the click-away scope is the board layer, i.e.
            // exactly the map outside every drawer (the top bar and the other side's drawer do not close it). The
            // popover listens trickle-down and never stops the press, so the board still receives it.
            bool left = side == GameDrawerSide.Left;
            var anchor = OdyPopoverAnchor.ToElement(OverlayLayer, left ? OdyPopoverOrigin.TopLeft : OdyPopoverOrigin.TopRight);
            anchor.OffsetX = left ? DrawerEdgeInset : -DrawerEdgeInset;
            anchor.OffsetY = DrawerTopOffset;
            var popover = new OdyPopover(OverlayLayer, element, new OdyPopoverOptions(anchor)
            {
                Pivot = left ? OdyPopoverOrigin.TopLeft : OdyPopoverOrigin.TopRight,
                Width = wide ? DrawerWideWidth : DrawerWidth,
                FillToBottomEdge = true,
                HidePaper = true,
                EdgeMargin = DrawerEdgeInset,
                RemoveOnClose = false,
                ClickAwayScope = BoardLayer,
                Name = "drawer-" + id,
                PaperClasses = wide
                    ? new[] { OdyClasses.Drawer, left ? OdyClasses.DrawerLeft : OdyClasses.DrawerRight, OdyClasses.DrawerWide }
                    : new[] { OdyClasses.Drawer, left ? OdyClasses.DrawerLeft : OdyClasses.DrawerRight }
            });
            popover.Mount();

            Button toggle = OdyUi.Button(title, () => ToggleDrawer(id), OdyButtonVariant.Secondary, "toggle-" + id, small: true);
            _topbarToggles.Add(toggle);
            popover.Closed += _ => toggle.EnableInClassList(OdyClasses.ButtonToggleOn, false);

            _drawers[id] = new Drawer(id, side, popover, content, toggle);
            _drawerOrder.Add(id);
            return content;
        }

        public bool IsDrawerOpen(string id) => _drawers.TryGetValue(id, out Drawer? drawer) && drawer.Popover.IsOpen;

        /// <summary>The drawer's popover (placement, size), or <c>null</c> for an unknown id.</summary>
        public OdyPopover? DrawerPopover(string id) => _drawers.TryGetValue(id, out Drawer? drawer) ? drawer.Popover : null;

        public bool OpenDrawer(string id)
        {
            if (!_drawers.TryGetValue(id, out Drawer? drawer)) return false;
            // One drawer per side: the map stays mostly visible.
            foreach (Drawer other in _drawers.Values)
            {
                if (other.Side == drawer.Side && !ReferenceEquals(other, drawer)) SetOpen(other, false);
            }

            SetOpen(drawer, true);
            DrawerOpened?.Invoke(id);
            return true;
        }

        public bool CloseDrawer(string id)
        {
            if (!_drawers.TryGetValue(id, out Drawer? drawer)) return false;
            SetOpen(drawer, false);
            return true;
        }

        public bool ToggleDrawer(string id) => IsDrawerOpen(id) ? CloseDrawer(id) : OpenDrawer(id);

        public void CloseAllDrawers()
        {
            foreach (Drawer drawer in _drawers.Values) SetOpen(drawer, false);
        }

        /// <summary>The map-click rule, public for tests (a real press arrives through each open drawer's click-away scope).</summary>
        public void HandleMapPointerDown()
        {
            foreach (Drawer drawer in _drawers.Values) drawer.Popover.HandleClickAway(null);
        }

        /// <summary>Fills the corner dock (collapsible; open by default).</summary>
        public void SetDockContent(string title, params VisualElement[] content)
        {
            if (_dockBody == null || _dockTitle == null) throw new InvalidOperationException("Build() first.");
            _dockTitle.text = title ?? string.Empty;
            _dockBody.Clear();
            foreach (VisualElement element in content) _dockBody.Add(element);
        }

        public void SetDockCollapsed(bool collapsed)
        {
            if (_dock == null) return;
            _dock.EnableInClassList(OdyClasses.DockCollapsed, collapsed);
            // Temporary glyphs until the art pass.
            if (_dockToggle != null) _dockToggle.text = collapsed ? "+" : "−";
        }

        public void ToggleDock() => SetDockCollapsed(!IsDockCollapsed);

        /// <summary>
        /// Once the full-screen board has a real size, pans it once so the world origin sits at the screen center
        /// instead of the old 440px board's center. Uses only the board's existing public pan API.
        /// </summary>
        public void CenterBoardOnceLaidOut(BoardScreenPresenter board)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            BoardLayer.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (_boardCentered || _disposed) return;
                float width = evt.newRect.width;
                float height = evt.newRect.height;
                if (float.IsNaN(width) || float.IsNaN(height) || width <= 0f || height <= 0f) return;
                _boardCentered = true;
                double deltaX = width / 2.0 - 220.0;
                double deltaY = height / 2.0 - 220.0;
                board.BeginBoardPan(0.0, 0.0);
                board.MoveBoardPan(deltaX, deltaY);
                board.EndBoardPan();
                board.Refresh();
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            CloseAllDrawers();
            _roleSubscription?.Dispose();
            _disposed = true;
        }

        private void BuildTopbar()
        {
            var topbar = new VisualElement { name = "game-topbar" };
            topbar.AddToClassList(OdyClasses.Topbar);
            topbar.Add(OdyUi.Text(_sceneTitle, OdyClasses.TopbarTitle));
            _roleBadge = OdyUi.Badge(string.Empty, OdyStatusKind.Info, "game-role-badge");
            topbar.Add(_roleBadge);
            _topbarSlot = new VisualElement { name = "game-topbar-slot" };
            _topbarSlot.AddToClassList(OdyClasses.Row);
            topbar.Add(_topbarSlot);
            var spacer = new VisualElement();
            spacer.AddToClassList(OdyClasses.Spacer);
            topbar.Add(spacer);
            _topbarToggles = new VisualElement { name = "game-topbar-toggles" };
            _topbarToggles.AddToClassList(OdyClasses.TopbarToggles);
            topbar.Add(_topbarToggles);
            OverlayLayer.Add(topbar);
        }

        private void BuildDock()
        {
            _dock = new VisualElement { name = "game-dock" };
            _dock.AddToClassList(OdyClasses.Dock);
            var header = new VisualElement();
            header.AddToClassList(OdyClasses.DockHeader);
            _dockTitle = OdyUi.Text(string.Empty, OdyClasses.DockTitle);
            header.Add(_dockTitle);
            _dockToggle = OdyUi.Button("−", ToggleDock, OdyButtonVariant.Ghost, "game-dock-toggle");
            _dockToggle.AddToClassList(OdyClasses.ButtonIcon);
            header.Add(_dockToggle);
            _dock.Add(header);
            var body = new ScrollView(ScrollViewMode.Vertical) { name = "game-dock-body" };
            body.AddToClassList(OdyClasses.DockBody);
            _dockBody = body.contentContainer;
            _dockBody.style.paddingLeft = 12;
            _dockBody.style.paddingRight = 12;
            _dockBody.style.paddingTop = 8;
            _dock.Add(body);
            OverlayLayer.Add(_dock);
        }

        private void OnRoleChanged(RoleSelectionSnapshot snapshot)
        {
            if (_roleBadge == null) return;
            switch (snapshot.Role)
            {
                case BaselineRole.MainGM:
                    _roleBadge.text = "MainGM";
                    OdyUi.SetBadgeKind(_roleBadge, OdyStatusKind.Accent);
                    break;
                case BaselineRole.Player:
                    _roleBadge.text = "Player";
                    OdyUi.SetBadgeKind(_roleBadge, OdyStatusKind.Info);
                    break;
                default:
                    _roleBadge.text = "Observer";
                    OdyUi.SetBadgeKind(_roleBadge, OdyStatusKind.Neutral);
                    break;
            }
        }

        private static void SetOpen(Drawer drawer, bool open)
        {
            if (open) drawer.Popover.Open();
            else drawer.Popover.Close();
            drawer.Toggle.EnableInClassList(OdyClasses.ButtonToggleOn, open);
        }

        private sealed class Drawer
        {
            public Drawer(string id, GameDrawerSide side, OdyPopover popover, VisualElement content, Button toggle)
            {
                Id = id;
                Side = side;
                Popover = popover;
                Content = content;
                Toggle = toggle;
            }

            public string Id { get; }
            public GameDrawerSide Side { get; }
            public OdyPopover Popover { get; }
            public VisualElement Content { get; }
            public Button Toggle { get; }
        }
    }
}
