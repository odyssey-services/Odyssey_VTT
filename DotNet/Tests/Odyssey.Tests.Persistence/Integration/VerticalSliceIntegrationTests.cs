using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Audience;
using Odyssey.Application.Board;
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
using Odyssey.Rules.Versions;

namespace Odyssey.Tests.Persistence.Integration
{
    /// <summary>
    /// ODY-S03-008: roadmap section 12.6's ten-step "Бросок и журнал" scenario,
    /// run literally in order in one test method (not ten independent tests --
    /// the same "one sequence, not isolated steps" structural choice
    /// ODY-S01-013/ODY-S02-013 already established for their own slices), over
    /// already-merged ODY-S03-004..007 public APIs. No new production code
    /// exists to support this test.
    ///
    /// Real infrastructure throughout: <see cref="SqliteCampaignRepository"/>/
    /// <see cref="SqliteSceneRepository"/>/<see cref="SqliteGameLogRepository"/>
    /// against a real temp-directory SQLite <c>campaign.db</c> (mirroring
    /// <c>TC-PERSIST-*</c>'s own fixture pattern, not a mock repository), and
    /// the real <see cref="DeterministicRandomStreamFactory"/> (mirroring
    /// <c>TC-DICE-*</c>'s own convention, not a fake RNG).
    ///
    /// No real network exists in this revision (SLICE-03_IMPLEMENTATION_BACKLOG.md
    /// section 2.3) -- step 7 ("only permitted clients receive the result") is
    /// proven at the module boundary via <see cref="DiceRollVisibilityPolicy"/>
    /// directly (the same policy a future wire codec would consult, ODY-S03-006's
    /// own documented boundary), and step 9's "reconnect" is this revision's own
    /// campaign-persistence sense -- reopening <c>campaign.db</c> via a brand-new
    /// repository instance (a process restart / reopened campaign), never
    /// <c>ODY-S02-012</c>'s networked reconnect protocol (ODY-S03-007 task
    /// contract section 3, not reopened here).
    /// </summary>
    public sealed class VerticalSliceIntegrationTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly RulesetVersion TestRulesetVersion = RulesetVersion.Parse("1.0.0");
        private static readonly RngKeyEpochId TestEpoch = RngKeyEpochId.Parse("epoch-001");
        private static readonly IWallClock Clock = new SystemWallClock();
        private string _workDir = null!;

        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        private static IAuthoritativeRandomStreamFactory NewRngFactory()
        {
            byte[] key = new byte[32];
            for (int index = 0; index < key.Length; index++) key[index] = (byte)(index + 1);
            return new DeterministicRandomStreamFactory(CampaignRngKey.FromBytes(key));
        }

        private sealed class SystemWallClock : IWallClock
        {
            public UtcInstant GetUtcNow() => UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow);
        }

        [SetUp]
        public void SetUp()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "ody-s03-008-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true); } catch (IOException) { }
        }

        [Test]
        public void TenStepSlice_TokenSelectionThroughRollRerollWithJournalPersistence_AllStepsSucceed()
        {
            var campaignRepository = new SqliteCampaignRepository(Clock);
            var createRequest = new CreateCampaignRequest(_workDir, "SLICE-03 Vertical Slice Campaign", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = campaignRepository.Create(createRequest, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True, "campaign creation must succeed before the scenario begins");
            CampaignHandle campaign = created.Value;

            var sceneRepository = new SqliteSceneRepository(Clock);
            SceneId sceneId = sceneRepository.CreateScene(campaign, "Vertical Slice Scene", NewCommandId(), TestCorrelationId).Value.SceneId;

            UserId player = NewUserId();
            UserId mainGm = NewUserId();
            UserId excludedObserver = NewUserId();
            const string groupId = "group_selected_participants";

            // ---- Step 1: player selects own token (control ownership). ----
            // "Selection" has no dedicated command in this revision (a UI-layer
            // concern) -- the module-level proof of "the player's own token" is
            // that BoardMovementService authorizes a move BECAUSE the token's
            // ControllerUserId is the acting player (08_Scenes_And_Board
            // section 11.1), and rejects an actor who is neither the
            // controller nor MainGM.
            Result<TokenRecord> tokenCreated = sceneRepository.CreateToken(campaign, sceneId, new TokenPosition(0, 0), player, NewCommandId(), TestCorrelationId);
            Assert.That(tokenCreated.IsSuccess, Is.True, "step 1 (create the player's own token) must succeed");
            TokenRecord token = tokenCreated.Value;
            Assert.That(token.ControllerUserId, Is.EqualTo(player), "step 1: the token must be controlled by the acting player");

            var wrongActorMove = new MoveTokenRequest(campaign, excludedObserver, actorIsMainGm: false, token.TokenId, new TokenPosition(1, 1), token.Revision, NewCommandId(), TestCorrelationId);
            Result<TokenRecord> wrongActorResult = BoardMovementService.MoveToken(sceneRepository, wrongActorMove);
            Assert.That(wrongActorResult.IsFailure, Is.True, "step 1: a non-controller, non-MainGM actor must not be authorized to move the token");

            var ownMove = new MoveTokenRequest(campaign, player, actorIsMainGm: false, token.TokenId, new TokenPosition(5, 5), token.Revision, NewCommandId(), TestCorrelationId);
            Result<TokenRecord> ownMoveResult = BoardMovementService.MoveToken(sceneRepository, ownMove);
            Assert.That(ownMoveResult.IsSuccess, Is.True, "step 1: the controlling player must be authorized to move their own selected token");

            // ---- Steps 2-3: player sends a roll intent; host validates permission. ----
            var groupDirectory = new InMemoryCampaignUserGroupDirectory();
            groupDirectory.Upsert(new CampaignUserGroup(groupId, campaign.CampaignId, new List<UserId> { player }, CampaignUserGroupStatus.Active, revision: 1));
            DiceRollAudience audience = DiceRollAudience.SelectedParticipants(null, new List<string> { groupId });

            var deniedRequest = new SubmitRollRequest(player, actorCanCreateRoll: false, "AttributeCheck", "1d20+3", audience, campaign.CampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, TestCorrelationId);
            var store = new DiceRollStore();
            Result<DiceRoll> deniedResult = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, deniedRequest);
            Assert.That(deniedResult.IsFailure, Is.True, "step 3: host must reject a roll intent lacking the create-roll permission");
            Assert.That(deniedResult.Error.Code, Is.EqualTo(ErrorCodes.DiceRollDenied));

            var rollRequest = new SubmitRollRequest(player, actorCanCreateRoll: true, "AttributeCheck", "1d20+3", audience, campaign.CampaignId, NewCommandId(), TestRulesetVersion, TestEpoch, TestCorrelationId);
            Result<DiceRoll> rollResult = DiceRollService.SubmitRoll(store, NewRngFactory(), Clock, rollRequest);
            Assert.That(rollResult.IsSuccess, Is.True, "steps 2-3: an authorized roll intent must succeed");
            string rollId = rollResult.Value.RollId;

            // ---- Step 4: host generates the d100/formula result. ----
            DiceRoll generatedRoll = rollResult.Value;
            Assert.That(generatedRoll.NaturalResults.Count, Is.EqualTo(1), "step 4: the formula's one die must have produced exactly one natural result");
            Assert.That(generatedRoll.NaturalResults[0].Sides, Is.EqualTo(20));
            int originalBaseTotal = generatedRoll.BaseTotal;
            var originalNaturalResults = new List<NaturalResult>(generatedRoll.NaturalResults);
            string originalFormulaOriginal = generatedRoll.FormulaOriginal;

            // ---- Step 5: modifiers are applied. ----
            var proposed = DiceRollService.ProposeModifier(store, new ProposeModifierRequest(rollId, player, "attribute", "Strength", 2, TestCorrelationId));
            Assert.That(proposed.IsSuccess, Is.True, "step 5: proposing a modifier must succeed");
            string modifierEntryId = proposed.Value.ModifierEntries[0].ModifierEntryId;

            var decided = DiceRollService.DecideModifier(store, new DecideModifierRequest(rollId, modifierEntryId, mainGm, decidedByUserIsMainGm: true, ModifierDecision.Accepted, changedValue: null, reason: null, TestCorrelationId));
            Assert.That(decided.IsSuccess, Is.True, "step 5: MainGM accepting a proposed modifier must succeed");
            Assert.That(decided.Value.ModifierEntries[0].Decision, Is.EqualTo(ModifierDecision.Accepted));
            Assert.That(decided.Value.FinalTotal, Is.EqualTo(originalBaseTotal + 2), "step 5: the accepted modifier's value must count toward FinalTotal, visibly (no hidden GM adjustment)");

            // ---- Step 6: GM overrides with a mandatory reason. ----
            var overrideWithoutReason = new ApplyOverrideRequest(rollId, mainGm, actorIsMainGm: true, "natural 14", "natural 18", reason: null, TestCorrelationId);
            Result<RollOverride> overrideDeniedResult = DiceRollService.ApplyOverride(store, Clock, overrideWithoutReason);
            Assert.That(overrideDeniedResult.IsFailure, Is.True, "step 6: an override without a reason must be rejected");
            Assert.That(overrideDeniedResult.Error.Code, Is.EqualTo(ErrorCodes.DiceOverrideReasonRequired));

            var overrideWithReason = new ApplyOverrideRequest(rollId, mainGm, actorIsMainGm: true, "natural 14", "natural 18", "narratively more dramatic for the scene", TestCorrelationId);
            Result<RollOverride> overrideResult = DiceRollService.ApplyOverride(store, Clock, overrideWithReason);
            Assert.That(overrideResult.IsSuccess, Is.True, "step 6: an override with a reason must succeed");

            store.TryGet(rollId, out DiceRoll overriddenRoll);
            Assert.That(overriddenRoll.Status, Is.EqualTo(DiceRollStatus.Overridden), "step 6: the roll's status must flip to Overridden");
            Assert.That(overriddenRoll.NaturalResults[0].Value, Is.EqualTo(originalNaturalResults[0].Value), "step 6: the override must never rewrite the roll's own NaturalResults");
            Assert.That(overriddenRoll.BaseTotal, Is.EqualTo(originalBaseTotal), "step 6: the override must never rewrite the roll's own BaseTotal");

            // ---- Step 7: only permitted participants receive the result -- ----
            // a nontrivial SelectedParticipants case (not the trivial Public
            // case), reusing ODY-S03-006's DiceRollVisibilityPolicy directly --
            // the same policy a future wire codec would consult; there is no
            // real network in this revision to observe a packet over.
            Assert.That(DiceRollVisibilityPolicy.TryGetVisibleRoll(overriddenRoll, player, BaselineRole.Player, groupDirectory, out DiceRollView playerView), Is.True, "step 7: the selected player must see the result");
            Assert.That(playerView.Roll.RollId, Is.EqualTo(rollId));
            Assert.That(DiceRollVisibilityPolicy.TryGetVisibleRoll(overriddenRoll, mainGm, BaselineRole.MainGM, groupDirectory, out _), Is.True, "step 7: MainGM must always see the result");
            Assert.That(DiceRollVisibilityPolicy.TryGetVisibleRoll(overriddenRoll, excludedObserver, BaselineRole.Observer, groupDirectory, out DiceRollView excludedView), Is.False, "step 7: a participant not selected and not MainGM must not receive the result");
            Assert.That(excludedView, Is.Null, "step 7: safe denial -- no distinguishable view for the excluded participant");

            // ---- Step 8: the event is persisted (real SQLite, not a fake). ----
            var gameLogRepository = new SqliteGameLogRepository(Clock);
            Result<GameLogEntryRecord> saved = gameLogRepository.SaveDiceRollEntry(campaign, overriddenRoll, NewCommandId(), TestCorrelationId);
            Assert.That(saved.IsSuccess, Is.True, "step 8: persisting the resolved (and overridden) roll and its game-log entry must succeed");
            Assert.That(saved.Value.AuthoritativeSequence, Is.GreaterThanOrEqualTo(1));

            // ---- Step 9: reconnect restores the visible journal by CURRENT rights. ----
            // "Reconnect" here means reopening campaign.db via a brand-new
            // repository instance -- see class remarks for the explicit
            // distinction from ODY-S02-012's networked protocol.
            var reopenedGameLogRepository = new SqliteGameLogRepository(Clock);
            IReadOnlyList<GameLogEntryRecord> entriesAtReconnect = reopenedGameLogRepository.ListGameLog(campaign, TestCorrelationId).Value;
            Assert.That(entriesAtReconnect.Count, Is.EqualTo(1), "step 9: the persisted entry must survive reopening the campaign database");

            IReadOnlyList<GameLogEntryRecord> playerViewAtReconnect = GameLogReconnectService.GetVisibleEntries(entriesAtReconnect, player, BaselineRole.Player, groupDirectory);
            Assert.That(playerViewAtReconnect.Count, Is.EqualTo(1), "step 9: the still-selected player must see the restored entry");

            // Permission revoked while "disconnected": the group's current
            // membership no longer includes the player (ADR-017 section 1
            // point 8's principle, applied outside its original networking
            // context per ODY-S03-007 task contract section 3).
            var groupDirectoryAfterRevoke = new InMemoryCampaignUserGroupDirectory();
            groupDirectoryAfterRevoke.Upsert(new CampaignUserGroup(groupId, campaign.CampaignId, new List<UserId>(), CampaignUserGroupStatus.Active, revision: 2));
            IReadOnlyList<GameLogEntryRecord> playerViewAfterRevoke = GameLogReconnectService.GetVisibleEntries(entriesAtReconnect, player, BaselineRole.Player, groupDirectoryAfterRevoke);
            Assert.That(playerViewAfterRevoke.Count, Is.EqualTo(0), "step 9: a permission revoked before reconnect must hide the entry, recomputed by CURRENT membership, not the membership saved at record time");
            IReadOnlyList<GameLogEntryRecord> gmViewAfterRevoke = GameLogReconnectService.GetVisibleEntries(entriesAtReconnect, mainGm, BaselineRole.MainGM, groupDirectoryAfterRevoke);
            Assert.That(gmViewAfterRevoke.Count, Is.EqualTo(1), "step 9: MainGM's own visibility is unaffected by the player's revoked membership");

            // ---- Step 10: after a reroll, the original event remains in the journal, unchanged. ----
            var rerollRequest = new RequestFullRerollRequest(rollId, player, actorIsMainGm: false, NewCommandId(), TestRulesetVersion, TestEpoch, TestCorrelationId);
            Result<DiceRoll> rerollResult = DiceRollService.RequestFullReroll(store, NewRngFactory(), Clock, rerollRequest);
            Assert.That(rerollResult.IsSuccess, Is.True, "step 10: the original actor must be authorized to request a full reroll");
            DiceRoll reroll = rerollResult.Value;
            Assert.That(reroll.PreviousRollId, Is.EqualTo(rollId), "step 10: the reroll must be chained to the original via PreviousRollId");
            Assert.That(reroll.RollId, Is.Not.EqualTo(rollId), "step 10: a reroll is a new roll, never the same identity as the original");

            store.TryGet(rollId, out DiceRoll originalAfterReroll);
            Assert.That(originalAfterReroll.Status, Is.EqualTo(DiceRollStatus.SupersededByReroll), "step 10: the original's status marker flips to SupersededByReroll");
            Assert.That(originalAfterReroll.NaturalResults[0].Value, Is.EqualTo(originalNaturalResults[0].Value), "step 10: the original's own NaturalResults are never rewritten by a later reroll");
            Assert.That(originalAfterReroll.FormulaOriginal, Is.EqualTo(originalFormulaOriginal), "step 10: the original's own formula text is never rewritten by a later reroll");
            IReadOnlyList<RollOverride> overridesAfterReroll = store.GetOverrides(rollId);
            Assert.That(overridesAfterReroll.Count, Is.EqualTo(1), "step 10: the step-6 override audit record is not lost by the later reroll -- it is a separate, independent store entry");

            // The reroll is persisted as its OWN, additional row -- proving the
            // append-only guarantee holds at the persistence layer too: saving
            // a second, distinct roll for the same campaign must not disturb
            // the first roll's already-committed row.
            Result<GameLogEntryRecord> rerollSaved = gameLogRepository.SaveDiceRollEntry(campaign, reroll, NewCommandId(), TestCorrelationId);
            Assert.That(rerollSaved.IsSuccess, Is.True, "step 10: persisting the reroll as a new, additional entry must succeed");
            IReadOnlyList<GameLogEntryRecord> entriesAfterReroll = gameLogRepository.ListGameLog(campaign, TestCorrelationId).Value;
            Assert.That(entriesAfterReroll.Count, Is.EqualTo(2), "step 10: the journal must now hold both the original entry and the reroll's own entry");

            GameLogEntryRecord originalEntryAfterReroll = FindByDiceRollId(entriesAfterReroll, rollId);
            Assert.That(originalEntryAfterReroll.Roll.NaturalResults[0].Value, Is.EqualTo(originalNaturalResults[0].Value), "step 10: the original entry's persisted row is untouched by persisting the reroll's own separate row");
            Assert.That(originalEntryAfterReroll.Roll.Status, Is.EqualTo(DiceRollStatus.Overridden), "step 10: the original entry's persisted row still reflects the state it was saved under at step 8 -- persisting a later roll never rewrites an earlier row (ADR-012 section 4.2)");
        }

        private static GameLogEntryRecord FindByDiceRollId(IReadOnlyList<GameLogEntryRecord> entries, string diceRollId)
        {
            foreach (GameLogEntryRecord entry in entries)
            {
                if (string.Equals(entry.DiceRollId, diceRollId, StringComparison.Ordinal)) return entry;
            }

            throw new InvalidOperationException("GameLogEntry not found for DiceRollId: " + diceRollId);
        }
    }
}
