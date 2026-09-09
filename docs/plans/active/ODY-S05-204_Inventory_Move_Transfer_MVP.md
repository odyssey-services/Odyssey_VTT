# ODY-S05-204 — Inventory Move / Transfer MVP

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-204-inventory-move-transfer-mvp`
**Pull request:** Draft [#116](https://github.com/odyssey-services/Odyssey_VTT/pull/116)
**Last updated:** 2026-09-09 UTC

## 1. Purpose and user-visible outcome

MainGM can atomically move an existing item instance or stack between Inventory containers, with optimistic-concurrency protection and durable replay behavior.

## 2. Task contract

- Task: `docs/tasks/active/ODY-S05-204_Inventory_Move_Transfer_MVP.md`.
- Authorities: ADR-027 sections 5-7, 12, 14; backlog row `ODY-S05-204`; `AGENTS.md`; `PLANS.md`.
- Scope: Application commands/port, SQLite move transaction and one move ledger, errors, persistence tests, metadata, and task documentation.
- Non-goals: equipment/drop/pickup, item use, attacks, ActiveEffects, split/merge, catalog work, Unity, ADR edits, or changing creation-ledger semantics.

## 3. Current state

- PR #115 is merged into `origin/main` as `542c4df`.
- Current creation replay uses `InventoryCommandLedger`; it is intentionally separate from movement replay.
- Inventory records persist `InventoryId`, `OwnerRef`, `LocationRef`, revisions, snapshots, and timestamps; SQLite already enables foreign keys and WAL transactions.

## 4. Proposed approach

1. Define narrow Application request and repository move contracts for instances and stacks, with the complete expected-state identity supplied by the caller.
2. Add a single `InventoryMoveCommandLedger` table keyed by `CommandId`, recording target kind/id, source/destination IDs, destination key, and all expected revisions.
3. In one SQLite transaction: reject creation-ledger collision, check move replay identity, load target/source/destination, validate contained source and expected revisions, reject no-op, update inventory revision(s), update the target row, insert the move ledger, then commit.
4. Keep Application limited to MainGM gate and delegating the atomic move. The repository determines stored destination owner and current row returned by replay.

## 5. Milestones

### M1 — Durable contracts and move transaction

- [x] Add request/port/error contracts and one additive move ledger table.
- [x] Implement atomic instance/stack move primitives and replay identity checks.

### M2 — Behavior evidence

- [x] Add `TC-INVENTORY-046` onward for move, transfer, guards, replay, collision, and rollback.
- [x] Register metadata and error codes.

### M3 — Review-ready task

- [x] Update backlog status after the Draft PR number is assigned.
- [x] Run required commands and review scope diff.

## 6. Progress log

- 2026-09-08 UTC — Fetched origin, verified merged PR #115 at `542c4df`, created task branch from `origin/main`, read authorities, and created task contract/ExecPlan before product edits.
- 2026-09-09 UTC — Implemented the MainGM-gated application surface, atomic SQLite move transaction, dedicated ledger, and test coverage. Local build, full test suite, format, policy, and test-structure gates passed.

## 7. Decisions

- 2026-09-08 — Keep move identity in one dedicated table rather than extending `InventoryCommandLedger`, because the task explicitly requires a separate move ledger and creation behavior must remain unchanged.
- 2026-09-08 — Use repository-held transaction logic for target, Inventory, revision, and ledger writes; Application must not rebuild/persist rows as a sequence of reads and creates.

## 8. Discoveries and deviations

- None.

## 9. Validation and acceptance evidence

- `dotnet build DotNet\\Odyssey.Core.sln`, `dotnet test DotNet\\Odyssey.Core.sln`, `verify-format`, `check-repository-policy`, and `verify-test-structure` passed locally.

## 10. Recovery and rollback

Failed SQLite moves roll back automatically. The move ledger is inserted only with the successful row/inventory updates. Before merge, revert this task branch to remove the additive behavior.

## 11. Open questions and blockers

- None: the supplied task fixes the valid source/destination vocabulary, revision behavior, and idempotency semantics.

## 12. Outcome and follow-up

- `ODY-S05-205` remains the owner of split/merge; equipment/drop/pickup and other excluded flows remain unimplemented.
