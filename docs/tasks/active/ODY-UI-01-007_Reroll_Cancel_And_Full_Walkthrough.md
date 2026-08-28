# ODY-UI-01-007 — Reroll Cancel and Full Manual Walkthrough

**Status:** In Progress  
**Roadmap stage / slice:** SLICE-UI-01  
**Owner:** Codex (agent)  
**Requested by:** Product owner  
**Branch:** `feat/ody-ui-01-007-reroll-cancel-and-full-walkthrough`  
**Pull request:** Not opened  
**ExecPlan:** `docs/plans/active/ODY-UI-01-007_Reroll_Cancel_And_Full_Walkthrough.md`  
**Created:** 2026-08-28  
**Last updated:** 2026-08-28 10:23 UTC

## 1. Goal

Add reroll/cancel controls to the trial roll UI and assemble the board, role selector, roll panel, and game log into one Play Mode-visible trial screen, with one automated full walkthrough proving the UI presenters can exercise the ten-step `ODY-S03-008` scenario.

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-UI-01-002` through `006` delivered separate presenters, but no single Play Mode screen currently exposes them together for owner manual validation.
- Value or risk reduction: closes `SLICE-UI-01` by making the whole minimal trial flow visible and runnable.
- Blocking or enabling relationship: enables the product owner to perform the first real Unity Play Mode manual walkthrough.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1
- `docs/tasks/SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` §5
- `docs/tasks/active/ODY-S03-008_Vertical_Slice_Integration.md`
- `Packages/com.odyssey.application/Runtime/Dice/DiceRollService.cs`
- `Packages/com.odyssey.application/Runtime/Dice/DiceContracts.cs`
- `Assets/Odyssey/Client/Runtime/AppShellEntryPoint.cs`
- `Assets/Odyssey/Client/Runtime/OdysseyRuntimeHost.cs`
- `Assets/Odyssey/Client/Runtime/RuntimeComposition.cs`
- `Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs`
- `Assets/Odyssey/Client/Runtime/RollPanelPresenter.cs`
- `Assets/Odyssey/Client/Runtime/GameLogPresenter.cs`

### Requirement and test IDs

- Requirement IDs: `ODY-UI-01-007`; `ODY-S03-008` ten-step scenario; roadmap §12.6 as already represented by `ODY-S03-008`.
- Existing test IDs: Unity EditMode/PlayMode presenter tests from `ODY-UI-01-002` through `006`.
- New test IDs to introduce: None. `ODY-UI-*` tasks are not entered in `Tests/Metadata/test-catalog.json`; Unity coverage is recorded here by method name.

### Task-safe private context

- Approved summary / references: add only reroll/cancel controls and a composed trial UI screen for manual Play Mode walkthrough; do not copy private task text.

## 4. Verified current state

### Verified facts

- Preflight completed: `origin/main` contains merged PR #73, and local `main` was fast-forwarded to merge commit `35fc3da`.
- Branch `feat/ody-ui-01-007-reroll-cancel-and-full-walkthrough` was created from updated `main`.
- `OdysseyRuntimeHost` currently starts the `DeveloperShell` runtime and loads `Assets/Odyssey/Client/Scenes/AppShell.unity`.
- `AppShellEntryPoint` currently initializes only `DeveloperShellPresenter`.
- `BoardScreenPresenter`, `RollPanelPresenter`, and `GameLogPresenter` are separate code-built UI Toolkit presenters.
- `RollPanelPresenter` owns a `DiceRollStore`, exposes `LastRoll`, and already calls submit/modifier/override services.
- `DiceRollService.RequestFullReroll` creates a new roll, marks the original `SupersededByReroll`, and preserves original roll data.
- `DiceRollService.CancelRoll` marks the roll `Cancelled` and requires a reason.

### Assumptions

- The smallest manual launch path is to add a DeveloperShell launch button that swaps the existing `AppShell` UI into a composed trial screen. This preserves existing Bootstrap/AppShell/runtime composition and avoids adding a second scene or bootstrap mechanism. It will be verified by PlayMode coverage and final manual instructions.

## 5. Scope

### In scope

- Add reroll/cancel controls and presenter methods to `RollPanelPresenter`.
- Add a small composed trial screen presenter that builds one visible screen from the existing board, role selector, roll panel, and game-log presenters.
- Extend the existing `DeveloperShellPresenter`/`AppShellEntryPoint` path with a launch action for the trial screen.
- Add one automated full presenter walkthrough for the ten-step scenario.
- Update task contract, ExecPlan, and `SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` with real evidence after PR CI.

### Out of scope

- Visual polish, animation, final production UI, real networking, new ADRs, and changes under `Packages/com.odyssey.application`, `Packages/com.odyssey.domain`, or `Packages/com.odyssey.persistence`.

### Allowed paths

```text
Assets/Odyssey/Client/Runtime/AppShellEntryPoint.cs
Assets/Odyssey/Client/Runtime/DeveloperShellPresenter.cs
Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs
Assets/Odyssey/Client/Runtime/RollPanelPresenter.cs
Assets/Odyssey/Client/Runtime/TrialScreenPresenter.cs
Assets/Odyssey/Client/Runtime/TrialScreenPresenter.cs.meta
Assets/Odyssey/Client/Tests/EditMode/*Trial*Tests.cs
Assets/Odyssey/Client/Tests/EditMode/*Trial*Tests.cs.meta
Assets/Odyssey/Client/Tests/EditMode/RollPanelPresenterTests.cs
Assets/Odyssey/Client/Tests/PlayMode/OdysseyPlayModeFoundationSmokeTests.cs
docs/tasks/active/ODY-UI-01-007_Reroll_Cancel_And_Full_Walkthrough.md
docs/plans/active/ODY-UI-01-007_Reroll_Cancel_And_Full_Walkthrough.md
docs/tasks/SLICE-UI-01_IMPLEMENTATION_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
Packages/**
docs/adr/**
ProjectSettings/**
Packages/manifest.json
```

## 6. Technical constraints

- Module ownership and dependency direction: Unity Client may compose Application services and Persistence adapters; no lower package may depend on Unity.
- Authoritative-state and transaction boundary: reroll/cancel must call `DiceRollService.RequestFullReroll` and `DiceRollService.CancelRoll`.
- Serialization / compatibility boundary: no persisted contract or schema changes.
- Time / RNG rule: use existing injected `IWallClock` and `IAuthoritativeRandomStreamFactory`; no new RNG/time source in authoritative logic.
- Unity / thread / lifetime rule: presenters are code-built UI Toolkit classes and subscriptions must be owned by `PresentationRuntime`.
- Dependency / licensing rule: no new dependencies.
- Security / privacy / redaction rule: result/log display must continue to use existing visibility policies and reconnect filtering.
- Performance or platform constraint: Windows Unity Editor Play Mode/manual trial UI.
- Other: do not add `ODY-UI-*` to `Tests/Metadata/test-catalog.json`.

## 7. Expected behavior

### Scenario 1 — reroll

**Given** a visible current roll exists  
**When** the actor presses Reroll  
**Then** the roll panel calls `RequestFullReroll`, stores the new roll as `LastRoll`, and the original roll is preserved with status `SupersededByReroll`.

### Scenario 2 — cancel

**Given** a visible current roll exists and a cancel reason is provided  
**When** the actor presses Cancel  
**Then** the roll panel calls `CancelRoll`, updates status text, and the original immutable roll data is not rewritten.

### Scenario 3 — full trial screen

**Given** the user starts the existing Bootstrap/AppShell flow in Play Mode  
**When** the user opens the trial UI from the DeveloperShell launch control  
**Then** board, role selector, roll panel, and game log are visible together and can walk the ten `ODY-S03-008` steps.

### Required invariants

- The composed screen uses one shared `CampaignHandle` for board and game log.
- The roll panel and board consume the same shared `RoleSelection`.
- The composed trial screen does not create a second runtime bootstrap path.
- Any real composition gap is recorded in this contract instead of silently bypassed.

## 8. Deliverables

- Production code: reroll/cancel controls and composed trial screen launch path in Unity Client.
- Tests: Unity automated walkthrough and focused reroll/cancel coverage as needed.
- Scripts / CI: None.
- Configuration: None expected.
- Documentation: this task contract, ExecPlan, backlog final status/evidence.
- Generated evidence or build artifacts: validation output and development build evidence.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. `RollPanelPresenter` exposes Reroll and Cancel UI controls.
2. Reroll calls `DiceRollService.RequestFullReroll` and updates `LastRoll` to the new roll.
3. The original rerolled roll remains available in the store with status `SupersededByReroll`, with formula/natural results unchanged.
4. Cancel calls `DiceRollService.CancelRoll` with a reason and updates the visible status/result.
5. Cancelling preserves the roll's formula/natural results and changes only lifecycle status.
6. The existing Play Mode launch path can open a single composed trial screen containing board, one role selector, roll panel, and game log.
7. The composed screen shares one `CampaignHandle` between board and game log and one `RoleSelection` between board and roll panel.
8. One automated test walks the ten `ODY-S03-008` steps through the composed presenters.
9. The final report includes concrete manual Unity Editor steps to open and walk the trial UI.
10. No changes are made under `Packages/com.odyssey.application`, `Packages/com.odyssey.domain`, `Packages/com.odyssey.persistence`, no ADRs are changed, and no dependency/version changes are introduced.
11. Required validation commands pass and real results are recorded.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `RollPanelPresenter_RerollAndCancel...` | Unity EditMode | Reroll/cancel UI paths call the real dice service and preserve immutable roll data | Pass |
| `TrialScreenPresenter_FullWalkthrough...` | Unity EditMode or PlayMode | Full ten-step scenario through the composed presenters | Pass |
| `DeveloperShell...LaunchesTrial...` | Unity PlayMode or EditMode | Existing AppShell launch path can open the composed trial UI | Pass |

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\test-fast.ps1
.\scripts\test-unity.ps1
.\scripts\build-dev.ps1
.\scripts\verify-repository.ps1
```

### Manual validation

- Product owner manual validation after PR: open the Play Mode trial UI using the final instructions in this contract/final report and walk the visible ten-step flow.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Unity `6000.4.0f1`, Editor Play Mode for manual trial UI; EditMode/PlayMode tests for automation.
- Scripting backend: editor default for tests; development build uses Mono per existing script.
- Network topology or database fixture: local temporary SQLite campaign for tests; local persistent trial campaign for manual Play Mode.
- Other: .NET SDK per `global.json`.

### Validation not required by this task

- Real networking and release IL2CPP build are out of scope for this trial UI task.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None expected; UI Client only, no persistence schema/contract changes.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert the branch/PR.
- Data-loss risk and protection: automated tests use temporary campaign directories; manual trial screen creates disposable local trial data under Unity persistent data.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: synthetic trial tokens, dice rolls, game-log summary payloads.
- Trust boundaries: UI calls existing Application/Persistence services directly under `SLICE-UI-01` trial convention.
- Authorization / audience checks: existing `RoleSelection`, dice permissions, board movement ownership, roll visibility, and game-log reconnect filtering.
- Redaction requirements: do not bypass `DiceRollVisibilityPolicy` or `GameLogReconnectService`.
- Log-safe fields: no new diagnostics logging expected.
- Abuse / malformed input limits: existing dice/persistence services own validation; UI surfaces safe reason codes only.
- Security tests: full walkthrough asserts observer denial and role-dependent visibility.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: the task changes multiple Unity Client runtime components, adds a composed Play Mode-visible launch path, and exercises persistence/audience/state transitions end-to-end.
- ExecPlan path: `docs/plans/active/ODY-UI-01-007_Reroll_Cancel_And_Full_Walkthrough.md`
- Expected pull request count: 1
- Milestone or sequencing constraints: create contract/plan first; update backlog only after PR and CI evidence.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, ExecPlan, `docs/tasks/SLICE-UI-01_IMPLEMENTATION_BACKLOG.md`.
- Documents that must not change: ADRs and private documentation.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [ ] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [ ] Required automated tests pass.
- [ ] Required manual checks are completed or explicitly assigned to owner review.
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

- `Assets/Odyssey/Client/Runtime/AppShellEntryPoint.cs` — launch composed trial screen from the existing AppShell entry point.
- `Assets/Odyssey/Client/Runtime/DeveloperShellPresenter.cs` — add `Open Trial UI` launch button and facade callback.
- `Assets/Odyssey/Client/Runtime/OdysseyRuntimeHost.cs` — pass the AppShell trial-launch callback through the DeveloperShell facade.
- `Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs` — allow composition into a supplied container and suppress duplicate role selector for the composed screen.
- `Assets/Odyssey/Client/Runtime/RollPanelPresenter.cs` — add reroll/cancel controls and service calls; allow suppressing duplicate role selector.
- `Assets/Odyssey/Client/Runtime/TrialScreenPresenter.cs` — compose board, one role selector, roll panel, and game log with shared role/campaign state.
- `Assets/Odyssey/Client/Tests/EditMode/RollPanelPresenterTests.cs` — focused reroll/cancel coverage.
- `Assets/Odyssey/Client/Tests/EditMode/RuntimeCompositionAndDiagnosticsTests.cs` — update test facade for the new launch callback.
- `Assets/Odyssey/Client/Tests/EditMode/TrialScreenPresenterTests.cs` — full composed ten-step walkthrough.
- `Assets/Odyssey/Client/Tests/PlayMode/OdysseyPlayModeFoundationSmokeTests.cs` — Play Mode launch smoke for the trial screen.
- Task contract and ExecPlan.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Pass | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Pass | `Repository policy check passed`. |
| `dotnet build DotNet\Odyssey.Core.sln` | Pass | 0 warnings, 0 errors. |
| `dotnet test DotNet\Odyssey.Core.sln` | Pass | Contracts 1/1, Domain 27/27, Networking 67/67, Unit 105/105, Architecture 2/2, Persistence 60/60. |
| `.\scripts\test-fast.ps1` | Pass | 0 warnings, 0 errors; `TC-DOTNET-001 PASS` for all six fast assemblies. |
| `.\scripts\test-unity.ps1` | Pass | Unity compile exit 0; EditMode 62/62, PlayMode 3/3. |
| `.\scripts\build-dev.ps1` | Pending rerun | First attempt correctly failed the clean-tree provenance precondition because implementation/docs were uncommitted; rerun after commit. |
| `.\scripts\verify-repository.ps1` | Pass | `REPOSITORY-VERIFY PASS repository checks passed`. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Pass | `RollPanelPresenter` renders `reroll-button`, `cancel-reason`, and `cancel-roll-button`. |
| AC-2 | Pass | `RollPanelPresenter.RequestFullReroll` calls `DiceRollService.RequestFullReroll` and updates `LastRoll`. |
| AC-3 | Pass | `PlayerReroll_CurrentRoll_CreatesNewRollAndSupersedesOriginalWithoutRewritingData`; Unity EditMode passed. |
| AC-4 | Pass | `RollPanelPresenter.CancelRoll` calls `DiceRollService.CancelRoll` with the supplied reason and updates status/result. |
| AC-5 | Pass | `PlayerCancel_CurrentRollWithReason_CancelsWithoutRewritingData`; Unity EditMode passed. |
| AC-6 | Pass | `DeveloperShellLaunchesTrialScreen`; Unity PlayMode passed. |
| AC-7 | Pass | `TrialScreenPresenter` creates one shared `RoleSelection`, `CampaignHandle`, roll store, and group directory for child presenters. |
| AC-8 | Pass | `FullWalkthrough_ComposedPresenters_RunTenStepScenario`; Unity EditMode passed. |
| AC-9 | Pending | Final report/manual instructions pending PR handoff. |
| AC-10 | Pass | Diff scope reviewed: no `Packages/**`, ADR, dependency, package, or Unity version changes. |
| AC-11 | Pending rerun | All required checks passed except final `build-dev` rerun after clean commit. |

### Build and artifact evidence

- Build identity: Pending.
- Artifact path / name: Pending.
- Checksums: Pending.
- Test or quality report: Pending.

### Known limitations

- Real owner manual validation occurs after handoff; this task provides the launchable screen and exact instructions.

### Follow-up tasks

- None expected unless validation finds a real composition gap.

### Self-review summary

- Scope review: Pending.
- Architecture review: Pending.
- Test review: Pending.
- Security/privacy review: Pending.
- Documentation/version review: Pending.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-08-28 — Decision: use an ExecPlan. Authority / approval: `PLANS.md` §1 and task scope spanning multiple Unity Client runtime components plus persistence/audience/state-transition validation.
- 2026-08-28 — Decision: prefer a DeveloperShell launch button that opens the composed trial screen inside the existing `AppShell` scene over adding a new bootstrap path. Authority / approval: task instruction to follow the existing `DeveloperShellPresenter`/`AppShellEntryPoint`/`OdysseyRuntimeHost` composition mechanism and keep the trial UI minimal.
- 2026-08-28 — Decision: use one shared role selector in the composed trial screen and suppress duplicate internal role selectors from board/roll presenters only for composition. Authority / approval: task requirement that all elements be visible together on one screen and current presenter constructors already share `RoleSelection`.

### Approved task changes

- None.
