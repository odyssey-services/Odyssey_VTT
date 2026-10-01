# ODY-S11-225 — Keyboard parity: Escape closes popovers and modals, Enter submits single-line fields (UI polish P2, item 14)

**Status:** In Review  
**Roadmap stage / slice:** SLICE-11 polish P2 (items 12–14: `ODY-S11-223`…`ODY-S11-225`)  
**Owner:** Claude Code  
**Requested by:** Product owner  
**Branch:** `claude/pensive-gates-n18srp`  
**Pull request:** Not opened  
**ExecPlan:** this contract (client UI only)  
**Created:** 2026-10-01  
**Last updated:** 2026-10-01

## 1. Goal

- Escape closes any open modal or popup.
- Enter in a single-line form text field submits it, where that makes sense, without breaking multiline description
  fields.
- Tab between fields already works since `ODY-S11-215`.

## 2. Verified current state

- **Escape did nothing.**
  - No popover, drawer, dialog or select list listened for it.
  - The input module delivers keys only through bound actions (finding of `ODY-S11-215`). Escape is bound to
    `UI/Cancel` and Enter to `UI/Submit`; they reach UI Toolkit as `NavigationCancelEvent` and
    `NavigationSubmitEvent`, not as dependable raw `KeyDownEvent`s.
- **Enter in a text field did nothing**, so every action needed a click on its button.

## 3. Sources and what was transferred

- **Source:** Owlbear 2.1's keyboard parity: keyboard equivalents for pointer actions, and the reverse.
- **Transferred:** the technique only.

## 4. Scope

### In scope

**`OdyPopover` (`ODY-S11-210`) handles Escape:**
- While open, each popover listens for `NavigationCancelEvent` (trickle-down) at the top of its tree, which at
  runtime is the panel root. Escape is therefore seen wherever focus is.
- Only the **topmost** open popover closes. "Topmost" means the last visible popover in tree order, which is the one
  painted on top. It is found by walking the tree, with no registry or static state.
- The closing popover stops the event, so one press closes one popover: a select list over a drawer closes first,
  then the drawer on the next press.
- New close reason `Escape`. New option `CloseOnEscape`, default true.

**Behaviour per surface:**

| Surface | Escape |
|---|---|
| Drawers | close; their top-bar toggle follows |
| Select lists, tooltips | close |
| Confirmation dialogs | **Cancel**, never Confirm. A stray click on the scrim still does not dismiss them. |

**Enter:**
- `OdyUi.SubmitOnEnter(field, action)` / `OdyEnterSubmit` listen for `NavigationSubmitEvent` on a single-line
  `TextField`.
- Attaching it to a multiline field is refused, because Enter there is a new line.
- A disabled field does nothing.
- Applied where there is one obvious, non-destructive action:
  - character "Open by id";
  - combat "Open encounter by id";
  - character "Rename";
  - resource "Add".
- Deliberately **not** applied to the confirmation dialog's reason field: Enter must not confirm an irreversible
  action.
- Tests; test catalog.

### Out of scope
- Keyboard navigation between board tokens, and new shortcuts.
- Backend: untouched.

## 5. Tests and validation

| ID | Test | Runner |
|---|---|---|
| `TC-KEYPARITY-001` | `OdyKeyboardParityTests.Escape_ClosesOnlyTheTopmostOpenPopover_ThenTheNextOne` | EditMode |
| `TC-KEYPARITY-002` | `OdyKeyboardParityTests.Escape_OnAConfirmDialog_IsCancel_NeverConfirm` | EditMode |
| `TC-KEYPARITY-003` | `OdyKeyboardParityTests.Escape_ClosesAnOpenDrawer_AndItsToggleFollows` | EditMode |
| `TC-KEYPARITY-004` | `OdyKeyboardParityTests.Enter_SubmitsSingleLineFields_NotMultiline_NorDisabled` | EditMode |
| `TC-KEYPARITY-005` | `OdyKeyboardParityTests.Enter_InTheCharacterOpenByIdField_OpensThatCharacter` | EditMode |
| `TC-KEYPARITY-006` | `OdysseyPlayModeFoundationSmokeTests.RealEscapeKey_ClosesTheOpenDrawer_RealEnterKey_SubmitsTheOpenByIdField` | PlayMode |

`TC-KEYPARITY-006` drives a real `Keyboard` and `Mouse` through `InputTestFixture`:
1. A real click opens the Combat drawer, and a real Escape closes it.
2. A real Enter in "Open by id" submits it; the invalid id shows its banner.

**Requires human visual confirmation in Play mode:**
1. Escape closes, in turn: an open select list, then the drawer it is in, then the next drawer.
2. On a delete confirmation, Escape cancels and nothing is deleted.
3. Enter in "Open by id" or "Rename" acts like the button.
4. Enter in the multiline review comment adds a new line.

**Compilation and test status in this container** (no Unity, no Input System, no PowerShell):
- **Not compiled against real UnityEngine.**
- `OdyPopover.cs`, `OdysseyUiKit.cs`, the four call sites and `TC-KEYPARITY-001…005` were compiled against stand-ins
  and run there: **91/91 passed**.
- **`TC-KEYPARITY-006` is not compiled at all here.** It needs the Input System test framework, which this container
  does not have. It was reviewed by reading only. **Compilation unconfirmed.**
- Backend untouched by this item.

**Not run:** `test-unity.ps1`, `test-fast.ps1`, `verify-*.ps1`. Owner run pending.

## 6. Security, privacy, compatibility

- Escape never confirms. Enter is never attached to destructive confirmations.
- No data or contract change. Rollback = revert the commit.
