using System;
using Odyssey.Application.Commands;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// SLICE-10 Block 3: persistence port for one token's own vision parameters (facing/FOV/range),
    /// scoped to a Scene the same plain-field way <see cref="ObstacleRecord.SceneId"/> already is
    /// (not a database foreign key). A separate table (<c>TokenVisionSettings</c>), not new columns
    /// on the existing <c>Token</c> table -- this codebase has never once used `ALTER TABLE` (recon
    /// confirmed), and a new column on an existing, unversioned-schema table is not the same safe
    /// operation a new `CREATE TABLE IF NOT EXISTS` is for already-existing campaign database files.
    /// A storage port only -- it does not decide who may set a token's facing or vision parameters;
    /// <c>Odyssey.Application.Board.TokenVisionService</c> owns that.
    /// </summary>
    public interface ITokenVisionRepository
    {
        Result<TokenVisionSettingsRecord> GetVisionSettings(CampaignHandle campaign, TokenId tokenId, CorrelationId correlationId);

        /// <summary>Atomic, revision-gated update of <see cref="TokenVisionSettingsRecord.FacingDegrees"/> only -- <see cref="TokenVisionSettingsRecord.FovAngleDegrees"/>/<see cref="TokenVisionSettingsRecord.ViewDistance"/> are left unchanged, by exact precedent of <see cref="ISceneRepository.SetTokenZOrder"/> touching only its own one field.</summary>
        Result<TokenVisionSettingsRecord> SetFacing(CampaignHandle campaign, TokenId tokenId, double facingDegrees, long expectedRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>Atomic, revision-gated update of <see cref="TokenVisionSettingsRecord.FovAngleDegrees"/>/<see cref="TokenVisionSettingsRecord.ViewDistance"/> together -- <see cref="TokenVisionSettingsRecord.FacingDegrees"/> is left unchanged.</summary>
        Result<TokenVisionSettingsRecord> SetVisionParameters(CampaignHandle campaign, TokenId tokenId, double fovAngleDegrees, double viewDistance, long expectedRevision, CommandId commandId, CorrelationId correlationId);
    }

    public sealed class TokenVisionSettingsRecord
    {
        /// <summary>SLICE-10 Block 3: the temporary default a token receives at `CreateToken` time (`Odyssey.Persistence.Sqlite.SqliteSceneRepository.CreateToken`'s own point-edit) until a MainGM configures it explicitly -- omnidirectional (360 degrees), so an unconfigured token's own vision is never accidentally narrower than intended.</summary>
        public const double DefaultFovAngleDegrees = 360.0;

        /// <summary>SLICE-10 Block 3: the temporary default view distance -- no existing precedent for a "sight range"/"attack range" default exists anywhere in this codebase (recon confirmed), so this is a deliberately generous placeholder a MainGM is expected to tune via <c>TokenVisionService.SetTokenVisionParameters</c>, not a tuned gameplay value.</summary>
        public const double DefaultViewDistance = 100.0;

        public TokenVisionSettingsRecord(TokenId tokenId, SceneId sceneId, CampaignId campaignId, double facingDegrees, double fovAngleDegrees, double viewDistance, long revision, UtcInstant createdAt, UtcInstant updatedAt)
        {
            if (!tokenId.IsValid) throw new ArgumentException("TokenId is required.", nameof(tokenId));
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!double.IsFinite(facingDegrees)) throw new ArgumentException("FacingDegrees must be finite.", nameof(facingDegrees));
            if (!double.IsFinite(fovAngleDegrees) || fovAngleDegrees < 0) throw new ArgumentException("FovAngleDegrees must be finite and non-negative.", nameof(fovAngleDegrees));
            if (!double.IsFinite(viewDistance) || viewDistance < 0) throw new ArgumentException("ViewDistance must be finite and non-negative.", nameof(viewDistance));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));

            TokenId = tokenId;
            SceneId = sceneId;
            CampaignId = campaignId;
            FacingDegrees = facingDegrees;
            FovAngleDegrees = fovAngleDegrees;
            ViewDistance = viewDistance;
            Revision = revision;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        public TokenId TokenId { get; }
        public SceneId SceneId { get; }
        public CampaignId CampaignId { get; }
        public double FacingDegrees { get; }
        public double FovAngleDegrees { get; }
        public double ViewDistance { get; }
        public long Revision { get; }
        public UtcInstant CreatedAt { get; }
        public UtcInstant UpdatedAt { get; }
    }

    public static class TokenVisionFailures
    {
        public static Error NotFound(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceTokenVisionNotFound,
            ErrorCategory.NotFound,
            SafeReasonCode.TargetUnavailable,
            UserMessageKey.Parse("errors.persistence.token_vision_not_found"),
            RetryDirective.DoNotRetry,
            correlationId);

        public static Error IoFailed(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceTokenVisionIoFailed,
            ErrorCategory.PermanentInfrastructure,
            SafeReasonCode.UnexpectedError,
            UserMessageKey.Parse("errors.persistence.token_vision_io_failed"),
            RetryDirective.ManualRecoveryRequired,
            correlationId);

        /// <summary>ADR-020's own `TokenRevisionConflict` convention: the atomic optimistic-concurrency guard inside `SqliteTokenVisionRepository`'s own transaction.</summary>
        public static Error RevisionConflict(CorrelationId correlationId) => Error.Create(
            ErrorCodes.PersistenceTokenVisionRevisionConflict,
            ErrorCategory.Conflict,
            SafeReasonCode.StateChanged,
            UserMessageKey.Parse("errors.persistence.token_vision_revision_conflict"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>`TokenVisionService.SetTokenFacing`: the actor is neither the token's own controller nor MainGm.</summary>
        public static Error SetFacingDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.TokenVisionSetFacingDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.token_vision.set_facing_denied"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>`TokenVisionService.SetTokenVisionParameters`: the actor is not MainGm.</summary>
        public static Error SetVisionParametersDenied(CorrelationId correlationId) => Error.Create(
            ErrorCodes.TokenVisionSetVisionParametersDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.token_vision.set_vision_parameters_denied"),
            RetryDirective.DoNotRetry,
            correlationId);

        /// <summary>`TokenVisionService.ComputeLineOfSight`: observer and target tokens are not on the same Scene -- a LOS query across two different maps has no meaning.</summary>
        public static Error ObserverAndTargetNotInSameScene(CorrelationId correlationId) => Error.Create(
            ErrorCodes.TokenVisionObserverAndTargetNotInSameScene,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.token_vision.observer_and_target_not_in_same_scene"),
            RetryDirective.DoNotRetry,
            correlationId);
    }
}
