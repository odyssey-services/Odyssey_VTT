using NUnit.Framework;
using Odyssey.Domain.Content;
using Odyssey.Persistence.Sqlite;
using Odyssey.Unity.Client;
using UnityEngine.UIElements;

namespace Odyssey.Tests.Unity.EditMode
{
    /// <summary>
    /// ODY-S11-219 (polish P1 item 9): tooltip on cut-off text. EditMode has no text layout, so the measurement is fed
    /// in through <see cref="OdyTruncationTooltip.UpdateTruncation"/>; the real hover over a cut name is a manual check.
    /// </summary>
    public sealed class OdyTruncationTooltipTests
    {
        [Test]
        public void WouldTruncate_OnlyWhenTheTextIsWiderThanItsSpace()
        {
            Assert.That(OdyTruncationTooltip.WouldTruncate(200f, 120f), Is.True);
            Assert.That(OdyTruncationTooltip.WouldTruncate(120f, 120f), Is.False, "exactly fits");
            Assert.That(OdyTruncationTooltip.WouldTruncate(120.3f, 120f), Is.False, "rounding slack");
            Assert.That(OdyTruncationTooltip.WouldTruncate(200f, 0f), Is.False, "not laid out yet");
            Assert.That(OdyTruncationTooltip.WouldTruncate(float.NaN, 120f), Is.False);
        }

        [Test]
        public void Tooltip_ShowsTheFullTextOnlyWhenCut_AndHides()
        {
            var screen = new VisualElement();
            screen.AddToClassList(OdyClasses.PopoverHost);
            Label label = OdyUi.TruncatedText("A very long item name that does not fit in the list", OdyClasses.ListItemTitle);
            screen.Add(label);
            OdyTruncationTooltip tooltip = OdyTruncationTooltip.Of(label)!;
            Assert.That(tooltip, Is.Not.Null, "TruncatedText attaches the rule");
            Assert.That(label.ClassListContains(OdyClasses.TextTruncate), Is.True, "one line with an ellipsis");

            tooltip.UpdateTruncation(80f, 300f);
            Assert.That(tooltip.Show(), Is.False, "text that fits gets no tooltip");

            tooltip.UpdateTruncation(420f, 160f);
            Assert.That(tooltip.Show(), Is.True);
            Assert.That(tooltip.Popover!.Host, Is.SameAs(screen));
            Assert.That(screen.Q<Label>("ody-tooltip-text").text, Is.EqualTo(label.text), "the full text");
            Assert.That(tooltip.Popover.Paper.pickingMode, Is.EqualTo(PickingMode.Ignore), "never steals the hover");

            tooltip.Hide();
            Assert.That(tooltip.IsShowing, Is.False);
            Assert.That(screen.Q<VisualElement>("ody-tooltip"), Is.Null, "removed from the host");
        }

        [Test]
        public void ListRows_UseTheRule_CatalogDefinitionNames()
        {
            using GameTestHost host = GameTestHost.Create();
            SqliteInventoryRepository inventory = host.NewInventoryRepository();
            using var catalog = new ContentCatalogPresenter(host.Context, host.NewCatalogRepository(inventory));
            VisualElement view = catalog.BuildView();
            catalog.StartNew(ContentDefinitionType.Skill);
            catalog.Form!.Name = "Climbing sheer cliffs in a storm while carrying a wounded companion";
            Assert.That(catalog.SaveDraft().IsSuccess, Is.True);

            Label title = view.Q<Label>(className: OdyClasses.ListItemTitle);
            Assert.That(title, Is.Not.Null);
            Assert.That(title.text, Does.StartWith("Climbing sheer cliffs"));
            Assert.That(OdyTruncationTooltip.Of(title), Is.Not.Null, "catalog list names show their full text when cut");
            Assert.That(OdyTruncationTooltip.Of(view.Q<Label>(className: OdyClasses.ListItemMeta)), Is.Not.Null);
        }
    }
}
