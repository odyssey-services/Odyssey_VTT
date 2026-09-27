using System;

namespace Odyssey.Unity.Client
{
    /// <summary>
    /// ODY-S08-102 (SLICE-08 block 2): the board's world&lt;-&gt;pixel transform -- pan and zoom-to-cursor.
    /// Deliberately holds no <c>UnityEngine</c>/UI Toolkit reference at all, so it is a plain, directly
    /// testable C# type: a caller feeds it plain <c>double</c> pixel deltas and anchor points computed from
    /// whatever input system it lives behind, and gets plain <c>double</c> coordinates back. This mirrors
    /// how <see cref="BoardScreenPresenter"/> already keeps its selection/move logic (<c>SelectToken</c>/
    /// <c>TryMoveSelectedTokenTo</c>) directly callable and testable without simulating real UI Toolkit
    /// events; <see cref="BoardScreenPresenter"/> is this type's only caller.
    ///
    /// The transform is <c>pixel = (world - Offset) * Scale</c>. Before this task the board used a fixed
    /// transform (<c>OriginOffsetPixels</c> = 220, <c>PixelsPerUnit</c> = 40, both <c>const</c>); this type
    /// replaces those constants with the same shape of transform, now mutable through <see cref="Pan"/> and
    /// <see cref="Zoom"/>.
    /// </summary>
    public sealed class BoardCamera
    {
        /// <summary>The fixed scale (pixels per world unit) <see cref="BoardScreenPresenter"/> used before this task -- the default starting zoom level.</summary>
        public const double DefaultScale = 40.0;

        /// <summary>
        /// Lower bound on <see cref="Scale"/> (pixels per world unit). At this floor one screen pixel is
        /// 0.2 world units -- comfortably below "hundreds of world units per pixel," which would make
        /// panning and clicking unusably imprecise -- while still letting the view zoom out to an eighth of
        /// the default scale. A scale allowed to reach (or approach) zero would collapse the whole board
        /// into a single point, which this floor also rules out.
        /// </summary>
        public const double MinScale = 5.0;

        /// <summary>
        /// Upper bound on <see cref="Scale"/>. Ten times <see cref="DefaultScale"/>: enough to zoom in
        /// meaningfully on a small area without <see cref="double"/> precision or float pixel values
        /// (UI Toolkit styles are <c>float</c>) becoming a practical concern.
        /// </summary>
        public const double MaxScale = 400.0;

        private double _offsetX;
        private double _offsetY;
        private double _scale;

        /// <summary>
        /// Constructs a camera. The default offset places world (0,0) at pixel (220, 220) -- the exact
        /// pixel position the old fixed <c>OriginOffsetPixels</c> transform placed it at -- so a freshly
        /// opened board looks the same as it did before this task, before any pan or zoom.
        /// </summary>
        public BoardCamera(double offsetX = -220.0 / DefaultScale, double offsetY = -220.0 / DefaultScale, double scale = DefaultScale)
        {
            _offsetX = offsetX;
            _offsetY = offsetY;
            _scale = Clamp(scale);
        }

        /// <summary>The world X coordinate currently mapped to pixel 0.</summary>
        public double OffsetX => _offsetX;

        /// <summary>The world Y coordinate currently mapped to pixel 0.</summary>
        public double OffsetY => _offsetY;

        /// <summary>Current scale, in pixels per world unit. Always within [<see cref="MinScale"/>, <see cref="MaxScale"/>].</summary>
        public double Scale => _scale;

        public double ToPixelsX(double worldX) => (worldX - _offsetX) * _scale;

        public double ToPixelsY(double worldY) => (worldY - _offsetY) * _scale;

        public double FromPixelsX(double pixelX) => pixelX / _scale + _offsetX;

        public double FromPixelsY(double pixelY) => pixelY / _scale + _offsetY;

        /// <summary>
        /// Shifts the camera by a screen-pixel drag of (<paramref name="deltaPixelX"/>, <paramref name="deltaPixelY"/>):
        /// the world point that was under the cursor before the drag is under the cursor again afterwards
        /// (the familiar "grab and drag" feel -- content follows the cursor, camera moves the opposite way).
        /// No bound is applied to the resulting offset: the board is an infinite canvas by design (task
        /// contract section 1.4), so panning is never clamped.
        /// </summary>
        public void Pan(double deltaPixelX, double deltaPixelY)
        {
            _offsetX -= deltaPixelX / _scale;
            _offsetY -= deltaPixelY / _scale;
        }

        /// <summary>
        /// Multiplies <see cref="Scale"/> by <paramref name="factor"/> (clamped to
        /// [<see cref="MinScale"/>, <see cref="MaxScale"/>] afterwards), then adjusts the offset so the
        /// world point that was under (<paramref name="anchorPixelX"/>, <paramref name="anchorPixelY"/>)
        /// before the call is still under that exact pixel position afterwards -- "zoom to cursor," the
        /// same convention most infinite-canvas tools (e.g. Google Maps) use, and not the board's origin
        /// or center.
        /// </summary>
        public void Zoom(double factor, double anchorPixelX, double anchorPixelY)
        {
            if (!(factor > 0.0)) throw new ArgumentOutOfRangeException(nameof(factor), "Zoom factor must be positive.");

            double anchorWorldX = FromPixelsX(anchorPixelX);
            double anchorWorldY = FromPixelsY(anchorPixelY);

            _scale = Clamp(_scale * factor);

            _offsetX = anchorWorldX - anchorPixelX / _scale;
            _offsetY = anchorWorldY - anchorPixelY / _scale;
        }

        private static double Clamp(double scale) => Math.Max(MinScale, Math.Min(MaxScale, scale));
    }
}
