# ODY-S00-009 - Windows Development Build and Player Smoke

**Status:** Draft  
**Roadmap stage / slice:** SLICE-00  
**Owner:** Unassigned  
**Requested by:** Product owner  
**Branch:** `feat/ody-s00-009-windows-player-build-smoke`  
**Pull request:** Not opened  
**ExecPlan:** `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`  
**Created:** 2026-08-14  
**Last updated:** 2026-08-14 UTC

## 1. Goal

Create and run the repository-controlled Windows x64 Development-Debug Player build, package the build artifact with BuildIdentity and checksums, and prove a minimal startup/shutdown Player smoke against the built application.

This task remains Draft until the owner approves the TestCaseId/catalog mapping for the mandatory ADR-009 Windows build and Player smoke scenarios.

## 2. Why this task exists

- Problem or dependency being addressed: SLICE-00 has CI, BuildIdentity, Unity project validation, diagnostics, and local Unity test evidence, but it does not yet produce the real Windows Development-Debug application artifact.
- Value or risk reduction: A scripted build and smoke test prove that the repository state can create a runnable Windows Player with the expected identity, startup path, diagnostics, and shutdown behavior.
- Blocking or enabling relationship: Blocks ODY-S00-010 SLICE-00 Acceptance and M1 Closure.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.0.md`
- `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.5.md`
- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/TASK_TEMPLATE.md`
- `docs/tasks/SLICE-00_BACKLOG.md`
- `docs/tasks/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
- `docs/tasks/completed/ODY-S00-008_Fast_CI_and_Build_Identity.md`
- `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
- `Tests/Metadata/test-catalog.json`
- `docs/adr/ADR-005_Dependency_Composition_v1.0.md`
- `docs/adr/ADR-006_Test_Project_Structure_and_Dual_Unity_DotNet_Compilation_v1.0.md`
- `docs/adr/ADR-007_Versioning_and_Build_Identity_v1.0.md`
- `docs/adr/ADR-009_Unity_Project_and_Build_Baseline_v1.1.md`
- `docs/adr/ADR-010_Logging_Diagnostics_and_Redaction_v1.1.md`

### Requirement and test IDs

- Requirement IDs: `SLICE-00`, `M1`, `M5`, backlog ODY-S00-009, ADR-007 build identity/artifact requirements, ADR-009 build automation and Player smoke requirements.
- Existing test IDs: `TST-UNI-019`, `TST-UNI-021`, `TST-UNI-022`, `TST-UNI-023`, `TST-UNI-024`, and `TST-UNI-030` from ADR-009; ODY-S00-008-owned `TC-BUILDID-*`, `TC-CI-*`, and `TC-PROVENANCE-*` remain existing prerequisite evidence and are not reassigned.
- New test IDs to introduce: Blocked. `Tests/Metadata/test-catalog.json` contains no ODY-S00-009 catalog entries or owner-approved ODY-S00-009 `TC-*` range for Windows build and Player smoke evidence.

### Task-safe private context

- Approved summary / references: Use only repository-safe task, PR, CI, and build evidence. Do not copy private product documents, local private paths, secrets, personal data, or hidden campaign content into committed files, logs, artifacts, or PR text.

## 4. Verified current state

### Verified facts

- ODY-S00-008 is complete through owner-merged PR #12 and corrective PR #13.
- Final corrective feature HEAD `43225c9f753903c7678704891c22d5e98676fb3e` entered `main` as merge commit `1e6483aee42c53595bbc4758dff0a9a696345661`.
- Main push CI run `31799960601` passed all four required no-secret jobs.
- Development provenance checksum passed and `build-identity.json` SHA-256 was `91b1fe5662089adecb483e61431066afc266015dad3e0196e593c4c3683b9f30`.
- Local Unity evidence for ODY-S00-008 used Unity `6000.4.0f1`, compile passed, EditMode passed 33/33, PlayMode passed 2/2, and no Player build was run.
- `scripts/build-dev.ps1` and `scripts/build-release.ps1` exist as repository entry points, but this task has not audited or executed them.
- `Tests/Metadata/test-catalog.json` contains ODY-S00-008 `TC-BUILDID-*`, `TC-CI-*`, `TC-PROVENANCE-*`, and `TC-DIAG-*` entries, but no ODY-S00-009 Windows build/Player smoke entries.

### Assumptions

- A licensed local Unity `6000.4.0f1 (8cf496087c8f)` installation with Windows Build Support is available for the future implementation validation. This must be verified during implementation.

## 5. Scope

### In scope

- Repository-controlled Windows Standalone x64 Development-Debug build through `scripts/build-dev.ps1`.
- Build automation that applies and verifies the Development-Debug profile without relying on local active Unity UI state.
- Build output under `artifacts/` or a task-approved external output path, never inside `Assets/`, `Packages/`, or `ProjectSettings/`.
- BuildIdentity generation and inclusion beside the Player artifact as `build-identity.json`.
- SHA-256 checksums for the produced artifact files/package.
- Minimal automated Player smoke for the built artifact: process launch, `Bootstrap` startup to `Ready`, `AppShell` loaded, BuildIdentity readable, HDRP active, UI Toolkit root displayed, Input System Cancel/Submit path verified where automation exists, no fatal Player log errors, idempotent clean shutdown, and evidence retention.
- Redacted Player/build logs and smoke evidence according to ADR-010.
- Task-specific documentation and parent ExecPlan evidence updates after implementation.

### Out of scope

- Installer, updater, Steam/distribution channel, GitHub Release publication, Release Candidate, Release, tags, signing/notarization, SBOM/provenance attestation framework, telemetry, crash upload, package upgrades, new BuildIdentity schema, new dependency, SQLite, networking, database schema, gameplay, maps, tokens, dice, characters, combat, content tools, or M1 final acceptance report.

### Allowed paths

Future implementation may edit only after this task is Ready and implementation is explicitly approved.

```text
docs/tasks/active/ODY-S00-009_Windows_Development_Build_and_Player_Smoke.md
docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md
README.md
Tests/Metadata/test-catalog.json
```

### Paths requiring explicit approval before editing

```text
Assets/**
Packages/**
ProjectSettings/**
.github/workflows/**
DotNet/**
scripts/**
config/**
version.json
THIRD_PARTY_NOTICES.md
docs/adr/**
TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.5.md
ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.0.md
```

Production, test, workflow, Unity settings, package, script, and dependency changes are not approved while this task remains Draft.

## 6. Technical constraints

- Module ownership and dependency direction: Follow ADR-001 through existing module boundaries. Unity Client remains the presentation/composition root; Core source remains single-copy.
- Authoritative-state and transaction boundary: Not applicable; the Player smoke must not create gameplay, campaign persistence, network transport, or authoritative user data.
- Serialization / compatibility boundary: Follow ADR-003 v1.1 and ADR-007. BuildIdentity and sidecar JSON must use the approved explicit contracts; no new schema is introduced by this task contract.
- Time / RNG rule: Authoritative clocks/RNG are not part of this task. Build timestamps are BuildIdentity metadata only.
- Unity / thread / lifetime rule: Follow ADR-005 and ADR-009. Player startup/shutdown must use the existing Bootstrap/AppShell lifecycle and prove clean disposal.
- Dependency / licensing rule: No new dependency, GitHub Action, Unity package, executable, or downloadable tool is approved by this Draft contract.
- Security / privacy / redaction rule: Follow ADR-010. Build and Player logs/artifacts must exclude secrets, local usernames, machine names, persistent device IDs, absolute local paths, private documentation, and hidden campaign content.
- Performance or platform constraint: Target Windows 10/11 x64, Unity `6000.4.0f1`, Development-Debug Mono build profile unless an explicit owner decision changes the profile.
- Other: Unity Editor execution in GitHub Actions remains unapproved under Technical Development Baseline v0.5. Local Unity/build evidence is mandatory unless a future owner-approved runner/licensing amendment exists.

## 7. Expected behavior

### Scenario 1 - Scripted Development-Debug build

**Given** a clean task branch with ODY-S00-008 completed  
**When** the approved development build script is run with Unity `6000.4.0f1`  
**Then** it produces a Windows x64 Development-Debug Player artifact under the approved output location with `build-identity.json`, checksums, and build report evidence.

### Scenario 2 - Player startup smoke

**Given** the built Windows Player artifact  
**When** the approved smoke runner launches it  
**Then** the Player reaches `Ready`, loads `AppShell`, exposes BuildIdentity, records safe diagnostics, and exits cleanly without fatal Player log errors.

### Required invariants

- No Release, ReleaseCandidate, tag, installer, updater, distribution, telemetry, SQLite, networking, gameplay, or database work is introduced.
- Build output is generated evidence and does not enter tracked production source unless the implementation task explicitly records an allowed artifact policy.
- `PlayerSettings.bundleVersion` reflects `version.json`; it is not a second source of truth.
- Test assemblies and TestKit must not enter the Player.

## 8. Deliverables

- Production code: None while Draft; future Ready implementation may add only build/smoke integration needed by the approved contract.
- Tests: Blocked pending owner-approved ODY-S00-009 TestCaseId/catalog mapping.
- Scripts / CI: None while Draft; future implementation may update build/smoke scripts only after explicit approval.
- Configuration: None while Draft.
- Documentation: This task contract and synchronized SLICE-00 planning/status documents.
- Generated evidence or build artifacts: None in this docs-only transition.
- Migration / recovery material: Not applicable.

## 9. Acceptance criteria

1. The repository-controlled build entry point creates a Windows Standalone x64 Development-Debug Player artifact from Unity `6000.4.0f1`.
2. The build applies/verifies the intended profile and does not depend on local active Unity UI state.
3. The build output is outside `Assets/`, `Packages/`, and `ProjectSettings/`.
4. The artifact includes BuildIdentity sidecar JSON and SHA-256 checksums.
5. BuildIdentity sidecar data matches the generated runtime identity and exact source commit.
6. Player smoke launches the built artifact and proves `Bootstrap` reaches `Ready`.
7. Player smoke proves `AppShell` is loaded.
8. Player smoke proves BuildIdentity is visible/readable in the runtime path.
9. Player smoke proves the UI Toolkit root is displayed and the Input System Cancel/Submit path is validated where automation exists.
10. Player smoke proves no fatal Player log errors are present.
11. Player smoke proves idempotent clean shutdown without leaked background operations.
12. Build and Player logs/evidence are redacted and exclude secrets, local usernames, machine names, persistent device IDs, absolute paths, private documentation, and hidden campaign content.
13. Test assemblies/TestKit do not enter the Player artifact.
14. No Release, ReleaseCandidate, tag, installer/updater, distribution channel, telemetry, package upgrade, dependency, SQLite, networking, database, or gameplay scope is added.
15. Required validation commands have real evidence and do not claim unrun checks.

The task must not move to Ready until the blocker in section 18 is resolved.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TST-UNI-019` | Unity build / script | Development-Debug Mono build passes | Pass |
| `TST-UNI-021` | Windows Player smoke | Player startup reaches Ready | Pass |
| `TST-UNI-022` | Windows Player smoke / artifact check | BuildIdentity in UI/runtime and sidecar match | Pass |
| `TST-UNI-023` | Unity build / script | Build script does not depend on active local profile | Pass |
| `TST-UNI-024` | artifact policy check | Build output is not created inside `Assets/` or `Packages/` | Pass |
| `TST-UNI-030` | Windows Player smoke | Repeated startup/shutdown does not leave persistent objects/tasks | Pass |

These ADR-009 IDs are not yet represented in `Tests/Metadata/test-catalog.json` with ODY-S00-009 ownership. The implementation task must not proceed until the owner approves the exact catalog entries or replacement IDs that preserve these meanings.

### Required commands

Draft contract validation only:

```powershell
git diff --name-status
git diff --check
.\scripts\verify-format.ps1
.\scripts\verify-test-structure.ps1
.\scripts\verify-ci.ps1
.\scripts\verify-unity-project.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-repository.ps1
```

Future implementation validation must include the approved build and smoke commands after the TestCaseId/catalog blocker is resolved. Likely entry points are `.\scripts\build-dev.ps1` and the repository-approved Player smoke command, but this Draft contract does not authorize implementation or script changes.

### Manual validation

- Owner review of TestCaseId/catalog mapping before moving this task to Ready.
- Owner review of build artifact/evidence before merge during future implementation.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Unity `6000.4.0f1 (8cf496087c8f)`, Windows Standalone x64 Development-Debug.
- Scripting backend: Mono for Development-Debug; IL2CPP remains mandatory before SLICE-00 closure where required by ADR-009/M1, but this task's canonical artifact is Development-Debug unless owner expands scope.
- Network topology or database fixture: None.
- Other: No GitHub Actions Unity Editor execution under current Unity Personal constraint.

### Validation not required by this task

- Full `.NET` suite during this docs-only transition: not run because no production/test code changes are made.
- Unity batchmode during this docs-only transition: not run because no Unity files are changed.
- Release, ReleaseCandidate, tag, installer/updater, distribution, telemetry, SQLite, networking, database, or gameplay validation: out of scope.

## 11. Compatibility, migration, and rollback

- Compatibility impact: Future implementation creates a development artifact/evidence path only; no persisted campaign/user data contract is introduced.
- Version fields affected: BuildIdentity generated metadata only. `ApplicationVersion` stays `0.1.0` unless separately approved.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: Revert the ODY-S00-009 implementation PR and delete generated ignored artifacts.
- Data-loss risk and protection: No user data is touched.
- Recovery rehearsal required: Future implementation must document cleanup for partial build/smoke artifacts.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | - | - | - | - |

No new dependency, GitHub Action, Unity package, executable, external tool, or download is approved by this Draft contract.

## 13. Security, privacy, and hidden information

- Data classes handled: BuildIdentity, build logs, Player logs, artifact manifest/checksums, smoke evidence.
- Trust boundaries: Local build workstation, Git repository, generated artifacts, optional future CI evidence if owner-approved.
- Authorization / audience checks: Not applicable; no gameplay/session permissions runtime is introduced.
- Redaction requirements: Redact or exclude secrets, tokens, license data, environment dumps, local usernames, machine names, persistent device IDs, full local paths, private documents, hidden gameplay data, and campaign data.
- Log-safe fields: ADR-010 allowlisted structured fields and BuildIdentity safe fields only.
- Abuse / malformed input limits: Build/smoke scripts fail closed on missing files, malformed sidecar identity, checksum mismatch, fatal logs, timeout, wrong Unity version, or out-of-scope output paths.
- Security tests: Blocked pending TestCaseId/catalog mapping.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: ODY-S00-009 touches Unity build automation, generated artifacts, Player runtime smoke, diagnostics, BuildIdentity, and parent SLICE-00 milestone evidence.
- ExecPlan path: `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
- Expected pull request count: One implementation PR after Ready activation.
- Milestone or sequencing constraints: Do not begin implementation until ODY-S00-009 is moved from Draft to Ready after owner-approved TestCaseId/catalog mapping.

## 15. Documentation and versioning impact

- Documents that must change: ODY-S00-009 task contract, parent ExecPlan, parent task, backlog, README, Active Baseline current-task pointer, completion evidence after implementation.
- Documents that must not change: ADRs, Technical Development Baseline, baseline versions, product requirement documents, private documentation.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: None for this docs-only transition.
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

### Changed files / areas

- This Draft task contract was created during ODY-S00-008 closure / ODY-S00-009 creation.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `git diff --name-status` | Passed | Docs transition only plus owner-approved stale path pointer corrections in `Tests/Metadata/test-catalog.json` and `scripts/check-repository-policy.ps1`. |
| `git diff --check` | Passed | No whitespace errors. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\verify-test-structure.ps1` | Passed | `TC-ARCH-001 PASS`; controlled invalid Domain->Rules, package version mismatch, duplicate catalog ownership, and duplicate TestCaseId fixtures rejected. |
| `.\scripts\verify-ci.ps1` | Passed | `TC-CI-001` through `TC-CI-012` passed; controlled invalid workflow fixtures rejected. |
| `.\scripts\verify-unity-project.ps1` | Passed | Static Unity project/package/toolchain source validation passed; Unity Editor compile is not claimed. |
| `.\scripts\check-repository-policy.ps1` | Passed | `REPO-POLICY-001` through `REPO-POLICY-005` passed; nested CI/static Unity checks passed. |
| `.\scripts\verify-repository.ps1` | Passed | Repository policy, test structure, CI verifier, static Unity verifier, and SDK check passed; selected SDK `10.0.302`. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 through AC-15 | Blocked | ODY-S00-009 implementation is not started; TestCaseId/catalog mapping is unresolved. |

### Build and artifact evidence

- Build identity: None for ODY-S00-009.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: None.

### Known limitations

- No ODY-S00-009 Windows Development-Debug artifact exists yet.
- No ODY-S00-009 Player smoke exists yet.
- No ODY-S00-009 TestCaseId/catalog mapping exists yet.

### Follow-up tasks

- Owner must approve the ODY-S00-009 TestCaseId/catalog mapping before this task can move to Ready and implementation can start.

### Self-review summary

- Scope review: Draft contract stays within backlog ODY-S00-009 and excludes release/distribution/gameplay/database/networking scope.
- Architecture review: Uses existing ADR-005, ADR-006, ADR-007, ADR-009, and ADR-010 constraints without changing them.
- Test review: Does not create arbitrary new TestCase IDs; records missing catalog mapping as blocker.
- Security/privacy review: Redaction and artifact privacy constraints are explicit.
- Documentation/version review: No baseline, ADR, TDB, schema, protocol, ruleset, package, or application version is changed.

## 18. Blockers, decisions, and change control

### Blockers

- TestCaseId/catalog mapping blocker: `Tests/Metadata/test-catalog.json` does not contain ODY-S00-009 entries for the mandatory Windows build and Player smoke scenarios. Owner decision required: approve exact catalog entries or replacement `TC-*` IDs that preserve ADR-009 meanings for `TST-UNI-019`, `TST-UNI-021`, `TST-UNI-022`, `TST-UNI-023`, `TST-UNI-024`, and `TST-UNI-030`.

### Decisions made during execution

- 2026-08-14 - Create ODY-S00-009 contract from repository authorities because no existing draft task contract was present - Authority / approval: product owner decision.
- 2026-08-14 - Keep ODY-S00-009 Draft rather than Ready while TestCaseId/catalog mapping is missing - Authority / approval: product owner instruction in this task contract transition.

### Approved task changes

- None.
