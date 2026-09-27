using System;
using NUnit.Framework;
using Odyssey.Unity.Client;

namespace Odyssey.Tests.Unity.EditMode
{
    /// <summary>
    /// ODY-S08-102: pure math tests for <see cref="BoardCamera"/> -- plain method calls with numbers, no
    /// simulated pointer/wheel events at all (task contract section 1.1's own testability requirement).
    /// </summary>
    public sealed class BoardCameraTests
    {
        [Test] // TC-BOARD-051
        public void ToPixels_MatchesTheOldFixedTransform_BeforeAnyPanOrZoom()
        {
            var camera = new BoardCamera();

            // The old fixed transform: pixel = 220 + world * 40 (before the token-centering subtraction the
            // presenter itself applies). A fresh camera must reproduce it exactly, so a freshly opened board
            // looks the same as it always did.
            Assert.That(camera.ToPixelsX(0), Is.EqualTo(220.0).Within(1e-9));
            Assert.That(camera.ToPixelsY(0), Is.EqualTo(220.0).Within(1e-9));
            Assert.That(camera.ToPixelsX(3), Is.EqualTo(220.0 + 3 * 40.0).Within(1e-9));
            Assert.That(camera.ToPixelsY(-2), Is.EqualTo(220.0 - 2 * 40.0).Within(1e-9));
            Assert.That(camera.Scale, Is.EqualTo(40.0));
        }

        [Test] // TC-BOARD-052
        public void ToPixels_AndFromPixels_RoundTrip_AtVariousOffsetsAndScales()
        {
            var camera = new BoardCamera();
            camera.Pan(deltaPixelX: 137.0, deltaPixelY: -64.0);
            camera.Zoom(factor: 2.3, anchorPixelX: 40.0, anchorPixelY: 300.0);

            foreach ((double worldX, double worldY) in new[] { (0.0, 0.0), (5.5, -3.25), (-100.0, 250.0), (0.001, -0.001) })
            {
                double pixelX = camera.ToPixelsX(worldX);
                double pixelY = camera.ToPixelsY(worldY);
                double roundTrippedX = camera.FromPixelsX(pixelX);
                double roundTrippedY = camera.FromPixelsY(pixelY);

                Assert.That(roundTrippedX, Is.EqualTo(worldX).Within(1e-9), "FromPixels(ToPixels(worldX)) must recover worldX");
                Assert.That(roundTrippedY, Is.EqualTo(worldY).Within(1e-9), "FromPixels(ToPixels(worldY)) must recover worldY");
            }
        }

        [Test] // TC-BOARD-053
        public void Pan_ShiftsTheCameraSoAWorldPointFollowsTheDrag_ByExactlyTheDraggedPixelAmount()
        {
            var camera = new BoardCamera();
            double worldX = 4.0;
            double worldY = -1.5;
            double pixelXBefore = camera.ToPixelsX(worldX);
            double pixelYBefore = camera.ToPixelsY(worldY);

            camera.Pan(deltaPixelX: 25.0, deltaPixelY: -10.0);

            double pixelXAfter = camera.ToPixelsX(worldX);
            double pixelYAfter = camera.ToPixelsY(worldY);
            Assert.That(pixelXAfter - pixelXBefore, Is.EqualTo(25.0).Within(1e-9), "a fixed world point must move by exactly the dragged pixel delta (content follows the cursor)");
            Assert.That(pixelYAfter - pixelYBefore, Is.EqualTo(-10.0).Within(1e-9));
            Assert.That(camera.Scale, Is.EqualTo(40.0), "panning must never change the scale");
        }

        [Test] // TC-BOARD-054
        public void Zoom_KeepsTheExactPixelUnderTheAnchorFixed_NotTheOrigin()
        {
            var camera = new BoardCamera();
            const double anchorPixelX = 310.0;
            const double anchorPixelY = 90.0;
            double anchorWorldXBefore = camera.FromPixelsX(anchorPixelX);
            double anchorWorldYBefore = camera.FromPixelsY(anchorPixelY);

            camera.Zoom(factor: 1.5, anchorPixelX, anchorPixelY);

            Assert.That(camera.Scale, Is.EqualTo(60.0).Within(1e-9));
            double anchorPixelXAfter = camera.ToPixelsX(anchorWorldXBefore);
            double anchorPixelYAfter = camera.ToPixelsY(anchorWorldYBefore);
            Assert.That(anchorPixelXAfter, Is.EqualTo(anchorPixelX).Within(1e-9), "the world point under the cursor must stay under the exact same pixel after zooming");
            Assert.That(anchorPixelYAfter, Is.EqualTo(anchorPixelY).Within(1e-9));

            // The board's origin (pixel 0,0 before the zoom) is a different point from the anchor, and a
            // cursor-anchored zoom does not keep it fixed -- only the anchor itself stays under the cursor.
            double originWorldBefore = new BoardCamera().FromPixelsX(0);
            Assert.That(originWorldBefore, Is.Not.EqualTo(anchorWorldXBefore).Within(1e-9), "sanity: the anchor used in this test is not the origin");
            double originPixelXAfter = camera.ToPixelsX(originWorldBefore);
            Assert.That(originPixelXAfter, Is.Not.EqualTo(0.0).Within(1e-6), "a cursor-anchored zoom is expected to move the pixel position of points other than the anchor");
        }

        [Test] // TC-BOARD-055
        public void Zoom_RepeatedBeyondTheBounds_ClampsToMinAndMaxScale_NeverDegenerate()
        {
            var zoomedOut = new BoardCamera();
            for (int i = 0; i < 200; i++) zoomedOut.Zoom(factor: 0.5, anchorPixelX: 0, anchorPixelY: 0);
            Assert.That(zoomedOut.Scale, Is.EqualTo(BoardCamera.MinScale).Within(1e-9));
            Assert.That(zoomedOut.Scale, Is.GreaterThan(0.0), "scale must never reach zero or negative -- the board must never collapse to a point");

            var zoomedIn = new BoardCamera();
            for (int i = 0; i < 200; i++) zoomedIn.Zoom(factor: 2.0, anchorPixelX: 0, anchorPixelY: 0);
            Assert.That(zoomedIn.Scale, Is.EqualTo(BoardCamera.MaxScale).Within(1e-9));

            // Even at the clamped extremes, the transform stays a well-behaved, invertible mapping.
            Assert.That(zoomedOut.FromPixelsX(zoomedOut.ToPixelsX(7.0)), Is.EqualTo(7.0).Within(1e-6));
            Assert.That(zoomedIn.FromPixelsX(zoomedIn.ToPixelsX(7.0)), Is.EqualTo(7.0).Within(1e-6));
        }

        [Test] // TC-BOARD-056
        public void Zoom_WithANonPositiveFactor_Throws()
        {
            var camera = new BoardCamera();
            Assert.Throws<ArgumentOutOfRangeException>(() => camera.Zoom(0.0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => camera.Zoom(-1.0, 0, 0));
        }
    }
}
