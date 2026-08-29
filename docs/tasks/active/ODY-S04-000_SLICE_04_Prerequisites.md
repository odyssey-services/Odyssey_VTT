# ODY-S04-000 — SLICE-04 Characters and Progression Prerequisites

**Status:** In Review  
**Roadmap stage / slice:** SLICE-04  
**Owner:** Codex (agent)  
**Requested by:** Product owner  
**Branch:** `feat/ody-s04-000-slice-04-prerequisites`  
**Pull request:** [#79](https://github.com/odyssey-services/Odyssey_VTT/pull/79)  
**ExecPlan:** Not required  
**Created:** 2026-08-30  
**Last updated:** 2026-08-29 22:09 UTC

## 1. Goal

Determine which new ADRs are required before official roadmap `SLICE-04 — Персонаж и развитие` implementation can begin, and record that decision in `docs/tasks/SLICE-04_BACKLOG.md` plus this parent task contract. This task does not implement Character mechanics or author the ADRs themselves.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-03` and `SLICE-UI-01` are closed, but no official `SLICE-04` prerequisite structure exists yet.
- Value or risk reduction: prevents two opposite errors before Character work starts: treating product specs as if they automatically close architecture gaps, or creating speculative ADRs for questions already answered by existing ADRs/specifications.
- Blocking or enabling relationship: blocks `SLICE-04` implementation backlog creation until the required ADR set is explicit and reviewable.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`.
- `AGENTS.md`, `PLANS.md`, `docs/tasks/TASK_TEMPLATE.md`.
- `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` section 13 in full.
- `Documentation/04_Odyssey_Rules_Engine_Odyssey_VTT_v0.6.md` in full.
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` in full.
- `Documentation/03_Domain_Model_Odyssey_VTT_v0.25.md` sections 13-15, 30, 32-40 as Character/Progression-related model context.
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`.
- `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md`.
- `docs/adr/ADR-003_Serialization_Strategy_v1.1.md`.
- `docs/adr/ADR-004_Result_and_Error_Model_v1.0.md`.
- `docs/adr/ADR-005_Dependency_Composition_v1.0.md`.
- `docs/adr/ADR-006_Test_Project_Structure_and_Dual_Unity_DotNet_Compilation_v1.0.md`.
- `docs/adr/ADR-007_Versioning_and_Build_Identity_v1.0.md`.
- `docs/adr/ADR-008_Deterministic_Clock_and_RNG_v1.0.md`.
- `docs/adr/ADR-009_Unity_Project_and_Build_Baseline_v1.1.md`.
- `docs/adr/ADR-010_Logging_Diagnostics_and_Redaction_v1.1.md`.
- `docs/adr/ADR-011_Local_Campaign_Format_v1.1.md`.
- `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md`.
- `docs/adr/ADR-013_Migration_Runner_v1.0.md`.
- `docs/adr/ADR-014_Owner_Key_Storage_Baseline_v1.0.md`.
- `docs/adr/ADR-015_Transport_Abstraction_v1.0.md`.
- `docs/adr/ADR-016_Rendezvous_Relay_Strategy_v1.0.md`.
- `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md`.
- `docs/adr/ADR-018_Identity_Baseline_v1.0.md`.
- `docs/adr/ADR-019_Permissions_Baseline_v1.0.md`.
- `docs/adr/ADR-020_Board_Geometry_And_Movement_Determinism_v1.0.md`.
- `docs/adr/ADR-021_Extended_Audience_And_Selected_Participant_Visibility_v1.0.md`.
- `docs/tasks/SLICE-01_BACKLOG.md`, `docs/tasks/SLICE-02_BACKLOG.md`, and `docs/tasks/SLICE-03_BACKLOG.md` as structure precedents.

### Requirement and test IDs

- Requirement IDs: `ODY-S04-000`, `SLICE-04` prerequisite revision.
- Existing test IDs: None.
- New test IDs to introduce: None.

### Task-safe private context

- Approved summary / references: The non-tracked `Documentation/` product files were read as local private authorities. This tracked task records only section references, sanitized summaries, and already-public ADR/task paths. No private prose, local private path outside the repository, secret, personal data, or hidden campaign content is copied into this file.

## 4. Verified current state

### Verified facts

- `git fetch origin main`, `git checkout main`, `git merge --ff-only origin/main`, and `git log --oneline -10` confirmed local `main` contains PR #78 (`ca5f243`) and PR #77 (`27b2a1d`), so `SLICE-UI-01` closeout and the final `007a`/`007b` fixes are in `main`.
- `SLICE-03` implementation is already present in `main`; recent history includes PR #74 and the earlier `SLICE-03` closure chain.
- The official roadmap section 13 names `SLICE-04 — Персонаж и развитие`, separate from the closed `SLICE-UI-01` trial-UI initiative.
- `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` section 13 scopes Character foundation, Drafts/templates, Mechanics/progression, Ability/resources/anatomy, Ownership/lifecycle operations, an 11-step vertical slice, and exit criteria.
- `Documentation/04_Odyssey_Rules_Engine_Odyssey_VTT_v0.6.md` has a Character and Progression Rules Contract section and already fixes Rules Engine-level attribute, advancement validation, skill advancement, ability, resource, anatomy, revert, and respec rules.
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` exists and covers Character aggregate, Drafts/templates, DevelopmentPool, advancement purchases, abilities/resources/anatomy, ownership/control, editing locks, history, archive/delete, Dead/restore, `.odchar`, Ruleset migration, permissions, commands/events, persistence, networking, reason codes, flows, tests, and readiness criteria.
- `Documentation/03_Domain_Model_Odyssey_VTT_v0.25.md` already lists Character, Character templates/drafts, Progression, Character/Progression commands, transaction boundaries, CharacterHistory, AdministrativeAudit, and recommended aggregate roots/services.
- `docs/adr/ADR-001` through `ADR-021` are all present and accepted. Their scopes cover module boundaries, commands/events, serialization, errors, composition, test structure, versioning/build identity, clock/RNG, Unity baseline, logging/redaction, local campaign format, journal/snapshots, migration runner, owner key storage, transport, relay strategy, reconnect projection, identity, permissions baseline, board geometry, and extended audience.

### Assumptions

- None.

## 5. Scope

### In scope

- Create `docs/tasks/SLICE-04_BACKLOG.md`.
- Create this task contract.
- Decide and justify, for each of the five roadmap/product areas named by the task, whether existing ADRs/specifications are sufficient or a new ADR is required.
- Decide whether ADR files are created by this task or deferred to child tasks.
- Run documentation-only validation.

### Out of scope

- Any production code, Unity asset, package, test, schema, command/event implementation, persistence implementation, networking implementation, or UI.
- Creating ADR files.
- Creating implementation backlog tasks after the prerequisite ADRs.
- Real internet spike work (`ODY-S02-014`/`ADR-016` section 14).
- Contentless content such as concrete attribute/skill/class/ability catalogs.

### Allowed paths

```text
docs/tasks/SLICE-04_BACKLOG.md
docs/tasks/active/ODY-S04-000_SLICE_04_Prerequisites.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Assets/**
Packages/**
DotNet/**
ProjectSettings/**
Documentation/**
```

## 6. Technical constraints

- Module ownership and dependency direction: No code change; future ADRs must preserve `ADR-001`.
- Authoritative-state and transaction boundary: No state mutation; future ADRs must reuse `ADR-002`.
- Serialization / compatibility boundary: No contract created here; future ADRs must reuse `ADR-003`/`ADR-007`/`ADR-011`/`ADR-013`.
- Time / RNG rule: No authoritative logic; future rules/mechanics must reuse `ADR-008`.
- Unity / thread / lifetime rule: Not applicable.
- Dependency / licensing rule: No new dependency, tool, action, package, or download.
- Security / privacy / redaction rule: Private `Documentation/` content stays out of tracked docs except sanitized references/summaries.
- Performance or platform constraint: Not applicable.
- Other: Do not reopen `SLICE-UI-01` or the blocked real internet spike.

## 7. Expected behavior

### Scenario 1 — Prerequisite backlog is ready

**Given** `main` contains `SLICE-03` and `SLICE-UI-01` closure  
**When** `docs/tasks/SLICE-04_BACKLOG.md` is reviewed  
**Then** it identifies exactly which ADRs block `SLICE-04` implementation and which named areas require no new ADR.

### Scenario 2 — No implementation starts

**Given** the Character/Progression product specs contain implementation-ready detail  
**When** this prerequisite task finishes  
**Then** no code, schema, tests, Unity files, or ADR files are changed.

### Required invariants

- Each of the five required analysis areas has an explicit verdict.
- The backlog records no technical spike required, with reasons.
- ADR authoring is deferred to separate child tasks.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `docs/tasks/SLICE-04_BACKLOG.md`; this task contract.
- Generated evidence or build artifacts: None.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. `docs/tasks/SLICE-04_BACKLOG.md` exists and follows the `SLICE-01`/`SLICE-02`/`SLICE-03` prerequisite backlog style: purpose, prerequisite exit criteria, scope decisions, ordered backlog, task boundaries/dependencies, non-goals, and change control.
2. This task contract exists with all 18 numbered sections.
3. The backlog explicitly addresses all five task-specified analysis areas: Character data model/versioning, Drafts/templates, Development economy, Ability/resource/anatomy, and Ownership/lifecycle operations.
4. For each of the five areas, the backlog says either "covered by existing ADR/specification" or "requires new ADR" and gives the reason.
5. The backlog lists the future ADR numbers and subjects required before implementation begins, if any.
6. The backlog explicitly states whether ADR files are created now or deferred, and why.
7. No code, tests, Unity assets, packages, scripts, or private `Documentation/` files are changed.
8. `.\scripts\verify-format.ps1` and `.\scripts\check-repository-policy.ps1` pass.

## 10. Tests and validation

### Required automated tests

None. This is a documentation-only prerequisite task.

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- Product owner reviews the ADR count, boundaries, and `SLICE-04_BACKLOG.md` before any `ODY-S04-00X` child task is activated.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable.
- Scripting backend: Not applicable.
- Network topology or database fixture: None.
- Other: PowerShell validation only.

### Validation not required by this task

- `dotnet build`, `dotnet test`, `test-unity`, `build-dev`, migration rehearsal, player smoke, and performance profiling are not required because no code, test, Unity, schema, package, or CI file changes.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this docs-only PR.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | - | - | - | - |

## 13. Security, privacy, and hidden information

- Data classes handled: task-safe summaries of private product documentation and public repository ADR/task paths.
- Trust boundaries: private `Documentation/` sources are read-only and not copied verbatim into tracked files.
- Authorization / audience checks: Not changed.
- Redaction requirements: no private excerpts, secrets, credentials, personal data, or hidden campaign content in commits/PR text.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: None.

## 14. Planning and execution mode

- Planning mode: `Brief plan`
- Reason for selected mode: The task changes only documentation in `docs/tasks`, creates no ADR content and no public code/schema/protocol/permission contract, has one focused implementation path, and requires only two validation scripts. Future ADR child tasks are expected to use ExecPlans, but this parent scaffold does not need one.
- ExecPlan path: Not required
- Expected pull request count: 1
- Milestone or sequencing constraints: Do not activate child ADR tasks until this prerequisite backlog is reviewed. Do not create the `SLICE-04` implementation backlog until all required ADRs are `Accepted`.

## 15. Documentation and versioning impact

- Documents that must change: `docs/tasks/SLICE-04_BACKLOG.md`; this task contract.
- Documents that must not change: ADR files, implementation backlogs, private `Documentation/` sources, production code, scripts, Unity assets.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed or assigned to owner review.
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

- `docs/tasks/SLICE-04_BACKLOG.md` — new prerequisite backlog for official roadmap `SLICE-04`.
- `docs/tasks/active/ODY-S04-000_SLICE_04_Prerequisites.md` — this task contract.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | `Repository policy check passed`; includes required repository structure, forbidden tracked patterns, LFS policy, ErrorCode registry, workflow policy, and static Unity project/package/toolchain checks. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `docs/tasks/SLICE-04_BACKLOG.md` created with prerequisite backlog structure. |
| AC-2 | Passed | This contract contains all 18 numbered sections. |
| AC-3 | Passed | Backlog section 3 addresses all five required analysis areas. |
| AC-4 | Passed | Backlog section 3 records a verdict and reason for each area. |
| AC-5 | Passed | Backlog sections 2 and 4 list future `ADR-022` through `ADR-025`. |
| AC-6 | Passed | Backlog sections 1, 4, and 7 state ADR files are deferred to child tasks. |
| AC-7 | Passed | Diff scope is docs-only under `docs/tasks`. |
| AC-8 | Passed | Required validation commands passed locally. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: Not applicable.

### Known limitations

- ADRs are not authored by this task. `SLICE-04` implementation backlog remains blocked until all four future ADRs are accepted.

### Follow-up tasks

- `ODY-S04-001` — ADR-022 Character Aggregate, Section Revisions, and History Projection.
- `ODY-S04-002` — ADR-023 Character Drafts, Templates, and Approval Workflow.
- `ODY-S04-003` — ADR-024 Development Economy and Progression Transactions.
- `ODY-S04-004` — ADR-025 Character Ownership, Lifecycle, and Ruleset Migration Operations.

### Self-review summary

- Scope review: Only two `docs/tasks` files are introduced.
- Architecture review: No architecture is changed yet; future ADR boundaries are explicitly identified.
- Test review: No test changes; validation is docs/policy only.
- Security/privacy review: Private product docs are summarized safely and not copied verbatim.
- Documentation/version review: No baseline, ADR, schema, protocol, ruleset, or app version is changed.

## 18. Blockers, decisions, and change control

### Blockers

- `SLICE-04` implementation backlog remains blocked until future `ADR-022` through `ADR-025` are accepted.

### Decisions made during execution

- 2026-08-29 — Decision: create the prerequisite backlog and task contract only; do not create ADR files in `ODY-S04-000`. Authority / approval: `SLICE-01`/`SLICE-02`/`SLICE-03` prerequisite backlog precedent and current task scope.
- 2026-08-29 — Decision: require four future ADRs: Character aggregate/history, Drafts/templates/approval, Development economy, and Ownership/lifecycle/ruleset migration. Authority / approval: roadmap section 13, `10_Characters_And_Progression`, `04_Odyssey_Rules_Engine`, Domain Model character/progression sections, and accepted ADR gap analysis.
- 2026-08-29 — Decision: no prerequisite technical spike is required. Authority / approval: the open questions are design/contract decisions over already-proven command, journal, migration, permission, and rules substrates; no new empirical measurement target is visible.
- 2026-08-29 — Decision: ability/resource/anatomy does not require a separate prerequisite ADR because the product specs already fix definition-vs-instance, computed effective values, resource recovery command flow, and anatomy snapshot semantics. Authority / approval: `10_Characters_And_Progression` sections 16-18 and `04_Odyssey_Rules_Engine` section 40.

### Approved task changes

- None.
