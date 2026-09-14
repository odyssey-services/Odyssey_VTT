using System.IO;
using NUnit.Framework;

namespace Odyssey.Tests.Architecture
{
    /// <summary>ODY-S05-605: `ActiveEffectExpiryRules`'s six new combat-duration boundary-check functions must stay pure -- no SQL/I-O inside the decision functions themselves; only their caller (`CombatEffectExpiryService`) may read/write.</summary>
    public sealed class CombatEffectExpiryScopeTests
    {
        [Test] // TC-ATTACK-045
        public void Combat_duration_boundary_check_functions_are_pure_with_no_sql_or_io()
        {
            DirectoryInfo? root = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Packages"))) root = root.Parent;
            Assert.That(root, Is.Not.Null);
            string source = File.ReadAllText(Path.Combine(root!.FullName, "Packages", "com.odyssey.application", "Runtime", "Effects", "ActiveEffectExpiryRules.cs"));
            foreach (string forbidden in new[] { "SqliteConnection", "SqliteCommand", "CreateCommand(", "ExecuteReader(", "ExecuteScalar(", "ExecuteNonQuery(", "INSERT INTO", "UPDATE ", "DELETE ", "SELECT ", "File.", "Directory.", "DateTime.UtcNow", "DateTime.Now", "System.Random" })
                Assert.That(source, Does.Not.Contain(forbidden));
        }
    }
}
