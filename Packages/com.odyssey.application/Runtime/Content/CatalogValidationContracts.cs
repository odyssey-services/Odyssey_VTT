using System;
using System.Collections.Generic;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Content
{
    /// <summary>
    /// ODY-S05-104: Catalog Validation MVP. Application-layer, side-effect-
    /// free validation over the generic `ContentDefinition` envelope
    /// (`ODY-S05-101`) and the typed shapes `ODY-S05-105`'s
    /// <see cref="TypedDefinitionCodec"/> decodes it into. Proves real
    /// usability/applicability -- not just required-field presence
    /// (`SLICE-05_IMPLEMENTATION_BACKLOG.md` section 3.4, `ADR-027` section
    /// 20) -- and returns a structured <see cref="CatalogValidationResult"/>,
    /// never a raw exception.
    ///
    /// This service does not publish, archive, or delete anything; it never
    /// calls a repository write method. `ODY-S05-103`'s own publish gate is
    /// the intended caller of <see cref="ValidateDraftForPublish"/>. It does
    /// not execute attacks, abilities, effects, or `ContentBlock` graphs --
    /// `MechanicsPayloadRef` (`ODY-S05-105`'s own opaque placeholder for a
    /// future `11_Content_Block_System` section 8 graph) is validated only
    /// as a structurally-acceptable non-empty reference when present; no
    /// real graph/cycle/operation-name checking exists anywhere in this
    /// codebase yet to validate against (an explicitly recorded MVP
    /// boundary, not a skipped check -- see this task's own contract
    /// section 18).
    /// </summary>
    public static class CatalogValidationService
    {
        /// <summary>
        /// Full common + per-type usability validation, without requiring
        /// <see cref="ContentDefinitionStatus.Draft"/> -- usable to
        /// re-validate a definition regardless of its current lifecycle
        /// status (e.g. a tooling/preview scenario). <see cref="ValidateDraftForPublish"/>
        /// is the one `ODY-S05-103`'s own publish gate is expected to call.
        /// </summary>
        public static Result<CatalogValidationResult> ValidateContentDefinition(IContentCatalogRepository repository, ValidateContentDefinitionRequest request)
            => ValidateInternal(repository, request, requireDraft: false);

        /// <summary>
        /// Everything <see cref="ValidateContentDefinition"/> checks, plus
        /// the publish-time-only requirement that the definition being
        /// validated is currently a <see cref="ContentDefinitionStatus.Draft"/>
        /// (`ADR-027` section 4.1: only a Draft can ever be published).
        /// </summary>
        public static Result<CatalogValidationResult> ValidateDraftForPublish(IContentCatalogRepository repository, ValidateContentDefinitionRequest request)
            => ValidateInternal(repository, request, requireDraft: true);

        private static Result<CatalogValidationResult> ValidateInternal(IContentCatalogRepository repository, ValidateContentDefinitionRequest request, bool requireDraft)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (request == null) throw new ArgumentNullException(nameof(request));

            // Common validation item 1: the definition must exist. A
            // missing definition is a genuine operation failure (there is
            // nothing to validate), not a validation issue inside an
            // otherwise-successful result -- mirrors every other repository
            // read call's own NotFound convention.
            Result<ContentDefinitionRecord> fetched = repository.GetContentDefinition(request.Campaign, request.DefinitionId, request.CorrelationId);
            if (fetched.IsFailure)
            {
                return Result<CatalogValidationResult>.Failure(fetched.Error);
            }

            var issues = new List<CatalogValidationIssue>();
            ContentDefinitionRecord record = fetched.Value;

            if (requireDraft && record.Status != ContentDefinitionStatus.Draft)
            {
                issues.Add(new CatalogValidationIssue(
                    CatalogValidationIssueCode.DefinitionNotDraft,
                    CatalogValidationSeverity.Error,
                    UserMessageKey.Parse("errors.content_catalog.validation.not_draft"),
                    "status"));
            }

            ValidateRulesetCompatibility(request.Campaign, record, issues);

            switch (record.DefinitionType)
            {
                case ContentDefinitionType.Item:
                    ValidateItem(record, request.CorrelationId, issues);
                    break;
                case ContentDefinitionType.Weapon:
                    ValidateWeapon(repository, request.Campaign, record, request.CorrelationId, issues);
                    break;
                case ContentDefinitionType.Armor:
                    ValidateArmor(record, request.CorrelationId, issues);
                    break;
                case ContentDefinitionType.Ammo:
                    ValidateAmmo(record, request.CorrelationId, issues);
                    break;
                case ContentDefinitionType.Ability:
                    ValidateAbility(record, request.CorrelationId, issues);
                    break;
                case ContentDefinitionType.Effect:
                    ValidateEffect(record, request.CorrelationId, issues);
                    break;
                default:
                    // ODY-S05-105 gave a real typed shape only to these 6
                    // ContentDefinitionType values. Perk/Action/Mechanic/
                    // Attribute/Skill/BodyPart/Resource/NpcTemplateData have
                    // no TypedDefinitionCodec path yet -- validated at the
                    // generic envelope/reference level only (below), an
                    // explicitly recorded MVP boundary, not an oversight.
                    break;
            }

            ValidateReferencesAndCycles(repository, request.Campaign, record, request.CorrelationId, issues);

            return Result<CatalogValidationResult>.Success(new CatalogValidationResult(issues));
        }

        private static void ValidateRulesetCompatibility(CampaignHandle campaign, ContentDefinitionRecord record, List<CatalogValidationIssue> issues)
        {
            // Empty RulesetCompatibility means the author declared no
            // restriction -- compatible with any ruleset, matching
            // ODY-S05-101/102's own "[]" default for this field.
            if (record.RulesetCompatibility.Count == 0)
            {
                return;
            }

            string activeRulesetKey = campaign.Manifest.RulesetId + "@" + campaign.Manifest.RulesetVersion;
            foreach (string entry in record.RulesetCompatibility)
            {
                if (string.Equals(entry, activeRulesetKey, StringComparison.Ordinal))
                {
                    return;
                }
            }

            issues.Add(new CatalogValidationIssue(
                CatalogValidationIssueCode.RulesetIncompatible,
                CatalogValidationSeverity.Error,
                UserMessageKey.Parse("errors.content_catalog.validation.ruleset_incompatible"),
                "rulesetCompatibility"));
        }

        private static void ValidateItem(ContentDefinitionRecord record, CorrelationId correlationId, List<CatalogValidationIssue> issues)
        {
            Result<ItemDefinition> decoded = TypedDefinitionCodec.DecodeItem(record.DefinitionType, record.PropertiesJson, correlationId);
            if (decoded.IsFailure)
            {
                AddCodecFailureIssue(decoded.Error, "properties", issues);
                return;
            }

            // Stack size/durability/charges internal consistency is already
            // guaranteed by ItemDefinition's own constructor (ODY-S05-105);
            // built-in ability/effect reference existence/exact-version/
            // target-type correctness is checked by
            // ValidateReferencesAndCycles below, not duplicated here.
        }

        private static void ValidateWeapon(IContentCatalogRepository repository, CampaignHandle campaign, ContentDefinitionRecord record, CorrelationId correlationId, List<CatalogValidationIssue> issues)
        {
            Result<WeaponDefinition> decoded = TypedDefinitionCodec.DecodeWeapon(record.DefinitionType, record.PropertiesJson, correlationId);
            if (decoded.IsFailure)
            {
                AddCodecFailureIssue(decoded.Error, "properties", issues);
                return;
            }

            WeaponDefinition weapon = decoded.Value;

            // Damage/range/attack-mode/action-cost are already ctor-
            // guaranteed non-negative/enum-defined/non-empty by
            // WeaponDefinition itself; the genuine usability gap
            // ODY-S05-105 deliberately left open is ammo compatibility.
            if (weapon.AmmoRequirement != AmmoRequirement.None && weapon.CompatibleAmmoKeys.Count == 0)
            {
                issues.Add(new CatalogValidationIssue(
                    CatalogValidationIssueCode.WeaponAmmoCompatibilityKeysRequired,
                    CatalogValidationSeverity.Error,
                    UserMessageKey.Parse("errors.content_catalog.validation.weapon_ammo_keys_required"),
                    "properties.compatibleAmmoKeys"));
            }
            else if (weapon.AmmoRequirement == AmmoRequirement.Required && !CatalogHasCompatibleAmmo(repository, campaign, weapon.CompatibleAmmoKeys, correlationId))
            {
                issues.Add(new CatalogValidationIssue(
                    CatalogValidationIssueCode.WeaponNoCompatibleAmmoInCatalog,
                    CatalogValidationSeverity.Error,
                    UserMessageKey.Parse("errors.content_catalog.validation.weapon_no_compatible_ammo"),
                    "properties.compatibleAmmoKeys"));
            }
        }

        /// <summary>
        /// Scans every catalog definition (any status -- an Ammo definition
        /// does not need to be Published to prove the weapon/ammo category
        /// key vocabulary already overlaps; publish-readiness of the ammo
        /// itself is that ammo's own separate validation concern) for an
        /// `Ammo`-typed definition whose own `CompatibilityKeys` overlaps
        /// the weapon's `CompatibleAmmoKeys`.
        /// </summary>
        private static bool CatalogHasCompatibleAmmo(IContentCatalogRepository repository, CampaignHandle campaign, IReadOnlyList<string> weaponCompatibleAmmoKeys, CorrelationId correlationId)
        {
            Result<IReadOnlyList<ContentDefinitionRecord>> listed = repository.ListContentDefinitions(campaign, statusFilter: null, correlationId);
            if (listed.IsFailure)
            {
                return false;
            }

            foreach (ContentDefinitionRecord candidate in listed.Value)
            {
                if (candidate.DefinitionType != ContentDefinitionType.Ammo)
                {
                    continue;
                }

                Result<AmmoDefinition> ammo = TypedDefinitionCodec.DecodeAmmo(candidate.DefinitionType, candidate.PropertiesJson, correlationId);
                if (ammo.IsFailure)
                {
                    continue;
                }

                foreach (string ammoKey in ammo.Value.CompatibilityKeys)
                {
                    if (Contains(weaponCompatibleAmmoKeys, ammoKey))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool Contains(IReadOnlyList<string> values, string candidate)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], candidate, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateArmor(ContentDefinitionRecord record, CorrelationId correlationId, List<CatalogValidationIssue> issues)
        {
            Result<ArmorDefinition> decoded = TypedDefinitionCodec.DecodeArmor(record.DefinitionType, record.PropertiesJson, correlationId);
            if (decoded.IsFailure)
            {
                AddCodecFailureIssue(decoded.Error, "properties", issues);
                return;
            }

            // Equipment slot key / covered body-part presence / protection /
            // embedded-ItemDefinition durability consistency are already
            // ctor-guaranteed by ArmorDefinition/ItemDefinition themselves
            // (ODY-S05-105). No Ruleset-wide anatomy-profile/body-part
            // catalog exists anywhere in this codebase to check
            // CoveredBodyPartIds against -- BodyPartId (ODY-S04-109) is a
            // per-Character structural slot with no backing registry, the
            // same honest boundary this task's own contract records for
            // ResourceDefinitionId (see ValidateAbility). Structural
            // BodyPartId validity is guaranteed by ArmorDefinition's own
            // constructor.
        }

        private static void ValidateAmmo(ContentDefinitionRecord record, CorrelationId correlationId, List<CatalogValidationIssue> issues)
        {
            Result<AmmoDefinition> decoded = TypedDefinitionCodec.DecodeAmmo(record.DefinitionType, record.PropertiesJson, correlationId);
            if (decoded.IsFailure)
            {
                AddCodecFailureIssue(decoded.Error, "properties", issues);
                return;
            }

            // CompatibilityKeys non-empty is already ctor-guaranteed;
            // EffectContributionRefs existence/exact-version/target-type
            // correctness is checked by ValidateReferencesAndCycles below.
        }

        private static void ValidateAbility(ContentDefinitionRecord record, CorrelationId correlationId, List<CatalogValidationIssue> issues)
        {
            Result<AbilityDefinition> decoded = TypedDefinitionCodec.DecodeAbility(record.DefinitionType, record.PropertiesJson, correlationId);
            if (decoded.IsFailure)
            {
                AddCodecFailureIssue(decoded.Error, "properties", issues);
                return;
            }

            ValidateMechanicsPayloadRef(decoded.Value.MechanicsPayloadRef, CatalogValidationIssueCode.AbilityMechanicsPayloadRefInvalid, "errors.content_catalog.validation.ability_mechanics_payload_invalid", issues);

            // ResourceDefinitionId (ODY-S04-108) is SLICE-04's own
            // lightweight, fixture-only Ruleset key with no backing catalog
            // table anywhere in this codebase -- there is no live registry
            // to check a resource cost's ResourceDefinitionId existence
            // against yet. Structural (regex) validity is already
            // guaranteed by AbilityResourceCost's own constructor. Target
            // rule shape (min/max/source) is already ctor-guaranteed by
            // ContentTargetRule. This is an explicitly recorded MVP
            // boundary -- see this task's own contract section 18 -- not a
            // skipped check.
        }

        private static void ValidateEffect(ContentDefinitionRecord record, CorrelationId correlationId, List<CatalogValidationIssue> issues)
        {
            Result<EffectDefinition> decoded = TypedDefinitionCodec.DecodeEffect(record.DefinitionType, record.PropertiesJson, correlationId);
            if (decoded.IsFailure)
            {
                AddCodecFailureIssue(decoded.Error, "properties", issues);
                return;
            }

            ValidateMechanicsPayloadRef(decoded.Value.MechanicsPayloadRef, CatalogValidationIssueCode.EffectMechanicsPayloadRefInvalid, "errors.content_catalog.validation.effect_mechanics_payload_invalid", issues);

            // Target rule / duration type+value pairing / stacking policy
            // are already ctor-guaranteed by ContentTargetRule/
            // EffectDefinition themselves (ODY-S05-105).
        }

        /// <summary>
        /// ContentBlock/mechanics payload MVP boundary: `MechanicsPayloadRef`
        /// is a deliberately opaque placeholder (`ODY-S05-105`'s own design
        /// decision) -- no real `11_Content_Block_System` section 8
        /// `ContentBlockGraph` exists anywhere in this codebase yet to
        /// validate DAG-ness/cycles/unsupported-operation-names against
        /// (`11_Content_Block_System` section 25's own static-validation
        /// list). Validated here only as a structurally-acceptable
        /// reference: `null` means "no mechanics implemented yet" (allowed
        /// -- this is an MVP, not every ability/effect needs real
        /// mechanics); a non-null value must not be empty/whitespace-only.
        /// This explicit boundary is recorded, not silently skipped -- see
        /// this task's own contract section 7 ("ContentBlock / mechanics
        /// payload MVP") and section 18.
        /// </summary>
        private static void ValidateMechanicsPayloadRef(string? mechanicsPayloadRef, CatalogValidationIssueCode issueCode, string messageKey, List<CatalogValidationIssue> issues)
        {
            if (mechanicsPayloadRef != null && string.IsNullOrWhiteSpace(mechanicsPayloadRef))
            {
                issues.Add(new CatalogValidationIssue(issueCode, CatalogValidationSeverity.Error, UserMessageKey.Parse(messageKey), "properties.mechanicsPayloadRef"));
            }
        }

        private static void AddCodecFailureIssue(Error codecError, string fieldPath, List<CatalogValidationIssue> issues)
        {
            CatalogValidationIssueCode code = codecError.Code == ErrorCodes.ContentCatalogTypedDefinitionWrongType
                ? CatalogValidationIssueCode.TypedPayloadWrongType
                : CatalogValidationIssueCode.TypedPayloadMalformed;
            issues.Add(new CatalogValidationIssue(code, CatalogValidationSeverity.Error, codecError.UserMessageKey, fieldPath));
        }

        /// <summary>
        /// Common validation items 6-9: every exact <see cref="ContentDefinitionRef"/>
        /// reachable from <paramref name="root"/> -- both the generic
        /// <see cref="ContentDefinitionRecord.DependencyRefs"/> envelope
        /// field and the typed-property references `ODY-S05-105`'s own
        /// shapes carry (`ItemDefinition.BuiltInAbilityRefs`/
        /// `BuiltInEffectRefs`, `AmmoDefinition.EffectContributionRefs`) --
        /// must resolve to an existing definition at the exact requested
        /// <see cref="ContentDefinitionRef.Version"/>, loadable regardless
        /// of that target's own <see cref="ContentDefinitionStatus"/>
        /// (`GetContentDefinition` does not filter by status, so an Archived
        /// target still resolves -- `ADR-027` section 4.1 rule 3). A
        /// depth-first traversal with a currently-on-stack set detects a
        /// dependency cycle deterministically and terminates the recursion
        /// the moment a back-edge is found -- no infinite loop is possible
        /// by construction; an additional flat node-count cap is a cheap
        /// defensive safety net, not required for correctness.
        /// </summary>
        private static void ValidateReferencesAndCycles(IContentCatalogRepository repository, CampaignHandle campaign, ContentDefinitionRecord root, CorrelationId correlationId, List<CatalogValidationIssue> issues)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var onStack = new HashSet<string>(StringComparer.Ordinal);
            bool cycleReported = false;
            bool tooDeepReported = false;
            const int MaxVisitedNodes = 256;

            void Visit(ContentDefinitionRecord record)
            {
                string key = record.ContentDefinitionId.ToString();
                if (onStack.Contains(key))
                {
                    if (!cycleReported)
                    {
                        issues.Add(new CatalogValidationIssue(
                            CatalogValidationIssueCode.DependencyCycleDetected,
                            CatalogValidationSeverity.Error,
                            UserMessageKey.Parse("errors.content_catalog.validation.dependency_cycle"),
                            "dependencyRefs"));
                        cycleReported = true;
                    }

                    return;
                }

                if (visited.Contains(key))
                {
                    return;
                }

                if (visited.Count >= MaxVisitedNodes)
                {
                    if (!tooDeepReported)
                    {
                        issues.Add(new CatalogValidationIssue(
                            CatalogValidationIssueCode.DependencyGraphTooDeep,
                            CatalogValidationSeverity.Error,
                            UserMessageKey.Parse("errors.content_catalog.validation.dependency_graph_too_deep"),
                            "dependencyRefs"));
                        tooDeepReported = true;
                    }

                    return;
                }

                visited.Add(key);
                onStack.Add(key);

                foreach ((ContentDefinitionRef childRef, ContentDefinitionType? expectedType, string fieldPath) in CollectOutgoingRefs(record, correlationId))
                {
                    Result<ContentDefinitionRecord> childResult = repository.GetContentDefinition(campaign, childRef.DefinitionId, correlationId);
                    if (childResult.IsFailure)
                    {
                        issues.Add(new CatalogValidationIssue(
                            CatalogValidationIssueCode.ReferenceMissing,
                            CatalogValidationSeverity.Error,
                            UserMessageKey.Parse("errors.content_catalog.validation.reference_missing"),
                            fieldPath));
                        continue;
                    }

                    ContentDefinitionRecord child = childResult.Value;
                    if (child.Version != childRef.Version)
                    {
                        issues.Add(new CatalogValidationIssue(
                            CatalogValidationIssueCode.ReferenceVersionMismatch,
                            CatalogValidationSeverity.Error,
                            UserMessageKey.Parse("errors.content_catalog.validation.reference_version_mismatch"),
                            fieldPath));
                        continue;
                    }

                    if (expectedType.HasValue && child.DefinitionType != expectedType.Value)
                    {
                        issues.Add(new CatalogValidationIssue(
                            CatalogValidationIssueCode.ReferenceWrongType,
                            CatalogValidationSeverity.Error,
                            UserMessageKey.Parse("errors.content_catalog.validation.reference_wrong_type"),
                            fieldPath));
                        continue;
                    }

                    Visit(child);
                }

                onStack.Remove(key);
            }

            Visit(root);
        }

        /// <summary>
        /// The generic <see cref="ContentDefinitionRecord.DependencyRefs"/>
        /// field (no expected target type -- `ADR-027` section 4 rule 5's
        /// generic `ContentDependency` tracking) plus every typed-property
        /// <see cref="ContentDefinitionRef"/> `ODY-S05-105`'s own shapes
        /// declare, each carrying the target <see cref="ContentDefinitionType"/>
        /// it must resolve to. A definition whose own <see cref="TypedDefinitionCodec"/>
        /// decode fails contributes no typed outgoing edges (its own shape
        /// is already invalid and reported separately when it is the record
        /// being directly validated); traversal simply does not descend
        /// further through it.
        /// </summary>
        private static List<(ContentDefinitionRef Ref, ContentDefinitionType? ExpectedType, string FieldPath)> CollectOutgoingRefs(ContentDefinitionRecord record, CorrelationId correlationId)
        {
            var refs = new List<(ContentDefinitionRef, ContentDefinitionType?, string)>();

            for (int i = 0; i < record.DependencyRefs.Count; i++)
            {
                refs.Add((record.DependencyRefs[i], null, "dependencyRefs[" + i + "]"));
            }

            switch (record.DefinitionType)
            {
                case ContentDefinitionType.Item:
                    {
                        Result<ItemDefinition> decoded = TypedDefinitionCodec.DecodeItem(record.DefinitionType, record.PropertiesJson, correlationId);
                        if (decoded.IsSuccess)
                        {
                            AddItemRefs(decoded.Value, refs);
                        }

                        break;
                    }
                case ContentDefinitionType.Weapon:
                    {
                        Result<WeaponDefinition> decoded = TypedDefinitionCodec.DecodeWeapon(record.DefinitionType, record.PropertiesJson, correlationId);
                        if (decoded.IsSuccess)
                        {
                            AddItemRefs(decoded.Value.Item, refs);
                        }

                        break;
                    }
                case ContentDefinitionType.Armor:
                    {
                        Result<ArmorDefinition> decoded = TypedDefinitionCodec.DecodeArmor(record.DefinitionType, record.PropertiesJson, correlationId);
                        if (decoded.IsSuccess)
                        {
                            AddItemRefs(decoded.Value.Item, refs);
                        }

                        break;
                    }
                case ContentDefinitionType.Ammo:
                    {
                        Result<AmmoDefinition> decoded = TypedDefinitionCodec.DecodeAmmo(record.DefinitionType, record.PropertiesJson, correlationId);
                        if (decoded.IsSuccess)
                        {
                            AddItemRefs(decoded.Value.Item, refs);
                            IReadOnlyList<ContentDefinitionRef> effectRefs = decoded.Value.EffectContributionRefs;
                            for (int i = 0; i < effectRefs.Count; i++)
                            {
                                refs.Add((effectRefs[i], ContentDefinitionType.Effect, "properties.effectContributionRefs[" + i + "]"));
                            }
                        }

                        break;
                    }

                    // Ability/Effect (ODY-S05-105) carry no ContentDefinitionRef
                    // fields of their own -- AbilityResourceCost uses the
                    // unrelated ResourceDefinitionId, and neither type embeds an
                    // ItemDefinition. Any cross-reference for these two kinds
                    // can only come through the generic DependencyRefs field
                    // already added above.
            }

            return refs;
        }

        private static void AddItemRefs(ItemDefinition item, List<(ContentDefinitionRef, ContentDefinitionType?, string)> refs)
        {
            IReadOnlyList<ContentDefinitionRef> abilityRefs = item.BuiltInAbilityRefs;
            for (int i = 0; i < abilityRefs.Count; i++)
            {
                refs.Add((abilityRefs[i], ContentDefinitionType.Ability, "properties.builtInAbilityRefs[" + i + "]"));
            }

            IReadOnlyList<ContentDefinitionRef> effectRefs = item.BuiltInEffectRefs;
            for (int i = 0; i < effectRefs.Count; i++)
            {
                refs.Add((effectRefs[i], ContentDefinitionType.Effect, "properties.builtInEffectRefs[" + i + "]"));
            }
        }
    }

    /// <summary>Request for <see cref="CatalogValidationService.ValidateContentDefinition"/>/<see cref="CatalogValidationService.ValidateDraftForPublish"/>. Carries the active-ruleset context implicitly through <see cref="Campaign"/>.<c>Manifest</c> (`RulesetId`/`RulesetVersion`) -- no separate ruleset parameter is needed since every campaign already knows its own active Ruleset.</summary>
    public sealed class ValidateContentDefinitionRequest
    {
        public ValidateContentDefinitionRequest(CampaignHandle campaign, ContentDefinitionId definitionId, CorrelationId correlationId)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            if (!definitionId.IsValid) throw new ArgumentException("ContentDefinitionId is required.", nameof(definitionId));

            DefinitionId = definitionId;
            CorrelationId = correlationId;
        }

        public CampaignHandle Campaign { get; }
        public ContentDefinitionId DefinitionId { get; }
        public CorrelationId CorrelationId { get; }
    }

    /// <summary>Minimum severity this MVP ever produces is <see cref="Error"/> (every issue below blocks `IsValid`) -- <see cref="Warning"/> exists in the vocabulary for a future non-blocking check, per `11_Content_Block_System` section 25's own "Errors блокируют publication. Warnings требуют явного подтверждения" distinction, but no validation rule in this task ever produces one.</summary>
    public enum CatalogValidationSeverity
    {
        Error = 1,
        Warning = 2
    }

    /// <summary>
    /// ODY-S05-104's own small, explicit catalog validation vocabulary.
    /// Deliberately plain enum members, not registered `ErrorCode`s
    /// (`docs/errors/ERROR_CODES.md`'s registry governs `Error`/`Result`
    /// failures returned by an operation, not the structured issue list a
    /// *successful* validation run produces) -- a validation run that finds
    /// issues is still a successful `Result&lt;CatalogValidationResult&gt;.Success`,
    /// not a `Result.Failure`.
    /// </summary>
    public enum CatalogValidationIssueCode
    {
        /// <summary>Common validation item 2: the definition is not currently `Draft`, checked only by <see cref="CatalogValidationService.ValidateDraftForPublish"/>.</summary>
        DefinitionNotDraft = 1,

        /// <summary>Mapped from <see cref="ErrorCodes.ContentCatalogTypedDefinitionWrongType"/> -- decode was attempted against a mismatched <see cref="ContentDefinitionType"/>.</summary>
        TypedPayloadWrongType = 2,

        /// <summary>Mapped from <see cref="ErrorCodes.ContentCatalogTypedDefinitionMalformedPayload"/> -- `PropertiesJson` is not valid for its own <see cref="ContentDefinitionType"/>.</summary>
        TypedPayloadMalformed = 3,

        /// <summary>Common validation item 5: `RulesetCompatibility` does not include the campaign's own active Ruleset id/version.</summary>
        RulesetIncompatible = 4,

        /// <summary>Common validation item 6: a referenced <see cref="ContentDefinitionRef.DefinitionId"/> does not exist in the catalog at all.</summary>
        ReferenceMissing = 5,

        /// <summary>Common validation item 7: a referenced definition exists, but its own current <see cref="ContentDefinitionRecord.Version"/> does not equal the exact requested <see cref="ContentDefinitionRef.Version"/>.</summary>
        ReferenceVersionMismatch = 6,

        /// <summary>A referenced definition exists at the exact requested version, but its own <see cref="ContentDefinitionRecord.DefinitionType"/> does not match what the referencing field expects (e.g. `BuiltInAbilityRefs` pointing at a non-`Ability` definition).</summary>
        ReferenceWrongType = 7,

        /// <summary>Common validation item 9: the dependency graph reachable from the definition being validated contains a cycle.</summary>
        DependencyCycleDetected = 8,

        /// <summary>Defensive safety net distinct from a genuine cycle -- the reachable dependency graph exceeds this validator's own flat node-count cap.</summary>
        DependencyGraphTooDeep = 9,

        /// <summary>Weapon usability: `AmmoRequirement` is `Required`/`Optional` but `CompatibleAmmoKeys` is empty.</summary>
        WeaponAmmoCompatibilityKeysRequired = 10,

        /// <summary>Weapon usability: `AmmoRequirement` is `Required`, `CompatibleAmmoKeys` is non-empty, but no `Ammo`-typed definition anywhere in the catalog shares any of those keys.</summary>
        WeaponNoCompatibleAmmoInCatalog = 11,

        /// <summary>ContentBlock/mechanics payload MVP boundary: an `AbilityDefinition.MechanicsPayloadRef` is present but empty/whitespace-only.</summary>
        AbilityMechanicsPayloadRefInvalid = 12,

        /// <summary>ContentBlock/mechanics payload MVP boundary: an `EffectDefinition.MechanicsPayloadRef` is present but empty/whitespace-only.</summary>
        EffectMechanicsPayloadRefInvalid = 13
    }

    /// <summary>One structured validation finding. <see cref="FieldPath"/> is an optional, best-effort JSON-path-shaped hint (e.g. `properties.damageExpression`) -- not a strict schema path grammar, matching every other lightweight "hint, not contract" string field already in this codebase.</summary>
    public sealed class CatalogValidationIssue
    {
        public CatalogValidationIssue(CatalogValidationIssueCode issueCode, CatalogValidationSeverity severity, UserMessageKey messageKey, string? fieldPath)
        {
            if (!Enum.IsDefined(typeof(CatalogValidationIssueCode), issueCode)) throw new ArgumentOutOfRangeException(nameof(issueCode));
            if (!Enum.IsDefined(typeof(CatalogValidationSeverity), severity)) throw new ArgumentOutOfRangeException(nameof(severity));

            IssueCode = issueCode;
            Severity = severity;
            MessageKey = messageKey;
            FieldPath = fieldPath;
        }

        public CatalogValidationIssueCode IssueCode { get; }
        public CatalogValidationSeverity Severity { get; }

        /// <summary>Public-safe message key, the same `UserMessageKey` convention `Error` already uses -- never a raw/interpolated message string.</summary>
        public UserMessageKey MessageKey { get; }
        public string? FieldPath { get; }
    }

    /// <summary>
    /// The structured outcome of one validation run. <see cref="IsValid"/>
    /// is `true` only when no <see cref="CatalogValidationSeverity.Error"/>-severity
    /// issue is present -- a `Warning`-only result (none exist yet in this
    /// task) would still be `IsValid`, per `11_Content_Block_System` section
    /// 25's own "Errors блокируют publication. Warnings требуют явного
    /// подтверждения" distinction.
    /// </summary>
    public sealed class CatalogValidationResult
    {
        public CatalogValidationResult(IReadOnlyList<CatalogValidationIssue> issues)
        {
            Issues = issues ?? throw new ArgumentNullException(nameof(issues));

            bool hasError = false;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == CatalogValidationSeverity.Error)
                {
                    hasError = true;
                    break;
                }
            }

            IsValid = !hasError;
        }

        public bool IsValid { get; }
        public IReadOnlyList<CatalogValidationIssue> Issues { get; }
    }
}
