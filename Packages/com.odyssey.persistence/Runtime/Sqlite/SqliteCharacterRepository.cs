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
    /// ODY-S04-101 implementation of <see cref="ICharacterRepository"/>. Mirrors
    /// <see cref="SqliteSceneRepository"/>'s exact shape -- each method opens
    /// its own short-lived connection under the ADR-011 section 7.1 PRAGMA
    /// profile, every mutating method commits through the shared
    /// <see cref="SqliteSavingPipeline"/> (current-state row + DomainEvent +
    /// AppliedCommands in one ADR-012 section 5 transaction).
    ///
    /// ADR-022 section 5's twelve section revisions are all real columns on
    /// the single <c>Character</c> row from creation onward (see
    /// <c>EnsureCharacterTables</c>) -- this task's own commands
    /// (<see cref="UpdateIdentity"/>/<see cref="UpdatePresentation"/>) only
    /// ever touch <c>IdentityRevision</c>/<c>PresentationRevision</c> plus the
    /// overall <c>CharacterRevision</c>; every other section revision is
    /// reserved, unused schema for later tasks (ODY-S04-102/105-111).
    ///
    /// <see cref="GetCharacterHistory"/> deliberately does not read from any
    /// dedicated, separately-maintained history table -- there is none. It
    /// reads only the shared, already-existing <c>DomainEvents</c> table
    /// (ADR-012) and rebuilds entries purely from event payloads, proving
    /// ADR-022 section 8's "projection, not a second source of truth"
    /// contract for real, not merely by declared intent.
    /// </summary>
    public sealed class SqliteCharacterRepository : ICharacterRepository
    {
        private readonly IWallClock _clock;
        private readonly SqliteSavingPipeline _pipeline;

        private static readonly string[] HistoryEventTypes =
        {
            "odyssey.persistence.character_created",
            "odyssey.persistence.character_identity_updated",
            "odyssey.persistence.character_presentation_updated",
        };

        public SqliteCharacterRepository(IWallClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _pipeline = new SqliteSavingPipeline(clock);
        }

        public Result<CharacterRecord> CreateCharacter(CreateCharacterRequest request, CommandId commandId, CorrelationId correlationId)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            CampaignHandle campaign = request.Campaign;

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureCharacterTables(connection);
                UtcInstant now = _clock.GetUtcNow();

                return _pipeline.Execute(
                    connection,
                    campaign.CampaignId,
                    commandId,
                    correlationId,
                    tryReplay: transaction => ReplayCharacter(connection, transaction, campaign.CampaignId, "LastCommandId = $commandId", commandId, correlationId),
                    apply: transaction =>
                    {
                        CharacterId characterId = CharacterId.NewId(now);
                        CharacterSectionRevisions revisions = CharacterSectionRevisions.Initial();
                        const CharacterLifecycleStatus lifecycleStatus = CharacterLifecycleStatus.Draft;
                        const CharacterApprovalState approvalState = CharacterApprovalState.Draft;

                        using (var insert = connection.CreateCommand())
                        {
                            insert.Transaction = transaction;
                            insert.CommandText = "INSERT INTO Character (" +
                                "CharacterId, CampaignId, CharacterKind, LifecycleStatus, ApprovalState, DisplayName, PortraitReference, " +
                                "CharacterRevision, IdentityRevision, PresentationRevision, CustomFieldsRevision, MechanicsRevision, " +
                                "AttributeValuesRevision, CharacterSkillsRevision, CharacterAbilitiesRevision, CharacterResourcesRevision, " +
                                "CharacterAnatomyRevision, OwnershipRevision, LifecycleRevision, RuntimeStateRevision, " +
                                "CreatedAt, UpdatedAt, LastCommandId) VALUES (" +
                                "$characterId, $campaignId, $characterKind, $lifecycleStatus, $approvalState, $displayName, NULL, " +
                                "$characterRevision, $identityRevision, $presentationRevision, $customFieldsRevision, $mechanicsRevision, " +
                                "$attributeValuesRevision, $characterSkillsRevision, $characterAbilitiesRevision, $characterResourcesRevision, " +
                                "$characterAnatomyRevision, $ownershipRevision, $lifecycleRevision, $runtimeStateRevision, " +
                                "$createdAt, $updatedAt, $lastCommandId);";
                            insert.Parameters.AddWithValue("$characterId", characterId.ToString());
                            insert.Parameters.AddWithValue("$campaignId", campaign.CampaignId.ToString());
                            insert.Parameters.AddWithValue("$characterKind", request.CharacterKind.ToString());
                            insert.Parameters.AddWithValue("$lifecycleStatus", lifecycleStatus.ToString());
                            insert.Parameters.AddWithValue("$approvalState", approvalState.ToString());
                            insert.Parameters.AddWithValue("$displayName", request.DisplayName);
                            AddRevisionParameters(insert, revisions);
                            insert.Parameters.AddWithValue("$createdAt", now.ToString());
                            insert.Parameters.AddWithValue("$updatedAt", now.ToString());
                            insert.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            insert.ExecuteNonQuery();
                        }

                        var record = new CharacterRecord(characterId, campaign.CampaignId, request.CharacterKind, lifecycleStatus, approvalState, request.DisplayName, null, revisions, now, now);

                        var payload = new JObject
                        {
                            ["characterId"] = characterId.ToString(),
                            ["campaignId"] = campaign.CampaignId.ToString(),
                            ["characterKind"] = request.CharacterKind.ToString(),
                            ["displayNameSnapshot"] = request.DisplayName,
                            ["newCharacterRevision"] = revisions.CharacterRevision,
                        };

                        return Result<PipelineWrite<CharacterRecord>>.Success(new PipelineWrite<CharacterRecord>(
                            record, "odyssey.persistence.character_created", payload.ToString(Newtonsoft.Json.Formatting.None), characterId.ToString(),
                            aggregateType: "character", aggregateId: characterId.ToString(), aggregateRevision: revisions.CharacterRevision));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterIoFailed(correlationId));
            }
        }

        public Result<CharacterRecord> GetCharacter(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureCharacterTables(connection);

                using var select = connection.CreateCommand();
                select.CommandText = SelectColumns + " FROM Character WHERE CharacterId = $characterId LIMIT 1;";
                select.Parameters.AddWithValue("$characterId", characterId.ToString());
                using SqliteDataReader reader = select.ExecuteReader();
                if (!reader.Read())
                {
                    return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterNotFound(correlationId));
                }

                return Result<CharacterRecord>.Success(ReadCharacterRecord(reader));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterIoFailed(correlationId));
            }
        }

        public Result<CharacterRecord> UpdateIdentity(CampaignHandle campaign, CharacterId characterId, string newDisplayName, long expectedIdentityRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (string.IsNullOrWhiteSpace(newDisplayName) || newDisplayName.Length > 128) throw new ArgumentException("DisplayName is not safe.", nameof(newDisplayName));
            if (expectedIdentityRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedIdentityRevision));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureCharacterTables(connection);

                return _pipeline.Execute(
                    connection,
                    campaign.CampaignId,
                    commandId,
                    correlationId,
                    tryReplay: transaction => ReplayCharacter(connection, transaction, campaign.CampaignId, "CharacterId = $characterId AND LastCommandId = $commandId", commandId, correlationId, characterId),
                    apply: transaction =>
                    {
                        CharacterRecord? current = SelectForUpdate(connection, transaction, characterId);
                        if (current == null)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(PersistenceFailures.CharacterNotFound(correlationId));
                        }

                        // ADR-022 section 5: only the Identity section's own
                        // revision gates this command -- a concurrent, already
                        // committed Presentation edit (different section) is
                        // never checked here and never blocks this one.
                        if (current.Revisions.IdentityRevision != expectedIdentityRevision)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(PersistenceFailures.CharacterRevisionConflict(correlationId));
                        }

                        UtcInstant now = _clock.GetUtcNow();
                        long newIdentityRevision = current.Revisions.IdentityRevision + 1;
                        long newCharacterRevision = current.Revisions.CharacterRevision + 1;
                        string previousDisplayName = current.DisplayName;

                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText = "UPDATE Character SET DisplayName = $displayName, IdentityRevision = $identityRevision, CharacterRevision = $characterRevision, UpdatedAt = $updatedAt, LastCommandId = $lastCommandId WHERE CharacterId = $characterId;";
                            update.Parameters.AddWithValue("$displayName", newDisplayName);
                            update.Parameters.AddWithValue("$identityRevision", newIdentityRevision);
                            update.Parameters.AddWithValue("$characterRevision", newCharacterRevision);
                            update.Parameters.AddWithValue("$updatedAt", now.ToString());
                            update.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            update.Parameters.AddWithValue("$characterId", characterId.ToString());
                            update.ExecuteNonQuery();
                        }

                        CharacterSectionRevisions newRevisions = WithIdentityRevision(current.Revisions, newIdentityRevision, newCharacterRevision);
                        var record = new CharacterRecord(characterId, campaign.CampaignId, current.CharacterKind, current.LifecycleStatus, current.ApprovalState, newDisplayName, current.PortraitReference, newRevisions, current.CreatedAt, now);

                        var payload = new JObject
                        {
                            ["characterId"] = characterId.ToString(),
                            ["displayNameSnapshot"] = newDisplayName,
                            ["previousDisplayNameSnapshot"] = previousDisplayName,
                            ["newIdentityRevision"] = newIdentityRevision,
                            ["newCharacterRevision"] = newCharacterRevision,
                        };

                        return Result<PipelineWrite<CharacterRecord>>.Success(new PipelineWrite<CharacterRecord>(
                            record, "odyssey.persistence.character_identity_updated", payload.ToString(Newtonsoft.Json.Formatting.None), characterId.ToString(),
                            aggregateType: "character", aggregateId: characterId.ToString(), aggregateRevision: newCharacterRevision));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterIoFailed(correlationId));
            }
        }

        public Result<CharacterRecord> UpdatePresentation(CampaignHandle campaign, CharacterId characterId, string? portraitReference, long expectedPresentationRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (expectedPresentationRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedPresentationRevision));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureCharacterTables(connection);

                return _pipeline.Execute(
                    connection,
                    campaign.CampaignId,
                    commandId,
                    correlationId,
                    tryReplay: transaction => ReplayCharacter(connection, transaction, campaign.CampaignId, "CharacterId = $characterId AND LastCommandId = $commandId", commandId, correlationId, characterId),
                    apply: transaction =>
                    {
                        CharacterRecord? current = SelectForUpdate(connection, transaction, characterId);
                        if (current == null)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(PersistenceFailures.CharacterNotFound(correlationId));
                        }

                        // ADR-022 section 5: only the Presentation section's
                        // own revision gates this command -- a concurrent,
                        // already committed Identity edit never blocks this
                        // one, and this command never checks IdentityRevision.
                        if (current.Revisions.PresentationRevision != expectedPresentationRevision)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(PersistenceFailures.CharacterRevisionConflict(correlationId));
                        }

                        UtcInstant now = _clock.GetUtcNow();
                        long newPresentationRevision = current.Revisions.PresentationRevision + 1;
                        long newCharacterRevision = current.Revisions.CharacterRevision + 1;

                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText = "UPDATE Character SET PortraitReference = $portraitReference, PresentationRevision = $presentationRevision, CharacterRevision = $characterRevision, UpdatedAt = $updatedAt, LastCommandId = $lastCommandId WHERE CharacterId = $characterId;";
                            update.Parameters.AddWithValue("$portraitReference", (object?)portraitReference ?? DBNull.Value);
                            update.Parameters.AddWithValue("$presentationRevision", newPresentationRevision);
                            update.Parameters.AddWithValue("$characterRevision", newCharacterRevision);
                            update.Parameters.AddWithValue("$updatedAt", now.ToString());
                            update.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            update.Parameters.AddWithValue("$characterId", characterId.ToString());
                            update.ExecuteNonQuery();
                        }

                        CharacterSectionRevisions newRevisions = WithPresentationRevision(current.Revisions, newPresentationRevision, newCharacterRevision);
                        var record = new CharacterRecord(characterId, campaign.CampaignId, current.CharacterKind, current.LifecycleStatus, current.ApprovalState, current.DisplayName, portraitReference, newRevisions, current.CreatedAt, now);

                        var payload = new JObject
                        {
                            ["characterId"] = characterId.ToString(),
                            ["displayNameSnapshot"] = current.DisplayName,
                            ["portraitReferenceSnapshot"] = portraitReference,
                            ["newPresentationRevision"] = newPresentationRevision,
                            ["newCharacterRevision"] = newCharacterRevision,
                        };

                        return Result<PipelineWrite<CharacterRecord>>.Success(new PipelineWrite<CharacterRecord>(
                            record, "odyssey.persistence.character_presentation_updated", payload.ToString(Newtonsoft.Json.Formatting.None), characterId.ToString(),
                            aggregateType: "character", aggregateId: characterId.ToString(), aggregateRevision: newCharacterRevision));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterIoFailed(correlationId));
            }
        }

        public Result<IReadOnlyList<CharacterHistoryEntry>> GetCharacterHistory(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureCharacterTables(connection);

                string targetCharacterId = characterId.ToString();
                var entries = new List<CharacterHistoryEntry>();

                using (var select = connection.CreateCommand())
                {
                    select.CommandText =
                        "SELECT EventSequence, EventType, PayloadJson, CreatedAtHost FROM DomainEvents " +
                        "WHERE CampaignId = $campaignId AND EventType IN ($t0, $t1, $t2) ORDER BY EventSequence;";
                    select.Parameters.AddWithValue("$campaignId", campaign.CampaignId.ToString());
                    select.Parameters.AddWithValue("$t0", HistoryEventTypes[0]);
                    select.Parameters.AddWithValue("$t1", HistoryEventTypes[1]);
                    select.Parameters.AddWithValue("$t2", HistoryEventTypes[2]);
                    using SqliteDataReader reader = select.ExecuteReader();
                    while (reader.Read())
                    {
                        long eventSequence = reader.GetInt64(0);
                        string eventType = reader.GetString(1);
                        string payloadJson = reader.GetString(2);
                        UtcInstant occurredAt = UtcInstant.Parse(reader.GetString(3));

                        JObject payload = JObject.Parse(payloadJson);
                        string? payloadCharacterId = (string?)payload["characterId"];
                        if (!string.Equals(payloadCharacterId, targetCharacterId, StringComparison.Ordinal))
                        {
                            // This event's own DomainEvents row carries no
                            // AggregateId column (ADR-012's shared, aggregate-
                            // agnostic table shape) -- filtering by the payload's
                            // own characterId field is this rebuild's only
                            // correct way to select this Character's events out
                            // of the campaign-wide journal.
                            continue;
                        }

                        string? displayNameSnapshot = (string?)payload["displayNameSnapshot"];
                        if (displayNameSnapshot == null)
                        {
                            return Result<IReadOnlyList<CharacterHistoryEntry>>.Failure(PersistenceFailures.IntegrityCheckFailed(correlationId));
                        }

                        entries.Add(new CharacterHistoryEntry(eventSequence, eventType, characterId, displayNameSnapshot, occurredAt));
                    }
                }

                return Result<IReadOnlyList<CharacterHistoryEntry>>.Success(entries);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<IReadOnlyList<CharacterHistoryEntry>>.Failure(PersistenceFailures.CharacterIoFailed(correlationId));
            }
        }

        private static Result<CharacterRecord> ReplayCharacter(SqliteConnection connection, SqliteTransaction transaction, CampaignId campaignId, string whereClause, CommandId commandId, CorrelationId correlationId, CharacterId? knownCharacterId = null)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = SelectColumns + " FROM Character WHERE " + whereClause + " LIMIT 1;";
            if (knownCharacterId.HasValue)
            {
                select.Parameters.AddWithValue("$characterId", knownCharacterId.Value.ToString());
            }

            select.Parameters.AddWithValue("$commandId", commandId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read())
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId));
            }

            return Result<CharacterRecord>.Success(ReadCharacterRecord(reader));
        }

        private static CharacterRecord? SelectForUpdate(SqliteConnection connection, SqliteTransaction transaction, CharacterId characterId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = SelectColumns + " FROM Character WHERE CharacterId = $characterId LIMIT 1;";
            select.Parameters.AddWithValue("$characterId", characterId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            return reader.Read() ? ReadCharacterRecord(reader) : null;
        }

        private const string SelectColumns =
            "SELECT CharacterId, CampaignId, CharacterKind, LifecycleStatus, ApprovalState, DisplayName, PortraitReference, " +
            "CharacterRevision, IdentityRevision, PresentationRevision, CustomFieldsRevision, MechanicsRevision, " +
            "AttributeValuesRevision, CharacterSkillsRevision, CharacterAbilitiesRevision, CharacterResourcesRevision, " +
            "CharacterAnatomyRevision, OwnershipRevision, LifecycleRevision, RuntimeStateRevision, CreatedAt, UpdatedAt";

        /// <summary>
        /// ODY-S04-101: shared column-order contract for every SELECT against
        /// <c>Character</c> that returns a full row, matching
        /// <see cref="SelectColumns"/>'s exact order -- the same
        /// "one shared column list, every caller uses it" convention
        /// <c>SqliteSceneRepository.ReadTokenRecord</c> already established.
        /// </summary>
        private static CharacterRecord ReadCharacterRecord(SqliteDataReader reader)
        {
            CharacterId characterId = CharacterId.Parse(reader.GetString(0));
            CampaignId campaignId = CampaignId.Parse(reader.GetString(1));
            var characterKind = (CharacterKind)Enum.Parse(typeof(CharacterKind), reader.GetString(2));
            var lifecycleStatus = (CharacterLifecycleStatus)Enum.Parse(typeof(CharacterLifecycleStatus), reader.GetString(3));
            var approvalState = (CharacterApprovalState)Enum.Parse(typeof(CharacterApprovalState), reader.GetString(4));
            string displayName = reader.GetString(5);
            string? portraitReference = reader.IsDBNull(6) ? null : reader.GetString(6);
            var revisions = new CharacterSectionRevisions(
                characterRevision: reader.GetInt64(7),
                identityRevision: reader.GetInt64(8),
                presentationRevision: reader.GetInt64(9),
                customFieldsRevision: reader.GetInt64(10),
                mechanicsRevision: reader.GetInt64(11),
                attributeValuesRevision: reader.GetInt64(12),
                characterSkillsRevision: reader.GetInt64(13),
                characterAbilitiesRevision: reader.GetInt64(14),
                characterResourcesRevision: reader.GetInt64(15),
                characterAnatomyRevision: reader.GetInt64(16),
                ownershipRevision: reader.GetInt64(17),
                lifecycleRevision: reader.GetInt64(18),
                runtimeStateRevision: reader.GetInt64(19));
            UtcInstant createdAt = UtcInstant.Parse(reader.GetString(20));
            UtcInstant updatedAt = UtcInstant.Parse(reader.GetString(21));

            return new CharacterRecord(characterId, campaignId, characterKind, lifecycleStatus, approvalState, displayName, portraitReference, revisions, createdAt, updatedAt);
        }

        private static void AddRevisionParameters(SqliteCommand command, CharacterSectionRevisions revisions)
        {
            command.Parameters.AddWithValue("$characterRevision", revisions.CharacterRevision);
            command.Parameters.AddWithValue("$identityRevision", revisions.IdentityRevision);
            command.Parameters.AddWithValue("$presentationRevision", revisions.PresentationRevision);
            command.Parameters.AddWithValue("$customFieldsRevision", revisions.CustomFieldsRevision);
            command.Parameters.AddWithValue("$mechanicsRevision", revisions.MechanicsRevision);
            command.Parameters.AddWithValue("$attributeValuesRevision", revisions.AttributeValuesRevision);
            command.Parameters.AddWithValue("$characterSkillsRevision", revisions.CharacterSkillsRevision);
            command.Parameters.AddWithValue("$characterAbilitiesRevision", revisions.CharacterAbilitiesRevision);
            command.Parameters.AddWithValue("$characterResourcesRevision", revisions.CharacterResourcesRevision);
            command.Parameters.AddWithValue("$characterAnatomyRevision", revisions.CharacterAnatomyRevision);
            command.Parameters.AddWithValue("$ownershipRevision", revisions.OwnershipRevision);
            command.Parameters.AddWithValue("$lifecycleRevision", revisions.LifecycleRevision);
            command.Parameters.AddWithValue("$runtimeStateRevision", revisions.RuntimeStateRevision);
        }

        private static CharacterSectionRevisions WithIdentityRevision(CharacterSectionRevisions source, long newIdentityRevision, long newCharacterRevision) => new CharacterSectionRevisions(
            newCharacterRevision, newIdentityRevision, source.PresentationRevision, source.CustomFieldsRevision, source.MechanicsRevision,
            source.AttributeValuesRevision, source.CharacterSkillsRevision, source.CharacterAbilitiesRevision, source.CharacterResourcesRevision,
            source.CharacterAnatomyRevision, source.OwnershipRevision, source.LifecycleRevision, source.RuntimeStateRevision);

        private static CharacterSectionRevisions WithPresentationRevision(CharacterSectionRevisions source, long newPresentationRevision, long newCharacterRevision) => new CharacterSectionRevisions(
            newCharacterRevision, source.IdentityRevision, newPresentationRevision, source.CustomFieldsRevision, source.MechanicsRevision,
            source.AttributeValuesRevision, source.CharacterSkillsRevision, source.CharacterAbilitiesRevision, source.CharacterResourcesRevision,
            source.CharacterAnatomyRevision, source.OwnershipRevision, source.LifecycleRevision, source.RuntimeStateRevision);

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

        private static void EnsureCharacterTables(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS Character (
    CharacterId TEXT PRIMARY KEY,
    CampaignId TEXT NOT NULL,
    CharacterKind TEXT NOT NULL,
    LifecycleStatus TEXT NOT NULL,
    ApprovalState TEXT NOT NULL,
    DisplayName TEXT NOT NULL,
    PortraitReference TEXT,
    CharacterRevision INTEGER NOT NULL,
    IdentityRevision INTEGER NOT NULL,
    PresentationRevision INTEGER NOT NULL,
    CustomFieldsRevision INTEGER NOT NULL,
    MechanicsRevision INTEGER NOT NULL,
    AttributeValuesRevision INTEGER NOT NULL,
    CharacterSkillsRevision INTEGER NOT NULL,
    CharacterAbilitiesRevision INTEGER NOT NULL,
    CharacterResourcesRevision INTEGER NOT NULL,
    CharacterAnatomyRevision INTEGER NOT NULL,
    OwnershipRevision INTEGER NOT NULL,
    LifecycleRevision INTEGER NOT NULL,
    RuntimeStateRevision INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    LastCommandId TEXT NOT NULL
);";
            command.ExecuteNonQuery();
        }
    }
}
