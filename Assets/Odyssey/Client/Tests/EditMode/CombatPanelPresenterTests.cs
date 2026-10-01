using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Application.Audience;
using Odyssey.Application.Combat;
using Odyssey.Application.Commands;
using Odyssey.Application.CharacterAdvancement;
using Odyssey.Application.Checks;
using Odyssey.Application.Content;
using Odyssey.Application.Dice;
using Odyssey.Application.Effects;
using Odyssey.Application.Inventory;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Persistence;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Checks;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Content;
using Odyssey.Domain.Effects;
using Odyssey.Domain.Geometry;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Persistence.Sqlite;
using Odyssey.Rules.Combat;
using Odyssey.Unity.Client;
using UnityEngine.UIElements;

namespace Odyssey.Tests.Unity.EditMode
{
    /// <summary>ODY-S11-205: combat UI -- encounter, turn, attack preview/resolve, abilities, items, checks, MainGM tools.</summary>
    public sealed class CombatPanelPresenterTests
    {
        private static readonly ResourceDefinitionId Health = ResourceDefinitionId.Parse("health");
        private static readonly ResourceDefinitionId Mana = ResourceDefinitionId.Parse("mana");

        [Test]
        public void Encounter_ManualInitiativeOrder_CreateAndAdvance_AreMainGmOnly()
        {
            using var f = CombatFixture.Create();
            f.Panel.Refresh();
            Assert.That(f.Panel.Candidates, Is.EquivalentTo(new[] { f.Attacker, f.Defender }), "candidates are the scene's character tokens");

            f.Panel.AddToOrder(f.Defender);
            f.Panel.AddToOrder(f.Attacker);
            f.Panel.MoveInOrder(f.Attacker, -1);
            Assert.That(f.Panel.SetupOrder, Is.EqualTo(new[] { f.Attacker, f.Defender }), "the MainGM orders participants by hand");

            f.Host.Selection.SelectRole(BaselineRole.Player);
            Assert.That(f.Panel.CreateEncounter().IsFailure, Is.True);
            Assert.That(f.Panel.Banner.Text, Does.Contain("Only the MainGM"));
            Assert.That(f.View.Q<Button>("combat-create"), Is.Null);

            f.Host.Selection.SelectRole(BaselineRole.MainGM);
            Assert.That(f.Panel.CreateEncounter().IsSuccess, Is.True);
            Assert.That(f.Panel.CurrentParticipant, Is.EqualTo(f.Attacker));
            Assert.That(f.View.Q<Label>("combat-current").text, Does.Contain("Attacker"));

            Assert.That(f.Panel.Advance().IsSuccess, Is.True);
            Assert.That(f.Panel.CurrentParticipant, Is.EqualTo(f.Defender), "Next turn follows the hand-made order");
            f.Host.Selection.SelectRole(BaselineRole.Player);
            Assert.That(f.Panel.Advance().IsFailure, Is.True);
            Assert.That(f.View.Q<Button>("combat-advance"), Is.Null, "players get no Next turn button");
        }

        [Test]
        public void TurnChange_AnnouncesTheActingParticipantOnce_WhetherAdvancedHereOrObservedOnReload()
        {
            using var f = CombatFixture.Create();
            var announced = new List<CharacterId>();
            f.Panel.ActiveParticipantChanged += announced.Add;

            f.StartEncounter();
            Assert.That(announced, Is.EqualTo(new[] { f.Attacker }), "starting the encounter announces who acts first");
            f.Panel.Refresh();
            Assert.That(announced, Has.Count.EqualTo(1), "a reload of the same turn announces nothing");

            Assert.That(f.Panel.Advance().IsSuccess, Is.True);
            Assert.That(announced, Is.EqualTo(new[] { f.Attacker, f.Defender }));

            // Someone else advances (another session of the MainGM); this panel sees it on its next reload.
            CombatEncounterRecord current = f.Panel.Encounter!;
            Assert.That(CombatEncounterService.Advance(f.Ports.Encounters, f.Host.CampaignRepository, f.Host.Campaign,
                new AdvanceCombatEncounterRequest(current.EncounterId, current.Revision, f.Host.Selection.MainGmUserId, UiCommandIds.NewCommandId()), UiCommandIds.NewCorrelationId()).IsSuccess, Is.True);
            f.Panel.Refresh();
            Assert.That(announced, Has.Count.EqualTo(3), "a turn change observed on reload is announced too");
            Assert.That(announced[2], Is.EqualTo(f.Panel.CurrentParticipant!.Value));
        }

        [Test]
        public void AttentionCount_CountsItemsWaitingForTheMainGm_AndEmptiesWhenTheyAreDecided()
        {
            using var f = CombatFixture.Create(forceIntervention: true);
            var reported = new List<int>();
            f.Panel.AttentionCountChanged += reported.Add;
            f.StartEncounter();
            Assert.That(f.Panel.AttentionCount, Is.EqualTo(0));

            f.Panel.ToggleTarget(f.Defender);
            Result<AttackOutcomeRecord> attack = f.Panel.ResolveAttack();
            Assert.That(attack.IsSuccess, Is.True);
            Assert.That(attack.Value.OutcomeKind, Is.EqualTo(AttackOutcomeKind.Pending));
            Assert.That(f.Panel.AttentionCount, Is.EqualTo(1), "one attack waits for the MainGM");
            Assert.That(reported, Is.EqualTo(new[] { 1 }));

            f.Host.Selection.SelectRole(BaselineRole.Player);
            Assert.That(f.Panel.AttentionCount, Is.EqualTo(0), "only the MainGM decides, so players get no badge");
            f.Host.Selection.SelectRole(BaselineRole.MainGM);
            Assert.That(f.Panel.AttentionCount, Is.EqualTo(1));

            Assert.That(f.Panel.ResolveIntervention(attack.Value.ResolveAttackCommandId, AttackInterventionResolution.Reject).IsSuccess, Is.True);
            Assert.That(f.Panel.AttentionCount, Is.EqualTo(0), "the queue is empty again");
            Assert.That(reported.Last(), Is.EqualTo(0));
        }

        [Test]
        public void Attack_PreviewWritesNothing_ResolveAppliesDamage_JournalAndActionHintUpdate()
        {
            using var f = CombatFixture.Create();
            f.StartEncounter();
            f.Panel.ToggleTarget(f.Defender);
            Assert.That(f.Panel.Weapons, Has.Count.EqualTo(1), "only the equipped weapon is offered");

            Result<ProposedAttackResolution> preview = f.Panel.PreviewAttack();
            Assert.That(preview.IsSuccess, Is.True);
            Assert.That(preview.Value.Range.IsInRange, Is.True, "distance 3 within range 5");
            Assert.That(f.View.Q<VisualElement>("combat-attack-preview-card"), Is.Not.Null);
            Assert.That(f.CurrentValue(f.Defender, Health), Is.EqualTo(10), "a preview writes nothing");

            Result<AttackOutcomeRecord> resolved = f.Panel.ResolveAttack();
            Assert.That(resolved.IsSuccess, Is.True);
            Assert.That(resolved.Value.OutcomeKind, Is.EqualTo(AttackOutcomeKind.Accepted));
            Assert.That(f.CurrentValue(f.Defender, Health), Is.EqualTo(6), "raw 10 on a d6 = 4 damage");
            Assert.That(f.Panel.ActionsUsedThisTurn(f.Attacker), Is.EqualTo(1), "client-side action hint counts the weapon's action cost");
            Assert.That(f.Panel.Journal.Any(e => e.EntryType == CombatPanelPresenter.AttackResolvedEntryType), Is.True);
            Assert.That(f.Panel.PendingAttacks, Is.Empty);
        }

        [Test]
        public void Attack_OutOfRange_ShowsNoHitAndNoDamage()
        {
            using var f = CombatFixture.Create(defenderX: 10);
            f.StartEncounter();
            f.Panel.ToggleTarget(f.Defender);
            Result<ProposedAttackResolution> preview = f.Panel.PreviewAttack();
            Assert.That(preview.IsSuccess, Is.True);
            Assert.That(preview.Value.Range.IsInRange, Is.False);
            Assert.That(preview.Value.Hit.IsHit, Is.False, "a hit is binary by range; there is no hit chance");
            Assert.That(preview.Value.DamageDeltas, Is.Empty);
        }

        [Test]
        public void CorrectLogEntry_AddsACorrectionNextToTheOriginal_AndNeverUndoesDamage()
        {
            using var f = CombatFixture.Create();
            f.StartEncounter();
            f.Panel.ToggleTarget(f.Defender);
            f.Panel.ResolveAttack();
            GameLogEntryRecord original = f.Panel.Journal.Single(e => e.EntryType == CombatPanelPresenter.AttackResolvedEntryType);

            OdyConfirmDialog dialog = f.Panel.RequestCorrectLogEntry(original.RootCommandId);
            Assert.That(dialog.Confirm(), Is.False, "a reason is required");
            dialog.SetText("Wrong target named");
            Assert.That(dialog.Confirm(), Is.True);

            Assert.That(f.Panel.Journal.Any(e => e.LogEntryId == original.LogEntryId), Is.True, "the original entry stays visible");
            Assert.That(f.Panel.Journal.Any(e => e.EntryType == CombatPanelPresenter.AttackCompensatedEntryType), Is.True);
            Assert.That(f.CurrentValue(f.Defender, Health), Is.EqualTo(6), "damage is not rolled back");
            Assert.That(f.View.Q<Button>("combat-journal-correct-" + original.LogEntryId).text, Is.EqualTo("Correct log entry"), "named as a log correction, not an undo");
        }

        [Test]
        public void Ability_LinkedInTheSheet_ThenActivated_AndItemUsedOnSelf()
        {
            using var f = CombatFixture.Create();
            CharacterAbilityId abilityId = f.GrantAndLinkSelfDamageAbility();
            ItemStackRecord potion = f.GivePotions(2);
            f.StartEncounter();

            Assert.That(f.Panel.Abilities, Has.Count.EqualTo(1));
            Assert.That(f.Panel.Abilities[0].Definition!.TargetRule.TargetSource, Is.EqualTo(ContentTargetSource.ActingCharacter));
            Result<AbilityActivationRecord> activated = f.Panel.ActivateAbility(abilityId, new[] { f.Attacker });
            Assert.That(activated.IsSuccess, Is.True);
            Assert.That(f.CurrentValue(f.Attacker, Mana), Is.EqualTo(8));
            Assert.That(f.CurrentValue(f.Attacker, Health), Is.EqualTo(7));

            Assert.That(f.Panel.UsableItems.Select(i => i.Key), Does.Contain(potion.ItemStackId.ToString()));
            Assert.That(f.Panel.UseItem(potion.ItemStackId.ToString()).IsSuccess, Is.True, "items act on their user; no target is asked for");
            Assert.That(f.CurrentValue(f.Attacker, Health), Is.EqualTo(9));
        }

        [Test]
        public void Check_ShowsPassOrFail()
        {
            using var f = CombatFixture.Create();
            f.Panel.Refresh();
            Result<CheckOutcomeRecord> check = f.Panel.PerformCheck(f.Attacker, "1d20", 5, DiceRollAudienceKind.Public);
            Assert.That(check.IsSuccess, Is.True);
            Assert.That(check.Value.Result, Is.EqualTo(CheckResultKind.Pass));
            Assert.That(f.View.Q<Label>("combat-check-result").text, Is.EqualTo("Pass"));
            Assert.That(f.Panel.PerformCheck(f.Attacker, " ", 5, DiceRollAudienceKind.GMOnly).IsFailure, Is.True);
        }

        [Test]
        public void Intervention_And_StackConflicts_AreMainGmOnly_AndOnlyForRealPendingItems()
        {
            using var f = CombatFixture.Create();
            f.StartEncounter();
            f.Panel.ToggleTarget(f.Defender);
            AttackOutcomeRecord accepted = f.Panel.ResolveAttack().Value;

            Result<AttackOutcomeRecord> notPending = f.Panel.ResolveIntervention(accepted.ResolveAttackCommandId, AttackInterventionResolution.Approve);
            Assert.That(notPending.IsFailure, Is.True, "an applied attack is not waiting for a decision");
            Assert.That(f.Panel.ConflictCandidates, Is.Empty, "no effects, no conflicts");

            f.Host.Selection.SelectRole(BaselineRole.Player);
            Assert.That(f.Panel.ResolveIntervention(accepted.ResolveAttackCommandId, AttackInterventionResolution.Reject).IsFailure, Is.True);
            Assert.That(f.Panel.Banner.Text, Does.Contain("Only the MainGM"));
            Assert.That(f.View.Q<Button>("combat-journal-correct-" + accepted.GameLogEntryId), Is.Null, "players cannot correct the log");
        }

        /// <summary>
        /// ODY-S11-217 test double: the real SQLite attack repository, except that every attack is recorded with
        /// "intervention required", so it becomes a genuine durable Pending outcome without setting up effect rules.
        /// </summary>
        private sealed class InterventionRequiredAttackApply : IAttackApplyRepository
        {
            private readonly IAttackApplyRepository _inner;

            public InterventionRequiredAttackApply(IAttackApplyRepository inner) => _inner = inner;

            public Result<AttackOutcomeRecord> GetOutcome(CampaignHandle campaign, CommandId resolveAttackCommandId, CorrelationId correlationId) => _inner.GetOutcome(campaign, resolveAttackCommandId, correlationId);

            public Result<AttackOutcomeRecord> RecordAttackOutcome(CampaignHandle campaign, AttackIntent intent, AttackRandomSample randomSample, bool interventionRequired, IReadOnlyList<AttackEffectCandidate> effectCandidates, IReadOnlyList<AttackDelta> damageDeltas, IReadOnlyList<AttackDelta> costDeltas, UserId actorUserId, CommandId commandId, CorrelationId correlationId) =>
                _inner.RecordAttackOutcome(campaign, intent, randomSample, true, effectCandidates, damageDeltas, costDeltas, actorUserId, commandId, correlationId);

            public Result<AttackOutcomeRecord> ResolveAttackIntervention(CampaignHandle campaign, CommandId pendingCommandId, AttackInterventionResolution resolution, UserId actorUserId, CommandId commandId, CorrelationId correlationId) => _inner.ResolveAttackIntervention(campaign, pendingCommandId, resolution, actorUserId, commandId, correlationId);

            public Result<AttackCompensationRecord> CompensateAttackOutcome(CampaignHandle campaign, CommandId resolveAttackCommandId, string reasonCode, string correctedSummaryPayload, UserId actorUserId, CommandId commandId, CorrelationId correlationId) => _inner.CompensateAttackOutcome(campaign, resolveAttackCommandId, reasonCode, correctedSummaryPayload, actorUserId, commandId, correlationId);

            public Result<CombatStackConflictRecord> ResolveStackConflict(CampaignHandle campaign, CommandId raisingCommandId, ActiveEffectId conflictingActiveEffectId, ActiveEffectStackConflictResolution resolution, UserId actorUserId, CommandId commandId, CorrelationId correlationId) => _inner.ResolveStackConflict(campaign, raisingCommandId, conflictingActiveEffectId, resolution, actorUserId, commandId, correlationId);
        }

        private sealed class CombatFixture : IDisposable
        {
            private CombatFixture(GameTestHost host, double defenderX, bool forceIntervention)
            {
                Host = host;
                Inventory = host.NewInventoryRepository();
                Characters = host.NewCharacterRepository(Inventory);
                Catalog = host.NewCatalogRepository(Inventory);
                Scenes = host.NewSceneRepository();
                var effects = new SqliteActiveEffectRepository(host.Clock, host.CampaignRepository);
                var encounters = new SqliteCombatEncounterRepository(host.Clock, effects);
                Ports = new CombatPorts(Characters, Inventory, Catalog, Scenes, encounters,
                    new SqliteAttackStateReader(encounters, Inventory, Characters, host.Clock, Scenes, new SqliteObstacleRepository(host.Clock)),
                    forceIntervention ? new InterventionRequiredAttackApply(new SqliteAttackApplyRepository(host.Clock, host.CampaignRepository)) : new SqliteAttackApplyRepository(host.Clock, host.CampaignRepository),
                    new CoreAttackRulesEvaluator(),
                    new SqliteActivateAbilityStateReader(Characters, Catalog, host.Clock),
                    new SqliteActivateAbilityRepository(host.Clock, effects),
                    new SqliteUseItemStateReader(Characters, Inventory, Catalog, host.Clock),
                    new SqliteUseItemRepository(host.Clock, effects),
                    effects,
                    new SqliteCheckStateReader(Characters),
                    new SqliteCheckRepository(host.Clock),
                    new DiceRollStore(),
                    new SqliteGameLogRepository(host.Clock),
                    new InMemoryCampaignUserGroupDirectory(),
                    new FixedRandomStreamFactory(10, 20, 30, 40),
                    RngKeyEpochId.Parse("epoch-001"));

                Attacker = ActiveCharacter("Attacker", host.Selection.PlayerUserId);
                Defender = ActiveCharacter("Defender", null);
                InitResource(Attacker, Health);
                InitResource(Attacker, Mana);
                InitResource(Defender, Health);

                AttackerInventory = CreateInventory(Attacker);
                CreateInventory(Defender);
                ContentDefinitionRecord sword = Publish(ContentDefinitionType.Weapon, "Sword", TypedDefinitionCodec.EncodeWeapon(new WeaponDefinition(PlainItem(), "1d6", 5, WeaponAttackMode.Melee, 1, AmmoRequirement.None, Array.Empty<string>())));
                Result<ItemInstanceRecord> weapon = InventoryCreationService.CreateItemInstanceFromDefinition(Catalog, Inventory, host.CampaignRepository, host.Clock,
                    new CreateItemInstanceFromDefinitionRequest(host.Campaign, ItemInstanceId.NewId(host.Clock.GetUtcNow()), AttackerInventory.InventoryId, AttackerInventory.OwnerRef, InventoryLocationRef.Contained(AttackerInventory.InventoryId, "backpack"), sword.ContentDefinitionId, Gm, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()));
                Assert.That(weapon.IsSuccess, Is.True);
                Assert.That(EquipmentService.Equip(Inventory, Characters, host.CampaignRepository,
                    new EquipRequest(host.Campaign, InventoryItemRef.ForInstance(weapon.Value.ItemInstanceId), AttackerInventory.InventoryId, weapon.Value.Revision, "main_hand", Array.Empty<BodyPartId>(), Gm, host.Clock.GetUtcNow(), Gm, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId())).IsSuccess, Is.True);

                Assert.That(Scenes.CreateToken(host.Campaign, host.Demo.SceneId, new TokenPosition(0, 0), host.Selection.PlayerUserId, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId(), Attacker).IsSuccess, Is.True);
                Assert.That(Scenes.CreateToken(host.Campaign, host.Demo.SceneId, new TokenPosition(defenderX, 0), Gm, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId(), Defender).IsSuccess, Is.True);

                Panel = new CombatPanelPresenter(host.Context, Ports);
                View = Panel.BuildView();
            }

            public GameTestHost Host { get; }
            public SqliteInventoryRepository Inventory { get; }
            public SqliteCharacterRepository Characters { get; }
            public SqliteContentCatalogRepository Catalog { get; }
            public SqliteSceneRepository Scenes { get; }
            public CombatPorts Ports { get; }
            public CharacterId Attacker { get; }
            public CharacterId Defender { get; }
            public InventoryRecord AttackerInventory { get; }
            public CombatPanelPresenter Panel { get; }
            public VisualElement View { get; }
            private UserId Gm => Host.Selection.MainGmUserId;

            /// <param name="forceIntervention">ODY-S11-217: every attack is recorded as waiting for the MainGM (a real Pending record).</param>
            public static CombatFixture Create(double defenderX = 3, bool forceIntervention = false) => new CombatFixture(GameTestHost.Create(BaselineRole.MainGM), defenderX, forceIntervention);

            public void StartEncounter()
            {
                Panel.Refresh();
                Panel.AddToOrder(Attacker);
                Panel.AddToOrder(Defender);
                Assert.That(Panel.CreateEncounter().IsSuccess, Is.True);
            }

            public long CurrentValue(CharacterId id, ResourceDefinitionId resource) =>
                Characters.GetCharacter(Host.Campaign, id, UiCommandIds.NewCorrelationId()).Value.Resources.Single(r => r.ResourceDefinitionId.Equals(resource)).CurrentValue;

            /// <summary>GM-grants an ability, then links it in the character sheet (the UI path) to a published self-damage Ability.</summary>
            public CharacterAbilityId GrantAndLinkSelfDamageAbility()
            {
                var envelope = new MechanicsPrimitiveEnvelope(1, new MechanicsPrimitive[] { new AdjustResourcePrimitive(Health, "-3") });
                var ability = new AbilityDefinition(AbilityEntryPointType.ActiveAction, "OnUse", 0, new[] { new AbilityResourceCost(Mana, 2) }, new ContentTargetRule(ContentTargetSource.ActingCharacter, 1, 1, true), MechanicsPayloadCodec.EncodePrimitives(envelope));
                ContentDefinitionRecord published = Publish(ContentDefinitionType.Ability, "Power Strike", TypedDefinitionCodec.EncodeAbility(ability));

                using var sheet = new CharacterPanelPresenter(Host.Context, Characters, Scenes, Host.ExportDirectory, Catalog);
                sheet.BuildView();
                sheet.Open(Attacker);
                Assert.That(sheet.AcquireAbility("PowerStrike", SourceKind.GMGrant).IsSuccess, Is.True);
                CharacterAbility granted = sheet.Current!.Abilities.Single();
                Assert.That(sheet.LinkAbilityToDefinition(granted.CharacterAbilityId, new ContentDefinitionRef(published.ContentDefinitionId, published.Version)).IsSuccess, Is.True);
                return granted.CharacterAbilityId;
            }

            public ItemStackRecord GivePotions(long quantity)
            {
                var heal = new MechanicsPrimitiveEnvelope(1, new MechanicsPrimitive[] { new AdjustResourcePrimitive(Health, "2") });
                ContentDefinitionRecord effect = Publish(ContentDefinitionType.Effect, "Heal", TypedDefinitionCodec.EncodeEffect(new EffectDefinition(new ContentTargetRule(ContentTargetSource.SourceEntity, 1, 1, true), EffectDurationType.Instant, null, EffectStackPolicy.RefreshDuration, MechanicsPayloadCodec.EncodePrimitives(heal))));
                var refs = new[] { new ContentDefinitionRef(effect.ContentDefinitionId, effect.Version) };
                ContentDefinitionRecord potion = Publish(ContentDefinitionType.Item, "Healing Draught", TypedDefinitionCodec.EncodeItem(new ItemDefinition(ItemCategory.Consumable, true, 10, 1, false, null, false, null, Array.Empty<ContentDefinitionRef>(), refs)), refs);
                Result<ItemStackRecord> stack = InventoryCreationService.CreateItemStackFromDefinition(Catalog, Inventory, Host.CampaignRepository, Host.Clock,
                    new CreateItemStackFromDefinitionRequest(Host.Campaign, ItemStackId.NewId(Host.Clock.GetUtcNow()), AttackerInventory.InventoryId, AttackerInventory.OwnerRef, InventoryLocationRef.Contained(AttackerInventory.InventoryId, "backpack"), potion.ContentDefinitionId, ItemStackQuantity.Create(quantity), Gm, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()));
                Assert.That(stack.IsSuccess, Is.True);
                return stack.Value;
            }

            public void Dispose()
            {
                Panel.Dispose();
                Host.Dispose();
            }

            private CharacterId ActiveCharacter(string name, UserId? owner)
            {
                CharacterKind kind = owner.HasValue ? CharacterKind.PlayerCharacter : CharacterKind.NonPlayerCharacter;
                Result<CharacterRecord> created = Characters.BindDraftToCampaign(new BindDraftToCampaignRequest(Host.Campaign, kind, name, "humanoid", owner, CharacterCreationSeed.None(), null, null), UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId());
                Assert.That(created.IsSuccess, Is.True);
                Result<CharacterRecord> approved = Characters.ApproveCharacterDraft(Host.Campaign, created.Value.CharacterId, Gm, created.Value.Revisions.LifecycleRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId());
                Assert.That(approved.IsSuccess, Is.True);
                return approved.Value.CharacterId;
            }

            private void InitResource(CharacterId id, ResourceDefinitionId resource)
            {
                CharacterRecord current = Characters.GetCharacter(Host.Campaign, id, UiCommandIds.NewCorrelationId()).Value;
                Assert.That(CharacterAdvancementService.InitializeResourceWithDefaults(Characters, Host.Campaign, id, resource, Gm, current.Revisions.CharacterResourcesRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()).IsSuccess, Is.True);
            }

            private InventoryRecord CreateInventory(CharacterId owner)
            {
                Result<InventoryRecord> created = InventoryLocator.Create(Inventory, Host.Campaign, InventoryLocator.CharacterInventoryId(owner), InventoryLocator.CharacterOwner(owner), Host.Clock);
                Assert.That(created.IsSuccess, Is.True);
                return created.Value;
            }

            private static ItemDefinition PlainItem() => new ItemDefinition(ItemCategory.Generic, false, null, 3, false, null, false, null, Array.Empty<ContentDefinitionRef>(), Array.Empty<ContentDefinitionRef>());

            private ContentDefinitionRecord Publish(ContentDefinitionType type, string name, string propertiesJson, IReadOnlyList<ContentDefinitionRef>? dependencyRefs = null)
            {
                Result<ContentDefinitionRecord> draft = ContentCatalogAuthoringService.CreateDraftDefinition(Catalog, Host.CampaignRepository,
                    new CreateDraftDefinitionRequest(Host.Campaign, type, name, null, Gm, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId(), new[] { Host.Context.ActiveRulesetKey }, null, propertiesJson, dependencyRefs));
                Assert.That(draft.IsSuccess, Is.True, "draft " + name);
                Result<ContentDefinitionRecord> published = ContentCatalogLifecycleService.PublishDefinition(Catalog, Host.CampaignRepository,
                    new PublishDefinitionRequest(Host.Campaign, draft.Value.ContentDefinitionId, draft.Value.Revision, Gm, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()));
                Assert.That(published.IsSuccess, Is.True, "publish " + name);
                return published.Value;
            }
        }

        private sealed class FixedRandomStream : IAuthoritativeRandomStream
        {
            private readonly int[] _values;

            public FixedRandomStream(int[] values) => _values = values;

            public RandomStreamIdentity Identity => default;

            public Result<RandomSample> NextInclusive(int minInclusive, int maxInclusive, int drawIndex) => Result<RandomSample>.Success(new RandomSample(_values[drawIndex % _values.Length], default!));
        }

        private sealed class FixedRandomStreamFactory : IAuthoritativeRandomStreamFactory
        {
            private readonly int[] _values;

            public FixedRandomStreamFactory(params int[] values) => _values = values;

            public Result<IAuthoritativeRandomStream> Create(RandomDecisionContext context) => Result<IAuthoritativeRandomStream>.Success(new FixedRandomStream(_values));
        }
    }
}
