# ODY-S03-003 — Close SLICE-03 Prerequisites and Create Implementation Backlog

**Status:** In Review
**Roadmap stage / slice:** SLICE-03 (prerequisites → implementation transition)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s03-003-close-prerequisites-and-implementation-backlog`
**Pull request:** Draft — [#57](https://github.com/odyssey-services/Odyssey_VTT/pull/57) (open, awaiting owner review)
**ExecPlan:** Not required (Brief plan)
**Created:** 2026-08-26
**Last updated:** 2026-08-26 UTC

## 1. Goal

Close `docs/tasks/SLICE-03_BACKLOG.md` (the prerequisite ADR revision) with an explicit, honest closure record, and create `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md`, decomposing roadmap §12.6's ten-step vertical slice into ordered child tasks — mirroring how `ODY-S02-008` closed `SLICE-02_BACKLOG.md` and created `SLICE-02_IMPLEMENTATION_BACKLOG.md`. No code, no new task contract files for the reserved child tasks, no reopening of `ADR-020`/`ADR-021`.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-03_BACKLOG.md` §1 explicitly reserved the implementation backlog's creation for once both criteria in its own section 2 are satisfied — they now are (`ODY-S03-001`/`002` both merged, `ADR-020`/`ADR-021` both `Accepted`). Without this task, the prerequisite revision stays formally open and no implementation work has a scaffolded starting point.
- Value or risk reduction: gives the next phase of `SLICE-03` a decomposed, dependency-ordered backlog to pick up one task at a time — the same organizational discipline every prior slice in this repository used — while explicitly narrowing roadmap §12.3's broad Board baseline and §12.4's search/archive list to exactly what the ten-step scenario and its exit criteria require, so no future task silently over-scopes.
- Blocking or enabling relationship: unblocks `ODY-S03-004` (the first implementation child task) from being authored; does not itself implement anything.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1.2
- `17_Roadmap_Odyssey_VTT_v0.11.md` §12.3 (Board baseline), §12.6 (ten-step vertical slice), §12.7 (exit criteria)
- `Documentation/08_Scenes_And_Board_Odyssey_VTT_v0.5.md`, `Documentation/09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md` (full scope for the decomposition)
- `docs/adr/ADR-020_Board_Geometry_And_Movement_Determinism_v1.0.md`, `docs/adr/ADR-021_Extended_Audience_And_Selected_Participant_Visibility_v1.0.md` (this revision's new foundation)
- `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md`, `ADR-008_Deterministic_Clock_and_RNG_v1.0.md`, `ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md`, `ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md`, `ADR-019_Permissions_Baseline_v1.0.md` (already-accepted mechanisms this decomposition builds on, none reopened)
- `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md` and `docs/tasks/active/ODY-S02-008_SLICE_02_Implementation_Backlog.md` (direct structural/procedural precedent)

### Requirement and test IDs

- Requirement IDs: `SLICE-03` (prerequisites, closing), backlog `ODY-S03-003`.
- Existing test IDs: None reused.
- New test IDs introduced: None (pure documentation task, no production code).

### Task-safe private context

- Approved summary / references: roadmap §12.3/§12.6/§12.7's content and `08_Scenes_And_Board`/`09_Dice_And_Game_Log`'s section content are quoted/summarized (short customary phrases and direct quotes clearly attributed) into this task and the new backlog. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `ODY-S03-001`/`ODY-S03-002` (`ADR-020`/`ADR-021`, PRs #55/#56) are both merged to `main` — confirmed by `git log --oneline -10` before branching.
- `SLICE-03_BACKLOG.md` §1/§2 explicitly reserves the implementation backlog's creation for once its own two exit criteria are satisfied — confirmed by `Read`; both are now satisfied (two `Accepted` ADRs).
- Unlike `SLICE-02_BACKLOG.md`'s closure (which carried forward `ADR-016` §14's unresolved empirical gate), `SLICE-03_BACKLOG.md`'s two ADRs (`ADR-020`, `ADR-021`) were both accepted without any deferred spike, partial-confidence caveat, or blocking gate — confirmed by re-reading both ADRs' own §14–16/§13–15 sections; no equivalent carried-forward condition exists for this closure.
- Roadmap §12.6's ten-step scenario ("Бросок и журнал") only exercises token selection (step 1) from the broader §12.3 Board baseline list — confirmed by `Read`; §12.3 additionally names gridless/hex grid, pan/zoom, pointer, ruler, basic drawing, object lock, layer baseline, and scene save, none of which appear in the ten steps.
- Roadmap §12.7's exit criteria add exactly three board-relevant requirements beyond token selection: board-state restart/reconnect identity, movement-requires-control, and Undo/Redo-respects-permissions — confirmed by `Read`.
- `08_Scenes_And_Board` §17.1/§17.2 confirm ruler and pointer are explicitly local-only, not networked, not persisted — confirmed by `Read` (session context from `ODY-S03-001`'s prior full read of this document).
- `09_Dice_And_Game_Log` §27/§30 (full-text search, session archive/export) are not named by any of the ten §12.6 steps or any §12.7 exit criterion — confirmed by `Read`; `ADR-021` §9 already explicitly excludes full search design from its own scope.
- `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md` and `docs/tasks/active/ODY-S02-008_SLICE_02_Implementation_Backlog.md` were both read in full as the structural template for this task's own backlog document and task contract.

### Assumptions

- None. All facts above were directly observed via `Read`/`git log` before and during this task.

## 5. Scope

### In scope

- `docs/tasks/SLICE-03_BACKLOG.md` — closure edit only: `Status` line and a new §2.1 "Revision status and owner acceptance" section, recording both criteria satisfied and the (unlike `SLICE-02`) clean, ungated closure. No other section of this file is rewritten beyond the `ODY-S03-002` row status (`In Review` → `Done`) and one closing-note sentence in §7 (Backlog change control) pointing at the successor document.
- `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` (new) — decomposes roadmap §12.6 into `ODY-S03-004`–`009`, with the Board-baseline and search/archive narrowing decisions (§4.2 of the originating ТЗ) explicitly justified.
- `docs/tasks/active/ODY-S03-003_SLICE_03_Implementation_Backlog.md` (this file).

### Out of scope

- Creating any `ODY-S03-004`–`009` task contract file — this task only reserves their numbers, titles, and boundaries in the new backlog document.
- Starting implementation of any reserved child task.
- Reopening any decision in `ADR-020`/`ADR-021` or any earlier-accepted ADR (`ADR-002`/`004`/`008`/`012`/`017`/`019`).
- Rewriting any part of `SLICE-03_BACKLOG.md` beyond its closure section and the one row-status/closing-note edit named above.
- Real network/internet integration (`ADR-016` §14, `ODY-S02-014`) — a separate, already-deferred `SLICE-02` product-owner decision, not part of this task chain.
- Full generality of `07_Permissions`/`08_Scenes_And_Board` beyond what roadmap §12.6/§12.7 requires — not expanded by this task.

### Allowed paths

```text
docs/tasks/SLICE-03_BACKLOG.md
docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S03-003_SLICE_03_Implementation_Backlog.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: not applicable — no code.
- Authoritative-state and transaction boundary: not applicable.
- Serialization / compatibility boundary: not applicable.
- Time / RNG rule: not applicable.
- Unity / thread / lifetime rule: not applicable.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: not applicable to this task's own execution; the new backlog correctly builds each child task atop already-accepted `ADR-019`/`ADR-021` redaction mechanisms without reopening them.
- Performance or platform constraint: not applicable.
- Other: the closure record must not overstate or understate either ADR's acceptance status — verified explicitly in the Completion evidence section.

## 7. Expected behavior

This is a pure documentation task; "expected behavior" means the two documents' own normative content, not runtime behavior.

### Scenario 1 — honest, clean closure (no gate to carry forward)

**Given** both `ADR-020` and `ADR-021` were accepted without any deferred spike or partial-confidence caveat
**When** `SLICE-03_BACKLOG.md` §2.1 is written
**Then** it states this plainly, including an explicit contrast with `SLICE-02_BACKLOG.md`'s own gated closure — not silently omitting the comparison, and not inventing a gate that does not exist.

### Scenario 2 — Board baseline narrowed with explicit justification, not silently

**Given** roadmap §12.3's broad Board baseline list versus §12.6/§12.7's actual exercised subset
**When** `SLICE-03_IMPLEMENTATION_BACKLOG.md` §2.1 is written
**Then** it names every excluded §12.3 item (drawing, ruler, pointer, object lock, layer baseline, pan/zoom, grid-type-switching UI) and states why each is not required by any of the ten steps or any exit criterion.

### Scenario 3 — decomposition matches the ten roadmap steps, with justified groupings

**Given** roadmap §12.6's ten steps
**When** the new backlog's ordered task table is written
**Then** every step maps to at least one child task, the groupings are justified (not arbitrary), and no child task reopens `ADR-002`/`004`/`008`/`012`/`017`/`019`/`020`/`021`.

### Scenario 4 — all eight exit criteria mapped

**Given** roadmap §12.7's eight exit criteria (including `GATE-B` closure)
**When** the new backlog's §3 is written
**Then** every criterion is mapped to at least one specific child task or explained as the closure task's own responsibility (`GATE-B`).

### Required invariants

- No `ODY-S03-004`–`009` task contract file is created by this task.
- `ADR-002`/`004`/`008`/`012`/`017`/`019`/`020`/`021` files are unmodified.
- `SLICE-03_BACKLOG.md`'s content outside its closure section/row-status edit is unmodified (no retroactive rewriting of its historical record).

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `docs/tasks/SLICE-03_BACKLOG.md` (closure section), `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` (new), this task contract.
- Generated evidence or build artifacts: None.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. `SLICE-03_BACKLOG.md`'s `Status` line reads `CLOSED` with the acceptance date, and a new §2.1 records both criteria satisfied.
2. §2.1 explicitly states this closure carries forward no unresolved gate, contrasted against `SLICE-02_BACKLOG.md`'s gated closure.
3. `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` exists, decomposing roadmap §12.6's ten steps into ordered, justified child tasks (`ODY-S03-004`–`009`).
4. The new backlog's §2.1 explicitly names and justifies every roadmap §12.3 Board-baseline item excluded from this vertical slice.
5. The new backlog's §2.2 explicitly excludes full-text search and session archive/export, citing that neither is exercised by the ten steps or exit criteria.
6. The new backlog's §3 maps all eight roadmap §12.7 exit criteria to specific child tasks or the closure task's own responsibility.
7. No `ODY-S03-004`–`009` task contract file is created.
8. `ADR-002`/`004`/`008`/`012`/`017`/`019`/`020`/`021` are unmodified by this task's diff.
9. `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1` pass.
10. `git diff --name-status` against `main` shows only files listed in §5's Allowed paths.
11. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

None (pure documentation task).

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- Read `SLICE-03_BACKLOG.md` §2.1 and `SLICE-03_IMPLEMENTATION_BACKLOG.md` end-to-end after writing to confirm the honesty requirement (Scenario 1), the Board-baseline narrowing justification (Scenario 2), the ten-step decomposition (Scenario 3), and the exit-criteria mapping (Scenario 4) are all substantively met, not just present as headings.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable.
- Network topology or database fixture: Not applicable.
- Other: None.

### Validation not required by this task

- `dotnet build`/`dotnet test` — no production or test code is touched by this task.
- Any test of a reserved child task's future implementation — none exists yet.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None — no code, schema, or protocol is touched.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits; both edited/new documents are self-contained, referenced by nothing else in the repository beyond future child tasks that don't exist yet.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

No dependency is introduced by this task.

## 13. Security, privacy, and hidden information

- Data classes handled: None — this task touches no code, credential, or campaign data.
- Trust boundaries: Not applicable.
- Authorization / audience checks: Not applicable — this task organizes future work, it does not implement or change any authorization mechanism.
- Redaction requirements: Not applicable.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: Not applicable; this task's own correctness is verified by confirming the decomposition correctly reuses (not reinvents) `ADR-019`/`ADR-021`'s redaction mechanisms (§9 AC-6), a documentation-accuracy check, not a code security test.

## 14. Planning and execution mode

- Planning mode: `Brief plan`
- Reason for selected mode: evaluated fresh against `PLANS.md` §1.2's triggers, following the same reasoning `ODY-S02-008` used for its own analogous close-and-create task (consulted, not copied verbatim). This task introduces no new architecture, module, public contract, persisted format, protocol, permissions model, dependency graph, Unity/package version, or build pipeline change — it is pure organizational documentation reserving future task numbers and boundaries, plus an honest closure record for already-completed work. It does not span multiple milestones or PRs (single Draft PR), does not change any production module (zero code touched), does not affect authoritative state, persistence, networking, security, permissions, hidden information, redaction, diagnostics, time, or randomness (it only *describes*, at a planning level, which future tasks will), has one clear implementation path (write the closure section and the new backlog document), and completes in one focused pull request with no migration or recovery procedure required — matching every `PLANS.md` §1.1 Brief-plan-eligibility criterion.
- ExecPlan path: Not required.
- Expected pull request count: 1 (this closure/scaffold). Each subsequent `ODY-S03-004`–`009` child task will be its own separate task and pull request, not part of this activation.
- Milestone or sequencing constraints: must not begin before both `SLICE-03_BACKLOG.md` prerequisite tasks are merged to `main` (verified in §4). Unblocks `ODY-S03-004`/`ODY-S03-005` (mutually independent, per the new backlog's §6).

## 15. Documentation and versioning impact

- Documents that must change: `docs/tasks/SLICE-03_BACKLOG.md` (closure section, `ODY-S03-002` row status), `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` (new), this task contract.
- Documents that must not change: `ADR-001`–`021`, `docs/tasks/active/ODY-S03-000`–`002_*` (read only), `docs/tasks/completed/*`, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything under `Documentation/`, `SLICE-03_BACKLOG.md`'s content outside its closure section/row-status edit.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: None — no ADR changes version.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [x] Required automated tests pass (none required; no code touched).
- [x] Required manual checks are completed.
- [ ] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `docs/tasks/SLICE-03_BACKLOG.md` — `Status` line, new §2.1, `ODY-S03-002` row status, one closing-note sentence in §7.
- `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` — new.
- `docs/tasks/active/ODY-S03-003_SLICE_03_Implementation_Backlog.md` (this file) — new.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | All `REPO-POLICY-*`/`TC-CI-*` checks passed; `Repository policy check passed.` |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `SLICE-03_BACKLOG.md` `Status: ... CLOSED (owner-accepted 2026-08-26...)`. |
| AC-2 | Passed | §2.1's closing paragraph explicitly contrasts this ungated closure with `SLICE-02_BACKLOG.md`'s `ADR-016` §14-gated closure. |
| AC-3 | Passed | `SLICE-03_IMPLEMENTATION_BACKLOG.md` §4, `ODY-S03-004`–`009`. |
| AC-4 | Passed | `SLICE-03_IMPLEMENTATION_BACKLOG.md` §2.1 names and justifies every excluded §12.3 item (drawing, ruler, pointer, object lock, layer baseline, pan/zoom, grid-type UI). |
| AC-5 | Passed | `SLICE-03_IMPLEMENTATION_BACKLOG.md` §2.2. |
| AC-6 | Passed | `SLICE-03_IMPLEMENTATION_BACKLOG.md` §3 — all 8 criteria mapped, criterion 8 (`GATE-B`) to the closure task. |
| AC-7 | Passed | No task contract file created under `docs/tasks/active/ODY-S03-004` through `009`. |
| AC-8 | Passed | `git status --porcelain` confirms no `ADR-002`/`004`/`008`/`012`/`017`/`019`/`020`/`021` file touched. |
| AC-9 | Passed | See Validation results table above — both commands pass. |
| AC-10 | Passed | `git status --porcelain` shows only files listed in §5's Allowed paths. |
| AC-11 | Pending | PR to be opened as Draft; CI status to be confirmed. |

## 18. Blockers, risks, and open decisions

- No blockers for this task's own closure.
- Open decision (deliberately left to future tasks, not this one): the exact Brief-plan-vs-ExecPlan choice for each of `ODY-S03-004`–`009`, made independently when each is authored.
- Risk: none identified — this is a low-risk documentation-only task building directly on already-accepted, already-merged decisions.
