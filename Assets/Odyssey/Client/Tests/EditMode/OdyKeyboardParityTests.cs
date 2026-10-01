using NUnit.Framework;
using Odyssey.Domain.Character;
using Odyssey.Persistence.Sqlite;
using Odyssey.Unity.Client;
using UnityEngine.UIElements;

namespace Odyssey.Tests.Unity.EditMode
{
    /// <summary>
    /// ODY-S11-225 (polish P2 item 14): Escape closes the topmost popover or modal; Enter submits chosen single-line
    /// fields. EditMode has no event dispatch, so the handlers' public entry points are called; the real key presses
    /// are covered by the PlayMode test TC-KEYPARITY-006.
    /// </summary>
    public sealed class OdyKeyboardParityTests
    {
        [Test]
        public void Escape_ClosesOnlyTheTopmostOpenPopover_ThenTheNextOne()
        {
            var screen = new VisualElement();
            screen.AddToClassList(OdyClasses.PopoverHost);
            var overlay = new VisualElement();
            screen.Add(overlay);
            var drawer = new OdyPopover(overlay, new Label("drawer"), new OdyPopoverOptions(OdyPopoverAnchor.ToPoint(0f, 0f)) { RemoveOnClose = false, Name = "drawer" });
            drawer.Open();
            OdySelect select = OdyUi.Select("Mode", new[] { "A", "B" }, 0, "mode");
            screen.Add(select);
            OdyPopover menu = select.OpenMenu()!;
            OdyPopoverCloseReason? reason = null;
            menu.Closed += r => reason = r;

            Assert.That(OdyPopover.TopmostOpen(screen), Is.SameAs(menu), "the list opened over the drawer is on top");
            Assert.That(drawer.HandleEscape(screen), Is.False, "a popover below the top one stays open");
            Assert.That(menu.HandleEscape(screen), Is.True);
            Assert.That(reason, Is.EqualTo(OdyPopoverCloseReason.Escape));
            Assert.That(select.IsMenuOpen, Is.False);
            Assert.That(drawer.IsOpen, Is.True, "one Escape closes one popover");

            Assert.That(OdyPopover.TopmostOpen(screen), Is.SameAs(drawer));
            Assert.That(drawer.HandleEscape(screen), Is.True, "the next Escape closes the next one");
            Assert.That(OdyPopover.TopmostOpen(screen), Is.Null);

            var sticky = OdyPopover.Show(screen, new Label("s"), new OdyPopoverOptions(OdyPopoverAnchor.ToPoint(0f, 0f)) { CloseOnEscape = false });
            Assert.That(sticky.HandleEscape(screen), Is.False, "opt-out respected");
        }

        [Test]
        public void Escape_OnAConfirmDialog_IsCancel_NeverConfirm()
        {
            var host = new VisualElement();
            bool confirmed = false;
            bool cancelled = false;
            OdyConfirmDialog dialog = OdyConfirmDialog.Show(host, new OdyConfirmOptions("Delete?", "Gone for good.", "Delete") { Destructive = true }, _ => confirmed = true, () => cancelled = true);
            Assert.That(dialog.Popover.Options.DisableClickAway, Is.True, "a stray click still does not dismiss it");

            Assert.That(dialog.Popover.HandleEscape(host), Is.True);
            Assert.That(cancelled, Is.True);
            Assert.That(confirmed, Is.False);
            Assert.That(dialog.IsOpen, Is.False);
            Assert.That(host.Q<VisualElement>("ody-confirm-dialog"), Is.Null);
            Assert.That(dialog.Confirm(), Is.False, "a closed dialog cannot be confirmed afterwards");
        }

        [Test]
        public void Escape_ClosesAnOpenDrawer_AndItsToggleFollows()
        {
            using var runtime = new PresentationRuntime();
            var root = new VisualElement();
            var shell = new GameShellPresenter(root, new RoleSelection(), runtime, "Scene A");
            shell.Build();
            shell.AddDrawer("combat", "Combat", GameDrawerSide.Right);
            shell.OpenDrawer("combat");

            Assert.That(shell.DrawerPopover("combat")!.HandleEscape(root), Is.True);
            Assert.That(shell.IsDrawerOpen("combat"), Is.False);
            Assert.That(OdyUi.IsActive(root.Q<Button>("toggle-combat")), Is.False);
            shell.Dispose();
        }

        [Test]
        public void Enter_SubmitsSingleLineFields_NotMultiline_NorDisabled()
        {
            int submitted = 0;
            TextField single = OdyUi.TextField("Id", string.Empty, "id");
            OdyEnterSubmit enter = OdyUi.SubmitOnEnter(single, () => submitted++);
            Assert.That(OdyEnterSubmit.Of(single), Is.SameAs(enter));
            Assert.That(enter.Submit(), Is.True);
            Assert.That(submitted, Is.EqualTo(1));

            single.SetEnabled(false);
            Assert.That(enter.Submit(), Is.False, "a read-only field does nothing on Enter");
            Assert.That(submitted, Is.EqualTo(1));

            TextField multiline = OdyUi.TextField("Comment", string.Empty, "comment", multiline: true);
            TestDelegate attachToMultiline = () => OdyUi.SubmitOnEnter(multiline, () => submitted++);
            Assert.That(attachToMultiline, Throws.ArgumentException, "Enter in a multiline field is a new line");
        }

        [Test]
        public void Enter_InTheCharacterOpenByIdField_OpensThatCharacter()
        {
            using GameTestHost host = GameTestHost.Create();
            SqliteInventoryRepository inventory = host.NewInventoryRepository();
            using var panel = new CharacterPanelPresenter(host.Context, host.NewCharacterRepository(inventory), host.NewSceneRepository(), host.ExportDirectory);
            VisualElement view = panel.BuildView();
            panel.CreateCharacter(new CharacterCreateForm { DisplayName = "Rook", Kind = CharacterKind.PlayerCharacter, PrimaryOwnerUserId = host.Selection.PlayerUserId });
            string id = panel.Current!.CharacterId.ToString();
            panel.Open(panel.Current.CharacterId);

            TextField openId = view.Q<TextField>("character-open-id");
            OdyEnterSubmit enter = OdyEnterSubmit.Of(openId)!;
            Assert.That(enter, Is.Not.Null, "Open by id submits on Enter");
            openId.value = id;
            Assert.That(enter.Submit(), Is.True);
            Assert.That(panel.Current!.CharacterId.ToString(), Is.EqualTo(id));
        }
    }
}
