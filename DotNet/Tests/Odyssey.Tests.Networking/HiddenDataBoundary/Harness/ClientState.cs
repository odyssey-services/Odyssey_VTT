using System.Collections.Generic;
using System.Linq;

namespace Odyssey.Tests.Networking.HiddenDataBoundary.Harness
{
    /// <summary>
    /// Client-side runtime projection state -- what a connected client actually
    /// holds in memory. Deliberately a separate structure from
    /// <see cref="ClientLocalCache"/> so the two roadmap 11.5 surfaces
    /// ("runtime-state" and "local cache") are genuinely distinct, not the same
    /// assertion asked twice.
    /// </summary>
    public sealed class ClientRuntimeState
    {
        private readonly Dictionary<string, GameEntity> _entities = new();
        private readonly HashSet<string> _allowedCommands = new();

        public IReadOnlyDictionary<string, GameEntity> Entities => _entities;
        public IReadOnlySet<string> AllowedCommands => _allowedCommands;

        public void ApplySnapshot(ProjectionSnapshot snapshot)
        {
            _entities.Clear();
            foreach (var entity in snapshot.Entities) _entities[entity.Id] = entity;
            _allowedCommands.Clear();
            foreach (var command in snapshot.AllowedCommands) _allowedCommands.Add(command);
        }

        public void ApplyDelta(ProjectionDeltaBatch batch)
        {
            foreach (var operation in batch.Operations)
            {
                switch (operation.Kind)
                {
                    case ProjectionOperationKind.AddEntity:
                        _entities[operation.Entity!.Id] = operation.Entity;
                        break;
                    case ProjectionOperationKind.RemoveFromProjection:
                        _entities.Remove(operation.TargetId);
                        break;
                    case ProjectionOperationKind.AddCapability:
                        _allowedCommands.Add(operation.TargetId);
                        break;
                    case ProjectionOperationKind.RemoveCapability:
                        _allowedCommands.Remove(operation.TargetId);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Client-side persisted local cache (ADR-017 section 3's "AssetCacheIndex"-
    /// style client metadata, roadmap 11.5's "local cache" surface) -- updated at
    /// the same points runtime state is, and must be purged by
    /// RemoveFromProjection just like runtime state, not left stale.
    /// </summary>
    public sealed class ClientLocalCache
    {
        private readonly Dictionary<string, GameEntity> _cachedEntities = new();

        public IReadOnlyDictionary<string, GameEntity> CachedEntities => _cachedEntities;

        public void ApplySnapshot(ProjectionSnapshot snapshot)
        {
            _cachedEntities.Clear();
            foreach (var entity in snapshot.Entities) _cachedEntities[entity.Id] = entity;
        }

        public void ApplyDelta(ProjectionDeltaBatch batch)
        {
            foreach (var operation in batch.Operations)
            {
                switch (operation.Kind)
                {
                    case ProjectionOperationKind.AddEntity:
                        _cachedEntities[operation.Entity!.Id] = operation.Entity;
                        break;
                    case ProjectionOperationKind.RemoveFromProjection:
                        _cachedEntities.Remove(operation.TargetId);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// A client that keeps both structures in sync from received payload --
    /// mirrors how a real client would update runtime state and local cache
    /// together when it applies a snapshot or delta.
    /// </summary>
    public sealed class HiddenDataBoundaryClient
    {
        public ClientRuntimeState Runtime { get; } = new();
        public ClientLocalCache Cache { get; } = new();

        public void ApplySnapshot(ProjectionSnapshot snapshot)
        {
            Runtime.ApplySnapshot(snapshot);
            Cache.ApplySnapshot(snapshot);
        }

        public void ApplyDelta(ProjectionDeltaBatch batch)
        {
            Runtime.ApplyDelta(batch);
            Cache.ApplyDelta(batch);
        }

        /// <summary>
        /// A diagnostic-log-worthy summary of what this client currently knows
        /// about -- built only from this client's own runtime state, never from
        /// host world state. If a hidden entity was correctly never delivered,
        /// it structurally cannot appear here.
        /// </summary>
        public IReadOnlyList<string> KnownEntityIdsForDiagnostics() => Runtime.Entities.Keys.OrderBy(id => id, System.StringComparer.Ordinal).ToList();
    }
}
