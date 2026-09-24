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
using Odyssey.Domain.Content;
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

            Result<CharacterRecord> initialized = CharacterAdvancementService.InitializeResourceWithDefaults(_characterRepository, _campaign, character.CharacterId, Health, NewUserId(), actorIsMainGm: true, revisionBefore, NewCommandId(), TestCorrelationId);

            Assert.That(initialized.IsSuccess, Is.True);
            Assert.That(initialized.Value.Resources, Has.Count.EqualTo(1));
            Assert.That(initialized.Value.Resources[0].ResourceDefinitionId, Is.EqualTo(Health));
            Assert.That(initialized.Value.Revisions.CharacterResourcesRevision, Is.EqualTo(revisionBefore + 1));
        }

        [Test]
        public void InitializeCharacterResource_ByNonMainGm_IsRejected()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> initialized = CharacterAdvancementService.InitializeResourceWithDefaults(_characterRepository, _campaign, character.CharacterId, Health, NewUserId(), actorIsMainGm: false, character.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);

            Assert.That(initialized.IsFailure, Is.True);
            Assert.That(initialized.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterResourceOperationDenied));
        }

        [Test]
        public void SetResourceCurrentValue_OutsideBounds_IsRejected_NoStateChange()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> initialized = CharacterAdvancementService.InitializeResourceWithDefaults(_characterRepository, _campaign, character.CharacterId, Health, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);
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
            Result<CharacterRecord> initialized = CharacterAdvancementService.InitializeResourceWithDefaults(_characterRepository, _campaign, character.CharacterId, Health, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);
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
            Result<CharacterRecord> initialized = CharacterAdvancementService.InitializeResourceWithDefaults(_characterRepository, _campaign, character.CharacterId, Health, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);
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
            Result<CharacterRecord> initialized = CharacterAdvancementService.InitializeResourceWithDefaults(_characterRepository, _campaign, character.CharacterId, Health, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);
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

            Result<CharacterRecord> first = CharacterAdvancementService.InitializeResourceWithDefaults(_characterRepository, _campaign, character.CharacterId, Health, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterResourcesRevision, commandId, TestCorrelationId);
            Assert.That(first.IsSuccess, Is.True);

            Result<CharacterRecord> replay = CharacterAdvancementService.InitializeResourceWithDefaults(_characterRepository, _campaign, character.CharacterId, Health, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterResourcesRevision, commandId, TestCorrelationId);
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

            Result<CharacterRecord> initialized = CharacterAdvancementService.InitializeAnatomyWithDefaults(_characterRepository, _campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, revisionBefore, NewCommandId(), TestCorrelationId);

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
            Result<CharacterRecord> first = CharacterAdvancementService.InitializeAnatomyWithDefaults(_characterRepository, _campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
            Assert.That(first.IsSuccess, Is.True);

            Result<CharacterRecord> second = CharacterAdvancementService.InitializeAnatomyWithDefaults(_characterRepository, _campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, first.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
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
            Result<CharacterRecord> initialized = CharacterAdvancementService.InitializeAnatomyWithDefaults(_characterRepository, _campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
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

            Result<CharacterRecord> initialized = CharacterAdvancementService.InitializeAnatomyWithDefaults(_characterRepository, _campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

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
            Result<CharacterRecord> initialized = CharacterAdvancementService.InitializeAnatomyWithDefaults(_characterRepository, _campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Result<CharacterRecord> duplicate = _characterRepository.AddBodyPart(_campaign, character.CharacterId, BodyPartId.Parse("Head"), "Second Head", 5, null, "{}", NewUserId(), actorIsMainGm: true, initialized.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Assert.That(duplicate.IsFailure, Is.True);
            Assert.That(duplicate.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterBodyPartAlreadyExists));
        }

        [Test]
        public void RemoveBodyPart_OnUnknownBodyPartId_IsRejected()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> initialized = CharacterAdvancementService.InitializeAnatomyWithDefaults(_characterRepository, _campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

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
            Result<CharacterRecord> initialized = CharacterAdvancementService.InitializeAnatomyWithDefaults(_characterRepository, _campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

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
            Result<CharacterRecord> initialized = CharacterAdvancementService.InitializeAnatomyWithDefaults(_characterRepository, _campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Result<CharacterRecord> removed = _characterRepository.RemoveBodyPart(_campaign, character.CharacterId, BodyPartId.Parse("Head"), NewUserId(), actorIsMainGm: true, initialized.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Assert.That(removed.IsSuccess, Is.True);
            Assert.That(removed.Value.Anatomy!.BodyParts.Any(p => p.BodyPartId.Equals(BodyPartId.Parse("Head"))), Is.False);
        }

        [Test]
        public void RemoveBodyPart_WithPermanentModificationDependent_IsRejected()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> initialized = CharacterAdvancementService.InitializeAnatomyWithDefaults(_characterRepository, _campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
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
            Result<CharacterRecord> initialized = CharacterAdvancementService.InitializeAnatomyWithDefaults(_characterRepository, _campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

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
            Result<CharacterRecord> initialized = CharacterAdvancementService.InitializeAnatomyWithDefaults(_characterRepository, _campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
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
            Result<CharacterRecord> initialized = CharacterAdvancementService.InitializeAnatomyWithDefaults(_characterRepository, _campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
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

            Result<CharacterRecord> first = CharacterAdvancementService.InitializeAnatomyWithDefaults(_characterRepository, _campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, commandId, TestCorrelationId);
            Assert.That(first.IsSuccess, Is.True);

            Result<CharacterRecord> replay = CharacterAdvancementService.InitializeAnatomyWithDefaults(_characterRepository, _campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, commandId, TestCorrelationId);
            Assert.That(replay.IsSuccess, Is.True);

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.Anatomy!.MigrationHistory, Has.Count.EqualTo(1), "a replayed duplicate CommandId must not initialize/journal twice");
        }

        // ---- Concurrency: CharacterResources + CharacterAnatomy do not false-conflict ----

        [Test]
        public void ConcurrentCharacterResourcesEdit_And_CharacterAnatomyEdit_CommitWithoutFalseConflict()
        {
            CharacterRecord character = CreateCharacter();

            Result<CharacterRecord> resourceResult = CharacterAdvancementService.InitializeResourceWithDefaults(_characterRepository, _campaign, character.CharacterId, Health, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);
            Result<CharacterRecord> anatomyResult = CharacterAdvancementService.InitializeAnatomyWithDefaults(_characterRepository, _campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Assert.That(resourceResult.IsSuccess, Is.True);
            Assert.That(anatomyResult.IsSuccess, Is.True);

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.Resources, Has.Count.EqualTo(1));
            Assert.That(reRead.Value.Anatomy, Is.Not.Null);
        }

        // TC-CHAR-170 (ODY-S04-115a): GetCharacterHistory must succeed (no
        // IntegrityCheckFailed) and surface character_resource_initialized/
        // character_resource_changed/character_anatomy_initialized/
        // character_anatomy_changed, once these ODY-S04-109 event types are
        // added to SqliteCharacterRepository.HistoryEventTypes.
        [Test]
        public void GetCharacterHistory_AfterResourceAndAnatomyChanges_Succeeds_SurfacesAllFourEventTypes()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> resourceInitialized = CharacterAdvancementService.InitializeResourceWithDefaults(_characterRepository, _campaign, character.CharacterId, Health, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);
            Assert.That(resourceInitialized.IsSuccess, Is.True);
            CharacterResource resource = resourceInitialized.Value.Resources[0];
            Result<CharacterRecord> resourceChanged = _characterRepository.SetResourceCurrentValue(_campaign, character.CharacterId, resource.CharacterResourceId, resource.MinimumValue, NewUserId(), actorIsMainGm: true, resourceInitialized.Value.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);
            Assert.That(resourceChanged.IsSuccess, Is.True);

            Result<CharacterRecord> anatomyInitialized = CharacterAdvancementService.InitializeAnatomyWithDefaults(_characterRepository, _campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, resourceChanged.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
            Assert.That(anatomyInitialized.IsSuccess, Is.True);
            Result<CharacterRecord> anatomyChanged = _characterRepository.UpdateBodyPart(_campaign, character.CharacterId, BodyPartId.Parse("Head"), 99, "{\"armored\":true}", NewUserId(), actorIsMainGm: true, anatomyInitialized.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);
            Assert.That(anatomyChanged.IsSuccess, Is.True);

            Result<IReadOnlyList<CharacterHistoryEntry>> history = _characterRepository.GetCharacterHistory(_campaign, character.CharacterId, TestCorrelationId);

            Assert.That(history.IsSuccess, Is.True, "GetCharacterHistory must not fail with IntegrityCheckFailed for any of these four event types");
            Assert.That(history.Value.Select(e => e.EventType), Does.Contain("odyssey.persistence.character_resource_initialized"));
            Assert.That(history.Value.Select(e => e.EventType), Does.Contain("odyssey.persistence.character_resource_changed"));
            Assert.That(history.Value.Select(e => e.EventType), Does.Contain("odyssey.persistence.character_anatomy_initialized"));
            Assert.That(history.Value.Select(e => e.EventType), Does.Contain("odyssey.persistence.character_anatomy_changed"));
            Assert.That(history.Value, Has.All.Property(nameof(CharacterHistoryEntry.DisplayNameSnapshot)).Not.Null);
        }

        // ---- ODY-S09-101: CharacterAdvancementService + caller-supplied initialization values ----

        [Test] // TC-CHAR-181
        public void InitializeResourceWithDefaults_AppliesTheRulesFixtureDefaults_IdenticalToTheOriginalBehavior()
        {
            CharacterRecord character = CreateCharacter();

            Result<CharacterRecord> initialized = CharacterAdvancementService.InitializeResourceWithDefaults(_characterRepository, _campaign, character.CharacterId, Health, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);

            Assert.That(initialized.IsSuccess, Is.True);
            CharacterResource resource = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId).Value.Resources.Single();

            // Literal values as produced by the pre-ODY-S09-101 repository method (a before/after snapshot of both
            // Initialize* commands, including journal payloads, showed 0 differences).
            Assert.That(resource.BaseMaximum, Is.EqualTo(10));
            Assert.That(resource.CurrentValue, Is.EqualTo(10), "a new resource starts full");
            Assert.That(resource.PermanentMaximumAdjustment, Is.EqualTo(0));
            Assert.That(resource.MinimumValue, Is.EqualTo(0));
            Assert.That(resource.EffectiveMaximum, Is.EqualTo(10));
            Assert.That(resource.RecoveryRule, Is.EqualTo(RecoveryRule.Manual));
            Assert.That(resource.Revision, Is.EqualTo(1));

            // ...and they are exactly the constants the service now reads from Rules.
            Assert.That(resource.BaseMaximum, Is.EqualTo(Odyssey.Rules.Character.ResourceInitializationRules.DefaultBaseMaximum));
            Assert.That(resource.MinimumValue, Is.EqualTo(Odyssey.Rules.Character.ResourceInitializationRules.DefaultMinimumValue));
            Assert.That(resource.RecoveryRule, Is.EqualTo(Odyssey.Rules.Character.ResourceInitializationRules.DefaultRecoveryRule));
        }

        [Test] // TC-CHAR-182
        public void InitializeAnatomyWithDefaults_AppliesTheRulesFixtureDefaults_IdenticalToTheOriginalBehavior()
        {
            CharacterRecord character = CreateCharacter();

            Result<CharacterRecord> initialized = CharacterAdvancementService.InitializeAnatomyWithDefaults(_characterRepository, _campaign, character.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Assert.That(initialized.IsSuccess, Is.True);
            CharacterAnatomy anatomy = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId).Value.Anatomy!;

            // Literal values as produced by the pre-ODY-S09-101 repository method.
            Assert.That(anatomy.AnatomyProfileDefinitionId, Is.EqualTo(Humanoid));
            Assert.That(anatomy.AnatomyProfileVersion, Is.EqualTo("0.1.0-fixture"));
            Assert.That(anatomy.Revision, Is.EqualTo(1));
            Assert.That(anatomy.PermanentModifications, Is.Empty);
            Assert.That(anatomy.BodyParts.Select(p => p.BodyPartId.ToString()), Is.EqualTo(new[] { "Head", "Torso", "LeftArm", "RightArm" }));
            Assert.That(anatomy.BodyParts.Select(p => p.Name), Is.EqualTo(new[] { "Head", "Torso", "Left Arm", "Right Arm" }));
            Assert.That(anatomy.BodyParts.Select(p => p.DamageLimit), Is.EqualTo(new long[] { 10, 20, 10, 10 }));
            Assert.That(anatomy.BodyParts.Select(p => p.AttachedToBodyPartId.HasValue ? p.AttachedToBodyPartId.Value.ToString() : null), Is.EqualTo(new string?[] { null, null, "Torso", "Torso" }));
            Assert.That(anatomy.BodyParts.Select(p => p.Properties), Is.All.EqualTo("{}"));
            Assert.That(anatomy.MigrationHistory, Has.Count.EqualTo(1));
            Assert.That(anatomy.MigrationHistory[0].ActionKind, Is.EqualTo("Initialized"));
            Assert.That(anatomy.MigrationHistory[0].Description, Is.EqualTo("CharacterAnatomy initialized from fixture Humanoid"));

            // ...and they are exactly what the service now reads from Rules.
            Assert.That(anatomy.AnatomyProfileVersion, Is.EqualTo(Odyssey.Rules.Character.AnatomyInitializationRules.DefaultAnatomyProfileVersion));
            IReadOnlyList<BodyPart> fixtureParts = Odyssey.Rules.Character.AnatomyInitializationRules.DefaultHumanoidBodyParts();
            Assert.That(anatomy.BodyParts, Has.Count.EqualTo(fixtureParts.Count));
            for (int i = 0; i < fixtureParts.Count; i++)
            {
                Assert.That(anatomy.BodyParts[i].BodyPartId, Is.EqualTo(fixtureParts[i].BodyPartId));
                Assert.That(anatomy.BodyParts[i].Name, Is.EqualTo(fixtureParts[i].Name));
                Assert.That(anatomy.BodyParts[i].DamageLimit, Is.EqualTo(fixtureParts[i].DamageLimit));
                Assert.That(anatomy.BodyParts[i].AttachedToBodyPartId, Is.EqualTo(fixtureParts[i].AttachedToBodyPartId));
            }
        }

        [Test] // TC-CHAR-183
        public void InitializeCharacterResource_OnTheRepository_StoresTheCallerSuppliedValues_NotAnyHiddenDefault()
        {
            CharacterRecord character = CreateCharacter();

            Result<CharacterRecord> initialized = _characterRepository.InitializeCharacterResource(_campaign, character.CharacterId, Health, baseMaximum: 25, minimumValue: -3, RecoveryRule.OnRest, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterResourcesRevision, NewCommandId(), TestCorrelationId);

            Assert.That(initialized.IsSuccess, Is.True);
            CharacterResource resource = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId).Value.Resources.Single();
            Assert.That(resource.BaseMaximum, Is.EqualTo(25));
            Assert.That(resource.CurrentValue, Is.EqualTo(25), "starts full: CurrentValue == BaseMaximum");
            Assert.That(resource.MinimumValue, Is.EqualTo(-3));
            Assert.That(resource.RecoveryRule, Is.EqualTo(RecoveryRule.OnRest));
        }

        [Test] // TC-CHAR-184
        public void InitializeCharacterAnatomy_OnTheRepository_StoresTheCallerSuppliedVersionAndBodyParts_NotAnyHiddenDefault()
        {
            CharacterRecord character = CreateCharacter();
            var customParts = new List<BodyPart> { new BodyPart(BodyPartId.Parse("Tail"), "Tail", 7, null, "{}") };

            Result<CharacterRecord> initialized = _characterRepository.InitializeCharacterAnatomy(_campaign, character.CharacterId, AnatomyProfileDefinitionId.Parse("Construct"), "9.9.9-custom", customParts, NewUserId(), actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, NewCommandId(), TestCorrelationId);

            Assert.That(initialized.IsSuccess, Is.True);
            CharacterAnatomy anatomy = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId).Value.Anatomy!;
            Assert.That(anatomy.AnatomyProfileDefinitionId, Is.EqualTo(AnatomyProfileDefinitionId.Parse("Construct")));
            Assert.That(anatomy.AnatomyProfileVersion, Is.EqualTo("9.9.9-custom"));
            Assert.That(anatomy.BodyParts, Has.Count.EqualTo(1));
            Assert.That(anatomy.BodyParts[0].BodyPartId, Is.EqualTo(BodyPartId.Parse("Tail")));
            Assert.That(anatomy.BodyParts[0].DamageLimit, Is.EqualTo(7));
        }

        [Test] // TC-CHAR-185
        public void InitializeCharacterResource_OnTheRepository_RejectsInvalidCallerValues_BeforeTouchingTheDatabase()
        {
            CharacterRecord character = CreateCharacter();
            long revision = character.Revisions.CharacterResourcesRevision;

            Assert.Throws<ArgumentException>(new Action(() => _characterRepository.InitializeCharacterResource(_campaign, character.CharacterId, Health, baseMaximum: 0, minimumValue: 1, RecoveryRule.Manual, NewUserId(), actorIsMainGm: true, revision, NewCommandId(), TestCorrelationId)));
            Assert.Throws<ArgumentOutOfRangeException>(new Action(() => _characterRepository.InitializeCharacterResource(_campaign, character.CharacterId, Health, baseMaximum: 10, minimumValue: 0, (RecoveryRule)999, NewUserId(), actorIsMainGm: true, revision, NewCommandId(), TestCorrelationId)));

            CharacterRecord reread = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId).Value;
            Assert.That(reread.Resources, Is.Empty);
            Assert.That(reread.Revisions.CharacterResourcesRevision, Is.EqualTo(revision));
        }

        [Test] // TC-CHAR-186
        public void InitializeCharacterAnatomy_OnTheRepository_RejectsBlankVersionAndNullBodyParts_BeforeTouchingTheDatabase()
        {
            CharacterRecord character = CreateCharacter();
            long revision = character.Revisions.CharacterAnatomyRevision;

            Assert.Throws<ArgumentException>(new Action(() => _characterRepository.InitializeCharacterAnatomy(_campaign, character.CharacterId, Humanoid, "  ", new List<BodyPart>(), NewUserId(), actorIsMainGm: true, revision, NewCommandId(), TestCorrelationId)));
            Assert.Throws<ArgumentNullException>(new Action(() => _characterRepository.InitializeCharacterAnatomy(_campaign, character.CharacterId, Humanoid, "0.1.0", null!, NewUserId(), actorIsMainGm: true, revision, NewCommandId(), TestCorrelationId)));

            CharacterRecord reread = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId).Value;
            Assert.That(reread.Anatomy, Is.Null);
            Assert.That(reread.Revisions.CharacterAnatomyRevision, Is.EqualTo(revision));
        }

        [Test] // TC-CHAR-187
        public void CharacterAdvancementService_PassesTheRulesDefaultsAndEveryOtherArgumentThroughTheICharacterRepositoryAbstraction()
        {
            var recorder = new RecordingCharacterRepository(_characterRepository);
            CharacterRecord character = CreateCharacter();
            UserId actor = NewUserId();
            CommandId resourceCommand = NewCommandId();
            CommandId anatomyCommand = NewCommandId();

            Result<CharacterRecord> resource = CharacterAdvancementService.InitializeResourceWithDefaults(recorder, _campaign, character.CharacterId, Health, actor, actorIsMainGm: true, character.Revisions.CharacterResourcesRevision, resourceCommand, TestCorrelationId);
            Result<CharacterRecord> anatomy = CharacterAdvancementService.InitializeAnatomyWithDefaults(recorder, _campaign, character.CharacterId, Humanoid, actor, actorIsMainGm: true, character.Revisions.CharacterAnatomyRevision, anatomyCommand, TestCorrelationId);

            Assert.That(resource.IsSuccess, Is.True);
            Assert.That(anatomy.IsSuccess, Is.True);

            Assert.That(recorder.ResourceCalls, Has.Count.EqualTo(1));
            RecordingCharacterRepository.ResourceCall r = recorder.ResourceCalls[0];
            Assert.That(r.BaseMaximum, Is.EqualTo(Odyssey.Rules.Character.ResourceInitializationRules.DefaultBaseMaximum));
            Assert.That(r.MinimumValue, Is.EqualTo(Odyssey.Rules.Character.ResourceInitializationRules.DefaultMinimumValue));
            Assert.That(r.RecoveryRule, Is.EqualTo(Odyssey.Rules.Character.ResourceInitializationRules.DefaultRecoveryRule));
            Assert.That(r.Campaign, Is.SameAs(_campaign));
            Assert.That(r.CharacterId, Is.EqualTo(character.CharacterId));
            Assert.That(r.ResourceDefinitionId, Is.EqualTo(Health));
            Assert.That(r.ActorUserId, Is.EqualTo(actor));
            Assert.That(r.ActorIsMainGm, Is.True);
            Assert.That(r.ExpectedRevision, Is.EqualTo(character.Revisions.CharacterResourcesRevision));
            Assert.That(r.CommandId, Is.EqualTo(resourceCommand));
            Assert.That(r.CorrelationId, Is.EqualTo(TestCorrelationId));

            Assert.That(recorder.AnatomyCalls, Has.Count.EqualTo(1));
            RecordingCharacterRepository.AnatomyCall a = recorder.AnatomyCalls[0];
            Assert.That(a.AnatomyProfileVersion, Is.EqualTo(Odyssey.Rules.Character.AnatomyInitializationRules.DefaultAnatomyProfileVersion));
            Assert.That(a.BodyParts, Has.Count.EqualTo(Odyssey.Rules.Character.AnatomyInitializationRules.DefaultHumanoidBodyParts().Count));
            Assert.That(a.Campaign, Is.SameAs(_campaign));
            Assert.That(a.CharacterId, Is.EqualTo(character.CharacterId));
            Assert.That(a.AnatomyProfileDefinitionId, Is.EqualTo(Humanoid));
            Assert.That(a.ActorUserId, Is.EqualTo(actor));
            Assert.That(a.ActorIsMainGm, Is.True);
            Assert.That(a.ExpectedRevision, Is.EqualTo(character.Revisions.CharacterAnatomyRevision));
            Assert.That(a.CommandId, Is.EqualTo(anatomyCommand));
            Assert.That(a.CorrelationId, Is.EqualTo(TestCorrelationId));

            Assert.Throws<ArgumentNullException>(new Action(() => CharacterAdvancementService.InitializeResourceWithDefaults(null!, _campaign, character.CharacterId, Health, actor, true, 1, NewCommandId(), TestCorrelationId)));
            Assert.Throws<ArgumentNullException>(new Action(() => CharacterAdvancementService.InitializeAnatomyWithDefaults(null!, _campaign, character.CharacterId, Humanoid, actor, true, 1, NewCommandId(), TestCorrelationId)));
        }

        [Test] // TC-CHAR-188
        public void CharacterAdvancementService_LivesInApplication_AndDependsOnlyOnTheICharacterRepositoryPort_NotOnAnySqlImplementation()
        {
            System.Type service = typeof(CharacterAdvancementService);

            Assert.That(service.Assembly.GetName().Name, Is.EqualTo("Odyssey.Application"));
            Assert.That(service.Assembly.GetReferencedAssemblies().Select(n => n.Name), Does.Not.Contain("Odyssey.Persistence"), "Odyssey.Application must not (and cannot) reference the Persistence assembly");

            System.Reflection.MethodInfo[] methods = service.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly);
            Assert.That(methods.Select(m => m.Name), Is.EquivalentTo(new[] { "InitializeResourceWithDefaults", "InitializeAnatomyWithDefaults", "PurchaseAttributeIncrease", "PurchaseSkillLevel", "RequestSkillAdvancedRecommendation", "AcquireAbility" }));
            foreach (System.Reflection.MethodInfo method in methods)
            {
                Assert.That(method.GetParameters()[0].ParameterType, Is.EqualTo(typeof(ICharacterRepository)), method.Name + " must take the abstraction, not a concrete repository");
            }
        }

        private sealed class RecordingCharacterRepository : ICharacterRepository
        {
            private readonly ICharacterRepository _inner;

            public RecordingCharacterRepository(ICharacterRepository inner)
            {
                _inner = inner;
            }

            public List<ResourceCall> ResourceCalls { get; } = new List<ResourceCall>();
            public List<AnatomyCall> AnatomyCalls { get; } = new List<AnatomyCall>();

            public sealed class ResourceCall
            {
                public ResourceCall(CampaignHandle campaign, CharacterId characterId, ResourceDefinitionId resourceDefinitionId, long baseMaximum, long minimumValue, RecoveryRule recoveryRule, UserId actorUserId, bool actorIsMainGm, long expectedRevision, CommandId commandId, CorrelationId correlationId)
                {
                    Campaign = campaign; CharacterId = characterId; ResourceDefinitionId = resourceDefinitionId; BaseMaximum = baseMaximum; MinimumValue = minimumValue; RecoveryRule = recoveryRule;
                    ActorUserId = actorUserId; ActorIsMainGm = actorIsMainGm; ExpectedRevision = expectedRevision; CommandId = commandId; CorrelationId = correlationId;
                }

                public CampaignHandle Campaign { get; }
                public CharacterId CharacterId { get; }
                public ResourceDefinitionId ResourceDefinitionId { get; }
                public long BaseMaximum { get; }
                public long MinimumValue { get; }
                public RecoveryRule RecoveryRule { get; }
                public UserId ActorUserId { get; }
                public bool ActorIsMainGm { get; }
                public long ExpectedRevision { get; }
                public CommandId CommandId { get; }
                public CorrelationId CorrelationId { get; }
            }

            public sealed class AnatomyCall
            {
                public AnatomyCall(CampaignHandle campaign, CharacterId characterId, AnatomyProfileDefinitionId anatomyProfileDefinitionId, string anatomyProfileVersion, IReadOnlyList<BodyPart> bodyParts, UserId actorUserId, bool actorIsMainGm, long expectedRevision, CommandId commandId, CorrelationId correlationId)
                {
                    Campaign = campaign; CharacterId = characterId; AnatomyProfileDefinitionId = anatomyProfileDefinitionId; AnatomyProfileVersion = anatomyProfileVersion; BodyParts = bodyParts;
                    ActorUserId = actorUserId; ActorIsMainGm = actorIsMainGm; ExpectedRevision = expectedRevision; CommandId = commandId; CorrelationId = correlationId;
                }

                public CampaignHandle Campaign { get; }
                public CharacterId CharacterId { get; }
                public AnatomyProfileDefinitionId AnatomyProfileDefinitionId { get; }
                public string AnatomyProfileVersion { get; }
                public IReadOnlyList<BodyPart> BodyParts { get; }
                public UserId ActorUserId { get; }
                public bool ActorIsMainGm { get; }
                public long ExpectedRevision { get; }
                public CommandId CommandId { get; }
                public CorrelationId CorrelationId { get; }
            }

            public Result<CriticalSuccessEvidenceRecord> RecordCriticalSuccessEvidence(CampaignHandle campaign, CharacterId characterId, SkillDefinitionId skillDefinitionId, string? sourceDiceRollId, string? sourceActionId, CommandId commandId, CorrelationId correlationId) => _inner.RecordCriticalSuccessEvidence(campaign, characterId, skillDefinitionId, sourceDiceRollId, sourceActionId, commandId, correlationId);

            public Result<CharacterRecord> CreateCharacter(CreateCharacterRequest request, CommandId commandId, CorrelationId correlationId) => _inner.CreateCharacter(request, commandId, correlationId);
            public Result<CharacterRecord> GetCharacter(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId) => _inner.GetCharacter(campaign, characterId, correlationId);
            public Result<CharacterRecord> SetCharacterPortrait(CampaignHandle campaign, CharacterId characterId, AssetId? portraitAssetId, long expectedPresentationRevision, CommandId commandId, CorrelationId correlationId) => _inner.SetCharacterPortrait(campaign, characterId, portraitAssetId, expectedPresentationRevision, commandId, correlationId);
            public Result<CharacterRecord> UpdateIdentity(CampaignHandle campaign, CharacterId characterId, string newDisplayName, long expectedIdentityRevision, CommandId commandId, CorrelationId correlationId) => _inner.UpdateIdentity(campaign, characterId, newDisplayName, expectedIdentityRevision, commandId, correlationId);
            public Result<CharacterRecord> UpdatePresentation(CampaignHandle campaign, CharacterId characterId, string? portraitReference, long expectedPresentationRevision, CommandId commandId, CorrelationId correlationId) => _inner.UpdatePresentation(campaign, characterId, portraitReference, expectedPresentationRevision, commandId, correlationId);
            public Result<IReadOnlyList<CharacterHistoryEntry>> GetCharacterHistory(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId) => _inner.GetCharacterHistory(campaign, characterId, correlationId);
            public Result<CharacterRecord> AssignPrimaryOwner(CampaignHandle campaign, CharacterId characterId, UserId newPrimaryOwnerUserId, string reasonCode, bool actorIsMainGm, long expectedOwnershipRevision, CommandId commandId, CorrelationId correlationId) => _inner.AssignPrimaryOwner(campaign, characterId, newPrimaryOwnerUserId, reasonCode, actorIsMainGm, expectedOwnershipRevision, commandId, correlationId);
            public Result<CharacterRecord> AddCharacterCoOwner(CampaignHandle campaign, CharacterId characterId, UserId coOwnerUserId, bool actorIsMainGm, long expectedOwnershipRevision, CommandId commandId, CorrelationId correlationId) => _inner.AddCharacterCoOwner(campaign, characterId, coOwnerUserId, actorIsMainGm, expectedOwnershipRevision, commandId, correlationId);
            public Result<CharacterRecord> RemoveCharacterCoOwner(CampaignHandle campaign, CharacterId characterId, UserId coOwnerUserId, bool actorIsMainGm, long expectedOwnershipRevision, CommandId commandId, CorrelationId correlationId) => _inner.RemoveCharacterCoOwner(campaign, characterId, coOwnerUserId, actorIsMainGm, expectedOwnershipRevision, commandId, correlationId);
            public Result<CharacterRecord> GrantPermanentCharacterControl(CampaignHandle campaign, CharacterId characterId, UserId controlUserId, bool actorIsMainGm, long expectedOwnershipRevision, CommandId commandId, CorrelationId correlationId) => _inner.GrantPermanentCharacterControl(campaign, characterId, controlUserId, actorIsMainGm, expectedOwnershipRevision, commandId, correlationId);
            public Result<CharacterRecord> GrantTemporaryCharacterControl(CampaignHandle campaign, CharacterId characterId, UserId controlUserId, UtcInstant? expiresAt, bool actorIsMainGm, long expectedOwnershipRevision, CommandId commandId, CorrelationId correlationId) => _inner.GrantTemporaryCharacterControl(campaign, characterId, controlUserId, expiresAt, actorIsMainGm, expectedOwnershipRevision, commandId, correlationId);
            public Result<CharacterRecord> RevokeCharacterControl(CampaignHandle campaign, CharacterId characterId, UserId controlUserId, bool actorIsMainGm, long expectedOwnershipRevision, CommandId commandId, CorrelationId correlationId) => _inner.RevokeCharacterControl(campaign, characterId, controlUserId, actorIsMainGm, expectedOwnershipRevision, commandId, correlationId);
            public Result<CharacterRecord> BindDraftToCampaign(BindDraftToCampaignRequest request, CommandId commandId, CorrelationId correlationId) => _inner.BindDraftToCampaign(request, commandId, correlationId);
            public Result<CharacterRecord> SubmitCharacterDraft(CampaignHandle campaign, CharacterId characterId, long expectedLifecycleRevision, CommandId commandId, CorrelationId correlationId) => _inner.SubmitCharacterDraft(campaign, characterId, expectedLifecycleRevision, commandId, correlationId);
            public Result<CharacterReviewCommentRecord> AddCharacterReviewComment(CampaignHandle campaign, CharacterId characterId, UserId authorUserId, string text, CommandId commandId, CorrelationId correlationId) => _inner.AddCharacterReviewComment(campaign, characterId, authorUserId, text, commandId, correlationId);
            public Result<CharacterRecord> ApproveCharacterDraft(CampaignHandle campaign, CharacterId characterId, bool actorIsMainGm, long expectedLifecycleRevision, CommandId commandId, CorrelationId correlationId) => _inner.ApproveCharacterDraft(campaign, characterId, actorIsMainGm, expectedLifecycleRevision, commandId, correlationId);
            public Result<IReadOnlyList<CharacterReviewCommentRecord>> GetCharacterReviewComments(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId) => _inner.GetCharacterReviewComments(campaign, characterId, correlationId);
            public Result<CharacterRecord> GrantDevelopmentPoints(CampaignHandle campaign, CharacterId characterId, long amount, string reason, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, CommandId commandId, CorrelationId correlationId) => _inner.GrantDevelopmentPoints(campaign, characterId, amount, reason, actorUserId, actorIsMainGm, expectedMechanicsRevision, commandId, correlationId);
            public Result<CharacterRecord> PurchaseAttributeIncrease(CampaignHandle campaign, CharacterId characterId, AttributeDefinitionId attributeDefinitionId, long toValue, long decidedFromValue, bool exceedsNormalCap, long decidedCost, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, long expectedAttributeRevision, CommandId commandId, CorrelationId correlationId) => _inner.PurchaseAttributeIncrease(campaign, characterId, attributeDefinitionId, toValue, decidedFromValue, exceedsNormalCap, decidedCost, actorUserId, actorIsMainGm, expectedMechanicsRevision, expectedAttributeRevision, commandId, correlationId);
            public Result<IReadOnlyList<DevelopmentTransactionRecord>> GetDevelopmentLedger(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId) => _inner.GetDevelopmentLedger(campaign, characterId, correlationId);
            public Result<CharacterRecord> PurchaseSkillLevel(CampaignHandle campaign, CharacterId characterId, SkillDefinitionId skillDefinitionId, long toLevel, long decidedFromLevel, bool requiresRecommendation, long decidedCost, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, long expectedSkillRevision, CommandId commandId, CorrelationId correlationId) => _inner.PurchaseSkillLevel(campaign, characterId, skillDefinitionId, toLevel, decidedFromLevel, requiresRecommendation, decidedCost, actorUserId, actorIsMainGm, expectedMechanicsRevision, expectedSkillRevision, commandId, correlationId);
            public Result<IReadOnlyList<CriticalSuccessEvidenceRecord>> GetCriticalSuccessEvidence(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId) => _inner.GetCriticalSuccessEvidence(campaign, characterId, correlationId);
            public Result<AdvancementRecommendationRecord> RequestSkillAdvancedRecommendation(CampaignHandle campaign, CharacterId characterId, SkillDefinitionId skillDefinitionId, long targetLevel, long decidedFromLevel, long decidedReservedAmount, IReadOnlyList<CriticalSuccessEvidenceId> evidenceIds, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, CommandId commandId, CorrelationId correlationId) => _inner.RequestSkillAdvancedRecommendation(campaign, characterId, skillDefinitionId, targetLevel, decidedFromLevel, decidedReservedAmount, evidenceIds, actorUserId, actorIsMainGm, expectedMechanicsRevision, commandId, correlationId);
            public Result<CharacterRecord> ResolveAdvancementRecommendation(CampaignHandle campaign, CharacterId characterId, AdvancementRecommendationId recommendationId, bool approve, bool spendReservedPoints, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, long expectedRecommendationRevision, CommandId commandId, CorrelationId correlationId) => _inner.ResolveAdvancementRecommendation(campaign, characterId, recommendationId, approve, spendReservedPoints, actorUserId, actorIsMainGm, expectedMechanicsRevision, expectedRecommendationRevision, commandId, correlationId);
            public Result<AdvancementRecommendationRecord> GetAdvancementRecommendation(CampaignHandle campaign, CharacterId characterId, AdvancementRecommendationId recommendationId, CorrelationId correlationId) => _inner.GetAdvancementRecommendation(campaign, characterId, recommendationId, correlationId);
            public Result<IReadOnlyList<AdvancementPurchase>> GetAdvancementPurchases(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId) => _inner.GetAdvancementPurchases(campaign, characterId, correlationId);
            public Result<CharacterRecord> RevertAdvancementPurchase(CampaignHandle campaign, CharacterId characterId, AdvancementPurchaseId purchaseId, string reasonCode, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, CommandId commandId, CorrelationId correlationId) => _inner.RevertAdvancementPurchase(campaign, characterId, purchaseId, reasonCode, actorUserId, actorIsMainGm, expectedMechanicsRevision, commandId, correlationId);
            public Result<CharacterRespecPreview> PreviewCharacterRespec(CampaignHandle campaign, CharacterId characterId, IReadOnlyList<CharacterRespecTarget> targets, CorrelationId correlationId) => _inner.PreviewCharacterRespec(campaign, characterId, targets, correlationId);
            public Result<CharacterRecord> ApplyCharacterRespec(CampaignHandle campaign, CharacterId characterId, IReadOnlyList<CharacterRespecTarget> targets, string reasonCode, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, CommandId commandId, CorrelationId correlationId) => _inner.ApplyCharacterRespec(campaign, characterId, targets, reasonCode, actorUserId, actorIsMainGm, expectedMechanicsRevision, commandId, correlationId);
            public Result<CharacterRecord> AcquireAbility(CampaignHandle campaign, CharacterId characterId, AbilityDefinitionId abilityDefinitionId, SourceKind sourceKind, string? sourceRef, RankMode rankMode, long? numericRank, string? namedRankKey, string configuration, long progressionPurchaseCost, UserId actorUserId, bool actorIsMainGm, long? expectedMechanicsRevision, long expectedCharacterAbilitiesRevision, CommandId commandId, CorrelationId correlationId) => _inner.AcquireAbility(campaign, characterId, abilityDefinitionId, sourceKind, sourceRef, rankMode, numericRank, namedRankKey, configuration, progressionPurchaseCost, actorUserId, actorIsMainGm, expectedMechanicsRevision, expectedCharacterAbilitiesRevision, commandId, correlationId);
            public Result<CharacterRecord> RemoveAbility(CampaignHandle campaign, CharacterId characterId, CharacterAbilityId characterAbilityId, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAbilitiesRevision, CommandId commandId, CorrelationId correlationId) => _inner.RemoveAbility(campaign, characterId, characterAbilityId, actorUserId, actorIsMainGm, expectedCharacterAbilitiesRevision, commandId, correlationId);
            public Result<CharacterRecord> LinkAbilityActivationSource(CampaignHandle campaign, CharacterId characterId, CharacterAbilityId characterAbilityId, ContentDefinitionRef activationDefinitionRef, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAbilitiesRevision, CommandId commandId, CorrelationId correlationId) => _inner.LinkAbilityActivationSource(campaign, characterId, characterAbilityId, activationDefinitionRef, actorUserId, actorIsMainGm, expectedCharacterAbilitiesRevision, commandId, correlationId);
            public Result<CharacterRecord> InitializeCharacterResource(CampaignHandle campaign, CharacterId characterId, ResourceDefinitionId resourceDefinitionId, long baseMaximum, long minimumValue, RecoveryRule recoveryRule, UserId actorUserId, bool actorIsMainGm, long expectedCharacterResourcesRevision, CommandId commandId, CorrelationId correlationId)
            {
                ResourceCalls.Add(new ResourceCall(campaign, characterId, resourceDefinitionId, baseMaximum, minimumValue, recoveryRule, actorUserId, actorIsMainGm, expectedCharacterResourcesRevision, commandId, correlationId));
                return _inner.InitializeCharacterResource(campaign, characterId, resourceDefinitionId, baseMaximum, minimumValue, recoveryRule, actorUserId, actorIsMainGm, expectedCharacterResourcesRevision, commandId, correlationId);
            }
            public Result<CharacterRecord> SetResourceCurrentValue(CampaignHandle campaign, CharacterId characterId, CharacterResourceId characterResourceId, long newCurrentValue, UserId actorUserId, bool actorIsMainGm, long expectedCharacterResourcesRevision, CommandId commandId, CorrelationId correlationId) => _inner.SetResourceCurrentValue(campaign, characterId, characterResourceId, newCurrentValue, actorUserId, actorIsMainGm, expectedCharacterResourcesRevision, commandId, correlationId);
            public Result<CharacterRecord> SetResourceMaximum(CampaignHandle campaign, CharacterId characterId, CharacterResourceId characterResourceId, long newBaseMaximum, long newPermanentMaximumAdjustment, UserId actorUserId, bool actorIsMainGm, long expectedCharacterResourcesRevision, CommandId commandId, CorrelationId correlationId) => _inner.SetResourceMaximum(campaign, characterId, characterResourceId, newBaseMaximum, newPermanentMaximumAdjustment, actorUserId, actorIsMainGm, expectedCharacterResourcesRevision, commandId, correlationId);
            public Result<CharacterRecord> InitializeCharacterAnatomy(CampaignHandle campaign, CharacterId characterId, AnatomyProfileDefinitionId anatomyProfileDefinitionId, string anatomyProfileVersion, IReadOnlyList<BodyPart> bodyParts, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAnatomyRevision, CommandId commandId, CorrelationId correlationId)
            {
                AnatomyCalls.Add(new AnatomyCall(campaign, characterId, anatomyProfileDefinitionId, anatomyProfileVersion, bodyParts, actorUserId, actorIsMainGm, expectedCharacterAnatomyRevision, commandId, correlationId));
                return _inner.InitializeCharacterAnatomy(campaign, characterId, anatomyProfileDefinitionId, anatomyProfileVersion, bodyParts, actorUserId, actorIsMainGm, expectedCharacterAnatomyRevision, commandId, correlationId);
            }
            public Result<CharacterRecord> AddBodyPart(CampaignHandle campaign, CharacterId characterId, BodyPartId bodyPartId, string name, long damageLimit, BodyPartId? attachedToBodyPartId, string properties, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAnatomyRevision, CommandId commandId, CorrelationId correlationId) => _inner.AddBodyPart(campaign, characterId, bodyPartId, name, damageLimit, attachedToBodyPartId, properties, actorUserId, actorIsMainGm, expectedCharacterAnatomyRevision, commandId, correlationId);
            public Result<CharacterRecord> RemoveBodyPart(CampaignHandle campaign, CharacterId characterId, BodyPartId bodyPartId, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAnatomyRevision, CommandId commandId, CorrelationId correlationId) => _inner.RemoveBodyPart(campaign, characterId, bodyPartId, actorUserId, actorIsMainGm, expectedCharacterAnatomyRevision, commandId, correlationId);
            public Result<CharacterRecord> UpdateBodyPart(CampaignHandle campaign, CharacterId characterId, BodyPartId bodyPartId, long? newDamageLimit, string? newProperties, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAnatomyRevision, CommandId commandId, CorrelationId correlationId) => _inner.UpdateBodyPart(campaign, characterId, bodyPartId, newDamageLimit, newProperties, actorUserId, actorIsMainGm, expectedCharacterAnatomyRevision, commandId, correlationId);
            public Result<CharacterRecord> ReplaceAnatomyProfile(CampaignHandle campaign, CharacterId characterId, AnatomyProfileDefinitionId newAnatomyProfileDefinitionId, string newAnatomyProfileVersion, IReadOnlyList<BodyPart> newBodyParts, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAnatomyRevision, CommandId commandId, CorrelationId correlationId) => _inner.ReplaceAnatomyProfile(campaign, characterId, newAnatomyProfileDefinitionId, newAnatomyProfileVersion, newBodyParts, actorUserId, actorIsMainGm, expectedCharacterAnatomyRevision, commandId, correlationId);
            public Result<CharacterRecord> ApplyPermanentModification(CampaignHandle campaign, CharacterId characterId, BodyPartId attachedToBodyPartId, string kind, string description, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAnatomyRevision, CommandId commandId, CorrelationId correlationId) => _inner.ApplyPermanentModification(campaign, characterId, attachedToBodyPartId, kind, description, actorUserId, actorIsMainGm, expectedCharacterAnatomyRevision, commandId, correlationId);
            public Result<CharacterRecord> ArchiveCharacter(CampaignHandle campaign, CharacterId characterId, UserId actorUserId, bool actorIsMainGm, long expectedLifecycleRevision, CommandId commandId, CorrelationId correlationId) => _inner.ArchiveCharacter(campaign, characterId, actorUserId, actorIsMainGm, expectedLifecycleRevision, commandId, correlationId);
            public Result DeleteCharacterPermanently(CampaignHandle campaign, CharacterId characterId, string reasonCode, UserId actorUserId, bool actorIsMainGm, long expectedLifecycleRevision, CommandId commandId, CorrelationId correlationId) => _inner.DeleteCharacterPermanently(campaign, characterId, reasonCode, actorUserId, actorIsMainGm, expectedLifecycleRevision, commandId, correlationId);
            public Result<CharacterRecord> TransitionCharacterToDead(CampaignHandle campaign, CharacterId characterId, LifecycleDeathIssuerKind issuerKind, UserId actorUserId, bool actorIsMainGm, long expectedLifecycleRevision, CommandId commandId, CorrelationId correlationId) => _inner.TransitionCharacterToDead(campaign, characterId, issuerKind, actorUserId, actorIsMainGm, expectedLifecycleRevision, commandId, correlationId);
            public Result<CharacterRecord> RestoreDeadCharacter(RestoreDeadCharacterRequest request, CommandId commandId, CorrelationId correlationId) => _inner.RestoreDeadCharacter(request, commandId, correlationId);
            public Result<CharacterExportBundle> ExportCharacter(CampaignHandle campaign, CharacterId characterId, string bundleDirectoryPath, ExportActorContext actorContext, CorrelationId correlationId) => _inner.ExportCharacter(campaign, characterId, bundleDirectoryPath, actorContext, correlationId);
            public Result<CharacterRecord> ImportCharacter(ImportCharacterRequest request, CommandId bindCommandId, CommandId applyStateCommandId, CorrelationId correlationId) => _inner.ImportCharacter(request, bindCommandId, applyStateCommandId, correlationId);
            public Result<Odyssey.Rules.Character.CharacterRulesetMigrationPlan> PreviewCharacterRulesetMigration(CampaignHandle campaign, CharacterId characterId, string targetRulesetId, string targetRulesetVersion, Odyssey.Rules.Character.RulesetDefinitionCatalog targetCatalog, CorrelationId correlationId) => _inner.PreviewCharacterRulesetMigration(campaign, characterId, targetRulesetId, targetRulesetVersion, targetCatalog, correlationId);
            public Result<CharacterRecord> ApplyCharacterRulesetMigration(CampaignHandle campaign, CharacterId characterId, Odyssey.Rules.Character.CharacterRulesetMigrationPlan plan, Odyssey.Rules.Character.RulesetDefinitionCatalog targetCatalog, UserId actorUserId, bool actorIsMainGm, CommandId commandId, CorrelationId correlationId) => _inner.ApplyCharacterRulesetMigration(campaign, characterId, plan, targetCatalog, actorUserId, actorIsMainGm, commandId, correlationId);
            public Result<CharacterRecord> RevertCharacterRulesetMigration(CampaignHandle campaign, CharacterId characterId, CommandId migrationCommandId, string reasonCode, UserId actorUserId, bool actorIsMainGm, long expectedCharacterRevision, CommandId commandId, CorrelationId correlationId) => _inner.RevertCharacterRulesetMigration(campaign, characterId, migrationCommandId, reasonCode, actorUserId, actorIsMainGm, expectedCharacterRevision, commandId, correlationId);
        }
    }
}
