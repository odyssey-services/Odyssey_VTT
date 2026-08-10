# ODY-S00-003 — Module and Test Skeleton

**Status:** In Review  
**Roadmap stage / slice:** SLICE-00  
**Owner:** Codex  
**Requested by:** Product owner  
**Branch:** `feat/ody-s00-003-module-test-skeleton`  
**Pull request:** Not opened  
**ExecPlan:** `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`  
**Created:** 2026-08-10  
**Last updated:** 2026-08-10 16:23 UTC

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
- `DotNet/Odyssey.Core.sln` does not exist yet.
- Only `scripts/check-repository-policy.ps1` exists as a repository command in the merged ODY-S00-002 state; broader canonical scripts are future Slice-00 deliverables.

### Assumptions

- The implementation branch exists after owner merge of the ODY-S00-002 closure PR.
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
  - `Assets/Odyssey/Client/Runtime/Odyssey.Unity.Client.Runtime.asmdef` file with assembly field `"name": "Odyssey.Unity.Client"`
  - `Assets/Odyssey/Client/Editor/Odyssey.Unity.Client.Editor.asmdef` file with assembly field `"name": "Odyssey.Unity.Client.Editor"`
- Pure .NET skeleton:
  - `global.json` with the exact installed approved .NET 10 LTS SDK and ADR-006 feature-band roll-forward policy.
  - `Directory.Build.props` with only shared .NET build settings required by this skeleton.
  - `DotNet/Odyssey.Core.sln`
  - ADR-006 bridge/project structure for single-source Core compilation:
    - `DotNet/Projects/Odyssey.Domain.csproj`
    - `DotNet/Projects/Odyssey.Rules.csproj`
    - `DotNet/Projects/Odyssey.Content.csproj`
    - `DotNet/Projects/Odyssey.Application.csproj`
    - `DotNet/Tests/Odyssey.Tests.Unit/`
    - `DotNet/Tests/Odyssey.Tests.Domain/`
    - `DotNet/Tests/Odyssey.Tests.Contracts/`
    - `DotNet/Tests/Odyssey.Tests.Architecture/`
  - Pure .NET bridge projects are created only for `Odyssey.Domain`, `Odyssey.Rules`, `Odyssey.Content`, and `Odyssey.Application`.
  - The four pure .NET bridge projects target `netstandard2.1` and include physical production source from:
    - `Packages/com.odyssey.domain/Runtime/**/*.cs`
    - `Packages/com.odyssey.rules/Runtime/**/*.cs`
    - `Packages/com.odyssey.content/Runtime/**/*.cs`
    - `Packages/com.odyssey.application/Runtime/**/*.cs`
  - Production source is not copied into `DotNet/`.
  - Do not create `Odyssey.Persistence.csproj`, `Odyssey.Networking.csproj`, `Odyssey.Tests.Persistence`, or `Odyssey.Tests.Networking` unless ADR-006 requires them for SLICE-00.
- Test projects:
  - `Odyssey.Tests.Unit`
  - `Odyssey.Tests.Domain`
  - `Odyssey.Tests.Contracts`
  - `Odyssey.Tests.Architecture`
  - `Odyssey.Tests.Unity.EditMode`
  - `Odyssey.Tests.Unity.PlayMode`
- Automatic architecture guard for ADR-001 dependency direction.
- Repository scripts that belong directly to module/test skeleton verification, such as `scripts/restore.ps1`, `scripts/verify-format.ps1`, `scripts/test-fast.ps1`, and `scripts/verify-repository.ps1`, if they can perform real checks.
- Real test-structure and Unity validation scripts: `scripts/verify-test-structure.ps1` and `scripts/test-unity.ps1`.

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
Assets/Odyssey/Client/Tests/EditMode/**
Assets/Odyssey/Client/Tests/PlayMode/**
global.json
Directory.Build.props
DotNet/**
Tests/**
scripts/restore.ps1
scripts/verify-format.ps1
scripts/verify-test-structure.ps1
scripts/test-fast.ps1
scripts/test-unity.ps1
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
- Other: Test assemblies, test fixtures, and test helpers must not enter Player assemblies. The Unity Client runtime assembly is `Odyssey.Unity.Client`; `Odyssey.Unity.Client.Runtime` is not a separate production module or architectural responsibility.

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
- Pure .NET bridge projects exist only for Domain, Rules, Content, and Application, and compile source directly from their matching package `Runtime/**/*.cs` paths; they do not copy production files into `DotNet/`.
- `Persistence` and `Networking` do not reference each other.
- Persistence and Networking are created as embedded Unity packages with `package.json` and production `.asmdef`, but no `Odyssey.Persistence.csproj`, `Odyssey.Networking.csproj`, `Odyssey.Tests.Persistence`, or `Odyssey.Tests.Networking` is created unless ADR-006 requires them for SLICE-00.
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
4. `DotNet/Odyssey.Core.sln` and the four Domain/Rules/Content/Application bridge projects compile the matching production source from `Packages/com.odyssey.domain/Runtime/**/*.cs`, `Packages/com.odyssey.rules/Runtime/**/*.cs`, `Packages/com.odyssey.content/Runtime/**/*.cs`, and `Packages/com.odyssey.application/Runtime/**/*.cs` without copying source into `DotNet/`.
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
| `TST-DOTNET-001` | .NET build/test | Domain, Rules, Content, and Application bridge projects compile source directly from their matching package `Runtime/**/*.cs` paths | Pass |
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
| .NET 10 LTS SDK | `10.0.302` / Microsoft installed SDK | Pure .NET test host and pinned SDK selection through `global.json` | Microsoft .NET license terms | ADR-006 and product owner ODY-S00-003 direction |
| Microsoft.NET.Test.Sdk | `18.8.1` / NuGet Gallery `https://www.nuget.org/packages/Microsoft.NET.Test.Sdk` | .NET test execution | MIT | ADR-006 and product owner ODY-S00-003 direction |
| NUnit | `4.6.1` / NuGet Gallery `https://www.nuget.org/packages/NUnit` | Pure .NET test framework | MIT | ADR-006 and product owner ODY-S00-003 direction |
| NUnit3TestAdapter | `6.2.0` / NuGet Gallery `https://www.nuget.org/packages/NUnit3TestAdapter` | NUnit discovery/execution in .NET test host | MIT | ADR-006 and product owner ODY-S00-003 direction |

Do not add production or development dependencies beyond those already approved by ADR-006 and the Technical Development Baseline. Do not add a coverage collector unless a real coverage gate is implemented in this task. Do not add a mocking framework. Do not create `Directory.Packages.props` unless central package version management is explicitly selected and justified in task evidence.

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

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed.
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

ODY-S00-003 implementation is complete and ready for owner review.

### Changed files / areas

- `global.json` pins .NET SDK `10.0.302` with `latestPatch` roll-forward and prerelease disabled.
- `Directory.Build.props` defines shared .NET build settings needed by the skeleton.
- `Packages/com.odyssey.domain/**`, `Packages/com.odyssey.rules/**`, `Packages/com.odyssey.content/**`, `Packages/com.odyssey.application/**`, `Packages/com.odyssey.persistence/**`, and `Packages/com.odyssey.networking/**` create embedded package metadata, runtime `.asmdef`, Unity `.meta`, and internal marker source.
- `Assets/Odyssey/Client/Runtime/**` and `Assets/Odyssey/Client/Editor/**` create Unity Client runtime/editor assembly boundaries with internal markers.
- `Assets/Odyssey/Client/Tests/EditMode/**` and `Assets/Odyssey/Client/Tests/PlayMode/**` create Unity test-only assemblies and smoke tests.
- `DotNet/Odyssey.Core.sln`, `DotNet/Projects/**`, and `DotNet/Tests/**` create the four pure .NET bridge projects and four .NET test projects.
- `scripts/restore.ps1`, `scripts/verify-format.ps1`, `scripts/verify-test-structure.ps1`, `scripts/test-fast.ps1`, `scripts/test-unity.ps1`, and `scripts/verify-repository.ps1` create real repository entry checks.
- `.gitignore`, `scripts/check-repository-policy.ps1`, `Packages/packages-lock.json`, `README.md`, parent task, backlog, and ExecPlan are updated for the new skeleton/status.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet --info` | Failed blocker | Installed SDK is `9.0.308`; no stable .NET 10 SDK is installed. Contract requires STOP before creating `global.json` or solution. |
| `dotnet --list-sdks` | Failed blocker | Output lists only `9.0.308 [C:\Program Files\dotnet\sdk]`. |
| `git --version` | Passed | `git version 2.54.0.windows.1`. |
| `git lfs version` | Passed | `git-lfs/3.7.1 (GitHub; windows amd64; go 1.25.1; git b84b3384)`. |
| PowerShell version | Passed | `5.1.26100.8972`. |
| Unity version / changeset check | Passed | `ProjectSettings/ProjectVersion.txt` records `6000.4.0f1 (8cf496087c8f)`. |
| `where.exe dotnet` after owner SDK install | Passed | `C:\Program Files\dotnet\dotnet.exe`. |
| `dotnet --list-sdks` after owner SDK install | Passed | Installed SDKs include `9.0.308` and `10.0.302`. |
| `dotnet --version` after owner SDK install | Passed | Selected SDK is `10.0.302`. |
| `dotnet --info` after owner SDK install | Passed | SDK `10.0.302`, x64, host/runtime `10.0.10`; no `global.json` exists yet before implementation. |
| `dotnet --version` after `global.json` | Passed | Selected SDK remains `10.0.302`. |
| Sandboxed `.\scripts\restore.ps1` | Failed / environment | Failed on denied access to `C:\Users\alexx\AppData\Roaming\NuGet\NuGet.Config`; rerun with approved escalation passed. |
| Sandboxed `.\scripts\verify-format.ps1` | Failed / environment | `dotnet format` restore failed under sandboxed NuGet access; rerun with approved escalation passed. |
| `.\scripts\restore.ps1` | Passed | Escalated rerun: all projects up-to-date for restore. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\verify-test-structure.ps1` | Passed | `TST-ARCH-001 PASS valid ADR-001 graph passes`; `TST-ARCH-002 PASS invalid dependency fixture failed with exit code 1`. |
| `.\scripts\test-fast.ps1` | Passed | Guard passed; restore passed; build completed with `0` warnings and `0` errors; .NET tests passed `4/4`, failed `0`, skipped `0`. |
| `.\scripts\test-unity.ps1` | Passed | Unity batch compile exit `0`; EditMode exit `0`, `total=1 passed=1 failed=0 skipped=0`; PlayMode exit `0`, `total=1 passed=1 failed=0 skipped=0`. Logs/results under `Logs/ODY-S00-003/`. |
| `.\scripts\verify-repository.ps1` | Passed | Repository policy passed; architecture guard passed; SDK `10.0.302` check passed. |
| `.\scripts\check-repository-policy.ps1` | Passed | `REPO-POLICY-001` through `REPO-POLICY-004` PASS. |
| `dotnet build DotNet/Odyssey.Core.sln` | Passed | Build completed with `0` warnings and `0` errors. |
| `dotnet test DotNet/Odyssey.Core.sln` | Passed | Four .NET test projects ran one test each; total passed `4`, failed `0`, skipped `0`. |
| `git diff --check` | Passed | Exit code `0`; no whitespace errors after reverting unintended Unity `ProjectSettings.asset` reserialization. |
| Source inventory checks | Passed | No production source under `DotNet/Projects` outside generated `bin/obj`; only four bridge projects exist; no Persistence/Networking bridge projects; no Core `UnityEngine` references. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | Six embedded production packages exist with `package.json`, runtime `.asmdef`, `.meta`, and internal marker source. |
| AC-2 | Passed | `verify-test-structure.ps1` validates package, `.asmdef`, and `.csproj` graphs against ADR-001, including no production-to-test references. |
| AC-3 | Passed | Unity Client runtime/editor asmdefs exist; runtime assembly name is `Odyssey.Unity.Client`; editor assembly is Editor-only. |
| AC-4 | Passed | `DotNet/Odyssey.Core.sln` contains only Domain/Rules/Content/Application bridge projects compiling source from package `Runtime/**/*.cs`. |
| AC-5 | Passed | Four .NET test projects and Unity EditMode/PlayMode test-only assemblies exist; Unity test asmdefs include `TestAssemblies`. |
| AC-6 | Passed | Architecture guard detects invalid dependency fixture with non-zero exit, cycles, forbidden edges, dependency parity, orphan/duplicate source, production-to-test, and Core UnityEngine references. |
| AC-7 | Passed | Repository scripts run real checks and failed during development on real defects/sandbox blockers before final pass. |
| AC-8 | Passed | No gameplay/domain behavior, persistence/network implementation, runtime composition, serialization contracts, GitHub Actions, Player build automation, or unrelated Unity/package/version changes were introduced. |
| AC-9 | Passed | Required validation commands are recorded with real pass/fail evidence. |

### Build and artifact evidence

- Build identity: Not created.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: `Logs/ODY-S00-003/editmode-results.xml`, `Logs/ODY-S00-003/playmode-results.xml`, and Unity logs under `Logs/ODY-S00-003/`.

### Known limitations

- Unity result/log files live under ignored `Logs/ODY-S00-003/` and are recorded as local evidence, not tracked artifacts.
- Windows Player build, IL2CPP smoke, BuildIdentity, GitHub Actions, serialization/AOT, runtime composition, gameplay, persistence implementation, and networking implementation remain out of scope.

### Follow-up tasks

- ODY-S00-004 remains Draft and must not be activated until owner review/merge of ODY-S00-003.

### Self-review summary

- Scope review: Diff is limited to module/test skeleton, scripts, package lock, narrow `.gitignore` exceptions, and status/evidence docs.
- Architecture review: ADR-001 dependency graph is encoded in package, `.asmdef`, `.csproj`, and automated guard checks; no new architecture rule is introduced.
- Test review: .NET, architecture, Unity EditMode, and Unity PlayMode checks all run with nonzero test counts; zero-test success is not claimed.
- Security/privacy review: Contract excludes private docs, secrets, local handoffs, and hidden campaign/product content.
- Documentation/version review: No ADR, baseline, Unity package, or version amendment is authorized by this task.

## 18. Blockers, decisions, and change control

### Blockers

- None currently. Initial stable .NET 10 SDK blocker was resolved by owner-installed SDK `10.0.302`.

### Decisions made during execution

- 2026-08-10 — Activate ODY-S00-003 only as `Ready` during ODY-S00-002 post-merge closure; do not begin implementation until the closure PR is merged — Authority / approval: product owner instruction.
- 2026-08-10 — ODY-S00-003 preflight must stop and set task status to Blocked when stable .NET 10 SDK is absent; do not install SDK or switch major versions automatically — Authority / approval: product owner instruction.
- 2026-08-10 — Owner resolved the .NET 10 blocker by installing stable .NET SDK `10.0.302` x64; resume ODY-S00-003 on existing branch without rewriting blocked evidence — Authority / approval: product owner instruction.

### Approved task changes

- None.
