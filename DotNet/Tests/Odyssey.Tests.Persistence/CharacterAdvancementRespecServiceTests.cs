using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// ODY-S09-103: respec. The plan (which purchases are returned, what is repurchased, at what cost) is computed by
    /// <see cref="CharacterAdvancementService"/>; <see cref="SqliteCharacterRepository.ApplyCharacterRespec"/> receives
    /// it decided, together with the MechanicsRevision it was computed at, and checks it against the locked state.
    /// Numbers are literal fixture values (attribute 2/point, skill 3/point), written out on purpose. Real temp-directory
    /// SQLite campaign; the interception double is the <see cref="CharacterAdvancementCostServiceTests.InterceptingProxy"/>
    /// over the real repository.
    /// </summary>
    public sealed class CharacterAdvancementRespecServiceTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
        private static readonly AttributeDefinitionId Strength = AttributeDefinitionId.Parse("Strength");
        private static readonly SkillDefinitionId Tactics = SkillDefinitionId.Parse("Tactics");
        private static readonly SkillDefinitionId Stealth = SkillDefinitionId.Parse("Stealth");
        private const string Reason = "GM correction";

        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;
        private SqliteCharacterRepository _repo = null!;
        private UserId _gm;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s09-103-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_campaignDir, "Respec Service Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
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

        private CharacterRecord Reload(CharacterRecord character)
        {
            Result<CharacterRecord> read = _repo.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(read.IsSuccess, Is.True);
            return read.Value;
        }

        private CharacterRecord CreateCharacter(long points)
        {
            Result<CharacterRecord> created = _repo.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "Respec Character"), NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            Result<CharacterRecord> granted = _repo.GrantDevelopmentPoints(_campaign, created.Value.CharacterId, points, "Grant", _gm, actorIsMainGm: true, created.Value.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(granted.IsSuccess, Is.True);
            return granted.Value;
        }

        private void BuyAttribute(CharacterRecord character, long toValue)
        {
            CharacterRecord c = Reload(character);
            long rev = c.Attributes.FirstOrDefault(a => a.AttributeDefinitionId.Equals(Strength))?.Revision ?? 0;
            Assert.That(CharacterAdvancementService.PurchaseAttributeIncrease(_repo, _campaign, c.CharacterId, Strength, toValue, _gm, true, c.Revisions.MechanicsRevision, rev, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);
        }

        private void BuySkill(CharacterRecord character, SkillDefinitionId skill, long toLevel)
        {
            CharacterRecord c = Reload(character);
            long rev = c.Skills.FirstOrDefault(s => s.SkillDefinitionId.Equals(skill))?.Revision ?? 0;
            Assert.That(CharacterAdvancementService.PurchaseSkillLevel(_repo, _campaign, c.CharacterId, skill, toLevel, _gm, true, c.Revisions.MechanicsRevision, rev, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);
        }

        // Strength bought 0->3 (cost 6) and 3->5 (cost 4); Tactics 0->2 (cost 6); Stealth 0->1 (cost 3).
        private CharacterRecord MultiTargetCharacter()
        {
            CharacterRecord character = CreateCharacter(100);
            BuyAttribute(character, 3);
            BuyAttribute(character, 5);
            BuySkill(character, Tactics, 2);
            BuySkill(character, Stealth, 1);
            return character;
        }

        private static CharacterRespecTarget[] MultiTargets() => new[]
        {
            new CharacterRespecTarget(AdvancementOperationKind.AttributeIncrease, Strength.ToString(), desiredValue: 4),
            new CharacterRespecTarget(AdvancementOperationKind.SkillLevelPurchase, Tactics.ToString(), desiredValue: 3),
            new CharacterRespecTarget(AdvancementOperationKind.SkillLevelPurchase, Stealth.ToString(), desiredValue: 1), // already there: no entries
        };

        private string Snapshot(CharacterRecord character)
        {
            CharacterRecord c = Reload(character);
            string attrs = string.Join(",", c.Attributes.Select(a => $"{a.AttributeDefinitionId}:{a.BaseValue}:{a.SpentDevelopmentPoints}:{a.Revision}"));
            string skills = string.Join(",", c.Skills.Select(s => $"{s.SkillDefinitionId}:{s.Level}:{s.SpentDevelopmentPoints}:{s.Revision}"));
            string ledger = string.Join(",", _repo.GetDevelopmentLedger(_campaign, c.CharacterId, TestCorrelationId).Value.Select(l => $"{l.Kind}:{l.Amount}"));
            string purchases = string.Join(",", _repo.GetAdvancementPurchases(_campaign, c.CharacterId, TestCorrelationId).Value.Select(p => $"{p.TargetDefinitionId}:{p.ToValue}:{p.Cost}:{p.Status}"));
            int history = _repo.GetCharacterHistory(_campaign, c.CharacterId, TestCorrelationId).Value.Count;
            return $"pool={c.DevelopmentPool.Earned}/{c.DevelopmentPool.Spent}/{c.DevelopmentPool.Reserved}|attrs={attrs}|skills={skills}|rev={c.Revisions.CharacterRevision}/{c.Revisions.MechanicsRevision}|ledger={ledger}|purchases={purchases}|history={history}";
        }

        private Result<CharacterRecord> Apply(ICharacterRepository repo, CharacterRecord character, CharacterRespecTarget[] targets, bool asMainGm = true, string reason = Reason, CharacterRecord? revisionBasis = null)
        {
            CharacterRecord c = revisionBasis ?? Reload(character);
            return CharacterAdvancementService.ApplyCharacterRespec(repo, _campaign, character.CharacterId, targets, reason, asMainGm ? _gm : NewUserId(), asMainGm, c.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
        }

        // ------------------------------------------------------------------ preview

        [Test] // TC-CHAR-203
        public void PreviewCharacterRespec_MultiTargetPlan_HasTheExactEntriesAndTotals()
        {
            CharacterRecord character = MultiTargetCharacter();

            Result<CharacterRespecPreview> preview = CharacterAdvancementService.PreviewCharacterRespec(_repo, _campaign, character.CharacterId, MultiTargets(), TestCorrelationId);

            Assert.That(preview.IsSuccess, Is.True);
            string[] entries = preview.Value.Entries.Select(e => $"{e.Action}:{e.TargetDefinitionId}:{e.Amount}").ToArray();
            Assert.That(entries, Is.EqualTo(new[]
            {
                "Return:Strength:6",   // 0 -> 3
                "Return:Strength:4",   // 3 -> 5
                "Spend:Strength:8",    // 0 -> 4 from scratch: 4 * 2
                "Return:Tactics:6",    // 0 -> 2
                "Spend:Tactics:9",     // 0 -> 3 from scratch: 3 * 3
            }), "Stealth is already at its desired level and contributes nothing");
            Assert.That(preview.Value.TotalReturned, Is.EqualTo(16));
            Assert.That(preview.Value.TotalSpent, Is.EqualTo(17));
            Assert.That(preview.Value.NetAvailableChange, Is.EqualTo(-1));
            Assert.That(preview.Value.Entries.Where(e => e.Action == CharacterRespecPlanAction.Return).All(e => e.SourcePurchaseId.HasValue), Is.True);
            Assert.That(preview.Value.Entries.Where(e => e.Action == CharacterRespecPlanAction.Spend).All(e => !e.SourcePurchaseId.HasValue), Is.True);
        }

        [Test] // TC-CHAR-204
        public void PreviewCharacterRespec_IsAPureRead_NoWritesNoRevisionNoGate()
        {
            CharacterRecord character = MultiTargetCharacter();
            string before = Snapshot(character);
            var calls = new List<string>();
            ICharacterRepository recording = CharacterAdvancementCostServiceTests.InterceptingProxy.Create(_repo, (method, args) =>
            {
                calls.Add(method.Name);
                return (false, null);
            });

            Result<CharacterRespecPreview> first = CharacterAdvancementService.PreviewCharacterRespec(recording, _campaign, character.CharacterId, MultiTargets(), TestCorrelationId);
            Result<CharacterRespecPreview> second = CharacterAdvancementService.PreviewCharacterRespec(recording, _campaign, character.CharacterId, MultiTargets(), TestCorrelationId);

            Assert.That(first.IsSuccess && second.IsSuccess, Is.True);
            Assert.That(second.Value.TotalReturned, Is.EqualTo(first.Value.TotalReturned));
            Assert.That(second.Value.TotalSpent, Is.EqualTo(first.Value.TotalSpent));
            Assert.That(calls.Distinct(), Is.EquivalentTo(new[] { nameof(ICharacterRepository.GetCharacter), nameof(ICharacterRepository.GetAdvancementPurchases) }), "only reads reach the repository");
            Assert.That(Snapshot(character), Is.EqualTo(before), "pool, attributes, revisions, ledger, purchases and event history are untouched");

            // No revision is asked of, or checked against, a preview: it works on any state, and reports the same
            // typed failures as before for a missing character or an unsupported kind.
            Result<CharacterRespecPreview> missing = CharacterAdvancementService.PreviewCharacterRespec(_repo, _campaign, CharacterId.NewId(Clock.GetUtcNow()), MultiTargets(), TestCorrelationId);
            Assert.That(missing.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterNotFound));
            Result<CharacterRespecPreview> unsupported = CharacterAdvancementService.PreviewCharacterRespec(_repo, _campaign, character.CharacterId, new[] { new CharacterRespecTarget(AdvancementOperationKind.AbilityAcquisition, "Fireball", 0) }, TestCorrelationId);
            Assert.That(unsupported.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterAdvancementOperationKindNotSupported));
        }

        // ------------------------------------------------------------------ apply

        [Test] // TC-CHAR-205
        public void ApplyCharacterRespec_LegalMultiTargetPlan_AppliesExactlyThePlan()
        {
            CharacterRecord character = MultiTargetCharacter();
            Assert.That(Reload(character).DevelopmentPool.Spent, Is.EqualTo(19), "6 + 4 + 6 + 3 before the respec");

            Result<CharacterRecord> applied = Apply(_repo, character, MultiTargets());

            Assert.That(applied.IsSuccess, Is.True);
            Assert.That(applied.Value.DevelopmentPool.Spent, Is.EqualTo(20), "19 - 16 returned + 17 repurchased");
            Assert.That(applied.Value.DevelopmentPool.Available, Is.EqualTo(80));
            Assert.That(applied.Value.Attributes.Single().BaseValue, Is.EqualTo(4));
            Assert.That(applied.Value.Attributes.Single().SpentDevelopmentPoints, Is.EqualTo(8));
            Assert.That(applied.Value.Skills.Single(s => s.SkillDefinitionId.Equals(Tactics)).Level, Is.EqualTo(3));
            Assert.That(applied.Value.Skills.Single(s => s.SkillDefinitionId.Equals(Tactics)).SpentDevelopmentPoints, Is.EqualTo(9));
            Assert.That(applied.Value.Skills.Single(s => s.SkillDefinitionId.Equals(Stealth)).Level, Is.EqualTo(1), "an untouched target keeps its state");

            IReadOnlyList<DevelopmentTransactionRecord> ledger = _repo.GetDevelopmentLedger(_campaign, character.CharacterId, TestCorrelationId).Value;
            Assert.That(ledger.Where(l => l.Kind == DevelopmentTransactionKind.RespecReturn).Select(l => l.Amount), Is.EquivalentTo(new long[] { 6, 4, 6 }));
            Assert.That(ledger.Where(l => l.Kind == DevelopmentTransactionKind.RespecSpend).Select(l => l.Amount), Is.EquivalentTo(new long[] { 8, 9 }));

            IReadOnlyList<AdvancementPurchase> purchases = _repo.GetAdvancementPurchases(_campaign, character.CharacterId, TestCorrelationId).Value;
            Assert.That(purchases.Count(p => p.Status == AdvancementPurchaseStatus.SupersededByRespec), Is.EqualTo(3));
            Assert.That(purchases.Where(p => p.Status == AdvancementPurchaseStatus.Applied).Select(p => $"{p.TargetDefinitionId}:{p.ToValue}:{p.Cost}"), Is.EquivalentTo(new[] { "Stealth:1:3", "Strength:4:8", "Tactics:3:9" }));
        }

        [Test] // TC-CHAR-206
        public void ApplyCharacterRespec_RejectedCases_LeaveNothingWritten_AndKeepTheirTypedErrors()
        {
            CharacterRecord character = MultiTargetCharacter();
            string before = Snapshot(character);

            Assert.That(Apply(_repo, character, MultiTargets(), asMainGm: false).Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterAdvancementOperationDenied));
            Assert.That(Apply(_repo, character, MultiTargets(), reason: "  ").Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterAdvancementReasonRequired));
            Assert.That(Apply(_repo, character, new[] { new CharacterRespecTarget(AdvancementOperationKind.AbilityAcquisition, "Fireball", 0) }).Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterAdvancementOperationKindNotSupported));

            // Denied and reason-required are reported ahead of everything, exactly as before -- even for a missing character.
            var missing = CharacterId.NewId(Clock.GetUtcNow());
            Assert.That(CharacterAdvancementService.ApplyCharacterRespec(_repo, _campaign, missing, MultiTargets(), Reason, NewUserId(), false, 1, NewCommandId(), TestCorrelationId).Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterAdvancementOperationDenied));
            Assert.That(CharacterAdvancementService.ApplyCharacterRespec(_repo, _campaign, missing, MultiTargets(), Reason, _gm, true, 1, NewCommandId(), TestCorrelationId).Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterNotFound));

            Assert.That(Snapshot(character), Is.EqualTo(before));
        }

        [Test] // TC-CHAR-207
        public void ApplyCharacterRespec_PlanThatBecameInvalid_IsRejected_ByTheRepository_WithNothingWritten()
        {
            CharacterRecord character = MultiTargetCharacter();
            CharacterRecord atPlanTime = Reload(character);
            Result<CharacterRespecPreview> plan = CharacterAdvancementService.PreviewCharacterRespec(_repo, _campaign, character.CharacterId, MultiTargets(), TestCorrelationId);
            Assert.That(plan.IsSuccess, Is.True);

            // A competing change lands after the plan was built.
            BuyAttribute(character, 6);
            string afterCompetitor = Snapshot(character);
            CharacterRecord now = Reload(character);

            // (a) The stale plan with the revision it was computed at: the locked revision has moved.
            Result<CharacterRecord> stale = _repo.ApplyCharacterRespec(_campaign, character.CharacterId, MultiTargets(), plan.Value, atPlanTime.Revisions.MechanicsRevision, Reason, _gm, true, now.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(stale.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRevisionConflict));
            Assert.That(Snapshot(character), Is.EqualTo(afterCompetitor));

            // (b) After a legitimate respec supersedes those purchases, the old plan is resubmitted with the CURRENT
            // revision on both sides, so both revision checks pass; only the check of the plan against the locked
            // purchase history is left, and it rejects the Return entries that name superseded purchases.
            Result<CharacterRecord> ok = Apply(_repo, character, MultiTargets());
            Assert.That(ok.IsSuccess, Is.True);
            string afterRespec = Snapshot(character);
            CharacterRecord afterRespecRecord = Reload(character);
            Result<CharacterRecord> replayedOldPlan = _repo.ApplyCharacterRespec(_campaign, character.CharacterId, MultiTargets(), plan.Value, afterRespecRecord.Revisions.MechanicsRevision, Reason, _gm, true, afterRespecRecord.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(replayedOldPlan.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRevisionConflict), "its Return entries point at purchases the respec already superseded");
            Assert.That(Snapshot(character), Is.EqualTo(afterRespec));
        }

        [Test] // TC-CHAR-208
        public void ApplyCharacterRespec_CompetingWriteBetweenServiceReadAndCommit_IsRejected_NotSilentlyApplied()
        {
            CharacterRecord character = CreateCharacter(100);
            BuyAttribute(character, 3);
            CharacterRecord atStart = Reload(character);
            var targets = new[] { new CharacterRespecTarget(AdvancementOperationKind.AttributeIncrease, Strength.ToString(), desiredValue: 3) };

            // The read the service makes is stale by the time it commits: a competing purchase (Strength 3 -> 4) lands
            // right after the service read the Character. Against the stale read Strength is already at the desired 3,
            // so the plan it builds is EMPTY -- committing it would report success while leaving Strength at 4.
            string afterCompetitor = string.Empty;
            ICharacterRepository racing = CharacterAdvancementCostServiceTests.InterceptingProxy.Create(_repo, (method, args) =>
            {
                if (method.Name != nameof(ICharacterRepository.GetCharacter)) return (false, null);
                object? snapshot = method.Invoke(_repo, args);
                BuyAttribute(character, 4);
                afterCompetitor = Snapshot(character);
                return (true, snapshot);
            });

            // First race: the caller only knows the revision it started with, so the revision gate rejects.
            Result<CharacterRecord> withStartRevision = Apply(racing, character, targets, revisionBasis: atStart);
            Assert.That(withStartRevision.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRevisionConflict), "caller-side gate");
            Assert.That(Snapshot(character), Is.EqualTo(afterCompetitor));

            // Second race: the caller already holds the post-competitor revision, so the caller-side gate would pass;
            // only the revision the plan was computed at can catch the stale read.
            CharacterRecord fresh = Reload(character);
            int reads = 0;
            ICharacterRepository staleReader = CharacterAdvancementCostServiceTests.InterceptingProxy.Create(_repo, (method, args) =>
            {
                if (method.Name != nameof(ICharacterRepository.GetCharacter)) return (false, null);
                reads++;
                return (true, Result<CharacterRecord>.Success(atStart));
            });
            Result<CharacterRecord> basisOnly = Apply(staleReader, character, targets, revisionBasis: fresh);
            Assert.That(reads, Is.EqualTo(1));
            Assert.That(basisOnly.IsFailure, Is.True, "an empty plan built from a stale read must not be committed as a success");
            Assert.That(basisOnly.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRevisionConflict));
            Assert.That(Snapshot(character), Is.EqualTo(afterCompetitor));
            Assert.That(Reload(character).Attributes.Single().BaseValue, Is.EqualTo(4));
        }

        // ------------------------------------------------------------------ the service depends only on the port

        [Test] // TC-CHAR-209
        public void RespecService_UsesOnlyTheICharacterRepositoryPort_AndAlwaysCallsApply()
        {
            CharacterRecord character = MultiTargetCharacter();
            CharacterRecord c = Reload(character);
            var calls = new List<(string Name, object?[] Args)>();
            ICharacterRepository recording = CharacterAdvancementCostServiceTests.InterceptingProxy.Create(_repo, (method, args) =>
            {
                calls.Add((method.Name, args ?? Array.Empty<object?>()));
                if (method.Name == nameof(ICharacterRepository.ApplyCharacterRespec)) return (true, Result<CharacterRecord>.Success(c));
                return (false, null);
            });

            CharacterAdvancementService.ApplyCharacterRespec(recording, _campaign, character.CharacterId, MultiTargets(), Reason, _gm, true, c.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(calls.Select(x => x.Name), Is.EqualTo(new[] { nameof(ICharacterRepository.GetCharacter), nameof(ICharacterRepository.GetAdvancementPurchases), nameof(ICharacterRepository.ApplyCharacterRespec) }));
            object?[] apply = calls.Last().Args;
            var plan = (CharacterRespecPreview)apply[3]!;
            Assert.That(plan.TotalReturned, Is.EqualTo(16), "the plan is decided by the service");
            Assert.That(plan.TotalSpent, Is.EqualTo(17));
            Assert.That(apply[4], Is.EqualTo(c.Revisions.MechanicsRevision), "decidedMechanicsRevision is the revision the plan was read at");
            Assert.That(apply[5], Is.EqualTo(Reason));

            // Unsupported kind: the repository is still called (nothing short-circuits), with an empty plan.
            calls.Clear();
            CharacterAdvancementService.ApplyCharacterRespec(recording, _campaign, character.CharacterId, new[] { new CharacterRespecTarget(AdvancementOperationKind.AbilityAcquisition, "Fireball", 0) }, Reason, _gm, true, c.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            var emptyPlan = (CharacterRespecPreview)calls.Last().Args[3]!;
            Assert.That(emptyPlan.Entries, Is.Empty);
            Assert.That(calls.Last().Name, Is.EqualTo(nameof(ICharacterRepository.ApplyCharacterRespec)));

            // Missing character: still called, with a revision no Character can have.
            calls.Clear();
            var missing = CharacterId.NewId(Clock.GetUtcNow());
            CharacterAdvancementService.ApplyCharacterRespec(recording, _campaign, missing, MultiTargets(), Reason, _gm, true, 1, NewCommandId(), TestCorrelationId);
            Assert.That(calls.Last().Name, Is.EqualTo(nameof(ICharacterRepository.ApplyCharacterRespec)));
            Assert.That(calls.Last().Args[4], Is.EqualTo(0L));

            // Invalid input throws before any repository call; a null repository is rejected.
            calls.Clear();
            Assert.Throws<ArgumentException>(new Action(() => CharacterAdvancementService.ApplyCharacterRespec(recording, _campaign, character.CharacterId, new CharacterRespecTarget[0], Reason, _gm, true, 1, NewCommandId(), TestCorrelationId)));
            Assert.Throws<ArgumentException>(new Action(() => CharacterAdvancementService.PreviewCharacterRespec(recording, _campaign, character.CharacterId, new CharacterRespecTarget[0], TestCorrelationId)));
            Assert.That(calls, Is.Empty);
            Assert.Throws<ArgumentNullException>(new Action(() => CharacterAdvancementService.ApplyCharacterRespec(null!, _campaign, character.CharacterId, MultiTargets(), Reason, _gm, true, 1, NewCommandId(), TestCorrelationId)));
            Assert.Throws<ArgumentNullException>(new Action(() => CharacterAdvancementService.PreviewCharacterRespec(null!, _campaign, character.CharacterId, MultiTargets(), TestCorrelationId)));
        }
    }
}
