# Odyssey VTT — SLICE-01 Local Campaign Prerequisites Backlog

**Status:** Prerequisite backlog — implementation backlog to follow after ADR acceptance
**Slice:** `SLICE-01 — Local Campaign (prerequisites)`
**Parent task:** `docs/tasks/active/ODY-S01-000_SLICE_01_Local_Campaign_Prerequisites.md`
**ExecPlan:** Not required (Brief plan)
**Created:** 2026-08-20
**Last updated:** 2026-08-20 UTC

## 1. Purpose

This backlog converts roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` section 10.2's prerequisite list into small, reviewable tasks. It does **not** add product features, and it does **not** cover the `SLICE-01` vertical-slice implementation described in roadmap section 10.5 (create campaign → import one test map → create a scene → place two tokens → change their positions → close the application → reopen the campaign → verify saved state → restore state from backup). That implementation work begins only in a **future backlog revision**, created after all four ADRs listed below reach `Accepted`.

This revision's only outcome is: four accepted ADRs (Local Campaign Format, Snapshot and Append-Only Journal, Migration Runner, Owner Key Storage Baseline) and a complete, owner-reviewed `SP-02 — Persistence Reliability` technical spike report.

## 2. Slice exit criteria (this backlog revision only)

This prerequisite backlog revision is complete only when all of the following are proven:

1. ADR — Local Campaign Format is `Accepted`.
2. ADR — Snapshot and Append-Only Journal is `Accepted`.
3. ADR — Migration Runner is `Accepted`.
4. ADR — Owner Key Storage Baseline is `Accepted`.
5. `SP-02 — Persistence Reliability` spike report is complete and owner-reviewed.

These are **not** the full `SLICE-01` exit criteria (roadmap section 10.6). The full slice exit criteria — including "state survives restart," "backup restores into a separate copy," and the other roadmap section 10.6 conditions — apply only once the vertical-slice implementation backlog (a separate future revision, created after this one closes) is also complete.

## 3. Ordered backlog

| Order | Task ID | Title | Status | Depends on | Planning mode | Primary result |
|---:|---|---|---|---|---|---|
| 1 | `ODY-S01-001` | ADR: Local Campaign Format | Done (`ADR-011` Accepted) | None | ExecPlan | `.odcamp` container physical structure, `manifest.json` schema and field authority, campaign version dimensions, SQLite runtime profile baseline |
| 2 | `ODY-S01-002` | ADR: Snapshot and Append-Only Journal | Done (`ADR-012` Accepted) | 001 | ExecPlan | Snapshot trigger/creation rules and the append-only Domain Event Store journal contract (ordering, payload hashing, event visibility) |
| 3 | `ODY-S01-003` | ADR: Migration Runner | Draft | 001 | Not yet determined | Schema/ruleset migration registry, execution order, transactionality, failure/rollback behavior, and SchemaHistory recording |
| 4 | `ODY-S01-004` | ADR: Owner Key Storage Baseline | Draft | None | Not yet determined | OS secure-storage mechanism for the campaign owner key, implementing the principle confirmed in `Documentation/21_Security_And_Privacy_Odyssey_VTT_v0.1.md` section 5 |
| 5 | `ODY-S01-005` | Technical Spike SP-02: Persistence Reliability | Draft | 001, 002 | Not yet determined | Report: SQLite WAL/transaction-mode reliability under crash, interrupted backup, migration failure/rollback, snapshot size/speed, and corrupted-database recovery, with selected strategy and measurements |

"Planning mode" is intentionally left "Not yet determined": each child task's Brief-plan-vs-ExecPlan decision is made when that task's own contract is authored, per `PLANS.md` section 1, not pre-decided by this scaffold.

## 4. Task boundaries

### ODY-S01-001 — ADR: Local Campaign Format

Defines the `.odcamp` container's physical folder/file structure, `manifest.json` schema and field authority, campaign version dimensions, and the SQLite runtime profile (WAL mode, single writer, read connections), per `05_Persistence_Odyssey_VTT_v0.8.md` sections 4–9. Does not implement any of this in code, does not select or pin a specific SQLite provider library, and does not define snapshot/journal or migration behavior — those belong to `ODY-S01-002` and `ODY-S01-003`.

### ODY-S01-002 — ADR: Snapshot and Append-Only Journal

Defines snapshot triggers and creation, and the append-only Domain Event Store journal contract — ordering, payload hashing, event visibility — per `05_Persistence_Odyssey_VTT_v0.8.md` sections 11, 12, and 21, and invariants `PE-INV-004`, `PE-INV-005`, and `PE-INV-008`. Does not implement the journal or snapshot writer in code, and does not define migration behavior against this journal — that belongs to `ODY-S01-003`.

### ODY-S01-003 — ADR: Migration Runner

Defines the schema/ruleset migration registry, execution order, transactionality, failure/rollback behavior, and `SchemaHistory` recording, per `05_Persistence_Odyssey_VTT_v0.8.md` section 6 (campaign versions) and section 25 (Database migrations), specifically section 25.6 (Ruleset migration). Does not implement a running migration tool, and does not decide the campaign format or journal shape — those are owned by `ODY-S01-001` and `ODY-S01-002`.

### ODY-S01-004 — ADR: Owner Key Storage Baseline

Defines the concrete OS secure-storage mechanism (platform API, format, rotation, loss-recovery) that implements the principle already confirmed in `Documentation/21_Security_And_Privacy_Odyssey_VTT_v0.1.md` section 5: the owner key never enters the campaign file, `campaign.db`, `.odcamp`, or backup. Does not implement the storage mechanism in code, and does not depend on the campaign format ADR since it concerns OS-level key custody, not campaign file structure.

### ODY-S01-005 — Technical Spike SP-02: Persistence Reliability

Investigates SQLite WAL/transaction-mode behavior, crash during a critical operation, interrupted backup, migration failure and rollback, snapshot size/speed, and corrupted-database recovery, per roadmap section 10.4, producing a report with a selected strategy and measurements. Does not implement production persistence code; the report's findings feed back into `ODY-S01-001`–`003` only if the owner explicitly approves a resulting ADR amendment.

## 5. Dependency rules

- `ODY-S01-001` has no dependency; it is the foundational campaign-format decision. `ODY-S01-002` and `ODY-S01-003` both build on its physical-structure and version-field decisions.
- `ODY-S01-002` depends on `ODY-S01-001` (snapshot/journal placement and versioning build on the campaign physical structure and version dimensions `ODY-S01-001` defines).
- `ODY-S01-003` depends on `ODY-S01-001` (migration targets the schema/version dimensions `ODY-S01-001` defines). It has a practical, non-blocking relationship with `ODY-S01-002` — a migration may need to account for snapshot/journal invalidation — but this backlog does not require `ODY-S01-002` `Accepted` before `ODY-S01-003` begins; the two should be reconciled before either is finalized if their content conflicts.
- `ODY-S01-004` may begin independently of `ODY-S01-001`–`003`; it does not depend on the campaign file format, only on the OS secure-storage principle already confirmed in `Documentation/21_Security_And_Privacy_Odyssey_VTT_v0.1.md` section 5.
- `ODY-S01-005` depends on `ODY-S01-001` and `ODY-S01-002` being `Accepted`, since the spike exercises the chosen format and journal design under failure scenarios. Note: roadmap section 10.4 also lists "migration failure and rollback" as a spike scenario; that specific scenario benefits in practice from at least a working draft of `ODY-S01-003`'s migration runner design, even though this backlog does not make `ODY-S01-003` a hard blocker for `ODY-S01-005` activation. If `ODY-S01-003` is not yet `Accepted` when `ODY-S01-005` begins, the owner should decide whether to defer that one scenario or proceed with a draft design.

## 6. Global non-goals

This backlog revision excludes:

- Persistence implementation code, `.odcamp` physical implementation, SQLite provider library selection or integration, and the migration runner as executable code;
- Scene, token, or campaign-creation UI, networking, and permissions runtime;
- The `SLICE-01` vertical slice itself (roadmap section 10.5: campaign creation, map import, scene, tokens, movement, restart, saved-state verification, backup restore) — deferred entirely to a future implementation backlog revision, created only after all four ADRs in section 3 above are `Accepted`;
- Any ADR content — each ADR's content is authored in its own child task, one at a time, by a separate future task activation; this backlog only organizes and sequences them, it does not decide any technical question itself;
- Public release or compatibility promises to end users.

## 7. Backlog change control

- New work requires a new `ODY-S01-XXX` task contract.
- A task may be split before implementation by updating this backlog and, if a governing ExecPlan exists for that specific child task, that ExecPlan too.
- A task may not be merged with unrelated cleanup merely to reduce task count.
- Completed task files move to `docs/tasks/completed/` only after required review.
- This backlog does not replace task acceptance criteria or ADR content; it does not itself decide any technical question.
- The `SLICE-01` implementation backlog (vertical slice) is a separate future backlog revision, created only after all four ADRs listed in section 3 are `Accepted`; it is entirely out of scope for this revision.
