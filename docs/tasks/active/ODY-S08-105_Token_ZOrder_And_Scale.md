# ODY-S08-105 — Token Z-Order and Scale (SLICE-08 block 5, part 1)

## 1. Task identity
`ODY-S08-105`; status: In Review (Draft PR, merge deferred to the product owner). Implements the z-order and scale half of block 5 of `SLICE-08` (`docs/tasks/SLICE-08_IMPLEMENTATION_BACKLOG.md`); multi-selection is split out as the reserved item `ODY-S08-106` (backlog section 8). First `SLICE-08` task that changes backend schema.

## 2. Goal
Let overlapping tokens be ordered (the one clicked comes to the front) and let a token be resized with Shift+mouse wheel, both persisted on the token, without changing camera zoom, click/select/drag, or movement rules.

## 3. Authority
This task's governing ТЗ; backlog block 5; precedents: `TokenRecord.PortraitAssetId` / `SetTokenPortrait` (`ODY-S07-106`) for the schema and setter shape, `ODY-S08-102` (camera/wheel), `ODY-S08-104` (token pointer gesture).

## 4. In scope
- `TokenRecord`: `ZOrder` (`long`, default 0 in the constructor) and `Scale` (`double`, default 1.0, validated to `[0.5, 3.0]`, exposed as `TokenRecord.MinScale`/`MaxScale`).
- `ISceneRepository`/`SqliteSceneRepository`: `SetTokenZOrder` and `SetTokenScale`, cloned from `SetTokenPortrait` (separate methods; `expectedRevision`; `commandId` replay; `correlationId`; `Result<TokenRecord>`; `TokenRevisionConflict`/`TokenNotFound`). Events `odyssey.persistence.token_z_order_set` / `token_scale_set`.
- Schema: two plain columns in `CREATE TABLE Token` (`ZOrder INTEGER NOT NULL DEFAULT 0`, `Scale REAL NOT NULL DEFAULT 1.0`); every token `SELECT` and `ReadTokenRecord` carry them; `MoveToken`/`SetTokenPortrait` results carry them through. `CreateToken` assigns `MAX(ZOrder)+1` of the token's scene inside the create transaction.
- `BoardScreenPresenter`: `RenderTokens` sorts by `ZOrder` ascending and sizes by `Scale`; `BeginTokenDrag` (the real pointer-down path) calls `RaiseTokenToTop`, which makes no call when the token is already strictly above every other one; `OnBoardWheel` delegates to `HandleBoardWheel`, where Shift over a token calls `ScaleTokenAt` and anything else zooms the camera exactly as before; the hit test uses the scaled size and prefers the topmost token.
- Backlog block 5 decomposed; this contract and its ExecPlan.

## 5. Out of scope
Multi-select and group operations (reserved `ODY-S08-106`); any effect of scale on `BoardMovementService`/occupancy; a manual send-to-back UI; `BoardCamera.cs`, `BoardPointerGesture.cs`, `AssetPoolPresenter.cs`, `NativeFileDialog.cs`, `AssetTextureCache.cs`, `BoardMovementService.cs`, `.asmdef`/`.csproj`, verify scripts, every ADR.

## 6. Domain / 7. Application / 8. Persistence
Application: `TokenRecord` and `ISceneRepository` (`SceneRepositoryContracts.cs`). Persistence: `SqliteSceneRepository.cs`. No domain change.

## 9. Tests and validation
Backend (`SqliteSceneRepositoryTests.cs`): `TC-BOARD-078` (new token gets max+1 per scene, default scale 1, survives re-read), `TC-BOARD-079` (both setters revision-gated, idempotent per command id, kept across `SetTokenPortrait`/`MoveToken`), `TC-BOARD-080` (scale bounds/non-finite rejected, bounds accepted, nothing written on rejection). Client (`BoardScreenPresenterTests.cs`): `TC-BOARD-081` (click on a non-top token raises it; tree order follows), `TC-BOARD-082` (click on the top token: zero `SetTokenZOrder` calls, revision unchanged), `TC-BOARD-083` (Shift+wheel over a token scales it, camera untouched), `TC-BOARD-084` (plain wheel zooms the camera, token untouched; Shift over empty board also zooms), `TC-BOARD-085` (clamped at both bounds, no call at a bound, never zero size). Regression: `TC-BOARD-072`-`077` (ODY-S08-104) unchanged and passing.

## 10-17. (see plan)
Compatibility, security, observability, performance, dependencies and completion evidence follow the same shape as `ODY-S08-102`-`104`.

## 18. Change control

### Decisions made during execution
- **Bounds `[0.5, 3.0]`.** Below half the base size a 28px token is under 14px — too small to hit or read; three times covers a Large/Huge creature without one token swallowing the board. Multiplicative 1.1 step per wheel notch (same as camera zoom) reaches either bound in ~7 / ~18 notches.
- **No migration, following the `PortraitAssetId` precedent.** The schema is created with `CREATE TABLE IF NOT EXISTS` and is unversioned; `PortraitAssetId`, `CharacterId` and every prior token column were added the same way, and there are no deployed campaign databases whose data must be preserved (the product has not shipped). Consequence, stated plainly: a campaign database *created before this change* has a `Token` table without the new columns, and `IF NOT EXISTS` will not add them — token reads on such a file would fail with a `SqliteException` (surfaced as `SceneIoFailed`). Such a file must be recreated. If shipped data ever exists, a real migration is needed first.
- **`SetTokenScale` rejects out-of-range/non-finite values with `ArgumentOutOfRangeException`,** like `expectedRevision < 1` does — an out-of-range scale is a caller bug (the UI clamps first), not a domain failure. `TokenRecord`'s constructor enforces the same range, so a corrupted row surfaces loudly instead of drawing a degenerate token.
- **The raise lives in `BeginTokenDrag`,** the method the real `OnTokenPointerDown` calls, so the tests exercise the same path as the real pointer-down, not a parallel one. It reorders the element in place (`BringToFront`) instead of `Refresh()`, because rebuilding the DOM under an active pointer capture would drop the capture and abort the gesture. A failed raise only sets the status text; the gesture continues.
- **"Already top" means strictly above every other token.** With tied values (only possible for legacy rows, all 0) the first click raises.
- **Hit test now uses the scaled size and picks the topmost overlapping token** (also affects `ApplyDroppedAsset`'s drop target, consistently with what the user sees). Non-overlapping behaviour is identical to before.
- **Shift over empty board zooms the camera** (unchanged behaviour), rather than being swallowed.
- **`HandleBoardWheel` added** so the wheel branching is testable with plain numbers, per the project's testability convention; `OnBoardWheel` is now a thin wrapper.

### Findings (reported, not fixed silently)
- Real UI Toolkit event wiring (`OnBoardWheel`'s use of `evt.shiftKey`, `OnTokenPointerDown`) is not simulated in tests — the same accepted precedent as `ODY-S08-102`-`104`.
- Scale is purely visual: a token scaled up still occupies one cell for `BoardMovementService` (explicit non-goal).

### Blockers
None.
