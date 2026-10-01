# ODY-S11-221 — Live numeric readout during resize/rotate (UI polish P1, item 11): not applicable

**Status:** Closed — not applicable (no client change)  
**Roadmap stage / slice:** SLICE-11 polish P1 (items 6–11: `ODY-S11-216`…`ODY-S11-221`)  
**Owner:** Claude Code  
**Requested by:** Product owner  
**Branch:** `claude/pensive-gates-n18srp`  
**Pull request:** Not opened  
**ExecPlan:** Not required  
**Created:** 2026-10-01  
**Last updated:** 2026-10-01

## 1. Goal (as requested)

If the board has an interactive resize or rotate gesture for tokens, show a floating label with the current value
(grid cells or degrees) for the whole time the gesture is held. If no such gesture exists, mark the item "not
applicable" and do not invent one.

## 2. Verified current state (`BoardScreenPresenter`, `BoardTokenInspectorPresenter`)

- **No resize or rotate drag gesture and no handles exist.** Searching the board for resize, rotate and handle
  finds none for tokens. The only `style.rotate` (`PositionSegmentElement`) orients the drawn obstacle line.
- **Token size** changes only by **Shift+wheel over a token** (`HandleBoardWheel` → `ScaleTokenAt`, `ODY-S08-105`).
  - Each wheel notch immediately commits one `SetTokenScale` step (×/÷ the step factor, clamped to
    `TokenRecord.MinScale`…`MaxScale`).
  - There is no press-and-hold period during which a value could be shown "until release".
- **Token facing (rotation)** is a number field with an "Apply Facing" button in the token inspector (SLICE-10
  Block 6 part 3). It is not a gesture.

## 3. Decision

- **Item closed as "not applicable": Odyssey has no interactive resize/rotate gesture.** Nothing was invented.
- **Optional follow-ups**, for the product owner to decide; not done here:
  - a short-lived scale readout (for example "×1.25") next to the cursor after a Shift+wheel notch;
  - a live length readout while drawing a wall with the obstacle draw gesture, which is the board's only
    press-drag-release gesture with a live preview.
  - Neither is the resize/rotate the item describes.

## 4. Validation

- No code changed, so there are no tests.
- `git diff origin/main -- Packages DotNet` is empty.
- `verify-docs.ps1` was not run (no PowerShell in the container).
