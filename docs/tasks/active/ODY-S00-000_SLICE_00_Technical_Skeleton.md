# ODY-S00-000 — Deliver the SLICE-00 Technical Skeleton

**Status:** In Progress  
**Roadmap stage / slice:** SLICE-00  
**Owner:** Unassigned  
**Requested by:** Product owner  
**Branch:** Not created  
**Pull request:** Not opened  
**ExecPlan:** `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`  
**Created:** 2026-07-28  
**Last updated:** 2026-08-11 20:14 UTC

## 1. Goal

Deliver the complete `SLICE-00` technical skeleton: a private authoritative repository, exact Unity project, clean Core module/test graph, deterministic foundational contracts, safe diagnostics, serialization/AOT evidence, required CI, and a scripted Windows development build accepted as Milestone M1.

## 2. Why this task exists

- Problem or dependency being addressed: Product implementation cannot begin safely until repository, architecture, contracts, tests, build identity, diagnostics, and delivery gates exist.
- Value or risk reduction: Establishes one repeatable foundation and prevents future slices from embedding hidden architecture choices in feature work.
- Blocking or enabling relationship: Blocks `SLICE-01 — Local Campaign` and all later production slices.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.8.md`
- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/TASK_TEMPLATE.md`
- `docs/tasks/SLICE-00_BACKLOG.md`
- `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.3.md`, sections 4, 5, 8–16, 23–31
- `02_MVP_Scope_Odyssey_VTT_v0.10.md`, section 6, `SLICE-00`
- `17_Roadmap_Odyssey_VTT_v0.11.md`, sections 9.4–9.7
- `16_Test_Strategy_Odyssey_VTT_v0.1.md`, applicable M0/M1 gates
- ADR-001 through ADR-010

### Requirement and test IDs

- Requirement IDs: `SLICE-00`, Milestone `M1`, Technical Development Baseline decisions `TDB-DEC-001–TDB-DEC-027`
- Existing test IDs: Test IDs defined by ADR-002–ADR-010 for `SLICE-00`
- New test IDs to introduce: Task-specific IDs assigned in `ODY-S00-001–010`

### Task-safe private context

- Approved summary / references: Build only the technical skeleton. No gameplay feature or private product document content is permitted in the authoritative repository.

## 4. Verified current state

### Verified facts

- The current documentation baseline contains Technical Development Baseline v0.3, Active Documentation Baseline v1.8, accepted ADR-001–ADR-010, `AGENTS.md`, `PLANS.md`, and the task workflow.
- The approved platform is Windows 10/11 x64 with Unity `6000.4.0f1`, HDRP, UI Toolkit, and Input System.
- The approved code repository is Private and authoritative at `odyssey-services/Odyssey_VTT`, uses All Rights Reserved and Git LFS; protected `main`, owner-reviewed pull requests, and GitHub Actions remain required outcomes, not claims of completed verification.
- Repository Foundation is complete through merged PR #1.
- Unity Project Foundation is complete through owner-merged PR #4; merge commit `70e7d49e217d4aecb7a2e873d31787d26001f47f` records the Unity `6000.4.0f1 (8cf496087c8f)` HDRP baseline.
- Module and Test Skeleton is complete through owner-merged PR #6; merge commit `5e6f5e03ef022c5d7b0e6fef559c2383796d95be` records the Core module/test skeleton and dual .NET/Unity test foundation.
- ODY-S00-004 is complete through owner-merged PR #8. ODY-S00-005 is complete through owner-merged PR #9, merge commit `7aa5cc972c48d9af6509895bb6d9ed1e18899fdf`. ODY-S00-006 is complete through owner-merged PR #10, merged head `b695bc09f344a36b45adb30ed7c0186bf71902d9`, merge commit `abb139c3c93115c468d020db3eb423c47cfdd83b`, merged at `2026-08-11T18:52:47Z`. ODY-S00-007 is complete through owner-merged PR #11, merge commit `88382217a1053fbe5eb631024063800f45e69926`. ODY-S00-008 is the current In Progress child task on `feat/ody-s00-008-fast-ci-build-identity`; the CI licensing decision is recorded as no-secret GitHub Actions plus mandatory local Unity merge validation; no PR exists yet.

### Assumptions

- The owner-selected repository is `odyssey-services/Odyssey_VTT`; it remains Private until a separate owner decision.
- A licensed local Unity installation can execute the accepted Unity `6000.4.0f1` baseline on Windows for mandatory local Unity validation. Automated Unity CI is not approved under the current Unity Personal constraint.

## 5. Scope

### In scope

- All outcomes and evidence listed in `docs/tasks/SLICE-00_BACKLOG.md` tasks `ODY-S00-001–010`.
- Coordination, sequencing, task splitting, and M1 acceptance evidence.

### Out of scope

- All production feature work from `SLICE-01` onward.
- SQLite, networking, accounts, permissions runtime, maps, characters, combat, content authoring, chat, and audio.
- Release publication, installer, updater, remote telemetry, and distribution channel.

### Allowed paths

```text
Repository-wide, but only as allowed by the active child task contract.
```

### Paths requiring explicit approval before editing

```text
Accepted ADR files
Product requirement documents
Unity/package/toolchain versions
Application/schema/format/contract/protocol/ruleset versions
```

## 6. Technical constraints

- Module ownership and dependency direction: ADR-001 exact graph; architecture checks are blocking.
- Authoritative-state and transaction boundary: ADR-002; only a test operation and in-memory adapters are permitted.
- Serialization / compatibility boundary: ADR-003 v1.1; explicit DTOs, explicit canonical JSON codecs, IL2CPP proof.
- Time / RNG rule: ADR-008; no global time or randomness in authoritative logic.
- Unity / thread / lifetime rule: ADR-005 and ADR-009; one composition root, explicit lifetimes, bootstrap/AppShell baseline.
- Dependency / licensing rule: Technical Baseline and `AGENTS.md`; no unapproved third-party dependencies.
- Security / privacy / redaction rule: ADR-010; private documentation and hidden/secret data never enter public artifacts or logs.
- Performance or platform constraint: Windows x64; 1920×1080 baseline; Development-Debug Mono and required IL2CPP smoke.
- Other: One physical production source set shared by Unity and .NET according to ADR-006.

## 7. Expected behavior

### Scenario 1 — Clean developer checkout

**Given** a clean Windows workstation with the pinned prerequisites  
**When** the repository bootstrap and validation entry points are run  
**Then** the exact Unity project validates through the mandatory local Unity merge gate, Core compiles/tests without Unity, required no-secret checks pass, and a Windows development artifact is produced with BuildIdentity.

### Scenario 2 — Invalid architecture change

**Given** a pull request introduces a forbidden module reference or test/production boundary violation  
**When** fast validation runs  
**Then** the pull request check fails before merge.

### Required invariants

- Private product documentation is absent from authoritative Git history.
- No gameplay feature is implemented in `SLICE-00`.
- Production source is not duplicated between Unity and .NET.
- `main` cannot be merged by Codex and requires owner review.

## 8. Deliverables

- Production code: Minimal technical skeleton only.
- Tests: ADR-required unit, contract, architecture, EditMode, PlayMode, and Player smoke coverage.
- Scripts / CI: Repository entry points, required no-secret GitHub Actions checks, and mandatory local Unity merge evidence.
- Configuration: Unity, packages, versions, compatibility and build profiles.
- Documentation: Public-safe technical authorities, task contracts, ExecPlan, traceability and quality report.
- Generated evidence or build artifacts: Windows development build, BuildIdentity, checksums, test reports.
- Migration / recovery material: Not applicable; no persistent campaign schema is introduced.

## 9. Acceptance criteria

1. Every child task `ODY-S00-001–010` reaches `Done` or is replaced by an owner-approved task with equivalent blocking acceptance.
2. All slice exit criteria in the backlog are proven by recorded evidence.
3. The clean-checkout rehearsal succeeds without private files or unrecorded manual repository state.
4. M1 quality and traceability reports are owner-reviewed.
5. No unresolved blocking failure or hidden deferred scope remains.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| ADR-defined SLICE-00 suite | .NET / Unity / Player / scripts | Accepted architecture and baseline contracts | Pass |
| `S00-GATE-001` | repository rehearsal | Clean checkout can restore, test and build | Pass |
| `S00-GATE-002` | repository policy | Authoritative Git history contains no forbidden private or secret material | Pass |

### Required commands

Canonical commands are introduced by child tasks. The final task must run the complete set defined by `AGENTS.md` and the repository scripts actually present at that time.

### Manual validation

- Owner reviews repository visibility, protection, licensing, artifacts, diagnostic output, and M1 quality report.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64
- Unity editor or Player profile: Unity `6000.3.20f1`; Development-Debug; Windows IL2CPP smoke
- Scripting backend: Mono development plus IL2CPP smoke
- Network topology or database fixture: None
- Other: Clean checkout separate from the primary working directory

### Validation not required by this task

- Persistent SQLite recovery, networking, relay, account, permissions, gameplay, performance certification, installer, and Release publication; they belong to later tasks/slices.

## 11. Compatibility, migration, and rollback

- Compatibility impact: Establishes initial compatibility sources; no previous release exists.
- Version fields affected: Initial `ApplicationVersion` and baseline schema/contract/protocol fields created by child tasks.
- Migration or upcaster: No persistent user data migration.
- Forward / backward behavior: Only initial contract fixtures and explicit unsupported-version behavior.
- Rollback method: Revert the affected pull request or return to the last passing tagged/identified development artifact.
- Data-loss risk and protection: No user campaign data exists; protect repository history and generated evidence.
- Recovery rehearsal required: Clean checkout and failed-startup cleanup.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| Unity-approved baseline packages | Locked by ADR-009 | Engine/render/UI/input/test baseline | Unity package licenses | Product owner via ADR-009 |
| NUnit / official Unity Test Framework | Locked by ADR-006 | Test runners | Approved package licenses | Product owner via ADR-006 |

All additional dependencies require a child task update and explicit approval before use.

## 13. Security, privacy, and hidden information

- Data classes handled: Public technical documents, repository metadata, local diagnostics, synthetic test fixtures.
- Trust boundaries: Private authoritative Git repository, GitHub Actions, local build workstation, generated artifacts.
- Authorization / audience checks: Repository owner controls merge/protection; no product permissions runtime exists.
- Redaction requirements: No private product excerpts, secrets, personal paths, user names, tokens, RNG secrets, or hidden campaign data.
- Log-safe fields: Only ADR-010 allowlisted technical fields and synthetic identifiers.
- Abuse / malformed input limits: Serialization spike parser ceilings and repository secret scanning/policy checks.
- Security tests: ADR-010 redaction tests and repository-policy checks.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: Cross-module, multi-pull-request foundation with architecture, Unity, compatibility, CI, security, and build risk.
- ExecPlan path: `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
- Expected pull request count: Approximately 9 implementation pull requests plus final acceptance evidence; task splitting may change this with owner approval.
- Milestone or sequencing constraints: Follow `docs/tasks/SLICE-00_BACKLOG.md` dependency order.

## 15. Documentation and versioning impact

- Documents that must change: Child task contracts, parent ExecPlan, backlog status, traceability matrix, quality report, public technical docs when implementation materially requires it.
- Documents that must not change: Private product requirements and accepted ADRs unless a genuine new decision is approved.
- Application version change: Initial version source is created; no product release version bump beyond ADR-007 baseline without owner instruction.
- Schema / format / contract / protocol / ruleset version change: Initial scaffolds only as explicitly defined by ADR-007 and child tasks.
- Documentation version changes: Only material contract changes justify a version increase.
- Changelog or release-note requirement: Development evidence and task history; no end-user release notes.

## 16. Definition of Done

- [ ] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [ ] Required automated tests pass.
- [ ] Required manual checks are completed.
- [ ] Required commands and their real results are recorded.
- [ ] Architecture and dependency rules remain valid.
- [ ] Security, privacy, redaction, and audience rules are verified.
- [ ] Compatibility, rollback, and versioning obligations are complete.
- [ ] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [ ] Documentation is updated only where materially required.
- [ ] Every child task and pull request has honest completion evidence.
- [ ] Product owner completes M1 review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- Planning package only at task creation; implementation evidence is recorded during execution.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| Documentation package integrity | Passed | Bundle manifest and Markdown checks performed when this contract was created |
| Repository build/test commands | Not run | No Unity/.NET implementation exists yet; repository foundation policy checks are recorded in ODY-S00-001. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1–AC-5 | Deferred | Must be proven by child tasks and final M1 closure |

### Build and artifact evidence

- Build identity: Not created
- Artifact path / name: None
- Checksums: Documentation bundle only
- Test or quality report: Not created

### Known limitations

- Automated Unity CI is not approved under the current Unity Personal constraint; exact branch protection/ruleset settings remain unverified and must be recorded honestly.

### Follow-up tasks

- `ODY-S00-008` through `ODY-S00-010`; ODY-S00-001 through ODY-S00-007 are completed, and ODY-S00-008 is In Progress for Fast CI and Build Identity implementation after owner approval.
- ADR-010 diagnostic session/bundle scenarios `TC-DIAG-033`, `TC-DIAG-034`, `TC-DIAG-035`, `TC-DIAG-036`, `TC-DIAG-037`, `TC-DIAG-038`, `TC-DIAG-039`, and `TC-DIAG-040` are future ODY-S00-008 scope after BuildIdentity exists; ODY-S00-010 remains final reconciliation, not an implementation task.

### Self-review summary

- Scope review: Technical skeleton only.
- Architecture review: Uses ADR-001–ADR-010 without introducing new architectural decisions.
- Test review: Validation is planned; no repository tests are claimed.
- Security/privacy review: Public/private boundary explicitly preserved.
- Documentation/version review: Parent task, backlog and ExecPlan are new operational artifacts only.

## 18. Blockers, decisions, and change control

### Blockers

- ODY-S00-001 through ODY-S00-007 are complete. ODY-S00-008 is In Progress on `feat/ody-s00-008-fast-ci-build-identity`; the Personal-license CI decision is recorded; no PR exists yet.
- ODY-S00-008 implementation evidence is recorded in its active task contract: no-secret Fast CI, BuildIdentity generation/parity, static Unity validation, and local Unity compile/EditMode/PlayMode gates passed. ODY-S00-009 remains blocked and unstarted until owner review/merge of ODY-S00-008.
- ODY-S00-008 owns `TC-DIAG-033`, `TC-DIAG-034`, `TC-DIAG-035`, `TC-DIAG-036`, `TC-DIAG-037`, `TC-DIAG-038`, `TC-DIAG-039`, and `TC-DIAG-040` after BuildIdentity exists. ODY-S00-009 remains blocked by ODY-S00-008.

### Decisions made during execution

- 2026-07-28 — Treat Technical Baseline PR labels as delivery groups that may be split into review-safe tasks without changing their required outcomes — Authority / approval: Product owner instruction to prepare the execution package, `PLANS.md` scope-control rules.

### Approved task changes

- None.
