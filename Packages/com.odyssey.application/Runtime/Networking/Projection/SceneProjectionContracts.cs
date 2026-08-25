using System;
using System.Collections.Generic;
using System.Text;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Serialization;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Networking.Projection
{
    /// <summary>
    /// ODY-S02-010: the minimal entity classification this task needs to
    /// prove roadmap 11.6 step 4 -- not a full game-content model
    /// (SLICE-03+ scope). "Public" is visible to every baseline role
    /// including Observer; "HiddenGameplay" is GM-only unless the entity is
    /// explicitly assigned to the viewing Player (ADR-019 section 5.2/5.3).
    /// </summary>
    public enum SceneEntityVisibility
    {
        Public = 1,
        HiddenGameplay = 2
    }

    /// <summary>
    /// A single scene entity in the host's authoritative state. "Assigned to"
    /// a UserId is this task's minimal stand-in for ADR-019 section 7's
    /// "character-assignment" pipeline step -- a direct actor link, not the
    /// full Ownership/Control model ADR-019 section 10 explicitly defers.
    /// </summary>
    public sealed class SceneEntity
    {
        public SceneEntity(string entityId, string displayName, SceneEntityVisibility visibility, UserId? assignedToUserId)
        {
            if (string.IsNullOrWhiteSpace(entityId) || entityId.Length > 64) throw new ArgumentException("EntityId is not safe.", nameof(entityId));
            if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 96) throw new ArgumentException("DisplayName is not safe.", nameof(displayName));
            if (!Enum.IsDefined(typeof(SceneEntityVisibility), visibility)) throw new ArgumentOutOfRangeException(nameof(visibility));
            if (assignedToUserId.HasValue && !assignedToUserId.Value.IsValid) throw new ArgumentException("AssignedToUserId, if present, must be valid.", nameof(assignedToUserId));

            EntityId = entityId;
            DisplayName = displayName;
            Visibility = visibility;
            AssignedToUserId = assignedToUserId;
        }

        public string EntityId { get; }
        public string DisplayName { get; }
        public SceneEntityVisibility Visibility { get; }
        public UserId? AssignedToUserId { get; }
    }

    /// <summary>
    /// ODY-S02-010: the single host-side authoritative scene state
    /// (ADR-019 section 7's "единое авторитативное состояние плюс
    /// per-connection redaction-фильтр", not N independently maintained
    /// authoritative copies -- section 14.2's rejected alternative). One
    /// instance per active session, in-memory only; no persistence yet
    /// (that is a future SLICE-03+ concern, out of this task's scope).
    /// </summary>
    public sealed class Scene
    {
        private readonly List<SceneEntity> _entities = new List<SceneEntity>();

        public Scene(string sceneId)
        {
            if (string.IsNullOrWhiteSpace(sceneId) || sceneId.Length > 64) throw new ArgumentException("SceneId is not safe.", nameof(sceneId));
            SceneId = sceneId;
        }

        public string SceneId { get; }
        public IReadOnlyList<SceneEntity> Entities => _entities;

        public void AddEntity(SceneEntity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            _entities.Add(entity);
        }
    }

    /// <summary>
    /// ADR-019 section 7's pipeline step "Membership -&gt; PermissionDecision
    /// inputs": the audience actor's UserId and baseline role (ODY-S02-009's
    /// SessionMember already carries both). Action-check PermissionDecision
    /// itself (ADR-019 section 3.3/6.1, "may Actor perform this command") is
    /// ODY-S02-011 scope -- this context only feeds the read/visibility
    /// check (section 6.2), never a command authorization decision.
    /// </summary>
    public sealed class ActorVisibilityContext
    {
        public ActorVisibilityContext(UserId audienceUserId, BaselineRole role)
        {
            if (!audienceUserId.IsValid) throw new ArgumentException("AudienceUserId is required.", nameof(audienceUserId));
            if (!Enum.IsDefined(typeof(BaselineRole), role)) throw new ArgumentOutOfRangeException(nameof(role));

            AudienceUserId = audienceUserId;
            Role = role;
        }

        public UserId AudienceUserId { get; }
        public BaselineRole Role { get; }
    }

    /// <summary>
    /// ADR-019 section 3.4/section 6.2: the read/visibility check, a pure
    /// function of scene state and actor context, computed entirely in the
    /// Application layer -- never delegated to the client, never given
    /// Odyssey.Networking direct access to build it (ADR-001 section 6.6).
    /// A fresh implementation for this task, not a reuse of ODY-S02-007's
    /// (SP-04) test-only harness types (SLICE-02_IMPLEMENTATION_BACKLOG
    /// section 2.3).
    /// </summary>
    public static class VisibilityPolicy
    {
        public static IReadOnlyList<SceneEntity> ComputeVisibleEntities(Scene scene, ActorVisibilityContext context)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            if (context == null) throw new ArgumentNullException(nameof(context));

            List<SceneEntity> visible = new List<SceneEntity>();
            foreach (SceneEntity entity in scene.Entities)
            {
                if (IsVisible(entity, context)) visible.Add(entity);
            }

            return visible;
        }

        private static bool IsVisible(SceneEntity entity, ActorVisibilityContext context)
        {
            if (context.Role == BaselineRole.MainGM) return true;
            if (entity.Visibility == SceneEntityVisibility.Public) return true;

            // ADR-019 section 5.2: Player sees their assigned entity's full
            // data even when HiddenGameplay; section 5.3: Observer never sees
            // hidden data under any circumstance in baseline, regardless of
            // assignment.
            return context.Role == BaselineRole.Player
                && entity.AssignedToUserId.HasValue
                && entity.AssignedToUserId.Value.Equals(context.AudienceUserId);
        }
    }

    /// <summary>
    /// ADR-017 section 4's ProjectionSnapshot identity, field-for-field: all
    /// four identity dimensions (SnapshotId; BaseSessionSequence/
    /// ProjectionRevision/PermissionRevision; PayloadHash) together, plus
    /// SessionId/AudienceUserId/CreatedAtHostTime. Delta batches, gap
    /// detection, and reconnect fallback (ADR-017 sections 5-9) are
    /// ODY-S02-011/012 scope, not represented here -- this type only carries
    /// the primary/late-join snapshot path (ADR-017 section 7).
    /// </summary>
    public sealed class ProjectionSnapshot
    {
        public ProjectionSnapshot(
            string snapshotId,
            SessionId sessionId,
            UserId audienceUserId,
            long baseSessionSequence,
            long projectionRevision,
            long permissionRevision,
            IReadOnlyList<SceneEntity> visibleEntities,
            string payloadHash,
            UtcInstant createdAtHostTime)
        {
            if (string.IsNullOrWhiteSpace(snapshotId) || snapshotId.Length > 64) throw new ArgumentException("SnapshotId is not safe.", nameof(snapshotId));
            if (!sessionId.IsValid) throw new ArgumentException("SessionId is required.", nameof(sessionId));
            if (!audienceUserId.IsValid) throw new ArgumentException("AudienceUserId is required.", nameof(audienceUserId));
            if (baseSessionSequence < 0) throw new ArgumentOutOfRangeException(nameof(baseSessionSequence));
            if (projectionRevision < 0) throw new ArgumentOutOfRangeException(nameof(projectionRevision));
            if (permissionRevision < 0) throw new ArgumentOutOfRangeException(nameof(permissionRevision));
            if (visibleEntities == null) throw new ArgumentNullException(nameof(visibleEntities));
            if (string.IsNullOrWhiteSpace(payloadHash)) throw new ArgumentException("PayloadHash is required.", nameof(payloadHash));
            if (!createdAtHostTime.IsValid) throw new ArgumentException("CreatedAtHostTime is required.", nameof(createdAtHostTime));

            SnapshotId = snapshotId;
            SessionId = sessionId;
            AudienceUserId = audienceUserId;
            BaseSessionSequence = baseSessionSequence;
            ProjectionRevision = projectionRevision;
            PermissionRevision = permissionRevision;
            VisibleEntities = visibleEntities;
            PayloadHash = payloadHash;
            CreatedAtHostTime = createdAtHostTime;
        }

        public string SnapshotId { get; }
        public SessionId SessionId { get; }
        public UserId AudienceUserId { get; }
        public long BaseSessionSequence { get; }
        public long ProjectionRevision { get; }
        public long PermissionRevision { get; }
        public IReadOnlyList<SceneEntity> VisibleEntities { get; }
        public string PayloadHash { get; }
        public UtcInstant CreatedAtHostTime { get; }
    }

    /// <summary>
    /// ADR-019 section 7's pipeline applied in one place: Membership (caller
    /// already holds the actor's SessionMember/role, ODY-S02-009) -&gt;
    /// PermissionDecision inputs (ActorVisibilityContext) -&gt;
    /// VisibilityPolicy -&gt; Scene assignments -&gt; ClientProjection
    /// (ProjectionSnapshot). Deterministic given identical inputs: repeating
    /// a snapshot request for the same scene/context/sequence numbers yields
    /// the same PayloadHash (SnapshotId/CreatedAtHostTime intentionally vary
    /// per build -- they identify the build event, not the content).
    /// </summary>
    public static class SceneProjectionBuilder
    {
        public static ProjectionSnapshot BuildSnapshot(SessionId sessionId, Scene scene, ActorVisibilityContext context, long baseSessionSequence, long projectionRevision, long permissionRevision, IWallClock clock)
        {
            if (!sessionId.IsValid) throw new ArgumentException("SessionId is required.", nameof(sessionId));
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (clock == null) throw new ArgumentNullException(nameof(clock));

            IReadOnlyList<SceneEntity> visible = VisibilityPolicy.ComputeVisibleEntities(scene, context);
            string payloadHash = ComputePayloadHash(sessionId, context.AudienceUserId, baseSessionSequence, projectionRevision, permissionRevision, visible);

            // Local opaque identifier, not a gameplay RNG result -- the same
            // ADR-008 exemption ODY-S02-009's JoinCode.Generate() and
            // ODY-S01-007's CampaignId already establish for this codebase.
            string snapshotId = "psnap_" + Guid.NewGuid().ToString("N");

            return new ProjectionSnapshot(snapshotId, sessionId, context.AudienceUserId, baseSessionSequence, projectionRevision, permissionRevision, visible, payloadHash, clock.GetUtcNow());
        }

        private static string ComputePayloadHash(SessionId sessionId, UserId audienceUserId, long baseSessionSequence, long projectionRevision, long permissionRevision, IReadOnlyList<SceneEntity> entities)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(sessionId).Append('|').Append(audienceUserId).Append('|')
                .Append(baseSessionSequence).Append('|').Append(projectionRevision).Append('|').Append(permissionRevision);
            foreach (SceneEntity entity in entities)
            {
                builder.Append('|').Append(entity.EntityId).Append(':').Append(entity.DisplayName).Append(':').Append(entity.Visibility)
                    .Append(':').Append(entity.AssignedToUserId.HasValue ? entity.AssignedToUserId.Value.ToString() : "-");
            }

            return CanonicalJson.Sha256LowerHex(Encoding.UTF8.GetBytes(builder.ToString()));
        }
    }
}
