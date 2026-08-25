using System;
using System.Collections.Generic;

namespace Odyssey.Tests.Networking.HiddenDataBoundary.Harness
{
    /// <summary>
    /// ODY-S02-007 (SP-04) harness types only -- not production code. Minimal,
    /// functional (not stubbed) implementation of the ADR-017 ProjectionSnapshot/
    /// ProjectionDeltaBatch shape and ADR-019's VisibilityPolicy pipeline, scoped
    /// to exactly what this spike's test needs. See the harness README for why
    /// this lives in the test project rather than Tools/Spikes/ or production
    /// Odyssey.Application/Networking code.
    /// </summary>
    public enum DataClassification
    {
        Public = 1,
        HiddenGameplay = 2
    }

    public sealed class GameEntity
    {
        public GameEntity(string id, string displayName, DataClassification classification)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            Classification = classification;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public DataClassification Classification { get; }
    }

    /// <summary>
    /// The single host-side authoritative game state -- ADR-019 section 7's
    /// "единое авторитативное состояние", not a per-connection copy.
    /// </summary>
    public sealed class HostWorldState
    {
        private readonly Dictionary<string, GameEntity> _entities = new();

        public void AddEntity(GameEntity entity) => _entities[entity.Id] = entity;

        public IReadOnlyDictionary<string, GameEntity> Entities => _entities;
    }

    public enum BaselineRole
    {
        MainGM = 1,
        Player = 2,
        Observer = 3
    }

    /// <summary>
    /// Per-actor permission state -- ADR-019 section 5's baseline role plus the
    /// minimal override surface this test needs (an explicit visibility grant
    /// and an explicit action capability), not the full PermissionKey/Scope
    /// system ADR-019 section 10 explicitly defers.
    /// </summary>
    public sealed class ActorPermissionState
    {
        private readonly HashSet<string> _explicitVisibilityGrants = new(StringComparer.Ordinal);
        private readonly HashSet<string> _capabilities = new(StringComparer.Ordinal);

        public ActorPermissionState(BaselineRole role) => Role = role;

        public BaselineRole Role { get; }

        public void GrantVisibility(string entityId) => _explicitVisibilityGrants.Add(entityId);
        public void RevokeVisibility(string entityId) => _explicitVisibilityGrants.Remove(entityId);
        public void GrantCapability(string capability) => _capabilities.Add(capability);
        public void RevokeCapability(string capability) => _capabilities.Remove(capability);

        public IReadOnlySet<string> ExplicitVisibilityGrants => _explicitVisibilityGrants;
        public IReadOnlySet<string> Capabilities => _capabilities;

        /// <summary>An immutable point-in-time copy, for callers that need to compare "before" against "after" a mutation on this same instance.</summary>
        public HashSet<string> SnapshotCapabilities() => new(_capabilities, StringComparer.Ordinal);
    }

    /// <summary>
    /// ADR-019 section 3.4/section 7's VisibilityPolicy -- a pure function of
    /// world state and permission state, computed host-side, never delegated to
    /// the client (ADR-019 section 6.2).
    /// </summary>
    public static class VisibilityPolicy
    {
        public static HashSet<string> ComputeVisibleEntityIds(HostWorldState world, ActorPermissionState permissions)
        {
            var visible = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entity in world.Entities.Values)
            {
                if (permissions.Role == BaselineRole.MainGM ||
                    entity.Classification == DataClassification.Public ||
                    permissions.ExplicitVisibilityGrants.Contains(entity.Id))
                {
                    visible.Add(entity.Id);
                }
            }

            return visible;
        }
    }

    public enum ProjectionOperationKind
    {
        AddEntity = 1,
        RemoveFromProjection = 2,
        AddCapability = 3,
        RemoveCapability = 4
    }

    public sealed class ProjectionOperation
    {
        private ProjectionOperation(ProjectionOperationKind kind, string targetId, GameEntity? entity)
        {
            Kind = kind;
            TargetId = targetId ?? throw new ArgumentNullException(nameof(targetId));
            Entity = entity;
        }

        public ProjectionOperationKind Kind { get; }
        public string TargetId { get; }
        public GameEntity? Entity { get; }

        public static ProjectionOperation AddEntity(GameEntity entity) => new(ProjectionOperationKind.AddEntity, entity.Id, entity);
        public static ProjectionOperation RemoveFromProjection(string entityId) => new(ProjectionOperationKind.RemoveFromProjection, entityId, null);
        public static ProjectionOperation AddCapability(string capability) => new(ProjectionOperationKind.AddCapability, capability, null);
        public static ProjectionOperation RemoveCapability(string capability) => new(ProjectionOperationKind.RemoveCapability, capability, null);
    }

    /// <summary>ADR-017 section 4's ProjectionSnapshot -- baseline field subset this test needs.</summary>
    public sealed class ProjectionSnapshot
    {
        public ProjectionSnapshot(string snapshotId, string audienceUserId, long baseSessionSequence, IReadOnlyList<GameEntity> entities, IReadOnlyList<string> allowedCommands)
        {
            SnapshotId = snapshotId;
            AudienceUserId = audienceUserId;
            BaseSessionSequence = baseSessionSequence;
            Entities = entities;
            AllowedCommands = allowedCommands;
        }

        public string SnapshotId { get; }
        public string AudienceUserId { get; }
        public long BaseSessionSequence { get; }
        public IReadOnlyList<GameEntity> Entities { get; }
        public IReadOnlyList<string> AllowedCommands { get; }
    }

    /// <summary>ADR-017 section 5's ProjectionDeltaBatch -- baseline field subset this test needs.</summary>
    public sealed class ProjectionDeltaBatch
    {
        public ProjectionDeltaBatch(string audienceUserId, long sequenceFrom, long sequenceTo, IReadOnlyList<ProjectionOperation> operations)
        {
            AudienceUserId = audienceUserId;
            SequenceFrom = sequenceFrom;
            SequenceTo = sequenceTo;
            Operations = operations;
        }

        public string AudienceUserId { get; }
        public long SequenceFrom { get; }
        public long SequenceTo { get; }
        public IReadOnlyList<ProjectionOperation> Operations { get; }
    }

    /// <summary>
    /// Host-side builder: ADR-019 section 7's pipeline (Membership -&gt;
    /// PermissionDecision -&gt; VisibilityPolicy -&gt; ClientProjection), and
    /// section 8's revocation-delta mechanism (reuse of RemoveFromProjection/
    /// RemoveCapability, not a new channel).
    /// </summary>
    public static class ProjectionBuilder
    {
        public static ProjectionSnapshot BuildSnapshot(HostWorldState world, string audienceUserId, ActorPermissionState permissions, long sequence)
        {
            HashSet<string> visibleIds = VisibilityPolicy.ComputeVisibleEntityIds(world, permissions);
            var entities = new List<GameEntity>();
            foreach (var id in visibleIds) entities.Add(world.Entities[id]);
            return new ProjectionSnapshot("snap_" + Guid.NewGuid().ToString("N"), audienceUserId, sequence, entities, new List<string>(permissions.Capabilities));
        }

        /// <summary>
        /// Builds the delta batch reflecting a permission-state change for one
        /// audience -- ADR-019 section 8's revocation mechanism, using only
        /// already-accepted ADR-017 operation kinds.
        /// </summary>
        public static ProjectionDeltaBatch BuildPermissionChangeDelta(
            HostWorldState world,
            string audienceUserId,
            HashSet<string> previouslyVisibleIds,
            ActorPermissionState newPermissions,
            IReadOnlySet<string> previousCapabilities,
            long sequenceFrom,
            long sequenceTo)
        {
            HashSet<string> nowVisibleIds = VisibilityPolicy.ComputeVisibleEntityIds(world, newPermissions);
            var operations = new List<ProjectionOperation>();

            foreach (var id in nowVisibleIds)
            {
                if (!previouslyVisibleIds.Contains(id))
                {
                    operations.Add(ProjectionOperation.AddEntity(world.Entities[id]));
                }
            }

            foreach (var id in previouslyVisibleIds)
            {
                if (!nowVisibleIds.Contains(id))
                {
                    operations.Add(ProjectionOperation.RemoveFromProjection(id));
                }
            }

            foreach (var capability in newPermissions.Capabilities)
            {
                if (!previousCapabilities.Contains(capability))
                {
                    operations.Add(ProjectionOperation.AddCapability(capability));
                }
            }

            foreach (var capability in previousCapabilities)
            {
                if (!newPermissions.Capabilities.Contains(capability))
                {
                    operations.Add(ProjectionOperation.RemoveCapability(capability));
                }
            }

            return new ProjectionDeltaBatch(audienceUserId, sequenceFrom, sequenceTo, operations);
        }

        /// <summary>
        /// Builds a delta batch for an unrelated world-state change (no
        /// permission change), still filtered through VisibilityPolicy -- proves
        /// a hidden entity never leaks through an ordinary gameplay delta either.
        /// </summary>
        public static ProjectionDeltaBatch BuildUnrelatedChangeDelta(HostWorldState world, string audienceUserId, ActorPermissionState permissions, GameEntity changedEntity, long sequenceFrom, long sequenceTo)
        {
            HashSet<string> visibleIds = VisibilityPolicy.ComputeVisibleEntityIds(world, permissions);
            var operations = new List<ProjectionOperation>();
            if (visibleIds.Contains(changedEntity.Id))
            {
                operations.Add(ProjectionOperation.AddEntity(changedEntity));
            }

            return new ProjectionDeltaBatch(audienceUserId, sequenceFrom, sequenceTo, operations);
        }
    }
}
