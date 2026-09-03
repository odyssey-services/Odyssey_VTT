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
    /// `EnsureContentDefinitionTables` (`CREATE TABLE IF NOT EXISTS`), and a
    /// shared `SelectColumns`/`ReadContentDefinitionRecord` convention (no
    /// append-only journal -- `11_Content_Block_System`/`ADR-027` do not
    /// require catalog definitions to participate in `DomainEvents`/history
    /// the way `Character` does).
    ///
    /// Idempotency: a durable <c>ContentDefinitionCommandLedger</c> table
    /// (<c>CommandId</c> primary key -&gt; <c>ContentDefinitionId</c>),
    /// written in the same transaction as every create/update, is the sole
    /// source of replay truth -- deliberately NOT a `LastCommandId` column
    /// on the `ContentDefinition` row itself. A single mutable
    /// `LastCommandId` column would be overwritten by every later
    /// create/update on the same row, so replaying an *older* command after
    /// a *newer* one already changed that column would stop being
    /// recognized as a replay: `CreateDraftContentDefinition` would mint a
    /// genuine duplicate row, and `UpdateDraftContentDefinition` would
    /// either double-apply an already-applied mutation or fail with a false
    /// stale-revision conflict. The ledger's own `CommandId` primary key
    /// means every command this repository has ever successfully applied
    /// stays independently replayable, in any order, for the row's entire
    /// lifetime -- not just against whichever command happened to write
    /// last.
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
                        "CreatedAt, UpdatedAt) VALUES (" +
                        "$id, $origin, $definitionType, $name, $description, $status, $version, $revision, " +
                        "$rulesetCompatibilityJson, $tagsJson, $propertiesJson, $dependencyRefsJson, " +
                        "$createdByUserId, NULL, NULL, NULL, NULL, " +
                        "$createdAt, $updatedAt);";
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
                    insert.ExecuteNonQuery();
                }

                InsertCommandLedgerEntry(connection, transaction, commandId, definitionId, now);

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
                    update.CommandText = "UPDATE ContentDefinition SET Name = $name, Description = $description, PropertiesJson = $propertiesJson, Revision = $revision, UpdatedAt = $updatedAt WHERE ContentDefinitionId = $id;";
                    update.Parameters.AddWithValue("$name", name);
                    update.Parameters.AddWithValue("$description", (object?)description ?? DBNull.Value);
                    update.Parameters.AddWithValue("$propertiesJson", propertiesJson);
                    update.Parameters.AddWithValue("$revision", newRevision);
                    update.Parameters.AddWithValue("$updatedAt", now.ToString());
                    update.Parameters.AddWithValue("$id", definitionId.ToString());
                    update.ExecuteNonQuery();
                }

                InsertCommandLedgerEntry(connection, transaction, commandId, definitionId, now);

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

        /// <summary>
        /// ODY-S05-102: creates a new Draft (fresh <see cref="ContentDefinitionId"/>,
        /// `Status=Draft`, `Version=0`, `Revision=1`) by copying the exact
        /// current fields of an already-Published source definition. The
        /// source row is only ever read, never written -- `ADR-027` section
        /// 4.1's Published-immutability rule cannot be violated by this
        /// method by construction. Uses the same `ContentDefinitionCommandLedger`
        /// idempotency mechanism as <see cref="CreateDraftContentDefinition"/>/
        /// <see cref="UpdateDraftContentDefinition"/> -- a replay of this
        /// command returns the already-created Draft, never mints a second
        /// one.
        /// </summary>
        public Result<ContentDefinitionRecord> CreateNextDraftVersionFromPublished(CampaignHandle campaign, ContentDefinitionId publishedDefinitionId, UserId createdByUserId, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!publishedDefinitionId.IsValid) throw new ArgumentException("PublishedDefinitionId is required.", nameof(publishedDefinitionId));
            if (!createdByUserId.IsValid) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
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

                ContentDefinitionRecord? source = SelectForUpdate(connection, transaction, publishedDefinitionId);
                if (source == null)
                {
                    transaction.Commit();
                    return Result<ContentDefinitionRecord>.Failure(PersistenceFailures.ContentDefinitionNotFound(correlationId));
                }

                if (source.Status != ContentDefinitionStatus.Published)
                {
                    transaction.Commit();
                    return Result<ContentDefinitionRecord>.Failure(PersistenceFailures.ContentDefinitionNotPublished(correlationId));
                }

                UtcInstant now = _clock.GetUtcNow();
                ContentDefinitionId newDefinitionId = ContentDefinitionId.NewId(now);
                const ContentDefinitionOrigin origin = ContentDefinitionOrigin.RulesetPackage;
                const ContentDefinitionStatus newStatus = ContentDefinitionStatus.Draft;
                const long newVersion = 0;
                const long newRevision = 1;

                using (var insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = "INSERT INTO ContentDefinition (" +
                        "ContentDefinitionId, Origin, DefinitionType, Name, Description, Status, Version, Revision, " +
                        "RulesetCompatibilityJson, TagsJson, PropertiesJson, DependencyRefsJson, " +
                        "CreatedByUserId, PublishedByUserId, PublishedAt, ArchivedAt, ArchiveReason, " +
                        "CreatedAt, UpdatedAt) VALUES (" +
                        "$id, $origin, $definitionType, $name, $description, $status, $version, $revision, " +
                        "$rulesetCompatibilityJson, $tagsJson, $propertiesJson, $dependencyRefsJson, " +
                        "$createdByUserId, NULL, NULL, NULL, NULL, " +
                        "$createdAt, $updatedAt);";
                    insert.Parameters.AddWithValue("$id", newDefinitionId.ToString());
                    insert.Parameters.AddWithValue("$origin", origin.ToString());
                    insert.Parameters.AddWithValue("$definitionType", source.DefinitionType.ToString());
                    insert.Parameters.AddWithValue("$name", source.Name);
                    insert.Parameters.AddWithValue("$description", (object?)source.Description ?? DBNull.Value);
                    insert.Parameters.AddWithValue("$status", newStatus.ToString());
                    insert.Parameters.AddWithValue("$version", newVersion);
                    insert.Parameters.AddWithValue("$revision", newRevision);
                    insert.Parameters.AddWithValue("$rulesetCompatibilityJson", SerializeStringList(source.RulesetCompatibility));
                    insert.Parameters.AddWithValue("$tagsJson", SerializeStringList(source.Tags));
                    insert.Parameters.AddWithValue("$propertiesJson", source.PropertiesJson);
                    insert.Parameters.AddWithValue("$dependencyRefsJson", SerializeDependencyRefs(source.DependencyRefs));
                    insert.Parameters.AddWithValue("$createdByUserId", createdByUserId.ToString());
                    insert.Parameters.AddWithValue("$createdAt", now.ToString());
                    insert.Parameters.AddWithValue("$updatedAt", now.ToString());
                    insert.ExecuteNonQuery();
                }

                InsertCommandLedgerEntry(connection, transaction, commandId, newDefinitionId, now);

                transaction.Commit();

                var record = new ContentDefinitionRecord(
                    newDefinitionId, origin, source.DefinitionType, source.Name, source.Description, newStatus,
                    newVersion, newRevision, source.RulesetCompatibility, source.Tags, source.PropertiesJson,
                    source.DependencyRefs, createdByUserId, null, null, null, null, now, now);
                return Result<ContentDefinitionRecord>.Success(record);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<ContentDefinitionRecord>.Failure(PersistenceFailures.ContentDefinitionIoFailed(correlationId));
            }
        }

        /// <summary>
        /// ODY-S05-101 amendment: looks up the durable
        /// `ContentDefinitionCommandLedger` (`CommandId` -&gt;
        /// `ContentDefinitionId`), not a mutable `LastCommandId` column on
        /// the `ContentDefinition` row -- a column would be overwritten by
        /// every later create/update on the same row, silently breaking
        /// replay of an older command once a newer one has touched that
        /// row. The ledger's own `CommandId` primary key means every
        /// command this repository has ever successfully applied stays
        /// independently replayable for the row's entire lifetime. Used
        /// identically by both <see cref="CreateDraftContentDefinition"/>
        /// and <see cref="UpdateDraftContentDefinition"/> -- a replay
        /// always returns the definition's own *current* state, never
        /// re-running the original mutation.
        /// </summary>
        private static ContentDefinitionRecord? TryFindByCommandId(SqliteConnection connection, SqliteTransaction transaction, CommandId commandId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT ContentDefinitionId FROM ContentDefinitionCommandLedger WHERE CommandId = $commandId LIMIT 1;";
            select.Parameters.AddWithValue("$commandId", commandId.ToString());
            object? result = select.ExecuteScalar();
            if (result == null || result is DBNull) return null;

            ContentDefinitionId definitionId = ContentDefinitionId.Parse((string)result);
            return SelectForUpdate(connection, transaction, definitionId);
        }

        /// <summary>
        /// ODY-S05-101 amendment: records this <paramref name="commandId"/>
        /// as durably applied against <paramref name="definitionId"/>, in
        /// the same transaction as the create/update it accompanies.
        /// `CommandId` is the ledger's own primary key, so a genuine
        /// attempt to reuse the same `CommandId` for a materially different
        /// mutation (a bug, not a legitimate replay) fails loudly at the
        /// database level rather than silently overwriting a prior mapping.
        /// </summary>
        private static void InsertCommandLedgerEntry(SqliteConnection connection, SqliteTransaction transaction, CommandId commandId, ContentDefinitionId definitionId, UtcInstant now)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO ContentDefinitionCommandLedger (CommandId, ContentDefinitionId, CreatedAt) VALUES ($commandId, $definitionId, $createdAt);";
            insert.Parameters.AddWithValue("$commandId", commandId.ToString());
            insert.Parameters.AddWithValue("$definitionId", definitionId.ToString());
            insert.Parameters.AddWithValue("$createdAt", now.ToString());
            insert.ExecuteNonQuery();
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
    UpdatedAt TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS ContentDefinitionCommandLedger (
    CommandId TEXT PRIMARY KEY,
    ContentDefinitionId TEXT NOT NULL,
    CreatedAt TEXT NOT NULL
);";
            command.ExecuteNonQuery();
        }
    }
}
