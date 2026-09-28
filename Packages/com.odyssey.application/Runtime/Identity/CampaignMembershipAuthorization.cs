using System;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Identity
{
    /// <summary>
    /// ODY-S10-101: the one canonical place where "is this user the MainGM of this campaign" is decided, by
    /// the same precedent as <c>CharacterOwnershipAssignment.IsAssignedCharacter</c> for "assigned character":
    /// every subsystem that used to trust a client-supplied MainGM flag must call this instead of
    /// re-implementing the comparison. It reads the durable membership
    /// (<see cref="ICampaignRepository.GetMemberRole"/>); it never trusts anything the caller says about
    /// itself.
    ///
    /// Lives in <c>Odyssey.Application.Identity</c> (next to <c>DevIdentityProvider</c>), not in the
    /// Character namespace: it is about campaign participants, not about a character's ownership, and it
    /// depends only on the Application-layer repository port.
    /// </summary>
    public static class CampaignMembershipAuthorization
    {
        /// <summary>
        /// Success(true) only when <paramref name="actorUserId"/> has a membership in
        /// <paramref name="campaign"/> with role <see cref="CampaignMembershipRole.MainGm"/>; Success(false)
        /// for a Player, an Observer, or a user with no membership at all. A storage failure is returned as
        /// a failure -- callers must fail closed on it, never treat it as "not MainGM" silently or as "is".
        /// </summary>
        public static Result<bool> IsMainGm(ICampaignRepository repository, CampaignHandle campaign, UserId actorUserId, CorrelationId correlationId)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));

            Result<CampaignMemberLookup> role = repository.GetMemberRole(campaign, actorUserId, correlationId);
            if (role.IsFailure) return Result<bool>.Failure(role.Error);
            return Result<bool>.Success(role.Value.IsMember && role.Value.Role == CampaignMembershipRole.MainGm);
        }
    }
}
