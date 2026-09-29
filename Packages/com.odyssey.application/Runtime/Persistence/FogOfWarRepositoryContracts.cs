using System;
using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// SLICE-10 Block 4: persistence port for one player's persistent "map memory" -- a set of
    /// explored-area reveals, each keyed directly on <see cref="UserId"/> (this codebase's own
    /// established convention for "state per actor": <c>TokenRecord.ControllerUserId</c>,
    /// <c>CampaignMembership</c> itself, never an artificial "membership id"). A row is a circle
    /// (<see cref="FogRevealRecord.CenterX"/>/<see cref="FogRevealRecord.CenterY"/>/
    /// <see cref="FogRevealRecord.Radius"/>) -- a deliberate simplification that ignores obstacle
    /// shape, unlike the exact, obstacle-aware <c>LineOfSight.CanSee</c> live-visibility computation
    /// (Block 3) this task composes but does not modify.
    ///
    /// INSERT-only: no method here updates or deletes an existing row. Persistent map memory is
    /// monotonic by product decision (2026-09-29) -- once explored, a region never "forgets" -- so an
    /// append-only store is not a workaround for the invariant, it is the natural storage shape for it.
    /// A storage port only -- it does not decide who may read another player's reveals;
    /// <c>Odyssey.Application.Board.PlayerVisibilityService</c> owns that (self-scoped-or-MainGM).
    /// </summary>
    public interface IFogOfWarRepository
    {
        /// <summary>Always an INSERT of a brand-new row -- never an update of an existing one. <see cref="Odyssey.Application.Board.PlayerVisibilityService.RecordExploration"/> is the only caller, and only after it has already determined (via <see cref="ListReveals"/>) that no existing reveal of this same <paramref name="userId"/> fully covers the new circle.</summary>
        Result<FogRevealRecord> RecordReveal(CampaignHandle campaign, SceneId sceneId, UserId userId, double centerX, double centerY, double radius, CommandId commandId, CorrelationId correlationId);

        /// <summary>Every reveal ever recorded for this exact <paramref name="userId"/> in this Scene -- the full, unmerged set (this task does not union/merge overlapping circles into a smaller representation; see <c>PlayerVisibilityService</c>'s own remarks).</summary>
        Result<IReadOnlyList<FogRevealRecord>> ListReveals(CampaignHandle campaign, SceneId sceneId, UserId userId, CorrelationId correlationId);
    }

    public sealed class FogRevealRecord
    {
        public FogRevealRecord(FogRevealId revealId, CampaignId campaignId, SceneId sceneId, UserId userId, double centerX, double centerY, double radius, UtcInstant createdAt)
        {
            if (!revealId.IsValid) throw new ArgumentException("FogRevealId is required.", nameof(revealId));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));
            if (!userId.IsValid) throw new ArgumentException("UserId is required.", nameof(userId));
            if (!double.IsFinite(centerX) || !double.IsFinite(centerY)) throw new ArgumentException("CenterX/CenterY must be finite.");
            if (!double.IsFinite(radius) || radius < 0) throw new ArgumentException("Radius must be finite and non-negative.", nameof(radius));

            RevealId = revealId;
            CampaignId = campaignId;
            SceneId = sceneId;
            UserId = userId;
            CenterX = centerX;
            CenterY = centerY;
            Radius = radius;
            CreatedAt = createdAt;
        }

        public FogRevealId RevealId { get; }
        public CampaignId CampaignId { get; }
        public SceneId SceneId { get; }
        public UserId UserId { get; }
        public double CenterX { get; }
        public double CenterY { get; }
        public double Radius { get; }
        public UtcInstant CreatedAt { get; }
    }

    public static class FogOfWarFailures
    {
        public static Error IoFailed(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceFogOfWarIoFailed,
            ErrorCategory.PermanentInfrastructure,
            SafeReasonCode.UnexpectedError,
            UserMessageKey.Parse("errors.persistence.fog_of_war_io_failed"),
            RetryDirective.ManualRecoveryRequired,
            correlationId);

        /// <summary>`PlayerVisibilityService.ComputeVisibleTokens`/`IsPointExplored`: the requesting user is asking about a different user's own fog state and is not the campaign's registered MainGm -- self-scoped reads only, fail closed.</summary>
        public static Error TargetUserDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PlayerVisibilityTargetUserDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.player_visibility.target_user_denied"),
            RetryDirective.DoNotRetry,
            correlationId);
    }
}
