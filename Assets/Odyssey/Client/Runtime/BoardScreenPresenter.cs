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
        private const double PixelsPerUnit = 40.0;
        private const double TokenSizePixels = 28.0;
        private const double OriginOffsetPixels = 220.0;

        private readonly UIDocument _document;
        private readonly ISceneRepository _sceneRepository;
        private readonly CampaignHandle _campaign;
        private readonly SceneId _sceneId;
        private readonly bool _includeRoleSelector;
        private readonly Dictionary<string, VisualElement> _tokenElementsByTokenId = new Dictionary<string, VisualElement>(StringComparer.Ordinal);
        // ODY-S08-101: decoded textures for the presenter's lifetime, keyed by AssetId. AssetId is
        // minted fresh by every RegisterAsset (AssetId.NewId), so an id never changes content and no
        // revision-based invalidation is needed. Only successful loads are cached; a failed load is
        // retried on the next Refresh.
        private readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
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
            foreach (Texture2D texture in _textureCache.Values) DestroyTexture(texture);
            _textureCache.Clear();
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
            _boardArea.RegisterCallback<ClickEvent>(OnBoardAreaClicked);
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

        private Result<Texture2D> LoadTexture(AssetId assetId)
        {
            string key = assetId.ToString();
            if (_textureCache.TryGetValue(key, out Texture2D? cached) && cached != null)
            {
                return Result<Texture2D>.Success(cached);
            }

            Result<byte[]> content = _sceneRepository.ReadAssetContent(_campaign, assetId, NewCorrelationId());
            if (content.IsFailure)
            {
                return Result<Texture2D>.Failure(content.Error);
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, content.Value))
            {
                DestroyTexture(texture);
                return Result<Texture2D>.Failure(BoardScreenErrors.TextureDecodeFailed());
            }

            _textureCache[key] = texture;
            return Result<Texture2D>.Success(texture);
        }

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

        private static void DestroyTexture(Texture2D texture)
        {
            if (texture == null) return;
            if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(texture);
            else UnityEngine.Object.DestroyImmediate(texture);
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
            Error? firstAssetError = null;

            foreach (TokenRecord token in tokens)
            {
                VisualElement tokenElement = new VisualElement { name = "token-" + token.TokenId };
                tokenElement.AddToClassList("board-token");
                tokenElement.style.position = Position.Absolute;
                tokenElement.style.width = (float)TokenSizePixels;
                tokenElement.style.height = (float)TokenSizePixels;
                tokenElement.style.left = ToPixels(token.Position.X);
                tokenElement.style.top = ToPixels(token.Position.Y);
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

                TokenId capturedTokenId = token.TokenId;
                tokenElement.RegisterCallback<ClickEvent>(evt =>
                {
                    SelectToken(capturedTokenId);
                    evt.StopPropagation();
                });

                _boardArea.Add(tokenElement);
                _tokenElementsByTokenId[token.TokenId.ToString()] = tokenElement;
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

        private void OnBoardAreaClicked(ClickEvent evt)
        {
            Vector2 localPosition = evt.localPosition;
            TokenPosition destination = FromPixels(localPosition.x, localPosition.y);
            TryMoveSelectedTokenTo(destination);
        }

        private void SetStatus(string text)
        {
            if (_statusLabel != null) _statusLabel.text = text;
        }

        private static float ToPixels(double unit) => (float)(OriginOffsetPixels + unit * PixelsPerUnit - TokenSizePixels / 2);

        private static TokenPosition FromPixels(double pixelX, double pixelY)
        {
            double x = (pixelX - OriginOffsetPixels) / PixelsPerUnit;
            double y = (pixelY - OriginOffsetPixels) / PixelsPerUnit;
            return new TokenPosition(x, y);
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
