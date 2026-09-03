using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S04-107: real, non-stubbed tests against a real temp-directory
    /// campaign and a real SQLite database, mirroring
    /// <see cref="CharacterSkillPurchaseCriticalEvidenceTests"/>'s exact
    /// fixture convention.
    ///
    /// Block 0 (pkt 0 gap fix): confirms <c>PurchaseAttributeIncrease</c>/
    /// <c>PurchaseSkillLevel</c>/<c>ResolveAdvancementRecommendation</c>'s
    /// approve branch now co-commit an <see cref="AdvancementPurchase"/>
    /// record (ADR-024 section 3.3/5.1 step 4). The regression requirement
    /// itself -- every pre-existing ODY-S04-105/106 test still passes with
    /// its own assertions unmodified -- is verified by the unmodified
    /// <c>CharacterDevelopmentPoolAttributePurchaseTests</c>/
    /// <c>CharacterSkillPurchaseCriticalEvidenceTests</c> files continuing to
    /// pass unchanged (not re-asserted here).
    ///
    /// Block Б: <c>RevertAdvancementPurchase</c>/<c>PreviewCharacterRespec</c>/
    /// <c>ApplyCharacterRespec</c> (ADR-024 section 6.2/7.2).
    /// </summary>
    public sealed class CharacterAdvancementRevertRespecTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
        private static readonly AttributeDefinitionId Strength = AttributeDefinitionId.Parse("Strength");
        private static readonly SkillDefinitionId Stealth = SkillDefinitionId.Parse("Stealth");
        private const string GmReason = "GM correction";

        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;
        private SqliteCharacterRepository _characterRepository = null!;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s04-107-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_campaignDir, "Revert Respec Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = _campaignRepository.Create(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _characterRepository = new SqliteCharacterRepository(Clock);
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaignRepository.Close(_campaign, TestCorrelationId); }
            catch (IOException) { }

            try { if (Directory.Exists(_campaignDir)) Directory.Delete(_campaignDir, recursive: true); }
            catch (IOException) { }
        }

        private CharacterRecord CreateCharacter(string name = "Revert Respec Character")
        {
            var request = new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, name);
            Result<CharacterRecord> created = _characterRepository.CreateCharacter(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        private CharacterRecord GrantPoints(CharacterRecord character, long amount)
        {
            Result<CharacterRecord> granted = _characterRepository.GrantDevelopmentPoints(_campaign, character.CharacterId, amount, "Grant", NewUserId(), actorIsMainGm: true, character.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(granted.IsSuccess, Is.True);
            return granted.Value;
        }

        private SqliteConnection OpenReadOnly() => new SqliteConnection("Data Source=" + Path.Combine(_campaignDir, "campaign.db") + ";Mode=ReadOnly");

        private static long ReadLong(SqliteConnection connection, string sql, params string[] parameters)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            for (int index = 0; index < parameters.Length; index++)
            {
                command.Parameters.AddWithValue("$c" + index, parameters[index]);
            }

            object? result = command.ExecuteScalar();
            return result == null || result is DBNull ? 0L : Convert.ToInt64(result);
        }

        private static string ReadString(SqliteConnection connection, string sql, params string[] parameters)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            for (int index = 0; index < parameters.Length; index++)
            {
                command.Parameters.AddWithValue("$c" + index, parameters[index]);
            }

            object? result = command.ExecuteScalar();
            return result == null || result is DBNull ? string.Empty : (string)result;
        }

        // ---- Block 0: AdvancementPurchase creation --------------------------------

        [Test]
        public void PurchaseAttributeIncrease_CreatesAdvancementPurchase_Applied()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 20);

            Result<CharacterRecord> purchased = _characterRepository.PurchaseAttributeIncrease(_campaign, character.CharacterId, Strength, toValue: 3, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, expectedAttributeRevision: 0, NewCommandId(), TestCorrelationId);
            Assert.That(purchased.IsSuccess, Is.True);

            Result<IReadOnlyList<AdvancementPurchase>> purchases = _characterRepository.GetAdvancementPurchases(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(purchases.IsSuccess, Is.True);
            AdvancementPurchase purchase = purchases.Value.Single();
            Assert.That(purchase.OperationKind, Is.EqualTo(AdvancementOperationKind.AttributeIncrease));
            Assert.That(purchase.TargetDefinitionId, Is.EqualTo(Strength.ToString()));
            Assert.That(purchase.FromValue, Is.EqualTo(0));
            Assert.That(purchase.ToValue, Is.EqualTo(3));
            Assert.That(purchase.Cost, Is.EqualTo(6)); // CostPerAttributePoint == 2 (fixture)
            Assert.That(purchase.Status, Is.EqualTo(AdvancementPurchaseStatus.Applied));
        }

        [Test]
        public void PurchaseSkillLevel_CreatesAdvancementPurchase_Applied()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 20);

            Result<CharacterRecord> purchased = _characterRepository.PurchaseSkillLevel(_campaign, character.CharacterId, Stealth, toLevel: 2, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, expectedSkillRevision: 0, NewCommandId(), TestCorrelationId);
            Assert.That(purchased.IsSuccess, Is.True);

            AdvancementPurchase purchase = _characterRepository.GetAdvancementPurchases(_campaign, character.CharacterId, TestCorrelationId).Value.Single();
            Assert.That(purchase.OperationKind, Is.EqualTo(AdvancementOperationKind.SkillLevelPurchase));
            Assert.That(purchase.FromValue, Is.EqualTo(0));
            Assert.That(purchase.ToValue, Is.EqualTo(2));
            Assert.That(purchase.Cost, Is.EqualTo(6)); // CostPerSkillPoint == 3 (fixture)
            Assert.That(purchase.Status, Is.EqualTo(AdvancementPurchaseStatus.Applied));
        }

        [Test]
        public void ResolveAdvancementRecommendation_ApproveWithSpend_CreatesAdvancementPurchase()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 100);
            UserId gm = NewUserId();

            Result<AdvancementRecommendationRecord> requested = _characterRepository.RequestSkillAdvancedRecommendation(_campaign, character.CharacterId, Stealth, targetLevel: 5, Array.Empty<CriticalSuccessEvidenceId>(), gm, actorIsMainGm: true, granted.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(requested.IsSuccess, Is.True);
            AdvancementRecommendationRecord recommendation = requested.Value;

            Result<CharacterRecord> afterReserve = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Result<CharacterRecord> resolved = _characterRepository.ResolveAdvancementRecommendation(_campaign, character.CharacterId, recommendation.RecommendationId, approve: true, spendReservedPoints: true, gm, actorIsMainGm: true, afterReserve.Value.Revisions.MechanicsRevision, recommendation.Revision, NewCommandId(), TestCorrelationId);
            Assert.That(resolved.IsSuccess, Is.True);

            AdvancementPurchase purchase = _characterRepository.GetAdvancementPurchases(_campaign, character.CharacterId, TestCorrelationId).Value.Single();
            Assert.That(purchase.FromValue, Is.EqualTo(0));
            Assert.That(purchase.ToValue, Is.EqualTo(5));
            Assert.That(purchase.Cost, Is.EqualTo(recommendation.ReservedAmount));
            Assert.That(purchase.Status, Is.EqualTo(AdvancementPurchaseStatus.Applied));
        }

        [Test]
        public void ResolveAdvancementRecommendation_ApproveWithoutSpend_CreatesAdvancementPurchase_WithZeroCost()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 100);
            UserId gm = NewUserId();

            Result<AdvancementRecommendationRecord> requested = _characterRepository.RequestSkillAdvancedRecommendation(_campaign, character.CharacterId, Stealth, targetLevel: 5, Array.Empty<CriticalSuccessEvidenceId>(), gm, actorIsMainGm: true, granted.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(requested.IsSuccess, Is.True);
            AdvancementRecommendationRecord recommendation = requested.Value;

            Result<CharacterRecord> afterReserve = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Result<CharacterRecord> resolved = _characterRepository.ResolveAdvancementRecommendation(_campaign, character.CharacterId, recommendation.RecommendationId, approve: true, spendReservedPoints: false, gm, actorIsMainGm: true, afterReserve.Value.Revisions.MechanicsRevision, recommendation.Revision, NewCommandId(), TestCorrelationId);
            Assert.That(resolved.IsSuccess, Is.True);

            AdvancementPurchase purchase = _characterRepository.GetAdvancementPurchases(_campaign, character.CharacterId, TestCorrelationId).Value.Single();
            Assert.That(purchase.ToValue, Is.EqualTo(5));
            Assert.That(purchase.Cost, Is.EqualTo(0)); // ADR-024 section 6.1 branch 3: fully evidence-funded.
            Assert.That(purchase.Status, Is.EqualTo(AdvancementPurchaseStatus.Applied));
        }

        [Test]
        public void ResolveAdvancementRecommendation_Dismiss_CreatesNoAdvancementPurchase()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 100);
            UserId gm = NewUserId();

            Result<AdvancementRecommendationRecord> requested = _characterRepository.RequestSkillAdvancedRecommendation(_campaign, character.CharacterId, Stealth, targetLevel: 5, Array.Empty<CriticalSuccessEvidenceId>(), gm, actorIsMainGm: true, granted.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            AdvancementRecommendationRecord recommendation = requested.Value;

            Result<CharacterRecord> afterReserve = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Result<CharacterRecord> resolved = _characterRepository.ResolveAdvancementRecommendation(_campaign, character.CharacterId, recommendation.RecommendationId, approve: false, spendReservedPoints: false, gm, actorIsMainGm: true, afterReserve.Value.Revisions.MechanicsRevision, recommendation.Revision, NewCommandId(), TestCorrelationId);
            Assert.That(resolved.IsSuccess, Is.True);

            Assert.That(_characterRepository.GetAdvancementPurchases(_campaign, character.CharacterId, TestCorrelationId).Value, Is.Empty);
        }

        // ---- Block Б: RevertAdvancementPurchase -----------------------------------

        [Test]
        public void RevertAdvancementPurchase_OnIndependentPurchase_Succeeds()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 20);
            Result<CharacterRecord> purchased = _characterRepository.PurchaseAttributeIncrease(_campaign, character.CharacterId, Strength, toValue: 3, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, expectedAttributeRevision: 0, NewCommandId(), TestCorrelationId);
            AdvancementPurchase purchase = _characterRepository.GetAdvancementPurchases(_campaign, character.CharacterId, TestCorrelationId).Value.Single();

            using (SqliteConnection connection = OpenReadOnly())
            {
                connection.Open();
                long originalEventCountBefore = ReadLong(connection, "SELECT COUNT(*) FROM DomainEvents WHERE EventType = 'odyssey.persistence.character_attribute_increased'");
                Assert.That(originalEventCountBefore, Is.EqualTo(1));
            }

            Result<CharacterRecord> reverted = _characterRepository.RevertAdvancementPurchase(_campaign, character.CharacterId, purchase.PurchaseId, GmReason, NewUserId(), actorIsMainGm: true, purchased.Value.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);

            Assert.That(reverted.IsSuccess, Is.True);
            Assert.That(reverted.Value.Attributes.Single(a => a.AttributeDefinitionId.Equals(Strength)).BaseValue, Is.EqualTo(0));
            Assert.That(reverted.Value.DevelopmentPool.Available, Is.EqualTo(20));
            Assert.That(reverted.Value.DevelopmentPool.Spent, Is.EqualTo(0));

            AdvancementPurchase revertedRecord = _characterRepository.GetAdvancementPurchases(_campaign, character.CharacterId, TestCorrelationId).Value.Single();
            Assert.That(revertedRecord.Status, Is.EqualTo(AdvancementPurchaseStatus.Reverted));

            using (SqliteConnection connection = OpenReadOnly())
            {
                connection.Open();
                // The original forward event is neither deleted nor mutated -- ADR-012 section 6.
                long originalEventCountAfter = ReadLong(connection, "SELECT COUNT(*) FROM DomainEvents WHERE EventType = 'odyssey.persistence.character_attribute_increased'");
                Assert.That(originalEventCountAfter, Is.EqualTo(1));
                string originalPayload = ReadString(connection, "SELECT PayloadJson FROM DomainEvents WHERE EventType = 'odyssey.persistence.character_attribute_increased'");
                Assert.That(originalPayload, Does.Contain("\"toValue\":3"));

                long compensatingCount = ReadLong(connection, "SELECT COUNT(*) FROM DomainEvents WHERE EventType = 'odyssey.persistence.character_advancement_purchase_reverted' AND IsCompensating = 1");
                Assert.That(compensatingCount, Is.EqualTo(1));
                long originalEventId = ReadLong(connection, "SELECT EventSequence FROM DomainEvents WHERE EventType = 'odyssey.persistence.character_attribute_increased'");
                long referencedOriginalEventId = ReadLong(connection, "SELECT OriginalEventId FROM DomainEvents WHERE EventType = 'odyssey.persistence.character_advancement_purchase_reverted'");
                Assert.That(referencedOriginalEventId, Is.EqualTo(originalEventId));
            }
        }

        [Test]
        public void RevertAdvancementPurchase_WithDependentLaterPurchase_IsRejected_NoStateChange()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 20);
            Result<CharacterRecord> first = _characterRepository.PurchaseAttributeIncrease(_campaign, character.CharacterId, Strength, toValue: 3, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, expectedAttributeRevision: 0, NewCommandId(), TestCorrelationId);
            AdvancementPurchase firstPurchase = _characterRepository.GetAdvancementPurchases(_campaign, character.CharacterId, TestCorrelationId).Value.Single();
            Result<CharacterRecord> second = _characterRepository.PurchaseAttributeIncrease(_campaign, character.CharacterId, Strength, toValue: 5, NewUserId(), actorIsMainGm: true, first.Value.Revisions.MechanicsRevision, expectedAttributeRevision: 1, NewCommandId(), TestCorrelationId);
            Assert.That(second.IsSuccess, Is.True);

            Result<CharacterRecord> reverted = _characterRepository.RevertAdvancementPurchase(_campaign, character.CharacterId, firstPurchase.PurchaseId, GmReason, NewUserId(), actorIsMainGm: true, second.Value.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);

            Assert.That(reverted.IsFailure, Is.True);
            Assert.That(reverted.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterAdvancementPurchaseHasDependent));

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.Attributes.Single(a => a.AttributeDefinitionId.Equals(Strength)).BaseValue, Is.EqualTo(5));
        }

        [Test]
        public void RevertAdvancementPurchase_WithoutReasonCode_IsRejected()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 20);
            Result<CharacterRecord> purchased = _characterRepository.PurchaseAttributeIncrease(_campaign, character.CharacterId, Strength, toValue: 3, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, expectedAttributeRevision: 0, NewCommandId(), TestCorrelationId);
            AdvancementPurchase purchase = _characterRepository.GetAdvancementPurchases(_campaign, character.CharacterId, TestCorrelationId).Value.Single();

            Result<CharacterRecord> reverted = _characterRepository.RevertAdvancementPurchase(_campaign, character.CharacterId, purchase.PurchaseId, "", NewUserId(), actorIsMainGm: true, purchased.Value.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);

            Assert.That(reverted.IsFailure, Is.True);
            Assert.That(reverted.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterAdvancementReasonRequired));
        }

        [Test]
        public void RevertAdvancementPurchase_DuplicateCommandId_DoesNotRevertTwice()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 20);
            Result<CharacterRecord> purchased = _characterRepository.PurchaseAttributeIncrease(_campaign, character.CharacterId, Strength, toValue: 3, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, expectedAttributeRevision: 0, NewCommandId(), TestCorrelationId);
            AdvancementPurchase purchase = _characterRepository.GetAdvancementPurchases(_campaign, character.CharacterId, TestCorrelationId).Value.Single();

            CommandId revertCommandId = NewCommandId();
            Result<CharacterRecord> first = _characterRepository.RevertAdvancementPurchase(_campaign, character.CharacterId, purchase.PurchaseId, GmReason, NewUserId(), actorIsMainGm: true, purchased.Value.Revisions.MechanicsRevision, revertCommandId, TestCorrelationId);
            Assert.That(first.IsSuccess, Is.True);

            Result<CharacterRecord> replay = _characterRepository.RevertAdvancementPurchase(_campaign, character.CharacterId, purchase.PurchaseId, GmReason, NewUserId(), actorIsMainGm: true, purchased.Value.Revisions.MechanicsRevision, revertCommandId, TestCorrelationId);
            Assert.That(replay.IsSuccess, Is.True);

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.DevelopmentPool.Available, Is.EqualTo(20), "a replayed duplicate CommandId must not refund a second time");
            Assert.That(reRead.Value.DevelopmentPool.Spent, Is.EqualTo(0));
        }

        // ---- Block Б: PreviewCharacterRespec ---------------------------------------

        [Test]
        public void PreviewCharacterRespec_CreatesNoEvents_NoStateChange()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 20);
            Result<CharacterRecord> purchased = _characterRepository.PurchaseAttributeIncrease(_campaign, character.CharacterId, Strength, toValue: 3, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, expectedAttributeRevision: 0, NewCommandId(), TestCorrelationId);
            long mechanicsRevisionBefore = purchased.Value.Revisions.MechanicsRevision;
            long availableBefore = purchased.Value.DevelopmentPool.Available;

            long eventCountBefore;
            using (SqliteConnection connection = OpenReadOnly())
            {
                connection.Open();
                eventCountBefore = ReadLong(connection, "SELECT COUNT(*) FROM DomainEvents");
            }

            var targets = new[] { new CharacterRespecTarget(AdvancementOperationKind.AttributeIncrease, Strength.ToString(), desiredValue: 5) };
            Result<CharacterRespecPreview> preview = _characterRepository.PreviewCharacterRespec(_campaign, character.CharacterId, targets, TestCorrelationId);

            Assert.That(preview.IsSuccess, Is.True);
            Assert.That(preview.Value.TotalReturned, Is.EqualTo(6)); // returning the ToValue=3 purchase (Cost=6)
            Assert.That(preview.Value.TotalSpent, Is.EqualTo(10)); // fresh 0->5 purchase, CostPerAttributePoint=2

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.Revisions.MechanicsRevision, Is.EqualTo(mechanicsRevisionBefore));
            Assert.That(reRead.Value.DevelopmentPool.Available, Is.EqualTo(availableBefore));

            using (SqliteConnection connection = OpenReadOnly())
            {
                connection.Open();
                long eventCountAfter = ReadLong(connection, "SELECT COUNT(*) FROM DomainEvents");
                Assert.That(eventCountAfter, Is.EqualTo(eventCountBefore));
            }
        }

        // ---- Block Б: ApplyCharacterRespec ------------------------------------------

        [Test]
        public void ApplyCharacterRespec_UndoesAndRepurchases_WithIndividuallyVisibleEventsAndOneCompletionEvent()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 50);
            Result<CharacterRecord> attrPurchase = _characterRepository.PurchaseAttributeIncrease(_campaign, character.CharacterId, Strength, toValue: 3, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, expectedAttributeRevision: 0, NewCommandId(), TestCorrelationId);
            Result<CharacterRecord> skillPurchase = _characterRepository.PurchaseSkillLevel(_campaign, character.CharacterId, Stealth, toLevel: 2, NewUserId(), actorIsMainGm: true, attrPurchase.Value.Revisions.MechanicsRevision, expectedSkillRevision: 0, NewCommandId(), TestCorrelationId);
            Assert.That(skillPurchase.IsSuccess, Is.True);

            var targets = new[]
            {
                new CharacterRespecTarget(AdvancementOperationKind.AttributeIncrease, Strength.ToString(), desiredValue: 5),
                new CharacterRespecTarget(AdvancementOperationKind.SkillLevelPurchase, Stealth.ToString(), desiredValue: 0),
            };

            CommandId applyCommandId = NewCommandId();
            Result<CharacterRecord> applied = _characterRepository.ApplyCharacterRespec(_campaign, character.CharacterId, targets, GmReason, NewUserId(), actorIsMainGm: true, skillPurchase.Value.Revisions.MechanicsRevision, applyCommandId, TestCorrelationId);

            Assert.That(applied.IsSuccess, Is.True);
            Assert.That(applied.Value.Attributes.Single(a => a.AttributeDefinitionId.Equals(Strength)).BaseValue, Is.EqualTo(5));
            Assert.That(applied.Value.Skills, Is.Empty);

            // Strength: return 6 (3*2), spend 10 (5*2) = net +... ; Stealth: return 6 (2*3), spend 0 (fully undone).
            Assert.That(applied.Value.DevelopmentPool.Spent, Is.EqualTo(10)); // only the new Strength purchase remains a real Spend-equivalent (RespecSpend), everything else returned.

            using SqliteConnection connection = OpenReadOnly();
            connection.Open();

            long compensationGroupEventCount = ReadLong(connection, "SELECT COUNT(*) FROM DomainEvents WHERE CompensationGroupId = $c0", applyCommandId.ToString());
            // 2 compensating "reverted" + 1 forward "attribute_increased" (Strength repurchase) + 1 completion event.
            Assert.That(compensationGroupEventCount, Is.EqualTo(4));

            long revertedEventCount = ReadLong(connection, "SELECT COUNT(*) FROM DomainEvents WHERE EventType = 'odyssey.persistence.character_advancement_purchase_reverted' AND CompensationGroupId = $c0", applyCommandId.ToString());
            Assert.That(revertedEventCount, Is.EqualTo(2), "each undone purchase must produce its own individually visible compensating event, not one collapsed event");

            long completedEventCount = ReadLong(connection, "SELECT COUNT(*) FROM DomainEvents WHERE EventType = 'odyssey.persistence.character_respec_completed'");
            Assert.That(completedEventCount, Is.EqualTo(1));

            string completedPayload = ReadString(connection, "SELECT PayloadJson FROM DomainEvents WHERE EventType = 'odyssey.persistence.character_respec_completed'");
            Assert.That(completedPayload, Does.Contain("\"producedEventSequences\""));
            Assert.That(completedPayload, Does.Contain(GmReason));

            IReadOnlyList<AdvancementPurchase> purchasesAfter = _characterRepository.GetAdvancementPurchases(_campaign, character.CharacterId, TestCorrelationId).Value;
            Assert.That(purchasesAfter.Count(p => p.Status == AdvancementPurchaseStatus.SupersededByRespec), Is.EqualTo(2));
            Assert.That(purchasesAfter.Count(p => p.Status == AdvancementPurchaseStatus.Applied), Is.EqualTo(1));
        }

        [Test]
        public void ApplyCharacterRespec_RecomputesServerSide_IgnoringAnyStalePreview()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 50);
            Result<CharacterRecord> attrPurchase = _characterRepository.PurchaseAttributeIncrease(_campaign, character.CharacterId, Strength, toValue: 3, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, expectedAttributeRevision: 0, NewCommandId(), TestCorrelationId);

            var targets = new[] { new CharacterRespecTarget(AdvancementOperationKind.AttributeIncrease, Strength.ToString(), desiredValue: 5) };

            // A preview taken before a further purchase -- ApplyCharacterRespec
            // takes no preview parameter at all, so it can never consult this
            // stale result; it must recompute fresh against the CURRENT state.
            Result<CharacterRespecPreview> stalePreview = _characterRepository.PreviewCharacterRespec(_campaign, character.CharacterId, targets, TestCorrelationId);
            Assert.That(stalePreview.IsSuccess, Is.True);
            Assert.That(stalePreview.Value.TotalReturned, Is.EqualTo(6));

            // State changes after the preview was taken.
            Result<CharacterRecord> secondPurchase = _characterRepository.PurchaseAttributeIncrease(_campaign, character.CharacterId, Strength, toValue: 4, NewUserId(), actorIsMainGm: true, attrPurchase.Value.Revisions.MechanicsRevision, expectedAttributeRevision: 1, NewCommandId(), TestCorrelationId);
            Assert.That(secondPurchase.IsSuccess, Is.True);

            Result<CharacterRecord> applied = _characterRepository.ApplyCharacterRespec(_campaign, character.CharacterId, targets, GmReason, NewUserId(), actorIsMainGm: true, secondPurchase.Value.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);

            Assert.That(applied.IsSuccess, Is.True);
            Assert.That(applied.Value.Attributes.Single(a => a.AttributeDefinitionId.Equals(Strength)).BaseValue, Is.EqualTo(5));
            // Both real purchases (cost 6 + cost 2) must be returned, not just the stale preview's single entry.
            Assert.That(applied.Value.DevelopmentPool.Spent, Is.EqualTo(10)); // fresh 0->5 purchase only.
        }

        [Test]
        public void ApplyCharacterRespec_DuplicateCommandId_DoesNotDuplicateBatch()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 50);
            Result<CharacterRecord> attrPurchase = _characterRepository.PurchaseAttributeIncrease(_campaign, character.CharacterId, Strength, toValue: 3, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, expectedAttributeRevision: 0, NewCommandId(), TestCorrelationId);

            var targets = new[] { new CharacterRespecTarget(AdvancementOperationKind.AttributeIncrease, Strength.ToString(), desiredValue: 5) };
            CommandId applyCommandId = NewCommandId();

            Result<CharacterRecord> first = _characterRepository.ApplyCharacterRespec(_campaign, character.CharacterId, targets, GmReason, NewUserId(), actorIsMainGm: true, attrPurchase.Value.Revisions.MechanicsRevision, applyCommandId, TestCorrelationId);
            Assert.That(first.IsSuccess, Is.True);

            Result<CharacterRecord> replay = _characterRepository.ApplyCharacterRespec(_campaign, character.CharacterId, targets, GmReason, NewUserId(), actorIsMainGm: true, attrPurchase.Value.Revisions.MechanicsRevision, applyCommandId, TestCorrelationId);
            Assert.That(replay.IsSuccess, Is.True);

            using SqliteConnection connection = OpenReadOnly();
            connection.Open();
            long completedEventCount = ReadLong(connection, "SELECT COUNT(*) FROM DomainEvents WHERE EventType = 'odyssey.persistence.character_respec_completed'");
            Assert.That(completedEventCount, Is.EqualTo(1), "a replayed duplicate CommandId must not re-apply the batch a second time");

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.Attributes.Single(a => a.AttributeDefinitionId.Equals(Strength)).BaseValue, Is.EqualTo(5));
        }

        // TC-CHAR-168 (ODY-S04-115a): GetCharacterHistory must succeed (no
        // IntegrityCheckFailed) after an ApplyCharacterRespec batch whose
        // plan both reverts an earlier purchase and re-purchases an
        // attribute -- this is the exact case where ApplyCharacterRespec's
        // own revertedPayload/forwardPayload previously omitted
        // displayNameSnapshot, including on the already-whitelisted
        // character_attribute_increased event type the forward branch
        // reuses.
        [Test]
        public void GetCharacterHistory_AfterRespecWithRevertAndAttributeRepurchase_Succeeds()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 50);
            Result<CharacterRecord> attrPurchase = _characterRepository.PurchaseAttributeIncrease(_campaign, character.CharacterId, Strength, toValue: 3, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, expectedAttributeRevision: 0, NewCommandId(), TestCorrelationId);
            Result<CharacterRecord> skillPurchase = _characterRepository.PurchaseSkillLevel(_campaign, character.CharacterId, Stealth, toLevel: 2, NewUserId(), actorIsMainGm: true, attrPurchase.Value.Revisions.MechanicsRevision, expectedSkillRevision: 0, NewCommandId(), TestCorrelationId);
            Assert.That(skillPurchase.IsSuccess, Is.True);

            var targets = new[]
            {
                new CharacterRespecTarget(AdvancementOperationKind.AttributeIncrease, Strength.ToString(), desiredValue: 5),
                new CharacterRespecTarget(AdvancementOperationKind.SkillLevelPurchase, Stealth.ToString(), desiredValue: 0),
            };
            Result<CharacterRecord> applied = _characterRepository.ApplyCharacterRespec(_campaign, character.CharacterId, targets, GmReason, NewUserId(), actorIsMainGm: true, skillPurchase.Value.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(applied.IsSuccess, Is.True);

            Result<IReadOnlyList<CharacterHistoryEntry>> history = _characterRepository.GetCharacterHistory(_campaign, character.CharacterId, TestCorrelationId);

            Assert.That(history.IsSuccess, Is.True, "GetCharacterHistory must not fail with IntegrityCheckFailed for the respec batch's own forward character_attribute_increased/character_skill_level_purchased events");
            Assert.That(history.Value.Select(e => e.EventType), Does.Contain("odyssey.persistence.character_attribute_increased"));
            Assert.That(history.Value.Select(e => e.EventType), Does.Contain("odyssey.persistence.character_respec_completed"));
            Assert.That(history.Value, Has.All.Property(nameof(CharacterHistoryEntry.DisplayNameSnapshot)).Not.Null);
        }
    }
}
