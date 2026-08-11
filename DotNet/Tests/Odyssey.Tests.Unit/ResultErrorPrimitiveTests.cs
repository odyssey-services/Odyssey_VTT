using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Application.Identity;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;

namespace Odyssey.Tests.Unit
{
    public sealed class ResultErrorPrimitiveTests
    {
        [Test]
        public void ResultAndResultOfTHaveExactlySuccessAndFailureStates()
        {
            Error error = CreateValidationError();
            Result<string> success = Result<string>.Success("ok");
            Result<string> failure = Result<string>.Failure(error);
            Result unitSuccess = Result.Success();
            Result unitFailure = Result.Failure(error);

            Assert.That(success.IsValid, Is.True);
            Assert.That(success.IsSuccess, Is.True);
            Assert.That(success.IsFailure, Is.False);
            Assert.That(success.Value, Is.EqualTo("ok"));
            AssertThrows<InvalidOperationException>(() => _ = success.Error);

            Assert.That(failure.IsValid, Is.True);
            Assert.That(failure.IsSuccess, Is.False);
            Assert.That(failure.IsFailure, Is.True);
            Assert.That(failure.Error, Is.SameAs(error));
            AssertThrows<InvalidOperationException>(() => _ = failure.Value);

            Assert.That(unitSuccess.IsSuccess, Is.True);
            Assert.That(unitFailure.IsFailure, Is.True);
            Assert.That(default(Result<string>).IsValid, Is.False);
            Assert.That(default(Result).IsValid, Is.False);
            AssertThrows<ArgumentNullException>(() => Result<string>.Failure(null!));
            AssertThrows<ArgumentNullException>(() => Result<string>.Success(null!));
        }

        [Test]
        public void ErrorRequiresSafeFieldsAndExcludesUnsafeDetails()
        {
            Error error = CreateValidationError();

            Assert.That(error.Code, Is.EqualTo(ErrorCodes.ApplicationValidationInvalid));
            Assert.That(error.Category, Is.EqualTo(ErrorCategory.Validation));
            Assert.That(error.SafeReasonCode, Is.EqualTo(SafeReasonCode.InvalidRequest));
            Assert.That(error.UserMessageKey.ToString(), Is.EqualTo("errors.application.validation_invalid"));
            Assert.That(error.SafeMessageArguments, Has.Count.EqualTo(1));
            Assert.That(error.ValidationDetails, Has.Count.EqualTo(1));
            Assert.That(error.Metadata, Has.Count.EqualTo(1));
            Assert.That(error.CorrelationId.IsValid, Is.True);
            Assert.That(error.DiagnosticId.HasValue, Is.False);
            AssertThrows<ArgumentException>(() => SafeMessageArgument.FromKnownPublicText(@"C:\\Users\\secret\\file.txt"));
            AssertThrows<ArgumentException>(() => ErrorMetadata.Create("unsafe", "token secret"));
            AssertThrows<FormatException>(() => SafeReasonCode.Parse("InvalidInput"));
            Assert.That(SafeReasonCode.TryParse("SomeRandomReason", out _), Is.False);
            AssertThrows<ArgumentOutOfRangeException>(() => Error.Create(
                ErrorCodes.ApplicationValidationInvalid,
                (ErrorCategory)0,
                SafeReasonCode.InvalidRequest,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                RetryDirective.DoNotRetry,
                CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef")));
            AssertThrows<ArgumentOutOfRangeException>(() => Error.Create(
                ErrorCodes.ApplicationValidationInvalid,
                (ErrorCategory)999,
                SafeReasonCode.InvalidRequest,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                RetryDirective.DoNotRetry,
                CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef")));
            AssertThrows<ArgumentOutOfRangeException>(() => Error.Create(
                ErrorCodes.ApplicationValidationInvalid,
                ErrorCategory.Validation,
                SafeReasonCode.InvalidRequest,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                (RetryDirective)0,
                CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef")));
            AssertThrows<ArgumentOutOfRangeException>(() => Error.Create(
                ErrorCodes.ApplicationValidationInvalid,
                ErrorCategory.Validation,
                SafeReasonCode.InvalidRequest,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                (RetryDirective)999,
                CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef")));
        }

        [Test]
        public void RetryDirectiveVocabularyIsExact()
        {
            string[] names = Enum.GetNames(typeof(RetryDirective));

            Assert.That(names, Is.EquivalentTo(new[]
            {
                "DoNotRetry",
                "RetrySameRequest",
                "RetryWithBackoff",
                "RefreshStateThenRetry",
                "ReconnectThenRetry",
                "UserActionRequired",
                "UpgradeRequired",
                "ManualRecoveryRequired"
            }));
        }

        [Test]
        public void ErrorVocabulariesAreExact()
        {
            Assert.That(Enum.GetNames(typeof(ErrorCategory)), Is.EquivalentTo(new[]
            {
                "Validation",
                "Authorization",
                "RuleViolation",
                "NotFound",
                "Conflict",
                "Precondition",
                "Capacity",
                "Compatibility",
                "Integrity",
                "TransientInfrastructure",
                "PermanentInfrastructure",
                "Cancelled",
                "Security",
                "Internal"
            }));
            Assert.That(Enum.GetNames(typeof(ValidationSeverity)), Is.EquivalentTo(new[]
            {
                "Error",
                "Warning"
            }));

            string[] safeReasons =
            {
                "InvalidRequest",
                "PermissionDenied",
                "ActionNotAllowed",
                "TargetUnavailable",
                "StateChanged",
                "ResourceUnavailable",
                "CapacityReached",
                "ApprovalRequired",
                "InteractionExpired",
                "VersionUnsupported",
                "UpdateRequired",
                "DataCorrupted",
                "ServiceUnavailable",
                "OperationTimedOut",
                "OperationCancelled",
                "ManualRecoveryRequired",
                "UnexpectedError"
            };
            foreach (string safeReason in safeReasons)
            {
                Assert.That(SafeReasonCode.TryParse(safeReason, out SafeReasonCode parsed), Is.True);
                Assert.That(parsed.ToString(), Is.EqualTo(safeReason));
            }

            Assert.That(SafeReasonCode.TryParse("SomeRandomReason", out _), Is.False);
        }

        [Test]
        public void ValidationDetailsArgumentsAndMetadataAreBoundedAndAllowlisted()
        {
            SafeMessageArgument argument = SafeMessageArgument.FromReferenceKey("payload.destination.x");
            ValidationDetail detail = ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                "payload.destination.x",
                new[] { argument });
            ErrorMetadata metadata = ErrorMetadata.Create("limit.max", "8");

            Assert.That(detail.IsValid, Is.True);
            Assert.That(detail.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(metadata.IsValid, Is.True);
            Assert.That(ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                "manifest.assets[0].relative_path").IsValid, Is.True);
            Assert.That(ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                "manifest.assets[3].relative_path").IsValid, Is.True);
            Assert.That(ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                "manifest.assets[123].relative_path").IsValid, Is.True);
            Assert.That(ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                "matrix[1][2]").IsValid, Is.True);
            Assert.That(ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                "character.attributes.strength").IsValid, Is.True);
            AssertThrows<ArgumentException>(() => ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                @"C:\\secret"));
            AssertThrows<ArgumentException>(() => SafeMessageArgument.FromKnownPublicText("SELECT * FROM hidden"));
            AssertThrows<ArgumentException>(() => ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                "manifest.assets[].relative_path"));
            AssertThrows<ArgumentException>(() => ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                "manifest.assets[-1].relative_path"));
            AssertThrows<ArgumentException>(() => ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                "manifest.assets[abc].relative_path"));
            AssertThrows<ArgumentException>(() => ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                "manifest.assets[3"));
            AssertThrows<ArgumentException>(() => ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                "manifest.assets[3]relative_path"));
            AssertThrows<ArgumentException>(() => ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                "manifest.assets[03].relative_path"));
            AssertThrows<ArgumentException>(() => ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                "manifest.assets[00].relative_path"));
            AssertThrows<ArgumentException>(() => ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                "manifest.assets.3]"));
            AssertThrows<ArgumentException>(() => ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                "manifest..assets"));
            AssertThrows<ArgumentException>(() => ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                ".manifest.assets"));
            AssertThrows<ArgumentException>(() => ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                "manifest.assets."));
            AssertThrows<ArgumentException>(() => ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                "manifest assets"));
            AssertThrows<ArgumentException>(() => ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                "../manifest"));

            SafeMessageArgument[] tooManyArguments =
            {
                argument, argument, argument, argument, argument
            };
            AssertThrows<ArgumentOutOfRangeException>(() => ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                safeMessageArguments: tooManyArguments));

            ValidationDetail[] tooManyDetails =
            {
                detail, detail, detail, detail, detail, detail, detail, detail, detail
            };
            AssertThrows<ArgumentOutOfRangeException>(() => Error.Create(
                ErrorCodes.ApplicationValidationInvalid,
                ErrorCategory.Validation,
                SafeReasonCode.InvalidRequest,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                RetryDirective.DoNotRetry,
                CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"),
                validationDetails: tooManyDetails));
        }

        [Test]
        public void ErrorAndNestedCollectionsAreImmutable()
        {
            SafeMessageArgument first = SafeMessageArgument.FromReferenceKey("field.name");
            SafeMessageArgument second = SafeMessageArgument.FromReferenceKey("field.other");
            SafeMessageArgument[] arguments = { first };
            ValidationDetail[] details =
            {
                ValidationDetail.Create(
                    ErrorCodes.ApplicationValidationInvalid,
                    UserMessageKey.Parse("errors.application.validation_invalid"),
                    "payload.destination.x",
                    arguments)
            };
            ErrorMetadata[] metadata = { ErrorMetadata.Create("limit.max", "64") };

            Error error = Error.Create(
                ErrorCodes.ApplicationValidationInvalid,
                ErrorCategory.Validation,
                SafeReasonCode.InvalidRequest,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                RetryDirective.DoNotRetry,
                CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"),
                safeMessageArguments: arguments,
                validationDetails: details,
                metadata: metadata);

            arguments[0] = second;
            details[0] = ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                "character.attributes.strength");
            metadata[0] = ErrorMetadata.Create("limit.max", "128");

            Assert.That(error.SafeMessageArguments[0], Is.EqualTo(first));
            Assert.That(error.ValidationDetails[0].FieldPath, Is.EqualTo("payload.destination.x"));
            Assert.That(error.ValidationDetails[0].SafeMessageArguments[0], Is.EqualTo(first));
            Assert.That(error.Metadata[0].Value, Is.EqualTo("64"));
            Assert.That(error.SafeMessageArguments, Is.Not.TypeOf<SafeMessageArgument[]>());
            Assert.That(error.ValidationDetails, Is.Not.TypeOf<ValidationDetail[]>());
            Assert.That(error.Metadata, Is.Not.TypeOf<ErrorMetadata[]>());
            Assert.That(error.ValidationDetails[0].SafeMessageArguments, Is.Not.TypeOf<SafeMessageArgument[]>());

            AssertThrows<NotSupportedException>(() => ((IList<SafeMessageArgument>)error.SafeMessageArguments)[0] = second);
            AssertThrows<NotSupportedException>(() => ((IList<ValidationDetail>)error.ValidationDetails)[0] = details[0]);
            AssertThrows<NotSupportedException>(() => ((IList<ErrorMetadata>)error.Metadata)[0] = metadata[0]);
            AssertThrows<NotSupportedException>(() => ((IList<SafeMessageArgument>)error.ValidationDetails[0].SafeMessageArguments)[0] = second);
        }

        [Test]
        public void ValidationWarningsAreAllowedPrimitivesButRejectedInFailureErrors()
        {
            ValidationDetail warning = ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                "payload.destination.x",
                severity: ValidationSeverity.Warning);

            Assert.That(warning.IsValid, Is.True);
            Assert.That(warning.Severity, Is.EqualTo(ValidationSeverity.Warning));
            AssertThrows<ArgumentException>(() => Error.Create(
                ErrorCodes.ApplicationValidationInvalid,
                ErrorCategory.Validation,
                SafeReasonCode.InvalidRequest,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                RetryDirective.DoNotRetry,
                CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"),
                validationDetails: new[] { warning }));
        }

        [Test]
        public void MetadataIsAllowlistedPerErrorCode()
        {
            ErrorMetadata allowed = ErrorMetadata.Create("limit.max", "64");
            Error validationError = Error.Create(
                ErrorCodes.ApplicationValidationInvalid,
                ErrorCategory.Validation,
                SafeReasonCode.InvalidRequest,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                RetryDirective.DoNotRetry,
                CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"),
                metadata: new[] { allowed });

            Assert.That(validationError.Metadata, Has.Count.EqualTo(1));
            AssertThrows<ArgumentException>(() => Error.Create(
                ErrorCodes.ApplicationInternalUnexpected,
                ErrorCategory.Internal,
                SafeReasonCode.UnexpectedError,
                UserMessageKey.Parse("errors.application.unexpected"),
                RetryDirective.ManualRecoveryRequired,
                CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"),
                metadata: new[] { allowed }));
            AssertThrows<ArgumentException>(() => Error.Create(
                ErrorCodes.ApplicationValidationInvalid,
                ErrorCategory.Validation,
                SafeReasonCode.InvalidRequest,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                RetryDirective.DoNotRetry,
                CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"),
                metadata: new[] { ErrorMetadata.Create("unknown.key", "64") }));

            ErrorMetadata[] tooManyMetadata =
            {
                allowed, allowed, allowed, allowed, allowed, allowed, allowed, allowed, allowed
            };
            AssertThrows<ArgumentOutOfRangeException>(() => Error.Create(
                ErrorCodes.ApplicationValidationInvalid,
                ErrorCategory.Validation,
                SafeReasonCode.InvalidRequest,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                RetryDirective.DoNotRetry,
                CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"),
                metadata: tooManyMetadata));
        }

        [Test]
        public void SafeMessageArgumentsRequireExplicitTrustFactory()
        {
            Assert.That(SafeMessageArgument.FromReferenceKey("manifest.assets.3").IsValid, Is.True);
            Assert.That(SafeMessageArgument.FromInteger(123).ToString(), Is.EqualTo("123"));
            Assert.That(SafeMessageArgument.FromKnownPublicText("Known public label").IsValid, Is.True);
            AssertThrows<ArgumentException>(() => SafeMessageArgument.FromReferenceKey("Known public label"));
            AssertThrows<ArgumentException>(() => SafeMessageArgument.FromKnownPublicText(@"C:\\Users\\secret\\file.txt"));
        }

        [Test]
        public void UserMessageKeyGrammarRequiresErrorsAreaAndKey()
        {
            Assert.That(UserMessageKey.Parse("errors.application.unexpected").IsValid, Is.True);
            Assert.That(UserMessageKey.Parse("errors.application.validation_invalid").IsValid, Is.True);
            Assert.That(UserMessageKey.Parse("errors.board.token.not_found").IsValid, Is.True);
            Assert.That(UserMessageKey.TryParse("errors.foo", out _), Is.False);
            Assert.That(UserMessageKey.TryParse("errors.", out _), Is.False);
            Assert.That(UserMessageKey.TryParse("errors..foo", out _), Is.False);
            Assert.That(UserMessageKey.TryParse("Errors.application.foo", out _), Is.False);
            Assert.That(UserMessageKey.TryParse("errors.application. white", out _), Is.False);
        }

        private static Error CreateValidationError()
        {
            SafeMessageArgument argument = SafeMessageArgument.FromReferenceKey("name");
            ValidationDetail detail = ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                "request.name",
                new[] { argument });

            return Error.Create(
                ErrorCodes.ApplicationValidationInvalid,
                ErrorCategory.Validation,
                SafeReasonCode.InvalidRequest,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                RetryDirective.DoNotRetry,
                CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"),
                safeMessageArguments: new[] { argument },
                validationDetails: new[] { detail },
                metadata: new[] { ErrorMetadata.Create("limit.max", "64") });
        }

        private static void AssertThrows<TException>(Action action)
            where TException : Exception
        {
            Assert.That(Assert.Throws<TException>(action), Is.Not.Null);
        }
    }
}
