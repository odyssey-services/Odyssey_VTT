using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Persistence.Sqlite
{
    /// <summary>
    /// ODY-S05-101: implements <see cref="IContentCatalogRepository"/> --
    /// Content Catalog Foundation only. Follows
    /// <see cref="SqliteCharacterTemplateRepository"/>'s exact structural
    /// precedent for a single-table aggregate with no `DomainEvents`
    /// participation: per-call short-lived <see cref="SqliteConnection"/>,
    /// `EnsureContentDefinitionTables` (`CREATE TABLE IF NOT EXISTS`), a
    /// manual `LastCommandId` idempotency column (no append-only journal --
    /// `11_Content_Block_System`/`ADR-027` do not require catalog
    /// definitions to participate in `DomainEvents`/history the way
    /// `Character` does), and a shared `SelectColumns`/`ReadContentDefinitionRecord`
    /// convention.
    ///
    /// This table is physically stored inside the same `campaign.db` file
    /// every other campaign-scoped repository already uses (there is no
    /// separate global/cross-campaign Ruleset-store mechanism anywhere in
    /// this codebase yet). It carries no `CampaignId` column at all --
    /// `SLICE-05_IMPLEMENTATION_BACKLOG.md` section 3.2's explicit
    /// product-owner MVP scope decision ("base/Ruleset catalog only, no
    /// campaign-specific catalog or overrides") is satisfied at the
    /// data-model level: no column or code path here distinguishes one
    /// campaign's own catalog rows from another's, even though each
    /// campaign's own file physically holds its own copy today. True
    /// cross-campaign Ruleset-catalog sharing would require a future,
    /// separately-scoped storage decision -- recorded honestly, not solved
    /// by this task.
    ///
    /// Stores definitions only. Never creates, reads, or references
    /// `Inventory`, `ItemInstance`, `ItemStack`, equipment runtime state, or
    /// `ActiveEffect` -- confirmed by this file's own complete absence of
    /// any such type or table (`ADR-027` section 4's catalog/runtime
    /// boundary).
    /// </summary>
    public sealed class SqliteContentCatalogRepository : IContentCatalogRepository
    {
        private readonly IWallClock _clock;

        public SqliteContentCatalogRepository(IWallClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public Result<ContentDefinitionRecord> CreateDraftContentDefinition(CreateDraftContentDefinitionRequest request, CommandId commandId, CorrelationId correlationId)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            try
            {
                using SqliteConnection connection = OpenConnection(request.Campaign.RootPath);
                EnsureContentDefinitionTables(connection);
                using SqliteTransaction transaction = connection.BeginTransaction();

                ContentDefinitionRecord? replay = TryFindByCommandId(connection, transaction, commandId);
                if (replay != null)
                {
                    transaction.Commit();
                    return Result<ContentDefinitionRecord>.Success(replay);
                }

                UtcInstant now = _clock.GetUtcNow();
                ContentDefinitionId definitionId = ContentDefinitionId.NewId(now);
                const ContentDefinitionOrigin origin = ContentDefinitionOrigin.RulesetPackage;
                const ContentDefinitionStatus status = ContentDefinitionStatus.Draft;
                const long initialVersion = 0;
                const long initialRevision = 1;

                using (var insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = "INSERT INTO ContentDefinition (" +
                        "ContentDefinitionId, Origin, DefinitionType, Name, Description, Status, Version, Revision, " +
                        "RulesetCompatibilityJson, TagsJson, PropertiesJson, DependencyRefsJson, " +
                        "CreatedByUserId, PublishedByUserId, PublishedAt, ArchivedAt, ArchiveReason, " +
                        "CreatedAt, UpdatedAt, LastCommandId) VALUES (" +
                        "$id, $origin, $definitionType, $name, $description, $status, $version, $revision, " +
                        "$rulesetCompatibilityJson, $tagsJson, $propertiesJson, $dependencyRefsJson, " +
                        "$createdByUserId, NULL, NULL, NULL, NULL, " +
                        "$createdAt, $updatedAt, $lastCommandId);";
                    insert.Parameters.AddWithValue("$id", definitionId.ToString());
                    insert.Parameters.AddWithValue("$origin", origin.ToString());
                    insert.Parameters.AddWithValue("$definitionType", request.DefinitionType.ToString());
                    insert.Parameters.AddWithValue("$name", request.Name);
                    insert.Parameters.AddWithValue("$description", (object?)request.Description ?? DBNull.Value);
                    insert.Parameters.AddWithValue("$status", status.ToString());
                    insert.Parameters.AddWithValue("$version", initialVersion);
                    insert.Parameters.AddWithValue("$revision", initialRevision);
                    insert.Parameters.AddWithValue("$rulesetCompatibilityJson", SerializeStringList(request.RulesetCompatibility));
                    insert.Parameters.AddWithValue("$tagsJson", SerializeStringList(request.Tags));
                    insert.Parameters.AddWithValue("$propertiesJson", request.PropertiesJson);
                    insert.Parameters.AddWithValue("$dependencyRefsJson", SerializeDependencyRefs(request.DependencyRefs));
                    insert.Parameters.AddWithValue("$createdByUserId", request.CreatedByUserId.ToString());
                    insert.Parameters.AddWithValue("$createdAt", now.ToString());
                    insert.Parameters.AddWithValue("$updatedAt", now.ToString());
                    insert.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                    insert.ExecuteNonQuery();
                }

                transaction.Commit();

                var record = new ContentDefinitionRecord(
                    definitionId, origin, request.DefinitionType, request.Name, request.Description, status,
                    initialVersion, initialRevision, request.RulesetCompatibility, request.Tags, request.PropertiesJson,
                    request.DependencyRefs, request.CreatedByUserId, null, null, null, null, now, now);
                return Result<ContentDefinitionRecord>.Success(record);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<ContentDefinitionRecord>.Failure(PersistenceFailures.ContentDefinitionIoFailed(correlationId));
            }
        }

        public Result<ContentDefinitionRecord> UpdateDraftContentDefinition(CampaignHandle campaign, ContentDefinitionId definitionId, string name, string? description, string propertiesJson, long expectedRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!definitionId.IsValid) throw new ArgumentException("ContentDefinitionId is required.", nameof(definitionId));
            if (string.IsNullOrWhiteSpace(name) || name.Length > 128) throw new ArgumentException("Name is not safe.", nameof(name));
            if (propertiesJson == null) throw new ArgumentNullException(nameof(propertiesJson));
            if (expectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureContentDefinitionTables(connection);
                using SqliteTransaction transaction = connection.BeginTransaction();

                ContentDefinitionRecord? replay = TryFindByCommandId(connection, transaction, commandId);
                if (replay != null)
                {
                    transaction.Commit();
                    return Result<ContentDefinitionRecord>.Success(replay);
                }

                ContentDefinitionRecord? current = SelectForUpdate(connection, transaction, definitionId);
                if (current == null)
                {
                    transaction.Commit();
                    return Result<ContentDefinitionRecord>.Failure(PersistenceFailures.ContentDefinitionNotFound(correlationId));
                }

                // ADR-027 section 4.1: Published immutability, enforced here
                // at the foundation level even though the real publish/
                // archive workflow (ODY-S05-103) does not exist yet -- only
                // a Draft may ever be touched by this bare update primitive.
                if (current.Status != ContentDefinitionStatus.Draft)
                {
                    transaction.Commit();
                    return Result<ContentDefinitionRecord>.Failure(PersistenceFailures.ContentDefinitionNotDraft(correlationId));
                }

                if (current.Revision != expectedRevision)
                {
                    transaction.Commit();
                    return Result<ContentDefinitionRecord>.Failure(PersistenceFailures.ContentDefinitionRevisionConflict(correlationId));
                }

                UtcInstant now = _clock.GetUtcNow();
                long newRevision = current.Revision + 1;

                using (var update = connection.CreateCommand())
                {
                    update.Transaction = transaction;
                    update.CommandText = "UPDATE ContentDefinition SET Name = $name, Description = $description, PropertiesJson = $propertiesJson, Revision = $revision, UpdatedAt = $updatedAt, LastCommandId = $lastCommandId WHERE ContentDefinitionId = $id;";
                    update.Parameters.AddWithValue("$name", name);
                    update.Parameters.AddWithValue("$description", (object?)description ?? DBNull.Value);
                    update.Parameters.AddWithValue("$propertiesJson", propertiesJson);
                    update.Parameters.AddWithValue("$revision", newRevision);
                    update.Parameters.AddWithValue("$updatedAt", now.ToString());
                    update.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                    update.Parameters.AddWithValue("$id", definitionId.ToString());
                    update.ExecuteNonQuery();
                }

                transaction.Commit();

                var record = new ContentDefinitionRecord(
                    definitionId, current.Origin, current.DefinitionType, name, description, current.Status,
                    current.Version, newRevision, current.RulesetCompatibility, current.Tags, propertiesJson,
                    current.DependencyRefs, current.CreatedByUserId, current.PublishedByUserId, current.PublishedAt,
                    current.ArchivedAt, current.ArchiveReason, current.CreatedAt, now);
                return Result<ContentDefinitionRecord>.Success(record);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<ContentDefinitionRecord>.Failure(PersistenceFailures.ContentDefinitionIoFailed(correlationId));
            }
        }

        public Result<ContentDefinitionRecord> GetContentDefinition(CampaignHandle campaign, ContentDefinitionId definitionId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!definitionId.IsValid) throw new ArgumentException("ContentDefinitionId is required.", nameof(definitionId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureContentDefinitionTables(connection);

                using var select = connection.CreateCommand();
                select.CommandText = SelectColumns + " FROM ContentDefinition WHERE ContentDefinitionId = $id LIMIT 1;";
                select.Parameters.AddWithValue("$id", definitionId.ToString());
                using SqliteDataReader reader = select.ExecuteReader();
                if (!reader.Read())
                {
                    return Result<ContentDefinitionRecord>.Failure(PersistenceFailures.ContentDefinitionNotFound(correlationId));
                }

                return Result<ContentDefinitionRecord>.Success(ReadContentDefinitionRecord(reader));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<ContentDefinitionRecord>.Failure(PersistenceFailures.ContentDefinitionIoFailed(correlationId));
            }
        }

        public Result<IReadOnlyList<ContentDefinitionRecord>> ListContentDefinitions(CampaignHandle campaign, ContentDefinitionStatus? statusFilter, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureContentDefinitionTables(connection);

                var results = new List<ContentDefinitionRecord>();
                using (var select = connection.CreateCommand())
                {
                    select.CommandText = statusFilter.HasValue
                        ? SelectColumns + " FROM ContentDefinition WHERE Status = $status ORDER BY CreatedAt;"
                        : SelectColumns + " FROM ContentDefinition ORDER BY CreatedAt;";
                    if (statusFilter.HasValue)
                    {
                        select.Parameters.AddWithValue("$status", statusFilter.Value.ToString());
                    }

                    using SqliteDataReader reader = select.ExecuteReader();
                    while (reader.Read())
                    {
                        results.Add(ReadContentDefinitionRecord(reader));
                    }
                }

                return Result<IReadOnlyList<ContentDefinitionRecord>>.Success(results);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<IReadOnlyList<ContentDefinitionRecord>>.Failure(PersistenceFailures.ContentDefinitionIoFailed(correlationId));
            }
        }

        private static ContentDefinitionRecord? TryFindByCommandId(SqliteConnection connection, SqliteTransaction transaction, CommandId commandId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = SelectColumns + " FROM ContentDefinition WHERE LastCommandId = $commandId LIMIT 1;";
            select.Parameters.AddWithValue("$commandId", commandId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            return reader.Read() ? ReadContentDefinitionRecord(reader) : null;
        }

        private static ContentDefinitionRecord? SelectForUpdate(SqliteConnection connection, SqliteTransaction transaction, ContentDefinitionId definitionId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = SelectColumns + " FROM ContentDefinition WHERE ContentDefinitionId = $id LIMIT 1;";
            select.Parameters.AddWithValue("$id", definitionId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            return reader.Read() ? ReadContentDefinitionRecord(reader) : null;
        }

        private const string SelectColumns =
            "SELECT ContentDefinitionId, Origin, DefinitionType, Name, Description, Status, Version, Revision, " +
            "RulesetCompatibilityJson, TagsJson, PropertiesJson, DependencyRefsJson, " +
            "CreatedByUserId, PublishedByUserId, PublishedAt, ArchivedAt, ArchiveReason, CreatedAt, UpdatedAt";

        private static ContentDefinitionRecord ReadContentDefinitionRecord(SqliteDataReader reader)
        {
            ContentDefinitionId id = ContentDefinitionId.Parse(reader.GetString(0));
            var origin = (ContentDefinitionOrigin)Enum.Parse(typeof(ContentDefinitionOrigin), reader.GetString(1));
            var definitionType = (ContentDefinitionType)Enum.Parse(typeof(ContentDefinitionType), reader.GetString(2));
            string name = reader.GetString(3);
            string? description = reader.IsDBNull(4) ? null : reader.GetString(4);
            var status = (ContentDefinitionStatus)Enum.Parse(typeof(ContentDefinitionStatus), reader.GetString(5));
            long version = reader.GetInt64(6);
            long revision = reader.GetInt64(7);
            IReadOnlyList<string> rulesetCompatibility = DeserializeStringList(reader.GetString(8));
            IReadOnlyList<string> tags = DeserializeStringList(reader.GetString(9));
            string propertiesJson = reader.GetString(10);
            IReadOnlyList<ContentDefinitionRef> dependencyRefs = DeserializeDependencyRefs(reader.GetString(11));
            UserId createdByUserId = UserId.Parse(reader.GetString(12));
            UserId? publishedByUserId = reader.IsDBNull(13) ? (UserId?)null : UserId.Parse(reader.GetString(13));
            UtcInstant? publishedAt = reader.IsDBNull(14) ? (UtcInstant?)null : UtcInstant.Parse(reader.GetString(14));
            UtcInstant? archivedAt = reader.IsDBNull(15) ? (UtcInstant?)null : UtcInstant.Parse(reader.GetString(15));
            string? archiveReason = reader.IsDBNull(16) ? null : reader.GetString(16);
            UtcInstant createdAt = UtcInstant.Parse(reader.GetString(17));
            UtcInstant updatedAt = UtcInstant.Parse(reader.GetString(18));

            return new ContentDefinitionRecord(
                id, origin, definitionType, name, description, status, version, revision,
                rulesetCompatibility, tags, propertiesJson, dependencyRefs,
                createdByUserId, publishedByUserId, publishedAt, archivedAt, archiveReason, createdAt, updatedAt);
        }

        internal static string SerializeStringList(IReadOnlyList<string> values)
        {
            var array = new JArray();
            foreach (string value in values) array.Add(value);
            return array.ToString(Newtonsoft.Json.Formatting.None);
        }

        internal static IReadOnlyList<string> DeserializeStringList(string json)
        {
            var array = JArray.Parse(json);
            var values = new List<string>(array.Count);
            foreach (JToken token in array) values.Add((string)token!);
            return values;
        }

        internal static string SerializeDependencyRefs(IReadOnlyList<ContentDefinitionRef> refs)
        {
            var array = new JArray();
            foreach (ContentDefinitionRef reference in refs) array.Add(reference.ToString());
            return array.ToString(Newtonsoft.Json.Formatting.None);
        }

        internal static IReadOnlyList<ContentDefinitionRef> DeserializeDependencyRefs(string json)
        {
            var array = JArray.Parse(json);
            var refs = new List<ContentDefinitionRef>(array.Count);
            foreach (JToken token in array) refs.Add(ContentDefinitionRef.Parse((string)token!));
            return refs;
        }

        private static SqliteConnection OpenConnection(string rootPath)
        {
            string dbPath = Path.Combine(rootPath, "campaign.db");
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

        internal static void EnsureContentDefinitionTables(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS ContentDefinition (
    ContentDefinitionId TEXT PRIMARY KEY,
    Origin TEXT NOT NULL,
    DefinitionType TEXT NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT,
    Status TEXT NOT NULL,
    Version INTEGER NOT NULL,
    Revision INTEGER NOT NULL,
    RulesetCompatibilityJson TEXT NOT NULL DEFAULT '[]',
    TagsJson TEXT NOT NULL DEFAULT '[]',
    PropertiesJson TEXT NOT NULL DEFAULT '{}',
    DependencyRefsJson TEXT NOT NULL DEFAULT '[]',
    CreatedByUserId TEXT NOT NULL,
    PublishedByUserId TEXT,
    PublishedAt TEXT,
    ArchivedAt TEXT,
    ArchiveReason TEXT,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    LastCommandId TEXT NOT NULL
);";
            command.ExecuteNonQuery();
        }
    }
}
