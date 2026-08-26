using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Odyssey.Domain.Dice
{
    /// <summary>
    /// ODY-S03-005: `09_Dice_And_Game_Log` section 7.1's MVP formula grammar --
    /// dice groups, constants, `+`/`-` only, nothing more:
    /// <code>
    /// expression  = signedTerm, { ("+" | "-"), term } ;
    /// signedTerm  = ["+" | "-"], term ;
    /// term        = diceGroup | integer ;
    /// diceGroup   = [positiveInteger], ("d" | "D"), positiveInteger ;
    /// integer     = digit, { digit } ;
    /// </code>
    /// Kept in Odyssey.Domain (no dependency, ADR-001 section 5) as pure text
    /// parsing with no ruleset semantics -- the same placement reasoning
    /// ODY-S03-004 used for BoardGeometry. Uses the same TryParse/Parse
    /// convention every other Domain value type in this module already uses
    /// (DomainIdentity.cs's CanonicalId), not <c>Result&lt;T&gt;</c> -- that
    /// type lives in Odyssey.Application, which Domain must never reference
    /// (ADR-001 section 5's dependency matrix). Failure detail is a Domain-
    /// only <see cref="DiceFormulaParseError"/> enum; the Application-layer
    /// caller (Odyssey.Application.Dice) maps it to a typed
    /// <c>Result&lt;T&gt;</c>/<c>SafeReasonCode</c> failure.
    /// </summary>
    public enum DiceFormulaParseError
    {
        None = 0,
        Empty,
        InvalidSyntax,
        TooManyDiceGroups,
        TooManyDice,
        SidesOutOfRange,
        FormulaTooLong,
    }

    public enum DiceTermKind
    {
        DiceGroup = 1,
        Constant = 2,
    }

    /// <summary>Section 7.5's `DiceTerm` value object.</summary>
    public readonly struct DiceTerm
    {
        internal DiceTerm(int sign, DiceTermKind kind, int? count, int? sides, int? constantValue)
        {
            Sign = sign;
            Kind = kind;
            Count = count;
            Sides = sides;
            ConstantValue = constantValue;
        }

        public int Sign { get; }
        public DiceTermKind Kind { get; }
        public int? Count { get; }
        public int? Sides { get; }
        public int? ConstantValue { get; }
    }

    /// <summary>Section 7.5's `DiceFormula` value object.</summary>
    public sealed class DiceFormula
    {
        public const int ParserVersion = 1;

        internal DiceFormula(string originalText, string normalizedText, IReadOnlyList<DiceTerm> terms, int totalDiceCount, int diceGroupCount)
        {
            OriginalText = originalText;
            NormalizedText = normalizedText;
            Terms = terms;
            TotalDiceCount = totalDiceCount;
            DiceGroupCount = diceGroupCount;
        }

        public string OriginalText { get; }
        public string NormalizedText { get; }
        public IReadOnlyList<DiceTerm> Terms { get; }
        public int TotalDiceCount { get; }
        public int DiceGroupCount { get; }
    }

    public static class DiceFormulaParser
    {
        /// <summary>Section 7.4's limits, applied before any RNG use.</summary>
        public const int MaxDiceCount = 100;
        public const int MaxDiceGroups = 20;
        public const int MinSides = 2;
        public const int MaxSides = 1000;
        public const int MaxFormulaLength = 256;

        public static bool TryParse(string? text, out DiceFormula formula, out DiceFormulaParseError error)
        {
            formula = null!;
            error = DiceFormulaParseError.None;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = DiceFormulaParseError.Empty;
                return false;
            }

            if (text!.Length > MaxFormulaLength)
            {
                error = DiceFormulaParseError.FormulaTooLong;
                return false;
            }

            // Section 7.2: strip safe whitespace, uppercase "D" -> "d". Original
            // text is preserved separately, unmodified.
            string normalized = text.Replace(" ", string.Empty).Replace("D", "d");

            var terms = new List<DiceTerm>();
            int totalDiceCount = 0;
            int diceGroupCount = 0;
            int index = 0;
            bool expectTerm = true;

            while (index < normalized.Length)
            {
                int sign = 1;
                if (expectTerm && (normalized[index] == '+' || normalized[index] == '-'))
                {
                    sign = normalized[index] == '-' ? -1 : 1;
                    index++;
                }
                else if (!expectTerm)
                {
                    if (normalized[index] != '+' && normalized[index] != '-')
                    {
                        error = DiceFormulaParseError.InvalidSyntax;
                        return false;
                    }

                    sign = normalized[index] == '-' ? -1 : 1;
                    index++;
                }

                if (index >= normalized.Length)
                {
                    error = DiceFormulaParseError.InvalidSyntax;
                    return false;
                }

                int termStart = index;
                while (index < normalized.Length && char.IsDigit(normalized[index]))
                {
                    index++;
                }

                string leadingDigits = normalized.Substring(termStart, index - termStart);

                if (index < normalized.Length && normalized[index] == 'd')
                {
                    // diceGroup = [positiveInteger] "d" positiveInteger
                    index++;
                    int sidesStart = index;
                    while (index < normalized.Length && char.IsDigit(normalized[index]))
                    {
                        index++;
                    }

                    string sidesDigits = normalized.Substring(sidesStart, index - sidesStart);
                    if (sidesDigits.Length == 0 || (leadingDigits.Length > 0 && leadingDigits[0] == '0') || (sidesDigits.Length > 0 && sidesDigits[0] == '0'))
                    {
                        error = DiceFormulaParseError.InvalidSyntax;
                        return false;
                    }

                    // Section 7.2: "d20" expands to "1d20" internally.
                    int count = leadingDigits.Length == 0 ? 1 : int.Parse(leadingDigits, CultureInfo.InvariantCulture);
                    int sides = int.Parse(sidesDigits, CultureInfo.InvariantCulture);

                    if (count <= 0 || sides < MinSides || sides > MaxSides)
                    {
                        error = DiceFormulaParseError.SidesOutOfRange;
                        return false;
                    }

                    totalDiceCount += count;
                    diceGroupCount++;
                    if (diceGroupCount > MaxDiceGroups)
                    {
                        error = DiceFormulaParseError.TooManyDiceGroups;
                        return false;
                    }

                    if (totalDiceCount > MaxDiceCount)
                    {
                        error = DiceFormulaParseError.TooManyDice;
                        return false;
                    }

                    terms.Add(new DiceTerm(sign, DiceTermKind.DiceGroup, count, sides, null));
                }
                else
                {
                    // integer = digit, { digit }
                    if (leadingDigits.Length == 0 || (leadingDigits.Length > 1 && leadingDigits[0] == '0'))
                    {
                        error = DiceFormulaParseError.InvalidSyntax;
                        return false;
                    }

                    int constantValue = int.Parse(leadingDigits, CultureInfo.InvariantCulture);
                    terms.Add(new DiceTerm(sign, DiceTermKind.Constant, null, null, constantValue));
                }

                expectTerm = false;
            }

            if (terms.Count == 0)
            {
                error = DiceFormulaParseError.InvalidSyntax;
                return false;
            }

            formula = new DiceFormula(text, RebuildNormalized(terms), terms, totalDiceCount, diceGroupCount);
            return true;
        }

        public static DiceFormula Parse(string text)
        {
            if (!TryParse(text, out DiceFormula formula, out DiceFormulaParseError error))
            {
                throw new FormatException("Dice formula is not valid: " + error);
            }

            return formula;
        }

        private static string RebuildNormalized(IReadOnlyList<DiceTerm> terms)
        {
            var builder = new StringBuilder();
            for (int index = 0; index < terms.Count; index++)
            {
                DiceTerm term = terms[index];
                if (index > 0 || term.Sign < 0)
                {
                    builder.Append(term.Sign < 0 ? '-' : '+');
                }

                if (term.Kind == DiceTermKind.DiceGroup)
                {
                    builder.Append(term.Count!.Value.ToString(CultureInfo.InvariantCulture));
                    builder.Append('d');
                    builder.Append(term.Sides!.Value.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    builder.Append(term.ConstantValue!.Value.ToString(CultureInfo.InvariantCulture));
                }
            }

            return builder.ToString();
        }
    }
}
