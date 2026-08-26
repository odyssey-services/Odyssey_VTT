using NUnit.Framework;
using Odyssey.Domain.Geometry;

namespace Odyssey.Tests.Domain.Geometry
{
    /// <summary>
    /// ODY-S03-004: ADR-020's GridType=None geometry primitives -- the only
    /// case this task's vertical slice exercises (see BoardGeometry.cs's own
    /// doc comment). Golden-vector style: expected results are computed by
    /// hand, not by comparing the implementation against itself.
    /// </summary>
    public sealed class BoardGeometryTests
    {
        [Test]
        public void EuclideanDistance_ThreeFourFive_ReturnsFive()
        {
            // TC-BOARD-001
            double distance = BoardGeometry.EuclideanDistance(0, 0, 3, 4);
            Assert.That(distance, Is.EqualTo(5.0).Within(1e-12));
        }

        [Test]
        public void EuclideanDistance_SamePoint_ReturnsZero()
        {
            // TC-BOARD-001
            double distance = BoardGeometry.EuclideanDistance(10, -7, 10, -7);
            Assert.That(distance, Is.EqualTo(0.0));
        }

        [Test]
        public void IsFinite_RejectsNaNAndInfinity_AcceptsOrdinaryValues()
        {
            // TC-BOARD-002
            Assert.That(BoardGeometry.IsFinite(1.0, 2.0), Is.True);
            Assert.That(BoardGeometry.IsFinite(double.NaN, 0), Is.False);
            Assert.That(BoardGeometry.IsFinite(0, double.NaN), Is.False);
            Assert.That(BoardGeometry.IsFinite(double.PositiveInfinity, 0), Is.False);
            Assert.That(BoardGeometry.IsFinite(0, double.NegativeInfinity), Is.False);
        }

        [Test]
        public void AlmostEqual_WithinEpsilon_IsTrue_JustOutside_IsFalse()
        {
            // TC-BOARD-003: ADR-020 section 6.1's GeometryEpsilonV1 boundary.
            double a = 1.0;
            double justInside = 1.0 + (BoardGeometry.GeometryEpsilonV1 / 2.0);
            double justOutside = 1.0 + (BoardGeometry.GeometryEpsilonV1 * 2.0);

            Assert.That(BoardGeometry.AlmostEqual(a, justInside), Is.True);
            Assert.That(BoardGeometry.AlmostEqual(a, justOutside), Is.False);
        }

        [Test]
        public void SamePosition_RequiresBothAxesWithinEpsilon()
        {
            // TC-BOARD-003
            Assert.That(BoardGeometry.SamePosition(5, 5, 5, 5), Is.True);
            Assert.That(BoardGeometry.SamePosition(5, 5, 5.0000001, 5), Is.True, "within epsilon on X, exact on Y");
            Assert.That(BoardGeometry.SamePosition(5, 5, 5.1, 5), Is.False, "X differs beyond epsilon");
            Assert.That(BoardGeometry.SamePosition(5, 5, 5, 5.1), Is.False, "Y differs beyond epsilon");
        }
    }
}
