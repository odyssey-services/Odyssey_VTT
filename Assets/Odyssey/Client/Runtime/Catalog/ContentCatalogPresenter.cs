using System;
using System.Collections.Generic;
using Odyssey.Application.Content;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    /// <summary>
    /// ODY-S11-202: the content catalog screen (hosted in the wide "Catalog" drawer). Lists definitions of the seven
    /// supported types with type/status filters, and edits one definition through <see cref="ContentDefinitionFormModel"/>
    /// with the full lifecycle: create draft, save draft, validate, publish, create next version, archive, delete draft.
    ///
    /// Every mutation goes through the Application services (<see cref="ContentCatalogAuthoringService"/>,
    /// <see cref="ContentCatalogLifecycleService"/>) with a fresh CommandId/CorrelationId and the record's current Revision,
    /// and the local state is replaced by the record the service returns (never an optimistic client guess). The services
    /// decide MainGM authorization from stored membership; the presenter only explains the restriction up front.
    ///
    /// Known backend boundary (recorded in the task contract): <see cref="IContentCatalogRepository.UpdateDraftContentDefinition"/>
    /// updates name, description and properties only, so tags and ruleset compatibility are editable at creation time and
    /// read-only afterwards.
    /// </summary>
    public sealed class ContentCatalogPresenter : IDisposable
    {
        public const string AllTypesChoice = "All types";
        public const string AllStatusesChoice = "All statuses";

        private readonly GameSessionContext _context;
        private readonly IContentCatalogRepository _catalog;
        private readonly List<ContentDefinitionRecord> _definitions = new List<ContentDefinitionRecord>();
        private readonly List<ContentDefinitionRecord> _visible = new List<ContentDefinitionRecord>();
        private readonly List<string> _issues = new List<string>();
        private VisualElement? _root;
        private VisualElement? _list;
        private VisualElement? _detail;
        private VisualElement? _newRow;
        private OdyBanner? _roleNotice;
        private IDisposable? _roleSubscription;
        private readonly Dictionary<string, VisualElement> _conditional = new Dictionary<string, VisualElement>(StringComparer.Ordinal);
        private readonly Dictionary<string, VisualElement> _fieldsByKey = new Dictionary<string, VisualElement>(StringComparer.Ordinal);
        private bool _disposed;

        public ContentCatalogPresenter(GameSessionContext context, IContentCatalogRepository catalog)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Banner = new OdyBanner("catalog-banner");
        }

        public OdyBanner Banner { get; }
        public ContentDefinitionType? TypeFilter { get; private set; }
        public ContentDefinitionStatus? StatusFilter { get; private set; }
        public IReadOnlyList<ContentDefinitionRecord> VisibleDefinitions => _visible;

        /// <summary>The definition currently open in the editor (null for an unsaved new draft or nothing selected).</summary>
        public ContentDefinitionRecord? Selected { get; private set; }

        /// <summary>The editor state (null when nothing is open). Tests edit it and call <see cref="ApplyFormChange"/>.</summary>
        public ContentDefinitionFormModel? Form { get; private set; }

        /// <summary>Readable issues from the last validation / failed publish.</summary>
        public IReadOnlyList<string> LastIssues => _issues;

        public bool CanAuthor => _context.ActorIsMainGm;

        /// <summary>True when the open definition can be edited: MainGM, and a Draft (or a new unsaved draft).</summary>
        public bool IsFormEditable => CanAuthor && Form != null && (Selected == null || Selected.Status == ContentDefinitionStatus.Draft);

        public VisualElement BuildView()
        {
            _root = new VisualElement { name = "catalog-screen" };
            _root.AddToClassList(OdyClasses.Column);

            _roleNotice = new OdyBanner("catalog-role-notice");
            _root.Add(_roleNotice.Element);
            _root.Add(Banner.Element);

            var filters = new VisualElement();
            filters.AddToClassList(OdyClasses.FormRow);
            var typeChoices = new List<string> { AllTypesChoice };
            foreach (ContentDefinitionType type in ContentDefinitionFormModel.SupportedTypes) typeChoices.Add(type.ToString());
            DropdownField typeFilter = OdyUi.Dropdown("Type", typeChoices, 0, "catalog-type-filter");
            typeFilter.RegisterValueChangedCallback(evt => SetTypeFilter(EnumChoices.TryParse(evt.newValue, out ContentDefinitionType t) ? t : (ContentDefinitionType?)null));
            filters.Add(typeFilter);
            var statusChoices = new List<string> { AllStatusesChoice };
            statusChoices.AddRange(EnumChoices.Names<ContentDefinitionStatus>());
            DropdownField statusFilter = OdyUi.Dropdown("Status", statusChoices, 0, "catalog-status-filter");
            statusFilter.RegisterValueChangedCallback(evt => SetStatusFilter(EnumChoices.TryParse(evt.newValue, out ContentDefinitionStatus s) ? s : (ContentDefinitionStatus?)null));
            filters.Add(statusFilter);
            _root.Add(filters);

            _newRow = new VisualElement { name = "catalog-new-row" };
            _newRow.AddToClassList(OdyClasses.ButtonRow);
            _newRow.Add(OdyUi.Text("New:", OdyClasses.TextMuted));
            foreach (ContentDefinitionType type in ContentDefinitionFormModel.SupportedTypes)
            {
                ContentDefinitionType captured = type;
                _newRow.Add(OdyUi.Button(type.ToString(), () => StartNew(captured), OdyButtonVariant.Secondary, "catalog-new-" + type.ToString().ToLowerInvariant(), small: true));
            }

            _root.Add(_newRow);

            _list = new VisualElement { name = "catalog-list" };
            _list.AddToClassList(OdyClasses.List);
            _root.Add(_list);

            _detail = new VisualElement { name = "catalog-detail" };
            _root.Add(_detail);

            _roleSubscription = _context.Selection.Subscribe(_ => OnRoleChanged());
            _context.PresentationRuntime.AddSubscription(_roleSubscription);
            OnRoleChanged();
            return _root;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _roleSubscription?.Dispose();
            _disposed = true;
        }

        /// <summary>Reloads the catalog from the repository (non-MainGM: Published only; MainGM: everything incl. Archived).</summary>
        public Result Refresh()
        {
            CorrelationId correlationId = UiCommandIds.NewCorrelationId();
            Result<IReadOnlyList<ContentDefinitionRecord>> listed = _catalog.ListContentDefinitions(_context.Campaign, CanAuthor ? (ContentDefinitionStatus?)null : ContentDefinitionStatus.Published, correlationId);
            if (listed.IsFailure)
            {
                Banner.ShowError("Loading the catalog failed", listed.Error);
                return Result.Failure(listed.Error);
            }

            _definitions.Clear();
            foreach (ContentDefinitionRecord record in listed.Value)
            {
                if (ContentDefinitionFormModel.IsSupported(record.DefinitionType)) _definitions.Add(record);
            }

            if (CanAuthor)
            {
                // Archived rows through the lifecycle service (its MainGM-only query), merged without duplicates.
                Result<IReadOnlyList<ContentDefinitionRecord>> archived = ContentCatalogLifecycleService.ListArchivedDefinitions(_catalog, _context.CampaignRepository, new ListArchivedDefinitionsRequest(_context.Campaign, _context.ActorUserId, correlationId));
                if (archived.IsSuccess)
                {
                    foreach (ContentDefinitionRecord record in archived.Value)
                    {
                        if (ContentDefinitionFormModel.IsSupported(record.DefinitionType) && FindById(record.ContentDefinitionId) == null) _definitions.Add(record);
                    }
                }
            }

            if (Selected != null)
            {
                ContentDefinitionRecord? fresh = FindById(Selected.ContentDefinitionId);
                if (fresh == null)
                {
                    CloseEditor();
                }
                else if (fresh.Revision != Selected.Revision || fresh.Status != Selected.Status)
                {
                    OpenRecord(fresh);
                }
            }

            RenderList();
            return Result.Success();
        }

        public void SetTypeFilter(ContentDefinitionType? type)
        {
            TypeFilter = type;
            RenderList();
        }

        public void SetStatusFilter(ContentDefinitionStatus? status)
        {
            StatusFilter = status;
            RenderList();
        }

        /// <summary>Opens a blank draft of <paramref name="type"/> (MainGM only; others get the explanation).</summary>
        public bool StartNew(ContentDefinitionType type)
        {
            if (!CanAuthor)
            {
                Banner.Show(OdyBannerKind.Warning, "Only the MainGM can create catalog definitions.");
                return false;
            }

            Selected = null;
            Form = ContentDefinitionFormModel.CreateNew(type, _context.ActiveRulesetKey);
            _issues.Clear();
            Banner.Hide();
            RenderDetail();
            return true;
        }

        public Result Select(ContentDefinitionId definitionId)
        {
            ContentDefinitionRecord? record = FindById(definitionId);
            if (record == null)
            {
                Result<ContentDefinitionRecord> read = _catalog.GetContentDefinition(_context.Campaign, definitionId, UiCommandIds.NewCorrelationId());
                if (read.IsFailure)
                {
                    Banner.ShowError("Opening the definition failed", read.Error);
                    return Result.Failure(read.Error);
                }

                record = read.Value;
            }

            return OpenRecord(record);
        }

        /// <summary>Re-evaluates conditional field visibility after the model changed (the UI's own change callbacks call it too).</summary>
        public void ApplyFormChange()
        {
            if (Form == null) return;
            SetConditional("item.maxStack", Form.IsItemBased && Form.IsStackable);
            SetConditional("item.maxDurability", Form.IsItemBased && Form.HasDurability);
            SetConditional("item.maxCharges", Form.IsItemBased && Form.HasCharges);
            SetConditional("weapon.ammoKeys", Form.ShowsCompatibleAmmoKeys);
            SetConditional("effect.durationValue", Form.ShowsDurationValue);
            UpdateAmmoHint();
            ShowFieldErrors(Array.Empty<CatalogFieldError>());
        }

        public bool IsFieldVisible(string key) => _conditional.TryGetValue(key, out VisualElement element) && OdyUi.IsVisible(element);

        /// <summary>Creates the draft (new form) or saves the open Draft (name/description/properties at its current Revision).</summary>
        public Result<ContentDefinitionRecord> SaveDraft()
        {
            if (Form == null) return Fail<ContentDefinitionRecord>("Saving", CatalogUiErrors.NothingOpen());
            if (!CanAuthor) return Fail<ContentDefinitionRecord>("Saving", ContentCatalogAuthoringFailures.NotMainGm(UiCommandIds.NewCorrelationId()));
            if (!Form.TryBuildPropertiesJson(out string propertiesJson, out IReadOnlyList<CatalogFieldError> errors))
            {
                ShowFieldErrors(errors);
                Banner.Show(OdyBannerKind.Error, "Fix the highlighted fields: " + errors[0].Message);
                return Result<ContentDefinitionRecord>.Failure(CatalogUiErrors.FormInvalid());
            }

            CorrelationId correlationId = UiCommandIds.NewCorrelationId();
            string? description = string.IsNullOrWhiteSpace(Form.Description) ? null : Form.Description.Trim();
            Result<ContentDefinitionRecord> saved;
            if (Selected == null)
            {
                var request = new CreateDraftDefinitionRequest(_context.Campaign, Form.Type, Form.Name.Trim(), description, _context.ActorUserId, UiCommandIds.NewCommandId(), correlationId, Form.RulesetCompatibility, Form.Tags, propertiesJson);
                saved = ContentCatalogAuthoringService.CreateDraftDefinition(_catalog, _context.CampaignRepository, request);
            }
            else
            {
                var request = new UpdateDraftDefinitionRequest(_context.Campaign, Selected.ContentDefinitionId, Form.Name.Trim(), description, propertiesJson, Selected.Revision, _context.ActorUserId, UiCommandIds.NewCommandId(), correlationId);
                saved = ContentCatalogAuthoringService.UpdateDraftDefinition(_catalog, _context.CampaignRepository, request);
            }

            if (saved.IsFailure) return FailAndResync<ContentDefinitionRecord>("Saving", saved.Error);
            AcceptServerRecord(saved.Value);
            Banner.Show(OdyBannerKind.Success, "Draft saved (revision " + saved.Value.Revision + ").");
            return saved;
        }

        /// <summary>Runs the backend's publish validation on the open Draft and lists its issues readably.</summary>
        public Result<CatalogValidationResult> ValidateSelected()
        {
            if (Selected == null) return Fail<CatalogValidationResult>("Validation", CatalogUiErrors.NothingOpen());
            Result<CatalogValidationResult> validated = CatalogValidationService.ValidateDraftForPublish(_catalog, new ValidateContentDefinitionRequest(_context.Campaign, Selected.ContentDefinitionId, UiCommandIds.NewCorrelationId()));
            if (validated.IsFailure) return Fail<CatalogValidationResult>("Validation", validated.Error);
            ShowIssues(validated.Value);
            if (validated.Value.IsValid) Banner.Show(OdyBannerKind.Success, "Ready to publish: no validation issues.");
            else Banner.Show(OdyBannerKind.Warning, "Publishing would fail: " + _issues[0]);
            return validated;
        }

        /// <summary>Publishes the open Draft at its current Revision; on a validation failure lists the concrete issues.</summary>
        public Result<ContentDefinitionRecord> PublishSelected()
        {
            if (Selected == null) return Fail<ContentDefinitionRecord>("Publishing", CatalogUiErrors.NothingOpen());
            var request = new PublishDefinitionRequest(_context.Campaign, Selected.ContentDefinitionId, Selected.Revision, _context.ActorUserId, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId());
            Result<ContentDefinitionRecord> published = ContentCatalogLifecycleService.PublishDefinition(_catalog, _context.CampaignRepository, request);
            if (published.IsFailure)
            {
                if (published.Error.UserMessageKey.ToString() == "errors.content_catalog.publish_validation_failed")
                {
                    Result<CatalogValidationResult> validated = CatalogValidationService.ValidateDraftForPublish(_catalog, new ValidateContentDefinitionRequest(_context.Campaign, Selected.ContentDefinitionId, UiCommandIds.NewCorrelationId()));
                    if (validated.IsSuccess) ShowIssues(validated.Value);
                    Banner.Show(OdyBannerKind.Error, "Not published: " + (_issues.Count > 0 ? _issues[0] : OdyMessages.Describe(published.Error)));
                    return published;
                }

                return FailAndResync<ContentDefinitionRecord>("Publishing", published.Error);
            }

            AcceptServerRecord(published.Value);
            Banner.Show(OdyBannerKind.Success, "Published version " + published.Value.Version + ".");
            return published;
        }

        /// <summary>Creates the next Draft version from the open Published definition and opens it.</summary>
        public Result<ContentDefinitionRecord> CreateNextVersion()
        {
            if (Selected == null) return Fail<ContentDefinitionRecord>("New version", CatalogUiErrors.NothingOpen());
            long sourceVersion = Selected.Version;
            var request = new CreateNextDraftVersionFromPublishedRequest(_context.Campaign, Selected.ContentDefinitionId, _context.ActorUserId, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId());
            Result<ContentDefinitionRecord> created = ContentCatalogAuthoringService.CreateNextDraftVersionFromPublished(_catalog, _context.CampaignRepository, request);
            if (created.IsFailure) return FailAndResync<ContentDefinitionRecord>("New version", created.Error);
            AcceptServerRecord(created.Value);
            Banner.Show(OdyBannerKind.Success, "New draft created from published version " + sourceVersion + ".");
            return created;
        }

        /// <summary>Asks for confirmation (irreversible) before archiving.</summary>
        public OdyConfirmDialog? RequestArchive()
        {
            if (Selected == null) return null;
            ContentDefinitionRecord target = Selected;
            return OdyConfirmDialog.Show(_context.ModalHost,
                new OdyConfirmOptions("Archive \"" + target.Name + "\"?", "Archived definitions cannot be published or edited again. Existing references keep working.", "Archive") { Destructive = true },
                _ => ArchiveSelected(null));
        }

        public Result<ContentDefinitionRecord> ArchiveSelected(string? reason)
        {
            if (Selected == null) return Fail<ContentDefinitionRecord>("Archiving", CatalogUiErrors.NothingOpen());
            var request = new ArchiveDefinitionRequest(_context.Campaign, Selected.ContentDefinitionId, reason, _context.ActorUserId, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId());
            Result<ContentDefinitionRecord> archived = ContentCatalogLifecycleService.ArchiveDefinition(_catalog, _context.CampaignRepository, request);
            if (archived.IsFailure) return FailAndResync<ContentDefinitionRecord>("Archiving", archived.Error);
            AcceptServerRecord(archived.Value);
            Banner.Show(OdyBannerKind.Success, "Archived.");
            return archived;
        }

        /// <summary>Asks for confirmation (irreversible) before physically deleting a Draft.</summary>
        public OdyConfirmDialog? RequestDelete()
        {
            if (Selected == null) return null;
            ContentDefinitionRecord target = Selected;
            return OdyConfirmDialog.Show(_context.ModalHost,
                new OdyConfirmOptions("Delete draft \"" + target.Name + "\"?", "The draft is removed permanently. This cannot be undone.", "Delete") { Destructive = true },
                _ => DeleteSelected());
        }

        public Result DeleteSelected()
        {
            if (Selected == null) return Result.Failure(CatalogUiErrors.NothingOpen());
            var request = new DeleteDraftDefinitionRequest(_context.Campaign, Selected.ContentDefinitionId, _context.ActorUserId, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId());
            Result deleted = ContentCatalogLifecycleService.DeleteDraftDefinition(_catalog, _context.CampaignRepository, request);
            if (deleted.IsFailure)
            {
                Banner.ShowError("Deleting", deleted.Error);
                return deleted;
            }

            CloseEditor();
            Refresh();
            Banner.Show(OdyBannerKind.Success, "Draft deleted.");
            return deleted;
        }

        // ---- internals ------------------------------------------------------------------------------

        private Result OpenRecord(ContentDefinitionRecord record)
        {
            Result<ContentDefinitionFormModel> loaded = ContentDefinitionFormModel.Load(record, UiCommandIds.NewCorrelationId());
            if (loaded.IsFailure)
            {
                Banner.ShowError("Opening the definition failed", loaded.Error);
                return Result.Failure(loaded.Error);
            }

            Selected = record;
            Form = loaded.Value;
            _issues.Clear();
            RenderDetail();
            RenderList();
            return Result.Success();
        }

        private void AcceptServerRecord(ContentDefinitionRecord record)
        {
            Refresh();
            OpenRecord(FindById(record.ContentDefinitionId) ?? record);
        }

        private void CloseEditor()
        {
            Selected = null;
            Form = null;
            _issues.Clear();
            RenderDetail();
        }

        private ContentDefinitionRecord? FindById(ContentDefinitionId id)
        {
            foreach (ContentDefinitionRecord record in _definitions)
            {
                if (record.ContentDefinitionId.Equals(id)) return record;
            }

            return null;
        }

        private Result<T> Fail<T>(string action, Error error)
        {
            Banner.ShowError(action, error);
            return Result<T>.Failure(error);
        }

        // A stale revision means someone else changed the row: reload so the next attempt uses the server's state.
        private Result<T> FailAndResync<T>(string action, Error error)
        {
            if (error.SafeReasonCode.Equals(SafeReasonCode.StateChanged)) Refresh();
            Banner.ShowError(action, error);
            return Result<T>.Failure(error);
        }

        private void ShowIssues(CatalogValidationResult result)
        {
            _issues.Clear();
            foreach (CatalogValidationIssue issue in result.Issues)
            {
                string text = CatalogIssueText.Describe(issue);
                _issues.Add(issue.Severity == CatalogValidationSeverity.Warning ? "Warning: " + text : text);
            }

            RenderIssues();
        }

        private void OnRoleChanged()
        {
            if (_roleNotice == null) return;
            if (CanAuthor) _roleNotice.Hide();
            else _roleNotice.Show(OdyBannerKind.Info, "Only the MainGM can author the content catalog. You can browse published definitions.");
            if (_newRow != null) OdyUi.SetVisible(_newRow, CanAuthor);
            if (!CanAuthor && Selected == null) Form = null;
            Refresh();
            RenderDetail();
        }

        private void RenderList()
        {
            if (_list == null) return;
            _list.Clear();
            _visible.Clear();
            foreach (ContentDefinitionRecord record in _definitions)
            {
                if (TypeFilter.HasValue && record.DefinitionType != TypeFilter.Value) continue;
                if (StatusFilter.HasValue && record.Status != StatusFilter.Value) continue;
                _visible.Add(record);
            }

            if (_visible.Count == 0)
            {
                _list.Add(OdyUi.EmptyState(CanAuthor ? "No definitions yet. Use New to create one." : "No published definitions.", "catalog-empty"));
                return;
            }

            foreach (ContentDefinitionRecord record in _visible)
            {
                ContentDefinitionId id = record.ContentDefinitionId;
                var row = new Button(() => Select(id)) { name = "catalog-row-" + id, text = string.Empty };
                row.AddToClassList(OdyClasses.ListItem);
                if (Selected != null && Selected.ContentDefinitionId.Equals(id)) row.AddToClassList(OdyClasses.ListItemSelected);
                var main = new VisualElement();
                main.AddToClassList(OdyClasses.ListItemMain);
                main.Add(OdyUi.Text(record.Name, OdyClasses.ListItemTitle));
                main.Add(OdyUi.Text(record.DefinitionType + "  ·  " + VersionText(record) + "  ·  rev " + record.Revision, OdyClasses.ListItemMeta));
                row.Add(main);
                row.Add(OdyUi.Badge(record.Status.ToString(), StatusKind(record.Status)));
                _list.Add(row);
            }
        }

        private void RenderDetail()
        {
            if (_detail == null) return;
            _detail.Clear();
            _conditional.Clear();
            _fieldsByKey.Clear();
            if (Form == null) return;

            ContentDefinitionFormModel form = Form;
            bool editable = IsFormEditable;
            VisualElement card = OdyUi.Card(Selected == null ? "New " + form.Type : form.Type + " · " + Selected.Status + " · " + VersionText(Selected), out VisualElement body, "catalog-editor");
            _detail.Add(card);
            if (!editable && Selected != null && Selected.Status != ContentDefinitionStatus.Draft)
            {
                body.Add(OdyUi.Text(Selected.Status == ContentDefinitionStatus.Published
                    ? "Published definitions are immutable. Create a new version to change it."
                    : "Archived definitions are read-only.", OdyClasses.FieldHint));
            }

            // Common envelope
            VisualElement common = OdyUi.Section("General");
            body.Add(common);
            AddText(common, "name", "Name", "catalog-name", () => form.Name, v => form.Name = v, editable);
            AddText(common, "description", "Description", "catalog-description", () => form.Description, v => form.Description = v, editable, multiline: true);
            bool envelopeEditable = editable && Selected == null;
            AddText(common, "tags", "Tags (comma separated)", "catalog-tags", () => form.TagsText, v => form.TagsText = v, envelopeEditable);
            AddText(common, "ruleset", "Ruleset compatibility", "catalog-ruleset", () => form.RulesetCompatibilityText, v => form.RulesetCompatibilityText = v, envelopeEditable);
            if (editable && Selected != null) common.Add(OdyUi.Text("Tags and ruleset compatibility are fixed when the draft is created.", OdyClasses.FieldHint));

            if (form.IsItemBased) RenderItemFields(body, form, editable);
            switch (form.Type)
            {
                case ContentDefinitionType.Weapon:
                    RenderWeaponFields(body, form, editable);
                    break;
                case ContentDefinitionType.Armor:
                    RenderArmorFields(body, form, editable);
                    break;
                case ContentDefinitionType.Ammo:
                    RenderAmmoFields(body, form, editable);
                    break;
                case ContentDefinitionType.Ability:
                    RenderAbilityFields(body, form, editable);
                    break;
                case ContentDefinitionType.Effect:
                    RenderEffectFields(body, form, editable);
                    break;
                case ContentDefinitionType.Skill:
                    body.Add(OdyUi.Text("Skills have no type-specific fields: name, description and tags are the whole definition.", OdyClasses.FieldHint));
                    break;
            }

            var issues = new VisualElement { name = "catalog-issues" };
            body.Add(issues);

            var actions = new VisualElement { name = "catalog-actions" };
            actions.AddToClassList(OdyClasses.ButtonRow);
            if (CanAuthor)
            {
                if (Selected == null || Selected.Status == ContentDefinitionStatus.Draft)
                {
                    actions.Add(OdyUi.Button(Selected == null ? "Create draft" : "Save draft", () => SaveDraft(), OdyButtonVariant.Primary, "catalog-save"));
                }

                if (Selected != null && Selected.Status == ContentDefinitionStatus.Draft)
                {
                    actions.Add(OdyUi.Button("Validate", () => ValidateSelected(), OdyButtonVariant.Secondary, "catalog-validate"));
                    actions.Add(OdyUi.Button("Publish", () => PublishSelected(), OdyButtonVariant.Primary, "catalog-publish"));
                    actions.Add(OdyUi.Button("Delete draft", () => RequestDelete(), OdyButtonVariant.Danger, "catalog-delete"));
                }

                if (Selected != null && Selected.Status == ContentDefinitionStatus.Published)
                {
                    actions.Add(OdyUi.Button("New version", () => CreateNextVersion(), OdyButtonVariant.Primary, "catalog-new-version"));
                    actions.Add(OdyUi.Button("Archive", () => RequestArchive(), OdyButtonVariant.Danger, "catalog-archive"));
                }
            }

            actions.Add(OdyUi.Button("Close", CloseEditor, OdyButtonVariant.Ghost, "catalog-close"));
            body.Add(actions);

            ApplyFormChange();
            RenderIssues();
        }

        private void RenderIssues()
        {
            VisualElement? container = _detail?.Q<VisualElement>("catalog-issues");
            if (container == null) return;
            container.Clear();
            if (_issues.Count == 0) return;
            VisualElement section = OdyUi.Section("Publish validation");
            foreach (string issue in _issues)
            {
                Label label = OdyUi.Text("• " + issue, OdyClasses.TextWrap, OdyClasses.TextDanger);
                label.name = "catalog-issue";
                section.Add(label);
            }

            container.Add(section);
        }

        private void RenderItemFields(VisualElement body, ContentDefinitionFormModel form, bool editable)
        {
            VisualElement section = OdyUi.Section("Item");
            body.Add(section);
            AddEnum(section, "item.category", "Category", "catalog-item-category", () => form.Category, v => form.Category = v, editable);
            AddLong(section, "item.weight", "Weight", "catalog-item-weight", () => form.Weight, v => form.Weight = v, editable);
            AddToggle(section, "item.stackable", "Stackable", "catalog-item-stackable", () => form.IsStackable, v => form.IsStackable = v, editable);
            AddLong(section, "item.maxStack", "Max stack size", "catalog-item-max-stack", () => form.MaxStackSize, v => form.MaxStackSize = v, editable, conditional: true);
            AddToggle(section, "item.durability", "Has durability", "catalog-item-has-durability", () => form.HasDurability, v => form.HasDurability = v, editable);
            AddLong(section, "item.maxDurability", "Max durability", "catalog-item-max-durability", () => form.MaxDurability, v => form.MaxDurability = v, editable, conditional: true);
            AddToggle(section, "item.charges", "Has charges", "catalog-item-has-charges", () => form.HasCharges, v => form.HasCharges = v, editable);
            AddLong(section, "item.maxCharges", "Max charges", "catalog-item-max-charges", () => form.MaxCharges, v => form.MaxCharges = v, editable, conditional: true);
            AddRefPicker(section, "Built-in abilities", "catalog-item-abilities", ContentDefinitionType.Ability, form.BuiltInAbilityRefs, editable);
            AddRefPicker(section, "Built-in effects", "catalog-item-effects", ContentDefinitionType.Effect, form.BuiltInEffectRefs, editable);
        }

        private void RenderWeaponFields(VisualElement body, ContentDefinitionFormModel form, bool editable)
        {
            VisualElement section = OdyUi.Section("Weapon");
            body.Add(section);
            AddText(section, "weapon.damage", "Damage expression", "catalog-weapon-damage", () => form.DamageExpression, v => form.DamageExpression = v, editable);
            AddLong(section, "weapon.range", "Range", "catalog-weapon-range", () => form.Range, v => form.Range = v, editable);
            AddEnum(section, "weapon.mode", "Attack mode", "catalog-weapon-mode", () => form.AttackMode, v => form.AttackMode = v, editable);
            AddLong(section, "weapon.actionCost", "Action cost", "catalog-weapon-action-cost", () => form.WeaponActionCost, v => form.WeaponActionCost = v, editable);
            AddEnum(section, "weapon.ammo", "Ammunition", "catalog-weapon-ammo", () => form.AmmoRequirement, v => form.AmmoRequirement = v, editable);
            AddText(section, "weapon.ammoKeys", "Compatible ammo keys (comma separated)", "catalog-weapon-ammo-keys", () => form.CompatibleAmmoKeysText, v => form.CompatibleAmmoKeysText = v, editable, conditional: true);
            Label hint = OdyUi.Text(string.Empty, OdyClasses.FieldHint);
            hint.name = "catalog-weapon-ammo-hint";
            OdyUi.SetVisible(hint, false);
            section.Add(hint);
        }

        private void RenderArmorFields(VisualElement body, ContentDefinitionFormModel form, bool editable)
        {
            VisualElement section = OdyUi.Section("Armor");
            body.Add(section);
            AddText(section, "armor.slot", "Equipment slot key", "catalog-armor-slot", () => form.EquipmentSlotKey, v => form.EquipmentSlotKey = v, editable);
            AddText(section, "armor.bodyParts", "Covered body part ids (comma separated)", "catalog-armor-body-parts", () => form.CoveredBodyPartIdsText, v => form.CoveredBodyPartIdsText = v, editable);
            section.Add(OdyUi.Text("Temporary text input: a body-part picker needs character anatomy in the UI (character sheet).", OdyClasses.FieldHint));
            AddLong(section, "armor.protection", "Protection", "catalog-armor-protection", () => form.Protection, v => form.Protection = v, editable);
        }

        private void RenderAmmoFields(VisualElement body, ContentDefinitionFormModel form, bool editable)
        {
            VisualElement section = OdyUi.Section("Ammunition");
            body.Add(section);
            AddText(section, "ammo.keys", "Compatibility keys (comma separated)", "catalog-ammo-keys", () => form.AmmoCompatibilityKeysText, v => form.AmmoCompatibilityKeysText = v, editable);
            AddText(section, "ammo.damage", "Damage contribution (optional)", "catalog-ammo-damage", () => form.DamageContribution, v => form.DamageContribution = v, editable);
            AddRefPicker(section, "Effect contributions", "catalog-ammo-effects", ContentDefinitionType.Effect, form.EffectContributionRefs, editable);
        }

        private void RenderAbilityFields(VisualElement body, ContentDefinitionFormModel form, bool editable)
        {
            VisualElement section = OdyUi.Section("Ability");
            body.Add(section);
            AddEnum(section, "ability.entry", "Entry point", "catalog-ability-entry", () => form.EntryPointType, v => form.EntryPointType = v, editable);
            AddText(section, "ability.trigger", "Trigger", "catalog-ability-trigger", () => form.Trigger, v => form.Trigger = v, editable);
            AddLong(section, "ability.actionCost", "Action cost", "catalog-ability-action-cost", () => form.AbilityActionCost, v => form.AbilityActionCost = v, editable);

            VisualElement costs = OdyUi.Section("Resource costs", "catalog-ability-costs");
            for (int index = 0; index < form.ResourceCosts.Count; index++)
            {
                ResourceCostModel cost = form.ResourceCosts[index];
                var row = new VisualElement();
                row.AddToClassList(OdyClasses.FormRow);
                TextField id = OdyUi.TextField("Resource id", cost.ResourceDefinitionId, "catalog-ability-cost-id-" + index);
                id.RegisterValueChangedCallback(evt => cost.ResourceDefinitionId = evt.newValue);
                id.SetEnabled(editable);
                row.Add(id);
                IntegerField amount = OdyUi.IntegerField("Amount", EnumChoices.ClampToInt(cost.Amount), "catalog-ability-cost-amount-" + index);
                amount.RegisterValueChangedCallback(evt => cost.Amount = evt.newValue);
                amount.SetEnabled(editable);
                row.Add(amount);
                if (editable)
                {
                    ResourceCostModel captured = cost;
                    row.Add(OdyUi.Button("Remove", () => { form.ResourceCosts.Remove(captured); RenderDetail(); }, OdyButtonVariant.Ghost, "catalog-ability-cost-remove-" + index, small: true));
                }

                costs.Add(row);
            }

            if (editable) costs.Add(OdyUi.Button("+ Add resource cost", () => { form.ResourceCosts.Add(new ResourceCostModel(string.Empty, 1)); RenderDetail(); }, OdyButtonVariant.Secondary, "catalog-ability-cost-add", small: true));
            section.Add(costs);

            section.Add(TargetRuleEditor.Build(form.AbilityTargetRule, "catalog-ability", editable, ApplyFormChange));
            AddText(section, "ability.mechanics", "Mechanics payload ref (optional)", "catalog-ability-mechanics", () => form.AbilityMechanicsPayloadRef, v => form.AbilityMechanicsPayloadRef = v, editable);
        }

        private void RenderEffectFields(VisualElement body, ContentDefinitionFormModel form, bool editable)
        {
            VisualElement section = OdyUi.Section("Effect");
            body.Add(section);
            section.Add(TargetRuleEditor.Build(form.EffectTargetRule, "catalog-effect", editable, ApplyFormChange));
            AddEnum(section, "effect.duration", "Duration", "catalog-effect-duration", () => form.DurationType, v => form.DurationType = v, editable);
            AddLong(section, "effect.durationValue", "Duration value", "catalog-effect-duration-value", () => form.DurationValue, v => form.DurationValue = v, editable, conditional: true);
            AddEnum(section, "effect.stack", "Stack policy", "catalog-effect-stack", () => form.StackPolicy, v => form.StackPolicy = v, editable);
            AddText(section, "effect.mechanics", "Mechanics payload ref (optional)", "catalog-effect-mechanics", () => form.EffectMechanicsPayloadRef, v => form.EffectMechanicsPayloadRef = v, editable);
        }

        private void AddText(VisualElement parent, string key, string label, string name, Func<string> get, Action<string> set, bool editable, bool multiline = false, bool conditional = false)
        {
            TextField field = OdyUi.TextField(label, get(), name, multiline);
            field.RegisterValueChangedCallback(evt =>
            {
                set(evt.newValue);
                ApplyFormChange();
            });
            field.SetEnabled(editable);
            Register(parent, key, field, conditional);
        }

        private void AddLong(VisualElement parent, string key, string label, string name, Func<long> get, Action<long> set, bool editable, bool conditional = false)
        {
            IntegerField field = OdyUi.IntegerField(label, EnumChoices.ClampToInt(get()), name);
            field.RegisterValueChangedCallback(evt =>
            {
                set(evt.newValue);
                ApplyFormChange();
            });
            field.SetEnabled(editable);
            Register(parent, key, field, conditional);
        }

        private void AddToggle(VisualElement parent, string key, string label, string name, Func<bool> get, Action<bool> set, bool editable)
        {
            Toggle toggle = OdyUi.Toggle(label, get(), name);
            toggle.RegisterValueChangedCallback(evt =>
            {
                set(evt.newValue);
                ApplyFormChange();
            });
            toggle.SetEnabled(editable);
            Register(parent, key, toggle, conditional: false);
        }

        private void AddEnum<T>(VisualElement parent, string key, string label, string name, Func<T> get, Action<T> set, bool editable) where T : struct, Enum
        {
            List<string> choices = EnumChoices.Names<T>();
            DropdownField field = OdyUi.Dropdown(label, choices, choices.IndexOf(get().ToString()), name);
            field.RegisterValueChangedCallback(evt =>
            {
                if (EnumChoices.TryParse(evt.newValue, out T parsed)) set(parsed);
                ApplyFormChange();
            });
            field.SetEnabled(editable);
            Register(parent, key, field, conditional: false);
        }

        /// <summary>Toggle list of Published definitions of <paramref name="refType"/>; each ref pins that exact version.</summary>
        private void AddRefPicker(VisualElement parent, string label, string name, ContentDefinitionType refType, List<ContentDefinitionRef> refs, bool editable)
        {
            var box = new VisualElement { name = name };
            box.Add(OdyUi.Text(label, OdyClasses.TextLabel));
            var shown = new HashSet<string>(StringComparer.Ordinal);
            foreach (ContentDefinitionRecord candidate in _definitions)
            {
                if (candidate.DefinitionType != refType || candidate.Status != ContentDefinitionStatus.Published) continue;
                var reference = new ContentDefinitionRef(candidate.ContentDefinitionId, candidate.Version);
                shown.Add(reference.ToString());
                Toggle toggle = OdyUi.Toggle(candidate.Name + " (v" + candidate.Version + ")", refs.Contains(reference), name + "-" + candidate.ContentDefinitionId);
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue && !refs.Contains(reference)) refs.Add(reference);
                    if (!evt.newValue) refs.Remove(reference);
                });
                toggle.SetEnabled(editable);
                box.Add(toggle);
            }

            foreach (ContentDefinitionRef existing in refs)
            {
                if (!shown.Contains(existing.ToString())) box.Add(OdyUi.Text("Pinned: " + existing + " (not a current published " + refType + ")", OdyClasses.FieldHint));
            }

            if (shown.Count == 0 && refs.Count == 0) box.Add(OdyUi.Text("No published " + refType + " definitions to reference yet.", OdyClasses.FieldHint));
            parent.Add(box);
        }

        private void Register(VisualElement parent, string key, VisualElement field, bool conditional)
        {
            parent.Add(field);
            _fieldsByKey[key] = field;
            if (conditional) _conditional[key] = field;
        }

        private void SetConditional(string key, bool visible)
        {
            if (_conditional.TryGetValue(key, out VisualElement element)) OdyUi.SetVisible(element, visible);
        }

        private void ShowFieldErrors(IReadOnlyList<CatalogFieldError> errors)
        {
            foreach (VisualElement field in _fieldsByKey.Values) field.RemoveFromClassList(OdyClasses.FieldInvalid);
            foreach (CatalogFieldError error in errors)
            {
                string key = error.Field.StartsWith("ability.cost.", StringComparison.Ordinal) ? string.Empty : error.Field;
                if (_fieldsByKey.TryGetValue(key, out VisualElement field)) field.AddToClassList(OdyClasses.FieldInvalid);
            }
        }

        // Weapon + AmmoRequirement.Required: an advisory hint so the author learns before publishing that no Ammo shares
        // the keys (the backend's publish validation is the authority; this only reads the already-loaded catalog).
        private void UpdateAmmoHint()
        {
            Label? hint = _detail?.Q<Label>("catalog-weapon-ammo-hint");
            if (hint == null || Form == null) return;
            bool show = Form.Type == ContentDefinitionType.Weapon && Form.AmmoRequirement == AmmoRequirement.Required
                && OdyUi.ParseList(Form.CompatibleAmmoKeysText).Count > 0
                && !Form.HasCompatibleAmmoIn(_definitions, _context.ActiveRulesetKey, UiCommandIds.NewCorrelationId());
            hint.text = show ? "No Ammo definition in the catalog shares these keys yet -- publishing a weapon that requires ammunition will fail until one exists." : string.Empty;
            OdyUi.SetVisible(hint, show);
        }

        public bool AmmoHintVisible => _detail?.Q<Label>("catalog-weapon-ammo-hint") is Label hint && OdyUi.IsVisible(hint);

        private static string VersionText(ContentDefinitionRecord record) => record.Version > 0 ? "v" + record.Version : "unpublished";

        private static OdyStatusKind StatusKind(ContentDefinitionStatus status)
        {
            switch (status)
            {
                case ContentDefinitionStatus.Draft:
                    return OdyStatusKind.Draft;
                case ContentDefinitionStatus.Published:
                    return OdyStatusKind.Published;
                default:
                    return OdyStatusKind.Archived;
            }
        }
    }

    /// <summary>Readable text for each catalog validation issue code (safe, fixed sentences).</summary>
    public static class CatalogIssueText
    {
        public static string Describe(CatalogValidationIssue issue)
        {
            if (issue == null) throw new ArgumentNullException(nameof(issue));
            string where = string.IsNullOrEmpty(issue.FieldPath) ? string.Empty : " (" + issue.FieldPath + ")";
            switch (issue.IssueCode)
            {
                case CatalogValidationIssueCode.DefinitionNotDraft:
                    return "Only a Draft can be published.";
                case CatalogValidationIssueCode.TypedPayloadWrongType:
                    return "The stored properties belong to a different type.";
                case CatalogValidationIssueCode.TypedPayloadMalformed:
                    return "The stored properties could not be read" + where + ".";
                case CatalogValidationIssueCode.RulesetIncompatible:
                    return "Ruleset compatibility does not include the campaign's active ruleset" + where + ".";
                case CatalogValidationIssueCode.ReferenceMissing:
                    return "A referenced definition does not exist" + where + ".";
                case CatalogValidationIssueCode.ReferenceVersionMismatch:
                    return "A referenced definition version is not published" + where + ".";
                case CatalogValidationIssueCode.ReferenceWrongType:
                    return "A reference points to a definition of the wrong type" + where + ".";
                case CatalogValidationIssueCode.DependencyCycleDetected:
                    return "The references form a cycle" + where + ".";
                case CatalogValidationIssueCode.DependencyGraphTooDeep:
                    return "The reference chain is too deep" + where + ".";
                case CatalogValidationIssueCode.WeaponAmmoCompatibilityKeysRequired:
                    return "The weapon uses ammunition but lists no compatible ammo keys.";
                case CatalogValidationIssueCode.WeaponNoCompatibleAmmoInCatalog:
                    return "The weapon requires ammunition, but no Ammo definition in the catalog shares any of its compatible ammo keys. Create an Ammo definition with a matching compatibility key first.";
                case CatalogValidationIssueCode.AbilityMechanicsPayloadRefInvalid:
                    return "The ability's mechanics payload ref must not be blank when set.";
                case CatalogValidationIssueCode.EffectMechanicsPayloadRefInvalid:
                    return "The effect's mechanics payload ref must not be blank when set.";
                default:
                    return "Validation issue " + issue.IssueCode + where + ".";
            }
        }
    }

    internal static class CatalogUiErrors
    {
        private static readonly CorrelationId Placeholder = CorrelationId.Parse("corr_00000000000000000000000000000000");

        internal static Error NothingOpen() => Error.Create(ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Validation, SafeReasonCode.InvalidRequest, UserMessageKey.Parse("errors.catalog_ui.nothing_open"), RetryDirective.DoNotRetry, Placeholder);

        internal static Error FormInvalid() => Error.Create(ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Validation, SafeReasonCode.InvalidRequest, UserMessageKey.Parse("errors.catalog_ui.form_invalid"), RetryDirective.DoNotRetry, Placeholder);
    }
}
