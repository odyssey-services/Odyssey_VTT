# ODY-S04-001 — ADR Character Aggregate, Section Revisions, and History Projection

**Status:** Active  
**Owner:** Codex (agent)  
**Branch:** `feat/ody-s04-001-adr-character-aggregate`  
**Pull request:** Not opened  
**Last updated:** 2026-08-29 22:53 UTC

## 1. Purpose and user-visible outcome

Accept `ADR-022`, which fixes the Character aggregate boundary, section revision and lock model, minimum Character event history snapshots, and `CharacterHistoryProjection` source-of-truth contract before `SLICE-04` implementation decomposition begins.

## 2. Task contract

- Goal: create accepted `ADR-022` and task evidence for `ODY-S04-001`.
- Acceptance criteria: ADR exists, answers all four required questions, includes alternatives, excludes future ADR-023/024/025 scopes, updates `SLICE-04_BACKLOG.md`, and passes required validation.
- Requirement IDs: `ODY-S04-001`, `ADR-022`, `SLICE-04` prerequisite backlog item 1.
- In scope: `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md`, task contract, this ExecPlan, and `SLICE-04_BACKLOG.md` status row.
- Out of scope: production code, tests, schema, DTOs, Unity UI, Draft/template workflow, DevelopmentPool/progression economy, full ownership/lifecycle/ruleset migration operations, ability/resource/anatomy mechanics beyond section membership.
- Required authorities: `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`, `AGENTS.md`, `PLANS.md`, `docs/tasks/TASK_TEMPLATE.md`, `docs/tasks/SLICE-04_BACKLOG.md`, roadmap section 13.3/13.8/13.9 summaries, Character/Progression and Rules Engine summaries, Domain Model Character/Progression summaries, `ADR-001`, `ADR-002`, `ADR-003`, `ADR-012`, `ADR-013`, and style precedents `ADR-020`/`ADR-021`.
- Required validation commands: `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`.

## 3. Current state

- Local `main` was fast-forwarded to `origin/main` at `e237ab9`, which includes PR #79 and the `ODY-S04-000` prerequisite backlog.
- `docs/tasks/SLICE-04_BACKLOG.md` lists `ODY-S04-001` as the first prerequisite ADR task and future `ADR-022`.
- Existing accepted ADRs provide command/event/idempotency, serialization, append-only journal, projection transaction, and database schema migration substrate.
- No `ADR-022` file exists before this task.

Assumptions: none.

## 4. Proposed approach

Write a documentation-only ADR that specializes the already accepted substrates instead of redefining them:

- choose one `Character` aggregate root with section revisions for parallel editing;
- define minimum section lock keys and make locks concurrency gates, not idempotency or history;
- define minimum historical snapshots carried by Character-significant events;
- define `CharacterHistoryProjection` as a rebuildable projection from DomainEvents/current projection inputs;
- update the parent backlog row to `Done`;
- create a task contract with validation evidence.

No production code, schema, tests, dependencies, or private product prose enters the repository.

## 5. Milestones

### M1 — ADR and contract authored

- [x] Create `ADR-022` with decisions, alternatives, exclusions, traceability, and normative action.
- [x] Create `ODY-S04-001` task contract with all 18 sections.
- [x] Update `SLICE-04_BACKLOG.md` row for `ODY-S04-001`.

### M2 — Validation and review readiness

- [x] Run `.\scripts\verify-format.ps1`.
- [x] Run `.\scripts\check-repository-policy.ps1`.
- [ ] Commit, push, open Draft PR, and record PR/CI status.

## 6. Progress log

- 2026-08-29 22:53 UTC — Preflight confirmed `origin/main` includes PR #79 at `e237ab9`; created branch `feat/ody-s04-001-adr-character-aggregate`.
- 2026-08-29 22:53 UTC — Read task brief, `SLICE-04_BACKLOG.md`, `TASK_TEMPLATE.md`, `PLANS.md`, and relevant roadmap/product/ADR excerpts.
- 2026-08-29 22:53 UTC — Authored initial ADR/task/plan/backlog patch.
- 2026-08-29 22:53 UTC — Local validation passed: `.\scripts\verify-format.ps1` and `.\scripts\check-repository-policy.ps1`.

## 7. Decisions

- 2026-08-29 — Decision: use ExecPlan. Rationale: this ADR changes future public domain/persistence/reconnect contracts and the prerequisite backlog expected ExecPlan-level tracking. Authority: `PLANS.md` §1.2 and `docs/tasks/SLICE-04_BACKLOG.md`.
- 2026-08-29 — Decision: Character is one aggregate root with section revisions, not multiple independent roots. Rationale: cross-section lifecycle/history/reconnect invariants need one authoritative boundary while section revisions preserve parallel edits. Authority: `ADR-022` §4.
- 2026-08-29 — Decision: no technical spike. Rationale: all remaining questions are model/contract decisions over accepted substrates. Authority: `docs/tasks/SLICE-04_BACKLOG.md` §5.

## 8. Discoveries and deviations

- `ODY-S04-000` was already merged into `main` through PR #79 before this task began.
- Adding this ExecPlan creates one additional docs file beyond the brief's expected three-file diff; this is required by `PLANS.md` because the ADR affects future public contracts and is expected to be resumable.

## 9. Validation and acceptance evidence

- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed with `Repository policy check passed`.

## 10. Recovery and rollback

Rollback is a normal docs-only revert of this branch/PR. No production code, schema, migration, assets, dependencies, or generated artifacts are created.

## 11. Open questions and blockers

None.

## 12. Outcome and follow-up

Pending Draft PR and CI.
