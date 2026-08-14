# ODY-S00-009 - Windows Development Build and Player Smoke

**Status:** In Progress  
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

Owner-approved TestCaseId/catalog mapping for the mandatory ADR-009 Windows build and Player smoke scenarios is registered as `TC-PLAYER-001` through `TC-PLAYER-010`.

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
- New test IDs introduced: `TC-PLAYER-001` through `TC-PLAYER-010` for ODY-S00-009 Windows build and Player smoke evidence. Catalog entries point to the implemented owning scripts.

### Task-safe private context

- Approved summary / references: Use only repository-safe task, PR, CI, and build evidence. Do not copy private product documents, local private paths, secrets, personal data, or hidden campaign content into committed files, logs, artifacts, or PR text.

## 4. Verified current state

### Verified facts

- ODY-S00-008 is complete through owner-merged PR #12 and corrective PR #13.
- Final corrective feature HEAD `43225c9f753903c7678704891c22d5e98676fb3e` entered `main` as merge commit `1e6483aee42c53595bbc4758dff0a9a696345661`.
- Main push CI run `31799960601` passed all four required no-secret jobs.
- Development provenance checksum passed and `build-identity.json` SHA-256 was `91b1fe5662089adecb483e61431066afc266015dad3e0196e593c4c3683b9f30`.
- Local Unity evidence for ODY-S00-008 used Unity `6000.4.0f1`, compile passed, EditMode passed 33/33, PlayMode passed 2/2, and no Player build was run.
- `scripts/build-dev.ps1` exists as the ODY-S00-009 Windows Development-Debug build entry point.
- `scripts/test-player-smoke.ps1` exists as the ODY-S00-009 built Player smoke entry point.
- `scripts/build-release.ps1` does not exist and is not in ODY-S00-009 scope.
- `Tests/Metadata/test-catalog.json` contains owner-approved ODY-S00-009 `TC-PLAYER-001` through `TC-PLAYER-010` entries that preserve the mandatory ADR-009 Windows build and Player smoke meanings.
- Independent pre-PR implementation audit of HEAD `8b792c245fe5ca1d21555f32e3ef4480d444953b` returned `NO-GO` with five P1 blockers: untracked source drift was not rejected by `scripts/build-dev.ps1`; Player smoke activation lacked a Development Player/debug-build guard; smoke evidence replacement used delete-before-move; the Unity Editor build entry point trusted `-odysseyBuildOutput` without independently proving canonical containment; diagnostic property `build_id` used generic `BoundedText`.

### Assumptions

- A licensed local Unity `6000.4.0f1 (8cf496087c8f)` installation with Windows Build Support is available for the future implementation validation. This must be verified during implementation.

## 5. Scope

### In scope

- Repository-controlled Windows Standalone x64 Development-Debug build through `scripts/build-dev.ps1`.
- Build automation that applies and verifies the Development-Debug profile without relying on local active Unity UI state.
- Build output under the canonical layout `artifacts/builds/<BuildId>/Windows-x64/`, never inside `Assets/`, `Packages/`, or `ProjectSettings/`. Alternative repository or external output paths are not accepted for canonical ODY-S00-009 validation.
- BuildIdentity generation and inclusion beside the Player artifact as `build-identity.json`.
- SHA-256 checksums for the produced artifact files/package.
- Minimal automated Player smoke for the built artifact: process launch, `Bootstrap` startup to `Ready`, `AppShell` loaded, BuildIdentity readable, HDRP active, UI Toolkit root displayed, Input System Submit path processed, Input System Cancel path processed, no fatal Player log errors, idempotent clean shutdown, and evidence retention.
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
scripts/verify-test-structure.ps1
```

### Paths requiring explicit approval before editing

```text
Assets/**
Packages/**
ProjectSettings/**
.github/workflows/**
DotNet/**
scripts/build-dev.ps1
scripts/test-player-smoke.ps1
config/**
version.json
THIRD_PARTY_NOTICES.md
docs/adr/**
TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.5.md
ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.0.md
```

Production, test, workflow, Unity settings, package, and dependency changes are not approved by this contract activation. Script changes are planned only for `scripts/build-dev.ps1`, `scripts/test-player-smoke.ps1`, and necessary Unity build/smoke entry points during the separately approved ODY-S00-009 implementation.

## 6. Technical constraints

- Module ownership and dependency direction: Follow ADR-001 through existing module boundaries. Unity Client remains the presentation/composition root; Core source remains single-copy.
- Authoritative-state and transaction boundary: Not applicable; the Player smoke must not create gameplay, campaign persistence, network transport, or authoritative user data.
- Serialization / compatibility boundary: Follow ADR-003 v1.1 and ADR-007. BuildIdentity and sidecar JSON must use the approved explicit contracts; no new schema is introduced by this task contract.
- Time / RNG rule: Authoritative clocks/RNG are not part of this task. Build timestamps are BuildIdentity metadata only.
- Unity / thread / lifetime rule: Follow ADR-005 and ADR-009. Player startup/shutdown must use the existing Bootstrap/AppShell lifecycle and prove clean disposal.
- Dependency / licensing rule: No new dependency, GitHub Action, Unity package, executable, or downloadable tool is approved by this contract.
- Security / privacy / redaction rule: Follow ADR-010. Build and Player logs/artifacts must exclude secrets, local usernames, machine names, persistent device IDs, absolute local paths, private documentation, and hidden campaign content.
- Performance or platform constraint: Target Windows 10/11 x64, Unity `6000.4.0f1`, Development-Debug Mono build profile unless an explicit owner decision changes the profile.
- Other: Unity Editor execution in GitHub Actions remains unapproved under Technical Development Baseline v0.5. Local Unity/build evidence is mandatory unless a future owner-approved runner/licensing amendment exists.

## 7. Expected behavior

### Scenario 1 - Scripted Development-Debug build

**Given** a clean task branch with ODY-S00-008 completed  
**When** the approved development build script is run with Unity `6000.4.0f1`  
**Then** it produces a Windows x64 Development-Debug Player artifact under `artifacts/builds/<BuildId>/Windows-x64/` with `build-identity.json`, checksums, and build report evidence.

### Scenario 2 - Player startup smoke

**Given** the built Windows Player artifact  
**When** the approved smoke runner launches it  
**Then** the Player reaches `Ready`, loads `AppShell`, exposes BuildIdentity, records safe diagnostics, and exits cleanly without fatal Player log errors.

### Smoke timeout and process contract

- Bootstrap Ready timeout: 120 seconds from Player process launch.
- Clean shutdown timeout: 15 seconds after the smoke runner requests shutdown.
- Hard timeout for one smoke run: 150 seconds.
- Successful Player exit code: exactly `0`.
- Elapsed-time measurement must use injected or monotonic timing where applicable.
- On timeout, the smoke runner must preserve safe available evidence, terminate the full Player process tree, verify no child processes remain, return non-zero, and record the smoke as failed regardless of the forced process exit code.
- Any crash, non-zero exit, timeout, or orphan process is a failure.

### Required invariants

- No Release, ReleaseCandidate, tag, installer, updater, distribution, telemetry, SQLite, networking, gameplay, or database work is introduced.
- Build output is generated evidence and does not enter tracked production source unless the implementation task explicitly records an allowed artifact policy.
- `PlayerSettings.bundleVersion` reflects `version.json`; it is not a second source of truth.
- Test assemblies and TestKit must not enter the Player.

## 8. Deliverables

- Production code: Minimal Unity Client build/smoke integration needed by the approved contract.
- Tests: Owner-approved `TC-PLAYER-001` through `TC-PLAYER-010` are mapped to the implemented owning scripts.
- Scripts / CI: `scripts/build-dev.ps1`, `scripts/test-player-smoke.ps1`, and necessary Unity build/smoke entry points are implemented. `scripts/build-release.ps1` is not planned.
- Configuration: None in this activation.
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
9. Player smoke proves the UI Toolkit root is displayed, the Input System Submit path is processed, and the Input System Cancel path is processed inside the built Windows Development Player. Manual or deferred evidence is not accepted for this criterion.
10. Player smoke proves no fatal Player log errors are present.
11. Player smoke proves idempotent clean shutdown without leaked background operations.
12. Build and Player logs/evidence are redacted and exclude secrets, local usernames, machine names, persistent device IDs, absolute paths, private documentation, and hidden campaign content.
13. Test assemblies/TestKit do not enter the Player artifact.
14. No Release, ReleaseCandidate, tag, installer/updater, distribution channel, telemetry, package upgrade, dependency, SQLite, networking, database, or gameplay scope is added.
15. Required validation commands have real evidence and do not claim unrun checks.

The task is In Progress under the owner-approved implementation ТЗ.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-PLAYER-001` | Unity build / script | Windows Standalone x64 Development-Debug Mono Player build passes under Unity `6000.4.0f1` | Pass |
| `TC-PLAYER-002` | Unity build / script | Build script applies and verifies the Development-Debug profile independently of the active Unity Editor profile | Pass |
| `TC-PLAYER-003` | artifact policy check | Artifact uses `artifacts/builds/<BuildId>/Windows-x64/` and never writes build output under `Assets/`, `Packages/`, or `ProjectSettings/` | Pass |
| `TC-PLAYER-004` | Windows Player smoke / artifact check | Final Player artifact includes BuildIdentity JSON/checksums and runtime/sidecar identity parity for the exact source commit | Pass |
| `TC-PLAYER-005` | Windows Player smoke | Built Player launches and reaches `Bootstrap Ready` within 120 seconds | Pass |
| `TC-PLAYER-006` | Windows Player smoke | Player proves `AppShell` loaded, HDRP active, UI Toolkit root displayed, and Input System Submit and Cancel paths processed | Pass |
| `TC-PLAYER-007` | Windows Player smoke | Player requests clean shutdown, exits with code 0 within 15 seconds, repeated runs leave no persistent process tree, and total smoke duration is bounded by 150 seconds | Pass |
| `TC-PLAYER-008` | Windows Player smoke / log safety | Player/build logs and smoke evidence contain no secrets, usernames, machine names, persistent device IDs, absolute local paths, private documentation, or hidden content | Pass |
| `TC-PLAYER-009` | artifact policy check | Player artifact excludes test assemblies, TestKit, private documentation, and editor/test-only outputs | Pass |
| `TC-PLAYER-010` | artifact policy check | Development artifact creates no Release/RC/tag/signing/installer/updater/distribution/SBOM/telemetry outputs | Pass |

Catalog paths point to `scripts/build-dev.ps1` for `TC-PLAYER-001` through `TC-PLAYER-003` and `TC-PLAYER-010`, and `scripts/test-player-smoke.ps1` for `TC-PLAYER-004` through `TC-PLAYER-009`.

### Required commands

Contract activation validation:

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

Implementation validation includes the approved build and smoke commands. Required entry points are `.\scripts\build-dev.ps1` and `.\scripts\test-player-smoke.ps1`.

### Manual validation

- Owner review of TestCaseId/catalog mapping before moving this task to Ready: completed by explicit owner approval on 2026-08-14.
- Owner review of build artifact/evidence before merge during future implementation.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Unity `6000.4.0f1 (8cf496087c8f)`, Windows Standalone x64 Development-Debug.
- Scripting backend: Mono for Development-Debug; IL2CPP remains mandatory before SLICE-00 closure where required by ADR-009/M1, but this task's canonical artifact is Development-Debug unless owner expands scope.
- Network topology or database fixture: None.
- Other: No GitHub Actions Unity Editor execution under current Unity Personal constraint.

### Validation not required by this task

- Full `.NET` suite before implementation commit: required.
- Unity batchmode before implementation commit: required.
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

No new dependency, GitHub Action, Unity package, executable, external tool, or download is approved by this contract.

## 13. Security, privacy, and hidden information

- Data classes handled: BuildIdentity, build logs, Player logs, artifact manifest/checksums, smoke evidence.
- Trust boundaries: Local build workstation, Git repository, generated artifacts, optional future CI evidence if owner-approved.
- Authorization / audience checks: Not applicable; no gameplay/session permissions runtime is introduced.
- Redaction requirements: Redact or exclude secrets, tokens, license data, environment dumps, local usernames, machine names, persistent device IDs, full local paths, private documents, hidden gameplay data, and campaign data.
- Log-safe fields: ADR-010 allowlisted structured fields and BuildIdentity safe fields only.
- Abuse / malformed input limits: Build/smoke scripts fail closed on missing files, malformed sidecar identity, checksum mismatch, fatal logs, timeout, wrong Unity version, or out-of-scope output paths.
- Security tests: Planned under `TC-PLAYER-008`, `TC-PLAYER-009`, and `TC-PLAYER-010`.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: ODY-S00-009 touches Unity build automation, generated artifacts, Player runtime smoke, diagnostics, BuildIdentity, and parent SLICE-00 milestone evidence.
- ExecPlan path: `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
- Expected pull request count: One implementation PR after Ready activation.
- Milestone or sequencing constraints: ODY-S00-009 is In Progress after owner-approved TestCaseId/catalog mapping and implementation ТЗ approval. Do not move to In Review or open a PR until validation, clean-HEAD build/smoke evidence, commit, and push complete.

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

- This task contract was created during ODY-S00-008 closure / ODY-S00-009 creation, moved to Ready after owner-approved `TC-PLAYER-001` through `TC-PLAYER-010` mapping, and moved to In Progress for implementation.
- Audit blocker correction scope is limited to build/smoke scripts and Unity Client build/smoke/diagnostics integration, Application diagnostics/BuildIdentity contract validation, focused .NET/Unity tests, `scripts/verify-test-structure.ps1`, this task evidence, and the parent ExecPlan. Pull request remains not opened.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\restore.ps1` | Passed | Initial sandbox run was environment-blocked by denied access to `Microsoft SDKs`; escalated rerun restored all projects successfully. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\verify-test-structure.ps1` | Passed | `TC-ARCH-001 PASS`; controlled invalid Domain->Rules, package version mismatch, duplicate catalog ownership, and duplicate TestCaseId fixtures rejected; ODY-S00-009 `TC-PLAYER` script guards passed. |
| `.\scripts\test-fast.ps1` | Passed | Initial sandbox run was environment-blocked by denied access to `Microsoft SDKs`; escalated rerun passed .NET tests: 86 total, 86 passed, 0 failed, 0 skipped. |
| `dotnet build .\DotNet\Odyssey.Core.sln --no-restore` | Passed | Escalated rerun passed: 0 warnings, 0 errors. |
| `dotnet test .\DotNet\Odyssey.Core.sln --no-build --no-restore` | Passed | Escalated rerun passed before correction: 86 total, 86 passed, 0 failed, 0 skipped. Audit correction focused rerun after strict BuildId tests: 88 total, 88 passed, 0 failed, 0 skipped. |
| `.\scripts\verify-ci.ps1` | Passed | `TC-CI-001` through `TC-CI-012` passed; controlled invalid workflow fixtures rejected. |
| `.\scripts\verify-unity-project.ps1` | Passed | Static Unity project/package/toolchain source validation passed; Unity Editor compile is not claimed. |
| `.\scripts\check-repository-policy.ps1` | Passed | `REPO-POLICY-001` through `REPO-POLICY-005` passed; nested CI/static Unity checks passed. |
| `.\scripts\verify-repository.ps1` | Passed | Repository policy, test structure, CI verifier, static Unity verifier, and SDK check passed; selected SDK `10.0.302`. |
| `.\scripts\test-unity.ps1` | Passed | Escalated rerun before correction passed with Unity `6000.4.0f1`: compile exit code 0, EditMode 33/33, PlayMode 2/2. Audit correction rerun passed: compile exit code 0, EditMode 36/36, PlayMode 2/2. |
| `git diff --check` | Passed | No whitespace errors after restoring Unity batchmode `ProjectSettings/ProjectSettings.asset` drift. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 through AC-15 | Implemented / pending final clean-HEAD Player evidence | Build/smoke scripts, Unity build entry point, runtime smoke hook, catalog paths, audit-blocker corrections, and guards are implemented. Pre-commit .NET, static, repository, and Unity compile/EditMode/PlayMode gates passed. Final `TC-PLAYER` build/smoke evidence must be produced from the clean committed HEAD before push. |

### Build and artifact evidence

- Build identity: Pending final clean-HEAD `scripts/build-dev.ps1` run.
- Artifact path / name: Pending final clean-HEAD `artifacts/builds/<BuildId>/Windows-x64/Odyssey.exe`.
- Checksums: Pending final clean-HEAD `scripts/build-dev.ps1` / `scripts/test-player-smoke.ps1` validation.
- Test or quality report: Pre-commit .NET and Unity validation passed; final Player smoke pending clean committed HEAD.

### Known limitations

- Final ODY-S00-009 Windows Development-Debug artifact evidence is pending post-commit clean-HEAD validation.
- Final ODY-S00-009 Player smoke evidence is pending post-commit clean-HEAD validation.
- ODY-S00-009 remains In Progress until final build/smoke evidence passes and owner review begins.

### Follow-up tasks

- Complete validation, commit, clean-HEAD build/smoke validation, and push for owner review.

### Self-review summary

- Scope review: Ready contract stays within backlog ODY-S00-009 and excludes release/distribution/gameplay/database/networking scope.
- Architecture review: Uses existing ADR-005, ADR-006, ADR-007, ADR-009, and ADR-010 constraints without changing them.
- Test review: Owner-approved `TC-PLAYER-001` through `TC-PLAYER-010` are mapped to implemented owning scripts and guarded by `scripts/verify-test-structure.ps1`.
- Security/privacy review: Redaction and artifact privacy constraints are explicit.
- Documentation/version review: No baseline, ADR, TDB, schema, protocol, ruleset, package, or application version is changed.

## 18. Blockers, decisions, and change control

### Blockers

- Independent pre-PR implementation audit of HEAD `8b792c245fe5ca1d21555f32e3ef4480d444953b` returned `NO-GO`, blockers 5 P1. Audit-blocker correction pre-commit gates passed; final clean-HEAD build/smoke evidence remains pending before push.

### Decisions made during execution

- 2026-08-14 - Create ODY-S00-009 contract from repository authorities because no existing draft task contract was present - Authority / approval: product owner decision.
- 2026-08-14 - Keep ODY-S00-009 Draft rather than Ready while TestCaseId/catalog mapping is missing - Authority / approval: product owner instruction in this task contract transition.
- 2026-08-14 - Approve `TC-PLAYER-001` through `TC-PLAYER-010`, canonical output layout `artifacts/builds/<BuildId>/Windows-x64/`, smoke timeouts, exit/process cleanup rules, and mandatory automated Input System Submit/Cancel evidence - Authority / approval: product owner instruction.
- 2026-08-14 - Start ODY-S00-009 implementation in the existing task branch; status moved to In Progress - Authority / approval: product owner implementation ТЗ.
- 2026-08-14 - Correct the five independent pre-PR audit P1 blockers without opening a PR and keep ODY-S00-009 In Progress - Authority / approval: product owner audit-blocker remediation ТЗ.

### Approved task changes

- ODY-S00-009 moved from Draft to Ready after owner-approved Player build and smoke TestCase mapping.
- ODY-S00-009 moved from Ready to In Progress for implementation.
