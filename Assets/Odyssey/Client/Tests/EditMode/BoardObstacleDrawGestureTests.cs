using NUnit.Framework;
using Odyssey.Unity.Client;

namespace Odyssey.Tests.Unity.EditMode
{
    /// <summary>SLICE-10 Block 6 part 1: pure logic tests for <see cref="BoardObstacleDrawGesture"/> -- plain method calls with numbers, no simulated pointer events.</summary>
    public sealed class BoardObstacleDrawGestureTests
    {
        [Test] // TC-BOARD-108
        public void CapturesTheSegmentFromTheStartToTheCurrentPointerPosition()
        {
            var gesture = new BoardObstacleDrawGesture();
            gesture.Begin(10.0, 20.0);

            Assert.That(gesture.TryGetSegment(out double sx, out double sy, out double cx, out double cy), Is.True);
            Assert.That(sx, Is.EqualTo(10.0));
            Assert.That(sy, Is.EqualTo(20.0));
            Assert.That(cx, Is.EqualTo(10.0));
            Assert.That(cy, Is.EqualTo(20.0));

            gesture.Move(50.0, 60.0);

            Assert.That(gesture.TryGetSegment(out sx, out sy, out cx, out cy), Is.True);
            Assert.That(sx, Is.EqualTo(10.0), "the start point must not move once the gesture has begun");
            Assert.That(sy, Is.EqualTo(20.0));
            Assert.That(cx, Is.EqualTo(50.0));
            Assert.That(cy, Is.EqualTo(60.0));
        }

        [Test] // TC-BOARD-109
        public void End_BelowTheDragThreshold_ReportsNoCommit_AnAccidentalClick()
        {
            var gesture = new BoardObstacleDrawGesture();
            gesture.Begin(100.0, 100.0);
            gesture.Move(102.0, 101.0);

            bool committed = gesture.End(out double sx, out double sy, out double ex, out double ey);

            Assert.That(committed, Is.False, "movement under the threshold must not create an obstacle");
            Assert.That(sx, Is.EqualTo(100.0));
            Assert.That(ex, Is.EqualTo(102.0));
        }

        [Test] // TC-BOARD-110
        public void End_AtOrAboveTheDragThreshold_ReportsACommit_WithTheFinalSegment()
        {
            var gesture = new BoardObstacleDrawGesture();
            gesture.Begin(0.0, 0.0);
            gesture.Move(30.0, 40.0);

            bool committed = gesture.End(out double sx, out double sy, out double ex, out double ey);

            Assert.That(committed, Is.True);
            Assert.That(sx, Is.EqualTo(0.0));
            Assert.That(sy, Is.EqualTo(0.0));
            Assert.That(ex, Is.EqualTo(30.0));
            Assert.That(ey, Is.EqualTo(40.0));
            Assert.That(gesture.IsActive, Is.False, "End must always finish the gesture, committed or not");
        }

        [Test] // TC-BOARD-111
        public void Cancel_DiscardsTheGesture_NeitherEndNorAnyFurtherMoveDoesAnything()
        {
            var gesture = new BoardObstacleDrawGesture();
            gesture.Begin(0.0, 0.0);
            gesture.Move(50.0, 50.0);

            gesture.Cancel();

            Assert.That(gesture.IsActive, Is.False);
            Assert.That(gesture.TryGetSegment(out _, out _, out _, out _), Is.False);
            Assert.That(gesture.End(out _, out _, out _, out _), Is.False, "a cancelled gesture must never report a commit");
        }

        [Test] // TC-BOARD-112
        public void MoveOrEnd_WithNoActiveGesture_IsANoOp()
        {
            var gesture = new BoardObstacleDrawGesture();

            gesture.Move(10.0, 10.0);

            Assert.That(gesture.TryGetSegment(out _, out _, out _, out _), Is.False);
            Assert.That(gesture.End(out _, out _, out _, out _), Is.False);
        }
    }
}
