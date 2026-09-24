using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.CharacterAdvancement;
using Odyssey.Application.Commands;
using Odyssey.Application.Content;
using Odyssey.Application.Effects;
using Odyssey.Application.Inventory;
using Odyssey.Application.Persistence;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Content;
using Odyssey.Domain.Effects;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S06-107: real, end-to-end tests for `UseItem` -- `ADR-030` §10's own root command (block D of the
    /// MVP roadmap). Every test goes through the real `UseItemService.UseItem` production entry point against
    /// a real temp-directory SQLite campaign -- never a direct unit call to a private method. Built from the
    /// start to `ODY-S06-106`'s FINAL, thrice-revised idempotency/compensation rigor (per this task's own
    /// governing ТЗ §0), so `TC-USEITEM-005`/`006` mirror `TC-ABILITY-013`/`014` directly rather than waiting
    /// for a future doработка to add them.
    /// </summary>
    public sealed class UseItemIntegrationTests
    {
        private const string ActiveRuleset = "ruleset.core@1.0.0";
        private static readonly CorrelationId Corr = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly RngKeyEpochId Epoch = RngKeyEpochId.Parse("epoch-001");
        private static readonly ResourceDefinitionId Health = ResourceDefinitionId.Parse("health");

        private string _root = null!;
        private CampaignHandle _campaign = null!;
        private IWallClock _clock = null!;
        private SqliteCharacterRepository _characters = null!;
        private SqliteContentCatalogRepository _catalog = null!;
        private SqliteActiveEffectRepository _effects = null!;
        private SqliteInventoryRepository _inventory = null!;
        private SqliteUseItemStateReader _reader = null!;
        private SqliteUseItemRepository _apply = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "ody-s06-107-" + Guid.NewGuid().ToString("N"));
            _clock = new SystemWallClock();
            Result<CampaignHandle> campaign = new SqliteCampaignRepository(_clock).Create(new CreateCampaignRequest(_root, "use-item", "ruleset.core", "1.0.0", "0.1.0"), Command(), Corr);
            Assert.That(campaign.IsSuccess, Is.True);
            _campaign = campaign.Value;
            _characters = new SqliteCharacterRepository(_clock);
            _catalog = new SqliteContentCatalogRepository(_clock);
            _effects = new SqliteActiveEffectRepository(_clock);
            _inventory = new SqliteInventoryRepository(_clock);
            _reader = new SqliteUseItemStateReader(_characters, _inventory, _catalog, _clock);
            _apply = new SqliteUseItemRepository(_clock, _effects);
        }

        [Test] // TC-USEITEM-001
        public void UseItem_StackWithQuantityGreaterThanOne_ConsumesExactlyOneUnit_AppliesAdjustResource()
        {
            CharacterId actor = Active("actor");
            InitResource(actor, Health);
            InventoryRecord inventory = CreateInventory(actor);
            ContentDefinitionRecord effect = PublishInstantEffect(Health, "-3");
            ItemStackRecord stack = CreateStack(inventory, quantity: 4, effect);

            Result<ItemUsageRecord> result = UseItemService.UseItem(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, Request(actor, InventoryItemRef.ForStack(stack.ItemStackId), stack.Revision, inventory.Revision));

            Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error.Code.ToString() : string.Empty);
            Assert.That(CurrentValue(actor, Health), Is.EqualTo(7), "10 - 3 = 7.");
            Result<ItemStackRecord> reread = _inventory.GetItemStack(_campaign, stack.ItemStackId, Corr);
            Assert.That(reread.IsSuccess, Is.True);
            Assert.That(reread.Value.Quantity.Value, Is.EqualTo(3), "Exactly one unit must be consumed.");
        }

        [Test] // TC-USEITEM-002
        public void UseItem_StackWithQuantityOne_DeletesTheRowEntirely()
        {
            CharacterId actor = Active("actor");
            InitResource(actor, Health);
            InventoryRecord inventory = CreateInventory(actor);
            ContentDefinitionRecord effect = PublishInstantEffect(Health, "-2");
            ItemStackRecord stack = CreateStack(inventory, quantity: 1, effect);

            Result<ItemUsageRecord> result = UseItemService.UseItem(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, Request(actor, InventoryItemRef.ForStack(stack.ItemStackId), stack.Revision, inventory.Revision));

            Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error.Code.ToString() : string.Empty);
            Assert.That(CurrentValue(actor, Health), Is.EqualTo(8));
            Result<ItemStackRecord> reread = _inventory.GetItemStack(_campaign, stack.ItemStackId, Corr);
            Assert.That(reread.IsFailure, Is.True, "The last unit's own use must delete the stack row (ItemStackQuantity cannot represent zero).");
        }

        [Test] // TC-USEITEM-003
        public void UseItem_ItemWithMultipleInstantEffects_AllAppliedAtomically()
        {
            CharacterId actor = Active("actor");
            InitResource(actor, Health);
            InventoryRecord inventory = CreateInventory(actor);
            ContentDefinitionRecord first = PublishInstantEffect(Health, "-2");
            ContentDefinitionRecord second = PublishInstantEffect(Health, "-1");
            ItemStackRecord stack = CreateStack(inventory, quantity: 1, first, second);

            Result<ItemUsageRecord> result = UseItemService.UseItem(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, Request(actor, InventoryItemRef.ForStack(stack.ItemStackId), stack.Revision, inventory.Revision));

            Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error.Code.ToString() : string.Empty);
            Assert.That(CurrentValue(actor, Health), Is.EqualTo(7), "10 - 2 - 1 = 7 -- both Instant effects applied.");
        }

        [Test] // TC-USEITEM-004
        public void UseItem_ItemWithInstantAndWhileEquippedEffects_OnlyInstantIsApplied()
        {
            CharacterId actor = Active("actor");
            InitResource(actor, Health);
            InventoryRecord inventory = CreateInventory(actor);
            ContentDefinitionRecord instant = PublishInstantEffect(Health, "-4");
            ContentDefinitionRecord whileEquipped = PublishWhileEquippedEffect();
            ItemStackRecord stack = CreateStack(inventory, quantity: 1, instant, whileEquipped);

            Result<ItemUsageRecord> result = UseItemService.UseItem(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, Request(actor, InventoryItemRef.ForStack(stack.ItemStackId), stack.Revision, inventory.Revision));

            Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error.Code.ToString() : string.Empty);
            Assert.That(CurrentValue(actor, Health), Is.EqualTo(6), "Only the Instant effect's own -4 applies.");
            Assert.That(result.Value.AppliedEffectRefs.Count, Is.EqualTo(0), "Neither built-in effect declares an ApplyEffect primitive here -- the Instant one only adjusts a resource; WhileItemEquipped is not even decoded past its own DurationType filter.");
        }

        [Test] // TC-USEITEM-005
        public void UseItem_SecondApplyEffectFailsAfterFirstSucceeds_EverythingIsCompensated()
        {
            CharacterId actor = Active("actor");
            InventoryRecord inventory = CreateInventory(actor);
            ContentDefinitionRecord firstApplied = PublishFixtureAppliedEffect();
            ContentDefinitionRecord secondApplied = PublishFixtureAppliedEffect();
            ContentDefinitionRecord itemEffect1 = PublishApplyEffectEffect(firstApplied);
            ContentDefinitionRecord itemEffect2 = PublishApplyEffectEffect(secondApplied);
            ItemStackRecord stack = CreateStack(inventory, quantity: 2, itemEffect1, itemEffect2);

            Result<ContentDefinitionRecord> archived = ContentCatalogLifecycleService.ArchiveDefinition(_catalog, new ArchiveDefinitionRequest(_campaign, secondApplied.ContentDefinitionId, "test archive", actorIsMainGm: true, Command(), Corr));
            Assert.That(archived.IsSuccess, Is.True, archived.IsFailure ? archived.Error.Code.ToString() : string.Empty);

            UseItemRequest request = Request(actor, InventoryItemRef.ForStack(stack.ItemStackId), stack.Revision, inventory.Revision);
            Result<ItemUsageRecord> result = UseItemService.UseItem(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, request);

            Assert.That(result.IsFailure, Is.True, "The second, archived ApplyEffect must fail the whole use.");
            Result<ItemStackRecord> reread = _inventory.GetItemStack(_campaign, stack.ItemStackId, Corr);
            Assert.That(reread.IsSuccess, Is.True);
            Assert.That(reread.Value.Quantity.Value, Is.EqualTo(2), "The consumed unit must be restored -- consume decrements then restore increments back, so the row's own Revision legitimately advances by 2 (like any other reversed delta in this codebase, e.g. ActivateAbility's own resource-delta reversal), even though the observable Quantity is identical to before.");
            Assert.That(CountStillAttached(actor), Is.EqualTo(0), "The FIRST effect, already successfully created, must be removed too -- not left behind.");

            Result<ItemUsageRecord> retried = UseItemService.UseItem(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, request);
            Assert.That(retried.IsFailure, Is.True, "A retry of a compensated CommandId must remain a permanent failure -- never a silent success.");
        }

        [Test] // TC-USEITEM-006
        public void UseItem_CompensationItselfFailsPartway_RetryDoesNotSilentlySucceed()
        {
            CharacterId actor = Active("actor");
            InventoryRecord inventory = CreateInventory(actor);
            ContentDefinitionRecord firstApplied = PublishFixtureAppliedEffect();
            ContentDefinitionRecord secondApplied = PublishFixtureAppliedEffect();
            ContentDefinitionRecord thirdApplied = PublishFixtureAppliedEffect();
            ContentDefinitionRecord itemEffect1 = PublishApplyEffectEffect(firstApplied);
            ContentDefinitionRecord itemEffect2 = PublishApplyEffectEffect(secondApplied);
            ContentDefinitionRecord itemEffect3 = PublishApplyEffectEffect(thirdApplied);
            ItemStackRecord stack = CreateStack(inventory, quantity: 2, itemEffect1, itemEffect2, itemEffect3);

            Result<ContentDefinitionRecord> archived = ContentCatalogLifecycleService.ArchiveDefinition(_catalog, new ArchiveDefinitionRequest(_campaign, thirdApplied.ContentDefinitionId, "test archive", actorIsMainGm: true, Command(), Corr));
            Assert.That(archived.IsSuccess, Is.True, archived.IsFailure ? archived.Error.Code.ToString() : string.Empty);

            UseItemRequest request = Request(actor, InventoryItemRef.ForStack(stack.ItemStackId), stack.Revision, inventory.Revision);

            var failingEffects = new FailsNthRemovalActiveEffectRepository(_effects, failOnCallNumber: 2);
            var apply1 = new SqliteUseItemRepository(_clock, failingEffects);
            Result<ItemUsageRecord> first = UseItemService.UseItem(_reader, apply1, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, request);
            Assert.That(first.IsFailure, Is.True);
            Assert.That(CountStillAttached(actor), Is.EqualTo(1), "The second effect's own removal was made to fail -- exactly one of the two already-created effects must still be attached.");
            Result<ItemStackRecord> midway = _inventory.GetItemStack(_campaign, stack.ItemStackId, Corr);
            Assert.That(midway.IsSuccess, Is.True);
            Assert.That(midway.Value.Quantity.Value, Is.EqualTo(1), "Compensation failed before ever reaching item restoration -- the unit is still consumed.");

            var apply2 = new SqliteUseItemRepository(_clock, _effects);
            Result<ItemUsageRecord> retried = UseItemService.UseItem(_reader, apply2, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, request);
            Assert.That(retried.IsFailure, Is.True, "A retry after an incomplete compensation attempt must never be reported as Success, even once the resumed compensation now completes.");
            Assert.That(CountStillAttached(actor), Is.EqualTo(0), "The resumed compensation must finish removing the second effect too.");
            Result<ItemStackRecord> final = _inventory.GetItemStack(_campaign, stack.ItemStackId, Corr);
            Assert.That(final.IsSuccess, Is.True);
            Assert.That(final.Value.Quantity.Value, Is.EqualTo(2), "The resumed compensation must finish restoring the consumed unit too.");
        }

        /// <summary>
        /// ODY-S06-107 doработка (product-owner-ordered, after independent verification found the
        /// delete-and-reinsert restore branch of `RestoreConsumedItemUnitInTransaction` had zero test
        /// coverage): a stack with quantity 1 -- consumption physically deletes the row (`ItemStackQuantity`
        /// cannot represent zero), so compensation must go through the delete-and-reinsert branch, not the
        /// decrement/increment branch `TC-USEITEM-005`/`006` already cover. Verifies the restored row's own
        /// `Revision` equals the PRE-consumption value exactly (`prior.Revision`, no artificial advance) --
        /// unlike the decrement branch, which legitimately advances by 2 across a full round trip.
        /// </summary>
        [Test] // TC-USEITEM-011
        public void UseItem_FullyConsumedStack_CompensationRestoresExactPriorRecord_RevisionUnchanged()
        {
            CharacterId actor = Active("actor");
            InventoryRecord inventory = CreateInventory(actor);
            ContentDefinitionRecord firstApplied = PublishFixtureAppliedEffect();
            ContentDefinitionRecord secondApplied = PublishFixtureAppliedEffect();
            ContentDefinitionRecord itemEffect1 = PublishApplyEffectEffect(firstApplied);
            ContentDefinitionRecord itemEffect2 = PublishApplyEffectEffect(secondApplied);
            ItemStackRecord stack = CreateStack(inventory, quantity: 1, itemEffect1, itemEffect2);

            Result<ContentDefinitionRecord> archived = ContentCatalogLifecycleService.ArchiveDefinition(_catalog, new ArchiveDefinitionRequest(_campaign, secondApplied.ContentDefinitionId, "test archive", actorIsMainGm: true, Command(), Corr));
            Assert.That(archived.IsSuccess, Is.True, archived.IsFailure ? archived.Error.Code.ToString() : string.Empty);

            UseItemRequest request = Request(actor, InventoryItemRef.ForStack(stack.ItemStackId), stack.Revision, inventory.Revision);
            Result<ItemUsageRecord> result = UseItemService.UseItem(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, request);

            Assert.That(result.IsFailure, Is.True, "The second, archived ApplyEffect must fail the whole use.");
            Result<ItemStackRecord> reread = _inventory.GetItemStack(_campaign, stack.ItemStackId, Corr);
            Assert.That(reread.IsSuccess, Is.True, "The deleted stack row must be re-inserted by compensation.");
            Assert.That(reread.Value.ItemStackId, Is.EqualTo(stack.ItemStackId));
            Assert.That(reread.Value.InventoryId, Is.EqualTo(stack.InventoryId));
            Assert.That(reread.Value.OwnerRef, Is.EqualTo(stack.OwnerRef));
            Assert.That(reread.Value.Quantity.Value, Is.EqualTo(1), "The exact prior quantity must be restored.");
            Assert.That(reread.Value.Revision, Is.EqualTo(stack.Revision), "The delete-and-reinsert restore branch writes back prior.Revision exactly -- unlike the decrement/increment branch (TC-USEITEM-005/006), which legitimately advances by 2, this branch never artificially advances the revision at all.");
            Assert.That(CountStillAttached(actor), Is.EqualTo(0), "The already-created first effect must be removed too.");

            Result<ItemUsageRecord> retried = UseItemService.UseItem(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, request);
            Assert.That(retried.IsFailure, Is.True, "A retry of a compensated CommandId must remain a permanent failure -- never a silent success.");
        }

        /// <summary>
        /// ODY-S06-107 doработка (product-owner-ordered, same finding as `TC-USEITEM-011`): a non-stackable
        /// `ItemInstanceRecord` (no `HasCharges`) -- consumption always deletes the instance outright (a
        /// single use consumes the whole item), so compensation always goes through the delete-and-reinsert
        /// branch for instances. Verifies the restored instance is the exact prior record, including its own
        /// unchanged `Revision`.
        /// </summary>
        [Test] // TC-USEITEM-012
        public void UseItem_NonStackableInstance_CompensationRestoresExactPriorRecord_RevisionUnchanged()
        {
            CharacterId actor = Active("actor");
            InventoryRecord inventory = CreateInventory(actor);
            ContentDefinitionRecord firstApplied = PublishFixtureAppliedEffect();
            ContentDefinitionRecord secondApplied = PublishFixtureAppliedEffect();
            ContentDefinitionRecord itemEffect1 = PublishApplyEffectEffect(firstApplied);
            ContentDefinitionRecord itemEffect2 = PublishApplyEffectEffect(secondApplied);
            var effectRefs = new[] { new ContentDefinitionRef(itemEffect1.ContentDefinitionId, itemEffect1.Version), new ContentDefinitionRef(itemEffect2.ContentDefinitionId, itemEffect2.Version) };
            var itemDefinition = new ItemDefinition(ItemCategory.Consumable, false, null, 1, false, null, false, null, Array.Empty<ContentDefinitionRef>(), effectRefs);
            ItemInstanceRecord instance = CreateInstance(inventory, itemDefinition);

            Result<ContentDefinitionRecord> archived = ContentCatalogLifecycleService.ArchiveDefinition(_catalog, new ArchiveDefinitionRequest(_campaign, secondApplied.ContentDefinitionId, "test archive", actorIsMainGm: true, Command(), Corr));
            Assert.That(archived.IsSuccess, Is.True, archived.IsFailure ? archived.Error.Code.ToString() : string.Empty);

            UseItemRequest request = Request(actor, InventoryItemRef.ForInstance(instance.ItemInstanceId), instance.Revision, inventory.Revision);
            Result<ItemUsageRecord> result = UseItemService.UseItem(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, request);

            Assert.That(result.IsFailure, Is.True, "The second, archived ApplyEffect must fail the whole use.");
            Result<ItemInstanceRecord> reread = _inventory.GetItemInstance(_campaign, instance.ItemInstanceId, Corr);
            Assert.That(reread.IsSuccess, Is.True, "The deleted instance must be re-inserted by compensation.");
            Assert.That(reread.Value.ItemInstanceId, Is.EqualTo(instance.ItemInstanceId));
            Assert.That(reread.Value.InventoryId, Is.EqualTo(instance.InventoryId));
            Assert.That(reread.Value.OwnerRef, Is.EqualTo(instance.OwnerRef));
            Assert.That(reread.Value.Revision, Is.EqualTo(instance.Revision), "The delete-and-reinsert restore branch writes back prior.Revision exactly for instances too.");
            Assert.That(CountStillAttached(actor), Is.EqualTo(0), "The already-created first effect must be removed too.");

            Result<ItemUsageRecord> retried = UseItemService.UseItem(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, request);
            Assert.That(retried.IsFailure, Is.True, "A retry of a compensated CommandId must remain a permanent failure -- never a silent success.");
        }

        [Test] // TC-USEITEM-007
        public void UseItem_ItemWithHasCharges_HonestRejection_NotACrash()
        {
            CharacterId actor = Active("actor");
            InventoryRecord inventory = CreateInventory(actor);
            ContentDefinitionRecord effect = PublishInstantEffect(Health, "-1");
            var itemDefinition = new ItemDefinition(ItemCategory.Consumable, false, null, 1, false, null, true, 3, Array.Empty<ContentDefinitionRef>(), new[] { new ContentDefinitionRef(effect.ContentDefinitionId, effect.Version) });
            ItemInstanceRecord instance = CreateInstance(inventory, itemDefinition);

            Result<ItemUsageRecord> result = UseItemService.UseItem(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, Request(actor, InventoryItemRef.ForInstance(instance.ItemInstanceId), instance.Revision, inventory.Revision));

            Assert.That(result.IsFailure, Is.True);
            Result<ItemInstanceRecord> reread = _inventory.GetItemInstance(_campaign, instance.ItemInstanceId, Corr);
            Assert.That(reread.IsSuccess, Is.True, "A HasCharges item must be rejected honestly before any consumption is attempted.");
        }

        [Test] // TC-USEITEM-008
        public void UseItem_StaleExpectedItemRevision_RejectedNotAppliedOverStaleState()
        {
            CharacterId actor = Active("actor");
            InitResource(actor, Health);
            InventoryRecord inventory = CreateInventory(actor);
            ContentDefinitionRecord effect = PublishInstantEffect(Health, "-1");
            ItemStackRecord stack = CreateStack(inventory, quantity: 3, effect);

            Result<ItemUsageRecord> result = UseItemService.UseItem(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, Request(actor, InventoryItemRef.ForStack(stack.ItemStackId), stack.Revision + 1, inventory.Revision));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(CurrentValue(actor, Health), Is.EqualTo(10), "Nothing must be applied when the revision check fails.");
            Result<ItemStackRecord> reread = _inventory.GetItemStack(_campaign, stack.ItemStackId, Corr);
            Assert.That(reread.IsSuccess, Is.True);
            Assert.That(reread.Value.Quantity.Value, Is.EqualTo(3), "Nothing must be consumed when the revision check fails.");
        }

        [Test] // TC-USEITEM-009
        public void UseItem_RetryWithSameCommandId_IsIdempotent_NoDoubleConsumptionOrReroll()
        {
            CharacterId actor = Active("actor");
            InitResource(actor, Health);
            InventoryRecord inventory = CreateInventory(actor);
            ContentDefinitionRecord effect = PublishInstantEffect(Health, "-1d6");
            ItemStackRecord stack = CreateStack(inventory, quantity: 5, effect);
            UseItemRequest request = Request(actor, InventoryItemRef.ForStack(stack.ItemStackId), stack.Revision, inventory.Revision);
            var random = new CountingRandomFactory();

            Result<ItemUsageRecord> first = UseItemService.UseItem(_reader, _apply, _catalog, _effects, random, _clock, _campaign, Epoch, request);
            Assert.That(first.IsSuccess, Is.True, first.IsFailure ? first.Error.Code.ToString() : string.Empty);
            long healthAfterFirst = CurrentValue(actor, Health);

            Result<ItemUsageRecord> second = UseItemService.UseItem(_reader, _apply, _catalog, _effects, random, _clock, _campaign, Epoch, request);

            Assert.That(second.IsSuccess, Is.True);
            Assert.That(random.CreateCalls, Is.EqualTo(1), "A retry with the same CommandId must not re-derive the random stream.");
            Assert.That(CurrentValue(actor, Health), Is.EqualTo(healthAfterFirst), "No double AdjustResource/reroll on retry.");
            Result<ItemStackRecord> reread = _inventory.GetItemStack(_campaign, stack.ItemStackId, Corr);
            Assert.That(reread.IsSuccess, Is.True);
            Assert.That(reread.Value.Quantity.Value, Is.EqualTo(4), "A replayed CommandId must not consume a second unit.");
        }

        [Test] // TC-USEITEM-010
        public void UseItem_UnequippedConsumableItem_SucceedsWithoutEquipping()
        {
            CharacterId actor = Active("actor");
            InitResource(actor, Health);
            InventoryRecord inventory = CreateInventory(actor);
            ContentDefinitionRecord effect = PublishInstantEffect(Health, "-2");
            ItemStackRecord stack = CreateStack(inventory, quantity: 1, effect);

            Result<IReadOnlyList<EquippedEntryRecord>> equipped = _inventory.ListEquippedEntries(_campaign, _campaign.CampaignId, inventory.InventoryId, Corr);
            Assert.That(equipped.IsSuccess, Is.True);
            Assert.That(equipped.Value.Count, Is.EqualTo(0), "The item is deliberately never equipped in this test.");

            Result<ItemUsageRecord> result = UseItemService.UseItem(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, Request(actor, InventoryItemRef.ForStack(stack.ItemStackId), stack.Revision, inventory.Revision));

            Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error.Code.ToString() : string.Empty);
            Assert.That(CurrentValue(actor, Health), Is.EqualTo(8));
        }

        // ---- helpers ----

        private ContentDefinitionRecord AuthorDraft(ContentDefinitionType type, string name, string propertiesJson, IReadOnlyList<ContentDefinitionRef>? dependencyRefs = null)
        {
            var request = new CreateDraftDefinitionRequest(_campaign, type, name, "ODY-S06-107 fixture.", User(), actorIsMainGm: true, Command(), Corr, rulesetCompatibility: new[] { ActiveRuleset }, propertiesJson: propertiesJson, dependencyRefs: dependencyRefs);
            Result<ContentDefinitionRecord> result = ContentCatalogAuthoringService.CreateDraftDefinition(_catalog, request);
            Assert.That(result.IsSuccess, Is.True);
            return result.Value;
        }

        private ContentDefinitionRecord PublishFixture(ContentDefinitionRecord draft)
        {
            var request = new PublishDefinitionRequest(_campaign, draft.ContentDefinitionId, draft.Revision, User(), actorIsMainGm: true, Command(), Corr);
            Result<ContentDefinitionRecord> result = ContentCatalogLifecycleService.PublishDefinition(_catalog, request);
            Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error.Code.ToString() : string.Empty);
            return result.Value;
        }

        private ContentDefinitionRecord PublishInstantEffect(ResourceDefinitionId resourceKind, string amountFormula)
        {
            var envelope = new MechanicsPrimitiveEnvelope(1, new MechanicsPrimitive[] { new AdjustResourcePrimitive(resourceKind, amountFormula) });
            var targetRule = new ContentTargetRule(ContentTargetSource.SourceEntity, 1, 1, true);
            var effect = new EffectDefinition(targetRule, EffectDurationType.Instant, durationValue: null, EffectStackPolicy.RefreshDuration, MechanicsPayloadCodec.EncodePrimitives(envelope));
            return PublishFixture(AuthorDraft(ContentDefinitionType.Effect, "Test Instant Effect " + Guid.NewGuid().ToString("N"), TypedDefinitionCodec.EncodeEffect(effect)));
        }

        private ContentDefinitionRecord PublishFixtureAppliedEffect()
        {
            var targetRule = new ContentTargetRule(ContentTargetSource.SourceEntity, 1, 1, true);
            var effect = new EffectDefinition(targetRule, EffectDurationType.ForRounds, durationValue: 2, EffectStackPolicy.RefreshDuration, mechanicsPayloadRef: null);
            return PublishFixture(AuthorDraft(ContentDefinitionType.Effect, "Test Applied Effect " + Guid.NewGuid().ToString("N"), TypedDefinitionCodec.EncodeEffect(effect)));
        }

        private ContentDefinitionRecord PublishApplyEffectEffect(ContentDefinitionRecord appliedEffect)
        {
            var appliedRef = new ContentDefinitionRef(appliedEffect.ContentDefinitionId, appliedEffect.Version);
            var envelope = new MechanicsPrimitiveEnvelope(1, new MechanicsPrimitive[] { new ApplyEffectPrimitive(appliedRef) });
            var targetRule = new ContentTargetRule(ContentTargetSource.SourceEntity, 1, 1, true);
            var effect = new EffectDefinition(targetRule, EffectDurationType.Instant, durationValue: null, EffectStackPolicy.RefreshDuration, MechanicsPayloadCodec.EncodePrimitives(envelope));
            return PublishFixture(AuthorDraft(ContentDefinitionType.Effect, "Test Item Effect " + Guid.NewGuid().ToString("N"), TypedDefinitionCodec.EncodeEffect(effect), dependencyRefs: new[] { appliedRef }));
        }

        private ContentDefinitionRecord PublishWhileEquippedEffect()
        {
            var targetRule = new ContentTargetRule(ContentTargetSource.SourceEntity, 1, 1, true);
            var effect = new EffectDefinition(targetRule, EffectDurationType.WhileItemEquipped, durationValue: null, EffectStackPolicy.RefreshDuration, mechanicsPayloadRef: null);
            return PublishFixture(AuthorDraft(ContentDefinitionType.Effect, "Test WhileEquipped Effect " + Guid.NewGuid().ToString("N"), TypedDefinitionCodec.EncodeEffect(effect)));
        }

        private InventoryRecord CreateInventory(CharacterId owner)
        {
            UtcInstant now = _clock.GetUtcNow();
            var record = new InventoryRecord(InventoryId.NewId(now), _campaign.CampaignId, InventoryOwnerRef.ForCharacter(owner), 1, now, now);
            Result<InventoryRecord> created = _inventory.CreateInventory(_campaign, record, Command(), Corr);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        private ItemStackRecord CreateStack(InventoryRecord inventory, long quantity, params ContentDefinitionRecord[] builtInEffects)
        {
            UtcInstant now = _clock.GetUtcNow();
            var effectRefs = new ContentDefinitionRef[builtInEffects.Length];
            for (int i = 0; i < builtInEffects.Length; i++) effectRefs[i] = new ContentDefinitionRef(builtInEffects[i].ContentDefinitionId, builtInEffects[i].Version);
            var itemDefinition = new ItemDefinition(ItemCategory.Consumable, true, 10, 1, false, null, false, null, Array.Empty<ContentDefinitionRef>(), effectRefs);
            var sourceRef = new ContentDefinitionRef(ContentDefinitionId.NewId(now), 1);
            var snapshot = new ItemMechanicsSnapshot(sourceRef, 1, ContentDefinitionType.Item, TypedDefinitionCodec.EncodeItem(itemDefinition));
            var record = new ItemStackRecord(ItemStackId.NewId(now), inventory.CampaignId, inventory.InventoryId, inventory.OwnerRef, InventoryLocationRef.Contained(inventory.InventoryId, "main"), sourceRef, snapshot, ItemStackQuantity.Create(quantity), "{}", 1, now, now);
            Result<ItemStackRecord> created = _inventory.CreateItemStack(_campaign, record, Command(), Corr);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        private ItemInstanceRecord CreateInstance(InventoryRecord inventory, ItemDefinition itemDefinition)
        {
            UtcInstant now = _clock.GetUtcNow();
            var sourceRef = new ContentDefinitionRef(ContentDefinitionId.NewId(now), 1);
            var snapshot = new ItemMechanicsSnapshot(sourceRef, 1, ContentDefinitionType.Item, TypedDefinitionCodec.EncodeItem(itemDefinition));
            var record = new ItemInstanceRecord(ItemInstanceId.NewId(now), inventory.CampaignId, inventory.InventoryId, inventory.OwnerRef, InventoryLocationRef.Contained(inventory.InventoryId, "main"), sourceRef, snapshot, "{}", 1, now, now);
            Result<ItemInstanceRecord> created = _inventory.CreateItemInstance(_campaign, record, Command(), Corr);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        private UseItemRequest Request(CharacterId actor, InventoryItemRef item, long expectedItemRevision, long expectedInventoryRevision)
        {
            CharacterRecord current = _characters.GetCharacter(_campaign, actor, Corr).Value;
            var intent = new UseItemIntent(actor, item, expectedItemRevision, expectedInventoryRevision, current.Revisions.CharacterResourcesRevision);
            return new UseItemRequest(intent, User(), actorIsMainGm: true, Command(), Corr);
        }

        private void InitResource(CharacterId characterId, ResourceDefinitionId resourceKind)
        {
            CharacterRecord current = _characters.GetCharacter(_campaign, characterId, Corr).Value;
            Result<CharacterRecord> result = CharacterAdvancementService.InitializeResourceWithDefaults(_characters, _campaign, characterId, resourceKind, User(), true, current.Revisions.CharacterResourcesRevision, Command(), Corr);
            Assert.That(result.IsSuccess, Is.True);
        }

        private long CurrentValue(CharacterId characterId, ResourceDefinitionId resourceKind)
        {
            CharacterRecord current = _characters.GetCharacter(_campaign, characterId, Corr).Value;
            foreach (Domain.Character.CharacterResource resource in current.Resources)
            {
                if (resource.ResourceDefinitionId.Equals(resourceKind)) return resource.CurrentValue;
            }

            Assert.Fail("Resource " + resourceKind + " not found on " + characterId + ".");
            return -1;
        }

        private long CountStillAttached(CharacterId characterId)
        {
            Result<IReadOnlyList<ActiveEffectRecord>> onTarget = _effects.ListActiveEffectsByTarget(_campaign, _campaign.CampaignId, ActiveEffectTargetRef.ForCharacter(characterId), Corr);
            Assert.That(onTarget.IsSuccess, Is.True);
            long count = 0;
            foreach (ActiveEffectRecord record in onTarget.Value)
            {
                if (record.Effect.Status == ActiveEffectStatus.Active || record.Effect.Status == ActiveEffectStatus.Suspended) count++;
            }

            return count;
        }

        private CharacterId Active(string name)
        {
            CharacterId id = _characters.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, name), Command(), Corr).Value.CharacterId;
            CharacterRecord current = _characters.GetCharacter(_campaign, id, Corr).Value;
            return _characters.ApproveCharacterDraft(_campaign, id, true, current.Revisions.LifecycleRevision, Command(), Corr).Value.CharacterId;
        }

        private static CommandId Command() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId User() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        private sealed class ThrowingRandomFactory : IAuthoritativeRandomStreamFactory
        {
            public Result<IAuthoritativeRandomStream> Create(RandomDecisionContext context) => throw new AssertionException("This scenario must not derive an RNG stream at all.");
        }

        private sealed class FixedRandomStream : IAuthoritativeRandomStream
        {
            private readonly int[] _values;
            public FixedRandomStream(int[] values) { _values = values; }
            public RandomStreamIdentity Identity => default;
            public Result<RandomSample> NextInclusive(int minInclusive, int maxInclusive, int drawIndex) => Result<RandomSample>.Success(new RandomSample(_values[drawIndex], default!));
        }

        private sealed class FixedRandomStreamFactory : IAuthoritativeRandomStreamFactory
        {
            private readonly int[] _values;
            public FixedRandomStreamFactory(params int[] values) { _values = values; }
            public Result<IAuthoritativeRandomStream> Create(RandomDecisionContext context) => Result<IAuthoritativeRandomStream>.Success(new FixedRandomStream(_values));
        }

        private sealed class CountingRandomFactory : IAuthoritativeRandomStreamFactory
        {
            private readonly IAuthoritativeRandomStreamFactory _inner = new DeterministicRandomStreamFactory(CampaignRngKey.FromBytes(new byte[32] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }));
            public int CreateCalls;
            public Result<IAuthoritativeRandomStream> Create(RandomDecisionContext context) { CreateCalls++; return _inner.Create(context); }
        }

        /// <summary>ODY-S06-107 (mirroring ODY-S06-106's own TC-ABILITY-014 precedent): a real `IActiveEffectRepository`, forwarding every call to the real `_inner` implementation except that the `failOnCallNumber`-th call to `RemoveActiveEffect` returns a real, distinct `Result.Failure` without touching the database.</summary>
        private sealed class FailsNthRemovalActiveEffectRepository : IActiveEffectRepository
        {
            private readonly IActiveEffectRepository _inner;
            private readonly int _failOnCallNumber;
            private int _removeCallCount;

            public FailsNthRemovalActiveEffectRepository(IActiveEffectRepository inner, int failOnCallNumber)
            {
                _inner = inner;
                _failOnCallNumber = failOnCallNumber;
            }

            public Result<ActiveEffectRecord> CreateActiveEffect(CampaignHandle campaign, ActiveEffectRecord record, CommandId commandId, CorrelationId correlationId) => _inner.CreateActiveEffect(campaign, record, commandId, correlationId);
            public Result<ActiveEffectRecord> GetActiveEffect(CampaignHandle campaign, ActiveEffectId activeEffectId, CorrelationId correlationId) => _inner.GetActiveEffect(campaign, activeEffectId, correlationId);
            public Result<IReadOnlyList<ActiveEffectRecord>> ListActiveEffectsByTarget(CampaignHandle campaign, CampaignId campaignId, ActiveEffectTargetRef targetRef, CorrelationId correlationId) => _inner.ListActiveEffectsByTarget(campaign, campaignId, targetRef, correlationId);
            public Result<IReadOnlyList<ActiveEffectRecord>> ListActiveEffectsBySource(CampaignHandle campaign, CampaignId campaignId, ActiveEffectSourceRef sourceRef, CorrelationId correlationId) => _inner.ListActiveEffectsBySource(campaign, campaignId, sourceRef, correlationId);
            public Result<ActiveEffectRecord> ExpireActiveEffect(CampaignHandle campaign, CampaignId campaignId, ActiveEffectId activeEffectId, long expectedRevision, CommandId commandId, CorrelationId correlationId) => _inner.ExpireActiveEffect(campaign, campaignId, activeEffectId, expectedRevision, commandId, correlationId);
            public Result<long> SetItemEffectEquipped(CampaignHandle campaign, CampaignId campaignId, ActiveEffectId activeEffectId, bool equipped, long expectedRevision, UserId actorUserId, CommandId commandId, CorrelationId correlationId) => _inner.SetItemEffectEquipped(campaign, campaignId, activeEffectId, equipped, expectedRevision, actorUserId, commandId, correlationId);

            public Result<ActiveEffectRecord> RemoveActiveEffect(CampaignHandle campaign, CampaignId campaignId, ActiveEffectId activeEffectId, UserId actorUserId, bool actorIsMainGm, long expectedRevision, CommandId commandId, CorrelationId correlationId)
            {
                _removeCallCount++;
                if (_removeCallCount == _failOnCallNumber)
                {
                    return Result<ActiveEffectRecord>.Failure(PersistenceFailures.ActiveEffectIoFailed(correlationId));
                }

                return _inner.RemoveActiveEffect(campaign, campaignId, activeEffectId, actorUserId, actorIsMainGm, expectedRevision, commandId, correlationId);
            }
        }
    }
}
