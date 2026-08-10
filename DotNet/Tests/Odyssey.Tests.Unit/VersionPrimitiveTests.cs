using NUnit.Framework;
using Odyssey.Application.Versions;
using Odyssey.Content.Versions;
using Odyssey.Rules.Versions;

namespace Odyssey.Tests.Unit
{
    public sealed class VersionPrimitiveTests
    {
        [Test]
        public void VersionPrimitivesParseFormatAndCompareCanonicalSemVer()
        {
            ApplicationVersion app = ApplicationVersion.Parse("0.1.2");
            RulesetVersion ruleset = RulesetVersion.Parse("1.2.3");
            ContentPackageVersion content = ContentPackageVersion.Parse("2.3.4");

            Assert.That(app.ToString(), Is.EqualTo("0.1.2"));
            Assert.That(ruleset.ToString(), Is.EqualTo("1.2.3"));
            Assert.That(content.ToString(), Is.EqualTo("2.3.4"));
            Assert.That(ApplicationVersion.Parse("0.1.3") > app, Is.True);
            Assert.That(RulesetVersion.Parse("1.2.4") > ruleset, Is.True);
            Assert.That(ContentPackageVersion.Parse("2.3.5") > content, Is.True);
            Assert.That(default(ApplicationVersion).IsValid, Is.False);
            Assert.That(default(RulesetVersion).IsValid, Is.False);
            Assert.That(default(ContentPackageVersion).IsValid, Is.False);
        }

        [TestCase("01.0.0")]
        [TestCase("1.02.0")]
        [TestCase("1.2")]
        [TestCase("1.2.3-alpha")]
        [TestCase(" 1.2.3")]
        public void VersionPrimitivesRejectNonCanonicalValues(string value)
        {
            Assert.That(ApplicationVersion.TryParse(value, out _), Is.False);
            Assert.That(RulesetVersion.TryParse(value, out _), Is.False);
            Assert.That(ContentPackageVersion.TryParse(value, out _), Is.False);
        }

        [Test]
        public void VersionDimensionsRemainTypeSafeAndDoNotCreateVersionSources()
        {
            object app = ApplicationVersion.Parse("1.0.0");
            object ruleset = RulesetVersion.Parse("1.0.0");
            object content = ContentPackageVersion.Parse("1.0.0");
            string root = FindRepositoryRoot();

            Assert.That(app, Is.Not.EqualTo(ruleset));
            Assert.That(app, Is.Not.EqualTo(content));
            Assert.That(ruleset, Is.Not.EqualTo(content));
            Assert.That(System.IO.File.Exists(System.IO.Path.Combine(root, "version.json")), Is.False);
            Assert.That(System.IO.File.Exists(System.IO.Path.Combine(root, "config", "compatibility.json")), Is.False);
        }

        private static string FindRepositoryRoot()
        {
            string? current = TestContext.CurrentContext.TestDirectory;

            while (current != null && !System.IO.File.Exists(System.IO.Path.Combine(current, "AGENTS.md")))
            {
                System.IO.DirectoryInfo? parent = System.IO.Directory.GetParent(current);
                current = parent == null ? null : parent.FullName;
            }

            if (current == null)
            {
                throw new System.IO.DirectoryNotFoundException("Repository root was not found.");
            }

            return current;
        }
    }
}
