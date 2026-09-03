using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;
using Odyssey.Rules.Character;

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S04-113: real, non-stubbed tests against a real temp-directory
    /// campaign and a real SQLite database, mirroring
    /// <see cref="CharacterExportImportTests"/>'s exact fixture convention.
    /// Covers <c>PreviewCharacterRulesetMigration</c> (read-only,
    /// identity-mapping, unresolved-decision surfacing) and
    /// <c>ApplyCharacterRulesetMigration</c>/<c>RevertCharacterRulesetMigration</c>
    /// (stale-plan rejection, atomicity via duplicate-CommandId replay,
    /// compensating-batch revert) -- deliberately scoped to the identity/
    /// UnresolvedDecisions mapping this task's own ExecPlan section 4/5
    /// decided, never a fabricated cross-Ruleset value transformation.
    /// </summary>
    public sealed class CharacterRulesetMigrationTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
        private static readonly AttributeDefinitionId Strength = AttributeDefinitionId.Parse("Strength");

        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;
        private SqliteCharacterRepository _characterRepository = null!;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s04-113-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> created = _campaignRepository.Create(new CreateCampaignRequest(_campaignDir, "Ruleset Migration Test Campaign", "ruleset.core", "1.0.0", "0.1.0"), NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _characterRepository = new SqliteCharacterRepository(Clock);
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaignRepository.Close(_campaign, TestCorrelationId); } catch (IOException) { }
            try { if (Directory.Exists(_campaignDir)) Directory.Delete(_campaignDir, recursive: true); } catch (IOException) { }
        }

        private CharacterRecord CreateCharacterWithAttribute()
        {
            var bindRequest = new BindDraftToCampaignRequest(_campaign, CharacterKind.PlayerCharacter, "Migration Character", "Humanoid", NewUserId(), CharacterCreationSeed.None(), null, null);
            Result<CharacterRecord> bound = _characterRepository.BindDraftToCampaign(bindRequest, NewCommandId(), TestCorrelationId);
            Assert.That(bound.IsSuccess, Is.True);

            Result<CharacterRecord> granted = _characterRepository.GrantDevelopmentPoints(_campaign, bound.Value.CharacterId, 10, "fixture grant", NewUserId(), actorIsMainGm: true, bound.Value.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(granted.IsSuccess, Is.True);

            Result<CharacterRecord> purchased = _characterRepository.PurchaseAttributeIncrease(_campaign, bound.Value.CharacterId, Strength, toValue: 2, NewUserId(), actorIsMainGm: true, granted.Value.Revisions.MechanicsRevision, expectedAttributeRevision: 0, NewCommandId(), TestCorrelationId);
            Assert.That(purchased.IsSuccess, Is.True);
            return purchased.Value;
        }

        private static RulesetDefinitionCatalog FullyCompatibleCatalog() => new RulesetDefinitionCatalog(
            new[] { Strength.ToString() }, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

        private static RulesetDefinitionCatalog IncompatibleCatalog() => new RulesetDefinitionCatalog(
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

        // ---- PreviewCharacterRulesetMigration -----------------------------------

        [Test]
        public void Preview_IsReadOnly_NoEventWritten_NoStateChange()
        {
            CharacterRecord character = CreateCharacterWithAttribute();
            long revisionBefore = character.Revisions.CharacterRevision;

            Result<CharacterRulesetMigrationPlan> preview = _characterRepository.PreviewCharacterRulesetMigration(_campaign, character.CharacterId, "ruleset.core", "1.1.0", FullyCompatibleCatalog(), TestCorrelationId);

            Assert.That(preview.IsSuccess, Is.True);
            Assert.That(preview.Value.HasUnresolvedDecisions, Is.False);
            Assert.That(preview.Value.DefinitionMappings, Has.Count.EqualTo(1));
            Assert.That(preview.Value.ValueChanges, Is.Empty);

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.Revisions.CharacterRevision, Is.EqualTo(revisionBefore), "PreviewCharacterRulesetMigration must never mutate state");
            Assert.That(reRead.Value.RulesetVersion, Is.EqualTo(character.RulesetVersion));

            Result<IReadOnlyList<CharacterHistoryEntry>> history = _characterRepository.GetCharacterHistory(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(history.IsSuccess, Is.True);
            foreach (CharacterHistoryEntry entry in history.Value)
            {
                Assert.That(entry.EventType, Is.Not.EqualTo("odyssey.persistence.character_ruleset_migrated"), "Preview must never write a CharacterRulesetMigrated event");
            }
        }

        [Test]
        public void Preview_UnrecognizedDefinition_SurfacesInUnresolvedDecisions()
        {
            CharacterRecord character = CreateCharacterWithAttribute();

            Result<CharacterRulesetMigrationPlan> preview = _characterRepository.PreviewCharacterRulesetMigration(_campaign, character.CharacterId, "ruleset.core", "2.0.0", IncompatibleCatalog(), TestCorrelationId);

            Assert.That(preview.IsSuccess, Is.True);
            Assert.That(preview.Value.HasUnresolvedDecisions, Is.True);
            Assert.That(preview.Value.UnresolvedDecisions, Has.Count.EqualTo(1));
            Assert.That(preview.Value.UnresolvedDecisions[0].DefinitionId, Is.EqualTo(Strength.ToString()));
            Assert.That(preview.Value.DefinitionMappings, Is.Empty);
        }

        // ---- ApplyCharacterRulesetMigration --------------------------------------

        [Test]
        public void Apply_WithFullyResolvedPlan_Succeeds_PinsTargetRulesetVersion()
        {
            CharacterRecord character = CreateCharacterWithAttribute();
            RulesetDefinitionCatalog catalog = FullyCompatibleCatalog();
            Result<CharacterRulesetMigrationPlan> preview = _characterRepository.PreviewCharacterRulesetMigration(_campaign, character.CharacterId, "ruleset.core", "1.1.0", catalog, TestCorrelationId);
            Assert.That(preview.IsSuccess, Is.True);

            Result<CharacterRecord> applied = _characterRepository.ApplyCharacterRulesetMigration(_campaign, character.CharacterId, preview.Value, catalog, NewUserId(), actorIsMainGm: true, NewCommandId(), TestCorrelationId);

            Assert.That(applied.IsSuccess, Is.True);
            Assert.That(applied.Value.RulesetVersion, Is.EqualTo("1.1.0"));
            Assert.That(applied.Value.Attributes, Has.Count.EqualTo(1), "identity mapping never invents a value transformation -- the attribute itself is unchanged");
        }

        [Test]
        public void Apply_WithUnresolvedDecisions_IsRejected()
        {
            CharacterRecord character = CreateCharacterWithAttribute();
            RulesetDefinitionCatalog catalog = IncompatibleCatalog();
            Result<CharacterRulesetMigrationPlan> preview = _characterRepository.PreviewCharacterRulesetMigration(_campaign, character.CharacterId, "ruleset.core", "2.0.0", catalog, TestCorrelationId);
            Assert.That(preview.IsSuccess, Is.True);
            Assert.That(preview.Value.HasUnresolvedDecisions, Is.True);

            Result<CharacterRecord> applied = _characterRepository.ApplyCharacterRulesetMigration(_campaign, character.CharacterId, preview.Value, catalog, NewUserId(), actorIsMainGm: true, NewCommandId(), TestCorrelationId);

            Assert.That(applied.IsFailure, Is.True);
            Assert.That(applied.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRulesetMigrationHasUnresolvedDecisions));
        }

        [Test]
        public void Apply_ByNonMainGm_IsRejected_NoStateChange()
        {
            CharacterRecord character = CreateCharacterWithAttribute();
            RulesetDefinitionCatalog catalog = FullyCompatibleCatalog();
            Result<CharacterRulesetMigrationPlan> preview = _characterRepository.PreviewCharacterRulesetMigration(_campaign, character.CharacterId, "ruleset.core", "1.1.0", catalog, TestCorrelationId);

            Result<CharacterRecord> applied = _characterRepository.ApplyCharacterRulesetMigration(_campaign, character.CharacterId, preview.Value, catalog, NewUserId(), actorIsMainGm: false, NewCommandId(), TestCorrelationId);

            Assert.That(applied.IsFailure, Is.True);
            Assert.That(applied.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRulesetMigrationDenied));

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.RulesetVersion, Is.EqualTo(character.RulesetVersion));
        }

        [Test]
        public void Apply_WithStalePlan_IsRejected_NoStateChange()
        {
            CharacterRecord character = CreateCharacterWithAttribute();
            RulesetDefinitionCatalog catalog = FullyCompatibleCatalog();
            Result<CharacterRulesetMigrationPlan> preview = _characterRepository.PreviewCharacterRulesetMigration(_campaign, character.CharacterId, "ruleset.core", "1.1.0", catalog, TestCorrelationId);
            Assert.That(preview.IsSuccess, Is.True);

            // Character mutated after preview was built -- the cached plan is now stale.
            Result<CharacterRecord> secondPurchase = _characterRepository.PurchaseAttributeIncrease(_campaign, character.CharacterId, AttributeDefinitionId.Parse("Dexterity"), toValue: 1, NewUserId(), actorIsMainGm: true, character.Revisions.MechanicsRevision, expectedAttributeRevision: 0, NewCommandId(), TestCorrelationId);
            Assert.That(secondPurchase.IsSuccess, Is.True);

            Result<CharacterRecord> applied = _characterRepository.ApplyCharacterRulesetMigration(_campaign, character.CharacterId, preview.Value, catalog, NewUserId(), actorIsMainGm: true, NewCommandId(), TestCorrelationId);

            Assert.That(applied.IsFailure, Is.True);
            Assert.That(applied.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRulesetMigrationStalePlan));

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.RulesetVersion, Is.EqualTo(character.RulesetVersion), "a rejected stale-plan Apply must leave RulesetVersion untouched");
        }

        [Test]
        public void Apply_DuplicateCommandId_DoesNotDuplicateEffect()
        {
            CharacterRecord character = CreateCharacterWithAttribute();
            RulesetDefinitionCatalog catalog = FullyCompatibleCatalog();
            Result<CharacterRulesetMigrationPlan> preview = _characterRepository.PreviewCharacterRulesetMigration(_campaign, character.CharacterId, "ruleset.core", "1.1.0", catalog, TestCorrelationId);
            CommandId commandId = NewCommandId();

            Result<CharacterRecord> first = _characterRepository.ApplyCharacterRulesetMigration(_campaign, character.CharacterId, preview.Value, catalog, NewUserId(), actorIsMainGm: true, commandId, TestCorrelationId);
            Assert.That(first.IsSuccess, Is.True);

            Result<CharacterRecord> replay = _characterRepository.ApplyCharacterRulesetMigration(_campaign, character.CharacterId, preview.Value, catalog, NewUserId(), actorIsMainGm: true, commandId, TestCorrelationId);
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.RulesetVersion, Is.EqualTo("1.1.0"));

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.Revisions.CharacterRevision, Is.EqualTo(character.Revisions.CharacterRevision + 1), "a replayed duplicate CommandId must not apply the migration twice");
        }

        [Test]
        public void ApplyThenRevert_RestoresPriorRulesetVersion_ViaCompensatingEvent()
        {
            CharacterRecord character = CreateCharacterWithAttribute();
            RulesetDefinitionCatalog catalog = FullyCompatibleCatalog();
            Result<CharacterRulesetMigrationPlan> preview = _characterRepository.PreviewCharacterRulesetMigration(_campaign, character.CharacterId, "ruleset.core", "1.1.0", catalog, TestCorrelationId);
            CommandId migrationCommandId = NewCommandId();
            Result<CharacterRecord> applied = _characterRepository.ApplyCharacterRulesetMigration(_campaign, character.CharacterId, preview.Value, catalog, NewUserId(), actorIsMainGm: true, migrationCommandId, TestCorrelationId);
            Assert.That(applied.IsSuccess, Is.True);
            Assert.That(applied.Value.RulesetVersion, Is.EqualTo("1.1.0"));

            Result<CharacterRecord> reverted = _characterRepository.RevertCharacterRulesetMigration(_campaign, character.CharacterId, migrationCommandId, "GM decided to undo", NewUserId(), actorIsMainGm: true, applied.Value.Revisions.CharacterRevision, NewCommandId(), TestCorrelationId);

            Assert.That(reverted.IsSuccess, Is.True);
            Assert.That(reverted.Value.RulesetVersion, Is.EqualTo(character.RulesetVersion));

            Result<IReadOnlyList<CharacterHistoryEntry>> history = _characterRepository.GetCharacterHistory(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(history.IsSuccess, Is.True);
            bool foundReverted = false;
            foreach (CharacterHistoryEntry entry in history.Value)
            {
                if (entry.EventType == "odyssey.persistence.character_ruleset_migration_reverted") foundReverted = true;
            }

            Assert.That(foundReverted, Is.True, "the compensating revert event must be individually visible in history (CAP-INV-005)");
        }

        [Test]
        public void Revert_Twice_SecondCallIsRejected()
        {
            CharacterRecord character = CreateCharacterWithAttribute();
            RulesetDefinitionCatalog catalog = FullyCompatibleCatalog();
            Result<CharacterRulesetMigrationPlan> preview = _characterRepository.PreviewCharacterRulesetMigration(_campaign, character.CharacterId, "ruleset.core", "1.1.0", catalog, TestCorrelationId);
            CommandId migrationCommandId = NewCommandId();
            Result<CharacterRecord> applied = _characterRepository.ApplyCharacterRulesetMigration(_campaign, character.CharacterId, preview.Value, catalog, NewUserId(), actorIsMainGm: true, migrationCommandId, TestCorrelationId);
            Assert.That(applied.IsSuccess, Is.True);

            Result<CharacterRecord> firstRevert = _characterRepository.RevertCharacterRulesetMigration(_campaign, character.CharacterId, migrationCommandId, "undo once", NewUserId(), actorIsMainGm: true, applied.Value.Revisions.CharacterRevision, NewCommandId(), TestCorrelationId);
            Assert.That(firstRevert.IsSuccess, Is.True);

            Result<CharacterRecord> secondRevert = _characterRepository.RevertCharacterRulesetMigration(_campaign, character.CharacterId, migrationCommandId, "undo twice", NewUserId(), actorIsMainGm: true, firstRevert.Value.Revisions.CharacterRevision, NewCommandId(), TestCorrelationId);

            Assert.That(secondRevert.IsFailure, Is.True);
            Assert.That(secondRevert.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRulesetMigrationAlreadyReverted));
        }

        [Test]
        public void Revert_WithoutReasonCode_IsRejected()
        {
            CharacterRecord character = CreateCharacterWithAttribute();
            RulesetDefinitionCatalog catalog = FullyCompatibleCatalog();
            Result<CharacterRulesetMigrationPlan> preview = _characterRepository.PreviewCharacterRulesetMigration(_campaign, character.CharacterId, "ruleset.core", "1.1.0", catalog, TestCorrelationId);
            CommandId migrationCommandId = NewCommandId();
            Result<CharacterRecord> applied = _characterRepository.ApplyCharacterRulesetMigration(_campaign, character.CharacterId, preview.Value, catalog, NewUserId(), actorIsMainGm: true, migrationCommandId, TestCorrelationId);

            Result<CharacterRecord> reverted = _characterRepository.RevertCharacterRulesetMigration(_campaign, character.CharacterId, migrationCommandId, "", NewUserId(), actorIsMainGm: true, applied.Value.Revisions.CharacterRevision, NewCommandId(), TestCorrelationId);

            Assert.That(reverted.IsFailure, Is.True);
            Assert.That(reverted.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRulesetMigrationRevertReasonRequired));
        }

        [Test]
        public void Revert_OnUnknownMigrationCommandId_IsRejected()
        {
            CharacterRecord character = CreateCharacterWithAttribute();

            Result<CharacterRecord> reverted = _characterRepository.RevertCharacterRulesetMigration(_campaign, character.CharacterId, NewCommandId(), "no such migration", NewUserId(), actorIsMainGm: true, character.Revisions.CharacterRevision, NewCommandId(), TestCorrelationId);

            Assert.That(reverted.IsFailure, Is.True);
            Assert.That(reverted.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRulesetMigrationNotFound));
        }

        [Test]
        public void RulesetMigration_NeverRoutesThroughSchemaMigrationRunner()
        {
            // ADR-013 section 9 / this task's own required invariant --
            // confirmed by inspection (no DatabaseSchemaVersion/SchemaHistory
            // reference exists anywhere in this file's own new methods),
            // recorded here as an explicit, permanent regression guard: this
            // test asserts the same real behavior the code review already
            // confirmed by reading -- a successful Apply/Revert never
            // changes the campaign's own DatabaseSchemaVersion.
            CharacterRecord character = CreateCharacterWithAttribute();
            RulesetDefinitionCatalog catalog = FullyCompatibleCatalog();
            Result<CharacterRulesetMigrationPlan> preview = _characterRepository.PreviewCharacterRulesetMigration(_campaign, character.CharacterId, "ruleset.core", "1.1.0", catalog, TestCorrelationId);
            Result<CharacterRecord> applied = _characterRepository.ApplyCharacterRulesetMigration(_campaign, character.CharacterId, preview.Value, catalog, NewUserId(), actorIsMainGm: true, NewCommandId(), TestCorrelationId);
            Assert.That(applied.IsSuccess, Is.True);

            Result<CampaignHandle> reopened = _campaignRepository.Open(_campaignDir, TestCorrelationId);
            Assert.That(reopened.IsSuccess, Is.True);
            Assert.That(reopened.Value.Manifest.RulesetVersion, Is.EqualTo(_campaign.Manifest.RulesetVersion), "a Character-level Ruleset migration must never touch the campaign's own pinned RulesetVersion/DatabaseSchemaVersion");
        }
    }
}
