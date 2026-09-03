# ODY-S04-115 — SLICE-04 Acceptance and Closure Gate

**Status:** In Review
**Roadmap stage / slice:** SLICE-04 (vertical slice implementation)
**Owner:** Unassigned
**Requested by:** Product owner
**Branch:** `feat/ody-s04-115-slice-04-acceptance-closure-gate`
**Pull request:** To be opened
**ExecPlan:** Not required — see section 14 (Brief plan)
**Created:** 2026-09-03
**Last updated:** 2026-09-03 UTC

## 1. Goal

All 14 `SLICE-04_IMPLEMENTATION_BACKLOG.md` §3 exit criteria (roadmap §13.9 restated as an independently checkable list) are checked against real, re-run evidence from `ODY-S04-101`–`114`, recorded in a traceability matrix and quality report, and `SLICE-04_IMPLEMENTATION_BACKLOG.md` is updated to reflect the revision's honest state — closing roadmap milestone gate `GATE-C — Character Playable`.

## 2. Why this task exists

- Problem: fourteen separate tasks (`101`–`114`, including the `113a` gap fix) each closed with their own evidence, but nothing has checked all 14 roadmap-derived exit criteria together, against fresh evidence, in one place.
- Value: gives the product owner one document to review before formally accepting `SLICE-04`'s closure and `GATE-C`'s milestone gate, instead of reconstructing the picture from fourteen task contracts.
- Enabling relationship: this is the last task of `SLICE-04_IMPLEMENTATION_BACKLOG.md`; closing it (once accepted) closes the backlog itself and `GATE-C`.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`
- `AGENTS.md`
- `PLANS.md` §1
- `17_Roadmap_Odyssey_VTT_v0.11.md` §13.9 (exit criteria), §13.10 (Milestone `M5`/`GATE-C`)
- `docs/tasks/active/ODY-S03-009_SLICE_03_Acceptance_And_Closure_Gate.md` and `ODY-S03-009_Traceability_and_Quality_Report.md` (structural precedent: separate report file, real re-run evidence per criterion, an honestly-recorded nuance for one criterion rather than a silently inflated Pass or an over-cautious Blocked, owner-acceptance statement deliberately withheld for a follow-up commit)
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` §2 (scope-narrowing decisions, not reopened), §3 (the 14 exit criteria, cross-referenced not redefined), §5/§6 (each child task's own final row/boundary)
- `docs/tasks/active/ODY-S04-101_Character_Aggregate_Lifecycle_Skeleton_Sqlite_Persistence.md` through `ODY-S04-114_Vertical_Slice_Integration.md`'s task contracts §9 (Acceptance criteria) and §17 (Completion evidence) — the factual source for every citation in the traceability report, not recalled from memory

### Requirement and test IDs

- Requirement IDs: `SLICE-04_IMPLEMENTATION_BACKLOG.md` §3, all 14 criteria
- Existing test IDs: `TC-CHAR-001`–`166` (all re-verified, not duplicated), plus every pre-existing `TC-BOARD`/`TC-DICE`/`TC-PERSIST`/`TC-NET`/`TC-ARCH`/`TC-CI` TestCaseId from earlier slices (full-suite re-run)
- New test IDs to introduce: None

### Task-safe private context

- Approved summary / references: None

## 4. Verified current state

### Verified facts

- `ODY-S04-101`–`114` (including the `ODY-S04-113a` gap fix) are all `Done`/merged on `main` — confirmed via `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md`'s header (PRs [#85](https://github.com/odyssey-services/Odyssey_VTT/pull/85)–[#98](https://github.com/odyssey-services/Odyssey_VTT/pull/98)).
- Owner-directed review of `ODY-S04-114`'s own report found that `SqliteCharacterRepository.HistoryEventTypes` — the hand-maintained whitelist `GetCharacterHistory` filters against — does not track any event type from `ODY-S04-106` (skill purchase, critical evidence, advancement recommendation), `ODY-S04-107` (revert/respec), `ODY-S04-108` (ability acquisition), or `ODY-S04-109` (resource/anatomy change); only `ODY-S04-102`'s ownership events and `ODY-S04-105`'s `character_attribute_increased` are tracked among "mechanics"-adjacent events, alongside lifecycle/draft/archive/dead/restore/import/migration events from `101`/`103`/`104`/`110`/`111`/`112`/`113`.
- Checked against the literal wording of all 14 `SLICE-04_IMPLEMENTATION_BACKLOG.md` §3 criteria: **no criterion requires those specific missing event types to appear in `CharacterHistoryProjection`.** Criterion 10 ("Archive and Dead preserve history... renderable via `CharacterHistoryProjection`") is satisfied — `character_archived`/`character_deleted`/`character_died`/`character_restored` are all tracked. This gap is therefore a real product-quality shortfall in `CharacterHistoryProjection`'s completeness (ADR-022 §3.6: "groups... Character-significant history entries for UI/reconnect/search surfaces"), but it does not fail any of the 14 stated exit criteria and does not block `GATE-C` on its own stated terms.
- This task's own precedent (`ODY-S03-009`, criterion 4) already establishes the correct honest-middle-ground pattern for exactly this situation: a real, verified nuance that is neither hidden inside a plain "Pass" nor over-stated as "Blocked," recorded explicitly with its own scope note and a named follow-up task.

### Assumptions

None — every exit-criterion status in the traceability report must cite a specific, re-run test, a specific script output, or a specific, freshly-repeated code inspection, not an assumption.

## 5. Scope

### In scope

- A separate traceability/quality report file, `docs/tasks/active/ODY-S04-115_Traceability_and_Quality_Report.md` (structural choice mirroring `ODY-S03-009`/`ODY-S02-015`'s own precedent), covering all 14 `SLICE-04_IMPLEMENTATION_BACKLOG.md` §3 exit criteria with direct test-method-level or code-inspection-level evidence.
- Recording the `HistoryEventTypes` completeness gap (section 4) in the traceability report as an explicit, honestly-worded known limitation — not against any specific numbered criterion (since none applies), but as its own named finding — together with a named follow-up task ID reserved for fixing it (this task does not fix it itself; see "Out of scope").
- Point-updates to `SLICE-04_IMPLEMENTATION_BACKLOG.md` (header, overall revision status) recording the revision's honest technical state (14 of 14 criteria Pass, the `HistoryEventTypes` finding recorded with its own follow-up task reference) — without writing the formal owner-acceptance date/confirmation (see "Out of scope").

### Out of scope, and why

- **Any new production code or new tests.** Confirmed: this task's diff must touch only documentation files; no new `TC-*` ID is introduced.
- **Fixing the `HistoryEventTypes` completeness gap itself.** That is production code, and per `PLANS.md`/this repository's own established convention (`ODY-S04-113a`'s own precedent), a defect found during review or closure is fixed by its own dedicated task, not folded into a closure/traceability task that is expected to touch documentation only. This task reserves and names that follow-up task ID (`ODY-S04-115a`, mirroring the `-a` gap-fix suffix convention already used for `ODY-UI-01-002a`/`007a`/`ODY-S04-113a`) but does not implement it.
- **Reopening any already-accepted ADR** (`ADR-022`–`026`, `ADR-002`, `ADR-012`, `ADR-013`, `ADR-017`, `ADR-019`, `ADR-023`, `ADR-024`, `ADR-025`, all already used by `101`–`114`). This task cites their already-accepted content, never revises it.
- **The formal owner-acceptance statement (date, explicit confirmation).** Per this task's own instruction: that statement is added by a separate, small, point-fix commit after the product owner explicitly confirms acceptance in conversation — not written speculatively here, per the `ODY-S03-009`/`ODY-S02-015`/`ODY-S01-014` precedent this task is explicitly told to follow.

### Allowed paths

```text
docs/tasks/active/ODY-S04-115_SLICE_04_Acceptance_And_Closure_Gate.md
docs/tasks/active/ODY-S04-115_Traceability_and_Quality_Report.md
docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
Packages/**
DotNet/**
docs/adr/**
docs/tasks/active/ODY-S04-101_Character_Aggregate_Lifecycle_Skeleton_Sqlite_Persistence.md through ODY-S04-114_Vertical_Slice_Integration.md
```

## 6. Technical constraints

- Module ownership and dependency direction: Not applicable — no production code.
- Authoritative-state and transaction boundary: Not applicable.
- Time / RNG rule: Not applicable.
- Unity / thread / lifetime rule: Not applicable.
- Dependency / licensing rule: No new dependency.
- Security / privacy / redaction rule: Not applicable — this task verifies existing redaction/audit evidence (criteria 8, 11), introduces none.
- Performance or platform constraint: Not applicable.
- Other: Not applicable.

## 7. Expected behavior

### Scenario 1 — every exit criterion has real evidence

**Given** the merged state of `101`–`114`
**When** each of the 14 `SLICE-04_IMPLEMENTATION_BACKLOG.md` §3 criteria is checked
**Then** all 14 cite a specific, re-run test method, script output, or code inspection, and are marked Pass; the `HistoryEventTypes` completeness gap is recorded as a named finding with its own follow-up task reference, distinct from and not conflated with any of the 14 numbered criteria.

### Scenario 2 — a gap among the 14, if found, is reported, not hidden

**Given** a criterion with weak or missing evidence
**When** this task checks it
**Then** it is recorded as an open gap, not marked Pass — not expected here based on section 4's own analysis (the one known nuance found does not map to any of the 14 criteria's literal wording), but this task must still perform the check for real rather than assume the outcome.

### Required invariants

- No criterion is marked Pass without a specific, cited, re-run test, script output, or fresh code inspection.
- The `HistoryEventTypes` finding is recorded plainly, with a named follow-up task ID, never smoothed over or omitted.
- The owner-acceptance statement is not written by this task.

## 8. Deliverables

- Production code: None.
- Tests: None (all evidence is re-running existing tests or fresh code inspection, not writing new tests).
- Scripts / CI: None.
- Configuration: None.
- Documentation: This task contract, `ODY-S04-115_Traceability_and_Quality_Report.md`, `SLICE-04_IMPLEMENTATION_BACKLOG.md` (header/closure-status note, and a reserved row for `ODY-S04-115a`).
- Generated evidence or build artifacts: None persisted beyond the traceability report's recorded command output.
- Migration / recovery material: Not applicable.

## 9. Acceptance criteria

1. All 14 `SLICE-04_IMPLEMENTATION_BACKLOG.md` §3 exit criteria are checked with direct, re-run evidence or fresh code inspection, not restated prior claims.
2. Criteria 1–2 (no Vehicle forced into Character; no `CharacterLevel` field) are confirmed by fresh code inspection of `Odyssey.Domain.Character`/`CharacterRecord` in this rehearsal, not merely cited from a prior task's own claim.
3. Criterion 9 (unrelated-section concurrent commits) is confirmed by a fresh re-run of its owning test(s), cited by exact test method name.
4. The `HistoryEventTypes` completeness gap (section 4) is recorded as its own named finding in the traceability report, explicitly stating it does not fail any of the 14 numbered criteria (with the specific reasoning from section 4), and reserving/naming `ODY-S04-115a` as its follow-up task.
5. If any of the 14 criteria lacked real evidence, this task would report it as an open gap rather than marking Pass.
6. `SLICE-04_IMPLEMENTATION_BACKLOG.md` records the revision's honest technical state (14 of 14 criteria Pass, the `HistoryEventTypes` finding and its follow-up task reference) without writing the formal owner-acceptance date/confirmation and without declaring the revision "fully complete" ahead of that acceptance.
7. No production or test code is touched.
8. All required validation commands (section 10) pass.

The task is not complete while any mandatory criterion is unverified, failed, or silently deferred.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| None (new) | — | This task re-runs existing `TC-CHAR-*` (plus the full pre-existing suite) as evidence; it introduces no new TestCaseId | — |

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

```bash
dotnet build DotNet/Odyssey.Core.sln
dotnet test DotNet/Odyssey.Core.sln
```

### Manual validation

- None — all acceptance evidence is automated, script-based, or direct code inspection.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 development machine; CI runs the pure .NET solution.
- Unity editor or Player profile: Not applicable — no Unity re-verification performed (no `SLICE-04` task touched Unity/UI; verify this by a fresh check and record it in the traceability report).
- Scripting backend: Not applicable.
- Network topology or database fixture: Not applicable — this task runs existing tests, it does not add fixtures.
- Other: None.

### Validation not required by this task

- Any full-content-catalog/production-balance validation — `SLICE-04_IMPLEMENTATION_BACKLOG.md` §2.3, out of scope for the whole revision.
- `AssistantGM`/delegation testing — `ADR-019`'s own deferred scope, not reopened.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None.
- Version fields affected: None.
- Migration or upcaster: None required.
- Forward / backward behavior: Not applicable.
- Rollback method: Revert the branch.
- Data-loss risk and protection: None — documentation-only change.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: None new.
- Trust boundaries: Not applicable.
- Authorization / audience checks: Not applicable — this task verifies existing evidence (criteria 4, 8), introduces none.
- Redaction requirements: Not applicable — criterion 11's Pass status cites existing `ODY-S04-112`/`ADR-026` evidence, unmodified.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: Not applicable — no new test introduced; existing security-relevant tests (owner/MainGM gating, export redaction, cross-Character revert rejection) are cited, not re-authored.

## 14. Planning and execution mode

- Planning mode: `Brief plan`
- Reason for selected mode: Checked against `PLANS.md` §1's conditions individually, the same way `ODY-S03-009`/`ODY-S02-015` did for their own slices, not assumed by analogy alone. (1) Contained in one area — documentation only. (2) Does not change a public contract, persisted format, protocol, permissions, redaction, dependency graph, package version, or build pipeline. (3) One clear path — re-run existing validation, map results to the 14 criteria, write the report, being explicit about the one finding that carries a scope note. (4) Fits one focused PR. (5) No migration or recovery procedure required. `PLANS.md`'s ExecPlan triggers do not apply — no port/DTO/event/command/schema/protocol/manifest/package/build-profile/migration is introduced; this task only reads and re-runs already-existing behavior, plus performs a few direct code-inspection greps.
- Brief plan:
  1. Files inspected: `SLICE-04_IMPLEMENTATION_BACKLOG.md` §2/§3/§5/§6; `ODY-S03-009`'s task contract and traceability report (structural reference); each `101`–`114` task contract for exact commit/PR/test-method evidence; `Tests/Metadata/test-catalog.json` for TestCaseId ownership; direct `grep`/inspection of `Odyssey.Domain.Character`/`CharacterRecord` for criteria 1/2, and of `SqliteCharacterRepository.HistoryEventTypes` for the known finding (section 4).
  2. Intended change: one traceability/quality report file, this task contract, a backlog closure-status update (including reserving `ODY-S04-115a`).
  3. Validation: re-run the full existing test/script suite fresh; no new tests written.
  4. Non-goals: no production code (including no fix for the `HistoryEventTypes` gap — that is `ODY-S04-115a`), no reopened ADR decisions, no owner-acceptance statement.
- ExecPlan path: Not required.
- Expected pull request count: 1
- Milestone or sequencing constraints: None beyond `101`–`114` already merged (backlog's stated dependency, already satisfied). This is the last task in `SLICE-04_IMPLEMENTATION_BACKLOG.md`.

## 15. Documentation and versioning impact

- Documents that must change: `SLICE-04_IMPLEMENTATION_BACKLOG.md` (header/closure-status note, reserved `ODY-S04-115a` row).
- Documents that must not change: any ADR, `101`–`114` task contracts, `SLICE-04_BACKLOG.md` (the prerequisite-revision backlog, referenced only as a structural pattern).
- Application version change: No.
- Schema / format / contract / manifest / protocol / ruleset version change: None.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass (re-run of existing suite).
- [x] Required manual checks are completed (None required).
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`. Formal `SLICE-04` acceptance is a separate, explicit product-owner action, to be given in conversation and recorded by a follow-up point-fix commit.

## 17. Completion evidence

### Changed files / areas

- `docs/tasks/active/ODY-S04-115_SLICE_04_Acceptance_And_Closure_Gate.md` — this task contract, completion evidence filled in.
- `docs/tasks/active/ODY-S04-115_Traceability_and_Quality_Report.md` — new file: the 14-criterion traceability matrix, TestCase matrix, quality-report command table, and the `HistoryEventTypes` named finding, mirroring `ODY-S03-009`'s own structure.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` — header/status line and row 14/15 of the ordered backlog table updated to reflect `ODY-S04-114`'s actual merged state and this task's own 14/14-Pass, owner-acceptance-Pending result.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Pass | `FORMAT-001 PASS repository text formatting checks passed` |
| `.\scripts\verify-test-structure.ps1` | Pass | `TC-ARCH-001 PASS valid ADR-001 graph passes`; four controlled-invalid fixtures correctly rejected; exit code 0 |
| `dotnet build DotNet/Odyssey.Core.sln` | Pass | 0 warnings, 0 errors |
| `dotnet test DotNet/Odyssey.Core.sln` | Pass | 474/474 passed, 0 failed (Contracts 1, Domain 56, Networking 67, Unit 105, Architecture 2, Persistence 243) |
| `.\scripts\check-repository-policy.ps1` | Pass | `REPO-POLICY-001`-`005` PASS; `TC-CI-001`-`012` PASS; `Repository policy check passed` |

Full detail, per-criterion citations, and isolated re-runs of the specific tests backing criteria 1, 2, 3, 4, 6, 7, 8, 9, 10, 11, 12, 13 are in `ODY-S04-115_Traceability_and_Quality_Report.md`.

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Pass | All 14 `SLICE-04_IMPLEMENTATION_BACKLOG.md` §3 criteria checked with direct, re-run evidence or fresh code inspection — traceability report section 1. |
| AC-2 | Pass | Criteria 1–2 confirmed by fresh `grep` inspection of `Odyssey.Domain.Character`/`CharacterRecord` performed in this rehearsal (no Vehicle type, no `CharacterLevel` field) — traceability report section 1, rows 1–2. |
| AC-3 | Pass | Criterion 9 confirmed by a fresh, isolated re-run of `CharacterDeadRestoredTests.ConcurrentEdit_LifecycleDeath_AndIndependentMechanicsPurchase_CommitWithoutFalseConflict` (`TC-CHAR-143`), cited by exact method name — traceability report section 1, row 9. |
| AC-4 | Pass | The `HistoryEventTypes` completeness gap is recorded as its own named finding (traceability report section 1a), explicitly stating it fails none of the 14 criteria (with the specific per-criterion-10 reasoning), and reserves `ODY-S04-115a` as its follow-up. |
| AC-5 | Pass | All 14 criteria had real, citable evidence in this rehearsal; none required reporting as an open gap. |
| AC-6 | Pass | `SLICE-04_IMPLEMENTATION_BACKLOG.md`'s header/status line and row 15 now state 14/14 Pass and the `HistoryEventTypes` finding/follow-up reference, without any owner-acceptance date/confirmation text and without declaring the revision "fully complete." |
| AC-7 | Pass | `git status --porcelain` on this task's branch touches only the three allowed documentation paths — no `Packages/**`/`DotNet/**` change. |
| AC-8 | Pass | All five required commands (section 10) passed in this rehearsal — validation-results table above. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: Not applicable — no build artifact produced by this task.
- Checksums: Not applicable.
- Test or quality report: `docs/tasks/active/ODY-S04-115_Traceability_and_Quality_Report.md`.

### Owner acceptance

**Pending.** A separate, explicit product-owner action, to be recorded by a small point-fix commit after the product owner confirms acceptance in conversation, per the `ODY-S03-009`/`ODY-S02-015`/`ODY-S01-014` precedent.

### Known limitations

- `SqliteCharacterRepository.HistoryEventTypes` does not track `ODY-S04-106`–`109`'s own event types (section 4) — does not fail any of the 14 numbered exit criteria, but is a real completeness gap in `CharacterHistoryProjection`. Follow-up task: `ODY-S04-115a` (reserved by this task, not implemented here).

### Follow-up tasks

- `ODY-S04-115a` — fix `SqliteCharacterRepository.HistoryEventTypes` to track `ODY-S04-106`–`109`'s own event types, restoring `CharacterHistoryProjection`'s completeness per `ADR-022` §3.6. Reserved by this task; not implemented here.

### Self-review summary

- Scope review: Diff touches only the three allowed documentation paths; no production/test code, no new `TC-*` ID, no reopened ADR, no owner-acceptance statement written.
- Architecture review: Not applicable — no architecture changed; every citation traces to already-accepted `ADR-022`-`026` content and already-merged `ODY-S04-101`-`114` code, re-inspected rather than assumed.
- Test review: Full suite re-run fresh (474/474); the specific tests backing criteria 1–13 were individually isolated and re-run in this rehearsal, not merely inferred from the aggregate pass.
- Security/privacy review: Not applicable for new work — existing security-relevant evidence (MainGM/owner gating, cross-Character revert rejection, export redaction) was cited, not re-authored.
- Documentation/version review: Only `SLICE-04_IMPLEMENTATION_BACKLOG.md`'s header and rows 14/15 changed to reflect the now-merged `ODY-S04-114` state and this task's own honest 14/14-Pass, acceptance-Pending result; no ADR or prior task contract touched.

## 18. Blockers, decisions, and change control

### Blockers

- None expected. All 14 criteria are expected to be Pass with real evidence (section 4's own analysis); the `HistoryEventTypes` finding is a recorded nuance with its own named follow-up task, not a blocker to this task's own closure or to `SLICE-04`'s technical completion.

### Decisions made during execution

- 2026-09-03 — Decision: the `HistoryEventTypes` completeness gap found during owner-directed review of `ODY-S04-114` is recorded in this closure task as a named finding with a reserved follow-up task ID (`ODY-S04-115a`), rather than being fixed inline (out of scope for a documentation-only closure task) or silently omitted from the traceability report — Authority / approval: Product owner ("Делай тз под 115"; the finding itself was raised in the immediately preceding review turn).

### Approved task changes

- None yet.

---

## Template completion rules

1. Remove instructional examples that do not apply, but keep all numbered section headings.
2. Write `None` or `Not applicable` instead of leaving an ambiguous blank.
3. A task may be marked `Ready` only when goal, scope, authorities, acceptance criteria, validation, and required decisions are complete.
4. A task may be marked `In Progress` only after the working branch exists and the required ExecPlan is created when applicable.
5. A task may be marked `In Review` only after completion evidence is filled honestly.
6. A task may be marked `Done` only after required review and all non-deferred acceptance criteria pass.
7. Deferred work requires an explicit follow-up Task ID; it cannot disappear into prose.
8. Never mark an unrun validation command as passed.
9. Never update golden files, snapshots, manifests, or expected outputs only to make a failing test green without verifying the intended behavior.
10. Never broaden MVP scope or create a new architectural rule inside a task; request an owner decision or ADR instead.
