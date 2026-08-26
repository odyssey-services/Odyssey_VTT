using System;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Board;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence.Board
{
    /// <summary>
    /// ODY-S03-004: real, SQLite-backed authoritative-movement tests. Uses the
    /// real <see cref="SqliteSceneRepository"/> throughout, not a fake/mock --
    /// unlike ODY-S02-011's in-memory network prototype, this task's whole
    /// premise is durable campaign persistence, so the real repository is the
    /// only meaningful test double (see task contract section 3).
    /// </summary>
    public sealed class BoardMovementServiceTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private string _workDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;
        private SqliteSceneRepository _sceneRepository = null!;
        private SceneId _sceneId;

        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        [SetUp]
        public void SetUp()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "ody-s03-004-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_workDir, "Board Movement Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = _campaignRepository.Create(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;

            _sceneRepository = new SqliteSceneRepository(Clock);
            _sceneId = _sceneRepository.CreateScene(_campaign, "Board Movement Test Scene", NewCommandId(), TestCorrelationId).Value.SceneId;
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaignRepository.Close(_campaign, TestCorrelationId); } catch (IOException) { }
            try { if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true); } catch (IOException) { }
        }

        [Test]
        public void MoveToken_ByController_Succeeds_AndAdvancesRevision()
        {
            // TC-BOARD-004
            UserId controller = NewUserId();
            TokenRecord token = _sceneRepository.CreateToken(_campaign, _sceneId, new TokenPosition(0, 0), controller, NewCommandId(), TestCorrelationId).Value;

            var request = new MoveTokenRequest(_campaign, controller, actorIsMainGm: false, token.TokenId, new TokenPosition(3, 4), token.Revision, NewCommandId(), TestCorrelationId);
            Result<TokenRecord> result = BoardMovementService.MoveToken(_sceneRepository, request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Position.X, Is.EqualTo(3));
            Assert.That(result.Value.Position.Y, Is.EqualTo(4));
            Assert.That(result.Value.Revision, Is.EqualTo(token.Revision + 1));
        }

        [Test]
        public void MoveToken_ByNonControllerNonMainGm_IsRejected_TokenPositionUnchanged()
        {
            // TC-BOARD-005 (exit criterion 2: "Player не может перемещать чужой токен без control")
            UserId controller = NewUserId();
            UserId otherPlayer = NewUserId();
            TokenRecord token = _sceneRepository.CreateToken(_campaign, _sceneId, new TokenPosition(1, 1), controller, NewCommandId(), TestCorrelationId).Value;

            var request = new MoveTokenRequest(_campaign, otherPlayer, actorIsMainGm: false, token.TokenId, new TokenPosition(9, 9), token.Revision, NewCommandId(), TestCorrelationId);
            Result<TokenRecord> result = BoardMovementService.MoveToken(_sceneRepository, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.BoardTokenMoveDenied));
            Assert.That(result.Error.SafeReasonCode, Is.EqualTo(SafeReasonCode.PermissionDenied));

            TokenRecord unchanged = _sceneRepository.GetToken(_campaign, token.TokenId, TestCorrelationId).Value;
            Assert.That(unchanged.Position.X, Is.EqualTo(1));
            Assert.That(unchanged.Position.Y, Is.EqualTo(1));
            Assert.That(unchanged.Revision, Is.EqualTo(1), "a rejected move must not advance the revision");
        }

        [Test]
        public void MoveToken_ByMainGm_OnAnyoneElsesToken_Succeeds()
        {
            // TC-BOARD-006
            UserId controller = NewUserId();
            UserId mainGm = NewUserId();
            TokenRecord token = _sceneRepository.CreateToken(_campaign, _sceneId, new TokenPosition(0, 0), controller, NewCommandId(), TestCorrelationId).Value;

            var request = new MoveTokenRequest(_campaign, mainGm, actorIsMainGm: true, token.TokenId, new TokenPosition(7, 7), token.Revision, NewCommandId(), TestCorrelationId);
            Result<TokenRecord> result = BoardMovementService.MoveToken(_sceneRepository, request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Position.X, Is.EqualTo(7));
        }

        [Test]
        public void MoveToken_WithStaleExpectedRevision_IsRejected_NoMutation()
        {
            // TC-BOARD-007 (ADR-002 section 10.2's optimistic concurrency)
            UserId controller = NewUserId();
            TokenRecord token = _sceneRepository.CreateToken(_campaign, _sceneId, new TokenPosition(0, 0), controller, NewCommandId(), TestCorrelationId).Value;

            // A first move advances the revision to 2.
            var firstMove = new MoveTokenRequest(_campaign, controller, actorIsMainGm: false, token.TokenId, new TokenPosition(1, 1), token.Revision, NewCommandId(), TestCorrelationId);
            Assert.That(BoardMovementService.MoveToken(_sceneRepository, firstMove).IsSuccess, Is.True);

            // A second move submitted with the now-stale ExpectedRevision (1, not 2).
            var staleMove = new MoveTokenRequest(_campaign, controller, actorIsMainGm: false, token.TokenId, new TokenPosition(2, 2), token.Revision, NewCommandId(), TestCorrelationId);
            Result<TokenRecord> result = BoardMovementService.MoveToken(_sceneRepository, staleMove);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceTokenRevisionConflict));
            Assert.That(result.Error.SafeReasonCode, Is.EqualTo(SafeReasonCode.StateChanged));

            TokenRecord unchanged = _sceneRepository.GetToken(_campaign, token.TokenId, TestCorrelationId).Value;
            Assert.That(unchanged.Position.X, Is.EqualTo(1), "the stale-revision move must not have applied");
            Assert.That(unchanged.Revision, Is.EqualTo(2));
        }

        [Test]
        public void MoveToken_ToPositionAlreadyOccupiedByAnotherToken_IsRejected()
        {
            // TC-BOARD-008 (BOARD-INV-009)
            UserId controller = NewUserId();
            TokenRecord tokenA = _sceneRepository.CreateToken(_campaign, _sceneId, new TokenPosition(0, 0), controller, NewCommandId(), TestCorrelationId).Value;
            TokenRecord tokenB = _sceneRepository.CreateToken(_campaign, _sceneId, new TokenPosition(5, 5), controller, NewCommandId(), TestCorrelationId).Value;

            var request = new MoveTokenRequest(_campaign, controller, actorIsMainGm: false, tokenA.TokenId, new TokenPosition(5, 5), tokenA.Revision, NewCommandId(), TestCorrelationId);
            Result<TokenRecord> result = BoardMovementService.MoveToken(_sceneRepository, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.BoardTokenDestinationOccupied));

            TokenRecord unchangedA = _sceneRepository.GetToken(_campaign, tokenA.TokenId, TestCorrelationId).Value;
            Assert.That(unchangedA.Position.X, Is.EqualTo(0));
            Assert.That(unchangedA.Revision, Is.EqualTo(1));
        }

        [Test]
        public void MoveToken_ToNonFiniteDestination_IsRejected_BeforeAnyPersistenceCall()
        {
            // TC-BOARD-009 (ADR-020 section 4.2)
            UserId controller = NewUserId();
            TokenRecord token = _sceneRepository.CreateToken(_campaign, _sceneId, new TokenPosition(0, 0), controller, NewCommandId(), TestCorrelationId).Value;

            var request = new MoveTokenRequest(_campaign, controller, actorIsMainGm: false, token.TokenId, new TokenPosition(double.NaN, 0), token.Revision, NewCommandId(), TestCorrelationId);
            Result<TokenRecord> result = BoardMovementService.MoveToken(_sceneRepository, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.BoardTokenDestinationInvalid));

            TokenRecord unchanged = _sceneRepository.GetToken(_campaign, token.TokenId, TestCorrelationId).Value;
            Assert.That(unchanged.Revision, Is.EqualTo(1));
        }

        [Test]
        public void UndoMoveToken_RestoresPreviousPosition_AsNewCompensatingCommand_NotBlindRollback()
        {
            // TC-BOARD-010 (08_Scenes_And_Board section 21.5, BOARD-INV-030 -- exit criterion 7)
            UserId controller = NewUserId();
            TokenPosition original = new TokenPosition(1, 1);
            TokenRecord token = _sceneRepository.CreateToken(_campaign, _sceneId, original, controller, NewCommandId(), TestCorrelationId).Value;

            var move = new MoveTokenRequest(_campaign, controller, actorIsMainGm: false, token.TokenId, new TokenPosition(8, 8), token.Revision, NewCommandId(), TestCorrelationId);
            Result<TokenRecord> moved = BoardMovementService.MoveToken(_sceneRepository, move);
            Assert.That(moved.IsSuccess, Is.True);
            Assert.That(moved.Value.Revision, Is.EqualTo(2));

            var undo = new MoveTokenRequest(_campaign, controller, actorIsMainGm: false, token.TokenId, original, moved.Value.Revision, NewCommandId(), TestCorrelationId);
            Result<TokenRecord> undone = BoardMovementService.UndoMoveToken(_sceneRepository, undo);

            Assert.That(undone.IsSuccess, Is.True);
            Assert.That(undone.Value.Position.X, Is.EqualTo(1));
            Assert.That(undone.Value.Position.Y, Is.EqualTo(1));
            Assert.That(undone.Value.Revision, Is.EqualTo(3), "undo is a new compensating command (revision 3), not a rollback to revision 1");
        }

        [Test]
        public void UndoMoveToken_ByNonController_IsRejected_PositionStaysAtMovedLocation()
        {
            // TC-BOARD-011: Undo delegates to the same MoveToken pipeline
            // (08_Scenes_And_Board section 21.5), so it re-validates control
            // ownership at undo time exactly as an ordinary move would -- it
            // is not a privileged operation exempt from the permission check
            // that produced the state it is trying to compensate. This task
            // has no control-transfer command (section 5), so "no longer
            // controls it" is exercised directly: an actor who was never the
            // controller cannot undo someone else's move.
            UserId controller = NewUserId();
            UserId otherActor = NewUserId();
            TokenRecord token = _sceneRepository.CreateToken(_campaign, _sceneId, new TokenPosition(1, 1), controller, NewCommandId(), TestCorrelationId).Value;

            var move = new MoveTokenRequest(_campaign, controller, actorIsMainGm: false, token.TokenId, new TokenPosition(8, 8), token.Revision, NewCommandId(), TestCorrelationId);
            Result<TokenRecord> moved = BoardMovementService.MoveToken(_sceneRepository, move);
            Assert.That(moved.IsSuccess, Is.True);

            var undoByOtherActor = new MoveTokenRequest(_campaign, otherActor, actorIsMainGm: false, token.TokenId, new TokenPosition(1, 1), moved.Value.Revision, NewCommandId(), TestCorrelationId);
            Result<TokenRecord> rejected = BoardMovementService.UndoMoveToken(_sceneRepository, undoByOtherActor);

            Assert.That(rejected.IsFailure, Is.True);
            Assert.That(rejected.Error.Code, Is.EqualTo(ErrorCodes.BoardTokenMoveDenied));

            TokenRecord unchanged = _sceneRepository.GetToken(_campaign, token.TokenId, TestCorrelationId).Value;
            Assert.That(unchanged.Position.X, Is.EqualTo(8), "the rejected undo must leave the token at its moved position");
        }

        [Test]
        public void UndoMoveToken_WithStaleRevision_IsRejected_NotBlindRollback()
        {
            // TC-BOARD-012: token moved again by someone else between the
            // original move and the undo attempt -- undo must see the current
            // revision, not silently overwrite the intervening move.
            UserId controller = NewUserId();
            TokenRecord token = _sceneRepository.CreateToken(_campaign, _sceneId, new TokenPosition(1, 1), controller, NewCommandId(), TestCorrelationId).Value;

            var move = new MoveTokenRequest(_campaign, controller, actorIsMainGm: false, token.TokenId, new TokenPosition(8, 8), token.Revision, NewCommandId(), TestCorrelationId);
            Result<TokenRecord> moved = BoardMovementService.MoveToken(_sceneRepository, move);
            Assert.That(moved.IsSuccess, Is.True);

            // An intervening move (e.g. MainGM administrative move) advances the revision again.
            var interveningMove = new MoveTokenRequest(_campaign, controller, actorIsMainGm: true, token.TokenId, new TokenPosition(9, 9), moved.Value.Revision, NewCommandId(), TestCorrelationId);
            Result<TokenRecord> intervened = BoardMovementService.MoveToken(_sceneRepository, interveningMove);
            Assert.That(intervened.IsSuccess, Is.True);

            // Undo submitted against the now-stale revision from before the intervening move.
            var staleUndo = new MoveTokenRequest(_campaign, controller, actorIsMainGm: false, token.TokenId, new TokenPosition(1, 1), moved.Value.Revision, NewCommandId(), TestCorrelationId);
            Result<TokenRecord> result = BoardMovementService.UndoMoveToken(_sceneRepository, staleUndo);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceTokenRevisionConflict));

            TokenRecord unchanged = _sceneRepository.GetToken(_campaign, token.TokenId, TestCorrelationId).Value;
            Assert.That(unchanged.Position.X, Is.EqualTo(9), "the stale undo must not overwrite the intervening move");
        }

        [Test]
        public void TokenState_SurvivesCloseAndReopen_IdenticalPositionControllerAndRevision()
        {
            // TC-BOARD-013 (08_Scenes_And_Board section 21.6/BT-079 -- restart
            // persistence groundwork for exit criterion 1; full reconnect
            // coverage is ODY-S03-007, not this task).
            UserId controller = NewUserId();
            TokenRecord token = _sceneRepository.CreateToken(_campaign, _sceneId, new TokenPosition(0, 0), controller, NewCommandId(), TestCorrelationId).Value;
            var move = new MoveTokenRequest(_campaign, controller, actorIsMainGm: false, token.TokenId, new TokenPosition(6, 6), token.Revision, NewCommandId(), TestCorrelationId);
            Result<TokenRecord> moved = BoardMovementService.MoveToken(_sceneRepository, move);
            Assert.That(moved.IsSuccess, Is.True);

            _campaignRepository.Close(_campaign, TestCorrelationId);

            Result<CampaignHandle> reopened = _campaignRepository.Open(_workDir, TestCorrelationId);
            Assert.That(reopened.IsSuccess, Is.True);
            _campaign = reopened.Value;

            var reopenedSceneRepository = new SqliteSceneRepository(Clock);
            TokenRecord restored = reopenedSceneRepository.GetToken(_campaign, token.TokenId, TestCorrelationId).Value;

            Assert.That(restored.Position.X, Is.EqualTo(6));
            Assert.That(restored.Position.Y, Is.EqualTo(6));
            Assert.That(restored.ControllerUserId, Is.EqualTo(controller));
            Assert.That(restored.Revision, Is.EqualTo(2));
        }
    }
}
