using System.IO;
using NUnit.Framework;

namespace Odyssey.Tests.Contracts
{
    public sealed class ProjectContractTests
    {
        [Test]
        public void PersistenceAndNetworkingBridgeProjectsAreNotCreated()
        {
            string root = FindRepositoryRoot();

            Assert.That(File.Exists(Path.Combine(root, "DotNet", "Projects", "Odyssey.Persistence.csproj")), Is.False);
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
