# ODY-S04-003 — ADR Development Economy and Progression Transactions

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s04-003-adr-development-economy`
**Pull request:** <to be filled after `gh pr create`>
**Last updated:** 2026-08-30 UTC

## 1. Purpose and user-visible outcome

Accept `ADR-024`, which fixes the `DevelopmentPool`/`DevelopmentTransaction` ledger boundary, atomicity/duplicate-spend prevention for advancement purchases, reservation and error-cancellation shape, `CriticalSuccessEvidence` single-use mechanism, and `CharacterRespec` compensation shape, before `SLICE-04` implementation decomposition begins for this concern.

## 2. Task contract

- Goal: create accepted `ADR-024` and task evidence for `ODY-S04-003`.
- Acceptance criteria: ADR exists, answers all four required questions, includes alternatives, excludes future `ADR-025` scope and already-decided `ADR-022`/`ADR-023` scope, does not contradict `ADR-022`/`ADR-012`, updates `SLICE-04_BACKLOG.md`, and passes required validation.
- Requirement IDs: `ODY-S04-003`, `ADR-024`, `SLICE-04` prerequisite backlog item 3.
- In scope: `docs/adr/ADR-024_Development_Economy_And_Progression_Transactions_v1.0.md`, task contract, this ExecPlan, and `SLICE-04_BACKLOG.md` status row.
- Out of scope: production code, tests, schema, DTOs, Unity UI, Character aggregate boundary (`ADR-022`), Draft/template/approval (`ADR-023`), ownership/lifecycle/ruleset migration operations (`ADR-025`), ability/resource/anatomy mechanics themselves, concrete numeric ruleset balances.
- Required authorities: `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`, `AGENTS.md`, `PLANS.md`, `docs/tasks/TASK_TEMPLATE.md`, `docs/tasks/SLICE-04_BACKLOG.md` §3.3, roadmap §13.5/§13.9, Character/Progression §4/§11–14/§26–28, `ADR-002` (full read), `ADR-012` (full read), `ADR-022` (full read), and style precedents `ADR-022`/`ADR-023`.
- Required validation commands: `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`.

## 3. Current state

- Local `main` was fast-forwarded to `origin/main` at `f014961`, which includes PR #81 (`ODY-S04-002`/`ADR-023`, Accepted).
- `docs/tasks/SLICE-04_BACKLOG.md` lists `ODY-S04-003` as the third prerequisite ADR task, depending on `ODY-S04-001` only, and future `ADR-024`.
- `ADR-022` and `ADR-012` are fully read and provide the section-revision/lock/event-snapshot boundary and the append-only journal/compensating-event mechanism this ADR must reuse without redefining.
- No `ADR-024` file exists before this task.

Assumptions: none.

## 4. Proposed approach

Write a documentation-only ADR that specializes the already-accepted substrates instead of redefining them:

- place `DevelopmentPool`/`DevelopmentTransaction` inside the `ADR-022` Character aggregate's `Mechanics` section, with `DevelopmentTransaction` as a co-committed ledger projection rather than a second journal;
- rely on `ADR-002`'s `CommandId`/`AppliedCommands` and `ADR-012`'s one-transaction journal↔projection boundary as the sole idempotency/atomicity mechanisms for purchases;
- limit reservation to genuinely pending operations (skill 5+ recommendation), modeled as a domain-specific `ADR-002` §20 pending-workflow-equivalent event pair;
- fix `RevertAdvancementPurchase`/`CharacterRespec` as `ADR-012` §6 compensating operations, never direct edits, with `CharacterRespec` producing an inspectable ordered batch rather than one opaque event;
- fix `CriticalSuccessEvidence` single-use via a flag-plus-revision on the evidence row itself;
- update the parent backlog row to `Done`;
- create a task contract with validation evidence.

No production code, schema, tests, dependencies, or private product prose enters the repository.

## 5. Milestones

### M1 — ADR and contract authored

- [x] Create `ADR-024` with decisions, alternatives, exclusions, traceability, and normative action.
- [x] Create `ODY-S04-003` task contract with all 18 sections.
- [x] Update `SLICE-04_BACKLOG.md` row for `ODY-S04-003`.

### M2 — Validation and review readiness

- [x] Run `.\scripts\verify-format.ps1`.
- [x] Run `.\scripts\check-repository-policy.ps1`.
- [ ] Commit, push, and open Draft PR.
- [ ] Record CI status.

## 6. Progress log

- 2026-08-30 — Preflight confirmed `origin/main` includes PR #81 at `f014961`; created branch `feat/ody-s04-003-adr-development-economy`.
- 2026-08-30 — Read roadmap §13.5/§13.9, Character/Progression §4/§11–14/§26–28 in full, `ADR-022` in full, `ADR-002` (relevant sections already known from prior tasks this series, re-confirmed), `ADR-012` in full, and `SLICE-04_BACKLOG.md` §3.3.
- 2026-08-30 — Authored `ADR-024` resolving all four required questions plus considered alternatives.
- 2026-08-30 — Authored task contract and this ExecPlan; updated `SLICE-04_BACKLOG.md` row.
- 2026-08-30 — Local validation: `.\scripts\verify-format.ps1` and `.\scripts\check-repository-policy.ps1` (see section 9).

## 7. Decisions

- 2026-08-30 — Decision: use ExecPlan. Rationale: this ADR changes future public domain/persistence contracts, matching `ODY-S04-001`/`ODY-S04-002`'s own precedent and `SLICE-04_BACKLOG.md`'s "ExecPlan expected" note. Authority: `PLANS.md` §1.
- 2026-08-30 — Decision: `DevelopmentPool` is `Mechanics`-section data inside the `ADR-022` aggregate, not a subordinate aggregate. Rationale: avoids a second source of truth for Character history and avoids nested-command-handler orchestration across two aggregates for one purchase. Authority: `ADR-024` §4.
- 2026-08-30 — Decision: no technical spike. Rationale: all remaining questions are model/contract decisions over already-accepted substrates (`ADR-002`, `ADR-012`, `ADR-022`). Authority: `docs/tasks/SLICE-04_BACKLOG.md` §5.

## 8. Discoveries and deviations

- Confirmed this task depends only on `ODY-S04-001`, not `ODY-S04-002` — `SLICE-04_BACKLOG.md` §6 states this explicitly, and re-reading it before starting avoided incorrectly re-reading/re-citing `ADR-023` as a required authority for this task.
- Product §12.1's `DevelopmentTransaction.Kind` enum already lists every transaction kind this ADR needed (`Grant`/`Spend`/`Reserve`/`ReleaseReservation`/`Refund`/`Correction`/`RespecReturn`/`RespecSpend`) — no new `Kind` value was invented, which simplified the reservation/revert/respec design directly onto already-fixed product vocabulary.
- Adding this ExecPlan creates one additional docs file beyond the brief's expected three-file diff; consistent with `ODY-S04-001`/`ODY-S04-002`'s own precedent, required by `PLANS.md` because the ADR affects future public contracts.

## 9. Validation and acceptance evidence

- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed with `Repository policy check passed`.

## 10. Recovery and rollback

Rollback is a normal docs-only revert of this branch/PR. No production code, schema, migration, assets, dependencies, or generated artifacts are created.

## 11. Open questions and blockers

None.

## 12. Outcome and follow-up

Draft PR: <to be filled after `gh pr create`>. CI pending.
