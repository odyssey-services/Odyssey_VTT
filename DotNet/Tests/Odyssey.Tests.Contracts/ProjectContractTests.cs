using System.IO;
using NUnit.Framework;

namespace Odyssey.Tests.Contracts
{
    public sealed class ProjectContractTests
    {
        [Test]
        public void NetworkingBridgeProjectIsNotCreated()
        {
            string root = FindRepositoryRoot();

            // Odyssey.Persistence.csproj was created by ODY-S01-007 (SLICE-01 Campaign
            // Storage Foundation), the vertical slice at which ADR-006 section 24
            // expects it to appear. Odyssey.Networking.csproj remains Stage 3 scope.
            Assert.That(File.Exists(Path.Combine(root, "DotNet", "Projects", "Odyssey.Persistence.csproj")), Is.True);
            Assert.That(File.Exists(Path.Combine(root, "DotNet", "Projects", "Odyssey.Networking.csproj")), Is.False);
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
