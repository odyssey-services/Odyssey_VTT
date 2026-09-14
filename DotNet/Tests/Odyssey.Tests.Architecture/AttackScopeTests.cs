using System.IO;
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
    }
}
