using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Odyssey.Application.Combat;
using Odyssey.Application.Commands;
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
            _reader = new SqliteAttackStateReader(_encounters, _inventory, _characters, _clock);
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
            Assert.That(snapshot.Topology.Availability, Is.EqualTo(AttackInputAvailability.UnavailableNotBound));
            Assert.That(snapshot.ArmorAndEffects.Availability, Is.EqualTo(AttackInputAvailability.UnavailableNotBound));
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

            // Item revision: moving the actor's own item to a different container key bumps ItemInstanceRecord.Revision
            // without changing its owner or mechanics snapshot.
            ItemInstanceRecord movedItem = Move(item, "belt");
            Assert.That(movedItem.Revision, Is.GreaterThan(item.Revision));
            string afterItemRevisionChanged = _reader.Read(_campaign, Intent(afterSecondAdvance, actor, target, movedItem), Corr).Value.Snapshot.Fingerprint;
            Assert.That(afterItemRevisionChanged, Is.Not.EqualTo(afterEncounterRevisionChanged));

            // Participant lifecycle: transitioning the target to Dead changes its LifecycleStatus without touching the encounter or item.
            TransitionToDead(target);
            string afterTargetDied = _reader.Read(_campaign, Intent(afterSecondAdvance, actor, target, movedItem), Corr).Value.Snapshot.Fingerprint;
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

        private ItemInstanceRecord ItemFor(CharacterId owner)
        {
            UtcInstant now = _clock.GetUtcNow();
            InventoryId inventoryId = InventoryId.NewId(now);
            InventoryRecord inventory = new InventoryRecord(inventoryId, _campaign.CampaignId, InventoryOwnerRef.ForCharacter(owner), 1, now, now);
            Assert.That(_inventory.CreateInventory(_campaign, inventory, Command(), Corr).IsSuccess, Is.True);
            ContentDefinitionRef source = ContentDefinitionRef.Parse("cdef_0123456789abcdef0123456789abcdef/1");
            ItemInstanceRecord item = new ItemInstanceRecord(ItemInstanceId.NewId(now), _campaign.CampaignId, inventoryId, InventoryOwnerRef.ForCharacter(owner), InventoryLocationRef.Contained(inventoryId, "main"), source, new ItemMechanicsSnapshot(source, 1, ContentDefinitionType.Item, "{}"), "{}", 1, now, now);
            return _inventory.CreateItemInstance(_campaign, item, Command(), Corr).Value;
        }

        private ItemInstanceRecord Move(ItemInstanceRecord item, string destinationContainerKey)
        {
            var request = new MoveItemInstanceRequest(_campaign, item.ItemInstanceId, item.Revision, item.InventoryId, item.Revision, item.InventoryId, item.Revision, destinationContainerKey, User(), true, Command(), Corr);
            Result<ItemInstanceRecord> moved = InventoryMovementService.MoveItemInstance(_inventory, request);
            Assert.That(moved.IsSuccess, Is.True);
            return moved.Value;
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
