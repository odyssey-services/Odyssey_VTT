using System;
using System.Collections.Generic;

namespace Odyssey.Domain.Geometry
{
    /// <summary>One obstacle segment, as much of <c>Odyssey.Application.Persistence.ObstacleRecord</c> as <see cref="LineOfSight"/> needs -- a plain Domain-layer value, not a reference to the Application-layer record itself (ADR-001 section 5: Domain depends on nothing).</summary>
    public readonly struct ObstacleSegment
    {
        public ObstacleSegment(ObstacleKind kind, bool? isOpen, double x1, double y1, double x2, double y2)
        {
            Kind = kind;
            IsOpen = isOpen;
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
        }

        public ObstacleKind Kind { get; }
        public bool? IsOpen { get; }
        public double X1 { get; }
        public double Y1 { get; }
        public double X2 { get; }
        public double Y2 { get; }
    }

    /// <summary>
    /// SLICE-10 Block 3: pure line-of-sight geometry -- range, field-of-view cone, and obstacle
    /// occlusion -- kept in <c>Odyssey.Domain</c> (no dependency on any other module, ADR-001
    /// section 5) so the Application-layer <c>TokenVisionService</c> is a thin repository-reading
    /// shell over these functions, the same two-layer shape
    /// <c>SqliteAttackStateReader</c>/<c>CoreAttackRulesEvaluator</c> already established. No
    /// <c>SpatialIndexV1</c> (ADR-020 section 7) -- a linear scan over every obstacle on every call
    /// is the explicitly ADR-sanctioned MVP at this task's scale (up to ~200 tokens per scene).
    /// </summary>
    public static class LineOfSight
    {
        /// <summary>
        /// Is there a clear (unobstructed) line between the observer and the target? Only obstacles
        /// for which <see cref="ObstacleGeometry.BlocksVision"/> is true are even tested for
        /// intersection -- an open door or a window never occludes, regardless of its geometry. This
        /// calls <see cref="ObstacleGeometry.BlocksVision"/> directly rather than re-deriving the
        /// Block 2 blocking rule -- see the task contract section 18 for why that rule is not
        /// duplicated here.
        /// </summary>
        public static bool HasClearLine(double observerX, double observerY, double targetX, double targetY, IReadOnlyList<ObstacleSegment> obstacles)
        {
            if (obstacles == null) throw new ArgumentNullException(nameof(obstacles));

            for (int index = 0; index < obstacles.Count; index++)
            {
                ObstacleSegment obstacle = obstacles[index];
                if (!ObstacleGeometry.BlocksVision(obstacle.Kind, obstacle.IsOpen))
                {
                    continue;
                }

                if (SegmentIntersection.SegmentsIntersect(observerX, observerY, targetX, targetY, obstacle.X1, obstacle.Y1, obstacle.X2, obstacle.Y2))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Is the target within the observer's field-of-view cone, centered on <paramref name="observerFacingDegrees"/>,
        /// spanning <paramref name="fovAngleDegrees"/> total width (so <c>±fovAngleDegrees/2</c> either
        /// side of facing)? <paramref name="fovAngleDegrees"/> &gt;= 360 is always `true` (omnidirectional --
        /// no cone to be outside of). The boundary (exactly half the cone width away from facing) counts
        /// as visible (`&lt;=`, not `&lt;`), matching <see cref="IsWithinRange"/>'s own inclusive-boundary
        /// convention. Angle wraparound (facing near 0/360) is handled by taking the shortest angular
        /// difference, never a naive subtraction.
        /// </summary>
        public static bool IsWithinFovCone(double observerX, double observerY, double observerFacingDegrees, double fovAngleDegrees, double targetX, double targetY)
        {
            if (fovAngleDegrees >= 360.0)
            {
                return true;
            }

            if (BoardGeometry.SamePosition(observerX, observerY, targetX, targetY))
            {
                // A target exactly at the observer's own position has no well-defined bearing --
                // treat it as visible (it cannot be "outside a cone" from zero distance).
                return true;
            }

            double bearingToTarget = NormalizeDegrees(Math.Atan2(targetY - observerY, targetX - observerX) * (180.0 / Math.PI));
            double facing = NormalizeDegrees(observerFacingDegrees);
            double delta = Math.Abs(facing - bearingToTarget);
            double shortestDelta = delta > 180.0 ? 360.0 - delta : delta;

            return shortestDelta <= (fovAngleDegrees / 2.0) + BoardGeometry.GeometryEpsilonV1;
        }

        /// <summary>Is the target within <paramref name="viewDistance"/> world units of the observer? Boundary-inclusive (epsilon-tolerant), by exact precedent of <see cref="BoardGeometry.AlmostEqual"/> -- a target exactly at the edge of vision range must not be a false negative.</summary>
        public static bool IsWithinRange(double observerX, double observerY, double targetX, double targetY, double viewDistance)
        {
            double distance = BoardGeometry.EuclideanDistance(observerX, observerY, targetX, targetY);
            return distance <= viewDistance + BoardGeometry.GeometryEpsilonV1;
        }

        /// <summary>
        /// The full composition: in range AND within the field-of-view cone AND no blocking obstacle
        /// on the path. A pure function -- non-finite input coordinates return `false` rather than
        /// throwing (a caller-side validation failure, not a geometry computation this function
        /// should crash on).
        /// </summary>
        public static bool CanSee(double observerX, double observerY, double observerFacingDegrees, double fovAngleDegrees, double viewDistance, double targetX, double targetY, IReadOnlyList<ObstacleSegment> obstacles)
        {
            if (!BoardGeometry.IsFinite(observerX, observerY) || !BoardGeometry.IsFinite(targetX, targetY) ||
                !double.IsFinite(observerFacingDegrees) || !double.IsFinite(fovAngleDegrees) || !double.IsFinite(viewDistance))
            {
                return false;
            }

            if (!IsWithinRange(observerX, observerY, targetX, targetY, viewDistance))
            {
                return false;
            }

            if (!IsWithinFovCone(observerX, observerY, observerFacingDegrees, fovAngleDegrees, targetX, targetY))
            {
                return false;
            }

            return HasClearLine(observerX, observerY, targetX, targetY, obstacles);
        }

        private static double NormalizeDegrees(double degrees)
        {
            double normalized = degrees % 360.0;
            return normalized < 0 ? normalized + 360.0 : normalized;
        }
    }
}
