using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Networking;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Networking.InProcess;

namespace Odyssey.Tests.Networking
{
    internal sealed class SystemWallClock : IWallClock
    {
        public UtcInstant GetUtcNow() => UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// ODY-S02-001: contract tests for ISessionTransport, proven against
    /// InProcessSessionTransport (the in-process/mock implementation this task
    /// ships) -- send/receive, protocol negotiation, timeout/cancellation, and
    /// failure-when-unavailable, per the task's own required test list. Not a
    /// test of any real network/relay behavior -- that is ODY-S02-002/003.
    /// </summary>
    public sealed class InProcessSessionTransportTests
    {
        private static readonly IWallClock Clock = new SystemWallClock();

        private static ProtocolVersionRange Range(int min, int max, int preferred) =>
            new ProtocolVersionRange(ProtocolVersion.Create(min), ProtocolVersion.Create(max), ProtocolVersion.Create(preferred));

        private static NetworkEnvelope MakeEnvelope(SessionId sessionId, ProtocolVersion protocolVersion, string payloadType = "test.payload")
        {
            return new NetworkEnvelope(
                MessageId.NewId(Clock.GetUtcNow()),
                sessionId,
                senderUserId: null,
                senderClientInstanceId: null,
                NetworkMessageKind.ApplicationPayload,
                protocolVersion,
                correlationId: null,
                causationId: null,
                sentAtHostTime: Clock.GetUtcNow(),
                payloadType,
                payloadVersion: 1,
                payload: new byte[] { 1, 2, 3 });
        }

        [Test]
        public async Task ConnectAsync_OverlappingRanges_NegotiatesHighestCommonVersion_ReturnsConnectionHandle()
        {
            // ConnectAsync negotiates the CALLED instance's own local range
            // against the range parameter the caller supplies (the remote
            // peer's declared range) -- it does not read the paired peer
            // object's range. Calling it on the "host" side (localRange=1..3)
            // with the client's declared range (2..5) exercises real overlap
            // negotiation: overlap is [max(1,2), min(3,5)] = [2,3].
            (ISessionTransport host, ISessionTransport client) = InProcessSessionTransport.CreatePair(Range(1, 3, 3), Range(2, 5, 5), Clock);

            Result<ConnectionHandle> result = await host.ConnectAsync(new SessionEndpoint("client-1"), Range(2, 5, 5), CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.NegotiatedProtocolVersion.Value, Is.EqualTo(3), "the negotiated version must be the highest value both ranges support (min(3,5)=3)");
        }

        [Test]
        public async Task ConnectAsync_NonOverlappingRanges_ReturnsTypedProtocolVersionUnsupported()
        {
            (ISessionTransport host, ISessionTransport client) = InProcessSessionTransport.CreatePair(Range(1, 2, 2), Range(5, 8, 5), Clock);

            Result<ConnectionHandle> result = await host.ConnectAsync(new SessionEndpoint("client-1"), Range(5, 8, 5), CancellationToken.None);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.NetworkingProtocolVersionUnsupported));
            Assert.That(result.Error.Category, Is.EqualTo(ErrorCategory.Compatibility));
        }

        [Test]
        public async Task SendReliable_ThenDrainOnPeer_DeliversEnvelopesInArrivalOrder()
        {
            (ISessionTransport host, ISessionTransport client) = InProcessSessionTransport.CreatePair(Range(1, 1, 1), Range(1, 1, 1), Clock);
            Result<ConnectionHandle> connected = await client.ConnectAsync(new SessionEndpoint("host-1"), Range(1, 1, 1), CancellationToken.None);
            Assert.That(connected.IsSuccess, Is.True);
            Result<ConnectionHandle> hostConnected = await host.ConnectAsync(new SessionEndpoint("client-1"), Range(1, 1, 1), CancellationToken.None);
            Assert.That(hostConnected.IsSuccess, Is.True);

            SessionId sessionId = connected.Value.SessionId;
            NetworkEnvelope first = MakeEnvelope(sessionId, connected.Value.NegotiatedProtocolVersion, "first");
            NetworkEnvelope second = MakeEnvelope(sessionId, connected.Value.NegotiatedProtocolVersion, "second");

            Result sendFirst = await client.SendReliableAsync(connected.Value, first, CancellationToken.None);
            Result sendSecond = await client.SendReliableAsync(connected.Value, second, CancellationToken.None);
            Assert.That(sendFirst.IsSuccess, Is.True);
            Assert.That(sendSecond.IsSuccess, Is.True);

            Result<IReadOnlyList<NetworkEnvelope>> drained = host.DrainReliable(hostConnected.Value);
            Assert.That(drained.IsSuccess, Is.True);
            Assert.That(drained.Value.Count, Is.EqualTo(2));
            Assert.That(drained.Value[0].PayloadType, Is.EqualTo("first"));
            Assert.That(drained.Value[1].PayloadType, Is.EqualTo("second"));

            // A second drain with nothing new queued must return empty, not
            // re-deliver already-drained envelopes.
            Result<IReadOnlyList<NetworkEnvelope>> secondDrain = host.DrainReliable(hostConnected.Value);
            Assert.That(secondDrain.Value.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task SendRealtime_ThenDrainOnPeer_DeliversEnvelope()
        {
            (ISessionTransport host, ISessionTransport client) = InProcessSessionTransport.CreatePair(Range(1, 1, 1), Range(1, 1, 1), Clock);
            Result<ConnectionHandle> connected = await client.ConnectAsync(new SessionEndpoint("host-1"), Range(1, 1, 1), CancellationToken.None);
            Result<ConnectionHandle> hostConnected = await host.ConnectAsync(new SessionEndpoint("client-1"), Range(1, 1, 1), CancellationToken.None);

            var preview = new RealtimeEnvelope(connected.Value.SessionId, null, null, connected.Value.NegotiatedProtocolVersion, "drag.preview", new byte[] { 9 });
            Result sendResult = await client.SendRealtimeAsync(connected.Value, preview, CancellationToken.None);
            Assert.That(sendResult.IsSuccess, Is.True);

            Result<IReadOnlyList<RealtimeEnvelope>> drained = host.DrainRealtime(hostConnected.Value);
            Assert.That(drained.IsSuccess, Is.True);
            Assert.That(drained.Value.Count, Is.EqualTo(1));
            Assert.That(drained.Value[0].PayloadType, Is.EqualTo("drag.preview"));
        }

        [Test]
        public async Task SendReliable_BeforeConnect_ReturnsTypedNotConnected_NoRawException()
        {
            (ISessionTransport host, ISessionTransport client) = InProcessSessionTransport.CreatePair(Range(1, 1, 1), Range(1, 1, 1), Clock);

            // A handle constructed independently of an actual successful
            // ConnectAsync call on this transport instance -- simulates calling
            // Send before Connect ever succeeded on this side.
            var phantomHandle = new ConnectionHandle(SessionId.Parse("sess_00000000000000000000000000000000"), ProtocolVersion.Create(1), Clock.GetUtcNow());
            NetworkEnvelope envelope = MakeEnvelope(phantomHandle.SessionId, phantomHandle.NegotiatedProtocolVersion);

            Result result = await client.SendReliableAsync(phantomHandle, envelope, CancellationToken.None);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.NetworkingTransportNotConnected));
        }

        // NetworkingFailures.ConnectFailed and .SendFailed exist for a real
        // transport's failure modes (e.g. a relay session that drops before
        // negotiation, or a send that fails at the socket level) that
        // InProcessSessionTransport -- always fully paired via CreatePair,
        // with no real I/O to fail -- cannot trigger through its public API.
        // These tests prove the factories themselves produce the correct
        // typed Error shape, rather than skipping them silently.
        [Test]
        public void NetworkingFailures_ConnectFailed_ProducesTransientInfrastructureError()
        {
            Error error = NetworkingFailures.ConnectFailed(CorrelationId.Parse("corr_00000000000000000000000000000000"));
            Assert.That(error.Code, Is.EqualTo(ErrorCodes.NetworkingTransportConnectFailed));
            Assert.That(error.Category, Is.EqualTo(ErrorCategory.TransientInfrastructure));
            Assert.That(error.RetryDirective, Is.EqualTo(RetryDirective.RetryWithBackoff));
        }

        [Test]
        public void NetworkingFailures_ConnectTimedOut_ProducesTransientInfrastructureError()
        {
            Error error = NetworkingFailures.ConnectTimedOut(CorrelationId.Parse("corr_00000000000000000000000000000000"));
            Assert.That(error.Code, Is.EqualTo(ErrorCodes.NetworkingTransportConnectTimedOut));
            Assert.That(error.Category, Is.EqualTo(ErrorCategory.TransientInfrastructure));
            Assert.That(error.RetryDirective, Is.EqualTo(RetryDirective.RetryWithBackoff));
        }

        [Test]
        public void NetworkingFailures_SendFailed_ProducesTransientInfrastructureError()
        {
            Error error = NetworkingFailures.SendFailed(CorrelationId.Parse("corr_00000000000000000000000000000000"));
            Assert.That(error.Code, Is.EqualTo(ErrorCodes.NetworkingTransportSendFailed));
            Assert.That(error.Category, Is.EqualTo(ErrorCategory.TransientInfrastructure));
            Assert.That(error.RetryDirective, Is.EqualTo(RetryDirective.RetryWithBackoff));
        }

        [Test]
        public async Task ConnectAsync_AlreadyCancelledToken_ReturnsTypedOperationCancelled_NoRawException()
        {
            (ISessionTransport host, ISessionTransport client) = InProcessSessionTransport.CreatePair(Range(1, 1, 1), Range(1, 1, 1), Clock);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Result<ConnectionHandle> result = await client.ConnectAsync(new SessionEndpoint("host-1"), Range(1, 1, 1), cts.Token);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.NetworkingTransportOperationCancelled));
            Assert.That(result.Error.Category, Is.EqualTo(ErrorCategory.Cancelled));
        }

        [Test]
        public async Task SendReliable_AlreadyCancelledToken_ReturnsTypedOperationCancelled_NoRawException()
        {
            (ISessionTransport host, ISessionTransport client) = InProcessSessionTransport.CreatePair(Range(1, 1, 1), Range(1, 1, 1), Clock);
            Result<ConnectionHandle> connected = await client.ConnectAsync(new SessionEndpoint("host-1"), Range(1, 1, 1), CancellationToken.None);
            NetworkEnvelope envelope = MakeEnvelope(connected.Value.SessionId, connected.Value.NegotiatedProtocolVersion);

            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Result result = await client.SendReliableAsync(connected.Value, envelope, cts.Token);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.NetworkingTransportOperationCancelled));
        }

        [Test]
        public async Task Disconnect_ThenSend_ReturnsTypedNotConnected()
        {
            (ISessionTransport host, ISessionTransport client) = InProcessSessionTransport.CreatePair(Range(1, 1, 1), Range(1, 1, 1), Clock);
            Result<ConnectionHandle> connected = await client.ConnectAsync(new SessionEndpoint("host-1"), Range(1, 1, 1), CancellationToken.None);
            Assert.That(connected.IsSuccess, Is.True);

            Result disconnectResult = client.Disconnect(connected.Value);
            Assert.That(disconnectResult.IsSuccess, Is.True);

            NetworkEnvelope envelope = MakeEnvelope(connected.Value.SessionId, connected.Value.NegotiatedProtocolVersion);
            Result sendAfterDisconnect = await client.SendReliableAsync(connected.Value, envelope, CancellationToken.None);

            Assert.That(sendAfterDisconnect.IsFailure, Is.True);
            Assert.That(sendAfterDisconnect.Error.Code, Is.EqualTo(ErrorCodes.NetworkingTransportNotConnected));
        }

        [Test]
        public void ProtocolVersionRange_PreferredOutsideMinMax_ThrowsArgumentException()
        {
            Action action = () => new ProtocolVersionRange(ProtocolVersion.Create(2), ProtocolVersion.Create(4), ProtocolVersion.Create(10));
            Assert.Throws<ArgumentException>(action);
        }

        [Test]
        public void ProtocolVersionRange_MaxBelowMin_ThrowsArgumentException()
        {
            Action action = () => new ProtocolVersionRange(ProtocolVersion.Create(5), ProtocolVersion.Create(1), ProtocolVersion.Create(3));
            Assert.Throws<ArgumentException>(action);
        }

        [Test]
        public void NegotiateWith_IdenticalSingleVersionRanges_NegotiatesThatVersion()
        {
            ProtocolVersionRange a = Range(1, 1, 1);
            ProtocolVersionRange b = Range(1, 1, 1);
            Assert.That(a.NegotiateWith(b)!.Value.Value, Is.EqualTo(1));
        }
    }
}
