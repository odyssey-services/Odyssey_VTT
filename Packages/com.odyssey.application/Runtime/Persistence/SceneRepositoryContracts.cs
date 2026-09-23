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

        /// <summary>
        /// ODY-S06-104: <paramref name="characterId"/> is an optional "one token = one
        /// Character" link (08_Scenes_And_Board's own token vocabulary; a token with no
        /// Character is a prop/marker, still fully supported). Set once at creation --
        /// this MVP scope has no re-linking command; a token created without one stays
        /// without one. Defaulted to preserve every pre-existing caller unchanged.
        /// </summary>
        Result<TokenRecord> CreateToken(CampaignHandle campaign, SceneId sceneId, TokenPosition initialPosition, UserId controllerUserId, CommandId commandId, CorrelationId correlationId, CharacterId? characterId = null);
        Result<TokenRecord> GetToken(CampaignHandle campaign, TokenId tokenId, CorrelationId correlationId);
        Result<TokenRecord> MoveToken(CampaignHandle campaign, TokenId tokenId, TokenPosition newPosition, long expectedRevision, CommandId commandId, CorrelationId correlationId);
        Result<IReadOnlyList<TokenRecord>> ListTokens(CampaignHandle campaign, SceneId sceneId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S06-104: campaign-wide (not Scene-scoped) lookup of every token currently
        /// linked to this Character -- needed because <see cref="Odyssey.Domain.Combat.AttackIntent"/>'s
        /// own CombatEncounter carries no SceneId (deliberately scene-agnostic), so the
        /// Scene a Character's token lives on cannot be known in advance the way
        /// <see cref="ListTokens"/>'s own SceneId-scoped listing assumes. Ordinarily at
        /// most one result (the "one token = one Character" convention), but this
        /// returns every match rather than asserting uniqueness the schema itself does
        /// not enforce.
        /// </summary>
        Result<IReadOnlyList<TokenRecord>> ListTokensByCharacter(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId);
        Result<AssetManifestEntryRecord> RegisterAsset(CampaignHandle campaign, string sourceFilePath, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S07-105: sets or clears a Scene's background/map, the first
        /// mutation of <see cref="SceneRecord"/> since <see cref="CreateScene"/>
        /// itself. <paramref name="backgroundAssetId"/> is <c>null</c> to clear
        /// an existing background -- a Scene with no background is a valid
        /// state, not an error. Fail-closed against a non-null
        /// <paramref name="backgroundAssetId"/> that does not exist in this same
        /// campaign's own `AssetManifestEntries` (each campaign owns a separate
        /// database file, so "exists in this campaign" and "exists at all,
        /// scoped to this campaign" are the same lookup) -- deliberately not the
        /// unvalidated, unlinked-to-any-registry antipattern `CharacterRecord.PortraitReference`
        /// already is. By exact precedent of <see cref="MoveToken"/>: takes
        /// <paramref name="expectedRevision"/> (optimistic concurrency, checked
        /// atomically inside the same transaction as the update) and
        /// <paramref name="commandId"/> (idempotent replay through the same
        /// <c>SqliteSavingPipeline</c> journal-projection transaction every other
        /// mutating method here already uses).
        /// </summary>
        Result<SceneRecord> SetSceneBackground(CampaignHandle campaign, SceneId sceneId, AssetId? backgroundAssetId, long expectedRevision, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// ODY-S07-106: sets or clears a token's own portrait
        /// (<see cref="TokenRecord.PortraitAssetId"/>), by exact precedent of
        /// <see cref="SetSceneBackground"/> (single record revision,
        /// fail-closed against this campaign's own `AssetManifestEntries`,
        /// `AssetReferences` `ReferencedByType` = "Token" kept to one current row).
        /// A token's portrait is an independent field -- it is NOT inherited
        /// from the linked Character's portrait, and no such inheritance is
        /// introduced.
        /// </summary>
        Result<TokenRecord> SetTokenPortrait(CampaignHandle campaign, TokenId tokenId, AssetId? portraitAssetId, long expectedRevision, CommandId commandId, CorrelationId correlationId);
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
        public SceneRecord(SceneId sceneId, CampaignId campaignId, string name, string status, long revision, UtcInstant createdAt, UtcInstant updatedAt, AssetId? backgroundAssetId = null)
        {
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (string.IsNullOrWhiteSpace(name) || name.Length > 128) throw new ArgumentException("Name is not safe.", nameof(name));
            if (string.IsNullOrWhiteSpace(status)) throw new ArgumentException("Status is required.", nameof(status));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
            if (backgroundAssetId.HasValue && !backgroundAssetId.Value.IsValid) throw new ArgumentException("BackgroundAssetId must be valid when supplied.", nameof(backgroundAssetId));

            SceneId = sceneId;
            CampaignId = campaignId;
            Name = name;
            Status = status;
            Revision = revision;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
            BackgroundAssetId = backgroundAssetId;
        }

        public SceneId SceneId { get; }
        public CampaignId CampaignId { get; }
        public string Name { get; }
        public string Status { get; }
        public long Revision { get; }
        public UtcInstant CreatedAt { get; }
        public UtcInstant UpdatedAt { get; }

        /// <summary>ODY-S07-105: the Scene's own background/map -- a real, validated exact-reference to an already-registered `AssetManifestEntries` row (set via <see cref="ISceneRepository.SetSceneBackground"/>), or `null` for a Scene with no background (a valid, non-error state). No origin/scale/placement metadata -- an explicit non-goal, see that method's own doc comment.</summary>
        public AssetId? BackgroundAssetId { get; }
    }

    public sealed class TokenRecord
    {
        public TokenRecord(TokenId tokenId, SceneId sceneId, CampaignId campaignId, TokenPosition position, UserId controllerUserId, long revision, UtcInstant createdAt, UtcInstant updatedAt, CharacterId? characterId = null, AssetId? portraitAssetId = null)
        {
            if (!tokenId.IsValid) throw new ArgumentException("TokenId is required.", nameof(tokenId));
            if (portraitAssetId.HasValue && !portraitAssetId.Value.IsValid) throw new ArgumentException("PortraitAssetId must be valid when supplied.", nameof(portraitAssetId));
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!controllerUserId.IsValid) throw new ArgumentException("ControllerUserId is required.", nameof(controllerUserId));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
            if (characterId.HasValue && !characterId.Value.IsValid) throw new ArgumentException("CharacterId must be valid when supplied.", nameof(characterId));

            TokenId = tokenId;
            SceneId = sceneId;
            CampaignId = campaignId;
            Position = position;
            ControllerUserId = controllerUserId;
            Revision = revision;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
            CharacterId = characterId;
            PortraitAssetId = portraitAssetId;
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

        /// <summary>ODY-S06-104: the optional "one token = one Character" link -- null for a prop/marker token with no Character behind it.</summary>
        public CharacterId? CharacterId { get; }

        /// <summary>ODY-S07-106: the token's own validated portrait (set via <see cref="ISceneRepository.SetTokenPortrait"/>), or `null`. Independent of the linked Character's portrait -- never auto-inherited.</summary>
        public AssetId? PortraitAssetId { get; }
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
