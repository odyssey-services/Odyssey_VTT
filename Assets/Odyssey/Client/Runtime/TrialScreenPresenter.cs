using System;
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
        public const string CharacterDrawerId = "character";
        public const string InventoryDrawerId = "inventory";
        public const string CombatDrawerId = "combat";
        public const string CatalogDrawerId = "catalog";
        public const string AssetsDrawerId = "assets";
        private const string SelectedParticipantGroupId = "trial-player-group";
        private static readonly RulesetVersion TestRulesetVersion = RulesetVersion.Parse("1.0.0");
        private static readonly RngKeyEpochId TestEpoch = RngKeyEpochId.Parse("epoch-001");

        private readonly UIDocument _document;
        private readonly PresentationRuntime _presentationRuntime;
        private readonly string _rootDirectory;
        private readonly IWallClock _clock;
        private RoleSelectorPresenter? _roleSelectorPresenter;
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
        public AssetPoolPresenter? AssetPool { get; private set; }
        public BoardScreenDemoCampaignHandle? DemoCampaign { get; private set; }

        /// <summary>ODY-S11-201: the overlay layout (drawers, dock, top bar).</summary>
        public GameShellPresenter? Shell { get; private set; }

        /// <summary>ODY-S11-201: the shared parameter object handed to every game panel.</summary>
        public GameSessionContext? Context { get; private set; }

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
                var obstacleRepository = new SqliteObstacleRepository(_clock);
                var visionRepository = new SqliteTokenVisionRepository(_clock);
                var fogRepository = new SqliteFogOfWarRepository(_clock);
                var rollStore = new DiceRollStore();
                var rngFactory = NewRngFactory();

                VisualElement appRoot = _document.rootVisualElement.Q<VisualElement>("odyssey-root") ?? _document.rootVisualElement;

                // ODY-S11-201: Owlbear layout -- full-screen board under floating overlays (GameShellPresenter).
                var shell = new GameShellPresenter(appRoot, selection, _presentationRuntime, "Trial Scene", "trial-screen");
                shell.Build();
                Shell = shell;

                Result members = AddTrialMembers(demo.Value.CampaignRepository, demo.Value.Campaign, selection);
                if (members.IsFailure) return members;
                Context = new GameSessionContext(demo.Value.Campaign, demo.Value.CampaignRepository, demo.Value.SceneId, _clock, selection, _presentationRuntime, TestRulesetVersion, shell.ModalHost);

                _roleSelectorPresenter = new RoleSelectorPresenter(selection, _presentationRuntime);
                VisualElement roleSelectorView = _roleSelectorPresenter.BuildView();
                roleSelectorView.Q<DropdownField>("role-selector-dropdown")?.AddToClassList(OdyClasses.TopbarRole);
                shell.AddToTopbar(roleSelectorView);

                var board = new BoardScreenPresenter(_document, sceneRepository, demo.Value.Campaign, demo.Value.CampaignRepository, obstacleRepository, visionRepository, fogRepository, demo.Value.SceneId, selection, _presentationRuntime, includeRoleSelector: false)
                {
                    FullBleed = true
                };
                Result boardInitialized = board.InitializeInto(shell.BoardLayer);
                if (boardInitialized.IsFailure) return boardInitialized;
                // The board presenter tags its parent with the dark developer-shell class; the board layer has its own.
                shell.BoardLayer.RemoveFromClassList("app-root");
                shell.CenterBoardOnceLaidOut(board);

                // Panels of phases 2-5 fill these drawers (ODY-S11-202...205).
                VisualElement characterDrawer = shell.AddDrawer(CharacterDrawerId, "Character", GameDrawerSide.Left);
                VisualElement inventoryDrawer = shell.AddDrawer(InventoryDrawerId, "Inventory", GameDrawerSide.Left);
                VisualElement combatDrawer = shell.AddDrawer(CombatDrawerId, "Combat", GameDrawerSide.Right);
                VisualElement catalogDrawer = shell.AddDrawer(CatalogDrawerId, "Catalog", GameDrawerSide.Right, wide: true);
                characterDrawer.Add(OdyUi.EmptyState("Character sheet -- ODY-S11-203."));
                inventoryDrawer.Add(OdyUi.EmptyState("Inventory -- ODY-S11-204."));
                combatDrawer.Add(OdyUi.EmptyState("Combat -- ODY-S11-205."));
                catalogDrawer.Add(OdyUi.EmptyState("Content catalog -- ODY-S11-202."));

                var assetPool = new AssetPoolPresenter(_document, sceneRepository, demo.Value.Campaign, board);
                VisualElement assetPoolView = assetPool.BuildView();
                assetPoolView.AddToClassList(GameShellPresenter.LegacyPanelClass);
                shell.AddDrawer(AssetsDrawerId, "Assets", GameDrawerSide.Right).Add(assetPoolView);

                var rollPanel = new RollPanelPresenter(selection, _presentationRuntime, rollStore, rngFactory, _clock, groups, demo.Value.Campaign, demo.Value.CampaignRepository, TestRulesetVersion, TestEpoch, includeRoleSelector: false);
                VisualElement rollPanelView = rollPanel.BuildView();
                rollPanelView.AddToClassList(GameShellPresenter.LegacyPanelClass);

                var gameLog = new GameLogPresenter(selection, _presentationRuntime, rollPanel, demo.Value.Campaign, _clock, groups);
                VisualElement gameLogView = gameLog.BuildView();
                gameLogView.AddToClassList(GameShellPresenter.LegacyPanelClass);
                shell.SetDockContent("Rolls & Game Log", rollPanelView, gameLogView);
                // Kept name: the dock content is the former controls column (PlayMode smoke tests look it up).
                if (shell.DockContent != null) shell.DockContent.name = "trial-controls-column";

                Selection = selection;
                Board = board;
                AssetPool = assetPool;
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
            AssetPool?.Dispose();
            Board?.Dispose();
            _roleSelectorPresenter?.Dispose();
            Shell?.Dispose();
            _disposed = true;
        }

        // ODY-S11-201: the trial's player and observer become stored campaign members (the host is already the MainGM),
        // so the membership-checked services later panels call see the same three actors the role selector offers.
        private static Result AddTrialMembers(ICampaignRepository campaignRepository, CampaignHandle campaign, RoleSelection selection)
        {
            CorrelationId correlationId = UiCommandIds.NewCorrelationId();
            Result<CampaignMembership> player = campaignRepository.AddMember(campaign, selection.PlayerUserId, CampaignMembershipRole.Player, UiCommandIds.NewCommandId(), correlationId);
            if (player.IsFailure) return Result.Failure(player.Error);
            Result<CampaignMembership> observer = campaignRepository.AddMember(campaign, selection.ObserverUserId, CampaignMembershipRole.Observer, UiCommandIds.NewCommandId(), correlationId);
            return observer.IsFailure ? Result.Failure(observer.Error) : Result.Success();
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
