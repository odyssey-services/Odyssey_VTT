using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Odyssey.Tests.Architecture
{
    public sealed class AttackScopeTests
    {
        [Test]
        public void Evaluation_surface_is_read_only_and_has_no_apply_log_or_client_dependency()
        {
            DirectoryInfo? root = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Packages"))) root = root.Parent;
            Assert.That(root, Is.Not.Null);
            string source = File.ReadAllText(Path.Combine(root!.FullName, "Packages", "com.odyssey.application", "Runtime", "Combat", "AttackEvaluationService.cs")) +
                            File.ReadAllText(Path.Combine(root.FullName, "Packages", "com.odyssey.domain", "Runtime", "Combat", "AttackPipelineContracts.cs")) +
                            File.ReadAllText(Path.Combine(root.FullName, "Packages", "com.odyssey.persistence", "Runtime", "Sqlite", "SqliteAttackStateReader.cs"));
            foreach (string forbidden in new[] { "CombatEncounterCommandLedger", "GameLog", "ActiveEffect", "UnityEngine", "Odyssey.Networking", "Save(", "Apply(", "INSERT INTO", "UPDATE ", "DELETE ", "CREATE TABLE", "DateTime.UtcNow", "DateTime.Now", "Stopwatch", "Environment.TickCount", "System.Random" })
                Assert.That(source, Does.Not.Contain(forbidden));
        }

        [Test] // TC-ATTACK-027
        public void Persistence_source_declares_only_the_two_deliberate_attack_named_types()
        {
            // ODY-S05-604 adds a second, legitimate standalone Attack-named
            // persistence type (SqliteAttackApplyRepository) alongside
            // ODY-S05-603's read-only SqliteAttackStateReader -- this is a
            // direct, mechanical update of the invariant this test already
            // enforced (no *unexpected* Attack-named persistence type), not a
            // change to ODY-S05-603's own behavior.
            DirectoryInfo? root = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Packages"))) root = root.Parent;
            Assert.That(root, Is.Not.Null);
            string persistenceRoot = Path.Combine(root!.FullName, "Packages", "com.odyssey.persistence", "Runtime");
            var declaredTypeNames = new System.Collections.Generic.List<string>();
            foreach (string file in Directory.GetFiles(persistenceRoot, "*.cs", SearchOption.AllDirectories))
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(file), @"\b(?:class|struct|record)\s+(\w*Attack\w*)\b"))
                    declaredTypeNames.Add(match.Groups[1].Value);
            }
            Assert.That(declaredTypeNames, Is.EquivalentTo(new[] { "SqliteAttackStateReader", "SqliteAttackApplyRepository" }));
        }
    }
}
