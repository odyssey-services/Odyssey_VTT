namespace Odyssey.Application.Results
{
    internal static class ErrorMetadataPolicy
    {
        internal static bool IsAllowed(ErrorCode code, string key)
        {
            if (code == ErrorCodes.ApplicationValidationInvalid)
            {
                return key == "limit.max";
            }

            if (code == ErrorCodes.ApplicationInternalUnexpected)
            {
                return false;
            }

            if (code == ErrorCodes.ApplicationBootstrapConfigurationInvalid ||
                code == ErrorCodes.ApplicationBootstrapInitializationCancelled ||
                code == ErrorCodes.ApplicationBootstrapCompositionInvalid ||
                code == ErrorCodes.ApplicationBootstrapUnexpected)
            {
                return false;
            }

            return false;
        }
    }
}
