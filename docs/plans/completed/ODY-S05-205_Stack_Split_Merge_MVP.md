# ODY-S05-205 - Stack Split/Merge MVP

**Status:** Done (PR #119, merged into main)
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-205-stack-split-merge-mvp-impl`
**Pull request:** Draft - [#119](https://github.com/odyssey-services/Odyssey_VTT/pull/119)
**Last updated:** 2026-09-10 UTC

## 1. Purpose and user-visible outcome
MainGM can atomically split a stack or merge compatible stacks without changing pinned runtime mechanics.

## 2. Task contract
- Goal and acceptance: `docs/tasks/active/ODY-S05-205_Stack_Split_Merge_MVP.md`.
- Authorities: ADR-027 section 6.2, backlog, AGENTS, PLANS.
- Scope: Application service/port, SQLite ledger/transactions, tests, and evidence.
- Non-goals: equipment, drop/pickup, use/effects, attacks, Unity, catalog/ADR/migration, generic deletion.
- Validation: build, full test, format, policy, and test structure.

## 3. Current state
- #116 merged at `36d00e4`; Inventory has creation/movement ledgers and CAS movement.
- No stack quantity operation or stack-operation ledger exists.

## 4. Proposed approach
One MainGM-gated service delegates to narrow repository transactions. Each checks all ledgers, validates stored snapshots and revisions, makes CAS stack/inventory writes, inserts the stack ledger last, and commits. Merge deletes only its consumed source in that transaction.

## 5. Milestones
### M1 - Documentation baseline
- [x] Create task contract and ExecPlan before product edits.
- [x] Commit documentation separately (baseline commit applies the supplied patch).
### M2 - Atomic operations
- [x] Add contracts, service, ledger, and CAS split/merge primitives.
- [x] Preserve total quantity and pinned mechanics.
### M3 - Review evidence
- [x] Add/register `TC-INVENTORY-061`-`078`.
- [x] Update backlog/evidence and open Draft PR.

## 6. Progress log
- 2026-09-09 UTC - Verified #116 merge, created branch, read authorities, and prepared documentation baseline.
- 2026-09-10 UTC - Applied the product owner's in-progress patch as a baseline commit; implemented `SqliteInventoryRepository.SplitItemStack` / `MergeItemStacks` and the `InventoryStackCommandLedger` on the `MoveItem<T>` transaction/CAS/replay pattern; added `InventoryStackFailures` and three error codes; wrote `TC-INVENTORY-061`-`078` and updated the `ODY-S05-203`/`204` scope guards; all five required commands pass (see section 9); opened Draft PR [#119](https://github.com/odyssey-services/Odyssey_VTT/pull/119).

## 7. Decisions
- 2026-09-09 - Dedicated stack ledger preserves creation and movement replay semantics. Authority: task.

## 8. Discoveries and deviations
- **Split/merge do not increment `Inventory.Revision`.** Section 4 leaves this open; unlike a move, neither operation changes an item's owner or inventory, so there is nothing to serialise at the inventory level. `ExpectedInventoryRevision` is still read and CAS-checked (a stale value is rejected). Only the affected `ItemStack` row revisions advance.
- **Catalog `MaxStackSize` is not enforced by merge.** The stored mechanics-snapshot payload is opaque at this persistence layer by `ODY-S05-202`/`203` design, so a typed `ItemDefinition.MaxStackSize` cannot be read here without crossing that boundary and no domain/catalog field was added. Merge enforces only long-range representability (`inventory.stack_merge.exceeds_max_quantity`); catalog max-stack-size enforcement is deferred to a later Application-layer command (follow-up).
- **Patch fix.** The supplied `InventoryStackOperation` constructor rejected the merge request's `quantity: 0` (`quantity < 1` guard). Made the quantity guard operation-kind aware: split requires `>= 1`, merge requires `== 0`. Smallest change that makes `MergeItemStacksRequest` constructible; the rest of the patch is unchanged.
- **Scope guards updated.** `InventoryCreationServiceTests` and `InventoryRuntimeRecordTests` forbade the fragments `Split`/`Merge` "before ODY-S05-205"; `SqliteInventoryRepositoryTests` pinned the exact Inventory table set. All three are in this task's allowed test paths and were updated to admit this task's stack-operation surface and the new ledger while still forbidding `Transfer`/`Equipment`/`Attack`/`ActiveEffect`/`ItemDefinitionMigration`.

## 9. Validation and acceptance evidence
- `dotnet build DotNet\Odyssey.Core.sln` - passed, 0 warnings, 0 errors.
- `dotnet test DotNet\Odyssey.Core.sln` - passed, 697 total, 0 failed (Persistence project carries `TC-INVENTORY-061`-`078`).
- `.\scripts\verify-format.ps1` - `FORMAT-001 PASS`.
- `.\scripts\verify-test-structure.ps1` - `TC-ARCH-001` / `TC-ARCH-002 PASS`.
- `.\scripts\check-repository-policy.ps1` - `REPO-POLICY-001`-`005 PASS`, `Repository policy check passed.`

## 10. Recovery and rollback
- One SQLite transaction rolls back both stacks, inventory revision, and ledger. No public generic delete operation.

## 11. Open questions and blockers
- None.

## 12. Outcome and follow-up
- `ODY-S05-206` owns runtime reference checks; `ODY-S05-207` owns integration proof.
