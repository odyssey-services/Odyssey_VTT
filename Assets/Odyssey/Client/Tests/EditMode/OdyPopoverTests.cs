using NUnit.Framework;
using Odyssey.Unity.Client;
using UnityEngine.UIElements;

namespace Odyssey.Tests.Unity.EditMode
{
    /// <summary>
    /// ODY-S11-210 (polish P0 item 1): the unified popover primitive and its three migrated call sites. EditMode has no
    /// layout pass, so placement is checked through <see cref="OdyPopoverLayout"/> and <see cref="OdyPopover.PlaceWithin"/>
    /// with explicit geometry; what it looks like on screen is a manual check (see the task contract).
    /// </summary>
    public sealed class OdyPopoverTests
    {
        private static readonly OdyRect Viewport = new OdyRect(0f, 0f, 1000f, 600f);

        [Test]
        public void Layout_PivotSetsTheGrowthDirection()
        {
            OdyRect growsRightDown = OdyPopoverLayout.Place(100f, 100f, OdyPopoverOrigin.TopLeft, 200f, 50f, Viewport, 8f);
            Assert.That((growsRightDown.X, growsRightDown.Y), Is.EqualTo((100f, 100f)));

            OdyRect growsLeftDown = OdyPopoverLayout.Place(500f, 100f, OdyPopoverOrigin.TopRight, 200f, 50f, Viewport, 8f);
            Assert.That((growsLeftDown.X, growsLeftDown.Y), Is.EqualTo((300f, 100f)));

            OdyRect growsUp = OdyPopoverLayout.Place(500f, 300f, new OdyPopoverOrigin(OdyPopoverHorizontal.Center, OdyPopoverVertical.Bottom), 200f, 50f, Viewport, 8f);
            Assert.That((growsUp.X, growsUp.Y), Is.EqualTo((400f, 250f)));

            OdyRect centered = OdyPopoverLayout.Place(500f, 300f, OdyPopoverOrigin.Center, 440f, 200f, Viewport, 8f);
            Assert.That((centered.X, centered.Y, centered.Width, centered.Height), Is.EqualTo((280f, 200f, 440f, 200f)));
        }

        [Test]
        public void Layout_KeepsTheEdgeMargin_MovingAndShrinkingSoNothingIsClipped()
        {
            OdyRect pushedIn = OdyPopoverLayout.Place(950f, 580f, OdyPopoverOrigin.TopLeft, 200f, 100f, Viewport, 8f);
            Assert.That(pushedIn.Right, Is.EqualTo(992f), "right edge keeps the margin");
            Assert.That(pushedIn.Bottom, Is.EqualTo(592f), "bottom edge keeps the margin");

            OdyRect pushedFromTopLeft = OdyPopoverLayout.Place(-50f, -50f, OdyPopoverOrigin.TopLeft, 100f, 100f, Viewport, 8f);
            Assert.That((pushedFromTopLeft.X, pushedFromTopLeft.Y), Is.EqualTo((8f, 8f)));

            OdyRect oversized = OdyPopoverLayout.Place(10f, 10f, OdyPopoverOrigin.TopLeft, 5000f, 5000f, Viewport, 8f);
            Assert.That((oversized.X, oversized.Y, oversized.Width, oversized.Height), Is.EqualTo((8f, 8f, 984f, 584f)), "larger than the viewport -> shrunk to fit inside the margins");
        }

        [Test]
        public void ElementAnchor_UsesTheAnchorOriginAndOffset_PointAnchorUsesCoordinates()
        {
            var host = new VisualElement();
            var anchorElement = new VisualElement();
            host.Add(anchorElement);
            var anchor = OdyPopoverAnchor.ToElement(anchorElement, OdyPopoverOrigin.BottomLeft);
            anchor.OffsetY = 4f;
            var popover = OdyPopover.Show(host, new Label("menu"), new OdyPopoverOptions(anchor) { Width = 150f, Name = "p" });

            OdyRect placed = popover.PlaceWithin(Viewport, new OdyRect(40f, 20f, 120f, 30f), 150f, 80f);
            Assert.That((placed.X, placed.Y), Is.EqualTo((40f, 54f)), "below the anchor's bottom-left corner, plus the offset");
            Assert.That(popover.Paper.style.left.value.value, Is.EqualTo(40f));
            Assert.That(popover.Paper.style.top.value.value, Is.EqualTo(54f));
            Assert.That(popover.Paper.style.width.value.value, Is.EqualTo(150f));

            var atPoint = OdyPopover.Show(host, new Label("ctx"), new OdyPopoverOptions(OdyPopoverAnchor.ToPoint(300f, 200f)) { Pivot = OdyPopoverOrigin.TopRight });
            OdyRect pointPlaced = atPoint.PlaceWithin(Viewport, new OdyRect(300f, 200f, 0f, 0f), 100f, 40f);
            Assert.That((pointPlaced.X, pointPlaced.Y), Is.EqualTo((200f, 200f)), "grows left from the point");
        }

        [Test]
        public void SetWidthAndSetHeight_ChangeTheSizeWhileOpen()
        {
            var host = new VisualElement();
            var popover = OdyPopover.Show(host, new Label("x"), new OdyPopoverOptions(OdyPopoverAnchor.ToPoint(0f, 0f)) { Width = 200f });
            Assert.That(popover.IsOpen, Is.True);

            popover.SetWidth(320f);
            popover.SetHeight(120f);
            Assert.That(popover.Width, Is.EqualTo(320f));
            Assert.That(popover.Height, Is.EqualTo(120f));
            Assert.That(popover.Paper.style.width.value.value, Is.EqualTo(320f));
            Assert.That(popover.Paper.style.height.value.value, Is.EqualTo(120f));

            popover.SetWidth(null);
            Assert.That(popover.Width, Is.Null, "back to content size");
            Assert.That(popover.Paper.style.width.keyword, Is.EqualTo(StyleKeyword.Null));
        }

        [Test]
        public void ClickAway_ClosesOnOutsidePresses_NotInsideOrOnTheAnchor_AndCanBeDisabled()
        {
            var host = new VisualElement();
            var anchorElement = new Button { name = "anchor" };
            host.Add(anchorElement);
            var inside = new Button { name = "inside" };
            var outside = new VisualElement { name = "outside" };
            host.Add(outside);
            OdyPopoverCloseReason? reason = null;

            var popover = OdyPopover.Show(host, inside, new OdyPopoverOptions(OdyPopoverAnchor.ToElement(anchorElement, OdyPopoverOrigin.BottomLeft)));
            popover.Closed += r => reason = r;
            Assert.That(popover.HandleClickAway(inside), Is.False, "a press inside keeps it open");
            Assert.That(popover.HandleClickAway(anchorElement), Is.False, "the anchor (its toggle) handles itself");
            Assert.That(popover.HandleClickAway(outside), Is.True);
            Assert.That(popover.IsOpen, Is.False);
            Assert.That(reason, Is.EqualTo(OdyPopoverCloseReason.ClickAway));
            Assert.That(popover.Element.parent, Is.Null, "one-shot popovers remove themselves");

            var sticky = OdyPopover.Show(host, new Label("s"), new OdyPopoverOptions(OdyPopoverAnchor.ToPoint(0f, 0f)) { DisableClickAway = true });
            Assert.That(sticky.HandleClickAway(outside), Is.False);
            Assert.That(sticky.IsOpen, Is.True, "disableClickAway: only an explicit close");
            sticky.Close();
            Assert.That(sticky.IsOpen, Is.False);
        }

        [Test]
        public void HidePaper_Modal_AndPersistentPopoversBuildTheRightElements()
        {
            var host = new VisualElement();
            var plain = new OdyPopover(host, new Label("a"), new OdyPopoverOptions(OdyPopoverAnchor.ToPoint(0f, 0f)) { Name = "plain" });
            Assert.That(plain.Element, Is.SameAs(plain.Paper));
            Assert.That(plain.Paper.ClassListContains(OdyClasses.PopoverPaper), Is.True);
            Assert.That(plain.Paper.ClassListContains(OdyClasses.PopoverPaperHidden), Is.False);

            var bare = new OdyPopover(host, new Label("b"), new OdyPopoverOptions(OdyPopoverAnchor.ToPoint(0f, 0f)) { HidePaper = true });
            Assert.That(bare.Paper.ClassListContains(OdyClasses.PopoverPaperHidden), Is.True, "hidePaper drops the default surface");

            var modal = new OdyPopover(host, new Label("m"), new OdyPopoverOptions(OdyPopoverAnchor.ToElement(host, OdyPopoverOrigin.Center)) { Modal = true, Name = "m", PaperName = "m-panel" });
            Assert.That(modal.Element.ClassListContains(OdyClasses.ModalScrim), Is.True, "modal popovers sit on a scrim");
            Assert.That(modal.Paper.parent, Is.SameAs(modal.Element));
            Assert.That(modal.Paper.name, Is.EqualTo("m-panel"));

            var persistent = new OdyPopover(host, new Label("p"), new OdyPopoverOptions(OdyPopoverAnchor.ToPoint(0f, 0f)) { RemoveOnClose = false, Name = "persistent" });
            persistent.Mount();
            Assert.That(host.Q<VisualElement>("persistent"), Is.Not.Null, "mounted before first open");
            Assert.That(OdyUi.IsVisible(persistent.Element), Is.False);
            persistent.Open();
            Assert.That(OdyUi.IsVisible(persistent.Element), Is.True);
            persistent.Close();
            Assert.That(persistent.Element.parent, Is.SameAs(host), "persistent popovers stay mounted, hidden");
            Assert.That(OdyUi.IsVisible(persistent.Element), Is.False);
        }

        [Test]
        public void Select_OpensItsListAsAPopover_ChoosingRaisesValueChangedAndCloses()
        {
            var screen = new VisualElement { name = "screen" };
            screen.AddToClassList(OdyClasses.PopoverHost);
            var form = new VisualElement();
            screen.Add(form);
            OdySelect select = OdyUi.Select("Mode", new[] { "Melee", "Ranged", "Thrown" }, 0, "mode");
            form.Add(select);
            string? changedTo = null;
            select.ValueChanged += v => changedTo = v;

            Assert.That(select.Value, Is.EqualTo("Melee"));
            OdyPopover? menu = select.OpenMenu();
            Assert.That(menu, Is.Not.Null);
            Assert.That(menu!.Host, Is.SameAs(screen), "the list mounts on the nearest popover host, above the panels");
            Assert.That(screen.Q<Button>("mode-option-1"), Is.Not.Null);
            Assert.That(screen.Q<Button>("mode-option-0").ClassListContains(OdyClasses.SelectOptionActive), Is.True);

            Assert.That(select.Choose("Ranged"), Is.True);
            Assert.That(changedTo, Is.EqualTo("Ranged"));
            Assert.That(select.Index, Is.EqualTo(1));
            Assert.That(select.IsMenuOpen, Is.False);
            Assert.That(screen.Q<VisualElement>("mode-menu"), Is.Null);

            Assert.That(select.Choose("Unknown"), Is.False, "values outside the choices are ignored");
            select.SetValueWithoutNotify("Thrown");
            Assert.That(changedTo, Is.EqualTo("Ranged"), "no notification");

            form.SetEnabled(false);
            Assert.That(select.OpenMenu(), Is.Null, "a read-only form does not open its lists");
        }

        [Test]
        public void MigratedCallSites_ConfirmDialogDrawersAndCatalogSelectorsUseThePopover()
        {
            var host = new VisualElement();
            OdyConfirmDialog dialog = OdyConfirmDialog.Show(host, new OdyConfirmOptions("Delete", "Sure?", "Delete"), _ => { });
            Assert.That(dialog.Popover.Options.Modal, Is.True);
            Assert.That(dialog.Popover.Options.DisableClickAway, Is.True, "irreversible actions need an explicit answer");
            Assert.That(dialog.Popover.HandleClickAway(null), Is.False);
            Assert.That(dialog.IsOpen, Is.True);
            dialog.Cancel();

            using var runtime = new PresentationRuntime();
            var root = new VisualElement();
            var shell = new GameShellPresenter(root, new RoleSelection(), runtime, "Scene A");
            shell.Build();
            shell.AddDrawer("catalog", "Catalog", GameDrawerSide.Right, wide: true);
            OdyPopover drawer = shell.DrawerPopover("catalog")!;
            Assert.That(drawer.Options.ClickAwayScope, Is.SameAs(shell.BoardLayer), "only the map closes drawers");
            Assert.That(drawer.Options.RemoveOnClose, Is.False);
            Assert.That(drawer.Paper.ClassListContains(OdyClasses.Drawer), Is.True);
            Assert.That(shell.Screen.ClassListContains(OdyClasses.PopoverHost), Is.True);

            shell.OpenDrawer("catalog");
            OdyRect placed = drawer.PlaceWithin(Viewport, Viewport, GameShellPresenter.DrawerWideWidth, 0f);
            Assert.That(placed.Right, Is.EqualTo(Viewport.Right - GameShellPresenter.DrawerEdgeInset), "right drawer grows left from the inset corner");
            Assert.That(placed.Y, Is.EqualTo(GameShellPresenter.DrawerTopOffset), "below the top bar");
            Assert.That(placed.Bottom, Is.EqualTo(Viewport.Bottom - GameShellPresenter.DrawerEdgeInset), "down to the bottom edge");

            Assert.That(drawer.HandleClickAway(null), Is.True);
            Assert.That(shell.IsDrawerOpen("catalog"), Is.False);
            Assert.That(root.Q<Button>("toggle-catalog").ClassListContains(OdyClasses.ButtonToggleOn), Is.False, "the toggle follows a click-away close");
            shell.Dispose();
        }
    }
}
