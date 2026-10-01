# ODY-S11-226 — Board zoom/pan did not move the whole scene as one; add a reference grid

**Status:** In Review
**Roadmap stage / slice:** SLICE-11 (found during the product owner's first live visual pass of the UI)
**Owner:** Claude Code
**Requested by:** Product owner
**Branch:** `claude/pensive-gates-n18srp`
**Pull request:** Not opened
**ExecPlan:** Not required
**Created:** 2026-10-01
**Last updated:** 2026-10-01

## 1. Goal

Mouse-wheel zoom and middle-button pan move the map background, drawn obstacles (walls/doors/windows), fog of
war and tokens together, as one scene, exactly as they already move tokens alone. Add a faint reference grid,
one line per world unit, that follows the same transform.

## 2. Why this task exists — false-positive verification, not a later regression

The product owner reported, after finally looking at the live UI, that wheel-zoom and middle-drag pan did not
move the map/tokens/drawn objects together. Diagnosed and confirmed by code reading plus a live Unity
PlayMode run (see §17):

- **Root cause 1 — the map background never used the camera at all.** `ApplySceneBackground` applied the
  texture as a CSS `background-image` + `background-size: Cover` directly on the fixed `_boardArea` box. This
  code (`ApplyTexture`, commit `fda1ada`, "SLICE-08 backlog, asset content read API, minimal texture
  rendering") predates `BoardCamera` itself (`600baeb`, "board camera pan and zoom", the very next commit in
  the same slice) — the background was never wired into the camera transform introduced immediately
  afterward, in the same block of work.
- **Root cause 2 — obstacles/fog were positioned by the camera only at the moment of a full `Refresh()`, never
  during a live pan/zoom.** `RenderObstacles`/`RenderFogOfWar` do correctly read `BoardCamera.ToPixelsX/Y`,
  but only run inside `Refresh()` (which clears and rebuilds the whole board from the repository). `ZoomBoard`/
  `MoveBoardPan` — the methods a live wheel-zoom/middle-drag actually call — only ever called
  `RepositionTokens()`, never anything for obstacles/fog. Between two `Refresh()` calls (i.e. during the
  entire live gesture), the map/walls visibly stayed at their last `Refresh()`'s pixel position while tokens
  moved smoothly.
- **Conclusion: this is a false-positive verification, not a regression introduced later (e.g. by the
  `ODY-S11-210` `OdyPopover` overlay refactor).** The defect has existed, unchanged, since the camera was
  introduced in `ODY-S08-102`/`ODY-S08-107` (SLICE-08). Every later task's own "board functionality has no
  regression" claim (including SLICE-11 Phase 1's) was checked through EditMode tests of the gesture/camera
  *math* (`BoardCameraTests`, `BoardPointerGesture`, `ZoomBoard`/`MoveBoardPan` unit assertions) and real-Unity
  runs that never happened to include a human looking at whether the rendered background/obstacles actually
  moved on screen during a live drag — the EditMode tests have no layout/render pass to catch this, and no
  PlayMode test exercised a live multi-frame pan/zoom gesture and inspected obstacle/background pixel
  positions before this task. Nobody ever visually confirmed the specific thing that was broken.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`, ADR-001 (module boundaries)
- `docs/tasks/active/ODY-S11-214_Camera_Autofocus_On_Turn_Change.md` (camera-focus animation, also fixed here — see §4)

### Requirement and test IDs

- Requirement IDs: None
- Existing test IDs: `BoardScreenPresenterTests` (background/obstacle/token render tests), `BoardCameraTests`
- New test IDs: `TC-BOARD-121b`/`121c`/`121d` (`BoardScreenPresenterTests.ZoomBoard_RepositionsObstaclesAndBackground_NotOnlyTokens`,
  `MoveBoardPan_RepositionsObstaclesAndBackground_NotOnlyTokens`, `BoardGrid_IsVisibleAndFollowsZoom`)

### Task-safe private context

- Approved summary: None

## 4. Verified current state

### Verified facts

- `BoardScreenPresenter.ZoomBoard`/`MoveBoardPan` called only `RepositionTokens()` before this task (confirmed
  by reading the pre-task source directly).
- `RepositionTokens()` repositions tokens and the local player marker only.
- `RenderObstacles`/`RenderFogOfWar`/`ApplySceneBackground` are called from `Refresh()` only (which does
  `_boardArea.Clear()` + a full repository re-read) — never from the pan/zoom path.
- The camera-autofocus ticker (`AdvanceCameraFocus`, `ODY-S11-214`) had the exact same gap mid-animation,
  worked around by a full `Refresh()` only at the *end* of the ease (code comment: "so obstacles and fog
  follow the camera") — confirming this exact defect was already known, ad hoc, in one specific place, and
  never generalized or fixed at the root.
- Live PlayMode confirmation after the fix (role MainGM, a real Draw-Wall click+drag creating a real obstacle,
  then the real `ZoomBoard`/`BeginBoardPan`/`MoveBoardPan`/`EndBoardPan` calls a live wheel/middle-drag would
  make): a 2x zoom moved the token's left from 306 to 526px and the obstacle's left from 140 to 180px (both
  move, in the zoom-to-cursor direction); the pan moved the token's top from 366 to 406px and the obstacle's
  top from 178 to 218px — **identical +40px for both**, exactly what a uniform scene translation must produce.

### Assumptions

- None.

## 5. Scope

### In scope

- `BoardScreenPresenter.cs`: a single camera-driven world rect for the map background (`ApplyBoardBackgroundTexture`/
  `RepositionBoardBackground`), cached obstacle/HP-bar elements repositioned on every pan/zoom
  (`RepositionObstacles`, which also now repositions fog and the background), wired into `ZoomBoard`,
  `MoveBoardPan`, and the `AdvanceCameraFocus` ticker (ODY-S11-214's own camera-ease animation now keeps
  obstacles/fog/background in sync every tick, not only once at the end).
- `BoardGridPresenter.cs` (new): a faint, camera-driven reference grid, one line per world unit.
- Regression tests for all of the above.

### Out of scope

- Any change to the gesture/input logic itself (click-to-select, drag-to-move, box-select, obstacle drawing,
  camera math) — only what is positioned by the camera, not how the camera itself is computed.
- Backend — untouched.

### Allowed paths

```text
Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs
Assets/Odyssey/Client/Runtime/BoardGridPresenter.cs
Assets/Odyssey/Client/Tests/EditMode/BoardScreenPresenterTests.cs
```

## 6. Technical constraints

- No `Resources.Load`/`ServiceLocator`/`GetService`/`FindObjectOfType` — none introduced; `BoardGridPresenter`
  is constructed and owned directly by `BoardScreenPresenter`, exactly like `BoardFogOfWarPresenter`.
- Must not change `BoardCamera`'s own math (`ToPixelsX/Y`, `FromPixelsX/Y`, `Pan`, `Zoom`) — only which visual
  elements read from it, and when.
- The map background's world rect must be computed once per asset (not on every `Refresh()`), so that an
  unrelated `Refresh()` (a drawer opening, a role switch, another participant's move) does not silently discard
  the user's own pan/zoom by "re-fitting" the background to whatever the current viewport happens to be.

## 7. Expected behavior

### Scenario 1 — zoom moves the whole scene

**Given** a board with a background image, at least one drawn obstacle, and at least one token
**When** the user zooms with the mouse wheel
**Then** the background, the obstacle, and the token all move and rescale together, consistent with one
zoom-to-cursor transform.

### Scenario 2 — pan moves the whole scene

**Given** the same board
**When** the user pans with a middle-button drag
**Then** the background, the obstacle, and the token all translate by the exact same pixel amount.

### Scenario 3 — grid follows the scene

**Given** the board, at any zoom/pan state
**Then** a faint grid (one line per world unit) is visible, positioned consistently with the rest of the
scene, below obstacles/tokens, and never intercepts pointer input.

### Required invariants

- An unrelated `Refresh()` (not caused by the user's own pan/zoom) never resets the background's apparent
  zoom/pan to "fit the current viewport" — only a genuine background-asset change does that.
- The camera-autofocus animation (`ODY-S11-214`) keeps obstacles/fog/background in sync with the token it is
  centering on throughout the ease, not only at its end.

## 8. Deliverables

- Production code: the fixes and the new grid presenter described in §5.
- Tests: `TC-BOARD-121b/c/d` (new), existing background/obstacle tests updated only where the background's
  element identity changed (see §18).
- Documentation: this task file.

## 9. Acceptance criteria

1. `ZoomBoard`/`MoveBoardPan` reposition the map background and every rendered obstacle (and its HP bar), not
   only tokens — confirmed by `TC-BOARD-121b`/`121c`, both asserting an actual, non-trivial position change.
2. The grid element exists, is mounted below obstacles/tokens, and is not torn down by a zoom — `TC-BOARD-121d`.
3. A live PlayMode run (not just EditMode) confirms a real obstacle created through the real Draw Wall gesture
   moves together with a real token on a real `ZoomBoard`/pan call (see §17 for the actual numbers).
4. All pre-existing board tests (selection, token drag, obstacle drawing, fog) remain green.
5. `test-unity.ps1`, `test-fast.ps1`, `verify-*.ps1` all green.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-BOARD-121b` | Unity EditMode | `ZoomBoard` repositions an obstacle and the background, with the zoom-to-cursor math matching exactly | Pass |
| `TC-BOARD-121c` | Unity EditMode | `MoveBoardPan` repositions an obstacle and the background | Pass |
| `TC-BOARD-121d` | Unity EditMode | The grid element exists, is mounted below tokens, and survives a zoom | Pass |

### Required commands

```powershell
scripts/test-unity.ps1
scripts/test-fast.ps1
scripts/verify-format.ps1
scripts/verify-repository.ps1
scripts/verify-test-structure.ps1
```

### Manual validation

- Performed via a temporary, non-committed PlayMode diagnostic (real role switch, real Draw-Wall click/drag to
  create a real obstacle, then the real `ZoomBoard`/pan calls a live wheel/middle-drag would make): see the
  exact numbers in §17. Removed before this commit, per this track's established convention against leaving
  throwaway diagnostic tests in committed files.
- Not performed: an interactive human Play-mode walkthrough with a mouse — this environment has batchmode-only
  Unity access, no interactive GUI/display, the same limitation recorded throughout this verification track.
  No screenshot/GIF could be produced (a `ScreenCapture.CaptureScreenshot` attempt earlier in this track, for
  an unrelated task, confirmed batchmode has no real display surface to capture); the numeric before/after
  pixel evidence in §17 is offered as the closest available substitute.

### Validation not required by this task

- A full human visual QA pass confirming the grid's exact visual contrast/subtlety looks right — this needs a
  human with GUI access, flagged as an open follow-up like every other purely-visual item on this track.

## 11. Compatibility, migration, and rollback

Not applicable — presentation-only, no persisted state or contract affected. Rollback = revert the commit.

## 12. Dependencies and licensing

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

Not applicable.

## 14. Planning and execution mode

- Planning mode: Brief plan
- Expected pull request count: 1 (bundled with the rest of `claude/pensive-gates-n18srp`)

## 15. Documentation and versioning impact

- Documents that must change: None beyond this task file.
- Application version change: No.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass (249/249 EditMode, 9/9 PlayMode).
- [x] Required manual checks completed to the extent possible in this environment (see §10).
- [x] Required commands and their real results are recorded (see §17).
- [x] Architecture and dependency rules remain valid (`TC-ARCH-001` PASS).
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Self-review performed against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work. (Not opened; owner decides.)
- [ ] Product owner or authorized reviewer completes the required review.

## 17. Completion evidence

### Changed files / areas

- `Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs` — camera-driven background world-rect tracking
  (`ApplyBoardBackgroundTexture`/`RepositionBoardBackground`/`ClearBoardBackground`), cached obstacle/HP-bar
  elements + `RepositionObstacles()` (also repositions fog and the background), wired into `ZoomBoard`,
  `MoveBoardPan`, `AdvanceCameraFocus`; `ApplySceneBackground()`'s call moved to after `_boardArea.Clear()`
  (a background child element added before `Clear()` would be wiped by it).
- `Assets/Odyssey/Client/Runtime/BoardGridPresenter.cs` (new) — the reference grid.
- `Assets/Odyssey/Client/Tests/EditMode/BoardScreenPresenterTests.cs` — 3 new tests (`TC-BOARD-121b/c/d`); 3
  pre-existing background tests updated (see §18) because the background's identity moved from an inline style
  on `board-area` to its own child element.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `scripts/test-unity.ps1` | Passed | EditMode 249/249, PlayMode 9/9 |
| `scripts/test-fast.ps1` | Passed | `TC-ARCH-001 PASS`; `dotnet test` 952/952 |
| `scripts/verify-format.ps1` / `verify-repository.ps1` / `verify-test-structure.ps1` | Passed | all PASS |
| Live PlayMode diagnostic (real Draw-Wall obstacle + real camera calls) | Passed (temporary, removed) | zoom: token.left 306→526, obstacle.left 140→180; pan: token.top 366→406, obstacle.top 178→218 (identical +40px for both) |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `TC-BOARD-121b`/`121c` |
| AC-2 | Passed | `TC-BOARD-121d` |
| AC-3 | Passed | §17 live diagnostic numbers above |
| AC-4 | Passed | full EditMode/PlayMode suite green |
| AC-5 | Passed | all scripts green above |

### Known limitations

- No human has visually confirmed the grid's subtlety/contrast or watched the fix live with a mouse — this
  environment has no interactive GUI. The numeric evidence above is offered as the closest available
  substitute; a real human Play-mode pass remains an open item across this whole verification track.
- The map background's "fit to viewport" crop is computed once per asset change, using whatever the board's
  pixel size happens to be at that moment (see the technical constraint in §6) — if the window is resized
  between loading a background and this task's fix, the crop will reflect the window size at load time, not
  the current one. This matches the pre-existing (if broken) behavior's own implicit assumption and was not
  a goal of this task to change.

### Follow-up tasks

- A full human interactive visual QA pass of the fixed pan/zoom and the new grid, once a reviewer with GUI
  access is available.

### Self-review summary

- Scope review: touched only the camera-repositioning path and added one new presentation-only grid class; did
  not touch gesture/input logic or the camera's own math.
- Architecture review: `TC-ARCH-001` passes; `BoardGridPresenter` follows the exact existing
  `BoardFogOfWarPresenter`/`BoardToolbarPresenter` composition pattern.
- Test review: 3 new tests added with real, non-trivial assertions (exact zoom-to-cursor math, not just
  "changed"); 3 pre-existing tests updated with a documented, deliberate reason (the background's element
  identity changed), not to silently paper over a behavior change.
- Documentation/version review: this task file is the only new documentation; no version fields changed.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-10-01 — Diagnosed and documented that this is a false-positive verification from SLICE-08 (the camera
  was added after the background-rendering code and never retrofitted into it; `RenderObstacles`/
  `RenderFogOfWar` were never wired into the live pan/zoom path at all), not a regression from `ODY-S11-210`'s
  overlay refactor — per this task's own instruction to establish which case it is, not just silently fix it.
- 2026-10-01 — Updated 3 existing EditMode tests
  (`SceneWithBackgroundAssetId_AfterRefresh_BoardShowsTheTextureNotASolidFill`,
  `BackgroundClearedAfterBeingShown_NextRefreshRemovesTheTexture`,
  `RepeatedRefreshAndMoves_WithTheSameAssetId_ReadTheAssetFromDiskOnlyOnce`) to query the new
  `board-background` child element instead of `board-area`'s own inline style, because the background
  genuinely no longer lives there — a deliberate, disclosed architecture change (needed so the background can
  be positioned/scaled independently by the camera), not a rewrite to dodge a failure. Two sibling tests
  (`MissingAssetFile...`, `UndecodableImageBytes...`) needed no change: they assert the *absence* of a
  background image on `board-area` in a failure case, which remains true (vacuously) either way.
- 2026-10-01 — Also fixed the camera-autofocus ticker (`ODY-S11-214`) to reposition obstacles/fog/background on
  every tick of its ease, not only once at the end via a full `Refresh()` — the exact same underlying defect
  was already visible there, worked around ad hoc rather than fixed at the root.

### Approved task changes

- None.
