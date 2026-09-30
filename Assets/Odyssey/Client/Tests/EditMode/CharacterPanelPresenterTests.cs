using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Geometry;
using Odyssey.Domain.Identity;
using Odyssey.Persistence.Sqlite;
using Odyssey.Unity.Client;
using UnityEngine.UIElements;
using RulesAbilityCostRules = Odyssey.Rules.Character.AbilityCostRules;
using RulesAttributeCostRules = Odyssey.Rules.Character.AttributeCostRules;
using RulesSkillCostRules = Odyssey.Rules.Character.SkillCostRules;

namespace Odyssey.Tests.Unity.EditMode
{
    /// <summary>ODY-S11-203: character roster, creation, review, tabbed sheet and lifecycle.</summary>
    public sealed class CharacterPanelPresenterTests
    {
        [Test]
        public void CreatePlayerCharacter_RequiresOwner_AndRosterIsFilteredByRole()
        {
            using var fixture = Fixture.Create(BaselineRole.MainGM);
            CharacterPanelPresenter panel = fixture.Panel;

            Assert.That(panel.CreateCharacter(new CharacterCreateForm { DisplayName = "Aria", Kind = CharacterKind.PlayerCharacter, PrimaryOwnerUserId = null }).IsFailure, Is.True, "a PC needs a primary owner");
            Result<CharacterRecord> created = panel.CreateCharacter(new CharacterCreateForm { DisplayName = "Aria", Kind = CharacterKind.PlayerCharacter, PrimaryOwnerUserId = fixture.Host.Selection.PlayerUserId });
            Assert.That(created.IsSuccess, Is.True);
            Assert.That(created.Value.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Draft));
            Assert.That(panel.Current!.CharacterId, Is.EqualTo(created.Value.CharacterId));
            Assert.That(panel.Roster.Select(c => c.DisplayName), Does.Contain("Aria"));

            fixture.Host.Selection.SelectRole(BaselineRole.Observer);
            Assert.That(panel.Roster, Is.Empty, "the observer does not own or control Aria");
            Assert.That(panel.Current, Is.Null);

            fixture.Host.Selection.SelectRole(BaselineRole.Player);
            Assert.That(panel.Roster.Select(c => c.DisplayName), Does.Contain("Aria"), "the owner sees their character");
        }

        [Test]
        public void Roster_IncludesCharactersLinkedToSceneTokens()
        {
            using var fixture = Fixture.Create(BaselineRole.MainGM);
            Result<CharacterRecord> npc = fixture.Characters.BindDraftToCampaign(
                new BindDraftToCampaignRequest(fixture.Host.Campaign, CharacterKind.NonPlayerCharacter, "Goblin", "humanoid", null, CharacterCreationSeed.None(), null, null),
                UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId());
            Assert.That(npc.IsSuccess, Is.True);
            Assert.That(fixture.Scenes.CreateToken(fixture.Host.Campaign, fixture.Host.Demo.SceneId, new TokenPosition(1, 1), fixture.Host.Selection.MainGmUserId, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId(), npc.Value.CharacterId).IsSuccess, Is.True);

            using var fresh = new CharacterPanelPresenter(fixture.Host.Context, fixture.Characters, fixture.Scenes, fixture.Host.ExportDirectory);
            fresh.BuildView();
            Assert.That(fresh.Roster.Select(c => c.DisplayName), Does.Contain("Goblin"), "scene tokens are a real, persisted roster source");
        }

        [Test]
        public void ReviewCycle_SubmitCommentApprove()
        {
            using var fixture = Fixture.Create(BaselineRole.Player);
            CharacterPanelPresenter panel = fixture.Panel;
            Assert.That(panel.CreateCharacter(fixture.PlayerCharacter("Brin")).IsSuccess, Is.True);

            Assert.That(panel.SubmitForReview().IsSuccess, Is.True);
            Assert.That(panel.Current!.SubmittedAt.HasValue, Is.True);
            Assert.That(panel.AddReviewComment("Please check my backstory.").IsSuccess, Is.True);
            Assert.That(fixture.View.Q<Button>("character-review-approve"), Is.Null, "a player gets no approve button");
            Assert.That(panel.Approve().IsFailure, Is.True, "the backend refuses a non-MainGM approval");

            fixture.Host.Selection.SelectRole(BaselineRole.MainGM);
            panel.Open(panel.Roster.Single().CharacterId);
            Assert.That(panel.AddReviewComment("Looks good.").IsSuccess, Is.True);
            Assert.That(panel.ReviewComments.Select(c => c.Text), Is.EqualTo(new[] { "Please check my backstory.", "Looks good." }), "append-only feed in order");
            Assert.That(panel.Approve().IsSuccess, Is.True);
            Assert.That(panel.Current!.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Active));
            Assert.That(panel.Current.ApprovalState, Is.EqualTo(CharacterApprovalState.Approved));
        }

        [Test]
        public void Attributes_CostAndCapComeFromRules_PurchaseUpdatesPoolAndRevision()
        {
            using var fixture = Fixture.Create(BaselineRole.MainGM);
            CharacterPanelPresenter panel = fixture.Panel;
            panel.CreateCharacter(fixture.PlayerCharacter("Cyra"));
            Assert.That(panel.GrantDevelopmentPoints(30, "Start").IsSuccess, Is.True);
            long mechanicsBefore = panel.Current!.Revisions.MechanicsRevision;

            PurchasePreview preview = panel.PreviewAttribute("strength", 3);
            Assert.That(preview.Cost, Is.EqualTo(RulesAttributeCostRules.CostForIncrease(0, 3)), "the same Rules function the service uses");
            Assert.That(panel.PreviewAttribute("strength", RulesAttributeCostRules.NormalDevelopmentCap + 1).ExceedsNormalCap, Is.True);

            fixture.Host.Selection.SelectRole(BaselineRole.Player);
            Assert.That(panel.PurchaseAttribute("strength", 3).IsSuccess, Is.True, "the owner buys");
            Assert.That(panel.Current!.Attributes.Single(a => a.AttributeDefinitionId.ToString() == "strength").BaseValue, Is.EqualTo(3));
            Assert.That(panel.Current.DevelopmentPool.Spent, Is.EqualTo(preview.Cost));
            Assert.That(panel.Current.Revisions.MechanicsRevision, Is.GreaterThan(mechanicsBefore), "the next command uses the new revision");
            Assert.That(panel.PurchaseAttribute("strength", 2).IsFailure, Is.True, "not an increase");
            Assert.That(panel.PurchaseAttribute("9bad", 2).IsFailure, Is.True, "invalid key is caught before the service");
        }

        [Test]
        public void Skills_OrdinaryPurchase_ThenRecommendationRequestedAndApprovedByMainGm()
        {
            using var fixture = Fixture.Create(BaselineRole.MainGM);
            CharacterPanelPresenter panel = fixture.Panel;
            panel.CreateCharacter(fixture.PlayerCharacter("Dax"));
            panel.GrantDevelopmentPoints(60, "Start");
            fixture.Host.Selection.SelectRole(BaselineRole.Player);

            long maxOrdinary = RulesSkillCostRules.MaxOrdinaryPurchaseLevel;
            Assert.That(panel.PurchaseSkill("athletics", maxOrdinary).IsSuccess, Is.True);
            Assert.That(panel.PreviewSkill("athletics", maxOrdinary + 1).RequiresRecommendation, Is.True);
            Assert.That(panel.PurchaseSkill("athletics", maxOrdinary + 1).IsFailure, Is.True);
            Assert.That(panel.Banner.Text, Does.Contain("recommendation"));

            Result<AdvancementRecommendationRecord> requested = panel.RequestRecommendation("athletics", maxOrdinary + 1, new List<CriticalSuccessEvidenceId>());
            Assert.That(requested.IsSuccess, Is.True);
            Assert.That(panel.Current!.DevelopmentPool.Reserved, Is.EqualTo(requested.Value.ReservedAmount));
            Assert.That(panel.Recommendations.Single().Status, Is.EqualTo(AdvancementRecommendationStatus.Pending));
            Assert.That(fixture.View.Q<Button>("character-recommendation-approve-" + requested.Value.RecommendationId), Is.Null, "players only see the pending badge");

            fixture.Host.Selection.SelectRole(BaselineRole.MainGM);
            panel.Open(panel.Current.CharacterId);
            Assert.That(panel.ResolveRecommendation(requested.Value.RecommendationId, approve: true).IsSuccess, Is.True);
            Assert.That(panel.Current!.Skills.Single(s => s.SkillDefinitionId.ToString() == "athletics").Level, Is.EqualTo(maxOrdinary + 1));
        }

        [Test]
        public void Abilities_ProgressionPurchaseCostsPoints_GmGrantIsMainGmOnly()
        {
            using var fixture = Fixture.Create(BaselineRole.MainGM);
            CharacterPanelPresenter panel = fixture.Panel;
            panel.CreateCharacter(fixture.PlayerCharacter("Eli"));
            panel.GrantDevelopmentPoints(20, "Start");
            fixture.Host.Selection.SelectRole(BaselineRole.Player);

            Assert.That(panel.AcquireAbility("second_wind", SourceKind.ProgressionPurchase).IsSuccess, Is.True);
            Assert.That(panel.Current!.DevelopmentPool.Spent, Is.EqualTo(RulesAbilityCostRules.CostForAcquisition()));
            Assert.That(panel.AcquireAbility("blessing", SourceKind.GMGrant).IsFailure, Is.True, "players cannot grant");
            Assert.That(panel.AcquireAbility("x", SourceKind.Item).IsFailure, Is.True, "item abilities come from items, not the form");

            fixture.Host.Selection.SelectRole(BaselineRole.MainGM);
            panel.Open(panel.Current.CharacterId);
            Assert.That(panel.AcquireAbility("blessing", SourceKind.GMGrant).IsSuccess, Is.True);
            Assert.That(panel.Current!.Abilities, Has.Count.EqualTo(2));
            Assert.That(panel.Current.Abilities.Any(CharacterPanelPresenter.IsRemovable), Is.False, "purchased/granted abilities are not removable");
        }

        [Test]
        public void Resources_MainGmInitializesAndSets_BarReflectsValue_PlayerIsRefused()
        {
            using var fixture = Fixture.Create(BaselineRole.MainGM);
            CharacterPanelPresenter panel = fixture.Panel;
            panel.CreateCharacter(fixture.PlayerCharacter("Fen"));
            Assert.That(panel.InitializeResource("hp").IsSuccess, Is.True);
            CharacterResource hp = panel.Current!.Resources.Single();
            Assert.That(panel.SetResourceCurrent(hp.CharacterResourceId, 3).IsSuccess, Is.True);
            Assert.That(panel.Current!.Resources.Single().CurrentValue, Is.EqualTo(3));
            Assert.That(fixture.View.Q<VisualElement>("character-resource-bar-hp"), Is.Not.Null, "resources render as bars");

            fixture.Host.Selection.SelectRole(BaselineRole.Player);
            panel.Open(panel.Current.CharacterId);
            Assert.That(panel.SetResourceCurrent(hp.CharacterResourceId, 1).IsFailure, Is.True, "MainGM-only");
        }

        [Test]
        public void Anatomy_DependentRemovalIsExplained_AddRemoveAndModify()
        {
            using var fixture = Fixture.Create(BaselineRole.MainGM);
            CharacterPanelPresenter panel = fixture.Panel;
            panel.CreateCharacter(fixture.PlayerCharacter("Gale"));
            Assert.That(panel.InitializeDefaultAnatomy("humanoid").IsSuccess, Is.True);
            Assert.That(panel.Current!.Anatomy, Is.Not.Null);

            Assert.That(panel.RemoveBodyPart(BodyPartId.Parse("Torso")).IsFailure, Is.True, "arms are attached to the torso");
            Assert.That(panel.Banner.Text, Does.Contain("cannot be removed"));

            Assert.That(panel.AddBodyPart("Tail", "Tail", 5, "Torso").IsSuccess, Is.True);
            Assert.That(panel.Current!.Anatomy!.BodyParts.Any(p => p.BodyPartId.ToString() == "Tail"), Is.True);
            Assert.That(panel.RemoveBodyPart(BodyPartId.Parse("Tail")).IsSuccess, Is.True);
            Assert.That(panel.ApplyPermanentModification(BodyPartId.Parse("Head"), "Scar", "Old wound").IsSuccess, Is.True);
            Assert.That(panel.Current!.Anatomy!.PermanentModifications, Has.Count.EqualTo(1));
        }

        [Test]
        public void Ownership_MainGmManagesControl_PlayerSeesExplanation()
        {
            using var fixture = Fixture.Create(BaselineRole.MainGM);
            CharacterPanelPresenter panel = fixture.Panel;
            panel.CreateCharacter(fixture.PlayerCharacter("Hale"));
            UserId observer = fixture.Host.Selection.ObserverUserId;

            Assert.That(panel.AddCoOwner(observer).IsSuccess, Is.True);
            Assert.That(panel.Current!.Ownership.CoOwnerUserIds, Does.Contain(observer));
            Assert.That(panel.GrantTemporaryControl(fixture.Host.Selection.MainGmUserId, 2).IsSuccess, Is.True);
            Assert.That(panel.Current!.Ownership.TemporaryControlGrants.Any(g => g.ExpiresAt.HasValue), Is.True);

            fixture.Host.Selection.SelectRole(BaselineRole.Player);
            panel.Open(panel.Current.CharacterId);
            panel.Tabs!.Select(CharacterPanelPresenter.OwnershipTab);
            Assert.That(fixture.View.Q<VisualElement>("character-ownership-notice"), Is.Not.Null, "restriction is explained");
            Assert.That(fixture.View.Q<Button>("character-assign-owner"), Is.Null);
        }

        [Test]
        public void History_MainGmRevertsAttributePurchase_AndAppliesRespec()
        {
            using var fixture = Fixture.Create(BaselineRole.MainGM);
            CharacterPanelPresenter panel = fixture.Panel;
            panel.CreateCharacter(fixture.PlayerCharacter("Ivo"));
            panel.GrantDevelopmentPoints(40, "Start");
            panel.PurchaseAttribute("strength", 3);
            panel.PurchaseAttribute("agility", 2);
            panel.Refresh();

            AdvancementPurchase agility = panel.Purchases.Single(p => p.TargetDefinitionId == "agility");
            Assert.That(CharacterPanelPresenter.IsRevertible(agility), Is.True);
            OdyConfirmDialog revert = panel.RequestRevert(agility.PurchaseId);
            Assert.That(revert.Confirm(), Is.False, "a reason is required");
            revert.SetText("Mistake");
            Assert.That(revert.Confirm(), Is.True);
            Assert.That(panel.Current!.Attributes.FirstOrDefault(a => a.AttributeDefinitionId.ToString() == "agility")?.BaseValue ?? 0, Is.EqualTo(0), "reverted back");

            var targets = new List<CharacterRespecTarget> { new CharacterRespecTarget(AdvancementOperationKind.AttributeIncrease, "strength", 1) };
            Assert.That(panel.PreviewRespec(targets).IsSuccess, Is.True);
            Assert.That(panel.LastRespecPreview!.Entries, Is.Not.Empty);
            Assert.That(panel.ApplyRespec(targets, "Rebuild").IsSuccess, Is.True);
            Assert.That(panel.Current!.Attributes.Single(a => a.AttributeDefinitionId.ToString() == "strength").BaseValue, Is.EqualTo(1));
        }

        [Test]
        public void Lifecycle_ApproveDieRestoreArchive_AndConfirmedPermanentDelete()
        {
            using var fixture = Fixture.Create(BaselineRole.MainGM);
            CharacterPanelPresenter panel = fixture.Panel;
            panel.CreateCharacter(fixture.PlayerCharacter("Juno"));
            panel.Approve();

            panel.RequestMarkDead()!.Confirm();
            Assert.That(panel.Current!.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Dead));
            OdyConfirmDialog restore = panel.RequestRestore()!;
            Assert.That(restore.Confirm(), Is.False, "restore needs a reason");
            restore.SetText("Resurrection spell");
            restore.Confirm();
            Assert.That(panel.Current!.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Active));

            panel.RequestArchive()!.Confirm();
            Assert.That(panel.Current!.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Archived));

            CharacterId id = panel.Current.CharacterId;
            OdyConfirmDialog delete = panel.RequestDelete()!;
            Assert.That(fixture.Host.OpenDialog, Is.Not.Null, "irreversible delete asks for confirmation");
            delete.SetText("Duplicate");
            Assert.That(delete.Confirm(), Is.True);
            Assert.That(panel.Current, Is.Null);
            Assert.That(fixture.Characters.GetCharacter(fixture.Host.Campaign, id, UiCommandIds.NewCorrelationId()).IsFailure, Is.True, "physically deleted");
        }

        [Test]
        public void ExportThenImport_RoundTripsAnOdcharBundle()
        {
            using var fixture = Fixture.Create(BaselineRole.MainGM);
            CharacterPanelPresenter panel = fixture.Panel;
            panel.CreateCharacter(fixture.PlayerCharacter("Kestrel"));
            Assert.That(panel.Export().IsSuccess, Is.True);
            Assert.That(Directory.Exists(panel.LastExportPath!), Is.True);
            Assert.That(panel.LastExportPath, Does.EndWith(".odchar"));

            Result<CharacterRecord> imported = panel.Import(panel.LastExportPath!, fixture.Host.Selection.PlayerUserId);
            Assert.That(imported.IsSuccess, Is.True);
            Assert.That(imported.Value.DisplayName, Is.EqualTo("Kestrel"));
            Assert.That(panel.Roster.Count(c => c.DisplayName == "Kestrel"), Is.EqualTo(2));
        }

        [Test]
        public void StaleSectionRevision_IsRejected_AndTheSheetReloads()
        {
            using var fixture = Fixture.Create(BaselineRole.MainGM);
            CharacterPanelPresenter panel = fixture.Panel;
            panel.CreateCharacter(fixture.PlayerCharacter("Lark"));
            CharacterRecord opened = panel.Current!;
            Assert.That(fixture.Characters.UpdateIdentity(fixture.Host.Campaign, opened.CharacterId, "Lark (elsewhere)", opened.Revisions.IdentityRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()).IsSuccess, Is.True);

            Result<CharacterRecord> stale = panel.Rename("Lark (mine)");
            Assert.That(stale.IsFailure, Is.True);
            Assert.That(stale.Error.SafeReasonCode, Is.EqualTo(SafeReasonCode.StateChanged));
            Assert.That(panel.Current!.DisplayName, Is.EqualTo("Lark (elsewhere)"), "the sheet shows the server state after a conflict");
            Assert.That(panel.Rename("Lark (mine)").IsSuccess, Is.True, "the retry uses the fresh revision");
        }

        [Test]
        public void Sheet_HasAllEightTabs()
        {
            using var fixture = Fixture.Create(BaselineRole.MainGM);
            fixture.Panel.CreateCharacter(fixture.PlayerCharacter("Mira"));
            Assert.That(fixture.Panel.Tabs!.TabIds, Is.EqualTo(new[]
            {
                CharacterPanelPresenter.GeneralTab, CharacterPanelPresenter.AttributesTab, CharacterPanelPresenter.SkillsTab, CharacterPanelPresenter.AbilitiesTab,
                CharacterPanelPresenter.ResourcesTab, CharacterPanelPresenter.AnatomyTab, CharacterPanelPresenter.OwnershipTab, CharacterPanelPresenter.HistoryTab,
            }));
            fixture.Panel.Tabs.Select(CharacterPanelPresenter.SkillsTab);
            fixture.Panel.GrantDevelopmentPoints(1, "Tab keeps position");
            Assert.That(fixture.Panel.Tabs!.ActiveTabId, Is.EqualTo(CharacterPanelPresenter.SkillsTab), "re-render after a command keeps the open tab");
        }

        private sealed class Fixture : System.IDisposable
        {
            private Fixture(GameTestHost host)
            {
                Host = host;
                SqliteInventoryRepository inventory = host.NewInventoryRepository();
                Characters = host.NewCharacterRepository(inventory);
                Scenes = host.NewSceneRepository();
                Panel = new CharacterPanelPresenter(host.Context, Characters, Scenes, host.ExportDirectory);
                View = Panel.BuildView();
            }

            public GameTestHost Host { get; }
            public SqliteCharacterRepository Characters { get; }
            public SqliteSceneRepository Scenes { get; }
            public CharacterPanelPresenter Panel { get; }
            public VisualElement View { get; }

            public static Fixture Create(BaselineRole role) => new Fixture(GameTestHost.Create(role));

            public CharacterCreateForm PlayerCharacter(string name) => new CharacterCreateForm { DisplayName = name, Kind = CharacterKind.PlayerCharacter, PrimaryOwnerUserId = Host.Selection.PlayerUserId };

            public void Dispose()
            {
                Panel.Dispose();
                Host.Dispose();
            }
        }
    }
}
