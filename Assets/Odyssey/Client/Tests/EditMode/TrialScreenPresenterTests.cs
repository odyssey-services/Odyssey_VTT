using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Dice;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Unity.Client;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Tests.Unity.EditMode
{
    public sealed class TrialScreenPresenterTests
    {
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));

        [Test]
        public void FullWalkthrough_ComposedPresenters_RunTenStepScenario()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            using TestTrial trial = TestTrial.Create(directory.Path);

            Assert.That(trial.Document.rootVisualElement.Q<VisualElement>("board-area"), Is.Not.Null);
            Assert.That(trial.Document.rootVisualElement.Q<VisualElement>("role-selector"), Is.Not.Null);
            Assert.That(trial.Document.rootVisualElement.Q<VisualElement>("roll-panel"), Is.Not.Null);
            Assert.That(trial.Document.rootVisualElement.Q<VisualElement>("game-log"), Is.Not.Null);

            trial.Screen.Board!.SelectToken(trial.Screen.DemoCampaign!.OtherToken);
            Result<TokenRecord> deniedMove = trial.Screen.Board.TryMoveSelectedTokenTo(new TokenPosition(9, 9));
            Assert.That(deniedMove.IsFailure, Is.True, "step 1 control check: player cannot move another participant's token");

            trial.Screen.Board.SelectToken(trial.Screen.DemoCampaign.LocalToken);
            Result<TokenRecord> moved = trial.Screen.Board.TryMoveSelectedTokenTo(new TokenPosition(2, 1));
            Assert.That(moved.IsSuccess, Is.True, "step 1: player moves their own selected token");

            trial.Screen.Selection!.SelectRole(BaselineRole.Observer);
            Result<DiceRoll> observerRoll = trial.Screen.RollPanel!.SubmitRoll("1d20");
            Assert.That(observerRoll.IsFailure, Is.True, "steps 2-3: host rejects an observer roll intent");

            trial.Screen.Selection.SelectRole(BaselineRole.Player);
            Result<DiceRoll> rolled = trial.Screen.RollPanel.SubmitRoll("1d20+3");
            Assert.That(rolled.IsSuccess, Is.True, "steps 2-4: player roll is accepted and generated");
            Assert.That(rolled.Value.NaturalResults, Has.Count.EqualTo(1));
            DiceRoll original = rolled.Value;
            int originalBaseTotal = original.BaseTotal;
            int originalNatural = original.NaturalResults[0].Value;

            Result<DiceRoll> proposed = trial.Screen.RollPanel.ProposeModifier("Cover", 2);
            Assert.That(proposed.IsSuccess, Is.True, "step 5: modifier proposal succeeds");
            trial.Screen.Selection.SelectRole(BaselineRole.MainGM);
            Result<DiceRoll> accepted = trial.Screen.RollPanel.AcceptLatestModifier();
            Assert.That(accepted.IsSuccess, Is.True, "step 5: MainGM accepts modifier");
            Assert.That(accepted.Value.FinalTotal, Is.EqualTo(originalBaseTotal + 2));

            Result<RollOverride> missingReason = trial.Screen.RollPanel.ApplyOverride(" ");
            Assert.That(missingReason.IsFailure, Is.True, "step 6: override requires a reason");
            Result<RollOverride> overridden = trial.Screen.RollPanel.ApplyOverride("manual walkthrough ruling");
            Assert.That(overridden.IsSuccess, Is.True, "step 6: MainGM override with reason succeeds");
            Assert.That(trial.Screen.RollPanel.LastRoll!.BaseTotal, Is.EqualTo(originalBaseTotal));
            Assert.That(trial.Screen.RollPanel.LastRoll.NaturalResults[0].Value, Is.EqualTo(originalNatural));

            trial.Screen.Selection.SelectRole(BaselineRole.Player);
            Assert.That(trial.Text("roll-result"), Does.Contain("final"), "step 7: player receives the audience-visible result");
            trial.Screen.Selection.SelectRole(BaselineRole.MainGM);
            Assert.That(trial.Text("roll-result"), Does.Contain("final"), "step 7: MainGM receives the result");
            trial.Screen.Selection.SelectRole(BaselineRole.Observer);
            Assert.That(trial.Text("roll-result"), Is.EqualTo("No access to roll result."), "step 7: observer receives safe denial");

            trial.Screen.Selection.SelectRole(BaselineRole.Player);
            Result<GameLogEntryRecord> saved = trial.Screen.GameLog!.SaveAndReopen(NewCommandId());
            Assert.That(saved.IsSuccess, Is.True, "steps 8-9: save and reopen restores the journal");
            Assert.That(trial.VisibleLogEntries(), Is.EquivalentTo(new[] { saved.Value.SummaryPayload }));
            string persistedSummary = saved.Value.SummaryPayload;
            string persistedRollId = saved.Value.DiceRollId;

            trial.Screen.Selection.SelectRole(BaselineRole.Observer);
            Assert.That(trial.VisibleLogEntries(), Is.Empty, "step 9: observer does not see the restored game log entry");

            trial.Screen.Selection.SelectRole(BaselineRole.Player);
            Result<DiceRoll> rerolled = trial.Screen.RollPanel.RequestFullReroll();
            Assert.That(rerolled.IsSuccess, Is.True, "step 10: reroll succeeds through the composed roll presenter");
            Assert.That(rerolled.Value.PreviousRollId, Is.EqualTo(original.RollId));
            Assert.That(trial.Screen.RollPanel.TryGetRoll(original.RollId, out DiceRoll storedOriginal), Is.True);
            Assert.That(storedOriginal.Status, Is.EqualTo(DiceRollStatus.SupersededByReroll));
            Assert.That(storedOriginal.FormulaOriginal, Is.EqualTo(original.FormulaOriginal));
            Assert.That(storedOriginal.BaseTotal, Is.EqualTo(originalBaseTotal));
            Assert.That(storedOriginal.NaturalResults[0].Value, Is.EqualTo(originalNatural));
            Assert.That(trial.Screen.GameLog.Entries, Has.Count.EqualTo(1));
            Assert.That(trial.Screen.GameLog.Entries[0].DiceRollId, Is.EqualTo(persistedRollId));
            Assert.That(trial.Screen.GameLog.Entries[0].SummaryPayload, Is.EqualTo(persistedSummary));
        }

        private sealed class TestTrial : IDisposable
        {
            private readonly GameObject _gameObject;
            private readonly PresentationRuntime _presentationRuntime;
            private bool _disposed;

            private TestTrial(GameObject gameObject, UIDocument document, PresentationRuntime presentationRuntime, TrialScreenPresenter screen)
            {
                _gameObject = gameObject;
                Document = document;
                _presentationRuntime = presentationRuntime;
                Screen = screen;
            }

            public UIDocument Document { get; }
            public TrialScreenPresenter Screen { get; }

            public static TestTrial Create(string rootDirectory)
            {
                GameObject gameObject = new GameObject("Trial Screen Document");
                UIDocument document = gameObject.AddComponent<UIDocument>();
                PresentationRuntime presentationRuntime = new PresentationRuntime();
                var screen = new TrialScreenPresenter(document, presentationRuntime, rootDirectory, new FixedClock());
                Assert.That(screen.Initialize().IsSuccess, Is.True);
                return new TestTrial(gameObject, document, presentationRuntime, screen);
            }

            public string Text(string name)
            {
                Label label = Document.rootVisualElement.Q<Label>(name);
                Assert.That(label, Is.Not.Null);
                return label.text;
            }

            public List<string> VisibleLogEntries()
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
                Screen.Dispose();
                _presentationRuntime.Dispose();
                UnityEngine.Object.DestroyImmediate(_gameObject);
                _disposed = true;
            }
        }

        private sealed class FixedClock : IWallClock
        {
            public UtcInstant GetUtcNow()
            {
                return UtcInstant.Parse("2026-08-28T10:09:00.0000000Z");
            }
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "odyssey-trial-screen-" + Guid.NewGuid().ToString("N"));
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
