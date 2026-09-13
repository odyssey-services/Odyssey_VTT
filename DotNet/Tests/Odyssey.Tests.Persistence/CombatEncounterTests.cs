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
        public void Create_rejects_empty_duplicate_and_inactive_participants()
        {
            CharacterId active = Active("active");
            Assert.That(Create().IsFailure, Is.True);
            Assert.That(Create(active, active).IsFailure, Is.True);
            CharacterId draft = _characters.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, "draft"), Command(), Corr).Value.CharacterId;
            Assert.That(Create(draft).IsFailure, Is.True);
        }

        [Test]
        public void Stale_and_changed_replay_do_not_mutate()
        {
            CharacterId active = Active("state"); CombatEncounterRecord encounter = Create(active).Value; int events = Events(encounter.EncounterId).Count;
            Assert.That(CombatEncounterService.Advance(_encounters, _campaign, new AdvanceCombatEncounterRequest(encounter.EncounterId, 99, User(), true, Command()), Corr).IsFailure, Is.True);
            Assert.That(Events(encounter.EncounterId).Count, Is.EqualTo(events));
            CommandId command = Command(); Assert.That(CombatEncounterService.Advance(_encounters, _campaign, new AdvanceCombatEncounterRequest(encounter.EncounterId, encounter.Revision, User(), true, command), Corr).IsSuccess, Is.True);
            Result<CombatEncounterRecord> changed = CombatEncounterService.Advance(_encounters, _campaign, new AdvanceCombatEncounterRequest(encounter.EncounterId, encounter.Revision + 1, User(), true, command), Corr);
            Assert.That(changed.IsFailure, Is.True); Assert.That(changed.Error.Code, Is.EqualTo(ErrorCodes.CommandIdentityMismatch));
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

        [Test]
        public void Foreign_command_collisions_preserve_correlation_and_do_not_mutate()
        {
            CharacterId active = Active("collision"); CommandId deleteCommand = Command(); CorrelationId deleteCorrelation = CorrelationId.Parse("corr_11111111111111111111111111111111");
            Seed("CREATE TABLE IF NOT EXISTS ContentDefinitionDeleteLedger (CommandId TEXT PRIMARY KEY, ContentDefinitionId TEXT NOT NULL, DeletedAt TEXT NOT NULL); INSERT INTO ContentDefinitionDeleteLedger VALUES ($id, 'def_0123456789abcdef0123456789abcdef', '2026-01-01T00:00:00.0000000Z');", deleteCommand);
            Result<CombatEncounterRecord> rejected = CombatEncounterService.Create(_encounters, _campaign, new CreateCombatEncounterRequest(new[] { active }, User(), true, deleteCommand), deleteCorrelation);
            Assert.That(rejected.IsFailure, Is.True); Assert.That(rejected.Error.Code, Is.EqualTo(ErrorCodes.CommandIdentityMismatch)); Assert.That(rejected.Error.CorrelationId, Is.EqualTo(deleteCorrelation)); Assert.That(Count("CombatEncounter"), Is.Zero); Assert.That(Count("CombatEncounterCommandLedger"), Is.Zero);
            CommandId appliedCommand = Command(); Seed("INSERT INTO AppliedCommands (CommandId, Status, ResultEventSequenceFrom, ResultEventSequenceTo, ResultSummary, FailureCode, CreatedAt, CompletedAt) VALUES ($id, 'Completed', 1, 1, '', NULL, '2026-01-01T00:00:00.0000000Z', '2026-01-01T00:00:00.0000000Z');", appliedCommand);
            Assert.That(CombatEncounterService.Create(_encounters, _campaign, new CreateCombatEncounterRequest(new[] { active }, User(), true, appliedCommand), Corr).Error.Code, Is.EqualTo(ErrorCodes.CommandIdentityMismatch));
        }

        [Test]
        public void Schema_uses_primary_key_and_expected_indexes()
        {
            CharacterId active = Active("schema"); Create(active);
            using var c = new SqliteConnection("Data Source=" + Path.Combine(_root, "campaign.db")); c.Open(); using var q = c.CreateCommand(); q.CommandText = "PRAGMA table_info(CombatEncounterCommandLedger);"; using var r = q.ExecuteReader(); bool primaryKey = false; while (r.Read()) if (r.GetString(1) == "CommandId") primaryKey = r.GetInt64(5) == 1; Assert.That(primaryKey, Is.True);
            Assert.That(IndexExists(c, "IX_CombatEncounter_CampaignId"), Is.True); Assert.That(IndexExists(c, "IX_CombatEncounterLifecycleEvent_EncounterId"), Is.True);
        }

        [Test]
        public void Cross_campaign_and_injected_sql_failure_are_rejected_without_partial_rows()
        {
            CharacterId active = Active("rollback"); Create(active);
            Seed("CREATE TRIGGER FailCombatEvent BEFORE INSERT ON CombatEncounterLifecycleEvent BEGIN SELECT RAISE(ABORT, 'forced'); END;", Command());
            Assert.That(Create(active).IsFailure, Is.True); Assert.That(Count("CombatEncounter"), Is.EqualTo(1)); Assert.That(Count("CombatEncounterParticipant"), Is.EqualTo(1)); Assert.That(Count("CombatEncounterLifecycleEvent"), Is.EqualTo(3)); Assert.That(Count("CombatEncounterCommandLedger"), Is.EqualTo(1));
            using var c = new SqliteConnection("Data Source=" + Path.Combine(_root, "campaign.db")); c.Open(); using var drop = c.CreateCommand(); drop.CommandText = "DROP TRIGGER FailCombatEvent;"; drop.ExecuteNonQuery();
            CombatEncounterRecord encounter = Create(active).Value;
            string otherRoot = Path.Combine(Path.GetTempPath(), "ody-s05-602-other-" + Guid.NewGuid().ToString("N"));
            Result<CampaignHandle> other = new SqliteCampaignRepository(new SystemWallClock()).Create(new CreateCampaignRequest(otherRoot, "Other", "ruleset.core", "1.0.0", "0.1.0"), Command(), Corr);
            Assert.That(_encounters.Get(other.Value, encounter.EncounterId, Corr).IsFailure, Is.True);
        }

        private Result<CombatEncounterRecord> Create(params CharacterId[] ids) => CombatEncounterService.Create(_encounters, _campaign, new CreateCombatEncounterRequest(ids, User(), true, Command()), Corr);
        private Result<CombatEncounterRecord> Advance(CombatEncounterRecord record) => CombatEncounterService.Advance(_encounters, _campaign, new AdvanceCombatEncounterRequest(record.EncounterId, record.Revision, User(), true, Command()), Corr);
        private CharacterId Active(string name) { CharacterId id = _characters.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, name), Command(), Corr).Value.CharacterId; CharacterRecord current = _characters.GetCharacter(_campaign, id, Corr).Value; return _characters.ApproveCharacterDraft(_campaign, id, true, current.Revisions.LifecycleRevision, Command(), Corr).Value.CharacterId; }
        private void Archive(CharacterId id) { CharacterRecord current = _characters.GetCharacter(_campaign, id, Corr).Value; Assert.That(_characters.ArchiveCharacter(_campaign, id, User(), true, current.Revisions.LifecycleRevision, Command(), Corr).IsSuccess, Is.True); }
        private List<string> Events(CombatEncounterId id) { var values = new List<string>(); using var c = new SqliteConnection("Data Source=" + Path.Combine(_root, "campaign.db")); c.Open(); using var q = c.CreateCommand(); q.CommandText = "SELECT EventKind FROM CombatEncounterLifecycleEvent WHERE EncounterId=$id ORDER BY EventId;"; q.Parameters.AddWithValue("$id", id.ToString()); using var r = q.ExecuteReader(); while (r.Read()) values.Add(r.GetString(0)); return values; }
        private void Seed(string sql, CommandId command) { using var c = new SqliteConnection("Data Source=" + Path.Combine(_root, "campaign.db")); c.Open(); using var q = c.CreateCommand(); q.CommandText = sql; q.Parameters.AddWithValue("$id", command.ToString()); q.ExecuteNonQuery(); }
        private int Count(string table) { using var c = new SqliteConnection("Data Source=" + Path.Combine(_root, "campaign.db")); c.Open(); using var q = c.CreateCommand(); q.CommandText = "SELECT COUNT(*) FROM " + table; return Convert.ToInt32(q.ExecuteScalar()); }
        private static bool IndexExists(SqliteConnection c, string name) { using var q = c.CreateCommand(); q.CommandText = "SELECT 1 FROM sqlite_master WHERE type='index' AND name=$name;"; q.Parameters.AddWithValue("$name", name); return q.ExecuteScalar() != null; }
        private static CommandId Command() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N")); private static UserId User() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
        private sealed class CountingRepository : ICombatEncounterRepository { public int Calls { get; private set; } public Result<CombatEncounterRecord> Create(CampaignHandle c, CreateCombatEncounterCommand x, CorrelationId id) { Calls++; throw new InvalidOperationException(); } public Result<CombatEncounterRecord> Advance(CampaignHandle c, AdvanceCombatEncounterCommand x, CorrelationId id) { Calls++; throw new InvalidOperationException(); } public Result<CombatEncounterRecord> Get(CampaignHandle c, CombatEncounterId x, CorrelationId id) { Calls++; throw new InvalidOperationException(); } }
    }
}
