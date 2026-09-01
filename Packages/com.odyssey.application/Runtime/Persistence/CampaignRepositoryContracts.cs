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

        /// <summary>ODY-S04-101: <c>SqliteCharacterRepository</c>'s wrapper for Character file or SQLite I/O failures, mirroring <see cref="SceneIoFailed"/>'s exact convention for a new aggregate.</summary>
        public static Error CharacterIoFailed(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterIoFailed,
            ErrorCategory.PermanentInfrastructure,
            SafeReasonCode.UnexpectedError,
            UserMessageKey.Parse("errors.persistence.character_io_failed"),
            RetryDirective.ManualRecoveryRequired,
            correlationId);

        public static Error CharacterNotFound(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterNotFound,
            ErrorCategory.NotFound,
            SafeReasonCode.TargetUnavailable,
            UserMessageKey.Parse("errors.persistence.character_not_found"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-101: ADR-022 section 5's per-section optimistic-concurrency guard, mirroring <see cref="TokenRevisionConflict"/>'s exact convention -- enforced atomically inside the affected section's own transaction, independent of any Application-layer pre-check.</summary>
        public static Error CharacterRevisionConflict(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterRevisionConflict,
            ErrorCategory.Conflict,
            SafeReasonCode.StateChanged,
            UserMessageKey.Parse("errors.persistence.character_revision_conflict"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-102: ADR-025 section 4 -- <c>Character.ManageOwnership</c>-gated commands rejected for a non-MainGM actor, mirroring <c>Odyssey.Application.Board.BoardFailures.MoveDenied</c>'s exact convention (same <see cref="ErrorCategory.Authorization"/>/<see cref="SafeReasonCode.PermissionDenied"/> pair) for a different aggregate.</summary>
        public static Error CharacterOwnershipDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterOwnershipDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.persistence.character_ownership_denied"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-102: ADR-025 section 4.2 -- <c>AssignPrimaryOwner</c> requires a non-empty <c>ReasonCode</c> (CAP-INV-007).</summary>
        public static Error CharacterOwnershipReasonRequired(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterOwnershipReasonRequired,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.persistence.character_ownership_reason_required"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-103: <c>CharacterTemplate</c> lookup failure, mirroring <see cref="CharacterNotFound"/>'s exact convention for a sibling aggregate.</summary>
        public static Error CharacterTemplateNotFound(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterTemplateNotFound,
            ErrorCategory.NotFound,
            SafeReasonCode.TargetUnavailable,
            UserMessageKey.Parse("errors.persistence.character_template_not_found"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-103: <c>CharacterTemplate</c> file or SQLite I/O failure, mirroring <see cref="CharacterIoFailed"/>'s exact convention.</summary>
        public static Error CharacterTemplateIoFailed(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterTemplateIoFailed,
            ErrorCategory.PermanentInfrastructure,
            SafeReasonCode.UnexpectedError,
            UserMessageKey.Parse("errors.persistence.character_template_io_failed"),
            RetryDirective.ManualRecoveryRequired,
            correlationId);

        /// <summary>ODY-S04-103: product section 9.1's <c>Revision</c> optimistic-concurrency guard for <c>UpdateCharacterTemplate</c>/<c>ArchiveCharacterTemplate</c>, mirroring <see cref="CharacterRevisionConflict"/>'s exact convention.</summary>
        public static Error CharacterTemplateRevisionConflict(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterTemplateRevisionConflict,
            ErrorCategory.Conflict,
            SafeReasonCode.StateChanged,
            UserMessageKey.Parse("errors.persistence.character_template_revision_conflict"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-103: ADR-023 section 4.1's local Draft lookup failure.</summary>
        public static Error LocalCharacterDraftNotFound(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceLocalCharacterDraftNotFound,
            ErrorCategory.NotFound,
            SafeReasonCode.TargetUnavailable,
            UserMessageKey.Parse("errors.persistence.local_character_draft_not_found"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-103: local-profile-storage file or SQLite I/O failure, mirroring <see cref="CharacterIoFailed"/>'s exact convention for a different storage boundary.</summary>
        public static Error LocalCharacterDraftIoFailed(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceLocalCharacterDraftIoFailed,
            ErrorCategory.PermanentInfrastructure,
            SafeReasonCode.UnexpectedError,
            UserMessageKey.Parse("errors.persistence.local_character_draft_io_failed"),
            RetryDirective.ManualRecoveryRequired,
            correlationId);

        /// <summary>ODY-S04-103: ADR-023 section 6.1 -- a template's ruleset is not usable with the target campaign's own pinned ruleset. Rejected before any Character aggregate is created (ADR-023 section 11 item 4).</summary>
        public static Error CharacterDraftRulesetIncompatible(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterDraftRulesetIncompatible,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.persistence.character_draft_ruleset_incompatible"),
            RetryDirective.DoNotRetry,
            correlationId);

        // Used only by the codec's Read path (CampaignManifest.cs), which does not
        // receive a caller CorrelationId; matches the existing SerializationFailures
        // placeholder-correlation convention for codec-level structural failures.
        private static readonly CorrelationId PlaceholderCorrelationId = CorrelationId.Parse("corr_00000000000000000000000000000000");
    }
}
