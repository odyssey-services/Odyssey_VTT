using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Odyssey.Application.Commands;
using Odyssey.Application.Networking.Projection;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Serialization;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Networking.Command
{
    /// <summary>
    /// ODY-S02-011: host-side authoritative per-entity mutable field state
    /// (position + revision), kept separate from ODY-S02-010's Scene/
    /// SceneEntity (identity/visibility, immutable per snapshot build) --
    /// neither this task nor ODY-S02-010 introduces a full content/aggregate
    /// model yet. One instance per active session, in-memory only. Position
    /// reuses <see cref="TokenPosition"/> (Odyssey.Application.Persistence,
    /// ODY-S01-008) for semantic consistency with the existing persisted
    /// token-position shape -- without creating any dependency on
    /// CampaignHandle/SQLite (see this task's own decision log for why
    /// campaign persistence is not wired in at this prototype stage).
    /// </summary>
    public sealed class SceneMutableState
    {
        private readonly Dictionary<string, TokenPosition> _positions = new Dictionary<string, TokenPosition>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _revisions = new Dictionary<string, long>(StringComparer.Ordinal);
        private long _sequence;

        public SceneMutableState(Scene scene, IReadOnlyDictionary<string, TokenPosition>? initialPositions = null)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));

            foreach (SceneEntity entity in scene.Entities)
            {
                TokenPosition initial = initialPositions != null && initialPositions.TryGetValue(entity.EntityId, out TokenPosition provided) ? provided : new TokenPosition(0, 0);
                _positions[entity.EntityId] = initial;
                _revisions[entity.EntityId] = 1;
            }
        }

        public bool TryGetState(string entityId, out TokenPosition position, out long revision)
        {
            if (_positions.TryGetValue(entityId, out position) && _revisions.TryGetValue(entityId, out revision)) return true;
            position = default;
            revision = 0;
            return false;
        }

        internal (TokenPosition Position, long Revision) ApplyMove(string entityId, TokenPosition newPosition)
        {
            long newRevision = _revisions[entityId] + 1;
            _positions[entityId] = newPosition;
            _revisions[entityId] = newRevision;
            return (newPosition, newRevision);
        }

        internal long NextSequence() => ++_sequence;
    }

    /// <summary>
    /// Combines ODY-S02-010's identity/visibility Scene with this task's
    /// mutable field state and an in-memory command-receipt store -- this
    /// task's own, explicitly non-durable stand-in for ADR-002 section 4.4's
    /// AppliedCommands (see decision log: no crash-recoverable persistence at
    /// this prototype stage, an open question for future slice integration).
    /// </summary>
    public sealed class TokenMoveSessionState
    {
        private readonly Dictionary<string, (string Fingerprint, Result<TokenMoveOutcome> Result)> _receipts = new Dictionary<string, (string, Result<TokenMoveOutcome>)>(StringComparer.Ordinal);

        public TokenMoveSessionState(Scene scene, SceneMutableState mutableState)
        {
            Scene = scene ?? throw new ArgumentNullException(nameof(scene));
            MutableState = mutableState ?? throw new ArgumentNullException(nameof(mutableState));
        }

        public Scene Scene { get; }
        public SceneMutableState MutableState { get; }

        internal bool TryGetReceipt(CommandId commandId, out string fingerprint, out Result<TokenMoveOutcome> result)
        {
            if (_receipts.TryGetValue(commandId.ToString(), out (string Fingerprint, Result<TokenMoveOutcome> Result) entry))
            {
                fingerprint = entry.Fingerprint;
                result = entry.Result;
                return true;
            }

            fingerprint = string.Empty;
            result = default!;
            return false;
        }

        internal void StoreReceipt(CommandId commandId, string fingerprint, Result<TokenMoveOutcome> result) => _receipts[commandId.ToString()] = (fingerprint, result);
    }

    /// <summary>
    /// Roadmap 11.6 step 5: a player-issued token-move intent. CommandId
    /// (Odyssey.Application.Commands, ADR-002 section 6.2) is reused directly
    /// as the canonical idempotency key -- this task's own MoveTokenService
    /// pipeline follows ADR-002 section 11's ordering (duplicate check ->
    /// action-check -> load/validate -> mutate), but does not instantiate
    /// ADR-002's CommandExecutor/DomainEventBatch machinery, which is
    /// coupled to a persisted CampaignId this network-only prototype does
    /// not have (see decision log).
    /// </summary>
    public sealed class MoveTokenCommand
    {
        public MoveTokenCommand(CommandId commandId, SessionId sessionId, UserId actorUserId, string entityId, TokenPosition destination, long expectedRevision)
        {
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (!sessionId.IsValid) throw new ArgumentException("SessionId is required.", nameof(sessionId));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (string.IsNullOrWhiteSpace(entityId) || entityId.Length > 64) throw new ArgumentException("EntityId is not safe.", nameof(entityId));
            if (expectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedRevision));

            CommandId = commandId;
            SessionId = sessionId;
            ActorUserId = actorUserId;
            EntityId = entityId;
            Destination = destination;
            ExpectedRevision = expectedRevision;
        }

        public CommandId CommandId { get; }
        public SessionId SessionId { get; }
        public UserId ActorUserId { get; }
        public string EntityId { get; }
        public TokenPosition Destination { get; }
        public long ExpectedRevision { get; }
    }

    public sealed class TokenMoveOutcome
    {
        public TokenMoveOutcome(string entityId, TokenPosition position, long revision)
        {
            if (string.IsNullOrWhiteSpace(entityId)) throw new ArgumentException("EntityId is required.", nameof(entityId));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
            EntityId = entityId;
            Position = position;
            Revision = revision;
        }

        public string EntityId { get; }
        public TokenPosition Position { get; }
        public long Revision { get; }
    }

    public static class TokenMoveFailures
    {
        public static Error EntityNotFound(CorrelationId correlationId) => Error.Create(
            ErrorCodes.NetworkingCommandTokenNotFound,
            ErrorCategory.NotFound,
            SafeReasonCode.TargetUnavailable,
            UserMessageKey.Parse("errors.networking.command_token_not_found"),
            RetryDirective.DoNotRetry,
            correlationId);

        public static Error ActionNotAllowed(CorrelationId correlationId) => Error.Create(
            ErrorCodes.NetworkingCommandTokenMoveDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.networking.command_token_move_denied"),
            RetryDirective.DoNotRetry,
            correlationId);

        public static Error RevisionConflict(CorrelationId correlationId) => Error.Create(
            ErrorCodes.NetworkingCommandTokenRevisionConflict,
            ErrorCategory.Conflict,
            SafeReasonCode.StateChanged,
            UserMessageKey.Parse("errors.networking.command_token_revision_conflict"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>
        /// Same ErrorCode/SafeReasonCode/UserMessageKey ADR-002's own
        /// CommandExecutor.CreateIdentityMismatch already registers
        /// (Odyssey.Application.Commands.CommandContracts.cs) -- reused by
        /// value since that helper is private to CommandExecutor, not because
        /// a new registry entry is needed (section 9.3: same CommandId,
        /// different fingerprint).
        /// </summary>
        public static Error CommandIdentityMismatch(CorrelationId correlationId) => Error.Create(
            ErrorCodes.CommandIdentityMismatch,
            ErrorCategory.Security,
            SafeReasonCode.ActionNotAllowed,
            UserMessageKey.Parse("errors.application.command_identity_mismatch"),
            RetryDirective.DoNotRetry,
            correlationId);
    }

    /// <summary>
    /// Roadmap 11.6 steps 5-6: validates a MoveToken intent entirely
    /// host-side, following ADR-002 section 11's ordering narrowed to what
    /// this in-memory prototype needs: duplicate-CommandId replay/mismatch
    /// (section 9.2/9.3) -&gt; action-check at submission (ADR-019 section
    /// 6.1) -&gt; load current state -&gt; expected-revision check (section
    /// 10.2) -&gt; action-check again, immediately before commit (ADR-019
    /// section 6.1's two-point rule) -&gt; atomic in-memory mutation.
    /// </summary>
    public static class MoveTokenService
    {
        private static readonly CorrelationId PlaceholderCorrelationId = CorrelationId.Parse("corr_00000000000000000000000000000000");

        public static Result<TokenMoveOutcome> Execute(TokenMoveSessionState state, SessionAdmissionState admission, MoveTokenCommand command)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (admission == null) throw new ArgumentNullException(nameof(admission));
            if (command == null) throw new ArgumentNullException(nameof(command));

            string fingerprint = ComputeFingerprint(command);

            if (state.TryGetReceipt(command.CommandId, out string existingFingerprint, out Result<TokenMoveOutcome> existingResult))
            {
                return existingFingerprint == fingerprint ? existingResult : Result<TokenMoveOutcome>.Failure(TokenMoveFailures.CommandIdentityMismatch(PlaceholderCorrelationId));
            }

            Result submissionCheck = CheckAuthorization(state.Scene, admission, command);
            if (submissionCheck.IsFailure) return Reject(state, command, fingerprint, submissionCheck.Error);

            if (!state.MutableState.TryGetState(command.EntityId, out _, out long currentRevision)) return Reject(state, command, fingerprint, TokenMoveFailures.EntityNotFound(PlaceholderCorrelationId));

            if (currentRevision != command.ExpectedRevision) return Reject(state, command, fingerprint, TokenMoveFailures.RevisionConflict(PlaceholderCorrelationId));

            // ADR-019 section 6.1: repeated immediately before commit. In this
            // synchronous, single-threaded, in-memory prototype there is no
            // real intervening concurrency window, but the check is still
            // performed a second, independent time to honor the ADR's
            // structural two-point rule (submission + pre-commit), not
            // collapsed into a single call.
            Result preCommitCheck = CheckAuthorization(state.Scene, admission, command);
            if (preCommitCheck.IsFailure) return Reject(state, command, fingerprint, preCommitCheck.Error);

            (TokenPosition position, long revision) = state.MutableState.ApplyMove(command.EntityId, command.Destination);
            Result<TokenMoveOutcome> accepted = Result<TokenMoveOutcome>.Success(new TokenMoveOutcome(command.EntityId, position, revision));
            state.StoreReceipt(command.CommandId, fingerprint, accepted);
            return accepted;
        }

        private static Result<TokenMoveOutcome> Reject(TokenMoveSessionState state, MoveTokenCommand command, string fingerprint, Error error)
        {
            Result<TokenMoveOutcome> rejected = Result<TokenMoveOutcome>.Failure(error);
            state.StoreReceipt(command.CommandId, fingerprint, rejected);
            return rejected;
        }

        private static Result CheckAuthorization(Scene scene, SessionAdmissionState admission, MoveTokenCommand command)
        {
            if (!admission.Members.TryGetValue(command.ActorUserId.ToString(), out SessionMember? actor))
            {
                return Result.Failure(TokenMoveFailures.ActionNotAllowed(PlaceholderCorrelationId));
            }

            if (actor.Role == BaselineRole.MainGM) return Result.Success();
            if (actor.Role != BaselineRole.Player) return Result.Failure(TokenMoveFailures.ActionNotAllowed(PlaceholderCorrelationId));

            foreach (SceneEntity entity in scene.Entities)
            {
                if (string.Equals(entity.EntityId, command.EntityId, StringComparison.Ordinal))
                {
                    bool ownsEntity = entity.AssignedToUserId.HasValue && entity.AssignedToUserId.Value.Equals(command.ActorUserId);
                    return ownsEntity ? Result.Success() : Result.Failure(TokenMoveFailures.ActionNotAllowed(PlaceholderCorrelationId));
                }
            }

            // Entity does not exist -- ADR-002 section 10.3: a safe error must
            // not confirm the existence of a never-visible entity, so this
            // returns the same TargetUnavailable code the post-authorization
            // "entity not found" path below also uses, not a distinguishable one.
            return Result.Failure(TokenMoveFailures.EntityNotFound(PlaceholderCorrelationId));
        }

        private static string ComputeFingerprint(MoveTokenCommand command)
        {
            string seed = command.SessionId + "|" + command.ActorUserId + "|" + command.EntityId + "|" +
                command.Destination.X.ToString(CultureInfo.InvariantCulture) + "|" + command.Destination.Y.ToString(CultureInfo.InvariantCulture) + "|" + command.ExpectedRevision.ToString(CultureInfo.InvariantCulture);
            return CanonicalJson.Sha256LowerHex(Encoding.UTF8.GetBytes(seed));
        }
    }

    /// <summary>
    /// ADR-017 section 5's minimal PatchFields case: a single field-patch
    /// operation (position) addressed to one audience, flattened directly
    /// onto the batch since exactly one operation exists per accepted move.
    /// The general Operations[] list, other operation kinds, gap detection,
    /// and dedup-by-range (ADR-017 sections 5-8) are ODY-S02-012 scope.
    /// </summary>
    public sealed class TokenMovedDelta
    {
        public TokenMovedDelta(SessionId sessionId, UserId audienceUserId, long sequenceFrom, long sequenceTo, string entityId, TokenPosition position, long entityRevision, UtcInstant occurredAtHost)
        {
            if (!sessionId.IsValid) throw new ArgumentException("SessionId is required.", nameof(sessionId));
            if (!audienceUserId.IsValid) throw new ArgumentException("AudienceUserId is required.", nameof(audienceUserId));
            if (sequenceFrom < 1 || sequenceTo < sequenceFrom) throw new ArgumentOutOfRangeException(nameof(sequenceTo));
            if (string.IsNullOrWhiteSpace(entityId)) throw new ArgumentException("EntityId is required.", nameof(entityId));
            if (entityRevision < 1) throw new ArgumentOutOfRangeException(nameof(entityRevision));
            if (!occurredAtHost.IsValid) throw new ArgumentException("OccurredAtHost is required.", nameof(occurredAtHost));

            SessionId = sessionId;
            AudienceUserId = audienceUserId;
            SequenceFrom = sequenceFrom;
            SequenceTo = sequenceTo;
            EntityId = entityId;
            Position = position;
            EntityRevision = entityRevision;
            OccurredAtHost = occurredAtHost;
        }

        public SessionId SessionId { get; }
        public UserId AudienceUserId { get; }
        public long SequenceFrom { get; }
        public long SequenceTo { get; }
        public string EntityId { get; }
        public TokenPosition Position { get; }
        public long EntityRevision { get; }
        public UtcInstant OccurredAtHost { get; }
    }

    /// <summary>
    /// ADR-019 section 6.2/section 7: redaction computed here, in
    /// Odyssey.Application, before any payload reaches Odyssey.Networking.
    /// Reuses ODY-S02-010's VisibilityPolicy public API, unmodified -- an
    /// audience whose role cannot see the moved entity receives no delta for
    /// it at all, not a delta with the operation filtered out.
    /// </summary>
    public static class DeltaBroadcastPlanner
    {
        public static IReadOnlyList<TokenMovedDelta> PlanBroadcast(Scene scene, SessionAdmissionState admission, SceneMutableState mutableState, TokenMoveOutcome outcome, IWallClock clock)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            if (admission == null) throw new ArgumentNullException(nameof(admission));
            if (mutableState == null) throw new ArgumentNullException(nameof(mutableState));
            if (outcome == null) throw new ArgumentNullException(nameof(outcome));
            if (clock == null) throw new ArgumentNullException(nameof(clock));

            List<TokenMovedDelta> deltas = new List<TokenMovedDelta>();
            UtcInstant now = clock.GetUtcNow();
            SessionId sessionId = admission.Directory.SessionId;

            foreach (SessionMember member in admission.Members.Values)
            {
                ActorVisibilityContext context = new ActorVisibilityContext(member.UserId, member.Role);
                bool visible = VisibilityPolicy.ComputeVisibleEntities(scene, context).Any(entity => string.Equals(entity.EntityId, outcome.EntityId, StringComparison.Ordinal));
                if (!visible) continue;

                long sequence = mutableState.NextSequence();
                deltas.Add(new TokenMovedDelta(sessionId, member.UserId, sequence, sequence, outcome.EntityId, outcome.Position, outcome.Revision, now));
            }

            return deltas;
        }
    }
}
