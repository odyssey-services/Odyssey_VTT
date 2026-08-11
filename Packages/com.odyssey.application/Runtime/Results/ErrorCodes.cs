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
        public static readonly ErrorCode CommandIdentityMismatch = ErrorCode.Parse("application.command.identity_mismatch");
        public static readonly ErrorCode RandomInvalidRange = ErrorCode.Parse("application.random.invalid_range");
        public static readonly ErrorCode RandomDrawIndexMismatch = ErrorCode.Parse("application.random.draw_index_mismatch");
    }
}
