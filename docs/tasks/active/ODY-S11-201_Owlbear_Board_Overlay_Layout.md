# ODY-S11-201 — Owlbear-style board screen: full-screen map with overlay panels (phase 1)

**Status:** In Review  
**Roadmap stage / slice:** SLICE-11 (full client UI, phase 1 of 0–5)  
**Owner:** Claude Code  
**Requested by:** Product owner  
**Branch:** `claude/pensive-gates-n18srp`  
**Pull request:** Not opened  
**ExecPlan:** `ODY-S11-200` §14  
**Created:** 2026-09-30  
**Last updated:** 2026-09-30

## 1. Goal

The trial game screen shows the board full-screen, with every other panel floating over it: a thin top bar, slide-in
side drawers, and a collapsible corner dock for the roll panel + game log.

## 2. Why this task exists

The old `TrialScreenPresenter` laid out a fixed 440×440 board and a 380px controls column side by side. The product
decision is the Owlbear layout for the board (everything else follows the D&D Beyond design system, `ODY-S11-200`).

## 3. Authorities and requirement references

- `AGENTS.md` §5, §9; ADR-001, ADR-005 (explicit composition, one owner per resource, subscriptions released).
- `ODY-S08-107` (board controls — must not regress), `ODY-S10-111/113/114` (obstacles, fog, facing UI).
- New test IDs: `TC-GAMESHELL-001`…`TC-GAMESHELL-004`.

## 4. Verified current state

- `TrialScreenPresenter` composed board, asset pool, roll panel, game log into `trial-board-column` /
  `trial-controls-column`; `BoardScreenPresenter` set an inline 440×440 size and sized fog from two constants.
- PlayMode smoke tests look up `trial-screen`, `trial-controls-column`, and drag a wall at board offset (40,40).

## 5. Scope

### In scope
- New `Runtime/Game/GameShellPresenter.cs` (layout), `Runtime/Game/GameSessionContext.cs` (panel parameter object).
- `TrialScreenPresenter.cs` recomposed on the shell (same public properties; new `Shell`, `Context`).
- `BoardScreenPresenter.cs`: opt-in `FullBleed` embedding flag only (see §6).
- `OdysseyDesignSystem.uss` sections 12–14; `OdysseyUiKit.cs` (`GameScreen` class constant).
- Tests: new `GameShellPresenterTests.cs`; PlayMode drag offset adapted (see §10).

### Out of scope
- Board logic (camera, gestures, hit tests, token/obstacle/fog rendering, commands) — unchanged.
- Roll panel / game log / asset pool business code — unchanged; only where their views are mounted.
- `DeveloperShellPresenter` — unchanged (still the dark start screen).
- Backend — untouched.

## 6. Technical constraints and decisions

- **Full-bleed embedding.** `BoardScreenPresenter.FullBleed` (default `false`) only changes how the board area is
  sized: absolute, filling its parent, instead of the inline 440×440; fog uses the area's laid-out size (falls back to
  440 when no layout exists, e.g. EditMode). Everything else runs the same code. Default keeps all existing board
  tests byte-for-byte.
- **Board chrome.** The board's own title/toolbar/status stay the board's elements; USS section 13 hides the title
  and floats the toolbar (bottom-left, MainGM-only as before) and the status pill over the map.
- **Initial centering.** Once the board layer has a size, the shell pans the camera once through the board's
  existing public pan API so the world origin sits at the screen center.
- **Picking.** The overlay container has `PickingMode.Ignore`; only its panels catch clicks.
- **Closing.** A drawer closes from its × button, from its top-bar toggle, or from any press on the map (trickle-down
  on the board layer; the board still receives the same press). One drawer per side is open at a time.
- **Drawers.** Left: Character, Inventory. Right: Combat, Catalog (wide), Assets. Phases 2–5 fill the placeholders.
- **Dock.** Bottom-right, max 55% height, scrollable, collapsible (−/+); holds roll panel + game log.
- **Membership.** The trial's Player and Observer users are stored campaign members (Host is MainGM already), so
  membership-checked services of later phases see the same actors the role selector offers.

## 7. Expected behavior

- Map is the bottom layer and fills the screen in every panel state; panels never split the screen into a grid.
- Pan/zoom/select/box/marker/draw walls-doors-windows/fog/facing work exactly as before.

## 8. Deliverables

Code + tests listed in §5, this contract.

## 9. Acceptance criteria

1. Map occupies the whole screen; panels overlay it. ✔ (layout; visual check by owner)
2. All panels are slide-in / collapsible overlays. ✔
3. No board regression — board presenter logic unchanged; board tests unchanged. Owner Play-mode check pending.
4. Tests green — pending owner run (see §10).

## 10. Tests and validation

| ID | Test | Check |
|---|---|---|
| `TC-GAMESHELL-001` | `GameShellPresenterTests.Build_CreatesFullScreenBoardLayerUnderPickingTransparentOverlay` | layer order, picking |
| `TC-GAMESHELL-002` | `Drawers_StartClosed_OnePerSide_MapClickClosesAll` | drawer rules |
| `TC-GAMESHELL-003` | `Dock_IsCollapsible_AndRoleBadgeFollowsSelection` | dock, role indicator, dispose |
| `TC-GAMESHELL-004` | `TrialScreen_HostsBoardFullBleed_AndPanelsAsOverlays` | trial composition |

Adapted existing test (DOM/layout only, check unchanged): `RealMouseClick_DrawWallTool_RealPointerDownMoveUp_CreatesAnObstacle`
(TC-BOARD-121) drags at board offset (140,140)→(240,140) instead of (40,40)→(140,40), because the top-left of the
full-screen board is now under the top bar. `trial-screen` and `trial-controls-column` names are preserved.

Required commands: `test-unity.ps1`, `build-dev.ps1`, `test-fast.ps1`, `verify-format.ps1`, `verify-repository.ps1`.
**Not run in this session** (no Unity/pwsh/.NET in the container; product-owner-approved local validation).

Manual validation (owner): Play → Open Trial UI → map fills the window; open/close each drawer; click the map to close;
collapse the dock; pan (middle drag), zoom (wheel), select/box-select, right-click marker, MainGM draw wall/door/window,
fog as Player.

## 11. Compatibility, migration, and rollback

No persisted data or contract change. Rollback = revert the phase commits.

## 12. Dependencies and licensing

None.

## 13. Security, privacy, and hidden information

Fog/visibility logic is untouched; overlays show nothing the old columns did not.

## 16. Definition of Done

Code + docs committed; owner validation per §10.

## 17. Completion evidence

- `git diff --name-status main -- Packages DotNet` empty.
- Validation: not run in container.

## 18. Blockers, decisions, and change control

- Decisions recorded in §6.
