# ODY-S11-217 — Status badges on panel toggles (UI polish P1, item 7)

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

The Combat toggle in the top bar shows a counter badge when items wait for the MainGM. That means unresolved
`Pending` attacks or stacking-conflict candidates. The badge is visible even while the Combat drawer is closed, and
it disappears when the queue is empty.

## 2. Verified current state

- `CombatPanelPresenter.PendingAttacks` and `ConflictCandidates` (`ODY-S11-205`) were visible only inside the open
  Combat drawer.
- Both are session-tracked: the backend has no listing of pending attacks or conflicts (recorded in `ODY-S11-205`).
  They are recomputed on every `Refresh()`, and every combat action calls `Refresh()`.
- In the trial, attacks are made only from the Combat drawer, so the count changes only through that panel's own
  actions, or through a role switch, which also refreshes the panel.

## 3. Source and what was transferred

- **Source:** the Owlbear Rodeo Action API, where any panel icon can carry badge text and colour.
- **Transferred:** the technique only. Odyssey shows a counter on the drawer's toggle.
- **Executor's choice:** 9+ cap, accent colour, top-right placement.

## 4. Scope

### In scope
- `GameShellPresenter`:
  - `SetDrawerBadge(id, count)` and `DrawerBadgeText(id)`;
  - each drawer toggle gets a hidden badge `Label` (`toggle-<id>-badge`, class `.ody-tab__badge`);
  - the toggle gets an "N waiting" tooltip.
- `CombatPanelPresenter`:
  - `AttentionCount` is pending attacks + conflict candidates for the MainGM, and 0 for everyone else, because only
    the MainGM decides them;
  - `AttentionCountChanged` is raised on change after each refresh.
- `TrialScreenPresenter` wires the event to `Shell.SetDrawerBadge(CombatDrawerId, n)` and unsubscribes on dispose.
- USS `.ody-tab__badge`; tests; test catalog.

### Out of scope, possible follow-up
- Badges on other panels. Example: characters submitted for review waiting for the MainGM. That needs its own rule
  for "unread" and was not invented here. The shell API already supports it.
- Backend listings for pending attacks or conflicts.
- Backend: untouched.

## 5. Tests and validation

| ID | Test |
|---|---|
| `TC-BADGE-001` | `GameShellPresenterTests.DrawerBadge_ShowsACountOnTheClosedToggle_AndHidesAtZero` |
| `TC-BADGE-002` | `CombatPanelPresenterTests.AttentionCount_CountsItemsWaitingForTheMainGm_AndEmptiesWhenTheyAreDecided` |

`TC-BADGE-002` uses a test double around the real `SqliteAttackApplyRepository` that records every attack with
"intervention required". This produces a genuine durable `Pending` outcome without setting up effect rules. The
MainGM's real `ResolveIntervention(Reject)` then empties the queue.

**Batchmode-verifiable:** both tests.

**Requires human visual confirmation in Play mode.** A real `Pending` attack needs an on-hit effect whose stacking
rule requires intervention, which the trial demo data may not contain. So:
1. Badge placement and readability (red pill on the Combat toggle's corner, white number) can be checked with any
   such attack.
2. Otherwise the check is **blocked by the demo data, not by the code**, and stays covered by `TC-BADGE-002`.

**Compilation and test status in this container** (no Unity or PowerShell):
- **Not compiled against real UnityEngine.**
- The changed shell, combat panel, trial composition and both tests were compiled against stand-ins, and the tests
  ran: **77/77 passed** (SLICE-11 + P0 + P1 EditMode tests in the stand-in run).
- `git diff origin/main -- Packages DotNet` is empty.

**Not run:** `test-unity.ps1`, `test-fast.ps1`, `verify-*.ps1`. Owner run pending.

## 6. Security, privacy, compatibility

- The badge shows a count only, never what or whose. Players get 0, so it reveals nothing to them.
- No data or contract change. Rollback = revert the commit.
