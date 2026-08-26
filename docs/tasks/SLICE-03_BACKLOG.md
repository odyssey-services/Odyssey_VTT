# Odyssey VTT — SLICE-03 Playable Foundation Prerequisites Backlog

**Status:** Prerequisite backlog — `ODY-S03-001` Done (`ADR-020` Accepted, merged), `ODY-S03-002` In Review (`ADR-021` Accepted in-file, PR pending owner review)
**Slice:** `SLICE-03 — Playable Foundation (prerequisites)`
**Parent task:** `docs/tasks/active/ODY-S03-000_SLICE_03_Playable_Foundation_Prerequisites.md`
**ExecPlan:** Not required (Brief plan)
**Created:** 2026-08-26
**Last updated:** 2026-08-26 UTC

## 1. Purpose

This backlog converts roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` section 12.2's prerequisite requirements into small, reviewable tasks. It does **not** add product features, and it does **not** cover the `SLICE-03` vertical slice described in roadmap section 12.6 ("Бросок и журнал": player selects own token → sends roll intent → host validates permission → host generates d100 result → modifiers applied → GM can override with reason → only permitted clients receive result → event is persisted → reconnect restores visible log → original event remains after reroll/cancel). That implementation work begins only in a **future backlog revision**, created once all criteria in section 2 below are satisfied.

This revision's only outcome is the ADR(s) needed to close the genuine architectural gaps between `SLICE-02`'s already-accepted foundation (`ADR-002`, `ADR-004`, `ADR-008`, `ADR-012`, `ADR-015`, `ADR-017`, `ADR-019`) and roadmap section 12's Board/Dice/Game-Log scope — not a re-derivation of everything roadmap section 12.2 lists, since most of it is already covered.

## 2. Slice exit criteria (this backlog revision only)

This prerequisite backlog revision is complete only when both of the following are proven:

1. ADR — Board Geometry and Movement Determinism is `Accepted`.
2. ADR — Extended Audience and Selected-Participant Visibility is `Accepted`.

These are **not** the full `SLICE-03` exit criteria (roadmap section 12.7). The full slice exit criteria — "board state одинаков после restart и reconnect," "бросок рассчитывается только host," "roll visibility применяется на сетевой границе," and the rest — apply only once the vertical-slice implementation backlog (a separate future revision, created only after this one closes) is also complete.

**Note on scope, stated explicitly, not silently assumed:** unlike `SLICE-01`'s five persistence ADRs or `SLICE-02`'s five networking ADRs, this revision fixes only **two** new ADRs. This is not an undercount by omission — section 4 below justifies, item by item against roadmap section 12.2's own four prerequisite bullets, why the remaining ground is already covered by `SLICE-00`–`02`'s already-`Accepted` ADRs and needs no new architectural decision, only implementation work each future `SLICE-03` child task will do on its own.

## 3. No technical spike required (stated explicitly, not silently skipped)

Unlike `SLICE-01`'s `SP-02` (SQLite crash/backup measurement) and `SLICE-02`'s `SP-03`/`SP-04` (real internet connectivity; hidden-data-boundary proof), no dedicated technical spike is created by this revision. Reasoning:

- A spike is warranted when a question cannot be resolved by architecture-only reasoning and requires empirical measurement against something uncontrollable or previously unproven — real hardware crash behavior (`SP-02`), real external network conditions (`SP-03`), or proving a **brand-new** security-relevant mechanism is genuinely implementable end-to-end over a real transport before any code exists (`SP-04`, the first time `ADR-017`/`ADR-019`'s redaction pipeline was ever exercised for real).
- Board geometry (grid/hex distance, LOS/cover intersection, deterministic epsilon) is deterministic mathematics, verifiable by ordinary golden-vector unit tests — the same pattern `ADR-008` already established for RNG determinism, not an empirical unknown. `08_Scenes_And_Board` section 13.4/25.1 name these as "implementation ADR" decisions, not measurement-gated ones.
- The extended audience/visibility model (`SelectedParticipants`/`CampaignUserGroup`) is a new *input dimension* added to an *already-proven* mechanism — `ADR-017`/`ADR-019`'s single-authoritative-state-plus-per-connection-filter pipeline was already empirically validated end-to-end by `SP-04` (`ODY-S02-007`) and re-validated independently by `ODY-S02-010`–`013`'s own test suites, over real `InProcessSessionTransport` delivery, without any of those tasks needing a dedicated prerequisite spike of their own. Extending that same proven pipeline with one more audience-selection input carries materially lower risk than `SP-04`'s original "does this architecture work at all" question, and is provable by each future implementation task's own tests (the same convention `ODY-S02-010`–`013` already established), not a standalone prerequisite spike.
- Rendering/UI performance for hex-grid boards (`08_Scenes_And_Board` section 25.4) is explicitly a Unity-presentation-layer tuning concern, not an architecture-blocking unknown — it does not gate any ADR's `Accepted` status.

If a genuine empirical unknown surfaces once ADR content is actually drafted, the ADR child task itself may commission a spike at that point — this revision does not foreclose that, it only records that no such need is visible now.

## 4. Ordered backlog

| Order | Task ID | Title | Status | Depends on | Planning mode | Primary result |
|---:|---|---|---|---|---|---|
| 1 | `ODY-S03-001` | ADR: Board Geometry and Movement Determinism | Done | None | ExecPlan | `docs/adr/ADR-020_Board_Geometry_And_Movement_Determinism_v1.0.md` — `Accepted`. Fixes cross-platform IEEE-754 double arithmetic, exact Square/Hex/None distance formulas, `GeometryEpsilonV1` intersection convention (fail-closed), and `SpatialIndexV1` (uniform spatial hash) — the exact set of decisions `08_Scenes_And_Board` sections 13.4/25.1 name as "implementation ADR," not covered by `ADR-002`/`ADR-017`, which fix command/event and delivery mechanics but not geometry math itself. PR [#55](https://github.com/odyssey-services/Odyssey_VTT/pull/55) merged by product owner (owner review complete) — both this row's change-control conditions (CI green + owner review) now satisfied. |
| 2 | `ODY-S03-002` | ADR: Extended Audience and Selected-Participant Visibility | In Review | None | ExecPlan | `docs/adr/ADR-021_Extended_Audience_And_Selected_Participant_Visibility_v1.0.md` — `Accepted` in-file. Fixes `CampaignUserGroup` as a narrow read-model (not a full lifecycle contract), integrates `SelectedParticipants`/`CampaignUserGroup` as an additional `VisibilityPolicy` input atop `ADR-019` section 7's pipeline, and composes postfactum disclosure/revocation with `ADR-017`'s existing `AddJournalEntry`/`AddEntity`/`RemoveFromProjection` operations — extends `ADR-019` section 10's own explicitly-deferred scope, which the roadmap section 12.2's own fourth prerequisite bullet ("правила visibility броска, включая selected users/groups") names as still open. Draft PR [#56](https://github.com/odyssey-services/Odyssey_VTT/pull/56), CI pending. Status changes to `Done` only after product-owner review, per this row's own change-control rule (same pattern already applied to `ODY-S03-001`). |

## 5. Task boundaries

### ODY-S03-001 — ADR: Board Geometry and Movement Determinism

Fixes: the authoritative coordinate system's deterministic arithmetic guarantees (already partly fixed by `08_Scenes_And_Board` section 6.1's `WorldPosition`/finite-value rules, but not yet an ADR-level cross-platform determinism guarantee); grid/hex distance formula selection and determinism (`08_Scenes_And_Board` section 7.7's `Euclidean`/`ChebyshevDiagonalEqualsOne`/`AlternatingOneTwo`/hex-distance metrics); the geometry epsilon convention for wall/LOS/cover intersection edge cases (section 13.4's explicit "not user-facing campaign data" implementation-ADR flag); and the spatial-index approach for occupancy/obstacle/visibility queries at MVP scale (section 25.1's explicit "конкретная структура — implementation ADR" flag). Builds on `ADR-002` (command/event model — token-move and other board commands are ordinary authoritative commands, not reopened) and `ADR-017` (delivery mechanism — board deltas are ordinary projection deltas, not reopened). Does not redefine `Scene`/`Board`/`SceneObject`/`Token` domain schema itself — that is implementation-task content once this ADR fixes the underlying math each of those schemas relies on. Does not decide Unity rendering optimization (section 25.4) — presentation-layer tuning, not an ADR-level architectural decision.

### ODY-S03-002 — ADR: Extended Audience and Selected-Participant Visibility

Fixes: how a per-artifact (per-roll, per-log-entry, per-fog-audience) explicit audience selection — a stable list of `User`/`CampaignUserGroup` references, distinct from the three `ADR-019` baseline roles — integrates with `ADR-019` section 7's already-accepted single-authoritative-state-plus-per-connection-filter pipeline; whether `CampaignUserGroup` needs its own minimal aggregate (membership, creation/rename/archive) or can be represented more narrowly for this baseline; and how disclosure/revocation of an already-created artifact (a `DiceRoll`'s or `GameLogEntry`'s `VisibilityAudience` changing after the fact — `09_Dice_And_Game_Log` sections 28/33.5) composes with `ADR-017`'s existing `RemoveFromProjection`/`AddEntity`-style delta operations, without inventing a parallel mechanism. Depends on `ADR-019` (extends its explicitly-deferred section 10 scope; does not reopen the three-baseline-role decision itself) and, informationally, on `ADR-017` (reuses its delta operations; does not redefine them). Does not decide search-index implementation details (permission-aware full-text search, `09_Dice_And_Game_Log` section 27) beyond confirming the existing `PERM-INV-012`/`ADR-019` "a safe denial never confirms a hidden entity's existence" principle extends naturally to search — a future implementation task's own scoped concern, not this ADR's content to spell out mechanically.

## 6. Dependency rules

- `ODY-S03-001` has no dependency on any `SLICE-03` prerequisite task — board geometry determinism is orthogonal to the audience/visibility model `ODY-S03-002` fixes. Both may proceed in either order or in parallel.
- `ODY-S03-002` depends on the already-`Accepted` `ADR-019` (extends its section 10 deferred scope) and `ADR-017` (reuses its delta operations) — both already satisfied by `SLICE-02`'s closure; no new dependency on `ODY-S03-001`.
- Neither task depends on `ODY-S02-014` (Real Transport Integration, still `Blocked`) or the `ADR-016` section 14 follow-up spike — per product-owner decision, that remains a separate, later concern, explicitly not reopened by this revision.

## 7. Global non-goals

This backlog revision excludes:

- Any Board/Dice/Game-Log implementation code, Unity UI, or the `SLICE-03` vertical slice itself (roadmap section 12.6) — deferred entirely to a future implementation backlog revision, created only after both ADRs in section 4 above are `Accepted`;
- Any ADR content — each ADR's content is authored in its own child task, one at a time, by a separate future task activation; this backlog only organizes and sequences them, it does not decide any technical question itself;
- Reopening any already-`Accepted` `SLICE-00`/`01`/`02` ADR's own decisions (`ADR-002`, `ADR-004`, `ADR-008`, `ADR-012`, `ADR-015`, `ADR-017`, `ADR-019`) — each is cited as authority, extended where section 5 says so, never redecided;
- Starting `ODY-S02-014` or the `ADR-016` section 14 follow-up spike — explicitly a separate, later product-owner decision per this task's own ТЗ;
- Public release or compatibility promises to end users.

## 8. Backlog change control

- New work requires a new `ODY-S03-XXX` task contract.
- A task may be split before implementation by updating this backlog and, if a governing ExecPlan exists for that specific child task, that ExecPlan too.
- A task may not be merged with unrelated cleanup merely to reduce task count.
- Completed task files move to `docs/tasks/completed/` only after required review.
- This backlog does not replace task acceptance criteria or ADR content; it does not itself decide any technical question.
- The `SLICE-03` implementation backlog (vertical slice) is a separate future backlog revision, created only after both ADRs listed in section 4 are `Accepted`; it is entirely out of scope for this revision.
