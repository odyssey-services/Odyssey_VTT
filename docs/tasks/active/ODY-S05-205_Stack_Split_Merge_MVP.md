# ODY-S05-205 - Stack Split/Merge MVP

**Status:** In Progress
**Roadmap stage / slice:** SLICE-05
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-205-stack-split-merge-mvp`
**Pull request:** Not opened
**ExecPlan:** `docs/plans/active/ODY-S05-205_Stack_Split_Merge_MVP.md`
**Created:** 2026-09-09
**Last updated:** 2026-09-09 UTC

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
- [ ] Acceptance and scope verified.
- [ ] Required validation passes with real evidence.
- [ ] No architecture/dependency/security/versioning violation.
- [ ] Draft PR is open; Codex does not merge.

## 17. Completion evidence
### Changed files / areas
- Not run.
### Validation results
| Command / check | Result | Evidence |
|---|---|---|
| Required commands | Not run | Implementation pending. |
### Acceptance result
| Criterion | Status | Evidence |
|---|---|---|
| All | Not run | Implementation pending. |
### Build and artifact evidence
- Not applicable before validation.
### Known limitations
- Excluded systems remain unavailable.
### Follow-up tasks
- `ODY-S05-206`, `ODY-S05-207`.
### Self-review summary
- Pending implementation.

## 18. Blockers, decisions, and change control
### Blockers
- None.
### Decisions made during execution
- 2026-09-09 - Use a dedicated stack-operation ledger - task authority.
### Approved task changes
- None.
