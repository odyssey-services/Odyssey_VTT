using NUnit.Framework;
using Odyssey.Domain.Combat;

namespace Odyssey.Tests.Architecture
{
    /// <summary>Registration anchor for TC-COMBAT-016 and TC-COMBAT-017.</summary>
    public sealed class CombatScopeTests
    {
        [Test]
        public void Timeline_vocabulary_has_no_attack_outcome()
        {
            Assert.That(System.Enum.IsDefined(typeof(CombatLifecycleEventKind), CombatLifecycleEventKind.TurnStarted), Is.True);
            Assert.That(System.Enum.GetNames(typeof(CombatLifecycleEventKind)), Does.Not.Contain("AttackResolved"));
        }
    }
}
