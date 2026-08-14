using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Odyssey.Application.Diagnostics;
using Odyssey.Application.Identity;
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
        }

        [Test]
        public void DiagnosticBundleManifestRecordsCategoriesAndChecksums()
        {
            DiagnosticBundleManifest manifest = DiagnosticBundlePlanner.CreateManifest(DiagnosticId, BuildId(), new[]
            {
                Candidate(DiagnosticBundleCategory.RuntimeSummary, "runtime/summary.txt", "runtime"),
                Candidate(DiagnosticBundleCategory.BuildIdentity, "build/build-identity.json", "{}"),
                Candidate(DiagnosticBundleCategory.DiagnosticLogs, "logs/app.jsonl", "{}")
            });

            Assert.That(manifest.Entries.Select(entry => entry.Category), Is.EquivalentTo(new[] { DiagnosticBundleCategory.RuntimeSummary, DiagnosticBundleCategory.BuildIdentity, DiagnosticBundleCategory.DiagnosticLogs }));
            Assert.That(manifest.Entries.All(entry => entry.Sha256.Length == 64), Is.True);
            Assert.That(manifest.Entries.All(entry => entry.Status == DiagnosticBundleEntryStatus.Included), Is.True);
            Assert.That(manifest.Entries.Single(entry => entry.RelativePath == "runtime/summary.txt").Sha256, Is.EqualTo(Sha256(Encoding.UTF8.GetBytes("runtime"))));
        }

        [Test]
        public void DiagnosticBundleTruncatesAtFiftyMiBAndReportsStoredSize()
        {
            byte[] oversized = Encoding.UTF8.GetBytes(new string('a', (int)DiagnosticBundleManifest.MaximumBundleBytes + 1));
            DiagnosticBundleManifest manifest = DiagnosticBundlePlanner.CreateManifest(DiagnosticId, BuildId(), new[] { (DiagnosticBundleCategory.DiagnosticLogs, "logs/oversized.jsonl", oversized) });

            Assert.That(manifest.TotalStoredBytes, Is.EqualTo(DiagnosticBundleManifest.MaximumBundleBytes));
            Assert.That(manifest.Entries.Single().OriginalBytes, Is.EqualTo(DiagnosticBundleManifest.MaximumBundleBytes + 1));
            Assert.That(manifest.Entries.Single().StoredBytes, Is.EqualTo(DiagnosticBundleManifest.MaximumBundleBytes));
            Assert.That(manifest.Entries.Single().Status, Is.EqualTo(DiagnosticBundleEntryStatus.Truncated));
        }

        [Test]
        public void DiagnosticBundleRecordsAllRemainingCandidatesAsExcludedAfterSizeCap()
        {
            byte[] oversized = Encoding.UTF8.GetBytes(new string('a', (int)DiagnosticBundleManifest.MaximumBundleBytes + 1));
            DiagnosticBundleManifest manifest = DiagnosticBundlePlanner.CreateManifest(DiagnosticId, BuildId(), new[]
            {
                (DiagnosticBundleCategory.DiagnosticLogs, "logs/oversized.jsonl", oversized),
                Candidate(DiagnosticBundleCategory.RuntimeSummary, "runtime/summary.txt", "os=windows"),
                Candidate(DiagnosticBundleCategory.BuildIdentity, "build/build-identity.json", "{}")
            });

            Assert.That(manifest.TotalStoredBytes, Is.EqualTo(DiagnosticBundleManifest.MaximumBundleBytes));
            Assert.That(manifest.Entries, Has.Count.EqualTo(3));
            Assert.That(manifest.Entries[0].Status, Is.EqualTo(DiagnosticBundleEntryStatus.Truncated));
            Assert.That(manifest.Entries[1].Status, Is.EqualTo(DiagnosticBundleEntryStatus.Excluded));
            Assert.That(manifest.Entries[1].OriginalBytes, Is.EqualTo(10));
            Assert.That(manifest.Entries[1].StoredBytes, Is.EqualTo(0));
            Assert.That(manifest.Entries[1].Sha256, Is.EqualTo(Sha256(Array.Empty<byte>())));
            Assert.That(manifest.Entries[2].Status, Is.EqualTo(DiagnosticBundleEntryStatus.Excluded));
            Assert.That(manifest.Entries[2].OriginalBytes, Is.EqualTo(2));
            Assert.That(manifest.Entries[2].StoredBytes, Is.EqualTo(0));
        }

        [Test]
        public void DiagnosticBundleManifestNeverIncludesCampaignDatabaseOrPrivateDocumentationOrMachineIdentity()
        {
            DiagnosticBundleManifest manifest = DiagnosticBundlePlanner.CreateManifest(DiagnosticId, BuildId(), new[] { Candidate(DiagnosticBundleCategory.RuntimeSummary, "runtime/summary.txt", "os=windows;unity=6000.4.0f1") });

            Assert.That(manifest.CampaignDatabaseIncluded, Is.False);
            Assert.That(manifest.PrivateDocumentationIncluded, Is.False);
            Assert.That(manifest.MachineIdentifierIncluded, Is.False);
            Assert.That(DiagnosticBundlePlanner.IsSafeSystemSummary("os=windows;unity=6000.4.0f1"), Is.True);
            Assert.That(DiagnosticBundlePlanner.IsSafeSystemSummary("machine=DEVBOX;username=alex"), Is.False);
            Assert.That(DiagnosticBundlePlanner.IsSafeSystemSummary("persistentDeviceId=abc"), Is.False);
        }

        private static (DiagnosticBundleCategory Category, string RelativePath, byte[] Content) Candidate(DiagnosticBundleCategory category, string path, string content)
        {
            return (category, path, Encoding.UTF8.GetBytes(content));
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
