using NUnit.Framework;
using Odyssey.Unity.Client;

namespace Odyssey.Tests.Unity.EditMode
{
    /// <summary>SLICE-10 Block 6 part 1: pure math tests for <see cref="BoardHitTestMath.DistancePointToSegment"/> -- plain method calls with numbers, no UI Toolkit/board involved.</summary>
    public sealed class BoardHitTestMathTests
    {
        [Test] // TC-BOARD-103
        public void PointDirectlyOnTheSegment_DistanceIsZero()
        {
            double distance = BoardHitTestMath.DistancePointToSegment(5, 0, 0, 0, 10, 0);
            Assert.That(distance, Is.EqualTo(0).Within(1e-9));
        }

        [Test] // TC-BOARD-104
        public void PointBeyondTheSegmentsEnd_DistanceIsToTheEndpoint_NotTheInfiniteLine()
        {
            // The segment is (0,0)-(10,0). A point at (20,0) is on the line's own extension, well past the
            // segment's own end -- the correct answer is the distance to (10,0), 10, not 0 (which a
            // point-to-infinite-line distance would wrongly report).
            double distance = BoardHitTestMath.DistancePointToSegment(20, 0, 0, 0, 10, 0);
            Assert.That(distance, Is.EqualTo(10).Within(1e-9));
        }

        [Test] // TC-BOARD-105
        public void PointBesideTheSegment_DistanceIsThePerpendicularOffset()
        {
            // The segment is (0,0)-(10,0); a point at (5,3) sits directly above its midpoint.
            double distance = BoardHitTestMath.DistancePointToSegment(5, 3, 0, 0, 10, 0);
            Assert.That(distance, Is.EqualTo(3).Within(1e-9));
        }

        [Test] // TC-BOARD-106
        public void PointBeforeTheSegmentsStart_DistanceIsToTheStartpoint()
        {
            double distance = BoardHitTestMath.DistancePointToSegment(-5, 0, 0, 0, 10, 0);
            Assert.That(distance, Is.EqualTo(5).Within(1e-9));
        }

        [Test] // TC-BOARD-107
        public void DegenerateZeroLengthSegment_IsTreatedAsAPoint()
        {
            double distance = BoardHitTestMath.DistancePointToSegment(3, 4, 0, 0, 0, 0);
            Assert.That(distance, Is.EqualTo(5).Within(1e-9));
        }
    }
}
