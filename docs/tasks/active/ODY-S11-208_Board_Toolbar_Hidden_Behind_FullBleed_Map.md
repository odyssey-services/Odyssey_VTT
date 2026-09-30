# ODY-S11-208 — Board toolbar hidden/unclickable behind the full-bleed map (Owlbear layout, phase 1)

**Status:** In Review
**Roadmap stage / slice:** SLICE-11 (phase 1, Owlbear board overlay layout)
**Owner:** Claude Code
**Requested by:** Product owner (found during `ODY-S11-207` diagnostics on `claude/pensive-gates-n18srp`)
**Branch:** `claude/pensive-gates-n18srp`
**Pull request:** Not opened
**ExecPlan:** Not required
**Created:** 2026-10-01
**Last updated:** 2026-10-01

## 1. Goal

Make the board's title/toolbar/status chrome (`board-title`, `board-toolbar` with its Select/Draw Wall/Draw
Door/Draw Window buttons, `board-status`) actually receive real mouse clicks in the Owlbear full-bleed board
layout, instead of the clicks landing on the map underneath them.

## 2. Why this task exists

- Problem: found by point diagnostics on `TC-BOARD-121` during `ODY-S11-207` (PlayMode timing investigation).
  Instrumenting `board-status` before/after a drag at the `board-tool-drawwall` button's own screen
  coordinates showed the status text change to a box-select message ("N token(s) in the box"), i.e. the
  click was received by the map, not the button. `TC-BOARD-121` and three other PlayMode smoke tests were
  failing (deterministically, twice reproduced) on a `WaitUntil` timeout that `ODY-S11-207` had first assumed
  was a bootstrap-cost/timing-budget problem; that assumption is now known to be wrong (see `ODY-S11-207`'s own
  record) — raising the timeout to 60s did not make the wait resolve, because the underlying click was never
  reaching its target element at all.
- Value: without this fix, the MainGM cannot use the drawing toolbar (or read the status pill) at all in the
  new Owlbear layout — this is a functional regression in already-implemented SLICE-11 phase-1 work, not a
  cosmetic issue.
- Relationship to `ODY-S11-207`: `ODY-S11-207`'s task explicitly excluded touching SLICE-11 functionality, so
  this fix was written up and executed as its own task, per that ticket's own instruction.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`, ADR-001 (module boundaries), ADR-005 (composition/lifetimes)
- `docs/tasks/active/ODY-S11-201_Owlbear_Board_Overlay_Layout.md` (original phase-1 contract; its §10 already
  documents the `TC-BOARD-121` coordinate adjustment for the full-bleed board, but not this click-routing bug)

### Requirement and test IDs

- Requirement IDs: None
- Existing test IDs: `TC-BOARD-121` (`RealMouseClick_DrawWallTool_RealPointerDownMoveUp_CreatesAnObstacle`)
- New test IDs to introduce: None (existing `TC-BOARD-121` is the regression test; see §10)

### Task-safe private context

- Approved summary: None

## 4. Verified current state

### Verified facts

- `BoardScreenPresenter.BuildView` ([BoardScreenPresenter.cs](../../../Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs))
  added `board-title`, the `board-toolbar` view, and `board-status` to `appRoot` **before** `_boardArea`.
  When `FullBleed` is true, `_boardArea` gets inline `position: Absolute; left/top/right/bottom: 0`, covering
  the entire `appRoot`. In UI Toolkit, a later sibling paints and receives pointer-hit-testing priority over
  an earlier sibling at the same screen position — so the full-bleed board, added last, silently intercepted
  every click meant for the toolbar/status, regardless of their own CSS `position` values.
- `Assets/Odyssey/Client/UI/OdysseyDesignSystem.uss` section 13 already contains CSS rules
  (`.ody-board-layer #board-toolbar { position: absolute; ... }` etc.) that look like an intended fix for
  exactly this, but (a) CSS `position` does not change UI Toolkit's sibling-order-based hit-test priority, so
  even a loaded stylesheet would not have fixed the click routing, and (b) confirmed by a project-wide search
  that `OdysseyDesignSystem.uss` is never attached to any `UIDocument`/`PanelSettings` on this branch (no
  `styleSheets.Add`/UXML `<Style>` reference exists anywhere in `Assets/Odyssey/Client/Runtime`) — so none of
  its ~1200 lines of `.ody-*` rules are in effect in the running app at all. This second finding is
  significantly larger in scope than this task and is reported separately (see §18); this task's fix does
  not depend on it and works with or without the stylesheet ever being attached.
- `TrialScreenPresenter` calls `board.InitializeInto(shell.BoardLayer)` — the board's chrome and board-area
  are both mounted inside `GameShellPresenter.BoardLayer`, which is a sibling of `GameShellPresenter.OverlayLayer`
  (topbar, drawers, dock already live there and are already known-clickable).
- Reproduced the fix's effect before writing it up: with `board.OverlayHost` unset, `TC-BOARD-121` hangs to
  the `WaitUntil` budget (60s when temporarily raised for diagnosis) waiting for an obstacle element that is
  never created, because the drawing tool is never actually selected (the click lands on the map).

### Assumptions

- None.

## 5. Scope

### In scope

- `Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs`: composition of `board-title`/`board-toolbar`/`board-status`
  (which parent they mount on, their positioning) when `FullBleed` is active.
- `Assets/Odyssey/Client/Runtime/TrialScreenPresenter.cs`: one property assignment wiring
  `shell.OverlayLayer` into the board presenter.

### Out of scope

- Drawing-tool gesture logic (`BoardObstacleDrawGesture`, pointer-down/move/up handlers), board camera/pan,
  token gestures — untouched.
- The separately-found "`OdysseyDesignSystem.uss` is never attached" issue — reported, not fixed here (see §18).
- Backend (`Packages/com.odyssey.*`, `DotNet/**`) — untouched.
- The dev-shell / fixed-size (non-`FullBleed`) board path and its existing tests — unaffected (see §7).

### Allowed paths

```text
Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs
Assets/Odyssey/Client/Runtime/TrialScreenPresenter.cs
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: Not applicable (single Unity client module, presentation layer only).
- Unity / thread / lifetime rule: `BoardScreenPresenter.OverlayHost` is read once in `BuildView`, at
  `InitializeInto` time, matching the existing `FullBleed` property's own lifecycle contract (must be set
  before `InitializeInto`/`Initialize`).
- Other: fix must not depend on `OdysseyDesignSystem.uss` being attached (it currently is not); positioning
  for the floated toolbar/status uses inline `VisualElement.style`, mirroring how `_boardArea`'s own
  `FullBleed` positioning is already done inline rather than through CSS.

## 7. Expected behavior

### Scenario 1 — Owlbear layout, MainGM draws a wall

**Given** the Owlbear full-bleed board (`TrialScreenPresenter`'s composition, `FullBleed = true`)
**When** the MainGM clicks the `board-tool-drawwall` button and then drags on the open map
**Then** the click activates the Draw Wall tool (not a map box-select), and the drag creates a wall obstacle.

### Scenario 2 — dev shell / fixed-size board unaffected

**Given** `BoardScreenPresenter` used without `OverlayHost` set (its default, e.g. the pre-existing
fixed-440×440 embedding and every existing EditMode test)
**When** `InitializeInto` builds the view
**Then** `board-title`/`board-toolbar`/`board-status` are added to `appRoot` exactly as before, byte-for-byte
unchanged.

### Required invariants

- `OverlayHost == null` never changes existing (pre-`ODY-S11-208`) behavior.

## 8. Deliverables

- Production code: `BoardScreenPresenter.OverlayHost` property + conditional chrome mounting;
  `TrialScreenPresenter` wiring `shell.OverlayLayer` into it.
- Tests: None new; existing `TC-BOARD-121` is the regression check.
- Scripts / CI: None.
- Documentation: this task file; note added to `ODY-S11-201`'s own record is not required (its §10 already
  correctly describes the coordinate change; this task's bug was independent of that change).
- Generated evidence or build artifacts: None retained (temporary diagnostic logs deleted after use).

## 9. Acceptance criteria

1. `TC-BOARD-121`, run in isolation, passes (clicking `board-tool-drawwall` then dragging creates a wall obstacle).
2. All 7 PlayMode smoke tests pass together in one `test-unity.ps1` run, using the **original** 10-second
   `WaitUntil` budget (not raised) — confirming this was the actual root cause of all 4 tests `ODY-S11-207`
   found failing, not a separate cross-test state leak.
3. All 201 EditMode tests remain green (fixed-size/dev-shell board path unaffected).
4. `test-fast.ps1` (`TC-ARCH-001` + `dotnet test`) and `verify-format.ps1`/`verify-repository.ps1`/
   `verify-test-structure.ps1` remain green.
5. `git diff --name-status origin/main -- Packages DotNet` remains empty.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-BOARD-121` | Unity PlayMode | Real mouse click on the draw-wall toolbar button actually activates the tool, not the map underneath it | Pass |

### Required commands

```powershell
scripts/test-unity.ps1
scripts/test-fast.ps1
scripts/verify-format.ps1
scripts/verify-repository.ps1
scripts/verify-test-structure.ps1
```

### Manual validation

- None beyond the automated PlayMode test; a full interactive Play-mode walkthrough remains blocked by this
  environment's lack of an interactive Unity GUI (documented in earlier SLICE-11 verification tasks).

### Required environments / profiles

- OS / architecture: Windows, Unity 6000.4.0f1 (batchmode), .NET 10.

### Validation not required by this task

- Interactive/manual visual confirmation that the floated toolbar looks exactly like the intended design —
  the design system stylesheet that would give it its final visual polish is not attached on this branch
  (see §18); this task only restores real click routing and a reasonable inline floating position.

## 11. Compatibility, migration, and rollback

Not applicable — presentation-only composition change, no persisted state, contract, or protocol affected.

## 12. Dependencies and licensing

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

Not applicable.

## 14. Planning and execution mode

- Planning mode: Brief plan
- Reason for selected mode: Small, well-understood composition/z-order fix confirmed by direct diagnostics
  before implementation.
- Expected pull request count: 1 (bundled with the rest of `claude/pensive-gates-n18srp`, per owner's own
  branch/PR decision — see §18).

## 15. Documentation and versioning impact

- Documents that must change: None beyond this task file.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed (none required beyond the automated test).
- [x] Required commands and their real results are recorded (see §17).
- [x] Architecture and dependency rules remain valid (`TC-ARCH-001` PASS).
- [x] Security, privacy, redaction, and audience rules are verified where applicable (not applicable).
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable (not applicable).
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Self-review performed against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work. (Not opened; owner decides.)
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs` — added `OverlayHost` property; when set, mounts
  `board-toolbar`/`board-status` on it with inline floating position instead of on `appRoot` before
  `_boardArea`; skips adding the redundant `board-title` label in that mode (the shell's own topbar already
  shows a scene title).
- `Assets/Odyssey/Client/Runtime/TrialScreenPresenter.cs` — sets `board.OverlayHost = shell.OverlayLayer`
  before `board.InitializeInto(shell.BoardLayer)`.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `TC-BOARD-121` in isolation | Passed | `Odyssey.Tests.Unity.PlayMode...RealMouseClick_DrawWallTool_RealPointerDownMoveUp_CreatesAnObstacle Passed 3.403375` |
| `scripts/test-unity.ps1` PlayMode (all 7, original 10s budget) | Passed | `root total=7 passed=7 failed=0` |
| `scripts/test-unity.ps1` EditMode (all 201) | Passed | `root total=201 passed=201 failed=0` |
| `scripts/test-fast.ps1` | Passed | `TC-ARCH-001 PASS`; `dotnet test`: Contracts 1/1, Domain 90/90, Networking 67/67, Unit 220/220, Architecture 10/10, Persistence 948/948 |
| `scripts/verify-format.ps1` | Passed | `FORMAT-001 PASS` |
| `scripts/verify-repository.ps1` | Passed | `REPOSITORY-VERIFY PASS` |
| `scripts/verify-test-structure.ps1` | Passed | exit 0, `TC-ARCH-001`/`TC-ARCH-002` all PASS |
| `git diff --name-status origin/main -- Packages DotNet` | Passed | empty |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | isolated `TC-BOARD-121` run above |
| AC-2 | Passed | full 7-test PlayMode run above, unmodified 10s budget |
| AC-3 | Passed | 201/201 EditMode |
| AC-4 | Passed | test-fast.ps1 + verify-*.ps1 results above |
| AC-5 | Passed | empty backend diff |

### Known limitations

- The design-system stylesheet (`OdysseyDesignSystem.uss`) is not attached to any `UIDocument` on this
  branch, so the floated toolbar/status use hand-written inline styles rather than the (currently inert)
  `.ody-*` classes already defined for them. Once the stylesheet is properly attached in a future task, its
  `.ody-board-layer #board-toolbar`/`#board-status` selectors will no longer match (the elements moved out of
  `.ody-board-layer` onto the overlay layer) and should be revisited or removed together with that fix.
- No automated coverage added specifically asserting sibling/z-order (beyond `TC-BOARD-121` exercising it
  end-to-end); considered sufficient given the existing test already fails deterministically on the bug and
  passes deterministically on the fix.

### Follow-up tasks

- Design-system stylesheet never attached to any `UIDocument`/`PanelSettings` — none of `OdysseyDesignSystem.uss`'s
  `.ody-*` rules currently apply anywhere in the running app (topbar, drawers, dock, board-layer overlay
  rules, etc. all fall back to default UI Toolkit layout). Needs its own task to wire it up (e.g. an explicit
  `[SerializeField] StyleSheet` on `AppShellEntryPoint`, by the same pattern `ODY-S11-101` used for
  `CatalogTheme.uss`) and then a full visual pass, since a lot of the intended Owlbear look has never actually
  been seen rendered. Not filed as a numbered task yet — flagged here for the product owner to prioritize and title.

### Self-review summary

- Scope review: touched exactly the two files needed for the composition fix; did not touch gesture/camera
  logic, backend, or the stylesheet-attachment problem.
- Architecture review: `TC-ARCH-001` passes; no new cross-module dependency introduced.
- Test review: existing `TC-BOARD-121` now genuinely exercises and passes the real gesture; not rewritten to
  dodge the bug.
- Documentation/version review: this task file is the only new documentation; no version fields changed.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-10-01 — Chose Option 1 (move chrome to `GameShellPresenter.OverlayLayer`) over Option 2 (reorder within
  `appRoot`/`SendToBack`) because `OverlayLayer` is already the established, working home for every other
  piece of floating chrome in this layout (topbar, drawers, dock), and moving the board's own chrome there is
  structurally guaranteed safe against the full-bleed board regardless of any future change to `BoardLayer`'s
  internal contents — a within-`appRoot` reorder would remain fragile to any future sibling added after the
  toolbar. — Authority / approval: ODY-S11-208 task text itself named this as the preferred option when an
  existing overlay layer is available.
- 2026-10-01 — Did not add the redundant `board-title` label to the overlay in `FullBleed` mode, matching the
  intent already expressed (but inert) in `OdysseyDesignSystem.uss`'s `.ody-board-layer #board-title { display: none; }`
  rule, since `GameShellPresenter`'s own topbar already shows a scene title. — Authority / approval: existing
  (inert) design-system CSS intent, applied directly since the stylesheet itself is not attached.
- 2026-10-01 — Did not fix or investigate further the separately-discovered "`OdysseyDesignSystem.uss` never
  attached" issue; recorded as a follow-up task instead, since fixing it would be a much larger, separately
  reviewable visual-pass task well beyond this ticket's z-order scope. — Authority / approval: ODY-S11-208's
  own scope boundary (composition/z-order only).

### Approved task changes

- None.
