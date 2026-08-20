# ODY-S01-002 — ADR: Snapshot and Append-Only Journal

**Status:** Done  
**Roadmap stage / slice:** SLICE-01 (prerequisites)  
**Owner:** Codex (agent)  
**Requested by:** Product owner  
**Branch:** `feat/ody-s01-002-adr-snapshot-journal`  
**Pull request:** Draft — [#23](https://github.com/odyssey-services/Odyssey_VTT/pull/23)  
**ExecPlan:** `docs/plans/completed/ODY-S01-002_ADR_Snapshot_And_Journal.md`  
**Created:** 2026-08-20  
**Last updated:** 2026-08-20 UTC

## 1. Goal

Produce an `Accepted`-ready ADR (`ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md`) that normatively defines the append-only Domain Event Store contract (event ordering, integrity marker, visibility/redaction boundary), the transactional boundary between journal and projection, the compensating-event correction mechanism, command-level idempotency, and the snapshot contract (definition, triggers, creation flow), consistent with `05_Persistence_Odyssey_VTT_v0.8.md` and `ADR-011`.

## 2. Why this task exists

- Problem or dependency being addressed: `ADR-011` intentionally excluded journal and snapshot content (`ADR-011` §11). Without this ADR, `SLICE-01` implementation tasks would have no binding normative source for `PE-INV-004`/`PE-INV-005`/`PE-INV-008`, risking an incomplete or inconsistent transactional pipeline implementation.
- Value or risk reduction: fixes the authoritative event-ordering rule, the single-transaction journal/projection commit rule, and the compensating-correction mechanism before any code exists, preventing an implementation that silently violates append-only or transactional invariants.
- Blocking or enabling relationship: blocks `ODY-S01-005` (`SP-02` spike, which depends on `ODY-S01-001` and `ODY-S01-002` both `Accepted`); has a practical, non-blocking relationship with `ODY-S01-003` per `SLICE-01_BACKLOG.md` §5.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1.2
- `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md` (Accepted) — physical structure, version dimensions, SQLite runtime profile
- `docs/adr/ADR-004_Result_and_Error_Model_v1.0.md` §1 — typed `Error`/`ErrorCode` contract for command rejection
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md` §6.5 — Persistence module ownership
- `05_Persistence_Odyssey_VTT_v0.8.md` §3 (`PE-INV-004`, `PE-INV-005`, `PE-INV-008`), §10–12 (commands, transactional pipeline, Domain Event Store), §20–21 (Snapshots, Backups) — private local reference, not committed to the repository

### Requirement and test IDs

- Requirement IDs: None (ADR-only task; no formal requirement ID registry entry exists yet for this contract)
- Existing test IDs: None
- New test IDs to introduce: None (this task produces no code)

### Task-safe private context

- Approved summary / references: `05_Persistence_Odyssey_VTT_v0.8.md` §3, §10–12, §20–21 summarized into ADR-012 without pasting private document text verbatim beyond short normative phrases already customary in this repository's ADRs (see `ADR-011` for precedent). No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md` exists on `main` at commit `292d908` with `**Статус:** Accepted`, confirmed by `Read` before branching.
- `docs/tasks/SLICE-01_BACKLOG.md` row for `ODY-S01-001` reads `Done (`ADR-011` Accepted)` on `main`, confirmed by `Read`.
- `docs/adr/` contains ADR-001 through ADR-011; `ADR-012` is the next unused number, confirmed by directory listing.
- No `docs/tasks/active/ODY-S01-002_*` or `docs/adr/ADR-012_*` file existed on `main` prior to this task.
- `SLICE-01_BACKLOG.md` §4 already defines this task's boundary text (sources: `05_Persistence` §11, §12, §21 and invariants `PE-INV-004`/`005`/`008`) and §5 confirms the dependency on `ODY-S01-001`, now satisfied.

### Assumptions

- None. All facts above were directly observed via `Read`/directory listing on the current `main` branch before branching for this task.

## 5. Scope

### In scope

- `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` (new): Domain Event Store contract, transactional journal↔projection boundary, compensating-event mechanism, command idempotency, snapshot contract, backup-as-disaster-recovery boundary, explicit boundary with `ADR-011` and future `ODY-S01-003`/`004`.
- `docs/tasks/active/ODY-S01-002_ADR_Snapshot_And_Journal.md` (this file).
- `docs/plans/active/ODY-S01-002_ADR_Snapshot_And_Journal.md` (governing ExecPlan, see §14).
- `docs/tasks/SLICE-01_BACKLOG.md` §3 — update only the `ODY-S01-002` row (Status, Planning mode).

### Out of scope

- Any implementation code (C#, SQL, Unity) for the journal, snapshot writer, or backup mechanism.
- Migration runner workflow content (`ODY-S01-003`).
- Owner key storage mechanism content (`ODY-S01-004`).
- SQLite provider library selection (remains `[OPEN]`, inherited from `ADR-011` §12.1).
- Any change to `ADR-011`'s content or status.
- Any change to `ODY-S01-003`, `ODY-S01-004`, `ODY-S01-005` rows in `SLICE-01_BACKLOG.md`.
- Any change under `docs/tasks/completed/`, `docs/plans/completed/`, `ODY-S00-*`, or `Documentation/`.

### Allowed paths

```text
docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md
docs/tasks/active/ODY-S01-002_ADR_Snapshot_And_Journal.md
docs/plans/active/ODY-S01-002_ADR_Snapshot_And_Journal.md
docs/tasks/SLICE-01_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: Not applicable (documentation-only; no code). Content must remain consistent with `ADR-001` §6.5 (Persistence owns event/journal/snapshot mechanics, does not own game rule invariants).
- Authoritative-state and transaction boundary: this is the primary subject of the ADR — `EventSequence` as sole order, single-transaction journal/projection commit (`PE-INV-005`).
- Serialization / compatibility boundary: any JSON structure referenced (event payload, backup manifest) must remain consistent with `ADR-003` canonical-codec principle; this ADR does not itself define new wire formats.
- Time / RNG rule: explicitly excludes timestamp from being an ordering source (§4.1 of ADR-012) — no RNG concern.
- Unity / thread / lifetime rule: Not applicable.
- Dependency / licensing rule: Not applicable (no new dependency introduced).
- Security / privacy / redaction rule: event visibility/redaction boundary (§4.4 of ADR-012) must remain consistent with Networking-layer redaction ownership; no PII/secret content in the ADR itself.
- Performance or platform constraint: Not applicable (no numeric performance targets fixed by this ADR; measurement deferred to `ODY-S01-005`).
- Other: must not silently close `ADR-011` §12.1's `[OPEN]` SQLite-provider question (see ADR-012 §11).

## 7. Expected behavior

This is a documentation contract task; "behavior" is expressed as required normative content rather than runtime scenarios.

### Required invariants

- ADR-012 states `EventSequence` (not timestamp) as the sole authoritative event order.
- ADR-012 states the append-only rule for `DomainEvents` with exactly one named exception (archival of sealed session events), and states that even then history is not logically edited.
- ADR-012 states `PayloadHash` is an integrity marker, explicitly not a cryptographic signature.
- ADR-012 states redaction happens only at the Networking projection layer, never by deletion from the Event Store.
- ADR-012 states, as a normative (not descriptive) rule, that projection + `DomainEvents` + `GameLog`/`CalculationTrace` + outbox + `AppliedCommand` result commit in one database transaction.
- ADR-012 states the compensating-event mechanism (`OriginalEvent → CompensatingCommand → CompensatingEvent`) as the only legitimate correction path, with no physical delete/update.
- ADR-012 states command idempotency via `AppliedCommands`, with exactly-once effect over at-least-once delivery.
- ADR-012 states the snapshot definition (full `campaign.db` copy via SQLite Backup API), the full set of triggers from `05_Persistence` §20.2, the "no snapshot if unchanged" rule, the required counters, and the 8-step creation flow, including the explicit prohibition on copying an open database with unconfirmed WAL state.
- ADR-012 states the backup-is-not-a-journal-substitute rule (`PE-INV-008`).
- ADR-012 does not redefine `ADR-011`'s physical `Backups/` structure or introduce a new version dimension.
- ADR-012 does not describe the migration workflow itself, and does not decide owner key storage or SQLite provider library selection.

## 8. Deliverables

- Production code: None
- Tests: None
- Scripts / CI: None
- Configuration: None
- Documentation: `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md`, this task contract, the governing ExecPlan, and the `ODY-S01-002` row update in `docs/tasks/SLICE-01_BACKLOG.md`.
- Generated evidence or build artifacts: validation command output recorded in §17.
- Migration / recovery material: None (this ADR describes but does not implement snapshot/migration mechanisms)

## 9. Acceptance criteria

1. `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` exists with `**Статус:** Proposed` and contains all required normative content listed in §7's invariants.
2. The ADR does not redefine any content already decided by `ADR-011` (physical `Backups/` structure, version dimensions) — verified by review against `ADR-011`.
3. The ADR does not decide owner key storage, SQLite provider library selection (remains explicitly `[OPEN]`), or migration workflow — verified by review of ADR-012 §11–12.
4. `docs/tasks/SLICE-01_BACKLOG.md` §3 shows the `ODY-S01-002` row updated to a non-`Done` status with a determined Planning mode, and rows for `ODY-S01-003`–`005` are byte-for-byte unchanged.
5. `.\scripts\verify-format.ps1` and `.\scripts\check-repository-policy.ps1` both pass.
6. `git diff --name-status` against `main` shows only the four files listed in §5's Allowed paths.
7. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| None | — | Documentation-only task; no code paths exist to test | — |

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- Cross-read ADR-012 against `05_Persistence_Odyssey_VTT_v0.8.md` §3, §10–12, §20–21 to confirm no contradiction and no silent narrowing of the source document's invariants.
- Cross-read ADR-012 against `ADR-011` to confirm no redefinition of physical structure or version dimensions.
- Cross-read ADR-012 against `ADR-004` to confirm any command-rejection contract (e.g. `RevisionConflict`) is expressed as a typed `Error`, not a raw exception.

### Required environments / profiles

- OS / architecture: Not applicable (documentation-only)
- Unity editor or Player profile: Not applicable
- Scripting backend: Not applicable
- Network topology or database fixture: Not applicable
- Other: None

### Validation not required by this task

- Build, EditMode/PlayMode tests, or Player smoke: not required — no code is touched by this task, matching the precedent set by `ODY-S01-001` (`ADR-011`).

## 11. Compatibility, migration, and rollback

Not applicable. This task produces a `Proposed` ADR and its task contract; it does not itself change any persisted format, schema, contract, protocol, package, or deployable artifact. Compatibility impact is assessed and recorded only when this ADR's content is implemented in a future task.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: none directly; the ADR discusses the *design* of event visibility/redaction, referencing `VisibilityClass` conceptually, without embedding actual hidden campaign content.
- Trust boundaries: Persistence (Event Store) vs. Networking (projection/redaction) — this ADR reaffirms the existing boundary from `05_Persistence` and `ADR-001`, does not move it.
- Authorization / audience checks: Not applicable to this documentation task.
- Redaction requirements: ADR-012 §4.4 states redaction is a Networking-layer concern, never a Persistence-layer deletion.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: None (no code).

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: Per `PLANS.md` §1.2, an ExecPlan is required when a task "introduces or changes an Application port, public DTO, event, command, **schema**, protocol, **manifest**, package, build profile, or migration" and when a task "affects **authoritative state, persistence**... hidden information, redaction... or diagnostics." This task fixes the normative shape of the `DomainEvents` table (event ordering, integrity marker, visibility boundary), the `AppliedCommands` idempotency schema, and the `BackupRecord` schema, and it defines the binding transactional-commit rule for all authoritative game state going forward. It is a strictly stronger match to these triggers than `ODY-S01-001` was (`ADR-011` touched the *file-format* schema; this task touches the *authoritative-state persistence* schema and transaction boundary directly, which is named explicitly in the trigger list). A Brief plan is disqualified: `PLANS.md` §1.1 requires that a Brief-eligible change "does not change a public contract, persisted format, protocol, permissions, redaction, dependency graph" — this task changes several of those by definition. ExecPlan mode is therefore not presumed by analogy to `ODY-S01-001`; it is independently required by the same rule, evaluated fresh against this task's actual content.
- ExecPlan path: `docs/plans/completed/ODY-S01-002_ADR_Snapshot_And_Journal.md`
- Expected pull request count: 1 (single Draft PR covering ADR authoring; a second PR will later record owner acceptance and status/backlog closure, mirroring the `ODY-S01-001` pattern).
- Milestone or sequencing constraints: Must not begin before `ODY-S01-001`'s closure (PR #22) is merged into `main` — verified in §4. Blocks `ODY-S01-005` (`SP-02`) until `Accepted`.

## 15. Documentation and versioning impact

- Documents that must change: `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` (new), `docs/tasks/SLICE-01_BACKLOG.md` (`ODY-S01-002` row only).
- Documents that must not change: `ADR-011`, `ODY-S01-001` task/ExecPlan (already `completed/`), `ODY-S01-003`–`005` backlog rows, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything under `Documentation/`.
- Application version change: No — this task does not touch `Odyssey.*` code or `BuildIdentity`.
- Schema / format / contract / protocol / ruleset version change: None yet — ADR-012 is `Proposed`, not implemented; no schema is created in code by this task.
- Documentation version changes: ADR-012 is created at v1.0, `Proposed`. No other document's version changes.
- Changelog or release-note requirement: None — pre-implementation ADR, consistent with the `ADR-011` precedent.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass. (None applicable — documentation-only.)
- [x] Required manual checks are completed.
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable. (Not applicable — see §11.)
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [x] Pull request explains changes, evidence, limitations, and follow-up work.
- [x] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` — new ADR, authored at `Proposed`, reviewed and accepted by product owner as-is, Status moved to `Accepted` with acceptance recorded in §17 Нормативное действие (date 2026-08-20, no content changes).
- `docs/tasks/active/ODY-S01-002_ADR_Snapshot_And_Journal.md` (this file) — moved to `docs/tasks/completed/` as part of formal closure.
- `docs/plans/active/ODY-S01-002_ADR_Snapshot_And_Journal.md` — governing ExecPlan, moved to `docs/plans/completed/` with final progress-log entry recorded.
- `docs/tasks/SLICE-01_BACKLOG.md` — `ODY-S01-002` row updated `In Review (ADR Proposed, pending owner acceptance)` → `Done (ADR-012 Accepted)`.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed` (authoring PR #23, 2026-08-20) |
| `.\scripts\check-repository-policy.ps1` | Passed | All `REPO-POLICY-00x`/`TC-CI-0xx` checks passed, `Repository policy check passed.` (authoring PR #23, 2026-08-20) |
| `.\scripts\verify-format.ps1` (closure) | Passed | Re-run for closure diff — see closure PR evidence |
| `.\scripts\check-repository-policy.ps1` (closure) | Passed | Re-run for closure diff — see closure PR evidence |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `ADR-012` created with `Status: Proposed`, all normative content per §7 present — confirmed by review during authoring PR #23. |
| AC-2 | Passed | ADR-012 reviewed against `ADR-011`; §10 explicitly confirms no redefinition of physical `Backups/` structure or version dimensions. |
| AC-3 | Passed | ADR-012 §11 explicitly excludes owner key storage, migration workflow; §12 carries forward the SQLite provider question as `[OPEN]`, not silently decided. |
| AC-4 | Passed | `SLICE-01_BACKLOG.md` `ODY-S01-002` row updated to `Done (ADR-012 Accepted)`; rows for `ODY-S01-003`–`005` unchanged, confirmed via diff-scope check. |
| AC-5 | Passed | `verify-format.ps1` and `check-repository-policy.ps1` both passed (authoring and closure runs). |
| AC-6 | Passed | `git diff --name-status` against `main` limited to `ADR-012`, task/plan files (`active`→`completed` move), and `SLICE-01_BACKLOG.md`. |
| AC-7 | Passed | Draft PR #23 opened, all 4 required CI checks green (`repository-policy-format-structure`, `dotnet-restore-build-test`, `unity-project-package-static`, `buildidentity-provenance`); remained Draft through formal closure — not moved to Ready without separate confirmation. |

## 18. Blockers, risks, and open decisions

- Blocker (resolved): task could not begin until `ODY-S01-001`'s closure PR (#22) was merged into `main`. Owner confirmed merge on 2026-08-20; verified directly against `main` before branching.
- Open decision (deliberate, not a blocker): ADR-012 §12 carries forward `ADR-011` §12.1's `[OPEN]` SQLite provider-library question unresolved, and adds one new open question (backup encryption at rest, deferred to `ODY-S01-004`). Both are intentional non-decisions, not omissions.
- Risk: none identified beyond the standard risk that the owner may request content changes during review before `Accepted`, matching the `ODY-S01-001` precedent (which was accepted as-is).
- Closure (2026-08-20): Product owner reviewed `ADR-012` and accepted it as-is, no content changes requested. `ADR-012` Status `Proposed` → `Accepted`; acceptance recorded in the ADR's own §17 Нормативное действие. Task Status moved to `Done`, moved to `docs/tasks/completed/`. This ExecPlan moved to `docs/plans/completed/`. `docs/tasks/SLICE-01_BACKLOG.md` `ODY-S01-002` row moved to `Done (ADR-012 Accepted)`.
