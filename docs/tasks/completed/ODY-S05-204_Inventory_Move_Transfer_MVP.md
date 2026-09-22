# ODY-S05-204 — Inventory Move / Transfer MVP

**Status:** Done (PR #116, merged into main)
**Roadmap stage / slice:** SLICE-05
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-204-inventory-move-transfer-mvp`
**Pull request:** Draft [#116](https://github.com/odyssey-services/Odyssey_VTT/pull/116)
**ExecPlan:** `docs/plans/active/ODY-S05-204_Inventory_Move_Transfer_MVP.md`
**Created:** 2026-09-08
**Last updated:** 2026-09-09 UTC

## 1. Goal

Add MainGM-only, atomic, revision-guarded moves of existing `ItemInstanceRecord` and `ItemStackRecord` between Inventory containers.

## 2. Why this task exists

- Problem or dependency being addressed: runtime items and stacks created by `ODY-S05-203` cannot yet change containers.
- Value or risk reduction: protects the one-place Inventory invariant during transfer and establishes the durable boundary needed before stack split/merge.
- Blocking or enabling relationship: enables `ODY-S05-205`; equipment/drop/pickup remain later work.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`; `PLANS.md`; `docs/tasks/TASK_TEMPLATE.md`.
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, sections 5-7, 12, and 14.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, row `ODY-S05-204`.

### Requirement and test IDs

- Requirement IDs: `ODY-S05-204`, `SLICE-05`.
- Existing test IDs: `TC-INVENTORY-001`-`045`.
- New test IDs: `TC-INVENTORY-046`-`060`.

### Task-safe private context

- Approved summary / references: product-owner task brief for `ODY-S05-204`.

## 4. Verified current state

### Verified facts

- PR #115 is merged into `origin/main` at `542c4df`.
- The current repository supports Inventory create/read/list and `InventoryCommandLedger` creation idempotency only; no move primitive or move ledger exists.
- Existing Inventory rows are authoritative for this task; source locations are not independently validated against Character or Scene entities.

### Assumptions

- None.

## 5. Scope

### In scope

- Application move service/requests, repository move primitives, additive `InventoryMoveCommandLedger`, errors, persistence tests, metadata, and implementation documentation.

### Out of scope

- Equipment, unequip, drop, pickup, item use, ActiveEffect, attack, split/merge, Unity/UI, catalog changes, and ADR changes.

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Inventory/
Packages/com.odyssey.application/Runtime/Persistence/InventoryRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteInventoryRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/
Tests/Metadata/test-catalog.json
docs/errors/ERROR_CODES.md
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-204_Inventory_Move_Transfer_MVP.md
docs/plans/active/ODY-S05-204_Inventory_Move_Transfer_MVP.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Assets/**
```

## 6. Technical constraints

- Application must not emulate movement with read/create calls.
- One SQLite transaction validates revisions, updates source/destination Inventory revision(s), updates the target row, writes the move ledger, then commits.
- The move ledger is separate from `InventoryCommandLedger`; creation semantics remain unchanged.
- MainGM authorization precedes repository calls; no local paths or raw SQLite details enter errors.

## 7. Expected behavior

### Scenario 1 — contained ItemInstance moves within one Inventory

Given a contained ItemInstance and matching revisions, when MainGM changes its container key, then the record and that Inventory each advance once.

### Scenario 2 — ItemStack transfers across owners

Given existing Character-owned and Scene-owned Inventories, when MainGM transfers a contained stack, then its owner becomes the persisted destination owner.

### Scenario 3 — non-MainGM denial

Given a non-MainGM request, when submitted, then it fails before repository mutation or replay probe.

### Scenario 4 — stale revision

Given any stale target/source/destination revision, when submitted, then all rows and ledgers remain unchanged.

### Scenario 5 — exact replay

Given a successful move command, when replayed with identical identity, then it returns the current stored target without a second mutation.

### Required invariants

- Only `Contained` sources and `Contained(destinationInventoryId, containerKey)` destinations are valid.
- Destination owner comes from the persisted destination Inventory.
- Exact current destination, `Equipped`, `SceneDropped`, and `Other` reject.
- Failure leaves no partial update or ledger entry.
- Mechanics snapshot, runtime state, quantity, and `CreatedAt` are preserved.

## 8. Deliverables

- Production code: service/requests, repository primitives, move ledger, and errors.
- Tests: SQLite persistence/application coverage.
- Documentation: metadata, error registry, backlog, task contract, and ExecPlan.
- Dependencies, configuration, generated artifacts: None.

## 9. Acceptance criteria

1. MainGM-only enforcement precedes repository calls.
2. Instance and stack moves atomically update target and Inventory revisions.
3. Replay returns the current stored record without another mutation.
4. Creation-ledger and different move-identity collisions reject.
5. Every guard failure rolls back target, Inventory revisions, and ledger state.
6. Schema guard admits only the required move ledger addition.
7. No equipment, drop/pickup, ActiveEffect, attack, or split/merge runtime system is introduced.

## 10. Tests and validation

| Test ID | Behavior | Result |
|---|---|---|
| `TC-INVENTORY-046`-`047` | Instance and stack moves | Passed |
| `TC-INVENTORY-048`-`049` | Same-Inventory and cross-Inventory paths | Passed |
| `TC-INVENTORY-050`-`052` | MainGM and source/destination guards | Passed |
| `TC-INVENTORY-053`-`055` | Target, source, and destination revision guards | Passed |
| `TC-INVENTORY-056` | Exact-destination no-op rejection | Passed |
| `TC-INVENTORY-057`-`058` | Replay and ledger collision | Passed |
| `TC-INVENTORY-059` | Transaction rollback and move-ledger primary key | Passed |
| `TC-INVENTORY-060` | Scope guard | Passed |

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- Review the complete diff for no out-of-scope runtime system or ADR/Unity change. Result: Passed; no ADR or Unity files changed.

### Required environments / profiles

- Windows x64, pure .NET, real temporary SQLite campaign database.

### Validation not required by this task

- Unity/Player and attack/equipment/ActiveEffect testing because those systems are excluded.

## 11. Compatibility, migration, and rollback

- Additive `CREATE TABLE IF NOT EXISTS` move ledger only; existing record formats and creation-ledger behavior remain unchanged.
- Failed transactions roll back. Before merge, reverting the task removes the feature.

## 12. Dependencies and licensing

- None.

## 13. Security, privacy, and hidden information

- MainGM is the sole MVP authority; authorization precedes repository calls.
- Errors remain public-safe and expose neither local paths nor raw SQLite details.

## 14. Planning and execution mode

- Mode: `ExecPlan` at `docs/plans/active/ODY-S05-204_Inventory_Move_Transfer_MVP.md`.
- One Draft PR is expected.

## 15. Documentation and versioning impact

- On implementation: `ERROR_CODES.md`, test catalog, backlog, task contract, and ExecPlan update.
- No application, ADR, or version bump.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [ ] Required manual checks are completed.
- [ ] Required commands and their real results are recorded.
- [ ] Architecture and dependency rules remain valid.
- [ ] Security, privacy, redaction, and audience rules are verified where applicable.
- [ ] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [ ] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [ ] Documentation is updated only where materially required.
- [ ] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

| Area | Result |
|---|---|
| Product code, schema, tests, metadata, error registry, backlog | Implemented |

### Validation results

| Command / check | Result |
|---|---|
| `dotnet build`, `dotnet test`, `verify-format`, `check-repository-policy`, `verify-test-structure` | Passed |

### Acceptance result

| Criterion | Result |
|---|---|
| All acceptance criteria | Passed locally |

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-08 — Dedicated move ledger remains separate from the creation ledger.
- 2026-09-08 — Repository owns the atomic transaction; Application does not read/rebuild/write movement state.
- 2026-09-09 — Final Inventory and target writes use SQLite compare-and-swap predicates. A zero-row update rolls back before the move ledger can be inserted.

### Approved task changes

- None.
