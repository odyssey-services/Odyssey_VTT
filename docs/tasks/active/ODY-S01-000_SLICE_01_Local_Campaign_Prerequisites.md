# ODY-S01-000 - SLICE-01 Local Campaign Prerequisites

**Status:** Draft  
**Roadmap stage / slice:** SLICE-01  
**Owner:** Unassigned  
**Requested by:** Product owner  
**Branch:** `feat/slice-01-prerequisites-scaffold`  
**Pull request:** Not opened  
**ExecPlan:** Not required  
**Created:** 2026-08-20  
**Last updated:** 2026-08-20 UTC

## 1. Goal

Close all roadmap Stage 2 prerequisite requirements — four ADRs (campaign format, snapshot + append-only journal, migration runner, owner key storage baseline) and the `SP-02 — Persistence Reliability` technical spike with its report — before any `SLICE-01` vertical-slice implementation work (campaign creation, map import, scene, tokens, movement, restart, saved-state check, backup restore) begins.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-00`/`M1` is closed, but no `SLICE-01` organizational structure exists yet. Roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` section 10.2 requires five prerequisite documents before Stage 2 implementation can begin; four of them (the ADRs) do not exist yet.
- Value or risk reduction: Prevents implementation from starting on an undecided persistence format, journal, or migration strategy — the same architecture-first discipline already used for `SLICE-00`.
- Blocking or enabling relationship: Blocks all `SLICE-01` vertical-slice work. Enables a future implementation backlog revision once the four ADRs are `Accepted` and `SP-02` is complete.

## 3. Authorities and requirement references

### Required authorities

- `17_Roadmap_Odyssey_VTT_v0.11.md`, section 10 (Этап 2 — Persistence и Local Campaign), specifically section 10.2 (prerequisite documents) and section 10.4 (`SP-02` scope) — source of the prerequisite list this task organizes.
- `05_Persistence_Odyssey_VTT_v0.8.md` — context source for the campaign format, snapshot/journal, and migration runner ADRs; exact sections are cited per child task in `docs/tasks/SLICE-01_BACKLOG.md` section 4.
- `Documentation/21_Security_And_Privacy_Odyssey_VTT_v0.1.md` section 5 — private, non-tracked (gitignored `Documentation/`), already owner-approved. This is the source principle for the future Owner Key Storage Baseline ADR. Quoted verbatim below per explicit product owner instruction, not paraphrased:

  > "Owner key никогда не входит в файл кампании, `campaign.db`, `.odcamp` или backup. Owner key хранится через предоставляемое ОС защищённое хранилище (secure storage конкретной платформы), а не в виде обычного файла рядом с кампанией.
  >
  > Конкретный механизм хранения (какой именно OS API, формат, ротация, восстановление при потере) — предмет отдельной **ADR owner key storage baseline**. Эта ADR реализует принцип, зафиксированный здесь, и является источником истины для деталей реализации. Данный документ не дублирует и не предвосхищает решения этой ADR."

- `docs/tasks/SLICE-01_BACKLOG.md` (this task's governed backlog).
- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`.
- `AGENTS.md`, `PLANS.md`, `docs/tasks/TASK_TEMPLATE.md`.
- `docs/tasks/completed/ODY-S00-000_SLICE_00_Technical_Skeleton.md` and `docs/tasks/completed/ODY-S00-010_Traceability_and_Quality_Report.md` — structural precedent for this parent task and confirmation that `SLICE-00`/`M1` is closed (merge commit `7fbc9b0b7af242e6400538baf35a419536805872`).

### Requirement and test IDs

- Requirement IDs: `SLICE-01` (prerequisites revision only), Milestone `M2` (not closed by this task), roadmap `SP-02`.
- Existing test IDs: None yet defined for `SLICE-01`.
- New test IDs to introduce: None by this task. Each ADR/spike child task defines its own if needed.

### Task-safe private context

- Approved summary / references: This task contract cites `Documentation/21_Security_And_Privacy_Odyssey_VTT_v0.1.md` section 5 verbatim, per explicit product owner instruction, as the foundational principle for the future Owner Key Storage Baseline ADR (`ODY-S01-004`). No other private document content, campaign data, or secrets are copied into this file or into `docs/tasks/SLICE-01_BACKLOG.md`.

## 4. Verified current state

### Verified facts

- `SLICE-00` is complete and `M1` is closed; the product owner explicitly accepted closure on 2026-08-19 (merge commit `7fbc9b0b7af242e6400538baf35a419536805872`), per `docs/tasks/completed/ODY-S00-010_Traceability_and_Quality_Report.md`.
- No `SLICE-01` task contract, backlog, or ADR exists anywhere in the repository as of this activation.
- `Documentation/21_Security_And_Privacy_Odyssey_VTT_v0.1.md` v0.1 exists locally; it is private, gitignored, and not tracked in this repository, and is already product-owner-approved per this task's activation instruction.
- Roadmap section 10.2 names five prerequisite documents for Stage 2: `05_Persistence_Odyssey_VTT_v0.8.md` (already accepted and tracked), plus four ADRs (campaign format, snapshot + append-only journal, migration runner, owner key storage baseline) that do not exist yet, plus the "начальная часть" of `21_Security_And_Privacy.md` (already satisfied per the fact above).

### Assumptions

- None.

## 5. Scope

### In scope

- Creating this parent task contract (`ODY-S01-000`).
- Creating `docs/tasks/SLICE-01_BACKLOG.md`, listing and sequencing exactly five child tasks: four ADR tasks (`ODY-S01-001` through `ODY-S01-004`) and the `SP-02` technical spike task (`ODY-S01-005`). This parent task organizes and sequences them; it does not author their content.

### Out of scope

- Any persistence implementation code, `.odcamp` physical implementation, SQLite provider library selection or integration, or migration runner as executable code — all deferred to a future implementation backlog revision, created only after the four ADRs below are `Accepted`.
- Any UI (scenes, tokens, campaign creation flow), networking, or permissions runtime work.
- The `SLICE-01` vertical slice itself (roadmap section 10.5: create campaign → import one test map → create a scene → place two tokens → change their positions → close the application → reopen the campaign → verify saved state → restore state from backup) — not started by this task.
- Any ADR content. Each ADR's content is authored in its own separate child task, one at a time, by a separate future ТЗ. This task creates only the parent contract and backlog scaffold.
- Creating or modifying `docs/tasks/active/ODY-S01-001_...md` through `ODY-S01-005_...md`. These child task contract files are not created by this activation.

### Allowed paths

```text
docs/tasks/active/ODY-S01-000_SLICE_01_Local_Campaign_Prerequisites.md
docs/tasks/SLICE-01_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/** (no ADR content is created by this task)
Documentation/21_Security_And_Privacy_Odyssey_VTT_v0.1.md (private, non-tracked; must not be created or modified by this or any task in this repository)
docs/plans/** (Brief plan mode; no ExecPlan is created)
docs/tasks/active/ODY-S01-001_*.md through ODY-S01-005_*.md (child task contracts; not created by this activation)
Any production code, test code, script, Unity, or package file
```

## 6. Technical constraints

- Module ownership and dependency direction: Not applicable.
- Authoritative-state and transaction boundary: Not applicable.
- Serialization / compatibility boundary: Not applicable; any serialization decision belongs to the ADR child tasks, not this scaffold.
- Time / RNG rule: Not applicable.
- Unity / thread / lifetime rule: Not applicable.
- Dependency / licensing rule: No new dependency, GitHub Action, Unity package, executable, or downloadable tool is introduced or approved by this contract.
- Security / privacy / redaction rule: `Documentation/21_Security_And_Privacy_Odyssey_VTT_v0.1.md` remains private and non-tracked; only its section 5 principle is quoted verbatim into this tracked task file, per explicit product owner instruction. No other private content, secrets, personal data, or hidden campaign content may enter the repository.
- Performance or platform constraint: Not applicable.
- Other: None.

## 7. Expected behavior

### Scenario 1 - Parent task and backlog exist and are internally consistent

**Given** `SLICE-00`/`M1` is closed and no `SLICE-01` organizational structure exists  
**When** this task contract and `docs/tasks/SLICE-01_BACKLOG.md` are created  
**Then** the backlog lists exactly five ordered child tasks (four ADRs plus `SP-02`), each with clear scope boundaries and dependency rules, and no child task contract file or ADR file exists as a result.

### Required invariants

- No ADR content is authored by this task.
- No implementation code, script, or configuration is introduced.
- The `SLICE-01` vertical-slice implementation backlog is explicitly deferred to a future backlog revision, not created here.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: This task contract; `docs/tasks/SLICE-01_BACKLOG.md`.
- Generated evidence or build artifacts: None.
- Migration / recovery material: Not applicable.

## 9. Acceptance criteria

1. `docs/tasks/active/ODY-S01-000_SLICE_01_Local_Campaign_Prerequisites.md` exists, following `docs/tasks/TASK_TEMPLATE.md` with all 18 numbered sections present.
2. `docs/tasks/SLICE-01_BACKLOG.md` exists, mirrors the structure of `docs/tasks/SLICE-00_BACKLOG.md` (Purpose, Slice exit criteria, Ordered backlog table, Task boundaries, Dependency rules, Global non-goals, Backlog change control), and lists exactly 5 ordered child tasks with IDs `ODY-S01-001` through `ODY-S01-005`.
3. No child task contract file (`ODY-S01-001...md` through `ODY-S01-005...md`) exists as a result of this task.
4. No ADR file exists as a result of this task.
5. `scripts/verify-format.ps1` and `scripts/check-repository-policy.ps1` both pass unchanged; this task introduces no new required-path expectations into either script.

The task is not complete while any mandatory criterion is unverified, failed, or silently deferred.

## 10. Tests and validation

### Required automated tests

None. This is a documentation-only organizational task; no new test IDs are introduced.

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- Owner review of the parent task contract and backlog scope/ordering before any `ODY-S01-00X` child task is activated.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 (PowerShell validation only; no Unity or .NET build is required since no production/test/script/config/workflow file is touched).
- Unity editor or Player profile: Not applicable.
- Scripting backend: Not applicable.
- Network topology or database fixture: None.
- Other: None.

### Validation not required by this task

- `dotnet build`/`dotnet test`, Unity compile/EditMode/PlayMode, `verify-ci.ps1`, `verify-unity-project.ps1`, `verify-repository.ps1`, `verify-build-identity.ps1`, `test-serialization-aot.ps1`, `test-unity.ps1`, `build-dev.ps1`, `test-player-smoke.ps1`: none of these are affected because no production code, test code, script, Unity asset, package, or CI workflow file is touched by this task.

## 11. Compatibility, migration, and rollback

Not applicable. This task introduces no persisted state, public contract, protocol, package, Unity version, or build identity change.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | - | - | - | - |

No new dependency, GitHub Action, Unity package, executable, or download is approved by this contract.

## 13. Security, privacy, and hidden information

- Data classes handled: `Documentation/21_Security_And_Privacy_Odyssey_VTT_v0.1.md` section 5 is quoted verbatim (a public-safe, already-approved principle statement containing no secrets); no other private or hidden content is handled.
- Trust boundaries: Not applicable beyond the redaction rule below.
- Authorization / audience checks: Not applicable.
- Redaction requirements: No secrets, personal data, local paths, or hidden campaign content may be introduced; only the explicitly authorized section 5 principle quote enters this tracked file.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: None; concrete security/key-storage mechanism decisions and their tests are deferred to the `ODY-S01-004` Owner Key Storage Baseline ADR child task.

## 14. Planning and execution mode

- Planning mode: `Brief plan`
- Reason for selected mode: Pure organizational scaffold (parent task contract plus backlog); it introduces no new architecture, module, public contract, persisted format, protocol, permissions model, dependency graph, Unity/package version, or build pipeline change. It has one clear implementation path, is completable and validatable in a single pull request, and requires no migration or recovery procedure.
- ExecPlan path: Not required
- Expected pull request count: 1 (this scaffold). Each subsequent ADR or `SP-02` child task will be its own separate task and pull request, not part of this activation.
- Milestone or sequencing constraints: Do not create any `ODY-S01-00X` child task contract until this parent task and backlog are reviewed. Do not begin ADR content authoring or `SP-02` execution under this task.

## 15. Documentation and versioning impact

- Documents that must change: This task contract; `docs/tasks/SLICE-01_BACKLOG.md`.
- Documents that must not change: All ADRs, Technical Development Baseline, Active Documentation Baseline, product requirement documents, ExecPlans, and `Documentation/21_Security_And_Privacy_Odyssey_VTT_v0.1.md` (private, non-tracked, read-only source for this task).
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [ ] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [ ] Required automated tests pass.
- [ ] Required manual checks are completed.
- [ ] Required commands and their real results are recorded.
- [ ] Architecture and dependency rules remain valid.
- [ ] Security, privacy, redaction, and audience rules are verified where applicable.
- [ ] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [ ] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [ ] Documentation is updated only where materially required.
- [ ] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

Fill this section with real results before moving the task to `Done`. Not yet applicable — this activation only creates the parent task contract and backlog scaffold; no child task work has started.

### Changed files / areas

- This task contract and `docs/tasks/SLICE-01_BACKLOG.md` were created from repository authorities (roadmap, `05_Persistence`, `Documentation/21_Security_And_Privacy_Odyssey_VTT_v0.1.md` section 5, and the `SLICE-00` structural precedent).

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Passed | See final report / commit evidence. |
| `.\scripts\check-repository-policy.ps1` | Passed | See final report / commit evidence. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 through AC-5 | Passed | Both files created per template/backlog structure; no child task or ADR file created; validation commands pass. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: Not applicable.

### Known limitations

- This scaffold does not decide any technical question. All four ADRs and the `SP-02` report remain to be authored in separate future child tasks.

### Follow-up tasks

- `ODY-S01-001` through `ODY-S01-005`, to be created one at a time by separate future task activations, per `docs/tasks/SLICE-01_BACKLOG.md`.

### Self-review summary

- Scope review: Contract stays within organizational scaffold boundary; no ADR content, no implementation code, no vertical-slice work introduced.
- Architecture review: No architecture, ADR, or module-boundary change is introduced.
- Test review: No new TestCase IDs are introduced.
- Security/privacy review: Only the explicitly authorized `Documentation/21_Security_And_Privacy_Odyssey_VTT_v0.1.md` section 5 quote enters this tracked file; no other private content.
- Documentation/version review: No baseline, ADR, TDB, schema, protocol, ruleset, package, or application version is changed.

## 18. Blockers, decisions, and change control

### Blockers

- None at contract-creation. This contract requires owner review before any `ODY-S01-00X` child task is activated.

### Decisions made during execution

- 2026-08-20 - Create the `ODY-S01-000` parent task contract and `docs/tasks/SLICE-01_BACKLOG.md` as an organizational scaffold only, mirroring the `ODY-S00-000`/`SLICE-00_BACKLOG.md` pattern, following explicit product owner request after `SLICE-00`/`M1` closure - Authority / approval: product owner instruction.

### Approved task changes

- None yet.
