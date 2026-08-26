using System;
using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// ODY-S01-008 minimal Scene/Token/Asset persistence port -- sufficient for
    /// roadmap section 10.5 steps 2-5 (import one test map, create a scene, place
    /// two tokens, change their positions). Not the full Scene/Board/Layer/
    /// SceneObject/Component domain model (03_Domain_Model section 10) -- only
    /// identity, name, status/revision, and bare token position.
    ///
    /// ODY-S01-009: every mutating method now takes a caller-supplied
    /// <see cref="CommandId"/> and routes through the ADR-012 section 5 single-
    /// transaction journal-projection pipeline (current-state row + DomainEvent +
    /// AppliedCommands committed atomically; redelivering the same CommandId
    /// replays the stored outcome instead of re-applying the effect). This is a
    /// breaking change to the ODY-S01-008 signatures; see the ODY-S01-009 task
    /// contract section 6 for the full justification.
    ///
    /// ODY-S03-004: <see cref="CreateToken"/> now takes a required
    /// <see cref="UserId"/> controller (08_Scenes_And_Board section 11.1's
    /// <c>LinkedEntityRef</c>/control-ownership concept, narrowed to a single
    /// "who may move this token" reference -- not the full ownership/control
    /// split ADR-019 section 10 already deferred). <see cref="MoveToken"/> now
    /// takes a required <c>expectedRevision</c>, closing a genuine optimistic-
    /// concurrency gap the ODY-S01-008/009 signature left open (it re-read but
    /// never validated the current revision before overwriting it). Both are
    /// breaking changes to the ODY-S01-009 signatures, the same kind of
    /// evolution that task's own doc comment already flagged as expected.
    /// A new <see cref="GetToken"/> read method lets a caller (this task's own
    /// <c>Odyssey.Application.Board.BoardMovementService</c>) check current
    /// controller/revision before submitting a command, without duplicating
    /// <see cref="MoveToken"/>'s internal read.
    /// </summary>
    public interface ISceneRepository
    {
        Result<SceneRecord> CreateScene(CampaignHandle campaign, string sceneName, CommandId commandId, CorrelationId correlationId);
        Result<TokenRecord> CreateToken(CampaignHandle campaign, SceneId sceneId, TokenPosition initialPosition, UserId controllerUserId, CommandId commandId, CorrelationId correlationId);
        Result<TokenRecord> GetToken(CampaignHandle campaign, TokenId tokenId, CorrelationId correlationId);
        Result<TokenRecord> MoveToken(CampaignHandle campaign, TokenId tokenId, TokenPosition newPosition, long expectedRevision, CommandId commandId, CorrelationId correlationId);
        Result<IReadOnlyList<TokenRecord>> ListTokens(CampaignHandle campaign, SceneId sceneId, CorrelationId correlationId);
        Result<AssetManifestEntryRecord> RegisterAsset(CampaignHandle campaign, string sourceFilePath, CommandId commandId, CorrelationId correlationId);
    }

    public readonly struct TokenPosition : IEquatable<TokenPosition>
    {
        public TokenPosition(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }
        public bool Equals(TokenPosition other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object? obj) => obj is TokenPosition other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
    }

    public sealed class SceneRecord
    {
        public SceneRecord(SceneId sceneId, CampaignId campaignId, string name, string status, long revision, UtcInstant createdAt, UtcInstant updatedAt)
        {
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (string.IsNullOrWhiteSpace(name) || name.Length > 128) throw new ArgumentException("Name is not safe.", nameof(name));
            if (string.IsNullOrWhiteSpace(status)) throw new ArgumentException("Status is required.", nameof(status));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));

            SceneId = sceneId;
            CampaignId = campaignId;
            Name = name;
            Status = status;
            Revision = revision;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        public SceneId SceneId { get; }
        public CampaignId CampaignId { get; }
        public string Name { get; }
        public string Status { get; }
        public long Revision { get; }
        public UtcInstant CreatedAt { get; }
        public UtcInstant UpdatedAt { get; }
    }

    public sealed class TokenRecord
    {
        public TokenRecord(TokenId tokenId, SceneId sceneId, CampaignId campaignId, TokenPosition position, UserId controllerUserId, long revision, UtcInstant createdAt, UtcInstant updatedAt)
        {
            if (!tokenId.IsValid) throw new ArgumentException("TokenId is required.", nameof(tokenId));
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!controllerUserId.IsValid) throw new ArgumentException("ControllerUserId is required.", nameof(controllerUserId));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));

            TokenId = tokenId;
            SceneId = sceneId;
            CampaignId = campaignId;
            Position = position;
            ControllerUserId = controllerUserId;
            Revision = revision;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        public TokenId TokenId { get; }
        public SceneId SceneId { get; }
        public CampaignId CampaignId { get; }
        public TokenPosition Position { get; }

        /// <summary>
        /// ODY-S03-004: 08_Scenes_And_Board section 11.1's <c>LinkedEntityRef</c>/
        /// control-ownership concept, narrowed to a single "who may move this
        /// token" user reference -- not the full ownership/control split
        /// (ADR-019 section 10, still deferred).
        /// </summary>
        public UserId ControllerUserId { get; }
        public long Revision { get; }
        public UtcInstant CreatedAt { get; }
        public UtcInstant UpdatedAt { get; }
    }

    public sealed class AssetManifestEntryRecord
    {
        public AssetManifestEntryRecord(AssetId assetId, string relativePath, string sha256Hash, long sizeBytes)
        {
            if (!assetId.IsValid) throw new ArgumentException("AssetId is required.", nameof(assetId));
            if (string.IsNullOrWhiteSpace(relativePath)) throw new ArgumentException("RelativePath is required.", nameof(relativePath));
            if (string.IsNullOrWhiteSpace(sha256Hash)) throw new ArgumentException("Hash is required.", nameof(sha256Hash));
            if (sizeBytes < 0) throw new ArgumentOutOfRangeException(nameof(sizeBytes));

            AssetId = assetId;
            RelativePath = relativePath;
            Sha256Hash = sha256Hash;
            SizeBytes = sizeBytes;
        }

        public AssetId AssetId { get; }
        public string RelativePath { get; }
        public string Sha256Hash { get; }
        public long SizeBytes { get; }
    }
}
