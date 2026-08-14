# ODY-S00-008 - Fast CI and Build Identity

**Status:** In Progress
**Roadmap stage / slice:** SLICE-00
**Owner:** Codex
**Requested by:** Product owner
**Branch:** `feat/ody-s00-008-fast-ci-build-identity`
**Pull request:** Not opened
**ExecPlan:** `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
**Created:** 2026-08-12 13:02 UTC
**Last updated:** 2026-08-12 18:20 UTC

## 1. Goal

Create the SLICE-00 fast pull-request CI gates and canonical BuildIdentity pipeline so every checked repository state is tied to exact source, toolchain, compatibility, and provenance metadata.

Fast CI is split into no-secret GitHub Actions gates and mandatory local Unity merge validation under the current Unity Personal constraint. The resulting no-secret CI must reject invalid repository states and expose one generated BuildIdentity consistently to the Developer Shell, diagnostics, and generated metadata. It must not create or publish a Release or pretend Unity compiled in GitHub Actions.

## 2. Why this task exists

- Problem or dependency being addressed: ODY-S00-007 proved deterministic serialization across pure .NET, Unity Mono, and focused Windows x64 IL2CPP, but the repository still lacks authoritative required PR workflows and generated BuildIdentity.
- Value or risk reduction: Fast CI and BuildIdentity make every reviewed state traceable to the exact commit, toolchain, compatibility configuration, and generated evidence before Windows build work starts.
- Blocking or enabling relationship: ODY-S00-008 blocks ODY-S00-009. ODY-S00-009 owns the real Windows Development-Debug application artifact and Player smoke.

The Developer Shell currently represents BuildIdentity as unavailable. M5 cannot complete until fast CI, version/build provenance, and remaining diagnostic session/bundle evidence exist.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.0.md`
- `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.5.md`
- Preserved CI/build requirements of Technical Development Baseline v0.3 where v0.4 does not replace them
- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/TASK_TEMPLATE.md`
- `docs/tasks/SLICE-00_BACKLOG.md`
- `docs/tasks/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
- `docs/tasks/completed/ODY-S00-007_Serialization_and_AOT_Compatibility_Spike.md`
- `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`
- `docs/adr/ADR-005_Dependency_Composition_v1.0.md`
- `docs/adr/ADR-006_Test_Project_Structure_and_Dual_Unity_DotNet_Compilation_v1.0.md`
- `docs/adr/ADR-007_Versioning_and_Build_Identity_v1.0.md`
- `docs/adr/ADR-009_Unity_Project_and_Build_Baseline_v1.1.md`
- `docs/adr/ADR-010_Logging_Diagnostics_and_Redaction_v1.1.md`
- Preserved ADR-010 v1.0 diagnostic bundle semantics
- Owner-merged PR #11 and merge commit `88382217a1053fbe5eb631024063800f45e69926`

### Requirement and test IDs

- Requirement IDs: `SLICE-00`, `M1`, `M5`, ADR-007 BuildIdentity/CI requirements, ADR-010 diagnostic bundle/session requirements.
- Existing test IDs: `TC-DIAG-033`, `TC-DIAG-034`, `TC-DIAG-035`, `TC-DIAG-036`, `TC-DIAG-037`, `TC-DIAG-038`, `TC-DIAG-039`, `TC-DIAG-040`; existing `TC-ARCH-*`, `TC-DOTNET-*`, `TC-UNITY-*`, `TC-SER-*`, and repository policy IDs must be preserved.
- Proposed task-specific TestCase IDs to introduce after catalog audit: `TC-CI-001` through `TC-CI-012`, `TC-BUILDID-001` through `TC-BUILDID-014`, `TC-VERSION-003` through `TC-VERSION-008`, and `TC-PROVENANCE-001` through `TC-PROVENANCE-006`.

Before implementation assigns new IDs to `Tests/Metadata/test-catalog.json`, audit the repository test catalog, ADR-006, ADR-007, and accepted repository-accessible Test Strategy material. Do not repurpose an existing ID.

### Proposed ID meanings

| Proposed ID | Meaning |
|---|---|
| `TC-CI-001` | Required `pull_request`/`main` and `push`/`main` triggers. |
| `TC-CI-002` | Top-level `contents: read` permissions. |
| `TC-CI-003` | Immutable approved Action SHAs. |
| `TC-CI-004` | Repository policy, format, and test-structure scripts wired to the owning job. |
| `TC-CI-005` | .NET restore, build, and test scripts wired to the owning job. |
| `TC-CI-006` | Static Unity version/package check does not run Unity. |
| `TC-CI-007` | BuildIdentity/provenance job wired for PR and `push`/`main` evidence. |
| `TC-CI-008` | No `pull_request_target`, write token, or fork secrets. |
| `TC-CI-009` | No GameCI or Unity secrets. |
| `TC-CI-010` | Invalid workflow fixture makes the owning check fail. |
| `TC-CI-011` | Unavailable toolchain or non-zero script cannot false-green. |
| `TC-CI-012` | Stable check names, bounded time/retention, and no required-job skip trick. |
| `TC-VERSION-003` | Valid strict root `version.json` schema v1 is accepted. |
| `TC-VERSION-004` | Malformed JSON, duplicate properties, unknown schema, unknown fields and missing required version-source fields are rejected. |
| `TC-VERSION-005` | Exact SemVer source rules are enforced and tracked ApplicationVersion remains `0.1.0`. |
| `TC-VERSION-006` | Valid `config/compatibility.json` schema v1 and range invariants are accepted. |
| `TC-VERSION-007` | Malformed/duplicate/unknown compatibility configuration, non-positive values and invalid ranges are rejected. |
| `TC-VERSION-008` | Canonical compatibility representation and SHA-256 digest are stable for identical validated inputs. |
| `TC-BUILDID-001` | Canonical BuildIdentity generator creates Local identity. |
| `TC-BUILDID-002` | Canonical BuildIdentity generator creates PullRequest identity. |
| `TC-BUILDID-003` | Development identity exists where required for CI/main evidence. |
| `TC-BUILDID-004` | BuildId changes for a new build execution. |
| `TC-BUILDID-005` | Full and short Git SHA, ref, and working-tree state are exact. |
| `TC-BUILDID-006` | Unity version, .NET SDK version, configuration, target, architecture, scripting backend, and API compatibility are recorded. |
| `TC-BUILDID-007` | Dirty Local build is explicitly marked. |
| `TC-BUILDID-008` | PR/CI identity cannot claim Release or ReleaseCandidate. |
| `TC-BUILDID-009` | Generated C# and JSON describe the same identity. |
| `TC-BUILDID-010` | Developer Shell displays supplied BuildIdentity. |
| `TC-BUILDID-011` | Developer Shell has explicit unavailable/failure state when identity cannot be loaded. |
| `TC-BUILDID-012` | Startup diagnostics include only allowlisted safe BuildIdentity fields. |
| `TC-BUILDID-013` | Username, machine name, absolute path, persistent device ID, and secrets are absent. |
| `TC-BUILDID-014` | Generated identity records the exact source commit. |
| `TC-PROVENANCE-001` | `build-identity.json` is generated. |
| `TC-PROVENANCE-002` | Evidence artifact SHA-256 checksums are generated. |
| `TC-PROVENANCE-003` | Evidence links BuildId and commit. |
| `TC-PROVENANCE-004` | Evidence retention is bounded. |
| `TC-PROVENANCE-005` | No GitHub Release or ODY-S00-009 application artifact is produced. |
| `TC-PROVENANCE-006` | Branch protection/ruleset availability is recorded honestly with owner action if inaccessible. |

### Task-safe private context

- Approved summary / references: Use only repository-safe PR #11 metadata and merge evidence. Do not copy private product documents, local paths, secrets, license data, or hidden campaign content into committed files or workflow artifacts.

## 4. Verified current state

### Verified facts

- PR #11 is owner-merged into `main` as merge commit `88382217a1053fbe5eb631024063800f45e69926`, with merged head `555c7adbead725cf84658588d3777a3827f39dd6`.
- ODY-S00-007 completed deterministic Newtonsoft-based serialization compatibility across pure .NET, Unity Mono, and focused Windows x64 IL2CPP.
- Active Developer Shell runtime exists and currently has safe BuildIdentity unavailable behavior from prior tasks.
- Existing repository entry-point scripts include restore, formatting, test-structure, fast .NET tests, Unity tests, repository verification, repository policy, and serialization AOT smoke.
- `.github/workflows/**`, root `version.json`, root `config/compatibility.json`, generated BuildIdentity files, and required PR CI workflows are not created by this activation commit.
- ODY-S00-009 remains Draft and owns the real Windows Development-Debug application artifact and Player smoke.
- Zero-write CI/Unity runner preflight completed with verdict `GO AFTER OWNER DECISION`.
- Owner decision: Unity Personal is available; a dedicated isolated self-hosted runner is unavailable; a paid Unity serial and Unity Licensing Server are unavailable.
- Selected path: no-secret GitHub Actions gates plus mandatory local Unity merge validation.
- GameCI Personal `.ulf` workaround is considered but not approved; Unity credentials and license files are not added to GitHub Secrets.

### Assumptions

- Branch protection/ruleset settings may require owner action or higher GitHub permissions; implementation must verify directly before claiming enforcement.
- Automated Unity execution in GitHub Actions remains unavailable until a future owner-approved licensing/runner amendment.

## 5. Scope

### In scope

- Root `version.json` following ADR-007 schema version 1 with initial/current development `ApplicationVersion` `0.1.0`.
- Root `config/compatibility.json` following ADR-007 with strict schema and required version validation.
- One canonical BuildIdentity generator for Local, PullRequest, and required Development identities.
- Generated identity outputs used consistently by Developer Shell, startup diagnostics, and generated `build-identity.json`.
- Fast repository-controlled no-secret GitHub Actions workflows for pull requests and required main/development verification scope.
- Mandatory local Unity merge validation using exact Unity `6000.4.0f1 (8cf496087c8f)` and `scripts/test-unity.ps1`.
- Stable required-check names and documentation of intended `main` required checks.
- BuildIdentity/provenance evidence with SHA-256 checksums and bounded retention.
- ADR-010 diagnostic session/bundle evidence for `TC-DIAG-033` through `TC-DIAG-040`.
- Architecture guards and tests proving invalid fixtures fail the owning gate.
- Zero-write Unity runner/licensing preflight decision record before implementation; current result prohibits Unity Editor execution in GitHub Actions.

### Out of scope

- ODY-S00-009 Windows Development-Debug application artifact.
- Application Player startup/shutdown smoke.
- Release or Release Candidate publication.
- Git tags.
- ApplicationVersion bump beyond initial ADR-007 `0.1.0` source.
- SQLite, campaign database/schema/migrations, full `.odcamp` implementation, networking, transport, gameplay, map, tokens, dice, characters, combat, content tools, installer/updater, code signing, SBOM/provenance attestation framework, telemetry, crash upload, Unity/HDRP/Input System upgrade, unrelated package changes, new DI/logging/versioning/CI framework dependencies, weakening/skipping/deleting existing tests, modifying canonical serialization vectors merely to satisfy CI, or deleting historical ODY-S00-007 evidence.

### Allowed paths

These are future implementation permissions for ODY-S00-008, not permission to edit them during this activation commit.

```text
.github/workflows/**
version.json
config/compatibility.json
Directory.Build.props
Assets/Odyssey/Client/Editor/**
Assets/Odyssey/Client/Runtime/**
Packages/com.odyssey.application/Runtime/**
DotNet/Projects/**
DotNet/Tests/**
Assets/Odyssey/Client/Tests/EditMode/**
Tests/Metadata/test-catalog.json
scripts/**
README.md
THIRD_PARTY_NOTICES.md
ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.0.md
docs/tasks/**
docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md
```

### Paths requiring explicit approval before editing

```text
ProjectSettings/**
Packages/manifest.json
Packages/packages-lock.json
docs/adr/**
TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.5.md
AGENTS.md
PLANS.md
```

Any new dependency, GitHub Action, Unity package, or architecture decision requires exact approval and license evidence before implementation. GameCI actions are not approved by this contract.

## 6. Technical constraints

- Module ownership and dependency direction: Follow ADR-001. BuildIdentity contracts may be Application-owned and exposed through Unity Client composition; Domain must not gain CI, Unity, filesystem, workflow, logging, or infrastructure dependencies.
- Authoritative-state and transaction boundary: Not applicable; no gameplay or campaign mutation is introduced.
- Serialization / compatibility boundary: Follow ADR-003 v1.1 and ADR-007. `version.json`, `compatibility.json`, generated `build-identity.json`, and diagnostic bundle metadata must be explicit versioned contracts and not Domain aggregate serialization.
- Time / RNG rule: Build timestamps are generated by the build identity pipeline as UTC metadata; authoritative gameplay time/RNG remains out of scope.
- Unity / thread / lifetime rule: Follow ADR-005 and ADR-009. Runtime BuildIdentity exposure must not introduce service locator behavior or mutable global state.
- Dependency / licensing rule: New GitHub Actions, dependencies, executables, or downloaded tools are blocked until exact source, immutable SHA/version, license, necessity, and owner approval are recorded. GameCI and Unity license secrets are not approved.
- Security / privacy / redaction rule: Follow ADR-010. BuildIdentity and diagnostics must exclude username, machine name, absolute local path, persistent device ID, secrets, environment dumps, private documentation, and hidden campaign content.
- Performance or platform constraint: CI is a fast gate. Do not silently add PlayMode, full IL2CPP, or ODY-S00-009 Player build to the fast PR gate unless an accepted authority explicitly requires it.
- Other: Mandatory criteria cannot be silently deferred. Current Unity Personal licensing means GitHub Actions do not run Unity; local Unity validation is the required merge gate. If local Unity validation cannot prove the exact version and pass, stop instead of claiming readiness.

## 7. Expected behavior

### Scenario 1 - Pull request fast gate

**Given** a pull request against `main`  
**When** GitHub Actions run the required fast gate  
**Then** repository policy, formatting, structure/architecture, source inventory, toolchain, restore, .NET build/tests, static Unity project/package integrity, and BuildIdentity validation execute through repository entry-point scripts and fail closed on invalid state. GitHub Actions do not run or emulate Unity Editor under the current licensing decision.

### Scenario 1b - Local Unity merge gate

**Given** ODY-S00-008 is being prepared for Draft PR readiness or owner merge  
**When** local Unity validation is run  
**Then** `scripts/test-unity.ps1` uses exact Unity `6000.4.0f1 (8cf496087c8f)` and records Unity compile, EditMode, and PlayMode evidence. Missing evidence, wrong Unity version, failure, or generated drift that is not cleaned blocks completion and PR readiness.

### Scenario 2 - Canonical identity

**Given** a local, pull-request, or development build identity generation request  
**When** the generator reads `version.json`, `config/compatibility.json`, Git metadata, and approved toolchain metadata  
**Then** one canonical BuildIdentity feeds generated metadata, runtime Developer Shell display, and startup diagnostics without independently hand-written version strings.

### Scenario 3 - Diagnostic bundle safety

**Given** a local explicit diagnostic bundle/session operation  
**When** the ODY-S00-008 diagnostic checks run  
**Then** session expiry, secret-field guard, include/exclude categories, checksums, truncation, campaign database absence, private documentation absence, and machine/persistent-device absence are proven by `TC-DIAG-033` through `TC-DIAG-040`.

### Required invariants

- The same source BuildIdentity feeds runtime UI, diagnostics, and generated metadata.
- PR/CI identity cannot claim Release or ReleaseCandidate.
- Dirty Local identity is explicitly marked.
- A new build execution receives a new BuildId.
- Generated identity records the exact source commit.
- Fork pull requests receive no secrets.
- CI does not produce false-green results or substitute static validation for Unity compile.
- Local Unity merge evidence is mandatory while Unity CI is unavailable.
- No ODY-S00-009 application artifact, Release, Release Candidate, Git tag, SQLite, networking, gameplay, telemetry, or private documentation is introduced.

## 8. Deliverables

- Production code: BuildIdentity contracts/generator integration and Developer Shell/diagnostic exposure needed by ADR-007.
- Tests: .NET and Unity tests for version sources, BuildIdentity, diagnostics, and architecture/security guards.
- Scripts / CI: No-secret GitHub Actions workflows and repository entry-point script updates needed for fast gates and identity/provenance generation.
- Configuration: root `version.json` and `config/compatibility.json`.
- Documentation: task Completion Evidence, parent ExecPlan evidence, README/status updates, check-name/branch-protection evidence, and license/action evidence where applicable.
- Generated evidence or build artifacts: `build-identity.json`, evidence checksums, bounded CI artifacts. No Release and no ODY-S00-009 application artifact.
- Migration / recovery material: rollback by reverting the ODY-S00-008 PR and removing task-owned workflow/identity/config artifacts.

## 9. Acceptance criteria

1. `version.json` and `config/compatibility.json` are strictly validated against ADR-007.
2. ApplicationVersion remains `0.1.0`; no automatic bump or tag is introduced.
3. One canonical BuildIdentity generator produces valid Local and CI identities.
4. Clean, dirty, local, pull-request, and required development channel behavior is tested.
5. BuildId and DisplayVersion follow ADR-007.
6. Generated identity points to the exact source commit and toolchain.
7. The same identity is exposed to Developer Shell, diagnostics, and `build-identity.json`.
8. Machine name, username, absolute local path, and persistent device ID are absent.
9. Required no-secret fast CI gates call repository entry-point scripts.
10. An invalid architecture/repository fixture causes its owning check to fail.
11. Exact .NET toolchain and static Unity project/package state are validated in GitHub Actions without claiming Unity compile evidence.
12. Exact Unity `6000.4.0f1 (8cf496087c8f)` local validation through `scripts/test-unity.ps1` is recorded before completion and PR readiness.
13. Local Unity failure, missing evidence, wrong Unity version, or unclean generated drift blocks completion and PR readiness.
14. Action dependencies use immutable SHAs, minimal permissions, and verified licenses.
15. GameCI and Unity license secrets are absent from the implementation.
16. Fork PRs receive no secrets.
17. Artifact/evidence retention is bounded.
18. Generated evidence has BuildId/commit linkage and SHA-256 checksums.
19. `TC-DIAG-033` through `TC-DIAG-040` pass with their preserved meanings.
20. No Release, tag, or ODY-S00-009 application artifact is produced.
21. No SQLite, networking, gameplay, telemetry, or private documentation enters the task.
22. Required commands have real evidence.
23. PR remains owner-merged only; Codex never merges.

The task is not complete while any mandatory criterion is unverified, failed, or silently deferred.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-DIAG-033` | .NET / script | Diagnostic session expiry | Pass |
| `TC-DIAG-034` | .NET / script | Secret-field guard | Pass |
| `TC-DIAG-035` | .NET / script | Bundle manifest include/exclude categories | Pass |
| `TC-DIAG-036` | .NET / script | Bundle checksums | Pass |
| `TC-DIAG-037` | .NET / script | 50 MiB truncation report | Pass |
| `TC-DIAG-038` | .NET / script | Campaign database absence | Pass |
| `TC-DIAG-039` | .NET / script | Closed/private documentation absence | Pass |
| `TC-DIAG-040` | .NET / script | Machine name and persistent device ID absence from system summary | Pass |
| `TC-CI-001` through `TC-CI-012` | GitHub Actions / scripts | Fast gate behavior, security, and false-green prevention after ID audit | Pass |
| `TC-VERSION-003` through `TC-VERSION-008` | .NET / scripts | Version source and compatibility config validation after ID audit | Pass |
| `TC-BUILDID-001` through `TC-BUILDID-014` | .NET / Unity EditMode / scripts | BuildIdentity generation and runtime exposure after ID audit | Pass |
| `TC-PROVENANCE-001` through `TC-PROVENANCE-006` | scripts / GitHub Actions | Evidence artifact provenance and settings evidence after ID audit | Pass |

### Required commands

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\restore.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-format.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-test-structure.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-fast.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-unity.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-repository.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-repository-policy.ps1
dotnet build .\DotNet\Odyssey.Core.sln --no-restore
dotnet test .\DotNet\Odyssey.Core.sln --no-build --no-restore
git diff --check
git diff --cached --check
```

Additional workflow validation must retrieve real GitHub Actions results after workflows exist. Do not claim branch protection/ruleset or required-check enforcement without direct evidence.

### Manual validation

- Verify PR #11 merge evidence before implementation starts.
- Perform zero-write Unity runner/licensing preflight before selecting or implementing workflow Unity execution.
- Verify GitHub branch protection/ruleset availability or record exact owner action required.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 for local validation; GitHub-hosted or approved runner profile for CI.
- Unity editor or Player profile: Unity `6000.4.0f1`; Unity compile/EditMode/PlayMode evidence is required through the local merge gate. Full Player build belongs to ODY-S00-009.
- Scripting backend: Validate configured Unity project/backend metadata; do not claim ODY-S00-009 Player build.
- Network topology or database fixture: None for gameplay/persistence; diagnostic bundle tests must prove campaign database absence.
- Other: GitHub Actions runner/licensing and action dependency evidence must be explicit. Current authority prohibits Unity Editor execution in GitHub Actions.

### Validation not required by this task

- ODY-S00-009 Windows Development-Debug application build and Player startup/shutdown smoke: out of scope.
- Release/ReleaseCandidate publication, signing, SBOM, installer/updater: out of scope.
- SQLite migrations, networking transport, gameplay, full `.odcamp`: out of scope.
- Full IL2CPP application artifact: out of scope unless a future owner decision changes ODY-S00-008.

## 11. Compatibility, migration, and rollback

- Compatibility impact: Introduces ADR-007 version source and compatibility config contracts for build/CI identity; does not create campaign persistence or network protocol compatibility.
- Version fields affected: `ApplicationVersion` initial source `0.1.0`; compatibility config schema v1 and initial compatibility-version values required by ADR-007.
- Migration or upcaster: None.
- Forward / backward behavior: Unknown schema or unsupported future version/config fields fail safely before generating identity.
- Rollback method: Revert the ODY-S00-008 PR; remove task-owned workflows, generated identity/config sources, and tests.
- Data-loss risk and protection: None; no campaign data or persistence.
- Recovery rehearsal required: CI and local validation must prove generated identity can be regenerated from source metadata.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None approved by activation | Not applicable | Implementation must verify exact action/dependency source before use | Not applicable | Not applicable |

Official GitHub Actions are not automatically approved by this activation. Their exact repository, immutable SHA, license, and necessity must be verified during implementation before use.

GameCI actions were considered during zero-write preflight and are not approved. Reason: the current Unity Personal activation path does not match an owner-approved unattended CI mechanism for this repository. `UNITY_LICENSE`, `UNITY_EMAIL`, and `UNITY_PASSWORD` secrets are not created. Approved implementation actions are currently limited to official GitHub Actions after immutable-SHA and license verification.

## 13. Security, privacy, and hidden information

- Data classes handled: Build metadata, Git metadata, toolchain metadata, compatibility config, diagnostic bundle metadata, CI logs/artifacts.
- Trust boundaries: Pull requests may be untrusted; fork PRs must not receive secrets. Unity credentials/license files must not be added to GitHub Secrets. Local machine data must not enter committed files, logs, generated identity, diagnostics, or artifacts.
- Authorization / audience checks: Not a gameplay permissions task; CI token permissions must be minimal and explicit.
- Redaction requirements: No secret values in logs; no environment dumps; no private documentation; no hidden campaign content.
- Log-safe fields: BuildId, safe product/version/channel values, commit SHA, ref, toolchain versions, configuration, target, compatibility versions and digests.
- Abuse / malformed input limits: Invalid schemas, unknown required fields, bad version syntax, digest mismatch, and unavailable toolchains fail closed.
- Security tests: `TC-CI-008`, `TC-CI-009`, `TC-CI-012`, `TC-BUILDID-013`, `TC-DIAG-034`, `TC-DIAG-038`, `TC-DIAG-039`, `TC-DIAG-040`, and repository policy checks.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: This task changes CI, generated identity, runtime display/diagnostic exposure, version/compatibility sources, GitHub settings evidence, and artifact provenance across several ownership areas.
- ExecPlan path: `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
- Expected pull request count: 1 implementation PR after owner review of this activation contract.
- Milestone or sequencing constraints: Implementation is owner-approved and in progress. ODY-S00-009 remains blocked by ODY-S00-008.

## 15. Documentation and versioning impact

- Documents that must change: This task contract, parent ExecPlan evidence, backlog, README/status pointers, test catalog during implementation, branch protection/check-name evidence, task Completion Evidence, and license/action evidence when dependencies/actions are selected.
- Documents that must not change: ADR files, `AGENTS.md`, and `PLANS.md` unless a material conflict requires owner approval.
- Application version change: Yes - create initial ADR-007 `version.json` with `0.1.0`; no automatic bump.
- Schema / format / contract / protocol / ruleset version change: Add ADR-007 `version.json`, `config/compatibility.json`, generated BuildIdentity, and diagnostic bundle evidence contracts. No database schema, network protocol, ruleset, campaign format, or Release publication.
- Documentation version changes: Technical Baseline v0.5 and Active Baseline v2.0 record the owner Unity Personal CI decision.
- Changelog or release-note requirement: None; record evidence in task and ExecPlan only.

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

Implementation complete pending commit/push. Final command results are recorded below.

### Zero-write preflight and owner decision

```text
Verdict: GO AFTER OWNER DECISION
Owner decision: Unity Personal; no isolated self-hosted runner.
Selected path: no-secret GitHub CI plus mandatory local Unity merge validation.
```

After this decision is recorded, the licensing blocker is resolved for starting no-secret ODY-S00-008 implementation after PM review. Automated Unity CI remains unapproved until a later owner decision.

### Changed files / areas

- No-secret GitHub Actions workflow.
- Root `version.json` and `config/compatibility.json`.
- Application-owned BuildIdentity/version/diagnostic bundle contracts.
- Unity Client BuildIdentity provider, Developer Shell display, and startup diagnostic exposure.
- Repository scripts for CI, static Unity validation, BuildIdentity generation, and BuildIdentity verification.
- Test catalog entries and .NET/Unity tests for ODY-S00-008.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\restore.ps1` | Pass | Initial sandbox run failed on SDK access to `C:\Users\alexx\AppData\Local\Microsoft SDKs`; escalated rerun passed: all projects up to date for restore. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-format.ps1` | Pass | `FORMAT-001 PASS repository text formatting checks passed`. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-test-structure.ps1` | Pass | `TC-ARCH-001 PASS`; controlled invalid Domain->Rules, package mismatch, duplicate catalog ownership, and duplicate TestCaseId fixtures all rejected. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-fast.ps1` | Pass | Build succeeded with 0 warnings/0 errors; TRX evidence: Contracts 1/1, Domain 1/1, Unit 77/77, Architecture 2/2. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-repository-policy.ps1` | Pass | `REPO-POLICY-001` through `REPO-POLICY-005` passed; registry negative fixtures passed; CI/static Unity subchecks passed. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-repository.ps1` | Pass | Repository policy, test structure, CI verifier, static Unity verifier, and SDK check passed; selected SDK `10.0.302`. |
| `dotnet build .\DotNet\Odyssey.Core.sln --no-restore` | Pass | Escalated rerun passed: 0 warnings, 0 errors. |
| `dotnet test .\DotNet\Odyssey.Core.sln --no-build --no-restore` | Pass | Escalated rerun passed: Contracts 1, Domain 1, Unit 77, Architecture 2; 0 failed. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-ci.ps1` | Pass | `TC-CI-001` through `TC-CI-012` passed; controlled invalid workflow fixtures rejected old single-job shape, missing `push`, `windows-latest`, missing job, floating Action tag, `continue-on-error`, unapproved Unity Action, excessive retention, and required-job skip condition. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-unity-project.ps1` | Pass | Static Unity project/package/toolchain source validation passed; Unity compile not claimed. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\generate-build-identity.ps1 -Channel local -GitRef heads/local -BuildNumber 1 -RunAttempt 1 -TimestampUtc 20260812T120000Z` | Pass | Generated dirty Local BuildId `odyssey-local-20260812t120000z-g4a989c318380-dirty` for commit `4a989c3183808b0e0e0a2faabf610305e8fa1904`. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-build-identity.ps1` | Pass | Latest Unity-gate identity parity/checksum passed for BuildId `odyssey-local-20260812t151454z-g4a989c318380-dirty`; checksum `fa0f4cfe0fd49ba86bfac7cb98897df4bf00511499c8354e3bc40fa62f8140dc`. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-unity.ps1` | Pass | Escalated local Unity gate passed: generated Local BuildIdentity, Unity Editor `6000.4.0f1`, batch compile exit 0, EditMode exit 0, PlayMode exit 0, EditMode 33/33, PlayMode 2/2. |

### CI contract correction validation

Independent pre-PR audit of implementation HEAD `eea58a01e63179061f21ad44fabbc27a1a1c880d` returned `NO-GO` for one blocker: the workflow/verifier contract still used the earlier single-job `odyssey-fast-ci` shape. The correction replaces it with workflow `ci`, triggers `pull_request`/`main` and `push`/`main`, and four required `windows-2022` jobs: `repository-policy-format-structure`, `dotnet-restore-build-test`, `unity-project-package-static`, and `buildidentity-provenance`.

| Command / check | Result | Evidence / notes |
|---|---|---|
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\restore.ps1` | Pass | Initial sandbox run failed on SDK access to `C:\Users\alexx\AppData\Local\Microsoft SDKs`; escalated rerun restored 4 of 8 projects and found the rest up to date. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-format.ps1` | Pass | `FORMAT-001 PASS repository text formatting checks passed`. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-test-structure.ps1` | Pass | `TC-ARCH-001 PASS`; controlled invalid Domain->Rules, package mismatch, duplicate catalog ownership, and duplicate TestCaseId fixtures all rejected. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-fast.ps1` | Pass | Initial sandbox run failed on SDK access; escalated rerun passed. Build: 0 warnings, 0 errors. Tests: Contracts 1/1, Domain 1/1, Unit 77/77, Architecture 2/2. |
| `dotnet build .\DotNet\Odyssey.Core.sln --no-restore` | Pass | 0 warnings, 0 errors. |
| `dotnet test .\DotNet\Odyssey.Core.sln --no-build --no-restore` | Pass | Contracts 1, Domain 1, Unit 77, Architecture 2; 0 failed. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-ci.ps1` | Pass | `TC-CI-001` through `TC-CI-012` passed; negative fixtures rejected old single-job shape, missing `push`, `windows-latest`, missing job, floating Action tag, `continue-on-error`, unapproved Unity Action, excessive retention, and required-job skip condition. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-unity-project.ps1` | Pass | Static Unity project/package/toolchain source validation passed; Unity Editor compile not claimed. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-repository-policy.ps1` | Pass | Repository policy and nested CI/static Unity checks passed. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-repository.ps1` | Pass | Repository policy, structure, CI verifier, static Unity verifier, and SDK check passed; selected SDK `10.0.302`. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\generate-build-identity.ps1 -Channel pull_request -GitRef refs/pull/8/merge -BuildNumber 1008 -PullRequestNumber 8 -RunAttempt 1 -TimestampUtc 20260814T012000Z` | Pass | Generated `odyssey-pr-1008.1-geea58a01e631` for commit `eea58a01e63179061f21ad44fabbc27a1a1c880d`. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-build-identity.ps1 -BuildIdentityPath .\artifacts\build-identity\odyssey-pr-1008.1-geea58a01e631\build-identity.json` | Pass | Checksum `0423ba22feb1cde59bc4d44995b82c68633fdd41a4c5566c6a708a45292baa19`; commit link verified. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\generate-build-identity.ps1 -Channel development -GitRef refs/heads/main -BuildNumber 2008 -RunAttempt 2 -TimestampUtc 20260814T012500Z` | Pass | Generated `odyssey-development-2008.2-geea58a01e631` for commit `eea58a01e63179061f21ad44fabbc27a1a1c880d`. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-build-identity.ps1 -BuildIdentityPath .\artifacts\build-identity\odyssey-development-2008.2-geea58a01e631\build-identity.json` | Pass | Checksum `edfaa2ee4e9d3c2ca43f1ea8e3d67982209f05bc5b1ad3f6d2c6a67212dd976d`; commit link verified. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-unity.ps1` | Pass | Unity Editor `6000.4.0f1`; generated Local BuildIdentity `odyssey-local-20260814t012620z-geea58a01e631-dirty`; batch compile exit 0; EditMode 33/33; PlayMode 2/2; Player build not run. |

Unity batchmode produced ProjectSettings whitespace drift in `ProjectSettings/ProjectSettings.asset`; the path was clean before Unity, contained no semantic settings change, and was restored after validation. No Release/RC identity outputs were produced.

### Draft PR and first GitHub Actions evidence

Independent pre-PR audit verdict: `GO for Draft PR`.

Draft PR #12 is open at https://github.com/odyssey-services/Odyssey_VTT/pull/12 against `main`.

```text
PR state: Draft
Ready for Review: No
Merge: Not requested
Initial validated code HEAD: 5c401fd4c7f2cb033b951e4f2ce0ee338c545ac4
First real GitHub Actions run: 31762586128
Run URL: https://github.com/odyssey-services/Odyssey_VTT/actions/runs/31762586128
```

First run check results:

| Check | Conclusion | Head SHA | Runner |
|---|---|---|---|
| `ci / repository-policy-format-structure` | Success | `5c401fd4c7f2cb033b951e4f2ce0ee338c545ac4` | `windows-2022` |
| `ci / dotnet-restore-build-test` | Success | `5c401fd4c7f2cb033b951e4f2ce0ee338c545ac4` | `windows-2022` |
| `ci / unity-project-package-static` | Success | `5c401fd4c7f2cb033b951e4f2ce0ee338c545ac4` | `windows-2022` |
| `ci / buildidentity-provenance` | Success | `5c401fd4c7f2cb033b951e4f2ce0ee338c545ac4` | `windows-2022` |

First provenance artifact evidence:

```text
Artifact: odyssey-build-identity
Contents:
- build-identity.json
- checksums.sha256
Retention: 7 days
Expires: 2026-08-21T02:04:56Z
Checksum: 60a8389c6561ffc96bbebaa159f9bd00e6445ad4422aae66ee463dd7846e4c46
Artifact files absent: generated C#, StreamingAssets copy, repository snapshot, application/Player artifact, environment dump, extra files.
```

First provenance linkage:

```text
PR: #12
Feature HEAD: 5c401fd4c7f2cb033b951e4f2ce0ee338c545ac4
Synthetic PR merge checkout SHA: 91bb6f4c01ef3ba2c392ab1204e09e848badb603
BuildIdentity gitCommitSha: 91bb6f4c01ef3ba2c392ab1204e09e848badb603
displayVersion: 0.1.0-pr.12.1+g91bb6f4c01ef
gitRef: refs/pull/12/merge
channel: pull_request
buildNumber: 31762586128
runAttempt: 1
```

Owner clarification: a dedicated `pullRequestNumber` property is not part of ADR-007 BuildIdentity schema v1 and is not required for ODY-S00-008. PR provenance is accepted through canonical cross-field evidence: `displayVersion` contains `pr.12.1`, `gitRef` is `refs/pull/12/merge`, `buildNumber` equals the GitHub run ID, `runAttempt` equals the run attempt, and `gitCommitSha` equals the PR job synthetic merge checkout SHA. The earlier artifact audit finding is reclassified as non-blocking.

Branch protection/ruleset read-only evidence:

```text
Branch protection/ruleset: Owner action/evidence pending
Reason: GitHub branch protection endpoint returned 403 to the integration, and rulesets endpoint returned GitHub plan/public-repository requirement.
```

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 through AC-23 | Satisfied | Version/config strict validation, canonical BuildIdentity, no-secret CI, local Unity merge gate, provenance checksums, ADR-010 diagnostic session/bundle evidence, and scope guards are implemented and validated. |

### Build and artifact evidence

- Build identity: Latest local validation generated `odyssey-local-20260812t151454z-g4a989c318380-dirty`.
- Artifact path / name: `artifacts/build-identity/odyssey-local-20260812t151454z-g4a989c318380-dirty/build-identity.json`.
- Checksums: `artifacts/build-identity/odyssey-local-20260812t151454z-g4a989c318380-dirty/checksums.sha256`; SHA-256 `fa0f4cfe0fd49ba86bfac7cb98897df4bf00511499c8354e3bc40fa62f8140dc`.
- Test or quality report: `.NET` TRX files under `Logs/ODY-S00-008/dotnet/`; Unity XML results under `Logs/ODY-S00-008/editmode-results.xml` and `Logs/ODY-S00-008/playmode-results.xml`.

### Known limitations

- Unity Editor execution in GitHub Actions is not approved under the current Unity Personal constraint.
- Branch protection/ruleset settings may require owner action or unavailable permissions and must be recorded honestly.
- Generated `Assets/Odyssey/Generated/BuildIdentity.g.cs`, `Assets/StreamingAssets/Odyssey/build-identity.json`, `Logs/`, `Library/`, and `artifacts/` are local/ignored validation outputs, not committed production source.

### Follow-up tasks

- ODY-S00-009 remains blocked until ODY-S00-008 completes.

### Self-review summary

- Independent pre-PR audit on corrected HEAD `5c401fd4c7f2cb033b951e4f2ce0ee338c545ac4` returned `GO for Draft PR`. Draft PR #12 is open and remains `Draft`; the first real GitHub Actions run `31762586128` passed all four required checks. Branch protection/ruleset remains `Owner action/evidence pending`.
- Scope review: Implementation is limited to ODY-S00-008 fast CI, BuildIdentity, provenance, static Unity validation, and ADR-010 diagnostic session/bundle evidence.
- Architecture review: Contract follows ADR-001, ADR-005, ADR-006, ADR-007, ADR-009, and ADR-010 without changing ADRs.
- Test review: Existing `TC-VERSION-001` and `TC-VERSION-002` remain owned by ODY-S00-004; ODY-S00-008 owns `TC-VERSION-003` through `TC-VERSION-008`.
- Security/privacy review: CI secrets, local identifiers, machine data, private docs, and hidden campaign content are explicitly prohibited.
- Documentation/version review: Active Baseline v2.0 and Technical Baseline v0.5 record the owner decision without ADR change.
- Implementation review: No Release, Git tag, ODY-S00-009 Player artifact, SQLite, networking, gameplay, telemetry, Unity package/settings change, or GameCI/Unity secret integration was introduced.

## 18. Blockers, decisions, and change control

### Blockers

- Automated Unity CI remains blocked until a future owner-approved licensing or runner amendment.

### Decisions made during execution

- 2026-08-12 - Activate ODY-S00-008 only after owner-merged PR #11; do not begin implementation in the activation commit - Authority / approval: product owner instruction.
- 2026-08-12 - ODY-S00-008 owns `TC-DIAG-033` through `TC-DIAG-040` after BuildIdentity exists; ODY-S00-009 owns the real Windows Development-Debug application artifact and Player smoke - Authority / approval: product owner instruction and ADR-010.
- 2026-08-12 - Under Unity Personal with no isolated self-hosted runner, paid serial, or Unity Licensing Server, ODY-S00-008 uses no-secret GitHub Actions plus mandatory local Unity merge validation; GameCI and Unity secrets are not approved - Authority / approval: product owner instruction and Technical Development Baseline v0.5.
- 2026-08-12 - Preserve `TC-VERSION-001` and `TC-VERSION-002` for ODY-S00-004 and assign ODY-S00-008 version-source/config coverage to `TC-VERSION-003` through `TC-VERSION-008` - Authority / approval: product owner addendum.

### Approved task changes

- Record Personal-license CI decision.
- Implement ODY-S00-008 Fast CI, BuildIdentity, provenance, and diagnostic session/bundle evidence.
- Correct CI workflow contract after independent pre-PR audit `NO-GO`; correction commit SHA is reported in final handoff for this scoped correction.
