using System;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;
using Odyssey.Persistence.Sqlite;
using Odyssey.Unity.Client;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Tests.Unity.EditMode
{
    public sealed class RoleSelectorPresenterTests
    {
        private static CorrelationId TestCorrelationId => CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly UnityWallClock Clock = new UnityWallClock();

        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId User(string suffix) => UserId.Parse("user_0000000000000000000000000000000" + suffix);

        [Test]
        public void RoleSelection_SelectPlayer_UpdatesBoardPresenterActor()
        {
            using TestBoard board = TestBoard.Create(BaselineRole.Observer);

            board.Selection.SelectRole(BaselineRole.Player);

            Assert.That(board.Presenter.LocalActorUserId, Is.EqualTo(board.Selection.PlayerUserId));
            Assert.That(board.Presenter.LocalActorIsMainGm, Is.False);
            Assert.That(board.Selection.ActorCanCreateRoll, Is.True);
            Assert.That(board.Document.rootVisualElement.Q<DropdownField>("role-selector-dropdown"), Is.Not.Null);
        }

        [Test]
        public void RoleSelection_SelectMainGm_UpdatesBoardPresenterActor()
        {
            using TestBoard board = TestBoard.Create(BaselineRole.Player);

            board.Selection.SelectRole(BaselineRole.MainGM);

            Assert.That(board.Presenter.LocalActorUserId, Is.EqualTo(board.Selection.MainGmUserId));
            Assert.That(board.Presenter.LocalActorIsMainGm, Is.True);
            Assert.That(board.Selection.ActorCanCreateRoll, Is.True);
        }

        [Test]
        public void RoleSelection_SelectObserver_ExposesObserverRoleAndCannotCreateRoll()
        {
            using TestBoard board = TestBoard.Create(BaselineRole.Player);

            board.Selector.SelectRole(BaselineRole.Observer);

            Assert.That(board.Selection.Role, Is.EqualTo(BaselineRole.Observer));
            Assert.That(board.Selection.ActorUserId, Is.EqualTo(board.Selection.ObserverUserId));
            Assert.That(board.Selection.ActorIsMainGm, Is.False);
            Assert.That(board.Selection.ActorCanCreateRoll, Is.False);
            Assert.That(board.Presenter.LocalActorUserId, Is.EqualTo(board.Selection.ObserverUserId));
            Assert.That(board.Presenter.LocalActorIsMainGm, Is.False);
        }

        [Test]
        public void RoleSelection_SwitchingRoles_DoesNotLeaveStaleValues()
        {
            using TestBoard board = TestBoard.Create(BaselineRole.Player);

            board.Selector.SelectRole(BaselineRole.MainGM);
            Assert.That(board.Selection.ActorIsMainGm, Is.True);

            board.Selector.SelectRole(BaselineRole.Observer);
            Assert.That(board.Selection.ActorUserId, Is.EqualTo(board.Selection.ObserverUserId));
            Assert.That(board.Selection.ActorIsMainGm, Is.False);
            Assert.That(board.Selection.ActorCanCreateRoll, Is.False);
            Assert.That(board.Presenter.LocalActorIsMainGm, Is.False);

            board.Selector.SelectRole(BaselineRole.Player);
            Assert.That(board.Selection.ActorUserId, Is.EqualTo(board.Selection.PlayerUserId));
            Assert.That(board.Selection.ActorIsMainGm, Is.False);
            Assert.That(board.Selection.ActorCanCreateRoll, Is.True);
            Assert.That(board.Presenter.LocalActorUserId, Is.EqualTo(board.Selection.PlayerUserId));
        }

        private sealed class TestBoard : IDisposable
        {
            private readonly TemporaryDirectory _directory;
            private readonly SqliteCampaignRepository _campaignRepository;
            private readonly GameObject _gameObject;
            private readonly PresentationRuntime _presentationRuntime;
            private bool _disposed;

            private TestBoard(TemporaryDirectory directory, SqliteCampaignRepository campaignRepository, CampaignHandle campaign, GameObject gameObject, UIDocument document, PresentationRuntime presentationRuntime, RoleSelection selection, BoardScreenPresenter presenter, RoleSelectorPresenter selector)
            {
                _directory = directory;
                _campaignRepository = campaignRepository;
                Campaign = campaign;
                _gameObject = gameObject;
                Document = document;
                _presentationRuntime = presentationRuntime;
                Selection = selection;
                Presenter = presenter;
                Selector = selector;
            }

            public CampaignHandle Campaign { get; }
            public UIDocument Document { get; }
            public RoleSelection Selection { get; }
            public BoardScreenPresenter Presenter { get; }
            public RoleSelectorPresenter Selector { get; }

            public static TestBoard Create(BaselineRole initialRole)
            {
                TemporaryDirectory directory = new TemporaryDirectory();
                var campaignRepository = new SqliteCampaignRepository(Clock);
                var createRequest = new CreateCampaignRequest(directory.Path, "Role Selector Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
                Result<CampaignHandle> created = campaignRepository.Create(createRequest, NewCommandId(), TestCorrelationId);
                Assert.That(created.IsSuccess, Is.True);

                var sceneRepository = new SqliteSceneRepository(Clock);
                SceneId sceneId = sceneRepository.CreateScene(created.Value, "Test Scene", NewCommandId(), TestCorrelationId).Value.SceneId;
                Result<TokenRecord> token = sceneRepository.CreateToken(created.Value, sceneId, new TokenPosition(0, 0), User("2"), NewCommandId(), TestCorrelationId);
                Assert.That(token.IsSuccess, Is.True);

                GameObject gameObject = new GameObject("Role Selector Document");
                UIDocument document = gameObject.AddComponent<UIDocument>();
                PresentationRuntime presentationRuntime = new PresentationRuntime();
                RoleSelection selection = new RoleSelection(User("2"), User("1"), User("3"), initialRole);
                var presenter = new BoardScreenPresenter(document, sceneRepository, created.Value, sceneId, selection, presentationRuntime);
                Assert.That(presenter.Initialize().IsSuccess, Is.True);
                var selector = new RoleSelectorPresenter(selection, presentationRuntime);

                return new TestBoard(directory, campaignRepository, created.Value, gameObject, document, presentationRuntime, selection, presenter, selector);
            }

            public void Dispose()
            {
                if (_disposed) return;
                Presenter.Dispose();
                Selector.Dispose();
                _presentationRuntime.Dispose();
                UnityEngine.Object.DestroyImmediate(_gameObject);
                _campaignRepository.Close(Campaign, TestCorrelationId);
                _directory.Dispose();
                _disposed = true;
            }
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "odyssey-role-selector-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public void Dispose()
            {
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
    }
}
