using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Content;
using Odyssey.Application.Effects;
using Odyssey.Application.Inventory;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Content;
using Odyssey.Domain.Effects;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence
{
    public sealed class ItemEffectLifecycleTests
    {
        private static readonly CorrelationId Corr = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly UserId Actor = UserId.Parse("user_0123456789abcdef0123456789abcdef");
        private static readonly UtcInstant Now = UtcInstant.Parse("2026-09-12T10:00:00.0000000Z");
        private static CommandId Command() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private readonly MutableTestClock _clock = new MutableTestClock(Now);
        private string _directory = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaigns = null!;
        private SqliteInventoryRepository _inventory = null!;
        private SqliteContentCatalogRepository _catalog = null!;
        private SqliteActiveEffectRepository _effects = null!;
        private InventoryRecord _bag = null!;
        private ActiveEffectTargetRef Target => ActiveEffectTargetRef.ForCharacter(CharacterId.Parse(_bag.OwnerRef.TargetRef));

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "ody-s05-505-" + Guid.NewGuid().ToString("N"));
            _campaigns = new SqliteCampaignRepository(_clock);
            var campaign = _campaigns.Create(new CreateCampaignRequest(_directory, "Item effect tests", "ruleset.core", "1.0.0", "0.1.0"), Command(), Corr);
            Assert.That(campaign.IsSuccess, Is.True);
            _campaign = campaign.Value;
            _inventory = new SqliteInventoryRepository(_clock);
            _catalog = new SqliteContentCatalogRepository(_clock);
            _effects = new SqliteActiveEffectRepository(_clock);
            _bag = new InventoryRecord(InventoryId.NewId(Now), _campaign.CampaignId, InventoryOwnerRef.ForCharacter(CharacterId.NewId(Now)), 1, Now, Now);
            Assert.That(_inventory.CreateInventory(_campaign, _bag, Command(), Corr).IsSuccess, Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            _campaigns.Close(_campaign, Corr);
            using (var pool = new SqliteConnection("Data Source=" + Path.Combine(_directory, "campaign.db")))
                SqliteConnection.ClearPool(pool);
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Test] // TC-ACTIVEEFFECT-051
        public void EquipCreatesAllBuiltInsFromPinnedSnapshotsAndReplayCreatesNoDuplicates()
        {
            ContentDefinitionRef first = PublishEffect(EffectDurationType.WhileItemEquipped);
            ContentDefinitionRef second = PublishEffect(EffectDurationType.Permanent);
            InventoryItemRef item = CreateItem(false, first, second);
            var equipped = Equip(item);
            CommandId command = Command();
            var result = ItemEffectLifecycleService.OnItemEquipped(_inventory, _catalog, _effects, _campaign, equipped, Target, command, Corr);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Count, Is.EqualTo(2));
            Assert.That(result.Value.All(r => r.Effect.SourceRef.Equals(ActiveEffectSourceRef.ForEquippedItem(item)) && r.Effect.Status == ActiveEffectStatus.Active && r.Effect.StackCount == 1), Is.True);
            Assert.That(result.Value[0].Effect.EffectMechanicsSnapshot.Payload, Is.EqualTo(EffectJson(EffectDurationType.WhileItemEquipped)));
            var replay = ItemEffectLifecycleService.OnItemEquipped(_inventory, _catalog, _effects, _campaign, equipped, Target, command, Corr);
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.Select(r => r.Effect.ActiveEffectId), Is.EqualTo(result.Value.Select(r => r.Effect.ActiveEffectId)));
            Assert.That(Count("ActiveEffect"), Is.EqualTo(2L));
        }

        [Test] // TC-ACTIVEEFFECT-052
        public void EquipWithoutBuiltInsDoesNotCreateEffects()
        {
            var result = React(Equip(CreateItem(false)));
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Count, Is.EqualTo(0));
            Assert.That(Count("ActiveEffect"), Is.EqualTo(0L));
        }

        [Test] // TC-ACTIVEEFFECT-053
        public void UnequipSuspendsBothItemSourceKindsButLeavesAnotherItemAlone()
        {
            InventoryItemRef item = CreateItem(false);
            var equipped = Equip(item);
            var first = Seed(item, EffectDurationType.WhileItemEquipped);
            var second = Seed(item, EffectDurationType.WhileItemEquipped, equippedSource: true);
            var other = Seed(CreateItem(false), EffectDurationType.WhileItemEquipped);
            Unequip(equipped);
            var result = ItemEffectLifecycleService.OnItemUnequipped(_effects, _campaign, item, Actor, Command(), Corr);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Count, Is.EqualTo(2));
            Assert.That(Read(first).Status, Is.EqualTo(ActiveEffectStatus.Suspended));
            Assert.That(Read(second).Revision, Is.EqualTo(2L));
            Assert.That(Read(other).Status, Is.EqualTo(ActiveEffectStatus.Active));
            Assert.That(Read(other).Revision, Is.EqualTo(1L));
        }

        [Test] // TC-ACTIVEEFFECT-054
        public void ReequipResumesSameRowAndPreservesSnapshotAndAppliedTime()
        {
            InventoryItemRef item = CreateItem(false, PublishEffect(EffectDurationType.WhileItemEquipped));
            var equipped = Equip(item);
            var created = React(equipped).Value.Single();
            Unequip(equipped);
            Assert.That(ItemEffectLifecycleService.OnItemUnequipped(_effects, _campaign, item, Actor, Command(), Corr).IsSuccess, Is.True);
            var resumed = React(Equip(item));
            Assert.That(resumed.IsSuccess, Is.True);
            Assert.That(resumed.Value.Count, Is.EqualTo(1));
            Assert.That(Count("ActiveEffect"), Is.EqualTo(1L));
            var current = Read(created);
            Assert.That(current.Status, Is.EqualTo(ActiveEffectStatus.Active));
            Assert.That(current.Revision, Is.EqualTo(3L));
            Assert.That(current.AppliedAt, Is.EqualTo(created.Effect.AppliedAt));
            Assert.That(current.EffectMechanicsSnapshot, Is.EqualTo(created.Effect.EffectMechanicsSnapshot));
        }

        [Test] // TC-ACTIVEEFFECT-055
        public void UnequipLeavesAllOtherPersistableDurationsUntouched()
        {
            InventoryItemRef item = CreateItem(false);
            var durations = new[] { EffectDurationType.Permanent, EffectDurationType.UntilRemoved, EffectDurationType.UntilSceneChange,
                EffectDurationType.UntilSessionEnd, EffectDurationType.WhileCondition, EffectDurationType.WhileSourceExists, EffectDurationType.ForDuration };
            var records = durations.Select(d => Seed(item, d)).ToArray();
            var result = ItemEffectLifecycleService.OnItemUnequipped(_effects, _campaign, item, Actor, Command(), Corr);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Count, Is.EqualTo(0));
            Assert.That(records.All(r => Read(r).Status == ActiveEffectStatus.Active && Read(r).Revision == 1), Is.True);
        }

        [Test] // TC-ACTIVEEFFECT-056
        public void StaleRevisionFailsWithoutProjectionOrJournalChange()
        {
            var effect = Seed(CreateItem(false), EffectDurationType.WhileItemEquipped);
            long events = Count("DomainEvents");
            var failed = Transition(effect, false, 9, Command());
            Assert.That(failed.IsFailure, Is.True);
            Assert.That(failed.Error.Code, Is.EqualTo(ErrorCodes.PersistenceActiveEffectRevisionConflict));
            Assert.That(Read(effect).Revision, Is.EqualTo(1L));
            Assert.That(Count("DomainEvents"), Is.EqualTo(events));
        }

        [Test] // TC-ACTIVEEFFECT-057
        public void ReplaySurvivesLaterResumeAndRepositoryReopen()
        {
            var effect = Seed(CreateItem(false), EffectDurationType.WhileItemEquipped);
            CommandId suspend = Command();
            Assert.That(Transition(effect, false, 1, suspend).Value, Is.EqualTo(2L));
            Assert.That(Transition(effect, true, 2, Command()).Value, Is.EqualTo(3L));
            long events = Count("DomainEvents");
            _effects = new SqliteActiveEffectRepository(_clock);
            var replay = Transition(effect, false, 1, suspend);
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value, Is.EqualTo(2L));
            Assert.That(Read(effect).Status, Is.EqualTo(ActiveEffectStatus.Active));
            Assert.That(Read(effect).Revision, Is.EqualTo(3L));
            Assert.That(Count("DomainEvents"), Is.EqualTo(events));
        }

        [Test] // TC-ACTIVEEFFECT-058
        public void CommandReuseWithChangedActorEffectRevisionOrDirectionFails()
        {
            var effect = Seed(CreateItem(false), EffectDurationType.WhileItemEquipped);
            var other = Seed(CreateItem(false), EffectDurationType.WhileItemEquipped);
            CommandId command = Command();
            Assert.That(Transition(effect, false, 1, command).IsSuccess, Is.True);
            long events = Count("DomainEvents");
            Assert.That(Transition(effect, true, 1, command).IsFailure, Is.True);
            Assert.That(Transition(effect, false, 2, command).IsFailure, Is.True);
            Assert.That(Transition(other, false, 1, command).IsFailure, Is.True);
            var changedActor = _effects.SetItemEffectEquipped(_campaign, _campaign.CampaignId, effect.Effect.ActiveEffectId, false, 1,
                UserId.Parse("user_abcdef0123456789abcdef0123456789"), command, Corr);
            Assert.That(changedActor.IsFailure, Is.True);
            Assert.That(Count("DomainEvents"), Is.EqualTo(events));
        }

        [Test] // TC-ACTIVEEFFECT-059
        public void RepositoryRejectsNonEquipmentDurationAndTerminalEffects()
        {
            var item = CreateItem(false);
            var permanent = Seed(item, EffectDurationType.Permanent);
            var expired = Seed(item, EffectDurationType.WhileItemEquipped, status: ActiveEffectStatus.Expired);
            var removed = Seed(item, EffectDurationType.WhileItemEquipped, status: ActiveEffectStatus.Removed);
            long events = Count("DomainEvents");
            Assert.That(Transition(permanent, false, 1, Command()).IsFailure, Is.True);
            Assert.That(Transition(expired, true, 1, Command()).IsFailure, Is.True);
            Assert.That(Transition(removed, true, 1, Command()).IsFailure, Is.True);
            Assert.That(Count("DomainEvents"), Is.EqualTo(events));
        }

        [Test] // TC-ACTIVEEFFECT-060
        public void TransitionRejectsCampaignMismatch()
        {
            var effect = Seed(CreateItem(false), EffectDurationType.WhileItemEquipped);
            var result = _effects.SetItemEffectEquipped(_campaign, CampaignId.NewId(Now), effect.Effect.ActiveEffectId, false, 1, Actor, Command(), Corr);
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceActiveEffectCampaignMismatch));
        }

        [Test] // TC-ACTIVEEFFECT-061
        public void StackSourceSupportsCreateSuspendAndResume()
        {
            var item = CreateItem(true, PublishEffect(EffectDurationType.WhileItemEquipped));
            var equipped = Equip(item);
            var effect = React(equipped).Value.Single();
            Assert.That(effect.Effect.SourceRef.ItemRef.Kind, Is.EqualTo(InventoryItemRefKind.ItemStack));
            Unequip(equipped);
            Assert.That(ItemEffectLifecycleService.OnItemUnequipped(_effects, _campaign, item, Actor, Command(), Corr).IsSuccess, Is.True);
            Assert.That(Read(effect).Status, Is.EqualTo(ActiveEffectStatus.Suspended));
            Assert.That(React(Equip(item)).IsSuccess, Is.True);
            Assert.That(Read(effect).Status, Is.EqualTo(ActiveEffectStatus.Active));
            Assert.That(Count("ActiveEffect"), Is.EqualTo(1L));
        }

        [Test] // TC-ACTIVEEFFECT-062
        public void InvalidPinnedSnapshotFailsBeforeAnySuspension()
        {
            var item = CreateItem(false);
            var valid = Seed(item, EffectDurationType.WhileItemEquipped);
            Seed(item, EffectDurationType.WhileItemEquipped, payload: "{}");
            var result = ItemEffectLifecycleService.OnItemUnequipped(_effects, _campaign, item, Actor, Command(), Corr);
            Assert.That(result.IsFailure, Is.True);
            Assert.That(Read(valid).Status, Is.EqualTo(ActiveEffectStatus.Active));
            Assert.That(Read(valid).Revision, Is.EqualTo(1L));
        }

        [Test] // TC-ACTIVEEFFECT-063
        public void MissingReferencedDefinitionPreventsPartialCreation()
        {
            var item = CreateItem(false, PublishEffect(EffectDurationType.Permanent), new ContentDefinitionRef(ContentDefinitionId.NewId(Now), 1));
            var result = React(Equip(item));
            Assert.That(result.IsFailure, Is.True);
            Assert.That(Count("ActiveEffect"), Is.EqualTo(0L));
        }

        [Test] // TC-ACTIVEEFFECT-064
        public void InstantEffectCreatesNoPersistentRow()
        {
            var result = React(Equip(CreateItem(false, PublishEffect(EffectDurationType.Instant))));
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Count, Is.EqualTo(0));
            Assert.That(Count("ActiveEffect"), Is.EqualTo(0L));
        }

        [Test] // TC-ACTIVEEFFECT-065
        public void ForDurationRequiresExplicitRulesetExpiryAndPreservesIt()
        {
            var equipped = Equip(CreateItem(false, PublishEffect(EffectDurationType.ForDuration)));
            Assert.That(React(equipped).IsFailure, Is.True);
            var expiry = UtcInstant.Parse("2026-09-12T11:00:00.0000000Z");
            var result = ItemEffectLifecycleService.OnItemEquipped(_inventory, _catalog, _effects, _campaign, equipped, Target, Command(), Corr,
                (snapshot, appliedAt) => Result<UtcInstant>.Success(expiry));
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Single().Effect.ExpiresAt, Is.EqualTo(expiry));
        }

        [Test] // TC-ACTIVEEFFECT-066
        public void ExpiredWhileEquippedEffectIsNotResurrected()
        {
            var item = CreateItem(false);
            var effect = Seed(item, EffectDurationType.WhileItemEquipped, status: ActiveEffectStatus.Expired);
            Assert.That(React(Equip(item)).IsSuccess, Is.True);
            Assert.That(Read(effect).Status, Is.EqualTo(ActiveEffectStatus.Expired));
            Assert.That(Read(effect).Revision, Is.EqualTo(1L));
        }

        [Test] // TC-ACTIVEEFFECT-067
        public void ReactionCommandCannotBeReusedForAnotherItem()
        {
            var definition = PublishEffect(EffectDurationType.Permanent);
            var first = Equip(CreateItem(false, definition));
            var second = Equip(CreateItem(false, definition));
            CommandId command = Command();
            Assert.That(ItemEffectLifecycleService.OnItemEquipped(_inventory, _catalog, _effects, _campaign, first, Target, command, Corr).IsSuccess, Is.True);
            var changedItem = ItemEffectLifecycleService.OnItemEquipped(_inventory, _catalog, _effects, _campaign, second, Target, command, Corr);
            Assert.That(changedItem.IsFailure, Is.True);
            Assert.That(Count("ActiveEffect"), Is.EqualTo(1L));
        }

        [Test] // TC-ACTIVEEFFECT-068
        public void TransitionCommitsCanonicalEventSummaryHashAndAggregateRevisionTogether()
        {
            var effect = Seed(CreateItem(false), EffectDurationType.WhileItemEquipped);
            CommandId command = Command();
            Assert.That(Transition(effect, false, 1, command).IsSuccess, Is.True);
            string expected = "{\"version\":1,\"operation\":\"item-effect-equipment\",\"campaignId\":\"" + _campaign.CampaignId +
                "\",\"activeEffectId\":\"" + effect.Effect.ActiveEffectId + "\",\"actorUserId\":\"" + Actor + "\",\"expectedRevision\":1,\"status\":\"Suspended\"}";
            using var connection = new SqliteConnection("Data Source=" + Path.Combine(_directory, "campaign.db"));
            connection.Open();
            using var query = connection.CreateCommand();
            query.CommandText = "SELECT e.PayloadJson, e.PayloadHash, c.ResultSummary, r.Revision FROM DomainEvents e " +
                "JOIN AppliedCommands c ON c.CommandId=e.CommandId JOIN AggregateRevisions r ON r.AggregateId=$effectId AND r.AggregateType='active_effect' WHERE e.CommandId=$commandId;";
            query.Parameters.AddWithValue("$effectId", effect.Effect.ActiveEffectId.ToString());
            query.Parameters.AddWithValue("$commandId", command.ToString());
            using var reader = query.ExecuteReader();
            Assert.That(reader.Read(), Is.True);
            Assert.That(reader.GetString(0), Is.EqualTo(expected));
            using var sha = System.Security.Cryptography.SHA256.Create();
            string expectedHash = Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(expected))).ToLowerInvariant();
            Assert.That(reader.GetString(1), Is.EqualTo(expectedHash));
            Assert.That(reader.GetString(2), Is.EqualTo(expected));
            Assert.That(reader.GetInt64(3), Is.EqualTo(2L));
            Assert.That(reader.Read(), Is.False);
        }

        private Result<System.Collections.Generic.IReadOnlyList<ActiveEffectRecord>> React(EquippedEntryRecord equipped) =>
            ItemEffectLifecycleService.OnItemEquipped(_inventory, _catalog, _effects, _campaign, equipped, Target, Command(), Corr);

        private Result<long> Transition(ActiveEffectRecord effect, bool equipped, long revision, CommandId command) =>
            _effects.SetItemEffectEquipped(_campaign, _campaign.CampaignId, effect.Effect.ActiveEffectId, equipped, revision, Actor, command, Corr);

        private ActiveEffect Read(ActiveEffectRecord record) => _effects.GetActiveEffect(_campaign, record.Effect.ActiveEffectId, Corr).Value.Effect;

        private EquippedEntryRecord Equip(InventoryItemRef item)
        {
            long revision = item.Kind == InventoryItemRefKind.ItemInstance ? _inventory.GetItemInstance(_campaign, item.ItemInstanceId, Corr).Value.Revision : _inventory.GetItemStack(_campaign, item.ItemStackId, Corr).Value.Revision;
            var request = new EquipRequest(_campaign, item, _bag.InventoryId, revision, "slot", Array.Empty<BodyPartId>(), Actor, Now, Actor, true, Command(), Corr);
            var result = EquipmentService.Equip(_inventory, new SqliteCharacterRepository(_clock), request);
            Assert.That(result.IsSuccess, Is.True);
            return result.Value;
        }

        private void Unequip(EquippedEntryRecord equipped)
        {
            var item = equipped.Entry.ItemRef;
            long revision = item.Kind == InventoryItemRefKind.ItemInstance ? _inventory.GetItemInstance(_campaign, item.ItemInstanceId, Corr).Value.Revision : _inventory.GetItemStack(_campaign, item.ItemStackId, Corr).Value.Revision;
            var result = EquipmentService.Unequip(_inventory, new UnequipRequest(_campaign, item, _bag.InventoryId, revision, equipped.Entry.Revision, "main", Actor, true, Command(), Corr));
            Assert.That(result.IsSuccess, Is.True);
        }

        private InventoryItemRef CreateItem(bool stack, params ContentDefinitionRef[] effects)
        {
            var reference = new ContentDefinitionRef(ContentDefinitionId.NewId(Now), 1);
            var definition = new ItemDefinition(ItemCategory.Generic, stack, stack ? 10 : (long?)null, 1, false, null, false, null, Array.Empty<ContentDefinitionRef>(), effects);
            var snapshot = new ItemMechanicsSnapshot(reference, 1, ContentDefinitionType.Item, TypedDefinitionCodec.EncodeItem(definition));
            // The item catalog row deliberately does not exist: runtime behavior
            // must come from this pinned snapshot, not today's catalog entry.
            if (stack)
            {
                var record = new ItemStackRecord(ItemStackId.NewId(Now), _campaign.CampaignId, _bag.InventoryId, _bag.OwnerRef,
                    InventoryLocationRef.Contained(_bag.InventoryId, "main"), reference, snapshot, ItemStackQuantity.Create(2), "{}", 1, Now, Now);
                Assert.That(_inventory.CreateItemStack(_campaign, record, Command(), Corr).IsSuccess, Is.True);
                return InventoryItemRef.ForStack(record.ItemStackId);
            }
            var instance = new ItemInstanceRecord(ItemInstanceId.NewId(Now), _campaign.CampaignId, _bag.InventoryId, _bag.OwnerRef,
                InventoryLocationRef.Contained(_bag.InventoryId, "main"), reference, snapshot, "{}", 1, Now, Now);
            Assert.That(_inventory.CreateItemInstance(_campaign, instance, Command(), Corr).IsSuccess, Is.True);
            return InventoryItemRef.ForInstance(instance.ItemInstanceId);
        }

        private ContentDefinitionRef PublishEffect(EffectDurationType duration)
        {
            var draft = _catalog.CreateDraftContentDefinition(new CreateDraftContentDefinitionRequest(_campaign, ContentDefinitionType.Effect,
                "Equipment effect fixture", "Synthetic test data", Actor, propertiesJson: EffectJson(duration)), Command(), Corr);
            Assert.That(draft.IsSuccess, Is.True);
            var published = _catalog.PublishDefinition(_campaign, draft.Value.ContentDefinitionId, Actor, draft.Value.Revision, Command(), Corr);
            Assert.That(published.IsSuccess, Is.True);
            return new ContentDefinitionRef(published.Value.ContentDefinitionId, published.Value.Version);
        }

        private static string EffectJson(EffectDurationType duration) => TypedDefinitionCodec.EncodeEffect(new EffectDefinition(
            new ContentTargetRule(ContentTargetSource.ActingCharacter, 1, 1, true), duration, duration == EffectDurationType.ForDuration ? 1 : (long?)null,
            EffectStackPolicy.IndependentInstances, null));

        private ActiveEffectRecord Seed(InventoryItemRef item, EffectDurationType duration, bool equippedSource = false, ActiveEffectStatus status = ActiveEffectStatus.Active, string? payload = null)
        {
            var reference = new ContentDefinitionRef(ContentDefinitionId.NewId(Now), 1);
            var effect = new ActiveEffect(ActiveEffectId.NewId(Now), reference, new EffectMechanicsSnapshot(reference, 1, ContentDefinitionType.Effect, payload ?? EffectJson(duration)),
                equippedSource ? ActiveEffectSourceRef.ForEquippedItem(item) : ActiveEffectSourceRef.ForItem(item), Target, status, 1, Actor, Now,
                duration == EffectDurationType.ForDuration ? UtcInstant.Parse("2026-09-12T11:00:00.0000000Z") : (UtcInstant?)null, 1);
            var record = new ActiveEffectRecord(_campaign.CampaignId, effect);
            Assert.That(_effects.CreateActiveEffect(_campaign, record, Command(), Corr).IsSuccess, Is.True);
            return record;
        }

        private long Count(string table)
        {
            using var connection = new SqliteConnection("Data Source=" + Path.Combine(_directory, "campaign.db"));
            connection.Open();
            using var query = connection.CreateCommand();
            query.CommandText = "SELECT COUNT(*) FROM " + table + ";";
            return (long)query.ExecuteScalar()!;
        }
    }
}
