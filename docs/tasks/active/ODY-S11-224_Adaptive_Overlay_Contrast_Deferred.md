# ODY-S11-224 — Adaptive contrast for overlays on arbitrary maps (UI polish P2, item 13): deferred as a follow-up

**Status:** Closed — deferred to a separate follow-up (no client change)  
**Roadmap stage / slice:** SLICE-11 polish P2 (items 12–14: `ODY-S11-223`…`ODY-S11-225`)  
**Owner:** Claude Code  
**Requested by:** Product owner  
**Branch:** `claude/pensive-gates-n18srp`  
**Pull request:** Not opened  
**ExecPlan:** Not required  
**Created:** 2026-10-01  
**Last updated:** 2026-10-01

## 1. Goal (as requested, a soft requirement)

If it is technically simple, pick the colour of grid lines and measurement labels automatically to contrast with the
map underneath. This is the Owlbear 2.1 technique. Otherwise defer it explicitly as a separate follow-up.

## 2. Verified current state (`BoardScreenPresenter`)

- **There is no grid and there are no measurement labels.** A search of the client finds no grid rendering. The
  board's own documentation lists hex-grid rendering as out of scope.
- Overlays that do sit on the map, all with fixed inline colours:

  | Overlay | Colour today |
  |---|---|
  | Obstacles | coloured **by kind**: wall grey, closed door dark brown, open door light brown, window translucent blue. The colour carries meaning. |
  | Player marker ring | yellow |
  | Wall-draw preview | yellow |
  | Box-select | blue fill and border |
  | Fog | black at fixed alpha |
  | Token fill | blue for own tokens, red for others |
  | Marching-ants outline (`ODY-S11-216`) | **already contrast-independent by design**: dark dashes over a white line, readable on light and dark maps |

- The scene background is a `Texture2D` loaded through the asset cache. A mean luminance could be computed from it,
  for example a CPU pass over a downscaled copy.

## 3. Why deferred

- **The item's own subject does not exist.** Grid lines and measurement labels, the things Owlbear adapts, are not
  part of the board, so there is nothing to recolour.
- **Recolouring what does exist is not "technically simple"; it is a design decision.**
  - Obstacle colours encode the obstacle kind. Changing them per map needs a rule for keeping wall, door and window
    distinguishable on both light and dark maps, which is a palette and product decision.
  - Measuring map luminance needs a texture read pass. The background is drawn as a style background image, not a
    material, so the pass would also have to handle non-readable textures and run again when the background changes.
  - That is more than a polish tweak, and the item says not to block the rest of the list with it.
- The one decorative overlay where contrast matters most, the selection outline, is already self-contrasting.

## 4. Follow-up (proposed, separate task)

1. When a grid or measurement overlay is introduced, give it an adaptive colour from the start:
   - compute the mean luminance of the scene background once per background change, on a small downscaled copy;
   - choose a light or dark line colour from it;
   - expose the result through a design-system class, not inline colours.
2. Optionally apply the same light-or-dark choice to the marker ring and the wall-draw preview. Keep the obstacle-kind
   palette fixed unless the product owner defines a dual palette.

## 5. Validation

- No code changed, so there are no tests.
- `verify-docs.ps1` was not run (no PowerShell in the container).
- Backend untouched by this item.
