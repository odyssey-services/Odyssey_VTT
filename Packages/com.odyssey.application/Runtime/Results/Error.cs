using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using Odyssey.Application.Identity;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Results
{
    public sealed class Error
    {
        public const int MaxSafeMessageArguments = 4;
        private readonly ReadOnlyCollection<SafeMessageArgument> _safeMessageArguments;
        private readonly ReadOnlyCollection<ValidationDetail> _validationDetails;
        private readonly ReadOnlyCollection<ErrorMetadata> _metadata;

        private Error(
            ErrorCode code,
            ErrorCategory category,
            SafeReasonCode safeReasonCode,
            UserMessageKey userMessageKey,
            ReadOnlyCollection<SafeMessageArgument> safeMessageArguments,
            RetryDirective retryDirective,
            CorrelationId correlationId,
            ReadOnlyCollection<ValidationDetail> validationDetails,
            DiagnosticId? diagnosticId,
            ReadOnlyCollection<ErrorMetadata> metadata)
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
            if (!IsDefined(category)) throw new ArgumentOutOfRangeException(nameof(category));
            if (!safeReasonCode.IsValid) throw new ArgumentException("Safe reason code is required.", nameof(safeReasonCode));
            if (!userMessageKey.IsValid) throw new ArgumentException("User message key is required.", nameof(userMessageKey));
            if (!IsDefined(retryDirective)) throw new ArgumentOutOfRangeException(nameof(retryDirective));
            if (!correlationId.IsValid) throw new ArgumentException("Correlation id is required.", nameof(correlationId));
            if (diagnosticId.HasValue && !diagnosticId.Value.IsValid) throw new ArgumentException("Diagnostic id must be valid.", nameof(diagnosticId));

            ReadOnlyCollection<SafeMessageArgument> arguments = ValidationDetail.CopyBounded(safeMessageArguments, MaxSafeMessageArguments, nameof(safeMessageArguments));
            ReadOnlyCollection<ValidationDetail> details = CopyValidationDetails(validationDetails);
            ReadOnlyCollection<ErrorMetadata> metadataCopy = CopyMetadata(code, metadata);

            return new Error(code, category, safeReasonCode, userMessageKey, arguments, retryDirective, correlationId, details, diagnosticId, metadataCopy);
        }

        private static ReadOnlyCollection<ValidationDetail> CopyValidationDetails(IReadOnlyList<ValidationDetail>? source)
        {
            if (source == null || source.Count == 0) return Array.AsReadOnly(Array.Empty<ValidationDetail>());
            if (source.Count > ValidationDetail.MaxDetailsPerError) throw new ArgumentOutOfRangeException(nameof(source));

            ValidationDetail[] copy = new ValidationDetail[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                if (!source[index].IsValid) throw new ArgumentException("Validation detail is required.", nameof(source));
                if (source[index].Severity != ValidationSeverity.Error) throw new ArgumentException("Failure validation details must have Error severity.", nameof(source));
                copy[index] = source[index];
            }

            return Array.AsReadOnly(copy);
        }

        private static ReadOnlyCollection<ErrorMetadata> CopyMetadata(ErrorCode code, IReadOnlyList<ErrorMetadata>? source)
        {
            if (source == null || source.Count == 0) return Array.AsReadOnly(Array.Empty<ErrorMetadata>());
            if (source.Count > ErrorMetadata.MaxMetadataPerError) throw new ArgumentOutOfRangeException(nameof(source));

            ErrorMetadata[] copy = new ErrorMetadata[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                if (!source[index].IsValid) throw new ArgumentException("Metadata is required.", nameof(source));
                if (!ErrorMetadataPolicy.IsAllowed(code, source[index].Key)) throw new ArgumentException("Metadata key is not allowed for this ErrorCode.", nameof(source));
                copy[index] = source[index];
            }

            return Array.AsReadOnly(copy);
        }

        private static bool IsDefined(ErrorCategory category) => Enum.IsDefined(typeof(ErrorCategory), category);
        private static bool IsDefined(RetryDirective retryDirective) => Enum.IsDefined(typeof(RetryDirective), retryDirective);
    }
}
