using System.IO;
using NUnit.Framework;

namespace Odyssey.Tests.Architecture
{
    /// <summary>ODY-S05-606: the combat effect apply path must never call the public `IActiveEffectRepository.CreateActiveEffect` -- that method opens its own separate connection/transaction, which would break ADR-029 section 8's own "one atomic apply transaction" requirement. It must reuse `SqliteActiveEffectRepository`'s own internal SQL helpers instead.</summary>
    public sealed class AttackEffectApplicationScopeTests
    {
        [Test] // TC-ATTACK-055
        public void Combat_apply_path_never_calls_the_public_CreateActiveEffect_method()
        {
            DirectoryInfo? root = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Packages"))) root = root.Parent;
            Assert.That(root, Is.Not.Null);
            string source = File.ReadAllText(Path.Combine(root!.FullName, "Packages", "com.odyssey.persistence", "Runtime", "Sqlite", "SqliteAttackApplyRepository.cs")) +
                            File.ReadAllText(Path.Combine(root.FullName, "Packages", "com.odyssey.application", "Runtime", "Combat", "AttackApplyService.cs"));
            Assert.That(source, Does.Not.Contain(".CreateActiveEffect("));
        }
    }
}
