using NUnit.Framework;
using Odyssey.Domain.Geometry;

namespace Odyssey.Tests.Unit.Geometry
{
    /// <summary>SLICE-10 Block 3: TC-PERSIST-080's own truth table for ADR-020 section 6.2's orientation primitive and the segment-intersection construction built on it.</summary>
    public sealed class SegmentIntersectionTests
    {
        [Test]
        public void Orientation_Left_Right_Collinear_ClassifyCorrectly()
        {
            // (0,0)->(1,0)->(1,1): a counter-clockwise (Left) turn.
            Assert.That(SegmentIntersection.Orientation(0, 0, 1, 0, 1, 1), Is.EqualTo(OrientationResult.Left));
            // (0,0)->(1,0)->(1,-1): a clockwise (Right) turn.
            Assert.That(SegmentIntersection.Orientation(0, 0, 1, 0, 1, -1), Is.EqualTo(OrientationResult.Right));
            // (0,0)->(1,0)->(2,0): all three on the same line.
            Assert.That(SegmentIntersection.Orientation(0, 0, 1, 0, 2, 0), Is.EqualTo(OrientationResult.Collinear));
        }

        [Test] // TC-PERSIST-080
        public void SegmentsIntersect_ClearCrossing_IsTrue()
        {
            // A diagonal X: (0,0)-(2,2) crosses (0,2)-(2,0) at (1,1).
            Assert.That(SegmentIntersection.SegmentsIntersect(0, 0, 2, 2, 0, 2, 2, 0), Is.True);
        }

        [Test] // TC-PERSIST-081
        public void SegmentsIntersect_ParallelNonIntersecting_IsFalse()
        {
            Assert.That(SegmentIntersection.SegmentsIntersect(0, 0, 10, 0, 0, 5, 10, 5), Is.False);
        }

        [Test] // TC-PERSIST-082
        public void SegmentsIntersect_CollinearOverlapping_FailsClosed_IsTrue()
        {
            // Both on the X axis: (0,0)-(5,0) and (3,0)-(8,0) -- they genuinely overlap on [3,5].
            Assert.That(SegmentIntersection.SegmentsIntersect(0, 0, 5, 0, 3, 0, 8, 0), Is.True);
        }

        [Test] // TC-PERSIST-083
        public void SegmentsIntersect_CollinearNonIntersecting_IsFalse()
        {
            // Both on the X axis but with a real gap between them: (0,0)-(2,0) and (5,0)-(8,0).
            // Collinear does not, by itself, mean blocking -- only a genuine touch/overlap does
            // (ADR-020 section 6.3 scopes fail-closed to "Collinear/touching", not to any distance).
            Assert.That(SegmentIntersection.SegmentsIntersect(0, 0, 2, 0, 5, 0, 8, 0), Is.False);
        }

        [Test] // TC-PERSIST-084
        public void SegmentsIntersect_EndpointTouch_FailsClosed_IsTrue()
        {
            // (0,0)-(2,2) touches (2,2)-(4,0) exactly at the shared endpoint (2,2).
            Assert.That(SegmentIntersection.SegmentsIntersect(0, 0, 2, 2, 2, 2, 4, 0), Is.True);
        }

        [Test] // TC-PERSIST-085
        public void SegmentsIntersect_TShapedTouch_FailsClosed_IsTrue()
        {
            // (0,0)-(4,0) is a horizontal segment; (2,0)-(2,3) touches it in a T-shape at (2,0),
            // the midpoint of the first segment and the endpoint of the second.
            Assert.That(SegmentIntersection.SegmentsIntersect(0, 0, 4, 0, 2, 0, 2, 3), Is.True);
        }

        [Test] // TC-PERSIST-086
        public void SegmentsIntersect_DisjointNonCollinear_IsFalse()
        {
            Assert.That(SegmentIntersection.SegmentsIntersect(0, 0, 1, 1, 5, 5, 6, 6.5), Is.False);
        }
    }
}
