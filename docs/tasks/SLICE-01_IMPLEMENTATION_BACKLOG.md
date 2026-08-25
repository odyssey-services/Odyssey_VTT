# Odyssey VTT — SLICE-01 Local Campaign Implementation Backlog

**Status:** All 8 tasks (`ODY-S01-007`–`014`) merged to `main`; all 8 roadmap §10.6 exit criteria checked with real evidence (see `docs/tasks/active/ODY-S01-014_Traceability_and_Quality_Report.md`). **Technically complete, pending explicit product-owner acceptance** — see section 3.1 below; this backlog is not marked `CLOSED` until that acceptance is recorded.
**Slice:** `SLICE-01 — Local Campaign (vertical slice implementation)`
**Parent task:** `docs/tasks/active/ODY-S01-006_SLICE_01_Implementation_Backlog.md`
**Predecessor backlog:** `docs/tasks/SLICE-01_BACKLOG.md` (prerequisite ADR/spike revision — closed 2026-08-24, historical; not rewritten by this document)
**ExecPlan:** Not required (Brief plan)
**Created:** 2026-08-24
**Last updated:** 2026-08-25 UTC

## 1. Purpose

This backlog converts roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` section 10.3 ("Входит"), section 10.5 (the nine-step vertical slice), and section 10.6 (exit criteria) into small, reviewable implementation tasks. It is the implementation-revision that `docs/tasks/SLICE-01_BACKLOG.md` section 1 reserved for creation once its own (prerequisite) revision closed — which it did on 2026-08-24, with `ADR-011`–`014` all `Accepted` (`ADR-011` amended to v1.1) and the `SP-02` spike report accepted.

This backlog does **not** itself implement anything. It only decomposes the vertical slice into ordered child tasks, each of which will be its own separate task contract and pull request, activated one at a time exactly as `SLICE-00_BACKLOG.md` and `SLICE-01_BACKLOG.md` (prerequisites) were.

Its sources of scope are, exclusively:

- `17_Roadmap_Odyssey_VTT_v0.11.md` §10.3 ("Входит": Campaign Storage, Saving, Backups, Export baseline) — private local reference, not committed to the repository.
- `17_Roadmap_Odyssey_VTT_v0.11.md` §10.5 (the nine-step vertical slice scenario).
- `17_Roadmap_Odyssey_VTT_v0.11.md` §10.6 (exit criteria).
- The already-`Accepted` ADRs governing each area: `ADR-011_Local_Campaign_Format_v1.1.md` (Campaign Storage, Export baseline container), `ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` (Saving, Backups), `ADR-013_Migration_Runner_v1.0.md` (migration registry — see section 2 for this revision's narrowed scope), `ADR-014_Owner_Key_Storage_Baseline_v1.0.md` (explicitly excluded from this revision — see section 2).

## 2. Scope decisions requiring explicit justification

### 2.1 Migration runner (`ADR-013`) — **narrowed to a registry baseline only**, not full runner implementation

Roadmap §10.3's "Входит" list does not name migration runner implementation as part of Stage 2's Campaign Storage, Saving, Backups, or Export baseline groups. The "Backups" group does list "backup перед миграцией" as a trigger to support, but that only requires the snapshot subsystem to expose the trigger (already covered by `ODY-S01-011` below), not the migration runner itself. The nine-step vertical slice (§10.5) creates a campaign, saves state, restarts, and restores from backup — every step operates at a single, unchanging schema version; the slice never opens an old-schema-version campaign, and `05_Persistence_Odyssey_VTT_v0.8.md` §25.2 fixes migration as triggering specifically "при открытии старой версии" — a condition this slice never creates.

Given that, full implementation of `ADR-013`'s transactional pipeline, temp-copy execution, failure/rollback handling, and read-only compatibility mode would be built but never exercised by anything in this revision's own acceptance scenario — untested, unvalidated implementation is worse than no implementation. `ADR-013`'s normative rules were already empirically validated at the spike level by `SP-02` (`docs/tasks/completed/ODY-S01-005_SP-02_Persistence_Reliability_Report.md` §2.4), which is sufficient confidence for a deferred, dedicated future implementation once an actual schema version bump is needed.

**Decision:** this revision includes only a **migration registry baseline** (`ODY-S01-010`): the `SchemaHistory` table, the `DatabaseSchemaVersion` field wiring already specified by `ADR-011`, and a single registered identity migration (`0001_Initial`) with a version and a test proving the registry itself is well-formed and versioned. This satisfies roadmap §10.6's exit criterion "миграции имеют версию и тест" at the level this vertical slice can actually exercise, without claiming a full runner that nothing in this revision would test. Full `ADR-013` runner implementation (temp-copy execution, transactional steps, failure rollback, read-only compatibility mode) is explicitly deferred to a future task, activated only once a real schema version increment is needed.

### 2.2 Owner key storage (`ADR-014`) — **excluded entirely** from this revision

Roadmap §10.3's "Входит" list names `CampaignId`/`CampaignPublicId` explicitly under Campaign Storage, but does not name owner key storage anywhere in Stage 2's scope. The nine-step vertical slice (§10.5) has no step involving authentication, ownership verification, or any multi-machine/multi-user scenario — it is a single local user, single machine, single campaign lifecycle (create → save → restart → reopen → restore). `ADR-014` itself states, in its own §11.3/§12.2, that the UX/application-level behavior when an owner key is absent depends on future Networking/Account ADRs (Stage 3) not yet written, and that this is a deliberate, unresolved open question, not an oversight.

**Decision:** owner key storage implementation is **entirely out of scope** for this revision — not even a stub task is created for it here. It logically belongs with Stage 3 (networking/account), where ownership/authority actually becomes observable behavior. This is a scope exclusion, not a deferred child task of this backlog.

## 3. Slice exit criteria

`SLICE-01` (vertical-slice implementation) is complete only when all of the following, taken verbatim from roadmap §10.6, are proven:

1. Состояние сцены переживает перезапуск (scene state survives restart).
2. Подтверждённая транзакция не теряется (a confirmed transaction is not lost).
3. Неуспешная транзакция не оставляет частичного состояния (a failed transaction leaves no partial state).
4. Backup восстанавливается в отдельную копию (backup restores into a separate copy).
5. Повреждение основной базы не уничтожает последнюю валидную копию (corruption of the main database does not destroy the last valid copy).
6. Миграции имеют версию и тест (migrations have a version and a test) — satisfied at the **registry-baseline** level per section 2.1 above, not by a full runner exercise; this is an explicit, recorded scope narrowing, not a silent reduction of the roadmap's wording.
7. Windows paths не записываются в переносимый формат как обязательная зависимость (Windows paths are not written into the portable format as a mandatory dependency).
8. Закрыт `GATE-A — Architecture Ready` в части локального хранения (Architecture-Ready gate closed for the local-storage portion).

## 3.1 Revision status

All 8 of 8 criteria above are checked against real, re-run evidence as of `ODY-S01-014` (see `docs/tasks/active/ODY-S01-014_Traceability_and_Quality_Report.md` section 1 for the full per-criterion table, and section 5 for the final checklist). No gap was found.

This revision is **technically complete** — all 8 tasks (`ODY-S01-007`–`014`) are merged to `main`, and all 8 exit criteria are Pass. It is **not yet formally closed**: per `ODY-S01-014`'s own task contract, the explicit product-owner acceptance statement (date, confirmation) is deliberately not written here or in the traceability report — it is added by a separate, small, point-fix commit once the product owner explicitly confirms acceptance, mirroring how `docs/tasks/SLICE-01_BACKLOG.md` section 1 recorded the prerequisite revision's closure only after that confirmation happened. Until that commit lands, this backlog's own header `Status` line above reflects "technically complete, pending acceptance," not `CLOSED`.

## 4. Ordered backlog

| Order | Task ID | Group | Title | Status | Depends on | Planning mode | Primary result |
|---:|---|---|---|---|---|---|---|
| 1 | `ODY-S01-007` | Campaign Storage | Campaign Storage Foundation | Done (PR #28 merged) | None | ExecPlan | Create/open a local campaign: `campaign.db` per `ADR-011` §7 PRAGMA profile, `manifest.json`, `CampaignId`/`CampaignPublicId`, campaign settings, base schema tables, application-level repositories |
| 2 | `ODY-S01-008` | Campaign Storage | Scene and Token Minimal Model | In Review (PR #29 open, CI green) | 007 | ExecPlan | One `Scene`, two `SceneObject`/token records, asset manifest entry — the minimal domain model roadmap §10.5 steps 3–5 require |
| 3 | `ODY-S01-009` | Saving | Saving Pipeline | In Review (PR not yet opened) | 007, 008 | ExecPlan | Domain Event Store writes, the single-transaction journal↔projection commit rule (`ADR-012` §5), atomic significant-operation writes, safe application close, crash/unclean-shutdown recovery (WAL replay), open-time integrity check (`05_Persistence` §22) |
| 4 | `ODY-S01-010` | Migration | Migration Registry Baseline | In Review (PR not yet opened) | 007 | Brief plan | `SchemaHistory` table, `DatabaseSchemaVersion` wiring, one registered `0001_Initial` identity migration with a version and a test — **not** the full `ADR-013` runner (see section 2.1) |
| 5 | `ODY-S01-011` | Backups | Backups | In Review (PR not yet opened) | 009 | ExecPlan | Manual backup, backup at test-session start/end, snapshot creation via SQLite Backup API (`ADR-012` §8), recent/daily/weekly rotation baseline, restore-into-separate-copy flow, a corruption test fixture |
| 6 | `ODY-S01-012` | Export baseline | Export Baseline | In Review (PR not yet opened) | 007, 011 | ExecPlan | Initial `.odcamp` container export (`ADR-011`), manifest validation on import, import into a new local copy, explicit absence of automatic merge |
| 7 | `ODY-S01-013` | Integration | Vertical Slice Integration | In Review (PR not yet opened) | 007–012 | Brief plan | The roadmap §10.5 nine-step scenario as an automated, reproducible end-to-end check: create campaign → import one test map → create scene → place two tokens → move them → close application → reopen campaign → verify saved state → restore from backup |
| 8 | `ODY-S01-014` | Gate | SLICE-01 Acceptance and Closure Gate | In Review (PR not yet opened) | 007–013 | Brief plan | Traceability matrix, quality report, all eight roadmap §10.6 exit criteria checked with real evidence, owner acceptance — mirrors `ODY-S00-010`'s closure pattern |

"Planning mode" is intentionally left "Not yet determined" for every child task: each task's own Brief-plan-vs-ExecPlan decision is made when that task's own contract is authored, per `PLANS.md` section 1, not pre-decided by this scaffold — the same convention `SLICE-01_BACKLOG.md` (prerequisites) used.

No `ODY-S01-007`–`014` task contract file exists yet. This backlog only reserves their numbers, titles, and boundaries; each is created and activated as its own separate task, one at a time, when picked up.

## 5. Task boundaries

### ODY-S01-007 — Campaign Storage Foundation

Implements campaign creation and opening per `ADR-011` v1.1: the physical folder structure, `campaign.db` under the exact PRAGMA profile (§7.1), `manifest.json` (§5), `CampaignId` generation, campaign settings storage, the base system-table schema (§8.2), and the application-level repository layer that later tasks build on. Does not implement scenes/tokens (`ODY-S01-008`), the Domain Event Store write path (`ODY-S01-009`), migrations (`ODY-S01-010`), backups (`ODY-S01-011`), or export (`ODY-S01-012`). Uses `Microsoft.Data.Sqlite` + `SQLitePCLRaw.bundle_e_sqlite3 >= 3.0.3` per `ADR-011` v1.1 §1 — the first task in the repository to actually add this as a real production `.csproj` dependency (not spike-scope).

### ODY-S01-008 — Scene and Token Minimal Model

Adds the minimal domain model needed for roadmap §10.5 steps 3–5: one `Scene` record, two `SceneObject`/token records with position fields, and one asset manifest entry (for the imported test map). Does not implement combat, dice, character sheets, content systems, or any gameplay rule beyond position storage — those belong to later slices entirely outside `SLICE-01`.

### ODY-S01-009 — Saving Pipeline

Implements the Domain Event Store write path and the `ADR-012` §5 single-transaction journal↔projection commit rule for the operations `ODY-S01-007`/`008` introduce (campaign creation, scene/token creation, token position changes). Implements safe application close (WAL checkpoint per `ADR-011` §7.4), recovery after an unclean shutdown (WAL replay, empirically proven at the spike level by `SP-02` §2.2), and the open-time integrity check (`05_Persistence` §22 quick check). Does not implement the compensating-event mechanism beyond what these specific operations require, snapshot/backup creation (`ODY-S01-011`), or migration (`ODY-S01-010`).

### ODY-S01-010 — Migration Registry Baseline

Implements only the `SchemaHistory` table, `DatabaseSchemaVersion` field wiring already specified by `ADR-011`, and a single registered `0001_Initial` identity migration with a version and a test proving the registry is well-formed. Does **not** implement the `ADR-013` migration runner's transactional pipeline, temp-copy execution, failure/rollback handling, or read-only compatibility mode — see section 2.1 for the explicit justification for this narrowed scope. A future task, outside this backlog revision, implements the full runner once an actual schema version increment is needed.

### ODY-S01-011 — Backups

Implements manual backup, backup triggers at test-session start/end (`ADR-012` §8.2), snapshot creation via the SQLite Backup API following the exact 8-step flow (`ADR-012` §8.4, empirically proven by `SP-02` §2.3/§2.5), a recent/daily/weekly rotation baseline (`ADR-012` §8.5's default policy, not a fixed contract per that ADR), a restore-into-separate-copy flow, and a corruption test fixture (mirroring `SP-02` §2.6's harness scenario, but as a real product-level test, not spike evidence). Does not implement backup encryption at rest (already closed as "no" by `ADR-014` §8) or the pre-migration snapshot trigger's actual migration-side consumer (that belongs to a future migration-runner task, per `ODY-S01-010`'s narrowed scope).

### ODY-S01-012 — Export Baseline

Implements the initial `.odcamp` container export per `ADR-011` (physical archive format), manifest validation on import, import into a new local copy, and the explicit absence of any automatic merge behavior (roadmap §10.3's "отсутствие автоматического merge"). Depends on `ODY-S01-007` (campaign to export) and `ODY-S01-011` (snapshot mechanism the export baseline reuses per `ADR-011`/`ADR-012`'s existing boundary — export does not reinvent its own copy mechanism). Does not implement owner-key-aware reopening behavior on a new machine (`ADR-014` §11.3, explicitly `[OPEN]`, excluded from this revision per section 2.2).

### ODY-S01-013 — Vertical Slice Integration

Implements the roadmap §10.5 nine-step scenario as a single, automated, reproducible end-to-end check exercising every prior task's deliverable together: create campaign → import one test map → create a scene → place two tokens → change their positions → close the application → reopen the campaign → verify saved state → restore state from backup. Does not introduce new persistence behavior beyond what `ODY-S01-007`–`012` already implement — it is an integration proof, not a new feature.

### ODY-S01-014 — SLICE-01 Acceptance and Closure Gate

Produces a traceability matrix and quality report mirroring `ODY-S00-010`'s pattern, checks all eight roadmap §10.6 exit criteria against real evidence from `ODY-S01-007`–`013`, and records explicit product-owner acceptance closing `SLICE-01`. Does not implement new product behavior — closure/evidence only.

## 6. Dependency rules

- `ODY-S01-007` has no dependency — it is the foundational campaign-creation task every other task in this revision builds on.
- `ODY-S01-008` depends on `ODY-S01-007` (scenes/tokens live inside a campaign that must already exist).
- `ODY-S01-009` depends on `ODY-S01-007` and `ODY-S01-008` (the saving pipeline persists the operations those two tasks introduce).
- `ODY-S01-010` depends on `ODY-S01-007` only (the migration registry attaches to the campaign's schema-version field, not to scene/token or saving-pipeline content).
- `ODY-S01-011` depends on `ODY-S01-009` (backups snapshot a campaign that must already have a working saving pipeline to produce meaningful state to back up).
- `ODY-S01-012` depends on `ODY-S01-007` and `ODY-S01-011` (export packages the campaign and reuses the snapshot mechanism `ODY-S01-011` establishes).
- `ODY-S01-013` depends on all of `ODY-S01-007`–`012` (it is the integration proof exercising every prior deliverable together).
- `ODY-S01-014` depends on all of `ODY-S01-007`–`013` (closure requires every deliverable and the integration proof to already exist).

`ODY-S01-010` (migration registry) has no dependency on `ODY-S01-009`/`011`/`012` and may be activated any time after `ODY-S01-007`, in parallel with the Saving/Backups/Export line if useful — it does not block or get blocked by them.

## 7. Global non-goals

This backlog revision excludes:

- Full `ADR-013` migration runner implementation (transactional pipeline, temp-copy execution, failure/rollback handling, read-only compatibility mode) — see section 2.1. Deferred to a future task outside this revision.
- Owner key storage (`ADR-014`) implementation in any form — see section 2.2. Deferred to Stage 3 (networking/account).
- Networking, session sync, and any multi-client/multi-user scenario — roadmap §10.3 does not include them in Stage 2's scope; they belong to Stage 3.
- Permissions runtime and redaction beyond what `ADR-010` already provides — Stage 3 scope.
- Character sheets, combat, dice, or any content/rules-engine system — roadmap §10.3 does not include them; they belong to later slices entirely.
- Any UI/UX polish beyond what is needed to prove the roadmap §10.5 scenario programmatically — this revision proves the persistence contract works, not a finished user-facing campaign-management screen.
- Backup encryption at rest — already closed as "no" for MVP by `ADR-014` §8; not reopened by this backlog.
- Cross-platform (non-Windows) persistence behavior — `ADR-009`'s Windows-only MVP baseline applies unchanged.

## 8. Backlog change control

- New work requires a new `ODY-S01-0XX` task contract; this document only reserves numbers `ODY-S01-007` through `ODY-S01-014`.
- A task may be split before implementation by updating this backlog (and, if a governing ExecPlan exists for that specific child task, that ExecPlan too), following the same rule `SLICE-00_BACKLOG.md`/`SLICE-01_BACKLOG.md` already use.
- A task may not be merged with unrelated cleanup merely to reduce task count.
- Completed task files move to `docs/tasks/completed/` only after required review, per the established convention in this repository.
- This backlog does not replace any task's own acceptance criteria or any ADR's content; it does not itself decide any technical question beyond the two explicit scope decisions in section 2.
- The predecessor `docs/tasks/SLICE-01_BACKLOG.md` (prerequisite ADR/spike revision) is not rewritten by this document — it remains a closed, historical artifact, per this repository's convention of not retroactively editing completed backlog revisions.
- If this document's section 2 narrowing decisions are later found incorrect (for example, an actual schema migration becomes needed sooner than expected), that is a new task/backlog-revision decision, not a silent edit to this document's already-recorded reasoning — this document would gain an explicit amendment note, not a rewritten section 2.
