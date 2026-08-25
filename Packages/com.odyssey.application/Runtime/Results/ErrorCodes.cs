namespace Odyssey.Application.Results
{
    public static class ErrorCodes
    {
        public static readonly ErrorCode ApplicationValidationInvalid = ErrorCode.Parse("application.validation.invalid");
        public static readonly ErrorCode ApplicationInternalUnexpected = ErrorCode.Parse("application.internal.unexpected");
        public static readonly ErrorCode ApplicationBootstrapConfigurationInvalid = ErrorCode.Parse("application.bootstrap.configuration_invalid");
        public static readonly ErrorCode ApplicationBootstrapInitializationCancelled = ErrorCode.Parse("application.bootstrap.initialization_cancelled");
        public static readonly ErrorCode ApplicationBootstrapCompositionInvalid = ErrorCode.Parse("application.bootstrap.composition_invalid");
        public static readonly ErrorCode ApplicationBootstrapUnexpected = ErrorCode.Parse("application.bootstrap.unexpected");
        public static readonly ErrorCode ApplicationDeveloperProbeRejected = ErrorCode.Parse("application.developer.probe_rejected");
        public static readonly ErrorCode CommandIdentityMismatch = ErrorCode.Parse("application.command.identity_mismatch");
        public static readonly ErrorCode RandomInvalidRange = ErrorCode.Parse("application.random.invalid_range");
        public static readonly ErrorCode RandomDrawIndexMismatch = ErrorCode.Parse("application.random.draw_index_mismatch");
        public static readonly ErrorCode SerializationInvalidPayload = ErrorCode.Parse("application.serialization.invalid_payload");
        public static readonly ErrorCode SerializationUnsupportedContract = ErrorCode.Parse("application.serialization.unsupported_contract");
        public static readonly ErrorCode SerializationIntegrityMismatch = ErrorCode.Parse("application.serialization.integrity_mismatch");
        public static readonly ErrorCode VersioningInvalidSource = ErrorCode.Parse("application.versioning.invalid_source");
        public static readonly ErrorCode PersistenceCampaignNotFound = ErrorCode.Parse("persistence.campaign.not_found");
        public static readonly ErrorCode PersistenceCampaignIoFailed = ErrorCode.Parse("persistence.campaign.io_failed");
        public static readonly ErrorCode PersistenceManifestInvalid = ErrorCode.Parse("persistence.manifest.invalid");
        public static readonly ErrorCode PersistenceManifestConflict = ErrorCode.Parse("persistence.manifest.conflict");
        public static readonly ErrorCode PersistenceSceneNotFound = ErrorCode.Parse("persistence.scene.not_found");
        public static readonly ErrorCode PersistenceSceneIoFailed = ErrorCode.Parse("persistence.scene.io_failed");
        public static readonly ErrorCode PersistenceTokenNotFound = ErrorCode.Parse("persistence.token.not_found");
        public static readonly ErrorCode PersistenceIntegrityCheckFailed = ErrorCode.Parse("persistence.integrity.check_failed");
        public static readonly ErrorCode PersistenceCommandReplayFailed = ErrorCode.Parse("persistence.command.replay_failed");
        public static readonly ErrorCode PersistenceBackupCreateFailed = ErrorCode.Parse("persistence.backup.create_failed");
        public static readonly ErrorCode PersistenceBackupNotFound = ErrorCode.Parse("persistence.backup.not_found");
        public static readonly ErrorCode PersistenceBackupRestoreFailed = ErrorCode.Parse("persistence.backup.restore_failed");
        public static readonly ErrorCode PersistenceExportCreateFailed = ErrorCode.Parse("persistence.export.create_failed");
        public static readonly ErrorCode PersistenceExportImportFailed = ErrorCode.Parse("persistence.export.import_failed");
        public static readonly ErrorCode NetworkingTransportConnectFailed = ErrorCode.Parse("networking.transport.connect_failed");
        public static readonly ErrorCode NetworkingTransportConnectTimedOut = ErrorCode.Parse("networking.transport.connect_timed_out");
        public static readonly ErrorCode NetworkingProtocolVersionUnsupported = ErrorCode.Parse("networking.protocol.version_unsupported");
        public static readonly ErrorCode NetworkingTransportSendFailed = ErrorCode.Parse("networking.transport.send_failed");
        public static readonly ErrorCode NetworkingTransportNotConnected = ErrorCode.Parse("networking.transport.not_connected");
        public static readonly ErrorCode NetworkingTransportOperationCancelled = ErrorCode.Parse("networking.transport.operation_cancelled");
    }
}
