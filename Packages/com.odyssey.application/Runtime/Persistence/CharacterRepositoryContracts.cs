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
