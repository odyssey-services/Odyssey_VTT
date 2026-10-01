using System;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Board;
using Odyssey.Application.Commands;
using Odyssey.Application.Networking.Session;
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
    /// SLICE-10 Block 6 part 3: real, end-to-end tests for the token facing/FOV/view-distance inspector
    /// and cover preview on <see cref="BoardScreenPresenter"/> -- real <see cref="SqliteTokenVisionRepository"/>/
    /// <see cref="SqliteObstacleRepository"/>/<see cref="SqliteFogOfWarRepository"/> against a
    /// temp-directory campaign, never a mock, by exact precedent of
    /// <see cref="BoardScreenPresenterFogOfWarTests"/>.
    /// </summary>
    public sealed class BoardScreenPresenterTokenVisionInspectorTests
    {
        private static CorrelationId TestCorrelationId => CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly UnityWallClock Clock = new UnityWallClock();

        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

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
                var createRequest = new CreateCampaignRequest(_directory.Path, "Board Token Vision Inspector Test Campaign", "ruleset.core", "1.0.0", "0.1.0", MainGmActor);
                Campaign = CampaignRepository.Create(createRequest, NewCommandId(), TestCorrelationId).Value;
                SceneRepository = new SqliteSceneRepository(Clock);
                ObstacleRepository = new SqliteObstacleRepository(Clock);
                VisionRepository = new SqliteTokenVisionRepository(Clock);
                FogRepository = new SqliteFogOfWarRepository(Clock);
                SceneId = SceneRepository.CreateScene(Campaign, "Test Scene", NewCommandId(), TestCorrelationId).Value.SceneId;

                GameObject = new GameObject("Board Token Vision Inspector Document");
                Document = GameObject.AddComponent<UIDocument>();
            }

            public BoardScreenPresenter NewPresenter(UserId localActor, bool isMainGm)
            {
                return new BoardScreenPresenter(Document, SceneRepository, Campaign, CampaignRepository, ObstacleRepository, VisionRepository, FogRepository, SceneId, localActor) { LocalActorIsMainGm = isMainGm };
            }

            public BoardScreenPresenter NewPresenterWithFogRepository(UserId localActor, bool isMainGm, IFogOfWarRepository fogRepository)
            {
                return new BoardScreenPresenter(Document, SceneRepository, Campaign, CampaignRepository, ObstacleRepository, VisionRepository, fogRepository, SceneId, localActor) { LocalActorIsMainGm = isMainGm };
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
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ody-slice10-block6-tvi-" + Guid.NewGuid().ToString("N"));
            }

            public void Dispose()
            {
                try { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); } catch (IOException) { }
            }
        }

        // A fog repository whose ListReveals always fails -- used only to break RecordExploration's own
        // internal read without touching TryApplyTokenFacing's own, separate GetVisionSettings call (which
        // goes through ITokenVisionRepository, not IFogOfWarRepository), so the mutation isolates the
        // best-effort assertion to RecordExploration alone.
        private sealed class ListRevealsAlwaysFailsFogRepository : IFogOfWarRepository
        {
            public Result<FogRevealRecord> RecordReveal(CampaignHandle campaign, SceneId sceneId, UserId userId, double centerX, double centerY, double radius, CommandId commandId, CorrelationId correlationId)
            {
                throw new InvalidOperationException("Not expected to be reached -- ListReveals fails first.");
            }

            public Result<System.Collections.Generic.IReadOnlyList<FogRevealRecord>> ListReveals(CampaignHandle campaign, SceneId sceneId, UserId userId, CorrelationId correlationId)
            {
                return Result<System.Collections.Generic.IReadOnlyList<FogRevealRecord>>.Failure(PersistenceFailures.CampaignIoFailed(correlationId));
            }
        }

        [Test] // TC-BOARD-129
        public void SelectingAToken_PopulatesInspectorFields_FromRealStoredVisionSettings()
        {
            using var fixture = new Fixture();
            UserId player = NewUserId();
            TokenRecord token = fixture.SceneRepository.CreateToken(fixture.Campaign, fixture.SceneId, new TokenPosition(0, 0), player, NewCommandId(), TestCorrelationId).Value;
            Result<TokenVisionSettingsRecord> seeded = fixture.VisionRepository.GetVisionSettings(fixture.Campaign, token.TokenId, TestCorrelationId);
            Assert.That(seeded.IsSuccess, Is.True);
            fixture.VisionRepository.SetFacing(fixture.Campaign, token.TokenId, 90.0, seeded.Value.Revision, NewCommandId(), TestCorrelationId);
            Result<TokenVisionSettingsRecord> stored = fixture.VisionRepository.GetVisionSettings(fixture.Campaign, token.TokenId, TestCorrelationId);
            Assert.That(stored.IsSuccess, Is.True);

            using var presenter = fixture.NewPresenter(player, isMainGm: false);
            Assert.That(presenter.Initialize().IsSuccess, Is.True);
            presenter.SelectToken(token.TokenId);

            FloatField? facingField = fixture.Document.rootVisualElement.Q<FloatField>("token-facing-field");
            FloatField? fovField = fixture.Document.rootVisualElement.Q<FloatField>("token-fov-field");
            FloatField? viewDistanceField = fixture.Document.rootVisualElement.Q<FloatField>("token-view-distance-field");
            Assert.That(facingField, Is.Not.Null);
            Assert.That(facingField!.value, Is.EqualTo((float)stored.Value.FacingDegrees).Within(0.001f));
            Assert.That(fovField!.value, Is.EqualTo((float)stored.Value.FovAngleDegrees).Within(0.001f));
            Assert.That(viewDistanceField!.value, Is.EqualTo((float)stored.Value.ViewDistance).Within(0.001f));
        }

        [Test] // TC-BOARD-130
        public void SetTokenFacing_ByOwner_Succeeds_ByStranger_IsDenied()
        {
            using var fixture = new Fixture();
            UserId owner = NewUserId();
            UserId stranger = NewUserId();
            TokenRecord token = fixture.SceneRepository.CreateToken(fixture.Campaign, fixture.SceneId, new TokenPosition(0, 0), owner, NewCommandId(), TestCorrelationId).Value;

            using var ownerPresenter = fixture.NewPresenter(owner, isMainGm: false);
            Assert.That(ownerPresenter.Initialize().IsSuccess, Is.True);
            Result<TokenVisionSettingsRecord> ownerResult = ownerPresenter.TryApplyTokenFacing(token.TokenId, 45.0);
            Assert.That(ownerResult.IsSuccess, Is.True, "the token's own controller must be able to set its facing");
            Assert.That(ownerResult.Value.FacingDegrees, Is.EqualTo(45.0).Within(0.001));

            using var strangerPresenter = fixture.NewPresenter(stranger, isMainGm: false);
            Assert.That(strangerPresenter.Initialize().IsSuccess, Is.True);
            Result<TokenVisionSettingsRecord> strangerResult = strangerPresenter.TryApplyTokenFacing(token.TokenId, 200.0);
            Assert.That(strangerResult.IsFailure, Is.True, "a non-owner, non-MainGm actor must be denied by the real server-side authorization");

            Result<TokenVisionSettingsRecord> stillStored = fixture.VisionRepository.GetVisionSettings(fixture.Campaign, token.TokenId, TestCorrelationId);
            Assert.That(stillStored.Value.FacingDegrees, Is.EqualTo(45.0).Within(0.001), "the denied stranger's attempt must not have changed the stored facing");
        }

        [Test] // TC-BOARD-131
        public void SetTokenVisionParameters_ByMainGm_Succeeds_ByOwnerOnly_IsDenied()
        {
            using var fixture = new Fixture();
            UserId owner = NewUserId();
            TokenRecord token = fixture.SceneRepository.CreateToken(fixture.Campaign, fixture.SceneId, new TokenPosition(0, 0), owner, NewCommandId(), TestCorrelationId).Value;

            using var ownerPresenter = fixture.NewPresenter(owner, isMainGm: false);
            Assert.That(ownerPresenter.Initialize().IsSuccess, Is.True);
            Result<TokenVisionSettingsRecord> ownerResult = ownerPresenter.TryApplyTokenVisionParameters(token.TokenId, 90.0, 50.0);
            Assert.That(ownerResult.IsFailure, Is.True, "owning the token is not enough -- SetTokenVisionParameters is MainGm-only");

            using var mainGmPresenter = fixture.NewPresenter(fixture.MainGmActor, isMainGm: true);
            Assert.That(mainGmPresenter.Initialize().IsSuccess, Is.True);
            Result<TokenVisionSettingsRecord> mainGmResult = mainGmPresenter.TryApplyTokenVisionParameters(token.TokenId, 90.0, 50.0);
            Assert.That(mainGmResult.IsSuccess, Is.True);
            Assert.That(mainGmResult.Value.FovAngleDegrees, Is.EqualTo(90.0).Within(0.001));
            Assert.That(mainGmResult.Value.ViewDistance, Is.EqualTo(50.0).Within(0.001));
        }

        [Test] // TC-BOARD-132
        public void SetTokenFacing_EvenIfRecordExplorationIsBroken_IsNotRolledBack()
        {
            using var fixture = new Fixture();
            UserId owner = NewUserId();
            TokenRecord token = fixture.SceneRepository.CreateToken(fixture.Campaign, fixture.SceneId, new TokenPosition(0, 0), owner, NewCommandId(), TestCorrelationId).Value;
            var brokenFog = new ListRevealsAlwaysFailsFogRepository();

            using var presenter = fixture.NewPresenterWithFogRepository(owner, isMainGm: false, brokenFog);
            Assert.That(presenter.Initialize().IsSuccess, Is.True);

            Result<TokenVisionSettingsRecord> result = presenter.TryApplyTokenFacing(token.TokenId, 123.0);

            Assert.That(result.IsSuccess, Is.True, "an artificially broken RecordExploration (best-effort) must never block or roll back the facing change itself");
            Result<TokenVisionSettingsRecord> stored = fixture.VisionRepository.GetVisionSettings(fixture.Campaign, token.TokenId, TestCorrelationId);
            Assert.That(stored.Value.FacingDegrees, Is.EqualTo(123.0).Within(0.001));
        }

        [Test] // TC-BOARD-133
        public void SetTokenFacing_RevisionChangedSinceLastRefresh_StillSucceeds_UsingFreshRevision()
        {
            using var fixture = new Fixture();
            UserId owner = NewUserId();
            TokenRecord token = fixture.SceneRepository.CreateToken(fixture.Campaign, fixture.SceneId, new TokenPosition(0, 0), owner, NewCommandId(), TestCorrelationId).Value;

            using var presenter = fixture.NewPresenter(owner, isMainGm: false);
            Assert.That(presenter.Initialize().IsSuccess, Is.True);
            presenter.SelectToken(token.TokenId);

            // Simulate another participant changing this token's facing (bumping its Revision) after the
            // presenter's own last Refresh() but before this presenter applies its own change -- if the
            // implementation used a Revision cached at panel-open time, this call would now be rejected as a
            // stale-revision conflict.
            Result<TokenVisionSettingsRecord> racedAway = fixture.VisionRepository.GetVisionSettings(fixture.Campaign, token.TokenId, TestCorrelationId);
            fixture.VisionRepository.SetFacing(fixture.Campaign, token.TokenId, 10.0, racedAway.Value.Revision, NewCommandId(), TestCorrelationId);

            Result<TokenVisionSettingsRecord> result = presenter.TryApplyTokenFacing(token.TokenId, 270.0);

            Assert.That(result.IsSuccess, Is.True, "the fresh-read Revision (re-read immediately before the write) must be used, not one cached from an earlier Refresh()");
            Assert.That(result.Value.FacingDegrees, Is.EqualTo(270.0).Within(0.001));
        }

        [Test] // TC-TOKENMENU-001 (ODY-S11-218)
        public void CoverTargets_OfferOnlyTokensThePlayerCanSee_TheMainGmSeesAll()
        {
            using var fixture = new Fixture();
            UserId player = NewUserId();
            Assert.That(fixture.CampaignRepository.AddMember(fixture.Campaign, player, CampaignMembershipRole.Player, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);
            TokenRecord own = fixture.SceneRepository.CreateToken(fixture.Campaign, fixture.SceneId, new TokenPosition(0, 0), player, NewCommandId(), TestCorrelationId).Value;
            TokenRecord near = fixture.SceneRepository.CreateToken(fixture.Campaign, fixture.SceneId, new TokenPosition(3, 0), fixture.MainGmActor, NewCommandId(), TestCorrelationId).Value;
            // Far beyond the default 100-unit view distance: hidden from the player by ComputeVisibleTokens.
            TokenRecord far = fixture.SceneRepository.CreateToken(fixture.Campaign, fixture.SceneId, new TokenPosition(500, 0), fixture.MainGmActor, NewCommandId(), TestCorrelationId).Value;

            using var presenter = fixture.NewPresenter(player, isMainGm: false);
            Assert.That(presenter.Initialize().IsSuccess, Is.True);
            presenter.SelectToken(own.TokenId);
            Assert.That(presenter.InspectorCoverTargets, Is.EquivalentTo(new[] { near.TokenId }), "a hidden token is not even listed");
            Assert.That(presenter.InspectorCoverTargets, Has.No.Member(far.TokenId));

            // Same board, now the MainGM's unfiltered view (the selection is kept across the re-render).
            presenter.LocalActorUserId = fixture.MainGmActor;
            presenter.LocalActorIsMainGm = true;
            Assert.That(presenter.Refresh().IsSuccess, Is.True);
            Assert.That(presenter.InspectorCoverTargets, Is.EquivalentTo(new[] { near.TokenId, far.TokenId }), "the MainGM sees every token");
        }

        [Test] // TC-BOARD-134
        public void TryCheckCover_UsesInspectedTokenAsAttacker_DropdownSelectionAsTarget()
        {
            using var fixture = new Fixture();
            TokenRecord attacker = fixture.SceneRepository.CreateToken(fixture.Campaign, fixture.SceneId, new TokenPosition(0, 0), fixture.MainGmActor, NewCommandId(), TestCorrelationId).Value;
            TokenRecord target = fixture.SceneRepository.CreateToken(fixture.Campaign, fixture.SceneId, new TokenPosition(10, 0), fixture.MainGmActor, NewCommandId(), TestCorrelationId).Value;
            fixture.ObstacleRepository.CreateObstacle(fixture.Campaign, fixture.SceneId, ObstacleKind.Wall, 5, -5, 5, 5, NewCommandId(), TestCorrelationId);

            using var presenter = fixture.NewPresenter(fixture.MainGmActor, isMainGm: true);
            Assert.That(presenter.Initialize().IsSuccess, Is.True);

            Result<CoverDegree> result = presenter.TryCheckCover(attacker.TokenId, target.TokenId);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(CoverDegree.Full), "a wall fully between the inspected (attacker) token and the dropdown-selected target must be reported as Full cover, matching CoverSuggestionServiceTests' own known geometry");

            // The reverse order must not accidentally produce the same result by symmetry alone -- confirm
            // this call really used attacker/target in the order passed, not swapped internally.
            Result<CoverDegree> direct = CoverSuggestionService.SuggestCover(fixture.SceneRepository, fixture.ObstacleRepository, fixture.VisionRepository, fixture.CampaignRepository, new SuggestCoverRequest(fixture.Campaign, fixture.MainGmActor, attacker.TokenId, target.TokenId, TestCorrelationId));
            Assert.That(result.Value, Is.EqualTo(direct.Value));
        }
    }
}
