# ODY-S11-212 — Unified tab convention (UI polish P0, item 3)

**Status:** In Review  
**Roadmap stage / slice:** SLICE-11 polish P0 (items 1–5: `ODY-S11-210`…`ODY-S11-214`)  
**Owner:** Claude Code  
**Requested by:** Product owner  
**Branch:** `claude/pensive-gates-n18srp`  
**Pull request:** Not opened  
**ExecPlan:** this contract (client UI only)  
**Created:** 2026-10-01  
**Last updated:** 2026-10-01

## 1. Goal

One tab convention across the client:

- The active tab has an accent fill and border plus bold text, using Phase 0 palette colours.
- Sub-tabs inside a screen are pill-shaped (999px radius).
- Every tab button shows a focus ring during keyboard navigation.
- One active-state class is shared by the board's top-bar panel toggles, the character sheet tabs and the catalog.

## 2. Why this task exists

Before this task the client had three different conventions:

- character-sheet tabs used an accent underline;
- top-bar toggles used a separate `ody-button--toggle-on` class;
- the catalog type filter was a dropdown.

No tab showed keyboard focus.

## 3. Authorities and requirement references

- `ODY-S11-200` (palette, tokens), `ODY-S11-201` (top bar), `ODY-S11-202` (catalog), `ODY-S11-203` (sheet).
- New test IDs: `TC-TABS-001`…`TC-TABS-004`.

## 4. Scope

### In scope
- `OdysseyUiKit.cs`:
  - the single active class `OdyClasses.TabActive` (`ody-tab--active`), set through `OdyUi.SetActive` / `IsActive`;
  - `OdyClasses.TabPill`;
  - `OdyUi.TabButton`, a focusable tab-convention button;
  - new `OdyTabBar`, a tab row without panels;
  - `OdyTabs` is now built on `OdyTabBar` and has a `pill` option;
  - new `OdyFocusVisible`.
- `ButtonToggleOn` / `.ody-button--toggle-on` are removed.
- Top-bar panel toggles (`GameShellPresenter`) are tab-convention buttons with the shared active class. The shell
  owns an `OdyFocusVisible` on its screen.
- Character sheet: `new OdyTabs("character-tabs", pill: true)`.
- Catalog: the type filter is an `OdyTabBar` of pills ("All types" + one per supported type), kept in sync with
  `SetTypeFilter`.
- USS section 9 rewritten, plus the `--ody-color-focus` token (the Phase 0 info blue `#2B6CB0`).
- Design doc "Tabs" section; tests; test catalog.

### Out of scope
- The board's floating tool strip (`BoardToolbarPresenter`, Select/Wall/Door tools). It still uses its own inline
  border highlight from `ODY-S11-208`. This is a recorded follow-up, not changed here.
- The catalog status filter (stays a dropdown) and the selected-row highlight of lists (`ody-list-item--selected`).
- Backend: untouched.

## 5. Decisions

- **`:focus-visible` emulation.** USS has neither `:focus-visible` nor `outline`, so `OdyFocusVisible` works this
  way:
  - It listens trickle-down on the screen.
  - A `NavigationMoveEvent`, or a `KeyDownEvent` with Tab, adds `.ody-focus-visible`; the next `PointerDownEvent`
    removes it.
  - The ring is `.ody-focus-visible .ody-tab:focus { border-color: --ody-color-focus }`. It stays visible on an active
    tab too, because that selector is more specific than `.ody-tab--active`.
  - Every tab has a permanent transparent 2px border, so active and focus states never shift the layout.
- **Colours.** The focus ring uses the info blue, not the accent, so the user can tell "focused" from "active".
  Active colours are `--ody-color-accent-soft` (fill), `--ody-color-accent` (border, text) and bold text.
- **Pill shape** is applied only where the task names it: sub-tabs inside a screen (character sheet) and the
  catalog type filter, which also sits inside a screen. The top-bar toggles keep the small radius.

## 6. Acceptance criteria

1. Active = accent fill + accent border + bold, using Phase 0 palette tokens. ✔
2. Pill (999px) for sub-tabs inside screens (character sheet). ✔
3. Focus ring on every tab button during keyboard navigation. ✔ (emulated, see §5)
4. One active-state class for top-bar toggles, character sheet and catalog. ✔
5. Visible Tab-key navigation: requires the manual check below.

## 7. Tests and validation

| ID | Test (`OdyTabConventionTests`) |
|---|---|
| `TC-TABS-001` | `TabBar_HasExactlyOneActiveTab_PillShape_FocusableButtons_AndNotifiesOncePerChange` |
| `TC-TABS-002` | `FocusRing_ShowsWhileNavigatingByKeyboard_AndHidesOnThePointer` |
| `TC-TABS-003` | `SameActiveClass_OnTopbarToggles_CharacterSheetTabs_AndCatalogTypeFilter` |
| `TC-TABS-004` | `CatalogTypeTabs_FilterTheList_AndFollowSetTypeFilter` |

Existing assertions `TC-GAMESHELL` (`Drawers_StartClosed_…`) and `TC-POPOVER-008` now expect `OdyClasses.TabActive`
on the toggle instead of the removed `ButtonToggleOn`. The behaviour checked is unchanged.

**Batchmode-verifiable:** classes, single active state, pill class, focusable buttons, filter sync, and the
focus-ring class toggling. Tests call `NoteKeyboardNavigation` / `NotePointer` because EditMode has no event dispatch.

**Requires human visual confirmation in Play mode:**
1. Active tabs show the light red fill, red border and bold red text: character sheet, top-bar toggles of open
   drawers, and the catalog type filter.
2. Character-sheet and catalog-filter tabs are pill-shaped.
3. Pressing Tab moves keyboard focus across the top-bar toggles and tabs, with a visible blue ring on the focused tab.
   The ring disappears after a mouse click and comes back on the next Tab.
4. Hover still works, and nothing shifts when a tab becomes active or focused.

**Run in this session** (scratch build outside the repository; no Unity or pwsh in the container):
- Client sources and EditMode tests compiled against UnityEngine stand-ins.
- **68/68 passed**.
- `git diff origin/main -- Packages DotNet` is empty.

**Not run:** `test-unity.ps1`, `test-fast.ps1`, `verify-format.ps1`, `verify-repository.ps1`, `verify-docs.ps1`,
`build-dev.ps1`. Owner validation is pending.

## 7a. Defect found in verification (ODY-S11-215)

Real-Unity verification showed that a physical **Tab** key press never reached UI Toolkit. The input module has no
Next/Previous navigation, and Tab was bound to nothing. As a result neither focus movement nor the ring worked from
Tab; arrows were fine. The EditMode test `TC-TABS-002` calls `OdyFocusVisible` directly and could not catch this.
Fixed in `ODY-S11-215` (`UiTabNavigation`, PlayMode test `TC-FOCUSRING-TAB-001`).

## 8. Security, privacy, compatibility

No data or contract change. Rollback = revert the commit. No dependency added.
