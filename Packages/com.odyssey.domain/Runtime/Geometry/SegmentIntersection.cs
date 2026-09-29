using System;

namespace Odyssey.Domain.Geometry
{
    /// <summary>ADR-020 section 6.2: the classification an epsilon-tolerant orientation test produces.</summary>
    public enum OrientationResult
    {
        Left,
        Right,
        Collinear,
    }

    /// <summary>
    /// SLICE-10 Block 3: the one project-wide implementation of ADR-020 section 6.2's orientation
    /// primitive and the segment-intersection test built on it, kept in <c>Odyssey.Domain</c> (no
    /// dependency on any other module, ADR-001 section 5) next to <see cref="BoardGeometry"/>/
    /// <see cref="ObstacleGeometry"/> so every future geometry-service consumer (line-of-sight now,
    /// cover in a later block) shares this one primitive rather than a per-service epsilon logic
    /// (ADR-020 section 6.2's own explicit instruction).
    ///
    /// ADR-020 leaves the exact intersection algorithm unspecified (only the orientation primitive
    /// and the fail-closed boundary rule, section 6.3) -- <see cref="SegmentsIntersect"/> is this
    /// task's own original engineering work: the standard four-orientation-test construction, with
    /// the collinear special cases resolved by an epsilon-tolerant bounding-box containment check
    /// (<c>OnSegment</c>) rather than a blanket "any Collinear result blocks" rule -- two segments
    /// that are collinear but unambiguously far apart on the same infinite line must still return
    /// `false` (see the task contract section 9's own truth table); fail-closed governs only the
    /// genuinely ambiguous boundary/touching case, exactly as ADR-020 section 6.3 scopes it
    /// ("Collinear/touching" -- not "collinear at any distance").
    /// </summary>
    public static class SegmentIntersection
    {
        /// <summary>
        /// ADR-020 section 6.2's exact formula: the signed area (2D cross-product) of triangle
        /// P-Q-R. Classified <see cref="OrientationResult.Collinear"/> when
        /// <c>abs(orientation) &lt; GeometryEpsilonV1 * scaleFactor</c>, where <paramref name="scaleFactor"/>
        /// -- left unspecified by the ADR beyond "a normalizing multiplier by the magnitude of the
        /// input coordinates" -- is computed here as the product of the two triangle-edge lengths
        /// from P (<c>|PQ| * |PR|</c>, each floored at 1 world unit): dividing the raw cross-product
        /// area by that product is proportional to the sine of the angle at P, which is scale-
        /// invariant (avoids false-Collinear for geometrically distant points, per the ADR's own
        /// stated goal) and the 1-unit floor keeps the effective tolerance from shrinking below
        /// <see cref="BoardGeometry.GeometryEpsilonV1"/> for very short segments (the ADR's other
        /// stated goal -- not false-classifying a short segment as non-Collinear).
        /// </summary>
        public static OrientationResult Orientation(double px, double py, double qx, double qy, double rx, double ry)
        {
            double cross = ((qx - px) * (ry - py)) - ((qy - py) * (rx - px));
            double pq = BoardGeometry.EuclideanDistance(px, py, qx, qy);
            double pr = BoardGeometry.EuclideanDistance(px, py, rx, ry);
            double scaleFactor = Math.Max(1.0, pq) * Math.Max(1.0, pr);

            if (Math.Abs(cross) < BoardGeometry.GeometryEpsilonV1 * scaleFactor)
            {
                return OrientationResult.Collinear;
            }

            return cross > 0 ? OrientationResult.Left : OrientationResult.Right;
        }

        /// <summary>
        /// Does segment AB intersect segment CD? The standard four-orientation-test construction;
        /// see the class remarks for how the collinear special cases are resolved. ADR-020 section
        /// 6.3's fail-closed rule applies at the boundary-touching case (an endpoint of one segment
        /// lying exactly on the other, within the epsilon-tolerant <c>OnSegment</c> check below) --
        /// that case returns <c>true</c> (blocking), never a coin-flip `false`.
        /// </summary>
        public static bool SegmentsIntersect(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy)
        {
            OrientationResult o1 = Orientation(ax, ay, bx, by, cx, cy);
            OrientationResult o2 = Orientation(ax, ay, bx, by, dx, dy);
            OrientationResult o3 = Orientation(cx, cy, dx, dy, ax, ay);
            OrientationResult o4 = Orientation(cx, cy, dx, dy, bx, by);

            // General case: C and D fall on opposite sides of AB, and A and B fall on opposite
            // sides of CD -- a proper crossing, none of the four tests ambiguous.
            if (o1 != OrientationResult.Collinear && o2 != OrientationResult.Collinear &&
                o3 != OrientationResult.Collinear && o4 != OrientationResult.Collinear &&
                o1 != o2 && o3 != o4)
            {
                return true;
            }

            // Collinear special cases -- fail closed (ADR-020 section 6.3) only for the genuinely
            // ambiguous touching/overlapping sub-case, decided by an epsilon-tolerant bounding-box
            // containment test, not a blanket "any Collinear anywhere blocks."
            if (o1 == OrientationResult.Collinear && OnSegment(ax, ay, bx, by, cx, cy)) return true;
            if (o2 == OrientationResult.Collinear && OnSegment(ax, ay, bx, by, dx, dy)) return true;
            if (o3 == OrientationResult.Collinear && OnSegment(cx, cy, dx, dy, ax, ay)) return true;
            if (o4 == OrientationResult.Collinear && OnSegment(cx, cy, dx, dy, bx, by)) return true;

            return false;
        }

        /// <summary>Given that R is already known (epsilon-tolerant) collinear with segment P-Q, is R also within P-Q's own bounding box -- i.e. does R lie on the segment itself, not merely on the infinite line through it? Epsilon-tolerant at the boundary, consistent with the fail-closed convention this whole primitive follows.</summary>
        private static bool OnSegment(double px, double py, double qx, double qy, double rx, double ry)
        {
            double eps = BoardGeometry.GeometryEpsilonV1;
            return rx <= Math.Max(px, qx) + eps && rx >= Math.Min(px, qx) - eps &&
                   ry <= Math.Max(py, qy) + eps && ry >= Math.Min(py, qy) - eps;
        }
    }
}
