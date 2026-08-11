namespace Odyssey.Application.Results
{
    public static class ErrorCodes
    {
        public static readonly ErrorCode ApplicationValidationInvalid = ErrorCode.Parse("application.validation.invalid");
        public static readonly ErrorCode ApplicationInternalUnexpected = ErrorCode.Parse("application.internal.unexpected");
        public static readonly ErrorCode CommandIdentityMismatch = ErrorCode.Parse("application.command.identity_mismatch");
        public static readonly ErrorCode RandomInvalidRange = ErrorCode.Parse("application.random.invalid_range");
        public static readonly ErrorCode RandomDrawIndexMismatch = ErrorCode.Parse("application.random.draw_index_mismatch");
    }
}
