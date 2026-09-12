using System;
using Odyssey.Domain.Content;
using Odyssey.Domain.Effects;
using Odyssey.Domain.Time;
using Odyssey.Rules.Effects;

namespace Odyssey.Application.Effects
{
    /// <summary>ODY-S05-503: `ADR-028` §7's own outcome vocabulary for a stacking decision -- one value per behavior, plus the pending `RequestGMResolution` case.</summary>
    public enum ActiveEffectStackDecisionKind
    {
        /// <summary>`IndependentInstances`, or any policy's own "no conflicting row exists yet" case: always create a new, separate row.</summary>
        CreateNewEffect = 1,

        /// <summary>`RefreshDuration`: no new row; the existing row's `AppliedAt`/`ExpiresAt` are reset as if freshly applied now.</summary>
        RefreshExistingDuration = 2,

        /// <summary>`ReplaceExisting`, or `ReplaceIfStronger` when the candidate is stronger: the existing row transitions to `Removed` and a new row is created in its place.</summary>
        ReplaceExistingEffect = 3,

        /// <summary>`IncreaseStacks`: the existing row's `StackCount` increments by 1; `AppliedAt` is unchanged.</summary>
        IncreaseExistingStack = 4,

        /// <summary>`IgnoreNewApplication`, or `ReplaceIfStronger` when the candidate is not stronger: no new row, no mutation. The item-use/apply command itself still succeeds (`ADR-028` §7 rule 6's own "silent success" convention) -- only the stacking outcome is a no-op.</summary>
        IgnoreNewApplication = 5,

        /// <summary>`RequestGMResolution`: neither the new application nor any mutation of the existing row happens yet -- <see cref="ActiveEffectStackDecision.Conflict"/> carries the pending record a MainGM later resolves via <see cref="ActiveEffectStackingRules.ResolveActiveEffectStackConflict"/>.</summary>
        RequestGmResolution = 6
    }

    /// <summary>
    /// ODY-S05-503: the immutable outcome of <see cref="ActiveEffectStackingRules.ResolveStacking"/>
    /// or <see cref="ActiveEffectStackingRules.ResolveActiveEffectStackConflict"/> -- a pure
    /// description of what should happen, never itself performing any I/O.
    /// By direct structural analogy to `ItemDefinitionMigrationRules.cs`'s own
    /// `ComputeBlockingIssues`/`BuildPreview` precedent (`ODY-S05-401`/`402`):
    /// those methods decide, a *separate* later task's own repository method
    /// actually persists (`ODY-S05-403`'s own `ApplyItemDefinitionMigration`).
    /// `ODY-S05-503` is this block's own decision-only task; which future task
    /// wires a <see cref="ActiveEffectStackDecision"/> against real persistence
    /// is not decided here (see this task's own ExecPlan/task contract §18 for
    /// the full reasoning behind not adding a persistence method in this task).
    /// </summary>
    public sealed class ActiveEffectStackDecision
    {
        public ActiveEffectStackDecision(
            ActiveEffectStackDecisionKind kind,
            ActiveEffectRecord? effectToCreate,
            ActiveEffectId? existingActiveEffectId,
            UtcInstant? refreshedAppliedAt,
            UtcInstant? refreshedExpiresAt,
            ActiveEffectStackConflict? conflict)
        {
            switch (kind)
            {
                case ActiveEffectStackDecisionKind.CreateNewEffect:
                    if (effectToCreate == null) throw new ArgumentException("CreateNewEffect requires EffectToCreate.", nameof(effectToCreate));
                    if (existingActiveEffectId.HasValue || refreshedAppliedAt.HasValue || refreshedExpiresAt.HasValue || conflict != null)
                        throw new ArgumentException("CreateNewEffect must carry no other payload.", nameof(kind));
                    break;
                case ActiveEffectStackDecisionKind.RefreshExistingDuration:
                    if (!existingActiveEffectId.HasValue || !existingActiveEffectId.Value.IsValid) throw new ArgumentException("RefreshExistingDuration requires ExistingActiveEffectId.", nameof(existingActiveEffectId));
                    if (!refreshedAppliedAt.HasValue) throw new ArgumentException("RefreshExistingDuration requires RefreshedAppliedAt.", nameof(refreshedAppliedAt));
                    if (effectToCreate != null || conflict != null) throw new ArgumentException("RefreshExistingDuration must carry no create/conflict payload.", nameof(kind));
                    break;
                case ActiveEffectStackDecisionKind.ReplaceExistingEffect:
                    if (!existingActiveEffectId.HasValue || !existingActiveEffectId.Value.IsValid) throw new ArgumentException("ReplaceExistingEffect requires ExistingActiveEffectId.", nameof(existingActiveEffectId));
                    if (effectToCreate == null) throw new ArgumentException("ReplaceExistingEffect requires EffectToCreate.", nameof(effectToCreate));
                    if (refreshedAppliedAt.HasValue || refreshedExpiresAt.HasValue || conflict != null)
                        throw new ArgumentException("ReplaceExistingEffect must carry no refresh/conflict payload.", nameof(kind));
                    break;
                case ActiveEffectStackDecisionKind.IncreaseExistingStack:
                    if (!existingActiveEffectId.HasValue || !existingActiveEffectId.Value.IsValid) throw new ArgumentException("IncreaseExistingStack requires ExistingActiveEffectId.", nameof(existingActiveEffectId));
                    if (effectToCreate != null || refreshedAppliedAt.HasValue || refreshedExpiresAt.HasValue || conflict != null)
                        throw new ArgumentException("IncreaseExistingStack must carry no other payload.", nameof(kind));
                    break;
                case ActiveEffectStackDecisionKind.IgnoreNewApplication:
                    if (effectToCreate != null || existingActiveEffectId.HasValue || refreshedAppliedAt.HasValue || refreshedExpiresAt.HasValue || conflict != null)
                        throw new ArgumentException("IgnoreNewApplication must carry no payload.", nameof(kind));
                    break;
                case ActiveEffectStackDecisionKind.RequestGmResolution:
                    if (conflict == null) throw new ArgumentException("RequestGmResolution requires Conflict.", nameof(conflict));
                    if (effectToCreate != null || existingActiveEffectId.HasValue || refreshedAppliedAt.HasValue || refreshedExpiresAt.HasValue)
                        throw new ArgumentException("RequestGmResolution must carry no other payload.", nameof(kind));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }

            Kind = kind;
            EffectToCreate = effectToCreate;
            ExistingActiveEffectId = existingActiveEffectId;
            RefreshedAppliedAt = refreshedAppliedAt;
            RefreshedExpiresAt = refreshedExpiresAt;
            Conflict = conflict;
        }

        public ActiveEffectStackDecisionKind Kind { get; }

        /// <summary>Populated for <see cref="ActiveEffectStackDecisionKind.CreateNewEffect"/> and <see cref="ActiveEffectStackDecisionKind.ReplaceExistingEffect"/> -- the full row a caller should create.</summary>
        public ActiveEffectRecord? EffectToCreate { get; }

        /// <summary>Populated for every kind that acts on an already-existing row.</summary>
        public ActiveEffectId? ExistingActiveEffectId { get; }

        /// <summary>Populated only for <see cref="ActiveEffectStackDecisionKind.RefreshExistingDuration"/>.</summary>
        public UtcInstant? RefreshedAppliedAt { get; }

        /// <summary>Populated only for <see cref="ActiveEffectStackDecisionKind.RefreshExistingDuration"/>; null is itself meaningful (the refreshed effect has no computable expiry).</summary>
        public UtcInstant? RefreshedExpiresAt { get; }

        /// <summary>Populated only for <see cref="ActiveEffectStackDecisionKind.RequestGmResolution"/>.</summary>
        public ActiveEffectStackConflict? Conflict { get; }
    }

    /// <summary>
    /// ODY-S05-503: `ADR-028` §7 rule 7's own pending-conflict record for
    /// `RequestGMResolution` -- "creates `ActiveEffectStackConflict` pending
    /// record (`TargetRef`, new `EffectDefinitionRef`/snapshot/`SourceRef`,
    /// conflicting `ActiveEffectId`, `RaisedAt`)." Rather than duplicating
    /// those four fields as their own properties, this record wraps the full
    /// <see cref="ActiveEffectRecord"/> the new application would become if it
    /// were resolved as `ApplyAsIndependentInstance`/`Replace` -- that record
    /// already carries `TargetRef`/`EffectDefinitionRef`/`EffectMechanicsSnapshot`/
    /// `SourceRef` as its own fields, so wrapping it is a faithful,
    /// non-redundant representation of the same four fields `ADR-028` names,
    /// not a different shape.
    ///
    /// This is a pure Domain/Application-layer value object, not a persisted
    /// row (`ADR-028` §14's own non-goal: "a full UI for
    /// `ActiveEffectStackConflict` resolution -- only that the record and
    /// resolution command must exist"; this task's own ExecPlan/task contract
    /// §18 records the reasoning for deferring any cross-session persistence
    /// mechanism to whichever future task actually needs it).
    /// </summary>
    public sealed class ActiveEffectStackConflict
    {
        public ActiveEffectStackConflict(ActiveEffectRecord candidateApplication, ActiveEffectId conflictingActiveEffectId, UtcInstant raisedAt)
        {
            CandidateApplication = candidateApplication ?? throw new ArgumentNullException(nameof(candidateApplication));
            if (!conflictingActiveEffectId.IsValid) throw new ArgumentException("ConflictingActiveEffectId is required.", nameof(conflictingActiveEffectId));

            ConflictingActiveEffectId = conflictingActiveEffectId;
            RaisedAt = raisedAt;
        }

        /// <summary>The new application, as it would be created if this conflict is resolved as `ApplyAsIndependentInstance`/`Replace` -- carries `TargetRef`/`EffectDefinitionRef`/`EffectMechanicsSnapshot`/`SourceRef` per `ADR-028` §7 rule 7.</summary>
        public ActiveEffectRecord CandidateApplication { get; }

        /// <summary>The existing row this application conflicts with.</summary>
        public ActiveEffectId ConflictingActiveEffectId { get; }

        public UtcInstant RaisedAt { get; }
    }

    /// <summary>`ADR-028` §7 rule 7's own three resolution outcomes for a pending <see cref="ActiveEffectStackConflict"/>.</summary>
    public enum ActiveEffectStackConflictResolution
    {
        ApplyAsIndependentInstance = 1,
        Replace = 2,
        Ignore = 3
    }

    /// <summary>
    /// ODY-S05-503: `ADR-028` §7's own pure decision layer for all 7
    /// `EffectStackPolicy` behaviors, by direct structural analogy to
    /// `ItemDefinitionMigrationRules.ComputeBlockingIssues` (`ODY-S05-402`):
    /// a static method, no I/O, decoding nothing itself (the caller supplies
    /// an already-loaded, already-decoded <see cref="EffectStackPolicy"/> and
    /// any already-loaded conflicting <see cref="ActiveEffectRecord"/>),
    /// returning an immutable decision. Consumes `ODY-S05-502`'s own aggregate
    /// only for its value types -- it does not read or write the database.
    /// </summary>
    public static class ActiveEffectStackingRules
    {
        /// <summary>
        /// Resolves what should happen when <paramref name="candidateApplication"/>
        /// is applied to a target that may already have an `ActiveEffect` row for
        /// the exact same `EffectDefinitionRef` (<paramref name="existingConflictingEffect"/>,
        /// or <c>null</c> if this is the first application -- in which case every
        /// policy behaves the same way: create the new row, since there is
        /// nothing yet to stack against).
        /// </summary>
        /// <param name="stackPolicy">The applying `EffectDefinition`'s own `StackPolicy` (`ADR-028` §7) -- the caller decodes it via `TypedDefinitionCodec.DecodeEffect`, this method does not.</param>
        /// <param name="existingConflictingEffect">An already-loaded row sharing <paramref name="candidateApplication"/>'s own `TargetRef`+`EffectDefinitionRef`, or <c>null</c> if none exists yet.</param>
        /// <param name="candidateApplication">The full row that would be created if this were `IndependentInstances`/a first application -- already built by the caller with today's snapshot, `AppliedAt`, and (if computable) `ExpiresAt`.</param>
        /// <param name="now">The current host time, used only to stamp a <see cref="ActiveEffectStackConflict.RaisedAt"/> for `RequestGMResolution` -- never to compute duration/expiry, which this task does not own.</param>
        public static ActiveEffectStackDecision ResolveStacking(
            EffectStackPolicy stackPolicy,
            ActiveEffectRecord? existingConflictingEffect,
            ActiveEffectRecord candidateApplication,
            UtcInstant now)
        {
            if (candidateApplication == null) throw new ArgumentNullException(nameof(candidateApplication));
            if (!Enum.IsDefined(typeof(EffectStackPolicy), stackPolicy)) throw new ArgumentOutOfRangeException(nameof(stackPolicy));

            if (existingConflictingEffect != null)
            {
                if (!existingConflictingEffect.Effect.TargetRef.Equals(candidateApplication.Effect.TargetRef))
                    throw new ArgumentException("ExistingConflictingEffect must target the same TargetRef as the candidate application.", nameof(existingConflictingEffect));
                if (!existingConflictingEffect.Effect.EffectDefinitionRef.Equals(candidateApplication.Effect.EffectDefinitionRef))
                    throw new ArgumentException("ExistingConflictingEffect must reference the same EffectDefinitionRef as the candidate application.", nameof(existingConflictingEffect));
                if (!existingConflictingEffect.CampaignId.Equals(candidateApplication.CampaignId))
                    throw new ArgumentException("ExistingConflictingEffect must belong to the same campaign as the candidate application.", nameof(existingConflictingEffect));
            }

            // No conflicting row exists yet: every policy's own first-application
            // behavior is identical -- there is nothing to stack against.
            if (existingConflictingEffect == null)
            {
                return new ActiveEffectStackDecision(ActiveEffectStackDecisionKind.CreateNewEffect, candidateApplication, null, null, null, null);
            }

            ActiveEffectId existingId = existingConflictingEffect.Effect.ActiveEffectId;

            switch (stackPolicy)
            {
                case EffectStackPolicy.IndependentInstances:
                    return new ActiveEffectStackDecision(ActiveEffectStackDecisionKind.CreateNewEffect, candidateApplication, null, null, null, null);

                case EffectStackPolicy.RefreshDuration:
                    return new ActiveEffectStackDecision(ActiveEffectStackDecisionKind.RefreshExistingDuration, null, existingId,
                        candidateApplication.Effect.AppliedAt, candidateApplication.Effect.ExpiresAt, null);

                case EffectStackPolicy.ReplaceIfStronger:
                    bool stronger = EffectStackRules.IsStronger(candidateApplication.Effect.EffectMechanicsSnapshot, existingConflictingEffect.Effect.EffectMechanicsSnapshot);
                    return stronger
                        ? new ActiveEffectStackDecision(ActiveEffectStackDecisionKind.ReplaceExistingEffect, candidateApplication, existingId, null, null, null)
                        : new ActiveEffectStackDecision(ActiveEffectStackDecisionKind.IgnoreNewApplication, null, null, null, null, null);

                case EffectStackPolicy.ReplaceExisting:
                    return new ActiveEffectStackDecision(ActiveEffectStackDecisionKind.ReplaceExistingEffect, candidateApplication, existingId, null, null, null);

                case EffectStackPolicy.IncreaseStacks:
                    return new ActiveEffectStackDecision(ActiveEffectStackDecisionKind.IncreaseExistingStack, null, existingId, null, null, null);

                case EffectStackPolicy.IgnoreNewApplication:
                    return new ActiveEffectStackDecision(ActiveEffectStackDecisionKind.IgnoreNewApplication, null, null, null, null, null);

                case EffectStackPolicy.RequestGMResolution:
                    var conflict = new ActiveEffectStackConflict(candidateApplication, existingId, now);
                    return new ActiveEffectStackDecision(ActiveEffectStackDecisionKind.RequestGmResolution, null, null, null, null, conflict);

                default:
                    throw new ArgumentOutOfRangeException(nameof(stackPolicy));
            }
        }

        /// <summary>
        /// `ADR-028` §7 rule 7's own resolution command for a pending
        /// <see cref="ActiveEffectStackConflict"/> -- a pure function, no I/O,
        /// translating a MainGM's choice into the same
        /// <see cref="ActiveEffectStackDecision"/> shape <see cref="ResolveStacking"/>
        /// itself produces, so a caller has exactly one decision type to act
        /// on regardless of whether it came from an immediate policy or a
        /// resolved conflict.
        /// </summary>
        public static ActiveEffectStackDecision ResolveActiveEffectStackConflict(ActiveEffectStackConflict conflict, ActiveEffectStackConflictResolution resolution)
        {
            if (conflict == null) throw new ArgumentNullException(nameof(conflict));
            if (!Enum.IsDefined(typeof(ActiveEffectStackConflictResolution), resolution)) throw new ArgumentOutOfRangeException(nameof(resolution));

            return resolution switch
            {
                ActiveEffectStackConflictResolution.ApplyAsIndependentInstance =>
                    new ActiveEffectStackDecision(ActiveEffectStackDecisionKind.CreateNewEffect, conflict.CandidateApplication, null, null, null, null),
                ActiveEffectStackConflictResolution.Replace =>
                    new ActiveEffectStackDecision(ActiveEffectStackDecisionKind.ReplaceExistingEffect, conflict.CandidateApplication, conflict.ConflictingActiveEffectId, null, null, null),
                ActiveEffectStackConflictResolution.Ignore =>
                    new ActiveEffectStackDecision(ActiveEffectStackDecisionKind.IgnoreNewApplication, null, null, null, null, null),
                _ => throw new ArgumentOutOfRangeException(nameof(resolution))
            };
        }
    }
}
