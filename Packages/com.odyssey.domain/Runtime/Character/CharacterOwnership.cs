using System;
using System.Collections.Generic;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Domain.Character
{
    /// <summary>
    /// ODY-S04-102: 10_Characters_And_Progression section 19's
    /// <c>CharacterOwnership</c> shape, living inside ADR-022's already-
    /// reserved <c>Ownership</c> section (ADR-025 section 4.1 -- no new
    /// section/lock/revision). This is pure Domain data; the section's own
    /// revision (<see cref="CharacterSectionRevisions.OwnershipRevision"/>)
    /// and persistence remain Application/Persistence concerns.
    /// </summary>
    public sealed class CharacterOwnership
    {
        public CharacterOwnership(
            UserId? primaryOwnerUserId,
            IReadOnlyList<UserId> coOwnerUserIds,
            IReadOnlyList<UserId> permanentControllerUserIds,
            IReadOnlyList<CharacterTemporaryControlGrant> temporaryControlGrants)
        {
            if (primaryOwnerUserId.HasValue && !primaryOwnerUserId.Value.IsValid) throw new ArgumentException("PrimaryOwnerUserId must be valid when present.", nameof(primaryOwnerUserId));

            PrimaryOwnerUserId = primaryOwnerUserId;
            CoOwnerUserIds = coOwnerUserIds ?? throw new ArgumentNullException(nameof(coOwnerUserIds));
            PermanentControllerUserIds = permanentControllerUserIds ?? throw new ArgumentNullException(nameof(permanentControllerUserIds));
            TemporaryControlGrants = temporaryControlGrants ?? throw new ArgumentNullException(nameof(temporaryControlGrants));
        }

        public static CharacterOwnership Empty() => new CharacterOwnership(null, Array.Empty<UserId>(), Array.Empty<UserId>(), Array.Empty<CharacterTemporaryControlGrant>());

        public UserId? PrimaryOwnerUserId { get; }
        public IReadOnlyList<UserId> CoOwnerUserIds { get; }
        public IReadOnlyList<UserId> PermanentControllerUserIds { get; }
        public IReadOnlyList<CharacterTemporaryControlGrant> TemporaryControlGrants { get; }
    }

    /// <summary>
    /// ODY-S04-102: product section 19's <c>TemporaryControlGrants</c> entry
    /// shape. Neither product section 19 nor ADR-025 section 4 describes an
    /// automatic expiration *enforcement* mechanism (no background sweep, no
    /// scheduled revocation event) -- this task's own engineering decision
    /// (not an ADR decision) is the minimum sufficient to make "temporary"
    /// meaningfully different from "permanent" without inventing one: an
    /// optional <see cref="ExpiresAt"/> is stored, and
    /// <see cref="CharacterOwnershipAssignment.IsAssignedCharacter"/>
    /// evaluates it lazily, at read time, against the caller-supplied current
    /// host time -- never by a stored "IsActive" flag that could drift stale.
    /// A grant with no <see cref="ExpiresAt"/> remains active until an
    /// explicit <c>RevokeCharacterControl</c> command removes it.
    /// </summary>
    public sealed class CharacterTemporaryControlGrant
    {
        public CharacterTemporaryControlGrant(UserId userId, UtcInstant grantedAt, UtcInstant? expiresAt)
        {
            if (!userId.IsValid) throw new ArgumentException("UserId is required.", nameof(userId));
            if (expiresAt.HasValue && expiresAt.Value.CompareTo(grantedAt) < 0) throw new ArgumentException("ExpiresAt cannot precede GrantedAt.", nameof(expiresAt));

            UserId = userId;
            GrantedAt = grantedAt;
            ExpiresAt = expiresAt;
        }

        public UserId UserId { get; }
        public UtcInstant GrantedAt { get; }
        public UtcInstant? ExpiresAt { get; }

        public bool IsActiveAt(UtcInstant now) => !ExpiresAt.HasValue || now.CompareTo(ExpiresAt.Value) < 0;
    }

    /// <summary>
    /// ODY-S04-102: ADR-025 section 4.3's "assigned character" specialization
    /// of ADR-019's baseline concept -- both ownership (primary/co-owner) and
    /// an active control grant (permanent, or temporary and not yet expired)
    /// satisfy it. This is the one canonical predicate every future
    /// Player-action-eligibility check against a Character should call,
    /// rather than re-deriving the same ownership/control logic ad hoc.
    /// </summary>
    public static class CharacterOwnershipAssignment
    {
        public static bool IsAssignedCharacter(CharacterOwnership ownership, UserId actorUserId, UtcInstant now)
        {
            if (ownership == null) throw new ArgumentNullException(nameof(ownership));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));

            if (ownership.PrimaryOwnerUserId.HasValue && ownership.PrimaryOwnerUserId.Value.Equals(actorUserId))
            {
                return true;
            }

            foreach (UserId coOwner in ownership.CoOwnerUserIds)
            {
                if (coOwner.Equals(actorUserId)) return true;
            }

            foreach (UserId controller in ownership.PermanentControllerUserIds)
            {
                if (controller.Equals(actorUserId)) return true;
            }

            foreach (CharacterTemporaryControlGrant grant in ownership.TemporaryControlGrants)
            {
                if (grant.UserId.Equals(actorUserId) && grant.IsActiveAt(now)) return true;
            }

            return false;
        }
    }
}
