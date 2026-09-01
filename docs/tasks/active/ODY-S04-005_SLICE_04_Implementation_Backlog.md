# ODY-S04-005 — Create SLICE-04 Implementation Backlog

**Status:** In Review
**Roadmap stage / slice:** SLICE-04 (prerequisites → implementation transition)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s04-005-implementation-backlog`
**Pull request:** <to be filled after `gh pr create`>
**ExecPlan:** Not required (Brief plan)
**Created:** 2026-09-01
**Last updated:** 2026-09-01 UTC

## 1. Goal

Create `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md`, decomposing roadmap §13.8's eleven-step `SLICE-04` vertical slice into an ordered backlog of `ODY-S04-101`–`115` child tasks — mirroring how `ODY-S03-003` created `SLICE-03_IMPLEMENTATION_BACKLOG.md` once `SLICE-03`'s own prerequisite ADR revision closed. No code, no new child task contract files, no reopening of `ADR-022`–`025`.

## 2. Why this task exists

- Problem or dependency being addressed: `docs/tasks/SLICE-04_BACKLOG.md` §0/§2 is now `COMPLETE` — all four prerequisite ADRs (`ADR-022`–`025`) are `Accepted` — but no decomposed, dependency-ordered backlog exists yet for the vertical slice itself. Without this task, `SLICE-04` implementation has no scaffolded starting point and any future task would have to re-derive the decomposition ad hoc.
- Value or risk reduction: gives the next phase of `SLICE-04` a decomposed, dependency-ordered backlog to pick up one task at a time — the same organizational discipline every prior slice in this repository used — while explicitly justifying where the product's own five-stage `CAP-SLICE-01`–`05` decomposition needs further splitting for independently-testable substance.
- Blocking or enabling relationship: unblocks `ODY-S04-101` (the first implementation child task) from being authored; does not itself implement anything.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`.
- `AGENTS.md`, `PLANS.md` §1.
- `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` §13 in full (especially §13.8 eleven-step vertical slice, §13.9 exit criteria).
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §33 (test scenarios), §34 (readiness criteria), §35 (recommended implementation stages), §36 (decisions deliberately left to implementation).
- `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md`, `ADR-023_Character_Drafts_Templates_And_Approval_Workflow_v1.0.md`, `ADR-024_Development_Economy_And_Progression_Transactions_v1.0.md`, `ADR-025_Character_Ownership_Lifecycle_And_Ruleset_Migration_Operations_v1.0.md` (all four, full read).
- `docs/adr/ADR-002`/`003`/`012`/`013`/`017`/`019` (substrate these four ADRs already build on, not reopened here).
- `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` and `docs/tasks/active/ODY-S03-003_SLICE_03_Implementation_Backlog.md` (direct structural/procedural precedent).
- `docs/tasks/SLICE-04_BACKLOG.md` — read for context and ADR references only; not edited by this task.

### Requirement and test IDs

- Requirement IDs: `SLICE-04` (implementation), backlog `ODY-S04-005`.
- Existing test IDs: None reused.
- New test IDs introduced: None (pure documentation task, no production code).

### Task-safe private context

- Approved summary / references: roadmap §13's content and `10_Characters_And_Progression_Odyssey_VTT_v0.2.md`'s section content are quoted/summarized (short customary phrases and direct quotes clearly attributed) into this task and the new backlog. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `gh pr view 83 --json state,mergedAt,mergeCommit` confirmed `state: MERGED`, and `git merge-base --is-ancestor 0b2238a... origin/main` independently confirmed the merge commit is a real ancestor of `origin/main` (the now-standard preflight precaution every `ODY-S04-00X` task in this series applies).
- `docs/tasks/SLICE-04_BACKLOG.md` §0 reads "Status: Prerequisite backlog — COMPLETE" and §2 lists all four prerequisite ADRs satisfied — confirmed by `Read`.
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §35 proposes exactly five implementation stages (`CAP-SLICE-01`–`05`) — confirmed by `Read`. Cross-checked against §33's 80 test scenarios and §34's 60 readiness criteria: several stages bundle materially different invariant clusters with different `ADR-023`/`024`/`025` section boundaries and different dependency shapes (for example, `CAP-SLICE-01` bundles the aggregate skeleton with primary-owner-assignment audit, a fully separate `ADR-025` §4 concern; `CAP-SLICE-03` bundles ordinary purchase, skill-5+ evidence/recommendation, and revert/respec — three different `ADR-024` sections).
- `ADR-022`–`025` were re-read in full during this task; each already answers every architectural question this backlog's decomposition needed. No open architectural question requiring a fifth ADR was found.
- `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` and `docs/tasks/active/ODY-S03-003_SLICE_03_Implementation_Backlog.md` were both read in full as the structural template for this task's own backlog document and task contract.

### Assumptions

- None. All facts above were directly observed via `Read`/`gh`/`git` during this task.

## 5. Scope

### In scope

- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (new) — decomposes roadmap §13.8 into `ODY-S04-101`–`115`, with the `CAP-SLICE`-splitting and ownership-skeleton-vs-operations decisions explicitly justified.
- `docs/tasks/active/ODY-S04-005_SLICE_04_Implementation_Backlog.md` (this file).

### Out of scope

- Creating any `ODY-S04-101`–`115` task contract file — this task only reserves their numbers, titles, and boundaries in the new backlog document.
- Starting implementation of any reserved child task.
- Reopening any decision in `ADR-022`–`025` or any earlier-accepted ADR (`ADR-002`/`003`/`012`/`013`/`017`/`019`).
- Any edit to `docs/tasks/SLICE-04_BACKLOG.md` — it is closed and remains a historical artifact, per this task's own explicit instruction.
- Any content catalog (concrete skills/abilities/classes/Ruleset cost tables) — structure only, not content.
- Full production Character-sheet/approval/template-picker UI (`SLICE-10` scope).

### Allowed paths

```text
docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S04-005_SLICE_04_Implementation_Backlog.md
```

### Paths requiring explicit approval before editing

```text
docs/tasks/SLICE-04_BACKLOG.md
docs/adr/ADR-001* through docs/adr/ADR-025*
```

## 6. Technical constraints

- Module ownership and dependency direction: not applicable — no code.
- Authoritative-state and transaction boundary: not applicable.
- Serialization / compatibility boundary: not applicable.
- Time / RNG rule: not applicable.
- Unity / thread / lifetime rule: not applicable.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: not applicable to this task's own execution; the new backlog correctly builds each child task atop already-accepted `ADR-019`/`ADR-022`–`025` mechanisms without reopening them.
- Performance or platform constraint: not applicable.
- Other: if any genuine architectural gap is found during decomposition, this task must stop and report it rather than deciding it inline — verified explicitly in section 17.

## 7. Expected behavior

This is a pure documentation task; "expected behavior" means the new document's own normative content, not runtime behavior.

### Scenario 1 — decomposition matches the eleven roadmap steps, with justified groupings

**Given** roadmap §13.8's eleven steps and product §35's five recommended stages
**When** the new backlog's ordered task table is written
**Then** every stage maps to one or more child tasks, every split beyond the product's own five stages is explicitly justified against §33/§34's real test-scenario/readiness-criteria volume, and no child task reopens `ADR-002`/`003`/`012`/`013`/`017`/`019`/`022`/`023`/`024`/`025`.

### Scenario 2 — all fourteen restated exit criteria mapped

**Given** roadmap §13.9's exit criteria
**When** the new backlog's exit-criteria section is written
**Then** every criterion is restated as an independently checkable item and mapped to at least one specific child task or explained as the closure task's own responsibility (`GATE-C`).

### Scenario 3 — "no new ADR needed" is verified, not assumed

**Given** this task's own explicit instruction not to invent a fifth ADR
**When** the new backlog is written
**Then** it includes an explicit "No new ADR needed" section confirming every decomposition question is already answered by `ADR-022`–`025` plus existing substrate, and this task contract records that no unresolved architectural gap was found.

### Required invariants

- No `ODY-S04-101`–`115` task contract file is created by this task.
- `ADR-022`–`025` (and all earlier ADRs) files are unmodified.
- `docs/tasks/SLICE-04_BACKLOG.md` is unmodified (it remains closed and historical).

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (new), this task contract.
- Generated evidence or build artifacts: None.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` exists, decomposing roadmap §13.8's eleven-step scenario into ordered, justified child tasks (`ODY-S04-101`–`115`).
2. The new backlog's scope-decision section explicitly justifies every split beyond product §35's five `CAP-SLICE` stages.
3. The new backlog restates roadmap §13.9's exit criteria as an independently checkable list and maps every item to a specific child task or the closure task's own responsibility.
4. The new backlog includes an explicit "No new ADR needed" confirmation, and this task's own final report states plainly whether any unresolved architectural gap was found (none was).
5. The new backlog explicitly excludes content catalogs, `ADR-019` role/permission extensions, the `.odchar` file format itself beyond its Draft-creation interaction, and production Character-sheet UI as non-goals.
6. No `ODY-S04-101`–`115` task contract file is created.
7. `ADR-022`–`025` (and all earlier ADRs) and `docs/tasks/SLICE-04_BACKLOG.md` are unmodified by this task's diff.
8. `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1` pass.
9. `git diff --name-status` against `main` shows only files listed in §5's Allowed paths.
10. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

None (pure documentation task).

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- Read `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` end-to-end after writing to confirm the decomposition justification (Scenario 1), the exit-criteria mapping (Scenario 2), and the "no new ADR needed" confirmation (Scenario 3) are all substantively met, not just present as headings.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable.
- Network topology or database fixture: Not applicable.
- Other: None.

### Validation not required by this task

- `dotnet build`/`dotnet test`/`test-unity` — no production or test code is touched by this task.
- Any test of a reserved child task's future implementation — none exists yet.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None — no code, schema, or protocol is touched.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits; the new document is self-contained, referenced by nothing else in the repository beyond future child tasks that don't exist yet.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: None — this task touches no code, credential, or campaign data.
- Trust boundaries: Not applicable.
- Authorization / audience checks: Not applicable — this task organizes future work, it does not implement or change any authorization mechanism.
- Redaction requirements: Not applicable.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: Not applicable; this task's own correctness is verified by confirming the decomposition correctly reuses (not reinvents) `ADR-019`/`ADR-022`–`025`'s mechanisms — a documentation-accuracy check, not a code security test.

## 14. Planning and execution mode

- Planning mode: `Brief plan`
- Reason for selected mode: evaluated fresh against `PLANS.md` §1's triggers, following the same reasoning `ODY-S03-003` used for its own analogous create-implementation-backlog task (consulted, not copied verbatim). This task introduces no new architecture, module, public contract, persisted format, protocol, permissions model, dependency graph, Unity/package version, or build pipeline change — it is pure organizational documentation reserving future task numbers and boundaries. It does not span multiple milestones or PRs (single Draft PR), does not change any production module (zero code touched), does not affect authoritative state, persistence, networking, security, permissions, hidden information, redaction, diagnostics, time, or randomness, has one clear implementation path (write the new backlog document), and completes in one focused pull request with no migration or recovery procedure required.
- ExecPlan path: Not required.
- Expected pull request count: 1 (this scaffold). Each subsequent `ODY-S04-101`–`115` child task will be its own separate task and pull request, not part of this activation.
- Milestone or sequencing constraints: must not begin before `SLICE-04_BACKLOG.md`'s prerequisite revision is `COMPLETE` (verified in §4). Unblocks `ODY-S04-101` (the first implementation child task, no dependency per the new backlog's §7).

## 15. Documentation and versioning impact

- Documents that must change: `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (new), this task contract.
- Documents that must not change: `ADR-001`–`025`, `docs/tasks/SLICE-04_BACKLOG.md`, `docs/tasks/active/ODY-S04-000`–`004_*` (read only), `docs/tasks/completed/*`, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything under `Documentation/`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: None — no ADR changes version.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass (none required; no code touched).
- [x] Required manual checks are completed.
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

- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` — new.
- `docs/tasks/active/ODY-S04-005_SLICE_04_Implementation_Backlog.md` (this file) — new.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `git merge-base --is-ancestor <PR #83 merge commit> origin/main` | Passed | Confirmed the merge commit is a real ancestor, not merely a GitHub-reported status. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | All `REPO-POLICY-*`/`TC-CI-*` checks passed; `Repository policy check passed.` |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `SLICE-04_IMPLEMENTATION_BACKLOG.md` §5, `ODY-S04-101`–`115`. |
| AC-2 | Passed | `SLICE-04_IMPLEMENTATION_BACKLOG.md` §2.1/§2.2 justify every split beyond the five product `CAP-SLICE` stages. |
| AC-3 | Passed | `SLICE-04_IMPLEMENTATION_BACKLOG.md` §3 — 14 restated criteria, all mapped; criterion 14 (`GATE-C`) to the closure task. |
| AC-4 | Passed | `SLICE-04_IMPLEMENTATION_BACKLOG.md` §4 "No new ADR needed"; this task's own final report confirms no unresolved architectural gap was found. |
| AC-5 | Passed | `SLICE-04_IMPLEMENTATION_BACKLOG.md` §8 (Global non-goals). |
| AC-6 | Passed | No task contract file created under `docs/tasks/active/ODY-S04-101` through `115`. |
| AC-7 | Passed | `git status --porcelain` confirms no `ADR-001`–`025` or `SLICE-04_BACKLOG.md` file touched. |
| AC-8 | Passed | See Validation results table above — both commands pass. |
| AC-9 | Passed | `git status --porcelain` shows only files listed in §5's Allowed paths. |
| AC-10 | Passed | Draft PR link and CI status recorded in the final report once opened. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: Not applicable.

### Known limitations

- No `ODY-S04-101`–`115` task contract exists yet — each is created and activated one at a time, per this backlog's own scaffold-only intent.

### Follow-up tasks

- `ODY-S04-101` — Character Aggregate, Lifecycle Skeleton & SQLite Persistence (first child task, no dependency).

### Self-review summary

- Scope review: limited to allowed documentation files; `SLICE-04_BACKLOG.md` and all ADRs left untouched.
- Architecture review: decomposition reuses `ADR-022`–`025` and earlier substrate without redefinition; no fifth ADR proposed; no unresolved architectural gap found.
- Test review: no tests changed; required docs/policy validation passed.
- Security/privacy review: no private excerpts copied beyond customary short quotes; no authorization mechanism changed.
- Documentation/version review: no ADR or app/schema/protocol version changed.

## 18. Blockers, risks, and open decisions

- No blockers for this task's own closure.
- Open decision (deliberately left to future tasks, not this one): the exact Brief-plan-vs-ExecPlan choice for each of `ODY-S04-101`–`115`, made independently when each is authored, per `PLANS.md` §1.
- Risk: none identified — this is a low-risk documentation-only task building directly on already-accepted, already-merged decisions.
