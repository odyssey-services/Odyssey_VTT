using System;
using System.Collections.Generic;
using Odyssey.Application.Board;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
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
        private readonly SceneId _sceneId;
        private readonly bool _includeRoleSelector;
        // ODY-S08-102: replaces the old fixed OriginOffsetPixels/PixelsPerUnit transform. Purely local,
        // per-presenter, ephemeral state (task contract section 1.4: no persistence, no sync).
        private readonly BoardCamera _camera = new BoardCamera();
        private readonly BoardPointerGesture _boardPointerGesture = new BoardPointerGesture();
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
        private TokenId? _selectedTokenId;
        private bool _disposed;

        public BoardScreenPresenter(UIDocument document, ISceneRepository sceneRepository, CampaignHandle campaign, SceneId sceneId, UserId localActorUserId)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _sceneRepository = sceneRepository ?? throw new ArgumentNullException(nameof(sceneRepository));
            _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));
            if (!localActorUserId.IsValid) throw new ArgumentException("LocalActorUserId is required.", nameof(localActorUserId));
            _sceneId = sceneId;
            _includeRoleSelector = true;
            LocalActorUserId = localActorUserId;
        }

        public BoardScreenPresenter(UIDocument document, ISceneRepository sceneRepository, CampaignHandle campaign, SceneId sceneId, RoleSelection roleSelection, PresentationRuntime presentationRuntime, bool includeRoleSelector = true)
            : this(document, sceneRepository, campaign, sceneId, (roleSelection ?? throw new ArgumentNullException(nameof(roleSelection))).ActorUserId)
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

        /// <summary>Whether the current local actor holds the MainGM baseline role. Settable -- see class remarks.</summary>
        public bool LocalActorIsMainGm { get; set; }

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
            _textureCache.Dispose();
            _disposed = true;
        }

        private void BuildView(VisualElement? parent)
        {
            VisualElement root = _document.rootVisualElement;
            VisualElement appRoot = parent ?? root.Q<VisualElement>("odyssey-root") ?? root;
            appRoot.Clear();
            appRoot.AddToClassList("app-root");

            Label title = new Label("Odyssey Board Screen (trial)") { name = "board-title" };
            appRoot.Add(title);

            if (_includeRoleSelector && _roleSelection != null && _presentationRuntime != null)
            {
                _roleSelectorPresenter = new RoleSelectorPresenter(_roleSelection, _presentationRuntime);
                appRoot.Add(_roleSelectorPresenter.BuildView());
            }

            _statusLabel = new Label { name = "board-status" };
            appRoot.Add(_statusLabel);

            _boardArea = new VisualElement { name = "board-area" };
            _boardArea.style.position = Position.Relative;
            _boardArea.style.width = 440;
            _boardArea.style.height = 440;
            _boardArea.style.marginTop = 8;
            _boardArea.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.14f));
            // ODY-S08-102: pointer-down/move/up (not ClickEvent) drives the board's own click-vs-pan
            // disambiguation (BoardPointerGesture) -- see the class remarks and OnBoardPointerDown/Move/Up.
            _boardArea.RegisterCallback<PointerDownEvent>(OnBoardPointerDown);
            _boardArea.RegisterCallback<PointerMoveEvent>(OnBoardPointerMove);
            _boardArea.RegisterCallback<PointerUpEvent>(OnBoardPointerUp);
            _boardArea.RegisterCallback<PointerCaptureOutEvent>(OnBoardPointerCaptureOut);
            _boardArea.RegisterCallback<WheelEvent>(OnBoardWheel);
            appRoot.Add(_boardArea);
        }

        public Result Refresh()
        {
            Result<IReadOnlyList<TokenRecord>> tokens = _sceneRepository.ListTokens(_campaign, _sceneId, NewCorrelationId());
            if (tokens.IsFailure)
            {
                SetStatus("Failed to list tokens: " + tokens.Error.SafeReasonCode);
                return Result.Failure(tokens.Error);
            }

            Error? backgroundError = ApplySceneBackground();
            Error? tokenAssetError = RenderTokens(tokens.Value);

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

        private Error? RenderTokens(IReadOnlyList<TokenRecord> tokens)
        {
            if (_boardArea == null) return null;
            _boardArea.Clear();
            _tokenElementsByTokenId.Clear();
            _tokenPositionsByTokenId.Clear();
            _tokenGesturesByTokenId.Clear();
            Error? firstAssetError = null;

            foreach (TokenRecord token in tokens)
            {
                VisualElement tokenElement = new VisualElement { name = "token-" + token.TokenId };
                tokenElement.AddToClassList("board-token");
                tokenElement.style.position = Position.Absolute;
                tokenElement.style.width = (float)TokenSizePixels;
                tokenElement.style.height = (float)TokenSizePixels;
                PositionTokenElement(tokenElement, token.Position);
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

                bool isSelected = _selectedTokenId.HasValue && _selectedTokenId.Value.Equals(token.TokenId);
                tokenElement.style.borderTopWidth = isSelected ? 3 : 1;
                tokenElement.style.borderBottomWidth = isSelected ? 3 : 1;
                tokenElement.style.borderLeftWidth = isSelected ? 3 : 1;
                tokenElement.style.borderRightWidth = isSelected ? 3 : 1;

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
            }

            return firstAssetError;
        }

        private Color TokenColor(TokenRecord token, out bool isLocalActorControlled)
        {
            isLocalActorControlled = token.ControllerUserId.Equals(LocalActorUserId);
            return isLocalActorControlled ? new Color(0.25f, 0.65f, 0.95f) : new Color(0.75f, 0.35f, 0.30f);
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
        public TokenId? SelectedTokenId => _selectedTokenId;

        public void SelectToken(TokenId tokenId)
        {
            if (_selectedTokenId.HasValue && _selectedTokenId.Value.Equals(tokenId))
            {
                _selectedTokenId = null;
                SetStatus("Deselected.");
                Refresh();
                return;
            }

            _selectedTokenId = tokenId;
            SetStatus("Selected token " + tokenId + ".");
            Refresh();
        }

        /// <summary>
        /// Attempts to move the currently-selected token to <paramref name="destination"/>
        /// via <see cref="BoardMovementService.MoveToken"/>, using the
        /// current <see cref="LocalActorUserId"/>/<see cref="LocalActorIsMainGm"/>.
        /// Public for the same testability reason as <see cref="SelectToken"/>.
        /// </summary>
        public Result<TokenRecord> TryMoveSelectedTokenTo(TokenPosition destination)
        {
            if (!_selectedTokenId.HasValue)
            {
                SetStatus("Select a token first.");
                return Result<TokenRecord>.Failure(BoardScreenErrors.NoTokenSelected());
            }

            TokenId tokenId = _selectedTokenId.Value;
            Result<TokenRecord> current = _sceneRepository.GetToken(_campaign, tokenId, NewCorrelationId());
            if (current.IsFailure)
            {
                SetStatus("Move failed: " + current.Error.SafeReasonCode);
                _selectedTokenId = null;
                Refresh();
                return current;
            }

            var request = new MoveTokenRequest(_campaign, LocalActorUserId, LocalActorIsMainGm, tokenId, destination, current.Value.Revision, NewCommandId(), NewCorrelationId());
            Result<TokenRecord> moved = BoardMovementService.MoveToken(_sceneRepository, request);

            _selectedTokenId = null;
            if (moved.IsFailure)
            {
                SetStatus("Move denied: " + moved.Error.SafeReasonCode);
            }
            else
            {
                SetStatus("Moved token " + tokenId + " to (" + moved.Value.Position.X.ToString("0.0") + ", " + moved.Value.Position.Y.ToString("0.0") + ").");
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
            Result<TokenRecord> current = _sceneRepository.GetToken(_campaign, tokenId, NewCorrelationId());
            if (current.IsFailure)
            {
                SetStatus("Move failed: " + current.Error.SafeReasonCode);
                Refresh();
                return current;
            }

            var request = new MoveTokenRequest(_campaign, LocalActorUserId, LocalActorIsMainGm, tokenId, destination, current.Value.Revision, NewCommandId(), NewCorrelationId());
            Result<TokenRecord> moved = BoardMovementService.MoveToken(_sceneRepository, request);

            if (moved.IsFailure)
            {
                SetStatus("Move denied: " + moved.Error.SafeReasonCode);
            }
            else
            {
                SetStatus("Moved token " + tokenId + " to (" + moved.Value.Position.X.ToString("0.0") + ", " + moved.Value.Position.Y.ToString("0.0") + ").");
            }

            Refresh();
            return moved;
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
            evt.StopPropagation();
            if (_boardArea == null) return;
            tokenElement.CapturePointer(evt.pointerId);
            Vector2 boardLocal = _boardArea.WorldToLocal(evt.position);
            BeginTokenDrag(tokenId, boardLocal.x, boardLocal.y);
        }

        private void OnTokenPointerMove(PointerMoveEvent evt, VisualElement tokenElement, TokenId tokenId)
        {
            if (_boardArea == null || !tokenElement.HasPointerCapture(evt.pointerId)) return;
            Vector2 boardLocal = _boardArea.WorldToLocal(evt.position);
            MoveTokenDrag(tokenId, boardLocal.x, boardLocal.y);
        }

        private void OnTokenPointerUp(PointerUpEvent evt, VisualElement tokenElement, TokenId tokenId)
        {
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
                Refresh();
            }
        }

        /// <summary>Starts tracking a token's own drag gesture. Public -- see the class remarks on testability.</summary>
        public void BeginTokenDrag(TokenId tokenId, double boardPixelX, double boardPixelY)
        {
            if (!_tokenGesturesByTokenId.TryGetValue(tokenId.ToString(), out BoardPointerGesture? gesture)) return;
            gesture.Begin(boardPixelX, boardPixelY);
            _draggingTokenId = tokenId;
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
                TokenPosition worldPosition = ToWorldPosition(boardPixelX, boardPixelY);
                _tokenPositionsByTokenId[key] = worldPosition;
                if (_tokenElementsByTokenId.TryGetValue(key, out VisualElement? tokenElement))
                {
                    PositionTokenElement(tokenElement, worldPosition);
                }
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

            if (wasClick)
            {
                SelectToken(tokenId);
                return;
            }

            TokenPosition destination = ToWorldPosition(boardPixelX, boardPixelY);
            TryMoveTokenTo(tokenId, destination);
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
            _boardArea.CapturePointer(evt.pointerId);
            BeginBoardPointerGesture(evt.localPosition.x, evt.localPosition.y);
        }

        private void OnBoardPointerMove(PointerMoveEvent evt)
        {
            if (_boardArea == null || !_boardArea.HasPointerCapture(evt.pointerId)) return;
            MoveBoardPointer(evt.localPosition.x, evt.localPosition.y);
        }

        private void OnBoardPointerUp(PointerUpEvent evt)
        {
            if (_boardArea == null) return;
            if (_boardArea.HasPointerCapture(evt.pointerId)) _boardArea.ReleasePointer(evt.pointerId);
            EndBoardPointerGesture(evt.localPosition.x, evt.localPosition.y);
        }

        private void OnBoardPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            _boardPointerGesture.Cancel();
        }

        private void OnBoardWheel(WheelEvent evt)
        {
            // Scrolling "away from the user" (the standard mouse-wheel-forward notch, reported here as a
            // negative delta.y) zooms in -- the same convention most infinite-canvas tools use (e.g. Google
            // Maps, most browsers' own page zoom). Scrolling "towards the user" (positive delta.y) zooms out.
            double factor = evt.delta.y < 0 ? BoardZoomStepFactor : 1.0 / BoardZoomStepFactor;
            ZoomBoard(factor, evt.localMousePosition.x, evt.localMousePosition.y);
            evt.StopPropagation();
        }

        /// <summary>~10% per wheel notch -- a smooth, gradual zoom step; not otherwise significant.</summary>
        private const double BoardZoomStepFactor = 1.1;

        /// <summary>
        /// Starts tracking the board's own pointer gesture at a pixel position. Public for the same
        /// testability reason as <see cref="SelectToken"/>: a test drives a full down/move/up sequence with
        /// plain pixel numbers, exactly reproducing the click-vs-drag disambiguation task contract section
        /// 1.2 requires, without simulating a single UI Toolkit event.
        /// </summary>
        public void BeginBoardPointerGesture(double pixelX, double pixelY) => _boardPointerGesture.Begin(pixelX, pixelY);

        /// <summary>Feeds a pointer move into the board's gesture; once the drag threshold is crossed, pans the camera and repositions the already-rendered tokens. Public -- see <see cref="BeginBoardPointerGesture"/>.</summary>
        public void MoveBoardPointer(double pixelX, double pixelY)
        {
            if (_boardPointerGesture.Move(pixelX, pixelY, out double deltaX, out double deltaY))
            {
                _camera.Pan(deltaX, deltaY);
                RepositionTokens();
            }
        }

        /// <summary>
        /// Ends the board's gesture. If it never crossed the drag threshold, dispatches the pointer-up
        /// position as an ordinary click -- <see cref="TryMoveSelectedTokenTo"/>, unchanged, exactly as
        /// the old <c>ClickEvent</c>-driven <c>OnBoardAreaClicked</c> did. Public -- see
        /// <see cref="BeginBoardPointerGesture"/>.
        /// </summary>
        public void EndBoardPointerGesture(double pixelX, double pixelY)
        {
            if (_boardPointerGesture.End())
            {
                TryMoveSelectedTokenTo(ToWorldPosition(pixelX, pixelY));
            }
        }

        /// <summary>Zooms the camera to <paramref name="anchorPixelX"/>/<paramref name="anchorPixelY"/> and repositions the already-rendered tokens. Public -- see <see cref="BeginBoardPointerGesture"/>.</summary>
        public void ZoomBoard(double factor, double anchorPixelX, double anchorPixelY)
        {
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
            double half = TokenSizePixels / 2.0;
            foreach (KeyValuePair<string, TokenPosition> entry in _tokenPositionsByTokenId)
            {
                double centerX = _camera.ToPixelsX(entry.Value.X);
                double centerY = _camera.ToPixelsY(entry.Value.Y);
                if (boardPixelX >= centerX - half && boardPixelX <= centerX + half && boardPixelY >= centerY - half && boardPixelY <= centerY + half)
                {
                    return TokenId.Parse(entry.Key);
                }
            }

            return null;
        }

        // Repositions the already-rendered token elements from their last-known world position (no DB
        // read, no texture-cache lookup, no DOM teardown) -- called after every pan/zoom so dragging and
        // scrolling stay smooth. A full Refresh() (which this deliberately is not) is still what re-reads
        // token positions from the repository.
        private void RepositionTokens()
        {
            foreach (KeyValuePair<string, TokenPosition> entry in _tokenPositionsByTokenId)
            {
                if (_tokenElementsByTokenId.TryGetValue(entry.Key, out VisualElement? tokenElement))
                {
                    PositionTokenElement(tokenElement, entry.Value);
                }
            }
        }

        private void PositionTokenElement(VisualElement tokenElement, TokenPosition position)
        {
            tokenElement.style.left = (float)(_camera.ToPixelsX(position.X) - TokenSizePixels / 2);
            tokenElement.style.top = (float)(_camera.ToPixelsY(position.Y) - TokenSizePixels / 2);
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
            var createRequest = new CreateCampaignRequest(rootDirectory, "SLICE-UI-01 Trial Campaign", "ruleset.core", "1.0.0", "0.1.0");
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

            return Result<BoardScreenDemoCampaignHandle>.Success(new BoardScreenDemoCampaignHandle(created.Value, scene.Value.SceneId, localActor, localToken.Value.TokenId, otherToken.Value.TokenId));
        }

        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
    }

    public sealed class BoardScreenDemoCampaignHandle
    {
        public BoardScreenDemoCampaignHandle(CampaignHandle campaign, SceneId sceneId, UserId localActorUserId, TokenId localToken, TokenId otherToken)
        {
            Campaign = campaign;
            SceneId = sceneId;
            LocalActorUserId = localActorUserId;
            LocalToken = localToken;
            OtherToken = otherToken;
        }

        public CampaignHandle Campaign { get; }
        public SceneId SceneId { get; }
        public UserId LocalActorUserId { get; }
        public TokenId LocalToken { get; }
        public TokenId OtherToken { get; }
    }
}
