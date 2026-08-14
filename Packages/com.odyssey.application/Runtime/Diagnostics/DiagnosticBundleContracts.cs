using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Odyssey.Application.Identity;
using Odyssey.Application.Serialization;
using Odyssey.Application.Versions;
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
                byte[] sanitized = DiagnosticBundleText.RequireSafeContent(candidates[index].Category, content);
                if (remaining == 0)
                {
                    entries.Add(new DiagnosticBundleEntry(candidates[index].Category, candidates[index].RelativePath, content.LongLength, 0, DiagnosticBundleEntryStatus.Excluded, Sha256(Array.Empty<byte>())));
                    continue;
                }

                long stored = Math.Min(sanitized.LongLength, remaining);
                DiagnosticBundleEntryStatus status = stored == sanitized.LongLength ? DiagnosticBundleEntryStatus.Included : DiagnosticBundleEntryStatus.Truncated;
                byte[] storedBytes = new byte[(int)stored];
                Array.Copy(sanitized, storedBytes, stored);
                string digest = Sha256(storedBytes);
                entries.Add(new DiagnosticBundleEntry(candidates[index].Category, candidates[index].RelativePath, content.LongLength, stored, status, digest));
                totalStored += stored;
                remaining -= stored;
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
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal static byte[] RequireSafeContent(DiagnosticBundleCategory category, byte[] content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            byte[] canonical;
            switch (category)
            {
                case DiagnosticBundleCategory.BuildIdentity:
                    canonical = CanonicalizeBuildIdentity(content);
                    break;
                case DiagnosticBundleCategory.RuntimeSummary:
                    canonical = CanonicalizeRuntimeSummary(content);
                    break;
                case DiagnosticBundleCategory.DiagnosticLogs:
                    canonical = CanonicalizeDiagnosticLogs(content);
                    break;
                default:
                    throw new ArgumentException("Diagnostic bundle category is not export-safe.", nameof(category));
            }

            if (!PassesFinalExportSafetyScan(canonical)) throw new ArgumentException("Diagnostic bundle content is not export-safe.", nameof(content));
            return canonical;
        }

        private static byte[] CanonicalizeBuildIdentity(byte[] content)
        {
            Odyssey.Application.Results.Result<BuildIdentity> identity = BuildIdentityCodec.ReadBuildIdentity(content);
            if (identity.IsFailure) throw new ArgumentException("BuildIdentity payload is invalid.", nameof(content));
            Odyssey.Application.Results.Result<JsonPayload> canonical = BuildIdentityCodec.WriteBuildIdentity(identity.Value);
            if (canonical.IsFailure) throw new ArgumentException("BuildIdentity payload cannot be canonicalized.", nameof(content));
            return canonical.Value.Bytes;
        }

        private static byte[] CanonicalizeRuntimeSummary(byte[] content)
        {
            Odyssey.Application.Results.Result<JsonObjectReader> reader = JsonObjectReader.Read(content, JsonPayloadLimits.DiagnosticRecordBytes);
            if (reader.IsFailure) throw new ArgumentException("Runtime summary payload is invalid.", nameof(content));
            Odyssey.Application.Results.Result schema = reader.Value.EnsureOnly(
                "contractType",
                "contractVersion",
                "os",
                "unityVersion",
                "dotnetSdkVersion",
                "configuration",
                "target",
                "architecture",
                "scriptingBackend",
                "apiCompatibilityLevel",
                "compatibilityConfigDigest",
                "contractRegistryDigest");
            if (schema.IsFailure) throw new ArgumentException("Runtime summary has unknown fields.", nameof(content));

            string contractType = RequiredString(reader.Value, "contractType", content);
            int contractVersion = RequiredInt32(reader.Value, "contractVersion", content);
            string os = RequiredToken(reader.Value, "os", content);
            string unityVersion = RequiredVersionToken(reader.Value, "unityVersion", content);
            string dotnetSdkVersion = RequiredVersionToken(reader.Value, "dotnetSdkVersion", content);
            string configuration = RequiredToken(reader.Value, "configuration", content);
            string target = RequiredToken(reader.Value, "target", content);
            string architecture = RequiredToken(reader.Value, "architecture", content);
            string scriptingBackend = RequiredToken(reader.Value, "scriptingBackend", content);
            string apiCompatibilityLevel = RequiredVersionToken(reader.Value, "apiCompatibilityLevel", content);
            string compatibilityConfigDigest = RequiredSha256(reader.Value, "compatibilityConfigDigest", content);
            string contractRegistryDigest = RequiredSha256(reader.Value, "contractRegistryDigest", content);

            if (contractType != "odyssey.diagnostics.runtime.summary" || contractVersion != 1) throw new ArgumentException("Runtime summary contract is unsupported.", nameof(content));
            return new CanonicalJsonWriter()
                .StartObject()
                .String("contractType", contractType)
                .Int32("contractVersion", contractVersion)
                .String("os", os)
                .String("unityVersion", unityVersion)
                .String("dotnetSdkVersion", dotnetSdkVersion)
                .String("configuration", configuration)
                .String("target", target)
                .String("architecture", architecture)
                .String("scriptingBackend", scriptingBackend)
                .String("apiCompatibilityLevel", apiCompatibilityLevel)
                .String("compatibilityConfigDigest", compatibilityConfigDigest)
                .String("contractRegistryDigest", contractRegistryDigest)
                .EndObject()
                .ToPayload()
                .Bytes;
        }

        private static byte[] CanonicalizeDiagnosticLogs(byte[] content)
        {
            string text = DecodeStrictUtf8(content);
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Diagnostic logs cannot be empty.", nameof(content));
            string normalized = text.Replace("\r\n", "\n");
            if (normalized.Contains("\r")) throw new ArgumentException("Diagnostic logs must use LF-delimited JSONL.", nameof(content));
            string[] lines = normalized.Split('\n');
            LogEventV1JsonCodec codec = new LogEventV1JsonCodec();
            List<byte> bytes = new List<byte>(content.Length);
            for (int index = 0; index < lines.Length; index++)
            {
                if (lines[index].Length == 0)
                {
                    if (index == lines.Length - 1) continue;
                    throw new ArgumentException("Diagnostic logs must not contain blank lines.", nameof(content));
                }

                byte[] lineBytes = StrictUtf8.GetBytes(lines[index]);
                Odyssey.Application.Results.Result<LogEventV1> parsed = codec.Read(lineBytes);
                if (parsed.IsFailure) throw new ArgumentException("Diagnostic log record is invalid.", nameof(content));
                Odyssey.Application.Results.Result<JsonPayload> canonical = codec.Write(parsed.Value);
                if (canonical.IsFailure) throw new ArgumentException("Diagnostic log record cannot be canonicalized.", nameof(content));
                if (!PassesFinalExportSafetyScan(canonical.Value.Bytes)) throw new ArgumentException("Diagnostic log record is not export-safe.", nameof(content));
                if (bytes.Count > 0) bytes.Add((byte)'\n');
                bytes.AddRange(canonical.Value.Bytes);
            }

            if (bytes.Count == 0) throw new ArgumentException("Diagnostic logs cannot be empty.", nameof(content));
            return bytes.ToArray();
        }

        private static string DecodeStrictUtf8(byte[] content)
        {
            try
            {
                return StrictUtf8.GetString(content);
            }
            catch (DecoderFallbackException ex)
            {
                throw new ArgumentException("Diagnostic bundle content must be valid UTF-8.", nameof(content), ex);
            }
        }

        private static string RequiredString(JsonObjectReader reader, string name, byte[] content)
        {
            Odyssey.Application.Results.Result<string> result = reader.RequiredString(name);
            if (result.IsFailure) throw new ArgumentException("Runtime summary is missing a required field.", nameof(content));
            return result.Value;
        }

        private static int RequiredInt32(JsonObjectReader reader, string name, byte[] content)
        {
            Odyssey.Application.Results.Result<int> result = reader.RequiredInt32(name);
            if (result.IsFailure) throw new ArgumentException("Runtime summary has an invalid integer field.", nameof(content));
            return result.Value;
        }

        private static string RequiredToken(JsonObjectReader reader, string name, byte[] content)
        {
            string value = RequiredString(reader, name, content);
            if (!IsSafeToken(value)) throw new ArgumentException("Runtime summary token is invalid.", nameof(content));
            return value;
        }

        private static string RequiredVersionToken(JsonObjectReader reader, string name, byte[] content)
        {
            string value = RequiredString(reader, name, content);
            if (!IsSafeVersionToken(value)) throw new ArgumentException("Runtime summary version token is invalid.", nameof(content));
            return value;
        }

        private static string RequiredSha256(JsonObjectReader reader, string name, byte[] content)
        {
            string value = RequiredString(reader, name, content);
            if (!IsSha256(value)) throw new ArgumentException("Runtime summary digest is invalid.", nameof(content));
            return value;
        }

        private static bool PassesFinalExportSafetyScan(byte[] content)
        {
            string value = DecodeStrictUtf8(content);
            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (char.IsControl(c) && c != '\n' && c != '\t') return false;
            }

            string lower = value.ToLowerInvariant();
            if (lower.Contains("secret") ||
                lower.Contains("token") ||
                lower.Contains("password") ||
                lower.Contains("credential") ||
                lower.Contains("apikey") ||
                lower.Contains("api_key") ||
                lower.Contains("email") ||
                lower.Contains("account") ||
                lower.Contains("private") ||
                lower.Contains("gmnote") ||
                lower.Contains("door code") ||
                lower.Contains("hiddengameplay") ||
                lower.Contains("hidden") ||
                lower.Contains("personal") ||
                lower.Contains("username") ||
                lower.Contains("machine") ||
                lower.Contains("serial") ||
                lower.Contains("deviceid") ||
                lower.Contains("persistentdeviceid") ||
                lower.Contains("c:\\") ||
                lower.Contains("/users/") ||
                ContainsEmailLikeValue(lower))
            {
                return false;
            }

            return true;
        }

        private static bool IsSafeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 80 || value.Trim() != value) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '-' || c == '_')) return false;
            }

            return true;
        }

        private static bool IsSafeVersionToken(string value)
        {
            if (!IsSafeToken(value)) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '.' || c == '-' || c == '_')) return false;
            }

            return true;
        }

        private static bool ContainsEmailLikeValue(string value)
        {
            int at = value.IndexOf('@');
            if (at <= 0 || at >= value.Length - 1) return false;
            int dotAfterAt = value.IndexOf('.', at + 2);
            return dotAfterAt > at + 1 && dotAfterAt < value.Length - 1;
        }

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
