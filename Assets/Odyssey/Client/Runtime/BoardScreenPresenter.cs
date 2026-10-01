using System;
using System.Collections.Generic;
using System.Linq;
using Odyssey.Application.Board;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Geometry;
using Odyssey.Domain.Identity;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    /// <summary>
    /// ODY-UI-01-002: the minimal trial board screen -- renders the active
    /// scene's tokens at their real, persisted <see cref="TokenPosition"/>
    /// coordinates, and lets a click-to-select-then-click-destination
    /// gesture call <see cref="BoardMovementService.MoveToken"/> directly
    /// (SLICE-UI-01_BACKLOG.md section 3.2's direct-call convention -- no
    /// adapter layer, no DI container). A plain C# class, not a
    /// <c>MonoBehaviour</c>, constructor-injected with its dependencies and
    /// built entirely from code-created <see cref="VisualElement"/>s over an
    /// already-configured <see cref="UIDocument"/> -- the exact same shape
    /// <see cref="DeveloperShellPresenter"/> already established as this
    /// repository's only prior UI screen, not a new pattern invented here.
    ///
    /// Rendering technique decision (task contract section 3): plain
    /// absolutely-positioned <see cref="VisualElement"/>s inside the
    /// existing UI Toolkit document, not a separate GameObject/SpriteRenderer
    /// scene hierarchy. ADR-001 section 6.7 already names "UI Toolkit views"
    /// as this module's expected pattern; a second rendering technology
    /// would need its own camera/world-space setup and coordinate-space
    /// conversion for a screen this task's own scope keeps deliberately
    /// minimal (SLICE-UI-01_BACKLOG.md section 3.4 excludes drag-and-drop
    /// polish, animation, and hex-grid rendering) -- introducing a second
    /// technology here would cost more than it buys.
    ///
    /// <see cref="LocalActorUserId"/>/<see cref="LocalActorIsMainGm"/> are
    /// mutable, public, caller-settable properties; the ODY-UI-01-003 role
    /// selector can now keep them synchronized from one shared selection,
    /// matching
    /// <c>ODY-S03-004</c>/<c>005</c>'s already-established convention that
    /// actor identity/role are caller-supplied, not resolved from a real
    /// session.
    /// </summary>
    public sealed class BoardScreenPresenter : IDisposable
    {
        private const double TokenSizePixels = 28.0;

        private readonly UIDocument _document;
        private readonly ISceneRepository _sceneRepository;
        private readonly CampaignHandle _campaign;
        private readonly ICampaignRepository _campaignRepository;
        // SLICE-10 Block 6 part 1: obstacle drawing/toggling/damage all go through this port, injected the
        // same way _campaignRepository already is -- a storage/authorization dependency, not something this
        // presenter constructs internally.
        private readonly IObstacleRepository _obstacleRepository;
        // SLICE-10 Block 6 part 2: fog-of-war UI needs the same two ports PlayerVisibilityService already
        // composes over in its own tests (ComputeVisibleTokens/ListExploredReveals/RecordExploration) --
        // injected the same way _obstacleRepository already is, not constructed internally.
        private readonly ITokenVisionRepository _visionRepository;
        private readonly IFogOfWarRepository _fogRepository;
        private readonly BoardFogOfWarPresenter _fogPresenter = new BoardFogOfWarPresenter();
        private const double BoardWidthPixels = 440.0;
        private const double BoardHeightPixels = 440.0;
        private readonly SceneId _sceneId;
        private readonly bool _includeRoleSelector;
        // ODY-S08-102: replaces the old fixed OriginOffsetPixels/PixelsPerUnit transform. Purely local,
        // per-presenter, ephemeral state (task contract section 1.4: no persistence, no sync).
        private readonly BoardCamera _camera = new BoardCamera();
        // ODY-S08-107: _boardPointerGesture now drives only the middle-button camera pan (it still reports the
        // movement deltas a pan needs); the left button on empty board is the box-select/click gesture below.
        private readonly BoardPointerGesture _boardPointerGesture = new BoardPointerGesture();
        private readonly BoardBoxSelectGesture _boxGesture = new BoardBoxSelectGesture();
        private bool _boxAdditive;
        private VisualElement? _boxElement;

        // SLICE-10 Block 6 part 1: the active tool (Select is the default and preserves every pre-existing
        // gesture unchanged). Switching tools is refused while a draw gesture is already in progress -- see
        // SetTool's own remarks -- so a half-drawn segment is always either committed or explicitly cancelled
        // (Escape on the board area, or CancelObstacleDraw for tests), never silently abandoned.
        private BoardTool _currentTool = BoardTool.Select;
        private BoardToolbarPresenter? _toolbarPresenter;
        private readonly BoardObstacleDrawGesture _obstacleDrawGesture = new BoardObstacleDrawGesture();
        // Which button-0 gesture the current press started (draw vs. the pre-existing box-select/click) --
        // decided once at press time from _currentTool, not re-read on every move/up, so a (disallowed, but
        // defensively handled) tool change mid-gesture cannot flip which gesture a move/up call feeds.
        private bool _obstacleDrawActive;
        private const double ObstacleLineThicknessPixels = 4.0;
        private const double ObstacleHitTestThresholdPixels = 8.0;
        private VisualElement? _obstacleDrawPreviewElement;
        private readonly Dictionary<string, ObstacleRecord> _obstacleRecordsByObstacleId = new Dictionary<string, ObstacleRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, ObstacleDurabilityRecord> _obstacleDurabilityByObstacleId = new Dictionary<string, ObstacleDurabilityRecord>(StringComparer.Ordinal);
        private ObstacleId? _selectedObstacleId;
        private VisualElement? _obstacleInspectorElement;
        private IntegerField? _obstacleDamageAmountField;

        // SLICE-10 Block 6 part 3: token facing/FOV/view-distance inspector + cover preview.
        private BoardTokenInspectorPresenter? _tokenInspectorPresenter;
        // The token the inspector's fields were last populated from -- SetValues is only called again when
        // this changes, so an in-progress, not-yet-applied edit is never clobbered by an unrelated Refresh()
        // (e.g. another participant moving elsewhere, or this board's own fog recompute). See
        // BoardTokenInspectorPresenter.SetValues's own remarks.
        private TokenId? _lastInspectedTokenId;
        private const double VisionIndicatorThicknessPixels = 2.0;

        // Which mouse button owns the board gesture in progress (-1: none). The mouse reports one pointer id
        // for every button, so this -- not the pointer id -- is what keeps a second button from starting a
        // second gesture, and keeps its release from ending the first.
        private int _activeBoardButton = -1;

        // ODY-S08-107: the right-button "player trail" marker -- local visual state only, in world coordinates.
        private const double PlayerMarkerSizePixels = 22.0;
        private const long PlayerMarkerLifetimeMilliseconds = 2500;
        private VisualElement? _markerElement;
        private IVisualElementScheduledItem? _markerExpiry;
        private TokenPosition? _markerWorldPosition;
        private readonly Dictionary<string, VisualElement> _tokenElementsByTokenId = new Dictionary<string, VisualElement>(StringComparer.Ordinal);
        // ODY-S08-102: the last-rendered world position of each token, so a pan/zoom can reposition
        // already-rendered token elements without a full Refresh() (no repeated DB read/texture-cache
        // lookups while the user is actively dragging or scrolling).
        private readonly Dictionary<string, TokenPosition> _tokenPositionsByTokenId = new Dictionary<string, TokenPosition>(StringComparer.Ordinal);
        // ODY-S08-104: one BoardPointerGesture per currently-rendered token (by exact precedent of the
        // board's own _boardPointerGesture, ODY-S08-102) -- disambiguates a click-to-select from the start
        // of a token drag. Recreated on every RenderTokens pass, same lifetime as the token elements
        // themselves; a drag always completes (commit or cancel) before the next Refresh() can run, so no
        // in-flight gesture is ever discarded.
        private readonly Dictionary<string, BoardPointerGesture> _tokenGesturesByTokenId = new Dictionary<string, BoardPointerGesture>(StringComparer.Ordinal);
        // The token currently being dragged, if any -- lets an unsolicited PointerCaptureOutEvent (task
        // contract section 1.5: capture lost some other way than our own PointerUp) tell whether it needs
        // to roll the visual position back, versus the ordinary PointerCaptureOutEvent that follows our own
        // ReleasePointer call at the end of a normal drag (by then this is already cleared).
        private TokenId? _draggingTokenId;

        // ODY-S08-105: last-rendered z-order and scale per token -- the in-memory mirror RenderTokens fills, so
        // the pointer-down "is this already on top?" check and the hit test need no repository read.
        private readonly Dictionary<string, long> _tokenZOrdersByTokenId = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> _tokenScalesByTokenId = new Dictionary<string, double>(StringComparer.Ordinal);
        // SLICE-10 Block 6 part 3: needed to gate facing editability (owner-or-MainGM) in the token
        // inspector without a repository read -- the same in-memory-mirror convention as the two dictionaries above.
        private readonly Dictionary<string, UserId> _tokenControllersByTokenId = new Dictionary<string, UserId>(StringComparer.Ordinal);
        // ODY-S08-101/ODY-S08-103: decoded textures for the presenter's lifetime, keyed by AssetId.
        // Extracted into AssetTextureCache in ODY-S08-103 so AssetPoolPresenter can reuse the exact same
        // read+decode+cache logic instead of a second, independent implementation.
        private readonly AssetTextureCache _textureCache = new AssetTextureCache();
        private bool _boardBackgroundApplied;
        private readonly RoleSelection? _roleSelection;
        private readonly PresentationRuntime? _presentationRuntime;
        private IDisposable? _roleSubscription;
        private RoleSelectorPresenter? _roleSelectorPresenter;
        private VisualElement? _boardArea;
        private Label? _statusLabel;
        // ODY-S08-106: the selection is a set (session-only, never persisted), keyed by the token id's string
        // form like every other per-token dictionary in this class.
        private readonly HashSet<string> _selectedTokenIds = new HashSet<string>(StringComparer.Ordinal);

        // ODY-S08-106: state of the token gesture in progress (set by BeginTokenDrag, cleared when it ends).
        private bool _dragShift;
        private bool _dragStarted;
        private string? _dragAnchorKey;
        private readonly List<string> _dragGroup = new List<string>();
        private readonly Dictionary<string, TokenPosition> _dragStartPositions = new Dictionary<string, TokenPosition>(StringComparer.Ordinal);
        // ODY-S11-211: local moves are drawn instantly, moves observed from elsewhere ease in (BoardTokenMotion).
        private readonly BoardTokenMotion _tokenMotion = new BoardTokenMotion();
        private IVisualElementScheduledItem? _tokenMotionTicker;
        // ODY-S11-214: camera autofocus on the acting participant; manual camera input always wins.
        private readonly BoardCameraFocus _cameraFocus = new BoardCameraFocus();
        private IVisualElementScheduledItem? _cameraFocusTicker;
        private readonly Dictionary<string, string> _tokenKeysByCharacterId = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _visibleTokenKeys = new HashSet<string>(StringComparer.Ordinal);
        private bool _disposed;

        public BoardScreenPresenter(UIDocument document, ISceneRepository sceneRepository, CampaignHandle campaign, ICampaignRepository campaignRepository, IObstacleRepository obstacleRepository, ITokenVisionRepository visionRepository, IFogOfWarRepository fogRepository, SceneId sceneId, UserId localActorUserId)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _sceneRepository = sceneRepository ?? throw new ArgumentNullException(nameof(sceneRepository));
            _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            // ODY-S10-101: a token move by someone other than the token's controller is authorized against the
            // stored campaign membership, so the presenter needs the repository that holds it.
            _campaignRepository = campaignRepository ?? throw new ArgumentNullException(nameof(campaignRepository));
            _obstacleRepository = obstacleRepository ?? throw new ArgumentNullException(nameof(obstacleRepository));
            _visionRepository = visionRepository ?? throw new ArgumentNullException(nameof(visionRepository));
            _fogRepository = fogRepository ?? throw new ArgumentNullException(nameof(fogRepository));
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));
            if (!localActorUserId.IsValid) throw new ArgumentException("LocalActorUserId is required.", nameof(localActorUserId));
            _sceneId = sceneId;
            _includeRoleSelector = true;
            LocalActorUserId = localActorUserId;
        }

        public BoardScreenPresenter(UIDocument document, ISceneRepository sceneRepository, CampaignHandle campaign, ICampaignRepository campaignRepository, IObstacleRepository obstacleRepository, ITokenVisionRepository visionRepository, IFogOfWarRepository fogRepository, SceneId sceneId, RoleSelection roleSelection, PresentationRuntime presentationRuntime, bool includeRoleSelector = true)
            : this(document, sceneRepository, campaign, campaignRepository, obstacleRepository, visionRepository, fogRepository, sceneId, (roleSelection ?? throw new ArgumentNullException(nameof(roleSelection))).ActorUserId)
        {
            _roleSelection = roleSelection;
            _presentationRuntime = presentationRuntime ?? throw new ArgumentNullException(nameof(presentationRuntime));
            _includeRoleSelector = includeRoleSelector;
            ApplyRoleSelection(roleSelection.Current, refresh: false);
            _roleSubscription = roleSelection.Subscribe(snapshot => ApplyRoleSelection(snapshot, refresh: true));
            _presentationRuntime.AddSubscription(_roleSubscription);
        }

        /// <summary>The single local actor this trial UI currently acts as. Settable -- see class remarks.</summary>
        public UserId LocalActorUserId { get; set; }

        /// <summary>Whether the current local actor holds the MainGM baseline role, as the role selector reports it. Settable -- see class remarks. Since ODY-S10-101 this is presentation state only: token-move authorization no longer reads it (it uses the stored campaign membership).</summary>
        public bool LocalActorIsMainGm { get; set; }

        /// <summary>
        /// ODY-S11-201 (Owlbear layout): when set before <see cref="InitializeInto"/>, the board area is not given its
        /// fixed 440x440 inline size -- it fills its parent (the full-screen board layer; the design system's
        /// <c>.ody-board-layer #board-area</c> rule) and the fog overlay follows the area's real laid-out size. Only the
        /// embedding changes: camera, gestures, hit tests, token/obstacle/fog rendering and every command are the
        /// same code paths. Default false keeps the pre-existing fixed-size board byte-for-byte (all existing tests).
        /// </summary>
        public bool FullBleed { get; set; }

        /// <summary>
        /// ODY-S11-208: when set before <see cref="InitializeInto"/> (Owlbear/<see cref="FullBleed"/> mode), the
        /// title/toolbar/status chrome is mounted here instead of <c>appRoot</c>. <c>appRoot</c> also hosts
        /// <c>_boardArea</c>, and when <see cref="FullBleed"/> is true that area is absolutely positioned to cover
        /// the whole parent and is added after the chrome -- later siblings paint and receive pointer events on
        /// top in UI Toolkit, so the full-bleed board silently swallowed every click meant for the toolbar's
        /// Select/Draw Wall/Draw Door/Draw Window buttons (TC-BOARD-121 finding, ODY-S11-207/208). Mounting the
        /// chrome on a host that is a structural sibling of the board layer, not a document-order predecessor
        /// inside it, fixes this regardless of stylesheet loading. Null (default) keeps the pre-existing
        /// appRoot-hosted chrome byte-for-byte (dev shell, fixed-size board, existing EditMode/PlayMode tests).
        /// </summary>
        public VisualElement? OverlayHost { get; set; }

        public Result Initialize()
        {
            return InitializeInto(null);
        }

        public Result InitializeInto(VisualElement? parent)
        {
            try
            {
                BuildView(parent);
                return Refresh();
            }
            catch (Exception)
            {
                return Result.Failure(BoardScreenErrors.RenderFailed());
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _roleSelectorPresenter?.Dispose();
            _roleSubscription?.Dispose();
            _tokenMotionTicker?.Pause();
            CancelCameraFocus();
            _textureCache.Dispose();
            _disposed = true;
        }

        private void BuildView(VisualElement? parent)
        {
            VisualElement root = _document.rootVisualElement;
            VisualElement appRoot = parent ?? root.Q<VisualElement>("odyssey-root") ?? root;
            appRoot.Clear();
            appRoot.AddToClassList("app-root");

            // ODY-S11-208: in Owlbear/FullBleed mode the chrome is mounted on OverlayHost, a structural sibling
            // of appRoot's board layer, not a document-order predecessor inside it -- see OverlayHost's remarks.
            VisualElement chromeHost = OverlayHost ?? appRoot;

            if (OverlayHost == null)
            {
                Label title = new Label("Odyssey Board Screen (trial)") { name = "board-title" };
                chromeHost.Add(title);
            }

            if (_includeRoleSelector && _roleSelection != null && _presentationRuntime != null)
            {
                _roleSelectorPresenter = new RoleSelectorPresenter(_roleSelection, _presentationRuntime);
                appRoot.Add(_roleSelectorPresenter.BuildView());
            }

            // SLICE-10 Block 6 part 1: the toolbar's own visibility (MainGM-only, presentational) is set on
            // every Refresh(), so it stays correct regardless of how LocalActorIsMainGm was last changed.
            _toolbarPresenter = new BoardToolbarPresenter(OnToolSelected);
            VisualElement toolbarView = _toolbarPresenter.BuildView();
            chromeHost.Add(toolbarView);
            _toolbarPresenter.SetActiveTool(_currentTool);

            _statusLabel = new Label { name = "board-status" };
            chromeHost.Add(_statusLabel);

            if (OverlayHost != null)
            {
                // Floats the toolbar and status pill over the map's bottom-left corner -- a thin strip, not a
                // block that competes with the map for space -- independent of the design-system stylesheet
                // (not currently attached to this UIDocument; see ODY-S11-208 completion evidence).
                toolbarView.style.position = Position.Absolute;
                toolbarView.style.left = 12;
                toolbarView.style.bottom = 56;
                toolbarView.style.paddingLeft = 4;
                toolbarView.style.paddingRight = 4;
                toolbarView.style.paddingTop = 4;
                toolbarView.style.paddingBottom = 4;
                toolbarView.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.14f, 0.92f));
                toolbarView.style.borderTopLeftRadius = 8;
                toolbarView.style.borderTopRightRadius = 8;
                toolbarView.style.borderBottomLeftRadius = 8;
                toolbarView.style.borderBottomRightRadius = 8;

                _statusLabel.style.position = Position.Absolute;
                _statusLabel.style.left = 12;
                _statusLabel.style.bottom = 12;
                _statusLabel.style.maxWidth = Length.Percent(50);
                _statusLabel.style.paddingLeft = 8;
                _statusLabel.style.paddingRight = 8;
                _statusLabel.style.paddingTop = 4;
                _statusLabel.style.paddingBottom = 4;
                _statusLabel.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.14f, 0.92f));
                _statusLabel.style.color = new StyleColor(Color.white);
                _statusLabel.style.borderTopLeftRadius = 12;
                _statusLabel.style.borderTopRightRadius = 12;
                _statusLabel.style.borderBottomLeftRadius = 12;
                _statusLabel.style.borderBottomRightRadius = 12;
                _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            }

            _boardArea = new VisualElement { name = "board-area" };
            if (FullBleed)
            {
                _boardArea.style.position = Position.Absolute;
                _boardArea.style.left = 0;
                _boardArea.style.top = 0;
                _boardArea.style.right = 0;
                _boardArea.style.bottom = 0;
                _boardArea.style.overflow = Overflow.Hidden;
                _boardArea.RegisterCallback<GeometryChangedEvent>(OnBoardAreaGeometryChanged);
            }
            else
            {
                _boardArea.style.position = Position.Relative;
                _boardArea.style.width = (float)BoardWidthPixels;
                _boardArea.style.height = (float)BoardHeightPixels;
                _boardArea.style.marginTop = 8;
            }

            _boardArea.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.14f));
            // Focusable so Escape (OnBoardKeyDown) can reach it and cancel an in-progress draw gesture --
            // requires the board area to have received focus first (e.g. from a prior click on it), a known,
            // accepted MVP limitation documented in the task report.
            _boardArea.focusable = true;
            // ODY-S08-102: pointer-down/move/up (not ClickEvent) drives the board's own click-vs-pan
            // disambiguation (BoardPointerGesture) -- see the class remarks and OnBoardPointerDown/Move/Up.
            _boardArea.RegisterCallback<PointerDownEvent>(OnBoardPointerDown);
            _boardArea.RegisterCallback<PointerMoveEvent>(OnBoardPointerMove);
            _boardArea.RegisterCallback<PointerUpEvent>(OnBoardPointerUp);
            _boardArea.RegisterCallback<PointerCaptureOutEvent>(OnBoardPointerCaptureOut);
            _boardArea.RegisterCallback<WheelEvent>(OnBoardWheel);
            _boardArea.RegisterCallback<KeyDownEvent>(OnBoardKeyDown);
            appRoot.Add(_boardArea);

            BuildObstacleInspector();

            _tokenInspectorPresenter = new BoardTokenInspectorPresenter(OnApplyTokenFacingButton, OnApplyTokenVisionParametersButton, OnCheckCoverButton);
        }

        public Result Refresh()
        {
            Result<IReadOnlyList<TokenRecord>> tokens = _sceneRepository.ListTokens(_campaign, _sceneId, NewCorrelationId());
            if (tokens.IsFailure)
            {
                SetStatus("Failed to list tokens: " + tokens.Error.SafeReasonCode);
                return Result.Failure(tokens.Error);
            }

            // SLICE-10 Block 6 part 1: a scene-geometry read failure (rare -- IO only, ListObstacles performs
            // no authorization) degrades to "no obstacles rendered this pass" rather than failing the whole
            // Refresh() -- tokens are the more critical render, exactly as an asset-load failure already only
            // affects its own visual, never the rest of the board.
            Result<IReadOnlyList<ObstacleRecord>> obstacles = _obstacleRepository.ListObstacles(_campaign, _sceneId, NewCorrelationId());

            Error? backgroundError = ApplySceneBackground();

            // SLICE-10 Block 6 part 2: null means "MainGm, no filtering" -- every token renders, exactly as
            // before this task. A non-null set is the real, authoritative ComputeVisibleTokens result; a
            // player's own controlled token not being in it is trusted and rendered as absent too (task
            // contract section 2.3: never client-side override the server's answer).
            IReadOnlyCollection<TokenId>? visibleTokenIds = null;
            if (!LocalActorIsMainGm)
            {
                var visibilityRequest = new ComputeVisibleTokensRequest(_campaign, _sceneId, LocalActorUserId, LocalActorUserId, NewCorrelationId());
                Result<IReadOnlyCollection<TokenId>> visibility = PlayerVisibilityService.ComputeVisibleTokens(_sceneRepository, _visionRepository, _obstacleRepository, _campaignRepository, visibilityRequest);
                visibleTokenIds = visibility.IsSuccess ? visibility.Value : Array.Empty<TokenId>();
            }

            // ODY-S10-115: obstacles are filtered by the player's own map memory the same way tokens
            // already are by ComputeVisibleTokens above -- IObstacleRepository.ListObstacles itself stays
            // the unfiltered source of truth (LineOfSight/SuggestCover need the true, complete set
            // regardless of any one player's own exploration), so this is a second, additive read for
            // this presenter's own render pass only, not a replacement of the read above. MainGm gets the
            // unfiltered list, unconditionally, exactly as MainGm already does for tokens/fog.
            IReadOnlyList<ObstacleRecord> obstaclesToRender = obstacles.IsSuccess ? obstacles.Value : Array.Empty<ObstacleRecord>();
            if (!LocalActorIsMainGm)
            {
                var obstacleVisibilityRequest = new ListExploredObstaclesRequest(_campaign, _sceneId, LocalActorUserId, LocalActorUserId, NewCorrelationId());
                Result<IReadOnlyList<ObstacleRecord>> exploredObstacles = PlayerVisibilityService.ListExploredObstacles(_obstacleRepository, _fogRepository, _campaignRepository, obstacleVisibilityRequest);
                obstaclesToRender = exploredObstacles.IsSuccess ? exploredObstacles.Value : Array.Empty<ObstacleRecord>();
            }

            _boardArea?.Clear();
            RenderObstacles(obstaclesToRender);
            RenderFogOfWar();
            Error? tokenAssetError = RenderTokens(tokens.Value, visibleTokenIds);
            RestoreOverlays();
            _toolbarPresenter?.SetVisible(LocalActorIsMainGm);

            // Image assets are presentation: a missing/corrupt/undecodable one never stops the
            // board from rendering (fallback visuals are drawn), but the first failure is reported.
            Error? assetError = backgroundError ?? tokenAssetError;
            if (assetError != null)
            {
                SetStatus("Asset load failed: " + assetError.SafeReasonCode);
                return Result.Failure(assetError);
            }

            return Result.Success();
        }

        // Re-adds whichever overlay elements are currently live -- RenderObstacles/RenderTokens' shared
        // _boardArea.Clear() (moved here from RenderTokens itself, task contract section 2.3: obstacles and
        // tokens now share one render pass) removes every child indiscriminately, tokens included.
        private void RestoreOverlays()
        {
            if (_boardArea == null) return;
            if (_boxElement != null && _boxGesture.IsDragging) _boardArea.Add(_boxElement);
            if (_markerElement != null && _markerWorldPosition.HasValue) _boardArea.Add(_markerElement);
            if (_obstacleDrawPreviewElement != null && _obstacleDrawGesture.IsActive) _boardArea.Add(_obstacleDrawPreviewElement);
            ShowObstacleInspectorIfSelected();
            ShowTokenVisionInspectorIfSelected();
        }

        // SLICE-10 Block 6 part 2: permanent map memory. Recomputed from the real, authorized
        // ListExploredReveals result on every Refresh() (never cached in screen space), so a camera
        // pan/zoom is picked up the same way RenderObstacles/RenderTokens already are. Added to the board
        // area right after RenderObstacles and before RenderTokens, so a currently-visible token (added
        // next) always renders on top of the darkness, never obscured by it -- fog only ever hides map
        // geometry (background/obstacles), never a token that ComputeVisibleTokens has already approved.
        private void RenderFogOfWar()
        {
            if (_boardArea == null) return;
            if (LocalActorIsMainGm)
            {
                _fogPresenter.Hide();
                return;
            }

            var request = new ListExploredRevealsRequest(_campaign, _sceneId, LocalActorUserId, LocalActorUserId, NewCorrelationId());
            Result<IReadOnlyList<FogRevealRecord>> reveals = PlayerVisibilityService.ListExploredReveals(_fogRepository, _campaignRepository, request);
            _fogPresenter.Show(reveals.IsSuccess ? reveals.Value : Array.Empty<FogRevealRecord>(), _camera, CurrentBoardWidthPixels(), CurrentBoardHeightPixels());
            _boardArea.Add(_fogPresenter.Element);
        }

        // ODY-S11-201: the fixed constants unless FullBleed and the area already has a real layout (EditMode tests
        // without a panel never lay out, so they keep the constants).
        private double CurrentBoardWidthPixels()
        {
            if (!FullBleed || _boardArea == null) return BoardWidthPixels;
            float width = _boardArea.layout.width;
            return float.IsNaN(width) || width <= 0f ? BoardWidthPixels : width;
        }

        private double CurrentBoardHeightPixels()
        {
            if (!FullBleed || _boardArea == null) return BoardHeightPixels;
            float height = _boardArea.layout.height;
            return float.IsNaN(height) || height <= 0f ? BoardHeightPixels : height;
        }

        // ODY-S11-201: a window resize re-sizes the already-computed fog darkness; no repository read.
        private void OnBoardAreaGeometryChanged(GeometryChangedEvent evt)
        {
            if (!_fogPresenter.IsVisible) return;
            _fogPresenter.Show(_fogPresenter.CurrentReveals, _camera, CurrentBoardWidthPixels(), CurrentBoardHeightPixels());
        }

        private Error? ApplySceneBackground()
        {
            if (_boardArea == null) return null;

            Result<SceneRecord> scene = _sceneRepository.GetScene(_campaign, _sceneId, NewCorrelationId());
            if (scene.IsFailure)
            {
                ClearBoardBackground();
                // An unknown scene has always rendered as an empty board; keep that unchanged.
                return scene.Error.Code.Equals(ErrorCodes.PersistenceSceneNotFound) ? null : scene.Error;
            }

            if (!scene.Value.BackgroundAssetId.HasValue)
            {
                ClearBoardBackground();
                return null;
            }

            Result<Texture2D> texture = LoadTexture(scene.Value.BackgroundAssetId.Value);
            if (texture.IsFailure)
            {
                ClearBoardBackground();
                return texture.Error;
            }

            ApplyTexture(_boardArea, texture.Value);
            _boardBackgroundApplied = true;
            return null;
        }

        // Only undo what this presenter itself applied, so a board that never had a
        // background gets no inline image style written at all (identical to before).
        private void ClearBoardBackground()
        {
            if (_boardArea == null || !_boardBackgroundApplied) return;
            ClearBackground(_boardArea);
            _boardBackgroundApplied = false;
        }

        private Result<Texture2D> LoadTexture(AssetId assetId) => _textureCache.Load(_sceneRepository, _campaign, assetId, NewCorrelationId());

        private static void ApplyTexture(VisualElement element, Texture2D texture)
        {
            element.style.backgroundImage = new StyleBackground(texture);
            element.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
        }

        private static void ClearBackground(VisualElement element)
        {
            element.style.backgroundImage = StyleKeyword.Null;
            element.style.backgroundSize = StyleKeyword.Null;
        }


        private void ApplyRoleSelection(RoleSelectionSnapshot snapshot, bool refresh)
        {
            LocalActorUserId = snapshot.ActorUserId;
            LocalActorIsMainGm = snapshot.ActorIsMainGm;
            if (refresh) Refresh();
        }

        private Error? RenderTokens(IReadOnlyList<TokenRecord> tokens, IReadOnlyCollection<TokenId>? visibleTokenIds)
        {
            if (_boardArea == null) return null;
            // ODY-S11-211: where each token was last rendered, to tell a moved token from an unchanged one.
            var previousPositions = new Dictionary<string, TokenPosition>(_tokenPositionsByTokenId, StringComparer.Ordinal);
            _tokenElementsByTokenId.Clear();
            _tokenPositionsByTokenId.Clear();
            _tokenKeysByCharacterId.Clear();
            _visibleTokenKeys.Clear();
            _tokenGesturesByTokenId.Clear();
            _tokenZOrdersByTokenId.Clear();
            _tokenScalesByTokenId.Clear();
            _tokenControllersByTokenId.Clear();
            Error? firstAssetError = null;

            // ODY-S08-105: UI Toolkit draws siblings in tree order, so ascending ZOrder (stable for ties)
            // puts the highest ZOrder last, i.e. on top.
            foreach (TokenRecord token in tokens.OrderBy(t => t.ZOrder))
            {
                string tokenKey = token.TokenId.ToString();
                bool isVisible = visibleTokenIds == null || visibleTokenIds.Contains(token.TokenId);
                VisualElement tokenElement = new VisualElement { name = "token-" + token.TokenId };
                tokenElement.AddToClassList("board-token");
                tokenElement.style.position = Position.Absolute;
                tokenElement.style.width = (float)(TokenSizePixels * token.Scale);
                tokenElement.style.height = (float)(TokenSizePixels * token.Scale);
                TokenPosition? previousPosition = previousPositions.TryGetValue(tokenKey, out TokenPosition previous) ? previous : (TokenPosition?)null;
                PositionTokenElement(tokenElement, _tokenMotion.Observe(tokenKey, previousPosition, token.Position, isVisible), token.Scale);
                bool hasPortraitTexture = false;
                if (token.PortraitAssetId.HasValue)
                {
                    Result<Texture2D> portrait = LoadTexture(token.PortraitAssetId.Value);
                    if (portrait.IsSuccess)
                    {
                        ApplyTexture(tokenElement, portrait.Value);
                        hasPortraitTexture = true;
                    }
                    else if (firstAssetError == null)
                    {
                        firstAssetError = portrait.Error;
                    }
                }

                // Solid ownership-colored fill exactly as before, unless a portrait texture is shown.
                if (!hasPortraitTexture)
                {
                    tokenElement.style.backgroundColor = new StyleColor(TokenColor(token, out _));
                }

                bool isSelected = _selectedTokenIds.Contains(token.TokenId.ToString());
                tokenElement.style.borderTopWidth = isSelected ? 3 : 1;
                tokenElement.style.borderBottomWidth = isSelected ? 3 : 1;
                tokenElement.style.borderLeftWidth = isSelected ? 3 : 1;
                tokenElement.style.borderRightWidth = isSelected ? 3 : 1;

                // SLICE-10 Block 6 part 2: a token absent from the real, authoritative ComputeVisibleTokens
                // result is not rendered at all (DisplayStyle.None, not merely darkened) -- an
                // in-range-then-moved-out-of-sight enemy must disappear, matching Block 4's own "map is
                // remembered, tokens are not" decision (task contract section 2.3). Picking is also
                // disabled so a hidden token can never be selected/dragged/hit-tested while invisible. The
                // element is still created and tracked in every dictionary below (unchanged from before this
                // task) so the rest of this class's per-token bookkeeping needs no special-casing.
                tokenElement.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
                tokenElement.pickingMode = isVisible ? PickingMode.Position : PickingMode.Ignore;

                // ODY-S08-104: a token drags itself (PointerDown/Move/Up + CapturePointer, by exact
                // precedent of AssetPoolPresenter.OnItemPointerDown), replacing the old ClickEvent-based
                // selection entirely -- capturing the pointer on every PointerDown suppresses UI Toolkit's
                // own ClickEvent for the same gesture, so a ClickEvent handler here would no longer fire
                // reliably and is removed rather than kept as dead, misleading code. Selection now happens
                // in EndTokenDrag, which OnTokenPointerUp calls: a click (movement under the threshold) still
                // selects, byte-for-byte the same outcome, just reached through the pointer-up path instead.
                TokenId capturedTokenId = token.TokenId;
                var tokenGesture = new BoardPointerGesture();
                _tokenGesturesByTokenId[token.TokenId.ToString()] = tokenGesture;
                tokenElement.RegisterCallback<PointerDownEvent>(evt => OnTokenPointerDown(evt, tokenElement, capturedTokenId));
                tokenElement.RegisterCallback<PointerMoveEvent>(evt => OnTokenPointerMove(evt, tokenElement, capturedTokenId));
                tokenElement.RegisterCallback<PointerUpEvent>(evt => OnTokenPointerUp(evt, tokenElement, capturedTokenId));
                tokenElement.RegisterCallback<PointerCaptureOutEvent>(_ => OnTokenPointerCaptureOut(capturedTokenId));

                _boardArea.Add(tokenElement);
                _tokenElementsByTokenId[token.TokenId.ToString()] = tokenElement;
                _tokenPositionsByTokenId[token.TokenId.ToString()] = token.Position;
                _tokenZOrdersByTokenId[token.TokenId.ToString()] = token.ZOrder;
                _tokenScalesByTokenId[token.TokenId.ToString()] = token.Scale;
                _tokenControllersByTokenId[token.TokenId.ToString()] = token.ControllerUserId;
                if (isVisible) _visibleTokenKeys.Add(tokenKey);
                if (token.CharacterId.HasValue && !_tokenKeysByCharacterId.ContainsKey(token.CharacterId.Value.ToString())) _tokenKeysByCharacterId[token.CharacterId.Value.ToString()] = tokenKey;
            }

            _tokenMotion.RetainOnly(_tokenElementsByTokenId.Keys);
            EnsureTokenMotionTicking();
            return firstAssetError;
        }

        private Color TokenColor(TokenRecord token, out bool isLocalActorControlled)
        {
            isLocalActorControlled = token.ControllerUserId.Equals(LocalActorUserId);
            return isLocalActorControlled ? new Color(0.25f, 0.65f, 0.95f) : new Color(0.75f, 0.35f, 0.30f);
        }

        // ---- SLICE-10 Block 6 part 1: obstacle rendering (walls/doors/windows) and their HP bars --------
        //
        // Full reconciliation of the whole obstacle set on every Refresh(), the exact same "not an
        // incremental diff" shape RenderTokens already uses (task contract section 2.3) -- obstacles never
        // move or resize once created (no drag/resize in this task, section 4's own non-goal), so this is
        // simpler than the token case, not a shortcut taken under time pressure.

        private void RenderObstacles(IReadOnlyList<ObstacleRecord> obstacles)
        {
            if (_boardArea == null) return;
            _obstacleRecordsByObstacleId.Clear();
            _obstacleDurabilityByObstacleId.Clear();

            foreach (ObstacleRecord obstacle in obstacles)
            {
                string key = obstacle.ObstacleId.ToString();
                _obstacleRecordsByObstacleId[key] = obstacle;

                double x1 = _camera.ToPixelsX(obstacle.X1);
                double y1 = _camera.ToPixelsY(obstacle.Y1);
                double x2 = _camera.ToPixelsX(obstacle.X2);
                double y2 = _camera.ToPixelsY(obstacle.Y2);

                VisualElement line = new VisualElement { name = "obstacle-" + obstacle.ObstacleId, pickingMode = PickingMode.Ignore };
                line.style.position = Position.Absolute;
                ApplyObstacleColor(line, obstacle);
                PositionSegmentElement(line, x1, y1, x2, y2, ObstacleLineThicknessPixels);
                _boardArea.Add(line);

                // SLICE-10 Block 6 part 1 task contract section 2.6: one GetObstacleDurability call per
                // obstacle per Refresh() (N+1), not a batch read -- the contract has no batch method and
                // adding one is explicitly out of this task's scope. A failure here (the obstacle was never
                // given a maxHp) is the expected, non-error case for most obstacles: no HP bar, no status
                // message, nothing logged.
                Result<ObstacleDurabilityRecord> durability = _obstacleRepository.GetObstacleDurability(_campaign, obstacle.ObstacleId, NewCorrelationId());
                if (durability.IsSuccess)
                {
                    _obstacleDurabilityByObstacleId[key] = durability.Value;
                    RenderObstacleHpBar(obstacle.ObstacleId, durability.Value, x1, y1, x2, y2);
                }
            }
        }

        // Placeholder colors (task contract section 2.3 -- explicitly not final art, fixed values disclosed
        // in the task report): a Wall is a solid, opaque stone-gray line; a Door is a warm brown that lightens
        // when open; a Window is the same blue-gray as a Wall but translucent.
        private static void ApplyObstacleColor(VisualElement element, ObstacleRecord obstacle)
        {
            Color color;
            switch (obstacle.Kind)
            {
                case ObstacleKind.Door:
                    color = obstacle.IsOpen == true ? new Color(0.75f, 0.55f, 0.30f, 1f) : new Color(0.50f, 0.32f, 0.14f, 1f);
                    break;
                case ObstacleKind.Window:
                    color = new Color(0.55f, 0.75f, 0.95f, 0.45f);
                    break;
                default:
                    color = new Color(0.55f, 0.55f, 0.58f, 1f);
                    break;
            }

            element.style.backgroundColor = new StyleColor(color);
        }

        /// <summary>
        /// Positions and rotates an absolutely-positioned <see cref="VisualElement"/> so it visually connects
        /// two board-local pixel points -- the technique chosen for drawing a line with UI Toolkit, since no
        /// prior task in this codebase renders one (task contract section 2.3, disclosed in the task report):
        /// the element's own width becomes the segment's pixel length, its height is a small fixed
        /// <paramref name="thicknessPixels"/>, and <c>style.rotate</c>/<c>transformOrigin</c> (supported by
        /// this project's Unity 6 UI Toolkit runtime) rotate it around its own left-center point, which is
        /// pinned to (<paramref name="x1"/>, <paramref name="y1"/>) -- so the element's left edge is always the
        /// segment's start point, regardless of angle.
        /// </summary>
        private static void PositionSegmentElement(VisualElement element, double x1, double y1, double x2, double y2, double thicknessPixels)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            double length = Math.Sqrt(dx * dx + dy * dy);
            double angleDegrees = Math.Atan2(dy, dx) * (180.0 / Math.PI);

            element.style.left = (float)x1;
            element.style.top = (float)(y1 - thicknessPixels / 2.0);
            element.style.width = (float)length;
            element.style.height = (float)thicknessPixels;
            element.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(50));
            element.style.rotate = new StyleRotate(new Rotate(new Angle((float)angleDegrees, AngleUnit.Degree)));
        }

        private const double ObstacleHpBarWidthPixels = 40.0;
        private const double ObstacleHpBarHeightPixels = 6.0;

        private void RenderObstacleHpBar(ObstacleId obstacleId, ObstacleDurabilityRecord durability, double x1, double y1, double x2, double y2)
        {
            if (_boardArea == null) return;
            double midX = (x1 + x2) / 2.0;
            double midY = (y1 + y2) / 2.0 - ObstacleHpBarHeightPixels - 6.0;

            var track = new VisualElement { name = "obstacle-hp-track-" + obstacleId, pickingMode = PickingMode.Ignore };
            track.style.position = Position.Absolute;
            track.style.left = (float)(midX - ObstacleHpBarWidthPixels / 2.0);
            track.style.top = (float)midY;
            track.style.width = (float)ObstacleHpBarWidthPixels;
            track.style.height = (float)ObstacleHpBarHeightPixels;
            track.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f, 0.85f));

            double fraction = durability.MaxHp > 0 ? Math.Max(0.0, Math.Min(1.0, (double)durability.CurrentHp / durability.MaxHp)) : 0.0;
            var fill = new VisualElement { name = "obstacle-hp-fill-" + obstacleId, pickingMode = PickingMode.Ignore };
            fill.style.position = Position.Absolute;
            fill.style.left = 0;
            fill.style.top = 0;
            fill.style.bottom = 0;
            fill.style.width = new Length((float)(fraction * 100.0), LengthUnit.Percent);
            fill.style.backgroundColor = new StyleColor(HpBarColor(fraction));
            track.Add(fill);

            _boardArea.Add(track);
        }

        private static Color HpBarColor(double fraction)
        {
            if (fraction > 0.5) return new Color(0.30f, 0.80f, 0.30f, 1f);
            if (fraction > 0.25) return new Color(0.90f, 0.75f, 0.20f, 1f);
            return new Color(0.85f, 0.25f, 0.20f, 1f);
        }

        /// <summary>
        /// Selects (or, if already selected, deselects) a token. Public so
        /// tests can exercise the presenter's own selection/move logic
        /// directly, without depending on UI Toolkit's synthetic pointer
        /// event dispatch inside an EditMode batch run -- the same
        /// separation-of-logic-from-event-plumbing this task's own contract
        /// section 5 calls for. The UI Toolkit click callback
        /// (<see cref="RenderTokens"/>) is a thin wrapper over this method,
        /// not a second implementation of it.
        /// </summary>
        public TokenId? SelectedTokenId
        {
            get
            {
                if (_selectedTokenIds.Count != 1) return null;
                foreach (string key in _selectedTokenIds) return TokenId.Parse(key);
                return null;
            }
        }

        /// <summary>ODY-S08-106: every currently selected token (session-only; empty when nothing is selected).</summary>
        public IReadOnlyList<TokenId> SelectedTokenIds
        {
            get
            {
                var result = new List<TokenId>(_selectedTokenIds.Count);
                foreach (string key in _selectedTokenIds) result.Add(TokenId.Parse(key));
                return result;
            }
        }

        /// <summary>
        /// A plain (no-modifier) click on a token: replaces the whole selection with this one token, except
        /// that clicking the token that is already the only selected one clears the selection (the
        /// pre-ODY-S08-106 toggle, unchanged for a selection of size 1).
        /// </summary>
        public void SelectToken(TokenId tokenId)
        {
            string key = tokenId.ToString();
            if (_selectedTokenIds.Count == 1 && _selectedTokenIds.Contains(key))
            {
                _selectedTokenIds.Clear();
                SetStatus("Deselected.");
                Refresh();
                return;
            }

            _selectedTokenIds.Clear();
            _selectedTokenIds.Add(key);
            SetStatus("Selected token " + tokenId + ".");
            Refresh();
        }

        /// <summary>ODY-S08-106/107: a Shift+click on a token: adds it to the selection if absent, removes exactly it if present; the rest of the selection is untouched.</summary>
        public void ToggleTokenSelection(TokenId tokenId)
        {
            string key = tokenId.ToString();
            if (!_selectedTokenIds.Remove(key))
            {
                _selectedTokenIds.Add(key);
                SetStatus("Added token " + tokenId + " to the selection (" + _selectedTokenIds.Count + " selected).");
            }
            else
            {
                SetStatus("Removed token " + tokenId + " from the selection (" + _selectedTokenIds.Count + " selected).");
            }

            Refresh();
        }

        /// <summary>
        /// Attempts to move the currently-selected token to <paramref name="destination"/>
        /// via <see cref="BoardMovementService.MoveToken"/>, using the
        /// current <see cref="LocalActorUserId"/> (MainGM-ness is looked up from the stored campaign membership).
        /// Public for the same testability reason as <see cref="SelectToken"/>.
        /// </summary>
        public Result<TokenRecord> TryMoveSelectedTokenTo(TokenPosition destination)
        {
            TokenId? selected = SelectedTokenId;
            if (!selected.HasValue)
            {
                SetStatus("Select a token first.");
                return Result<TokenRecord>.Failure(BoardScreenErrors.NoTokenSelected());
            }

            TokenId tokenId = selected.Value;
            // ODY-S11-211: the local user's own move (or its rollback) is drawn instantly.
            _tokenMotion.MarkLocal(tokenId.ToString());
            Result<TokenRecord> current = _sceneRepository.GetToken(_campaign, tokenId, NewCorrelationId());
            if (current.IsFailure)
            {
                SetStatus("Move failed: " + current.Error.SafeReasonCode);
                _selectedTokenIds.Clear();
                Refresh();
                return current;
            }

            var request = new MoveTokenRequest(_campaign, LocalActorUserId, tokenId, destination, current.Value.Revision, NewCommandId(), NewCorrelationId());
            Result<TokenRecord> moved = BoardMovementService.MoveToken(_sceneRepository, _campaignRepository, request);

            _selectedTokenIds.Clear();
            if (moved.IsFailure)
            {
                SetStatus("Move denied: " + moved.Error.SafeReasonCode);
            }
            else
            {
                SetStatus("Moved token " + tokenId + " to (" + moved.Value.Position.X.ToString("0.0") + ", " + moved.Value.Position.Y.ToString("0.0") + ").");
                RecordExplorationBestEffort(tokenId);
            }

            Refresh();
            return moved;
        }

        /// <summary>
        /// Attempts to move <paramref name="tokenId"/> to <paramref name="destination"/>, without requiring
        /// (or touching) the current selection -- the token-drag path this task adds. Symmetric to
        /// <see cref="TryMoveSelectedTokenTo"/>'s own error handling: the same real, committed
        /// <see cref="BoardMovementService.MoveToken"/> call (never a preview), the same status text shape,
        /// and <see cref="Refresh"/> called unconditionally afterwards -- which is also what rolls the
        /// visual position back to the last confirmed one on a denied/failed move (task contract section
        /// 1.4): nothing was persisted, so re-reading from the repository restores the pre-drag position.
        /// Public for the same testability reason as every other method in this class.
        /// </summary>
        public Result<TokenRecord> TryMoveTokenTo(TokenId tokenId, TokenPosition destination)
        {
            // ODY-S11-211: the local user's own move (or its rollback) is drawn instantly.
            _tokenMotion.MarkLocal(tokenId.ToString());
            Result<TokenRecord> current = _sceneRepository.GetToken(_campaign, tokenId, NewCorrelationId());
            if (current.IsFailure)
            {
                SetStatus("Move failed: " + current.Error.SafeReasonCode);
                Refresh();
                return current;
            }

            var request = new MoveTokenRequest(_campaign, LocalActorUserId, tokenId, destination, current.Value.Revision, NewCommandId(), NewCorrelationId());
            Result<TokenRecord> moved = BoardMovementService.MoveToken(_sceneRepository, _campaignRepository, request);

            if (moved.IsFailure)
            {
                SetStatus("Move denied: " + moved.Error.SafeReasonCode);
            }
            else
            {
                SetStatus("Moved token " + tokenId + " to (" + moved.Value.Position.X.ToString("0.0") + ", " + moved.Value.Position.Y.ToString("0.0") + ").");
                RecordExplorationBestEffort(tokenId);
            }

            Refresh();
            return moved;
        }

        // SLICE-10 Block 6 part 2: best-effort map-memory update after a successful token move/creation.
        // The task contract's own facts (section 1) name only TryMoveTokenTo as the drag/click-move path,
        // but TryMoveSelectedTokenTo (the older, still-live click-to-move path invoked from
        // OnBoardPointerUp) commits the exact same BoardMovementService.MoveToken call -- wiring only one
        // of the two would silently leave map memory frozen for whichever move path a session happens to
        // use, which the task's own purpose (a demonstrable, real fog of war) rules out; this deviates from
        // the contract's literal method name but not from its stated intent, and is disclosed as such in the
        // task report. A failure here (any Result.Failure, or the ports simply not being ready) is
        // swallowed -- it must never surface as an error of the move/creation itself.
        private void RecordExplorationBestEffort(TokenId observerTokenId)
        {
            var request = new RecordExplorationRequest(_campaign, observerTokenId, NewCommandId(), NewCorrelationId());
            PlayerVisibilityService.RecordExploration(_fogRepository, _sceneRepository, _visionRepository, request);
        }

        // ---- ODY-S08-104: dragging a token across the board -----------------------------------------
        //
        // Real UI Toolkit callbacks are thin wrappers over BeginTokenDrag/MoveTokenDrag/EndTokenDrag, the
        // same testable-public-method shape as the board's own camera gesture (ODY-S08-102) and the asset
        // pool's own drag (ODY-S08-103). Coordinates are converted from panel space to board-local pixels
        // via _boardArea.WorldToLocal -- the same conversion AssetPoolPresenter's own drop-resolution
        // already uses -- so ToWorldPosition/BoardCamera need no second implementation.
        //
        // Until PointerUp, only the in-memory preview (_tokenPositionsByTokenId + PositionTokenElement,
        // ODY-S08-102's own mechanism) is touched -- no repository call. Exactly one MoveToken call happens,
        // at PointerUp, whether the gesture turns out to be a click (routed to SelectToken instead) or a drag.

        private void OnTokenPointerDown(PointerDownEvent evt, VisualElement tokenElement, TokenId tokenId)
        {
            // A pointer-down that starts on a token must never reach the board's own gesture tracking
            // (OnBoardPointerDown) -- by exact precedent of ODY-S08-102's own reasoning, now serving the
            // token's own drag instead of merely protecting a click.
            // ODY-S08-107: only the left button drives a token. Middle/right presses are left alone (no
            // StopPropagation, no capture) so they bubble to the board and pan / place the marker even over a
            // token. A left press while another gesture already owns the pointer is ignored too.
            if (evt.button != 0) return;
            if (_activeBoardButton != -1 || _draggingTokenId.HasValue) return;
            evt.StopPropagation();
            if (_boardArea == null) return;
            tokenElement.CapturePointer(evt.pointerId);
            Vector2 boardLocal = _boardArea.WorldToLocal(evt.position);
            // ODY-S08-106/107: the Shift state (it was Ctrl before ODY-S08-107) is read here, at the physical
            // press, and carried in the gesture state. Ctrl is not read anywhere: a Ctrl+click is a plain click.
            BeginTokenDrag(tokenId, boardLocal.x, boardLocal.y, evt.shiftKey);
        }

        private void OnTokenPointerMove(PointerMoveEvent evt, VisualElement tokenElement, TokenId tokenId)
        {
            if (_boardArea == null || !tokenElement.HasPointerCapture(evt.pointerId)) return;
            Vector2 boardLocal = _boardArea.WorldToLocal(evt.position);
            MoveTokenDrag(tokenId, boardLocal.x, boardLocal.y);
        }

        private void OnTokenPointerUp(PointerUpEvent evt, VisualElement tokenElement, TokenId tokenId)
        {
            if (evt.button != 0) return; // releasing another button must not end this token's drag
            if (_boardArea == null || !tokenElement.HasPointerCapture(evt.pointerId)) return;
            Vector2 boardLocal = _boardArea.WorldToLocal(evt.position);
            // Cleared before ReleasePointer so that whatever PointerCaptureOutEvent it produces (this
            // codebase makes no assumption about whether that happens synchronously or not) finds
            // _draggingTokenId already cleared and does nothing -- see OnTokenPointerCaptureOut.
            _draggingTokenId = null;
            tokenElement.ReleasePointer(evt.pointerId);
            EndTokenDrag(tokenId, boardLocal.x, boardLocal.y);
        }

        private void OnTokenPointerCaptureOut(TokenId tokenId)
        {
            string key = tokenId.ToString();
            if (_tokenGesturesByTokenId.TryGetValue(key, out BoardPointerGesture? gesture)) gesture.Cancel();

            // Only true when capture was lost some way OTHER than our own PointerUp/ReleasePointer above
            // (task contract section 1.5) -- an ordinary end-of-drag already cleared this. Refresh() re-reads
            // the token's last CONFIRMED position from the repository (nothing was persisted mid-drag),
            // which is exactly the visual rollback this case needs -- the same mechanism TryMoveTokenTo's
            // own Refresh() already uses for a denied/failed commit.
            if (_draggingTokenId.HasValue && _draggingTokenId.Value.Equals(tokenId))
            {
                _draggingTokenId = null;
                // ODY-S11-211: rolling back the local user's own drag is drawn instantly, not animated.
                _tokenMotion.MarkLocal(key);
                foreach (string member in _dragGroup) _tokenMotion.MarkLocal(member);
                ResetDragState();
                Refresh();
            }
        }

        /// <summary>Starts tracking a token's own drag gesture. Public -- see the class remarks on testability.</summary>
        public void BeginTokenDrag(TokenId tokenId, double boardPixelX, double boardPixelY, bool shift = false)
        {
            string anchorKey = tokenId.ToString();
            if (!_tokenGesturesByTokenId.TryGetValue(anchorKey, out BoardPointerGesture? gesture)) return;
            gesture.Begin(boardPixelX, boardPixelY);
            _draggingTokenId = tokenId;

            // ODY-S08-106: which tokens a drag would carry is decided now, from the selection at press time.
            // Only a token that is part of a selection of two or more drags the whole group; anything else
            // drags just the grabbed token (exactly the ODY-S08-104 behaviour).
            ResetDragState();
            _dragShift = shift;
            _dragAnchorKey = anchorKey;
            if (_selectedTokenIds.Count >= 2 && _selectedTokenIds.Contains(anchorKey))
            {
                foreach (string key in _selectedTokenIds)
                {
                    if (_tokenPositionsByTokenId.ContainsKey(key)) _dragGroup.Add(key);
                }
            }
            else
            {
                _dragGroup.Add(anchorKey);
            }

            foreach (string key in _dragGroup)
            {
                // ODY-S11-211: a grabbed token stops any remote animation -- the local drag is always 1:1.
                _tokenMotion.Cancel(key);
                if (_tokenPositionsByTokenId.TryGetValue(key, out TokenPosition start)) _dragStartPositions[key] = start;
            }

            // ODY-S08-105: pointer-down on a token raises it above the others, before the click/drag
            // decision. Purely additive -- the gesture above is already set up exactly as in ODY-S08-104.
            RaiseTokenToTop(tokenId);
        }

        /// <summary>
        /// ODY-S08-105: makes the token the topmost one in the scene. If it already is (strictly above every
        /// other token), makes no repository call at all. Otherwise reads the token's revision fresh and calls
        /// <see cref="ISceneRepository.SetTokenZOrder"/> with max+1, then moves the existing element to the
        /// end of the board's children in place -- no <see cref="Refresh"/> mid-gesture, which would rebuild
        /// the DOM under the active pointer capture. A failed write leaves the visual order untouched.
        /// Public -- see the class remarks on testability.
        /// </summary>
        public Result RaiseTokenToTop(TokenId tokenId)
        {
            string key = tokenId.ToString();
            if (!_tokenZOrdersByTokenId.TryGetValue(key, out long current)) return Result.Success();

            long max = long.MinValue;
            bool othersAtOrAbove = false;
            foreach (KeyValuePair<string, long> entry in _tokenZOrdersByTokenId)
            {
                if (entry.Value > max) max = entry.Value;
                if (entry.Key != key && entry.Value >= current) othersAtOrAbove = true;
            }

            if (!othersAtOrAbove) return Result.Success();

            Result<TokenRecord> fresh = _sceneRepository.GetToken(_campaign, tokenId, NewCorrelationId());
            if (fresh.IsFailure)
            {
                SetStatus("Could not raise the token: " + fresh.Error.Code);
                return Result.Failure(fresh.Error);
            }

            long newZOrder = max + 1;
            Result<TokenRecord> updated = _sceneRepository.SetTokenZOrder(_campaign, tokenId, newZOrder, fresh.Value.Revision, NewCommandId(), NewCorrelationId());
            if (updated.IsFailure)
            {
                SetStatus("Could not raise the token: " + updated.Error.Code);
                return Result.Failure(updated.Error);
            }

            _tokenZOrdersByTokenId[key] = updated.Value.ZOrder;
            if (_tokenElementsByTokenId.TryGetValue(key, out VisualElement? element)) element.BringToFront();
            return Result.Success();
        }

        /// <summary>
        /// Feeds a pointer move into a token's own drag gesture. Once the drag threshold is crossed, updates
        /// only the in-memory visual preview (never the repository) to follow the cursor's current world
        /// position. Public -- see the class remarks on testability.
        /// </summary>
        public void MoveTokenDrag(TokenId tokenId, double boardPixelX, double boardPixelY)
        {
            string key = tokenId.ToString();
            if (!_tokenGesturesByTokenId.TryGetValue(key, out BoardPointerGesture? gesture)) return;

            if (gesture.Move(boardPixelX, boardPixelY, out _, out _) && gesture.IsDragging)
            {
                BeginDragIfNeeded(key);

                TokenPosition worldPosition = ToWorldPosition(boardPixelX, boardPixelY);
                ApplyDragPreview(key, worldPosition);
            }
        }

        // Live preview only (never the repository): the grabbed token follows the pointer exactly as in
        // ODY-S08-104; every other token of the group shifts by the same world delta, so the group keeps its
        // shape.
        private void ApplyDragPreview(string anchorKey, TokenPosition anchorWorldPosition)
        {
            _dragStartPositions.TryGetValue(anchorKey, out TokenPosition anchorStart);
            double deltaX = anchorWorldPosition.X - anchorStart.X;
            double deltaY = anchorWorldPosition.Y - anchorStart.Y;
            foreach (string memberKey in _dragGroup)
            {
                TokenPosition preview;
                if (memberKey == anchorKey)
                {
                    preview = anchorWorldPosition;
                }
                else if (_dragStartPositions.TryGetValue(memberKey, out TokenPosition memberStart))
                {
                    preview = new TokenPosition(memberStart.X + deltaX, memberStart.Y + deltaY);
                }
                else
                {
                    continue;
                }

                _tokenPositionsByTokenId[memberKey] = preview;
                if (_tokenElementsByTokenId.TryGetValue(memberKey, out VisualElement? element))
                {
                    PositionTokenElement(element, preview, ScaleOf(memberKey));
                }
            }
        }

        // First real drag step of a gesture: grabbing a token that is not part of the current selection makes
        // it the selection (a Shift-drag never edits the selection). Updated in place -- no Refresh under the
        // active pointer capture.
        private void BeginDragIfNeeded(string key)
        {
            if (_dragStarted) return;
            _dragStarted = true;
            if (!_dragShift && !_selectedTokenIds.Contains(key))
            {
                _selectedTokenIds.Clear();
                _selectedTokenIds.Add(key);
                UpdateSelectionBorders();
            }
        }

        private void ResetDragState()
        {
            _dragShift = false;
            _dragStarted = false;
            _dragAnchorKey = null;
            _dragGroup.Clear();
            _dragStartPositions.Clear();
        }

        // Re-applies the selection border to the already-rendered elements (same widths RenderTokens uses).
        private void UpdateSelectionBorders()
        {
            foreach (KeyValuePair<string, VisualElement> entry in _tokenElementsByTokenId)
            {
                bool isSelected = _selectedTokenIds.Contains(entry.Key);
                entry.Value.style.borderTopWidth = isSelected ? 3 : 1;
                entry.Value.style.borderBottomWidth = isSelected ? 3 : 1;
                entry.Value.style.borderLeftWidth = isSelected ? 3 : 1;
                entry.Value.style.borderRightWidth = isSelected ? 3 : 1;
            }
        }

        /// <summary>
        /// Ends a token's own drag gesture. Movement below the drag threshold is a click -- selects the
        /// token, exactly as the old <c>ClickEvent</c> handler did, and leaves its position untouched.
        /// Movement above the threshold commits the drag with exactly one <see cref="TryMoveTokenTo"/> call
        /// (never one per <see cref="MoveTokenDrag"/> call). Public -- see the class remarks on testability.
        /// </summary>
        public void EndTokenDrag(TokenId tokenId, double boardPixelX, double boardPixelY)
        {
            if (!_tokenGesturesByTokenId.TryGetValue(tokenId.ToString(), out BoardPointerGesture? gesture)) return;
            bool wasClick = gesture.End();
            _draggingTokenId = null;
            bool shift = _dragShift;
            List<string> group = new List<string>(_dragGroup);

            if (wasClick)
            {
                ResetDragState();
                // ODY-S08-106/107: Shift+click adds/removes just this token; a plain click replaces (or, for the
                // only selected token, clears) the selection -- the modifier was read at pointer-down.
                if (shift) ToggleTokenSelection(tokenId);
                else SelectToken(tokenId);
                return;
            }

            // Destinations are computed from the pointer BEFORE any commit: every TryMoveTokenTo calls
            // Refresh(), which rebuilds the in-memory positions from the repository.
            string anchorKey = tokenId.ToString();
            BeginDragIfNeeded(anchorKey);
            if (group.Count == 0)
            {
                group.Add(anchorKey);
                _dragGroup.Add(anchorKey);
                if (_tokenPositionsByTokenId.TryGetValue(anchorKey, out TokenPosition anchorStart)) _dragStartPositions[anchorKey] = anchorStart;
            }

            ApplyDragPreview(anchorKey, ToWorldPosition(boardPixelX, boardPixelY));
            var destinations = new List<KeyValuePair<TokenId, TokenPosition>>();
            foreach (string memberKey in group)
            {
                if (_tokenPositionsByTokenId.TryGetValue(memberKey, out TokenPosition destination))
                {
                    destinations.Add(new KeyValuePair<TokenId, TokenPosition>(TokenId.Parse(memberKey), destination));
                }
            }

            ResetDragState();
            // ODY-S11-211: every member's commit (and any rollback) is the local user's own move: drawn instantly.
            foreach (KeyValuePair<TokenId, TokenPosition> entry in destinations) _tokenMotion.MarkLocal(entry.Key.ToString());

            // ODY-S08-106: a group commits as one ordinary MoveToken per token (BoardMovementService has no
            // batch API). NOT atomic: a token whose move is denied is rolled back visually by its own
            // Refresh(), while the tokens already committed stay committed -- no attempt is made to undo them.
            int moved = 0;
            foreach (KeyValuePair<TokenId, TokenPosition> entry in destinations)
            {
                if (TryMoveTokenTo(entry.Key, entry.Value).IsSuccess) moved++;
            }

            if (destinations.Count > 1)
            {
                SetStatus("Moved " + moved + " of " + destinations.Count + " tokens.");
            }
        }

        // ---- SLICE-10 Block 6 part 1: obstacle selection, door toggle, and manual damage ---------------

        /// <summary>The obstacle currently selected in <see cref="BoardTool.Select"/> mode (for the damage panel), or <c>null</c>. Exposed for tests.</summary>
        public ObstacleId? SelectedObstacleId => _selectedObstacleId;

        // Pure math against the last-rendered obstacle positions, by exact precedent of HitTestToken -- no
        // per-element pointer-event handler on the obstacle line itself (obstacles are pickingMode.Ignore;
        // unlike a token, nothing here is ever dragged, so a real per-element handler buys nothing).
        private ObstacleId? HitTestObstacle(double boardPixelX, double boardPixelY)
        {
            ObstacleId? best = null;
            double bestDistance = ObstacleHitTestThresholdPixels;
            foreach (KeyValuePair<string, ObstacleRecord> entry in _obstacleRecordsByObstacleId)
            {
                ObstacleRecord obstacle = entry.Value;
                double x1 = _camera.ToPixelsX(obstacle.X1);
                double y1 = _camera.ToPixelsY(obstacle.Y1);
                double x2 = _camera.ToPixelsX(obstacle.X2);
                double y2 = _camera.ToPixelsY(obstacle.Y2);
                double distance = BoardHitTestMath.DistancePointToSegment(boardPixelX, boardPixelY, x1, y1, x2, y2);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = obstacle.ObstacleId;
                }
            }

            return best;
        }

        /// <summary>
        /// A click on an obstacle in <see cref="BoardTool.Select"/> mode always selects it (so its damage
        /// panel, if any, appears/updates); a <see cref="ObstacleKind.Door"/> is additionally toggled by the
        /// same click -- task contract section 2.4 (any click toggles a door) and section 2.6 (any click
        /// selects for the damage panel) both describe the same gesture without saying which wins, so this
        /// combines them rather than picking one arbitrarily, disclosed in the task report.
        /// </summary>
        private void HandleObstacleClick(ObstacleId obstacleId)
        {
            _selectedObstacleId = obstacleId;
            if (_obstacleRecordsByObstacleId.TryGetValue(obstacleId.ToString(), out ObstacleRecord obstacle) && obstacle.Kind == ObstacleKind.Door)
            {
                ToggleObstacleDoor(obstacleId, obstacle);
                return;
            }

            SetStatus("Selected obstacle " + obstacleId + ".");
            Refresh();
        }

        /// <summary>
        /// Toggles a door's <see cref="ObstacleRecord.IsOpen"/> via <see cref="ObstacleAuthoringService.ToggleDoorState"/>
        /// -- open to any registered campaign participant on the server (task contract section 2.4: no
        /// artificial client-side role restriction), exactly the same direct-call/read-revision/Refresh()
        /// pattern every other server command in this class already uses. <paramref name="lastKnown"/>
        /// (the `RenderObstacles`-cached record from the last `Refresh()`) is used only to identify the
        /// obstacle and its Scene -- ODY-S10-112 finding: the actual `Revision`/`IsOpen` passed to the
        /// server command are re-read fresh via <see cref="IObstacleRepository.ListObstacles"/>
        /// immediately before the write, the same "never trust the render-time cache for a revision-gated
        /// command" rule <see cref="TryApplyObstacleDamage"/>'s own fresh <c>GetObstacleDurability</c> call
        /// already follows -- a stale cached revision was causing a spurious "denied" toggle whenever
        /// another participant had already toggled the same door since the last `Refresh()`. No single-
        /// obstacle-by-id read exists on <see cref="IObstacleRepository"/> (only `ListObstacles`/Scene and
        /// `GetObstacleDurability`/id), so this scans the fresh `ListObstacles` result for the matching id
        /// rather than adding a new method to the server-side contract for a client-only fix.
        /// </summary>
        private void ToggleObstacleDoor(ObstacleId obstacleId, ObstacleRecord lastKnown)
        {
            Result<IReadOnlyList<ObstacleRecord>> fresh = _obstacleRepository.ListObstacles(_campaign, lastKnown.SceneId, NewCorrelationId());
            if (fresh.IsFailure)
            {
                SetStatus("Could not toggle the door: " + fresh.Error.SafeReasonCode);
                Refresh();
                return;
            }

            ObstacleRecord? current = null;
            foreach (ObstacleRecord candidate in fresh.Value)
            {
                if (candidate.ObstacleId.ToString() == obstacleId.ToString())
                {
                    current = candidate;
                    break;
                }
            }

            // The same "denied/unavailable" status path as any other rejected server command -- no crash,
            // no special-cased UI, just a status message and a Refresh() that re-reads the real state.
            if (current == null || current.Kind != ObstacleKind.Door)
            {
                SetStatus("Could not toggle the door: obstacle is no longer available.");
                Refresh();
                return;
            }

            bool nextIsOpen = !(current.IsOpen ?? false);
            var request = new ToggleDoorStateRequest(_campaign, obstacleId, nextIsOpen, current.Revision, LocalActorUserId, NewCommandId(), NewCorrelationId());
            Result<ObstacleRecord> toggled = ObstacleAuthoringService.ToggleDoorState(_obstacleRepository, _campaignRepository, request);
            if (toggled.IsFailure)
            {
                SetStatus("Could not toggle the door: " + toggled.Error.SafeReasonCode);
            }
            else
            {
                SetStatus("Door is now " + (toggled.Value.IsOpen == true ? "open." : "closed."));
            }

            Refresh();
        }

        /// <summary>
        /// MainGM-only manual damage command (task contract section 2.6) -- <see cref="ObstacleAuthoringService.ApplyObstacleDamage"/>
        /// is a direct command outside the attack pipeline (SLICE-10 Block 5's own decision, unchanged here),
        /// so its UI lives on the obstacle itself, not in any attack UI. Reads the durability revision fresh
        /// immediately before writing, the same pattern every other command in this class already uses.
        /// Public -- see the class remarks on testability.
        /// </summary>
        public Result<ObstacleDurabilityRecord> TryApplyObstacleDamage(ObstacleId obstacleId, long amount)
        {
            Result<ObstacleDurabilityRecord> current = _obstacleRepository.GetObstacleDurability(_campaign, obstacleId, NewCorrelationId());
            if (current.IsFailure)
            {
                SetStatus("Could not read obstacle durability: " + current.Error.SafeReasonCode);
                return current;
            }

            var request = new ApplyObstacleDamageRequest(_campaign, obstacleId, amount, current.Value.Revision, LocalActorUserId, NewCommandId(), NewCorrelationId());
            Result<ObstacleDurabilityRecord> applied = ObstacleAuthoringService.ApplyObstacleDamage(_obstacleRepository, _campaignRepository, request);
            if (applied.IsFailure)
            {
                SetStatus("Damage denied: " + applied.Error.SafeReasonCode);
            }
            else
            {
                SetStatus("Applied " + amount + " damage; " + applied.Value.CurrentHp + "/" + applied.Value.MaxHp + " HP remaining.");
            }

            Refresh();
            return applied;
        }

        private void BuildObstacleInspector()
        {
            _obstacleInspectorElement = new VisualElement { name = "obstacle-inspector" };
            _obstacleInspectorElement.style.position = Position.Absolute;
            _obstacleInspectorElement.style.backgroundColor = new StyleColor(new Color(0.10f, 0.10f, 0.12f, 0.92f));
            _obstacleInspectorElement.style.paddingLeft = 4;
            _obstacleInspectorElement.style.paddingRight = 4;
            _obstacleInspectorElement.style.paddingTop = 2;
            _obstacleInspectorElement.style.paddingBottom = 2;
            _obstacleInspectorElement.style.flexDirection = FlexDirection.Row;

            _obstacleDamageAmountField = new IntegerField { name = "obstacle-damage-amount", value = 1 };
            _obstacleDamageAmountField.style.width = 48;
            _obstacleInspectorElement.Add(_obstacleDamageAmountField);

            var damageButton = new Button(OnApplyDamageButtonClicked) { name = "obstacle-damage-button", text = "Apply Damage" };
            _obstacleInspectorElement.Add(damageButton);
        }

        private void OnApplyDamageButtonClicked()
        {
            if (!_selectedObstacleId.HasValue || _obstacleDamageAmountField == null) return;
            long amount = _obstacleDamageAmountField.value;
            if (amount <= 0) return;
            TryApplyObstacleDamage(_selectedObstacleId.Value, amount);
        }

        /// <summary>
        /// Shows the damage panel next to the selected obstacle only when it is still rendered, has a
        /// durability record (task contract section 2.6: no bar/panel at all for an indestructible
        /// obstacle), and the local actor presents as MainGM (presentational only -- the server re-checks
        /// <see cref="ObstacleAuthoringService.ApplyObstacleDamage"/> regardless).
        /// </summary>
        private void ShowObstacleInspectorIfSelected()
        {
            if (_obstacleInspectorElement == null || _boardArea == null) return;
            if (!_selectedObstacleId.HasValue || !LocalActorIsMainGm)
            {
                _obstacleInspectorElement.RemoveFromHierarchy();
                return;
            }

            string key = _selectedObstacleId.Value.ToString();
            if (!_obstacleRecordsByObstacleId.TryGetValue(key, out ObstacleRecord obstacle) || !_obstacleDurabilityByObstacleId.ContainsKey(key))
            {
                _obstacleInspectorElement.RemoveFromHierarchy();
                return;
            }

            double x1 = _camera.ToPixelsX(obstacle.X1);
            double y1 = _camera.ToPixelsY(obstacle.Y1);
            double x2 = _camera.ToPixelsX(obstacle.X2);
            double y2 = _camera.ToPixelsY(obstacle.Y2);
            _obstacleInspectorElement.style.left = (float)((x1 + x2) / 2.0);
            _obstacleInspectorElement.style.top = (float)((y1 + y2) / 2.0 + 12.0);

            if (_obstacleInspectorElement.parent != _boardArea) _boardArea.Add(_obstacleInspectorElement);
        }

        // ---- SLICE-10 Block 6 part 3: token facing/FOV/view-distance inspector, vision indicator, cover preview ----

        /// <summary>
        /// Shows the token vision inspector next to the currently, singly selected token (<see cref="SelectedTokenId"/>
        /// -- never the multi-select group, task contract section 3's own invariant against reusing group
        /// selection for attacker/target semantics) and, while shown, draws its facing/FOV indicator.
        /// Facing is editable by the token's own controller or a MainGm; FOV/view-distance only by a MainGm
        /// -- both presentational gates only, exactly as <see cref="ShowObstacleInspectorIfSelected"/>'s own
        /// MainGm gate already is; the server re-checks <see cref="TokenVisionService.SetTokenFacing"/>/
        /// <see cref="TokenVisionService.SetTokenVisionParameters"/> regardless.
        /// </summary>
        private void ShowTokenVisionInspectorIfSelected()
        {
            if (_tokenInspectorPresenter == null || _boardArea == null) return;

            TokenId? selected = SelectedTokenId;
            if (!selected.HasValue)
            {
                _tokenInspectorPresenter.Element.RemoveFromHierarchy();
                _lastInspectedTokenId = null;
                return;
            }

            string key = selected.Value.ToString();
            if (!_tokenPositionsByTokenId.TryGetValue(key, out TokenPosition position) || !_tokenControllersByTokenId.TryGetValue(key, out UserId controller))
            {
                _tokenInspectorPresenter.Element.RemoveFromHierarchy();
                _lastInspectedTokenId = null;
                return;
            }

            Result<TokenVisionSettingsRecord> vision = _visionRepository.GetVisionSettings(_campaign, selected.Value, NewCorrelationId());
            if (vision.IsFailure)
            {
                _tokenInspectorPresenter.Element.RemoveFromHierarchy();
                _lastInspectedTokenId = null;
                return;
            }

            // SetValues only on an actual selection change -- see BoardTokenInspectorPresenter.SetValues's
            // own remarks on why this must not run on every Refresh().
            if (!_lastInspectedTokenId.HasValue || !_lastInspectedTokenId.Value.Equals(selected.Value))
            {
                _tokenInspectorPresenter.SetValues(vision.Value.FacingDegrees, vision.Value.FovAngleDegrees, vision.Value.ViewDistance);
                _lastInspectedTokenId = selected;
            }

            bool facingEditable = LocalActorIsMainGm || controller.Equals(LocalActorUserId);
            _tokenInspectorPresenter.SetEditable(facingEditable, LocalActorIsMainGm);

            var otherTokenIds = new List<TokenId>();
            foreach (string otherKey in _tokenPositionsByTokenId.Keys)
            {
                if (otherKey == key) continue;
                otherTokenIds.Add(TokenId.Parse(otherKey));
            }

            _tokenInspectorPresenter.SetCoverTargets(otherTokenIds);

            double centerX = _camera.ToPixelsX(position.X);
            double centerY = _camera.ToPixelsY(position.Y);
            _tokenInspectorPresenter.PositionAt(centerX, centerY + TokenSizePixels + 4.0);

            if (_tokenInspectorPresenter.Element.parent != _boardArea) _boardArea.Add(_tokenInspectorPresenter.Element);

            RenderTokenVisionIndicator(centerX, centerY, vision.Value.FacingDegrees, vision.Value.FovAngleDegrees, vision.Value.ViewDistance);
        }

        /// <summary>
        /// Minimal direction/FOV-cone indicator (task contract section 4: no final art) -- one line in the
        /// facing direction, length proportional to <paramref name="viewDistance"/>, plus two more marking
        /// the FOV cone's edges when <paramref name="fovAngleDegrees"/> is less than the omnidirectional
        /// default (360 -- a cone would be meaningless at exactly 360, so only the direction line is drawn
        /// then). Uses <see cref="PositionSegmentElement"/>, the exact rotation technique Block 6 part 1's
        /// obstacle-line rendering already established -- no new drawing technique needed. Angles are
        /// computed directly in pixel space (not converted from a separately-computed world endpoint)
        /// because <see cref="BoardCamera"/> scales both axes by the same factor with no flip, so an angle
        /// is preserved exactly between world and pixel space; this matches
        /// <see cref="Odyssey.Domain.Geometry.LineOfSight.IsWithinFovCone"/>'s own bearing convention
        /// (<c>Math.Atan2(dy, dx)</c>) so the drawn cone visually matches what the server actually computes.
        /// Recomputed every Refresh() (never cached in screen space), so pan/zoom keeps it correctly placed.
        /// </summary>
        private void RenderTokenVisionIndicator(double centerX, double centerY, double facingDegrees, double fovAngleDegrees, double viewDistance)
        {
            double lengthPixels = viewDistance * _camera.Scale;
            AddVisionIndicatorLine(centerX, centerY, facingDegrees, lengthPixels, "token-vision-facing");

            if (fovAngleDegrees < 360.0)
            {
                AddVisionIndicatorLine(centerX, centerY, facingDegrees - fovAngleDegrees / 2.0, lengthPixels, "token-vision-fov-left");
                AddVisionIndicatorLine(centerX, centerY, facingDegrees + fovAngleDegrees / 2.0, lengthPixels, "token-vision-fov-right");
            }
        }

        private void AddVisionIndicatorLine(double centerX, double centerY, double angleDegrees, double lengthPixels, string name)
        {
            if (_boardArea == null) return;

            double radians = angleDegrees * Math.PI / 180.0;
            double endX = centerX + lengthPixels * Math.Cos(radians);
            double endY = centerY + lengthPixels * Math.Sin(radians);

            var line = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            line.style.position = Position.Absolute;
            line.style.backgroundColor = new StyleColor(new Color(1f, 0.85f, 0.1f, 0.75f));
            PositionSegmentElement(line, centerX, centerY, endX, endY, VisionIndicatorThicknessPixels);
            _boardArea.Add(line);
        }

        private void OnApplyTokenFacingButton(double facingDegrees)
        {
            TokenId? selected = SelectedTokenId;
            if (!selected.HasValue) return;
            TryApplyTokenFacing(selected.Value, facingDegrees);
        }

        private void OnApplyTokenVisionParametersButton(double fovAngleDegrees, double viewDistance)
        {
            TokenId? selected = SelectedTokenId;
            if (!selected.HasValue) return;
            TryApplyTokenVisionParameters(selected.Value, fovAngleDegrees, viewDistance);
        }

        private void OnCheckCoverButton(TokenId targetTokenId)
        {
            TokenId? selected = SelectedTokenId;
            if (!selected.HasValue) return;
            TryCheckCover(selected.Value, targetTokenId);
        }

        /// <summary>
        /// Owner-or-MainGM (server-enforced; the caller's own presentational gate is
        /// <see cref="ShowTokenVisionInspectorIfSelected"/>'s <c>facingEditable</c>). Reads the vision
        /// settings' <see cref="TokenVisionSettingsRecord.Revision"/> fresh immediately before writing --
        /// the exact <c>ODY-S10-112</c> lesson (never trust a render-time-cached revision for a
        /// revision-gated command) applied here for the first time to token vision settings. On success,
        /// records exploration for this token best-effort (never blocking/rolling back a successful facing
        /// change) -- the Block 4 follow-up this task's own facts section names as now due. Public for the
        /// same testability reason as every other command method in this class.
        /// </summary>
        public Result<TokenVisionSettingsRecord> TryApplyTokenFacing(TokenId tokenId, double facingDegrees)
        {
            Result<TokenVisionSettingsRecord> current = _visionRepository.GetVisionSettings(_campaign, tokenId, NewCorrelationId());
            if (current.IsFailure)
            {
                SetStatus("Could not read vision settings: " + current.Error.SafeReasonCode);
                Refresh();
                return current;
            }

            var request = new SetTokenFacingRequest(_campaign, tokenId, facingDegrees, current.Value.Revision, LocalActorUserId, NewCommandId(), NewCorrelationId());
            Result<TokenVisionSettingsRecord> result = TokenVisionService.SetTokenFacing(_visionRepository, _sceneRepository, _campaignRepository, request);
            if (result.IsFailure)
            {
                SetStatus("Set facing denied: " + result.Error.SafeReasonCode);
            }
            else
            {
                SetStatus("Token facing set to " + facingDegrees.ToString("0.0") + " degrees.");
                RecordExplorationBestEffort(tokenId);
            }

            Refresh();
            return result;
        }

        /// <summary>
        /// MainGM-only (server-enforced). Reads the vision settings' <see cref="TokenVisionSettingsRecord.Revision"/>
        /// fresh immediately before writing, the same <c>ODY-S10-112</c> lesson as <see cref="TryApplyTokenFacing"/>.
        /// A single call sets both FOV and view distance together (matching <see cref="TokenVisionService.SetTokenVisionParameters"/>'s
        /// own combined signature) -- when both facing and FOV/range are changed in the panel at once, this
        /// method and <see cref="TryApplyTokenFacing"/> are still two separate calls (task contract section
        /// 2.1), never merged into one, since they map to two separate server commands with different
        /// authorization.
        /// </summary>
        public Result<TokenVisionSettingsRecord> TryApplyTokenVisionParameters(TokenId tokenId, double fovAngleDegrees, double viewDistance)
        {
            Result<TokenVisionSettingsRecord> current = _visionRepository.GetVisionSettings(_campaign, tokenId, NewCorrelationId());
            if (current.IsFailure)
            {
                SetStatus("Could not read vision settings: " + current.Error.SafeReasonCode);
                Refresh();
                return current;
            }

            var request = new SetTokenVisionParametersRequest(_campaign, tokenId, fovAngleDegrees, viewDistance, current.Value.Revision, LocalActorUserId, NewCommandId(), NewCorrelationId());
            Result<TokenVisionSettingsRecord> result = TokenVisionService.SetTokenVisionParameters(_visionRepository, _campaignRepository, request);
            if (result.IsFailure)
            {
                SetStatus("Set vision parameters denied: " + result.Error.SafeReasonCode);
            }
            else
            {
                SetStatus("Token FOV/view distance updated.");
            }

            Refresh();
            return result;
        }

        /// <summary>
        /// <see cref="CoverSuggestionService.SuggestCover"/> performs no authorization at all (used as-is,
        /// task contract section 2.3) -- <paramref name="attackerTokenId"/> is always the inspected token,
        /// <paramref name="targetTokenId"/> always the dropdown's own selection; nothing is persisted, so no
        /// <see cref="Refresh"/> is needed, only the inspector's own result label updates. Public for the
        /// same testability reason as every other command method in this class.
        /// </summary>
        public Result<CoverDegree> TryCheckCover(TokenId attackerTokenId, TokenId targetTokenId)
        {
            var request = new SuggestCoverRequest(_campaign, attackerTokenId, targetTokenId, NewCorrelationId());
            Result<CoverDegree> result = CoverSuggestionService.SuggestCover(_sceneRepository, _obstacleRepository, request);
            if (result.IsFailure)
            {
                _tokenInspectorPresenter?.SetCoverResult("Cover check failed: " + result.Error.SafeReasonCode);
            }
            else
            {
                _tokenInspectorPresenter?.SetCoverResult("Cover: " + result.Value);
            }

            return result;
        }

        // ---- SLICE-10 Block 6 part 1: drawing a wall/door/window ---------------------------------------
        //
        // Real UI Toolkit callbacks are thin wrappers over BeginObstacleDraw/MoveObstacleDraw/EndObstacleDraw,
        // the same testable-public-method shape as every other board gesture in this class. Until the gesture
        // ends, only a visual preview line is shown -- no repository call happens until EndObstacleDraw
        // decides the drag was long enough to commit (task contract section 2.2's own accidental-click guard).

        private void OnToolSelected(BoardTool tool) => SetTool(tool);

        /// <summary>
        /// Switches the active tool. Refused (returns <c>false</c>, no change) while a draw gesture is
        /// already in progress -- task contract section 2.1's own instruction that switching tools mid-gesture
        /// must not abandon it; the gesture must be completed (mouse up) or explicitly cancelled
        /// (<see cref="CancelObstacleDraw"/>, wired to Escape on the board area) first. Public -- see the
        /// class remarks on testability.
        /// </summary>
        public bool SetTool(BoardTool tool)
        {
            if (_obstacleDrawGesture.IsActive) return false;
            _currentTool = tool;
            _toolbarPresenter?.SetActiveTool(tool);
            return true;
        }

        /// <summary>The currently active tool. Exposed for tests.</summary>
        public BoardTool CurrentTool => _currentTool;

        private static ObstacleKind ToObstacleKind(BoardTool tool)
        {
            switch (tool)
            {
                case BoardTool.DrawWall: return ObstacleKind.Wall;
                case BoardTool.DrawDoor: return ObstacleKind.Door;
                case BoardTool.DrawWindow: return ObstacleKind.Window;
                default: throw new ArgumentOutOfRangeException(nameof(tool), tool, "Not a drawing tool.");
            }
        }

        /// <summary>Starts capturing a new obstacle segment at a board-local pixel position. Public -- see the class remarks on testability.</summary>
        public void BeginObstacleDraw(double boardPixelX, double boardPixelY)
        {
            _obstacleDrawGesture.Begin(boardPixelX, boardPixelY);
            ShowObstacleDrawPreview(boardPixelX, boardPixelY, boardPixelX, boardPixelY);
        }

        /// <summary>Feeds a pointer move into the in-progress segment capture, updating the preview line. Public -- see <see cref="BeginObstacleDraw"/>.</summary>
        public void MoveObstacleDraw(double boardPixelX, double boardPixelY)
        {
            _obstacleDrawGesture.Move(boardPixelX, boardPixelY);
            if (_obstacleDrawGesture.TryGetSegment(out double sx, out double sy, out double cx, out double cy))
            {
                ShowObstacleDrawPreview(sx, sy, cx, cy);
            }
        }

        /// <summary>
        /// Ends the segment capture. A movement below <see cref="BoardPointerGesture.DragThresholdPixels"/>
        /// (an accidental click) creates nothing (task contract section 2.2); otherwise converts the two
        /// pixel endpoints to world coordinates through <see cref="BoardCamera"/> and calls
        /// <see cref="ObstacleAuthoringService.CreateObstacle"/> for the tool's own <see cref="ObstacleKind"/>.
        /// The tool itself stays active afterwards (task contract section 2.2: drawing several obstacles in a
        /// row needs no re-selection) -- only an explicit <see cref="SetTool"/> call changes it. Public -- see
        /// <see cref="BeginObstacleDraw"/>.
        /// </summary>
        public void EndObstacleDraw(double boardPixelX, double boardPixelY)
        {
            _obstacleDrawGesture.Move(boardPixelX, boardPixelY);
            bool committed = _obstacleDrawGesture.End(out double sx, out double sy, out double ex, out double ey);
            HideObstacleDrawPreview();
            if (!committed) return;

            TokenPosition start = ToWorldPosition(sx, sy);
            TokenPosition end = ToWorldPosition(ex, ey);
            ObstacleKind kind = ToObstacleKind(_currentTool);
            var request = new CreateObstacleRequest(_campaign, _sceneId, kind, start.X, start.Y, end.X, end.Y, LocalActorUserId, NewCommandId(), NewCorrelationId());
            Result<ObstacleRecord> created = ObstacleAuthoringService.CreateObstacle(_obstacleRepository, _campaignRepository, request);
            if (created.IsFailure)
            {
                // Task contract section 2.2: a denied request (not MainGM, etc.) must not leave a phantom
                // obstacle -- nothing was created, and Refresh() below re-reads the real (unchanged) set.
                SetStatus("Could not create obstacle: " + created.Error.SafeReasonCode);
            }
            else
            {
                SetStatus("Created " + kind + " obstacle.");
            }

            Refresh();
        }

        /// <summary>Aborts the in-progress segment capture without creating anything -- wired to Escape on the board area. Public -- see <see cref="BeginObstacleDraw"/>.</summary>
        public void CancelObstacleDraw()
        {
            _obstacleDrawGesture.Cancel();
            HideObstacleDrawPreview();
        }

        private void ShowObstacleDrawPreview(double x1, double y1, double x2, double y2)
        {
            if (_boardArea == null) return;
            if (_obstacleDrawPreviewElement == null)
            {
                _obstacleDrawPreviewElement = new VisualElement { name = "obstacle-draw-preview", pickingMode = PickingMode.Ignore };
                _obstacleDrawPreviewElement.style.position = Position.Absolute;
                _obstacleDrawPreviewElement.style.backgroundColor = new StyleColor(new Color(1f, 0.85f, 0.1f, 0.6f));
            }

            PositionSegmentElement(_obstacleDrawPreviewElement, x1, y1, x2, y2, ObstacleLineThicknessPixels);
            if (_obstacleDrawPreviewElement.parent != _boardArea) _boardArea.Add(_obstacleDrawPreviewElement);
        }

        private void HideObstacleDrawPreview() => _obstacleDrawPreviewElement?.RemoveFromHierarchy();

        private void OnBoardKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape) return;
            if (!_obstacleDrawGesture.IsActive) return;
            CancelObstacleDraw();
            evt.StopPropagation();
        }

        // ---- ODY-S08-102: board camera (pan/zoom) and click-vs-drag gesture -------------------------
        //
        // The real UI Toolkit callbacks below are thin wrappers over BeginBoardPointerGesture/
        // MoveBoardPointer/EndBoardPointerGesture/ZoomBoard, exactly the same "public method a test can
        // call directly, no simulated event dispatch" shape SelectToken/TryMoveSelectedTokenTo already
        // established (class remarks) -- BoardCamera and BoardPointerGesture are pure C# and need no
        // simulated input at all to test either.

        private void OnBoardPointerDown(PointerDownEvent evt)
        {
            if (_boardArea == null) return;
            Vector2 local = _boardArea.WorldToLocal(evt.position);
            if (!HandleBoardButtonDown(evt.button, local.x, local.y, evt.shiftKey)) return;
            if (evt.button == 0 || evt.button == 1) _boardArea.CapturePointer(evt.pointerId);
            if (evt.button != 0) evt.StopPropagation();
        }

        /// <summary>
        /// The board's mouse-button decision, separated from the UI Toolkit event so a test can drive it with
        /// plain numbers. One gesture at a time: a press while another button's gesture (or a token drag) is
        /// running is refused (returns false) and starts nothing. Left (0): click / selection box; middle (1):
        /// camera pan; right (2): local player marker. Returns whether the press was accepted.
        /// </summary>
        public bool HandleBoardButtonDown(int button, double pixelX, double pixelY, bool shift)
        {
            // ODY-S11-214: any press on the board takes the camera back from autofocus.
            CancelCameraFocus();
            if (_activeBoardButton != -1 || _draggingTokenId.HasValue) return false;
            switch (button)
            {
                case 0:
                    _activeBoardButton = 0;
                    // SLICE-10 Block 6 part 1: which button-0 gesture owns this press is decided once, here,
                    // from the tool active at press time -- see _obstacleDrawActive's own remarks.
                    _obstacleDrawActive = _currentTool != BoardTool.Select;
                    if (_obstacleDrawActive) BeginObstacleDraw(pixelX, pixelY);
                    else BeginBoardPointerGesture(pixelX, pixelY, shift);
                    return true;
                case 1:
                    _activeBoardButton = 1;
                    BeginBoardPan(pixelX, pixelY);
                    return true;
                case 2:
                    PlacePlayerMarker(pixelX, pixelY);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The release counterpart of <see cref="HandleBoardButtonDown"/>: only the button that owns the gesture ends it; any other button's release is ignored (returns false).</summary>
        public bool HandleBoardButtonUp(int button, double pixelX, double pixelY)
        {
            if (_activeBoardButton == -1 || button != _activeBoardButton) return false;
            _activeBoardButton = -1;
            if (button == 0)
            {
                if (_obstacleDrawActive) EndObstacleDraw(pixelX, pixelY);
                else EndBoardPointerGesture(pixelX, pixelY);
            }
            else
            {
                EndBoardPan();
            }

            return true;
        }

        /// <summary>The mouse button that owns the board gesture in progress (0 left, 1 middle), or -1 when none. Exposed for tests.</summary>
        public int ActiveBoardButton => _activeBoardButton;

        private void OnBoardPointerMove(PointerMoveEvent evt)
        {
            if (_boardArea == null || _activeBoardButton == -1 || !_boardArea.HasPointerCapture(evt.pointerId)) return;
            Vector2 local = _boardArea.WorldToLocal(evt.position);
            if (_activeBoardButton == 0)
            {
                if (_obstacleDrawActive) MoveObstacleDraw(local.x, local.y);
                else MoveBoardPointer(local.x, local.y);
            }
            else if (_activeBoardButton == 1) MoveBoardPan(local.x, local.y);
        }

        private void OnBoardPointerUp(PointerUpEvent evt)
        {
            if (_boardArea == null) return;
            Vector2 local = _boardArea.WorldToLocal(evt.position);
            if (!HandleBoardButtonUp(evt.button, local.x, local.y)) return;
            if (_boardArea.HasPointerCapture(evt.pointerId)) _boardArea.ReleasePointer(evt.pointerId);
        }

        private void OnBoardPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            _boardPointerGesture.Cancel();
            _boxGesture.Cancel();
            _activeBoardButton = -1;
            HideBoxElement();
            CancelObstacleDraw();
        }

        private void OnBoardWheel(WheelEvent evt)
        {
            HandleBoardWheel(evt.delta.y, evt.shiftKey, evt.localMousePosition.x, evt.localMousePosition.y);
            evt.StopPropagation();
        }

        /// <summary>
        /// The board's wheel decision, separated from the UI Toolkit event so a test can drive it with plain
        /// numbers. Shift held and a token under the pointer: scales that token (<see cref="ScaleTokenAt"/>),
        /// the camera is untouched. Anything else -- including Shift over empty board -- zooms the camera,
        /// exactly as before ODY-S08-105.
        /// </summary>
        public void HandleBoardWheel(double deltaY, bool shift, double boardPixelX, double boardPixelY)
        {
            if (shift && ScaleTokenAt(boardPixelX, boardPixelY, deltaY)) return;
            ZoomBoardByWheel(deltaY, boardPixelX, boardPixelY);
        }

        private void ZoomBoardByWheel(double deltaY, double anchorPixelX, double anchorPixelY)
        {
            // Scrolling "away from the user" (the standard mouse-wheel-forward notch, reported here as a
            // negative delta.y) zooms in -- the same convention most infinite-canvas tools use (e.g. Google
            // Maps, most browsers' own page zoom). Scrolling "towards the user" (positive delta.y) zooms out.
            double factor = deltaY < 0 ? BoardZoomStepFactor : 1.0 / BoardZoomStepFactor;
            ZoomBoard(factor, anchorPixelX, anchorPixelY);
        }

        /// <summary>~10% per wheel notch, same step as the camera zoom.</summary>
        private const double TokenScaleStepFactor = 1.1;

        /// <summary>
        /// ODY-S08-105: scales the topmost token under a board-local pixel position by one wheel notch
        /// (wheel forward, negative <paramref name="wheelDeltaY"/>, grows), clamped to
        /// [<see cref="TokenRecord.MinScale"/>, <see cref="TokenRecord.MaxScale"/>]. Returns false when no
        /// token is there (nothing consumed). A notch that would leave the scale unchanged (already at a
        /// bound) makes no repository call. The revision is read fresh; the element is resized in place.
        /// Public -- see the class remarks on testability.
        /// </summary>
        public bool ScaleTokenAt(double boardPixelX, double boardPixelY, double wheelDeltaY)
        {
            TokenId? hit = HitTestToken(boardPixelX, boardPixelY);
            if (!hit.HasValue) return false;

            TokenId tokenId = hit.Value;
            string key = tokenId.ToString();
            double current = _tokenScalesByTokenId.TryGetValue(key, out double known) ? known : 1.0;
            double stepped = wheelDeltaY < 0 ? current * TokenScaleStepFactor : current / TokenScaleStepFactor;
            double next = Math.Min(TokenRecord.MaxScale, Math.Max(TokenRecord.MinScale, stepped));
            if (next == current) return true;

            Result<TokenRecord> fresh = _sceneRepository.GetToken(_campaign, tokenId, NewCorrelationId());
            if (fresh.IsFailure)
            {
                SetStatus("Could not scale the token: " + fresh.Error.Code);
                return true;
            }

            Result<TokenRecord> updated = _sceneRepository.SetTokenScale(_campaign, tokenId, next, fresh.Value.Revision, NewCommandId(), NewCorrelationId());
            if (updated.IsFailure)
            {
                SetStatus("Could not scale the token: " + updated.Error.Code);
                return true;
            }

            _tokenScalesByTokenId[key] = updated.Value.Scale;
            if (_tokenElementsByTokenId.TryGetValue(key, out VisualElement? element) && _tokenPositionsByTokenId.TryGetValue(key, out TokenPosition position))
            {
                element.style.width = (float)(TokenSizePixels * updated.Value.Scale);
                element.style.height = (float)(TokenSizePixels * updated.Value.Scale);
                PositionTokenElement(element, position, updated.Value.Scale);
            }

            return true;
        }

        /// <summary>~10% per wheel notch -- a smooth, gradual zoom step; not otherwise significant.</summary>
        private const double BoardZoomStepFactor = 1.1;

        /// <summary>
        /// Starts the left-button gesture on the empty board at a pixel position: a click, or (once the
        /// pointer moves past the drag threshold) a selection box. With <paramref name="shift"/> the box adds
        /// to the current selection instead of replacing it. Public for the same testability reason as
        /// <see cref="SelectToken"/>: a test drives a full down/move/up sequence with plain pixel numbers,
        /// without simulating a single UI Toolkit event.
        /// </summary>
        public void BeginBoardPointerGesture(double pixelX, double pixelY, bool shift = false)
        {
            _boxGesture.Begin(pixelX, pixelY);
            _boxAdditive = shift;
        }

        /// <summary>Feeds a pointer move into the left-button gesture; past the drag threshold, shows and updates the selection box. Public -- see <see cref="BeginBoardPointerGesture"/>.</summary>
        public void MoveBoardPointer(double pixelX, double pixelY)
        {
            if (_boxGesture.Move(pixelX, pixelY)) UpdateBoxElement();
        }

        /// <summary>
        /// Ends the left-button gesture. A drag selects every token whose position lies inside the box (in
        /// world coordinates, through the camera): replacing the selection, or adding to it when the gesture
        /// began with Shift. A click (movement below the threshold) is unchanged from before this task: it
        /// tries to move the single selected token there, or clears a selection of two or more. Public -- see
        /// <see cref="BeginBoardPointerGesture"/>.
        /// </summary>
        public void EndBoardPointerGesture(double pixelX, double pixelY)
        {
            _boxGesture.Move(pixelX, pixelY);
            bool isDrag = _boxGesture.TryGetBox(out double minX, out double minY, out double maxX, out double maxY);
            bool wasClick = _boxGesture.End();
            HideBoxElement();

            if (isDrag)
            {
                SelectTokensInBox(minX, minY, maxX, maxY, _boxAdditive);
                return;
            }

            if (wasClick)
            {
                // SLICE-10 Block 6 part 1: an obstacle hit takes priority over the pre-existing token-move/
                // deselect click behaviour below (a token's own pointer-down already stops propagation before
                // reaching here, so the only real ambiguity is obstacle-vs-empty-board, not obstacle-vs-token).
                ObstacleId? obstacleHit = HitTestObstacle(pixelX, pixelY);
                if (obstacleHit.HasValue)
                {
                    HandleObstacleClick(obstacleHit.Value);
                    return;
                }

                // Clearing here (without its own Refresh()) is enough -- every path below already calls
                // Refresh() unconditionally before this method returns.
                _selectedObstacleId = null;

                // ODY-S08-106: with two or more tokens selected a single destination point cannot place them
                // all, so a click on empty board just clears the selection. With exactly one selected token
                // (or none) the behaviour is unchanged: try to move it there.
                if (_selectedTokenIds.Count >= 2)
                {
                    _selectedTokenIds.Clear();
                    SetStatus("Deselected.");
                    Refresh();
                    return;
                }

                TryMoveSelectedTokenTo(ToWorldPosition(pixelX, pixelY));
            }
        }

        // The box is converted to world coordinates and compared with the tokens' world positions, so pan and
        // zoom are accounted for by BoardCamera rather than by comparing raw pixels.
        private void SelectTokensInBox(double minPixelX, double minPixelY, double maxPixelX, double maxPixelY, bool additive)
        {
            double worldMinX = _camera.FromPixelsX(minPixelX);
            double worldMaxX = _camera.FromPixelsX(maxPixelX);
            double worldMinY = _camera.FromPixelsY(minPixelY);
            double worldMaxY = _camera.FromPixelsY(maxPixelY);

            if (!additive) _selectedTokenIds.Clear();
            int inside = 0;
            foreach (KeyValuePair<string, TokenPosition> entry in _tokenPositionsByTokenId)
            {
                if (entry.Value.X >= worldMinX && entry.Value.X <= worldMaxX && entry.Value.Y >= worldMinY && entry.Value.Y <= worldMaxY)
                {
                    _selectedTokenIds.Add(entry.Key);
                    inside++;
                }
            }

            SetStatus(inside + " token(s) in the box; " + _selectedTokenIds.Count + " selected.");
            Refresh();
        }

        private void UpdateBoxElement()
        {
            if (_boardArea == null) return;
            if (!_boxGesture.TryGetBox(out double minX, out double minY, out double maxX, out double maxY)) return;
            if (_boxElement == null)
            {
                _boxElement = new VisualElement { name = "board-selection-box", pickingMode = PickingMode.Ignore };
                _boxElement.style.position = Position.Absolute;
                _boxElement.style.backgroundColor = new StyleColor(new Color(0.3f, 0.6f, 1f, 0.15f));
                _boxElement.style.borderTopWidth = 1;
                _boxElement.style.borderBottomWidth = 1;
                _boxElement.style.borderLeftWidth = 1;
                _boxElement.style.borderRightWidth = 1;
                var border = new StyleColor(new Color(0.3f, 0.6f, 1f, 0.9f));
                _boxElement.style.borderTopColor = border;
                _boxElement.style.borderBottomColor = border;
                _boxElement.style.borderLeftColor = border;
                _boxElement.style.borderRightColor = border;
            }

            _boxElement.style.left = (float)minX;
            _boxElement.style.top = (float)minY;
            _boxElement.style.width = (float)(maxX - minX);
            _boxElement.style.height = (float)(maxY - minY);
            if (_boxElement.parent != _boardArea) _boardArea.Add(_boxElement);
        }

        private void HideBoxElement() => _boxElement?.RemoveFromHierarchy();

        /// <summary>The selection box element while a box drag is in progress, else <c>null</c>. Exposed for tests.</summary>
        public VisualElement? SelectionBoxElement => _boxElement != null && _boxElement.parent != null ? _boxElement : null;

        // ---- ODY-S08-107: middle-button camera pan --------------------------------------------------

        /// <summary>Starts a camera pan at a board-local pixel position (middle button). Public -- see <see cref="BeginBoardPointerGesture"/>.</summary>
        public void BeginBoardPan(double pixelX, double pixelY)
        {
            CancelCameraFocus(); // ODY-S11-214: manual camera input wins over autofocus
            _boardPointerGesture.Begin(pixelX, pixelY);
        }

        /// <summary>Feeds a pointer move into the pan; once past <see cref="BoardPointerGesture.DragThresholdPixels"/> it pans the camera and repositions the rendered tokens and the marker. Public -- see <see cref="BeginBoardPointerGesture"/>.</summary>
        public void MoveBoardPan(double pixelX, double pixelY)
        {
            if (_boardPointerGesture.Move(pixelX, pixelY, out double deltaX, out double deltaY))
            {
                _camera.Pan(deltaX, deltaY);
                RepositionTokens();
            }
        }

        /// <summary>Ends the pan. A pan has no click equivalent: releasing without ever crossing the threshold does nothing. Public -- see <see cref="BeginBoardPointerGesture"/>.</summary>
        public void EndBoardPan() => _boardPointerGesture.End();

        // ---- ODY-S08-107: right-button local player marker ------------------------------------------
        //
        // LOCAL ONLY: a plain VisualElement plus a world position in memory. Nothing is sent over any
        // network and nothing is written to the repository or database; other participants never see it.
        // Two and a half seconds is long enough to notice and point at, short enough not to clutter the
        // board; a second press replaces the marker immediately. A ring (transparent fill, bordered) is used
        // instead of a filled dot so the token or map under it stays visible.

        /// <summary>Places (or moves) the local marker at a board-local pixel position; it disappears by itself after a short time. Public -- see <see cref="BeginBoardPointerGesture"/>.</summary>
        public void PlacePlayerMarker(double pixelX, double pixelY)
        {
            if (_boardArea == null) return;
            _markerWorldPosition = ToWorldPosition(pixelX, pixelY);
            if (_markerElement == null)
            {
                _markerElement = new VisualElement { name = "board-player-marker", pickingMode = PickingMode.Ignore };
                _markerElement.style.position = Position.Absolute;
                _markerElement.style.width = (float)PlayerMarkerSizePixels;
                _markerElement.style.height = (float)PlayerMarkerSizePixels;
                _markerElement.style.borderTopLeftRadius = (float)(PlayerMarkerSizePixels / 2);
                _markerElement.style.borderTopRightRadius = (float)(PlayerMarkerSizePixels / 2);
                _markerElement.style.borderBottomLeftRadius = (float)(PlayerMarkerSizePixels / 2);
                _markerElement.style.borderBottomRightRadius = (float)(PlayerMarkerSizePixels / 2);
                _markerElement.style.borderTopWidth = 3;
                _markerElement.style.borderBottomWidth = 3;
                _markerElement.style.borderLeftWidth = 3;
                _markerElement.style.borderRightWidth = 3;
                var ring = new StyleColor(new Color(1f, 0.85f, 0.1f, 0.95f));
                _markerElement.style.borderTopColor = ring;
                _markerElement.style.borderBottomColor = ring;
                _markerElement.style.borderLeftColor = ring;
                _markerElement.style.borderRightColor = ring;
            }

            if (_markerElement.parent != _boardArea) _boardArea.Add(_markerElement);
            PositionMarkerElement();

            _markerExpiry?.Pause();
            _markerExpiry = _markerElement.schedule.Execute(ClearPlayerMarker);
            _markerExpiry.ExecuteLater(PlayerMarkerLifetimeMilliseconds);
        }

        /// <summary>Removes the local marker now (also what the expiry timer calls).</summary>
        public void ClearPlayerMarker()
        {
            _markerExpiry?.Pause();
            _markerExpiry = null;
            _markerWorldPosition = null;
            _markerElement?.RemoveFromHierarchy();
        }

        /// <summary>The marker's world position, or <c>null</c> when no marker is showing. Exposed for tests.</summary>
        public TokenPosition? PlayerMarkerWorldPosition => _markerWorldPosition;

        /// <summary>The marker element while it is showing, else <c>null</c>. Exposed for tests.</summary>
        public VisualElement? PlayerMarkerElement => _markerWorldPosition.HasValue ? _markerElement : null;

        private void PositionMarkerElement()
        {
            if (_markerElement == null || !_markerWorldPosition.HasValue) return;
            _markerElement.style.left = (float)(_camera.ToPixelsX(_markerWorldPosition.Value.X) - PlayerMarkerSizePixels / 2);
            _markerElement.style.top = (float)(_camera.ToPixelsY(_markerWorldPosition.Value.Y) - PlayerMarkerSizePixels / 2);
        }

        /// <summary>Zooms the camera to <paramref name="anchorPixelX"/>/<paramref name="anchorPixelY"/> and repositions the already-rendered tokens. Public -- see <see cref="BeginBoardPointerGesture"/>.</summary>
        public void ZoomBoard(double factor, double anchorPixelX, double anchorPixelY)
        {
            CancelCameraFocus(); // ODY-S11-214: manual camera input wins over autofocus
            _camera.Zoom(factor, anchorPixelX, anchorPixelY);
            RepositionTokens();
        }

        /// <summary>The board camera's current state, exposed read-only for tests and any future caller that needs to know the current pan/zoom (task contract section 1.4: this state is never persisted or synced).</summary>
        public BoardCamera Camera => _camera;

        /// <summary>
        /// The board's own root element, exposed so <see cref="AssetPoolPresenter"/>'s real drag-and-drop
        /// wiring can test whether a drop's panel-space position falls within the board at all (via
        /// <c>VisualElement.WorldToLocal</c>/<c>ContainsPoint</c>) before calling <see cref="ApplyDroppedAsset"/>
        /// with the resulting board-local pixel coordinates. Not used by this presenter's own tests, which
        /// call <see cref="ApplyDroppedAsset"/> directly with plain numbers -- the same "real wiring
        /// untested, pure decision logic tested directly" split every other gesture in this presenter
        /// already uses (pointer-down/move/up, wheel).
        /// </summary>
        public VisualElement? BoardArea => _boardArea;

        /// <summary>
        /// ODY-S08-103: applies an asset dragged from the asset pool and dropped at a board-local pixel
        /// position (the same coordinate space <see cref="BeginBoardPointerGesture"/> already uses -- top-left
        /// of the board area is (0,0), unaffected by pan/zoom, which only changes what world position that
        /// pixel maps to). If the point lands on a rendered token, sets that token's portrait
        /// (<see cref="ISceneRepository.SetTokenPortrait"/>); otherwise sets the scene's background
        /// (<see cref="ISceneRepository.SetSceneBackground"/>) -- task contract section 1.4. Both calls read
        /// the current revision immediately before writing (never a cached one), the same pattern
        /// <see cref="TryMoveSelectedTokenTo"/> already uses for <c>MoveToken</c>.
        /// </summary>
        public Result ApplyDroppedAsset(AssetId assetId, double boardPixelX, double boardPixelY)
        {
            if (!assetId.IsValid) throw new ArgumentException("AssetId is required.", nameof(assetId));

            TokenId? targetTokenId = HitTestToken(boardPixelX, boardPixelY);
            if (targetTokenId.HasValue)
            {
                Result<TokenRecord> current = _sceneRepository.GetToken(_campaign, targetTokenId.Value, NewCorrelationId());
                if (current.IsFailure)
                {
                    SetStatus("Drop failed: " + current.Error.SafeReasonCode);
                    return Result.Failure(current.Error);
                }

                Result<TokenRecord> updated = _sceneRepository.SetTokenPortrait(_campaign, targetTokenId.Value, assetId, current.Value.Revision, NewCommandId(), NewCorrelationId());
                if (updated.IsFailure)
                {
                    SetStatus("Drop failed: " + updated.Error.SafeReasonCode);
                    return Result.Failure(updated.Error);
                }

                SetStatus("Set token portrait.");
                Refresh();
                return Result.Success();
            }

            Result<SceneRecord> currentScene = _sceneRepository.GetScene(_campaign, _sceneId, NewCorrelationId());
            if (currentScene.IsFailure)
            {
                SetStatus("Drop failed: " + currentScene.Error.SafeReasonCode);
                return Result.Failure(currentScene.Error);
            }

            Result<SceneRecord> updatedScene = _sceneRepository.SetSceneBackground(_campaign, _sceneId, assetId, currentScene.Value.Revision, NewCommandId(), NewCorrelationId());
            if (updatedScene.IsFailure)
            {
                SetStatus("Drop failed: " + updatedScene.Error.SafeReasonCode);
                return Result.Failure(updatedScene.Error);
            }

            SetStatus("Set scene background.");
            Refresh();
            return Result.Success();
        }

        // Pure math, exactly like the camera itself: a dropped point is "on" a token when it falls inside
        // that token's own current on-screen square (computed from the same world position and camera the
        // renderer used), independent of UI Toolkit layout/worldBound timing.
        private TokenId? HitTestToken(double boardPixelX, double boardPixelY)
        {
            // ODY-S08-105: the square is the token's scaled size, and among overlapping hits the topmost
            // (highest ZOrder) wins -- the one the user actually sees under the pointer.
            TokenId? best = null;
            long bestZ = long.MinValue;
            foreach (KeyValuePair<string, TokenPosition> entry in _tokenPositionsByTokenId)
            {
                double half = TokenSizePixels * (_tokenScalesByTokenId.TryGetValue(entry.Key, out double scale) ? scale : 1.0) / 2.0;
                double centerX = _camera.ToPixelsX(entry.Value.X);
                double centerY = _camera.ToPixelsY(entry.Value.Y);
                if (boardPixelX >= centerX - half && boardPixelX <= centerX + half && boardPixelY >= centerY - half && boardPixelY <= centerY + half)
                {
                    long z = _tokenZOrdersByTokenId.TryGetValue(entry.Key, out long known) ? known : 0;
                    if (!best.HasValue || z > bestZ)
                    {
                        best = TokenId.Parse(entry.Key);
                        bestZ = z;
                    }
                }
            }

            return best;
        }

        // Repositions the already-rendered token elements from their last-known world position (no DB
        // read, no texture-cache lookup, no DOM teardown) -- called after every pan/zoom so dragging and
        // scrolling stay smooth. A full Refresh() (which this deliberately is not) is still what re-reads
        // token positions from the repository.
        private void RepositionTokens()
        {
            PositionMarkerElement();
            foreach (KeyValuePair<string, TokenPosition> entry in _tokenPositionsByTokenId)
            {
                if (_tokenElementsByTokenId.TryGetValue(entry.Key, out VisualElement? tokenElement))
                {
                    TokenPosition drawn = _tokenMotion.TryGetDisplayed(entry.Key, out TokenPosition animated) ? animated : entry.Value;
                    PositionTokenElement(tokenElement, drawn, ScaleOf(entry.Key));
                }
            }
        }

        /// <summary>ODY-S11-211: whether a token is currently easing towards a position changed elsewhere. Exposed for tests.</summary>
        public bool IsTokenAnimating(TokenId tokenId) => _tokenMotion.IsAnimating(tokenId.ToString());

        /// <summary>
        /// ODY-S11-211: steps running token animations by <paramref name="elapsedMs"/> and redraws them. Called by the
        /// board area's scheduler at runtime; public so tests step it deterministically. Returns whether any still runs.
        /// </summary>
        public bool AdvanceTokenMotion(double elapsedMs)
        {
            bool running = _tokenMotion.Advance(elapsedMs);
            RepositionTokens();
            if (!running)
            {
                // A fresh item next time (not Resume), so its first tick delta starts from when it is scheduled.
                _tokenMotionTicker?.Pause();
                _tokenMotionTicker = null;
            }

            return running;
        }

        /// <summary>
        /// ODY-S11-214: at a turn change, eases the camera so the acting character's token is in frame. Does nothing
        /// when the token is already comfortably in frame, is not on this board, or is hidden from the local actor
        /// (focusing would reveal its position). Zoom is unchanged; any manual pan/zoom/press cancels the move.
        /// </summary>
        public BoardFocusOutcome FocusOnCharacter(CharacterId characterId)
        {
            if (!_tokenKeysByCharacterId.TryGetValue(characterId.ToString(), out string? key) || !_tokenPositionsByTokenId.TryGetValue(key, out TokenPosition position)) return BoardFocusOutcome.NotOnBoard;
            if (!_visibleTokenKeys.Contains(key)) return BoardFocusOutcome.Hidden;
            double width = CurrentBoardWidthPixels();
            double height = CurrentBoardHeightPixels();
            if (BoardCameraFocus.IsInFrame(_camera, position, width, height)) return BoardFocusOutcome.AlreadyInFrame;
            _cameraFocus.Start(_camera, position, width, height);
            if (_boardArea != null && !_disposed && _cameraFocusTicker == null)
            {
                _cameraFocusTicker = _boardArea.schedule.Execute(timer => AdvanceCameraFocus(timer.deltaTime)).Every(OdyMotion.FrameIntervalMs);
            }

            return BoardFocusOutcome.Started;
        }

        /// <summary>ODY-S11-214: whether the camera is currently easing towards a token. Exposed for tests.</summary>
        public bool IsCameraFocusing => _cameraFocus.IsActive;

        /// <summary>
        /// ODY-S11-214: steps the camera move by <paramref name="elapsedMs"/>. Called by the board area's scheduler at
        /// runtime; public so tests step it deterministically. Returns whether it still runs.
        /// </summary>
        public bool AdvanceCameraFocus(double elapsedMs)
        {
            if (!_cameraFocus.IsActive)
            {
                StopCameraFocusTicker();
                return false;
            }

            bool running = _cameraFocus.Advance(_camera, elapsedMs);
            RepositionTokens();
            if (!running)
            {
                StopCameraFocusTicker();
                // Same follow-up as the shell's initial centering: one re-render so obstacles and fog follow the camera.
                if (!_disposed) Refresh();
            }

            return running;
        }

        private void CancelCameraFocus()
        {
            _cameraFocus.Cancel();
            StopCameraFocusTicker();
        }

        private void StopCameraFocusTicker()
        {
            _cameraFocusTicker?.Pause();
            _cameraFocusTicker = null;
        }

        private void EnsureTokenMotionTicking()
        {
            if (!_tokenMotion.AnyActive || _boardArea == null || _disposed || _tokenMotionTicker != null) return;
            _tokenMotionTicker = _boardArea.schedule.Execute(timer => AdvanceTokenMotion(timer.deltaTime)).Every(OdyMotion.FrameIntervalMs);
        }

        private double ScaleOf(string key) => _tokenScalesByTokenId.TryGetValue(key, out double scale) ? scale : 1.0;

        private void PositionTokenElement(VisualElement tokenElement, TokenPosition position, double scale)
        {
            double half = TokenSizePixels * scale / 2;
            tokenElement.style.left = (float)(_camera.ToPixelsX(position.X) - half);
            tokenElement.style.top = (float)(_camera.ToPixelsY(position.Y) - half);
        }

        private TokenPosition ToWorldPosition(double pixelX, double pixelY) => new TokenPosition(_camera.FromPixelsX(pixelX), _camera.FromPixelsY(pixelY));

        private void SetStatus(string text)
        {
            if (_statusLabel != null) _statusLabel.text = text;
        }

        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static CorrelationId NewCorrelationId() => CorrelationId.Parse("corr_" + Guid.NewGuid().ToString("N"));
    }

    internal static class BoardScreenErrors
    {
        private static readonly CorrelationId PlaceholderCorrelationId = CorrelationId.Parse("corr_00000000000000000000000000000000");

        internal static Error RenderFailed() => Error.Create(
            ErrorCodes.ApplicationInternalUnexpected,
            ErrorCategory.Internal,
            SafeReasonCode.UnexpectedError,
            UserMessageKey.Parse("errors.board_screen.render_failed"),
            RetryDirective.DoNotRetry,
            PlaceholderCorrelationId);

        /// <summary>ODY-S08-101: the asset bytes passed the persistence-layer integrity check but Unity's own ImageConversion.LoadImage could not decode them.</summary>
        internal static Error TextureDecodeFailed() => Error.Create(
            ErrorCodes.ApplicationValidationInvalid,
            ErrorCategory.Integrity,
            SafeReasonCode.DataCorrupted,
            UserMessageKey.Parse("errors.board_screen.texture_decode_failed"),
            RetryDirective.DoNotRetry,
            PlaceholderCorrelationId);

        internal static Error NoTokenSelected() => Error.Create(
            ErrorCodes.ApplicationValidationInvalid,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.board_screen.no_token_selected"),
            RetryDirective.DoNotRetry,
            PlaceholderCorrelationId);
    }

    /// <summary>
    /// ODY-UI-01-002: creates a throwaway, self-contained demo campaign
    /// (one scene, two tokens with distinct controllers) so a human can
    /// press Play and immediately have something to click, without a manual
    /// operator setup step (task contract section 3's decision). Not reused
    /// by any later task's own persistence work (<c>ODY-UI-01-006</c>) --
    /// this is a fresh campaign every run, not the "save and reopen" flow
    /// that task owns.
    /// </summary>
    public static class BoardScreenDemoCampaign
    {
        public static Result<BoardScreenDemoCampaignHandle> CreateFresh(string rootDirectory, Odyssey.Application.Time.IWallClock clock)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("Root directory is required.", nameof(rootDirectory));
            if (clock == null) throw new ArgumentNullException(nameof(clock));

            UserId localActor = UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
            UserId otherPlayer = UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

            return CreateFresh(rootDirectory, clock, localActor, otherPlayer);
        }

        public static Result<BoardScreenDemoCampaignHandle> CreateFresh(string rootDirectory, Odyssey.Application.Time.IWallClock clock, UserId localActor, UserId otherPlayer)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("Root directory is required.", nameof(rootDirectory));
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            if (!localActor.IsValid) throw new ArgumentException("Local actor UserId is required.", nameof(localActor));
            if (!otherPlayer.IsValid) throw new ArgumentException("Other player UserId is required.", nameof(otherPlayer));

            var campaignRepository = new Odyssey.Persistence.Sqlite.SqliteCampaignRepository(clock);
            var createRequest = new CreateCampaignRequest(rootDirectory, "SLICE-UI-01 Trial Campaign", "ruleset.core", "1.0.0", "0.1.0", global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost());
            CorrelationId correlationId = CorrelationId.Parse("corr_" + Guid.NewGuid().ToString("N"));
            Result<CampaignHandle> created = campaignRepository.Create(createRequest, NewCommandId(), correlationId);
            if (created.IsFailure) return Result<BoardScreenDemoCampaignHandle>.Failure(created.Error);

            var sceneRepository = new Odyssey.Persistence.Sqlite.SqliteSceneRepository(clock);
            Result<SceneRecord> scene = sceneRepository.CreateScene(created.Value, "Trial Scene", NewCommandId(), correlationId);
            if (scene.IsFailure) return Result<BoardScreenDemoCampaignHandle>.Failure(scene.Error);

            Result<TokenRecord> localToken = sceneRepository.CreateToken(created.Value, scene.Value.SceneId, new TokenPosition(0, 0), localActor, NewCommandId(), correlationId);
            if (localToken.IsFailure) return Result<BoardScreenDemoCampaignHandle>.Failure(localToken.Error);

            Result<TokenRecord> otherToken = sceneRepository.CreateToken(created.Value, scene.Value.SceneId, new TokenPosition(3, 2), otherPlayer, NewCommandId(), correlationId);
            if (otherToken.IsFailure) return Result<BoardScreenDemoCampaignHandle>.Failure(otherToken.Error);

            return Result<BoardScreenDemoCampaignHandle>.Success(new BoardScreenDemoCampaignHandle(created.Value, scene.Value.SceneId, localActor, localToken.Value.TokenId, otherToken.Value.TokenId, campaignRepository));
        }

        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
    }

    public sealed class BoardScreenDemoCampaignHandle
    {
        public BoardScreenDemoCampaignHandle(CampaignHandle campaign, SceneId sceneId, UserId localActorUserId, TokenId localToken, TokenId otherToken, ICampaignRepository campaignRepository)
        {
            Campaign = campaign;
            CampaignRepository = campaignRepository;
            SceneId = sceneId;
            LocalActorUserId = localActorUserId;
            LocalToken = localToken;
            OtherToken = otherToken;
        }

        public CampaignHandle Campaign { get; }

        /// <summary>ODY-S10-101: the repository that created the demo campaign and holds its membership (the host is its MainGM).</summary>
        public ICampaignRepository CampaignRepository { get; }
        public SceneId SceneId { get; }
        public UserId LocalActorUserId { get; }
        public TokenId LocalToken { get; }
        public TokenId OtherToken { get; }
    }
}
