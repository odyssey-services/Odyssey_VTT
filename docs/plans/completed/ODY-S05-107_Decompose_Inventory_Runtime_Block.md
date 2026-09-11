# ODY-S05-107 — Decompose Inventory Runtime Block

**Status:** Done (PR #112, merged into main)
**Owner:** Codex (agent)
**Branch:** `docs/ody-s05-107-inventory-runtime-backlog`
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/112
**Last updated:** 2026-09-06 UTC

## 1. Purpose and user-visible outcome

Prepare the next `SLICE-05` implementation wave after Content Catalog MVP closure by decomposing Inventory runtime into small, reviewable future tasks. The result is a planning-only backlog update and task contract, not product code.

## 2. Task contract

- Goal: add the `ODY-S05-107` task contract/brief plan and update `SLICE-05_IMPLEMENTATION_BACKLOG.md` so Inventory runtime is decomposed after `ODY-S05-101`-`106`.
- Acceptance criteria: Content Catalog MVP marked complete; Inventory runtime decomposed into small implementation tasks; each task has purpose/dependencies/planning mode/in-scope/out-of-scope; `ADR-027` boundaries preserved; no product code/schema/test implementation or accepted ADR changes; Draft PR description identifies the work as planning/decomposition only.
- Requirement IDs: `ODY-S05-107`, `SLICE-05`.
- In scope: `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, this task contract, this brief plan.
- Out of scope: product code, schema, migrations, runtime tests, Unity/UI, real item commands, equipment behavior, attack resolution, ActiveEffect runtime, ItemDefinition migration workflow, balanced content, `.odcontent`, accepted ADR edits.
- Required authorities: `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, `ADR-027` sections 5-12/20, Domain Model sections 16-18, Roadmap section 14, Content Catalog task contracts `ODY-S05-101`-`106`.
- Required validation commands: `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- `git fetch origin` completed.
- Branch `docs/ody-s05-107-inventory-runtime-backlog` was created from `origin/main`.
- `origin/main` is merge commit `57274d5`, PR #111 (`ODY-S05-106`).
- PR #105/#106/#107/#108/#109/#110/#111 are all merged.
- `SLICE-05_IMPLEMENTATION_BACKLOG.md` still listed `ODY-S05-106` as `In Review`; this task updates it to `Done` with PR #111.
- `ADR-027` already decides Inventory/runtime boundaries; no ADR contradiction was found.

Assumptions: none.

## 4. Proposed approach

Update only planning docs:

- Mark `ODY-S05-106` complete in the existing Content Catalog MVP table.
- Add an Inventory runtime block after the completed Content Catalog MVP block.
- Reserve `ODY-S05-201`-`207` for Inventory foundation, persistence, create-from-Published-definition commands, move/transfer, stack split/merge, runtime dependency checks, and integration fixtures.
- Keep Equipment runtime, item use/effects, ItemDefinition migration workflow, attack pipeline, balanced content, `.odcontent`, and Unity UI as later reserved blocks unless minimally referenced for Inventory location/dependency modeling.
- Record this task contract and validation evidence.

## 5. Milestones

### M1 — Backlog and task docs

- [x] Add `docs/tasks/active/ODY-S05-107_Decompose_Inventory_Runtime_Block.md`.
- [x] Add this brief plan.
- [x] Update `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` with `ODY-S05-106` Done and Inventory runtime task decomposition.

### M2 — Validation and PR

- [x] Review diff for docs-only scope.
- [x] Run `.\scripts\verify-format.ps1`.
- [x] Run `.\scripts\check-repository-policy.ps1`.
- [x] Run `.\scripts\verify-test-structure.ps1`.
- [x] Commit, push, and open Draft PR.

## 6. Progress log

- 2026-09-06 — Preflight: fetched `origin`, created branch from `origin/main`, verified PR #105-#111 are merged and #111 is the current `origin/main` merge commit.
- 2026-09-06 — Read the required backlog, ADR-027, Domain Model/Roadmap sections, Content Catalog task contract summaries, `TASK_TEMPLATE.md`, and `PLANS.md`.
- 2026-09-06 — Updated `SLICE-05_IMPLEMENTATION_BACKLOG.md`, added this task contract, and added this brief plan.
- 2026-09-06 — Validation passed: `verify-format.ps1`, `check-repository-policy.ps1`, and `verify-test-structure.ps1`.
- 2026-09-06 — Opened Draft PR #112: https://github.com/odyssey-services/Odyssey_VTT/pull/112.

## 7. Decisions

- 2026-09-06 — Decision: use Brief plan, not ExecPlan. Rationale: docs/planning-only; no public runtime contract, schema, permission behavior, production code, or ADR change. Authority: `PLANS.md` section 1.1 and `ODY-S05-107` scope.
- 2026-09-06 — Decision: reserve `ODY-S05-201`-`207` for the Inventory runtime block, leaving Equipment runtime as the next reserved block. Rationale: this matches the user-provided sequence and avoids over-decomposing later mechanics before Inventory location/runtime state exists. Authority: `ADR-027` sections 5-12 and Roadmap section 14.
- 2026-09-06 — Decision: do not edit accepted ADR sections. Rationale: no contradiction found; this task applies accepted architecture to planning only. Authority: user out-of-scope instruction.

## 8. Discoveries and deviations

- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` still marked `ODY-S05-106` as `In Review` even though PR #111 is merged into `origin/main`; corrected as part of the required preflight.
- Current Content Catalog task contracts remain in `docs/tasks/active/` with `In Review` status even after their PRs merged; this task does not move or rewrite those existing contracts because its required update is the consolidated `SLICE-05_IMPLEMENTATION_BACKLOG.md` status.
- No architecture contradiction was found.

## 9. Validation and acceptance evidence

- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed with `Repository policy check passed`.
- `.\scripts\verify-test-structure.ps1`: passed with `TC-ARCH-001 PASS valid ADR-001 graph passes` and controlled invalid cases rejected.
- Diff review: changed only `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, this task contract, and this brief plan. No product code, schema, tests, Unity files, or accepted ADR files changed.

## 10. Recovery and rollback

Rollback is a normal revert of this documentation-only branch. No schema, migration, build artifact, dependency, or runtime state is changed.

## 11. Open questions and blockers

None.

## 12. Outcome and follow-up

Draft PR: https://github.com/odyssey-services/Odyssey_VTT/pull/112. Follow-up implementation tasks reserved: `ODY-S05-201` through `ODY-S05-207`.
