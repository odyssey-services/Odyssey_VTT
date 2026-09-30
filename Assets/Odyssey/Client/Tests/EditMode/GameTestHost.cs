using System;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Inventory;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;
using Odyssey.Rules.Versions;
using Odyssey.Unity.Client;
using UnityEngine.UIElements;

namespace Odyssey.Tests.Unity.EditMode
{
    /// <summary>
    /// ODY-S11-202..205: a throwaway campaign (the trial demo campaign: host = MainGM, player and observer stored as
    /// members) plus the <see cref="GameSessionContext"/> every game panel takes. Isolated temp storage, fixed clock.
    /// </summary>
    internal sealed class GameTestHost : IDisposable
    {
        private readonly string _directory;
        private bool _disposed;

        private GameTestHost(string directory, BaselineRole role)
        {
            _directory = directory;
            Clock = new FixedClock();
            Selection = new RoleSelection(RoleSelection.DefaultPlayerUserId, RoleSelection.DefaultMainGmUserId, RoleSelection.DefaultObserverUserId, role);
            Runtime = new PresentationRuntime();
            ModalHost = new VisualElement { name = "test-modal-host" };

            Result<BoardScreenDemoCampaignHandle> demo = BoardScreenDemoCampaign.CreateFresh(Path.Combine(directory, "campaign"), Clock, Selection.PlayerUserId, Selection.ObserverUserId);
            Assert.That(demo.IsSuccess, Is.True, "demo campaign");
            Demo = demo.Value;
            Assert.That(Demo.CampaignRepository.AddMember(Demo.Campaign, Selection.PlayerUserId, CampaignMembershipRole.Player, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()).IsSuccess, Is.True);
            Assert.That(Demo.CampaignRepository.AddMember(Demo.Campaign, Selection.ObserverUserId, CampaignMembershipRole.Observer, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()).IsSuccess, Is.True);
            Context = new GameSessionContext(Demo.Campaign, Demo.CampaignRepository, Demo.SceneId, Clock, Selection, Runtime, RulesetVersion.Parse("1.0.0"), ModalHost);
        }

        public FixedClock Clock { get; }
        public RoleSelection Selection { get; }
        public PresentationRuntime Runtime { get; }
        public VisualElement ModalHost { get; }
        public BoardScreenDemoCampaignHandle Demo { get; }
        public GameSessionContext Context { get; }
        public CampaignHandle Campaign => Demo.Campaign;
        public ICampaignRepository CampaignRepository => Demo.CampaignRepository;

        public static GameTestHost Create(BaselineRole role = BaselineRole.MainGM)
        {
            string directory = Path.Combine(Path.GetTempPath(), "odyssey-game-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return new GameTestHost(directory, role);
        }

        /// <summary>The trial composition's repositories (same wiring as <see cref="TrialScreenPresenter"/>).</summary>
        public SqliteInventoryRepository NewInventoryRepository() => new SqliteInventoryRepository(Clock, CampaignRepository);

        public SqliteCharacterRepository NewCharacterRepository(IInventoryRepository inventory) => new SqliteCharacterRepository(
            Clock,
            CampaignRepository,
            null,
            new ICharacterDeletionDependencyChecker[] { new InventoryCharacterDeletionDependencyChecker(inventory) },
            new IBodyPartRemovalDependencyChecker[] { new InventoryBodyPartRemovalDependencyChecker(inventory) });

        public SqliteSceneRepository NewSceneRepository() => new SqliteSceneRepository(Clock);

        public SqliteContentCatalogRepository NewCatalogRepository(IInventoryRepository inventory) => new SqliteContentCatalogRepository(Clock, new IContentDefinitionDeletionDependencyChecker[] { new InventoryContentDefinitionDependencyChecker(inventory) });

        public string ExportDirectory => Path.Combine(_directory, "exports");

        /// <summary>The single open confirmation dialog mounted on the modal host, if any.</summary>
        public VisualElement? OpenDialog => ModalHost.Q<VisualElement>("ody-confirm-dialog");

        public void Dispose()
        {
            if (_disposed) return;
            Runtime.Dispose();
            // Best-effort cleanup of isolated temp storage (no retry/sleep loop).
            try
            {
                if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            _disposed = true;
        }

        internal sealed class FixedClock : IWallClock
        {
            public UtcInstant GetUtcNow() => UtcInstant.Parse("2026-09-30T12:00:00.0000000Z");
        }
    }
}
