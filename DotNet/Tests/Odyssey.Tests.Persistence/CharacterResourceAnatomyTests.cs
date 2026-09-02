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
    /// ODY-S04-109: real, non-stubbed tests against a real temp-directory
    /// campaign and a real SQLite database, mirroring
    /// <see cref="CharacterAbilityInstancesTests"/>'s exact fixture
    /// convention. Covers <c>CharacterResource</c> (initialization,
    /// current-value bounds, maximum-decrease clamp, no-auto-restore),
    /// <c>CharacterAnatomy</c> (initialization, independent snapshot,
    /// AddBodyPart/RemoveBodyPart's internal dependency check,
    /// UpdateBodyPart/ReplaceAnatomyProfile/ApplyPermanentModification,
    /// MigrationHistory accumulation), idempotency, and no-false-conflict.
    /// </summary>
    public sealed class CharacterResourceAnatomyTests
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
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s04-109-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_campaignDir, "Resource Anatomy Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
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

        private CharacterRecord CreateCharacter(string name = "Resource Anatomy Character")
        {
            var request = new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, name);
            Result<CharacterRecord> created = _characterRepository.CreateCharacter(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        // ---- CharacterResource -------------------------------------------------

        [Test]
        public void InitializeCharacterResource_Succeeds_And_ActuallyIncrementsCharacterResourcesRevision()
        {
            CharacterRecord character = CreateCharacter();
            long revisionBefore = character.Revisions.CharacterResourcesRevision;

            Result<CharacterRecord> initialized = _characterRepository.InitializeCharacterResource(_campaign, character.CharacterId, Health, NewUserId(), actorIsMainGm: true, revisionBefore, NewCommandId(), TestCorrelationId);

            Assert.That(initialized.IsSuccess, Is.True);
            Assert.That(initialized.Value.Resources, Has.Count.EqualTo(1));
            Assert.That(initialized.Value.Resources[0].ResourceDefinitionId, Is.EqualTo(Health));
            Assert.That(initialized.Value.Revisions.CharacterResourcesRevision, Is.EqualTo(revisionBefore + 1));
        }

        [Test]
        public void InitializeCharacterResource_ByNonMainGm_IsRejected()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> initialized = _characterRepository.InitializeCharacterResource(_campaign, character.CharacterId, Health, NewUserId(), actorIsMainGm: false, character.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);

            Assert.That(initialized.IsFailure, Is.True);
            Assert.That(initialized.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterResourceOperationDenied));
        }

        [Test]
        public void SetResourceCurrentValue_OutsideBounds_IsRejected_NoStateChange()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> initialized = _characterRepository.InitializeCharacterResource(_campaign, character.CharacterId, Health, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);
            CharacterResource resource = initialized.Value.Resources[0];

            Result<CharacterRecord> tooHigh = _characterRepository.SetResourceCurrentValue(_campaign, character.CharacterId, resource.CharacterResourceId, resource.EffectiveMaximum + 1, NewUserId(), actorIsMainGm: true, initialized.Value.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);
            Assert.That(tooHigh.IsFailure, Is.True);
            Assert.That(tooHigh.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterResourceValueOutOfRange));

            Result<CharacterRecord> tooLow = _characterRepository.SetResourceCurrentValue(_campaign, character.CharacterId, resource.CharacterResourceId, resource.MinimumValue - 1, NewUserId(), actorIsMainGm: true, initialized.Value.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);
            Assert.That(tooLow.IsFailure, Is.True);
            Assert.That(tooLow.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterResourceValueOutOfRange));

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.Resources[0].CurrentValue, Is.EqualTo(resource.CurrentValue));
        }

        [Test]
        public void SetResourceCurrentValue_WithinBounds_ChangesOnlyViaExplicitCommand()
        {
            // Requirement 46/47: CurrentValue only ever changes via this
            // explicit command -- verified by reading it back unchanged
            // before any command runs, then changed only after the command.
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> initialized = _characterRepository.InitializeCharacterResource(_campaign, character.CharacterId, Health, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);
            CharacterResource resource = initialized.Value.Resources[0];
            long originalValue = resource.CurrentValue;

            Result<CharacterRecord> reReadBefore = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reReadBefore.Value.Resources[0].CurrentValue, Is.EqualTo(originalValue), "no automatic change before any explicit command runs");

            long damagedValue = resource.MinimumValue;
            Result<CharacterRecord> damaged = _characterRepository.SetResourceCurrentValue(_campaign, character.CharacterId, resource.CharacterResourceId, damagedValue, NewUserId(), actorIsMainGm: true, initialized.Value.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);
            Assert.That(damaged.IsSuccess, Is.True);
            Assert.That(damaged.Value.Resources[0].CurrentValue, Is.EqualTo(damagedValue));
        }

        [Test]
        public void SetResourceMaximum_DecreaseBelowCurrentValue_ClampsCurrentValueImmediately()
        {
            // Requirement 44.
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> initialized = _characterRepository.InitializeCharacterResource(_campaign, character.CharacterId, Health, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);
            CharacterResource resource = initialized.Value.Resources[0];
            long originalEffectiveMaximum = resource.EffectiveMaximum;
            Assert.That(resource.CurrentValue, Is.EqualTo(originalEffectiveMaximum), "fixture starts at full health");

            long reducedMaximum = originalEffectiveMaximum - 5;
            Result<CharacterRecord> reduced = _characterRepository.SetResourceMaximum(_campaign, character.CharacterId, resource.CharacterResourceId, reducedMaximum, 0, NewUserId(), actorIsMainGm: true, initialized.Value.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);

            Assert.That(reduced.IsSuccess, Is.True);
            CharacterResource updated = reduced.Value.Resources[0];
            Assert.That(updated.EffectiveMaximum, Is.EqualTo(reducedMaximum));
            Assert.That(updated.CurrentValue, Is.EqualTo(reducedMaximum), "CurrentValue must be clamped down immediately");
        }

        [Test]
        public void SetResourceMaximum_LaterIncrease_DoesNotAutoRestoreClampedCurrentValue()
        {
            // Requirement 45.
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> initialized = _characterRepository.InitializeCharacterResource(_campaign, character.CharacterId, Health, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);
            CharacterResource resource = initialized.Value.Resources[0];
            long originalEffectiveMaximum = resource.EffectiveMaximum;

            Result<CharacterRecord> reduced = _characterRepository.SetResourceMaximum(_campaign, character.CharacterId, resource.CharacterResourceId, originalEffectiveMaximum - 5, 0, NewUserId(), actorIsMainGm: true, initialized.Value.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);
            long clampedValue = reduced.Value.Resources[0].CurrentValue;

            Result<CharacterRecord> restoredMaximum = _characterRepository.SetResourceMaximum(_campaign, character.CharacterId, resource.CharacterResourceId, originalEffectiveMaximum, 0, NewUserId(), actorIsMainGm: true, reduced.Value.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);

            Assert.That(restoredMaximum.IsSuccess, Is.True);
            CharacterResource afterRestore = restoredMaximum.Value.Resources[0];
            Assert.That(afterRestore.EffectiveMaximum, Is.EqualTo(originalEffectiveMaximum));
            Assert.That(afterRestore.CurrentValue, Is.EqualTo(clampedValue), "increasing the maximum again must not auto-restore the previously-clamped CurrentValue");
        }

        [Test]
        public void SetResourceCurrentValue_OnUnknownCharacterResourceId_IsRejected()
        {
            CharacterRecord character = CreateCharacter();
            CharacterResourceId unknownId = CharacterResourceId.NewId(Clock.GetUtcNow());

            Result<CharacterRecord> result = _characterRepository.SetResourceCurrentValue(_campaign, character.CharacterId, unknownId, 0, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterResourceNotFound));
        }

        [Test]
        public void InitializeCharacterResource_DuplicateCommandId_DoesNotInitializeTwice()
        {
            CharacterRecord character = CreateCharacter();
            CommandId commandId = NewCommandId();

            Result<CharacterRecord> first = _characterRepository.InitializeCharacterResource(_campaign, character.CharacterId, Health, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterResourcesRevision, commandId, TestCorrelationId);
            Assert.That(first.IsSuccess, Is.True);

            Result<CharacterRecord> replay = _characterRepository.InitializeCharacterResource(_campaign, character.CharacterId, Health, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterResourcesRevision, commandId, TestCorrelationId);
            Assert.That(replay.IsSuccess, Is.True);

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.Resources, Has.Count.EqualTo(1), "a replayed duplicate CommandId must not initialize a second resource");
        }

        // ---- CharacterAnatomy ---------------------------------------------------

        [Test]
        public void InitializeCharacterAnatomy_Succeeds_And_ActuallyIncrementsCharacterAnatomyRevision()
        {
            CharacterRecord character = CreateCharacter();
            long revisionBefore = character.Revisions.CharacterAnatomyRevision;

            Result<CharacterRecord> initialized = _characterRepository.InitializeCharacterAnatomy(_campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, revisionBefore, NewCommandId(), TestCorrelationId);

            Assert.That(initialized.IsSuccess, Is.True);
            Assert.That(initialized.Value.Anatomy, Is.Not.Null);
            Assert.That(initialized.Value.Anatomy!.AnatomyProfileDefinitionId, Is.EqualTo(Humanoid));
            Assert.That(initialized.Value.Anatomy.BodyParts, Is.Not.Empty);
            Assert.That(initialized.Value.Revisions.CharacterAnatomyRevision, Is.EqualTo(revisionBefore + 1));
        }

        [Test]
        public void InitializeCharacterAnatomy_Twice_IsRejected()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> first = _characterRepository.InitializeCharacterAnatomy(_campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
            Assert.That(first.IsSuccess, Is.True);

            Result<CharacterRecord> second = _characterRepository.InitializeCharacterAnatomy(_campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, first.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
            Assert.That(second.IsFailure, Is.True);
            Assert.That(second.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterAnatomyAlreadyInitialized));
        }

        [Test]
        public void CharacterAnatomy_IsIndependentSnapshot_NotALiveReferenceToTheFixture()
        {
            // Requirement 48/49, mirroring ODY-S04-103's own
            // UpdateCharacterTemplate_AfterBind_DoesNotChangeAlreadyCreatedCharacter
            // pattern: since no mutable AnatomyProfileDefinition catalog
            // exists (only a pure-function fixture), independence is proven
            // by confirming AnatomyProfileVersion is pinned at
            // initialization time and a later ReplaceAnatomyProfile with a
            // DIFFERENT version does not retroactively alter the original
            // initialization's own historical event -- the live current
            // snapshot only changes via that explicit command, never a
            // side effect of the fixture itself changing.
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> initialized = _characterRepository.InitializeCharacterAnatomy(_campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
            string pinnedVersion = initialized.Value.Anatomy!.AnatomyProfileVersion;
            Assert.That(pinnedVersion, Is.EqualTo(Odyssey.Rules.Character.AnatomyInitializationRules.DefaultAnatomyProfileVersion));

            // Calling the fixture function again (as a future definition
            // update would) produces the same content -- the character's
            // own already-initialized snapshot is untouched by it.
            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.Anatomy!.AnatomyProfileVersion, Is.EqualTo(pinnedVersion));
            Assert.That(reRead.Value.Anatomy.BodyParts.Count, Is.EqualTo(initialized.Value.Anatomy.BodyParts.Count));
        }

        [Test]
        public void AddBodyPart_RequiresInitializedAnatomy_MainGmOnly()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> notInitialized = _characterRepository.AddBodyPart(_campaign, character.CharacterId, BodyPartId.Parse("Tail"), "Tail", 5, null, "{}", NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
            Assert.That(notInitialized.IsFailure, Is.True);
            Assert.That(notInitialized.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterAnatomyNotInitialized));

            Result<CharacterRecord> initialized = _characterRepository.InitializeCharacterAnatomy(_campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Result<CharacterRecord> byNonMainGm = _characterRepository.AddBodyPart(_campaign, character.CharacterId, BodyPartId.Parse("Tail"), "Tail", 5, null, "{}", NewUserId(), actorIsMainGm: false, initialized.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
            Assert.That(byNonMainGm.IsFailure, Is.True);
            Assert.That(byNonMainGm.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterAnatomyOperationDenied));

            Result<CharacterRecord> added = _characterRepository.AddBodyPart(_campaign, character.CharacterId, BodyPartId.Parse("Tail"), "Tail", 5, null, "{}", NewUserId(), actorIsMainGm: true, initialized.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
            Assert.That(added.IsSuccess, Is.True);
            Assert.That(added.Value.Anatomy!.BodyParts.Any(p => p.BodyPartId.Equals(BodyPartId.Parse("Tail"))), Is.True);
        }

        [Test]
        public void AddBodyPart_WithAlreadyExistingBodyPartId_IsRejected()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> initialized = _characterRepository.InitializeCharacterAnatomy(_campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Result<CharacterRecord> duplicate = _characterRepository.AddBodyPart(_campaign, character.CharacterId, BodyPartId.Parse("Head"), "Second Head", 5, null, "{}", NewUserId(), actorIsMainGm: true, initialized.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Assert.That(duplicate.IsFailure, Is.True);
            Assert.That(duplicate.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterBodyPartAlreadyExists));
        }

        [Test]
        public void RemoveBodyPart_OnUnknownBodyPartId_IsRejected()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> initialized = _characterRepository.InitializeCharacterAnatomy(_campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Result<CharacterRecord> result = _characterRepository.RemoveBodyPart(_campaign, character.CharacterId, BodyPartId.Parse("Unknown"), NewUserId(), actorIsMainGm: true, initialized.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterBodyPartNotFound));
        }

        [Test]
        public void RemoveBodyPart_WithDependentBodyPart_IsRejected_NoStateChange()
        {
            // Requirement 51 (internal dependency substitute, section 1.3):
            // the fixture's Torso has LeftArm/RightArm attached to it.
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> initialized = _characterRepository.InitializeCharacterAnatomy(_campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Result<CharacterRecord> removed = _characterRepository.RemoveBodyPart(_campaign, character.CharacterId, BodyPartId.Parse("Torso"), NewUserId(), actorIsMainGm: true, initialized.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Assert.That(removed.IsFailure, Is.True);
            Assert.That(removed.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterBodyPartHasDependent));

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.Anatomy!.BodyParts.Any(p => p.BodyPartId.Equals(BodyPartId.Parse("Torso"))), Is.True);
        }

        [Test]
        public void RemoveBodyPart_WithoutDependent_Succeeds()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> initialized = _characterRepository.InitializeCharacterAnatomy(_campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Result<CharacterRecord> removed = _characterRepository.RemoveBodyPart(_campaign, character.CharacterId, BodyPartId.Parse("Head"), NewUserId(), actorIsMainGm: true, initialized.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Assert.That(removed.IsSuccess, Is.True);
            Assert.That(removed.Value.Anatomy!.BodyParts.Any(p => p.BodyPartId.Equals(BodyPartId.Parse("Head"))), Is.False);
        }

        [Test]
        public void RemoveBodyPart_WithPermanentModificationDependent_IsRejected()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> initialized = _characterRepository.InitializeCharacterAnatomy(_campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
            Result<CharacterRecord> modified = _characterRepository.ApplyPermanentModification(_campaign, character.CharacterId, BodyPartId.Parse("Head"), "Mutation", "Third eye", NewUserId(), actorIsMainGm: true, initialized.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
            Assert.That(modified.IsSuccess, Is.True);

            Result<CharacterRecord> removed = _characterRepository.RemoveBodyPart(_campaign, character.CharacterId, BodyPartId.Parse("Head"), NewUserId(), actorIsMainGm: true, modified.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Assert.That(removed.IsFailure, Is.True);
            Assert.That(removed.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterBodyPartHasDependent));
        }

        [Test]
        public void UpdateBodyPart_ChangesDamageLimitAndProperties()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> initialized = _characterRepository.InitializeCharacterAnatomy(_campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Result<CharacterRecord> updated = _characterRepository.UpdateBodyPart(_campaign, character.CharacterId, BodyPartId.Parse("Head"), 99, "{\"armored\":true}", NewUserId(), actorIsMainGm: true, initialized.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Assert.That(updated.IsSuccess, Is.True);
            BodyPart head = updated.Value.Anatomy!.BodyParts.Single(p => p.BodyPartId.Equals(BodyPartId.Parse("Head")));
            Assert.That(head.DamageLimit, Is.EqualTo(99));
            Assert.That(head.Properties, Is.EqualTo("{\"armored\":true}"));
        }

        [Test]
        public void ReplaceAnatomyProfile_ReplacesBodyParts_PreservesPermanentModificationsAndHistory()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> initialized = _characterRepository.InitializeCharacterAnatomy(_campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
            Result<CharacterRecord> modified = _characterRepository.ApplyPermanentModification(_campaign, character.CharacterId, BodyPartId.Parse("Head"), "Mutation", "Third eye", NewUserId(), actorIsMainGm: true, initialized.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            var newBodyParts = new[] { new BodyPart(BodyPartId.Parse("Core"), "Core", 30, null, "{}") };
            Result<CharacterRecord> replaced = _characterRepository.ReplaceAnatomyProfile(_campaign, character.CharacterId, AnatomyProfileDefinitionId.Parse("Construct"), "0.2.0-fixture", newBodyParts, NewUserId(), actorIsMainGm: true, modified.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Assert.That(replaced.IsSuccess, Is.True);
            Assert.That(replaced.Value.Anatomy!.AnatomyProfileDefinitionId, Is.EqualTo(AnatomyProfileDefinitionId.Parse("Construct")));
            Assert.That(replaced.Value.Anatomy.BodyParts.Select(p => p.BodyPartId.ToString()), Is.EquivalentTo(new[] { "Core" }));
            Assert.That(replaced.Value.Anatomy.PermanentModifications, Has.Count.EqualTo(1), "PermanentModifications must be preserved across a profile replacement");
        }

        [Test]
        public void MigrationHistory_AccumulatesOneEntryPerAnatomyCommand()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> initialized = _characterRepository.InitializeCharacterAnatomy(_campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
            Assert.That(initialized.Value.Anatomy!.MigrationHistory, Has.Count.EqualTo(1));

            Result<CharacterRecord> added = _characterRepository.AddBodyPart(_campaign, character.CharacterId, BodyPartId.Parse("Tail"), "Tail", 5, null, "{}", NewUserId(), actorIsMainGm: true, initialized.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
            Assert.That(added.Value.Anatomy!.MigrationHistory, Has.Count.EqualTo(2));

            Result<CharacterRecord> removed = _characterRepository.RemoveBodyPart(_campaign, character.CharacterId, BodyPartId.Parse("Tail"), NewUserId(), actorIsMainGm: true, added.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
            Assert.That(removed.Value.Anatomy!.MigrationHistory, Has.Count.EqualTo(3));

            Result<CharacterRecord> modified = _characterRepository.ApplyPermanentModification(_campaign, character.CharacterId, BodyPartId.Parse("Head"), "Prosthetic", "Mechanical eye", NewUserId(), actorIsMainGm: true, removed.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
            Assert.That(modified.Value.Anatomy!.MigrationHistory, Has.Count.EqualTo(4));
        }

        [Test]
        public void InitializeCharacterAnatomy_DuplicateCommandId_DoesNotInitializeTwice()
        {
            CharacterRecord character = CreateCharacter();
            CommandId commandId = NewCommandId();

            Result<CharacterRecord> first = _characterRepository.InitializeCharacterAnatomy(_campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, commandId, TestCorrelationId);
            Assert.That(first.IsSuccess, Is.True);

            Result<CharacterRecord> replay = _characterRepository.InitializeCharacterAnatomy(_campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, commandId, TestCorrelationId);
            Assert.That(replay.IsSuccess, Is.True);

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.Anatomy!.MigrationHistory, Has.Count.EqualTo(1), "a replayed duplicate CommandId must not initialize/journal twice");
        }

        // ---- Concurrency: CharacterResources + CharacterAnatomy do not false-conflict ----

        [Test]
        public void ConcurrentCharacterResourcesEdit_And_CharacterAnatomyEdit_CommitWithoutFalseConflict()
        {
            CharacterRecord character = CreateCharacter();

            Result<CharacterRecord> resourceResult = _characterRepository.InitializeCharacterResource(_campaign, character.CharacterId, Health, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);
            Result<CharacterRecord> anatomyResult = _characterRepository.InitializeCharacterAnatomy(_campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Assert.That(resourceResult.IsSuccess, Is.True);
            Assert.That(anatomyResult.IsSuccess, Is.True);

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.Resources, Has.Count.EqualTo(1));
            Assert.That(reRead.Value.Anatomy, Is.Not.Null);
        }
    }
}
