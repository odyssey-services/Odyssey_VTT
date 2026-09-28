# ODY-S08-107 — Board Controls Rework: Box Selection, Shift-Click, Middle-Button Pan, Right-Button Marker

## 1. Task identity
`ODY-S08-107`; status: In Review (Draft PR, merge deferred to the product owner). A post-slice revision, recorded as row 7 of `docs/tasks/SLICE-08_IMPLEMENTATION_BACKLOG.md` (`SLICE-08` itself was already fully delivered by `ODY-S08-106`). **Placement decision:** it is *not* given a new track: it edits the same presenter and revises `SLICE-08` decisions, so a backlog row with an explicit "post-slice, not a sixth block" note keeps the history in one place; the ТЗ allows only that file for the backlog entry.

## 2. Goal
Bring the mouse layout closer to Owlbear after the product owner tried the finished board: left drag on empty board = selection box, Shift+click for multi-select, middle button = camera pan, right button = local player marker.

## 3. Authority
This task's governing ТЗ. It **revises** three merged decisions: left-drag pan (`ODY-S08-102`), Ctrl+click multi-select (`ODY-S08-106`), and adds the first mouse-button distinction in the presenter.

## 4. In scope
- `BoardScreenPresenter.cs`:
  - Token handlers act on the left button only (`evt.button == 0`); a middle/right press is neither captured nor stopped, so it bubbles to the board even over a token. The token's pointer-up ignores other buttons' releases.
  - `OnBoardPointerDown` → `HandleBoardButtonDown(button, x, y, shift)` (testable): left = click/selection box, middle = pan, right = marker. One gesture at a time via `_activeBoardButton`; `HandleBoardButtonUp` ends a gesture only for the button that owns it.
  - Selection box: `BeginBoardPointerGesture(x, y, shift)` / `MoveBoardPointer` / `EndBoardPointerGesture` (same public names, new meaning). A drag selects tokens whose world position (`BoardCamera.FromPixels…` of the box corners vs. the token's world position) lies inside; Shift adds, otherwise replaces. A click (below the 5px threshold) is unchanged from `ODY-S08-104`/`106`.
  - Pan: `BeginBoardPan`/`MoveBoardPan`/`EndBoardPan` on the existing `BoardPointerGesture` (unchanged type).
  - Marker: `PlacePlayerMarker` (world position stored, ring `VisualElement`, `PickingMode.Ignore`, follows pan/zoom via `RepositionTokens`, expires after 2.5 s, replaced by a second press, `ClearPlayerMarker`).
  - Multi-select modifier `evt.ctrlKey` → `evt.shiftKey`; the `BeginTokenDrag` optional parameter is now `shift`. Ctrl is read nowhere.
- New pure type `BoardBoxSelectGesture` (`Assets/Odyssey/Client/Runtime/BoardBoxSelectGesture.cs`, plus its Unity `.meta`).
- Tests `TC-BOARD-094`-`102`; test/catalog wording for `063`/`086`/`087` updated.
- Backlog row 7; this contract and its plan.

## 5. Out of scope
Any network transmission or persistence of the marker (visibility to other participants is a separate future task needing a real client-to-client network); `BoardCamera.cs`, `BoardPointerGesture.cs`, `AssetPoolPresenter.cs`, `NativeFileDialog.cs`, `AssetTextureCache.cs`, `BoardMovementService.cs`, backend/persistence, `Packages/com.odyssey.networking`, `.asmdef`/`.csproj`, verify scripts, every ADR.

## 6. Domain / 7. Application / 8. Persistence
Unchanged.

## 9. Tests and validation
`TC-BOARD-094` (box replaces the selection; box drawn during the drag and removed after), `095` (Shift box adds), `096` (outside tokens excluded; world-coordinate box after pan+zoom), `097` (Shift+click add/remove; Ctrl no longer special), `098` (Shift+wheel scales and leaves selection; Shift+click never scales), `099` (middle pan on empty board), `100` (middle pan over a token neither selects nor moves it), `101` (marker world position, follows pan, replace, clear), `102` (one gesture at a time; foreign release ignored, both orders). Regression: `TC-BOARD-072`-`093` pass unchanged in meaning; `063` (pan) now drives the middle-button API and `086`/`087` the Shift modifier — the intended, disclosed meaning changes.

## 10-17. (see plan)
Compatibility, security, observability, performance, dependencies and completion evidence follow the same shape as `ODY-S08-102`-`106`.

## 18. Change control

### Decisions made during execution
- **Pointer-id risk (ТЗ §1.1).** UI Toolkit reports the mouse under a single pointer id whatever button is pressed, so pointer capture cannot tell buttons apart. Handled explicitly and independently of that fact: `_activeBoardButton` records which button owns the gesture; another button's press is refused while it runs (also while a token drag is in progress, and a left press on a token is ignored while a board gesture runs); a release of a different button is ignored. The state machine is exercised through public number-driven methods (`TC-BOARD-102`). What was *not* verified is the real event stream in a running Player/Editor window (button numbers on real `PointerDownEvent`s); it follows the documented `PointerEventBase.button` contract (0 left, 1 middle, 2 right) and, as in `ODY-S08-102`-`106`, event wiring is not simulated in tests.
- **Pan via `BoardPointerGesture` keeps its 5px threshold,** as the ТЗ prescribes reusing it: the first 5px of a middle drag produce no pan (the gesture reports deltas only after the threshold). A pan has no "click" meaning, so a middle press/release without movement does nothing.
- **Marker: a ring, 22px, 2.5 s.** A ring keeps the token/map under it visible; 2.5 s is long enough to notice and point, short enough not to clutter; a second press replaces it at once. No pulse animation (kept simple). Expiry uses the element's scheduler and is not exercised by an EditMode test (no running panel loop); the explicit `ClearPlayerMarker` path is.
- **Box membership is by token centre position** (`TokenPosition`), not by the token's drawn square, so a large scaled token is selected only when its centre is inside.
- **Box and marker are children of the board area;** `RenderTokens` clears the board, so it re-adds a live box/marker afterwards.
- **Left-drag pan is removed on purpose** (product decision); the old `BoardPointerGesture`-based left drag test (`TC-BOARD-063`) now targets the middle-button API.

### Findings (reported, not fixed silently)
- Recording a control-scheme change across several merged decisions did not require an ADR here (presenter-local, no architectural rule changed); no ADR was edited.
- The marker is deliberately local: showing it to other participants needs a real client-to-client transport that does not exist (`ADR-015`/`016`/`017` are design only; Unity Relay is not allowed in production before a pilot per `ADR-016`).
- No code path of this task references networking, persistence or a repository for the marker (grep-verifiable: it uses only `VisualElement`, `BoardCamera` and an in-memory `TokenPosition?`).

### Blockers
None.
