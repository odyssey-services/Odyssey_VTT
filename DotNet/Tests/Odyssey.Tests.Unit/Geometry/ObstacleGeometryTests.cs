using NUnit.Framework;
using Odyssey.Domain.Geometry;

namespace Odyssey.Tests.Unit.Geometry
{
    /// <summary>SLICE-10 Block 2: TC-PERSIST-070's own truth table for every Kind x IsOpen combination -- pure functions, no repository/database involved.</summary>
    public sealed class ObstacleGeometryTests
    {
        [Test]
        public void Wall_AlwaysBlocksVisionAndMovement_RegardlessOfIsOpen()
        {
            Assert.That(ObstacleGeometry.BlocksVision(ObstacleKind.Wall, null), Is.True);
            Assert.That(ObstacleGeometry.BlocksMovement(ObstacleKind.Wall, null), Is.True);
            Assert.That(ObstacleGeometry.BlocksVision(ObstacleKind.Wall, true), Is.True, "IsOpen is not meaningful for a Wall -- it must not change the result");
            Assert.That(ObstacleGeometry.BlocksMovement(ObstacleKind.Wall, false), Is.True);
        }

        [Test]
        public void Window_NeverBlocksVision_AlwaysBlocksMovement_RegardlessOfIsOpen()
        {
            Assert.That(ObstacleGeometry.BlocksVision(ObstacleKind.Window, null), Is.False);
            Assert.That(ObstacleGeometry.BlocksMovement(ObstacleKind.Window, null), Is.True);
            Assert.That(ObstacleGeometry.BlocksVision(ObstacleKind.Window, true), Is.False, "windows do not toggle in this task -- IsOpen must not change the result");
            Assert.That(ObstacleGeometry.BlocksMovement(ObstacleKind.Window, false), Is.True);
        }

        [Test]
        public void Door_Closed_BlocksBothVisionAndMovement()
        {
            Assert.That(ObstacleGeometry.BlocksVision(ObstacleKind.Door, false), Is.True);
            Assert.That(ObstacleGeometry.BlocksMovement(ObstacleKind.Door, false), Is.True);
        }

        [Test]
        public void Door_Open_BlocksNeitherVisionNorMovement()
        {
            Assert.That(ObstacleGeometry.BlocksVision(ObstacleKind.Door, true), Is.False);
            Assert.That(ObstacleGeometry.BlocksMovement(ObstacleKind.Door, true), Is.False);
        }

        [Test]
        public void Door_NullIsOpen_FailsClosed_TreatedAsBlocking()
        {
            // Defensive: production code always sets IsOpen=false at creation for a Door (never null),
            // but the pure function itself must not silently pass an unexpected null through as "open".
            Assert.That(ObstacleGeometry.BlocksVision(ObstacleKind.Door, null), Is.True);
            Assert.That(ObstacleGeometry.BlocksMovement(ObstacleKind.Door, null), Is.True);
        }
    }
}
