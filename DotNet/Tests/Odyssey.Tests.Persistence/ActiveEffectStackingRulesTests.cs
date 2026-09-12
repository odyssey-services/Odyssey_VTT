using System;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Effects;
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
using Odyssey.Rules.Effects;

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S05-503: real tests for `ADR-028` §7's own 7 `EffectStackPolicy`
    /// behaviors (`ActiveEffectStackingRules.ResolveStacking`), the
    /// `RequestGMResolution` pending-conflict round trip
    /// (`ActiveEffectStackConflict`/`ResolveActiveEffectStackConflict`), and
    /// `Odyssey.Rules.Effects.EffectStackRules.IsStronger`. This is a pure
    /// decision layer -- consistent with `ItemDefinitionMigrationRulesTests.cs`'s
    /// own precedent for `ComputeBlockingIssues` (`ODY-S05-402`) -- no new
    /// persistence method exists on `IActiveEffectRepository`/
    /// `SqliteActiveEffectRepository` for this task (see this task's own task
    /// contract §18); `CreateActiveEffect`/`GetActiveEffect` (`ODY-S05-502`)
    /// are reused, unmodified, only to build realistic already-persisted
    /// "existing conflicting effect" fixtures.
    /// </summary>
    public sealed class ActiveEffectStackingRulesTests
    {
        private static readonly CorrelationId Corr = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaigns = null!;
        private SqliteActiveEffectRepository _activeEffects = null!;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s05-503-" + Guid.NewGuid().ToString("N"));
            _campaigns = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> created = _campaigns.Create(new CreateCampaignRequest(_campaignDir, "Stacking Rules Test Campaign", "ruleset.core", "1.0.0", "0.1.0"), NewCommandId(), Corr);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _activeEffects = new SqliteActiveEffectRepository(Clock);
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaigns.Close(_campaign, Corr); } catch (IOException) { }
            try { if (Directory.Exists(_campaignDir)) Directory.Delete(_campaignDir, true); } catch (IOException) { }
        }

        // ---- helpers ----

        private ContentDefinitionRef NewEffectRef() => new ContentDefinitionRef(ContentDefinitionId.NewId(Clock.GetUtcNow()), 1);

        private ActiveEffectRecord NewRecord(ContentDefinitionRef effectRef, long snapshotVersion, ActiveEffectTargetRef targetRef, UtcInstant appliedAt, UtcInstant? expiresAt = null)
        {
            var snapshot = new EffectMechanicsSnapshot(effectRef, snapshotVersion, ContentDefinitionType.Effect, "{\"duration\":\"instant\"}");
            var effect = new ActiveEffect(ActiveEffectId.NewId(Clock.GetUtcNow()), effectRef, snapshot, ActiveEffectSourceRef.ForGMDirect(), targetRef, ActiveEffectStatus.Active, 1, NewUserId(), appliedAt, expiresAt, 1);
            return new ActiveEffectRecord(_campaign.CampaignId, effect);
        }

        /// <summary>Persists <paramref name="record"/> for real via `ODY-S05-502`'s own unmodified `CreateActiveEffect`, then reads it back -- so "existing conflicting effect" fixtures in these tests are genuinely already-persisted rows, not merely in-memory constructions.</summary>
        private ActiveEffectRecord PersistAsExisting(ActiveEffectRecord record)
        {
            Result<ActiveEffectRecord> created = _activeEffects.CreateActiveEffect(_campaign, record, NewCommandId(), Corr);
            Assert.That(created.IsSuccess, Is.True, created.IsFailure ? created.Error.Code.ToString() : string.Empty);
            Result<ActiveEffectRecord> fetched = _activeEffects.GetActiveEffect(_campaign, record.Effect.ActiveEffectId, Corr);
            Assert.That(fetched.IsSuccess, Is.True);
            return fetched.Value;
        }

        // ---- IndependentInstances ----

        [Test] // TC-ACTIVEEFFECT-020
        public void ResolveStacking_IndependentInstances_AlwaysCreatesANewRow_EvenWithAConflictingExistingEffect()
        {
            ContentDefinitionRef effectRef = NewEffectRef();
            ActiveEffectTargetRef target = ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow()));
            ActiveEffectRecord existing = PersistAsExisting(NewRecord(effectRef, 1, target, Clock.GetUtcNow()));
            ActiveEffectRecord candidate = NewRecord(effectRef, 1, target, Clock.GetUtcNow());

            ActiveEffectStackDecision decision = ActiveEffectStackingRules.ResolveStacking(EffectStackPolicy.IndependentInstances, existing, candidate, Clock.GetUtcNow());

            Assert.That(decision.Kind, Is.EqualTo(ActiveEffectStackDecisionKind.CreateNewEffect));
            Assert.That(decision.EffectToCreate, Is.SameAs(candidate));
            Assert.That(decision.ExistingActiveEffectId, Is.Null);
        }

        // ---- No existing conflicting effect: every policy behaves like a first application ----

        [Test] // TC-ACTIVEEFFECT-021
        public void ResolveStacking_NoExistingConflictingEffect_AlwaysCreatesANewRow_RegardlessOfPolicy()
        {
            ContentDefinitionRef effectRef = NewEffectRef();
            ActiveEffectTargetRef target = ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow()));
            ActiveEffectRecord candidate = NewRecord(effectRef, 1, target, Clock.GetUtcNow());

            ActiveEffectStackDecision decision = ActiveEffectStackingRules.ResolveStacking(EffectStackPolicy.IgnoreNewApplication, null, candidate, Clock.GetUtcNow());

            Assert.That(decision.Kind, Is.EqualTo(ActiveEffectStackDecisionKind.CreateNewEffect));
            Assert.That(decision.EffectToCreate, Is.SameAs(candidate));
        }

        // ---- RefreshDuration ----

        [Test] // TC-ACTIVEEFFECT-022
        public void ResolveStacking_RefreshDuration_RefreshesTheExistingRowsAppliedAtAndExpiresAt_NoNewRow()
        {
            ContentDefinitionRef effectRef = NewEffectRef();
            ActiveEffectTargetRef target = ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow()));
            UtcInstant originalAppliedAt = UtcInstant.Parse("2026-09-12T00:00:00.0000000Z");
            ActiveEffectRecord existing = PersistAsExisting(NewRecord(effectRef, 1, target, originalAppliedAt));

            UtcInstant refreshedAt = UtcInstant.Parse("2026-09-12T01:00:00.0000000Z");
            UtcInstant refreshedExpiry = UtcInstant.Parse("2026-09-12T02:00:00.0000000Z");
            ActiveEffectRecord candidate = NewRecord(effectRef, 1, target, refreshedAt, refreshedExpiry);

            ActiveEffectStackDecision decision = ActiveEffectStackingRules.ResolveStacking(EffectStackPolicy.RefreshDuration, existing, candidate, Clock.GetUtcNow());

            Assert.That(decision.Kind, Is.EqualTo(ActiveEffectStackDecisionKind.RefreshExistingDuration));
            Assert.That(decision.ExistingActiveEffectId, Is.EqualTo(existing.Effect.ActiveEffectId));
            Assert.That(decision.RefreshedAppliedAt, Is.EqualTo(refreshedAt));
            Assert.That(decision.RefreshedExpiresAt, Is.EqualTo(refreshedExpiry));
            Assert.That(decision.EffectToCreate, Is.Null, "RefreshDuration never creates a second row");
        }

        // ---- ReplaceIfStronger ----

        [Test] // TC-ACTIVEEFFECT-023
        public void ResolveStacking_ReplaceIfStronger_WhenCandidateIsStronger_ReplacesTheExistingRow()
        {
            ContentDefinitionRef effectRef = NewEffectRef();
            ActiveEffectTargetRef target = ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow()));
            ActiveEffectRecord existing = PersistAsExisting(NewRecord(effectRef, 1, target, Clock.GetUtcNow()));
            ActiveEffectRecord strongerCandidate = NewRecord(effectRef, 2, target, Clock.GetUtcNow());

            ActiveEffectStackDecision decision = ActiveEffectStackingRules.ResolveStacking(EffectStackPolicy.ReplaceIfStronger, existing, strongerCandidate, Clock.GetUtcNow());

            Assert.That(decision.Kind, Is.EqualTo(ActiveEffectStackDecisionKind.ReplaceExistingEffect));
            Assert.That(decision.ExistingActiveEffectId, Is.EqualTo(existing.Effect.ActiveEffectId));
            Assert.That(decision.EffectToCreate, Is.SameAs(strongerCandidate));
        }

        [Test] // TC-ACTIVEEFFECT-024
        public void ResolveStacking_ReplaceIfStronger_WhenCandidateIsNotStronger_IgnoresTheNewApplication()
        {
            ContentDefinitionRef effectRef = NewEffectRef();
            ActiveEffectTargetRef target = ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow()));
            ActiveEffectRecord existing = PersistAsExisting(NewRecord(effectRef, 2, target, Clock.GetUtcNow()));
            ActiveEffectRecord tiedCandidate = NewRecord(effectRef, 2, target, Clock.GetUtcNow());

            ActiveEffectStackDecision decision = ActiveEffectStackingRules.ResolveStacking(EffectStackPolicy.ReplaceIfStronger, existing, tiedCandidate, Clock.GetUtcNow());

            Assert.That(decision.Kind, Is.EqualTo(ActiveEffectStackDecisionKind.IgnoreNewApplication));
            Assert.That(decision.EffectToCreate, Is.Null);
            Assert.That(decision.ExistingActiveEffectId, Is.Null, "an ignored application leaves the existing row completely untouched, including its own identity being absent from the decision");
        }

        // ---- ReplaceExisting ----

        [Test] // TC-ACTIVEEFFECT-025
        public void ResolveStacking_ReplaceExisting_AlwaysReplaces_RegardlessOfRelativeStrength()
        {
            ContentDefinitionRef effectRef = NewEffectRef();
            ActiveEffectTargetRef target = ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow()));
            ActiveEffectRecord existing = PersistAsExisting(NewRecord(effectRef, 5, target, Clock.GetUtcNow()));
            ActiveEffectRecord weakerCandidate = NewRecord(effectRef, 1, target, Clock.GetUtcNow());

            ActiveEffectStackDecision decision = ActiveEffectStackingRules.ResolveStacking(EffectStackPolicy.ReplaceExisting, existing, weakerCandidate, Clock.GetUtcNow());

            Assert.That(decision.Kind, Is.EqualTo(ActiveEffectStackDecisionKind.ReplaceExistingEffect));
            Assert.That(decision.ExistingActiveEffectId, Is.EqualTo(existing.Effect.ActiveEffectId));
            Assert.That(decision.EffectToCreate, Is.SameAs(weakerCandidate));
        }

        // ---- IncreaseStacks ----

        [Test] // TC-ACTIVEEFFECT-026
        public void ResolveStacking_IncreaseStacks_IncreasesTheExistingRowsStackCount_NoNewRow()
        {
            ContentDefinitionRef effectRef = NewEffectRef();
            ActiveEffectTargetRef target = ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow()));
            ActiveEffectRecord existing = PersistAsExisting(NewRecord(effectRef, 1, target, Clock.GetUtcNow()));
            ActiveEffectRecord candidate = NewRecord(effectRef, 1, target, Clock.GetUtcNow());

            ActiveEffectStackDecision decision = ActiveEffectStackingRules.ResolveStacking(EffectStackPolicy.IncreaseStacks, existing, candidate, Clock.GetUtcNow());

            Assert.That(decision.Kind, Is.EqualTo(ActiveEffectStackDecisionKind.IncreaseExistingStack));
            Assert.That(decision.ExistingActiveEffectId, Is.EqualTo(existing.Effect.ActiveEffectId));
            Assert.That(decision.EffectToCreate, Is.Null);
        }

        // ---- IgnoreNewApplication ----

        [Test] // TC-ACTIVEEFFECT-027
        public void ResolveStacking_IgnoreNewApplication_ProducesNoMutationAtAll()
        {
            ContentDefinitionRef effectRef = NewEffectRef();
            ActiveEffectTargetRef target = ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow()));
            ActiveEffectRecord existing = PersistAsExisting(NewRecord(effectRef, 1, target, Clock.GetUtcNow()));
            ActiveEffectRecord candidate = NewRecord(effectRef, 1, target, Clock.GetUtcNow());

            ActiveEffectStackDecision decision = ActiveEffectStackingRules.ResolveStacking(EffectStackPolicy.IgnoreNewApplication, existing, candidate, Clock.GetUtcNow());

            Assert.That(decision.Kind, Is.EqualTo(ActiveEffectStackDecisionKind.IgnoreNewApplication));
            Assert.That(decision.EffectToCreate, Is.Null);
            Assert.That(decision.ExistingActiveEffectId, Is.Null);
        }

        // ---- RequestGMResolution + full resolution cycle ----

        [Test] // TC-ACTIVEEFFECT-028
        public void ResolveStacking_RequestGMResolution_RaisesAPendingConflict_WithNoImmediateMutation()
        {
            ContentDefinitionRef effectRef = NewEffectRef();
            ActiveEffectTargetRef target = ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow()));
            ActiveEffectRecord existing = PersistAsExisting(NewRecord(effectRef, 1, target, Clock.GetUtcNow()));
            ActiveEffectRecord candidate = NewRecord(effectRef, 1, target, Clock.GetUtcNow());
            UtcInstant now = Clock.GetUtcNow();

            ActiveEffectStackDecision decision = ActiveEffectStackingRules.ResolveStacking(EffectStackPolicy.RequestGMResolution, existing, candidate, now);

            Assert.That(decision.Kind, Is.EqualTo(ActiveEffectStackDecisionKind.RequestGmResolution));
            Assert.That(decision.EffectToCreate, Is.Null, "the item-use command succeeds, but no row is created until the conflict is resolved");
            Assert.That(decision.ExistingActiveEffectId, Is.Null);
            Assert.That(decision.Conflict, Is.Not.Null);
            Assert.That(decision.Conflict!.CandidateApplication, Is.SameAs(candidate));
            Assert.That(decision.Conflict.ConflictingActiveEffectId, Is.EqualTo(existing.Effect.ActiveEffectId));
            Assert.That(decision.Conflict.RaisedAt, Is.EqualTo(now));
        }

        [Test] // TC-ACTIVEEFFECT-029
        public void ResolveActiveEffectStackConflict_ApplyAsIndependentInstance_CreatesTheCandidateAsANewRow()
        {
            ContentDefinitionRef effectRef = NewEffectRef();
            ActiveEffectTargetRef target = ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow()));
            ActiveEffectRecord existing = PersistAsExisting(NewRecord(effectRef, 1, target, Clock.GetUtcNow()));
            ActiveEffectRecord candidate = NewRecord(effectRef, 1, target, Clock.GetUtcNow());
            var conflict = new ActiveEffectStackConflict(candidate, existing.Effect.ActiveEffectId, Clock.GetUtcNow());

            ActiveEffectStackDecision decision = ActiveEffectStackingRules.ResolveActiveEffectStackConflict(conflict, ActiveEffectStackConflictResolution.ApplyAsIndependentInstance);

            Assert.That(decision.Kind, Is.EqualTo(ActiveEffectStackDecisionKind.CreateNewEffect));
            Assert.That(decision.EffectToCreate, Is.SameAs(candidate));
            Assert.That(decision.ExistingActiveEffectId, Is.Null, "an independent instance leaves the original conflicting row alone");
        }

        [Test] // TC-ACTIVEEFFECT-030
        public void ResolveActiveEffectStackConflict_Replace_ReplacesTheOriginalConflictingRow()
        {
            ContentDefinitionRef effectRef = NewEffectRef();
            ActiveEffectTargetRef target = ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow()));
            ActiveEffectRecord existing = PersistAsExisting(NewRecord(effectRef, 1, target, Clock.GetUtcNow()));
            ActiveEffectRecord candidate = NewRecord(effectRef, 1, target, Clock.GetUtcNow());
            var conflict = new ActiveEffectStackConflict(candidate, existing.Effect.ActiveEffectId, Clock.GetUtcNow());

            ActiveEffectStackDecision decision = ActiveEffectStackingRules.ResolveActiveEffectStackConflict(conflict, ActiveEffectStackConflictResolution.Replace);

            Assert.That(decision.Kind, Is.EqualTo(ActiveEffectStackDecisionKind.ReplaceExistingEffect));
            Assert.That(decision.EffectToCreate, Is.SameAs(candidate));
            Assert.That(decision.ExistingActiveEffectId, Is.EqualTo(existing.Effect.ActiveEffectId));
        }

        [Test] // TC-ACTIVEEFFECT-031
        public void ResolveActiveEffectStackConflict_Ignore_ProducesNoMutationAtAll()
        {
            ContentDefinitionRef effectRef = NewEffectRef();
            ActiveEffectTargetRef target = ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow()));
            ActiveEffectRecord existing = PersistAsExisting(NewRecord(effectRef, 1, target, Clock.GetUtcNow()));
            ActiveEffectRecord candidate = NewRecord(effectRef, 1, target, Clock.GetUtcNow());
            var conflict = new ActiveEffectStackConflict(candidate, existing.Effect.ActiveEffectId, Clock.GetUtcNow());

            ActiveEffectStackDecision decision = ActiveEffectStackingRules.ResolveActiveEffectStackConflict(conflict, ActiveEffectStackConflictResolution.Ignore);

            Assert.That(decision.Kind, Is.EqualTo(ActiveEffectStackDecisionKind.IgnoreNewApplication));
            Assert.That(decision.EffectToCreate, Is.Null);
            Assert.That(decision.ExistingActiveEffectId, Is.Null);
        }

        // ---- Odyssey.Rules.Effects.EffectStackRules.IsStronger ----

        [Test] // TC-ACTIVEEFFECT-032
        public void EffectStackRules_IsStronger_ReturnsTrue_WhenCandidateHasAHigherDefinitionSnapshotVersion()
        {
            ContentDefinitionRef effectRef = NewEffectRef();
            var older = new EffectMechanicsSnapshot(effectRef, 1, ContentDefinitionType.Effect, "{}");
            var newer = new EffectMechanicsSnapshot(effectRef, 2, ContentDefinitionType.Effect, "{}");

            Assert.That(EffectStackRules.IsStronger(newer, older), Is.True);
            Assert.That(EffectStackRules.IsStronger(older, newer), Is.False);
        }

        [Test] // TC-ACTIVEEFFECT-033
        public void EffectStackRules_IsStronger_ReturnsFalse_OnATieBetweenEqualDefinitionSnapshotVersions()
        {
            ContentDefinitionRef effectRef = NewEffectRef();
            var one = new EffectMechanicsSnapshot(effectRef, 3, ContentDefinitionType.Effect, "{}");
            var other = new EffectMechanicsSnapshot(effectRef, 3, ContentDefinitionType.Effect, "{}");

            Assert.That(EffectStackRules.IsStronger(one, other), Is.False, "a tie is deliberately NOT stronger -- ReplaceIfStronger then behaves as IgnoreNewApplication, the least destructive outcome");
        }

        // ---- Precondition guards ----

        [Test] // TC-ACTIVEEFFECT-034
        public void ResolveStacking_RejectsAnExistingConflictingEffectTargetingADifferentTargetRef()
        {
            ContentDefinitionRef effectRef = NewEffectRef();
            ActiveEffectRecord existing = PersistAsExisting(NewRecord(effectRef, 1, ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow())), Clock.GetUtcNow()));
            ActiveEffectRecord candidate = NewRecord(effectRef, 1, ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow())), Clock.GetUtcNow());

            Action act = () => ActiveEffectStackingRules.ResolveStacking(EffectStackPolicy.IndependentInstances, existing, candidate, Clock.GetUtcNow());
            Assert.Throws<ArgumentException>(act);
        }

        // ---- Realistic end-to-end fixture: a genuinely persisted existing row drives a real decision ----

        [Test] // TC-ACTIVEEFFECT-035
        public void ResolveStacking_ReplaceExisting_AgainstAGenuinelyPersistedExistingRow_ProducesADecisionReferencingItsRealId()
        {
            ContentDefinitionRef effectRef = NewEffectRef();
            ActiveEffectTargetRef target = ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow()));
            ActiveEffectRecord persistedExisting = PersistAsExisting(NewRecord(effectRef, 1, target, Clock.GetUtcNow()));

            // Re-load independently (as a real caller would, e.g. via ListActiveEffectsByTarget) rather than reusing the in-memory reference.
            Result<ActiveEffectRecord> reloaded = _activeEffects.GetActiveEffect(_campaign, persistedExisting.Effect.ActiveEffectId, Corr);
            Assert.That(reloaded.IsSuccess, Is.True);

            ActiveEffectRecord candidate = NewRecord(effectRef, 1, target, Clock.GetUtcNow());
            ActiveEffectStackDecision decision = ActiveEffectStackingRules.ResolveStacking(EffectStackPolicy.ReplaceExisting, reloaded.Value, candidate, Clock.GetUtcNow());

            Assert.That(decision.Kind, Is.EqualTo(ActiveEffectStackDecisionKind.ReplaceExistingEffect));
            Assert.That(decision.ExistingActiveEffectId, Is.EqualTo(persistedExisting.Effect.ActiveEffectId));
        }
    }
}
