# ODY-S05-306 — Equipment Runtime Integration Fixtures

**Status:** In Review
**Roadmap stage / slice:** SLICE-05 (Equipment runtime block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-306-equipment-runtime-integration-fixtures`
**Pull request:** [#128](https://github.com/odyssey-services/Odyssey_VTT/pull/128)
**ExecPlan:** `docs/plans/active/ODY-S05-306_Equipment_Runtime_Integration_Fixtures.md` (Brief plan)
**Created:** 2026-09-11
**Last updated:** 2026-09-11 UTC

## 1. Goal

Add end-to-end integration fixtures proving `ODY-S05-301`–`305` work together as a single block: a Published Armor/Weapon definition becomes a runtime `ItemInstance`, `EquipmentService.Equip` succeeds and blocks `RemoveBodyPart` on the referenced body part, `EquipmentService.Unequip` succeeds, and `RemoveBodyPart` then succeeds — all through already-accepted public services, with no new production code. This mirrors `ODY-S05-207` for the Inventory runtime block: a small technical composition proof, not a new feature.

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-S05-301`–`305` each have isolated unit tests for their own piece, but nothing exercises the whole `catalog → create → Equip → RemoveBodyPart-blocked → Unequip → RemoveBodyPart-succeeds` path together, across module boundaries, the way a real MainGM session would.
- Value or risk reduction: `ODY-S05-305`'s own unit tests construct `EquippedEntry` via a test-only `EquipDirectly` helper that bypasses `EquipmentService.Equip`'s MainGM gate and calls the repository primitive directly — this proved the query/wiring logic in isolation, but never proved the real, fully-gated `EquipmentService.Equip`→`EquippedEntry`→`InventoryBodyPartRemovalDependencyChecker`→`RemoveBodyPart` chain end-to-end. This task closes that gap.
- Blocking or enabling relationship: this is the last task in the Equipment runtime block (`ODY-S05-301`–`306`); it depends on `ODY-S05-301`–`305` and blocks nothing further within this revision.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/TASK_TEMPLATE.md`
- `docs/tasks/completed/ODY-S05-207_Inventory_Runtime_Integration_Fixtures.md` — the structural template this task follows near one-to-one.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 6 (`ODY-S05-306`), §12.1 (task boundary — composes existing surfaces, no new production behavior unless a fixture hook was reserved, none was).
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md` §7 (Equipment model, all 6 rules).

### Requirement and test IDs

- Requirement IDs: `ODY-S05-306`, `SLICE-05`.
- Existing test IDs: `TC-INVENTORY-001`–`152` (re-verified unmodified against the `main` base this fixture composes).
- New test IDs introduced: `TC-INVENTORY-153`–`156`.

### Task-safe private context

- Approved summary / references: the user-provided `ODY-S05-306` task brief only. Synthetic fixture content only; no hidden campaign content, secrets, or private paths.

## 4. Verified current state

### Verified facts

- `git fetch origin` + `git log --oneline origin/main` confirmed `origin/main` is at `83c08d4` (PR #127, `ODY-S05-305`), with `ODY-S05-301`–`305` all merged — unlike `ODY-S05-207`, no local stacking of unmerged sibling PRs is needed.
- The `ODY-S05-306` name is absent from `docs/tasks/active/`, `docs/plans/active/`, and the working copy — this contract and its plan are authored from scratch.
- No production fixture hook is reserved for this task anywhere in `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12.1 or `ODY-S05-301`–`305` — confirmed by direct code read; a repository-wide search for "306" outside `docs/tasks/**`/`docs/plans/**` found nothing. This task therefore adds only a test file and test metadata.
- The public surfaces composed are all already present and individually tested: `ContentCatalogAuthoringService.CreateDraftDefinition`, `ContentCatalogLifecycleService.PublishDefinition`, `TypedDefinitionCodec.EncodeArmor`/`EncodeWeapon`, `InventoryCreationService.CreateItemInstanceFromDefinition`, `EquipmentService.Equip`/`Unequip`, `SqliteCharacterRepository.RemoveBodyPart` with `bodyPartRemovalDependencyCheckers`, `InventoryBodyPartRemovalDependencyChecker`.
- `InventoryCreationService.IsInstanceDefinitionType` (direct code read) already allows `ContentDefinitionType.Item`, `.Weapon`, and `.Armor` — `CreateItemInstanceFromDefinition` needs no change to accept a Published Armor/Weapon definition.
- `ArmorDefinition`'s constructor (direct code read) requires a non-empty `CoveredBodyPartIds` list of valid `BodyPartId` values and a non-negative `protection`; `CatalogValidationService.ValidateArmor` only re-decodes the properties (no Ruleset-wide anatomy-profile check exists to reject an arbitrary `BodyPartId` like `"Torso"`).
- `EquipmentService.Equip`'s `BodyPartRefs` parameter is independent of `ArmorDefinition.CoveredBodyPartIds` — `EquipmentService.Equip` only checks that the referenced `BodyPartId` exists on the Character's current `CharacterAnatomy` (rule 4), not that it matches what the Armor definition claims to cover. The fixture uses the same body part for both for narrative coherence, but this is not enforced by any production code.
- `ADR-027` §7 rule 6 and `EquipmentService.cs`'s own doc-comment confirm no mechanical effect (protection, damage) is evaluated by Equip/Unequip — the fixture must not assert anything about "protection" or "damage," only placement/removal facts.
- `BodyPartRemovalDependencyCheckerTests.cs`'s `CreateInitializedCharacter` helper and the default humanoid fixture (`Head`, `Torso`, `LeftArm`, `RightArm`) are the direct precedent to reuse for constructing a Character with real anatomy.

### Assumptions

- None.

## 5. Scope

### In scope

- `DotNet/Tests/Odyssey.Tests.Persistence/Integration/EquipmentRuntimeIntegrationFixtureTests.cs` (new): private helper methods on the test class (not a new production type) plus real, SQLite-backed end-to-end tests `TC-INVENTORY-153`–`156`.
- `Tests/Metadata/test-catalog.json`: new entries.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`: row 6 (`ODY-S05-306`) status update with PR link.
- This task contract and its Brief plan.

### Out of scope

- Any new production code under `Packages/com.odyssey.*` (verified no fixture hook is reserved).
- Weapon/armor mechanical effects (protection, damage, attack pipeline), `ActiveEffect`, item use, `ItemDefinition` migration — none implemented; none tested as if present.
- New `ErrorCode` — every failure this task's tests assert already exists (`CharacterBodyPartHasDependent`).
- New persistence table or column.
- Any change to `EquipItemCore<T>`/`UnequipItemCore<T>`/`HasAnyEquippedEntryReferencingBodyPart`/`IBodyPartRemovalDependencyChecker` or anything else `ODY-S05-301`–`305` already implemented — used, not modified.
- Unity/UI; any `docs/adr/**` edit; any edit to the `ODY-S05-301`–`305` contracts/plans.

### Allowed paths

```text
DotNet/Tests/Odyssey.Tests.Persistence/Integration/EquipmentRuntimeIntegrationFixtureTests.cs
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-306_Equipment_Runtime_Integration_Fixtures.md
docs/plans/active/ODY-S05-306_Equipment_Runtime_Integration_Fixtures.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Packages/com.odyssey.**   (no fixture hook reserved — none may be created)
Assets/** and Unity-side
docs/tasks/active/ODY-S05-301_* through ODY-S05-305_*
```

## 6. Technical constraints

- Module ownership and dependency direction: test-only, in the existing `Odyssey.Tests.Persistence` project, which already references every layer it needs. No production project is touched (`ADR-001`).
- Authoritative-state and transaction boundary: not applicable — this task composes existing already-transactional service/repository methods; it introduces no new mutation path.
- Serialization / compatibility boundary: no new persisted contract; the fixture definition is encoded exclusively through the already-versioned `TypedDefinitionCodec`.
- Time / RNG rule: not applicable.
- Unity / thread / lifetime rule: not applicable — pure .NET test code.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: not applicable — synthetic fixture content; existing MainGM authorization is exercised, not changed.
- Performance or platform constraint: not applicable.
- Other: Equip/Unequip go through `EquipmentService.Equip`/`Unequip` (MainGM-gated), never a direct `IInventoryRepository.EquipItem`/`UnequipItem` call — the whole point of this task is proving the gated path, not the isolated primitive `ODY-S05-303`/`304` already tested. The runtime item is created through `InventoryCreationService.CreateItemInstanceFromDefinition`, never a raw `CreateItemInstance` with a hand-built `ItemInstanceRecord`.

## 7. Expected behavior

### Scenario 1 — the block composes end-to-end with Armor (`TC-INVENTORY-153`)

**Given** a Published Armor definition covering `Head`, a Character with initialized anatomy, and a `Contained` runtime `ItemInstance` created from that definition
**When** `EquipmentService.Equip` places it referencing `Head`, `RemoveBodyPart("Head")` is attempted, `EquipmentService.Unequip` removes it, and `RemoveBodyPart("Head")` is attempted again
**Then** Equip succeeds; the first `RemoveBodyPart` fails with `CharacterBodyPartHasDependent` and `Head` still exists on the Character; Unequip succeeds; the second `RemoveBodyPart` succeeds and `Head` no longer exists on the Character. `Head` (not `Torso`) is used because `Torso` has `LeftArm`/`RightArm` attached to it in the default humanoid fixture and can therefore never be removed regardless of equipment state — using `Head` isolates the round trip to the equipment dependency alone.

### Scenario 2 — the path is not Armor-specific (`TC-INVENTORY-154`)

**Given** the same sequence, but with a Published Weapon definition (no ammo requirement) instead of Armor, referencing `RightArm`
**When** the same Equip→blocked-RemoveBodyPart→Unequip→RemoveBodyPart sequence runs
**Then** the same outcomes hold. `RightArm` has no internal Character-only dependent of its own (only `Torso` does), so it is also removable once unequipped.

### Scenario 3 — the block is scoped to the specific body part, through the full stack (`TC-INVENTORY-155`)

**Given** an item equipped referencing only `Head`
**When** `RemoveBodyPart` is attempted on an unrelated body part (`LeftArm`) while the item is still equipped
**Then** it succeeds, proving the checker's dependency scoping works through the real `EquipmentService.Equip`-created `EquippedEntry`, not only in `ODY-S05-305`'s own narrower unit tests.

### Scenario 4 — round-trip sanity: the item is not lost (`TC-INVENTORY-156`)

**Given** the full Equip→Unequip round trip from Scenario 1
**When** the item is re-read after Unequip
**Then** its `LocationRef` is `Contained` in its original Inventory and it is still physically present (not deleted).

### Required invariants

- No `ODY-S05-301`–`305` semantics are extended or reinterpreted — every assertion checks behavior those tasks already implement; this task only proves they compose.
- No `Packages/com.odyssey.*` production file, no `docs/adr/**` file, no Unity file is changed.
- No new `ErrorCode`, table, or column.
- No assertion about weapon/armor mechanical effects (protection, damage) — `ADR-027` §7 rule 6 keeps those on the item, not Equipment placement.

## 8. Deliverables

- Production code: None.
- Tests: `EquipmentRuntimeIntegrationFixtureTests.cs` — `TC-INVENTORY-153`–`156`.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (row 6), this task contract, its Brief plan.
- Generated evidence or build artifacts: validation command output in §17.
- Migration / recovery material: None — no schema change, no production code change.

## 9. Acceptance criteria

1. A Published Armor definition becomes a runtime `ItemInstance` via `InventoryCreationService.CreateItemInstanceFromDefinition` (`TC-INVENTORY-153`).
2. `EquipmentService.Equip` succeeds and places the item as `Equipped`, referencing the target body part (`TC-INVENTORY-153`).
3. `RemoveBodyPart` on the referenced body part is rejected with `CharacterBodyPartHasDependent` while equipped, with no state change (`TC-INVENTORY-153`).
4. `EquipmentService.Unequip` succeeds (`TC-INVENTORY-153`).
5. `RemoveBodyPart` on the same body part succeeds after Unequip, and the body part is genuinely absent from `CharacterAnatomy.BodyParts` afterward (`TC-INVENTORY-153`).
6. The same sequence succeeds with a Weapon definition, proving the path is not Armor-specific (`TC-INVENTORY-154`).
7. `RemoveBodyPart` on an unrelated body part succeeds while the referenced one remains equipped-blocked, through the full stack (`TC-INVENTORY-155`).
8. After the round trip, the item's `LocationRef` is `Contained` in its original Inventory and the item still physically exists (`TC-INVENTORY-156`).
9. New tests registered in `Tests/Metadata/test-catalog.json`; task contract and Brief plan added; backlog row 6 marked `In Review` with PR link.
10. `dotnet build`, `dotnet test`, `verify-format.ps1`, `check-repository-policy.ps1`, `verify-test-structure.ps1` all pass with real recorded output.
11. `git diff --name-status` against `main` shows only §5 allowed paths.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-INVENTORY-153` | .NET / NUnit (Persistence) | catalog → runtime item → Equip → blocked RemoveBodyPart → Unequip → successful RemoveBodyPart, with Armor | Pass |
| `TC-INVENTORY-154` | .NET / NUnit (Persistence) | the same sequence with a Weapon definition | Pass |
| `TC-INVENTORY-155` | .NET / NUnit (Persistence) | RemoveBodyPart on an unrelated body part succeeds while the referenced one is blocked, through the full stack | Pass |
| `TC-INVENTORY-156` | .NET / NUnit (Persistence) | round-trip sanity: item returns to Contained and is not lost | Pass |

### Required commands

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- `git diff --name-status` review confirming no `Packages/com.odyssey.*` production file, no `docs/adr/**` file, no Unity/UI file is touched.

### Required environments / profiles

- OS / architecture: Windows x64; CI runs the pure .NET solution.
- Unity editor or Player profile: Not applicable.
- Scripting backend: Not applicable.
- Network topology or database fixture: real temp-directory SQLite campaign database, the sibling `SLICE-05` test convention.
- Other: `dotnet` SDK per `global.json`.

### Validation not required by this task

- Unity / IL2CPP / PlayMode — no Unity code.
- Migration rehearsal — no schema change.
- Weapon/armor mechanical effects, `ActiveEffect`, attack composition — none implemented.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None — test-only change.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert the branch/PR.
- Data-loss risk and protection: None — no production code touched.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: None — synthetic fixture content only.
- Trust boundaries: Not applicable.
- Authorization / audience checks: Not applicable — existing MainGM-only authorization is exercised, not changed.
- Redaction requirements: Not applicable.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: Not applicable beyond the existing `ODY-S05-303`/`305` safe-rejection proofs this task reaches through the full stack.

## 14. Planning and execution mode

- Planning mode: Brief plan.
- Reason for selected mode: like `ODY-S05-207`, this task introduces no new public contract, no new persistence shape, and no new architecture — it composes already-accepted, already-tested public surfaces into an integration proof (`PLANS.md` §1's Brief-plan default; same precedent as `ODY-S05-207`/`106`).
- ExecPlan path: `docs/plans/active/ODY-S05-306_Equipment_Runtime_Integration_Fixtures.md` (Brief plan).
- Expected pull request count: 1 (Draft).
- Milestone or sequencing constraints: composes `ODY-S05-301`–`305`, all already merged — no PR stacking needed. Last task in the Equipment runtime block.

## 15. Documentation and versioning impact

- Documents that must change: `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (row 6), this task contract, its Brief plan.
- Documents that must not change: `docs/errors/ERROR_CODES.md` (no new `ErrorCode`), ADRs, `docs/tasks/active/ODY-S05-301_*` through `ODY-S05-305_*`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None.
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
- [x] Pull request explains changes, evidence, limitations, and follow-up work, and states explicitly that this closes the Equipment runtime block (`ODY-S05-301`–`306`).
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `DotNet/Tests/Odyssey.Tests.Persistence/Integration/EquipmentRuntimeIntegrationFixtureTests.cs` — new, 4 tests.
- `Tests/Metadata/test-catalog.json` — `TC-INVENTORY-153`–`156`.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` — row 6 → `In Review (PR #NNN)` (filled after PR opens).
- This task contract and its Brief plan.
- **Zero changes anywhere under `Packages/com.odyssey.*`** — confirmed by `git diff --stat`.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test DotNet\Odyssey.Core.sln` | Passed | Full suite passed: Contracts 1, Domain 80, Networking 67, Unit 136, Architecture 2, Persistence 489 (775 total, 0 failed). |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | `Repository policy check passed.` on first run (no new error codes). |
| `.\scripts\verify-test-structure.ps1` | Passed | Exit code 0; `TC-ARCH-001 PASS valid ADR-001 graph passes`. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| 1 | Passed | `TC-INVENTORY-153` (`InventoryCreationService.CreateItemInstanceFromDefinition`). |
| 2 | Passed | `TC-INVENTORY-153` (`EquipmentService.Equip` success + `BodyPartRefs` assertion). |
| 3 | Passed | `TC-INVENTORY-153` (`CharacterBodyPartHasDependent`, body part still present). |
| 4 | Passed | `TC-INVENTORY-153` (`EquipmentService.Unequip` success). |
| 5 | Passed | `TC-INVENTORY-153` (second `RemoveBodyPart` succeeds, body part genuinely absent). |
| 6 | Passed | `TC-INVENTORY-154` (Weapon variant). |
| 7 | Passed | `TC-INVENTORY-155` (unrelated body part removable while the referenced one stays blocked). |
| 8 | Passed | `TC-INVENTORY-156` (item `Contained`, still physically present after Unequip). |
| 9 | Passed | four `test-catalog.json` entries; this contract + Brief plan; backlog row 6 → `In Review (PR #NNN)`. |
| 10 | Passed | validation-results table above. |
| 11 | Passed | `git diff --name-status`: only the new fixture file, `test-catalog.json`, and this task's own docs. Zero `Packages/com.odyssey.*`, `docs/adr/**`, or Unity file changed. |

### Build and artifact evidence

- No new project, script, CI, configuration, table, column, or `ErrorCode`.

### Known limitations

- Weapon/armor mechanical effects (protection, damage) are not tested — none exist yet, per `ADR-027` §7 rule 6.
- Non-`Contained` Unequip destinations remain out of scope, as decided by `ODY-S05-304`.
- `ODY-S05-201`–`207`'s own task/plan files remain in `docs/tasks/active/`/`docs/plans/active/` despite `Done` backlog status — a pre-existing, unrelated documentation-sync gap, observed but not fixed here.

### Follow-up tasks

- None within the Equipment runtime block — this is its last task. Reserved future `SLICE-05` blocks (item-sourced abilities/effects runtime, full attack pipeline) remain named, not decomposed, per `SLICE-05_IMPLEMENTATION_BACKLOG.md` §8.

### Self-review summary

- Scope review: this task's own commits touch only the new fixture, `test-catalog.json`, backlog row 6, and this contract + plan. Zero `Packages/com.odyssey.*`, `docs/adr/**`, or Unity file — confirmed by direct `git diff --stat` read, not merely asserted.
- Architecture review: zero new public contracts; every step flows through an already-accepted `ODY-S05-301`–`305` public service exactly as a real MainGM session would — `EquipmentService.Equip`/`Unequip`, never a raw `IInventoryRepository.EquipItem`/`UnequipItem` call.
- Test review: 4 new tests, all passing; one real bug caught and fixed during authoring (using `EquippedEntry.Revision` instead of the item's own post-Equip revision for `UnequipRequest.ExpectedTargetRevision` — a test-authoring mistake, not a production defect); full-suite `dotnet test` and the three scripts green.
- Security/privacy review: not applicable — synthetic content, no new authorization surface.
- Documentation/version review: `test-catalog.json` + backlog row 6 updated; `ERROR_CODES.md` deliberately untouched; no ADR or app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None. Unlike `ODY-S05-207`, no PR stacking is needed.

### Decisions made during execution

- 2026-09-11 — File location: `DotNet/Tests/Odyssey.Tests.Persistence/Integration/EquipmentRuntimeIntegrationFixtureTests.cs`, in the existing `Integration/` folder, mirroring `InventoryRuntimeIntegrationFixtureTests.cs`'s own naming and location exactly. Authority: this ТЗ §1's own instruction to mirror `ODY-S05-207` structurally.
- 2026-09-11 — Decision: use Armor (not Weapon) as the primary fixture, per this ТЗ §2's own recommendation — no `AmmoRequirement`/ammo-catalog dependency, simpler to construct. A secondary Weapon-variant test (`TC-INVENTORY-154`) is included to prove the path is not Armor-specific, using `AmmoRequirement.None` and an empty `compatibleAmmoKeys` list to avoid pulling in the Ammo catalog.
- 2026-09-11 — Decision: the fixture's `EquipmentService.Equip` call references the same `BodyPartId` the Armor/Weapon definition's own coverage field names, purely for narrative coherence — confirmed by direct code read that `EquipmentService.Equip`'s rule-4 check does not compare against `ArmorDefinition.CoveredBodyPartIds`/`WeaponDefinition` at all; the two are independent. No assertion in this task's tests depends on that coincidence being enforced anywhere.
- 2026-09-11 — Decision: `CreateInitializedCharacter` and `CreateInventory` helpers are copied/adapted from `BodyPartRemovalDependencyCheckerTests.cs`/`ODY-S05-207`'s own precedent rather than invented fresh, keeping this fixture consistent with the established test-helper style across the block.
- 2026-09-11 — Discovery/decision: the fixture must equip/remove `Head`, not `Torso`, even though the ТЗ's own illustrative examples used `Torso`. The default humanoid anatomy fixture (`AnatomyInitializationRules.DefaultHumanoidBodyParts`) attaches `LeftArm`/`RightArm` to `Torso`, so `Torso` can never be removed regardless of equipment state (the pre-existing internal Character-only dependency check always blocks it) — using it would make the "RemoveBodyPart succeeds after Unequip" assertion untestable. `Head` has no internal dependent, so it isolates the round trip to the equipment dependency this task exists to prove. Authority: direct code read of `AnatomyInitializationRules.cs` during test authoring.

### Approved task changes

- None.

### Open questions for the product owner

- None.
