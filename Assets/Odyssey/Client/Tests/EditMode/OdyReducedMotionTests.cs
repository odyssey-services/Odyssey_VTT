using NUnit.Framework;
using Odyssey.Application.Persistence;
using Odyssey.Unity.Client;
using UnityEngine.UIElements;

namespace Odyssey.Tests.Unity.EditMode
{
    /// <summary>
    /// ODY-S11-223 (polish P2 item 12): the "Reduce motion" switch and the animations that honour it. Whether the screen
    /// really looks still is a manual check.
    /// </summary>
    public sealed class OdyReducedMotionTests
    {
        [Test]
        public void ShellSwitch_PutsTheReducedMotionClassOnTheScreen_AndReportsChanges()
        {
            using var runtime = new PresentationRuntime();
            var root = new VisualElement();
            var shell = new GameShellPresenter(root, new RoleSelection(), runtime, "Scene A");
            shell.Build();
            Button toggle = root.Q<Button>("game-reduce-motion");
            Assert.That(toggle, Is.Not.Null, "the switch lives in the top bar (there is no settings screen)");
            Assert.That(shell.ReducedMotion, Is.False);
            bool? reported = null;
            shell.ReducedMotionChanged += value => reported = value;

            shell.SetReducedMotion(true);
            Assert.That(shell.Screen.ClassListContains(OdyClasses.ReducedMotion), Is.True);
            Assert.That(OdyUi.IsActive(toggle), Is.True, "the switch shows its state like any toggle");
            Assert.That(reported, Is.True);

            shell.AddDrawer("combat", "Combat", GameDrawerSide.Right);
            Assert.That(shell.OpenDrawer("combat"), Is.True, "functional transitions still happen");
            Assert.That(shell.IsDrawerOpen("combat"), Is.True);

            shell.SetReducedMotion(false);
            Assert.That(shell.ReducedMotion, Is.False);
            Assert.That(reported, Is.False);
            shell.Dispose();
        }

        [Test]
        public void TokenMotion_Disabled_DrawsChangesMadeElsewhereAtOnce()
        {
            var motion = new BoardTokenMotion { Enabled = false };
            var target = new TokenPosition(10, 0);
            Assert.That(motion.Observe("t", new TokenPosition(0, 0), target, visible: true), Is.EqualTo(target));
            Assert.That(motion.IsAnimating("t"), Is.False, "the update still happens, only the easing is dropped");
        }

        [Test]
        public void ResourceBar_UnderReducedMotion_ShowsTheNewValueAtOnce()
        {
            var screen = new VisualElement();
            screen.AddToClassList(OdyClasses.ReducedMotion);
            var bar = new OdyResourceBar("hp");
            screen.Add(bar.Element);
            bar.SetValue(2, 0, 10);
            bar.AnimateFrom(1.0);
            Assert.That(bar.IsAnimating, Is.False);
            Assert.That(bar.DisplayedFraction, Is.EqualTo(0.2).Within(1e-9));

            // Animation started before the bar was under the reduced-motion screen: the first tick finishes it.
            var detached = new OdyResourceBar("mp");
            detached.SetValue(2, 0, 10);
            detached.AnimateFrom(1.0);
            Assert.That(detached.IsAnimating, Is.True);
            screen.Add(detached.Element);
            detached.AdvanceAnimation(1);
            Assert.That(detached.IsAnimating, Is.False);
            Assert.That(detached.DisplayedFraction, Is.EqualTo(0.2).Within(1e-9));
        }

        [Test]
        public void MarchingAntsAndSkeleton_StandStill_UnderTheShellSwitch()
        {
            using var runtime = new PresentationRuntime();
            var root = new VisualElement();
            var shell = new GameShellPresenter(root, new RoleSelection(), runtime, "Scene A");
            shell.Build();
            var host = new VisualElement();
            shell.BoardLayer.Add(host);
            var ants = new OdyMarchingAnts("ants");
            ants.AttachTo(host);
            var skeleton = new OdySkeleton("loading");
            shell.OverlayLayer.Add(skeleton.Element);

            shell.SetReducedMotion(true);
            ants.Advance(1000);
            skeleton.Advance(OdySkeleton.ShimmerHalfPeriodMs * 3);
            Assert.That(ants.Phase, Is.EqualTo(0f));
            Assert.That(skeleton.IsBright, Is.False);
            shell.Dispose();
        }
    }
}
