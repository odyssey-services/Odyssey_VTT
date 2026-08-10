using System.IO;
using NUnit.Framework;

namespace Odyssey.Tests.Contracts;

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

        while (current is not null && !File.Exists(Path.Combine(current, "AGENTS.md")))
        {
            current = Directory.GetParent(current)?.FullName;
        }

        return current ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
