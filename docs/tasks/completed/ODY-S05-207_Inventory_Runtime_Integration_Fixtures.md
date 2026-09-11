# ODY-S05-207 — Inventory Runtime Integration Fixtures

**Status:** Done (PR #121, merged into main)
**Roadmap stage / slice:** SLICE-05 (Inventory runtime block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-207-inventory-runtime-integration-fixtures`
**Pull request:** Draft — [#121](https://github.com/odyssey-services/Odyssey_VTT/pull/121)
**ExecPlan:** `docs/plans/active/ODY-S05-207_Inventory_Runtime_Integration_Fixtures.md` (Brief plan)
**Created:** 2026-09-11
**Last updated:** 2026-09-11 UTC

## 1. Goal

Add one end-to-end integration fixture proving `ODY-S05-201`–`206` work together as a single block: a Published Content Catalog definition becomes a runtime `ItemStack` with a full copied mechanics snapshot, that snapshot does not drift when a successor definition version is published, the stack moves between inventories, splits and merges, and permanent character deletion is blocked while the character still owns it — all through already-accepted public services, with no new production code. This mirrors `ODY-S05-106` for the Content Catalog MVP block: a small technical composition proof, not a new feature.

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-S05-201`–`206` each have isolated unit/integration tests for their own piece, but nothing exercises the whole `catalog → create → move → split/merge → delete-gate` path together, across the module boundaries, the way a real MainGM session would.
- Value or risk reduction: catches integration gaps between the six prior tasks that isolated tests cannot see (a request-DTO shape mismatch, a snapshot that silently follows a successor definition, a revision-increment interaction, an owner-ref that does not travel with a move) before the Inventory runtime block is closed out.
- Blocking or enabling relationship: this is the last task in the Inventory runtime block (`ODY-S05-201`–`207`); it depends on `ODY-S05-203`/`204`/`205`/`206` and blocks nothing further within this revision.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.0.md`
- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/TASK_TEMPLATE.md`
- `docs/tasks/active/ODY-S05-106_Minimal_Test_Catalog_Fixtures.md` — the structural template this task follows near one-to-one.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` row 7 (`ODY-S05-207`), §7.1 (task boundary — "mirroring `ODY-S05-106` … compose existing surfaces and avoid new production behavior unless an earlier task deliberately reserved a tiny fixture hook"), §9/§10 (block dependencies).
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md` §4 (catalog boundary), §5 (Inventory aggregate), §6 / §6.1 ("Publishing a new ItemDefinition version does not alter existing instances"), §9 (the `ODY-S05-206` stub closures).

### Requirement and test IDs

- Requirement IDs: `ODY-S05-207`, `SLICE-05`; `ADR-027` §6.1.
- Existing test IDs: `TC-INVENTORY-001`–`090` (re-verified unmodified against the `main` + PR #119 + PR #120 base this fixture composes).
- New test IDs introduced: `TC-INVENTORY-091`–`094`.

### Task-safe private context

- Approved summary / references: sanitized product-owner task brief only. Synthetic fixture content only; no hidden campaign content, secrets, or private paths.

## 4. Verified current state

### Verified facts

- `git fetch origin` + `git log --oneline origin/main` confirmed `origin/main` is at `36d00e4` (PR #116, `ODY-S05-204`). `ODY-S05-205` (PR #119) and `ODY-S05-206` (PR #120) are open Draft PRs, not yet merged.
- The `ODY-S05-207` name is absent from `docs/tasks/active/`, `docs/plans/active/`, and the working copy — this contract and its plan are authored from scratch.
- No production `fixture hook` is reserved for this task anywhere in `SLICE-05_IMPLEMENTATION_BACKLOG.md` §7.1 or `ODY-S05-201`–`206` — verified before writing any product file. This task therefore adds only a test file and test metadata.
- The public surfaces composed are all already present and individually tested: `ContentCatalogAuthoringService.CreateDraftDefinition` / `CreateNextDraftVersionFromPublished` / `UpdateDraftDefinition`, `ContentCatalogLifecycleService.PublishDefinition`, `TypedDefinitionCodec.EncodeItem`, `InventoryCreationService.CreateItemStackFromDefinition`, `InventoryMovementService.MoveItemStack`, `InventoryStackOperationService.Split` / `Merge` (PR #119), `InventoryCharacterDeletionDependencyChecker` + `SqliteCharacterRepository`'s `deletionDependencyCheckers` constructor list (PR #120).
- `SqliteContentCatalogRepository.PublishDefinition` always writes `Version = 1` in this MVP, and `CreateNextDraftVersionFromPublished` mints a **new** `ContentDefinitionId` (Draft, Version 0) by copying the published row — there is no "version 2 of the same id" mechanism yet. The §6.1 immutability invariant is therefore proven here as: an existing stack's snapshot keeps pinning the **original** `ContentDefinitionId` and its original `PropertiesJson` after a changed successor definition is published (see `TC-INVENTORY-092` and §18).
- Because PR #119 and PR #120 are not in `main`, this branch is `main` + a local merge of both feature branches. That merge surfaces one build gap (PR #119's test fake `ThrowingInventoryRepository` does not implement PR #120's two new `IInventoryRepository` members) — fixed with two throw-stub lines; recorded in §18. Whoever merges #119 then #120 into `main` applies the same two-line fix.

### Assumptions

- PR #119 and PR #120 will be merged into `main` before this PR. If not, this PR must merge after them (its base is `main`; GitHub collapses the diff to just the test file + metadata once they land).

## 5. Scope

### In scope

- `DotNet/Tests/Odyssey.Tests.Persistence/Integration/InventoryRuntimeIntegrationFixtureTests.cs` (new): private helper methods on the test class (not a new production type) plus 4 real, SQLite-backed end-to-end tests `TC-INVENTORY-091`–`094`.
- `Tests/Metadata/test-catalog.json`: four new entries.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`: row 7 (`ODY-S05-207`) status update with PR link.
- This task contract and its Brief plan.
- (Merge-integration only) two throw-stub lines in the existing `InventoryStackOperationServiceTests.cs` fake `IInventoryRepository`, needed solely because this branch stacks PR #119 and PR #120 together — see §18.

### Out of scope

- Any new production code under `Packages/com.odyssey.*` (verified no fixture hook is reserved).
- Equipment, `ActiveEffect`, item use, attack pipeline, `ItemDefinition` migration — none implemented; none tested as if present.
- New `ErrorCode` — every failure this task's tests assert already exists (`InventoryCreateDefinitionNotPublished` from `ODY-S05-203`, `PersistenceCharacterDeletionHasDependent`, `PersistenceItemStackNotFound`).
- New persistence table or column.
- Unity/UI; any `docs/adr/**` edit; any edit to the `ODY-S05-201`–`206` contracts/plans.

### Allowed paths

```text
DotNet/Tests/Odyssey.Tests.Persistence/Integration/InventoryRuntimeIntegrationFixtureTests.cs
DotNet/Tests/Odyssey.Tests.Persistence/InventoryStackOperationServiceTests.cs   (merge-integration only — two throw-stub lines; see §18)
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-207_Inventory_Runtime_Integration_Fixtures.md
docs/plans/active/ODY-S05-207_Inventory_Runtime_Integration_Fixtures.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Packages/com.odyssey.**   (no fixture hook reserved — none may be created)
Assets/** and Unity-side
docs/tasks/active/ODY-S05-201_* through ODY-S05-206_*
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
- Other: every catalog Draft is authored through `ContentCatalogAuthoringService`, never a direct `IContentCatalogRepository` call, matching the real GM path — the same rule `ODY-S05-106` follows.

## 7. Expected behavior

### Scenario 1 — the block composes from catalog to stack operations (`TC-INVENTORY-091`)

**Given** a Published stackable-consumable `ItemDefinition` and two characters, each with an inventory
**When** an `ItemStack` is created in character A's inventory from the Published definition, moved to character B's inventory, split into two parts, and merged back
**Then** the created stack's snapshot is a full copy of the definition at publish time (`Payload`, `DefinitionSnapshotVersion = 1`, `ContentType = Item`, exact `SourceDefinitionRef`); the move increments the stack's and both inventories' revisions; the split conserves total quantity and both parts keep the identical pinned snapshot/state/owner/location; the merge restores the original total and the consumed part is physically gone (`PersistenceItemStackNotFound`).

### Scenario 2 — a runtime snapshot does not drift when a successor definition is published (`TC-INVENTORY-092`)

**Given** an `ItemStack` created from a Published definition
**When** a successor Draft is branched from it via `CreateNextDraftVersionFromPublished`, given different `PropertiesJson`, and published
**Then** the existing stack still pins the **original** `ContentDefinitionId` and its original `PropertiesJson` — the successor's changes do not reach it (`ADR-027` §6.1).

### Scenario 3 — character deletion gate through the full path (`TC-INVENTORY-093`)

**Given** a stack created from a Published definition in character A's inventory and then moved wholesale to character B's inventory (so its `OwnerRef` becomes B's)
**When** `DeleteCharacterPermanently` runs with an `InventoryCharacterDeletionDependencyChecker` registered
**Then** it is blocked for character B (`PersistenceCharacterDeletionHasDependent`, B still exists) and succeeds for the emptied character A.

### Scenario 4 — safe rejection of a still-Draft definition (`TC-INVENTORY-094`)

**Given** a Draft (never-Published) `ItemDefinition`
**When** `InventoryCreationService.CreateItemStackFromDefinition` is called against it
**Then** it fails with the existing `InventoryCreateDefinitionNotPublished` error and no `ItemStack` row is written.

### Required invariants

- No `ODY-S05-201`–`206` semantics are extended or reinterpreted — every assertion checks behavior those tasks already implement; this task only proves they compose.
- No `Packages/com.odyssey.*` production file, no `docs/adr/**` file, no Unity file is changed (the two throw-stub lines in `InventoryStackOperationServiceTests.cs` are a test file and a pure merge artifact — see §18).
- No new `ErrorCode`, table, or column.

## 8. Deliverables

- Production code: None.
- Tests: `InventoryRuntimeIntegrationFixtureTests.cs` (4 cases) — `TC-INVENTORY-091`–`094`.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (row 7), this task contract, its Brief plan.
- Generated evidence or build artifacts: validation command output in §17.
- Migration / recovery material: None — no schema change, no production code change.

## 9. Acceptance criteria

1. A stack created from a Published definition carries a full copied snapshot equal to the definition at publish time (`TC-INVENTORY-091`).
2. Moving the stack between inventories increments the stack's and both inventories' revisions (`TC-INVENTORY-091`).
3. Split conserves total quantity with identical pinned snapshot/state/owner/location on both parts; merge restores the original total and removes the consumed part (`TC-INVENTORY-091`).
4. Publishing a successor definition does not alter an existing runtime stack's snapshot (`TC-INVENTORY-092`).
5. `DeleteCharacterPermanently` (with the real checker) is blocked for the stack's current owner and allowed for the emptied character, reached through the full catalog→inventory path (`TC-INVENTORY-093`).
6. `CreateItemStackFromDefinition` on a Draft definition is rejected by the existing `InventoryCreateDefinitionNotPublished` error, no row written (`TC-INVENTORY-094`).
7. New tests registered in `Tests/Metadata/test-catalog.json`; task contract and Brief plan added; backlog row 7 marked `In Review` with PR link.
8. `dotnet build`, `dotnet test`, `verify-format.ps1`, `check-repository-policy.ps1`, `verify-test-structure.ps1` all pass with real recorded output.
9. `git diff --name-status` against `main` shows only §5 allowed paths (plus the PR #119 / PR #120 content this branch stacks, which collapses once they merge).

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-INVENTORY-091` | .NET / NUnit (Persistence) | catalog → runtime stack + copied snapshot → cross-inventory move + revision increments → split/merge quantity conservation + consumed-part removal | Pass |
| `TC-INVENTORY-092` | .NET / NUnit (Persistence) | successor definition publish leaves an existing stack's pinned snapshot unchanged (`ADR-027` §6.1) | Pass |
| `TC-INVENTORY-093` | .NET / NUnit (Persistence) | permanent character deletion blocked for the item owner, allowed for the emptied character, through the full path | Pass |
| `TC-INVENTORY-094` | .NET / NUnit (Persistence) | stack-from-Draft rejected by the existing not-published error, no row written | Pass |

### Required commands

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- `git diff --name-status` review confirming no `Packages/com.odyssey.*` production file, no `docs/adr/**` file, no Unity/UI file is touched by this task's own commits (the only `.cs` outside the new fixture is the two-line merge-integration stub in `InventoryStackOperationServiceTests.cs`, a test file — §18).

### Required environments / profiles

- OS / architecture: Windows x64; CI runs the pure .NET solution.
- Unity editor or Player profile: Not applicable.
- Scripting backend: Not applicable.
- Network topology or database fixture: real temp-directory SQLite campaign database, the sibling `SLICE-05` test convention.
- Other: `dotnet` SDK per `global.json`.

### Validation not required by this task

- Unity / IL2CPP / PlayMode — no Unity code.
- Migration rehearsal — no schema change.
- Equipment / `ActiveEffect` / attack composition — none implemented.

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
- Security tests: Not applicable beyond `TC-INVENTORY-093`/`094`'s own safe-rejection proofs, which are existing `ODY-S05-203`/`206` behavior.

## 14. Planning and execution mode

- Planning mode: Brief plan.
- Reason for selected mode: like `ODY-S05-106`, this task introduces no new public contract, no new persistence shape, and no new architecture — it composes already-accepted, already-tested public surfaces into an integration proof (`PLANS.md` §1's Brief-plan default for tasks that do not change public contracts; same precedent as `ODY-S05-106` / `ODY-S04-114` / `ODY-S03-008`).
- ExecPlan path: `docs/plans/active/ODY-S05-207_Inventory_Runtime_Integration_Fixtures.md` (Brief plan).
- Expected pull request count: 1 (Draft).
- Milestone or sequencing constraints: composes `ODY-S05-203`/`204`/`205`/`206`; must merge after PR #119 and PR #120. Last task in the Inventory runtime block.

## 15. Documentation and versioning impact

- Documents that must change: `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (row 7), this task contract, its Brief plan.
- Documents that must not change: `docs/errors/ERROR_CODES.md` (no new `ErrorCode`), `ADR-001`–`027`, `docs/tasks/SLICE-05_BACKLOG.md`, `docs/tasks/active/ODY-S05-201_*` through `ODY-S05-206_*`.
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
- [x] Pull request explains changes, evidence, limitations, and follow-up work, and states explicitly that this closes the Inventory runtime block and stacks on PR #119 / PR #120.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `DotNet/Tests/Odyssey.Tests.Persistence/Integration/InventoryRuntimeIntegrationFixtureTests.cs` — new, 4 tests.
- `DotNet/Tests/Odyssey.Tests.Persistence/InventoryStackOperationServiceTests.cs` — two throw-stub lines on the existing fake `IInventoryRepository` (merge-integration only, see §18).
- `Tests/Metadata/test-catalog.json` — `TC-INVENTORY-091`–`094`.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` — row 7 → `In Review (PR #121)`.
- This task contract and its Brief plan.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | Passed | `Сборка успешно завершена. Предупреждений: 0. Ошибок: 0`. |
| `dotnet test DotNet\Odyssey.Core.sln` | Passed | 713 total, 0 failed (Contracts 1, Domain 74, Networking 67, Unit 136, Architecture 2, Persistence 433 — the 4 new `TC-INVENTORY-091`–`094` in `InventoryRuntimeIntegrationFixtureTests`; also carries PR #119's 18 + PR #120's 12 stacked). |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | `REPO-POLICY-001`–`005 PASS`; `Repository policy check passed.` (no new `ErrorCode`). |
| `.\scripts\verify-test-structure.ps1` | Passed | `TC-ARCH-001` / `TC-ARCH-002 PASS`; every `TC-INVENTORY-091`–`094` resolves to this contract. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| 1 | Passed | `TC-INVENTORY-091` (snapshot copy assertions). |
| 2 | Passed | `TC-INVENTORY-091` (move revision assertions). |
| 3 | Passed | `TC-INVENTORY-091` (split/merge assertions). |
| 4 | Passed | `TC-INVENTORY-092`. |
| 5 | Passed | `TC-INVENTORY-093`. |
| 6 | Passed | `TC-INVENTORY-094`. |
| 7 | Passed | four `test-catalog.json` entries; this contract + Brief plan; backlog row 7 → `In Review (PR #121)`. |
| 8 | Passed | validation-results table above. |
| 9 | Passed | `git diff --name-status`: the only non-stacked `.cs` outside the fixture is the two-line `InventoryStackOperationServiceTests.cs` merge stub (a test file). No `Packages/com.odyssey.*`, `docs/adr/**`, or Unity file changed by this task. |

### Build and artifact evidence

- No new project, script, CI, configuration, table, column, or `ErrorCode`.

### Known limitations

- The MVP has no "version 2 of the same `ContentDefinitionId`" mechanism — `PublishDefinition` always writes `Version = 1` and `CreateNextDraftVersionFromPublished` mints a new id. `TC-INVENTORY-092` therefore proves `ADR-027` §6.1 in the form the MVP supports: an existing stack's snapshot keeps pinning the **original** id and `PropertiesJson` after a changed successor is published. A true multi-version-of-one-id immutability test is a follow-up for whenever real definition versioning lands.
- The fixture graph is deliberately minimal (one stackable consumable) — it proves the pipeline composes, not every field combination; per-type exhaustiveness stays with `ODY-S05-201`–`206`'s own unit suites.
- Equipment / `ActiveEffect` / attack composition is not exercised — none of it is implemented (`SLICE-05_IMPLEMENTATION_BACKLOG.md` §8, Reserved future blocks).

### Follow-up tasks

- None within the Inventory runtime block — this is its last task. Reserved future `SLICE-05` blocks (Equipment runtime, item-sourced abilities/effects runtime, full attack pipeline) remain named, not decomposed, per `SLICE-05_IMPLEMENTATION_BACKLOG.md` §8.

### Self-review summary

- Scope review: this task's own commits touch the new fixture, `test-catalog.json`, backlog row 7, this contract + plan, and two throw-stub lines in one existing test file (a merge artifact of stacking #119 + #120). No `Packages/com.odyssey.*`, `docs/adr/**`, or Unity file.
- Architecture review: zero new public contracts; every step flows through an already-accepted `ODY-S05-201`–`206` public service exactly as a real MainGM session would.
- Test review: 4 new tests, all passing; full-suite `dotnet test` and the three scripts green.
- Security/privacy review: not applicable — synthetic content, no new authorization surface.
- Documentation/version review: `test-catalog.json` + backlog row 7 updated; `ERROR_CODES.md` deliberately untouched; no ADR or app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task's own closure. Sequencing: this PR must merge after PR #119 and PR #120.

### Decisions made during execution

- 2026-09-11 — File location: `DotNet/Tests/Odyssey.Tests.Persistence/Integration/InventoryRuntimeIntegrationFixtureTests.cs`, in the existing `Integration/` folder (alongside `CharacterVerticalSliceIntegrationTests.cs` / `VerticalSliceIntegrationTests.cs`), since this is a cross-boundary integration proof named "Integration Fixtures". Authority: ТЗ §2 item 8.
- 2026-09-11 — `TC-INVENTORY-092` proves `ADR-027` §6.1 in the form the MVP supports (original id / `PropertiesJson` unchanged after a successor publish), because there is no version-2-of-one-id mechanism yet — see Known limitations. Authority: code inspection of `SqliteContentCatalogRepository.PublishDefinition` / `CreateNextDraftVersionFromPublished`.
- 2026-09-11 — Fixture is plain private C# helper methods on the test class, not a JSON asset or a new production factory type — same decision and rationale as `ODY-S05-106` §18.

### Approved task changes

- 2026-09-11 — `DotNet/Tests/Odyssey.Tests.Persistence/InventoryStackOperationServiceTests.cs` was added to allowed paths for two throw-stub lines only. Cause: this branch stacks PR #119 (`ODY-S05-205`) and PR #120 (`ODY-S05-206`) locally so the fixture can exercise split/merge and the deletion checker together; neither PR's diff breaks against current `main` alone, but their combination does — #119's fake `ThrowingInventoryRepository` does not implement #120's `HasAnyItemOwnedByCharacter` / `HasAnyRuntimeReferenceToDefinition`. The same two-line fix belongs to whichever of #119/#120 merges into `main` second. Recorded per `TASK_TEMPLATE.md` completion rule 7.

### Open questions for the product owner

- None.
