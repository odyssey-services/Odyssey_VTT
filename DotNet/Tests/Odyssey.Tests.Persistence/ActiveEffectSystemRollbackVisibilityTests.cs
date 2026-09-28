using System.Linq;
using NUnit.Framework;
using Odyssey.Application.Persistence;

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S10-103 follow-up (independent review finding): <c>RemoveActiveEffectAsSystemRollback</c> was first
    /// added directly to the public <see cref="IActiveEffectRepository"/> with no authorization check at all --
    /// reachable by any code holding that (very widely held) interface. It was moved to its own internal
    /// interface, <c>IActiveEffectSystemRollback</c>. This is a type-level assertion, not a behavior test: it
    /// fails if the unsafe method is ever reintroduced to the public interface, independent of whether any test
    /// happens to exercise that path.
    /// </summary>
    public sealed class ActiveEffectSystemRollbackVisibilityTests
    {
        [Test] // TC-PERSIST-059
        public void IActiveEffectRepository_NoLongerDeclaresTheSystemRollbackMethod()
        {
            System.Reflection.MethodInfo[] members = typeof(IActiveEffectRepository).GetMethods();

            Assert.That(members.Select(m => m.Name), Does.Not.Contain("RemoveActiveEffectAsSystemRollback"),
                "RemoveActiveEffectAsSystemRollback must not be reachable through the public IActiveEffectRepository " +
                "-- any code holding that interface would be able to remove any effect from any target with no " +
                "authorization check at all.");

            // And the public interface is otherwise unaffected: every member it had before this follow-up remains.
            string[] expectedNames =
            {
                "CreateActiveEffect", "GetActiveEffect", "ListActiveEffectsByTarget", "ListActiveEffectsBySource",
                "ExpireActiveEffect", "SetItemEffectEquipped", "RemoveActiveEffect",
            };
            Assert.That(members.Select(m => m.Name), Is.EquivalentTo(expectedNames));
        }

        [Test] // TC-PERSIST-059
        public void IActiveEffectRepository_TypeItselfIsPublic_TheRollbackInterfaceIsNot()
        {
            Assert.That(typeof(IActiveEffectRepository).IsPublic, Is.True, "the ordinary repository contract stays public");

            System.Type? rollbackInterface = typeof(IActiveEffectRepository).Assembly.GetType("Odyssey.Application.Persistence.IActiveEffectSystemRollback");
            Assert.That(rollbackInterface, Is.Not.Null, "the rollback interface must exist in the same assembly");
            Assert.That(rollbackInterface!.IsPublic, Is.False, "it must not be a publicly visible type -- only assemblies granted InternalsVisibleTo may see it");
            Assert.That(rollbackInterface.IsNotPublic, Is.True);
        }
    }
}
