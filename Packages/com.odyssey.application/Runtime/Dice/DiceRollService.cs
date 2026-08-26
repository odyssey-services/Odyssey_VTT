using System;
using System.Collections.Generic;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Dice;
using Odyssey.Domain.Identity;
using Odyssey.Rules.Versions;

namespace Odyssey.Application.Dice
{
    /// <summary>
    /// ODY-S03-005: host-authoritative dice roll engine. Only this class ever
    /// draws from <see cref="IAuthoritativeRandomStream"/> for a roll (section
    /// 14.2: "только host вызывает production RNG") -- there is no code path
    /// by which a caller-supplied result reaches a <see cref="DiceRoll"/>.
    /// Reuses <c>ADR-008</c>'s already-accepted RNG contracts unchanged (no
    /// new algorithm, section 38 point 4), and follows the same two-point
    /// authorization discipline <c>Odyssey.Application.Board.BoardMovementService</c>
    /// (ODY-S03-004) and ODY-S02-011's <c>MoveTokenService</c> already
    /// established, applied here even though this synchronous, single-threaded
    /// path has no real intervening concurrency window of its own -- the same
    /// documented, deliberate discipline, not redundancy overlooked.
    /// </summary>
    public static class DiceRollService
    {
        private static readonly RngPurpose RollPurpose = RngPurpose.Parse("dice.roll");

        public static Result<DiceRoll> SubmitRoll(DiceRollStore store, IAuthoritativeRandomStreamFactory rngFactory, IWallClock clock, SubmitRollRequest request)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (rngFactory == null) throw new ArgumentNullException(nameof(rngFactory));
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            if (request == null) throw new ArgumentNullException(nameof(request));

            // Submission-time authorization check (ADR-019 section 6.1's first point).
            if (!request.ActorCanCreateRoll)
            {
                return Result<DiceRoll>.Failure(DiceFailures.RollDenied(request.CorrelationId));
            }

            if (!DiceFormulaParser.TryParse(request.Formula, out DiceFormula formula, out _))
            {
                return Result<DiceRoll>.Failure(DiceFailures.InvalidFormula(request.CorrelationId));
            }

            // Pre-generation authorization re-check (ADR-019 section 6.1's second point).
            if (!request.ActorCanCreateRoll)
            {
                return Result<DiceRoll>.Failure(DiceFailures.RollDenied(request.CorrelationId));
            }

            RandomDecisionContext context = RandomDecisionContext.Create(
                request.CampaignId, request.CommandId, decisionOrdinal: 0, RollPurpose,
                request.RulesetVersion, request.RngKeyEpochId, request.CorrelationId);

            Result<IAuthoritativeRandomStream> streamResult = rngFactory.Create(context);
            if (streamResult.IsFailure)
            {
                return Result<DiceRoll>.Failure(streamResult.Error);
            }

            Result<(IReadOnlyList<NaturalResult> Naturals, int BaseTotal, IReadOnlyList<RngProofData> Proofs)> generated = GenerateNaturalResults(formula, streamResult.Value, request.CorrelationId);
            if (generated.IsFailure)
            {
                return Result<DiceRoll>.Failure(generated.Error);
            }

            var automaticModifiers = new List<ModifierEntry>();
            foreach (AutomaticModifierRequest automatic in request.AutomaticModifiers)
            {
                automaticModifiers.Add(new ModifierEntry(
                    store.NewId("mod"), automatic.SourceKind, automatic.Label, automatic.Value,
                    proposedByUserId: null, ModifierDecision.Automatic, decidedByUserId: null, decisionReason: null, appliedValue: automatic.Value));
            }

            string rollId = store.NewId("roll");
            var roll = new DiceRoll(
                rollId, request.ActorUserId, request.Purpose, request.CampaignId,
                formula.OriginalText, formula.NormalizedText, DiceFormula.ParserVersion,
                generated.Value.Naturals, automaticModifiers, generated.Value.BaseTotal,
                RandomDecisionContext.RngAlgorithmVersion, generated.Value.Proofs,
                DiceRollStatus.Resolved, previousRollId: null, clock.GetUtcNow());

            store.Save(roll);
            return Result<DiceRoll>.Success(roll);
        }

        /// <summary>Section 12.2: a proposal is its own visible step, decided separately (<see cref="DecideModifier"/>) -- never silently folded into FinalTotal.</summary>
        public static Result<DiceRoll> ProposeModifier(DiceRollStore store, ProposeModifierRequest request)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!store.TryGet(request.RollId, out DiceRoll roll))
            {
                return Result<DiceRoll>.Failure(DiceFailures.RollNotFound(request.CorrelationId));
            }

            var entries = new List<ModifierEntry>(roll.ModifierEntries)
            {
                new ModifierEntry(store.NewId("mod"), request.SourceKind, request.Label, request.Value, request.ProposedByUserId, ModifierDecision.Proposed, decidedByUserId: null, decisionReason: null, appliedValue: 0)
            };

            DiceRoll updated = roll.WithModifierEntries(entries);
            store.Save(updated);
            return Result<DiceRoll>.Success(updated);
        }

        /// <summary>Section 12.2: GM (or an equivalently authorized actor) accepts, changes with reason, or rejects with reason -- every decision stays a visible, sourced ModifierEntry.</summary>
        public static Result<DiceRoll> DecideModifier(DiceRollStore store, DecideModifierRequest request)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!store.TryGet(request.RollId, out DiceRoll roll))
            {
                return Result<DiceRoll>.Failure(DiceFailures.RollNotFound(request.CorrelationId));
            }

            if (!request.DecidedByUserIsMainGm)
            {
                return Result<DiceRoll>.Failure(DiceFailures.ModifierDecisionDenied(request.CorrelationId));
            }

            int entryIndex = -1;
            for (int index = 0; index < roll.ModifierEntries.Count; index++)
            {
                if (string.Equals(roll.ModifierEntries[index].ModifierEntryId, request.ModifierEntryId, StringComparison.Ordinal))
                {
                    entryIndex = index;
                    break;
                }
            }

            if (entryIndex < 0)
            {
                return Result<DiceRoll>.Failure(DiceFailures.ModifierNotFound(request.CorrelationId));
            }

            if ((request.Decision == ModifierDecision.Changed || request.Decision == ModifierDecision.Rejected) && string.IsNullOrWhiteSpace(request.Reason))
            {
                return Result<DiceRoll>.Failure(DiceFailures.ModifierDecisionReasonRequired(request.CorrelationId));
            }

            ModifierEntry original = roll.ModifierEntries[entryIndex];
            int appliedValue = request.Decision switch
            {
                ModifierDecision.Accepted => original.Value,
                ModifierDecision.Changed => request.ChangedValue ?? original.Value,
                ModifierDecision.Rejected => 0,
                _ => original.Value,
            };

            var decided = new ModifierEntry(original.ModifierEntryId, original.SourceKind, original.Label, original.Value, original.ProposedByUserId, request.Decision, request.DecidedByUserId, request.Reason, appliedValue);

            var entries = new List<ModifierEntry>(roll.ModifierEntries);
            entries[entryIndex] = decided;

            DiceRoll updated = roll.WithModifierEntries(entries);
            store.Save(updated);
            return Result<DiceRoll>.Success(updated);
        }

        /// <summary>Section 19: a separate, immutable record; the original roll's NaturalResults/FinalTotal are never rewritten -- only its Status marker flips.</summary>
        public static Result<RollOverride> ApplyOverride(DiceRollStore store, IWallClock clock, ApplyOverrideRequest request)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!store.TryGet(request.RollId, out DiceRoll roll))
            {
                return Result<RollOverride>.Failure(DiceFailures.RollNotFound(request.CorrelationId));
            }

            if (!request.ActorIsMainGm)
            {
                return Result<RollOverride>.Failure(DiceFailures.OverrideDenied(request.CorrelationId));
            }

            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return Result<RollOverride>.Failure(DiceFailures.OverrideReasonRequired(request.CorrelationId));
            }

            var rollOverride = new RollOverride(store.NewId("ovr"), roll.RollId, request.ActorUserId, request.OriginalInterpretation, request.AppliedInterpretation, request.Reason!, clock.GetUtcNow());
            store.AddOverride(rollOverride);
            store.Save(roll.WithStatus(DiceRollStatus.Overridden));

            return Result<RollOverride>.Success(rollOverride);
        }

        /// <summary>Section 17: the whole roll is redone -- never a partial/per-die reroll. A new DiceRoll, chained via PreviousRollId; the original is preserved, only its Status flips to SupersededByReroll.</summary>
        public static Result<DiceRoll> RequestFullReroll(DiceRollStore store, IAuthoritativeRandomStreamFactory rngFactory, IWallClock clock, RequestFullRerollRequest request)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (rngFactory == null) throw new ArgumentNullException(nameof(rngFactory));
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!store.TryGet(request.OriginalRollId, out DiceRoll original))
            {
                return Result<DiceRoll>.Failure(DiceFailures.RollNotFound(request.CorrelationId));
            }

            // Section 17.3: the acting player of the original roll, or MainGM.
            if (!original.ActorUserId.Equals(request.ActorUserId) && !request.ActorIsMainGm)
            {
                return Result<DiceRoll>.Failure(DiceFailures.RerollDenied(request.CorrelationId));
            }

            if (!DiceFormulaParser.TryParse(original.FormulaOriginal, out DiceFormula formula, out _))
            {
                return Result<DiceRoll>.Failure(DiceFailures.InvalidFormula(request.CorrelationId));
            }

            RandomDecisionContext context = RandomDecisionContext.Create(
                original.CampaignId, request.CommandId, decisionOrdinal: 0, RollPurpose,
                request.RulesetVersion, request.RngKeyEpochId, request.CorrelationId);

            Result<IAuthoritativeRandomStream> streamResult = rngFactory.Create(context);
            if (streamResult.IsFailure)
            {
                return Result<DiceRoll>.Failure(streamResult.Error);
            }

            Result<(IReadOnlyList<NaturalResult> Naturals, int BaseTotal, IReadOnlyList<RngProofData> Proofs)> generated = GenerateNaturalResults(formula, streamResult.Value, request.CorrelationId);
            if (generated.IsFailure)
            {
                return Result<DiceRoll>.Failure(generated.Error);
            }

            string newRollId = store.NewId("roll");
            var reroll = new DiceRoll(
                newRollId, original.ActorUserId, original.Purpose, original.CampaignId,
                formula.OriginalText, formula.NormalizedText, DiceFormula.ParserVersion,
                generated.Value.Naturals, new List<ModifierEntry>(), generated.Value.BaseTotal,
                RandomDecisionContext.RngAlgorithmVersion, generated.Value.Proofs,
                DiceRollStatus.Resolved, previousRollId: original.RollId, clock.GetUtcNow());

            store.Save(reroll);
            store.Save(original.WithStatus(DiceRollStatus.SupersededByReroll));

            return Result<DiceRoll>.Success(reroll);
        }

        /// <summary>Section 18: the DiceRoll is never deleted -- only its Status flips to Cancelled, with a mandatory reason for an already-resolved roll (section 18.3).</summary>
        public static Result<DiceRoll> CancelRoll(DiceRollStore store, CancelRollRequest request)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!store.TryGet(request.RollId, out DiceRoll roll))
            {
                return Result<DiceRoll>.Failure(DiceFailures.RollNotFound(request.CorrelationId));
            }

            if (!roll.ActorUserId.Equals(request.ActorUserId) && !request.ActorIsMainGm)
            {
                return Result<DiceRoll>.Failure(DiceFailures.CancelDenied(request.CorrelationId));
            }

            // Section 18.3: cancelling an already-resolved roll requires a reason.
            // This task's SubmitRoll is synchronous/atomic (section 3 of the task
            // contract) -- there is no pre-RNG pending RollRequest state to cancel
            // without a reason (section 18.1); every roll reaching this store is
            // already past RNG.
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return Result<DiceRoll>.Failure(DiceFailures.CancelReasonRequired(request.CorrelationId));
            }

            DiceRoll cancelled = roll.WithStatus(DiceRollStatus.Cancelled);
            store.Save(cancelled);
            return Result<DiceRoll>.Success(cancelled);
        }

        private static Result<(IReadOnlyList<NaturalResult>, int, IReadOnlyList<RngProofData>)> GenerateNaturalResults(DiceFormula formula, IAuthoritativeRandomStream stream, Odyssey.Domain.Identity.CorrelationId correlationId)
        {
            var naturals = new List<NaturalResult>();
            var proofs = new List<RngProofData>();
            int baseTotal = 0;
            int drawIndex = 0;

            for (int termIndex = 0; termIndex < formula.Terms.Count; termIndex++)
            {
                DiceTerm term = formula.Terms[termIndex];
                if (term.Kind == DiceTermKind.Constant)
                {
                    baseTotal += term.Sign * term.ConstantValue!.Value;
                    continue;
                }

                int groupSum = 0;
                for (int dieIndex = 0; dieIndex < term.Count!.Value; dieIndex++)
                {
                    Result<RandomSample> sample = stream.NextInclusive(1, term.Sides!.Value, drawIndex);
                    drawIndex++;
                    if (sample.IsFailure)
                    {
                        return Result<(IReadOnlyList<NaturalResult>, int, IReadOnlyList<RngProofData>)>.Failure(sample.Error);
                    }

                    naturals.Add(new NaturalResult(dieIndex, termIndex, term.Sides!.Value, sample.Value.Value));
                    proofs.Add(sample.Value.ProofData);
                    groupSum += sample.Value.Value;
                }

                baseTotal += term.Sign * groupSum;
            }

            return Result<(IReadOnlyList<NaturalResult>, int, IReadOnlyList<RngProofData>)>.Success((naturals, baseTotal, proofs));
        }
    }
}
