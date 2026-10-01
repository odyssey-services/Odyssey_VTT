# ODY-S11-215 — Tab key never reaches the UI; the keyboard focus ring never shows from Tab

**Status:** In Review
**Roadmap stage / slice:** SLICE-11 (polish P0, found during independent verification of `ODY-S11-212`)
**Owner:** Claude Code
**Requested by:** Product owner (found during real-Unity verification of P0 CoreFeel on `claude/pensive-gates-n18srp`)
**Branch:** `claude/pensive-gates-n18srp`
**Pull request:** Not opened
**ExecPlan:** Not required
**Created:** 2026-10-01
**Last updated:** 2026-10-01

## 1. Goal

Make pressing the Tab key actually reach the UI Toolkit panel (focus the next control and show the keyboard
focus ring), as `ODY-S11-212`'s own acceptance criteria require ("Кольцо фокуса видно при навигации Tab с
клавиатуры").

## 2. Why this task exists

- Problem: verified with a real Unity PlayMode run (not EditMode, not a direct method call) that a physical
  Tab key press produces **no reaction whatsoever** in the running app -- the focused element does not even
  change, and `OdyFocusVisible`'s ring never appears. An arrow key (Down), by contrast, correctly shows the
  ring in the same run. Root cause confirmed by reading
  `Assets/Odyssey/Client/Input/Odyssey.inputactions`: the `UI/Navigate` action (the one
  `InputSystemUIInputModule` turns into `NavigationMoveEvent`s) is bound only to arrow keys, WASD, and
  gamepad stick/dpad -- **Tab is not bound to anything in this asset.** `OdyFocusVisible.OnKeyDown`
  (`Assets/Odyssey/Client/Runtime/Ui/OdysseyUiKit.cs`) does separately listen for a raw `KeyDownEvent` with
  `keyCode == KeyCode.Tab`, but `InputSystemUIInputModule` only forwards the specific actions it is configured
  with (Navigate/Submit/Cancel/Point/Click/RightClick/MiddleClick/ScrollWheel) -- it does not forward arbitrary
  unbound keys as generic `KeyDownEvent`s, so that handler is effectively dead code against the actual input
  configuration.
- Value: `ODY-S11-212`'s own task contract and the P0 polish pass both state Tab specifically as the expected
  keyboard-navigation key (matching ordinary desktop UI conventions, where Tab -- not arrows -- cycles focus
  between controls/fields). Right now that claim is false for a real player.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `docs/tasks/active/ODY-S11-212_Unified_Tab_Convention.md` (states the Tab-ring requirement this task found false)

### Requirement and test IDs

- Requirement IDs: None
- Existing test IDs: `Odyssey.Tests.Unity.EditMode.OdyTabConventionTests.FocusRing_ShowsWhileNavigatingByKeyboard_AndHidesOnThePointer`
  -- this test calls `OdyFocusVisible.NoteKeyboardNavigation()` directly, bypassing the entire input pipeline,
  so it could not and did not catch this gap.
- New test IDs to introduce: `TC-FOCUSRING-TAB-001`
  (`OdysseyPlayModeFoundationSmokeTests.RealTabKey_MovesFocusForwardAndBack_ShowsTheFocusRing_PointerHidesIt_ArrowsStillNavigate`).
  It uses a real `Keyboard` device through `InputTestFixture` and never calls `OdyFocusVisible` directly.

### Task-safe private context

- Approved summary: None

## 4. Verified current state

### Verified facts

- Reproduced live: in a real Unity PlayMode run of the composed Trial screen, with an element given focus,
  pressing Tab (`InputTestFixture.Press/Release(keyboard.tabKey)`, several frames) produces no change in
  `panel.focusController.focusedElement` and no `ody-focus-visible` class on the screen root.
- Control check in the same run: pressing the Down arrow key (bound to `Navigate`) immediately shows the
  `ody-focus-visible` class, confirming the ring mechanism itself (`OdyFocusVisible.OnNavigationMove`) works
  correctly when it actually receives an event -- the defect is specifically that Tab produces no event at all,
  not that the ring logic is broken.
- `Assets/Odyssey/Client/Input/Odyssey.inputactions`, `UI` action map, `Navigate` action bindings: 2D vector
  composite over arrow keys and WASD, plus gamepad left stick/dpad. No `<Keyboard>/tab` binding anywhere in
  the asset.
- `Assets/Odyssey/Client/Runtime/Ui/OdysseyUiKit.cs` (`OdyFocusVisible`): registers both
  `NavigationMoveEvent` (`OnNavigationMove`) and a raw `KeyDownEvent` check for `KeyCode.Tab` (`OnKeyDown`).
  The latter never fires under the current action-based input module configuration.
- `AppShellEntryPoint.EnsureRuntimeUiInput()` wires `InputSystemUIInputModule.actionsAsset` from
  `InputSystem.actions` (the project-wide asset above) -- this is the real, production wiring, not a test-only
  path.

### Assumptions

- None. All facts above were confirmed live, not inferred.

## 5. Scope

### In scope

- `Assets/Odyssey/Client/Input/Odyssey.inputactions`: add a `<Keyboard>/tab` binding to the `Navigate` action
  (or a dedicated binding/action if the author prefers keeping Tab semantically distinct from directional
  navigation -- see §14 for the two options).
- `Assets/Odyssey/Client/Runtime/Ui/OdysseyUiKit.cs` (`OdyFocusVisible`): adjust only if the chosen fix makes
  the raw `KeyDownEvent`/`KeyCode.Tab` listener redundant or needs it kept as a fallback -- decide and record,
  do not leave silently-dead code without a comment explaining why it is still there.
- A new PlayMode regression test exercising a real Tab key press through the real input module.

### Out of scope

- Any other keyboard shortcut or navigation scheme.
- The rest of `ODY-S11-212`'s tab-bar/pill-shape work, already verified separately and unaffected by this bug.
- Backend -- not applicable (pure client input configuration).

### Allowed paths

```text
Assets/Odyssey/Client/Input/Odyssey.inputactions
Assets/Odyssey/Client/Runtime/Ui/OdysseyUiKit.cs
Assets/Odyssey/Client/Tests/PlayMode/OdysseyPlayModeFoundationSmokeTests.cs
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Must not change `Navigate`'s existing arrow-key/WASD/gamepad behavior -- those already work correctly and
  are relied on by the control check above.
- Must not introduce a `Resources.Load`/string-based lookup -- the Input Actions asset is already referenced
  via `InputSystem.actions`, an existing, approved mechanism; nothing new here needs to touch `TC-ARCH-001`.

## 7. Expected behavior

### Scenario 1 — Tab shows the ring

**Given** the Trial screen with keyboard focus on some control
**When** the player presses Tab
**Then** focus moves to the next focusable control and the keyboard focus ring (`ody-focus-visible`) appears,
exactly as arrow-key navigation already does.

### Scenario 2 — mixed input does not get the ring stuck

**Given** the ring is showing after a Tab press
**When** the player clicks with the mouse, then presses Tab again
**Then** the ring hides on the click and reappears on the next Tab -- already covered by the existing EditMode
test at the `OdyFocusVisible` level; add the equivalent through a real Tab key press once wired.

### Required invariants

- Arrow-key/WASD/gamepad `Navigate` behavior is unchanged.

## 8. Deliverables

- Production code: the input-binding (and/or `OdyFocusVisible`) fix.
- Tests: new PlayMode test driving a real `Keyboard` Tab press.
- Documentation: this task file; a short note added to `ODY-S11-212`'s own task file acknowledging the defect
  and pointing to this fix once merged.

## 9. Acceptance criteria

1. A new PlayMode test presses a real Tab key (via `InputTestFixture`, not a direct method call) and asserts
   the focus ring appears; it fails against the current `claude/pensive-gates-n18srp` code before the fix and
   passes after.
2. The existing arrow-key/gamepad `Navigate` behavior remains exactly as before (existing EditMode test for
   `OdyFocusVisible` still green, plus the new PlayMode test's own control check if added).
3. `test-unity.ps1` (EditMode + PlayMode), `test-fast.ps1`, and all `verify-*.ps1` remain green.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-FOCUSRING-TAB-001` | Unity PlayMode | real Tab / Shift+Tab presses move focus forward and back and show the ring; a real mouse press hides it; arrows (`UI/Navigate`) still show it | Pass (owner run pending) |

### Required commands

```powershell
scripts/test-unity.ps1
scripts/test-fast.ps1
scripts/verify-format.ps1
scripts/verify-repository.ps1
scripts/verify-test-structure.ps1
```

### Manual validation

- None required beyond the new automated PlayMode test, which already exercises the real input pipeline.

## 11. Compatibility, migration, and rollback

Not applicable -- input-binding/client-only change, no persisted state or contract affected.

## 12. Dependencies and licensing

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

Not applicable.

## 14. Planning and execution mode

- Planning mode: Brief plan
- Reason for selected mode: Narrow, well-diagnosed input-configuration fix with a clear repro and a clear
  control case proving the rest of the mechanism already works.
- **Decided (2026-10-01, see §18): neither option as written.** Binding Tab into `Navigate` conflicts with how the
  module processes `Navigate`. A dedicated binding in the asset would need a string-based `FindAction` lookup. Tab is
  instead a code-created action owned by the composition root. Original question, kept for the record: bind Tab
  directly into the existing `Navigate` action
  (simplest; Tab and arrows become interchangeable for focus movement, which most desktop users expect
  anyway), or add a separate dedicated action/binding so Tab can be distinguished from directional navigation
  if some future UI wants to treat them differently. Record whichever is chosen here before implementing.
- Expected pull request count: 1

## 15. Documentation and versioning impact

- Documents that must change: `docs/tasks/active/ODY-S11-212_Unified_Tab_Convention.md` (acknowledge + link
  this fix once done).
- Application version change: No.

## 16. Definition of Done

- [ ] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [ ] Required automated tests pass.
- [ ] Required commands and their real results are recorded.
- [ ] Architecture and dependency rules remain valid.
- [ ] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [ ] Documentation is updated only where materially required.
- [ ] Self-review performed against this task and `AGENTS.md`.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

- Changed: `UiTabNavigation.cs` (new), `AppShellEntryPoint.cs`, `OdysseyUiKit.cs` (`OdyFocusVisible`),
  `OdysseyPlayModeFoundationSmokeTests.cs` (new test), `Tests/Metadata/test-catalog.json`, this file,
  `ODY-S11-212` (note).
- `git diff origin/main -- Packages DotNet`: empty.
- **Run in the container:** the 72 SLICE-11 EditMode tests in the scratch build (Unity stand-ins, outside the
  repository) passed after removing the raw-key listener.
- **Not run (no Unity or PowerShell in the container):** `test-unity.ps1` (including the new PlayMode test),
  `test-fast.ps1`, `verify-format.ps1`, `verify-repository.ps1`, `verify-test-structure.ps1`. `UiTabNavigation`
  depends on the Input System package and was compiled only by the owner's Unity.
- **Requires the owner's real-Unity confirmation:**
  1. Tab moves focus forward through the top-bar toggles and on into the character sheet tabs and catalog form
     fields; Shift+Tab moves it back.
  2. The blue ring shows during Tab navigation and hides after a mouse click.
  3. Arrows still navigate.

## 18. Blockers, decisions, and change control

### Blockers

- None. The binding question in §14 was resolved by the technical finding below.

### Decisions made during execution

- 2026-10-01 — Found and diagnosed during independent real-Unity verification of P0 CoreFeel
  (`ODY-S11-210..214`); written up as a separate task per that verification's own instruction not to fix
  defects silently inside a verification pass.

- 2026-10-01: **Recommended fix (Tab as an extra `Navigate` binding) rejected after reading the real package
  source.** Source: Input System `1.19.0` at the exact pinned version (`com.unity.inputsystem`, GitHub tag
  `1.19.0`).
  - `InputSystemUIInputModule.ProcessNavigation` reads `Navigate` as a Vector2 and maps it only to
    `MoveDirection` Left/Right/Up/Down. There is no Next/Previous.
  - Tab bound into `Navigate`, for example as a composite part, would therefore move focus *spatially*, like an
    arrow key, instead of in tab order.
  - Shift+Tab could not mean "previous" at all: a Shift modifier composite would only pick another arrow direction.
- **What Unity itself does.** Unity's own InputForUI path (`InputSystemProvider`, used when no EventSystem drives UI
  Toolkit) handles Tab with a code-created `<Keyboard>/tab` button action outside the actions asset
  (`RegisterFixedActions`). Shift decides between `NavigationEvent.Direction.Next` and `Previous`. Our app uses an
  EventSystem + `InputSystemUIInputModule` (`AppShellEntryPoint.EnsureRuntimeUiInput`), so that provider is not in
  play, and Tab is lost.
- **Chosen fix: the same technique, on our side.** `UiTabNavigation` (new, `Assets/Odyssey/Client/Runtime/UiTabNavigation.cs`):
  - It owns a code-created `<Keyboard>/tab` button action.
  - On press it sends `NavigationMoveEvent` Next (or Previous with Shift held) to the focused element (to the
    document root when nothing is focused). UI Toolkit's focus ring turns that into a tab-order focus change.
  - It is constructed and disposed by `AppShellEntryPoint`, the composition root, through explicit ownership. There
    is no `FindAction`/string lookup and no TC-ARCH-001 pattern.
- **Focus ring.** `OdyFocusVisible` needed no logic change: it already reacts to `NavigationMoveEvent`, which arrows
  and Tab now both produce. Its raw `KeyDownEvent` Tab listener was dead code against this input configuration and is
  removed, with a comment saying why.
- **Unchanged.** `Odyssey.inputactions` is **not changed**. Arrow, WASD and gamepad `Navigate` behave exactly as
  before.
- **Known limits (documented, not hidden):**
  - **One move per Tab press.** Holding Tab does not auto-repeat; the arrows keep the module's own repeat.
  - **First press with nothing focused.** That press only focuses the first element. Its event targets the document
    root, outside the screen that `OdyFocusVisible` watches, so the ring appears from the second press. In normal use
    something is focused after any click.
  - **Tab binding is fixed in code.** It is not user-rebindable, like Unity's own provider.
  - **Double handling.** If a future Unity or Input System version starts forwarding Tab to UI Toolkit by itself
    while an EventSystem is present, Tab would move focus twice. `TC-FOCUSRING-TAB-001` would catch that, because
    Shift+Tab must return to the start element.
- **Observed, out of scope.** `Navigate` is also bound to WASD, which may interfere with typing in text fields. This
  is pre-existing and not changed here.

### Approved task changes

- Paths beyond §5's allowed list, needed by the decision above: `Assets/Odyssey/Client/Runtime/UiTabNavigation.cs`
  (new) and `Assets/Odyssey/Client/Runtime/AppShellEntryPoint.cs` (ownership of the new object).
  `Assets/Odyssey/Client/Input/Odyssey.inputactions` stays untouched.
