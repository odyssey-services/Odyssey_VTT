# Odyssey VTT — SLICE-03 Playable Foundation Implementation Backlog

**Status:** Implementation revision — `ODY-S03-004` Done (PR #58 merged by product owner); `ODY-S03-005` Done (PR #59 merged by product owner); `ODY-S03-006` In Review (Draft PR open, CI in progress); `ODY-S03-007`–`009` Draft, not yet activated
**Slice:** `SLICE-03 — Playable Foundation (vertical slice implementation)`
**Parent task:** `docs/tasks/active/ODY-S03-003_SLICE_03_Implementation_Backlog.md`
**Predecessor backlog:** `docs/tasks/SLICE-03_BACKLOG.md` (prerequisite ADR revision — closed 2026-08-26, historical; not rewritten by this document beyond its own closure section)
**ExecPlan:** Not required (Brief plan)
**Created:** 2026-08-26
**Last updated:** 2026-08-26 UTC

## 1. Purpose

This backlog converts roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` section 12.6 (the ten-step "Бросок и журнал" vertical slice) and section 12.7 (exit criteria) into small, reviewable implementation tasks. It is the implementation-revision that `docs/tasks/SLICE-03_BACKLOG.md` section 1 reserved for creation once its own (prerequisite) revision closed — which it did on 2026-08-26, with `ADR-020`/`ADR-021` both `Accepted` (`SLICE-03_BACKLOG.md` §2.1).

This backlog does **not** itself implement anything. It only decomposes the vertical slice into ordered child tasks, each of which will be its own separate task contract and pull request, activated one at a time — the same convention `SLICE-01_IMPLEMENTATION_BACKLOG.md` and `SLICE-02_IMPLEMENTATION_BACKLOG.md` used. No child task contract file is created by this document; it only reserves numbers, titles, and boundaries.

Its sources of scope are, exclusively:

- `17_Roadmap_Odyssey_VTT_v0.11.md` §12.6 (the ten-step vertical slice scenario) — private local reference, not committed to the repository.
- `17_Roadmap_Odyssey_VTT_v0.11.md` §12.7 (exit criteria).
- `17_Roadmap_Odyssey_VTT_v0.11.md` §12.3 (Board baseline) — narrowed to only what §12.6/§12.7 actually exercise (section 2.1).
- The already-`Accepted` ADRs governing each area: `ADR-002` (command/event model), `ADR-004` (result/error model), `ADR-008` (deterministic RNG), `ADR-012` (append-only journal), `ADR-017` (snapshot/delta/reconnect), `ADR-019` (permissions baseline), `ADR-020` (board geometry/movement determinism), `ADR-021` (extended audience/selected-participant visibility).

No child task in this backlog reopens any decision these ADRs already made; each builds directly on those contracts as fixed.

## 2. Scope decisions requiring explicit justification

### 2.1 Board baseline (roadmap §12.3) — narrowed to exactly what the vertical slice exercises

Roadmap §12.3 names a broad Board baseline: single active scene, gridless, basic hex grid, pan/zoom, token selection, authoritative movement, pointer, ruler, basic drawing, object lock, layer baseline, Undo/Redo for supported commands, scene save. The §12.6 ten-step scenario itself only exercises **token selection** (step 1); it never draws, measures, or locks an object. §12.7's exit criteria add exactly three board-relevant requirements beyond token selection: "Board state одинаков после restart и reconnect," "Player не может перемещать чужой токен без control," and "Undo/Redo не обходит permissions и host validation."

**Decision, following the same narrowing discipline `SLICE-02_IMPLEMENTATION_BACKLOG.md` §2.2 used for the asset channel:** this revision implements exactly — single active scene existence, token selection tied to `LinkedEntityRef`/control ownership, authoritative movement validated host-side (reusing `ADR-002`'s command pipeline and `ADR-020`'s geometry), Undo/Redo for the movement command specifically (as a permission-and-revision-rechecked compensating command, `08_Scenes_And_Board` §21.5), and scene/board state persistence surviving restart and reconnect (reusing `ADR-012`/`ADR-017`). It does **not** implement pan/zoom (pure local camera state, `08_Scenes_And_Board` §6.3, not authoritative), pointer or ruler (explicitly local-only/not networked/not persisted, §17.1/§17.2), basic drawing (`08_Scenes_And_Board` §17.3–17.6, no persistent `Drawing` object is created or read by any of the ten steps or exit criteria), object lock as its own tested feature (§20.4 — relevant only to concurrent multi-editor scenarios the ten-step scenario does not exercise), gridless-vs-hex grid rendering choice (a presentation/`GridType` selection already fully fixed by `ADR-020`, not itself a new behavior to implement), or layer baseline as a distinct feature (tokens exist on the already-fixed system-layer model without a dedicated layer-management task). None of these gate any §12.7 exit criterion or any of the ten steps.

**Consequence:** the full remaining Board baseline (drawing tools, ruler, pointer, layer management UI, object locking as a concurrent-editing feature, grid-type switching UI) is deferred to a broader, later Board-implementation task outside this minimal vertical slice — not blocked by anything in this revision, simply not required to close `SLICE-03`'s own exit criteria.

### 2.2 Full-text search and session archive/export (roadmap §12.4) — not a task in this revision

Roadmap §12.4 lists "filters and permission-aware search" and "session archive and JSON export" as part of the Dice/Log baseline. Neither is named by any of the ten §12.6 steps or any §12.7 exit criterion — the scenario never searches the log or exports an archive. `ADR-021` §9 already explicitly excluded full permission-aware search design from its own scope, confirming only that the safe-denial principle extends to it (not designing the search itself).

**Decision:** no dedicated search/archive/export task is created in this revision. A future, separate implementation task (outside this vertical slice) picks up `09_Dice_And_Game_Log` §27's full search design and §30's archive/export contract once product priority calls for it — the same narrowing this session already applied consistently (`SLICE-02_IMPLEMENTATION_BACKLOG.md` §2.2 for the asset channel).

### 2.3 No real-transport gate equivalent to `ADR-016` §14 — not applicable to this revision

`SLICE-02_IMPLEMENTATION_BACKLOG.md` §2.1 carried forward `ADR-016` §14's normative pre-production-integration gate on the real Unity Relay SDK, blocking `ODY-S02-014` until a dedicated follow-up spike closes `SP-03`'s gaps. Nothing in `SLICE-03`'s prerequisite closure (`SLICE-03_BACKLOG.md` §2.1) carries forward an equivalent gate — both `ADR-020` and `ADR-021` were accepted without any deferred spike or partial-confidence caveat (`SLICE-03_BACKLOG.md` §2.1's own explicit statement). This revision therefore has no task analogous to `ODY-S02-014`, and no task in this backlog is blocked on an external product-owner decision.

## 3. Slice exit criteria

`SLICE-03` (vertical-slice implementation) is complete only when all of the following, taken verbatim from roadmap §12.7, are proven:

1. Board state одинаков после restart и reconnect (board state is identical after restart and reconnect).
2. Player не может перемещать чужой токен без control (a Player cannot move another entity's token without control).
3. бросок рассчитывается только host (the roll is calculated only by the host).
4. roll visibility применяется на сетевой границе (roll visibility is enforced at the network boundary).
5. журнал объясняет итог (the log explains the outcome).
6. GM Override всегда оставляет audit trail (a GM override always leaves an audit trail).
7. Undo/Redo не обходит permissions и host validation (Undo/Redo does not bypass permissions and host validation).
8. закрыт `GATE-B — Playable Foundation` (roadmap milestone gate `GATE-B` is closed).

Criterion 8 (`GATE-B` closure) is a milestone-gate statement, not itself a technical property — it is satisfied by this revision's own closure task confirming criteria 1–7 hold with real evidence, mirroring how `SLICE-02_IMPLEMENTATION_BACKLOG.md`'s closure task (`ODY-S02-015`) served the same role for `SLICE-02`'s own milestone.

## 4. Ordered backlog

| Order | Task ID | Roadmap step(s) | Title | Status | Depends on | Planning mode | Primary result |
|---:|---|---|---|---|---|---|---|
| 1 | `ODY-S03-004` | 1 | Board Foundation: Scene, Token Selection & Authoritative Movement | Done | None | ExecPlan | Single active scene existence; token selection tied to control ownership (`ControllerUserId`); `BoardMovementService.MoveToken` validated host-side reusing `ADR-002`'s pipeline and `ADR-020`'s `GridType=None` geometry (`BoardGeometry`); `ISceneRepository.MoveToken` now enforces `ExpectedRevision` atomically (a real gap fixed, not previously checked); `UndoMoveToken` as a permission-and-revision-rechecked compensating command, not a blind rollback — satisfies exit criteria 2 and 7. 13 new `TC-BOARD-*` tests, all passing. PR [#58](https://github.com/odyssey-services/Odyssey_VTT/pull/58) merged by product owner (owner review complete). |
| 2 | `ODY-S03-005` | 2–6, 10 | Dice Roll Engine, Host Authority, Modifiers & Reroll/Cancel | Done | None | ExecPlan | `Odyssey.Domain.Dice.DiceFormulaParser` (MVP grammar/limits) + `Odyssey.Application.Dice.DiceRollService`: roll intent submission and host-side permission validation (caller-supplied `ActorCanCreateRoll`, extending `ADR-019`'s action-check pipeline to `Roll.*` keys); d100/formula-parser result generation using `ADR-008`'s already-implemented host-authoritative injectable RNG (`RngContracts.cs`, no new algorithm); modifier proposal/decision pipeline (`ProposeModifier`/`DecideModifier`) with no hidden GM numeric modifiers; `GMOverride` as a separate immutable reasoned record; full reroll and cancellation, original `DiceRoll` never rewritten — satisfies exit criteria 3 and 6. 27 new `TC-DICE-*` tests, all passing. PR [#59](https://github.com/odyssey-services/Odyssey_VTT/pull/59) merged by product owner. |
| 3 | `ODY-S03-006` | 7 | Audience-Aware Roll & Log Delivery | In Review | 005 | ExecPlan | New `Odyssey.Application.Dice.DiceRollVisibilityPolicy` (not a `VisibilityPolicy` extension — an incompatible sibling vocabulary per `ADR-021` §3.3) applies `ADR-021` §5's audience model to `DiceRoll` delivery for all four `09_Dice_And_Game_Log` §16 audience kinds (`Public`/`PlayerAndGM`/`GMOnly`/`SelectedParticipants`); new shared `Odyssey.Application.Audience` namespace with a minimal `ICampaignUserGroupDirectory` fixture (`ADR-021` §4's narrow read-model, not the full lifecycle); pure `TryGetVisibleRoll`/`ComputeAudienceViews` functions, no wire codec — satisfies exit criterion 4. 6 new `TC-DICE-019`–`023` tests, all passing, including safe-denial and MainGM-always-sees coverage. Draft PR (link recorded once opened). |
| 4 | `ODY-S03-007` | 8–9 | Game Log & Board State Persistence, Reconnect Replay | Draft | 004, 005, 006 | Not yet determined | `GameLogEntry`/board-state persistence via `ADR-012`'s append-only journal; reconnect delta/snapshot delivery via `ADR-017`, reusing `ADR-021`'s audience-aware redaction (006) so a reconnecting client's replayed log/board state matches its current, not stale, visibility — satisfies exit criteria 1 and 5 |
| 5 | `ODY-S03-008` | 1–10 | Vertical Slice Integration | Draft | 004–007 | Not yet determined | The roadmap §12.6 ten-step scenario as one automated, reproducible end-to-end check: player selects own token → sends roll intent → host validates permission → host generates d100 result → modifiers applied → GM overrides with reason → only permitted clients receive result → event persisted → reconnect restores visible log → original event remains after reroll/cancel |
| 6 | `ODY-S03-009` | — | SLICE-03 Acceptance and Closure Gate | Draft | 008 | Not yet determined | Traceability matrix, all eight roadmap §12.7 exit criteria (including `GATE-B` closure) checked with real evidence, owner acceptance — mirrors `ODY-S02-015`'s closure pattern |

"Planning mode" is intentionally left "Not yet determined" for every child task: each task's own Brief-plan-vs-ExecPlan decision is made when that task's own contract is authored, per `PLANS.md` section 1, not pre-decided by this scaffold — the same convention `SLICE-02_IMPLEMENTATION_BACKLOG.md` used.

No `ODY-S03-004`–`009` task contract file exists yet. This backlog only reserves their numbers, titles, and boundaries; each is created and activated as its own separate task, one at a time, when picked up.

## 5. Task boundaries

### ODY-S03-004 — Board Foundation: Scene, Token Selection & Authoritative Movement

Implements a single active scene's existence, token selection restricted to a controlled `LinkedEntityRef` (`08_Scenes_And_Board` §11, `BOARD-INV-006`/`007`), and `MoveToken` command validation entirely host-side — reusing `ADR-002`'s existing command pipeline for the transaction/permission-recheck structure and `ADR-020`'s geometry (distance formulas, `GeometryEpsilonV1`, `SpatialIndexV1`) for the actual movement/obstacle validation math. Implements Undo/Redo for the movement command as a compensating command with fresh permission/revision validation (`08_Scenes_And_Board` §21.5, `ADR-020` not reopened) — satisfying exit criterion 7 alongside criterion 2. Does not implement dice rolls, audience-aware redaction, or persistence/reconnect (`ODY-S03-005`–`007`). Does not implement drawing, ruler, pointer, object-lock-as-a-feature, or layer management (section 2.1).

### ODY-S03-005 — Dice Roll Engine, Host Authority, Modifiers & Reroll/Cancel

Implements roll-intent submission and host-side permission validation extending `ADR-019`'s existing action-check pipeline to the `Roll.*` permission keys (`09_Dice_And_Game_Log` §32); the MVP dice-formula parser and d100 result generation using `ADR-008`'s already-accepted host-authoritative injectable RNG (no new RNG algorithm, `ADR-008` §38 point 4); the modifier proposal/decision pipeline with the explicit "no hidden GM numeric modifiers" rule (§12.3); `GMOverride` as a separate, immutable record requiring a mandatory reason (§19); full reroll (whole-roll only, §17) and cancellation (§18), with the original `DiceRoll` never rewritten in place. Satisfies exit criteria 3 ("host calculates the roll") and 6 ("GM override always leaves an audit trail"). Does not implement audience-aware delivery of the result (`ODY-S03-006`) or persistence/reconnect (`ODY-S03-007`) — this task proves the roll is computed and overridden correctly host-side, not who receives it or how it survives a restart.

### ODY-S03-006 — Audience-Aware Roll & Log Delivery

Extends `ADR-019`'s `VisibilityPolicy` function with `ADR-021`'s §5 additional inputs (`SelectedParticipants`/`CampaignUserGroup`), applied specifically to `DiceRoll`/`GameLogEntry` delivery for the four audience kinds `09_Dice_And_Game_Log` §16.1 names (`Public`/`PlayerAndGM`/`GMOnly`/`SelectedParticipants`). Computed at the same Application-layer projection-construction point `ADR-019` §6.2 already fixed — not a new check point. Satisfies exit criterion 4 ("roll visibility is enforced at the network boundary"). Does not implement `CampaignUserGroup`'s lifecycle commands (create/rename/archive — `ADR-021` §4 already deferred these as ordinary, non-architecturally-novel `ADR-002` commands; this task may need a minimal group-membership fixture for its own tests, not a full management UI/command surface). Does not implement persistence/reconnect (`ODY-S03-007`).

### ODY-S03-007 — Game Log & Board State Persistence, Reconnect Replay

Implements `GameLogEntry` and board-state persistence via `ADR-012`'s already-accepted append-only journal contract, and reconnect delta/snapshot delivery via `ADR-017`, reusing `ODY-S03-006`'s audience-aware redaction so a reconnecting client's replayed log and board state reflect its *current* visibility (`ADR-021` §6's evaluation-time rule, `ADR-019` §1 point 8's reconnect-by-current-permissions rule) — neither reopened here. Satisfies exit criteria 1 ("board state is identical after restart and reconnect") and 5 ("the log explains the outcome," i.e., the persisted formula/`CalculationTrace` is retrievable and complete). Does not implement full-text search over the log (section 2.2) or session archive/export (section 2.2).

### ODY-S03-008 — Vertical Slice Integration

Implements the roadmap §12.6 ten-step scenario as a single, automated, reproducible end-to-end check exercising every prior task's deliverable together — the same "integration proof, not a new feature" role `ODY-S01-013`/`ODY-S02-013` played for their own slices. Does not introduce new behavior beyond what `ODY-S03-004`–`007` already implement.

### ODY-S03-009 — SLICE-03 Acceptance and Closure Gate

Produces a traceability matrix and quality report mirroring `ODY-S02-015`'s pattern, checks all eight roadmap §12.7 exit criteria (including `GATE-B` milestone closure) against real evidence from `ODY-S03-004`–`008`, and records explicit product-owner acceptance. Does not implement new product behavior — closure/evidence only.

## 6. Dependency rules

- `ODY-S03-004` has no dependency — it is a foundational board/token task built directly atop already-accepted `ADR-002`/`ADR-020`.
- `ODY-S03-005` has no dependency on `ODY-S03-004` — the dice-roll engine (RNG, formula parser, modifiers, `GMOverride`, reroll/cancel) is independent domain logic built atop already-accepted `ADR-002`/`ADR-008`/`ADR-019`, not board/token state. Both `ODY-S03-004` and `ODY-S03-005` may proceed in either order or in parallel, the same independence `SLICE-03_BACKLOG.md` §6 already established between `ODY-S03-001`/`002`.
- `ODY-S03-006` depends on `ODY-S03-005` (audience-aware delivery needs a `DiceRoll`/`GameLogEntry` artifact to redact).
- `ODY-S03-007` depends on `ODY-S03-004` (board state to persist), `ODY-S03-005` (roll/log entities to persist), and `ODY-S03-006` (reconnect replay must reuse the already-established audience-aware redaction, not recompute a separate, potentially inconsistent visibility rule).
- `ODY-S03-008` depends on all of `ODY-S03-004`–`007` (it is the integration proof exercising every prior deliverable together).
- `ODY-S03-009` depends on `ODY-S03-008` (closure requires the integration proof to exist as evidence).

## 7. Global non-goals

This backlog revision excludes:

- Drawing tools, ruler, pointer, object-lock-as-a-feature, layer management UI, and grid-type-switching UI — see section 2.1.
- Full permission-aware full-text search over the Game Log, session archive, and JSON export — see section 2.2.
- `CampaignUserGroup`'s lifecycle commands (create/rename/archive) as a dedicated feature/UI — `ADR-021` §4 already deferred these as ordinary `ADR-002` commands with no architecturally novel content; any child task needing group membership for its own tests uses a minimal fixture, not a management surface.
- `AssistantGM`, delegation, arbitrary `PermissionKey`/`Scope`, ownership/control-based visibility beyond character-assignment, temporary permissions — all outside `ADR-019`'s own baseline scope (`ADR-019` §10) and outside `ADR-021`'s extension of it; not reopened by this revision.
- Real Unity Relay transport integration (`ADR-016` §14, `ODY-S02-014`) — a `SLICE-02` concern, not reopened or touched by this revision (section 2.3).
- Combat, characters, progression, inventory, or any content/rules-engine system beyond what `09_Dice_And_Game_Log`'s roll model itself requires — roadmap §12.6 does not include them; they belong to later roadmap stages.
- Any UI/UX polish beyond what is needed to prove the roadmap §12.6 scenario programmatically.
- Full `08_Scenes_And_Board_Odyssey_VTT_v0.5.md`/`09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md` generality beyond what sections 2.1/2.2 above scope this revision to.

## 8. Backlog change control

- New work requires a new `ODY-S03-0XX` task contract; this document only reserves numbers `ODY-S03-004` through `ODY-S03-009`.
- A task may be split before implementation by updating this backlog (and, if a governing ExecPlan exists for that specific child task, that ExecPlan too), following the same rule prior backlog revisions in this repository already use.
- A task may not be merged with unrelated cleanup merely to reduce task count.
- Completed task files move to `docs/tasks/completed/` only after required review, per the established convention in this repository.
- This backlog does not replace any task's own acceptance criteria or any ADR's content; it does not itself decide any technical question beyond the three explicit scope decisions in section 2.
- The predecessor `docs/tasks/SLICE-03_BACKLOG.md` (prerequisite ADR revision) is not rewritten by this document beyond its own closure section (§2.1, added by `ODY-S03-003`) — it remains otherwise a closed, historical artifact.
- If this document's section 2 narrowing decisions are later found incorrect or resolved sooner than expected, that is a new task/backlog-revision decision, not a silent edit to this document's already-recorded reasoning — this document would gain an explicit amendment note, not a rewritten section 2.
