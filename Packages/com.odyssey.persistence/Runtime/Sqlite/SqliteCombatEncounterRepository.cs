using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using Odyssey.Application.Combat;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Persistence.Sqlite
{
    /// <summary>ODY-S05-602's small, authoritative encounter timeline store. It deliberately owns no attack, effect or Game Log behaviour.</summary>
    public sealed class SqliteCombatEncounterRepository : ICombatEncounterRepository
    {
        private readonly IWallClock _clock;
        public SqliteCombatEncounterRepository(IWallClock clock) { _clock = clock ?? throw new ArgumentNullException(nameof(clock)); }

        public Result<CombatEncounterRecord> Create(CampaignHandle campaign, CreateCombatEncounterCommand request, CorrelationId correlationId)
        {
            if (campaign == null || request == null) throw new ArgumentNullException(campaign == null ? nameof(campaign) : nameof(request));
            if (!DistinctNonEmpty(request.ParticipantOrder)) return Result<CombatEncounterRecord>.Failure(Invalid(correlationId));
            try
            {
                using SqliteConnection c = Open(campaign.RootPath); Ensure(c);
                using SqliteTransaction t = c.BeginTransaction();
                Result<CombatEncounterRecord>? replay = Replay(c, t, campaign.CampaignId, request.CommandId, "create", Fingerprint(request.ParticipantOrder), correlationId);
                if (replay != null) { t.Commit(); return replay.Value; }
                if (HasForeignCommandId(c, t, request.CommandId)) return Result<CombatEncounterRecord>.Failure(IdentityMismatch(correlationId));
                for (int i = 0; i < request.ParticipantOrder.Count; i++) if (!IsEligible(c, t, campaign.CampaignId, request.ParticipantOrder[i])) return Result<CombatEncounterRecord>.Failure(Invalid(correlationId));
                UtcInstant now = _clock.GetUtcNow(); CombatEncounterId id = CombatEncounterId.NewId(now);
                InsertEncounter(c, t, id, campaign, 1, 1, 1, CombatEncounterStatus.Open, CombatPhase.TurnOpen, request.ParticipantOrder[0], now);
                for (int i = 0; i < request.ParticipantOrder.Count; i++) InsertParticipant(c, t, id, request.ParticipantOrder[i], i);
                InsertEvent(c, t, id, CombatLifecycleEventKind.Created, 1, 1, null, now);
                InsertEvent(c, t, id, CombatLifecycleEventKind.RoundStarted, 1, 1, null, now);
                InsertEvent(c, t, id, CombatLifecycleEventKind.TurnStarted, 1, 1, request.ParticipantOrder[0], now);
                CombatEncounterRecord record = GetInternal(c, t, campaign.CampaignId, id)!;
                InsertLedger(c, t, request.CommandId, "create", Fingerprint(request.ParticipantOrder), id, now);
                t.Commit(); return Result<CombatEncounterRecord>.Success(record);
            }
            catch (SqliteException) { return Result<CombatEncounterRecord>.Failure(Io(correlationId)); }
            catch (IOException) { return Result<CombatEncounterRecord>.Failure(Io(correlationId)); }
        }

        public Result<CombatEncounterRecord> Advance(CampaignHandle campaign, AdvanceCombatEncounterCommand request, CorrelationId correlationId)
        {
            if (campaign == null || request == null) throw new ArgumentNullException(campaign == null ? nameof(campaign) : nameof(request));
            try
            {
                using SqliteConnection c = Open(campaign.RootPath); Ensure(c);
                using SqliteTransaction t = c.BeginTransaction(); string fingerprint = request.EncounterId + ":" + request.ExpectedRevision;
                Result<CombatEncounterRecord>? replay = Replay(c, t, campaign.CampaignId, request.CommandId, "advance", fingerprint, correlationId);
                if (replay != null) { t.Commit(); return replay.Value; }
                if (HasForeignCommandId(c, t, request.CommandId)) return Result<CombatEncounterRecord>.Failure(IdentityMismatch(correlationId));
                CombatEncounterRecord? current = GetInternal(c, t, campaign.CampaignId, request.EncounterId);
                if (current == null || current.Status != CombatEncounterStatus.Open) return Result<CombatEncounterRecord>.Failure(Invalid(correlationId));
                if (current.Revision != request.ExpectedRevision) return Result<CombatEncounterRecord>.Failure(Conflict(correlationId));
                UtcInstant now = _clock.GetUtcNow(); InsertEvent(c, t, current.EncounterId, CombatLifecycleEventKind.TurnEnded, current.RoundOrdinal, current.TurnOrdinal, current.CurrentParticipantId, now);
                int start = IndexOf(current.Participants, current.CurrentParticipantId!.Value) + 1;
                CharacterId? next = null; int nextIndex = -1;
                for (int offset = 0; offset < current.Participants.Count; offset++)
                {
                    int index = (start + offset) % current.Participants.Count; CharacterId candidate = current.Participants[index].CharacterId;
                    if (IsEligible(c, t, campaign.CampaignId, candidate)) { next = candidate; nextIndex = index; break; }
                    InsertEvent(c, t, current.EncounterId, CombatLifecycleEventKind.ParticipantSkipped, current.RoundOrdinal, current.TurnOrdinal, candidate, now);
                }
                long round = current.RoundOrdinal; long turn = current.TurnOrdinal + 1; long revision = current.Revision + 1;
                if (!next.HasValue)
                {
                    if (!Update(c, t, current.EncounterId, current.Revision, revision, round, turn, CombatEncounterStatus.ClosedNoEligibleParticipants, CombatPhase.Closed, null, now)) return Result<CombatEncounterRecord>.Failure(Conflict(correlationId));
                    InsertEvent(c, t, current.EncounterId, CombatLifecycleEventKind.ClosedNoEligibleParticipants, round, turn, null, now);
                }
                else
                {
                    if (nextIndex <= IndexOf(current.Participants, current.CurrentParticipantId.Value)) { round++; InsertEvent(c, t, current.EncounterId, CombatLifecycleEventKind.RoundStarted, round, turn, null, now); }
                    if (!Update(c, t, current.EncounterId, current.Revision, revision, round, turn, CombatEncounterStatus.Open, CombatPhase.TurnOpen, next, now)) return Result<CombatEncounterRecord>.Failure(Conflict(correlationId));
                    InsertEvent(c, t, current.EncounterId, CombatLifecycleEventKind.TurnStarted, round, turn, next, now);
                }
                CombatEncounterRecord result = GetInternal(c, t, campaign.CampaignId, current.EncounterId)!;
                InsertLedger(c, t, request.CommandId, "advance", fingerprint, current.EncounterId, now); t.Commit(); return Result<CombatEncounterRecord>.Success(result);
            }
            catch (SqliteException) { return Result<CombatEncounterRecord>.Failure(Io(correlationId)); }
            catch (IOException) { return Result<CombatEncounterRecord>.Failure(Io(correlationId)); }
        }

        public Result<CombatEncounterRecord> Get(CampaignHandle campaign, CombatEncounterId encounterId, CorrelationId correlationId)
        { try { using SqliteConnection c = Open(campaign.RootPath); Ensure(c); CombatEncounterRecord? r = GetInternal(c, null, campaign.CampaignId, encounterId); return r == null ? Result<CombatEncounterRecord>.Failure(Invalid(correlationId)) : Result<CombatEncounterRecord>.Success(r); } catch (SqliteException) { return Result<CombatEncounterRecord>.Failure(Io(correlationId)); } }

        private static bool DistinctNonEmpty(IReadOnlyList<CharacterId> ids) { if (ids.Count == 0) return false; var seen = new HashSet<CharacterId>(); foreach (CharacterId id in ids) if (!id.IsValid || !seen.Add(id)) return false; return true; }
        private static string Fingerprint(IReadOnlyList<CharacterId> ids) { var values = new string[ids.Count]; for (int i = 0; i < ids.Count; i++) values[i] = ids[i].ToString(); return string.Join(",", values); }
        private static int IndexOf(IReadOnlyList<CombatParticipant> items, CharacterId id) { for (int i = 0; i < items.Count; i++) if (items[i].CharacterId == id) return i; return -1; }
        private static bool IsEligible(SqliteConnection c, SqliteTransaction t, CampaignId campaign, CharacterId character) { using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = "SELECT 1 FROM Character WHERE CharacterId=$id AND CampaignId=$campaign AND LifecycleStatus='Active' LIMIT 1;"; q.Parameters.AddWithValue("$id", character.ToString()); q.Parameters.AddWithValue("$campaign", campaign.ToString()); return q.ExecuteScalar() != null; }
        private static Result<CombatEncounterRecord>? Replay(SqliteConnection c, SqliteTransaction t, CampaignId campaign, CommandId command, string kind, string fingerprint, CorrelationId correlationId) { using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = "SELECT Kind,Fingerprint,EncounterId FROM CombatEncounterCommandLedger WHERE CommandId=$id;"; q.Parameters.AddWithValue("$id", command.ToString()); using var r = q.ExecuteReader(); if (!r.Read()) return null; string storedKind = r.GetString(0), storedFingerprint = r.GetString(1), storedEncounter = r.GetString(2); r.Close(); if (storedKind != kind || storedFingerprint != fingerprint) return Result<CombatEncounterRecord>.Failure(IdentityMismatch(correlationId)); CombatEncounterRecord? result = GetInternal(c, t, campaign, CombatEncounterId.Parse(storedEncounter)); return result == null ? Result<CombatEncounterRecord>.Failure(Io(correlationId)) : Result<CombatEncounterRecord>.Success(result); }
        private static bool HasForeignCommandId(SqliteConnection c, SqliteTransaction t, CommandId command)
        {
            string[] tables = { "AppliedCommands", "InventoryCommandLedger", "InventoryMoveCommandLedger", "InventoryStackCommandLedger", "EquipmentCommandLedger", "ItemDefinitionMigrationCommandLedger", "ContentDefinitionCommandLedger", "ContentDefinitionDeleteCommandLedger", "ActiveEffect" };
            for (int i = 0; i < tables.Length; i++)
            {
                using var exists = c.CreateCommand(); exists.Transaction = t; exists.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name;"; exists.Parameters.AddWithValue("$name", tables[i]);
                if (exists.ExecuteScalar() == null) continue;
                using var query = c.CreateCommand(); query.Transaction = t; query.CommandText = "SELECT 1 FROM " + tables[i] + " WHERE " + (tables[i] == "ActiveEffect" ? "LastCommandId" : "CommandId") + "=$id LIMIT 1;"; query.Parameters.AddWithValue("$id", command.ToString());
                if (query.ExecuteScalar() != null) return true;
            }
            return false;
        }
        private static CombatEncounterRecord? GetInternal(SqliteConnection c, SqliteTransaction? t, CampaignId campaign, CombatEncounterId id) { using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = "SELECT CampaignId,RulesetId,RulesetVersion,Revision,RoundOrdinal,TurnOrdinal,Status,Phase,CurrentParticipantId,CreatedAt,UpdatedAt FROM CombatEncounter WHERE EncounterId=$id AND CampaignId=$campaign;"; q.Parameters.AddWithValue("$id", id.ToString()); q.Parameters.AddWithValue("$campaign", campaign.ToString()); using var r = q.ExecuteReader(); if (!r.Read()) return null; var participants = new List<CombatParticipant>(); string cid = r.GetString(0); string rulesetId = r.GetString(1); string version = r.GetString(2); long revision = r.GetInt64(3), round = r.GetInt64(4), turn = r.GetInt64(5); var status = (CombatEncounterStatus)Enum.Parse(typeof(CombatEncounterStatus), r.GetString(6)); var phase = (CombatPhase)Enum.Parse(typeof(CombatPhase), r.GetString(7)); CharacterId? current = r.IsDBNull(8) ? null : CharacterId.Parse(r.GetString(8)); UtcInstant created = UtcInstant.Parse(r.GetString(9)), updated = UtcInstant.Parse(r.GetString(10)); r.Close(); using var p = c.CreateCommand(); p.Transaction = t; p.CommandText = "SELECT CharacterId,ParticipantOrder FROM CombatEncounterParticipant WHERE EncounterId=$id ORDER BY ParticipantOrder;"; p.Parameters.AddWithValue("$id", id.ToString()); using var pr = p.ExecuteReader(); while (pr.Read()) participants.Add(new CombatParticipant(CharacterId.Parse(pr.GetString(0)), pr.GetInt32(1))); return new CombatEncounterRecord(id, CampaignId.Parse(cid), rulesetId, version, participants, revision, round, turn, status, phase, current, created, updated); }
        private static void InsertEncounter(SqliteConnection c, SqliteTransaction t, CombatEncounterId id, CampaignHandle campaign, long rev, long round, long turn, CombatEncounterStatus status, CombatPhase phase, CharacterId current, UtcInstant now) { using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = "INSERT INTO CombatEncounter (EncounterId,CampaignId,RulesetId,RulesetVersion,Revision,RoundOrdinal,TurnOrdinal,Status,Phase,CurrentParticipantId,CreatedAt,UpdatedAt) VALUES ($id,$campaign,$rulesetId,$version,$revision,$round,$turn,$status,$phase,$current,$now,$now);"; q.Parameters.AddWithValue("$id", id.ToString()); q.Parameters.AddWithValue("$campaign", campaign.CampaignId.ToString()); q.Parameters.AddWithValue("$rulesetId", campaign.Manifest.RulesetId); q.Parameters.AddWithValue("$version", campaign.Manifest.RulesetVersion); q.Parameters.AddWithValue("$revision", rev); q.Parameters.AddWithValue("$round", round); q.Parameters.AddWithValue("$turn", turn); q.Parameters.AddWithValue("$status", status.ToString()); q.Parameters.AddWithValue("$phase", phase.ToString()); q.Parameters.AddWithValue("$current", current.ToString()); q.Parameters.AddWithValue("$now", now.ToString()); q.ExecuteNonQuery(); }
        private static void InsertParticipant(SqliteConnection c, SqliteTransaction t, CombatEncounterId id, CharacterId character, int order) { using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = "INSERT INTO CombatEncounterParticipant (EncounterId,CharacterId,ParticipantOrder) VALUES ($id,$character,$order);"; q.Parameters.AddWithValue("$id", id.ToString()); q.Parameters.AddWithValue("$character", character.ToString()); q.Parameters.AddWithValue("$order", order); q.ExecuteNonQuery(); }
        private static void InsertEvent(SqliteConnection c, SqliteTransaction t, CombatEncounterId id, CombatLifecycleEventKind kind, long round, long turn, CharacterId? character, UtcInstant now) { using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = "INSERT INTO CombatEncounterLifecycleEvent (EncounterId,EventKind,RoundOrdinal,TurnOrdinal,CharacterId,OccurredAt) VALUES ($id,$kind,$round,$turn,$character,$now);"; q.Parameters.AddWithValue("$id", id.ToString()); q.Parameters.AddWithValue("$kind", kind.ToString()); q.Parameters.AddWithValue("$round", round); q.Parameters.AddWithValue("$turn", turn); q.Parameters.AddWithValue("$character", character.HasValue ? (object)character.Value.ToString() : DBNull.Value); q.Parameters.AddWithValue("$now", now.ToString()); q.ExecuteNonQuery(); }
        private static bool Update(SqliteConnection c, SqliteTransaction t, CombatEncounterId id, long expected, long revision, long round, long turn, CombatEncounterStatus status, CombatPhase phase, CharacterId? current, UtcInstant now) { using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = "UPDATE CombatEncounter SET Revision=$revision,RoundOrdinal=$round,TurnOrdinal=$turn,Status=$status,Phase=$phase,CurrentParticipantId=$current,UpdatedAt=$now WHERE EncounterId=$id AND Revision=$expected;"; q.Parameters.AddWithValue("$id", id.ToString()); q.Parameters.AddWithValue("$expected", expected); q.Parameters.AddWithValue("$revision", revision); q.Parameters.AddWithValue("$round", round); q.Parameters.AddWithValue("$turn", turn); q.Parameters.AddWithValue("$status", status.ToString()); q.Parameters.AddWithValue("$phase", phase.ToString()); q.Parameters.AddWithValue("$current", current.HasValue ? (object)current.Value.ToString() : DBNull.Value); q.Parameters.AddWithValue("$now", now.ToString()); return q.ExecuteNonQuery() == 1; }
        private static void InsertLedger(SqliteConnection c, SqliteTransaction t, CommandId id, string kind, string fingerprint, CombatEncounterId encounter, UtcInstant now) { using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = "INSERT INTO CombatEncounterCommandLedger (CommandId,Kind,Fingerprint,EncounterId,AppliedAt) VALUES ($id,$kind,$fingerprint,$encounter,$now);"; q.Parameters.AddWithValue("$id", id.ToString()); q.Parameters.AddWithValue("$kind", kind); q.Parameters.AddWithValue("$fingerprint", fingerprint); q.Parameters.AddWithValue("$encounter", encounter.ToString()); q.Parameters.AddWithValue("$now", now.ToString()); q.ExecuteNonQuery(); }
        private static SqliteConnection Open(string root) { var c = new SqliteConnection("Data Source=" + Path.Combine(root, "campaign.db")); c.Open(); using var q = c.CreateCommand(); q.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;"; q.ExecuteNonQuery(); return c; }
        private static void Ensure(SqliteConnection c) { using var q = c.CreateCommand(); q.CommandText = @"CREATE TABLE IF NOT EXISTS CombatEncounter (EncounterId TEXT PRIMARY KEY, CampaignId TEXT NOT NULL, RulesetId TEXT NOT NULL, RulesetVersion TEXT NOT NULL, Revision INTEGER NOT NULL, RoundOrdinal INTEGER NOT NULL, TurnOrdinal INTEGER NOT NULL, Status TEXT NOT NULL, Phase TEXT NOT NULL, CurrentParticipantId TEXT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL); CREATE TABLE IF NOT EXISTS CombatEncounterParticipant (EncounterId TEXT NOT NULL, CharacterId TEXT NOT NULL, ParticipantOrder INTEGER NOT NULL, PRIMARY KEY (EncounterId, CharacterId), UNIQUE (EncounterId, ParticipantOrder)); CREATE TABLE IF NOT EXISTS CombatEncounterLifecycleEvent (EventId INTEGER PRIMARY KEY AUTOINCREMENT, EncounterId TEXT NOT NULL, EventKind TEXT NOT NULL, RoundOrdinal INTEGER NOT NULL, TurnOrdinal INTEGER NOT NULL, CharacterId TEXT NULL, OccurredAt TEXT NOT NULL); CREATE TABLE IF NOT EXISTS CombatEncounterCommandLedger (CommandId TEXT PRIMARY KEY, Kind TEXT NOT NULL, Fingerprint TEXT NOT NULL, EncounterId TEXT NOT NULL, AppliedAt TEXT NOT NULL); CREATE INDEX IF NOT EXISTS IX_CombatEncounter_CampaignId ON CombatEncounter(CampaignId); CREATE INDEX IF NOT EXISTS IX_CombatEncounterLifecycleEvent_EncounterId ON CombatEncounterLifecycleEvent(EncounterId, EventId);"; q.ExecuteNonQuery(); }
        private static Error Invalid(CorrelationId c) => Error.Create(ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Validation, SafeReasonCode.InvalidRequest, UserMessageKey.Parse("errors.combat.invalid"), RetryDirective.DoNotRetry, c);
        private static Error Denied(CorrelationId c) => Error.Create(ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Authorization, SafeReasonCode.PermissionDenied, UserMessageKey.Parse("errors.combat.denied"), RetryDirective.DoNotRetry, c);
        private static Error Conflict(CorrelationId c) => Error.Create(ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Conflict, SafeReasonCode.StateChanged, UserMessageKey.Parse("errors.combat.conflict"), RetryDirective.DoNotRetry, c);
        private static Error Io(CorrelationId c) => Error.Create(ErrorCodes.PersistenceCampaignIoFailed, ErrorCategory.PermanentInfrastructure, SafeReasonCode.UnexpectedError, UserMessageKey.Parse("errors.combat.io"), RetryDirective.ManualRecoveryRequired, c);
        private static Error IdentityMismatch(CorrelationId c) => Error.Create(ErrorCodes.CommandIdentityMismatch, ErrorCategory.Conflict, SafeReasonCode.InvalidRequest, UserMessageKey.Parse("errors.combat.identity_mismatch"), RetryDirective.DoNotRetry, c);
    }
}
