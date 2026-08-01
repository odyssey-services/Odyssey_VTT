# ODY-S00-002 — Create the Unity Project Foundation

**Status:** Ready  
**Roadmap stage / slice:** SLICE-00  
**Owner:** Unassigned  
**Requested by:** Product owner  
**Branch:** Not created  
**Pull request:** Not opened  
**ExecPlan:** `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`  
**Created:** 2026-08-01  
**Last updated:** 2026-08-01 19:26 UTC

## 1. Goal

Create the exact repository-owned Unity `6000.3.20f1` HDRP project foundation required by ADR-009, including pinned packages/settings, UI Toolkit and Input System baselines, project-owned quality assets, and the minimal `Bootstrap`/`AppShell` scenes. The project must open, import, and compile cleanly without introducing Core business behavior.

## 2. Why this task exists

- Problem or dependency being addressed: Repository policy is complete, but no Unity project exists.
- Value or risk reduction: Pins the Editor and package graph before Core modules or gameplay code are introduced.
- Blocking or enabling relationship: Depends on completed `ODY-S00-001`; blocks `ODY-S00-003` and all later Unity work.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.7.md`
- `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.2.md`, sections 5–6, 13, 22, 30–32
- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/TASK_TEMPLATE.md`
- `docs/tasks/SLICE-00_BACKLOG.md`
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`
- `docs/adr/ADR-005_Dependency_Composition_v1.0.md`, Unity bootstrap/lifecycle constraints
- `docs/adr/ADR-006_Test_Project_Structure_and_Dual_Unity_DotNet_Compilation_v1.0.md`, Unity test-boundary constraints
- `docs/adr/ADR-007_Versioning_and_Build_Identity_v1.0.md`, version-source boundaries only
- `docs/adr/ADR-009_Unity_Project_and_Build_Baseline_v1.0.md`, sections 1, 5–15, 22, 24, 27–28

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
- ADR-009 pins Unity `6000.3.20f1` with revision `c9ba695d4f07`, HDRP, UI Toolkit, Input System New-only, Windows x64, and D3D12 before D3D11.

### Assumptions

- A licensed Windows x64 installation of Unity `6000.3.20f1` and Windows Build Support (IL2CPP) will be available before this task moves to `In Progress`; verify and record the actual executable/module state first.
- Exact package versions will come from the official HDRP template resolved by the pinned Editor and will be committed in `manifest.json` and `packages-lock.json`; do not guess them in advance.

## 5. Scope

### In scope

- Create the Unity project at the repository root using the official HDRP template compatible with `6000.3.20f1`.
- Commit `Assets/`, `Packages/manifest.json`, `Packages/packages-lock.json`, `ProjectSettings/`, and required `.meta` files.
- Pin `ProjectVersion.txt` to `6000.3.20f1 (c9ba695d4f07)`.
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
docs/tasks/active/ODY-S00-002_Unity_Project_Foundation.md
docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md
docs/tasks/SLICE-00_BACKLOG.md
THIRD_PARTY_NOTICES.md             # only if the resolved graph requires a notice update
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.2.md
ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.7.md
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

**Given** a clean checkout and licensed Unity `6000.3.20f1`  
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

- Unity Editor remains exactly `6000.3.20f1 (c9ba695d4f07)`.
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

1. `ProjectSettings/ProjectVersion.txt` records `6000.3.20f1` and `c9ba695d4f07`.
2. `Packages/manifest.json` and `Packages/packages-lock.json` are parseable, fully pinned, use approved sources, and contain no preview/experimental dependency.
3. HDRP is the active render pipeline and Low/Medium/High use project-owned compatible HDRP assets/settings.
4. UI Toolkit project-owned panel/settings assets and a minimal AppShell root exist without uGUI parallel baseline or product UI behavior.
5. Input System is New-only; legacy Input Manager is not used by new code; the minimal input asset is present.
6. Windows target is Standalone x86-64; Auto Graphics API is disabled; D3D12 precedes D3D11.
7. Visible Meta Files and Force Text are enabled, required `.meta` files are tracked, and generated/local Unity directories remain ignored.
8. `Bootstrap.unity` is build index 0, `AppShell.unity` is present, and the project contains no second runtime root, service locator, or authoritative state.
9. A clean Editor open/import/compile with Unity `6000.3.20f1` completes without compiler, package restore/signature, missing asset, or scene/settings errors.
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
# Record the actual Unity 6000.3.20f1 batch-mode open/import/compile command used on the implementation machine.
# Run ./scripts/test-unity.ps1 only if ODY-S00-002 introduces it or it exists by validation time.
```

### Manual validation

- Verify Unity Hub/Editor shows `6000.3.20f1` and Windows Build Support (IL2CPP) is installed before implementation.
- Inspect package sources, signatures/warnings, manifest/lock diff, build settings, graphics API order, HDRP quality bindings, UI Toolkit panel settings, Input System mode, and scene list.
- Review Unity Console and Editor log for compiler/package/import errors.
- Review the complete PR diff for generated caches, absolute paths, private data, unexpected packages, or product behavior.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64
- Unity editor or Player profile: Unity `6000.3.20f1 (c9ba695d4f07)`
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
| Unity Editor | `6000.3.20f1`, official Unity distribution | Project creation/import | Unity terms | ADR-009 |
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

- Documents that must change: This task completion evidence, parent ExecPlan progress, and Slice-00 backlog status.
- Documents that must not change: Accepted ADR, Technical Development Baseline v0.2, Active Documentation Baseline v1.7, private product documents.
- Application version change: No — version source is deferred.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: None.
- Changelog or release-note requirement: Task/ExecPlan evidence only; no user-facing release note.

## 16. Definition of Done

- [ ] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [ ] Required automated and manual validation is complete with real evidence.
- [ ] Clean Unity `6000.3.20f1` open/import/compile passes.
- [ ] Package graph, licenses/sources, settings, scenes, and generated-file exclusions are reviewed.
- [ ] Architecture boundaries and no-authoritative-state invariant remain valid.
- [ ] No unapproved dependency, package source, tool, asset, or license is introduced.
- [ ] No Unity/package/version drift or unrelated cleanup is included.
- [ ] Security/privacy and repository policy checks pass.
- [ ] Complete diff and Unity YAML/meta integrity are reviewed.
- [ ] Task, backlog, and ExecPlan evidence are updated honestly.
- [ ] Owner reviews and merges; Codex does not merge into `main`.

## 17. Completion evidence

Not started. Fill with actual implementation and validation results before moving to `In Review`.

### Changed files / areas

- None.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| Repository policy at contract creation | Passed | REPO-POLICY-001–004 PASS; controlled negative fixture rejected with exit 1; `git diff --check` and Git attribute checks passed. |
| Unity validation | Not run | Implementation has not started. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1–AC-12 | Pending | ODY-S00-002 is Ready, not In Progress. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: None.

### Known limitations

- Unity `6000.3.20f1` and Windows Build Support (IL2CPP) availability must be verified before implementation.
- Repository scripts beyond the policy check do not exist yet.

### Follow-up tasks

- ODY-S00-003 begins only after this task reaches Done.

### Self-review summary

- Scope review: Contract only; Unity implementation has not started.
- Architecture review: Authorities and module boundaries named without creating source.
- Test review: Required tests and explicitly deferred checks identified.
- Security/privacy review: Package and local-data boundaries identified.
- Documentation/version review: No authority or application/contract version change.

## 18. Blockers, decisions, and change control

### Blockers

- None for Ready status. Unity installation/module verification is the first required pre-implementation check.

### Decisions made during execution

- 2026-08-01 — Activate only the ODY-S00-002 contract after ODY-S00-001 merge; do not create Unity project in the post-merge closure PR — Authority / approval: product owner instruction.

### Approved task changes

- None.
