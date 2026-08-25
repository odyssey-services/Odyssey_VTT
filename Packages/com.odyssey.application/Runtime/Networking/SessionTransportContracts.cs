using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Odyssey.Application.Commands;
using Odyssey.Application.Results;
using Odyssey.Domain.Events;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Networking
{
    /// <summary>
    /// ODY-S02-001: the transport abstraction roadmap section 11.3 names.
    /// `Odyssey.Application` declares this port; `Odyssey.Networking` implements
    /// it (ADR-001 section 6.6). This task defines the interface and an
    /// in-process/mock implementation for automated tests only -- no real
    /// network, relay, or rendezvous code exists yet (that is ODY-S02-002/003).
    ///
    /// Shape follows 06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md section
    /// 4.3's illustrative sketch (reliable + realtime send/receive over a
    /// connection to a SessionEndpoint), adapted to this codebase's established
    /// ADR-004 Result/Error discipline: the source sketch uses bare
    /// Task/IAsyncEnumerable with exceptions; every method here instead returns
    /// a typed Result (or Task&lt;Result&lt;T&gt;&gt; for the genuinely
    /// I/O-bound connect/send operations), and reads are a synchronous
    /// drain-style poll (DrainReliable/DrainRealtime) rather than
    /// IAsyncEnumerable -- simpler to implement deterministically in the mock
    /// transport this task ships, and no real async transport exists yet to
    /// make IAsyncEnumerable's backpressure/cancellation semantics pull their
    /// weight. A future real-transport task may reconsider this if it finds
    /// drain-style polling insufficient; nothing here forecloses that.
    /// </summary>
    public interface ISessionTransport
    {
        /// <summary>
        /// Establishes a connection to <paramref name="endpoint"/>, negotiating
        /// a protocol version against <paramref name="clientProtocolRange"/>
        /// (06_Networking section 10.2). Returns a typed
        /// <see cref="NetworkingFailures.ProtocolVersionUnsupported"/> failure
        /// on a non-overlapping range (section 10.3), never a raw exception.
        /// </summary>
        Task<Result<ConnectionHandle>> ConnectAsync(SessionEndpoint endpoint, ProtocolVersionRange clientProtocolRange, CancellationToken cancellationToken);

        Task<Result> SendReliableAsync(ConnectionHandle connection, NetworkEnvelope envelope, CancellationToken cancellationToken);

        Task<Result> SendRealtimeAsync(ConnectionHandle connection, RealtimeEnvelope envelope, CancellationToken cancellationToken);

        /// <summary>
        /// Returns every reliable envelope received since the last drain, in
        /// arrival order, and clears them from the inbox. Never blocks.
        /// </summary>
        Result<IReadOnlyList<NetworkEnvelope>> DrainReliable(ConnectionHandle connection);

        /// <summary>
        /// Returns every realtime envelope received since the last drain, in
        /// arrival order, and clears them from the inbox. Never blocks.
        /// </summary>
        Result<IReadOnlyList<RealtimeEnvelope>> DrainRealtime(ConnectionHandle connection);

        Result Disconnect(ConnectionHandle connection);
    }

    /// <summary>
    /// 06_Networking section 10.1/10.2: a monotonic integer, the same
    /// versioning style ADR-011 section 7 already established for
    /// CampaignFormatVersion ("monotonic integer, начиная с 1") -- not SemVer,
    /// since protocol compatibility here is range-overlap on a single integer
    /// axis, not major/minor/patch semantics.
    /// </summary>
    public readonly struct ProtocolVersion : IEquatable<ProtocolVersion>, IComparable<ProtocolVersion>
    {
        private readonly int _value;
        private ProtocolVersion(int value) => _value = value;
        public bool IsValid => _value > 0;
        public int Value => IsValid ? _value : throw new InvalidOperationException("ProtocolVersion is invalid.");
        public static ProtocolVersion Create(int value) => value > 0 ? new ProtocolVersion(value) : throw new ArgumentOutOfRangeException(nameof(value));
        public int CompareTo(ProtocolVersion other) => _value.CompareTo(other._value);
        public bool Equals(ProtocolVersion other) => _value == other._value;
        public override bool Equals(object? obj) => obj is ProtocolVersion other && Equals(other);
        public override int GetHashCode() => _value;
        public override string ToString() => IsValid ? _value.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        public static bool operator ==(ProtocolVersion left, ProtocolVersion right) => left.Equals(right);
        public static bool operator !=(ProtocolVersion left, ProtocolVersion right) => !left.Equals(right);
    }

    /// <summary>
    /// 06_Networking section 10.2: MinSupportedProtocolVersion/
    /// MaxSupportedProtocolVersion/PreferredProtocolVersion. A connection is
    /// possible only when two ranges overlap (section 10.2); the negotiated
    /// version is the highest value common to both ranges.
    /// </summary>
    public sealed class ProtocolVersionRange
    {
        public ProtocolVersionRange(ProtocolVersion min, ProtocolVersion max, ProtocolVersion preferred)
        {
            if (!min.IsValid) throw new ArgumentException("Min is required.", nameof(min));
            if (!max.IsValid) throw new ArgumentException("Max is required.", nameof(max));
            if (!preferred.IsValid) throw new ArgumentException("Preferred is required.", nameof(preferred));
            if (max.CompareTo(min) < 0) throw new ArgumentException("Max must be >= Min.", nameof(max));
            if (preferred.CompareTo(min) < 0 || preferred.CompareTo(max) > 0) throw new ArgumentException("Preferred must be within [Min, Max].", nameof(preferred));

            Min = min;
            Max = max;
            Preferred = preferred;
        }

        public ProtocolVersion Min { get; }
        public ProtocolVersion Max { get; }
        public ProtocolVersion Preferred { get; }

        /// <summary>
        /// The highest protocol version both ranges support, or null if the
        /// ranges do not overlap at all (06_Networking section 10.3: the
        /// connecting side then receives ConnectionRejected/
        /// ProtocolVersionUnsupported, never a snapshot).
        /// </summary>
        public ProtocolVersion? NegotiateWith(ProtocolVersionRange other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            int overlapMax = Math.Min(Max.Value, other.Max.Value);
            int overlapMin = Math.Max(Min.Value, other.Min.Value);
            return overlapMax >= overlapMin ? ProtocolVersion.Create(overlapMax) : (ProtocolVersion?)null;
        }
    }

    /// <summary>
    /// A stand-in for the destination this task's mock transport connects to.
    /// Deliberately minimal: a real transport's endpoint shape (host/port,
    /// relay session token, rendezvous descriptor, ...) is ODY-S02-002/003
    /// scope, not decided here.
    /// </summary>
    public sealed class SessionEndpoint
    {
        public SessionEndpoint(string endpointId)
        {
            if (string.IsNullOrWhiteSpace(endpointId) || endpointId.Length > 128) throw new ArgumentException("EndpointId is not safe.", nameof(endpointId));
            EndpointId = endpointId;
        }

        public string EndpointId { get; }
    }

    /// <summary>
    /// Application-safe handle to an established connection -- never exposes a
    /// raw socket/relay session object, the same "safe handle, not a live
    /// resource" pattern CampaignHandle already established for Persistence.
    /// </summary>
    public sealed class ConnectionHandle
    {
        public ConnectionHandle(SessionId sessionId, ProtocolVersion negotiatedProtocolVersion, UtcInstant connectedAt)
        {
            if (!sessionId.IsValid) throw new ArgumentException("SessionId is required.", nameof(sessionId));
            if (!negotiatedProtocolVersion.IsValid) throw new ArgumentException("NegotiatedProtocolVersion is required.", nameof(negotiatedProtocolVersion));

            SessionId = sessionId;
            NegotiatedProtocolVersion = negotiatedProtocolVersion;
            ConnectedAt = connectedAt;
        }

        public SessionId SessionId { get; }
        public ProtocolVersion NegotiatedProtocolVersion { get; }
        public UtcInstant ConnectedAt { get; }
    }

    public enum NetworkMessageKind
    {
        /// <summary>Protocol version handshake (06_Networking section 10).</summary>
        Handshake = 1,

        /// <summary>A generic Application-layer payload carried over the reliable channel; the specific meaning is carried by PayloadType/PayloadVersion, not this enum -- future protocol-owning ADRs (snapshot/delta, commands) are not required to add new NetworkMessageKind values for every payload shape.</summary>
        ApplicationPayload = 2,

        /// <summary>Connection liveness check.</summary>
        Heartbeat = 3,
    }

    /// <summary>
    /// 06_Networking section 11.1's NetworkEnvelope, field-for-field.
    /// </summary>
    public sealed class NetworkEnvelope
    {
        public NetworkEnvelope(
            MessageId messageId,
            SessionId sessionId,
            UserId? senderUserId,
            ClientInstanceId? senderClientInstanceId,
            NetworkMessageKind messageKind,
            ProtocolVersion protocolVersion,
            CorrelationId? correlationId,
            CausationCommandId? causationId,
            UtcInstant? sentAtHostTime,
            string payloadType,
            int payloadVersion,
            byte[] payload)
        {
            if (!messageId.IsValid) throw new ArgumentException("MessageId is required.", nameof(messageId));
            if (!sessionId.IsValid) throw new ArgumentException("SessionId is required.", nameof(sessionId));
            if (!Enum.IsDefined(typeof(NetworkMessageKind), messageKind)) throw new ArgumentOutOfRangeException(nameof(messageKind));
            if (!protocolVersion.IsValid) throw new ArgumentException("ProtocolVersion is required.", nameof(protocolVersion));
            if (string.IsNullOrWhiteSpace(payloadType) || payloadType.Length > 96) throw new ArgumentException("PayloadType is not safe.", nameof(payloadType));
            if (payloadVersion < 1) throw new ArgumentOutOfRangeException(nameof(payloadVersion));
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            MessageId = messageId;
            SessionId = sessionId;
            SenderUserId = senderUserId;
            SenderClientInstanceId = senderClientInstanceId;
            MessageKind = messageKind;
            ProtocolVersion = protocolVersion;
            CorrelationId = correlationId;
            CausationId = causationId;
            SentAtHostTime = sentAtHostTime;
            PayloadType = payloadType;
            PayloadVersion = payloadVersion;
            Payload = payload;
        }

        public MessageId MessageId { get; }
        public SessionId SessionId { get; }
        public UserId? SenderUserId { get; }
        public ClientInstanceId? SenderClientInstanceId { get; }
        public NetworkMessageKind MessageKind { get; }
        public ProtocolVersion ProtocolVersion { get; }
        public CorrelationId? CorrelationId { get; }
        public CausationCommandId? CausationId { get; }
        public UtcInstant? SentAtHostTime { get; }
        public string PayloadType { get; }
        public int PayloadVersion { get; }
        public byte[] Payload { get; }
    }

    /// <summary>
    /// 06_Networking section 5.2: transient, may be dropped, never persisted,
    /// never changes authoritative state -- so it carries no MessageId
    /// (nothing to deduplicate), CorrelationId, or CausationId.
    /// </summary>
    public sealed class RealtimeEnvelope
    {
        public RealtimeEnvelope(
            SessionId sessionId,
            UserId? senderUserId,
            ClientInstanceId? senderClientInstanceId,
            ProtocolVersion protocolVersion,
            string payloadType,
            byte[] payload)
        {
            if (!sessionId.IsValid) throw new ArgumentException("SessionId is required.", nameof(sessionId));
            if (!protocolVersion.IsValid) throw new ArgumentException("ProtocolVersion is required.", nameof(protocolVersion));
            if (string.IsNullOrWhiteSpace(payloadType) || payloadType.Length > 96) throw new ArgumentException("PayloadType is not safe.", nameof(payloadType));
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            SessionId = sessionId;
            SenderUserId = senderUserId;
            SenderClientInstanceId = senderClientInstanceId;
            ProtocolVersion = protocolVersion;
            PayloadType = payloadType;
            Payload = payload;
        }

        public SessionId SessionId { get; }
        public UserId? SenderUserId { get; }
        public ClientInstanceId? SenderClientInstanceId { get; }
        public ProtocolVersion ProtocolVersion { get; }
        public string PayloadType { get; }
        public byte[] Payload { get; }
    }

    /// <summary>
    /// Base timeout/retry defaults for connect/send operations, configurable
    /// per transport instance -- the same "sane default, constructor-injectable
    /// override" pattern BackupRotationPolicy already established.
    /// </summary>
    public sealed class TransportTimeoutPolicy
    {
        public static readonly TransportTimeoutPolicy Default = new TransportTimeoutPolicy(
            connectTimeout: TimeSpan.FromSeconds(10),
            sendTimeout: TimeSpan.FromSeconds(5),
            maxRetries: 3,
            retryBackoff: TimeSpan.FromMilliseconds(500));

        public TransportTimeoutPolicy(TimeSpan connectTimeout, TimeSpan sendTimeout, int maxRetries, TimeSpan retryBackoff)
        {
            if (connectTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(connectTimeout));
            if (sendTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(sendTimeout));
            if (maxRetries < 0) throw new ArgumentOutOfRangeException(nameof(maxRetries));
            if (retryBackoff < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(retryBackoff));

            ConnectTimeout = connectTimeout;
            SendTimeout = sendTimeout;
            MaxRetries = maxRetries;
            RetryBackoff = retryBackoff;
        }

        public TimeSpan ConnectTimeout { get; }
        public TimeSpan SendTimeout { get; }
        public int MaxRetries { get; }
        public TimeSpan RetryBackoff { get; }
    }

    public static class NetworkingFailures
    {
        public static Error ConnectFailed(CorrelationId correlationId) => Error.Create(
            ErrorCodes.NetworkingTransportConnectFailed,
            ErrorCategory.TransientInfrastructure,
            SafeReasonCode.ServiceUnavailable,
            UserMessageKey.Parse("errors.networking.transport_connect_failed"),
            RetryDirective.RetryWithBackoff,
            correlationId);

        public static Error ConnectTimedOut(CorrelationId correlationId) => Error.Create(
            ErrorCodes.NetworkingTransportConnectTimedOut,
            ErrorCategory.TransientInfrastructure,
            SafeReasonCode.OperationTimedOut,
            UserMessageKey.Parse("errors.networking.transport_connect_timed_out"),
            RetryDirective.RetryWithBackoff,
            correlationId);

        public static Error ProtocolVersionUnsupported(CorrelationId correlationId) => Error.Create(
            ErrorCodes.NetworkingProtocolVersionUnsupported,
            ErrorCategory.Compatibility,
            SafeReasonCode.VersionUnsupported,
            UserMessageKey.Parse("errors.networking.protocol_version_unsupported"),
            RetryDirective.UpgradeRequired,
            correlationId);

        public static Error SendFailed(CorrelationId correlationId) => Error.Create(
            ErrorCodes.NetworkingTransportSendFailed,
            ErrorCategory.TransientInfrastructure,
            SafeReasonCode.ServiceUnavailable,
            UserMessageKey.Parse("errors.networking.transport_send_failed"),
            RetryDirective.RetryWithBackoff,
            correlationId);

        public static Error NotConnected(CorrelationId correlationId) => Error.Create(
            ErrorCodes.NetworkingTransportNotConnected,
            ErrorCategory.Precondition,
            SafeReasonCode.ActionNotAllowed,
            UserMessageKey.Parse("errors.networking.transport_not_connected"),
            RetryDirective.ReconnectThenRetry,
            correlationId);

        public static Error OperationCancelled(CorrelationId correlationId) => Error.Create(
            ErrorCodes.NetworkingTransportOperationCancelled,
            ErrorCategory.Cancelled,
            SafeReasonCode.OperationCancelled,
            UserMessageKey.Parse("errors.networking.transport_operation_cancelled"),
            RetryDirective.DoNotRetry,
            correlationId);
    }
}
