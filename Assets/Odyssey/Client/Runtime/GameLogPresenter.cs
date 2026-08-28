using System;
using System.Collections.Generic;
using Odyssey.Application.Audience;
using Odyssey.Application.Commands;
using Odyssey.Application.Dice;
using Odyssey.Application.GameLog;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Persistence.Sqlite;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    public sealed class GameLogPresenter : IDisposable
    {
        private readonly RoleSelection _roleSelection;
        private readonly PresentationRuntime _presentationRuntime;
        private readonly RollPanelPresenter _rollPanel;
        private readonly CampaignHandle _campaign;
        private readonly IWallClock _clock;
        private readonly ICampaignUserGroupDirectory _groups;
        private readonly List<GameLogEntryRecord> _entries = new List<GameLogEntryRecord>();
        private ScrollView? _list;
        private Label? _status;
        private bool _disposed;

        public GameLogPresenter(RoleSelection roleSelection, PresentationRuntime presentationRuntime, RollPanelPresenter rollPanel, CampaignHandle campaign, IWallClock clock, ICampaignUserGroupDirectory groups)
        {
            _roleSelection = roleSelection ?? throw new ArgumentNullException(nameof(roleSelection));
            _presentationRuntime = presentationRuntime ?? throw new ArgumentNullException(nameof(presentationRuntime));
            _rollPanel = rollPanel ?? throw new ArgumentNullException(nameof(rollPanel));
            _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _groups = groups ?? throw new ArgumentNullException(nameof(groups));
        }

        public IReadOnlyList<GameLogEntryRecord> Entries => _entries;

        public VisualElement BuildView()
        {
            VisualElement root = new VisualElement { name = "game-log" };
            root.AddToClassList("game-log");
            Button save = new Button { name = "game-log-save-reopen-button", text = "Save & Reopen Campaign" };
            Action saveHandler = () => SaveAndReopen();
            save.clicked += saveHandler;
            _presentationRuntime.AddSubscription(new ButtonSubscription(save, saveHandler));
            root.Add(save);

            _list = new ScrollView { name = "game-log-list" };
            root.Add(_list);

            _status = new Label("No saved entries.") { name = "game-log-status" };
            root.Add(_status);

            _presentationRuntime.AddSubscription(_roleSelection.Subscribe(_ => RefreshVisibleEntries()));
            _presentationRuntime.AddSubscription(new RollSubscription(_rollPanel, _ => RefreshVisibleEntries()));
            RefreshVisibleEntries();
            return root;
        }

        public Result<GameLogEntryRecord> SaveAndReopen()
        {
            return SaveAndReopen(NewCommandId());
        }

        public Result<GameLogEntryRecord> SaveAndReopen(CommandId commandId)
        {
            if (_rollPanel.LastRoll == null)
            {
                return ShowFailure(DiceFailures.RollNotFound(NewCorrelationId()), "Save");
            }

            var repository = new SqliteGameLogRepository(_clock);
            Result<GameLogEntryRecord> saved = repository.SaveDiceRollEntry(_campaign, _rollPanel.LastRoll, commandId, NewCorrelationId());
            if (saved.IsFailure)
            {
                return ShowFailure(saved.Error, "Save");
            }

            var reopened = new SqliteGameLogRepository(_clock);
            Result<IReadOnlyList<GameLogEntryRecord>> listed = reopened.ListGameLog(_campaign, NewCorrelationId());
            if (listed.IsFailure)
            {
                return ShowFailure(listed.Error, "Reopen");
            }

            _entries.Clear();
            _entries.AddRange(listed.Value);
            RefreshVisibleEntries();
            return saved;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }

        private Result<GameLogEntryRecord> ShowFailure(Error error, string action)
        {
            SetStatus(action + ": " + error.SafeReasonCode);
            return Result<GameLogEntryRecord>.Failure(error);
        }

        private void RefreshVisibleEntries()
        {
            if (_list == null) return;
            _list.Clear();

            RoleSelectionSnapshot role = _roleSelection.Current;
            IReadOnlyList<GameLogEntryRecord> visible = GameLogReconnectService.GetVisibleEntries(_entries, role.ActorUserId, role.Role, _groups);
            foreach (GameLogEntryRecord entry in visible)
            {
                _list.Add(new Label(entry.SummaryPayload) { name = "game-log-entry-" + entry.LogEntryId });
            }

            if (_status != null)
            {
                _status.text = _entries.Count == 0
                    ? "No saved entries."
                    : visible.Count == 0
                        ? "No visible log entries."
                        : "Visible entries: " + visible.Count;
            }
        }

        private void SetStatus(string text)
        {
            if (_status != null) _status.text = text;
        }

        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static CorrelationId NewCorrelationId() => CorrelationId.Parse("corr_" + Guid.NewGuid().ToString("N"));

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

        private sealed class RollSubscription : IDisposable
        {
            private readonly RollPanelPresenter _rollPanel;
            private readonly Action<DiceRoll> _handler;
            private bool _disposed;

            public RollSubscription(RollPanelPresenter rollPanel, Action<DiceRoll> handler)
            {
                _rollPanel = rollPanel;
                _handler = handler;
                _rollPanel.LastRollChanged += _handler;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _rollPanel.LastRollChanged -= _handler;
                _disposed = true;
            }
        }
    }
}
