using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;
using Odyssey.Unity.Client;
using UnityEngine.UIElements;

namespace Odyssey.Tests.Unity.EditMode
{
    /// <summary>ODY-S11-200 phase 0: the reusable design-system building blocks behave as documented.</summary>
    public sealed class OdysseyUiKitTests
    {
        [Test]
        public void Tabs_FirstTabIsActive_SelectSwitchesVisiblePanel()
        {
            var tabs = new OdyTabs("sheet-tabs");
            tabs.AddTab("general", "General");
            tabs.AddTab("skills", "Skills");
            var changes = new List<string>();
            tabs.TabChanged += changes.Add;

            Assert.That(tabs.ActiveTabId, Is.EqualTo("general"));
            Assert.That(tabs.IsPanelVisible("general"), Is.True);
            Assert.That(tabs.IsPanelVisible("skills"), Is.False);

            Assert.That(tabs.Select("skills"), Is.True);
            Assert.That(tabs.IsPanelVisible("general"), Is.False);
            Assert.That(tabs.IsPanelVisible("skills"), Is.True);
            Assert.That(tabs.Element.Q<Button>("tab-skills").ClassListContains(OdyClasses.TabActive), Is.True);
            Assert.That(changes, Is.EqualTo(new[] { "skills" }));
            Assert.That(tabs.Select("unknown"), Is.False);
        }

        [Test]
        public void ResourceBar_ComputesClampedFractionAndLabel()
        {
            var bar = new OdyResourceBar("hp");
            bar.SetValue(5, 0, 10, "HP");
            Assert.That(bar.FillFraction, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(bar.LabelText, Is.EqualTo("HP  5 / 10"));

            bar.SetValue(15, 0, 10);
            Assert.That(bar.FillFraction, Is.EqualTo(1.0));
            bar.SetValue(-3, 0, 10);
            Assert.That(bar.FillFraction, Is.EqualTo(0.0));
            bar.SetValue(3, 0, 0);
            Assert.That(bar.FillFraction, Is.EqualTo(0.0), "a non-positive span shows an empty bar, never a division by zero");
        }

        [Test]
        public void ConfirmDialog_RequiresTextBeforeConfirming_AndRemovesItselfOnEitherOutcome()
        {
            var host = new VisualElement();
            string? confirmedWith = null;
            var options = new OdyConfirmOptions("Delete character", "This cannot be undone.", "Delete") { Destructive = true, RequiredTextLabel = "Reason" };
            OdyConfirmDialog dialog = OdyConfirmDialog.Show(host, options, text => confirmedWith = text);

            Assert.That(host.Q<VisualElement>("ody-confirm-dialog"), Is.Not.Null);
            Assert.That(dialog.Confirm(), Is.False, "blank required text keeps the dialog open");
            Assert.That(dialog.IsOpen, Is.True);
            Assert.That(confirmedWith, Is.Null);

            dialog.SetText("  duplicate  ");
            Assert.That(dialog.Confirm(), Is.True);
            Assert.That(confirmedWith, Is.EqualTo("duplicate"));
            Assert.That(dialog.IsOpen, Is.False);
            Assert.That(host.Q<VisualElement>("ody-confirm-dialog"), Is.Null);

            bool cancelled = false;
            OdyConfirmDialog second = OdyConfirmDialog.Show(host, new OdyConfirmOptions("Archive", "Archive it?", "Archive"), _ => Assert.Fail("must not confirm"), () => cancelled = true);
            second.Cancel();
            Assert.That(cancelled, Is.True);
            Assert.That(host.childCount, Is.EqualTo(0));
        }

        [Test]
        public void Banner_ShowsKindAndText_HideClears()
        {
            var banner = new OdyBanner("catalog-banner");
            Assert.That(banner.IsVisible, Is.False);
            banner.Show(OdyBannerKind.Warning, "Only the MainGM can author.");
            Assert.That(banner.IsVisible, Is.True);
            Assert.That(banner.Text, Is.EqualTo("Only the MainGM can author."));
            Assert.That(banner.Element.ClassListContains("ody-banner--warning"), Is.True);
            banner.Show(OdyBannerKind.Error, "x");
            Assert.That(banner.Element.ClassListContains("ody-banner--warning"), Is.False);
            banner.Hide();
            Assert.That(banner.IsVisible, Is.False);
        }

        [Test]
        public void Badge_ReplacesPreviousKindModifier()
        {
            Label badge = OdyUi.Badge("Draft", OdyStatusKind.Draft);
            Assert.That(badge.ClassListContains("ody-badge--draft"), Is.True);
            OdyUi.SetBadgeKind(badge, OdyStatusKind.Published);
            Assert.That(badge.ClassListContains("ody-badge--draft"), Is.False);
            Assert.That(badge.ClassListContains("ody-badge--published"), Is.True);
        }

        [Test]
        public void ParseList_TrimsDeduplicatesAndDropsEmpties()
        {
            Assert.That(OdyUi.ParseList(" arrow, bolt ,,arrow\n stone "), Is.EqualTo(new[] { "arrow", "bolt", "stone" }));
            Assert.That(OdyUi.ParseList("   "), Is.Empty);
        }

        [Test]
        public void Messages_UseSafeUserMessageKeyOrReasonCodeOnly()
        {
            Error denied = Error.Create(ErrorCodes.ContentCatalogAuthoringDenied, ErrorCategory.Authorization, SafeReasonCode.PermissionDenied, UserMessageKey.Parse("errors.content_catalog.authoring_denied"), RetryDirective.DoNotRetry, CorrelationId.Parse("corr_00000000000000000000000000000000"));
            Assert.That(OdyMessages.Describe(denied), Is.EqualTo("Only the MainGM can author the content catalog."));

            Error unknown = Error.Create(ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Conflict, SafeReasonCode.StateChanged, UserMessageKey.Parse("errors.some.unmapped_key"), RetryDirective.DoNotRetry, CorrelationId.Parse("corr_00000000000000000000000000000000"));
            Assert.That(OdyMessages.Describe(unknown), Does.Contain("changed since it was loaded"));
        }
    }
}
