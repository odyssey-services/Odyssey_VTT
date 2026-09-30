# ODY-S11-202 — Content catalog UI for all seven typed definitions (phase 2)

**Status:** In Review  
**Roadmap stage / slice:** SLICE-11 (full client UI, phase 2 of 0–5)  
**Owner:** Claude Code  
**Requested by:** Product owner  
**Branch:** `claude/pensive-gates-n18srp`  
**Pull request:** Not opened  
**ExecPlan:** `ODY-S11-200` §14  
**Created:** 2026-09-30  
**Last updated:** 2026-09-30

## 1. Goal

The MainGM can create, edit, validate, publish, version, archive and delete catalog definitions of every typed
definition (Item, Weapon, Armor, Ammo, Ability, Effect, Skill) from the Catalog drawer; everyone else browses
published definitions with an explanation of why they cannot author.

## 2. Why this task exists

The backend (`ODY-S05-101…106`, `ODY-S07-103/104`, `ODY-S10-104`) has the full catalog, but no client screen.
The spec's premise that an Item screen existed (`ODY-S11-101`) is false (see `ODY-S11-200` §4); per the product
owner's decision the screen is built new, covering all seven types at once.

## 3. Authorities and requirement references

- ADR-027 (content catalog), ADR-002 (commands, CommandId), ADR-004 (typed errors), ADR-003 (codec-built JSON only).
- `ContentCatalogAuthoringService`, `ContentCatalogLifecycleService`, `CatalogValidationService`, `TypedDefinitionCodec`.
- New test IDs: `TC-CATALOGUI-001`…`TC-CATALOGUI-012`.

## 4. Verified current state (backend facts used)

- Common envelope `ContentDefinitionRecord` (Name/Description/Status/Version/Revision/RulesetCompatibility/Tags/
  PropertiesJson/DependencyRefs). Typed payloads are encoded only by `TypedDefinitionCodec`.
- `UpdateDraftContentDefinition` changes **name, description, properties only** — not tags or ruleset compatibility.
- `CreateNextDraftVersionFromPublished` creates a new Draft row (new id, Version 0) copying the published one.
- Publish runs `CatalogValidationService.ValidateDraftForPublish`; a failure returns one generic
  `publish_validation_failed` error, the individual issues come from the validation service.
- Weapon + `AmmoRequirement=Required` publishes only if some Ammo (any status, ruleset-compatible) shares a key.
- `MechanicsPayloadRef`: `null` allowed; a non-null value must not be blank.
- Archived rows: `ContentCatalogLifecycleService.ListArchivedDefinitions` (MainGM-only).

## 5. Scope

### In scope
- `Runtime/Catalog/ContentDefinitionFormModel.cs` (form state + validation for all 7 types, codec load/save).
- `Runtime/Catalog/TargetRuleEditor.cs` (the one shared `ContentTargetRule` widget; `EnumChoices` helpers).
- `Runtime/Catalog/ContentCatalogPresenter.cs` (screen), wired into the Catalog drawer in `TrialScreenPresenter`.
- Tests: `ContentCatalogPresenterTests.cs`, shared fixture `GameTestHost.cs`.

### Out of scope
- AnatomyProfile/BodyPart/Perk/Action/Mechanic/Attribute/Resource/NpcTemplateData editors (not in the six+Skill list).
- Backend — untouched.

## 6. Decisions

- **Form rules mirror Domain preconditions** (e.g. stackable ⇒ max stack ≥ 1, Armor ≥ 1 valid `BodyPartId`,
  Ammo ≥ 1 key, Ability trigger required, resource ids match the `ResourceDefinitionId` pattern, target max ≥ min,
  Effect duration value ≥ 1 only for ForRounds/ForTurns/ForDuration). Field errors are shown per field; the backend
  still decides.
- **Conditional fields**: MaxStackSize/MaxDurability/MaxCharges follow their toggles; CompatibleAmmoKeys only when
  AmmoRequirement ≠ None (and sent empty for None); DurationValue only for the three counted durations (sent null otherwise).
- **Readable publish failures**: on `publish_validation_failed` the presenter re-runs the validation service and
  lists each issue with a fixed sentence (`CatalogIssueText`). For Weapon+Required ammo an advisory hint appears
  while editing if no Ammo in the loaded catalog shares a key (client hint only, not authoritative).
- **References** (Item built-in abilities/effects, Ammo effect contributions) are picked from Published
  definitions of the right type and pin that exact version.
- **Tags / ruleset compatibility** are editable at creation only (backend limit, §4); ruleset defaults to the
  campaign's active ruleset (`rulesetId@version`) so a fresh draft can publish.
- **Role**: non-MainGM sees an info banner "Only the MainGM can author…", only Published rows, read-only forms;
  the services still enforce MainGM from stored membership.
- **State**: every mutation uses a new CommandId/CorrelationId and the record's current Revision; after success the
  list reloads and the editor reopens the server's record. A `StateChanged` failure reloads before reporting.
- **Irreversible actions** (archive, delete draft) use `OdyConfirmDialog`.

## 7. Expected behavior

See §6; list filterable by type and status; list rows show name, type, version, revision and a status badge.

## 9. Acceptance criteria

1. Design system applied (`ody-*` classes only). ✔
2. All 7 types in the type filter with full forms, conditional fields and full lifecycle. ✔
3. Readable publish validation errors per type. ✔
4. Tests per type. ✔ (pending owner run)

## 10. Tests and validation

| ID | Test |
|---|---|
| `TC-CATALOGUI-001` | `SupportedTypes_AreTheSevenTypedDefinitions_EachWithItsOwnFormSection` |
| `TC-CATALOGUI-002` | `Item_FullLifecycle_CreateSavePublishNewVersionArchiveDelete` |
| `TC-CATALOGUI-003` | `Weapon_AmmoKeysVisibleOnlyWithAmmo_RequiredWithoutAmmoExplainsWhyPublishFails` |
| `TC-CATALOGUI-004` | `Armor_ValidatesBodyPartIds_AndPublishes` |
| `TC-CATALOGUI-005` | `Ammo_RequiresAKey_AndReferencesPublishedEffects` |
| `TC-CATALOGUI-006` | `Ability_ResourceCostsAndTargetRule_ValidateAndRoundTrip` |
| `TC-CATALOGUI-007` | `Effect_DurationValueOnlyForCountedDurations_AndPublishes` |
| `TC-CATALOGUI-008` | `Skill_EnvelopeOnly_Publishes` |
| `TC-CATALOGUI-009` | `NonMainGm_SeesExplanationAndPublishedOnly_CannotAuthor` |
| `TC-CATALOGUI-010` | `Filters_NarrowByTypeAndStatus` |
| `TC-CATALOGUI-011` | `StaleRevision_IsRejected_AndThePresenterResyncsToTheServerState` |
| `TC-CATALOGUI-012` | `TargetRuleEditor_EditsTheSharedModel_AndDescribesRules` |

**Not run in this session** (no Unity/pwsh/.NET in the container; owner validates locally).

Manual validation (owner): Catalog drawer as MainGM → create one of each type, publish; Weapon with Required ammo
before/after creating an Ammo; switch to Player → banner + read-only.

## 11–13. Compatibility, dependencies, security

No contract or persistence change; no dependency; no hidden data (catalog rows are campaign content).

## 17. Completion evidence

`git diff --name-status main -- Packages DotNet` empty. Validation not run in container.

## 18. Blockers, decisions, and change control

- Backend limit recorded: tags/ruleset compatibility not updatable after creation (no backend change made).
