using System;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// ADR-001 section 6.5 / section 10: repository interfaces are Application ports;
    /// Odyssey.Persistence supplies the implementation. This port never exposes a raw
    /// SQLite connection or throws a raw provider exception (ADR-004).
    /// </summary>
    public interface ICampaignRepository
    {
        Result<CampaignHandle> Create(CreateCampaignRequest request, CorrelationId correlationId);
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

        // Used only by the codec's Read path (CampaignManifest.cs), which does not
        // receive a caller CorrelationId; matches the existing SerializationFailures
        // placeholder-correlation convention for codec-level structural failures.
        private static readonly CorrelationId PlaceholderCorrelationId = CorrelationId.Parse("corr_00000000000000000000000000000000");
    }
}
