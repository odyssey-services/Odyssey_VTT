using System;
using Odyssey.Application.Commands;
using Odyssey.Application.Dice;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Checks;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Checks
{
    /// <summary>
    /// ODY-S07-102: `ADR-031` section 9's own standalone command -- read-only authoritative state seam,
    /// mirroring `IActivateAbilityStateReader`/`IAttackStateReader`'s own established shape. Implementations
    /// must not write. No `CanControlActor` method: `ADR-031` section 7 (no `CharacterRecord` section is
    /// mutated by a check) and `RecordCriticalSuccessEvidence`'s own explicit "no permission gate" design
    /// (`ODY-S04-106`) together mean this command has no discretionary authorization decision to make --
    /// see `CheckContracts.cs`'s own `CheckRequest` for the recorded reasoning, not a silent omission.
    /// </summary>
    public interface ICheckStateReader
    {
        Result<CheckParticipantState> Read(CampaignHandle campaign, CharacterId actorId, CorrelationId correlationId);
    }

    /// <summary>
    /// The command's own public surface -- a raw formula string (`ADR-030` section 6.2's shared grammar,
    /// reused verbatim, `ADR-031` section 6), a caller-supplied difficulty class (section 3.3: a plain
    /// number, never a published catalog lookup), and a required `DiceRollAudience` (`DiceContracts.cs`'s
    /// own "security-relevant choice this contract never leaves implicit" rule, continued here rather than
    /// hardcoded inside `CheckService`).
    /// </summary>
    public sealed class CheckIntent
    {
        public CheckIntent(CharacterId actorId, string formula, long difficultyClass, DiceRollAudience audience)
        {
            if (!actorId.IsValid) throw new ArgumentException("ActorId is required.", nameof(actorId));
            if (string.IsNullOrWhiteSpace(formula)) throw new ArgumentException("Formula is required.", nameof(formula));
            if (difficultyClass < 0) throw new ArgumentOutOfRangeException(nameof(difficultyClass), "DifficultyClass must be >= 0.");
            if (audience == null) throw new ArgumentNullException(nameof(audience));

            ActorId = actorId;
            Formula = formula;
            DifficultyClass = difficultyClass;
            Audience = audience;
        }

        public CharacterId ActorId { get; }
        public string Formula { get; }
        public long DifficultyClass { get; }
        public DiceRollAudience Audience { get; }
    }

    /// <summary>
    /// No `ActorIsMainGm`/`expectedXxxRevision` field -- an explicit, disclosed divergence from
    /// `ActivateAbilityRequest`/`AttackRequest`'s own established shape, not an oversight (`ADR-031`
    /// section 7): a check never mutates any `CharacterRecord` section (`Attributes`/`Skills` are read
    /// only), so there is no section revision to declare and re-check; and its only potential write,
    /// `RecordCriticalSuccessEvidence`, itself accepts no revision parameter and performs no permission
    /// gate (confirmed by direct signature/doc-comment read of `ICharacterRepository.RecordCriticalSuccessEvidence`).
    /// </summary>
    public sealed class CheckRequest
    {
        public CheckRequest(CheckIntent intent, UserId actorUserId, CommandId commandId, CorrelationId correlationId)
        {
            Intent = intent ?? throw new ArgumentNullException(nameof(intent));
            if (!actorUserId.IsValid || !commandId.IsValid || !correlationId.IsValid) throw new ArgumentException("Actor, command and correlation identities are required.");
            ActorUserId = actorUserId;
            CommandId = commandId;
            CorrelationId = correlationId;
        }

        public CheckIntent Intent { get; }
        public UserId ActorUserId { get; }
        public CommandId CommandId { get; }
        public CorrelationId CorrelationId { get; }
    }

    /// <summary>
    /// ODY-S07-102: the durable record of one `SubmitCheck`/`PerformCheck` command's own already-applied
    /// outcome -- mirrors `AbilityActivationRecord`/`AttackOutcomeRecord`'s own idempotency-read role
    /// exactly (`apply.GetCheckOutcome`, checked BEFORE `DiceRollService.SubmitRoll` is ever called,
    /// `ADR-008` rule 14). No `CompensatedAt`/`CompensationStartedAt` -- a check has no resource delta or
    /// created-effect side effect to compensate (`ADR-031` imposes no compensation requirement on checks
    /// the way `ADR-030`'s own doработка eventually required for `ActivateAbility`'s resource+effect
    /// side effects); its only potential side effect, `RecordCriticalSuccessEvidence`, is itself already
    /// idempotent by `CommandId` through `ICharacterRepository`'s own existing ledger mechanism.
    /// </summary>
    public sealed class CheckOutcomeRecord
    {
        public CheckOutcomeRecord(CommandId commandId, CampaignId campaignId, CharacterId actorId, string formula, long difficultyClass, string diceRollId, CheckResultKind result, bool isNaturalMaximum, SkillDefinitionId? resolvedSkill, UtcInstant occurredAt)
        {
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!actorId.IsValid) throw new ArgumentException("ActorId is required.", nameof(actorId));
            if (string.IsNullOrWhiteSpace(formula)) throw new ArgumentException("Formula is required.", nameof(formula));
            if (string.IsNullOrWhiteSpace(diceRollId)) throw new ArgumentException("DiceRollId is required.", nameof(diceRollId));

            CommandId = commandId;
            CampaignId = campaignId;
            ActorId = actorId;
            Formula = formula;
            DifficultyClass = difficultyClass;
            DiceRollId = diceRollId;
            Result = result;
            IsNaturalMaximum = isNaturalMaximum;
            ResolvedSkill = resolvedSkill;
            OccurredAt = occurredAt;
        }

        public CommandId CommandId { get; }
        public CampaignId CampaignId { get; }
        public CharacterId ActorId { get; }
        public string Formula { get; }
        public long DifficultyClass { get; }
        public string DiceRollId { get; }
        public CheckResultKind Result { get; }
        public bool IsNaturalMaximum { get; }
        public SkillDefinitionId? ResolvedSkill { get; }
        public UtcInstant OccurredAt { get; }
    }

    /// <summary>
    /// ODY-S07-102: the write side of the check command -- a standalone repository, by the same precedent
    /// `IActivateAbilityRepository`/`IAttackApplyRepository` already established as separate from
    /// `ICharacterRepository`'s own general CRUD surface.
    /// </summary>
    public interface ICheckRepository
    {
        /// <summary>Idempotency read, called BEFORE `DiceRollService.SubmitRoll` -- see `IActivateAbilityRepository.GetActivation`'s own identical role and doc comment for why this ordering is mandatory, not merely conventional, for this command.</summary>
        Result<CheckOutcomeRecord> GetCheckOutcome(CampaignHandle campaign, CommandId commandId, CorrelationId correlationId);

        /// <summary>Atomically records the check's own final, already-computed outcome -- no resource delta, no created effect, nothing else to apply.</summary>
        Result<CheckOutcomeRecord> RecordCheckOutcome(CampaignHandle campaign, CharacterId actorId, string formula, long difficultyClass, string diceRollId, CheckResultKind result, bool isNaturalMaximum, SkillDefinitionId? resolvedSkill, CommandId commandId, CorrelationId correlationId);
    }
}
