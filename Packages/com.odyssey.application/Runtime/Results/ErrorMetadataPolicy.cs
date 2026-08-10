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

            return false;
        }
    }
}
