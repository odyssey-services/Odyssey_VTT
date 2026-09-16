using System;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Combat;
using Odyssey.Application.Commands;
using Odyssey.Application.Content;
using Odyssey.Application.Effects;
using Odyssey.Application.Persistence;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Content;
using Odyssey.Domain.Effects;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S06-106: real, end-to-end tests for `ActivateAbility` -- `ADR-030` §9's own root command, and
    /// this task's own first real implementation of the shared mechanics-primitive interpreter (`ADR-030`
    /// §7). Every test goes through the real `ActivateAbilityService.ActivateAbility` production entry
    /// point against a real temp-directory SQLite campaign -- never a direct unit call to a private method.
    /// </summary>
    public sealed class ActivateAbilityIntegrationTests
    {
        private const string ActiveRuleset = "ruleset.core@1.0.0";
        private static readonly CorrelationId Corr = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly RngKeyEpochId Epoch = RngKeyEpochId.Parse("epoch-001");
        private static readonly ResourceDefinitionId Mana = ResourceDefinitionId.Parse("mana");
        private static readonly ResourceDefinitionId Health = ResourceDefinitionId.Parse("health");
        private static readonly AbilityDefinitionId TestAbilityKey = AbilityDefinitionId.Parse("TestAbility");

        private string _root = null!;
        private CampaignHandle _campaign = null!;
        private IWallClock _clock = null!;
        private SqliteCharacterRepository _characters = null!;
        private SqliteContentCatalogRepository _catalog = null!;
        private SqliteActiveEffectRepository _effects = null!;
        private SqliteActivateAbilityStateReader _reader = null!;
        private SqliteActivateAbilityRepository _apply = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "ody-s06-106-" + Guid.NewGuid().ToString("N"));
            _clock = new SystemWallClock();
            Result<CampaignHandle> campaign = new SqliteCampaignRepository(_clock).Create(new CreateCampaignRequest(_root, "activate-ability", "ruleset.core", "1.0.0", "0.1.0"), Command(), Corr);
            Assert.That(campaign.IsSuccess, Is.True);
            _campaign = campaign.Value;
            _characters = new SqliteCharacterRepository(_clock);
            _catalog = new SqliteContentCatalogRepository(_clock);
            _effects = new SqliteActiveEffectRepository(_clock);
            _reader = new SqliteActivateAbilityStateReader(_characters, _catalog, _clock);
            _apply = new SqliteActivateAbilityRepository(_clock);
        }

        [Test] // TC-ABILITY-001
        public void ActivateAbility_NoDiceInFormula_DeterministicCostAndAdjustResource_ExactValues()
        {
            CharacterId actor = Active("actor");
            InitResource(actor, Mana);
            InitResource(actor, Health);
            ContentDefinitionRecord published = PublishAbilityWithAdjustResource(costMana: 3, resourceKind: Health, amountFormula: "-5");
            CharacterAbility ability = GrantActivatableAbility(actor, published);

            Result<AbilityActivationRecord> result = ActivateAbilityService.ActivateAbility(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, Request(actor, ability.CharacterAbilityId, actor));

            Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error.Code.ToString() : string.Empty);
            Assert.That(CurrentValue(actor, Mana), Is.EqualTo(7), "10 - 3 cost = 7.");
            Assert.That(CurrentValue(actor, Health), Is.EqualTo(5), "10 - 5 AdjustResource = 5 (a resource cannot exceed its own EffectiveMaximum, so this scenario uses a self-damage-shaped delta, not a heal).");
        }

        [Test] // TC-ABILITY-002
        public void ActivateAbility_FormulaWithDiceAndAttribute_FixedRngStream_ExactValue()
        {
            CharacterId actor = Active("actor");
            InitResource(actor, Mana);
            InitResource(actor, Health);
            GrantAttribute(actor, "Strength", 4);
            ContentDefinitionRecord published = PublishAbilityWithAdjustResource(costMana: 0, resourceKind: Health, amountFormula: "-1d6-Strength");
            CharacterAbility ability = GrantActivatableAbility(actor, published);

            Result<AbilityActivationRecord> result = ActivateAbilityService.ActivateAbility(_reader, _apply, _catalog, _effects, new FixedRandomStreamFactory(10, 20, 30, 40), _clock, _campaign, Epoch, Request(actor, ability.CharacterAbilityId, actor));

            Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error.Code.ToString() : string.Empty);
            // raw=10 mapped onto a d6: ((10-1)%6)+1 = 4; -4 -4 Strength = -8; 10 - 8 = 2.
            Assert.That(CurrentValue(actor, Health), Is.EqualTo(2));
        }

        [Test] // TC-ABILITY-003
        public void ActivateAbility_ApplyEffectPrimitive_RealActiveEffectRecordIsCreated()
        {
            CharacterId actor = Active("actor");
            ContentDefinitionRecord effect = PublishFixtureEffect();
            ContentDefinitionRecord published = PublishAbilityWithApplyEffect(new ContentDefinitionRef(effect.ContentDefinitionId, effect.Version));
            CharacterAbility ability = GrantActivatableAbility(actor, published);

            Result<AbilityActivationRecord> result = ActivateAbilityService.ActivateAbility(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, Request(actor, ability.CharacterAbilityId, actor));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.AppliedEffectRefs.Count, Is.EqualTo(1));
            Result<System.Collections.Generic.IReadOnlyList<ActiveEffectRecord>> onActor = _effects.ListActiveEffectsByTarget(_campaign, _campaign.CampaignId, ActiveEffectTargetRef.ForCharacter(actor), Corr);
            Assert.That(onActor.IsSuccess, Is.True);
            Assert.That(onActor.Value.Count, Is.EqualTo(1));
            Assert.That(onActor.Value[0].Effect.EffectDefinitionRef.Equals(new ContentDefinitionRef(effect.ContentDefinitionId, effect.Version)), Is.True);
        }

        [Test] // TC-ABILITY-004
        public void ActivateAbility_InsufficientResourceForCost_RejectedEntirely_NothingApplied()
        {
            CharacterId actor = Active("actor");
            InitResource(actor, Mana);
            InitResource(actor, Health);
            ContentDefinitionRecord published = PublishAbilityWithAdjustResource(costMana: 999, resourceKind: Health, amountFormula: "5");
            CharacterAbility ability = GrantActivatableAbility(actor, published);

            Result<AbilityActivationRecord> result = ActivateAbilityService.ActivateAbility(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, Request(actor, ability.CharacterAbilityId, actor));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(CurrentValue(actor, Mana), Is.EqualTo(10), "Cost must not be partially charged.");
            Assert.That(CurrentValue(actor, Health), Is.EqualTo(10), "AdjustResource must not apply when the cost charge fails.");
        }

        [Test] // TC-ABILITY-005
        public void ActivateAbility_ActorDoesNotOwnCharacterAndIsNotMainGm_DeniedByAuthorization()
        {
            CharacterId actor = Active("actor");
            InitResource(actor, Health);
            ContentDefinitionRecord published = PublishAbilityWithAdjustResource(costMana: 0, resourceKind: Health, amountFormula: "5");
            CharacterAbility ability = GrantActivatableAbility(actor, published);
            var request = new ActivateAbilityRequest(new ActivateAbilityIntent(actor, ability.CharacterAbilityId, new[] { actor }), User(), actorIsMainGm: false, Command(), Corr);

            Result<AbilityActivationRecord> result = ActivateAbilityService.ActivateAbility(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(CurrentValue(actor, Health), Is.EqualTo(10));
        }

        [Test] // TC-ABILITY-006
        public void ActivateAbility_RetryWithSameCommandId_IsIdempotent_NoDoubleChargeOrReroll()
        {
            CharacterId actor = Active("actor");
            InitResource(actor, Mana);
            InitResource(actor, Health);
            ContentDefinitionRecord published = PublishAbilityWithAdjustResource(costMana: 3, resourceKind: Health, amountFormula: "-1d6");
            CharacterAbility ability = GrantActivatableAbility(actor, published);
            ActivateAbilityRequest request = Request(actor, ability.CharacterAbilityId, actor);
            var random = new CountingRandomFactory();

            Result<AbilityActivationRecord> first = ActivateAbilityService.ActivateAbility(_reader, _apply, _catalog, _effects, random, _clock, _campaign, Epoch, request);
            Assert.That(first.IsSuccess, Is.True, first.IsFailure ? first.Error.Code.ToString() : string.Empty);
            long manaAfterFirst = CurrentValue(actor, Mana);
            long healthAfterFirst = CurrentValue(actor, Health);

            Result<AbilityActivationRecord> second = ActivateAbilityService.ActivateAbility(_reader, _apply, _catalog, _effects, random, _clock, _campaign, Epoch, request);

            Assert.That(second.IsSuccess, Is.True);
            Assert.That(random.CreateCalls, Is.EqualTo(1), "A retry with the same CommandId must not re-derive the random stream.");
            Assert.That(CurrentValue(actor, Mana), Is.EqualTo(manaAfterFirst), "No double charge on retry.");
            Assert.That(CurrentValue(actor, Health), Is.EqualTo(healthAfterFirst), "No double AdjustResource/reroll on retry.");
        }

        [Test] // TC-ABILITY-007
        public void ActivateAbility_IncompatibleMechanicsPayloadSchemaVersion_HonestFailure_NotACrash()
        {
            CharacterId actor = Active("actor");
            var targetRule = new ContentTargetRule(ContentTargetSource.ActingCharacter, 1, 1, true);
            var abilityDefinition = new AbilityDefinition(AbilityEntryPointType.ActiveAction, "OnUse", actionCost: 0, Array.Empty<AbilityResourceCost>(), targetRule, "{\"schemaVersion\":999,\"primitives\":[]}");
            ContentDefinitionRecord draft = AuthorDraft(ContentDefinitionType.Ability, "Bad Payload Ability", TypedDefinitionCodec.EncodeAbility(abilityDefinition));
            ContentDefinitionRecord published = PublishFixture(draft);
            CharacterAbility ability = GrantActivatableAbility(actor, published);

            Result<AbilityActivationRecord> result = ActivateAbilityService.ActivateAbility(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, Request(actor, ability.CharacterAbilityId, actor));

            Assert.That(result.IsFailure, Is.True);
        }

        [Test] // TC-ABILITY-008
        public void ActivateAbility_NoOpenCombatEncounter_Succeeds_IndependentOfCombatState()
        {
            // Deliberately: no CombatEncounter is created anywhere in this test's own campaign at all.
            CharacterId actor = Active("actor");
            InitResource(actor, Health);
            ContentDefinitionRecord published = PublishAbilityWithAdjustResource(costMana: 0, resourceKind: Health, amountFormula: "-3");
            CharacterAbility ability = GrantActivatableAbility(actor, published);

            Result<AbilityActivationRecord> result = ActivateAbilityService.ActivateAbility(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, Request(actor, ability.CharacterAbilityId, actor));

            Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error.Code.ToString() : string.Empty);
            Assert.That(CurrentValue(actor, Health), Is.EqualTo(7));
        }

        // ---- helpers ----

        private ContentDefinitionRecord AuthorDraft(ContentDefinitionType type, string name, string propertiesJson, System.Collections.Generic.IReadOnlyList<ContentDefinitionRef>? dependencyRefs = null)
        {
            var request = new CreateDraftDefinitionRequest(_campaign, type, name, "ODY-S06-106 fixture.", User(), actorIsMainGm: true, Command(), Corr, rulesetCompatibility: new[] { ActiveRuleset }, propertiesJson: propertiesJson, dependencyRefs: dependencyRefs);
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

        private ContentDefinitionRecord PublishFixtureEffect()
        {
            var targetRule = new ContentTargetRule(ContentTargetSource.SourceEntity, 1, 1, true);
            var effect = new EffectDefinition(targetRule, EffectDurationType.ForRounds, durationValue: 2, EffectStackPolicy.RefreshDuration, mechanicsPayloadRef: null);
            return PublishFixture(AuthorDraft(ContentDefinitionType.Effect, "Test Effect " + Guid.NewGuid().ToString("N"), TypedDefinitionCodec.EncodeEffect(effect)));
        }

        private ContentDefinitionRecord PublishAbilityWithAdjustResource(long costMana, ResourceDefinitionId resourceKind, string amountFormula)
        {
            var envelope = new MechanicsPrimitiveEnvelope(1, new MechanicsPrimitive[] { new AdjustResourcePrimitive(resourceKind, amountFormula) });
            System.Collections.Generic.IReadOnlyList<AbilityResourceCost> costs = costMana > 0 ? new[] { new AbilityResourceCost(Mana, costMana) } : Array.Empty<AbilityResourceCost>();
            var targetRule = new ContentTargetRule(ContentTargetSource.ActingCharacter, 1, 1, true);
            var ability = new AbilityDefinition(AbilityEntryPointType.ActiveAction, "OnUse", actionCost: 0, costs, targetRule, MechanicsPayloadCodec.EncodePrimitives(envelope));
            return PublishFixture(AuthorDraft(ContentDefinitionType.Ability, "Test Ability " + Guid.NewGuid().ToString("N"), TypedDefinitionCodec.EncodeAbility(ability)));
        }

        private ContentDefinitionRecord PublishAbilityWithApplyEffect(ContentDefinitionRef effectRef)
        {
            var envelope = new MechanicsPrimitiveEnvelope(1, new MechanicsPrimitive[] { new ApplyEffectPrimitive(effectRef) });
            var targetRule = new ContentTargetRule(ContentTargetSource.ActingCharacter, 1, 1, true);
            var ability = new AbilityDefinition(AbilityEntryPointType.ActiveAction, "OnUse", actionCost: 0, Array.Empty<AbilityResourceCost>(), targetRule, MechanicsPayloadCodec.EncodePrimitives(envelope));
            return PublishFixture(AuthorDraft(ContentDefinitionType.Ability, "Test Ability " + Guid.NewGuid().ToString("N"), TypedDefinitionCodec.EncodeAbility(ability), dependencyRefs: new[] { effectRef }));
        }

        private CharacterAbility GrantActivatableAbility(CharacterId characterId, ContentDefinitionRecord published)
        {
            CharacterRecord current = _characters.GetCharacter(_campaign, characterId, Corr).Value;
            string sourceRef = new ContentDefinitionRef(published.ContentDefinitionId, published.Version).ToString();
            Result<CharacterRecord> acquired = _characters.AcquireAbility(_campaign, characterId, TestAbilityKey, SourceKind.GMGrant, sourceRef, RankMode.None, null, null, "{}", User(), actorIsMainGm: true, null, current.Revisions.CharacterAbilitiesRevision, Command(), Corr);
            Assert.That(acquired.IsSuccess, Is.True);
            foreach (CharacterAbility candidate in acquired.Value.Abilities)
            {
                if (candidate.SourceRef == sourceRef) return candidate;
            }

            Assert.Fail("Granted ability not found on the reloaded CharacterRecord.");
            return null!;
        }

        private static ActivateAbilityRequest Request(CharacterId actor, CharacterAbilityId characterAbilityId, params CharacterId[] targets)
            => new ActivateAbilityRequest(new ActivateAbilityIntent(actor, characterAbilityId, targets), User(), actorIsMainGm: true, Command(), Corr);

        private void InitResource(CharacterId characterId, ResourceDefinitionId resourceKind)
        {
            CharacterRecord current = _characters.GetCharacter(_campaign, characterId, Corr).Value;
            Result<CharacterRecord> result = _characters.InitializeCharacterResource(_campaign, characterId, resourceKind, User(), true, current.Revisions.CharacterResourcesRevision, Command(), Corr);
            Assert.That(result.IsSuccess, Is.True);
        }

        private long CurrentValue(CharacterId characterId, ResourceDefinitionId resourceKind)
        {
            CharacterRecord current = _characters.GetCharacter(_campaign, characterId, Corr).Value;
            foreach (CharacterResource resource in current.Resources)
            {
                if (resource.ResourceDefinitionId.Equals(resourceKind)) return resource.CurrentValue;
            }

            Assert.Fail("Resource " + resourceKind + " not found on " + characterId + ".");
            return -1;
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
    }
}
