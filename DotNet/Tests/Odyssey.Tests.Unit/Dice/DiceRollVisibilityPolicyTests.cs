using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Application.Audience;
using Odyssey.Application.Commands;
using Odyssey.Application.Dice;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Random;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Rules.Versions;

namespace Odyssey.Tests.Unit.Dice
{
    /// <summary>
    /// ODY-S03-006: proves <see cref="DiceRollVisibilityPolicy"/> against all
    /// four 09_Dice_And_Game_Log section 16 audience kinds, plus safe denial
    /// (no distinguishable signal that a hidden roll exists), by analogy to
    /// ODY-S02-007's hidden-data-boundary tests.
    /// </summary>
    public sealed class DiceRollVisibilityPolicyTests
    {
        private static readonly IWallClock Clock = new SystemWallClock();
        private static readonly CampaignId TestCampaignId = CampaignId.Parse("camp_0123456789abcdef0123456789abcdef");
        private static readonly RulesetVersion TestRulesetVersion = RulesetVersion.Parse("1.0.0");
        private static readonly RngKeyEpochId TestEpoch = RngKeyEpochId.Parse("epoch-001");

        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
        private static CorrelationId NewCorrelationId() => CorrelationId.Parse("corr_" + Guid.NewGuid().ToString("N"));

        private static IAuthoritativeRandomStreamFactory NewRngFactory()
        {
            byte[] key = new byte[32];
            for (int index = 0; index < key.Length; index++) key[index] = (byte)(index + 1);
            return new DeterministicRandomStreamFactory(CampaignRngKey.FromBytes(key));
        }

        private sealed class SystemWallClock : IWallClock
        {
            public UtcInstant GetUtcNow() => UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow);
        }

        private static DiceRoll SubmitRoll(UserId actor, DiceRollAudience audience)
        {
            var store = new DiceRollStore();
            var request = new SubmitRollRequest(actor, true, "AttributeCheck", "1d20", audience, TestCampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, NewCorrelationId());
            return DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, request).Value;
        }

        [Test]
        public void PublicRoll_IsVisibleToEveryone()
        {
            UserId actor = NewUserId();
            UserId observer = NewUserId();
            DiceRoll roll = SubmitRoll(actor, DiceRollAudience.Public());
            var groups = new InMemoryCampaignUserGroupDirectory();

            Assert.That(DiceRollVisibilityPolicy.TryGetVisibleRoll(roll, actor, BaselineRole.Player, groups, out DiceRollView actorView), Is.True);
            Assert.That(actorView.Roll, Is.SameAs(roll));
            Assert.That(DiceRollVisibilityPolicy.TryGetVisibleRoll(roll, observer, BaselineRole.Observer, groups, out DiceRollView observerView), Is.True);
            Assert.That(observerView.Roll, Is.SameAs(roll));
            Assert.That(DiceRollVisibilityPolicy.TryGetVisibleRoll(roll, NewUserId(), BaselineRole.MainGM, groups, out DiceRollView gmView), Is.True);
            Assert.That(gmView.Roll, Is.SameAs(roll));
        }

        [Test]
        public void PlayerAndGMRoll_IsVisibleToActorAndMainGM_NotToUnrelatedObserver()
        {
            UserId actor = NewUserId();
            UserId mainGm = NewUserId();
            UserId observer = NewUserId();
            DiceRoll roll = SubmitRoll(actor, DiceRollAudience.PlayerAndGM());
            var groups = new InMemoryCampaignUserGroupDirectory();

            Assert.That(DiceRollVisibilityPolicy.TryGetVisibleRoll(roll, actor, BaselineRole.Player, groups, out DiceRollView actorView), Is.True);
            Assert.That(actorView.Roll, Is.SameAs(roll));
            Assert.That(DiceRollVisibilityPolicy.TryGetVisibleRoll(roll, mainGm, BaselineRole.MainGM, groups, out DiceRollView gmView), Is.True);
            Assert.That(gmView.Roll, Is.SameAs(roll));

            Assert.That(DiceRollVisibilityPolicy.TryGetVisibleRoll(roll, observer, BaselineRole.Observer, groups, out DiceRollView observerView), Is.False);
            Assert.That(observerView, Is.Null);
        }

        [Test]
        public void GMOnlyRoll_IsVisibleOnlyToMainGM_EvenTheRollersOwnActorCannotSeeIt()
        {
            UserId actor = NewUserId();
            UserId mainGm = NewUserId();
            DiceRoll roll = SubmitRoll(actor, DiceRollAudience.GMOnly());
            var groups = new InMemoryCampaignUserGroupDirectory();

            Assert.That(DiceRollVisibilityPolicy.TryGetVisibleRoll(roll, mainGm, BaselineRole.MainGM, groups, out DiceRollView gmView), Is.True);
            Assert.That(gmView.Roll, Is.SameAs(roll));

            // Section 11.2's blind-roll design: the acting player does not see
            // their own GMOnly result -- deliberate, not a bug.
            Assert.That(DiceRollVisibilityPolicy.TryGetVisibleRoll(roll, actor, BaselineRole.Player, groups, out DiceRollView actorView), Is.False);
            Assert.That(actorView, Is.Null);
        }

        [Test]
        public void SelectedParticipantsRoll_IsVisibleToListedUserAndActiveGroupMember_NotToOthers()
        {
            UserId actor = NewUserId();
            UserId mainGm = NewUserId();
            UserId selectedUser = NewUserId();
            UserId groupMember = NewUserId();
            UserId excludedPlayer = NewUserId();

            const string groupId = "group_selected";
            var groups = new InMemoryCampaignUserGroupDirectory();
            groups.Upsert(new CampaignUserGroup(groupId, TestCampaignId, new List<UserId> { groupMember }, CampaignUserGroupStatus.Active, revision: 1));

            DiceRoll roll = SubmitRoll(actor, DiceRollAudience.SelectedParticipants(new List<UserId> { selectedUser }, new List<string> { groupId }));

            Assert.That(DiceRollVisibilityPolicy.TryGetVisibleRoll(roll, selectedUser, BaselineRole.Player, groups, out DiceRollView selectedView), Is.True);
            Assert.That(selectedView.Roll, Is.SameAs(roll));
            Assert.That(DiceRollVisibilityPolicy.TryGetVisibleRoll(roll, groupMember, BaselineRole.Player, groups, out DiceRollView groupView), Is.True);
            Assert.That(groupView.Roll, Is.SameAs(roll));

            // Section 16.2: Main GM always sees, regardless of audience kind,
            // even when not itself a member of SelectedParticipants.
            Assert.That(DiceRollVisibilityPolicy.TryGetVisibleRoll(roll, mainGm, BaselineRole.MainGM, groups, out DiceRollView gmView), Is.True);
            Assert.That(gmView.Roll, Is.SameAs(roll));

            Assert.That(DiceRollVisibilityPolicy.TryGetVisibleRoll(roll, excludedPlayer, BaselineRole.Player, groups, out DiceRollView excludedView), Is.False);
            Assert.That(excludedView, Is.Null);
        }

        [Test]
        public void SelectedParticipantsRoll_ArchivedGroupMembership_DoesNotGrantVisibility()
        {
            UserId actor = NewUserId();
            UserId archivedGroupMember = NewUserId();
            const string groupId = "group_archived";
            var groups = new InMemoryCampaignUserGroupDirectory();
            groups.Upsert(new CampaignUserGroup(groupId, TestCampaignId, new List<UserId> { archivedGroupMember }, CampaignUserGroupStatus.Archived, revision: 1));

            DiceRoll roll = SubmitRoll(actor, DiceRollAudience.SelectedParticipants(null, new List<string> { groupId }));

            Assert.That(DiceRollVisibilityPolicy.TryGetVisibleRoll(roll, archivedGroupMember, BaselineRole.Player, groups, out DiceRollView view), Is.False);
            Assert.That(view, Is.Null);
        }

        [Test]
        public void ComputeAudienceViews_ReturnsOnlyEntriesForVisibleParticipants_SafeDenialLeavesNoTraceForOthers()
        {
            UserId actor = NewUserId();
            UserId mainGm = NewUserId();
            UserId excludedObserver = NewUserId();
            DiceRoll roll = SubmitRoll(actor, DiceRollAudience.PlayerAndGM());
            var groups = new InMemoryCampaignUserGroupDirectory();

            var participants = new List<(UserId UserId, BaselineRole Role)>
            {
                (actor, BaselineRole.Player),
                (mainGm, BaselineRole.MainGM),
                (excludedObserver, BaselineRole.Observer),
            };

            IReadOnlyDictionary<UserId, DiceRollView> views = DiceRollVisibilityPolicy.ComputeAudienceViews(roll, participants, groups);

            Assert.That(views.Count, Is.EqualTo(2));
            Assert.That(views.ContainsKey(actor), Is.True);
            Assert.That(views.ContainsKey(mainGm), Is.True);

            // Safe denial: the excluded observer gets no entry at all -- not a
            // null placeholder, not an error, no signal that a roll exists.
            Assert.That(views.ContainsKey(excludedObserver), Is.False);
        }
    }
}
