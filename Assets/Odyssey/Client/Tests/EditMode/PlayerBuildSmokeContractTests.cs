using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Odyssey.Application.Serialization;
using Odyssey.Application.Versions;
using Odyssey.Unity.Client;

namespace Odyssey.Tests.Unity.EditMode
{
    public sealed class PlayerBuildSmokeContractTests
    {
        private const string Sha = "0123456789abcdef0123456789abcdef01234567";

        [Test]
        public void SmokeActivationRequiresDevelopmentDebugBuildAndValidArguments()
        {
            string[] validArgs =
            {
                "Odyssey.exe",
                "--odyssey-player-smoke",
                "--odyssey-smoke-evidence",
                "smoke-1.json",
                "--odyssey-smoke-run",
                "1"
            };

            Assert.That(PlayerSmokeMode.TryParseActivation(true, true, validArgs, out string? evidenceName, out int runNumber), Is.True);
            Assert.That(evidenceName, Is.EqualTo("smoke-1.json"));
            Assert.That(runNumber, Is.EqualTo(1));
            Assert.That(PlayerSmokeMode.TryParseActivation(false, true, validArgs, out _, out _), Is.False);
            Assert.That(PlayerSmokeMode.TryParseActivation(true, false, validArgs, out _, out _), Is.False);
            Assert.That(PlayerSmokeMode.TryParseActivation(true, true, new[] { "Odyssey.exe" }, out _, out _), Is.False);
        }

        [Test]
        public void EvidencePublicationReplacesAtomicallyAndPreservesPreviousTargetOnFailure()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            string target = Path.Combine(directory.Path, "smoke-1.json");
            string temp = Path.Combine(directory.Path, "smoke-1.json.tmp");
            File.WriteAllText(target, "{\"result\":\"old\"}");

            PlayerSmokeMode.WriteEvidenceFile(target, "{\"result\":\"pass\"}", temp);

            Assert.That(File.ReadAllText(target), Is.EqualTo("{\"result\":\"pass\"}"));
            Assert.That(File.Exists(temp), Is.False);

            File.WriteAllText(target, "{\"result\":\"still-valid\"}");
            Directory.CreateDirectory(temp);
            Assert.Throws<UnauthorizedAccessException>(() => PlayerSmokeMode.WriteEvidenceFile(target, "{\"result\":\"new\"}", temp));
            Assert.That(File.ReadAllText(target), Is.EqualTo("{\"result\":\"still-valid\"}"));
            Assert.That(Directory.Exists(temp), Is.True);
        }

        [Test]
        public void EditorBuildOutputPathMustMatchCanonicalArtifactPath()
        {
            using TemporaryDirectory directory = new TemporaryDirectory();
            BuildIdentity identity = CreateBuildIdentity(BuildChannel.Development);
            string root = directory.Path;
            string identityDirectory = Path.Combine(root, "Assets", "StreamingAssets", "Odyssey");
            Directory.CreateDirectory(identityDirectory);
            File.WriteAllBytes(Path.Combine(identityDirectory, "build-identity.json"), BuildIdentityCodec.WriteBuildIdentity(identity).Value.Bytes);
            string expectedDirectory = Path.Combine(root, "artifacts", "builds", identity.BuildId, "Windows-x64");
            Directory.CreateDirectory(expectedDirectory);
            string expected = Path.Combine(expectedDirectory, "Odyssey.exe");

            Assert.That(ValidateEditorOutput(root, expected), Is.EqualTo(Path.GetFullPath(expected)));
            AssertRejects(root, Path.Combine(root, "..", "outside", "Odyssey.exe"));
            AssertRejects(root, Path.Combine(root, "artifacts", "builds-evil", identity.BuildId, "Windows-x64", "Odyssey.exe"));
            AssertRejects(root, Path.Combine(root, "artifacts", "builds", identity.BuildId, "Windows-x64-evil", "Odyssey.exe"));
            AssertRejects(root, Path.Combine(root, "artifacts", "builds", identity.BuildId, "Windows-x64", "..", "Odyssey.exe"));
            AssertRejects(root, Path.Combine(root, "artifacts", "builds", "odyssey-development-999.1-g0123456789ab", "Windows-x64", "Odyssey.exe"));
            AssertRejects(root, Path.Combine(root, "artifacts", "builds", identity.BuildId, "Windows-x64", "Other.exe"));
        }

        private static string ValidateEditorOutput(string root, string outputPath)
        {
            Type type = Type.GetType("Odyssey.Unity.Client.Editor.OdysseyDevelopmentBuild, Odyssey.Unity.Client.Editor", throwOnError: true)!;
            MethodInfo method = type.GetMethod("ValidateBuildOutputPathForTest", BindingFlags.Public | BindingFlags.Static)!;
            return (string)method.Invoke(null, new object[] { root, outputPath });
        }

        private static void AssertRejects(string root, string outputPath)
        {
            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() => ValidateEditorOutput(root, outputPath));
            Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        }

        private static BuildIdentity CreateBuildIdentity(BuildChannel channel)
        {
            return BuildIdentityCodec.Create(
                new VersionSource(ApplicationVersion.Parse("0.1.0")),
                StandardCompatibility(),
                channel,
                31799960601,
                1,
                Sha,
                channel == BuildChannel.PullRequest ? "refs/pull/12/merge" : "refs/heads/main",
                WorkingTreeState.Clean,
                "20260812T1200000000001Z",
                "6000.4.0f1",
                "8cf496087c8f",
                "10.0.302",
                "Development-Debug",
                "WindowsStandalone",
                "x86_64",
                "Mono",
                "NETStandard2.1",
                channel == BuildChannel.PullRequest ? 12 : (long?)null);
        }

        private static CompatibilityConfig StandardCompatibility()
        {
            return new CompatibilityConfig(
                new CompatibilityRange(1, 1),
                new CompatibilityRange(1, 1),
                new CompatibilityRange(1, 1),
                new CompatibilityRange(1, 1),
                new CompatibilityRange(1, 1),
                new CompatibilityRange(1, 1),
                new CompatibilityRange(1, 1),
                new ProtocolCompatibilityRange(1, 1, 1));
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "odyssey-player-contract-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public void Dispose()
            {
                if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
            }
        }
    }
}
