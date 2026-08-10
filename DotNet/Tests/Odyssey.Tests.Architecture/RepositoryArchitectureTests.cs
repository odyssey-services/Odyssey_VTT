using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace Odyssey.Tests.Architecture
{
    public sealed class RepositoryArchitectureTests
    {
        [Test]
        public void RepositoryStructurePassesArchitectureGuard()
        {
            string root = FindRepositoryRoot();
            string script = Path.Combine(root, "scripts", "verify-test-structure.ps1");

            using (Process process = Process.Start(new ProcessStartInfo
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
            }) ?? throw new InvalidOperationException("Failed to start architecture guard."))
            {
                process.WaitForExit();

                string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                Assert.That(process.ExitCode, Is.EqualTo(0), output);
                Assert.That(output, Does.Contain("TC-ARCH-001 PASS"));
                Assert.That(output, Does.Contain("TC-ARCH-002 PASS"));
            }
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
