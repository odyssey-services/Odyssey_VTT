using System;
using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Results;
using Odyssey.Domain.Geometry;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// SLICE-10 Block 2: persistence port for one obstacle (wall/door/window) segment, scoped to a
    /// Scene the same plain-field way <see cref="TokenRecord.SceneId"/> already is (not a database
    /// foreign key -- <see cref="Odyssey.Persistence.Sqlite.SqliteSceneRepository"/>'s own
    /// <c>EnsureSceneTokenTables</c> precedent). A storage port only -- it does not decide who may
    /// create an obstacle or toggle a door; <c>Odyssey.Application.Board.ObstacleAuthoringService</c>
    /// owns that.
    /// </summary>
    public interface IObstacleRepository
    {
        /// <summary>
        /// <paramref name="maxHp"/>/<paramref name="protection"/>: SLICE-10 Block 5 Part B -- durability is
        /// optional per obstacle. When <paramref name="maxHp"/> is supplied (must be &gt; 0), a matching
        /// <see cref="ObstacleDurabilityRecord"/> row is created in the same transaction, seeded at full
        /// health and not destroyed. <paramref name="protection"/> defaults to <c>0</c> and is only
        /// meaningful together with <paramref name="maxHp"/> -- supplying a non-zero <paramref name="protection"/>
        /// without <paramref name="maxHp"/> is a caller error (there is nothing for it to protect).
        /// </summary>
        Result<ObstacleRecord> CreateObstacle(CampaignHandle campaign, SceneId sceneId, ObstacleKind kind, double x1, double y1, double x2, double y2, CommandId commandId, CorrelationId correlationId, long? maxHp = null, long protection = 0);

        /// <summary>Atomic, revision-gated flip of <see cref="ObstacleRecord.IsOpen"/> -- by exact precedent of <see cref="ISceneRepository.MoveToken"/>'s own <c>expectedRevision</c> CAS check. The caller (<c>ObstacleAuthoringService.ToggleDoorState</c>) has already confirmed <see cref="ObstacleRecord.Kind"/> is <see cref="ObstacleKind.Door"/> before reaching here; this method re-confirms it and fails closed if not, rather than trusting the caller silently.</summary>
        Result<ObstacleRecord> ToggleDoorState(CampaignHandle campaign, ObstacleId obstacleId, bool isOpen, long expectedRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>SLICE-10 Block 5 Part B: excludes any obstacle whose <see cref="ObstacleDurabilityRecord.IsDestroyed"/> is true -- the single integration point with cover/line-of-sight geometry (Block 3's `LineOfSight`/this task's own `CoverGeometry`, neither of which is modified): a destroyed obstacle simply stops appearing here, with no separate synchronization needed.</summary>
        Result<IReadOnlyList<ObstacleRecord>> ListObstacles(CampaignHandle campaign, SceneId sceneId, CorrelationId correlationId);

        /// <summary>
        /// SLICE-10 Block 5 Part B: applies raw <paramref name="damageAmount"/> to the obstacle's own
        /// durability row -- <c>effectiveDamage = Max(0, damageAmount - Protection)</c>,
        /// <c>CurrentHp = Max(0, CurrentHp - effectiveDamage)</c>, <c>IsDestroyed = (CurrentHp == 0)</c>,
        /// revision-gated exactly like <see cref="ToggleDoorState"/>. Fails closed with
        /// <see cref="ObstacleFailures.DurabilityNotConfigured"/> if this obstacle was created without
        /// <c>maxHp</c> (no durability row exists to damage). No authorization here -- MainGM-only is
        /// decided by <c>ObstacleAuthoringService.ApplyObstacleDamage</c>, by exact precedent of
        /// <see cref="CreateObstacle"/>'s own MainGM check living one layer up, not in this repository.
        /// </summary>
        Result<ObstacleDurabilityRecord> ApplyObstacleDamage(CampaignHandle campaign, ObstacleId obstacleId, long damageAmount, long expectedRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>Read-only lookup of an obstacle's own durability row, for callers (tests, tooling) that need to inspect current HP/IsDestroyed directly rather than only observing its effect through <see cref="ListObstacles"/>.</summary>
        Result<ObstacleDurabilityRecord> GetObstacleDurability(CampaignHandle campaign, ObstacleId obstacleId, CorrelationId correlationId);
    }

    public sealed class ObstacleRecord
    {
        public ObstacleRecord(ObstacleId obstacleId, SceneId sceneId, CampaignId campaignId, ObstacleKind kind, double x1, double y1, double x2, double y2, bool? isOpen, long revision, UtcInstant createdAt, UtcInstant updatedAt)
        {
            if (!obstacleId.IsValid) throw new ArgumentException("ObstacleId is required.", nameof(obstacleId));
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!BoardGeometry.IsFinite(x1, y1) || !BoardGeometry.IsFinite(x2, y2)) throw new ArgumentException("Obstacle endpoints must be finite.");
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
            if (isOpen.HasValue && kind != ObstacleKind.Door) throw new ArgumentException("IsOpen is meaningful only for a Door.", nameof(isOpen));

            ObstacleId = obstacleId;
            SceneId = sceneId;
            CampaignId = campaignId;
            Kind = kind;
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
            IsOpen = isOpen;
            Revision = revision;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        public ObstacleId ObstacleId { get; }
        public SceneId SceneId { get; }
        public CampaignId CampaignId { get; }
        public ObstacleKind Kind { get; }
        public double X1 { get; }
        public double Y1 { get; }
        public double X2 { get; }
        public double Y2 { get; }

        /// <summary>Meaningful only for <see cref="ObstacleKind.Door"/> (defaults to <c>false</c>, i.e. closed, at creation); always <c>null</c> for <see cref="ObstacleKind.Wall"/>/<see cref="ObstacleKind.Window"/> -- see <see cref="ObstacleGeometry"/>.</summary>
        public bool? IsOpen { get; }

        public long Revision { get; }
        public UtcInstant CreatedAt { get; }
        public UtcInstant UpdatedAt { get; }
    }

    /// <summary>SLICE-10 Block 5 Part B: one obstacle's own optional HP/armor state. Exists only for obstacles created with a <c>maxHp</c> -- a wall/door/window with no durability configured is indestructible, by omission, not a special flag.</summary>
    public sealed class ObstacleDurabilityRecord
    {
        public ObstacleDurabilityRecord(ObstacleId obstacleId, CampaignId campaignId, long maxHp, long currentHp, long protection, bool isDestroyed, long revision, UtcInstant createdAt, UtcInstant updatedAt)
        {
            if (!obstacleId.IsValid) throw new ArgumentException("ObstacleId is required.", nameof(obstacleId));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (maxHp <= 0) throw new ArgumentOutOfRangeException(nameof(maxHp));
            if (currentHp < 0 || currentHp > maxHp) throw new ArgumentOutOfRangeException(nameof(currentHp));
            if (protection < 0) throw new ArgumentOutOfRangeException(nameof(protection));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
            if (isDestroyed != (currentHp == 0)) throw new ArgumentException("IsDestroyed must be exactly (CurrentHp == 0).", nameof(isDestroyed));

            ObstacleId = obstacleId;
            CampaignId = campaignId;
            MaxHp = maxHp;
            CurrentHp = currentHp;
            Protection = protection;
            IsDestroyed = isDestroyed;
            Revision = revision;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        public ObstacleId ObstacleId { get; }
        public CampaignId CampaignId { get; }
        public long MaxHp { get; }
        public long CurrentHp { get; }
        public long Protection { get; }
        public bool IsDestroyed { get; }
        public long Revision { get; }
        public UtcInstant CreatedAt { get; }
        public UtcInstant UpdatedAt { get; }
    }

    public static class ObstacleFailures
    {
        public static Error NotFound(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceObstacleNotFound,
            ErrorCategory.NotFound,
            SafeReasonCode.TargetUnavailable,
            UserMessageKey.Parse("errors.persistence.obstacle_not_found"),
            RetryDirective.DoNotRetry,
            correlationId);

        public static Error IoFailed(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceObstacleIoFailed,
            ErrorCategory.PermanentInfrastructure,
            SafeReasonCode.UnexpectedError,
            UserMessageKey.Parse("errors.persistence.obstacle_io_failed"),
            RetryDirective.ManualRecoveryRequired,
            correlationId);

        /// <summary>ODY-S03-004 `TokenRevisionConflict`'s exact convention: the atomic optimistic-concurrency guard inside `SqliteObstacleRepository.ToggleDoorState`'s own transaction.</summary>
        public static Error RevisionConflict(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceObstacleRevisionConflict,
            ErrorCategory.Conflict,
            SafeReasonCode.StateChanged,
            UserMessageKey.Parse("errors.persistence.obstacle_revision_conflict"),
            RetryDirective.DoNotRetry,
            correlationId);

        public static Error CreateDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.ObstacleAuthoringDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.obstacle.authoring_denied"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>`ObstacleAuthoringService.ToggleDoorState`: the actor is not a registered participant of this campaign in any role.</summary>
        public static Error ToggleDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.ObstacleToggleDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.obstacle.toggle_denied"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>`ToggleDoorState` called against a Wall/Window -- a typed rejection, not a silent no-op or an exception.</summary>
        public static Error ToggleNotADoor(CorrelationId correlationId) => Error.Create(
            ErrorCodes.ObstacleToggleNotADoor,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.obstacle.toggle_not_a_door"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>`ObstacleAuthoringService.ApplyObstacleDamage`: the actor is not MainGm.</summary>
        public static Error ApplyDamageDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.ObstacleApplyDamageDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.obstacle.apply_damage_denied"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>`ApplyObstacleDamage`/`GetObstacleDurability` against an obstacle created without `maxHp` -- there is no durability row to damage or read. A typed rejection, not a silent no-op treating it as indestructible-and-successful.</summary>
        public static Error DurabilityNotConfigured(CorrelationId correlationId) => Error.Create(
            ErrorCodes.ObstacleDurabilityNotConfigured,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.obstacle.durability_not_configured"),
            RetryDirective.DoNotRetry,
            correlationId);
    }
}
