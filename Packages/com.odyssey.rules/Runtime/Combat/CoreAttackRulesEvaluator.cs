using System;
using System.Collections.Generic;
using Odyssey.Domain.Character;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Content;
using Odyssey.Domain.Geometry;
using Odyssey.Domain.Identity;

namespace Odyssey.Rules.Combat
{
    /// <summary>
    /// ODY-S06-105: the first real, production `IAttackRulesEvaluator` implementation -- every prior task
    /// (`ODY-S05-601`-`611`, `ODY-S06-101`-`104`) exercised only hand-written test fixtures. Consumes
    /// `AttackEvaluationSnapshot.ActionWeapon` (pre-decoded by `AttackEvaluationService`, since this class
    /// lives in `Odyssey.Rules`, which cannot reference `Odyssey.Application.Content.TypedDefinitionCodec`),
    /// `Actor.AttributeValues` (`ODY-S06-102`), `ArmorAndEffects` (`ODY-S06-103`), and `Topology`
    /// (`ODY-S06-104`) to produce a real damage/range/hit resolution -- never a placeholder.
    ///
    /// MVP simplifications, each deliberate and documented at its own point below, not oversights:
    /// hit is defined purely by range (no separate to-hit roll/armor-vs-hit mechanic exists yet); multiple
    /// equipped armor pieces on one target are summed, not selected by body part (no hit-location model
    /// exists yet); `Range`/`Hit` are single aggregate values for the whole resolution (matching
    /// `ProposedAttackResolution`'s own shape), not per-target, so a multi-target intent with one
    /// out-of-range target conservatively reports a miss for the whole resolution even though in-range
    /// targets still receive real, independently-computed damage; an unresolved `attributeReference` is a
    /// fail-closed thrown exception, since `IAttackRulesEvaluator.Preview`/`Evaluate` return
    /// `ProposedAttackResolution` unconditionally and cannot carry a typed `Result` failure (the interface's
    /// own signature is frozen, not to be changed by this task).
    /// </summary>
    public sealed class CoreAttackRulesEvaluator : IAttackRulesEvaluator
    {
        private static readonly ResourceDefinitionId DamageResourceKind = ResourceDefinitionId.Parse("health");

        /// <summary>
        /// SLICE-10 Block 5 Part C (product decision 2026-09-29): a flat damage subtraction per graduated
        /// cover degree, the same additive shape as <see cref="AggregateProtection"/>'s own armor
        /// subtraction -- not a to-hit modifier (there is no to-hit roll to modify; see this class's own
        /// remarks on `Hit == Range`). These are deliberate MVP placeholder values, not tuned game
        /// balance -- the product owner may ask for different numbers in a future, separate ticket
        /// without reopening this table's own architecture.
        /// </summary>
        private static readonly IReadOnlyDictionary<CoverDegree, long> CoverPenaltyTable = new Dictionary<CoverDegree, long>
        {
            [CoverDegree.None] = 0,
            [CoverDegree.Half] = 2,
            [CoverDegree.ThreeQuarters] = 5,
            [CoverDegree.Full] = 10,
        };

        public ProposedAttackResolution Preview(AttackIntent intent, AttackEvaluationSnapshot snapshot)
            => Resolve(intent, snapshot, randomSample: null);

        public ProposedAttackResolution Evaluate(AttackIntent intent, AttackEvaluationSnapshot snapshot, AttackRandomSample randomSample)
            => Resolve(intent, snapshot, randomSample);

        private static ProposedAttackResolution Resolve(AttackIntent intent, AttackEvaluationSnapshot snapshot, AttackRandomSample? randomSample)
        {
            if (snapshot.ActionWeapon == null)
            {
                // Fail-closed: this evaluator is meant for real weapon-driven attacks. A non-weapon or
                // undecodable action item reaching here is a content/wiring inconsistency, not a normal
                // "no data yet" case (unlike a missing attribute/armor/position, which this evaluator treats
                // as a graceful default elsewhere) -- there is no sane numeric resolution to fall back to.
                throw new InvalidOperationException("CoreAttackRulesEvaluator requires a decoded WeaponDefinition (AttackEvaluationSnapshot.ActionWeapon); the action item did not decode as a weapon.");
            }

            WeaponDefinition weapon = snapshot.ActionWeapon;
            AttackDamageFormula formula = AttackDamageFormulaParser.Parse(weapon.DamageExpression);

            bool anyTargetOutOfRange = false;
            var damageDeltas = new List<AttackDelta>();
            var modifiers = new List<AttackModifierEntry>();
            string? firstArmoredTargetRef = null;
            long firstArmoredTargetAbsorbed = 0;

            foreach (CharacterId targetId in intent.TargetIds)
            {
                bool inRange = IsInRange(snapshot, targetId, weapon.Range);
                if (!inRange)
                {
                    anyTargetOutOfRange = true;
                    continue;
                }

                long protection = AggregateProtection(snapshot, targetId, out string armorRef);
                if (firstArmoredTargetRef == null && protection > 0)
                {
                    firstArmoredTargetRef = armorRef;
                    firstArmoredTargetAbsorbed = protection;
                }

                // SLICE-10 Block 5 Part C: cover reduces damage the same additive way armor does -- it
                // never affects Hit/Range (a target behind Full cover is still hit if in range, per the
                // product's own explicit decision; only the resulting damage shrinks, possibly to zero).
                long coverPenalty = CoverPenaltyTable[GetCoverDegree(snapshot, targetId)];
                if (coverPenalty > 0)
                {
                    modifiers.Add(new AttackModifierEntry("cover", (int)coverPenalty));
                }

                int cursor = 0;
                Func<int, int> nextDie = randomSample.HasValue
                    ? sides => MapRawRollToDie(randomSample.Value.Values[cursor++ % randomSample.Value.Values.Count], sides)
                    : PreviewDieValue;
                long formulaValue = EvaluateFormula(formula, snapshot.Actor.AttributeValues, nextDie);
                long finalDamage = Math.Max(0, formulaValue - protection - coverPenalty);
                damageDeltas.Add(new AttackDelta(TargetRef(targetId), (int)-finalDamage));
            }

            var range = new AttackRangeResult(!anyTargetOutOfRange, anyTargetOutOfRange ? "At least one target is beyond the weapon's own range." : "All targets are within the weapon's own range.");
            // ODY-S06-105 section 3.1's own MVP simplification: no separate to-hit mechanic exists yet
            // (no armor-vs-hit roll, no attacker/defender contest) -- range is the only thing that can
            // cause a miss in this version, so Hit mirrors Range exactly. A future, fuller Ruleset is
            // expected to make these two independent axes; this is not a permanent design choice.
            var hit = new AttackHitResult(!anyTargetOutOfRange, range.Reason);
            AttackArmorProposal? armorProposal = firstArmoredTargetRef == null ? null : new AttackArmorProposal(firstArmoredTargetRef, (int)firstArmoredTargetAbsorbed);

            return new ProposedAttackResolution(
                intent,
                snapshot,
                randomSample,
                range,
                Array.AsReadOnly(modifiers.ToArray()),
                hit,
                bodyPart: null,
                armorProposal,
                Array.AsReadOnly(damageDeltas.ToArray()),
                Array.Empty<AttackDelta>(),
                Array.Empty<AttackEffectCandidate>());
        }

        /// <summary>
        /// ODY-S06-105 section 3.1: a target whose distance is unresolved (missing Character-Token link,
        /// cross-Scene pair, or `Topology` unavailable altogether) is treated as in range -- the same
        /// "missing data does not become a new hard-fail" rule `ODY-S06-104` already established for the
        /// snapshot itself, now extended to this evaluator's own range decision (product-owner-approved,
        /// this task's own governing ТЗ section 2).
        /// </summary>
        private static bool IsInRange(AttackEvaluationSnapshot snapshot, CharacterId targetId, long weaponRange)
        {
            if (snapshot.Topology.Availability != AttackTopologyAvailability.Available) return true;
            foreach (AttackTargetDistanceEntry entry in snapshot.Topology.Entries)
            {
                if (entry.TargetId == targetId) return entry.Distance <= weaponRange;
            }

            return true;
        }

        /// <summary>SLICE-10 Block 5 Part C: a target absent from <see cref="AttackEvaluationSnapshot.Cover"/>'s own entries (including every target when <see cref="AttackCoverAvailability.UnavailableNotBound"/>) defaults to <see cref="CoverDegree.None"/> -- the permissive "missing data is not a hard-fail" rule already established for <see cref="AttackTopologyInput"/>/range.</summary>
        private static CoverDegree GetCoverDegree(AttackEvaluationSnapshot snapshot, CharacterId targetId)
        {
            if (snapshot.Cover.Availability != AttackCoverAvailability.Available)
            {
                return CoverDegree.None;
            }

            foreach (AttackTargetCoverEntry entry in snapshot.Cover.Entries)
            {
                if (entry.TargetId == targetId)
                {
                    return entry.Degree;
                }
            }

            return CoverDegree.None;
        }

        /// <summary>
        /// ODY-S06-105 section 3.2: sums every equipped armor piece's own `Protection` for this target --
        /// no selection by `CoveredBodyPartIds`, since no hit-location model exists yet (a future, fuller
        /// Ruleset's own job, not invented here). `armorRef` is a semicolon-joined list of each contributing
        /// piece's own `EquipmentSlotKey` -- `ArmorDefinition` itself carries no `ContentDefinitionRef` of
        /// its own (only `EquipmentSlotKey`/`CoveredBodyPartIds`/`Protection`), so this is the closest
        /// available identifying string for `AttackArmorProposal.ArmorRef`.
        /// </summary>
        private static long AggregateProtection(AttackEvaluationSnapshot snapshot, CharacterId targetId, out string armorRef)
        {
            long total = 0;
            var slotKeys = new List<string>();
            if (snapshot.ArmorAndEffects.Availability == AttackArmorAvailability.Available)
            {
                foreach (AttackTargetArmorEntry entry in snapshot.ArmorAndEffects.Entries)
                {
                    if (entry.TargetId != targetId) continue;
                    total += entry.Armor.Protection;
                    slotKeys.Add(entry.Armor.EquipmentSlotKey);
                }
            }

            armorRef = string.Join(";", slotKeys);
            return total;
        }

        private static long EvaluateFormula(AttackDamageFormula formula, IReadOnlyDictionary<AttributeDefinitionId, long> actorAttributes, Func<int, int> nextDie)
        {
            long total = 0;
            foreach (AttackFormulaTerm term in formula.Terms)
            {
                long termValue;
                switch (term.Kind)
                {
                    case AttackFormulaTermKind.Constant:
                        termValue = term.ConstantValue!.Value;
                        break;

                    case AttackFormulaTermKind.DiceGroup:
                        long sum = 0;
                        for (int index = 0; index < term.Count!.Value; index++)
                        {
                            sum += nextDie(term.Sides!.Value);
                        }

                        termValue = sum;
                        break;

                    case AttackFormulaTermKind.AttributeReference:
                        // ADR-030 section 6.2: an unresolved attribute reference is fail-closed, never a
                        // silent zero -- see this class's own doc comment for why a thrown exception is the
                        // concrete realization of "fail-closed" against an interface that cannot return
                        // a typed Result.
                        if (!AttributeDefinitionId.TryParse(term.AttributeName, out AttributeDefinitionId attributeId) || !actorAttributes.TryGetValue(attributeId, out long value))
                        {
                            throw new InvalidOperationException("Attack damage formula references an unresolved attribute: " + term.AttributeName);
                        }

                        termValue = value;
                        break;

                    default:
                        throw new InvalidOperationException("Unrecognized attack formula term kind: " + term.Kind);
                }

                total += term.Sign * termValue;
            }

            return total;
        }

        /// <summary>
        /// ODY-S06-105 section 3.4: maps a wide `1..100` authoritative-random draw onto a die of the
        /// formula's own real side count. `((raw - 1) % sides) + 1` is a known, documented, acceptably-biased
        /// MVP mapping (not perfectly uniform for every `sides` value) -- the evaluator cannot ask the RNG
        /// for a narrower range up front, since the weapon's own formula (and therefore each die's own side
        /// count) is not known until after the roll has already been drawn by `AttackEvaluationService`.
        /// </summary>
        private static int MapRawRollToDie(int raw, int sides) => ((raw - 1) % sides) + 1;

        /// <summary>ODY-S06-105: `Preview`'s own deterministic stand-in for a die roll -- no `AttackRandomSample` exists yet at preview time. Uses each die's own simple integer-rounded average (e.g. 1d6 -> 3), not a min/max extreme, as the least-surprising single deterministic estimate.</summary>
        private static int PreviewDieValue(int sides) => (sides + 1) / 2;

        private static string TargetRef(CharacterId targetId) => "character:" + targetId + ":" + DamageResourceKind;
    }
}
