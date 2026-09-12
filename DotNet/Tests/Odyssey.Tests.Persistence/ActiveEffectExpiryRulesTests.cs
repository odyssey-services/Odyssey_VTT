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
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;
using Odyssey.Rules.Effects;

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S05-504: real tests for `ADR-028` §8's own 8 presently-implementable,
    /// non-`WhileItemEquipped` `EffectDurationType` expiry mechanisms
    /// (`ActiveEffectExpiryRules`), the fail-closed rule (`ADR-028` §11) as an
    /// explicit code path, `Odyssey.Rules.Effects.EffectConditionRules`, and
    /// the new `SqliteActiveEffectRepository.ExpireActiveEffect` `Status →
    /// Expired` transition. No `WhileItemEquipped`, turn/round-based,
    /// stacking, or removal logic is introduced or exercised here.
    /// </summary>
    public sealed class ActiveEffectExpiryRulesTests
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
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s05-504-" + Guid.NewGuid().ToString("N"));
            _campaigns = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> created = _campaigns.Create(new CreateCampaignRequest(_campaignDir, "Expiry Rules Test Campaign", "ruleset.core", "1.0.0", "0.1.0"), NewCommandId(), Corr);
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

        private ActiveEffectRecord NewRecord(UtcInstant appliedAt, UtcInstant? expiresAt = null)
        {
            ContentDefinitionRef effectRef = NewEffectRef();
            var snapshot = new EffectMechanicsSnapshot(effectRef, 1, ContentDefinitionType.Effect, "{\"duration\":\"for_duration\"}");
            var target = ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow()));
            var effect = new ActiveEffect(ActiveEffectId.NewId(Clock.GetUtcNow()), effectRef, snapshot, ActiveEffectSourceRef.ForGMDirect(), target, ActiveEffectStatus.Active, 1, NewUserId(), appliedAt, expiresAt, 1);
            return new ActiveEffectRecord(_campaign.CampaignId, effect);
        }

        private ActiveEffectRecord PersistAsExisting(ActiveEffectRecord record)
        {
            Result<ActiveEffectRecord> created = _activeEffects.CreateActiveEffect(_campaign, record, NewCommandId(), Corr);
            Assert.That(created.IsSuccess, Is.True, created.IsFailure ? created.Error.Code.ToString() : string.Empty);
            return created.Value;
        }

        // ---- Permanent / UntilRemoved: no automatic expiry ----

        [Test] // TC-ACTIVEEFFECT-036
        public void CheckNoAutomaticExpiry_ReturnsNotExpired_ForPermanent()
        {
            Assert.That(ActiveEffectExpiryRules.CheckNoAutomaticExpiry(EffectDurationType.Permanent), Is.EqualTo(ActiveEffectExpiryDecision.NotExpired));
        }

        [Test] // TC-ACTIVEEFFECT-037
        public void CheckNoAutomaticExpiry_ReturnsNotExpired_ForUntilRemoved()
        {
            Assert.That(ActiveEffectExpiryRules.CheckNoAutomaticExpiry(EffectDurationType.UntilRemoved), Is.EqualTo(ActiveEffectExpiryDecision.NotExpired));
        }

        [Test] // TC-ACTIVEEFFECT-038
        public void CheckNoAutomaticExpiry_RejectsAnyOtherDurationType()
        {
            Action act = () => ActiveEffectExpiryRules.CheckNoAutomaticExpiry(EffectDurationType.ForDuration);
            Assert.Throws<ArgumentException>(act, "Permanent/UntilRemoved are the only two duration types with no automatic expiry check at all");
        }

        // ---- ForDuration: real wall-clock expiry ----

        [Test] // TC-ACTIVEEFFECT-039
        public void CheckForDurationExpiry_ReturnsNotExpired_BeforeExpiresAt()
        {
            UtcInstant now = UtcInstant.Parse("2026-09-12T00:00:00.0000000Z");
            UtcInstant expiresAt = UtcInstant.Parse("2026-09-12T01:00:00.0000000Z");

            Assert.That(ActiveEffectExpiryRules.CheckForDurationExpiry(expiresAt, now), Is.EqualTo(ActiveEffectExpiryDecision.NotExpired));
        }

        [Test] // TC-ACTIVEEFFECT-040
        public void CheckForDurationExpiry_ReturnsExpired_AtOrAfterExpiresAt()
        {
            UtcInstant expiresAt = UtcInstant.Parse("2026-09-12T01:00:00.0000000Z");
            UtcInstant exactlyAt = UtcInstant.Parse("2026-09-12T01:00:00.0000000Z");
            UtcInstant after = UtcInstant.Parse("2026-09-12T02:00:00.0000000Z");

            Assert.That(ActiveEffectExpiryRules.CheckForDurationExpiry(expiresAt, exactlyAt), Is.EqualTo(ActiveEffectExpiryDecision.Expired));
            Assert.That(ActiveEffectExpiryRules.CheckForDurationExpiry(expiresAt, after), Is.EqualTo(ActiveEffectExpiryDecision.Expired));
        }

        [Test] // TC-ACTIVEEFFECT-041
        public void CheckForDurationExpiry_RejectsANullExpiresAt()
        {
            Action act = () => ActiveEffectExpiryRules.CheckForDurationExpiry(null, Clock.GetUtcNow());
            Assert.Throws<ArgumentException>(act, "a ForDuration effect must always have a computed ExpiresAt");
        }

        // ---- WhileCondition: Odyssey.Rules evaluator + fail-closed ----

        [Test] // TC-ACTIVEEFFECT-042
        public void EffectConditionRules_Evaluate_IsAlwaysInconclusive_TodaysHonestNoOp()
        {
            var snapshot = new EffectMechanicsSnapshot(NewEffectRef(), 1, ContentDefinitionType.Effect, "{\"anything\":true}");

            Assert.That(EffectConditionRules.Evaluate(snapshot), Is.EqualTo(EffectConditionEvaluationResult.Inconclusive),
                "no real Ruleset condition language exists yet -- this is a documented, honest no-op, not a defect");
        }

        [Test] // TC-ACTIVEEFFECT-043
        public void CheckWhileConditionExpiry_Inconclusive_FailsClosed_RemainsNotExpired()
        {
            Assert.That(ActiveEffectExpiryRules.CheckWhileConditionExpiry(EffectConditionEvaluationResult.Inconclusive), Is.EqualTo(ActiveEffectExpiryDecision.NotExpired),
                "ADR-028 section 11: an inconclusive check is never treated as expired by default");
        }

        [Test] // TC-ACTIVEEFFECT-044
        public void CheckWhileConditionExpiry_ConditionHolds_RemainsNotExpired()
        {
            Assert.That(ActiveEffectExpiryRules.CheckWhileConditionExpiry(EffectConditionEvaluationResult.ConditionHolds), Is.EqualTo(ActiveEffectExpiryDecision.NotExpired));
        }

        [Test] // TC-ACTIVEEFFECT-045
        public void CheckWhileConditionExpiry_ConditionFailed_Expires()
        {
            Assert.That(ActiveEffectExpiryRules.CheckWhileConditionExpiry(EffectConditionEvaluationResult.ConditionFailed), Is.EqualTo(ActiveEffectExpiryDecision.Expired));
        }

        // ---- UntilSceneChange / UntilSessionEnd / WhileSourceExists: external-trigger contract ----

        [TestCase(ActiveEffectExternalExpiryTriggerKind.UntilSceneChange)] // TC-ACTIVEEFFECT-046
        [TestCase(ActiveEffectExternalExpiryTriggerKind.UntilSessionEnd)]
        [TestCase(ActiveEffectExternalExpiryTriggerKind.WhileSourceExists)]
        public void OnExternalTriggerFired_AlwaysExpires_ForEveryTriggerKind(ActiveEffectExternalExpiryTriggerKind triggerKind)
        {
            Assert.That(ActiveEffectExpiryRules.OnExternalTriggerFired(triggerKind), Is.EqualTo(ActiveEffectExpiryDecision.Expired),
                "once one of these three external triggers fires, there is no further condition to evaluate -- unlike WhileCondition");
        }

        // ---- SqliteActiveEffectRepository.ExpireActiveEffect: real Status -> Expired transition ----

        [Test] // TC-ACTIVEEFFECT-047
        public void ExpireActiveEffect_TransitionsStatusToExpired_AndIncrementsRevision()
        {
            ActiveEffectRecord created = PersistAsExisting(NewRecord(Clock.GetUtcNow(), UtcInstant.Parse("2026-09-12T00:00:00.0000000Z")));

            Result<ActiveEffectRecord> expired = _activeEffects.ExpireActiveEffect(_campaign, _campaign.CampaignId, created.Effect.ActiveEffectId, created.Effect.Revision, NewCommandId(), Corr);

            Assert.That(expired.IsSuccess, Is.True, expired.IsFailure ? expired.Error.Code.ToString() : string.Empty);
            Assert.That(expired.Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Expired));
            Assert.That(expired.Value.Effect.Revision, Is.EqualTo(created.Effect.Revision + 1));

            Result<ActiveEffectRecord> fetched = _activeEffects.GetActiveEffect(_campaign, created.Effect.ActiveEffectId, Corr);
            Assert.That(fetched.IsSuccess, Is.True);
            Assert.That(fetched.Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Expired), "the row survives as Status=Expired history, never a physical delete");
        }

        [Test] // TC-ACTIVEEFFECT-048
        public void ExpireActiveEffect_WithAStaleExpectedRevision_IsRejected_NoMutation()
        {
            ActiveEffectRecord created = PersistAsExisting(NewRecord(Clock.GetUtcNow()));
            long staleRevision = created.Effect.Revision + 5;

            Result<ActiveEffectRecord> expired = _activeEffects.ExpireActiveEffect(_campaign, _campaign.CampaignId, created.Effect.ActiveEffectId, staleRevision, NewCommandId(), Corr);

            Assert.That(expired.IsFailure, Is.True);
            Assert.That(expired.Error.Code, Is.EqualTo(ErrorCodes.PersistenceActiveEffectRevisionConflict));

            Result<ActiveEffectRecord> fetched = _activeEffects.GetActiveEffect(_campaign, created.Effect.ActiveEffectId, Corr);
            Assert.That(fetched.IsSuccess, Is.True);
            Assert.That(fetched.Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Active), "a rejected CAS transition must not mutate the row at all");
            Assert.That(fetched.Value.Effect.Revision, Is.EqualTo(created.Effect.Revision));
        }

        [Test] // TC-ACTIVEEFFECT-049
        public void ExpireActiveEffect_ReplayWithSameCommandId_ReturnsTheSameResultWithoutDoubleTransitioning()
        {
            ActiveEffectRecord created = PersistAsExisting(NewRecord(Clock.GetUtcNow()));
            CommandId commandId = NewCommandId();

            Result<ActiveEffectRecord> first = _activeEffects.ExpireActiveEffect(_campaign, _campaign.CampaignId, created.Effect.ActiveEffectId, created.Effect.Revision, commandId, Corr);
            Result<ActiveEffectRecord> replay = _activeEffects.ExpireActiveEffect(_campaign, _campaign.CampaignId, created.Effect.ActiveEffectId, created.Effect.Revision, commandId, Corr);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.Effect.Revision, Is.EqualTo(first.Value.Effect.Revision), "a replay must not increment Revision a second time");
        }

        [Test] // TC-ACTIVEEFFECT-050
        public void ExpireActiveEffect_WithMismatchedCampaignId_IsRejected()
        {
            ActiveEffectRecord created = PersistAsExisting(NewRecord(Clock.GetUtcNow()));
            CampaignId otherCampaignId = CampaignId.NewId(Clock.GetUtcNow());

            Result<ActiveEffectRecord> expired = _activeEffects.ExpireActiveEffect(_campaign, otherCampaignId, created.Effect.ActiveEffectId, created.Effect.Revision, NewCommandId(), Corr);

            Assert.That(expired.IsFailure, Is.True);
            Assert.That(expired.Error.Code, Is.EqualTo(ErrorCodes.PersistenceActiveEffectCampaignMismatch));
        }
    }
}
