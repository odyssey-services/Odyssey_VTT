using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Domain.Content;
using Odyssey.Domain.Character;
using Odyssey.Persistence.Sqlite;
using Odyssey.Unity.Client;
using UnityEngine.UIElements;

namespace Odyssey.Tests.Unity.EditMode
{
    /// <summary>
    /// ODY-S11-212 (polish P0 item 3): one tab convention. These tests check the classes and state. How the accent
    /// fill, the pill shape and the focus ring look (and that Tab really moves focus) is a manual check.
    /// </summary>
    public sealed class OdyTabConventionTests
    {
        [Test]
        public void TabBar_HasExactlyOneActiveTab_PillShape_FocusableButtons_AndNotifiesOncePerChange()
        {
            var bar = new OdyTabBar("filter", pill: true);
            bar.AddTab("a", "A");
            bar.AddTab("b", "B");
            var changes = new List<string>();
            bar.TabChanged += changes.Add;

            Assert.That(bar.Select("a"), Is.True);
            Assert.That(bar.Select("b"), Is.True);
            Assert.That(bar.Select("b"), Is.True, "selecting the active tab again is fine");
            Assert.That(bar.Select("missing"), Is.False);
            Assert.That(changes, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(bar.Select("a", notify: false), Is.True);
            Assert.That(changes, Has.Count.EqualTo(2), "silent selection for syncing from code");

            Button a = bar.ButtonFor("a")!;
            Button b = bar.ButtonFor("b")!;
            Assert.That(OdyUi.IsActive(a) && !OdyUi.IsActive(b), Is.True, "exactly one active");
            foreach (Button tab in new[] { a, b })
            {
                Assert.That(tab.ClassListContains(OdyClasses.Tab), Is.True);
                Assert.That(tab.ClassListContains(OdyClasses.TabPill), Is.True, "sub-tabs are pills");
                Assert.That(tab.focusable, Is.True, "reachable with the Tab key");
            }

            var topLevel = new OdyTabs("top");
            topLevel.AddTab("x", "X");
            Assert.That(topLevel.Bar.ButtonFor("x")!.ClassListContains(OdyClasses.TabPill), Is.False, "pill only where asked");
        }

        [Test]
        public void FocusRing_ShowsWhileNavigatingByKeyboard_AndHidesOnThePointer()
        {
            using var runtime = new PresentationRuntime();
            var root = new VisualElement();
            var shell = new GameShellPresenter(root, new RoleSelection(), runtime, "Scene A");
            shell.Build();
            OdyFocusVisible focus = shell.FocusVisible!;

            Assert.That(shell.Screen.ClassListContains(OdyClasses.FocusVisible), Is.False, "no rings by default");
            focus.NoteKeyboardNavigation();
            Assert.That(shell.Screen.ClassListContains(OdyClasses.FocusVisible), Is.True, "Tab/arrow navigation shows rings");
            focus.NotePointer();
            Assert.That(focus.IsKeyboardMode, Is.False, "a click hides them again");

            focus.NoteKeyboardNavigation();
            shell.Dispose();
            Assert.That(shell.Screen.ClassListContains(OdyClasses.FocusVisible), Is.False, "disposed with the shell");
        }

        [Test]
        public void SameActiveClass_OnTopbarToggles_CharacterSheetTabs_AndCatalogTypeFilter()
        {
            using var runtime = new PresentationRuntime();
            var root = new VisualElement();
            var shell = new GameShellPresenter(root, new RoleSelection(), runtime, "Scene A");
            shell.Build();
            shell.AddDrawer("combat", "Combat", GameDrawerSide.Right);
            Button toggle = root.Q<Button>("toggle-combat");
            Assert.That(toggle.ClassListContains(OdyClasses.Tab), Is.True, "panel toggles follow the tab convention");
            shell.OpenDrawer("combat");
            Assert.That(toggle.ClassListContains(OdyClasses.TabActive), Is.True);
            shell.CloseDrawer("combat");
            Assert.That(toggle.ClassListContains(OdyClasses.TabActive), Is.False);
            shell.Dispose();

            using GameTestHost host = GameTestHost.Create();
            SqliteInventoryRepository inventory = host.NewInventoryRepository();
            using var characters = new CharacterPanelPresenter(host.Context, host.NewCharacterRepository(inventory), host.NewSceneRepository(), host.ExportDirectory);
            characters.BuildView();
            characters.CreateCharacter(new CharacterCreateForm { DisplayName = "Tab", Kind = CharacterKind.PlayerCharacter, PrimaryOwnerUserId = host.Selection.PlayerUserId });
            OdyTabBar sheetBar = characters.Tabs!.Bar;
            Assert.That(sheetBar.IsPill, Is.True, "character sheet sub-tabs are pills");
            Assert.That(OdyUi.IsActive(sheetBar.ButtonFor(characters.Tabs.ActiveTabId!)!), Is.True);
            Assert.That(sheetBar.TabIds.Count(id => OdyUi.IsActive(sheetBar.ButtonFor(id)!)), Is.EqualTo(1));

            using var catalog = new ContentCatalogPresenter(host.Context, host.NewCatalogRepository(inventory));
            catalog.BuildView();
            OdyTabBar typeTabs = catalog.TypeTabs!;
            Assert.That(typeTabs.IsPill, Is.True);
            Assert.That(OdyUi.IsActive(typeTabs.ButtonFor(ContentCatalogPresenter.AllTypesTabId)!), Is.True, "all types by default");
        }

        [Test]
        public void CatalogTypeTabs_FilterTheList_AndFollowSetTypeFilter()
        {
            using GameTestHost host = GameTestHost.Create();
            SqliteInventoryRepository inventory = host.NewInventoryRepository();
            using var catalog = new ContentCatalogPresenter(host.Context, host.NewCatalogRepository(inventory));
            catalog.BuildView();
            catalog.StartNew(ContentDefinitionType.Skill);
            catalog.Form!.Name = "Climb";
            Assert.That(catalog.SaveDraft().IsSuccess, Is.True);

            OdyTabBar typeTabs = catalog.TypeTabs!;
            Assert.That(typeTabs.Select(ContentDefinitionType.Effect.ToString()), Is.True);
            Assert.That(catalog.TypeFilter, Is.EqualTo(ContentDefinitionType.Effect), "picking a tab filters");
            Assert.That(catalog.VisibleDefinitions, Is.Empty);

            catalog.SetTypeFilter(ContentDefinitionType.Skill);
            Assert.That(typeTabs.ActiveTabId, Is.EqualTo(ContentDefinitionType.Skill.ToString()), "the tabs follow a filter set from code");
            Assert.That(catalog.VisibleDefinitions.Select(d => d.Name), Is.EqualTo(new[] { "Climb" }));

            typeTabs.Select(ContentCatalogPresenter.AllTypesTabId);
            Assert.That(catalog.TypeFilter, Is.Null);
        }
    }
}
