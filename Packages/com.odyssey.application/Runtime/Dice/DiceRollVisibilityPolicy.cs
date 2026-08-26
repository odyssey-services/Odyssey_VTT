using System;
using System.Collections.Generic;
using Odyssey.Application.Audience;
using Odyssey.Application.Networking.Session;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Dice
{
    /// <summary>
    /// ODY-S03-006: 09_Dice_And_Game_Log section 16.5's projection rule --
    /// each connection gets either the full record or nothing (partial
    /// disclosure, section 16.5's "redacted record если контракт явно
    /// разрешает", is not implemented by this baseline task -- section 8 of
    /// the task contract records this as a known limitation, not silently
    /// dropped). There is no "denied" variant: a non-entitled caller receives
    /// no <see cref="DiceRollView"/> at all, the same safe-denial shape
    /// <c>VisibilityPolicy.ComputeVisibleEntities</c> (ODY-S02-010) already
    /// uses for scene entities -- never a distinguishable error, per
    /// <c>PERM-INV-012</c>/<c>ADR-021</c> section 8's "safe denial never
    /// confirms a hidden entity's existence."
    /// </summary>
    public sealed class DiceRollView
    {
        public DiceRollView(DiceRoll roll)
        {
            if (roll == null) throw new ArgumentNullException(nameof(roll));
            Roll = roll;
        }

        /// <summary>
        /// The full, unredacted roll. Baseline scope is all-or-nothing
        /// (section 16.5); no field-level redaction is applied within a
        /// visible view.
        /// </summary>
        public DiceRoll Roll { get; }
    }

    /// <summary>
    /// ODY-S03-006: applies <c>ADR-021</c> section 5's extension of
    /// <c>ADR-019</c> section 7's single-authoritative-state-plus-per-
    /// connection-filter pipeline to <see cref="DiceRoll"/> specifically --
    /// not a reuse of <c>Odyssey.Application.Networking.Projection.VisibilityPolicy</c>
    /// (that type's <c>SceneEntity</c>/two-kind visibility model is a
    /// different, already-fixed vocabulary per ADR-021 section 3.3; forcing
    /// <c>DiceRoll</c> through it would mean inventing a translation between
    /// two incompatible enums for no architectural benefit). Computed
    /// entirely in the Application layer, before any payload would reach
    /// <c>Odyssey.Networking</c> (ADR-019 section 6.2, not reopened) -- this
    /// task does not itself touch Networking; a future task wires this
    /// policy's output to a wire codec.
    /// </summary>
    public static class DiceRollVisibilityPolicy
    {
        /// <summary>
        /// Section 16.2: "Main GM всегда имеет доступ к gameplay event" --
        /// checked first, unconditionally, before any audience-kind branch.
        /// Returns <c>false</c> (no view) for every non-entitled combination,
        /// including a GMOnly roll requested by its own acting player
        /// (section 11.2's blind-roll design: the roller intentionally does
        /// not see a GM-only/blind result -- not a bug, the documented
        /// behavior).
        /// </summary>
        public static bool TryGetVisibleRoll(DiceRoll roll, UserId audienceUserId, BaselineRole role, ICampaignUserGroupDirectory groups, out DiceRollView view)
        {
            if (roll == null) throw new ArgumentNullException(nameof(roll));
            if (!audienceUserId.IsValid) throw new ArgumentException("AudienceUserId is required.", nameof(audienceUserId));
            if (groups == null) throw new ArgumentNullException(nameof(groups));

            bool visible = IsVisible(roll, audienceUserId, role, groups);
            view = visible ? new DiceRollView(roll) : null!;
            return visible;
        }

        /// <summary>Plural form for building every connected participant's view of one roll in a single pass -- mirrors <c>SceneProjectionBuilder</c>'s per-connection application of one pipeline to one authoritative state.</summary>
        public static IReadOnlyDictionary<UserId, DiceRollView> ComputeAudienceViews(DiceRoll roll, IEnumerable<(UserId UserId, BaselineRole Role)> participants, ICampaignUserGroupDirectory groups)
        {
            if (roll == null) throw new ArgumentNullException(nameof(roll));
            if (participants == null) throw new ArgumentNullException(nameof(participants));
            if (groups == null) throw new ArgumentNullException(nameof(groups));

            var result = new Dictionary<UserId, DiceRollView>();
            foreach ((UserId userId, BaselineRole role) in participants)
            {
                if (TryGetVisibleRoll(roll, userId, role, groups, out DiceRollView view))
                {
                    result[userId] = view;
                }
            }

            return result;
        }

        private static bool IsVisible(DiceRoll roll, UserId audienceUserId, BaselineRole role, ICampaignUserGroupDirectory groups)
        {
            if (role == BaselineRole.MainGM)
            {
                return true;
            }

            switch (roll.Audience.Kind)
            {
                case DiceRollAudienceKind.Public:
                    return true;

                case DiceRollAudienceKind.PlayerAndGM:
                    // MainGM already handled above; "the player" is the roll's own actor.
                    return roll.ActorUserId.Equals(audienceUserId);

                case DiceRollAudienceKind.GMOnly:
                    return false;

                case DiceRollAudienceKind.SelectedParticipants:
                    return IsSelectedParticipant(roll.Audience, audienceUserId, groups);

                default:
                    // Fail-closed: an unrecognized audience kind never
                    // widens visibility (08_Scenes_And_Board section 24.6's
                    // fail-closed principle, applied here to roll audience).
                    return false;
            }
        }

        private static bool IsSelectedParticipant(DiceRollAudience audience, UserId audienceUserId, ICampaignUserGroupDirectory groups)
        {
            foreach (UserId selectedUserId in audience.SelectedUserIds)
            {
                if (selectedUserId.Equals(audienceUserId))
                {
                    return true;
                }
            }

            foreach (string groupId in audience.SelectedGroupIds)
            {
                // ADR-021 section 4/16.5: an archived group does not apply to
                // new decisions -- current membership is re-checked at view
                // time (evaluation-time rule, section 6), not fixed at roll
                // creation.
                if (groups.TryGetGroup(groupId, out CampaignUserGroup group) && group.Status == CampaignUserGroupStatus.Active)
                {
                    foreach (UserId memberUserId in group.MemberUserIds)
                    {
                        if (memberUserId.Equals(audienceUserId))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
