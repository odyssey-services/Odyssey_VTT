using System;

namespace Odyssey.Domain.Geometry
{
    /// <summary>
    /// ODY-S03-004: the deterministic geometry primitives <c>ADR-020</c> fixes,
    /// scoped to the <c>GridType=None</c> case (ADR-020 section 5.3) -- this is
    /// the only case this task's vertical slice exercises, since grid/hex
    /// rendering and grid-coordinate snapping are explicitly out of scope
    /// (SLICE-03_IMPLEMENTATION_BACKLOG.md section 2.1). Square/Hex distance
    /// metrics and grid-coordinate conversion (ADR-020 sections 4.3, 5.1-5.2)
    /// remain for a future task that actually needs a grid.
    ///
    /// Kept in Odyssey.Domain (no dependency on any other module, ADR-001
    /// section 5) since it is pure math over <c>double</c> values -- ADR-020
    /// section 9 requires this Core geometry stay free of any UnityEngine/Unity
    /// Physics dependency; Domain is the natural home, the same way
    /// Odyssey.Domain.Time/Identity already hold pure value types.
    /// </summary>
    public static class BoardGeometry
    {
        /// <summary>
        /// ADR-020 section 6.1: versioned epsilon constant, in world units
        /// (meters). Changing this value requires a new <c>GeometryEpsilonV2</c>
        /// and an ADR-020 amendment, not a silent edit here.
        /// </summary>
        public const double GeometryEpsilonV1 = 1e-6;

        /// <summary>
        /// ADR-020 section 4.2/06_Scenes_And_Board section 6.1: authoritative
        /// positions are finite-only; NaN and Infinity are rejected.
        /// </summary>
        public static bool IsFinite(double x, double y) => double.IsFinite(x) && double.IsFinite(y);

        /// <summary>
        /// ADR-020 section 5.1/5.3: <c>Euclidean</c> -- the Square-grid default
        /// and the only formula <c>GridType=None</c> uses -- direct world
        /// distance, no grid-coordinate conversion. Fixed operation order
        /// (ADR-020 section 4.2): subtract, square, sum, then one <see
        /// cref="Math.Sqrt(double)"/> call, identical on every compilation
        /// target since both operate on IEEE-754 <c>double</c>.
        /// </summary>
        public static double EuclideanDistance(double ax, double ay, double bx, double by)
        {
            double dx = bx - ax;
            double dy = by - ay;
            return Math.Sqrt((dx * dx) + (dy * dy));
        }

        /// <summary>
        /// ADR-020 section 6.2's epsilon-tolerant comparison primitive, applied
        /// to a single scalar difference (the two-coordinate form used for
        /// occupancy/"same position" checks composes this per axis, see
        /// <see cref="SamePosition"/>).
        /// </summary>
        public static bool AlmostEqual(double a, double b, double epsilon = GeometryEpsilonV1) => Math.Abs(a - b) < epsilon;

        /// <summary>
        /// BOARD-INV-009 (08_Scenes_And_Board section 3): two tokens may not
        /// occupy the same position. Without a footprint/grid-cell model yet
        /// (deferred, section 2.1 of the originating backlog), "occupies the
        /// same position" is interpreted as epsilon-equal world coordinates on
        /// both axes -- the minimal-viable reading of the invariant available
        /// before a footprint/cell model exists.
        /// </summary>
        public static bool SamePosition(double ax, double ay, double bx, double by, double epsilon = GeometryEpsilonV1) =>
            AlmostEqual(ax, bx, epsilon) && AlmostEqual(ay, by, epsilon);
    }
}
