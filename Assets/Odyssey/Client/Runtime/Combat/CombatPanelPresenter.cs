using System;
using System.Collections.Generic;
using Odyssey.Application.Combat;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Identity;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    /// <summary>
    /// ODY-S11-205: the combat panel (Combat drawer). Reflects exactly what the combat backend does -- no invented
    /// mechanics:
    /// <list type="bullet">
    /// <item>Initiative is not computed: the MainGM orders participants by hand when creating the encounter.</item>
    /// <item>No hit locations (the backend never picks a body part) and no "% to hit" (a hit is decided by range).</item>
    /// <item>Items are used on self only.</item>
    /// <item>"Correct log entry" (Compensate) only adds a correcting journal entry with a reason; damage and resources
    /// are never rolled back.</item>
    /// <item>Action economy is not checked by the backend. Decision (task contract): a client-side, non-authoritative
    /// hint -- the panel counts the action cost used by each participant in the current turn in this session and warns
    /// when it goes over a suggested budget; it never blocks the command.</item>
    /// </list>
    /// Privileged actions (create/advance encounter, intervention, log correction, stack conflicts) are MainGM-only in
    /// the backend and explained in the UI. Encounters, pending interventions and stack conflicts have no listing query,
    /// so the panel works with the ones created/raised in this session plus "open encounter by id" (recorded limitation).
    /// </summary>
    public sealed partial class CombatPanelPresenter : IDisposable
    {
        private CombatEncounterRecord? _encounter;
        private readonly GameSessionContext _context;
        private readonly CombatPorts _ports;
        private readonly List<CharacterId> _setupOrder = new List<CharacterId>();
        private readonly List<CharacterId> _candidates = new List<CharacterId>();
        private readonly Dictionary<string, string> _names = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _actionsUsed = new Dictionary<string, long>(StringComparer.Ordinal);
        private VisualElement? _body;
        private IDisposable? _roleSubscription;
        private bool _disposed;

        public CombatPanelPresenter(GameSessionContext context, CombatPorts ports)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _ports = ports ?? throw new ArgumentNullException(nameof(ports));
            Banner = new OdyBanner("combat-banner");
        }

        public OdyBanner Banner { get; }
        public CombatEncounterRecord? Encounter
        {
            get => _encounter;
            private set
            {
                CombatEncounterRecord? previous = _encounter;
                _encounter = value;
                // ODY-S11-214: a new acting participant -- whether this panel advanced the turn, started or opened the
                // encounter, or a reload observed someone else's advance -- is announced once (the board focuses it).
                if (value == null || !value.CurrentParticipantId.HasValue) return;
                bool sameTurn = previous != null && previous.EncounterId.Equals(value.EncounterId) && previous.RoundOrdinal == value.RoundOrdinal && previous.TurnOrdinal == value.TurnOrdinal
                    && previous.CurrentParticipantId.HasValue && previous.CurrentParticipantId.Value.Equals(value.CurrentParticipantId.Value);
                if (!sameTurn) ActiveParticipantChanged?.Invoke(value.CurrentParticipantId.Value);
            }
        }

        /// <summary>ODY-S11-214: raised when the acting participant (turn) changes; the trial composition focuses the board camera.</summary>
        public event Action<CharacterId>? ActiveParticipantChanged;

        /// <summary>
        /// ODY-S11-217: how many items wait for the current actor's decision -- for the MainGM, pending attacks plus
        /// stacking-conflict candidates; for everyone else 0 (only the MainGM decides them). Recomputed on every refresh.
        /// </summary>
        public int AttentionCount { get; private set; }

        /// <summary>ODY-S11-217: raised when <see cref="AttentionCount"/> changes; the trial composition shows it as a badge on the closed Combat toggle.</summary>
        public event Action<int>? AttentionCountChanged;
        public IReadOnlyList<CharacterId> SetupOrder => _setupOrder;
        public IReadOnlyList<CharacterId> Candidates => _candidates;
        public bool ActorIsMainGm => _context.ActorIsMainGm;

        /// <summary>Suggested action-cost budget per participant turn (client hint only; the server does not check it).</summary>
        public long ActionBudget { get; set; } = 1;

        public VisualElement BuildView()
        {
            var root = new VisualElement { name = "combat-panel" };
            root.Add(Banner.Element);
            _body = new VisualElement { name = "combat-body" };
            root.Add(_body);
            _roleSubscription = _context.Selection.Subscribe(_ => Refresh());
            _context.PresentationRuntime.AddSubscription(_roleSubscription);
            Refresh();
            return root;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _roleSubscription?.Dispose();
            _disposed = true;
        }

        public Result Refresh()
        {
            CorrelationId correlationId = UiCommandIds.NewCorrelationId();
            _candidates.Clear();
            Result<IReadOnlyList<TokenRecord>> tokens = _ports.Scenes.ListTokens(_context.Campaign, _context.SceneId, correlationId);
            if (tokens.IsSuccess)
            {
                foreach (TokenRecord token in tokens.Value)
                {
                    if (token.CharacterId.HasValue && !_candidates.Contains(token.CharacterId.Value)) _candidates.Add(token.CharacterId.Value);
                }
            }

            if (Encounter != null)
            {
                Result<CombatEncounterRecord> fresh = _ports.Encounters.Get(_context.Campaign, Encounter.EncounterId, correlationId);
                if (fresh.IsSuccess) Encounter = fresh.Value;
            }

            LoadActorData();
            LoadJournal();
            Render();
            UpdateAttention();
            return Result.Success();
        }

        // ---- encounter setup (MainGM, manual order) ---------------------------------------------------

        public void AddToOrder(CharacterId characterId)
        {
            if (!_setupOrder.Contains(characterId)) _setupOrder.Add(characterId);
            Render();
        }

        public void RemoveFromOrder(CharacterId characterId)
        {
            _setupOrder.Remove(characterId);
            Render();
        }

        /// <summary>Moves a participant up (-1) or down (+1) in the hand-made order.</summary>
        public void MoveInOrder(CharacterId characterId, int direction)
        {
            int index = _setupOrder.IndexOf(characterId);
            int target = index + Math.Sign(direction);
            if (index < 0 || target < 0 || target >= _setupOrder.Count) return;
            _setupOrder[index] = _setupOrder[target];
            _setupOrder[target] = characterId;
            Render();
        }

        public Result<CombatEncounterRecord> CreateEncounter()
        {
            if (!ActorIsMainGm)
            {
                Banner.Show(OdyBannerKind.Warning, "Only the MainGM starts an encounter.");
                return Result<CombatEncounterRecord>.Failure(UiGuard.InvalidRequest());
            }

            if (_setupOrder.Count == 0)
            {
                Banner.Show(OdyBannerKind.Error, "Add participants in initiative order first.");
                return Result<CombatEncounterRecord>.Failure(UiGuard.InvalidRequest());
            }

            var order = new List<CharacterId>(_setupOrder);
            Result<CombatEncounterRecord> created = UiGuard.Run(() => CombatEncounterService.Create(_ports.Encounters, _context.CampaignRepository, _context.Campaign,
                new CreateCombatEncounterRequest(order, _context.ActorUserId, UiCommandIds.NewCommandId()), UiCommandIds.NewCorrelationId()));
            if (created.IsFailure)
            {
                Banner.ShowError("Starting the encounter", created.Error);
                return created;
            }

            Encounter = created.Value;
            _setupOrder.Clear();
            _actionsUsed.Clear();
            Banner.Show(OdyBannerKind.Success, "Encounter started: round " + created.Value.RoundOrdinal + ", " + NameOf(created.Value.CurrentParticipantId) + " acts first.");
            Refresh();
            return created;
        }

        public Result OpenEncounter(string encounterIdText)
        {
            if (!CombatEncounterId.TryParse((encounterIdText ?? string.Empty).Trim(), out CombatEncounterId id))
            {
                Banner.Show(OdyBannerKind.Error, "Enter an encounter id like enc_... .");
                return Result.Failure(UiGuard.InvalidRequest());
            }

            Result<CombatEncounterRecord> read = _ports.Encounters.Get(_context.Campaign, id, UiCommandIds.NewCorrelationId());
            if (read.IsFailure)
            {
                Banner.ShowError("Opening the encounter", read.Error);
                return Result.Failure(read.Error);
            }

            Encounter = read.Value;
            Refresh();
            return Result.Success();
        }

        /// <summary>MainGM: next turn at the encounter's current revision.</summary>
        public Result<CombatEncounterRecord> Advance()
        {
            if (Encounter == null) return Result<CombatEncounterRecord>.Failure(UiGuard.InvalidRequest());
            if (!ActorIsMainGm)
            {
                Banner.Show(OdyBannerKind.Warning, "Only the MainGM advances the turn.");
                return Result<CombatEncounterRecord>.Failure(UiGuard.InvalidRequest());
            }

            CombatEncounterRecord basis = Encounter;
            Result<CombatEncounterRecord> advanced = UiGuard.Run(() => CombatEncounterService.Advance(_ports.Encounters, _context.CampaignRepository, _context.Campaign,
                new AdvanceCombatEncounterRequest(basis.EncounterId, basis.Revision, _context.ActorUserId, UiCommandIds.NewCommandId()), UiCommandIds.NewCorrelationId()));
            if (advanced.IsFailure)
            {
                ReportFailure("Next turn", advanced.Error);
                return advanced;
            }

            Encounter = advanced.Value;
            Banner.Show(OdyBannerKind.Success, advanced.Value.Status == CombatEncounterStatus.Open
                ? "Round " + advanced.Value.RoundOrdinal + ", turn " + advanced.Value.TurnOrdinal + ": " + NameOf(advanced.Value.CurrentParticipantId) + "."
                : "The encounter closed: no eligible participants remain.");
            Refresh();
            return advanced;
        }

        public CharacterId? CurrentParticipant => Encounter?.CurrentParticipantId;

        private void UpdateAttention()
        {
            int count = ActorIsMainGm ? PendingAttacks.Count + ConflictCandidates.Count : 0;
            if (count == AttentionCount) return;
            AttentionCount = count;
            AttentionCountChanged?.Invoke(count);
        }

        // ---- action economy hint ------------------------------------------------------------------

        public long ActionsUsedThisTurn(CharacterId characterId) => _actionsUsed.TryGetValue(TurnKey(characterId), out long used) ? used : 0;

        public bool IsOverBudget(CharacterId characterId) => ActionsUsedThisTurn(characterId) > ActionBudget;

        private void RecordAction(CharacterId characterId, long cost)
        {
            string key = TurnKey(characterId);
            _actionsUsed[key] = ActionsUsedThisTurn(characterId) + Math.Max(cost, 0);
        }

        private string TurnKey(CharacterId characterId) => (Encounter?.EncounterId.ToString() ?? "none") + "|" + (Encounter?.RoundOrdinal ?? 0) + "|" + (Encounter?.TurnOrdinal ?? 0) + "|" + characterId;

        private string BudgetText(CharacterId characterId)
        {
            long used = ActionsUsedThisTurn(characterId);
            return "Actions used this turn: " + used + " of a suggested " + ActionBudget + (used >= ActionBudget ? " -- over budget would be allowed by the server; this is only a hint." : " (hint only, not enforced by the server).");
        }

        // ---- helpers ------------------------------------------------------------------------------

        public string NameOf(CharacterId? id)
        {
            if (!id.HasValue) return "nobody";
            string key = id.Value.ToString();
            if (_names.TryGetValue(key, out string cached)) return cached;
            Result<CharacterRecord> read = _ports.Characters.GetCharacter(_context.Campaign, id.Value, UiCommandIds.NewCorrelationId());
            string name = read.IsSuccess ? read.Value.DisplayName : key;
            _names[key] = name;
            return name;
        }

        /// <summary>Names a backend delta/target ref: a character id becomes its name, anything else stays as is.</summary>
        public string NameOfRef(string targetRef)
        {
            if (string.IsNullOrEmpty(targetRef)) return string.Empty;
            foreach (string part in targetRef.Split(':', '/', '|'))
            {
                if (CharacterId.TryParse(part, out CharacterId id)) return targetRef.Replace(part, NameOf(id));
            }

            return targetRef;
        }

        private void ReportFailure(string action, Error error)
        {
            if (error.SafeReasonCode.Equals(SafeReasonCode.StateChanged) || error.SafeReasonCode.Equals(SafeReasonCode.ActionNotAllowed)) Refresh();
            Banner.ShowError(action, error);
        }

        private void Render()
        {
            if (_body == null) return;
            _body.Clear();
            _body.Add(BuildEncounterSection());
            if (Encounter != null && Encounter.Status == CombatEncounterStatus.Open)
            {
                _body.Add(BuildAttackSection());
                _body.Add(BuildAbilitySection());
                _body.Add(BuildUseItemSection());
            }

            _body.Add(BuildCheckSection());
            _body.Add(BuildInterventionSection());
            _body.Add(BuildStackConflictSection());
            _body.Add(BuildJournalSection());
        }

        private VisualElement BuildEncounterSection()
        {
            VisualElement section = OdyUi.Section("Encounter", "combat-encounter");
            if (Encounter != null)
            {
                CombatEncounterRecord e = Encounter;
                var header = new VisualElement();
                header.AddToClassList(OdyClasses.Row);
                header.Add(OdyUi.Badge("Round " + e.RoundOrdinal, OdyStatusKind.Accent, "combat-round"));
                header.Add(OdyUi.Badge("Turn " + e.TurnOrdinal, OdyStatusKind.Info, "combat-turn"));
                header.Add(OdyUi.Badge(e.Status == CombatEncounterStatus.Open ? e.Phase.ToString() : "Closed", e.Status == CombatEncounterStatus.Open ? OdyStatusKind.Success : OdyStatusKind.Archived, "combat-phase"));
                section.Add(header);
                Label current = OdyUi.Text("Acting: " + NameOf(e.CurrentParticipantId), OdyClasses.TextH3);
                current.name = "combat-current";
                section.Add(current);

                var order = new VisualElement { name = "combat-order" };
                order.AddToClassList(OdyClasses.List);
                var participants = new List<CombatParticipant>(e.Participants);
                participants.Sort((a, b) => a.Order.CompareTo(b.Order));
                foreach (CombatParticipant participant in participants)
                {
                    var row = new VisualElement();
                    row.AddToClassList(OdyClasses.ListItem);
                    bool isCurrent = e.CurrentParticipantId.HasValue && e.CurrentParticipantId.Value == participant.CharacterId;
                    if (isCurrent) row.AddToClassList(OdyClasses.ListItemSelected);
                    row.Add(OdyUi.Text((participant.Order + 1) + ". " + NameOf(participant.CharacterId), OdyClasses.ListItemTitle, OdyClasses.Grow));
                    if (isCurrent) row.Add(OdyUi.Badge("Acting", OdyStatusKind.Accent));
                    order.Add(row);
                }

                section.Add(order);
                if (e.CurrentParticipantId.HasValue) section.Add(OdyUi.Text(BudgetText(e.CurrentParticipantId.Value), OdyClasses.FieldHint));
                if (ActorIsMainGm && e.Status == CombatEncounterStatus.Open) section.Add(OdyUi.ButtonRow(OdyUi.Button("Next turn", () => Advance(), OdyButtonVariant.Primary, "combat-advance", small: true)));
                if (!ActorIsMainGm) section.Add(OdyUi.Text("The MainGM advances turns.", OdyClasses.FieldHint));
                section.Add(OdyUi.Text("Encounter id: " + e.EncounterId, OdyClasses.TextCaption));
            }
            else
            {
                section.Add(OdyUi.EmptyState("No encounter open.", "combat-no-encounter"));
            }

            if (ActorIsMainGm)
            {
                VisualElement setup = OdyUi.Section("New encounter -- initiative order (by hand)", "combat-setup");
                setup.Add(OdyUi.Text("Initiative is not rolled: put the participants in order. Candidates are characters with tokens on this scene.", OdyClasses.FieldHint));
                var ordered = new VisualElement { name = "combat-setup-order" };
                ordered.AddToClassList(OdyClasses.List);
                for (int index = 0; index < _setupOrder.Count; index++)
                {
                    CharacterId id = _setupOrder[index];
                    var row = new VisualElement();
                    row.AddToClassList(OdyClasses.ListItem);
                    row.Add(OdyUi.Text((index + 1) + ". " + NameOf(id), OdyClasses.ListItemTitle, OdyClasses.Grow));
                    row.Add(OdyUi.Button("▲", () => MoveInOrder(id, -1), OdyButtonVariant.Ghost, "combat-setup-up-" + id, small: true));
                    row.Add(OdyUi.Button("▼", () => MoveInOrder(id, 1), OdyButtonVariant.Ghost, "combat-setup-down-" + id, small: true));
                    row.Add(OdyUi.Button("×", () => RemoveFromOrder(id), OdyButtonVariant.Ghost, "combat-setup-remove-" + id, small: true));
                    ordered.Add(row);
                }

                if (_setupOrder.Count == 0) ordered.Add(OdyUi.EmptyState("Nobody added yet."));
                setup.Add(ordered);
                var add = new VisualElement();
                add.AddToClassList(OdyClasses.ButtonRow);
                foreach (CharacterId candidate in _candidates)
                {
                    if (_setupOrder.Contains(candidate)) continue;
                    CharacterId id = candidate;
                    add.Add(OdyUi.Button("+ " + NameOf(id), () => AddToOrder(id), OdyButtonVariant.Secondary, "combat-setup-add-" + id, small: true));
                }

                if (_candidates.Count == 0) add.Add(OdyUi.Text("No character tokens on this scene -- use \"Place token on scene\" in the Character panel.", OdyClasses.FieldHint));
                setup.Add(add);
                setup.Add(OdyUi.ButtonRow(OdyUi.Button("Start encounter", () => CreateEncounter(), OdyButtonVariant.Primary, "combat-create", small: true)));
                section.Add(setup);
            }
            else if (Encounter == null)
            {
                section.Add(OdyUi.Text("The MainGM starts encounters and sets the initiative order.", OdyClasses.FieldHint));
            }

            var open = new VisualElement();
            open.AddToClassList(OdyClasses.FormRow);
            TextField openId = OdyUi.TextField("Open encounter by id", string.Empty, "combat-open-id");
            open.Add(openId);
            open.Add(OdyUi.Button("Open", () => OpenEncounter(openId.value), OdyButtonVariant.Secondary, "combat-open-button", small: true));
            OdyUi.SubmitOnEnter(openId, () => OpenEncounter(openId.value));
            section.Add(open);
            return section;
        }
    }
}
