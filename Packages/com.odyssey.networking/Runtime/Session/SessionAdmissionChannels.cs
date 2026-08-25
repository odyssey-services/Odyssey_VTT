using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Odyssey.Application.Networking;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Networking.Session
{
    /// <summary>
    /// ODY-S02-009: the real (not test-only) Networking-layer adapter that
    /// carries the Application-layer admission/lobby messages
    /// (SessionAdmissionWireCodecs) over ISessionTransport's reliable channel
    /// -- ADR-015 section 5.1, ADR-001 section 6.6's "mapping wire messages
    /// to Application contracts" boundary. This module never makes an
    /// admission decision itself (that stays in
    /// Odyssey.Application.Networking.Session.SessionAdmissionService) -- it
    /// only encodes, sends, receives, and decodes.
    /// </summary>
    public static class SessionAdmissionClientChannel
    {
        public static async Task<Result> SendJoinRequestAsync(ISessionTransport transport, ConnectionHandle handle, JoinCode joinCode, UserId userId, IWallClock clock, CancellationToken cancellationToken)
        {
            Result<byte[]> payload = SessionAdmissionWireCodec.WriteJoinRequest(new JoinRequestMessage(joinCode.ToString(), userId.ToString()));
            if (payload.IsFailure) return Result.Failure(payload.Error);
            NetworkEnvelope envelope = MakeEnvelope(handle, "odyssey.session.join_request", payload.Value, clock);
            return await transport.SendReliableAsync(handle, envelope, cancellationToken);
        }

        public static async Task<Result> SendRoleAssignmentRequestAsync(ISessionTransport transport, ConnectionHandle handle, UserId requestingUserId, UserId targetUserId, BaselineRole role, IWallClock clock, CancellationToken cancellationToken)
        {
            Result<byte[]> payload = SessionAdmissionWireCodec.WriteRoleAssignmentRequest(new RoleAssignmentRequestMessage(requestingUserId.ToString(), targetUserId.ToString(), role.ToString()));
            if (payload.IsFailure) return Result.Failure(payload.Error);
            NetworkEnvelope envelope = MakeEnvelope(handle, "odyssey.session.role_assignment_request", payload.Value, clock);
            return await transport.SendReliableAsync(handle, envelope, cancellationToken);
        }

        /// <summary>
        /// Drains and decodes every admission outcome received since the last
        /// drain, in arrival order. Returns an empty list (inside a success)
        /// when nothing has arrived yet -- never blocks, matching
        /// ISessionTransport.DrainReliable's own never-null, empty-means-
        /// nothing-yet contract.
        /// </summary>
        public static Result<IReadOnlyList<AdmissionOutcomeMessage>> DrainOutcomes(ISessionTransport transport, ConnectionHandle handle)
        {
            Result<IReadOnlyList<NetworkEnvelope>> drained = transport.DrainReliable(handle);
            if (drained.IsFailure) return Result<IReadOnlyList<AdmissionOutcomeMessage>>.Failure(drained.Error);

            List<AdmissionOutcomeMessage> outcomes = new List<AdmissionOutcomeMessage>();
            foreach (NetworkEnvelope envelope in drained.Value)
            {
                if (envelope.PayloadType != "odyssey.session.admission_outcome") continue;
                Result<AdmissionOutcomeMessage> outcome = SessionAdmissionWireCodec.ReadOutcome(envelope.Payload);
                if (outcome.IsFailure) return Result<IReadOnlyList<AdmissionOutcomeMessage>>.Failure(outcome.Error);
                outcomes.Add(outcome.Value);
            }

            return Result<IReadOnlyList<AdmissionOutcomeMessage>>.Success(outcomes);
        }

        private static NetworkEnvelope MakeEnvelope(ConnectionHandle handle, string payloadType, byte[] payload, IWallClock clock) =>
            new NetworkEnvelope(
                MessageId.NewId(clock.GetUtcNow()),
                handle.SessionId,
                senderUserId: null,
                senderClientInstanceId: null,
                NetworkMessageKind.ApplicationPayload,
                handle.NegotiatedProtocolVersion,
                correlationId: null,
                causationId: null,
                sentAtHostTime: clock.GetUtcNow(),
                payloadType,
                payloadVersion: 1,
                payload: payload);
    }

    public static class SessionAdmissionHostChannel
    {
        /// <summary>
        /// Drains every pending request envelope, applies it against the
        /// authoritative SessionAdmissionState (SessionAdmissionService --
        /// the only place an admission decision is made), and sends back a
        /// typed outcome for each. Returns the count processed.
        /// </summary>
        public static async Task<Result<int>> ProcessPendingRequestsAsync(ISessionTransport transport, ConnectionHandle handle, SessionAdmissionState state, IWallClock clock, CancellationToken cancellationToken)
        {
            Result<IReadOnlyList<NetworkEnvelope>> drained = transport.DrainReliable(handle);
            if (drained.IsFailure) return Result<int>.Failure(drained.Error);

            int processed = 0;
            foreach (NetworkEnvelope envelope in drained.Value)
            {
                AdmissionOutcomeMessage outcome;
                switch (envelope.PayloadType)
                {
                    case "odyssey.session.join_request":
                        outcome = HandleJoinRequest(state, envelope.Payload);
                        break;
                    case "odyssey.session.role_assignment_request":
                        outcome = HandleRoleAssignmentRequest(state, envelope.Payload);
                        break;
                    default:
                        continue;
                }

                Result<byte[]> encoded = SessionAdmissionWireCodec.WriteOutcome(outcome);
                if (encoded.IsFailure) return Result<int>.Failure(encoded.Error);
                NetworkEnvelope response = new NetworkEnvelope(
                    MessageId.NewId(clock.GetUtcNow()),
                    handle.SessionId,
                    senderUserId: null,
                    senderClientInstanceId: null,
                    NetworkMessageKind.ApplicationPayload,
                    handle.NegotiatedProtocolVersion,
                    correlationId: null,
                    causationId: null,
                    sentAtHostTime: clock.GetUtcNow(),
                    "odyssey.session.admission_outcome",
                    payloadVersion: 1,
                    payload: encoded.Value);
                Result sendResult = await transport.SendReliableAsync(handle, response, cancellationToken);
                if (sendResult.IsFailure) return Result<int>.Failure(sendResult.Error);
                processed++;
            }

            return Result<int>.Success(processed);
        }

        private static AdmissionOutcomeMessage HandleJoinRequest(SessionAdmissionState state, byte[] payload)
        {
            Result<JoinRequestMessage> request = SessionAdmissionWireCodec.ReadJoinRequest(payload);
            if (request.IsFailure) return AdmissionOutcomeMessage.FromFailure(request.Error);
            if (!JoinCode.TryParse(request.Value.JoinCode, out JoinCode joinCode)) return AdmissionOutcomeMessage.FromFailure(SessionAdmissionFailures.JoinCodeInvalid(PlaceholderCorrelationId()));
            if (!UserId.TryParse(request.Value.UserId, out UserId userId)) return AdmissionOutcomeMessage.FromFailure(SessionAdmissionFailures.JoinCodeInvalid(PlaceholderCorrelationId()));

            Result<SessionMember> result = SessionAdmissionService.TryJoin(state, joinCode, userId);
            return result.IsSuccess ? AdmissionOutcomeMessage.FromSuccess(result.Value) : AdmissionOutcomeMessage.FromFailure(result.Error);
        }

        private static AdmissionOutcomeMessage HandleRoleAssignmentRequest(SessionAdmissionState state, byte[] payload)
        {
            Result<RoleAssignmentRequestMessage> request = SessionAdmissionWireCodec.ReadRoleAssignmentRequest(payload);
            if (request.IsFailure) return AdmissionOutcomeMessage.FromFailure(request.Error);
            if (!UserId.TryParse(request.Value.RequestingUserId, out UserId requestingUserId)) return AdmissionOutcomeMessage.FromFailure(SessionAdmissionFailures.RoleAssignmentDenied(PlaceholderCorrelationId()));
            if (!UserId.TryParse(request.Value.TargetUserId, out UserId targetUserId)) return AdmissionOutcomeMessage.FromFailure(SessionAdmissionFailures.RoleAssignmentDenied(PlaceholderCorrelationId()));
            if (!Enum.TryParse(request.Value.Role, out BaselineRole role)) return AdmissionOutcomeMessage.FromFailure(SessionAdmissionFailures.RoleAssignmentDenied(PlaceholderCorrelationId()));

            Result<SessionMember> result = SessionAdmissionService.AssignRole(state, requestingUserId, targetUserId, role);
            return result.IsSuccess ? AdmissionOutcomeMessage.FromSuccess(result.Value) : AdmissionOutcomeMessage.FromFailure(result.Error);
        }

        private static CorrelationId PlaceholderCorrelationId() => CorrelationId.Parse("corr_00000000000000000000000000000000");
    }
}
