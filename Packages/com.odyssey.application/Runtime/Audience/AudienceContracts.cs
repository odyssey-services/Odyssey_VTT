using System;
using System.Collections.Generic;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Audience
{
    /// <summary>ADR-021 section 4's narrow read-model subset of 07_Permissions section 16.1's full CampaignUserGroup aggregate -- exactly what audience-resolution needs, not the full lifecycle contract (create/rename/archive stay ordinary future commands, not this ADR's or this task's concern).</summary>
    public enum CampaignUserGroupStatus
    {
        Active = 1,
        Archived = 2,
    }

    /// <summary>
    /// ADR-021 section 4: CampaignUserGroupId/CampaignId/MemberUserIds/Status/Revision --
    /// the read-model subset needed to answer "is this user currently a member of
    /// this group," nothing about how the group came to exist.
    /// </summary>
    public sealed class CampaignUserGroup
    {
        public CampaignUserGroup(string campaignUserGroupId, CampaignId campaignId, IReadOnlyList<UserId> memberUserIds, CampaignUserGroupStatus status, long revision)
        {
            if (string.IsNullOrWhiteSpace(campaignUserGroupId)) throw new ArgumentException("CampaignUserGroupId is required.", nameof(campaignUserGroupId));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (memberUserIds == null) throw new ArgumentNullException(nameof(memberUserIds));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));

            CampaignUserGroupId = campaignUserGroupId;
            CampaignId = campaignId;
            MemberUserIds = memberUserIds;
            Status = status;
            Revision = revision;
        }

        public string CampaignUserGroupId { get; }
        public CampaignId CampaignId { get; }
        public IReadOnlyList<UserId> MemberUserIds { get; }
        public CampaignUserGroupStatus Status { get; }
        public long Revision { get; }
    }

    /// <summary>
    /// ADR-021 section 4/6: audience resolution reads *current* group state,
    /// never a snapshot fixed at artifact-creation time. This is a query-side
    /// port only -- no lifecycle command (create/rename/membership-change/
    /// archive) is defined here, per ADR-021 section 4's own scoping (those
    /// are ordinary future ADR-002 commands, not an architecturally new
    /// question this task or ADR-021 needed to fix).
    /// </summary>
    public interface ICampaignUserGroupDirectory
    {
        bool TryGetGroup(string campaignUserGroupId, out CampaignUserGroup group);
    }

    /// <summary>
    /// ODY-S03-006: a minimal in-memory fixture satisfying <see cref="ICampaignUserGroupDirectory"/> --
    /// enough to prove <c>SelectedParticipants</c>/group-based audience resolution
    /// end-to-end (task contract section 3's explicit decision: a fixture for this
    /// task's own tests and any future caller, not a full lifecycle
    /// implementation, which ADR-021 section 4 already deferred as an ordinary
    /// implementation task). A future task wiring real group lifecycle commands
    /// replaces or backs this with a durable store without changing the
    /// <see cref="ICampaignUserGroupDirectory"/> contract callers already depend on.
    /// </summary>
    public sealed class InMemoryCampaignUserGroupDirectory : ICampaignUserGroupDirectory
    {
        private readonly Dictionary<string, CampaignUserGroup> _groups = new Dictionary<string, CampaignUserGroup>(StringComparer.Ordinal);

        public void Upsert(CampaignUserGroup group)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            _groups[group.CampaignUserGroupId] = group;
        }

        public bool TryGetGroup(string campaignUserGroupId, out CampaignUserGroup group) =>
            _groups.TryGetValue(campaignUserGroupId, out group!);
    }
}
