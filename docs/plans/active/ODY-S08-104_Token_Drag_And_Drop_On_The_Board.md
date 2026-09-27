# ExecPlan — ODY-S08-104 Token Drag-and-Drop on the Board

## 1. Purpose
Add mouse dragging of an already-placed token, coexisting with camera pan and asset-pool drag, without breaking the existing click-select/click-move flow.

## 2. Scope
`BoardScreenPresenter.cs` only (per-token `BoardPointerGesture`, `TryMoveTokenTo`, `BeginTokenDrag`/`MoveTokenDrag`/`EndTokenDrag`, real pointer wiring); tests; catalog; backlog closure (block 3b) plus repair of a corrupted prior revision; this contract and plan.

## 3. Non-goals
`BoardPointerGesture.cs`/`BoardCamera.cs`/`AssetPoolPresenter.cs`/`NativeFileDialog.cs`/`AssetTextureCache.cs`/backend/persistence (all untouched); token z-order/scale/selection.

## 4. Architecture
Per-token `BoardPointerGesture` + `CapturePointer`, same shape as the pool's own item drag. `PointerMove` -> in-memory preview only (`_tokenPositionsByTokenId`/`PositionTokenElement`). `PointerUp` -> click (`SelectToken`) or drag commit (`TryMoveTokenTo`, exactly once). Rollback via the existing `Refresh()`-on-failure convention.

## 5. Milestones
1. Per-token gesture + capture wiring, remove `ClickEvent`. 2. `TryMoveTokenTo` + `BeginTokenDrag`/`MoveTokenDrag`/`EndTokenDrag`. 3. Tests (`TC-BOARD-072`-`077`, `MoveCountingSceneRepository`). 4. Repair the corrupted `SLICE-08` backlog and close block 3b. 5. Unity batchmode run, `dotnet test`, verify scripts. 6. PR.

## 6-8. State/flow, error handling, test strategy
See task contract §9 and §18.

## 9. Validation and acceptance evidence
Unity 6000.4.0f1 batchmode EditMode run (exit code, pass/fail count); `dotnet build`/`dotnet test` (unaffected, confirmed green); verify-format/verify-repository/verify-test-structure; diff-scope check limited to `BoardScreenPresenter.cs`, its tests, the catalog, and docs. **Not yet completed this session** — see the chat status report.

## 10. Recovery and rollback
Revert the PR; no data migration.
