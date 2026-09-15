using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Combat;
using Odyssey.Application.Commands;
using Odyssey.Application.Content;
using Odyssey.Application.Effects;
using Odyssey.Application.Persistence;
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
    /// ODY-S05-611: real tests for the automatic combat-duration expiry
    /// wiring inside `SqliteCombatEncounterRepository.Advance` -- closes
    /// `605`'s own disclosed "no automatic call site exists" gap, reconfirmed
    /// still open by `608`. Composes `605`'s own unmodified `CombatEffectExpiryService`/
    /// `ActiveEffectExpiryRules` and `502`'s own unmodified `ExpireActiveEffect`
    /// -- neither is changed here, only invoked automatically. No `606`/`607`/`609`/`610`
    /// territory is exercised; continues the existing `TC-ATTACK-*` series
    /// (this task's own contract decision log records why, not a new
    /// `TC-COMBAT-*` prefix).
    /// </summary>
    public sealed class CombatDurationExpiryWiringTests
    {
        private static readonly CorrelationId Corr = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private string _root = null!;
        private CampaignHandle _campaign = null!;
        private IWallClock _clock = null!;
        private SqliteCharacterRepository _characters = null!;
        private SqliteCombatEncounterRepository _encounters = null!;
        private SqliteActiveEffectRepository _activeEffects = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "ody-s05-611-" + Guid.NewGuid().ToString("N"));
            _clock = new SystemWallClock();
            Result<CampaignHandle> campaign = new SqliteCampaignRepository(_clock).Create(new CreateCampaignRequest(_root, "combat-duration-expiry-wiring", "ruleset.core", "1.0.0", "0.1.0"), Command(), Corr);
            Assert.That(campaign.IsSuccess, Is.True);
            _campaign = campaign.Value;
            _characters = new SqliteCharacterRepository(_clock);
            _activeEffects = new SqliteActiveEffectRepository(_clock);
            _encounters = new SqliteCombatEncounterRepository(_clock);
        }

        [Test] // TC-ATTACK-093
        public void A_real_Advance_call_expires_a_ForRounds_effect_whose_boundary_it_crosses()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ActiveEffectId effectId = CreateCombatEffect(target, encounter.EncounterId, actor, target, EffectDurationType.ForRounds, encounter.RoundOrdinal, 0, 1);

            // Boundary round = appliedRoundOrdinal(1) + requiredCount(1) + 1 = 3;
            // with 2 participants, round wraps (increments) only every 2nd
            // Advance call -- 4 calls reach round 3.
            encounter = Advance(Advance(Advance(Advance(encounter))));

            Assert.That(GetEffect(target, effectId).Effect.Status, Is.EqualTo(ActiveEffectStatus.Expired), "The real Advance call itself -- not a direct EvaluateAndExpireIfDue call -- expired this effect.");
        }

        [Test] // TC-ATTACK-094
        public void An_effect_whose_boundary_has_not_yet_arrived_stays_Active_after_Advance()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ActiveEffectId effectId = CreateCombatEffect(target, encounter.EncounterId, actor, target, EffectDurationType.ForRounds, encounter.RoundOrdinal, 0, 1);

            encounter = Advance(encounter); // round stays 1 -- well before the boundary round (3).

            Assert.That(GetEffect(target, effectId).Effect.Status, Is.EqualTo(ActiveEffectStatus.Active));
        }

        [Test] // TC-ATTACK-095
        public void A_non_combat_effect_and_an_already_terminal_effect_are_left_untouched_by_Advance()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ActiveEffectId nonCombatId = CreateNonCombatEffect(target);
            ActiveEffectId terminalId = CreateCombatEffect(target, encounter.EncounterId, actor, target, EffectDurationType.ForRounds, encounter.RoundOrdinal, 0, 1);
            Assert.That(_activeEffects.ExpireActiveEffect(_campaign, _campaign.CampaignId, terminalId, 1, Command(), Corr).IsSuccess, Is.True, "Force this one to Expired directly, ahead of time, so Advance's own wiring finds it already terminal.");

            encounter = Advance(Advance(Advance(Advance(encounter))));

            Assert.That(GetEffect(target, nonCombatId).Effect.Status, Is.EqualTo(ActiveEffectStatus.Active), "No CombatBinding -- Advance's wiring never even considers it.");
            Assert.That(GetEffect(target, terminalId).Effect.Status, Is.EqualTo(ActiveEffectStatus.Expired), "Already terminal before Advance ran -- EvaluateAndExpireIfDue's own no-op-if-not-Active guard leaves it exactly as it was, not re-processed or errored.");
        }

        [Test] // TC-ATTACK-096
        public void Advance_still_commits_and_a_sibling_candidate_still_expires_even_when_one_candidates_own_processing_fails()
        {
            CharacterId actor = Active("actor"), target = Active("target"), other = Active("other");
            CombatEncounterRecord encounter = CreateEncounter(actor, target, other);
            ActiveEffectId brokenId = CreateEffectWithRawPayload(target, encounter.EncounterId, actor, target, "not-a-valid-effect-definition-payload", encounter.RoundOrdinal, 0, 1);
            ActiveEffectId goodId = CreateCombatEffect(other, encounter.EncounterId, actor, other, EffectDurationType.ForRounds, encounter.RoundOrdinal, 0, 1);
            long revisionBefore = encounter.Revision;

            // Boundary round = 1 + 1 + 1 = 3; with 3 participants, round only
            // wraps once per full cycle through all 3 -- 6 Advance calls reach round 3.
            for (int i = 0; i < 6; i++)
            {
                encounter = Advance(encounter);
            }

            Assert.That(encounter.Revision, Is.GreaterThan(revisionBefore), "The round/turn advance itself committed six times regardless of the broken candidate's own decode failure.");
            Assert.That(GetEffect(target, brokenId).Effect.Status, Is.EqualTo(ActiveEffectStatus.Active), "A malformed candidate's own decode failure is skipped, never thrown -- left Active, not corrupted, and does not abort the rest of Advance's own wiring.");
            Assert.That(GetEffect(other, goodId).Effect.Status, Is.EqualTo(ActiveEffectStatus.Expired), "A sibling candidate in the SAME Advance calls still expires correctly despite the other one's own failure.");
        }

        [Test] // TC-ATTACK-097
        public void Multiple_due_candidates_across_different_participants_in_one_Advance_call_all_expire()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ActiveEffectId actorEffect = CreateCombatEffect(actor, encounter.EncounterId, actor, actor, EffectDurationType.ForRounds, encounter.RoundOrdinal, 0, 1);
            ActiveEffectId targetEffect = CreateCombatEffect(target, encounter.EncounterId, actor, target, EffectDurationType.ForRounds, encounter.RoundOrdinal, 0, 1);

            encounter = Advance(Advance(Advance(Advance(encounter))));

            Assert.That(GetEffect(actor, actorEffect).Effect.Status, Is.EqualTo(ActiveEffectStatus.Expired), "Not only the first candidate found.");
            Assert.That(GetEffect(target, targetEffect).Effect.Status, Is.EqualTo(ActiveEffectStatus.Expired), "The second candidate, on a different participant, also expired.");
        }

        [Test] // TC-ATTACK-098
        public void An_effect_on_a_character_outside_the_current_participant_roster_is_not_expired_by_Advance()
        {
            CharacterId actor = Active("actor"), target = Active("target"), bystander = Active("bystander");
            CombatEncounterRecord encounter = CreateEncounter(actor, target); // bystander never joins this encounter
            ActiveEffectId bystanderEffect = CreateCombatEffect(bystander, encounter.EncounterId, actor, bystander, EffectDurationType.ForRounds, encounter.RoundOrdinal, 0, 1);

            encounter = Advance(Advance(Advance(Advance(encounter))));

            Assert.That(GetEffect(bystander, bystanderEffect).Effect.Status, Is.EqualTo(ActiveEffectStatus.Active), "Documented residual gap (this task's own contract decision log): a target outside the current roster is never enumerated by this wiring path -- expected, not a bug.");
        }

        [Test] // TC-ATTACK-099
        public void A_non_MainGM_Advance_attempt_is_still_denied_after_the_wiring_was_added()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);

            Result<CombatEncounterRecord> result = CombatEncounterService.Advance(_encounters, _campaign, new AdvanceCombatEncounterRequest(encounter.EncounterId, encounter.Revision, User(), false, Command()), Corr);

            Assert.That(result.IsFailure, Is.True, "Regression check: the pre-existing MainGM gate is untouched by this task's own wiring.");
        }

        [Test] // TC-ATTACK-100
        public void Both_a_round_boundary_and_a_turn_boundary_duration_type_are_evaluated_by_the_wiring()
        {
            CharacterId actor = Active("actor"), target = Active("target");
            CombatEncounterRecord encounter = CreateEncounter(actor, target);
            ActiveEffectId turnBoundaryEffect = CreateCombatEffect(target, encounter.EncounterId, actor, target, EffectDurationType.UntilTargetTurnStart, encounter.RoundOrdinal, 0, 0);

            encounter = Advance(encounter); // a single Advance -- target becomes current, its own real TurnStarted event fires.

            Assert.That(GetEffect(target, turnBoundaryEffect).Effect.Status, Is.EqualTo(ActiveEffectStatus.Expired), "A single Advance call (a turn boundary, not a full round wrap) also triggers the expiry check -- the same wiring TC-ATTACK-093 already exercised for a round boundary.");
        }

        // ---- helpers (composition only, no new production type) ----

        private ActiveEffectId CreateCombatEffect(CharacterId target, CombatEncounterId encounterId, CharacterId? sourceCombatant, CharacterId? targetCombatant, EffectDurationType durationType, long appliedRoundOrdinal, long appliedLifecycleEventId, int requiredCount)
        {
            var definition = new EffectDefinition(new ContentTargetRule(ContentTargetSource.ManualSelection, 1, 1, false), durationType, DurationValueFor(durationType), EffectStackPolicy.IndependentInstances, null);
            string payload = TypedDefinitionCodec.EncodeEffect(definition);
            return CreateEffectWithRawPayload(target, encounterId, sourceCombatant, targetCombatant, payload, appliedRoundOrdinal, appliedLifecycleEventId, requiredCount);
        }

        private static long? DurationValueFor(EffectDurationType durationType)
            => durationType == EffectDurationType.ForRounds || durationType == EffectDurationType.ForTurns || durationType == EffectDurationType.ForDuration ? 1 : (long?)null;

        private ActiveEffectId CreateEffectWithRawPayload(CharacterId target, CombatEncounterId encounterId, CharacterId? sourceCombatant, CharacterId? targetCombatant, string payload, long appliedRoundOrdinal, long appliedLifecycleEventId, int requiredCount)
        {
            UtcInstant now = _clock.GetUtcNow();
            ContentDefinitionRef effectRef = new ContentDefinitionRef(ContentDefinitionId.NewId(now), 1);
            var snapshot = new EffectMechanicsSnapshot(effectRef, effectRef.Version, ContentDefinitionType.Effect, payload);
            var binding = new CombatDurationBinding(encounterId, sourceCombatant, targetCombatant, appliedRoundOrdinal, appliedLifecycleEventId, requiredCount);
            var effect = new ActiveEffect(ActiveEffectId.NewId(now), effectRef, snapshot, ActiveEffectSourceRef.ForAction(), ActiveEffectTargetRef.ForCharacter(target), ActiveEffectStatus.Active, 1, User(), now, null, 1, binding);
            var record = new ActiveEffectRecord(_campaign.CampaignId, effect);
            Result<ActiveEffectRecord> created = _activeEffects.CreateActiveEffect(_campaign, record, Command(), Corr);
            Assert.That(created.IsSuccess, Is.True);
            return effect.ActiveEffectId;
        }

        private ActiveEffectId CreateNonCombatEffect(CharacterId target)
        {
            UtcInstant now = _clock.GetUtcNow();
            ContentDefinitionRef effectRef = new ContentDefinitionRef(ContentDefinitionId.NewId(now), 1);
            var definition = new EffectDefinition(new ContentTargetRule(ContentTargetSource.ManualSelection, 1, 1, false), EffectDurationType.Permanent, null, EffectStackPolicy.IndependentInstances, null);
            string payload = TypedDefinitionCodec.EncodeEffect(definition);
            var snapshot = new EffectMechanicsSnapshot(effectRef, effectRef.Version, ContentDefinitionType.Effect, payload);
            var effect = new ActiveEffect(ActiveEffectId.NewId(now), effectRef, snapshot, ActiveEffectSourceRef.ForAction(), ActiveEffectTargetRef.ForCharacter(target), ActiveEffectStatus.Active, 1, User(), now, null, 1, null);
            var record = new ActiveEffectRecord(_campaign.CampaignId, effect);
            Result<ActiveEffectRecord> created = _activeEffects.CreateActiveEffect(_campaign, record, Command(), Corr);
            Assert.That(created.IsSuccess, Is.True);
            return effect.ActiveEffectId;
        }

        private ActiveEffectRecord GetEffect(CharacterId target, ActiveEffectId id)
        {
            IReadOnlyList<ActiveEffectRecord> effects = _activeEffects.ListActiveEffectsByTarget(_campaign, _campaign.CampaignId, ActiveEffectTargetRef.ForCharacter(target), Corr).Value;
            foreach (ActiveEffectRecord effect in effects)
            {
                if (effect.Effect.ActiveEffectId.Equals(id)) return effect;
            }

            Assert.Fail("ActiveEffect " + id + " not found for target " + target + ".");
            return null!;
        }

        private CombatEncounterRecord CreateEncounter(params CharacterId[] participants)
            => CombatEncounterService.Create(_encounters, _campaign, new CreateCombatEncounterRequest(participants, User(), true, Command()), Corr).Value;

        private CombatEncounterRecord Advance(CombatEncounterRecord encounter)
            => CombatEncounterService.Advance(_encounters, _campaign, new AdvanceCombatEncounterRequest(encounter.EncounterId, encounter.Revision, User(), true, Command()), Corr).Value;

        private CharacterId Active(string name)
        {
            CharacterId id = _characters.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, name), Command(), Corr).Value.CharacterId;
            CharacterRecord current = _characters.GetCharacter(_campaign, id, Corr).Value;
            return _characters.ApproveCharacterDraft(_campaign, id, true, current.Revisions.LifecycleRevision, Command(), Corr).Value.CharacterId;
        }

        private static CommandId Command() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId User() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
    }
}
