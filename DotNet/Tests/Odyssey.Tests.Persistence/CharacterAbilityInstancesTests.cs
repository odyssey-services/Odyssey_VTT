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
    /// ODY-S04-108: real, non-stubbed tests against a real temp-directory
    /// campaign and a real SQLite database, mirroring
    /// <see cref="CharacterAdvancementRevertRespecTests"/>'s exact fixture
    /// convention. Covers <c>AcquireAbility</c> (all relevant
    /// <see cref="SourceKind"/> paths)/<c>RemoveAbility</c>/<c>RankMode</c>
    /// validation, and section 1.3's regression (a third
    /// <c>AdvancementOperationKind</c> value must not silently corrupt
    /// <c>RevertAdvancementPurchase</c>/<c>ApplyCharacterRespec</c>).
    /// </summary>
    public sealed class CharacterAbilityInstancesTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
        private static readonly AbilityDefinitionId Fireball = AbilityDefinitionId.Parse("Fireball");
        private const string FixtureConfiguration = "{}";

        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;
        private SqliteCharacterRepository _characterRepository = null!;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s04-108-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_campaignDir, "Ability Instances Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
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

        private CharacterRecord CreateCharacter(string name = "Ability Character")
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

        private static long AbilityCost => Odyssey.Rules.Character.AbilityCostRules.CostForAcquisition();

        // ---- AcquireAbility(ProgressionPurchase) -----------------------------------

        [Test]
        public void AcquireAbility_ProgressionPurchase_WithSufficientBalance_Succeeds_NoStateChangeOtherwise()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, AbilityCost);

            Result<CharacterRecord> acquired = _characterRepository.AcquireAbility(_campaign, character.CharacterId, Fireball, SourceKind.ProgressionPurchase, null, RankMode.None, null, null, FixtureConfiguration, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, granted.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);

            Assert.That(acquired.IsSuccess, Is.True);
            Assert.That(acquired.Value.Abilities, Has.Count.EqualTo(1));
            Assert.That(acquired.Value.Abilities[0].AbilityDefinitionId, Is.EqualTo(Fireball));
            Assert.That(acquired.Value.Abilities[0].SourceKind, Is.EqualTo(SourceKind.ProgressionPurchase));
            Assert.That(acquired.Value.DevelopmentPool.Spent, Is.EqualTo(AbilityCost));
            Assert.That(acquired.Value.DevelopmentPool.Available, Is.EqualTo(0));
        }

        [Test]
        public void AcquireAbility_ProgressionPurchase_WithInsufficientBalance_IsRejected_NoStateChange()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, AbilityCost - 1);

            Result<CharacterRecord> acquired = _characterRepository.AcquireAbility(_campaign, character.CharacterId, Fireball, SourceKind.ProgressionPurchase, null, RankMode.None, null, null, FixtureConfiguration, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, granted.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);

            Assert.That(acquired.IsFailure, Is.True);
            Assert.That(acquired.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterDevelopmentInsufficientBalance));

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.Abilities, Is.Empty);
            Assert.That(reRead.Value.DevelopmentPool.Spent, Is.EqualTo(0));
            Assert.That(reRead.Value.Revisions.CharacterAbilitiesRevision, Is.EqualTo(granted.Revisions.CharacterAbilitiesRevision));
        }

        [Test]
        public void AcquireAbility_ProgressionPurchase_CreatesAdvancementPurchase_AbilityAcquisition()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, AbilityCost);

            Result<CharacterRecord> acquired = _characterRepository.AcquireAbility(_campaign, character.CharacterId, Fireball, SourceKind.ProgressionPurchase, null, RankMode.None, null, null, FixtureConfiguration, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, granted.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);
            Assert.That(acquired.IsSuccess, Is.True);

            AdvancementPurchase purchase = _characterRepository.GetAdvancementPurchases(_campaign, character.CharacterId, TestCorrelationId).Value.Single();
            Assert.That(purchase.OperationKind, Is.EqualTo(AdvancementOperationKind.AbilityAcquisition));
            Assert.That(purchase.TargetDefinitionId, Is.EqualTo(Fireball.ToString()));
            Assert.That(purchase.FromValue, Is.EqualTo(0));
            Assert.That(purchase.ToValue, Is.EqualTo(1));
            Assert.That(purchase.Cost, Is.EqualTo(AbilityCost));
            Assert.That(purchase.Status, Is.EqualTo(AdvancementPurchaseStatus.Applied));
        }

        [Test]
        public void AcquireAbility_ProgressionPurchase_DuplicateCommandId_DoesNotDoubleSpend()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, AbilityCost * 2);

            CommandId acquireCommandId = NewCommandId();
            Result<CharacterRecord> first = _characterRepository.AcquireAbility(_campaign, character.CharacterId, Fireball, SourceKind.ProgressionPurchase, null, RankMode.None, null, null, FixtureConfiguration, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, granted.Revisions.CharacterAbilitiesRevision, acquireCommandId, TestCorrelationId);
            Assert.That(first.IsSuccess, Is.True);

            Result<CharacterRecord> replay = _characterRepository.AcquireAbility(_campaign, character.CharacterId, Fireball, SourceKind.ProgressionPurchase, null, RankMode.None, null, null, FixtureConfiguration, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, granted.Revisions.CharacterAbilitiesRevision, acquireCommandId, TestCorrelationId);
            Assert.That(replay.IsSuccess, Is.True);

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.Abilities, Has.Count.EqualTo(1), "a replayed duplicate CommandId must not acquire a second ability");
            Assert.That(reRead.Value.DevelopmentPool.Spent, Is.EqualTo(AbilityCost));
        }

        // ---- AcquireAbility(GMGrant) ------------------------------------------------

        [Test]
        public void AcquireAbility_GMGrant_ByNonMainGm_IsRejected()
        {
            CharacterRecord character = CreateCharacter();

            Result<CharacterRecord> acquired = _characterRepository.AcquireAbility(_campaign, character.CharacterId, Fireball, SourceKind.GMGrant, null, RankMode.None, null, null, FixtureConfiguration, NewUserId(), actorIsMainGm: false, null, character.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);

            Assert.That(acquired.IsFailure, Is.True);
            Assert.That(acquired.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterAbilityGrantDenied));
        }

        [Test]
        public void AcquireAbility_GMGrant_ByMainGm_Succeeds_NoPoolChange_NoAdvancementPurchase()
        {
            CharacterRecord character = CreateCharacter();

            Result<CharacterRecord> acquired = _characterRepository.AcquireAbility(_campaign, character.CharacterId, Fireball, SourceKind.GMGrant, null, RankMode.None, null, null, FixtureConfiguration, NewUserId(), actorIsMainGm: true, null, character.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);

            Assert.That(acquired.IsSuccess, Is.True);
            Assert.That(acquired.Value.Abilities, Has.Count.EqualTo(1));
            Assert.That(acquired.Value.Abilities[0].SourceKind, Is.EqualTo(SourceKind.GMGrant));
            Assert.That(acquired.Value.DevelopmentPool.Spent, Is.EqualTo(0));
            Assert.That(acquired.Value.DevelopmentPool.Available, Is.EqualTo(0));

            Assert.That(_characterRepository.GetAdvancementPurchases(_campaign, character.CharacterId, TestCorrelationId).Value, Is.Empty, "GMGrant must not create an AdvancementPurchase");
        }

        // ---- CharacterAbilitiesRevision actually increments (section 1.1 regression) -----

        [Test]
        public void AcquireAbility_And_RemoveAbility_ActuallyIncrementCharacterAbilitiesRevision()
        {
            CharacterRecord character = CreateCharacter();
            long revisionBefore = character.Revisions.CharacterAbilitiesRevision;

            Result<CharacterRecord> acquired = _characterRepository.AcquireAbility(_campaign, character.CharacterId, Fireball, SourceKind.GMGrant, null, RankMode.None, null, null, FixtureConfiguration, NewUserId(), actorIsMainGm: true, null, revisionBefore, NewCommandId(), TestCorrelationId);
            Assert.That(acquired.IsSuccess, Is.True);
            Assert.That(acquired.Value.Revisions.CharacterAbilitiesRevision, Is.EqualTo(revisionBefore + 1));

            CharacterAbilityId abilityId = acquired.Value.Abilities[0].CharacterAbilityId;

            // Item is directly constructible/removable -- exercised via a
            // second GMGrant-sourced item-kind acquisition would not be
            // legal to remove; acquire one with SourceKind=Item instead to
            // exercise the full increment + remove path.
            Result<CharacterRecord> acquiredItem = _characterRepository.AcquireAbility(_campaign, character.CharacterId, Fireball, SourceKind.Item, "item_0001", RankMode.None, null, null, FixtureConfiguration, NewUserId(), actorIsMainGm: true, null, acquired.Value.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);
            Assert.That(acquiredItem.IsSuccess, Is.True);
            Assert.That(acquiredItem.Value.Revisions.CharacterAbilitiesRevision, Is.EqualTo(revisionBefore + 2));

            CharacterAbilityId itemAbilityId = acquiredItem.Value.Abilities.Single(a => a.SourceKind == SourceKind.Item).CharacterAbilityId;

            Result<CharacterRecord> removed = _characterRepository.RemoveAbility(_campaign, character.CharacterId, itemAbilityId, NewUserId(), actorIsMainGm: true, acquiredItem.Value.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);
            Assert.That(removed.IsSuccess, Is.True);
            Assert.That(removed.Value.Revisions.CharacterAbilitiesRevision, Is.EqualTo(revisionBefore + 3));
            Assert.That(removed.Value.Abilities.Select(a => a.CharacterAbilityId), Does.Not.Contain(itemAbilityId));
            Assert.That(removed.Value.Abilities.Select(a => a.CharacterAbilityId), Does.Contain(abilityId), "the unrelated GMGrant ability must be untouched");
        }

        // ---- RankMode validation ----------------------------------------------------

        [Test]
        public void CharacterAbility_RankMode_Numeric_WithoutNumericRank_IsRejected()
        {
            Action action = () => new CharacterAbility(CharacterAbilityId.NewId(Clock.GetUtcNow()), Fireball, SourceKind.GMGrant, null, Clock.GetUtcNow(), RankMode.Numeric, null, null, true, FixtureConfiguration, null, 1);
            Assert.Throws<ArgumentException>(action);
        }

        [Test]
        public void CharacterAbility_RankMode_Named_WithoutNamedRankKey_IsRejected()
        {
            Action action = () => new CharacterAbility(CharacterAbilityId.NewId(Clock.GetUtcNow()), Fireball, SourceKind.GMGrant, null, Clock.GetUtcNow(), RankMode.Named, null, null, true, FixtureConfiguration, null, 1);
            Assert.Throws<ArgumentException>(action);
        }

        [Test]
        public void CharacterAbility_RankMode_None_WithNumericRankSet_IsRejected()
        {
            Action action = () => new CharacterAbility(CharacterAbilityId.NewId(Clock.GetUtcNow()), Fireball, SourceKind.GMGrant, null, Clock.GetUtcNow(), RankMode.None, 3, null, true, FixtureConfiguration, null, 1);
            Assert.Throws<ArgumentException>(action);
        }

        [Test]
        public void CharacterAbility_RankMode_None_WithNamedRankKeySet_IsRejected()
        {
            Action action = () => new CharacterAbility(CharacterAbilityId.NewId(Clock.GetUtcNow()), Fireball, SourceKind.GMGrant, null, Clock.GetUtcNow(), RankMode.None, null, "Adept", true, FixtureConfiguration, null, 1);
            Assert.Throws<ArgumentException>(action);
        }

        [Test]
        public void CharacterAbility_RankMode_Numeric_WithBothFieldsCorrect_Succeeds()
        {
            var ability = new CharacterAbility(CharacterAbilityId.NewId(Clock.GetUtcNow()), Fireball, SourceKind.GMGrant, null, Clock.GetUtcNow(), RankMode.Numeric, 3, null, true, FixtureConfiguration, null, 1);
            Assert.That(ability.NumericRank, Is.EqualTo(3));
        }

        [Test]
        public void CharacterAbility_RankMode_Named_WithBothFieldsCorrect_Succeeds()
        {
            var ability = new CharacterAbility(CharacterAbilityId.NewId(Clock.GetUtcNow()), Fireball, SourceKind.GMGrant, null, Clock.GetUtcNow(), RankMode.Named, null, "Adept", true, FixtureConfiguration, null, 1);
            Assert.That(ability.NamedRankKey, Is.EqualTo("Adept"));
        }

        // ---- RemoveAbility legality by SourceKind ------------------------------------

        [Test]
        public void RemoveAbility_OnUnknownCharacterAbilityId_IsRejected()
        {
            CharacterRecord character = CreateCharacter();
            CharacterAbilityId unknownId = CharacterAbilityId.NewId(Clock.GetUtcNow());

            Result<CharacterRecord> removed = _characterRepository.RemoveAbility(_campaign, character.CharacterId, unknownId, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);

            Assert.That(removed.IsFailure, Is.True);
            Assert.That(removed.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterAbilityNotFound));
        }

        [Test]
        public void RemoveAbility_OnItemSource_Succeeds()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> acquired = _characterRepository.AcquireAbility(_campaign, character.CharacterId, Fireball, SourceKind.Item, "item_0001", RankMode.None, null, null, FixtureConfiguration, NewUserId(), actorIsMainGm: true, null, character.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);
            CharacterAbilityId abilityId = acquired.Value.Abilities[0].CharacterAbilityId;

            Result<CharacterRecord> removed = _characterRepository.RemoveAbility(_campaign, character.CharacterId, abilityId, NewUserId(), actorIsMainGm: true, acquired.Value.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);

            Assert.That(removed.IsSuccess, Is.True);
            Assert.That(removed.Value.Abilities, Is.Empty);
        }

        [Test]
        public void RemoveAbility_OnActiveEffectSource_Succeeds()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> acquired = _characterRepository.AcquireAbility(_campaign, character.CharacterId, Fireball, SourceKind.ActiveEffect, "effect_0001", RankMode.None, null, null, FixtureConfiguration, NewUserId(), actorIsMainGm: true, null, character.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);
            CharacterAbilityId abilityId = acquired.Value.Abilities[0].CharacterAbilityId;

            Result<CharacterRecord> removed = _characterRepository.RemoveAbility(_campaign, character.CharacterId, abilityId, NewUserId(), actorIsMainGm: true, acquired.Value.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);

            Assert.That(removed.IsSuccess, Is.True);
            Assert.That(removed.Value.Abilities, Is.Empty);
        }

        [TestCase(SourceKind.ProgressionPurchase)]
        [TestCase(SourceKind.GMGrant)]
        [TestCase(SourceKind.CharacterTemplate)]
        [TestCase(SourceKind.RulesetAdvancement)]
        public void RemoveAbility_OnNonRemovableSource_IsRejected_NoStateChange(SourceKind sourceKind)
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord funded = sourceKind == SourceKind.ProgressionPurchase ? GrantPoints(character, AbilityCost) : character;

            Result<CharacterRecord> acquired = sourceKind == SourceKind.ProgressionPurchase
                ? _characterRepository.AcquireAbility(_campaign, character.CharacterId, Fireball, sourceKind, null, RankMode.None, null, null, FixtureConfiguration, NewUserId(), actorIsMainGm: true, funded.Revisions.MechanicsRevision, funded.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId)
                : _characterRepository.AcquireAbility(_campaign, character.CharacterId, Fireball, sourceKind, null, RankMode.None, null, null, FixtureConfiguration, NewUserId(), actorIsMainGm: true, null, funded.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);
            Assert.That(acquired.IsSuccess, Is.True);
            CharacterAbilityId abilityId = acquired.Value.Abilities[0].CharacterAbilityId;

            Result<CharacterRecord> removed = _characterRepository.RemoveAbility(_campaign, character.CharacterId, abilityId, NewUserId(), actorIsMainGm: true, acquired.Value.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);

            Assert.That(removed.IsFailure, Is.True);
            Assert.That(removed.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterAbilityRemovalNotAllowed));

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.Abilities, Has.Count.EqualTo(1));
        }

        [Test]
        public void RemoveAbility_DuplicateCommandId_DoesNotRemoveTwice()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> acquired = _characterRepository.AcquireAbility(_campaign, character.CharacterId, Fireball, SourceKind.Item, "item_0001", RankMode.None, null, null, FixtureConfiguration, NewUserId(), actorIsMainGm: true, null, character.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);
            CharacterAbilityId abilityId = acquired.Value.Abilities[0].CharacterAbilityId;

            CommandId removeCommandId = NewCommandId();
            Result<CharacterRecord> first = _characterRepository.RemoveAbility(_campaign, character.CharacterId, abilityId, NewUserId(), actorIsMainGm: true, acquired.Value.Revisions.CharacterAbilitiesRevision, removeCommandId, TestCorrelationId);
            Assert.That(first.IsSuccess, Is.True);

            Result<CharacterRecord> replay = _characterRepository.RemoveAbility(_campaign, character.CharacterId, abilityId, NewUserId(), actorIsMainGm: true, acquired.Value.Revisions.CharacterAbilitiesRevision, removeCommandId, TestCorrelationId);
            Assert.That(replay.IsSuccess, Is.True);

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.Abilities, Is.Empty);
        }

        // ---- Section 1.3 regression: AbilityAcquisition must not corrupt Revert/Respec ----

        [Test]
        public void RevertAdvancementPurchase_OnAbilityAcquisitionPurchase_IsRejectedExplicitly_NotAsDependent()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, AbilityCost);
            Result<CharacterRecord> acquired = _characterRepository.AcquireAbility(_campaign, character.CharacterId, Fireball, SourceKind.ProgressionPurchase, null, RankMode.None, null, null, FixtureConfiguration, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, granted.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);
            Assert.That(acquired.IsSuccess, Is.True);

            AdvancementPurchase purchase = _characterRepository.GetAdvancementPurchases(_campaign, character.CharacterId, TestCorrelationId).Value.Single();
            Assert.That(purchase.OperationKind, Is.EqualTo(AdvancementOperationKind.AbilityAcquisition));

            Result<CharacterRecord> reverted = _characterRepository.RevertAdvancementPurchase(_campaign, character.CharacterId, purchase.PurchaseId, "test", NewUserId(), actorIsMainGm: true, acquired.Value.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);

            Assert.That(reverted.IsFailure, Is.True);
            Assert.That(reverted.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterAdvancementOperationKindNotSupported));
            Assert.That(reverted.Error.Code, Is.Not.EqualTo(ErrorCodes.PersistenceCharacterAdvancementPurchaseHasDependent), "must not mis-parse TargetDefinitionId as a SkillDefinitionId and return the wrong, misleading error");

            // No state change: the ability is still owned, the pool is untouched.
            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.Abilities, Has.Count.EqualTo(1));
            Assert.That(reRead.Value.DevelopmentPool.Spent, Is.EqualTo(AbilityCost));
        }

        [Test]
        public void ApplyCharacterRespec_TargetingAbilityAcquisitionOperationKind_IsRejectedExplicitly()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, AbilityCost);
            Result<CharacterRecord> acquired = _characterRepository.AcquireAbility(_campaign, character.CharacterId, Fireball, SourceKind.ProgressionPurchase, null, RankMode.None, null, null, FixtureConfiguration, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, granted.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);
            Assert.That(acquired.IsSuccess, Is.True);

            var targets = new[] { new CharacterRespecTarget(AdvancementOperationKind.AbilityAcquisition, Fireball.ToString(), desiredValue: 0) };

            Result<CharacterRespecPreview> preview = _characterRepository.PreviewCharacterRespec(_campaign, character.CharacterId, targets, TestCorrelationId);
            Assert.That(preview.IsFailure, Is.True);
            Assert.That(preview.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterAdvancementOperationKindNotSupported));

            Result<CharacterRecord> applied = _characterRepository.ApplyCharacterRespec(_campaign, character.CharacterId, targets, "test", NewUserId(), actorIsMainGm: true, acquired.Value.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(applied.IsFailure, Is.True);
            Assert.That(applied.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterAdvancementOperationKindNotSupported));

            // No state change from the rejected respec attempt.
            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.Abilities, Has.Count.EqualTo(1));
            Assert.That(reRead.Value.DevelopmentPool.Spent, Is.EqualTo(AbilityCost));
        }

        // ---- Concurrent section edit (CharacterAbilities + Mechanics) does not false-conflict ----

        [Test]
        public void ConcurrentCharacterAbilitiesEdit_And_MechanicsEdit_CommitWithoutFalseConflict()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord granted = GrantPoints(character, 100);

            // Both commands declare only the sections they need -- an
            // ability grant (CharacterAbilities only) must not conflict
            // with a concurrent attribute purchase (Mechanics only), even
            // though both start from the same pre-edit CharacterRecord.
            Result<CharacterRecord> abilityResult = _characterRepository.AcquireAbility(_campaign, character.CharacterId, Fireball, SourceKind.GMGrant, null, RankMode.None, null, null, FixtureConfiguration, NewUserId(), actorIsMainGm: true, null, granted.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);
            Result<CharacterRecord> attributeResult = _characterRepository.PurchaseAttributeIncrease(_campaign, character.CharacterId, AttributeDefinitionId.Parse("Strength"), toValue: 1, NewUserId(), actorIsMainGm: true, granted.Revisions.MechanicsRevision, expectedAttributeRevision: 0, NewCommandId(), TestCorrelationId);

            Assert.That(abilityResult.IsSuccess, Is.True);
            Assert.That(attributeResult.IsSuccess, Is.True);

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.Abilities, Has.Count.EqualTo(1));
            Assert.That(reRead.Value.Attributes, Has.Count.EqualTo(1));
        }

        // TC-CHAR-169 (ODY-S04-115a): GetCharacterHistory must succeed (no
        // IntegrityCheckFailed) and surface character_ability_acquired/
        // character_ability_removed, once these ODY-S04-108 event types are
        // added to SqliteCharacterRepository.HistoryEventTypes.
        [Test]
        public void GetCharacterHistory_AfterAbilityAcquiredAndRemoved_Succeeds_SurfacesBothEventTypes()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> acquiredItem = _characterRepository.AcquireAbility(_campaign, character.CharacterId, Fireball, SourceKind.Item, "item_0001", RankMode.None, null, null, FixtureConfiguration, NewUserId(), actorIsMainGm: true, null, character.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);
            Assert.That(acquiredItem.IsSuccess, Is.True);
            CharacterAbilityId itemAbilityId = acquiredItem.Value.Abilities.Single().CharacterAbilityId;
            Result<CharacterRecord> removed = _characterRepository.RemoveAbility(_campaign, character.CharacterId, itemAbilityId, NewUserId(), actorIsMainGm: true, acquiredItem.Value.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);
            Assert.That(removed.IsSuccess, Is.True);

            Result<IReadOnlyList<CharacterHistoryEntry>> history = _characterRepository.GetCharacterHistory(_campaign, character.CharacterId, TestCorrelationId);

            Assert.That(history.IsSuccess, Is.True, "GetCharacterHistory must not fail with IntegrityCheckFailed for either ability event type");
            Assert.That(history.Value.Select(e => e.EventType), Does.Contain("odyssey.persistence.character_ability_acquired"));
            Assert.That(history.Value.Select(e => e.EventType), Does.Contain("odyssey.persistence.character_ability_removed"));
            Assert.That(history.Value, Has.All.Property(nameof(CharacterHistoryEntry.DisplayNameSnapshot)).Not.Null);
        }
    }
}
