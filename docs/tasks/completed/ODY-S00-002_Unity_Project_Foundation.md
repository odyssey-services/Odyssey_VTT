# ODY-S00-002 — Create the Unity Project Foundation

**Status:** Done
**Roadmap stage / slice:** SLICE-00
**Owner:** Codex
**Requested by:** Product owner
**Branch:** `feat/ody-s00-002-unity-project-foundation`
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/4
**ExecPlan:** `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
**Created:** 2026-08-01
**Last updated:** 2026-08-10 14:21 UTC

## 1. Goal

Create the exact repository-owned Unity `6000.4.0f1` HDRP project foundation required by ADR-009, including pinned packages/settings, UI Toolkit and Input System baselines, project-owned quality assets, and the minimal `Bootstrap`/`AppShell` scenes. The project must open, import, and compile cleanly without introducing Core business behavior.

## 2. Why this task exists

- Problem or dependency being addressed: Repository policy is complete, but no Unity project exists.
- Value or risk reduction: Pins the Editor and package graph before Core modules or gameplay code are introduced.
- Blocking or enabling relationship: Depends on completed `ODY-S00-001`; blocks `ODY-S00-003` and all later Unity work.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.8.md`
- `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.3.md`, sections 5–6, 13, 22, 30–32
- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/TASK_TEMPLATE.md`
- `docs/tasks/SLICE-00_BACKLOG.md`
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`
- `docs/adr/ADR-005_Dependency_Composition_v1.0.md`, Unity bootstrap/lifecycle constraints
- `docs/adr/ADR-006_Test_Project_Structure_and_Dual_Unity_DotNet_Compilation_v1.0.md`, Unity test-boundary constraints
- `docs/adr/ADR-007_Versioning_and_Build_Identity_v1.0.md`, version-source boundaries only
- `docs/adr/ADR-009_Unity_Project_and_Build_Baseline_v1.1.md`, sections 1, 5–15, 22, 24, 27–28

### Requirement and test IDs

- Requirement IDs: `SLICE-00`, Milestone `M1`, delivery group `PR-001`, `TDB-DEC-002–TDB-DEC-007`, `TDB-DEC-022–TDB-DEC-024`
- Existing test IDs: `TST-UNI-001–TST-UNI-014` as applicable to the project foundation
- New test IDs to introduce: None unless an acceptance gap is discovered and recorded before implementation

### Task-safe private context

- Approved summary / references: Create only the technical Unity foundation. No gameplay, campaign content, private product document body, or hidden data is required.

## 4. Verified current state

### Verified facts

- `ODY-S00-001` is complete through merged PR #1, merge commit `9c7a61893b107624c29ecaa0af34335a715b11e3`.
- The repository contains no `Assets/`, `Packages/`, `ProjectSettings/`, Unity project, C# production code, .NET solution, or GitHub Actions workflow.
- ADR-009 pins Unity `6000.4.0f1` with revision `8cf496087c8f`, HDRP, UI Toolkit, Input System New-only, Windows x64, and D3D12 before D3D11.

### Assumptions

- A licensed Windows x64 installation of Unity `6000.4.0f1` and Windows Build Support (IL2CPP) will be available before this task moves to `In Progress`; verify and record the actual executable/module state first.
- Exact package versions will come from the official HDRP template resolved by the pinned Editor and will be committed in `manifest.json` and `packages-lock.json`; do not guess them in advance.

## 5. Scope

### In scope

- Create the Unity project at the repository root using the official HDRP template compatible with `6000.4.0f1`.
- Commit `Assets/`, `Packages/manifest.json`, `Packages/packages-lock.json`, `ProjectSettings/`, and required `.meta` files.
- Pin `ProjectVersion.txt` to `6000.4.0f1 (8cf496087c8f)`.
- Keep only approved official Unity packages required for HDRP, UI Toolkit, Input System, and Unity Test Framework; explain the resolved graph.
- Configure HDRP as the only render pipeline with Odyssey-owned Low/Medium/High assets and compatible global/quality/volume settings.
- Configure Windows Standalone x86-64, D3D12 followed by D3D11, Auto Graphics API disabled, linear color space, visible meta files, and force-text asset serialization.
- Configure Input System Package (New) only and create the minimal Odyssey input asset required by ADR-009.
- Create project-owned UI Toolkit panel/settings assets and a minimal AppShell visual root without product UI behavior.
- Create `Assets/Odyssey/Client/Scenes/Bootstrap.unity` at build index 0 and `AppShell.unity`; preserve one runtime-root design without implementing Application/Core services.
- Add the minimum EditMode/configuration checks needed to prove applicable `TST-UNI-001–014` settings when feasible in this task.
- Record clean open/import/compile evidence and package/settings inspection evidence.

### Out of scope

- Domain, Rules, Content, Application, Persistence, or Networking implementation.
- Embedded `Packages/com.odyssey.*` production modules beyond empty path planning; module/test skeleton belongs to `ODY-S00-003`.
- Gameplay, campaign state, persistence, networking, accounts, permissions, maps, characters, combat, dice, content tools, chat, or audio.
- Service locator, static `Instance`, DI framework, authoritative state, or real runtime composition.
- GitHub Actions, CI required checks, BuildIdentity, `version.json`, `config/compatibility.json`, release tags, or artifact publishing.
- Windows Player build, Release-Candidate/Release build, IL2CPP smoke, performance certification, installer, or updater; later Slice-00 tasks own those gates.
- Unity/package/Editor version changes beyond the exact accepted baseline.
- Addressables, localization, VFX Graph unless required transitively by the approved HDRP graph, UI Toolkit Test Framework, third-party packages, paid assets, or external registries.

### Allowed paths

```text
Assets/**
Packages/manifest.json
Packages/packages-lock.json
ProjectSettings/**
scripts/**                         # only a minimal Unity validation entry point if required and approved by the task
docs/tasks/completed/ODY-S00-002_Unity_Project_Foundation.md
docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md
docs/tasks/SLICE-00_BACKLOG.md
THIRD_PARTY_NOTICES.md             # only if the resolved graph requires a notice update
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.3.md
ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.8.md
.github/workflows/**
version.json
config/compatibility.json
DotNet/**
Any application/schema/format/contract/protocol/ruleset version source
```

## 6. Technical constraints

- Module ownership and dependency direction: Follow ADR-001; this task does not create Core ownership or bypass it through Assets.
- Authoritative-state and transaction boundary: No authoritative campaign state or Application command handling is introduced.
- Serialization / compatibility boundary: Force-text Unity asset serialization only; no production JSON/domain serialization contract.
- Time / RNG rule: No authoritative time or randomness logic.
- Unity / thread / lifetime rule: ADR-005 and ADR-009; one future composition root, `Bootstrap` index 0, `AppShell` loaded by Bootstrap, no service locator or parallel singleton graph.
- Dependency / licensing rule: Official pinned Unity registry packages only. No preview/experimental, floating, Git, disk, tarball, scoped-registry, unsigned, or third-party package without a separately approved decision.
- Security / privacy / redaction rule: No credentials, Unity license data, absolute local paths, private product documents, paid assets, or hidden gameplay data.
- Performance or platform constraint: Windows 10/11 x64, 1920×1080 baseline, HDRP with Low/Medium/High project-owned profiles, D3D12 then D3D11.
- Other: Do not update the accepted Editor patch or package graph as incidental cleanup.

## 7. Expected behavior

### Scenario 1 — Clean Unity import

**Given** a clean checkout and licensed Unity `6000.4.0f1`
**When** the project is opened or imported in batch mode
**Then** the pinned package graph resolves without signature/version drift and the project compiles with no Unity Console errors.

### Scenario 2 — Minimal startup scenes

**Given** the project build settings
**When** scenes are inspected
**Then** `Bootstrap.unity` is build index 0, `AppShell.unity` is present, and no second runtime root or business behavior exists.

### Scenario 3 — Configuration drift

**Given** an incompatible Editor, preview package, legacy/Both input handling, Auto Graphics API, wrong graphics order, or non-HDRP pipeline
**When** applicable configuration validation runs
**Then** validation fails with a clear technical reason.

### Required invariants

- Unity Editor remains exactly `6000.4.0f1 (8cf496087c8f)`.
- HDRP is the only render pipeline; UI Toolkit and Input System New-only are the only runtime UI/input baselines.
- Package versions are pinned in both manifest and lock files.
- No Core source is duplicated under `Assets/`.
- No gameplay/runtime feature or authoritative state is introduced.

## 8. Deliverables

- Production code: Minimal Unity Client scaffolding only where required for scene/UI startup; no business logic.
- Tests: Applicable EditMode/configuration checks for `TST-UNI-001–014`.
- Scripts / CI: Minimal local Unity validation entry point only if needed; no GitHub Actions.
- Configuration: Pinned Unity project, package graph, HDRP/UI Toolkit/Input System, Windows graphics/player/editor settings.
- Documentation: Task completion evidence and parent ExecPlan progress update.
- Generated evidence or build artifacts: Unity Editor/package/import/compile logs only; no Player build artifact.
- Migration / recovery material: Not applicable.

## 9. Acceptance criteria

1. `ProjectSettings/ProjectVersion.txt` records `6000.4.0f1` and `8cf496087c8f`.
2. `Packages/manifest.json` and `Packages/packages-lock.json` are parseable, fully pinned, use approved sources, and contain no preview/experimental dependency.
3. HDRP is the active render pipeline and Low/Medium/High use project-owned compatible HDRP assets/settings.
4. UI Toolkit project-owned panel/settings assets and a minimal AppShell root exist without uGUI parallel baseline or product UI behavior.
5. Input System is New-only; legacy Input Manager is not used by new code; the minimal input asset is present.
6. Windows target is Standalone x86-64; Auto Graphics API is disabled; D3D12 precedes D3D11.
7. Visible Meta Files and Force Text are enabled, required `.meta` files are tracked, and generated/local Unity directories remain ignored.
8. `Bootstrap.unity` is build index 0, `AppShell.unity` is present, and the project contains no second runtime root, service locator, or authoritative state.
9. A clean Editor open/import/compile with Unity `6000.4.0f1` completes without compiler, package restore/signature, missing asset, or scene/settings errors.
10. Applicable `TST-UNI-001–014` checks pass or each unavailable automation point has honest manual evidence; no Player/IL2CPP success is claimed.
11. The diff contains no Core module implementation, gameplay/runtime feature, GitHub Actions workflow, BuildIdentity/version source, private document, credential, paid asset, or unapproved dependency.
12. Required repository policy and diff checks pass, validation evidence is recorded, and owner review is obtained before merge.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TST-UNI-001` | EditMode/config inspection | Exact Editor version and revision | Pass |
| `TST-UNI-002–003` | EditMode/config inspection | Manifest/lock parse and no preview/experimental packages | Pass |
| `TST-UNI-006–007` | EditMode/config inspection | HDRP active; Low/Medium/High project-owned assets | Pass |
| `TST-UNI-010` | EditMode/config inspection | D3D12 precedes D3D11 and Auto Graphics API is disabled | Pass |
| `TST-UNI-013` | EditMode/config inspection | Input System Package (New) only | Pass |
| `TST-UNI-014` | EditMode/config inspection | Bootstrap build index 0 and AppShell present | Pass |
| `REPO-POLICY-001–004` | PowerShell/Git attributes | Repository foundation rules remain valid | Pass |

### Required commands

Use repository entry points if they exist when implementation begins. At contract creation only `scripts/check-repository-policy.ps1` exists; do not claim missing scripts were run.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/check-repository-policy.ps1
git diff --check
git status --short
git ls-files
# Record the actual Unity 6000.4.0f1 batch-mode open/import/compile command used on the implementation machine.
# Run ./scripts/test-unity.ps1 only if ODY-S00-002 introduces it or it exists by validation time.
```

### Manual validation

- Verify Unity Hub/Editor shows `6000.4.0f1` and Windows Build Support (IL2CPP) is installed before implementation.
- Inspect package sources, signatures/warnings, manifest/lock diff, build settings, graphics API order, HDRP quality bindings, UI Toolkit panel settings, Input System mode, and scene list.
- Review Unity Console and Editor log for compiler/package/import errors.
- Review the complete PR diff for generated caches, absolute paths, private data, unexpected packages, or product behavior.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64
- Unity editor or Player profile: Unity `6000.4.0f1 (8cf496087c8f)`
- Scripting backend: Editor/Mono compile path; Windows IL2CPP module presence verified, but Player build not required
- Network topology or database fixture: None
- Other: Official Unity registry/package restore access

### Validation not required by this task

- Windows Player Mono or IL2CPP build/smoke; owned by later Slice-00 build tasks and required before M1.
- Pure .NET restore/build/test; projects are created by ODY-S00-003.
- PlayMode lifecycle/functional tests beyond minimal scene/config evidence.
- GitHub Actions/required checks, BuildIdentity, serialization/AOT vectors, performance profiling, installer, or release publication.

## 11. Compatibility, migration, and rollback

- Compatibility impact: Establishes the initial Unity project/package/settings baseline; no prior Unity project or user data exists.
- Version fields affected: `ProjectVersion.txt` and Unity-managed project/package versions only; no application/schema/format/contract/protocol/ruleset version.
- Migration or upcaster: Not applicable.
- Forward / backward behavior: The project must be reopened only with the pinned Editor; later patch/package upgrades require their own approved PR.
- Rollback method: Close the unmerged PR or revert the complete Unity foundation PR, including manifest, lock, settings, assets, scenes, and meta files together.
- Data-loss risk and protection: No campaign/user data. Protect repository history from generated caches, local settings, license data, secrets, and proprietary assets.
- Recovery rehearsal required: Clean checkout/open/import/compile with the pinned Editor.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| Unity Editor | `6000.4.0f1`, official Unity distribution | Project creation/import | Unity terms | ADR-009 |
| Unity HDRP and required official packages | Exact versions resolved by pinned Editor/template and lock file | Render/UI/input/test baseline | Unity package licenses | ADR-009 |

Any resolved package not covered by ADR-009 must stop implementation for owner approval and license/provenance review before commit.

## 13. Security, privacy, and hidden information

- Data classes handled: Repository-safe technical settings, Unity YAML/assets, package metadata, validation logs.
- Trust boundaries: Unity Hub/Editor and official registry → local project → Private authoritative Git repository.
- Authorization / audience checks: No product authorization runtime.
- Redaction requirements: Exclude Unity credentials/licenses, machine/user names, absolute local paths, private product text, tokens, proprietary/paid assets, and diagnostic dumps containing such data.
- Log-safe fields: Relative project paths, package IDs/versions, test IDs, Editor version/revision, safe error categories.
- Abuse / malformed input limits: Package graph/signature validation; no untrusted content import.
- Security tests: Repository policy, tracked-path review, package source/signature inspection, secret/private-path scan.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: Unity project creation changes packages, project-wide settings, scenes, assets, and toolchain-bound validation across multiple logical stages.
- ExecPlan path: `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
- Expected pull request count: 1
- Milestone or sequencing constraints: Begin only after verifying the pinned Unity installation; complete before ODY-S00-003.

## 15. Documentation and versioning impact

- Documents that must change: ADR-009 v1.1, Technical Development Baseline v0.3, Active Documentation Baseline v1.8, this task completion evidence, parent ExecPlan progress, Slice-00 backlog status, AGENTS.md, and ADR README.
- Documents that must not change: private product documents. ADR-009 v1.0, Technical Development Baseline v0.2, and Active Documentation Baseline v1.7 remain historical records; current normative references are amended by owner approval dated 2026-08-10.
- Application version change: No — version source is deferred.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: approved material change on 2026-08-10: ADR-009 v1.0 → v1.1, Technical Development Baseline v0.2 → v0.3, Active Documentation Baseline v1.7 → v1.8.
- Changelog or release-note requirement: Task/ExecPlan evidence only; no user-facing release note.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated and manual validation is complete with real evidence.
- [x] Clean Unity `6000.4.0f1` open/import/compile passes.
- [x] Package graph, licenses/sources, settings, scenes, and generated-file exclusions are reviewed.
- [x] Architecture boundaries and no-authoritative-state invariant remain valid.
- [x] No unapproved dependency, package source, tool, asset, or license is introduced.
- [x] No Unity/package/version drift or unrelated cleanup is included.
- [x] Security/privacy and repository policy checks pass.
- [x] Complete diff and Unity YAML/meta integrity are reviewed.
- [x] Task, backlog, and ExecPlan evidence are updated honestly.
- [x] Owner reviews and merges; Codex does not merge into `main`.

## 17. Completion evidence

Unity `6000.4.0f1 (8cf496087c8f)` is now the formal repository baseline through ADR-009 v1.1, Technical Development Baseline v0.3, and Active Documentation Baseline v1.8. The Unity foundation was copied from `D:\Game_Dev\Odyssey_VTT\Odyssey_VTT` into the authoritative repository using only `Assets/`, `Packages/`, and `ProjectSettings/`.

Owner merged PR #4 into `main` using the GitHub merge-commit method.

- Pull request: https://github.com/odyssey-services/Odyssey_VTT/pull/4
- Merge commit: `70e7d49e217d4aecb7a2e873d31787d26001f47f`
- Merge date: `2026-08-10T16:21:33+02:00` (`2026-08-10 14:21:33 UTC`)
- Merge method: GitHub merge commit
- Final accepted Unity baseline: Unity `6000.4.0f1`, changeset `8cf496087c8f`, HDRP `17.4.0`, Input System `1.19.0`

### Changed files / areas

- `Assets/Odyssey/**`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- `ProjectSettings/**`
- `docs/adr/ADR-009_Unity_Project_and_Build_Baseline_v1.1.md`
- `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.3.md`
- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.8.md`
- `AGENTS.md`
- `docs/adr/README.md`
- `docs/tasks/completed/ODY-S00-002_Unity_Project_Foundation.md`
- `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
- `docs/tasks/SLICE-00_BACKLOG.md`

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| Post-merge sync with `origin/main` | Passed | Local `main` fast-forwarded to `origin/main` at merge commit `70e7d49e217d4aecb7a2e873d31787d26001f47f`; worktree was clean before creating closure branch `chore/ody-s00-002-complete`. |
| Branch sync with `origin/main` | Passed | Local branch `feat/ody-s00-002-unity-project-foundation` fast-forwarded to latest `origin/main` before implementation. No merge from main was performed after task work began. |
| Unity project copy scope | Passed | Only `Assets/`, `Packages/`, and `ProjectSettings/` were copied into `D:\Documents\Odyssey_VTT`; external `.git`, `Library`, `Logs`, and `UserSettings` were not copied. |
| Unity first batchmode attempt via `Start-Process -Wait` | Failed / stopped | No log was created and the process remained idle; it was stopped manually. Recorded process exit was `-1`. |
| Unity sandboxed direct batchmode attempt | Failed / stopped | Unity failed before import on local Unity cache access (`CurlRequestCache.db`) and printed a crash stack; the process was stopped. This was an environment/sandbox startup failure, not a project compile result. |
| Escalated Unity first repository import | Completed | Unity created the repository `Library`, resolved packages, and completed initial AssetDatabase import. |
| Final Unity batchmode open/import/compile after review corrections | Passed | `UnityExitCode=0`; final log `Logs/OdysseyRepositoryReviewFixValidation.log` ends with `return code 0`. |
| Unity final log error scan after review corrections | Passed | `error_pattern_matches=0` for compiler/package/import/missing-reference failure patterns, including missing UIDocument panel references. |
| `ProjectSettings/ProjectVersion.txt` | Passed | Records `m_EditorVersion: 6000.4.0f1` and `m_EditorVersionWithRevision: 6000.4.0f1 (8cf496087c8f)`. |
| Manifest / lock parse and package source check | Passed | `manifest_dependency_count=37`, `bad_manifest_dependencies=0`, `bad_lock_sources=0`. HDRP `17.4.0`, Input System `1.19.0`, Test Framework `1.6.0`; no URP or `com.unity.2d.*` in lock. |
| Root package check | Passed | Root packages include `com.unity.render-pipelines.high-definition 17.4.0`, `com.unity.inputsystem 1.19.0`, `com.unity.test-framework 1.6.0`, and Unity built-in modules. |
| Render pipeline / graphics settings | Passed | `GraphicsSettings.asset` maps `UnityEngine.Rendering.HighDefinition.HDRenderPipeline`; Windows graphics APIs are `D3D12` then `D3D11` with `m_Automatic: 0`. `templateDefaultScene`, `cloudProjectId`, and `organizationId` are empty. |
| Quality settings | Passed | Quality profiles are `High`, `Medium`, `Low`; current/default Standalone quality index is `1` (`Medium`); project-owned HDRP assets exist for High/Medium/Low. |
| UI Toolkit binding | Passed | `AppShell.unity` UIDocument `m_PanelSettings` references `OdysseyPanelSettings.asset` GUID `2677362e67e332f45b94106f6a8ddb28`; source UXML remains bound. |
| Minimal Input System asset | Passed | `Odyssey.inputactions` contains only `UI` map with `Navigate`, `Submit`, `Cancel`, `Point`, `Click`, and `ScrollWheel`; template/gameplay action scan returned `template_gameplay_matches=0`; `activeInputHandler: 1`. |
| Scene build settings | Passed | `Bootstrap.unity` is first enabled scene; `AppShell.unity` is second enabled scene. |
| Player identity and resolution baseline | Passed | `companyName: Odyssey`, `productName: Odyssey VTT`, Standalone `applicationIdentifier: com.odyssey.vtt`, default resolution `1920x1080`, native resolution off, HDR display off, dynamic resolution mode off. |
| Release terminology / baseline docs | Passed | ADR-009 v1.1 and TDB v0.3 use Unity 6.4 Update release / Supported release, not LTS; ADR release date records `18 March 2026`; Active Baseline historical v1.7/v1.1 entries restored to v0.2 / ADR-009 v1.0 + Unity 6000.3.20f1. |
| No C# / `.asmdef` in `Assets` | Passed | Recursive file scan returned no `.cs` and no `.asmdef` under `Assets`. |
| Force Text / Visible Meta | Passed | `m_SerializationMode: 2`; `m_Mode: Visible Meta Files`. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-repository-policy.ps1` | Passed | REPO-POLICY-001 through REPO-POLICY-004 PASS; repository policy check passed. |
| Controlled negative fixture | Passed | Forced intent-to-add `Documentation/private.md` was rejected with `REPO-POLICY-002 FAIL`; script exit was `1`; fixture was then removed from index and disk. |
| `git diff --check` | Passed | Exit code 0; no whitespace errors. |
| `git status --short --branch` | Passed with environment warning | Shows expected modified/untracked task, docs, Unity project files; Git also warns that global user ignore is not accessible. |
| `git ls-files --others --exclude-standard` | Passed | Lists expected untracked Unity project files and new baseline docs before staging. |
| Generated path absence check | Passed | No tracked paths matched `Library`, `Temp`, `Obj`, `Build`, `Builds`, `Logs`, `UserSettings`, `MemoryCaptures`, `Recordings`, or `artifacts`. |
| Ignored generated directories | Passed | `Library/`, `Logs/`, and Unity-generated `UserSettings/` are ignored and untracked after validation. |
| `git lfs ls-files` | Passed | No LFS objects are currently tracked. |
| `git check-attr filter` LFS samples | Passed | `sample.psd` and `sample.wav` resolve to `filter: lfs`; source/Markdown/JSON/Unity YAML/meta/UI samples are `filter: unspecified`. |
| Existing repository script inventory | Passed | Only `scripts/check-repository-policy.ps1` exists. Canonical later scripts do not exist yet. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | ProjectVersion records `6000.4.0f1 (8cf496087c8f)`. |
| AC-2 | Passed | Manifest/lock parse, pinned package versions, approved sources, no preview/experimental/URP/2D packages. |
| AC-3 | Passed | HDRP is active; High/Medium/Low project-owned HDRP assets exist. |
| AC-4 | Passed | UI Toolkit assets exist and `AppShell.unity` UIDocument references `OdysseyPanelSettings.asset` by GUID; no root uGUI package baseline or product UI behavior added. |
| AC-5 | Passed | Input System New-only setting is active and `Odyssey.inputactions` is UI-only; template/gameplay `Player`, movement, combat, XR/touch/joystick actions were removed and verification returned `template_gameplay_matches=0`. |
| AC-6 | Passed | Windows Standalone graphics order D3D12 then D3D11; Auto Graphics API disabled. |
| AC-7 | Passed | Visible Meta Files and Force Text enabled; `.meta` files present; generated/local Unity dirs ignored. |
| AC-8 | Passed | Bootstrap index 0 and AppShell index 1; no C# runtime root, service locator, or authoritative state introduced. |
| AC-9 | Passed | Final Unity batchmode open/import/compile after review corrections returned `UnityExitCode=0`. |
| AC-10 | Passed with manual/config evidence | Applicable `TST-UNI-001` through `TST-UNI-014` configuration checks passed by file/log inspection; no Player/IL2CPP success is claimed. |
| AC-11 | Passed | Diff contains no Core module implementation, gameplay feature, template gameplay input actions, GitHub Actions workflow, BuildIdentity/version source, private document, credential, paid asset, or unapproved dependency. |
| AC-12 | Passed | Repository policy, diff, LFS, generated-path, package, settings, Unity log, and negative fixture checks completed. Owner review and merge completed through PR #4. |

### Build and artifact evidence

- Build identity: Not applicable.
- Player artifact path / name: None.
- Checksums: None.
- Test or quality report: Unity Editor logs only; no Player build report.

### Known limitations

- Windows Player build and IL2CPP smoke were not run; they are out of scope for ODY-S00-002.
- `Library/`, `Logs/`, and `UserSettings/` exist locally after Unity validation but are ignored and not part of the review diff.
- Only `scripts/check-repository-policy.ps1` exists in this repository state; broader canonical scripts are future Slice-00 deliverables.

### Follow-up tasks

- ODY-S00-003 is activated as `Ready` by the post-merge closure PR and begins only after that closure PR is owner-reviewed and merged.

### Self-review summary

- Scope review: Unity foundation only; no C#/.asmdef/Core module work added.
- Architecture review: ADR-009 v1.1 records the Unity baseline amendment; ADR-001/005/006 boundaries remain untouched.
- Test review: Required project/package/settings checks, minimal input scan, UIDocument binding check, Player identity/resolution check, and Unity batchmode compile passed; deferred build/CI checks are listed honestly.
- Security/privacy review: No private product docs, credentials, paid assets, external registries, or generated cache directories are tracked.
- Documentation/version review: ADR-009 v1.1, TDB v0.3, Active Baseline v1.8, task, ExecPlan, backlog, AGENTS, and ADR README were updated for the owner-approved Unity 6000.4 baseline.

## 18. Blockers, decisions, and change control

### Blockers

- None. ODY-S00-002 was owner-reviewed and merged through PR #4.

### Decisions made during execution

- 2026-08-01 — Activate only the ODY-S00-002 contract after ODY-S00-001 merge; do not create Unity project in the post-merge closure PR — Authority / approval: product owner instruction.
- 2026-08-10 — Unity `6000.3` versus `6000.4` is acceptable for the development process; `6000.4.0f1` may be used for local scaffold/import validation in ODY-S00-002 if the actual version/revision is recorded. HDRP, UI Toolkit, Input System New-only, Windows x64, D3D12→D3D11, Force Text, Visible Meta Files, and no generated/cache directories remain required.
- 2026-08-10 — Owner direction superseded local tolerance with a formal baseline amendment: preserve Unity `6000.4.0f1 (8cf496087c8f)`, document ADR-009 v1.1, TDB v0.3, and Active Baseline v1.8 before closing ODY-S00-002.

### Approved task changes

- 2026-08-10 — ADR-009 v1.1 supersedes ADR-009 v1.0 only for the Unity Editor/package baseline; repository baseline is Unity `6000.4.0f1 (8cf496087c8f)` with HDRP `17.4.0`.
