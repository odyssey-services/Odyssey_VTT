using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Audience;
using Odyssey.Application.Commands;
using Odyssey.Application.Dice;
using Odyssey.Application.GameLog;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Persistence;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S03-007: proves durable DiceRoll/GameLogEntry persistence
    /// (ADR-012 section 5's one-transaction commit, reused via the shared
    /// <c>SqliteSavingPipeline</c>) and audience-aware "reconnect" reading
    /// (a fresh repository instance against the same <c>campaign.db</c> --
    /// a process restart / reopened campaign, not a network reconnect --
    /// task contract section 3).
    /// </summary>
    public sealed class SqliteGameLogRepositoryTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private string _workDir = null!;
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;

        [SetUp]
        public void SetUp()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "ody-s03-007-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_workDir, "GameLog Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = _campaignRepository.Create(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                _campaignRepository.Close(_campaign, TestCorrelationId);
            }
            catch (IOException) { }

            try
            {
                if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup only.
            }
        }

        private static DiceRoll NewResolvedRoll(CampaignId campaignId, UserId actor, DiceRollAudience audience)
        {
            string rollId = "roll_" + Guid.NewGuid().ToString("N");
            var naturalResults = new List<NaturalResult> { new NaturalResult(dieIndex: 0, groupIndex: 0, sides: 20, value: 14) };
            var modifierEntries = new List<ModifierEntry>
            {
                new ModifierEntry("mod_" + Guid.NewGuid().ToString("N"), "attribute", "Strength", 3, proposedByUserId: null, ModifierDecision.Automatic, decidedByUserId: null, decisionReason: null, appliedValue: 3),
            };

            return new DiceRoll(
                rollId, actor, "AttributeCheck", campaignId,
                "1d20+3", "1d20+3", formulaParserVersion: 1,
                naturalResults, modifierEntries, baseTotal: 14, rngAlgorithmVersion: 1,
                Array.Empty<RngProofData>(), DiceRollStatus.Resolved, previousRollId: null,
                UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow), audience);
        }

        [Test]
        public void SaveDiceRollEntry_ThenNewRepositoryInstance_RestoresIdenticalRollAndLogEntry()
        {
            UserId actor = NewUserId();
            DiceRoll roll = NewResolvedRoll(_campaign.CampaignId, actor, DiceRollAudience.Public());
            CommandId commandId = NewCommandId();

            var repository = new SqliteGameLogRepository(Clock);
            Result<GameLogEntryRecord> saved = repository.SaveDiceRollEntry(_campaign, roll, commandId, TestCorrelationId);
            Assert.That(saved.IsSuccess, Is.True);
            Assert.That(saved.Value.AuthoritativeSequence, Is.GreaterThanOrEqualTo(1));

            // A brand-new repository instance against the same campaign.db --
            // no shared in-memory state, the same "restart" scenario a real
            // process restart or reopened campaign would produce.
            var reopened = new SqliteGameLogRepository(Clock);
            Result<IReadOnlyList<GameLogEntryRecord>> listed = reopened.ListGameLog(_campaign, TestCorrelationId);
            Assert.That(listed.IsSuccess, Is.True);
            Assert.That(listed.Value.Count, Is.EqualTo(1));

            GameLogEntryRecord restored = listed.Value[0];
            Assert.That(restored.EntryType, Is.EqualTo("DiceRollResolved"));
            Assert.That(restored.ActorUserId, Is.EqualTo(actor));
            Assert.That(restored.DiceRollId, Is.EqualTo(roll.RollId));

            DiceRoll restoredRoll = restored.Roll;
            Assert.That(restoredRoll.RollId, Is.EqualTo(roll.RollId));
            Assert.That(restoredRoll.ActorUserId, Is.EqualTo(roll.ActorUserId));
            Assert.That(restoredRoll.FormulaOriginal, Is.EqualTo(roll.FormulaOriginal));
            Assert.That(restoredRoll.BaseTotal, Is.EqualTo(roll.BaseTotal));
            Assert.That(restoredRoll.FinalTotal, Is.EqualTo(roll.FinalTotal));
            Assert.That(restoredRoll.Status, Is.EqualTo(roll.Status));
            Assert.That(restoredRoll.Audience.Kind, Is.EqualTo(DiceRollAudienceKind.Public));
            Assert.That(restoredRoll.NaturalResults.Count, Is.EqualTo(1));
            Assert.That(restoredRoll.NaturalResults[0].Value, Is.EqualTo(14));
            Assert.That(restoredRoll.ModifierEntries.Count, Is.EqualTo(1));
            Assert.That(restoredRoll.ModifierEntries[0].AppliedValue, Is.EqualTo(3));
        }

        [Test]
        public void SaveDiceRollEntry_RedeliveredWithSameCommandId_DoesNotDuplicate()
        {
            DiceRoll roll = NewResolvedRoll(_campaign.CampaignId, NewUserId(), DiceRollAudience.Public());
            CommandId commandId = NewCommandId();
            var repository = new SqliteGameLogRepository(Clock);

            Result<GameLogEntryRecord> first = repository.SaveDiceRollEntry(_campaign, roll, commandId, TestCorrelationId);
            Result<GameLogEntryRecord> second = repository.SaveDiceRollEntry(_campaign, roll, commandId, TestCorrelationId);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(second.IsSuccess, Is.True);
            Assert.That(second.Value.LogEntryId, Is.EqualTo(first.Value.LogEntryId));

            Result<IReadOnlyList<GameLogEntryRecord>> listed = repository.ListGameLog(_campaign, TestCorrelationId);
            Assert.That(listed.Value.Count, Is.EqualTo(1));
        }

        [Test]
        public void SelectedParticipantsRoll_GroupMembershipRevokedBeforeReconnect_HidesEntryAfterReopen()
        {
            UserId player = NewUserId();
            UserId mainGm = NewUserId();
            const string groupId = "group_reconnect_test";

            var directoryAtRecordTime = new InMemoryCampaignUserGroupDirectory();
            directoryAtRecordTime.Upsert(new CampaignUserGroup(groupId, _campaign.CampaignId, new List<UserId> { player }, CampaignUserGroupStatus.Active, revision: 1));

            DiceRoll roll = NewResolvedRoll(_campaign.CampaignId, NewUserId(), DiceRollAudience.SelectedParticipants(null, new List<string> { groupId }));
            var repository = new SqliteGameLogRepository(Clock);
            Result<GameLogEntryRecord> saved = repository.SaveDiceRollEntry(_campaign, roll, NewCommandId(), TestCorrelationId);
            Assert.That(saved.IsSuccess, Is.True);

            IReadOnlyList<GameLogEntryRecord> entriesBeforeDisconnect = repository.ListGameLog(_campaign, TestCorrelationId).Value;
            IReadOnlyList<GameLogEntryRecord> visibleBeforeDisconnect = GameLogReconnectService.GetVisibleEntries(entriesBeforeDisconnect, player, BaselineRole.Player, directoryAtRecordTime);
            Assert.That(visibleBeforeDisconnect.Count, Is.EqualTo(1));

            // ADR-017 section 1 point 8's principle, applied outside its
            // original networking context (task contract section 3):
            // membership is re-evaluated against CURRENT state at "reconnect"
            // (reopen) time -- here, the player has since been removed from
            // the group, simulating a permission revoked while disconnected.
            var directoryAtReconnectTime = new InMemoryCampaignUserGroupDirectory();
            directoryAtReconnectTime.Upsert(new CampaignUserGroup(groupId, _campaign.CampaignId, new List<UserId>(), CampaignUserGroupStatus.Active, revision: 2));

            var reopened = new SqliteGameLogRepository(Clock);
            IReadOnlyList<GameLogEntryRecord> entriesAfterReconnect = reopened.ListGameLog(_campaign, TestCorrelationId).Value;
            IReadOnlyList<GameLogEntryRecord> visibleAfterReconnect = GameLogReconnectService.GetVisibleEntries(entriesAfterReconnect, player, BaselineRole.Player, directoryAtReconnectTime);
            Assert.That(visibleAfterReconnect.Count, Is.EqualTo(0));

            // Safe denial: MainGM still sees it unconditionally (section
            // 16.2), the revoked player's own reconnect view has no
            // distinguishable trace of a hidden entry.
            IReadOnlyList<GameLogEntryRecord> gmView = GameLogReconnectService.GetVisibleEntries(entriesAfterReconnect, mainGm, BaselineRole.MainGM, directoryAtReconnectTime);
            Assert.That(gmView.Count, Is.EqualTo(1));
        }

        [Test]
        public void CreateToken_ThenNewSceneRepositoryInstance_ListsIdenticalTokenState()
        {
            var repositoryBeforeRestart = new SqliteSceneRepository(Clock);
            SceneId sceneId = repositoryBeforeRestart.CreateScene(_campaign, "Board Restart Test", NewCommandId(), TestCorrelationId).Value.SceneId;
            UserId controller = NewUserId();
            Result<TokenRecord> token = repositoryBeforeRestart.CreateToken(_campaign, sceneId, new TokenPosition(3, 4), controller, NewCommandId(), TestCorrelationId);
            Assert.That(token.IsSuccess, Is.True);
            Result<TokenRecord> moved = repositoryBeforeRestart.MoveToken(_campaign, token.Value.TokenId, new TokenPosition(7, 8), token.Value.Revision, NewCommandId(), TestCorrelationId);
            Assert.That(moved.IsSuccess, Is.True);

            // A brand-new SqliteSceneRepository instance against the same
            // campaign.db -- exit criterion 1's "board state одинаков после
            // restart и reconnect," proven directly rather than assumed from
            // the repository's already-stateless-per-call design.
            var repositoryAfterRestart = new SqliteSceneRepository(Clock);
            Result<IReadOnlyList<TokenRecord>> tokensAfterRestart = repositoryAfterRestart.ListTokens(_campaign, sceneId, TestCorrelationId);
            Assert.That(tokensAfterRestart.IsSuccess, Is.True);
            Assert.That(tokensAfterRestart.Value.Count, Is.EqualTo(1));
            Assert.That(tokensAfterRestart.Value[0].Position.X, Is.EqualTo(7));
            Assert.That(tokensAfterRestart.Value[0].Position.Y, Is.EqualTo(8));
            Assert.That(tokensAfterRestart.Value[0].Revision, Is.EqualTo(2));
            Assert.That(tokensAfterRestart.Value[0].ControllerUserId, Is.EqualTo(controller));
        }
    }
}
