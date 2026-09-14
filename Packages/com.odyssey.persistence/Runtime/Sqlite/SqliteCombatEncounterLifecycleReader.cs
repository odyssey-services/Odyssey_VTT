using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Odyssey.Application.Combat;
using Odyssey.Application.Effects;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;

namespace Odyssey.Persistence.Sqlite
{
    /// <summary>
    /// ODY-S05-605: the sole SQLite implementation of <see cref="ICombatEncounterLifecycleReader"/>.
    /// Read-only: it calls the existing, unmodified <see cref="ICombatEncounterRepository.Get"/>
    /// first (confirms the encounter exists and, as a side effect, that
    /// `ODY-S05-602`'s own tables already exist) and then queries the
    /// already-existing `CombatEncounterLifecycleEvent` audit table directly
    /// -- the same physical table `SqliteCombatEncounterRepository` already
    /// owns, composed rather than duplicated, mirroring
    /// `SqliteAttackStateReader`'s own "new standalone reader over existing
    /// tables" precedent. No method is appended to `ICombatEncounterRepository`.
    /// </summary>
    public sealed class SqliteCombatEncounterLifecycleReader : ICombatEncounterLifecycleReader
    {
        private readonly ICombatEncounterRepository _encounters;

        public SqliteCombatEncounterLifecycleReader(ICombatEncounterRepository encounters)
        {
            _encounters = encounters ?? throw new ArgumentNullException(nameof(encounters));
        }

        public Result<int> CountLifecycleEvents(CampaignHandle campaign, CombatEncounterId encounterId, CharacterId characterId, string eventKind, long sinceEventId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!encounterId.IsValid) throw new ArgumentException("EncounterId is required.", nameof(encounterId));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (string.IsNullOrWhiteSpace(eventKind)) throw new ArgumentException("EventKind is required.", nameof(eventKind));

            Result<Application.Combat.CombatEncounterRecord> encounter = _encounters.Get(campaign, encounterId, correlationId);
            if (encounter.IsFailure) return Result<int>.Failure(encounter.Error);

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                using var select = connection.CreateCommand();
                select.CommandText = "SELECT COUNT(*) FROM CombatEncounterLifecycleEvent WHERE EncounterId=$encounterId AND CharacterId=$characterId AND EventKind=$eventKind AND EventId > $sinceEventId;";
                select.Parameters.AddWithValue("$encounterId", encounterId.ToString());
                select.Parameters.AddWithValue("$characterId", characterId.ToString());
                select.Parameters.AddWithValue("$eventKind", eventKind);
                select.Parameters.AddWithValue("$sinceEventId", sinceEventId);
                return Result<int>.Success(Convert.ToInt32(select.ExecuteScalar()));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<int>.Failure(PersistenceFailures.CombatEncounterLifecycleIoFailed(correlationId));
            }
        }

        private static SqliteConnection OpenConnection(string campaignRootPath)
        {
            string dbPath = Path.Combine(campaignRootPath, "campaign.db");
            var connection = new SqliteConnection("Data Source=" + dbPath);
            connection.Open();
            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText =
                    "PRAGMA journal_mode = WAL; " +
                    "PRAGMA foreign_keys = ON; " +
                    "PRAGMA synchronous = FULL; " +
                    "PRAGMA busy_timeout = 5000;";
                pragma.ExecuteNonQuery();
            }

            return connection;
        }
    }
}
