using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Application.Persistence;
using Odyssey.Unity.Client;

namespace Odyssey.Tests.Unity.EditMode
{
    /// <summary>
    /// ODY-S11-211 (polish P0 item 2): local action instant, remote action animated. Tweens are stepped with explicit
    /// milliseconds (no clock, no frames); how the motion looks on screen is a manual check (see the task contract).
    /// </summary>
    public sealed class OdyMotionTests
    {
        private const double Duration = OdyMotion.RemoteUpdateDurationMs;

        [Test]
        public void Tween_EasesOutFromStartToTarget_WithinTheRequestedDurationRange()
        {
            Assert.That(OdyMotion.RemoteUpdateDurationMs, Is.InRange(150.0, 250.0), "starting point inside the requested 150-250 ms");
            var tween = new OdyTween(0.0, 100.0, Duration);
            Assert.That(tween.Current, Is.EqualTo(0.0));

            tween.Advance(Duration / 2);
            Assert.That(tween.Current, Is.GreaterThan(50.0).And.LessThan(100.0), "ease-out: past halfway at half time");
            Assert.That(tween.IsDone, Is.False);

            tween.Advance(Duration);
            Assert.That(tween.IsDone, Is.True);
            Assert.That(tween.Current, Is.EqualTo(100.0), "arrives exactly, never overshoots");

            Assert.That(new OdyTween(5.0, 9.0, 0.0).Current, Is.EqualTo(9.0), "zero duration = instant");
        }

        [Test]
        public void TokenMotion_LocalChangeIsInstant_RemoteChangeEasesToTheNewPosition()
        {
            var motion = new BoardTokenMotion();
            var start = new TokenPosition(0, 0);
            var target = new TokenPosition(10, 0);

            Assert.That(motion.Observe("a", null, start, visible: true), Is.EqualTo(start), "first render: drawn where it is");

            motion.MarkLocal("a");
            Assert.That(motion.Observe("a", start, target, visible: true), Is.EqualTo(target), "own move: no delay");
            Assert.That(motion.IsAnimating("a"), Is.False);

            TokenPosition drawn = motion.Observe("a", target, start, visible: true);
            Assert.That(drawn, Is.EqualTo(target), "a move from elsewhere starts where the token was drawn");
            Assert.That(motion.IsAnimating("a"), Is.True);

            Assert.That(motion.Advance(Duration / 2), Is.True);
            Assert.That(motion.TryGetDisplayed("a", out TokenPosition mid), Is.True);
            Assert.That(mid.X, Is.LessThan(10.0).And.GreaterThan(0.0));

            Assert.That(motion.Advance(Duration), Is.False, "finished");
            Assert.That(motion.IsAnimating("a"), Is.False);
            Assert.That(motion.TryGetDisplayed("a", out _), Is.False, "drawn at the authoritative position again");
        }

        [Test]
        public void TokenMotion_ReRenderKeepsGoing_NewTargetContinuesFromTheDrawnPoint_GrabStopsIt_HiddenIsInstant()
        {
            var motion = new BoardTokenMotion();
            var a = new TokenPosition(0, 0);
            var b = new TokenPosition(20, 0);
            var c = new TokenPosition(20, 20);

            motion.Observe("t", a, b, visible: true);
            motion.Advance(Duration / 4);
            motion.TryGetDisplayed("t", out TokenPosition before);
            Assert.That(motion.Observe("t", b, b, visible: true), Is.EqualTo(before), "a refresh mid-animation does not restart it");
            Assert.That(motion.IsAnimating("t"), Is.True);

            Assert.That(motion.Observe("t", b, c, visible: true), Is.EqualTo(before), "re-targeted from where it is drawn, no jump");
            motion.Advance(Duration);
            Assert.That(motion.IsAnimating("t"), Is.False);

            motion.Observe("t", c, a, visible: true);
            motion.Cancel("t");
            Assert.That(motion.IsAnimating("t"), Is.False, "the local user grabbing it wins at once");

            Assert.That(motion.Observe("h", a, b, visible: false), Is.EqualTo(b), "hidden tokens are never animated");
            Assert.That(motion.IsAnimating("h"), Is.False);

            motion.Observe("gone", a, b, visible: true);
            motion.RetainOnly(new List<string> { "t" });
            Assert.That(motion.IsAnimating("gone"), Is.False, "tokens no longer rendered are forgotten");
        }

        [Test]
        public void ResourceBar_SetValueIsInstant_AnimateFromEasesTheFill_LabelShowsTheNewValueAtOnce()
        {
            var bar = new OdyResourceBar("hp");
            bar.SetValue(10, 0, 10, "HP");
            Assert.That(bar.DisplayedFraction, Is.EqualTo(1.0));
            Assert.That(bar.IsAnimating, Is.False);

            bar.SetValue(4, 0, 10, "HP");
            Assert.That(bar.DisplayedFraction, Is.EqualTo(0.4).Within(1e-9), "SetValue alone: instant");

            bar.AnimateFrom(1.0);
            Assert.That(bar.IsAnimating, Is.True);
            Assert.That(bar.DisplayedFraction, Is.EqualTo(1.0), "starts at the previously shown value");
            Assert.That(bar.FillFraction, Is.EqualTo(0.4).Within(1e-9));
            Assert.That(bar.LabelText, Is.EqualTo("HP  4 / 10"), "the numbers are never animated");

            bar.AdvanceAnimation(Duration / 2);
            Assert.That(bar.DisplayedFraction, Is.LessThan(1.0).And.GreaterThan(0.4));
            bar.AdvanceAnimation(Duration);
            Assert.That(bar.IsAnimating, Is.False);
            Assert.That(bar.DisplayedFraction, Is.EqualTo(0.4).Within(1e-9));

            bar.AnimateFrom(0.4);
            Assert.That(bar.IsAnimating, Is.False, "no change, no animation");
        }
    }
}
