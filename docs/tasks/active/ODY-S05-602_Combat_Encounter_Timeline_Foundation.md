# ODY-S05-602 — Combat Encounter Timeline Foundation

**Status:** In Progress  
**Roadmap stage / slice:** SLICE-05, Block 3  
**Owner:** Codex (agent)  
**Branch:** `feat/ody-s05-602-combat-encounter-timeline`  
**Pull request:** Not opened  
**Plan:** `docs/plans/active/ODY-S05-602_Combat_Encounter_Timeline_Foundation.md` (ExecPlan)

## 1. Goal

Implement the authoritative combat encounter timeline only: ordered Character participants, rounds, turns, lifecycle records, setup, and safe advance.

## 2. Why this task exists

`ADR-029` requires persisted timeline infrastructure before attack evaluation or application may exist.

## 3. Authorities and requirement references

`AGENTS.md`; `ADR-029` §1 rules 1–2, §4, §9–§12; `ADR-008`; `ADR-012`; `ADR-028`; backlog §17/§17.1; existing Scene/Character/ActiveEffect/Inventory/SQLite patterns. Requirement IDs: `ODY-S05-602`, `SLICE-05`; tests `TC-COMBAT-001`–`020`.

## 4. Verified current state

Branch begins at `origin/main` `245109e`, which merged PR #145. No production inspection or edit occurred before this contract/plan commit.

## 5. Scope

In: pure encounter vocabulary, MainGM create/advance commands, standalone repository/SQLite tables/ledger, tests/catalog, task/plan/backlog status after PR exists. Out: attacks, preview, RNG rolls, pending apply, combat effects/durations, compensation/Game Log, UI/networking/scheduler/initiative formula/generic engine.

## 6. Technical constraints

Domain has no Unity/SQLite/serializer/clock/RNG/networking dependency. Commands use injected clock, `CommandId`/`CorrelationId`, idempotency, CAS, one SQLite transaction, append-only lifecycle records, and no direct timer/RNG/UI timing.

## 7. Expected behavior

MainGM creates a non-empty, distinct, eligible ordered Character list and persists creation → round 1 → first turn. Advance ends current turn, skips ineligible participants explicitly, wraps rounds, and closes after a full ineligible pass. Exact replays return durable state without duplicate records.

## 8. Deliverables

Domain/Application/Persistence timeline surfaces, SQLite schema/ledger, NUnit coverage and `TC-COMBAT-001`–`020`, docs/plan/backlog status, Draft PR.

## 9. Acceptance criteria

All behaviors and exclusions in the product brief are implemented; lifecycle/replay/rollback/schema/scope tests cover the required minimum; no later-pipeline concern enters the diff.

## 10. Tests and validation

Run `dotnet build`, `dotnet test`, `verify-format`, `check-repository-policy`, `verify-test-structure`. Before implementation these are **Not run**; results will be recorded honestly.

## 11. Compatibility, migration, and rollback

New internal SQLite tables only; no existing data migration. Rollback is reverting the PR before merge.

## 12. Dependencies and licensing

No new dependency or license.

## 13. Security, privacy, and hidden information

MainGM enforcement and safe errors; no client-trusted final state, secret, hidden combat data, or raw event transport.

## 14. Planning and execution mode

ExecPlan: multiple production modules, persistence, commands, idempotency, time/clock and security boundaries.

## 15. Documentation and versioning impact

Task/plan first; backlog status only after PR exists. No application/protocol/ruleset version change.

## 16. Definition of Done

- [ ] Production behavior and tests complete.
- [ ] Required validation recorded.
- [ ] Draft PR opened; Codex does not merge.

## 17. Completion evidence

Documentation-first stage only. All implementation validation: **Not run**.

## 18. Blockers, decisions, and change control

### Blockers

None.

### Decisions made during execution

- 2026-09-13 — Documentation-first commit before production inspection/edit. Authority: product brief.

### Approved task changes

None.
