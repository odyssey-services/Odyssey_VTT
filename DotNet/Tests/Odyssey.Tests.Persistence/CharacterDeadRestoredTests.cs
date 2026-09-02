using System;
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
    /// ODY-S04-111: real, non-stubbed tests against a real temp-directory
    /// campaign and a real SQLite database, mirroring
    /// <see cref="CharacterArchivePhysicalDeleteTests"/>'s exact fixture
    /// convention. Covers <c>TransitionCharacterToDead</c> (section 1.1's
    /// two-path discriminator, CAP-INV-008, transition legality, section
    /// 1.4's ADR-024 non-interference) and <c>RestoreDeadCharacter</c>
    /// (section 1.3's forward-not-compensating event, section 5 item 2's
    /// multi-section revision declaration, section 1.2's RuntimeState
    /// boundary).
    /// </summary>
    public sealed class CharacterDeadRestoredTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
        private static readonly ResourceDefinitionId Health = ResourceDefinitionId.Parse("Health");
        private static readonly AnatomyProfileDefinitionId Humanoid = AnatomyProfileDefinitionId.Parse("Humanoid");

        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;
        private SqliteCharacterRepository _characterRepository = null!;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s04-111-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_campaignDir, "Dead Restored Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
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

        private CharacterRecord CreateCharacter(string name = "Dead Restored Character")
        {
            var request = new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, name);
            Result<CharacterRecord> created = _characterRepository.CreateCharacter(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        private CharacterRecord ActivateCharacter(CharacterRecord character)
        {
            Result<CharacterRecord> approved = _characterRepository.ApproveCharacterDraft(_campaign, character.CharacterId, actorIsMainGm: true, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);
            Assert.That(approved.IsSuccess, Is.True);
            return approved.Value;
        }

        private CharacterRecord KillCharacter(CharacterRecord character)
        {
            Result<CharacterRecord> dead = _characterRepository.TransitionCharacterToDead(_campaign, character.CharacterId, LifecycleDeathIssuerKind.GMOverride, NewUserId(), actorIsMainGm: true, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);
            Assert.That(dead.IsSuccess, Is.True);
            return dead.Value;
        }

        private SqliteConnection OpenReadOnly() => new SqliteConnection("Data Source=" + Path.Combine(_campaignDir, "campaign.db") + ";Mode=ReadOnly");

        // ---- TransitionCharacterToDead -----------------------------------------

        [Test]
        public void TransitionCharacterToDead_GMOverride_ByMainGm_Succeeds()
        {
            CharacterRecord character = ActivateCharacter(CreateCharacter());

            Result<CharacterRecord> dead = _characterRepository.TransitionCharacterToDead(_campaign, character.CharacterId, LifecycleDeathIssuerKind.GMOverride, NewUserId(), actorIsMainGm: true, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);

            Assert.That(dead.IsSuccess, Is.True);
            Assert.That(dead.Value.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Dead));
        }

        [Test]
        public void TransitionCharacterToDead_GMOverride_ByNonMainGm_IsRejected_NoStateChange()
        {
            CharacterRecord character = ActivateCharacter(CreateCharacter());

            Result<CharacterRecord> dead = _characterRepository.TransitionCharacterToDead(_campaign, character.CharacterId, LifecycleDeathIssuerKind.GMOverride, NewUserId(), actorIsMainGm: false, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);

            Assert.That(dead.IsFailure, Is.True);
            Assert.That(dead.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterDeadTransitionDenied));

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Active));
        }

        [Test]
        public void TransitionCharacterToDead_HostSystemFatalDamageCompletion_Succeeds()
        {
            // Section 1.1: structural entry point only -- no real Rules
            // Engine workflow exists, so no actorIsMainGm check applies to
            // this path at all (actorIsMainGm: false, and it still succeeds).
            CharacterRecord character = ActivateCharacter(CreateCharacter());

            Result<CharacterRecord> dead = _characterRepository.TransitionCharacterToDead(_campaign, character.CharacterId, LifecycleDeathIssuerKind.HostSystemFatalDamageCompletion, NewUserId(), actorIsMainGm: false, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);

            Assert.That(dead.IsSuccess, Is.True);
            Assert.That(dead.Value.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Dead));
        }

        [Test]
        public void TransitionCharacterToDead_FromIllegalSourceState_IsRejected()
        {
            // Draft -> Dead is not a legal edge (product section 7.1 /
            // CharacterLifecycleTransitions.IsValidTransition).
            CharacterRecord character = CreateCharacter();
            Assert.That(character.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Draft));

            Result<CharacterRecord> dead = _characterRepository.TransitionCharacterToDead(_campaign, character.CharacterId, LifecycleDeathIssuerKind.GMOverride, NewUserId(), actorIsMainGm: true, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);

            Assert.That(dead.IsFailure, Is.True);
            Assert.That(dead.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterLifecycleTransitionInvalid));
        }

        [Test]
        public void TransitionCharacterToDead_DoesNotTouch_DevelopmentPoolOrMechanicsRevision()
        {
            // ADR-025 section 6.2 / this task's section 1.4: a pending
            // AdvancementRecommendation/Reserved amount and the Mechanics
            // section's own revision counter must survive the Dead
            // transition completely unchanged.
            CharacterRecord character = ActivateCharacter(CreateCharacter());
            Result<CharacterRecord> granted = _characterRepository.GrantDevelopmentPoints(_campaign, character.CharacterId, 10, "Grant", NewUserId(), actorIsMainGm: true, character.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(granted.IsSuccess, Is.True);
            long earnedBefore = granted.Value.DevelopmentPool.Earned;
            long reservedBefore = granted.Value.DevelopmentPool.Reserved;
            long mechanicsRevisionBefore = granted.Value.Revisions.MechanicsRevision;

            Result<CharacterRecord> dead = _characterRepository.TransitionCharacterToDead(_campaign, character.CharacterId, LifecycleDeathIssuerKind.GMOverride, NewUserId(), actorIsMainGm: true, granted.Value.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);

            Assert.That(dead.IsSuccess, Is.True);
            Assert.That(dead.Value.DevelopmentPool.Earned, Is.EqualTo(earnedBefore));
            Assert.That(dead.Value.DevelopmentPool.Reserved, Is.EqualTo(reservedBefore));
            Assert.That(dead.Value.Revisions.MechanicsRevision, Is.EqualTo(mechanicsRevisionBefore), "the Dead transition must not touch the Mechanics section at all");
        }

        [Test]
        public void TransitionCharacterToDead_DuplicateCommandId_DoesNotDuplicateEffect()
        {
            CharacterRecord character = ActivateCharacter(CreateCharacter());
            CommandId commandId = NewCommandId();

            Result<CharacterRecord> first = _characterRepository.TransitionCharacterToDead(_campaign, character.CharacterId, LifecycleDeathIssuerKind.GMOverride, NewUserId(), actorIsMainGm: true, character.Revisions.LifecycleRevision, commandId, TestCorrelationId);
            Assert.That(first.IsSuccess, Is.True);

            Result<CharacterRecord> replay = _characterRepository.TransitionCharacterToDead(_campaign, character.CharacterId, LifecycleDeathIssuerKind.GMOverride, NewUserId(), actorIsMainGm: true, character.Revisions.LifecycleRevision, commandId, TestCorrelationId);
            Assert.That(replay.IsSuccess, Is.True);

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Dead));
            Assert.That(reRead.Value.Revisions.LifecycleRevision, Is.EqualTo(character.Revisions.LifecycleRevision + 1), "a replayed duplicate CommandId must not transition twice");
        }

        // ---- RestoreDeadCharacter -----------------------------------------------

        [Test]
        public void RestoreDeadCharacter_FromDead_Succeeds_ChangesLifecycleStatus()
        {
            CharacterRecord dead = KillCharacter(ActivateCharacter(CreateCharacter()));

            var request = new RestoreDeadCharacterRequest(
                _campaign, dead.CharacterId, CharacterLifecycleStatus.Active, "brought back by the party cleric",
                newBodyParts: null, newPermanentModifications: null, newResourceCurrentValues: null,
                NewUserId(), actorIsMainGm: true, dead.Revisions.LifecycleRevision,
                expectedCharacterAnatomyRevision: null, expectedCharacterResourcesRevision: null);

            Result<CharacterRecord> restored = _characterRepository.RestoreDeadCharacter(request, NewCommandId(), TestCorrelationId);

            Assert.That(restored.IsSuccess, Is.True);
            Assert.That(restored.Value.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Active));
        }

        [Test]
        public void RestoreDeadCharacter_FromNonDeadState_IsRejected()
        {
            CharacterRecord character = ActivateCharacter(CreateCharacter());

            var request = new RestoreDeadCharacterRequest(
                _campaign, character.CharacterId, CharacterLifecycleStatus.Active, "not actually dead",
                newBodyParts: null, newPermanentModifications: null, newResourceCurrentValues: null,
                NewUserId(), actorIsMainGm: true, character.Revisions.LifecycleRevision,
                expectedCharacterAnatomyRevision: null, expectedCharacterResourcesRevision: null);

            Result<CharacterRecord> restored = _characterRepository.RestoreDeadCharacter(request, NewCommandId(), TestCorrelationId);

            Assert.That(restored.IsFailure, Is.True);
            Assert.That(restored.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRestoreNotDead));
        }

        [Test]
        public void RestoreDeadCharacter_WithoutReasonCode_IsRejected()
        {
            CharacterRecord dead = KillCharacter(ActivateCharacter(CreateCharacter()));

            var request = new RestoreDeadCharacterRequest(
                _campaign, dead.CharacterId, CharacterLifecycleStatus.Active, "",
                newBodyParts: null, newPermanentModifications: null, newResourceCurrentValues: null,
                NewUserId(), actorIsMainGm: true, dead.Revisions.LifecycleRevision,
                expectedCharacterAnatomyRevision: null, expectedCharacterResourcesRevision: null);

            Result<CharacterRecord> restored = _characterRepository.RestoreDeadCharacter(request, NewCommandId(), TestCorrelationId);

            Assert.That(restored.IsFailure, Is.True);
            Assert.That(restored.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRestoreReasonRequired));
        }

        [Test]
        public void RestoreDeadCharacter_ByNonMainGm_IsRejected_NoStateChange()
        {
            CharacterRecord dead = KillCharacter(ActivateCharacter(CreateCharacter()));

            var request = new RestoreDeadCharacterRequest(
                _campaign, dead.CharacterId, CharacterLifecycleStatus.Active, "attempted by a non-GM",
                newBodyParts: null, newPermanentModifications: null, newResourceCurrentValues: null,
                NewUserId(), actorIsMainGm: false, dead.Revisions.LifecycleRevision,
                expectedCharacterAnatomyRevision: null, expectedCharacterResourcesRevision: null);

            Result<CharacterRecord> restored = _characterRepository.RestoreDeadCharacter(request, NewCommandId(), TestCorrelationId);

            Assert.That(restored.IsFailure, Is.True);
            Assert.That(restored.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRestoreDenied));

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, dead.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Dead));
        }

        [Test]
        public void RestoreDeadCharacter_WithExplicitAnatomyAndResourceChanges_UpdatesValues_AndOnlyThoseRevisionsIncrease()
        {
            CharacterRecord character = ActivateCharacter(CreateCharacter());
            Result<CharacterRecord> withAnatomy = _characterRepository.InitializeCharacterAnatomy(_campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
            Assert.That(withAnatomy.IsSuccess, Is.True);
            Result<CharacterRecord> withResource = _characterRepository.InitializeCharacterResource(_campaign, character.CharacterId, Health, NewUserId(), actorIsMainGm: true, withAnatomy.Value.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);
            Assert.That(withResource.IsSuccess, Is.True);

            CharacterRecord dead = KillCharacter(withResource.Value);
            long anatomyRevisionBefore = dead.Revisions.CharacterAnatomyRevision;
            long resourcesRevisionBefore = dead.Revisions.CharacterResourcesRevision;
            CharacterResourceId resourceId = dead.Resources[0].CharacterResourceId;
            long restoredCurrentValue = dead.Resources[0].MinimumValue + 1;

            var newBodyParts = new[] { new BodyPart(BodyPartId.Parse("Core"), "Core", 30, null, "{}") };
            var request = new RestoreDeadCharacterRequest(
                _campaign, dead.CharacterId, CharacterLifecycleStatus.Active, "restored with new body and partial health",
                newBodyParts: newBodyParts, newPermanentModifications: null,
                newResourceCurrentValues: new[] { new CharacterRestoreResourceValue(resourceId, restoredCurrentValue) },
                NewUserId(), actorIsMainGm: true, dead.Revisions.LifecycleRevision,
                expectedCharacterAnatomyRevision: anatomyRevisionBefore, expectedCharacterResourcesRevision: resourcesRevisionBefore);

            Result<CharacterRecord> restored = _characterRepository.RestoreDeadCharacter(request, NewCommandId(), TestCorrelationId);

            Assert.That(restored.IsSuccess, Is.True);
            Assert.That(restored.Value.Anatomy!.BodyParts.Count, Is.EqualTo(1));
            Assert.That(restored.Value.Anatomy.BodyParts[0].BodyPartId, Is.EqualTo(BodyPartId.Parse("Core")));
            Assert.That(restored.Value.Revisions.CharacterAnatomyRevision, Is.EqualTo(anatomyRevisionBefore + 1));
            Assert.That(restored.Value.Resources[0].CurrentValue, Is.EqualTo(restoredCurrentValue));
            Assert.That(restored.Value.Revisions.CharacterResourcesRevision, Is.EqualTo(resourcesRevisionBefore + 1));
        }

        [Test]
        public void RestoreDeadCharacter_WithoutTouchingAnatomyOrResources_LeavesThoseRevisionsUnchanged()
        {
            CharacterRecord character = ActivateCharacter(CreateCharacter());
            Result<CharacterRecord> withAnatomy = _characterRepository.InitializeCharacterAnatomy(_campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
            Result<CharacterRecord> withResource = _characterRepository.InitializeCharacterResource(_campaign, character.CharacterId, Health, NewUserId(), actorIsMainGm: true, withAnatomy.Value.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);
            CharacterRecord dead = KillCharacter(withResource.Value);
            long anatomyRevisionBefore = dead.Revisions.CharacterAnatomyRevision;
            long resourcesRevisionBefore = dead.Revisions.CharacterResourcesRevision;

            var request = new RestoreDeadCharacterRequest(
                _campaign, dead.CharacterId, CharacterLifecycleStatus.Active, "restored without touching anatomy or resources",
                newBodyParts: null, newPermanentModifications: null, newResourceCurrentValues: null,
                NewUserId(), actorIsMainGm: true, dead.Revisions.LifecycleRevision,
                expectedCharacterAnatomyRevision: null, expectedCharacterResourcesRevision: null);

            Result<CharacterRecord> restored = _characterRepository.RestoreDeadCharacter(request, NewCommandId(), TestCorrelationId);

            Assert.That(restored.IsSuccess, Is.True);
            Assert.That(restored.Value.Revisions.CharacterAnatomyRevision, Is.EqualTo(anatomyRevisionBefore), "GM did not choose to touch CharacterAnatomy -- its revision must not increase");
            Assert.That(restored.Value.Revisions.CharacterResourcesRevision, Is.EqualTo(resourcesRevisionBefore), "GM did not choose to touch CharacterResources -- its revision must not increase");
        }

        [Test]
        public void RestoreDeadCharacter_ProducesForwardEvent_NotCompensating()
        {
            // Section 1.3: CharacterRestored must never be a compensating
            // event referencing CharacterDied (CAP-INV-008 -- this is not
            // "Undo").
            CharacterRecord dead = KillCharacter(ActivateCharacter(CreateCharacter()));

            var request = new RestoreDeadCharacterRequest(
                _campaign, dead.CharacterId, CharacterLifecycleStatus.Active, "forward event check",
                newBodyParts: null, newPermanentModifications: null, newResourceCurrentValues: null,
                NewUserId(), actorIsMainGm: true, dead.Revisions.LifecycleRevision,
                expectedCharacterAnatomyRevision: null, expectedCharacterResourcesRevision: null);
            Result<CharacterRecord> restored = _characterRepository.RestoreDeadCharacter(request, NewCommandId(), TestCorrelationId);
            Assert.That(restored.IsSuccess, Is.True);

            using SqliteConnection connection = OpenReadOnly();
            connection.Open();
            using var select = connection.CreateCommand();
            select.CommandText = "SELECT IsCompensating, OriginalEventId FROM DomainEvents WHERE EventType = 'odyssey.persistence.character_restored';";
            using SqliteDataReader reader = select.ExecuteReader();
            Assert.That(reader.Read(), Is.True, "a character_restored row must exist");
            Assert.That(Convert.ToInt64(reader["IsCompensating"]), Is.EqualTo(0), "CharacterRestored must not be marked IsCompensating");
            Assert.That(reader["OriginalEventId"], Is.InstanceOf<DBNull>(), "CharacterRestored must not reference an OriginalEventId");
        }

        [Test]
        public void RestoreDeadCharacter_DuplicateCommandId_DoesNotDuplicateEffect()
        {
            CharacterRecord dead = KillCharacter(ActivateCharacter(CreateCharacter()));
            CommandId commandId = NewCommandId();

            var request = new RestoreDeadCharacterRequest(
                _campaign, dead.CharacterId, CharacterLifecycleStatus.Active, "duplicate check",
                newBodyParts: null, newPermanentModifications: null, newResourceCurrentValues: null,
                NewUserId(), actorIsMainGm: true, dead.Revisions.LifecycleRevision,
                expectedCharacterAnatomyRevision: null, expectedCharacterResourcesRevision: null);

            Result<CharacterRecord> first = _characterRepository.RestoreDeadCharacter(request, commandId, TestCorrelationId);
            Assert.That(first.IsSuccess, Is.True);

            Result<CharacterRecord> replay = _characterRepository.RestoreDeadCharacter(request, commandId, TestCorrelationId);
            Assert.That(replay.IsSuccess, Is.True);

            using SqliteConnection connection = OpenReadOnly();
            connection.Open();
            using var select = connection.CreateCommand();
            select.CommandText = "SELECT COUNT(*) FROM DomainEvents WHERE EventType = 'odyssey.persistence.character_restored';";
            long count = Convert.ToInt64(select.ExecuteScalar());
            Assert.That(count, Is.EqualTo(1), "a replayed duplicate CommandId must not append a second CharacterRestored event");
        }

        [Test]
        public void ConcurrentEdit_LifecycleDeath_AndIndependentMechanicsPurchase_CommitWithoutFalseConflict()
        {
            // Section 1.4's own corollary: since the Dead transition only
            // ever declares/checks LifecycleRevision, an independent
            // Mechanics-section command (checking only MechanicsRevision)
            // must not conflict with it, regardless of ordering.
            CharacterRecord character = ActivateCharacter(CreateCharacter());

            Result<CharacterRecord> dead = _characterRepository.TransitionCharacterToDead(_campaign, character.CharacterId, LifecycleDeathIssuerKind.GMOverride, NewUserId(), actorIsMainGm: true, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);
            Assert.That(dead.IsSuccess, Is.True);

            Result<CharacterRecord> granted = _characterRepository.GrantDevelopmentPoints(_campaign, character.CharacterId, 5, "Grant after death", NewUserId(), actorIsMainGm: true, character.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);

            Assert.That(granted.IsSuccess, Is.True, "an independent Mechanics-section command must not be falsely rejected by the earlier, unrelated Lifecycle-section transition");
            Assert.That(granted.Value.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Dead));
        }
    }
}
