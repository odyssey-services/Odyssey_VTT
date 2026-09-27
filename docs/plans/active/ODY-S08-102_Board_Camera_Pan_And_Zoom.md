# ExecPlan — ODY-S08-102 Board Camera: Pan and Zoom

## 1. Purpose
Replace `BoardScreenPresenter`'s fixed coordinate transform with a real, pannable, zoomable camera, without disturbing the existing click-to-select/click-to-move flow.

## 2. Scope
`BoardCamera`, `BoardPointerGesture` (new pure C# types); `BoardScreenPresenter.cs` wiring; `BoardCameraTests.cs`, `BoardPointerGestureTests.cs` (new); additions to `BoardScreenPresenterTests.cs`; `test-catalog.json`; backlog; this contract and plan.

## 3. Non-goals
Backend/persistence, `BoardMovementService`'s own logic, drag-and-drop, file picker, token z-order/scale/selection, camera persistence/sync, touch/pinch, board-bounds clamping.

## 4. Architecture
`BoardCamera`: `pixel = (world - offset) * scale`, mutated by `Pan`/`Zoom`, clamped scale. `BoardPointerGesture`: down/move/up state machine with a pixel-distance threshold deciding click vs. drag. `BoardScreenPresenter`: owns one instance of each; pointer/wheel callbacks on the empty board area are thin wrappers over public `BeginBoardPointerGesture`/`MoveBoardPointer`/`EndBoardPointerGesture`/`ZoomBoard` methods (the same testability shape as `SelectToken`/`TryMoveSelectedTokenTo`); tokens stop `PointerDownEvent` propagation so a token click never starts a pan.

## 5. Milestones
1. `BoardCamera`/`BoardPointerGesture` pure types + unit tests. 2. Presenter wiring (pointer/wheel callbacks, token positioning via camera, token pointer-down stop-propagation). 3. Presenter-level integration tests (click/drag split, zoom repositioning). 4. Unity batchmode compile + EditMode run. 5. Docs, catalog, PR.

## 6. State and data flow
Pointer/wheel event -> presenter callback -> `BoardCamera`/`BoardPointerGesture` (pure) -> `RepositionTokens()` (DOM only) or, for a real click, the unchanged `TryMoveSelectedTokenTo` -> `BoardMovementService.MoveToken` -> repository -> `Refresh()`.

## 7. Error handling
No new error paths; camera math has no failure mode besides the documented `ArgumentOutOfRangeException` for a non-positive zoom factor (a programming error, not a runtime condition the UI can trigger).

## 8. Test strategy
Pure numeric tests for the two new types (no simulated events, matching the existing precedent); presenter-level tests drive `BeginBoardPointerGesture`/`MoveBoardPointer`/`EndBoardPointerGesture`/`ZoomBoard` directly with plain pixel numbers to prove the click/drag split and the zoom repositioning, exactly as `SelectToken`/`TryMoveSelectedTokenTo` are already tested.

## 9. Validation and acceptance evidence
Unity 6000.4.0f1 batchmode EditMode run (exit code, pass/fail counts recorded in the task contract and PR description); `dotnet test` (unaffected, confirmed green anyway); `git diff --name-status` scope check.

## 10. Recovery and rollback
Revert the PR; no data migration, no persisted state involved.
