using System.IO;
using NUnit.Framework;

namespace Odyssey.Tests.Contracts
{
    public sealed class ProjectContractTests
    {
        [Test]
        public void PersistenceAndNetworkingBridgeProjectsExist()
        {
            string root = FindRepositoryRoot();

            // Odyssey.Persistence.csproj was created by ODY-S01-007 (SLICE-01 Campaign
            // Storage Foundation), the vertical slice at which ADR-006 section 24
            // expects it to appear. Odyssey.Networking.csproj was created by
            // ODY-S02-001 (SLICE-02 Transport Abstraction), the Stage 3 vertical
            // slice at which it becomes real -- confirmed via ODY-S02-000's own
            // verified-facts section that no prior task had touched the package.
            Assert.That(File.Exists(Path.Combine(root, "DotNet", "Projects", "Odyssey.Persistence.csproj")), Is.True);
            Assert.That(File.Exists(Path.Combine(root, "DotNet", "Projects", "Odyssey.Networking.csproj")), Is.True);
        }

        private static string FindRepositoryRoot()
        {
            string? current = TestContext.CurrentContext.TestDirectory;

            while (current != null && !File.Exists(Path.Combine(current, "AGENTS.md")))
            {
                DirectoryInfo? parent = Directory.GetParent(current);
                current = parent == null ? null : parent.FullName;
            }

            if (current == null)
            {
                throw new DirectoryNotFoundException("Repository root was not found.");
            }

            return current;
        }
    }
}
