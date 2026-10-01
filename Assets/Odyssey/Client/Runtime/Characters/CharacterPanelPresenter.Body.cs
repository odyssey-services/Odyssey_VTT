using System;
using System.Collections.Generic;
using Odyssey.Application.CharacterAdvancement;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    // ODY-S11-203: sheet tabs Resources, Anatomy, Ownership, History (ledger, purchases/revert, respec).
    public sealed partial class CharacterPanelPresenter
    {
        private readonly List<DevelopmentTransactionRecord> _ledger = new List<DevelopmentTransactionRecord>();
        private readonly List<AdvancementPurchase> _purchases = new List<AdvancementPurchase>();

        public IReadOnlyList<DevelopmentTransactionRecord> Ledger => _ledger;
        public IReadOnlyList<AdvancementPurchase> Purchases => _purchases;

        /// <summary>The last respec plan shown to the user (advisory: Apply recomputes it server-side from a fresh read).</summary>
        public CharacterRespecPreview? LastRespecPreview { get; private set; }

        // ---- Resources ----------------------------------------------------------------------------

        public Result<CharacterRecord> InitializeResource(string resourceKey)
        {
            if (!ResourceDefinitionId.TryParse((resourceKey ?? string.Empty).Trim(), out ResourceDefinitionId resourceId))
            {
                Banner.Show(OdyBannerKind.Error, "\"" + resourceKey + "\" is not a valid resource key.");
                return Result<CharacterRecord>.Failure(UiGuard.InvalidRequest());
            }

            return Mutate("Add resource", c => CharacterAdvancementService.InitializeResourceWithDefaults(_characters, _context.Campaign, c.CharacterId, resourceId, _context.ActorUserId, c.Revisions.CharacterResourcesRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()), resourceId + " added with the ruleset defaults.");
        }

        public Result<CharacterRecord> SetResourceCurrent(CharacterResourceId resourceId, long value) =>
            Mutate("Set resource", c => _characters.SetResourceCurrentValue(_context.Campaign, c.CharacterId, resourceId, value, _context.ActorUserId, c.Revisions.CharacterResourcesRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()), "Resource set to " + value + ".");

        public Result<CharacterRecord> SetResourceMaximum(CharacterResourceId resourceId, long baseMaximum, long permanentAdjustment) =>
            Mutate("Set maximum", c => _characters.SetResourceMaximum(_context.Campaign, c.CharacterId, resourceId, baseMaximum, permanentAdjustment, _context.ActorUserId, c.Revisions.CharacterResourcesRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()), "Maximum updated.");

        public static long EffectiveMaximum(CharacterResource resource) => resource.BaseMaximum + resource.PermanentMaximumAdjustment;

        /// <summary>The rendered bar of a resource of the open character (by resource definition key), or <c>null</c>.</summary>
        public OdyResourceBar? ResourceBar(string resourceKey) => _resourceBars.TryGetValue(resourceKey ?? string.Empty, out OdyResourceBar? bar) ? bar : null;

        // ---- Anatomy ------------------------------------------------------------------------------

        public Result<CharacterRecord> InitializeDefaultAnatomy(string profileId)
        {
            if (!AnatomyProfileDefinitionId.TryParse((profileId ?? string.Empty).Trim(), out AnatomyProfileDefinitionId id))
            {
                Banner.Show(OdyBannerKind.Error, "\"" + profileId + "\" is not a valid anatomy profile id.");
                return Result<CharacterRecord>.Failure(UiGuard.InvalidRequest());
            }

            return Mutate("Initialize anatomy", c => CharacterAdvancementService.InitializeAnatomyWithDefaults(_characters, _context.Campaign, c.CharacterId, id, _context.ActorUserId, c.Revisions.CharacterAnatomyRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()), "Default anatomy initialized.");
        }

        public Result<CharacterRecord> AddBodyPart(string bodyPartKey, string name, long damageLimit, string? attachedToKey)
        {
            if (!BodyPartId.TryParse((bodyPartKey ?? string.Empty).Trim(), out BodyPartId bodyPartId))
            {
                Banner.Show(OdyBannerKind.Error, "\"" + bodyPartKey + "\" is not a valid body part id.");
                return Result<CharacterRecord>.Failure(UiGuard.InvalidRequest());
            }

            BodyPartId? attachedTo = null;
            if (!string.IsNullOrWhiteSpace(attachedToKey))
            {
                if (!BodyPartId.TryParse(attachedToKey!.Trim(), out BodyPartId parent))
                {
                    Banner.Show(OdyBannerKind.Error, "\"" + attachedToKey + "\" is not a valid body part id.");
                    return Result<CharacterRecord>.Failure(UiGuard.InvalidRequest());
                }

                attachedTo = parent;
            }

            if (string.IsNullOrWhiteSpace(name) || damageLimit <= 0)
            {
                Banner.Show(OdyBannerKind.Error, "A body part needs a name and a damage limit above 0.");
                return Result<CharacterRecord>.Failure(UiGuard.InvalidRequest());
            }

            return Mutate("Add body part", c => _characters.AddBodyPart(_context.Campaign, c.CharacterId, bodyPartId, name.Trim(), damageLimit, attachedTo, "{}", _context.ActorUserId, c.Revisions.CharacterAnatomyRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()), bodyPartId + " added.");
        }

        /// <summary>Fails readably when something is attached to the part or equipment is worn on it (backend dependency check).</summary>
        public Result<CharacterRecord> RemoveBodyPart(BodyPartId bodyPartId) =>
            Mutate("Remove body part", c => _characters.RemoveBodyPart(_context.Campaign, c.CharacterId, bodyPartId, _context.ActorUserId, c.Revisions.CharacterAnatomyRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()), bodyPartId + " removed.");

        public Result<CharacterRecord> ApplyPermanentModification(BodyPartId bodyPartId, string kind, string description)
        {
            if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(description))
            {
                Banner.Show(OdyBannerKind.Error, "A modification needs a kind and a description.");
                return Result<CharacterRecord>.Failure(UiGuard.InvalidRequest());
            }

            return Mutate("Permanent modification", c => _characters.ApplyPermanentModification(_context.Campaign, c.CharacterId, bodyPartId, kind.Trim(), description.Trim(), _context.ActorUserId, c.Revisions.CharacterAnatomyRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()), "Modification applied.");
        }

        // ---- Ownership (MainGM) -------------------------------------------------------------------

        public Result<CharacterRecord> AssignPrimaryOwner(UserId user, string reason) =>
            Mutate("Assign owner", c => _characters.AssignPrimaryOwner(_context.Campaign, c.CharacterId, user, (reason ?? string.Empty).Trim(), _context.ActorUserId, c.Revisions.OwnershipRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()), "Primary owner assigned.");

        public Result<CharacterRecord> AddCoOwner(UserId user) =>
            Mutate("Add co-owner", c => _characters.AddCharacterCoOwner(_context.Campaign, c.CharacterId, user, _context.ActorUserId, c.Revisions.OwnershipRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()), "Co-owner added.");

        public Result<CharacterRecord> RemoveCoOwner(UserId user) =>
            Mutate("Remove co-owner", c => _characters.RemoveCharacterCoOwner(_context.Campaign, c.CharacterId, user, _context.ActorUserId, c.Revisions.OwnershipRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()), "Co-owner removed.");

        public Result<CharacterRecord> GrantPermanentControl(UserId user) =>
            Mutate("Grant control", c => _characters.GrantPermanentCharacterControl(_context.Campaign, c.CharacterId, user, _context.ActorUserId, c.Revisions.OwnershipRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()), "Permanent control granted.");

        /// <summary><paramref name="hours"/> &lt;= 0 means no expiry.</summary>
        public Result<CharacterRecord> GrantTemporaryControl(UserId user, int hours)
        {
            UtcInstant? expiresAt = hours > 0 ? _context.Clock.GetUtcNow().Add(TimeSpan.FromHours(hours)) : (UtcInstant?)null;
            return Mutate("Grant temporary control", c => _characters.GrantTemporaryCharacterControl(_context.Campaign, c.CharacterId, user, expiresAt, _context.ActorUserId, c.Revisions.OwnershipRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()), "Temporary control granted.");
        }

        public Result<CharacterRecord> RevokeControl(UserId user) =>
            Mutate("Revoke control", c => _characters.RevokeCharacterControl(_context.Campaign, c.CharacterId, user, _context.ActorUserId, c.Revisions.OwnershipRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()), "Control revoked.");

        // ---- History ------------------------------------------------------------------------------

        public static bool IsRevertible(AdvancementPurchase purchase) =>
            purchase.Status == AdvancementPurchaseStatus.Applied
            && (purchase.OperationKind == AdvancementOperationKind.AttributeIncrease || purchase.OperationKind == AdvancementOperationKind.SkillLevelPurchase);

        public Result<CharacterRecord> RevertPurchase(AdvancementPurchaseId purchaseId, string reason) =>
            Mutate("Revert purchase", c => _characters.RevertAdvancementPurchase(_context.Campaign, c.CharacterId, purchaseId, (reason ?? string.Empty).Trim(), _context.ActorUserId, c.Revisions.MechanicsRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()), "Purchase reverted; points refunded.");

        public OdyConfirmDialog RequestRevert(AdvancementPurchaseId purchaseId) => OdyConfirmDialog.Show(_context.ModalHost,
            new OdyConfirmOptions("Revert this purchase?", "The value goes back and the points are refunded.", "Revert") { Destructive = true, RequiredTextLabel = "Reason" },
            reason => RevertPurchase(purchaseId, reason));

        public Result<CharacterRespecPreview> PreviewRespec(IReadOnlyList<CharacterRespecTarget> targets)
        {
            if (Current == null) return Result<CharacterRespecPreview>.Failure(UiGuard.InvalidRequest());
            if (targets == null || targets.Count == 0)
            {
                Banner.Show(OdyBannerKind.Error, "Change at least one value to preview a respec.");
                return Result<CharacterRespecPreview>.Failure(UiGuard.InvalidRequest());
            }

            CharacterId id = Current.CharacterId;
            Result<CharacterRespecPreview> preview = UiGuard.Run(() => CharacterAdvancementService.PreviewCharacterRespec(_characters, _context.Campaign, id, targets, UiCommandIds.NewCorrelationId()));
            if (preview.IsFailure)
            {
                ReportFailure("Respec preview", preview.Error);
                return preview;
            }

            LastRespecPreview = preview.Value;
            Banner.Show(OdyBannerKind.Info, "Respec plan: " + preview.Value.Entries.Count + " steps, returns " + preview.Value.TotalReturned + ", spends " + preview.Value.TotalSpent + ".");
            RenderSheet();
            return preview;
        }

        public Result<CharacterRecord> ApplyRespec(IReadOnlyList<CharacterRespecTarget> targets, string reason)
        {
            if (targets == null || targets.Count == 0) return Result<CharacterRecord>.Failure(UiGuard.InvalidRequest());
            Result<CharacterRecord> applied = Mutate("Respec", c => CharacterAdvancementService.ApplyCharacterRespec(_characters, _context.Campaign, c.CharacterId, targets, (reason ?? string.Empty).Trim(), _context.ActorUserId, c.Revisions.MechanicsRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()), "Respec applied.");
            if (applied.IsSuccess) LastRespecPreview = null;
            return applied;
        }

        public OdyConfirmDialog RequestApplyRespec(IReadOnlyList<CharacterRespecTarget> targets) => OdyConfirmDialog.Show(_context.ModalHost,
            new OdyConfirmOptions("Apply respec?", "Purchases are returned and re-bought according to the plan.", "Apply respec") { Destructive = true, RequiredTextLabel = "Reason" },
            reason => ApplyRespec(targets, reason));

        private void LoadHistory()
        {
            _ledger.Clear();
            _purchases.Clear();
            if (Current == null) return;
            CorrelationId correlationId = UiCommandIds.NewCorrelationId();
            Result<IReadOnlyList<DevelopmentTransactionRecord>> ledger = _characters.GetDevelopmentLedger(_context.Campaign, Current.CharacterId, correlationId);
            if (ledger.IsSuccess) _ledger.AddRange(ledger.Value);
            Result<IReadOnlyList<AdvancementPurchase>> purchases = _characters.GetAdvancementPurchases(_context.Campaign, Current.CharacterId, correlationId);
            if (purchases.IsSuccess) _purchases.AddRange(purchases.Value);
        }

        // ---- rendering ------------------------------------------------------------------------------

        private void RenderResourcesTab(VisualElement tab, CharacterRecord c)
        {
            if (!ActorIsMainGm) tab.Add(OdyUi.Text("Only the MainGM changes current values and maximums.", OdyClasses.FieldHint));
            if (c.Resources.Count == 0) tab.Add(OdyUi.EmptyState("No resources yet.", "character-resources-empty"));
            _resourceBars.Clear();
            foreach (CharacterResource resource in c.Resources)
            {
                string key = resource.ResourceDefinitionId.ToString();
                VisualElement card = OdyUi.Card(key, out VisualElement body, "character-resource-" + key);
                var bar = new OdyResourceBar("character-resource-bar-" + key);
                long maximum = EffectiveMaximum(resource);
                bar.SetValue(resource.CurrentValue, resource.MinimumValue, maximum);
                // ODY-S11-211: this panel's own change is shown at once; a value changed elsewhere (combat damage,
                // another participant, a reload after a conflict) eases from what was shown before.
                string shownKey = c.CharacterId + "/" + key;
                if (!_renderingOwnChange && _shownResourceFractions.TryGetValue(shownKey, out double shown)) bar.AnimateFrom(shown);
                _shownResourceFractions[shownKey] = bar.FillFraction;
                _resourceBars[key] = bar;
                body.Add(bar.Element);
                body.Add(OdyUi.Text("min " + resource.MinimumValue + " · base max " + resource.BaseMaximum + " · adj " + resource.PermanentMaximumAdjustment + " · recovery " + EnumChoices.Humanize(resource.RecoveryRule.ToString()), OdyClasses.TextCaption));
                if (ActorIsMainGm)
                {
                    CharacterResourceId id = resource.CharacterResourceId;
                    var row = new VisualElement();
                    row.AddToClassList(OdyClasses.FormRow);
                    IntegerField current = OdyUi.IntegerField("Current", EnumChoices.ClampToInt(resource.CurrentValue), "character-resource-current-" + key);
                    IntegerField baseMax = OdyUi.IntegerField("Base max", EnumChoices.ClampToInt(resource.BaseMaximum), "character-resource-max-" + key);
                    IntegerField adjust = OdyUi.IntegerField("Max adj", EnumChoices.ClampToInt(resource.PermanentMaximumAdjustment), "character-resource-adj-" + key);
                    row.Add(current);
                    row.Add(baseMax);
                    row.Add(adjust);
                    body.Add(row);
                    body.Add(OdyUi.ButtonRow(
                        OdyUi.Button("Set current", () => SetResourceCurrent(id, current.value), OdyButtonVariant.Secondary, "character-resource-set-current-" + key, small: true),
                        OdyUi.Button("Set maximum", () => SetResourceMaximum(id, baseMax.value, adjust.value), OdyButtonVariant.Secondary, "character-resource-set-max-" + key, small: true)));
                }

                tab.Add(card);
            }

            if (!ActorIsMainGm) return;
            VisualElement add = OdyUi.Section("Add a resource");
            TextField keyField = OdyUi.TextField("Resource key", "hp", "character-resource-key");
            add.Add(keyField);
            add.Add(OdyUi.Text("Starts with the ruleset defaults; adjust the maximum afterwards.", OdyClasses.FieldHint));
            add.Add(OdyUi.ButtonRow(OdyUi.Button("Add", () => InitializeResource(keyField.value), OdyButtonVariant.Primary, "character-resource-add", small: true)));
            OdyUi.SubmitOnEnter(keyField, () => InitializeResource(keyField.value));
            tab.Add(add);
        }

        private void RenderAnatomyTab(VisualElement tab, CharacterRecord c)
        {
            CharacterAnatomy? anatomy = c.Anatomy;
            if (anatomy == null)
            {
                tab.Add(OdyUi.EmptyState("The anatomy is not initialized yet.", "character-anatomy-empty"));
                if (!ActorIsMainGm)
                {
                    tab.Add(OdyUi.Text("Only the MainGM initializes and changes anatomy.", OdyClasses.FieldHint));
                    return;
                }

                TextField profile = OdyUi.TextField("Anatomy profile id", c.AnatomyProfileRef ?? DefaultAnatomyProfile, "character-anatomy-profile");
                tab.Add(profile);
                tab.Add(OdyUi.ButtonRow(OdyUi.Button("Initialize default anatomy", () => InitializeDefaultAnatomy(profile.value), OdyButtonVariant.Primary, "character-anatomy-init", small: true)));
                return;
            }

            tab.Add(OdyUi.Text("Profile " + anatomy.AnatomyProfileDefinitionId + " v" + anatomy.AnatomyProfileVersion, OdyClasses.TextCaption));
            var list = new VisualElement { name = "character-body-parts" };
            list.AddToClassList(OdyClasses.List);
            foreach (BodyPart part in anatomy.BodyParts)
            {
                VisualElement row = ListRow(part.Name + " (" + part.BodyPartId + ")", "damage limit " + part.DamageLimit + (part.AttachedToBodyPartId.HasValue ? " · attached to " + part.AttachedToBodyPartId.Value : string.Empty));
                if (ActorIsMainGm)
                {
                    BodyPartId id = part.BodyPartId;
                    row.Add(OdyUi.Button("Remove", () => OdyConfirmDialog.Show(_context.ModalHost, new OdyConfirmOptions("Remove " + id + "?", "Parts attached to it or equipment worn on it block the removal.", "Remove") { Destructive = true }, _ => RemoveBodyPart(id)), OdyButtonVariant.Ghost, "character-body-part-remove-" + id, small: true));
                }

                list.Add(row);
            }

            tab.Add(list);

            VisualElement modifications = OdyUi.Section("Permanent modifications", "character-modifications");
            if (anatomy.PermanentModifications.Count == 0) modifications.Add(OdyUi.EmptyState("None."));
            foreach (PermanentModification modification in anatomy.PermanentModifications)
            {
                modifications.Add(ListRow(modification.Kind + " on " + modification.AttachedToBodyPartId, modification.Description));
            }

            tab.Add(modifications);
            if (!ActorIsMainGm)
            {
                tab.Add(OdyUi.Text("Only the MainGM changes anatomy.", OdyClasses.FieldHint));
                return;
            }

            var partIds = new List<string> { "(none)" };
            foreach (BodyPart part in anatomy.BodyParts) partIds.Add(part.BodyPartId.ToString());

            VisualElement add = OdyUi.Section("Add a body part");
            var form = new VisualElement();
            form.AddToClassList(OdyClasses.FormRow);
            TextField id2 = OdyUi.TextField("Id", "tail", "character-body-part-id");
            TextField name = OdyUi.TextField("Name", "Tail", "character-body-part-name");
            IntegerField limit = OdyUi.IntegerField("Damage limit", 5, "character-body-part-limit");
            DropdownField attached = OdyUi.Dropdown("Attached to", partIds, 0, "character-body-part-attached");
            form.Add(id2);
            form.Add(name);
            form.Add(limit);
            form.Add(attached);
            add.Add(form);
            add.Add(OdyUi.ButtonRow(OdyUi.Button("Add", () => AddBodyPart(id2.value, name.value, limit.value, attached.index <= 0 ? null : attached.value), OdyButtonVariant.Primary, "character-body-part-add", small: true)));
            tab.Add(add);

            if (anatomy.BodyParts.Count == 0) return;
            var targets = new List<string>();
            foreach (BodyPart part in anatomy.BodyParts) targets.Add(part.BodyPartId.ToString());
            VisualElement modify = OdyUi.Section("Apply a permanent modification");
            var modForm = new VisualElement();
            modForm.AddToClassList(OdyClasses.FormRow);
            DropdownField target = OdyUi.Dropdown("Body part", targets, 0, "character-modification-part");
            TextField kind = OdyUi.TextField("Kind", "Scar", "character-modification-kind");
            TextField description = OdyUi.TextField("Description", string.Empty, "character-modification-description");
            modForm.Add(target);
            modForm.Add(kind);
            modForm.Add(description);
            modify.Add(modForm);
            modify.Add(OdyUi.ButtonRow(OdyUi.Button("Apply", () => { if (BodyPartId.TryParse(target.value, out BodyPartId part)) ApplyPermanentModification(part, kind.value, description.value); }, OdyButtonVariant.Primary, "character-modification-apply", small: true)));
            tab.Add(modify);
        }

        private void RenderOwnershipTab(VisualElement tab, CharacterRecord c)
        {
            CharacterOwnership ownership = c.Ownership;
            VisualElement current = OdyUi.Section("Current");
            current.Add(OdyUi.KeyValue("Primary owner", ownership.PrimaryOwnerUserId.HasValue ? UserLabel(ownership.PrimaryOwnerUserId.Value) : "none", out _, "character-primary-owner"));
            current.Add(OdyUi.KeyValue("Co-owners", JoinUsers(ownership.CoOwnerUserIds), out _, "character-co-owners"));
            current.Add(OdyUi.KeyValue("Permanent control", JoinUsers(ownership.PermanentControllerUserIds), out _, "character-controllers"));
            var temporary = new List<string>();
            foreach (CharacterTemporaryControlGrant grant in ownership.TemporaryControlGrants)
            {
                temporary.Add(UserLabel(grant.UserId) + (grant.ExpiresAt.HasValue ? " until " + grant.ExpiresAt.Value.Value.ToString("u") : " (no expiry)"));
            }

            current.Add(OdyUi.KeyValue("Temporary control", temporary.Count == 0 ? "none" : string.Join("; ", temporary), out _, "character-temporary-control"));
            tab.Add(current);

            if (!ActorIsMainGm)
            {
                var notice = new OdyBanner("character-ownership-notice");
                notice.Show(OdyBannerKind.Info, "Only the MainGM assigns owners and grants or revokes control.");
                tab.Add(notice.Element);
                return;
            }

            VisualElement manage = OdyUi.Section("Manage");
            DropdownField user = BuildUserDropdown("User", "character-ownership-user", _context.Selection.PlayerUserId);
            manage.Add(user);
            var row = new VisualElement();
            row.AddToClassList(OdyClasses.FormRow);
            TextField reason = OdyUi.TextField("Reason (owner change)", "Reassigned by MainGM", "character-ownership-reason");
            IntegerField hours = OdyUi.IntegerField("Hours (temporary, 0 = none)", 2, "character-ownership-hours");
            row.Add(reason);
            row.Add(hours);
            manage.Add(row);
            manage.Add(OdyUi.ButtonRow(
                OdyUi.Button("Make primary owner", () => WithUser(user.value, u => AssignPrimaryOwner(u, reason.value)), OdyButtonVariant.Primary, "character-assign-owner", small: true),
                OdyUi.Button("Add co-owner", () => WithUser(user.value, u => AddCoOwner(u)), OdyButtonVariant.Secondary, "character-add-co-owner", small: true),
                OdyUi.Button("Remove co-owner", () => WithUser(user.value, u => RemoveCoOwner(u)), OdyButtonVariant.Secondary, "character-remove-co-owner", small: true),
                OdyUi.Button("Grant control", () => WithUser(user.value, u => GrantPermanentControl(u)), OdyButtonVariant.Secondary, "character-grant-control", small: true),
                OdyUi.Button("Grant temporary", () => WithUser(user.value, u => GrantTemporaryControl(u, hours.value)), OdyButtonVariant.Secondary, "character-grant-temporary", small: true),
                OdyUi.Button("Revoke control", () => WithUser(user.value, u => RevokeControl(u)), OdyButtonVariant.Danger, "character-revoke-control", small: true)));
            tab.Add(manage);
        }

        private void RenderHistoryTab(VisualElement tab, CharacterRecord c)
        {
            LoadHistory();

            VisualElement ledger = OdyUi.Section("Development ledger", "character-ledger");
            if (_ledger.Count == 0) ledger.Add(OdyUi.EmptyState("No transactions yet."));
            foreach (DevelopmentTransactionRecord entry in _ledger)
            {
                ledger.Add(ListRow(entry.Kind + " " + entry.Amount, entry.Reason + " · " + entry.CreatedAt.Value.ToString("u")));
            }

            tab.Add(ledger);

            VisualElement purchases = OdyUi.Section("Purchases", "character-purchases");
            if (_purchases.Count == 0) purchases.Add(OdyUi.EmptyState("No purchases yet."));
            foreach (AdvancementPurchase purchase in _purchases)
            {
                VisualElement row = ListRow(EnumChoices.Humanize(purchase.OperationKind.ToString()) + ": " + purchase.TargetDefinitionId + " " + purchase.FromValue + " → " + purchase.ToValue, "cost " + purchase.Cost + " · " + purchase.CreatedAt.Value.ToString("u"));
                row.Add(OdyUi.Badge(purchase.Status.ToString(), purchase.Status == AdvancementPurchaseStatus.Applied ? OdyStatusKind.Success : OdyStatusKind.Neutral));
                if (ActorIsMainGm && IsRevertible(purchase))
                {
                    AdvancementPurchaseId id = purchase.PurchaseId;
                    row.Add(OdyUi.Button("Revert", () => RequestRevert(id), OdyButtonVariant.Ghost, "character-purchase-revert-" + id, small: true));
                }

                purchases.Add(row);
            }

            tab.Add(purchases);
            tab.Add(OdyUi.Text("The MainGM can revert attribute and skill purchases (never ability purchases) and apply a respec.", OdyClasses.FieldHint));
            if (!ActorIsMainGm) return;

            VisualElement respec = OdyUi.Section("Respec", "character-respec");
            respec.Add(OdyUi.Text("Set the desired values, preview the plan, then apply it.", OdyClasses.FieldHint));
            var desired = new List<(AdvancementOperationKind Kind, string Key, long Current, IntegerField Field)>();
            foreach (AttributeValue attribute in c.Attributes)
            {
                IntegerField field = OdyUi.IntegerField(attribute.AttributeDefinitionId.ToString(), EnumChoices.ClampToInt(attribute.BaseValue), "character-respec-attribute-" + attribute.AttributeDefinitionId);
                desired.Add((AdvancementOperationKind.AttributeIncrease, attribute.AttributeDefinitionId.ToString(), attribute.BaseValue, field));
                respec.Add(field);
            }

            foreach (CharacterSkill skill in c.Skills)
            {
                IntegerField field = OdyUi.IntegerField(skill.SkillDefinitionId.ToString(), EnumChoices.ClampToInt(skill.Level), "character-respec-skill-" + skill.SkillDefinitionId);
                desired.Add((AdvancementOperationKind.SkillLevelPurchase, skill.SkillDefinitionId.ToString(), skill.Level, field));
                respec.Add(field);
            }

            List<CharacterRespecTarget> CollectTargets()
            {
                var targets = new List<CharacterRespecTarget>();
                foreach (var entry in desired)
                {
                    if (entry.Field.value != entry.Current && entry.Field.value >= 0) targets.Add(new CharacterRespecTarget(entry.Kind, entry.Key, entry.Field.value));
                }

                return targets;
            }

            if (LastRespecPreview != null)
            {
                VisualElement plan = OdyUi.Card("Preview", out VisualElement planBody, "character-respec-preview");
                foreach (CharacterRespecPlanEntry entry in LastRespecPreview.Entries)
                {
                    planBody.Add(OdyUi.Text(entry.Action + " " + EnumChoices.Humanize(entry.OperationKind.ToString()) + " " + entry.TargetDefinitionId + ": " + entry.Amount, OdyClasses.TextSmall));
                }

                planBody.Add(OdyUi.Text("Returns " + LastRespecPreview.TotalReturned + " · spends " + LastRespecPreview.TotalSpent, OdyClasses.TextStrong));
                respec.Add(plan);
            }

            respec.Add(OdyUi.ButtonRow(
                OdyUi.Button("Preview", () => PreviewRespec(CollectTargets()), OdyButtonVariant.Secondary, "character-respec-preview-button", small: true),
                OdyUi.Button("Apply respec", () => { List<CharacterRespecTarget> targets = CollectTargets(); if (targets.Count > 0) RequestApplyRespec(targets); }, OdyButtonVariant.Danger, "character-respec-apply", small: true)));
            tab.Add(respec);
        }

        private string JoinUsers(IReadOnlyList<UserId> users)
        {
            if (users.Count == 0) return "none";
            var labels = new List<string>();
            foreach (UserId user in users) labels.Add(UserLabel(user));
            return string.Join("; ", labels);
        }

        private void WithUser(string choice, Action<UserId> action)
        {
            UserId? user = UserFromChoice(choice);
            if (!user.HasValue)
            {
                Banner.Show(OdyBannerKind.Error, "Choose a campaign member.");
                return;
            }

            action(user.Value);
        }
    }
}
