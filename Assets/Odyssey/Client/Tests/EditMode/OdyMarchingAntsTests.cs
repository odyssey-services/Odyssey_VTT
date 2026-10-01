using System.Linq;
using NUnit.Framework;
using Odyssey.Unity.Client;
using UnityEngine.UIElements;

namespace Odyssey.Tests.Unity.EditMode
{
    /// <summary>
    /// ODY-S11-216 (polish P1 item 6): marching-ants geometry and stepping. The moving dashes themselves are a manual
    /// visual check (EditMode has no renderer).
    /// </summary>
    public sealed class OdyMarchingAntsTests
    {
        [Test]
        public void Dashes_CoverThePerimeterWithTheDashPattern_SplittingAtCorners()
        {
            // 20 x 10 rectangle, perimeter 60, period 10 (dash 6 + gap 4): 6 dashes of 6 = 36 px of dash.
            var dashes = OdyMarchingAnts.Dashes(20f, 10f, 6f, 4f, 0f);
            Assert.That(dashes.Sum(d => d.Length), Is.EqualTo(36f).Within(1e-3f));
            Assert.That(dashes.All(d => OnPerimeter(d.X1, d.Y1, 20f, 10f) && OnPerimeter(d.X2, d.Y2, 20f, 10f)), Is.True, "every dash lies on the outline");
            Assert.That(dashes.All(d => d.X1 == d.X2 || d.Y1 == d.Y2), Is.True, "dashes crossing a corner are split, so each piece is straight");

            Assert.That(OdyMarchingAnts.Dashes(0f, 10f, 6f, 4f, 0f), Is.Empty, "nothing to outline");
        }

        [Test]
        public void Phase_ShiftsTheDashes_AndAFullPeriodLooksTheSame()
        {
            var still = OdyMarchingAnts.Dashes(40f, 40f, 6f, 4f, 0f);
            var moved = OdyMarchingAnts.Dashes(40f, 40f, 6f, 4f, 3f);
            Assert.That(moved[1].X1, Is.EqualTo(still[1].X1 + 3f).Within(1e-3f), "the offset slides the pattern along the edge");
            var wrapped = OdyMarchingAnts.Dashes(40f, 40f, 6f, 4f, 10f);
            Assert.That(wrapped.Select(d => d.X1), Is.EqualTo(still.Select(d => d.X1)).Within(1e-3f), "one full period is the same picture");
        }

        [Test]
        public void Advance_MovesAndWrapsThePhase_ButNotUnderReducedMotion()
        {
            var host = new VisualElement();
            var ants = new OdyMarchingAnts("ants", outset: 4f);
            ants.AttachTo(host);
            Assert.That(ants.Element.pickingMode, Is.EqualTo(PickingMode.Ignore), "never blocks the board");
            Assert.That(ants.Element.style.left.value.value, Is.EqualTo(-4f));

            ants.Advance(250);
            float expected = (float)(OdyMarchingAnts.SpeedPixelsPerSecond * 0.25 % (OdyMarchingAnts.DashLength + OdyMarchingAnts.GapLength));
            Assert.That(ants.Phase, Is.EqualTo(expected).Within(1e-3f));
            ants.Advance(10_000);
            Assert.That(ants.Phase, Is.LessThan(OdyMarchingAnts.DashLength + OdyMarchingAnts.GapLength), "stays within one period");

            host.AddToClassList(OdyClasses.ReducedMotion);
            float frozen = ants.Phase;
            ants.Advance(500);
            Assert.That(ants.Phase, Is.EqualTo(frozen), "reduced motion: the outline stays, the dashes stop");

            ants.Detach();
            Assert.That(ants.Element.parent, Is.Null);
        }

        private static bool OnPerimeter(float x, float y, float width, float height)
        {
            const float e = 1e-3f;
            bool onVertical = (System.Math.Abs(x) < e || System.Math.Abs(x - width) < e) && y >= -e && y <= height + e;
            bool onHorizontal = (System.Math.Abs(y) < e || System.Math.Abs(y - height) < e) && x >= -e && x <= width + e;
            return onVertical || onHorizontal;
        }
    }
}
