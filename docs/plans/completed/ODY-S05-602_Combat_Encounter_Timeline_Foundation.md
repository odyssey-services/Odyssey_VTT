# ODY-S05-602 — Combat Encounter Timeline Foundation

**Status:** Done (PR #146, merged into main)
**Owner:** Codex (agent)  
**Branch:** `feat/ody-s05-602-combat-encounter-timeline`  
**Pull request:** Not opened

## 1. Purpose and user-visible outcome

Provide the authoritative turn/round board on which later attack commands can run, without implementing attacks.

## 2. Task contract

See `docs/tasks/active/ODY-S05-602_Combat_Encounter_Timeline_Foundation.md`. Required commands: build/test plus format/policy/test-structure verification.

## 3. Current state

Started from `origin/main` `245109e` after PR #145. Documentation-first commit precedes production inspection.

## 4. Proposed approach

Reuse existing aggregate/repository/SQLite-ledger patterns. Add only encounter identity/state, ordered Character participants, lifecycle records, create/advance commands, and their narrow tests. Use an injected clock and a single transaction; do not add an initiative abstraction or generic engine.

## 5. Milestones

### M1 — Documentation gate

- [x] Task contract and ExecPlan committed without production inspection/edit.

### M2 — Timeline vertical slice

- [ ] Inspect existing patterns; implement Domain/Application/Persistence and tests.

### M3 — Evidence and review

- [ ] Run required validation, update docs/backlog, open Draft PR.

## 6. Progress log

- 2026-09-13 — Created contract and plan; validation not run.

## 7. Decisions

- 2026-09-13 — Persist supplied order; no initiative formula. Authority: task brief, `ADR-029` §1.

## 8. Discoveries and deviations

None yet.

## 9. Validation and acceptance evidence

2026-09-13: `test-fast`, `verify-format`, `check-repository-policy`, `verify-test-structure`, and `verify-repository` passed after implementation. `verify-docs.ps1` and `format.ps1` do not exist in this repository, so neither was run; the changed C# files were formatted with the repository's `dotnet format` solution command before `verify-format` passed.

## 10. Recovery and rollback

### Amendment (2026-09-13)

Restored the Application/Persistence boundary with a narrow service; added durable-ledger collision checks and real SQLite encounter tests. No attack-pipeline seam was added.

### Amendment 2 (2026-09-13)

Corrected the catalog-delete ledger identifier and added direct-SQL fixture coverage for foreign command-id collisions, schema constraints, and source-level scope guards.

### Amendment 3 (2026-09-13)

Added constructor invariant, exact replay, foreign-advance collision, and `IWallClock`/forbidden-clock-source evidence without changing production behavior.

Revert PR before merge; no migration is planned.

## 11. Open questions and blockers

None.

## 12. Outcome and follow-up

Pending implementation; `ODY-S05-603` remains out of scope.
