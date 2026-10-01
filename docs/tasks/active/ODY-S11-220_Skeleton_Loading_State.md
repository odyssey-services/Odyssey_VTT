# ODY-S11-220 — Skeleton loading state (UI polish P1, item 10)

**Status:** In Review. The primitive is delivered; the visual DoD is **blocked by the current architecture** (see §2).  
**Roadmap stage / slice:** SLICE-11 polish P1 (items 6–11: `ODY-S11-216`…`ODY-S11-221`)  
**Owner:** Claude Code  
**Requested by:** Product owner  
**Branch:** `claude/pensive-gates-n18srp`  
**Pull request:** Not opened  
**ExecPlan:** this contract (client UI only)  
**Created:** 2026-10-01  
**Last updated:** 2026-10-01

## 1. Goal (as requested)

Lists and fields that wait for a server answer longer than one frame show a skeleton placeholder instead of an empty
screen. Examples: character creation, opening a sheet by id, the inventory list. The placeholder is grey bars, with
an optional shimmer that must be ready to switch off with the later reduced-motion setting. DoD: at least one screen
(the character sheet opened by id) shows the skeleton while it waits.

## 2. Verified current state: nothing in the client ever waits

- **Every client data read is synchronous on the main thread.** That covers SQLite repositories called directly by
  the presenters, and file import/export.
- A repository-wide search of `Assets/Odyssey/Client/Runtime` finds no `async`, `await`, `Task<…>`, `Task.Run`,
  threads or loading coroutines. The only coroutine is the Player smoke mode.
- The client has no network transport.
- UI Toolkit paints between frames. During a synchronous call nothing is painted, and when the call returns the data
  is already there.
- **Conclusion.** A skeleton shown "while waiting" can never actually appear on screen today: for "character sheet
  opened by id", `CharacterPanelPresenter.Open` reads and renders in the same call. Making it visible would require
  an artificial delay, which means inventing latency. Per the pass's rule against inventing missing mechanics, that
  was **not done**.

## 3. What was delivered (ready for the first asynchronous load)

- `Runtime/Ui/OdySkeleton.cs` (new):
  - placeholder bars, with the last one at 60% width, ignoring picking;
  - an optional shimmer: a class toggled every 650 ms, giving the 1.3 s cycle named in the request for the
    prototype's `.cp-skel`, with a USS colour transition on the bars, because USS has no keyframe animation;
  - `Advance(ms)` stepped by the UI Toolkit scheduler.
- **Reduced motion.** Under an ancestor with `ody-reduced-motion` (`OdyMotion.IsReducedMotion`, introduced with
  `ODY-S11-216`), the shimmer does not toggle and the USS transition is 0 s. The P2 setting only has to set that class.
- USS section 18 and tests.
- **Wiring, for the future task that introduces asynchronous or network loading:**
  1. Show `new OdySkeleton(...).Element` in the target container, then call `StartShimmer()`.
  2. When the data arrives, `StopShimmer()` and replace the skeleton with the content.

## 4. Scope

- **Out of scope:** an artificial delay, asynchronous repositories, networking. Backend: untouched.

## 5. Tests and validation

| ID | Test (`OdySkeletonTests`) |
|---|---|
| `TC-SKEL-001` | `Skeleton_HasGreyBars_TheLastOneShorter_AndIgnoresThePointer` |
| `TC-SKEL-002` | `Shimmer_TogglesEveryHalfPeriod_StopsUnderReducedMotion_AndOnStop` |

**Batchmode-verifiable:** structure and shimmer stepping, including the reduced-motion freeze.

**Human visual confirmation:** **not possible yet.** No screen waits for data (§2). The DoD item "at least one
screen shows the skeleton while waiting" stays open until asynchronous loading exists. The product owner decides
whether to accept the primitive now or to drop it until then.

**Compilation and test status in this container** (no Unity or PowerShell):
- **Not compiled against real UnityEngine.**
- Stand-in compile and run only: **82/82 passed** in that run.
- USS transitions are not exercised there.
- `git diff origin/main -- Packages DotNet` is empty.

**Not run:** `test-unity.ps1`, `test-fast.ps1`, `verify-*.ps1`. Owner run pending.

## 6. Security, privacy, compatibility

No data or contract change. Rollback = revert the commit.
