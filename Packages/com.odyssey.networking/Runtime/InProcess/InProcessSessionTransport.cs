using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Odyssey.Application.Networking;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Networking.InProcess
{
    /// <summary>
    /// ODY-S02-001: the "in-process/mock transport только для automated tests"
    /// roadmap section 11.3 requires. This is the first production code in
    /// Odyssey.Networking (ADR-001 section 6.6's boundary was already defined,
    /// but no task had implemented anything against it until this one --
    /// confirmed by ODY-S02-000's own verified-facts section).
    ///
    /// Two instances, created together via <see cref="CreatePair"/>, deliver
    /// envelopes to each other through in-memory queues -- no socket, no real
    /// network I/O. A real relay-backed transport (ODY-S02-002/003) implements
    /// the same <see cref="ISessionTransport"/> port; this class exists purely
    /// so ODY-S02-001's own contract tests, and every later task's automated
    /// tests, can exercise the port deterministically without a live network.
    /// </summary>
    public sealed class InProcessSessionTransport : ISessionTransport
    {
        private readonly ProtocolVersionRange _localRange;
        private readonly IWallClock _clock;
        private readonly ConcurrentQueue<NetworkEnvelope> _reliableInbox = new ConcurrentQueue<NetworkEnvelope>();
        private readonly ConcurrentQueue<RealtimeEnvelope> _realtimeInbox = new ConcurrentQueue<RealtimeEnvelope>();
        private InProcessSessionTransport? _peer;
        private ConnectionHandle? _connection;

        private InProcessSessionTransport(ProtocolVersionRange localRange, IWallClock clock)
        {
            _localRange = localRange ?? throw new ArgumentNullException(nameof(localRange));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <summary>
        /// Creates two connected mock transport endpoints ("host" and "client"),
        /// each configured with its own supported protocol range, wired to
        /// deliver envelopes to each other.
        /// </summary>
        public static (ISessionTransport Host, ISessionTransport Client) CreatePair(ProtocolVersionRange hostRange, ProtocolVersionRange clientRange, IWallClock clock)
        {
            var host = new InProcessSessionTransport(hostRange, clock);
            var client = new InProcessSessionTransport(clientRange, clock);
            host._peer = client;
            client._peer = host;
            return (host, client);
        }

        public Task<Result<ConnectionHandle>> ConnectAsync(SessionEndpoint endpoint, ProtocolVersionRange clientProtocolRange, CancellationToken cancellationToken)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));
            if (clientProtocolRange == null) throw new ArgumentNullException(nameof(clientProtocolRange));

            var correlationId = CorrelationId.Parse("corr_00000000000000000000000000000000");

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(Result<ConnectionHandle>.Failure(NetworkingFailures.OperationCancelled(correlationId)));
            }

            if (_peer == null)
            {
                return Task.FromResult(Result<ConnectionHandle>.Failure(NetworkingFailures.ConnectFailed(correlationId)));
            }

            ProtocolVersion? negotiated = _localRange.NegotiateWith(clientProtocolRange);
            if (!negotiated.HasValue)
            {
                return Task.FromResult(Result<ConnectionHandle>.Failure(NetworkingFailures.ProtocolVersionUnsupported(correlationId)));
            }

            UtcInstant now = _clock.GetUtcNow();
            SessionId sessionId = SessionId.Parse("sess_" + DeterministicHex32(endpoint.EndpointId, now));
            var handle = new ConnectionHandle(sessionId, negotiated.Value, now);
            _connection = handle;
            return Task.FromResult(Result<ConnectionHandle>.Success(handle));
        }

        public Task<Result> SendReliableAsync(ConnectionHandle connection, NetworkEnvelope envelope, CancellationToken cancellationToken)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));

            var correlationId = CorrelationId.Parse("corr_00000000000000000000000000000000");
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(Result.Failure(NetworkingFailures.OperationCancelled(correlationId)));
            }

            if (_connection == null || _peer == null)
            {
                return Task.FromResult(Result.Failure(NetworkingFailures.NotConnected(correlationId)));
            }

            _peer._reliableInbox.Enqueue(envelope);
            return Task.FromResult(Result.Success());
        }

        public Task<Result> SendRealtimeAsync(ConnectionHandle connection, RealtimeEnvelope envelope, CancellationToken cancellationToken)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));

            var correlationId = CorrelationId.Parse("corr_00000000000000000000000000000000");
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(Result.Failure(NetworkingFailures.OperationCancelled(correlationId)));
            }

            if (_connection == null || _peer == null)
            {
                return Task.FromResult(Result.Failure(NetworkingFailures.NotConnected(correlationId)));
            }

            // 06_Networking section 5.2: realtime preview data may be lost --
            // this mock delivers it (no artificial drop), but callers must not
            // depend on delivery, the same as a real unreliable channel.
            _peer._realtimeInbox.Enqueue(envelope);
            return Task.FromResult(Result.Success());
        }

        public Result<IReadOnlyList<NetworkEnvelope>> DrainReliable(ConnectionHandle connection)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            var drained = new List<NetworkEnvelope>();
            while (_reliableInbox.TryDequeue(out NetworkEnvelope? envelope))
            {
                drained.Add(envelope);
            }

            return Result<IReadOnlyList<NetworkEnvelope>>.Success(drained);
        }

        public Result<IReadOnlyList<RealtimeEnvelope>> DrainRealtime(ConnectionHandle connection)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            var drained = new List<RealtimeEnvelope>();
            while (_realtimeInbox.TryDequeue(out RealtimeEnvelope? envelope))
            {
                drained.Add(envelope);
            }

            return Result<IReadOnlyList<RealtimeEnvelope>>.Success(drained);
        }

        public Result Disconnect(ConnectionHandle connection)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            _connection = null;
            // The peer link is intentionally left in place: the peer's own
            // Disconnect() call governs its own state, and this side's future
            // sends must fail with NotConnected regardless of the peer's state.
            return Result.Success();
        }

        private static string DeterministicHex32(string seed, UtcInstant now)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            byte[] hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(seed + "|" + now));
            var builder = new System.Text.StringBuilder(32);
            for (int index = 0; index < 16; index++)
            {
                builder.Append(hash[index].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
