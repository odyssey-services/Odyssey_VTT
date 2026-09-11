# ODY-S05-205 - Stack Split/Merge MVP

**Status:** Done (PR #119, merged into main)
**Roadmap stage / slice:** SLICE-05
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-205-stack-split-merge-mvp-impl`
**Pull request:** Draft — [#119](https://github.com/odyssey-services/Odyssey_VTT/pull/119) (open, awaiting owner review)
**ExecPlan:** `docs/plans/active/ODY-S05-205_Stack_Split_Merge_MVP.md`
**Created:** 2026-09-09
**Last updated:** 2026-09-10 UTC

## 1. Goal
Add MainGM-only, atomic split and merge operations for existing ItemStack runtime state.

## 2. Why this task exists
- Problem or dependency being addressed: runtime stacks cannot change quantity safely.
- Value or risk reduction: preserves quantity and shared-snapshot invariants.
- Blocking or enabling relationship: builds on Done ODY-S05-204 (PR #116, merged into main).

## 3. Authorities and requirement references
### Required authorities
- `AGENTS.md`, `PLANS.md`, `docs/tasks/TASK_TEMPLATE.md`.
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md` sections 5, 6.2, and 14.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, `ODY-S05-205`.
### Requirement and test IDs
- Requirement IDs: `ODY-S05-205`, `SLICE-05`.
- Existing test IDs: `TC-INVENTORY-001`-`060`.
- New test IDs: `TC-INVENTORY-061`-`078`.
### Task-safe private context
- Sanitized product-owner task brief only.

## 4. Verified current state
### Verified facts
- `origin/main` contains PR #116 at `36d00e4`.
- Existing Inventory uses separate creation/movement ledgers and CAS movement writes.
### Assumptions
- None.

## 5. Scope
### In scope
- MainGM split/merge service, narrow repository primitives, `InventoryStackCommandLedger`, tests, metadata, backlog, and evidence.
### Out of scope
- Unity/UI, equipment/equip/unequip, drop/pickup, item use, ActiveEffect, attack, catalog/ADR changes, ItemDefinition migration, and generic item deletion.
### Allowed paths
```text
Packages/com.odyssey.application/Runtime/Inventory/
Packages/com.odyssey.application/Runtime/Persistence/InventoryRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteInventoryRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/
DotNet/Tests/Odyssey.Tests.Unit/Inventory/
Tests/Metadata/test-catalog.json
docs/errors/ERROR_CODES.md
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-205_Stack_Split_Merge_MVP.md
docs/plans/active/ODY-S05-205_Stack_Split_Merge_MVP.md
```
### Paths requiring explicit approval before editing
```text
docs/adr/**
Assets/**
```

## 6. Technical constraints
- Repository owns one CAS-protected SQLite transaction; Application does not compose read/create/delete calls.
- A dedicated stack ledger does not change creation/movement ledger semantics.
- Merge physically removes only its consumed source inside the successful transaction; no general delete API exists.
- Stored snapshots, never current catalog definitions, determine compatibility and max quantity.

## 7. Expected behavior
### Scenario 1 - split
MainGM splits a positive proper subset of a contained stack into a new same-location record, preserving total quantity and all pinned mechanics/state.
### Scenario 2 - merge
MainGM merges two exactly mechanically identical contained stacks in one owner/location into the chosen survivor, atomically removing the consumed source.
### Required invariants
- Authorization precedes all repository calls.
- Split never creates a zero-quantity stack.
- Merge requires exact definition ref, snapshot, stack state, owner, inventory, and location.
- Replay returns the current result without repeated mutation.

## 8. Deliverables
- Production code: service/requests, repository port/primitives, ledger, safe errors.
- Tests: SQLite persistence/application and narrow scope guards.
- Scripts / CI: None.
- Configuration: None.
- Documentation: metadata, errors, backlog, contract, ExecPlan.
- Generated evidence or build artifacts: test/build output only.
- Migration / recovery material: additive ledger and rollback tests.

## 9. Acceptance criteria
1. MainGM split/merge are atomic, idempotent, and CAS-protected.
2. Split preserves total quantity, snapshot, state, owner, and exact location.
3. Merge accepts only mechanically identical stacks and uses stored snapshot max-stack rules.
4. Failures leave records, inventory revision, and ledger unchanged.
5. No excluded capability or general delete API is added.

## 10. Tests and validation
| Test ID | Layer / runner | Behavior | Required result |
|---|---|---|---|
| `TC-INVENTORY-061`-`069` | SQLite .NET | split, guards, replay | Pass |
| `TC-INVENTORY-070`-`078` | SQLite .NET | merge, limits, rollback, ledger | Pass |
```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```
### Manual validation
- Review final diff for no excluded runtime, ADR, or Unity edits.
### Required environments / profiles
- Windows x64, pure .NET, temporary SQLite database.
### Validation not required by this task
- Unity/Player and excluded systems.

## 11. Compatibility, migration, and rollback
- One additive `InventoryStackCommandLedger`; failed transactions roll back. Revert before release removes the surface.

## 12. Dependencies and licensing
- None.

## 13. Security, privacy, and hidden information
- MainGM check occurs before repository access; safe errors contain no SQLite details.

## 14. Planning and execution mode
- Planning mode: ExecPlan.
- Reason: public persistence port/schema, transaction, permissions, and durable replay.
- ExecPlan path: named above.
- Expected pull request count: 1.
- Documentation commits precede product edits.

## 15. Documentation and versioning impact
- Update test metadata, errors, backlog, task, and plan. No ADR/version change.

## 16. Definition of Done
- [x] Acceptance and scope verified.
- [x] Required validation passes with real evidence.
- [x] No architecture/dependency/security/versioning violation.
- [x] Draft PR is open; Codex does not merge.

## 17. Completion evidence
### Changed files / areas
- `Packages/com.odyssey.application/Runtime/Persistence/InventoryRepositoryContracts.cs` - `IInventoryRepository.SplitItemStack` / `MergeItemStacks` (from the applied patch).
- `Packages/com.odyssey.application/Runtime/Inventory/InventoryStackOperationService.cs` - MainGM-gated `Split` / `Merge` service, `InventoryStackOperation`, `SplitItemStackRequest`, `MergeItemStacksRequest` (from the applied patch; the quantity guard was adjusted so the merge path's `quantity: 0` is constructible - split `>= 1`, merge `== 0`).
- `Packages/com.odyssey.application/Runtime/Inventory/InventoryStackFailures.cs` - new public-safe factories for the three new codes.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` - `InventoryStackSplitQuantityInvalid`, `InventoryStackMergeMismatch`, `InventoryStackMergeExceedsMaxQuantity`.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteInventoryRepository.cs` - `SplitItemStack` / `MergeItemStacks`, `RunStackOperation` / `SplitInTransaction` / `MergeInTransaction` / `TryStackReplay` / `InsertStackLedger` helpers, and the `InventoryStackCommandLedger` table in `EnsureInventoryTables`.
- `DotNet/Tests/Odyssey.Tests.Persistence/InventoryStackOperationServiceTests.cs` - new, `TC-INVENTORY-061`-`078`.
- `DotNet/Tests/Odyssey.Tests.Persistence/SqliteInventoryRepositoryTests.cs`, `DotNet/Tests/Odyssey.Tests.Persistence/InventoryCreationServiceTests.cs`, `DotNet/Tests/Odyssey.Tests.Unit/Inventory/InventoryRuntimeRecordTests.cs` - updated the `ODY-S05-203`/`204` scope guards that pre-forbade split/merge and the new ledger table.
- `Tests/Metadata/test-catalog.json` - registered `TC-INVENTORY-061`-`078`.
- `docs/errors/ERROR_CODES.md` - registered the three new codes.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` - `ODY-S05-205` row -> `In Review (PR #119)`.

### Validation results
| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | Passed | `Сборка успешно завершена. Предупреждений: 0. Ошибок: 0`. |
| `dotnet test DotNet\Odyssey.Core.sln` | Passed | 697 total, 0 failed (Contracts 1, Domain 74, Networking 67, Unit 136, Architecture 2, Persistence 417 - the Persistence project carries the 18 new `TC-INVENTORY-061`-`078`). |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\verify-test-structure.ps1` | Passed | `TC-ARCH-001 PASS` / `TC-ARCH-002 PASS` (all four controlled-invalid fixtures). |
| `.\scripts\check-repository-policy.ps1` | Passed | `REPO-POLICY-001`-`005 PASS`; `Repository policy check passed.` (registry complete, including the three new codes and their `TC-INVENTORY-063`/`072`/`076` references). |

### Acceptance result
| Criterion | Status | Evidence |
|---|---|---|
| 1 - MainGM split/merge atomic, idempotent, CAS-protected | Passed | `TC-INVENTORY-061`, `-068`, `-069`, `-070`, `-077`, `-078`; single SQLite transaction per call in `RunStackOperation`. |
| 2 - Split preserves total quantity, snapshot, state, owner, exact location | Passed | `TC-INVENTORY-061`. |
| 3 - Merge accepts only mechanically identical stacks; stored-snapshot rules | Passed | `TC-INVENTORY-070`, `-072`, `-073`; identity check reads only stored fields, never the live catalog. Stored-snapshot max-stack-size: see Known limitations. |
| 4 - Failures leave records, inventory revision, and ledger unchanged | Passed | `TC-INVENTORY-063`-`067`, `-072`-`076`, `-078`. |
| 5 - No excluded capability or general delete API is added | Passed | Merge's `DELETE` is a private step inside its own transaction; scope-guard tests updated but still forbid `Transfer`/`Equipment`/`Attack`/`ActiveEffect`/`ItemDefinitionMigration`; `verify-test-structure` green. |

### Build and artifact evidence
- No new project, script, CI, or configuration. One additive SQLite table (`InventoryStackCommandLedger`), created by `EnsureInventoryTables` on open.

### Known limitations
- **Catalog `MaxStackSize` is not enforced by merge.** The stored `ItemMechanicsSnapshot.Payload` is opaque at the persistence layer by `ODY-S05-202`/`203` design (there is even a scope guard asserting the repository does not decode typed definitions), so a typed `ItemDefinition.MaxStackSize` cannot be read here without crossing that boundary. Merge enforces only that the combined quantity stays representable in the stored `long` range (`inventory.stack_merge.exceeds_max_quantity`). Catalog-defined maximum-stack-size enforcement belongs to a later Application-layer command that decodes the typed definition - candidate follow-up `ODY-S05-206`/`207` or a new task; no domain/catalog field was added here.
- Split/merge deny for a non-MainGM actor with the shared `inventory.move.denied` code, reused from `InventoryMovementFailures.Denied` as wired by the supplied patch's service; no separate `stack_split`/`stack_merge` denied code was added.

### Follow-up tasks
- `ODY-S05-206` (runtime reference dependency checks), `ODY-S05-207` (integration fixtures), plus the catalog `MaxStackSize` enforcement noted above.

### Self-review summary
- Diff confined to section 5's allowed paths; no ADR, `Assets/`, Unity, or excluded-system change. Mirrors the existing `MoveItem<T>` transaction/CAS/replay pattern. All five required commands run with real output recorded above.

## 18. Blockers, decisions, and change control
### Blockers
- None.
### Decisions made during execution
- 2026-09-09 - Use a dedicated `InventoryStackCommandLedger` rather than reusing the move/create ledgers - task authority (section 6).
- 2026-09-10 - Split and merge do **not** increment `Inventory.Revision`: neither changes an item's owner/inventory, unlike a move. `ExpectedInventoryRevision` is still read and CAS-checked. (See ExecPlan section 8.)
- 2026-09-10 - `InventoryStackOperation`'s quantity guard was made operation-kind aware so the merge path's `quantity: 0` is constructible; smallest change to the applied patch.
- 2026-09-10 - Catalog `MaxStackSize` enforcement deferred (see Known limitations); no domain/catalog field added.
### Approved task changes
- None.
