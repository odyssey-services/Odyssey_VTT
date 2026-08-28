using System;
using NUnit.Framework;
using Odyssey.Application.Dice;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Rules.Versions;
using Odyssey.Unity.Client;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Tests.Unity.EditMode
{
    public sealed class RollPanelPresenterTests
    {
        private static readonly CampaignId TestCampaignId = CampaignId.Parse("camp_0123456789abcdef0123456789abcdef");
        private static readonly RulesetVersion TestRulesetVersion = RulesetVersion.Parse("1.0.0");
        private static readonly RngKeyEpochId TestEpoch = RngKeyEpochId.Parse("epoch-001");
        private static UserId User(string suffix) => UserId.Parse("user_0000000000000000000000000000000" + suffix);

        [Test]
        public void PlayerRoll_DefaultAudience_IsPlayerAndGMAndVisible()
        {
            using TestPanel panel = TestPanel.Create(BaselineRole.Player);

            Result<DiceRoll> result = panel.Presenter.SubmitRoll("1d20+3");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(panel.Presenter.LastRoll, Is.SameAs(result.Value));
            Assert.That(result.Value.ActorUserId, Is.EqualTo(panel.Selection.PlayerUserId));
            Assert.That(result.Value.Audience.Kind, Is.EqualTo(DiceRollAudienceKind.PlayerAndGM));
            Assert.That(result.Value.NaturalResults, Has.Count.EqualTo(1));
            Assert.That(panel.Text("roll-result"), Does.Contain("final"));
            Assert.That(panel.Text("roll-status"), Does.Contain("accepted"));
        }

        [Test]
        public void MainGmRoll_ValidFormula_StoresLastRoll()
        {
            using TestPanel panel = TestPanel.Create(BaselineRole.MainGM);

            Result<DiceRoll> result = panel.Presenter.SubmitRoll("2d6+1");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(panel.Presenter.LastRoll, Is.SameAs(result.Value));
            Assert.That(result.Value.ActorUserId, Is.EqualTo(panel.Selection.MainGmUserId));
            Assert.That(result.Value.NaturalResults, Has.Count.EqualTo(2));
        }

        [Test]
        public void Roll_SelectedParticipantsAudience_SelectsPlayerUser()
        {
            using TestPanel panel = TestPanel.Create(BaselineRole.Player);
            panel.SelectAudience("SelectedParticipants");

            Result<DiceRoll> result = panel.Presenter.SubmitRoll("1d20");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Audience.Kind, Is.EqualTo(DiceRollAudienceKind.SelectedParticipants));
            Assert.That(result.Value.Audience.SelectedUserIds.Count, Is.EqualTo(1));
            Assert.That(result.Value.Audience.SelectedUserIds[0], Is.EqualTo(panel.Selection.PlayerUserId));
            Assert.That(result.Value.Audience.SelectedGroupIds.Count, Is.EqualTo(1));
            Assert.That(panel.Text("roll-result"), Does.Contain("final"));
        }

        [Test]
        public void ObserverRoll_ValidFormula_ShowsDeniedError()
        {
            using TestPanel panel = TestPanel.Create(BaselineRole.Observer);

            Result<DiceRoll> result = panel.Presenter.SubmitRoll("1d20+3");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.DiceRollDenied));
            Assert.That(panel.Presenter.LastRoll, Is.Null);
            Assert.That(panel.Text("roll-status"), Does.Contain(SafeReasonCode.PermissionDenied.ToString()));
        }

        [Test]
        public void RoleSwitch_ObserverCannotSeePlayerAndGmRoll()
        {
            using TestPanel panel = TestPanel.Create(BaselineRole.Player);
            Result<DiceRoll> result = panel.Presenter.SubmitRoll("1d20+3");
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(panel.Text("roll-result"), Does.Contain("final"));

            panel.Selection.SelectRole(BaselineRole.Observer);

            Assert.That(panel.Text("roll-result"), Is.EqualTo("No access to roll result."));
            Assert.That(panel.Text("roll-result"), Does.Not.Contain("1d20"));
            Assert.That(panel.Text("roll-result"), Does.Not.Contain("final"));
        }

        [Test]
        public void MainGmDecisions_ProposedModifier_UpdatesFinalTotal()
        {
            using TestPanel panel = TestPanel.Create(BaselineRole.MainGM);
            DiceRoll roll = panel.Presenter.SubmitRoll("1d20").Value;
            int baseTotal = roll.BaseTotal;

            Result<DiceRoll> proposed = panel.Presenter.ProposeModifier("Cover", 2);
            Assert.That(proposed.IsSuccess, Is.True);
            Assert.That(proposed.Value.FinalTotal, Is.EqualTo(baseTotal));

            Result<DiceRoll> accepted = panel.Presenter.AcceptLatestModifier();
            Assert.That(accepted.IsSuccess, Is.True);
            Assert.That(accepted.Value.FinalTotal, Is.EqualTo(baseTotal + 2));

            panel.Presenter.ProposeModifier("Bless", 4);
            Result<DiceRoll> changed = panel.Presenter.ChangeLatestModifier(5, "test_changed");
            Assert.That(changed.IsSuccess, Is.True);
            Assert.That(changed.Value.FinalTotal, Is.EqualTo(baseTotal + 2 + 5));

            panel.Presenter.ProposeModifier("Penalty", 9);
            Result<DiceRoll> rejected = panel.Presenter.RejectLatestModifier("test_rejected");
            Assert.That(rejected.IsSuccess, Is.True);
            Assert.That(rejected.Value.FinalTotal, Is.EqualTo(baseTotal + 2 + 5));
            Assert.That(rejected.Value.ModifierEntries[rejected.Value.ModifierEntries.Count - 1].Decision, Is.EqualTo(ModifierDecision.Rejected));
        }

        [Test]
        public void PlayerAcceptModifier_ProposedModifier_ShowsDeniedError()
        {
            using TestPanel panel = TestPanel.Create(BaselineRole.Player);
            DiceRoll roll = panel.Presenter.SubmitRoll("1d20").Value;
            int baseTotal = roll.BaseTotal;
            panel.Presenter.ProposeModifier("Cover", 2);

            Result<DiceRoll> accepted = panel.Presenter.AcceptLatestModifier();

            Assert.That(accepted.IsFailure, Is.True);
            Assert.That(accepted.Error.Code, Is.EqualTo(ErrorCodes.DiceModifierDecisionDenied));
            Assert.That(panel.Presenter.LastRoll!.FinalTotal, Is.EqualTo(baseTotal));
            Assert.That(panel.Text("roll-status"), Does.Contain(SafeReasonCode.PermissionDenied.ToString()));
        }

        [Test]
        public void MainGmOverride_EmptyReason_ShowsErrorAndDoesNotChangeRoll()
        {
            using TestPanel panel = TestPanel.Create(BaselineRole.MainGM);
            DiceRoll roll = panel.Presenter.SubmitRoll("1d20").Value;
            int baseTotal = roll.BaseTotal;
            int naturalCount = roll.NaturalResults.Count;

            Result<RollOverride> result = panel.Presenter.ApplyOverride(" ");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.DiceOverrideReasonRequired));
            Assert.That(panel.Presenter.LastRoll!.Status, Is.EqualTo(DiceRollStatus.Resolved));
            Assert.That(panel.Presenter.LastRoll.BaseTotal, Is.EqualTo(baseTotal));
            Assert.That(panel.Presenter.LastRoll.NaturalResults, Has.Count.EqualTo(naturalCount));
            Assert.That(panel.Text("roll-status"), Does.Contain(SafeReasonCode.InvalidRequest.ToString()));
        }

        [Test]
        public void MainGmOverride_WithReason_SetsRollStatusOverridden()
        {
            using TestPanel panel = TestPanel.Create(BaselineRole.MainGM);
            DiceRoll roll = panel.Presenter.SubmitRoll("1d20").Value;

            Result<RollOverride> result = panel.Presenter.ApplyOverride("table ruling");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.DiceRollId, Is.EqualTo(roll.RollId));
            Assert.That(panel.Presenter.LastRoll!.Status, Is.EqualTo(DiceRollStatus.Overridden));
            Assert.That(panel.Text("roll-result"), Does.Contain("Overridden"));
            Assert.That(panel.Text("roll-status"), Does.Contain("accepted"));
        }

        [Test]
        public void PlayerOverride_WithReason_ShowsDeniedError()
        {
            using TestPanel panel = TestPanel.Create(BaselineRole.Player);
            DiceRoll roll = panel.Presenter.SubmitRoll("1d20").Value;

            Result<RollOverride> result = panel.Presenter.ApplyOverride("not allowed");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.DiceOverrideDenied));
            Assert.That(panel.Presenter.LastRoll!.Status, Is.EqualTo(DiceRollStatus.Resolved));
            Assert.That(panel.Presenter.LastRoll.RollId, Is.EqualTo(roll.RollId));
            Assert.That(panel.Text("roll-status"), Does.Contain(SafeReasonCode.PermissionDenied.ToString()));
        }

        [Test]
        public void PlayerReroll_CurrentRoll_CreatesNewRollAndSupersedesOriginalWithoutRewritingData()
        {
            using TestPanel panel = TestPanel.Create(BaselineRole.Player);
            DiceRoll original = panel.Presenter.SubmitRoll("1d20+3").Value;
            int originalBaseTotal = original.BaseTotal;
            int originalNatural = original.NaturalResults[0].Value;

            Result<DiceRoll> rerolled = panel.Presenter.RequestFullReroll();

            Assert.That(rerolled.IsSuccess, Is.True);
            Assert.That(rerolled.Value.RollId, Is.Not.EqualTo(original.RollId));
            Assert.That(rerolled.Value.PreviousRollId, Is.EqualTo(original.RollId));
            Assert.That(panel.Presenter.LastRoll, Is.SameAs(rerolled.Value));
            Assert.That(panel.Presenter.TryGetRoll(original.RollId, out DiceRoll storedOriginal), Is.True);
            Assert.That(storedOriginal.Status, Is.EqualTo(DiceRollStatus.SupersededByReroll));
            Assert.That(storedOriginal.FormulaOriginal, Is.EqualTo(original.FormulaOriginal));
            Assert.That(storedOriginal.BaseTotal, Is.EqualTo(originalBaseTotal));
            Assert.That(storedOriginal.NaturalResults[0].Value, Is.EqualTo(originalNatural));
            Assert.That(panel.Text("roll-status"), Does.Contain("Reroll: accepted"));
        }

        [Test]
        public void PlayerCancel_CurrentRollWithReason_CancelsWithoutRewritingData()
        {
            using TestPanel panel = TestPanel.Create(BaselineRole.Player);
            DiceRoll original = panel.Presenter.SubmitRoll("1d20+3").Value;
            int originalBaseTotal = original.BaseTotal;
            int originalNatural = original.NaturalResults[0].Value;

            Result<DiceRoll> cancelled = panel.Presenter.CancelRoll("manual test cancel");

            Assert.That(cancelled.IsSuccess, Is.True);
            Assert.That(cancelled.Value.RollId, Is.EqualTo(original.RollId));
            Assert.That(cancelled.Value.Status, Is.EqualTo(DiceRollStatus.Cancelled));
            Assert.That(cancelled.Value.FormulaOriginal, Is.EqualTo(original.FormulaOriginal));
            Assert.That(cancelled.Value.BaseTotal, Is.EqualTo(originalBaseTotal));
            Assert.That(cancelled.Value.NaturalResults[0].Value, Is.EqualTo(originalNatural));
            Assert.That(panel.Presenter.LastRoll, Is.SameAs(cancelled.Value));
            Assert.That(panel.Text("roll-result"), Does.Contain("Cancelled"));
            Assert.That(panel.Text("roll-status"), Does.Contain("Cancel: accepted"));
        }

        [Test]
        public void RoleSwitch_UpdatesMainGmOnlyButtons()
        {
            using TestPanel panel = TestPanel.Create(BaselineRole.Player);

            Assert.That(panel.Button("modifier-accept-button").enabledSelf, Is.False);
            Assert.That(panel.Button("override-button").enabledSelf, Is.False);
            Assert.That(panel.Button("reroll-button").enabledSelf, Is.True);
            Assert.That(panel.Button("cancel-roll-button").enabledSelf, Is.True);

            panel.Selection.SelectRole(BaselineRole.MainGM);
            Assert.That(panel.Button("modifier-accept-button").enabledSelf, Is.True);
            Assert.That(panel.Button("override-button").enabledSelf, Is.True);
            Assert.That(panel.Button("reroll-button").enabledSelf, Is.True);
            Assert.That(panel.Button("cancel-roll-button").enabledSelf, Is.True);

            panel.Selection.SelectRole(BaselineRole.Observer);
            Assert.That(panel.Button("modifier-accept-button").enabledSelf, Is.False);
            Assert.That(panel.Button("override-button").enabledSelf, Is.False);
            Assert.That(panel.Button("reroll-button").enabledSelf, Is.False);
            Assert.That(panel.Button("cancel-roll-button").enabledSelf, Is.False);
        }

        private sealed class TestPanel : IDisposable
        {
            private readonly GameObject _gameObject;
            private readonly PresentationRuntime _presentationRuntime;
            private bool _disposed;

            private TestPanel(GameObject gameObject, UIDocument document, PresentationRuntime presentationRuntime, RoleSelection selection, RollPanelPresenter presenter)
            {
                _gameObject = gameObject;
                Document = document;
                _presentationRuntime = presentationRuntime;
                Selection = selection;
                Presenter = presenter;
            }

            public UIDocument Document { get; }
            public RoleSelection Selection { get; }
            public RollPanelPresenter Presenter { get; }

            public static TestPanel Create(BaselineRole initialRole)
            {
                GameObject gameObject = new GameObject("Roll Panel Document");
                UIDocument document = gameObject.AddComponent<UIDocument>();
                PresentationRuntime presentationRuntime = new PresentationRuntime();
                RoleSelection selection = new RoleSelection(User("2"), User("1"), User("3"), initialRole);
                var presenter = new RollPanelPresenter(selection, presentationRuntime, new DiceRollStore(), NewRngFactory(), new FixedClock(), TestCampaignId, TestRulesetVersion, TestEpoch);
                document.rootVisualElement.Add(presenter.BuildView());
                return new TestPanel(gameObject, document, presentationRuntime, selection, presenter);
            }

            public string Text(string name)
            {
                Label label = Document.rootVisualElement.Q<Label>(name);
                Assert.That(label, Is.Not.Null);
                return label.text;
            }

            public Button Button(string name)
            {
                Button button = Document.rootVisualElement.Q<Button>(name);
                Assert.That(button, Is.Not.Null);
                return button;
            }

            public void SelectAudience(string value)
            {
                DropdownField dropdown = Document.rootVisualElement.Q<DropdownField>("roll-audience");
                Assert.That(dropdown, Is.Not.Null);
                dropdown.value = value;
            }

            public void Dispose()
            {
                if (_disposed) return;
                Presenter.Dispose();
                _presentationRuntime.Dispose();
                UnityEngine.Object.DestroyImmediate(_gameObject);
                _disposed = true;
            }
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
                return UtcInstant.Parse("2026-08-27T18:32:00.0000000Z");
            }
        }
    }
}
