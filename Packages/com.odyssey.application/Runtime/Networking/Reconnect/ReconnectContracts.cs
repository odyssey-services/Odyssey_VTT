using System;
using System.Collections.Generic;
using System.Linq;
using Odyssey.Application.Networking.Projection;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Networking.Reconnect
{
    /// <summary>
    /// ODY-S02-012: one committed token-move event, recorded host-side for
    /// gap-repair/reconnect catch-up (ADR-017 section 8). Redaction is
    /// deliberately NOT baked in at record time -- <see cref="VisibilityPolicy"/>
    /// is re-applied against an audience's CURRENT role/assignment when a
    /// buffered entry is replayed (ADR-017 section 1 point 8: redaction
    /// always by current, not saved, permissions), never cached from the
    /// moment the move happened.
    /// </summary>
    public sealed class BufferedDelta
    {
        public BufferedDelta(long bufferSequence, string entityId, TokenPosition position, long entityRevision, UtcInstant occurredAtHost)
        {
            if (bufferSequence < 1) throw new ArgumentOutOfRangeException(nameof(bufferSequence));
            if (string.IsNullOrWhiteSpace(entityId)) throw new ArgumentException("EntityId is required.", nameof(entityId));
            if (entityRevision < 1) throw new ArgumentOutOfRangeException(nameof(entityRevision));
            if (!occurredAtHost.IsValid) throw new ArgumentException("OccurredAtHost is required.", nameof(occurredAtHost));

            BufferSequence = bufferSequence;
            EntityId = entityId;
            Position = position;
            EntityRevision = entityRevision;
            OccurredAtHost = occurredAtHost;
        }

        public long BufferSequence { get; }
        public string EntityId { get; }
        public TokenPosition Position { get; }
        public long EntityRevision { get; }
        public UtcInstant OccurredAtHost { get; }
    }

    /// <summary>
    /// ADR-017 section 8: a bounded (fixed-capacity, oldest-evicted-first)
    /// host-side buffer of recent committed changes, shared across every
    /// audience of one session -- not one buffer per connection. Capacity is
    /// this task's own implementation parameter (ADR-017 section 8 point 4
    /// leaves the exact number unfixed, deliberately); see the task contract
    /// decision log for the chosen value and rationale.
    /// </summary>
    public sealed class SessionDeltaBuffer
    {
        public const int DefaultCapacity = 3;
        private readonly int _capacity;
        private readonly LinkedList<BufferedDelta> _entries = new LinkedList<BufferedDelta>();
        private long _nextSequence = 1;

        public SessionDeltaBuffer(int capacity = DefaultCapacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        /// <summary>The most recent sequence still known to the buffer's owner, whether or not it was evicted from the buffer itself.</summary>
        public long LatestSequence => _nextSequence - 1;

        public BufferedDelta Record(string entityId, TokenPosition position, long entityRevision, UtcInstant occurredAtHost)
        {
            BufferedDelta entry = new BufferedDelta(_nextSequence, entityId, position, entityRevision, occurredAtHost);
            _nextSequence++;
            _entries.AddLast(entry);
            while (_entries.Count > _capacity) _entries.RemoveFirst();
            return entry;
        }

        /// <summary>
        /// Returns every buffered entry after <paramref name="fromExclusiveSequence"/>
        /// when the buffer still holds the full missed range; returns false
        /// (fallback required, ADR-017 section 8) when any part of that
        /// range has already been evicted.
        /// </summary>
        public bool TryGetRangeSince(long fromExclusiveSequence, out IReadOnlyList<BufferedDelta> entries)
        {
            if (fromExclusiveSequence >= LatestSequence)
            {
                entries = Array.Empty<BufferedDelta>();
                return true;
            }

            long oldestBuffered = _entries.Count == 0 ? _nextSequence : _entries.First!.Value.BufferSequence;
            if (fromExclusiveSequence < oldestBuffered - 1)
            {
                entries = Array.Empty<BufferedDelta>();
                return false;
            }

            List<BufferedDelta> matched = new List<BufferedDelta>();
            foreach (BufferedDelta entry in _entries)
            {
                if (entry.BufferSequence > fromExclusiveSequence) matched.Add(entry);
            }

            entries = matched;
            return true;
        }
    }

    /// <summary>
    /// Combines ODY-S02-010's Scene (identity/visibility) and ODY-S02-011's
    /// SceneMutableState (position/revision) with this task's session-wide
    /// delta buffer and per-audience LastAcknowledgedSequence bookkeeping --
    /// the host-side state a reconnect flow needs, all in-memory, matching
    /// the same non-durable prototype level as ODY-S02-009 through 011.
    /// </summary>
    public sealed class ReconnectSessionState
    {
        private readonly Dictionary<string, long> _lastAcknowledged = new Dictionary<string, long>(StringComparer.Ordinal);

        public ReconnectSessionState(Scene scene, SessionDeltaBuffer buffer)
        {
            Scene = scene ?? throw new ArgumentNullException(nameof(scene));
            Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        }

        public Scene Scene { get; }
        public SessionDeltaBuffer Buffer { get; }

        public long GetLastAcknowledged(UserId userId) => _lastAcknowledged.TryGetValue(userId.ToString(), out long sequence) ? sequence : 0;
        public void SetLastAcknowledged(UserId userId, long sequence) => _lastAcknowledged[userId.ToString()] = sequence;
    }

    public enum ReconnectPathKind
    {
        DeltaCatchup = 1,
        SnapshotFallback = 2
    }

    /// <summary>
    /// ADR-017 section 9 steps 6-7's decision, computed once: either buffered
    /// delta catch-up (already redacted, ADR-017 section 1 point 8) or a full
    /// re-resolved ProjectionSnapshot (ODY-S02-010, unmodified) -- never both,
    /// never neither.
    /// </summary>
    public sealed class ReconnectPlan
    {
        private ReconnectPlan(ReconnectPathKind kind, IReadOnlyList<BufferedDelta> catchupEntries, ProjectionSnapshot? fallbackSnapshot)
        {
            Kind = kind;
            CatchupEntries = catchupEntries;
            FallbackSnapshot = fallbackSnapshot;
        }

        public ReconnectPathKind Kind { get; }
        public IReadOnlyList<BufferedDelta> CatchupEntries { get; }
        public ProjectionSnapshot? FallbackSnapshot { get; }

        public static ReconnectPlan DeltaCatchup(IReadOnlyList<BufferedDelta> entries) => new ReconnectPlan(ReconnectPathKind.DeltaCatchup, entries, null);
        public static ReconnectPlan SnapshotFallback(ProjectionSnapshot snapshot) => new ReconnectPlan(ReconnectPathKind.SnapshotFallback, Array.Empty<BufferedDelta>(), snapshot);
    }

    /// <summary>
    /// ADR-017 section 9 (10-step reconnect flow), narrowed to steps 4-7
    /// (this task's scope; authentication/session/membership binding is
    /// already ODY-S02-009's admission, asset-manifest-diff/scene-load/ready
    /// are client-side UI concerns not modeled here). Step 5 ("пересчёт
    /// текущих permissions") is structural: <see cref="ActorVisibilityContext"/>
    /// is built fresh from the admission state's CURRENT SessionMember on
    /// every call, never from a value cached at disconnect time.
    /// </summary>
    public static class ReconnectPlanner
    {
        public static Result<ReconnectPlan> Plan(ReconnectSessionState state, SessionAdmissionState admission, UserId audienceUserId, SessionId sessionId, IWallClock clock)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (admission == null) throw new ArgumentNullException(nameof(admission));
            if (clock == null) throw new ArgumentNullException(nameof(clock));

            if (!admission.Members.TryGetValue(audienceUserId.ToString(), out SessionMember? member))
            {
                return Result<ReconnectPlan>.Failure(SessionAdmissionFailures.MemberNotFound(CorrelationId.Parse("corr_00000000000000000000000000000000")));
            }

            ActorVisibilityContext context = new ActorVisibilityContext(audienceUserId, member.Role);
            long lastAcknowledged = state.GetLastAcknowledged(audienceUserId);

            if (state.Buffer.TryGetRangeSince(lastAcknowledged, out IReadOnlyList<BufferedDelta> missed))
            {
                HashSet<string> visibleIds = new HashSet<string>(VisibilityPolicy.ComputeVisibleEntities(state.Scene, context).Select(entity => entity.EntityId), StringComparer.Ordinal);
                List<BufferedDelta> visibleMissed = missed.Where(entry => visibleIds.Contains(entry.EntityId)).ToList();
                return Result<ReconnectPlan>.Success(ReconnectPlan.DeltaCatchup(visibleMissed));
            }

            ProjectionSnapshot snapshot = SceneProjectionBuilder.BuildSnapshot(sessionId, state.Scene, context, state.Buffer.LatestSequence, projectionRevision: 1, permissionRevision: 1, clock);
            return Result<ReconnectPlan>.Success(ReconnectPlan.SnapshotFallback(snapshot));
        }
    }

    /// <summary>
    /// Records a committed move into the session-wide buffer, then plans
    /// immediate delivery only to audiences that are both entitled (per
    /// current VisibilityPolicy) and currently connected -- an entitled but
    /// disconnected audience is skipped here and catches up later via
    /// <see cref="ReconnectPlanner"/>, never sent to directly.
    /// </summary>
    public static class ContinuityBroadcastPlanner
    {
        public static IReadOnlyList<(UserId Audience, BufferedDelta Entry)> RecordAndPlanImmediateBroadcast(ReconnectSessionState state, SessionAdmissionState admission, string entityId, TokenPosition position, long entityRevision, ISet<UserId> connectedAudiences, IWallClock clock)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (admission == null) throw new ArgumentNullException(nameof(admission));
            if (connectedAudiences == null) throw new ArgumentNullException(nameof(connectedAudiences));
            if (clock == null) throw new ArgumentNullException(nameof(clock));

            BufferedDelta entry = state.Buffer.Record(entityId, position, entityRevision, clock.GetUtcNow());

            List<(UserId, BufferedDelta)> targets = new List<(UserId, BufferedDelta)>();
            foreach (SessionMember member in admission.Members.Values)
            {
                ActorVisibilityContext context = new ActorVisibilityContext(member.UserId, member.Role);
                bool visible = VisibilityPolicy.ComputeVisibleEntities(state.Scene, context).Any(candidate => string.Equals(candidate.EntityId, entry.EntityId, StringComparison.Ordinal));
                if (!visible) continue;
                if (!connectedAudiences.Contains(member.UserId)) continue;

                targets.Add((member.UserId, entry));
            }

            return targets;
        }
    }

    /// <summary>
    /// ODY-S02-012's minimal client-side runtime projection: tracks the
    /// highest BufferSequence actually applied, so a redelivered (duplicate)
    /// range is detected and ignored (ADR-017 section 6) rather than
    /// reapplied. Not the full client runtime -- just enough surface for
    /// this task's tests to assert convergence and dedup.
    /// </summary>
    public sealed class ClientProjectionState
    {
        private readonly Dictionary<string, TokenPosition> _positions = new Dictionary<string, TokenPosition>(StringComparer.Ordinal);

        public long LastAppliedSequence { get; private set; }

        public IReadOnlyDictionary<string, TokenPosition> Positions => _positions;

        /// <summary>Returns true if the entry was newly applied, false if it was a duplicate/already-applied range and was ignored.</summary>
        public bool TryApply(long bufferSequence, string entityId, TokenPosition position)
        {
            if (bufferSequence <= LastAppliedSequence) return false;

            _positions[entityId] = position;
            LastAppliedSequence = bufferSequence;
            return true;
        }

        public void ApplySnapshot(ProjectionSnapshot snapshot, long resumeSequence)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            _positions.Clear();
            foreach (SceneEntity entity in snapshot.VisibleEntities)
            {
                _positions[entity.EntityId] = default;
            }

            LastAppliedSequence = resumeSequence;
        }
    }
}
