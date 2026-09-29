using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Domain.Geometry;

namespace Odyssey.Tests.Unit.Geometry
{
    /// <summary>SLICE-10 Block 5: TC-PERSIST-120+ -- pure geometry, no repository/database involved.</summary>
    public sealed class CoverTests
    {
        [Test] // TC-PERSIST-120
        public void ComputeCoverDegree_NoObstacles_IsNone()
        {
            var noObstacles = new List<ObstacleSegment>();
            CoverDegree degree = CoverGeometry.ComputeCoverDegree(0, 0, 10, 0, noObstacles);
            Assert.That(degree, Is.EqualTo(CoverDegree.None));
        }

        [Test] // TC-PERSIST-121
        public void ComputeCoverDegree_WallBlockingAllFourSamples_IsFull()
        {
            // A wide wall spanning well past the target's sample offsets on both axes.
            var wall = new[] { new ObstacleSegment(ObstacleKind.Wall, null, 5, -5, 5, 5) };
            CoverDegree degree = CoverGeometry.ComputeCoverDegree(0, 0, 10, 0, wall);
            Assert.That(degree, Is.EqualTo(CoverDegree.Full));
        }

        [Test] // TC-PERSIST-122
        public void ComputeCoverDegree_OpenDoorOnThePath_IsNone_UsesBlocksVisionNotGeometry()
        {
            // Geometrically "on the path" (same position as the wall above), but an open door does not block vision.
            var openDoor = new[] { new ObstacleSegment(ObstacleKind.Door, true, 5, -5, 5, 5) };
            CoverDegree degree = CoverGeometry.ComputeCoverDegree(0, 0, 10, 0, openDoor);
            Assert.That(degree, Is.EqualTo(CoverDegree.None), "an open door must not contribute any cover, even though it geometrically sits on the attacker-target line -- the blocking predicate is BlocksVision, not raw geometry");
        }

        [Test] // TC-PERSIST-123
        public void ComputeCoverDegree_WindowOnThePath_IsNone()
        {
            var window = new[] { new ObstacleSegment(ObstacleKind.Window, null, 5, -5, 5, 5) };
            CoverDegree degree = CoverGeometry.ComputeCoverDegree(0, 0, 10, 0, window);
            Assert.That(degree, Is.EqualTo(CoverDegree.None));
        }

        [Test] // TC-PERSIST-124
        public void ComputeCoverDegree_ClosedDoorBlockingAllFourSamples_IsFull()
        {
            var closedDoor = new[] { new ObstacleSegment(ObstacleKind.Door, false, 5, -5, 5, 5) };
            CoverDegree degree = CoverGeometry.ComputeCoverDegree(0, 0, 10, 0, closedDoor);
            Assert.That(degree, Is.EqualTo(CoverDegree.Full));
        }

        [Test] // TC-PERSIST-125
        public void ComputeCoverDegree_BlockedSampleCountTable_MapsToTheCorrectDegree()
        {
            // Four short wall segments, each placed to block exactly one of the four diagonal
            // samples around the target at (10,0) -- added one at a time to walk through the full
            // 0/1/2/3/4-blocked-samples table in a single, explicit test.
            double offset = CoverGeometry.DefaultTargetRadius * 0.70710678118654752;
            var sampleNE = (X: 10 + offset, Y: 0 + offset);
            var sampleSE = (X: 10 + offset, Y: 0 - offset);
            var sampleNW = (X: 10 - offset, Y: 0 + offset);
            var sampleSW = (X: 10 - offset, Y: 0 - offset);

            var obstacles = new List<ObstacleSegment>();
            Assert.That(CoverGeometry.ComputeCoverDegree(0, 0, 10, 0, obstacles), Is.EqualTo(CoverDegree.None), "0 blocked");

            obstacles.Add(ShortWallThrough(sampleNE));
            Assert.That(CoverGeometry.ComputeCoverDegree(0, 0, 10, 0, obstacles), Is.EqualTo(CoverDegree.Half), "1 blocked");

            obstacles.Add(ShortWallThrough(sampleSE));
            Assert.That(CoverGeometry.ComputeCoverDegree(0, 0, 10, 0, obstacles), Is.EqualTo(CoverDegree.Half), "2 blocked");

            obstacles.Add(ShortWallThrough(sampleNW));
            Assert.That(CoverGeometry.ComputeCoverDegree(0, 0, 10, 0, obstacles), Is.EqualTo(CoverDegree.ThreeQuarters), "3 blocked");

            obstacles.Add(ShortWallThrough(sampleSW));
            Assert.That(CoverGeometry.ComputeCoverDegree(0, 0, 10, 0, obstacles), Is.EqualTo(CoverDegree.Full), "4 blocked");
        }

        // A short wall segment straddling the given point, perpendicular-ish to any reasonable attacker line, so the attacker(0,0)-to-sample segment reliably crosses it without also blocking the other three samples.
        private static ObstacleSegment ShortWallThrough((double X, double Y) point)
        {
            return new ObstacleSegment(ObstacleKind.Wall, null, point.X - 0.01, point.Y + 0.01, point.X + 0.01, point.Y - 0.01);
        }
    }
}
