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
using RulesAttributeCostRules = Odyssey.Rules.Character.AttributeCostRules;

namespace Odyssey.Persistence.Sqlite
{
    /// <summary>
    /// ODY-S04-101/102 implementation of <see cref="ICharacterRepository"/>.
    /// Mirrors <see cref="SqliteSceneRepository"/>'s exact shape -- each
    /// method opens its own short-lived connection under the ADR-011 section
    /// 7.1 PRAGMA profile, every mutating method commits through the shared
    /// <see cref="SqliteSavingPipeline"/> (current-state row + DomainEvent +
    /// AppliedCommands in one ADR-012 section 5 transaction).
    ///
    /// ADR-022 section 5's twelve section revisions are all real columns on
    /// the single <c>Character</c> row from creation onward (see
    /// <c>EnsureCharacterTables</c>). ODY-S04-101 wired Identity/Presentation;
    /// ODY-S04-102 is the first task to actually use the <c>Ownership</c>
    /// section -- <see cref="AssignPrimaryOwner"/>/co-owner/control-grant
    /// commands each declare only <c>OwnershipRevision</c>, never any other
    /// section's revision, following the exact same per-section-gating
    /// pattern <see cref="UpdateIdentity"/>/<see cref="UpdatePresentation"/>
    /// already established.
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
            "odyssey.persistence.character_primary_owner_assigned",
            "odyssey.persistence.character_co_owner_added",
            "odyssey.persistence.character_co_owner_removed",
            "odyssey.persistence.character_control_granted",
            "odyssey.persistence.character_control_revoked",
            "odyssey.persistence.character_draft_bound",
            "odyssey.persistence.character_draft_submitted",
            "odyssey.persistence.character_approved",
            "odyssey.persistence.character_development_points_granted",
            "odyssey.persistence.character_attribute_increased",
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
                        CharacterOwnership ownership = CharacterOwnership.Empty();
                        const CharacterLifecycleStatus lifecycleStatus = CharacterLifecycleStatus.Draft;
                        const CharacterApprovalState approvalState = CharacterApprovalState.Draft;

                        using (var insert = connection.CreateCommand())
                        {
                            insert.Transaction = transaction;
                            insert.CommandText = "INSERT INTO Character (" +
                                "CharacterId, CampaignId, CharacterKind, LifecycleStatus, ApprovalState, DisplayName, PortraitReference, " +
                                "PrimaryOwnerUserId, CoOwnerUserIdsJson, PermanentControllerUserIdsJson, TemporaryControlGrantsJson, " +
                                "CharacterRevision, IdentityRevision, PresentationRevision, CustomFieldsRevision, MechanicsRevision, " +
                                "AttributeValuesRevision, CharacterSkillsRevision, CharacterAbilitiesRevision, CharacterResourcesRevision, " +
                                "CharacterAnatomyRevision, OwnershipRevision, LifecycleRevision, RuntimeStateRevision, " +
                                "RulesetVersion, AnatomyProfileRef, TemplateId, TemplateVersionAtCopyTime, SeedCopyJson, SubmittedAt, " +
                                "PoolEarned, PoolSpent, PoolReserved, AttributesJson, " +
                                "CreatedAt, UpdatedAt, LastCommandId) VALUES (" +
                                "$characterId, $campaignId, $characterKind, $lifecycleStatus, $approvalState, $displayName, NULL, " +
                                "NULL, $coOwners, $permanentControllers, $temporaryGrants, " +
                                "$characterRevision, $identityRevision, $presentationRevision, $customFieldsRevision, $mechanicsRevision, " +
                                "$attributeValuesRevision, $characterSkillsRevision, $characterAbilitiesRevision, $characterResourcesRevision, " +
                                "$characterAnatomyRevision, $ownershipRevision, $lifecycleRevision, $runtimeStateRevision, " +
                                "'', NULL, NULL, NULL, '[]', NULL, " +
                                "0, 0, 0, '[]', " +
                                "$createdAt, $updatedAt, $lastCommandId);";
                            insert.Parameters.AddWithValue("$characterId", characterId.ToString());
                            insert.Parameters.AddWithValue("$campaignId", campaign.CampaignId.ToString());
                            insert.Parameters.AddWithValue("$characterKind", request.CharacterKind.ToString());
                            insert.Parameters.AddWithValue("$lifecycleStatus", lifecycleStatus.ToString());
                            insert.Parameters.AddWithValue("$approvalState", approvalState.ToString());
                            insert.Parameters.AddWithValue("$displayName", request.DisplayName);
                            insert.Parameters.AddWithValue("$coOwners", SerializeUserIds(ownership.CoOwnerUserIds));
                            insert.Parameters.AddWithValue("$permanentControllers", SerializeUserIds(ownership.PermanentControllerUserIds));
                            insert.Parameters.AddWithValue("$temporaryGrants", SerializeGrants(ownership.TemporaryControlGrants));
                            AddRevisionParameters(insert, revisions);
                            insert.Parameters.AddWithValue("$createdAt", now.ToString());
                            insert.Parameters.AddWithValue("$updatedAt", now.ToString());
                            insert.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            insert.ExecuteNonQuery();
                        }

                        // ODY-S04-101's own bare skeleton path -- no ruleset
                        // pinning, no template, no initial owner. See
                        // BindDraftToCampaign for the ADR-023-compliant real
                        // creation path this task adds alongside it.
                        var record = new CharacterRecord(characterId, campaign.CampaignId, request.CharacterKind, lifecycleStatus, approvalState, request.DisplayName, null, ownership, revisions, string.Empty, null, null, null, Array.Empty<CopiedCharacterSeedItem>(), null, DevelopmentPool.Empty(), Array.Empty<AttributeValue>(), now, now);

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

        public Result<CharacterRecord> BindDraftToCampaign(BindDraftToCampaignRequest request, CommandId commandId, CorrelationId correlationId)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            CampaignHandle campaign = request.Campaign;

            // ADR-023 section 6.1: deterministic compatibility validation,
            // rejected before any Character aggregate is created -- no
            // partial state, no database write attempted at all when a
            // template is used and incompatible.
            if (request.Seed.TemplateId.HasValue)
            {
                bool compatible = CharacterTemplateCompatibility.IsCompatible(
                    request.TemplateRulesetId!, request.TemplateRulesetVersion!,
                    campaign.Manifest.RulesetId, campaign.Manifest.RulesetVersion);
                if (!compatible)
                {
                    return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterDraftRulesetIncompatible(correlationId));
                }
            }

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
                        // ADR-025 section 4.2's CAP-INV-007 administrative
                        // reassignment is a separate concern (ODY-S04-102) --
                        // this is the Draft's own initial owner, an ordinary
                        // field set once at creation (backlog section 2.2).
                        var ownership = new CharacterOwnership(request.InitialPrimaryOwnerUserId, Array.Empty<UserId>(), Array.Empty<UserId>(), Array.Empty<CharacterTemporaryControlGrant>());
                        const CharacterLifecycleStatus lifecycleStatus = CharacterLifecycleStatus.Draft;
                        const CharacterApprovalState approvalState = CharacterApprovalState.Draft;
                        // ADR-023 section 6.2: pinned to the campaign's own
                        // current ruleset version at this moment -- never the
                        // template's own recorded RulesetVersion, and never
                        // re-read later.
                        string pinnedRulesetVersion = campaign.Manifest.RulesetVersion;

                        using (var insert = connection.CreateCommand())
                        {
                            insert.Transaction = transaction;
                            insert.CommandText = "INSERT INTO Character (" +
                                "CharacterId, CampaignId, CharacterKind, LifecycleStatus, ApprovalState, DisplayName, PortraitReference, " +
                                "PrimaryOwnerUserId, CoOwnerUserIdsJson, PermanentControllerUserIdsJson, TemporaryControlGrantsJson, " +
                                "CharacterRevision, IdentityRevision, PresentationRevision, CustomFieldsRevision, MechanicsRevision, " +
                                "AttributeValuesRevision, CharacterSkillsRevision, CharacterAbilitiesRevision, CharacterResourcesRevision, " +
                                "CharacterAnatomyRevision, OwnershipRevision, LifecycleRevision, RuntimeStateRevision, " +
                                "RulesetVersion, AnatomyProfileRef, TemplateId, TemplateVersionAtCopyTime, SeedCopyJson, SubmittedAt, " +
                                "PoolEarned, PoolSpent, PoolReserved, AttributesJson, " +
                                "CreatedAt, UpdatedAt, LastCommandId) VALUES (" +
                                "$characterId, $campaignId, $characterKind, $lifecycleStatus, $approvalState, $displayName, NULL, " +
                                "$primaryOwnerUserId, $coOwners, $permanentControllers, $temporaryGrants, " +
                                "$characterRevision, $identityRevision, $presentationRevision, $customFieldsRevision, $mechanicsRevision, " +
                                "$attributeValuesRevision, $characterSkillsRevision, $characterAbilitiesRevision, $characterResourcesRevision, " +
                                "$characterAnatomyRevision, $ownershipRevision, $lifecycleRevision, $runtimeStateRevision, " +
                                "$rulesetVersion, $anatomyProfileRef, $templateId, $templateVersion, $seedCopyJson, NULL, " +
                                "0, 0, 0, '[]', " +
                                "$createdAt, $updatedAt, $lastCommandId);";
                            insert.Parameters.AddWithValue("$characterId", characterId.ToString());
                            insert.Parameters.AddWithValue("$campaignId", campaign.CampaignId.ToString());
                            insert.Parameters.AddWithValue("$characterKind", request.CharacterKind.ToString());
                            insert.Parameters.AddWithValue("$lifecycleStatus", lifecycleStatus.ToString());
                            insert.Parameters.AddWithValue("$approvalState", approvalState.ToString());
                            insert.Parameters.AddWithValue("$displayName", request.DisplayName);
                            insert.Parameters.AddWithValue("$primaryOwnerUserId", (object?)request.InitialPrimaryOwnerUserId?.ToString() ?? DBNull.Value);
                            insert.Parameters.AddWithValue("$coOwners", SerializeUserIds(ownership.CoOwnerUserIds));
                            insert.Parameters.AddWithValue("$permanentControllers", SerializeUserIds(ownership.PermanentControllerUserIds));
                            insert.Parameters.AddWithValue("$temporaryGrants", SerializeGrants(ownership.TemporaryControlGrants));
                            AddRevisionParameters(insert, revisions);
                            insert.Parameters.AddWithValue("$rulesetVersion", pinnedRulesetVersion);
                            insert.Parameters.AddWithValue("$anatomyProfileRef", request.AnatomyProfileRef);
                            insert.Parameters.AddWithValue("$templateId", (object?)request.Seed.TemplateId?.ToString() ?? DBNull.Value);
                            insert.Parameters.AddWithValue("$templateVersion", (object?)request.Seed.TemplateVersionAtCopyTime ?? DBNull.Value);
                            insert.Parameters.AddWithValue("$seedCopyJson", SqliteLocalCharacterDraftRepository.SerializeSeedCopy(request.Seed.Items));
                            insert.Parameters.AddWithValue("$createdAt", now.ToString());
                            insert.Parameters.AddWithValue("$updatedAt", now.ToString());
                            insert.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            insert.ExecuteNonQuery();
                        }

                        var record = new CharacterRecord(characterId, campaign.CampaignId, request.CharacterKind, lifecycleStatus, approvalState, request.DisplayName, null, ownership, revisions, pinnedRulesetVersion, request.AnatomyProfileRef, request.Seed.TemplateId, request.Seed.TemplateVersionAtCopyTime, request.Seed.Items, null, DevelopmentPool.Empty(), Array.Empty<AttributeValue>(), now, now);

                        var payload = new JObject
                        {
                            ["characterId"] = characterId.ToString(),
                            ["campaignId"] = campaign.CampaignId.ToString(),
                            ["characterKind"] = request.CharacterKind.ToString(),
                            ["displayNameSnapshot"] = request.DisplayName,
                            ["rulesetVersion"] = pinnedRulesetVersion,
                            ["templateId"] = request.Seed.TemplateId?.ToString(),
                            ["templateVersionAtCopyTime"] = request.Seed.TemplateVersionAtCopyTime,
                            ["initialPrimaryOwnerUserId"] = request.InitialPrimaryOwnerUserId?.ToString(),
                            ["newCharacterRevision"] = revisions.CharacterRevision,
                        };

                        return Result<PipelineWrite<CharacterRecord>>.Success(new PipelineWrite<CharacterRecord>(
                            record, "odyssey.persistence.character_draft_bound", payload.ToString(Newtonsoft.Json.Formatting.None), characterId.ToString(),
                            aggregateType: "character", aggregateId: characterId.ToString(), aggregateRevision: revisions.CharacterRevision));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterIoFailed(correlationId));
            }
        }

        public Result<CharacterRecord> SubmitCharacterDraft(CampaignHandle campaign, CharacterId characterId, long expectedLifecycleRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (expectedLifecycleRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedLifecycleRevision));
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

                        // ADR-022 section 5: only the Lifecycle section's own
                        // revision gates this command -- this task's own
                        // decision for which section SubmitCharacterDraft
                        // writes (see the interface doc comment).
                        if (current.Revisions.LifecycleRevision != expectedLifecycleRevision)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(PersistenceFailures.CharacterRevisionConflict(correlationId));
                        }

                        // Only legal while still Draft -- not a
                        // CharacterLifecycleTransitions edge (LifecycleStatus
                        // itself does not change here), a plain precondition
                        // on this command's own legality.
                        if (current.LifecycleStatus != CharacterLifecycleStatus.Draft)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(PersistenceFailures.CharacterLifecycleTransitionInvalid(correlationId));
                        }

                        UtcInstant now = _clock.GetUtcNow();
                        long newLifecycleRevision = current.Revisions.LifecycleRevision + 1;
                        long newCharacterRevision = current.Revisions.CharacterRevision + 1;

                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText = "UPDATE Character SET SubmittedAt = $submittedAt, LifecycleRevision = $lifecycleRevision, CharacterRevision = $characterRevision, UpdatedAt = $updatedAt, LastCommandId = $lastCommandId WHERE CharacterId = $characterId;";
                            update.Parameters.AddWithValue("$submittedAt", now.ToString());
                            update.Parameters.AddWithValue("$lifecycleRevision", newLifecycleRevision);
                            update.Parameters.AddWithValue("$characterRevision", newCharacterRevision);
                            update.Parameters.AddWithValue("$updatedAt", now.ToString());
                            update.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            update.Parameters.AddWithValue("$characterId", characterId.ToString());
                            update.ExecuteNonQuery();
                        }

                        CharacterSectionRevisions newRevisions = WithRevisions(current.Revisions, characterRevision: newCharacterRevision, lifecycleRevision: newLifecycleRevision);
                        var record = new CharacterRecord(characterId, campaign.CampaignId, current.CharacterKind, current.LifecycleStatus, current.ApprovalState, current.DisplayName, current.PortraitReference, current.Ownership, newRevisions, current.RulesetVersion, current.AnatomyProfileRef, current.TemplateId, current.TemplateVersionAtCopyTime, current.SeedCopy, now, current.DevelopmentPool, current.Attributes, current.CreatedAt, now);

                        var payload = new JObject
                        {
                            ["characterId"] = characterId.ToString(),
                            ["displayNameSnapshot"] = current.DisplayName,
                            ["submittedAt"] = now.ToString(),
                            ["newLifecycleRevision"] = newLifecycleRevision,
                            ["newCharacterRevision"] = newCharacterRevision,
                        };

                        return Result<PipelineWrite<CharacterRecord>>.Success(new PipelineWrite<CharacterRecord>(
                            record, "odyssey.persistence.character_draft_submitted", payload.ToString(Newtonsoft.Json.Formatting.None), characterId.ToString(),
                            aggregateType: "character", aggregateId: characterId.ToString(), aggregateRevision: newCharacterRevision));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterIoFailed(correlationId));
            }
        }

        public Result<CharacterReviewCommentRecord> AddCharacterReviewComment(CampaignHandle campaign, CharacterId characterId, UserId authorUserId, string text, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (!authorUserId.IsValid) throw new ArgumentException("AuthorUserId is required.", nameof(authorUserId));
            if (string.IsNullOrWhiteSpace(text) || text.Length > 2000) throw new ArgumentException("Text is not safe.", nameof(text));
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
                    tryReplay: transaction => ReplayComment(connection, transaction, commandId, correlationId),
                    apply: transaction =>
                    {
                        // A comment addresses an existing Character, but --
                        // per ADR-023 section 7.1 -- never checks or touches
                        // its Revisions: this is a conflict-free append, the
                        // same shape as a GameLogEntry append (ADR-002
                        // section 17.1). It can never conflict with, or be
                        // conflicted by, any concurrent section edit.
                        CharacterRecord? current = SelectForUpdate(connection, transaction, characterId);
                        if (current == null)
                        {
                            return Result<PipelineWrite<CharacterReviewCommentRecord>>.Failure(PersistenceFailures.CharacterNotFound(correlationId));
                        }

                        UtcInstant now = _clock.GetUtcNow();
                        CharacterReviewCommentId commentId = CharacterReviewCommentId.NewId(now);
                        var record = new CharacterReviewCommentRecord(commentId, characterId, authorUserId, text, now, null);

                        using (var insert = connection.CreateCommand())
                        {
                            insert.Transaction = transaction;
                            insert.CommandText = "INSERT INTO CharacterReviewComment (CommentId, CampaignId, CharacterId, AuthorUserId, Text, CreatedAt, ResolvedAt, CommandId) VALUES ($commentId, $campaignId, $characterId, $authorUserId, $text, $createdAt, NULL, $commandId);";
                            insert.Parameters.AddWithValue("$commentId", commentId.ToString());
                            insert.Parameters.AddWithValue("$campaignId", campaign.CampaignId.ToString());
                            insert.Parameters.AddWithValue("$characterId", characterId.ToString());
                            insert.Parameters.AddWithValue("$authorUserId", authorUserId.ToString());
                            insert.Parameters.AddWithValue("$text", text);
                            insert.Parameters.AddWithValue("$createdAt", now.ToString());
                            insert.Parameters.AddWithValue("$commandId", commandId.ToString());
                            insert.ExecuteNonQuery();
                        }

                        var payload = new JObject
                        {
                            ["commentId"] = commentId.ToString(),
                            ["characterId"] = characterId.ToString(),
                            ["authorUserId"] = authorUserId.ToString(),
                            ["text"] = text,
                        };

                        // No aggregateType/aggregateRevision -- this command
                        // never bumps CharacterRevision or any section
                        // revision; nothing about the Character's own row
                        // changes.
                        return Result<PipelineWrite<CharacterReviewCommentRecord>>.Success(new PipelineWrite<CharacterReviewCommentRecord>(
                            record, "odyssey.persistence.character_review_comment_added", payload.ToString(Newtonsoft.Json.Formatting.None), commentId.ToString()));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CharacterReviewCommentRecord>.Failure(PersistenceFailures.CharacterIoFailed(correlationId));
            }
        }

        public Result<CharacterRecord> ApproveCharacterDraft(CampaignHandle campaign, CharacterId characterId, bool actorIsMainGm, long expectedLifecycleRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (expectedLifecycleRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedLifecycleRevision));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            // ADR-023 section 7.3: Character.Approve is MainGM-only -- the
            // same caller-supplied-boolean baseline AssignPrimaryOwner
            // already uses, checked before touching the database at all.
            if (!actorIsMainGm)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterApprovalDenied(correlationId));
            }

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

                        if (current.Revisions.LifecycleRevision != expectedLifecycleRevision)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(PersistenceFailures.CharacterRevisionConflict(correlationId));
                        }

                        // The sole state-legality gate: the generic
                        // ADR-022/ODY-S04-101 transition table, not a
                        // duplicated ad hoc check. A repeat ApproveCharacterDraft
                        // on an already-Active Character is rejected here
                        // because IsValidTransition(Active, Active) is false
                        // (the table's own same-status rule), not because of
                        // a separate business precondition.
                        if (!CharacterLifecycleTransitions.IsValidTransition(current.LifecycleStatus, CharacterLifecycleStatus.Active))
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(PersistenceFailures.CharacterLifecycleTransitionInvalid(correlationId));
                        }

                        UtcInstant now = _clock.GetUtcNow();
                        long newLifecycleRevision = current.Revisions.LifecycleRevision + 1;
                        long newCharacterRevision = current.Revisions.CharacterRevision + 1;
                        const CharacterLifecycleStatus newLifecycleStatus = CharacterLifecycleStatus.Active;
                        const CharacterApprovalState newApprovalState = CharacterApprovalState.Approved;

                        // LifecycleStatus and ApprovalState change together
                        // in this single UPDATE statement, inside the single
                        // transaction SqliteSavingPipeline.Execute already
                        // commits atomically (current-state row + DomainEvent
                        // + AppliedCommands, ADR-012 section 5) -- there is
                        // no intermediate state where one field changed and
                        // the other did not.
                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText = "UPDATE Character SET LifecycleStatus = $lifecycleStatus, ApprovalState = $approvalState, LifecycleRevision = $lifecycleRevision, CharacterRevision = $characterRevision, UpdatedAt = $updatedAt, LastCommandId = $lastCommandId WHERE CharacterId = $characterId;";
                            update.Parameters.AddWithValue("$lifecycleStatus", newLifecycleStatus.ToString());
                            update.Parameters.AddWithValue("$approvalState", newApprovalState.ToString());
                            update.Parameters.AddWithValue("$lifecycleRevision", newLifecycleRevision);
                            update.Parameters.AddWithValue("$characterRevision", newCharacterRevision);
                            update.Parameters.AddWithValue("$updatedAt", now.ToString());
                            update.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            update.Parameters.AddWithValue("$characterId", characterId.ToString());
                            update.ExecuteNonQuery();
                        }

                        CharacterSectionRevisions newRevisions = WithRevisions(current.Revisions, characterRevision: newCharacterRevision, lifecycleRevision: newLifecycleRevision);
                        var record = new CharacterRecord(characterId, campaign.CampaignId, current.CharacterKind, newLifecycleStatus, newApprovalState, current.DisplayName, current.PortraitReference, current.Ownership, newRevisions, current.RulesetVersion, current.AnatomyProfileRef, current.TemplateId, current.TemplateVersionAtCopyTime, current.SeedCopy, current.SubmittedAt, current.DevelopmentPool, current.Attributes, current.CreatedAt, now);

                        var payload = new JObject
                        {
                            ["characterId"] = characterId.ToString(),
                            ["displayNameSnapshot"] = current.DisplayName,
                            ["lifecycleStatusBefore"] = current.LifecycleStatus.ToString(),
                            ["lifecycleStatusAfter"] = newLifecycleStatus.ToString(),
                            ["approvalStateBefore"] = current.ApprovalState.ToString(),
                            ["approvalStateAfter"] = newApprovalState.ToString(),
                            ["newLifecycleRevision"] = newLifecycleRevision,
                            ["newCharacterRevision"] = newCharacterRevision,
                        };

                        return Result<PipelineWrite<CharacterRecord>>.Success(new PipelineWrite<CharacterRecord>(
                            record, "odyssey.persistence.character_approved", payload.ToString(Newtonsoft.Json.Formatting.None), characterId.ToString(),
                            aggregateType: "character", aggregateId: characterId.ToString(), aggregateRevision: newCharacterRevision));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterIoFailed(correlationId));
            }
        }

        public Result<IReadOnlyList<CharacterReviewCommentRecord>> GetCharacterReviewComments(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureCharacterTables(connection);

                var comments = new List<CharacterReviewCommentRecord>();
                using (var select = connection.CreateCommand())
                {
                    select.CommandText = "SELECT CommentId, CharacterId, AuthorUserId, Text, CreatedAt, ResolvedAt FROM CharacterReviewComment WHERE CharacterId = $characterId ORDER BY CreatedAt, CommentId;";
                    select.Parameters.AddWithValue("$characterId", characterId.ToString());
                    using SqliteDataReader reader = select.ExecuteReader();
                    while (reader.Read())
                    {
                        CharacterReviewCommentId commentId = CharacterReviewCommentId.Parse(reader.GetString(0));
                        CharacterId readCharacterId = CharacterId.Parse(reader.GetString(1));
                        UserId authorUserId = UserId.Parse(reader.GetString(2));
                        string text = reader.GetString(3);
                        UtcInstant createdAt = UtcInstant.Parse(reader.GetString(4));
                        UtcInstant? resolvedAt = reader.IsDBNull(5) ? (UtcInstant?)null : UtcInstant.Parse(reader.GetString(5));
                        comments.Add(new CharacterReviewCommentRecord(commentId, readCharacterId, authorUserId, text, createdAt, resolvedAt));
                    }
                }

                return Result<IReadOnlyList<CharacterReviewCommentRecord>>.Success(comments);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<IReadOnlyList<CharacterReviewCommentRecord>>.Failure(PersistenceFailures.CharacterIoFailed(correlationId));
            }
        }

        public Result<CharacterRecord> GrantDevelopmentPoints(CampaignHandle campaign, CharacterId characterId, long amount, string reason, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason is required.", nameof(reason));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));

            // Product section 12.2: "Только MainGM может начислять очки
            // развития" -- the same caller-supplied-boolean convention
            // AssignPrimaryOwner already uses, checked before touching the
            // database at all.
            if (!actorIsMainGm)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterDevelopmentGrantDenied(correlationId));
            }

            return MutateMechanics(campaign, characterId, expectedMechanicsRevision, commandId, correlationId, current =>
            {
                var newPool = new DevelopmentPool(current.DevelopmentPool.Earned + amount, current.DevelopmentPool.Spent, current.DevelopmentPool.Reserved);
                UtcInstant now = _clock.GetUtcNow();
                var ledgerEntry = new DevelopmentTransactionRecord(
                    DevelopmentTransactionId.NewId(now), characterId, DevelopmentTransactionKind.Grant, amount, null, reason, actorUserId, campaign.Manifest.RulesetVersion, now, correlationId);

                var payload = new JObject
                {
                    ["amount"] = amount,
                    ["reason"] = reason,
                    ["actorUserId"] = actorUserId.ToString(),
                    ["newAvailable"] = newPool.Available,
                };

                return Result<MechanicsMutation>.Success(new MechanicsMutation(
                    newPool, current.Attributes, "odyssey.persistence.character_development_points_granted", payload, new[] { ledgerEntry }));
            });
        }

        public Result<CharacterRecord> PurchaseAttributeIncrease(CampaignHandle campaign, CharacterId characterId, AttributeDefinitionId attributeDefinitionId, long toValue, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, long expectedAttributeRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (!attributeDefinitionId.IsValid) throw new ArgumentException("AttributeDefinitionId is required.", nameof(attributeDefinitionId));
            if (toValue < 0) throw new ArgumentOutOfRangeException(nameof(toValue));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (expectedAttributeRevision < 0) throw new ArgumentOutOfRangeException(nameof(expectedAttributeRevision));

            return MutateMechanics(campaign, characterId, expectedMechanicsRevision, commandId, correlationId, current =>
            {
                // Product section 13.1: "у пользователя есть право развивать
                // персонажа" -- MainGM or an assigned user of this Character,
                // reusing ODY-S04-102's own IsAssignedCharacter predicate
                // rather than duplicating an ownership check here.
                UtcInstant now = _clock.GetUtcNow();
                bool permitted = actorIsMainGm || CharacterOwnershipAssignment.IsAssignedCharacter(current.Ownership, actorUserId, now);
                if (!permitted)
                {
                    return Result<MechanicsMutation>.Failure(PersistenceFailures.CharacterDevelopmentPurchaseDenied(correlationId));
                }

                AttributeValue? existing = null;
                foreach (AttributeValue candidate in current.Attributes)
                {
                    if (candidate.AttributeDefinitionId.Equals(attributeDefinitionId)) { existing = candidate; break; }
                }

                long fromValue = existing?.BaseValue ?? 0;
                long currentAttributeRevision = existing?.Revision ?? 0;

                // ADR-024 section 4.2's entry-level gate -- independent of
                // MechanicsRevision, checked against the addressed
                // attribute's own current revision (0 for an attribute never
                // purchased before).
                if (currentAttributeRevision != expectedAttributeRevision)
                {
                    return Result<MechanicsMutation>.Failure(PersistenceFailures.CharacterRevisionConflict(correlationId));
                }

                if (toValue <= fromValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(toValue), "ToValue must exceed the attribute's current BaseValue for an increase.");
                }

                // Product section 11.3 / RulesAttributeCostRules: TEST
                // FIXTURE cost/cap -- see that class's own doc comment. No
                // Ruleset-catalog cost table exists yet anywhere in this
                // codebase.
                if (RulesAttributeCostRules.ExceedsNormalCap(toValue))
                {
                    return Result<MechanicsMutation>.Failure(PersistenceFailures.CharacterAttributeCapExceeded(correlationId));
                }

                long cost = RulesAttributeCostRules.CostForIncrease(fromValue, toValue);
                if (cost > current.DevelopmentPool.Available)
                {
                    return Result<MechanicsMutation>.Failure(PersistenceFailures.CharacterDevelopmentInsufficientBalance(correlationId));
                }

                var newPool = new DevelopmentPool(current.DevelopmentPool.Earned, current.DevelopmentPool.Spent + cost, current.DevelopmentPool.Reserved);
                long newSpentDevelopmentPoints = (existing?.SpentDevelopmentPoints ?? 0) + cost;
                long newAttributeRevision = currentAttributeRevision + 1;
                var newAttribute = new AttributeValue(attributeDefinitionId, toValue, existing?.PermanentAdjustment ?? 0, newSpentDevelopmentPoints, newAttributeRevision);

                var newAttributes = new List<AttributeValue>(current.Attributes.Count + 1);
                bool replaced = false;
                foreach (AttributeValue candidate in current.Attributes)
                {
                    if (candidate.AttributeDefinitionId.Equals(attributeDefinitionId))
                    {
                        newAttributes.Add(newAttribute);
                        replaced = true;
                    }
                    else
                    {
                        newAttributes.Add(candidate);
                    }
                }

                if (!replaced) newAttributes.Add(newAttribute);

                var ledgerEntry = new DevelopmentTransactionRecord(
                    DevelopmentTransactionId.NewId(now), characterId, DevelopmentTransactionKind.Spend, cost, attributeDefinitionId.ToString(), "Attribute increase purchase", actorUserId, campaign.Manifest.RulesetVersion, now, correlationId);

                var payload = new JObject
                {
                    ["attributeDefinitionId"] = attributeDefinitionId.ToString(),
                    ["fromValue"] = fromValue,
                    ["toValue"] = toValue,
                    ["cost"] = cost,
                    ["newEffectiveValue"] = newAttribute.EffectiveValue,
                    ["actorUserId"] = actorUserId.ToString(),
                    ["newAvailable"] = newPool.Available,
                };

                return Result<MechanicsMutation>.Success(new MechanicsMutation(
                    newPool, newAttributes, "odyssey.persistence.character_attribute_increased", payload, new[] { ledgerEntry }));
            });
        }

        public Result<IReadOnlyList<DevelopmentTransactionRecord>> GetDevelopmentLedger(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureCharacterTables(connection);

                var entries = new List<DevelopmentTransactionRecord>();
                using (var select = connection.CreateCommand())
                {
                    select.CommandText = "SELECT TransactionId, CharacterId, Kind, Amount, SourceRef, Reason, ActorUserId, RulesetVersion, CreatedAt, CorrelationId FROM DevelopmentTransaction WHERE CharacterId = $characterId ORDER BY CreatedAt, TransactionId;";
                    select.Parameters.AddWithValue("$characterId", characterId.ToString());
                    using SqliteDataReader reader = select.ExecuteReader();
                    while (reader.Read())
                    {
                        entries.Add(ReadDevelopmentTransactionRecord(reader));
                    }
                }

                return Result<IReadOnlyList<DevelopmentTransactionRecord>>.Success(entries);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<IReadOnlyList<DevelopmentTransactionRecord>>.Failure(PersistenceFailures.CharacterIoFailed(correlationId));
            }
        }

        private static DevelopmentTransactionRecord ReadDevelopmentTransactionRecord(SqliteDataReader reader)
        {
            DevelopmentTransactionId transactionId = DevelopmentTransactionId.Parse(reader.GetString(0));
            CharacterId characterId = CharacterId.Parse(reader.GetString(1));
            var kind = (DevelopmentTransactionKind)Enum.Parse(typeof(DevelopmentTransactionKind), reader.GetString(2));
            long amount = reader.GetInt64(3);
            string? sourceRef = reader.IsDBNull(4) ? null : reader.GetString(4);
            string reason = reader.GetString(5);
            UserId actorUserId = UserId.Parse(reader.GetString(6));
            string rulesetVersion = reader.GetString(7);
            UtcInstant createdAt = UtcInstant.Parse(reader.GetString(8));
            CorrelationId correlationId = CorrelationId.Parse(reader.GetString(9));
            return new DevelopmentTransactionRecord(transactionId, characterId, kind, amount, sourceRef, reason, actorUserId, rulesetVersion, createdAt, correlationId);
        }

        private static void InsertDevelopmentTransaction(SqliteConnection connection, SqliteTransaction transaction, CampaignId campaignId, DevelopmentTransactionRecord entry)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO DevelopmentTransaction (TransactionId, CampaignId, CharacterId, Kind, Amount, SourceRef, Reason, ActorUserId, RulesetVersion, CreatedAt, CorrelationId) VALUES ($transactionId, $campaignId, $characterId, $kind, $amount, $sourceRef, $reason, $actorUserId, $rulesetVersion, $createdAt, $correlationId);";
            insert.Parameters.AddWithValue("$transactionId", entry.TransactionId.ToString());
            insert.Parameters.AddWithValue("$campaignId", campaignId.ToString());
            insert.Parameters.AddWithValue("$characterId", entry.CharacterId.ToString());
            insert.Parameters.AddWithValue("$kind", entry.Kind.ToString());
            insert.Parameters.AddWithValue("$amount", entry.Amount);
            insert.Parameters.AddWithValue("$sourceRef", (object?)entry.SourceRef ?? DBNull.Value);
            insert.Parameters.AddWithValue("$reason", entry.Reason);
            insert.Parameters.AddWithValue("$actorUserId", entry.ActorUserId.ToString());
            insert.Parameters.AddWithValue("$rulesetVersion", entry.RulesetVersion);
            insert.Parameters.AddWithValue("$createdAt", entry.CreatedAt.ToString());
            insert.Parameters.AddWithValue("$correlationId", entry.CorrelationId.ToString());
            insert.ExecuteNonQuery();
        }

        /// <summary>
        /// ODY-S04-105: the shared shape every <c>Mechanics</c>-section
        /// command follows -- load, <c>MechanicsRevision</c> check, caller-
        /// supplied pure business logic producing a new pool/attributes/
        /// event/ledger set, one commit. Mirrors <c>MutateOwnership</c>'s own
        /// role for the <c>Ownership</c> section (ODY-S04-102) -- future
        /// purchase commands (skill/ability, ODY-S04-106/107) reuse this same
        /// helper rather than re-implementing the gate/load/check/commit
        /// sequence.
        /// </summary>
        private Result<CharacterRecord> MutateMechanics(
            CampaignHandle campaign,
            CharacterId characterId,
            long expectedMechanicsRevision,
            CommandId commandId,
            CorrelationId correlationId,
            Func<CharacterRecord, Result<MechanicsMutation>> mutate)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (expectedMechanicsRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedMechanicsRevision));
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

                        if (current.Revisions.MechanicsRevision != expectedMechanicsRevision)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(PersistenceFailures.CharacterRevisionConflict(correlationId));
                        }

                        Result<MechanicsMutation> mutationResult = mutate(current);
                        if (mutationResult.IsFailure)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(mutationResult.Error);
                        }

                        MechanicsMutation mutation = mutationResult.Value;
                        UtcInstant now = _clock.GetUtcNow();
                        long newMechanicsRevision = current.Revisions.MechanicsRevision + 1;
                        long newCharacterRevision = current.Revisions.CharacterRevision + 1;

                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText = "UPDATE Character SET PoolEarned = $poolEarned, PoolSpent = $poolSpent, PoolReserved = $poolReserved, AttributesJson = $attributesJson, MechanicsRevision = $mechanicsRevision, CharacterRevision = $characterRevision, UpdatedAt = $updatedAt, LastCommandId = $lastCommandId WHERE CharacterId = $characterId;";
                            update.Parameters.AddWithValue("$poolEarned", mutation.NewPool.Earned);
                            update.Parameters.AddWithValue("$poolSpent", mutation.NewPool.Spent);
                            update.Parameters.AddWithValue("$poolReserved", mutation.NewPool.Reserved);
                            update.Parameters.AddWithValue("$attributesJson", SerializeAttributes(mutation.NewAttributes));
                            update.Parameters.AddWithValue("$mechanicsRevision", newMechanicsRevision);
                            update.Parameters.AddWithValue("$characterRevision", newCharacterRevision);
                            update.Parameters.AddWithValue("$updatedAt", now.ToString());
                            update.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            update.Parameters.AddWithValue("$characterId", characterId.ToString());
                            update.ExecuteNonQuery();
                        }

                        foreach (DevelopmentTransactionRecord ledgerEntry in mutation.LedgerEntries)
                        {
                            InsertDevelopmentTransaction(connection, transaction, campaign.CampaignId, ledgerEntry);
                        }

                        CharacterSectionRevisions newRevisions = WithRevisions(current.Revisions, characterRevision: newCharacterRevision, mechanicsRevision: newMechanicsRevision);
                        var record = new CharacterRecord(characterId, campaign.CampaignId, current.CharacterKind, current.LifecycleStatus, current.ApprovalState, current.DisplayName, current.PortraitReference, current.Ownership, newRevisions, current.RulesetVersion, current.AnatomyProfileRef, current.TemplateId, current.TemplateVersionAtCopyTime, current.SeedCopy, current.SubmittedAt, mutation.NewPool, mutation.NewAttributes, current.CreatedAt, now);

                        mutation.PayloadExtra["characterId"] = characterId.ToString();
                        mutation.PayloadExtra["displayNameSnapshot"] = current.DisplayName;
                        mutation.PayloadExtra["newMechanicsRevision"] = newMechanicsRevision;
                        mutation.PayloadExtra["newCharacterRevision"] = newCharacterRevision;

                        return Result<PipelineWrite<CharacterRecord>>.Success(new PipelineWrite<CharacterRecord>(
                            record, mutation.EventType, mutation.PayloadExtra.ToString(Newtonsoft.Json.Formatting.None), characterId.ToString(),
                            aggregateType: "character", aggregateId: characterId.ToString(), aggregateRevision: newCharacterRevision));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterIoFailed(correlationId));
            }
        }

        /// <summary>ODY-S04-105: the pure business-logic result <see cref="MutateMechanics"/>'s caller-supplied callback returns -- the new pool/attribute state, the event to emit, and the ledger row(s) to co-commit.</summary>
        private sealed class MechanicsMutation
        {
            public MechanicsMutation(DevelopmentPool newPool, IReadOnlyList<AttributeValue> newAttributes, string eventType, JObject payloadExtra, IReadOnlyList<DevelopmentTransactionRecord> ledgerEntries)
            {
                NewPool = newPool;
                NewAttributes = newAttributes;
                EventType = eventType;
                PayloadExtra = payloadExtra;
                LedgerEntries = ledgerEntries;
            }

            public DevelopmentPool NewPool { get; }
            public IReadOnlyList<AttributeValue> NewAttributes { get; }
            public string EventType { get; }
            public JObject PayloadExtra { get; }
            public IReadOnlyList<DevelopmentTransactionRecord> LedgerEntries { get; }
        }

        private static Result<CharacterReviewCommentRecord> ReplayComment(SqliteConnection connection, SqliteTransaction transaction, CommandId commandId, CorrelationId correlationId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT CommentId, CharacterId, AuthorUserId, Text, CreatedAt, ResolvedAt FROM CharacterReviewComment WHERE CommandId = $commandId LIMIT 1;";
            select.Parameters.AddWithValue("$commandId", commandId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read())
            {
                return Result<CharacterReviewCommentRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId));
            }

            CharacterReviewCommentId commentId = CharacterReviewCommentId.Parse(reader.GetString(0));
            CharacterId characterId = CharacterId.Parse(reader.GetString(1));
            UserId authorUserId = UserId.Parse(reader.GetString(2));
            string text = reader.GetString(3);
            UtcInstant createdAt = UtcInstant.Parse(reader.GetString(4));
            UtcInstant? resolvedAt = reader.IsDBNull(5) ? (UtcInstant?)null : UtcInstant.Parse(reader.GetString(5));
            return Result<CharacterReviewCommentRecord>.Success(new CharacterReviewCommentRecord(commentId, characterId, authorUserId, text, createdAt, resolvedAt));
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
                        // committed Presentation/Ownership edit (different
                        // section) is never checked here and never blocks
                        // this one.
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

                        CharacterSectionRevisions newRevisions = WithRevisions(current.Revisions, characterRevision: newCharacterRevision, identityRevision: newIdentityRevision);
                        var record = new CharacterRecord(characterId, campaign.CampaignId, current.CharacterKind, current.LifecycleStatus, current.ApprovalState, newDisplayName, current.PortraitReference, current.Ownership, newRevisions, current.RulesetVersion, current.AnatomyProfileRef, current.TemplateId, current.TemplateVersionAtCopyTime, current.SeedCopy, current.SubmittedAt, current.DevelopmentPool, current.Attributes, current.CreatedAt, now);

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
                        // own revision gates this command.
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

                        CharacterSectionRevisions newRevisions = WithRevisions(current.Revisions, characterRevision: newCharacterRevision, presentationRevision: newPresentationRevision);
                        var record = new CharacterRecord(characterId, campaign.CampaignId, current.CharacterKind, current.LifecycleStatus, current.ApprovalState, current.DisplayName, portraitReference, current.Ownership, newRevisions, current.RulesetVersion, current.AnatomyProfileRef, current.TemplateId, current.TemplateVersionAtCopyTime, current.SeedCopy, current.SubmittedAt, current.DevelopmentPool, current.Attributes, current.CreatedAt, now);

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

        public Result<CharacterRecord> AssignPrimaryOwner(CampaignHandle campaign, CharacterId characterId, UserId newPrimaryOwnerUserId, string reasonCode, bool actorIsMainGm, long expectedOwnershipRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (!newPrimaryOwnerUserId.IsValid) throw new ArgumentException("NewPrimaryOwnerUserId is required.", nameof(newPrimaryOwnerUserId));
            if (expectedOwnershipRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedOwnershipRevision));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            // ADR-025 section 4.2: MainGM-only (CAP-INV-007) -- the same
            // caller-supplied-boolean baseline BoardMovementService/
            // DiceRollService already use, checked before touching the
            // database at all.
            if (!actorIsMainGm)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterOwnershipDenied(correlationId));
            }

            if (string.IsNullOrWhiteSpace(reasonCode))
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterOwnershipReasonRequired(correlationId));
            }

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

                        // ADR-022 section 5: only the Ownership section's own
                        // revision gates this command.
                        if (current.Revisions.OwnershipRevision != expectedOwnershipRevision)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(PersistenceFailures.CharacterRevisionConflict(correlationId));
                        }

                        UtcInstant now = _clock.GetUtcNow();
                        long newOwnershipRevision = current.Revisions.OwnershipRevision + 1;
                        long newCharacterRevision = current.Revisions.CharacterRevision + 1;
                        UserId? previousPrimaryOwnerUserId = current.Ownership.PrimaryOwnerUserId;

                        // CAP-INV-007: never silently changes CoOwnerUserIds/
                        // control grants -- only PrimaryOwnerUserId itself.
                        var newOwnership = new CharacterOwnership(newPrimaryOwnerUserId, current.Ownership.CoOwnerUserIds, current.Ownership.PermanentControllerUserIds, current.Ownership.TemporaryControlGrants);

                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText = "UPDATE Character SET PrimaryOwnerUserId = $primaryOwnerUserId, OwnershipRevision = $ownershipRevision, CharacterRevision = $characterRevision, UpdatedAt = $updatedAt, LastCommandId = $lastCommandId WHERE CharacterId = $characterId;";
                            update.Parameters.AddWithValue("$primaryOwnerUserId", newPrimaryOwnerUserId.ToString());
                            update.Parameters.AddWithValue("$ownershipRevision", newOwnershipRevision);
                            update.Parameters.AddWithValue("$characterRevision", newCharacterRevision);
                            update.Parameters.AddWithValue("$updatedAt", now.ToString());
                            update.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            update.Parameters.AddWithValue("$characterId", characterId.ToString());
                            update.ExecuteNonQuery();
                        }

                        CharacterSectionRevisions newRevisions = WithRevisions(current.Revisions, characterRevision: newCharacterRevision, ownershipRevision: newOwnershipRevision);
                        var record = new CharacterRecord(characterId, campaign.CampaignId, current.CharacterKind, current.LifecycleStatus, current.ApprovalState, current.DisplayName, current.PortraitReference, newOwnership, newRevisions, current.RulesetVersion, current.AnatomyProfileRef, current.TemplateId, current.TemplateVersionAtCopyTime, current.SeedCopy, current.SubmittedAt, current.DevelopmentPool, current.Attributes, current.CreatedAt, now);

                        var payload = new JObject
                        {
                            ["characterId"] = characterId.ToString(),
                            ["displayNameSnapshot"] = current.DisplayName,
                            ["previousPrimaryOwnerUserId"] = previousPrimaryOwnerUserId?.ToString(),
                            ["newPrimaryOwnerUserId"] = newPrimaryOwnerUserId.ToString(),
                            ["reasonCode"] = reasonCode,
                            ["newOwnershipRevision"] = newOwnershipRevision,
                            ["newCharacterRevision"] = newCharacterRevision,
                        };

                        return Result<PipelineWrite<CharacterRecord>>.Success(new PipelineWrite<CharacterRecord>(
                            record, "odyssey.persistence.character_primary_owner_assigned", payload.ToString(Newtonsoft.Json.Formatting.None), characterId.ToString(),
                            aggregateType: "character", aggregateId: characterId.ToString(), aggregateRevision: newCharacterRevision));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterIoFailed(correlationId));
            }
        }

        public Result<CharacterRecord> AddCharacterCoOwner(CampaignHandle campaign, CharacterId characterId, UserId coOwnerUserId, bool actorIsMainGm, long expectedOwnershipRevision, CommandId commandId, CorrelationId correlationId)
        {
            return MutateOwnership(
                campaign, characterId, actorIsMainGm, expectedOwnershipRevision, commandId, correlationId,
                mutate: current =>
                {
                    var coOwners = new List<UserId>(current.Ownership.CoOwnerUserIds);
                    if (!coOwners.Contains(coOwnerUserId))
                    {
                        // "A duplicate add does not create a duplicate entry"
                        // (this task's own explicit requirement) -- the list
                        // never holds the same UserId twice, regardless of how
                        // many times this command is issued with different
                        // CommandIds.
                        coOwners.Add(coOwnerUserId);
                    }

                    return (
                        new CharacterOwnership(current.Ownership.PrimaryOwnerUserId, coOwners, current.Ownership.PermanentControllerUserIds, current.Ownership.TemporaryControlGrants),
                        "odyssey.persistence.character_co_owner_added",
                        new JObject { ["coOwnerUserId"] = coOwnerUserId.ToString() });
                });
        }

        public Result<CharacterRecord> RemoveCharacterCoOwner(CampaignHandle campaign, CharacterId characterId, UserId coOwnerUserId, bool actorIsMainGm, long expectedOwnershipRevision, CommandId commandId, CorrelationId correlationId)
        {
            return MutateOwnership(
                campaign, characterId, actorIsMainGm, expectedOwnershipRevision, commandId, correlationId,
                mutate: current =>
                {
                    var coOwners = new List<UserId>();
                    foreach (UserId existing in current.Ownership.CoOwnerUserIds)
                    {
                        if (!existing.Equals(coOwnerUserId)) coOwners.Add(existing);
                    }

                    return (
                        new CharacterOwnership(current.Ownership.PrimaryOwnerUserId, coOwners, current.Ownership.PermanentControllerUserIds, current.Ownership.TemporaryControlGrants),
                        "odyssey.persistence.character_co_owner_removed",
                        new JObject { ["coOwnerUserId"] = coOwnerUserId.ToString() });
                });
        }

        public Result<CharacterRecord> GrantPermanentCharacterControl(CampaignHandle campaign, CharacterId characterId, UserId controlUserId, bool actorIsMainGm, long expectedOwnershipRevision, CommandId commandId, CorrelationId correlationId)
        {
            return MutateOwnership(
                campaign, characterId, actorIsMainGm, expectedOwnershipRevision, commandId, correlationId,
                mutate: current =>
                {
                    var controllers = new List<UserId>(current.Ownership.PermanentControllerUserIds);
                    if (!controllers.Contains(controlUserId)) controllers.Add(controlUserId);

                    return (
                        new CharacterOwnership(current.Ownership.PrimaryOwnerUserId, current.Ownership.CoOwnerUserIds, controllers, current.Ownership.TemporaryControlGrants),
                        "odyssey.persistence.character_control_granted",
                        new JObject { ["controlUserId"] = controlUserId.ToString(), ["grantKind"] = "Permanent" });
                });
        }

        public Result<CharacterRecord> GrantTemporaryCharacterControl(CampaignHandle campaign, CharacterId characterId, UserId controlUserId, UtcInstant? expiresAt, bool actorIsMainGm, long expectedOwnershipRevision, CommandId commandId, CorrelationId correlationId)
        {
            return MutateOwnership(
                campaign, characterId, actorIsMainGm, expectedOwnershipRevision, commandId, correlationId,
                mutate: current =>
                {
                    UtcInstant grantedAt = _clock.GetUtcNow();
                    var grants = new List<CharacterTemporaryControlGrant>();
                    foreach (CharacterTemporaryControlGrant existing in current.Ownership.TemporaryControlGrants)
                    {
                        // Replacing an existing grant for the same user rather
                        // than accumulating two grants for one user, mirroring
                        // the co-owner/permanent-controller de-duplication
                        // convention above.
                        if (!existing.UserId.Equals(controlUserId)) grants.Add(existing);
                    }

                    grants.Add(new CharacterTemporaryControlGrant(controlUserId, grantedAt, expiresAt));

                    return (
                        new CharacterOwnership(current.Ownership.PrimaryOwnerUserId, current.Ownership.CoOwnerUserIds, current.Ownership.PermanentControllerUserIds, grants),
                        "odyssey.persistence.character_control_granted",
                        new JObject { ["controlUserId"] = controlUserId.ToString(), ["grantKind"] = "Temporary", ["expiresAt"] = expiresAt?.ToString() });
                });
        }

        public Result<CharacterRecord> RevokeCharacterControl(CampaignHandle campaign, CharacterId characterId, UserId controlUserId, bool actorIsMainGm, long expectedOwnershipRevision, CommandId commandId, CorrelationId correlationId)
        {
            return MutateOwnership(
                campaign, characterId, actorIsMainGm, expectedOwnershipRevision, commandId, correlationId,
                mutate: current =>
                {
                    var controllers = new List<UserId>();
                    foreach (UserId existing in current.Ownership.PermanentControllerUserIds)
                    {
                        if (!existing.Equals(controlUserId)) controllers.Add(existing);
                    }

                    var grants = new List<CharacterTemporaryControlGrant>();
                    foreach (CharacterTemporaryControlGrant existing in current.Ownership.TemporaryControlGrants)
                    {
                        if (!existing.UserId.Equals(controlUserId)) grants.Add(existing);
                    }

                    return (
                        new CharacterOwnership(current.Ownership.PrimaryOwnerUserId, current.Ownership.CoOwnerUserIds, controllers, grants),
                        "odyssey.persistence.character_control_revoked",
                        new JObject { ["controlUserId"] = controlUserId.ToString() });
                });
        }

        /// <summary>
        /// ODY-S04-102: the shared shape every Ownership-section command
        /// (except <see cref="AssignPrimaryOwner"/>, which additionally
        /// requires a mandatory reason) follows -- MainGM-gate, load,
        /// OwnershipRevision check, caller-supplied pure mutation of
        /// <see cref="CharacterOwnership"/>, one transaction commit. Keeps
        /// the five co-owner/control-grant commands from each re-implementing
        /// the identical gate/load/check/commit sequence.
        /// </summary>
        private Result<CharacterRecord> MutateOwnership(
            CampaignHandle campaign,
            CharacterId characterId,
            bool actorIsMainGm,
            long expectedOwnershipRevision,
            CommandId commandId,
            CorrelationId correlationId,
            Func<CharacterRecord, (CharacterOwnership NewOwnership, string EventType, JObject PayloadExtra)> mutate)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (expectedOwnershipRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedOwnershipRevision));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            if (!actorIsMainGm)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterOwnershipDenied(correlationId));
            }

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

                        if (current.Revisions.OwnershipRevision != expectedOwnershipRevision)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(PersistenceFailures.CharacterRevisionConflict(correlationId));
                        }

                        (CharacterOwnership newOwnership, string eventType, JObject payloadExtra) = mutate(current);

                        UtcInstant now = _clock.GetUtcNow();
                        long newOwnershipRevision = current.Revisions.OwnershipRevision + 1;
                        long newCharacterRevision = current.Revisions.CharacterRevision + 1;

                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText = "UPDATE Character SET PrimaryOwnerUserId = $primaryOwnerUserId, CoOwnerUserIdsJson = $coOwners, PermanentControllerUserIdsJson = $permanentControllers, TemporaryControlGrantsJson = $temporaryGrants, OwnershipRevision = $ownershipRevision, CharacterRevision = $characterRevision, UpdatedAt = $updatedAt, LastCommandId = $lastCommandId WHERE CharacterId = $characterId;";
                            update.Parameters.AddWithValue("$primaryOwnerUserId", (object?)newOwnership.PrimaryOwnerUserId?.ToString() ?? DBNull.Value);
                            update.Parameters.AddWithValue("$coOwners", SerializeUserIds(newOwnership.CoOwnerUserIds));
                            update.Parameters.AddWithValue("$permanentControllers", SerializeUserIds(newOwnership.PermanentControllerUserIds));
                            update.Parameters.AddWithValue("$temporaryGrants", SerializeGrants(newOwnership.TemporaryControlGrants));
                            update.Parameters.AddWithValue("$ownershipRevision", newOwnershipRevision);
                            update.Parameters.AddWithValue("$characterRevision", newCharacterRevision);
                            update.Parameters.AddWithValue("$updatedAt", now.ToString());
                            update.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            update.Parameters.AddWithValue("$characterId", characterId.ToString());
                            update.ExecuteNonQuery();
                        }

                        CharacterSectionRevisions newRevisions = WithRevisions(current.Revisions, characterRevision: newCharacterRevision, ownershipRevision: newOwnershipRevision);
                        var record = new CharacterRecord(characterId, campaign.CampaignId, current.CharacterKind, current.LifecycleStatus, current.ApprovalState, current.DisplayName, current.PortraitReference, newOwnership, newRevisions, current.RulesetVersion, current.AnatomyProfileRef, current.TemplateId, current.TemplateVersionAtCopyTime, current.SeedCopy, current.SubmittedAt, current.DevelopmentPool, current.Attributes, current.CreatedAt, now);

                        payloadExtra["characterId"] = characterId.ToString();
                        payloadExtra["displayNameSnapshot"] = current.DisplayName;
                        payloadExtra["newOwnershipRevision"] = newOwnershipRevision;
                        payloadExtra["newCharacterRevision"] = newCharacterRevision;

                        return Result<PipelineWrite<CharacterRecord>>.Success(new PipelineWrite<CharacterRecord>(
                            record, eventType, payloadExtra.ToString(Newtonsoft.Json.Formatting.None), characterId.ToString(),
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
                    var placeholders = new List<string>();
                    for (int index = 0; index < HistoryEventTypes.Length; index++)
                    {
                        string parameterName = "$t" + index;
                        placeholders.Add(parameterName);
                    }

                    select.CommandText =
                        "SELECT EventSequence, EventType, PayloadJson, CreatedAtHost FROM DomainEvents " +
                        "WHERE CampaignId = $campaignId AND EventType IN (" + string.Join(", ", placeholders) + ") ORDER BY EventSequence;";
                    select.Parameters.AddWithValue("$campaignId", campaign.CampaignId.ToString());
                    for (int index = 0; index < HistoryEventTypes.Length; index++)
                    {
                        select.Parameters.AddWithValue("$t" + index, HistoryEventTypes[index]);
                    }

                    using SqliteDataReader reader = select.ExecuteReader();
                    while (reader.Read())
                    {
                        long eventSequence = reader.GetInt64(0);
                        string eventType = reader.GetString(1);
                        string payloadJson = reader.GetString(2);
                        UtcInstant occurredAt = UtcInstant.Parse(reader.GetString(3));

                        JObject payload = (JObject)ParseJsonPreservingStrings(payloadJson);
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
            "PrimaryOwnerUserId, CoOwnerUserIdsJson, PermanentControllerUserIdsJson, TemporaryControlGrantsJson, " +
            "CharacterRevision, IdentityRevision, PresentationRevision, CustomFieldsRevision, MechanicsRevision, " +
            "AttributeValuesRevision, CharacterSkillsRevision, CharacterAbilitiesRevision, CharacterResourcesRevision, " +
            "CharacterAnatomyRevision, OwnershipRevision, LifecycleRevision, RuntimeStateRevision, " +
            "RulesetVersion, AnatomyProfileRef, TemplateId, TemplateVersionAtCopyTime, SeedCopyJson, SubmittedAt, " +
            "PoolEarned, PoolSpent, PoolReserved, AttributesJson, CreatedAt, UpdatedAt";

        /// <summary>
        /// ODY-S04-101/102: shared column-order contract for every SELECT
        /// against <c>Character</c> that returns a full row, matching
        /// <see cref="SelectColumns"/>'s exact order -- the same "one shared
        /// column list, every caller uses it" convention
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
            UserId? primaryOwnerUserId = reader.IsDBNull(7) ? (UserId?)null : UserId.Parse(reader.GetString(7));
            IReadOnlyList<UserId> coOwnerUserIds = DeserializeUserIds(reader.GetString(8));
            IReadOnlyList<UserId> permanentControllerUserIds = DeserializeUserIds(reader.GetString(9));
            IReadOnlyList<CharacterTemporaryControlGrant> temporaryControlGrants = DeserializeGrants(reader.GetString(10));
            var ownership = new CharacterOwnership(primaryOwnerUserId, coOwnerUserIds, permanentControllerUserIds, temporaryControlGrants);

            var revisions = new CharacterSectionRevisions(
                characterRevision: reader.GetInt64(11),
                identityRevision: reader.GetInt64(12),
                presentationRevision: reader.GetInt64(13),
                customFieldsRevision: reader.GetInt64(14),
                mechanicsRevision: reader.GetInt64(15),
                attributeValuesRevision: reader.GetInt64(16),
                characterSkillsRevision: reader.GetInt64(17),
                characterAbilitiesRevision: reader.GetInt64(18),
                characterResourcesRevision: reader.GetInt64(19),
                characterAnatomyRevision: reader.GetInt64(20),
                ownershipRevision: reader.GetInt64(21),
                lifecycleRevision: reader.GetInt64(22),
                runtimeStateRevision: reader.GetInt64(23));
            string rulesetVersion = reader.GetString(24);
            string? anatomyProfileRef = reader.IsDBNull(25) ? null : reader.GetString(25);
            CharacterTemplateId? templateId = reader.IsDBNull(26) ? (CharacterTemplateId?)null : CharacterTemplateId.Parse(reader.GetString(26));
            long? templateVersionAtCopyTime = reader.IsDBNull(27) ? (long?)null : reader.GetInt64(27);
            IReadOnlyList<CopiedCharacterSeedItem> seedCopy = SqliteLocalCharacterDraftRepository.DeserializeSeedCopy(reader.GetString(28));
            UtcInstant? submittedAt = reader.IsDBNull(29) ? (UtcInstant?)null : UtcInstant.Parse(reader.GetString(29));
            var developmentPool = new DevelopmentPool(reader.GetInt64(30), reader.GetInt64(31), reader.GetInt64(32));
            IReadOnlyList<AttributeValue> attributes = DeserializeAttributes(reader.GetString(33));
            UtcInstant createdAt = UtcInstant.Parse(reader.GetString(34));
            UtcInstant updatedAt = UtcInstant.Parse(reader.GetString(35));

            return new CharacterRecord(characterId, campaignId, characterKind, lifecycleStatus, approvalState, displayName, portraitReference, ownership, revisions, rulesetVersion, anatomyProfileRef, templateId, templateVersionAtCopyTime, seedCopy, submittedAt, developmentPool, attributes, createdAt, updatedAt);
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

        /// <summary>
        /// ODY-S04-102: replaces <c>ODY-S04-101</c>'s narrower
        /// <c>WithIdentityRevision</c>/<c>WithPresentationRevision</c>
        /// helpers with one shared "copy with named overrides" function, now
        /// that a third section (<c>Ownership</c>) needs the identical
        /// pattern -- avoids a fourth/fifth near-duplicate helper as later
        /// tasks add their own sections.
        /// </summary>
        private static CharacterSectionRevisions WithRevisions(
            CharacterSectionRevisions source,
            long? characterRevision = null,
            long? identityRevision = null,
            long? presentationRevision = null,
            long? ownershipRevision = null,
            long? lifecycleRevision = null,
            long? mechanicsRevision = null) => new CharacterSectionRevisions(
                characterRevision ?? source.CharacterRevision,
                identityRevision ?? source.IdentityRevision,
                presentationRevision ?? source.PresentationRevision,
                source.CustomFieldsRevision,
                mechanicsRevision ?? source.MechanicsRevision,
                source.AttributeValuesRevision,
                source.CharacterSkillsRevision,
                source.CharacterAbilitiesRevision,
                source.CharacterResourcesRevision,
                source.CharacterAnatomyRevision,
                ownershipRevision ?? source.OwnershipRevision,
                lifecycleRevision ?? source.LifecycleRevision,
                source.RuntimeStateRevision);

        private static string SerializeAttributes(IReadOnlyList<AttributeValue> attributes)
        {
            var array = new JArray();
            foreach (AttributeValue attribute in attributes)
            {
                array.Add(new JObject
                {
                    ["attributeDefinitionId"] = attribute.AttributeDefinitionId.ToString(),
                    ["baseValue"] = attribute.BaseValue,
                    ["permanentAdjustment"] = attribute.PermanentAdjustment,
                    ["spentDevelopmentPoints"] = attribute.SpentDevelopmentPoints,
                    ["revision"] = attribute.Revision,
                });
            }

            return array.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static IReadOnlyList<AttributeValue> DeserializeAttributes(string json)
        {
            var array = (JArray)ParseJsonPreservingStrings(json);
            var list = new List<AttributeValue>(array.Count);
            foreach (JToken token in array)
            {
                AttributeDefinitionId attributeDefinitionId = AttributeDefinitionId.Parse((string)token["attributeDefinitionId"]!);
                long baseValue = (long)token["baseValue"]!;
                long permanentAdjustment = (long)token["permanentAdjustment"]!;
                long spentDevelopmentPoints = (long)token["spentDevelopmentPoints"]!;
                long revision = (long)token["revision"]!;
                list.Add(new AttributeValue(attributeDefinitionId, baseValue, permanentAdjustment, spentDevelopmentPoints, revision));
            }

            return list;
        }

        private static string SerializeUserIds(IReadOnlyList<UserId> userIds)
        {
            var array = new JArray();
            foreach (UserId userId in userIds)
            {
                array.Add(userId.ToString());
            }

            return array.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static IReadOnlyList<UserId> DeserializeUserIds(string json)
        {
            var array = (JArray)ParseJsonPreservingStrings(json);
            var list = new List<UserId>(array.Count);
            foreach (JToken item in array)
            {
                list.Add(UserId.Parse((string)item!));
            }

            return list;
        }

        private static string SerializeGrants(IReadOnlyList<CharacterTemporaryControlGrant> grants)
        {
            var array = new JArray();
            foreach (CharacterTemporaryControlGrant grant in grants)
            {
                array.Add(new JObject
                {
                    ["userId"] = grant.UserId.ToString(),
                    ["grantedAt"] = grant.GrantedAt.ToString(),
                    ["expiresAt"] = grant.ExpiresAt?.ToString(),
                });
            }

            return array.ToString(Newtonsoft.Json.Formatting.None);
        }

        /// <summary>
        /// Newtonsoft's default <c>JArray.Parse</c>/<c>JObject.Parse</c>
        /// (which use a plain <c>JsonTextReader</c> internally with its own
        /// default <c>DateParseHandling.DateTime</c>) auto-detect date-like
        /// strings and silently convert them into <c>JTokenType.Date</c>
        /// tokens -- reading one back with a plain string cast then
        /// reformats it using .NET's own culture-default
        /// <c>DateTimeOffset.ToString()</c>, corrupting <see cref="UtcInstant"/>'s
        /// exact round-trip format. <see cref="JsonLoadSettings"/> has no
        /// <c>DateParseHandling</c> property in the approved Newtonsoft.Json
        /// 13.0.2 (<c>ADR-003</c>) -- the reader itself must be configured
        /// directly, which is what this helper does, keeping every JSON
        /// string field a plain string parsed explicitly by this
        /// repository's own <see cref="UtcInstant.Parse"/> only.
        /// </summary>
        internal static JToken ParseJsonPreservingStrings(string json)
        {
            using var stringReader = new StringReader(json);
            using var jsonReader = new Newtonsoft.Json.JsonTextReader(stringReader) { DateParseHandling = Newtonsoft.Json.DateParseHandling.None };
            return JToken.Load(jsonReader);
        }

        private static IReadOnlyList<CharacterTemporaryControlGrant> DeserializeGrants(string json)
        {
            var array = (JArray)ParseJsonPreservingStrings(json);
            var list = new List<CharacterTemporaryControlGrant>(array.Count);
            foreach (JToken item in array)
            {
                UserId userId = UserId.Parse((string)item["userId"]!);
                UtcInstant grantedAt = UtcInstant.Parse((string)item["grantedAt"]!);
                UtcInstant? expiresAt = item["expiresAt"]!.Type == JTokenType.Null ? (UtcInstant?)null : UtcInstant.Parse((string)item["expiresAt"]!);
                list.Add(new CharacterTemporaryControlGrant(userId, grantedAt, expiresAt));
            }

            return list;
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
    PrimaryOwnerUserId TEXT,
    CoOwnerUserIdsJson TEXT NOT NULL DEFAULT '[]',
    PermanentControllerUserIdsJson TEXT NOT NULL DEFAULT '[]',
    TemporaryControlGrantsJson TEXT NOT NULL DEFAULT '[]',
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
    RulesetVersion TEXT NOT NULL DEFAULT '',
    AnatomyProfileRef TEXT,
    TemplateId TEXT,
    TemplateVersionAtCopyTime INTEGER,
    SeedCopyJson TEXT NOT NULL DEFAULT '[]',
    SubmittedAt TEXT,
    PoolEarned INTEGER NOT NULL DEFAULT 0,
    PoolSpent INTEGER NOT NULL DEFAULT 0,
    PoolReserved INTEGER NOT NULL DEFAULT 0,
    AttributesJson TEXT NOT NULL DEFAULT '[]',
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    LastCommandId TEXT NOT NULL
);";
            command.ExecuteNonQuery();

            using var developmentTransactionTable = connection.CreateCommand();
            developmentTransactionTable.CommandText = @"
CREATE TABLE IF NOT EXISTS DevelopmentTransaction (
    TransactionId TEXT PRIMARY KEY,
    CampaignId TEXT NOT NULL,
    CharacterId TEXT NOT NULL,
    Kind TEXT NOT NULL,
    Amount INTEGER NOT NULL,
    SourceRef TEXT,
    Reason TEXT NOT NULL,
    ActorUserId TEXT NOT NULL,
    RulesetVersion TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    CorrelationId TEXT NOT NULL
);";
            developmentTransactionTable.ExecuteNonQuery();

            using var reviewCommentTable = connection.CreateCommand();
            reviewCommentTable.CommandText = @"
CREATE TABLE IF NOT EXISTS CharacterReviewComment (
    CommentId TEXT PRIMARY KEY,
    CampaignId TEXT NOT NULL,
    CharacterId TEXT NOT NULL,
    AuthorUserId TEXT NOT NULL,
    Text TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    ResolvedAt TEXT,
    CommandId TEXT NOT NULL
);";
            reviewCommentTable.ExecuteNonQuery();
        }
    }
}
