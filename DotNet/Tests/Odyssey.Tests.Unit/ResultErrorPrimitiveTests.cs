using System;
using NUnit.Framework;
using Odyssey.Application.Identity;
using Odyssey.Application.Results;

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
            Assert.That(error.SafeReasonCode, Is.EqualTo(SafeReasonCode.InvalidInput));
            Assert.That(error.UserMessageKey.ToString(), Is.EqualTo("errors.application.validation_invalid"));
            Assert.That(error.SafeMessageArguments, Has.Count.EqualTo(1));
            Assert.That(error.ValidationDetails, Has.Count.EqualTo(1));
            Assert.That(error.Metadata, Has.Count.EqualTo(1));
            Assert.That(error.CorrelationId.IsValid, Is.True);
            Assert.That(error.DiagnosticId.HasValue, Is.False);
            AssertThrows<ArgumentException>(() => SafeMessageArgument.Create(@"C:\\Users\\secret\\file.txt"));
            AssertThrows<ArgumentException>(() => ErrorMetadata.Create("unsafe", "token secret"));
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
        public void ValidationDetailsArgumentsAndMetadataAreBoundedAndAllowlisted()
        {
            SafeMessageArgument argument = SafeMessageArgument.Create("field name");
            ValidationDetail detail = ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                "request.field",
                new[] { argument });
            ErrorMetadata metadata = ErrorMetadata.Create("limit.max", "8");

            Assert.That(detail.IsValid, Is.True);
            Assert.That(metadata.IsValid, Is.True);
            AssertThrows<ArgumentException>(() => ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                @"C:\\secret"));
            AssertThrows<ArgumentException>(() => SafeMessageArgument.Create("SELECT * FROM hidden"));

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
                SafeReasonCode.InvalidInput,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                RetryDirective.DoNotRetry,
                CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"),
                validationDetails: tooManyDetails));
        }

        private static Error CreateValidationError()
        {
            SafeMessageArgument argument = SafeMessageArgument.Create("name");
            ValidationDetail detail = ValidationDetail.Create(
                ErrorCodes.ApplicationValidationInvalid,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                "request.name",
                new[] { argument });

            return Error.Create(
                ErrorCodes.ApplicationValidationInvalid,
                ErrorCategory.Validation,
                SafeReasonCode.InvalidInput,
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
