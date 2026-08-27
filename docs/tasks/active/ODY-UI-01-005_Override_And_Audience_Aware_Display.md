# ODY-UI-01-005 — Override and Audience-Aware Result Display

**Status:** In Progress  
**Roadmap stage / slice:** SLICE-UI-01  
**Owner:** Codex (agent)  
**Requested by:** Product owner  
**Branch:** `feat/ody-ui-01-005-override-and-audience-aware-display`  
**Pull request:** Not opened  
**ExecPlan:** `docs/plans/active/ODY-UI-01-005_Override_And_Audience_Aware_Display.md`  
**Created:** 2026-08-27  
**Last updated:** 2026-08-27 23:17 UTC

## 1. Goal

Extend the minimal roll panel with a MainGM-only override control and audience-aware result display that calls `DiceRollVisibilityPolicy.TryGetVisibleRoll` before showing roll details for the currently selected role.

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-UI-01-004` can create and modify rolls, but cannot yet exercise roadmap steps 6-7 by hand.
- Value or risk reduction: proves the Unity Client uses the existing Application override and audience projection contracts instead of displaying authoritative dice state directly.
- Blocking or enabling relationship: enables later persistence/game-log UI and the final `SLICE-UI-01` manual walkthrough.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1
- `docs/tasks/SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` §5
- `docs/tasks/active/ODY-S03-008_Vertical_Slice_Integration.md` steps 6-7
- `docs/tasks/TASK_TEMPLATE.md`
- `Packages/com.odyssey.application/Runtime/Dice/DiceContracts.cs`
- `Packages/com.odyssey.application/Runtime/Dice/DiceRollService.cs`
- `Packages/com.odyssey.application/Runtime/Dice/DiceRollVisibilityPolicy.cs`
- `Packages/com.odyssey.application/Runtime/Audience/AudienceContracts.cs`
- `Assets/Odyssey/Client/Runtime/RollPanelPresenter.cs`
- `Assets/Odyssey/Client/Runtime/RoleSelection.cs`
- `Assets/Odyssey/Client/Runtime/RoleSelectorPresenter.cs`

### Requirement and test IDs

- Requirement IDs: `ODY-UI-01-005`; roadmap §12.6 steps 6-7 as summarized by `ODY-S03-008`.
- Existing test IDs: `TC-DICE-*` service and visibility tests from `ODY-S03-005`/`ODY-S03-006`; `ODY-UI-01-004` roll panel tests.
- New test IDs to introduce: None. `verify-test-structure.ps1` currently accepts only `ODY-SNN-NNN` task IDs in `Tests/Metadata/test-catalog.json`, so this UI task records Unity EditMode coverage by method name in this contract.

### Task-safe private context

- Approved summary / references: add only the minimal override and audience display behavior described by this task; do not copy private task text.

## 4. Verified current state

### Verified facts

- `main` was fast-forwarded to `origin/main` before branching; recent log includes merged PR #67, #69, #70, and #71.
- `RollPanelPresenter` already owns the current `DiceRollStore`, `LastRoll`, `LastRollChanged`, role selector subscription, and direct `DiceRollService.SubmitRoll`/modifier calls.
- `DiceRollService.ApplyOverride` returns `DiceOverrideDenied` when the actor is not MainGM and `DiceOverrideReasonRequired` when the reason is empty.
- `DiceRollVisibilityPolicy.TryGetVisibleRoll` returns a `DiceRollView` only when the current role/user is entitled; `Public` is visible to Observer, while `PlayerAndGM` excludes unrelated Observer.
- `InMemoryCampaignUserGroupDirectory` is available for a minimal `SelectedParticipants` fixture without adding group lifecycle UI.

### Assumptions

- Extending `RollPanelPresenter` is the smallest safe structure because it already owns the roll state, role subscription, and UI status labels required by override and display behavior.

## 5. Scope

### In scope

- Extend `RollPanelPresenter` with an audience selector for `Public`, `PlayerAndGM`, and `SelectedParticipants`.
- Default the selector to `PlayerAndGM`.
- For `SelectedParticipants`, create a minimal audience using the current `RoleSelection.PlayerUserId` and an in-memory group fixture containing that user.
- Add a MainGM-only override reason field and Override button calling `DiceRollService.ApplyOverride`.
- Display no roll-yielding details until `DiceRollVisibilityPolicy.TryGetVisibleRoll` returns true for the current selected role.
- Show distinct text for "no roll yet" and "no access to roll result".
- Update Unity EditMode coverage for override and audience-aware display.

### Out of scope

- SQLite persistence, game-log list, save/reopen, reroll, cancel, full group-management UI, networking, ADR changes, and Application/Domain/Persistence package changes.

### Allowed paths

```text
Assets/Odyssey/Client/Runtime/RollPanelPresenter.cs
Assets/Odyssey/Client/Tests/EditMode/RollPanelPresenterTests.cs
docs/tasks/active/ODY-UI-01-005_Override_And_Audience_Aware_Display.md
docs/plans/active/ODY-UI-01-005_Override_And_Audience_Aware_Display.md
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

- Module ownership and dependency direction: Unity Client may call Application services; Application/Domain packages must not depend on Unity.
- Authoritative-state and transaction boundary: UI applies overrides only through `DiceRollService.ApplyOverride`; it must not mutate dice state directly.
- Serialization / compatibility boundary: Not applicable; no persisted or transport contract changes.
- Time / RNG rule: roll generation continues through the existing injected RNG factory and clock; override timestamps use the injected clock.
- Unity / thread / lifetime rule: UI callbacks and role subscriptions are owned by `PresentationRuntime`; presenters release subscriptions on dispose.
- Dependency / licensing rule: no new dependencies.
- Security / privacy / redaction rule: result text must be derived only after `DiceRollVisibilityPolicy.TryGetVisibleRoll` grants access.
- Performance or platform constraint: Windows Unity EditMode for this task.
- Other: do not add unsupported `ODY-UI-*` rows to `Tests/Metadata/test-catalog.json`.

## 7. Expected behavior

### Scenario 1 — audience-aware result display

**Given** a Player-created default `PlayerAndGM` roll exists  
**When** the selected role is Player or MainGM  
**Then** the panel displays the roll result after `TryGetVisibleRoll` returns true.

### Scenario 2 — safe denial display

**Given** a Player-created default `PlayerAndGM` roll exists  
**When** the selected role switches to Observer  
**Then** the panel shows "No access to roll result." and no formula/total details are rendered.

### Scenario 3 — MainGM override

**Given** a roll exists and the selected role is MainGM  
**When** the user enters a reason and presses Override  
**Then** `ApplyOverride` succeeds, the stored roll status becomes `Overridden`, and the visible result refreshes through the visibility policy.

### Scenario 4 — override denial

**Given** a roll exists  
**When** Player presses Override or MainGM presses Override without a reason  
**Then** the existing dice service error is shown and the roll's natural results/base total are unchanged.

### Required invariants

- Roll details are never formatted from `LastRoll` before the visibility policy grants access.
- "No roll yet" and "No access to roll result" remain visually distinct text states.
- MainGM-only override controls follow the current role selection.

## 8. Deliverables

- Production code: updated `RollPanelPresenter`.
- Tests: updated Unity EditMode roll panel tests.
- Scripts / CI: None.
- Configuration: None.
- Documentation: this task contract, ExecPlan, backlog status after PR/CI.
- Generated evidence or build artifacts: validation outputs recorded in section 17.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. The roll panel includes an audience selector with `Public`, `PlayerAndGM`, and `SelectedParticipants`.
2. The default selected audience is `PlayerAndGM` and is documented with rationale.
3. `SelectedParticipants` uses a minimal fixture selecting the role selector's `PlayerUserId`; no group-management UI is added.
4. Roll submission uses the selected audience in `SubmitRollRequest`.
5. The result display calls `DiceRollVisibilityPolicy.TryGetVisibleRoll` before showing roll formula, base total, final total, or status.
6. Player and MainGM can see a default `PlayerAndGM` roll; Observer sees safe denial text with no roll details.
7. Override controls are enabled only for MainGM.
8. MainGM override without a reason is rejected visibly and does not change `NaturalResults` or `BaseTotal`.
9. MainGM override with a reason succeeds and stores an `Overridden` roll status through `DiceRollService.ApplyOverride`.
10. Non-MainGM override attempts are rejected by the service and surfaced visibly.
11. No Application/Domain/Persistence package, ADR, serialization contract, dependency, or Unity/package version changes are made.
12. Required validation commands pass and real results are recorded.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `PlayerRoll_DefaultAudience_IsPlayerAndGMAndVisible` | Unity EditMode | Default audience is `PlayerAndGM`; Player sees policy-approved result | Pass |
| `Roll_SelectedParticipantsAudience_SelectsPlayerUser` | Unity EditMode | SelectedParticipants option creates the minimal player-selected audience | Pass |
| `RoleSwitch_ObserverCannotSeePlayerAndGmRoll` | Unity EditMode | Observer safe denial is produced by `TryGetVisibleRoll` | Pass |
| `MainGmOverride_EmptyReason_ShowsErrorAndDoesNotChangeRoll` | Unity EditMode | Mandatory reason denial is visible and leaves roll data unchanged | Pass |
| `MainGmOverride_WithReason_SetsRollStatusOverridden` | Unity EditMode | MainGM override succeeds through `ApplyOverride` | Pass |
| `PlayerOverride_WithReason_ShowsDeniedError` | Unity EditMode | Non-MainGM override attempt is service-denied and surfaced | Pass |
| `RoleSwitch_UpdatesMainGmOnlyButtons` | Unity EditMode | Modifier and override buttons follow role selection | Pass |

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\test-unity.ps1
```

### Manual validation

- None required before PR; the slice's full manual walkthrough belongs to `ODY-UI-01-007`.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Unity `6000.4.0f1`, EditMode tests.
- Scripting backend: editor default; no Player build change.
- Network topology or database fixture: in-memory dice store and in-memory campaign user group directory.
- Other: .NET SDK per `global.json`.

### Validation not required by this task

- SQLite persistence, game-log reconnect, reroll/cancel, full manual walkthrough, real networking, and IL2CPP release build are outside this task.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None expected.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert the branch/PR.
- Data-loss risk and protection: None; in-memory UI state only.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: synthetic dice roll formula, safe role ids, totals visible to entitled viewers, and safe service error reason codes.
- Trust boundaries: UI-to-Application direct call, as already scoped by `SLICE-UI-01`.
- Authorization / audience checks: override authorization and reason validation are enforced by `DiceRollService`; result visibility is enforced by `DiceRollVisibilityPolicy`.
- Redaction requirements: UI must not show roll formula/totals/status when `TryGetVisibleRoll` returns false.
- Log-safe fields: no new logging.
- Abuse / malformed input limits: formula and override reason validation remain in `DiceRollService`.
- Security tests: Observer safe denial and non-GM override denial EditMode tests.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: the task changes Unity UI behavior that affects audience visibility/redaction and uses Application authorization paths, has multiple logical stages, and includes an explicit owner change-control decision.
- ExecPlan path: `docs/plans/active/ODY-UI-01-005_Override_And_Audience_Aware_Display.md`
- Expected pull request count: 1
- Milestone or sequencing constraints: create contract/plan before production edits; update backlog only after PR and CI evidence.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, ExecPlan, `docs/tasks/SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` after PR/CI.
- Documents that must not change: ADRs and private documentation.
- Application version change: No — UI task only.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

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

### Changed files / areas

- `Assets/Odyssey/Client/Runtime/RollPanelPresenter.cs` — audience selector, visibility-policy result display, and MainGM override control.
- `Assets/Odyssey/Client/Tests/EditMode/RollPanelPresenterTests.cs` — updated and expanded EditMode tests for audience and override behavior.
- `docs/plans/active/ODY-UI-01-005_Override_And_Audience_Aware_Display.md` — ExecPlan.
- This task contract.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | Repository policy check passed; `REPO-POLICY-001` through `005` and `TC-CI-001` through `012` pass. |
| `dotnet build DotNet\Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. Initial sandbox run failed because `C:\Users\alexx\AppData\Local\Microsoft SDKs` was denied; approved rerun passed. |
| `dotnet test DotNet\Odyssey.Core.sln` | Passed | Contracts 1/1, Domain 27/27, Networking 67/67, Unit 105/105, Architecture 2/2, Persistence 60/60. |
| `.\scripts\test-unity.ps1` | Passed | First run failed one new assertion (`Has.Count` on audience collection under Unity/NUnit); fixed to explicit `.Count`. Rerun passed: EditMode total=55 passed=55 failed=0 skipped=0; PlayMode total=2 passed=2 failed=0 skipped=0. |
| `.\scripts\test-fast.ps1` | Passed | `TC-DOTNET-001` pass; Contracts 1/1, Domain 27/27, Networking 67/67, Unit 105/105, Architecture 2/2, Persistence 60/60. |
| `.\scripts\verify-repository.ps1` | Passed | `REPOSITORY-VERIFY PASS repository checks passed`, SDK configured/selected `10.0.302`. |
| `.\scripts\build-dev.ps1` | Passed | `BuildId=odyssey-development-1787872587.1-g1064758ef73a`; executable emitted under `artifacts\builds\odyssey-development-1787872587.1-g1064758ef73a\Windows-x64\Odyssey.exe`. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `roll-audience` dropdown contains `Public`, `PlayerAndGM`, and `SelectedParticipants`. |
| AC-2 | Passed | Default dropdown value is `PlayerAndGM`; covered by `PlayerRoll_DefaultAudience_IsPlayerAndGMAndVisible`. |
| AC-3 | Passed | `SelectedParticipants` builds selected user/group fixture from `RoleSelection.PlayerUserId`; covered by `Roll_SelectedParticipantsAudience_SelectsPlayerUser`. |
| AC-4 | Passed | `SubmitRoll` passes `SelectedAudience()` into `SubmitRollRequest`. |
| AC-5 | Passed | `RefreshResultDisplay` calls `DiceRollVisibilityPolicy.TryGetVisibleRoll` before `FormatRoll`. |
| AC-6 | Passed | `RoleSwitch_ObserverCannotSeePlayerAndGmRoll` proves Player sees the roll, then Observer sees no details. MainGM visibility is covered by MainGM roll/override tests. |
| AC-7 | Passed | `ApplyRoleState` enables the override button only for MainGM; covered by `RoleSwitch_UpdatesMainGmOnlyButtons`. |
| AC-8 | Passed | `MainGmOverride_EmptyReason_ShowsErrorAndDoesNotChangeRoll` verifies visible error and unchanged roll data. |
| AC-9 | Passed | `MainGmOverride_WithReason_SetsRollStatusOverridden` verifies `ApplyOverride` success and refreshed `Overridden` status. |
| AC-10 | Passed | `PlayerOverride_WithReason_ShowsDeniedError` verifies service denial and visible safe reason. |
| AC-11 | Passed | Diff is limited to Unity Client presenter/tests plus task/plan docs; no `Packages/`, ADR, dependency, or version files are intentionally changed. |
| AC-12 | Passed | Required local commands passed with real results above. |

### Build and artifact evidence

- Build identity: `odyssey-local-20260827t231234z-g17c04b725770-dirty` from successful `test-unity.ps1`; development build `odyssey-development-1787872587.1-g1064758ef73a`.
- Artifact path / name: None expected.
- Checksums: None.
- Test or quality report: `Logs/ODY-S00-008/editmode-results.xml`, `Logs/ODY-S00-008/playmode-results.xml`, and dotnet `.trx` files under `Logs/ODY-S00-008/dotnet/`.

### Known limitations

- Persistence, game-log display, reroll/cancel, and full manual walkthrough remain assigned to later `SLICE-UI-01` tasks.

### Follow-up tasks

- `ODY-UI-01-006` — persistence and game log.
- `ODY-UI-01-007` — reroll/cancel and full manual walkthrough.

### Self-review summary

- Scope review: implementation is limited to Unity Client presenter/tests plus task/plan docs before PR evidence; no Application/Domain/Persistence package source touched.
- Architecture review: Unity Client calls existing Application service and visibility contracts directly; no dependency direction change.
- Test review: 11 RollPanel EditMode tests now cover roll, modifier, override, and audience display behavior; full Unity and .NET suites pass locally.
- Security/privacy review: roll details are formatted only after `TryGetVisibleRoll` grants access; observer denial text contains no formula or totals.
- Documentation/version review: no schema, package, ADR, or version update required.

## 18. Blockers, decisions, and change control

### Blockers

- None after owner clarification.

### Decisions made during execution

- 2026-08-27 — Decision: the original task conflict is resolved by removing the "do not add audience beyond Public" constraint. Authority / approval: product owner clarification via PM after Codex reported that `Public` is correctly visible to Observer under `DiceRollVisibilityPolicy`.
- 2026-08-27 — Decision: add a minimal audience selector with `Public`, `PlayerAndGM`, and `SelectedParticipants`. Authority / approval: product owner clarification via PM.
- 2026-08-27 — Decision: default the panel to `PlayerAndGM`, not `Public`, so Observer safe denial is meaningful immediately; choose `PlayerAndGM`, not `SelectedParticipants`, because it proves the same Observer exclusion with less fixture state while leaving `SelectedParticipants` available in the dropdown. Authority / approval: product owner clarification via PM.
- 2026-08-27 — Decision: implement `SelectedParticipants` as a minimal fixture selecting `RoleSelection.PlayerUserId` and backed by one in-memory group containing that user, with no group-management UI. Authority / approval: product owner clarification via PM and `ADR-021` §4 scope boundary.
- 2026-08-27 — Decision: extend the existing `RollPanelPresenter` instead of adding a second presenter. Authority / approval: current code ownership; `RollPanelPresenter` already owns `LastRoll`, role subscription, status/result labels, and dice service dependencies.
- 2026-08-27 — Finding: the first real Unity run failed one new assertion because Unity's NUnit runner did not accept `Has.Count` for the audience collection shape; changed the test to assert the explicit `.Count` value. Authority / approval: test-only correction preserving the same acceptance proof.

### Approved task changes

- 2026-08-27 — Removed the original "no audience beyond Public" non-goal and added audience selector scope — Approved by: product owner clarification via PM.
