# ODY-S05-401 — Migration Preview Foundation

**Status:** Done (PR #130, merged into main)
**Roadmap stage / slice:** SLICE-05 (ItemDefinition migration block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-401-migration-preview-foundation`
**Pull request:** [odyssey-services/Odyssey_VTT#130](https://github.com/odyssey-services/Odyssey_VTT/pull/130) (Draft)
**ExecPlan:** `docs/plans/active/ODY-S05-401_Migration_Preview_Foundation.md`
**Created:** 2026-09-12
**Last updated:** 2026-09-12 UTC

## 1. Goal

Implement `ADR-027` §10 steps 1/2/4 (of nine) only: an `ItemDefinitionMigrationPreview` type and a pure builder that lists which `ItemInstance`/`ItemStack` records a candidate source→target `ItemDefinition` migration would affect, and their before/after snapshot values. No blocking-incompatibility computation (`ODY-S05-402`), no `ADR-012` backup call, no confirm/apply (`ODY-S05-403`).

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-S05-109` decomposed the `ItemDefinition` migration block but nothing yet exists to answer "what would this migration touch, and how." This task is that foundation.
- Value or risk reduction: gives `ODY-S05-402`/`403` a tested, self-contained preview to compute blocking rules against and confirm/apply from, without either of those tasks needing to invent record-discovery or snapshot-diffing logic themselves.
- Blocking or enabling relationship: unblocks `ODY-S05-402` (blocking-incompatibility rules) and, transitively, `ODY-S05-403`/`404`.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, §13 row 1, §13.1.
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, §6.2 (mechanically identical stacks), §10 (migration workflow steps 1/2/4), §14 (module ownership).
- Existing patterns: `Packages/com.odyssey.rules/Runtime/Character/RulesetMigrationRules.cs` + `SqliteCharacterRepository.ApplyCharacterRulesetMigration` (`ODY-S04-113`, form precedent only); `HasAnyRuntimeReferenceToDefinition`, `MergeItemStacks`, `InventoryCreationService.CopySnapshot` (all in `SqliteInventoryRepository.cs`/`InventoryCreationService.cs`).

### Requirement and test IDs

- Requirement IDs: `ODY-S05-401`, `SLICE-05`.
- Existing test IDs: `TC-INVENTORY-001`-`156` as predecessor evidence.
- New test IDs introduced: `TC-INVENTORY-157`-`168`.

### Task-safe private context

- Approved summary / references: the user-provided `ODY-S05-401` task brief only.

## 4. Verified current state

### Verified facts

- `git fetch origin` completed; `origin/main` contains merge commit `98ced31`, PR #129 (`ODY-S05-109`).
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §13 row 1 (`ODY-S05-401`) is `Proposed`, scoping this task to preview construction only (steps 1-4 of §10), explicitly excluding blocking-incompatibility computation, the `ADR-012` backup call, and any apply logic.
- Direct code read confirms: no version-lineage field (`PreviousVersionId`/`RootDefinitionId`) exists anywhere in `Odyssey.Domain.Content`/`Odyssey.Application.Content`; `ContentCatalogAuthoringService.CreateNextDraftVersionFromPublished` mints a structurally new `ContentDefinitionId` with no back-reference to the definition it copied from. There is no way to auto-resolve "the next version" of a definition — a caller must name both source and target explicitly.
- `HasAnyRuntimeReferenceToDefinition` (`SqliteInventoryRepository.cs`, direct code read) matches `SourceItemDefinitionRef LIKE EscapeLike(definitionId.ToString()) + "/%" ESCAPE '\\'` across `ItemInstance` and `ItemStack`, ignoring the pinned version — the exact prefix-match pattern this task's new list primitives must reuse, but it returns only `Result<bool>`, never the matching rows, and has no `List*` counterpart.
- `MergeItemStacks`'s own "mechanically identical" check (`SqliteInventoryRepository.cs` ~line 738, direct code read) compares `InventoryId` (both sides equal to the operation's own `InventoryId`), `SourceItemDefinitionRef`, `MechanicsSnapshot`, `StackState`, `OwnerRef`, and `LocationRef`. `ADR-027` §6.2's own prose definition of "mechanically identical" names only: same `ItemDefinitionId`+version+`DefinitionSnapshotVersion`, same stackability/quantity rules, no diverging per-unit state — it does not mention ownership or location. `MergeItemStacks`'s extra three fields exist because *merging* additionally requires the two stacks to already be in the same place, not because they are part of "mechanically identical" itself.
- `ADR-027` §14 (direct re-read) explicitly assigns "preview/confirm orchestration" to `Odyssey.Application`, not `Odyssey.Rules`. `RulesetMigrationRules` (the cited form precedent) sits in `Odyssey.Rules.Character` because `ADR-025` (a different ADR, Character/progression) assigns that differently, and `RulesetMigrationRules.BuildPlan` only ever consumes pure `Odyssey.Domain.Character` types (`AttributeValue`, `CharacterSkill`, `CharacterAbility`, `CharacterResource`), never an `Odyssey.Application` record. This task's builder must consume `ContentDefinitionRecord`/`ItemInstanceRecord`/`ItemStackRecord`, all three of which are themselves `Odyssey.Application` types (confirmed by direct code read) — and `ADR-001`'s module dependency direction does not allow `Odyssey.Rules` to depend on `Odyssey.Application`. The new preview type/builder must therefore live in `Odyssey.Application.Inventory`, citing `RulesetMigrationRules`'s *shape* only, not its literal module.
- `InventoryCreationService.CopySnapshot` (private, direct code read) builds a runtime snapshot as `new ItemMechanicsSnapshot(sourceRef, definition.Version, definition.DefinitionType, definition.PropertiesJson)`. The exact same public-constructor call, against the *target* definition instead, is this task's own "after" snapshot — no new `TypedDefinitionCodec` method is needed.
- `InventoryCreationServiceTests.cs`'s `InventoryCreationScope_DoesNotIntroduceLaterInventoryCapabilities` (direct code read) scans only types in namespace `Odyssey.Application.Inventory` for forbidden fragments including `"ItemDefinitionMigration"` — placing the new type there means this guard needs a narrow allow-list addition. `SqliteInventoryRepositoryTests.cs`'s equivalent type-name guard (direct code read) scans `typeof(SqliteInventoryRepository).Assembly.GetTypes()` — the whole `Odyssey.Persistence` assembly — which does not include the new `Odyssey.Application` types at all; this guard needs no change, confirmed by reading its exact scan scope, not assumed, and re-confirmed by a full-suite test run after implementation (§17).
- No existing scope guard anywhere in the tracked test suite scans the `Odyssey.Rules` assembly for forbidden fragments — a further confirmation (not the deciding reason) that the module-placement question in the point above had to be settled by `ADR-027` §14's own text, not by which placement happens to dodge an existing guard.

### Assumptions

- None.

## 5. Scope

### In scope

- `IInventoryRepository.ListItemInstancesBySourceDefinitionId`/`ListItemStacksBySourceDefinitionId` + `SqliteInventoryRepository` implementation (reusing the existing prefix-match `LIKE`/`EscapeLike` pattern and `ItemInstanceSelectColumns`/`ItemStackSelectColumns`/`ReadItemInstance`/`ReadItemStack`).
- New file `Packages/com.odyssey.application/Runtime/Inventory/ItemDefinitionMigrationRules.cs`: `ItemDefinitionMigrationPreview`, `ItemDefinitionMigrationAffectedInstance`, `ItemDefinitionMigrationAffectedStackGroup` (+ a stack-membership value type), `ItemDefinitionMigrationInventoryRevision`, and the static `ItemDefinitionMigrationRules` class (`BuildPreview`/`ComputePreviewRevision`).
- Updating `ThrowingInventoryRepository` (`InventoryStackOperationServiceTests.cs`) with the two new interface members.
- Narrowing `InventoryCreationServiceTests.cs`'s type-name guard with an allow-list addition.
- Persistence tests and test metadata `TC-INVENTORY-157`-`168`.
- Task contract, ExecPlan, and backlog status updates.

### Out of scope

- Blocking-incompatibility computation of any kind (`ODY-S05-402`'s own job) — this task only lists what would be affected, never whether the migration is safe to run.
- The `ADR-012` backup call, revision-guarded confirmation, atomic apply, or any state mutation (`ODY-S05-403`'s own job).
- Integration fixtures (`ODY-S05-404`'s own job).
- Any change to `EquipItemCore<T>`/`UnequipItemCore<T>`/`HasAnyEquippedEntryReferencingBodyPart`/any other Equipment code.
- Any change to `RulesetMigrationRules.cs`/`ApplyCharacterRulesetMigration` — read-only reference.
- ADR edits, Unity/UI.

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Persistence/InventoryRepositoryContracts.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteInventoryRepository.cs
Packages/com.odyssey.application/Runtime/Inventory/ItemDefinitionMigrationRules.cs
DotNet/Tests/Odyssey.Tests.Persistence/**
DotNet/Tests/Odyssey.Tests.Unit/Inventory/InventoryRuntimeRecordTests.cs (point scope-guard narrowing only, discovered mid-implementation; see §18)
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-401_Migration_Preview_Foundation.md
docs/plans/active/ODY-S05-401_Migration_Preview_Foundation.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Packages/com.odyssey.domain/**
Packages/com.odyssey.rules/**
Packages/com.odyssey.application/Runtime/Inventory/EquipmentService.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: the preview type/builder lives in `Odyssey.Application` per `ADR-027` §14's explicit "preview/confirm orchestration" assignment; Persistence owns the new read primitives (`ADR-001`).
- Authoritative-state and transaction boundary: no write path is introduced; the two new repository methods are plain reads, no transaction.
- Serialization / compatibility boundary: no new persisted contract; reuses the existing `ItemMechanicsSnapshot`/`ContentDefinitionRef` shapes unchanged.
- Time / RNG rule: no new time/randomness usage.
- Unity / thread / lifetime rule: no Unity files.
- Dependency / licensing rule: no new dependency (uses the existing `System.Security.Cryptography.SHA256`, already used by `RulesetMigrationRules.ComputePreviewHash`).
- Security / privacy / redaction rule: no hidden campaign data, private docs, raw exceptions, or secrets in tests/docs.
- Other: the builder takes only already-fetched records as parameters (no repository/I-O call inside it) — the same "pure calculation over caller-supplied data" shape `RulesetMigrationRules.BuildPlan` already uses.

## 7. Expected behavior

### Scenario 1 — preview lists an affected instance with before/after snapshots

**Given** a Published source definition, a Published target definition, and a `Contained` `ItemInstance` created from the source
**When** `ItemDefinitionMigrationRules.BuildPreview` is called with that instance in the affected list
**Then** the resulting preview's `AffectedInstances` contains one entry whose `BeforeSnapshot` equals the instance's own stored `MechanicsSnapshot` and whose `AfterSnapshot` equals a snapshot built from the target definition's current `Version`/`DefinitionType`/`PropertiesJson`.

### Scenario 2 — stack grouping by the narrow "mechanically identical" field set

**Given** several `ItemStack` records sharing `SourceItemDefinitionRef`+`MechanicsSnapshot`+`StackState` but differing `InventoryId`/`OwnerRef`/`LocationRef`
**When** the preview groups affected stacks
**Then** they appear in the same `ItemDefinitionMigrationAffectedStackGroup`, proving grouping ignores the fields `MergeItemStacks` additionally requires for its own spatial merge constraint.

### Scenario 3 — a mechanics-divergent stack does not join the group

**Given** two stacks with the same `SourceItemDefinitionRef` but a different `MechanicsSnapshot.Payload`
**When** the preview groups affected stacks
**Then** they appear in separate groups.

### Scenario 4 — list-by-`DefinitionId` finds records across versions and inventories

**Given** `ItemInstance`/`ItemStack` records created from two different Published versions of the same `ContentDefinitionId`, spread across more than one `Inventory` in the same campaign
**When** `ListItemInstancesBySourceDefinitionId`/`ListItemStacksBySourceDefinitionId` are called with that `ContentDefinitionId`
**Then** every matching record is returned regardless of pinned version or which `Inventory` it lives in, scoped only to the requested campaign.

### Scenario 5 — `PreviewRevision` is deterministic and sensitive to every field

**Given** two calls to `BuildPreview` with identical inputs
**When** their `PreviewRevision` values are compared
**Then** they are equal; changing any one input (affected records, expected revisions, source/target refs) changes the resulting `PreviewRevision`.

### Required invariants

- Source and target are always caller-supplied `ContentDefinitionRecord`s; no automatic version-resolution logic exists.
- Stack grouping never considers `InventoryId`/`OwnerRef`/`LocationRef`.
- `AffectedInventoryRevision` is always a list, one entry per distinct `InventoryId` referenced by any affected record — never a single number.
- The "after" snapshot is always a real, fully-reconstructed `ItemMechanicsSnapshot`, never a partial/textual placeholder.
- No blocking-incompatibility computation, `ADR-012` backup call, or apply logic exists anywhere in this task's code.

## 8. Deliverables

- Production code: `IInventoryRepository` extension + SQLite implementation, `ItemDefinitionMigrationRules.cs` (preview type + builder).
- Tests: persistence-level tests for preview construction, stack grouping (positive + negative), the two new list primitives, and `PreviewRevision` determinism/sensitivity.
- Scripts / CI: None.
- Configuration: None.
- Documentation: task contract, ExecPlan, backlog update, test metadata.
- Generated evidence or build artifacts: none persisted.
- Migration / recovery material: no migration runner step; no new table.

## 9. Acceptance criteria

1. `ItemDefinitionMigrationPreview` and its builder construct from two explicitly-supplied `ContentDefinitionRecord`s (source/target); no auto-resolution of "the next version" exists anywhere.
2. Affected `ItemStack` grouping uses only `SourceItemDefinitionRef`+`MechanicsSnapshot`+`StackState`; a difference in `InventoryId`/`OwnerRef`/`LocationRef` alone does not split a group, and a difference in `MechanicsSnapshot` always does.
3. `AffectedInventoryRevision` is represented as a list of `(InventoryId, Revision)` pairs, one per distinct `InventoryId` referenced by any affected record.
4. The "after" snapshot for every affected record is a real `ItemMechanicsSnapshot` built from the target definition's current `Version`/`DefinitionType`/`PropertiesJson` — the same construction `InventoryCreationService.CopySnapshot` already uses.
5. `ListItemInstancesBySourceDefinitionId`/`ListItemStacksBySourceDefinitionId` return every matching record for a `ContentDefinitionId` regardless of pinned version, scoped to one campaign, across every `Inventory` in it.
6. `PreviewRevision` is a deterministic digest: identical inputs produce identical values; any single differing input produces a different value.
7. No blocking-incompatibility computation, no `ADR-012` backup call, no apply/mutation logic exists anywhere in this task's diff.
8. `EquipItemCore<T>`/`UnequipItemCore<T>`/`HasAnyEquippedEntryReferencingBodyPart`/`RulesetMigrationRules.cs` are unchanged.
9. `Tests/Metadata/test-catalog.json` contains `TC-INVENTORY-157`-`168`.
10. Task contract, ExecPlan, and `SLICE-05_IMPLEMENTATION_BACKLOG.md` are updated.
11. Required validation commands pass and diff review confirms only allowed paths touched.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-INVENTORY-157` | .NET / NUnit (Persistence) | BuildPreview lists an affected ItemInstance with correct before/after snapshots | Pass |
| `TC-INVENTORY-158` | .NET / NUnit (Persistence) | BuildPreview lists an affected ItemStack with correct before/after snapshots | Pass |
| `TC-INVENTORY-159` | .NET / NUnit (Persistence) | Stacks sharing SourceItemDefinitionRef/MechanicsSnapshot/StackState group together despite differing InventoryId/OwnerRef/LocationRef | Pass |
| `TC-INVENTORY-160` | .NET / NUnit (Persistence) | Stacks with a divergent MechanicsSnapshot do not join the same group | Pass |
| `TC-INVENTORY-161` | .NET / NUnit (Persistence) | AffectedInventoryRevision lists one entry per distinct InventoryId referenced by affected records | Pass |
| `TC-INVENTORY-162` | .NET / NUnit (Persistence) | BuildPreview throws for an affected record whose InventoryId has no matching supplied InventoryRecord | Pass |
| `TC-INVENTORY-163` | .NET / NUnit (Persistence) | ListItemInstancesBySourceDefinitionId matches records created from two different Published versions of the same DefinitionId | Pass |
| `TC-INVENTORY-164` | .NET / NUnit (Persistence) | ListItemStacksBySourceDefinitionId matches across multiple Inventories in the same campaign | Pass |
| `TC-INVENTORY-165` | .NET / NUnit (Persistence) | List-by-DefinitionId methods are scoped to the requested campaign only | Pass |
| `TC-INVENTORY-166` | .NET / NUnit (Persistence) | PreviewRevision is identical for two BuildPreview calls with identical inputs | Pass |
| `TC-INVENTORY-167` | .NET / NUnit (Persistence) | PreviewRevision changes when an affected record's snapshot changes | Pass |
| `TC-INVENTORY-168` | .NET / NUnit (Persistence) | PreviewRevision changes when an expected inventory revision changes | Pass |

### Required commands

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- Review `git diff --name-status` and confirm no Unity files, ADR edits, or Equipment/`RulesetMigrationRules.cs` changes.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 development machine.
- Unity editor or Player profile: not applicable.
- Scripting backend: not applicable.
- Network topology or database fixture: local temp-directory campaign with real SQLite database.
- Other: pure .NET build/test path.

### Validation not required by this task

- Unity Editor/Player validation because no Unity files change.
- Blocking-incompatibility/confirm/apply/integration-fixture testing because those are `ODY-S05-402`/`403`/`404`'s own jobs.

## 11. Compatibility, migration, and rollback

- Compatibility impact: no schema change; adds two read-only query primitives.
- Version fields affected: no manifest/application/schema version is bumped in this task.
- Migration or upcaster: none.
- Forward / backward behavior: older builds will not call the new primitives; no data format change.
- Rollback method: revert this branch/PR before merge.
- Data-loss risk and protection: none; every new code path is read-only or pure computation.
- Recovery rehearsal required: no.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: synthetic catalog/inventory test records; no real player data.
- Trust boundaries: none new; this task adds read-only queries and a pure calculation with no caller-facing authorization surface of its own (that belongs to `ODY-S05-403`'s MainGM gate).
- Authorization / audience checks: not implemented here — no command exists yet to authorize.
- Redaction requirements: no diagnostics or network projections added.
- Log-safe fields: no logging added.
- Abuse / malformed input limits: constructors reject invalid IDs/refs/null payloads and a missing `InventoryRecord` for a referenced `InventoryId`.
- Security tests: not applicable beyond the fail-fast constructor validation tests.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`.
- Reason for selected mode: introduces a new public DTO and a new `IInventoryRepository` port method, and has open design questions requiring investigation before coding (`PLANS.md` §1.2).
- ExecPlan path: `docs/plans/active/ODY-S05-401_Migration_Preview_Foundation.md`.
- Expected pull request count: 1.
- Milestone or sequencing constraints: must follow merged PR #129 and precede `ODY-S05-402`.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, ExecPlan, `Tests/Metadata/test-catalog.json`, and `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`.
- Documents that must not change: accepted ADRs; `RulesetMigrationRules.cs`; Equipment code.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: adds two repository primitives and one new Application DTO; no manifest/schema version bump or protocol/ruleset change.
- Documentation version changes: none.
- Changelog or release-note requirement: none.

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
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

_Filled at the end of implementation._

### Changed files / areas

- `Packages/com.odyssey.application/Runtime/Persistence/InventoryRepositoryContracts.cs` — two new `IInventoryRepository` method declarations.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteInventoryRepository.cs` — their SQLite implementation.
- `Packages/com.odyssey.application/Runtime/Inventory/ItemDefinitionMigrationRules.cs` — new file: preview type, affected-record types, builder, `ComputePreviewRevision`.
- `DotNet/Tests/Odyssey.Tests.Persistence/ItemDefinitionMigrationRulesTests.cs` — new file, pure builder unit tests, `TC-INVENTORY-157`-`162`, `166`-`168`.
- `DotNet/Tests/Odyssey.Tests.Persistence/SqliteInventoryRepositoryTests.cs` — 3 new tests for the list primitives, `TC-INVENTORY-163`-`165`, plus 2 new helper methods.
- `DotNet/Tests/Odyssey.Tests.Persistence/InventoryStackOperationServiceTests.cs` — `ThrowingInventoryRepository` fake updated with the 2 new interface members.
- `DotNet/Tests/Odyssey.Tests.Persistence/InventoryCreationServiceTests.cs` — point allow-list addition to the existing type-name scope guard (new array `allowedItemDefinitionMigrationTypes`, filtered at the call site; the shared `forbidden` array is untouched).
- `DotNet/Tests/Odyssey.Tests.Unit/Inventory/InventoryRuntimeRecordTests.cs` — point allow-list narrowing of the filename-fragment scope guard (discovered mid-implementation; see §18).
- `Tests/Metadata/test-catalog.json` — `TC-INVENTORY-157`-`168` registered.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` — row `ODY-S05-401` updated to `In Review`.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | PASS | 0 warnings, 0 errors. |
| `dotnet test DotNet\Odyssey.Core.sln` | PASS | Contracts 1/1, Domain 80/80, Networking 67/67, Unit 136/136, Architecture 2/2, Persistence 501/501. |
| `.\scripts\verify-format.ps1` | PASS | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | PASS | `Repository policy check passed.` |
| `.\scripts\verify-test-structure.ps1` | PASS | Exit code 0. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 (explicit source/target, no auto-resolution) | Met | `ItemDefinitionMigrationRules.BuildPreview` takes only caller-supplied `ContentDefinitionRecord` params; no version-lookup logic anywhere in the file. |
| AC-2 (narrow stack grouping) | Met | `TC-INVENTORY-159`/`160`; `StackGroupKey` compares only `SourceItemDefinitionRef`+`MechanicsSnapshot`+`StackState`. |
| AC-3 (`AffectedInventoryRevisions` as a list) | Met | `TC-INVENTORY-161`; `ItemDefinitionMigrationInventoryRevision` list on `ItemDefinitionMigrationPreview`. |
| AC-4 (real "after" snapshot) | Met | `TC-INVENTORY-157`/`158`; `BuildAfterSnapshot` reuses `CopySnapshot`'s exact construction. |
| AC-5 (list-by-`DefinitionId`, version-agnostic, campaign-wide) | Met | `TC-INVENTORY-163`/`164`/`165`. |
| AC-6 (deterministic `PreviewRevision`) | Met | `TC-INVENTORY-166`/`167`/`168`. |
| AC-7 (no blocking/backup/apply logic) | Met | Confirmed by diff review — no such code exists anywhere in the changed files. |
| AC-8 (Equipment/`RulesetMigrationRules.cs` untouched) | Met | Not present in `git diff --stat`. |
| AC-9 (test metadata registered) | Met | `Tests/Metadata/test-catalog.json` updated. |
| AC-10 (docs updated) | Met | This contract, the ExecPlan, and the backlog row. |
| AC-11 (validation + scope) | Met | All 5 commands PASS (table above); diff reviewed against allowed/forbidden paths. |

### Build and artifact evidence

- No build artifacts are persisted beyond the standard `artifacts/bin/**` output already produced by `dotnet build`.

### Known limitations

- No blocking-incompatibility computation, backup call, or apply logic exists — `ODY-S05-402`/`403`'s own jobs.
- Whether `ODY-S05-403` re-validates `PreviewRevision` against a freshly-rebuilt preview or trusts the stored value is left to that task's own ExecPlan.

### Follow-up tasks

- `ODY-S05-402` — Migration Blocking Incompatibility Rules.

### Self-review summary

- Scope review: diff touches only allowed paths plus the one justified point scope-guard narrowing in `Odyssey.Tests.Unit` (§18); no Equipment, `RulesetMigrationRules.cs`, or ADR file touched.
- Architecture review: new type placed in `Odyssey.Application.Inventory` per `ADR-027` §14 and `ADR-001`'s dependency direction (§18); no new cross-module dependency introduced.
- Test review: `TC-INVENTORY-157`-`168` cover every required scenario from this ТЗ's §7, including both grouping positive/negative cases and hash determinism/sensitivity; full suite green.
- Security/privacy review: no new trust boundary, no logging, no secrets; fail-fast constructor validation on every new type.
- Documentation/version review: task contract, ExecPlan, and backlog updated; no schema/manifest/protocol version bump required.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-12 — **Design decision (Open Question 1, required by this ТЗ §2): source/target resolved only via caller-supplied `ContentDefinitionRecord`s, no automatic version-resolution.** No version-lineage field (`PreviousVersionId`/`RootDefinitionId`) exists anywhere in the codebase, and `CreateNextDraftVersionFromPublished` mints a structurally new `ContentDefinitionId` with no back-reference — there is nothing to automatically resolve "the next version" from. The caller (a future GM command in `ODY-S05-403`, or this task's own test harness) must name both sides explicitly. Authority: this ТЗ §2's own explicit direction; direct code search confirming no lineage mechanism exists.
- 2026-09-12 — **Design decision (Open Question 2, required by this ТЗ §3): stack grouping for the preview uses only `SourceItemDefinitionRef`+`MechanicsSnapshot`+`StackState`, not `MergeItemStacks`'s wider field set.** `ADR-027` §6.2's own prose definition of "mechanically identical" never mentions ownership or location; `MergeItemStacks`'s extra `InventoryId`/`OwnerRef`/`LocationRef` equality checks exist only because merging additionally requires the two stacks to already coexist in the same place — a *merge* precondition, not part of "mechanically identical" itself. A migration preview cares whether stacks *would end up* in the same mechanical state, not where they currently sit. Authority: this ТЗ §3's own explicit direction; direct comparison against `ADR-027` §6.2's own text and `MergeItemStacks`'s own code.
- 2026-09-12 — **Design decision (Open Question 3, required by this ТЗ §6 item 3): `AffectedInventoryRevision` is a list of `(InventoryId, Revision)` pairs, one per distinct `InventoryId` referenced by any affected record.** A campaign-wide `ItemDefinition` migration can realistically touch many Characters' inventories at once (this is exactly why the new list primitives are campaign-scoped, not inventory-scoped); a single number cannot express a multi-inventory CAS guard. The builder computes the distinct `InventoryId` set from the affected records and requires the caller to supply a matching `InventoryRecord` for each, the same "caller pre-fetches, builder only computes" shape `RulesetMigrationRules.BuildPlan` already uses for its own revision inputs. Authority: this ТЗ §6 item 3's own explicit direction.
- 2026-09-12 — **Design decision (Open Question 4, required by this ТЗ §6 item 4): the "after" snapshot reuses `InventoryCreationService.CopySnapshot`'s exact construction against the target definition — no new `TypedDefinitionCodec` method, no textual placeholder.** `ODY-S05-403`'s own confirm/apply step will need to write exactly this value to every affected record; computing anything less complete here (a partial diff, a field list) would just be redone in full there, and a preview showing an incomplete "after" value would mislead the reviewing GM. Authority: this ТЗ §6 item 4's own explicit direction; direct code read of `CopySnapshot`'s existing construction.
- 2026-09-12 — **Design decision (module placement, corrected during investigation, not merely assumed): the new preview type/builder lives in `Odyssey.Application.Inventory`, not `Odyssey.Rules` despite `RulesetMigrationRules` being the cited form precedent.** An initial instinct to mirror `RulesetMigrationRules`'s literal module (`Odyssey.Rules.Character`) was corrected after re-reading `ADR-027` §14, which explicitly assigns "preview/confirm orchestration" to `Odyssey.Application`, and after confirming this builder must consume `ContentDefinitionRecord`/`ItemInstanceRecord`/`ItemStackRecord` — all `Odyssey.Application` types `Odyssey.Rules` is not permitted to depend on under `ADR-001`'s module dependency direction. `RulesetMigrationRules` itself only ever consumes pure `Odyssey.Domain.Character` types, which is why its own module placement differs; the "cite by analogy, not by literal copy" caveat this ТЗ §5 already applied to field-shape also applies to module placement. Authority: `ADR-027` §14's own explicit text; `ADR-001`; direct code read confirming `RulesetMigrationRules.BuildPlan`'s parameter types are all `Odyssey.Domain`.
- 2026-09-12 — Decision: `SqliteInventoryRepositoryTests.cs`'s type-name scope guard needs no change. It scans only `typeof(SqliteInventoryRepository).Assembly.GetTypes()` (the `Odyssey.Persistence` assembly), and this task's new types live in `Odyssey.Application` — confirmed by direct read of the guard's exact scan scope before deciding, and re-confirmed by a full-suite test run after implementation (no unexpected failure). `InventoryCreationServiceTests.cs`'s guard, scoped to the `Odyssey.Application.Inventory` namespace, does need a narrow allow-list addition, per this ТЗ §7's own instruction to add a point exclusion, not edit the `forbidden` array itself. Authority: direct code read of both guards' exact scan scope; this ТЗ §7.
- 2026-09-12 — Decision: name the confirm-guard field `PreviewRevision` (matching `ADR-027` §10 step 5's own exact terminology) rather than `PreviewHash` (`RulesetMigrationRules`'s own field name for the same concept in its different domain) — the underlying implementation is still a SHA-256 digest, matching `RulesetMigrationRules.ComputePreviewHash`'s own algorithm exactly, only the public field/method name is aligned to this ADR's own vocabulary. Authority: `ADR-027` §10 step 5's own exact field name.
- 2026-09-12 — Decision: the two new `IInventoryRepository` methods are named `ListItemInstancesBySourceDefinitionId`/`ListItemStacksBySourceDefinitionId` and reuse `EscapeLike` unchanged — no new escaping logic is written, avoiding the exact class of bug (`_`/`%` treated as SQL wildcards) `EscapeLike` already exists to prevent. Authority: this ТЗ §4's own explicit instruction to reuse `HasAnyRuntimeReferenceToDefinition`'s pattern.
- 2026-09-12 — Discovery and decision: a full `dotnet test` run after implementation surfaced a THIRD scope guard the governing ТЗ did not name — `DotNet/Tests/Odyssey.Tests.Unit/Inventory/InventoryRuntimeRecordTests.cs`'s `InventoryRuntimeScope_AllowsOnlyMoveBehaviorBeforeLaterInventoryTasks`, which scans `Packages/**/Runtime/Inventory/*.cs` FILE NAMES (not type names, unlike the two guards this ТЗ's §7 did name) for forbidden fragments including `"Migration"`, asserting zero matches. Adding `ItemDefinitionMigrationRules.cs` under `Packages/com.odyssey.application/Runtime/Inventory/` triggered it. **Narrowed the same way this ТЗ's own §7 mandated for its two named guards**: replaced the blanket "no Migration file" assertion with an exact `Is.EquivalentTo(new[] { "ItemDefinitionMigrationRules.cs" })` allow-list — mirroring the very same test's own existing pattern for `Move`/`Stack`/`Equipment` file names on adjacent lines, not a deletion or weakening, since any *other* future Migration-named file would still fail it. This is a minimal (1-line), unavoidable, and correctly-scoped guard update; it required touching one file (`DotNet/Tests/Odyssey.Tests.Unit/Inventory/InventoryRuntimeRecordTests.cs`) outside the allowed-paths list this task contract originally wrote in §5, which is amended above to record it. Authority: this ТЗ §7's own point-allow-exception principle, applied to a guard the initial investigation had not located because the ТЗ itself only named the two guards under `DotNet/Tests/Odyssey.Tests.Persistence/`.

### Approved task changes

- None.
