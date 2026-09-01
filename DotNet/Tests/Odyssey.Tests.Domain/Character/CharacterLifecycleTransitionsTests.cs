using NUnit.Framework;
using Odyssey.Domain.Character;

namespace Odyssey.Tests.Domain.Character
{
    /// <summary>
    /// ODY-S04-101: pure, generic tests for
    /// <see cref="CharacterLifecycleTransitions.IsValidTransition"/> against
    /// 10_Characters_And_Progression section 7.1's adjacency table. These are
    /// deliberately generic shape checks only -- they do not test which actor
    /// or command may take a given edge (Dead/Archived business rules are
    /// ODY-S04-110/111's own scope).
    /// </summary>
    public sealed class CharacterLifecycleTransitionsTests
    {
        [TestCase(CharacterLifecycleStatus.Draft, CharacterLifecycleStatus.Active)]
        [TestCase(CharacterLifecycleStatus.Active, CharacterLifecycleStatus.Inactive)]
        [TestCase(CharacterLifecycleStatus.Inactive, CharacterLifecycleStatus.Active)]
        [TestCase(CharacterLifecycleStatus.Active, CharacterLifecycleStatus.Retired)]
        [TestCase(CharacterLifecycleStatus.Inactive, CharacterLifecycleStatus.Retired)]
        [TestCase(CharacterLifecycleStatus.Active, CharacterLifecycleStatus.Dead)]
        [TestCase(CharacterLifecycleStatus.Inactive, CharacterLifecycleStatus.Dead)]
        [TestCase(CharacterLifecycleStatus.Retired, CharacterLifecycleStatus.Dead)]
        [TestCase(CharacterLifecycleStatus.Draft, CharacterLifecycleStatus.Archived)]
        [TestCase(CharacterLifecycleStatus.Active, CharacterLifecycleStatus.Archived)]
        [TestCase(CharacterLifecycleStatus.Inactive, CharacterLifecycleStatus.Archived)]
        [TestCase(CharacterLifecycleStatus.Retired, CharacterLifecycleStatus.Archived)]
        [TestCase(CharacterLifecycleStatus.Dead, CharacterLifecycleStatus.Archived)]
        [TestCase(CharacterLifecycleStatus.Dead, CharacterLifecycleStatus.Active)]
        [TestCase(CharacterLifecycleStatus.Dead, CharacterLifecycleStatus.Inactive)]
        [TestCase(CharacterLifecycleStatus.Dead, CharacterLifecycleStatus.Retired)]
        [TestCase(CharacterLifecycleStatus.Archived, CharacterLifecycleStatus.Draft)]
        [TestCase(CharacterLifecycleStatus.Archived, CharacterLifecycleStatus.Active)]
        [TestCase(CharacterLifecycleStatus.Archived, CharacterLifecycleStatus.Inactive)]
        [TestCase(CharacterLifecycleStatus.Archived, CharacterLifecycleStatus.Retired)]
        public void IsValidTransition_ForEveryProductTableEdge_ReturnsTrue(CharacterLifecycleStatus from, CharacterLifecycleStatus to)
        {
            Assert.That(CharacterLifecycleTransitions.IsValidTransition(from, to), Is.True, $"{from} -> {to} must be a legal edge per product section 7.1's table.");
        }

        [TestCase(CharacterLifecycleStatus.Draft, CharacterLifecycleStatus.Dead)]
        [TestCase(CharacterLifecycleStatus.Draft, CharacterLifecycleStatus.Inactive)]
        [TestCase(CharacterLifecycleStatus.Draft, CharacterLifecycleStatus.Retired)]
        [TestCase(CharacterLifecycleStatus.Retired, CharacterLifecycleStatus.Active)]
        [TestCase(CharacterLifecycleStatus.Retired, CharacterLifecycleStatus.Inactive)]
        [TestCase(CharacterLifecycleStatus.Archived, CharacterLifecycleStatus.Dead)]
        [TestCase(CharacterLifecycleStatus.Archived, CharacterLifecycleStatus.Archived)]
        [TestCase(CharacterLifecycleStatus.Active, CharacterLifecycleStatus.Active)]
        [TestCase(CharacterLifecycleStatus.Dead, CharacterLifecycleStatus.Dead)]
        public void IsValidTransition_ForEveryNonTableEdge_ReturnsFalse(CharacterLifecycleStatus from, CharacterLifecycleStatus to)
        {
            Assert.That(CharacterLifecycleTransitions.IsValidTransition(from, to), Is.False, $"{from} -> {to} must not be a legal edge -- it is not in product section 7.1's table (or is a same-status no-op).");
        }
    }
}
