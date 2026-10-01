using NUnit.Framework;
using Odyssey.Application.Persistence;
using Odyssey.Unity.Client;

namespace Odyssey.Tests.Unity.EditMode
{
    /// <summary>
    /// ODY-S11-214 (polish P0 item 5): the camera move to the acting participant, stepped with explicit milliseconds.
    /// Whether the move feels right and gives way to the user's own pan/zoom is a manual check.
    /// </summary>
    public sealed class BoardCameraFocusTests
    {
        private const double Width = 800;
        private const double Height = 600;

        [Test]
        public void InFrame_MeansInsideTheViewMinusTheInsetOnEachSide()
        {
            var camera = new BoardCamera(0, 0, 40);
            Assert.That(BoardCameraFocus.IsInFrame(camera, new TokenPosition(10, 7.5), Width, Height), Is.True, "center");
            Assert.That(BoardCameraFocus.IsInFrame(camera, new TokenPosition(1, 7.5), Width, Height), Is.False, "40px from the left edge is inside the 15% inset");
            Assert.That(BoardCameraFocus.IsInFrame(camera, new TokenPosition(100, 7.5), Width, Height), Is.False, "far off screen");
        }

        [Test]
        public void Focus_EasesTheCameraUntilTheTokenIsCentered_WithoutChangingZoom()
        {
            var camera = new BoardCamera(0, 0, 40);
            var target = new TokenPosition(100, -50);
            var focus = new BoardCameraFocus();
            focus.Start(camera, target, Width, Height);
            Assert.That(focus.IsActive, Is.True);

            Assert.That(focus.Advance(camera, BoardCameraFocus.DurationMs / 2), Is.True);
            double midX = camera.ToPixelsX(target.X);
            Assert.That(midX, Is.GreaterThan(Width / 2).And.LessThan((100 - 0) * 40.0), "on its way");

            Assert.That(focus.Advance(camera, BoardCameraFocus.DurationMs), Is.False);
            Assert.That(focus.IsActive, Is.False);
            Assert.That(camera.ToPixelsX(target.X), Is.EqualTo(Width / 2).Within(1e-6), "centered horizontally");
            Assert.That(camera.ToPixelsY(target.Y), Is.EqualTo(Height / 2).Within(1e-6), "centered vertically");
            Assert.That(camera.Scale, Is.EqualTo(40), "zoom is never changed");
        }

        [Test]
        public void Cancel_StopsTheMoveWhereItIs()
        {
            var camera = new BoardCamera(0, 0, 40);
            var focus = new BoardCameraFocus();
            focus.Start(camera, new TokenPosition(100, 0), Width, Height);
            focus.Advance(camera, BoardCameraFocus.DurationMs / 3);
            double offsetAtCancel = camera.OffsetX;

            focus.Cancel();
            Assert.That(focus.IsActive, Is.False);
            Assert.That(focus.Advance(camera, BoardCameraFocus.DurationMs), Is.False);
            Assert.That(camera.OffsetX, Is.EqualTo(offsetAtCancel), "after a cancel the camera belongs to the user");
        }
    }
}
