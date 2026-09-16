using System.IO;
using NUnit.Framework;

namespace Odyssey.Tests.Architecture
{
    /// <summary>
    /// ODY-S06-106: the same `ADR-001` §6.2 module-boundary guard `RulesScopeTests` (`ODY-S06-105`)
    /// already established for `Odyssey.Rules.Combat`, now extended to this task's own new
    /// `Odyssey.Rules.Mechanics` folder -- `MechanicsPrimitiveInterpreter` must receive its only
    /// randomness as an already-drawn `AttackRandomSample` (reused, unmodified, from `ODY-S06-105`) and
    /// must never reference `Odyssey.Application` (confirmed structurally by `Odyssey.Rules.asmdef`'s own
    /// `references: ["Odyssey.Domain"]`, unchanged by this task -- `ActivateAbilityService`'s own weapon/
    /// ability decode step lives in `Odyssey.Application`, exactly mirroring `ODY-S06-105`'s own resolution
    /// of the identical architecture conflict for `CoreAttackRulesEvaluator`).
    /// </summary>
    public sealed class MechanicsInterpreterScopeTests
    {
        [Test] // TC-ABILITY-009
        public void Odyssey_Rules_mechanics_source_never_references_RNG_or_Application_types_directly()
        {
            // Scoped to this task's own new Mechanics/ folder only -- ODY-S06-105's own CoreAttackRulesEvaluator.cs
            // (Combat/) legitimately mentions "Odyssey.Application.Content.TypedDefinitionCodec" in its own
            // prose doc comment (explaining exactly the architecture conflict this task's own equivalent
            // comment also explains), and this task is forbidden from modifying that file's content -- so this
            // stricter check (which also forbids the mere string "Odyssey.Application", broader than
            // RulesScopeTests' own RNG-only check) must not scan that sibling file at all.
            DirectoryInfo? root = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Packages"))) root = root.Parent;
            Assert.That(root, Is.Not.Null);
            string mechanicsRoot = Path.Combine(root!.FullName, "Packages", "com.odyssey.rules", "Runtime", "Mechanics");
            Assert.That(Directory.Exists(mechanicsRoot), Is.True);

            foreach (string file in Directory.GetFiles(mechanicsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(file);
                foreach (string forbidden in new[]
                {
                    "IAuthoritativeRandomStream",
                    "IAuthoritativeRandomStreamFactory",
                    "RandomDecisionContext",
                    "RngKeyEpochId",
                    "Odyssey.Application",
                    "System.Random",
                    "new Random(",
                    "TypedDefinitionCodec",
                    "MechanicsPayloadCodec",
                })
                {
                    Assert.That(source, Does.Not.Contain(forbidden), "'" + forbidden + "' must not appear in " + Path.GetFileName(file) + " -- Odyssey.Rules references only Odyssey.Domain (confirmed by Odyssey.Rules.asmdef/csproj) and receives randomness only as an already-drawn AttackRandomSample.");
                }
            }
        }
    }
}
