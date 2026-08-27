using System;
using System.Globalization;
using Odyssey.Application.Commands;
using Odyssey.Application.Dice;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Rules.Versions;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    public sealed class RollPanelPresenter : IDisposable
    {
        private static readonly CampaignId DefaultCampaignId = CampaignId.Parse("camp_10000000000000000000000000000002");
        private static readonly RulesetVersion DefaultRulesetVersion = RulesetVersion.Parse("1.0.0");
        private static readonly RngKeyEpochId DefaultRngKeyEpochId = RngKeyEpochId.Parse("epoch-001");
        private readonly RoleSelection _roleSelection;
        private readonly PresentationRuntime _presentationRuntime;
        private readonly DiceRollStore _store;
        private readonly IAuthoritativeRandomStreamFactory _rngFactory;
        private readonly IWallClock _clock;
        private readonly CampaignId _campaignId;
        private readonly RulesetVersion _rulesetVersion;
        private readonly RngKeyEpochId _rngKeyEpochId;
        private RoleSelectorPresenter? _roleSelectorPresenter;
        private TextField? _formula;
        private TextField? _modifierLabel;
        private IntegerField? _modifierValue;
        private Label? _status;
        private Label? _result;
        private Button? _accept;
        private Button? _change;
        private Button? _reject;
        private string? _latestModifierEntryId;
        private bool _disposed;

        public RollPanelPresenter(RoleSelection roleSelection, PresentationRuntime presentationRuntime)
            : this(
                roleSelection,
                presentationRuntime,
                new DiceRollStore(),
                NewDefaultRngFactory(),
                new UnityWallClock(),
                DefaultCampaignId,
                DefaultRulesetVersion,
                DefaultRngKeyEpochId)
        {
        }

        public RollPanelPresenter(RoleSelection roleSelection, PresentationRuntime presentationRuntime, DiceRollStore store, IAuthoritativeRandomStreamFactory rngFactory, IWallClock clock, CampaignId campaignId, RulesetVersion rulesetVersion, RngKeyEpochId rngKeyEpochId)
        {
            _roleSelection = roleSelection ?? throw new ArgumentNullException(nameof(roleSelection));
            _presentationRuntime = presentationRuntime ?? throw new ArgumentNullException(nameof(presentationRuntime));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _rngFactory = rngFactory ?? throw new ArgumentNullException(nameof(rngFactory));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _campaignId = campaignId.IsValid ? campaignId : throw new ArgumentException("Campaign id is required.", nameof(campaignId));
            _rulesetVersion = rulesetVersion.IsValid ? rulesetVersion : throw new ArgumentException("Ruleset version is required.", nameof(rulesetVersion));
            _rngKeyEpochId = rngKeyEpochId.IsValid ? rngKeyEpochId : throw new ArgumentException("RNG key epoch id is required.", nameof(rngKeyEpochId));
        }

        public DiceRoll? LastRoll { get; private set; }
        public event Action<DiceRoll>? LastRollChanged;

        public VisualElement BuildView()
        {
            VisualElement root = new VisualElement { name = "roll-panel" };
            root.AddToClassList("roll-panel");

            _roleSelectorPresenter = new RoleSelectorPresenter(_roleSelection, _presentationRuntime);
            root.Add(_roleSelectorPresenter.BuildView());

            _formula = new TextField("Formula") { name = "roll-formula", value = "1d20" };
            root.Add(_formula);

            Button roll = AddButton(root, "roll-button", "Roll", () => SubmitRoll(_formula.value));

            VisualElement modifierRow = new VisualElement { name = "modifier-row" };
            modifierRow.AddToClassList("modifier-row");
            _modifierLabel = new TextField("Modifier") { name = "modifier-label", value = "Manual" };
            _modifierValue = new IntegerField("Value") { name = "modifier-value", value = 1 };
            modifierRow.Add(_modifierLabel);
            modifierRow.Add(_modifierValue);
            AddButton(modifierRow, "modifier-propose-button", "Propose", () => ProposeModifier(_modifierLabel.value, _modifierValue.value));
            root.Add(modifierRow);

            VisualElement decisions = new VisualElement { name = "modifier-decision-row" };
            decisions.AddToClassList("modifier-decision-row");
            _accept = AddButton(decisions, "modifier-accept-button", "Accept", () => AcceptLatestModifier());
            _change = AddButton(decisions, "modifier-change-button", "Change", () => ChangeLatestModifier(_modifierValue.value, "changed_by_main_gm"));
            _reject = AddButton(decisions, "modifier-reject-button", "Reject", () => RejectLatestModifier("rejected_by_main_gm"));
            root.Add(decisions);

            _result = new Label("No roll yet.") { name = "roll-result" };
            _status = new Label("Ready.") { name = "roll-status" };
            root.Add(_result);
            root.Add(_status);

            _presentationRuntime.AddSubscription(_roleSelection.Subscribe(ApplyRoleState));
            ApplyRoleState(_roleSelection.Current);
            roll.SetEnabled(true);
            return root;
        }

        public Result<DiceRoll> SubmitRoll(string? formula)
        {
            RoleSelectionSnapshot role = _roleSelection.Current;
            Result<DiceRoll> submitted = DiceRollService.SubmitRoll(
                _store,
                _rngFactory,
                _clock,
                new SubmitRollRequest(
                    role.ActorUserId,
                    role.ActorCanCreateRoll,
                    "trial.roll",
                    formula ?? string.Empty,
                    DiceRollAudience.Public(),
                    _campaignId,
                    NewCommandId(),
                    _rulesetVersion,
                    _rngKeyEpochId,
                    NewCorrelationId()));

            return StoreOrShowFailure(submitted, "Roll");
        }

        public Result<DiceRoll> ProposeModifier(string? label, int value)
        {
            if (LastRoll == null)
            {
                return ShowFailure(DiceFailures.RollNotFound(NewCorrelationId()), "Modifier");
            }

            string safeLabel = string.IsNullOrWhiteSpace(label) ? "Manual" : label!.Trim();
            Result<DiceRoll> proposed = DiceRollService.ProposeModifier(
                _store,
                new ProposeModifierRequest(LastRoll.RollId, _roleSelection.ActorUserId, "manual", safeLabel, value, NewCorrelationId()));

            if (proposed.IsSuccess && proposed.Value.ModifierEntries.Count > 0)
            {
                _latestModifierEntryId = proposed.Value.ModifierEntries[proposed.Value.ModifierEntries.Count - 1].ModifierEntryId;
            }

            return StoreOrShowFailure(proposed, "Modifier");
        }

        public Result<DiceRoll> AcceptLatestModifier()
        {
            return DecideLatestModifier(ModifierDecision.Accepted, null, null);
        }

        public Result<DiceRoll> ChangeLatestModifier(int changedValue, string reason)
        {
            return DecideLatestModifier(ModifierDecision.Changed, changedValue, reason);
        }

        public Result<DiceRoll> RejectLatestModifier(string reason)
        {
            return DecideLatestModifier(ModifierDecision.Rejected, null, reason);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _roleSelectorPresenter?.Dispose();
            _disposed = true;
        }

        private Result<DiceRoll> DecideLatestModifier(ModifierDecision decision, int? changedValue, string? reason)
        {
            if (LastRoll == null)
            {
                return ShowFailure(DiceFailures.RollNotFound(NewCorrelationId()), "Decision");
            }

            if (string.IsNullOrEmpty(_latestModifierEntryId))
            {
                return ShowFailure(DiceFailures.ModifierNotFound(NewCorrelationId()), "Decision");
            }

            RoleSelectionSnapshot role = _roleSelection.Current;
            Result<DiceRoll> decided = DiceRollService.DecideModifier(
                _store,
                new DecideModifierRequest(LastRoll.RollId, _latestModifierEntryId, role.ActorUserId, role.ActorIsMainGm, decision, changedValue, reason, NewCorrelationId()));

            return StoreOrShowFailure(decided, "Decision");
        }

        private Result<DiceRoll> StoreOrShowFailure(Result<DiceRoll> serviceResult, string action)
        {
            if (serviceResult.IsFailure)
            {
                return ShowFailure(serviceResult.Error, action);
            }

            LastRoll = serviceResult.Value;
            LastRollChanged?.Invoke(serviceResult.Value);
            if (_result != null)
            {
                _result.text = FormatRoll(serviceResult.Value);
            }

            if (_status != null)
            {
                _status.text = action + ": accepted";
            }

            return serviceResult;
        }

        private Result<DiceRoll> ShowFailure(Error error, string action)
        {
            if (_status != null)
            {
                _status.text = action + ": " + error.SafeReasonCode;
            }

            return Result<DiceRoll>.Failure(error);
        }

        private void ApplyRoleState(RoleSelectionSnapshot snapshot)
        {
            bool isMainGm = snapshot.ActorIsMainGm;
            _accept?.SetEnabled(isMainGm);
            _change?.SetEnabled(isMainGm);
            _reject?.SetEnabled(isMainGm);
        }

        private Button AddButton(VisualElement parent, string name, string text, Action handler)
        {
            Button button = new Button { name = name, text = text, userData = handler };
            button.clicked += handler;
            _presentationRuntime.AddSubscription(new ButtonSubscription(button, handler));
            parent.Add(button);
            return button;
        }

        private static string FormatRoll(DiceRoll roll)
        {
            return "Roll " + roll.FormulaOriginal + ": base " + roll.BaseTotal.ToString(CultureInfo.InvariantCulture) + ", final " + roll.FinalTotal.ToString(CultureInfo.InvariantCulture);
        }

        private static CommandId NewCommandId()
        {
            return CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        }

        private static CorrelationId NewCorrelationId()
        {
            return CorrelationId.Parse("corr_" + Guid.NewGuid().ToString("N"));
        }

        private static IAuthoritativeRandomStreamFactory NewDefaultRngFactory()
        {
            byte[] key = new byte[CampaignRngKey.ByteLength];
            for (int index = 0; index < key.Length; index++)
            {
                key[index] = (byte)(index + 1);
            }

            return new DeterministicRandomStreamFactory(CampaignRngKey.FromBytes(key));
        }

        private sealed class ButtonSubscription : IDisposable
        {
            private readonly Button _button;
            private readonly Action _handler;
            private bool _disposed;

            public ButtonSubscription(Button button, Action handler)
            {
                _button = button;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _button.clicked -= _handler;
                _disposed = true;
            }
        }
    }
}
