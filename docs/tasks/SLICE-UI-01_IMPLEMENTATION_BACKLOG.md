# Odyssey VTT — SLICE-UI-01 Minimal Trial UI Implementation Backlog

**Status:** Implementation revision — `ODY-UI-01-002` through `ODY-UI-01-006` complete; next child task is `ODY-UI-01-007`
**Slice:** `SLICE-UI-01 — Minimal UI Prerequisites` (renamed from `SLICE-04` 2026-08-27; see `SLICE-UI-01_BACKLOG.md` §0)
**Parent task:** `docs/tasks/active/ODY-UI-01-001_SLICE_UI_01_Implementation_Backlog.md`
**Predecessor backlog:** `docs/tasks/SLICE-UI-01_BACKLOG.md` (prerequisite revision — closed 2026-08-27, `ODY-UI-01-000`; zero new ADRs required, all five architectural questions confirmed against already-`Accepted` `ADR-001`/`002`/`008`)
**ExecPlan:** Not required (Brief plan)
**Created:** 2026-08-27
**Last updated:** 2026-08-27 UTC

## 1. Purpose

This backlog converts `SLICE-UI-01_BACKLOG.md` §3.4's already-agreed minimal screen/action list into small, reviewable implementation tasks — the same role `ODY-S03-003` played for `SLICE-03_IMPLEMENTATION_BACKLOG.md` after `SLICE-03_BACKLOG.md` closed. It does **not** itself implement anything. It only decomposes the already-decided minimal UI into ordered child tasks, each of which will be its own separate task contract and pull request, activated one at a time — the same convention `SLICE-01`/`SLICE-02`/`SLICE-03_IMPLEMENTATION_BACKLOG.md` already used.

Its sources of scope are, exclusively:

- `docs/tasks/SLICE-UI-01_BACKLOG.md` §3.4 (the minimal screen/action list — the *only* source of scope; nothing beyond it is added here) and §3.1–3.5 (the already-decided UI↔Application boundary, persistence choice, and role-switching convention, all cited as fixed, none reopened).
- `docs/tasks/active/ODY-S03-008_Vertical_Slice_Integration.md` (the ten-step scenario these screens exist to let a human walk by hand).
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md` §6.7 (already-`Accepted` `Odyssey.Unity.Client` boundary — cited, not reopened).

No child task in this backlog reopens any decision `SLICE-UI-01_BACKLOG.md` already made; each builds directly on those decisions as fixed.

## 2. Scope decisions requiring explicit justification

### 2.1 Six child tasks, grouped by screen/concern, not by roadmap step

`SLICE-UI-01_BACKLOG.md` §3.4 lists eight UI elements (board/token view, role selector, roll panel, modifier control, override control, result display, save-and-reopen action, game-log list) plus reroll/cancel buttons. Grouping them one-screen-per-task would produce eight-plus tiny tasks with heavy cross-dependencies (the result display cannot be built without the roll panel that produces a result to display); grouping them all into one task would produce an unreviewable, all-at-once UI task, the opposite of the small-reviewable-task discipline every `SLICE-0X_IMPLEMENTATION_BACKLOG.md` has used so far.

**Decision:** six child tasks, grouped by the natural feature boundary each already-built Application service defines, in the build order a human would need them to exist:

1. **Board screen** (`BoardMovementService`) — token render, selection, click-to-move.
2. **Role selector** (`ODY-UI-01-000`'s §3.3 convention) — cross-cutting infrastructure every later screen consumes.
3. **Roll panel + modifiers** (`DiceRollService.SubmitRoll`/`ProposeModifier`/`DecideModifier`).
4. **Override control + audience-aware result display** (`DiceRollService.ApplyOverride`, `DiceRollVisibilityPolicy`).
5. **Save/reopen campaign + game log list** (`SqliteGameLogRepository`, `GameLogReconnectService`).
6. **Reroll/cancel + full manual walkthrough** (`DiceRollService.RequestFullReroll`/`CancelRoll`, plus the manual, by-hand analogue of `ODY-S03-008`'s own ten-step proof).

This mirrors `SLICE-UI-01_BACKLOG.md` §3.4's own listed grouping almost exactly (the ТЗ that created this backlog already proposed this six-task split) — this backlog adopts it because it is already sound: each task maps to exactly one (or two closely coupled) Application contract(s), each produces a visibly demonstrable increment, and the dependency chain is linear and shallow (see §5).

### 2.2 Board screen ships before the role selector, with a single implicit local actor

Unlike roll/audience/override work (which genuinely needs to *switch* identity to prove `DiceRollVisibilityPolicy`'s per-role behavior), `BoardMovementService.MoveToken`'s own control-ownership check (`ADR-019` §5.2, exit criterion 2) is provable with a single fixed local actor: create two tokens with different `ControllerUserId`s, try moving each, observe one succeeds and one is denied. No role *switch* is required to prove that a non-controller is denied — only a second, differently-owned token. **Decision:** the board screen (task 1) ships first, using a single hardcoded local actor identity; the role selector (task 2) is introduced immediately after specifically because roll/modifier/override/audience work (tasks 3–5) cannot be meaningfully tried by hand without it — those genuinely require playing as different roles, not just different tokens.

### 2.3 No task contract file is created by this backlog

Per this task's own instruction and the `ODY-S03-003` precedent: this document only reserves task numbers, titles, and boundaries. Each child task contract (`ODY-UI-01-002` through `ODY-UI-01-007`) is created and activated as its own separate task, one at a time, when picked up — not by this scaffold.

## 3. Slice exit criteria

`SLICE-UI-01` (implementation) is complete only when a human can walk `ODY-S03-008`'s own ten-step scenario end-to-end through the UI, by hand:

1. Select the player's own token on the board (step 1).
2. Submit a roll intent through the roll panel; see it accepted or denied by role (steps 2–3).
3. See the formula result generated (step 4).
4. Propose and decide a modifier through the UI (step 5).
5. Apply a GM override with a mandatory reason through the UI (step 6).
6. See the result honor audience-aware visibility when switching roles (step 7).
7. Save and reopen the campaign; see the journal restored (steps 8–9).
8. Reroll; see the original entry remain in the journal unchanged (step 10).

Criterion 8 above ("a human can walk the full scenario") is satisfied by task 6's own final manual walkthrough, mirroring how `SLICE-03_IMPLEMENTATION_BACKLOG.md` §3's criterion 8 (`GATE-B` closure) was satisfied by that slice's own closure task confirming the rest.

## 4. Ordered backlog

| Order | Task ID | Screen/concern | Status | Depends on | Planning mode | Primary result |
|---:|---|---|---|---|---|---|
| 1 | `ODY-UI-01-002` | Board screen | Done | None | ExecPlan | `BoardScreenPresenter` renders the active scene's tokens at their real `TokenPosition` coordinates (`GridType=None`, no hex/grid rendering per `ADR-020`'s own scope); click-to-select then click-destination-to-move calls `BoardMovementService.MoveToken` with a single hardcoded local actor identity; a non-controller's attempted move is denied. Satisfies roadmap §12.6 step 1 and exit criterion 2 ("Player не может перемещать чужой токен без control") by hand. 4 EditMode tests. PR [#67](https://github.com/odyssey-services/Odyssey_VTT/pull/67) merged. The Unity/`Odyssey.Persistence` compile gap discovered during this task's own validation was resolved by `ODY-UI-01-002a` (PR [#68](https://github.com/odyssey-services/Odyssey_VTT/pull/68), merged); a real `scripts/test-unity.ps1` run then genuinely executed all 4 tests — all pass (40/40 EditMode, 2/2 PlayMode) — after fixing three further independent issues the never-before-real Unity run surfaced (a `double`→`StyleLength` cast, a missing `using`, and a test-cleanup file-locking race; see `docs/tasks/active/ODY-UI-01-002_Board_Screen.md` §18). |
| 2 | `ODY-UI-01-003` | Role selector | Done | None | ExecPlan | A compact UI Toolkit "Playing as: Player / MainGM / Observer" selector backed by shared Client-layer `RoleSelection` state using existing `BaselineRole` values. Supplies `actorUserId`/`actorIsMainGm`/`ActorCanCreateRoll` for later screens and retrofits `BoardScreenPresenter` to follow the selected actor. 4 new EditMode tests passed in a real `scripts/test-unity.ps1` run (44/44 EditMode, 2/2 PlayMode). Draft PR [#70](https://github.com/odyssey-services/Odyssey_VTT/pull/70) opened; CI run [33094205756](https://github.com/odyssey-services/Odyssey_VTT/actions/runs/33094205756) passed all 4 checks. |
| 3 | `ODY-UI-01-004` | Roll panel + modifiers | Done | 003 | ExecPlan | `RollPanelPresenter` adds a formula field and "Roll" button calling `DiceRollService.SubmitRoll` from the current `RoleSelection`, plus modifier label/value proposal and MainGM Accept/Change/Reject decisions through `ProposeModifier`/`DecideModifier`. Stores the most recent successful `DiceRoll` as `LastRoll` for later UI tasks; default audience is `Public` because audience-aware display is `ODY-UI-01-005`. 6 new EditMode tests passed in a real `scripts/test-unity.ps1` run (50/50 EditMode, 2/2 PlayMode). Draft PR [#71](https://github.com/odyssey-services/Odyssey_VTT/pull/71) opened; CI run [33105832028](https://github.com/odyssey-services/Odyssey_VTT/actions/runs/33105832028) passed all 4 checks. |
| 4 | `ODY-UI-01-005` | Override + audience-aware result display | Done | 003, 004 | ExecPlan | `RollPanelPresenter` now has a roll audience selector (`Public`, `PlayerAndGM`, `SelectedParticipants`) defaulting to `PlayerAndGM`, plus a MainGM-only override reason field/button calling `DiceRollService.ApplyOverride`. Result display calls `DiceRollVisibilityPolicy.TryGetVisibleRoll` before showing roll details, so Player/MainGM see the default roll and Observer gets safe-denial text with no formula/total details. 5 new/updated EditMode coverage points passed in a real `scripts/test-unity.ps1` run (55/55 EditMode, 2/2 PlayMode). Draft PR [#72](https://github.com/odyssey-services/Odyssey_VTT/pull/72) opened; CI run [33125908428](https://github.com/odyssey-services/Odyssey_VTT/actions/runs/33125908428) passed all 4 checks. |
| 5 | `ODY-UI-01-006` | Persistence + game log | Done | 004 | ExecPlan | `GameLogPresenter` adds a "Save & Reopen Campaign" action persisting the latest roll through `SqliteGameLogRepository.SaveDiceRollEntry`, then re-listing via a fresh repository instance against the same supplied `CampaignHandle`. It renders an unstyled scrollable list of visible `GameLogEntryRecord.SummaryPayload` values filtered through `GameLogReconnectService.GetVisibleEntries`, so Player/MainGM see the default `PlayerAndGM` roll and Observer does not. 4 new EditMode tests passed in a real `scripts/test-unity.ps1` run (59/59 EditMode, 2/2 PlayMode). Draft PR [#73](https://github.com/odyssey-services/Odyssey_VTT/pull/73) opened; CI run [33127771319](https://github.com/odyssey-services/Odyssey_VTT/actions/runs/33127771319) passed all 4 checks. |
| 6 | `ODY-UI-01-007` | Reroll/cancel + full walkthrough | Draft | 002, 003, 004, 005, 006 | Not yet determined | Reroll/Cancel buttons calling `RequestFullReroll`/`CancelRoll`; a final, manual, by-hand walkthrough of all ten `ODY-S03-008` steps through the assembled UI, with any real composition gap found reported (not silently patched around), mirroring `ODY-S03-008`'s own "report, don't improvise" instruction. Satisfies roadmap §12.6 step 10 and closes `SLICE-UI-01`'s own exit criteria (§3 above). |

"Planning mode" is intentionally left "Not yet determined" for every child task: each task's own Brief-plan-vs-ExecPlan decision is made when that task's own contract is authored, per `PLANS.md` §1, not pre-decided by this scaffold — the same convention `SLICE-03_IMPLEMENTATION_BACKLOG.md` used.

No `ODY-UI-01-002`–`007` task contract file exists yet. This backlog only reserves their numbers, titles, and boundaries; each is created and activated as its own separate task, one at a time, when picked up.

## 5. Task boundaries

### ODY-UI-01-002 — Board screen

Fixes: a Unity scene view (UI Toolkit + a simple `VisualElement`/`GameObject` render of tokens, or a minimal 2D representation — the exact rendering technique is this task's own implementation detail, not fixed here) showing the single active scene's tokens at their real, persisted `TokenPosition` coordinates; click-to-select, then click-destination-to-move, calling `BoardMovementService.MoveToken` directly (`SLICE-UI-01_BACKLOG.md` §3.2's direct-call convention). Uses a single hardcoded local actor identity (§2.2 above) — the role selector (task 2) is not yet available to this task. Does not implement drag-and-drop polish, animation, pan/zoom, hex-grid rendering, drawing, or a ruler — all explicitly excluded by `SLICE-UI-01_BACKLOG.md` §3.4.

### ODY-UI-01-003 — Role selector

Fixes: a persistent, cross-cutting "Playing as: Player / MainGM / Observer" UI control (`SLICE-UI-01_BACKLOG.md` §3.3), visible from every screen, supplying the caller-side `actorUserId`/`actorIsMainGm`/`ActorCanCreateRoll` values later tasks' Application calls need. Retrofits task 1's board screen to consume the selected actor instead of its own hardcoded placeholder, closing that task's own documented simplification. Does not implement any real session/identity/permission model — `ADR-019`'s full baseline remains out of scope, exactly as `ODY-S03-004`/`005` already established for their own caller-supplied-boolean simplification.

### ODY-UI-01-004 — Roll panel + modifiers

Fixes: a formula text field and "Roll" button calling `DiceRollService.SubmitRoll` (using the role selector's current actor and a caller-chosen `DiceRollAudience`, at minimum `Public` and one nontrivial case); a modifier-propose control and, when playing as MainGM, Accept/Change/Reject controls calling `ProposeModifier`/`DecideModifier`. Depends on `ODY-UI-01-003` for the acting identity. Does not implement roll-history browsing beyond what task 5's game-log list provides, and does not implement every one of the four audience kinds in this task's own UI — task 4 (override/result display) is where audience-aware display is actually exercised.

### ODY-UI-01-005 — Override control + audience-aware result display

Fixes: a reason field and "Override" button, enabled only when playing as MainGM, calling `ApplyOverride` (visibly rejecting an empty reason, per `ODY-S03-005`'s own mandatory-reason rule); a result display that calls `DiceRollVisibilityPolicy.TryGetVisibleRoll` for the currently role-selected viewer before rendering anything, so switching the role selector to an excluded Observer visibly shows nothing (safe denial, `ODY-S03-006`'s own tested property, now provable by hand). Depends on `ODY-UI-01-003` (role selector) and `ODY-UI-01-004` (a roll must exist to override or display). Does not implement per-field partial redaction — `ODY-S03-006`'s own all-or-nothing baseline is unchanged.

### ODY-UI-01-006 — Persistence + game log

Fixes: a "Save & Reopen Campaign" action calling `SqliteGameLogRepository.SaveDiceRollEntry` for the current roll, then constructing a *new* repository instance (simulating the same "reopen campaign.db" pattern `ODY-S03-007`/`008`'s own tests already use as "reconnect") and re-listing via `ListGameLog`; a simple, unstyled scrollable list of `GameLogEntryRecord.SummaryPayload` values, filtered through the currently role-selected viewer via `GameLogReconnectService.GetVisibleEntries`. Depends on `ODY-UI-01-004` (a resolved roll must exist to persist). Does not implement board/scene persistence UI beyond what already exists implicitly through `SqliteSceneRepository` (task 1 already persists tokens durably; no separate "save the board" action is needed since every board write is already durable per `ODY-S03-004`).

### ODY-UI-01-007 — Reroll/cancel + full manual walkthrough

Fixes: Reroll/Cancel buttons calling `DiceRollService.RequestFullReroll`/`CancelRoll`; and, as this task's own closing deliverable, one real, manual, by-hand walkthrough of all ten `ODY-S03-008` roadmap §12.6 steps using the assembled UI from tasks 1–6 together, with any real composition gap found reported in this task's own contract (not silently patched around with new production logic outside its own scope), mirroring `ODY-S03-008`'s own explicit "stop and report, don't improvise" instruction. Depends on all five prior tasks. Does not implement any new game mechanic — this task only proves the already-assembled UI can walk the already-proven scenario.

## 6. Dependency rules

- `ODY-UI-01-002` (board) has no dependency on any other child task — it can begin immediately once this backlog is accepted.
- `ODY-UI-01-003` (role selector) has no dependency on `ODY-UI-01-002`'s own completion to *begin*, but its own deliverable includes retrofitting task 2's hardcoded actor — so in practice it is picked up after task 1 ships, not before, to avoid two tasks touching the same board screen's actor-resolution code concurrently.
- `ODY-UI-01-004` (roll panel) depends on `ODY-UI-01-003` (needs the role selector's actor/permission values).
- `ODY-UI-01-005` (override + result display) depends on `ODY-UI-01-003` and `ODY-UI-01-004` (needs both the role selector and an existing roll to override/display).
- `ODY-UI-01-006` (persistence + log) depends on `ODY-UI-01-004` (needs a resolved roll to persist).
- `ODY-UI-01-007` (reroll/cancel + walkthrough) depends on all five prior tasks (`002`–`006`) — it is the closing task, exercising everything the other five built, together.
- No task in this backlog depends on `ODY-S02-014`/`ADR-016` §14 (real network) — unchanged from `SLICE-UI-01_BACKLOG.md`'s own carried-forward framing.

## 7. Global non-goals

This backlog revision excludes:

- Any UI implementation code itself — each is its own separate future child task activation, not this scaffold.
- Everything `SLICE-UI-01_BACKLOG.md` §3.4 already explicitly excluded: drawing/annotation tools, a ruler, drag-and-drop polish, animation, sound, multiple scenes or scene-management UI, pan/zoom polish, hex-grid rendering, localization, mobile/web platform targets.
- Real network integration (`ODY-S02-014`/`ADR-016` §14) — a separate, still-deferred product-owner decision.
- Reopening any already-`Accepted` ADR, or any already-decided `SLICE-UI-01_BACKLOG.md` §3.1–3.5 architectural/scope decision.
- Any new game mechanic beyond what `SLICE-00`–`03` already implemented.
- Reconciling the (already-resolved, per `ODY-UI-01-000`/rename point-fix) naming collision with the roadmap's own `SLICE-04` — that is closed, not this backlog's concern.

## 8. Backlog change control

- New work requires a new `ODY-UI-01-XXX` task contract.
- A task may be split before implementation by updating this backlog and, if a governing ExecPlan exists for that specific child task, that ExecPlan too.
- A task may not be merged with unrelated cleanup merely to reduce task count.
- This backlog does not replace task acceptance criteria; it does not itself decide any technical question `SLICE-UI-01_BACKLOG.md` didn't already settle.
