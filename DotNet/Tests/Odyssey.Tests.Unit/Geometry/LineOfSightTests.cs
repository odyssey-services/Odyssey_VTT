using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Domain.Geometry;

namespace Odyssey.Tests.Unit.Geometry
{
    /// <summary>SLICE-10 Block 3: TC-PERSIST-087+ -- pure geometry, no repository/database involved.</summary>
    public sealed class LineOfSightTests
    {
        [Test] // TC-PERSIST-087
        public void HasClearLine_WallBetween_Blocks()
        {
            var obstacles = new[] { new ObstacleSegment(ObstacleKind.Wall, null, 5, -5, 5, 5) };
            Assert.That(LineOfSight.HasClearLine(0, 0, 10, 0, obstacles), Is.False);
        }

        [Test] // TC-PERSIST-088
        public void HasClearLine_OpenDoorBetween_DoesNotBlock()
        {
            var obstacles = new[] { new ObstacleSegment(ObstacleKind.Door, true, 5, -5, 5, 5) };
            Assert.That(LineOfSight.HasClearLine(0, 0, 10, 0, obstacles), Is.True);
        }

        [Test] // TC-PERSIST-089
        public void HasClearLine_ClosedDoorBetween_Blocks()
        {
            var obstacles = new[] { new ObstacleSegment(ObstacleKind.Door, false, 5, -5, 5, 5) };
            Assert.That(LineOfSight.HasClearLine(0, 0, 10, 0, obstacles), Is.False);
        }

        [Test] // TC-PERSIST-090
        public void HasClearLine_WindowBetween_DoesNotBlock()
        {
            var obstacles = new[] { new ObstacleSegment(ObstacleKind.Window, null, 5, -5, 5, 5) };
            Assert.That(LineOfSight.HasClearLine(0, 0, 10, 0, obstacles), Is.True);
        }

        [Test] // TC-PERSIST-091
        public void HasClearLine_NoObstacleOnPath_IsClear()
        {
            var obstacles = new[] { new ObstacleSegment(ObstacleKind.Wall, null, 50, 50, 60, 60) };
            Assert.That(LineOfSight.HasClearLine(0, 0, 10, 0, obstacles), Is.True);
        }

        [Test] // TC-PERSIST-092
        public void IsWithinFovCone_TargetDirectlyAhead_IsVisible()
        {
            // Observer at origin, facing due east (0 degrees); target due east.
            Assert.That(LineOfSight.IsWithinFovCone(0, 0, 0, 90, 10, 0), Is.True);
        }

        [Test] // TC-PERSIST-093
        public void IsWithinFovCone_OnTheHalfAngleBoundary_IsVisible()
        {
            // FOV 90 degrees, facing due east -- boundary is exactly 45 degrees off-axis.
            // Target at (10,10) from origin is at bearing 45 degrees.
            Assert.That(LineOfSight.IsWithinFovCone(0, 0, 0, 90, 10, 10), Is.True, "the boundary itself must count as visible, not strictly less-than");
        }

        [Test] // TC-PERSIST-094
        public void IsWithinFovCone_OutsideCone_IsNotVisible()
        {
            // FOV 90 degrees, facing due east; target due south (bearing -90/270) is well outside.
            Assert.That(LineOfSight.IsWithinFovCone(0, 0, 0, 90, 0, -10), Is.False);
        }

        [Test] // TC-PERSIST-095
        public void IsWithinFovCone_360Degrees_AlwaysVisible()
        {
            Assert.That(LineOfSight.IsWithinFovCone(0, 0, 0, 360, 0, -10), Is.True);
            Assert.That(LineOfSight.IsWithinFovCone(0, 0, 123, 360, -5, 17), Is.True);
        }

        [Test] // TC-PERSIST-096
        public void IsWithinFovCone_HandlesWraparoundAcrossZeroDegrees()
        {
            // Facing 350 degrees (nearly due north-ish, just under 0/360), FOV 30 degrees --
            // the cone spans [335, 365=5]. A target at bearing 5 degrees is inside the cone,
            // even though a naive |facing - bearing| = |350 - 5| = 345 would wrongly say "far away."
            double facingDegrees = 350;
            double targetX = System.Math.Cos(5.0 * System.Math.PI / 180.0) * 10;
            double targetY = System.Math.Sin(5.0 * System.Math.PI / 180.0) * 10;
            Assert.That(LineOfSight.IsWithinFovCone(0, 0, facingDegrees, 30, targetX, targetY), Is.True, "the shortest angular difference must be used, not a naive subtraction");
        }

        [Test] // TC-PERSIST-097
        public void IsWithinRange_OnTheBoundary_IsVisible()
        {
            Assert.That(LineOfSight.IsWithinRange(0, 0, 10, 0, 10), Is.True, "exactly at the range boundary must not be a false negative");
        }

        [Test] // TC-PERSIST-098
        public void IsWithinRange_BeyondRange_IsNotVisible()
        {
            Assert.That(LineOfSight.IsWithinRange(0, 0, 10.5, 0, 10), Is.False);
        }

        [Test] // TC-PERSIST-099
        public void CanSee_ComposesRangeConeAndObstacles()
        {
            var noObstacles = new List<ObstacleSegment>();
            // In range (5), in cone (facing east, 90 FOV, target due east), clear line.
            Assert.That(LineOfSight.CanSee(0, 0, 0, 90, 20, 5, 0, noObstacles), Is.True);

            // Out of range.
            Assert.That(LineOfSight.CanSee(0, 0, 0, 90, 3, 5, 0, noObstacles), Is.False);

            // Out of cone (facing north, target due east, narrow FOV).
            Assert.That(LineOfSight.CanSee(0, 0, 90, 10, 20, 5, 0, noObstacles), Is.False);

            // Blocked by a wall.
            var wall = new[] { new ObstacleSegment(ObstacleKind.Wall, null, 2, -5, 2, 5) };
            Assert.That(LineOfSight.CanSee(0, 0, 0, 90, 20, 5, 0, wall), Is.False);
        }

        [Test] // TC-PERSIST-100
        public void CanSee_NonFiniteInput_ReturnsFalse_DoesNotThrow()
        {
            var noObstacles = new List<ObstacleSegment>();
            Assert.That(LineOfSight.CanSee(double.NaN, 0, 0, 90, 20, 5, 0, noObstacles), Is.False);
            Assert.That(LineOfSight.CanSee(0, 0, 0, 90, 20, double.PositiveInfinity, 0, noObstacles), Is.False);
            Assert.That(LineOfSight.CanSee(0, 0, double.NaN, 90, 20, 5, 0, noObstacles), Is.False);
        }
    }
}
