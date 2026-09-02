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
    /// ODY-S04-112: real, non-stubbed tests against real temp-directory
    /// campaigns and a real SQLite database, mirroring
    /// <see cref="CharacterDeadRestoredTests"/>'s exact fixture convention.
    /// Covers <c>ExportCharacter</c> (ADR-026 section 4/5's bundle structure,
    /// identity redaction, role-invariant output) and <c>ImportCharacter</c>
    /// (ADR-025 section 7.6's fresh-Draft/RulesetVersion-pinning, plus this
    /// task's own mechanics/anatomy/resource round-trip preservation).
    /// </summary>
    public sealed class CharacterExportImportTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
        private static readonly ResourceDefinitionId Health = ResourceDefinitionId.Parse("Health");
        private static readonly AnatomyProfileDefinitionId Humanoid = AnatomyProfileDefinitionId.Parse("Humanoid");

        private string _sourceCampaignDir = null!;
        private string _targetCampaignDir = null!;
        private string _bundleDir = null!;
        private CampaignHandle _sourceCampaign = null!;
        private CampaignHandle _targetCampaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;
        private SqliteCharacterRepository _characterRepository = null!;

        [SetUp]
        public void SetUp()
        {
            _sourceCampaignDir = Path.Combine(Path.GetTempPath(), "ody-s04-112-src-" + Guid.NewGuid().ToString("N"));
            _targetCampaignDir = Path.Combine(Path.GetTempPath(), "ody-s04-112-dst-" + Guid.NewGuid().ToString("N"));
            _bundleDir = Path.Combine(Path.GetTempPath(), "ody-s04-112-bundle-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);

            Result<CampaignHandle> source = _campaignRepository.Create(new CreateCampaignRequest(_sourceCampaignDir, "Export Source Campaign", "ruleset.core", "1.0.0", "0.1.0"), NewCommandId(), TestCorrelationId);
            Assert.That(source.IsSuccess, Is.True);
            _sourceCampaign = source.Value;

            Result<CampaignHandle> target = _campaignRepository.Create(new CreateCampaignRequest(_targetCampaignDir, "Import Target Campaign", "ruleset.core", "1.0.0", "0.1.0"), NewCommandId(), TestCorrelationId);
            Assert.That(target.IsSuccess, Is.True);
            _targetCampaign = target.Value;

            _characterRepository = new SqliteCharacterRepository(Clock);
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaignRepository.Close(_sourceCampaign, TestCorrelationId); } catch (IOException) { }
            try { _campaignRepository.Close(_targetCampaign, TestCorrelationId); } catch (IOException) { }
            try { if (Directory.Exists(_sourceCampaignDir)) Directory.Delete(_sourceCampaignDir, recursive: true); } catch (IOException) { }
            try { if (Directory.Exists(_targetCampaignDir)) Directory.Delete(_targetCampaignDir, recursive: true); } catch (IOException) { }
            try { if (Directory.Exists(_bundleDir)) Directory.Delete(_bundleDir, recursive: true); } catch (IOException) { }
        }

        private CharacterRecord CreateActiveCharacter(CampaignHandle campaign, string name = "Export Import Character")
        {
            // BindDraftToCampaign (ODY-S04-103's real ADR-023-compliant
            // creation path), not the bare CreateCharacter skeleton -- only
            // this path actually sets AnatomyProfileRef, a required field on
            // any Character meant to be exported.
            var bindRequest = new BindDraftToCampaignRequest(campaign, CharacterKind.PlayerCharacter, name, "Humanoid", NewUserId(), CharacterCreationSeed.None(), null, null);
            Result<CharacterRecord> bound = _characterRepository.BindDraftToCampaign(bindRequest, NewCommandId(), TestCorrelationId);
            Assert.That(bound.IsSuccess, Is.True);
            Result<CharacterRecord> approved = _characterRepository.ApproveCharacterDraft(campaign, bound.Value.CharacterId, actorIsMainGm: true, bound.Value.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);
            Assert.That(approved.IsSuccess, Is.True);
            return approved.Value;
        }

        // ---- ExportCharacter -----------------------------------------------

        [Test]
        public void ExportCharacter_WritesManifestAndCharacterJson_NoIdentityFields()
        {
            CharacterRecord character = CreateActiveCharacter(_sourceCampaign);
            var actorContext = new ExportActorContext(NewUserId(), actorIsMainGm: true);

            Result<CharacterExportBundle> exported = _characterRepository.ExportCharacter(_sourceCampaign, character.CharacterId, _bundleDir, actorContext, TestCorrelationId);

            Assert.That(exported.IsSuccess, Is.True);
            Assert.That(File.Exists(Path.Combine(_bundleDir, "manifest.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(_bundleDir, "character.json")), Is.True);

            string manifestText = File.ReadAllText(Path.Combine(_bundleDir, "manifest.json"));
            string characterText = File.ReadAllText(Path.Combine(_bundleDir, "character.json"));
            Assert.That(manifestText, Does.Contain("\"formatVersion\""));
            Assert.That(manifestText, Does.Contain("1.0"));

            // ADR-026 section 4/section 8 rule 2: never CharacterOwnership/CharacterId/CampaignId.
            Assert.That(characterText, Does.Not.Contain("characterId"));
            Assert.That(characterText, Does.Not.Contain("campaignId"));
            Assert.That(characterText, Does.Not.Contain("ownership"));
            Assert.That(characterText, Does.Not.Contain(character.CharacterId.ToString()));
            Assert.That(characterText, Does.Not.Contain(_sourceCampaign.CampaignId.ToString()));
        }

        [Test]
        public void ExportCharacter_ByMainGmAndByOwner_ProducesIdenticalCharacterJson()
        {
            CharacterRecord character = CreateActiveCharacter(_sourceCampaign);
            UserId owner = NewUserId();
            Result<CharacterRecord> assigned = _characterRepository.AssignPrimaryOwner(_sourceCampaign, character.CharacterId, owner, "assign for export test", actorIsMainGm: true, character.Revisions.OwnershipRevision, NewCommandId(), TestCorrelationId);
            Assert.That(assigned.IsSuccess, Is.True);

            string mainGmDir = _bundleDir + "-maingm";
            string ownerDir = _bundleDir + "-owner";
            try
            {
                Result<CharacterExportBundle> byMainGm = _characterRepository.ExportCharacter(_sourceCampaign, character.CharacterId, mainGmDir, new ExportActorContext(NewUserId(), actorIsMainGm: true), TestCorrelationId);
                Result<CharacterExportBundle> byOwner = _characterRepository.ExportCharacter(_sourceCampaign, character.CharacterId, ownerDir, new ExportActorContext(owner, actorIsMainGm: false), TestCorrelationId);

                Assert.That(byMainGm.IsSuccess, Is.True);
                Assert.That(byOwner.IsSuccess, Is.True);

                string mainGmJson = File.ReadAllText(Path.Combine(mainGmDir, "character.json"));
                string ownerJson = File.ReadAllText(Path.Combine(ownerDir, "character.json"));
                Assert.That(mainGmJson, Is.EqualTo(ownerJson), "MainGM and the Character's own owner must produce byte-identical character.json today (ADR-026 section 5)");
            }
            finally
            {
                if (Directory.Exists(mainGmDir)) Directory.Delete(mainGmDir, recursive: true);
                if (Directory.Exists(ownerDir)) Directory.Delete(ownerDir, recursive: true);
            }
        }

        [Test]
        public void ExportCharacter_OnUnknownCharacterId_IsRejected()
        {
            CharacterId unknownId = CharacterId.NewId(Clock.GetUtcNow());
            Result<CharacterExportBundle> exported = _characterRepository.ExportCharacter(_sourceCampaign, unknownId, _bundleDir, new ExportActorContext(NewUserId(), actorIsMainGm: true), TestCorrelationId);

            Assert.That(exported.IsFailure, Is.True);
            Assert.That(exported.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterNotFound));
        }

        // ---- ImportCharacter -------------------------------------------------

        [Test]
        public void ImportCharacter_CreatesFreshCharacterId_DraftRequiringApproval_RulesetPinnedToTarget()
        {
            CharacterRecord character = CreateActiveCharacter(_sourceCampaign);
            Result<CharacterExportBundle> exported = _characterRepository.ExportCharacter(_sourceCampaign, character.CharacterId, _bundleDir, new ExportActorContext(NewUserId(), actorIsMainGm: true), TestCorrelationId);
            Assert.That(exported.IsSuccess, Is.True);

            UserId newOwner = NewUserId();
            var importRequest = new ImportCharacterRequest(_targetCampaign, _bundleDir, newOwner);
            Result<CharacterRecord> imported = _characterRepository.ImportCharacter(importRequest, NewCommandId(), NewCommandId(), TestCorrelationId);

            Assert.That(imported.IsSuccess, Is.True);
            Assert.That(imported.Value.CharacterId, Is.Not.EqualTo(character.CharacterId));
            Assert.That(imported.Value.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Draft));
            Assert.That(imported.Value.ApprovalState, Is.EqualTo(CharacterApprovalState.Draft));
            Assert.That(imported.Value.RulesetVersion, Is.EqualTo(_targetCampaign.Manifest.RulesetVersion));
        }

        [Test]
        public void ImportCharacter_WithIncompatibleRuleset_IsRejected_SameAsBindDraftToCampaign()
        {
            CharacterRecord character = CreateActiveCharacter(_sourceCampaign);
            Result<CharacterExportBundle> exported = _characterRepository.ExportCharacter(_sourceCampaign, character.CharacterId, _bundleDir, new ExportActorContext(NewUserId(), actorIsMainGm: true), TestCorrelationId);
            Assert.That(exported.IsSuccess, Is.True);

            string incompatibleDir = Path.Combine(Path.GetTempPath(), "ody-s04-112-incompat-" + Guid.NewGuid().ToString("N"));
            Result<CampaignHandle> incompatibleCampaign = _campaignRepository.Create(new CreateCampaignRequest(incompatibleDir, "Incompatible Ruleset Campaign", "ruleset.other", "9.0.0", "0.1.0"), NewCommandId(), TestCorrelationId);
            Assert.That(incompatibleCampaign.IsSuccess, Is.True);

            try
            {
                var importRequest = new ImportCharacterRequest(incompatibleCampaign.Value, _bundleDir, NewUserId());
                Result<CharacterRecord> imported = _characterRepository.ImportCharacter(importRequest, NewCommandId(), NewCommandId(), TestCorrelationId);

                Assert.That(imported.IsFailure, Is.True);
                Assert.That(imported.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterDraftRulesetIncompatible));
            }
            finally
            {
                _campaignRepository.Close(incompatibleCampaign.Value, TestCorrelationId);
                if (Directory.Exists(incompatibleDir)) Directory.Delete(incompatibleDir, recursive: true);
            }
        }

        [Test]
        public void ImportCharacter_OnMissingBundle_IsRejected_Gracefully()
        {
            string missingDir = Path.Combine(Path.GetTempPath(), "ody-s04-112-missing-" + Guid.NewGuid().ToString("N"));
            var importRequest = new ImportCharacterRequest(_targetCampaign, missingDir, NewUserId());

            Result<CharacterRecord> imported = _characterRepository.ImportCharacter(importRequest, NewCommandId(), NewCommandId(), TestCorrelationId);

            Assert.That(imported.IsFailure, Is.True);
            Assert.That(imported.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterExportBundleMalformed));
        }

        [Test]
        public void RoundTrip_PreservesAttributesSkillsAbilitiesResourcesAnatomy_WithFreshInstanceIds()
        {
            CharacterRecord character = CreateActiveCharacter(_sourceCampaign);

            Result<CharacterRecord> granted = _characterRepository.GrantDevelopmentPoints(_sourceCampaign, character.CharacterId, 50, "fixture grant", NewUserId(), actorIsMainGm: true, character.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(granted.IsSuccess, Is.True);

            var strength = AttributeDefinitionId.Parse("Strength");
            Result<CharacterRecord> attributeResult = _characterRepository.PurchaseAttributeIncrease(_sourceCampaign, character.CharacterId, strength, toValue: 2, NewUserId(), actorIsMainGm: true, granted.Value.Revisions.MechanicsRevision, expectedAttributeRevision: 0, NewCommandId(), TestCorrelationId);
            Assert.That(attributeResult.IsSuccess, Is.True);

            var lockpicking = SkillDefinitionId.Parse("Lockpicking");
            Result<CharacterRecord> skillResult = _characterRepository.PurchaseSkillLevel(_sourceCampaign, character.CharacterId, lockpicking, toLevel: 1, NewUserId(), actorIsMainGm: true, attributeResult.Value.Revisions.MechanicsRevision, expectedSkillRevision: 0, NewCommandId(), TestCorrelationId);
            Assert.That(skillResult.IsSuccess, Is.True);

            var fireball = AbilityDefinitionId.Parse("Fireball");
            Result<CharacterRecord> abilityResult = _characterRepository.AcquireAbility(_sourceCampaign, character.CharacterId, fireball, SourceKind.GMGrant, null, RankMode.None, null, null, "{}", NewUserId(), actorIsMainGm: true, skillResult.Value.Revisions.MechanicsRevision, skillResult.Value.Revisions.CharacterAbilitiesRevision, NewCommandId(), TestCorrelationId);
            Assert.That(abilityResult.IsSuccess, Is.True);

            Result<CharacterRecord> resourceResult = _characterRepository.InitializeCharacterResource(_sourceCampaign, character.CharacterId, Health, NewUserId(), actorIsMainGm: true, abilityResult.Value.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);
            Assert.That(resourceResult.IsSuccess, Is.True);
            CharacterResource sourceResource = resourceResult.Value.Resources[0];
            long damagedValue = sourceResource.MinimumValue + 1;
            Result<CharacterRecord> damagedResult = _characterRepository.SetResourceCurrentValue(_sourceCampaign, character.CharacterId, sourceResource.CharacterResourceId, damagedValue, NewUserId(), actorIsMainGm: true, resourceResult.Value.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);
            Assert.That(damagedResult.IsSuccess, Is.True);

            Result<CharacterRecord> anatomyResult = _characterRepository.InitializeCharacterAnatomy(_sourceCampaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, damagedResult.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
            Assert.That(anatomyResult.IsSuccess, Is.True);

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_sourceCampaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.IsSuccess, Is.True);
            CharacterRecord fullSource = reRead.Value;

            Result<CharacterExportBundle> exported = _characterRepository.ExportCharacter(_sourceCampaign, character.CharacterId, _bundleDir, new ExportActorContext(NewUserId(), actorIsMainGm: true), TestCorrelationId);
            Assert.That(exported.IsSuccess, Is.True);

            var importRequest = new ImportCharacterRequest(_targetCampaign, _bundleDir, NewUserId());
            Result<CharacterRecord> imported = _characterRepository.ImportCharacter(importRequest, NewCommandId(), NewCommandId(), TestCorrelationId);
            Assert.That(imported.IsSuccess, Is.True);
            CharacterRecord target = imported.Value;

            // Values preserved.
            Assert.That(target.DevelopmentPool.Earned, Is.EqualTo(fullSource.DevelopmentPool.Earned));
            Assert.That(target.DevelopmentPool.Spent, Is.EqualTo(fullSource.DevelopmentPool.Spent));
            Assert.That(target.DevelopmentPool.Reserved, Is.EqualTo(0), "Reserved is never imported -- it has no meaning without its own non-exported pending AdvancementRecommendation rows");

            AttributeValue targetAttribute = target.Attributes.Single(a => a.AttributeDefinitionId.Equals(strength));
            Assert.That(targetAttribute.BaseValue, Is.EqualTo(2));

            CharacterSkill targetSkill = target.Skills.Single(s => s.SkillDefinitionId.Equals(lockpicking));
            Assert.That(targetSkill.Level, Is.EqualTo(1));

            Assert.That(target.Abilities, Has.Count.EqualTo(1));
            CharacterAbility targetAbility = target.Abilities[0];
            Assert.That(targetAbility.AbilityDefinitionId, Is.EqualTo(fireball));
            Assert.That(targetAbility.CharacterAbilityId, Is.Not.EqualTo(abilityResult.Value.Abilities[0].CharacterAbilityId), "a fresh CharacterAbilityId must be minted on import, never the source campaign's own instance id");

            Assert.That(target.Resources, Has.Count.EqualTo(1));
            CharacterResource targetResource = target.Resources[0];
            Assert.That(targetResource.ResourceDefinitionId, Is.EqualTo(Health));
            Assert.That(targetResource.CurrentValue, Is.EqualTo(damagedValue));
            Assert.That(targetResource.CharacterResourceId, Is.Not.EqualTo(sourceResource.CharacterResourceId), "a fresh CharacterResourceId must be minted on import");

            Assert.That(target.Anatomy, Is.Not.Null);
            Assert.That(target.Anatomy!.BodyParts.Count, Is.EqualTo(fullSource.Anatomy!.BodyParts.Count));
        }

        [Test]
        public void ImportCharacter_WithoutTouchedSections_LeavesThoseRevisionsAtInitial()
        {
            CharacterRecord character = CreateActiveCharacter(_sourceCampaign);
            Result<CharacterExportBundle> exported = _characterRepository.ExportCharacter(_sourceCampaign, character.CharacterId, _bundleDir, new ExportActorContext(NewUserId(), actorIsMainGm: true), TestCorrelationId);
            Assert.That(exported.IsSuccess, Is.True);

            var importRequest = new ImportCharacterRequest(_targetCampaign, _bundleDir, NewUserId());
            Result<CharacterRecord> imported = _characterRepository.ImportCharacter(importRequest, NewCommandId(), NewCommandId(), TestCorrelationId);

            Assert.That(imported.IsSuccess, Is.True);
            Assert.That(imported.Value.Abilities, Is.Empty);
            Assert.That(imported.Value.Resources, Is.Empty);
            Assert.That(imported.Value.Anatomy, Is.Null);
            Assert.That(imported.Value.Revisions.CharacterAbilitiesRevision, Is.EqualTo(1));
            Assert.That(imported.Value.Revisions.CharacterResourcesRevision, Is.EqualTo(1));
            Assert.That(imported.Value.Revisions.CharacterAnatomyRevision, Is.EqualTo(1));
        }

        [Test]
        public void ImportCharacter_DuplicateCommandIds_DoesNotDuplicateEffect()
        {
            CharacterRecord character = CreateActiveCharacter(_sourceCampaign);
            Result<CharacterExportBundle> exported = _characterRepository.ExportCharacter(_sourceCampaign, character.CharacterId, _bundleDir, new ExportActorContext(NewUserId(), actorIsMainGm: true), TestCorrelationId);
            Assert.That(exported.IsSuccess, Is.True);

            CommandId bindCommandId = NewCommandId();
            CommandId applyCommandId = NewCommandId();
            var importRequest = new ImportCharacterRequest(_targetCampaign, _bundleDir, NewUserId());

            Result<CharacterRecord> first = _characterRepository.ImportCharacter(importRequest, bindCommandId, applyCommandId, TestCorrelationId);
            Assert.That(first.IsSuccess, Is.True);

            Result<CharacterRecord> replay = _characterRepository.ImportCharacter(importRequest, bindCommandId, applyCommandId, TestCorrelationId);
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.CharacterId, Is.EqualTo(first.Value.CharacterId), "a replayed duplicate CommandId pair must not create a second imported Character");
        }
    }
}
