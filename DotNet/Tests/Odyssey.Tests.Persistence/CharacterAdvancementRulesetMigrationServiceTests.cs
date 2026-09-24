using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Odyssey.Application.CharacterAdvancement;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Identity;
using Odyssey.Persistence.Sqlite;
using Odyssey.Rules.Character;

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S09-104: ruleset migration. The plan (mappings, unresolved decisions) is built by
    /// <see cref="CharacterAdvancementService"/> from a fresh read on every call; <see cref="SqliteCharacterRepository.ApplyCharacterRulesetMigration"/>
    /// receives only flat values plus the state the plan was built from and rejects a plan whose state has moved
    /// (<c>CharacterRulesetMigrationStalePlan</c>) -- replacing the former PreviewHash comparison. Real temp-directory
    /// SQLite campaign; the interception double is <see cref="CharacterAdvancementCostServiceTests.InterceptingProxy"/>.
    /// </summary>
    public sealed class CharacterAdvancementRulesetMigrationServiceTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
        private static readonly AttributeDefinitionId Strength = AttributeDefinitionId.Parse("Strength");
        private static readonly SkillDefinitionId Tactics = SkillDefinitionId.Parse("Tactics");
        private static readonly SkillDefinitionId Stealth = SkillDefinitionId.Parse("Stealth");

        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;
        private SqliteCharacterRepository _repo = null!;
        private UserId _gm;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s09-104-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> created = _campaignRepository.Create(new CreateCampaignRequest(_campaignDir, "Migration Service Test Campaign", "ruleset.core", "1.0.0", "0.1.0"), NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _repo = new SqliteCharacterRepository(Clock);
            _gm = NewUserId();
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaignRepository.Close(_campaign, TestCorrelationId); } catch (IOException) { }
            try { if (Directory.Exists(_campaignDir)) Directory.Delete(_campaignDir, recursive: true); } catch (IOException) { }
        }

        private CharacterRecord Reload(CharacterRecord character)
        {
            Result<CharacterRecord> read = _repo.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(read.IsSuccess, Is.True);
            return read.Value;
        }

        // Strength 2 (attribute) and Tactics 1 (skill) purchased.
        private CharacterRecord CreateCharacter()
        {
            var bind = new BindDraftToCampaignRequest(_campaign, CharacterKind.PlayerCharacter, "Migration Character", "Humanoid", NewUserId(), CharacterCreationSeed.None(), null, null);
            Result<CharacterRecord> bound = _repo.BindDraftToCampaign(bind, NewCommandId(), TestCorrelationId);
            Assert.That(bound.IsSuccess, Is.True);
            Result<CharacterRecord> granted = _repo.GrantDevelopmentPoints(_campaign, bound.Value.CharacterId, 30, "grant", _gm, true, bound.Value.Revisions.MechanicsRevision, NewCommandId(), TestCorrelationId);
            Assert.That(granted.IsSuccess, Is.True);
            Result<CharacterRecord> attr = CharacterAdvancementService.PurchaseAttributeIncrease(_repo, _campaign, bound.Value.CharacterId, Strength, 2, _gm, true, granted.Value.Revisions.MechanicsRevision, 0, NewCommandId(), TestCorrelationId);
            Assert.That(attr.IsSuccess, Is.True);
            Result<CharacterRecord> skill = CharacterAdvancementService.PurchaseSkillLevel(_repo, _campaign, bound.Value.CharacterId, Tactics, 1, _gm, true, attr.Value.Revisions.MechanicsRevision, 0, NewCommandId(), TestCorrelationId);
            Assert.That(skill.IsSuccess, Is.True);
            return skill.Value;
        }

        private void BuyStealth(CharacterRecord character)
        {
            CharacterRecord c = Reload(character);
            Assert.That(CharacterAdvancementService.PurchaseSkillLevel(_repo, _campaign, c.CharacterId, Stealth, 1, _gm, true, c.Revisions.MechanicsRevision, 0, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);
        }

        private static RulesetDefinitionCatalog Catalog(params string[] recognizedIds) => new RulesetDefinitionCatalog(
            recognizedIds.Where(i => i == "Strength").ToArray(), recognizedIds.Where(i => i == "Tactics" || i == "Stealth").ToArray(), Array.Empty<string>(), Array.Empty<string>());

        private string Snapshot(CharacterRecord character)
        {
            CharacterRecord c = Reload(character);
            int history = _repo.GetCharacterHistory(_campaign, c.CharacterId, TestCorrelationId).Value.Count;
            return $"ruleset={c.RulesetVersion}|rev={c.Revisions.CharacterRevision}/{c.Revisions.MechanicsRevision}/{c.Revisions.CharacterAbilitiesRevision}/{c.Revisions.CharacterResourcesRevision}|attrs={c.Attributes.Count}|skills={c.Skills.Count}|history={history}";
        }

        private Result<CharacterRecord> Apply(ICharacterRepository repo, CharacterRecord character, string target, RulesetDefinitionCatalog catalog, bool asMainGm = true, CommandId? commandId = null) =>
            CharacterAdvancementService.ApplyCharacterRulesetMigration(repo, _campaign, character.CharacterId, "ruleset.core", target, catalog, asMainGm ? _gm : NewUserId(), asMainGm, commandId ?? NewCommandId(), TestCorrelationId);

        [Test] // TC-CHAR-210
        public void PreviewCharacterRulesetMigration_ReturnsTheExactPlan_AndIsAPureRead()
        {
            CharacterRecord character = CreateCharacter();
            string before = Snapshot(character);
            var calls = new List<string>();
            ICharacterRepository recording = CharacterAdvancementCostServiceTests.InterceptingProxy.Create(_repo, (method, args) => { calls.Add(method.Name); return (false, null); });

            Result<CharacterRulesetMigrationPlan> preview = CharacterAdvancementService.PreviewCharacterRulesetMigration(recording, _campaign, character.CharacterId, "ruleset.core", "2.0.0", Catalog("Strength"), TestCorrelationId);

            Assert.That(preview.IsSuccess, Is.True);
            CharacterRulesetMigrationPlan plan = preview.Value;
            Assert.That(plan.SourceRulesetId, Is.EqualTo("ruleset.core"));
            Assert.That(plan.SourceRulesetVersion, Is.EqualTo(character.RulesetVersion));
            Assert.That(plan.TargetRulesetId, Is.EqualTo("ruleset.core"));
            Assert.That(plan.TargetRulesetVersion, Is.EqualTo("2.0.0"));
            Assert.That(plan.DefinitionMappings.Select(m => $"{m.Category}:{m.SourceDefinitionId}->{m.TargetDefinitionId}"), Is.EqualTo(new[] { "Attribute:Strength->Strength" }));
            Assert.That(plan.UnresolvedDecisions.Select(d => $"{d.Category}:{d.DefinitionId}"), Is.EqualTo(new[] { "Skill:Tactics" }));
            Assert.That(plan.HasUnresolvedDecisions, Is.True);
            Assert.That(plan.ExpectedMechanicsRevision, Is.EqualTo(character.Revisions.MechanicsRevision));
            Assert.That(plan.ExpectedCharacterAbilitiesRevision, Is.EqualTo(character.Revisions.CharacterAbilitiesRevision));
            Assert.That(plan.ExpectedCharacterResourcesRevision, Is.EqualTo(character.Revisions.CharacterResourcesRevision));
            Assert.That(plan.PreviewHash, Is.Not.Empty);
            Assert.That(calls, Is.EqualTo(new[] { nameof(ICharacterRepository.GetCharacter) }), "a preview only reads the Character");
            Assert.That(Snapshot(character), Is.EqualTo(before));

            Result<CharacterRulesetMigrationPlan> missing = CharacterAdvancementService.PreviewCharacterRulesetMigration(_repo, _campaign, CharacterId.NewId(Clock.GetUtcNow()), "ruleset.core", "2.0.0", Catalog("Strength"), TestCorrelationId);
            Assert.That(missing.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterNotFound));
        }

        [Test] // TC-CHAR-211
        public void ApplyCharacterRulesetMigration_ResolvedPlan_PinsTheTargetVersion_AndChangesNothingElse()
        {
            CharacterRecord character = CreateCharacter();

            Result<CharacterRecord> applied = Apply(_repo, character, "2.0.0", Catalog("Strength", "Tactics"));

            Assert.That(applied.IsSuccess, Is.True);
            Assert.That(applied.Value.RulesetVersion, Is.EqualTo("2.0.0"));
            Assert.That(applied.Value.Revisions.CharacterRevision, Is.EqualTo(character.Revisions.CharacterRevision + 1));
            Assert.That(applied.Value.Revisions.MechanicsRevision, Is.EqualTo(character.Revisions.MechanicsRevision), "no Mechanics column is touched");
            Assert.That(applied.Value.Revisions.CharacterAbilitiesRevision, Is.EqualTo(character.Revisions.CharacterAbilitiesRevision));
            Assert.That(applied.Value.Revisions.CharacterResourcesRevision, Is.EqualTo(character.Revisions.CharacterResourcesRevision));
            Assert.That(applied.Value.Attributes.Single().BaseValue, Is.EqualTo(2));
            Assert.That(applied.Value.Skills.Single().Level, Is.EqualTo(1));
            Assert.That(applied.Value.DevelopmentPool.Spent, Is.EqualTo(character.DevelopmentPool.Spent));
            Assert.That(_repo.GetCharacterHistory(_campaign, character.CharacterId, TestCorrelationId).Value.Count(e => e.EventType == "odyssey.persistence.character_ruleset_migrated"), Is.EqualTo(1));
        }

        [Test] // TC-CHAR-212
        public void ApplyCharacterRulesetMigration_RejectedCases_WriteNothing_AndKeepTheirTypedErrorsAndOrder()
        {
            CharacterRecord character = CreateCharacter();
            string before = Snapshot(character);

            Assert.That(Apply(_repo, character, "2.0.0", Catalog("Strength")).Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRulesetMigrationHasUnresolvedDecisions), "Tactics is not recognized by the target");
            Assert.That(Apply(_repo, character, "2.0.0", Catalog("Strength"), asMainGm: false).Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRulesetMigrationDenied), "denied is reported ahead of unresolved decisions, as before");
            Assert.That(Apply(_repo, character, "2.0.0", Catalog("Strength", "Tactics"), asMainGm: false).Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRulesetMigrationDenied));

            var missing = CharacterId.NewId(Clock.GetUtcNow());
            Assert.That(CharacterAdvancementService.ApplyCharacterRulesetMigration(_repo, _campaign, missing, "ruleset.core", "2.0.0", Catalog("Strength"), NewUserId(), false, NewCommandId(), TestCorrelationId).Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRulesetMigrationDenied));
            Assert.That(CharacterAdvancementService.ApplyCharacterRulesetMigration(_repo, _campaign, missing, "ruleset.core", "2.0.0", Catalog("Strength"), _gm, true, NewCommandId(), TestCorrelationId).Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterNotFound));

            Assert.That(Snapshot(character), Is.EqualTo(before));
        }

        [Test] // TC-CHAR-213
        public void ApplyCharacterRulesetMigration_CompetingWriteBetweenServiceReadAndCommit_IsRejected_NotCommittedAgainstStaleState()
        {
            CharacterRecord character = CreateCharacter();
            RulesetDefinitionCatalog catalog = Catalog("Strength", "Tactics"); // Stealth is NOT recognized by the target
            string afterCompetitor = string.Empty;

            // A competing purchase of a skill the target ruleset does not recognize lands right after the service read the
            // Character. Against the stale read the plan is fully resolved; against the real state it is not. Committing
            // it would migrate a Character that has an unresolved definition.
            ICharacterRepository racing = CharacterAdvancementCostServiceTests.InterceptingProxy.Create(_repo, (method, args) =>
            {
                if (method.Name != nameof(ICharacterRepository.GetCharacter)) return (false, null);
                object? snapshot = method.Invoke(_repo, args);
                BuyStealth(character);
                afterCompetitor = Snapshot(character);
                return (true, snapshot);
            });

            Result<CharacterRecord> applied = Apply(racing, character, "2.0.0", catalog);

            Assert.That(applied.IsFailure, Is.True, "a plan built from a stale read must not be committed");
            Assert.That(applied.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRulesetMigrationStalePlan));
            Assert.That(Snapshot(character), Is.EqualTo(afterCompetitor));
            Assert.That(Reload(character).RulesetVersion, Is.EqualTo(character.RulesetVersion));

            // Retrying recomputes on the actual state and now reports the real problem.
            Assert.That(Apply(_repo, character, "2.0.0", catalog).Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRulesetMigrationHasUnresolvedDecisions));
        }

        [Test] // TC-CHAR-214
        public void ApplyCharacterRulesetMigration_CompetingMigration_ChangesTheSourceVersion_AndIsRejected()
        {
            CharacterRecord character = CreateCharacter();
            RulesetDefinitionCatalog catalog = Catalog("Strength", "Tactics");

            // The three section revisions do not move when a migration commits (it bumps only CharacterRevision), so only
            // the source RulesetVersion the plan was built from can reveal that this plan describes an older state.
            ICharacterRepository racing = CharacterAdvancementCostServiceTests.InterceptingProxy.Create(_repo, (method, args) =>
            {
                if (method.Name != nameof(ICharacterRepository.GetCharacter)) return (false, null);
                object? snapshot = method.Invoke(_repo, args);
                Assert.That(Apply(_repo, character, "1.5.0", catalog).IsSuccess, Is.True);
                return (true, snapshot);
            });

            Result<CharacterRecord> applied = Apply(racing, character, "2.0.0", catalog);

            Assert.That(applied.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRulesetMigrationStalePlan));
            Assert.That(Reload(character).RulesetVersion, Is.EqualTo("1.5.0"), "the competitor's migration stands");
        }

        [Test] // TC-CHAR-215
        public void ApplyCharacterRulesetMigration_NoHashIsNeeded_AndAChangeThatNeverInvalidatedAPlanStillDoesNot()
        {
            CharacterRecord character = CreateCharacter();
            RulesetDefinitionCatalog catalog = Catalog("Strength", "Tactics");
            CharacterRecord read = Reload(character);

            // The repository takes no plan, catalog or hash: the flat values and the state they were built from suffice.
            Result<CharacterRecord> direct = _repo.ApplyCharacterRulesetMigration(_campaign, character.CharacterId, "3.0.0", read.RulesetVersion, read.Revisions.MechanicsRevision, read.Revisions.CharacterAbilitiesRevision, read.Revisions.CharacterResourcesRevision, false, 2, _gm, true, NewCommandId(), TestCorrelationId);
            Assert.That(direct.IsSuccess, Is.True);
            Assert.That(direct.Value.RulesetVersion, Is.EqualTo("3.0.0"));

            // The former PreviewHash covered the source version, the three section revisions and the mappings -- not the
            // identity or CharacterRevision. A rename between read and commit therefore never made a plan stale, and
            // still does not.
            CharacterRecord second = CreateCharacter();
            ICharacterRepository racing = CharacterAdvancementCostServiceTests.InterceptingProxy.Create(_repo, (method, args) =>
            {
                if (method.Name != nameof(ICharacterRepository.GetCharacter)) return (false, null);
                object? snapshot = method.Invoke(_repo, args);
                Assert.That(_repo.UpdateIdentity(_campaign, second.CharacterId, "Renamed", second.Revisions.IdentityRevision, NewCommandId(), TestCorrelationId).IsSuccess, Is.True);
                return (true, snapshot);
            });
            Assert.That(Apply(racing, second, "2.0.0", catalog).IsSuccess, Is.True);
            Assert.That(Reload(second).RulesetVersion, Is.EqualTo("2.0.0"));
        }

        [Test] // TC-CHAR-216
        public void MigrationService_UsesOnlyTheICharacterRepositoryPort_AndAlwaysCallsApply()
        {
            CharacterRecord character = CreateCharacter();
            CharacterRecord c = Reload(character);
            var calls = new List<(string Name, object?[] Args)>();
            ICharacterRepository recording = CharacterAdvancementCostServiceTests.InterceptingProxy.Create(_repo, (method, args) =>
            {
                calls.Add((method.Name, args ?? Array.Empty<object?>()));
                if (method.Name == nameof(ICharacterRepository.ApplyCharacterRulesetMigration)) return (true, Result<CharacterRecord>.Success(c));
                return (false, null);
            });

            Apply(recording, character, "2.0.0", Catalog("Strength"));
            Assert.That(calls.Select(x => x.Name), Is.EqualTo(new[] { nameof(ICharacterRepository.GetCharacter), nameof(ICharacterRepository.ApplyCharacterRulesetMigration) }));
            object?[] a = calls.Last().Args;
            Assert.That(a[2], Is.EqualTo("2.0.0"), "targetRulesetVersion");
            Assert.That(a[3], Is.EqualTo(c.RulesetVersion), "decidedSourceRulesetVersion");
            Assert.That(a[4], Is.EqualTo(c.Revisions.MechanicsRevision));
            Assert.That(a[5], Is.EqualTo(c.Revisions.CharacterAbilitiesRevision));
            Assert.That(a[6], Is.EqualTo(c.Revisions.CharacterResourcesRevision));
            Assert.That(a[7], Is.EqualTo(true), "hasUnresolvedDecisions: Tactics is unrecognized -- decided by the service, and still passed on (nothing short-circuits)");
            Assert.That(a[8], Is.EqualTo(1), "definitionMappingCount: Strength only");

            calls.Clear();
            CharacterAdvancementService.ApplyCharacterRulesetMigration(recording, _campaign, CharacterId.NewId(Clock.GetUtcNow()), "ruleset.core", "2.0.0", Catalog("Strength"), _gm, true, NewCommandId(), TestCorrelationId);
            Assert.That(calls.Last().Name, Is.EqualTo(nameof(ICharacterRepository.ApplyCharacterRulesetMigration)));
            Assert.That(calls.Last().Args[3], Is.EqualTo(string.Empty));
            Assert.That(calls.Last().Args[4], Is.EqualTo(0L), "a failed read still reaches the repository, with a basis no Character can have");

            calls.Clear();
            Assert.Throws<ArgumentException>(new Action(() => CharacterAdvancementService.ApplyCharacterRulesetMigration(recording, _campaign, character.CharacterId, " ", "2.0.0", Catalog("Strength"), _gm, true, NewCommandId(), TestCorrelationId)));
            Assert.Throws<ArgumentNullException>(new Action(() => CharacterAdvancementService.PreviewCharacterRulesetMigration(recording, _campaign, character.CharacterId, "ruleset.core", "2.0.0", null!, TestCorrelationId)));
            Assert.That(calls, Is.Empty);
            Assert.Throws<ArgumentNullException>(new Action(() => CharacterAdvancementService.ApplyCharacterRulesetMigration(null!, _campaign, character.CharacterId, "ruleset.core", "2.0.0", Catalog("Strength"), _gm, true, NewCommandId(), TestCorrelationId)));
            Assert.Throws<ArgumentNullException>(new Action(() => CharacterAdvancementService.PreviewCharacterRulesetMigration(null!, _campaign, character.CharacterId, "ruleset.core", "2.0.0", Catalog("Strength"), TestCorrelationId)));
        }
    }
}
