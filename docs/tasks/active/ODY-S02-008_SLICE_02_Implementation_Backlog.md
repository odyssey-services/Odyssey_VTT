# ODY-S02-008 — Close SLICE-02 Prerequisite Revision and Create Implementation Backlog

**Status:** In Review
**Roadmap stage / slice:** SLICE-02 (prerequisites → implementation transition)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s02-008-slice-02-implementation-backlog`
**Pull request:** Not opened
**ExecPlan:** Not required (Brief plan)
**Created:** 2026-08-25
**Last updated:** 2026-08-25 UTC

## 1. Goal

Close `docs/tasks/SLICE-02_BACKLOG.md` (the prerequisite ADR/spike revision) with an explicit, honest closure record — including the deliberate distinction between `SP-03`'s partial (owner-accepted-at-lower-confidence) coverage and `SP-04`'s full coverage — and create `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md`, decomposing roadmap §11.6's ten-step vertical slice into ordered child tasks, mirroring how `ODY-S01-006` closed `SLICE-01_BACKLOG.md` and created `SLICE-01_IMPLEMENTATION_BACKLOG.md`. No code, no new task contract files for the reserved child tasks, no reopening of `ADR-015`–`019`.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-02_BACKLOG.md` §1 explicitly reserved the implementation backlog's creation for "once all seven criteria in section 2 are satisfied" — they now are (`ODY-S02-001`–`007` all merged). Without this task, the prerequisite revision stays formally open and no implementation work has a scaffolded starting point.
- Value or risk reduction: gives the next phase of `SLICE-02` a decomposed, dependency-ordered backlog to pick up one task at a time — the same organizational discipline every prior slice in this repository used — while explicitly carrying forward `ADR-016`'s Unity Relay pre-integration gate so no future task accidentally starts real-transport work before its precondition is met.
- Blocking or enabling relationship: unblocks `ODY-S02-009` (the first implementation child task) from being authored; does not itself implement anything.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1.2
- `17_Roadmap_Odyssey_VTT_v0.11.md` §11.6 (ten-step vertical slice), §11.7 (exit criteria)
- `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` (structural reference — how a vertical slice was decomposed previously)
- `docs/tasks/active/ODY-S01-006_SLICE_01_Implementation_Backlog.md` (structural reference — how the analogous close-and-create task was itself authored)
- `docs/adr/ADR-015_Transport_Abstraction_v1.0.md`–`ADR-019_Permissions_Baseline_v1.0.md` (the five accepted contracts every child task builds on, none reopened)
- `docs/adr/ADR-016_Rendezvous_Relay_Strategy_v1.0.md` §1 point 9/§14 (the pre-production-integration gate this task must carry forward, not resolve)
- `docs/tasks/active/ODY-S02-002_SP-03_Internet_Connectivity_Report.md`, `docs/tasks/active/ODY-S02-007_SP-04_Hidden_Data_Boundary_Report.md` (source of the honest closure distinction)

### Requirement and test IDs

- Requirement IDs: `SLICE-02` (prerequisites, closing), backlog `ODY-S02-008`.
- Existing test IDs: None reused.
- New test IDs introduced: None (pure documentation task, no production code).

### Task-safe private context

- Approved summary / references: roadmap §11.6/§11.7's content is quoted/summarized (short customary phrases and direct quotes clearly attributed) into this task and the new backlog. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `ODY-S02-001`–`007` (`ADR-015`–`019`, `SP-03`, `SP-04`; PRs #38–#44) are all merged to `main` — confirmed by `git log --oneline -15` before branching.
- `SLICE-02_BACKLOG.md` §1/§2 explicitly reserves the implementation backlog's creation for once its own seven exit criteria are satisfied — confirmed by `Read`; all seven are now satisfied (five `Accepted` ADRs, two owner-reviewed spike reports).
- `ADR-016` §1 point 9/§14 fixes a normative pre-production-integration gate on Unity Relay SDK integration — confirmed by `Read`; not yet satisfied (no follow-up spike has been commissioned).
- `ODY-S02-002_SP-03_Internet_Connectivity_Report.md` §2/§8 documents 5 of 7 roadmap §11.4 checklist items as `NOT_VERIFIED` — confirmed by `Read`; this is the source of the honest closure-note distinction this task must carry into `SLICE-02_BACKLOG.md`'s closure record.
- `ODY-S02-007_SP-04_Hidden_Data_Boundary_Report.md` §3/§6 documents full coverage, no gap found — confirmed by `Read`.
- `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` and `docs/tasks/active/ODY-S01-006_SLICE_01_Implementation_Backlog.md` were both read in full as the structural template for this task's own backlog document and task contract.

### Assumptions

- None. All facts above were directly observed via `Read`/`git log` before and during this task.

## 5. Scope

### In scope

- `docs/tasks/SLICE-02_BACKLOG.md` — closure edit only: `Status` line and a new §2.1 "Revision status and owner acceptance" section, recording all 7 criteria satisfied and the honest `SP-03`-vs-`SP-04` confidence distinction. No other section of this file is rewritten.
- `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md` (new) — decomposes roadmap §11.6 into `ODY-S02-009`–`015`, with the `ADR-016` §14 gate explicitly carried forward onto `ODY-S02-014`.
- `docs/tasks/active/ODY-S02-008_SLICE_02_Implementation_Backlog.md` (this file).

### Out of scope

- Creating any `ODY-S02-009`–`015` task contract file — this task only reserves their numbers, titles, and boundaries in the new backlog document, per its own explicit instruction.
- Starting implementation of any reserved child task.
- Reopening any decision in `ADR-015`–`019`.
- Rewriting any part of `SLICE-02_BACKLOG.md` beyond its closure section.
- Commissioning or performing the `ADR-016` §14 follow-up empirical spike — that remains the product owner's decision, only carried forward as a documented precondition here.

### Allowed paths

```text
docs/tasks/SLICE-02_BACKLOG.md
docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S02-008_SLICE_02_Implementation_Backlog.md
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
- Security / privacy / redaction rule: not applicable to this task's own execution; the new backlog correctly carries forward `ADR-016`'s security-relevant gate rather than dropping it.
- Performance or platform constraint: not applicable.
- Other: the closure record must not overstate `SP-03`'s confidence level — verified explicitly in the Completion evidence section, matching this task's own honesty instruction.

## 7. Expected behavior

This is a pure documentation task; "expected behavior" means the two documents' own normative content, not runtime behavior.

### Scenario 1 — honest closure, not a uniform "all accepted"

**Given** `SP-03`'s partial coverage and `SP-04`'s full coverage
**When** `SLICE-02_BACKLOG.md` §2.1 is written
**Then** it states each spike's actual confidence level distinctly, quoting/citing the specific gap count for `SP-03`, not a single undifferentiated "both spikes accepted" line.

### Scenario 2 — the Unity Relay gate is carried forward, not silently dropped

**Given** `ADR-016` §14's normative pre-integration condition
**When** `SLICE-02_IMPLEMENTATION_BACKLOG.md` is written
**Then** the real-transport child task (`ODY-S02-014`) is explicitly marked `Blocked`, with the precondition named, and the backlog's own exit-criteria section states plainly that criterion 1 (real internet) may remain unmet at this revision's eventual closure if that gate is still unmet.

### Scenario 3 — decomposition matches the ten roadmap steps, with justified groupings

**Given** roadmap §11.6's ten steps
**When** the backlog's ordered task table is written
**Then** every step maps to at least one child task, the groupings are justified (not arbitrary), and no child task reopens `ADR-015`–`019`.

### Required invariants

- No `ODY-S02-009`–`015` task contract file is created by this task.
- `ADR-015`–`019` files are unmodified.
- `SLICE-02_BACKLOG.md`'s content outside the closure section is unmodified (no retroactive rewriting of its historical record).

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `docs/tasks/SLICE-02_BACKLOG.md` (closure section), `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md` (new), this task contract.
- Generated evidence or build artifacts: None.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. `SLICE-02_BACKLOG.md`'s `Status` line reads `CLOSED` with the acceptance date, and a new §2.1 records all 7 criteria satisfied.
2. §2.1 states `SP-03`'s partial coverage (5 of 7 `NOT_VERIFIED` items) and its owner-accepted-at-lower-confidence status distinctly from `SP-04`'s full-coverage, no-gap status — not a single undifferentiated line.
3. §2.1 explicitly states the `ADR-016` §14 gate remains carried forward, unresolved by this closure.
4. `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md` exists, decomposing roadmap §11.6's ten steps into ordered, justified child tasks (`ODY-S02-009`–`015`).
5. `ODY-S02-014` (real transport) is explicitly marked `Blocked`, naming its precondition, not `Draft` alongside the others.
6. The new backlog's exit-criteria section states plainly that criterion 1 may remain unmet at closure — not glossed over.
7. No `ODY-S02-009`–`015` task contract file is created.
8. `ADR-015`–`019` are unmodified by this task's diff.
9. `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1` pass; `dotnet build`/`dotnet test` on `DotNet/Odyssey.Core.sln` pass unchanged (no code touched).
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

```bash
dotnet build DotNet/Odyssey.Core.sln
dotnet test DotNet/Odyssey.Core.sln
```

### Manual validation

- Read `SLICE-02_BACKLOG.md` §2.1 and `SLICE-02_IMPLEMENTATION_BACKLOG.md` end-to-end after writing to confirm the honesty requirement (Scenario 1) and the gate-carry-forward requirement (Scenario 2) are both substantively met, not just present as headings.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable.
- Network topology or database fixture: Not applicable.
- Other: `dotnet` SDK matching `global.json` (`10.0.302`), used only to confirm the existing solution is unaffected.

### Validation not required by this task

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
- Security tests: Not applicable; this task's own correctness is verified by confirming the `ADR-016` gate is carried forward accurately (§9 AC-3/AC-5), a documentation-accuracy check, not a code security test.

## 14. Planning and execution mode

- Planning mode: `Brief plan`
- Reason for selected mode: evaluated fresh against `PLANS.md` §1.2's triggers, following the same reasoning `ODY-S01-006` used for its own analogous close-and-create task (consulted, not copied verbatim). This task introduces no new architecture, module, public contract, persisted format, protocol, permissions model, dependency graph, Unity/package version, or build pipeline change — it is pure organizational documentation reserving future task numbers and boundaries, plus an honest closure record for already-completed work. It does not span multiple milestones or PRs (single Draft PR), does not change any production module (zero code touched), does not affect authoritative state, persistence, networking, security, permissions, hidden information, redaction, diagnostics, time, or randomness (it only *describes*, at a planning level, which future tasks will, and carries forward — does not resolve — an existing security-relevant gate), has one clear implementation path (write the closure section and the new backlog document), and completes in one focused pull request with no migration or recovery procedure required — matching every `PLANS.md` §1.1 Brief-plan-eligibility criterion.
- ExecPlan path: Not required.
- Expected pull request count: 1 (this closure/scaffold). Each subsequent `ODY-S02-009`–`015` child task will be its own separate task and pull request, not part of this activation.
- Milestone or sequencing constraints: must not begin before all 7 `SLICE-02_BACKLOG.md` prerequisite tasks are merged to `main` (verified in §4). Unblocks `ODY-S02-009`.

## 15. Documentation and versioning impact

- Documents that must change: `docs/tasks/SLICE-02_BACKLOG.md` (closure section only), `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md` (new), this task contract.
- Documents that must not change: `ADR-001`–`019`, `docs/tasks/active/ODY-S02-001`–`007_*` (read only), `docs/tasks/completed/*`, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything under `Documentation/`, `SLICE-02_BACKLOG.md`'s content outside its closure section.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: None — no ADR changes version.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [x] Required automated tests pass (none required; existing suite unaffected).
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

- `docs/tasks/SLICE-02_BACKLOG.md` — `Status` line, new §2.1.
- `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md` — new.
- `docs/tasks/active/ODY-S02-008_SLICE_02_Implementation_Backlog.md` (this file) — new.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet/Odyssey.Core.sln` | Passed | 0 warnings, 0 errors — unaffected by this documentation-only task. |
| `dotnet test DotNet/Odyssey.Core.sln` | Passed | 155/155, 0 failed — unchanged. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS`. |
| `.\scripts\check-repository-policy.ps1` | Passed | All checks pass, including `REPO-POLICY-005`. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `SLICE-02_BACKLOG.md` `Status: ... CLOSED (owner-accepted 2026-08-25...)`. |
| AC-2 | Passed | §2.1 items 6/7 state `SP-03`'s 5/7 `NOT_VERIFIED` count and `SP-04`'s full coverage distinctly. |
| AC-3 | Passed | §2.1's "Carried-forward condition" paragraph. |
| AC-4 | Passed | `SLICE-02_IMPLEMENTATION_BACKLOG.md` §4, `ODY-S02-009`–`015`. |
| AC-5 | Passed | `ODY-S02-014` row: `Status: Blocked — pending ADR-016 §14 follow-up spike`. |
| AC-6 | Passed | `SLICE-02_IMPLEMENTATION_BACKLOG.md` §3, criterion 1's note. |
| AC-7 | Passed | No task contract file created under `docs/tasks/active/ODY-S02-009` through `015`. |
| AC-8 | Passed | `git status --porcelain` confirms no `ADR-015`–`019` file touched. |
| AC-9 | Passed | See Validation results table above — all four commands pass. |
| AC-10 | Passed | `git status --porcelain` shows only files listed in §5's Allowed paths. |
| AC-11 | Pending | PR not yet opened. |

## 18. Blockers, risks, and open decisions

- No blockers for this task's own closure.
- Open decision (the product owner's, not this task's): when/whether to commission the `ADR-016` §14 follow-up spike, which gates `ODY-S02-014`.
- Risk: none identified — this is a low-risk documentation-only task building directly on already-accepted, already-merged decisions.
