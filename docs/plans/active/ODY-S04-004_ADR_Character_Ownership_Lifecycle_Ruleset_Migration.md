# ODY-S04-004 — ADR Character Ownership, Lifecycle, and Ruleset Migration Operations

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s04-004-adr-ownership-lifecycle-migration`
**Pull request:** <to be filled after `gh pr create`>
**Last updated:** 2026-08-31 UTC

## 1. Purpose and user-visible outcome

Accept `ADR-025`, which fixes Character-specific owner/co-owner/controller semantics over `ADR-019`'s baseline, archive/dependency-aware physical delete, Dead/`CharacterRestored` invariants, and Character Ruleset migration preview/snapshot/rollback (including its boundary with `ADR-013` and its interaction with `.odchar` import) — closing all four `SLICE-04` prerequisite ADR gaps.

## 2. Task contract

- Goal: create accepted `ADR-025` and task evidence for `ODY-S04-004`.
- Acceptance criteria: ADR exists, answers all four required questions, includes alternatives, excludes already-decided `ADR-022`/`ADR-023`/`ADR-024` scope and any `ADR-019` role extension, does not contradict `ADR-019`/`ADR-013`, updates `SLICE-04_BACKLOG.md`, passes required validation, and confirms all four prerequisite ADRs are now `Accepted`.
- Requirement IDs: `ODY-S04-004`, `ADR-025`, `SLICE-04` prerequisite backlog item 4 (final).
- In scope: `docs/adr/ADR-025_Character_Ownership_Lifecycle_And_Ruleset_Migration_Operations_v1.0.md`, task contract, this ExecPlan, and `SLICE-04_BACKLOG.md` status row.
- Out of scope: production code, tests, schema, DTOs, Unity UI, Character aggregate boundary (`ADR-022`), Draft/template/approval (`ADR-023`), development economy (`ADR-024`), ability/resource/anatomy mechanics, the `.odchar` file format itself, any `ADR-019` role/permission extension.
- Required authorities: `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`, `AGENTS.md`, `PLANS.md`, `docs/tasks/TASK_TEMPLATE.md`, `docs/tasks/SLICE-04_BACKLOG.md` §3.5, roadmap §13.7/§13.9, Character/Progression §4/§19/§22–25/§26–28, `ADR-019` (full read), `ADR-022` (full read), `ADR-023`/`ADR-024` (relevant sections), `ADR-012`/`ADR-013` (full read), and style precedents `ADR-022`/`ADR-023`/`ADR-024`.
- Required validation commands: `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`.

## 3. Current state

- Local `main` was fast-forwarded to `origin/main` at `be28562`, which includes PR #82 (`ODY-S04-003`/`ADR-024`, Accepted) — independently verified via `git merge-base --is-ancestor`, not just `gh pr view`'s reported state.
- `docs/tasks/SLICE-04_BACKLOG.md` lists `ODY-S04-004` as the fourth and final prerequisite ADR task, depending on all three preceding tasks, and future `ADR-025`.
- `ADR-019`, `ADR-022`, and `ADR-013` are fully read and provide the role baseline, aggregate/section boundary, and schema-migration-runner boundary this ADR must specialize/fill without redefining.
- No `ADR-025` file exists before this task.

Assumptions: none.

## 4. Proposed approach

Write a documentation-only ADR that specializes the already-accepted substrates instead of redefining them:

- place `CharacterOwnership` inside `ADR-022`'s already-reserved `Ownership` section, gating all ownership/control-grant commands behind `Character.ManageOwnership` (MainGM-only), specializing `ADR-019`'s "assigned character" concept without adding a role;
- fix archive as an ordinary `Lifecycle`-section transition and physical delete as a dependency-gated, MainGM-only, host-revalidated operation that never deletes `DomainEvents`, relying on `ADR-022`'s already-anticipated historical-snapshot survival;
- fix the Dead transition to `HostSystem`/`GMOverride` issuers only, leaving `ADR-024` reservations untouched, and model `CharacterRestored` as a forward (not compensating) event;
- fix Character Ruleset migration as its own `Preview`/`Apply` pair, explicitly bounded against `ADR-013`'s schema migration runner (per `ADR-013` §9's own anticipation of this exact ADR), reusing ordinary transaction atomicity for failure rollback and `ADR-024`'s compensating-batch pattern for post-commit reversal, and reusing `ADR-023`'s unmodified Draft-binding pipeline for `.odchar` import;
- update the parent backlog row to `Done` and confirm all four prerequisite ADRs are now `Accepted`;
- create a task contract with validation evidence.

No production code, schema, tests, dependencies, or private product prose enters the repository.

## 5. Milestones

### M1 — ADR and contract authored

- [x] Create `ADR-025` with decisions, alternatives, exclusions, traceability, and normative action.
- [x] Create `ODY-S04-004` task contract with all 18 sections.
- [x] Update `SLICE-04_BACKLOG.md` row for `ODY-S04-004`.

### M2 — Validation and review readiness

- [x] Run `.\scripts\verify-format.ps1`.
- [x] Run `.\scripts\check-repository-policy.ps1`.
- [ ] Commit, push, and open Draft PR.
- [ ] Record CI status.

## 6. Progress log

- 2026-08-31 — Preflight confirmed PR #82's merge commit is a real ancestor of `origin/main` at `be28562` (`git merge-base --is-ancestor`, not just `gh pr view` status); created branch `feat/ody-s04-004-adr-ownership-lifecycle-migration`.
- 2026-08-31 — Read roadmap §13.7/§13.9, Character/Progression §4/§19/§22–25/§26–28 in full, `ADR-019` in full (re-confirmed relevant sections), `ADR-022` in full (re-confirmed), `ADR-023`/`ADR-024` (relevant sections, re-confirmed from prior tasks this series), `ADR-012` (re-confirmed relevant sections), and `ADR-013` in full (new to this task, especially §9's explicit ruleset-migration boundary).
- 2026-08-31 — Authored `ADR-025` resolving all four required questions plus considered alternatives.
- 2026-08-31 — Authored task contract and this ExecPlan; updated `SLICE-04_BACKLOG.md` row.
- 2026-08-31 — Local validation: `.\scripts\verify-format.ps1` and `.\scripts\check-repository-policy.ps1` (see section 9).

## 7. Decisions

- 2026-08-31 — Decision: use ExecPlan. Rationale: this ADR changes future public domain/persistence/permission contracts, matching `ODY-S04-001`/`002`/`003`'s own precedent and `SLICE-04_BACKLOG.md`'s "ExecPlan expected" note. Authority: `PLANS.md` §1.
- 2026-08-31 — Decision: verify PR #82's real merge state via `git merge-base --is-ancestor`, not `gh pr view` alone. Rationale: this task's own ТЗ explicitly required this precaution, citing an earlier `SLICE-UI-01` lesson about a GitHub-reported status not matching actual branch ancestry. Authority: this task's own preflight instruction.
- 2026-08-31 — Decision: no technical spike. Rationale: all remaining questions are model/contract decisions over already-accepted substrates (`ADR-002`, `ADR-012`, `ADR-013`, `ADR-019`, `ADR-022`, `ADR-023`, `ADR-024`). Authority: `docs/tasks/SLICE-04_BACKLOG.md` §5.

## 8. Discoveries and deviations

- `ADR-019` §10's own `PERM-INV-007`/`008` deferral rows, and `ADR-013` §9's own explicit statement that Character Ruleset migration "must be defined by a separate ADR... when Rules Engine/Content Domain reach that stage," both directly and explicitly anticipate this exact ADR — this was verified directly rather than assumed, giving strong textual confirmation that closing these gaps here is not an unapproved expansion of either prior ADR.
- Product §26 names no distinct permission for granting Character control separately from `Character.ManageOwnership` — resolved conservatively (ADR §4.3) by keeping all ownership/control-membership changes under the single already-MainGM-only permission, rather than inventing a new delegation-capable permission; recorded explicitly as a judgment call, not smoothed over.
- Adding this ExecPlan creates one additional docs file beyond the brief's expected three-file diff; consistent with `ODY-S04-001`/`002`/`003`'s own precedent, required by `PLANS.md` because the ADR affects future public contracts.

## 9. Validation and acceptance evidence

- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed with `Repository policy check passed`.

## 10. Recovery and rollback

Rollback is a normal docs-only revert of this branch/PR. No production code, schema, migration, assets, dependencies, or generated artifacts are created.

## 11. Open questions and blockers

None. This is the final `SLICE-04` prerequisite ADR task; once merged, `SLICE-04_BACKLOG.md` §2's exit criteria are fully satisfied.

## 12. Outcome and follow-up

Draft PR: <to be filled after `gh pr create`>. CI pending.
