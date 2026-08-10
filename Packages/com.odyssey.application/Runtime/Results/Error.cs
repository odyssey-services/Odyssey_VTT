using System;
using System.Collections.Generic;
using Odyssey.Application.Identity;

namespace Odyssey.Application.Results
{
    public sealed class Error : IEquatable<Error>
    {
        public const int MaxSafeMessageArguments = 4;
        private readonly SafeMessageArgument[] _safeMessageArguments;
        private readonly ValidationDetail[] _validationDetails;
        private readonly ErrorMetadata[] _metadata;

        private Error(
            ErrorCode code,
            ErrorCategory category,
            SafeReasonCode safeReasonCode,
            UserMessageKey userMessageKey,
            SafeMessageArgument[] safeMessageArguments,
            RetryDirective retryDirective,
            CorrelationId correlationId,
            ValidationDetail[] validationDetails,
            DiagnosticId? diagnosticId,
            ErrorMetadata[] metadata)
        {
            Code = code;
            Category = category;
            SafeReasonCode = safeReasonCode;
            UserMessageKey = userMessageKey;
            _safeMessageArguments = safeMessageArguments;
            RetryDirective = retryDirective;
            CorrelationId = correlationId;
            _validationDetails = validationDetails;
            DiagnosticId = diagnosticId;
            _metadata = metadata;
        }

        public ErrorCode Code { get; }
        public ErrorCategory Category { get; }
        public SafeReasonCode SafeReasonCode { get; }
        public UserMessageKey UserMessageKey { get; }
        public IReadOnlyList<SafeMessageArgument> SafeMessageArguments => _safeMessageArguments;
        public RetryDirective RetryDirective { get; }
        public CorrelationId CorrelationId { get; }
        public IReadOnlyList<ValidationDetail> ValidationDetails => _validationDetails;
        public DiagnosticId? DiagnosticId { get; }
        public IReadOnlyList<ErrorMetadata> Metadata => _metadata;

        public static Error Create(
            ErrorCode code,
            ErrorCategory category,
            SafeReasonCode safeReasonCode,
            UserMessageKey userMessageKey,
            RetryDirective retryDirective,
            CorrelationId correlationId,
            IReadOnlyList<SafeMessageArgument>? safeMessageArguments = null,
            IReadOnlyList<ValidationDetail>? validationDetails = null,
            DiagnosticId? diagnosticId = null,
            IReadOnlyList<ErrorMetadata>? metadata = null)
        {
            if (!code.IsValid) throw new ArgumentException("Error code is required.", nameof(code));
            if (!safeReasonCode.IsValid) throw new ArgumentException("Safe reason code is required.", nameof(safeReasonCode));
            if (!userMessageKey.IsValid) throw new ArgumentException("User message key is required.", nameof(userMessageKey));
            if (!correlationId.IsValid) throw new ArgumentException("Correlation id is required.", nameof(correlationId));
            if (diagnosticId.HasValue && !diagnosticId.Value.IsValid) throw new ArgumentException("Diagnostic id must be valid.", nameof(diagnosticId));

            SafeMessageArgument[] arguments = ValidationDetail.CopyBounded(safeMessageArguments, MaxSafeMessageArguments, nameof(safeMessageArguments));
            ValidationDetail[] details = CopyValidationDetails(validationDetails);
            ErrorMetadata[] metadataCopy = CopyMetadata(metadata);

            return new Error(code, category, safeReasonCode, userMessageKey, arguments, retryDirective, correlationId, details, diagnosticId, metadataCopy);
        }

        public bool Equals(Error? other)
        {
            if (other == null) return false;
            return Code.Equals(other.Code) &&
                Category == other.Category &&
                SafeReasonCode.Equals(other.SafeReasonCode) &&
                UserMessageKey.Equals(other.UserMessageKey) &&
                RetryDirective == other.RetryDirective &&
                CorrelationId.Equals(other.CorrelationId) &&
                Nullable.Equals(DiagnosticId, other.DiagnosticId);
        }

        public override bool Equals(object? obj) => Equals(obj as Error);
        public override int GetHashCode() => HashCode.Combine(Code, Category, SafeReasonCode, UserMessageKey, RetryDirective, CorrelationId, DiagnosticId);

        private static ValidationDetail[] CopyValidationDetails(IReadOnlyList<ValidationDetail>? source)
        {
            if (source == null || source.Count == 0) return Array.Empty<ValidationDetail>();
            if (source.Count > ValidationDetail.MaxDetailsPerError) throw new ArgumentOutOfRangeException(nameof(source));

            ValidationDetail[] copy = new ValidationDetail[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                if (!source[index].IsValid) throw new ArgumentException("Validation detail is required.", nameof(source));
                copy[index] = source[index];
            }

            return copy;
        }

        private static ErrorMetadata[] CopyMetadata(IReadOnlyList<ErrorMetadata>? source)
        {
            if (source == null || source.Count == 0) return Array.Empty<ErrorMetadata>();
            if (source.Count > ErrorMetadata.MaxMetadataPerError) throw new ArgumentOutOfRangeException(nameof(source));

            ErrorMetadata[] copy = new ErrorMetadata[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                if (!source[index].IsValid) throw new ArgumentException("Metadata is required.", nameof(source));
                copy[index] = source[index];
            }

            return copy;
        }
    }
}
