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

        /// <summary>ODY-S04-104: <c>SubmitCharacterDraft</c>/<c>ApproveCharacterDraft</c> rejection when the requested operation is not legal for the Character's current <c>LifecycleStatus</c> -- <c>SubmitCharacterDraft</c>'s own precondition, or <c>CharacterLifecycleTransitions.IsValidTransition</c> returning false for <c>ApproveCharacterDraft</c> (ADR-022 section 5/6's already-reserved <c>Lifecycle</c> section).</summary>
        public static Error CharacterLifecycleTransitionInvalid(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterLifecycleTransitionInvalid,
            ErrorCategory.Conflict,
            SafeReasonCode.StateChanged,
            UserMessageKey.Parse("errors.persistence.character_lifecycle_transition_invalid"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-104: ADR-023 section 7.3 -- <c>Character.Approve</c> gate rejection for a non-MainGM actor, mirroring <see cref="CharacterOwnershipDenied"/>'s exact convention for a different command set.</summary>
        public static Error CharacterApprovalDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterApprovalDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.persistence.character_approval_denied"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-105: product section 12.2 -- <c>GrantDevelopmentPoints</c> is MainGM-only, mirroring <see cref="CharacterOwnershipDenied"/>'s exact convention for a different command.</summary>
        public static Error CharacterDevelopmentGrantDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterDevelopmentGrantDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.persistence.character_development_grant_denied"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-105: product section 13.1 -- <c>PurchaseAttributeIncrease</c> rejection when the actor is neither MainGM nor an assigned user of the Character.</summary>
        public static Error CharacterDevelopmentPurchaseDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterDevelopmentPurchaseDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.persistence.character_development_purchase_denied"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-105: ADR-024 section 5.1 step 2 -- insufficient <c>DevelopmentPool.Available</c> for the requested purchase; rejected with no state change.</summary>
        public static Error CharacterDevelopmentInsufficientBalance(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterDevelopmentInsufficientBalance,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.persistence.character_development_insufficient_balance"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-105: product section 11.3 -- the requested attribute increase exceeds <c>NormalDevelopmentCap</c> with no applicable rule/ability/GM override (none exist yet in this codebase).</summary>
        public static Error CharacterAttributeCapExceeded(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterAttributeCapExceeded,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.persistence.character_attribute_cap_exceeded"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-106: product section 14.3 -- <c>PurchaseSkillLevel</c> rejection when the target level requires the recommendation/reservation pipeline instead (level 5+, ADR-024 section 6.1).</summary>
        public static Error CharacterSkillLevelRequiresRecommendation(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterSkillLevelRequiresRecommendation,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.persistence.character_skill_level_requires_recommendation"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-106: <c>ResolveAdvancementRecommendation</c>/<c>GetAdvancementRecommendation</c> lookup failure for an unknown <c>AdvancementRecommendationId</c>, mirroring <see cref="CharacterNotFound"/>'s exact convention for a sibling entity.</summary>
        public static Error CharacterAdvancementRecommendationNotFound(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterAdvancementRecommendationNotFound,
            ErrorCategory.NotFound,
            SafeReasonCode.TargetUnavailable,
            UserMessageKey.Parse("errors.persistence.character_advancement_recommendation_not_found"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-106: ADR-024 section 6.1 -- <c>ResolveAdvancementRecommendation</c> rejection when the recommendation is not <c>Pending</c> (already resolved once).</summary>
        public static Error CharacterAdvancementRecommendationNotPending(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterAdvancementRecommendationNotPending,
            ErrorCategory.Conflict,
            SafeReasonCode.StateChanged,
            UserMessageKey.Parse("errors.persistence.character_advancement_recommendation_not_pending"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-106: product section 14.3 -- <c>ResolveAdvancementRecommendation</c> is MainGM-only ("GM reviews... GM approves or dismisses"); rejection for a non-MainGM actor.</summary>
        public static Error CharacterAdvancementResolutionDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterAdvancementResolutionDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.persistence.character_advancement_resolution_denied"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-107: <c>RevertAdvancementPurchase</c>/<c>ApplyCharacterRespec</c> are GM-correction operations -- MainGM-only, mirroring <see cref="CharacterAdvancementResolutionDenied"/>'s exact convention for a sibling GM-gated operation.</summary>
        public static Error CharacterAdvancementOperationDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterAdvancementOperationDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.persistence.character_advancement_operation_denied"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-107: ADR-002 section 21.2's compensation metadata -- <c>RevertAdvancementPurchase</c>/<c>ApplyCharacterRespec</c> both require a non-empty <c>ReasonCode</c>.</summary>
        public static Error CharacterAdvancementReasonRequired(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterAdvancementReasonRequired,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.persistence.character_advancement_reason_required"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-107: <c>RevertAdvancementPurchase</c> lookup failure for an unknown <c>AdvancementPurchaseId</c>, mirroring <see cref="CharacterAdvancementRecommendationNotFound"/>'s exact convention for a sibling entity.</summary>
        public static Error CharacterAdvancementPurchaseNotFound(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterAdvancementPurchaseNotFound,
            ErrorCategory.NotFound,
            SafeReasonCode.TargetUnavailable,
            UserMessageKey.Parse("errors.persistence.character_advancement_purchase_not_found"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-107: ADR-024 section 6.2 -- <c>RevertAdvancementPurchase</c> rejection when the purchase's own <c>Status</c> is not <c>Applied</c> (already reverted, or superseded by a respec).</summary>
        public static Error CharacterAdvancementPurchaseNotApplied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterAdvancementPurchaseNotApplied,
            ErrorCategory.Conflict,
            SafeReasonCode.StateChanged,
            UserMessageKey.Parse("errors.persistence.character_advancement_purchase_not_applied"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-107: ADR-024 section 6.2's minimal, Rules-Engine-free dependency check -- <c>RevertAdvancementPurchase</c> rejection when a later purchase has since raised the addressed entry's value beyond this purchase's own <c>ToValue</c>.</summary>
        public static Error CharacterAdvancementPurchaseHasDependent(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterAdvancementPurchaseHasDependent,
            ErrorCategory.Conflict,
            SafeReasonCode.StateChanged,
            UserMessageKey.Parse("errors.persistence.character_advancement_purchase_has_dependent"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-108: section 1.3's own explicit defense -- <c>RevertAdvancementPurchase</c>/<c>ApplyCharacterRespec</c>/<c>ComputeRespecPlan</c> reject an <c>AdvancementPurchase</c>/<c>CharacterRespecTarget</c> whose <c>OperationKind</c> is not one they know how to revert/respec (currently only <c>AttributeIncrease</c>/<c>SkillLevelPurchase</c> -- <c>AbilityAcquisition</c> is explicitly out of scope), rather than mis-parsing <c>TargetDefinitionId</c> as the wrong id type and producing a misleading <c>CharacterAdvancementPurchaseHasDependent</c>.</summary>
        public static Error CharacterAdvancementOperationKindNotSupported(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterAdvancementOperationKindNotSupported,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.persistence.character_advancement_operation_kind_not_supported"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-108: product section 16 -- <c>AcquireAbility</c> with <c>SourceKind=GMGrant</c> is MainGM-only, mirroring <see cref="CharacterDevelopmentGrantDenied"/>'s exact convention for a sibling GM-only grant.</summary>
        public static Error CharacterAbilityGrantDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterAbilityGrantDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.persistence.character_ability_grant_denied"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-108: <c>RemoveAbility</c> lookup failure for an unknown <c>CharacterAbilityId</c>, mirroring <see cref="CharacterAdvancementPurchaseNotFound"/>'s exact convention for a sibling entity.</summary>
        public static Error CharacterAbilityNotFound(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterAbilityNotFound,
            ErrorCategory.NotFound,
            SafeReasonCode.TargetUnavailable,
            UserMessageKey.Parse("errors.persistence.character_ability_not_found"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-108: product section 16 -- "a permanent purchased ability survives unequip"; <c>RemoveAbility</c> is legal only for <c>SourceKind=Item</c>/<c>ActiveEffect</c>, rejected for every other source.</summary>
        public static Error CharacterAbilityRemovalNotAllowed(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterAbilityRemovalNotAllowed,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.persistence.character_ability_removal_not_allowed"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-109: product section 17 -- every <c>CharacterResource</c> command is MainGM-only, mirroring <see cref="CharacterAbilityGrantDenied"/>'s exact convention for a sibling section.</summary>
        public static Error CharacterResourceOperationDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterResourceOperationDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.persistence.character_resource_operation_denied"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-109: <c>SetResourceCurrentValue</c>/<c>SetResourceMaximum</c> lookup failure for an unknown <c>CharacterResourceId</c>.</summary>
        public static Error CharacterResourceNotFound(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterResourceNotFound,
            ErrorCategory.NotFound,
            SafeReasonCode.TargetUnavailable,
            UserMessageKey.Parse("errors.persistence.character_resource_not_found"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-109: product section 17.1 -- <c>SetResourceCurrentValue</c> rejection when the requested value falls outside <c>[MinimumValue, EffectiveMaximum]</c>.</summary>
        public static Error CharacterResourceValueOutOfRange(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterResourceValueOutOfRange,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.persistence.character_resource_value_out_of_range"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-109: product section 18 -- every <c>CharacterAnatomy</c> command is MainGM-only ("GM может...").</summary>
        public static Error CharacterAnatomyOperationDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterAnatomyOperationDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.persistence.character_anatomy_operation_denied"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-109: every anatomy command except <c>InitializeCharacterAnatomy</c> requires an existing <c>CharacterAnatomy</c> snapshot.</summary>
        public static Error CharacterAnatomyNotInitialized(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterAnatomyNotInitialized,
            ErrorCategory.Conflict,
            SafeReasonCode.StateChanged,
            UserMessageKey.Parse("errors.persistence.character_anatomy_not_initialized"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-109: <c>InitializeCharacterAnatomy</c> rejection when a <c>CharacterAnatomy</c> snapshot already exists for this Character.</summary>
        public static Error CharacterAnatomyAlreadyInitialized(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterAnatomyAlreadyInitialized,
            ErrorCategory.Conflict,
            SafeReasonCode.StateChanged,
            UserMessageKey.Parse("errors.persistence.character_anatomy_already_initialized"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-109: <c>RemoveBodyPart</c>/<c>UpdateBodyPart</c>/<c>ApplyPermanentModification</c> lookup failure for an unknown <c>BodyPartId</c>.</summary>
        public static Error CharacterBodyPartNotFound(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterBodyPartNotFound,
            ErrorCategory.NotFound,
            SafeReasonCode.TargetUnavailable,
            UserMessageKey.Parse("errors.persistence.character_body_part_not_found"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-109 (section 1.3): product section 18/requirement 51 -- <c>RemoveBodyPart</c> rejection when another body part or permanent modification within the same <c>CharacterAnatomy</c> still references the part being removed. Item-system dependencies are not checked -- no Item system exists yet (see this method's own call site doc comment).</summary>
        public static Error CharacterBodyPartHasDependent(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterBodyPartHasDependent,
            ErrorCategory.Conflict,
            SafeReasonCode.StateChanged,
            UserMessageKey.Parse("errors.persistence.character_body_part_has_dependent"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-109: <c>AddBodyPart</c> rejection when a body part with the same <c>BodyPartId</c> already exists in this Character's <c>CharacterAnatomy</c>.</summary>
        public static Error CharacterBodyPartAlreadyExists(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterBodyPartAlreadyExists,
            ErrorCategory.Conflict,
            SafeReasonCode.StateChanged,
            UserMessageKey.Parse("errors.persistence.character_body_part_already_exists"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-110: <c>ArchiveCharacter</c> rejection for an actor who is neither MainGM nor an assigned user of this Character (ADR-025 section 5.1).</summary>
        public static Error CharacterArchiveDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterArchiveDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.persistence.character_archive_denied"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-110: product section 22.2 -- <c>DeleteCharacterPermanently</c> is MainGM-only, rejection for a non-MainGM actor.</summary>
        public static Error CharacterDeletionDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterDeletionDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.persistence.character_deletion_denied"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-110: product section 22.2's "отдельного подтверждения" -- <c>DeleteCharacterPermanently</c> requires a non-empty <c>ReasonCode</c>.</summary>
        public static Error CharacterDeletionReasonRequired(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterDeletionReasonRequired,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.persistence.character_deletion_reason_required"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-110: ADR-025 section 5.2 -- <c>DeleteCharacterPermanently</c> rejection when an <see cref="Odyssey.Application.Persistence.ICharacterDeletionDependencyChecker"/> reports a blocking dependency.</summary>
        public static Error CharacterDeletionHasDependent(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterDeletionHasDependent,
            ErrorCategory.Conflict,
            SafeReasonCode.StateChanged,
            UserMessageKey.Parse("errors.persistence.character_deletion_has_dependent"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-111: ADR-025 section 6.1 -- <c>TransitionCharacterToDead</c> rejection when <c>LifecycleDeathIssuerKind.GMOverride</c> is claimed by an actor who is not MainGM (`CAP-INV-008`: a plain owner/controller can never reach `Dead`).</summary>
        public static Error CharacterDeadTransitionDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterDeadTransitionDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.persistence.character_dead_transition_denied"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-111: product section 23.2 -- <c>RestoreDeadCharacter</c> is MainGM-only, rejection for a non-MainGM actor.</summary>
        public static Error CharacterRestoreDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterRestoreDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.persistence.character_restore_denied"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-111: product section 23.2 -- <c>RestoreDeadCharacter</c> requires a non-empty <c>ReasonCode</c>.</summary>
        public static Error CharacterRestoreReasonRequired(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterRestoreReasonRequired,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.persistence.character_restore_reason_required"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-111: ADR-025 section 6.1 -- <c>RestoreDeadCharacter</c> is legal only from <c>LifecycleStatus=Dead</c>.</summary>
        public static Error CharacterRestoreNotDead(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterRestoreNotDead,
            ErrorCategory.Conflict,
            SafeReasonCode.StateChanged,
            UserMessageKey.Parse("errors.persistence.character_restore_not_dead"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-106: <c>ResolveAdvancementRecommendation</c> references a <c>CriticalSuccessEvidenceId</c> that does not exist -- an integrity condition, not a normal user-facing rejection path (the recommendation's own evidence list is validated to reference real rows at request time).</summary>
        public static Error CharacterCriticalEvidenceNotFound(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterCriticalEvidenceNotFound,
            ErrorCategory.Integrity,
            SafeReasonCode.DataCorrupted,
            UserMessageKey.Parse("errors.persistence.character_critical_evidence_not_found"),
            RetryDirective.ManualRecoveryRequired,
            correlationId);

        /// <summary>ODY-S04-112: <c>ImportCharacter</c> rejection when the `.odchar` bundle at the given directory is missing its `manifest.json`/`character.json`, or either file fails to parse into the expected shape -- a graceful `Result.Failure`, never a thrown exception, for a malformed or truncated file.</summary>
        public static Error CharacterExportBundleMalformed(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterExportBundleMalformed,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.persistence.character_export_bundle_malformed"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-113: product section 25's own process step 1 ("GM выбирает новую версию Ruleset") -- MainGM-only, checked before touching the database at all.</summary>
        public static Error CharacterRulesetMigrationDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterRulesetMigrationDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.persistence.character_ruleset_migration_denied"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-113: ADR-025 section 7.2 -- <c>ApplyCharacterRulesetMigration</c> rejection while the plan still has an open <c>UnresolvedDecision</c>; the GM must resolve it first.</summary>
        public static Error CharacterRulesetMigrationHasUnresolvedDecisions(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterRulesetMigrationHasUnresolvedDecisions,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.persistence.character_ruleset_migration_has_unresolved_decisions"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-113: CAP-INV-004 -- <c>ApplyCharacterRulesetMigration</c> rejection when a freshly-recomputed PreviewHash does not match the caller-supplied plan's own hash (the Character was mutated since preview, or the plan was tampered with).</summary>
        public static Error CharacterRulesetMigrationStalePlan(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterRulesetMigrationStalePlan,
            ErrorCategory.Conflict,
            SafeReasonCode.StateChanged,
            UserMessageKey.Parse("errors.persistence.character_ruleset_migration_stale_plan"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-113: <c>RevertCharacterRulesetMigration</c> rejection when no `odyssey.persistence.character_ruleset_migrated` DomainEvents row matches the given CommandId.</summary>
        public static Error CharacterRulesetMigrationNotFound(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterRulesetMigrationNotFound,
            ErrorCategory.NotFound,
            SafeReasonCode.TargetUnavailable,
            UserMessageKey.Parse("errors.persistence.character_ruleset_migration_not_found"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-113: <c>RevertCharacterRulesetMigration</c> rejection when a compensating revert event already references the target migration's own EventSequence.</summary>
        public static Error CharacterRulesetMigrationAlreadyReverted(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterRulesetMigrationAlreadyReverted,
            ErrorCategory.Conflict,
            SafeReasonCode.StateChanged,
            UserMessageKey.Parse("errors.persistence.character_ruleset_migration_already_reverted"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S04-113: <c>RevertCharacterRulesetMigration</c> rejection when no <c>ReasonCode</c> is supplied, mirroring <see cref="CharacterAdvancementReasonRequired"/>'s own convention.</summary>
        public static Error CharacterRulesetMigrationRevertReasonRequired(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceCharacterRulesetMigrationRevertReasonRequired,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.persistence.character_ruleset_migration_revert_reason_required"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S05-101: `ContentDefinition` lookup failure, mirroring <see cref="CharacterTemplateNotFound"/>'s exact convention for a sibling aggregate.</summary>
        public static Error ContentDefinitionNotFound(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceContentDefinitionNotFound,
            ErrorCategory.NotFound,
            SafeReasonCode.TargetUnavailable,
            UserMessageKey.Parse("errors.persistence.content_definition_not_found"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S05-101: `ContentDefinition` file or SQLite I/O failure, mirroring <see cref="CharacterTemplateIoFailed"/>'s exact convention.</summary>
        public static Error ContentDefinitionIoFailed(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceContentDefinitionIoFailed,
            ErrorCategory.PermanentInfrastructure,
            SafeReasonCode.UnexpectedError,
            UserMessageKey.Parse("errors.persistence.content_definition_io_failed"),
            RetryDirective.ManualRecoveryRequired,
            correlationId);

        /// <summary>ODY-S05-101: `11_Content_Block_System` section 6.2's `Revision` optimistic-concurrency guard for `UpdateDraftContentDefinition`, mirroring <see cref="CharacterTemplateRevisionConflict"/>'s exact convention.</summary>
        public static Error ContentDefinitionRevisionConflict(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceContentDefinitionRevisionConflict,
            ErrorCategory.Conflict,
            SafeReasonCode.StateChanged,
            UserMessageKey.Parse("errors.persistence.content_definition_revision_conflict"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S05-101: `ADR-027` section 4.1's Published-immutability rule, enforced at the foundation level -- `UpdateDraftContentDefinition` refuses to touch a definition whose `Status` is not `Draft`. The real publish/archive transition commands are `ODY-S05-103`'s own job; this only guards the foundation's own bare update primitive.</summary>
        public static Error ContentDefinitionNotDraft(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceContentDefinitionNotDraft,
            ErrorCategory.Conflict,
            SafeReasonCode.ActionNotAllowed,
            UserMessageKey.Parse("errors.persistence.content_definition_not_draft"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>ODY-S05-102: `CreateNextDraftVersionFromPublished` rejection when the source `ContentDefinitionId` is not currently `Published` -- there is no Draft/Archived source to branch a next version from; only an already-Published definition has an immutable baseline worth copying.</summary>
        public static Error ContentDefinitionNotPublished(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceContentDefinitionNotPublished,
            ErrorCategory.Conflict,
            SafeReasonCode.ActionNotAllowed,
            UserMessageKey.Parse("errors.persistence.content_definition_not_published"),
            RetryDirective.DoNotRetry,
            correlationId);

        // Used only by the codec's Read path (CampaignManifest.cs), which does not
        // receive a caller CorrelationId; matches the existing SerializationFailures
        // placeholder-correlation convention for codec-level structural failures.
        private static readonly CorrelationId PlaceholderCorrelationId = CorrelationId.Parse("corr_00000000000000000000000000000000");
    }
}
