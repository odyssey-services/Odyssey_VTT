using System;
using Odyssey.Application.Combat;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Content;
using Odyssey.Domain.Effects;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Effects
{
    /// <summary>
    /// ODY-S05-605: the read-only seam a caller uses to determine whether a
    /// combat participant's `TurnStarted`/`TurnEnded` has already fired
    /// strictly after a captured `CombatDurationBinding.AppliedLifecycleEventId`
    /// high-water-mark. Composes the existing, unmodified
    /// `CombatEncounterLifecycleEvent` audit table (`ODY-S05-602`) -- it does
    /// not append a method to <see cref="ICombatEncounterRepository"/>,
    /// mirroring `ODY-S05-603`'s own `IAttackStateReader`/`SqliteAttackStateReader`
    /// precedent of a new, standalone read seam over existing tables rather
    /// than a foreign-repository extension.
    /// </summary>
    public interface ICombatEncounterLifecycleReader
    {
        /// <summary>Counts `CombatEncounterLifecycleEvent` rows for <paramref name="encounterId"/>/<paramref name="characterId"/>/<paramref name="eventKind"/> ("TurnStarted" or "TurnEnded") with `EventId` strictly greater than <paramref name="sinceEventId"/>.</summary>
        Result<int> CountLifecycleEvents(CampaignHandle campaign, CombatEncounterId encounterId, CharacterId characterId, string eventKind, long sinceEventId, CorrelationId correlationId);
    }

    /// <summary>
    /// ODY-S05-605: `ADR-029` §7's own mechanism-only caller -- evaluates
    /// exactly one of the six turn/round-based `EffectDurationType` boundary
    /// checks (`ActiveEffectExpiryRules`, this same directory) for one
    /// already-persisted <see cref="ActiveEffectRecord"/> and, only when the
    /// pure decision is <see cref="ActiveEffectExpiryDecision.Expired"/>,
    /// calls the existing, unmodified <see cref="IActiveEffectRepository.ExpireActiveEffect"/>
    /// -- never a new or duplicated apply path. There is no event bus/publisher
    /// in this codebase (confirmed by direct repository search) and this
    /// class does not introduce one: it is called directly by whatever future
    /// caller decides *when* to check (e.g. at `CombatEncounterService.Advance`
    /// time, or when a target's active effects are listed) -- wiring that
    /// call site is explicitly out of this task's own scope (see the
    /// ODY-S05-605 task contract section 5).
    /// </summary>
    public static class CombatEffectExpiryService
    {
        public static Result<ActiveEffectRecord> EvaluateAndExpireIfDue(
            IActiveEffectRepository activeEffects,
            ICombatEncounterRepository encounters,
            ICombatEncounterLifecycleReader lifecycleReader,
            CampaignHandle campaign,
            ActiveEffectRecord candidate,
            EffectDurationType durationType,
            CommandId commandId,
            CorrelationId correlationId)
        {
            if (activeEffects == null) throw new ArgumentNullException(nameof(activeEffects));
            if (encounters == null) throw new ArgumentNullException(nameof(encounters));
            if (lifecycleReader == null) throw new ArgumentNullException(nameof(lifecycleReader));
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            if (!IsCombatDurationType(durationType)) throw new ArgumentOutOfRangeException(nameof(durationType), "Only the six ADR-029 section 7 turn/round-based duration types are valid here.");
            if (!candidate.Effect.CombatBinding.HasValue) throw new ArgumentException("Candidate must carry a CombatDurationBinding.", nameof(candidate));

            if (candidate.Effect.Status != ActiveEffectStatus.Active)
            {
                // Nothing to do: already Expired/Suspended/Removed by another path.
                return Result<ActiveEffectRecord>.Success(candidate);
            }

            CombatDurationBinding binding = candidate.Effect.CombatBinding.Value;
            Result<CombatEncounterRecord> encounterResult = encounters.Get(campaign, binding.EncounterId, correlationId);
            if (encounterResult.IsFailure)
            {
                // Fail-closed: an encounter we cannot read is inconclusive, not a reason to expire.
                return Result<ActiveEffectRecord>.Success(candidate);
            }

            CombatEncounterRecord encounter = encounterResult.Value;
            ActiveEffectExpiryDecision decision = durationType switch
            {
                EffectDurationType.ForRounds => ActiveEffectExpiryRules.CheckForRoundsExpiry(binding.AppliedRoundOrdinal, binding.RequiredCount, encounter.RoundOrdinal),
                EffectDurationType.ForTurns => ActiveEffectExpiryRules.CheckForTurnsExpiry(binding.RequiredCount, CountSince(lifecycleReader, campaign, encounter, binding, binding.TargetCombatantId, "TurnEnded", correlationId)),
                EffectDurationType.UntilSourceTurnStart => ActiveEffectExpiryRules.CheckUntilSourceTurnStartExpiry(HappenedSince(lifecycleReader, campaign, encounter, binding, binding.SourceCombatantId, "TurnStarted", correlationId)),
                EffectDurationType.UntilSourceTurnEnd => ActiveEffectExpiryRules.CheckUntilSourceTurnEndExpiry(HappenedSince(lifecycleReader, campaign, encounter, binding, binding.SourceCombatantId, "TurnEnded", correlationId)),
                EffectDurationType.UntilTargetTurnStart => ActiveEffectExpiryRules.CheckUntilTargetTurnStartExpiry(HappenedSince(lifecycleReader, campaign, encounter, binding, binding.TargetCombatantId, "TurnStarted", correlationId)),
                EffectDurationType.UntilTargetTurnEnd => ActiveEffectExpiryRules.CheckUntilTargetTurnEndExpiry(HappenedSince(lifecycleReader, campaign, encounter, binding, binding.TargetCombatantId, "TurnEnded", correlationId)),
                _ => throw new ArgumentOutOfRangeException(nameof(durationType))
            };

            return decision == ActiveEffectExpiryDecision.Expired
                ? activeEffects.ExpireActiveEffect(campaign, campaign.CampaignId, candidate.Effect.ActiveEffectId, candidate.Effect.Revision, commandId, correlationId)
                : Result<ActiveEffectRecord>.Success(candidate);
        }

        public static bool IsCombatDurationType(EffectDurationType durationType) => durationType switch
        {
            EffectDurationType.ForRounds => true,
            EffectDurationType.ForTurns => true,
            EffectDurationType.UntilSourceTurnStart => true,
            EffectDurationType.UntilSourceTurnEnd => true,
            EffectDurationType.UntilTargetTurnStart => true,
            EffectDurationType.UntilTargetTurnEnd => true,
            _ => false
        };

        /// <summary>Fail-closed: a combatant no longer present in the live encounter, or a lifecycle read failure, is inconclusive -- never treated as "the boundary happened."</summary>
        private static bool HappenedSince(ICombatEncounterLifecycleReader reader, CampaignHandle campaign, CombatEncounterRecord encounter, CombatDurationBinding binding, CharacterId? combatant, string eventKind, CorrelationId correlationId)
        {
            if (!combatant.HasValue || !IsParticipant(encounter, combatant.Value)) return false;
            Result<int> count = reader.CountLifecycleEvents(campaign, binding.EncounterId, combatant.Value, eventKind, binding.AppliedLifecycleEventId, correlationId);
            return count.IsSuccess && count.Value > 0;
        }

        /// <summary>Fail-closed: returns a negative count (per `CheckForTurnsExpiry`'s own contract) when the combatant is no longer a participant or the read fails, so the pure function never expires on inconclusive data.</summary>
        private static int CountSince(ICombatEncounterLifecycleReader reader, CampaignHandle campaign, CombatEncounterRecord encounter, CombatDurationBinding binding, CharacterId? combatant, string eventKind, CorrelationId correlationId)
        {
            if (!combatant.HasValue || !IsParticipant(encounter, combatant.Value)) return -1;
            Result<int> count = reader.CountLifecycleEvents(campaign, binding.EncounterId, combatant.Value, eventKind, binding.AppliedLifecycleEventId, correlationId);
            return count.IsSuccess ? count.Value : -1;
        }

        private static bool IsParticipant(CombatEncounterRecord encounter, CharacterId characterId)
        {
            for (int index = 0; index < encounter.Participants.Count; index++)
            {
                if (encounter.Participants[index].CharacterId == characterId) return true;
            }

            return false;
        }
    }
}
