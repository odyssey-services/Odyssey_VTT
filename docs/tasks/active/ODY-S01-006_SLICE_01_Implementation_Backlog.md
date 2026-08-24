# ODY-S01-006 — SLICE-01 Implementation Backlog Scaffold

**Status:** In Review  
**Roadmap stage / slice:** SLICE-01 (implementation revision)  
**Owner:** Codex (agent)  
**Requested by:** Product owner  
**Branch:** `feat/ody-s01-006-implementation-backlog-scaffold`  
**Pull request:** Not yet opened  
**ExecPlan:** Not required (Brief plan)  
**Created:** 2026-08-24  
**Last updated:** 2026-08-24 UTC

## 1. Goal

Create the organizational scaffold — this task contract and `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` — that decomposes roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` §10.3/§10.5/§10.6 into ordered, reviewable child implementation tasks (`ODY-S01-007` through `ODY-S01-014`), each activated separately and one at a time. This task does not implement any of those child tasks itself.

## 2. Why this task exists

- Problem or dependency being addressed: `docs/tasks/SLICE-01_BACKLOG.md` (the prerequisite ADR/spike revision) closed on 2026-08-24 with all five of its own exit criteria satisfied, and its §1 explicitly reserves creation of the vertical-slice implementation revision as the next step, not performed by that closure itself.
- Value or risk reduction: prevents the vertical slice's roadmap §10.3 scope from being implemented ad hoc, task-by-task, without a shared decomposition, ordering, and dependency record — the same discipline `SLICE-00_BACKLOG.md` and `SLICE-01_BACKLOG.md` (prerequisites) already established for this repository.
- Blocking or enabling relationship: blocks nothing by itself; enables `ODY-S01-007` (the first child implementation task) to begin under a reviewed, explicit scope rather than an improvised one.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1
- `docs/tasks/TASK_TEMPLATE.md`
- `docs/tasks/SLICE-01_BACKLOG.md` (prerequisite revision, closed — source of the "next step" this task fulfills)
- `docs/tasks/active/ODY-S01-000_SLICE_01_Local_Campaign_Prerequisites.md` (structural precedent for a pure scaffold task)
- `17_Roadmap_Odyssey_VTT_v0.11.md` §10.3, §10.5, §10.6 — private local reference, not committed to the repository; the sole source of this backlog's scope
- `docs/adr/ADR-011_Local_Campaign_Format_v1.1.md`, `ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md`, `ADR-013_Migration_Runner_v1.0.md`, `ADR-014_Owner_Key_Storage_Baseline_v1.0.md` (all `Accepted`) — the accepted authorities each child task group implements
- `docs/tasks/completed/ODY-S01-005_SP-02_Persistence_Reliability_Report.md` — empirical evidence cited in this task's §2 scope decisions (migration runner narrowing)
- `docs/tasks/SLICE-00_BACKLOG.md` — structural/granularity precedent for decomposing a slice into small, reviewable tasks

### Requirement and test IDs

- Requirement IDs: `SLICE-01` (implementation revision), roadmap section 10.3/10.5/10.6.
- Existing test IDs: None (this task introduces no code, so no `TC-*` registry entries).
- New test IDs to introduce: None.

### Task-safe private context

- Approved summary / references: roadmap §10.3's four "Входит" groups (Campaign Storage, Saving, Backups, Export baseline), §10.5's nine-step scenario, and §10.6's eight exit criteria are summarized/reproduced (§10.6's criteria verbatim, per that section's own short enumerable nature, same approach as `SLICE-01_BACKLOG.md`'s prerequisite revision used for its own exit criteria) into this task and `SLICE-01_IMPLEMENTATION_BACKLOG.md`. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `docs/adr/ADR-011_Local_Campaign_Format_v1.1.md`, `ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md`, `ADR-013_Migration_Runner_v1.0.md`, and `ADR-014_Owner_Key_Storage_Baseline_v1.0.md` all carry `**Статус:** Accepted` on `main` at commit `6acd05e`, confirmed by `grep` before branching.
- `docs/tasks/SLICE-01_BACKLOG.md` §1/§2 on `main` explicitly record the prerequisite revision as closed (all five exit criteria satisfied 2026-08-24) and explicitly reserve the vertical-slice implementation revision as the next step, confirmed by `Read`.
- No `docs/tasks/active/ODY-S01-006_*`, `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md`, or `docs/tasks/active/ODY-S01-007_*`–`014_*` file existed on `main` prior to this task.
- `docs/tasks/completed/ODY-S01-005_SP-02_Persistence_Reliability_Report.md` §2.4 documents the empirical migration-failure/rollback finding cited in this task's §2.1 scope decision, confirmed by `Read`.
- `docs/adr/ADR-014_Owner_Key_Storage_Baseline_v1.0.md` §11.3/§12.2 explicitly defers UX/application-level owner-key-absence behavior to future Networking/Account ADRs, confirmed by `Read`, supporting this task's §2.2 exclusion decision.

### Assumptions

- None. All facts above were directly observed via `Read`/`grep` on the current `main` branch before branching for this task.

## 5. Scope

### In scope

- `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` (new): the implementation-revision backlog document — purpose, scope-decision justifications (§2, migration runner and owner key storage), exit criteria (roadmap §10.6 verbatim), ordered child-task decomposition (`ODY-S01-007`–`014`), task boundaries, dependency rules, global non-goals, backlog change control.
- `docs/tasks/active/ODY-S01-006_SLICE_01_Implementation_Backlog.md` (this file).
- `docs/tasks/SLICE-01_BACKLOG.md` §1 — one pointer line recording that the implementation revision now exists as `SLICE-01_IMPLEMENTATION_BACKLOG.md`; no other content in that file changes.

### Out of scope

- Any implementation code (C#, SQL, Unity) for any of `ODY-S01-007`–`014`.
- Creating any `ODY-S01-007`–`014` task contract file — this task only reserves their numbers, titles, and boundaries inside the backlog document.
- Deciding any technical question beyond the two explicit scope narrowings in `SLICE-01_IMPLEMENTATION_BACKLOG.md` §2 (migration runner scope, owner key storage exclusion).
- Rewriting or otherwise editing the closed `docs/tasks/SLICE-01_BACKLOG.md` beyond the single §1 pointer line.
- Any change to `ADR-011`–`014`, `Documentation/`, or `docs/tasks/completed/`.

### Allowed paths

```text
docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S01-006_SLICE_01_Implementation_Backlog.md
docs/tasks/SLICE-01_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: Not applicable — documentation-only, no code.
- Authoritative-state and transaction boundary: Not applicable — this task introduces no persisted state; it only records which future tasks will.
- Serialization / compatibility boundary: Not applicable.
- Time / RNG rule: Not applicable.
- Unity / thread / lifetime rule: Not applicable.
- Dependency / licensing rule: Not applicable — no dependency introduced. (`SLICE-01_IMPLEMENTATION_BACKLOG.md` §5 notes that `ODY-S01-007`, when it is later activated, will be the first task to add `Microsoft.Data.Sqlite` as a real production dependency per `ADR-011` v1.1 — that addition happens in that future task, not this one.)
- Security / privacy / redaction rule: Not applicable.
- Performance or platform constraint: Not applicable.
- Other: the two scope-narrowing decisions (§2.1 migration runner, §2.2 owner key storage) must each be explicitly justified in the backlog document, not silently decided — this is the task's own explicit instruction and is treated as a hard constraint on this task's output.

## 7. Expected behavior

This is a pure organizational scaffold; "behavior" is expressed as required document content rather than runtime scenarios.

### Required invariants

- `SLICE-01_IMPLEMENTATION_BACKLOG.md` sources its scope exclusively from roadmap §10.3/§10.5/§10.6, not from invented requirements.
- The migration-runner scope decision (§2.1) is explicit, justified with reasoning grounded in the roadmap text, `05_Persistence` §25.2, and the `SP-02` report — not asserted without justification, and not silently decided either way.
- The owner-key-storage exclusion decision (§2.2) is explicit, justified with reasoning grounded in the roadmap text and `ADR-014`'s own stated open questions — not asserted without justification, and not silently decided either way.
- The exit-criteria section reproduces all eight roadmap §10.6 criteria, with the migration criterion explicitly annotated as satisfied only at the registry-baseline level per §2.1's narrowing, not silently reworded to hide the narrowing.
- The ordered backlog reserves child task IDs `ODY-S01-007` through `ODY-S01-014` with titles and boundaries, but creates no child task contract files.
- Every child task's "Planning mode" column is explicitly left "Not yet determined," matching the convention `SLICE-01_BACKLOG.md` (prerequisites) already used, not pre-decided by this scaffold.
- Dependency rules between child tasks are stated explicitly, not left implicit.
- Global non-goals explicitly exclude networking, permissions runtime, and character/combat/dice/content systems, consistent with roadmap §10.3 not naming them for Stage 2.
- The predecessor `SLICE-01_BACKLOG.md` is not rewritten — only a single pointer line is added to its §1.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` (new), `docs/tasks/active/ODY-S01-006_SLICE_01_Implementation_Backlog.md` (this file), one pointer line in `docs/tasks/SLICE-01_BACKLOG.md` §1.
- Generated evidence or build artifacts: validation command output recorded in §17.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` exists and sources its scope exclusively from roadmap §10.3/§10.5/§10.6.
2. The migration-runner scope decision is explicitly stated and justified in the backlog document's §2.1, with the narrowed scope (registry baseline only) clearly distinguished from full `ADR-013` runner implementation.
3. The owner-key-storage exclusion decision is explicitly stated and justified in the backlog document's §2.2.
4. The exit-criteria section reproduces all eight roadmap §10.6 criteria, with the migration criterion's registry-baseline narrowing explicitly annotated, not hidden.
5. The ordered backlog lists `ODY-S01-007` through `ODY-S01-014` with titles, primary results, and dependency relationships, and no child task contract file is created by this task.
6. `docs/tasks/SLICE-01_BACKLOG.md` shows exactly one added pointer line in §1; no other content in that file changes.
7. `.\scripts\verify-format.ps1` and `.\scripts\check-repository-policy.ps1` both pass.
8. `git diff --name-status` against `main` shows only the three files listed in §5's Allowed paths.
9. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| None | — | Documentation-only task; no code paths exist to test | — |

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- Cross-read `SLICE-01_IMPLEMENTATION_BACKLOG.md` against roadmap §10.3/§10.5/§10.6 to confirm no invented scope and no silently dropped roadmap item.
- Cross-read the migration-runner scope decision against `05_Persistence` §25.2 and `SP-02` report §2.4 to confirm the reasoning is grounded, not asserted.
- Cross-read the owner-key-storage exclusion against `ADR-014` §11.3/§12.2 to confirm the reasoning is grounded, not asserted.
- Confirmed via `Read` that `docs/tasks/SLICE-01_BACKLOG.md`'s only change is the single §1 pointer line.

### Required environments / profiles

- OS / architecture: Not applicable (documentation-only)
- Unity editor or Player profile: Not applicable
- Scripting backend: Not applicable
- Network topology or database fixture: Not applicable
- Other: None

### Validation not required by this task

- Build, EditMode/PlayMode tests, or Player smoke: not required — no code is touched by this task.

## 11. Compatibility, migration, and rollback

Not applicable. This task produces only organizational documentation; it does not itself change any persisted format, schema, contract, protocol, package, or deployable artifact.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: none — this task's content is organizational scope decisions, not data.
- Trust boundaries: Not applicable.
- Authorization / audience checks: Not applicable.
- Redaction requirements: Not applicable.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: None.

## 14. Planning and execution mode

- Planning mode: `Brief plan`
- Reason for selected mode: evaluated fresh against `PLANS.md` §1.2's triggers, following the same reasoning `ODY-S01-000` used for its own scaffold task (consulted, not copied verbatim): this task introduces no new architecture, module, public contract, persisted format, protocol, permissions model, dependency graph, Unity/package version, or build pipeline change — it is pure organizational documentation reserving future task numbers and boundaries. It does not span multiple milestones or PRs (single Draft PR), does not change any production module (zero code touched), does not affect authoritative state, persistence, networking, security, permissions, hidden information, redaction, diagnostics, time, or randomness (it only *describes*, at a planning level, which future tasks will), has one clear implementation path (write the backlog document and this task contract), and completes in one focused pull request with no migration or recovery procedure required — matching every `PLANS.md` §1.1 Brief-plan-eligibility criterion.
- ExecPlan path: Not required.
- Expected pull request count: 1 (this scaffold). Each subsequent `ODY-S01-007`–`014` child task will be its own separate task and pull request, not part of this activation — matching `ODY-S01-000`'s own milestone/sequencing constraint for its child ADR/spike tasks.
- Milestone or sequencing constraints: Do not create any `ODY-S01-007`–`014` child task contract until this scaffold is reviewed. Do not begin implementation of any child task's scope under this task.

## 15. Documentation and versioning impact

- Documents that must change: `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` (new), `docs/tasks/active/ODY-S01-006_SLICE_01_Implementation_Backlog.md` (this file, new), `docs/tasks/SLICE-01_BACKLOG.md` (one pointer line in §1 only).
- Documents that must not change: `ADR-011`–`014`, `docs/tasks/completed/ODY-S01-001`–`005` and their reports, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything under `Documentation/`.
- Application version change: No — this task does not touch `Odyssey.*` code or `BuildIdentity`.
- Schema / format / contract / protocol / ruleset version change: None — no schema is created or implied in code by this task.
- Documentation version changes: None — no versioned document (ADR, baseline) changes version by this task.
- Changelog or release-note requirement: None — pre-implementation organizational scaffold, no production-facing change.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [x] Required automated tests pass. (None applicable — documentation-only.)
- [ ] Required manual checks are completed.
- [ ] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable. (Not applicable — see §11.)
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [ ] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` — new implementation-revision backlog document.
- `docs/tasks/active/ODY-S01-006_SLICE_01_Implementation_Backlog.md` — this task contract.
- `docs/tasks/SLICE-01_BACKLOG.md` — one pointer line added to §1.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Pending | To be recorded before commit. |
| `.\scripts\check-repository-policy.ps1` | Pending | To be recorded before commit. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Pending | — |
| AC-2 | Pending | — |
| AC-3 | Pending | — |
| AC-4 | Pending | — |
| AC-5 | Pending | — |
| AC-6 | Pending | — |
| AC-7 | Pending | — |
| AC-8 | Pending | — |
| AC-9 | Pending | — |

## 18. Blockers, risks, and open decisions

- Blocker: none. The prerequisite `SLICE-01_BACKLOG.md` revision is closed, confirmed in §4.
- Open decision (deliberate, not a blocker): `SLICE-01_IMPLEMENTATION_BACKLOG.md` §2 records two explicit scope-narrowing decisions (migration runner deferred to registry-baseline only; owner key storage excluded entirely) — both are intentional, justified scope decisions made by this task, not omissions, and both are explicitly reversible by a future backlog amendment if circumstances change (see that document's §8).
- Risk: none identified — this task carries no implementation risk since it produces no code.
