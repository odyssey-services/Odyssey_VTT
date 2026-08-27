# ODY-UI-01-004 — Roll Panel and Modifiers

**Status:** Active  
**Owner:** Codex (agent)  
**Branch:** `feat/ody-ui-01-004-roll-panel-and-modifiers`  
**Pull request:** Not opened  
**Last updated:** 2026-08-27 18:51 UTC

## 1. Purpose and user-visible outcome

The minimal trial UI gains a roll panel where Player/MainGM can submit a dice formula, Observer sees a denied state, and MainGM can decide proposed modifiers. This makes `ODY-S03-008` roadmap steps 2-5 hand-walkable through UI.

## 2. Task contract

- Goal: add a UI Toolkit roll panel calling existing `DiceRollService.SubmitRoll`, `ProposeModifier`, and `DecideModifier` using the current `RoleSelection`.
- Acceptance criteria: see `docs/tasks/active/ODY-UI-01-004_Roll_Panel_And_Modifiers.md` §9.
- Requirement IDs: `ODY-UI-01-004`, roadmap §12.6 steps 2-5 as summarized by `ODY-S03-008`.
- In scope: roll formula field/button, modifier propose and MainGM decide controls, visible safe errors, `LastRoll`, EditMode tests.
- Out of scope: override, audience-aware result display, persistence/log, reroll/cancel, Application/Domain/Persistence package changes, ADR changes, dependencies.
- Required authorities: task contract §3.
- Required validation commands: `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `dotnet build DotNet\Odyssey.Core.sln`, `dotnet test DotNet\Odyssey.Core.sln`, `.\scripts\test-unity.ps1`.

## 3. Current state

- `RoleSelection` already provides `RoleSelectionSnapshot` with actor id, MainGM flag, and `ActorCanCreateRoll`.
- `DiceRollService` already implements the required roll and modifier behavior through public request types.
- `PresentationRuntime` already owns button/selection subscriptions for UI Toolkit presenters.
- `Assets/Odyssey/Client/Runtime/Odyssey.Unity.Client.Runtime.asmdef` and the EditMode test asmdef already reference `Odyssey.Application`.
- No `RollPanelPresenter` exists yet.

## 4. Proposed approach

Add one Client-layer presenter that owns a task-local `DiceRollStore`, injected RNG factory, injected clock, campaign/ruleset/epoch ids, and the existing `RoleSelection`. The presenter builds a compact UI Toolkit view, wires buttons through `PresentationRuntime`, calls `DiceRollService` directly, and stores the returned roll in `LastRoll` only on success. A small EditMode test suite exercises the presenter public methods and visible labels/buttons without adding new packages or changing application contracts.

The default audience is `DiceRollAudience.Public()` because this task does not render audience-filtered results; `ODY-UI-01-005` owns audience-aware display. Public keeps this task's UI behavior observable without inventing hidden visibility semantics.

## 5. Milestones

### M1 — Contract and plan ready

- [x] Create task contract under `docs/tasks/active`.
- [x] Create ExecPlan under `docs/plans/active`.
- [x] Record default audience and `LastRoll` decisions before production edits.

### M2 — Roll panel presenter

- [x] Add `RollPanelPresenter` and `.meta`.
- [x] Build UI fields/buttons/status labels.
- [x] Wire `SubmitRoll`, `ProposeModifier`, and `DecideModifier` to existing Application service calls.

### M3 — Unity EditMode coverage

- [x] Add roll panel EditMode tests and `.meta`.
- [x] Prove Player/MainGM success, Observer denial, modifier accept/change/reject, and non-GM denial.

### M4 — Validation, PR, CI, and backlog evidence

- [x] Run required local commands and record real results.
- [x] Self-review complete diff for scope/architecture/privacy.
- [ ] Open Draft PR and wait for green CI.
- [ ] Update task contract/backlog with PR and CI evidence.

## 6. Progress log

- 2026-08-27 18:32 UTC — Preflight completed: fetched `origin/main`, fast-forwarded `main`, confirmed PR #67/#69/#70 in recent history, and created `feat/ody-ui-01-004-roll-panel-and-modifiers`.
- 2026-08-27 18:32 UTC — Read required backlog, dice contracts/service, role selection/presenter, board presenter, `ODY-S03-008`, `TASK_TEMPLATE.md`, and `PLANS.md`.
- 2026-08-27 18:32 UTC — Created task contract and ExecPlan before production edits.
- 2026-08-27 18:43 UTC — Added `RollPanelPresenter` and six EditMode tests.
- 2026-08-27 18:43 UTC — Local validation passed: `verify-format`, `check-repository-policy`, `dotnet build`, `dotnet test`, and `test-unity`.
- 2026-08-27 18:51 UTC — Initial PR CI exposed unsupported `ODY-UI-*` test-catalog ownership; removed those catalog rows and reran `verify-test-structure`/`test-fast` successfully.

## 7. Decisions

- 2026-08-27 — Decision: use `DiceRollAudience.Public()` as this task's default. Rationale: this task needs a visible roll result but does not own audience-aware rendering; `Public` is the smallest non-surprising default and task 005 owns visibility policy UI. Authority: `SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` §5.
- 2026-08-27 — Decision: expose `LastRoll` and `LastRollChanged`, not a history list. Rationale: later UI tasks need the current roll, while roll history/game log is explicitly out of scope until `ODY-UI-01-006`. Authority: task scope and backlog §5.
- 2026-08-27 — Decision: keep a task-local in-memory `DiceRollStore`. Rationale: it matches `DiceRollService`'s current contract and avoids persistence work assigned to `ODY-UI-01-006`. Authority: `DiceRollStore` and task out-of-scope list.

## 8. Discoveries and deviations

- Unity's first EditMode run failed because the new test fixed clock used `2026-08-27T18:32:00Z`; the existing `UtcInstant.Parse` contract requires seven fractional digits. The test was corrected to `2026-08-27T18:32:00.0000000Z`.
- Unity generated package `.meta` drift and `ProjectSettings.asset` whitespace drift during test runs. They were removed/restored before commit because they were unrelated to this task.
- Initial PR CI failed because `Tests/Metadata/test-catalog.json` rows with `ODY-UI-01-004` violate `verify-test-structure.ps1`'s current `ODY-SNN-NNN` task-id pattern. Removed the unsupported catalog rows and kept the UI coverage documented by method name in the task contract.

## 9. Validation and acceptance evidence

- `.\scripts\verify-format.ps1` — Passed, `FORMAT-001 PASS`.
- `.\scripts\check-repository-policy.ps1` — Passed, repository policy and CI workflow checks pass.
- `.\scripts\verify-test-structure.ps1` — Passed after removing unsupported UI task-id catalog rows.
- `.\scripts\test-fast.ps1` — Passed after approved rerun outside sandbox SDK read restriction.
- `.\scripts\verify-repository.ps1` — Passed, `REPOSITORY-VERIFY PASS`.
- `dotnet build DotNet\Odyssey.Core.sln` — Passed, 0 warnings, 0 errors after approved rerun outside sandbox SDK read restriction.
- `dotnet test DotNet\Odyssey.Core.sln` — Passed: Contracts 1, Domain 27, Networking 67, Unit 105, Architecture 2, Persistence 60.
- `.\scripts\test-unity.ps1` — Passed: EditMode total=50 passed=50; PlayMode total=2 passed=2.

## 10. Recovery and rollback

No migrations or durable state changes are planned. Revert this branch/PR to remove the UI presenter, tests, and task documentation updates. If Unity test runs produce generated project-setting or package metadata drift, review it and remove only generated/unrelated files before commit.

## 11. Open questions and blockers

- None.

## 12. Outcome and follow-up

Implementation and local validation are complete. Draft PR, CI evidence, and backlog update remain pending.
