# ODY-UI-01-007a — Runtime UI Click Routing Gap Fix

**Status:** Done  
**Roadmap stage / slice:** SLICE-UI-01  
**Owner:** Codex (agent)  
**Requested by:** Product owner  
**Branch:** `fix/ody-ui-01-007a-runtime-ui-click-routing`  
**Pull request:** Initial stacked PR [#75](https://github.com/odyssey-services/Odyssey_VTT/pull/75); final mainline PR [#77](https://github.com/odyssey-services/Odyssey_VTT/pull/77)  
**ExecPlan:** `docs/plans/active/ODY-UI-01-007a_Runtime_UI_Click_Routing_Gap_Fix.md`  
**Created:** 2026-08-28  
**Last updated:** 2026-08-29 20:40 UTC

## 1. Goal

Make real mouse clicks in Unity Play Mode reach UI Toolkit `Button.clicked` handlers in the Developer Shell and composed Trial UI, and cover that path with a PlayMode test that uses Input System pointer events instead of direct handler invocation.

## 2. Why this task exists

- Problem or dependency being addressed: owner manual validation of PR #74 found that visible runtime UI buttons hover/focus but do not invoke click handlers.
- Value or risk reduction: restores manual operation for `ODY-UI-01-007` and prevents future tests from missing the same click-routing class of defect.
- Blocking or enabling relationship: blocks owner manual walkthrough of `ODY-UI-01-007` and all runtime mouse-driven trial UI interaction.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1
- `docs/tasks/active/ODY-UI-01-007_Reroll_Cancel_And_Full_Walkthrough.md`
- `Assets/Odyssey/Client/Scenes/Bootstrap.unity`
- `Assets/Odyssey/Client/Scenes/AppShell.unity`
- `Assets/Odyssey/Client/Input/Odyssey.inputactions`
- `ProjectSettings/EditorBuildSettings.asset`
- `Assets/Odyssey/Client/Runtime/AppShellEntryPoint.cs`
- `Assets/Odyssey/Client/Runtime/OdysseyRuntimeHost.cs`
- `Assets/Odyssey/Client/Runtime/DeveloperShellPresenter.cs`
- `Assets/Odyssey/Client/Runtime/TrialScreenPresenter.cs`
- `Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs`
- `Assets/Odyssey/Client/Runtime/RollPanelPresenter.cs`
- `Assets/Odyssey/Client/Runtime/RoleSelectorPresenter.cs`
- `Assets/Odyssey/Client/Runtime/GameLogPresenter.cs`
- Unity Input System UI Support documentation, package `com.unity.inputsystem`, Required Actions for UI.

### Requirement and test IDs

- Requirement IDs: `ODY-UI-01-007a`
- Existing test IDs: `TC-UNITY-TEST-001`, Unity PlayMode presenter smoke tests.
- New test IDs to introduce: None. `ODY-UI-*` tests are recorded by method name in this contract.

### Task-safe private context

- Approved summary / references: fix the runtime UI click routing gap found during owner manual validation of `ODY-UI-01-007` / PR #74.

## 4. Verified current state

### Verified facts

- PR #74 is merged into `main`; branch `fix/ody-ui-01-007a-runtime-ui-click-routing` was created from PR #74 HEAD `7ef87c5`.
- PR #75 merged `007a` into the intermediate `feat/ody-ui-01-007-reroll-cancel-and-full-walkthrough` branch, not `main`.
- PR #77 later merged the accumulated `007a`/`007b` branch into `main`.
- `Bootstrap.unity` contains only the Bootstrap `GameObject` with `OdysseyRuntimeHost`; it has no `EventSystem` or `InputSystemUIInputModule`.
- `AppShell.unity` contains an `AppShell UI` `GameObject` with `UIDocument`/`AppShellEntryPoint`, plus a camera; it has no `EventSystem` or `InputSystemUIInputModule`.
- `ProjectSettings/EditorBuildSettings.asset` assigns project-wide input actions to `Assets/Odyssey/Client/Input/Odyssey.inputactions` by GUID `35845fe01580c41289b024647b1d1c53`.
- Unity Input System UI Support documentation says UI Toolkit in Unity 2023.2+ maps project-wide UI actions directly, while required UI action names/types must remain compatible; pointer `Click`, `RightClick`, `MiddleClick`, and `ScrollWheel` are `PassThrough`.
- `Odyssey.inputactions` had `UI/Click` as `Button`, had no `UI/RightClick`, and had no `UI/MiddleClick`.
- Existing PlayMode helper `Click(...)` and `PlayerSmokeInputProbe.InvokeButton(...)` call `button.userData` directly before falling back to a synthetic `ClickEvent`, so they do not prove real Input System pointer routing.

### Assumptions

- The smallest root-cause fix is to correct the `UI` action map and add an explicit runtime `EventSystem`/`InputSystemUIInputModule` bridge in composition code, rather than hand-editing scene YAML.

## 5. Scope

### In scope

- Correct the UI action map in `Assets/Odyssey/Client/Input/Odyssey.inputactions`.
- Add a PlayMode test that simulates a real mouse click through `InputSystem.QueueStateEvent`/`InputSystem.Update`.
- Cover both Developer Shell launch and at least one button inside the composed Trial UI.
- Update task contract and ExecPlan with validation and PR evidence.

### Out of scope

- New gameplay UI behavior, touch/mobile input, full input-system audit, lower package changes, ADR changes, Unity/package version changes, and new dependencies.

### Allowed paths

```text
Assets/Odyssey/Client/Input/Odyssey.inputactions
Assets/Odyssey/Client/Runtime/AppShellEntryPoint.cs
Assets/Odyssey/Client/Tests/PlayMode/Odyssey.Tests.Unity.PlayMode.asmdef
Assets/Odyssey/Client/Tests/PlayMode/OdysseyPlayModeFoundationSmokeTests.cs
scripts/verify-test-structure.ps1
docs/tasks/active/ODY-UI-01-007a_Runtime_UI_Click_Routing_Gap_Fix.md
docs/plans/active/ODY-UI-01-007a_Runtime_UI_Click_Routing_Gap_Fix.md
```

### Paths requiring explicit approval before editing

```text
Assets/Odyssey/Client/Scenes/Bootstrap.unity
Assets/Odyssey/Client/Scenes/AppShell.unity
ProjectSettings/**
Packages/**
docs/adr/**
```

## 6. Technical constraints

- Module ownership and dependency direction: Unity Client-only fix; no changes under `Packages/**`.
- Authoritative-state and transaction boundary: Not applicable.
- Serialization / compatibility boundary: no persisted schema/contract/protocol changes.
- Time / RNG rule: Not applicable.
- Unity / thread / lifetime rule: use existing UI Toolkit runtime path and Input System package.
- Dependency / licensing rule: no new dependencies.
- Security / privacy / redaction rule: no hidden-data or audience behavior changes.
- Performance or platform constraint: Unity `6000.4.0f1`, Windows Play Mode runtime UI.
- Other: do not use `button.userData` or reflection in the new click-routing regression test.

## 7. Expected behavior

### Scenario 1 — Developer Shell click

**Given** Bootstrap/AppShell is running in Unity Play Mode  
**When** a real mouse down/up is queued over `Open Trial UI`  
**Then** the `Button.clicked` handler runs and the composed trial screen opens.

### Scenario 2 — Trial UI click

**Given** the composed Trial UI is open  
**When** a real mouse down/up is queued over a visible Trial screen board token  
**Then** the token click handler runs and the Trial screen visibly updates selection state.

### Required invariants

- Existing direct-handler tests may remain for smoke coverage, but the new regression test must not call `button.userData`, `Button.clicked` handlers, or reflection.
- No `EventSystem` is added unless project-wide UI Toolkit input is proven insufficient.

## 8. Deliverables

- Production code: corrected UI action map configuration and runtime UI input bridge.
- Tests: new/updated PlayMode test using real Input System pointer events.
- Scripts / CI: repository policy whitelist updated for the PlayMode test-only Input System references.
- Configuration: `Odyssey.inputactions`.
- Documentation: task contract and ExecPlan.
- Generated evidence or build artifacts: validation command results.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. Real Play Mode mouse click routing invokes `Button.clicked` for `Open Trial UI`.
2. Real Play Mode mouse click routing invokes `Button.clicked` for at least one button inside the composed Trial UI.
3. `Odyssey.inputactions` UI action map matches Unity's documented required action names/types for pointer click actions relevant to this bug.
4. No lower package, ADR, dependency, Unity version, or unrelated screen behavior changes are introduced.
5. A PlayMode regression test proves the click route without using `button.userData`, reflection, or direct handler invocation.
6. Required validation commands pass and results are recorded.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `RealMouseClick_RoutesThroughUiToolkitInputToDeveloperShellAndTrialScreenElements` | Unity PlayMode | Real Input System mouse events reach Developer Shell `Button.clicked` and Trial screen UI handlers | Pass |

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\test-unity.ps1
```

### Manual validation

- Open Unity `6000.4.0f1`, load `Assets/Odyssey/Client/Scenes/Bootstrap.unity`, press Play, click `Open Trial UI`, then click buttons inside the Trial UI and confirm handlers visibly run.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Unity `6000.4.0f1`, Play Mode.
- Scripting backend: Editor default for tests.
- Network topology or database fixture: local test/runtime trial campaign.
- Other: .NET SDK per `global.json`.

### Validation not required by this task

- `build-dev.ps1` and release IL2CPP are outside this task's explicit validation list.

## 11. Compatibility, migration, and rollback

- Compatibility impact: UI input asset configuration and explicit runtime input bridge for UI Toolkit panels.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Play Mode UI clicks begin reaching existing handlers.
- Rollback method: revert this branch/PR.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: synthetic trial UI state only.
- Trust boundaries: Unity Client input to existing UI Toolkit handlers.
- Authorization / audience checks: unchanged.
- Redaction requirements: unchanged.
- Log-safe fields: no new diagnostics logging expected.
- Abuse / malformed input limits: no new external input parser.
- Security tests: existing audience/security tests remain unchanged.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: runtime input infrastructure plus scene/project configuration investigation and a new PlayMode regression path.
- ExecPlan path: `docs/plans/active/ODY-UI-01-007a_Runtime_UI_Click_Routing_Gap_Fix.md`
- Expected pull request count: 1
- Milestone or sequencing constraints: implementation originally stacked on PR #74 because #74 was then open and contained the UI being unblocked; final mainline inclusion is through PR #77.

## 15. Documentation and versioning impact

- Documents that must change: this task contract and ExecPlan.
- Documents that must not change: ADRs and private documentation.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed or explicitly assigned to owner review.
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [x] Pull request explains changes, evidence, limitations, and follow-up work.
- [x] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Assets/Odyssey/Client/Input/Odyssey.inputactions` — `UI/Click` changed to `PassThrough`; `UI/RightClick` and `UI/MiddleClick` actions/bindings added.
- `Assets/Odyssey/Client/Runtime/AppShellEntryPoint.cs` — creates/reuses `EventSystem` and ensures `InputSystemUIInputModule` at runtime.
- `Assets/Odyssey/Client/Tests/PlayMode/Odyssey.Tests.Unity.PlayMode.asmdef` — references existing Input System test assemblies for PlayMode-only regression coverage.
- `Assets/Odyssey/Client/Tests/PlayMode/OdysseyPlayModeFoundationSmokeTests.cs` — adds real mouse click routing test through Developer Shell and Trial screen.
- `scripts/verify-test-structure.ps1` — permits the PlayMode test-only asmdef to reference `Unity.InputSystem` and `Unity.InputSystem.TestFramework`.
- Task contract and ExecPlan updated.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Pass | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Pass | Repository policy check passed. |
| `dotnet build DotNet\Odyssey.Core.sln` | Pass | Build succeeded, 0 warnings, 0 errors. Required escalation because sandbox denied `C:\Users\alexx\AppData\Local\Microsoft SDKs`. |
| `dotnet test DotNet\Odyssey.Core.sln` | Pass | Contracts 1, Domain 27, Networking 67, Unit 105, Architecture 2, Persistence 60; all passed. Required escalation for SDK cache access. |
| `.\scripts\test-unity.ps1` | Pass | Original stacked validation: BuildId `odyssey-local-20260828t163333z-g7ef87c54e2d4-dirty`; EditMode 62/62 passed, PlayMode 4/4 passed. Final mainline PR #77 local validation: EditMode 62/62 passed, PlayMode 5/5 passed. |
| `.\scripts\test-fast.ps1` | Pass | Clean final run passed architecture checks and .NET TRX totals: Domain 27, Contracts 1, Networking 67, Unit 105, Architecture 2, Persistence 60. Required escalation for SDK cache access. |
| `.\scripts\verify-repository.ps1` | Pass | `REPOSITORY-VERIFY PASS repository checks passed`. Required escalation for SDK cache access. |
| `.\scripts\verify-docs.ps1` | Not run | Script is not present in this checkout (`Test-Path` returned `False`). |
| PR #77 CI | Pass | `repository-policy-format-structure`, `dotnet-restore-build-test`, `unity-project-package-static`, and `buildidentity-provenance` passed in GitHub Actions run `33272950562`. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Pass | `RealMouseClick_RoutesThroughUiToolkitInputToDeveloperShellAndTrialScreenElements` clicks `accepted-probe-button` through `InputTestFixture` and observes accepted probe result. |
| AC-2 | Pass | Same test clicks `trial-ui-button`, then clicks a visible Trial screen token and observes selected-token status. |
| AC-3 | Pass | `AssertUiInputActionsConfigured` verifies `UI/Click`, `UI/RightClick`, and `UI/MiddleClick` are `PassThrough`; Unity PlayMode passed. |
| AC-4 | Pass | No lower package, ADR, dependency, Unity version, or unrelated screen behavior changes were introduced. |
| AC-5 | Pass | New regression test does not use `button.userData`, reflection, or direct handler invocation for the real click route. |
| AC-6 | Pass | Required validation commands passed and results are recorded above. |

### Build and artifact evidence

- Build identity: `odyssey-local-20260828t163333z-g7ef87c54e2d4-dirty`.
- Artifact path / name: Not applicable.
- Checksums: Not applicable.
- Test or quality report: Unity EditMode/PlayMode result XMLs and .NET TRX logs under `Logs/`.

### Merge and PR evidence

- Initial stacked PR: [#75](https://github.com/odyssey-services/Odyssey_VTT/pull/75), merged 2026-08-29 19:58 UTC into `feat/ody-ui-01-007-reroll-cancel-and-full-walkthrough`; merge SHA `e294eee9d2fd7d94a29cf59268824cbc662bf942`.
- Final mainline PR: [#77](https://github.com/odyssey-services/Odyssey_VTT/pull/77), merged 2026-08-29 20:29 UTC into `main`; merge SHA `27b2a1dac67daa91c653ef01593aae02a089340f`.
- Final code SHA included in `main`: `119a0db89431eb12cfaeb85ef001c6b76d341c7d` via PR #77.

### Known limitations

- None known.

### Follow-up tasks

- None expected.

### Self-review summary

- Scope review: Complete; edits are limited to input routing, test-only policy, tests, and task docs.
- Architecture review: Complete; Unity Client/runtime composition only, no lower module dependency changes.
- Test review: Complete; automated coverage now exercises real Input System mouse events.
- Security/privacy review: Complete; no hidden-data, audience, persistence, networking, or diagnostics behavior changed.
- Documentation/version review: Complete; no ADR, package, Unity version, schema, or protocol changes.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-08-28 — Decision: stack `ODY-UI-01-007a` on PR #74 rather than `main`. Authority / approval: task §2 and verified then-current PR #74 open/unmerged state.
- 2026-08-29 — Discovery / change-control decision: PR #75 was merged into the intermediate `feat/ody-ui-01-007-reroll-cancel-and-full-walkthrough` branch, not `main`, because the PM task did not explicitly require `base=main` for the stacked PR. Codex caught the mismatch during documentation closeout preflight and did not backfill false `main` status. Final mainline inclusion was completed honestly through PR #77, merge SHA `27b2a1dac67daa91c653ef01593aae02a089340f`.
- 2026-08-28 — Decision: first try project-wide UI Toolkit actions instead of adding scene-local `EventSystem`/`InputSystemUIInputModule`. Authority / approval: Unity Input System UI Support documentation for UI Toolkit 2023.2+ and verified `ProjectSettings/EditorBuildSettings.asset` project-wide actions assignment.
- 2026-08-28 — Decision: add `EventSystem`/`InputSystemUIInputModule` programmatically from `AppShellEntryPoint`, not as scene YAML. Authority / approval: the real PlayMode pointer test still timed out after correcting project-wide actions only; task §4 asks to choose scene object vs composition code, and code composition avoids repeated manual scene setup.
- 2026-08-28 — Decision: keep `PlayerSmokeInputProbe.InvokeButton` as keyboard smoke helper but record that it is not regression evidence for mouse click routing. Authority / approval: task §4 asks to decide whether to fix or document its limitation; the new PlayMode pointer test owns this bug class.
- 2026-08-28 — Decision: update `scripts/verify-test-structure.ps1` to allow `Unity.InputSystem` and `Unity.InputSystem.TestFramework` only for `Odyssey.Tests.Unity.PlayMode`. Authority / approval: the new PlayMode test requires Unity's Input System test fixture and the verifier is the owning policy for asmdef references.
- 2026-08-28 — Decision: use a visible Trial screen token as the second real-click assertion instead of the Roll button. Authority / approval: Unity batchmode stayed at 640x480 and the Roll panel button wrapped below the visible viewport, while the task acceptance requires proving a composed Trial UI runtime click route, not specifically the roll command.
- 2026-08-28 — Finding attribution: the runtime click-routing gap was found by the product owner during manual validation of `ODY-UI-01-007` / PR #74 and independently confirmed by PM through direct reading of `Bootstrap.unity`, `AppShell.unity`, `Odyssey.inputactions`, and `AppShellEntryPoint.cs` before this task was assigned.

### Discoveries and deviations

- 2026-08-28 — First `test-unity` attempt failed Unity compile because `Odyssey.Tests.Unity.PlayMode` did not reference `Unity.InputSystem`; the runtime assembly already had the package reference, so the PlayMode test asmdef was updated to reference the existing package without adding a dependency.
- 2026-08-28 — After correcting project-wide `Odyssey.inputactions`, the new real pointer PlayMode test still timed out opening `Open Trial UI`. This confirmed an additional runtime routing gap beyond action type mismatch; `AppShellEntryPoint` now creates the input module in code.
- 2026-08-28 — `dotnet build`, `dotnet test`, `test-fast`, `test-unity`, and `verify-repository` required escalation because the sandbox denied access to local SDK/Unity state outside the workspace.

### Approved task changes

- None.
