# ODY-S11-219 — Tooltip on truncated text, as a universal rule (UI polish P1, item 9)

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

Any text cut with an ellipsis shows its full text in a hover tooltip, and only when it really is cut. This applies to
the catalog definitions list, the character roster, inventory item lists, and the combat lists and journal.

## 2. Verified current state

- List row titles and meta lines **wrapped** (`white-space: normal`), so nothing was truncated, and no tooltip
  existed anywhere.
- **Runtime UI Toolkit does not display the built-in `VisualElement.tooltip`**; it is an Editor-only feature. The
  tooltip must therefore be our own element.

## 3. Source and what was transferred

- **Source:** Owlbear Rodeo 2.1 shows an elided asset name in full on hover.
- **Transferred:** the technique only. It is built on the P0 `OdyPopover` primitive (`ODY-S11-210`), not as a second
  floating-surface implementation.

## 4. Scope

### In scope
- `Runtime/Ui/OdyTruncationTooltip.cs` (new), behaving as follows:
  1. On `PointerEnterEvent` it measures the label: `MeasureTextSize` against `contentRect.width`.
  2. Only if the text is wider, it opens a non-interactive `OdyPopover` below the label with the full text. The
     popover ignores picking, so it never steals the hover.
  3. It closes on `PointerLeaveEvent`, and on `DetachFromPanelEvent`, so a list re-render leaves nothing behind.
  4. The instance is stored in the label's `userData`.
- `OdyUi.TruncatedText(text, classes)`: the factory that applies the rule.
- Applied to row titles and meta lines (14 call sites):
  - catalog definitions;
  - character roster;
  - character sheet ability/skill rows;
  - inventory items;
  - combat item list, pending attacks, stacking conflicts, journal.
- USS section 17:
  - `.ody-text-truncate` (one line, `text-overflow: ellipsis`);
  - `.ody-tooltip` (dark pill, white text, max 360px wide);
  - `.ody-list-item__main` can now shrink.
- Design doc rule 6; tests; test catalog.

### Out of scope
- Long free text such as banners, hints and descriptions. They keep wrapping on purpose, because cutting them would
  hide information.
- Backend: untouched.

## 5. Decisions

- **Rows are now single-line with an ellipsis.** This is a deliberate layout change that the item implies. List
  rows stay one line high, and long names are read in the tooltip.
- **Measured on hover, not on every layout.** That is cheap and always reflects the current width. Text that fits
  never gets a tooltip.

## 6. Tests and validation

| ID | Test (`OdyTruncationTooltipTests`) |
|---|---|
| `TC-TRUNC-001` | `WouldTruncate_OnlyWhenTheTextIsWiderThanItsSpace` |
| `TC-TRUNC-002` | `Tooltip_ShowsTheFullTextOnlyWhenCut_AndHides` |
| `TC-TRUNC-003` | `ListRows_UseTheRule_CatalogDefinitionNames` |

**Batchmode-verifiable:** the decision rule, show and hide with the full text, and that the lists use the rule. The
measurement is fed in, because EditMode has no text layout.

**Requires human visual confirmation in Play mode:**
1. Create a catalog definition with a long name. In the catalog list the name ends in "…".
2. Hovering it shows the full name in a dark tooltip below it, and leaving hides it.
3. A short name shows no tooltip.
4. The same applies in the character roster and the inventory list.

**Compilation and test status in this container** (no Unity or PowerShell):
- **Not compiled against real UnityEngine.**
- New and changed files compiled against stand-ins only. That covers `MeasureTextSize`, `MeasureMode` and the pointer
  and detach events, all of which are stand-ins here.
- Tests ran in the stand-in run: **80/80 passed**.
- `git diff origin/main -- Packages DotNet` is empty.

**Not run:** `test-unity.ps1`, `test-fast.ps1`, `verify-*.ps1`. Owner run pending.

## 7. Security, privacy, compatibility

- The tooltip repeats text already in the label and never adds information.
- No data or contract change. Rollback = revert the commit.
