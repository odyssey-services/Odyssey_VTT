# ExecPlan — ODY-S10-111 SLICE-10 Block 6 Part 1: Obstacle Drawing UI

## 1. Purpose
Add the first client-side UI for SLICE-10's server-only Blocks 1-5: draw walls/doors/windows by mouse, toggle a door by clicking it, and show/manually apply damage to a destructible obstacle's HP -- entirely through the existing, unmodified server commands.

## 2. Scope
Four new Runtime files (`BoardTool.cs`, `BoardObstacleDrawGesture.cs`, `BoardHitTestMath.cs`, `BoardToolbarPresenter.cs`), point-edits to `BoardScreenPresenter.cs`/`TrialScreenPresenter.cs`, three new test files, point-edits to three existing EditMode test files and the PlayMode smoke test file, the catalogue, the `SLICE-10` backlog, this plan and the contract.

## 3. Non-goals
Fog-of-war rendering, cover preview, facing/FOV UI (Block 6 part 2); final art; a batched durability read; undo/history/segment editing; a full notification system; any server-layer change.

## 4. Architecture
`BoardObstacleDrawGesture`/`BoardHitTestMath` are plain C# (no UnityEngine/UI Toolkit dependency), exercised directly with numbers -- the same testability shape `BoardCamera`/`BoardPointerGesture`/`BoardBoxSelectGesture` already established. `BoardToolbarPresenter` is a small, focused presenter (constructor-injected callback, `BuildView`/`SetActiveTool`/`SetVisible`), the same shape `RoleSelectorPresenter` already established, not folded into `BoardScreenPresenter`. `BoardScreenPresenter` itself gains: an `IObstacleRepository` (constructor-injected like `ICampaignRepository`), obstacle rendering/HP-bar/draw-preview/damage-inspector elements sharing the token render loop's own full-reconciliation `Refresh()` pass, and drawing-gesture routing spliced into the existing button-down/move/up handlers exactly where the tool decides which of two gestures (draw vs. the pre-existing box-select/click) a left-button press starts. Every server call (`CreateObstacle`/`ToggleDoorState`/`ApplyObstacleDamage`/`ListObstacles`/`GetObstacleDurability`) follows the exact same direct-call/read-revision-fresh/`Refresh()` pattern every pre-existing command in this class already uses -- no adapter, no new call shape.

## 5. Milestones
1. Recon confirmation that zero client code references any SLICE-10 Block 1-5 command, and of `BoardCamera`/`BoardPointerGesture`/`BoardBoxSelectGesture`/`HitTestToken`'s own exact shapes (done in the task's own §0). 2. `BoardTool.cs`, `BoardObstacleDrawGesture.cs`, `BoardHitTestMath.cs` plus their pure-logic tests. 3. `BoardToolbarPresenter.cs`. 4. `BoardScreenPresenter.cs` point-edits: constructor parameter, obstacle rendering/HP bars, drawing-gesture routing, click handling (door toggle/selection), damage panel. 5. The four existing-constructor-call-site mechanical fixes. 6. `BoardScreenPresenterObstacleTests.cs` and the one PlayMode smoke test; a mutation check. 7. The disclosed PlayMode test-ordering fragility fix. 8. Catalogue, backlog, contract, plan. 9. Full Unity batchmode compile + EditMode + PlayMode validation and Draft PR.

## 6-8. State/flow, error handling, test strategy
See task contract section 9 and section 18.

## 9. Validation and acceptance evidence
Unity 6000.4.0f1 batchmode: compile (0 errors), EditMode 136/136, PlayMode 6/6 (`scripts/test-unity.ps1`); `verify-format`/`verify-repository`/`verify-test-structure`; a mutation proving the drawing gesture's own commit-threshold test bites; `git diff --name-status origin/main` reviewed against the forbidden-path list (confirming no server-layer file is touched).

## 10. Recovery and rollback
Revert the PR; no schema or server-side change at all (this task is entirely client-side, reading/writing through already-existing, unmodified server commands).
