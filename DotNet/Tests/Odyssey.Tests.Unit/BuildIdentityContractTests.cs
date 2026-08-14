using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Odyssey.Application.Serialization;
using Odyssey.Application.Versions;

namespace Odyssey.Tests.Unit
{
    public sealed class BuildIdentityContractTests
    {
        private const string Sha = "0123456789abcdef0123456789abcdef01234567";

        [Test]
        public void VersionSourceSchemaV1IsStrictAndAcceptsTrackedApplicationVersion()
        {
            VersionSource version = BuildIdentityCodec.ReadVersionSource(File.ReadAllBytes(Path.Combine(FindRepositoryRoot(), "version.json"))).Value;

            Assert.That(version.SchemaVersion, Is.EqualTo(1));
            Assert.That(version.ApplicationVersion.ToString(), Is.EqualTo("0.1.0"));
        }

        [Test]
        public void InvalidVersionSourcesFailSafely()
        {
            string[] invalid =
            {
                "{",
                "{\"schemaVersion\":1,\"schemaVersion\":1,\"applicationVersion\":\"0.1.0\"}",
                "{\"schemaVersion\":2,\"applicationVersion\":\"0.1.0\"}",
                "{\"schemaVersion\":1,\"applicationVersion\":\"0.1.0\",\"extra\":1}",
                "{\"schemaVersion\":1}",
                "{\"schemaVersion\":1,\"applicationVersion\":\"0.1.0-alpha\"}"
            };

            foreach (string json in invalid)
            {
                Assert.That(BuildIdentityCodec.ReadVersionSource(CanonicalJson.ToUtf8Bytes(json)).IsFailure, Is.True, json);
            }
        }

        [Test]
        public void CompatibilityConfigSchemaV1AndRangesAreStrict()
        {
            CompatibilityConfig config = BuildIdentityCodec.ReadCompatibilityConfig(File.ReadAllBytes(Path.Combine(FindRepositoryRoot(), "config", "compatibility.json"))).Value;

            Assert.That(config.SchemaVersion, Is.EqualTo(1));
            Assert.That(config.DatabaseSchemaVersion.Current, Is.EqualTo(1));
            Assert.That(config.NetworkProtocolVersion.Minimum, Is.EqualTo(1));
            Assert.That(config.NetworkProtocolVersion.Preferred, Is.EqualTo(1));
            Assert.That(config.NetworkProtocolVersion.Maximum, Is.EqualTo(1));
        }

        [Test]
        public void InvalidCompatibilityConfigFailsSafely()
        {
            string valid = BuildIdentityCodec.WriteCompatibilityConfig(StandardCompatibility()).Utf8Text;
            string[] invalid =
            {
                "{",
                valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1"),
                valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":2"),
                valid.Replace("\"current\":1", "\"current\":1,\"unexpected\":1"),
                valid.Replace("\"minimum\":1", "\"minimum\":0"),
                valid.Replace("\"minimum\":1,\"current\":1", "\"minimum\":2,\"current\":1"),
                valid.Replace("\"minimum\":1,\"preferred\":1,\"maximum\":1", "\"minimum\":2,\"preferred\":1,\"maximum\":1")
            };

            foreach (string json in invalid)
            {
                Assert.That(BuildIdentityCodec.ReadCompatibilityConfig(CanonicalJson.ToUtf8Bytes(json)).IsFailure, Is.True, json);
            }
        }

        [Test]
        public void CompatibilityCanonicalDigestIsStableForIdenticalValidatedInputs()
        {
            CompatibilityConfig left = BuildIdentityCodec.ReadCompatibilityConfig(File.ReadAllBytes(Path.Combine(FindRepositoryRoot(), "config", "compatibility.json"))).Value;
            CompatibilityConfig right = BuildIdentityCodec.ReadCompatibilityConfig(BuildIdentityCodec.WriteCompatibilityConfig(left).Bytes).Value;

            Assert.That(BuildIdentityCodec.WriteCompatibilityConfig(left).Utf8Text, Is.EqualTo(BuildIdentityCodec.WriteCompatibilityConfig(right).Utf8Text));
            Assert.That(BuildIdentityCodec.ComputeCompatibilityDigest(left), Is.EqualTo(BuildIdentityCodec.ComputeCompatibilityDigest(right)));
        }

        [Test]
        public void BuildIdentityChannelsUseCanonicalDisplayVersionAndBuildId()
        {
            VersionSource version = new VersionSource(ApplicationVersion.Parse("0.1.0"));
            CompatibilityConfig compatibility = StandardCompatibility();
            BuildIdentity local = BuildIdentityCodec.Create(version, compatibility, BuildChannel.Local, 1, 1, Sha, "heads/local", WorkingTreeState.Dirty, "20260812T120000Z", "6000.4.0f1", "8cf496087c8f", "10.0.302", "Development-Debug", "WindowsStandalone", "x86_64", "Mono", "NETStandard2.1");
            BuildIdentity pullRequest = BuildIdentityCodec.Create(version, compatibility, BuildChannel.PullRequest, 987654321, 2, Sha, "refs/pull/9/merge", WorkingTreeState.Clean, "20260812T120000Z", "6000.4.0f1", "8cf496087c8f", "10.0.302", "Development-Debug", "WindowsStandalone", "x86_64", "Mono", "NETStandard2.1", pullRequestNumber: 9);
            BuildIdentity development = BuildIdentityCodec.Create(version, compatibility, BuildChannel.Development, 987654321, 3, Sha, "heads/main", WorkingTreeState.Clean, "20260812T120001Z", "6000.4.0f1", "8cf496087c8f", "10.0.302", "Development-Debug", "WindowsStandalone", "x86_64", "Mono", "NETStandard2.1");

            Assert.That(local.DisplayVersion, Is.EqualTo("0.1.0-local.20260812T120000Z+g0123456789ab.dirty"));
            Assert.That(local.BuildId, Is.EqualTo("odyssey-local-20260812t120000z-g0123456789ab-dirty"));
            Assert.That(pullRequest.DisplayVersion, Is.EqualTo("0.1.0-pr.9.2+g0123456789ab"));
            Assert.That(pullRequest.BuildId, Is.EqualTo("odyssey-pr-987654321.2-g0123456789ab"));
            Assert.That(development.DisplayVersion, Is.EqualTo("0.1.0-dev.987654321.3+g0123456789ab"));
            Assert.That(development.BuildId, Is.EqualTo("odyssey-development-987654321.3-g0123456789ab"));
            Assert.That(new[] { local.BuildId, pullRequest.BuildId, development.BuildId }.Distinct().Count(), Is.EqualTo(3));
        }

        [Test]
        public void BuildIdentityJsonRoundTripsWithoutLocalIdentifiersOrReleaseClaim()
        {
            BuildIdentity identity = BuildIdentityCodec.Create(new VersionSource(ApplicationVersion.Parse("0.1.0")), StandardCompatibility(), BuildChannel.Local, 1, 1, Sha, "heads/local", WorkingTreeState.Clean, "20260812T120000Z", "6000.4.0f1", "8cf496087c8f", "10.0.302", "Development-Debug", "WindowsStandalone", "x86_64", "Mono", "NETStandard2.1");
            JsonPayload json = BuildIdentityCodec.WriteBuildIdentity(identity).Value;
            BuildIdentity read = BuildIdentityCodec.ReadBuildIdentity(json.Bytes).Value;
            string text = json.Utf8Text.ToLowerInvariant();

            Assert.That(read.BuildId, Is.EqualTo(identity.BuildId));
            Assert.That(read.GitCommitSha, Is.EqualTo(Sha));
            Assert.That(read.UnityVersion, Is.EqualTo("6000.4.0f1"));
            Assert.That(read.UnityChangeset, Is.EqualTo("8cf496087c8f"));
            Assert.That(read.Release, Is.False);
            Assert.That(text, Does.Not.Contain(Environment.UserName.ToLowerInvariant()));
            Assert.That(text, Does.Not.Contain(Environment.MachineName.ToLowerInvariant()));
            Assert.That(text, Does.Not.Contain("c:\\"));
            Assert.That(text, Does.Not.Contain("persistentdeviceid"));
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

        private static string FindRepositoryRoot()
        {
            string? current = TestContext.CurrentContext.TestDirectory;
            while (current != null && !File.Exists(Path.Combine(current, "AGENTS.md")))
            {
                DirectoryInfo? parent = Directory.GetParent(current);
                current = parent == null ? null : parent.FullName;
            }

            return current ?? throw new DirectoryNotFoundException("Repository root was not found.");
        }
    }
}
