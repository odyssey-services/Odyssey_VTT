using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Odyssey.Application.Combat;
using Odyssey.Application.Commands;
using Odyssey.Application.Content;
using Odyssey.Application.Inventory;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;
using Odyssey.Application.Random;
using Odyssey.Rules.Combat;

namespace Odyssey.Tests.Persistence
{
    public sealed class SqliteAttackStateReaderTests
    {
        private static readonly CorrelationId Corr = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private string _root = null!;
        private CampaignHandle _campaign = null!;
        private IWallClock _clock = null!;
        private SqliteCharacterRepository _characters = null!;
        private SqliteCombatEncounterRepository _encounters = null!;
        private SqliteInventoryRepository _inventory = null!;
        private SqliteSceneRepository _scenes = null!;
        private SqliteAttackStateReader _reader = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "ody-s05-603-" + Guid.NewGuid().ToString("N"));
            _clock = new SystemWallClock();
            Result<CampaignHandle> campaign = new SqliteCampaignRepository(_clock).Create(new CreateCampaignRequest(_root, "attack", "ruleset.core", "1.0.0", "0.1.0"), Command(), Corr);
            Assert.That(campaign.IsSuccess, Is.True);
            _campaign = campaign.Value;
            _characters = new SqliteCharacterRepository(_clock);
            _encounters = new SqliteCombatEncounterRepository(_clock);
            _inventory = new SqliteInventoryRepository(_clock);
            _scenes = new SqliteSceneRepository(_clock);
            _reader = new SqliteAttackStateReader(_encounters, _inventory, _characters, _clock, _scenes);
        }

        [Test] // TC-ATTACK-016
        public void Reader_constrains_action_source_to_actor_owned_item_snapshot_and_rejects_non_owned_item()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord actorItem = ItemFor(actor);
            ItemInstanceRecord targetItem = ItemFor(target);

            Result<AttackEvaluationState> ownedRead = _reader.Read(_campaign, Intent(encounter, actor, target, actorItem), Corr);
            Assert.That(ownedRead.IsSuccess, Is.True);
            Assert.That(ownedRead.Value.Snapshot.ActionSourceRef, Is.EqualTo(actorItem.SourceItemDefinitionRef));
            Assert.That(ownedRead.Value.Snapshot.ActionMechanics, Is.EqualTo(actorItem.MechanicsSnapshot));

            Result<AttackEvaluationState> notOwnedRead = _reader.Read(_campaign, Intent(encounter, actor, target, targetItem), Corr);
            Assert.That(notOwnedRead.IsFailure, Is.True);
        }

        [Test] // TC-ATTACK-017
        public void Reader_uses_current_encounter_revision_participants_and_pinned_mechanics_snapshot()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);

            Result<AttackEvaluationState> read = _reader.Read(_campaign, Intent(encounter, actor, target, item), Corr);
            Assert.That(read.IsSuccess, Is.True);
            Assert.That(read.Value.Snapshot.EncounterRevision, Is.EqualTo(encounter.Revision));
            Assert.That(read.Value.Snapshot.Actor.CharacterId, Is.EqualTo(actor));
            Assert.That(read.Value.Snapshot.Targets.Count, Is.EqualTo(1));
            Assert.That(read.Value.Snapshot.Targets[0].CharacterId, Is.EqualTo(target));
            Assert.That(read.Value.Snapshot.ActionMechanics, Is.EqualTo(item.MechanicsSnapshot));

            AttackIntent staleRevision = new AttackIntent(encounter.EncounterId, actor, new[] { target }, item.ItemInstanceId, encounter.Revision + 1);
            Assert.That(_reader.Read(_campaign, staleRevision, Corr).IsFailure, Is.True);

            CharacterId outsider = Active("outsider");
            AttackIntent outsideTarget = new AttackIntent(encounter.EncounterId, actor, new[] { outsider }, item.ItemInstanceId, encounter.Revision);
            Assert.That(_reader.Read(_campaign, outsideTarget, Corr).IsFailure, Is.True);
        }

        [Test] // TC-ATTACK-025
        public void Reader_marks_topology_and_armor_effects_as_explicitly_unavailable_not_a_valid_empty_value()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);

            AttackEvaluationSnapshot snapshot = _reader.Read(_campaign, Intent(encounter, actor, target, item), Corr).Value.Snapshot;
            Assert.That(snapshot.Topology.Availability, Is.EqualTo(AttackTopologyAvailability.UnavailableNotBound));
            Assert.That(snapshot.ArmorAndEffects.Availability, Is.EqualTo(AttackArmorAvailability.UnavailableNotBound));
            Assert.That(snapshot.Topology.Reason, Is.Not.Null.And.Not.Empty);
            Assert.That(snapshot.ArmorAndEffects.Reason, Is.Not.Null.And.Not.Empty);

            // AttackUnavailableInput has exactly one legal availability value (UnavailableNotBound);
            // it exists to make "not bound" an explicit, typed input rather than a null/default that
            // Rules could mistake for a valid empty range/armor/effect binding.
            Assert.That(Throws<ArgumentException>(() => { _ = new AttackUnavailableInput(AttackInputAvailability.Available, "reason"); }), Is.True);
            Assert.That(Throws<ArgumentException>(() => { _ = new AttackUnavailableInput(AttackInputAvailability.UnavailableNotBound, ""); }), Is.True);
        }

        [Test] // TC-ATTACK-023
        public void Fingerprint_changes_when_encounter_revision_item_revision_or_participant_lifecycle_changes()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            string baseline = _reader.Read(_campaign, Intent(encounter, actor, target, item), Corr).Value.Snapshot.Fingerprint;

            // Encounter revision: two Advance calls return current turn to the actor with a higher Revision/RoundOrdinal.
            CombatEncounterRecord afterFirstAdvance = Advance(encounter);
            CombatEncounterRecord afterSecondAdvance = Advance(afterFirstAdvance);
            Assert.That(afterSecondAdvance.CurrentParticipantId, Is.EqualTo(actor));
            Assert.That(afterSecondAdvance.Revision, Is.GreaterThan(encounter.Revision));
            string afterEncounterRevisionChanged = _reader.Read(_campaign, Intent(afterSecondAdvance, actor, target, item), Corr).Value.Snapshot.Fingerprint;
            Assert.That(afterEncounterRevisionChanged, Is.Not.EqualTo(baseline));

            // Item revision: ODY-S06-103's own equip-status gate now requires the action item to stay
            // Equipped for a read to succeed, so a Contained-container Move (which requires the source to
            // BE Contained) can no longer serve this purpose -- unequipping then re-equipping the actor's
            // own item bumps ItemInstanceRecord.Revision the same way, without changing its owner or
            // mechanics snapshot, while leaving it Equipped again at the end.
            ItemInstanceRecord reequippedItem = Requip(item);
            Assert.That(reequippedItem.Revision, Is.GreaterThan(item.Revision));
            string afterItemRevisionChanged = _reader.Read(_campaign, Intent(afterSecondAdvance, actor, target, reequippedItem), Corr).Value.Snapshot.Fingerprint;
            Assert.That(afterItemRevisionChanged, Is.Not.EqualTo(afterEncounterRevisionChanged));

            // Participant lifecycle: transitioning the target to Dead changes its LifecycleStatus without touching the encounter or item.
            TransitionToDead(target);
            string afterTargetDied = _reader.Read(_campaign, Intent(afterSecondAdvance, actor, target, reequippedItem), Corr).Value.Snapshot.Fingerprint;
            Assert.That(afterTargetDied, Is.Not.EqualTo(afterItemRevisionChanged));
        }

        [Test] // TC-ATTACK-024
        public void Reader_rejects_item_not_owned_by_the_acting_character_without_creating_any_row()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord targetOwnedItem = ItemFor(target);
            long before = TotalRowCount();

            Result<AttackEvaluationState> rejected = _reader.Read(_campaign, Intent(encounter, actor, target, targetOwnedItem), Corr);

            Assert.That(rejected.IsFailure, Is.True);
            Assert.That(TotalRowCount(), Is.EqualTo(before));
        }

        [Test] // TC-ATTACK-026
        public void Read_never_creates_a_durable_sqlite_mutation_across_any_table()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            long beforeAnyRead = TotalRowCount();

            // AttackEvaluationService.PreviewAttack/EvaluateAttack touch persistence exclusively
            // through IAttackStateReader.Read/CanControlActor (see AttackEvaluationService.AuthorizeAndRead);
            // exercising the reader directly covers every persistence path both operations use.
            Assert.That(_reader.Read(_campaign, Intent(encounter, actor, target, item), Corr).IsSuccess, Is.True);
            Assert.That(_reader.Read(_campaign, Intent(encounter, actor, target, item), Corr).IsSuccess, Is.True);
            Assert.That(_reader.Read(_campaign, new AttackIntent(encounter.EncounterId, actor, new[] { target }, item.ItemInstanceId, encounter.Revision + 1), Corr).IsFailure, Is.True);
            Assert.That(_reader.CanControlActor(_campaign, actor, User(), Corr).IsSuccess, Is.True);

            Assert.That(TotalRowCount(), Is.EqualTo(beforeAnyRead));
        }

        [Test] // TC-ATTACK-028
        public void CanControlActor_reflects_character_ownership_assignment()
        {
            CharacterId actor = Active("actor");
            UserId owner = User();
            CharacterRecord current = _characters.GetCharacter(_campaign, actor, Corr).Value;
            Result<CharacterRecord> assigned = _characters.AssignPrimaryOwner(_campaign, actor, owner, "test ownership", actorIsMainGm: true, current.Revisions.OwnershipRevision, Command(), Corr);
            Assert.That(assigned.IsSuccess, Is.True);

            Assert.That(_reader.CanControlActor(_campaign, actor, owner, Corr).Value, Is.True);
            Assert.That(_reader.CanControlActor(_campaign, actor, User(), Corr).Value, Is.False);
        }

        // ---- ODY-S06-102: attribute data reaches the attack snapshot -----------------

        [Test] // TC-ATTACK-101
        public void PreviewAttack_CopiesActorAttributeEffectiveValues_FromCharacterRecord_IntoSnapshot()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            GrantAttribute(actor, "Strength", 5);
            GrantAttribute(actor, "Dexterity", 3);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);

            Result<ProposedAttackResolution> preview = AttackEvaluationService.PreviewAttack(_reader, new RecordingRules(), _campaign, ServiceRequest(encounter, actor, target, item));
            Assert.That(preview.IsSuccess, Is.True);
            AttackParticipantState snapshotActor = preview.Value.Snapshot.Actor;
            Assert.That(snapshotActor.AttributeValues[AttributeDefinitionId.Parse("Strength")], Is.EqualTo(5));
            Assert.That(snapshotActor.AttributeValues[AttributeDefinitionId.Parse("Dexterity")], Is.EqualTo(3));
        }

        [Test] // TC-ATTACK-102
        public void PreviewAttack_CopiesTargetAttributeEffectiveValues_FromCharacterRecord_IntoSnapshot()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            GrantAttribute(target, "Constitution", 7);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);

            Result<ProposedAttackResolution> preview = AttackEvaluationService.PreviewAttack(_reader, new RecordingRules(), _campaign, ServiceRequest(encounter, actor, target, item));
            Assert.That(preview.IsSuccess, Is.True);
            Assert.That(preview.Value.Snapshot.Targets[0].AttributeValues[AttributeDefinitionId.Parse("Constitution")], Is.EqualTo(7));
        }

        [Test] // TC-ATTACK-103
        public void PreviewAttack_CharacterWithNoPurchasedAttributes_HasEmptyAttributeValues_NotACrash()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);

            Result<ProposedAttackResolution> preview = AttackEvaluationService.PreviewAttack(_reader, new RecordingRules(), _campaign, ServiceRequest(encounter, actor, target, item));
            Assert.That(preview.IsSuccess, Is.True);
            Assert.That(preview.Value.Snapshot.Actor.AttributeValues, Is.Empty);
            Assert.That(preview.Value.Snapshot.Targets[0].AttributeValues, Is.Empty);
        }

        [Test] // TC-ATTACK-104
        public void EvaluateAttack_AlsoCopiesAttributeValues_ThroughTheRandomizedPath()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            GrantAttribute(actor, "Strength", 9);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            var random = new DeterministicRandomStreamFactory(CampaignRngKey.FromBytes(new byte[32]));

            Result<ProposedAttackResolution> evaluated = AttackEvaluationService.EvaluateAttack(_reader, new RecordingRules(), random, _campaign, RngKeyEpochId.Parse("epoch-001"), ServiceRequest(encounter, actor, target, item));
            Assert.That(evaluated.IsSuccess, Is.True);
            Assert.That(evaluated.Value.Snapshot.Actor.AttributeValues[AttributeDefinitionId.Parse("Strength")], Is.EqualTo(9));
        }

        // ---- ODY-S06-103: weapon equip-status gate + armor data reaches the attack snapshot ----------

        [Test] // TC-ATTACK-105
        public void Read_RejectsAnOwnedButNotEquippedWeapon_NewBehaviorPreviouslyPassed()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord unequippedWeapon = CreateContainedItem(actor);

            Result<AttackEvaluationState> rejected = _reader.Read(_campaign, Intent(encounter, actor, target, unequippedWeapon), Corr);

            Assert.That(rejected.IsFailure, Is.True);
        }

        [Test] // TC-ATTACK-106
        public void Read_AcceptsAnOwnedAndEquippedWeapon_LegitimatePathStillWorks()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord equippedWeapon = ItemFor(actor);

            Result<AttackEvaluationState> accepted = _reader.Read(_campaign, Intent(encounter, actor, target, equippedWeapon), Corr);

            Assert.That(accepted.IsSuccess, Is.True);
        }

        [Test] // TC-ATTACK-107
        public void Read_TargetWithNoEquippedArmor_ArmorAndEffectsIsUnavailable()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);

            AttackEvaluationSnapshot snapshot = _reader.Read(_campaign, Intent(encounter, actor, target, item), Corr).Value.Snapshot;

            Assert.That(snapshot.ArmorAndEffects.Availability, Is.EqualTo(AttackArmorAvailability.UnavailableNotBound));
            Assert.That(snapshot.ArmorAndEffects.Entries, Is.Empty);
        }

        [Test] // TC-ATTACK-108
        public void Read_TargetWithOneEquippedArmor_SnapshotCarriesTheRealDecodedArmorDefinition()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            EquipArmor(target, "chest_slot", protection: 6, "Torso");

            AttackEvaluationSnapshot snapshot = _reader.Read(_campaign, Intent(encounter, actor, target, item), Corr).Value.Snapshot;

            Assert.That(snapshot.ArmorAndEffects.Availability, Is.EqualTo(AttackArmorAvailability.Available));
            Assert.That(snapshot.ArmorAndEffects.Entries.Count, Is.EqualTo(1));
            AttackTargetArmorEntry entry = snapshot.ArmorAndEffects.Entries[0];
            Assert.That(entry.TargetId, Is.EqualTo(target));
            Assert.That(entry.Armor.Protection, Is.EqualTo(6));
            Assert.That(entry.Armor.CoveredBodyPartIds, Is.EquivalentTo(new[] { BodyPartId.Parse("Torso") }));
        }

        [Test] // TC-ATTACK-109
        public void Read_TargetWithArmorAndANonArmorEquippedItem_SnapshotCarriesOnlyTheRealArmor()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);
            EquipArmor(target, "chest_slot", protection: 4, "Torso");
            Equip(CreateContainedItem(target), "amulet_slot");

            AttackEvaluationSnapshot snapshot = _reader.Read(_campaign, Intent(encounter, actor, target, item), Corr).Value.Snapshot;

            Assert.That(snapshot.ArmorAndEffects.Availability, Is.EqualTo(AttackArmorAvailability.Available));
            Assert.That(snapshot.ArmorAndEffects.Entries.Count, Is.EqualTo(1));
            Assert.That(snapshot.ArmorAndEffects.Entries[0].Armor.Protection, Is.EqualTo(4));
        }

        [Test] // TC-ATTACK-110
        public void Read_MultipleTargets_EachHasItsOwnCorrectArmor_NotMixedUp()
        {
            CharacterId actor = Active("actor"), targetA = Active("targetA"), targetB = Active("targetB");
            CombatEncounterRecord encounter = CombatEncounterService.Create(_encounters, _campaign, new CreateCombatEncounterRequest(new[] { actor, targetA, targetB }, User(), true, Command()), Corr).Value;
            ItemInstanceRecord item = ItemFor(actor);
            EquipArmor(targetA, "chest_slot", protection: 3, "Torso");
            EquipArmor(targetB, "head_slot", protection: 9, "Head");

            AttackIntent intent = new AttackIntent(encounter.EncounterId, actor, new[] { targetA, targetB }, item.ItemInstanceId, encounter.Revision);
            AttackEvaluationSnapshot snapshot = _reader.Read(_campaign, intent, Corr).Value.Snapshot;

            Assert.That(snapshot.ArmorAndEffects.Availability, Is.EqualTo(AttackArmorAvailability.Available));
            Assert.That(snapshot.ArmorAndEffects.Entries.Count, Is.EqualTo(2));
            AttackTargetArmorEntry entryA = Single(snapshot.ArmorAndEffects.Entries, targetA);
            AttackTargetArmorEntry entryB = Single(snapshot.ArmorAndEffects.Entries, targetB);
            Assert.That(entryA.Armor.Protection, Is.EqualTo(3));
            Assert.That(entryB.Armor.Protection, Is.EqualTo(9));
        }

        // ---- ODY-S06-104: Character-Token position resolution reaches the attack snapshot -----------

        [Test] // TC-ATTACK-111
        public void PreviewAttack_ActorAndTargetTokensOnSameScene_TopologyCarriesTheRealComputedDistance()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            SceneId scene = CreateScene();
            LinkToken(scene, actor, 0, 0);
            LinkToken(scene, target, 3, 4);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);

            Result<ProposedAttackResolution> preview = AttackEvaluationService.PreviewAttack(_reader, new RecordingRules(), _campaign, ServiceRequest(encounter, actor, target, item));
            Assert.That(preview.IsSuccess, Is.True);
            AttackTopologyInput topology = preview.Value.Snapshot.Topology;
            Assert.That(topology.Availability, Is.EqualTo(AttackTopologyAvailability.Available));
            Assert.That(topology.Entries.Count, Is.EqualTo(1));
            Assert.That(topology.Entries[0].TargetId, Is.EqualTo(target));
            Assert.That(topology.Entries[0].Distance, Is.EqualTo(5.0).Within(1e-9));
        }

        [Test] // TC-ATTACK-112
        public void Read_ActorWithNoLinkedToken_TopologyIsUnavailable()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            SceneId scene = CreateScene();
            LinkToken(scene, target, 1, 1);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);

            AttackEvaluationSnapshot snapshot = _reader.Read(_campaign, Intent(encounter, actor, target, item), Corr).Value.Snapshot;

            Assert.That(snapshot.Topology.Availability, Is.EqualTo(AttackTopologyAvailability.UnavailableNotBound));
            Assert.That(snapshot.Topology.Entries, Is.Empty);
        }

        [Test] // TC-ATTACK-113
        public void Read_TargetWithNoLinkedToken_TopologyIsUnavailable()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            SceneId scene = CreateScene();
            LinkToken(scene, actor, 0, 0);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);

            AttackEvaluationSnapshot snapshot = _reader.Read(_campaign, Intent(encounter, actor, target, item), Corr).Value.Snapshot;

            Assert.That(snapshot.Topology.Availability, Is.EqualTo(AttackTopologyAvailability.UnavailableNotBound));
            Assert.That(snapshot.Topology.Entries, Is.Empty);
        }

        [Test] // TC-ATTACK-114
        public void Read_ActorAndTargetOnDifferentScenes_TopologyIsUnavailableForThatTarget()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            SceneId actorScene = CreateScene();
            SceneId targetScene = CreateScene();
            LinkToken(actorScene, actor, 0, 0);
            LinkToken(targetScene, target, 1, 1);
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);

            AttackEvaluationSnapshot snapshot = _reader.Read(_campaign, Intent(encounter, actor, target, item), Corr).Value.Snapshot;

            Assert.That(snapshot.Topology.Availability, Is.EqualTo(AttackTopologyAvailability.UnavailableNotBound));
            Assert.That(snapshot.Topology.Entries, Is.Empty);
        }

        [Test] // TC-ATTACK-115
        public void Read_MultipleTargets_SomeWithPositionSomeWithout_MixedResultIsCorrect()
        {
            CharacterId actor = Active("actor"), targetA = Active("targetA"), targetB = Active("targetB");
            SceneId scene = CreateScene();
            LinkToken(scene, actor, 0, 0);
            LinkToken(scene, targetA, 6, 8);
            // targetB deliberately has no linked token.
            CombatEncounterRecord encounter = CombatEncounterService.Create(_encounters, _campaign, new CreateCombatEncounterRequest(new[] { actor, targetA, targetB }, User(), true, Command()), Corr).Value;
            ItemInstanceRecord item = ItemFor(actor);

            AttackIntent intent = new AttackIntent(encounter.EncounterId, actor, new[] { targetA, targetB }, item.ItemInstanceId, encounter.Revision);
            AttackEvaluationSnapshot snapshot = _reader.Read(_campaign, intent, Corr).Value.Snapshot;

            Assert.That(snapshot.Topology.Availability, Is.EqualTo(AttackTopologyAvailability.Available));
            Assert.That(snapshot.Topology.Entries.Count, Is.EqualTo(1));
            Assert.That(snapshot.Topology.Entries[0].TargetId, Is.EqualTo(targetA));
            Assert.That(snapshot.Topology.Entries[0].Distance, Is.EqualTo(10.0).Within(1e-9));
        }

        [Test] // TC-ATTACK-116
        public void Read_ExistingTestsWithNoLinkedTokens_TopologyRemainsUnavailable_NotBrokenByThisTask()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ItemInstanceRecord item = ItemFor(actor);

            AttackEvaluationSnapshot snapshot = _reader.Read(_campaign, Intent(encounter, actor, target, item), Corr).Value.Snapshot;

            Assert.That(snapshot.Topology.Availability, Is.EqualTo(AttackTopologyAvailability.UnavailableNotBound));
        }

        private SceneId CreateScene() => _scenes.CreateScene(_campaign, "Battle Map " + Guid.NewGuid().ToString("N"), Command(), Corr).Value.SceneId;

        private TokenRecord LinkToken(SceneId scene, CharacterId characterId, double x, double y)
        {
            Result<TokenRecord> created = _scenes.CreateToken(_campaign, scene, new TokenPosition(x, y), User(), Command(), Corr, characterId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        private CharacterRecord GrantAttribute(CharacterId characterId, string attributeName, long value)
        {
            CharacterRecord current = _characters.GetCharacter(_campaign, characterId, Corr).Value;
            Result<CharacterRecord> granted = _characters.GrantDevelopmentPoints(_campaign, characterId, 100, "test", User(), actorIsMainGm: true, current.Revisions.MechanicsRevision, Command(), Corr);
            Assert.That(granted.IsSuccess, Is.True);
            Result<CharacterRecord> purchased = _characters.PurchaseAttributeIncrease(_campaign, characterId, AttributeDefinitionId.Parse(attributeName), value, User(), actorIsMainGm: true, granted.Value.Revisions.MechanicsRevision, expectedAttributeRevision: 0, Command(), Corr);
            Assert.That(purchased.IsSuccess, Is.True);
            return purchased.Value;
        }

        private static AttackRequest ServiceRequest(CombatEncounterRecord encounter, CharacterId actor, CharacterId target, ItemInstanceRecord item)
            => new AttackRequest(Intent(encounter, actor, target, item), User(), actorIsMainGm: true, Command(), Corr);

        // Real attack-evaluator implementation is ODY-S06-105's own job; this fixture is a fixed-outcome
        // stand-in identical in shape to AttackEvaluationServiceTests' own -- this task only needs the real
        // AttackEvaluationService/SqliteAttackStateReader pipeline to run so the snapshot it produces is real.
        private sealed class RecordingRules : IAttackRulesEvaluator
        {
            public ProposedAttackResolution Preview(AttackIntent intent, AttackEvaluationSnapshot snapshot) => Resolution(intent, snapshot, null);
            public ProposedAttackResolution Evaluate(AttackIntent intent, AttackEvaluationSnapshot snapshot, AttackRandomSample randomSample) => Resolution(intent, snapshot, randomSample);
            private static ProposedAttackResolution Resolution(AttackIntent intent, AttackEvaluationSnapshot snapshot, AttackRandomSample? sample)
                => new ProposedAttackResolution(intent, snapshot, sample, new AttackRangeResult(true, "in-range"), Array.AsReadOnly(new AttackModifierEntry[0]), new AttackHitResult(true, "hit"), null, null, Array.AsReadOnly(new AttackDelta[0]), Array.AsReadOnly(new AttackDelta[0]), Array.AsReadOnly(new AttackEffectCandidate[0]));
        }

        private CombatEncounterRecord CreateEncounter(CharacterId actor, CharacterId target)
            => CombatEncounterService.Create(_encounters, _campaign, new CreateCombatEncounterRequest(new[] { actor, target }, User(), true, Command()), Corr).Value;

        private CombatEncounterRecord Advance(CombatEncounterRecord encounter)
            => CombatEncounterService.Advance(_encounters, _campaign, new AdvanceCombatEncounterRequest(encounter.EncounterId, encounter.Revision, User(), true, Command()), Corr).Value;

        private static AttackIntent Intent(CombatEncounterRecord encounter, CharacterId actor, CharacterId target, ItemInstanceRecord item)
            => new AttackIntent(encounter.EncounterId, actor, new[] { target }, item.ItemInstanceId, encounter.Revision);

        private CharacterId Active(string name)
        {
            CharacterId id = _characters.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, name), Command(), Corr).Value.CharacterId;
            CharacterRecord current = _characters.GetCharacter(_campaign, id, Corr).Value;
            return _characters.ApproveCharacterDraft(_campaign, id, true, current.Revisions.LifecycleRevision, Command(), Corr).Value.CharacterId;
        }

        private void TransitionToDead(CharacterId id)
        {
            CharacterRecord current = _characters.GetCharacter(_campaign, id, Corr).Value;
            Result<CharacterRecord> transitioned = _characters.TransitionCharacterToDead(_campaign, id, LifecycleDeathIssuerKind.GMOverride, User(), true, current.Revisions.LifecycleRevision, Command(), Corr);
            Assert.That(transitioned.IsSuccess, Is.True);
        }

        // ODY-S06-103: the attack action item must now be currently Equipped, not merely owned -- every
        // ItemFor caller in this file wants the still-supported (owned AND equipped) legitimate path,
        // so this helper equips what it creates. CreateContainedItem is the raw, deliberately-unequipped
        // primitive for the tests that need an owned-but-not-equipped item instead (TC-ATTACK-105).
        private ItemInstanceRecord ItemFor(CharacterId owner) => Equip(CreateContainedItem(owner), "main_hand");

        private ItemInstanceRecord CreateContainedItem(CharacterId owner) => CreateContainedItemWithMechanics(owner, ContentDefinitionType.Item, "{}");

        private ItemInstanceRecord CreateContainedItemWithMechanics(CharacterId owner, ContentDefinitionType contentType, string payload)
        {
            UtcInstant now = _clock.GetUtcNow();
            InventoryId inventoryId = InventoryId.NewId(now);
            InventoryRecord inventory = new InventoryRecord(inventoryId, _campaign.CampaignId, InventoryOwnerRef.ForCharacter(owner), 1, now, now);
            Assert.That(_inventory.CreateInventory(_campaign, inventory, Command(), Corr).IsSuccess, Is.True);
            ContentDefinitionRef source = ContentDefinitionRef.Parse("cdef_" + Guid.NewGuid().ToString("N") + "/1");
            ItemInstanceRecord item = new ItemInstanceRecord(ItemInstanceId.NewId(now), _campaign.CampaignId, inventoryId, InventoryOwnerRef.ForCharacter(owner), InventoryLocationRef.Contained(inventoryId, "main"), source, new ItemMechanicsSnapshot(source, 1, contentType, payload), "{}", 1, now, now);
            return _inventory.CreateItemInstance(_campaign, item, Command(), Corr).Value;
        }

        // ODY-S06-103: a real, decodable ArmorDefinition payload (TypedDefinitionCodec.EncodeArmor, the
        // same codec DecodeArmor reads back) -- not a hand-written JSON stand-in.
        private ItemInstanceRecord CreateContainedArmorItem(CharacterId owner, string equipmentSlotKey, long protection, string coveredBodyPart)
        {
            var itemDefinition = new ItemDefinition(ItemCategory.Generic, false, null, 1, false, null, false, null, Array.Empty<ContentDefinitionRef>(), Array.Empty<ContentDefinitionRef>());
            var armorDefinition = new ArmorDefinition(itemDefinition, equipmentSlotKey, new[] { BodyPartId.Parse(coveredBodyPart) }, protection);
            return CreateContainedItemWithMechanics(owner, ContentDefinitionType.Armor, TypedDefinitionCodec.EncodeArmor(armorDefinition));
        }

        private ItemInstanceRecord EquipArmor(CharacterId owner, string equipmentSlotKey, long protection, string coveredBodyPart)
            => Equip(CreateContainedArmorItem(owner, equipmentSlotKey, protection, coveredBodyPart), equipmentSlotKey);

        private ItemInstanceRecord Equip(ItemInstanceRecord item, string equipmentSlotKey)
        {
            var entry = new EquippedEntry(item.InventoryId, InventoryItemRef.ForInstance(item.ItemInstanceId), equipmentSlotKey, Array.Empty<BodyPartId>(), User(), _clock.GetUtcNow(), 1);
            Result<EquippedEntryRecord> equipped = _inventory.EquipItem(_campaign, new EquipTransition(new EquippedEntryRecord(_campaign.CampaignId, entry), item.Revision, Command()), Corr);
            Assert.That(equipped.IsSuccess, Is.True);
            return _inventory.GetItemInstance(_campaign, item.ItemInstanceId, Corr).Value;
        }

        // ODY-S06-103: unequip then re-equip the actor's own item -- bumps ItemInstanceRecord.Revision the
        // same way a Contained-container Move used to, without changing owner or mechanics snapshot, while
        // leaving the item Equipped again at the end (required by the new equip-status gate).
        private ItemInstanceRecord Requip(ItemInstanceRecord equippedItem)
        {
            Result<EquippedEntryRecord> current = _inventory.GetEquippedEntry(_campaign, InventoryItemRef.ForInstance(equippedItem.ItemInstanceId), Corr);
            Assert.That(current.IsSuccess, Is.True);
            Result<bool> unequipped = _inventory.UnequipItem(_campaign, new UnequipTransition(current.Value.Entry.ItemRef, current.Value.Entry.InventoryId, equippedItem.Revision, current.Value.Entry.Revision, "main", Command()), Corr);
            Assert.That(unequipped.IsSuccess, Is.True);
            ItemInstanceRecord contained = _inventory.GetItemInstance(_campaign, equippedItem.ItemInstanceId, Corr).Value;
            return Equip(contained, current.Value.Entry.EquipmentSlotRef);
        }

        private static AttackTargetArmorEntry Single(IReadOnlyList<AttackTargetArmorEntry> entries, CharacterId targetId)
        {
            for (int index = 0; index < entries.Count; index++) if (entries[index].TargetId == targetId) return entries[index];
            throw new InvalidOperationException("No armor entry found for target " + targetId + ".");
        }

        private long TotalRowCount()
        {
            using SqliteConnection connection = OpenRawConnection();
            var tables = new List<string>();
            using (SqliteCommand list = connection.CreateCommand())
            {
                list.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
                using SqliteDataReader reader = list.ExecuteReader();
                while (reader.Read()) tables.Add(reader.GetString(0));
            }
            long total = 0;
            foreach (string table in tables)
            {
                using SqliteCommand count = connection.CreateCommand();
                count.CommandText = "SELECT COUNT(*) FROM \"" + table + "\";";
                total += Convert.ToInt64(count.ExecuteScalar());
            }
            return total;
        }

        private SqliteConnection OpenRawConnection()
        {
            var connection = new SqliteConnection("Data Source=" + Path.Combine(_root, "campaign.db"));
            connection.Open();
            return connection;
        }

        private static CommandId Command() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId User() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
        private static bool Throws<T>(Action action) where T : Exception { try { action(); return false; } catch (T) { return true; } }
    }
}
