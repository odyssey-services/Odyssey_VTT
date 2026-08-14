using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Odyssey.Application.Diagnostics;
using Odyssey.Application.Identity;
using Odyssey.Application.Serialization;
using Odyssey.Application.Versions;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Tests.Unit
{
    public sealed class DiagnosticSessionBundleTests
    {
        private static readonly DiagnosticId DiagnosticId = DiagnosticId.Parse("diag_00000000000000000000000000000001");

        [Test]
        public void DiagnosticSessionExpiresAtThirtyMinuteMaximum()
        {
            UtcInstant started = UtcInstant.Parse("2026-08-12T12:00:00.0000000Z");
            DiagnosticSession session = new DiagnosticSession(DiagnosticId, started, started.Add(TimeSpan.FromMinutes(30)));

            Assert.That(session.IsExpired(started.Add(TimeSpan.FromMinutes(29))), Is.False);
            Assert.That(session.IsExpired(started.Add(TimeSpan.FromMinutes(30))), Is.True);
            AssertArgumentException(() => new DiagnosticSession(DiagnosticId, started, started.Add(TimeSpan.FromMinutes(31))));
        }

        [Test]
        public void DiagnosticBundleRejectsSecretAndPrivateInputsBeforeManifestCreation()
        {
            AssertArgumentException(() => DiagnosticBundlePlanner.CreateManifest(DiagnosticId, BuildId(), new[] { Candidate(DiagnosticBundleCategory.DiagnosticLogs, "private/closed-notes.txt", "secret") }));
            AssertArgumentException(() => DiagnosticBundlePlanner.CreateManifest(DiagnosticId, BuildId(), new[] { Candidate(DiagnosticBundleCategory.DiagnosticLogs, "campaign-database.odcamp", "db") }));
            AssertArgumentException(() => DiagnosticBundlePlanner.CreateManifest(DiagnosticId, BuildId(), new[] { Candidate(DiagnosticBundleCategory.DiagnosticLogs, "logs/app.jsonl", "{\"message\":\"fake secret token\"}") }));
            AssertArgumentException(() => DiagnosticBundlePlanner.CreateManifest(DiagnosticId, BuildId(), new[] { (DiagnosticBundleCategory.DiagnosticLogs, "logs/app.jsonl", new byte[] { 0xff, 0xfe, 0xfd }) }));
            AssertArgumentException(() => DiagnosticBundlePlanner.CreateManifest(DiagnosticId, BuildId(), new[] { Candidate(DiagnosticBundleCategory.DiagnosticLogs, "logs/app.jsonl", "{\"apiKey\":\"ABC123\"}") }));
            AssertArgumentException(() => DiagnosticBundlePlanner.CreateManifest(DiagnosticId, BuildId(), new[] { Candidate(DiagnosticBundleCategory.DiagnosticLogs, "logs/app.jsonl", "{\"email\":\"alice@example.com\"}") }));
            AssertArgumentException(() => DiagnosticBundlePlanner.CreateManifest(DiagnosticId, BuildId(), new[] { Candidate(DiagnosticBundleCategory.DiagnosticLogs, "logs/app.jsonl", "{\"message\":\"meet me after the session\"}") }));
            AssertArgumentException(() => DiagnosticBundlePlanner.CreateManifest(DiagnosticId, BuildId(), new[] { Candidate(DiagnosticBundleCategory.DiagnosticLogs, "logs/app.jsonl", "{\"gmNote\":\"door code is 4815\"}") }));
        }

        [Test]
        public void DiagnosticBundleManifestRecordsCategoriesAndChecksums()
        {
            byte[] runtimeSummary = RuntimeSummary();
            byte[] buildIdentity = CanonicalBuildIdentity();
            byte[] diagnosticLog = CanonicalDiagnosticLog();
            DiagnosticBundleManifest manifest = DiagnosticBundlePlanner.CreateManifest(DiagnosticId, BuildId(), new[]
            {
                (DiagnosticBundleCategory.RuntimeSummary, "runtime/summary.json", runtimeSummary),
                (DiagnosticBundleCategory.BuildIdentity, "build/build-identity.json", buildIdentity),
                (DiagnosticBundleCategory.DiagnosticLogs, "logs/app.jsonl", diagnosticLog)
            });

            Assert.That(manifest.Entries.Select(entry => entry.Category), Is.EquivalentTo(new[] { DiagnosticBundleCategory.RuntimeSummary, DiagnosticBundleCategory.BuildIdentity, DiagnosticBundleCategory.DiagnosticLogs }));
            Assert.That(manifest.Entries.All(entry => entry.Sha256.Length == 64), Is.True);
            Assert.That(manifest.Entries.All(entry => entry.Status == DiagnosticBundleEntryStatus.Included), Is.True);
            Assert.That(manifest.Entries.Single(entry => entry.RelativePath == "runtime/summary.json").Sha256, Is.EqualTo(Sha256(runtimeSummary)));
            Assert.That(manifest.Entries.Single(entry => entry.RelativePath == "build/build-identity.json").Sha256, Is.EqualTo(Sha256(buildIdentity)));
            Assert.That(manifest.Entries.Single(entry => entry.RelativePath == "logs/app.jsonl").Sha256, Is.EqualTo(Sha256(diagnosticLog)));
        }

        [Test]
        public void DiagnosticBundleRejectsMalformedTamperedOrUnknownTypedPayloads()
        {
            byte[] buildIdentity = CanonicalBuildIdentity();
            string tamperedDigest = Encoding.UTF8.GetString(buildIdentity).Replace("\"compatibilityConfigDigest\":\"" + BuildIdentityCodec.ComputeCompatibilityDigest(StandardCompatibility()) + "\"", "\"compatibilityConfigDigest\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"");

            AssertArgumentException(() => DiagnosticBundlePlanner.CreateManifest(DiagnosticId, BuildId(), new[] { Candidate(DiagnosticBundleCategory.BuildIdentity, "build/build-identity.json", "{}") }));
            AssertArgumentException(() => DiagnosticBundlePlanner.CreateManifest(DiagnosticId, BuildId(), new[] { Candidate(DiagnosticBundleCategory.BuildIdentity, "build/build-identity.json", tamperedDigest) }));
            AssertArgumentException(() => DiagnosticBundlePlanner.CreateManifest(DiagnosticId, BuildId(), new[] { Candidate(DiagnosticBundleCategory.RuntimeSummary, "runtime/summary.json", "{\"contractType\":\"odyssey.diagnostics.runtime.summary\",\"contractVersion\":1,\"os\":\"windows\",\"unknownField\":\"x\"}") }));
            AssertArgumentException(() => DiagnosticBundlePlanner.CreateManifest(DiagnosticId, BuildId(), new[] { Candidate(DiagnosticBundleCategory.DiagnosticLogs, "logs/app.jsonl", "{\"contractType\":\"odyssey.diagnostics.log.event\"") }));
            AssertArgumentException(() => DiagnosticBundlePlanner.CreateManifest(DiagnosticId, BuildId(), new[] { (DiagnosticBundleCategory.UnityProjectSnapshot, "unity/project.json", RuntimeSummary()) }));
        }

        [Test]
        public void DiagnosticBundleTruncatesAtFiftyMiBAndReportsStoredSize()
        {
            byte[] oversized = OversizedCanonicalDiagnosticLog();
            DiagnosticBundleManifest manifest = DiagnosticBundlePlanner.CreateManifest(DiagnosticId, BuildId(), new[] { (DiagnosticBundleCategory.DiagnosticLogs, "logs/oversized.jsonl", oversized) });

            Assert.That(manifest.TotalStoredBytes, Is.EqualTo(DiagnosticBundleManifest.MaximumBundleBytes));
            Assert.That(manifest.Entries.Single().OriginalBytes, Is.GreaterThan(DiagnosticBundleManifest.MaximumBundleBytes));
            Assert.That(manifest.Entries.Single().StoredBytes, Is.EqualTo(DiagnosticBundleManifest.MaximumBundleBytes));
            Assert.That(manifest.Entries.Single().Status, Is.EqualTo(DiagnosticBundleEntryStatus.Truncated));
        }

        [Test]
        public void DiagnosticBundleRecordsAllRemainingCandidatesAsExcludedAfterSizeCap()
        {
            byte[] oversized = OversizedCanonicalDiagnosticLog();
            byte[] runtimeSummary = RuntimeSummary();
            byte[] buildIdentity = CanonicalBuildIdentity();
            DiagnosticBundleManifest manifest = DiagnosticBundlePlanner.CreateManifest(DiagnosticId, BuildId(), new[]
            {
                (DiagnosticBundleCategory.DiagnosticLogs, "logs/oversized.jsonl", oversized),
                (DiagnosticBundleCategory.RuntimeSummary, "runtime/summary.json", runtimeSummary),
                (DiagnosticBundleCategory.BuildIdentity, "build/build-identity.json", buildIdentity)
            });

            Assert.That(manifest.TotalStoredBytes, Is.EqualTo(DiagnosticBundleManifest.MaximumBundleBytes));
            Assert.That(manifest.Entries, Has.Count.EqualTo(3));
            Assert.That(manifest.Entries[0].Status, Is.EqualTo(DiagnosticBundleEntryStatus.Truncated));
            Assert.That(manifest.Entries[1].Status, Is.EqualTo(DiagnosticBundleEntryStatus.Excluded));
            Assert.That(manifest.Entries[1].OriginalBytes, Is.EqualTo(runtimeSummary.LongLength));
            Assert.That(manifest.Entries[1].StoredBytes, Is.EqualTo(0));
            Assert.That(manifest.Entries[1].Sha256, Is.EqualTo(Sha256(Array.Empty<byte>())));
            Assert.That(manifest.Entries[2].Status, Is.EqualTo(DiagnosticBundleEntryStatus.Excluded));
            Assert.That(manifest.Entries[2].OriginalBytes, Is.EqualTo(buildIdentity.LongLength));
            Assert.That(manifest.Entries[2].StoredBytes, Is.EqualTo(0));
        }

        [Test]
        public void DiagnosticBundleManifestNeverIncludesCampaignDatabaseOrPrivateDocumentationOrMachineIdentity()
        {
            DiagnosticBundleManifest manifest = DiagnosticBundlePlanner.CreateManifest(DiagnosticId, BuildId(), new[] { (DiagnosticBundleCategory.RuntimeSummary, "runtime/summary.json", RuntimeSummary()) });

            Assert.That(manifest.CampaignDatabaseIncluded, Is.False);
            Assert.That(manifest.PrivateDocumentationIncluded, Is.False);
            Assert.That(manifest.MachineIdentifierIncluded, Is.False);
            Assert.That(DiagnosticBundlePlanner.IsSafeSystemSummary("os=windows;unity=6000.4.0f1"), Is.True);
            Assert.That(DiagnosticBundlePlanner.IsSafeSystemSummary("machine=DEVBOX;username=alex"), Is.False);
            Assert.That(DiagnosticBundlePlanner.IsSafeSystemSummary("persistentDeviceId=abc"), Is.False);
        }

        [Test]
        public void DiagnosticBundleAcceptsCanonicalTypedPayloadsAndRejectsOpaqueBytes()
        {
            byte[] buildIdentity = CanonicalBuildIdentity();
            byte[] runtimeSummary = RuntimeSummary();
            byte[] diagnosticLog = CanonicalDiagnosticLog();
            DiagnosticBundleManifest manifest = DiagnosticBundlePlanner.CreateManifest(DiagnosticId, BuildId(), new[]
            {
                (DiagnosticBundleCategory.BuildIdentity, "build/build-identity.json", buildIdentity),
                (DiagnosticBundleCategory.RuntimeSummary, "runtime/summary.json", runtimeSummary),
                (DiagnosticBundleCategory.DiagnosticLogs, "logs/app.jsonl", diagnosticLog)
            });

            Assert.That(manifest.Entries.Single(entry => entry.RelativePath == "build/build-identity.json").Sha256, Is.EqualTo(Sha256(buildIdentity)));
            Assert.That(manifest.Entries.Single(entry => entry.RelativePath == "runtime/summary.json").Sha256, Is.EqualTo(Sha256(runtimeSummary)));
            Assert.That(manifest.Entries.Single(entry => entry.RelativePath == "logs/app.jsonl").Sha256, Is.EqualTo(Sha256(diagnosticLog)));
            AssertArgumentException(() => DiagnosticBundlePlanner.CreateManifest(DiagnosticId, BuildId(), new[] { (DiagnosticBundleCategory.RuntimeSummary, "runtime/summary.json", new byte[] { 0xff, 0xfe, 0xfd }) }));
        }

        private static (DiagnosticBundleCategory Category, string RelativePath, byte[] Content) Candidate(DiagnosticBundleCategory category, string path, string content)
        {
            return (category, path, Encoding.UTF8.GetBytes(content));
        }

        private static byte[] CanonicalBuildIdentity()
        {
            BuildIdentity identity = BuildIdentityCodec.Create(new VersionSource(ApplicationVersion.Parse("0.1.0")), StandardCompatibility(), BuildChannel.Local, 1, 1, "487df0fe97051541c3cdfce5253c8a2f7a70fa54", "heads/local", WorkingTreeState.Clean, "20260812T1200000000001Z", "6000.4.0f1", "8cf496087c8f", "10.0.302", "Development-Debug", "WindowsStandalone", "x86_64", "Mono", "NETStandard2.1");
            return BuildIdentityCodec.WriteBuildIdentity(identity).Value.Bytes;
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

        private static byte[] RuntimeSummary()
        {
            return new CanonicalJsonWriter()
                .StartObject()
                .String("contractType", "odyssey.diagnostics.runtime.summary")
                .Int32("contractVersion", 1)
                .String("os", "windows")
                .String("unityVersion", "6000.4.0f1")
                .String("dotnetSdkVersion", "10.0.302")
                .String("configuration", "Development-Debug")
                .String("target", "WindowsStandalone")
                .String("architecture", "x86_64")
                .String("scriptingBackend", "Mono")
                .String("apiCompatibilityLevel", "NETStandard2.1")
                .String("compatibilityConfigDigest", BuildIdentityCodec.ComputeCompatibilityDigest(StandardCompatibility()))
                .String("contractRegistryDigest", BuildIdentityCodec.ComputeContractRegistryDigest())
                .EndObject()
                .ToPayload()
                .Bytes;
        }

        private static byte[] CanonicalDiagnosticLog()
        {
            LogEventV1 logEvent = new LogEventV1(
                UtcInstant.Parse("2026-08-12T00:00:00.0000000Z"),
                LogLevel.Information,
                OdysseyEventCodes.DiagnosticsProbeEmitted,
                SubsystemName.Parse("diagnostics"),
                BuildIdAvailability.UnavailableNotYetComposed,
                ProcessInstanceId.Parse("proc_0123456789abcdef0123456789abcdef"),
                MessageTemplateKey.Parse("log.diagnostics.probe.emitted"),
                new[] { new SafeLogProperty(SafePropertyKey.Parse("probe"), SafeLogValue.Code("bundle")) },
                CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"));
            return new LogEventV1JsonCodec().Write(logEvent).Value.Bytes;
        }

        private static byte[] OversizedCanonicalDiagnosticLog()
        {
            string largeValue = new string('a', 900000);
            string largeLine = Encoding.UTF8.GetString(CanonicalDiagnosticLog()).Replace("\"renderedValue\":\"bundle\"", "\"renderedValue\":\"" + largeValue + "\"").Replace("\"valueKind\":\"code\"", "\"valueKind\":\"code\"");
            int repeat = (int)(DiagnosticBundleManifest.MaximumBundleBytes / Encoding.UTF8.GetByteCount(largeLine)) + 2;
            StringBuilder builder = new StringBuilder((largeLine.Length + 1) * repeat);
            for (int index = 0; index < repeat; index++)
            {
                if (index > 0) builder.Append('\n');
                builder.Append(largeLine);
            }

            return Encoding.UTF8.GetBytes(builder.ToString());
        }

        private static void AssertArgumentException(Action action)
        {
            Assert.Throws<ArgumentException>(action);
        }

        private static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static string BuildId()
        {
            return "odyssey-local-20260812t120000z-g0123456789ab";
        }
    }
}
