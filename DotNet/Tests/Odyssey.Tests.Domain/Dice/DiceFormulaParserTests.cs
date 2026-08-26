using NUnit.Framework;
using Odyssey.Domain.Dice;

namespace Odyssey.Tests.Domain.Dice
{
    /// <summary>ODY-S03-005: 09_Dice_And_Game_Log section 7's MVP formula grammar.</summary>
    public sealed class DiceFormulaParserTests
    {
        [TestCase("d20", 1, 20)]
        [TestCase("2d6", 2, 6)]
        [TestCase("D8", 1, 8)]
        public void TryParse_SingleDiceGroup_ProducesExpectedCountAndSides(string text, int expectedCount, int expectedSides)
        {
            // TC-DICE-001
            bool ok = DiceFormulaParser.TryParse(text, out DiceFormula formula, out _);
            Assert.That(ok, Is.True);
            Assert.That(formula.Terms.Count, Is.EqualTo(1));
            Assert.That(formula.Terms[0].Kind, Is.EqualTo(DiceTermKind.DiceGroup));
            Assert.That(formula.Terms[0].Count, Is.EqualTo(expectedCount));
            Assert.That(formula.Terms[0].Sides, Is.EqualTo(expectedSides));
        }

        [Test]
        public void TryParse_CompoundFormula_3d10Plus5_ParsesTwoTerms()
        {
            // TC-DICE-002
            bool ok = DiceFormulaParser.TryParse("3d10+5", out DiceFormula formula, out _);
            Assert.That(ok, Is.True);
            Assert.That(formula.Terms.Count, Is.EqualTo(2));
            Assert.That(formula.Terms[0].Kind, Is.EqualTo(DiceTermKind.DiceGroup));
            Assert.That(formula.Terms[0].Count, Is.EqualTo(3));
            Assert.That(formula.Terms[0].Sides, Is.EqualTo(10));
            Assert.That(formula.Terms[1].Kind, Is.EqualTo(DiceTermKind.Constant));
            Assert.That(formula.Terms[1].ConstantValue, Is.EqualTo(5));
            Assert.That(formula.Terms[1].Sign, Is.EqualTo(1));
        }

        [Test]
        public void TryParse_MixedSignedGroups_2d8Minus1d4Plus3_ParsesSignsCorrectly()
        {
            // TC-DICE-002
            bool ok = DiceFormulaParser.TryParse("2d8-1d4+3", out DiceFormula formula, out _);
            Assert.That(ok, Is.True);
            Assert.That(formula.Terms.Count, Is.EqualTo(3));
            Assert.That(formula.Terms[0].Sign, Is.EqualTo(1));
            Assert.That(formula.Terms[1].Sign, Is.EqualTo(-1));
            Assert.That(formula.Terms[1].Sides, Is.EqualTo(4));
            Assert.That(formula.Terms[2].Sign, Is.EqualTo(1));
            Assert.That(formula.Terms[2].ConstantValue, Is.EqualTo(3));
        }

        [Test]
        public void TryParse_LeadingNegativeDiceGroup_MinusD6Plus12_ParsesCorrectly()
        {
            // TC-DICE-002
            bool ok = DiceFormulaParser.TryParse("-d6+12", out DiceFormula formula, out _);
            Assert.That(ok, Is.True);
            Assert.That(formula.Terms[0].Sign, Is.EqualTo(-1));
            Assert.That(formula.Terms[0].Count, Is.EqualTo(1));
            Assert.That(formula.Terms[0].Sides, Is.EqualTo(6));
        }

        [TestCase("(2d6+3)*2")]
        [TestCase("4d6/2")]
        [TestCase("4d6kh3")]
        [TestCase("4d6dl1")]
        [TestCase("max(2d6, 8)")]
        [TestCase("1d6!")]
        public void TryParse_ForbiddenSyntax_IsRejected(string text)
        {
            // TC-DICE-003 (section 7.3's forbidden-syntax list)
            bool ok = DiceFormulaParser.TryParse(text, out _, out DiceFormulaParseError error);
            Assert.That(ok, Is.False);
            Assert.That(error, Is.Not.EqualTo(DiceFormulaParseError.None));
        }

        [Test]
        public void TryParse_TooManyDiceGroups_ExceedsLimit_IsRejected()
        {
            // TC-DICE-004 (section 7.4: MaxDiceGroups = 20)
            string formula = string.Join("+", System.Linq.Enumerable.Repeat("1d6", 21));
            bool ok = DiceFormulaParser.TryParse(formula, out _, out DiceFormulaParseError error);
            Assert.That(ok, Is.False);
            Assert.That(error, Is.EqualTo(DiceFormulaParseError.TooManyDiceGroups));
        }

        [Test]
        public void TryParse_TooManyTotalDice_ExceedsLimit_IsRejected()
        {
            // TC-DICE-004 (section 7.4: MaxDiceCount = 100)
            bool ok = DiceFormulaParser.TryParse("101d6", out _, out DiceFormulaParseError error);
            Assert.That(ok, Is.False);
            Assert.That(error, Is.EqualTo(DiceFormulaParseError.TooManyDice));
        }

        [TestCase("1d1")]
        [TestCase("1d1001")]
        public void TryParse_SidesOutOfRange_IsRejected(string text)
        {
            // TC-DICE-004 (section 7.4: MinSides = 2, MaxSides = 1000)
            bool ok = DiceFormulaParser.TryParse(text, out _, out DiceFormulaParseError error);
            Assert.That(ok, Is.False);
            Assert.That(error, Is.EqualTo(DiceFormulaParseError.SidesOutOfRange));
        }

        [Test]
        public void TryParse_D100_IsValid_WithinLimits()
        {
            // TC-DICE-004: d100 is a valid single logical die (section 8), within [2,1000].
            bool ok = DiceFormulaParser.TryParse("1d100", out DiceFormula formula, out _);
            Assert.That(ok, Is.True);
            Assert.That(formula.Terms[0].Sides, Is.EqualTo(100));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("abc")]
        [TestCase("++5")]
        public void TryParse_InvalidOrEmptyText_IsRejected(string text)
        {
            // TC-DICE-003
            bool ok = DiceFormulaParser.TryParse(text, out _, out DiceFormulaParseError error);
            Assert.That(ok, Is.False);
            Assert.That(error, Is.Not.EqualTo(DiceFormulaParseError.None));
        }
    }
}
