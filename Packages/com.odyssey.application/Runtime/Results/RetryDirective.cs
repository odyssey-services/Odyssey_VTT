namespace Odyssey.Application.Results
{
    public enum RetryDirective
    {
        DoNotRetry = 1,
        RetrySameRequest = 2,
        RetryWithBackoff = 3,
        RefreshStateThenRetry = 4,
        ReconnectThenRetry = 5,
        UserActionRequired = 6,
        UpgradeRequired = 7,
        ManualRecoveryRequired = 8
    }
}
