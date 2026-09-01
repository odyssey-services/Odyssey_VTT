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
    /// ODY-S04-103: implements <see cref="ILocalCharacterDraftRepository"/>.
    /// ADR-023 section 4.1: a local Draft is not an ADR-022 Character
    /// aggregate instance and does not participate in
    /// <c>DomainEvents</c>/<c>CharacterHistoryProjection</c> -- this
    /// repository stores it as an ordinary row in <c>local_profile.db</c>,
    /// the same personal-profile storage boundary a
    /// <see cref="TemplateScope.Personal"/> <c>CharacterTemplate</c> uses
    /// (<see cref="SqliteCharacterTemplateRepository"/>), with no event
    /// journal and no <c>SqliteSavingPipeline</c> involvement.
    ///
    /// If <see cref="CreateLocalCharacterDraftRequest.PersonalTemplateId"/> is
    /// set, this method performs ADR-023 section 5.3's deep copy right here,
    /// reading the Personal template from the same connection/file and
    /// minting fresh identifiers via <see cref="CharacterTemplateSeedCopier"/>
    /// -- once, at Draft-creation time. <c>BindDraftToCampaign</c>
    /// (<see cref="SqliteCharacterRepository"/>) later carries this already-
    /// copied result through unchanged; it never re-copies it.
    /// </summary>
    public sealed class SqliteLocalCharacterDraftRepository : ILocalCharacterDraftRepository
    {
        private readonly IWallClock _clock;

        public SqliteLocalCharacterDraftRepository(IWallClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public Result<LocalCharacterDraftRecord> CreateLocalCharacterDraft(LocalProfileHandle profile, CreateLocalCharacterDraftRequest request, CommandId commandId, CorrelationId correlationId)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            try
            {
                using SqliteConnection connection = OpenConnection(profile.RootPath);
                EnsureLocalCharacterDraftTables(connection);
                SqliteCharacterTemplateRepository.EnsureCharacterTemplateTables(connection);
                using SqliteTransaction transaction = connection.BeginTransaction();

                LocalCharacterDraftRecord? replay = TryFindByCommandId(connection, transaction, commandId);
                if (replay != null)
                {
                    transaction.Commit();
                    return Result<LocalCharacterDraftRecord>.Success(replay);
                }

                CharacterTemplateId? templateId = null;
                long? templateVersionAtCopyTime = null;
                IReadOnlyList<CopiedCharacterSeedItem> seedCopy = Array.Empty<CopiedCharacterSeedItem>();
                UtcInstant now = _clock.GetUtcNow();

                if (request.PersonalTemplateId.HasValue)
                {
                    using var selectTemplate = connection.CreateCommand();
                    selectTemplate.Transaction = transaction;
                    selectTemplate.CommandText = "SELECT TemplateScope, OwnerUserId, SeedJson, Revision FROM CharacterTemplate WHERE TemplateId = $templateId LIMIT 1;";
                    selectTemplate.Parameters.AddWithValue("$templateId", request.PersonalTemplateId.Value.ToString());
                    using SqliteDataReader templateReader = selectTemplate.ExecuteReader();
                    if (!templateReader.Read())
                    {
                        transaction.Commit();
                        return Result<LocalCharacterDraftRecord>.Failure(PersistenceFailures.CharacterTemplateNotFound(correlationId));
                    }

                    string scopeText = templateReader.GetString(0);
                    UserId? ownerUserId = templateReader.IsDBNull(1) ? (UserId?)null : UserId.Parse(templateReader.GetString(1));
                    string seedJson = templateReader.GetString(2);
                    long revision = templateReader.GetInt64(3);

                    if (!string.Equals(scopeText, TemplateScope.Personal.ToString(), StringComparison.Ordinal)
                        || !ownerUserId.HasValue || !ownerUserId.Value.Equals(profile.OwnerUserId))
                    {
                        // A Draft may only copy from a Personal template it
                        // actually owns -- a Campaign template is applied at
                        // BindDraftToCampaign instead (ADR-023 section 5.3).
                        transaction.Commit();
                        return Result<LocalCharacterDraftRecord>.Failure(PersistenceFailures.CharacterTemplateNotFound(correlationId));
                    }

                    CharacterTemplateSeed rawSeed = SqliteCharacterTemplateRepository.DeserializeSeed(seedJson);
                    templateId = request.PersonalTemplateId.Value;
                    templateVersionAtCopyTime = revision;
                    seedCopy = CharacterTemplateSeedCopier.CopyWithFreshIdentifiers(rawSeed, templateId.Value, now);
                }

                LocalCharacterDraftId draftId = LocalCharacterDraftId.NewId(now);

                using (var insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = "INSERT INTO LocalCharacterDraft (" +
                        "DraftId, OwnerUserId, CharacterKind, Name, AnatomyProfileRef, TemplateId, TemplateVersionAtCopyTime, SeedCopyJson, CreatedAt, LastCommandId) VALUES (" +
                        "$draftId, $ownerUserId, $characterKind, $name, $anatomyProfileRef, $templateId, $templateVersion, $seedCopyJson, $createdAt, $lastCommandId);";
                    insert.Parameters.AddWithValue("$draftId", draftId.ToString());
                    insert.Parameters.AddWithValue("$ownerUserId", profile.OwnerUserId.ToString());
                    insert.Parameters.AddWithValue("$characterKind", request.CharacterKind.ToString());
                    insert.Parameters.AddWithValue("$name", request.Name);
                    insert.Parameters.AddWithValue("$anatomyProfileRef", request.AnatomyProfileRef);
                    insert.Parameters.AddWithValue("$templateId", (object?)templateId?.ToString() ?? DBNull.Value);
                    insert.Parameters.AddWithValue("$templateVersion", (object?)templateVersionAtCopyTime ?? DBNull.Value);
                    insert.Parameters.AddWithValue("$seedCopyJson", SerializeSeedCopy(seedCopy));
                    insert.Parameters.AddWithValue("$createdAt", now.ToString());
                    insert.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                    insert.ExecuteNonQuery();
                }

                transaction.Commit();

                var record = new LocalCharacterDraftRecord(draftId, profile.OwnerUserId, request.CharacterKind, request.Name, request.AnatomyProfileRef, templateId, templateVersionAtCopyTime, seedCopy, now);
                return Result<LocalCharacterDraftRecord>.Success(record);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<LocalCharacterDraftRecord>.Failure(PersistenceFailures.LocalCharacterDraftIoFailed(correlationId));
            }
        }

        public Result<LocalCharacterDraftRecord> GetLocalCharacterDraft(LocalProfileHandle profile, LocalCharacterDraftId draftId, CorrelationId correlationId)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!draftId.IsValid) throw new ArgumentException("DraftId is required.", nameof(draftId));

            try
            {
                using SqliteConnection connection = OpenConnection(profile.RootPath);
                EnsureLocalCharacterDraftTables(connection);

                using var select = connection.CreateCommand();
                select.CommandText = SelectColumns + " FROM LocalCharacterDraft WHERE DraftId = $draftId LIMIT 1;";
                select.Parameters.AddWithValue("$draftId", draftId.ToString());
                using SqliteDataReader reader = select.ExecuteReader();
                if (!reader.Read())
                {
                    return Result<LocalCharacterDraftRecord>.Failure(PersistenceFailures.LocalCharacterDraftNotFound(correlationId));
                }

                return Result<LocalCharacterDraftRecord>.Success(ReadLocalCharacterDraftRecord(reader));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<LocalCharacterDraftRecord>.Failure(PersistenceFailures.LocalCharacterDraftIoFailed(correlationId));
            }
        }

        private static LocalCharacterDraftRecord? TryFindByCommandId(SqliteConnection connection, SqliteTransaction transaction, CommandId commandId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = SelectColumns + " FROM LocalCharacterDraft WHERE LastCommandId = $commandId LIMIT 1;";
            select.Parameters.AddWithValue("$commandId", commandId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            return reader.Read() ? ReadLocalCharacterDraftRecord(reader) : null;
        }

        private const string SelectColumns =
            "SELECT DraftId, OwnerUserId, CharacterKind, Name, AnatomyProfileRef, TemplateId, TemplateVersionAtCopyTime, SeedCopyJson, CreatedAt";

        private static LocalCharacterDraftRecord ReadLocalCharacterDraftRecord(SqliteDataReader reader)
        {
            LocalCharacterDraftId draftId = LocalCharacterDraftId.Parse(reader.GetString(0));
            UserId ownerUserId = UserId.Parse(reader.GetString(1));
            var characterKind = (CharacterKind)Enum.Parse(typeof(CharacterKind), reader.GetString(2));
            string name = reader.GetString(3);
            string anatomyProfileRef = reader.GetString(4);
            CharacterTemplateId? templateId = reader.IsDBNull(5) ? (CharacterTemplateId?)null : CharacterTemplateId.Parse(reader.GetString(5));
            long? templateVersionAtCopyTime = reader.IsDBNull(6) ? (long?)null : reader.GetInt64(6);
            IReadOnlyList<CopiedCharacterSeedItem> seedCopy = DeserializeSeedCopy(reader.GetString(7));
            UtcInstant createdAt = UtcInstant.Parse(reader.GetString(8));

            return new LocalCharacterDraftRecord(draftId, ownerUserId, characterKind, name, anatomyProfileRef, templateId, templateVersionAtCopyTime, seedCopy, createdAt);
        }

        internal static string SerializeSeedCopy(IReadOnlyList<CopiedCharacterSeedItem> items)
        {
            var array = new JArray();
            foreach (CopiedCharacterSeedItem item in items)
            {
                array.Add(new JObject
                {
                    ["newSeedItemId"] = item.NewSeedItemId.ToString(),
                    ["sourceTemplateId"] = item.SourceTemplateId?.ToString(),
                    ["sourceSeedItemId"] = item.SourceSeedItemId.ToString(),
                    ["category"] = item.Category,
                    ["name"] = item.Name,
                    ["value"] = item.Value,
                });
            }

            return array.ToString(Newtonsoft.Json.Formatting.None);
        }

        internal static IReadOnlyList<CopiedCharacterSeedItem> DeserializeSeedCopy(string json)
        {
            var array = (JArray)SqliteCharacterRepository.ParseJsonPreservingStrings(json);
            var items = new List<CopiedCharacterSeedItem>(array.Count);
            foreach (JToken token in array)
            {
                TemplateSeedItemId newSeedItemId = TemplateSeedItemId.Parse((string)token["newSeedItemId"]!);
                CharacterTemplateId? sourceTemplateId = token["sourceTemplateId"]!.Type == JTokenType.Null ? (CharacterTemplateId?)null : CharacterTemplateId.Parse((string)token["sourceTemplateId"]!);
                TemplateSeedItemId sourceSeedItemId = TemplateSeedItemId.Parse((string)token["sourceSeedItemId"]!);
                string category = (string)token["category"]!;
                string name = (string)token["name"]!;
                string? value = token["value"]!.Type == JTokenType.Null ? null : (string)token["value"]!;
                items.Add(new CopiedCharacterSeedItem(newSeedItemId, sourceTemplateId, sourceSeedItemId, category, name, value));
            }

            return items;
        }

        private static SqliteConnection OpenConnection(string rootPath)
        {
            string dbPath = Path.Combine(rootPath, "local_profile.db");
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

        private static void EnsureLocalCharacterDraftTables(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS LocalCharacterDraft (
    DraftId TEXT PRIMARY KEY,
    OwnerUserId TEXT NOT NULL,
    CharacterKind TEXT NOT NULL,
    Name TEXT NOT NULL,
    AnatomyProfileRef TEXT NOT NULL,
    TemplateId TEXT,
    TemplateVersionAtCopyTime INTEGER,
    SeedCopyJson TEXT NOT NULL DEFAULT '[]',
    CreatedAt TEXT NOT NULL,
    LastCommandId TEXT NOT NULL
);";
            command.ExecuteNonQuery();
        }
    }
}
