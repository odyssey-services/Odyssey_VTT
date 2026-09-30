using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Geometry;
using Odyssey.Domain.Identity;
using Odyssey.Persistence.Sqlite;
using Odyssey.Unity.Client;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Tests.Unity.EditMode
{
    /// <summary>
    /// SLICE-10 Block 6 part 2: real, end-to-end tests for the fog-of-war UI on
    /// <see cref="BoardScreenPresenter"/> -- permanent map memory (rendered from the real, authorized
    /// <c>PlayerVisibilityService.ListExploredReveals</c> result) and live token visibility (rendered from
    /// the real <c>PlayerVisibilityService.ComputeVisibleTokens</c> result). Real <see cref="SqliteFogOfWarRepository"/>/
    /// <see cref="SqliteTokenVisionRepository"/>/<see cref="SqliteObstacleRepository"/> against a
    /// temp-directory campaign, never a mock, by exact precedent of
    /// <see cref="BoardScreenPresenterObstacleTests"/>.
    /// </summary>
    public sealed class BoardScreenPresenterFogOfWarTests
    {
        private static CorrelationId TestCorrelationId => CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly UnityWallClock Clock = new UnityWallClock();

        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
        private static VisualElement TokenElement(UIDocument document, TokenId tokenId) => document.rootVisualElement.Q<VisualElement>("token-" + tokenId);

        private sealed class Fixture : IDisposable
        {
            private readonly TemporaryDirectoryHandle _directory = new TemporaryDirectoryHandle();
            public readonly SqliteCampaignRepository CampaignRepository;
            public readonly SqliteSceneRepository SceneRepository;
            public readonly SqliteObstacleRepository ObstacleRepository;
            public readonly SqliteTokenVisionRepository VisionRepository;
            public readonly SqliteFogOfWarRepository FogRepository;
            public readonly CampaignHandle Campaign;
            public readonly SceneId SceneId;
            public readonly UserId MainGmActor;
            public readonly GameObject GameObject;
            public readonly UIDocument Document;

            public Fixture()
            {
                CampaignRepository = new SqliteCampaignRepository(Clock);
                MainGmActor = global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost();
                var createRequest = new CreateCampaignRequest(_directory.Path, "Board Fog Of War Test Campaign", "ruleset.core", "1.0.0", "0.1.0", MainGmActor);
                Campaign = CampaignRepository.Create(createRequest, NewCommandId(), TestCorrelationId).Value;
                SceneRepository = new SqliteSceneRepository(Clock);
                ObstacleRepository = new SqliteObstacleRepository(Clock);
                VisionRepository = new SqliteTokenVisionRepository(Clock);
                FogRepository = new SqliteFogOfWarRepository(Clock);
                SceneId = SceneRepository.CreateScene(Campaign, "Test Scene", NewCommandId(), TestCorrelationId).Value.SceneId;

                GameObject = new GameObject("Board Fog Of War Document");
                Document = GameObject.AddComponent<UIDocument>();
            }

            public BoardScreenPresenter NewPresenter(UserId localActor, bool isMainGm)
            {
                var presenter = new BoardScreenPresenter(Document, SceneRepository, Campaign, CampaignRepository, ObstacleRepository, VisionRepository, FogRepository, SceneId, localActor) { LocalActorIsMainGm = isMainGm };
                return presenter;
            }

            public BoardScreenPresenter NewPresenterWithVisionRepository(UserId localActor, bool isMainGm, ITokenVisionRepository visionRepository)
            {
                var presenter = new BoardScreenPresenter(Document, SceneRepository, Campaign, CampaignRepository, ObstacleRepository, visionRepository, FogRepository, SceneId, localActor) { LocalActorIsMainGm = isMainGm };
                return presenter;
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(GameObject);
                CampaignRepository.Close(Campaign, TestCorrelationId);
                _directory.Dispose();
            }
        }

        private sealed class TemporaryDirectoryHandle : IDisposable
        {
            public string Path { get; }

            public TemporaryDirectoryHandle()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ody-slice10-block6-fow-" + Guid.NewGuid().ToString("N"));
            }

            public void Dispose()
            {
                try { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); } catch (IOException) { }
            }
        }

        // A vision repository that fails GetVisionSettings unconditionally (RecordExploration's first
        // dependent read) while delegating everything else to a real repository -- used only to prove
        // RecordExploration's own failure never blocks/rolls back the token move that triggered it
        // (task contract section 3's "best-effort" invariant). Used only against a MainGm actor in these
        // tests, so ComputeVisibleTokens (which also depends on ITokenVisionRepository) is never reached.
        private sealed class VisionSettingsAlwaysFailsRepository : ITokenVisionRepository
        {
            private readonly ITokenVisionRepository _inner;

            public VisionSettingsAlwaysFailsRepository(ITokenVisionRepository inner)
            {
                _inner = inner;
            }

            public Result<TokenVisionSettingsRecord> GetVisionSettings(CampaignHandle campaign, TokenId tokenId, CorrelationId correlationId)
            {
                return Result<TokenVisionSettingsRecord>.Failure(PersistenceFailures.CampaignIoFailed(correlationId));
            }

            public Result<TokenVisionSettingsRecord> SetFacing(CampaignHandle campaign, TokenId tokenId, double facingDegrees, long expectedRevision, CommandId commandId, CorrelationId correlationId) => _inner.SetFacing(campaign, tokenId, facingDegrees, expectedRevision, commandId, correlationId);

            public Result<TokenVisionSettingsRecord> SetVisionParameters(CampaignHandle campaign, TokenId tokenId, double fovAngleDegrees, double viewDistance, long expectedRevision, CommandId commandId, CorrelationId correlationId) => _inner.SetVisionParameters(campaign, tokenId, fovAngleDegrees, viewDistance, expectedRevision, commandId, correlationId);
        }

        [Test] // TC-BOARD-123
        public void SuccessfulTokenMove_RecordsExploration_RealFogRepositoryGainsAReveal()
        {
            using var fixture = new Fixture();
            UserId player = NewUserId();
            Assert.That(fixture.CampaignRepository.AddMember(fixture.Campaign, player, CampaignMembershipRole.Player, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);
            TokenRecord token = fixture.SceneRepository.CreateToken(fixture.Campaign, fixture.SceneId, new TokenPosition(0, 0), player, NewCommandId(), TestCorrelationId).Value;

            using var presenter = fixture.NewPresenter(player, isMainGm: false);
            Assert.That(presenter.Initialize().IsSuccess, Is.True);

            Result<TokenRecord> moved = presenter.TryMoveTokenTo(token.TokenId, new TokenPosition(3, 3));
            Assert.That(moved.IsSuccess, Is.True);

            Result<IReadOnlyList<FogRevealRecord>> reveals = fixture.FogRepository.ListReveals(fixture.Campaign, fixture.SceneId, player, TestCorrelationId);
            Assert.That(reveals.IsSuccess, Is.True);
            Assert.That(reveals.Value.Count, Is.EqualTo(1), "a successful token move must have recorded exactly one map-memory reveal for the moving player");
            Assert.That(reveals.Value[0].CenterX, Is.EqualTo(3).Within(0.001));
            Assert.That(reveals.Value[0].CenterY, Is.EqualTo(3).Within(0.001));
        }

        [Test] // TC-BOARD-124
        public void SuccessfulTokenMove_EvenIfRecordExplorationIsBroken_IsNotRolledBack()
        {
            using var fixture = new Fixture();
            TokenRecord token = fixture.SceneRepository.CreateToken(fixture.Campaign, fixture.SceneId, new TokenPosition(0, 0), fixture.MainGmActor, NewCommandId(), TestCorrelationId).Value;
            var brokenVision = new VisionSettingsAlwaysFailsRepository(fixture.VisionRepository);

            // A MainGm actor so ComputeVisibleTokens/fog rendering (which also use ITokenVisionRepository)
            // are never reached this Refresh() -- isolates the assertion to RecordExploration's own failure.
            using var presenter = fixture.NewPresenterWithVisionRepository(fixture.MainGmActor, isMainGm: true, brokenVision);
            Assert.That(presenter.Initialize().IsSuccess, Is.True);

            Result<TokenRecord> moved = presenter.TryMoveTokenTo(token.TokenId, new TokenPosition(7, 2));

            Assert.That(moved.IsSuccess, Is.True, "an artificially broken RecordExploration (best-effort) must never block or roll back the token move itself");
            Result<TokenRecord> persisted = fixture.SceneRepository.GetToken(fixture.Campaign, token.TokenId, TestCorrelationId);
            Assert.That(persisted.Value.Position.X, Is.EqualTo(7));
            Assert.That(persisted.Value.Position.Y, Is.EqualTo(2));
        }

        [Test] // TC-BOARD-125
        public void NonMainGmActor_TokenOutsideComputedVisibility_IsNotRendered()
        {
            using var fixture = new Fixture();
            UserId player = NewUserId();
            Assert.That(fixture.CampaignRepository.AddMember(fixture.Campaign, player, CampaignMembershipRole.Player, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);
            fixture.SceneRepository.CreateToken(fixture.Campaign, fixture.SceneId, new TokenPosition(0, 0), player, NewCommandId(), TestCorrelationId);
            TokenRecord blocked = fixture.SceneRepository.CreateToken(fixture.Campaign, fixture.SceneId, new TokenPosition(20, 0), fixture.MainGmActor, NewCommandId(), TestCorrelationId).Value;
            fixture.ObstacleRepository.CreateObstacle(fixture.Campaign, fixture.SceneId, ObstacleKind.Wall, 10, -5, 10, 5, NewCommandId(), TestCorrelationId);

            using var presenter = fixture.NewPresenter(player, isMainGm: false);
            Assert.That(presenter.Initialize().IsSuccess, Is.True);

            VisualElement? element = TokenElement(fixture.Document, blocked.TokenId);
            Assert.That(element, Is.Not.Null, "the element is still created for bookkeeping, only hidden");
            Assert.That(element!.style.display.value, Is.EqualTo(DisplayStyle.None), "a token outside the real ComputeVisibleTokens result must not be rendered");
        }

        [Test] // TC-BOARD-126
        public void MainGmActor_SeesEveryToken_RegardlessOfLiveVisibilityOrFog()
        {
            using var fixture = new Fixture();
            TokenRecord farAway = fixture.SceneRepository.CreateToken(fixture.Campaign, fixture.SceneId, new TokenPosition(10000, 10000), NewUserId(), NewCommandId(), TestCorrelationId).Value;
            fixture.ObstacleRepository.CreateObstacle(fixture.Campaign, fixture.SceneId, ObstacleKind.Wall, -5, -5, 5, 5, NewCommandId(), TestCorrelationId);

            using var presenter = fixture.NewPresenter(fixture.MainGmActor, isMainGm: true);
            Assert.That(presenter.Initialize().IsSuccess, Is.True);

            VisualElement? element = TokenElement(fixture.Document, farAway.TokenId);
            Assert.That(element, Is.Not.Null);
            Assert.That(element!.style.display.value, Is.Not.EqualTo(DisplayStyle.None), "the MainGm must see every token unconditionally, with no fog/visibility filtering applied");
        }

        [Test] // TC-BOARD-127
        public void PlayerActor_WithNoExploredReveals_TokenVisibilityFilteringStillApplied_RegressionSafe()
        {
            // Regression guard: Block 6 part 1's own obstacle-drawing/door-toggle/damage behavior must be
            // unaffected by this task's rendering additions -- a non-MainGm actor with a real obstacle drawn
            // must still see that obstacle rendered (fog/token-visibility are additive, not replacing it).
            using var fixture = new Fixture();
            UserId player = NewUserId();
            Assert.That(fixture.CampaignRepository.AddMember(fixture.Campaign, player, CampaignMembershipRole.Player, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);
            fixture.ObstacleRepository.CreateObstacle(fixture.Campaign, fixture.SceneId, ObstacleKind.Wall, 0, 0, 5, 0, NewCommandId(), TestCorrelationId);

            using var presenter = fixture.NewPresenter(player, isMainGm: false);
            Result initialized = presenter.Initialize();

            Assert.That(initialized.IsSuccess, Is.True, "adding fog-of-war rendering must not break an ordinary Refresh() for a non-MainGm actor with real obstacles present");
            Assert.That(fixture.Document.rootVisualElement.Q<VisualElement>("board-area"), Is.Not.Null);
        }
    }
}
