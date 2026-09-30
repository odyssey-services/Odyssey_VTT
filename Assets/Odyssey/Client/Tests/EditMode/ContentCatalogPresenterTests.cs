using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Application.Content;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Content;
using Odyssey.Persistence.Sqlite;
using Odyssey.Unity.Client;
using UnityEngine.UIElements;

namespace Odyssey.Tests.Unity.EditMode
{
    /// <summary>ODY-S11-202: content catalog UI for Item/Weapon/Armor/Ammo/Ability/Effect/Skill.</summary>
    public sealed class ContentCatalogPresenterTests
    {
        [Test]
        public void SupportedTypes_AreTheSevenTypedDefinitions_EachWithItsOwnFormSection()
        {
            Assert.That(ContentDefinitionFormModel.SupportedTypes, Is.EquivalentTo(new[]
            {
                ContentDefinitionType.Item, ContentDefinitionType.Weapon, ContentDefinitionType.Armor, ContentDefinitionType.Ammo,
                ContentDefinitionType.Ability, ContentDefinitionType.Effect, ContentDefinitionType.Skill,
            }));

            using GameTestHost host = GameTestHost.Create();
            using var catalog = NewPresenter(host, out VisualElement view);
            var expectedFields = new Dictionary<ContentDefinitionType, string>
            {
                { ContentDefinitionType.Item, "catalog-item-category" },
                { ContentDefinitionType.Weapon, "catalog-weapon-damage" },
                { ContentDefinitionType.Armor, "catalog-armor-slot" },
                { ContentDefinitionType.Ammo, "catalog-ammo-keys" },
                { ContentDefinitionType.Ability, "catalog-ability-trigger" },
                { ContentDefinitionType.Effect, "catalog-effect-duration" },
            };

            foreach (ContentDefinitionType type in ContentDefinitionFormModel.SupportedTypes)
            {
                Assert.That(catalog.StartNew(type), Is.True, type.ToString());
                Assert.That(view.Q<TextField>("catalog-name"), Is.Not.Null, type + " has the common envelope");
                if (expectedFields.TryGetValue(type, out string field)) Assert.That(view.Q<VisualElement>(field), Is.Not.Null, type + " has its own fields");
            }

            Assert.That(view.Q<VisualElement>("catalog-ability-target-rule"), Is.Null, "Skill has no type-specific section");
        }

        [Test]
        public void Item_FullLifecycle_CreateSavePublishNewVersionArchiveDelete()
        {
            using GameTestHost host = GameTestHost.Create();
            using var catalog = NewPresenter(host, out _);

            catalog.StartNew(ContentDefinitionType.Item);
            catalog.Form!.Name = "Rope";
            catalog.Form.IsStackable = true;
            catalog.Form.MaxStackSize = 5;
            catalog.Form.Weight = 2;
            Result<ContentDefinitionRecord> created = catalog.SaveDraft();
            Assert.That(created.IsSuccess, Is.True);
            Assert.That(catalog.Selected!.Status, Is.EqualTo(ContentDefinitionStatus.Draft));
            long firstRevision = catalog.Selected.Revision;

            catalog.Form!.Name = "Silk rope";
            Result<ContentDefinitionRecord> updated = catalog.SaveDraft();
            Assert.That(updated.IsSuccess, Is.True);
            Assert.That(catalog.Selected!.Revision, Is.GreaterThan(firstRevision), "the presenter keeps the server's new revision");
            Assert.That(catalog.Selected.Name, Is.EqualTo("Silk rope"));

            Result<ContentDefinitionRecord> published = catalog.PublishSelected();
            Assert.That(published.IsSuccess, Is.True, "a valid item publishes");
            Assert.That(catalog.Selected!.Status, Is.EqualTo(ContentDefinitionStatus.Published));
            Assert.That(catalog.IsFormEditable, Is.False, "published definitions are immutable");
            var publishedId = catalog.Selected.ContentDefinitionId;

            Result<ContentDefinitionRecord> next = catalog.CreateNextVersion();
            Assert.That(next.IsSuccess, Is.True);
            Assert.That(catalog.Selected!.Status, Is.EqualTo(ContentDefinitionStatus.Draft));
            Assert.That(catalog.Form!.IsStackable && catalog.Form.MaxStackSize == 5, Is.True, "the new draft copies the published properties");

            OdyConfirmDialog? deleteDialog = catalog.RequestDelete();
            Assert.That(deleteDialog, Is.Not.Null, "deleting asks for confirmation");
            Assert.That(host.OpenDialog, Is.Not.Null);
            Assert.That(deleteDialog!.Confirm(), Is.True);
            Assert.That(catalog.Selected, Is.Null);
            Assert.That(catalog.VisibleDefinitions.Any(d => d.Status == ContentDefinitionStatus.Draft), Is.False, "the draft is gone");

            Assert.That(catalog.Select(publishedId).IsSuccess, Is.True);
            OdyConfirmDialog? archiveDialog = catalog.RequestArchive();
            Assert.That(archiveDialog, Is.Not.Null, "archiving asks for confirmation");
            archiveDialog!.Confirm();
            Assert.That(catalog.Selected!.Status, Is.EqualTo(ContentDefinitionStatus.Archived));
            Assert.That(catalog.VisibleDefinitions.Any(d => d.ContentDefinitionId.Equals(publishedId) && d.Status == ContentDefinitionStatus.Archived), Is.True, "MainGM sees archived rows");
        }

        [Test]
        public void Weapon_AmmoKeysVisibleOnlyWithAmmo_RequiredWithoutAmmoExplainsWhyPublishFails()
        {
            using GameTestHost host = GameTestHost.Create();
            using var catalog = NewPresenter(host, out _);

            catalog.StartNew(ContentDefinitionType.Weapon);
            Assert.That(catalog.IsFieldVisible("weapon.ammoKeys"), Is.False, "AmmoRequirement=None hides the keys");
            catalog.Form!.Name = "Longbow";
            catalog.Form.AttackMode = WeaponAttackMode.Ranged;
            catalog.Form.AmmoRequirement = AmmoRequirement.Required;
            catalog.ApplyFormChange();
            Assert.That(catalog.IsFieldVisible("weapon.ammoKeys"), Is.True);

            Assert.That(catalog.SaveDraft().IsFailure, Is.True, "Required ammo without keys is a form error");
            catalog.Form!.CompatibleAmmoKeysText = "arrow";
            catalog.ApplyFormChange();
            Assert.That(catalog.AmmoHintVisible, Is.True, "the client warns before publishing that no Ammo shares the key");
            Assert.That(catalog.SaveDraft().IsSuccess, Is.True);

            Result<ContentDefinitionRecord> failed = catalog.PublishSelected();
            Assert.That(failed.IsFailure, Is.True);
            Assert.That(catalog.LastIssues.Any(issue => issue.Contains("no Ammo definition")), Is.True, "the concrete backend issue is shown, not a generic error");
            Assert.That(catalog.Banner.Text, Does.Contain("Not published"));
            var weaponId = catalog.Selected!.ContentDefinitionId;

            catalog.StartNew(ContentDefinitionType.Ammo);
            catalog.Form!.Name = "Arrow";
            catalog.Form.AmmoCompatibilityKeysText = "arrow";
            Assert.That(catalog.SaveDraft().IsSuccess, Is.True);
            Assert.That(catalog.PublishSelected().IsSuccess, Is.True);

            catalog.Select(weaponId);
            catalog.ApplyFormChange();
            Assert.That(catalog.AmmoHintVisible, Is.False);
            Assert.That(catalog.PublishSelected().IsSuccess, Is.True, "with a compatible Ammo the weapon publishes");
        }

        [Test]
        public void Armor_ValidatesBodyPartIds_AndPublishes()
        {
            using GameTestHost host = GameTestHost.Create();
            using var catalog = NewPresenter(host, out _);

            catalog.StartNew(ContentDefinitionType.Armor);
            catalog.Form!.Name = "Helmet";
            catalog.Form.EquipmentSlotKey = "head";
            catalog.Form.CoveredBodyPartIdsText = "9head";
            Assert.That(catalog.Form.Validate().Any(error => error.Field == "armor.bodyParts"), Is.True, "an invalid body part id is a field error");
            Assert.That(catalog.SaveDraft().IsFailure, Is.True);

            catalog.Form.CoveredBodyPartIdsText = "Head, Neck";
            catalog.Form.Protection = 3;
            Assert.That(catalog.SaveDraft().IsSuccess, Is.True);
            Assert.That(catalog.PublishSelected().IsSuccess, Is.True);
            Assert.That(catalog.Form!.CoveredBodyPartIdsText, Is.EqualTo("Head, Neck"), "round-trips through the codec");
        }

        [Test]
        public void Ammo_RequiresAKey_AndReferencesPublishedEffects()
        {
            using GameTestHost host = GameTestHost.Create();
            using var catalog = NewPresenter(host, out _);

            ContentDefinitionRecord effect = PublishEffect(catalog, "Burning");

            catalog.StartNew(ContentDefinitionType.Ammo);
            catalog.Form!.Name = "Fire arrow";
            Assert.That(catalog.Form.Validate().Any(error => error.Field == "ammo.keys"), Is.True);
            catalog.Form.AmmoCompatibilityKeysText = "arrow";
            catalog.Form.DamageContribution = "1d4";
            catalog.Form.EffectContributionRefs.Add(new ContentDefinitionRef(effect.ContentDefinitionId, effect.Version));
            Assert.That(catalog.SaveDraft().IsSuccess, Is.True);
            Assert.That(catalog.PublishSelected().IsSuccess, Is.True, "a reference to a published effect validates");
            Assert.That(catalog.Form!.EffectContributionRefs, Has.Count.EqualTo(1));
        }

        [Test]
        public void Ability_ResourceCostsAndTargetRule_ValidateAndRoundTrip()
        {
            using GameTestHost host = GameTestHost.Create();
            using var catalog = NewPresenter(host, out VisualElement view);

            catalog.StartNew(ContentDefinitionType.Ability);
            Assert.That(view.Q<VisualElement>("catalog-ability-target-rule"), Is.Not.Null, "the shared target rule widget");
            catalog.Form!.Name = "Fireball";
            catalog.Form.Trigger = "on_cast";
            catalog.Form.ResourceCosts.Add(new ResourceCostModel("9mana", 2));
            catalog.Form.AbilityTargetRule.MinimumCount = 2;
            catalog.Form.AbilityTargetRule.MaximumCount = 1;
            IReadOnlyList<CatalogFieldError> errors = catalog.Form.Validate();
            Assert.That(errors.Any(e => e.Field.StartsWith("ability.cost.")), Is.True, "invalid resource id");
            Assert.That(errors.Any(e => e.Field == "ability.target.max"), Is.True, "max below min");

            catalog.Form.ResourceCosts[0].ResourceDefinitionId = "mana";
            catalog.Form.AbilityTargetRule.MaximumCount = 3;
            catalog.Form.AbilityTargetRule.Source = ContentTargetSource.AreaContents;
            catalog.Form.AbilityMechanicsPayloadRef = "   ";
            Assert.That(catalog.SaveDraft().IsSuccess, Is.True);
            Assert.That(catalog.PublishSelected().IsSuccess, Is.True, "a blank mechanics ref is stored as none, never as an empty string");

            ContentDefinitionFormModel reloaded = catalog.Form!;
            Assert.That(reloaded.ResourceCosts.Single().ResourceDefinitionId, Is.EqualTo("mana"));
            Assert.That(reloaded.AbilityTargetRule.Source, Is.EqualTo(ContentTargetSource.AreaContents));
            Assert.That(reloaded.AbilityTargetRule.MinimumCount, Is.EqualTo(2));
            Assert.That(reloaded.AbilityTargetRule.MaximumCount, Is.EqualTo(3));
        }

        [Test]
        public void Effect_DurationValueOnlyForCountedDurations_AndPublishes()
        {
            using GameTestHost host = GameTestHost.Create();
            using var catalog = NewPresenter(host, out _);

            catalog.StartNew(ContentDefinitionType.Effect);
            Assert.That(catalog.IsFieldVisible("effect.durationValue"), Is.False, "Instant needs no value");
            catalog.Form!.Name = "Stunned";
            catalog.Form.DurationType = EffectDurationType.ForRounds;
            catalog.ApplyFormChange();
            Assert.That(catalog.IsFieldVisible("effect.durationValue"), Is.True);
            catalog.Form.DurationValue = 0;
            Assert.That(catalog.Form.Validate().Any(e => e.Field == "effect.durationValue"), Is.True);
            catalog.Form.DurationValue = 3;
            catalog.Form.StackPolicy = EffectStackPolicy.RequestGMResolution;
            Assert.That(catalog.SaveDraft().IsSuccess, Is.True);
            Assert.That(catalog.PublishSelected().IsSuccess, Is.True);
            Assert.That(catalog.Form!.DurationValue, Is.EqualTo(3));
            Assert.That(catalog.Form.StackPolicy, Is.EqualTo(EffectStackPolicy.RequestGMResolution));

            catalog.CreateNextVersion();
            catalog.Form!.DurationType = EffectDurationType.UntilRemoved;
            catalog.ApplyFormChange();
            Assert.That(catalog.IsFieldVisible("effect.durationValue"), Is.False);
            Assert.That(catalog.SaveDraft().IsSuccess, Is.True, "a non-counted duration sends no value");
        }

        [Test]
        public void Skill_EnvelopeOnly_Publishes()
        {
            using GameTestHost host = GameTestHost.Create();
            using var catalog = NewPresenter(host, out _);

            catalog.StartNew(ContentDefinitionType.Skill);
            catalog.Form!.Name = "Stealth";
            catalog.Form.Description = "Move unseen.";
            catalog.Form.TagsText = "dex, sneaky";
            Assert.That(catalog.SaveDraft().IsSuccess, Is.True);
            Assert.That(catalog.Selected!.Tags, Is.EquivalentTo(new[] { "dex", "sneaky" }));
            Assert.That(catalog.PublishSelected().IsSuccess, Is.True);
        }

        [Test]
        public void NonMainGm_SeesExplanationAndPublishedOnly_CannotAuthor()
        {
            using GameTestHost host = GameTestHost.Create(BaselineRole.MainGM);
            using var catalog = NewPresenter(host, out VisualElement view);
            PublishEffect(catalog, "Blessed");
            catalog.StartNew(ContentDefinitionType.Skill);
            catalog.Form!.Name = "Draft skill";
            catalog.SaveDraft();

            host.Selection.SelectRole(BaselineRole.Player);
            Assert.That(catalog.CanAuthor, Is.False);
            Assert.That(view.Q<VisualElement>("catalog-role-notice").ClassListContains(OdyClasses.Hidden), Is.False, "the restriction is explained, not silently hidden");
            Assert.That(view.Q<Label>("catalog-role-notice-text").text, Does.Contain("Only the MainGM"));
            Assert.That(catalog.VisibleDefinitions.All(d => d.Status == ContentDefinitionStatus.Published), Is.True);
            Assert.That(catalog.StartNew(ContentDefinitionType.Item), Is.False);
            Assert.That(catalog.Banner.Text, Does.Contain("Only the MainGM"));

            catalog.Select(catalog.VisibleDefinitions[0].ContentDefinitionId);
            Assert.That(catalog.IsFormEditable, Is.False, "read-only for players");
            Assert.That(catalog.SaveDraft().IsFailure, Is.True);
        }

        [Test]
        public void Filters_NarrowByTypeAndStatus()
        {
            using GameTestHost host = GameTestHost.Create();
            using var catalog = NewPresenter(host, out _);
            PublishEffect(catalog, "Slowed");
            catalog.StartNew(ContentDefinitionType.Skill);
            catalog.Form!.Name = "Climb";
            catalog.SaveDraft();

            catalog.SetTypeFilter(ContentDefinitionType.Effect);
            Assert.That(catalog.VisibleDefinitions.All(d => d.DefinitionType == ContentDefinitionType.Effect), Is.True);
            catalog.SetTypeFilter(null);
            catalog.SetStatusFilter(ContentDefinitionStatus.Draft);
            Assert.That(catalog.VisibleDefinitions.Select(d => d.Name), Is.EqualTo(new[] { "Climb" }));
        }

        [Test]
        public void StaleRevision_IsRejected_AndThePresenterResyncsToTheServerState()
        {
            using GameTestHost host = GameTestHost.Create();
            var repository = new SqliteContentCatalogRepository(host.Clock);
            using var catalog = NewPresenter(host, repository, out _);
            catalog.StartNew(ContentDefinitionType.Skill);
            catalog.Form!.Name = "Swim";
            catalog.SaveDraft();
            ContentDefinitionRecord opened = catalog.Selected!;

            // Someone else saves the same draft first.
            var concurrent = new UpdateDraftDefinitionRequest(host.Campaign, opened.ContentDefinitionId, "Swim (edited elsewhere)", null, opened.PropertiesJson, opened.Revision, host.Selection.MainGmUserId, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId());
            Assert.That(ContentCatalogAuthoringService.UpdateDraftDefinition(repository, host.CampaignRepository, concurrent).IsSuccess, Is.True);

            catalog.Form!.Name = "Swim (mine)";
            Result<ContentDefinitionRecord> stale = catalog.SaveDraft();
            Assert.That(stale.IsFailure, Is.True);
            Assert.That(stale.Error.SafeReasonCode, Is.EqualTo(SafeReasonCode.StateChanged));
            Assert.That(catalog.Selected!.Revision, Is.GreaterThan(opened.Revision), "reloaded from the server, not kept stale");
            Assert.That(catalog.Selected.Name, Is.EqualTo("Swim (edited elsewhere)"));
        }

        [Test]
        public void TargetRuleEditor_EditsTheSharedModel_AndDescribesRules()
        {
            var model = new TargetRuleModel();
            int changes = 0;
            VisualElement editor = TargetRuleEditor.Build(model, "t", editable: true, () => changes++);
            Assert.That(editor.Q<DropdownField>("t-target-source"), Is.Not.Null);
            Assert.That(editor.Q<IntegerField>("t-target-min"), Is.Not.Null);
            Assert.That(editor.Q<IntegerField>("t-target-max"), Is.Not.Null);
            Assert.That(editor.Q<Toggle>("t-target-allow-self"), Is.Not.Null);

            Assert.That(TargetRuleEditor.Describe(new ContentTargetRule(ContentTargetSource.ManualSelection, 1, 2, true)), Is.EqualTo("Manual selection, 1-2 targets, self allowed"));
            Assert.That(EnumChoices.Humanize("ActiveAction"), Is.EqualTo("Active action"));
        }

        private static ContentCatalogPresenter NewPresenter(GameTestHost host, out VisualElement view) => NewPresenter(host, new SqliteContentCatalogRepository(host.Clock), out view);

        private static ContentCatalogPresenter NewPresenter(GameTestHost host, IContentCatalogRepository repository, out VisualElement view)
        {
            var presenter = new ContentCatalogPresenter(host.Context, repository);
            view = presenter.BuildView();
            Assert.That(presenter.Refresh().IsSuccess, Is.True);
            return presenter;
        }

        private static ContentDefinitionRecord PublishEffect(ContentCatalogPresenter catalog, string name)
        {
            catalog.StartNew(ContentDefinitionType.Effect);
            catalog.Form!.Name = name;
            Assert.That(catalog.SaveDraft().IsSuccess, Is.True);
            Result<ContentDefinitionRecord> published = catalog.PublishSelected();
            Assert.That(published.IsSuccess, Is.True);
            return catalog.Selected!;
        }
    }
}
