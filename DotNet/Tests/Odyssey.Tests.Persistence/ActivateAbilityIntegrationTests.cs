using System;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.CharacterAdvancement;
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
            _apply = new SqliteActivateAbilityRepository(_clock, _effects);
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
            CharacterRecord current = _characters.GetCharacter(_campaign, actor, Corr).Value;
            var intent = new ActivateAbilityIntent(actor, ability.CharacterAbilityId, new[] { actor }, current.Revisions.CharacterAbilitiesRevision, current.Revisions.CharacterResourcesRevision);
            var request = new ActivateAbilityRequest(intent, User(), actorIsMainGm: false, Command(), Corr);

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

        /// <summary>
        /// ODY-S06-106 doработка (product-owner-ordered fix, item 1): a real, injected failure -- the
        /// ApplyEffect primitive's own EffectDefinitionRef is archived BEFORE activation, a real, natural
        /// `Status != Published` rejection through the existing, unmodified catalog/decode path -- proves
        /// the previously-committed resource-delta transaction (cost charge + AdjustResource) is genuinely
        /// reversed, not left partially applied, and that the overall command reports honest failure, never
        /// a silent success.
        /// </summary>
        [Test] // TC-ABILITY-010
        public void ActivateAbility_ApplyEffectFailsAfterResourceCommit_ResourceDeltasAreCompensated_NotPartiallyApplied()
        {
            CharacterId actor = Active("actor");
            InitResource(actor, Mana);
            InitResource(actor, Health);
            ContentDefinitionRecord effect = PublishFixtureEffect();
            var effectRef = new ContentDefinitionRef(effect.ContentDefinitionId, effect.Version);
            ContentDefinitionRecord published = PublishAbilityWithAdjustResourceAndApplyEffect(costMana: 3, resourceKind: Health, amountFormula: "-5", effectRef: effectRef);
            CharacterAbility ability = GrantActivatableAbility(actor, published);

            Result<ContentDefinitionRecord> archived = ContentCatalogLifecycleService.ArchiveDefinition(_catalog, new ArchiveDefinitionRequest(_campaign, effect.ContentDefinitionId, "test archive", actorIsMainGm: true, Command(), Corr));
            Assert.That(archived.IsSuccess, Is.True, archived.IsFailure ? archived.Error.Code.ToString() : string.Empty);

            ActivateAbilityRequest request = Request(actor, ability.CharacterAbilityId, actor);
            Result<AbilityActivationRecord> result = ActivateAbilityService.ActivateAbility(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, request);

            Assert.That(result.IsFailure, Is.True, "An archived EffectDefinitionRef must fail the whole activation, not silently succeed.");
            Assert.That(CurrentValue(actor, Mana), Is.EqualTo(10), "The cost charge must be reversed after the downstream ApplyEffect failure.");
            Assert.That(CurrentValue(actor, Health), Is.EqualTo(10), "The AdjustResource delta must be reversed after the downstream ApplyEffect failure.");

            Result<AbilityActivationRecord> retried = ActivateAbilityService.ActivateAbility(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, request);
            Assert.That(retried.IsFailure, Is.True, "A retry of a compensated CommandId must remain a permanent failure -- never a silent success on replay.");
            Assert.That(CurrentValue(actor, Mana), Is.EqualTo(10), "A retry must not re-apply or double-compensate.");
            Assert.That(CurrentValue(actor, Health), Is.EqualTo(10), "A retry must not re-apply or double-compensate.");
        }

        /// <summary>
        /// ODY-S06-106 doработка (product-owner-ordered fix, item 3): the character's own `CharacterAbilities`
        /// revision changes (a second, unrelated ability is granted) AFTER the caller captured its own
        /// `ExpectedCharacterAbilitiesRevision` but BEFORE `RecordAbilityActivation`'s own transaction commits
        /// -- the stale value must be rejected, not silently applied over newer state.
        /// </summary>
        [Test] // TC-ABILITY-011
        public void ActivateAbility_StaleExpectedCharacterAbilitiesRevision_RejectedNotAppliedOverStaleState()
        {
            CharacterId actor = Active("actor");
            InitResource(actor, Health);
            ContentDefinitionRecord published = PublishAbilityWithAdjustResource(costMana: 0, resourceKind: Health, amountFormula: "-3");
            CharacterAbility ability = GrantActivatableAbility(actor, published);

            CharacterRecord staleState = _characters.GetCharacter(_campaign, actor, Corr).Value;
            var staleIntent = new ActivateAbilityIntent(actor, ability.CharacterAbilityId, new[] { actor }, staleState.Revisions.CharacterAbilitiesRevision, staleState.Revisions.CharacterResourcesRevision);
            var staleRequest = new ActivateAbilityRequest(staleIntent, User(), actorIsMainGm: true, Command(), Corr);

            // Concurrent mutation: an unrelated ability is granted to the same character, bumping
            // CharacterAbilitiesRevision past what staleIntent already captured.
            Result<CharacterRecord> concurrentGrant = CharacterAdvancementService.AcquireAbility(_characters, _campaign, actor, AbilityDefinitionId.Parse("OtherAbility"), SourceKind.GMGrant, null, RankMode.None, null, null, "{}", User(), actorIsMainGm: true, null, staleState.Revisions.CharacterAbilitiesRevision, Command(), Corr);
            Assert.That(concurrentGrant.IsSuccess, Is.True);

            Result<AbilityActivationRecord> result = ActivateAbilityService.ActivateAbility(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, staleRequest);

            Assert.That(result.IsFailure, Is.True, "A stale ExpectedCharacterAbilitiesRevision must be rejected, not applied over newer state.");
            Assert.That(CurrentValue(actor, Health), Is.EqualTo(10), "Nothing must be applied when the revision check fails.");
        }

        /// <summary>
        /// ODY-S06-106 doработка (product-owner-ordered fix, item 2): `CharacterAbility.SourceRef`'s own
        /// documented contract (provenance only, null for `GMGrant`/`ProgressionPurchase`) must remain
        /// genuinely intact for an ability that is nonetheless fully activatable through the SEPARATE
        /// `ActivationDefinitionRef` bridge.
        /// </summary>
        [Test] // TC-ABILITY-012
        public void ActivateAbility_SourceRefBridgeReplaced_SourceRefContractPreserved()
        {
            CharacterId actor = Active("actor");
            InitResource(actor, Health);
            ContentDefinitionRecord published = PublishAbilityWithAdjustResource(costMana: 0, resourceKind: Health, amountFormula: "-4");
            CharacterAbility ability = GrantActivatableAbility(actor, published);

            Assert.That(ability.SourceRef, Is.Null, "SourceRef must stay null for a GMGrant ability -- never repurposed as the activation bridge.");
            Assert.That(ability.ActivationDefinitionRef, Is.Not.Null);
            Assert.That(ability.ActivationDefinitionRef!.Value.Equals(new ContentDefinitionRef(published.ContentDefinitionId, published.Version)), Is.True);

            Result<AbilityActivationRecord> result = ActivateAbilityService.ActivateAbility(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, Request(actor, ability.CharacterAbilityId, actor));

            Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error.Code.ToString() : string.Empty);
            Assert.That(CurrentValue(actor, Health), Is.EqualTo(6));
        }

        /// <summary>
        /// ODY-S06-106 doработка (second fix, product-owner-ordered after independent verification found
        /// the first compensation fix still left already-created ActiveEffect rows behind on a
        /// later-effect/later-target failure): two ApplyEffectPrimitives on one self-targeted ability -- the
        /// FIRST effect applies successfully (a real ActiveEffect row is created), the SECOND fails (its own
        /// EffectDefinitionRef is archived before activation, a real, natural rejection, not a fake test
        /// double). Proves the already-created ActiveEffect from the first effect is genuinely removed too,
        /// not just the resource deltas.
        /// </summary>
        [Test] // TC-ABILITY-013
        public void ActivateAbility_SecondApplyEffectFailsAfterFirstSucceeds_AlreadyCreatedEffectIsCompensatedToo()
        {
            CharacterId actor = Active("actor");
            InitResource(actor, Mana);
            ContentDefinitionRecord firstEffect = PublishFixtureEffect();
            ContentDefinitionRecord secondEffect = PublishFixtureEffect();
            var firstEffectRef = new ContentDefinitionRef(firstEffect.ContentDefinitionId, firstEffect.Version);
            var secondEffectRef = new ContentDefinitionRef(secondEffect.ContentDefinitionId, secondEffect.Version);
            ContentDefinitionRecord published = PublishAbilityWithTwoApplyEffects(costMana: 3, firstEffectRef, secondEffectRef);
            CharacterAbility ability = GrantActivatableAbility(actor, published);

            Result<ContentDefinitionRecord> archived = ContentCatalogLifecycleService.ArchiveDefinition(_catalog, new ArchiveDefinitionRequest(_campaign, secondEffect.ContentDefinitionId, "test archive", actorIsMainGm: true, Command(), Corr));
            Assert.That(archived.IsSuccess, Is.True, archived.IsFailure ? archived.Error.Code.ToString() : string.Empty);

            ActivateAbilityRequest request = Request(actor, ability.CharacterAbilityId, actor);
            Result<AbilityActivationRecord> result = ActivateAbilityService.ActivateAbility(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, request);

            Assert.That(result.IsFailure, Is.True, "The second, archived ApplyEffect must fail the whole activation.");
            Assert.That(CurrentValue(actor, Mana), Is.EqualTo(10), "The cost charge must be reversed.");

            Assert.That(CountStillAttached(actor), Is.EqualTo(0), "The FIRST effect, already successfully created before the second one failed, must be removed too -- not left attached (ADR-012's append-only history keeps the row itself, transitioned to Status=Removed, not physically deleted).");

            Result<AbilityActivationRecord> retried = ActivateAbilityService.ActivateAbility(_reader, _apply, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, request);
            Assert.That(retried.IsFailure, Is.True, "A retry of a compensated CommandId must remain a permanent failure -- never a silent success, and never a second removal/creation attempt.");
            Assert.That(CurrentValue(actor, Mana), Is.EqualTo(10));
            Assert.That(CountStillAttached(actor), Is.EqualTo(0));
        }

        /// <summary>
        /// ODY-S06-106 doработка (THIRD fix, product-owner-ordered after independent verification found a
        /// retry after an INCOMPLETE compensation attempt was silently reported as `Success`): injects a
        /// real failure INSIDE `CompensateAbilityActivation`'s own removal loop itself (not inside the
        /// original `ApplyEffect` application, which `TC-ABILITY-010`/`013` already cover) -- three
        /// `ApplyEffectPrimitive`s, the first two create real `ActiveEffect`s, the third fails (archived
        /// ref), triggering compensation of the first two; a decorator wrapping the real
        /// `IActiveEffectRepository` fails only the SECOND `RemoveActiveEffect` call (a real, distinct
        /// `Result.Failure`, not a crash) while the first genuinely succeeds against the real database.
        /// </summary>
        [Test] // TC-ABILITY-014
        public void ActivateAbility_CompensationItselfFailsPartway_RetryDoesNotSilentlySucceed()
        {
            CharacterId actor = Active("actor");
            InitResource(actor, Mana);
            ContentDefinitionRecord firstEffect = PublishFixtureEffect();
            ContentDefinitionRecord secondEffect = PublishFixtureEffect();
            ContentDefinitionRecord thirdEffect = PublishFixtureEffect();
            var firstEffectRef = new ContentDefinitionRef(firstEffect.ContentDefinitionId, firstEffect.Version);
            var secondEffectRef = new ContentDefinitionRef(secondEffect.ContentDefinitionId, secondEffect.Version);
            var thirdEffectRef = new ContentDefinitionRef(thirdEffect.ContentDefinitionId, thirdEffect.Version);
            ContentDefinitionRecord published = PublishAbilityWithThreeApplyEffects(costMana: 3, firstEffectRef, secondEffectRef, thirdEffectRef);
            CharacterAbility ability = GrantActivatableAbility(actor, published);

            Result<ContentDefinitionRecord> archived = ContentCatalogLifecycleService.ArchiveDefinition(_catalog, new ArchiveDefinitionRequest(_campaign, thirdEffect.ContentDefinitionId, "test archive", actorIsMainGm: true, Command(), Corr));
            Assert.That(archived.IsSuccess, Is.True, archived.IsFailure ? archived.Error.Code.ToString() : string.Empty);

            ActivateAbilityRequest request = Request(actor, ability.CharacterAbilityId, actor);

            // First call: compensation removes the FIRST already-created effect for real, then fails on the
            // SECOND removal (a real, distinct Result.Failure from the decorator, not a crash) -- before
            // resource reversal or CompensatedAt is ever reached.
            var failingEffects = new FailsNthRemovalActiveEffectRepository(_effects, failOnCallNumber: 2);
            var apply1 = new SqliteActivateAbilityRepository(_clock, failingEffects);
            Result<AbilityActivationRecord> first = ActivateAbilityService.ActivateAbility(_reader, apply1, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, request);
            Assert.That(first.IsFailure, Is.True, "The third, archived ApplyEffect must fail the whole activation.");
            Assert.That(CurrentValue(actor, Mana), Is.EqualTo(7), "Compensation itself failed on the second effect removal before ever reaching resource reversal -- the cost charge is still applied, not yet reversed.");
            Assert.That(CountStillAttached(actor), Is.EqualTo(1), "Exactly one of the two already-created effects (the second) must still be attached -- the first was genuinely removed before the injected failure.");

            // Retry with the SAME CommandId, this time through a repository whose own IActiveEffectRepository
            // is the real, undecorated one -- proving CompensateAbilityActivation genuinely finishes the SAME
            // durable removal set the first attempt started (not a fresh, possibly-different one), and that
            // the overall retry call NEVER reports Success even though the resumed compensation now completes.
            var apply2 = new SqliteActivateAbilityRepository(_clock, _effects);
            Result<AbilityActivationRecord> retried = ActivateAbilityService.ActivateAbility(_reader, apply2, _catalog, _effects, new ThrowingRandomFactory(), _clock, _campaign, Epoch, request);
            Assert.That(retried.IsFailure, Is.True, "A retry after an incomplete compensation attempt must never be reported as Success, even once the resumed compensation now genuinely finishes.");
            Assert.That(CurrentValue(actor, Mana), Is.EqualTo(10), "The resumed compensation call must finish reversing the cost charge.");
            Assert.That(CountStillAttached(actor), Is.EqualTo(0), "The resumed compensation call must finish removing the second effect too -- consistent final state, not a state frozen halfway and masked as success.");
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

        private ContentDefinitionRecord PublishAbilityWithAdjustResourceAndApplyEffect(long costMana, ResourceDefinitionId resourceKind, string amountFormula, ContentDefinitionRef effectRef)
        {
            var envelope = new MechanicsPrimitiveEnvelope(1, new MechanicsPrimitive[] { new AdjustResourcePrimitive(resourceKind, amountFormula), new ApplyEffectPrimitive(effectRef) });
            System.Collections.Generic.IReadOnlyList<AbilityResourceCost> costs = costMana > 0 ? new[] { new AbilityResourceCost(Mana, costMana) } : Array.Empty<AbilityResourceCost>();
            var targetRule = new ContentTargetRule(ContentTargetSource.ActingCharacter, 1, 1, true);
            var ability = new AbilityDefinition(AbilityEntryPointType.ActiveAction, "OnUse", actionCost: 0, costs, targetRule, MechanicsPayloadCodec.EncodePrimitives(envelope));
            return PublishFixture(AuthorDraft(ContentDefinitionType.Ability, "Test Ability " + Guid.NewGuid().ToString("N"), TypedDefinitionCodec.EncodeAbility(ability), dependencyRefs: new[] { effectRef }));
        }

        private ContentDefinitionRecord PublishAbilityWithApplyEffect(ContentDefinitionRef effectRef)
        {
            var envelope = new MechanicsPrimitiveEnvelope(1, new MechanicsPrimitive[] { new ApplyEffectPrimitive(effectRef) });
            var targetRule = new ContentTargetRule(ContentTargetSource.ActingCharacter, 1, 1, true);
            var ability = new AbilityDefinition(AbilityEntryPointType.ActiveAction, "OnUse", actionCost: 0, Array.Empty<AbilityResourceCost>(), targetRule, MechanicsPayloadCodec.EncodePrimitives(envelope));
            return PublishFixture(AuthorDraft(ContentDefinitionType.Ability, "Test Ability " + Guid.NewGuid().ToString("N"), TypedDefinitionCodec.EncodeAbility(ability), dependencyRefs: new[] { effectRef }));
        }

        /// <summary>ODY-S06-106 doработка (second fix, TC-ABILITY-013): two `ApplyEffectPrimitive`s on one self-targeted ability -- the SECOND one is meant to be archived by the caller before activation, so the loop succeeds on the first effect (a real ActiveEffect gets created) before failing on the second.</summary>
        private ContentDefinitionRecord PublishAbilityWithTwoApplyEffects(long costMana, ContentDefinitionRef firstEffectRef, ContentDefinitionRef secondEffectRef)
        {
            var envelope = new MechanicsPrimitiveEnvelope(1, new MechanicsPrimitive[] { new ApplyEffectPrimitive(firstEffectRef), new ApplyEffectPrimitive(secondEffectRef) });
            System.Collections.Generic.IReadOnlyList<AbilityResourceCost> costs = costMana > 0 ? new[] { new AbilityResourceCost(Mana, costMana) } : Array.Empty<AbilityResourceCost>();
            var targetRule = new ContentTargetRule(ContentTargetSource.ActingCharacter, 1, 1, true);
            var ability = new AbilityDefinition(AbilityEntryPointType.ActiveAction, "OnUse", actionCost: 0, costs, targetRule, MechanicsPayloadCodec.EncodePrimitives(envelope));
            return PublishFixture(AuthorDraft(ContentDefinitionType.Ability, "Test Ability " + Guid.NewGuid().ToString("N"), TypedDefinitionCodec.EncodeAbility(ability), dependencyRefs: new[] { firstEffectRef, secondEffectRef }));
        }

        /// <summary>ODY-S06-106 doработка (third fix, TC-ABILITY-014): THREE `ApplyEffectPrimitive`s -- the first two are meant to stay Published (both real `ActiveEffect`s get created), the third is meant to be archived by the caller before activation, so compensation has TWO already-created effects to remove.</summary>
        private ContentDefinitionRecord PublishAbilityWithThreeApplyEffects(long costMana, ContentDefinitionRef firstEffectRef, ContentDefinitionRef secondEffectRef, ContentDefinitionRef thirdEffectRef)
        {
            var envelope = new MechanicsPrimitiveEnvelope(1, new MechanicsPrimitive[] { new ApplyEffectPrimitive(firstEffectRef), new ApplyEffectPrimitive(secondEffectRef), new ApplyEffectPrimitive(thirdEffectRef) });
            System.Collections.Generic.IReadOnlyList<AbilityResourceCost> costs = costMana > 0 ? new[] { new AbilityResourceCost(Mana, costMana) } : Array.Empty<AbilityResourceCost>();
            var targetRule = new ContentTargetRule(ContentTargetSource.ActingCharacter, 1, 1, true);
            var ability = new AbilityDefinition(AbilityEntryPointType.ActiveAction, "OnUse", actionCost: 0, costs, targetRule, MechanicsPayloadCodec.EncodePrimitives(envelope));
            return PublishFixture(AuthorDraft(ContentDefinitionType.Ability, "Test Ability " + Guid.NewGuid().ToString("N"), TypedDefinitionCodec.EncodeAbility(ability), dependencyRefs: new[] { firstEffectRef, secondEffectRef, thirdEffectRef }));
        }

        /// <summary>
        /// ODY-S06-106 doработка (product-owner-ordered fix): grants the ability with `sourceRef: null`
        /// (SourceKind.GMGrant's own documented default -- provenance, not activation, per `Ability.cs`'s
        /// own doc comment), then links activation eligibility via the SEPARATE, additive
        /// `ICharacterRepository.LinkAbilityActivationSource` command -- never repurposing `SourceRef`.
        /// </summary>
        private CharacterAbility GrantActivatableAbility(CharacterId characterId, ContentDefinitionRecord published)
        {
            CharacterRecord current = _characters.GetCharacter(_campaign, characterId, Corr).Value;
            Result<CharacterRecord> acquired = CharacterAdvancementService.AcquireAbility(_characters, _campaign, characterId, TestAbilityKey, SourceKind.GMGrant, null, RankMode.None, null, null, "{}", User(), actorIsMainGm: true, null, current.Revisions.CharacterAbilitiesRevision, Command(), Corr);
            Assert.That(acquired.IsSuccess, Is.True);
            CharacterAbility? granted = null;
            foreach (CharacterAbility candidate in acquired.Value.Abilities)
            {
                if (candidate.AbilityDefinitionId.Equals(TestAbilityKey) && candidate.SourceRef == null) { granted = candidate; break; }
            }

            if (granted == null)
            {
                Assert.Fail("Granted ability not found on the reloaded CharacterRecord.");
                return null!;
            }

            var activationRef = new ContentDefinitionRef(published.ContentDefinitionId, published.Version);
            Result<CharacterRecord> linked = _characters.LinkAbilityActivationSource(_campaign, characterId, granted.CharacterAbilityId, activationRef, User(), actorIsMainGm: true, acquired.Value.Revisions.CharacterAbilitiesRevision, Command(), Corr);
            Assert.That(linked.IsSuccess, Is.True, linked.IsFailure ? linked.Error.Code.ToString() : string.Empty);
            foreach (CharacterAbility candidate in linked.Value.Abilities)
            {
                if (candidate.CharacterAbilityId.Equals(granted.CharacterAbilityId)) return candidate;
            }

            Assert.Fail("Linked ability not found on the reloaded CharacterRecord.");
            return null!;
        }

        private ActivateAbilityRequest Request(CharacterId actor, CharacterAbilityId characterAbilityId, params CharacterId[] targets)
        {
            CharacterRecord current = _characters.GetCharacter(_campaign, actor, Corr).Value;
            var intent = new ActivateAbilityIntent(actor, characterAbilityId, targets, current.Revisions.CharacterAbilitiesRevision, current.Revisions.CharacterResourcesRevision);
            return new ActivateAbilityRequest(intent, User(), actorIsMainGm: true, Command(), Corr);
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
            foreach (CharacterResource resource in current.Resources)
            {
                if (resource.ResourceDefinitionId.Equals(resourceKind)) return resource.CurrentValue;
            }

            Assert.Fail("Resource " + resourceKind + " not found on " + characterId + ".");
            return -1;
        }

        /// <summary>ODY-S06-106 doработка (second fix, TC-ABILITY-013): `ListActiveEffectsByTarget` returns every row regardless of `Status` (ADR-012's append-only history keeps a `Removed` row, never physically deletes it) -- this counts only `Active`/`Suspended` rows, i.e. effects genuinely still affecting the target.</summary>
        private long CountStillAttached(CharacterId characterId)
        {
            Result<System.Collections.Generic.IReadOnlyList<ActiveEffectRecord>> onTarget = _effects.ListActiveEffectsByTarget(_campaign, _campaign.CampaignId, ActiveEffectTargetRef.ForCharacter(characterId), Corr);
            Assert.That(onTarget.IsSuccess, Is.True);
            long count = 0;
            foreach (ActiveEffectRecord record in onTarget.Value)
            {
                if (record.Effect.Status == ActiveEffectStatus.Active || record.Effect.Status == ActiveEffectStatus.Suspended) count++;
            }

            return count;
        }

        private CharacterRecord GrantAttribute(CharacterId characterId, string attributeName, long value)
        {
            CharacterRecord current = _characters.GetCharacter(_campaign, characterId, Corr).Value;
            Result<CharacterRecord> granted = _characters.GrantDevelopmentPoints(_campaign, characterId, 100, "test", User(), actorIsMainGm: true, current.Revisions.MechanicsRevision, Command(), Corr);
            Assert.That(granted.IsSuccess, Is.True);
            Result<CharacterRecord> purchased = CharacterAdvancementService.PurchaseAttributeIncrease(_characters, _campaign, characterId, AttributeDefinitionId.Parse(attributeName), value, User(), actorIsMainGm: true, granted.Value.Revisions.MechanicsRevision, expectedAttributeRevision: 0, Command(), Corr);
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

        /// <summary>
        /// ODY-S06-106 doработка (THIRD fix, TC-ABILITY-014): a real `IActiveEffectRepository`, forwarding
        /// every call to the real, unmodified `_inner` implementation except that the `failOnCallNumber`-th
        /// call to `RemoveActiveEffect` (across the whole repository, 1-based) returns a real, distinct
        /// `Result.Failure` instead of touching the database at all -- models a genuine transient removal
        /// failure (e.g. a real IO hiccup `SqliteActiveEffectRepository`'s own catch block would itself
        /// produce), mirroring this same test file's own established precedent of small, real interface
        /// implementations controlling one specific, deterministic behavior (`ThrowingRandomFactory`/
        /// `FixedRandomStreamFactory`/`CountingRandomFactory` above) -- not a mocking framework, not bypassing
        /// any real logic for calls it does not intercept.
        /// </summary>
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
            public Result<System.Collections.Generic.IReadOnlyList<ActiveEffectRecord>> ListActiveEffectsByTarget(CampaignHandle campaign, CampaignId campaignId, ActiveEffectTargetRef targetRef, CorrelationId correlationId) => _inner.ListActiveEffectsByTarget(campaign, campaignId, targetRef, correlationId);
            public Result<System.Collections.Generic.IReadOnlyList<ActiveEffectRecord>> ListActiveEffectsBySource(CampaignHandle campaign, CampaignId campaignId, ActiveEffectSourceRef sourceRef, CorrelationId correlationId) => _inner.ListActiveEffectsBySource(campaign, campaignId, sourceRef, correlationId);
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
