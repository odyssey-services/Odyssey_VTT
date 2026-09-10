# ODY-S05-205 - Stack Split/Merge MVP

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-205-stack-split-merge-mvp`
**Pull request:** Not opened
**Last updated:** 2026-09-09 UTC

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
- [ ] Commit documentation separately.
### M2 - Atomic operations
- [ ] Add contracts, service, ledger, and CAS split/merge primitives.
- [ ] Preserve total quantity and pinned mechanics.
### M3 - Review evidence
- [ ] Add/register `TC-INVENTORY-061`-`078`.
- [ ] Update backlog/evidence and open Draft PR.

## 6. Progress log
- 2026-09-09 UTC - Verified #116 merge, created branch, read authorities, and prepared documentation baseline.

## 7. Decisions
- 2026-09-09 - Dedicated stack ledger preserves creation and movement replay semantics. Authority: task.

## 8. Discoveries and deviations
- None.

## 9. Validation and acceptance evidence
- Not run; implementation pending.

## 10. Recovery and rollback
- One SQLite transaction rolls back both stacks, inventory revision, and ledger. No public generic delete operation.

## 11. Open questions and blockers
- None.

## 12. Outcome and follow-up
- `ODY-S05-206` owns runtime reference checks; `ODY-S05-207` owns integration proof.
