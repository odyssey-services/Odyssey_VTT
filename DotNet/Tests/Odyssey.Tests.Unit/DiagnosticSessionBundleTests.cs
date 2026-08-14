using System;
using System.Linq;
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
        }

        [Test]
        public void DiagnosticBundleTruncatesAtFiftyMiBAndReportsStoredSize()
        {
            byte[] oversized = new byte[(int)DiagnosticBundleManifest.MaximumBundleBytes + 1];
            DiagnosticBundleManifest manifest = DiagnosticBundlePlanner.CreateManifest(DiagnosticId, BuildId(), new[] { (DiagnosticBundleCategory.DiagnosticLogs, "logs/oversized.jsonl", oversized) });

            Assert.That(manifest.TotalStoredBytes, Is.EqualTo(DiagnosticBundleManifest.MaximumBundleBytes));
            Assert.That(manifest.Entries.Single().OriginalBytes, Is.EqualTo(DiagnosticBundleManifest.MaximumBundleBytes + 1));
            Assert.That(manifest.Entries.Single().StoredBytes, Is.EqualTo(DiagnosticBundleManifest.MaximumBundleBytes));
            Assert.That(manifest.Entries.Single().Status, Is.EqualTo(DiagnosticBundleEntryStatus.Truncated));
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

        private static string BuildId()
        {
            return "odyssey-local-20260812t120000z-g0123456789ab";
        }
    }
}
