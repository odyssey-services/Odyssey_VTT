using System;
using System.Collections.Generic;
using System.IO;
using Odyssey.Application.Audience;
using Odyssey.Application.Dice;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Persistence;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Rules.Versions;
using Odyssey.Persistence.Sqlite;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    public sealed class TrialScreenPresenter : IDisposable
    {
        private const string SelectedParticipantGroupId = "trial-player-group";
        private static readonly RulesetVersion TestRulesetVersion = RulesetVersion.Parse("1.0.0");
        private static readonly RngKeyEpochId TestEpoch = RngKeyEpochId.Parse("epoch-001");

        private readonly UIDocument _document;
        private readonly PresentationRuntime _presentationRuntime;
        private readonly string _rootDirectory;
        private readonly IWallClock _clock;
        private bool _disposed;

        public TrialScreenPresenter(UIDocument document, PresentationRuntime presentationRuntime, string rootDirectory, IWallClock clock)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _presentationRuntime = presentationRuntime ?? throw new ArgumentNullException(nameof(presentationRuntime));
            if (string.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("Root directory is required.", nameof(rootDirectory));
            _rootDirectory = rootDirectory;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public RoleSelection? Selection { get; private set; }
        public BoardScreenPresenter? Board { get; private set; }
        public RollPanelPresenter? RollPanel { get; private set; }
        public GameLogPresenter? GameLog { get; private set; }
        public BoardScreenDemoCampaignHandle? DemoCampaign { get; private set; }

        public Result Initialize()
        {
            try
            {
                string campaignRoot = Path.Combine(_rootDirectory, "campaign-" + Guid.NewGuid().ToString("N"));
                var selection = new RoleSelection(RoleSelection.DefaultPlayerUserId, RoleSelection.DefaultMainGmUserId, RoleSelection.DefaultObserverUserId, BaselineRole.Player);
                Result<BoardScreenDemoCampaignHandle> demo = BoardScreenDemoCampaign.CreateFresh(campaignRoot, _clock, selection.PlayerUserId, selection.ObserverUserId);
                if (demo.IsFailure) return Result.Failure(demo.Error);

                ICampaignUserGroupDirectory groups = CreateGroups(demo.Value.Campaign.CampaignId, selection.PlayerUserId);
                var sceneRepository = new SqliteSceneRepository(_clock);
                var rollStore = new DiceRollStore();
                var rngFactory = NewRngFactory();

                VisualElement appRoot = _document.rootVisualElement.Q<VisualElement>("odyssey-root") ?? _document.rootVisualElement;
                appRoot.Clear();
                appRoot.AddToClassList("app-root");
                VisualElement screen = new VisualElement { name = "trial-screen" };
                screen.AddToClassList("trial-screen");
                appRoot.Add(screen);
                screen.Add(new Label("Odyssey Trial UI") { name = "trial-title" });

                var roleSelector = new RoleSelectorPresenter(selection, _presentationRuntime);
                screen.Add(roleSelector.BuildView());

                VisualElement layout = new VisualElement { name = "trial-layout" };
                layout.style.flexDirection = FlexDirection.Row;
                layout.style.flexWrap = Wrap.Wrap;
                screen.Add(layout);

                VisualElement boardColumn = new VisualElement { name = "trial-board-column" };
                boardColumn.style.marginRight = 12;
                layout.Add(boardColumn);

                VisualElement controlsColumn = new VisualElement { name = "trial-controls-column" };
                controlsColumn.style.minWidth = 320;
                layout.Add(controlsColumn);

                var board = new BoardScreenPresenter(_document, sceneRepository, demo.Value.Campaign, demo.Value.SceneId, selection, _presentationRuntime, includeRoleSelector: false);
                Result boardInitialized = board.InitializeInto(boardColumn);
                if (boardInitialized.IsFailure) return boardInitialized;

                var rollPanel = new RollPanelPresenter(selection, _presentationRuntime, rollStore, rngFactory, _clock, groups, demo.Value.Campaign.CampaignId, TestRulesetVersion, TestEpoch, includeRoleSelector: false);
                controlsColumn.Add(rollPanel.BuildView());

                var gameLog = new GameLogPresenter(selection, _presentationRuntime, rollPanel, demo.Value.Campaign, _clock, groups);
                controlsColumn.Add(gameLog.BuildView());

                Selection = selection;
                Board = board;
                RollPanel = rollPanel;
                GameLog = gameLog;
                DemoCampaign = demo.Value;
                return Result.Success();
            }
            catch (Exception)
            {
                return Result.Failure(RuntimeErrors.CompositionInvalid());
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            GameLog?.Dispose();
            RollPanel?.Dispose();
            Board?.Dispose();
            _disposed = true;
        }

        private static ICampaignUserGroupDirectory CreateGroups(CampaignId campaignId, UserId player)
        {
            var groups = new InMemoryCampaignUserGroupDirectory();
            groups.Upsert(new CampaignUserGroup(SelectedParticipantGroupId, campaignId, new[] { player }, CampaignUserGroupStatus.Active, 1));
            return groups;
        }

        private static IAuthoritativeRandomStreamFactory NewRngFactory()
        {
            byte[] key = new byte[CampaignRngKey.ByteLength];
            for (int index = 0; index < key.Length; index++)
            {
                key[index] = (byte)(index + 1);
            }

            return new DeterministicRandomStreamFactory(CampaignRngKey.FromBytes(key));
        }
    }
}
