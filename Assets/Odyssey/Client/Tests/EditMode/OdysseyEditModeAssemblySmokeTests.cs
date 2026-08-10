using System;
using System.Linq;
using NUnit.Framework;

namespace Odyssey.Tests.Unity.EditMode
{
    public sealed class OdysseyEditModeAssemblySmokeTests
    {
        [Test]
        public void RequiredProductionAssembliesAreLoaded()
        {
            string[] expected =
            {
                "Odyssey.Domain",
                "Odyssey.Rules",
                "Odyssey.Content",
                "Odyssey.Application",
                "Odyssey.Persistence",
                "Odyssey.Networking",
                "Odyssey.Unity.Client"
            };

            string[] loaded = AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetName().Name)
                .Where(name => name != null)
                .Select(name => name!)
                .ToArray();

            foreach (string assemblyName in expected)
            {
                Assert.That(loaded, Does.Contain(assemblyName));
            }
        }
    }
}
