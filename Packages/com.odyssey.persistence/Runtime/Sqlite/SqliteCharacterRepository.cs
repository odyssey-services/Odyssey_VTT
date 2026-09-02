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
using RulesSkillCostRules = Odyssey.Rules.Character.SkillCostRules;
using RulesAbilityCostRules = Odyssey.Rules.Character.AbilityCostRules;
using RulesResourceInitializationRules = Odyssey.Rules.Character.ResourceInitializationRules;
using RulesAnatomyInitializationRules = Odyssey.Rules.Character.AnatomyInitializationRules;

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
        private readonly IBackupRepository _backupRepository;
        private readonly IReadOnlyList<ICharacterDeletionDependencyChecker> _deletionDependencyCheckers;

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
            "odyssey.persistence.character_archived",
            "odyssey.persistence.character_deleted",
        };

        /// <summary>
        /// ODY-S04-110 section 1.1/1.2: <paramref name="deletionDependencyCheckers"/>
        /// defaults to an empty list -- no Board/Item/GameLog cross-reference
        /// to CharacterId exists anywhere in this codebase yet (confirmed by
        /// search), so there is genuinely nothing to check today; a future
        /// task passes its own real <see cref="ICharacterDeletionDependencyChecker"/>
        /// implementations here without changing <c>DeleteCharacterPermanently</c>'s
        /// own shape. <paramref name="backupRepository"/> defaults to a
        /// plain <see cref="SqliteBackupRepository"/> constructed from the
        /// same clock -- mirrors <see cref="_pipeline"/>'s own
        /// self-construction convention, so every pre-existing caller of
        /// this single-argument constructor is unaffected; a caller that
        /// needs to observe/substitute the backup step (e.g. a test) can
        /// still pass one explicitly.
        /// </summary>
        public SqliteCharacterRepository(IWallClock clock, IBackupRepository? backupRepository = null, IReadOnlyList<ICharacterDeletionDependencyChecker>? deletionDependencyCheckers = null)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _pipeline = new SqliteSavingPipeline(clock);
            _backupRepository = backupRepository ?? new SqliteBackupRepository(clock);
            _deletionDependencyCheckers = deletionDependencyCheckers ?? Array.Empty<ICharacterDeletionDependencyChecker>();
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
                                "PoolEarned, PoolSpent, PoolReserved, AttributesJson, SkillsJson, AbilitiesJson, ResourcesJson, AnatomyJson, " +
                                "CreatedAt, UpdatedAt, LastCommandId) VALUES (" +
                                "$characterId, $campaignId, $characterKind, $lifecycleStatus, $approvalState, $displayName, NULL, " +
                                "NULL, $coOwners, $permanentControllers, $temporaryGrants, " +
                                "$characterRevision, $identityRevision, $presentationRevision, $customFieldsRevision, $mechanicsRevision, " +
                                "$attributeValuesRevision, $characterSkillsRevision, $characterAbilitiesRevision, $characterResourcesRevision, " +
                                "$characterAnatomyRevision, $ownershipRevision, $lifecycleRevision, $runtimeStateRevision, " +
                                "'', NULL, NULL, NULL, '[]', NULL, " +
                                "0, 0, 0, '[]', '[]', '[]', '[]', NULL, " +
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
                        var record = new CharacterRecord(characterId, campaign.CampaignId, request.CharacterKind, lifecycleStatus, approvalState, request.DisplayName, null, ownership, revisions, string.Empty, null, null, null, Array.Empty<CopiedCharacterSeedItem>(), null, DevelopmentPool.Empty(), Array.Empty<AttributeValue>(), Array.Empty<CharacterSkill>(), Array.Empty<CharacterAbility>(), Array.Empty<CharacterResource>(), null, now, now);

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
                                "PoolEarned, PoolSpent, PoolReserved, AttributesJson, SkillsJson, AbilitiesJson, ResourcesJson, AnatomyJson, " +
                                "CreatedAt, UpdatedAt, LastCommandId) VALUES (" +
                                "$characterId, $campaignId, $characterKind, $lifecycleStatus, $approvalState, $displayName, NULL, " +
                                "$primaryOwnerUserId, $coOwners, $permanentControllers, $temporaryGrants, " +
                                "$characterRevision, $identityRevision, $presentationRevision, $customFieldsRevision, $mechanicsRevision, " +
                                "$attributeValuesRevision, $characterSkillsRevision, $characterAbilitiesRevision, $characterResourcesRevision, " +
                                "$characterAnatomyRevision, $ownershipRevision, $lifecycleRevision, $runtimeStateRevision, " +
                                "$rulesetVersion, $anatomyProfileRef, $templateId, $templateVersion, $seedCopyJson, NULL, " +
                                "0, 0, 0, '[]', '[]', '[]', '[]', NULL, " +
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

                        var record = new CharacterRecord(characterId, campaign.CampaignId, request.CharacterKind, lifecycleStatus, approvalState, request.DisplayName, null, ownership, revisions, pinnedRulesetVersion, request.AnatomyProfileRef, request.Seed.TemplateId, request.Seed.TemplateVersionAtCopyTime, request.Seed.Items, null, DevelopmentPool.Empty(), Array.Empty<AttributeValue>(), Array.Empty<CharacterSkill>(), Array.Empty<CharacterAbility>(), Array.Empty<CharacterResource>(), null, now, now);

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
                        var record = new CharacterRecord(characterId, campaign.CampaignId, current.CharacterKind, current.LifecycleStatus, current.ApprovalState, current.DisplayName, current.PortraitReference, current.Ownership, newRevisions, current.RulesetVersion, current.AnatomyProfileRef, current.TemplateId, current.TemplateVersionAtCopyTime, current.SeedCopy, now, current.DevelopmentPool, current.Attributes, current.Skills, current.Abilities, current.Resources, current.Anatomy, current.CreatedAt, now);

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
                        var record = new CharacterRecord(characterId, campaign.CampaignId, current.CharacterKind, newLifecycleStatus, newApprovalState, current.DisplayName, current.PortraitReference, current.Ownership, newRevisions, current.RulesetVersion, current.AnatomyProfileRef, current.TemplateId, current.TemplateVersionAtCopyTime, current.SeedCopy, current.SubmittedAt, current.DevelopmentPool, current.Attributes, current.Skills, current.Abilities, current.Resources, current.Anatomy, current.CreatedAt, now);

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

        public Result<CharacterRecord> ArchiveCharacter(CampaignHandle campaign, CharacterId characterId, UserId actorUserId, bool actorIsMainGm, long expectedLifecycleRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
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

                        if (current.Revisions.LifecycleRevision != expectedLifecycleRevision)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(PersistenceFailures.CharacterRevisionConflict(correlationId));
                        }

                        // ADR-025 section 5.1: MainGM OR an assigned user of
                        // this Character -- deliberately NOT MainGM-only
                        // (see this method's own interface doc comment for
                        // the full justification). Checked after loading
                        // current state (needs Ownership to evaluate
                        // IsAssignedCharacter), unlike the MainGM-only gates
                        // elsewhere in this file that are checked before
                        // touching the database at all.
                        bool permitted = actorIsMainGm || CharacterOwnershipAssignment.IsAssignedCharacter(current.Ownership, actorUserId, _clock.GetUtcNow());
                        if (!permitted)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(PersistenceFailures.CharacterArchiveDenied(correlationId));
                        }

                        // The sole state-legality gate: the generic
                        // ADR-022/ODY-S04-101 transition table (already
                        // covers every "-> Archived" edge product section
                        // 7.1 names, including Archived -> Archived being
                        // illegal), not a duplicated ad hoc check.
                        if (!CharacterLifecycleTransitions.IsValidTransition(current.LifecycleStatus, CharacterLifecycleStatus.Archived))
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(PersistenceFailures.CharacterLifecycleTransitionInvalid(correlationId));
                        }

                        UtcInstant now = _clock.GetUtcNow();
                        long newLifecycleRevision = current.Revisions.LifecycleRevision + 1;
                        long newCharacterRevision = current.Revisions.CharacterRevision + 1;
                        CharacterLifecycleStatus lifecycleStatusBefore = current.LifecycleStatus;

                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText = "UPDATE Character SET LifecycleStatus = $lifecycleStatus, LifecycleRevision = $lifecycleRevision, CharacterRevision = $characterRevision, UpdatedAt = $updatedAt, LastCommandId = $lastCommandId WHERE CharacterId = $characterId;";
                            update.Parameters.AddWithValue("$lifecycleStatus", CharacterLifecycleStatus.Archived.ToString());
                            update.Parameters.AddWithValue("$lifecycleRevision", newLifecycleRevision);
                            update.Parameters.AddWithValue("$characterRevision", newCharacterRevision);
                            update.Parameters.AddWithValue("$updatedAt", now.ToString());
                            update.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            update.Parameters.AddWithValue("$characterId", characterId.ToString());
                            update.ExecuteNonQuery();
                        }

                        CharacterSectionRevisions newRevisions = WithRevisions(current.Revisions, characterRevision: newCharacterRevision, lifecycleRevision: newLifecycleRevision);
                        var record = new CharacterRecord(characterId, campaign.CampaignId, current.CharacterKind, CharacterLifecycleStatus.Archived, current.ApprovalState, current.DisplayName, current.PortraitReference, current.Ownership, newRevisions, current.RulesetVersion, current.AnatomyProfileRef, current.TemplateId, current.TemplateVersionAtCopyTime, current.SeedCopy, current.SubmittedAt, current.DevelopmentPool, current.Attributes, current.Skills, current.Abilities, current.Resources, current.Anatomy, current.CreatedAt, now);

                        var payload = new JObject
                        {
                            ["characterId"] = characterId.ToString(),
                            ["displayNameSnapshot"] = current.DisplayName,
                            ["portraitReferenceSnapshot"] = current.PortraitReference,
                            ["rulesetVersion"] = current.RulesetVersion,
                            ["lifecycleStatusBefore"] = lifecycleStatusBefore.ToString(),
                            ["lifecycleStatusAfter"] = CharacterLifecycleStatus.Archived.ToString(),
                            ["actorUserId"] = actorUserId.ToString(),
                            ["newLifecycleRevision"] = newLifecycleRevision,
                            ["newCharacterRevision"] = newCharacterRevision,
                        };

                        return Result<PipelineWrite<CharacterRecord>>.Success(new PipelineWrite<CharacterRecord>(
                            record, "odyssey.persistence.character_archived", payload.ToString(Newtonsoft.Json.Formatting.None), characterId.ToString(),
                            aggregateType: "character", aggregateId: characterId.ToString(), aggregateRevision: newCharacterRevision));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterIoFailed(correlationId));
            }
        }

        /// <summary>
        /// ODY-S04-110: ADR-025 section 5.2. Order of operations: (1) cheap
        /// validation and the MainGM/ReasonCode gates, before touching the
        /// database at all; (2) a replay short-circuit -- if this exact
        /// `CommandId` was already applied (the live row is gone by then,
        /// so the ordinary pre-check below would otherwise misreport
        /// `CharacterNotFound` for a legitimate duplicate delivery), skip
        /// straight to the pipeline, whose own `AppliedCommands` lookup
        /// replays the stored result; (3) otherwise, a non-authoritative
        /// pre-check (existence, `LifecycleRevision`, dependency checkers)
        /// purely to avoid paying for a full campaign backup ahead of an
        /// already-doomed request -- this pre-check is NOT the source of
        /// truth; (4) the backup itself (section 1.2, outside any open
        /// transaction on this repository's own connection, so it never
        /// contends with our own write lock); (5) the actual delete
        /// transaction, which re-validates existence/`LifecycleRevision`/
        /// dependencies for real (ADR-025 section 5.2's own "regardless of
        /// what a client-side preview showed" host authority) immediately
        /// before the irreversible commit.
        /// </summary>
        public Result DeleteCharacterPermanently(CampaignHandle campaign, CharacterId characterId, string reasonCode, UserId actorUserId, bool actorIsMainGm, long expectedLifecycleRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (expectedLifecycleRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedLifecycleRevision));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            // Product section 22.2: "доступно только MainGM" -- checked
            // before touching the database at all, matching every other
            // MainGM-only gate's own convention.
            if (!actorIsMainGm)
            {
                return Result.Failure(PersistenceFailures.CharacterDeletionDenied(correlationId));
            }

            if (string.IsNullOrWhiteSpace(reasonCode))
            {
                return Result.Failure(PersistenceFailures.CharacterDeletionReasonRequired(correlationId));
            }

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureCharacterTables(connection);

                BackupRecord? backupRecord = null;

                if (!IsCommandAlreadyApplied(connection, commandId))
                {
                    // Non-authoritative pre-check -- see this method's own
                    // doc comment. Avoids creating a real campaign backup
                    // for a request that is already invalid on its face.
                    Result<CharacterRecord> preCheck = GetCharacter(campaign, characterId, correlationId);
                    if (preCheck.IsFailure)
                    {
                        return Result.Failure(preCheck.Error);
                    }

                    if (preCheck.Value.Revisions.LifecycleRevision != expectedLifecycleRevision)
                    {
                        return Result.Failure(PersistenceFailures.CharacterRevisionConflict(correlationId));
                    }

                    foreach (ICharacterDeletionDependencyChecker checker in _deletionDependencyCheckers)
                    {
                        string? blockingDependency = checker.CheckBlockingDependency(campaign, characterId);
                        if (blockingDependency != null)
                        {
                            return Result.Failure(PersistenceFailures.CharacterDeletionHasDependent(correlationId));
                        }
                    }

                    // ADR-025 section 5.2 / product section 22.2 step 4: a
                    // full campaign backup before the irreversible delete
                    // commits. Reuses the existing
                    // IBackupRepository/SqliteBackupRepository
                    // (ODY-S01-011) -- not a new, Character-specific
                    // mechanism. Runs on its own connection/transaction,
                    // before the delete transaction below even opens, so it
                    // never contends with this repository's own write lock
                    // on the same database file.
                    Result<BackupRecord> backup = _backupRepository.CreateBackup(campaign, "pre-delete-character:" + characterId, correlationId);
                    if (backup.IsFailure)
                    {
                        return Result.Failure(backup.Error);
                    }

                    backupRecord = backup.Value;
                }

                Result<Unit> pipelineResult = _pipeline.Execute(
                    connection,
                    campaign.CampaignId,
                    commandId,
                    correlationId,
                    tryReplay: transaction => ReplayCharacterDeleted(connection, transaction, commandId, correlationId),
                    apply: transaction =>
                    {
                        CharacterRecord? current = SelectForUpdate(connection, transaction, characterId);
                        if (current == null)
                        {
                            return Result<PipelineWrite<Unit>>.Failure(PersistenceFailures.CharacterNotFound(correlationId));
                        }

                        if (current.Revisions.LifecycleRevision != expectedLifecycleRevision)
                        {
                            return Result<PipelineWrite<Unit>>.Failure(PersistenceFailures.CharacterRevisionConflict(correlationId));
                        }

                        // ADR-025 section 5.2: host-authoritative re-check,
                        // immediately before commit, regardless of what the
                        // pre-check above (or any client-side preview)
                        // already showed.
                        foreach (ICharacterDeletionDependencyChecker checker in _deletionDependencyCheckers)
                        {
                            string? blockingDependency = checker.CheckBlockingDependency(campaign, characterId);
                            if (blockingDependency != null)
                            {
                                return Result<PipelineWrite<Unit>>.Failure(PersistenceFailures.CharacterDeletionHasDependent(correlationId));
                            }
                        }

                        // Removes the Character's live current-state row.
                        // Direct search confirms no other table in this
                        // codebase stores a live CharacterId cross-reference
                        // today -- nothing else to delete.
                        using (var delete = connection.CreateCommand())
                        {
                            delete.Transaction = transaction;
                            delete.CommandText = "DELETE FROM Character WHERE CharacterId = $characterId;";
                            delete.Parameters.AddWithValue("$characterId", characterId.ToString());
                            delete.ExecuteNonQuery();
                        }

                        long newCharacterRevision = current.Revisions.CharacterRevision + 1;

                        // ADR-022 section 7's minimum historical snapshot
                        // (product section 22.3, verbatim field list) -- no
                        // full Character-sheet copy.
                        var payload = new JObject
                        {
                            ["characterId"] = characterId.ToString(),
                            ["displayNameSnapshot"] = current.DisplayName,
                            ["portraitReferenceSnapshot"] = current.PortraitReference,
                            ["rulesetVersion"] = current.RulesetVersion,
                            ["relevantValueSnapshots"] = new JObject
                            {
                                ["lifecycleStatusBefore"] = current.LifecycleStatus.ToString(),
                            },
                            ["reasonCode"] = reasonCode,
                            ["actorUserId"] = actorUserId.ToString(),
                            ["backupId"] = backupRecord!.BackupId.ToString(),
                        };

                        return Result<PipelineWrite<Unit>>.Success(new PipelineWrite<Unit>(
                            Unit.Value, "odyssey.persistence.character_deleted", payload.ToString(Newtonsoft.Json.Formatting.None), characterId.ToString(),
                            aggregateType: "character", aggregateId: characterId.ToString(), aggregateRevision: newCharacterRevision));
                    });

                return pipelineResult.IsSuccess ? Result.Success() : Result.Failure(pipelineResult.Error);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result.Failure(PersistenceFailures.CharacterIoFailed(correlationId));
            }
        }

        /// <summary>ODY-S04-110: <c>DeleteCharacterPermanently</c>'s own replay lookup -- the live `Character` row is gone after a successful delete, so (unlike every other command's `ReplayCharacter`) this checks `DomainEvents` directly for a prior `character_deleted` event carrying this exact `CommandId`.</summary>
        private static Result<Unit> ReplayCharacterDeleted(SqliteConnection connection, SqliteTransaction transaction, CommandId commandId, CorrelationId correlationId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT COUNT(*) FROM DomainEvents WHERE CommandId = $commandId AND EventType = 'odyssey.persistence.character_deleted';";
            select.Parameters.AddWithValue("$commandId", commandId.ToString());
            long count = Convert.ToInt64(select.ExecuteScalar());
            return count > 0 ? Result<Unit>.Success(Unit.Value) : Result<Unit>.Failure(PersistenceFailures.CommandReplayFailed(correlationId));
        }

        /// <summary>
        /// ODY-S04-110: <c>DeleteCharacterPermanently</c>'s own pre-transaction
        /// replay check -- after a successful delete the live `Character`
        /// row is gone, so an ordinary <c>GetCharacter</c>-based pre-check
        /// would misreport <c>CharacterNotFound</c> for a legitimate
        /// duplicate delivery of the same <c>CommandId</c> instead of
        /// letting <see cref="SqliteSavingPipeline.Execute{T}"/>'s own
        /// `AppliedCommands` lookup replay the stored result. Checked
        /// before the pre-check/backup so neither runs a second time for a
        /// duplicate.
        /// </summary>
        private static bool IsCommandAlreadyApplied(SqliteConnection connection, CommandId commandId)
        {
            using var select = connection.CreateCommand();
            select.CommandText = "SELECT COUNT(*) FROM AppliedCommands WHERE CommandId = $commandId AND Status = 'Completed';";
            select.Parameters.AddWithValue("$commandId", commandId.ToString());
            long count = Convert.ToInt64(select.ExecuteScalar());
            return count > 0;
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

            return MutateMechanics(campaign, characterId, expectedMechanicsRevision, commandId, correlationId, (current, connection, transaction) =>
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
                    newPool, current.Attributes, current.Skills, "odyssey.persistence.character_development_points_granted", payload, new[] { ledgerEntry }));
            });
        }

        public Result<CharacterRecord> PurchaseAttributeIncrease(CampaignHandle campaign, CharacterId characterId, AttributeDefinitionId attributeDefinitionId, long toValue, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, long expectedAttributeRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (!attributeDefinitionId.IsValid) throw new ArgumentException("AttributeDefinitionId is required.", nameof(attributeDefinitionId));
            if (toValue < 0) throw new ArgumentOutOfRangeException(nameof(toValue));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (expectedAttributeRevision < 0) throw new ArgumentOutOfRangeException(nameof(expectedAttributeRevision));

            return MutateMechanics(campaign, characterId, expectedMechanicsRevision, commandId, correlationId, (current, connection, transaction) =>
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

                // ODY-S04-107 (pkt 0 gap fix): ADR-024 section 3.3/5.1 step 4
                // -- every successful purchase co-commits an AdvancementPurchase
                // record. Business logic above is unchanged; this only adds
                // the record, per the task's own "не переоткрывай их бизнес-
                // логику покупки, только добавь запись" instruction.
                AdvancementPurchaseId purchaseId = AdvancementPurchaseId.NewId(now);
                var purchase = new AdvancementPurchase(purchaseId, characterId, AdvancementOperationKind.AttributeIncrease, attributeDefinitionId.ToString(), fromValue, toValue, cost, "{}", campaign.Manifest.RulesetVersion, actorUserId, now, AdvancementPurchaseStatus.Applied);
                InsertAdvancementPurchase(connection, transaction, campaign.CampaignId, purchase);

                var payload = new JObject
                {
                    ["attributeDefinitionId"] = attributeDefinitionId.ToString(),
                    ["fromValue"] = fromValue,
                    ["toValue"] = toValue,
                    ["cost"] = cost,
                    ["newEffectiveValue"] = newAttribute.EffectiveValue,
                    ["actorUserId"] = actorUserId.ToString(),
                    ["newAvailable"] = newPool.Available,
                    ["purchaseId"] = purchaseId.ToString(),
                };

                return Result<MechanicsMutation>.Success(new MechanicsMutation(
                    newPool, newAttributes, current.Skills, "odyssey.persistence.character_attribute_increased", payload, new[] { ledgerEntry }));
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

        public Result<CharacterRecord> PurchaseSkillLevel(CampaignHandle campaign, CharacterId characterId, SkillDefinitionId skillDefinitionId, long toLevel, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, long expectedSkillRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (!skillDefinitionId.IsValid) throw new ArgumentException("SkillDefinitionId is required.", nameof(skillDefinitionId));
            if (toLevel < 0) throw new ArgumentOutOfRangeException(nameof(toLevel));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (expectedSkillRevision < 0) throw new ArgumentOutOfRangeException(nameof(expectedSkillRevision));

            return MutateMechanics(campaign, characterId, expectedMechanicsRevision, commandId, correlationId, (current, connection, transaction) =>
            {
                UtcInstant now = _clock.GetUtcNow();

                // Product section 13.1's permission framing, reused unchanged for skills.
                bool permitted = actorIsMainGm || CharacterOwnershipAssignment.IsAssignedCharacter(current.Ownership, actorUserId, now);
                if (!permitted)
                {
                    return Result<MechanicsMutation>.Failure(PersistenceFailures.CharacterDevelopmentPurchaseDenied(correlationId));
                }

                // Product sections 14.2/14.3: level 5+ is the recommendation/
                // reservation pipeline's own job, never this ordinary
                // immediate-purchase command's.
                if (RulesSkillCostRules.RequiresRecommendation(toLevel))
                {
                    return Result<MechanicsMutation>.Failure(PersistenceFailures.CharacterSkillLevelRequiresRecommendation(correlationId));
                }

                CharacterSkill? existing = null;
                foreach (CharacterSkill candidate in current.Skills)
                {
                    if (candidate.SkillDefinitionId.Equals(skillDefinitionId)) { existing = candidate; break; }
                }

                long fromLevel = existing?.Level ?? 0;
                long currentSkillRevision = existing?.Revision ?? 0;

                // ADR-024 section 4.2's entry-level gate for CharacterSkill,
                // independent of MechanicsRevision -- 0 for a skill never
                // purchased before ("отсутствующий навык представлен
                // отсутствием CharacterSkill", product section 14).
                if (currentSkillRevision != expectedSkillRevision)
                {
                    return Result<MechanicsMutation>.Failure(PersistenceFailures.CharacterRevisionConflict(correlationId));
                }

                if (toLevel <= fromLevel)
                {
                    throw new ArgumentOutOfRangeException(nameof(toLevel), "ToLevel must exceed the skill's current Level for an increase.");
                }

                long cost = RulesSkillCostRules.CostForIncrease(fromLevel, toLevel);
                if (cost > current.DevelopmentPool.Available)
                {
                    return Result<MechanicsMutation>.Failure(PersistenceFailures.CharacterDevelopmentInsufficientBalance(correlationId));
                }

                var newPool = new DevelopmentPool(current.DevelopmentPool.Earned, current.DevelopmentPool.Spent + cost, current.DevelopmentPool.Reserved);
                long newSpentDevelopmentPoints = (existing?.SpentDevelopmentPoints ?? 0) + cost;
                long newSkillRevision = currentSkillRevision + 1;
                var newSkill = new CharacterSkill(skillDefinitionId, toLevel, existing?.PermanentAdjustment ?? 0, newSpentDevelopmentPoints, newSkillRevision);

                var newSkills = new List<CharacterSkill>(current.Skills.Count + 1);
                bool replaced = false;
                foreach (CharacterSkill candidate in current.Skills)
                {
                    if (candidate.SkillDefinitionId.Equals(skillDefinitionId))
                    {
                        newSkills.Add(newSkill);
                        replaced = true;
                    }
                    else
                    {
                        newSkills.Add(candidate);
                    }
                }

                if (!replaced) newSkills.Add(newSkill);

                var ledgerEntry = new DevelopmentTransactionRecord(
                    DevelopmentTransactionId.NewId(now), characterId, DevelopmentTransactionKind.Spend, cost, skillDefinitionId.ToString(), "Skill level purchase", actorUserId, campaign.Manifest.RulesetVersion, now, correlationId);

                // ODY-S04-107 (pkt 0 gap fix): see PurchaseAttributeIncrease's
                // own comment -- identical retrofit, business logic above
                // unchanged.
                AdvancementPurchaseId purchaseId = AdvancementPurchaseId.NewId(now);
                var purchase = new AdvancementPurchase(purchaseId, characterId, AdvancementOperationKind.SkillLevelPurchase, skillDefinitionId.ToString(), fromLevel, toLevel, cost, "{}", campaign.Manifest.RulesetVersion, actorUserId, now, AdvancementPurchaseStatus.Applied);
                InsertAdvancementPurchase(connection, transaction, campaign.CampaignId, purchase);

                var payload = new JObject
                {
                    ["skillDefinitionId"] = skillDefinitionId.ToString(),
                    ["fromLevel"] = fromLevel,
                    ["toLevel"] = toLevel,
                    ["cost"] = cost,
                    ["newEffectiveLevel"] = newSkill.EffectiveLevel,
                    ["actorUserId"] = actorUserId.ToString(),
                    ["newAvailable"] = newPool.Available,
                    ["purchaseId"] = purchaseId.ToString(),
                };

                return Result<MechanicsMutation>.Success(new MechanicsMutation(
                    newPool, current.Attributes, newSkills, "odyssey.persistence.character_skill_level_purchased", payload, new[] { ledgerEntry }));
            });
        }

        public Result<CriticalSuccessEvidenceRecord> RecordCriticalSuccessEvidence(CampaignHandle campaign, CharacterId characterId, SkillDefinitionId skillDefinitionId, string? sourceDiceRollId, string? sourceActionId, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (!skillDefinitionId.IsValid) throw new ArgumentException("SkillDefinitionId is required.", nameof(skillDefinitionId));
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
                    tryReplay: transaction => ReplayEvidence(connection, transaction, commandId, correlationId),
                    apply: transaction =>
                    {
                        CharacterRecord? current = SelectForUpdate(connection, transaction, characterId);
                        if (current == null)
                        {
                            return Result<PipelineWrite<CriticalSuccessEvidenceRecord>>.Failure(PersistenceFailures.CharacterNotFound(correlationId));
                        }

                        UtcInstant now = _clock.GetUtcNow();
                        CriticalSuccessEvidenceId evidenceId = CriticalSuccessEvidenceId.NewId(now);
                        var record = new CriticalSuccessEvidenceRecord(evidenceId, characterId, skillDefinitionId, sourceDiceRollId, sourceActionId, now, campaign.Manifest.RulesetVersion, null, 1);

                        using (var insert = connection.CreateCommand())
                        {
                            insert.Transaction = transaction;
                            insert.CommandText = "INSERT INTO CriticalSuccessEvidence (EvidenceId, CampaignId, CharacterId, SkillDefinitionId, SourceDiceRollId, SourceActionId, OccurredAt, RulesetVersion, UsedByAdvancementId, Revision, CommandId) VALUES ($evidenceId, $campaignId, $characterId, $skillDefinitionId, $sourceDiceRollId, $sourceActionId, $occurredAt, $rulesetVersion, NULL, 1, $commandId);";
                            insert.Parameters.AddWithValue("$evidenceId", evidenceId.ToString());
                            insert.Parameters.AddWithValue("$campaignId", campaign.CampaignId.ToString());
                            insert.Parameters.AddWithValue("$characterId", characterId.ToString());
                            insert.Parameters.AddWithValue("$skillDefinitionId", skillDefinitionId.ToString());
                            insert.Parameters.AddWithValue("$sourceDiceRollId", (object?)sourceDiceRollId ?? DBNull.Value);
                            insert.Parameters.AddWithValue("$sourceActionId", (object?)sourceActionId ?? DBNull.Value);
                            insert.Parameters.AddWithValue("$occurredAt", now.ToString());
                            insert.Parameters.AddWithValue("$rulesetVersion", campaign.Manifest.RulesetVersion);
                            insert.Parameters.AddWithValue("$commandId", commandId.ToString());
                            insert.ExecuteNonQuery();
                        }

                        var payload = new JObject
                        {
                            ["evidenceId"] = evidenceId.ToString(),
                            ["characterId"] = characterId.ToString(),
                            ["skillDefinitionId"] = skillDefinitionId.ToString(),
                        };

                        // No aggregateType/aggregateRevision -- evidence is
                        // its own immutable append, never touching Character's
                        // own CharacterRevision/Mechanics section.
                        return Result<PipelineWrite<CriticalSuccessEvidenceRecord>>.Success(new PipelineWrite<CriticalSuccessEvidenceRecord>(
                            record, "odyssey.persistence.character_critical_success_evidence_recorded", payload.ToString(Newtonsoft.Json.Formatting.None), evidenceId.ToString()));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CriticalSuccessEvidenceRecord>.Failure(PersistenceFailures.CharacterIoFailed(correlationId));
            }
        }

        public Result<IReadOnlyList<CriticalSuccessEvidenceRecord>> GetCriticalSuccessEvidence(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureCharacterTables(connection);

                var entries = new List<CriticalSuccessEvidenceRecord>();
                using (var select = connection.CreateCommand())
                {
                    select.CommandText = "SELECT EvidenceId, CharacterId, SkillDefinitionId, SourceDiceRollId, SourceActionId, OccurredAt, RulesetVersion, UsedByAdvancementId, Revision FROM CriticalSuccessEvidence WHERE CharacterId = $characterId ORDER BY OccurredAt, EvidenceId;";
                    select.Parameters.AddWithValue("$characterId", characterId.ToString());
                    using SqliteDataReader reader = select.ExecuteReader();
                    while (reader.Read())
                    {
                        entries.Add(ReadCriticalSuccessEvidenceRecord(reader));
                    }
                }

                return Result<IReadOnlyList<CriticalSuccessEvidenceRecord>>.Success(entries);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<IReadOnlyList<CriticalSuccessEvidenceRecord>>.Failure(PersistenceFailures.CharacterIoFailed(correlationId));
            }
        }

        public Result<AdvancementRecommendationRecord> RequestSkillAdvancedRecommendation(CampaignHandle campaign, CharacterId characterId, SkillDefinitionId skillDefinitionId, long targetLevel, IReadOnlyList<CriticalSuccessEvidenceId> evidenceIds, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (!skillDefinitionId.IsValid) throw new ArgumentException("SkillDefinitionId is required.", nameof(skillDefinitionId));
            if (targetLevel < 1) throw new ArgumentOutOfRangeException(nameof(targetLevel));
            if (evidenceIds == null) throw new ArgumentNullException(nameof(evidenceIds));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));

            AdvancementRecommendationRecord? createdRecord = null;

            Result<CharacterRecord> mutated = MutateMechanics(campaign, characterId, expectedMechanicsRevision, commandId, correlationId, (current, connection, transaction) =>
            {
                UtcInstant now = _clock.GetUtcNow();
                bool permitted = actorIsMainGm || CharacterOwnershipAssignment.IsAssignedCharacter(current.Ownership, actorUserId, now);
                if (!permitted)
                {
                    return Result<MechanicsMutation>.Failure(PersistenceFailures.CharacterDevelopmentPurchaseDenied(correlationId));
                }

                CharacterSkill? existing = null;
                foreach (CharacterSkill candidate in current.Skills)
                {
                    if (candidate.SkillDefinitionId.Equals(skillDefinitionId)) { existing = candidate; break; }
                }

                long fromLevel = existing?.Level ?? 0;
                if (targetLevel <= fromLevel)
                {
                    throw new ArgumentOutOfRangeException(nameof(targetLevel), "TargetLevel must exceed the skill's current Level.");
                }

                long reservedAmount = RulesSkillCostRules.CostForIncrease(fromLevel, targetLevel);
                if (reservedAmount > current.DevelopmentPool.Available)
                {
                    return Result<MechanicsMutation>.Failure(PersistenceFailures.CharacterDevelopmentInsufficientBalance(correlationId));
                }

                // ADR-024 section 6.1 step 1: Reserved increases, Available
                // decreases by the same amount -- Spent is untouched.
                var newPool = new DevelopmentPool(current.DevelopmentPool.Earned, current.DevelopmentPool.Spent, current.DevelopmentPool.Reserved + reservedAmount);

                AdvancementRecommendationId recommendationId = AdvancementRecommendationId.NewId(now);
                createdRecord = new AdvancementRecommendationRecord(recommendationId, characterId, skillDefinitionId, targetLevel, reservedAmount, evidenceIds, AdvancementRecommendationStatus.Pending, 1, now);
                InsertAdvancementRecommendation(connection, transaction, campaign.CampaignId, createdRecord, commandId);

                var ledgerEntry = new DevelopmentTransactionRecord(
                    DevelopmentTransactionId.NewId(now), characterId, DevelopmentTransactionKind.Reserve, reservedAmount, skillDefinitionId.ToString(), "Skill advancement recommendation reserved", actorUserId, campaign.Manifest.RulesetVersion, now, correlationId);

                var payload = new JObject
                {
                    ["recommendationId"] = recommendationId.ToString(),
                    ["skillDefinitionId"] = skillDefinitionId.ToString(),
                    ["targetLevel"] = targetLevel,
                    ["reservedAmount"] = reservedAmount,
                    ["actorUserId"] = actorUserId.ToString(),
                };

                return Result<MechanicsMutation>.Success(new MechanicsMutation(
                    newPool, current.Attributes, current.Skills, "odyssey.persistence.character_skill_advancement_recommendation_created", payload, new[] { ledgerEntry }));
            });

            if (mutated.IsFailure)
            {
                return Result<AdvancementRecommendationRecord>.Failure(mutated.Error);
            }

            if (createdRecord != null)
            {
                return Result<AdvancementRecommendationRecord>.Success(createdRecord);
            }

            // Duplicate CommandId: our own callback never ran (MutateMechanics
            // replayed instead), so look up the already-created recommendation
            // by CommandId directly -- ADR-024 section 6.1's own "a duplicate
            // of this same CommandId returns the same Pending result and does
            // not create a second reservation."
            Result<AdvancementRecommendationRecord> replay = FindAdvancementRecommendationByCommandId(campaign, commandId, correlationId);
            return replay;
        }

        public Result<CharacterRecord> ResolveAdvancementRecommendation(CampaignHandle campaign, CharacterId characterId, AdvancementRecommendationId recommendationId, bool approve, bool spendReservedPoints, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, long expectedRecommendationRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (!recommendationId.IsValid) throw new ArgumentException("RecommendationId is required.", nameof(recommendationId));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (expectedRecommendationRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedRecommendationRevision));

            // Product section 14.3: "GM reviews... GM approves or dismisses" --
            // MainGM-only, checked before touching the database at all.
            if (!actorIsMainGm)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterAdvancementResolutionDenied(correlationId));
            }

            return MutateMechanics(campaign, characterId, expectedMechanicsRevision, commandId, correlationId, (current, connection, transaction) =>
            {
                AdvancementRecommendationRecord? recommendation = SelectAdvancementRecommendationForUpdate(connection, transaction, characterId, recommendationId);
                if (recommendation == null)
                {
                    return Result<MechanicsMutation>.Failure(PersistenceFailures.CharacterAdvancementRecommendationNotFound(correlationId));
                }

                if (recommendation.Revision != expectedRecommendationRevision)
                {
                    return Result<MechanicsMutation>.Failure(PersistenceFailures.CharacterRevisionConflict(correlationId));
                }

                if (recommendation.Status != AdvancementRecommendationStatus.Pending)
                {
                    return Result<MechanicsMutation>.Failure(PersistenceFailures.CharacterAdvancementRecommendationNotPending(correlationId));
                }

                UtcInstant now = _clock.GetUtcNow();
                long newRecommendationRevision = recommendation.Revision + 1;

                if (!approve)
                {
                    // ADR-024 section 6.1 branch 1 (Dismissed): the reserved
                    // amount returns to Available; no skill change, evidence
                    // stays unconsumed.
                    var releasedPool = new DevelopmentPool(current.DevelopmentPool.Earned, current.DevelopmentPool.Spent, current.DevelopmentPool.Reserved - recommendation.ReservedAmount);
                    UpdateAdvancementRecommendationStatus(connection, transaction, recommendationId, AdvancementRecommendationStatus.Dismissed, newRecommendationRevision, commandId);

                    var releaseLedgerEntry = new DevelopmentTransactionRecord(
                        DevelopmentTransactionId.NewId(now), characterId, DevelopmentTransactionKind.ReleaseReservation, recommendation.ReservedAmount, recommendation.SkillDefinitionId.ToString(), "Advancement recommendation dismissed", actorUserId, campaign.Manifest.RulesetVersion, now, correlationId);

                    var dismissPayload = new JObject
                    {
                        ["recommendationId"] = recommendationId.ToString(),
                        ["skillDefinitionId"] = recommendation.SkillDefinitionId.ToString(),
                        ["outcome"] = "Dismissed",
                        ["actorUserId"] = actorUserId.ToString(),
                    };

                    return Result<MechanicsMutation>.Success(new MechanicsMutation(
                        releasedPool, current.Attributes, current.Skills, "odyssey.persistence.character_advancement_recommendation_resolved", dismissPayload, new[] { releaseLedgerEntry }));
                }

                // Approved (either branch): the skill level always applies
                // and referenced evidence is always consumed -- only the
                // pool movement (Spend vs. ReleaseReservation) differs,
                // per ADR-024 section 6.1 branches 2/3.
                CharacterSkill? existingSkill = null;
                foreach (CharacterSkill candidate in current.Skills)
                {
                    if (candidate.SkillDefinitionId.Equals(recommendation.SkillDefinitionId)) { existingSkill = candidate; break; }
                }

                long newSkillRevision = (existingSkill?.Revision ?? 0) + 1;
                long newSpentDevelopmentPoints = (existingSkill?.SpentDevelopmentPoints ?? 0) + (spendReservedPoints ? recommendation.ReservedAmount : 0);
                var newSkill = new CharacterSkill(recommendation.SkillDefinitionId, recommendation.TargetLevel, existingSkill?.PermanentAdjustment ?? 0, newSpentDevelopmentPoints, newSkillRevision);

                var newSkills = new List<CharacterSkill>(current.Skills.Count + 1);
                bool replaced = false;
                foreach (CharacterSkill candidate in current.Skills)
                {
                    if (candidate.SkillDefinitionId.Equals(recommendation.SkillDefinitionId))
                    {
                        newSkills.Add(newSkill);
                        replaced = true;
                    }
                    else
                    {
                        newSkills.Add(candidate);
                    }
                }

                if (!replaced) newSkills.Add(newSkill);

                DevelopmentPool newPool = spendReservedPoints
                    ? new DevelopmentPool(current.DevelopmentPool.Earned, current.DevelopmentPool.Spent + recommendation.ReservedAmount, current.DevelopmentPool.Reserved - recommendation.ReservedAmount)
                    : new DevelopmentPool(current.DevelopmentPool.Earned, current.DevelopmentPool.Spent, current.DevelopmentPool.Reserved - recommendation.ReservedAmount);
                DevelopmentTransactionKind ledgerKind = spendReservedPoints ? DevelopmentTransactionKind.Spend : DevelopmentTransactionKind.ReleaseReservation;

                // ADR-024 section 7.1: validate every referenced evidence row
                // is still unused before consuming any of them -- all-or-
                // nothing, no partial consumption if one was already spent by
                // a concurrently-committed resolution.
                var evidenceRows = new List<CriticalSuccessEvidenceRecord>(recommendation.EvidenceIds.Count);
                foreach (CriticalSuccessEvidenceId evidenceId in recommendation.EvidenceIds)
                {
                    CriticalSuccessEvidenceRecord? evidence = SelectEvidenceForUpdate(connection, transaction, evidenceId);
                    if (evidence == null)
                    {
                        return Result<MechanicsMutation>.Failure(PersistenceFailures.CharacterCriticalEvidenceNotFound(correlationId));
                    }

                    if (evidence.UsedByAdvancementId.HasValue)
                    {
                        return Result<MechanicsMutation>.Failure(PersistenceFailures.CharacterRevisionConflict(correlationId));
                    }

                    evidenceRows.Add(evidence);
                }

                foreach (CriticalSuccessEvidenceRecord evidence in evidenceRows)
                {
                    MarkEvidenceUsed(connection, transaction, evidence.EvidenceId, recommendationId, evidence.Revision);
                }

                UpdateAdvancementRecommendationStatus(connection, transaction, recommendationId, AdvancementRecommendationStatus.Approved, newRecommendationRevision, commandId);

                var approveLedgerEntry = new DevelopmentTransactionRecord(
                    DevelopmentTransactionId.NewId(now), characterId, ledgerKind, recommendation.ReservedAmount, recommendation.SkillDefinitionId.ToString(), "Advancement recommendation approved", actorUserId, campaign.Manifest.RulesetVersion, now, correlationId);

                // ODY-S04-107 (pkt 0 gap fix): ADR-024 section 3.3/5.1 step 4
                // -- the approve branch of ResolveAdvancementRecommendation is
                // itself a purchase (product section 14.3), so it too
                // co-commits an AdvancementPurchase. Cost is the amount
                // actually spent from the pool -- 0 when
                // spendReservedPoints=false (ADR-024 section 6.1 branch 3:
                // fully funded by consumed evidence, no development points
                // spent), matching AdvancementPurchase's own relaxed
                // Cost >= 0 validation. The dismiss branch above never
                // reaches this code, so it never creates a purchase record.
                long approvedFromLevel = existingSkill?.Level ?? 0;
                long approvedCost = spendReservedPoints ? recommendation.ReservedAmount : 0;
                AdvancementPurchaseId purchaseId = AdvancementPurchaseId.NewId(now);
                var purchase = new AdvancementPurchase(purchaseId, characterId, AdvancementOperationKind.SkillLevelPurchase, recommendation.SkillDefinitionId.ToString(), approvedFromLevel, recommendation.TargetLevel, approvedCost, "{}", campaign.Manifest.RulesetVersion, actorUserId, now, AdvancementPurchaseStatus.Applied);
                InsertAdvancementPurchase(connection, transaction, campaign.CampaignId, purchase);

                var approvePayload = new JObject
                {
                    ["recommendationId"] = recommendationId.ToString(),
                    ["skillDefinitionId"] = recommendation.SkillDefinitionId.ToString(),
                    ["outcome"] = "Approved",
                    ["spentReservedPoints"] = spendReservedPoints,
                    ["newLevel"] = recommendation.TargetLevel,
                    ["actorUserId"] = actorUserId.ToString(),
                    ["purchaseId"] = purchaseId.ToString(),
                };

                return Result<MechanicsMutation>.Success(new MechanicsMutation(
                    newPool, current.Attributes, newSkills, "odyssey.persistence.character_skill_level_purchased", approvePayload, new[] { approveLedgerEntry }));
            });
        }

        public Result<AdvancementRecommendationRecord> GetAdvancementRecommendation(CampaignHandle campaign, CharacterId characterId, AdvancementRecommendationId recommendationId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (!recommendationId.IsValid) throw new ArgumentException("RecommendationId is required.", nameof(recommendationId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureCharacterTables(connection);

                using var select = connection.CreateCommand();
                select.CommandText = "SELECT RecommendationId, CharacterId, SkillDefinitionId, TargetLevel, ReservedAmount, EvidenceIdsJson, Status, Revision, CreatedAt FROM AdvancementRecommendation WHERE CharacterId = $characterId AND RecommendationId = $recommendationId LIMIT 1;";
                select.Parameters.AddWithValue("$characterId", characterId.ToString());
                select.Parameters.AddWithValue("$recommendationId", recommendationId.ToString());
                using SqliteDataReader reader = select.ExecuteReader();
                if (!reader.Read())
                {
                    return Result<AdvancementRecommendationRecord>.Failure(PersistenceFailures.CharacterAdvancementRecommendationNotFound(correlationId));
                }

                return Result<AdvancementRecommendationRecord>.Success(ReadAdvancementRecommendationRecord(reader));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<AdvancementRecommendationRecord>.Failure(PersistenceFailures.CharacterIoFailed(correlationId));
            }
        }

        public Result<IReadOnlyList<AdvancementPurchase>> GetAdvancementPurchases(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureCharacterTables(connection);
                return Result<IReadOnlyList<AdvancementPurchase>>.Success(SelectAdvancementPurchasesForCharacter(connection, null, characterId));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<IReadOnlyList<AdvancementPurchase>>.Failure(PersistenceFailures.CharacterIoFailed(correlationId));
            }
        }

        /// <summary>
        /// ODY-S04-107: ADR-024 section 6.2's compensating command. See this
        /// interface method's own doc comment in
        /// <see cref="ICharacterRepository.RevertAdvancementPurchase"/> for
        /// the full contract; this is the SQLite implementation.
        ///
        /// Dependency-check boundary (ADR-024 section 6.2 explicitly defers
        /// the exact dependency graph to a future Rules Engine/ruleset --
        /// "not an architectural concern"): this method only checks that the
        /// addressed AttributeValue/CharacterSkill entry's CURRENT value
        /// still equals THIS purchase's own ToValue. If a later purchase (an
        /// ordinary one, a recommendation approval, or an earlier respec)
        /// has since raised the value further, the entry's current value no
        /// longer matches, and the revert is rejected as
        /// CharacterAdvancementPurchaseHasDependent. This is the smallest
        /// mechanically-necessary check achievable without a real
        /// requirements/prerequisite graph -- it does not attempt to model
        /// cross-entry dependencies (e.g. one skill unlocking another),
        /// because no such graph exists anywhere in this codebase yet.
        /// </summary>
        public Result<CharacterRecord> RevertAdvancementPurchase(CampaignHandle campaign, CharacterId characterId, AdvancementPurchaseId purchaseId, string reasonCode, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (!purchaseId.IsValid) throw new ArgumentException("PurchaseId is required.", nameof(purchaseId));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));

            if (string.IsNullOrWhiteSpace(reasonCode))
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterAdvancementReasonRequired(correlationId));
            }

            // ADR-024 section 6.2: reverting a spend is a GM correction
            // action -- MainGM-only, checked before touching the database at
            // all, matching every other GM-gated command's own convention.
            if (!actorIsMainGm)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterAdvancementOperationDenied(correlationId));
            }

            return MutateMechanics(campaign, characterId, expectedMechanicsRevision, commandId, correlationId, (current, connection, transaction) =>
            {
                AdvancementPurchase? purchase = SelectAdvancementPurchaseForUpdate(connection, transaction, characterId, purchaseId);
                if (purchase == null)
                {
                    return Result<MechanicsMutation>.Failure(PersistenceFailures.CharacterAdvancementPurchaseNotFound(correlationId));
                }

                if (purchase.Status != AdvancementPurchaseStatus.Applied)
                {
                    return Result<MechanicsMutation>.Failure(PersistenceFailures.CharacterAdvancementPurchaseNotApplied(correlationId));
                }

                UtcInstant now = _clock.GetUtcNow();
                IReadOnlyList<AttributeValue> newAttributes = current.Attributes;
                IReadOnlyList<CharacterSkill> newSkills = current.Skills;

                if (purchase.OperationKind == AdvancementOperationKind.AttributeIncrease)
                {
                    AttributeDefinitionId targetId = AttributeDefinitionId.Parse(purchase.TargetDefinitionId);
                    AttributeValue? existing = null;
                    foreach (AttributeValue candidate in current.Attributes)
                    {
                        if (candidate.AttributeDefinitionId.Equals(targetId)) { existing = candidate; break; }
                    }

                    if (existing == null || existing.BaseValue != purchase.ToValue)
                    {
                        return Result<MechanicsMutation>.Failure(PersistenceFailures.CharacterAdvancementPurchaseHasDependent(correlationId));
                    }

                    var reverted = new AttributeValue(targetId, purchase.FromValue, existing.PermanentAdjustment, existing.SpentDevelopmentPoints - purchase.Cost, existing.Revision + 1);
                    var replacedAttributes = new List<AttributeValue>(current.Attributes.Count);
                    foreach (AttributeValue candidate in current.Attributes)
                    {
                        replacedAttributes.Add(candidate.AttributeDefinitionId.Equals(targetId) ? reverted : candidate);
                    }

                    newAttributes = replacedAttributes;
                }
                else if (purchase.OperationKind == AdvancementOperationKind.SkillLevelPurchase)
                {
                    SkillDefinitionId targetId = SkillDefinitionId.Parse(purchase.TargetDefinitionId);
                    CharacterSkill? existing = null;
                    foreach (CharacterSkill candidate in current.Skills)
                    {
                        if (candidate.SkillDefinitionId.Equals(targetId)) { existing = candidate; break; }
                    }

                    if (existing == null || existing.Level != purchase.ToValue)
                    {
                        return Result<MechanicsMutation>.Failure(PersistenceFailures.CharacterAdvancementPurchaseHasDependent(correlationId));
                    }

                    var reverted = new CharacterSkill(targetId, purchase.FromValue, existing.PermanentAdjustment, existing.SpentDevelopmentPoints - purchase.Cost, existing.Revision + 1);
                    var replacedSkills = new List<CharacterSkill>(current.Skills.Count);
                    foreach (CharacterSkill candidate in current.Skills)
                    {
                        replacedSkills.Add(candidate.SkillDefinitionId.Equals(targetId) ? reverted : candidate);
                    }

                    newSkills = replacedSkills;
                }
                else
                {
                    // ODY-S04-108 section 1.3: AbilityAcquisition (or any
                    // future OperationKind) is explicitly not supported by
                    // this revert -- reject explicitly rather than silently
                    // mis-parsing TargetDefinitionId as a SkillDefinitionId
                    // and returning a misleading CharacterAdvancementPurchaseHasDependent.
                    return Result<MechanicsMutation>.Failure(PersistenceFailures.CharacterAdvancementOperationKindNotSupported(correlationId));
                }

                // ADR-024 section 6.2: Available increases by Cost, Spent
                // decreases by Cost, Earned unchanged. A Cost=0 purchase
                // (fully evidence-funded, ADR-024 section 6.1 branch 3) still
                // reverts the value/status but genuinely moves no points.
                var newPool = new DevelopmentPool(current.DevelopmentPool.Earned, current.DevelopmentPool.Spent - purchase.Cost, current.DevelopmentPool.Reserved);

                UpdateAdvancementPurchaseStatus(connection, transaction, purchaseId, AdvancementPurchaseStatus.Reverted);

                long? originalEventId = FindOriginatingEventSequence(connection, transaction, campaign.CampaignId, purchaseId);

                var ledgerEntries = new List<DevelopmentTransactionRecord>();
                if (purchase.Cost > 0)
                {
                    ledgerEntries.Add(new DevelopmentTransactionRecord(
                        DevelopmentTransactionId.NewId(now), characterId, DevelopmentTransactionKind.Refund, purchase.Cost, purchase.TargetDefinitionId, "Advancement purchase reverted: " + reasonCode, actorUserId, campaign.Manifest.RulesetVersion, now, correlationId));
                }

                var payload = new JObject
                {
                    ["purchaseId"] = purchaseId.ToString(),
                    ["operationKind"] = purchase.OperationKind.ToString(),
                    ["targetDefinitionId"] = purchase.TargetDefinitionId,
                    ["fromValue"] = purchase.ToValue,
                    ["toValue"] = purchase.FromValue,
                    ["cost"] = purchase.Cost,
                    ["reasonCode"] = reasonCode,
                    ["actorUserId"] = actorUserId.ToString(),
                    ["newAvailable"] = newPool.Available,
                };

                return Result<MechanicsMutation>.Success(new MechanicsMutation(
                    newPool, newAttributes, newSkills, "odyssey.persistence.character_advancement_purchase_reverted", payload, ledgerEntries,
                    originalEventId: originalEventId, compensationGroupId: null, isCompensating: true));
            });
        }

        /// <summary>
        /// ODY-S04-107: shared plan computation used identically by
        /// <see cref="PreviewCharacterRespec"/> and, inside its own
        /// transaction, <see cref="ApplyCharacterRespec"/> -- so Apply's
        /// server-side recomputation can never drift from what Preview would
        /// have shown for the same inputs (CAP-INV-004). Reads the
        /// authoritative CURRENT value directly from
        /// <paramref name="current"/>'s own Attributes/Skills (never derived
        /// from the AdvancementPurchase history), and returns every
        /// currently-Applied purchase for each addressed target as a Return
        /// entry, plus one Spend entry per target whose DesiredValue exceeds
        /// zero.
        /// </summary>
        private static Result<CharacterRespecPreview> ComputeRespecPlan(CharacterRecord current, IReadOnlyList<AdvancementPurchase> allPurchases, IReadOnlyList<CharacterRespecTarget> targets, CorrelationId correlationId)
        {
            var entries = new List<CharacterRespecPlanEntry>();
            long totalReturned = 0;
            long totalSpent = 0;

            foreach (CharacterRespecTarget target in targets)
            {
                long currentValue;
                if (target.OperationKind == AdvancementOperationKind.AttributeIncrease)
                {
                    AttributeDefinitionId targetId = AttributeDefinitionId.Parse(target.TargetDefinitionId);
                    AttributeValue? existing = null;
                    foreach (AttributeValue candidate in current.Attributes)
                    {
                        if (candidate.AttributeDefinitionId.Equals(targetId)) { existing = candidate; break; }
                    }

                    currentValue = existing?.BaseValue ?? 0;
                }
                else if (target.OperationKind == AdvancementOperationKind.SkillLevelPurchase)
                {
                    SkillDefinitionId targetId = SkillDefinitionId.Parse(target.TargetDefinitionId);
                    CharacterSkill? existing = null;
                    foreach (CharacterSkill candidate in current.Skills)
                    {
                        if (candidate.SkillDefinitionId.Equals(targetId)) { existing = candidate; break; }
                    }

                    currentValue = existing?.Level ?? 0;
                }
                else
                {
                    // ODY-S04-108 section 1.3: AbilityAcquisition respec is
                    // explicitly out of scope -- reject explicitly rather
                    // than mis-parsing TargetDefinitionId as the wrong id type.
                    return Result<CharacterRespecPreview>.Failure(PersistenceFailures.CharacterAdvancementOperationKindNotSupported(correlationId));
                }

                if (currentValue == target.DesiredValue) continue;

                foreach (AdvancementPurchase purchase in allPurchases)
                {
                    if (purchase.Status != AdvancementPurchaseStatus.Applied) continue;
                    if (purchase.OperationKind != target.OperationKind) continue;
                    if (!string.Equals(purchase.TargetDefinitionId, target.TargetDefinitionId, StringComparison.Ordinal)) continue;

                    entries.Add(new CharacterRespecPlanEntry(CharacterRespecPlanAction.Return, target.OperationKind, target.TargetDefinitionId, purchase.Cost, purchase.PurchaseId));
                    totalReturned += purchase.Cost;
                }

                if (target.DesiredValue > 0)
                {
                    long cost;
                    if (target.OperationKind == AdvancementOperationKind.AttributeIncrease)
                    {
                        cost = RulesAttributeCostRules.CostForIncrease(0, target.DesiredValue);
                    }
                    else
                    {
                        // Only SkillLevelPurchase reaches here -- AbilityAcquisition already returned above.
                        cost = RulesSkillCostRules.CostForIncrease(0, target.DesiredValue);
                    }

                    entries.Add(new CharacterRespecPlanEntry(CharacterRespecPlanAction.Spend, target.OperationKind, target.TargetDefinitionId, cost, null));
                    totalSpent += cost;
                }
            }

            return Result<CharacterRespecPreview>.Success(new CharacterRespecPreview(entries, totalReturned, totalSpent));
        }

        /// <summary>
        /// ODY-S04-107: ADR-002 section 4.2 read-only Query -- no
        /// <c>_pipeline</c> involvement at all, no events, no state change
        /// (verified directly by tests: MechanicsRevision/pool balance
        /// identical before and after this call).
        /// </summary>
        public Result<CharacterRespecPreview> PreviewCharacterRespec(CampaignHandle campaign, CharacterId characterId, IReadOnlyList<CharacterRespecTarget> targets, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (targets == null || targets.Count == 0) throw new ArgumentException("At least one target is required.", nameof(targets));

            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureCharacterTables(connection);

                using var select = connection.CreateCommand();
                select.CommandText = SelectColumns + " FROM Character WHERE CharacterId = $characterId LIMIT 1;";
                select.Parameters.AddWithValue("$characterId", characterId.ToString());
                CharacterRecord? current;
                using (SqliteDataReader reader = select.ExecuteReader())
                {
                    current = reader.Read() ? ReadCharacterRecord(reader) : null;
                }

                if (current == null)
                {
                    return Result<CharacterRespecPreview>.Failure(PersistenceFailures.CharacterNotFound(correlationId));
                }

                IReadOnlyList<AdvancementPurchase> allPurchases = SelectAdvancementPurchasesForCharacter(connection, null, characterId);
                return ComputeRespecPlan(current, allPurchases, targets, correlationId);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CharacterRespecPreview>.Failure(PersistenceFailures.CharacterIoFailed(correlationId));
            }
        }

        /// <summary>
        /// ODY-S04-107: ADR-024 section 7.2, product section 13.5 steps 4-8
        /// -- one compensating+forward batch in a single transaction. Unlike
        /// every other Mechanics-section command, this does not go through
        /// <see cref="MutateMechanics"/> (its callback contract commits
        /// exactly one DomainEvents row per call, which a multi-purchase
        /// respec batch must exceed): every non-final batch event is
        /// appended directly via <see cref="SqliteSavingPipeline.AppendDomainEvent"/>
        /// (made <c>internal</c> specifically for this caller), and only the
        /// batch's own trailing <c>CharacterRespecCompleted</c> event goes
        /// through the normal single-event <see cref="SqliteSavingPipeline.Execute{T}"/>
        /// path -- so every event, batch or not, is still written by the
        /// identical <c>AppendDomainEvent</c> code, never a duplicated
        /// INSERT. All events in the batch share one <c>CompensationGroupId</c>
        /// (this call's own <see cref="CommandId"/>, already unique and
        /// already the natural idempotency key) and remain individually
        /// visible in <c>GetCharacterHistory</c> -- never collapsed
        /// (CAP-INV-005).
        ///
        /// Recomputes <see cref="ComputeRespecPlan"/> fresh, inside this same
        /// transaction, from a freshly-read <see cref="AdvancementPurchase"/>
        /// list and the freshly-locked <see cref="CharacterRecord"/> --
        /// there is no client-supplied preview parameter on this method at
        /// all, so nothing to trust or distrust (CAP-INV-004).
        ///
        /// Product section 13.5 step 5's "snapshot before operation" is
        /// realized as the before/after configuration summary embedded
        /// directly in the trailing <c>CharacterRespecCompleted</c> event's
        /// own payload -- not a call to <c>SqliteBackupRepository</c>'s
        /// full-file campaign backup mechanism. ADR-024 section 7.2 (the
        /// ADR directly authoritative for this command) frames the
        /// respec "snapshot" as event-payload data, and ADR-022 section 7
        /// separately prohibits a full-Character-sheet-copy event -- a full
        /// database file backup is a different, heavier mechanism this task
        /// does not invoke.
        /// </summary>
        public Result<CharacterRecord> ApplyCharacterRespec(CampaignHandle campaign, CharacterId characterId, IReadOnlyList<CharacterRespecTarget> targets, string reasonCode, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (targets == null || targets.Count == 0) throw new ArgumentException("At least one target is required.", nameof(targets));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (expectedMechanicsRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedMechanicsRevision));

            if (string.IsNullOrWhiteSpace(reasonCode))
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterAdvancementReasonRequired(correlationId));
            }

            // Product section 13.5: performed by MainGM.
            if (!actorIsMainGm)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterAdvancementOperationDenied(correlationId));
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

                        if (current.Revisions.MechanicsRevision != expectedMechanicsRevision)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(PersistenceFailures.CharacterRevisionConflict(correlationId));
                        }

                        // CAP-INV-004: fresh read inside the transaction --
                        // never trusts a client-supplied preview.
                        IReadOnlyList<AdvancementPurchase> allPurchases = SelectAdvancementPurchasesForCharacter(connection, transaction, characterId);
                        Result<CharacterRespecPreview> planResult = ComputeRespecPlan(current, allPurchases, targets, correlationId);
                        if (planResult.IsFailure)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(planResult.Error);
                        }

                        CharacterRespecPreview plan = planResult.Value;

                        string compensationGroupId = commandId.ToString();
                        UtcInstant now = _clock.GetUtcNow();
                        var producedEventSequences = new List<long>();
                        var beforeSnapshot = new JObject();
                        var afterSnapshot = new JObject();

                        var attributesByDefinition = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
                        foreach (AttributeValue attribute in current.Attributes) attributesByDefinition[attribute.AttributeDefinitionId.ToString()] = attribute;
                        var skillsByDefinition = new Dictionary<string, CharacterSkill>(StringComparer.Ordinal);
                        foreach (CharacterSkill skill in current.Skills) skillsByDefinition[skill.SkillDefinitionId.ToString()] = skill;

                        var purchasesByPurchaseId = new Dictionary<string, AdvancementPurchase>(StringComparer.Ordinal);
                        foreach (AdvancementPurchase purchase in allPurchases) purchasesByPurchaseId[purchase.PurchaseId.ToString()] = purchase;

                        long poolSpentDelta = 0;
                        var ledgerEntries = new List<DevelopmentTransactionRecord>();

                        foreach (CharacterRespecPlanEntry entry in plan.Entries)
                        {
                            // ODY-S04-108 section 1.3: defense in depth --
                            // ComputeRespecPlan already rejects an
                            // AbilityAcquisition target before producing any
                            // plan entry, so this can only trip if a future
                            // change adds a fourth OperationKind without
                            // updating this loop too.
                            if (entry.OperationKind != AdvancementOperationKind.AttributeIncrease && entry.OperationKind != AdvancementOperationKind.SkillLevelPurchase)
                            {
                                return Result<PipelineWrite<CharacterRecord>>.Failure(PersistenceFailures.CharacterAdvancementOperationKindNotSupported(correlationId));
                            }

                            if (entry.Action == CharacterRespecPlanAction.Return)
                            {
                                AdvancementPurchase reverted = purchasesByPurchaseId[entry.SourcePurchaseId!.Value.ToString()];
                                beforeSnapshot[reverted.TargetDefinitionId] = reverted.ToValue;

                                long? originalEventId = FindOriginatingEventSequence(connection, transaction, campaign.CampaignId, reverted.PurchaseId);
                                var revertedPayload = new JObject
                                {
                                    ["characterId"] = characterId.ToString(),
                                    ["purchaseId"] = reverted.PurchaseId.ToString(),
                                    ["operationKind"] = reverted.OperationKind.ToString(),
                                    ["targetDefinitionId"] = reverted.TargetDefinitionId,
                                    ["fromValue"] = reverted.ToValue,
                                    ["toValue"] = reverted.FromValue,
                                    ["cost"] = reverted.Cost,
                                    ["reasonCode"] = reasonCode,
                                    ["actorUserId"] = actorUserId.ToString(),
                                    ["compensationGroupId"] = compensationGroupId,
                                };

                                long eventSequence = SqliteSavingPipeline.AppendDomainEvent(connection, transaction, campaign.CampaignId, commandId, "odyssey.persistence.character_advancement_purchase_reverted", revertedPayload.ToString(Newtonsoft.Json.Formatting.None), now, originalEventId, compensationGroupId, isCompensating: true);
                                producedEventSequences.Add(eventSequence);

                                UpdateAdvancementPurchaseStatus(connection, transaction, reverted.PurchaseId, AdvancementPurchaseStatus.SupersededByRespec);
                                if (reverted.Cost > 0)
                                {
                                    ledgerEntries.Add(new DevelopmentTransactionRecord(
                                        DevelopmentTransactionId.NewId(now), characterId, DevelopmentTransactionKind.RespecReturn, reverted.Cost, reverted.TargetDefinitionId, "Respec: purchase superseded (" + reasonCode + ")", actorUserId, campaign.Manifest.RulesetVersion, now, correlationId));
                                }

                                poolSpentDelta -= reverted.Cost;

                                if (reverted.OperationKind == AdvancementOperationKind.AttributeIncrease)
                                {
                                    attributesByDefinition.Remove(reverted.TargetDefinitionId);
                                }
                                else
                                {
                                    skillsByDefinition.Remove(reverted.TargetDefinitionId);
                                }
                            }
                            else
                            {
                                AdvancementPurchaseId newPurchaseId = AdvancementPurchaseId.NewId(now);

                                var forwardPayload = new JObject
                                {
                                    ["characterId"] = characterId.ToString(),
                                    ["purchaseId"] = newPurchaseId.ToString(),
                                    ["fromValue"] = 0,
                                    ["cost"] = entry.Amount,
                                    ["actorUserId"] = actorUserId.ToString(),
                                    ["compensationGroupId"] = compensationGroupId,
                                };

                                string eventType;
                                if (entry.OperationKind == AdvancementOperationKind.AttributeIncrease)
                                {
                                    AttributeDefinitionId attributeDefinitionId = AttributeDefinitionId.Parse(entry.TargetDefinitionId);
                                    long desiredValue = 0;
                                    foreach (CharacterRespecTarget target in targets)
                                    {
                                        if (target.OperationKind == entry.OperationKind && string.Equals(target.TargetDefinitionId, entry.TargetDefinitionId, StringComparison.Ordinal)) { desiredValue = target.DesiredValue; break; }
                                    }

                                    var newAttribute = new AttributeValue(attributeDefinitionId, desiredValue, 0, entry.Amount, 1);
                                    attributesByDefinition[entry.TargetDefinitionId] = newAttribute;
                                    afterSnapshot[entry.TargetDefinitionId] = desiredValue;
                                    forwardPayload["attributeDefinitionId"] = entry.TargetDefinitionId;
                                    forwardPayload["toValue"] = desiredValue;
                                    eventType = "odyssey.persistence.character_attribute_increased";

                                    InsertAdvancementPurchase(connection, transaction, campaign.CampaignId, new AdvancementPurchase(newPurchaseId, characterId, AdvancementOperationKind.AttributeIncrease, entry.TargetDefinitionId, 0, desiredValue, entry.Amount, "{}", campaign.Manifest.RulesetVersion, actorUserId, now, AdvancementPurchaseStatus.Applied));
                                }
                                else
                                {
                                    SkillDefinitionId skillDefinitionId = SkillDefinitionId.Parse(entry.TargetDefinitionId);
                                    long desiredValue = 0;
                                    foreach (CharacterRespecTarget target in targets)
                                    {
                                        if (target.OperationKind == entry.OperationKind && string.Equals(target.TargetDefinitionId, entry.TargetDefinitionId, StringComparison.Ordinal)) { desiredValue = target.DesiredValue; break; }
                                    }

                                    var newSkill = new CharacterSkill(skillDefinitionId, desiredValue, 0, entry.Amount, 1);
                                    skillsByDefinition[entry.TargetDefinitionId] = newSkill;
                                    afterSnapshot[entry.TargetDefinitionId] = desiredValue;
                                    forwardPayload["skillDefinitionId"] = entry.TargetDefinitionId;
                                    forwardPayload["toLevel"] = desiredValue;
                                    eventType = "odyssey.persistence.character_skill_level_purchased";

                                    InsertAdvancementPurchase(connection, transaction, campaign.CampaignId, new AdvancementPurchase(newPurchaseId, characterId, AdvancementOperationKind.SkillLevelPurchase, entry.TargetDefinitionId, 0, desiredValue, entry.Amount, "{}", campaign.Manifest.RulesetVersion, actorUserId, now, AdvancementPurchaseStatus.Applied));
                                }

                                forwardPayload["purchaseId"] = newPurchaseId.ToString();
                                long eventSequence = SqliteSavingPipeline.AppendDomainEvent(connection, transaction, campaign.CampaignId, commandId, eventType, forwardPayload.ToString(Newtonsoft.Json.Formatting.None), now, null, compensationGroupId, isCompensating: false);
                                producedEventSequences.Add(eventSequence);

                                ledgerEntries.Add(new DevelopmentTransactionRecord(
                                    DevelopmentTransactionId.NewId(now), characterId, DevelopmentTransactionKind.RespecSpend, entry.Amount, entry.TargetDefinitionId, "Respec: repurchased (" + reasonCode + ")", actorUserId, campaign.Manifest.RulesetVersion, now, correlationId));

                                poolSpentDelta += entry.Amount;
                            }
                        }

                        var newPool = new DevelopmentPool(current.DevelopmentPool.Earned, current.DevelopmentPool.Spent + poolSpentDelta, current.DevelopmentPool.Reserved);
                        IReadOnlyList<AttributeValue> newAttributes = new List<AttributeValue>(attributesByDefinition.Values);
                        IReadOnlyList<CharacterSkill> newSkills = new List<CharacterSkill>(skillsByDefinition.Values);

                        long newMechanicsRevision = current.Revisions.MechanicsRevision + 1;
                        long newCharacterRevision = current.Revisions.CharacterRevision + 1;

                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText = "UPDATE Character SET PoolEarned = $poolEarned, PoolSpent = $poolSpent, PoolReserved = $poolReserved, AttributesJson = $attributesJson, SkillsJson = $skillsJson, MechanicsRevision = $mechanicsRevision, CharacterRevision = $characterRevision, UpdatedAt = $updatedAt, LastCommandId = $lastCommandId WHERE CharacterId = $characterId;";
                            update.Parameters.AddWithValue("$poolEarned", newPool.Earned);
                            update.Parameters.AddWithValue("$poolSpent", newPool.Spent);
                            update.Parameters.AddWithValue("$poolReserved", newPool.Reserved);
                            update.Parameters.AddWithValue("$attributesJson", SerializeAttributes(newAttributes));
                            update.Parameters.AddWithValue("$skillsJson", SerializeSkills(newSkills));
                            update.Parameters.AddWithValue("$mechanicsRevision", newMechanicsRevision);
                            update.Parameters.AddWithValue("$characterRevision", newCharacterRevision);
                            update.Parameters.AddWithValue("$updatedAt", now.ToString());
                            update.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            update.Parameters.AddWithValue("$characterId", characterId.ToString());
                            update.ExecuteNonQuery();
                        }

                        foreach (DevelopmentTransactionRecord ledgerEntry in ledgerEntries)
                        {
                            InsertDevelopmentTransaction(connection, transaction, campaign.CampaignId, ledgerEntry);
                        }

                        CharacterSectionRevisions newRevisions = WithRevisions(current.Revisions, characterRevision: newCharacterRevision, mechanicsRevision: newMechanicsRevision);
                        var record = new CharacterRecord(characterId, campaign.CampaignId, current.CharacterKind, current.LifecycleStatus, current.ApprovalState, current.DisplayName, current.PortraitReference, current.Ownership, newRevisions, current.RulesetVersion, current.AnatomyProfileRef, current.TemplateId, current.TemplateVersionAtCopyTime, current.SeedCopy, current.SubmittedAt, newPool, newAttributes, newSkills, current.Abilities, current.Resources, current.Anatomy, current.CreatedAt, now);

                        var completedPayload = new JObject
                        {
                            ["characterId"] = characterId.ToString(),
                            ["reasonCode"] = reasonCode,
                            ["actorUserId"] = actorUserId.ToString(),
                            ["compensationGroupId"] = compensationGroupId,
                            ["producedEventSequences"] = new JArray(producedEventSequences),
                            ["beforeSnapshot"] = beforeSnapshot,
                            ["afterSnapshot"] = afterSnapshot,
                            ["totalReturned"] = plan.TotalReturned,
                            ["totalSpent"] = plan.TotalSpent,
                            ["displayNameSnapshot"] = current.DisplayName,
                            ["newMechanicsRevision"] = newMechanicsRevision,
                            ["newCharacterRevision"] = newCharacterRevision,
                        };

                        return Result<PipelineWrite<CharacterRecord>>.Success(new PipelineWrite<CharacterRecord>(
                            record, "odyssey.persistence.character_respec_completed", completedPayload.ToString(Newtonsoft.Json.Formatting.None), characterId.ToString(),
                            aggregateType: "character", aggregateId: characterId.ToString(), aggregateRevision: newCharacterRevision,
                            compensationGroupId: compensationGroupId, isCompensating: false));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterIoFailed(correlationId));
            }
        }

        /// <summary>
        /// ODY-S04-108: product section 16, ADR-024 section 5.1/9. Dispatches
        /// on <paramref name="sourceKind"/>:
        ///
        /// <see cref="Odyssey.Domain.Character.SourceKind.ProgressionPurchase"/>
        /// goes through <see cref="AcquireAbilityViaProgressionPurchase"/> --
        /// a genuinely cross-section transaction (<c>Mechanics</c> +
        /// <c>CharacterAbilities</c>) that neither <see cref="MutateMechanics"/>
        /// nor <see cref="MutateAbilities"/> alone can express, so it gets
        /// its own dedicated <c>_pipeline.Execute</c> call -- the same
        /// precedent <c>ApplyCharacterRespec</c> (ODY-S04-107) already
        /// established for "one genuinely cross-cutting case gets its own
        /// method, rather than forcing a third generalization onto a
        /// single-section helper."
        ///
        /// Every other <see cref="SourceKind"/> touches only
        /// <c>CharacterAbilities</c> and reuses <see cref="MutateAbilities"/>.
        /// Permission decision for this task's own scope: MainGM-only for
        /// ALL of them, including <see cref="Odyssey.Domain.Character.SourceKind.CharacterTemplate"/>/
        /// <see cref="Odyssey.Domain.Character.SourceKind.Item"/>/
        /// <see cref="Odyssey.Domain.Character.SourceKind.ActiveEffect"/>/
        /// <see cref="Odyssey.Domain.Character.SourceKind.RulesetAdvancement"/>
        /// -- product section 16 only specifies <c>GMGrant</c> as MainGM-only
        /// explicitly, and no Item/Inventory/ActiveEffect/template-copy
        /// system exists anywhere in this codebase yet to call this command
        /// on a player's behalf (confirmed by search) -- gating the
        /// remaining four identically to <c>GMGrant</c> is the smallest,
        /// safest default a future system can revisit explicitly when it is
        /// actually built, rather than shipping an undecided/ungated
        /// permission surface today.
        /// </summary>
        public Result<CharacterRecord> AcquireAbility(CampaignHandle campaign, CharacterId characterId, AbilityDefinitionId abilityDefinitionId, SourceKind sourceKind, string? sourceRef, RankMode rankMode, long? numericRank, string? namedRankKey, string configuration, UserId actorUserId, bool actorIsMainGm, long? expectedMechanicsRevision, long expectedCharacterAbilitiesRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (!abilityDefinitionId.IsValid) throw new ArgumentException("AbilityDefinitionId is required.", nameof(abilityDefinitionId));
            if (!Enum.IsDefined(typeof(SourceKind), sourceKind)) throw new ArgumentOutOfRangeException(nameof(sourceKind));
            if (!Enum.IsDefined(typeof(RankMode), rankMode)) throw new ArgumentOutOfRangeException(nameof(rankMode));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));

            if (sourceKind == SourceKind.ProgressionPurchase)
            {
                if (!expectedMechanicsRevision.HasValue || expectedMechanicsRevision.Value < 1)
                {
                    throw new ArgumentException("ExpectedMechanicsRevision is required for SourceKind.ProgressionPurchase.", nameof(expectedMechanicsRevision));
                }

                return AcquireAbilityViaProgressionPurchase(campaign, characterId, abilityDefinitionId, sourceRef, rankMode, numericRank, namedRankKey, configuration, actorUserId, actorIsMainGm, expectedMechanicsRevision.Value, expectedCharacterAbilitiesRevision, commandId, correlationId);
            }

            if (!actorIsMainGm)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterAbilityGrantDenied(correlationId));
            }

            return MutateAbilities(campaign, characterId, expectedCharacterAbilitiesRevision, commandId, correlationId, (current, connection, transaction) =>
            {
                UtcInstant now = _clock.GetUtcNow();
                CharacterAbilityId newAbilityId = CharacterAbilityId.NewId(now);
                var newAbility = new CharacterAbility(newAbilityId, abilityDefinitionId, sourceKind, sourceRef, now, rankMode, numericRank, namedRankKey, isEnabled: true, configuration, usesState: null, revision: 1);

                var newAbilities = new List<CharacterAbility>(current.Abilities.Count + 1);
                newAbilities.AddRange(current.Abilities);
                newAbilities.Add(newAbility);

                var payload = new JObject
                {
                    ["characterAbilityId"] = newAbilityId.ToString(),
                    ["abilityDefinitionId"] = abilityDefinitionId.ToString(),
                    ["sourceKind"] = sourceKind.ToString(),
                    ["sourceRef"] = sourceRef,
                    ["actorUserId"] = actorUserId.ToString(),
                };

                return Result<AbilitiesMutation>.Success(new AbilitiesMutation(newAbilities, "odyssey.persistence.character_ability_acquired", payload));
            });
        }

        /// <summary>
        /// ODY-S04-108 section 1.2: the one genuinely cross-section
        /// transaction this task introduces -- <c>Mechanics</c> (pool spend
        /// + <c>AdvancementPurchase</c>) and <c>CharacterAbilities</c> (new
        /// <c>CharacterAbility</c>) commit atomically in a single
        /// <c>_pipeline.Execute</c> call, with BOTH section revisions
        /// checked independently per ADR-022 section 5 rule 2. Reuses
        /// <c>PurchaseAttributeIncrease</c>/<c>PurchaseSkillLevel</c>'s own
        /// permission convention (MainGM or an assigned user, product
        /// section 13.1's framing extended verbatim to abilities per
        /// ADR-024 section 9's own module-boundary list naming
        /// <c>AcquireAbility</c> alongside them).
        /// </summary>
        private Result<CharacterRecord> AcquireAbilityViaProgressionPurchase(CampaignHandle campaign, CharacterId characterId, AbilityDefinitionId abilityDefinitionId, string? sourceRef, RankMode rankMode, long? numericRank, string? namedRankKey, string configuration, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, long expectedCharacterAbilitiesRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (expectedMechanicsRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedMechanicsRevision));
            if (expectedCharacterAbilitiesRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedCharacterAbilitiesRevision));
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

                        // ADR-022 section 5 rule 2: a command depending on
                        // several sections lists all required section
                        // revisions -- both are checked independently, and
                        // a stale EITHER one rejects the whole command.
                        if (current.Revisions.MechanicsRevision != expectedMechanicsRevision)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(PersistenceFailures.CharacterRevisionConflict(correlationId));
                        }

                        if (current.Revisions.CharacterAbilitiesRevision != expectedCharacterAbilitiesRevision)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(PersistenceFailures.CharacterRevisionConflict(correlationId));
                        }

                        UtcInstant now = _clock.GetUtcNow();
                        bool permitted = actorIsMainGm || CharacterOwnershipAssignment.IsAssignedCharacter(current.Ownership, actorUserId, now);
                        if (!permitted)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(PersistenceFailures.CharacterDevelopmentPurchaseDenied(correlationId));
                        }

                        long cost = RulesAbilityCostRules.CostForAcquisition();
                        if (cost > current.DevelopmentPool.Available)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(PersistenceFailures.CharacterDevelopmentInsufficientBalance(correlationId));
                        }

                        CharacterAbilityId newAbilityId = CharacterAbilityId.NewId(now);
                        var newAbility = new CharacterAbility(newAbilityId, abilityDefinitionId, SourceKind.ProgressionPurchase, sourceRef, now, rankMode, numericRank, namedRankKey, isEnabled: true, configuration, usesState: null, revision: 1);
                        var newAbilities = new List<CharacterAbility>(current.Abilities.Count + 1);
                        newAbilities.AddRange(current.Abilities);
                        newAbilities.Add(newAbility);

                        var newPool = new DevelopmentPool(current.DevelopmentPool.Earned, current.DevelopmentPool.Spent + cost, current.DevelopmentPool.Reserved);

                        // ADR-024 section 5.1 step 4: AcquireAbility is
                        // named alongside PurchaseAttributeIncrease/
                        // PurchaseSkillLevel -- it too creates an
                        // AdvancementPurchase (OperationKind=AbilityAcquisition,
                        // FromValue=0/ToValue=1: an ability is either owned
                        // or not, no intermediate levels in this task).
                        AdvancementPurchaseId purchaseId = AdvancementPurchaseId.NewId(now);
                        var purchase = new AdvancementPurchase(purchaseId, characterId, AdvancementOperationKind.AbilityAcquisition, abilityDefinitionId.ToString(), 0, 1, cost, "{}", campaign.Manifest.RulesetVersion, actorUserId, now, AdvancementPurchaseStatus.Applied);
                        InsertAdvancementPurchase(connection, transaction, campaign.CampaignId, purchase);

                        var ledgerEntry = new DevelopmentTransactionRecord(
                            DevelopmentTransactionId.NewId(now), characterId, DevelopmentTransactionKind.Spend, cost, abilityDefinitionId.ToString(), "Ability acquisition purchase", actorUserId, campaign.Manifest.RulesetVersion, now, correlationId);
                        InsertDevelopmentTransaction(connection, transaction, campaign.CampaignId, ledgerEntry);

                        long newMechanicsRevision = current.Revisions.MechanicsRevision + 1;
                        long newAbilitiesRevision = current.Revisions.CharacterAbilitiesRevision + 1;
                        long newCharacterRevision = current.Revisions.CharacterRevision + 1;

                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText = "UPDATE Character SET PoolEarned = $poolEarned, PoolSpent = $poolSpent, PoolReserved = $poolReserved, AbilitiesJson = $abilitiesJson, MechanicsRevision = $mechanicsRevision, CharacterAbilitiesRevision = $abilitiesRevision, CharacterRevision = $characterRevision, UpdatedAt = $updatedAt, LastCommandId = $lastCommandId WHERE CharacterId = $characterId;";
                            update.Parameters.AddWithValue("$poolEarned", newPool.Earned);
                            update.Parameters.AddWithValue("$poolSpent", newPool.Spent);
                            update.Parameters.AddWithValue("$poolReserved", newPool.Reserved);
                            update.Parameters.AddWithValue("$abilitiesJson", SerializeAbilities(newAbilities));
                            update.Parameters.AddWithValue("$mechanicsRevision", newMechanicsRevision);
                            update.Parameters.AddWithValue("$abilitiesRevision", newAbilitiesRevision);
                            update.Parameters.AddWithValue("$characterRevision", newCharacterRevision);
                            update.Parameters.AddWithValue("$updatedAt", now.ToString());
                            update.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            update.Parameters.AddWithValue("$characterId", characterId.ToString());
                            update.ExecuteNonQuery();
                        }

                        CharacterSectionRevisions newRevisions = WithRevisions(current.Revisions, characterRevision: newCharacterRevision, mechanicsRevision: newMechanicsRevision, characterAbilitiesRevision: newAbilitiesRevision);
                        var record = new CharacterRecord(characterId, campaign.CampaignId, current.CharacterKind, current.LifecycleStatus, current.ApprovalState, current.DisplayName, current.PortraitReference, current.Ownership, newRevisions, current.RulesetVersion, current.AnatomyProfileRef, current.TemplateId, current.TemplateVersionAtCopyTime, current.SeedCopy, current.SubmittedAt, newPool, current.Attributes, current.Skills, newAbilities, current.Resources, current.Anatomy, current.CreatedAt, now);

                        var payload = new JObject
                        {
                            ["characterId"] = characterId.ToString(),
                            ["characterAbilityId"] = newAbilityId.ToString(),
                            ["abilityDefinitionId"] = abilityDefinitionId.ToString(),
                            ["sourceKind"] = SourceKind.ProgressionPurchase.ToString(),
                            ["cost"] = cost,
                            ["purchaseId"] = purchaseId.ToString(),
                            ["actorUserId"] = actorUserId.ToString(),
                            ["newAvailable"] = newPool.Available,
                            ["displayNameSnapshot"] = current.DisplayName,
                            ["newMechanicsRevision"] = newMechanicsRevision,
                            ["newCharacterAbilitiesRevision"] = newAbilitiesRevision,
                            ["newCharacterRevision"] = newCharacterRevision,
                        };

                        return Result<PipelineWrite<CharacterRecord>>.Success(new PipelineWrite<CharacterRecord>(
                            record, "odyssey.persistence.character_ability_acquired", payload.ToString(Newtonsoft.Json.Formatting.None), characterId.ToString(),
                            aggregateType: "character", aggregateId: characterId.ToString(), aggregateRevision: newCharacterRevision));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterIoFailed(correlationId));
            }
        }

        public Result<CharacterRecord> RemoveAbility(CampaignHandle campaign, CharacterId characterId, CharacterAbilityId characterAbilityId, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAbilitiesRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (!characterAbilityId.IsValid) throw new ArgumentException("CharacterAbilityId is required.", nameof(characterAbilityId));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));

            if (!actorIsMainGm)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterAbilityGrantDenied(correlationId));
            }

            return MutateAbilities(campaign, characterId, expectedCharacterAbilitiesRevision, commandId, correlationId, (current, connection, transaction) =>
            {
                CharacterAbility? existing = null;
                foreach (CharacterAbility candidate in current.Abilities)
                {
                    if (candidate.CharacterAbilityId.Equals(characterAbilityId)) { existing = candidate; break; }
                }

                if (existing == null)
                {
                    return Result<AbilitiesMutation>.Failure(PersistenceFailures.CharacterAbilityNotFound(correlationId));
                }

                // Product section 16: "способность предмета или эффекта
                // исчезает при прекращении источника и не становится
                // постоянной покупкой" -- only Item/ActiveEffect are legal
                // to remove this way; a permanent purchased/granted ability
                // survives this ordinary command.
                if (existing.SourceKind != SourceKind.Item && existing.SourceKind != SourceKind.ActiveEffect)
                {
                    return Result<AbilitiesMutation>.Failure(PersistenceFailures.CharacterAbilityRemovalNotAllowed(correlationId));
                }

                var newAbilities = new List<CharacterAbility>(current.Abilities.Count - 1);
                foreach (CharacterAbility candidate in current.Abilities)
                {
                    if (!candidate.CharacterAbilityId.Equals(characterAbilityId))
                    {
                        newAbilities.Add(candidate);
                    }
                }

                var payload = new JObject
                {
                    ["characterAbilityId"] = characterAbilityId.ToString(),
                    ["abilityDefinitionId"] = existing.AbilityDefinitionId.ToString(),
                    ["sourceKind"] = existing.SourceKind.ToString(),
                    ["actorUserId"] = actorUserId.ToString(),
                };

                return Result<AbilitiesMutation>.Success(new AbilitiesMutation(newAbilities, "odyssey.persistence.character_ability_removed", payload));
            });
        }

        /// <summary>
        /// ODY-S04-108 section 1.1: the first real, incrementing
        /// <c>CharacterAbilities</c>-section helper -- mirrors
        /// <see cref="MutateMechanics"/>'s exact gate/load/callback/commit
        /// shape for a single section, the same way <c>MutateOwnership</c>
        /// already does for <c>Ownership</c>. Unlike ODY-S04-105/106's own
        /// choice to route <c>AttributeValuesRevision</c>/
        /// <c>CharacterSkillsRevision</c> through <c>MechanicsRevision</c>
        /// instead (ADR-024 section 4.2's justification for pool ledger
        /// data), no such justification exists for abilities -- this helper
        /// genuinely increments <c>CharacterAbilitiesRevision</c> on every
        /// call.
        /// </summary>
        private Result<CharacterRecord> MutateAbilities(
            CampaignHandle campaign,
            CharacterId characterId,
            long expectedCharacterAbilitiesRevision,
            CommandId commandId,
            CorrelationId correlationId,
            Func<CharacterRecord, SqliteConnection, SqliteTransaction, Result<AbilitiesMutation>> mutate)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (expectedCharacterAbilitiesRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedCharacterAbilitiesRevision));
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

                        if (current.Revisions.CharacterAbilitiesRevision != expectedCharacterAbilitiesRevision)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(PersistenceFailures.CharacterRevisionConflict(correlationId));
                        }

                        Result<AbilitiesMutation> mutationResult = mutate(current, connection, transaction);
                        if (mutationResult.IsFailure)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(mutationResult.Error);
                        }

                        AbilitiesMutation mutation = mutationResult.Value;
                        UtcInstant now = _clock.GetUtcNow();
                        long newAbilitiesRevision = current.Revisions.CharacterAbilitiesRevision + 1;
                        long newCharacterRevision = current.Revisions.CharacterRevision + 1;

                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText = "UPDATE Character SET AbilitiesJson = $abilitiesJson, CharacterAbilitiesRevision = $abilitiesRevision, CharacterRevision = $characterRevision, UpdatedAt = $updatedAt, LastCommandId = $lastCommandId WHERE CharacterId = $characterId;";
                            update.Parameters.AddWithValue("$abilitiesJson", SerializeAbilities(mutation.NewAbilities));
                            update.Parameters.AddWithValue("$abilitiesRevision", newAbilitiesRevision);
                            update.Parameters.AddWithValue("$characterRevision", newCharacterRevision);
                            update.Parameters.AddWithValue("$updatedAt", now.ToString());
                            update.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            update.Parameters.AddWithValue("$characterId", characterId.ToString());
                            update.ExecuteNonQuery();
                        }

                        CharacterSectionRevisions newRevisions = WithRevisions(current.Revisions, characterRevision: newCharacterRevision, characterAbilitiesRevision: newAbilitiesRevision);
                        var record = new CharacterRecord(characterId, campaign.CampaignId, current.CharacterKind, current.LifecycleStatus, current.ApprovalState, current.DisplayName, current.PortraitReference, current.Ownership, newRevisions, current.RulesetVersion, current.AnatomyProfileRef, current.TemplateId, current.TemplateVersionAtCopyTime, current.SeedCopy, current.SubmittedAt, current.DevelopmentPool, current.Attributes, current.Skills, mutation.NewAbilities, current.Resources, current.Anatomy, current.CreatedAt, now);

                        mutation.PayloadExtra["characterId"] = characterId.ToString();
                        mutation.PayloadExtra["displayNameSnapshot"] = current.DisplayName;
                        mutation.PayloadExtra["newCharacterAbilitiesRevision"] = newAbilitiesRevision;
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

        /// <summary>ODY-S04-108: the pure business-logic result <see cref="MutateAbilities"/>'s caller-supplied callback returns -- mirrors <see cref="MechanicsMutation"/>'s exact shape for the single <c>CharacterAbilities</c> section.</summary>
        private sealed class AbilitiesMutation
        {
            public AbilitiesMutation(IReadOnlyList<CharacterAbility> newAbilities, string eventType, JObject payloadExtra)
            {
                NewAbilities = newAbilities;
                EventType = eventType;
                PayloadExtra = payloadExtra;
            }

            public IReadOnlyList<CharacterAbility> NewAbilities { get; }
            public string EventType { get; }
            public JObject PayloadExtra { get; }
        }

        // ==================== ODY-S04-109: CharacterResource ====================

        public Result<CharacterRecord> InitializeCharacterResource(CampaignHandle campaign, CharacterId characterId, ResourceDefinitionId resourceDefinitionId, UserId actorUserId, bool actorIsMainGm, long expectedCharacterResourcesRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (!resourceDefinitionId.IsValid) throw new ArgumentException("ResourceDefinitionId is required.", nameof(resourceDefinitionId));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));

            if (!actorIsMainGm)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterResourceOperationDenied(correlationId));
            }

            return MutateResources(campaign, characterId, expectedCharacterResourcesRevision, commandId, correlationId, (current, connection, transaction) =>
            {
                UtcInstant now = _clock.GetUtcNow();
                CharacterResourceId newResourceId = CharacterResourceId.NewId(now);

                // RulesResourceInitializationRules: TEST FIXTURE ONLY -- see that class's own doc comment. No ResourceDefinition catalog exists yet.
                var newResource = new CharacterResource(newResourceId, resourceDefinitionId, RulesResourceInitializationRules.DefaultBaseMaximum, RulesResourceInitializationRules.DefaultBaseMaximum, 0, RulesResourceInitializationRules.DefaultMinimumValue, RulesResourceInitializationRules.DefaultRecoveryRule, 1);

                var newResources = new List<CharacterResource>(current.Resources.Count + 1);
                newResources.AddRange(current.Resources);
                newResources.Add(newResource);

                var payload = new JObject
                {
                    ["characterResourceId"] = newResourceId.ToString(),
                    ["resourceDefinitionId"] = resourceDefinitionId.ToString(),
                    ["baseMaximum"] = newResource.BaseMaximum,
                    ["actorUserId"] = actorUserId.ToString(),
                };

                return Result<ResourcesMutation>.Success(new ResourcesMutation(newResources, "odyssey.persistence.character_resource_initialized", payload));
            });
        }

        public Result<CharacterRecord> SetResourceCurrentValue(CampaignHandle campaign, CharacterId characterId, CharacterResourceId characterResourceId, long newCurrentValue, UserId actorUserId, bool actorIsMainGm, long expectedCharacterResourcesRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (!characterResourceId.IsValid) throw new ArgumentException("CharacterResourceId is required.", nameof(characterResourceId));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));

            if (!actorIsMainGm)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterResourceOperationDenied(correlationId));
            }

            return MutateResources(campaign, characterId, expectedCharacterResourcesRevision, commandId, correlationId, (current, connection, transaction) =>
            {
                CharacterResource? existing = null;
                foreach (CharacterResource candidate in current.Resources)
                {
                    if (candidate.CharacterResourceId.Equals(characterResourceId)) { existing = candidate; break; }
                }

                if (existing == null)
                {
                    return Result<ResourcesMutation>.Failure(PersistenceFailures.CharacterResourceNotFound(correlationId));
                }

                if (newCurrentValue < existing.MinimumValue || newCurrentValue > existing.EffectiveMaximum)
                {
                    return Result<ResourcesMutation>.Failure(PersistenceFailures.CharacterResourceValueOutOfRange(correlationId));
                }

                long fromValue = existing.CurrentValue;
                var updated = new CharacterResource(existing.CharacterResourceId, existing.ResourceDefinitionId, newCurrentValue, existing.BaseMaximum, existing.PermanentMaximumAdjustment, existing.MinimumValue, existing.RecoveryRule, existing.Revision + 1);

                var newResources = new List<CharacterResource>(current.Resources.Count);
                foreach (CharacterResource candidate in current.Resources)
                {
                    newResources.Add(candidate.CharacterResourceId.Equals(characterResourceId) ? updated : candidate);
                }

                var payload = new JObject
                {
                    ["characterResourceId"] = characterResourceId.ToString(),
                    ["fromValue"] = fromValue,
                    ["toValue"] = newCurrentValue,
                    ["actorUserId"] = actorUserId.ToString(),
                };

                return Result<ResourcesMutation>.Success(new ResourcesMutation(newResources, "odyssey.persistence.character_resource_changed", payload));
            });
        }

        public Result<CharacterRecord> SetResourceMaximum(CampaignHandle campaign, CharacterId characterId, CharacterResourceId characterResourceId, long newBaseMaximum, long newPermanentMaximumAdjustment, UserId actorUserId, bool actorIsMainGm, long expectedCharacterResourcesRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (!characterResourceId.IsValid) throw new ArgumentException("CharacterResourceId is required.", nameof(characterResourceId));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));

            if (!actorIsMainGm)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterResourceOperationDenied(correlationId));
            }

            return MutateResources(campaign, characterId, expectedCharacterResourcesRevision, commandId, correlationId, (current, connection, transaction) =>
            {
                CharacterResource? existing = null;
                foreach (CharacterResource candidate in current.Resources)
                {
                    if (candidate.CharacterResourceId.Equals(characterResourceId)) { existing = candidate; break; }
                }

                if (existing == null)
                {
                    return Result<ResourcesMutation>.Failure(PersistenceFailures.CharacterResourceNotFound(correlationId));
                }

                // Product section 17.1 / requirement 44: if the new
                // EffectiveMaximum is below CurrentValue, CurrentValue is
                // clamped down in the same commit. Requirement 45: a later
                // increase never restores the clamped value on its own --
                // this command only ever sets the new maximum, never bumps
                // CurrentValue upward itself.
                long newEffectiveMaximum = newBaseMaximum + newPermanentMaximumAdjustment;
                long clampedCurrentValue = Math.Min(existing.CurrentValue, newEffectiveMaximum);
                clampedCurrentValue = Math.Max(clampedCurrentValue, existing.MinimumValue);

                var updated = new CharacterResource(existing.CharacterResourceId, existing.ResourceDefinitionId, clampedCurrentValue, newBaseMaximum, newPermanentMaximumAdjustment, existing.MinimumValue, existing.RecoveryRule, existing.Revision + 1);

                var newResources = new List<CharacterResource>(current.Resources.Count);
                foreach (CharacterResource candidate in current.Resources)
                {
                    newResources.Add(candidate.CharacterResourceId.Equals(characterResourceId) ? updated : candidate);
                }

                var payload = new JObject
                {
                    ["characterResourceId"] = characterResourceId.ToString(),
                    ["fromCurrentValue"] = existing.CurrentValue,
                    ["toCurrentValue"] = clampedCurrentValue,
                    ["fromEffectiveMaximum"] = existing.EffectiveMaximum,
                    ["toEffectiveMaximum"] = newEffectiveMaximum,
                    ["actorUserId"] = actorUserId.ToString(),
                };

                return Result<ResourcesMutation>.Success(new ResourcesMutation(newResources, "odyssey.persistence.character_resource_changed", payload));
            });
        }

        /// <summary>ODY-S04-109 section 1.1: mirrors <see cref="MutateAbilities"/>'s exact single-section gate/load/callback/commit shape, but for the <c>CharacterResources</c> section -- genuinely increments <c>CharacterResourcesRevision</c> (reserved by ADR-022 section 5, never previously written).</summary>
        private Result<CharacterRecord> MutateResources(
            CampaignHandle campaign,
            CharacterId characterId,
            long expectedCharacterResourcesRevision,
            CommandId commandId,
            CorrelationId correlationId,
            Func<CharacterRecord, SqliteConnection, SqliteTransaction, Result<ResourcesMutation>> mutate)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (expectedCharacterResourcesRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedCharacterResourcesRevision));
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

                        if (current.Revisions.CharacterResourcesRevision != expectedCharacterResourcesRevision)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(PersistenceFailures.CharacterRevisionConflict(correlationId));
                        }

                        Result<ResourcesMutation> mutationResult = mutate(current, connection, transaction);
                        if (mutationResult.IsFailure)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(mutationResult.Error);
                        }

                        ResourcesMutation mutation = mutationResult.Value;
                        UtcInstant now = _clock.GetUtcNow();
                        long newResourcesRevision = current.Revisions.CharacterResourcesRevision + 1;
                        long newCharacterRevision = current.Revisions.CharacterRevision + 1;

                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText = "UPDATE Character SET ResourcesJson = $resourcesJson, CharacterResourcesRevision = $resourcesRevision, CharacterRevision = $characterRevision, UpdatedAt = $updatedAt, LastCommandId = $lastCommandId WHERE CharacterId = $characterId;";
                            update.Parameters.AddWithValue("$resourcesJson", SerializeResources(mutation.NewResources));
                            update.Parameters.AddWithValue("$resourcesRevision", newResourcesRevision);
                            update.Parameters.AddWithValue("$characterRevision", newCharacterRevision);
                            update.Parameters.AddWithValue("$updatedAt", now.ToString());
                            update.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            update.Parameters.AddWithValue("$characterId", characterId.ToString());
                            update.ExecuteNonQuery();
                        }

                        CharacterSectionRevisions newRevisions = WithRevisions(current.Revisions, characterRevision: newCharacterRevision, characterResourcesRevision: newResourcesRevision);
                        var record = new CharacterRecord(characterId, campaign.CampaignId, current.CharacterKind, current.LifecycleStatus, current.ApprovalState, current.DisplayName, current.PortraitReference, current.Ownership, newRevisions, current.RulesetVersion, current.AnatomyProfileRef, current.TemplateId, current.TemplateVersionAtCopyTime, current.SeedCopy, current.SubmittedAt, current.DevelopmentPool, current.Attributes, current.Skills, current.Abilities, mutation.NewResources, current.Anatomy, current.CreatedAt, now);

                        mutation.PayloadExtra["characterId"] = characterId.ToString();
                        mutation.PayloadExtra["displayNameSnapshot"] = current.DisplayName;
                        mutation.PayloadExtra["newCharacterResourcesRevision"] = newResourcesRevision;
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

        /// <summary>ODY-S04-109: the pure business-logic result <see cref="MutateResources"/>'s caller-supplied callback returns -- mirrors <see cref="AbilitiesMutation"/>'s exact shape for the single <c>CharacterResources</c> section.</summary>
        private sealed class ResourcesMutation
        {
            public ResourcesMutation(IReadOnlyList<CharacterResource> newResources, string eventType, JObject payloadExtra)
            {
                NewResources = newResources;
                EventType = eventType;
                PayloadExtra = payloadExtra;
            }

            public IReadOnlyList<CharacterResource> NewResources { get; }
            public string EventType { get; }
            public JObject PayloadExtra { get; }
        }

        // ==================== ODY-S04-109: CharacterAnatomy ====================

        public Result<CharacterRecord> InitializeCharacterAnatomy(CampaignHandle campaign, CharacterId characterId, AnatomyProfileDefinitionId anatomyProfileDefinitionId, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAnatomyRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (!anatomyProfileDefinitionId.IsValid) throw new ArgumentException("AnatomyProfileDefinitionId is required.", nameof(anatomyProfileDefinitionId));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));

            return MutateAnatomy(campaign, characterId, actorIsMainGm, expectedCharacterAnatomyRevision, commandId, correlationId, (current, connection, transaction) =>
            {
                if (current.Anatomy != null)
                {
                    return Result<AnatomyMutation>.Failure(PersistenceFailures.CharacterAnatomyAlreadyInitialized(correlationId));
                }

                UtcInstant now = _clock.GetUtcNow();

                // RulesAnatomyInitializationRules: TEST FIXTURE ONLY -- see that class's own doc comment. No AnatomyProfileDefinition catalog exists yet.
                var newAnatomy = new Odyssey.Domain.Character.CharacterAnatomy(
                    anatomyProfileDefinitionId,
                    RulesAnatomyInitializationRules.DefaultAnatomyProfileVersion,
                    RulesAnatomyInitializationRules.DefaultHumanoidBodyParts(),
                    Array.Empty<PermanentModification>(),
                    new[] { new AnatomyMigrationEntry("Initialized", "CharacterAnatomy initialized from fixture " + anatomyProfileDefinitionId, now) },
                    1);

                var payload = new JObject
                {
                    ["anatomyProfileDefinitionId"] = anatomyProfileDefinitionId.ToString(),
                    ["anatomyProfileVersion"] = newAnatomy.AnatomyProfileVersion,
                    ["actorUserId"] = actorUserId.ToString(),
                };

                return Result<AnatomyMutation>.Success(new AnatomyMutation(newAnatomy, "odyssey.persistence.character_anatomy_initialized", payload));
            });
        }

        public Result<CharacterRecord> AddBodyPart(CampaignHandle campaign, CharacterId characterId, BodyPartId bodyPartId, string name, long damageLimit, BodyPartId? attachedToBodyPartId, string properties, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAnatomyRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (!bodyPartId.IsValid) throw new ArgumentException("BodyPartId is required.", nameof(bodyPartId));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));

            return MutateAnatomy(campaign, characterId, actorIsMainGm, expectedCharacterAnatomyRevision, commandId, correlationId, (current, connection, transaction) =>
            {
                if (current.Anatomy == null)
                {
                    return Result<AnatomyMutation>.Failure(PersistenceFailures.CharacterAnatomyNotInitialized(correlationId));
                }

                foreach (BodyPart candidate in current.Anatomy.BodyParts)
                {
                    if (candidate.BodyPartId.Equals(bodyPartId))
                    {
                        return Result<AnatomyMutation>.Failure(PersistenceFailures.CharacterBodyPartAlreadyExists(correlationId));
                    }
                }

                // Product section 18: "добавить часть тела" undergoes a
                // dependency preview per this task's own section 1.3 -- for
                // ADDING a part, the only real, internally-checkable
                // dependency is that its own AttachedToBodyPartId (if any)
                // must reference an existing part.
                if (attachedToBodyPartId.HasValue)
                {
                    bool parentExists = false;
                    foreach (BodyPart candidate in current.Anatomy.BodyParts)
                    {
                        if (candidate.BodyPartId.Equals(attachedToBodyPartId.Value)) { parentExists = true; break; }
                    }

                    if (!parentExists)
                    {
                        return Result<AnatomyMutation>.Failure(PersistenceFailures.CharacterBodyPartNotFound(correlationId));
                    }
                }

                UtcInstant now = _clock.GetUtcNow();
                var newBodyPart = new BodyPart(bodyPartId, name, damageLimit, attachedToBodyPartId, properties);
                var newBodyParts = new List<BodyPart>(current.Anatomy.BodyParts.Count + 1);
                newBodyParts.AddRange(current.Anatomy.BodyParts);
                newBodyParts.Add(newBodyPart);

                var newMigrationHistory = new List<AnatomyMigrationEntry>(current.Anatomy.MigrationHistory.Count + 1);
                newMigrationHistory.AddRange(current.Anatomy.MigrationHistory);
                newMigrationHistory.Add(new AnatomyMigrationEntry("BodyPartAdded", "Added body part " + bodyPartId, now));

                var newAnatomy = new Odyssey.Domain.Character.CharacterAnatomy(current.Anatomy.AnatomyProfileDefinitionId, current.Anatomy.AnatomyProfileVersion, newBodyParts, current.Anatomy.PermanentModifications, newMigrationHistory, current.Anatomy.Revision + 1);

                var payload = new JObject
                {
                    ["bodyPartId"] = bodyPartId.ToString(),
                    ["name"] = name,
                    ["attachedToBodyPartId"] = attachedToBodyPartId?.ToString(),
                    ["actorUserId"] = actorUserId.ToString(),
                };

                return Result<AnatomyMutation>.Success(new AnatomyMutation(newAnatomy, "odyssey.persistence.character_anatomy_changed", payload));
            });
        }

        /// <summary>
        /// ODY-S04-109 section 1.3: dependency preview boundary. Product
        /// section 18/requirement 51's own item-dependency check is a stub
        /// -- NO Item/Inventory system exists anywhere in this codebase
        /// (confirmed by search), so there is nothing to check there; this
        /// is documented, not silently skipped. What IS checked, for real,
        /// is the one dependency this Character's own <c>CharacterAnatomy</c>
        /// snapshot can express: any other <see cref="BodyPart.AttachedToBodyPartId"/>
        /// or <see cref="PermanentModification.AttachedToBodyPartId"/>
        /// referencing the part being removed.
        /// </summary>
        public Result<CharacterRecord> RemoveBodyPart(CampaignHandle campaign, CharacterId characterId, BodyPartId bodyPartId, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAnatomyRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (!bodyPartId.IsValid) throw new ArgumentException("BodyPartId is required.", nameof(bodyPartId));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));

            return MutateAnatomy(campaign, characterId, actorIsMainGm, expectedCharacterAnatomyRevision, commandId, correlationId, (current, connection, transaction) =>
            {
                if (current.Anatomy == null)
                {
                    return Result<AnatomyMutation>.Failure(PersistenceFailures.CharacterAnatomyNotInitialized(correlationId));
                }

                bool exists = false;
                foreach (BodyPart candidate in current.Anatomy.BodyParts)
                {
                    if (candidate.BodyPartId.Equals(bodyPartId)) { exists = true; break; }
                }

                if (!exists)
                {
                    return Result<AnatomyMutation>.Failure(PersistenceFailures.CharacterBodyPartNotFound(correlationId));
                }

                // Item-system dependency (product requirement 51): NOT
                // checked -- no Item/Inventory system exists yet (this
                // task's own section 1.3 stub, documented not silent).
                // Internal dependency (this task's own real, checkable
                // substitute): does any other body part attach to this one,
                // or any permanent modification attach to this one?
                foreach (BodyPart candidate in current.Anatomy.BodyParts)
                {
                    if (candidate.AttachedToBodyPartId.HasValue && candidate.AttachedToBodyPartId.Value.Equals(bodyPartId))
                    {
                        return Result<AnatomyMutation>.Failure(PersistenceFailures.CharacterBodyPartHasDependent(correlationId));
                    }
                }

                foreach (PermanentModification modification in current.Anatomy.PermanentModifications)
                {
                    if (modification.AttachedToBodyPartId.Equals(bodyPartId))
                    {
                        return Result<AnatomyMutation>.Failure(PersistenceFailures.CharacterBodyPartHasDependent(correlationId));
                    }
                }

                UtcInstant now = _clock.GetUtcNow();
                var newBodyParts = new List<BodyPart>(current.Anatomy.BodyParts.Count - 1);
                foreach (BodyPart candidate in current.Anatomy.BodyParts)
                {
                    if (!candidate.BodyPartId.Equals(bodyPartId)) newBodyParts.Add(candidate);
                }

                var newMigrationHistory = new List<AnatomyMigrationEntry>(current.Anatomy.MigrationHistory.Count + 1);
                newMigrationHistory.AddRange(current.Anatomy.MigrationHistory);
                newMigrationHistory.Add(new AnatomyMigrationEntry("BodyPartRemoved", "Removed body part " + bodyPartId, now));

                var newAnatomy = new Odyssey.Domain.Character.CharacterAnatomy(current.Anatomy.AnatomyProfileDefinitionId, current.Anatomy.AnatomyProfileVersion, newBodyParts, current.Anatomy.PermanentModifications, newMigrationHistory, current.Anatomy.Revision + 1);

                var payload = new JObject
                {
                    ["bodyPartId"] = bodyPartId.ToString(),
                    ["actorUserId"] = actorUserId.ToString(),
                };

                return Result<AnatomyMutation>.Success(new AnatomyMutation(newAnatomy, "odyssey.persistence.character_anatomy_changed", payload));
            });
        }

        public Result<CharacterRecord> UpdateBodyPart(CampaignHandle campaign, CharacterId characterId, BodyPartId bodyPartId, long? newDamageLimit, string? newProperties, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAnatomyRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (!bodyPartId.IsValid) throw new ArgumentException("BodyPartId is required.", nameof(bodyPartId));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));

            return MutateAnatomy(campaign, characterId, actorIsMainGm, expectedCharacterAnatomyRevision, commandId, correlationId, (current, connection, transaction) =>
            {
                if (current.Anatomy == null)
                {
                    return Result<AnatomyMutation>.Failure(PersistenceFailures.CharacterAnatomyNotInitialized(correlationId));
                }

                BodyPart? existing = null;
                foreach (BodyPart candidate in current.Anatomy.BodyParts)
                {
                    if (candidate.BodyPartId.Equals(bodyPartId)) { existing = candidate; break; }
                }

                if (existing == null)
                {
                    return Result<AnatomyMutation>.Failure(PersistenceFailures.CharacterBodyPartNotFound(correlationId));
                }

                UtcInstant now = _clock.GetUtcNow();
                var updated = new BodyPart(existing.BodyPartId, existing.Name, newDamageLimit ?? existing.DamageLimit, existing.AttachedToBodyPartId, newProperties ?? existing.Properties);

                var newBodyParts = new List<BodyPart>(current.Anatomy.BodyParts.Count);
                foreach (BodyPart candidate in current.Anatomy.BodyParts)
                {
                    newBodyParts.Add(candidate.BodyPartId.Equals(bodyPartId) ? updated : candidate);
                }

                var newMigrationHistory = new List<AnatomyMigrationEntry>(current.Anatomy.MigrationHistory.Count + 1);
                newMigrationHistory.AddRange(current.Anatomy.MigrationHistory);
                newMigrationHistory.Add(new AnatomyMigrationEntry("BodyPartUpdated", "Updated body part " + bodyPartId, now));

                var newAnatomy = new Odyssey.Domain.Character.CharacterAnatomy(current.Anatomy.AnatomyProfileDefinitionId, current.Anatomy.AnatomyProfileVersion, newBodyParts, current.Anatomy.PermanentModifications, newMigrationHistory, current.Anatomy.Revision + 1);

                var payload = new JObject
                {
                    ["bodyPartId"] = bodyPartId.ToString(),
                    ["newDamageLimit"] = newDamageLimit,
                    ["actorUserId"] = actorUserId.ToString(),
                };

                return Result<AnatomyMutation>.Success(new AnatomyMutation(newAnatomy, "odyssey.persistence.character_anatomy_changed", payload));
            });
        }

        public Result<CharacterRecord> ReplaceAnatomyProfile(CampaignHandle campaign, CharacterId characterId, AnatomyProfileDefinitionId newAnatomyProfileDefinitionId, string newAnatomyProfileVersion, IReadOnlyList<BodyPart> newBodyParts, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAnatomyRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (!newAnatomyProfileDefinitionId.IsValid) throw new ArgumentException("AnatomyProfileDefinitionId is required.", nameof(newAnatomyProfileDefinitionId));
            if (string.IsNullOrWhiteSpace(newAnatomyProfileVersion)) throw new ArgumentException("AnatomyProfileVersion is required.", nameof(newAnatomyProfileVersion));
            if (newBodyParts == null) throw new ArgumentNullException(nameof(newBodyParts));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));

            return MutateAnatomy(campaign, characterId, actorIsMainGm, expectedCharacterAnatomyRevision, commandId, correlationId, (current, connection, transaction) =>
            {
                if (current.Anatomy == null)
                {
                    return Result<AnatomyMutation>.Failure(PersistenceFailures.CharacterAnatomyNotInitialized(correlationId));
                }

                UtcInstant now = _clock.GetUtcNow();
                var newMigrationHistory = new List<AnatomyMigrationEntry>(current.Anatomy.MigrationHistory.Count + 1);
                newMigrationHistory.AddRange(current.Anatomy.MigrationHistory);
                newMigrationHistory.Add(new AnatomyMigrationEntry("ProfileReplaced", "Replaced profile " + current.Anatomy.AnatomyProfileDefinitionId + " with " + newAnatomyProfileDefinitionId, now));

                // PermanentModifications/MigrationHistory are preserved --
                // only the profile/body-part shape is replaced.
                var newAnatomy = new Odyssey.Domain.Character.CharacterAnatomy(newAnatomyProfileDefinitionId, newAnatomyProfileVersion, newBodyParts, current.Anatomy.PermanentModifications, newMigrationHistory, current.Anatomy.Revision + 1);

                var payload = new JObject
                {
                    ["fromAnatomyProfileDefinitionId"] = current.Anatomy.AnatomyProfileDefinitionId.ToString(),
                    ["toAnatomyProfileDefinitionId"] = newAnatomyProfileDefinitionId.ToString(),
                    ["toAnatomyProfileVersion"] = newAnatomyProfileVersion,
                    ["actorUserId"] = actorUserId.ToString(),
                };

                return Result<AnatomyMutation>.Success(new AnatomyMutation(newAnatomy, "odyssey.persistence.character_anatomy_changed", payload));
            });
        }

        public Result<CharacterRecord> ApplyPermanentModification(CampaignHandle campaign, CharacterId characterId, BodyPartId attachedToBodyPartId, string kind, string description, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAnatomyRevision, CommandId commandId, CorrelationId correlationId)
        {
            if (!attachedToBodyPartId.IsValid) throw new ArgumentException("AttachedToBodyPartId is required.", nameof(attachedToBodyPartId));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));

            return MutateAnatomy(campaign, characterId, actorIsMainGm, expectedCharacterAnatomyRevision, commandId, correlationId, (current, connection, transaction) =>
            {
                if (current.Anatomy == null)
                {
                    return Result<AnatomyMutation>.Failure(PersistenceFailures.CharacterAnatomyNotInitialized(correlationId));
                }

                // Product section 18: "применить протез... после dependency
                // preview" -- the real, checkable dependency here is that
                // the target body part must exist.
                bool targetExists = false;
                foreach (BodyPart candidate in current.Anatomy.BodyParts)
                {
                    if (candidate.BodyPartId.Equals(attachedToBodyPartId)) { targetExists = true; break; }
                }

                if (!targetExists)
                {
                    return Result<AnatomyMutation>.Failure(PersistenceFailures.CharacterBodyPartNotFound(correlationId));
                }

                UtcInstant now = _clock.GetUtcNow();
                var newModification = new PermanentModification(PermanentModificationId.NewId(now), attachedToBodyPartId, kind, description, now);
                var newModifications = new List<PermanentModification>(current.Anatomy.PermanentModifications.Count + 1);
                newModifications.AddRange(current.Anatomy.PermanentModifications);
                newModifications.Add(newModification);

                var newMigrationHistory = new List<AnatomyMigrationEntry>(current.Anatomy.MigrationHistory.Count + 1);
                newMigrationHistory.AddRange(current.Anatomy.MigrationHistory);
                newMigrationHistory.Add(new AnatomyMigrationEntry("PermanentModificationApplied", kind + " applied to " + attachedToBodyPartId, now));

                var newAnatomy = new Odyssey.Domain.Character.CharacterAnatomy(current.Anatomy.AnatomyProfileDefinitionId, current.Anatomy.AnatomyProfileVersion, current.Anatomy.BodyParts, newModifications, newMigrationHistory, current.Anatomy.Revision + 1);

                var payload = new JObject
                {
                    ["permanentModificationId"] = newModification.PermanentModificationId.ToString(),
                    ["attachedToBodyPartId"] = attachedToBodyPartId.ToString(),
                    ["kind"] = kind,
                    ["actorUserId"] = actorUserId.ToString(),
                };

                return Result<AnatomyMutation>.Success(new AnatomyMutation(newAnatomy, "odyssey.persistence.character_anatomy_changed", payload));
            });
        }

        /// <summary>
        /// ODY-S04-109 section 1.2: unlike <see cref="MutateAbilities"/>/<see cref="MutateResources"/>
        /// (multi-entry collections), <c>CharacterAnatomy</c> is a SINGLE
        /// snapshot object -- this helper mirrors <see cref="MutateOwnership"/>'s
        /// own shape instead (single-object section, MainGM check hoisted
        /// before the transaction, one un-parameterized section revision).
        /// </summary>
        private Result<CharacterRecord> MutateAnatomy(
            CampaignHandle campaign,
            CharacterId characterId,
            bool actorIsMainGm,
            long expectedCharacterAnatomyRevision,
            CommandId commandId,
            CorrelationId correlationId,
            Func<CharacterRecord, SqliteConnection, SqliteTransaction, Result<AnatomyMutation>> mutate)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (expectedCharacterAnatomyRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedCharacterAnatomyRevision));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            // Product section 18: "GM может..." -- every anatomy command is MainGM-only, checked before touching the database at all.
            if (!actorIsMainGm)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterAnatomyOperationDenied(correlationId));
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

                        if (current.Revisions.CharacterAnatomyRevision != expectedCharacterAnatomyRevision)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(PersistenceFailures.CharacterRevisionConflict(correlationId));
                        }

                        Result<AnatomyMutation> mutationResult = mutate(current, connection, transaction);
                        if (mutationResult.IsFailure)
                        {
                            return Result<PipelineWrite<CharacterRecord>>.Failure(mutationResult.Error);
                        }

                        AnatomyMutation mutation = mutationResult.Value;
                        UtcInstant now = _clock.GetUtcNow();
                        long newAnatomyRevision = current.Revisions.CharacterAnatomyRevision + 1;
                        long newCharacterRevision = current.Revisions.CharacterRevision + 1;

                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText = "UPDATE Character SET AnatomyJson = $anatomyJson, CharacterAnatomyRevision = $anatomyRevision, CharacterRevision = $characterRevision, UpdatedAt = $updatedAt, LastCommandId = $lastCommandId WHERE CharacterId = $characterId;";
                            update.Parameters.AddWithValue("$anatomyJson", SerializeAnatomy(mutation.NewAnatomy));
                            update.Parameters.AddWithValue("$anatomyRevision", newAnatomyRevision);
                            update.Parameters.AddWithValue("$characterRevision", newCharacterRevision);
                            update.Parameters.AddWithValue("$updatedAt", now.ToString());
                            update.Parameters.AddWithValue("$lastCommandId", commandId.ToString());
                            update.Parameters.AddWithValue("$characterId", characterId.ToString());
                            update.ExecuteNonQuery();
                        }

                        CharacterSectionRevisions newRevisions = WithRevisions(current.Revisions, characterRevision: newCharacterRevision, characterAnatomyRevision: newAnatomyRevision);
                        var record = new CharacterRecord(characterId, campaign.CampaignId, current.CharacterKind, current.LifecycleStatus, current.ApprovalState, current.DisplayName, current.PortraitReference, current.Ownership, newRevisions, current.RulesetVersion, current.AnatomyProfileRef, current.TemplateId, current.TemplateVersionAtCopyTime, current.SeedCopy, current.SubmittedAt, current.DevelopmentPool, current.Attributes, current.Skills, current.Abilities, current.Resources, mutation.NewAnatomy, current.CreatedAt, now);

                        mutation.PayloadExtra["characterId"] = characterId.ToString();
                        mutation.PayloadExtra["displayNameSnapshot"] = current.DisplayName;
                        mutation.PayloadExtra["newCharacterAnatomyRevision"] = newAnatomyRevision;
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

        /// <summary>ODY-S04-109: the pure business-logic result <see cref="MutateAnatomy"/>'s caller-supplied callback returns.</summary>
        private sealed class AnatomyMutation
        {
            public AnatomyMutation(Odyssey.Domain.Character.CharacterAnatomy newAnatomy, string eventType, JObject payloadExtra)
            {
                NewAnatomy = newAnatomy;
                EventType = eventType;
                PayloadExtra = payloadExtra;
            }

            public Odyssey.Domain.Character.CharacterAnatomy NewAnatomy { get; }
            public string EventType { get; }
            public JObject PayloadExtra { get; }
        }

        private Result<AdvancementRecommendationRecord> FindAdvancementRecommendationByCommandId(CampaignHandle campaign, CommandId commandId, CorrelationId correlationId)
        {
            try
            {
                using SqliteConnection connection = OpenConnection(campaign.RootPath);
                EnsureCharacterTables(connection);

                using var select = connection.CreateCommand();
                select.CommandText = "SELECT RecommendationId, CharacterId, SkillDefinitionId, TargetLevel, ReservedAmount, EvidenceIdsJson, Status, Revision, CreatedAt FROM AdvancementRecommendation WHERE CommandId = $commandId LIMIT 1;";
                select.Parameters.AddWithValue("$commandId", commandId.ToString());
                using SqliteDataReader reader = select.ExecuteReader();
                if (!reader.Read())
                {
                    return Result<AdvancementRecommendationRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId));
                }

                return Result<AdvancementRecommendationRecord>.Success(ReadAdvancementRecommendationRecord(reader));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<AdvancementRecommendationRecord>.Failure(PersistenceFailures.CharacterIoFailed(correlationId));
            }
        }

        private static AdvancementRecommendationRecord? SelectAdvancementRecommendationForUpdate(SqliteConnection connection, SqliteTransaction transaction, CharacterId characterId, AdvancementRecommendationId recommendationId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT RecommendationId, CharacterId, SkillDefinitionId, TargetLevel, ReservedAmount, EvidenceIdsJson, Status, Revision, CreatedAt FROM AdvancementRecommendation WHERE CharacterId = $characterId AND RecommendationId = $recommendationId LIMIT 1;";
            select.Parameters.AddWithValue("$characterId", characterId.ToString());
            select.Parameters.AddWithValue("$recommendationId", recommendationId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            return reader.Read() ? ReadAdvancementRecommendationRecord(reader) : null;
        }

        private static AdvancementRecommendationRecord ReadAdvancementRecommendationRecord(SqliteDataReader reader)
        {
            AdvancementRecommendationId recommendationId = AdvancementRecommendationId.Parse(reader.GetString(0));
            CharacterId characterId = CharacterId.Parse(reader.GetString(1));
            SkillDefinitionId skillDefinitionId = SkillDefinitionId.Parse(reader.GetString(2));
            long targetLevel = reader.GetInt64(3);
            long reservedAmount = reader.GetInt64(4);
            IReadOnlyList<CriticalSuccessEvidenceId> evidenceIds = DeserializeEvidenceIds(reader.GetString(5));
            var status = (AdvancementRecommendationStatus)Enum.Parse(typeof(AdvancementRecommendationStatus), reader.GetString(6));
            long revision = reader.GetInt64(7);
            UtcInstant createdAt = UtcInstant.Parse(reader.GetString(8));
            return new AdvancementRecommendationRecord(recommendationId, characterId, skillDefinitionId, targetLevel, reservedAmount, evidenceIds, status, revision, createdAt);
        }

        private static void InsertAdvancementRecommendation(SqliteConnection connection, SqliteTransaction transaction, CampaignId campaignId, AdvancementRecommendationRecord record, CommandId commandId)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO AdvancementRecommendation (RecommendationId, CampaignId, CharacterId, SkillDefinitionId, TargetLevel, ReservedAmount, EvidenceIdsJson, Status, Revision, CreatedAt, CommandId) VALUES ($recommendationId, $campaignId, $characterId, $skillDefinitionId, $targetLevel, $reservedAmount, $evidenceIdsJson, $status, $revision, $createdAt, $commandId);";
            insert.Parameters.AddWithValue("$recommendationId", record.RecommendationId.ToString());
            insert.Parameters.AddWithValue("$campaignId", campaignId.ToString());
            insert.Parameters.AddWithValue("$characterId", record.CharacterId.ToString());
            insert.Parameters.AddWithValue("$skillDefinitionId", record.SkillDefinitionId.ToString());
            insert.Parameters.AddWithValue("$targetLevel", record.TargetLevel);
            insert.Parameters.AddWithValue("$reservedAmount", record.ReservedAmount);
            insert.Parameters.AddWithValue("$evidenceIdsJson", SerializeEvidenceIds(record.EvidenceIds));
            insert.Parameters.AddWithValue("$status", record.Status.ToString());
            insert.Parameters.AddWithValue("$revision", record.Revision);
            insert.Parameters.AddWithValue("$createdAt", record.CreatedAt.ToString());
            insert.Parameters.AddWithValue("$commandId", commandId.ToString());
            insert.ExecuteNonQuery();
        }

        private static void UpdateAdvancementRecommendationStatus(SqliteConnection connection, SqliteTransaction transaction, AdvancementRecommendationId recommendationId, AdvancementRecommendationStatus newStatus, long newRevision, CommandId commandId)
        {
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE AdvancementRecommendation SET Status = $status, Revision = $revision, CommandId = $commandId WHERE RecommendationId = $recommendationId;";
            update.Parameters.AddWithValue("$status", newStatus.ToString());
            update.Parameters.AddWithValue("$revision", newRevision);
            update.Parameters.AddWithValue("$commandId", commandId.ToString());
            update.Parameters.AddWithValue("$recommendationId", recommendationId.ToString());
            update.ExecuteNonQuery();
        }

        /// <summary>ODY-S04-107 (pkt 0 gap fix): co-commits one <c>AdvancementPurchase</c> row in the same transaction as the causing purchase -- mirrors <see cref="InsertAdvancementRecommendation"/>'s exact shape for a sibling side table.</summary>
        private static void InsertAdvancementPurchase(SqliteConnection connection, SqliteTransaction transaction, CampaignId campaignId, AdvancementPurchase purchase)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO AdvancementPurchase (PurchaseId, CampaignId, CharacterId, OperationKind, TargetDefinitionId, FromValue, ToValue, Cost, RequirementsSnapshot, RulesetVersion, ActorUserId, CreatedAt, Status) VALUES ($purchaseId, $campaignId, $characterId, $operationKind, $targetDefinitionId, $fromValue, $toValue, $cost, $requirementsSnapshot, $rulesetVersion, $actorUserId, $createdAt, $status);";
            insert.Parameters.AddWithValue("$purchaseId", purchase.PurchaseId.ToString());
            insert.Parameters.AddWithValue("$campaignId", campaignId.ToString());
            insert.Parameters.AddWithValue("$characterId", purchase.CharacterId.ToString());
            insert.Parameters.AddWithValue("$operationKind", purchase.OperationKind.ToString());
            insert.Parameters.AddWithValue("$targetDefinitionId", purchase.TargetDefinitionId);
            insert.Parameters.AddWithValue("$fromValue", purchase.FromValue);
            insert.Parameters.AddWithValue("$toValue", purchase.ToValue);
            insert.Parameters.AddWithValue("$cost", purchase.Cost);
            insert.Parameters.AddWithValue("$requirementsSnapshot", purchase.RequirementsSnapshot);
            insert.Parameters.AddWithValue("$rulesetVersion", purchase.RulesetVersion);
            insert.Parameters.AddWithValue("$actorUserId", purchase.ActorUserId.ToString());
            insert.Parameters.AddWithValue("$createdAt", purchase.CreatedAt.ToString());
            insert.Parameters.AddWithValue("$status", purchase.Status.ToString());
            insert.ExecuteNonQuery();
        }

        private static void UpdateAdvancementPurchaseStatus(SqliteConnection connection, SqliteTransaction transaction, AdvancementPurchaseId purchaseId, AdvancementPurchaseStatus newStatus)
        {
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE AdvancementPurchase SET Status = $status WHERE PurchaseId = $purchaseId;";
            update.Parameters.AddWithValue("$status", newStatus.ToString());
            update.Parameters.AddWithValue("$purchaseId", purchaseId.ToString());
            update.ExecuteNonQuery();
        }

        private const string AdvancementPurchaseColumns =
            "SELECT PurchaseId, CharacterId, OperationKind, TargetDefinitionId, FromValue, ToValue, Cost, RequirementsSnapshot, RulesetVersion, ActorUserId, CreatedAt, Status FROM AdvancementPurchase";

        private static AdvancementPurchase ReadAdvancementPurchase(SqliteDataReader reader)
        {
            AdvancementPurchaseId purchaseId = AdvancementPurchaseId.Parse(reader.GetString(0));
            CharacterId characterId = CharacterId.Parse(reader.GetString(1));
            var operationKind = (AdvancementOperationKind)Enum.Parse(typeof(AdvancementOperationKind), reader.GetString(2));
            string targetDefinitionId = reader.GetString(3);
            long fromValue = reader.GetInt64(4);
            long toValue = reader.GetInt64(5);
            long cost = reader.GetInt64(6);
            string requirementsSnapshot = reader.GetString(7);
            string rulesetVersion = reader.GetString(8);
            UserId actorUserId = UserId.Parse(reader.GetString(9));
            UtcInstant createdAt = UtcInstant.Parse(reader.GetString(10));
            var status = (AdvancementPurchaseStatus)Enum.Parse(typeof(AdvancementPurchaseStatus), reader.GetString(11));
            return new AdvancementPurchase(purchaseId, characterId, operationKind, targetDefinitionId, fromValue, toValue, cost, requirementsSnapshot, rulesetVersion, actorUserId, createdAt, status);
        }

        private static AdvancementPurchase? SelectAdvancementPurchaseForUpdate(SqliteConnection connection, SqliteTransaction transaction, CharacterId characterId, AdvancementPurchaseId purchaseId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = AdvancementPurchaseColumns + " WHERE CharacterId = $characterId AND PurchaseId = $purchaseId LIMIT 1;";
            select.Parameters.AddWithValue("$characterId", characterId.ToString());
            select.Parameters.AddWithValue("$purchaseId", purchaseId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            return reader.Read() ? ReadAdvancementPurchase(reader) : null;
        }

        /// <summary>ODY-S04-107: every <c>AdvancementPurchase</c> row for one Character, ordered oldest-first -- used both by <see cref="ICharacterRepository.GetAdvancementPurchases"/> and, inside a transaction, by <c>ComputeRespecPlan</c>'s own fresh server-side read.</summary>
        private static IReadOnlyList<AdvancementPurchase> SelectAdvancementPurchasesForCharacter(SqliteConnection connection, SqliteTransaction? transaction, CharacterId characterId)
        {
            using var select = connection.CreateCommand();
            if (transaction != null) select.Transaction = transaction;
            select.CommandText = AdvancementPurchaseColumns + " WHERE CharacterId = $characterId ORDER BY CreatedAt, PurchaseId;";
            select.Parameters.AddWithValue("$characterId", characterId.ToString());
            var list = new List<AdvancementPurchase>();
            using SqliteDataReader reader = select.ExecuteReader();
            while (reader.Read())
            {
                list.Add(ReadAdvancementPurchase(reader));
            }

            return list;
        }

        /// <summary>
        /// ODY-S04-107: locates the <c>EventSequence</c> of the original
        /// forward event (<c>character_attribute_increased</c>/
        /// <c>character_skill_level_purchased</c>) that produced a given
        /// <c>AdvancementPurchase</c>, for <c>RevertAdvancementPurchase</c>'s
        /// own <c>OriginalEventId</c> (ADR-012 section 6). Mirrors
        /// <c>GetCharacterHistory</c>'s own "no dedicated AggregateId column,
        /// filter DomainEvents by EventType + payload content" convention --
        /// PurchaseId is embedded in every purchase-producing event's own
        /// payload specifically so this lookup is possible without a new
        /// schema column. The narrowing <c>PayloadJson LIKE</c> clause is a
        /// coarse pre-filter only (no JSON1 extension is used anywhere in
        /// this codebase); the exact match is re-verified in C# below,
        /// because a canonical PurchaseId (<c>advpur_</c> + 32 hex chars) can
        /// never legitimately collide as a substring of another purchase's
        /// own id.
        /// </summary>
        private static long? FindOriginatingEventSequence(SqliteConnection connection, SqliteTransaction transaction, CampaignId campaignId, AdvancementPurchaseId purchaseId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText =
                "SELECT EventSequence, PayloadJson FROM DomainEvents " +
                "WHERE CampaignId = $campaignId AND (EventType = $attrType OR EventType = $skillType) AND PayloadJson LIKE $needle " +
                "ORDER BY EventSequence;";
            select.Parameters.AddWithValue("$campaignId", campaignId.ToString());
            select.Parameters.AddWithValue("$attrType", "odyssey.persistence.character_attribute_increased");
            select.Parameters.AddWithValue("$skillType", "odyssey.persistence.character_skill_level_purchased");
            select.Parameters.AddWithValue("$needle", "%\"purchaseId\":\"" + purchaseId + "\"%");
            using SqliteDataReader reader = select.ExecuteReader();
            while (reader.Read())
            {
                long eventSequence = reader.GetInt64(0);
                string payloadJson = reader.GetString(1);
                var payload = (JObject)ParseJsonPreservingStrings(payloadJson);
                if (payload.TryGetValue("purchaseId", out JToken? value) && string.Equals((string?)value, purchaseId.ToString(), StringComparison.Ordinal))
                {
                    return eventSequence;
                }
            }

            return null;
        }

        private static string SerializeEvidenceIds(IReadOnlyList<CriticalSuccessEvidenceId> evidenceIds)
        {
            var array = new JArray();
            foreach (CriticalSuccessEvidenceId evidenceId in evidenceIds)
            {
                array.Add(evidenceId.ToString());
            }

            return array.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static IReadOnlyList<CriticalSuccessEvidenceId> DeserializeEvidenceIds(string json)
        {
            var array = (JArray)ParseJsonPreservingStrings(json);
            var list = new List<CriticalSuccessEvidenceId>(array.Count);
            foreach (JToken item in array)
            {
                list.Add(CriticalSuccessEvidenceId.Parse((string)item!));
            }

            return list;
        }

        private static CriticalSuccessEvidenceRecord? SelectEvidenceForUpdate(SqliteConnection connection, SqliteTransaction transaction, CriticalSuccessEvidenceId evidenceId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT EvidenceId, CharacterId, SkillDefinitionId, SourceDiceRollId, SourceActionId, OccurredAt, RulesetVersion, UsedByAdvancementId, Revision FROM CriticalSuccessEvidence WHERE EvidenceId = $evidenceId LIMIT 1;";
            select.Parameters.AddWithValue("$evidenceId", evidenceId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            return reader.Read() ? ReadCriticalSuccessEvidenceRecord(reader) : null;
        }

        private static void MarkEvidenceUsed(SqliteConnection connection, SqliteTransaction transaction, CriticalSuccessEvidenceId evidenceId, AdvancementRecommendationId advancementId, long expectedRevision)
        {
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            // ADR-024 section 7.1: guarded by the evidence row's own revision
            // -- the WHERE clause's own Revision check is this method's real
            // protection against a race the caller's earlier read might have
            // missed; RowsAffected == 0 would mean it was already consumed
            // between this transaction's own read and write, which cannot
            // happen here since SQLite serializes writers and both the read
            // and this write share the same transaction.
            update.CommandText = "UPDATE CriticalSuccessEvidence SET UsedByAdvancementId = $advancementId, Revision = Revision + 1 WHERE EvidenceId = $evidenceId AND Revision = $expectedRevision;";
            update.Parameters.AddWithValue("$advancementId", advancementId.ToString());
            update.Parameters.AddWithValue("$evidenceId", evidenceId.ToString());
            update.Parameters.AddWithValue("$expectedRevision", expectedRevision);
            update.ExecuteNonQuery();
        }

        private static CriticalSuccessEvidenceRecord ReadCriticalSuccessEvidenceRecord(SqliteDataReader reader)
        {
            CriticalSuccessEvidenceId evidenceId = CriticalSuccessEvidenceId.Parse(reader.GetString(0));
            CharacterId characterId = CharacterId.Parse(reader.GetString(1));
            SkillDefinitionId skillDefinitionId = SkillDefinitionId.Parse(reader.GetString(2));
            string? sourceDiceRollId = reader.IsDBNull(3) ? null : reader.GetString(3);
            string? sourceActionId = reader.IsDBNull(4) ? null : reader.GetString(4);
            UtcInstant occurredAt = UtcInstant.Parse(reader.GetString(5));
            string rulesetVersion = reader.GetString(6);
            AdvancementRecommendationId? usedByAdvancementId = reader.IsDBNull(7) ? (AdvancementRecommendationId?)null : AdvancementRecommendationId.Parse(reader.GetString(7));
            long revision = reader.GetInt64(8);
            return new CriticalSuccessEvidenceRecord(evidenceId, characterId, skillDefinitionId, sourceDiceRollId, sourceActionId, occurredAt, rulesetVersion, usedByAdvancementId, revision);
        }

        private static Result<CriticalSuccessEvidenceRecord> ReplayEvidence(SqliteConnection connection, SqliteTransaction transaction, CommandId commandId, CorrelationId correlationId)
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = "SELECT EvidenceId, CharacterId, SkillDefinitionId, SourceDiceRollId, SourceActionId, OccurredAt, RulesetVersion, UsedByAdvancementId, Revision FROM CriticalSuccessEvidence WHERE CommandId = $commandId LIMIT 1;";
            select.Parameters.AddWithValue("$commandId", commandId.ToString());
            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read())
            {
                return Result<CriticalSuccessEvidenceRecord>.Failure(PersistenceFailures.CommandReplayFailed(correlationId));
            }

            return Result<CriticalSuccessEvidenceRecord>.Success(ReadCriticalSuccessEvidenceRecord(reader));
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
            Func<CharacterRecord, SqliteConnection, SqliteTransaction, Result<MechanicsMutation>> mutate)
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

                        Result<MechanicsMutation> mutationResult = mutate(current, connection, transaction);
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
                            update.CommandText = "UPDATE Character SET PoolEarned = $poolEarned, PoolSpent = $poolSpent, PoolReserved = $poolReserved, AttributesJson = $attributesJson, SkillsJson = $skillsJson, MechanicsRevision = $mechanicsRevision, CharacterRevision = $characterRevision, UpdatedAt = $updatedAt, LastCommandId = $lastCommandId WHERE CharacterId = $characterId;";
                            update.Parameters.AddWithValue("$poolEarned", mutation.NewPool.Earned);
                            update.Parameters.AddWithValue("$poolSpent", mutation.NewPool.Spent);
                            update.Parameters.AddWithValue("$poolReserved", mutation.NewPool.Reserved);
                            update.Parameters.AddWithValue("$attributesJson", SerializeAttributes(mutation.NewAttributes));
                            update.Parameters.AddWithValue("$skillsJson", SerializeSkills(mutation.NewSkills));
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
                        var record = new CharacterRecord(characterId, campaign.CampaignId, current.CharacterKind, current.LifecycleStatus, current.ApprovalState, current.DisplayName, current.PortraitReference, current.Ownership, newRevisions, current.RulesetVersion, current.AnatomyProfileRef, current.TemplateId, current.TemplateVersionAtCopyTime, current.SeedCopy, current.SubmittedAt, mutation.NewPool, mutation.NewAttributes, mutation.NewSkills, current.Abilities, current.Resources, current.Anatomy, current.CreatedAt, now);

                        mutation.PayloadExtra["characterId"] = characterId.ToString();
                        mutation.PayloadExtra["displayNameSnapshot"] = current.DisplayName;
                        mutation.PayloadExtra["newMechanicsRevision"] = newMechanicsRevision;
                        mutation.PayloadExtra["newCharacterRevision"] = newCharacterRevision;

                        return Result<PipelineWrite<CharacterRecord>>.Success(new PipelineWrite<CharacterRecord>(
                            record, mutation.EventType, mutation.PayloadExtra.ToString(Newtonsoft.Json.Formatting.None), characterId.ToString(),
                            aggregateType: "character", aggregateId: characterId.ToString(), aggregateRevision: newCharacterRevision,
                            originalEventId: mutation.OriginalEventId, compensationGroupId: mutation.CompensationGroupId, isCompensating: mutation.IsCompensating));
                    });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException)
            {
                return Result<CharacterRecord>.Failure(PersistenceFailures.CharacterIoFailed(correlationId));
            }
        }

        /// <summary>
        /// ODY-S04-105/106: the pure business-logic result
        /// <see cref="MutateMechanics"/>'s caller-supplied callback returns --
        /// the new pool/attribute/skill state, the event to emit, and the
        /// ledger row(s) to co-commit. ODY-S04-106's callback additionally
        /// receives the live <see cref="SqliteConnection"/>/<see cref="SqliteTransaction"/>
        /// so it can read/write sibling tables (<c>AdvancementRecommendation</c>,
        /// <c>CriticalSuccessEvidence</c>) inside the exact same transaction,
        /// rather than this class growing a special-cased side-effect slot
        /// for that one caller.
        /// </summary>
        private sealed class MechanicsMutation
        {
            public MechanicsMutation(DevelopmentPool newPool, IReadOnlyList<AttributeValue> newAttributes, IReadOnlyList<CharacterSkill> newSkills, string eventType, JObject payloadExtra, IReadOnlyList<DevelopmentTransactionRecord> ledgerEntries, long? originalEventId = null, string? compensationGroupId = null, bool isCompensating = false)
            {
                NewPool = newPool;
                NewAttributes = newAttributes;
                NewSkills = newSkills;
                EventType = eventType;
                PayloadExtra = payloadExtra;
                LedgerEntries = ledgerEntries;
                OriginalEventId = originalEventId;
                CompensationGroupId = compensationGroupId;
                IsCompensating = isCompensating;
            }

            public DevelopmentPool NewPool { get; }
            public IReadOnlyList<AttributeValue> NewAttributes { get; }
            public IReadOnlyList<CharacterSkill> NewSkills { get; }
            public string EventType { get; }
            public JObject PayloadExtra { get; }
            public IReadOnlyList<DevelopmentTransactionRecord> LedgerEntries { get; }

            /// <summary>ODY-S04-107: ADR-012 section 6's compensating-event metadata -- optional, default "not compensating," so every pre-existing caller is unaffected. See <see cref="SqliteSavingPipeline.PipelineWrite{T}"/>'s own doc comment for the full rationale.</summary>
            public long? OriginalEventId { get; }
            public string? CompensationGroupId { get; }
            public bool IsCompensating { get; }
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
                        var record = new CharacterRecord(characterId, campaign.CampaignId, current.CharacterKind, current.LifecycleStatus, current.ApprovalState, newDisplayName, current.PortraitReference, current.Ownership, newRevisions, current.RulesetVersion, current.AnatomyProfileRef, current.TemplateId, current.TemplateVersionAtCopyTime, current.SeedCopy, current.SubmittedAt, current.DevelopmentPool, current.Attributes, current.Skills, current.Abilities, current.Resources, current.Anatomy, current.CreatedAt, now);

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
                        var record = new CharacterRecord(characterId, campaign.CampaignId, current.CharacterKind, current.LifecycleStatus, current.ApprovalState, current.DisplayName, portraitReference, current.Ownership, newRevisions, current.RulesetVersion, current.AnatomyProfileRef, current.TemplateId, current.TemplateVersionAtCopyTime, current.SeedCopy, current.SubmittedAt, current.DevelopmentPool, current.Attributes, current.Skills, current.Abilities, current.Resources, current.Anatomy, current.CreatedAt, now);

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
                        var record = new CharacterRecord(characterId, campaign.CampaignId, current.CharacterKind, current.LifecycleStatus, current.ApprovalState, current.DisplayName, current.PortraitReference, newOwnership, newRevisions, current.RulesetVersion, current.AnatomyProfileRef, current.TemplateId, current.TemplateVersionAtCopyTime, current.SeedCopy, current.SubmittedAt, current.DevelopmentPool, current.Attributes, current.Skills, current.Abilities, current.Resources, current.Anatomy, current.CreatedAt, now);

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
                        var record = new CharacterRecord(characterId, campaign.CampaignId, current.CharacterKind, current.LifecycleStatus, current.ApprovalState, current.DisplayName, current.PortraitReference, newOwnership, newRevisions, current.RulesetVersion, current.AnatomyProfileRef, current.TemplateId, current.TemplateVersionAtCopyTime, current.SeedCopy, current.SubmittedAt, current.DevelopmentPool, current.Attributes, current.Skills, current.Abilities, current.Resources, current.Anatomy, current.CreatedAt, now);

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
            "PoolEarned, PoolSpent, PoolReserved, AttributesJson, SkillsJson, AbilitiesJson, ResourcesJson, AnatomyJson, CreatedAt, UpdatedAt";

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
            IReadOnlyList<CharacterSkill> skills = DeserializeSkills(reader.GetString(34));
            IReadOnlyList<CharacterAbility> abilities = DeserializeAbilities(reader.GetString(35));
            IReadOnlyList<CharacterResource> resources = DeserializeResources(reader.GetString(36));
            CharacterAnatomy? anatomy = reader.IsDBNull(37) ? null : DeserializeAnatomy(reader.GetString(37));
            UtcInstant createdAt = UtcInstant.Parse(reader.GetString(38));
            UtcInstant updatedAt = UtcInstant.Parse(reader.GetString(39));

            return new CharacterRecord(characterId, campaignId, characterKind, lifecycleStatus, approvalState, displayName, portraitReference, ownership, revisions, rulesetVersion, anatomyProfileRef, templateId, templateVersionAtCopyTime, seedCopy, submittedAt, developmentPool, attributes, skills, abilities, resources, anatomy, createdAt, updatedAt);
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
            long? mechanicsRevision = null,
            long? characterAbilitiesRevision = null,
            long? characterResourcesRevision = null,
            long? characterAnatomyRevision = null) => new CharacterSectionRevisions(
                characterRevision ?? source.CharacterRevision,
                identityRevision ?? source.IdentityRevision,
                presentationRevision ?? source.PresentationRevision,
                source.CustomFieldsRevision,
                mechanicsRevision ?? source.MechanicsRevision,
                source.AttributeValuesRevision,
                source.CharacterSkillsRevision,
                characterAbilitiesRevision ?? source.CharacterAbilitiesRevision,
                characterResourcesRevision ?? source.CharacterResourcesRevision,
                characterAnatomyRevision ?? source.CharacterAnatomyRevision,
                ownershipRevision ?? source.OwnershipRevision,
                lifecycleRevision ?? source.LifecycleRevision,
                source.RuntimeStateRevision);

        private static string SerializeSkills(IReadOnlyList<CharacterSkill> skills)
        {
            var array = new JArray();
            foreach (CharacterSkill skill in skills)
            {
                array.Add(new JObject
                {
                    ["skillDefinitionId"] = skill.SkillDefinitionId.ToString(),
                    ["level"] = skill.Level,
                    ["permanentAdjustment"] = skill.PermanentAdjustment,
                    ["spentDevelopmentPoints"] = skill.SpentDevelopmentPoints,
                    ["revision"] = skill.Revision,
                });
            }

            return array.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static IReadOnlyList<CharacterSkill> DeserializeSkills(string json)
        {
            var array = (JArray)ParseJsonPreservingStrings(json);
            var list = new List<CharacterSkill>(array.Count);
            foreach (JToken token in array)
            {
                SkillDefinitionId skillDefinitionId = SkillDefinitionId.Parse((string)token["skillDefinitionId"]!);
                long level = (long)token["level"]!;
                long permanentAdjustment = (long)token["permanentAdjustment"]!;
                long spentDevelopmentPoints = (long)token["spentDevelopmentPoints"]!;
                long revision = (long)token["revision"]!;
                list.Add(new CharacterSkill(skillDefinitionId, level, permanentAdjustment, spentDevelopmentPoints, revision));
            }

            return list;
        }

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

        private static string SerializeAbilities(IReadOnlyList<CharacterAbility> abilities)
        {
            var array = new JArray();
            foreach (CharacterAbility ability in abilities)
            {
                array.Add(new JObject
                {
                    ["characterAbilityId"] = ability.CharacterAbilityId.ToString(),
                    ["abilityDefinitionId"] = ability.AbilityDefinitionId.ToString(),
                    ["sourceKind"] = ability.SourceKind.ToString(),
                    ["sourceRef"] = ability.SourceRef,
                    ["acquiredAt"] = ability.AcquiredAt.ToString(),
                    ["rankMode"] = ability.RankMode.ToString(),
                    ["numericRank"] = ability.NumericRank,
                    ["namedRankKey"] = ability.NamedRankKey,
                    ["isEnabled"] = ability.IsEnabled,
                    ["configuration"] = ability.Configuration,
                    ["usesState"] = ability.UsesState,
                    ["revision"] = ability.Revision,
                });
            }

            return array.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static IReadOnlyList<CharacterAbility> DeserializeAbilities(string json)
        {
            var array = (JArray)ParseJsonPreservingStrings(json);
            var list = new List<CharacterAbility>(array.Count);
            foreach (JToken token in array)
            {
                CharacterAbilityId characterAbilityId = CharacterAbilityId.Parse((string)token["characterAbilityId"]!);
                AbilityDefinitionId abilityDefinitionId = AbilityDefinitionId.Parse((string)token["abilityDefinitionId"]!);
                var sourceKind = (SourceKind)Enum.Parse(typeof(SourceKind), (string)token["sourceKind"]!);
                string? sourceRef = (string?)token["sourceRef"];
                UtcInstant acquiredAt = UtcInstant.Parse((string)token["acquiredAt"]!);
                var rankMode = (RankMode)Enum.Parse(typeof(RankMode), (string)token["rankMode"]!);
                long? numericRank = (long?)token["numericRank"];
                string? namedRankKey = (string?)token["namedRankKey"];
                bool isEnabled = (bool)token["isEnabled"]!;
                string configuration = (string)token["configuration"]!;
                string? usesState = (string?)token["usesState"];
                long revision = (long)token["revision"]!;
                list.Add(new CharacterAbility(characterAbilityId, abilityDefinitionId, sourceKind, sourceRef, acquiredAt, rankMode, numericRank, namedRankKey, isEnabled, configuration, usesState, revision));
            }

            return list;
        }

        private static string SerializeResources(IReadOnlyList<CharacterResource> resources)
        {
            var array = new JArray();
            foreach (CharacterResource resource in resources)
            {
                array.Add(new JObject
                {
                    ["characterResourceId"] = resource.CharacterResourceId.ToString(),
                    ["resourceDefinitionId"] = resource.ResourceDefinitionId.ToString(),
                    ["currentValue"] = resource.CurrentValue,
                    ["baseMaximum"] = resource.BaseMaximum,
                    ["permanentMaximumAdjustment"] = resource.PermanentMaximumAdjustment,
                    ["minimumValue"] = resource.MinimumValue,
                    ["recoveryRule"] = resource.RecoveryRule.ToString(),
                    ["revision"] = resource.Revision,
                });
            }

            return array.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static IReadOnlyList<CharacterResource> DeserializeResources(string json)
        {
            var array = (JArray)ParseJsonPreservingStrings(json);
            var list = new List<CharacterResource>(array.Count);
            foreach (JToken token in array)
            {
                CharacterResourceId characterResourceId = CharacterResourceId.Parse((string)token["characterResourceId"]!);
                ResourceDefinitionId resourceDefinitionId = ResourceDefinitionId.Parse((string)token["resourceDefinitionId"]!);
                long currentValue = (long)token["currentValue"]!;
                long baseMaximum = (long)token["baseMaximum"]!;
                long permanentMaximumAdjustment = (long)token["permanentMaximumAdjustment"]!;
                long minimumValue = (long)token["minimumValue"]!;
                var recoveryRule = (RecoveryRule)Enum.Parse(typeof(RecoveryRule), (string)token["recoveryRule"]!);
                long revision = (long)token["revision"]!;
                list.Add(new CharacterResource(characterResourceId, resourceDefinitionId, currentValue, baseMaximum, permanentMaximumAdjustment, minimumValue, recoveryRule, revision));
            }

            return list;
        }

        private static string SerializeAnatomy(CharacterAnatomy anatomy)
        {
            var bodyParts = new JArray();
            foreach (BodyPart part in anatomy.BodyParts)
            {
                bodyParts.Add(new JObject
                {
                    ["bodyPartId"] = part.BodyPartId.ToString(),
                    ["name"] = part.Name,
                    ["damageLimit"] = part.DamageLimit,
                    ["attachedToBodyPartId"] = part.AttachedToBodyPartId?.ToString(),
                    ["properties"] = part.Properties,
                });
            }

            var modifications = new JArray();
            foreach (PermanentModification modification in anatomy.PermanentModifications)
            {
                modifications.Add(new JObject
                {
                    ["permanentModificationId"] = modification.PermanentModificationId.ToString(),
                    ["attachedToBodyPartId"] = modification.AttachedToBodyPartId.ToString(),
                    ["kind"] = modification.Kind,
                    ["description"] = modification.Description,
                    ["appliedAt"] = modification.AppliedAt.ToString(),
                });
            }

            var migrationHistory = new JArray();
            foreach (AnatomyMigrationEntry entry in anatomy.MigrationHistory)
            {
                migrationHistory.Add(new JObject
                {
                    ["actionKind"] = entry.ActionKind,
                    ["description"] = entry.Description,
                    ["occurredAt"] = entry.OccurredAt.ToString(),
                });
            }

            var root = new JObject
            {
                ["anatomyProfileDefinitionId"] = anatomy.AnatomyProfileDefinitionId.ToString(),
                ["anatomyProfileVersion"] = anatomy.AnatomyProfileVersion,
                ["bodyParts"] = bodyParts,
                ["permanentModifications"] = modifications,
                ["migrationHistory"] = migrationHistory,
                ["revision"] = anatomy.Revision,
            };

            return root.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static CharacterAnatomy DeserializeAnatomy(string json)
        {
            var root = (JObject)ParseJsonPreservingStrings(json);

            var bodyParts = new List<BodyPart>();
            foreach (JToken token in (JArray)root["bodyParts"]!)
            {
                BodyPartId bodyPartId = BodyPartId.Parse((string)token["bodyPartId"]!);
                string name = (string)token["name"]!;
                long damageLimit = (long)token["damageLimit"]!;
                string? attachedToRaw = (string?)token["attachedToBodyPartId"];
                BodyPartId? attachedToBodyPartId = attachedToRaw == null ? (BodyPartId?)null : BodyPartId.Parse(attachedToRaw);
                string properties = (string)token["properties"]!;
                bodyParts.Add(new BodyPart(bodyPartId, name, damageLimit, attachedToBodyPartId, properties));
            }

            var modifications = new List<PermanentModification>();
            foreach (JToken token in (JArray)root["permanentModifications"]!)
            {
                PermanentModificationId permanentModificationId = PermanentModificationId.Parse((string)token["permanentModificationId"]!);
                BodyPartId attachedToBodyPartId = BodyPartId.Parse((string)token["attachedToBodyPartId"]!);
                string kind = (string)token["kind"]!;
                string description = (string)token["description"]!;
                UtcInstant appliedAt = UtcInstant.Parse((string)token["appliedAt"]!);
                modifications.Add(new PermanentModification(permanentModificationId, attachedToBodyPartId, kind, description, appliedAt));
            }

            var migrationHistory = new List<AnatomyMigrationEntry>();
            foreach (JToken token in (JArray)root["migrationHistory"]!)
            {
                string actionKind = (string)token["actionKind"]!;
                string description = (string)token["description"]!;
                UtcInstant occurredAt = UtcInstant.Parse((string)token["occurredAt"]!);
                migrationHistory.Add(new AnatomyMigrationEntry(actionKind, description, occurredAt));
            }

            AnatomyProfileDefinitionId anatomyProfileDefinitionId = AnatomyProfileDefinitionId.Parse((string)root["anatomyProfileDefinitionId"]!);
            string anatomyProfileVersion = (string)root["anatomyProfileVersion"]!;
            long revision = (long)root["revision"]!;

            return new CharacterAnatomy(anatomyProfileDefinitionId, anatomyProfileVersion, bodyParts, modifications, migrationHistory, revision);
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
    SkillsJson TEXT NOT NULL DEFAULT '[]',
    AbilitiesJson TEXT NOT NULL DEFAULT '[]',
    ResourcesJson TEXT NOT NULL DEFAULT '[]',
    AnatomyJson TEXT,
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

            using var criticalEvidenceTable = connection.CreateCommand();
            criticalEvidenceTable.CommandText = @"
CREATE TABLE IF NOT EXISTS CriticalSuccessEvidence (
    EvidenceId TEXT PRIMARY KEY,
    CampaignId TEXT NOT NULL,
    CharacterId TEXT NOT NULL,
    SkillDefinitionId TEXT NOT NULL,
    SourceDiceRollId TEXT,
    SourceActionId TEXT,
    OccurredAt TEXT NOT NULL,
    RulesetVersion TEXT NOT NULL,
    UsedByAdvancementId TEXT,
    Revision INTEGER NOT NULL,
    CommandId TEXT NOT NULL
);";
            criticalEvidenceTable.ExecuteNonQuery();

            using var recommendationTable = connection.CreateCommand();
            recommendationTable.CommandText = @"
CREATE TABLE IF NOT EXISTS AdvancementRecommendation (
    RecommendationId TEXT PRIMARY KEY,
    CampaignId TEXT NOT NULL,
    CharacterId TEXT NOT NULL,
    SkillDefinitionId TEXT NOT NULL,
    TargetLevel INTEGER NOT NULL,
    ReservedAmount INTEGER NOT NULL,
    EvidenceIdsJson TEXT NOT NULL DEFAULT '[]',
    Status TEXT NOT NULL,
    Revision INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    CommandId TEXT NOT NULL
);";
            recommendationTable.ExecuteNonQuery();

            using var advancementPurchaseTable = connection.CreateCommand();
            advancementPurchaseTable.CommandText = @"
CREATE TABLE IF NOT EXISTS AdvancementPurchase (
    PurchaseId TEXT PRIMARY KEY,
    CampaignId TEXT NOT NULL,
    CharacterId TEXT NOT NULL,
    OperationKind TEXT NOT NULL,
    TargetDefinitionId TEXT NOT NULL,
    FromValue INTEGER NOT NULL,
    ToValue INTEGER NOT NULL,
    Cost INTEGER NOT NULL,
    RequirementsSnapshot TEXT NOT NULL,
    RulesetVersion TEXT NOT NULL,
    ActorUserId TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    Status TEXT NOT NULL
);";
            advancementPurchaseTable.ExecuteNonQuery();

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
