using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Content;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence.Content
{
    /// <summary>
    /// ODY-S05-106: Minimal Test Catalog Fixtures. Proves `ODY-S05-101`-`105`
    /// work together end-to-end -- GM authoring creates Drafts, typed
    /// definitions round-trip through `TypedDefinitionCodec` as
    /// `PropertiesJson`, cross-definition references stay exact-version
    /// `ContentDefinitionRef`s, `ODY-S05-104` validation gates publish, and
    /// `ODY-S05-103`'s own publish/archive/delete lifecycle stays correct
    /// over the resulting graph. This is a small technical fixture/proof
    /// catalog, deliberately not a final balanced content pack: one Item,
    /// one Weapon, one matching Ammo, one Armor, one Ability, one Effect,
    /// wired together with real cross-references, all scoped to the active
    /// test campaign's own Ruleset (`ruleset.core@1.0.0`).
    ///
    /// The fixture lives as plain C# helper methods in this test class,
    /// not a JSON asset file or a new production factory type -- see this
    /// task's own contract section 18 for why. Every Draft is created
    /// through <see cref="ContentCatalogAuthoringService"/> (never a direct
    /// repository call), matching the real GM-authoring path this fixture
    /// is meant to prove, not a shortcut around it.
    /// </summary>
    public sealed class MinimalTestCatalogFixtureTests
    {
        private const string ActiveRuleset = "ruleset.core@1.0.0";
        private const string AmmoCompatibilityKey = "9mm";

        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;
        private SqliteContentCatalogRepository _catalogRepository = null!;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s05-106-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_campaignDir, "Minimal Test Catalog Fixture Campaign", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = _campaignRepository.Create(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _catalogRepository = new SqliteContentCatalogRepository(Clock);
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaignRepository.Close(_campaign, TestCorrelationId); } catch (IOException) { }
            try { if (Directory.Exists(_campaignDir)) Directory.Delete(_campaignDir, recursive: true); } catch (IOException) { }
        }

        // ---- fixture-authoring helpers (all go through ContentCatalogAuthoringService) ----

        private ContentDefinitionRecord AuthorDraft(ContentDefinitionType type, string name, string propertiesJson, IReadOnlyList<ContentDefinitionRef>? dependencyRefs = null, IReadOnlyList<string>? rulesetCompatibility = null)
        {
            var request = new CreateDraftDefinitionRequest(
                _campaign, type, name, "ODY-S05-106 minimal test catalog fixture.", NewUserId(), actorIsMainGm: true, NewCommandId(), TestCorrelationId,
                rulesetCompatibility: rulesetCompatibility ?? new[] { ActiveRuleset }, propertiesJson: propertiesJson, dependencyRefs: dependencyRefs);
            Result<ContentDefinitionRecord> result = ContentCatalogAuthoringService.CreateDraftDefinition(_catalogRepository, request);
            Assert.That(result.IsSuccess, Is.True, $"fixture authoring of '{name}' must itself succeed");
            return result.Value;
        }

        private ContentDefinitionRecord PublishFixture(ContentDefinitionRecord draft)
        {
            var request = new PublishDefinitionRequest(_campaign, draft.ContentDefinitionId, draft.Revision, NewUserId(), actorIsMainGm: true, NewCommandId(), TestCorrelationId);
            Result<ContentDefinitionRecord> result = ContentCatalogLifecycleService.PublishDefinition(_catalogRepository, request);
            Assert.That(result.IsSuccess, Is.True, $"fixture publish of '{draft.Name}' must itself succeed -- {(result.IsFailure ? result.Error.Code.ToString() : string.Empty)}");
            return result.Value;
        }

        private static string EncodeEffect(string? mechanicsPayloadRef = "burn_snapshot_ref")
        {
            var targetRule = new ContentTargetRule(ContentTargetSource.SourceEntity, 1, 1, false);
            var effect = new EffectDefinition(targetRule, EffectDurationType.ForRounds, durationValue: 2, EffectStackPolicy.RefreshDuration, mechanicsPayloadRef);
            return TypedDefinitionCodec.EncodeEffect(effect);
        }

        private static string EncodeAbility()
        {
            var targetRule = new ContentTargetRule(ContentTargetSource.ActingCharacter, 1, 1, true);
            var ability = new AbilityDefinition(AbilityEntryPointType.ActiveAction, "OnReload", actionCost: 1, Array.Empty<AbilityResourceCost>(), targetRule, "reload_block_ref");
            return TypedDefinitionCodec.EncodeAbility(ability);
        }

        private static string EncodeItem(IReadOnlyList<ContentDefinitionRef>? builtInEffectRefs = null)
        {
            var item = new ItemDefinition(ItemCategory.Consumable, isStackable: true, maxStackSize: 10, weight: 1, hasDurability: false, maxDurability: null, hasCharges: false, maxCharges: null,
                Array.Empty<ContentDefinitionRef>(), builtInEffectRefs ?? Array.Empty<ContentDefinitionRef>());
            return TypedDefinitionCodec.EncodeItem(item);
        }

        private static string EncodeWeapon(AmmoRequirement ammoRequirement, IReadOnlyList<string> compatibleAmmoKeys)
        {
            var item = new ItemDefinition(ItemCategory.Generic, false, null, weight: 3, false, null, false, null, Array.Empty<ContentDefinitionRef>(), Array.Empty<ContentDefinitionRef>());
            var weapon = new WeaponDefinition(item, "1d10", range: 20, WeaponAttackMode.Ranged, actionCost: 1, ammoRequirement, compatibleAmmoKeys);
            return TypedDefinitionCodec.EncodeWeapon(weapon);
        }

        private static string EncodeAmmo(IReadOnlyList<string>? compatibilityKeys = null)
        {
            var item = new ItemDefinition(ItemCategory.Generic, true, 50, weight: 1, false, null, false, null, Array.Empty<ContentDefinitionRef>(), Array.Empty<ContentDefinitionRef>());
            var ammo = new AmmoDefinition(item, compatibilityKeys ?? new[] { AmmoCompatibilityKey }, damageContribution: null, Array.Empty<ContentDefinitionRef>());
            return TypedDefinitionCodec.EncodeAmmo(ammo);
        }

        private static string EncodeArmor()
        {
            var item = new ItemDefinition(ItemCategory.Generic, false, null, weight: 4, true, maxDurability: 20, false, null, Array.Empty<ContentDefinitionRef>(), Array.Empty<ContentDefinitionRef>());
            var armor = new ArmorDefinition(item, "chest_slot", new[] { BodyPartId.Parse("Torso") }, protection: 2);
            return TypedDefinitionCodec.EncodeArmor(armor);
        }

        private CatalogValidationResult Validate(ContentDefinitionId id)
        {
            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, new ValidateContentDefinitionRequest(_campaign, id, TestCorrelationId));
            Assert.That(result.IsSuccess, Is.True);
            return result.Value;
        }

        // ---- 1. Full graph publishes end-to-end through Authoring + Validation + Lifecycle ----

        [Test]
        public void MinimalFixtureGraph_PublishesEndToEnd_ThroughAuthoringValidationAndLifecycle()
        {
            ContentDefinitionRecord effectDraft = AuthorDraft(ContentDefinitionType.Effect, "Bleeding", EncodeEffect());
            ContentDefinitionRecord effectPublished = PublishFixture(effectDraft);
            var effectRef = new ContentDefinitionRef(effectPublished.ContentDefinitionId, effectPublished.Version);

            ContentDefinitionRecord abilityDraft = AuthorDraft(ContentDefinitionType.Ability, "Field Dressing", EncodeAbility(), dependencyRefs: new[] { effectRef });
            ContentDefinitionRecord abilityPublished = PublishFixture(abilityDraft);

            ContentDefinitionRecord itemDraft = AuthorDraft(ContentDefinitionType.Item, "Medkit", EncodeItem(builtInEffectRefs: new[] { effectRef }));
            ContentDefinitionRecord itemPublished = PublishFixture(itemDraft);

            ContentDefinitionRecord ammoDraft = AuthorDraft(ContentDefinitionType.Ammo, "9mm Rounds", EncodeAmmo());
            ContentDefinitionRecord ammoPublished = PublishFixture(ammoDraft);

            ContentDefinitionRecord weaponDraft = AuthorDraft(ContentDefinitionType.Weapon, "Service Pistol", EncodeWeapon(AmmoRequirement.Required, new[] { AmmoCompatibilityKey }));
            ContentDefinitionRecord weaponPublished = PublishFixture(weaponDraft);

            ContentDefinitionRecord armorDraft = AuthorDraft(ContentDefinitionType.Armor, "Light Vest", EncodeArmor());
            ContentDefinitionRecord armorPublished = PublishFixture(armorDraft);

            foreach (ContentDefinitionRecord published in new[] { effectPublished, abilityPublished, itemPublished, ammoPublished, weaponPublished, armorPublished })
            {
                Assert.That(published.Status, Is.EqualTo(ContentDefinitionStatus.Published), $"{published.Name} must have published successfully");
                Assert.That(published.Version, Is.EqualTo(1));
            }
        }

        // ---- 2/3. Weapon-ammo applicability ----

        [Test]
        public void WeaponFixture_RequiringAmmo_PassesValidation_WhenMatchingCompatibleAmmoExists()
        {
            AuthorDraft(ContentDefinitionType.Ammo, "9mm Rounds", EncodeAmmo());
            ContentDefinitionRecord weaponDraft = AuthorDraft(ContentDefinitionType.Weapon, "Service Pistol", EncodeWeapon(AmmoRequirement.Required, new[] { AmmoCompatibilityKey }));

            CatalogValidationResult validation = Validate(weaponDraft.ContentDefinitionId);

            Assert.That(validation.IsValid, Is.True, string.Join(", ", validation.Issues.Select(i => i.IssueCode)));
        }

        [Test]
        public void WeaponFixture_RequiringAmmo_FailsValidation_WhenMatchingAmmoIsMissing()
        {
            ContentDefinitionRecord weaponDraft = AuthorDraft(ContentDefinitionType.Weapon, "Service Pistol (No Ammo Yet)", EncodeWeapon(AmmoRequirement.Required, new[] { AmmoCompatibilityKey }));

            CatalogValidationResult validation = Validate(weaponDraft.ContentDefinitionId);

            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Issues.Any(i => i.IssueCode == CatalogValidationIssueCode.WeaponNoCompatibleAmmoInCatalog), Is.True);
        }

        [Test]
        public void WeaponFixture_RequiringAmmo_FailsValidation_WhenOnlyMatchingAmmoIsRulesetIncompatible()
        {
            AuthorDraft(ContentDefinitionType.Ammo, "Foreign 9mm Rounds", EncodeAmmo(), rulesetCompatibility: new[] { "other.ruleset@9.9.9" });
            ContentDefinitionRecord weaponDraft = AuthorDraft(ContentDefinitionType.Weapon, "Service Pistol", EncodeWeapon(AmmoRequirement.Required, new[] { AmmoCompatibilityKey }));

            CatalogValidationResult validation = Validate(weaponDraft.ContentDefinitionId);

            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Issues.Any(i => i.IssueCode == CatalogValidationIssueCode.WeaponNoCompatibleAmmoInCatalog), Is.True);
        }

        // ---- 4. Exact-version references ----

        [Test]
        public void TypedReferencesInFixture_RemainExactVersionRefs()
        {
            ContentDefinitionRecord effectDraft = AuthorDraft(ContentDefinitionType.Effect, "Bleeding", EncodeEffect());
            ContentDefinitionRecord effectPublished = PublishFixture(effectDraft);
            var effectRef = new ContentDefinitionRef(effectPublished.ContentDefinitionId, effectPublished.Version);

            ContentDefinitionRecord itemDraft = AuthorDraft(ContentDefinitionType.Item, "Medkit", EncodeItem(builtInEffectRefs: new[] { effectRef }));

            Result<ItemDefinition> decoded = TypedDefinitionCodec.DecodeItem(itemDraft.DefinitionType, itemDraft.PropertiesJson, TestCorrelationId);
            Assert.That(decoded.IsSuccess, Is.True);
            ContentDefinitionRef roundTripped = decoded.Value.BuiltInEffectRefs.Single();
            Assert.That(roundTripped.DefinitionId, Is.EqualTo(effectPublished.ContentDefinitionId));
            Assert.That(roundTripped.Version, Is.EqualTo(1));
        }

        // ---- 5. Published fixtures remain loadable ----

        [Test]
        public void PublishedFixtureDefinitions_RemainLoadable()
        {
            ContentDefinitionRecord effectPublished = PublishFixture(AuthorDraft(ContentDefinitionType.Effect, "Bleeding", EncodeEffect()));
            ContentDefinitionRecord weaponPublished = PublishFixture(AuthorDraft(ContentDefinitionType.Weapon, "Service Pistol", EncodeWeapon(AmmoRequirement.None, Array.Empty<string>())));

            foreach (ContentDefinitionId id in new[] { effectPublished.ContentDefinitionId, weaponPublished.ContentDefinitionId })
            {
                Result<ContentDefinitionRecord> reread = _catalogRepository.GetContentDefinition(_campaign, id, TestCorrelationId);
                Assert.That(reread.IsSuccess, Is.True);
                Assert.That(reread.Value.Status, Is.EqualTo(ContentDefinitionStatus.Published));
            }
        }

        // ---- 6. Archived fixture appears in the separate Archived list ----

        [Test]
        public void ArchivedFixtureDefinition_AppearsInArchivedList()
        {
            ContentDefinitionRecord stillPublished = PublishFixture(AuthorDraft(ContentDefinitionType.Effect, "Bleeding", EncodeEffect()));
            ContentDefinitionRecord toArchive = PublishFixture(AuthorDraft(ContentDefinitionType.Armor, "Light Vest", EncodeArmor()));

            var archiveRequest = new ArchiveDefinitionRequest(_campaign, toArchive.ContentDefinitionId, "retired fixture armor", actorIsMainGm: true, NewCommandId(), TestCorrelationId);
            Result<ContentDefinitionRecord> archived = ContentCatalogLifecycleService.ArchiveDefinition(_catalogRepository, archiveRequest);
            Assert.That(archived.IsSuccess, Is.True);

            Result<IReadOnlyList<ContentDefinitionRecord>> archivedList = ContentCatalogLifecycleService.ListArchivedDefinitions(_catalogRepository, new ListArchivedDefinitionsRequest(_campaign, actorIsMainGm: true, TestCorrelationId));

            Assert.That(archivedList.IsSuccess, Is.True);
            Assert.That(archivedList.Value.Select(r => r.ContentDefinitionId), Does.Contain(toArchive.ContentDefinitionId));
            Assert.That(archivedList.Value.Select(r => r.ContentDefinitionId), Does.Not.Contain(stillPublished.ContentDefinitionId));
        }

        // ---- 7/8. Physical delete rules over fixture definitions ----

        [Test]
        public void UnusedDraftFixture_CanBePhysicallyDeleted()
        {
            ContentDefinitionRecord unusedDraft = AuthorDraft(ContentDefinitionType.Ability, "Unused Draft Ability", EncodeAbility());

            var deleteRequest = new DeleteDraftDefinitionRequest(_campaign, unusedDraft.ContentDefinitionId, actorIsMainGm: true, NewCommandId(), TestCorrelationId);
            Result deleted = ContentCatalogLifecycleService.DeleteDraftDefinition(_catalogRepository, deleteRequest);

            Assert.That(deleted.IsSuccess, Is.True);
            Result<ContentDefinitionRecord> reread = _catalogRepository.GetContentDefinition(_campaign, unusedDraft.ContentDefinitionId, TestCorrelationId);
            Assert.That(reread.IsFailure, Is.True);
        }

        [Test]
        public void PublishedAndArchivedFixtureDefinitions_CannotBePhysicallyDeleted()
        {
            ContentDefinitionRecord published = PublishFixture(AuthorDraft(ContentDefinitionType.Weapon, "Service Pistol", EncodeWeapon(AmmoRequirement.None, Array.Empty<string>())));
            ContentDefinitionRecord toArchive = PublishFixture(AuthorDraft(ContentDefinitionType.Armor, "Light Vest", EncodeArmor()));
            ContentCatalogLifecycleService.ArchiveDefinition(_catalogRepository, new ArchiveDefinitionRequest(_campaign, toArchive.ContentDefinitionId, "retired", actorIsMainGm: true, NewCommandId(), TestCorrelationId));

            Result deletePublished = ContentCatalogLifecycleService.DeleteDraftDefinition(_catalogRepository, new DeleteDraftDefinitionRequest(_campaign, published.ContentDefinitionId, actorIsMainGm: true, NewCommandId(), TestCorrelationId));
            Result deleteArchived = ContentCatalogLifecycleService.DeleteDraftDefinition(_catalogRepository, new DeleteDraftDefinitionRequest(_campaign, toArchive.ContentDefinitionId, actorIsMainGm: true, NewCommandId(), TestCorrelationId));

            Assert.That(deletePublished.IsFailure, Is.True);
            Assert.That(deletePublished.Error.Code, Is.EqualTo(ErrorCodes.PersistenceContentDefinitionNotDraft));
            Assert.That(deleteArchived.IsFailure, Is.True);
            Assert.That(deleteArchived.Error.Code, Is.EqualTo(ErrorCodes.PersistenceContentDefinitionNotDraft));

            Assert.That(_catalogRepository.GetContentDefinition(_campaign, published.ContentDefinitionId, TestCorrelationId).IsSuccess, Is.True);
            Assert.That(_catalogRepository.GetContentDefinition(_campaign, toArchive.ContentDefinitionId, TestCorrelationId).IsSuccess, Is.True);
        }

        // ---- 9. Broken fixture graph fails safely ----

        [Test]
        public void BrokenFixtureGraph_MissingReferencedEffect_FailsValidationAndPublishSafely()
        {
            var missingEffectRef = new ContentDefinitionRef(ContentDefinitionId.NewId(UtcInstant.Parse("2026-09-04T00:00:00.0000000Z")), 1);
            ContentDefinitionRecord brokenItem = AuthorDraft(ContentDefinitionType.Item, "Broken Medkit", EncodeItem(builtInEffectRefs: new[] { missingEffectRef }));

            CatalogValidationResult validation = Validate(brokenItem.ContentDefinitionId);
            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Issues.Any(i => i.IssueCode == CatalogValidationIssueCode.ReferenceMissing), Is.True);

            var publishRequest = new PublishDefinitionRequest(_campaign, brokenItem.ContentDefinitionId, brokenItem.Revision, NewUserId(), actorIsMainGm: true, NewCommandId(), TestCorrelationId);
            Result<ContentDefinitionRecord> publishResult = ContentCatalogLifecycleService.PublishDefinition(_catalogRepository, publishRequest);

            Assert.That(publishResult.IsFailure, Is.True);
            Assert.That(publishResult.Error.Code, Is.EqualTo(ErrorCodes.ContentCatalogPublishValidationFailed));
            Result<ContentDefinitionRecord> reread = _catalogRepository.GetContentDefinition(_campaign, brokenItem.ContentDefinitionId, TestCorrelationId);
            Assert.That(reread.Value.Status, Is.EqualTo(ContentDefinitionStatus.Draft), "a broken fixture must never actually publish");
        }

        // ---- 10. No runtime item/inventory/equipment/effect implementation ----

        [Test]
        public void FixtureTests_IntroduceNoRuntimeItemInventoryEquipmentOrActiveEffectType()
        {
            System.Reflection.Assembly applicationAssembly = typeof(ContentCatalogLifecycleService).Assembly;
            var typesToCheck = applicationAssembly.GetTypes().Where(t => t.Namespace == "Odyssey.Application.Content").ToArray();

            string[] forbiddenSubstrings = { "Inventory", "ItemInstance", "ItemStack", "Equipment", "ActiveEffect" };
            foreach (System.Type type in typesToCheck)
            {
                foreach (string forbidden in forbiddenSubstrings)
                {
                    Assert.That(type.Name, Does.Not.Contain(forbidden), $"type '{type.Name}' must not reference runtime item/inventory/equipment/effect state ('{forbidden}')");
                }
            }
        }

        [Test]
        public void FixtureTests_IntroduceNoRuntimePersistenceTable()
        {
            AuthorDraft(ContentDefinitionType.Effect, "Bleeding", EncodeEffect()); // ensures catalog tables exist

            using var connection = new SqliteConnection("Data Source=" + Path.Combine(_campaignDir, "campaign.db"));
            connection.Open();
            using var select = connection.CreateCommand();
            select.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
            using SqliteDataReader reader = select.ExecuteReader();
            var tableNames = new List<string>();
            while (reader.Read()) tableNames.Add(reader.GetString(0));

            string[] forbiddenTableNames = { "Inventory", "ItemInstance", "ItemStack", "Equipment", "ActiveEffect" };
            foreach (string forbidden in forbiddenTableNames)
            {
                Assert.That(tableNames, Has.None.Contain(forbidden), $"no table containing '{forbidden}' may exist -- ODY-S05-106 is a fixture/proof task, introducing no runtime persistence table");
            }
        }
    }
}
