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
            UtcInstant createdAt,
            UtcInstant updatedAt)
        {
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!Enum.IsDefined(typeof(CharacterKind), characterKind)) throw new ArgumentOutOfRangeException(nameof(characterKind));
            if (!Enum.IsDefined(typeof(CharacterLifecycleStatus), lifecycleStatus)) throw new ArgumentOutOfRangeException(nameof(lifecycleStatus));
            if (!Enum.IsDefined(typeof(CharacterApprovalState), approvalState)) throw new ArgumentOutOfRangeException(nameof(approvalState));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("DisplayName is required.", nameof(displayName));

            CharacterId = characterId;
            CampaignId = campaignId;
            CharacterKind = characterKind;
            LifecycleStatus = lifecycleStatus;
            ApprovalState = approvalState;
            DisplayName = displayName;
            PortraitReference = portraitReference;
            Ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
            Revisions = revisions;
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
