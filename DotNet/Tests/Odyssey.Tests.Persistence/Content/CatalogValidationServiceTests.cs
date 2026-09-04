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
    /// ODY-S05-104: real, SQLite-backed tests for
    /// <see cref="CatalogValidationService"/> against the real
    /// <see cref="SqliteContentCatalogRepository"/>/<see cref="SqliteCampaignRepository"/>
    /// built by `ODY-S05-101`/`ODY-S05-102`, decoding through `ODY-S05-105`'s
    /// own <see cref="TypedDefinitionCodec"/>. Catalog Validation MVP only:
    /// this service never publishes/archives/deletes anything and never
    /// mutates a catalog row (proven directly below); it does not execute
    /// attacks, abilities, effects, or `ContentBlock` graphs.
    /// </summary>
    public sealed class CatalogValidationServiceTests
    {
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
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s05-104-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_campaignDir, "Catalog Validation Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
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

        // ---- fixture helpers ------------------------------------------------------

        private ContentDefinitionRecord CreateDraft(ContentDefinitionType type, string propertiesJson, IReadOnlyList<string>? rulesetCompatibility = null, IReadOnlyList<ContentDefinitionRef>? dependencyRefs = null, string name = "Test Definition")
        {
            var request = new CreateDraftContentDefinitionRequest(_campaign, type, name, "A validation test fixture.", NewUserId(), rulesetCompatibility, propertiesJson: propertiesJson, dependencyRefs: dependencyRefs);
            Result<ContentDefinitionRecord> created = _catalogRepository.CreateDraftContentDefinition(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True, "fixture setup must itself succeed");
            return created.Value;
        }

        /// <summary>
        /// Test-only direct SQL write, mirroring `ODY-S05-101`/`102`'s own
        /// established `MarkStatusDirectly` convention -- no `PublishDefinition`
        /// command exists yet (`ODY-S05-103`'s own future job), so a Published
        /// row with a specific exact <paramref name="version"/>, and
        /// optionally a specific `PropertiesJson`/`DependencyRefsJson`, can
        /// only be constructed directly at the SQL level in tests.
        /// </summary>
        private void MarkPublishedDirectly(ContentDefinitionId definitionId, long version, string? propertiesJson = null, IReadOnlyList<ContentDefinitionRef>? dependencyRefs = null)
        {
            using var connection = new SqliteConnection("Data Source=" + Path.Combine(_campaignDir, "campaign.db"));
            connection.Open();
            var setClauses = new List<string> { "Status = $status", "Version = $version" };
            using var update = connection.CreateCommand();
            update.Parameters.AddWithValue("$status", ContentDefinitionStatus.Published.ToString());
            update.Parameters.AddWithValue("$version", version);
            if (propertiesJson != null)
            {
                setClauses.Add("PropertiesJson = $props");
                update.Parameters.AddWithValue("$props", propertiesJson);
            }

            if (dependencyRefs != null)
            {
                setClauses.Add("DependencyRefsJson = $deps");
                update.Parameters.AddWithValue("$deps", SerializeRefs(dependencyRefs));
            }

            update.Parameters.AddWithValue("$id", definitionId.ToString());
            update.CommandText = "UPDATE ContentDefinition SET " + string.Join(", ", setClauses) + " WHERE ContentDefinitionId = $id;";
            update.ExecuteNonQuery();
        }

        private static string SerializeRefs(IReadOnlyList<ContentDefinitionRef> refs)
            => "[" + string.Join(",", refs.Select(r => "\"" + r + "\"")) + "]";

        private static ContentDefinitionRef RandomRef(long version = 1) => new ContentDefinitionRef(ContentDefinitionId.NewId(UtcInstant.Parse("2026-09-04T00:00:00.0000000Z")), version);

        private static string EncodeMinimalItem(IReadOnlyList<ContentDefinitionRef>? builtInAbilityRefs = null, IReadOnlyList<ContentDefinitionRef>? builtInEffectRefs = null)
        {
            var item = new ItemDefinition(ItemCategory.Generic, false, null, 1, false, null, false, null,
                builtInAbilityRefs ?? Array.Empty<ContentDefinitionRef>(), builtInEffectRefs ?? Array.Empty<ContentDefinitionRef>());
            return TypedDefinitionCodec.EncodeItem(item);
        }

        private static string EncodeMinimalWeapon(AmmoRequirement ammoRequirement = AmmoRequirement.None, IReadOnlyList<string>? compatibleAmmoKeys = null)
        {
            var item = new ItemDefinition(ItemCategory.Generic, false, null, 1, false, null, false, null, Array.Empty<ContentDefinitionRef>(), Array.Empty<ContentDefinitionRef>());
            var weapon = new WeaponDefinition(item, "1d6", 5, WeaponAttackMode.Melee, 1, ammoRequirement, compatibleAmmoKeys ?? Array.Empty<string>());
            return TypedDefinitionCodec.EncodeWeapon(weapon);
        }

        private static string EncodeMinimalArmor()
        {
            var item = new ItemDefinition(ItemCategory.Generic, false, null, 1, false, null, false, null, Array.Empty<ContentDefinitionRef>(), Array.Empty<ContentDefinitionRef>());
            var armor = new ArmorDefinition(item, "chest_slot", new[] { BodyPartId.Parse("Torso") }, 3);
            return TypedDefinitionCodec.EncodeArmor(armor);
        }

        private static string EncodeMinimalAmmo(IReadOnlyList<string>? compatibilityKeys = null, IReadOnlyList<ContentDefinitionRef>? effectContributionRefs = null)
        {
            var item = new ItemDefinition(ItemCategory.Generic, true, 20, 1, false, null, false, null, Array.Empty<ContentDefinitionRef>(), Array.Empty<ContentDefinitionRef>());
            var ammo = new AmmoDefinition(item, compatibilityKeys ?? new[] { "9mm" }, null, effectContributionRefs ?? Array.Empty<ContentDefinitionRef>());
            return TypedDefinitionCodec.EncodeAmmo(ammo);
        }

        private static string EncodeMinimalAbility()
        {
            var targetRule = new ContentTargetRule(ContentTargetSource.ActingCharacter, 1, 1, true);
            var ability = new AbilityDefinition(AbilityEntryPointType.ActiveAction, "OnCommand", 1, Array.Empty<AbilityResourceCost>(), targetRule, null);
            return TypedDefinitionCodec.EncodeAbility(ability);
        }

        private static string EncodeMinimalEffect(string? mechanicsPayloadRef = null)
        {
            var targetRule = new ContentTargetRule(ContentTargetSource.SourceEntity, 1, 1, false);
            var effect = new EffectDefinition(targetRule, EffectDurationType.Instant, null, EffectStackPolicy.IndependentInstances, mechanicsPayloadRef);
            return TypedDefinitionCodec.EncodeEffect(effect);
        }

        private ValidateContentDefinitionRequest RequestFor(ContentDefinitionId id) => new ValidateContentDefinitionRequest(_campaign, id, TestCorrelationId);

        // ---- 1. Valid definitions of every typed shape pass validation ------------

        [Test]
        public void ValidItemDefinition_PassesValidation()
        {
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Item, EncodeMinimalItem());

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.True, string.Join(", ", result.Value.Issues.Select(i => i.IssueCode)));
        }

        [Test]
        public void ValidWeaponDefinition_PassesValidation()
        {
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Weapon, EncodeMinimalWeapon());

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.True, string.Join(", ", result.Value.Issues.Select(i => i.IssueCode)));
        }

        [Test]
        public void ValidArmorDefinition_PassesValidation()
        {
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Armor, EncodeMinimalArmor());

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.True, string.Join(", ", result.Value.Issues.Select(i => i.IssueCode)));
        }

        [Test]
        public void ValidAmmoDefinition_PassesValidation()
        {
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Ammo, EncodeMinimalAmmo());

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.True, string.Join(", ", result.Value.Issues.Select(i => i.IssueCode)));
        }

        [Test]
        public void ValidAbilityDefinition_PassesValidation()
        {
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Ability, EncodeMinimalAbility());

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.True, string.Join(", ", result.Value.Issues.Select(i => i.IssueCode)));
        }

        [Test]
        public void ValidEffectDefinition_PassesValidation()
        {
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Effect, EncodeMinimalEffect());

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.True, string.Join(", ", result.Value.Issues.Select(i => i.IssueCode)));
        }

        // ---- 2. Wrong ContentDefinitionType / malformed typed JSON ----------------

        [Test]
        public void MalformedTypedJson_ReturnsTypedPayloadMalformedIssue()
        {
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Weapon, "{ this is not valid json");

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.False);
            Assert.That(result.Value.Issues.Any(i => i.IssueCode == CatalogValidationIssueCode.TypedPayloadMalformed), Is.True);
        }

        // ---- 3. Weapon usability -----------------------------------------------

        [Test]
        public void WeaponMissingDamageExpression_MalformedPayloadInStorage_FailsValidation()
        {
            // A Weapon-typed record whose PropertiesJson never went through
            // WeaponDefinition's own constructor (which would have rejected
            // an empty damage expression at construction time) -- the same
            // "storage got out of sync with the domain shape" scenario the
            // codec's own decode-time safety net exists for.
            string malformedWeaponJson = "{\"schemaVersion\":1,\"category\":\"Generic\",\"isStackable\":false,\"weight\":1,\"hasDurability\":false,\"hasCharges\":false,\"builtInAbilityRefs\":[],\"builtInEffectRefs\":[],\"damageExpression\":\"\",\"range\":5,\"attackMode\":\"Melee\",\"actionCost\":1,\"ammoRequirement\":\"None\",\"compatibleAmmoKeys\":[]}";
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Weapon, malformedWeaponJson);

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.False);
            Assert.That(result.Value.Issues.Any(i => i.IssueCode == CatalogValidationIssueCode.TypedPayloadMalformed), Is.True);
        }

        [Test]
        public void WeaponRequiringAmmo_WithNoCompatibleAmmoKeys_FailsValidation()
        {
            string weaponJson = EncodeMinimalWeapon(AmmoRequirement.Required, Array.Empty<string>());
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Weapon, weaponJson);

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.False);
            Assert.That(result.Value.Issues.Any(i => i.IssueCode == CatalogValidationIssueCode.WeaponAmmoCompatibilityKeysRequired), Is.True);
        }

        // ---- 4. Weapon requiring ammo with no compatible AmmoDefinition -----------

        [Test]
        public void WeaponRequiringAmmo_WithNoMatchingAmmoDefinitionInCatalog_FailsValidation()
        {
            string weaponJson = EncodeMinimalWeapon(AmmoRequirement.Required, new[] { "9mm" });
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Weapon, weaponJson);

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.False);
            Assert.That(result.Value.Issues.Any(i => i.IssueCode == CatalogValidationIssueCode.WeaponNoCompatibleAmmoInCatalog), Is.True);
        }

        [Test]
        public void WeaponRequiringAmmo_WithMatchingAmmoDefinitionInCatalog_PassesValidation()
        {
            // Control case: matching ammo declares no RulesetCompatibility
            // restriction at all (empty list -- unrestricted).
            CreateDraft(ContentDefinitionType.Ammo, EncodeMinimalAmmo(new[] { "9mm" }), rulesetCompatibility: Array.Empty<string>());
            string weaponJson = EncodeMinimalWeapon(AmmoRequirement.Required, new[] { "9mm" });
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Weapon, weaponJson);

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.True, string.Join(", ", result.Value.Issues.Select(i => i.IssueCode)));
        }

        [Test]
        public void WeaponRequiringAmmo_WithMatchingAmmoDefinitionCompatibleWithActiveRuleset_PassesValidation()
        {
            // Control case: matching ammo explicitly declares the active
            // campaign ruleset as compatible.
            CreateDraft(ContentDefinitionType.Ammo, EncodeMinimalAmmo(new[] { "9mm" }), rulesetCompatibility: new[] { "ruleset.core@1.0.0" });
            string weaponJson = EncodeMinimalWeapon(AmmoRequirement.Required, new[] { "9mm" });
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Weapon, weaponJson);

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.True, string.Join(", ", result.Value.Issues.Select(i => i.IssueCode)));
        }

        [Test]
        public void WeaponRequiringAmmo_WithKeyMatchingAmmoFromIncompatibleRuleset_FailsValidation()
        {
            // ODY-S05-104 amendment: a compatibility-key match alone is not
            // enough -- an AmmoDefinition scoped to a different Ruleset must
            // never satisfy a weapon's ammo requirement in the active
            // campaign, even though the plain string key happens to match.
            CreateDraft(ContentDefinitionType.Ammo, EncodeMinimalAmmo(new[] { "9mm" }), rulesetCompatibility: new[] { "other.ruleset@9.9.9" });
            string weaponJson = EncodeMinimalWeapon(AmmoRequirement.Required, new[] { "9mm" });
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Weapon, weaponJson);

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.False);
            Assert.That(result.Value.Issues.Any(i => i.IssueCode == CatalogValidationIssueCode.WeaponNoCompatibleAmmoInCatalog), Is.True);
        }

        // ---- 5. Armor usability -----------------------------------------------

        [Test]
        public void ArmorMissingRequiredFields_FailsValidation()
        {
            string malformedArmorJson = "{\"schemaVersion\":1,\"category\":\"Generic\",\"isStackable\":false,\"weight\":1,\"hasDurability\":false,\"hasCharges\":false,\"builtInAbilityRefs\":[],\"builtInEffectRefs\":[]}";
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Armor, malformedArmorJson);

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.False);
            Assert.That(result.Value.Issues.Any(i => i.IssueCode == CatalogValidationIssueCode.TypedPayloadMalformed), Is.True);
        }

        // ---- 6. Ammo usability -----------------------------------------------

        [Test]
        public void AmmoMissingCompatibilityKey_FailsValidation()
        {
            string malformedAmmoJson = "{\"schemaVersion\":1,\"category\":\"Generic\",\"isStackable\":true,\"maxStackSize\":20,\"weight\":1,\"hasDurability\":false,\"hasCharges\":false,\"builtInAbilityRefs\":[],\"builtInEffectRefs\":[],\"compatibilityKeys\":[],\"damageContribution\":null,\"effectContributionRefs\":[]}";
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Ammo, malformedAmmoJson);

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.False);
            Assert.That(result.Value.Issues.Any(i => i.IssueCode == CatalogValidationIssueCode.TypedPayloadMalformed), Is.True);
        }

        // ---- 7 & 10. Missing exact ContentDefinitionRef fails validation ----------

        [Test]
        public void AmmoEffectContributionRef_ToMissingEffectDefinition_FailsValidation()
        {
            ContentDefinitionRef missingEffectRef = RandomRef();
            string ammoJson = EncodeMinimalAmmo(effectContributionRefs: new[] { missingEffectRef });
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Ammo, ammoJson);

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.False);
            Assert.That(result.Value.Issues.Any(i => i.IssueCode == CatalogValidationIssueCode.ReferenceMissing && i.FieldPath == "properties.effectContributionRefs[0]"), Is.True);
        }

        [Test]
        public void ItemBuiltInAbilityRef_ToDefinitionOfWrongType_FailsValidation()
        {
            // Publish a real Effect (not an Ability) at version 1, then have
            // an Item's BuiltInAbilityRefs point at it -- exists, exact
            // version matches, but the target's own DefinitionType is wrong.
            ContentDefinitionRecord effectRecord = CreateDraft(ContentDefinitionType.Effect, EncodeMinimalEffect());
            MarkPublishedDirectly(effectRecord.ContentDefinitionId, 1);
            var wrongTypeRef = new ContentDefinitionRef(effectRecord.ContentDefinitionId, 1);

            string itemJson = EncodeMinimalItem(builtInAbilityRefs: new[] { wrongTypeRef });
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Item, itemJson);

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.False);
            Assert.That(result.Value.Issues.Any(i => i.IssueCode == CatalogValidationIssueCode.ReferenceWrongType), Is.True);
        }

        // ---- 8. Ability usability -----------------------------------------------

        [Test]
        public void AbilityMissingTrigger_FailsValidation()
        {
            string malformedAbilityJson = "{\"schemaVersion\":1,\"entryPointType\":\"ActiveAction\",\"trigger\":\"\",\"actionCost\":1,\"targetRule\":{\"targetSource\":\"ActingCharacter\",\"minimumCount\":1,\"maximumCount\":1,\"allowSelf\":true},\"mechanicsPayloadRef\":null,\"resourceCosts\":[]}";
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Ability, malformedAbilityJson);

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.False);
            Assert.That(result.Value.Issues.Any(i => i.IssueCode == CatalogValidationIssueCode.TypedPayloadMalformed), Is.True);
        }

        // ---- 9. Effect usability + ContentBlock/mechanics payload boundary --------

        [Test]
        public void EffectWithInvalidDurationEnum_FailsValidation()
        {
            string malformedEffectJson = "{\"schemaVersion\":1,\"targetRule\":{\"targetSource\":\"SourceEntity\",\"minimumCount\":1,\"maximumCount\":1,\"allowSelf\":false},\"durationType\":\"NotARealDuration\",\"durationValue\":null,\"stackPolicy\":\"IndependentInstances\",\"mechanicsPayloadRef\":null}";
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Effect, malformedEffectJson);

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.False);
            Assert.That(result.Value.Issues.Any(i => i.IssueCode == CatalogValidationIssueCode.TypedPayloadMalformed), Is.True);
        }

        [Test]
        public void EffectWithWhitespaceOnlyMechanicsPayloadRef_FailsValidation_ContentBlockBoundary()
        {
            // ContentBlock/mechanics payload MVP boundary: no real
            // ContentBlockGraph exists anywhere in this codebase yet to
            // validate DAG-ness/cycles/operation names against
            // (`11_Content_Block_System` section 8/25) -- MechanicsPayloadRef
            // is validated only as a structurally-acceptable opaque
            // reference (non-null implies non-blank). EffectDefinition's own
            // constructor does not itself reject a whitespace-only ref
            // (ODY-S05-105 scope), so this is exactly the usability gap
            // ODY-S05-104 closes.
            var targetRule = new ContentTargetRule(ContentTargetSource.SourceEntity, 1, 1, false);
            var effect = new EffectDefinition(targetRule, EffectDurationType.Instant, null, EffectStackPolicy.IndependentInstances, "   ");
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Effect, TypedDefinitionCodec.EncodeEffect(effect));

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.False);
            Assert.That(result.Value.Issues.Any(i => i.IssueCode == CatalogValidationIssueCode.EffectMechanicsPayloadRefInvalid), Is.True);
        }

        [Test]
        public void AbilityWithNonEmptyMechanicsPayloadRef_PassesValidation_ContentBlockBoundary()
        {
            var targetRule = new ContentTargetRule(ContentTargetSource.ActingCharacter, 1, 1, true);
            var ability = new AbilityDefinition(AbilityEntryPointType.ActiveAction, "OnCommand", 1, Array.Empty<AbilityResourceCost>(), targetRule, "block_ref_001");
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Ability, TypedDefinitionCodec.EncodeAbility(ability));

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.True, string.Join(", ", result.Value.Issues.Select(i => i.IssueCode)));
        }

        // ---- 11. Existing definition with wrong version fails validation ----------

        [Test]
        public void ItemBuiltInEffectRef_WithWrongVersion_FailsValidation()
        {
            ContentDefinitionRecord effectRecord = CreateDraft(ContentDefinitionType.Effect, EncodeMinimalEffect());
            MarkPublishedDirectly(effectRecord.ContentDefinitionId, 2); // published at version 2

            var refAtWrongVersion = new ContentDefinitionRef(effectRecord.ContentDefinitionId, 1); // item pins version 1
            string itemJson = EncodeMinimalItem(builtInEffectRefs: new[] { refAtWrongVersion });
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Item, itemJson);

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.False);
            Assert.That(result.Value.Issues.Any(i => i.IssueCode == CatalogValidationIssueCode.ReferenceVersionMismatch), Is.True);
        }

        [Test]
        public void ItemBuiltInEffectRef_WithMatchingVersion_PassesValidation()
        {
            ContentDefinitionRecord effectRecord = CreateDraft(ContentDefinitionType.Effect, EncodeMinimalEffect());
            MarkPublishedDirectly(effectRecord.ContentDefinitionId, 1);

            var refAtMatchingVersion = new ContentDefinitionRef(effectRecord.ContentDefinitionId, 1);
            string itemJson = EncodeMinimalItem(builtInEffectRefs: new[] { refAtMatchingVersion });
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Item, itemJson);

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.True, string.Join(", ", result.Value.Issues.Select(i => i.IssueCode)));
        }

        // ---- Referenced definition ruleset compatibility (ODY-S05-104 amendment) ----

        [Test]
        public void ItemBuiltInEffectRef_ToDefinitionFromIncompatibleRuleset_FailsValidation()
        {
            // Existing, exact-version-matching, correctly-typed Effect --
            // but scoped to a different Ruleset than the active campaign.
            ContentDefinitionRecord effectRecord = CreateDraft(ContentDefinitionType.Effect, EncodeMinimalEffect(), rulesetCompatibility: new[] { "other.ruleset@9.9.9" });
            MarkPublishedDirectly(effectRecord.ContentDefinitionId, 1);

            var reference = new ContentDefinitionRef(effectRecord.ContentDefinitionId, 1);
            string itemJson = EncodeMinimalItem(builtInEffectRefs: new[] { reference });
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Item, itemJson);

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.False);
            Assert.That(result.Value.Issues.Any(i => i.IssueCode == CatalogValidationIssueCode.RulesetIncompatible && i.FieldPath == "properties.builtInEffectRefs[0]"), Is.True);
        }

        [Test]
        public void ItemBuiltInEffectRef_ToDefinitionWithUnrestrictedRuleset_PassesValidation()
        {
            ContentDefinitionRecord effectRecord = CreateDraft(ContentDefinitionType.Effect, EncodeMinimalEffect(), rulesetCompatibility: Array.Empty<string>());
            MarkPublishedDirectly(effectRecord.ContentDefinitionId, 1);

            var reference = new ContentDefinitionRef(effectRecord.ContentDefinitionId, 1);
            string itemJson = EncodeMinimalItem(builtInEffectRefs: new[] { reference });
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Item, itemJson);

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.True, string.Join(", ", result.Value.Issues.Select(i => i.IssueCode)));
        }

        [Test]
        public void ItemBuiltInEffectRef_ToDefinitionCompatibleWithActiveRuleset_PassesValidation()
        {
            ContentDefinitionRecord effectRecord = CreateDraft(ContentDefinitionType.Effect, EncodeMinimalEffect(), rulesetCompatibility: new[] { "ruleset.core@1.0.0" });
            MarkPublishedDirectly(effectRecord.ContentDefinitionId, 1);

            var reference = new ContentDefinitionRef(effectRecord.ContentDefinitionId, 1);
            string itemJson = EncodeMinimalItem(builtInEffectRefs: new[] { reference });
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Item, itemJson);

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.True, string.Join(", ", result.Value.Issues.Select(i => i.IssueCode)));
        }

        [Test]
        public void GenericDependencyRef_ToDefinitionFromIncompatibleRuleset_FailsValidation()
        {
            // The generic DependencyRefs envelope field must be checked the
            // same way as a typed reference -- Ability/Effect carry no
            // typed ContentDefinitionRef field of their own, so this is
            // their own only way to declare a cross-reference at all.
            ContentDefinitionRecord effectRecord = CreateDraft(ContentDefinitionType.Effect, EncodeMinimalEffect(), rulesetCompatibility: new[] { "other.ruleset@9.9.9" });
            MarkPublishedDirectly(effectRecord.ContentDefinitionId, 1);

            var reference = new ContentDefinitionRef(effectRecord.ContentDefinitionId, 1);
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Ability, EncodeMinimalAbility(), dependencyRefs: new[] { reference });

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.False);
            Assert.That(result.Value.Issues.Any(i => i.IssueCode == CatalogValidationIssueCode.RulesetIncompatible && i.FieldPath == "dependencyRefs[0]"), Is.True);
        }

        // ---- Dependency cycle detection (common validation item 9) ---------------

        [Test]
        public void DependencyCycle_BetweenTwoDefinitions_IsDetected_AndDoesNotLoopForever()
        {
            ContentDefinitionRecord itemRecord = CreateDraft(ContentDefinitionType.Item, EncodeMinimalItem());
            ContentDefinitionRecord effectRecord = CreateDraft(ContentDefinitionType.Effect, EncodeMinimalEffect());

            // Item -> Effect via its own typed BuiltInEffectRefs.
            string itemJsonReferencingEffect = EncodeMinimalItem(builtInEffectRefs: new[] { new ContentDefinitionRef(effectRecord.ContentDefinitionId, 1) });
            MarkPublishedDirectly(itemRecord.ContentDefinitionId, 1, propertiesJson: itemJsonReferencingEffect);

            // Effect -> Item via the generic DependencyRefs envelope field
            // (Ability/Effect carry no typed ContentDefinitionRef field of
            // their own -- ODY-S05-105) -- closing the cycle.
            MarkPublishedDirectly(effectRecord.ContentDefinitionId, 1, dependencyRefs: new[] { new ContentDefinitionRef(itemRecord.ContentDefinitionId, 1) });

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateContentDefinition(_catalogRepository, RequestFor(itemRecord.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True, "a cycle must be reported as a validation issue, not an infinite loop or a repository failure");
            Assert.That(result.Value.IsValid, Is.False);
            Assert.That(result.Value.Issues.Any(i => i.IssueCode == CatalogValidationIssueCode.DependencyCycleDetected), Is.True);
        }

        // ---- 12. Ruleset compatibility --------------------------------------------

        [Test]
        public void RulesetCompatibility_NotIncludingActiveRuleset_FailsValidation()
        {
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Item, EncodeMinimalItem(), rulesetCompatibility: new[] { "other.ruleset@9.9.9" });

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.False);
            Assert.That(result.Value.Issues.Any(i => i.IssueCode == CatalogValidationIssueCode.RulesetIncompatible), Is.True);
        }

        [Test]
        public void RulesetCompatibility_IncludingActiveRuleset_PassesValidation()
        {
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Item, EncodeMinimalItem(), rulesetCompatibility: new[] { "ruleset.core@1.0.0" });

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.True, string.Join(", ", result.Value.Issues.Select(i => i.IssueCode)));
        }

        [Test]
        public void RulesetCompatibility_Empty_MeansCompatibleWithAnyRuleset()
        {
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Item, EncodeMinimalItem(), rulesetCompatibility: Array.Empty<string>());

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsValid, Is.True, string.Join(", ", result.Value.Issues.Select(i => i.IssueCode)));
        }

        // ---- 13. Non-Draft definition fails validate-for-publish -------------------

        [Test]
        public void PublishedDefinition_FailsValidateDraftForPublish_ButPassesValidateContentDefinition()
        {
            ContentDefinitionRecord record = CreateDraft(ContentDefinitionType.Item, EncodeMinimalItem());
            MarkPublishedDirectly(record.ContentDefinitionId, 1);

            Result<CatalogValidationResult> publishResult = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(record.ContentDefinitionId));
            Assert.That(publishResult.IsSuccess, Is.True);
            Assert.That(publishResult.Value.IsValid, Is.False);
            Assert.That(publishResult.Value.Issues.Any(i => i.IssueCode == CatalogValidationIssueCode.DefinitionNotDraft), Is.True);

            Result<CatalogValidationResult> generalResult = CatalogValidationService.ValidateContentDefinition(_catalogRepository, RequestFor(record.ContentDefinitionId));
            Assert.That(generalResult.IsSuccess, Is.True);
            Assert.That(generalResult.Value.Issues.Any(i => i.IssueCode == CatalogValidationIssueCode.DefinitionNotDraft), Is.False);
        }

        // ---- Definition must exist -------------------------------------------------

        [Test]
        public void ValidatingNonExistentDefinition_ReturnsResultFailure_NotAnIssue()
        {
            ContentDefinitionId missingId = ContentDefinitionId.NewId(UtcInstant.Parse("2026-09-04T00:00:00.0000000Z"));

            Result<CatalogValidationResult> result = CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(missingId));

            Assert.That(result.IsFailure, Is.True);
        }

        // ---- 14. Validation does not mutate catalog state/revision -----------------

        [Test]
        public void ValidationRun_DoesNotMutateCatalogRow()
        {
            ContentDefinitionRecord before = CreateDraft(ContentDefinitionType.Item, EncodeMinimalItem());

            CatalogValidationService.ValidateDraftForPublish(_catalogRepository, RequestFor(before.ContentDefinitionId));
            CatalogValidationService.ValidateContentDefinition(_catalogRepository, RequestFor(before.ContentDefinitionId));

            Result<ContentDefinitionRecord> after = _catalogRepository.GetContentDefinition(_campaign, before.ContentDefinitionId, TestCorrelationId);
            Assert.That(after.IsSuccess, Is.True);
            Assert.That(after.Value.Revision, Is.EqualTo(before.Revision));
            Assert.That(after.Value.Status, Is.EqualTo(before.Status));
            Assert.That(after.Value.Version, Is.EqualTo(before.Version));
            Assert.That(after.Value.UpdatedAt, Is.EqualTo(before.UpdatedAt));
        }

        // ---- 16. No runtime Inventory/ItemInstance/ItemStack/Equipment/ActiveEffect ----

        [Test]
        public void ValidationLayer_IntroducesNoRuntimeItemInventoryEquipmentOrActiveEffectType()
        {
            System.Reflection.Assembly applicationAssembly = typeof(CatalogValidationService).Assembly;
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
        public void ValidationLayer_NeverCallsAWriteMethod_NoTableSchemaChange()
        {
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
                Assert.That(tableNames, Has.None.Contain(forbidden), $"no table containing '{forbidden}' may exist -- this task implements Catalog Validation only, no new persistence table");
            }
        }
    }
}
