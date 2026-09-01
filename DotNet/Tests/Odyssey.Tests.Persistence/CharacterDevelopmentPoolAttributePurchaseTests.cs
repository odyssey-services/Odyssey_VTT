using System;
using System.IO;
using System.Linq;
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
    /// ODY-S04-105: real, non-stubbed tests for
    /// <see cref="SqliteCharacterRepository.GrantDevelopmentPoints"/> and
    /// <see cref="SqliteCharacterRepository.PurchaseAttributeIncrease"/>
    /// against a real temp-directory campaign and a real SQLite database --
    /// mirroring <c>SqliteCharacterRepositoryTests</c>'s exact fixture
    /// convention. None of these tests mock or bypass the repository/
    /// pipeline. The cost/cap values exercised here
    /// (<see cref="Odyssey.Rules.Character.AttributeCostRules"/>) are this
    /// task's own explicitly-flagged test fixture, not production Ruleset
    /// balance data.
    /// </summary>
    public sealed class CharacterDevelopmentPoolAttributePurchaseTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
        private static readonly AttributeDefinitionId Strength = AttributeDefinitionId.Parse("Strength");

        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;
        private SqliteCharacterRepository _characterRepository = null!;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s04-105-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_campaignDir, "Development Pool Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = _campaignRepository.Create(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _characterRepository = new SqliteCharacterRepository(Clock);
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
                if (Directory.Exists(_campaignDir)) Directory.Delete(_campaignDir, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup only.
            }
        }

        private CharacterRecord CreateCharacter(string name = "Development Character")
        {
            var request = new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, name);
            Result<CharacterRecord> created = _characterRepository.CreateCharacter(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        // TC-CHAR-038: GrantDevelopmentPoints by a non-MainGM actor is
        // rejected.
        [Test]
        public void GrantDevelopmentPoints_ByNonMainGm_IsRejected()
        {
            CharacterRecord character = CreateCharacter();

            Result<CharacterRecord> result = _characterRepository.GrantDevelopmentPoints(_campaign, character.CharacterId, 10, "Session reward", NewUserId(), actorIsMainGm: false, character.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterDevelopmentGrantDenied));
        }

        // TC-CHAR-039: GrantDevelopmentPoints by MainGM increases the
        // balance and is gated by MechanicsRevision.
        [Test]
        public void GrantDevelopmentPoints_ByMainGm_IncreasesBalance_GatedByMechanicsRevision()
        {
            CharacterRecord character = CreateCharacter();

            Result<CharacterRecord> result = _characterRepository.GrantDevelopmentPoints(_campaign, character.CharacterId, 10, "Session reward", NewUserId(), actorIsMainGm: true, character.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.DevelopmentPool.Earned, Is.EqualTo(10));
            Assert.That(result.Value.DevelopmentPool.Available, Is.EqualTo(10));
            Assert.That(result.Value.Revisions.MechanicsRevision, Is.EqualTo(character.Revisions.MechanicsRevision + 1));
        }

        // TC-CHAR-040: PurchaseAttributeIncrease with sufficient balance
        // succeeds; balance decreases by cost, BaseValue/EffectiveValue
        // update correctly.
        [Test]
        public void PurchaseAttributeIncrease_WithSufficientBalance_Succeeds()
        {
            CharacterRecord character = CreateCharacter();
            UserId owner = NewUserId();
            Result<CharacterRecord> granted = _characterRepository.GrantDevelopmentPoints(_campaign, character.CharacterId, 10, "Initial grant", NewUserId(), actorIsMainGm: true, character.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(granted.IsSuccess, Is.True);

            Result<CharacterRecord> purchased = _characterRepository.PurchaseAttributeIncrease(_campaign, character.CharacterId, Strength, toValue: 2, owner, actorIsMainGm: true, granted.Value.Revisions.MechanicsRevision, expectedAttributeRevision: 0, NewCommandId(), TestCorrelationId);

            Assert.That(purchased.IsSuccess, Is.True);
            // Fixture cost: 2 dev points per attribute point (AttributeCostRules.CostPerAttributePoint).
            Assert.That(purchased.Value.DevelopmentPool.Spent, Is.EqualTo(4));
            Assert.That(purchased.Value.DevelopmentPool.Available, Is.EqualTo(6));
            AttributeValue attribute = purchased.Value.Attributes.Single(a => a.AttributeDefinitionId.Equals(Strength));
            Assert.That(attribute.BaseValue, Is.EqualTo(2));
            Assert.That(attribute.EffectiveValue, Is.EqualTo(2));
            Assert.That(attribute.Revision, Is.EqualTo(1));
        }

        // TC-CHAR-041: PurchaseAttributeIncrease with insufficient balance is
        // rejected, with no state change.
        [Test]
        public void PurchaseAttributeIncrease_WithInsufficientBalance_IsRejected_NoStateChange()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> granted = _characterRepository.GrantDevelopmentPoints(_campaign, character.CharacterId, 1, "Small grant", NewUserId(), actorIsMainGm: true, character.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(granted.IsSuccess, Is.True);

            Result<CharacterRecord> purchased = _characterRepository.PurchaseAttributeIncrease(_campaign, character.CharacterId, Strength, toValue: 2, NewUserId(), actorIsMainGm: true, granted.Value.Revisions.MechanicsRevision, expectedAttributeRevision: 0, NewCommandId(), TestCorrelationId);

            Assert.That(purchased.IsFailure, Is.True);
            Assert.That(purchased.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterDevelopmentInsufficientBalance));

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.IsSuccess, Is.True);
            Assert.That(reRead.Value.DevelopmentPool.Spent, Is.EqualTo(0));
            Assert.That(reRead.Value.Attributes, Is.Empty);
        }

        // TC-CHAR-042: PurchaseAttributeIncrease above the attribute cap is
        // rejected.
        [Test]
        public void PurchaseAttributeIncrease_AboveCap_IsRejected()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> granted = _characterRepository.GrantDevelopmentPoints(_campaign, character.CharacterId, 1000, "Large grant", NewUserId(), actorIsMainGm: true, character.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(granted.IsSuccess, Is.True);

            // AttributeCostRules.NormalDevelopmentCap == 15 (test fixture).
            Result<CharacterRecord> purchased = _characterRepository.PurchaseAttributeIncrease(_campaign, character.CharacterId, Strength, toValue: 16, NewUserId(), actorIsMainGm: true, granted.Value.Revisions.MechanicsRevision, expectedAttributeRevision: 0, NewCommandId(), TestCorrelationId);

            Assert.That(purchased.IsFailure, Is.True);
            Assert.That(purchased.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterAttributeCapExceeded));
        }

        // TC-CHAR-043: a duplicate CommandId for PurchaseAttributeIncrease
        // does not spend the balance a second time -- direct assertion on
        // the real balance after replay, not just on rejection.
        [Test]
        public void PurchaseAttributeIncrease_DuplicateCommandId_DoesNotDoubleSpend()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> granted = _characterRepository.GrantDevelopmentPoints(_campaign, character.CharacterId, 10, "Grant", NewUserId(), actorIsMainGm: true, character.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(granted.IsSuccess, Is.True);
            CommandId commandId = NewCommandId();

            Result<CharacterRecord> first = _characterRepository.PurchaseAttributeIncrease(_campaign, character.CharacterId, Strength, toValue: 2, NewUserId(), actorIsMainGm: true, granted.Value.Revisions.MechanicsRevision, expectedAttributeRevision: 0, commandId, TestCorrelationId);
            Assert.That(first.IsSuccess, Is.True);
            Result<CharacterRecord> second = _characterRepository.PurchaseAttributeIncrease(_campaign, character.CharacterId, Strength, toValue: 2, NewUserId(), actorIsMainGm: true, granted.Value.Revisions.MechanicsRevision, expectedAttributeRevision: 0, commandId, TestCorrelationId);

            Assert.That(second.IsSuccess, Is.True);
            Assert.That(second.Value.DevelopmentPool.Spent, Is.EqualTo(first.Value.DevelopmentPool.Spent));

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.IsSuccess, Is.True);
            // Real balance directly checked -- exactly one spend of 4 (2 points * 2 cost), never two.
            Assert.That(reRead.Value.DevelopmentPool.Spent, Is.EqualTo(4));
            Assert.That(reRead.Value.DevelopmentPool.Available, Is.EqualTo(6));
        }

        // TC-CHAR-044: a concurrent edit to Mechanics and Identity commits
        // without a false conflict.
        [Test]
        public void MechanicsEdit_AndIdentityEdit_BothCommit_NoFalseConflict()
        {
            CharacterRecord character = CreateCharacter();

            Result<CharacterRecord> grantResult = _characterRepository.GrantDevelopmentPoints(_campaign, character.CharacterId, 5, "Grant", NewUserId(), actorIsMainGm: true, character.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Result<CharacterRecord> identityResult = _characterRepository.UpdateIdentity(_campaign, character.CharacterId, "Renamed", character.Revisions.IdentityRevision, NewCommandId(), TestCorrelationId);

            Assert.That(grantResult.IsSuccess, Is.True);
            Assert.That(identityResult.IsSuccess, Is.True);

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.IsSuccess, Is.True);
            Assert.That(reRead.Value.DisplayName, Is.EqualTo("Renamed"));
            Assert.That(reRead.Value.DevelopmentPool.Earned, Is.EqualTo(5));
        }

        // TC-CHAR-045: a stale expectedMechanicsRevision is rejected, with no
        // state change.
        [Test]
        public void GrantDevelopmentPoints_WithStaleExpectedMechanicsRevision_IsRejected_NoStateChange()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> firstGrant = _characterRepository.GrantDevelopmentPoints(_campaign, character.CharacterId, 5, "First grant", NewUserId(), actorIsMainGm: true, character.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(firstGrant.IsSuccess, Is.True);

            Result<CharacterRecord> staleGrant = _characterRepository.GrantDevelopmentPoints(_campaign, character.CharacterId, 5, "Stale grant", NewUserId(), actorIsMainGm: true, character.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);

            Assert.That(staleGrant.IsFailure, Is.True);
            Assert.That(staleGrant.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRevisionConflict));

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.IsSuccess, Is.True);
            Assert.That(reRead.Value.DevelopmentPool.Earned, Is.EqualTo(5));
        }

        // TC-CHAR-046: the DevelopmentTransaction/ledger correctly reflects
        // the purchase made -- amount, direction (Kind), and the addressed
        // attribute.
        [Test]
        public void DevelopmentLedger_ReflectsGrantAndPurchase_CorrectAmountKindAndAttribute()
        {
            CharacterRecord character = CreateCharacter();
            UserId actor = NewUserId();
            Result<CharacterRecord> granted = _characterRepository.GrantDevelopmentPoints(_campaign, character.CharacterId, 10, "Session reward", actor, actorIsMainGm: true, character.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(granted.IsSuccess, Is.True);
            Result<CharacterRecord> purchased = _characterRepository.PurchaseAttributeIncrease(_campaign, character.CharacterId, Strength, toValue: 3, actor, actorIsMainGm: true, granted.Value.Revisions.MechanicsRevision, expectedAttributeRevision: 0, NewCommandId(), TestCorrelationId);
            Assert.That(purchased.IsSuccess, Is.True);

            Result<System.Collections.Generic.IReadOnlyList<DevelopmentTransactionRecord>> ledger = _characterRepository.GetDevelopmentLedger(_campaign, character.CharacterId, TestCorrelationId);

            Assert.That(ledger.IsSuccess, Is.True);
            Assert.That(ledger.Value, Has.Count.EqualTo(2));
            DevelopmentTransactionRecord grantEntry = ledger.Value[0];
            DevelopmentTransactionRecord spendEntry = ledger.Value[1];
            Assert.That(grantEntry.Kind, Is.EqualTo(DevelopmentTransactionKind.Grant));
            Assert.That(grantEntry.Amount, Is.EqualTo(10));
            Assert.That(spendEntry.Kind, Is.EqualTo(DevelopmentTransactionKind.Spend));
            // 3 attribute points * 2 cost-per-point fixture = 6.
            Assert.That(spendEntry.Amount, Is.EqualTo(6));
            Assert.That(spendEntry.SourceRef, Is.EqualTo(Strength.ToString()));
        }

        // Permission check named in product section 13.1: an unrelated actor
        // (neither MainGM nor assigned to the Character) cannot purchase.
        [Test]
        public void PurchaseAttributeIncrease_ByUnrelatedActor_IsRejected()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> granted = _characterRepository.GrantDevelopmentPoints(_campaign, character.CharacterId, 10, "Grant", NewUserId(), actorIsMainGm: true, character.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(granted.IsSuccess, Is.True);

            Result<CharacterRecord> purchased = _characterRepository.PurchaseAttributeIncrease(_campaign, character.CharacterId, Strength, toValue: 2, NewUserId(), actorIsMainGm: false, granted.Value.Revisions.MechanicsRevision, expectedAttributeRevision: 0, NewCommandId(), TestCorrelationId);

            Assert.That(purchased.IsFailure, Is.True);
            Assert.That(purchased.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterDevelopmentPurchaseDenied));
        }

        // The assigned owner (not MainGM) can purchase for their own
        // Character -- product section 13.1's own permission rule, exercised
        // via ODY-S04-102's own IsAssignedCharacter/AssignPrimaryOwner.
        [Test]
        public void PurchaseAttributeIncrease_ByAssignedOwner_Succeeds()
        {
            CharacterRecord character = CreateCharacter();
            UserId owner = NewUserId();
            Result<CharacterRecord> ownerAssigned = _characterRepository.AssignPrimaryOwner(_campaign, character.CharacterId, owner, "Initial assignment", actorIsMainGm: true, character.Revisions.OwnershipRevision, NewCommandId(), TestCorrelationId);
            Assert.That(ownerAssigned.IsSuccess, Is.True);
            Result<CharacterRecord> granted = _characterRepository.GrantDevelopmentPoints(_campaign, character.CharacterId, 10, "Grant", NewUserId(), actorIsMainGm: true, ownerAssigned.Value.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(granted.IsSuccess, Is.True);

            Result<CharacterRecord> purchased = _characterRepository.PurchaseAttributeIncrease(_campaign, character.CharacterId, Strength, toValue: 2, owner, actorIsMainGm: false, granted.Value.Revisions.MechanicsRevision, expectedAttributeRevision: 0, NewCommandId(), TestCorrelationId);

            Assert.That(purchased.IsSuccess, Is.True);
        }

        // A stale expectedAttributeRevision (entry-level gate, independent of
        // MechanicsRevision) rejects a second purchase against the same
        // attribute.
        [Test]
        public void PurchaseAttributeIncrease_WithStaleAttributeRevision_IsRejected()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> granted = _characterRepository.GrantDevelopmentPoints(_campaign, character.CharacterId, 20, "Grant", NewUserId(), actorIsMainGm: true, character.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(granted.IsSuccess, Is.True);
            Result<CharacterRecord> firstPurchase = _characterRepository.PurchaseAttributeIncrease(_campaign, character.CharacterId, Strength, toValue: 2, NewUserId(), actorIsMainGm: true, granted.Value.Revisions.MechanicsRevision, expectedAttributeRevision: 0, NewCommandId(), TestCorrelationId);
            Assert.That(firstPurchase.IsSuccess, Is.True);

            // expectedAttributeRevision is now stale (0, but the attribute's
            // own revision advanced to 1 after the first purchase).
            Result<CharacterRecord> secondPurchase = _characterRepository.PurchaseAttributeIncrease(_campaign, character.CharacterId, Strength, toValue: 3, NewUserId(), actorIsMainGm: true, firstPurchase.Value.Revisions.MechanicsRevision, expectedAttributeRevision: 0, NewCommandId(), TestCorrelationId);

            Assert.That(secondPurchase.IsFailure, Is.True);
            Assert.That(secondPurchase.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRevisionConflict));
        }
    }
}
