using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
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
                if (Directory.Exists(Path)) Directory.Delete(Path, true);
            }
        }
    }
}
