namespace Odyssey.Application.Results
{
    public enum ErrorCategory
    {
        Validation = 1,
        Authorization = 2,
        RuleViolation = 3,
        NotFound = 4,
        Conflict = 5,
        Precondition = 6,
        Capacity = 7,
        Compatibility = 8,
        Integrity = 9,
        TransientInfrastructure = 10,
        PermanentInfrastructure = 11,
        Cancelled = 12,
        Security = 13,
        Internal = 14
    }
}
