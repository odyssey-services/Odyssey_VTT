using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Odyssey.Application.Combat;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Identity;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence
{
    public sealed class CombatEncounterTests
    {
        private static readonly CorrelationId Corr = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private string _root = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCharacterRepository _characters = null!;
        private SqliteCombatEncounterRepository _encounters = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "ody-s05-602-" + Guid.NewGuid().ToString("N"));
            var clock = new SystemWallClock();
            Result<CampaignHandle> campaign = new SqliteCampaignRepository(clock).Create(new CreateCampaignRequest(_root, "Combat", "ruleset.core", "1.0.0", "0.1.0"), Command(), Corr);
            Assert.That(campaign.IsSuccess, Is.True); _campaign = campaign.Value;
            _characters = new SqliteCharacterRepository(clock); _encounters = new SqliteCombatEncounterRepository(clock);
        }


        [Test]
        public void Create_captures_order_ruleset_and_lifecycle_sequence()
        {
            CharacterId first = Active("first"), second = Active("second");
            CombatEncounterRecord result = Create(first, second).Value;
            Assert.That(result.RulesetId, Is.EqualTo("ruleset.core")); Assert.That(result.RulesetVersion, Is.EqualTo("1.0.0"));
            Assert.That(result.Participants[0].CharacterId, Is.EqualTo(first));
            Assert.That(Events(result.EncounterId), Is.EqualTo(new[] { "Created", "RoundStarted", "TurnStarted" }));
        }

        [Test]
        public void Advance_wraps_skips_and_closes()
        {
            CharacterId first = Active("first"), second = Active("second"); CombatEncounterRecord created = Create(first, second).Value;
            CombatEncounterRecord next = Advance(created).Value; Assert.That(next.CurrentParticipantId, Is.EqualTo(second));
            CombatEncounterRecord wrapped = Advance(next).Value; Assert.That(wrapped.RoundOrdinal, Is.EqualTo(2));
            Archive(second); CombatEncounterRecord skipped = Advance(wrapped).Value; Assert.That(Events(created.EncounterId), Does.Contain("ParticipantSkipped"));
            Archive(first); CombatEncounterRecord closed = Advance(skipped).Value; Assert.That(closed.Status.ToString(), Is.EqualTo("ClosedNoEligibleParticipants")); Assert.That(Advance(closed).IsFailure, Is.True);
        }

        [Test]
        public void Service_denies_before_repository_and_replay_is_stable()
        {
            var fake = new CountingRepository(); CharacterId id = CharacterId.Parse("char_0123456789abcdef0123456789abcdef");
            Result<CombatEncounterRecord> denied = CombatEncounterService.Create(fake, _campaign, new CreateCombatEncounterRequest(new[] { id }, User(), false, Command()), Corr);
            Assert.That(denied.IsFailure, Is.True); Assert.That(denied.Error.CorrelationId, Is.EqualTo(Corr)); Assert.That(fake.Calls, Is.Zero);
            CharacterId active = Active("replay"); CommandId command = Command(); var request = new CreateCombatEncounterRequest(new[] { active }, User(), true, command);
            CombatEncounterRecord first = CombatEncounterService.Create(_encounters, _campaign, request, Corr).Value;
            CombatEncounterRecord replay = CombatEncounterService.Create(_encounters, _campaign, request, Corr).Value;
            Assert.That(replay.EncounterId, Is.EqualTo(first.EncounterId)); Assert.That(Events(first.EncounterId).Count, Is.EqualTo(3));
        }

        private Result<CombatEncounterRecord> Create(params CharacterId[] ids) => CombatEncounterService.Create(_encounters, _campaign, new CreateCombatEncounterRequest(ids, User(), true, Command()), Corr);
        private Result<CombatEncounterRecord> Advance(CombatEncounterRecord record) => CombatEncounterService.Advance(_encounters, _campaign, new AdvanceCombatEncounterRequest(record.EncounterId, record.Revision, User(), true, Command()), Corr);
        private CharacterId Active(string name) { CharacterId id = _characters.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, name), Command(), Corr).Value.CharacterId; CharacterRecord current = _characters.GetCharacter(_campaign, id, Corr).Value; return _characters.ApproveCharacterDraft(_campaign, id, true, current.Revisions.LifecycleRevision, Command(), Corr).Value.CharacterId; }
        private void Archive(CharacterId id) { CharacterRecord current = _characters.GetCharacter(_campaign, id, Corr).Value; Assert.That(_characters.ArchiveCharacter(_campaign, id, User(), true, current.Revisions.LifecycleRevision, Command(), Corr).IsSuccess, Is.True); }
        private List<string> Events(CombatEncounterId id) { var values = new List<string>(); using var c = new SqliteConnection("Data Source=" + Path.Combine(_root, "campaign.db")); c.Open(); using var q = c.CreateCommand(); q.CommandText = "SELECT EventKind FROM CombatEncounterLifecycleEvent WHERE EncounterId=$id ORDER BY EventId;"; q.Parameters.AddWithValue("$id", id.ToString()); using var r = q.ExecuteReader(); while (r.Read()) values.Add(r.GetString(0)); return values; }
        private static CommandId Command() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N")); private static UserId User() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
        private sealed class CountingRepository : ICombatEncounterRepository { public int Calls { get; private set; } public Result<CombatEncounterRecord> Create(CampaignHandle c, CreateCombatEncounterCommand x, CorrelationId id) { Calls++; throw new InvalidOperationException(); } public Result<CombatEncounterRecord> Advance(CampaignHandle c, AdvanceCombatEncounterCommand x, CorrelationId id) { Calls++; throw new InvalidOperationException(); } public Result<CombatEncounterRecord> Get(CampaignHandle c, CombatEncounterId x, CorrelationId id) { Calls++; throw new InvalidOperationException(); } }
    }
}
