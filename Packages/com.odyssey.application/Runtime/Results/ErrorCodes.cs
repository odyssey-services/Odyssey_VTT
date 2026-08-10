namespace Odyssey.Application.Results
{
    public static class ErrorCodes
    {
        public static readonly ErrorCode ApplicationValidationInvalid = ErrorCode.Parse("application.validation.invalid");
        public static readonly ErrorCode ApplicationInternalUnexpected = ErrorCode.Parse("application.internal.unexpected");
    }
}
