# ODY-S11-229 — Select/Draw Wall/Draw Door/Draw Window: exclusive tool switch

**Status:** In Review
**Roadmap stage / slice:** SLICE-11 (found during the product owner's first live visual pass of the UI)
**Owner:** Claude Code
**Requested by:** Product owner
**Branch:** `claude/pensive-gates-n18srp`
**Pull request:** Not opened
**ExecPlan:** Not required
**Created:** 2026-10-01
**Last updated:** 2026-10-01

## 1. Goal

Exactly one of the four board-toolbar buttons (Select/Draw Wall/Draw Door/Draw Window) is active at any time;
clicking a different one switches cleanly, including interrupting an in-progress draw gesture rather than
refusing the switch or leaving a stuck state.

## 2. Why this task exists, and what was actually found

The product owner observed the four buttons "do not behave like one exclusive tool." Investigated before
changing anything:

- **The single exclusive `BoardTool` state already existed** (`BoardTool` enum, one `_currentTool` field, one
  `SetTool` entry point, `BoardToolbarPresenter.SetActiveTool` highlighting exactly one button) — this was
  built in SLICE-10 Block 6 part 1, well before this polish pass. Confirmed live with real clicks (see §17):
  after each of the 4 buttons is clicked in turn, exactly one shows the active border, and it is always the one
  just clicked.
- **The one real gap:** `SetTool` *refused* to switch (returned `false`, left everything as-is) while a wall/
  door/window was half-drawn — a deliberate choice from that original task's own contract, never exercised by
  any automated test. This task's own DoD explicitly asks for the opposite: switching tools mid-draw should
  cancel the half-drawn shape and switch, "not leave a hung-up mode." That is a genuine, disclosed reversal of
  an earlier decision (see §18), not a bug fix for broken exclusivity.
- **Most likely source of the original "doesn't feel unified" observation:** `ODY-S11-208`, found and fixed
  earlier in this same verification track — before that fix, the full-bleed board silently intercepted every
  click meant for this exact toolbar, so none of the four buttons did anything at all when clicked for real.
  That is consistent with "doesn't behave like a working exclusive tool" without there being a *second*,
  independent exclusivity bug underneath.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `docs/tasks/active/ODY-S11-208_Board_Toolbar_Hidden_Behind_FullBleed_Map.md` (the real-click fix this task's
  live verification depends on)

### Requirement and test IDs

- Existing test IDs: `TC-BOARD-113`..`117` (draw/select tests, `BoardScreenPresenterObstacleTests.cs`)
- New test IDs: `TC-BOARD-121e` (`SwitchingToolMidDraw_CancelsTheHalfDrawnShape_AndSwitches_InsteadOfRefusing`)

### Task-safe private context

- Approved summary: None

## 4. Verified current state

### Verified facts

- `BoardScreenPresenter._currentTool`/`SetTool`/`BoardToolbarPresenter.SetActiveTool` already form one
  exclusive-state design — confirmed by reading the code and by a live PlayMode run clicking all 4 real
  buttons in sequence (see §17 for the exact log).
- `SetTool`'s old guard `if (_obstacleDrawGesture.IsActive) return false;` had no automated test anywhere in
  the repository (`grep` for `SetTool.*Is.False`/`mid-gesture`/`MidDraw` found nothing) — it was an untested
  code path.

### Assumptions

- None.

## 5. Scope

### In scope

- `BoardScreenPresenter.SetTool`: switching now always succeeds, cancelling an in-progress draw gesture first
  (reusing the existing `CancelObstacleDraw`, the same path Escape on the board area already uses) instead of
  refusing.
- One new EditMode test for the new behavior; live PlayMode confirmation of the already-correct exclusivity
  (temporary, not committed — see §17).

### Out of scope

- Any other board-tool behavior (drawing math, obstacle creation, color/visual treatment beyond the existing
  active-border highlight).
- Backend — untouched.

### Allowed paths

```text
Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs
Assets/Odyssey/Client/Tests/EditMode/BoardScreenPresenterObstacleTests.cs
```

## 6. Technical constraints

- Must not change how a *committed* obstacle gets created, only what happens to an *in-progress, uncommitted*
  gesture when the tool changes.
- `SetTool` keeps returning `bool` (always `true` now) for source compatibility with existing callers.

## 7. Expected behavior

### Scenario 1 — exclusive highlight

**Given** the board toolbar
**When** each of Select/Draw Wall/Draw Door/Draw Window is clicked in turn
**Then** exactly one button shows the active state at any time, always the one just clicked.

### Scenario 2 — switching cancels an in-progress draw

**Given** Draw Wall is active and a wall is half-drawn (pointer down, moved, not yet released)
**When** Select is clicked
**Then** the switch succeeds, the half-drawn preview disappears, and a later pointer-up at the old drag
position creates no obstacle.

### Required invariants

- A fully *committed* draw (pointer down, move, up, all before any tool switch) is unaffected.

## 8. Deliverables

- Production code: the `SetTool` change.
- Tests: `TC-BOARD-121e`.
- Documentation: this task file.

## 9. Acceptance criteria

1. `TC-BOARD-121e` passes: switching tools mid-draw cancels the gesture and the switch itself succeeds.
2. All pre-existing obstacle-drawing tests (`TC-BOARD-113`..`117`) remain green, unmodified in their
   assertions.
3. Live PlayMode confirms real clicks on all 4 toolbar buttons each show exactly one active at a time.
4. `test-unity.ps1`, `test-fast.ps1`, `verify-*.ps1` all green.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-BOARD-121e` | Unity EditMode | Switching tools mid-draw cancels the half-drawn shape and switches | Pass |

### Required commands

```powershell
scripts/test-unity.ps1
scripts/test-fast.ps1
scripts/verify-format.ps1
scripts/verify-repository.ps1
scripts/verify-test-structure.ps1
```

### Manual validation

- Performed via a temporary, non-committed PlayMode diagnostic: clicked each of the 4 real toolbar buttons in
  turn and read back each button's active-border style after each click (see §17 for the full log). Removed
  before this commit per this track's convention.

## 11. Compatibility, migration, and rollback

Not applicable — presentation/gesture-only. Rollback = revert the commit.

## 12. Dependencies and licensing

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

Not applicable.

## 14. Planning and execution mode

- Planning mode: Brief plan
- Expected pull request count: 1 (bundled with the rest of `claude/pensive-gates-n18srp`)

## 15. Documentation and versioning impact

- Documents that must change: None beyond this task file.
- Application version change: No.

## 16. Definition of Done

- [x] Goal achieved.
- [x] All acceptance criteria satisfied.
- [x] Required automated tests pass (250/250 EditMode, 10/10 PlayMode).
- [x] Required commands and their real results recorded (see §17).
- [x] Architecture rules remain valid (`TC-ARCH-001` PASS).
- [x] Self-review performed against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work. (Not opened; owner decides.)
- [ ] Product owner or authorized reviewer completes the required review.

## 17. Completion evidence

### Changed files / areas

- `BoardScreenPresenter.cs`: `SetTool` now cancels an in-progress draw gesture instead of refusing the switch.
- `BoardScreenPresenterObstacleTests.cs`: new `TC-BOARD-121e`.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `scripts/test-unity.ps1` | Passed | EditMode 250/250, PlayMode 10/10 |
| `scripts/test-fast.ps1` | Passed | `TC-ARCH-001 PASS`; `dotnet test` 952/952 |
| `scripts/verify-format.ps1` / `verify-repository.ps1` / `verify-test-structure.ps1` | Passed | all PASS |
| Live PlayMode diagnostic (real clicks on all 4 toolbar buttons, temporary, removed) | Passed | Each click left exactly one button active, always the one clicked: `select→select active=True, others False`; `drawwall→drawwall active=True, others False`; `drawdoor→drawdoor active=True, others False`; `drawwindow→drawwindow active=True, others False` |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `TC-BOARD-121e` |
| AC-2 | Passed | full EditMode suite green, `TC-BOARD-113..117` assertions unchanged |
| AC-3 | Passed | live diagnostic log above |
| AC-4 | Passed | all scripts green above |

### Known limitations

- No human has watched the active-border highlight with their own eyes (color/contrast, not just "is it the
  2px border or not") — this environment has no interactive GUI. The logical/structural correctness (exactly
  one active, always the clicked one) is confirmed live; its visual appearance is not.

### Follow-up tasks

- None identified beyond the standing "a human should eventually look at this UI" item already tracked
  elsewhere in this verification pass.

### Self-review summary

- Scope review: touched only `SetTool`'s mid-gesture behavior; did not touch drawing math, obstacle creation,
  or the toolbar's visual styling.
- Architecture review: `TC-ARCH-001` passes; no new dependency.
- Test review: new test added with a real, specific assertion (preview removed, no phantom obstacle on a later
  pointer-up); existing tests unmodified.
- Documentation/version review: this task file is the only new documentation.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-10-01 — Changed `SetTool` to cancel an in-progress draw gesture on switch instead of refusing the
  switch, explicitly reversing SLICE-10 Block 6 part 1's original decision. That original choice was never
  covered by an automated test and predates this toolbar's real clicks being confirmed to even reach it
  (`ODY-S11-208`); the product owner's current, explicit instruction in this task's own DoD is the one now in
  effect. Recorded here rather than silently overwritten, per this track's standing rule for reversing an
  earlier documented decision.

### Approved task changes

- None.
