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
            var createRequest = new CreateCampaignRequest(directory.Path, "Board Screen Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = campaignRepository.Create(createRequest, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            CampaignHandle campaign = created.Value;

            var sceneRepository = new SqliteSceneRepository(Clock);
            SceneId sceneId = sceneRepository.CreateScene(campaign, "Test Scene", NewCommandId(), TestCorrelationId).Value.SceneId;

            UserId localActor = NewUserId();
            TokenRecord ownToken = sceneRepository.CreateToken(campaign, sceneId, new TokenPosition(0, 0), localActor, NewCommandId(), TestCorrelationId).Value;

            GameObject gameObject = new GameObject("Board Screen Document");
            try
            {
                UIDocument document = gameObject.AddComponent<UIDocument>();
                using var presenter = new BoardScreenPresenter(document, sceneRepository, campaign, sceneId, localActor);
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
            var createRequest = new CreateCampaignRequest(directory.Path, "Board Screen Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
            CampaignHandle campaign = campaignRepository.Create(createRequest, NewCommandId(), TestCorrelationId).Value;

            var sceneRepository = new SqliteSceneRepository(Clock);
            SceneId sceneId = sceneRepository.CreateScene(campaign, "Test Scene", NewCommandId(), TestCorrelationId).Value.SceneId;

            UserId localActor = NewUserId();
            UserId otherController = NewUserId();
            TokenRecord foreignToken = sceneRepository.CreateToken(campaign, sceneId, new TokenPosition(1, 1), otherController, NewCommandId(), TestCorrelationId).Value;

            GameObject gameObject = new GameObject("Board Screen Document");
            try
            {
                UIDocument document = gameObject.AddComponent<UIDocument>();
                using var presenter = new BoardScreenPresenter(document, sceneRepository, campaign, sceneId, localActor);
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
            var createRequest = new CreateCampaignRequest(directory.Path, "Board Screen Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
            CampaignHandle campaign = campaignRepository.Create(createRequest, NewCommandId(), TestCorrelationId).Value;

            var sceneRepository = new SqliteSceneRepository(Clock);
            SceneId sceneId = sceneRepository.CreateScene(campaign, "Test Scene", NewCommandId(), TestCorrelationId).Value.SceneId;

            UserId mainGm = NewUserId();
            UserId otherController = NewUserId();
            TokenRecord foreignToken = sceneRepository.CreateToken(campaign, sceneId, new TokenPosition(1, 1), otherController, NewCommandId(), TestCorrelationId).Value;

            GameObject gameObject = new GameObject("Board Screen Document");
            try
            {
                UIDocument document = gameObject.AddComponent<UIDocument>();
                using var presenter = new BoardScreenPresenter(document, sceneRepository, campaign, sceneId, mainGm) { LocalActorIsMainGm = true };
                Assert.That(presenter.Initialize().IsSuccess, Is.True);

                presenter.SelectToken(foreignToken.TokenId);
                Result<TokenRecord> moved = presenter.TryMoveSelectedTokenTo(new TokenPosition(6, 6));

                Assert.That(moved.IsSuccess, Is.True, "MainGM must be authorized to move any token regardless of control");
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
            var createRequest = new CreateCampaignRequest(directory.Path, "Board Screen Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
            CampaignHandle campaign = campaignRepository.Create(createRequest, NewCommandId(), TestCorrelationId).Value;

            var sceneRepository = new SqliteSceneRepository(Clock);
            SceneId sceneId = sceneRepository.CreateScene(campaign, "Test Scene", NewCommandId(), TestCorrelationId).Value.SceneId;

            UserId localActor = NewUserId();
            TokenRecord tokenA = sceneRepository.CreateToken(campaign, sceneId, new TokenPosition(0, 0), localActor, NewCommandId(), TestCorrelationId).Value;
            TokenRecord tokenB = sceneRepository.CreateToken(campaign, sceneId, new TokenPosition(2, 3), NewUserId(), NewCommandId(), TestCorrelationId).Value;

            GameObject gameObject = new GameObject("Board Screen Document");
            try
            {
                UIDocument document = gameObject.AddComponent<UIDocument>();
                using var presenter = new BoardScreenPresenter(document, sceneRepository, campaign, sceneId, localActor);
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
                var createRequest = new CreateCampaignRequest(_directory.Path, "Board Texture Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
                Campaign = _campaignRepository.Create(createRequest, NewCommandId(), TestCorrelationId).Value;
                Repository = new SqliteSceneRepository(Clock);
                Scene = Repository.CreateScene(Campaign, "Test Scene", NewCommandId(), TestCorrelationId).Value;
                _sceneRevision = Scene.Revision;
                LocalActor = NewUserId();
                Token = Repository.CreateToken(Campaign, Scene.SceneId, new TokenPosition(0, 0), LocalActor, NewCommandId(), TestCorrelationId).Value;
            }

            public CampaignHandle Campaign { get; }
            public SqliteSceneRepository Repository { get; }
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
                using var presenter = new BoardScreenPresenter(document, fixture.Repository, fixture.Campaign, fixture.Scene.SceneId, fixture.LocalActor);
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
                using var presenter = new BoardScreenPresenter(document, fixture.Repository, fixture.Campaign, fixture.Scene.SceneId, fixture.LocalActor);
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
                using var presenter = new BoardScreenPresenter(document, fixture.Repository, fixture.Campaign, fixture.Scene.SceneId, fixture.LocalActor);
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
                using var presenter = new BoardScreenPresenter(document, fixture.Repository, fixture.Campaign, fixture.Scene.SceneId, fixture.LocalActor);
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
                using var presenter = new BoardScreenPresenter(document, fixture.Repository, fixture.Campaign, fixture.Scene.SceneId, fixture.LocalActor);
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
                using var presenter = new BoardScreenPresenter(document, counting, fixture.Campaign, fixture.Scene.SceneId, fixture.LocalActor);
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
                using var presenter = new BoardScreenPresenter(document, fixture.Repository, fixture.Campaign, fixture.Scene.SceneId, fixture.LocalActor);

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
                using var presenter = new BoardScreenPresenter(document, fixture.Repository, fixture.Campaign, fixture.Scene.SceneId, fixture.LocalActor);

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
            public Result<SceneRecord> GetScene(CampaignHandle campaign, SceneId sceneId, CorrelationId correlationId) => _inner.GetScene(campaign, sceneId, correlationId);
        }
    }
}
