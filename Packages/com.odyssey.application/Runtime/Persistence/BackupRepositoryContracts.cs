using System;
using System.Collections.Generic;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// ADR-012 section 8 snapshot contract / ADR-011 section 4.1 Backups/ tree.
    /// ODY-S01-011: manual backup creation, listing, and restore-into-a-new-copy.
    /// Not implemented here: Full backup composition (Assets/ + checksums.json --
    /// backlog's scope text for this task never mentions Full, only "snapshot
    /// creation via SQLite Backup API" and the recent/daily/weekly rotation of
    /// that snapshot), the GM Override/pre-migration/pre-maintenance snapshot
    /// triggers (ADR-012 section 8.2 items 5-7 -- no migration runner or GM
    /// Override exists yet to trigger from), and the Emergency backup tier.
    /// </summary>
    public interface IBackupRepository
    {
        Result<BackupRecord> CreateBackup(CampaignHandle campaign, string reason, CorrelationId correlationId);

        /// <summary>
        /// Takes a plain campaign folder path, not a <see cref="CampaignHandle"/>,
        /// deliberately: listing/restoring must keep working even when the
        /// campaign's own campaign.db cannot be opened (a corrupted database is
        /// exactly the scenario disaster recovery exists for) -- see
        /// <c>SqliteBackupRepository</c>'s remarks for the filesystem-based
        /// discovery design this enables.
        /// </summary>
        Result<IReadOnlyList<BackupRecord>> ListBackups(string campaignFolderPath, CorrelationId correlationId);

        /// <summary>
        /// Restores the given backup into a brand-new campaign directory under
        /// <paramref name="destinationParentDirectory"/>. Never writes into or
        /// overwrites the source campaign's own directory (05_Persistence's
        /// restore-into-separate-copy safety principle).
        /// </summary>
        Result<string> RestoreBackup(string campaignFolderPath, BackupId backupId, string destinationParentDirectory, CorrelationId correlationId);
    }

    /// <summary>
    /// ADR-012 section 8.7 / 05_Persistence section 21.2 minimal mandatory schema.
    /// </summary>
    public sealed class BackupRecord
    {
        public BackupRecord(
            BackupId backupId,
            CampaignId campaignId,
            string backupKind,
            string reason,
            long campaignRevision,
            long eventSequence,
            string databaseSchemaVersion,
            string campaignFormatVersion,
            string rulesetRef,
            UtcInstant createdAt,
            string relativePath,
            string databaseHash,
            long sizeBytes,
            string integrityStatus)
        {
            if (!backupId.IsValid) throw new ArgumentException("BackupId is required.", nameof(backupId));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (string.IsNullOrWhiteSpace(backupKind)) throw new ArgumentException("BackupKind is required.", nameof(backupKind));
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason is required.", nameof(reason));
            if (campaignRevision < 0) throw new ArgumentOutOfRangeException(nameof(campaignRevision));
            if (eventSequence < 0) throw new ArgumentOutOfRangeException(nameof(eventSequence));
            if (string.IsNullOrWhiteSpace(databaseSchemaVersion)) throw new ArgumentException("DatabaseSchemaVersion is required.", nameof(databaseSchemaVersion));
            if (string.IsNullOrWhiteSpace(campaignFormatVersion)) throw new ArgumentException("CampaignFormatVersion is required.", nameof(campaignFormatVersion));
            if (string.IsNullOrWhiteSpace(rulesetRef)) throw new ArgumentException("RulesetRef is required.", nameof(rulesetRef));
            if (string.IsNullOrWhiteSpace(relativePath)) throw new ArgumentException("RelativePath is required.", nameof(relativePath));
            if (string.IsNullOrWhiteSpace(databaseHash)) throw new ArgumentException("DatabaseHash is required.", nameof(databaseHash));
            if (sizeBytes < 0) throw new ArgumentOutOfRangeException(nameof(sizeBytes));
            if (string.IsNullOrWhiteSpace(integrityStatus)) throw new ArgumentException("IntegrityStatus is required.", nameof(integrityStatus));

            BackupId = backupId;
            CampaignId = campaignId;
            BackupKind = backupKind;
            Reason = reason;
            CampaignRevision = campaignRevision;
            EventSequence = eventSequence;
            DatabaseSchemaVersion = databaseSchemaVersion;
            CampaignFormatVersion = campaignFormatVersion;
            RulesetRef = rulesetRef;
            CreatedAt = createdAt;
            RelativePath = relativePath;
            DatabaseHash = databaseHash;
            SizeBytes = sizeBytes;
            IntegrityStatus = integrityStatus;
        }

        public BackupId BackupId { get; }
        public CampaignId CampaignId { get; }
        public string BackupKind { get; }
        public string Reason { get; }
        public long CampaignRevision { get; }
        public long EventSequence { get; }
        public string DatabaseSchemaVersion { get; }
        public string CampaignFormatVersion { get; }
        public string RulesetRef { get; }
        public UtcInstant CreatedAt { get; }
        public string RelativePath { get; }
        public string DatabaseHash { get; }
        public long SizeBytes { get; }
        public string IntegrityStatus { get; }
    }

    /// <summary>
    /// ADR-012 section 8.5: rotation numbers are a configurable, product-settable
    /// baseline, not a hard ADR contract. Defaults are 05_Persistence section
    /// 21.3's exact published baseline (10 fast / 7 daily / 4 weekly).
    /// </summary>
    public sealed class BackupRotationPolicy
    {
        public static readonly BackupRotationPolicy Default = new BackupRotationPolicy(fastRetentionCount: 10, dailyRetentionCount: 7, weeklyRetentionCount: 4);

        public BackupRotationPolicy(int fastRetentionCount, int dailyRetentionCount, int weeklyRetentionCount)
        {
            if (fastRetentionCount < 1) throw new ArgumentOutOfRangeException(nameof(fastRetentionCount));
            if (dailyRetentionCount < 1) throw new ArgumentOutOfRangeException(nameof(dailyRetentionCount));
            if (weeklyRetentionCount < 1) throw new ArgumentOutOfRangeException(nameof(weeklyRetentionCount));

            FastRetentionCount = fastRetentionCount;
            DailyRetentionCount = dailyRetentionCount;
            WeeklyRetentionCount = weeklyRetentionCount;
        }

        public int FastRetentionCount { get; }
        public int DailyRetentionCount { get; }
        public int WeeklyRetentionCount { get; }
    }
}
