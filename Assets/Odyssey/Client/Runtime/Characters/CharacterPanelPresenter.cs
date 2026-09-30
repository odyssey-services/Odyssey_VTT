using System;
using System.Collections.Generic;
using System.IO;
using Odyssey.Application.Board;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Geometry;
using Odyssey.Domain.Identity;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    /// <summary>Input of the character creation form.</summary>
    public sealed class CharacterCreateForm
    {
        public string DisplayName { get; set; } = string.Empty;
        public CharacterKind Kind { get; set; } = CharacterKind.PlayerCharacter;
        public string AnatomyProfileRef { get; set; } = CharacterPanelPresenter.DefaultAnatomyProfile;

        /// <summary>Required for a PlayerCharacter; optional otherwise.</summary>
        public UserId? PrimaryOwnerUserId { get; set; }
    }

    /// <summary>
    /// ODY-S11-203: the character panel (Character drawer) -- a roster, character creation / import, and a tabbed
    /// character sheet (General &amp; review, Attributes, Skills, Abilities, Resources, Anatomy, Ownership, History).
    ///
    /// Roster decision (task contract §3.1): the backend has no "list characters of a campaign" query. The roster is
    /// built only from sources that already exist: characters linked to tokens on the current scene
    /// (<see cref="ISceneRepository.ListTokens"/> -> <c>TokenRecord.CharacterId</c>), characters created or imported
    /// in this session, and "open by id". A Player sees only characters they own, co-own or control; the MainGM sees
    /// every roster entry. That filter is presentation-only (the backend's GetCharacter has no audience filter) and is
    /// recorded as a limitation awaiting a backend listing task.
    ///
    /// Every mutation goes through <c>CharacterAdvancementService</c> where one exists (attributes, skills,
    /// recommendations, abilities, respec, resource/anatomy defaults), otherwise through the character repository port,
    /// always with the matching section revision of the current record, a fresh CommandId/CorrelationId, and the
    /// returned record replaces local state. A <c>StateChanged</c> failure reloads the character before reporting.
    /// </summary>
    public sealed partial class CharacterPanelPresenter : IDisposable
    {
        public const string DefaultAnatomyProfile = "humanoid";

        private readonly GameSessionContext _context;
        private readonly ICharacterRepository _characters;
        private readonly ISceneRepository _scenes;
        private readonly string _exportDirectory;
        private readonly IContentCatalogRepository? _catalog;
        private readonly HashSet<string> _sessionCharacterIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<CharacterRecord> _roster = new List<CharacterRecord>();
        private readonly List<CampaignMembership> _members = new List<CampaignMembership>();
        private VisualElement? _root;
        private VisualElement? _rosterList;
        private VisualElement? _sheetHost;
        private IDisposable? _roleSubscription;
        private OdyTabs? _tabs;
        private string _activeTab = GeneralTab;
        private bool _disposed;

        public const string GeneralTab = "general";
        public const string AttributesTab = "attributes";
        public const string SkillsTab = "skills";
        public const string AbilitiesTab = "abilities";
        public const string ResourcesTab = "resources";
        public const string AnatomyTab = "anatomy";
        public const string OwnershipTab = "ownership";
        public const string HistoryTab = "history";

        /// <param name="catalog">Optional (ODY-S11-205): lets the MainGM link a character ability to a published catalog Ability so it can be activated in combat.</param>
        public CharacterPanelPresenter(GameSessionContext context, ICharacterRepository characters, ISceneRepository scenes, string exportDirectory, IContentCatalogRepository? catalog = null)
        {
            _catalog = catalog;
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _characters = characters ?? throw new ArgumentNullException(nameof(characters));
            _scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
            if (string.IsNullOrWhiteSpace(exportDirectory)) throw new ArgumentException("Export directory is required.", nameof(exportDirectory));
            _exportDirectory = exportDirectory;
            Banner = new OdyBanner("character-banner");
        }

        /// <summary>Raised after a token was placed for a character, so the board can re-render.</summary>
        public event Action? BoardChanged;

        /// <summary>Raised whenever the open character's record changed (other panels, e.g. inventory, follow it).</summary>
        public event Action<CharacterRecord?>? CurrentChanged;

        public OdyBanner Banner { get; }
        public CharacterRecord? Current { get; private set; }
        public IReadOnlyList<CharacterRecord> Roster => _roster;
        public OdyTabs? Tabs => _tabs;
        public string? LastExportPath { get; private set; }

        public bool ActorIsMainGm => _context.ActorIsMainGm;

        /// <summary>Owner, co-owner or controller of the open character (presentation gate; the backend re-checks).</summary>
        public bool ActorIsAssigned => Current != null && IsAssigned(Current, _context.ActorUserId);

        public bool ActorCanManage => ActorIsMainGm || ActorIsAssigned;

        public VisualElement BuildView()
        {
            _root = new VisualElement { name = "character-panel" };
            _root.Add(Banner.Element);

            VisualElement rosterSection = OdyUi.Section("Characters", "character-roster-section");
            rosterSection.Add(OdyUi.Text("Listed: characters on this scene's tokens and those created or imported in this session (the campaign has no full roster query yet).", OdyClasses.FieldHint));
            _rosterList = new VisualElement { name = "character-roster" };
            _rosterList.AddToClassList(OdyClasses.List);
            rosterSection.Add(_rosterList);

            var openRow = new VisualElement();
            openRow.AddToClassList(OdyClasses.FormRow);
            TextField openId = OdyUi.TextField("Open by id", string.Empty, "character-open-id");
            openId.AddToClassList(OdyClasses.FieldGrow);
            openRow.Add(openId);
            openRow.Add(OdyUi.Button("Open", () => OpenById(openId.value), OdyButtonVariant.Secondary, "character-open-button", small: true));
            rosterSection.Add(openRow);
            _root.Add(rosterSection);

            _root.Add(BuildCreateSection());
            _root.Add(BuildImportSection());

            _sheetHost = new VisualElement { name = "character-sheet" };
            _root.Add(_sheetHost);

            _roleSubscription = _context.Selection.Subscribe(_ => { Refresh(); });
            _context.PresentationRuntime.AddSubscription(_roleSubscription);
            Refresh();
            return _root;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _roleSubscription?.Dispose();
            _disposed = true;
        }

        /// <summary>Reloads members, the roster and the open character from the server.</summary>
        public Result Refresh()
        {
            CorrelationId correlationId = UiCommandIds.NewCorrelationId();
            Result<IReadOnlyList<CampaignMembership>> members = _context.CampaignRepository.ListMembers(_context.Campaign, correlationId);
            _members.Clear();
            if (members.IsSuccess) _members.AddRange(members.Value);

            var ids = new List<CharacterId>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            Result<IReadOnlyList<TokenRecord>> tokens = _scenes.ListTokens(_context.Campaign, _context.SceneId, correlationId);
            if (tokens.IsSuccess)
            {
                foreach (TokenRecord token in tokens.Value)
                {
                    if (token.CharacterId.HasValue && seen.Add(token.CharacterId.Value.ToString())) ids.Add(token.CharacterId.Value);
                }
            }

            foreach (string known in _sessionCharacterIds)
            {
                if (seen.Add(known) && CharacterId.TryParse(known, out CharacterId id)) ids.Add(id);
            }

            _roster.Clear();
            foreach (CharacterId id in ids)
            {
                Result<CharacterRecord> read = _characters.GetCharacter(_context.Campaign, id, correlationId);
                if (read.IsFailure) continue;
                if (ActorIsMainGm || IsAssigned(read.Value, _context.ActorUserId)) _roster.Add(read.Value);
            }

            if (Current != null)
            {
                Result<CharacterRecord> fresh = _characters.GetCharacter(_context.Campaign, Current.CharacterId, correlationId);
                SetCurrent(fresh.IsSuccess && (ActorIsMainGm || IsAssigned(fresh.Value, _context.ActorUserId)) ? fresh.Value : null);
            }
            else
            {
                RenderSheet();
            }

            RenderRoster();
            return Result.Success();
        }

        public Result Open(CharacterId characterId)
        {
            Result<CharacterRecord> read = _characters.GetCharacter(_context.Campaign, characterId, UiCommandIds.NewCorrelationId());
            if (read.IsFailure)
            {
                Banner.ShowError("Opening the character", read.Error);
                return Result.Failure(read.Error);
            }

            _sessionCharacterIds.Add(characterId.ToString());
            Banner.Hide();
            SetCurrent(read.Value);
            RenderRoster();
            return Result.Success();
        }

        public Result OpenById(string text)
        {
            if (!CharacterId.TryParse((text ?? string.Empty).Trim(), out CharacterId id))
            {
                Banner.Show(OdyBannerKind.Error, "Enter a character id like char_... .");
                return Result.Failure(UiGuard.InvalidRequest());
            }

            return Open(id);
        }

        /// <summary>BindDraftToCampaign with an empty seed (template listing does not exist either, see §3.1).</summary>
        public Result<CharacterRecord> CreateCharacter(CharacterCreateForm form)
        {
            if (form == null) throw new ArgumentNullException(nameof(form));
            string name = (form.DisplayName ?? string.Empty).Trim();
            if (name.Length == 0 || name.Length > 128)
            {
                Banner.Show(OdyBannerKind.Error, "Enter a name (1-128 characters).");
                return Result<CharacterRecord>.Failure(UiGuard.InvalidRequest());
            }

            if (string.IsNullOrWhiteSpace(form.AnatomyProfileRef))
            {
                Banner.Show(OdyBannerKind.Error, "Enter an anatomy profile reference.");
                return Result<CharacterRecord>.Failure(UiGuard.InvalidRequest());
            }

            if (form.Kind == CharacterKind.PlayerCharacter && (!form.PrimaryOwnerUserId.HasValue || !form.PrimaryOwnerUserId.Value.IsValid))
            {
                Banner.Show(OdyBannerKind.Error, "A player character needs a primary owner.");
                return Result<CharacterRecord>.Failure(UiGuard.InvalidRequest());
            }

            Result<CharacterRecord> created = UiGuard.Run(() => _characters.BindDraftToCampaign(
                new BindDraftToCampaignRequest(_context.Campaign, form.Kind, name, form.AnatomyProfileRef.Trim(), form.PrimaryOwnerUserId, CharacterCreationSeed.None(), null, null),
                UiCommandIds.NewCommandId(),
                UiCommandIds.NewCorrelationId()));
            if (created.IsFailure)
            {
                Banner.ShowError("Creating the character", created.Error);
                return created;
            }

            _sessionCharacterIds.Add(created.Value.CharacterId.ToString());
            Banner.Show(OdyBannerKind.Success, "Draft character \"" + created.Value.DisplayName + "\" created.");
            SetCurrent(created.Value);
            Refresh();
            return created;
        }

        /// <summary>MainGM: puts a token for the open character on the current scene (links roster and board).</summary>
        public Result<TokenRecord> PlaceTokenOnScene()
        {
            if (Current == null) return Result<TokenRecord>.Failure(UiGuard.InvalidRequest());
            if (!ActorIsMainGm)
            {
                Banner.Show(OdyBannerKind.Warning, "Only the MainGM places character tokens on the scene.");
                return Result<TokenRecord>.Failure(UiGuard.InvalidRequest());
            }

            UserId controller = Current.Ownership.PrimaryOwnerUserId ?? _context.ActorUserId;
            Result<TokenRecord> placed = UiGuard.Run(() => _scenes.CreateToken(_context.Campaign, _context.SceneId, new TokenPosition(0, 0), controller, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId(), Current.CharacterId));
            if (placed.IsFailure)
            {
                Banner.ShowError("Placing the token", placed.Error);
                return placed;
            }

            Banner.Show(OdyBannerKind.Success, "Token placed at the scene origin.");
            BoardChanged?.Invoke();
            return placed;
        }

        public Result<CharacterExportBundle> Export()
        {
            if (Current == null) return Result<CharacterExportBundle>.Failure(UiGuard.InvalidRequest());
            string safeName = MakeSafeFileName(Current.DisplayName);
            string path = Path.Combine(_exportDirectory, safeName + "-" + Current.CharacterId + ".odchar");
            CharacterId id = Current.CharacterId;
            Result<CharacterExportBundle> exported = UiGuard.Run(() => _characters.ExportCharacter(_context.Campaign, id, path, new ExportActorContext(_context.ActorUserId, ActorIsMainGm), UiCommandIds.NewCorrelationId()));
            if (exported.IsFailure)
            {
                Banner.ShowError("Export", exported.Error);
                return exported;
            }

            LastExportPath = path;
            Banner.Show(OdyBannerKind.Success, "Exported to " + Path.GetFileName(path) + " (in the exports folder).");
            return exported;
        }

        public Result<CharacterRecord> Import(string bundleDirectoryPath, UserId? primaryOwner)
        {
            if (string.IsNullOrWhiteSpace(bundleDirectoryPath))
            {
                Banner.Show(OdyBannerKind.Error, "Choose an .odchar bundle folder.");
                return Result<CharacterRecord>.Failure(UiGuard.InvalidRequest());
            }

            Result<CharacterRecord> imported = UiGuard.Run(() => _characters.ImportCharacter(new ImportCharacterRequest(_context.Campaign, bundleDirectoryPath.Trim(), primaryOwner), UiCommandIds.NewCommandId(), UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()));
            if (imported.IsFailure)
            {
                Banner.ShowError("Import", imported.Error);
                return imported;
            }

            _sessionCharacterIds.Add(imported.Value.CharacterId.ToString());
            Banner.Show(OdyBannerKind.Success, "Imported \"" + imported.Value.DisplayName + "\".");
            SetCurrent(imported.Value);
            Refresh();
            return imported;
        }

        // ---- shared command plumbing ----------------------------------------------------------------

        /// <summary>Runs one character mutation; on success the returned record becomes the open character.</summary>
        private Result<CharacterRecord> Mutate(string action, Func<CharacterRecord, Result<CharacterRecord>> call, string? successText = null)
        {
            if (Current == null)
            {
                Banner.Show(OdyBannerKind.Error, action + ": open a character first.");
                return Result<CharacterRecord>.Failure(UiGuard.InvalidRequest());
            }

            CharacterRecord basis = Current;
            Result<CharacterRecord> result = UiGuard.Run(() => call(basis));
            if (result.IsFailure)
            {
                ReportFailure(action, result.Error);
                return result;
            }

            SetCurrent(result.Value);
            RenderRoster();
            Banner.Show(OdyBannerKind.Success, successText ?? action + ": done.");
            return result;
        }

        private void ReportFailure(string action, Error error)
        {
            if (error.SafeReasonCode.Equals(SafeReasonCode.StateChanged) && Current != null)
            {
                Result<CharacterRecord> fresh = _characters.GetCharacter(_context.Campaign, Current.CharacterId, UiCommandIds.NewCorrelationId());
                if (fresh.IsSuccess) SetCurrent(fresh.Value);
            }

            Banner.ShowError(action, error);
        }

        private void SetCurrent(CharacterRecord? record)
        {
            if (_tabs?.ActiveTabId != null) _activeTab = _tabs.ActiveTabId;
            Current = record;
            if (record != null)
            {
                for (int index = 0; index < _roster.Count; index++)
                {
                    if (_roster[index].CharacterId.Equals(record.CharacterId)) _roster[index] = record;
                }
            }

            RenderSheet();
            CurrentChanged?.Invoke(record);
        }

        private static bool IsAssigned(CharacterRecord record, UserId user)
        {
            CharacterOwnership ownership = record.Ownership;
            if (ownership.PrimaryOwnerUserId.HasValue && ownership.PrimaryOwnerUserId.Value.Equals(user)) return true;
            foreach (UserId id in ownership.CoOwnerUserIds) if (id.Equals(user)) return true;
            foreach (UserId id in ownership.PermanentControllerUserIds) if (id.Equals(user)) return true;
            foreach (CharacterTemporaryControlGrant grant in ownership.TemporaryControlGrants) if (grant.UserId.Equals(user)) return true;
            return false;
        }

        private static string MakeSafeFileName(string name)
        {
            var builder = new System.Text.StringBuilder();
            foreach (char c in name)
            {
                builder.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            }

            string result = builder.ToString().Trim('_');
            return result.Length == 0 ? "character" : result;
        }

        // ---- roster / create / import views ---------------------------------------------------------

        private void RenderRoster()
        {
            if (_rosterList == null) return;
            _rosterList.Clear();
            if (_roster.Count == 0)
            {
                _rosterList.Add(OdyUi.EmptyState(ActorIsMainGm ? "No characters yet. Create one below." : "You have no characters here yet.", "character-roster-empty"));
                return;
            }

            foreach (CharacterRecord record in _roster)
            {
                CharacterId id = record.CharacterId;
                var row = new Button(() => Open(id)) { name = "character-row-" + id, text = string.Empty };
                row.AddToClassList(OdyClasses.ListItem);
                if (Current != null && Current.CharacterId.Equals(id)) row.AddToClassList(OdyClasses.ListItemSelected);
                var main = new VisualElement();
                main.AddToClassList(OdyClasses.ListItemMain);
                main.Add(OdyUi.Text(record.DisplayName, OdyClasses.ListItemTitle));
                main.Add(OdyUi.Text(EnumChoices.Humanize(record.CharacterKind.ToString()) + " · " + record.ApprovalState, OdyClasses.ListItemMeta));
                row.Add(main);
                row.Add(OdyUi.Badge(record.LifecycleStatus.ToString(), LifecycleKind(record.LifecycleStatus)));
                _rosterList.Add(row);
            }
        }

        private VisualElement BuildCreateSection()
        {
            VisualElement section = OdyUi.Section("New character", "character-create-section");
            var form = new CharacterCreateForm();
            TextField name = OdyUi.TextField("Name", string.Empty, "character-create-name");
            name.RegisterValueChangedCallback(evt => form.DisplayName = evt.newValue);
            section.Add(name);

            var row = new VisualElement();
            row.AddToClassList(OdyClasses.FormRow);
            List<string> kinds = EnumChoices.Names<CharacterKind>();
            DropdownField kind = OdyUi.Dropdown("Type", kinds, 0, "character-create-kind");
            kind.RegisterValueChangedCallback(evt => { if (EnumChoices.TryParse(evt.newValue, out CharacterKind parsed)) form.Kind = parsed; });
            row.Add(kind);
            TextField anatomy = OdyUi.TextField("Anatomy profile", DefaultAnatomyProfile, "character-create-anatomy");
            anatomy.RegisterValueChangedCallback(evt => form.AnatomyProfileRef = evt.newValue);
            row.Add(anatomy);
            section.Add(row);

            DropdownField owner = BuildUserDropdown("Primary owner", "character-create-owner", _context.Selection.PlayerUserId);
            owner.RegisterValueChangedCallback(evt => form.PrimaryOwnerUserId = UserFromChoice(evt.newValue));
            form.PrimaryOwnerUserId = UserFromChoice(owner.value);
            section.Add(owner);
            section.Add(OdyUi.Text("Anatomy profile is a text reference for now (no anatomy catalog picker yet). Characters start empty; templates cannot be listed by the backend.", OdyClasses.FieldHint));
            section.Add(OdyUi.ButtonRow(OdyUi.Button("Create draft", () => CreateCharacter(form), OdyButtonVariant.Primary, "character-create-button")));
            return section;
        }

        private VisualElement BuildImportSection()
        {
            VisualElement section = OdyUi.Section("Import .odchar", "character-import-section");
            TextField path = OdyUi.TextField("Bundle folder", string.Empty, "character-import-path");
            path.AddToClassList(OdyClasses.FieldGrow);
            section.Add(path);
            DropdownField owner = BuildUserDropdown("Primary owner", "character-import-owner", _context.Selection.PlayerUserId);
            section.Add(owner);
            section.Add(OdyUi.ButtonRow(
                OdyUi.Button("Use last export", () => { if (LastExportPath != null) path.value = LastExportPath; }, OdyButtonVariant.Ghost, "character-import-last", small: true),
                OdyUi.Button("Browse…", () => { string? picked = NativeFileDialog.OpenFolder("Choose an .odchar bundle"); if (picked != null) path.value = picked; }, OdyButtonVariant.Secondary, "character-import-browse", small: true),
                OdyUi.Button("Import", () => Import(path.value, UserFromChoice(owner.value)), OdyButtonVariant.Primary, "character-import-button", small: true)));
            return section;
        }

        private DropdownField BuildUserDropdown(string label, string name, UserId? preferred)
        {
            var choices = new List<string>();
            int index = 0;
            foreach (CampaignMembership member in _members)
            {
                if (preferred.HasValue && member.UserId.Equals(preferred.Value)) index = choices.Count;
                choices.Add(UserChoice(member.UserId, member.Role));
            }

            if (choices.Count == 0) choices.Add("(no members)");
            return OdyUi.Dropdown(label, choices, index, name);
        }

        internal string UserLabel(UserId user)
        {
            foreach (CampaignMembership member in _members)
            {
                if (member.UserId.Equals(user)) return UserChoice(member.UserId, member.Role);
            }

            return user.ToString();
        }

        private static string UserChoice(UserId user, CampaignMembershipRole role) => role + " — " + user;

        private static UserId? UserFromChoice(string? choice)
        {
            if (string.IsNullOrEmpty(choice)) return null;
            int separator = choice!.LastIndexOf(' ');
            string candidate = separator >= 0 ? choice.Substring(separator + 1) : choice;
            return UserId.TryParse(candidate, out UserId id) ? id : (UserId?)null;
        }

        private static OdyStatusKind LifecycleKind(CharacterLifecycleStatus status)
        {
            switch (status)
            {
                case CharacterLifecycleStatus.Draft:
                    return OdyStatusKind.Draft;
                case CharacterLifecycleStatus.Active:
                    return OdyStatusKind.Published;
                case CharacterLifecycleStatus.Dead:
                    return OdyStatusKind.Error;
                case CharacterLifecycleStatus.Archived:
                    return OdyStatusKind.Archived;
                default:
                    return OdyStatusKind.Neutral;
            }
        }
    }
}
