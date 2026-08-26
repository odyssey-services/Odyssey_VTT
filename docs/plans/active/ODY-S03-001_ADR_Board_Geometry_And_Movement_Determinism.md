# ExecPlan — ODY-S03-001: ADR: Board Geometry and Movement Determinism

**Governing task contract:** `docs/tasks/active/ODY-S03-001_ADR_Board_Geometry_And_Movement_Determinism.md`
**Status:** Complete (deliverable produced; PR pending CI/review)
**Created:** 2026-08-26
**Last updated:** 2026-08-26 UTC

## Authorities

- `08_Scenes_And_Board_Odyssey_VTT_v0.5.md` — full document, especially §6.1, §7.7, §13.4, §21.6/BT-079, §25.1, §25.4, §4.4.
- `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md` §26 (`board.token.move v1` example).
- `docs/adr/ADR-008_Deterministic_Clock_and_RNG_v1.0.md` — full document, as the structural/rigor precedent for cross-platform determinism and versioned-constant style.
- `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` §1.
- `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` — structural format template (most recently accepted ADR).
- `docs/tasks/active/ODY-S03-000_SLICE_03_Playable_Foundation_Prerequisites.md` §4 (Verified facts this ADR must answer).
- `docs/tasks/SLICE-03_BACKLOG.md` §5 (`ODY-S03-001` task boundary).

## Investigation performed

1. Read `08_Scenes_And_Board_Odyssey_VTT_v0.5.md` in full (3070 lines) to confirm the exact scope of what the product document already fixes (§6.1's `WorldPosition`/finite-value rules, §7.7's named distance-metric list) versus what it explicitly names as an open "implementation ADR" question (§13.4 epsilon, §25.1 spatial index) versus what is out of scope entirely (§25.4 rendering).
2. Confirmed via `ADR-002` §26 that token movement is already modeled as an ordinary authoritative command (`board.token.move v1`) — this ADR does not reopen the command model, only the geometry computation used inside its validation steps (`08_Scenes_And_Board` §12.4 points 7–9).
3. Confirmed via `ADR-017` §1 that board deltas reuse the generic projection-delivery protocol — not reopened.
4. Read `ADR-008` in full as the structural/rigor precedent for how this repository already documents cross-platform numeric determinism (IEEE-754 `double` contract, versioned algorithm/constant naming like `StreamDerivationV1`/`PRNGV1`) — applied the same pattern to geometry (`GeometryEpsilonV1`, `SpatialIndexV1`).
5. Derived exact formulas for the three Square distance metrics and hex-distance, since `08_Scenes_And_Board` §7.7 names them but does not define them mathematically — cross-checked against standard tabletop-RPG grid-distance conventions (Chebyshev for "diagonal=1", alternating 1-2 for "5-10-5-10", cube-coordinate hex distance) to avoid inventing a metric not already implied by the named options.
6. Chose `GeometryEpsilonV1 = 1e-6` world units and a uniform-spatial-hash `SpatialIndexV1`, with explicit rejected-alternatives reasoning (§12 of the ADR) — R-tree/quadtree rejected as unnecessary complexity at the documented MVP scale (`08_Scenes_And_Board` §25: ~200 tokens, single Board per Scene, snap-to-cell model).
7. Cross-checked `SLICE-03_BACKLOG.md` §3's "no spike required" justification against this ADR's own content — confirmed every decision here is provable by golden-vector/brute-force-reference tests (fixed in the ADR's own Definition of Done, §11), not requiring empirical measurement against an uncontrollable environment.

## Intended change

- New file: `docs/adr/ADR-020_Board_Geometry_And_Movement_Determinism_v1.0.md`, `Status: Accepted`, mirroring `ADR-019`'s structural format (Решение / Контекст / Термины / normative sections / Не входит / module-boundary compliance / Правила для Codex / Definition of Done / Рассмотренные альтернативы / Трассировка / Нормативное действие).
- New file: `docs/tasks/active/ODY-S03-001_ADR_Board_Geometry_And_Movement_Determinism.md` (task contract, all 18 `TASK_TEMPLATE.md` sections).
- This file (ExecPlan).
- `docs/tasks/SLICE-03_BACKLOG.md` — `ODY-S03-001` row status only (`Draft` → `In Review` now, → `Done` only after CI green and PR review, per the row's own change-control note).

## Tests or validation commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

No `dotnet build`/`dotnet test` required — no production/test code is touched by this task; run only if there is doubt the edit affected anything else.

## Explicit non-goals

- No production code, no unit tests of geometry — future implementation task's job (ADR §11, Definition of Done, is the contract that task must satisfy).
- No edit to `ADR-002`, `ADR-008`, or `ADR-017` — all three are cited as authority, not reopened.
- No `Scene`/`Board`/`SceneObject`/`Token` domain schema content — implementation-task content built atop this ADR's math.
- No Unity rendering-performance decision (`08_Scenes_And_Board` §25.4) — explicitly out of scope per the product document itself.
- No technical spike — `SLICE-03_BACKLOG.md` §3's justification already covers this ADR's content; re-confirmed in ADR §12.5.
