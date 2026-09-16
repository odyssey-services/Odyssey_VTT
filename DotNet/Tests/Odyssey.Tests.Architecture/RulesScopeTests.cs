using System.IO;
using NUnit.Framework;

namespace Odyssey.Tests.Architecture
{
    /// <summary>
    /// ODY-S06-105: `ADR-001` section 6.2's own `Odyssey.Rules` module boundary -- "Rules получает время, RNG
    /// outcomes и внешнее состояние через явные входные параметры", confirmed structurally by
    /// `Packages/com.odyssey.rules/Odyssey.Rules.asmdef`'s own `references: ["Odyssey.Domain"]` (no
    /// `Odyssey.Application`). `CoreAttackRulesEvaluator` (this task's own first real `IAttackRulesEvaluator`)
    /// receives its only randomness as an already-drawn `AttackRandomSample`, never touching
    /// `IAuthoritativeRandomStream`/`IAuthoritativeRandomStreamFactory`/`System.Random` itself -- this test
    /// scans the real source text (not just relying on the compiler forbidding the reference) so a future
    /// change cannot silently reintroduce a direct RNG dependency inside `Odyssey.Rules`.
    /// </summary>
    public sealed class RulesScopeTests
    {
        [Test] // TC-ATTACK-126
        public void Odyssey_Rules_source_never_references_RNG_types_directly()
        {
            DirectoryInfo? root = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Packages"))) root = root.Parent;
            Assert.That(root, Is.Not.Null);
            string rulesRoot = Path.Combine(root!.FullName, "Packages", "com.odyssey.rules", "Runtime");
            Assert.That(Directory.Exists(rulesRoot), Is.True);

            foreach (string file in Directory.GetFiles(rulesRoot, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(file);
                foreach (string forbidden in new[]
                {
                    "IAuthoritativeRandomStream",
                    "IAuthoritativeRandomStreamFactory",
                    "RandomDecisionContext",
                    "RngKeyEpochId",
                    "Odyssey.Application.Random",
                    "System.Random",
                    "new Random(",
                })
                {
                    Assert.That(source, Does.Not.Contain(forbidden), "'" + forbidden + "' must not appear in " + Path.GetFileName(file) + " -- Odyssey.Rules receives randomness only as an already-drawn AttackRandomSample.");
                }
            }
        }
    }
}
