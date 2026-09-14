using System;
using Odyssey.Domain.Content;
using Odyssey.Domain.Time;
using Odyssey.Rules.Effects;

namespace Odyssey.Application.Effects
{
    /// <summary>`ADR-028` §8's own two-outcome expiry vocabulary for every non-`Instant`, non-`WhileItemEquipped` duration mechanism this task owns.</summary>
    public enum ActiveEffectExpiryDecision
    {
        NotExpired = 1,
        Expired = 2
    }

    /// <summary>
    /// `ADR-028` §8's own vocabulary for the three duration types whose
    /// expiry is driven entirely by an external event firing, with no further
    /// condition to evaluate once it does (unlike `WhileCondition`, which is
    /// re-evaluated and can be inconclusive). This task defines only this
    /// shared trigger-kind vocabulary and the fact that firing always
    /// produces <see cref="ActiveEffectExpiryDecision.Expired"/> -- no
    /// publisher for any of the three exists yet anywhere in this codebase
    /// (confirmed by direct search: no `SceneActivated`/`SceneChanged` event,
    /// no session-end signal, no `ItemConsumed`/`ItemDestroyed` event),
    /// mirroring `ADR-027` §8.2 rule 3's own "form without publisher"
    /// precedent already established for `WhileItemEquipped`. A future task
    /// (`ODY-S05-505` or later) must supply the real event(s) and call
    /// <see cref="ActiveEffectExpiryRules.OnExternalTriggerFired"/> at the
    /// right moment -- this task does not wire any of the three to a real
    /// publisher.
    /// </summary>
    public enum ActiveEffectExternalExpiryTriggerKind
    {
        UntilSceneChange = 1,
        UntilSessionEnd = 2,
        WhileSourceExists = 3
    }

    /// <summary>
    /// ODY-S05-504: `ADR-028` §8's own 8 presently-implementable, non-`WhileItemEquipped`
    /// `EffectDurationType` expiry mechanisms, by direct structural analogy to
    /// `ODY-S05-503`'s own `ActiveEffectStackingRules` (same file-directory/
    /// namespace, `public static class`, pure functions, no I/O) and
    /// `ItemDefinitionMigrationRules.ComputeBlockingIssues` (the "decide only,
    /// a separate caller applies" precedent). Consumes `ODY-S05-502`'s own
    /// `ActiveEffect`/`EffectMechanicsSnapshot` types only for their value
    /// shape -- no repository call happens in this class itself.
    ///
    /// Every one of the 8 values this task owns (`Instant`/`Permanent`/
    /// `UntilRemoved`/`UntilSceneChange`/`UntilSessionEnd`/`WhileCondition`/
    /// `WhileSourceExists`/`ForDuration`) is handled explicitly below, per
    /// `ADR-028` §8's own "none is left undecided by omission" decision:
    /// <list type="bullet">
    /// <item><description><see cref="EffectDurationType.Instant"/>: never reaches this class at all -- `ADR-028` §8's own table: "No `ActiveEffect` row is created at all," so there is nothing to check. Deliberately no method here accepts this value.</description></item>
    /// <item><description><see cref="EffectDurationType.Permanent"/>/<see cref="EffectDurationType.UntilRemoved"/>: <see cref="CheckNoAutomaticExpiry"/> -- always <see cref="ActiveEffectExpiryDecision.NotExpired"/>, an explicit no-op rather than simply never being called.</description></item>
    /// <item><description><see cref="EffectDurationType.ForDuration"/>: <see cref="CheckForDurationExpiry"/> -- a real host-clock check.</description></item>
    /// <item><description><see cref="EffectDurationType.WhileCondition"/>: <see cref="CheckWhileConditionExpiry"/> -- consumes <see cref="EffectConditionRules.Evaluate"/>'s own result, implementing `ADR-028` §11's fail-closed rule as an explicit code path.</description></item>
    /// <item><description><see cref="EffectDurationType.UntilSceneChange"/>/<see cref="EffectDurationType.UntilSessionEnd"/>/<see cref="EffectDurationType.WhileSourceExists"/>: <see cref="OnExternalTriggerFired"/> -- a contract-only handler shape, no real publisher wired (see <see cref="ActiveEffectExternalExpiryTriggerKind"/>'s own doc comment).</description></item>
    /// </list>
    /// `WhileItemEquipped` and every turn/round-based value are explicitly
    /// out of this task's own scope (`ODY-S05-505` and the full attack
    /// pipeline block, respectively) -- no method here accepts them.
    /// </summary>
    public static class ActiveEffectExpiryRules
    {
        /// <summary>`ADR-028` §8: `Permanent`/`UntilRemoved` never expire automatically -- only `ODY-S05-506`'s own explicit `RemoveActiveEffect` ends them. This method exists so a caller has an explicit, documented answer for these two duration types rather than silently skipping them.</summary>
        public static ActiveEffectExpiryDecision CheckNoAutomaticExpiry(EffectDurationType durationType)
        {
            if (durationType != EffectDurationType.Permanent && durationType != EffectDurationType.UntilRemoved)
                throw new ArgumentException("CheckNoAutomaticExpiry is only valid for Permanent/UntilRemoved.", nameof(durationType));

            return ActiveEffectExpiryDecision.NotExpired;
        }

        /// <summary>
        /// `ADR-028` §8: a real host-clock expiry check for `ForDuration` --
        /// "an `ExpiresAt` timestamp exists and something checks it." Takes
        /// <paramref name="now"/> as a parameter rather than reading a clock
        /// itself, the same idiom <c>CharacterOwnership.IsActiveAt</c> and
        /// <c>DiagnosticBundleContracts.IsExpired</c> already establish in
        /// this codebase for an analogous wall-clock comparison.
        /// </summary>
        public static ActiveEffectExpiryDecision CheckForDurationExpiry(UtcInstant? expiresAt, UtcInstant now)
        {
            if (!expiresAt.HasValue) throw new ArgumentException("A ForDuration effect must have a computed ExpiresAt.", nameof(expiresAt));

            return now.CompareTo(expiresAt.Value) >= 0 ? ActiveEffectExpiryDecision.Expired : ActiveEffectExpiryDecision.NotExpired;
        }

        /// <summary>
        /// `ADR-028` §11's own fail-closed rule as an explicit code path:
        /// <see cref="EffectConditionEvaluationResult.Inconclusive"/> always
        /// yields <see cref="ActiveEffectExpiryDecision.NotExpired"/> -- never
        /// a defensive `Expired`. The caller is responsible for mapping any
        /// evaluator exception to <see cref="EffectConditionEvaluationResult.Inconclusive"/>
        /// before calling this method, per `ADR-028` §11's own "a `Rules`-layer
        /// error" inconclusive case.
        /// </summary>
        public static ActiveEffectExpiryDecision CheckWhileConditionExpiry(EffectConditionEvaluationResult conditionResult)
        {
            return conditionResult switch
            {
                EffectConditionEvaluationResult.ConditionHolds => ActiveEffectExpiryDecision.NotExpired,
                EffectConditionEvaluationResult.ConditionFailed => ActiveEffectExpiryDecision.Expired,
                EffectConditionEvaluationResult.Inconclusive => ActiveEffectExpiryDecision.NotExpired,
                _ => throw new ArgumentOutOfRangeException(nameof(conditionResult))
            };
        }

        /// <summary>
        /// The handler shape a future task's own real event publisher would
        /// call once one of the three external triggers actually fires for
        /// this effect's own campaign/target/source. See
        /// <see cref="ActiveEffectExternalExpiryTriggerKind"/>'s own doc
        /// comment for why no publisher is wired here. Firing any of the
        /// three always ends the effect -- there is no further condition to
        /// evaluate, unlike `WhileCondition`.
        /// </summary>
        public static ActiveEffectExpiryDecision OnExternalTriggerFired(ActiveEffectExternalExpiryTriggerKind triggerKind)
        {
            if (!Enum.IsDefined(typeof(ActiveEffectExternalExpiryTriggerKind), triggerKind)) throw new ArgumentOutOfRangeException(nameof(triggerKind));

            return ActiveEffectExpiryDecision.Expired;
        }

        /// <summary>
        /// ODY-S05-605: `ADR-029` §7's own `ForRounds` boundary -- "an effect
        /// committed in round R with N = 1 expires before actions in round
        /// R + 2," i.e. the boundary round is <c>R + N + 1</c>. Pure ordinal
        /// arithmetic against the encounter's own live `RoundOrdinal`; no I/O.
        /// Fail-closed: a <paramref name="currentRoundOrdinal"/> that
        /// precedes <paramref name="appliedRoundOrdinal"/> (stale/corrupt
        /// input) never expires the effect.
        /// </summary>
        public static ActiveEffectExpiryDecision CheckForRoundsExpiry(long appliedRoundOrdinal, int requiredRounds, long currentRoundOrdinal)
        {
            if (appliedRoundOrdinal < 1) throw new ArgumentOutOfRangeException(nameof(appliedRoundOrdinal));
            if (requiredRounds < 1) throw new ArgumentOutOfRangeException(nameof(requiredRounds));
            if (currentRoundOrdinal < 1) throw new ArgumentOutOfRangeException(nameof(currentRoundOrdinal));

            if (currentRoundOrdinal < appliedRoundOrdinal) return ActiveEffectExpiryDecision.NotExpired;
            return currentRoundOrdinal >= appliedRoundOrdinal + requiredRounds + 1 ? ActiveEffectExpiryDecision.Expired : ActiveEffectExpiryDecision.NotExpired;
        }

        /// <summary>
        /// ODY-S05-605: `ADR-029` §7's own `ForTurns` boundary -- "the effect
        /// expires at the `TurnEnded` boundary of the Nth counted target
        /// turn," counting only the target's own completed turns starting
        /// strictly after application (not a global turn count). The caller
        /// supplies <paramref name="completedTargetTurnsSinceApplication"/>
        /// already counted from `CombatEncounterLifecycleEvent` audit rows
        /// (an I/O concern this pure function never performs itself).
        /// Fail-closed: a negative count (the caller's own signal that it
        /// could not conclusively count, e.g. the target left the encounter)
        /// never expires the effect.
        /// </summary>
        public static ActiveEffectExpiryDecision CheckForTurnsExpiry(int requiredTurns, int completedTargetTurnsSinceApplication)
        {
            if (requiredTurns < 1) throw new ArgumentOutOfRangeException(nameof(requiredTurns));

            if (completedTargetTurnsSinceApplication < 0) return ActiveEffectExpiryDecision.NotExpired;
            return completedTargetTurnsSinceApplication >= requiredTurns ? ActiveEffectExpiryDecision.Expired : ActiveEffectExpiryDecision.NotExpired;
        }

        /// <summary>ODY-S05-605: `ADR-029` §7's own `UntilSourceTurnStart` boundary. <paramref name="sourceTurnStartedSinceApplication"/> is the caller's own already-derived, fail-closed-by-construction answer (false when inconclusive, e.g. the source left the encounter) to "has the source combatant's `TurnStarted` fired strictly after application."</summary>
        public static ActiveEffectExpiryDecision CheckUntilSourceTurnStartExpiry(bool sourceTurnStartedSinceApplication)
            => sourceTurnStartedSinceApplication ? ActiveEffectExpiryDecision.Expired : ActiveEffectExpiryDecision.NotExpired;

        /// <summary>ODY-S05-605: `ADR-029` §7's own `UntilSourceTurnEnd` boundary. <paramref name="sourceTurnEndedSinceApplication"/> is the caller's own already-derived, fail-closed-by-construction answer to "has the source combatant's `TurnEnded` fired strictly after application."</summary>
        public static ActiveEffectExpiryDecision CheckUntilSourceTurnEndExpiry(bool sourceTurnEndedSinceApplication)
            => sourceTurnEndedSinceApplication ? ActiveEffectExpiryDecision.Expired : ActiveEffectExpiryDecision.NotExpired;

        /// <summary>ODY-S05-605: `ADR-029` §7's own `UntilTargetTurnStart` boundary. <paramref name="targetTurnStartedSinceApplication"/> is the caller's own already-derived, fail-closed-by-construction answer to "has the target combatant's `TurnStarted` fired strictly after application."</summary>
        public static ActiveEffectExpiryDecision CheckUntilTargetTurnStartExpiry(bool targetTurnStartedSinceApplication)
            => targetTurnStartedSinceApplication ? ActiveEffectExpiryDecision.Expired : ActiveEffectExpiryDecision.NotExpired;

        /// <summary>ODY-S05-605: `ADR-029` §7's own `UntilTargetTurnEnd` boundary. <paramref name="targetTurnEndedSinceApplication"/> is the caller's own already-derived, fail-closed-by-construction answer to "has the target combatant's `TurnEnded` fired strictly after application."</summary>
        public static ActiveEffectExpiryDecision CheckUntilTargetTurnEndExpiry(bool targetTurnEndedSinceApplication)
            => targetTurnEndedSinceApplication ? ActiveEffectExpiryDecision.Expired : ActiveEffectExpiryDecision.NotExpired;
    }
}
