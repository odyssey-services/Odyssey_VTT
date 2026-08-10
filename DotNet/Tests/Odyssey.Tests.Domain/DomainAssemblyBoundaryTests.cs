using System;
using System.Linq;
using NUnit.Framework;

namespace Odyssey.Tests.Domain
{
    public sealed class DomainAssemblyBoundaryTests
    {
        [Test]
        public void DomainAssemblyDoesNotReferenceOtherOdysseyModules()
        {
            string[] odysseyReferences = AppDomain.CurrentDomain.Load("Odyssey.Domain")
                .GetReferencedAssemblies()
                .Select(reference => reference.Name ?? string.Empty)
                .Where(name => name.StartsWith("Odyssey.", StringComparison.Ordinal))
                .ToArray();

            Assert.That(odysseyReferences, Is.Empty);
        }
    }
}
