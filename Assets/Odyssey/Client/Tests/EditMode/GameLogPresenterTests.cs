using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Audience;
using Odyssey.Application.Commands;
using Odyssey.Application.Dice;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Persistence;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;
using Odyssey.Rules.Versions;
using Odyssey.Unity.Client;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Tests.Unity.EditMode
{
    public sealed class GameLogPresenterTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly RulesetVersion TestRulesetVersion = RulesetVersion.Parse("1.0.0");
        private static readonly RngKeyEpochId TestEpoch = RngKeyEpochId.Parse("epoch-001");
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId User(string suffix) => UserId.Parse("user_0000000000000000000000000000000" + suffix);

        [Test]
        public void SaveAndReopen_CurrentRoll_RestoresIdenticalLogEntry()
        {
            using TestPanel panel = TestPanel.Create(BaselineRole.Player);
            DiceRoll roll = panel.RollPanel.SubmitRoll("1d20+3").Value;

            Result<GameLogEntryRecord> saved = panel.GameLog.SaveAndReopen(NewCommandId());

            Assert.That(saved.IsSuccess, Is.True);
            Assert.That(panel.GameLog.Entries.Count, Is.EqualTo(1));
            Assert.That(panel.GameLog.Entries[0].DiceRollId, Is.EqualTo(roll.RollId));
            Assert.That(panel.GameLog.Entries[0].Roll.FormulaOriginal, Is.EqualTo(roll.FormulaOriginal));
            Assert.That(panel.GameLog.Entries[0].Roll.FinalTotal, Is.EqualTo(roll.FinalTotal));
            Assert.That(panel.VisibleEntryTexts(), Is.EquivalentTo(new[] { saved.Value.SummaryPayload }));
        }

        [Test]
        public void RefreshLog_CurrentRoleFiltersPlayerAndGmEntry()
        {
            using TestPanel panel = TestPanel.Create(BaselineRole.Player);
            panel.RollPanel.SubmitRoll("1d20+3");
            panel.GameLog.SaveAndReopen(NewCommandId());

            Assert.That(panel.VisibleEntryTexts().Count, Is.EqualTo(1));

            panel.Selection.SelectRole(BaselineRole.MainGM);
            Assert.That(panel.VisibleEntryTexts().Count, Is.EqualTo(1));

            panel.Selection.SelectRole(BaselineRole.Observer);
            Assert.That(panel.VisibleEntryTexts().Count, Is.EqualTo(0));
            Assert.That(panel.Text("game-log-status"), Is.EqualTo("No visible log entries."));
        }

        [Test]
        public void SaveAndReopen_SameCommandId_DoesNotDuplicateEntry()
        {
            using TestPanel panel = TestPanel.Create(BaselineRole.Player);
            panel.RollPanel.SubmitRoll("1d20");
            CommandId commandId = NewCommandId();

            Result<GameLogEntryRecord> first = panel.GameLog.SaveAndReopen(commandId);
            Result<GameLogEntryRecord> second = panel.GameLog.SaveAndReopen(commandId);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(second.IsSuccess, Is.True);
            Assert.That(second.Value.LogEntryId, Is.EqualTo(first.Value.LogEntryId));
            Assert.That(panel.GameLog.Entries.Count, Is.EqualTo(1));
            Assert.That(panel.VisibleEntryTexts().Count, Is.EqualTo(1));
        }

        [Test]
        public void Presenter_UsesSuppliedCampaignHandle()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            var clock = new FixedClock();
            Result<BoardScreenDemoCampaignHandle> demo = BoardScreenDemoCampaign.CreateFresh(directory.Path, clock);
            Assert.That(demo.IsSuccess, Is.True);

            using TestPanel panel = TestPanel.Create(BaselineRole.Player, demo.Value.Campaign, clock);
            panel.RollPanel.SubmitRoll("1d20");
            Result<GameLogEntryRecord> saved = panel.GameLog.SaveAndReopen(NewCommandId());

            Assert.That(saved.IsSuccess, Is.True);
            Assert.That(panel.GameLog.Entries[0].CampaignId, Is.EqualTo(demo.Value.Campaign.CampaignId));
            Assert.That(File.Exists(Path.Combine(demo.Value.Campaign.RootPath, "campaign.db")), Is.True);
        }

        private sealed class TestPanel : IDisposable
        {
            private readonly GameObject _gameObject;
            private readonly PresentationRuntime _presentationRuntime;
            private readonly TemporaryDirectory? _ownedDirectory;
            private readonly SqliteCampaignRepository? _ownedCampaignRepository;
            private readonly CampaignHandle _campaign;
            private bool _disposed;

            private TestPanel(GameObject gameObject, UIDocument document, PresentationRuntime presentationRuntime, TemporaryDirectory? ownedDirectory, SqliteCampaignRepository? ownedCampaignRepository, CampaignHandle campaign, RoleSelection selection, RollPanelPresenter rollPanel, GameLogPresenter gameLog)
            {
                _gameObject = gameObject;
                Document = document;
                _presentationRuntime = presentationRuntime;
                _ownedDirectory = ownedDirectory;
                _ownedCampaignRepository = ownedCampaignRepository;
                _campaign = campaign;
                Selection = selection;
                RollPanel = rollPanel;
                GameLog = gameLog;
            }

            public UIDocument Document { get; }
            public RoleSelection Selection { get; }
            public RollPanelPresenter RollPanel { get; }
            public GameLogPresenter GameLog { get; }

            public static TestPanel Create(BaselineRole initialRole)
            {
                var directory = new TemporaryDirectory();
                var clock = new FixedClock();
                var campaignRepository = new SqliteCampaignRepository(clock);
                var request = new CreateCampaignRequest(directory.Path, "Game Log UI Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
                CampaignHandle campaign = campaignRepository.Create(request, NewCommandId(), TestCorrelationId).Value;
                return Create(initialRole, campaign, clock, directory, campaignRepository);
            }

            public static TestPanel Create(BaselineRole initialRole, CampaignHandle campaign, IWallClock clock)
            {
                return Create(initialRole, campaign, clock, ownedDirectory: null, ownedCampaignRepository: null);
            }

            private static TestPanel Create(BaselineRole initialRole, CampaignHandle campaign, IWallClock clock, TemporaryDirectory? ownedDirectory, SqliteCampaignRepository? ownedCampaignRepository)
            {
                GameObject gameObject = new GameObject("Game Log Document");
                UIDocument document = gameObject.AddComponent<UIDocument>();
                PresentationRuntime presentationRuntime = new PresentationRuntime();
                RoleSelection selection = new RoleSelection(User("2"), User("1"), User("3"), initialRole);
                ICampaignUserGroupDirectory groups = NewGroups(campaign.CampaignId, selection.PlayerUserId);
                var rollPanel = new RollPanelPresenter(selection, presentationRuntime, new DiceRollStore(), NewRngFactory(), clock, groups, campaign.CampaignId, TestRulesetVersion, TestEpoch);
                var gameLog = new GameLogPresenter(selection, presentationRuntime, rollPanel, campaign, clock, groups);
                document.rootVisualElement.Add(rollPanel.BuildView());
                document.rootVisualElement.Add(gameLog.BuildView());
                return new TestPanel(gameObject, document, presentationRuntime, ownedDirectory, ownedCampaignRepository, campaign, selection, rollPanel, gameLog);
            }

            public string Text(string name)
            {
                Label label = Document.rootVisualElement.Q<Label>(name);
                Assert.That(label, Is.Not.Null);
                return label.text;
            }

            public List<string> VisibleEntryTexts()
            {
                var texts = new List<string>();
                ScrollView list = Document.rootVisualElement.Q<ScrollView>("game-log-list");
                Assert.That(list, Is.Not.Null);
                foreach (VisualElement child in list.contentContainer.Children())
                {
                    if (child is Label label)
                    {
                        texts.Add(label.text);
                    }
                }

                return texts;
            }

            public void Dispose()
            {
                if (_disposed) return;
                GameLog.Dispose();
                RollPanel.Dispose();
                _presentationRuntime.Dispose();
                UnityEngine.Object.DestroyImmediate(_gameObject);
                _ownedCampaignRepository?.Close(_campaign, TestCorrelationId);
                _ownedDirectory?.Dispose();
                _disposed = true;
            }
        }

        private static ICampaignUserGroupDirectory NewGroups(CampaignId campaignId, UserId player)
        {
            var groups = new InMemoryCampaignUserGroupDirectory();
            groups.Upsert(new CampaignUserGroup("trial-player-group", campaignId, new[] { player }, CampaignUserGroupStatus.Active, 1));
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

        private sealed class FixedClock : IWallClock
        {
            public UtcInstant GetUtcNow()
            {
                return UtcInstant.Parse("2026-08-27T23:32:00.0000000Z");
            }
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "odyssey-game-log-ui-" + Guid.NewGuid().ToString("N"));
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
