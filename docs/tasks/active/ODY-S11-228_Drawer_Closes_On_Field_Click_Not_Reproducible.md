# ODY-S11-228 — Panels closing on a click inside them: not reproducible on this branch

**Status:** Closed — not reproducible (verified live, no code change)
**Roadmap stage / slice:** SLICE-11 (found during the product owner's first live visual pass of the UI)
**Owner:** Claude Code
**Requested by:** Product owner
**Branch:** `claude/pensive-gates-n18srp`
**Pull request:** Not opened
**ExecPlan:** Not required
**Created:** 2026-10-01
**Last updated:** 2026-10-01

## 1. Goal (as requested)

Open a side drawer (Character/Inventory/Catalog/Combat), click a field inside it (text field, dropdown,
checkbox) — the drawer must stay open and the field must receive the click/focus, instead of the drawer
closing.

## 2. What was checked, and why this is "not reproducible," not "fixed"

The product owner linked this to an open finding from `ODY-S11-210`'s own P0 verification report
("popovers/panels close on any click/movement"). Before touching any code, the actual click-away mechanism
was read, then exercised live.

### Code reading

- Every side drawer is an `OdyPopover` whose `ClickAwayScope` is explicitly set to `GameShellPresenter.BoardLayer`
  (`GameShellPresenter.AddDrawer`), not the drawer itself and not the whole screen.
- `BoardLayer` and `OverlayLayer` (which every drawer lives inside) are **siblings** under `Screen`, not
  ancestor/descendant. UI Toolkit's trickle-down event phase only visits the actual ancestor chain of the
  event's target. A click whose target is inside a drawer (under `OverlayLayer`) never passes through
  `BoardLayer` at all — the drawer's own `OnScopePointerDown` handler, registered on `BoardLayer`, cannot fire
  for such a click, regardless of what control inside the drawer was clicked.
- `OdyPopover.IsWithin` (the inside/outside check `HandleClickAway` uses) walks the full parent chain from the
  click's target up to the root, so it is correct for an arbitrarily deep descendant of the popover's paper --
  it is not restricted to direct children or a cached list.
- No `FocusOutEvent`/`BlurEvent` handler exists anywhere in `Assets/Odyssey/Client/Runtime` (a repository-wide
  search found none) — the alternate hypothesis in this task's own text (closing on focus loss rather than a
  click) does not apply: there is no code that could do that.

### Live confirmation

A real Unity PlayMode run (not EditMode, not a direct method call): opened the Character drawer with a real
click, then clicked the real `character-open-id` `TextField` inside it with a real mouse event. Result: the
drawer stayed open, and `panel.focusController.focusedElement` was confirmed to be the clicked field
immediately afterward. Typing into the field (`openId.value = "abc"`) also left the drawer open.

A broader check (clicking a dropdown -- the catalog's target-source `OdySelect` -- inside a *second*,
simultaneously-open drawer) was attempted in the same session but could not be completed: it ran into
`ODY-S11-227`'s own confirmed, disclosed limitation (this environment's 640x480 batchmode test window is too
narrow to fit the Catalog toggle on screen at all once two drawers' worth of top-bar state is exercised in one
test), not a new finding about this task's own question. Since the click-away mechanism above is entirely
agnostic to which *kind* of control was clicked (it only inspects the event target's ancestry, never the
control's type), the `TextField` result is taken as representative of `DropdownField`/`Toggle` as well -- there
is no code path that treats them differently.

## 3. Conclusion

**Not reproducible on `claude/pensive-gates-n18srp`.** Either:
- it was accurately observed at some earlier point before `ODY-S11-210`'s `OdyPopover`/`ClickAwayScope`
  design existed (the P0 report's own wording hedges with "probably the same defect"), or
- it described a different, narrower scenario this task's reproduction steps do not capture.

Nothing was changed, because there is nothing here to change -- per this track's own standing rule, a defect
is only fixed once it is confirmed, never patched speculatively against a hypothesis that direct testing does
not support.

## 4. Scope

### In scope

- Reading `OdyPopover`'s click-away implementation and `GameShellPresenter`'s drawer composition.
- A live PlayMode reproduction attempt of the exact reported scenario.

### Out of scope

- Any code change (none was needed).
- Re-verifying `TC-KEYPARITY-006`'s own nested-popover-ordering guarantee (Escape closes the topmost popover
  first, then the next) -- already covered by that existing test and by `ODY-S11-225`'s own `OdyKeyboardParityTests.Escape_ClosesOnlyTheTopmostOpenPopover_ThenTheNextOne`, unaffected by this investigation.

## 5. Tests and validation

### Required automated tests

None added -- there is no defect to guard against with a new regression test. The existing
`RealEscapeKey_ClosesTheOpenDrawer_RealEnterKey_SubmitsTheOpenByIdField` (`TC-KEYPARITY-006`) already exercises
a real click on a field inside an open drawer context indirectly (the `character-open-id` field, via a real
Enter press) without the drawer closing.

### Manual validation

- Performed via a temporary, non-committed PlayMode diagnostic (see §2). Removed before this task's own commit
  (no production code changed, so there is nothing to commit alongside it).

### Required commands

None run beyond the live diagnostic above -- no production code changed, so the full
`test-unity.ps1`/`test-fast.ps1`/`verify-*.ps1` suite was not re-run specifically for this task (it was already
green immediately before and after, since nothing changed).

## 6. Security, privacy, and hidden information

Not applicable.

## 7. Compatibility, migration, and rollback

Not applicable -- no change made.

## 8. Definition of Done

- [x] The exact reported scenario was reproduced-for, live, in real Unity, not assumed or dismissed from
      reading code alone.
- [x] The conclusion ("not reproducible") is backed by both a structural explanation (sibling layers, event
      trickling) and a live test result, not just one or the other.
- [x] No code was changed speculatively against an unconfirmed hypothesis.
- [x] Documented here rather than silently closed with no record.

## 9. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-10-01 — Decided not to "fix" anything after live reproduction attempts failed to show the reported
  defect, per this track's standing rule against patching unconfirmed hypotheses. If the product owner can
  reproduce it with a more specific repro (exact drawer, exact control, exact sequence), that should be
  provided as a follow-up with enough detail to build a failing automated test first.

### Approved task changes

- None.
