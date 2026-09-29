using System;
using System.Collections.Generic;

namespace Odyssey.Domain.Geometry
{
    /// <summary>SLICE-10 Block 5: a graduated cover hint, by product decision (2026-09-29) to model degree, not a binary "has cover"/"no cover".</summary>
    public enum CoverDegree
    {
        None,
        Half,
        ThreeQuarters,
        Full,
    }

    /// <summary>
    /// SLICE-10 Block 5: pure cover-suggestion geometry, kept in <c>Odyssey.Domain</c> (no dependency
    /// on any other module, ADR-001 section 5), the same two-layer shape <see cref="LineOfSight"/>
    /// already established -- the Application-layer <c>CoverSuggestionService</c> is a thin
    /// repository-reading shell over <see cref="ComputeCoverDegree"/>.
    ///
    /// A deliberate hint, not an automatic combat modifier (product decision 2026-09-29): nothing
    /// here is wired into the attack pipeline. This is why the method takes plain coordinates and
    /// returns a plain enum -- exactly the shape a caller (UI, or a future, separate combat-modifier
    /// decision) can use however it chooses, without this file knowing or caring.
    /// </summary>
    public static class CoverGeometry
    {
        /// <summary>
        /// The MVP, not-yet-configurable "how big is a token, for cover purposes" radius -- half a
        /// world unit ("half a grid cell" by this codebase's general VTT convention: a token occupies
        /// a one-cell-diameter area). Per-token size is an explicit non-goal of this task; see the
        /// task contract for why a fixed constant, not a per-token field, was chosen for the MVP.
        /// </summary>
        public const double DefaultTargetRadius = 0.5;

        /// <summary>
        /// Samples four points around the target's silhouette (at the target's own position, offset
        /// by <see cref="DefaultTargetRadius"/> along the four diagonals relative to the attacker-to-
        /// target line -- the exact orientation is not load-bearing for correctness, only that four
        /// evenly-spaced points around the target are tested) and counts how many of the four
        /// attacker-to-sample segments are blocked by an obstacle for which
        /// <see cref="ObstacleGeometry.BlocksVision"/> is true -- the same predicate
        /// <see cref="LineOfSight.HasClearLine"/> itself uses (ADR-020 section 12.4: cover must not
        /// invent its own blocking interpretation, separate from line-of-sight's own). Reuses
        /// <see cref="SegmentIntersection.SegmentsIntersect"/> directly, not a re-derived test.
        ///
        /// This is a simplified, standard VTT adaptation of the tabletop "corner to corner" rule: one
        /// attacker point (not four attacker corners) against four target sample points -- see the
        /// task contract for why the fuller "4 attacker corners x 4 target corners" version was not
        /// implemented. This method does not itself decide whether the target is visible at all (that
        /// is <c>TokenVisionService.ComputeLineOfSight</c>'s job, Block 3, a separate call if needed);
        /// if all four samples are blocked this returns <see cref="CoverDegree.Full"/> even if the
        /// target's own center happens to be visible along some other line -- a deliberate
        /// simplification of the method, not a bug.
        /// </summary>
        public static CoverDegree ComputeCoverDegree(double attackerX, double attackerY, double targetX, double targetY, IReadOnlyList<ObstacleSegment> obstacles)
        {
            if (obstacles == null) throw new ArgumentNullException(nameof(obstacles));

            Span<(double X, double Y)> samples = stackalloc (double, double)[4];
            BuildTargetSamples(targetX, targetY, samples);

            int blockedCount = 0;
            for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++)
            {
                if (IsBlocked(attackerX, attackerY, samples[sampleIndex].X, samples[sampleIndex].Y, obstacles))
                {
                    blockedCount++;
                }
            }

            return blockedCount switch
            {
                0 => CoverDegree.None,
                1 => CoverDegree.Half,
                2 => CoverDegree.Half,
                3 => CoverDegree.ThreeQuarters,
                _ => CoverDegree.Full,
            };
        }

        private static bool IsBlocked(double attackerX, double attackerY, double sampleX, double sampleY, IReadOnlyList<ObstacleSegment> obstacles)
        {
            for (int index = 0; index < obstacles.Count; index++)
            {
                ObstacleSegment obstacle = obstacles[index];
                if (!ObstacleGeometry.BlocksVision(obstacle.Kind, obstacle.IsOpen))
                {
                    continue;
                }

                if (SegmentIntersection.SegmentsIntersect(attackerX, attackerY, sampleX, sampleY, obstacle.X1, obstacle.Y1, obstacle.X2, obstacle.Y2))
                {
                    return true;
                }
            }

            return false;
        }

        // The four diagonal offsets (45/135/225/315 degrees) around the target's own position -- an arbitrary but fixed orientation, not load-bearing for correctness (see this type's own remarks).
        private static void BuildTargetSamples(double targetX, double targetY, Span<(double X, double Y)> samples)
        {
            const double diagonalOffset = DefaultTargetRadius * 0.70710678118654752; // DefaultTargetRadius / sqrt(2), so each sample is exactly DefaultTargetRadius from the target center.
            samples[0] = (targetX + diagonalOffset, targetY + diagonalOffset);
            samples[1] = (targetX + diagonalOffset, targetY - diagonalOffset);
            samples[2] = (targetX - diagonalOffset, targetY + diagonalOffset);
            samples[3] = (targetX - diagonalOffset, targetY - diagonalOffset);
        }
    }
}
