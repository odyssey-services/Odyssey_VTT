using NUnit.Framework;
using Odyssey.Unity.Client;

namespace Odyssey.Tests.Unity.EditMode
{
    /// <summary>
    /// ODY-S08-102: pure logic tests for <see cref="BoardPointerGesture"/> -- plain method calls with
    /// numbers, no simulated pointer events (task contract section 1.1/1.2's own testability requirement).
    /// </summary>
    public sealed class BoardPointerGestureTests
    {
        [Test] // TC-BOARD-057
        public void MovementBelowTheThreshold_IsStillAClick_NoDragReported()
        {
            var gesture = new BoardPointerGesture();
            gesture.Begin(100.0, 100.0);

            bool draggedDuringMove = gesture.Move(102.0, 101.0, out double deltaX, out double deltaY);

            Assert.That(draggedDuringMove, Is.False, "movement under the threshold must never be reported as a drag");
            Assert.That(deltaX, Is.EqualTo(0.0));
            Assert.That(deltaY, Is.EqualTo(0.0));
            Assert.That(gesture.IsDragging, Is.False);
            Assert.That(gesture.End(), Is.True, "a gesture that never crossed the threshold must end as a click");
        }

        [Test] // TC-BOARD-058
        public void MovementAtOrAboveTheThreshold_BecomesADrag_AndReportsPixelDeltasFromThen()
        {
            var gesture = new BoardPointerGesture();
            gesture.Begin(100.0, 100.0);

            // Exactly at the threshold (5px straight-line distance) must already count as a drag.
            bool firstMove = gesture.Move(105.0, 100.0, out double firstDeltaX, out double firstDeltaY);

            Assert.That(firstMove, Is.True);
            Assert.That(gesture.IsDragging, Is.True);
            Assert.That(firstDeltaX, Is.EqualTo(5.0));
            Assert.That(firstDeltaY, Is.EqualTo(0.0));

            bool secondMove = gesture.Move(108.0, 96.0, out double secondDeltaX, out double secondDeltaY);
            Assert.That(secondMove, Is.True);
            Assert.That(secondDeltaX, Is.EqualTo(3.0), "once dragging, each delta is relative to the PREVIOUS move, not the down-point");
            Assert.That(secondDeltaY, Is.EqualTo(-4.0));

            Assert.That(gesture.End(), Is.False, "a gesture that crossed the threshold must not end as a click");
        }

        [Test] // TC-BOARD-059
        public void OnceDragging_TheGestureStaysADrag_EvenIfThePointerReturnsCloseToTheDownPoint()
        {
            var gesture = new BoardPointerGesture();
            gesture.Begin(0.0, 0.0);
            Assert.That(gesture.Move(50.0, 0.0, out _, out _), Is.True);

            bool returnedNearOrigin = gesture.Move(1.0, 0.0, out double deltaX, out double deltaY);

            Assert.That(gesture.IsDragging, Is.True, "a drag must not un-become a click just because the pointer moved back near the down-point");
            Assert.That(returnedNearOrigin, Is.True);
            Assert.That(deltaX, Is.EqualTo(-49.0));
            Assert.That(gesture.End(), Is.False);
        }

        [Test] // TC-BOARD-060
        public void Cancel_DiscardsTheGesture_NeitherAClickNorADrag()
        {
            var gesture = new BoardPointerGesture();
            gesture.Begin(0.0, 0.0);
            gesture.Move(50.0, 50.0, out _, out _);

            gesture.Cancel();

            Assert.That(gesture.IsDragging, Is.False);
            Assert.That(gesture.End(), Is.False, "a cancelled gesture must not be reported as a click either");
        }

        [Test] // TC-BOARD-061
        public void MoveOrEnd_WithNoActiveGesture_IsANoOp()
        {
            var gesture = new BoardPointerGesture();

            Assert.That(gesture.Move(10.0, 10.0, out double deltaX, out double deltaY), Is.False);
            Assert.That(deltaX, Is.EqualTo(0.0));
            Assert.That(deltaY, Is.EqualTo(0.0));
            Assert.That(gesture.End(), Is.False);
        }
    }
}
