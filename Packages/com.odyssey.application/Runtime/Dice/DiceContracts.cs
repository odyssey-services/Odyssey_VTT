using System;
using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Rules.Versions;

namespace Odyssey.Application.Dice
{
    /// <summary>09_Dice_And_Game_Log section 13.2's NaturalResult -- fixed at RNG time, never rewritten.</summary>
    public sealed class NaturalResult
    {
        public NaturalResult(int dieIndex, int groupIndex, int sides, int value)
        {
            DieIndex = dieIndex;
            GroupIndex = groupIndex;
            Sides = sides;
            Value = value;
        }

        public int DieIndex { get; }
        public int GroupIndex { get; }
        public int Sides { get; }
        public int Value { get; }
    }

    /// <summary>Section 12.1's ModifierDecision -- no "Rejected"-then-silently-omitted state: a rejected entry stays in the list, visible, with AppliedValue = 0.</summary>
    public enum ModifierDecision
    {
        Automatic = 1,
        Proposed = 2,
        Accepted = 3,
        Changed = 4,
        Rejected = 5,
    }

    /// <summary>
    /// Section 12.1's ModifierEntry / section 12.3's "no hidden numeric GM
    /// modifier" rule: every number entering FinalTotal is one of these,
    /// with a source, and is visible to the roll's audience -- there is no
    /// separate path for a GM to inject an unlabeled adjustment.
    /// </summary>
    public sealed class ModifierEntry
    {
        public ModifierEntry(string modifierEntryId, string sourceKind, string label, int value, UserId? proposedByUserId, ModifierDecision decision, UserId? decidedByUserId, string? decisionReason, int appliedValue)
        {
            if (string.IsNullOrWhiteSpace(modifierEntryId)) throw new ArgumentException("ModifierEntryId is required.", nameof(modifierEntryId));
            if (string.IsNullOrWhiteSpace(sourceKind)) throw new ArgumentException("SourceKind is required.", nameof(sourceKind));
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("Label is required.", nameof(label));

            ModifierEntryId = modifierEntryId;
            SourceKind = sourceKind;
            Label = label;
            Value = value;
            ProposedByUserId = proposedByUserId;
            Decision = decision;
            DecidedByUserId = decidedByUserId;
            DecisionReason = decisionReason;
            AppliedValue = appliedValue;
        }

        public string ModifierEntryId { get; }
        public string SourceKind { get; }
        public string Label { get; }
        public int Value { get; }
        public UserId? ProposedByUserId { get; }
        public ModifierDecision Decision { get; }
        public UserId? DecidedByUserId { get; }
        public string? DecisionReason { get; }

        /// <summary>The signed contribution actually counted into FinalTotal for this entry's current Decision.</summary>
        public int AppliedValue { get; }
    }

    /// <summary>Section 13.3's DiceRoll.Status.</summary>
    public enum DiceRollStatus
    {
        Resolved = 1,
        SupersededByReroll = 2,
        Cancelled = 3,
        Overridden = 4,
    }

    /// <summary>
    /// ODY-S03-006: 09_Dice_And_Game_Log section 16.1's four roll audience
    /// kinds. Kept distinct from <c>SceneEntityVisibility</c>
    /// (Odyssey.Application.Networking.Projection, ODY-S02-010's two-kind
    /// Public/HiddenGameplay model) per ADR-021 section 3.3: each consumer
    /// keeps its own already-documented vocabulary, not a forced-unified
    /// enum across roll/board/log.
    /// </summary>
    public enum DiceRollAudienceKind
    {
        Public = 1,
        PlayerAndGM = 2,
        GMOnly = 3,
        SelectedParticipants = 4,
    }

    /// <summary>
    /// Section 16.4: "Audience хранит стабильные ссылки на users/groups, а
    /// projection вычисляется по текущим permissions и membership" -- the
    /// references here (<see cref="SelectedUserIds"/>/<see cref="SelectedGroupIds"/>)
    /// are fixed at roll creation, but <c>DiceRollVisibilityPolicy</c>
    /// resolves group membership against the *current* directory state at
    /// view-build time, never a snapshot taken here (ADR-021 section 6's
    /// evaluation-time rule). Only meaningful when <see cref="Kind"/> is
    /// <see cref="DiceRollAudienceKind.SelectedParticipants"/>; empty for the
    /// other three kinds.
    /// </summary>
    public sealed class DiceRollAudience
    {
        private DiceRollAudience(DiceRollAudienceKind kind, IReadOnlyList<UserId> selectedUserIds, IReadOnlyList<string> selectedGroupIds)
        {
            Kind = kind;
            SelectedUserIds = selectedUserIds;
            SelectedGroupIds = selectedGroupIds;
        }

        public DiceRollAudienceKind Kind { get; }
        public IReadOnlyList<UserId> SelectedUserIds { get; }
        public IReadOnlyList<string> SelectedGroupIds { get; }

        public static DiceRollAudience Public() => new DiceRollAudience(DiceRollAudienceKind.Public, Array.Empty<UserId>(), Array.Empty<string>());
        public static DiceRollAudience PlayerAndGM() => new DiceRollAudience(DiceRollAudienceKind.PlayerAndGM, Array.Empty<UserId>(), Array.Empty<string>());
        public static DiceRollAudience GMOnly() => new DiceRollAudience(DiceRollAudienceKind.GMOnly, Array.Empty<UserId>(), Array.Empty<string>());

        public static DiceRollAudience SelectedParticipants(IReadOnlyList<UserId>? selectedUserIds, IReadOnlyList<string>? selectedGroupIds)
        {
            selectedUserIds ??= Array.Empty<UserId>();
            selectedGroupIds ??= Array.Empty<string>();
            if (selectedUserIds.Count == 0 && selectedGroupIds.Count == 0)
            {
                throw new ArgumentException("SelectedParticipants audience requires at least one selected user or group.");
            }

            return new DiceRollAudience(DiceRollAudienceKind.SelectedParticipants, selectedUserIds, selectedGroupIds);
        }
    }

    /// <summary>
    /// Section 13.1's DiceRoll entity, narrowed to what this task needs.
    /// Immutable per instance -- a modifier decision or status change
    /// produces a new instance with the same <see cref="RollId"/>, replacing
    /// the store's current version, the same "new value, same identity"
    /// pattern <c>Odyssey.Application.Board.BoardMovementService</c>'s
    /// <c>TokenRecord</c> already uses. <see cref="NaturalResults"/>,
    /// <see cref="FormulaOriginal"/>/<see cref="FormulaNormalized"/>, and
    /// <see cref="BaseTotal"/> never change after creation -- only
    /// <see cref="ModifierEntries"/> (while resolving), <see cref="Status"/>,
    /// and the derived <see cref="FinalTotal"/> can. <see cref="Audience"/>
    /// is fixed at creation (section 16.4's "стабильные ссылки"), consumed
    /// by ODY-S03-006's <c>DiceRollVisibilityPolicy</c>.
    /// </summary>
    public sealed class DiceRoll
    {
        public DiceRoll(
            string rollId, UserId actorUserId, string purpose, CampaignId campaignId,
            string formulaOriginal, string formulaNormalized, int formulaParserVersion,
            IReadOnlyList<NaturalResult> naturalResults, IReadOnlyList<ModifierEntry> modifierEntries,
            int baseTotal, int rngAlgorithmVersion, IReadOnlyList<RngProofData> rngProofs,
            DiceRollStatus status, string? previousRollId, UtcInstant createdAt, DiceRollAudience audience)
        {
            if (string.IsNullOrWhiteSpace(rollId)) throw new ArgumentException("RollId is required.", nameof(rollId));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (string.IsNullOrWhiteSpace(purpose)) throw new ArgumentException("Purpose is required.", nameof(purpose));
            if (audience == null) throw new ArgumentNullException(nameof(audience));

            RollId = rollId;
            ActorUserId = actorUserId;
            Purpose = purpose;
            CampaignId = campaignId;
            FormulaOriginal = formulaOriginal;
            FormulaNormalized = formulaNormalized;
            FormulaParserVersion = formulaParserVersion;
            NaturalResults = naturalResults;
            ModifierEntries = modifierEntries;
            BaseTotal = baseTotal;
            RngAlgorithmVersion = rngAlgorithmVersion;
            RngProofs = rngProofs;
            Status = status;
            PreviousRollId = previousRollId;
            CreatedAt = createdAt;
            Audience = audience;
        }

        public string RollId { get; }
        public UserId ActorUserId { get; }
        public string Purpose { get; }
        public CampaignId CampaignId { get; }
        public string FormulaOriginal { get; }
        public string FormulaNormalized { get; }
        public int FormulaParserVersion { get; }
        public IReadOnlyList<NaturalResult> NaturalResults { get; }
        public IReadOnlyList<ModifierEntry> ModifierEntries { get; }
        public int BaseTotal { get; }
        public int RngAlgorithmVersion { get; }
        public IReadOnlyList<RngProofData> RngProofs { get; }
        public DiceRollStatus Status { get; }
        public string? PreviousRollId { get; }
        public UtcInstant CreatedAt { get; }
        public DiceRollAudience Audience { get; }

        /// <summary>Section 13.4: base dice/constant total plus every currently-counted (Automatic/Accepted/Changed) modifier's AppliedValue -- never a hidden adjustment.</summary>
        public int FinalTotal
        {
            get
            {
                int total = BaseTotal;
                foreach (ModifierEntry entry in ModifierEntries)
                {
                    total += entry.AppliedValue;
                }

                return total;
            }
        }

        internal DiceRoll WithModifierEntries(IReadOnlyList<ModifierEntry> modifierEntries) =>
            new DiceRoll(RollId, ActorUserId, Purpose, CampaignId, FormulaOriginal, FormulaNormalized, FormulaParserVersion, NaturalResults, modifierEntries, BaseTotal, RngAlgorithmVersion, RngProofs, Status, PreviousRollId, CreatedAt, Audience);

        internal DiceRoll WithStatus(DiceRollStatus status) =>
            new DiceRoll(RollId, ActorUserId, Purpose, CampaignId, FormulaOriginal, FormulaNormalized, FormulaParserVersion, NaturalResults, ModifierEntries, BaseTotal, RngAlgorithmVersion, RngProofs, status, PreviousRollId, CreatedAt, Audience);
    }

    /// <summary>
    /// Section 19.1's RollOverride -- a separate, immutable record. The
    /// original <see cref="DiceRoll"/> is never rewritten by an override
    /// (section 19.2); only its <see cref="DiceRoll.Status"/> flips to
    /// <see cref="DiceRollStatus.Overridden"/> as a marker, its
    /// NaturalResults/FinalTotal untouched.
    /// </summary>
    public sealed class RollOverride
    {
        public RollOverride(string overrideId, string diceRollId, UserId actorUserId, string originalInterpretation, string appliedInterpretation, string reason, UtcInstant createdAt)
        {
            if (string.IsNullOrWhiteSpace(overrideId)) throw new ArgumentException("OverrideId is required.", nameof(overrideId));
            if (string.IsNullOrWhiteSpace(diceRollId)) throw new ArgumentException("DiceRollId is required.", nameof(diceRollId));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason is required.", nameof(reason));

            OverrideId = overrideId;
            DiceRollId = diceRollId;
            ActorUserId = actorUserId;
            OriginalInterpretation = originalInterpretation ?? string.Empty;
            AppliedInterpretation = appliedInterpretation ?? string.Empty;
            Reason = reason;
            CreatedAt = createdAt;
        }

        public string OverrideId { get; }
        public string DiceRollId { get; }
        public UserId ActorUserId { get; }
        public string OriginalInterpretation { get; }
        public string AppliedInterpretation { get; }
        public string Reason { get; }
        public UtcInstant CreatedAt { get; }
    }

    /// <summary>
    /// Roll intent. <see cref="ActorCanCreateRoll"/>/<see cref="ActorIsMainGm"/>
    /// are caller-supplied booleans -- this task has no session/role model of
    /// its own (deliberate simplification, mirroring
    /// <c>Odyssey.Application.Board.MoveTokenRequest.ActorIsMainGm</c>'s exact
    /// same pattern from ODY-S03-004), not a resolved <c>Roll.CreateCustom</c>
    /// permission from a real ADR-019 session. <see cref="Audience"/> is
    /// required, not defaulted -- ODY-S03-006: which audience a roll is
    /// visible to is a security-relevant choice this contract never leaves
    /// implicit.
    /// </summary>
    public sealed class SubmitRollRequest
    {
        public SubmitRollRequest(UserId actorUserId, bool actorCanCreateRoll, string purpose, string formula, DiceRollAudience audience, CampaignId campaignId, CommandId commandId, RulesetVersion rulesetVersion, RngKeyEpochId rngKeyEpochId, CorrelationId correlationId, IReadOnlyList<AutomaticModifierRequest>? automaticModifiers = null)
        {
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (string.IsNullOrWhiteSpace(purpose)) throw new ArgumentException("Purpose is required.", nameof(purpose));
            if (audience == null) throw new ArgumentNullException(nameof(audience));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (!rulesetVersion.IsValid) throw new ArgumentException("RulesetVersion is required.", nameof(rulesetVersion));
            if (!rngKeyEpochId.IsValid) throw new ArgumentException("RngKeyEpochId is required.", nameof(rngKeyEpochId));

            ActorUserId = actorUserId;
            ActorCanCreateRoll = actorCanCreateRoll;
            Purpose = purpose;
            Formula = formula;
            Audience = audience;
            CampaignId = campaignId;
            CommandId = commandId;
            RulesetVersion = rulesetVersion;
            RngKeyEpochId = rngKeyEpochId;
            CorrelationId = correlationId;
            AutomaticModifiers = automaticModifiers ?? Array.Empty<AutomaticModifierRequest>();
        }

        public UserId ActorUserId { get; }
        public bool ActorCanCreateRoll { get; }
        public string Purpose { get; }
        public string Formula { get; }
        public DiceRollAudience Audience { get; }
        public CampaignId CampaignId { get; }
        public CommandId CommandId { get; }
        public RulesetVersion RulesetVersion { get; }
        public RngKeyEpochId RngKeyEpochId { get; }
        public CorrelationId CorrelationId { get; }

        /// <summary>System-determined modifiers applied at roll time without a separate GM decision -- still visible ModifierEntry records (Decision=Automatic), not a hidden adjustment (section 12.3).</summary>
        public IReadOnlyList<AutomaticModifierRequest> AutomaticModifiers { get; }
    }

    public sealed class AutomaticModifierRequest
    {
        public AutomaticModifierRequest(string sourceKind, string label, int value)
        {
            SourceKind = sourceKind;
            Label = label;
            Value = value;
        }

        public string SourceKind { get; }
        public string Label { get; }
        public int Value { get; }
    }

    public sealed class ProposeModifierRequest
    {
        public ProposeModifierRequest(string rollId, UserId proposedByUserId, string sourceKind, string label, int value, CorrelationId correlationId)
        {
            RollId = rollId;
            ProposedByUserId = proposedByUserId;
            SourceKind = sourceKind;
            Label = label;
            Value = value;
            CorrelationId = correlationId;
        }

        public string RollId { get; }
        public UserId ProposedByUserId { get; }
        public string SourceKind { get; }
        public string Label { get; }
        public int Value { get; }
        public CorrelationId CorrelationId { get; }
    }

    public sealed class DecideModifierRequest
    {
        public DecideModifierRequest(string rollId, string modifierEntryId, UserId decidedByUserId, bool decidedByUserIsMainGm, ModifierDecision decision, int? changedValue, string? reason, CorrelationId correlationId)
        {
            RollId = rollId;
            ModifierEntryId = modifierEntryId;
            DecidedByUserId = decidedByUserId;
            DecidedByUserIsMainGm = decidedByUserIsMainGm;
            Decision = decision;
            ChangedValue = changedValue;
            Reason = reason;
            CorrelationId = correlationId;
        }

        public string RollId { get; }
        public string ModifierEntryId { get; }
        public UserId DecidedByUserId { get; }
        public bool DecidedByUserIsMainGm { get; }
        public ModifierDecision Decision { get; }
        public int? ChangedValue { get; }
        public string? Reason { get; }
        public CorrelationId CorrelationId { get; }
    }

    public sealed class ApplyOverrideRequest
    {
        public ApplyOverrideRequest(string rollId, UserId actorUserId, bool actorIsMainGm, string originalInterpretation, string appliedInterpretation, string? reason, CorrelationId correlationId)
        {
            RollId = rollId;
            ActorUserId = actorUserId;
            ActorIsMainGm = actorIsMainGm;
            OriginalInterpretation = originalInterpretation;
            AppliedInterpretation = appliedInterpretation;
            Reason = reason;
            CorrelationId = correlationId;
        }

        public string RollId { get; }
        public UserId ActorUserId { get; }
        public bool ActorIsMainGm { get; }
        public string OriginalInterpretation { get; }
        public CorrelationId CorrelationId { get; }
        public string AppliedInterpretation { get; }
        public string? Reason { get; }
    }

    public sealed class RequestFullRerollRequest
    {
        public RequestFullRerollRequest(string originalRollId, UserId actorUserId, bool actorIsMainGm, CommandId commandId, RulesetVersion rulesetVersion, RngKeyEpochId rngKeyEpochId, CorrelationId correlationId)
        {
            OriginalRollId = originalRollId;
            ActorUserId = actorUserId;
            ActorIsMainGm = actorIsMainGm;
            CommandId = commandId;
            RulesetVersion = rulesetVersion;
            RngKeyEpochId = rngKeyEpochId;
            CorrelationId = correlationId;
        }

        public string OriginalRollId { get; }
        public UserId ActorUserId { get; }
        public bool ActorIsMainGm { get; }
        public CommandId CommandId { get; }
        public RulesetVersion RulesetVersion { get; }
        public RngKeyEpochId RngKeyEpochId { get; }
        public CorrelationId CorrelationId { get; }
    }

    public sealed class CancelRollRequest
    {
        public CancelRollRequest(string rollId, UserId actorUserId, bool actorIsMainGm, string? reason, CorrelationId correlationId)
        {
            RollId = rollId;
            ActorUserId = actorUserId;
            ActorIsMainGm = actorIsMainGm;
            Reason = reason;
            CorrelationId = correlationId;
        }

        public string RollId { get; }
        public UserId ActorUserId { get; }
        public bool ActorIsMainGm { get; }
        public string? Reason { get; }
        public CorrelationId CorrelationId { get; }
    }

    public static class DiceFailures
    {
        public static Error RollDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.DiceRollDenied, ErrorCategory.Authorization, SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.dice.roll_denied"), RetryDirective.DoNotRetry, correlationId);

        public static Error InvalidFormula(CorrelationId correlationId) => Error.Create(
            ErrorCodes.DiceInvalidFormula, ErrorCategory.Validation, SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.dice.invalid_formula"), RetryDirective.DoNotRetry, correlationId);

        public static Error RollNotFound(CorrelationId correlationId) => Error.Create(
            ErrorCodes.DiceRollNotFound, ErrorCategory.NotFound, SafeReasonCode.TargetUnavailable,
            UserMessageKey.Parse("errors.dice.roll_not_found"), RetryDirective.DoNotRetry, correlationId);

        public static Error OverrideDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.DiceOverrideDenied, ErrorCategory.Authorization, SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.dice.override_denied"), RetryDirective.DoNotRetry, correlationId);

        public static Error OverrideReasonRequired(CorrelationId correlationId) => Error.Create(
            ErrorCodes.DiceOverrideReasonRequired, ErrorCategory.Validation, SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.dice.override_reason_required"), RetryDirective.DoNotRetry, correlationId);

        public static Error RerollDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.DiceRerollDenied, ErrorCategory.Authorization, SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.dice.reroll_denied"), RetryDirective.DoNotRetry, correlationId);

        public static Error CancelDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.DiceCancelDenied, ErrorCategory.Authorization, SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.dice.cancel_denied"), RetryDirective.DoNotRetry, correlationId);

        public static Error CancelReasonRequired(CorrelationId correlationId) => Error.Create(
            ErrorCodes.DiceCancelReasonRequired, ErrorCategory.Validation, SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.dice.cancel_reason_required"), RetryDirective.DoNotRetry, correlationId);

        public static Error ModifierNotFound(CorrelationId correlationId) => Error.Create(
            ErrorCodes.DiceModifierNotFound, ErrorCategory.NotFound, SafeReasonCode.TargetUnavailable,
            UserMessageKey.Parse("errors.dice.modifier_not_found"), RetryDirective.DoNotRetry, correlationId);

        public static Error ModifierDecisionReasonRequired(CorrelationId correlationId) => Error.Create(
            ErrorCodes.DiceModifierDecisionReasonRequired, ErrorCategory.Validation, SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.dice.modifier_decision_reason_required"), RetryDirective.DoNotRetry, correlationId);

        public static Error ModifierDecisionDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.DiceModifierDecisionDenied, ErrorCategory.Authorization, SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.dice.modifier_decision_denied"), RetryDirective.DoNotRetry, correlationId);
    }
}
