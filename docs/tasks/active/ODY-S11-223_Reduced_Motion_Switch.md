# ODY-S11-223 — "Reduce motion" switch (UI polish P2, item 12)

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

A "reduce motion" flag that switches off every decorative animation. Functional transitions still happen, just
without animation.

## 2. Verified current state

- There is **no settings screen and no settings store** in the client.
- P1 already prepared the hook: `OdyMotion.IsReducedMotion(element)` checks for `ody-reduced-motion` on any ancestor,
  and marching ants (`ODY-S11-216`) and the skeleton shimmer (`ODY-S11-220`) honour it.
- The other animations (remote token easing and HP bar easing from `ODY-S11-211`, camera autofocus pan from
  `ODY-S11-214`) did not honour it yet.

## 3. Sources and what was transferred

- **Sources:** Owlbear honours `prefers-reduced-motion` site-wide; the owner's `Odyssey_System` prototype does so for
  its skeleton shimmer.
- **Transferred:** the technique only.

## 4. Scope

### In scope
- `GameShellPresenter`:
  - a "Reduce motion" tab-convention toggle in the top bar (`game-reduce-motion`), placed there because no settings
    screen exists;
  - `ReducedMotion`, `SetReducedMotion(bool)` and `ReducedMotionChanged`;
  - the toggle puts `ody-reduced-motion` on the game screen.
- Behaviour under the flag:
  - **marching ants:** the dashes stand still and the outline stays (already in P1);
  - **skeleton shimmer:** no toggling and no USS transition (already in P1);
  - **token moves made elsewhere:** drawn at once (`BoardTokenMotion.Enabled = false`, set by the board each render).
    A move already easing finishes on the next tick;
  - **resource bars changed elsewhere:** the new value is shown at once. A bar that started easing before it was
    attached finishes on its first tick;
  - **camera autofocus at turn change:** the camera jumps straight to the token instead of panning;
  - **button and tab hover/press colour transitions:** 0 s.
- **Not affected, by design:** drawers, popovers and dialogs still open and close. They were never animated.
  Selection, focus rings and all data updates are unchanged.
- Tests; test catalog.

### Out of scope
- **Persisting the choice.** It is session-only, because no client settings store exists. A follow-up would be a
  settings store.
- **Reading the operating system's "reduce animations" preference.** Unity offers no API for it on Windows. A
  follow-up would be platform interop, which is not added here.
- Backend: untouched.

## 5. Tests and validation

| ID | Test |
|---|---|
| `TC-REDUCEDMOTION-001` | `OdyReducedMotionTests.ShellSwitch_PutsTheReducedMotionClassOnTheScreen_AndReportsChanges` |
| `TC-REDUCEDMOTION-002` | `OdyReducedMotionTests.TokenMotion_Disabled_DrawsChangesMadeElsewhereAtOnce` |
| `TC-REDUCEDMOTION-003` | `OdyReducedMotionTests.ResourceBar_UnderReducedMotion_ShowsTheNewValueAtOnce` |
| `TC-REDUCEDMOTION-004` | `OdyReducedMotionTests.MarchingAntsAndSkeleton_StandStill_UnderTheShellSwitch` |
| `TC-REDUCEDMOTION-005` | `BoardScreenPresenterTests.UnderReducedMotion_MovesMadeElsewhereAreInstant_AndTheCameraJumpsToTheActingToken` |

DoD "at least 2 kinds of P0/P1 animation react": **5 react**. They are marching ants, skeleton shimmer, token
easing, HP bar easing and the camera autofocus pan.

**Requires human visual confirmation in Play mode:**
1. Toggle "Reduce motion" in the top bar. A selected token's dashes stop.
2. "Next turn" makes the camera jump instead of glide.
3. After combat damage, reopening the Character drawer shows the HP bar's new value at once.
4. Toggle it off and all of the above animate again.
5. Drawers still open and close.

**Compilation and test status in this container** (no Unity or PowerShell):
- **Not compiled against real UnityEngine.**
- Stand-in compile and run: **86/86 passed**, including `TC-REDUCEDMOTION-001…004`.
- `BoardScreenPresenter.cs`, `BoardTokenMotion.cs` and `TC-REDUCEDMOTION-005`: a stand-in compile reported no error
  on changed lines. That test was **not run**.
- `git diff origin/main -- Packages DotNet` touches only the `ODY-S11-222` security fix files. Nothing in this task
  changes the backend.

**Not run:** `test-unity.ps1`, `test-fast.ps1`, `verify-*.ps1`. Owner run pending.

## 6. Security, privacy, compatibility

No data or contract change. Rollback = revert the commit.
