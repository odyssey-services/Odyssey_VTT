using System;
using System.Collections.Generic;
using System.Globalization;
using Odyssey.Domain.Dice;

namespace Odyssey.Rules.Combat
{
    public enum AttackFormulaTermKind
    {
        DiceGroup = 1,
        Constant = 2,
        AttributeReference = 3,
    }

    /// <summary>Section 6.2's own term shape -- a strict superset of `DiceTerm` adding <see cref="AttributeReference"/>.</summary>
    public readonly struct AttackFormulaTerm
    {
        internal AttackFormulaTerm(int sign, AttackFormulaTermKind kind, int? count, int? sides, int? constantValue, string? attributeName)
        {
            Sign = sign;
            Kind = kind;
            Count = count;
            Sides = sides;
            ConstantValue = constantValue;
            AttributeName = attributeName;
        }

        public int Sign { get; }
        public AttackFormulaTermKind Kind { get; }
        public int? Count { get; }
        public int? Sides { get; }
        public int? ConstantValue { get; }
        public string? AttributeName { get; }
    }

    public sealed class AttackDamageFormula
    {
        internal AttackDamageFormula(string originalText, IReadOnlyList<AttackFormulaTerm> terms)
        {
            OriginalText = originalText;
            Terms = terms;
        }

        public string OriginalText { get; }
        public IReadOnlyList<AttackFormulaTerm> Terms { get; }
    }

    public enum AttackDamageFormulaParseError
    {
        None = 0,
        Empty,
        InvalidSyntax,
        TooManyDiceGroups,
        TooManyDice,
        SidesOutOfRange,
        FormulaTooLong,
    }

    /// <summary>
    /// ODY-S06-105: `ADR-030` section 6.2's own documented, deliberate superset of `DiceFormulaParser`
    /// (`Packages/com.odyssey.domain/Runtime/Dice/DiceFormula.cs`, ADR-009, unmodified by this task) --
    /// investigated directly by `ODY-S06-101` and confirmed structurally unable to represent an
    /// attribute-referencing expression like "1d6+STR" (`DiceTermKind` is an exhaustive two-value enum,
    /// `DiceTerm`'s own constructor is `internal`). This parser recognizes the identical dice-group/integer
    /// terms, plus one new `attributeReference` term, referencing `DiceFormulaParser`'s own published
    /// `MinSides`/`MaxSides`/`MaxDiceGroups`/`MaxDiceCount`/`MaxFormulaLength` constants directly rather than
    /// duplicating them, so a future change to those limits cannot silently diverge between the two grammars.
    ///
    /// Lives in `Odyssey.Rules` (not `Odyssey.Domain`, where `DiceFormulaParser` lives) -- attribute-reference
    /// resolution is Rules-layer vocabulary (`ADR-001` section 6.2's own "formulas; expression evaluation"
    /// assignment), not the Domain-pure text parsing `DiceFormulaParser` itself is. `Odyssey.Rules`'s own
    /// `references: ["Odyssey.Domain"]` (confirmed by direct `.asmdef`/`.csproj` read) already permits this.
    ///
    /// Grammar (`ADR-030` section 6.2):
    /// <code>
    /// expression          = signedTerm, { ("+" | "-"), term } ;
    /// signedTerm          = ["+" | "-"], term ;
    /// term                = diceGroup | integer | attributeReference ;
    /// diceGroup           = [positiveInteger], ("d" | "D"), positiveInteger ;
    /// integer             = digit, { digit } ;
    /// attributeReference  = letter, { letter | digit | "_" } ;
    /// </code>
    ///
    /// Disambiguation this parser's own grammar needs that `DiceFormulaParser`'s simpler one never did: a
    /// term beginning with a letter is read as a diceGroup with an implicit count of 1 (e.g. "d6") ONLY when
    /// 'd'/'D' is immediately followed by a digit; every other leading-letter run (e.g. "STR", "Dexterity" --
    /// note the leading 'D' followed by a letter, not a digit) is read as an attributeReference in full. An
    /// attribute name is never case-normalized -- unlike `DiceFormulaParser`'s own blanket "D" to "d"
    /// normalization (safe there, since its only letter is the dice marker itself), an attribute name here
    /// must reach `AttributeDefinitionId.Parse` exactly as authored (a Ruleset-defined catalog key, e.g.
    /// "Strength", not "strength").
    /// </summary>
    public static class AttackDamageFormulaParser
    {
        public static bool TryParse(string? text, out AttackDamageFormula formula, out AttackDamageFormulaParseError error)
        {
            formula = null!;
            error = AttackDamageFormulaParseError.None;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = AttackDamageFormulaParseError.Empty;
                return false;
            }

            if (text!.Length > DiceFormulaParser.MaxFormulaLength)
            {
                error = AttackDamageFormulaParseError.FormulaTooLong;
                return false;
            }

            // Only whitespace is stripped -- case is never normalized (an attribute name must survive
            // exactly as authored; DiceFormulaParser's own blanket "D"->"d" lowercasing is not safe here).
            string normalized = text.Replace(" ", string.Empty);

            var terms = new List<AttackFormulaTerm>();
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
                        error = AttackDamageFormulaParseError.InvalidSyntax;
                        return false;
                    }

                    sign = normalized[index] == '-' ? -1 : 1;
                    index++;
                }

                if (index >= normalized.Length)
                {
                    error = AttackDamageFormulaParseError.InvalidSyntax;
                    return false;
                }

                char current = normalized[index];

                if (char.IsDigit(current))
                {
                    int termStart = index;
                    while (index < normalized.Length && char.IsDigit(normalized[index])) index++;
                    string leadingDigits = normalized.Substring(termStart, index - termStart);

                    if (index < normalized.Length && (normalized[index] == 'd' || normalized[index] == 'D'))
                    {
                        // diceGroup = positiveInteger, ("d" | "D"), positiveInteger
                        index++;
                        if (!TryReadSidesAndAddDiceTerm(normalized, ref index, sign, leadingDigits, ref totalDiceCount, ref diceGroupCount, terms, out error))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        // integer = digit, { digit }
                        if (leadingDigits.Length == 0 || (leadingDigits.Length > 1 && leadingDigits[0] == '0'))
                        {
                            error = AttackDamageFormulaParseError.InvalidSyntax;
                            return false;
                        }

                        int constantValue = int.Parse(leadingDigits, CultureInfo.InvariantCulture);
                        terms.Add(new AttackFormulaTerm(sign, AttackFormulaTermKind.Constant, null, null, constantValue, null));
                    }
                }
                else if (IsLetter(current))
                {
                    if ((current == 'd' || current == 'D') && index + 1 < normalized.Length && char.IsDigit(normalized[index + 1]))
                    {
                        // diceGroup with an implicit count of 1, e.g. "d6".
                        index++;
                        if (!TryReadSidesAndAddDiceTerm(normalized, ref index, sign, string.Empty, ref totalDiceCount, ref diceGroupCount, terms, out error))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        // attributeReference = letter, { letter | digit | "_" }
                        int nameStart = index;
                        index++;
                        while (index < normalized.Length && (IsLetter(normalized[index]) || char.IsDigit(normalized[index]) || normalized[index] == '_')) index++;
                        string attributeName = normalized.Substring(nameStart, index - nameStart);
                        terms.Add(new AttackFormulaTerm(sign, AttackFormulaTermKind.AttributeReference, null, null, null, attributeName));
                    }
                }
                else
                {
                    error = AttackDamageFormulaParseError.InvalidSyntax;
                    return false;
                }

                expectTerm = false;
            }

            if (terms.Count == 0)
            {
                error = AttackDamageFormulaParseError.InvalidSyntax;
                return false;
            }

            formula = new AttackDamageFormula(text, terms);
            return true;
        }

        public static AttackDamageFormula Parse(string text)
        {
            if (!TryParse(text, out AttackDamageFormula formula, out AttackDamageFormulaParseError error))
            {
                throw new FormatException("Attack damage formula is not valid: " + error);
            }

            return formula;
        }

        private static bool TryReadSidesAndAddDiceTerm(string normalized, ref int index, int sign, string leadingDigits, ref int totalDiceCount, ref int diceGroupCount, List<AttackFormulaTerm> terms, out AttackDamageFormulaParseError error)
        {
            error = AttackDamageFormulaParseError.None;
            int sidesStart = index;
            while (index < normalized.Length && char.IsDigit(normalized[index])) index++;
            string sidesDigits = normalized.Substring(sidesStart, index - sidesStart);

            if (sidesDigits.Length == 0 || (leadingDigits.Length > 0 && leadingDigits[0] == '0') || sidesDigits[0] == '0')
            {
                error = AttackDamageFormulaParseError.InvalidSyntax;
                return false;
            }

            int count = leadingDigits.Length == 0 ? 1 : int.Parse(leadingDigits, CultureInfo.InvariantCulture);
            int sides = int.Parse(sidesDigits, CultureInfo.InvariantCulture);

            if (count <= 0 || sides < DiceFormulaParser.MinSides || sides > DiceFormulaParser.MaxSides)
            {
                error = AttackDamageFormulaParseError.SidesOutOfRange;
                return false;
            }

            totalDiceCount += count;
            diceGroupCount++;
            if (diceGroupCount > DiceFormulaParser.MaxDiceGroups)
            {
                error = AttackDamageFormulaParseError.TooManyDiceGroups;
                return false;
            }

            if (totalDiceCount > DiceFormulaParser.MaxDiceCount)
            {
                error = AttackDamageFormulaParseError.TooManyDice;
                return false;
            }

            terms.Add(new AttackFormulaTerm(sign, AttackFormulaTermKind.DiceGroup, count, sides, null, null));
            return true;
        }

        private static bool IsLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
    }
}
