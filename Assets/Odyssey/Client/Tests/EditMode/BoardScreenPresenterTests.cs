using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;
using Odyssey.Persistence.Sqlite;
using Odyssey.Unity.Client;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Tests.Unity.EditMode
{
    /// <summary>
    /// ODY-UI-01-002: presenter-level tests, following the same pattern
    /// <c>RuntimeCompositionAndDiagnosticsTests.DeveloperShellDisplaysBuildIdentityAndUnavailableFallback</c>
    /// already established for <see cref="DeveloperShellPresenter"/> --
    /// a bare <see cref="GameObject"/> plus a <see cref="UIDocument"/>
    /// component works in EditMode without a running scene or Player.
    ///
    /// Selection/move logic is exercised through <see cref="BoardScreenPresenter.SelectToken"/>/
    /// <see cref="BoardScreenPresenter.TryMoveSelectedTokenTo"/> directly,
    /// not through simulated UI Toolkit pointer events -- no existing test
    /// in this repository simulates a click event in EditMode, and the
    /// presenter's own click callbacks are documented thin wrappers over
    /// these same public methods (task contract section 5).
    ///
    /// Real <see cref="SqliteSceneRepository"/> against a real temp-directory
    /// campaign, matching every <c>ODY-S03-*</c> test's own convention --
    /// not a mock.
    /// </summary>
    public sealed class BoardScreenPresenterTests
    {
        private static CorrelationId TestCorrelationId => CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly UnityWallClock Clock = new UnityWallClock();

        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        [Test]
        public void ControllingActor_SelectsOwnToken_MovesIt_PositionUpdatesAndRendersCorrectly()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            var campaignRepository = new SqliteCampaignRepository(Clock);
            var createRequest = new CreateCampaignRequest(directory.Path, "Board Screen Test Campaign", "ruleset.core", "1.0.0", "0.1.0", global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost());
            Result<CampaignHandle> created = campaignRepository.Create(createRequest, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            CampaignHandle campaign = created.Value;

            var sceneRepository = new SqliteSceneRepository(Clock);
            var obstacleRepository = new SqliteObstacleRepository(Clock);
            var visionRepository = new SqliteTokenVisionRepository(Clock);
            var fogRepository = new SqliteFogOfWarRepository(Clock);
            SceneId sceneId = sceneRepository.CreateScene(campaign, "Test Scene", NewCommandId(), TestCorrelationId).Value.SceneId;

            UserId localActor = NewUserId();
            TokenRecord ownToken = sceneRepository.CreateToken(campaign, sceneId, new TokenPosition(0, 0), localActor, NewCommandId(), TestCorrelationId).Value;

            GameObject gameObject = new GameObject("Board Screen Document");
            try
            {
                UIDocument document = gameObject.AddComponent<UIDocument>();
                using var presenter = new BoardScreenPresenter(document, sceneRepository, campaign, campaignRepository, obstacleRepository, visionRepository, fogRepository, sceneId, localActor);
                Assert.That(presenter.Initialize().IsSuccess, Is.True);

                presenter.SelectToken(ownToken.TokenId);
                Assert.That(presenter.SelectedTokenId, Is.EqualTo(ownToken.TokenId));

                Result<TokenRecord> moved = presenter.TryMoveSelectedTokenTo(new TokenPosition(5, 4));
                Assert.That(moved.IsSuccess, Is.True, "the controlling actor must be authorized to move their own token");
                Assert.That(moved.Value.Position.X, Is.EqualTo(5));
                Assert.That(moved.Value.Position.Y, Is.EqualTo(4));
                Assert.That(presenter.SelectedTokenId, Is.Null, "a successful move must clear the selection");

                Result<TokenRecord> persisted = sceneRepository.GetToken(campaign, ownToken.TokenId, TestCorrelationId);
                Assert.That(persisted.Value.Position.X, Is.EqualTo(5));
                Assert.That(persisted.Value.Position.Y, Is.EqualTo(4));

                VisualElement? tokenElement = document.rootVisualElement.Q<VisualElement>("token-" + ownToken.TokenId);
                Assert.That(tokenElement, Is.Not.Null, "the moved token must still be rendered after the move");
                Assert.That(tokenElement!.style.left.value.value, Is.Not.EqualTo(0f), "the rendered position must reflect the new coordinates, not the original ones");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
            }
        }

        [Test]
        public void NonControllingActor_SelectsForeignToken_MoveIsDenied_PositionUnchanged()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            var campaignRepository = new SqliteCampaignRepository(Clock);
            var createRequest = new CreateCampaignRequest(directory.Path, "Board Screen Test Campaign", "ruleset.core", "1.0.0", "0.1.0", global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost());
            CampaignHandle campaign = campaignRepository.Create(createRequest, NewCommandId(), TestCorrelationId).Value;

            var sceneRepository = new SqliteSceneRepository(Clock);
            var obstacleRepository = new SqliteObstacleRepository(Clock);
            var visionRepository = new SqliteTokenVisionRepository(Clock);
            var fogRepository = new SqliteFogOfWarRepository(Clock);
            SceneId sceneId = sceneRepository.CreateScene(campaign, "Test Scene", NewCommandId(), TestCorrelationId).Value.SceneId;

            UserId localActor = NewUserId();
            UserId otherController = NewUserId();
            TokenRecord foreignToken = sceneRepository.CreateToken(campaign, sceneId, new TokenPosition(1, 1), otherController, NewCommandId(), TestCorrelationId).Value;

            GameObject gameObject = new GameObject("Board Screen Document");
            try
            {
                UIDocument document = gameObject.AddComponent<UIDocument>();
                using var presenter = new BoardScreenPresenter(document, sceneRepository, campaign, campaignRepository, obstacleRepository, visionRepository, fogRepository, sceneId, localActor);
                Assert.That(presenter.Initialize().IsSuccess, Is.True);

                presenter.SelectToken(foreignToken.TokenId);
                Result<TokenRecord> moved = presenter.TryMoveSelectedTokenTo(new TokenPosition(9, 9));

                Assert.That(moved.IsFailure, Is.True, "a non-controller, non-MainGM actor must not be authorized to move the token");

                Result<TokenRecord> persisted = sceneRepository.GetToken(campaign, foreignToken.TokenId, TestCorrelationId);
                Assert.That(persisted.Value.Position.X, Is.EqualTo(1), "the foreign token's position must not change after a denied move");
                Assert.That(persisted.Value.Position.Y, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
            }
        }

        [Test]
        public void MainGmActor_MovesForeignToken_Succeeds()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            var campaignRepository = new SqliteCampaignRepository(Clock);
            var createRequest = new CreateCampaignRequest(directory.Path, "Board Screen Test Campaign", "ruleset.core", "1.0.0", "0.1.0", global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost());
            CampaignHandle campaign = campaignRepository.Create(createRequest, NewCommandId(), TestCorrelationId).Value;

            var sceneRepository = new SqliteSceneRepository(Clock);
            var obstacleRepository = new SqliteObstacleRepository(Clock);
            var visionRepository = new SqliteTokenVisionRepository(Clock);
            var fogRepository = new SqliteFogOfWarRepository(Clock);
            SceneId sceneId = sceneRepository.CreateScene(campaign, "Test Scene", NewCommandId(), TestCorrelationId).Value.SceneId;

            // ODY-S10-101: MainGM-ness is the stored membership now, so this actor is the campaign's host.
            UserId mainGm = global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost();
            UserId otherController = NewUserId();
            TokenRecord foreignToken = sceneRepository.CreateToken(campaign, sceneId, new TokenPosition(1, 1), otherController, NewCommandId(), TestCorrelationId).Value;

            GameObject gameObject = new GameObject("Board Screen Document");
            try
            {
                UIDocument document = gameObject.AddComponent<UIDocument>();
                using var presenter = new BoardScreenPresenter(document, sceneRepository, campaign, campaignRepository, obstacleRepository, visionRepository, fogRepository, sceneId, mainGm);
                Assert.That(presenter.Initialize().IsSuccess, Is.True);

                presenter.SelectToken(foreignToken.TokenId);
                Result<TokenRecord> moved = presenter.TryMoveSelectedTokenTo(new TokenPosition(6, 6));

                Assert.That(moved.IsSuccess, Is.True, "the campaign's stored MainGM must be authorized to move any token regardless of control");
                Assert.That(moved.Value.Position.X, Is.EqualTo(6));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
            }
        }

        [Test]
        public void Initialize_RendersAllExistingTokensAtTheirRealPersistedCoordinates()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            var campaignRepository = new SqliteCampaignRepository(Clock);
            var createRequest = new CreateCampaignRequest(directory.Path, "Board Screen Test Campaign", "ruleset.core", "1.0.0", "0.1.0", global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost());
            CampaignHandle campaign = campaignRepository.Create(createRequest, NewCommandId(), TestCorrelationId).Value;

            var sceneRepository = new SqliteSceneRepository(Clock);
            var obstacleRepository = new SqliteObstacleRepository(Clock);
            var visionRepository = new SqliteTokenVisionRepository(Clock);
            var fogRepository = new SqliteFogOfWarRepository(Clock);
            SceneId sceneId = sceneRepository.CreateScene(campaign, "Test Scene", NewCommandId(), TestCorrelationId).Value.SceneId;

            UserId localActor = NewUserId();
            TokenRecord tokenA = sceneRepository.CreateToken(campaign, sceneId, new TokenPosition(0, 0), localActor, NewCommandId(), TestCorrelationId).Value;
            TokenRecord tokenB = sceneRepository.CreateToken(campaign, sceneId, new TokenPosition(2, 3), NewUserId(), NewCommandId(), TestCorrelationId).Value;

            GameObject gameObject = new GameObject("Board Screen Document");
            try
            {
                UIDocument document = gameObject.AddComponent<UIDocument>();
                using var presenter = new BoardScreenPresenter(document, sceneRepository, campaign, campaignRepository, obstacleRepository, visionRepository, fogRepository, sceneId, localActor);
                Assert.That(presenter.Initialize().IsSuccess, Is.True);

                Assert.That(document.rootVisualElement.Q<VisualElement>("token-" + tokenA.TokenId), Is.Not.Null);
                Assert.That(document.rootVisualElement.Q<VisualElement>("token-" + tokenB.TokenId), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
            }
        }

        // ---- ODY-S08-102: board camera (pan/zoom) + click-vs-drag gesture ----------

        private BoardScreenPresenter BuildPresenterForCamera(out CampaignHandle campaign, out ISceneRepository sceneRepository, out TokenId tokenId, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory)
        {
            directory = new TemporaryDirectory();
            campaignRepository = new SqliteCampaignRepository(Clock);
            campaign = campaignRepository.Create(new CreateCampaignRequest(directory.Path, "Board Camera Test Campaign", "ruleset.core", "1.0.0", "0.1.0", global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost()), NewCommandId(), TestCorrelationId).Value;
            var sqliteSceneRepository = new SqliteSceneRepository(Clock);
            var obstacleRepository = new SqliteObstacleRepository(Clock);
            var visionRepository = new SqliteTokenVisionRepository(Clock);
            var fogRepository = new SqliteFogOfWarRepository(Clock);
            sceneRepository = sqliteSceneRepository;
            SceneId sceneId = sqliteSceneRepository.CreateScene(campaign, "Test Scene", NewCommandId(), TestCorrelationId).Value.SceneId;
            UserId localActor = NewUserId();
            TokenRecord token = sqliteSceneRepository.CreateToken(campaign, sceneId, new TokenPosition(0, 0), localActor, NewCommandId(), TestCorrelationId).Value;
            tokenId = token.TokenId;

            gameObject = new GameObject("Board Camera Document");
            document = gameObject.AddComponent<UIDocument>();
            var presenter = new BoardScreenPresenter(document, sceneRepository, campaign, campaignRepository, obstacleRepository, visionRepository, fogRepository, sceneId, localActor);
            Assert.That(presenter.Initialize().IsSuccess, Is.True);
            return presenter;
        }

        [Test] // TC-BOARD-062
        public void BoardPointerGesture_BelowDragThreshold_StillMovesTheSelectedToken_ExactlyLikeAnOrdinaryClick()
        {
            BoardScreenPresenter presenter = BuildPresenterForCamera(out CampaignHandle campaign, out ISceneRepository sceneRepository, out TokenId tokenId, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                presenter.SelectToken(tokenId);
                const double clickPixelX = 300.0;
                const double clickPixelY = 260.0;

                presenter.BeginBoardPointerGesture(clickPixelX, clickPixelY);
                presenter.MoveBoardPointer(clickPixelX + 2.0, clickPixelY + 1.0); // 2.24px -- below the 5px drag threshold
                presenter.EndBoardPointerGesture(clickPixelX + 2.0, clickPixelY + 1.0);

                TokenPosition expectedDestination = new TokenPosition(presenter.Camera.FromPixelsX(clickPixelX + 2.0), presenter.Camera.FromPixelsY(clickPixelY + 1.0));
                Result<TokenRecord> persisted = sceneRepository.GetToken(campaign, tokenId, TestCorrelationId);
                Assert.That(persisted.Value.Position.X, Is.EqualTo(expectedDestination.X).Within(1e-6), "movement below the drag threshold must still move the selected token, exactly like the old ClickEvent-driven behavior");
                Assert.That(persisted.Value.Position.Y, Is.EqualTo(expectedDestination.Y).Within(1e-6));
                Assert.That(presenter.SelectedTokenId, Is.Null, "a click-move must clear the selection exactly as before");
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        [Test] // TC-BOARD-063
        public void BoardMiddleButtonPan_AboveDragThreshold_PansTheCamera_AndDoesNotMoveOrDeselectTheSelectedToken()
        {
            BoardScreenPresenter presenter = BuildPresenterForCamera(out CampaignHandle campaign, out ISceneRepository sceneRepository, out TokenId tokenId, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                presenter.SelectToken(tokenId);
                const double startPixelX = 200.0;
                const double startPixelY = 200.0;
                const double deltaX = 40.0;
                const double deltaY = -15.0;
                double pixelXBefore = presenter.Camera.ToPixelsX(0);

                // ODY-S08-107: the pan moved from the left-button drag to the middle-button drag.
                presenter.BeginBoardPan(startPixelX, startPixelY);
                presenter.MoveBoardPan(startPixelX + deltaX, startPixelY + deltaY); // ~42.7px -- above the 5px drag threshold
                presenter.EndBoardPan();

                Result<TokenRecord> persisted = sceneRepository.GetToken(campaign, tokenId, TestCorrelationId);
                Assert.That(persisted.Value.Position.X, Is.EqualTo(0), "movement above the drag threshold must NOT move the token");
                Assert.That(persisted.Value.Position.Y, Is.EqualTo(0));
                Assert.That(presenter.SelectedTokenId, Is.EqualTo(tokenId), "panning the camera must not disturb the current selection");
                Assert.That(presenter.Camera.ToPixelsX(0), Is.EqualTo(pixelXBefore + deltaX).Within(1e-9), "the camera must have panned by exactly the dragged pixel delta");
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        [Test] // TC-BOARD-064
        public void ZoomBoard_ChangesTheCameraScale_AndRepositionsAlreadyRenderedTokenElements()
        {
            BoardScreenPresenter presenter = BuildPresenterForCamera(out CampaignHandle campaign, out ISceneRepository sceneRepository, out TokenId tokenId, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                VisualElement? tokenElement = document.rootVisualElement.Q<VisualElement>("token-" + tokenId);
                Assert.That(tokenElement, Is.Not.Null);
                float leftBefore = tokenElement!.style.left.value.value;

                // Anchored away from the token's own screen position (220, 220) -- a zoom anchored exactly
                // on the token would correctly leave it in place, which would not test repositioning.
                presenter.ZoomBoard(factor: 2.0, anchorPixelX: 60.0, anchorPixelY: 60.0);

                Assert.That(presenter.Camera.Scale, Is.EqualTo(80.0).Within(1e-9));
                Assert.That(tokenElement.style.left.value.value, Is.Not.EqualTo(leftBefore), "zooming must reposition the already-rendered token element (no full re-render needed)");

                Result<TokenRecord> persisted = sceneRepository.GetToken(campaign, tokenId, TestCorrelationId);
                Assert.That(persisted.Value.Position.X, Is.EqualTo(0), "zooming is purely a local view change -- it must never touch persisted state");
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        // ---- ODY-S08-103: dropping an asset-pool asset onto the board -------------

        private static string WriteTempImageSource(string name, byte[] content)
        {
            string path = Path.Combine(Path.GetTempPath(), "ody-s08-103-" + Guid.NewGuid().ToString("N") + "-" + name);
            File.WriteAllBytes(path, content);
            return path;
        }

        [Test] // TC-BOARD-067
        public void ApplyDroppedAsset_DroppedOnAToken_SetsThatTokensPortrait_WithAFreshRevision_NotAnyOtherToken()
        {
            BoardScreenPresenter presenter = BuildPresenterForCamera(out CampaignHandle campaign, out ISceneRepository sceneRepository, out TokenId tokenId, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            string sourceFile = WriteTempImageSource("portrait.png", System.Text.Encoding.UTF8.GetBytes("portrait bytes " + Guid.NewGuid().ToString("N")));
            try
            {
                // A second token, far from the first (0,0) one, so a mis-hit would be provable.
                Result<TokenRecord> otherToken = sceneRepository.CreateToken(campaign, sceneRepository.GetToken(campaign, tokenId, TestCorrelationId).Value.SceneId, new TokenPosition(5, 5), NewUserId(), NewCommandId(), TestCorrelationId);
                Assert.That(otherToken.IsSuccess, Is.True);
                Assert.That(presenter.Refresh().IsSuccess, Is.True);

                Result<AssetManifestEntryRecord> registered = sceneRepository.RegisterAsset(campaign, sourceFile, NewCommandId(), TestCorrelationId);
                Assert.That(registered.IsSuccess, Is.True);

                // The first token sits at world (0,0), which the default camera places at pixel (220, 220).
                Result result = presenter.ApplyDroppedAsset(registered.Value.AssetId, presenter.Camera.ToPixelsX(0), presenter.Camera.ToPixelsY(0));

                Assert.That(result.IsSuccess, Is.True);
                Result<TokenRecord> reReadTarget = sceneRepository.GetToken(campaign, tokenId, TestCorrelationId);
                Assert.That(reReadTarget.Value.PortraitAssetId, Is.EqualTo(registered.Value.AssetId));
                Result<TokenRecord> reReadOther = sceneRepository.GetToken(campaign, otherToken.Value.TokenId, TestCorrelationId);
                Assert.That(reReadOther.Value.PortraitAssetId, Is.Null, "only the token actually under the drop point may be touched");
                Result<SceneRecord> scene = sceneRepository.GetScene(campaign, reReadTarget.Value.SceneId, TestCorrelationId);
                Assert.That(scene.Value.BackgroundAssetId, Is.Null, "a drop on a token must not also set the scene background");
            }
            finally
            {
                File.Delete(sourceFile);
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        [Test] // TC-BOARD-068
        public void ApplyDroppedAsset_DroppedOnEmptyBoardArea_SetsTheSceneBackground_AndTouchesNoToken()
        {
            BoardScreenPresenter presenter = BuildPresenterForCamera(out CampaignHandle campaign, out ISceneRepository sceneRepository, out TokenId tokenId, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            string sourceFile = WriteTempImageSource("background.png", System.Text.Encoding.UTF8.GetBytes("background bytes " + Guid.NewGuid().ToString("N")));
            try
            {
                Result<AssetManifestEntryRecord> registered = sceneRepository.RegisterAsset(campaign, sourceFile, NewCommandId(), TestCorrelationId);
                Assert.That(registered.IsSuccess, Is.True);

                // Far from the only token's pixel position (220, 220) -- an ordinary empty patch of board.
                Result result = presenter.ApplyDroppedAsset(registered.Value.AssetId, 30.0, 30.0);

                Assert.That(result.IsSuccess, Is.True);
                Result<TokenRecord> token = sceneRepository.GetToken(campaign, tokenId, TestCorrelationId);
                Result<SceneRecord> scene = sceneRepository.GetScene(campaign, token.Value.SceneId, TestCorrelationId);
                Assert.That(scene.Value.BackgroundAssetId, Is.EqualTo(registered.Value.AssetId));
                Assert.That(token.Value.PortraitAssetId, Is.Null, "a drop on the empty board must not touch any token");
            }
            finally
            {
                File.Delete(sourceFile);
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        [Test] // TC-BOARD-069
        public void ApplyDroppedAsset_AlwaysReadsTheCurrentRevisionImmediatelyBeforeWriting_NeverACachedOne()
        {
            BoardScreenPresenter presenter = BuildPresenterForCamera(out CampaignHandle campaign, out ISceneRepository sceneRepository, out TokenId tokenId, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            string sourceFile = WriteTempImageSource("first.png", System.Text.Encoding.UTF8.GetBytes("first " + Guid.NewGuid().ToString("N")));
            string sourceFile2 = WriteTempImageSource("second.png", System.Text.Encoding.UTF8.GetBytes("second " + Guid.NewGuid().ToString("N")));
            try
            {
                Result<AssetManifestEntryRecord> first = sceneRepository.RegisterAsset(campaign, sourceFile, NewCommandId(), TestCorrelationId);
                Result<AssetManifestEntryRecord> second = sceneRepository.RegisterAsset(campaign, sourceFile2, NewCommandId(), TestCorrelationId);
                Assert.That(first.IsSuccess && second.IsSuccess, Is.True);

                // Two drops in a row onto the SAME token: if the second one relied on a revision cached from
                // before the first drop, it would be rejected as stale instead of succeeding.
                Result firstDrop = presenter.ApplyDroppedAsset(first.Value.AssetId, presenter.Camera.ToPixelsX(0), presenter.Camera.ToPixelsY(0));
                Result secondDrop = presenter.ApplyDroppedAsset(second.Value.AssetId, presenter.Camera.ToPixelsX(0), presenter.Camera.ToPixelsY(0));

                Assert.That(firstDrop.IsSuccess, Is.True);
                Assert.That(secondDrop.IsSuccess, Is.True, "a second drop right after the first must not be rejected as a stale revision -- each drop must re-read the current revision");
                Result<TokenRecord> reRead = sceneRepository.GetToken(campaign, tokenId, TestCorrelationId);
                Assert.That(reRead.Value.PortraitAssetId, Is.EqualTo(second.Value.AssetId), "the second, later drop must win");
            }
            finally
            {
                File.Delete(sourceFile);
                File.Delete(sourceFile2);
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        // ---- ODY-S08-104: dragging a token across the board ------------------------

        private BoardScreenPresenter BuildPresenterForTokenDrag(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId tokenId, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory)
        {
            directory = new TemporaryDirectory();
            campaignRepository = new SqliteCampaignRepository(Clock);
            campaign = campaignRepository.Create(new CreateCampaignRequest(directory.Path, "Token Drag Test Campaign", "ruleset.core", "1.0.0", "0.1.0", global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost()), NewCommandId(), TestCorrelationId).Value;
            var sqliteSceneRepository = new SqliteSceneRepository(Clock);
            var obstacleRepository = new SqliteObstacleRepository(Clock);
            var visionRepository = new SqliteTokenVisionRepository(Clock);
            var fogRepository = new SqliteFogOfWarRepository(Clock);
            sceneRepository = new MoveCountingSceneRepository(sqliteSceneRepository);
            SceneId sceneId = sqliteSceneRepository.CreateScene(campaign, "Test Scene", NewCommandId(), TestCorrelationId).Value.SceneId;
            UserId localActor = NewUserId();
            TokenRecord token = sqliteSceneRepository.CreateToken(campaign, sceneId, new TokenPosition(0, 0), localActor, NewCommandId(), TestCorrelationId).Value;
            tokenId = token.TokenId;

            gameObject = new GameObject("Token Drag Document");
            document = gameObject.AddComponent<UIDocument>();
            var presenter = new BoardScreenPresenter(document, sceneRepository, campaign, campaignRepository, obstacleRepository, visionRepository, fogRepository, sceneId, localActor);
            Assert.That(presenter.Initialize().IsSuccess, Is.True);
            return presenter;
        }

        [Test] // TC-BOARD-072
        public void TokenPointerGesture_BelowDragThreshold_SelectsTheToken_PositionUnchanged()
        {
            BoardScreenPresenter presenter = BuildPresenterForTokenDrag(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId tokenId, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                double tokenPixelX = presenter.Camera.ToPixelsX(0);
                double tokenPixelY = presenter.Camera.ToPixelsY(0);

                presenter.BeginTokenDrag(tokenId, tokenPixelX, tokenPixelY);
                presenter.MoveTokenDrag(tokenId, tokenPixelX + 2.0, tokenPixelY + 1.0); // 2.24px -- below the 5px drag threshold
                presenter.EndTokenDrag(tokenId, tokenPixelX + 2.0, tokenPixelY + 1.0);

                Assert.That(presenter.SelectedTokenId, Is.EqualTo(tokenId), "movement below the threshold must select the token, exactly like the old ClickEvent handler");
                Assert.That(sceneRepository.MoveTokenCalls, Is.EqualTo(0), "a click must never call MoveToken");
                Result<TokenRecord> persisted = sceneRepository.GetToken(campaign, tokenId, TestCorrelationId);
                Assert.That(persisted.Value.Position.X, Is.EqualTo(0));
                Assert.That(persisted.Value.Position.Y, Is.EqualTo(0));
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        [Test] // TC-BOARD-073
        public void TokenPointerGesture_AboveDragThreshold_CommitsExactlyOnce_UpdatesPositionInRepositoryAndVisually()
        {
            BoardScreenPresenter presenter = BuildPresenterForTokenDrag(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId tokenId, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                VisualElement? tokenElement = document.rootVisualElement.Q<VisualElement>("token-" + tokenId);
                Assert.That(tokenElement, Is.Not.Null);
                float leftBefore = tokenElement!.style.left.value.value;

                double startX = presenter.Camera.ToPixelsX(0);
                double startY = presenter.Camera.ToPixelsY(0);
                double endX = startX + 80.0;
                double endY = startY + 40.0;

                presenter.BeginTokenDrag(tokenId, startX, startY);
                presenter.MoveTokenDrag(tokenId, startX + 40.0, startY + 20.0); // intermediate move, already above threshold
                presenter.MoveTokenDrag(tokenId, endX, endY);
                presenter.EndTokenDrag(tokenId, endX, endY);

                Assert.That(sceneRepository.MoveTokenCalls, Is.EqualTo(1), "exactly one commit, not one per MoveTokenDrag call");
                double expectedX = presenter.Camera.FromPixelsX(endX);
                double expectedY = presenter.Camera.FromPixelsY(endY);
                Result<TokenRecord> persisted = sceneRepository.GetToken(campaign, tokenId, TestCorrelationId);
                Assert.That(persisted.Value.Position.X, Is.EqualTo(expectedX).Within(1e-9));
                Assert.That(persisted.Value.Position.Y, Is.EqualTo(expectedY).Within(1e-9));

                VisualElement? tokenElementAfter = document.rootVisualElement.Q<VisualElement>("token-" + tokenId);
                Assert.That(tokenElementAfter, Is.Not.Null, "the token must still be rendered after the drag commits");
                Assert.That(tokenElementAfter!.style.left.value.value, Is.Not.EqualTo(leftBefore), "the rendered position must reflect the new coordinates");
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        [Test] // TC-BOARD-074
        public void TokenPointerGesture_WhileDragging_UpdatesTheVisualPreview_WithoutAnyRepositoryMoveCall()
        {
            BoardScreenPresenter presenter = BuildPresenterForTokenDrag(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId tokenId, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                VisualElement? tokenElement = document.rootVisualElement.Q<VisualElement>("token-" + tokenId);
                Assert.That(tokenElement, Is.Not.Null);
                float leftBefore = tokenElement!.style.left.value.value;

                double startX = presenter.Camera.ToPixelsX(0);
                double startY = presenter.Camera.ToPixelsY(0);

                presenter.BeginTokenDrag(tokenId, startX, startY);
                presenter.MoveTokenDrag(tokenId, startX + 60.0, startY + 10.0);

                Assert.That(sceneRepository.MoveTokenCalls, Is.EqualTo(0), "no repository call may happen before the pointer is released");
                Assert.That(tokenElement.style.left.value.value, Is.Not.EqualTo(leftBefore), "the visual preview must follow the cursor while dragging");
                Result<TokenRecord> stillOriginal = sceneRepository.GetToken(campaign, tokenId, TestCorrelationId);
                Assert.That(stillOriginal.Value.Position.X, Is.EqualTo(0));
                Assert.That(stillOriginal.Value.Position.Y, Is.EqualTo(0));

                // Finish the gesture so the fixture does not leave a dangling pointer capture.
                presenter.EndTokenDrag(tokenId, startX + 60.0, startY + 10.0);
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        [Test] // TC-BOARD-075
        public void TokenPointerGesture_UnsuccessfulCommit_RevertsTheVisualPositionToTheLastConfirmedOne()
        {
            BoardScreenPresenter presenter = BuildPresenterForTokenDrag(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId tokenId, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                Result<TokenRecord> anchorToken = sceneRepository.GetToken(campaign, tokenId, TestCorrelationId);
                // A second token occupies (3, 3) -- BoardMovementService's own occupancy check (BOARD-INV-009)
                // must deny a move onto it, exactly as it would for the old click-to-move flow.
                Result<TokenRecord> occupant = sceneRepository.CreateToken(campaign, anchorToken.Value.SceneId, new TokenPosition(3, 3), NewUserId(), NewCommandId(), TestCorrelationId);
                Assert.That(occupant.IsSuccess, Is.True);
                Assert.That(presenter.Refresh().IsSuccess, Is.True);

                VisualElement? tokenElement = document.rootVisualElement.Q<VisualElement>("token-" + tokenId);
                Assert.That(tokenElement, Is.Not.Null);
                float originalLeft = tokenElement!.style.left.value.value;
                float originalTop = tokenElement.style.top.value.value;

                double destPixelX = presenter.Camera.ToPixelsX(3);
                double destPixelY = presenter.Camera.ToPixelsY(3);

                presenter.BeginTokenDrag(tokenId, presenter.Camera.ToPixelsX(0), presenter.Camera.ToPixelsY(0));
                presenter.MoveTokenDrag(tokenId, destPixelX, destPixelY);
                presenter.EndTokenDrag(tokenId, destPixelX, destPixelY);

                // BoardMovementService's own occupancy check (BOARD-INV-009) denies the move before ever
                // calling ISceneRepository.MoveToken -- the repository-level call count is correctly 0 here;
                // TryMoveTokenTo's own Refresh()-on-failure is what performs the rollback, not the repository.
                Assert.That(sceneRepository.MoveTokenCalls, Is.EqualTo(0));
                Result<TokenRecord> persisted = sceneRepository.GetToken(campaign, tokenId, TestCorrelationId);
                Assert.That(persisted.Value.Position.X, Is.EqualTo(0), "a denied move must leave the persisted position exactly as it was");
                Assert.That(persisted.Value.Position.Y, Is.EqualTo(0));

                VisualElement? tokenElementAfter = document.rootVisualElement.Q<VisualElement>("token-" + tokenId);
                Assert.That(tokenElementAfter, Is.Not.Null);
                Assert.That(tokenElementAfter!.style.left.value.value, Is.EqualTo(originalLeft).Within(0.01f), "the visual position must roll back to the last confirmed position, not stay where the pointer was released");
                Assert.That(tokenElementAfter.style.top.value.value, Is.EqualTo(originalTop).Within(0.01f));
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        [Test] // TC-BOARD-076
        public void TokenPointerGesture_DraggingATokenThatWasNeverSelected_StillMovesIt()
        {
            BoardScreenPresenter presenter = BuildPresenterForTokenDrag(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId tokenId, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                Assert.That(presenter.SelectedTokenId, Is.Null, "sanity: the token was never selected");

                double startX = presenter.Camera.ToPixelsX(0);
                double startY = presenter.Camera.ToPixelsY(0);
                double endX = startX + 50.0;
                double endY = startY + 30.0;

                presenter.BeginTokenDrag(tokenId, startX, startY);
                presenter.MoveTokenDrag(tokenId, endX, endY);
                presenter.EndTokenDrag(tokenId, endX, endY);

                Assert.That(sceneRepository.MoveTokenCalls, Is.EqualTo(1), "dragging must not require a prior click-to-select");
                double expectedX = presenter.Camera.FromPixelsX(endX);
                double expectedY = presenter.Camera.FromPixelsY(endY);
                Result<TokenRecord> persisted = sceneRepository.GetToken(campaign, tokenId, TestCorrelationId);
                Assert.That(persisted.Value.Position.X, Is.EqualTo(expectedX).Within(1e-9));
                Assert.That(persisted.Value.Position.Y, Is.EqualTo(expectedY).Within(1e-9));
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        [Test] // TC-BOARD-077
        public void Regression_ClickToSelectThenClickBoardToMove_StillWorksExactlyAsBefore()
        {
            BoardScreenPresenter presenter = BuildPresenterForTokenDrag(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId tokenId, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                double tokenPixelX = presenter.Camera.ToPixelsX(0);
                double tokenPixelY = presenter.Camera.ToPixelsY(0);

                presenter.BeginTokenDrag(tokenId, tokenPixelX, tokenPixelY);
                presenter.MoveTokenDrag(tokenId, tokenPixelX + 1.0, tokenPixelY + 1.0); // a real click on the token
                presenter.EndTokenDrag(tokenId, tokenPixelX + 1.0, tokenPixelY + 1.0);
                Assert.That(presenter.SelectedTokenId, Is.EqualTo(tokenId));

                const double destPixelX = 300.0;
                const double destPixelY = 260.0;
                presenter.BeginBoardPointerGesture(destPixelX, destPixelY);
                presenter.MoveBoardPointer(destPixelX + 1.0, destPixelY + 1.0); // a real click on the empty board
                presenter.EndBoardPointerGesture(destPixelX + 1.0, destPixelY + 1.0);

                double expectedX = presenter.Camera.FromPixelsX(destPixelX + 1.0);
                double expectedY = presenter.Camera.FromPixelsY(destPixelY + 1.0);
                Result<TokenRecord> persisted = sceneRepository.GetToken(campaign, tokenId, TestCorrelationId);
                Assert.That(persisted.Value.Position.X, Is.EqualTo(expectedX).Within(1e-6));
                Assert.That(persisted.Value.Position.Y, Is.EqualTo(expectedY).Within(1e-6));
                Assert.That(presenter.SelectedTokenId, Is.Null, "the old click-to-move flow must still clear the selection exactly as before");
                Assert.That(sceneRepository.MoveTokenCalls, Is.EqualTo(1));
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        // ---- ODY-S08-105: token z-order and scale (backend: TC-BOARD-078..080 in SqliteSceneRepositoryTests) -----

        /// <summary>Two overlapping tokens: lowerId (created first, ZOrder 1) and upperId (ZOrder 2), presenter already refreshed.</summary>
        private BoardScreenPresenter BuildPresenterWithTwoOverlappingTokens(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId lowerId, out TokenId upperId, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory)
        {
            BoardScreenPresenter presenter = BuildPresenterForTokenDrag(out campaign, out sceneRepository, out lowerId, out document, out gameObject, out campaignRepository, out directory);
            SceneId sceneId = sceneRepository.GetToken(campaign, lowerId, TestCorrelationId).Value.SceneId;
            TokenRecord upper = sceneRepository.CreateToken(campaign, sceneId, new TokenPosition(0.2, 0.0), NewUserId(), NewCommandId(), TestCorrelationId).Value;
            upperId = upper.TokenId;
            Assert.That(presenter.Refresh().IsSuccess, Is.True);
            return presenter;
        }

        [Test] // TC-BOARD-081
        public void TokenPointerDown_OnANonTopOverlappingToken_RaisesItAboveTheOther_AndItRendersOnTop()
        {
            BoardScreenPresenter presenter = BuildPresenterWithTwoOverlappingTokens(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId lowerId, out TokenId upperId, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                VisualElement board = presenter.BoardArea!;
                Assert.That(board.IndexOf(document.rootVisualElement.Q<VisualElement>("token-" + lowerId)), Is.LessThan(board.IndexOf(document.rootVisualElement.Q<VisualElement>("token-" + upperId))), "precondition: the later-created token is on top");

                double x = presenter.Camera.ToPixelsX(0);
                double y = presenter.Camera.ToPixelsY(0);
                presenter.BeginTokenDrag(lowerId, x, y);
                Assert.That(board.IndexOf(document.rootVisualElement.Q<VisualElement>("token-" + lowerId)), Is.GreaterThan(board.IndexOf(document.rootVisualElement.Q<VisualElement>("token-" + upperId))), "the raise reorders in place, without a Refresh under the active pointer capture");
                presenter.EndTokenDrag(lowerId, x, y); // a click

                Assert.That(sceneRepository.SetTokenZOrderCalls, Is.EqualTo(1));
                long lowerZ = sceneRepository.GetToken(campaign, lowerId, TestCorrelationId).Value.ZOrder;
                long upperZ = sceneRepository.GetToken(campaign, upperId, TestCorrelationId).Value.ZOrder;
                Assert.That(lowerZ, Is.GreaterThan(upperZ), "the clicked token's ZOrder must now exceed the other's");
                Assert.That(presenter.SelectedTokenId, Is.EqualTo(lowerId), "the click still selects, exactly as in ODY-S08-104");

                Assert.That(presenter.Refresh().IsSuccess, Is.True);
                Assert.That(board.IndexOf(document.rootVisualElement.Q<VisualElement>("token-" + lowerId)), Is.GreaterThan(board.IndexOf(document.rootVisualElement.Q<VisualElement>("token-" + upperId))), "a fresh render must sort by ZOrder ascending");
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }
        [Test] // TC-BOARD-082
        public void TokenPointerDown_OnAnAlreadyTopToken_MakesNoSetTokenZOrderCall()
        {
            BoardScreenPresenter presenter = BuildPresenterWithTwoOverlappingTokens(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId lowerId, out TokenId upperId, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                TokenRecord before = sceneRepository.GetToken(campaign, upperId, TestCorrelationId).Value;

                double x = presenter.Camera.ToPixelsX(0.2);
                double y = presenter.Camera.ToPixelsY(0.0);
                presenter.BeginTokenDrag(upperId, x, y);
                presenter.EndTokenDrag(upperId, x, y);

                Assert.That(sceneRepository.SetTokenZOrderCalls, Is.EqualTo(0), "an already-top token must cause no backend call at all");
                TokenRecord after = sceneRepository.GetToken(campaign, upperId, TestCorrelationId).Value;
                Assert.That(after.ZOrder, Is.EqualTo(before.ZOrder));
                Assert.That(after.Revision, Is.EqualTo(before.Revision));
                Assert.That(presenter.SelectedTokenId, Is.EqualTo(upperId));
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }
        [Test] // TC-BOARD-083
        public void ShiftWheelOverAToken_ChangesThatTokensScale_AndNotTheCamera()
        {
            BoardScreenPresenter presenter = BuildPresenterForTokenDrag(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId tokenId, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                double cameraScaleBefore = presenter.Camera.Scale;
                double x = presenter.Camera.ToPixelsX(0);
                double y = presenter.Camera.ToPixelsY(0);
                VisualElement element = document.rootVisualElement.Q<VisualElement>("token-" + tokenId)!;
                float widthBefore = element.style.width.value.value;

                presenter.HandleBoardWheel(-1.0, true, x, y);

                Assert.That(sceneRepository.SetTokenScaleCalls, Is.EqualTo(1));
                Assert.That(sceneRepository.GetToken(campaign, tokenId, TestCorrelationId).Value.Scale, Is.EqualTo(1.1).Within(1e-9));
                Assert.That(element.style.width.value.value, Is.EqualTo(widthBefore * 1.1f).Within(0.01f), "the token's visual size must follow its Scale");
                Assert.That(presenter.Camera.Scale, Is.EqualTo(cameraScaleBefore), "Shift+wheel over a token must not zoom the camera");
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }
        [Test] // TC-BOARD-084
        public void PlainWheelAtTheSamePosition_ZoomsTheCamera_AndNotTheTokensScale()
        {
            BoardScreenPresenter presenter = BuildPresenterForTokenDrag(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId tokenId, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                double cameraScaleBefore = presenter.Camera.Scale;
                double x = presenter.Camera.ToPixelsX(0);
                double y = presenter.Camera.ToPixelsY(0);

                presenter.HandleBoardWheel(-1.0, false, x, y);

                Assert.That(presenter.Camera.Scale, Is.GreaterThan(cameraScaleBefore), "a plain wheel must still zoom the camera in");
                Assert.That(sceneRepository.SetTokenScaleCalls, Is.EqualTo(0));
                Assert.That(sceneRepository.GetToken(campaign, tokenId, TestCorrelationId).Value.Scale, Is.EqualTo(1.0));

                // Shift over empty board (no token there) keeps the old camera behaviour too.
                double scaleAfterPlain = presenter.Camera.Scale;
                presenter.HandleBoardWheel(-1.0, true, 5.0, 5.0);
                Assert.That(presenter.Camera.Scale, Is.GreaterThan(scaleAfterPlain));
                Assert.That(sceneRepository.SetTokenScaleCalls, Is.EqualTo(0));
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }
        [Test] // TC-BOARD-085
        public void TokenScale_IsClampedToTheBounds_AndNeverDegenerates()
        {
            BoardScreenPresenter presenter = BuildPresenterForTokenDrag(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId tokenId, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                double x = presenter.Camera.ToPixelsX(0);
                double y = presenter.Camera.ToPixelsY(0);
                VisualElement element = document.rootVisualElement.Q<VisualElement>("token-" + tokenId)!;

                for (int i = 0; i < 60; i++) presenter.HandleBoardWheel(-1.0, true, x, y);
                Assert.That(sceneRepository.GetToken(campaign, tokenId, TestCorrelationId).Value.Scale, Is.EqualTo(TokenRecord.MaxScale));
                int callsAtMax = sceneRepository.SetTokenScaleCalls;
                presenter.HandleBoardWheel(-1.0, true, x, y);
                Assert.That(sceneRepository.SetTokenScaleCalls, Is.EqualTo(callsAtMax), "a notch at the upper bound changes nothing and makes no call");
                Assert.That(element.style.width.value.value, Is.EqualTo((float)(28.0 * TokenRecord.MaxScale)).Within(0.01f));

                for (int i = 0; i < 120; i++) presenter.HandleBoardWheel(1.0, true, x, y);
                Assert.That(sceneRepository.GetToken(campaign, tokenId, TestCorrelationId).Value.Scale, Is.EqualTo(TokenRecord.MinScale));
                Assert.That(element.style.width.value.value, Is.EqualTo((float)(28.0 * TokenRecord.MinScale)).Within(0.01f));
                Assert.That(element.style.width.value.value, Is.GreaterThan(0f), "the token must never shrink to nothing");
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        // ---- ODY-S08-106: multi-selection ---------------------------------------------------------

        /// <summary>Three non-overlapping tokens a (0,0), b (3,0), c (6,0), all controlled by the local actor, presenter refreshed.</summary>
        private BoardScreenPresenter BuildPresenterWithThreeTokens(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId a, out TokenId b, out TokenId c, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory)
        {
            BoardScreenPresenter presenter = BuildPresenterForTokenDrag(out campaign, out sceneRepository, out a, out document, out gameObject, out campaignRepository, out directory);
            TokenRecord first = sceneRepository.GetToken(campaign, a, TestCorrelationId).Value;
            b = sceneRepository.CreateToken(campaign, first.SceneId, new TokenPosition(3, 0), first.ControllerUserId, NewCommandId(), TestCorrelationId).Value.TokenId;
            c = sceneRepository.CreateToken(campaign, first.SceneId, new TokenPosition(6, 0), first.ControllerUserId, NewCommandId(), TestCorrelationId).Value.TokenId;
            Assert.That(presenter.Refresh().IsSuccess, Is.True);
            return presenter;
        }

        [Test]
        public void OwnMoveIsDrawnInstantly_MoveMadeElsewhereEasesToTheNewPosition()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            var campaignRepository = new SqliteCampaignRepository(Clock);
            var createRequest = new CreateCampaignRequest(directory.Path, "Board Motion Test Campaign", "ruleset.core", "1.0.0", "0.1.0", global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost());
            CampaignHandle campaign = campaignRepository.Create(createRequest, NewCommandId(), TestCorrelationId).Value;
            var sceneRepository = new SqliteSceneRepository(Clock);
            SceneId sceneId = sceneRepository.CreateScene(campaign, "Test Scene", NewCommandId(), TestCorrelationId).Value.SceneId;
            UserId localActor = NewUserId();
            TokenRecord token = sceneRepository.CreateToken(campaign, sceneId, new TokenPosition(0, 0), localActor, NewCommandId(), TestCorrelationId).Value;

            GameObject gameObject = new GameObject("Board Motion Document");
            try
            {
                UIDocument document = gameObject.AddComponent<UIDocument>();
                using var presenter = new BoardScreenPresenter(document, sceneRepository, campaign, campaignRepository, new SqliteObstacleRepository(Clock), new SqliteTokenVisionRepository(Clock), new SqliteFogOfWarRepository(Clock), sceneId, localActor);
                // ODY-S11-211 (TC-MOTION-006). Unfiltered render, so visibility rules play no part in what is checked here.
                presenter.LocalActorIsMainGm = true;
                Assert.That(presenter.Initialize().IsSuccess, Is.True);
                float PixelLeft(double worldX) => (float)(presenter.Camera.ToPixelsX(worldX) - 14.0);
                VisualElement Element() => document.rootVisualElement.Q<VisualElement>("token-" + token.TokenId)!;

                Assert.That(presenter.TryMoveTokenTo(token.TokenId, new TokenPosition(3, 0)).IsSuccess, Is.True);
                Assert.That(presenter.IsTokenAnimating(token.TokenId), Is.False, "the local user's own move is never delayed");
                Assert.That(Element().style.left.value.value, Is.EqualTo(PixelLeft(3)).Within(0.01f));

                // Another participant's move reaches storage; this board only sees it on its next render.
                long revision = sceneRepository.GetToken(campaign, token.TokenId, TestCorrelationId).Value.Revision;
                Assert.That(sceneRepository.MoveToken(campaign, token.TokenId, new TokenPosition(8, 0), revision, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);
                Assert.That(presenter.Refresh().IsSuccess, Is.True);
                Assert.That(presenter.IsTokenAnimating(token.TokenId), Is.True, "a move made elsewhere eases in");
                Assert.That(Element().style.left.value.value, Is.EqualTo(PixelLeft(3)).Within(0.01f), "starts where it was drawn");

                Assert.That(presenter.AdvanceTokenMotion(OdyMotion.RemoteUpdateDurationMs / 2), Is.True);
                float mid = Element().style.left.value.value;
                Assert.That(mid, Is.GreaterThan(PixelLeft(3)).And.LessThan(PixelLeft(8)));

                Assert.That(presenter.AdvanceTokenMotion(OdyMotion.RemoteUpdateDurationMs), Is.False);
                Assert.That(Element().style.left.value.value, Is.EqualTo(PixelLeft(8)).Within(0.01f), "arrives at the authoritative position");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
            }
        }

        [Test]
        public void FocusOnCharacter_EasesTheCameraToAnOffScreenToken_ManualPanInterrupts_HiddenTokensAreNeverFocused()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            var campaignRepository = new SqliteCampaignRepository(Clock);
            var createRequest = new CreateCampaignRequest(directory.Path, "Board Focus Test Campaign", "ruleset.core", "1.0.0", "0.1.0", global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost());
            CampaignHandle campaign = campaignRepository.Create(createRequest, NewCommandId(), TestCorrelationId).Value;
            var sceneRepository = new SqliteSceneRepository(Clock);
            SceneId sceneId = sceneRepository.CreateScene(campaign, "Test Scene", NewCommandId(), TestCorrelationId).Value.SceneId;
            UserId localActor = NewUserId();
            CharacterId nearCharacter = CharacterId.NewId(Clock.GetUtcNow());
            CharacterId farCharacter = CharacterId.NewId(Clock.GetUtcNow());
            // Neither token is controlled by the local actor, so as a player they see none of them (see the end of the test).
            sceneRepository.CreateToken(campaign, sceneId, new TokenPosition(0, 0), NewUserId(), NewCommandId(), TestCorrelationId, nearCharacter);
            sceneRepository.CreateToken(campaign, sceneId, new TokenPosition(200, 150), NewUserId(), NewCommandId(), TestCorrelationId, farCharacter);

            GameObject gameObject = new GameObject("Board Focus Document");
            try
            {
                UIDocument document = gameObject.AddComponent<UIDocument>();
                using var presenter = new BoardScreenPresenter(document, sceneRepository, campaign, campaignRepository, new SqliteObstacleRepository(Clock), new SqliteTokenVisionRepository(Clock), new SqliteFogOfWarRepository(Clock), sceneId, localActor);
                // ODY-S11-214 (TC-CAMFOCUS-005). Unfiltered render first: both tokens are visible.
                presenter.LocalActorIsMainGm = true;
                Assert.That(presenter.Initialize().IsSuccess, Is.True);

                Assert.That(presenter.FocusOnCharacter(nearCharacter), Is.EqualTo(BoardFocusOutcome.AlreadyInFrame), "the default camera already frames the origin");
                Assert.That(presenter.FocusOnCharacter(CharacterId.NewId(Clock.GetUtcNow())), Is.EqualTo(BoardFocusOutcome.NotOnBoard));

                Assert.That(presenter.FocusOnCharacter(farCharacter), Is.EqualTo(BoardFocusOutcome.Started));
                Assert.That(presenter.IsCameraFocusing, Is.True);
                Assert.That(presenter.AdvanceCameraFocus(BoardCameraFocus.DurationMs / 2), Is.True);
                Assert.That(presenter.AdvanceCameraFocus(BoardCameraFocus.DurationMs), Is.False);
                Assert.That(presenter.Camera.ToPixelsX(200), Is.EqualTo(220.0).Within(1e-6), "centered in the 440px board");
                Assert.That(presenter.Camera.ToPixelsY(150), Is.EqualTo(220.0).Within(1e-6));

                Assert.That(presenter.FocusOnCharacter(nearCharacter), Is.EqualTo(BoardFocusOutcome.Started));
                presenter.BeginBoardPan(10, 10);
                Assert.That(presenter.IsCameraFocusing, Is.False, "the user's own camera input wins at once");
                presenter.EndBoardPan();
                double offsetAfterInterrupt = presenter.Camera.OffsetX;
                Assert.That(presenter.AdvanceCameraFocus(BoardCameraFocus.DurationMs), Is.False);
                Assert.That(presenter.Camera.OffsetX, Is.EqualTo(offsetAfterInterrupt), "no further autofocus movement");

                // A player who controls no token sees no token (ComputeVisibleTokens): focusing must not reveal one.
                presenter.LocalActorIsMainGm = false;
                Assert.That(presenter.Refresh().IsSuccess, Is.True);
                Assert.That(presenter.FocusOnCharacter(farCharacter), Is.EqualTo(BoardFocusOutcome.Hidden));
                Assert.That(presenter.IsCameraFocusing, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
            }
        }

        [Test]
        public void SelectedTokensAndTheSelectionBox_GetAMarchingAntsOutline_DeselectRemovesIt()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            var campaignRepository = new SqliteCampaignRepository(Clock);
            var createRequest = new CreateCampaignRequest(directory.Path, "Board Ants Test Campaign", "ruleset.core", "1.0.0", "0.1.0", global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost());
            CampaignHandle campaign = campaignRepository.Create(createRequest, NewCommandId(), TestCorrelationId).Value;
            var sceneRepository = new SqliteSceneRepository(Clock);
            SceneId sceneId = sceneRepository.CreateScene(campaign, "Test Scene", NewCommandId(), TestCorrelationId).Value.SceneId;
            UserId localActor = NewUserId();
            TokenRecord token = sceneRepository.CreateToken(campaign, sceneId, new TokenPosition(0, 0), localActor, NewCommandId(), TestCorrelationId).Value;

            GameObject gameObject = new GameObject("Board Ants Document");
            try
            {
                UIDocument document = gameObject.AddComponent<UIDocument>();
                using var presenter = new BoardScreenPresenter(document, sceneRepository, campaign, campaignRepository, new SqliteObstacleRepository(Clock), new SqliteTokenVisionRepository(Clock), new SqliteFogOfWarRepository(Clock), sceneId, localActor);
                // ODY-S11-216 (TC-ANTS-004). Unfiltered render, so visibility rules play no part here.
                presenter.LocalActorIsMainGm = true;
                Assert.That(presenter.Initialize().IsSuccess, Is.True);
                Assert.That(presenter.SelectionOutline(token.TokenId), Is.Null, "nothing selected, no outline");

                presenter.SelectToken(token.TokenId);
                OdyMarchingAnts? outline = presenter.SelectionOutline(token.TokenId);
                Assert.That(outline, Is.Not.Null);
                Assert.That(outline!.Element.parent, Is.SameAs(document.rootVisualElement.Q<VisualElement>("token-" + token.TokenId)), "drawn on the selected token");

                presenter.SelectToken(token.TokenId); // a plain click on the only selected token clears the selection
                Assert.That(presenter.SelectionOutline(token.TokenId), Is.Null);
                Assert.That(outline.Element.parent, Is.Null, "removed with the selection");

                presenter.BeginBoardPointerGesture(300, 300);
                presenter.MoveBoardPointer(380, 360);
                Assert.That(presenter.SelectionBoxElement, Is.Not.Null);
                Assert.That(presenter.SelectionBoxOutline!.Element.parent, Is.SameAs(presenter.SelectionBoxElement), "the box marches too");
                presenter.EndBoardPointerGesture(380, 360);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
            }
        }

        private static void ClickToken(BoardScreenPresenter presenter, TokenId id, double worldX, bool shift)
        {
            double x = presenter.Camera.ToPixelsX(worldX);
            double y = presenter.Camera.ToPixelsY(0);
            presenter.BeginTokenDrag(id, x, y, shift);
            presenter.EndTokenDrag(id, x, y);
        }

        [Test] // TC-BOARD-086
        public void ShiftClick_OnAnUnselectedToken_AddsItToTheSelection_KeepingTheOthers()
        {
            BoardScreenPresenter presenter = BuildPresenterWithThreeTokens(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId a, out TokenId b, out TokenId c, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                presenter.SelectToken(a);
                ClickToken(presenter, b, 3, true);

                Assert.That(presenter.SelectedTokenIds, Is.EquivalentTo(new[] { a, b }));
                ClickToken(presenter, c, 6, true);
                Assert.That(presenter.SelectedTokenIds, Is.EquivalentTo(new[] { a, b, c }));
                Assert.That(sceneRepository.MoveTokenCalls, Is.EqualTo(0));
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        [Test] // TC-BOARD-087
        public void ShiftClick_OnASelectedToken_RemovesExactlyThatToken_LeavingTheRestSelected()
        {
            BoardScreenPresenter presenter = BuildPresenterWithThreeTokens(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId a, out TokenId b, out TokenId c, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                ClickToken(presenter, a, 0, true);
                ClickToken(presenter, b, 3, true);
                ClickToken(presenter, c, 6, true);
                Assert.That(presenter.SelectedTokenIds, Is.EquivalentTo(new[] { a, b, c }));

                ClickToken(presenter, b, 3, true);

                Assert.That(presenter.SelectedTokenIds, Is.EquivalentTo(new[] { a, c }));
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        [Test] // TC-BOARD-088
        public void PlainClick_OnAnyToken_ReplacesTheWholeSelection_AndOnTheOnlySelectedTokenClearsIt()
        {
            BoardScreenPresenter presenter = BuildPresenterWithThreeTokens(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId a, out TokenId b, out TokenId c, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                ClickToken(presenter, a, 0, true);
                ClickToken(presenter, b, 3, true);
                Assert.That(presenter.SelectedTokenIds, Is.EquivalentTo(new[] { a, b }));

                ClickToken(presenter, c, 6, false);
                Assert.That(presenter.SelectedTokenIds, Is.EquivalentTo(new[] { c }), "a plain click on an unselected token selects only it");

                ClickToken(presenter, a, 0, true);
                Assert.That(presenter.SelectedTokenIds, Is.EquivalentTo(new[] { c, a }));
                ClickToken(presenter, a, 0, false);
                Assert.That(presenter.SelectedTokenIds, Is.EquivalentTo(new[] { a }), "a plain click on a token inside a larger selection also collapses it to just that token");

                ClickToken(presenter, a, 0, false);
                Assert.That(presenter.SelectedTokenIds, Is.Empty, "a plain click on the only selected token clears the selection, as before");
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        [Test] // TC-BOARD-089
        public void ClickOnEmptyBoard_WithTwoOrMoreSelected_ClearsTheSelection_AndMovesNothing()
        {
            BoardScreenPresenter presenter = BuildPresenterWithThreeTokens(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId a, out TokenId b, out TokenId c, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                ClickToken(presenter, a, 0, true);
                ClickToken(presenter, b, 3, true);

                const double px = 300.0;
                const double py = 260.0;
                presenter.BeginBoardPointerGesture(px, py);
                presenter.EndBoardPointerGesture(px, py);

                Assert.That(presenter.SelectedTokenIds, Is.Empty);
                Assert.That(sceneRepository.MoveTokenCalls, Is.EqualTo(0));
                Assert.That(sceneRepository.GetToken(campaign, a, TestCorrelationId).Value.Position.X, Is.EqualTo(0));
                Assert.That(sceneRepository.GetToken(campaign, b, TestCorrelationId).Value.Position.X, Is.EqualTo(3));
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        [Test] // TC-BOARD-090
        public void ClickOnEmptyBoard_WithExactlyOneSelected_StillMovesThatToken()
        {
            BoardScreenPresenter presenter = BuildPresenterWithThreeTokens(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId a, out TokenId b, out TokenId c, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                presenter.SelectToken(a);

                const double px = 300.0;
                const double py = 260.0;
                presenter.BeginBoardPointerGesture(px, py);
                presenter.EndBoardPointerGesture(px, py);

                Assert.That(sceneRepository.MoveTokenCalls, Is.EqualTo(1));
                Assert.That(sceneRepository.GetToken(campaign, a, TestCorrelationId).Value.Position.X, Is.EqualTo(presenter.Camera.FromPixelsX(px)).Within(1e-9));
                Assert.That(presenter.SelectedTokenIds, Is.Empty, "the move clears the selection exactly as before");
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        [Test] // TC-BOARD-091
        public void DraggingATokenOfATwoTokenSelection_MovesBothByTheSameDelta_WithNoRepositoryMoveBeforeRelease()
        {
            BoardScreenPresenter presenter = BuildPresenterWithThreeTokens(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId a, out TokenId b, out TokenId c, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                ClickToken(presenter, a, 0, true);
                ClickToken(presenter, b, 3, true);

                double startX = presenter.Camera.ToPixelsX(0);
                double startY = presenter.Camera.ToPixelsY(0);
                double endX = startX + 80.0; // +2 world units at the default 40 px/unit
                presenter.BeginTokenDrag(a, startX, startY);
                presenter.MoveTokenDrag(a, endX, startY);

                Assert.That(sceneRepository.MoveTokenCalls, Is.EqualTo(0), "no repository move before the pointer is released");
                Assert.That(sceneRepository.GetToken(campaign, b, TestCorrelationId).Value.Position.X, Is.EqualTo(3), "the persisted position of the other group member is untouched mid-drag");
                float bLeftMid = document.rootVisualElement.Q<VisualElement>("token-" + b)!.style.left.value.value;
                Assert.That(bLeftMid, Is.EqualTo((float)(presenter.Camera.ToPixelsX(5) - 14.0)).Within(0.01f), "the other member's preview follows by the same delta");

                presenter.EndTokenDrag(a, endX, startY);

                Assert.That(sceneRepository.MoveTokenCalls, Is.EqualTo(2), "one ordinary MoveToken per group member");
                Assert.That(sceneRepository.GetToken(campaign, a, TestCorrelationId).Value.Position.X, Is.EqualTo(2.0).Within(1e-9));
                Assert.That(sceneRepository.GetToken(campaign, b, TestCorrelationId).Value.Position.X, Is.EqualTo(5.0).Within(1e-9));
                Assert.That(sceneRepository.GetToken(campaign, c, TestCorrelationId).Value.Position.X, Is.EqualTo(6.0), "an unselected token does not move");
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        [Test] // TC-BOARD-092
        public void DraggingATokenOutsideTheSelection_ReplacesTheSelectionWithIt_AndMovesOnlyIt()
        {
            BoardScreenPresenter presenter = BuildPresenterWithThreeTokens(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId a, out TokenId b, out TokenId c, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                ClickToken(presenter, a, 0, true);
                ClickToken(presenter, b, 3, true);

                double startX = presenter.Camera.ToPixelsX(6);
                double startY = presenter.Camera.ToPixelsY(0);
                presenter.BeginTokenDrag(c, startX, startY);
                presenter.MoveTokenDrag(c, startX, startY + 80.0);
                Assert.That(presenter.SelectedTokenIds, Is.EquivalentTo(new[] { c }), "the grabbed token becomes the selection as soon as the drag starts");
                presenter.EndTokenDrag(c, startX, startY + 80.0);

                Assert.That(sceneRepository.MoveTokenCalls, Is.EqualTo(1));
                Assert.That(sceneRepository.GetToken(campaign, c, TestCorrelationId).Value.Position.Y, Is.EqualTo(2.0).Within(1e-9));
                Assert.That(sceneRepository.GetToken(campaign, a, TestCorrelationId).Value.Position.X, Is.EqualTo(0));
                Assert.That(sceneRepository.GetToken(campaign, b, TestCorrelationId).Value.Position.X, Is.EqualTo(3));
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        [Test] // TC-BOARD-093
        public void GroupDrag_WithOneDeniedMember_KeepsTheAcceptedMoveCommitted_AndRollsBackOnlyTheDeniedOne()
        {
            BoardScreenPresenter presenter = BuildPresenterWithThreeTokens(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId a, out TokenId b, out TokenId c, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                ClickToken(presenter, a, 0, true);
                ClickToken(presenter, b, 3, true);

                // Dragging by +2: a goes to (2,0), free; b goes to (5,0). Put an occupant there first.
                TokenRecord first = sceneRepository.GetToken(campaign, a, TestCorrelationId).Value;
                sceneRepository.CreateToken(campaign, first.SceneId, new TokenPosition(5, 0), first.ControllerUserId, NewCommandId(), TestCorrelationId);
                Assert.That(presenter.Refresh().IsSuccess, Is.True);
                float bOriginalLeft = document.rootVisualElement.Q<VisualElement>("token-" + b)!.style.left.value.value;

                double startX = presenter.Camera.ToPixelsX(0);
                double startY = presenter.Camera.ToPixelsY(0);
                double endX = startX + 80.0;
                presenter.BeginTokenDrag(a, startX, startY);
                presenter.MoveTokenDrag(a, endX, startY);
                presenter.EndTokenDrag(a, endX, startY);

                Assert.That(sceneRepository.GetToken(campaign, a, TestCorrelationId).Value.Position.X, Is.EqualTo(2.0).Within(1e-9), "the accepted member stays moved and committed");
                Assert.That(sceneRepository.GetToken(campaign, b, TestCorrelationId).Value.Position.X, Is.EqualTo(3.0), "the denied member was not moved");
                VisualElement bAfter = document.rootVisualElement.Q<VisualElement>("token-" + b)!;
                Assert.That(bAfter.style.left.value.value, Is.EqualTo(bOriginalLeft).Within(0.01f), "the denied member is visually rolled back");
                VisualElement aAfter = document.rootVisualElement.Q<VisualElement>("token-" + a)!;
                Assert.That(aAfter.style.left.value.value, Is.EqualTo((float)(presenter.Camera.ToPixelsX(2) - 14.0)).Within(0.01f), "the accepted member is rendered at its committed position");
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        // ---- ODY-S08-107: board controls (box selection, Shift-click, middle-button pan, right-button marker) ----

        // Drags a box between two world-coordinate corners with the left button (through the same public
        // entry points a real drag ends up in).
        private static void DragBox(BoardScreenPresenter presenter, double worldX1, double worldY1, double worldX2, double worldY2, bool shift)
        {
            double x1 = presenter.Camera.ToPixelsX(worldX1);
            double y1 = presenter.Camera.ToPixelsY(worldY1);
            double x2 = presenter.Camera.ToPixelsX(worldX2);
            double y2 = presenter.Camera.ToPixelsY(worldY2);
            presenter.BeginBoardPointerGesture(x1, y1, shift);
            presenter.MoveBoardPointer((x1 + x2) / 2.0, (y1 + y2) / 2.0);
            presenter.MoveBoardPointer(x2, y2);
            presenter.EndBoardPointerGesture(x2, y2);
        }

        [Test] // TC-BOARD-094
        public void BoxSelect_LeftDragOnEmptyBoard_SelectsEveryTokenInside_ReplacingThePreviousSelection()
        {
            BoardScreenPresenter presenter = BuildPresenterWithThreeTokens(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId a, out TokenId b, out TokenId c, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                presenter.SelectToken(c);

                double x1 = presenter.Camera.ToPixelsX(-1);
                double y1 = presenter.Camera.ToPixelsY(-1);
                double x2 = presenter.Camera.ToPixelsX(4);
                double y2 = presenter.Camera.ToPixelsY(1);
                presenter.BeginBoardPointerGesture(x1, y1);
                presenter.MoveBoardPointer(x2, y2);
                Assert.That(presenter.SelectionBoxElement, Is.Not.Null, "a box is drawn while dragging");
                Assert.That(presenter.SelectionBoxElement!.style.width.value.value, Is.EqualTo((float)(x2 - x1)).Within(0.01f));
                presenter.EndBoardPointerGesture(x2, y2);

                Assert.That(presenter.SelectedTokenIds, Is.EquivalentTo(new[] { a, b }), "the tokens inside the box replace the old selection");
                Assert.That(presenter.SelectionBoxElement, Is.Null, "the box is gone after release");
                Assert.That(sceneRepository.MoveTokenCalls, Is.EqualTo(0), "a box drag never moves a token");
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        [Test] // TC-BOARD-095
        public void BoxSelect_WithShift_AddsTheTokensInsideToTheExistingSelection()
        {
            BoardScreenPresenter presenter = BuildPresenterWithThreeTokens(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId a, out TokenId b, out TokenId c, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                presenter.SelectToken(c);

                DragBox(presenter, -1, -1, 1, 1, true); // contains only a

                Assert.That(presenter.SelectedTokenIds, Is.EquivalentTo(new[] { c, a }), "Shift keeps the existing selection and adds the boxed token");
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        [Test] // TC-BOARD-096
        public void BoxSelect_DoesNotSelectTokensOutsideTheBox_AndUsesWorldCoordinatesAfterPanAndZoom()
        {
            BoardScreenPresenter presenter = BuildPresenterWithThreeTokens(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId a, out TokenId b, out TokenId c, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                DragBox(presenter, 2, -1, 4, 1, false); // contains only b
                Assert.That(presenter.SelectedTokenIds, Is.EquivalentTo(new[] { b }), "a and c are outside the box");

                // Pan and zoom the camera so pixels no longer equal the original layout, then box a again:
                // the box is compared in world coordinates, so only a is selected.
                presenter.BeginBoardPan(100, 100);
                presenter.MoveBoardPan(170, 130);
                presenter.EndBoardPan();
                presenter.ZoomBoard(1.5, 200, 200);
                DragBox(presenter, -0.5, -0.5, 0.5, 0.5, false);

                Assert.That(presenter.SelectedTokenIds, Is.EquivalentTo(new[] { a }));
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        [Test] // TC-BOARD-097
        public void ShiftClick_AddsAndRemovesFromTheSelection_AndCtrlIsNoLongerSpecial()
        {
            BoardScreenPresenter presenter = BuildPresenterWithThreeTokens(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId a, out TokenId b, out TokenId c, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                presenter.SelectToken(a);
                ClickToken(presenter, b, 3, true);
                Assert.That(presenter.SelectedTokenIds, Is.EquivalentTo(new[] { a, b }), "Shift+click adds");
                ClickToken(presenter, b, 3, true);
                Assert.That(presenter.SelectedTokenIds, Is.EquivalentTo(new[] { a }), "Shift+click on a selected token removes exactly it");

                // Ctrl is not read anywhere any more: a Ctrl+click reaches BeginTokenDrag with shift == false,
                // i.e. it is a plain click and replaces the selection.
                ClickToken(presenter, b, 3, true);
                Assert.That(presenter.SelectedTokenIds, Is.EquivalentTo(new[] { a, b }));
                ClickToken(presenter, c, 6, false);
                Assert.That(presenter.SelectedTokenIds, Is.EquivalentTo(new[] { c }), "without Shift a click replaces the whole selection");
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        [Test] // TC-BOARD-098
        public void ShiftWheel_ScalesTheToken_WithoutTouchingTheSelection_AndShiftClick_DoesNotScale()
        {
            BoardScreenPresenter presenter = BuildPresenterWithThreeTokens(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId a, out TokenId b, out TokenId c, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                presenter.SelectToken(a);
                ClickToken(presenter, b, 3, true); // selection {a, b}
                double x = presenter.Camera.ToPixelsX(0);
                double y = presenter.Camera.ToPixelsY(0);

                Assert.That(sceneRepository.SetTokenScaleCalls, Is.EqualTo(0), "a Shift+click never scales");
                Assert.That(sceneRepository.GetToken(campaign, b, TestCorrelationId).Value.Scale, Is.EqualTo(1.0));

                presenter.HandleBoardWheel(-1.0, true, x, y);

                Assert.That(sceneRepository.GetToken(campaign, a, TestCorrelationId).Value.Scale, Is.EqualTo(1.1).Within(1e-9), "Shift+wheel still scales the token under the pointer");
                Assert.That(presenter.SelectedTokenIds, Is.EquivalentTo(new[] { a, b }), "and does not change the selection");
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        [Test] // TC-BOARD-099
        public void MiddleButton_OnEmptyBoard_PansTheCamera_WithoutStartingABoxOrTouchingTokens()
        {
            BoardScreenPresenter presenter = BuildPresenterWithThreeTokens(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId a, out TokenId b, out TokenId c, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                presenter.SelectToken(b);
                double before = presenter.Camera.ToPixelsX(0);

                Assert.That(presenter.HandleBoardButtonDown(1, 300, 300, false), Is.True);
                Assert.That(presenter.ActiveBoardButton, Is.EqualTo(1));
                presenter.MoveBoardPan(340, 300);
                Assert.That(presenter.SelectionBoxElement, Is.Null, "a middle drag never draws a selection box");
                Assert.That(presenter.HandleBoardButtonUp(1, 340, 300), Is.True);

                Assert.That(presenter.Camera.ToPixelsX(0), Is.EqualTo(before + 40.0).Within(1e-9));
                Assert.That(presenter.SelectedTokenIds, Is.EquivalentTo(new[] { b }), "the selection is untouched");
                Assert.That(sceneRepository.MoveTokenCalls, Is.EqualTo(0));
                Assert.That(presenter.ActiveBoardButton, Is.EqualTo(-1));
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        [Test] // TC-BOARD-100
        public void MiddleButton_OverAToken_PansTheCamera_AndNeitherSelectsNorMovesThatToken()
        {
            BoardScreenPresenter presenter = BuildPresenterWithThreeTokens(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId a, out TokenId b, out TokenId c, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                double tokenX = presenter.Camera.ToPixelsX(0);
                double tokenY = presenter.Camera.ToPixelsY(0);
                double before = presenter.Camera.ToPixelsX(0);

                // The token's own handler ignores a non-left press (it neither captures nor stops the event),
                // so the press reaches the board, which starts the pan.
                Assert.That(presenter.HandleBoardButtonDown(1, tokenX, tokenY, false), Is.True);
                presenter.MoveBoardPan(tokenX + 50.0, tokenY);
                presenter.HandleBoardButtonUp(1, tokenX + 50.0, tokenY);

                Assert.That(presenter.Camera.ToPixelsX(0), Is.EqualTo(before + 50.0).Within(1e-9));
                Assert.That(presenter.SelectedTokenIds, Is.Empty, "the token under the cursor is not selected");
                Assert.That(sceneRepository.MoveTokenCalls, Is.EqualTo(0));
                Assert.That(sceneRepository.GetToken(campaign, a, TestCorrelationId).Value.Position.X, Is.EqualTo(0), "and is not moved");
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        [Test] // TC-BOARD-101
        public void RightButton_PlacesALocalMarkerAtTheWorldPosition_AndItFollowsThePanAndZoom()
        {
            BoardScreenPresenter presenter = BuildPresenterWithThreeTokens(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId a, out TokenId b, out TokenId c, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                const double px = 250.0;
                const double py = 180.0;
                Assert.That(presenter.PlayerMarkerWorldPosition, Is.Null);

                Assert.That(presenter.HandleBoardButtonDown(2, px, py, false), Is.True);

                TokenPosition? world = presenter.PlayerMarkerWorldPosition;
                Assert.That(world.HasValue, Is.True);
                Assert.That(world!.Value.X, Is.EqualTo(presenter.Camera.FromPixelsX(px)).Within(1e-9));
                Assert.That(world.Value.Y, Is.EqualTo(presenter.Camera.FromPixelsY(py)).Within(1e-9));
                VisualElement marker = presenter.PlayerMarkerElement!;
                float half = marker.style.width.value.value / 2f;
                Assert.That(marker.style.left.value.value + half, Is.EqualTo((float)px).Within(0.01f));
                Assert.That(marker.style.top.value.value + half, Is.EqualTo((float)py).Within(0.01f));
                Assert.That(presenter.ActiveBoardButton, Is.EqualTo(-1), "a right press is a single event, it owns no gesture");
                Assert.That(sceneRepository.MoveTokenCalls, Is.EqualTo(0));

                presenter.BeginBoardPan(100, 100);
                presenter.MoveBoardPan(160, 100);
                presenter.EndBoardPan();
                Assert.That(marker.style.left.value.value + half, Is.EqualTo((float)(px + 60.0)).Within(0.01f), "the marker follows the camera pan");
                Assert.That(presenter.PlayerMarkerWorldPosition!.Value.X, Is.EqualTo(world.Value.X), "its world position does not change");

                presenter.HandleBoardButtonDown(2, 10, 10, false);
                Assert.That(presenter.PlayerMarkerWorldPosition!.Value.X, Is.Not.EqualTo(world.Value.X), "a second right press replaces the marker");

                presenter.ClearPlayerMarker();
                Assert.That(presenter.PlayerMarkerWorldPosition, Is.Null);
                Assert.That(presenter.PlayerMarkerElement, Is.Null);
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        [Test] // TC-BOARD-102
        public void OnlyOneMouseButtonGestureRunsAtATime_AndAnotherButtonsReleaseIsIgnored()
        {
            BoardScreenPresenter presenter = BuildPresenterWithThreeTokens(out CampaignHandle campaign, out MoveCountingSceneRepository sceneRepository, out TokenId a, out TokenId b, out TokenId c, out UIDocument document, out GameObject gameObject, out SqliteCampaignRepository campaignRepository, out TemporaryDirectory directory);
            try
            {
                double before = presenter.Camera.ToPixelsX(0);

                Assert.That(presenter.HandleBoardButtonDown(0, 300, 300, false), Is.True);
                Assert.That(presenter.HandleBoardButtonDown(1, 300, 300, false), Is.False, "a middle press during a left gesture is refused");
                Assert.That(presenter.HandleBoardButtonDown(2, 300, 300, false), Is.False, "and so is a right press");
                Assert.That(presenter.PlayerMarkerWorldPosition, Is.Null);
                Assert.That(presenter.ActiveBoardButton, Is.EqualTo(0));

                Assert.That(presenter.HandleBoardButtonUp(1, 320, 300), Is.False, "the release of a button that owns nothing is ignored");
                Assert.That(presenter.ActiveBoardButton, Is.EqualTo(0), "the left gesture is still running");
                Assert.That(presenter.Camera.ToPixelsX(0), Is.EqualTo(before), "no pan happened");

                presenter.MoveBoardPointer(360, 340);
                Assert.That(presenter.SelectionBoxElement, Is.Not.Null, "the left gesture is still a box drag");
                Assert.That(presenter.HandleBoardButtonUp(0, 360, 340), Is.True);
                Assert.That(presenter.ActiveBoardButton, Is.EqualTo(-1));

                // And the other way round: a left press during a middle pan is refused.
                Assert.That(presenter.HandleBoardButtonDown(1, 100, 100, false), Is.True);
                Assert.That(presenter.HandleBoardButtonDown(0, 100, 100, false), Is.False);
                Assert.That(presenter.HandleBoardButtonUp(0, 100, 100), Is.False);
                Assert.That(presenter.HandleBoardButtonUp(1, 100, 100), Is.True);
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                campaignRepository.Close(campaign, TestCorrelationId);
                directory.Dispose();
            }
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "odyssey-board-screen-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public void Dispose()
            {
                // Windows can briefly hold the SQLite connection-pool file handle open past
                // Dispose() of the last SqliteConnection using it; retry the delete rather
                // than fail the test on an unrelated cleanup race.
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    try
                    {
                        if (Directory.Exists(Path)) Directory.Delete(Path, true);
                        return;
                    }
                    catch (IOException)
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                }
            }
        }

        // ---- ODY-S08-101: texture rendering ------------------------------------------

        private static readonly Color LocalTokenFallbackColor = new Color(0.25f, 0.65f, 0.95f);

        private static byte[] MakePng(int size, Color32 color)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[size * size];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
                texture.SetPixels32(pixels);
                texture.Apply();
                return texture.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static AssetManifestEntryRecord RegisterAssetBytes(ISceneRepository repository, CampaignHandle campaign, byte[] content)
        {
            string sourceFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "odyssey-board-asset-" + Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(sourceFile, content);
            try
            {
                Result<AssetManifestEntryRecord> registered = repository.RegisterAsset(campaign, sourceFile, NewCommandId(), TestCorrelationId);
                Assert.That(registered.IsSuccess, Is.True, "fixture asset registration must itself succeed");
                return registered.Value;
            }
            finally
            {
                File.Delete(sourceFile);
            }
        }

        private sealed class BoardFixture : IDisposable
        {
            private readonly TemporaryDirectory _directory = new TemporaryDirectory();
            private readonly SqliteCampaignRepository _campaignRepository = new SqliteCampaignRepository(Clock);

            public BoardFixture()
            {
                var createRequest = new CreateCampaignRequest(_directory.Path, "Board Texture Test Campaign", "ruleset.core", "1.0.0", "0.1.0", global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost());
                Campaign = _campaignRepository.Create(createRequest, NewCommandId(), TestCorrelationId).Value;
                Repository = new SqliteSceneRepository(Clock);
                ObstacleRepository = new SqliteObstacleRepository(Clock);
                VisionRepository = new SqliteTokenVisionRepository(Clock);
                FogRepository = new SqliteFogOfWarRepository(Clock);
                Scene = Repository.CreateScene(Campaign, "Test Scene", NewCommandId(), TestCorrelationId).Value;
                _sceneRevision = Scene.Revision;
                LocalActor = NewUserId();
                Token = Repository.CreateToken(Campaign, Scene.SceneId, new TokenPosition(0, 0), LocalActor, NewCommandId(), TestCorrelationId).Value;
            }

            public CampaignHandle Campaign { get; }
            public SqliteCampaignRepository CampaignRepository => _campaignRepository;
            public SqliteSceneRepository Repository { get; }
            public SqliteObstacleRepository ObstacleRepository { get; }
            public SqliteTokenVisionRepository VisionRepository { get; }
            public SqliteFogOfWarRepository FogRepository { get; }
            public SceneRecord Scene { get; }
            public UserId LocalActor { get; }
            public TokenRecord Token { get; private set; }
            private long _sceneRevision;

            public void SetBackground(AssetId? assetId)
            {
                Result<SceneRecord> result = Repository.SetSceneBackground(Campaign, Scene.SceneId, assetId, _sceneRevision, NewCommandId(), TestCorrelationId);
                Assert.That(result.IsSuccess, Is.True);
                _sceneRevision = result.Value.Revision;
            }

            public void SetTokenPortrait(AssetId assetId)
            {
                Result<TokenRecord> result = Repository.SetTokenPortrait(Campaign, Token.TokenId, assetId, Token.Revision, NewCommandId(), TestCorrelationId);
                Assert.That(result.IsSuccess, Is.True);
                Token = result.Value;
            }

            public void Dispose()
            {
                _campaignRepository.Close(Campaign, TestCorrelationId);
                _directory.Dispose();
            }
        }

        private static VisualElement Board(UIDocument document) => document.rootVisualElement.Q<VisualElement>("board-area");
        private static VisualElement TokenElement(UIDocument document, TokenRecord token) => document.rootVisualElement.Q<VisualElement>("token-" + token.TokenId);

        [Test] // TC-BOARD-040
        public void SceneWithBackgroundAssetId_AfterRefresh_BoardShowsTheTextureNotASolidFill()
        {
            using var fixture = new BoardFixture();
            fixture.SetBackground(RegisterAssetBytes(fixture.Repository, fixture.Campaign, MakePng(4, new Color32(200, 30, 30, 255))).AssetId);

            GameObject gameObject = new GameObject("Board Screen Document");
            try
            {
                UIDocument document = gameObject.AddComponent<UIDocument>();
                using var presenter = new BoardScreenPresenter(document, fixture.Repository, fixture.Campaign, fixture.CampaignRepository, fixture.ObstacleRepository, fixture.VisionRepository, fixture.FogRepository, fixture.Scene.SceneId, fixture.LocalActor);
                Assert.That(presenter.Initialize().IsSuccess, Is.True);

                Texture2D? texture = Board(document).style.backgroundImage.value.texture;
                Assert.That(texture, Is.Not.Null, "the board must display the registered image");
                Assert.That(texture!.width, Is.EqualTo(4), "and it must be the decoded registered image, not some placeholder");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test] // TC-BOARD-041
        public void SceneWithoutBackgroundAssetId_BoardStaysASolidFill_AsBefore()
        {
            using var fixture = new BoardFixture();

            GameObject gameObject = new GameObject("Board Screen Document");
            try
            {
                UIDocument document = gameObject.AddComponent<UIDocument>();
                using var presenter = new BoardScreenPresenter(document, fixture.Repository, fixture.Campaign, fixture.CampaignRepository, fixture.ObstacleRepository, fixture.VisionRepository, fixture.FogRepository, fixture.Scene.SceneId, fixture.LocalActor);
                Assert.That(presenter.Initialize().IsSuccess, Is.True);

                VisualElement board = Board(document);
                Assert.That(board.style.backgroundImage.value.texture, Is.Null);
                Assert.That(board.style.backgroundImage.keyword, Is.EqualTo(new VisualElement().style.backgroundImage.keyword), "indistinguishable from an element nobody styled: no inline image style is applied");
                Assert.That(board.style.backgroundColor.value, Is.EqualTo(new Color(0.12f, 0.12f, 0.14f)), "the solid fill is unchanged");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test] // TC-BOARD-047
        public void BackgroundClearedAfterBeingShown_NextRefreshRemovesTheTexture()
        {
            using var fixture = new BoardFixture();
            fixture.SetBackground(RegisterAssetBytes(fixture.Repository, fixture.Campaign, MakePng(4, new Color32(9, 9, 9, 255))).AssetId);

            GameObject gameObject = new GameObject("Board Screen Document");
            try
            {
                UIDocument document = gameObject.AddComponent<UIDocument>();
                using var presenter = new BoardScreenPresenter(document, fixture.Repository, fixture.Campaign, fixture.CampaignRepository, fixture.ObstacleRepository, fixture.VisionRepository, fixture.FogRepository, fixture.Scene.SceneId, fixture.LocalActor);
                Assert.That(presenter.Initialize().IsSuccess, Is.True);
                Assert.That(Board(document).style.backgroundImage.value.texture, Is.Not.Null, "precondition: the background is shown");

                fixture.SetBackground(null);
                Assert.That(presenter.Refresh().IsSuccess, Is.True);

                Assert.That(Board(document).style.backgroundImage.value.texture, Is.Null, "clearing the scene's background must clear the board image on the next Refresh");
                Assert.That(Board(document).style.backgroundColor.value, Is.EqualTo(new Color(0.12f, 0.12f, 0.14f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test] // TC-BOARD-042
        public void TokenWithPortraitAssetId_AfterRefresh_TokenShowsTheTextureNotTheOwnershipColor()
        {
            using var fixture = new BoardFixture();
            fixture.SetTokenPortrait(RegisterAssetBytes(fixture.Repository, fixture.Campaign, MakePng(8, new Color32(30, 200, 30, 255))).AssetId);

            GameObject gameObject = new GameObject("Board Screen Document");
            try
            {
                UIDocument document = gameObject.AddComponent<UIDocument>();
                using var presenter = new BoardScreenPresenter(document, fixture.Repository, fixture.Campaign, fixture.CampaignRepository, fixture.ObstacleRepository, fixture.VisionRepository, fixture.FogRepository, fixture.Scene.SceneId, fixture.LocalActor);
                Assert.That(presenter.Initialize().IsSuccess, Is.True);

                VisualElement tokenElement = TokenElement(document, fixture.Token);
                Texture2D? texture = tokenElement.style.backgroundImage.value.texture;
                Assert.That(texture, Is.Not.Null);
                Assert.That(texture!.width, Is.EqualTo(8));
                Assert.That(tokenElement.style.backgroundColor.value, Is.Not.EqualTo(LocalTokenFallbackColor), "the portrait replaces the solid fill");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test] // TC-BOARD-043
        public void TokenWithoutPortraitAssetId_StaysTheOwnershipColoredSquare_AsBefore()
        {
            using var fixture = new BoardFixture();

            GameObject gameObject = new GameObject("Board Screen Document");
            try
            {
                UIDocument document = gameObject.AddComponent<UIDocument>();
                using var presenter = new BoardScreenPresenter(document, fixture.Repository, fixture.Campaign, fixture.CampaignRepository, fixture.ObstacleRepository, fixture.VisionRepository, fixture.FogRepository, fixture.Scene.SceneId, fixture.LocalActor);
                Assert.That(presenter.Initialize().IsSuccess, Is.True);

                VisualElement tokenElement = TokenElement(document, fixture.Token);
                Assert.That(tokenElement.style.backgroundImage.value.texture, Is.Null);
                Assert.That(tokenElement.style.backgroundImage.keyword, Is.EqualTo(new VisualElement().style.backgroundImage.keyword), "indistinguishable from an element nobody styled");
                Assert.That(tokenElement.style.backgroundColor.value, Is.EqualTo(LocalTokenFallbackColor));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test] // TC-BOARD-044
        public void RepeatedRefreshAndMoves_WithTheSameAssetId_ReadTheAssetFromDiskOnlyOnce()
        {
            using var fixture = new BoardFixture();
            AssetId shared = RegisterAssetBytes(fixture.Repository, fixture.Campaign, MakePng(4, new Color32(10, 20, 200, 255))).AssetId;
            fixture.SetBackground(shared);
            fixture.SetTokenPortrait(shared);
            var counting = new CountingSceneRepository(fixture.Repository);

            GameObject gameObject = new GameObject("Board Screen Document");
            try
            {
                UIDocument document = gameObject.AddComponent<UIDocument>();
                using var presenter = new BoardScreenPresenter(document, counting, fixture.Campaign, fixture.CampaignRepository, fixture.ObstacleRepository, fixture.VisionRepository, fixture.FogRepository, fixture.Scene.SceneId, fixture.LocalActor);
                Assert.That(presenter.Initialize().IsSuccess, Is.True);
                Assert.That(presenter.Refresh().IsSuccess, Is.True);
                Assert.That(presenter.Refresh().IsSuccess, Is.True);

                presenter.SelectToken(fixture.Token.TokenId);
                Assert.That(presenter.TryMoveSelectedTokenTo(new TokenPosition(3, 2)).IsSuccess, Is.True);

                Assert.That(counting.ReadAssetContentCalls, Is.EqualTo(1), "background and portrait share one AssetId: one disk read for the presenter's whole life, however many Refresh calls");
                Assert.That(TokenElement(document, fixture.Token).style.backgroundImage.value.texture, Is.Not.Null, "the portrait is still shown after the move re-rendered the token");
                Assert.That(Board(document).style.backgroundImage.value.texture, Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test] // TC-BOARD-045
        public void MissingAssetFile_BoardStillRendersWithFallbackVisuals_AndRefreshReportsTheTypedError()
        {
            using var fixture = new BoardFixture();
            AssetManifestEntryRecord entry = RegisterAssetBytes(fixture.Repository, fixture.Campaign, MakePng(4, new Color32(1, 2, 3, 255)));
            fixture.SetBackground(entry.AssetId);
            fixture.SetTokenPortrait(entry.AssetId);
            File.Delete(System.IO.Path.Combine(fixture.Campaign.RootPath, entry.RelativePath.Replace('/', System.IO.Path.DirectorySeparatorChar)));

            GameObject gameObject = new GameObject("Board Screen Document");
            try
            {
                UIDocument document = gameObject.AddComponent<UIDocument>();
                using var presenter = new BoardScreenPresenter(document, fixture.Repository, fixture.Campaign, fixture.CampaignRepository, fixture.ObstacleRepository, fixture.VisionRepository, fixture.FogRepository, fixture.Scene.SceneId, fixture.LocalActor);

                Result result = presenter.Initialize();

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceAssetFileMissing));
                Assert.That(Board(document).style.backgroundImage.value.texture, Is.Null);
                VisualElement tokenElement = TokenElement(document, fixture.Token);
                Assert.That(tokenElement, Is.Not.Null, "the token must still be drawn");
                Assert.That(tokenElement.style.backgroundColor.value, Is.EqualTo(LocalTokenFallbackColor), "falling back to the ownership-colored square");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test] // TC-BOARD-046
        public void UndecodableImageBytes_ReportTypedDecodeError_AndFallBackToSolidVisuals()
        {
            using var fixture = new BoardFixture();
            // A real, hash-consistent asset whose bytes are simply not an image.
            AssetId notAnImage = RegisterAssetBytes(fixture.Repository, fixture.Campaign, System.Text.Encoding.UTF8.GetBytes("this is not an image " + Guid.NewGuid().ToString("N"))).AssetId;
            fixture.SetBackground(notAnImage);

            GameObject gameObject = new GameObject("Board Screen Document");
            try
            {
                UIDocument document = gameObject.AddComponent<UIDocument>();
                using var presenter = new BoardScreenPresenter(document, fixture.Repository, fixture.Campaign, fixture.CampaignRepository, fixture.ObstacleRepository, fixture.VisionRepository, fixture.FogRepository, fixture.Scene.SceneId, fixture.LocalActor);

                Result result = presenter.Initialize();

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.Error.UserMessageKey.ToString(), Is.EqualTo("errors.board_screen.texture_decode_failed"));
                Assert.That(Board(document).style.backgroundImage.value.texture, Is.Null);
                Assert.That(Board(document).style.backgroundColor.value, Is.EqualTo(new Color(0.12f, 0.12f, 0.14f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        /// <summary>Forwards every call to the real repository and counts <see cref="ReadAssetContent"/> -- a small, real interface implementation, not a mocking framework.</summary>
        private sealed class CountingSceneRepository : ISceneRepository
        {
            private readonly ISceneRepository _inner;

            public CountingSceneRepository(ISceneRepository inner)
            {
                _inner = inner;
            }

            public int ReadAssetContentCalls { get; private set; }

            public Result<byte[]> ReadAssetContent(CampaignHandle campaign, AssetId assetId, CorrelationId correlationId)
            {
                ReadAssetContentCalls++;
                return _inner.ReadAssetContent(campaign, assetId, correlationId);
            }

            public Result<SceneRecord> CreateScene(CampaignHandle campaign, string sceneName, CommandId commandId, CorrelationId correlationId) => _inner.CreateScene(campaign, sceneName, commandId, correlationId);
            public Result<TokenRecord> CreateToken(CampaignHandle campaign, SceneId sceneId, TokenPosition initialPosition, UserId controllerUserId, CommandId commandId, CorrelationId correlationId, CharacterId? characterId = null) => _inner.CreateToken(campaign, sceneId, initialPosition, controllerUserId, commandId, correlationId, characterId);
            public Result<TokenRecord> GetToken(CampaignHandle campaign, TokenId tokenId, CorrelationId correlationId) => _inner.GetToken(campaign, tokenId, correlationId);
            public Result<TokenRecord> MoveToken(CampaignHandle campaign, TokenId tokenId, TokenPosition newPosition, long expectedRevision, CommandId commandId, CorrelationId correlationId) => _inner.MoveToken(campaign, tokenId, newPosition, expectedRevision, commandId, correlationId);
            public Result<IReadOnlyList<TokenRecord>> ListTokens(CampaignHandle campaign, SceneId sceneId, CorrelationId correlationId) => _inner.ListTokens(campaign, sceneId, correlationId);
            public Result<IReadOnlyList<TokenRecord>> ListTokensByCharacter(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId) => _inner.ListTokensByCharacter(campaign, characterId, correlationId);
            public Result<AssetManifestEntryRecord> RegisterAsset(CampaignHandle campaign, string sourceFilePath, CommandId commandId, CorrelationId correlationId) => _inner.RegisterAsset(campaign, sourceFilePath, commandId, correlationId);
            public Result<SceneRecord> SetSceneBackground(CampaignHandle campaign, SceneId sceneId, AssetId? backgroundAssetId, long expectedRevision, CommandId commandId, CorrelationId correlationId) => _inner.SetSceneBackground(campaign, sceneId, backgroundAssetId, expectedRevision, commandId, correlationId);
            public Result<TokenRecord> SetTokenPortrait(CampaignHandle campaign, TokenId tokenId, AssetId? portraitAssetId, long expectedRevision, CommandId commandId, CorrelationId correlationId) => _inner.SetTokenPortrait(campaign, tokenId, portraitAssetId, expectedRevision, commandId, correlationId);
            public Result<TokenRecord> SetTokenZOrder(CampaignHandle campaign, TokenId tokenId, long zOrder, long expectedRevision, CommandId commandId, CorrelationId correlationId) => _inner.SetTokenZOrder(campaign, tokenId, zOrder, expectedRevision, commandId, correlationId);
            public Result<TokenRecord> SetTokenScale(CampaignHandle campaign, TokenId tokenId, double scale, long expectedRevision, CommandId commandId, CorrelationId correlationId) => _inner.SetTokenScale(campaign, tokenId, scale, expectedRevision, commandId, correlationId);
            public Result<SceneRecord> GetScene(CampaignHandle campaign, SceneId sceneId, CorrelationId correlationId) => _inner.GetScene(campaign, sceneId, correlationId);
            public Result<IReadOnlyList<AssetManifestEntryRecord>> ListAssets(CampaignHandle campaign, CorrelationId correlationId) => _inner.ListAssets(campaign, correlationId);
        }

        /// <summary>ODY-S08-104: forwards every call to the real repository and counts <see cref="MoveToken"/> -- proves a token drag commits exactly once, never once per <see cref="BoardScreenPresenter.MoveTokenDrag"/> call.</summary>
        private sealed class MoveCountingSceneRepository : ISceneRepository
        {
            private readonly ISceneRepository _inner;

            public MoveCountingSceneRepository(ISceneRepository inner)
            {
                _inner = inner;
            }

            public int MoveTokenCalls { get; private set; }

            public Result<TokenRecord> MoveToken(CampaignHandle campaign, TokenId tokenId, TokenPosition newPosition, long expectedRevision, CommandId commandId, CorrelationId correlationId)
            {
                MoveTokenCalls++;
                return _inner.MoveToken(campaign, tokenId, newPosition, expectedRevision, commandId, correlationId);
            }

            public Result<SceneRecord> CreateScene(CampaignHandle campaign, string sceneName, CommandId commandId, CorrelationId correlationId) => _inner.CreateScene(campaign, sceneName, commandId, correlationId);
            public Result<TokenRecord> CreateToken(CampaignHandle campaign, SceneId sceneId, TokenPosition initialPosition, UserId controllerUserId, CommandId commandId, CorrelationId correlationId, CharacterId? characterId = null) => _inner.CreateToken(campaign, sceneId, initialPosition, controllerUserId, commandId, correlationId, characterId);
            public Result<TokenRecord> GetToken(CampaignHandle campaign, TokenId tokenId, CorrelationId correlationId) => _inner.GetToken(campaign, tokenId, correlationId);
            public Result<IReadOnlyList<TokenRecord>> ListTokens(CampaignHandle campaign, SceneId sceneId, CorrelationId correlationId) => _inner.ListTokens(campaign, sceneId, correlationId);
            public Result<IReadOnlyList<TokenRecord>> ListTokensByCharacter(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId) => _inner.ListTokensByCharacter(campaign, characterId, correlationId);
            public Result<AssetManifestEntryRecord> RegisterAsset(CampaignHandle campaign, string sourceFilePath, CommandId commandId, CorrelationId correlationId) => _inner.RegisterAsset(campaign, sourceFilePath, commandId, correlationId);
            public Result<SceneRecord> SetSceneBackground(CampaignHandle campaign, SceneId sceneId, AssetId? backgroundAssetId, long expectedRevision, CommandId commandId, CorrelationId correlationId) => _inner.SetSceneBackground(campaign, sceneId, backgroundAssetId, expectedRevision, commandId, correlationId);
            public Result<TokenRecord> SetTokenPortrait(CampaignHandle campaign, TokenId tokenId, AssetId? portraitAssetId, long expectedRevision, CommandId commandId, CorrelationId correlationId) => _inner.SetTokenPortrait(campaign, tokenId, portraitAssetId, expectedRevision, commandId, correlationId);
            public int SetTokenZOrderCalls { get; private set; }
            public int SetTokenScaleCalls { get; private set; }

            public Result<TokenRecord> SetTokenZOrder(CampaignHandle campaign, TokenId tokenId, long zOrder, long expectedRevision, CommandId commandId, CorrelationId correlationId)
            {
                SetTokenZOrderCalls++;
                return _inner.SetTokenZOrder(campaign, tokenId, zOrder, expectedRevision, commandId, correlationId);
            }

            public Result<TokenRecord> SetTokenScale(CampaignHandle campaign, TokenId tokenId, double scale, long expectedRevision, CommandId commandId, CorrelationId correlationId)
            {
                SetTokenScaleCalls++;
                return _inner.SetTokenScale(campaign, tokenId, scale, expectedRevision, commandId, correlationId);
            }
            public Result<SceneRecord> GetScene(CampaignHandle campaign, SceneId sceneId, CorrelationId correlationId) => _inner.GetScene(campaign, sceneId, correlationId);
            public Result<byte[]> ReadAssetContent(CampaignHandle campaign, AssetId assetId, CorrelationId correlationId) => _inner.ReadAssetContent(campaign, assetId, correlationId);
            public Result<IReadOnlyList<AssetManifestEntryRecord>> ListAssets(CampaignHandle campaign, CorrelationId correlationId) => _inner.ListAssets(campaign, correlationId);
        }
    }
}
