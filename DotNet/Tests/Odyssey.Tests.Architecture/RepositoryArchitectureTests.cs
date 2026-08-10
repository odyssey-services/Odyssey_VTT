using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace Odyssey.Tests.Architecture;

public sealed class RepositoryArchitectureTests
{
    [Test]
    public void RepositoryStructurePassesArchitectureGuard()
    {
        string root = FindRepositoryRoot();
        string script = Path.Combine(root, "scripts", "verify-test-structure.ps1");

        using Process process = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell",
            ArgumentList =
            {
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                script
            },
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }) ?? throw new InvalidOperationException("Failed to start architecture guard.");

        process.WaitForExit();

        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        Assert.That(process.ExitCode, Is.EqualTo(0), output);
        Assert.That(output, Does.Contain("TST-ARCH-001 PASS"));
        Assert.That(output, Does.Contain("TST-ARCH-002 PASS"));
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
