using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Odyssey.Application.Networking;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Networking.InProcess;
using Odyssey.Networking.Session;
using Odyssey.Tests.Networking;

namespace Odyssey.Tests.Networking.SessionAdmission
{
    /// <summary>
    /// ODY-S02-009: the admission/lobby flow carried for real over
    /// InProcessSessionTransport (ADR-015) -- SendReliableAsync/DrainReliable,
    /// not a mocked-out transport. Exercises SessionAdmissionClientChannel/
    /// SessionAdmissionHostChannel (Odyssey.Networking) together with
    /// SessionAdmissionService (Odyssey.Application), the same host/client
    /// process split a real deployment would have.
    /// </summary>
    public sealed class SessionAdmissionTransportTests
    {
        private static readonly IWallClock Clock = new SystemWallClock();
        private static readonly UserId HostUser = UserId.Parse("user_00000000000000000000000000000001");
        private static readonly UserId JoiningUser = UserId.Parse("user_00000000000000000000000000000002");

        private static async Task<(ISessionTransport Host, ISessionTransport Client, ConnectionHandle HostHandle, ConnectionHandle ClientHandle)> ConnectPairAsync()
        {
            var range = new ProtocolVersionRange(ProtocolVersion.Create(1), ProtocolVersion.Create(1), ProtocolVersion.Create(1));
            (ISessionTransport host, ISessionTransport client) = InProcessSessionTransport.CreatePair(range, range, Clock);
            Result<ConnectionHandle> clientConnected = await client.ConnectAsync(new SessionEndpoint("host-1"), range, CancellationToken.None);
            Result<ConnectionHandle> hostConnected = await host.ConnectAsync(new SessionEndpoint("client-1"), range, CancellationToken.None);
            Assert.That(clientConnected.IsSuccess, Is.True);
            Assert.That(hostConnected.IsSuccess, Is.True);
            return (host, client, hostConnected.Value, clientConnected.Value);
        }

        [Test]
        public async Task FullAdmissionFlow_HostCreatesSession_PlayerJoinsByCode_HostAssignsRole_OverRealTransport()
        {
            (SessionAdmissionState state, JoinCode joinCode) = SessionAdmissionService.CreateSession(HostUser, Clock);
            (ISessionTransport hostTransport, ISessionTransport clientTransport, ConnectionHandle hostHandle, ConnectionHandle clientHandle) = await ConnectPairAsync();

            // Step 2: player sends a real join request over the wire.
            Result sendJoin = await SessionAdmissionClientChannel.SendJoinRequestAsync(clientTransport, clientHandle, joinCode, JoiningUser, Clock, CancellationToken.None);
            Assert.That(sendJoin.IsSuccess, Is.True);

            Result<int> processed = await SessionAdmissionHostChannel.ProcessPendingRequestsAsync(hostTransport, hostHandle, state, Clock, CancellationToken.None);
            Assert.That(processed.IsSuccess, Is.True);
            Assert.That(processed.Value, Is.EqualTo(1));

            Result<System.Collections.Generic.IReadOnlyList<AdmissionOutcomeMessage>> joinOutcomes = SessionAdmissionClientChannel.DrainOutcomes(clientTransport, clientHandle);
            Assert.That(joinOutcomes.IsSuccess, Is.True);
            Assert.That(joinOutcomes.Value.Count, Is.EqualTo(1));
            Assert.That(joinOutcomes.Value[0].Success, Is.True);
            Assert.That(joinOutcomes.Value[0].Role, Is.EqualTo(BaselineRole.Observer.ToString()));

            // Step 3: host assigns the Player role, also over the wire (in this
            // test, issued from the host's own transport instance -- a real
            // deployment would issue this from the host application's own UI
            // action, not a second network hop, but the wire encode/decode
            // path is exercised identically either way).
            Result sendRole = await SessionAdmissionClientChannel.SendRoleAssignmentRequestAsync(hostTransport, hostHandle, HostUser, JoiningUser, BaselineRole.Player, Clock, CancellationToken.None);
            Assert.That(sendRole.IsSuccess, Is.True);

            Result<int> processedRole = await SessionAdmissionHostChannel.ProcessPendingRequestsAsync(clientTransport, clientHandle, state, Clock, CancellationToken.None);
            Assert.That(processedRole.IsSuccess, Is.True);
            Assert.That(processedRole.Value, Is.EqualTo(1));

            Result<System.Collections.Generic.IReadOnlyList<AdmissionOutcomeMessage>> roleOutcomes = SessionAdmissionClientChannel.DrainOutcomes(hostTransport, hostHandle);
            Assert.That(roleOutcomes.IsSuccess, Is.True);
            Assert.That(roleOutcomes.Value.Count, Is.EqualTo(1));
            Assert.That(roleOutcomes.Value[0].Success, Is.True);
            Assert.That(roleOutcomes.Value[0].Role, Is.EqualTo(BaselineRole.Player.ToString()));

            Assert.That(state.Members[JoiningUser.ToString()].Role, Is.EqualTo(BaselineRole.Player));
        }

        [Test]
        public async Task Join_WithInvalidCode_OverRealTransport_ReturnsTypedFailure_NotException()
        {
            (SessionAdmissionState state, JoinCode correctCode) = SessionAdmissionService.CreateSession(HostUser, Clock);
            string correctText = correctCode.ToString();
            string wrongText = correctText[0] == 'A' ? "B" + correctText.Substring(1) : "A" + correctText.Substring(1);
            JoinCode wrongCode = JoinCode.Parse(wrongText);

            (ISessionTransport hostTransport, ISessionTransport clientTransport, ConnectionHandle hostHandle, ConnectionHandle clientHandle) = await ConnectPairAsync();

            await SessionAdmissionClientChannel.SendJoinRequestAsync(clientTransport, clientHandle, wrongCode, JoiningUser, Clock, CancellationToken.None);
            await SessionAdmissionHostChannel.ProcessPendingRequestsAsync(hostTransport, hostHandle, state, Clock, CancellationToken.None);

            Result<System.Collections.Generic.IReadOnlyList<AdmissionOutcomeMessage>> outcomes = SessionAdmissionClientChannel.DrainOutcomes(clientTransport, clientHandle);
            Assert.That(outcomes.IsSuccess, Is.True);
            Assert.That(outcomes.Value.Count, Is.EqualTo(1));
            Assert.That(outcomes.Value[0].Success, Is.False);
            Assert.That(outcomes.Value[0].ErrorCode, Is.EqualTo(ErrorCodes.NetworkingSessionJoinCodeInvalid.ToString()));
        }

        [Test]
        public async Task RoleAssignment_ToMainGM_OverRealTransport_ReturnsTypedFailure_NotException()
        {
            (SessionAdmissionState state, JoinCode joinCode) = SessionAdmissionService.CreateSession(HostUser, Clock);
            (ISessionTransport hostTransport, ISessionTransport clientTransport, ConnectionHandle hostHandle, ConnectionHandle clientHandle) = await ConnectPairAsync();

            await SessionAdmissionClientChannel.SendJoinRequestAsync(clientTransport, clientHandle, joinCode, JoiningUser, Clock, CancellationToken.None);
            await SessionAdmissionHostChannel.ProcessPendingRequestsAsync(hostTransport, hostHandle, state, Clock, CancellationToken.None);
            SessionAdmissionClientChannel.DrainOutcomes(clientTransport, clientHandle);

            await SessionAdmissionClientChannel.SendRoleAssignmentRequestAsync(hostTransport, hostHandle, HostUser, JoiningUser, BaselineRole.MainGM, Clock, CancellationToken.None);
            await SessionAdmissionHostChannel.ProcessPendingRequestsAsync(clientTransport, clientHandle, state, Clock, CancellationToken.None);

            Result<System.Collections.Generic.IReadOnlyList<AdmissionOutcomeMessage>> outcomes = SessionAdmissionClientChannel.DrainOutcomes(hostTransport, hostHandle);
            Assert.That(outcomes.IsSuccess, Is.True);
            Assert.That(outcomes.Value.Count, Is.EqualTo(1));
            Assert.That(outcomes.Value[0].Success, Is.False);
            Assert.That(outcomes.Value[0].ErrorCode, Is.EqualTo(ErrorCodes.NetworkingSessionRoleAssignmentDenied.ToString()));
        }
    }
}
