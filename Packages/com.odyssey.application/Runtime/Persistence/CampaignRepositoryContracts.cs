using System;
using Odyssey.Application.Commands;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// ADR-001 section 6.5 / section 10: repository interfaces are Application ports;
    /// Odyssey.Persistence supplies the implementation. This port never exposes a raw
    /// SQLite connection or throws a raw provider exception (ADR-004).
    ///
    /// ODY-S01-009: <c>Create</c> takes the caller-supplied <see cref="CommandId"/>
    /// (the same idempotency-key type <c>Odyssey.Application.Commands</c> already
    /// defines) because ADR-012 section 7 idempotency is a property of the command
    /// the caller is retrying, not something the repository can invent on its own --
    /// redelivering the exact same <see cref="CommandId"/> is what makes a retry safe.
    /// This is a breaking change to the ODY-S01-007 signature; see the ODY-S01-009
    /// task contract section 6 for the full justification and blast-radius check.
    /// </summary>
    public interface ICampaignRepository
    {
        Result<CampaignHandle> Create(CreateCampaignRequest request, CommandId commandId, CorrelationId correlationId);
        Result<CampaignHandle> Open(string campaignFolderPath, CorrelationId correlationId);
        Result Close(CampaignHandle handle, CorrelationId correlationId);
    }

    public sealed class CreateCampaignRequest
    {
        public CreateCampaignRequest(string campaignFolderPath, string campaignName, string rulesetId, string rulesetVersion, string applicationVersion)
        {
            if (string.IsNullOrWhiteSpace(campaignFolderPath)) throw new ArgumentException("Campaign folder path is required.", nameof(campaignFolderPath));
            if (string.IsNullOrWhiteSpace(campaignName) || campaignName.Length > 128) throw new ArgumentException("CampaignName is not safe.", nameof(campaignName));
            if (string.IsNullOrWhiteSpace(rulesetId)) throw new ArgumentException("RulesetId is required.", nameof(rulesetId));
            if (string.IsNullOrWhiteSpace(rulesetVersion)) throw new ArgumentException("RulesetVersion is required.", nameof(rulesetVersion));
            if (string.IsNullOrWhiteSpace(applicationVersion)) throw new ArgumentException("ApplicationVersion is required.", nameof(applicationVersion));

            CampaignFolderPath = campaignFolderPath;
            CampaignName = campaignName;
            RulesetId = rulesetId;
            RulesetVersion = rulesetVersion;
            ApplicationVersion = applicationVersion;
        }

        public string CampaignFolderPath { get; }
        public string CampaignName { get; }
        public string RulesetId { get; }
        public string RulesetVersion { get; }
        public string ApplicationVersion { get; }
    }

    /// <summary>
    /// Application-safe handle to an open campaign. Carries identity and the manifest
    /// snapshot only — never a live SQLite connection (ADR-001 section 6.5: Persistence
    /// owns the connection; Application never references it directly).
    /// </summary>
    public sealed class CampaignHandle
    {
        public CampaignHandle(CampaignId campaignId, CampaignPublicId campaignPublicId, string rootPath, CampaignManifest manifest)
        {
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!campaignPublicId.IsValid) throw new ArgumentException("CampaignPublicId is required.", nameof(campaignPublicId));
            if (string.IsNullOrWhiteSpace(rootPath)) throw new ArgumentException("Root path is required.", nameof(rootPath));

            CampaignId = campaignId;
            CampaignPublicId = campaignPublicId;
            RootPath = rootPath;
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        }

        public CampaignId CampaignId { get; }
        public CampaignPublicId CampaignPublicId { get; }
        public string RootPath { get; }
        public CampaignManifest Manifest { get; }
    }

    public static class PersistenceFailures
    {
        public static Error CampaignNotFound(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCampaignNotFound,
            ErrorCategory.NotFound,
            SafeReasonCode.TargetUnavailable,
            UserMessageKey.Parse("errors.persistence.campaign_not_found"),
            RetryDirective.DoNotRetry,
            correlationId);

        public static Error CampaignIoFailed(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCampaignIoFailed,
            ErrorCategory.PermanentInfrastructure,
            SafeReasonCode.UnexpectedError,
            UserMessageKey.Parse("errors.persistence.campaign_io_failed"),
            RetryDirective.ManualRecoveryRequired,
            correlationId);

        public static Error ManifestInvalid(CorrelationId? correlationId = null) => Error.Create(
            ErrorCodes.PersistenceManifestInvalid,
            ErrorCategory.Integrity,
            SafeReasonCode.DataCorrupted,
            UserMessageKey.Parse("errors.persistence.manifest_invalid"),
            RetryDirective.ManualRecoveryRequired,
            correlationId ?? PlaceholderCorrelationId);

        public static Error ManifestConflict(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceManifestConflict,
            ErrorCategory.Conflict,
            SafeReasonCode.DataCorrupted,
            UserMessageKey.Parse("errors.persistence.manifest_conflict"),
            RetryDirective.ManualRecoveryRequired,
            correlationId);

        public static Error SceneNotFound(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceSceneNotFound,
            ErrorCategory.NotFound,
            SafeReasonCode.TargetUnavailable,
            UserMessageKey.Parse("errors.persistence.scene_not_found"),
            RetryDirective.DoNotRetry,
            correlationId);

        public static Error SceneIoFailed(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceSceneIoFailed,
            ErrorCategory.PermanentInfrastructure,
            SafeReasonCode.UnexpectedError,
            UserMessageKey.Parse("errors.persistence.scene_io_failed"),
            RetryDirective.ManualRecoveryRequired,
            correlationId);

        public static Error TokenNotFound(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceTokenNotFound,
            ErrorCategory.NotFound,
            SafeReasonCode.TargetUnavailable,
            UserMessageKey.Parse("errors.persistence.token_not_found"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>
        /// ODY-S03-004: ADR-002 section 10.2's optimistic-concurrency check,
        /// enforced atomically inside <c>SqliteSceneRepository.MoveToken</c>'s
        /// own transaction -- the final guard against a concurrent revision
        /// change, independent of any Application-layer pre-check
        /// (<c>Odyssey.Application.Board.BoardMovementService</c>) that ran
        /// outside this transaction.
        /// </summary>
        public static Error TokenRevisionConflict(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceTokenRevisionConflict,
            ErrorCategory.Conflict,
            SafeReasonCode.StateChanged,
            UserMessageKey.Parse("errors.persistence.token_revision_conflict"),
            RetryDirective.DoNotRetry,
            correlationId);

        public static Error IntegrityCheckFailed(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceIntegrityCheckFailed,
            ErrorCategory.Integrity,
            SafeReasonCode.DataCorrupted,
            UserMessageKey.Parse("errors.persistence.integrity_check_failed"),
            RetryDirective.ManualRecoveryRequired,
            correlationId);

        public static Error CommandReplayFailed(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCommandReplayFailed,
            ErrorCategory.Integrity,
            SafeReasonCode.DataCorrupted,
            UserMessageKey.Parse("errors.persistence.command_replay_failed"),
            RetryDirective.ManualRecoveryRequired,
            correlationId);

        public static Error BackupCreateFailed(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceBackupCreateFailed,
            ErrorCategory.PermanentInfrastructure,
            SafeReasonCode.UnexpectedError,
            UserMessageKey.Parse("errors.persistence.backup_create_failed"),
            RetryDirective.ManualRecoveryRequired,
            correlationId);

        public static Error BackupNotFound(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceBackupNotFound,
            ErrorCategory.NotFound,
            SafeReasonCode.TargetUnavailable,
            UserMessageKey.Parse("errors.persistence.backup_not_found"),
            RetryDirective.DoNotRetry,
            correlationId);

        public static Error BackupRestoreFailed(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceBackupRestoreFailed,
            ErrorCategory.PermanentInfrastructure,
            SafeReasonCode.UnexpectedError,
            UserMessageKey.Parse("errors.persistence.backup_restore_failed"),
            RetryDirective.ManualRecoveryRequired,
            correlationId);

        public static Error ExportCreateFailed(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceExportCreateFailed,
            ErrorCategory.PermanentInfrastructure,
            SafeReasonCode.UnexpectedError,
            UserMessageKey.Parse("errors.persistence.export_create_failed"),
            RetryDirective.ManualRecoveryRequired,
            correlationId);

        public static Error ExportImportFailed(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceExportImportFailed,
            ErrorCategory.PermanentInfrastructure,
            SafeReasonCode.UnexpectedError,
            UserMessageKey.Parse("errors.persistence.export_import_failed"),
            RetryDirective.ManualRecoveryRequired,
            correlationId);

        /// <summary>ODY-S03-007: <c>SqliteGameLogRepository</c>'s wrapper for DiceRoll/GameLogEntry file or SQLite I/O failures, mirroring <see cref="SceneIoFailed"/>'s exact convention for a different aggregate pair.</summary>
        public static Error GameLogIoFailed(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceGameLogIoFailed,
            ErrorCategory.PermanentInfrastructure,
            SafeReasonCode.UnexpectedError,
            UserMessageKey.Parse("errors.persistence.gamelog_io_failed"),
            RetryDirective.ManualRecoveryRequired,
            correlationId);

        // Used only by the codec's Read path (CampaignManifest.cs), which does not
        // receive a caller CorrelationId; matches the existing SerializationFailures
        // placeholder-correlation convention for codec-level structural failures.
        private static readonly CorrelationId PlaceholderCorrelationId = CorrelationId.Parse("corr_00000000000000000000000000000000");
    }
}
