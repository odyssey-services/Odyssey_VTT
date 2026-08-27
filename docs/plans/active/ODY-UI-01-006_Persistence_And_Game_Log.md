# ODY-UI-01-006 — Persistence and Game Log

**Status:** Active  
**Owner:** Codex (agent)  
**Branch:** `feat/ody-ui-01-006-persistence-and-game-log`  
**Pull request:** [#73](https://github.com/odyssey-services/Odyssey_VTT/pull/73)  
**Last updated:** 2026-08-27 23:51 UTC

## 1. Purpose and user-visible outcome

The minimal UI can save the current roll, simulate reopening the campaign database, and show the role-filtered game log.

## 2. Task contract

- Goal: add a separate game-log presenter using `SqliteGameLogRepository` and `GameLogReconnectService`.
- Acceptance criteria: see `docs/tasks/active/ODY-UI-01-006_Persistence_And_Game_Log.md` §9.
- Requirement IDs: `ODY-UI-01-006`; roadmap §12.6 steps 8-9.
- In scope: save/reopen action, persisted summary list, current-role filtering, same campaign handle, EditMode tests.
- Out of scope: reroll/cancel, full manual walkthrough, search/archive/export, Application/Domain/Persistence package changes, ADRs.
- Required authorities: Active Baseline v2.2, `AGENTS.md`, `PLANS.md`, UI backlog §5, `ODY-S03-008`, game-log repository contracts, SQLite implementation, reconnect service, current roll/board presenters.
- Required validation commands: `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `dotnet build DotNet\Odyssey.Core.sln`, `dotnet test DotNet\Odyssey.Core.sln`, `.\scripts\test-unity.ps1`.

## 3. Current state

- `main` contains merged `ODY-UI-01-002` through `ODY-UI-01-005`.
- `RollPanelPresenter` has `LastRoll`/`LastRollChanged` and default `PlayerAndGM` audience.
- `BoardScreenDemoCampaign` creates one real SQLite-backed campaign and returns a `CampaignHandle`.
- `SqliteGameLogRepository` already implements save, replay by `CommandId`, and listing.
- `GameLogReconnectService` already implements audience filtering for persisted entries.

## 4. Proposed approach

Add a small `GameLogPresenter` that depends on `RoleSelection`, `PresentationRuntime`, `RollPanelPresenter`, `CampaignHandle`, `IWallClock`, and `ICampaignUserGroupDirectory`. The presenter builds a button plus a `ScrollView`. Pressing the button saves `RollPanelPresenter.LastRoll` with a generated `CommandId`, constructs a fresh `SqliteGameLogRepository`, calls `ListGameLog`, filters via `GameLogReconnectService.GetVisibleEntries`, and renders each visible `SummaryPayload`.

Expose a method that accepts a caller-provided `CommandId` for tests and idempotent replay verification; the button path uses a new id. Store listed entries in the presenter so role changes can re-filter without another save.

No board code change is needed: both board and log presenters can receive the same `CampaignHandle` from `BoardScreenDemoCampaign` or tests. This avoids a second demo database without broad composition refactoring.

## 5. Milestones

### M1 — Contract and plan

- [x] Task contract created.
- [x] ExecPlan created before production edits.

### M2 — Game-log presenter

- [x] New presenter builds save button and scrollable list.
- [x] Save/reopen uses `SqliteGameLogRepository` and supplied `CampaignHandle`.
- [x] Render path filters through `GameLogReconnectService`.

### M3 — Tests and local validation

- [x] EditMode tests cover save/reopen, role filtering, idempotent replay, and supplied campaign handle.
- [x] Required local commands pass.
- [x] Unity-generated drift, if any, is removed from the working tree.

### M4 — PR and evidence

- [x] Draft PR is opened.
- [x] CI passes.
- [x] Task contract, ExecPlan, and `SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` record final evidence.

## 6. Progress log

- 2026-08-27 23:32 UTC — Preflight completed: `main` fast-forwarded to `origin/main`, PR #72 present, branch `feat/ody-ui-01-006-persistence-and-game-log` created.
- 2026-08-27 23:32 UTC — Read required sources: UI backlog, game-log repository contracts, SQLite implementation, reconnect service, current roll/board presenters, and relevant tests.
- 2026-08-27 23:32 UTC — Created task contract and ExecPlan.
- 2026-08-27 23:46 UTC — Implemented `GameLogPresenter` with save/reopen/list behavior and audience filtering through `GameLogReconnectService`.
- 2026-08-27 23:46 UTC — Added Unity EditMode coverage for save/reopen restore, Player/MainGM/Observer visibility, same-command idempotency, and supplied campaign handle reuse.
- 2026-08-27 23:48 UTC — Local validation passed: format, repository policy, .NET build/test, fast tests, Unity EditMode/PlayMode, development build, and repository verify.
- 2026-08-27 23:51 UTC — Draft PR #73 opened; CI run 33127771319 passed all four required checks.

## 7. Decisions

- 2026-08-27 — Decision: use a separate `GameLogPresenter`. Rationale: the task asks for it and keeps `RollPanelPresenter` from absorbing persistence/list UI.
- 2026-08-27 — Decision: require a supplied `CampaignHandle`. Rationale: it lets board and log use the same demo campaign database without changing `BoardScreenPresenter`.
- 2026-08-27 — Decision: simulate reopen with a new `SqliteGameLogRepository` instance. Rationale: this matches existing `ODY-S03-007` tests and proves there is no shared in-memory log state.

## 8. Discoveries and deviations

None.

## 9. Validation and acceptance evidence

- `.\scripts\verify-format.ps1`: pass.
- `.\scripts\check-repository-policy.ps1`: pass.
- `dotnet build DotNet\Odyssey.Core.sln`: pass, 0 warnings, 0 errors.
- `dotnet test DotNet\Odyssey.Core.sln`: pass; 262 .NET tests across six assemblies.
- `.\scripts\test-fast.ps1`: pass, 0 warnings, 0 errors.
- `.\scripts\test-unity.ps1`: pass; Unity compile exit 0, EditMode 59/59, PlayMode 2/2.
- `.\scripts\build-dev.ps1`: pass; BuildId `odyssey-development-1787874513.1-gadda59cd8e0d`.
- `.\scripts\verify-repository.ps1`: pass.
- PR CI run [33127771319](https://github.com/odyssey-services/Odyssey_VTT/actions/runs/33127771319): pass; `buildidentity-provenance`, `dotnet-restore-build-test`, `repository-policy-format-structure`, and `unity-project-package-static`.

## 10. Recovery and rollback

Rollback is a normal branch/PR revert. No persisted schema, package versions, or migrations are changed; tests use temporary campaign directories.

## 11. Open questions and blockers

None.

## 12. Outcome and follow-up

Implementation complete locally and PR CI passed. Follow-up remains `ODY-UI-01-007`.
