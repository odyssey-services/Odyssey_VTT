using System;
using System.Collections.Generic;
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
    /// identity, name, status/revision, and bare token position. Writes go directly
    /// to normalized current-state tables (ADR-011 section 8.1's hybrid-schema
    /// principle); the Domain Event Store / transactional journal-projection
    /// pipeline (ADR-012 section 5) is ODY-S01-009 scope, not implemented here.
    /// </summary>
    public interface ISceneRepository
    {
        Result<SceneRecord> CreateScene(CampaignHandle campaign, string sceneName, CorrelationId correlationId);
        Result<TokenRecord> CreateToken(CampaignHandle campaign, SceneId sceneId, TokenPosition initialPosition, CorrelationId correlationId);
        Result<TokenRecord> MoveToken(CampaignHandle campaign, TokenId tokenId, TokenPosition newPosition, CorrelationId correlationId);
        Result<IReadOnlyList<TokenRecord>> ListTokens(CampaignHandle campaign, SceneId sceneId, CorrelationId correlationId);
        Result<AssetManifestEntryRecord> RegisterAsset(CampaignHandle campaign, string sourceFilePath, CorrelationId correlationId);
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
        public TokenRecord(TokenId tokenId, SceneId sceneId, CampaignId campaignId, TokenPosition position, long revision, UtcInstant createdAt, UtcInstant updatedAt)
        {
            if (!tokenId.IsValid) throw new ArgumentException("TokenId is required.", nameof(tokenId));
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));

            TokenId = tokenId;
            SceneId = sceneId;
            CampaignId = campaignId;
            Position = position;
            Revision = revision;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        public TokenId TokenId { get; }
        public SceneId SceneId { get; }
        public CampaignId CampaignId { get; }
        public TokenPosition Position { get; }
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
