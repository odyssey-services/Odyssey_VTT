# ODY-S01-002 — ADR-012 Snapshot and Append-Only Journal Authored and Proposed

**Status:** In Progress  
**Owner:** Codex  
**Branch:** `feat/ody-s01-002-adr-snapshot-journal`  
**Pull request:** Not yet opened  
**Last updated:** 2026-08-20

## 1. Purpose and user-visible outcome

When this plan is complete, `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` exists as a `Proposed` normative decision defining the append-only Domain Event Store contract, the journal↔projection transactional boundary, the compensating-event correction mechanism, command-level idempotency, and the snapshot contract — giving `ODY-S01-003` (Migration Runner) and `ODY-S01-005` (`SP-02` spike) a stable foundation, and giving the product owner a concrete document to accept, reject, or request changes on.

No implementation code is delivered. The observable outcome is a reviewable ADR proposal, not a running feature.

## 2. Task contract

Governing task: `docs/tasks/active/ODY-S01-002_ADR_Snapshot_And_Journal.md`.

- Goal: Author `ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` per that task's section 1.
- Acceptance criteria: That task's section 9, AC-1 through AC-7.
- Requirement IDs: `SLICE-01`, roadmap section 10.2, backlog `ODY-S01-002`.
- In scope: ADR content (event ordering, append-only rule, integrity marker, visibility/redaction boundary, transactional commit rule, compensating mechanism, command idempotency, snapshot contract); this ExecPlan; backlog status update.
- Out of scope: Any implementation code; migration runner workflow content; owner key storage content; SQLite provider library selection; marking the ADR `Accepted`.
- Required authorities: `05_Persistence_Odyssey_VTT_v0.8.md` sections 3, 10–12, 20–21; `ADR-011`, `ADR-004`, `ADR-001`; `docs/tasks/SLICE-01_BACKLOG.md`.
- Required validation commands: `scripts/verify-format.ps1`, `scripts/check-repository-policy.ps1`.

## 3. Current state

### Verified facts

- `ADR-011` is `Accepted` on `main` (merge commit `292d908`); `ODY-S01-001` backlog row is `Done`.
- `docs/adr/` contains ADR-001 through ADR-011; `ADR-012` is the next free number.
- No journal/snapshot implementation code exists anywhere in the repository.

### Assumptions

- None.

## 4. Proposed approach

Author `ADR-012` directly from `05_Persistence` sections 3, 10–12, 20–21, translating the product document's descriptive rules into binding ADR decisions, in the same structural style as `ADR-011` (numbered decision list, context, terms, normative sections, explicit exclusions, open questions, Codex rules, Definition of Done, rejected alternatives, traceability, normative effect). Cross-check every decision against `ADR-011` (physical structure, version dimensions — must not be redefined), `ADR-004` (typed `Error` contract for command rejection), and `ADR-001` (Persistence module ownership) so the new ADR does not silently conflict with already-accepted authorities. Explicitly carve out and forward-reference `ODY-S01-003` (migration workflow) and `ODY-S01-004` (owner key storage) rather than deciding their content here. Carry forward `ADR-011`'s open SQLite-provider-library question unresolved rather than silently closing it. Decide explicitly, with recorded justification, not to fix exact backup-rotation counts as a binding ADR number, since the source document already treats them as a configurable baseline.

No code changes are made; no module ownership or dependency direction changes as a result of this plan.

## 5. Milestones

### M1 — `ADR-012` drafted and internally consistent with its sources

- [x] `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` created, `Status: Proposed`.
- [x] Content covers Domain Event Store contract, transactional journal↔projection boundary, compensating mechanism, command idempotency, and snapshot contract as binding decisions.
- [x] Migration runner and owner key storage content explicitly excluded with forward references.
- [x] SQLite provider library choice carried forward as an open question, not silently decided.

### M2 — Task/backlog evidence and validation recorded

- [x] `docs/tasks/active/ODY-S01-002_ADR_Snapshot_And_Journal.md` completion evidence section drafted.
- [ ] `docs/tasks/SLICE-01_BACKLOG.md` `ODY-S01-002` status updated (Draft → In Review, Planning mode `ExecPlan`).
- [ ] `scripts/verify-format.ps1` and `scripts/check-repository-policy.ps1` both pass with real recorded results.

### M3 — Draft PR opened for owner review

- [ ] Draft PR opened; CI green on all required checks.
- [ ] PR not moved to Ready for Review without separate confirmation.

### M4 — ADR accepted and task closed (future, separate confirmation required)

- [ ] Product owner reviews and decides on `ADR-012` (accept as-is, or requests changes).
- [ ] If accepted: Status `Proposed` → `Accepted`; task and this ExecPlan moved to `completed/`; backlog status updated to `Done`.

## 6. Progress log

- 2026-08-20 UTC - Confirmed `ODY-S01-001` closure (PR #22) merged into `main` per owner confirmation ("Мердж провел - идем дальше"); re-verified `ADR-011` `Accepted` and backlog row `Done` directly against `main` before branching. Read `05_Persistence` sections 10–12 (commands/idempotency, transactional pipeline, Domain Event Store) and 20–21 (Snapshots, Backups), and `ADR-004` section 1 (Result/Error model) for compatibility. Authored `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` covering event ordering, append-only rule with its single archival exception, `PayloadHash` integrity marker, event visibility/redaction boundary, the normative single-transaction commit rule, the compensating-event mechanism, command idempotency, and the full snapshot contract, with explicit exclusions for `ODY-S01-003`/`004` content and the SQLite-provider-library open question carried forward unresolved. Created the governing task contract and this ExecPlan.

## 7. Decisions

- 2026-08-20 — Decision: Use `ExecPlan` planning mode. Rationale: `PLANS.md` section 1.2 requires an ExecPlan when a task "introduces or changes ... a schema" and when it "affects authoritative state, persistence ... hidden information, redaction." This ADR fixes the normative shape of the `DomainEvents`, `AppliedCommands`, and `BackupRecord` schemas and the binding transactional-commit rule for all authoritative game state — a strictly stronger match to these triggers than `ADR-011`'s file-format schema. Authority: `PLANS.md` section 1.2, evaluated fresh against this task's content (not presumed by analogy to `ODY-S01-001`).
- 2026-08-20 — Decision: Do not fix exact backup rotation counts (10 fast / 7 daily / 4 weekly) as a binding ADR-012 number. Rationale: `05_Persistence` section 21.3 already describes these as a "Стандарт" whose "Политика может быть изменена в CampaignSettings" — the source document itself treats them as a configurable product default, not a fixed technical contract. Fixing them here would incorrectly imply that changing a default value requires an ADR amendment. The binding technical contracts of this ADR are the Fast/Full backup composition and the `BackupRecord` schema, not the specific retention counts. Authority: `05_Persistence_Odyssey_VTT_v0.8.md` section 21.3, reasoned explicitly rather than silently deciding either way per the task's instruction.
- 2026-08-20 — Decision: Carry forward `ADR-011` section 12.1's open SQLite-provider-library question unresolved, and do not treat SQLite Backup API usage as requiring that decision. Rationale: SQLite Backup API is part of the standard SQLite C API surface, available regardless of which .NET provider wrapper is eventually chosen; nothing in the snapshot contract depends on resolving that choice here. Authority: `ADR-011` section 12.1; task instruction not to silently close this question.

## 8. Discoveries and deviations

- None so far. `05_Persistence` sections 3, 10–12, 20–21 were internally consistent with each other and with `ADR-011`/`ADR-004`, and did not require reconciling conflicting guidance.

## 9. Validation and acceptance evidence

To be filled with real command output before this plan's M2/M3 milestones are checked off (see the governing task's section 17 for the authoritative record; this section will not duplicate it beyond a pointer once validation runs).

## 10. Recovery and rollback

Not applicable. This plan produces a documentation-only ADR proposal; no persisted state, migration, or runtime behavior is created. If `ADR-012` is rejected or requires material revision, the plan is updated in place (new decision entry, revised milestones) rather than abandoned, unless the owner directs a full restart.

## 11. Open questions and blockers

- No blockers remaining for this plan's M1 milestone.
- SQLite provider library selection (carried from `ADR-011` section 12.1) and backup encryption at rest (new, ADR-012 section 12.1) remain open questions carried by the ADR itself, by design — not blockers to this plan's completion.

## 12. Outcome and follow-up

Current outcome: `ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` drafted at `Proposed`. Task contract and this ExecPlan created. Backlog update, validation, commit, push, and Draft PR remain to be executed (M2–M3).

Next action: run `verify-format.ps1` and `check-repository-policy.ps1`, update the `ODY-S01-002` backlog row, perform the diff-scope check, commit, push, and open a Draft PR — mirroring the `ODY-S01-001` workflow.
