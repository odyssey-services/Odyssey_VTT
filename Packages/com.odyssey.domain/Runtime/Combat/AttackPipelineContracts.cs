using System;
using System.Collections.Generic;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;

namespace Odyssey.Domain.Combat
{
    public enum EffectApplicationDecision { Apply = 1, DoNotApply = 2, RequiresIntervention = 3 }

    public readonly struct AttackRandomSample
    {
        public AttackRandomSample(int value) { Value = value; }
        public int Value { get; }
    }

    public sealed class AttackIntent
    {
        public AttackIntent(CombatEncounterId encounterId, CharacterId actorId, IReadOnlyList<CharacterId> targetIds, ContentDefinitionRef actionSource, long expectedEncounterRevision)
        {
            if (!encounterId.IsValid || !actorId.IsValid || targetIds == null || targetIds.Count == 0 || !actionSource.IsValid || expectedEncounterRevision < 1) throw new ArgumentException("Attack intent requires valid encounter, actor, targets, pinned action source and revision.");
            CharacterId[] copy = new CharacterId[targetIds.Count];
            for (int index = 0; index < targetIds.Count; index++)
            {
                if (!targetIds[index].IsValid) throw new ArgumentException("Target identity is required.", nameof(targetIds));
                for (int prior = 0; prior < index; prior++) if (copy[prior] == targetIds[index]) throw new ArgumentException("Attack targets must be distinct.", nameof(targetIds));
                copy[index] = targetIds[index];
            }
            EncounterId = encounterId; ActorId = actorId; TargetIds = Array.AsReadOnly(copy); ActionSource = actionSource; ExpectedEncounterRevision = expectedEncounterRevision;
        }
        public CombatEncounterId EncounterId { get; }
        public CharacterId ActorId { get; }
        public IReadOnlyList<CharacterId> TargetIds { get; }
        public ContentDefinitionRef ActionSource { get; }
        public long ExpectedEncounterRevision { get; }
    }

    public sealed class AttackEvaluationSnapshot
    {
        public AttackEvaluationSnapshot(string fingerprint, string rulesetId, string rulesetVersion, long encounterRevision)
        {
            if (string.IsNullOrWhiteSpace(fingerprint) || string.IsNullOrWhiteSpace(rulesetId) || string.IsNullOrWhiteSpace(rulesetVersion) || encounterRevision < 1) throw new ArgumentException("Snapshot values are required.");
            Fingerprint = fingerprint; RulesetId = rulesetId; RulesetVersion = rulesetVersion; EncounterRevision = encounterRevision;
        }
        public string Fingerprint { get; }
        public string RulesetId { get; }
        public string RulesetVersion { get; }
        public long EncounterRevision { get; }
    }

    public sealed class ProposedAttackResolution
    {
        public ProposedAttackResolution(AttackIntent intent, AttackEvaluationSnapshot snapshot, AttackRandomSample randomSample, IReadOnlyList<string> orderedModifiers, bool isInRange, bool isHit, EffectApplicationDecision effectDecision)
        { Intent = intent ?? throw new ArgumentNullException(nameof(intent)); Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot)); OrderedModifiers = orderedModifiers ?? throw new ArgumentNullException(nameof(orderedModifiers)); RandomSample = randomSample; IsInRange = isInRange; IsHit = isHit; EffectDecision = effectDecision; }
        public AttackIntent Intent { get; }
        public AttackEvaluationSnapshot Snapshot { get; }
        public AttackRandomSample RandomSample { get; }
        public IReadOnlyList<string> OrderedModifiers { get; }
        public bool IsInRange { get; }
        public bool IsHit { get; }
        public EffectApplicationDecision EffectDecision { get; }
    }
}
