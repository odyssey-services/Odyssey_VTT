using System;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Combat;
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

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S05-605: real tests for `ADR-029` section 7's six turn/round-based
    /// `EffectDurationType` boundary-check functions (`ActiveEffectExpiryRules`),
    /// the `CombatDurationBinding` snapshot field on `ActiveEffect`, the
    /// `CombatEffectExpiryService` orchestrator, and its real reuse of the
    /// existing `IActiveEffectRepository.ExpireActiveEffect`. No `ActiveEffect`
    /// creation, `EffectApplicationDecision`, atomic-apply, or delta-commit
    /// logic is introduced or exercised here -- those remain `ODY-S05-604`/
    /// `606`/`609`'s own territory.
    /// </summary>
    public sealed class CombatEffectExpiryTests
    {
        private static readonly CorrelationId Corr = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private string _root = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCharacterRepository _characters = null!;
        private SqliteCombatEncounterRepository _encounters = null!;
        private SqliteActiveEffectRepository _activeEffects = null!;
        private SqliteCombatEncounterLifecycleReader _reader = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "ody-s05-605-" + Guid.NewGuid().ToString("N"));
            Result<CampaignHandle> campaign = new SqliteCampaignRepository(Clock).Create(new CreateCampaignRequest(_root, "combat-effect-expiry", "ruleset.core", "1.0.0", "0.1.0"), Command(), Corr);
            Assert.That(campaign.IsSuccess, Is.True);
            _campaign = campaign.Value;
            _characters = new SqliteCharacterRepository(Clock);
            _encounters = new SqliteCombatEncounterRepository(Clock);
            _activeEffects = new SqliteActiveEffectRepository(Clock);
            _reader = new SqliteCombatEncounterLifecycleReader(_encounters);
        }

        // ---- Pure boundary-check functions (no I/O) ----

        [Test] // TC-ATTACK-035
        public void CheckForRoundsExpiry_IsNotExpiredBeforeTheBoundaryRound_AndExpiredExactlyAtIt()
        {
            // ADR-029 section 7: committed in round R with N=1 expires before actions in round R+2 -- boundary round = R+N+1.
            Assert.That(ActiveEffectExpiryRules.CheckForRoundsExpiry(appliedRoundOrdinal: 1, requiredRounds: 1, currentRoundOrdinal: 1), Is.EqualTo(ActiveEffectExpiryDecision.NotExpired));
            Assert.That(ActiveEffectExpiryRules.CheckForRoundsExpiry(appliedRoundOrdinal: 1, requiredRounds: 1, currentRoundOrdinal: 2), Is.EqualTo(ActiveEffectExpiryDecision.NotExpired));
            Assert.That(ActiveEffectExpiryRules.CheckForRoundsExpiry(appliedRoundOrdinal: 1, requiredRounds: 1, currentRoundOrdinal: 3), Is.EqualTo(ActiveEffectExpiryDecision.Expired));
            Assert.That(ActiveEffectExpiryRules.CheckForRoundsExpiry(appliedRoundOrdinal: 1, requiredRounds: 1, currentRoundOrdinal: 4), Is.EqualTo(ActiveEffectExpiryDecision.Expired));
            // Fail-closed: a current round that precedes the applied round (stale/bad data) never expires.
            Assert.That(ActiveEffectExpiryRules.CheckForRoundsExpiry(appliedRoundOrdinal: 5, requiredRounds: 1, currentRoundOrdinal: 1), Is.EqualTo(ActiveEffectExpiryDecision.NotExpired));
        }

        [Test] // TC-ATTACK-036
        public void CheckForTurnsExpiry_CountsOnlyTheBoundTargetsOwnCompletedTurns_AndFailsClosedOnANegativeCount()
        {
            Assert.That(ActiveEffectExpiryRules.CheckForTurnsExpiry(requiredTurns: 2, completedTargetTurnsSinceApplication: 0), Is.EqualTo(ActiveEffectExpiryDecision.NotExpired));
            Assert.That(ActiveEffectExpiryRules.CheckForTurnsExpiry(requiredTurns: 2, completedTargetTurnsSinceApplication: 1), Is.EqualTo(ActiveEffectExpiryDecision.NotExpired));
            Assert.That(ActiveEffectExpiryRules.CheckForTurnsExpiry(requiredTurns: 2, completedTargetTurnsSinceApplication: 2), Is.EqualTo(ActiveEffectExpiryDecision.Expired));
            Assert.That(ActiveEffectExpiryRules.CheckForTurnsExpiry(requiredTurns: 2, completedTargetTurnsSinceApplication: 3), Is.EqualTo(ActiveEffectExpiryDecision.Expired));
            // Fail-closed: a negative count is the caller's own "could not conclusively count" signal.
            Assert.That(ActiveEffectExpiryRules.CheckForTurnsExpiry(requiredTurns: 2, completedTargetTurnsSinceApplication: -1), Is.EqualTo(ActiveEffectExpiryDecision.NotExpired));
        }

        [Test] // TC-ATTACK-037
        public void CheckUntilSourceTurnStartAndEndExpiry_MapTheAlreadyDerivedBooleanDirectly()
        {
            Assert.That(ActiveEffectExpiryRules.CheckUntilSourceTurnStartExpiry(false), Is.EqualTo(ActiveEffectExpiryDecision.NotExpired));
            Assert.That(ActiveEffectExpiryRules.CheckUntilSourceTurnStartExpiry(true), Is.EqualTo(ActiveEffectExpiryDecision.Expired));
            Assert.That(ActiveEffectExpiryRules.CheckUntilSourceTurnEndExpiry(false), Is.EqualTo(ActiveEffectExpiryDecision.NotExpired));
            Assert.That(ActiveEffectExpiryRules.CheckUntilSourceTurnEndExpiry(true), Is.EqualTo(ActiveEffectExpiryDecision.Expired));
        }

        [Test] // TC-ATTACK-038
        public void CheckUntilTargetTurnStartAndEndExpiry_MapTheAlreadyDerivedBooleanDirectly()
        {
            Assert.That(ActiveEffectExpiryRules.CheckUntilTargetTurnStartExpiry(false), Is.EqualTo(ActiveEffectExpiryDecision.NotExpired));
            Assert.That(ActiveEffectExpiryRules.CheckUntilTargetTurnStartExpiry(true), Is.EqualTo(ActiveEffectExpiryDecision.Expired));
            Assert.That(ActiveEffectExpiryRules.CheckUntilTargetTurnEndExpiry(false), Is.EqualTo(ActiveEffectExpiryDecision.NotExpired));
            Assert.That(ActiveEffectExpiryRules.CheckUntilTargetTurnEndExpiry(true), Is.EqualTo(ActiveEffectExpiryDecision.Expired));
        }

        // ---- Orchestrator (real encounter, real ActiveEffect, real ExpireActiveEffect) ----

        [Test] // TC-ATTACK-039
        public void EvaluateAndExpireIfDue_ForRounds_ExpiresTheEffectViaExpireActiveEffect_ExactlyAtTheBoundaryRound()
        {
            CharacterId a = Active("a"), b = Active("b");
            CombatEncounterRecord encounter = CreateEncounter(a, b);
            ActiveEffectRecord effect = PersistCombatEffect(encounter.EncounterId, source: a, target: b, appliedRoundOrdinal: encounter.RoundOrdinal, appliedLifecycleEventId: 0, requiredCount: 1);

            encounter = Advance(encounter); // round 1 -> A's TurnEnded, B's TurnStarted (round still 1)
            encounter = Advance(encounter); // round 2 -> B's TurnEnded, A's TurnStarted (round 2)
            Result<ActiveEffectRecord> stillActive = CombatEffectExpiryService.EvaluateAndExpireIfDue(_activeEffects, _encounters, _reader, _campaign, effect, EffectDurationType.ForRounds, Command(), Corr);
            Assert.That(stillActive.IsSuccess, Is.True);
            Assert.That(_activeEffects.GetActiveEffect(_campaign, effect.Effect.ActiveEffectId, Corr).Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Active));

            encounter = Advance(encounter); // A's TurnEnded, B's TurnStarted (round 2)
            encounter = Advance(encounter); // round 3 -> B's TurnEnded, A's TurnStarted (round 3, the boundary)
            Result<ActiveEffectRecord> expired = CombatEffectExpiryService.EvaluateAndExpireIfDue(_activeEffects, _encounters, _reader, _campaign, effect, EffectDurationType.ForRounds, Command(), Corr);
            Assert.That(expired.IsSuccess, Is.True);
            Assert.That(_activeEffects.GetActiveEffect(_campaign, effect.Effect.ActiveEffectId, Corr).Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Expired));
        }

        [Test] // TC-ATTACK-040
        public void EvaluateAndExpireIfDue_ForTurns_CountsOnlyTheTargetsOwnCompletedTurns()
        {
            CharacterId a = Active("a"), b = Active("b");
            CombatEncounterRecord encounter = CreateEncounter(a, b);
            long snapshotEventId = MaxLifecycleEventId(encounter.EncounterId);
            ActiveEffectRecord effect = PersistCombatEffect(encounter.EncounterId, source: a, target: b, appliedRoundOrdinal: encounter.RoundOrdinal, appliedLifecycleEventId: snapshotEventId, requiredCount: 2);

            encounter = Advance(encounter); // A TurnEnded, B TurnStarted
            encounter = Advance(encounter); // B TurnEnded (1st for B), A TurnStarted
            Assert.That(CombatEffectExpiryService.EvaluateAndExpireIfDue(_activeEffects, _encounters, _reader, _campaign, effect, EffectDurationType.ForTurns, Command(), Corr).IsSuccess, Is.True);
            Assert.That(_activeEffects.GetActiveEffect(_campaign, effect.Effect.ActiveEffectId, Corr).Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Active));

            encounter = Advance(encounter); // A TurnEnded, B TurnStarted
            encounter = Advance(encounter); // B TurnEnded (2nd for B) -- the ForTurns boundary
            Assert.That(CombatEffectExpiryService.EvaluateAndExpireIfDue(_activeEffects, _encounters, _reader, _campaign, effect, EffectDurationType.ForTurns, Command(), Corr).IsSuccess, Is.True);
            Assert.That(_activeEffects.GetActiveEffect(_campaign, effect.Effect.ActiveEffectId, Corr).Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Expired));
        }

        [Test] // TC-ATTACK-041
        public void EvaluateAndExpireIfDue_UntilSourceTurnStartAndEnd_FireExactlyAtEachOwnBoundary()
        {
            CharacterId a = Active("a"), b = Active("b");
            CombatEncounterRecord encounter = CreateEncounter(a, b);
            long snapshotEventId = MaxLifecycleEventId(encounter.EncounterId);
            ActiveEffectRecord startEffect = PersistCombatEffect(encounter.EncounterId, source: b, target: null, appliedRoundOrdinal: encounter.RoundOrdinal, appliedLifecycleEventId: snapshotEventId, requiredCount: 0);
            ActiveEffectRecord endEffect = PersistCombatEffect(encounter.EncounterId, source: b, target: null, appliedRoundOrdinal: encounter.RoundOrdinal, appliedLifecycleEventId: snapshotEventId, requiredCount: 0);

            Assert.That(CombatEffectExpiryService.EvaluateAndExpireIfDue(_activeEffects, _encounters, _reader, _campaign, startEffect, EffectDurationType.UntilSourceTurnStart, Command(), Corr).IsSuccess, Is.True);
            Assert.That(_activeEffects.GetActiveEffect(_campaign, startEffect.Effect.ActiveEffectId, Corr).Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Active), "B's TurnStarted has not fired yet.");
            Assert.That(CombatEffectExpiryService.EvaluateAndExpireIfDue(_activeEffects, _encounters, _reader, _campaign, endEffect, EffectDurationType.UntilSourceTurnEnd, Command(), Corr).IsSuccess, Is.True);
            Assert.That(_activeEffects.GetActiveEffect(_campaign, endEffect.Effect.ActiveEffectId, Corr).Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Active));

            encounter = Advance(encounter); // A TurnEnded, B TurnStarted -- UntilSourceTurnStart's own boundary
            Assert.That(CombatEffectExpiryService.EvaluateAndExpireIfDue(_activeEffects, _encounters, _reader, _campaign, startEffect, EffectDurationType.UntilSourceTurnStart, Command(), Corr).IsSuccess, Is.True);
            Assert.That(_activeEffects.GetActiveEffect(_campaign, startEffect.Effect.ActiveEffectId, Corr).Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Expired));
            Assert.That(CombatEffectExpiryService.EvaluateAndExpireIfDue(_activeEffects, _encounters, _reader, _campaign, endEffect, EffectDurationType.UntilSourceTurnEnd, Command(), Corr).IsSuccess, Is.True);
            Assert.That(_activeEffects.GetActiveEffect(_campaign, endEffect.Effect.ActiveEffectId, Corr).Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Active), "B's TurnEnded has not fired yet.");

            encounter = Advance(encounter); // B TurnEnded -- UntilSourceTurnEnd's own boundary
            Assert.That(CombatEffectExpiryService.EvaluateAndExpireIfDue(_activeEffects, _encounters, _reader, _campaign, endEffect, EffectDurationType.UntilSourceTurnEnd, Command(), Corr).IsSuccess, Is.True);
            Assert.That(_activeEffects.GetActiveEffect(_campaign, endEffect.Effect.ActiveEffectId, Corr).Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Expired));
        }

        [Test] // TC-ATTACK-042
        public void EvaluateAndExpireIfDue_UntilTargetTurnStartAndEnd_FireExactlyAtEachOwnBoundary()
        {
            CharacterId a = Active("a"), b = Active("b");
            CombatEncounterRecord encounter = CreateEncounter(a, b);
            encounter = Advance(encounter); // now B is current, round1/turn2
            long snapshotEventId = MaxLifecycleEventId(encounter.EncounterId);
            ActiveEffectRecord startEffect = PersistCombatEffect(encounter.EncounterId, source: null, target: a, appliedRoundOrdinal: encounter.RoundOrdinal, appliedLifecycleEventId: snapshotEventId, requiredCount: 0);
            ActiveEffectRecord endEffect = PersistCombatEffect(encounter.EncounterId, source: null, target: a, appliedRoundOrdinal: encounter.RoundOrdinal, appliedLifecycleEventId: snapshotEventId, requiredCount: 0);

            Assert.That(CombatEffectExpiryService.EvaluateAndExpireIfDue(_activeEffects, _encounters, _reader, _campaign, startEffect, EffectDurationType.UntilTargetTurnStart, Command(), Corr).IsSuccess, Is.True);
            Assert.That(_activeEffects.GetActiveEffect(_campaign, startEffect.Effect.ActiveEffectId, Corr).Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Active));

            encounter = Advance(encounter); // B TurnEnded, A TurnStarted -- UntilTargetTurnStart's own boundary
            Assert.That(CombatEffectExpiryService.EvaluateAndExpireIfDue(_activeEffects, _encounters, _reader, _campaign, startEffect, EffectDurationType.UntilTargetTurnStart, Command(), Corr).IsSuccess, Is.True);
            Assert.That(_activeEffects.GetActiveEffect(_campaign, startEffect.Effect.ActiveEffectId, Corr).Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Expired));
            Assert.That(CombatEffectExpiryService.EvaluateAndExpireIfDue(_activeEffects, _encounters, _reader, _campaign, endEffect, EffectDurationType.UntilTargetTurnEnd, Command(), Corr).IsSuccess, Is.True);
            Assert.That(_activeEffects.GetActiveEffect(_campaign, endEffect.Effect.ActiveEffectId, Corr).Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Active), "A's TurnEnded has not fired yet.");

            encounter = Advance(encounter); // A TurnEnded -- UntilTargetTurnEnd's own boundary
            Assert.That(CombatEffectExpiryService.EvaluateAndExpireIfDue(_activeEffects, _encounters, _reader, _campaign, endEffect, EffectDurationType.UntilTargetTurnEnd, Command(), Corr).IsSuccess, Is.True);
            Assert.That(_activeEffects.GetActiveEffect(_campaign, endEffect.Effect.ActiveEffectId, Corr).Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Expired));
        }

        [Test] // TC-ATTACK-043
        public void EvaluateAndExpireIfDue_FailsClosed_WhenTheBoundCombatantIsNoLongerAParticipant()
        {
            CharacterId a = Active("a"), b = Active("b"), outsider = Active("outsider");
            CombatEncounterRecord encounter = CreateEncounter(a, b);
            // `outsider` was never a participant of this encounter -- a stand-in for
            // "the bound combatant is no longer in the encounter," which this
            // codebase has no participant-removal command to construct directly.
            ActiveEffectRecord effect = PersistCombatEffect(encounter.EncounterId, source: a, target: outsider, appliedRoundOrdinal: encounter.RoundOrdinal, appliedLifecycleEventId: 0, requiredCount: 1);

            Result<ActiveEffectRecord> result = CombatEffectExpiryService.EvaluateAndExpireIfDue(_activeEffects, _encounters, _reader, _campaign, effect, EffectDurationType.UntilTargetTurnStart, Command(), Corr);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(_activeEffects.GetActiveEffect(_campaign, effect.Effect.ActiveEffectId, Corr).Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Active), "An untracked combatant binding must never expire the effect.");
        }

        [Test] // TC-ATTACK-044
        public void EvaluateAndExpireIfDue_FailsClosed_WhenTheLifecycleReaderCannotConclusivelyAnswer()
        {
            CharacterId a = Active("a"), b = Active("b");
            CombatEncounterRecord encounter = CreateEncounter(a, b);
            ActiveEffectRecord effect = PersistCombatEffect(encounter.EncounterId, source: a, target: b, appliedRoundOrdinal: encounter.RoundOrdinal, appliedLifecycleEventId: 0, requiredCount: 1);
            var failingReader = new FailingLifecycleReader();

            Result<ActiveEffectRecord> result = CombatEffectExpiryService.EvaluateAndExpireIfDue(_activeEffects, _encounters, failingReader, _campaign, effect, EffectDurationType.UntilTargetTurnStart, Command(), Corr);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(_activeEffects.GetActiveEffect(_campaign, effect.Effect.ActiveEffectId, Corr).Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Active), "An inconclusive lifecycle read must never expire the effect.");
        }

        [Test] // TC-ATTACK-046
        public void CombatBinding_SurvivesARealPersistenceRoundTrip()
        {
            CharacterId a = Active("a"), b = Active("b");
            CombatEncounterRecord encounter = CreateEncounter(a, b);
            ActiveEffectRecord created = PersistCombatEffect(encounter.EncounterId, source: a, target: b, appliedRoundOrdinal: 3, appliedLifecycleEventId: 7, requiredCount: 2);

            Result<ActiveEffectRecord> reread = _activeEffects.GetActiveEffect(_campaign, created.Effect.ActiveEffectId, Corr);

            Assert.That(reread.IsSuccess, Is.True);
            Assert.That(reread.Value.Effect.CombatBinding.HasValue, Is.True, "CombatBinding must survive a fresh read, not only the in-memory object CreateActiveEffect returned.");
            CombatDurationBinding binding = reread.Value.Effect.CombatBinding!.Value;
            Assert.That(binding.EncounterId, Is.EqualTo(encounter.EncounterId));
            Assert.That(binding.SourceCombatantId, Is.EqualTo(a));
            Assert.That(binding.TargetCombatantId, Is.EqualTo(b));
            Assert.That(binding.AppliedRoundOrdinal, Is.EqualTo(3));
            Assert.That(binding.AppliedLifecycleEventId, Is.EqualTo(7));
            Assert.That(binding.RequiredCount, Is.EqualTo(2));
        }

        // ---- helpers ----

        private CombatEncounterRecord CreateEncounter(CharacterId a, CharacterId b)
            => CombatEncounterService.Create(_encounters, _campaign, new CreateCombatEncounterRequest(new[] { a, b }, User(), true, Command()), Corr).Value;

        private CombatEncounterRecord Advance(CombatEncounterRecord encounter)
            => CombatEncounterService.Advance(_encounters, _campaign, new AdvanceCombatEncounterRequest(encounter.EncounterId, encounter.Revision, User(), true, Command()), Corr).Value;

        private CharacterId Active(string name)
        {
            CharacterId id = _characters.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, name), Command(), Corr).Value.CharacterId;
            CharacterRecord current = _characters.GetCharacter(_campaign, id, Corr).Value;
            return _characters.ApproveCharacterDraft(_campaign, id, true, current.Revisions.LifecycleRevision, Command(), Corr).Value.CharacterId;
        }

        private ActiveEffectRecord PersistCombatEffect(CombatEncounterId encounterId, CharacterId? source, CharacterId? target, long appliedRoundOrdinal, long appliedLifecycleEventId, int requiredCount)
        {
            ContentDefinitionRef effectRef = new ContentDefinitionRef(ContentDefinitionId.NewId(Clock.GetUtcNow()), 1);
            var snapshot = new EffectMechanicsSnapshot(effectRef, 1, ContentDefinitionType.Effect, "{}");
            var binding = new CombatDurationBinding(encounterId, source, target, appliedRoundOrdinal, appliedLifecycleEventId, requiredCount);
            var effect = new ActiveEffect(ActiveEffectId.NewId(Clock.GetUtcNow()), effectRef, snapshot, ActiveEffectSourceRef.ForGMDirect(), ActiveEffectTargetRef.ForCharacter(target ?? source!.Value), ActiveEffectStatus.Active, 1, User(), Clock.GetUtcNow(), null, 1, binding);
            var record = new ActiveEffectRecord(_campaign.CampaignId, effect);
            Result<ActiveEffectRecord> created = _activeEffects.CreateActiveEffect(_campaign, record, Command(), Corr);
            Assert.That(created.IsSuccess, Is.True, created.IsFailure ? created.Error.Code.ToString() : string.Empty);
            return created.Value;
        }

        private long MaxLifecycleEventId(CombatEncounterId encounterId)
        {
            using Microsoft.Data.Sqlite.SqliteConnection connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + Path.Combine(_root, "campaign.db"));
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COALESCE(MAX(EventId), 0) FROM CombatEncounterLifecycleEvent WHERE EncounterId = $id;";
            command.Parameters.AddWithValue("$id", encounterId.ToString());
            return Convert.ToInt64(command.ExecuteScalar());
        }

        private static CommandId Command() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId User() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        private sealed class FailingLifecycleReader : ICombatEncounterLifecycleReader
        {
            public Result<int> CountLifecycleEvents(CampaignHandle campaign, Odyssey.Domain.Identity.CombatEncounterId encounterId, CharacterId characterId, string eventKind, long sinceEventId, CorrelationId correlationId)
                => Result<int>.Failure(PersistenceFailures.CombatEncounterLifecycleIoFailed(correlationId));
        }
    }
}
