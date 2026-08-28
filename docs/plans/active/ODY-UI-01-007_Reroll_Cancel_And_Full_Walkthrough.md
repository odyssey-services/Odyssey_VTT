# ODY-UI-01-007 — Reroll Cancel and Full Manual Walkthrough

**Status:** Active  
**Owner:** Codex (agent)  
**Branch:** `feat/ody-ui-01-007-reroll-cancel-and-full-walkthrough`  
**Pull request:** Not opened  
**Last updated:** 2026-08-28 10:23 UTC

## 1. Purpose and user-visible outcome

The product owner can launch the existing Unity Bootstrap/AppShell flow, open one minimal trial UI screen, and manually walk the assembled board, role selector, roll panel, reroll/cancel controls, and game log together.

## 2. Task contract

- Goal: add reroll/cancel controls and a composed Play Mode-visible trial screen for the ten-step `ODY-S03-008` walkthrough.
- Acceptance criteria: see `docs/tasks/active/ODY-UI-01-007_Reroll_Cancel_And_Full_Walkthrough.md` §9.
- Requirement IDs: `ODY-UI-01-007`, `ODY-S03-008` ten-step scenario.
- In scope: Unity Client presenter changes, trial composition screen, automated presenter walkthrough, manual instructions, task/backlog docs.
- Out of scope: visual polish, production UI, real networking, ADRs, lower package changes, schema/dependency/version changes.
- Required authorities: Active Baseline v2.2, `AGENTS.md`, `PLANS.md`, UI backlog §5, `ODY-S03-008`, dice service/contracts, runtime composition/AppShell, existing presenters.
- Required validation commands: `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `dotnet build DotNet\Odyssey.Core.sln`, `dotnet test DotNet\Odyssey.Core.sln`, `.\scripts\test-fast.ps1`, `.\scripts\test-unity.ps1`, `.\scripts\build-dev.ps1`, `.\scripts\verify-repository.ps1`.

## 3. Current state

- `main` includes merged `ODY-UI-01-002` through `006`; preflight fast-forward ended at merge commit `35fc3da`.
- The existing runtime host starts diagnostics/runtime composition, loads `AppShell.unity`, and initializes `DeveloperShellPresenter`.
- The board, roll panel, and game log presenters are separate code-built UI Toolkit classes used by tests.
- `RollPanelPresenter` already has the dice store, RNG factory, clock, campaign id, role selection, audience selection, modifier, override, result display, `LastRoll`, and `LastRollChanged`.
- `GameLogPresenter` already takes the same `CampaignHandle` and roll presenter.
- No existing presenter assembles the full trial UI into a launchable Play Mode screen.

## 4. Proposed approach

Keep the current Bootstrap/AppShell runtime path. Add a launch button to `DeveloperShellPresenter`; when clicked, `AppShellEntryPoint` swaps the existing root UI to a new `TrialScreenPresenter`. The trial presenter creates one demo campaign, one shared `RoleSelection`, one shared `DiceRollStore`, the existing board presenter, roll presenter, and game-log presenter. It renders one role selector at the top and suppresses duplicate internal role selectors in child presenters.

Extend `RollPanelPresenter` with two small controls: a cancel reason text field plus Reroll and Cancel buttons. Both call existing `DiceRollService` APIs and update `LastRoll`/status via the same path as submit/modifier decisions.

Use EditMode tests for the full presenter walkthrough because they can directly exercise presenter methods and inspect UI Toolkit elements without fragile frame/pointer dispatch. Keep a PlayMode smoke assertion for the existing Bootstrap/AppShell launch button so the owner path is also covered.

## 5. Milestones

### M1 — Contract and plan

- [x] Preflight verifies `ODY-UI-01-006` is merged.
- [x] Task contract is created.
- [x] ExecPlan is created before production edits.

### M2 — Reroll and cancel controls

- [x] `RollPanelPresenter` renders Reroll/Cancel controls.
- [x] Reroll uses `RequestFullReroll` and updates `LastRoll`.
- [x] Cancel uses `CancelRoll` with a reason and updates visible status/result.
- [x] Focused Unity tests prove lifecycle state and immutable roll data preservation.

### M3 — Composed trial screen and launch path

- [x] `TrialScreenPresenter` composes board, one role selector, roll panel, and game log with shared `RoleSelection` and `CampaignHandle`.
- [x] Existing `DeveloperShellPresenter`/`AppShellEntryPoint` path can launch the trial screen.
- [x] PlayMode or EditMode coverage proves the launch path renders the trial UI root.

### M4 — Full walkthrough and validation

- [x] One automated test walks all ten `ODY-S03-008` steps through the composed presenters.
- [ ] Required local validation commands pass.
- [x] Unity-generated drift is removed from the working tree.

### M5 — PR and final evidence

- [ ] Draft PR is opened.
- [ ] CI passes.
- [ ] Task contract, ExecPlan, and `SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` record final evidence and close `SLICE-UI-01`.

## 6. Progress log

- 2026-08-28 10:09 UTC — Preflight completed: PR #73 merged, `main` fast-forwarded to `35fc3da`, branch `feat/ody-ui-01-007-reroll-cancel-and-full-walkthrough` created.
- 2026-08-28 10:09 UTC — Read required task, UI backlog, `ODY-S03-008`, dice service/contracts, runtime composition, AppShell entry point, and current board/roll/log presenters.
- 2026-08-28 10:09 UTC — Created task contract and ExecPlan.
- 2026-08-28 10:23 UTC — Implemented reroll/cancel controls, composed `TrialScreenPresenter`, DeveloperShell launch path, focused lifecycle tests, full EditMode walkthrough, and PlayMode launch smoke.
- 2026-08-28 10:23 UTC — Local validation passed for format, repository policy, .NET build/test, fast tests, Unity tests, and repository verify. `build-dev` first attempt hit the expected clean-tree precondition and will be rerun after commit.

## 7. Decisions

- 2026-08-28 — Decision: use an ExecPlan. Rationale: the task changes multiple Unity Client runtime components and validates persistence/audience/state transitions together. Authority: `PLANS.md` §1.
- 2026-08-28 — Decision: launch the trial screen from `DeveloperShellPresenter` inside the existing `AppShell` scene. Rationale: it reuses the current Bootstrap/AppShell composition path and avoids a second scene/bootstrap mechanism for a trial UI. Authority: task §1/§4 and `AppShellEntryPoint`/`OdysseyRuntimeHost` current design.
- 2026-08-28 — Decision: use EditMode for the full ten-step presenter walkthrough and PlayMode for the launch smoke check. Rationale: EditMode gives stable direct presenter control for all scenario assertions; PlayMode covers the owner-visible entry point without brittle synthetic pointer choreography. Authority: task §4 allows choosing the format with justification.
- 2026-08-28 — Decision: render a single top-level role selector in the composed trial screen and suppress duplicate child role selectors. Rationale: both child presenters already consume the same `RoleSelection`; only their duplicate controls need to be omitted for one clean screen. Authority: task §1 requires all elements together.

## 8. Discoveries and deviations

- `.\scripts\build-dev.ps1` correctly refuses to build from a dirty tree; first attempt before commit failed with the clean-tree provenance precondition. This is not a code failure and will be rerun after the implementation commit.

## 9. Validation and acceptance evidence

- `.\scripts\verify-format.ps1`: pass.
- `.\scripts\check-repository-policy.ps1`: pass.
- `dotnet build DotNet\Odyssey.Core.sln`: pass, 0 warnings, 0 errors.
- `dotnet test DotNet\Odyssey.Core.sln`: pass; 262 .NET tests across six assemblies.
- `.\scripts\test-fast.ps1`: pass, 0 warnings, 0 errors.
- `.\scripts\test-unity.ps1`: pass; Unity compile exit 0, EditMode 62/62, PlayMode 3/3.
- `.\scripts\verify-repository.ps1`: pass.
- `.\scripts\build-dev.ps1`: pending clean-tree rerun after commit.

## 10. Recovery and rollback

Rollback is a normal branch/PR revert. No lower-package production code, persisted schema, dependency, package, Unity version, or ADR change is planned.

## 11. Open questions and blockers

None.

## 12. Outcome and follow-up

Pending implementation.
