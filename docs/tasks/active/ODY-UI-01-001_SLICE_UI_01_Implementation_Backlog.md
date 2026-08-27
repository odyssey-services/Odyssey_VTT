# ODY-UI-01-001 — SLICE-UI-01 Implementation Backlog

**Status:** In Review
**Roadmap stage / slice:** SLICE-UI-01 (minimal trial UI; see `SLICE-UI-01_BACKLOG.md` §0 for the rename history from `SLICE-04`)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-ui-01-001-slice-ui-01-implementation-backlog`
**Pull request:** Draft — [#66](https://github.com/odyssey-services/Odyssey_VTT/pull/66) (open, CI green, awaiting owner review)
**ExecPlan:** Not required — see §14 (Brief plan)
**Created:** 2026-08-27
**Last updated:** 2026-08-27 UTC

## 1. Goal

Decompose `SLICE-UI-01_BACKLOG.md` §3.4's already-agreed minimal screen/action list into an ordered set of small, reviewable child tasks and record them in `docs/tasks/SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` — the same role `ODY-S03-003` played for `SLICE-03` after its own prerequisite backlog closed. No UI code is written by this task; it only organizes and sequences future implementation work.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-UI-01_BACKLOG.md` (the prerequisite revision, formerly `SLICE-04`) closed with zero new ADRs and a fully-decided minimal screen/action list (§3.4), UI↔Application boundary (§3.1/3.2), role-switching convention (§3.3), and persistence choice (§3.5) — but no organizational structure exists yet to turn that list into actual, separately-reviewable implementation tasks.
- Value or risk reduction: without this decomposition, whoever picks up "build the trial UI" next would either build it as one large, unreviewable task, or invent their own ad hoc splitting on the fly — the same risk `ODY-S03-003` existed to prevent for `SLICE-03`'s own vertical slice.
- Blocking or enabling relationship: blocks all `SLICE-UI-01` implementation task activations (none may begin before this backlog exists and is reviewed). Enables `ODY-UI-01-002` through `ODY-UI-01-007`, each a separate future task activation.

## 3. Authorities and requirement references

### Required authorities

- `docs/tasks/SLICE-UI-01_BACKLOG.md` §3.4 (the minimal screen/action list — the sole source of scope for this decomposition) and §3.1–3.5 (already-decided UI↔Application boundary, persistence, role-switching convention — cited, not reopened).
- `docs/tasks/active/ODY-S03-008_Vertical_Slice_Integration.md` — the ten-step scenario these screens exist to let a human walk by hand; the source of what each child task must ultimately support.
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md` §6.7 — the already-`Accepted` `Odyssey.Unity.Client` boundary; `DeveloperShellPresenter.cs`/`AppShell.uxml` cited as the existing structural precedent every child task will follow.
- `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` — structural precedent for this document's own format (Purpose, Scope decisions, Ordered backlog, Task boundaries, Dependency rules, Global non-goals), not its content.

### Requirement and test IDs

- Requirement IDs: `SLICE-UI-01` (implementation revision).
- Existing test IDs: None cited as new evidence; this task performs no test run of its own beyond confirming `main`'s existing green state.
- New test IDs to introduce: None — each future child task defines its own if needed (most will be manual/UI verification, not automated `TC-*` IDs, per the trial-UI's own throwaway-quality framing).

### Task-safe private context

- Approved summary / references: None.

## 4. Verified current state

### Verified facts

- `SLICE-UI-01_BACKLOG.md`/`ODY-UI-01-000` (the prerequisite revision, including its `SLICE-04`→`SLICE-UI-01` rename point-fix) are merged into `main` — confirmed via `git fetch origin main && git merge --ff-only` and `git log --oneline -6` before this task's branch was created (`0343868` = merge of PR #65).
- `SLICE-UI-01_BACKLOG.md` §3.4 lists exactly eight UI elements plus reroll/cancel buttons, each already justified against a specific `ODY-S03-008` scenario step or a specific already-built Application contract — confirmed by `Read`. This task does not re-derive that list; it only groups and sequences it.
- No `ODY-UI-01-00X` (X ≥ 2) task contract file exists anywhere in the repository — confirmed by `Glob`.

### Assumptions

- None. All facts above were directly observed via `Read`/`Glob`/`git log` during this task.

## 5. Scope

### In scope

- Creating `docs/tasks/SLICE-UI-01_IMPLEMENTATION_BACKLOG.md`, grouping `SLICE-UI-01_BACKLOG.md` §3.4's screen/action list into six ordered child tasks (`ODY-UI-01-002` through `ODY-UI-01-007`), each with a stated dependency, deliverable, and the `ODY-S03-008` step(s)/exit criterion it closes.
- This task contract itself.

### Out of scope

- Any UI implementation code, Unity scene, script, or asset. Confirmed: this task's diff touches only documentation files.
- Creating or modifying any `ODY-UI-01-002`–`007` task contract file — reserved by number/title/boundary only, per the `ODY-S03-003` precedent.
- Reopening any decision `SLICE-UI-01_BACKLOG.md` §3.1–3.5 already made.
- Anything `SLICE-UI-01_BACKLOG.md` §3.4 already explicitly excluded (drawing, ruler, drag-and-drop polish, animation, sound, multiple scenes, pan/zoom polish, hex-grid rendering, localization, mobile/web).
- Real network integration (`ODY-S02-014`/`ADR-016` §14).

### Allowed paths

```text
docs/tasks/active/ODY-UI-01-001_SLICE_UI_01_Implementation_Backlog.md
docs/tasks/SLICE-UI-01_IMPLEMENTATION_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Assets/**
Packages/**
docs/tasks/SLICE-UI-01_BACKLOG.md (already-closed prerequisite revision; read-only source)
docs/tasks/active/ODY-UI-01-000_SLICE_UI_01_Minimal_UI_Prerequisites.md (read-only source)
Any production code, test code, script, Unity, or package file
```

## 6. Technical constraints

- Module ownership and dependency direction: Not applicable to this scaffold itself — each future child task's own module-boundary work is already fixed by `ADR-001` §6.7, cited not reopened.
- Authoritative-state and transaction boundary: Not applicable.
- Serialization / compatibility boundary: Not applicable.
- Time / RNG rule: Not applicable to this task directly.
- Unity / thread / lifetime rule: Not applicable — no Unity code is written.
- Dependency / licensing rule: No new dependency, GitHub Action, Unity package, executable, or downloadable tool is introduced.
- Security / privacy / redaction rule: Not applicable.
- Performance or platform constraint: Not applicable.
- Other: None.

## 7. Expected behavior

### Scenario 1 — the minimal screen list is fully decomposed, nothing added or dropped

**Given** `SLICE-UI-01_BACKLOG.md` §3.4's already-agreed list
**When** this task groups it into child tasks
**Then** every element of that list maps to exactly one child task, no new UI element is invented, and none of §3.4's explicit exclusions reappears in any child task's scope.

### Required invariants

- No new architectural or scope decision is made — every child task's boundary traces to `SLICE-UI-01_BACKLOG.md` §3.4/§3.1–3.5 or to an `ODY-S03-008` scenario step.
- No child task contract file is created by this task.
- No production code, UI, or Unity asset is introduced.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: This task contract; `docs/tasks/SLICE-UI-01_IMPLEMENTATION_BACKLOG.md`.
- Generated evidence or build artifacts: None.
- Migration / recovery material: Not applicable.

## 9. Acceptance criteria

1. `docs/tasks/SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` exists, structured analogously to `SLICE-03_IMPLEMENTATION_BACKLOG.md` (Purpose, Scope decisions, Ordered backlog table, Task boundaries, Dependency rules, Global non-goals).
2. Exactly six child tasks (`ODY-UI-01-002`–`007`) are listed, each stating its dependency, deliverable, and the `ODY-S03-008` step(s)/exit criterion it closes.
3. Every element of `SLICE-UI-01_BACKLOG.md` §3.4's list maps to exactly one child task; every explicit exclusion from §3.4 is repeated as a non-goal, not silently dropped or reintroduced.
4. No child task contract file (`ODY-UI-01-002...md` etc.) exists as a result of this task.
5. No production code, UI scene, script, or Unity asset exists as a result of this task.
6. `.\scripts\verify-format.ps1` and `.\scripts\check-repository-policy.ps1` both pass.
7. `git diff --name-status` against `main` shows only the two files listed in §5's Allowed paths.
8. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

The task is not complete while any mandatory criterion is unverified, failed, or silently deferred.

## 10. Tests and validation

### Required automated tests

None. This is a documentation-only decomposition task; no new test ID is introduced.

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- Owner review of the six-task grouping and dependency order before any `ODY-UI-01-00X` child task is activated.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 (PowerShell validation only).
- Unity editor or Player profile: Not applicable — no Unity code is touched.
- Scripting backend: Not applicable.
- Network topology or database fixture: None.
- Other: None.

### Validation not required by this task

- `dotnet build`/`dotnet test`, Unity compile/EditMode/PlayMode — none is affected because no production code, test code, script, Unity asset, package, or CI workflow file is touched.

## 11. Compatibility, migration, and rollback

Not applicable. This task introduces no persisted state, public contract, protocol, package, Unity version, or build identity change.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: None new.
- Trust boundaries: Not applicable.
- Authorization / audience checks: Not applicable — this task organizes future work, introduces no new authorization logic itself.
- Redaction requirements: Not applicable.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: None.

## 14. Planning and execution mode

- Planning mode: `Brief plan`
- Reason for selected mode: Checked against `PLANS.md` §1's conditions individually, the same discipline `ODY-S03-003` used for its own analogous scaffold. (1) Contained in one area — two documentation files, no production module touched. (2) Does not change a public contract, persisted format, protocol, permissions model, dependency graph, package version, or build pipeline — this task decides no technical question; it only groups and sequences an already-fixed list. (3) One clear path — read `SLICE-UI-01_BACKLOG.md` §3.4/§3.1–3.5 and `ODY-S03-008`, group into six tasks, write the two files. (4) Fits one focused pull request. (5) No migration or recovery procedure required. `PLANS.md` §1.2's ExecPlan triggers do not apply: no port/DTO/event/command/schema/protocol/manifest/package/build-profile/migration is introduced or changed by this task.
- Brief plan:
  1. Files inspected: `SLICE-UI-01_BACKLOG.md` §3.1–3.5 (all decisions, cited not reopened); `ODY-S03-008`'s own ten-step scenario; `ADR-001` §6.7 (cited); `SLICE-03_IMPLEMENTATION_BACKLOG.md` (structural format precedent).
  2. Intended change: `SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` (six ordered child tasks), this task contract.
  3. Validation: `verify-format.ps1`/`check-repository-policy.ps1`.
  4. Non-goals: no UI code, no child task contract file, no reopened `SLICE-UI-01_BACKLOG.md` decision.
- ExecPlan path: Not required.
- Expected pull request count: 1.
- Milestone or sequencing constraints: Do not activate any `ODY-UI-01-002`–`007` child task contract until this backlog is reviewed and accepted.

## 15. Documentation and versioning impact

- Documents that must change: This task contract; `docs/tasks/SLICE-UI-01_IMPLEMENTATION_BACKLOG.md`.
- Documents that must not change: All ADRs, `SLICE-UI-01_BACKLOG.md`, `ODY-UI-01-000`'s task contract, `ODY-S03-004`–`010` task contracts, `Assets/**`, `Packages/**`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass (none required).
- [x] Required manual checks are completed (owner review pending — see Pull request note).
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [x] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `docs/tasks/SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` — new.
- `docs/tasks/active/ODY-UI-01-001_SLICE_UI_01_Implementation_Backlog.md` — this task contract.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | All `REPO-POLICY-*`/`TC-CI-*` checks passed; `Repository policy check passed.` |
| CI — Draft PR [#66](https://github.com/odyssey-services/Odyssey_VTT/pull/66), commit `6808985` | Passed | Run [33030617453](https://github.com/odyssey-services/Odyssey_VTT/actions/runs/33030617453): `repository-policy-format-structure`, `dotnet-restore-build-test`, `unity-project-package-static`, `buildidentity-provenance` — all 4 `SUCCESS`. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `SLICE-UI-01_IMPLEMENTATION_BACKLOG.md`, structured per `SLICE-03_IMPLEMENTATION_BACKLOG.md`'s own format. |
| AC-2 | Passed | §4's ordered backlog table, six rows, each with dependency/deliverable/closed-criterion columns. |
| AC-3 | Passed | §5 (Task boundaries) traces each task back to §3.4's list; §7 (Global non-goals) repeats every §3.4 exclusion. |
| AC-4 | Passed | `git status --porcelain` before commit shows only documentation files. |
| AC-5 | Passed | Same evidence as AC-4. |
| AC-6 | Passed | See Validation results above. |
| AC-7 | Passed | `git diff --name-status` matches §5's Allowed paths exactly. |
| AC-8 | Passed | Draft PR [#66](https://github.com/odyssey-services/Odyssey_VTT/pull/66) open; all 4 required CI checks `SUCCESS` on run 33030617453 (commit `6808985`); PR remains Draft pending explicit owner confirmation before any merge. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: Not applicable.

### Known limitations

- None. This is a pure decomposition task with no ambiguity found in `SLICE-UI-01_BACKLOG.md` §3.4's own already-complete list.

### Follow-up tasks

- `ODY-UI-01-002` through `ODY-UI-01-007`, to be created one at a time by separate future task activations, per `docs/tasks/SLICE-UI-01_IMPLEMENTATION_BACKLOG.md`.

### Self-review summary

- Scope review: Zero production/UI code touched; two documentation files only; no child task contract created.
- Architecture review: No ADR or `SLICE-UI-01_BACKLOG.md` decision reopened; `ADR-001` §6.7 cited, not altered.
- Test review: No new TestCase IDs introduced.
- Security/privacy review: Not applicable.
- Documentation/version review: Only the two new/updated files required changes.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task's own closure. No `ODY-UI-01-002`–`007` child task should be activated until this backlog is reviewed and accepted by the product owner.

### Decisions made during execution

- 2026-08-27 — Decision: six child tasks, grouped by the natural feature boundary each already-built Application service defines (board, role selector, roll+modifiers, override+result display, persistence+log, reroll/cancel+walkthrough) — Authority: `SLICE-UI-01_BACKLOG.md` §3.4's own list already groups naturally this way; matches the ТЗ's own proposed six-task split, adopted because it is already sound (one-to-one or tightly-coupled-pair mapping to one Application contract per task, shallow linear dependency chain).
- 2026-08-27 — Decision: the board screen (`ODY-UI-01-002`) ships before the role selector (`ODY-UI-01-003`), using a single hardcoded local actor identity — Authority: `BoardMovementService`'s control-ownership check is provable with two differently-controlled tokens and no role *switch*, unlike roll/audience/override work, which genuinely needs to switch identity; `SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` §2.2 records the full reasoning.
- 2026-08-27 — Decision: no child task contract file is created by this task — Authority: this task's own ТЗ instruction and the `ODY-S03-003` precedent (a backlog scaffold reserves numbers/titles/boundaries only).

### Approved task changes

- None.
