using System;

namespace Odyssey.Unity.Client
{
    /// <summary>
    /// SLICE-10 Block 6 part 1: pure point-to-segment distance, a client-layer interactive hit-test
    /// primitive -- deliberately not <c>Odyssey.Domain</c> (no game rule reads this; it exists only so
    /// <see cref="BoardScreenPresenter"/> can tell whether a click landed "on" a thin obstacle line, the
    /// same role <see cref="BoardScreenPresenter"/>'s own private <c>HitTestToken</c> already plays for a
    /// token's square, not a re-derivation of anything Block 2/3/5's own <c>SegmentIntersection.cs</c>
    /// provides (that type answers "do two segments cross," never "how far is this point from one").
    /// Named distinctly from <c>Odyssey.Domain.Geometry.BoardGeometry</c> to avoid any confusion between
    /// the two -- this type is UI-only and holds no dependency on that one.
    /// </summary>
    public static class BoardHitTestMath
    {
        /// <summary>
        /// The shortest distance from point (<paramref name="px"/>, <paramref name="py"/>) to the segment
        /// from (<paramref name="x1"/>, <paramref name="y1"/>) to (<paramref name="x2"/>, <paramref name="y2"/>)
        /// -- the true segment, not the infinite line through it: a point beyond either endpoint is measured
        /// to that endpoint, not to its projection onto the line's own extension.
        /// </summary>
        public static double DistancePointToSegment(double px, double py, double x1, double y1, double x2, double y2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            double lengthSquared = dx * dx + dy * dy;

            if (lengthSquared < 1e-12)
            {
                // A degenerate, zero-length "segment" is just a point.
                double pointDx = px - x1;
                double pointDy = py - y1;
                return Math.Sqrt(pointDx * pointDx + pointDy * pointDy);
            }

            double t = ((px - x1) * dx + (py - y1) * dy) / lengthSquared;
            t = Math.Max(0.0, Math.Min(1.0, t));

            double closestX = x1 + t * dx;
            double closestY = y1 + t * dy;
            double distX = px - closestX;
            double distY = py - closestY;
            return Math.Sqrt(distX * distX + distY * distY);
        }
    }
}
