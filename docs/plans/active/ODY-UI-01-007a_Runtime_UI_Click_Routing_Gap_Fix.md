# ODY-UI-01-007a — Runtime UI Click Routing Gap Fix

**Status:** Implementation complete; draft PR pending  
**Owner:** Codex (agent)  
**Branch:** `fix/ody-ui-01-007a-runtime-ui-click-routing`  
**Pull request:** Not opened  
**Last updated:** 2026-08-28 16:35 UTC

## 1. Purpose and user-visible outcome

Real mouse clicks in Unity Play Mode activate the existing UI Toolkit buttons in the Developer Shell and Trial UI.

## 2. Task contract

- Goal: make real mouse clicks reach runtime UI `Button.clicked` handlers and add a PlayMode regression test that does not directly invoke handlers.
- Acceptance criteria: see `docs/tasks/active/ODY-UI-01-007a_Runtime_UI_Click_Routing_Gap_Fix.md` §9.
- Requirement IDs: `ODY-UI-01-007a`.
- In scope: `Odyssey.inputactions`, `AppShellEntryPoint` runtime UI input bridge, one PlayMode click-routing test, the PlayMode test asmdef reference and verifier whitelist needed for that test, task/plan docs.
- Out of scope: new UI features, lower package changes, ADRs, Unity/package versions, touch/mobile/full input audit.
- Required authorities: Active Baseline v2.2, `AGENTS.md`, `PLANS.md`, PR #74 task contract, current Bootstrap/AppShell scenes, current input actions, Unity Input System UI Support docs.
- Required validation commands: `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `dotnet build DotNet\Odyssey.Core.sln`, `dotnet test DotNet\Odyssey.Core.sln`, `.\scripts\test-unity.ps1`.

## 3. Current state

- PR #74 is open and unmerged; this fix branch is stacked on its HEAD `7ef87c5`.
- `Bootstrap.unity` has no `EventSystem` and only hosts `OdysseyRuntimeHost`.
- `AppShell.unity` has no `EventSystem`; it hosts a `UIDocument`, `AppShellEntryPoint`, and camera.
- `EditorBuildSettings.asset` maps `com.unity.input.settings.actions` to `Assets/Odyssey/Client/Input/Odyssey.inputactions`.
- `Odyssey.inputactions` has `UI/Click` as `Button`, lacks `UI/RightClick`, and lacks `UI/MiddleClick`.
- Existing PlayMode `Click` helper and `PlayerSmokeInputProbe.InvokeButton` call stored actions directly, so they do not exercise real pointer routing.

## 4. Proposed approach

Correct the UI action map to Unity's expected action names/types and create the `EventSystem`/`InputSystemUIInputModule` from `AppShellEntryPoint`, where the runtime UI is already composed. This keeps the fix out of hand-edited scene YAML while giving the runtime panel an explicit input module. PlayMode coverage queues mouse position and left-button state through Input System and waits for the UI Toolkit button handler's visible effect.

## 5. Milestones

### M1 — Contract and root-cause confirmation

- [x] Confirm PR #74 is open/unmerged and create stacked fix branch.
- [x] Read scenes, input action asset, runtime presenters, PlayMode tests, task template, test catalog, and Unity UI Support docs.
- [x] Create task contract and ExecPlan before production edits.

### M2 — Minimal input configuration fix

- [x] Change `UI/Click` to `PassThrough`.
- [x] Add documented `UI/RightClick` and `UI/MiddleClick` actions and mouse bindings.
- [x] Add the runtime input module programmatically instead of scene-local YAML.

### M3 — Real click regression coverage

- [x] Add PlayMode pointer simulation helper through Unity Input System test fixture.
- [x] Prove real click opens the Trial UI from `Open Trial UI`.
- [x] Prove real click activates a visible interactive element inside Trial UI.

### M4 — Validation and PR

- [x] Required local validation commands pass.
- [ ] Draft PR opened against PR #74 branch.
- [ ] CI passes and final evidence is recorded.

## 6. Progress log

- 2026-08-28 11:38 UTC — Verified PR #74 is open/unmerged at `7ef87c5`; created `fix/ody-ui-01-007a-runtime-ui-click-routing`.
- 2026-08-28 11:38 UTC — Confirmed scenes lack `EventSystem`, project-wide input actions point to `Odyssey.inputactions`, and the `UI` action map is missing documented pointer-action shape.
- 2026-08-28 11:38 UTC — Created task contract and ExecPlan.
- 2026-08-28 16:35 UTC — Corrected UI action map, added runtime input bridge, added PlayMode real mouse regression, and updated verifier whitelist for test-only Input System references.
- 2026-08-28 16:35 UTC — Required validation passed: format, repository policy, .NET build/test, test-fast, verify-repository, and Unity EditMode/PlayMode.

## 7. Decisions

- 2026-08-28 — Decision: stack on PR #74. Rationale: `007a` unblocks the unmerged `007` manual walkthrough and depends on its Trial UI. Authority: task §2.
- 2026-08-28 — Decision: first fix project-wide UI actions, not scene objects. Rationale: Unity 2023.2+ UI Toolkit directly uses project-wide actions, and the repo already assigns `Odyssey.inputactions` in `EditorBuildSettings.asset`. Authority: Unity Input System UI Support docs and repository state.
- 2026-08-28 — Decision: add the input module programmatically after project-wide actions alone failed the real pointer test. Rationale: this fixes the real runtime route without duplicating manual scene configuration. Authority: task §4.
- 2026-08-28 — Decision: leave `PlayerSmokeInputProbe.InvokeButton` as a keyboard smoke implementation detail, but not as click-routing evidence. Rationale: the new pointer PlayMode test owns the bug class with a real input route. Authority: task §4.
- 2026-08-28 — Decision: allow `Unity.InputSystem` and `Unity.InputSystem.TestFramework` only for the PlayMode test asmdef in `verify-test-structure.ps1`. Rationale: the test-only assembly needs Unity's Input System fixture to prove the bug class without handler shortcuts. Authority: repository verifier ownership of asmdef references.
- 2026-08-28 — Decision: use a visible Trial screen token as the second real-click target. Rationale: Unity batchmode stayed at 640x480 and the Roll panel wrapped below the test viewport; the required signal is that real clicks reach composed Trial UI handlers. Authority: task acceptance criteria.

## 8. Discoveries and deviations

- First `.\scripts\test-unity.ps1` attempt failed Unity compile because `Odyssey.Tests.Unity.PlayMode` did not reference `Unity.InputSystem`; the runtime assembly already referenced that package, so the test asmdef was updated without adding a dependency.
- Correcting only project-wide `Odyssey.inputactions` was insufficient: the real pointer PlayMode test still timed out on `Open Trial UI`. `AppShellEntryPoint` now creates the runtime input module in code.
- `dotnet build`, `dotnet test`, `test-fast`, `test-unity`, and `verify-repository` required escalation because the sandbox could not access local SDK/Unity state outside the workspace.

## 9. Validation and acceptance evidence

- `.\scripts\verify-format.ps1`: PASS, `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: PASS, repository policy check passed.
- `dotnet build DotNet\Odyssey.Core.sln`: PASS, 0 warnings, 0 errors.
- `dotnet test DotNet\Odyssey.Core.sln`: PASS, Contracts 1, Domain 27, Networking 67, Unit 105, Architecture 2, Persistence 60.
- `.\scripts\test-fast.ps1`: PASS, clean final run with .NET TRX totals all passed.
- `.\scripts\verify-repository.ps1`: PASS, `REPOSITORY-VERIFY PASS repository checks passed`.
- `.\scripts\test-unity.ps1`: PASS, build identity `odyssey-local-20260828t163333z-g7ef87c54e2d4-dirty`; EditMode 62/62 and PlayMode 4/4.

## 10. Recovery and rollback

Rollback is a normal branch/PR revert. No data migration, schema change, dependency change, or lower-package change is involved.

## 11. Open questions and blockers

None.

## 12. Outcome and follow-up

Implementation and local validation complete. Draft PR remains to be opened against PR #74 branch, then CI evidence can be added.
