using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Application.Networking.Projection;
using Odyssey.Application.Networking.Reconnect;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Tests.Networking.Reconnect
{
    /// <summary>
    /// ODY-S02-012: pure, transport-independent SessionDeltaBuffer/
    /// ReconnectPlanner logic -- roadmap 11.6 steps 8-10 without needing
    /// InProcessSessionTransport (the transport-carried path lives in
    /// ReconnectTransportTests.cs).
    /// </summary>
    public sealed class ReconnectServiceTests
    {
        private static readonly IWallClock Clock = new SystemWallClock();
        private static readonly UtcInstant Now = Clock.GetUtcNow();
        private static readonly UserId HostUser = UserId.Parse("user_00000000000000000000000000000001");
        private static readonly UserId PlayerUser = UserId.Parse("user_00000000000000000000000000000002");

        [Test]
        public void SessionDeltaBuffer_WithinCapacity_TryGetRangeSince_ReturnsAllMissedEntries()
        {
            SessionDeltaBuffer buffer = new SessionDeltaBuffer(capacity: 3);
            buffer.Record("e1", new TokenPosition(1, 1), 2, Now);
            buffer.Record("e2", new TokenPosition(2, 2), 2, Now);

            bool found = buffer.TryGetRangeSince(0, out IReadOnlyList<BufferedDelta> entries);

            Assert.That(found, Is.True);
            Assert.That(entries.Count, Is.EqualTo(2));
            Assert.That(entries[0].EntityId, Is.EqualTo("e1"));
            Assert.That(entries[1].EntityId, Is.EqualTo("e2"));
        }

        [Test]
        public void SessionDeltaBuffer_RangeExceedsCapacity_TryGetRangeSince_ReturnsFalse()
        {
            SessionDeltaBuffer buffer = new SessionDeltaBuffer(capacity: 2);
            buffer.Record("e1", new TokenPosition(1, 1), 2, Now);
            buffer.Record("e2", new TokenPosition(2, 2), 2, Now);
            buffer.Record("e3", new TokenPosition(3, 3), 2, Now); // evicts e1
            buffer.Record("e4", new TokenPosition(4, 4), 2, Now); // evicts e2

            bool found = buffer.TryGetRangeSince(0, out IReadOnlyList<BufferedDelta> entries);

            Assert.That(found, Is.False);
            Assert.That(entries.Count, Is.EqualTo(0));
        }

        [Test]
        public void SessionDeltaBuffer_AlreadyCaughtUp_TryGetRangeSince_ReturnsEmptySuccess()
        {
            SessionDeltaBuffer buffer = new SessionDeltaBuffer(capacity: 3);
            buffer.Record("e1", new TokenPosition(1, 1), 2, Now);

            bool found = buffer.TryGetRangeSince(buffer.LatestSequence, out IReadOnlyList<BufferedDelta> entries);

            Assert.That(found, Is.True);
            Assert.That(entries.Count, Is.EqualTo(0));
        }

        [Test]
        public void ReconnectPlanner_Plan_UnknownAudience_ReturnsTypedMemberNotFound()
        {
            (SessionAdmissionState admission, _) = SessionAdmissionService.CreateSession(HostUser, Clock);
            Scene scene = new Scene("scene_1");
            ReconnectSessionState state = new ReconnectSessionState(scene, new SessionDeltaBuffer());

            Result<ReconnectPlan> plan = ReconnectPlanner.Plan(state, admission, PlayerUser, admission.Directory.SessionId, Clock);

            Assert.That(plan.IsSuccess, Is.False);
            Assert.That(plan.Error.Code, Is.EqualTo(ErrorCodes.NetworkingSessionMemberNotFound));
        }

        [Test]
        public void ReconnectPlanner_Plan_FiltersCatchupEntriesByCurrentVisibility_NotStale()
        {
            (SessionAdmissionState admission, JoinCode joinCode) = SessionAdmissionService.CreateSession(HostUser, Clock);
            SessionAdmissionService.TryJoin(admission, joinCode, PlayerUser);
            SessionAdmissionService.AssignRole(admission, HostUser, PlayerUser, BaselineRole.Player);

            Scene scene = new Scene("scene_1");
            scene.AddEntity(new SceneEntity("token_hero", "Hero", SceneEntityVisibility.HiddenGameplay, PlayerUser));
            ReconnectSessionState state = new ReconnectSessionState(scene, new SessionDeltaBuffer());
            state.Buffer.Record("token_hero", new TokenPosition(1, 1), 2, Now);

            // Role revoked (downgraded to Observer) while "disconnected" --
            // ADR-017 section 1 point 8: redaction must use the CURRENT role.
            SessionAdmissionService.AssignRole(admission, HostUser, PlayerUser, BaselineRole.Observer);

            Result<ReconnectPlan> plan = ReconnectPlanner.Plan(state, admission, PlayerUser, admission.Directory.SessionId, Clock);

            Assert.That(plan.IsSuccess, Is.True);
            Assert.That(plan.Value.Kind, Is.EqualTo(ReconnectPathKind.DeltaCatchup));
            Assert.That(plan.Value.CatchupEntries.Count, Is.EqualTo(0));
        }

        [Test]
        public void ClientProjectionState_TryApply_DuplicateSequence_IsIgnored()
        {
            ClientProjectionState client = new ClientProjectionState();

            bool first = client.TryApply(1, "e1", new TokenPosition(5, 5));
            bool duplicate = client.TryApply(1, "e1", new TokenPosition(99, 99));

            Assert.That(first, Is.True);
            Assert.That(duplicate, Is.False);
            Assert.That(client.Positions["e1"].X, Is.EqualTo(5));
            Assert.That(client.LastAppliedSequence, Is.EqualTo(1));
        }
    }
}
