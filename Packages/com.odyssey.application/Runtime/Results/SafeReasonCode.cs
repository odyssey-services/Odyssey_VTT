using System;

namespace Odyssey.Application.Results
{
    public readonly struct SafeReasonCode : IEquatable<SafeReasonCode>
    {
        public const int MaxLength = 64;
        private readonly string _value;

        private SafeReasonCode(string value)
        {
            _value = value;
        }

        public bool IsValid => _value != null;
        public static SafeReasonCode InvalidRequest => Parse("InvalidRequest");
        public static SafeReasonCode PermissionDenied => Parse("PermissionDenied");
        public static SafeReasonCode ActionNotAllowed => Parse("ActionNotAllowed");
        public static SafeReasonCode TargetUnavailable => Parse("TargetUnavailable");
        public static SafeReasonCode StateChanged => Parse("StateChanged");
        public static SafeReasonCode ResourceUnavailable => Parse("ResourceUnavailable");
        public static SafeReasonCode CapacityReached => Parse("CapacityReached");
        public static SafeReasonCode ApprovalRequired => Parse("ApprovalRequired");
        public static SafeReasonCode InteractionExpired => Parse("InteractionExpired");
        public static SafeReasonCode VersionUnsupported => Parse("VersionUnsupported");
        public static SafeReasonCode UpdateRequired => Parse("UpdateRequired");
        public static SafeReasonCode DataCorrupted => Parse("DataCorrupted");
        public static SafeReasonCode ServiceUnavailable => Parse("ServiceUnavailable");
        public static SafeReasonCode OperationTimedOut => Parse("OperationTimedOut");
        public static SafeReasonCode OperationCancelled => Parse("OperationCancelled");
        public static SafeReasonCode ManualRecoveryRequired => Parse("ManualRecoveryRequired");
        public static SafeReasonCode UnexpectedError => Parse("UnexpectedError");

        public static bool TryParse(string? value, out SafeReasonCode code)
        {
            if (IsAllowed(value))
            {
                code = new SafeReasonCode(value!);
                return true;
            }

            code = default;
            return false;
        }

        public static SafeReasonCode Parse(string value)
        {
            if (!TryParse(value, out SafeReasonCode code))
            {
                throw new FormatException("SafeReasonCode is not canonical.");
            }

            return code;
        }

        public override string ToString() => _value ?? string.Empty;
        public bool Equals(SafeReasonCode other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is SafeReasonCode other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(SafeReasonCode left, SafeReasonCode right) => left.Equals(right);
        public static bool operator !=(SafeReasonCode left, SafeReasonCode right) => !left.Equals(right);

        internal static bool IsAllowed(string? value)
        {
            switch (value)
            {
                case "InvalidRequest":
                case "PermissionDenied":
                case "ActionNotAllowed":
                case "TargetUnavailable":
                case "StateChanged":
                case "ResourceUnavailable":
                case "CapacityReached":
                case "ApprovalRequired":
                case "InteractionExpired":
                case "VersionUnsupported":
                case "UpdateRequired":
                case "DataCorrupted":
                case "ServiceUnavailable":
                case "OperationTimedOut":
                case "OperationCancelled":
                case "ManualRecoveryRequired":
                case "UnexpectedError":
                    return true;
                default:
                    return false;
            }
        }
    }
}
