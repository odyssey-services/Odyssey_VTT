using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Odyssey.Application.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Diagnostics
{
    public enum DiagnosticBundleCategory
    {
        RuntimeSummary = 1,
        BuildIdentity = 2,
        DiagnosticLogs = 3,
        UnityProjectSnapshot = 4
    }

    public enum DiagnosticBundleEntryStatus
    {
        Included = 1,
        Excluded = 2,
        Truncated = 3
    }

    public sealed class DiagnosticSession
    {
        public const int MaximumDurationMinutes = 30;

        public DiagnosticSession(DiagnosticId diagnosticId, UtcInstant startedAtUtc, UtcInstant expiresAtUtc)
        {
            if (!diagnosticId.IsValid) throw new ArgumentException("DiagnosticId is required.", nameof(diagnosticId));
            if (!startedAtUtc.IsValid) throw new ArgumentException("StartedAtUtc is required.", nameof(startedAtUtc));
            if (!expiresAtUtc.IsValid) throw new ArgumentException("ExpiresAtUtc is required.", nameof(expiresAtUtc));
            if (expiresAtUtc.CompareTo(startedAtUtc) <= 0) throw new ArgumentException("Diagnostic session must expire after it starts.", nameof(expiresAtUtc));
            if (expiresAtUtc.Value - startedAtUtc.Value > TimeSpan.FromMinutes(MaximumDurationMinutes)) throw new ArgumentException("Diagnostic session duration is too long.", nameof(expiresAtUtc));
            DiagnosticId = diagnosticId;
            StartedAtUtc = startedAtUtc;
            ExpiresAtUtc = expiresAtUtc;
        }

        public DiagnosticId DiagnosticId { get; }
        public UtcInstant StartedAtUtc { get; }
        public UtcInstant ExpiresAtUtc { get; }
        public bool IsExpired(UtcInstant nowUtc)
        {
            if (!nowUtc.IsValid) throw new ArgumentException("NowUtc is required.", nameof(nowUtc));
            return nowUtc.CompareTo(ExpiresAtUtc) >= 0;
        }
    }

    public sealed class DiagnosticBundleEntry
    {
        public DiagnosticBundleEntry(DiagnosticBundleCategory category, string relativePath, long originalBytes, long storedBytes, DiagnosticBundleEntryStatus status, string sha256)
        {
            if (!Enum.IsDefined(typeof(DiagnosticBundleCategory), category)) throw new ArgumentOutOfRangeException(nameof(category));
            if (!DiagnosticBundleText.IsSafeRelativePath(relativePath)) throw new ArgumentException("RelativePath is not safe.", nameof(relativePath));
            if (originalBytes < 0) throw new ArgumentOutOfRangeException(nameof(originalBytes));
            if (storedBytes < 0 || storedBytes > originalBytes) throw new ArgumentOutOfRangeException(nameof(storedBytes));
            if (!Enum.IsDefined(typeof(DiagnosticBundleEntryStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if (!DiagnosticBundleText.IsSha256(sha256)) throw new ArgumentException("Sha256 is invalid.", nameof(sha256));
            Category = category;
            RelativePath = relativePath;
            OriginalBytes = originalBytes;
            StoredBytes = storedBytes;
            Status = status;
            Sha256 = sha256;
        }

        public DiagnosticBundleCategory Category { get; }
        public string RelativePath { get; }
        public long OriginalBytes { get; }
        public long StoredBytes { get; }
        public DiagnosticBundleEntryStatus Status { get; }
        public string Sha256 { get; }
    }

    public sealed class DiagnosticBundleManifest
    {
        public const long MaximumBundleBytes = 50L * 1024L * 1024L;

        public DiagnosticBundleManifest(DiagnosticId diagnosticId, string buildId, IReadOnlyList<DiagnosticBundleEntry> entries, long totalStoredBytes, bool campaignDatabaseIncluded, bool privateDocumentationIncluded, bool machineIdentifierIncluded)
        {
            if (!diagnosticId.IsValid) throw new ArgumentException("DiagnosticId is required.", nameof(diagnosticId));
            if (!DiagnosticBundleText.IsSafeBuildId(buildId)) throw new ArgumentException("BuildId is invalid.", nameof(buildId));
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (totalStoredBytes < 0 || totalStoredBytes > MaximumBundleBytes) throw new ArgumentOutOfRangeException(nameof(totalStoredBytes));
            if (campaignDatabaseIncluded) throw new ArgumentException("Campaign database files are outside MVP diagnostic bundles.", nameof(campaignDatabaseIncluded));
            if (privateDocumentationIncluded) throw new ArgumentException("Private documentation must not enter diagnostic bundles.", nameof(privateDocumentationIncluded));
            if (machineIdentifierIncluded) throw new ArgumentException("Machine identifiers must not enter diagnostic bundles.", nameof(machineIdentifierIncluded));

            List<DiagnosticBundleEntry> copy = new List<DiagnosticBundleEntry>(entries);
            long summed = 0;
            for (int index = 0; index < copy.Count; index++)
            {
                if (copy[index] == null) throw new ArgumentException("Entries cannot contain null.", nameof(entries));
                summed += copy[index].StoredBytes;
            }

            if (summed != totalStoredBytes) throw new ArgumentException("TotalStoredBytes must match entry sizes.", nameof(totalStoredBytes));
            DiagnosticId = diagnosticId;
            BuildId = buildId;
            Entries = copy.AsReadOnly();
            TotalStoredBytes = totalStoredBytes;
            CampaignDatabaseIncluded = false;
            PrivateDocumentationIncluded = false;
            MachineIdentifierIncluded = false;
        }

        public DiagnosticId DiagnosticId { get; }
        public string BuildId { get; }
        public IReadOnlyList<DiagnosticBundleEntry> Entries { get; }
        public long TotalStoredBytes { get; }
        public bool CampaignDatabaseIncluded { get; }
        public bool PrivateDocumentationIncluded { get; }
        public bool MachineIdentifierIncluded { get; }
    }

    public static class DiagnosticBundlePlanner
    {
        public static DiagnosticBundleManifest CreateManifest(DiagnosticId diagnosticId, string buildId, IReadOnlyList<(DiagnosticBundleCategory Category, string RelativePath, byte[] Content)> candidates)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            List<DiagnosticBundleEntry> entries = new List<DiagnosticBundleEntry>();
            long remaining = DiagnosticBundleManifest.MaximumBundleBytes;
            long totalStored = 0;
            for (int index = 0; index < candidates.Count; index++)
            {
                byte[] content = candidates[index].Content ?? throw new ArgumentException("Candidate content cannot be null.", nameof(candidates));
                if (DiagnosticBundleText.IsForbiddenPath(candidates[index].RelativePath)) throw new ArgumentException("Candidate path is forbidden.", nameof(candidates));
                long stored = Math.Min(content.LongLength, remaining);
                DiagnosticBundleEntryStatus status = stored == content.LongLength ? DiagnosticBundleEntryStatus.Included : DiagnosticBundleEntryStatus.Truncated;
                byte[] storedBytes = new byte[(int)stored];
                Array.Copy(content, storedBytes, stored);
                string digest = Sha256(storedBytes);
                entries.Add(new DiagnosticBundleEntry(candidates[index].Category, candidates[index].RelativePath, content.LongLength, stored, status, digest));
                totalStored += stored;
                remaining -= stored;
                if (remaining == 0) break;
            }

            return new DiagnosticBundleManifest(diagnosticId, buildId, entries, totalStored, false, false, false);
        }

        public static bool IsSafeSystemSummary(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary)) return false;
            string lower = summary.ToLowerInvariant();
            return !lower.Contains("username") &&
                !lower.Contains("machine") &&
                !lower.Contains("deviceid") &&
                !lower.Contains("persistentdeviceid") &&
                !lower.Contains("c:\\") &&
                !lower.Contains("users/");
        }

        private static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(bytes);
                StringBuilder builder = new StringBuilder(digest.Length * 2);
                for (int index = 0; index < digest.Length; index++) builder.Append(digest[index].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }
    }

    internal static class DiagnosticBundleText
    {
        internal static bool IsSafeBuildId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 160 || !value.StartsWith("odyssey-", StringComparison.Ordinal)) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '-')) return false;
            }

            return true;
        }

        internal static bool IsSafeRelativePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 160 || value.StartsWith("/", StringComparison.Ordinal) || value.Contains("\\") || value.Contains("..")) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '-' || c == '_' || c == '/')) return false;
            }

            return true;
        }

        internal static bool IsForbiddenPath(string value)
        {
            string lower = value.ToLowerInvariant();
            return lower.EndsWith(".odcamp", StringComparison.Ordinal) ||
                lower.Contains("database") ||
                lower.Contains("documentation/") ||
                lower.Contains("legacyreference/") ||
                lower.Contains("private");
        }

        internal static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            }

            return true;
        }
    }
}
