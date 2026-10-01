# ODY-S11-214 — Camera autofocus on the active participant at turn change (UI polish P0, item 5)

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

When the turn passes to a participant whose token position is known, the board camera eases so that the token is in
frame. Manual camera input always takes priority and interrupts the move. Also record whether Odyssey has a round
counter.

## 2. Round counter (recorded, no implementation needed)

**Yes.** `CombatEncounterRecord.RoundOrdinal`, together with `TurnOrdinal` (in
`Packages/com.odyssey.application/Runtime/Combat/CombatEncounterContracts.cs`), is maintained by
`CombatEncounterService.Advance`. The combat panel (`ODY-S11-205`) already shows it as the "Round N" badge
(`combat-round`), next to "Turn N" (`combat-turn`).

## 3. Authorities and requirement references

- `ODY-S11-205` (combat panel), `ODY-S11-201` (board), `ODY-S11-211` (motion conventions), ADR-005.
- Hidden information: `ComputeVisibleTokens` (SLICE-10 Block 6) decides what a non-MainGM sees.
- New test IDs: `TC-CAMFOCUS-001`…`TC-CAMFOCUS-005`.

## 4. Scope

### In scope
- `Runtime/BoardCameraFocus.cs` (new): an in-frame test and an eased camera-offset move, stepped with milliseconds.
- `BoardScreenPresenter`:
  - `FocusOnCharacter(CharacterId)` returns `Started`, `AlreadyInFrame`, `NotOnBoard` or `Hidden`;
  - `IsCameraFocusing` and `AdvanceCameraFocus` are exposed for tests;
  - `BeginBoardPan`, `ZoomBoard` (which the wheel uses) and `HandleBoardButtonDown` (any press on the board) cancel
    a running focus;
  - the token-per-character and visibility lookups are filled during the render pass.
- `CombatPanelPresenter.ActiveParticipantChanged`: raised when the acting participant changes. That covers starting
  or opening an encounter, the panel's own "Next turn", and a turn change observed on a reload. A reload of the same
  turn does not raise it.
- `TrialScreenPresenter` subscribes it to `Board.FocusOnCharacter` and unsubscribes on dispose.

### Out of scope
- Zoom changes (the camera only pans). Focusing on a turn change seen by another participant's client (there is no
  client networking).
- Backend: untouched.

## 5. Decisions

- **"In frame".** A token inside the view minus a 15% inset on each side counts as in frame, and the camera does not
  move. Otherwise the camera eases until the token is centred. The duration is **300 ms** with ease-out, slightly
  longer than a token move because the whole view moves. Both values are the executor's choice; neither is a number
  published by Owlbear Rodeo or another reference.
- **Hidden tokens are never focused.** If the acting participant's token is not in the local actor's visible set,
  the result is `Hidden` and the camera stays still, because moving it would reveal where the token is.
- **Manual input wins.** Any board press, pan or zoom cancels the move where it is, and it does not resume.
- At the end of a move, the board re-renders once, as the shell's initial centring already does, so obstacles and fog
  follow the camera. During the move only tokens and the marker are repositioned, which is the same behaviour as a
  manual pan.
- Time is stepped by the UI Toolkit scheduler delta, as in `ODY-S11-211`.

## 6. Acceptance criteria

1. On a turn change, with the token position known, the camera eases so the token is in frame. ✔ (logic; manual check
   below)
2. Manual camera input interrupts the animation and takes priority. ✔ (logic; manual check below)
3. Round counter recorded (§2). ✔

## 7. Tests and validation

| ID | Test |
|---|---|
| `TC-CAMFOCUS-001` | `BoardCameraFocusTests.InFrame_MeansInsideTheViewMinusTheInsetOnEachSide` |
| `TC-CAMFOCUS-002` | `BoardCameraFocusTests.Focus_EasesTheCameraUntilTheTokenIsCentered_WithoutChangingZoom` |
| `TC-CAMFOCUS-003` | `BoardCameraFocusTests.Cancel_StopsTheMoveWhereItIs` |
| `TC-CAMFOCUS-004` | `CombatPanelPresenterTests.TurnChange_AnnouncesTheActingParticipantOnce_WhetherAdvancedHereOrObservedOnReload` |
| `TC-CAMFOCUS-005` | `BoardScreenPresenterTests.FocusOnCharacter_EasesTheCameraToAnOffScreenToken_ManualPanInterrupts_HiddenTokensAreNeverFocused` |

**Batchmode-verifiable:** all five, stepped with explicit milliseconds.

**Requires human visual confirmation in Play mode.** No batchmode run stands in for these checks:
1. **Setup.** In the trial, as MainGM, place two character tokens far apart, using "Place token on scene" from the
   Character drawer. Pan the camera away from them.
2. **Start.** Start an encounter in the Combat drawer. The camera glides in about 0.3 s until the first actor's token
   is centred.
3. **Next turn.** Press "Next turn". The camera glides to the next actor. If that token is already well inside the
   view, the camera does not move.
4. **Interruption.** Press "Next turn", then immediately drag with the middle button or scroll. The glide stops at
   once and the camera follows only the user's input.
5. **Hidden token.** As a Player who cannot see the acting participant's token, the camera does not move.
6. **After the glide.** Obstacles and fog are drawn correctly after the glide ends.

**Run in this session** (scratch build outside the repository; no Unity or pwsh in the container):
- Client sources and EditMode tests compiled against UnityEngine stand-ins.
- **72/72 passed**. That includes `TC-CAMFOCUS-001…004`.
- `TC-CAMFOCUS-005` and the rest of `BoardScreenPresenterTests` are Unity-only and were **not run**. A compile check
  against stand-ins reported no error on any changed line of `BoardScreenPresenter.cs`, `BoardCameraFocus.cs` or the
  new test.
- `git diff origin/main -- Packages DotNet` is empty.

**Not run:** `test-unity.ps1`, `test-fast.ps1`, `verify-format.ps1`, `verify-repository.ps1`, `verify-docs.ps1`,
`build-dev.ps1`. Owner validation is pending.

## 8. Security, privacy, compatibility

- Hidden tokens are never focused (§5).
- No data, contract or persistence change. Rollback = revert the commit. No dependency added.
