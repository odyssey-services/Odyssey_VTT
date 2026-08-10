using System;
using System.Linq;
using NUnit.Framework;

namespace Odyssey.Tests.Unit;

public sealed class AssemblyReferenceSmokeTests
{
    [Test]
    public void CoreBridgeAssembliesExposeOnlyInternalMarkers()
    {
        string[] assemblyNames =
        {
            "Odyssey.Domain",
            "Odyssey.Rules",
            "Odyssey.Content",
            "Odyssey.Application"
        };

        foreach (string assemblyName in assemblyNames)
        {
            Type marker = AppDomain.CurrentDomain.Load(assemblyName)
                .GetTypes()
                .Single(type => type.FullName == assemblyName + ".AssemblyMarker");

            Assert.That(marker.IsNotPublic, Is.True, assemblyName);
        }
    }
}
