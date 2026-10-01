# ODY-S11-216 — Marching ants for board selection (UI polish P1, item 6)

**Status:** In Review  
**Roadmap stage / slice:** SLICE-11 polish P1 (items 6–11: `ODY-S11-216`…`ODY-S11-221`)  
**Owner:** Claude Code  
**Requested by:** Product owner  
**Branch:** `claude/pensive-gates-n18srp`  
**Pull request:** Not opened  
**ExecPlan:** this contract (client UI only)  
**Created:** 2026-10-01  
**Last updated:** 2026-10-01

## 1. Goal

Selected tokens and the box-select rectangle get an animated dashed outline ("marching ants") whose dash offset keeps
moving. The effect is purely visual: selection logic is unchanged.

## 2. Verified current state

- Both outlines were **static**:
  - selected tokens: an inline 3px solid border (`RenderTokens` / `UpdateSelectionBorders`);
  - the box-select rectangle (`ODY-S08-107`, `UpdateBoxElement`): an inline 1px solid border.
- USS has no dashed borders and no keyframe animation.

## 3. Source and what was transferred

- **Source:** Owlbear Rodeo 1.0, where selected items get a dashed outline whose offset grows every frame.
- **Transferred:** the technique only; it is drawn anew with UI Toolkit `Painter2D`.
- **Executor's choice, not published by Owlbear:** dash length 6px, gap 4px, line width 1.5px, speed 24 px/s.

## 4. Scope

### In scope
- `Runtime/Ui/OdyMarchingAnts.cs` (new). An overlay that ignores picking:
  - a pure `Dashes(width, height, dash, gap, phase)` geometry, clockwise from the top-left, split at corners;
  - `Advance(ms)` stepped by the UI Toolkit scheduler;
  - two-tone drawing: dark dashes over a light line, with colours from USS `.ody-marching-ants`.
- `OdyMotion.IsReducedMotion(element)` and `OdyClasses.ReducedMotion` (`ody-reduced-motion`): the hook for the P2
  reduced-motion setting. Under that class on any ancestor the dashes stand still and the outline remains.
- `BoardScreenPresenter`:
  - selected tokens get an outline 4px outside their border, and deselection removes it;
  - the box-select rectangle gets one too;
  - `SelectionOutline(TokenId)` and `SelectionBoxOutline` are exposed for tests.
  - The existing 3px/1px borders are **kept unchanged**, so no existing assertion changes.
- USS section 16; tests; test catalog.

### Out of scope
- Selection logic, obstacle selection, the reduced-motion setting itself (P2). Backend: untouched.

## 5. Tests and validation

| ID | Test |
|---|---|
| `TC-ANTS-001` | `OdyMarchingAntsTests.Dashes_CoverThePerimeterWithTheDashPattern_SplittingAtCorners` |
| `TC-ANTS-002` | `OdyMarchingAntsTests.Phase_ShiftsTheDashes_AndAFullPeriodLooksTheSame` |
| `TC-ANTS-003` | `OdyMarchingAntsTests.Advance_MovesAndWrapsThePhase_ButNotUnderReducedMotion` |
| `TC-ANTS-004` | `BoardScreenPresenterTests.SelectedTokensAndTheSelectionBox_GetAMarchingAntsOutline_DeselectRemovesIt` |

**Batchmode-verifiable:** all four. That covers geometry, stepping, reduced-motion freeze, and that the outline is
attached and removed.

**Requires human visual confirmation in Play mode:**
1. Click a token: a dark/white dashed outline appears just outside it, and the dashes visibly travel around it.
2. Drag a selection box: its outline marches too.
3. Deselecting removes the outline.
4. Pan and zoom keep the outline on the token.

**Compilation and test status in this container** (no Unity, no Input System, no PowerShell):
- **Not compiled against real UnityEngine.** All new and changed files were compiled only against stand-in types.
- `OdyMarchingAnts.cs`, `OdyMotion.cs`, `OdysseyUiKit.cs` and the three `OdyMarchingAntsTests`: compiled against the
  stand-ins, with the tests **75/75 passed** together with the earlier SLICE-11/P0 EditMode tests. Real
  `Painter2D` drawing is not exercised by that run.
- `BoardScreenPresenter.cs` and `TC-ANTS-004`: a stand-in compile reported **no error on any changed line**
  (remaining errors are stand-in gaps in unchanged code). The test was **not run**.
- `git diff origin/main -- Packages DotNet` is empty.

**Not run:** `test-unity.ps1`, `test-fast.ps1`, `verify-*.ps1`, `build-dev.ps1`. Owner run pending.

## 6. Security, privacy, compatibility

Hidden tokens are never selectable (pre-existing rule), so an outline never marks one. No data or contract change.
Rollback = revert the commit.
