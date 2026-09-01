using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Persistence.Sqlite
{
    /// <summary>
    /// ODY-S04-103: implements <see cref="ICharacterTemplateRepository"/>.
    /// ADR-023 section 5.1's single <c>CharacterTemplate</c> aggregate type is
    /// one shared table schema (<see cref="EnsureCharacterTemplateTables"/>)
    /// physically stored in one of two files depending on
    /// <see cref="TemplateStorageHandle.Scope"/> -- a <c>Personal</c> template
    /// lives in <c>local_profile.db</c> inside the profile's own root path
    /// (the same personal-profile storage boundary a local Draft uses,
    /// ADR-023 section 5.2), a <c>Campaign</c> template lives inside
    /// <c>campaign.db</c>, the same file <see cref="SqliteCharacterRepository"/>
    /// already uses for <c>Character</c>/<c>DomainEvents</c> -- "a sibling of
    /// Character, not a section of it."
    ///
    /// Unlike <c>Character</c>, no ADR/product requirement makes
    /// <c>CharacterTemplate</c> participate in <c>DomainEvents</c>/history --
    /// this repository does ordinary transactional row CRUD with a manual
    /// <c>LastCommandId</c> idempotency column, the same shape
    /// <see cref="SqliteSavingPipeline"/> provides for Character, just without
    /// its event-journal machinery, which this aggregate does not need.
    /// </summary>
    public sealed class SqliteCharacterTemplateRepository : ICharacterTemplateRepository
    {
        private readonly IWallClock _clock;

        public SqliteCharacterTemplateRepository(IWallClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public Result<CharacterTemplateRecord> CreatePersonalCharacterTemplate(LocalProfileHandle profile, string name, CharacterKind characterKind, string rulesetId, string rulesetVersion, string? anatomyProfileRef, CharacterTemplateSeed seed, CommandId commandId, CorrelationId correlationId)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (string.IsNullOrWhiteSpace(rulesetId)) throw new ArgumentException("RulesetId is required.", nameof(rulesetId));
            if (string.IsNullOrWhiteSpace(rulesetVersion)) throw new ArgumentException("RulesetVersion is required.", nameof(rulesetVersion));

            return Create(profile.RootPath, TemplateScope.Personal, profile.OwnerUserId, null, name, characterKind, rulesetId, rulesetVersion, anatomyProfileRef, seed, commandId, correlationId);
        }

        public Result<CharacterTemplateRecord> CreateCampaignCharacterTemplate(CampaignHandle campaign, string name, CharacterKind characterKind, string? anatomyProfileRef, CharacterTemplateSeed seed, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));

            return Create(campaign.RootPath, TemplateScope.Campaign, null, campaign.CampaignId, name, characterKind, campaign.Manifest.RulesetId, campaign.Manifest.RulesetVersion, anatomyProfileRef, seed, commandId, correlationId);
        }

        private Result<CharacterTemplateRecord> Create(string rootPath, TemplateScope scope, UserId? ownerUserId, CampaignId? campaignId, string name, CharacterKind characterKind, string rulesetId, string rulesetVersion, string? anatomyProfileRef, CharacterTemplateSeed seed, CommandId commandId, CorrelationId correlationId)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 128) throw new ArgumentException("Name is not safe.", nameof(name));
            if (!Enum.IsDefined(typeof(CharacterKind), characterKind)) throw new ArgumentOutOfRangeException(nameof(characterKind));
            if (seed == null) throw new ArgumentNullException(nameof(seed));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            try
            {
                using SqliteConnection connection = OpenConnection(rootPath, scope);
                EnsureCharacterTemplateTables(connection);
                using SqliteTransaction transaction = connection.BeginTransaction();

                CharacterTemplateRecord? replay = TryFindByCommandId(connection, transaction, commandId);
                if (replay != null)
                {
                    transaction.Commit();
                    return Result<CharacterTemplateRecord>.Success(replay);
                }

                UtcInstant now = _clock.GetUtcNow();
                CharacterTemplateId templateId = CharacterTemplateId.NewId(now);
                const long initialRevision = 1;
                const CharacterTemplateStatus status = CharacterTemplateStatus.Active;

                using (var insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = "INSERT INTO CharacterTemplate (" +
                        "TemplateId, TemplateScope, OwnerUserId, CampaignId, Name, CharacterKind, RulesetId, RulesetVersion, " +
                        "AnatomyProfileRef, SeedJson, Status, Revision, CreatedAt, UpdatedAt, LastCommandId) VALUES (" +
                        "$templateId, $scope, $ownerUserId, $campaignId, $name, $characterKind, $rulesetId, $rulesetVersion, " +
                        "$anatomyProfileRef, $seedJson, $status, $revision, $createdAt, $updatedAt, $lastCommandId);";
                    insert.Parameters.AddWithValue("$templateId", templateId.ToString());
                    insert.Parameters.AddWithValue("$scope", scope.ToString());
                    insert.Parameters.AddWithValue("$ownerUserId", (object?)ownerUserId?.ToString() ?? DBNull.Value);
                    insert.Parameters.AddWithValue("$campaignId", (object?)campaignId?.ToString() ?? DBNull.Value);
                    insert.Parameters.AddWithValue("$name", name);
                    insert.Parameters.AddWithValue("$characterKind", characterKind.ToString());
                    insert.Parameters.AddWithValue("$rulesetId", rulesetId);
                    insert.Parameters.AddWithValue("$rulesetVersion", rulesetVersion);
                    insert.Parameters.AddWithValue("$anatomyProfileRef", (object?)anatomyProfileRef ?? DBNull.Value);
                    insert.Parameters.AddWithValue("$seedJson", SerializeSeed(seed));
                    insert.Parameters.AddWithValue("$status", status.ToString());
                    insert.Parameters.AddWithValue("$revision", initialRevision);
                    insert.Parameters.AddWithValue("$createdAt", now.ToString());
                    insert.Parameters.AddWithValue("$updatedAt", now.ToString());
                    insert.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                    insert.ExecuteNonQuery();
                }

                transaction.Commit();

                var record = new CharacterTemplateRecord(templateId, scope, ownerUserId, campaignId, name, characterKind, rulesetId, rulesetVersion, anatomyProfileRef, seed, status, initialRevision, now, now);
                return Result<CharacterTemplateRecord>.Success(record);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CharacterTemplateRecord>.Failure(PersistenceFailures.CharacterTemplateIoFailed(correlationId));
            }
        }

        public Result<CharacterTemplateRecord> UpdateCharacterTemplate(TemplateStorageHandle storage, CharacterTemplateId templateId, string name, string? anatomyProfileRef, CharacterTemplateSeed seed, long expectedRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            if (!templateId.IsValid) throw new ArgumentException("TemplateId is required.", nameof(templateId));
            if (string.IsNullOrWhiteSpace(name) || name.Length > 128) throw new ArgumentException("Name is not safe.", nameof(name));
            if (seed == null) throw new ArgumentNullException(nameof(seed));
            if (expectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            try
            {
                using SqliteConnection connection = OpenConnection(storage.RootPath, storage.Scope);
                EnsureCharacterTemplateTables(connection);
                using SqliteTransaction transaction = connection.BeginTransaction();

                CharacterTemplateRecord? replay = TryFindByCommandId(connection, transaction, commandId);
                if (replay != null)
                {
                    transaction.Commit();
                    return Result<CharacterTemplateRecord>.Success(replay);
                }

                CharacterTemplateRecord? current = SelectForUpdate(connection, transaction, templateId);
                if (current == null)
                {
                    transaction.Commit();
                    return Result<CharacterTemplateRecord>.Failure(PersistenceFailures.CharacterTemplateNotFound(correlationId));
                }

                if (current.Revision != expectedRevision)
                {
                    transaction.Commit();
                    return Result<CharacterTemplateRecord>.Failure(PersistenceFailures.CharacterTemplateRevisionConflict(correlationId));
                }

                UtcInstant now = _clock.GetUtcNow();
                long newRevision = current.Revision + 1;

                using (var update = connection.CreateCommand())
                {
                    update.Transaction = transaction;
                    update.CommandText = "UPDATE CharacterTemplate SET Name = $name, AnatomyProfileRef = $anatomyProfileRef, SeedJson = $seedJson, Revision = $revision, UpdatedAt = $updatedAt, LastCommandId = $lastCommandId WHERE TemplateId = $templateId;";
                    update.Parameters.AddWithValue("$name", name);
                    update.Parameters.AddWithValue("$anatomyProfileRef", (object?)anatomyProfileRef ?? DBNull.Value);
                    update.Parameters.AddWithValue("$seedJson", SerializeSeed(seed));
                    update.Parameters.AddWithValue("$revision", newRevision);
                    update.Parameters.AddWithValue("$updatedAt", now.ToString());
                    update.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                    update.Parameters.AddWithValue("$templateId", templateId.ToString());
                    update.ExecuteNonQuery();
                }

                transaction.Commit();

                var record = new CharacterTemplateRecord(templateId, current.Scope, current.OwnerUserId, current.CampaignId, name, current.CharacterKind, current.RulesetId, current.RulesetVersion, anatomyProfileRef, seed, current.Status, newRevision, current.CreatedAt, now);
                return Result<CharacterTemplateRecord>.Success(record);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CharacterTemplateRecord>.Failure(PersistenceFailures.CharacterTemplateIoFailed(correlationId));
            }
        }

        public Result<CharacterTemplateRecord> ArchiveCharacterTemplate(TemplateStorageHandle storage, CharacterTemplateId templateId, long expectedRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            if (!templateId.IsValid) throw new ArgumentException("TemplateId is required.", nameof(templateId));
            if (expectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            try
            {
                using SqliteConnection connection = OpenConnection(storage.RootPath, storage.Scope);
                EnsureCharacterTemplateTables(connection);
                using SqliteTransaction transaction = connection.BeginTransaction();

                CharacterTemplateRecord? replay = TryFindByCommandId(connection, transaction, commandId);
                if (replay != null)
                {
                    transaction.Commit();
                    return Result<CharacterTemplateRecord>.Success(replay);
                }

                CharacterTemplateRecord? current = SelectForUpdate(connection, transaction, templateId);
                if (current == null)
                {
                    transaction.Commit();
                    return Result<CharacterTemplateRecord>.Failure(PersistenceFailures.CharacterTemplateNotFound(correlationId));
                }

                if (current.Revision != expectedRevision)
                {
                    transaction.Commit();
                    return Result<CharacterTemplateRecord>.Failure(PersistenceFailures.CharacterTemplateRevisionConflict(correlationId));
                }

                UtcInstant now = _clock.GetUtcNow();
                long newRevision = current.Revision + 1;
                const CharacterTemplateStatus newStatus = CharacterTemplateStatus.Archived;

                using (var update = connection.CreateCommand())
                {
                    update.Transaction = transaction;
                    update.CommandText = "UPDATE CharacterTemplate SET Status = $status, Revision = $revision, UpdatedAt = $updatedAt, LastCommandId = $lastCommandId WHERE TemplateId = $templateId;";
                    update.Parameters.AddWithValue("$status", newStatus.ToString());
                    update.Parameters.AddWithValue("$revision", newRevision);
                    update.Parameters.AddWithValue("$updatedAt", now.ToString());
                    update.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                    update.Parameters.AddWithValue("$templateId", templateId.ToString());
                    update.ExecuteNonQuery();
                }

                transaction.Commit();

                var record = new CharacterTemplateRecord(templateId, current.Scope, current.OwnerUserId, current.CampaignId, current.Name, current.CharacterKind, current.RulesetId, current.RulesetVersion, current.AnatomyProfileRef, current.Seed, newStatus, newRevision, current.CreatedAt, now);
                return Result<CharacterTemplateRecord>.Success(record);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CharacterTemplateRecord>.Failure(PersistenceFailures.CharacterTemplateIoFailed(correlationId));
            }
        }

        public Result<CharacterTemplateRecord> GetCharacterTemplate(TemplateStorageHandle storage, CharacterTemplateId templateId, CorrelationId correlationId)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            if (!templateId.IsValid) throw new ArgumentException("TemplateId is required.", nameof(templateId));

            try
            {
                using SqliteConnection connection = OpenConnection(storage.RootPath, storage.Scope);
                EnsureCharacterTemplateTables(connection);

                using var select = connection.CreateCommand();
                select.CommandText = SelectColumns + " FROM CharacterTemplate WHERE TemplateId = $templateId LIMIT 1;";
                select.Parameters.AddWithValue("$templateId", templateId.ToString());
                using SqliteDataReader reader = select.ExecuteReader();
                if (!reader.Read())
                {
                    return Result<CharacterTemplateRecord>.Failure(PersistenceFailures.CharacterTemplateNotFound(correlationId));
                }

                return Result<CharacterTemplateRecord>.Success(ReadCharacterTemplateRecord(reader));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CharacterTemplateRecord>.Failure(PersistenceFailures.CharacterTemplateIoFailed(correlationId));
            }
        }

        private static CharacterTemplateRecord? TryFindByCommandId(SqliteConnection connection, SqliteTransaction transaction, CommandId commandId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = SelectColumns + " FROM CharacterTemplate WHERE LastCommandId = $commandId LIMIT 1;";
            select.Parameters.AddWithValue("$commandId", commandId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            return reader.Read() ? ReadCharacterTemplateRecord(reader) : null;
        }

        private static CharacterTemplateRecord? SelectForUpdate(SqliteConnection connection, SqliteTransaction transaction, CharacterTemplateId templateId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = SelectColumns + " FROM CharacterTemplate WHERE TemplateId = $templateId LIMIT 1;";
            select.Parameters.AddWithValue("$templateId", templateId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            return reader.Read() ? ReadCharacterTemplateRecord(reader) : null;
        }

        private const string SelectColumns =
            "SELECT TemplateId, TemplateScope, OwnerUserId, CampaignId, Name, CharacterKind, RulesetId, RulesetVersion, " +
            "AnatomyProfileRef, SeedJson, Status, Revision, CreatedAt, UpdatedAt";

        private static CharacterTemplateRecord ReadCharacterTemplateRecord(SqliteDataReader reader)
        {
            CharacterTemplateId templateId = CharacterTemplateId.Parse(reader.GetString(0));
            var scope = (TemplateScope)Enum.Parse(typeof(TemplateScope), reader.GetString(1));
            UserId? ownerUserId = reader.IsDBNull(2) ? (UserId?)null : UserId.Parse(reader.GetString(2));
            CampaignId? campaignId = reader.IsDBNull(3) ? (CampaignId?)null : CampaignId.Parse(reader.GetString(3));
            string name = reader.GetString(4);
            var characterKind = (CharacterKind)Enum.Parse(typeof(CharacterKind), reader.GetString(5));
            string rulesetId = reader.GetString(6);
            string rulesetVersion = reader.GetString(7);
            string? anatomyProfileRef = reader.IsDBNull(8) ? null : reader.GetString(8);
            CharacterTemplateSeed seed = DeserializeSeed(reader.GetString(9));
            var status = (CharacterTemplateStatus)Enum.Parse(typeof(CharacterTemplateStatus), reader.GetString(10));
            long revision = reader.GetInt64(11);
            UtcInstant createdAt = UtcInstant.Parse(reader.GetString(12));
            UtcInstant updatedAt = UtcInstant.Parse(reader.GetString(13));

            return new CharacterTemplateRecord(templateId, scope, ownerUserId, campaignId, name, characterKind, rulesetId, rulesetVersion, anatomyProfileRef, seed, status, revision, createdAt, updatedAt);
        }

        internal static string SerializeSeed(CharacterTemplateSeed seed)
        {
            var array = new JArray();
            foreach (CharacterTemplateSeedItem item in seed.Items)
            {
                array.Add(new JObject
                {
                    ["seedItemId"] = item.SeedItemId.ToString(),
                    ["category"] = item.Category,
                    ["name"] = item.Name,
                    ["value"] = item.Value,
                });
            }

            return array.ToString(Newtonsoft.Json.Formatting.None);
        }

        internal static CharacterTemplateSeed DeserializeSeed(string json)
        {
            var array = (JArray)SqliteCharacterRepository.ParseJsonPreservingStrings(json);
            var items = new List<CharacterTemplateSeedItem>(array.Count);
            foreach (JToken token in array)
            {
                TemplateSeedItemId seedItemId = TemplateSeedItemId.Parse((string)token["seedItemId"]!);
                string category = (string)token["category"]!;
                string name = (string)token["name"]!;
                string? value = token["value"]!.Type == JTokenType.Null ? null : (string)token["value"]!;
                items.Add(new CharacterTemplateSeedItem(seedItemId, category, name, value));
            }

            return new CharacterTemplateSeed(items);
        }

        private static SqliteConnection OpenConnection(string rootPath, TemplateScope scope)
        {
            string fileName = scope == TemplateScope.Campaign ? "campaign.db" : "local_profile.db";
            string dbPath = Path.Combine(rootPath, fileName);
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

        internal static void EnsureCharacterTemplateTables(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS CharacterTemplate (
    TemplateId TEXT PRIMARY KEY,
    TemplateScope TEXT NOT NULL,
    OwnerUserId TEXT,
    CampaignId TEXT,
    Name TEXT NOT NULL,
    CharacterKind TEXT NOT NULL,
    RulesetId TEXT NOT NULL,
    RulesetVersion TEXT NOT NULL,
    AnatomyProfileRef TEXT,
    SeedJson TEXT NOT NULL DEFAULT '[]',
    Status TEXT NOT NULL,
    Revision INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    LastCommandId TEXT NOT NULL
);";
            command.ExecuteNonQuery();
        }
    }
}
