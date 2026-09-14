using System;
using System.Collections.Generic;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;

namespace Odyssey.Domain.Combat
{
    public enum EffectApplicationDecision { Apply = 1, DoNotApply = 2, RequiresIntervention = 3 }

    public readonly struct AttackRandomSample
    {
        public AttackRandomSample(int value) { Value = value; }
        public int Value { get; }
    }

    public readonly struct AttackRangeResult { public AttackRangeResult(bool isInRange, string reason) { IsInRange = isInRange; Reason = reason ?? throw new ArgumentNullException(nameof(reason)); } public bool IsInRange { get; } public string Reason { get; } }
    public readonly struct AttackModifierEntry { public AttackModifierEntry(string source, int value) { if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Source is required.", nameof(source)); Source = source; Value = value; } public string Source { get; } public int Value { get; } }
    public readonly struct AttackHitResult { public AttackHitResult(bool isHit, string outcome) { IsHit = isHit; Outcome = outcome ?? throw new ArgumentNullException(nameof(outcome)); } public bool IsHit { get; } public string Outcome { get; } }
    public readonly struct AttackBodyPartProposal { public AttackBodyPartProposal(string bodyPartRef) { if (string.IsNullOrWhiteSpace(bodyPartRef)) throw new ArgumentException("Body part is required.", nameof(bodyPartRef)); BodyPartRef = bodyPartRef; } public string BodyPartRef { get; } }
    public readonly struct AttackArmorProposal { public AttackArmorProposal(string armorRef, int absorbed) { ArmorRef = armorRef ?? throw new ArgumentNullException(nameof(armorRef)); Absorbed = absorbed; } public string ArmorRef { get; } public int Absorbed { get; } }
    public readonly struct AttackDelta { public AttackDelta(string targetRef, int value) { if (string.IsNullOrWhiteSpace(targetRef)) throw new ArgumentException("Target is required.", nameof(targetRef)); TargetRef = targetRef; Value = value; } public string TargetRef { get; } public int Value { get; } }
    public readonly struct AttackEffectCandidate { public AttackEffectCandidate(CharacterId targetId, ContentDefinitionRef effectRef, ContentDefinitionRef mechanicsRef, EffectApplicationDecision decision) { if (!targetId.IsValid || !effectRef.IsValid || !mechanicsRef.IsValid) throw new ArgumentException("Effect candidate is invalid."); TargetId = targetId; EffectRef = effectRef; MechanicsRef = mechanicsRef; Decision = decision; } public CharacterId TargetId { get; } public ContentDefinitionRef EffectRef { get; } public ContentDefinitionRef MechanicsRef { get; } public EffectApplicationDecision Decision { get; } }

    public sealed class AttackIntent
    {
        public AttackIntent(CombatEncounterId encounterId, CharacterId actorId, IReadOnlyList<CharacterId> targetIds, ItemInstanceId actionItemInstanceId, long expectedEncounterRevision)
        {
            if (!encounterId.IsValid || !actorId.IsValid || targetIds == null || targetIds.Count == 0 || !actionItemInstanceId.IsValid || expectedEncounterRevision < 1) throw new ArgumentException("Attack intent requires valid encounter, actor, targets, item source and revision.");
            CharacterId[] copy = new CharacterId[targetIds.Count];
            for (int index = 0; index < targetIds.Count; index++)
            {
                if (!targetIds[index].IsValid) throw new ArgumentException("Target identity is required.", nameof(targetIds));
                for (int prior = 0; prior < index; prior++) if (copy[prior] == targetIds[index]) throw new ArgumentException("Attack targets must be distinct.", nameof(targetIds));
                copy[index] = targetIds[index];
            }
            EncounterId = encounterId; ActorId = actorId; TargetIds = Array.AsReadOnly(copy); ActionItemInstanceId = actionItemInstanceId; ExpectedEncounterRevision = expectedEncounterRevision;
        }
        public CombatEncounterId EncounterId { get; }
        public CharacterId ActorId { get; }
        public IReadOnlyList<CharacterId> TargetIds { get; }
        public ItemInstanceId ActionItemInstanceId { get; }
        public long ExpectedEncounterRevision { get; }
    }

    public sealed class AttackEvaluationSnapshot
    {
        public AttackEvaluationSnapshot(string fingerprint, string rulesetId, string rulesetVersion, long encounterRevision, ContentDefinitionRef actionSourceRef, ItemMechanicsSnapshot actionMechanics)
        {
            if (string.IsNullOrWhiteSpace(fingerprint) || string.IsNullOrWhiteSpace(rulesetId) || string.IsNullOrWhiteSpace(rulesetVersion) || encounterRevision < 1 || !actionSourceRef.IsValid || !actionMechanics.SourceDefinitionRef.Equals(actionSourceRef)) throw new ArgumentException("Snapshot values are required.");
            Fingerprint = fingerprint; RulesetId = rulesetId; RulesetVersion = rulesetVersion; EncounterRevision = encounterRevision; ActionSourceRef = actionSourceRef; ActionMechanics = actionMechanics;
        }
        public string Fingerprint { get; }
        public string RulesetId { get; }
        public string RulesetVersion { get; }
        public long EncounterRevision { get; }
        public ContentDefinitionRef ActionSourceRef { get; }
        public ItemMechanicsSnapshot ActionMechanics { get; }
    }

    public sealed class ProposedAttackResolution
    {
        public ProposedAttackResolution(AttackIntent intent, AttackEvaluationSnapshot snapshot, AttackRandomSample? randomSample, AttackRangeResult range, IReadOnlyList<AttackModifierEntry> modifiers, AttackHitResult hit, AttackBodyPartProposal? bodyPart, AttackArmorProposal? armor, IReadOnlyList<AttackDelta> damageDeltas, IReadOnlyList<AttackDelta> costDeltas, IReadOnlyList<AttackEffectCandidate> effectCandidates)
        { Intent = intent ?? throw new ArgumentNullException(nameof(intent)); Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot)); RandomSample = randomSample; Range = range; Modifiers = Copy(modifiers, nameof(modifiers)); Hit = hit; BodyPart = bodyPart; Armor = armor; DamageDeltas = Copy(damageDeltas, nameof(damageDeltas)); CostDeltas = Copy(costDeltas, nameof(costDeltas)); EffectCandidates = Copy(effectCandidates, nameof(effectCandidates)); }
        public AttackIntent Intent { get; }
        public AttackEvaluationSnapshot Snapshot { get; }
        public AttackRandomSample? RandomSample { get; }
        public AttackRangeResult Range { get; }
        public IReadOnlyList<AttackModifierEntry> Modifiers { get; }
        public AttackHitResult Hit { get; }
        public AttackBodyPartProposal? BodyPart { get; }
        public AttackArmorProposal? Armor { get; }
        public IReadOnlyList<AttackDelta> DamageDeltas { get; }
        public IReadOnlyList<AttackDelta> CostDeltas { get; }
        public IReadOnlyList<AttackEffectCandidate> EffectCandidates { get; }
        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source, string name) { if (source == null) throw new ArgumentNullException(name); T[] copy = new T[source.Count]; for (int index = 0; index < copy.Length; index++) copy[index] = source[index]; return Array.AsReadOnly(copy); }
    }
}
