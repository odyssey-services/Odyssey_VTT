# ODY-S08-102 — Board Camera: Pan and Zoom (SLICE-08 block 2)

## 1. Task identity
`ODY-S08-102`; status: In Review (Draft PR, merge deferred to the product owner). Block 2 of `SLICE-08` (`docs/tasks/SLICE-08_IMPLEMENTATION_BACKLOG.md`); follows `ODY-S08-101` (block 1, merged).

## 2. Goal
Replace `BoardScreenPresenter`'s fixed `OriginOffsetPixels`/`PixelsPerUnit` transform with a real camera: pan by dragging the empty board area, zoom-to-cursor with the mouse wheel, without breaking the already-working click-to-select/click-to-move flow.

## 3. Authority
This task's governing ТЗ §0-§7; `docs/tasks/SLICE-08_IMPLEMENTATION_BACKLOG.md` block 2 (section 3/8); `ODY-S08-101`'s texture rendering (unchanged, not reopened).

## 4. In scope
- Two new pure C# types in `Assets/Odyssey/Client/Runtime/`, no `UnityEngine` reference: `BoardCamera` (world/pixel transform, pan, zoom-to-cursor, scale clamping) and `BoardPointerGesture` (click-vs-drag disambiguation from raw pixel positions).
- `BoardScreenPresenter.cs`: wires pointer-down/move/up and mouse wheel on the empty board area to these two types; tokens reposition from the camera instead of the fixed constants; a token's own `PointerDownEvent` stops propagation so a token click can never start a pan.
- Tests: `BoardCameraTests.cs`, `BoardPointerGestureTests.cs` (new files), additions to the existing `BoardScreenPresenterTests.cs`.
- `Tests/Metadata/test-catalog.json`; backlog row/status; this contract and plan.

## 5. Out of scope
Backend/persistence (`SqliteSceneRepository.cs`, `SceneRepositoryContracts.cs`, `ReadAssetContent`/`SetSceneBackground`/`SetTokenPortrait`); `BoardMovementService`'s own logic (only *when* it is called changes, never its body); drag-and-drop of tokens/files (block 3); the file picker (block 4); token z-order/scale/persistent selection (block 5); persisting or syncing camera state (task contract section 1.4); multi-touch/pinch; board-bounds clamping (the board is an infinite canvas by design).

## 6. Domain contract
Unchanged. No domain type is added or modified.

## 7. Application contract
Unchanged. No `Odyssey.Application` type is added or modified -- this is a client-only, UI-layer change.

## 8. Persistence boundary
Unchanged. `RegisterAsset`/`ReadAssetContent`/`SetSceneBackground`/`SetTokenPortrait` and every other persistence method are untouched; camera state is never read from or written to any repository.

## 9. Tests and validation
`TC-BOARD-051`-`061`: `BoardCamera`/`BoardPointerGesture` pure math and logic, called directly with numbers (no simulated events) -- default transform matches the old fixed one, pan/zoom round-trip, zoom keeps the exact anchor pixel fixed (direct numeric check, not approximate), scale clamps at both bounds without degenerating, non-positive zoom factor throws, movement below/at/above the drag threshold, a drag that returns near the down-point stays a drag, `Cancel`/no-active-gesture are no-ops. `TC-BOARD-062`-`064`: presenter-level integration -- movement below the threshold still moves the selected token to the exact expected destination and clears selection (byte-for-byte the old behavior, now reached through the gesture path); movement above the threshold pans the camera by exactly the dragged delta and leaves the token's persisted position and the selection untouched; `ZoomBoard` changes `Camera.Scale` and repositions an already-rendered token element without touching persisted state. The existing `BoardScreenPresenterTests.cs` (click-select/click-move, non-controller denial, MainGM override, texture rendering from `ODY-S08-101`) pass unchanged -- they already exercise `SelectToken`/`TryMoveSelectedTokenTo` directly, not through simulated events, so the pointer-wiring change does not touch what they test.

## 10. Compatibility and rollback
A freshly opened board looks exactly as before (the default `BoardCamera` reproduces the old fixed transform's pixel positions exactly). `TryMoveSelectedTokenTo`'s own contract and `BoardMovementService.MoveToken` are unchanged. `OnBoardAreaClicked`/`ToPixels`/`FromPixels` are removed as private implementation details (no public signature depended on them). Rollback: revert the PR.

## 11. Security and privacy
No change. No new inputs cross a trust boundary; camera state never leaves the client process.

## 12. Observability
No change: no new event, no new persisted field.

## 13. Performance
Panning/zooming reposition only already-rendered token elements (no DB read, no texture-cache lookup, no DOM teardown) -- cheaper than the full `Refresh()` a click-to-move still triggers.

## 14. Dependencies
`ODY-S08-101`.

## 15. Dependencies (packages)
None.

## 16. Implementation plan
`docs/plans/active/ODY-S08-102_Board_Camera_Pan_And_Zoom.md`.

## 17. Completion evidence
`dotnet test` green (unaffected -- this task touches no `DotNet/Projects` code); Unity 6000.4.0f1 batchmode EditMode run green (result and count reported below); `git diff --name-status` limited to the paths in §4.

## 18. Change control

### Decisions made during execution
- **Drag threshold: 5 screen pixels** (`BoardPointerGesture.DragThresholdPixels`), the same order of magnitude common UI toolkits use to distinguish a click from a drag (e.g. Windows' own default). Measured in screen pixels, not world units, so it stays a small, consistent gesture regardless of the current zoom level.
- **Wheel direction: scrolling away from the user (reported as `WheelEvent.delta.y < 0`) zooms in.** This matches the convention most infinite-canvas tools use (Google Maps, most browsers' page zoom). It is a single `<` flipped to `>` in `OnBoardWheel` if the product owner wants the opposite.
- **Zoom step: ×1.1 per wheel notch** (`BoardZoomStepFactor`), a smooth, gradual increment; not otherwise significant.
- **Scale bounds: `MinScale = 5`, `MaxScale = 400` pixels/world-unit** (default 40). At the floor, one screen pixel is 0.2 world units -- far from "hundreds of world units per pixel" -- while still allowing a meaningful zoom-out; the ceiling is ten times the default, enough to zoom in meaningfully without float-pixel precision (UI Toolkit styles are `float`) becoming a concern. Neither bound is prescribed by the ТЗ; both are this task's own justified choice, easy to retune (two `const`s).
- **Token click protection is structural, not covered by a runtime-simulated-event test.** Every token element registers its own `PointerDownEvent` handler that unconditionally calls `evt.StopPropagation()`, before the board-level handler (`OnBoardPointerDown`) could ever see the event -- so a pointer-down that starts on a token can never reach `BoardPointerGesture` at all, regardless of any subsequent movement. This is verifiable by reading the four-line wiring in `RenderTokens`, and the existing `BoardScreenPresenterTests.cs` doc comment already establishes this codebase's own precedent of testing selection/move logic through direct method calls rather than simulated UI Toolkit pointer event dispatch (`SendEvent` et al. are not used anywhere in this test suite). Adding one would be new testing infrastructure, not a small extension; recorded here as a disclosed choice rather than done silently.
- **`WheelEvent` has no `localPosition`; it exposes `localMousePosition`/`mousePosition` (it derives from `MouseEventBase<WheelEvent>`, not the newer `PointerEventBase<T>` pointer events).** `OnBoardWheel` uses `evt.localMousePosition`, the target-relative equivalent `PointerDownEvent`/`PointerMoveEvent`/`PointerUpEvent` express as `localPosition`. Found only by an actual Unity compile (`error CS1061`), not by reading API docs offline; recorded because it is the one place this task's assumption about a uniform "local position" property across all these event types was wrong.

### Findings (reported, not fixed -- out of scope)
- The scene background image does not pan or zoom with the camera -- it stays a static `background-size: cover` fill of the fixed 440×440 `board-area`, exactly as `ODY-S08-101` left it. The task contract's own invariant (section 2) forbids touching background/portrait texture rendering; only token positioning uses the camera. A future block (not named in the current five) would need to give the background its own world-space placement if it should move with the camera.
- No `ADR` question was found. The camera is a client-only, UI Toolkit-internal concern; it introduces no new module dependency and touches no `ADR-001`-`ADR-031` decision.

### Blockers
None.
