# ODY-S11-210 — Unified `OdyPopover` primitive (UI polish P0, item 1)

**Status:** In Review  
**Roadmap stage / slice:** SLICE-11 polish P0 (items 1–5: `ODY-S11-210`…`ODY-S11-214`)  
**Owner:** Claude Code  
**Requested by:** Product owner  
**Branch:** `claude/pensive-gates-n18srp`  
**Pull request:** Not opened  
**ExecPlan:** this contract (single module: client UI)  
**Created:** 2026-10-01  
**Last updated:** 2026-10-01

## 1. Goal

One popover component for every floating surface of the client. It can be anchored to an element or to
coordinates, and its pivot sets the growth direction (Left/Center/Right × Top/Center/Bottom). It takes a fixed or
content size, which `SetWidth`/`SetHeight` can change after opening. It supports `hidePaper`, `disableClickAway`
and a minimum edge margin so it is never clipped by the viewport. The existing drawers, confirmation modals and
catalog dropdown selectors move onto it.

## 2. Why this task exists

Phase 0 and phase 1 built three ad-hoc floating surfaces: drawers with fixed USS offsets, a scrim with a centred
modal, and Unity's built-in `DropdownField` menu. None of them handles viewport edges, anchoring or click-away
the same way. The technique is taken from the Owlbear Rodeo UI/UX research: one popover primitive with anchor and
transform origin. Only the technique was transferred, no code (Owlbear Legacy is GPLv3). The 8px default margin
is the executor's choice, not a published Owlbear value.

## 3. Authorities and requirement references

- `AGENTS.md` §5, §9 (explicit composition), ADR-001/ADR-005; `ODY-S11-200` (design system), `ODY-S11-201` (drawers).
- New test IDs: `TC-POPOVER-001`…`TC-POPOVER-008`.

## 4. Scope

### In scope
- `Assets/Odyssey/Client/Runtime/Ui/OdyPopover.cs` (new): `OdyPopover`, `OdyPopoverOptions`, `OdyPopoverAnchor`,
  `OdyPopoverOrigin`, `OdyPopoverLayout` (pure placement), `OdySelect`.
- Migrated call sites (3):
  1. **Drawers** (`GameShellPresenter.AddDrawer`): persistent popovers. They are anchored to the overlay's top corner on
     their side (inset 12px, below the 68px top bar), grow inwards and run to the bottom edge. Click-away scope is
     the board layer, which replaces the shell's own map-press callback.
  2. **Confirmation modals** (`OdyConfirmDialog`): a modal popover centred on its host, with click-away disabled.
  3. **Catalog form selectors**: `TargetRuleEditor` "Target source" and every enum field of the catalog form
     (`ContentCatalogPresenter.AddEnum`, e.g. item category, weapon mode or ammunition) are now `OdySelect`. Its option
     list is a popover anchored below the field.
- `OdyClasses` constants, `OdyUi.Select`; USS section 15 plus drawer rules reduced to styling only.
- `docs/ui/Odyssey_Design_System.md` (popover section, rule 5); tests; test catalog.

### Out of scope
- Other `DropdownField`s: character, inventory and combat forms, the role selector, roll panel and token inspector.
  They are pre-existing call sites, and the rule "new code uses only `OdyPopover`" covers them from now on.
- Escape-key closing, flip-to-other-side placement, open/close animation.
- Backend: untouched.

## 5. Decisions

- **Click-away never swallows input.** Non-modal popovers listen trickle-down on their scope and never stop
  propagation, so a map press both closes a drawer and still pans or selects (as in `ODY-S11-201`). Presses inside
  the popover or on its anchor are ignored, so the anchor's own toggle decides.
- **Edge margin.** Placement moves the popover inside `viewport − margin` on every side. A fixed size larger than
  that is shrunk. A content-sized popover gets inline `max-width`/`max-height` caps rather than a pinned size, so
  it can still resize with its content without a geometry-callback loop.
- **Host.** Panel popovers (`OdySelect`) mount on the nearest ancestor marked `.ody-popover-host` (the game screen),
  found by walking the element's own parents, with no lookup. That keeps them above every panel and inside the
  root that carries the design-system stylesheet (`ODY-S11-209`).
- `OdySelect` raises its own `ValueChanged`, so form code and tests do not depend on UI Toolkit change-event dispatch.
- Callbacks (pointer and geometry) are registered only while a popover is open and removed on close or dispose.

## 6. Acceptance criteria

1. One `OdyPopover` with all parameters listed in §1. ✔
2. At least 3 existing call sites migrated: drawers, confirmation modals and catalog selectors. ✔
3. Existing SLICE-11 tests unchanged in intent. The only edit is `TC-CATALOGUI` `TargetRuleEditor_…`, which now
   finds the selector as `OdySelect` and additionally checks that picking a value edits the model.
4. Backend untouched. ✔

## 7. Tests and validation

| ID | Test (`OdyPopoverTests`) |
|---|---|
| `TC-POPOVER-001` | `Layout_PivotSetsTheGrowthDirection` |
| `TC-POPOVER-002` | `Layout_KeepsTheEdgeMargin_MovingAndShrinkingSoNothingIsClipped` |
| `TC-POPOVER-003` | `ElementAnchor_UsesTheAnchorOriginAndOffset_PointAnchorUsesCoordinates` |
| `TC-POPOVER-004` | `SetWidthAndSetHeight_ChangeTheSizeWhileOpen` |
| `TC-POPOVER-005` | `ClickAway_ClosesOnOutsidePresses_NotInsideOrOnTheAnchor_AndCanBeDisabled` |
| `TC-POPOVER-006` | `HidePaper_Modal_AndPersistentPopoversBuildTheRightElements` |
| `TC-POPOVER-007` | `Select_OpensItsListAsAPopover_ChoosingRaisesValueChangedAndCloses` |
| `TC-POPOVER-008` | `MigratedCallSites_ConfirmDialogDrawersAndCatalogSelectorsUseThePopover` |

**Batchmode-verifiable** (logic, no layout pass): everything in the table. Placement is checked through the pure
`OdyPopoverLayout` and `OdyPopover.PlaceWithin` with explicit geometry.

**Requires human visual confirmation in Play mode** (not covered by any batchmode run):
- Drawers sit where they did before (left/right, below the top bar, to the bottom edge). The wide catalog drawer
  respects its 60% cap, and both follow a window resize.
- The confirmation dialog is centred over the scrim.
- A catalog selector (e.g. Weapon → Attack mode) opens its list directly below the field. Near the bottom of the
  window the list stays inside the window. Clicking outside closes it, and picking an option updates the form.
- A press on the map closes open drawers and still pans or selects on the same press.

**Run in this session** (scratch build outside the repository; no Unity/pwsh in the container):
- The client sources and EditMode tests compiled against UnityEngine stand-ins.
- **59/59 passed** (51 earlier SLICE-11 tests plus the 8 new ones).
- `git diff origin/main -- Packages DotNet` is empty.

**Not run:** `test-unity.ps1`, `test-fast.ps1`, `verify-format.ps1`, `verify-repository.ps1`, `verify-docs.ps1`,
`build-dev.ps1` (no PowerShell or Unity in the container). Owner validation is pending.

## 8. Security, privacy, compatibility

No data, contract or persistence change. Rollback = revert the commit. No dependency added.
