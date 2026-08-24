using System;
using System.Collections.Generic;

namespace Odyssey.Persistence.Sqlite
{
    /// <summary>
    /// ODY-S01-010: the minimal migration registry baseline per
    /// SLICE-01_IMPLEMENTATION_BACKLOG.md section 2.1 -- a well-formed,
    /// versioned list of registered migrations (ADR-013 section 4), not the
    /// migration runner itself (temp-copy execution, transactional steps,
    /// rollback, read-only compatibility mode are ODY-S01-013+ scope).
    ///
    /// Today the registry holds exactly one entry, <see cref="Initial"/>: the
    /// identity migration that does not change any schema (a freshly created
    /// campaign starts directly on <see cref="SqliteCampaignRepository.DatabaseSchemaVersion"/>)
    /// but is still registered in <c>SchemaHistory</c> as the formal record that
    /// the campaign's history began on that version.
    /// </summary>
    public sealed class MigrationDescriptor
    {
        public MigrationDescriptor(string migrationId, string fromVersion, string toVersion, string codeChecksum)
        {
            if (string.IsNullOrWhiteSpace(migrationId)) throw new ArgumentException("MigrationId is required.", nameof(migrationId));
            if (string.IsNullOrWhiteSpace(fromVersion)) throw new ArgumentException("FromVersion is required.", nameof(fromVersion));
            if (string.IsNullOrWhiteSpace(toVersion)) throw new ArgumentException("ToVersion is required.", nameof(toVersion));
            if (string.IsNullOrWhiteSpace(codeChecksum)) throw new ArgumentException("CodeChecksum is required.", nameof(codeChecksum));

            MigrationId = migrationId;
            FromVersion = fromVersion;
            ToVersion = toVersion;
            CodeChecksum = codeChecksum;
        }

        public string MigrationId { get; }
        public string FromVersion { get; }
        public string ToVersion { get; }
        public string CodeChecksum { get; }
    }

    public static class MigrationRegistry
    {
        /// <summary>
        /// The identity migration: FromVersion == ToVersion == the version every
        /// newly created campaign starts on. CodeChecksum is a fixed, immutable
        /// value for this entry (ADR-013 section 4: "immutable identifier, not
        /// reused or renamed after publication") -- there is no migration
        /// script/code to hash for an identity entry, so the checksum covers the
        /// entry's own canonical identity string instead.
        /// </summary>
        public static readonly MigrationDescriptor Initial = new MigrationDescriptor(
            "0001_Initial",
            SqliteCampaignRepository.DatabaseSchemaVersion,
            SqliteCampaignRepository.DatabaseSchemaVersion,
            ComputeChecksum("0001_Initial|identity|" + SqliteCampaignRepository.DatabaseSchemaVersion));

        public static readonly IReadOnlyList<MigrationDescriptor> Registered = new[] { Initial };

        private static string ComputeChecksum(string canonicalContent)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            byte[] hashBytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(canonicalContent));
            var builder = new System.Text.StringBuilder(hashBytes.Length * 2);
            for (int index = 0; index < hashBytes.Length; index++)
            {
                builder.Append(hashBytes[index].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
