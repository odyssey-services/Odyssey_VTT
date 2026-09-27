using System;
using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    /// <summary>
    /// ODY-S08-103: the campaign's asset pool -- a <see cref="ScrollView"/> of thumbnails (by exact
    /// precedent of <see cref="GameLogPresenter"/>'s own <see cref="ScrollView"/>-plus-one-child-per-row
    /// pattern, not a new way of building a list), a button that opens a native "Open File" dialog
    /// (through <see cref="NativeFileDialog"/>, this codebase's single point of contact with the vendored
    /// third-party library) and registers the chosen file, and in-app dragging of a pool item onto the
    /// board (<see cref="BoardScreenPresenter"/>), which decides whether the drop targets a token's
    /// portrait or the scene's background (<see cref="BoardScreenPresenter.ApplyDroppedAsset"/>).
    ///
    /// The upload flow's real entry point is <see cref="UploadFromDialog"/>, a thin wrapper the "Upload
    /// Image" button calls -- public for the same testability reason
    /// <see cref="BoardScreenPresenter.SelectToken"/> already established: a test constructs this presenter
    /// with a fake <c>openImageFile</c> delegate (returning a real path, or <c>null</c> for "the user
    /// cancelled") and calls <see cref="UploadFromDialog"/> directly, never opening a real dialog. The
    /// in-app drag's own pointer wiring (<see cref="OnItemPointerDown"/>/<see cref="OnItemPointerMove"/>/
    /// <see cref="OnItemPointerUp"/>) is a thin wrapper the same way <see cref="BoardScreenPresenter"/>'s own
    /// pointer/wheel callbacks are: real UI Toolkit event plumbing, not itself unit-tested (this codebase's
    /// own established precedent -- no test anywhere simulates a UI Toolkit pointer event); the drop
    /// decision it eventually calls (<see cref="BoardScreenPresenter.ApplyDroppedAsset"/>) is pure logic and
    /// is tested directly, with plain board-local pixel numbers.
    /// </summary>
    public sealed class AssetPoolPresenter : IDisposable
    {
        private const double ItemSizePixels = 48.0;

        private readonly UIDocument _document;
        private readonly ISceneRepository _sceneRepository;
        private readonly CampaignHandle _campaign;
        private readonly BoardScreenPresenter _board;
        private readonly Func<string?> _openImageFile;
        private readonly AssetTextureCache _textureCache = new AssetTextureCache();
        private readonly List<AssetManifestEntryRecord> _assets = new List<AssetManifestEntryRecord>();
        private ScrollView? _list;
        private Label? _status;
        private VisualElement? _dragGhost;
        private AssetId? _draggingAssetId;
        private bool _disposed;

        /// <summary>
        /// <paramref name="openImageFile"/> defaults to <see cref="NativeFileDialog.OpenImageFile"/>
        /// (a real native dialog); tests supply their own delegate instead, exactly like every other
        /// injected dependency this codebase's presenters already take through their constructor.
        /// </summary>
        public AssetPoolPresenter(UIDocument document, ISceneRepository sceneRepository, CampaignHandle campaign, BoardScreenPresenter board, Func<string?>? openImageFile = null)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _sceneRepository = sceneRepository ?? throw new ArgumentNullException(nameof(sceneRepository));
            _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _openImageFile = openImageFile ?? (() => NativeFileDialog.OpenImageFile("Select an image"));
        }

        /// <summary>The assets currently shown, in the order <see cref="ISceneRepository.ListAssets"/> returned them.</summary>
        public IReadOnlyList<AssetManifestEntryRecord> Assets => _assets;

        public VisualElement BuildView()
        {
            VisualElement root = new VisualElement { name = "asset-pool" };
            root.AddToClassList("asset-pool");
            root.Add(new Label("Asset Pool") { name = "asset-pool-title" });

            Button upload = new Button { name = "asset-pool-upload-button", text = "Upload Image" };
            upload.clicked += () => UploadFromDialog();
            root.Add(upload);

            _list = new ScrollView { name = "asset-pool-list" };
            root.Add(_list);

            _status = new Label { name = "asset-pool-status" };
            root.Add(_status);

            Refresh();
            return root;
        }

        public Result Refresh()
        {
            Result<IReadOnlyList<AssetManifestEntryRecord>> listed = _sceneRepository.ListAssets(_campaign, NewCorrelationId());
            if (listed.IsFailure)
            {
                SetStatus("Failed to list assets: " + listed.Error.SafeReasonCode);
                return Result.Failure(listed.Error);
            }

            _assets.Clear();
            _assets.AddRange(listed.Value);
            RenderList();
            SetStatus(_assets.Count == 0 ? "No assets yet." : _assets.Count + " asset(s).");
            return Result.Success();
        }

        /// <summary>
        /// Opens the (possibly injected) file dialog and, if the user picked a file, registers it and
        /// refreshes the pool. Returns <c>null</c> if the user cancelled (no dialog result at all -- no
        /// <see cref="ISceneRepository.RegisterAsset"/> call is made, and the pool is left exactly as it
        /// was); otherwise the <see cref="ISceneRepository.RegisterAsset"/> result.
        /// </summary>
        public Result<AssetManifestEntryRecord>? UploadFromDialog()
        {
            string? path = _openImageFile();
            if (path == null)
            {
                return null;
            }

            Result<AssetManifestEntryRecord> registered = _sceneRepository.RegisterAsset(_campaign, path, NewCommandId(), NewCorrelationId());
            if (registered.IsFailure)
            {
                SetStatus("Upload failed: " + registered.Error.SafeReasonCode);
                return registered;
            }

            Refresh();
            SetStatus("Uploaded.");
            return registered;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _textureCache.Dispose();
            CancelDrag();
            _disposed = true;
        }

        private void RenderList()
        {
            if (_list == null) return;
            _list.Clear();

            foreach (AssetManifestEntryRecord asset in _assets)
            {
                VisualElement item = new VisualElement { name = "asset-pool-item-" + asset.AssetId };
                item.AddToClassList("asset-pool-item");
                item.style.width = (float)ItemSizePixels;
                item.style.height = (float)ItemSizePixels;
                item.style.marginBottom = 4;

                Result<Texture2D> texture = _textureCache.Load(_sceneRepository, _campaign, asset.AssetId, NewCorrelationId());
                if (texture.IsSuccess)
                {
                    item.style.backgroundImage = new StyleBackground(texture.Value);
                    item.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                }
                else
                {
                    item.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
                }

                AssetId capturedAssetId = asset.AssetId;
                item.RegisterCallback<PointerDownEvent>(evt => OnItemPointerDown(evt, item, capturedAssetId));
                item.RegisterCallback<PointerMoveEvent>(evt => OnItemPointerMove(evt, item));
                item.RegisterCallback<PointerUpEvent>(evt => OnItemPointerUp(evt, item));
                item.RegisterCallback<PointerCaptureOutEvent>(_ => CancelDrag());

                _list.Add(item);
            }
        }

        // ---- in-app drag from a pool item onto the board ----------------------------------------------
        //
        // Real UI Toolkit pointer wiring, by direct precedent of BoardScreenPresenter's own camera-pan
        // gesture (PointerDownEvent/PointerMoveEvent/PointerUpEvent + CapturePointer, same UIDocument, no
        // third-party API needed for this part -- task contract section 1.4). Not itself unit-tested (this
        // codebase's own established precedent); BoardScreenPresenter.ApplyDroppedAsset, which this
        // eventually calls, is the pure decision logic and is tested directly.

        private void OnItemPointerDown(PointerDownEvent evt, VisualElement item, AssetId assetId)
        {
            item.CapturePointer(evt.pointerId);
            _draggingAssetId = assetId;

            _dragGhost = new VisualElement { name = "asset-pool-drag-ghost" };
            _dragGhost.pickingMode = PickingMode.Ignore;
            _dragGhost.style.position = Position.Absolute;
            _dragGhost.style.width = (float)ItemSizePixels;
            _dragGhost.style.height = (float)ItemSizePixels;
            _dragGhost.style.backgroundColor = item.resolvedStyle.backgroundColor;
            _dragGhost.style.backgroundImage = item.resolvedStyle.backgroundImage;
            _document.rootVisualElement.Add(_dragGhost);
            PositionGhost(evt.position);
            evt.StopPropagation();
        }

        private void OnItemPointerMove(PointerMoveEvent evt, VisualElement item)
        {
            if (!item.HasPointerCapture(evt.pointerId) || _dragGhost == null) return;
            PositionGhost(evt.position);
        }

        private void OnItemPointerUp(PointerUpEvent evt, VisualElement item)
        {
            if (!item.HasPointerCapture(evt.pointerId)) return;
            item.ReleasePointer(evt.pointerId);

            AssetId? assetId = _draggingAssetId;
            VisualElement? boardArea = _board.BoardArea;
            if (assetId.HasValue && boardArea != null)
            {
                Vector2 localPosition = boardArea.WorldToLocal(evt.position);
                if (boardArea.ContainsPoint(localPosition))
                {
                    _board.ApplyDroppedAsset(assetId.Value, localPosition.x, localPosition.y);
                }
            }

            CancelDrag();
        }

        private void PositionGhost(Vector2 panelPosition)
        {
            if (_dragGhost == null) return;
            _dragGhost.style.left = panelPosition.x - (float)(ItemSizePixels / 2);
            _dragGhost.style.top = panelPosition.y - (float)(ItemSizePixels / 2);
        }

        private void CancelDrag()
        {
            if (_dragGhost != null)
            {
                _dragGhost.RemoveFromHierarchy();
                _dragGhost = null;
            }

            _draggingAssetId = null;
        }

        private void SetStatus(string text)
        {
            if (_status != null) _status.text = text;
        }

        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static CorrelationId NewCorrelationId() => CorrelationId.Parse("corr_" + Guid.NewGuid().ToString("N"));
    }
}
