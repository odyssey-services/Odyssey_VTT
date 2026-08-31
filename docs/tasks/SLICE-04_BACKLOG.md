# Odyssey VTT — SLICE-04 Characters and Progression Prerequisites Backlog

**Status:** Prerequisite backlog — OPEN. Four ADRs (`ADR-022` through `ADR-025`) are required before `SLICE-04` vertical-slice implementation may begin.
**Slice:** `SLICE-04 — Characters and Progression (prerequisites)`
**Parent task:** `docs/tasks/active/ODY-S04-000_SLICE_04_Prerequisites.md`
**ExecPlan:** Not required (Brief plan)
**Created:** 2026-08-30
**Last updated:** 2026-08-29 UTC

## 1. Purpose

This backlog converts roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` section 13's `SLICE-04 — Персонаж и развитие` prerequisite question into small, reviewable ADR tasks. It does **not** add Character/Progression implementation, and it does **not** cover the `SLICE-04` vertical slice itself: local draft creation, template selection, host submit validation, GM approval, Active character publication, development grants, immediate valid purchases, critical evidence, skill 5+ recommendation resolution, reconnect/history proof, and `.odchar` import/export.

The blocking product documents named by roadmap section 13 already exist locally and are substantial: `04_Odyssey_Rules_Engine_Odyssey_VTT_v0.6.md` and `10_Characters_And_Progression_Odyssey_VTT_v0.2.md`. This prerequisite revision therefore does not write those product specifications again. Its only job is to decide which remaining architectural decisions must be accepted before implementation decomposition starts.

The earlier trial UI effort was renamed to `SLICE-UI-01`, so there is no naming collision with this official roadmap `SLICE-04`.

## 2. Slice exit criteria (this backlog revision only)

This prerequisite backlog revision is complete only when all of the following are proven:

1. ADR — Character Aggregate, Section Revisions, and History Projection is `Accepted`.
2. ADR — Character Drafts, Templates, and Approval Workflow is `Accepted`.
3. ADR — Development Economy and Progression Transactions is `Accepted`.
4. ADR — Character Ownership, Lifecycle, and Ruleset Migration Operations is `Accepted`.

These are **not** the full `SLICE-04` exit criteria from roadmap section 13.9. The full slice exit criteria — including no `CharacterLevel`, independent template copies, duplicate purchase idempotency, single-use critical evidence, owner assignment audit, preserved Archive/Dead history, `.odchar` import into a new Draft, and failed Ruleset migration rollback — apply only after the later implementation backlog and its child tasks are complete.

## 3. Scope decisions requiring explicit justification

### 3.1 Character data model and versioning require ADR-022

Roadmap section 13.3 and the Character/Progression specification already name `CharacterKind`, lifecycle states, identity/presentation/custom fields, section revisions and locks, and Character History projection. Existing ADRs cover the general substrate: `ADR-002` covers command/event/idempotency flow, `ADR-003` covers versioned DTOs/current-state migration rules, `ADR-012` covers append-only journal and snapshots, and `ADR-013` covers database schema migration runner behavior.

Those ADRs do not decide the Character aggregate boundary itself: whether section revisions live inside one aggregate or split into separately versioned records, which section locks exist at the authoritative boundary, what the minimum historical snapshot is for Character events, and how `CharacterHistoryProjection` remains a projection rather than a second source of truth. Therefore this requires a new ADR.

### 3.2 Drafts and templates require ADR-023

The product specification fixes local Drafts, `PersonalCharacterTemplate`, `CampaignCharacterTemplate`, compatibility validation, submit/review/approve, review comments, and independent copy/no live binding. `ADR-002` can carry the commands once the model is known, and `ADR-003` can serialize the resulting contracts, but neither decides the architectural split between local profile storage and campaign-authoritative state, nor the template compatibility boundary that turns local data into a campaign Character.

Therefore drafts/templates/approval need their own ADR before implementation tasks start.

### 3.3 Development economy requires ADR-024

The product specification fixes `DevelopmentPool`, `DevelopmentTransaction`, immediate valid purchases after host validation, reservations only for pending operations, critical evidence, skill 5+ recommendation, compensating revert, and full respec. `ADR-002` already supplies CommandId idempotency, durable outcomes, event batches, and compensation; `ADR-012` supplies append-only event storage. That is necessary but not sufficient: the slice still needs one accepted decision for the economy ledger boundary, duplicate spend prevention, one-transaction Character/DevelopmentPool/history updates, evidence single-use, and respec/revert compensation shape.

Therefore the development economy is a new ADR, not just ordinary implementation.

### 3.4 Ability, resources, and anatomy are covered without a new prerequisite ADR

Roadmap section 13.6 asks for ability instances/sources/rank modes, typed resources/recovery rules, and anatomy profile snapshot with individual modifications. The product specification and Rules Engine already resolve the architectural fork named in the task: effective values are computed, definitions are separate from instances, `CharacterAbility` stores source/rank state, `CharacterResource` stores current/base/effective maximum with typed recovery rules, and `CharacterAnatomy` is a snapshot of an anatomy profile plus individual modifications. `ADR-003` covers versioned serialized contracts and `ADR-001`/`04_Odyssey_Rules_Engine` keep UI from owning calculations.

No new prerequisite ADR is visible here. Implementation tasks must still build these mechanics and tests, but the snapshot-vs-derived and definition-vs-instance questions are already answered by the product specs and existing ADR substrate.

### 3.5 Ownership and lifecycle operations require ADR-025

Roadmap section 13.7 asks for direct MainGM primary owner assignment with audit, archive/dependency-aware delete, authoritative Dead, `CharacterRestored`, and Ruleset migration. `ADR-019` deliberately fixed only the earlier baseline roles and a simplified character-assignment/control subset; it explicitly deferred the fuller ownership/control model. `ADR-002`, `ADR-012`, and `ADR-013` cover generic commands, append-only events, compensation, backup/snapshot, and database migration runner behavior, but they do not decide Character-specific owner/co-owner/controller semantics, dependency-aware physical delete gates, Dead/restore invariants, or the relationship between database migration and Character Ruleset migration.

Therefore ownership/lifecycle/ruleset-migration operations require a new ADR.

## 4. Ordered backlog

| Order | Task ID | Title | Status | Depends on | Planning mode | Primary result |
|---:|---|---|---|---|---|---|
| 1 | `ODY-S04-001` | ADR: Character Aggregate, Section Revisions, and History Projection | Done | None | ExecPlan | Accepted `ADR-022` (`docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md`). Fixes Character aggregate boundary, section revisions/locks, lifecycle field ownership, minimum event snapshots, and `CharacterHistoryProjection` as a derived projection, reusing `ADR-002`/`003`/`012`/`013` rather than redefining them. |
| 2 | `ODY-S04-002` | ADR: Character Drafts, Templates, and Approval Workflow | Draft | 001 | ExecPlan expected | Future `ADR-023`. Fixes local Draft vs campaign-authoritative Character boundary, personal/campaign template lifecycle, compatibility validation, submit/review/comment/approve flow, and independent-copy/no-live-binding semantics. |
| 3 | `ODY-S04-003` | ADR: Development Economy and Progression Transactions | Draft | 001 | ExecPlan expected | Future `ADR-024`. Fixes `DevelopmentPool` ledger/transaction boundaries, immediate valid purchase, host revalidation, idempotent duplicate handling, critical evidence single-use, skill 5+ recommendation resolution, advancement revert, and respec compensation. |
| 4 | `ODY-S04-004` | ADR: Character Ownership, Lifecycle, and Ruleset Migration Operations | Draft | 001, 002, 003 | ExecPlan expected | Future `ADR-025`. Fixes direct MainGM primary-owner assignment with reason/audit, co-owner/controller boundaries against `ADR-019`, archive/dependency-aware delete, authoritative Dead/restore, and Character Ruleset migration preview/snapshot/rollback. |

Each child task will create its own task contract and decide its own Brief-plan-vs-ExecPlan mode when activated. ExecPlan is expected because each ADR changes future public contracts, persistence/permissions/versioning behavior, or authoritative lifecycle semantics, but this backlog does not pre-author the child plans.

## 5. No technical spike required

No prerequisite technical spike is created by this revision. The remaining questions are architectural/product-model decisions, not empirical measurements against an unknown environment. This differs from `SLICE-01`'s persistence reliability spike and `SLICE-02`'s internet/hidden-data spikes:

- Character storage and history use the already-proven local SQLite/journal/migration substrate from `ADR-011` through `ADR-013`.
- Command idempotency, compensation, and replay behavior use `ADR-002` and `ADR-012`; the Character ADRs must specialize them, not empirically prove that the substrate works.
- Ability/resource/anatomy snapshot rules are already fixed by the product specification and Rules Engine contract.
- Real internet transport remains blocked under the earlier `ODY-S02-014`/`ADR-016` condition and is not reopened by this slice.

If an ADR child task discovers an empirical unknown that cannot be resolved by the existing evidence, that child task may propose a spike then. This parent backlog does not hide such a possibility; it only records that none is visible before ADR authoring starts.

## 6. Dependency rules

- `ODY-S04-001` has no prerequisite child task because the Character aggregate boundary underpins every later decision.
- `ODY-S04-002` depends on `ODY-S04-001` because Draft approval creates a campaign Character and must know the target aggregate/revision/history boundary.
- `ODY-S04-003` depends on `ODY-S04-001` because progression mutates Character mechanics and Character history atomically.
- `ODY-S04-004` depends on `ODY-S04-001` for lifecycle/identity/history, on `ODY-S04-002` for approval/template lifecycle interaction, and on `ODY-S04-003` for respec/progression compensation and Ruleset migration interactions.
- No task in this backlog depends on `ODY-S02-014` or the real internet spike. That blocked work remains separate.

## 7. Global non-goals

This prerequisite backlog excludes:

- any production code, tests, persistence schema, Unity UI, rules resolver, export/import implementation, or character sheet layout;
- implementation decomposition into `ODY-S04-XXX` feature tasks after ADR acceptance;
- contentless content such as concrete skill catalogs, class catalogs, ability catalogs, or balanced ruleset tables;
- real internet transport work, Unity Gaming Services setup, or second-network validation;
- creating ADR files in this task;
- reopening `SLICE-UI-01` or the old trial-UI naming collision.

## 8. Backlog change control

- New work requires a new `ODY-S04-XXX` task contract.
- A task may be split before implementation by updating this backlog and, if a governing ExecPlan exists for that child task, that ExecPlan too.
- A task may not be merged with unrelated cleanup merely to reduce task count.
- Completed task files move to `docs/tasks/completed/` only after required review.
- This backlog does not replace child task acceptance criteria or ADR content; it only organizes prerequisite decisions.
- The `SLICE-04` implementation backlog is a separate future backlog revision, created only after all four ADRs listed above are `Accepted`.
