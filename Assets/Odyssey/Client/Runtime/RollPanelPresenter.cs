using System;
using System.Collections.Generic;
using System.Globalization;
using Odyssey.Application.Audience;
using Odyssey.Application.Commands;
using Odyssey.Application.Dice;
using Odyssey.Application.Networking.Session;
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
        private const string PublicAudience = "Public";
        private const string PlayerAndGmAudience = "PlayerAndGM";
        private const string SelectedParticipantsAudience = "SelectedParticipants";
        private const string SelectedParticipantGroupId = "trial-player-group";
        private static readonly List<string> AudienceChoices = new List<string> { PublicAudience, PlayerAndGmAudience, SelectedParticipantsAudience };
        private readonly RoleSelection _roleSelection;
        private readonly PresentationRuntime _presentationRuntime;
        private readonly DiceRollStore _store;
        private readonly IAuthoritativeRandomStreamFactory _rngFactory;
        private readonly IWallClock _clock;
        private readonly ICampaignUserGroupDirectory _groups;
        private readonly CampaignId _campaignId;
        private readonly RulesetVersion _rulesetVersion;
        private readonly RngKeyEpochId _rngKeyEpochId;
        private RoleSelectorPresenter? _roleSelectorPresenter;
        private DropdownField? _audience;
        private TextField? _formula;
        private TextField? _modifierLabel;
        private IntegerField? _modifierValue;
        private TextField? _overrideReason;
        private Label? _status;
        private Label? _result;
        private Button? _accept;
        private Button? _change;
        private Button? _reject;
        private Button? _override;
        private TextField? _cancelReason;
        private Button? _reroll;
        private Button? _cancel;
        private string? _latestModifierEntryId;
        private readonly bool _includeRoleSelector;
        private bool _disposed;

        public RollPanelPresenter(RoleSelection roleSelection, PresentationRuntime presentationRuntime)
            : this(
                roleSelection,
                presentationRuntime,
                new DiceRollStore(),
                NewDefaultRngFactory(),
                new UnityWallClock(),
                NewDefaultGroups(roleSelection),
                DefaultCampaignId,
                DefaultRulesetVersion,
                DefaultRngKeyEpochId)
        {
        }

        public RollPanelPresenter(RoleSelection roleSelection, PresentationRuntime presentationRuntime, DiceRollStore store, IAuthoritativeRandomStreamFactory rngFactory, IWallClock clock, CampaignId campaignId, RulesetVersion rulesetVersion, RngKeyEpochId rngKeyEpochId)
            : this(roleSelection, presentationRuntime, store, rngFactory, clock, NewDefaultGroups(roleSelection), campaignId, rulesetVersion, rngKeyEpochId)
        {
        }

        public RollPanelPresenter(RoleSelection roleSelection, PresentationRuntime presentationRuntime, DiceRollStore store, IAuthoritativeRandomStreamFactory rngFactory, IWallClock clock, ICampaignUserGroupDirectory groups, CampaignId campaignId, RulesetVersion rulesetVersion, RngKeyEpochId rngKeyEpochId, bool includeRoleSelector = true)
        {
            _roleSelection = roleSelection ?? throw new ArgumentNullException(nameof(roleSelection));
            _presentationRuntime = presentationRuntime ?? throw new ArgumentNullException(nameof(presentationRuntime));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _rngFactory = rngFactory ?? throw new ArgumentNullException(nameof(rngFactory));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _groups = groups ?? throw new ArgumentNullException(nameof(groups));
            _campaignId = campaignId.IsValid ? campaignId : throw new ArgumentException("Campaign id is required.", nameof(campaignId));
            _rulesetVersion = rulesetVersion.IsValid ? rulesetVersion : throw new ArgumentException("Ruleset version is required.", nameof(rulesetVersion));
            _rngKeyEpochId = rngKeyEpochId.IsValid ? rngKeyEpochId : throw new ArgumentException("RNG key epoch id is required.", nameof(rngKeyEpochId));
            _includeRoleSelector = includeRoleSelector;
        }

        public DiceRoll? LastRoll { get; private set; }
        public event Action<DiceRoll>? LastRollChanged;

        public bool TryGetRoll(string rollId, out DiceRoll roll)
        {
            return _store.TryGet(rollId, out roll);
        }

        public VisualElement BuildView()
        {
            VisualElement root = new VisualElement { name = "roll-panel" };
            root.AddToClassList("roll-panel");

            if (_includeRoleSelector)
            {
                _roleSelectorPresenter = new RoleSelectorPresenter(_roleSelection, _presentationRuntime);
                root.Add(_roleSelectorPresenter.BuildView());
            }

            _audience = new DropdownField("Audience", AudienceChoices, AudienceChoices.IndexOf(PlayerAndGmAudience)) { name = "roll-audience" };
            root.Add(_audience);

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

            VisualElement overrideRow = new VisualElement { name = "override-row" };
            overrideRow.AddToClassList("override-row");
            _overrideReason = new TextField("Override reason") { name = "override-reason" };
            overrideRow.Add(_overrideReason);
            _override = AddButton(overrideRow, "override-button", "Override", () => ApplyOverride(_overrideReason.value));
            root.Add(overrideRow);

            _result = new Label("No roll yet.") { name = "roll-result" };
            _status = new Label("Ready.") { name = "roll-status" };
            root.Add(_result);
            root.Add(_status);

            VisualElement lifecycle = new VisualElement { name = "roll-lifecycle-row" };
            lifecycle.AddToClassList("roll-lifecycle-row");
            _cancelReason = new TextField("Cancel reason") { name = "cancel-reason", value = "manual_cancel" };
            lifecycle.Add(_cancelReason);
            _reroll = AddButton(lifecycle, "reroll-button", "Reroll", () => RequestFullReroll());
            _cancel = AddButton(lifecycle, "cancel-roll-button", "Cancel", () => CancelRoll(_cancelReason.value));
            root.Add(lifecycle);

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
                    SelectedAudience(),
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

        public Result<RollOverride> ApplyOverride(string? reason)
        {
            if (LastRoll == null)
            {
                return ShowOverrideFailure(DiceFailures.RollNotFound(NewCorrelationId()), "Override");
            }

            RoleSelectionSnapshot role = _roleSelection.Current;
            Result<RollOverride> applied = DiceRollService.ApplyOverride(
                _store,
                _clock,
                new ApplyOverrideRequest(LastRoll.RollId, role.ActorUserId, role.ActorIsMainGm, FormatRoll(LastRoll), "GM override", reason, NewCorrelationId()));

            if (applied.IsFailure)
            {
                return ShowOverrideFailure(applied.Error, "Override");
            }

            if (_store.TryGet(LastRoll.RollId, out DiceRoll updated))
            {
                LastRoll = updated;
                LastRollChanged?.Invoke(updated);
            }

            RefreshResultDisplay();
            if (_status != null)
            {
                _status.text = "Override: accepted";
            }

            return applied;
        }

        public Result<DiceRoll> RequestFullReroll()
        {
            if (LastRoll == null)
            {
                return ShowFailure(DiceFailures.RollNotFound(NewCorrelationId()), "Reroll");
            }

            RoleSelectionSnapshot role = _roleSelection.Current;
            Result<DiceRoll> rerolled = DiceRollService.RequestFullReroll(
                _store,
                _rngFactory,
                _clock,
                new RequestFullRerollRequest(LastRoll.RollId, role.ActorUserId, role.ActorIsMainGm, NewCommandId(), _rulesetVersion, _rngKeyEpochId, NewCorrelationId()));

            return StoreOrShowFailure(rerolled, "Reroll");
        }

        public Result<DiceRoll> CancelRoll(string? reason)
        {
            if (LastRoll == null)
            {
                return ShowFailure(DiceFailures.RollNotFound(NewCorrelationId()), "Cancel");
            }

            RoleSelectionSnapshot role = _roleSelection.Current;
            Result<DiceRoll> cancelled = DiceRollService.CancelRoll(
                _store,
                new CancelRollRequest(LastRoll.RollId, role.ActorUserId, role.ActorIsMainGm, reason, NewCorrelationId()));

            return StoreOrShowFailure(cancelled, "Cancel");
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
            RefreshResultDisplay();

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

        private Result<RollOverride> ShowOverrideFailure(Error error, string action)
        {
            if (_status != null)
            {
                _status.text = action + ": " + error.SafeReasonCode;
            }

            return Result<RollOverride>.Failure(error);
        }

        private void ApplyRoleState(RoleSelectionSnapshot snapshot)
        {
            bool isMainGm = snapshot.ActorIsMainGm;
            _accept?.SetEnabled(isMainGm);
            _change?.SetEnabled(isMainGm);
            _reject?.SetEnabled(isMainGm);
            _override?.SetEnabled(isMainGm);
            _reroll?.SetEnabled(snapshot.Role != BaselineRole.Observer);
            _cancel?.SetEnabled(snapshot.Role != BaselineRole.Observer);
            RefreshResultDisplay();
        }

        private Button AddButton(VisualElement parent, string name, string text, Action handler)
        {
            Button button = new Button { name = name, text = text, userData = handler };
            button.clicked += handler;
            _presentationRuntime.AddSubscription(new ButtonSubscription(button, handler));
            parent.Add(button);
            return button;
        }

        private DiceRollAudience SelectedAudience()
        {
            string value = _audience?.value ?? PlayerAndGmAudience;
            switch (value)
            {
                case PublicAudience:
                    return DiceRollAudience.Public();
                case SelectedParticipantsAudience:
                    return DiceRollAudience.SelectedParticipants(new[] { _roleSelection.PlayerUserId }, new[] { SelectedParticipantGroupId });
                case PlayerAndGmAudience:
                default:
                    return DiceRollAudience.PlayerAndGM();
            }
        }

        private void RefreshResultDisplay()
        {
            if (_result == null) return;
            if (LastRoll == null)
            {
                _result.text = "No roll yet.";
                return;
            }

            RoleSelectionSnapshot role = _roleSelection.Current;
            if (!DiceRollVisibilityPolicy.TryGetVisibleRoll(LastRoll, role.ActorUserId, role.Role, _groups, out DiceRollView view))
            {
                _result.text = "No access to roll result.";
                return;
            }

            _result.text = FormatRoll(view.Roll);
        }

        private static string FormatRoll(DiceRoll roll)
        {
            return "Roll " + roll.FormulaOriginal + ": base " + roll.BaseTotal.ToString(CultureInfo.InvariantCulture) + ", final " + roll.FinalTotal.ToString(CultureInfo.InvariantCulture) + ", status " + roll.Status;
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

        private static ICampaignUserGroupDirectory NewDefaultGroups(RoleSelection roleSelection)
        {
            if (roleSelection == null) throw new ArgumentNullException(nameof(roleSelection));
            var groups = new InMemoryCampaignUserGroupDirectory();
            groups.Upsert(new CampaignUserGroup(SelectedParticipantGroupId, DefaultCampaignId, new[] { roleSelection.PlayerUserId }, CampaignUserGroupStatus.Active, 1));
            return groups;
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
