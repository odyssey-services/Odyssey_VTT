using System;
using System.Collections.Generic;
using Odyssey.Application.CharacterAdvancement;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using UnityEngine.UIElements;
using RulesAbilityCostRules = Odyssey.Rules.Character.AbilityCostRules;
using RulesAttributeCostRules = Odyssey.Rules.Character.AttributeCostRules;
using RulesSkillCostRules = Odyssey.Rules.Character.SkillCostRules;

namespace Odyssey.Unity.Client
{
    /// <summary>Cost/cap preview of one purchase, computed by the same Rules functions the Application service uses.</summary>
    public readonly struct PurchasePreview
    {
        public PurchasePreview(long fromValue, long toValue, long cost, bool exceedsNormalCap, bool requiresRecommendation)
        {
            FromValue = fromValue;
            ToValue = toValue;
            Cost = cost;
            ExceedsNormalCap = exceedsNormalCap;
            RequiresRecommendation = requiresRecommendation;
        }

        public long FromValue { get; }
        public long ToValue { get; }
        public long Cost { get; }
        public bool ExceedsNormalCap { get; }
        public bool RequiresRecommendation { get; }
    }

    // ODY-S11-203: sheet tabs General (incl. review and lifecycle), Attributes, Skills, Abilities.
    public sealed partial class CharacterPanelPresenter
    {
        private readonly List<CharacterReviewCommentRecord> _comments = new List<CharacterReviewCommentRecord>();
        private readonly List<CriticalSuccessEvidenceRecord> _evidence = new List<CriticalSuccessEvidenceRecord>();
        private readonly List<AdvancementRecommendationRecord> _recommendations = new List<AdvancementRecommendationRecord>();
        private readonly HashSet<string> _sessionRecommendationIds = new HashSet<string>(StringComparer.Ordinal);

        public IReadOnlyList<CharacterReviewCommentRecord> ReviewComments => _comments;
        public IReadOnlyList<CriticalSuccessEvidenceRecord> Evidence => _evidence;

        /// <summary>
        /// Recommendations the client can find: those referenced by this character's critical-success evidence
        /// (<c>UsedByAdvancementId</c>) plus those requested in this session -- the backend has no recommendation list.
        /// </summary>
        public IReadOnlyList<AdvancementRecommendationRecord> Recommendations => _recommendations;

        // ---- General / review / lifecycle ---------------------------------------------------------

        public Result<CharacterRecord> Rename(string newName)
        {
            string name = (newName ?? string.Empty).Trim();
            return Mutate("Rename", c => _characters.UpdateIdentity(_context.Campaign, c.CharacterId, name, c.Revisions.IdentityRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()), "Renamed.");
        }

        public Result<CharacterRecord> SubmitForReview() =>
            Mutate("Submit for review", c => _characters.SubmitCharacterDraft(_context.Campaign, c.CharacterId, c.Revisions.LifecycleRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()), "Submitted for MainGM review.");

        public Result<CharacterReviewCommentRecord> AddReviewComment(string text)
        {
            if (Current == null) return Result<CharacterReviewCommentRecord>.Failure(UiGuard.InvalidRequest());
            if (string.IsNullOrWhiteSpace(text))
            {
                Banner.Show(OdyBannerKind.Error, "Write a comment first.");
                return Result<CharacterReviewCommentRecord>.Failure(UiGuard.InvalidRequest());
            }

            CharacterId id = Current.CharacterId;
            Result<CharacterReviewCommentRecord> added = UiGuard.Run(() => _characters.AddCharacterReviewComment(_context.Campaign, id, _context.ActorUserId, text.Trim(), UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()));
            if (added.IsFailure)
            {
                ReportFailure("Comment", added.Error);
                return added;
            }

            Banner.Show(OdyBannerKind.Success, "Comment added.");
            RenderSheet();
            return added;
        }

        public Result<CharacterRecord> Approve() =>
            Mutate("Approve", c => _characters.ApproveCharacterDraft(_context.Campaign, c.CharacterId, _context.ActorUserId, c.Revisions.LifecycleRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()), "Character approved.");

        public Result<CharacterRecord> GrantDevelopmentPoints(long amount, string reason) =>
            Mutate("Grant points", c => _characters.GrantDevelopmentPoints(_context.Campaign, c.CharacterId, amount, (reason ?? string.Empty).Trim(), _context.ActorUserId, c.Revisions.MechanicsRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()), "Granted " + amount + " development points.");

        public Result<CharacterRecord> Archive() =>
            Mutate("Archive", c => _characters.ArchiveCharacter(_context.Campaign, c.CharacterId, _context.ActorUserId, c.Revisions.LifecycleRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()), "Character archived.");

        public Result<CharacterRecord> MarkDead() =>
            Mutate("Mark dead", c => _characters.TransitionCharacterToDead(_context.Campaign, c.CharacterId, LifecycleDeathIssuerKind.GMOverride, _context.ActorUserId, c.Revisions.LifecycleRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()), "Character marked dead.");

        public Result<CharacterRecord> Restore(string reason) =>
            Mutate("Restore", c => _characters.RestoreDeadCharacter(
                new RestoreDeadCharacterRequest(_context.Campaign, c.CharacterId, CharacterLifecycleStatus.Active, (reason ?? string.Empty).Trim(), null, null, null, _context.ActorUserId, c.Revisions.LifecycleRevision, null, null),
                UiCommandIds.NewCommandId(),
                UiCommandIds.NewCorrelationId()), "Character restored.");

        public Result DeletePermanently(string reason)
        {
            if (Current == null) return Result.Failure(UiGuard.InvalidRequest());
            CharacterRecord basis = Current;
            Result deleted = UiGuard.Run(() => _characters.DeleteCharacterPermanently(_context.Campaign, basis.CharacterId, (reason ?? string.Empty).Trim(), _context.ActorUserId, basis.Revisions.LifecycleRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()));
            if (deleted.IsFailure)
            {
                ReportFailure("Delete", deleted.Error);
                return deleted;
            }

            _sessionCharacterIds.Remove(basis.CharacterId.ToString());
            SetCurrent(null);
            Refresh();
            Banner.Show(OdyBannerKind.Success, "\"" + basis.DisplayName + "\" was deleted permanently.");
            return deleted;
        }

        public OdyConfirmDialog? RequestArchive() => Current == null ? null : OdyConfirmDialog.Show(_context.ModalHost,
            new OdyConfirmOptions("Archive \"" + Current.DisplayName + "\"?", "An archived character leaves active play.", "Archive") { Destructive = true },
            _ => Archive());

        public OdyConfirmDialog? RequestMarkDead() => Current == null ? null : OdyConfirmDialog.Show(_context.ModalHost,
            new OdyConfirmOptions("Mark \"" + Current.DisplayName + "\" dead?", "The character becomes Dead (GM override). It can only come back through Restore.", "Mark dead") { Destructive = true },
            _ => MarkDead());

        public OdyConfirmDialog? RequestRestore() => Current == null ? null : OdyConfirmDialog.Show(_context.ModalHost,
            new OdyConfirmOptions("Restore \"" + Current.DisplayName + "\"?", "The character returns to Active.", "Restore") { RequiredTextLabel = "Reason" },
            reason => Restore(reason));

        public OdyConfirmDialog? RequestDelete() => Current == null ? null : OdyConfirmDialog.Show(_context.ModalHost,
            new OdyConfirmOptions("Delete \"" + Current.DisplayName + "\" permanently?", "This removes the character and cannot be undone.", "Delete permanently") { Destructive = true, RequiredTextLabel = "Reason" },
            reason => DeletePermanently(reason));

        // ---- Attributes ---------------------------------------------------------------------------

        public PurchasePreview PreviewAttribute(string attributeKey, long toValue)
        {
            long from = CurrentAttributeValue(attributeKey);
            bool increase = toValue > from;
            return new PurchasePreview(from, toValue, increase ? RulesAttributeCostRules.CostForIncrease(from, toValue) : 0, increase && RulesAttributeCostRules.ExceedsNormalCap(toValue), false);
        }

        public Result<CharacterRecord> PurchaseAttribute(string attributeKey, long toValue)
        {
            if (!AttributeDefinitionId.TryParse((attributeKey ?? string.Empty).Trim(), out AttributeDefinitionId attributeId))
            {
                Banner.Show(OdyBannerKind.Error, "\"" + attributeKey + "\" is not a valid attribute key (letter first, then letters, digits or _).");
                return Result<CharacterRecord>.Failure(UiGuard.InvalidRequest());
            }

            PurchasePreview preview = PreviewAttribute(attributeId.ToString(), toValue);
            if (toValue <= preview.FromValue)
            {
                Banner.Show(OdyBannerKind.Error, "The new value must be above the current value " + preview.FromValue + ".");
                return Result<CharacterRecord>.Failure(UiGuard.InvalidRequest());
            }

            return Mutate("Attribute purchase", c => CharacterAdvancementService.PurchaseAttributeIncrease(_characters, _context.Campaign, c.CharacterId, attributeId, toValue, _context.ActorUserId, c.Revisions.MechanicsRevision, AttributeRevision(c, attributeId), UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()),
                attributeId + " raised to " + toValue + " for " + preview.Cost + " points.");
        }

        // ---- Skills -------------------------------------------------------------------------------

        public PurchasePreview PreviewSkill(string skillKey, long toLevel)
        {
            long from = CurrentSkillLevel(skillKey);
            bool recommendation = RulesSkillCostRules.RequiresRecommendation(toLevel);
            long cost = toLevel > from ? RulesSkillCostRules.CostForIncrease(from, toLevel) : 0;
            return new PurchasePreview(from, toLevel, cost, false, recommendation);
        }

        public Result<CharacterRecord> PurchaseSkill(string skillKey, long toLevel)
        {
            if (!SkillDefinitionId.TryParse((skillKey ?? string.Empty).Trim(), out SkillDefinitionId skillId))
            {
                Banner.Show(OdyBannerKind.Error, "\"" + skillKey + "\" is not a valid skill key.");
                return Result<CharacterRecord>.Failure(UiGuard.InvalidRequest());
            }

            PurchasePreview preview = PreviewSkill(skillId.ToString(), toLevel);
            if (preview.RequiresRecommendation)
            {
                Banner.Show(OdyBannerKind.Info, "Level " + toLevel + " needs a MainGM-approved recommendation -- use \"Request recommendation\".");
                return Result<CharacterRecord>.Failure(UiGuard.InvalidRequest());
            }

            if (toLevel <= preview.FromValue)
            {
                Banner.Show(OdyBannerKind.Error, "The new level must be above the current level " + preview.FromValue + ".");
                return Result<CharacterRecord>.Failure(UiGuard.InvalidRequest());
            }

            return Mutate("Skill purchase", c => CharacterAdvancementService.PurchaseSkillLevel(_characters, _context.Campaign, c.CharacterId, skillId, toLevel, _context.ActorUserId, c.Revisions.MechanicsRevision, SkillRevision(c, skillId), UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()),
                skillId + " raised to level " + toLevel + " for " + preview.Cost + " points.");
        }

        public Result<AdvancementRecommendationRecord> RequestRecommendation(string skillKey, long targetLevel, IReadOnlyList<CriticalSuccessEvidenceId> evidenceIds)
        {
            if (Current == null) return Result<AdvancementRecommendationRecord>.Failure(UiGuard.InvalidRequest());
            if (!SkillDefinitionId.TryParse((skillKey ?? string.Empty).Trim(), out SkillDefinitionId skillId))
            {
                Banner.Show(OdyBannerKind.Error, "\"" + skillKey + "\" is not a valid skill key.");
                return Result<AdvancementRecommendationRecord>.Failure(UiGuard.InvalidRequest());
            }

            if (targetLevel <= CurrentSkillLevel(skillId.ToString()))
            {
                Banner.Show(OdyBannerKind.Error, "The target level must be above the current level.");
                return Result<AdvancementRecommendationRecord>.Failure(UiGuard.InvalidRequest());
            }

            CharacterRecord basis = Current;
            Result<AdvancementRecommendationRecord> requested = UiGuard.Run(() => CharacterAdvancementService.RequestSkillAdvancedRecommendation(_characters, _context.Campaign, basis.CharacterId, skillId, targetLevel, evidenceIds ?? Array.Empty<CriticalSuccessEvidenceId>(), _context.ActorUserId, basis.Revisions.MechanicsRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()));
            if (requested.IsFailure)
            {
                ReportFailure("Recommendation request", requested.Error);
                return requested;
            }

            _sessionRecommendationIds.Add(requested.Value.RecommendationId.ToString());
            Result<CharacterRecord> fresh = _characters.GetCharacter(_context.Campaign, basis.CharacterId, UiCommandIds.NewCorrelationId());
            if (fresh.IsSuccess) SetCurrent(fresh.Value);
            Banner.Show(OdyBannerKind.Success, "Recommendation requested; " + requested.Value.ReservedAmount + " points reserved until the MainGM decides.");
            return requested;
        }

        public Result<CharacterRecord> ResolveRecommendation(AdvancementRecommendationId recommendationId, bool approve)
        {
            AdvancementRecommendationRecord? recommendation = null;
            foreach (AdvancementRecommendationRecord candidate in _recommendations)
            {
                if (candidate.RecommendationId.Equals(recommendationId)) recommendation = candidate;
            }

            if (recommendation == null) return Result<CharacterRecord>.Failure(UiGuard.InvalidRequest());
            long recommendationRevision = recommendation.Revision;
            return Mutate(approve ? "Approve recommendation" : "Dismiss recommendation",
                c => _characters.ResolveAdvancementRecommendation(_context.Campaign, c.CharacterId, recommendationId, approve, approve, _context.ActorUserId, c.Revisions.MechanicsRevision, recommendationRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()),
                approve ? "Recommendation approved: the level is applied and the reserved points are spent." : "Recommendation dismissed: the reserved points are released.");
        }

        // ---- Abilities ----------------------------------------------------------------------------

        public long AbilityProgressionCost => RulesAbilityCostRules.CostForAcquisition();

        /// <summary>Acquire through <see cref="SourceKind.ProgressionPurchase"/> (owner/MainGM, costs points) or <see cref="SourceKind.GMGrant"/> (MainGM).</summary>
        public Result<CharacterRecord> AcquireAbility(string abilityKey, SourceKind sourceKind)
        {
            if (sourceKind != SourceKind.ProgressionPurchase && sourceKind != SourceKind.GMGrant)
            {
                Banner.Show(OdyBannerKind.Error, "Abilities from items, effects, templates or ruleset advancement are granted by those systems, not by hand.");
                return Result<CharacterRecord>.Failure(UiGuard.InvalidRequest());
            }

            if (!AbilityDefinitionId.TryParse((abilityKey ?? string.Empty).Trim(), out AbilityDefinitionId abilityId))
            {
                Banner.Show(OdyBannerKind.Error, "\"" + abilityKey + "\" is not a valid ability key.");
                return Result<CharacterRecord>.Failure(UiGuard.InvalidRequest());
            }

            return Mutate(sourceKind == SourceKind.GMGrant ? "Grant ability" : "Buy ability",
                c => CharacterAdvancementService.AcquireAbility(_characters, _context.Campaign, c.CharacterId, abilityId, sourceKind, null, RankMode.None, null, null, "{}", _context.ActorUserId,
                    sourceKind == SourceKind.ProgressionPurchase ? c.Revisions.MechanicsRevision : (long?)null, c.Revisions.CharacterAbilitiesRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()),
                abilityId + " acquired.");
        }

        /// <summary>ODY-S11-205 (MainGM): links a character ability to a published Ability definition so combat can activate it.</summary>
        public Result<CharacterRecord> LinkAbilityToDefinition(CharacterAbilityId characterAbilityId, ContentDefinitionRef abilityDefinition) =>
            Mutate("Link ability", c => _characters.LinkAbilityActivationSource(_context.Campaign, c.CharacterId, characterAbilityId, abilityDefinition, _context.ActorUserId, c.Revisions.CharacterAbilitiesRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()), "Ability linked -- it can now be activated in combat.");

        private List<ContentDefinitionRecord> PublishedAbilityDefinitions()
        {
            var result = new List<ContentDefinitionRecord>();
            if (_catalog == null) return result;
            Result<IReadOnlyList<ContentDefinitionRecord>> published = _catalog.ListContentDefinitions(_context.Campaign, Odyssey.Domain.Content.ContentDefinitionStatus.Published, UiCommandIds.NewCorrelationId());
            if (published.IsFailure) return result;
            foreach (ContentDefinitionRecord record in published.Value) if (record.DefinitionType == Odyssey.Domain.Content.ContentDefinitionType.Ability) result.Add(record);
            return result;
        }

        public static bool IsRemovable(CharacterAbility ability) => ability.SourceKind == SourceKind.Item || ability.SourceKind == SourceKind.ActiveEffect;

        public Result<CharacterRecord> RemoveAbility(CharacterAbilityId characterAbilityId) =>
            Mutate("Remove ability", c => _characters.RemoveAbility(_context.Campaign, c.CharacterId, characterAbilityId, _context.ActorUserId, c.Revisions.CharacterAbilitiesRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()), "Ability removed.");

        // ---- helpers -----------------------------------------------------------------------------

        private long CurrentAttributeValue(string key)
        {
            if (Current == null) return 0;
            foreach (AttributeValue value in Current.Attributes) if (value.AttributeDefinitionId.ToString() == key) return value.BaseValue;
            return 0;
        }

        private long CurrentSkillLevel(string key)
        {
            if (Current == null) return 0;
            foreach (CharacterSkill skill in Current.Skills) if (skill.SkillDefinitionId.ToString() == key) return skill.Level;
            return 0;
        }

        private static long AttributeRevision(CharacterRecord character, AttributeDefinitionId id)
        {
            foreach (AttributeValue value in character.Attributes) if (value.AttributeDefinitionId.Equals(id)) return value.Revision;
            return 0;
        }

        private static long SkillRevision(CharacterRecord character, SkillDefinitionId id)
        {
            foreach (CharacterSkill skill in character.Skills) if (skill.SkillDefinitionId.Equals(id)) return skill.Revision;
            return 0;
        }

        private void LoadSideData()
        {
            _comments.Clear();
            _evidence.Clear();
            _recommendations.Clear();
            if (Current == null) return;
            CorrelationId correlationId = UiCommandIds.NewCorrelationId();
            Result<IReadOnlyList<CharacterReviewCommentRecord>> comments = _characters.GetCharacterReviewComments(_context.Campaign, Current.CharacterId, correlationId);
            if (comments.IsSuccess) _comments.AddRange(comments.Value);
            Result<IReadOnlyList<CriticalSuccessEvidenceRecord>> evidence = _characters.GetCriticalSuccessEvidence(_context.Campaign, Current.CharacterId, correlationId);
            if (evidence.IsSuccess) _evidence.AddRange(evidence.Value);

            var recommendationIds = new HashSet<string>(_sessionRecommendationIds, StringComparer.Ordinal);
            foreach (CriticalSuccessEvidenceRecord record in _evidence)
            {
                if (record.UsedByAdvancementId.HasValue) recommendationIds.Add(record.UsedByAdvancementId.Value.ToString());
            }

            foreach (string text in recommendationIds)
            {
                if (!AdvancementRecommendationId.TryParse(text, out AdvancementRecommendationId id)) continue;
                Result<AdvancementRecommendationRecord> read = _characters.GetAdvancementRecommendation(_context.Campaign, Current.CharacterId, id, correlationId);
                if (read.IsSuccess) _recommendations.Add(read.Value);
            }
        }

        // ---- rendering ------------------------------------------------------------------------------

        private void RenderSheet()
        {
            if (_sheetHost == null) return;
            _sheetHost.Clear();
            _tabs = null;
            if (Current == null) return;
            LoadSideData();
            CharacterRecord c = Current;

            VisualElement header = new VisualElement { name = "character-sheet-header" };
            header.AddToClassList(OdyClasses.Card);
            var titleRow = new VisualElement();
            titleRow.AddToClassList(OdyClasses.Row);
            Label title = OdyUi.Text(c.DisplayName, OdyClasses.TextH1);
            title.name = "character-name";
            titleRow.Add(title);
            titleRow.Add(OdyUi.Badge(c.LifecycleStatus.ToString(), LifecycleKind(c.LifecycleStatus), "character-lifecycle-badge"));
            titleRow.Add(OdyUi.Badge(c.ApprovalState == CharacterApprovalState.Approved ? "Approved" : "Not approved", c.ApprovalState == CharacterApprovalState.Approved ? OdyStatusKind.Success : OdyStatusKind.Pending, "character-approval-badge"));
            header.Add(titleRow);
            header.Add(OdyUi.Text(EnumChoices.Humanize(c.CharacterKind.ToString()) + " · ruleset " + c.RulesetVersion + " · " + c.CharacterId, OdyClasses.TextCaption));
            if (!ActorCanManage) header.Add(OdyUi.Text("You can view this character; changes are for its owner or the MainGM.", OdyClasses.FieldHint));
            _sheetHost.Add(header);

            // ODY-S11-212: sub-tabs inside a screen are pills.
            _tabs = new OdyTabs("character-tabs", pill: true);
            RenderGeneralTab(_tabs.AddTab(GeneralTab, "General"), c);
            RenderAttributesTab(_tabs.AddTab(AttributesTab, "Attributes"), c);
            RenderSkillsTab(_tabs.AddTab(SkillsTab, "Skills"), c);
            RenderAbilitiesTab(_tabs.AddTab(AbilitiesTab, "Abilities"), c);
            RenderResourcesTab(_tabs.AddTab(ResourcesTab, "Resources"), c);
            RenderAnatomyTab(_tabs.AddTab(AnatomyTab, "Anatomy"), c);
            RenderOwnershipTab(_tabs.AddTab(OwnershipTab, "Ownership"), c);
            RenderHistoryTab(_tabs.AddTab(HistoryTab, "History"), c);
            _tabs.Select(_activeTab);
            _sheetHost.Add(_tabs.Element);
        }

        private void RenderGeneralTab(VisualElement tab, CharacterRecord c)
        {
            VisualElement overview = OdyUi.Section("Overview");
            overview.Add(OdyUi.KeyValue("Type", EnumChoices.Humanize(c.CharacterKind.ToString()), out _));
            overview.Add(OdyUi.KeyValue("Lifecycle", c.LifecycleStatus.ToString(), out _, "character-lifecycle"));
            overview.Add(OdyUi.KeyValue("Approval", c.ApprovalState.ToString(), out _, "character-approval"));
            overview.Add(OdyUi.KeyValue("Ruleset version", c.RulesetVersion, out _));
            overview.Add(OdyUi.KeyValue("Anatomy profile", c.AnatomyProfileRef ?? "--", out _));
            overview.Add(OdyUi.KeyValue("Portrait", c.PortraitAssetId?.ToString() ?? c.PortraitReference ?? "none", out _));
            DevelopmentPool pool = c.DevelopmentPool;
            overview.Add(OdyUi.KeyValue("Development points", pool.Available + " available (earned " + pool.Earned + ", spent " + pool.Spent + ", reserved " + pool.Reserved + ")", out _, "character-points"));
            tab.Add(overview);

            if (ActorCanManage)
            {
                TextField rename = OdyUi.TextField("Name", c.DisplayName, "character-rename");
                tab.Add(rename);
                tab.Add(OdyUi.ButtonRow(OdyUi.Button("Rename", () => Rename(rename.value), OdyButtonVariant.Secondary, "character-rename-button", small: true)));
            }

            // Review cycle: submit (owner), append-only comment feed, approve (MainGM).
            VisualElement review = OdyUi.Section("Review", "character-review");
            string state = c.ApprovalState == CharacterApprovalState.Approved ? "Approved by the MainGM."
                : c.SubmittedAt.HasValue ? "Submitted -- waiting for the MainGM. New comments mean changes were asked for."
                : "Draft -- not submitted yet.";
            review.Add(OdyUi.Text(state, OdyClasses.TextWrap));
            var feed = new VisualElement { name = "character-review-comments" };
            if (_comments.Count == 0) feed.Add(OdyUi.EmptyState("No comments yet."));
            foreach (CharacterReviewCommentRecord comment in _comments)
            {
                VisualElement item = OdyUi.Card(null, out VisualElement body);
                item.AddToClassList(OdyClasses.CardFlat);
                body.Add(OdyUi.Text(UserLabel(comment.AuthorUserId) + " · " + comment.CreatedAt.Value.ToString("u"), OdyClasses.TextCaption));
                body.Add(OdyUi.Text(comment.Text, OdyClasses.TextWrap));
                feed.Add(item);
            }

            review.Add(feed);
            if (ActorCanManage)
            {
                TextField commentField = OdyUi.TextField("Comment", string.Empty, "character-review-comment", multiline: true);
                review.Add(commentField);
                var buttons = OdyUi.ButtonRow(OdyUi.Button("Add comment", () => AddReviewComment(commentField.value), OdyButtonVariant.Secondary, "character-review-add", small: true));
                bool isDraft = c.LifecycleStatus == CharacterLifecycleStatus.Draft;
                if (isDraft && !c.SubmittedAt.HasValue && ActorIsAssigned) buttons.Add(OdyUi.Button("Submit for review", () => SubmitForReview(), OdyButtonVariant.Primary, "character-review-submit", small: true));
                if (isDraft && ActorIsMainGm) buttons.Add(OdyUi.Button("Approve", () => Approve(), OdyButtonVariant.Primary, "character-review-approve", small: true));
                review.Add(buttons);
            }

            if (c.LifecycleStatus == CharacterLifecycleStatus.Draft && !ActorIsMainGm) review.Add(OdyUi.Text("Only the MainGM can approve a character.", OdyClasses.FieldHint));
            tab.Add(review);

            if (ActorIsMainGm)
            {
                VisualElement gm = OdyUi.Section("MainGM tools", "character-gm-tools");
                var row = new VisualElement();
                row.AddToClassList(OdyClasses.FormRow);
                IntegerField amount = OdyUi.IntegerField("Points", 5, "character-grant-amount");
                TextField reason = OdyUi.TextField("Reason", "Session reward", "character-grant-reason");
                row.Add(amount);
                row.Add(reason);
                gm.Add(row);
                gm.Add(OdyUi.ButtonRow(
                    OdyUi.Button("Grant points", () => GrantDevelopmentPoints(amount.value, reason.value), OdyButtonVariant.Secondary, "character-grant-button", small: true),
                    OdyUi.Button("Place token on scene", () => PlaceTokenOnScene(), OdyButtonVariant.Secondary, "character-place-token", small: true)));
                tab.Add(gm);
            }

            VisualElement lifecycle = OdyUi.Section("Lifecycle", "character-lifecycle-section");
            var lifecycleButtons = new VisualElement();
            lifecycleButtons.AddToClassList(OdyClasses.ButtonRow);
            lifecycleButtons.Add(OdyUi.Button("Export .odchar", () => Export(), OdyButtonVariant.Secondary, "character-export", small: true));
            // Offered only where the Domain's own transition table allows the edge (the backend checks it again).
            if (ActorCanManage && CharacterLifecycleTransitions.IsValidTransition(c.LifecycleStatus, CharacterLifecycleStatus.Archived)) lifecycleButtons.Add(OdyUi.Button("Archive", () => RequestArchive(), OdyButtonVariant.Danger, "character-archive", small: true));
            if (ActorIsMainGm && CharacterLifecycleTransitions.IsValidTransition(c.LifecycleStatus, CharacterLifecycleStatus.Dead)) lifecycleButtons.Add(OdyUi.Button("Mark dead", () => RequestMarkDead(), OdyButtonVariant.Danger, "character-mark-dead", small: true));
            if (ActorIsMainGm && c.LifecycleStatus == CharacterLifecycleStatus.Dead) lifecycleButtons.Add(OdyUi.Button("Restore", () => RequestRestore(), OdyButtonVariant.Primary, "character-restore", small: true));
            if (ActorIsMainGm) lifecycleButtons.Add(OdyUi.Button("Delete permanently", () => RequestDelete(), OdyButtonVariant.Danger, "character-delete", small: true));
            lifecycle.Add(lifecycleButtons);
            if (!ActorIsMainGm) lifecycle.Add(OdyUi.Text("Death, restore and permanent deletion are MainGM decisions.", OdyClasses.FieldHint));
            tab.Add(lifecycle);
        }

        private void RenderAttributesTab(VisualElement tab, CharacterRecord c)
        {
            tab.Add(OdyUi.Text("Costs and the normal cap come from the ruleset rules (" + RulesAttributeCostRules.CostPerAttributePoint + " points per step, cap " + RulesAttributeCostRules.NormalDevelopmentCap + ").", OdyClasses.FieldHint));
            var list = new VisualElement { name = "character-attributes" };
            list.AddToClassList(OdyClasses.List);
            if (c.Attributes.Count == 0) list.Add(OdyUi.EmptyState("No attributes yet."));
            foreach (AttributeValue attribute in c.Attributes)
            {
                string key = attribute.AttributeDefinitionId.ToString();
                long next = attribute.BaseValue + 1;
                PurchasePreview preview = PreviewAttribute(key, next);
                var row = ListRow(key, "base " + attribute.BaseValue + (attribute.PermanentAdjustment != 0 ? " · adj " + attribute.PermanentAdjustment : string.Empty) + " · spent " + attribute.SpentDevelopmentPoints);
                row.Add(OdyUi.Badge("= " + (attribute.BaseValue + attribute.PermanentAdjustment), OdyStatusKind.Neutral));
                if (ActorCanManage)
                {
                    Button buy = OdyUi.Button("+1 (" + preview.Cost + " pts)", () => PurchaseAttribute(key, next), OdyButtonVariant.Secondary, "character-attribute-buy-" + key, small: true);
                    if (preview.ExceedsNormalCap) buy.tooltip = "Above the normal cap " + RulesAttributeCostRules.NormalDevelopmentCap + ": needs an explicit rule, ability or MainGM override.";
                    row.Add(buy);
                }

                list.Add(row);
            }

            tab.Add(list);
            if (!ActorCanManage) return;
            VisualElement add = OdyUi.Section("Raise an attribute");
            var form = new VisualElement();
            form.AddToClassList(OdyClasses.FormRow);
            TextField key2 = OdyUi.TextField("Attribute key", "strength", "character-attribute-key");
            IntegerField to = OdyUi.IntegerField("To value", 1, "character-attribute-to");
            Label cost = OdyUi.Text(string.Empty, OdyClasses.FieldHint);
            cost.name = "character-attribute-preview";
            void UpdateCost()
            {
                PurchasePreview p = PreviewAttribute(key2.value.Trim(), to.value);
                cost.text = p.ToValue > p.FromValue
                    ? "From " + p.FromValue + " to " + p.ToValue + ": " + p.Cost + " points" + (p.ExceedsNormalCap ? " -- above the normal cap, needs an explicit rule, ability or MainGM override." : ".")
                    : "Enter a value above the current " + p.FromValue + ".";
            }

            key2.RegisterValueChangedCallback(_ => UpdateCost());
            to.RegisterValueChangedCallback(_ => UpdateCost());
            UpdateCost();
            form.Add(key2);
            form.Add(to);
            add.Add(form);
            add.Add(cost);
            add.Add(OdyUi.ButtonRow(OdyUi.Button("Buy", () => PurchaseAttribute(key2.value, to.value), OdyButtonVariant.Primary, "character-attribute-buy", small: true)));
            tab.Add(add);
        }

        private void RenderSkillsTab(VisualElement tab, CharacterRecord c)
        {
            tab.Add(OdyUi.Text("Up to level " + RulesSkillCostRules.MaxOrdinaryPurchaseLevel + " a level is bought (" + RulesSkillCostRules.CostPerSkillPoint + " points per level); higher levels need a recommendation approved by the MainGM.", OdyClasses.FieldHint));
            var list = new VisualElement { name = "character-skills" };
            list.AddToClassList(OdyClasses.List);
            if (c.Skills.Count == 0) list.Add(OdyUi.EmptyState("No skills yet."));
            foreach (CharacterSkill skill in c.Skills)
            {
                string key = skill.SkillDefinitionId.ToString();
                long next = skill.Level + 1;
                PurchasePreview preview = PreviewSkill(key, next);
                VisualElement row = ListRow(key, "level " + skill.Level + (skill.PermanentAdjustment != 0 ? " · adj " + skill.PermanentAdjustment : string.Empty) + " · spent " + skill.SpentDevelopmentPoints);
                if (ActorCanManage && !preview.RequiresRecommendation) row.Add(OdyUi.Button("+1 (" + preview.Cost + " pts)", () => PurchaseSkill(key, next), OdyButtonVariant.Secondary, "character-skill-buy-" + key, small: true));
                if (preview.RequiresRecommendation) row.Add(OdyUi.Badge("Next level needs recommendation", OdyStatusKind.Pending));
                list.Add(row);
            }

            tab.Add(list);

            if (ActorCanManage)
            {
                VisualElement buy = OdyUi.Section("Buy a skill level");
                var form = new VisualElement();
                form.AddToClassList(OdyClasses.FormRow);
                TextField key = OdyUi.TextField("Skill key", "athletics", "character-skill-key");
                IntegerField to = OdyUi.IntegerField("To level", 1, "character-skill-to");
                form.Add(key);
                form.Add(to);
                buy.Add(form);
                buy.Add(OdyUi.ButtonRow(OdyUi.Button("Buy", () => PurchaseSkill(key.value, to.value), OdyButtonVariant.Primary, "character-skill-buy", small: true)));
                tab.Add(buy);

                VisualElement request = OdyUi.Section("Request recommendation (level " + (RulesSkillCostRules.MaxOrdinaryPurchaseLevel + 1) + "+)", "character-recommendation-request");
                var requestForm = new VisualElement();
                requestForm.AddToClassList(OdyClasses.FormRow);
                TextField skillKey = OdyUi.TextField("Skill key", "athletics", "character-recommendation-skill");
                IntegerField target = OdyUi.IntegerField("Target level", (int)(RulesSkillCostRules.MaxOrdinaryPurchaseLevel + 1), "character-recommendation-level");
                requestForm.Add(skillKey);
                requestForm.Add(target);
                request.Add(requestForm);
                request.Add(OdyUi.Text("Critical-success evidence to cite:", OdyClasses.TextLabel));
                var chosen = new List<CriticalSuccessEvidenceId>();
                int available = 0;
                foreach (CriticalSuccessEvidenceRecord record in _evidence)
                {
                    if (record.UsedByAdvancementId.HasValue) continue;
                    available++;
                    CriticalSuccessEvidenceId id = record.EvidenceId;
                    Toggle toggle = OdyUi.Toggle(record.SkillDefinitionId + " · " + record.OccurredAt.Value.ToString("u"), false, "character-evidence-" + id);
                    toggle.RegisterValueChangedCallback(evt => { if (evt.newValue) chosen.Add(id); else chosen.Remove(id); });
                    request.Add(toggle);
                }

                if (available == 0) request.Add(OdyUi.Text("No unused critical-success evidence yet (recorded by natural-maximum checks).", OdyClasses.FieldHint));
                request.Add(OdyUi.ButtonRow(OdyUi.Button("Request", () => RequestRecommendation(skillKey.value, target.value, chosen), OdyButtonVariant.Primary, "character-recommendation-submit", small: true)));
                tab.Add(request);
            }

            VisualElement pending = OdyUi.Section("Recommendations", "character-recommendations");
            if (_recommendations.Count == 0) pending.Add(OdyUi.EmptyState("No recommendations found."));
            foreach (AdvancementRecommendationRecord recommendation in _recommendations)
            {
                VisualElement row = ListRow(recommendation.SkillDefinitionId + " → level " + recommendation.TargetLevel, recommendation.ReservedAmount + " points reserved · " + recommendation.CreatedAt.Value.ToString("u"));
                bool isPending = recommendation.Status == AdvancementRecommendationStatus.Pending;
                row.Add(OdyUi.Badge(isPending ? "Waiting for MainGM" : recommendation.Status.ToString(), isPending ? OdyStatusKind.Pending : recommendation.Status == AdvancementRecommendationStatus.Approved ? OdyStatusKind.Success : OdyStatusKind.Neutral));
                if (isPending && ActorIsMainGm)
                {
                    AdvancementRecommendationId id = recommendation.RecommendationId;
                    row.Add(OdyUi.Button("Approve", () => ResolveRecommendation(id, true), OdyButtonVariant.Primary, "character-recommendation-approve-" + id, small: true));
                    row.Add(OdyUi.Button("Dismiss", () => ResolveRecommendation(id, false), OdyButtonVariant.Ghost, "character-recommendation-dismiss-" + id, small: true));
                }

                pending.Add(row);
            }

            tab.Add(pending);
        }

        private void RenderAbilitiesTab(VisualElement tab, CharacterRecord c)
        {
            var list = new VisualElement { name = "character-abilities" };
            list.AddToClassList(OdyClasses.List);
            if (c.Abilities.Count == 0) list.Add(OdyUi.EmptyState("No abilities yet."));
            foreach (CharacterAbility ability in c.Abilities)
            {
                string rank = ability.RankMode == RankMode.Numeric ? " · rank " + ability.NumericRank : ability.RankMode == RankMode.Named ? " · " + ability.NamedRankKey : string.Empty;
                VisualElement row = ListRow(ability.AbilityDefinitionId.ToString(), "from " + EnumChoices.Humanize(ability.SourceKind.ToString()) + rank + (ability.IsEnabled ? string.Empty : " · disabled"));
                if (ability.ActivationDefinitionRef.HasValue) row.Add(OdyUi.Badge("Activatable", OdyStatusKind.Success));
                else if (ActorIsMainGm && _catalog != null)
                {
                    List<ContentDefinitionRecord> definitions = PublishedAbilityDefinitions();
                    if (definitions.Count > 0)
                    {
                        var labels = new List<string>();
                        foreach (ContentDefinitionRecord definition in definitions) labels.Add(definition.Name + " v" + definition.Version);
                        DropdownField link = OdyUi.Dropdown("Link to", labels, 0, "character-ability-link-" + ability.CharacterAbilityId);
                        row.Add(link);
                        CharacterAbilityId linkId = ability.CharacterAbilityId;
                        row.Add(OdyUi.Button("Link", () =>
                        {
                            int index = link.index;
                            if (index >= 0 && index < definitions.Count) LinkAbilityToDefinition(linkId, new ContentDefinitionRef(definitions[index].ContentDefinitionId, definitions[index].Version));
                        }, OdyButtonVariant.Secondary, "character-ability-link-button-" + ability.CharacterAbilityId, small: true));
                    }
                }

                if (IsRemovable(ability) && ActorIsMainGm)
                {
                    CharacterAbilityId id = ability.CharacterAbilityId;
                    row.Add(OdyUi.Button("Remove", () => OdyConfirmDialog.Show(_context.ModalHost, new OdyConfirmOptions("Remove ability?", "The ability granted by its item/effect source is removed from the character.", "Remove") { Destructive = true }, _ => RemoveAbility(id)), OdyButtonVariant.Ghost, "character-ability-remove-" + id, small: true));
                }

                list.Add(row);
            }

            tab.Add(list);
            tab.Add(OdyUi.Text("Only abilities granted by an item or an active effect can be removed (by the MainGM); purchased, granted, template and ruleset abilities stay.", OdyClasses.FieldHint));
            if (!ActorCanManage) return;

            VisualElement acquire = OdyUi.Section("Acquire an ability");
            var form = new VisualElement();
            form.AddToClassList(OdyClasses.FormRow);
            TextField key = OdyUi.TextField("Ability key", "second_wind", "character-ability-key");
            var sources = new List<string> { SourceKind.ProgressionPurchase.ToString() };
            if (ActorIsMainGm) sources.Add(SourceKind.GMGrant.ToString());
            DropdownField source = OdyUi.Dropdown("How", sources, 0, "character-ability-source");
            form.Add(key);
            form.Add(source);
            acquire.Add(form);
            acquire.Add(OdyUi.Text("Progression purchase costs " + AbilityProgressionCost + " points. A GM grant is free and MainGM-only.", OdyClasses.FieldHint));
            acquire.Add(OdyUi.ButtonRow(OdyUi.Button("Acquire", () => AcquireAbility(key.value, EnumChoices.TryParse(source.value, out SourceKind kind) ? kind : SourceKind.ProgressionPurchase), OdyButtonVariant.Primary, "character-ability-acquire", small: true)));
            tab.Add(acquire);
        }

        private static VisualElement ListRow(string title, string meta)
        {
            var row = new VisualElement();
            row.AddToClassList(OdyClasses.ListItem);
            var main = new VisualElement();
            main.AddToClassList(OdyClasses.ListItemMain);
            main.Add(OdyUi.Text(title, OdyClasses.ListItemTitle));
            main.Add(OdyUi.Text(meta, OdyClasses.ListItemMeta));
            row.Add(main);
            return row;
        }
    }
}
