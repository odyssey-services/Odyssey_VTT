using System;
using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// ODY-S04-101: the Character aggregate skeleton port (ADR-022 section 4).
    /// Mirrors <see cref="ISceneRepository"/>'s exact Application-port /
    /// Odyssey.Persistence.Sqlite-implementation split, for a new aggregate
    /// with ADR-022's own section-revision model instead of a single overall
    /// revision.
    ///
    /// Deliberately narrow, per SLICE-04_IMPLEMENTATION_BACKLOG.md section 6
    /// ("ODY-S04-101"): this port only creates a Character and edits its
    /// Identity/Presentation sections to prove the section-revision/lock
    /// mechanism and the history projection. It does not implement Ownership
    /// commands (ODY-S04-102), Draft/template binding (ODY-S04-103/104),
    /// development economy (ODY-S04-105-107), ability/resource/anatomy
    /// (ODY-S04-108/109), or any lifecycle-boundary operation beyond exposing
    /// the structural <see cref="CharacterLifecycleStatus"/>/
    /// <see cref="CharacterApprovalState"/> values themselves
    /// (ODY-S04-110/111).
    /// </summary>
    public interface ICharacterRepository
    {
        Result<CharacterRecord> CreateCharacter(CreateCharacterRequest request, CommandId commandId, CorrelationId correlationId);
        Result<CharacterRecord> GetCharacter(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId);

        /// <summary>
        /// ADR-022 section 5: declares exactly the Identity section's expected
        /// revision -- an in-flight edit to Presentation (or any other
        /// section) never conflicts with this call, and vice versa.
        /// </summary>
        Result<CharacterRecord> UpdateIdentity(CampaignHandle campaign, CharacterId characterId, string newDisplayName, long expectedIdentityRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ADR-022 section 5: declares exactly the Presentation section's
        /// expected revision -- independent of <see cref="UpdateIdentity"/>.
        /// </summary>
        Result<CharacterRecord> UpdatePresentation(CampaignHandle campaign, CharacterId characterId, string? portraitReference, long expectedPresentationRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ADR-022 section 8: rebuilds this Character's history purely from
        /// the append-only <c>DomainEvents</c> journal -- never from a
        /// separately-maintained, independently mutable history table. Called
        /// with no eagerly-maintained projection row involved at all in this
        /// task's own implementation, so a correct result here is direct proof
        /// of "rebuildable from events," not merely "read back what was
        /// separately written."
        /// </summary>
        Result<IReadOnlyList<CharacterHistoryEntry>> GetCharacterHistory(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-102: ADR-025 section 4.2. <paramref name="actorIsMainGm"/>
        /// is the same caller-supplied-boolean baseline simplification
        /// <c>BoardMovementService</c>/<c>DiceRollService</c> already use for
        /// MainGM-gated operations (ADR-019's own accepted simplification,
        /// not reopened here) -- not a new permission-decision service.
        /// Declares only the <c>Ownership</c> section's expected revision;
        /// never silently changes <see cref="CharacterOwnership.CoOwnerUserIds"/>/
        /// control grants.
        /// </summary>
        Result<CharacterRecord> AssignPrimaryOwner(CampaignHandle campaign, CharacterId characterId, UserId newPrimaryOwnerUserId, string reasonCode, bool actorIsMainGm, long expectedOwnershipRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>ODY-S04-102: ADR-025 section 4.3, <c>Character.ManageOwnership</c>-gated. A duplicate add of an already-present co-owner does not append a second entry.</summary>
        Result<CharacterRecord> AddCharacterCoOwner(CampaignHandle campaign, CharacterId characterId, UserId coOwnerUserId, bool actorIsMainGm, long expectedOwnershipRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>ODY-S04-102: ADR-025 section 4.3, <c>Character.ManageOwnership</c>-gated.</summary>
        Result<CharacterRecord> RemoveCharacterCoOwner(CampaignHandle campaign, CharacterId characterId, UserId coOwnerUserId, bool actorIsMainGm, long expectedOwnershipRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>ODY-S04-102: ADR-025 section 4.3, <c>Character.ManageOwnership</c>-gated.</summary>
        Result<CharacterRecord> GrantPermanentCharacterControl(CampaignHandle campaign, CharacterId characterId, UserId controlUserId, bool actorIsMainGm, long expectedOwnershipRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-102: ADR-025 section 4.3, <c>Character.ManageOwnership</c>-gated.
        /// <paramref name="expiresAt"/> is optional, caller-supplied, stored
        /// provenance only -- see <see cref="CharacterTemporaryControlGrant"/>'s
        /// own doc comment for why no automatic expiry-enforcement mechanism
        /// is introduced here.
        /// </summary>
        Result<CharacterRecord> GrantTemporaryCharacterControl(CampaignHandle campaign, CharacterId characterId, UserId controlUserId, UtcInstant? expiresAt, bool actorIsMainGm, long expectedOwnershipRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>ODY-S04-102: ADR-025 section 4.3, <c>Character.ManageOwnership</c>-gated. Revokes both a permanent controller entry and/or a temporary grant for the given user, whichever is present.</summary>
        Result<CharacterRecord> RevokeCharacterControl(CampaignHandle campaign, CharacterId characterId, UserId controlUserId, bool actorIsMainGm, long expectedOwnershipRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-103: ADR-023 section 4.2 -- creates exactly one, permanent
        /// ADR-022 Character aggregate instance, at <c>LifecycleStatus=Draft</c>/
        /// <c>ApprovalState=Draft</c>. This is the ADR-023-compliant real
        /// creation path (deep-copy-with-fresh-identifiers, compatibility
        /// validation, RulesetVersion pinning, initial owner) -- unlike
        /// <see cref="CreateCharacter"/>, which remains ODY-S04-101's own
        /// bare skeleton path, unmodified, so existing callers/tests are
        /// unaffected. Per ADR-023 section 4.4, a GM-created, never-local
        /// Draft is simply this same method invoked with
        /// <see cref="CharacterCreationSeed.None"/> and no preceding
        /// <c>CreateLocalCharacterDraft</c>.
        /// </summary>
        Result<CharacterRecord> BindDraftToCampaign(BindDraftToCampaignRequest request, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-104: ADR-023 section 7.1 -- a light revision check, not a
        /// lock; does not change <see cref="CharacterApprovalState"/>. This
        /// task's own decision for what is actually mutated: <see cref="CharacterRecord.SubmittedAt"/>,
        /// gated by the <c>Lifecycle</c> section's own revision (the same
        /// section key ADR-022 section 6 already reserves), since submitting
        /// is a workflow-visibility fact about the Character as a whole, not
        /// a value any other existing section already owns. Only legal while
        /// <see cref="CharacterLifecycleStatus"/> is <see cref="Character.CharacterLifecycleStatus.Draft"/>.
        /// </summary>
        Result<CharacterRecord> SubmitCharacterDraft(CampaignHandle campaign, CharacterId characterId, long expectedLifecycleRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-104: ADR-023 section 7.1 -- a conflict-free append,
        /// architecturally the same shape as a <c>GameLogEntry</c> append
        /// (ADR-002 section 17.1). Requires no <c>ExpectedCharacterRevision</c>/
        /// section revision at all; never touches <see cref="CharacterRecord.Revisions"/>
        /// and can never conflict with, or be conflicted by, any other
        /// Character command.
        /// </summary>
        Result<CharacterReviewCommentRecord> AddCharacterReviewComment(CampaignHandle campaign, CharacterId characterId, UserId authorUserId, string text, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-104: ADR-023 section 7.1/7.3 -- <c>Character.Approve</c>,
        /// MainGM-only (<paramref name="actorIsMainGm"/>, the same caller-
        /// supplied-boolean convention <see cref="AssignPrimaryOwner"/> already
        /// uses). Declares the expected <c>LifecycleRevision</c>. The sole
        /// state-legality gate is <see cref="CharacterLifecycleTransitions.IsValidTransition"/>
        /// on the current <see cref="CharacterLifecycleStatus"/> -&gt;
        /// <see cref="Character.CharacterLifecycleStatus.Active"/> edge -- not a
        /// duplicated ad hoc check -- so a repeat call on an already-<c>Active</c>
        /// Character is rejected because that table's own same-status rule
        /// returns false, not because of a separate business precondition.
        /// <see cref="CharacterLifecycleStatus"/> and <see cref="CharacterApprovalState"/>
        /// change together in the same <c>UPDATE</c> statement inside the
        /// same transaction <see cref="SqliteSavingPipeline"/> already commits
        /// atomically -- there is no intermediate state where one field
        /// changed and the other did not.
        /// </summary>
        Result<CharacterRecord> ApproveCharacterDraft(CampaignHandle campaign, CharacterId characterId, bool actorIsMainGm, long expectedLifecycleRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>ODY-S04-104: reads the full, ordered review-comment thread for one Character -- no audience/redaction filtering (product names no hidden-comment concept), matching <see cref="IGameLogRepository.ListGameLog"/>'s own "Persistence stores everything" convention.</summary>
        Result<IReadOnlyList<CharacterReviewCommentRecord>> GetCharacterReviewComments(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-105: ADR-024 section 4/5, product section 12.2 --
        /// <c>MainGM</c>-only (<paramref name="actorIsMainGm"/>, the same
        /// caller-supplied-boolean convention <see cref="AssignPrimaryOwner"/>
        /// already uses). Increases <c>DevelopmentPool.Earned</c>, gated by
        /// <c>MechanicsRevision</c> (ADR-024 section 4.2: pool fields are
        /// <c>Mechanics</c>-level metadata). Commits the pool update, a
        /// <c>DevelopmentPointsGranted</c> event, and a co-committed
        /// <c>DevelopmentTransaction</c> (<c>Kind=Grant</c>) ledger row in one
        /// transaction. <see cref="CommandId"/>/<c>AppliedCommands</c> remain
        /// the sole idempotency mechanism (ADR-024 section 5) -- no second
        /// economy-specific dedup key.
        /// </summary>
        Result<CharacterRecord> GrantDevelopmentPoints(CampaignHandle campaign, CharacterId characterId, long amount, string reason, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-105: ADR-024 section 5.1, product section 11/13.1 --
        /// an ordinary immediate purchase (no reservation, ADR-024 section
        /// 6.1 -- reservation is reserved for a genuinely pending operation,
        /// ODY-S04-106's own scope, not this command). Permission: MainGM or
        /// an assigned user of this Character (<see cref="CharacterOwnershipAssignment.IsAssignedCharacter"/>,
        /// reused from ODY-S04-102, not duplicated), matching product section
        /// 13.1's "у пользователя есть право развивать персонажа." Declares
        /// both <paramref name="expectedMechanicsRevision"/> (the pool) and
        /// <paramref name="expectedAttributeRevision"/> (the addressed
        /// <c>AttributeValue</c>'s own entry-level revision, ADR-024 section
        /// 4.2's <c>AttributeValue:&lt;AttributeDefinitionId&gt;</c> lock
        /// key) -- an attribute never previously purchased has revision
        /// <c>0</c>. Cost/cap come from <see cref="Odyssey.Rules.Character.AttributeCostRules"/>,
        /// this task's own explicitly-flagged test fixture -- no Ruleset-
        /// catalog mechanism exists yet anywhere in this codebase. Commits
        /// the pool decrement, the attribute's new <c>BaseValue</c>/
        /// <c>Revision</c>, an <c>AttributeIncreased</c> event, and a
        /// co-committed <c>DevelopmentTransaction</c> (<c>Kind=Spend</c>)
        /// ledger row in one transaction -- <see cref="CommandId"/>/
        /// <c>AppliedCommands</c> are the sole duplicate-spend guard.
        /// </summary>
        Result<CharacterRecord> PurchaseAttributeIncrease(CampaignHandle campaign, CharacterId characterId, AttributeDefinitionId attributeDefinitionId, long toValue, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, long expectedAttributeRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>ODY-S04-105: reads the full development ledger for one Character, ordered by <see cref="DevelopmentTransactionRecord.CreatedAt"/> -- matching <see cref="IGameLogRepository.ListGameLog"/>'s own "Persistence stores everything" convention. Rebuildable from <c>DomainEvents</c> if ever lost (ADR-024 section 4.3); this method reads the co-committed ledger table directly, the same way <see cref="GetCharacter"/> reads current state directly rather than rebuilding it from events on every call.</summary>
        Result<IReadOnlyList<DevelopmentTransactionRecord>> GetDevelopmentLedger(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-106: product section 14.2 -- an ordinary immediate
        /// purchase, exactly mirroring <see cref="PurchaseAttributeIncrease"/>'s
        /// own shape for a <c>CharacterSkill</c> entry instead of an
        /// <c>AttributeValue</c>. Rejected with <c>CharacterSkillLevelRequiresRecommendation</c>
        /// if <paramref name="toLevel"/> exceeds <see cref="Odyssey.Rules.Character.SkillCostRules.MaxOrdinaryPurchaseLevel"/>
        /// (product section 14.3's "levels 5+" boundary) -- that path is
        /// <see cref="RequestSkillAdvancedRecommendation"/>'s own job, never
        /// this command's. Cost comes from <see cref="Odyssey.Rules.Character.SkillCostRules"/>,
        /// this task's own explicitly-flagged test fixture.
        /// </summary>
        Result<CharacterRecord> PurchaseSkillLevel(CampaignHandle campaign, CharacterId characterId, SkillDefinitionId skillDefinitionId, long toLevel, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, long expectedSkillRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-106: ADR-024 section 3.5/7.1, product section 14.4 --
        /// records one immutable <c>CriticalSuccessEvidence</c> fact. The
        /// actual trigger (a critical success during a skill check) is a
        /// Rules Engine/dice-integration concern this task does not
        /// implement; this method is the durable recording primitive a
        /// future dice-integration task, or this task's own tests, use to
        /// produce real evidence rows for <see cref="RequestSkillAdvancedRecommendation"/>
        /// to reference. No permission gate -- recording an observed game
        /// fact is not a discretionary decision the way granting points or
        /// approving a recommendation is.
        /// </summary>
        Result<CriticalSuccessEvidenceRecord> RecordCriticalSuccessEvidence(CampaignHandle campaign, CharacterId characterId, SkillDefinitionId skillDefinitionId, string? sourceDiceRollId, string? sourceActionId, CommandId commandId, CorrelationId correlationId);

        /// <summary>ODY-S04-106: reads every recorded evidence row for one Character -- matching <see cref="IGameLogRepository.ListGameLog"/>'s own "Persistence stores everything" convention.</summary>
        Result<IReadOnlyList<CriticalSuccessEvidenceRecord>> GetCriticalSuccessEvidence(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-106: ADR-024 section 6.1 step 1 -- validates
        /// <see cref="DevelopmentPool.Available"/> against the amount the
        /// recommendation may eventually cost (<see cref="Odyssey.Rules.Character.SkillCostRules"/>'s
        /// own fixture cost for the addressed skill's target level), then,
        /// in one transaction, moves that amount from <c>Available</c> into
        /// <c>Reserved</c> (<c>DevelopmentTransaction.Kind=Reserve</c>) and
        /// creates the durable <c>AdvancementRecommendation</c> pending
        /// record (ADR-002 section 20's pending-workflow-equivalent pair,
        /// this domain's own named events per ADR-024 section 3.4). This
        /// task's own representation of ADR-002 section 20.1's "result is
        /// Pending": a successful <see cref="Result{T}"/> carrying the
        /// created, <see cref="AdvancementRecommendationStatus.Pending"/>
        /// record -- not <c>Odyssey.Application.Commands.CommandResult</c>'s
        /// own <c>Pending</c> status, since no existing Character command
        /// routes through that not-yet-wired-in command-dispatch layer
        /// (<c>SqliteSavingPipeline</c>'s own doc comment gives the same
        /// reasoning for why it does not either). A duplicate
        /// <paramref name="commandId"/> returns the same stored record and
        /// does not create a second reservation. Referenced
        /// <paramref name="evidenceIds"/> are recorded on the
        /// recommendation as candidates only -- they are not consumed
        /// (<c>UsedByAdvancementId</c> untouched) until
        /// <see cref="ResolveAdvancementRecommendation"/> actually approves
        /// with spend (ADR-024 section 7.1).
        /// </summary>
        Result<AdvancementRecommendationRecord> RequestSkillAdvancedRecommendation(CampaignHandle campaign, CharacterId characterId, SkillDefinitionId skillDefinitionId, long targetLevel, IReadOnlyList<CriticalSuccessEvidenceId> evidenceIds, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-106: ADR-024 section 6.1 step 2, MainGM-only (product
        /// section 14.3: "GM reviews... GM approves or dismisses"). Exactly
        /// one of: <paramref name="approve"/><c>=false</c> (Dismissed --
        /// releases the reservation, no skill change, evidence stays
        /// unconsumed); <paramref name="approve"/><c>=true</c> with
        /// <paramref name="spendReservedPoints"/><c>=true</c> (the reserved
        /// amount converts directly <c>Reserved</c>-&gt;<c>Spent</c> in a
        /// single transaction, never a release-then-reserve-then-spend
        /// sequence, ADR-024 section 6.1); or <paramref name="approve"/><c>=true</c>
        /// with <paramref name="spendReservedPoints"/><c>=false</c> (the
        /// reservation is released but the skill level still applies,
        /// referencing only the consumed evidence). This ADR deliberately
        /// does not decide numerically which branch a real
        /// <c>SkillAdvancementRule</c> would pick (ADR-024 section 6.1's own
        /// "not decided numerically by this ADR") -- <paramref name="spendReservedPoints"/>
        /// is this task's own explicit input standing in for that
        /// not-yet-implemented Rules Engine decision, so this method commits
        /// exactly the two ADR-named outcomes correctly rather than guessing
        /// which one a real rule would choose. Either approved branch
        /// atomically sets <c>UsedByAdvancementId</c> on every referenced
        /// evidence row -- rejected with a revision conflict, no partial
        /// state change, if any referenced evidence was already consumed by
        /// a concurrently-committed resolution (ADR-024 section 7.1).
        /// </summary>
        Result<CharacterRecord> ResolveAdvancementRecommendation(CampaignHandle campaign, CharacterId characterId, AdvancementRecommendationId recommendationId, bool approve, bool spendReservedPoints, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, long expectedRecommendationRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>ODY-S04-106: reads one <c>AdvancementRecommendation</c> by id.</summary>
        Result<AdvancementRecommendationRecord> GetAdvancementRecommendation(CampaignHandle campaign, CharacterId characterId, AdvancementRecommendationId recommendationId, CorrelationId correlationId);

        /// <summary>ODY-S04-107 (pkt 0): reads every <c>AdvancementPurchase</c> row for one Character, ordered by <see cref="AdvancementPurchase.CreatedAt"/> -- matching <see cref="GetDevelopmentLedger"/>'s own "Persistence stores everything" convention.</summary>
        Result<IReadOnlyList<AdvancementPurchase>> GetAdvancementPurchases(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-107: ADR-024 section 6.2 -- a compensating command
        /// (ADR-012 section 6) that undoes one <see cref="AdvancementPurchaseStatus.Applied"/>
        /// <c>AdvancementPurchase</c>. MainGM-only (<paramref name="actorIsMainGm"/>,
        /// the same caller-supplied-boolean convention every other GM-gated
        /// command already uses) -- reverting a spend is a GM correction
        /// action, not a player self-service one. <paramref name="reasonCode"/>
        /// is required (ADR-002 section 21.2's compensation metadata).
        /// Rejects with <c>CharacterAdvancementPurchaseHasDependent</c> when the
        /// addressed <c>AttributeValue</c>/<c>CharacterSkill</c> entry's
        /// current value no longer equals this purchase's own
        /// <see cref="AdvancementPurchase.ToValue"/> -- i.e. a later purchase
        /// has since raised it further. This is the explicitly minimal,
        /// Rules-Engine-free dependency check ADR-024 section 6.2 itself
        /// defers to future ruleset content ("the exact dependency graph is
        /// a Rules Engine/ruleset concern, not an architectural one") -- see
        /// this method's implementation doc comment for the full boundary.
        /// Reuses <c>MutateMechanics</c>: one compensating event
        /// (<c>IsCompensating=true</c>, <c>OriginalEventId</c> pointing at the
        /// original <c>AttributeIncreased</c>/<c>SkillLevelPurchased</c> event)
        /// plus a co-committed <c>DevelopmentTransaction</c> (<c>Kind=Refund</c>)
        /// in one transaction; sets <c>AdvancementPurchase.Status=Reverted</c>.
        /// </summary>
        Result<CharacterRecord> RevertAdvancementPurchase(CampaignHandle campaign, CharacterId characterId, AdvancementPurchaseId purchaseId, string reasonCode, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-107: ADR-024 section 7.2, product section 13.5 steps 1-3 --
        /// a read-only Query (ADR-002 section 4.2): no events, no state
        /// change (verified directly by tests: <c>MechanicsRevision</c>/pool
        /// balance identical before and after the call). Computes what would
        /// be returned (every currently-<c>Applied</c> purchase for each
        /// addressed target) and what would be newly purchased (one fresh
        /// purchase per target whose <see cref="CharacterRespecTarget.DesiredValue"/>
        /// exceeds zero), for client preview only -- <see cref="ApplyCharacterRespec"/>
        /// never trusts this result back (CAP-INV-004); it recomputes the
        /// identical plan itself, from scratch, inside its own transaction.
        /// </summary>
        Result<CharacterRespecPreview> PreviewCharacterRespec(CampaignHandle campaign, CharacterId characterId, IReadOnlyList<CharacterRespecTarget> targets, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-107: ADR-024 section 7.2, product section 13.5 steps 4-8 --
        /// one compensating+forward batch, MainGM-only, <paramref name="reasonCode"/>
        /// required. Recomputes the plan server-side from scratch inside its
        /// own transaction (CAP-INV-004: never trusts a client-supplied
        /// preview snapshot -- there is no such parameter on this method at
        /// all). For each undone purchase: a compensating event
        /// (<c>IsCompensating=true</c>) plus <c>DevelopmentTransaction.Kind=RespecReturn</c>,
        /// <c>AdvancementPurchase.Status=SupersededByRespec</c>. For each new
        /// purchase: an ordinary forward event plus
        /// <c>DevelopmentTransaction.Kind=RespecSpend</c> and a new
        /// <c>AdvancementPurchase</c> (<c>Status=Applied</c>). Every one of
        /// those batch events shares the same <c>CompensationGroupId</c>
        /// (this call's own <see cref="CommandId"/>) and is individually
        /// visible in <see cref="GetCharacterHistory"/> -- never collapsed
        /// into one opaque event (CAP-INV-005). Exactly one trailing
        /// <c>CharacterRespecCompleted</c> event closes the batch, carrying
        /// the full ordered list of produced event sequences and a
        /// before/after configuration snapshot (product section 13.5 step 5's
        /// "snapshot," realized as this event's own payload -- see the
        /// implementation doc comment for why no
        /// <c>SqliteBackupRepository</c> file-backup call is involved).
        /// <see cref="CommandId"/>/<c>AppliedCommands</c> remain the sole
        /// idempotency mechanism -- a duplicate <c>commandId</c> replays the
        /// stored result and does not re-apply the batch.
        /// </summary>
        Result<CharacterRecord> ApplyCharacterRespec(CampaignHandle campaign, CharacterId characterId, IReadOnlyList<CharacterRespecTarget> targets, string reasonCode, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-108: product section 16, ADR-024 section 5.1/9 -- one
        /// command for all six <see cref="SourceKind"/> values (product's
        /// own enum, reused verbatim).
        ///
        /// <see cref="SourceKind.ProgressionPurchase"/>: MainGM or an
        /// assigned user of this Character (the same
        /// <c>CharacterOwnershipAssignment.IsAssignedCharacter</c>
        /// convention <c>PurchaseAttributeIncrease</c>/<c>PurchaseSkillLevel</c>
        /// already use). Spends <c>DevelopmentPool</c> (cost from
        /// <see cref="Odyssey.Rules.Character.AbilityCostRules"/>, this
        /// task's own explicitly-flagged test fixture), creates an
        /// <c>AdvancementPurchase</c> (<c>OperationKind=AbilityAcquisition</c>,
        /// <c>FromValue=0</c>, <c>ToValue=1</c>) per ADR-024 section 5.1 step
        /// 4, and creates the <c>CharacterAbility</c> -- a genuine
        /// cross-section transaction touching both <c>Mechanics</c> and
        /// <c>CharacterAbilities</c> in one commit (ADR-022 section 5 rule
        /// 2: a command depending on several sections lists all required
        /// section revisions), so <paramref name="expectedMechanicsRevision"/>
        /// is REQUIRED for this <see cref="SourceKind"/> (validated by the
        /// implementation, not this signature alone).
        ///
        /// <see cref="SourceKind.GMGrant"/>: MainGM-only, touches only the
        /// <c>CharacterAbilities</c> section --
        /// <paramref name="expectedMechanicsRevision"/> is ignored (must be
        /// left <c>null</c>).
        ///
        /// <see cref="SourceKind.CharacterTemplate"/>/<see cref="SourceKind.Item"/>/
        /// <see cref="SourceKind.ActiveEffect"/>/<see cref="SourceKind.RulesetAdvancement"/>:
        /// structurally accepted (a future template-copy/Item/ActiveEffect
        /// system will call this command itself with these values) but no
        /// automatic acquisition through them is implemented by this task --
        /// see the SQLite implementation's own doc comment for the full
        /// permission decision recorded for these four values.
        ///
        /// Every path is gated by <paramref name="expectedCharacterAbilitiesRevision"/>
        /// (the section-wide <c>CharacterAbilitiesRevision</c> counter --
        /// ODY-S04-108 section 1.1's own fix: this is the first command to
        /// actually increment it, unlike <c>AttributeValuesRevision</c>/
        /// <c>CharacterSkillsRevision</c>, which ODY-S04-105/106 deliberately
        /// route through <c>MechanicsRevision</c> instead, per ADR-024
        /// section 4.2's own justification for pool ledger data -- a
        /// justification that does not extend to abilities).
        /// </summary>
        Result<CharacterRecord> AcquireAbility(CampaignHandle campaign, CharacterId characterId, AbilityDefinitionId abilityDefinitionId, SourceKind sourceKind, string? sourceRef, RankMode rankMode, long? numericRank, string? namedRankKey, string configuration, UserId actorUserId, bool actorIsMainGm, long? expectedMechanicsRevision, long expectedCharacterAbilitiesRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-108: product section 16 -- "способность предмета или
        /// эффекта исчезает при прекращении источника и не становится
        /// постоянной покупкой." Legal only for
        /// <see cref="SourceKind.Item"/>/<see cref="SourceKind.ActiveEffect"/>;
        /// rejected with <c>CharacterAbilityRemovalNotAllowed</c> for
        /// <see cref="SourceKind.ProgressionPurchase"/>/<see cref="SourceKind.GMGrant"/>/
        /// <see cref="SourceKind.CharacterTemplate"/>/<see cref="SourceKind.RulesetAdvancement"/>
        /// -- a permanent purchased/granted ability is never removed by this
        /// ordinary command (reverting an <c>AbilityAcquisition</c>
        /// <c>AdvancementPurchase</c> is explicitly out of scope, section
        /// 1.3). MainGM-only. Touches only the <c>CharacterAbilities</c>
        /// section, gated by <paramref name="expectedCharacterAbilitiesRevision"/>.
        /// </summary>
        Result<CharacterRecord> RemoveAbility(CampaignHandle campaign, CharacterId characterId, CharacterAbilityId characterAbilityId, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAbilitiesRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-109: product section 17. MainGM-only. Initializes a new
        /// <c>CharacterResource</c> from <see cref="Odyssey.Rules.Character.ResourceInitializationRules"/>'s
        /// own explicitly-flagged test fixture (no <c>ResourceDefinition</c>
        /// catalog exists yet). Touches only the <c>CharacterResources</c>
        /// section, gated by <paramref name="expectedCharacterResourcesRevision"/>
        /// (section-wide -- see <see cref="CharacterRecord.Resources"/>'s own
        /// doc comment for why no entry-level gate is checked).
        /// </summary>
        Result<CharacterRecord> InitializeCharacterResource(CampaignHandle campaign, CharacterId characterId, ResourceDefinitionId resourceDefinitionId, UserId actorUserId, bool actorIsMainGm, long expectedCharacterResourcesRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-109: product section 17.2/requirement 46 -- the ONE
        /// explicit, authoritative command that ever changes
        /// <see cref="CharacterResource.CurrentValue"/>, used identically
        /// for damage and for recovery (a positive or negative delta from
        /// the resource's own current value, per the caller-decided
        /// magnitude) -- there is no separate automatic recovery mechanism
        /// of any kind (no timer, no scene/session-change subscription),
        /// regardless of the resource's own <see cref="RecoveryRule"/>
        /// (requirement 47: <c>RecoveryRule.None</c> and every other value
        /// behave identically here -- only a future task wiring a real
        /// trigger to call this same command would differ). Rejected with
        /// <c>CharacterResourceValueOutOfRange</c> if the requested value
        /// falls outside <c>[MinimumValue, EffectiveMaximum]</c>. MainGM-only.
        /// </summary>
        Result<CharacterRecord> SetResourceCurrentValue(CampaignHandle campaign, CharacterId characterId, CharacterResourceId characterResourceId, long newCurrentValue, UserId actorUserId, bool actorIsMainGm, long expectedCharacterResourcesRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-109: product section 17.1/requirements 44-45. Changes
        /// <see cref="CharacterResource.BaseMaximum"/>/<see cref="CharacterResource.PermanentMaximumAdjustment"/>.
        /// If the new <c>EffectiveMaximum</c> is below the resource's
        /// current <c>CurrentValue</c>, <c>CurrentValue</c> is clamped down
        /// to the new <c>EffectiveMaximum</c> in the same commit
        /// (requirement 44) -- enforced structurally by
        /// <see cref="CharacterResource"/>'s own constructor, not a
        /// separate check this command could forget. A later increase of
        /// the maximum never restores the previously-lost value on its own
        /// (requirement 45) -- this command only ever sets the new maximum,
        /// it never touches <c>CurrentValue</c> upward. MainGM-only.
        /// </summary>
        Result<CharacterRecord> SetResourceMaximum(CampaignHandle campaign, CharacterId characterId, CharacterResourceId characterResourceId, long newBaseMaximum, long newPermanentMaximumAdjustment, UserId actorUserId, bool actorIsMainGm, long expectedCharacterResourcesRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-109 (section 1.2): product section 18. MainGM-only.
        /// Initializes the SINGLE <c>CharacterAnatomy</c> snapshot from
        /// <see cref="Odyssey.Rules.Character.AnatomyInitializationRules"/>'s
        /// own explicitly-flagged test fixture (no <c>AnatomyProfileDefinition</c>
        /// catalog exists yet), pinning <c>AnatomyProfileVersion</c> at this
        /// moment (requirement 49 -- never re-read from the fixture
        /// afterward). Rejected with <c>CharacterAnatomyAlreadyInitialized</c>
        /// if one already exists. Gated by the single, un-parameterized
        /// <c>CharacterAnatomy</c> lock key (<paramref name="expectedCharacterAnatomyRevision"/>).
        /// </summary>
        Result<CharacterRecord> InitializeCharacterAnatomy(CampaignHandle campaign, CharacterId characterId, AnatomyProfileDefinitionId anatomyProfileDefinitionId, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAnatomyRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>ODY-S04-109: product section 18 -- "добавить... часть тела." MainGM-only. Appends one <see cref="AnatomyMigrationEntry"/>. Rejected with <c>CharacterAnatomyNotInitialized</c> if no anatomy exists yet.</summary>
        Result<CharacterRecord> AddBodyPart(CampaignHandle campaign, CharacterId characterId, BodyPartId bodyPartId, string name, long damageLimit, BodyPartId? attachedToBodyPartId, string properties, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAnatomyRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-109 (section 1.3): product section 18/requirements 50-51
        /// -- "удалить... часть тела" with a dependency preview. No Item
        /// system exists anywhere in this codebase (confirmed by search) --
        /// product's own item-dependency check (requirement 51) is
        /// therefore a stub: "no item dependencies exist because no item
        /// system exists," pending a future Item-system task. What IS
        /// checked, for real, is the one dependency this Character's own
        /// <c>CharacterAnatomy</c> snapshot can actually express: any other
        /// <see cref="BodyPart.AttachedToBodyPartId"/> or
        /// <see cref="PermanentModification.AttachedToBodyPartId"/>
        /// referencing the part being removed. Rejected with
        /// <c>CharacterBodyPartHasDependent</c> if any such reference
        /// exists; no partial removal. MainGM-only.
        /// </summary>
        Result<CharacterRecord> RemoveBodyPart(CampaignHandle campaign, CharacterId characterId, BodyPartId bodyPartId, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAnatomyRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>ODY-S04-109: product section 18 -- "изменить пределы повреждений части тела" / "изменить свойства части," folded into one command (both target the same <see cref="BodyPart"/> row; two near-identical single-field setters would duplicate the same lookup/replace logic). Pass <c>null</c> for either parameter to leave that field unchanged. MainGM-only.</summary>
        Result<CharacterRecord> UpdateBodyPart(CampaignHandle campaign, CharacterId characterId, BodyPartId bodyPartId, long? newDamageLimit, string? newProperties, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAnatomyRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-109: product section 18 -- "заменить профиль." Replaces
        /// the entire <c>CharacterAnatomy</c> snapshot's
        /// <c>AnatomyProfileDefinitionId</c>/<c>AnatomyProfileVersion</c>/
        /// <c>BodyParts</c> in one commit, preserving <c>PermanentModifications</c>/
        /// <c>MigrationHistory</c>. Explicitly distinct from
        /// <c>ODY-S04-113</c>'s future Ruleset migration (a different,
        /// campaign-wide mechanism) -- this is a per-Character, GM-issued
        /// profile swap. MainGM-only.
        /// </summary>
        Result<CharacterRecord> ReplaceAnatomyProfile(CampaignHandle campaign, CharacterId characterId, AnatomyProfileDefinitionId newAnatomyProfileDefinitionId, string newAnatomyProfileVersion, IReadOnlyList<BodyPart> newBodyParts, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAnatomyRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>ODY-S04-109: product section 18 -- "применить протез, мутацию или постоянную модификацию," one generic command for all three (product itself groups them with no separate schema per kind -- see <see cref="PermanentModification"/>'s own doc comment). MainGM-only.</summary>
        Result<CharacterRecord> ApplyPermanentModification(CampaignHandle campaign, CharacterId characterId, BodyPartId attachedToBodyPartId, string kind, string description, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAnatomyRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-110: ADR-025 section 5.1 -- an ordinary `Lifecycle`-section
        /// transition (`LifecycleStatus → Archived`, product section 7.1's
        /// "Draft|Active|Inactive|Retired|Dead -> Archived") through the
        /// already-existing <see cref="CharacterLifecycleTransitions.IsValidTransition"/>
        /// table -- never a duplicated ad hoc legality check. Gated by
        /// <paramref name="expectedLifecycleRevision"/>.
        ///
        /// Actor: MainGM OR an assigned user of this Character
        /// (<see cref="CharacterOwnershipAssignment.IsAssignedCharacter"/>,
        /// the same convention <see cref="PurchaseAttributeIncrease"/>/
        /// <see cref="PurchaseSkillLevel"/> already use) -- NOT MainGM-only.
        /// ADR-025 section 5.1's own text: "`Character.Archive` is checked
        /// normally under the existing permission model; this ADR does not
        /// restrict it beyond what the permission itself already implies,"
        /// and product section 26's own MVP MainGM-exclusive permission list
        /// names only `GrantDevelopment`/`Respec`/`ManageOwnership`/
        /// `RestoreDead` -- `Character.Archive` is conspicuously absent from
        /// that list, unlike <see cref="DeleteCharacterPermanently"/>, which
        /// product section 22.2 states is MainGM-only in so many words. This
        /// is a deliberate choice, not a copy of the stricter sibling
        /// command's own gate.
        /// </summary>
        Result<CharacterRecord> ArchiveCharacter(CampaignHandle campaign, CharacterId characterId, UserId actorUserId, bool actorIsMainGm, long expectedLifecycleRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S04-110: ADR-025 section 5.2 -- MainGM-only. Before
        /// committing: (a) re-checks dependencies through the extensible,
        /// currently-empty-by-default <see cref="ICharacterDeletionDependencyChecker"/>
        /// mechanism (section 1.1 of this task's own ТЗ) -- a blocking
        /// dependency rejects with <c>CharacterDeletionHasDependent</c>, no
        /// state change; (b) creates a full campaign backup via the
        /// already-existing <c>IBackupRepository.CreateBackup</c>
        /// (ODY-S01-011, section 1.2 -- never a new, Character-specific
        /// backup mechanism), with reason <c>"pre-delete-character:&lt;CharacterId&gt;"</c>.
        /// On success, in one transaction: removes the Character's live
        /// current-state row (and any live cross-reference this codebase
        /// actually stores -- none exist today, confirmed by search) and
        /// commits a <c>CharacterDeleted</c> event (product section 28)
        /// carrying ADR-022 section 7's minimum historical snapshot
        /// (<c>DisplayNameSnapshot</c>, <c>PortraitReferenceSnapshot?</c>,
        /// <c>RelevantValueSnapshots</c>, <c>RulesetVersion</c>). Never
        /// deletes any `DomainEvents` row for this `CharacterId` -- ADR-012
        /// section 4.2's append-only guarantee has no "Character deleted"
        /// exception; <see cref="GetCharacterHistory"/> continues to render
        /// this Character's past from those events after this call
        /// (ADR-022 section 7 rule 3/section 8, ADR-025 section 5.3).
        /// <paramref name="reasonCode"/> is required (product section 22.2's
        /// "отдельного подтверждения," realized as this codebase's own
        /// established `ReasonCode` convention for GM-correction/irreversible
        /// operations). Returns a non-generic <see cref="Result"/> -- there
        /// is no live <see cref="CharacterRecord"/> left to return.
        /// </summary>
        Result DeleteCharacterPermanently(CampaignHandle campaign, CharacterId characterId, string reasonCode, UserId actorUserId, bool actorIsMainGm, long expectedLifecycleRevision, CommandId commandId, CorrelationId correlationId);
    }

    /// <summary>
    /// ODY-S04-110 section 1.1: ADR-025 section 5.2's host-authoritative
    /// dependency re-check for <see cref="ICharacterRepository.DeleteCharacterPermanently"/> --
    /// board-token references, inventory/item references, GameLog
    /// references, and any other live cross-reference. Direct search
    /// confirms none of Board/Scene, GameLog, or any other existing
    /// persistence implementation in this codebase stores a
    /// <see cref="CharacterId"/> anywhere -- there is nothing to check
    /// today for real, for all three named sources at once (unlike
    /// ODY-S04-108/109's own item-dependency stub, where only one of
    /// several checked sources was unimplementable). This interface exists
    /// so `DeleteCharacterPermanently` never hard-codes "no dependencies" as
    /// a literal, un-extensible constant: a future task that gives Board/
    /// Item/GameLog a real `CharacterId` cross-reference implements this
    /// interface and is registered into <c>SqliteCharacterRepository</c>'s
    /// own checker list, without changing `DeleteCharacterPermanently`'s own
    /// shape or call site at all.
    /// </summary>
    public interface ICharacterDeletionDependencyChecker
    {
        /// <summary>Returns a short, human-readable description of the blocking dependency if one exists, or <c>null</c> if this checker finds none.</summary>
        string? CheckBlockingDependency(CampaignHandle campaign, CharacterId characterId);
    }

    /// <summary>ODY-S04-107: one addressed attribute-or-skill target for a respec, and the value the caller wants it to end up at after the batch (0 means "fully undo, do not repurchase").</summary>
    public sealed class CharacterRespecTarget
    {
        public CharacterRespecTarget(AdvancementOperationKind operationKind, string targetDefinitionId, long desiredValue)
        {
            if (!Enum.IsDefined(typeof(AdvancementOperationKind), operationKind)) throw new ArgumentOutOfRangeException(nameof(operationKind));
            if (string.IsNullOrWhiteSpace(targetDefinitionId)) throw new ArgumentException("TargetDefinitionId is required.", nameof(targetDefinitionId));
            if (desiredValue < 0) throw new ArgumentOutOfRangeException(nameof(desiredValue));

            OperationKind = operationKind;
            TargetDefinitionId = targetDefinitionId;
            DesiredValue = desiredValue;
        }

        public AdvancementOperationKind OperationKind { get; }
        public string TargetDefinitionId { get; }
        public long DesiredValue { get; }
    }

    /// <summary>ODY-S04-107: which side of a respec plan entry this is.</summary>
    public enum CharacterRespecPlanAction
    {
        Return = 1,
        Spend = 2
    }

    /// <summary>ODY-S04-107: one line of a computed respec plan -- either returning a specific existing purchase's cost, or spending on a fresh purchase up to a target's desired value.</summary>
    public sealed class CharacterRespecPlanEntry
    {
        public CharacterRespecPlanEntry(CharacterRespecPlanAction action, AdvancementOperationKind operationKind, string targetDefinitionId, long amount, AdvancementPurchaseId? sourcePurchaseId)
        {
            if (!Enum.IsDefined(typeof(CharacterRespecPlanAction), action)) throw new ArgumentOutOfRangeException(nameof(action));
            if (!Enum.IsDefined(typeof(AdvancementOperationKind), operationKind)) throw new ArgumentOutOfRangeException(nameof(operationKind));
            if (string.IsNullOrWhiteSpace(targetDefinitionId)) throw new ArgumentException("TargetDefinitionId is required.", nameof(targetDefinitionId));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));

            Action = action;
            OperationKind = operationKind;
            TargetDefinitionId = targetDefinitionId;
            Amount = amount;
            SourcePurchaseId = sourcePurchaseId;
        }

        public CharacterRespecPlanAction Action { get; }
        public AdvancementOperationKind OperationKind { get; }
        public string TargetDefinitionId { get; }
        public long Amount { get; }

        /// <summary>Set only for <see cref="CharacterRespecPlanAction.Return"/> entries -- the specific <c>AdvancementPurchase</c> being undone.</summary>
        public AdvancementPurchaseId? SourcePurchaseId { get; }
    }

    /// <summary>ODY-S04-107: <see cref="ICharacterRepository.PreviewCharacterRespec"/>'s computed result -- the same shape <see cref="ICharacterRepository.ApplyCharacterRespec"/> recomputes for itself server-side.</summary>
    public sealed class CharacterRespecPreview
    {
        public CharacterRespecPreview(IReadOnlyList<CharacterRespecPlanEntry> entries, long totalReturned, long totalSpent)
        {
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));
            if (totalReturned < 0) throw new ArgumentOutOfRangeException(nameof(totalReturned));
            if (totalSpent < 0) throw new ArgumentOutOfRangeException(nameof(totalSpent));

            TotalReturned = totalReturned;
            TotalSpent = totalSpent;
        }

        public IReadOnlyList<CharacterRespecPlanEntry> Entries { get; }
        public long TotalReturned { get; }
        public long TotalSpent { get; }
        public long NetAvailableChange => TotalReturned - TotalSpent;
    }

    /// <summary>ODY-S04-106: ADR-024 section 3.5/7.1's <c>CriticalSuccessEvidence</c> read-model row.</summary>
    public sealed class CriticalSuccessEvidenceRecord
    {
        public CriticalSuccessEvidenceRecord(CriticalSuccessEvidenceId evidenceId, CharacterId characterId, SkillDefinitionId skillDefinitionId, string? sourceDiceRollId, string? sourceActionId, UtcInstant occurredAt, string rulesetVersion, AdvancementRecommendationId? usedByAdvancementId, long revision)
        {
            if (!evidenceId.IsValid) throw new ArgumentException("EvidenceId is required.", nameof(evidenceId));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (!skillDefinitionId.IsValid) throw new ArgumentException("SkillDefinitionId is required.", nameof(skillDefinitionId));
            if (string.IsNullOrWhiteSpace(rulesetVersion)) throw new ArgumentException("RulesetVersion is required.", nameof(rulesetVersion));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));

            EvidenceId = evidenceId;
            CharacterId = characterId;
            SkillDefinitionId = skillDefinitionId;
            SourceDiceRollId = sourceDiceRollId;
            SourceActionId = sourceActionId;
            OccurredAt = occurredAt;
            RulesetVersion = rulesetVersion;
            UsedByAdvancementId = usedByAdvancementId;
            Revision = revision;
        }

        public CriticalSuccessEvidenceId EvidenceId { get; }
        public CharacterId CharacterId { get; }
        public SkillDefinitionId SkillDefinitionId { get; }
        public string? SourceDiceRollId { get; }
        public string? SourceActionId { get; }
        public UtcInstant OccurredAt { get; }
        public string RulesetVersion { get; }

        /// <summary>ADR-024 section 7.1: set exactly once, guarded by <see cref="Revision"/> -- never a separate spent-evidence registry.</summary>
        public AdvancementRecommendationId? UsedByAdvancementId { get; }
        public long Revision { get; }
    }

    /// <summary>ODY-S04-106: ADR-024 section 3.4's <c>AdvancementRecommendation</c> durable pending record.</summary>
    public sealed class AdvancementRecommendationRecord
    {
        public AdvancementRecommendationRecord(AdvancementRecommendationId recommendationId, CharacterId characterId, SkillDefinitionId skillDefinitionId, long targetLevel, long reservedAmount, IReadOnlyList<CriticalSuccessEvidenceId> evidenceIds, AdvancementRecommendationStatus status, long revision, UtcInstant createdAt)
        {
            if (!recommendationId.IsValid) throw new ArgumentException("RecommendationId is required.", nameof(recommendationId));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (!skillDefinitionId.IsValid) throw new ArgumentException("SkillDefinitionId is required.", nameof(skillDefinitionId));
            if (targetLevel < 1) throw new ArgumentOutOfRangeException(nameof(targetLevel));
            if (reservedAmount < 0) throw new ArgumentOutOfRangeException(nameof(reservedAmount));
            if (!Enum.IsDefined(typeof(AdvancementRecommendationStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));

            RecommendationId = recommendationId;
            CharacterId = characterId;
            SkillDefinitionId = skillDefinitionId;
            TargetLevel = targetLevel;
            ReservedAmount = reservedAmount;
            EvidenceIds = evidenceIds ?? throw new ArgumentNullException(nameof(evidenceIds));
            Status = status;
            Revision = revision;
            CreatedAt = createdAt;
        }

        public AdvancementRecommendationId RecommendationId { get; }
        public CharacterId CharacterId { get; }
        public SkillDefinitionId SkillDefinitionId { get; }
        public long TargetLevel { get; }
        public long ReservedAmount { get; }
        public IReadOnlyList<CriticalSuccessEvidenceId> EvidenceIds { get; }
        public AdvancementRecommendationStatus Status { get; }
        public long Revision { get; }
        public UtcInstant CreatedAt { get; }
    }

    /// <summary>ODY-S04-105: ADR-024 section 3.2's <c>DevelopmentTransaction</c> read-model row -- see <see cref="Character.DevelopmentTransaction"/>'s own doc comment for why it carries no independent authority.</summary>
    public sealed class DevelopmentTransactionRecord
    {
        public DevelopmentTransactionRecord(DevelopmentTransactionId transactionId, CharacterId characterId, DevelopmentTransactionKind kind, long amount, string? sourceRef, string reason, UserId actorUserId, string rulesetVersion, UtcInstant createdAt, CorrelationId correlationId)
        {
            if (!transactionId.IsValid) throw new ArgumentException("TransactionId is required.", nameof(transactionId));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason is required.", nameof(reason));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (string.IsNullOrWhiteSpace(rulesetVersion)) throw new ArgumentException("RulesetVersion is required.", nameof(rulesetVersion));

            TransactionId = transactionId;
            CharacterId = characterId;
            Kind = kind;
            Amount = amount;
            SourceRef = sourceRef;
            Reason = reason;
            ActorUserId = actorUserId;
            RulesetVersion = rulesetVersion;
            CreatedAt = createdAt;
            CorrelationId = correlationId;
        }

        public DevelopmentTransactionId TransactionId { get; }
        public CharacterId CharacterId { get; }
        public DevelopmentTransactionKind Kind { get; }
        public long Amount { get; }
        public string? SourceRef { get; }
        public string Reason { get; }
        public UserId ActorUserId { get; }
        public string RulesetVersion { get; }
        public UtcInstant CreatedAt { get; }
        public CorrelationId CorrelationId { get; }
    }

    /// <summary>
    /// ODY-S04-104: product section 8.4's <c>CharacterReviewComment</c>,
    /// narrowed to what this task actually needs (no <c>ResolvedAt</c>
    /// mutation command exists yet -- the field is carried for forward
    /// compatibility with a future resolve-comment task, always <c>null</c>
    /// from this task's own write path).
    /// </summary>
    public sealed class CharacterReviewCommentRecord
    {
        public CharacterReviewCommentRecord(CharacterReviewCommentId commentId, CharacterId characterId, UserId authorUserId, string text, UtcInstant createdAt, UtcInstant? resolvedAt)
        {
            if (!commentId.IsValid) throw new ArgumentException("CommentId is required.", nameof(commentId));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (!authorUserId.IsValid) throw new ArgumentException("AuthorUserId is required.", nameof(authorUserId));
            if (string.IsNullOrWhiteSpace(text) || text.Length > 2000) throw new ArgumentException("Text is not safe.", nameof(text));

            CommentId = commentId;
            CharacterId = characterId;
            AuthorUserId = authorUserId;
            Text = text;
            CreatedAt = createdAt;
            ResolvedAt = resolvedAt;
        }

        public CharacterReviewCommentId CommentId { get; }
        public CharacterId CharacterId { get; }
        public UserId AuthorUserId { get; }
        public string Text { get; }
        public UtcInstant CreatedAt { get; }
        public UtcInstant? ResolvedAt { get; }
    }

    /// <summary>
    /// ODY-S04-103: ADR-023 section 4.2/6's inputs to
    /// <see cref="ICharacterRepository.BindDraftToCampaign"/>. Product section
    /// 8.2's minimum required fields are enforced here:
    /// <see cref="DisplayName"/>/<see cref="CharacterKind"/>/
    /// <see cref="AnatomyProfileRef"/> are always required, and
    /// <see cref="InitialPrimaryOwnerUserId"/> is required exactly when
    /// <see cref="CharacterKind"/> is <see cref="Character.CharacterKind.PlayerCharacter"/>
    /// (backlog section 2.2 -- an ordinary Draft field, not
    /// <c>AssignPrimaryOwner</c>). <see cref="TemplateRulesetId"/>/
    /// <see cref="TemplateRulesetVersion"/> are required exactly when
    /// <paramref name="seed"/> carries a <see cref="CharacterCreationSeed.TemplateId"/>
    /// -- they are this request's own compatibility-validation inputs
    /// (ADR-023 section 6.1), evaluated inside <c>BindDraftToCampaign</c>
    /// itself, not by the caller.
    /// </summary>
    public sealed class BindDraftToCampaignRequest
    {
        public BindDraftToCampaignRequest(
            CampaignHandle campaign,
            CharacterKind characterKind,
            string displayName,
            string anatomyProfileRef,
            UserId? initialPrimaryOwnerUserId,
            CharacterCreationSeed seed,
            string? templateRulesetId,
            string? templateRulesetVersion)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!Enum.IsDefined(typeof(CharacterKind), characterKind)) throw new ArgumentOutOfRangeException(nameof(characterKind));
            if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 128) throw new ArgumentException("DisplayName is not safe.", nameof(displayName));
            if (string.IsNullOrWhiteSpace(anatomyProfileRef)) throw new ArgumentException("AnatomyProfileRef is required.", nameof(anatomyProfileRef));
            if (characterKind == CharacterKind.PlayerCharacter && (!initialPrimaryOwnerUserId.HasValue || !initialPrimaryOwnerUserId.Value.IsValid))
            {
                throw new ArgumentException("InitialPrimaryOwnerUserId is required for a PlayerCharacter.", nameof(initialPrimaryOwnerUserId));
            }

            Seed = seed ?? throw new ArgumentNullException(nameof(seed));
            if (Seed.TemplateId.HasValue && (string.IsNullOrWhiteSpace(templateRulesetId) || string.IsNullOrWhiteSpace(templateRulesetVersion)))
            {
                throw new ArgumentException("TemplateRulesetId/TemplateRulesetVersion are required when the seed references a template.");
            }

            Campaign = campaign;
            CharacterKind = characterKind;
            DisplayName = displayName;
            AnatomyProfileRef = anatomyProfileRef;
            InitialPrimaryOwnerUserId = initialPrimaryOwnerUserId;
            TemplateRulesetId = templateRulesetId;
            TemplateRulesetVersion = templateRulesetVersion;
        }

        public CampaignHandle Campaign { get; }
        public CharacterKind CharacterKind { get; }
        public string DisplayName { get; }
        public string AnatomyProfileRef { get; }
        public UserId? InitialPrimaryOwnerUserId { get; }
        public CharacterCreationSeed Seed { get; }
        public string? TemplateRulesetId { get; }
        public string? TemplateRulesetVersion { get; }
    }

    public sealed class CreateCharacterRequest
    {
        public CreateCharacterRequest(CampaignHandle campaign, CharacterKind characterKind, string displayName)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            if (!Enum.IsDefined(typeof(CharacterKind), characterKind)) throw new ArgumentOutOfRangeException(nameof(characterKind));
            if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 128) throw new ArgumentException("DisplayName is not safe.", nameof(displayName));

            CharacterKind = characterKind;
            DisplayName = displayName;
        }

        public CampaignHandle Campaign { get; }
        public CharacterKind CharacterKind { get; }
        public string DisplayName { get; }
    }

    /// <summary>
    /// ODY-S04-101: the Character aggregate's current-state projection row
    /// (ADR-022 section 4's aggregate shape, narrowed to this task's own
    /// scope). <see cref="CharacterRevision"/> and every section revision
    /// ADR-022 section 5 reserves are present from creation onward, even for
    /// sections this task's own commands never touch -- see
    /// <see cref="CharacterSectionRevisions"/>'s own doc comment.
    /// </summary>
    public sealed class CharacterRecord
    {
        public CharacterRecord(
            CharacterId characterId,
            CampaignId campaignId,
            CharacterKind characterKind,
            CharacterLifecycleStatus lifecycleStatus,
            CharacterApprovalState approvalState,
            string displayName,
            string? portraitReference,
            CharacterOwnership ownership,
            CharacterSectionRevisions revisions,
            string rulesetVersion,
            string? anatomyProfileRef,
            CharacterTemplateId? templateId,
            long? templateVersionAtCopyTime,
            IReadOnlyList<CopiedCharacterSeedItem> seedCopy,
            UtcInstant? submittedAt,
            DevelopmentPool developmentPool,
            IReadOnlyList<AttributeValue> attributes,
            IReadOnlyList<CharacterSkill> skills,
            IReadOnlyList<CharacterAbility> abilities,
            IReadOnlyList<CharacterResource> resources,
            CharacterAnatomy? anatomy,
            UtcInstant createdAt,
            UtcInstant updatedAt)
        {
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!Enum.IsDefined(typeof(CharacterKind), characterKind)) throw new ArgumentOutOfRangeException(nameof(characterKind));
            if (!Enum.IsDefined(typeof(CharacterLifecycleStatus), lifecycleStatus)) throw new ArgumentOutOfRangeException(nameof(lifecycleStatus));
            if (!Enum.IsDefined(typeof(CharacterApprovalState), approvalState)) throw new ArgumentOutOfRangeException(nameof(approvalState));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("DisplayName is required.", nameof(displayName));
            if (rulesetVersion == null) throw new ArgumentNullException(nameof(rulesetVersion));

            CharacterId = characterId;
            CampaignId = campaignId;
            CharacterKind = characterKind;
            LifecycleStatus = lifecycleStatus;
            ApprovalState = approvalState;
            DisplayName = displayName;
            PortraitReference = portraitReference;
            Ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
            Revisions = revisions;
            RulesetVersion = rulesetVersion;
            AnatomyProfileRef = anatomyProfileRef;
            TemplateId = templateId;
            TemplateVersionAtCopyTime = templateVersionAtCopyTime;
            SeedCopy = seedCopy ?? throw new ArgumentNullException(nameof(seedCopy));
            SubmittedAt = submittedAt;
            DevelopmentPool = developmentPool ?? throw new ArgumentNullException(nameof(developmentPool));
            Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            Skills = skills ?? throw new ArgumentNullException(nameof(skills));
            Abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
            Resources = resources ?? throw new ArgumentNullException(nameof(resources));
            Anatomy = anatomy;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        public CharacterId CharacterId { get; }
        public CampaignId CampaignId { get; }
        public CharacterKind CharacterKind { get; }
        public CharacterLifecycleStatus LifecycleStatus { get; }
        public CharacterApprovalState ApprovalState { get; }
        public string DisplayName { get; }
        public string? PortraitReference { get; }

        /// <summary>ODY-S04-102: ADR-022's already-reserved <c>Ownership</c> section content -- see <see cref="CharacterSectionRevisions.OwnershipRevision"/> for its revision counter.</summary>
        public CharacterOwnership Ownership { get; }
        public CharacterSectionRevisions Revisions { get; }

        /// <summary>
        /// ODY-S04-103: ADR-022 section 4's <c>CreationInfo</c> conceptual
        /// area -- immutable metadata set once at creation (<see cref="CreateCharacter"/>
        /// or <see cref="ICharacterRepository.BindDraftToCampaign"/>) and
        /// never independently revised (ADR-022 section 5 reserves no
        /// <c>CreationInfoRevision</c> counter). Empty string for a Character
        /// created via <see cref="CreateCharacter"/> (ODY-S04-101's own bare
        /// skeleton path, which pins no ruleset) -- always a real pinned
        /// value (ADR-023 section 6.2) for one created via
        /// <see cref="ICharacterRepository.BindDraftToCampaign"/>.
        /// </summary>
        public string RulesetVersion { get; }
        public string? AnatomyProfileRef { get; }

        /// <summary>ADR-023 section 5.3: immutable provenance only, never a live reference back to the source template.</summary>
        public CharacterTemplateId? TemplateId { get; }
        public long? TemplateVersionAtCopyTime { get; }

        /// <summary>ADR-023 section 5.3's deep-copy-with-fresh-identifiers result, captured once at creation time. Empty for a blank Character/Draft.</summary>
        public IReadOnlyList<CopiedCharacterSeedItem> SeedCopy { get; }

        /// <summary>ODY-S04-104: set by <see cref="ICharacterRepository.SubmitCharacterDraft"/>, gated by <see cref="CharacterSectionRevisions.LifecycleRevision"/>. Null until the Draft has been submitted for review at least once.</summary>
        public UtcInstant? SubmittedAt { get; }

        /// <summary>ODY-S04-105: ADR-024 section 4's <c>Mechanics</c>-section ledger accounting -- see <see cref="CharacterSectionRevisions.MechanicsRevision"/> for its gating revision.</summary>
        public DevelopmentPool DevelopmentPool { get; }

        /// <summary>ODY-S04-105: product section 11's <c>AttributeValue</c> rows purchased so far -- empty until the first <c>PurchaseAttributeIncrease</c>. Each entry's own <see cref="AttributeValue.Revision"/> is the ADR-024 section 4.2 entry-level gate for further purchases against that same attribute.</summary>
        public IReadOnlyList<AttributeValue> Attributes { get; }

        /// <summary>ODY-S04-106: product section 14's <c>CharacterSkill</c> rows purchased so far -- empty until the first purchase/resolution ("отсутствующий навык представлен отсутствием CharacterSkill"). Each entry's own <see cref="CharacterSkill.Revision"/> is the ADR-024 section 4.2 entry-level gate for further purchases against that same skill.</summary>
        public IReadOnlyList<CharacterSkill> Skills { get; }

        /// <summary>ODY-S04-108: product section 16's <c>CharacterAbility</c> rows acquired so far -- empty until the first <c>AcquireAbility</c>. Each entry's own <see cref="CharacterAbility.Revision"/> is the ADR-022 section 6 entry-level gate for the <c>CharacterAbility:&lt;CharacterAbilityId&gt;</c> lock key; the section-wide <see cref="CharacterSectionRevisions.CharacterAbilitiesRevision"/> is the gate for <c>AcquireAbility</c>/<c>RemoveAbility</c> themselves.</summary>
        public IReadOnlyList<CharacterAbility> Abilities { get; }

        /// <summary>ODY-S04-109: product section 17's <c>CharacterResource</c> rows initialized so far -- empty until the first <c>InitializeCharacterResource</c>. Each entry's own <see cref="CharacterResource.Revision"/> is carried but not externally gated by callers (this task's own decision -- see the ExecPlan); the section-wide <see cref="CharacterSectionRevisions.CharacterResourcesRevision"/> is the sole gate, mirroring <see cref="Abilities"/>'s own single-section-gate shape (ODY-S04-108).</summary>
        public IReadOnlyList<CharacterResource> Resources { get; }

        /// <summary>ODY-S04-109: product section 18's <c>CharacterAnatomy</c> -- a SINGLE snapshot, null until <c>InitializeCharacterAnatomy</c>. Unlike <see cref="Resources"/>/<see cref="Abilities"/>/<see cref="Skills"/>, this is not a collection -- the whole snapshot changes together under <see cref="CharacterSectionRevisions.CharacterAnatomyRevision"/> alone (ADR-022 section 6's un-parameterized <c>CharacterAnatomy</c> lock key).</summary>
        public CharacterAnatomy? Anatomy { get; }
        public UtcInstant CreatedAt { get; }
        public UtcInstant UpdatedAt { get; }
    }

    /// <summary>
    /// ODY-S04-101: one rebuilt <see cref="ICharacterRepository.GetCharacterHistory"/>
    /// entry -- ADR-022 section 7's minimum historical snapshot, narrowed to
    /// what this task's own two event kinds (<c>character_created</c>,
    /// <c>character_identity_updated</c>) actually carry. Later tasks add
    /// their own event kinds to the same rebuild without changing this shape's
    /// existing fields.
    /// </summary>
    public sealed class CharacterHistoryEntry
    {
        public CharacterHistoryEntry(long eventSequence, string eventType, CharacterId characterId, string displayNameSnapshot, UtcInstant occurredAt)
        {
            if (eventSequence < 1) throw new ArgumentOutOfRangeException(nameof(eventSequence));
            if (string.IsNullOrWhiteSpace(eventType)) throw new ArgumentException("EventType is required.", nameof(eventType));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (string.IsNullOrWhiteSpace(displayNameSnapshot)) throw new ArgumentException("DisplayNameSnapshot is required.", nameof(displayNameSnapshot));

            EventSequence = eventSequence;
            EventType = eventType;
            CharacterId = characterId;
            DisplayNameSnapshot = displayNameSnapshot;
            OccurredAt = occurredAt;
        }

        /// <summary>ADR-012 section 4.1's EventSequence -- the sole authoritative order; never re-sorted by <see cref="OccurredAt"/>.</summary>
        public long EventSequence { get; }
        public string EventType { get; }
        public CharacterId CharacterId { get; }
        public string DisplayNameSnapshot { get; }
        public UtcInstant OccurredAt { get; }
    }
}
