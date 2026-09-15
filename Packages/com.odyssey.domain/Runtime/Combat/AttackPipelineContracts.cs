using System;
using System.Collections.Generic;
using Odyssey.Domain.Content;
using Odyssey.Domain.Character;
using Odyssey.Domain.Effects;
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

    public enum AttackInputAvailability { Available = 1, UnavailableNotBound = 2 }
    // ODY-S05-603: carries only lifecycle/approval state for fingerprinting and Rules input.
    // Character-side RulesetVersion is deliberately not read here -- ADR-029 requires only that
    // the encounter-sourced RulesetVersion travel in AttackEvaluationSnapshot (already true below);
    // it does not require cross-checking it against any Character-side value. See ODY-S05-603 task
    // contract's RulesetVersion decision record for the full citation.
    // ODY-S06-102: AttributeValues carries the participant's own already-loaded AttributeValue.EffectiveValue
    // readings, keyed by the same AttributeDefinitionId catalog key the Character aggregate already uses --
    // no new attribute-representation format. This is what makes ADR-030 section 6.2's attributeReference
    // formula term resolvable (e.g. "1d6+STR") without a second database read.
    public readonly struct AttackParticipantState
    {
        public AttackParticipantState(CharacterId characterId, CharacterLifecycleStatus lifecycleStatus, CharacterApprovalState approvalState, IReadOnlyDictionary<AttributeDefinitionId, long> attributeValues)
        {
            if (!characterId.IsValid) throw new ArgumentException("Participant state is invalid.");
            if (attributeValues == null) throw new ArgumentNullException(nameof(attributeValues));
            CharacterId = characterId;
            LifecycleStatus = lifecycleStatus;
            ApprovalState = approvalState;
            var copy = new Dictionary<AttributeDefinitionId, long>(attributeValues.Count);
            foreach (KeyValuePair<AttributeDefinitionId, long> entry in attributeValues) copy[entry.Key] = entry.Value;
            AttributeValues = copy;
        }
        public CharacterId CharacterId { get; }
        public CharacterLifecycleStatus LifecycleStatus { get; }
        public CharacterApprovalState ApprovalState { get; }
        public IReadOnlyDictionary<AttributeDefinitionId, long> AttributeValues { get; }
    }
    public readonly struct AttackUnavailableInput { public AttackUnavailableInput(AttackInputAvailability availability, string reason) { if (availability != AttackInputAvailability.UnavailableNotBound || string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Only explicit unavailable input is valid."); Availability = availability; Reason = reason; } public AttackInputAvailability Availability { get; } public string Reason { get; } }

    public enum AttackArmorAvailability { Available = 1, UnavailableNotBound = 2 }

    /// <summary>ODY-S06-103: one target's own one piece of currently-equipped armor, decoded into its real content-catalog <see cref="ArmorDefinition"/>. A target may carry several entries (e.g. a helmet and a breastplate both equipped) -- which piece, if any, a real hit actually consults is `ODY-S06-105`'s own aggregation/selection decision, not this type's.</summary>
    public readonly struct AttackTargetArmorEntry
    {
        public AttackTargetArmorEntry(CharacterId targetId, ArmorDefinition armor)
        {
            if (!targetId.IsValid) throw new ArgumentException("TargetId is required.", nameof(targetId));
            TargetId = targetId;
            Armor = armor ?? throw new ArgumentNullException(nameof(armor));
        }
        public CharacterId TargetId { get; }
        public ArmorDefinition Armor { get; }
    }

    /// <summary>
    /// ODY-S06-103: replaces <c>AttackEvaluationSnapshot.ArmorAndEffects</c>'s former <see cref="AttackUnavailableInput"/>
    /// typing -- that type's own constructor structurally rejects any <see cref="AttackInputAvailability"/> but
    /// <c>UnavailableNotBound</c> (`TC-ATTACK-025`), so it could never be extended to carry real armor data.
    /// <see cref="Unavailable"/> preserves the exact prior "not bound" semantics for when no target in the
    /// intent has any equipped armor at all; <see cref="Available"/> carries every real, decoded
    /// <see cref="ArmorDefinition"/> each target currently has equipped, tagged by <see cref="AttackTargetArmorEntry.TargetId"/>.
    /// </summary>
    public sealed class AttackArmorInput
    {
        private AttackArmorInput(AttackArmorAvailability availability, string reason, IReadOnlyList<AttackTargetArmorEntry> entries)
        {
            Availability = availability;
            Reason = reason;
            Entries = entries;
        }

        public static AttackArmorInput Unavailable(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason is required.", nameof(reason));
            return new AttackArmorInput(AttackArmorAvailability.UnavailableNotBound, reason, Array.Empty<AttackTargetArmorEntry>());
        }

        public static AttackArmorInput Available(IReadOnlyList<AttackTargetArmorEntry> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (entries.Count == 0) throw new ArgumentException("Available armor input requires at least one entry; use Unavailable when no target has any equipped armor.", nameof(entries));
            AttackTargetArmorEntry[] copy = new AttackTargetArmorEntry[entries.Count];
            for (int index = 0; index < copy.Length; index++) copy[index] = entries[index];
            return new AttackArmorInput(AttackArmorAvailability.Available, string.Empty, Array.AsReadOnly(copy));
        }

        public AttackArmorAvailability Availability { get; }
        public string Reason { get; }
        public IReadOnlyList<AttackTargetArmorEntry> Entries { get; }
    }

    public enum AttackTopologyAvailability { Available = 1, UnavailableNotBound = 2 }

    /// <summary>
    /// ODY-S06-104: one target's own already-computed Euclidean distance from the actor (`Odyssey.Domain.Geometry.BoardGeometry.EuclideanDistance`,
    /// world units/meters, the same scale `WeaponDefinition.Range` is read in). The distance, not the two raw
    /// positions, is what travels here -- computing it is a pure, deterministic operation with no Ruleset/business
    /// meaning of its own, so it belongs in this data-wiring layer; deciding what counts as "in range" (comparing
    /// this distance against `WeaponDefinition.Range`) is `ODY-S06-105`'s own job, producing `AttackRangeResult`.
    /// </summary>
    public readonly struct AttackTargetDistanceEntry
    {
        public AttackTargetDistanceEntry(CharacterId targetId, double distance)
        {
            if (!targetId.IsValid) throw new ArgumentException("TargetId is required.", nameof(targetId));
            if (!double.IsFinite(distance) || distance < 0) throw new ArgumentOutOfRangeException(nameof(distance));
            TargetId = targetId;
            Distance = distance;
        }
        public CharacterId TargetId { get; }
        public double Distance { get; }
    }

    /// <summary>
    /// ODY-S06-104: replaces <c>AttackEvaluationSnapshot.Topology</c>'s former <see cref="AttackUnavailableInput"/>
    /// typing -- the same structural dead end `ODY-S06-103` already found and fixed for `ArmorAndEffects`
    /// (that type's own constructor rejects any <see cref="AttackInputAvailability"/> but <c>UnavailableNotBound</c>).
    /// <see cref="Unavailable"/> preserves the exact prior "not bound" semantics for when no target's distance
    /// could be resolved (missing Character-Token link on either side, or actor/target tokens on different Scenes);
    /// <see cref="Available"/> carries every target whose distance WAS resolvable, tagged by
    /// <see cref="AttackTargetDistanceEntry.TargetId"/> -- a target that could not be resolved is simply absent
    /// from <see cref="Entries"/>, not a reason to fail the whole intent (unlike `ODY-S06-103`'s own deliberate
    /// equip-status gate, missing position data is not a new hard-fail).
    /// </summary>
    public sealed class AttackTopologyInput
    {
        private AttackTopologyInput(AttackTopologyAvailability availability, string reason, IReadOnlyList<AttackTargetDistanceEntry> entries)
        {
            Availability = availability;
            Reason = reason;
            Entries = entries;
        }

        public static AttackTopologyInput Unavailable(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason is required.", nameof(reason));
            return new AttackTopologyInput(AttackTopologyAvailability.UnavailableNotBound, reason, Array.Empty<AttackTargetDistanceEntry>());
        }

        public static AttackTopologyInput Available(IReadOnlyList<AttackTargetDistanceEntry> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (entries.Count == 0) throw new ArgumentException("Available topology input requires at least one entry; use Unavailable when no target's distance could be resolved.", nameof(entries));
            AttackTargetDistanceEntry[] copy = new AttackTargetDistanceEntry[entries.Count];
            for (int index = 0; index < copy.Length; index++) copy[index] = entries[index];
            return new AttackTopologyInput(AttackTopologyAvailability.Available, string.Empty, Array.AsReadOnly(copy));
        }

        public AttackTopologyAvailability Availability { get; }
        public string Reason { get; }
        public IReadOnlyList<AttackTargetDistanceEntry> Entries { get; }
    }

    public readonly struct AttackRangeResult { public AttackRangeResult(bool isInRange, string reason) { IsInRange = isInRange; Reason = reason ?? throw new ArgumentNullException(nameof(reason)); } public bool IsInRange { get; } public string Reason { get; } }
    public readonly struct AttackModifierEntry { public AttackModifierEntry(string source, int value) { if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Source is required.", nameof(source)); Source = source; Value = value; } public string Source { get; } public int Value { get; } }
    public readonly struct AttackHitResult { public AttackHitResult(bool isHit, string outcome) { IsHit = isHit; Outcome = outcome ?? throw new ArgumentNullException(nameof(outcome)); } public bool IsHit { get; } public string Outcome { get; } }
    public readonly struct AttackBodyPartProposal { public AttackBodyPartProposal(string bodyPartRef) { if (string.IsNullOrWhiteSpace(bodyPartRef)) throw new ArgumentException("Body part is required.", nameof(bodyPartRef)); BodyPartRef = bodyPartRef; } public string BodyPartRef { get; } }
    public readonly struct AttackArmorProposal { public AttackArmorProposal(string armorRef, int absorbed) { ArmorRef = armorRef ?? throw new ArgumentNullException(nameof(armorRef)); Absorbed = absorbed; } public string ArmorRef { get; } public int Absorbed { get; } }
    public readonly struct AttackDelta { public AttackDelta(string targetRef, int value) { if (string.IsNullOrWhiteSpace(targetRef)) throw new ArgumentException("Target is required.", nameof(targetRef)); TargetRef = targetRef; Value = value; } public string TargetRef { get; } public int Value { get; } }
    /// <summary>ODY-S05-606: `ADR-029` §8's own public-safe reason-category vocabulary for an `EffectApplicationDecision` -- a small closed enum, by analogy to `SafeReasonCode`'s own "public-safe, no free text" convention, not a reuse of that unrelated error-reason enum (this is a trigger-evaluation reason, not an error).</summary>
    public enum EffectApplicationReasonCategory
    {
        TriggerConditionMet = 1,
        TriggerConditionNotMet = 2,
        RequiresInterventionChoice = 3,
    }

    /// <summary>
    /// ODY-S05-603: `ADR-029` §8's own `EffectApplicationDecision` payload,
    /// computed by `Odyssey.Rules` at stage 11 (unmodified by `ODY-S05-606`).
    /// ODY-S05-606 backward-compatibly extends this with the remaining §8
    /// fields the original 4-field shape omitted: the public-safe
    /// <see cref="ReasonCategory"/> plus <see cref="HostOnlyReasonDetail"/>
    /// (the "host-only factual inputs needed to reproduce it"),
    /// <see cref="DurationBinding"/> (`ODY-S05-605`'s own `CombatDurationBinding`,
    /// reused verbatim -- "duration bindings where a combat duration is
    /// selected"), <see cref="StackPolicy"/> (`ADR-028`'s own existing
    /// `EffectStackPolicy`, reused verbatim -- "stacking key/options consumed
    /// by `ADR-028`'s existing `EffectStackPolicy` handling"), and
    /// <see cref="MechanicsSnapshot"/> (the effect's own already-pinned
    /// mechanics content -- `ADR-029` §8's own "not discovered by loading the
    /// latest content definition" rule means the full snapshot, not just a
    /// reference, must travel with the candidate for apply-time use; a real
    /// gap the original 4-field shape left unfillable, found and closed by
    /// this task -- see the ODY-S05-606 task contract's decision log). The
    /// original 4-argument constructor is preserved unchanged for every
    /// existing caller (`ODY-S05-603`/`604`'s own test fixtures) and now
    /// delegates to the extended one with explicit, documented defaults.
    /// </summary>
    public readonly struct AttackEffectCandidate
    {
        /// <summary>Preserved for backward compatibility with every existing caller. Defaults: <see cref="EffectApplicationReasonCategory.TriggerConditionMet"/>, no host-only detail, no combat duration binding, <see cref="EffectStackPolicy.IndependentInstances"/>, and a minimal placeholder <see cref="EffectMechanicsSnapshot"/> derived from <paramref name="mechanicsRef"/> -- callers that need the real §8 fields must use the extended constructor.</summary>
        public AttackEffectCandidate(CharacterId targetId, ContentDefinitionRef effectRef, ContentDefinitionRef mechanicsRef, EffectApplicationDecision decision)
            : this(targetId, effectRef, mechanicsRef, decision, EffectApplicationReasonCategory.TriggerConditionMet, string.Empty, null, EffectStackPolicy.IndependentInstances, new EffectMechanicsSnapshot(mechanicsRef, mechanicsRef.Version, ContentDefinitionType.Effect, "{}"))
        {
        }

        public AttackEffectCandidate(CharacterId targetId, ContentDefinitionRef effectRef, ContentDefinitionRef mechanicsRef, EffectApplicationDecision decision, EffectApplicationReasonCategory reasonCategory, string hostOnlyReasonDetail, CombatDurationBinding? durationBinding, EffectStackPolicy stackPolicy, EffectMechanicsSnapshot mechanicsSnapshot)
        {
            if (!targetId.IsValid || !effectRef.IsValid || !mechanicsRef.IsValid) throw new ArgumentException("Effect candidate is invalid.");
            if (!Enum.IsDefined(typeof(EffectApplicationDecision), decision)) throw new ArgumentOutOfRangeException(nameof(decision));
            if (!Enum.IsDefined(typeof(EffectApplicationReasonCategory), reasonCategory)) throw new ArgumentOutOfRangeException(nameof(reasonCategory));
            if (hostOnlyReasonDetail == null) throw new ArgumentNullException(nameof(hostOnlyReasonDetail));
            if (!Enum.IsDefined(typeof(EffectStackPolicy), stackPolicy)) throw new ArgumentOutOfRangeException(nameof(stackPolicy));
            if (!mechanicsSnapshot.SourceDefinitionRef.Equals(mechanicsRef)) throw new ArgumentException("MechanicsSnapshot must match MechanicsRef.", nameof(mechanicsSnapshot));

            TargetId = targetId;
            EffectRef = effectRef;
            MechanicsRef = mechanicsRef;
            Decision = decision;
            ReasonCategory = reasonCategory;
            HostOnlyReasonDetail = hostOnlyReasonDetail;
            DurationBinding = durationBinding;
            StackPolicy = stackPolicy;
            MechanicsSnapshot = mechanicsSnapshot;
        }

        public CharacterId TargetId { get; }
        public ContentDefinitionRef EffectRef { get; }
        public ContentDefinitionRef MechanicsRef { get; }
        public EffectApplicationDecision Decision { get; }

        /// <summary>Public-safe: what caused this decision, without exposing hidden Ruleset facts.</summary>
        public EffectApplicationReasonCategory ReasonCategory { get; }

        /// <summary>Host-only: the factual inputs needed to reproduce/audit the decision -- never sent to a client.</summary>
        public string HostOnlyReasonDetail { get; }

        /// <summary>Populated only when the applying effect's own duration is one of `ADR-028` §13's six combat values; null otherwise.</summary>
        public CombatDurationBinding? DurationBinding { get; }

        /// <summary>The applying effect definition's own `ADR-028` §7 stacking policy -- consumed by the existing `ODY-S05-503` stacking-policy decision layer, never re-decided here.</summary>
        public EffectStackPolicy StackPolicy { get; }

        /// <summary>The effect's own already-pinned mechanics content (`ADR-029` §8: "not discovered by loading the latest content definition").</summary>
        public EffectMechanicsSnapshot MechanicsSnapshot { get; }
    }

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
        public AttackEvaluationSnapshot(string fingerprint, string rulesetId, string rulesetVersion, long encounterRevision, ContentDefinitionRef actionSourceRef, ItemMechanicsSnapshot actionMechanics, AttackParticipantState actor, IReadOnlyList<AttackParticipantState> targets, AttackTopologyInput topology, AttackArmorInput armorAndEffects)
        {
            if (string.IsNullOrWhiteSpace(fingerprint) || string.IsNullOrWhiteSpace(rulesetId) || string.IsNullOrWhiteSpace(rulesetVersion) || encounterRevision < 1 || !actionSourceRef.IsValid || !actionMechanics.SourceDefinitionRef.Equals(actionSourceRef)) throw new ArgumentException("Snapshot values are required.");
            if (actor.CharacterId == default || targets == null || topology == null || armorAndEffects == null) throw new ArgumentException("Read participant state is required.");
            Fingerprint = fingerprint; RulesetId = rulesetId; RulesetVersion = rulesetVersion; EncounterRevision = encounterRevision; ActionSourceRef = actionSourceRef; ActionMechanics = actionMechanics; Actor = actor; Targets = Copy(targets, nameof(targets)); Topology = topology; ArmorAndEffects = armorAndEffects;
        }
        public string Fingerprint { get; }
        public string RulesetId { get; }
        public string RulesetVersion { get; }
        public long EncounterRevision { get; }
        public ContentDefinitionRef ActionSourceRef { get; }
        public ItemMechanicsSnapshot ActionMechanics { get; }
        public AttackParticipantState Actor { get; }
        public IReadOnlyList<AttackParticipantState> Targets { get; }
        public AttackTopologyInput Topology { get; }
        public AttackArmorInput ArmorAndEffects { get; }
        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source, string name) { T[] copy = new T[source.Count]; for (int index = 0; index < copy.Length; index++) copy[index] = source[index]; return Array.AsReadOnly(copy); }
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
