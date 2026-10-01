using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Time;
using Odyssey.Domain.Time;
using Odyssey.Unity.Client;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Tests.Unity.EditMode
{
    /// <summary>ODY-S11-201: Owlbear-style layout -- full-screen board, overlay top bar, drawers and dock.</summary>
    public sealed class GameShellPresenterTests
    {
        [Test]
        public void Build_CreatesFullScreenBoardLayerUnderPickingTransparentOverlay()
        {
            using var runtime = new PresentationRuntime();
            var root = new VisualElement();
            var shell = new GameShellPresenter(root, new RoleSelection(), runtime, "Scene A");
            shell.Build();

            Assert.That(root.ClassListContains(OdyClasses.GameRoot), Is.True);
            Assert.That(shell.Screen.parent, Is.SameAs(root));
            Assert.That(shell.Screen.IndexOf(shell.BoardLayer), Is.EqualTo(0), "the map is the bottom of the z-order");
            Assert.That(shell.Screen.IndexOf(shell.OverlayLayer), Is.GreaterThan(shell.Screen.IndexOf(shell.BoardLayer)));
            Assert.That(shell.ModalHost, Is.SameAs(shell.Screen), "modals mount above both layers");
            Assert.That(shell.BoardLayer.ClassListContains(OdyClasses.BoardLayer), Is.True);
            Assert.That(shell.OverlayLayer.pickingMode, Is.EqualTo(PickingMode.Ignore), "the overlay container never blocks the map");
            Assert.That(root.Q<VisualElement>("game-topbar"), Is.Not.Null);
            Assert.That(root.Q<VisualElement>("game-dock"), Is.Not.Null);
            shell.Dispose();
        }

        [Test]
        public void Drawers_StartClosed_OnePerSide_MapClickClosesAll()
        {
            using var runtime = new PresentationRuntime();
            var root = new VisualElement();
            var shell = new GameShellPresenter(root, new RoleSelection(), runtime, "Scene A");
            shell.Build();
            shell.AddDrawer("character", "Character", GameDrawerSide.Left);
            shell.AddDrawer("inventory", "Inventory", GameDrawerSide.Left);
            shell.AddDrawer("catalog", "Catalog", GameDrawerSide.Right, wide: true);
            var opened = new List<string>();
            shell.DrawerOpened += opened.Add;

            Assert.That(shell.IsDrawerOpen("character"), Is.False);
            Assert.That(root.Q<Button>("toggle-character"), Is.Not.Null, "every drawer gets a top-bar toggle");

            Assert.That(shell.OpenDrawer("character"), Is.True);
            Assert.That(shell.OpenDrawer("catalog"), Is.True);
            Assert.That(shell.IsDrawerOpen("character") && shell.IsDrawerOpen("catalog"), Is.True, "left and right are independent");

            shell.OpenDrawer("inventory");
            Assert.That(shell.IsDrawerOpen("inventory"), Is.True);
            Assert.That(shell.IsDrawerOpen("character"), Is.False, "one drawer per side keeps most of the map visible");
            Assert.That(root.Q<Button>("toggle-inventory").ClassListContains(OdyClasses.TabActive), Is.True);
            Assert.That(opened, Is.EqualTo(new[] { "character", "catalog", "inventory" }));

            shell.ToggleDrawer("inventory");
            Assert.That(shell.IsDrawerOpen("inventory"), Is.False);

            shell.OpenDrawer("inventory");
            shell.HandleMapPointerDown();
            Assert.That(shell.IsDrawerOpen("inventory") || shell.IsDrawerOpen("catalog"), Is.False, "a click on the map outside the panels closes them");
            shell.Dispose();
        }

        [Test]
        public void DrawerBadge_ShowsACountOnTheClosedToggle_AndHidesAtZero()
        {
            using var runtime = new PresentationRuntime();
            var root = new VisualElement();
            var shell = new GameShellPresenter(root, new RoleSelection(), runtime, "Scene A");
            shell.Build();
            shell.AddDrawer("combat", "Combat", GameDrawerSide.Right);
            Label badge = root.Q<Label>("toggle-combat-badge");
            Assert.That(badge, Is.Not.Null);
            Assert.That(badge.parent, Is.SameAs(root.Q<Button>("toggle-combat")), "the badge sits on the toggle itself");
            Assert.That(shell.DrawerBadgeText("combat"), Is.Null, "hidden while nothing waits");

            Assert.That(shell.SetDrawerBadge("combat", 2), Is.True);
            Assert.That(shell.IsDrawerOpen("combat"), Is.False);
            Assert.That(shell.DrawerBadgeText("combat"), Is.EqualTo("2"), "visible while the drawer is closed");
            shell.SetDrawerBadge("combat", 12);
            Assert.That(shell.DrawerBadgeText("combat"), Is.EqualTo("9+"));
            shell.SetDrawerBadge("combat", 0);
            Assert.That(shell.DrawerBadgeText("combat"), Is.Null);
            Assert.That(shell.SetDrawerBadge("unknown", 1), Is.False);
            shell.Dispose();
        }

        [Test]
        public void Dock_IsCollapsible_AndRoleBadgeFollowsSelection()
        {
            using var runtime = new PresentationRuntime();
            var root = new VisualElement();
            var selection = new RoleSelection();
            var shell = new GameShellPresenter(root, selection, runtime, "Scene A");
            shell.Build();
            shell.SetDockContent("Rolls", new Label("roll"));

            Assert.That(shell.IsDockCollapsed, Is.False);
            shell.ToggleDock();
            Assert.That(shell.IsDockCollapsed, Is.True);
            shell.ToggleDock();
            Assert.That(shell.IsDockCollapsed, Is.False);

            Assert.That(shell.RoleBadgeText, Is.EqualTo("Player"));
            selection.SelectRole(BaselineRole.MainGM);
            Assert.That(shell.RoleBadgeText, Is.EqualTo("MainGM"));
            shell.Dispose();
            selection.SelectRole(BaselineRole.Observer);
            Assert.That(shell.RoleBadgeText, Is.EqualTo("MainGM"), "disposed shell no longer listens");
        }

        [Test]
        public void TrialScreen_HostsBoardFullBleed_AndPanelsAsOverlays()
        {
            string directory = Path.Combine(Path.GetTempPath(), "odyssey-shell-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var gameObject = new GameObject("Shell Trial Document");
            try
            {
                UIDocument document = gameObject.AddComponent<UIDocument>();
                using var runtime = new PresentationRuntime();
                using var screen = new TrialScreenPresenter(document, runtime, directory, new FixedClock());
                Assert.That(screen.Initialize().IsSuccess, Is.True);

                GameShellPresenter shell = screen.Shell!;
                VisualElement boardArea = shell.BoardLayer.Q<VisualElement>("board-area");
                Assert.That(boardArea, Is.Not.Null, "the board lives in the full-screen board layer");
                Assert.That(boardArea.style.position.value, Is.EqualTo(Position.Absolute));
                Assert.That(boardArea.style.width.value.value, Is.Not.EqualTo(440f), "no fixed 440px inline width in the full-bleed layout");
                Assert.That(screen.Board!.FullBleed, Is.True);
                Assert.That(shell.BoardLayer.ClassListContains("app-root"), Is.False, "the dark developer theme no longer styles the board");

                Assert.That(shell.OverlayLayer.Q<VisualElement>("roll-panel"), Is.Not.Null, "roll panel is an overlay (dock)");
                Assert.That(shell.OverlayLayer.Q<VisualElement>("game-log"), Is.Not.Null, "game log is an overlay (dock)");
                Assert.That(shell.OverlayLayer.Q<VisualElement>("role-selector"), Is.Not.Null, "role selector sits in the top bar");
                Assert.That(shell.DrawerIds, Does.Contain(TrialScreenPresenter.AssetsDrawerId), "asset pool is a drawer");
                Assert.That(shell.IsDrawerOpen(TrialScreenPresenter.AssetsDrawerId), Is.False, "drawers start closed: the whole map is visible");
                Assert.That(screen.Context, Is.Not.Null);
                Assert.That(document.rootVisualElement.Q<VisualElement>("trial-screen"), Is.SameAs(shell.Screen));
                Assert.That(document.rootVisualElement.Q<VisualElement>("trial-controls-column"), Is.SameAs(shell.DockContent));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                try { Directory.Delete(directory, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        private sealed class FixedClock : IWallClock
        {
            public UtcInstant GetUtcNow() => UtcInstant.Parse("2026-09-30T10:00:00.0000000Z");
        }
    }
}
