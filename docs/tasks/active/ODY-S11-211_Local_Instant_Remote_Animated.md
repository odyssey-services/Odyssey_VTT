# ODY-S11-211 — Local action instant, remote action animated (UI polish P0, item 2)

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

- The local user's own token drag stays 1:1 with no delay.
- A token move that this board did not make itself eases from where the token was drawn to its new position. Such a
  move comes from another participant or arrives with a reload.
- An HP/resource bar changed elsewhere eases its fill from the previously shown value to the new one.

## 2. Why this task exists

The technique comes from the Owlbear Rodeo UI/UX research: own action immediate, others' actions interpolated so the
eye can follow them. Only the technique was transferred, no code. The **200 ms** duration (ease-out cubic) is a
starting point inside the 150–250 ms range set by the product owner. It is the executor's choice, not a number
published by Owlbear Rodeo (`OdyMotion.RemoteUpdateDurationMs`).

## 3. Authorities and requirement references

- `AGENTS.md` §6 (the client owns no authoritative state), §10 / ADR-008, ADR-005.
- New test IDs: `TC-MOTION-001`…`TC-MOTION-006`.

## 4. Verified current state

- The client has **no network transport**. `BoardScreenPresenter` sees other writers' changes only when it renders
  again (`Refresh()`, which re-reads `ISceneRepository.ListTokens`). In the running trial, the only writers of token
  positions are this board's own drag and click-to-move.
- Resource bars (`OdyResourceBar`) exist only in the character sheet's Resources tab. Other writers already exist:
  - combat damage and ability/item costs (`ODY-S11-205`) change character resources;
  - a reload after a `StateChanged` conflict brings in other writers' values.

## 5. Scope

### In scope
- `Runtime/Ui/OdyMotion.cs` (new): `OdyMotion` (duration, tick interval, easing) and `OdyTween`, an eased value that
  is stepped with elapsed milliseconds and never reads a clock.
- `Runtime/BoardTokenMotion.cs` (new): which token changes animate. Pure state.
- `BoardScreenPresenter` glue:
  - render passes report positions to `BoardTokenMotion`;
  - `TryMoveTokenTo`, `TryMoveSelectedTokenTo`, group-drag commit and capture-loss rollback mark their tokens as
    local, so these moves, and the rollback of a denied move, are instant;
  - grabbing a token cancels its animation;
  - the board area's UI Toolkit scheduler drives running animations;
  - `IsTokenAnimating` and `AdvanceTokenMotion` are exposed for tests.
- `OdyResourceBar.AnimateFrom` / `AdvanceAnimation` / `DisplayedFraction`.
- `CharacterPanelPresenter`: a record returned by the panel's own command renders instantly. Any other render (reload,
  reopen, refresh after a conflict) animates each bar from its last shown fill. `ResourceBar(key)` is exposed for tests.

### Out of scope
- Networking or any new remote source. Animating other properties: scale, z-order, appearance, obstacles, fog.
- The label numbers. They always show the new value at once, so only the fill eases.
- Backend: untouched.

## 6. Decisions

- **"Local" means made by this presenter.** No backend field says who last moved a token, and the backend is off
  limits, so the board classifies a change by its origin: the moves it commits itself are local, and everything
  it only observes is remote. That covers another participant once a transport exists, with no further client change.
- **Time source.** Animation is presentation only. The authoritative value is already stored and displayed state is
  never written back. The tween is stepped with the UI Toolkit scheduler's tick delta (`TimerState.deltaTime`) and
  never reads a clock itself. No wall clock, `UnityEngine.Time` or `Task.Delay` (ADR-008 concerns authoritative logic,
  and this stays outside it).
- A render pass in the middle of an animation keeps it running. A new destination continues from the drawn point, so
  nothing jumps. Hidden tokens are never animated, and tokens that disappear are forgotten.
- The board's scheduled item is created per animation burst rather than resumed, so the first tick's delta starts
  when it is scheduled.

## 7. Acceptance criteria

1. Own drag and click-to-move are drawn at once, with no delay. ✔ (logic; visual regression check below)
2. Token updates from elsewhere interpolate over 200 ms. ✔ (logic; see the manual-check limitation below)
3. HP/resource bars changed by others animate to the new value. ✔ (logic; manual check below)
4. Existing board tests are unaffected. Every existing board test moves tokens through the presenter (local, so
   instant) or only re-renders unchanged positions.

## 8. Tests and validation

| ID | Test |
|---|---|
| `TC-MOTION-001` | `OdyMotionTests.Tween_EasesOutFromStartToTarget_WithinTheRequestedDurationRange` |
| `TC-MOTION-002` | `OdyMotionTests.TokenMotion_LocalChangeIsInstant_RemoteChangeEasesToTheNewPosition` |
| `TC-MOTION-003` | `OdyMotionTests.TokenMotion_ReRenderKeepsGoing_NewTargetContinuesFromTheDrawnPoint_GrabStopsIt_HiddenIsInstant` |
| `TC-MOTION-004` | `OdyMotionTests.ResourceBar_SetValueIsInstant_AnimateFromEasesTheFill_LabelShowsTheNewValueAtOnce` |
| `TC-MOTION-005` | `CharacterPanelPresenterTests.ResourceBar_OwnChangeIsInstant_ChangeMadeElsewhereAnimatesFromTheShownValue` |
| `TC-MOTION-006` | `BoardScreenPresenterTests.OwnMoveIsDrawnInstantly_MoveMadeElsewhereEasesToTheNewPosition` |

**Batchmode-verifiable:** all six, stepped with explicit milliseconds and no frames.

**Requires human visual confirmation in Play mode.** No batchmode run stands in for these checks:
- **Own drag stays 1:1.** Drag a token quickly in the trial and check that it sticks to the cursor. After release it
  stays put, with no snap and no ease. A denied drag, for example onto an occupied cell, returns instantly.
- **HP bar eases when changed elsewhere.**
  1. Open a character's sheet → Resources tab in the left drawer.
  2. In the Combat drawer, resolve an attack that damages that character.
  3. Close and reopen the Character drawer.
  4. Check that the HP fill slides from the old value to the new one in about 0.2 s, while the numbers show the new
     value at once.
- **Remote token interpolation.** Visual confirmation is **blocked**. The running app has no source of token moves
  made elsewhere (no client networking), so there is nothing a human can trigger to see it. It is covered only by
  `TC-MOTION-002/003/006` until a remote source exists. The check to do then is that another participant's move
  slides smoothly, and that a pan or zoom during the slide keeps the token on its path.

**Run in this session** (scratch build outside the repository; no Unity or pwsh in the container):
- Client sources and EditMode tests compiled against UnityEngine stand-ins.
- **64/64 passed.** That covers the SLICE-11 tests and `TC-POPOVER-*`, plus `TC-MOTION-001…005`.
- `TC-MOTION-006` and the rest of `BoardScreenPresenterTests` are Unity-only and were **not run**. A compile check
  against stand-ins reported no error on any line this task changed in `BoardScreenPresenter.cs` or in the new test;
  the remaining errors were stand-in gaps in unchanged code.
- `git diff origin/main -- Packages DotNet` is empty.

**Not run:** `test-unity.ps1`, `test-fast.ps1`, `verify-format.ps1`, `verify-repository.ps1`, `verify-docs.ps1`,
`build-dev.ps1`. Owner validation is pending.

## 9. Security, privacy, compatibility

- Hidden tokens are never animated. A token absent from `ComputeVisibleTokens` is drawn hidden at once, so no
  motion path reveals it.
- No data, contract or persistence change. Rollback = revert the commit. No dependency added.
