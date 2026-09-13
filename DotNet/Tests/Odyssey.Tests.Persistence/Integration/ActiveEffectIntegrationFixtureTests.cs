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
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Content;
using Odyssey.Domain.Effects;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence.Integration
{
    /// <summary>
    /// ODY-S05-507: end-to-end integration fixture for the item-sourced
    /// abilities/effects block (`ODY-S05-502`-`506`). Mirrors `ODY-S05-207`'s
    /// <c>InventoryRuntimeIntegrationFixtureTests</c>, `ODY-S05-306`'s
    /// <c>EquipmentRuntimeIntegrationFixtureTests</c>, and `ODY-S05-404`'s
    /// <c>ItemDefinitionMigrationIntegrationFixtureTests</c>: no new
    /// production code, only a composition of already-accepted public
    /// services into one coherent MainGM session --
    ///   publish real `EffectDefinition`s through the real catalog lifecycle
    ///   -&gt; create an `ActiveEffect` through a genuinely real item-equip
    ///   (`EquipmentService.Equip` -&gt; `ItemEffectLifecycleService.OnItemEquipped`,
    ///   never `ActiveEffectDirectCommandService`) -&gt; exercise a real
    ///   `RequestGMResolution` conflict and its resolution
    ///   (`ActiveEffectStackingRules.ResolveStacking`/
    ///   `ResolveActiveEffectStackConflict`) -&gt; exercise a real `ForDuration`
    ///   expiry (`ActiveEffectExpiryRules.CheckForDurationExpiry` +
    ///   `IActiveEffectRepository.ExpireActiveEffect`) -&gt; suspend/resume a
    ///   `WhileItemEquipped` effect through a real `Unequip`/`Equip`
    ///   (`ItemEffectLifecycleService.OnItemUnequipped`/`OnItemEquipped`,
    ///   `SetItemEffectEquipped`) -&gt; `RemoveActiveEffect`, rejected for a
    ///   non-MainGM actor and successful for MainGM.
    ///
    /// `ODY-S05-502`-`506` each already have their own isolated unit tests;
    /// this file proves only that their already-accepted public surfaces
    /// compose into one realistic session, exactly as the backlog's own
    /// §15 row for this task specifies -- no scenario beyond that list is
    /// added, and none is skipped. No mock/fake stands in anywhere a real
    /// service already exists: every repository is a real `Sqlite*`
    /// implementation against a real temporary campaign database; the only
    /// hand-built objects are `ActiveEffectStackingRules`' own pure-function
    /// inputs, which even `ODY-S05-503`'s own unit tests already build the
    /// same way (there is no repository method that performs a stacking
    /// write, by that task's own deliberate design).
    /// </summary>
    public sealed class ActiveEffectIntegrationFixtureTests
    {
        private const string ActiveRuleset = "ruleset.core@1.0.0";
        private static readonly AnatomyProfileDefinitionId Humanoid = AnatomyProfileDefinitionId.Parse("Humanoid");
        private static readonly CorrelationId Corr = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
        private static readonly UserId MainGm = NewUserId();
        private static readonly UserId Player = NewUserId();

        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaigns = null!;
        private SqliteContentCatalogRepository _catalog = null!;
        private SqliteInventoryRepository _inventory = null!;
        private SqliteCharacterRepository _characters = null!;
        private SqliteActiveEffectRepository _effects = null!;
        private InventoryRecord _bag = null!;
        private ActiveEffectTargetRef _target;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s05-507-" + Guid.NewGuid().ToString("N"));
            _campaigns = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> created = _campaigns.Create(new CreateCampaignRequest(_campaignDir, "ActiveEffect Integration Fixture", "ruleset.core", "1.0.0", "0.1.0"), NewCommandId(), Corr);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _catalog = new SqliteContentCatalogRepository(Clock);
            _inventory = new SqliteInventoryRepository(Clock);
            _characters = new SqliteCharacterRepository(Clock,
                deletionDependencyCheckers: new ICharacterDeletionDependencyChecker[] { new InventoryCharacterDeletionDependencyChecker(_inventory) },
                bodyPartRemovalDependencyCheckers: new IBodyPartRemovalDependencyChecker[] { new InventoryBodyPartRemovalDependencyChecker(_inventory) });
            _effects = new SqliteActiveEffectRepository(Clock);

            CharacterRecord character = CreateInitializedCharacter();
            _bag = new InventoryRecord(InventoryId.NewId(Clock.GetUtcNow()), _campaign.CampaignId, InventoryOwnerRef.ForCharacter(character.CharacterId), 1, Clock.GetUtcNow(), Clock.GetUtcNow());
            Assert.That(_inventory.CreateInventory(_campaign, _bag, NewCommandId(), Corr).IsSuccess, Is.True);
            _target = ActiveEffectTargetRef.ForCharacter(character.CharacterId);
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaigns.Close(_campaign, Corr); } catch (IOException) { }
            using (var pool = new SqliteConnection("Data Source=" + Path.Combine(_campaignDir, "campaign.db")))
                SqliteConnection.ClearPool(pool);
            try { if (Directory.Exists(_campaignDir)) Directory.Delete(_campaignDir, true); } catch (IOException) { }
        }

        [Test] // TC-ACTIVEEFFECT-079
        public void EquippingAnItemWithBuiltInEffectRefs_CreatesARealActiveEffect_ThroughItemEffectLifecycleService()
        {
            // 1. Publish a real WhileItemEquipped EffectDefinition through the
            //    real catalog lifecycle (ODY-S05-101/102/103's own already-accepted
            //    Draft -> Published cycle).
            ContentDefinitionRecord effectDefinition = PublishEffect(EffectDurationType.WhileItemEquipped, EffectStackPolicy.IndependentInstances);

            // 2. Create a real Item catalog definition whose BuiltInEffectRefs
            //    names that published effect, then a real ItemInstance from it
            //    through the already-accepted InventoryCreationService.
            ItemInstanceRecord instance = CreateItemInstanceWithBuiltInEffects("slot_a", effectDefinition);

            // 3. Equip it for real through EquipmentService.Equip (ODY-S05-303),
            //    then react through ItemEffectLifecycleService.OnItemEquipped
            //    (ODY-S05-505) -- never ActiveEffectDirectCommandService, since
            //    this is item-triggered creation, not direct GM creation.
            EquippedEntryRecord equipped = Equip(instance, "slot_a", Array.Empty<BodyPartId>());
            Result<System.Collections.Generic.IReadOnlyList<ActiveEffectRecord>> reaction =
                ItemEffectLifecycleService.OnItemEquipped(_inventory, _catalog, _effects, _campaign, equipped, _target, NewCommandId(), Corr);

            Assert.That(reaction.IsSuccess, Is.True, reaction.IsFailure ? reaction.Error.Code.ToString() : string.Empty);
            Assert.That(reaction.Value.Count, Is.EqualTo(1));
            ActiveEffectRecord created = reaction.Value[0];
            Assert.That(created.Effect.Status, Is.EqualTo(ActiveEffectStatus.Active));
            Assert.That(created.Effect.SourceRef.Kind, Is.EqualTo(ActiveEffectSourceKind.EquippedItem));
            Assert.That(created.Effect.TargetRef, Is.EqualTo(_target));

            // Prove real persistence with a raw SQL SELECT, not just the
            // in-memory return value (ODY-S05-207's own fixture idiom).
            Assert.That(CountRows("SELECT COUNT(*) FROM ActiveEffect WHERE ActiveEffectId = '" + created.Effect.ActiveEffectId + "' AND Status = 'Active';"), Is.EqualTo(1L));
        }

        [Test] // TC-ACTIVEEFFECT-080
        public void UnequipThenReequip_SuspendsThenResumesTheSameWhileItemEquippedRow_ThroughRealEquipmentCommands()
        {
            ContentDefinitionRecord effectDefinition = PublishEffect(EffectDurationType.WhileItemEquipped, EffectStackPolicy.IndependentInstances);
            ItemInstanceRecord instance = CreateItemInstanceWithBuiltInEffects("slot_a", effectDefinition);
            EquippedEntryRecord equipped = Equip(instance, "slot_a", Array.Empty<BodyPartId>());
            ActiveEffectRecord created = ItemEffectLifecycleService.OnItemEquipped(_inventory, _catalog, _effects, _campaign, equipped, _target, NewCommandId(), Corr).Value.Single();

            // Unequip for real (ODY-S05-304), then react -- only WhileItemEquipped
            // effects sourced from this item suspend, never Expired/Removed.
            Unequip(instance, equipped);
            Result<System.Collections.Generic.IReadOnlyList<ActiveEffectRecord>> suspension =
                ItemEffectLifecycleService.OnItemUnequipped(_effects, _campaign, equipped.Entry.ItemRef, MainGm, NewCommandId(), Corr);
            Assert.That(suspension.IsSuccess, Is.True);
            Assert.That(suspension.Value.Single().Effect.Status, Is.EqualTo(ActiveEffectStatus.Suspended));
            Assert.That(CountRows("SELECT COUNT(*) FROM ActiveEffect WHERE ActiveEffectId = '" + created.Effect.ActiveEffectId + "' AND Status = 'Suspended';"), Is.EqualTo(1L));

            // Re-equip the same item instance for real -- OnItemEquipped must
            // resume the same row, never create a second one.
            ItemInstanceRecord refreshed = _inventory.GetItemInstance(_campaign, instance.ItemInstanceId, Corr).Value;
            EquippedEntryRecord reequipped = Equip(refreshed, "slot_a", Array.Empty<BodyPartId>());
            Result<System.Collections.Generic.IReadOnlyList<ActiveEffectRecord>> resumption =
                ItemEffectLifecycleService.OnItemEquipped(_inventory, _catalog, _effects, _campaign, reequipped, _target, NewCommandId(), Corr);

            Assert.That(resumption.IsSuccess, Is.True, resumption.IsFailure ? resumption.Error.Code.ToString() : string.Empty);
            Assert.That(resumption.Value.Single().Effect.ActiveEffectId, Is.EqualTo(created.Effect.ActiveEffectId), "re-equip resumes the same row, never a duplicate");
            Assert.That(CountRows("SELECT COUNT(*) FROM ActiveEffect;"), Is.EqualTo(1L), "exactly one ActiveEffect row must exist for this item's own effect across the whole suspend/resume cycle");
            Assert.That(CountRows("SELECT COUNT(*) FROM ActiveEffect WHERE ActiveEffectId = '" + created.Effect.ActiveEffectId + "' AND Status = 'Active';"), Is.EqualTo(1L));
        }

        [Test] // TC-ACTIVEEFFECT-081
        public void ReapplyingAnEffectWithRequestGMResolution_RaisesARealConflict_ResolvedThroughARealSecondRow()
        {
            // A second, independent item whose own built-in effect uses
            // RequestGMResolution -- kept on a separate item from the
            // WhileItemEquipped scenario above so the two demonstrated
            // behaviors never interfere with each other's own state.
            ContentDefinitionRecord effectDefinition = PublishEffect(EffectDurationType.Permanent, EffectStackPolicy.RequestGMResolution);
            ItemInstanceRecord instance = CreateItemInstanceWithBuiltInEffects("slot_b", effectDefinition);
            EquippedEntryRecord equipped = Equip(instance, "slot_b", Array.Empty<BodyPartId>());
            ActiveEffectRecord firstApplication = ItemEffectLifecycleService.OnItemEquipped(_inventory, _catalog, _effects, _campaign, equipped, _target, NewCommandId(), Corr).Value.Single();

            // A second, genuinely real use of the same item's own effect (the
            // exact scenario ODY-S05-503's own decision layer exists for,
            // and which OnItemEquipped itself deliberately refuses to
            // silently bypass for a non-IndependentInstances policy --
            // ItemEffectLifecycleService.cs's own comment: "503 currently
            // provides decisions, not atomic stacking writes. Do not
            // silently bypass a policy that requires those writes."). The
            // host composes ODY-S05-503's own decision layer directly, the
            // same way ActiveEffectStackingRulesTests.cs (ODY-S05-503) itself
            // already does, against this genuinely persisted first row.
            ContentDefinitionRecord freshDefinition = _catalog.GetContentDefinition(_campaign, effectDefinition.ContentDefinitionId, Corr).Value;
            var effectRef = new ContentDefinitionRef(freshDefinition.ContentDefinitionId, freshDefinition.Version);
            var snapshot = new EffectMechanicsSnapshot(effectRef, freshDefinition.Version, freshDefinition.DefinitionType, freshDefinition.PropertiesJson);
            var candidate = new ActiveEffectRecord(_campaign.CampaignId, new ActiveEffect(
                ActiveEffectId.NewId(Clock.GetUtcNow()), effectRef, snapshot, firstApplication.Effect.SourceRef, _target,
                ActiveEffectStatus.Active, 1, MainGm, Clock.GetUtcNow(), null, 1));

            Result<ActiveEffectRecord> existing = _effects.GetActiveEffect(_campaign, firstApplication.Effect.ActiveEffectId, Corr);
            Assert.That(existing.IsSuccess, Is.True);

            ActiveEffectStackDecision decision = ActiveEffectStackingRules.ResolveStacking(EffectStackPolicy.RequestGMResolution, existing.Value, candidate, Clock.GetUtcNow());
            Assert.That(decision.Kind, Is.EqualTo(ActiveEffectStackDecisionKind.RequestGmResolution));
            Assert.That(decision.Conflict, Is.Not.Null);
            Assert.That(decision.Conflict!.ConflictingActiveEffectId, Is.EqualTo(firstApplication.Effect.ActiveEffectId));

            // MainGM resolves the pending conflict as a real, independent
            // second instance (ADR-028 section 7 rule 7's own
            // ApplyAsIndependentInstance outcome).
            ActiveEffectStackDecision resolution = ActiveEffectStackingRules.ResolveActiveEffectStackConflict(decision.Conflict, ActiveEffectStackConflictResolution.ApplyAsIndependentInstance);
            Assert.That(resolution.Kind, Is.EqualTo(ActiveEffectStackDecisionKind.CreateNewEffect));

            Result<ActiveEffectRecord> secondApplication = _effects.CreateActiveEffect(_campaign, resolution.EffectToCreate!, NewCommandId(), Corr);
            Assert.That(secondApplication.IsSuccess, Is.True, secondApplication.IsFailure ? secondApplication.Error.Code.ToString() : string.Empty);

            // Both rows are real, independent, and both genuinely persisted.
            Result<System.Collections.Generic.IReadOnlyList<ActiveEffectRecord>> byTarget = _effects.ListActiveEffectsByTarget(_campaign, _campaign.CampaignId, _target, Corr);
            Assert.That(byTarget.Value.Count(r => r.Effect.EffectDefinitionRef.Equals(effectRef)), Is.EqualTo(2));
            Assert.That(CountRows("SELECT COUNT(*) FROM ActiveEffect WHERE EffectDefinitionRef = '" + effectRef + "';"), Is.EqualTo(2L));
        }

        [Test] // TC-ACTIVEEFFECT-082
        public void ForDurationEffect_ExpiresForReal_ThroughCheckForDurationExpiryAndExpireActiveEffect()
        {
            UtcInstant appliedAt = Clock.GetUtcNow();
            UtcInstant expiresAt = appliedAt.Add(TimeSpan.FromHours(1));
            ContentDefinitionRecord effectDefinition = PublishEffect(EffectDurationType.ForDuration, EffectStackPolicy.IndependentInstances);
            ItemInstanceRecord instance = CreateItemInstanceWithBuiltInEffects("slot_c", effectDefinition);
            EquippedEntryRecord equipped = Equip(instance, "slot_c", Array.Empty<BodyPartId>());

            Result<System.Collections.Generic.IReadOnlyList<ActiveEffectRecord>> created = ItemEffectLifecycleService.OnItemEquipped(
                _inventory, _catalog, _effects, _campaign, equipped, _target, NewCommandId(), Corr,
                resolveExpiry: (snapshot, now) => Result<UtcInstant>.Success(expiresAt));
            Assert.That(created.IsSuccess, Is.True, created.IsFailure ? created.Error.Code.ToString() : string.Empty);
            ActiveEffectRecord effect = created.Value.Single();
            Assert.That(effect.Effect.ExpiresAt, Is.EqualTo(expiresAt));

            // Before ExpiresAt: the real duration-check function reports NotExpired.
            UtcInstant beforeExpiry = appliedAt.Add(TimeSpan.FromMinutes(30));
            Assert.That(ActiveEffectExpiryRules.CheckForDurationExpiry(effect.Effect.ExpiresAt, beforeExpiry), Is.EqualTo(ActiveEffectExpiryDecision.NotExpired));
            Assert.That(_effects.GetActiveEffect(_campaign, effect.Effect.ActiveEffectId, Corr).Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Active));

            // At/after ExpiresAt: the real check reports Expired, and the
            // host applies that decision through the real ExpireActiveEffect.
            UtcInstant afterExpiry = appliedAt.Add(TimeSpan.FromHours(2));
            Assert.That(ActiveEffectExpiryRules.CheckForDurationExpiry(effect.Effect.ExpiresAt, afterExpiry), Is.EqualTo(ActiveEffectExpiryDecision.Expired));

            Result<ActiveEffectRecord> expired = _effects.ExpireActiveEffect(_campaign, _campaign.CampaignId, effect.Effect.ActiveEffectId, effect.Effect.Revision, NewCommandId(), Corr);
            Assert.That(expired.IsSuccess, Is.True, expired.IsFailure ? expired.Error.Code.ToString() : string.Empty);
            Assert.That(expired.Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Expired));

            // The row survives as history, never physically deleted.
            Assert.That(CountRows("SELECT COUNT(*) FROM ActiveEffect WHERE ActiveEffectId = '" + effect.Effect.ActiveEffectId + "' AND Status = 'Expired';"), Is.EqualTo(1L));
        }

        [Test] // TC-ACTIVEEFFECT-083
        public void RemoveActiveEffect_ByNonMainGm_IsRejected_WithNoMutationInTheRealDatabase()
        {
            ContentDefinitionRecord effectDefinition = PublishEffect(EffectDurationType.Permanent, EffectStackPolicy.IndependentInstances);
            ItemInstanceRecord instance = CreateItemInstanceWithBuiltInEffects("slot_d", effectDefinition);
            EquippedEntryRecord equipped = Equip(instance, "slot_d", Array.Empty<BodyPartId>());
            ActiveEffectRecord effect = ItemEffectLifecycleService.OnItemEquipped(_inventory, _catalog, _effects, _campaign, equipped, _target, NewCommandId(), Corr).Value.Single();
            long domainEventsBefore = CountRows("SELECT COUNT(*) FROM DomainEvents;");

            Result<ActiveEffectRecord> rejected = _effects.RemoveActiveEffect(_campaign, _campaign.CampaignId, effect.Effect.ActiveEffectId, Player, actorIsMainGm: false, effect.Effect.Revision, NewCommandId(), Corr);

            Assert.That(rejected.IsFailure, Is.True);
            Assert.That(rejected.Error.Code, Is.EqualTo(ErrorCodes.PersistenceActiveEffectOperationDenied));
            Assert.That(CountRows("SELECT COUNT(*) FROM ActiveEffect WHERE ActiveEffectId = '" + effect.Effect.ActiveEffectId + "' AND Status = 'Active' AND Revision = 1;"), Is.EqualTo(1L),
                "a denied removal must leave the real row completely unmutated");
            Assert.That(CountRows("SELECT COUNT(*) FROM DomainEvents;"), Is.EqualTo(domainEventsBefore), "a denied removal must not even open the apply transaction, let alone append a DomainEvents row");
        }

        [Test] // TC-ACTIVEEFFECT-084
        public void RemoveActiveEffect_ByMainGm_Succeeds_TransitioningTheRealRowToRemoved()
        {
            ContentDefinitionRecord effectDefinition = PublishEffect(EffectDurationType.Permanent, EffectStackPolicy.IndependentInstances);
            ItemInstanceRecord instance = CreateItemInstanceWithBuiltInEffects("slot_e", effectDefinition);
            EquippedEntryRecord equipped = Equip(instance, "slot_e", Array.Empty<BodyPartId>());
            ActiveEffectRecord effect = ItemEffectLifecycleService.OnItemEquipped(_inventory, _catalog, _effects, _campaign, equipped, _target, NewCommandId(), Corr).Value.Single();

            Result<ActiveEffectRecord> removed = _effects.RemoveActiveEffect(_campaign, _campaign.CampaignId, effect.Effect.ActiveEffectId, MainGm, actorIsMainGm: true, effect.Effect.Revision, NewCommandId(), Corr);

            Assert.That(removed.IsSuccess, Is.True, removed.IsFailure ? removed.Error.Code.ToString() : string.Empty);
            Assert.That(removed.Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Removed));
            Assert.That(removed.Value.Effect.Revision, Is.EqualTo(effect.Effect.Revision + 1));
            Assert.That(CountRows("SELECT COUNT(*) FROM ActiveEffect WHERE ActiveEffectId = '" + effect.Effect.ActiveEffectId + "' AND Status = 'Removed';"), Is.EqualTo(1L),
                "the row survives as Status=Removed history, never a physical delete");
        }

        // ---- helpers (private methods, no new production type) ----

        private CharacterRecord CreateInitializedCharacter()
        {
            Result<CharacterRecord> created = _characters.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "ActiveEffect Integration Fixture Character"), NewCommandId(), Corr);
            Assert.That(created.IsSuccess, Is.True);
            Result<CharacterRecord> initialized = _characters.InitializeCharacterAnatomy(_campaign, created.Value.CharacterId, Humanoid, NewUserId(), actorIsMainGm: true, created.Value.Revisions.CharacterAnatomyRevision, NewCommandId(), Corr);
            Assert.That(initialized.IsSuccess, Is.True);
            return initialized.Value;
        }

        private ItemInstanceRecord CreateItemInstanceWithBuiltInEffects(string tag, params ContentDefinitionRecord[] effects)
        {
            var effectRefs = effects.Select(e => new ContentDefinitionRef(e.ContentDefinitionId, e.Version)).ToArray();
            var item = new ItemDefinition(ItemCategory.Generic, isStackable: false, maxStackSize: null, weight: 1, hasDurability: false, maxDurability: null, hasCharges: false, maxCharges: null, Array.Empty<ContentDefinitionRef>(), effectRefs);
            ContentDefinitionRecord published = PublishDefinition(ContentDefinitionType.Item, "Integration Fixture Item " + tag, TypedDefinitionCodec.EncodeItem(item));

            Result<ItemInstanceRecord> created = InventoryCreationService.CreateItemInstanceFromDefinition(_catalog, _inventory, Clock, new CreateItemInstanceFromDefinitionRequest(
                _campaign, ItemInstanceId.NewId(Clock.GetUtcNow()), _bag.InventoryId, _bag.OwnerRef,
                InventoryLocationRef.Contained(_bag.InventoryId, "main"), published.ContentDefinitionId, NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(created.IsSuccess, Is.True, created.IsFailure ? created.Error.Code.ToString() : string.Empty);
            return created.Value;
        }

        private ContentDefinitionRecord PublishEffect(EffectDurationType duration, EffectStackPolicy stackPolicy)
        {
            string json = TypedDefinitionCodec.EncodeEffect(new EffectDefinition(
                new ContentTargetRule(ContentTargetSource.ActingCharacter, 1, 1, true), duration,
                duration == EffectDurationType.ForDuration ? 1 : (long?)null, stackPolicy, null));
            return PublishDefinition(ContentDefinitionType.Effect, "Integration Fixture Effect " + duration + "/" + stackPolicy, json);
        }

        private ContentDefinitionRecord PublishDefinition(ContentDefinitionType type, string name, string propertiesJson)
        {
            Result<ContentDefinitionRecord> draft = ContentCatalogAuthoringService.CreateDraftDefinition(_catalog, new CreateDraftDefinitionRequest(
                _campaign, type, name, "ODY-S05-507 integration fixture", NewUserId(), actorIsMainGm: true, NewCommandId(), Corr,
                rulesetCompatibility: new[] { ActiveRuleset }, propertiesJson: propertiesJson));
            Assert.That(draft.IsSuccess, Is.True, draft.IsFailure ? draft.Error.Code.ToString() : string.Empty);
            Result<ContentDefinitionRecord> published = ContentCatalogLifecycleService.PublishDefinition(_catalog, new PublishDefinitionRequest(
                _campaign, draft.Value.ContentDefinitionId, draft.Value.Revision, NewUserId(), actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(published.IsSuccess, Is.True, published.IsFailure ? published.Error.Code.ToString() : string.Empty);
            return published.Value;
        }

        private EquippedEntryRecord Equip(ItemInstanceRecord instance, string slotKey, BodyPartId[] bodyParts)
        {
            Result<EquippedEntryRecord> result = EquipmentService.Equip(_inventory, _characters, new EquipRequest(
                _campaign, InventoryItemRef.ForInstance(instance.ItemInstanceId), _bag.InventoryId, instance.Revision,
                slotKey, bodyParts, MainGm, Clock.GetUtcNow(), MainGm, actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error.Code.ToString() : string.Empty);
            return result.Value;
        }

        private void Unequip(ItemInstanceRecord instance, EquippedEntryRecord equipped)
        {
            ItemInstanceRecord current = _inventory.GetItemInstance(_campaign, instance.ItemInstanceId, Corr).Value;
            Result<bool> result = EquipmentService.Unequip(_inventory, new UnequipRequest(
                _campaign, InventoryItemRef.ForInstance(instance.ItemInstanceId), _bag.InventoryId,
                current.Revision, equipped.Entry.Revision, "main", MainGm, actorIsMainGm: true, NewCommandId(), Corr));
            Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error.Code.ToString() : string.Empty);
        }

        private long CountRows(string sql)
        {
            using SqliteConnection connection = OpenRawConnection();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return (long)command.ExecuteScalar()!;
        }

        private SqliteConnection OpenRawConnection()
        {
            var connection = new SqliteConnection("Data Source=" + Path.Combine(_campaignDir, "campaign.db"));
            connection.Open();
            return connection;
        }
    }
}
