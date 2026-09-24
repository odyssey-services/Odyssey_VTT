using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Odyssey.Application.CharacterAdvancement;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Identity;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S09-102: the four "cost" commands (attribute purchase, skill purchase, skill advancement recommendation,
    /// ability acquisition) now decide legality and cost in <see cref="CharacterAdvancementService"/> (Rules) and hand
    /// the decision to <see cref="SqliteCharacterRepository"/>, which holds no Rules reference. Numbers below are the
    /// literal fixture values (attribute 2/point, cap 15; skill 3/point, ordinary purchases up to level 4; ability 5),
    /// written out on purpose so a change to a Rules constant cannot silently change what these tests assert.
    /// Against a real temp-directory SQLite campaign; the interception doubles use <see cref="DispatchProxy"/> over the
    /// real repository rather than a 50-member hand-written decorator.
    /// </summary>
    public sealed class CharacterAdvancementCostServiceTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
        private static readonly AttributeDefinitionId Strength = AttributeDefinitionId.Parse("Strength");
        private static readonly SkillDefinitionId Tactics = SkillDefinitionId.Parse("Tactics");
        private static readonly AbilityDefinitionId Fireball = AbilityDefinitionId.Parse("Fireball");

        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;
        private SqliteCharacterRepository _repo = null!;
        private UserId _gm;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s09-102-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_campaignDir, "Cost Service Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = _campaignRepository.Create(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _repo = new SqliteCharacterRepository(Clock);
            _gm = NewUserId();
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaignRepository.Close(_campaign, TestCorrelationId); }
            catch (IOException) { }

            try { if (Directory.Exists(_campaignDir)) Directory.Delete(_campaignDir, recursive: true); }
            catch (IOException) { }
        }

        private CharacterRecord CreateCharacter(long points)
        {
            Result<CharacterRecord> created = _repo.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Cost Character"), NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            return Grant(created.Value, points);
        }

        private CharacterRecord Grant(CharacterRecord character, long amount)
        {
            Result<CharacterRecord> granted = _repo.GrantDevelopmentPoints(_campaign, character.CharacterId, amount, "Grant", _gm, actorIsMainGm: true, character.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(granted.IsSuccess, Is.True);
            return granted.Value;
        }

        private CharacterRecord Reload(CharacterRecord character)
        {
            Result<CharacterRecord> read = _repo.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(read.IsSuccess, Is.True);
            return read.Value;
        }

        // Everything a rejected command must leave untouched.
        private string Snapshot(CharacterRecord character)
        {
            CharacterRecord c = Reload(character);
            string attrs = string.Join(",", c.Attributes.Select(a => $"{a.AttributeDefinitionId}:{a.BaseValue}:{a.SpentDevelopmentPoints}:{a.Revision}"));
            string skills = string.Join(",", c.Skills.Select(s => $"{s.SkillDefinitionId}:{s.Level}:{s.SpentDevelopmentPoints}:{s.Revision}"));
            int ledger = _repo.GetDevelopmentLedger(_campaign, c.CharacterId, TestCorrelationId).Value.Count;
            int purchases = _repo.GetAdvancementPurchases(_campaign, c.CharacterId, TestCorrelationId).Value.Count;
            return $"pool={c.DevelopmentPool.Earned}/{c.DevelopmentPool.Spent}/{c.DevelopmentPool.Reserved}|attrs={attrs}|skills={skills}|abilities={c.Abilities.Count}|rev={c.Revisions.CharacterRevision}/{c.Revisions.MechanicsRevision}/{c.Revisions.CharacterAbilitiesRevision}|ledger={ledger}|purchases={purchases}";
        }

        private Result<CharacterRecord> BuyAttribute(ICharacterRepository repo, CharacterRecord character, long toValue, bool asMainGm = true)
        {
            CharacterRecord c = Reload(character);
            long attrRevision = c.Attributes.FirstOrDefault(a => a.AttributeDefinitionId.Equals(Strength))?.Revision ?? 0;
            return CharacterAdvancementService.PurchaseAttributeIncrease(repo, _campaign, c.CharacterId, Strength, toValue, asMainGm ? _gm : NewUserId(), asMainGm, c.Revisions.MechanicsRevision, attrRevision, NewCommandId(), TestCorrelationId);
        }

        private Result<CharacterRecord> BuySkill(ICharacterRepository repo, CharacterRecord character, long toLevel)
        {
            CharacterRecord c = Reload(character);
            long skillRevision = c.Skills.FirstOrDefault(s => s.SkillDefinitionId.Equals(Tactics))?.Revision ?? 0;
            return CharacterAdvancementService.PurchaseSkillLevel(repo, _campaign, c.CharacterId, Tactics, toLevel, _gm, actorIsMainGm: true, c.Revisions.MechanicsRevision, skillRevision, NewCommandId(), TestCorrelationId);
        }

        // ------------------------------------------------------------------ attribute purchase

        [Test] // TC-CHAR-189
        public void PurchaseAttributeIncrease_Legal_ChargesTwoPerPoint_AndUpdatesState()
        {
            CharacterRecord character = CreateCharacter(20);

            Result<CharacterRecord> first = BuyAttribute(_repo, character, 3);
            Assert.That(first.IsSuccess, Is.True);
            Assert.That(first.Value.DevelopmentPool.Earned, Is.EqualTo(20));
            Assert.That(first.Value.DevelopmentPool.Spent, Is.EqualTo(6), "0 -> 3 costs 3 * 2");
            Assert.That(first.Value.DevelopmentPool.Available, Is.EqualTo(14));
            AttributeValue attr = first.Value.Attributes.Single();
            Assert.That(attr.BaseValue, Is.EqualTo(3));
            Assert.That(attr.SpentDevelopmentPoints, Is.EqualTo(6));
            Assert.That(attr.Revision, Is.EqualTo(1));

            Result<CharacterRecord> second = BuyAttribute(_repo, character, 5);
            Assert.That(second.IsSuccess, Is.True);
            Assert.That(second.Value.DevelopmentPool.Spent, Is.EqualTo(10), "3 -> 5 costs 2 * 2 on top of 6");
            Assert.That(second.Value.Attributes.Single().BaseValue, Is.EqualTo(5));
            Assert.That(second.Value.Attributes.Single().Revision, Is.EqualTo(2));

            Assert.That(_repo.GetAdvancementPurchases(_campaign, character.CharacterId, TestCorrelationId).Value.Select(p => p.Cost), Is.EqualTo(new long[] { 6, 4 }));
        }

        [Test] // TC-CHAR-190
        public void PurchaseAttributeIncrease_Illegal_CapOrBalanceOrPermission_IsRejected_WithNoWrites()
        {
            CharacterRecord rich = CreateCharacter(100);
            string before = Snapshot(rich);

            Result<CharacterRecord> overCap = BuyAttribute(_repo, rich, 16);
            Assert.That(overCap.IsFailure, Is.True);
            Assert.That(overCap.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterAttributeCapExceeded));
            Assert.That(Snapshot(rich), Is.EqualTo(before), "cap 15: nothing may be written");

            CharacterRecord poor = CreateCharacter(5);
            string poorBefore = Snapshot(poor);
            Result<CharacterRecord> tooExpensive = BuyAttribute(_repo, poor, 3);
            Assert.That(tooExpensive.IsFailure, Is.True);
            Assert.That(tooExpensive.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterDevelopmentInsufficientBalance), "cost 6 > available 5");
            Assert.That(Snapshot(poor), Is.EqualTo(poorBefore));

            Result<CharacterRecord> denied = BuyAttribute(_repo, rich, 16, asMainGm: false);
            Assert.That(denied.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterDevelopmentPurchaseDenied), "an unauthorized actor is refused before the cap is even reported");
            Assert.That(Snapshot(rich), Is.EqualTo(before));
        }

        [Test] // TC-CHAR-191
        public void PurchaseAttributeIncrease_DecisionMadeOnStaleState_IsRejectedByTheRepository_NotSilentlyApplied()
        {
            CharacterRecord character = CreateCharacter(50);

            // The Application layer reads the character while Strength is unpurchased (0)...
            CharacterRecord staleRead = Reload(character);
            long staleFrom = staleRead.Attributes.Count == 0 ? 0 : staleRead.Attributes.Single().BaseValue;
            Assert.That(staleFrom, Is.EqualTo(0));

            // ...then a competing, legitimate purchase lands (Strength -> 2).
            Assert.That(BuyAttribute(_repo, character, 2).IsSuccess, Is.True);
            string afterCompetitor = Snapshot(character);

            // The stale decision (from 0, cost 3*2) is committed with the CURRENT revisions, so both revision gates pass:
            // only the decision-basis re-check under the lock can catch it.
            CharacterRecord now = Reload(character);
            Result<CharacterRecord> stale = _repo.PurchaseAttributeIncrease(
                _campaign, character.CharacterId, Strength, toValue: 3, decidedFromValue: 0, exceedsNormalCap: false, decidedCost: 6,
                _gm, actorIsMainGm: true, now.Revisions.MechanicsRevision, now.Attributes.Single().Revision, NewCommandId(), TestCorrelationId);

            Assert.That(stale.IsFailure, Is.True);
            Assert.That(stale.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRevisionConflict));
            Assert.That(Snapshot(character), Is.EqualTo(afterCompetitor), "the competitor's state must survive untouched");
        }

        [Test] // TC-CHAR-192
        public void PurchaseAttributeIncrease_CompetingWriteBetweenServiceReadAndCommit_IsRejected()
        {
            CharacterRecord character = CreateCharacter(50);
            CharacterRecord expectedAtStart = Reload(character);
            long mechanicsAfterCompetitor = 0;
            string afterCompetitor = string.Empty;

            // Race injected exactly between the service's read and its commit: the read returns the pre-competitor
            // snapshot, then a competing purchase commits.
            ICharacterRepository racing = InterceptingProxy.Create(_repo, (method, args) =>
            {
                if (method.Name != nameof(ICharacterRepository.GetCharacter)) return (false, null);
                object? snapshot = method.Invoke(_repo, args);
                Assert.That(BuyAttribute(_repo, character, 2).IsSuccess, Is.True);
                afterCompetitor = Snapshot(character);
                mechanicsAfterCompetitor = Reload(character).Revisions.MechanicsRevision;
                return (true, snapshot);
            });

            // First race: the caller only knows the revisions it started with, so the revision gate itself rejects.
            Result<CharacterRecord> withStartRevisions = CharacterAdvancementService.PurchaseAttributeIncrease(racing, _campaign, character.CharacterId, Strength, 3, _gm, true, expectedAtStart.Revisions.MechanicsRevision, 0, NewCommandId(), TestCorrelationId);
            Assert.That(withStartRevisions.IsFailure, Is.True);
            Assert.That(withStartRevisions.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRevisionConflict));
            Assert.That(Snapshot(character), Is.EqualTo(afterCompetitor));

            // Second race, this time with the caller already holding the fresh revisions: the read is stale, the gates
            // pass, and only the decision-basis re-check stands between the stale decision and the write.
            CharacterRecord fresh = Reload(character);
            long freshMechanics = fresh.Revisions.MechanicsRevision;
            long freshAttr = fresh.Attributes.Single().Revision;
            Assert.That(freshMechanics, Is.EqualTo(mechanicsAfterCompetitor));
            ICharacterRepository staleReader = InterceptingProxy.Create(_repo, (method, args) =>
            {
                if (method.Name != nameof(ICharacterRepository.GetCharacter)) return (false, null);
                return (true, Result<CharacterRecord>.Success(expectedAtStart));
            });
            Result<CharacterRecord> basisOnly = CharacterAdvancementService.PurchaseAttributeIncrease(staleReader, _campaign, character.CharacterId, Strength, 3, _gm, true, freshMechanics, freshAttr, NewCommandId(), TestCorrelationId);
            Assert.That(basisOnly.IsFailure, Is.True);
            Assert.That(basisOnly.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRevisionConflict));
            Assert.That(Snapshot(character), Is.EqualTo(afterCompetitor));
        }

        // ------------------------------------------------------------------ skill purchase

        [Test] // TC-CHAR-193
        public void PurchaseSkillLevel_Legal_ChargesThreePerLevel_AndUpdatesState()
        {
            CharacterRecord character = CreateCharacter(30);

            Result<CharacterRecord> first = BuySkill(_repo, character, 2);
            Assert.That(first.IsSuccess, Is.True);
            Assert.That(first.Value.DevelopmentPool.Spent, Is.EqualTo(6), "0 -> 2 costs 2 * 3");
            Assert.That(first.Value.Skills.Single().Level, Is.EqualTo(2));
            Assert.That(first.Value.Skills.Single().Revision, Is.EqualTo(1));

            Result<CharacterRecord> second = BuySkill(_repo, character, 4);
            Assert.That(second.IsSuccess, Is.True);
            Assert.That(second.Value.DevelopmentPool.Spent, Is.EqualTo(12), "2 -> 4 costs 2 * 3 on top of 6");
            Assert.That(second.Value.DevelopmentPool.Available, Is.EqualTo(18));
            Assert.That(second.Value.Skills.Single().Level, Is.EqualTo(4));
            Assert.That(second.Value.Skills.Single().SpentDevelopmentPoints, Is.EqualTo(12));
        }

        [Test] // TC-CHAR-194
        public void PurchaseSkillLevel_Illegal_RecommendationLevelOrBalance_IsRejected_WithNoWrites()
        {
            CharacterRecord rich = CreateCharacter(100);
            string before = Snapshot(rich);

            Result<CharacterRecord> level5 = BuySkill(_repo, rich, 5);
            Assert.That(level5.IsFailure, Is.True);
            Assert.That(level5.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterSkillLevelRequiresRecommendation), "level 5+ is the recommendation pipeline's");
            Assert.That(Snapshot(rich), Is.EqualTo(before));

            CharacterRecord poor = CreateCharacter(5);
            string poorBefore = Snapshot(poor);
            Result<CharacterRecord> tooExpensive = BuySkill(_repo, poor, 2);
            Assert.That(tooExpensive.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterDevelopmentInsufficientBalance), "cost 6 > available 5");
            Assert.That(Snapshot(poor), Is.EqualTo(poorBefore));
        }

        [Test] // TC-CHAR-195
        public void PurchaseSkillLevel_DecisionMadeOnStaleState_IsRejected_AtRepositoryAndThroughTheService()
        {
            CharacterRecord character = CreateCharacter(50);
            CharacterRecord staleRead = Reload(character);
            Assert.That(BuySkill(_repo, character, 2).IsSuccess, Is.True);
            string afterCompetitor = Snapshot(character);
            CharacterRecord now = Reload(character);

            Result<CharacterRecord> direct = _repo.PurchaseSkillLevel(
                _campaign, character.CharacterId, Tactics, toLevel: 3, decidedFromLevel: 0, requiresRecommendation: false, decidedCost: 9,
                _gm, actorIsMainGm: true, now.Revisions.MechanicsRevision, now.Skills.Single().Revision, NewCommandId(), TestCorrelationId);
            Assert.That(direct.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRevisionConflict));
            Assert.That(Snapshot(character), Is.EqualTo(afterCompetitor));

            ICharacterRepository staleReader = InterceptingProxy.Create(_repo, (method, args) =>
                method.Name == nameof(ICharacterRepository.GetCharacter) ? (true, Result<CharacterRecord>.Success(staleRead)) : (false, null));
            Result<CharacterRecord> viaService = CharacterAdvancementService.PurchaseSkillLevel(staleReader, _campaign, character.CharacterId, Tactics, 3, _gm, true, now.Revisions.MechanicsRevision, now.Skills.Single().Revision, NewCommandId(), TestCorrelationId);
            Assert.That(viaService.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRevisionConflict));
            Assert.That(Snapshot(character), Is.EqualTo(afterCompetitor));
        }

        // ------------------------------------------------------------------ skill advancement recommendation

        private Result<AdvancementRecommendationRecord> Recommend(ICharacterRepository repo, CharacterRecord character, long targetLevel)
        {
            Result<CriticalSuccessEvidenceRecord> evidence = _repo.RecordCriticalSuccessEvidence(_campaign, character.CharacterId, Tactics, "roll_" + Guid.NewGuid().ToString("N"), null, NewCommandId(), TestCorrelationId);
            Assert.That(evidence.IsSuccess, Is.True);
            CharacterRecord c = Reload(character);
            return CharacterAdvancementService.RequestSkillAdvancedRecommendation(repo, _campaign, c.CharacterId, Tactics, targetLevel, new[] { evidence.Value.EvidenceId }, _gm, actorIsMainGm: true, c.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
        }

        [Test] // TC-CHAR-196
        public void RequestSkillAdvancedRecommendation_Legal_ReservesThreePerLevelFromCurrentLevel()
        {
            CharacterRecord character = CreateCharacter(30);
            Assert.That(BuySkill(_repo, character, 2).IsSuccess, Is.True);

            Result<AdvancementRecommendationRecord> requested = Recommend(_repo, character, 5);

            Assert.That(requested.IsSuccess, Is.True);
            Assert.That(requested.Value.ReservedAmount, Is.EqualTo(9), "2 -> 5 reserves 3 * 3");
            Assert.That(requested.Value.Status, Is.EqualTo(AdvancementRecommendationStatus.Pending));
            CharacterRecord after = Reload(character);
            Assert.That(after.DevelopmentPool.Reserved, Is.EqualTo(9));
            Assert.That(after.DevelopmentPool.Spent, Is.EqualTo(6));
            Assert.That(after.DevelopmentPool.Available, Is.EqualTo(15));
        }

        [Test] // TC-CHAR-197
        public void RequestSkillAdvancedRecommendation_Illegal_InsufficientBalanceOrNotAnIncrease_IsRejected_WithNoWrites()
        {
            CharacterRecord poor = CreateCharacter(8);
            string before = Snapshot(poor);

            Result<AdvancementRecommendationRecord> tooExpensive = Recommend(_repo, poor, 3);
            Assert.That(tooExpensive.IsFailure, Is.True);
            Assert.That(tooExpensive.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterDevelopmentInsufficientBalance), "reserve 9 > available 8");
            Assert.That(Snapshot(poor), Is.EqualTo(before));

            CharacterRecord leveled = CreateCharacter(30);
            Assert.That(BuySkill(_repo, leveled, 3).IsSuccess, Is.True);
            string leveledBefore = Snapshot(leveled);
            Assert.Throws<ArgumentOutOfRangeException>(new Action(() => Recommend(_repo, leveled, 3)), "target must exceed the current level");
            Assert.That(Snapshot(leveled), Is.EqualTo(leveledBefore));
        }

        [Test] // TC-CHAR-198
        public void RequestSkillAdvancedRecommendation_DecisionMadeOnStaleState_IsRejected_AtRepositoryAndThroughTheService()
        {
            CharacterRecord character = CreateCharacter(50);
            CharacterRecord staleRead = Reload(character);
            Assert.That(BuySkill(_repo, character, 2).IsSuccess, Is.True);
            string afterCompetitor = Snapshot(character);
            CharacterRecord now = Reload(character);
            Result<CriticalSuccessEvidenceRecord> evidence = _repo.RecordCriticalSuccessEvidence(_campaign, character.CharacterId, Tactics, "roll_x", null, NewCommandId(), TestCorrelationId);
            now = Reload(character);
            afterCompetitor = Snapshot(character);

            Result<AdvancementRecommendationRecord> direct = _repo.RequestSkillAdvancedRecommendation(
                _campaign, character.CharacterId, Tactics, targetLevel: 5, decidedFromLevel: 0, decidedReservedAmount: 15, new[] { evidence.Value.EvidenceId },
                _gm, actorIsMainGm: true, now.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(direct.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRevisionConflict));
            Assert.That(Snapshot(character), Is.EqualTo(afterCompetitor));

            ICharacterRepository staleReader = InterceptingProxy.Create(_repo, (method, args) =>
                method.Name == nameof(ICharacterRepository.GetCharacter) ? (true, Result<CharacterRecord>.Success(staleRead)) : (false, null));
            Result<AdvancementRecommendationRecord> viaService = CharacterAdvancementService.RequestSkillAdvancedRecommendation(staleReader, _campaign, character.CharacterId, Tactics, 5, new[] { evidence.Value.EvidenceId }, _gm, true, now.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(viaService.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRevisionConflict));
            Assert.That(Snapshot(character), Is.EqualTo(afterCompetitor));
        }

        // ------------------------------------------------------------------ ability acquisition

        private Result<CharacterRecord> Acquire(ICharacterRepository repo, CharacterRecord character, SourceKind kind, CharacterRecord? basis = null)
        {
            CharacterRecord c = basis ?? Reload(character);
            return CharacterAdvancementService.AcquireAbility(repo, _campaign, character.CharacterId, Fireball, kind, null, RankMode.None, null, null, "{}", _gm, actorIsMainGm: true, c.Revisions.MechanicsRevision, c.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);
        }

        [Test] // TC-CHAR-199
        public void AcquireAbility_ProgressionPurchase_Legal_ChargesFive()
        {
            CharacterRecord character = CreateCharacter(12);

            Result<CharacterRecord> acquired = Acquire(_repo, character, SourceKind.ProgressionPurchase);

            Assert.That(acquired.IsSuccess, Is.True);
            Assert.That(acquired.Value.DevelopmentPool.Spent, Is.EqualTo(5));
            Assert.That(acquired.Value.DevelopmentPool.Available, Is.EqualTo(7));
            Assert.That(acquired.Value.Abilities.Single().AbilityDefinitionId, Is.EqualTo(Fireball));
            Assert.That(_repo.GetAdvancementPurchases(_campaign, character.CharacterId, TestCorrelationId).Value.Single().Cost, Is.EqualTo(5));
        }

        [Test] // TC-CHAR-200
        public void AcquireAbility_ProgressionPurchase_Illegal_InsufficientBalance_IsRejected_WithNoWrites()
        {
            CharacterRecord character = CreateCharacter(4);
            string before = Snapshot(character);

            Result<CharacterRecord> acquired = Acquire(_repo, character, SourceKind.ProgressionPurchase);

            Assert.That(acquired.IsFailure, Is.True);
            Assert.That(acquired.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterDevelopmentInsufficientBalance), "cost 5 > available 4");
            Assert.That(Snapshot(character), Is.EqualTo(before));
        }

        [Test] // TC-CHAR-201
        public void AcquireAbility_StaleRevisions_AreRejected_AndCostIsIgnoredOutsideProgressionPurchase()
        {
            CharacterRecord character = CreateCharacter(20);
            CharacterRecord stale = Reload(character);
            Assert.That(Acquire(_repo, character, SourceKind.ProgressionPurchase).IsSuccess, Is.True);
            string afterCompetitor = Snapshot(character);

            // Ability cost is a constant, so there is no state-derived decision to go stale; the gate is the revisions.
            Result<CharacterRecord> lost = Acquire(_repo, character, SourceKind.ProgressionPurchase, stale);
            Assert.That(lost.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRevisionConflict));
            Assert.That(Snapshot(character), Is.EqualTo(afterCompetitor));

            // A GM grant charges nothing, whatever cost argument reaches the repository -- it is never read on that path.
            CharacterRecord now = Reload(character);
            Result<CharacterRecord> grant = _repo.AcquireAbility(_campaign, character.CharacterId, AbilityDefinitionId.Parse("Grantable"), SourceKind.GMGrant, null, RankMode.None, null, null, "{}", progressionPurchaseCost: 999, _gm, actorIsMainGm: true, null, now.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);
            Assert.That(grant.IsSuccess, Is.True);
            Assert.That(grant.Value.DevelopmentPool.Spent, Is.EqualTo(now.DevelopmentPool.Spent));

            Assert.Throws<ArgumentOutOfRangeException>(new Action(() => _repo.AcquireAbility(_campaign, character.CharacterId, Fireball, SourceKind.ProgressionPurchase, null, RankMode.None, null, null, "{}", progressionPurchaseCost: -1, _gm, true, now.Revisions.MechanicsRevision, now.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId)));
        }

        // ------------------------------------------------------------------ the service depends only on the port

        [Test] // TC-CHAR-202
        public void CostService_UsesOnlyTheICharacterRepositoryPort_PassesRulesDecisionsThrough_AndTouchesNothingOnInvalidInput()
        {
            CharacterRecord character = CreateCharacter(50);
            CharacterRecord c = Reload(character);
            var calls = new List<(string Name, object?[] Args)>();
            ICharacterRepository recording = InterceptingProxy.Create(_repo, (method, args) =>
            {
                calls.Add((method.Name, args ?? Array.Empty<object?>()));
                if (method.Name == nameof(ICharacterRepository.GetCharacter)) return (false, null);
                // Commit methods are answered by the double: nothing is written, the arguments are just recorded.
                if (method.ReturnType == typeof(Result<CharacterRecord>)) return (true, Result<CharacterRecord>.Success(c));
                return (true, method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null);
            });

            CharacterAdvancementService.PurchaseAttributeIncrease(recording, _campaign, character.CharacterId, Strength, 20, _gm, true, c.Revisions.MechanicsRevision, 0, NewCommandId(), TestCorrelationId);
            object?[] attr = calls.Single(x => x.Name == nameof(ICharacterRepository.PurchaseAttributeIncrease)).Args;
            Assert.That(attr[3], Is.EqualTo(20L), "toValue");
            Assert.That(attr[4], Is.EqualTo(0L), "decidedFromValue: attribute not yet purchased");
            Assert.That(attr[5], Is.EqualTo(true), "exceedsNormalCap: 20 > 15, decided by Rules in the service");
            Assert.That(attr[6], Is.EqualTo(40L), "decidedCost: 20 * 2");

            calls.Clear();
            CharacterAdvancementService.PurchaseSkillLevel(recording, _campaign, character.CharacterId, Tactics, 5, _gm, true, c.Revisions.MechanicsRevision, 0, NewCommandId(), TestCorrelationId);
            object?[] skill = calls.Single(x => x.Name == nameof(ICharacterRepository.PurchaseSkillLevel)).Args;
            Assert.That(skill[5], Is.EqualTo(true), "requiresRecommendation: level 5");
            Assert.That(skill[6], Is.EqualTo(0L), "no cost is computed for a purchase the recommendation pipeline owns");

            calls.Clear();
            CharacterAdvancementService.AcquireAbility(recording, _campaign, character.CharacterId, Fireball, SourceKind.ProgressionPurchase, null, RankMode.None, null, null, "{}", _gm, true, c.Revisions.MechanicsRevision, c.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);
            Assert.That(calls.Single().Args[9], Is.EqualTo(5L), "progressionPurchaseCost");

            calls.Clear();
            CharacterAdvancementService.AcquireAbility(recording, _campaign, character.CharacterId, Fireball, SourceKind.GMGrant, null, RankMode.None, null, null, "{}", _gm, true, null, c.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);
            Assert.That(calls.Single().Args[9], Is.EqualTo(0L), "Rules is not consulted for a non-purchase source");

            calls.Clear();
            CharacterAdvancementService.RequestSkillAdvancedRecommendation(recording, _campaign, character.CharacterId, Tactics, 3, new CriticalSuccessEvidenceId[0], _gm, true, c.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            object?[] rec = calls.Single(x => x.Name == nameof(ICharacterRepository.RequestSkillAdvancedRecommendation)).Args;
            Assert.That(rec[4], Is.EqualTo(0L), "decidedFromLevel");
            Assert.That(rec[5], Is.EqualTo(9L), "decidedReservedAmount: 3 * 3");

            calls.Clear();
            Assert.Throws<ArgumentException>(new Action(() => CharacterAdvancementService.PurchaseAttributeIncrease(recording, _campaign, character.CharacterId, default, 3, _gm, true, 1, 0, NewCommandId(), TestCorrelationId)));
            Assert.That(calls, Is.Empty, "invalid input is rejected before any repository call, read included");

            Assert.Throws<ArgumentNullException>(new Action(() => CharacterAdvancementService.PurchaseAttributeIncrease(null!, _campaign, character.CharacterId, Strength, 3, _gm, true, 1, 0, NewCommandId(), TestCorrelationId)));
            Assert.Throws<ArgumentNullException>(new Action(() => CharacterAdvancementService.PurchaseSkillLevel(null!, _campaign, character.CharacterId, Tactics, 3, _gm, true, 1, 0, NewCommandId(), TestCorrelationId)));
            Assert.Throws<ArgumentNullException>(new Action(() => CharacterAdvancementService.RequestSkillAdvancedRecommendation(null!, _campaign, character.CharacterId, Tactics, 3, new CriticalSuccessEvidenceId[0], _gm, true, 1, NewCommandId(), TestCorrelationId)));
            Assert.Throws<ArgumentNullException>(new Action(() => CharacterAdvancementService.AcquireAbility(null!, _campaign, character.CharacterId, Fireball, SourceKind.ProgressionPurchase, null, RankMode.None, null, null, "{}", _gm, true, 1, 1, NewCommandId(), TestCorrelationId)));
        }

        // ------------------------------------------------------------------ interception double

        /// <summary>
        /// Wraps a real <see cref="ICharacterRepository"/>: the hook may answer a call itself; otherwise the call goes
        /// through to the real repository.
        /// </summary>
        public class InterceptingProxy : DispatchProxy
        {
            private ICharacterRepository _inner = null!;
            private Func<MethodInfo, object?[]?, (bool Handled, object? Result)> _hook = null!;

            public static ICharacterRepository Create(ICharacterRepository inner, Func<MethodInfo, object?[]?, (bool Handled, object? Result)> hook)
            {
                ICharacterRepository proxy = DispatchProxy.Create<ICharacterRepository, InterceptingProxy>();
                var self = (InterceptingProxy)(object)proxy;
                self._inner = inner;
                self._hook = hook;
                return proxy;
            }

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                (bool handled, object? result) = _hook(targetMethod!, args);
                if (handled) return result;
                try
                {
                    return targetMethod!.Invoke(_inner, args);
                }
                catch (TargetInvocationException ex) when (ex.InnerException != null)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                    throw;
                }
            }
        }
    }
}
