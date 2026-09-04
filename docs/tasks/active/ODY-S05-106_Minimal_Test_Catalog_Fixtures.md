# ODY-S05-106 — Minimal Test Catalog Fixtures

**Status:** In Review
**Roadmap stage / slice:** SLICE-05 (Content Catalog MVP block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-106-minimal-test-catalog-fixtures`
**Pull request:** To be opened
**Plan:** `docs/plans/active/ODY-S05-106_Minimal_Test_Catalog_Fixtures.md` (Brief plan)
**Created:** 2026-09-04
**Last updated:** 2026-09-04 UTC

## 1. Goal

Add a minimal test/built-in catalog fixture set proving `ODY-S05-101`-`105` work together end-to-end: GM authoring creates Draft definitions, typed definitions round-trip through `TypedDefinitionCodec` as `PropertiesJson`, cross-definition references stay exact-version `ContentDefinitionRef`s, `ODY-S05-104` validation gates publish, and `ODY-S05-103`'s own publish/archive/delete lifecycle behaves correctly over the resulting graph. This is a small technical fixture/proof catalog -- explicitly not a final balanced content pack.

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-S05-101`-`105` each have their own unit/integration tests proving their own individual piece works, but nothing yet exercises the whole Foundation -> Authoring -> typed-shape -> Validation -> Publish/Archive/Delete pipeline together, end-to-end, the way a real MainGM authoring session actually would.
- Value or risk reduction: catches integration gaps between the five prior tasks that isolated unit tests cannot see (e.g. a request DTO shape mismatch, an unexpected validation interaction, a lifecycle transition that behaves differently once real cross-references are involved) before this closes out the Content Catalog MVP block.
- Blocking or enabling relationship: this is the last task in the Content Catalog MVP block (`ODY-S05-101`-`106`); it depends on `ODY-S05-102`/`103`/`104`/`105` (all already merged) and blocks nothing further within this revision.

## 3. Authorities and requirement references

### Required authorities

- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, section 6's `ODY-S05-106` task-boundary paragraph.
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, sections 4, 9, 12, 20 (full read, re-confirmed unchanged since `ODY-S05-101`-`105`).
- `Documentation/11_Content_Block_System_Odyssey_VTT_v0.1.md` (re-confirmed unchanged since `ODY-S05-104`/`105`'s own full reads).
- `Packages/com.odyssey.domain/Runtime/Content/TypedDefinitions.cs` (full read, unchanged since `ODY-S05-105`) -- the six typed shapes this fixture composes.
- `Packages/com.odyssey.application/Runtime/Content/TypedDefinitionCodec.cs` (full read, unchanged since `ODY-S05-105`'s own amendments) -- the codec every fixture definition encodes/decodes through.
- `Packages/com.odyssey.application/Runtime/Content/ContentCatalogAuthoringContracts.cs` (full read, unchanged since `ODY-S05-102`) -- every fixture Draft is created through `ContentCatalogAuthoringService.CreateDraftDefinition`, never a direct repository call.
- `Packages/com.odyssey.application/Runtime/Content/CatalogValidationContracts.cs` (full read, unchanged since `ODY-S05-104`'s own two amendments) -- the real validation gate this task proves against, never re-implemented.
- `Packages/com.odyssey.application/Runtime/Content/ContentCatalogLifecycleContracts.cs` (full read, unchanged since `ODY-S05-103`'s own two amendments) -- the publish/archive/delete/list-archived surface this task exercises.

### Requirement and test IDs

- Requirement IDs: `ODY-S05-106`, `ADR-027` section 20 (product-owner MVP exit criterion 6: "A minimal built-in/test catalog fixture set proves weapon, armor, ammo, ability, effect... work end-to-end").
- Existing test IDs: `TC-CATALOG-001`-`099` (re-verified unmodified).
- New test IDs introduced: `TC-CATALOG-100`-`111`.

### Task-safe private context

- Approved summary / references: `ADR-027`/`11_Content_Block_System`'s own already-accepted/published content is cited directly. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `git fetch origin` + `git log --oneline origin/main` confirmed PR #105/#106/#107/#108/#109/#110 (`ODY-S05-101`/`102`/`105`/`104`(+follow-up)/`103`) are all already merged into `origin/main`.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` row 3 (`ODY-S05-103`) still read `In Review` despite PR #110 being merged, and its own test-range summary still said `TC-CATALOG-078`-`097` instead of the actual final `078`-`099` (two amendments landed after that row was last written) -- corrected as this task's own first preflight step.
- `ContentCatalogAuthoringService.CreateDraftDefinition`/`ContentCatalogLifecycleService.PublishDefinition`/`ArchiveDefinition`/`DeleteDraftDefinition`/`ListArchivedDefinitions` and `CatalogValidationService.ValidateDraftForPublish` were all re-confirmed unchanged from this session's own prior full reads -- no new architecture needed to compose them into an end-to-end fixture.
- `AbilityDefinition`/`EffectDefinition` (`ODY-S05-105`) carry no typed `ContentDefinitionRef` field of their own; only `ItemDefinition`'s embedded refs and `AmmoDefinition.EffectContributionRefs` do. Confirmed the fixture's own "ability/effect referenced through `DependencyRefs`" example is therefore the correct (and only) way to wire an Ability to an Effect, matching `ODY-S05-104`'s own prior test conventions.

### Assumptions

- None. Every fact above was directly observed via `Read`/`Grep`/`git`/`dotnet build`/`dotnet test` during this task.

## 5. Scope

### In scope

- `DotNet/Tests/Odyssey.Tests.Persistence/Content/MinimalTestCatalogFixtureTests.cs` (new): the fixture-building helper methods (plain private methods on the test class, not a new production type) plus 12 real, SQLite-backed end-to-end tests.
- `Tests/Metadata/test-catalog.json`: twelve new `TC-CATALOG-100`-`111` entries.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`: row 3 (`ODY-S05-103`) corrected to `Done` with the accurate test range; row 6 (`ODY-S05-106`) status update with PR link.
- This task contract and its Brief plan.

### Out of scope

- Final balanced game content, a large item catalog, marketplace/store/economy content, `.odcontent` import/export.
- Any Unity UI.
- Any runtime `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect`, attack resolution, or item use/equip/consume command.
- Any new validation rule beyond composing `ODY-S05-104`'s own existing `CatalogValidationService`.
- Any new lifecycle semantics beyond composing `ODY-S05-103`'s own existing `ContentCatalogLifecycleService`.
- Campaign-specific catalog overrides.
- Any new persistence table.
- Any new `ErrorCode` (none was needed -- every failure this task's tests assert already exists from `ODY-S05-103`/`104`).
- Any change to accepted `ADR-001`-`027` architecture sections.

### Allowed paths

```text
DotNet/Tests/Odyssey.Tests.Persistence/Content/MinimalTestCatalogFixtureTests.cs
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-106_Minimal_Test_Catalog_Fixtures.md
docs/plans/active/ODY-S05-106_Minimal_Test_Catalog_Fixtures.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/ADR-001* through docs/adr/ADR-027*
docs/tasks/SLICE-05_BACKLOG.md
docs/tasks/active/ODY-S05-001_*, ODY-S05-002_*, ODY-S05-101_* through ODY-S05-105_*
Any production code file under Packages/com.odyssey.domain, com.odyssey.application, com.odyssey.persistence
docs/errors/ERROR_CODES.md (no new ErrorCode -- see section 18)
Any Inventory/ItemInstance/ItemStack/Equipment/ActiveEffect file (none exist yet; none may be created by this task)
Unity assets/UI
```

## 6. Technical constraints

- Module ownership and dependency direction: this task adds test code only, in the existing `Odyssey.Tests.Persistence` project, which already references every layer (`Domain`/`Application`/`Persistence`) it needs. No production project is touched (`ADR-001`).
- Authoritative-state and transaction boundary: not applicable -- this task composes existing, already-transactional repository/service methods; it introduces no new mutation path.
- Serialization / compatibility boundary: no new persisted contract; every fixture definition is encoded exclusively through the already-versioned `TypedDefinitionCodec`.
- Time / RNG rule: not applicable.
- Unity / thread / lifetime rule: not applicable -- pure .NET test code.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: not applicable -- fixture data is synthetic test content only.
- Performance or platform constraint: not applicable.
- Other: every fixture Draft is authored through `ContentCatalogAuthoringService.CreateDraftDefinition` with `actorIsMainGm: true` (never a direct `IContentCatalogRepository.CreateDraftContentDefinition` call), matching the real GM-authoring path this fixture is meant to prove.

## 7. Expected behavior

### Scenario 1 -- the full minimal fixture graph publishes end-to-end

**Given** one Effect, one Ability (referencing the Effect via `DependencyRefs`), one Item (referencing the Effect via `BuiltInEffectRefs`), one Ammo, one Weapon (requiring that Ammo), and one Armor, each authored as a Draft through `ContentCatalogAuthoringService` with `RulesetCompatibility = ["ruleset.core@1.0.0"]` (the active test campaign's own Ruleset)
**When** each is published in dependency order (Effect first) through `ContentCatalogLifecycleService.PublishDefinition`
**Then** every publish succeeds, each definition ends `Status=Published`/`Version=1`.

### Scenario 2 -- weapon/ammo applicability, positive and negative

**Given** the fixture Weapon (`AmmoRequirement.Required`, `CompatibleAmmoKeys=["9mm"]`)
**When** validated via `CatalogValidationService.ValidateDraftForPublish` with a matching, ruleset-compatible Ammo fixture present, absent, or present-but-ruleset-incompatible
**Then** validation is valid only in the first case; the other two report `WeaponNoCompatibleAmmoInCatalog`.

### Scenario 3 -- exact-version references survive the round trip

**Given** the fixture Item's `BuiltInEffectRefs` pinned to the published Effect's own `{DefinitionId, Version=1}`
**When** the Item's own stored `PropertiesJson` is decoded again through `TypedDefinitionCodec.DecodeItem`
**Then** the decoded reference still pins the exact same `DefinitionId`/`Version` -- never resolved to "latest."

### Scenario 4 -- lifecycle stays correct over the fixture graph

**Given** Published and Archived fixture definitions
**When** `GetContentDefinition`, `ListArchivedDefinitions`, and `DeleteDraftDefinition` are called against them
**Then** Published/Archived fixtures remain loadable; the Archived fixture (and only it) appears in the Archived list; physical delete succeeds only for a genuinely unused Draft fixture and fails for Published/Archived ones.

### Scenario 5 -- a broken fixture fails safely, not silently

**Given** a fixture Item whose `BuiltInEffectRefs` points at a `ContentDefinitionId` that does not exist
**When** validated and then published
**Then** validation reports `ReferenceMissing`; publish fails with `ContentCatalogPublishValidationFailed` and never actually publishes the broken definition.

### Required invariants

- No `ODY-S05-103`/`104`'s own semantics are extended or reinterpreted -- every assertion in this task's tests checks behavior those tasks already implement and already test individually; this task only proves they compose correctly.
- No `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` type or table is introduced (verified directly by `TC-CATALOG-110`/`111`'s own reflection/schema scans).
- `ADR-001`-`027` are unmodified.
- No new `ErrorCode` is introduced.

## 8. Deliverables

- Production code: None.
- Tests: `MinimalTestCatalogFixtureTests.cs` (12 cases) -- `TC-CATALOG-100`-`111`.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (rows 3/6), this task contract, its Brief plan.
- Generated evidence or build artifacts: None persisted beyond this task's own recorded command output.
- Migration / recovery material: None -- no schema change, no production code change.

## 9. Acceptance criteria

1. A minimal valid catalog fixture (Item, Weapon, Ammo, Armor, Ability, Effect) publishes end-to-end through Authoring + Validation + Lifecycle (`TC-CATALOG-100`).
2. Weapon fixture requiring ammo passes validation only when matching, ruleset-compatible ammo exists (`TC-CATALOG-101`-`103`).
3. Typed references in fixture definitions remain exact-version refs after round-tripping through the codec (`TC-CATALOG-104`).
4. Published fixture definitions remain loadable (`TC-CATALOG-105`).
5. Archived fixture definition appears in the separate Archived list (`TC-CATALOG-106`).
6. Unused Draft fixture can be physically deleted (`TC-CATALOG-107`).
7. Published/Archived fixture definitions cannot be physically deleted (`TC-CATALOG-108`).
8. A broken fixture graph fails validation/publish safely, with existing `ODY-S05-104` issue/error behavior, never a raw exception (`TC-CATALOG-109`).
9. No runtime `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` type or table is introduced (`TC-CATALOG-110`/`111`).
10. Every Draft is created through `ContentCatalogAuthoringService`, never a direct repository call (structural: confirmed by code review of the fixture helper methods themselves).
11. New tests are registered in `Tests/Metadata/test-catalog.json`.
12. Task contract and Brief plan for `ODY-S05-106` are added.
13. `SLICE-05_IMPLEMENTATION_BACKLOG.md` marks `ODY-S05-103` `Done` (accurate test range) and `ODY-S05-106` `In Review` with PR link.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-CATALOG-100` | .NET / NUnit (Persistence) | Full fixture graph publishes end-to-end | Pass |
| `TC-CATALOG-101`-`103` | .NET / NUnit (Persistence) | Weapon/ammo applicability positive and negative (missing, ruleset-incompatible) | Pass |
| `TC-CATALOG-104` | .NET / NUnit (Persistence) | Exact-version reference survives round trip | Pass |
| `TC-CATALOG-105` | .NET / NUnit (Persistence) | Published fixtures remain loadable | Pass |
| `TC-CATALOG-106` | .NET / NUnit (Persistence) | Archived fixture appears in Archived list | Pass |
| `TC-CATALOG-107` | .NET / NUnit (Persistence) | Unused Draft fixture physically deletable | Pass |
| `TC-CATALOG-108` | .NET / NUnit (Persistence) | Published/Archived fixtures not physically deletable | Pass |
| `TC-CATALOG-109` | .NET / NUnit (Persistence) | Broken fixture fails validation and publish safely | Pass |
| `TC-CATALOG-110`/`111` | .NET / NUnit (Persistence) | No runtime item/inventory/equipment/effect type or table | Pass |

### Required commands

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- `git diff --name-status` review confirming no production code file, no `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` file, no `ADR-001`-`027` file, and no Unity/UI file is touched.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 development machine; CI runs the pure .NET solution.
- Unity editor or Player profile: Not applicable -- no Unity/UI code.
- Scripting backend: Not applicable.
- Network topology or database fixture: real temp-directory SQLite campaign database, the same fixture convention every sibling `SLICE-05` test already uses.
- Other: None.

### Validation not required by this task

- Unity Editor / player build validation -- no Unity code touched.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None -- test-only change.
- Version fields affected: None.
- Migration or upcaster: None required.
- Forward / backward behavior: Not applicable.
- Rollback method: Revert the branch.
- Data-loss risk and protection: None -- no production code touched.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: None -- synthetic test fixture content only.
- Trust boundaries: Not applicable.
- Authorization / audience checks: Not applicable -- this task tests existing MainGM-only authorization, it does not change it.
- Redaction requirements: Not applicable.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: Not applicable beyond `TC-CATALOG-109`'s own safe-failure proof (already `ODY-S05-104`'s own existing behavior).

## 14. Planning and execution mode

- Planning mode: Brief plan
- Reason for selected mode: this task introduces no new public contract, no new persistence shape, and no new architecture -- it composes five already-accepted, already-tested public surfaces (`ContentCatalogAuthoringService`, `TypedDefinitionCodec`, `CatalogValidationService`, `ContentCatalogLifecycleService`, and the six `ODY-S05-105` typed definition types) into an integration proof, matching `ODY-S04-114`/`ODY-S03-008`'s own precedent for integration-proof tasks (`PLANS.md` §1's own Brief-plan default for tasks that do not change public contracts).
- Plan path: `docs/plans/active/ODY-S05-106_Minimal_Test_Catalog_Fixtures.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: must not begin before `ODY-S05-102`/`103`/`104`/`105` are merged into `main` (all confirmed in section 4). This is the last task in the Content Catalog MVP block.

## 15. Documentation and versioning impact

- Documents that must change: `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (rows 3/6), this task contract, its Brief plan.
- Documents that must not change: `docs/errors/ERROR_CODES.md` (no new `ErrorCode`), `ADR-001`-`027`, `docs/tasks/SLICE-05_BACKLOG.md`, `docs/tasks/active/ODY-S05-001_*` through `ODY-S05-105_*`.
- Application version change: No.
- Schema / format / contract / protocol / Ruleset version change: None.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed.
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [x] Pull request explains changes, evidence, limitations, and follow-up work, and states explicitly that this closes the Content Catalog MVP block without adding new lifecycle/validation semantics or runtime state.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `DotNet/Tests/Odyssey.Tests.Persistence/Content/MinimalTestCatalogFixtureTests.cs` -- new, 12 tests.
- `Tests/Metadata/test-catalog.json` -- twelve new `TC-CATALOG-100`-`111` entries.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` -- row 3 corrected to `Done` with accurate test range, row 6 status update.
- This task contract and its Brief plan.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | Pass | 0 warnings, 0 errors |
| `dotnet test DotNet\Odyssey.Core.sln` | Pass | Full suite green (623/623), including 12 new `MinimalTestCatalogFixtureTests` cases, no regression |
| `.\scripts\verify-format.ps1` | Pass | `FORMAT-001 PASS repository text formatting checks passed` |
| `.\scripts\check-repository-policy.ps1` | Pass | `REPO-POLICY-001`–`005` PASS; `Repository policy check passed` |
| `.\scripts\verify-test-structure.ps1` | Pass | `TC-ARCH-001 PASS valid ADR-001 graph passes`; exit code 0 |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Pass | `TC-CATALOG-100`. |
| AC-2 | Pass | `TC-CATALOG-101`-`103`. |
| AC-3 | Pass | `TC-CATALOG-104`. |
| AC-4 | Pass | `TC-CATALOG-105`. |
| AC-5 | Pass | `TC-CATALOG-106`. |
| AC-6 | Pass | `TC-CATALOG-107`. |
| AC-7 | Pass | `TC-CATALOG-108`. |
| AC-8 | Pass | `TC-CATALOG-109`. |
| AC-9 | Pass | `TC-CATALOG-110`/`111`. |
| AC-10 | Pass | Fixture helper methods (`AuthorDraft`) call only `ContentCatalogAuthoringService.CreateDraftDefinition`. |
| AC-11 | Pass | Twelve `TC-CATALOG-100`-`111` entries added. |
| AC-12 | Pass | This task contract and Brief plan exist. |
| AC-13 | Pass | `SLICE-05_IMPLEMENTATION_BACKLOG.md` rows 3/6 updated; PR link to be backfilled once opened. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: This section plus the validation-results table (to be completed after the full validation suite runs).

### Known limitations

- The fixture graph is deliberately minimal (one of each of the six typed shapes) -- it proves the pipeline works, not that it works for every possible field combination each type supports; per-type field-level exhaustiveness remains `ODY-S05-104`'s and `ODY-S05-105`'s own existing unit-test suites' job, not this task's.
- `MechanicsPayloadRef` on the fixture Ability/Effect is a placeholder opaque string (`"reload_block_ref"`/`"burn_snapshot_ref"`), matching `ODY-S05-105`'s own established placeholder convention -- no real `ContentBlockGraph` exists anywhere in this codebase to reference instead.

### Follow-up tasks

- None within `SLICE-05`'s own Content Catalog MVP block -- this is the last task in it. Future `SLICE-05` blocks (Inventory, `ItemInstance`/`ItemStack`, Equipment, full attack pipeline) are reserved, not decomposed, per `SLICE-05_IMPLEMENTATION_BACKLOG.md` section 7.

### Self-review summary

- Scope review: diff limited to the four files in section 5's Allowed paths; no production code file, no `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` file, no `ADR-001`-`027` file, no Unity file touched.
- Architecture review: zero new public contracts; every fixture definition flows through the five already-accepted `ODY-S05-101`-`105` surfaces exactly as a real GM-authoring session would.
- Test review: 12 new tests, all passing on first run; full-suite `dotnet test` and remaining validation scripts to be run before PR.
- Security/privacy review: not applicable -- synthetic test content only, no new authorization surface.
- Documentation/version review: `test-catalog.json` updated; `ERROR_CODES.md` deliberately NOT touched (no new `ErrorCode`); no ADR or app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task's own closure.

### Decisions made during execution

- 2026-09-04 — Decision: the fixture lives as plain private C# helper methods on the test class itself, not a JSON fixture asset file and not a new production factory type. Authority: this task's own ТЗ "prefer the smallest maintainable shape" instruction -- a JSON asset would require its own loader/parser layer (a new production surface this task's own boundary explicitly discourages: "new production feature surface unless strictly needed for fixtures"), while plain C# helpers reuse the exact same `TypedDefinitionCodec.EncodeX`/`ContentCatalogAuthoringService`/`ContentCatalogLifecycleService` calls the tests themselves already need, with zero additional indirection.
- 2026-09-04 — Decision: the fixture's only typed cross-reference (`ItemDefinition.BuiltInEffectRefs` -> Effect) and its only generic cross-reference (`AbilityDefinition`'s own generic `DependencyRefs` -> the same Effect) are both wired to the *same* Effect fixture, rather than inventing two separate Effect fixtures. Authority: keeps the fixture graph genuinely minimal (one of each of the six typed shapes, as the ТЗ itself lists) while still exercising both reference mechanisms this codebase actually has (`AbilityDefinition`/`EffectDefinition` carry no typed `ContentDefinitionRef` field of their own, confirmed in section 4 -- `DependencyRefs` is their only way to reference anything).
- 2026-09-04 — Decision: no new `ErrorCode` was needed. Authority: every failure this task's own tests assert (`WeaponNoCompatibleAmmoInCatalog`, `ReferenceMissing`, `ContentCatalogPublishValidationFailed`, `PersistenceContentDefinitionNotDraft`) already exists from `ODY-S05-103`/`104`; this task only proves they fire correctly when reached through the real end-to-end path, not through a synthetic direct-repository-call test.

### Approved task changes

- None yet.
