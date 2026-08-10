# ODY-S00-003 — Module and Test Skeleton

**Status:** Ready  
**Roadmap stage / slice:** SLICE-00  
**Owner:** Codex  
**Requested by:** Product owner  
**Branch:** Not created; planned `feat/ody-s00-003-module-test-skeleton` after closure PR merge  
**Pull request:** Not opened  
**ExecPlan:** `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`  
**Created:** 2026-08-10  
**Last updated:** 2026-08-10 14:21 UTC

## 1. Goal

Create the physical module and test skeleton required by ADR-001 and ADR-006 so Core production source has one physical copy and compiles through both Unity assemblies and pure .NET bridge/test projects.

No runtime composition, gameplay, persistence, networking behavior, command pipeline, serialization contracts, or Unity application behavior is implemented in this task.

## 2. Why this task exists

- Problem or dependency being addressed: The Unity project foundation exists, but the ADR-001 module graph and ADR-006 dual-compilation/test skeleton do not.
- Value or risk reduction: Establishes dependency boundaries and automated architecture checks before production code appears.
- Blocking or enabling relationship: Depends on completed `ODY-S00-002`; blocks `ODY-S00-004` and later Core/runtime work.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.8.md`
- `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.3.md`, applicable module, Unity, .NET, testing, and repository-command sections
- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/TASK_TEMPLATE.md`
- `docs/tasks/SLICE-00_BACKLOG.md`
- `docs/tasks/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
- `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
- `docs/tasks/completed/ODY-S00-002_Unity_Project_Foundation.md`
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`
- `docs/adr/ADR-005_Dependency_Composition_v1.0.md`, Unity client composition-root boundary only
- `docs/adr/ADR-006_Test_Project_Structure_and_Dual_Unity_DotNet_Compilation_v1.0.md`
- `docs/adr/ADR-009_Unity_Project_and_Build_Baseline_v1.1.md`, existing Unity/package baseline only

### Requirement and test IDs

- Requirement IDs: `SLICE-00`, Milestone `M1`, PR-002 delivery group
- Existing test IDs: ADR-006 and test strategy IDs applicable to dual compilation, Unity EditMode/PlayMode test boundaries, and architecture validation
- New test IDs to introduce: Stable IDs for architecture guard and module/test skeleton checks if the current test strategy requires them during implementation

### Task-safe private context

- Approved summary / references: Build only the module/test skeleton for the technical foundation. Do not copy private product documents, hidden campaign content, local handoff text, secrets, or personal paths into repository artifacts.

## 4. Verified current state

### Verified facts

- ODY-S00-002 was owner-merged through PR #4 into `main` as merge commit `70e7d49e217d4aecb7a2e873d31787d26001f47f` on `2026-08-10T16:21:33+02:00`.
- The accepted Unity baseline is Unity `6000.4.0f1`, changeset `8cf496087c8f`, HDRP `17.4.0`, and Input System `1.19.0`.
- The repository contains Unity project folders `Assets/`, `Packages/`, and `ProjectSettings/` from ODY-S00-002.
- Core embedded package folders `Packages/com.odyssey.domain/`, `Packages/com.odyssey.rules/`, `Packages/com.odyssey.content/`, `Packages/com.odyssey.application/`, `Packages/com.odyssey.persistence/`, and `Packages/com.odyssey.networking/` do not exist yet.
- `DotNet/Odyssey.sln` does not exist yet.
- Only `scripts/check-repository-policy.ps1` exists as a repository command in the merged ODY-S00-002 state; broader canonical scripts are future Slice-00 deliverables.

### Assumptions

- The implementation branch will be created only after the ODY-S00-002 closure PR is owner-reviewed and merged.
- The exact .NET target framework, SDK constraints, and test framework versions are taken from ADR-006 and the current Technical Development Baseline at implementation time; this task does not independently upgrade tools.

## 5. Scope

### In scope

- Embedded Unity packages:
  - `Packages/com.odyssey.domain/`
  - `Packages/com.odyssey.rules/`
  - `Packages/com.odyssey.content/`
  - `Packages/com.odyssey.application/`
  - `Packages/com.odyssey.persistence/`
  - `Packages/com.odyssey.networking/`
- Minimal `package.json` and production `.asmdef` for each production module.
- Strict production dependency direction:
  - `Domain`
  - `Rules -> Domain`
  - `Content -> Domain, Rules`
  - `Application -> Domain, Rules, Content`
  - `Persistence -> Domain, Content, Application`
  - `Networking -> Domain, Content, Application`
  - `Unity Client -> all production modules`
  - `Persistence` and `Networking` must not reference each other.
- Unity Client assembly boundaries:
  - `Assets/Odyssey/Client/Runtime/`
  - `Assets/Odyssey/Client/Editor/`
  - `Odyssey.Unity.Client.Runtime.asmdef`
  - `Odyssey.Unity.Client.Editor.asmdef`
- Pure .NET skeleton:
  - `DotNet/Odyssey.sln`
  - ADR-006 bridge/project structure for single-source Core compilation.
  - .NET projects compile production source directly from `Packages/com.odyssey.*/Runtime/**`.
  - Production source is not copied into `DotNet/`.
- Test projects:
  - `Odyssey.Tests.Unit`
  - `Odyssey.Tests.Domain`
  - `Odyssey.Tests.Contracts`
  - `Odyssey.Tests.Architecture`
  - `Odyssey.Tests.Unity.EditMode`
  - `Odyssey.Tests.Unity.PlayMode`
- Automatic architecture guard for ADR-001 dependency direction.
- Repository scripts that belong directly to module/test skeleton verification, such as `scripts/restore.ps1`, `scripts/verify-format.ps1`, `scripts/test-fast.ps1`, and `scripts/verify-repository.ps1`, if they can perform real checks.

### Out of scope

- Runtime composition or Application behavior; this belongs to ODY-S00-006.
- Typed IDs, `Result/Error`, application version, command/event contracts, Clock/RNG, serialization DTOs/upcasters, persistence behavior, networking behavior, runtime logging behavior, gameplay, GitHub Actions, Windows Player build, release/build CI, BuildIdentity implementation, serialization/AOT spike, and Player build automation.
- Unity package baseline changes, Unity project/package settings changes, application/schema/format/contract/protocol/ruleset version changes, ADR amendments, Technical Baseline amendments, or Active Baseline amendments unless the product owner explicitly approves them before editing.

### Allowed paths

```text
Packages/com.odyssey.domain/**
Packages/com.odyssey.rules/**
Packages/com.odyssey.content/**
Packages/com.odyssey.application/**
Packages/com.odyssey.persistence/**
Packages/com.odyssey.networking/**
Assets/Odyssey/Client/Runtime/**
Assets/Odyssey/Client/Editor/**
Assets/Odyssey/Tests/**
DotNet/**
Tests/**
scripts/restore.ps1
scripts/verify-format.ps1
scripts/test-fast.ps1
scripts/verify-repository.ps1
docs/tasks/active/ODY-S00-003_Module_and_Test_Skeleton.md
docs/tasks/SLICE-00_BACKLOG.md
docs/tasks/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md
docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md
README.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v*.md
ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v*.md
Packages/manifest.json
Packages/packages-lock.json
ProjectSettings/**
.github/**
Assets/Odyssey/**/*.unity
Assets/Odyssey/**/*.asset
```

## 6. Technical constraints

- Module ownership and dependency direction: Follow ADR-001 exactly; no cycles, no dependency shortcuts through generic `Common`, `Shared`, `Utils`, or service-locator modules.
- Authoritative-state and transaction boundary: Not applicable; no commands, state mutation, gameplay behavior, persistence, or networking behavior is introduced.
- Serialization / compatibility boundary: Do not create production DTOs, upcasters, protocol contracts, persistence formats, or serializer contexts.
- Time / RNG rule: Do not introduce authoritative logic, clocks, schedulers, timers, `DateTime.Now`, `UnityEngine.Time`, `System.Random`, or `UnityEngine.Random`.
- Unity / thread / lifetime rule: Unity Client is a composition boundary only; no runtime composition or lifecycle implementation is introduced.
- Dependency / licensing rule: Do not add dependencies, GitHub Actions, executables, downloaded tools, or package/version updates unless explicitly authorized by ADR-006/current baseline.
- Security / privacy / redaction rule: Do not commit private documents, secrets, local caches, generated Unity folders, local absolute paths, or hidden product/campaign content.
- Performance or platform constraint: Windows 10/11 x64 and the accepted Unity `6000.4.0f1` baseline remain unchanged.
- Other: Test assemblies, test fixtures, and test helpers must not enter Player assemblies.

## 7. Expected behavior

### Scenario 1 — Module graph compiles

**Given** the repository checkout after ODY-S00-002  
**When** Unity and .NET compilation checks run  
**Then** each production module compiles in its allowed dependency position without duplicate production source copies.

### Scenario 2 — Invalid dependency is rejected

**Given** an intentional architecture-guard fixture or graph sample with a forbidden edge  
**When** the architecture guard runs  
**Then** the guard fails and identifies the forbidden dependency direction.

### Scenario 3 — Test code stays out of Player assemblies

**Given** Unity test assembly definitions and .NET test projects  
**When** source inventories and assembly references are checked  
**Then** production assemblies do not reference test assemblies or test-only packages.

### Required invariants

- Production source has one physical copy under the owned module packages.
- .NET projects compile source directly from `Packages/com.odyssey.*/Runtime/**`; they do not copy production files into `DotNet/`.
- `Persistence` and `Networking` do not reference each other.
- No runtime composition, product behavior, command/event handling, persistence adapter, network adapter, serialization contract, or gameplay rule is introduced.

## 8. Deliverables

- Production code: Minimal module package and assembly skeleton only; empty marker/types are allowed only if required to prove compilation boundaries.
- Tests: .NET and Unity test assembly skeletons plus architecture guard tests/checks.
- Scripts / CI: Real repository entry scripts needed for restore, format verification, fast tests, and repository verification if implemented in scope; no GitHub Actions.
- Configuration: `.asmdef`, package metadata, .NET solution/projects, and test project metadata needed for ADR-001/ADR-006.
- Documentation: Updated ODY-S00-003 completion evidence, parent task, ExecPlan, backlog, and README status if materially affected.
- Generated evidence or build artifacts: Command output summaries and log/report paths only; generated build/test caches remain untracked.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. Each required embedded production package exists with pinned, repository-owned metadata and a production assembly definition.
2. Production assembly references exactly match the ADR-001 allowed dependency graph, including no `Persistence <-> Networking` reference and no production-to-test reference.
3. Unity Client Runtime and Editor assemblies exist in the required paths and respect Unity/editor/test boundary rules.
4. `DotNet/Odyssey.sln` and bridge/test projects compile the same production source from `Packages/com.odyssey.*/Runtime/**` without copying source into `DotNet/`.
5. Required .NET test projects and Unity EditMode/PlayMode test assemblies exist and are excluded from Player runtime assemblies.
6. The architecture guard automatically detects the listed forbidden dependency categories and cyclic module references.
7. Repository scripts created by this task perform real checks and fail on relevant broken state; no fake or placeholder success wrappers are added.
8. The task introduces no gameplay/domain behavior, persistence/network behavior, runtime composition, logging runtime, serialization contracts, GitHub Actions, Player build automation, or unrelated Unity/package/version changes.
9. Required validation commands run with real pass/fail/not-run evidence recorded before the task moves to `In Review`.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TST-ARCH-001` | Architecture script / .NET test | ADR-001 allowed production dependency graph passes | Pass |
| `TST-ARCH-002` | Architecture script / .NET test | Forbidden dependency categories and cycles fail against intentional fixture/sample | Pass |
| `TST-DOTNET-001` | .NET build/test | Bridge projects compile production source directly from `Packages/com.odyssey.*/Runtime/**` | Pass |
| `TST-UNITY-ASM-001` | Unity compile/EditMode | Production and Unity Client `.asmdef` graph compiles in Unity | Pass |
| `TST-UNITY-TEST-001` | Unity EditMode/PlayMode boundary check | Unity test assemblies exist and do not enter Player assemblies | Pass |
| `TST-REPO-001` | Repository script | Repository policy and generated/private path exclusions remain enforced | Pass |

### Required commands

Commands must use repository entry points once created. If a command does not exist before ODY-S00-003, the implementation must either create a real script in scope or record it as not run with the reason.

```powershell
.\scripts\restore.ps1
.\scripts\verify-format.ps1
.\scripts\test-fast.ps1
.\scripts\verify-repository.ps1
.\scripts\check-repository-policy.ps1
git diff --check
git status --short --branch
```

Unity batchmode compile/EditMode/PlayMode validation is required if ADR-006 or the implementation-generated Unity test assemblies require it. Use the accepted Unity `6000.4.0f1` editor baseline and record the exact command, exit code, and log path.

### Manual validation

- Review all `.asmdef`, `.csproj`, and script references against ADR-001/ADR-006 dependency rules.
- Review the diff for duplicated production source, accidental generated Unity folders, private/local paths, package version drift, and out-of-scope behavior.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Unity Editor `6000.4.0f1 (8cf496087c8f)` for Unity compile/test validation; no Player profile required.
- Scripting backend: Editor/Mono-compatible checks only unless ADR-006 requires more; IL2CPP Player validation is out of scope.
- Network topology or database fixture: None.
- Other: .NET SDK and test framework versions exactly per ADR-006 and Technical Development Baseline v0.3.

### Validation not required by this task

- Windows Player build, IL2CPP smoke, release build, GitHub Actions, BuildIdentity output, serialization/AOT spike, persistence integration, networking integration, runtime composition, diagnostics runtime, gameplay feature tests, and clean-checkout M1 rehearsal.

## 11. Compatibility, migration, and rollback

- Compatibility impact: Adds physical module/test skeletons only; no persisted state, public protocol, schema, save format, or gameplay contract is introduced.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: Revert the ODY-S00-003 pull request and rerun repository policy and diff checks.
- Data-loss risk and protection: None.
- Recovery rehearsal required: None beyond validation commands.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

Do not add production or development dependencies beyond those already approved by ADR-006 and the Technical Development Baseline.

## 13. Security, privacy, and hidden information

- Data classes handled: Repository source/configuration only; no user, campaign, secret, or hidden GM data.
- Trust boundaries: Local repository and validation scripts.
- Authorization / audience checks: Not applicable.
- Redaction requirements: Do not log or commit secrets, private docs, full local paths in generated evidence, or hidden product/campaign content.
- Log-safe fields: Repository-relative paths, module names, command names, exit codes, and non-secret tool versions.
- Abuse / malformed input limits: Architecture guard should fail closed on malformed project/assembly metadata rather than silently passing.
- Security tests: Repository policy check and generated/private path exclusions.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: The task touches multiple production module boundaries, test assemblies, scripts, and dual Unity/.NET compilation contracts.
- ExecPlan path: `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
- Expected pull request count: 1 implementation PR after the ODY-S00-002 closure PR is merged
- Milestone or sequencing constraints: Do not start ODY-S00-003 implementation until owner merges the ODY-S00-002 closure PR. Work in `feat/ody-s00-003-module-test-skeleton`; do not merge.

## 15. Documentation and versioning impact

- Documents that must change: ODY-S00-003 task completion evidence, parent task, ExecPlan, Slice-00 backlog, and README status if materially affected.
- Documents that must not change: ADR-001 through ADR-010, Technical Development Baseline v0.3, Active Documentation Baseline v1.8, private product documents, changelogs, and handoff/context bundles.
- Application version change: No — version source is deferred to later Slice-00 work.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: None expected; operational task/status updates only.
- Changelog or release-note requirement: Task/ExecPlan evidence only; no user-facing release note.

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

Fill this section with real results before moving the task to `In Review`.

### Changed files / areas

- Not started. This contract only activates the task as Ready.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| Implementation validation | Not run | ODY-S00-003 implementation has not started. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 through AC-9 | Deferred | Must be proven during ODY-S00-003 implementation before review. |

### Build and artifact evidence

- Build identity: Not created.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: Not created.

### Known limitations

- No module/test skeleton files have been created yet; this task is Ready only.

### Follow-up tasks

- Start ODY-S00-003 in `feat/ody-s00-003-module-test-skeleton` after the ODY-S00-002 closure PR is owner-reviewed and merged.

### Self-review summary

- Scope review: Contract limits work to module/test skeleton and architecture guard.
- Architecture review: ADR-001 and ADR-006 are the controlling authorities; no new architecture rule is introduced.
- Test review: Required future checks are listed with no success claimed.
- Security/privacy review: Contract excludes private docs, secrets, local handoffs, and hidden campaign/product content.
- Documentation/version review: No ADR, baseline, Unity package, or version amendment is authorized by this task.

## 18. Blockers, decisions, and change control

### Blockers

- ODY-S00-003 implementation must wait until the ODY-S00-002 closure PR is owner-reviewed and merged.

### Decisions made during execution

- 2026-08-10 — Activate ODY-S00-003 only as `Ready` during ODY-S00-002 post-merge closure; do not begin implementation until the closure PR is merged — Authority / approval: product owner instruction.

### Approved task changes

- None.
