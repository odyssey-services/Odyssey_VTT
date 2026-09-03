using System;
using NUnit.Framework;
using Odyssey.Domain.Content;
using Odyssey.Domain.Time;

namespace Odyssey.Tests.Domain.Content
{
    /// <summary>
    /// ODY-S05-101: pure Domain tests for <see cref="ContentDefinitionId"/>
    /// and <see cref="ContentDefinitionRef"/> -- `ADR-027` section 4 rule 2's
    /// exact-version reference shape ("no `LatestCompatible` runtime
    /// behavior; references must be capable of carrying exact
    /// `DefinitionId + Version`").
    /// </summary>
    public sealed class ContentDefinitionRefTests
    {
        private static readonly UtcInstant Now = UtcInstant.Parse("2026-09-03T00:00:00.0000000Z");

        [Test]
        public void ContentDefinitionId_NewId_RoundTripsThroughToStringAndParse()
        {
            ContentDefinitionId id = ContentDefinitionId.NewId(Now);

            Assert.That(id.IsValid, Is.True);
            ContentDefinitionId reparsed = ContentDefinitionId.Parse(id.ToString());
            Assert.That(reparsed, Is.EqualTo(id));
        }

        [Test]
        public void ContentDefinitionId_TryParse_OnGarbageInput_ReturnsFalse()
        {
            Assert.That(ContentDefinitionId.TryParse("not-a-content-definition-id", out ContentDefinitionId id), Is.False);
            Assert.That(id.IsValid, Is.False);
        }

        [Test]
        public void ContentDefinitionRef_Constructor_WithVersionZero_IsRejected()
        {
            ContentDefinitionId id = ContentDefinitionId.NewId(Now);

            Assert.Throws<ArgumentOutOfRangeException>(new Action(() => new ContentDefinitionRef(id, 0)),
                "a ContentDefinitionRef always pins an exact, already-published version (>= 1) -- version 0 means 'no Published version yet' and can never be referenced");
        }

        [Test]
        public void ContentDefinitionRef_Constructor_WithInvalidDefinitionId_IsRejected()
        {
            Assert.Throws<ArgumentException>(new Action(() => new ContentDefinitionRef(default, 1)));
        }

        [Test]
        public void ContentDefinitionRef_RoundTripsThroughToStringAndParse()
        {
            ContentDefinitionId id = ContentDefinitionId.NewId(Now);
            var reference = new ContentDefinitionRef(id, 3);

            string canonical = reference.ToString();
            ContentDefinitionRef reparsed = ContentDefinitionRef.Parse(canonical);

            Assert.That(reparsed, Is.EqualTo(reference));
            Assert.That(reparsed.DefinitionId, Is.EqualTo(id));
            Assert.That(reparsed.Version, Is.EqualTo(3));
        }

        [Test]
        public void ContentDefinitionRef_TwoReferencesToSameDefinition_DifferentVersions_AreNotEqual()
        {
            ContentDefinitionId id = ContentDefinitionId.NewId(Now);
            var v1 = new ContentDefinitionRef(id, 1);
            var v2 = new ContentDefinitionRef(id, 2);

            Assert.That(v1, Is.Not.EqualTo(v2), "an exact-version reference must distinguish two different published versions of the same definition -- this is the whole point of not being a 'latest' pointer");
        }

        [TestCase("")]
        [TestCase("no-slash-at-all")]
        [TestCase("cdef_0123/")]
        [TestCase("cdef_0123/notanumber")]
        [TestCase("cdef_0123/0")]
        [TestCase("cdef_0123/-1")]
        public void ContentDefinitionRef_TryParse_OnMalformedInput_ReturnsFalse(string malformed)
        {
            Assert.That(ContentDefinitionRef.TryParse(malformed, out ContentDefinitionRef reference), Is.False);
            Assert.That(reference.IsValid, Is.False);
        }
    }
}
