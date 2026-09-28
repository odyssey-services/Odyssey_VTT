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
using Odyssey.Domain.Combat;
using Odyssey.Domain.Identity;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence
{
    public sealed class CombatEncounterTests
    {
        private static readonly CorrelationId Corr = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private string _root = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;
        private SqliteCharacterRepository _characters = null!;
        private SqliteCombatEncounterRepository _encounters = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "ody-s05-602-" + Guid.NewGuid().ToString("N"));
            var clock = new SystemWallClock();
            _campaignRepository = new SqliteCampaignRepository(clock);
            Result<CampaignHandle> campaign = _campaignRepository.Create(new CreateCampaignRequest(_root, "Combat", "ruleset.core", "1.0.0", "0.1.0", global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost()), Command(), Corr);
            Assert.That(campaign.IsSuccess, Is.True); _campaign = campaign.Value;
            _characters = new SqliteCharacterRepository(clock, new SqliteCampaignRepository(clock)); _encounters = new SqliteCombatEncounterRepository(clock);
        }


        [Test]
        public void Domain_identity_and_participant_invariants_reject_invalid_values()
        {
            Assert.That(Throws<FormatException>(() => CombatEncounterId.Parse("enc_invalid")), Is.True);
            Assert.That(Throws<ArgumentException>(() => new CombatParticipant(default, 0)), Is.True);
            CharacterId character = CharacterId.Parse("char_0123456789abcdef0123456789abcdef");
            Assert.That(Throws<ArgumentOutOfRangeException>(() => new CombatParticipant(character, -1)), Is.True);
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
            Assert.That(CombatEncounterService.Advance(_encounters, _campaignRepository, _campaign, new AdvanceCombatEncounterRequest(encounter.EncounterId, 99, global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost(), Command()), Corr).IsFailure, Is.True);
            Assert.That(Events(encounter.EncounterId).Count, Is.EqualTo(events));
            CommandId command = Command(); Assert.That(CombatEncounterService.Advance(_encounters, _campaignRepository, _campaign, new AdvanceCombatEncounterRequest(encounter.EncounterId, encounter.Revision, global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost(), command), Corr).IsSuccess, Is.True);
            Result<CombatEncounterRecord> changed = CombatEncounterService.Advance(_encounters, _campaignRepository, _campaign, new AdvanceCombatEncounterRequest(encounter.EncounterId, encounter.Revision + 1, global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost(), command), Corr);
            Assert.That(changed.IsFailure, Is.True); Assert.That(changed.Error.Code, Is.EqualTo(ErrorCodes.CommandIdentityMismatch));
        }

        [Test]
        public void Exact_advance_replay_returns_durable_result_without_duplicate_events()
        {
            CharacterId first = Active("first"), second = Active("second"); CombatEncounterRecord encounter = Create(first, second).Value;
            CommandId command = Command(); var request = new AdvanceCombatEncounterRequest(encounter.EncounterId, encounter.Revision, global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost(), command);
            CombatEncounterRecord advanced = CombatEncounterService.Advance(_encounters, _campaignRepository, _campaign, request, Corr).Value; int events = Events(encounter.EncounterId).Count;
            CombatEncounterRecord replay = CombatEncounterService.Advance(_encounters, _campaignRepository, _campaign, request, Corr).Value;
            Assert.That(replay.Revision, Is.EqualTo(advanced.Revision)); Assert.That(replay.CurrentParticipantId, Is.EqualTo(advanced.CurrentParticipantId)); Assert.That(replay.RoundOrdinal, Is.EqualTo(advanced.RoundOrdinal)); Assert.That(replay.TurnOrdinal, Is.EqualTo(advanced.TurnOrdinal)); Assert.That(Events(encounter.EncounterId).Count, Is.EqualTo(events));
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
            Result<CombatEncounterRecord> denied = CombatEncounterService.Create(fake, _campaignRepository, _campaign, new CreateCombatEncounterRequest(new[] { id }, User(), Command()), Corr);
            Assert.That(denied.IsFailure, Is.True); Assert.That(denied.Error.CorrelationId, Is.EqualTo(Corr)); Assert.That(fake.Calls, Is.Zero);
            CharacterId active = Active("replay"); CommandId command = Command(); var request = new CreateCombatEncounterRequest(new[] { active }, global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost(), command);
            CombatEncounterRecord first = CombatEncounterService.Create(_encounters, _campaignRepository, _campaign, request, Corr).Value;
            CombatEncounterRecord replay = CombatEncounterService.Create(_encounters, _campaignRepository, _campaign, request, Corr).Value;
            Assert.That(replay.EncounterId, Is.EqualTo(first.EncounterId)); Assert.That(Events(first.EncounterId).Count, Is.EqualTo(3));
        }

        [Test]
        public void Foreign_command_collisions_preserve_correlation_and_do_not_mutate()
        {
            CharacterId active = Active("collision"); CommandId deleteCommand = Command(); CorrelationId deleteCorrelation = CorrelationId.Parse("corr_11111111111111111111111111111111");
            Seed("CREATE TABLE IF NOT EXISTS ContentDefinitionDeleteLedger (CommandId TEXT PRIMARY KEY, ContentDefinitionId TEXT NOT NULL, DeletedAt TEXT NOT NULL); INSERT INTO ContentDefinitionDeleteLedger VALUES ($id, 'def_0123456789abcdef0123456789abcdef', '2026-01-01T00:00:00.0000000Z');", deleteCommand);
            Result<CombatEncounterRecord> rejected = CombatEncounterService.Create(_encounters, _campaignRepository, _campaign, new CreateCombatEncounterRequest(new[] { active }, global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost(), deleteCommand), deleteCorrelation);
            Assert.That(rejected.IsFailure, Is.True); Assert.That(rejected.Error.Code, Is.EqualTo(ErrorCodes.CommandIdentityMismatch)); Assert.That(rejected.Error.CorrelationId, Is.EqualTo(deleteCorrelation)); Assert.That(Count("CombatEncounter"), Is.Zero); Assert.That(Count("CombatEncounterCommandLedger"), Is.Zero);
            CommandId appliedCommand = Command(); Seed("INSERT INTO AppliedCommands (CommandId, Status, ResultEventSequenceFrom, ResultEventSequenceTo, ResultSummary, FailureCode, CreatedAt, CompletedAt) VALUES ($id, 'Completed', 1, 1, '', NULL, '2026-01-01T00:00:00.0000000Z', '2026-01-01T00:00:00.0000000Z');", appliedCommand);
            Assert.That(CombatEncounterService.Create(_encounters, _campaignRepository, _campaign, new CreateCombatEncounterRequest(new[] { active }, global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost(), appliedCommand), Corr).Error.Code, Is.EqualTo(ErrorCodes.CommandIdentityMismatch));
            CombatEncounterRecord encounter = Create(active).Value; int events = Events(encounter.EncounterId).Count; CommandId advanceCommand = Command(); Seed("INSERT INTO AppliedCommands (CommandId, Status, ResultEventSequenceFrom, ResultEventSequenceTo, ResultSummary, FailureCode, CreatedAt, CompletedAt) VALUES ($id, 'Completed', 1, 1, '', NULL, '2026-01-01T00:00:00.0000000Z', '2026-01-01T00:00:00.0000000Z');", advanceCommand);
            CorrelationId advanceCorrelation = CorrelationId.Parse("corr_22222222222222222222222222222222"); Result<CombatEncounterRecord> advance = CombatEncounterService.Advance(_encounters, _campaignRepository, _campaign, new AdvanceCombatEncounterRequest(encounter.EncounterId, encounter.Revision, global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost(), advanceCommand), advanceCorrelation);
            Assert.That(advance.IsFailure, Is.True); Assert.That(advance.Error.Code, Is.EqualTo(ErrorCodes.CommandIdentityMismatch)); Assert.That(advance.Error.CorrelationId, Is.EqualTo(advanceCorrelation)); Assert.That(_encounters.Get(_campaign, encounter.EncounterId, Corr).Value.Revision, Is.EqualTo(encounter.Revision)); Assert.That(Events(encounter.EncounterId).Count, Is.EqualTo(events));
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
            Result<CampaignHandle> other = new SqliteCampaignRepository(new SystemWallClock()).Create(new CreateCampaignRequest(otherRoot, "Other", "ruleset.core", "1.0.0", "0.1.0", global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost()), Command(), Corr);
            Assert.That(_encounters.Get(other.Value, encounter.EncounterId, Corr).IsFailure, Is.True);
        }

        private Result<CombatEncounterRecord> Create(params CharacterId[] ids) => CombatEncounterService.Create(_encounters, _campaignRepository, _campaign, new CreateCombatEncounterRequest(ids, global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost(), Command()), Corr);
        private Result<CombatEncounterRecord> Advance(CombatEncounterRecord record) => CombatEncounterService.Advance(_encounters, _campaignRepository, _campaign, new AdvanceCombatEncounterRequest(record.EncounterId, record.Revision, global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost(), Command()), Corr);
        private CharacterId Active(string name) { CharacterId id = _characters.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, name), Command(), Corr).Value.CharacterId; CharacterRecord current = _characters.GetCharacter(_campaign, id, Corr).Value; return _characters.ApproveCharacterDraft(_campaign, id, global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost(), current.Revisions.LifecycleRevision, Command(), Corr).Value.CharacterId; }
        private void Archive(CharacterId id) { CharacterRecord current = _characters.GetCharacter(_campaign, id, Corr).Value; Assert.That(_characters.ArchiveCharacter(_campaign, id, global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost(), current.Revisions.LifecycleRevision, Command(), Corr).IsSuccess, Is.True); }
        private List<string> Events(CombatEncounterId id) { var values = new List<string>(); using var c = new SqliteConnection("Data Source=" + Path.Combine(_root, "campaign.db")); c.Open(); using var q = c.CreateCommand(); q.CommandText = "SELECT EventKind FROM CombatEncounterLifecycleEvent WHERE EncounterId=$id ORDER BY EventId;"; q.Parameters.AddWithValue("$id", id.ToString()); using var r = q.ExecuteReader(); while (r.Read()) values.Add(r.GetString(0)); return values; }
        private void Seed(string sql, CommandId command) { using var c = new SqliteConnection("Data Source=" + Path.Combine(_root, "campaign.db")); c.Open(); using var q = c.CreateCommand(); q.CommandText = sql; q.Parameters.AddWithValue("$id", command.ToString()); q.ExecuteNonQuery(); }
        private int Count(string table) { using var c = new SqliteConnection("Data Source=" + Path.Combine(_root, "campaign.db")); c.Open(); using var q = c.CreateCommand(); q.CommandText = "SELECT COUNT(*) FROM " + table; return Convert.ToInt32(q.ExecuteScalar()); }
        private static bool IndexExists(SqliteConnection c, string name) { using var q = c.CreateCommand(); q.CommandText = "SELECT 1 FROM sqlite_master WHERE type='index' AND name=$name;"; q.Parameters.AddWithValue("$name", name); return q.ExecuteScalar() != null; }
        private static bool Throws<T>(Action action) where T : Exception { try { action(); return false; } catch (T) { return true; } }
        private static CommandId Command() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N")); private static UserId User() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
        private sealed class CountingRepository : ICombatEncounterRepository { public int Calls { get; private set; } public Result<CombatEncounterRecord> Create(CampaignHandle c, CreateCombatEncounterCommand x, CorrelationId id) { Calls++; throw new InvalidOperationException(); } public Result<CombatEncounterRecord> Advance(CampaignHandle c, AdvanceCombatEncounterCommand x, CorrelationId id) { Calls++; throw new InvalidOperationException(); } public Result<CombatEncounterRecord> Get(CampaignHandle c, CombatEncounterId x, CorrelationId id) { Calls++; throw new InvalidOperationException(); } }

        // ---- ODY-S10-103: MainGM is the stored membership -------------------------------------------

        private UserId AddMember(CampaignMembershipRole role)
        {
            UserId user = User();
            Assert.That(_campaignRepository.AddMember(_campaign, user, role, Command(), Corr).IsSuccess, Is.True);
            return user;
        }

        [Test] // TC-PERSIST-051
        public void CreateAndAdvance_AreMainGmOnly_ByTheStoredMembership()
        {
            UserId host = global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost();
            UserId stranger = User();
            UserId player = AddMember(CampaignMembershipRole.Player);
            UserId observer = AddMember(CampaignMembershipRole.Observer);
            UserId secondGm = AddMember(CampaignMembershipRole.MainGm);

            // Denied before the repository is ever touched.
            var counting = new CountingRepository();
            foreach (UserId actor in new[] { stranger, player, observer })
            {
                Result<CombatEncounterRecord> created = CombatEncounterService.Create(counting, _campaignRepository, _campaign, new CreateCombatEncounterRequest(new[] { Active("denied-" + actor) }, actor, Command()), Corr);
                Assert.That(created.IsFailure, Is.True, "Create must deny a user who is not a stored MainGm");
                Assert.That(created.Error.SafeReasonCode, Is.EqualTo(SafeReasonCode.PermissionDenied));
            }

            Assert.That(counting.Calls, Is.Zero, "a denial happens before any repository call");

            // A stored MainGm (the host, or an added one) is let through.
            CombatEncounterRecord encounter = CombatEncounterService.Create(_encounters, _campaignRepository, _campaign, new CreateCombatEncounterRequest(new[] { Active("host-a"), Active("host-b") }, host, Command()), Corr).Value;
            Assert.That(CombatEncounterService.Create(_encounters, _campaignRepository, _campaign, new CreateCombatEncounterRequest(new[] { Active("gm2-a"), Active("gm2-b") }, secondGm, Command()), Corr).IsSuccess, Is.True);

            foreach (UserId actor in new[] { stranger, player, observer })
            {
                Result<CombatEncounterRecord> advanced = CombatEncounterService.Advance(_encounters, _campaignRepository, _campaign, new AdvanceCombatEncounterRequest(encounter.EncounterId, encounter.Revision, actor, Command()), Corr);
                Assert.That(advanced.IsFailure, Is.True, "Advance must deny a user who is not a stored MainGm");
                Assert.That(advanced.Error.SafeReasonCode, Is.EqualTo(SafeReasonCode.PermissionDenied));
            }

            Assert.That(_encounters.Get(_campaign, encounter.EncounterId, Corr).Value.Revision, Is.EqualTo(encounter.Revision), "the encounter did not advance");
            Assert.That(CombatEncounterService.Advance(_encounters, _campaignRepository, _campaign, new AdvanceCombatEncounterRequest(encounter.EncounterId, encounter.Revision, secondGm, Command()), Corr).IsSuccess, Is.True);
        }

        [Test] // TC-PERSIST-054
        public void CreateAndAdvance_FailClosed_WhenTheMembershipLookupFails_ApplicationLayer()
        {
            var counting = new CountingRepository();
            var poisoned = PoisonedMembershipCampaignRepository.FailsOnLookup();
            UserId host = global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost();

            Result<CombatEncounterRecord> created = CombatEncounterService.Create(counting, poisoned, _campaign, new CreateCombatEncounterRequest(new[] { Active("closed") }, host, Command()), Corr);
            Assert.That(created.IsFailure, Is.True);
            Assert.That(created.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCampaignIoFailed), "an unreadable membership is the lookup's own failure, not a pass and not a fake denial");

            Result<CombatEncounterRecord> advanced = CombatEncounterService.Advance(counting, poisoned, _campaign, new AdvanceCombatEncounterRequest(CombatEncounterId.NewId(new SystemWallClock().GetUtcNow()), 1, host, Command()), Corr);
            Assert.That(advanced.IsFailure, Is.True);
            Assert.That(advanced.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCampaignIoFailed));
            Assert.That(counting.Calls, Is.Zero);
            Assert.That(poisoned.LookupCalls, Is.EqualTo(2));
        }
    }
}
