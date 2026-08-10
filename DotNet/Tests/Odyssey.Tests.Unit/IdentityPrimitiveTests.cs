using NUnit.Framework;
using Odyssey.Application.Identity;

namespace Odyssey.Tests.Unit
{
    public sealed class IdentityPrimitiveTests
    {
        [TestCase("corr_0123456789abcdef0123456789abcdef")]
        [TestCase("diag_abcdef0123456789abcdef0123456789")]
        public void IdentityPrimitivesAcceptCanonicalValues(string value)
        {
            if (value.StartsWith("corr_", System.StringComparison.Ordinal))
            {
                Assert.That(CorrelationId.TryParse(value, out CorrelationId correlationId), Is.True);
                Assert.That(correlationId.IsValid, Is.True);
                Assert.That(correlationId.ToString(), Is.EqualTo(value));
                Assert.That(CorrelationId.Parse(value), Is.EqualTo(correlationId));
                Assert.That(correlationId.GetHashCode(), Is.EqualTo(CorrelationId.Parse(value).GetHashCode()));
            }
            else
            {
                Assert.That(DiagnosticId.TryParse(value, out DiagnosticId diagnosticId), Is.True);
                Assert.That(diagnosticId.IsValid, Is.True);
                Assert.That(diagnosticId.ToString(), Is.EqualTo(value));
                Assert.That(DiagnosticId.Parse(value), Is.EqualTo(diagnosticId));
                Assert.That(diagnosticId.GetHashCode(), Is.EqualTo(DiagnosticId.Parse(value).GetHashCode()));
            }
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase("corr_")]
        [TestCase("corr_0123456789ABCDEF0123456789abcdef")]
        [TestCase("diag_abcdef0123456789abcdef012345678")]
        [TestCase("command_0123456789abcdef0123456789abcdef")]
        public void IdentityPrimitivesRejectInvalidValues(string value)
        {
            Assert.That(default(CorrelationId).IsValid, Is.False);
            Assert.That(default(DiagnosticId).IsValid, Is.False);
            Assert.That(CorrelationId.TryParse(value, out _), Is.False);
            Assert.That(DiagnosticId.TryParse(value, out _), Is.False);
        }
    }
}
