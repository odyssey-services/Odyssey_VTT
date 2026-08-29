# ODY-S04-001 — ADR Character Aggregate, Section Revisions, and History Projection

**Status:** In Review  
**Roadmap stage / slice:** SLICE-04  
**Owner:** Codex (agent)  
**Requested by:** Product owner  
**Branch:** `feat/ody-s04-001-adr-character-aggregate`  
**Pull request:** [#80](https://github.com/odyssey-services/Odyssey_VTT/pull/80)  
**ExecPlan:** `docs/plans/active/ODY-S04-001_ADR_Character_Aggregate_Section_Revisions_History.md`  
**Created:** 2026-08-30  
**Last updated:** 2026-08-29 22:53 UTC

## 1. Goal

Accept `ADR-022 — Character Aggregate, Section Revisions, and History Projection`, resolving the Character aggregate boundary, authoritative section locks, minimum Character event snapshots, and `CharacterHistoryProjection` source-of-truth contract.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-04_BACKLOG.md` identifies Character aggregate/revision/history as the first prerequisite ADR before Character implementation can be decomposed.
- Value or risk reduction: prevents implementation tasks from inventing incompatible Character aggregate splits, broad locks, event snapshot payloads, or independent history storage.
- Blocking or enabling relationship: enables `ODY-S04-002`, `ODY-S04-003`, and `ODY-S04-004`; `SLICE-04` implementation remains blocked until all four prerequisite ADRs are accepted.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`.
- `AGENTS.md`, `PLANS.md`, `docs/tasks/TASK_TEMPLATE.md`.
- `docs/tasks/SLICE-04_BACKLOG.md` §3.1.
- `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` §13.3, §13.8 steps 1/5/10, and §13.9.
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` Character aggregate, editing/concurrency, history, persistence, networking/reconnect, and readiness sections.
- `Documentation/04_Odyssey_Rules_Engine_Odyssey_VTT_v0.6.md` §40.
- `Documentation/03_Domain_Model_Odyssey_VTT_v0.25.md` Character/Progression aggregate, command/event, transaction, projection, and recommended aggregate sections.
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`.
- `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md`.
- `docs/adr/ADR-003_Serialization_Strategy_v1.1.md`.
- `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md`.
- `docs/adr/ADR-013_Migration_Runner_v1.0.md`.
- `docs/adr/ADR-020_Board_Geometry_And_Movement_Determinism_v1.0.md` and `docs/adr/ADR-021_Extended_Audience_And_Selected_Participant_Visibility_v1.0.md` as ADR format/depth precedents.

### Requirement and test IDs

- Requirement IDs: `ODY-S04-001`, `ADR-022`, `SLICE-04` prerequisite backlog item 1.
- Existing test IDs: None.
- New test IDs to introduce: None.

### Task-safe private context

- Approved summary / references: non-tracked product documentation was read as local authority and summarized by section/topic only. No private prose, local private path outside the repository, secret, personal data, or hidden campaign content is copied into this task, the plan, or ADR.

## 4. Verified current state

### Verified facts

- `git fetch origin main`, `git checkout main`, and `git merge --ff-only origin/main` advanced local `main` to `e237ab9`, the merge commit for PR #79.
- `git log --oneline -10` confirmed PR #79 is in `main` and contains `SLICE-04_BACKLOG.md` plus `ODY-S04-000`.
- `docs/tasks/SLICE-04_BACKLOG.md` lists `ODY-S04-001` as the first prerequisite task, with future `ADR-022`.
- No `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md` existed before this task.
- `PLANS.md` §1.2 requires an ExecPlan because this ADR changes future public domain/persistence/reconnect contracts and affects authoritative state semantics.

### Assumptions

- None.

## 5. Scope

### In scope

- Create `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md`.
- Create this task contract.
- Create an ExecPlan for the ADR task.
- Update the `ODY-S04-001` row in `docs/tasks/SLICE-04_BACKLOG.md` to `Done` and point to `ADR-022`.
- Run documentation-only validation.

### Out of scope

- Production code, tests, schema, DTOs, Unity assets, scenes, packages, scripts, migrations, or generated artifacts.
- Drafts/templates/approval workflow (`ODY-S04-002`/future `ADR-023`).
- Development economy/points/purchases/critical evidence/respec (`ODY-S04-003`/future `ADR-024`).
- Ownership/lifecycle operations/Dead/restore/physical delete/Ruleset migration (`ODY-S04-004`/future `ADR-025`).
- Ability/resource/anatomy mechanics beyond section revision/lock membership.
- Concrete content catalogs.

### Allowed paths

```text
docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md
docs/plans/active/ODY-S04-001_ADR_Character_Aggregate_Section_Revisions_History.md
docs/tasks/active/ODY-S04-001_ADR_Character_Aggregate_Section_Revisions_History.md
docs/tasks/SLICE-04_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
Assets/**
Packages/**
DotNet/**
ProjectSettings/**
Documentation/**
docs/adr/ADR-001* through docs/adr/ADR-021*
```

## 6. Technical constraints

- Module ownership and dependency direction: future implementation must keep Character aggregate semantics in Domain, command/revision/lock orchestration in Application, physical storage/projection tables in Persistence, delivery in Networking, and display/local forms in Unity Client per `ADR-001`.
- Authoritative-state and transaction boundary: ADR must reuse `ADR-002`/`ADR-012` command idempotency, event batches, current-state projections, and append-only journal; no parallel history mechanism.
- Serialization / compatibility boundary: event snapshots and future DTOs remain explicit/versioned under `ADR-003`; no direct Domain aggregate serialization.
- Time / RNG rule: durable locks that use expiry must use injected host clock per `ADR-008`; no clock code is implemented here.
- Unity / thread / lifetime rule: Not applicable.
- Dependency / licensing rule: no new dependency, tool, action, or package.
- Security / privacy / redaction rule: Character history projection is audience/permission filtered; tracked docs contain sanitized summaries only.
- Performance or platform constraint: Not applicable.
- Other: do not solve future ADR-023/024/025 scopes inside ADR-022.

## 7. Expected behavior

### Scenario 1 — Character aggregate boundary is reviewable

**Given** `SLICE-04_BACKLOG.md` identifies the Character aggregate boundary as a prerequisite gap  
**When** `ADR-022` is reviewed  
**Then** it states one Character aggregate root with section revisions, not multiple independent aggregate roots.

### Scenario 2 — History is not a second source of truth

**Given** future implementation needs Character history and reconnect proof  
**When** `ADR-022` is reviewed  
**Then** it requires Character history to rebuild from DomainEvents/current projection inputs and forbids independent history mutation.

### Required invariants

- All four task-specified questions are answered explicitly.
- `ADR-022` reuses existing command, serialization, journal, migration, and reconnect ADRs.
- No code/schema/test implementation is introduced.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `ADR-022`, this task contract, ExecPlan, and `SLICE-04_BACKLOG.md` row update.
- Generated evidence or build artifacts: None.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md` exists and is `Accepted`.
2. ADR answers the four task-required questions separately: aggregate boundary, section locks, minimum Character event snapshots, and `CharacterHistoryProjection` projection/reconnect contract.
3. ADR includes considered alternatives for single aggregate vs multiple records, optimistic revisions vs locks, and read-time vs persisted projection.
4. ADR explicitly excludes Draft/template, DevelopmentPool/progression, ownership/lifecycle/ruleset migration, ability/resource/anatomy mechanics, code, tests, and persistence implementation.
5. This task contract exists with all 18 sections.
6. ExecPlan exists because `PLANS.md` requires it for future public contract/authoritative state ADR work.
7. `docs/tasks/SLICE-04_BACKLOG.md` marks `ODY-S04-001` as `Done` and points to `ADR-022`.
8. Diff contains only documentation files under `docs/adr`, `docs/plans`, and `docs/tasks`.
9. `.\scripts\verify-format.ps1` and `.\scripts\check-repository-policy.ps1` pass.

## 10. Tests and validation

### Required automated tests

None. This is a documentation-only ADR task; replacement evidence is repository formatting and policy validation plus PR review.

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- Product owner reviews `ADR-022` before dependent tasks `ODY-S04-002` through `ODY-S04-004` proceed.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable.
- Scripting backend: Not applicable.
- Network topology or database fixture: None.
- Other: PowerShell validation only.

### Validation not required by this task

- `dotnet build`, `dotnet test`, `test-unity`, `build-dev`, migration rehearsal, and player smoke are not required because no code, test, Unity, schema, package, or CI file changes.

## 11. Compatibility, migration, and rollback

- Compatibility impact: future architectural contract only; no persisted state changes in this PR.
- Version fields affected: `ADR-022` document version introduced as `1.0`; no application/schema/contract/protocol/ruleset version changes.
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

- Data classes handled: task-safe summaries of private product documentation, public repository ADR/task paths, and future Character history/redaction architecture.
- Trust boundaries: private product docs are read-only and not copied verbatim into tracked files.
- Authorization / audience checks: no implementation; ADR requires future history projections to be authorized/redacted, reusing existing permission/reconnect rules.
- Redaction requirements: no private excerpts, secrets, credentials, personal data, or hidden campaign content in commits/PR text.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: None.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: `PLANS.md` §1.2 requires an ExecPlan because the ADR changes future public domain/persistence/reconnect contracts and affects authoritative state/history semantics. The `SLICE-04_BACKLOG.md` row also expected ExecPlan-level tracking for this child ADR.
- ExecPlan path: `docs/plans/active/ODY-S04-001_ADR_Character_Aggregate_Section_Revisions_History.md`
- Expected pull request count: 1
- Milestone or sequencing constraints: `ODY-S04-002`, `ODY-S04-003`, and `ODY-S04-004` depend on this ADR. `SLICE-04` implementation backlog still waits for all four prerequisite ADRs.

## 15. Documentation and versioning impact

- Documents that must change: `ADR-022`, this task contract, ExecPlan, `SLICE-04_BACKLOG.md`.
- Documents that must not change: existing ADR-001 through ADR-021, private `Documentation/` sources, production code, tests, scripts, Unity assets.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: none implemented; future Character event/DTO contract guidance is documented in ADR only.
- Documentation version changes: `ADR-022` introduced as v1.0.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass or are explicitly not applicable.
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

- `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md` — new accepted Character aggregate/revision/history ADR.
- `docs/plans/active/ODY-S04-001_ADR_Character_Aggregate_Section_Revisions_History.md` — ExecPlan for this ADR task.
- `docs/tasks/active/ODY-S04-001_ADR_Character_Aggregate_Section_Revisions_History.md` — this task contract.
- `docs/tasks/SLICE-04_BACKLOG.md` — marks `ODY-S04-001` as complete.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | `Repository policy check passed`; includes required repository structure, forbidden tracked patterns, LFS policy, ErrorCode registry, workflow policy, and static Unity project/package/toolchain checks. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `ADR-022` file exists and status is `Accepted`. |
| AC-2 | Passed | ADR sections 4-9 answer all four required questions. |
| AC-3 | Passed | ADR section 14 records required alternatives. |
| AC-4 | Passed | ADR section 10 excludes future ADR scopes and implementation work. |
| AC-5 | Passed | This contract contains all 18 numbered sections. |
| AC-6 | Passed | ExecPlan exists under `docs/plans/active`. |
| AC-7 | Passed | `SLICE-04_BACKLOG.md` row updated for `ODY-S04-001`. |
| AC-8 | Passed | Diff scope is docs-only. |
| AC-9 | Passed | Required validation commands passed locally. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: Not applicable.

### Known limitations

- No production implementation is included. The next three prerequisite ADR tasks remain open.

### Follow-up tasks

- `ODY-S04-002` — `ADR-023` Character Drafts, Templates, and Approval Workflow.
- `ODY-S04-003` — `ADR-024` Development Economy and Progression Transactions.
- `ODY-S04-004` — `ADR-025` Character Ownership, Lifecycle, and Ruleset Migration Operations.

### Self-review summary

- Scope review: limited to allowed documentation files.
- Architecture review: ADR reuses `ADR-002`/`003`/`012`/`013`/`017`; no replacement substrate introduced.
- Test review: no tests changed; required docs/policy validation passed.
- Security/privacy review: no private excerpts copied; future history redaction remains required.
- Documentation/version review: `ADR-022` v1.0 introduced; no app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task.
- `SLICE-04` implementation remains blocked until `ADR-023`, `ADR-024`, and `ADR-025` are also accepted.

### Decisions made during execution

- 2026-08-29 — Decision: create an ExecPlan despite the expected diff list naming only ADR/task/backlog files. Authority / approval: `PLANS.md` §1.2 and `SLICE-04_BACKLOG.md` planning-mode expectation.
- 2026-08-29 — Decision: Character is one aggregate root with section revisions rather than several independent roots. Authority / approval: `ADR-022` §4, roadmap Character foundation, Domain Model Character aggregate.
- 2026-08-29 — Decision: use narrow section locks only where a command invariant requires them; `CommandId`/`AppliedCommands` remain the duplicate-command mechanism. Authority / approval: `ADR-022` §5-6, `ADR-002`, `ADR-012`.
- 2026-08-29 — Decision: Character events store minimal historical snapshots, not full Character sheets. Authority / approval: `ADR-022` §7, Character/Progression historical snapshot requirements, `ADR-003`, `ADR-012`.
- 2026-08-29 — Decision: `CharacterHistoryProjection` is rebuildable from journal/current projection inputs and cannot be independently mutated. Authority / approval: `ADR-022` §8-9 and `ADR-012`.
- 2026-08-29 — Self-correction: PM found before merge that `ADR-022` §17 incorrectly claimed dated product-owner sign-off while PR #80 was still Draft and awaiting owner review. The premature sign-off sentence was removed; actual approval remains represented only by owner review/merge, not by a prewritten ADR line. Authority / approval: PM clarification for this correction.

### Approved task changes

- None.
