# ODY-S01-003 — ADR: Migration Runner

**Status:** Done  
**Roadmap stage / slice:** SLICE-01 (prerequisites)  
**Owner:** Codex (agent)  
**Requested by:** Product owner  
**Branch:** `feat/ody-s01-003-adr-migration-runner`  
**Pull request:** Draft — [#24](https://github.com/odyssey-services/Odyssey_VTT/pull/24)  
**ExecPlan:** `docs/plans/completed/ODY-S01-003_ADR_Migration_Runner.md`  
**Created:** 2026-08-20  
**Last updated:** 2026-08-20 UTC

## 1. Goal

Produce an `Accepted`-ready ADR (`ADR-013_Migration_Runner_v1.0.md`) that normatively defines the database schema migration registry, the migration-run workflow (confirmation, pre-migration snapshot, temp-copy execution, integrity validation), step transactionality, the migration-failure behavior, `SchemaHistory` schema, the boundary with ruleset migration, and read-only compatibility mode for a campaign newer than the application — consistent with `05_Persistence_Odyssey_VTT_v0.8.md`, `ADR-011`, and `ADR-012`.

## 2. Why this task exists

- Problem or dependency being addressed: `ADR-011` and `ADR-012` both intentionally excluded migration workflow content (`ADR-011` §11, `ADR-012` §11). Without this ADR, no binding normative source exists for `PE`-level migration safety (temp-copy execution, failure rollback, read-only newer-schema handling), risking an implementation that leaves a campaign in a half-migrated state after a failure.
- Value or risk reduction: fixes the temp-copy-until-proven-successful pattern and the hard failure-behavior rule before any migration code exists, and draws an explicit boundary against conflating database schema migration with ruleset migration (a materially different, product-facing workflow).
- Blocking or enabling relationship: `ODY-S01-005` (`SP-02` spike) benefits from at least a working draft of this ADR for its "migration failure and rollback" spike scenario per `SLICE-01_BACKLOG.md` §5, though it is not a hard blocker for `ODY-S01-005` activation.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1.2
- `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md` (Accepted) — version dimensions, `manifest.json`
- `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` (Accepted) — snapshot contract, §8.2 pre-migration trigger, §12.1/§12.2 open questions
- `docs/adr/ADR-004_Result_and_Error_Model_v1.0.md` §1 — typed `Error`/safe error contract for migration failure reporting
- `05_Persistence_Odyssey_VTT_v0.8.md` §6 (campaign versions, context only), §25 (Database migrations), §26 (Newer campaign / read-only mode) — private local reference, not committed to the repository

### Requirement and test IDs

- Requirement IDs: None (ADR-only task; no formal requirement ID registry entry exists yet for this contract)
- Existing test IDs: None
- New test IDs to introduce: None (this task produces no code)

### Task-safe private context

- Approved summary / references: `05_Persistence_Odyssey_VTT_v0.8.md` §6, §25, §26 summarized into ADR-013 without pasting private document text verbatim beyond short normative phrases already customary in this repository's ADRs (see `ADR-011`/`ADR-012` for precedent). No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md` and `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` both carry `**Статус:** Accepted` on `main` at commit `93bcc38`, confirmed by `grep` before branching.
- `docs/tasks/SLICE-01_BACKLOG.md` rows for `ODY-S01-001` and `ODY-S01-002` both read `Done` on `main`, confirmed by `Read`.
- `docs/adr/` contains ADR-001 through ADR-012; `ADR-013` is the next unused number, confirmed by directory listing.
- No `docs/tasks/active/ODY-S01-003_*` or `docs/adr/ADR-013_*` file existed on `main` prior to this task.
- `SLICE-01_BACKLOG.md` §4 already defines this task's boundary text (sources: `05_Persistence` §6, §25) and §5 confirms the dependency on `ODY-S01-001` (satisfied) and the non-blocking, reconcile-if-conflicting relationship with `ODY-S01-002` (also now `Accepted`, so no unresolved conflict risk remains).

### Assumptions

- None. All facts above were directly observed via `Read`/`grep`/directory listing on the current `main` branch before branching for this task.

## 5. Scope

### In scope

- `docs/adr/ADR-013_Migration_Runner_v1.0.md` (new): migration registry, migration-run workflow, step transactionality, migration-failure normative rule, `SchemaHistory` schema, boundary with ruleset migration, read-only compatibility mode for a newer campaign, explicit boundaries with `ADR-011`/`ADR-012` and inherited open questions.
- `docs/tasks/active/ODY-S01-003_ADR_Migration_Runner.md` (this file).
- `docs/plans/active/ODY-S01-003_ADR_Migration_Runner.md` (governing ExecPlan, see §14).
- `docs/tasks/SLICE-01_BACKLOG.md` §3 — update only the `ODY-S01-003` row (Status, Planning mode).

### Out of scope

- Any implementation code (C#, SQL, Unity) for the migration runner, temp-copy execution, or read-only compatibility adapter.
- `.odcamp`/`manifest.json` physical format content (`ADR-011`).
- Snapshot/journal contract content (`ADR-012`).
- Owner key storage mechanism content (`ODY-S01-004`).
- SQLite provider library selection (remains `[OPEN]`, inherited from `ADR-011`/`ADR-012`).
- The concrete future migration list (`0001_Initial`, `0002_...`, ...) — implementation content, not an ADR decision.
- The ruleset migration workflow itself (preview, confirmation, application, Rules Engine/Content Domain events) — only the single integration point (shared snapshot mechanism) is fixed by this ADR; the workflow is out of scope.
- Any change to `ADR-011`/`ADR-012` content or status.
- Any change to `ODY-S01-004`/`005` rows in `SLICE-01_BACKLOG.md`.
- Any change under `docs/tasks/completed/`, `docs/plans/completed/`, `ODY-S00-*`, or `Documentation/`.

### Allowed paths

```text
docs/adr/ADR-013_Migration_Runner_v1.0.md
docs/tasks/active/ODY-S01-003_ADR_Migration_Runner.md
docs/plans/active/ODY-S01-003_ADR_Migration_Runner.md
docs/tasks/SLICE-01_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: Not applicable (documentation-only; no code). Content must remain consistent with `ADR-001` (Persistence owns migration mechanics; ruleset migration belongs to Rules Engine/Content Domain, a different module).
- Authoritative-state and transaction boundary: this ADR's primary subject — migration-step transactionality (§25.3) and the temp-copy-until-proven-successful pattern (§25.4 `[IMPLEMENTATION]`).
- Serialization / compatibility boundary: `manifest.json`/`SchemaHistory` updates referenced must remain consistent with `ADR-003` canonical-codec principle; this ADR does not itself define new wire formats.
- Time / RNG rule: Not applicable.
- Unity / thread / lifetime rule: Not applicable.
- Dependency / licensing rule: Not applicable (no new dependency introduced).
- Security / privacy / redaction rule: migration failure reporting must use a safe error (`ADR-004`), not raw exception detail, per §25.4.
- Performance or platform constraint: Not applicable (no numeric performance targets fixed by this ADR; measurement deferred to `ODY-S01-005`).
- Other: must not silently close `ADR-011` §12.1 / `ADR-012` §12.1/§12.2 open questions; must not conflate database schema migration with ruleset migration (§25.6).

## 7. Expected behavior

This is a documentation contract task; "behavior" is expressed as required normative content rather than runtime scenarios.

### Required invariants

- ADR-013 states the migration registry's strict order, immutable identifier, `FromVersion`/`ToVersion`, checksum, transaction capability, and validation step per migration.
- ADR-013 states the 7-step migration-run workflow (version display, user confirmation, pre-migration snapshot, ordered execution, integrity validation, `SchemaHistory`/`manifest.json` update, campaign open) as normative, referencing `ADR-012`'s existing snapshot trigger without redefining it.
- ADR-013 states step transactionality and that the chain is successful only after the final validation, not after the last individual step.
- ADR-013 states, as a hard normative rule (not a description), the temp-copy execution pattern and the full failure-behavior list: step rollback, working database not replaced, pre-migration snapshot retained, safe error + diagnostic log to the user, version write blocked.
- ADR-013 states the complete `SchemaHistory` schema with all required fields.
- ADR-013 states that `RulesetVersion` change is not automatically a database schema migration, and defines the single explicit integration point (shared snapshot mechanism) without deciding the ruleset migration workflow itself.
- ADR-013 states the newer-campaign read-only compatibility mode behavior: write always forbidden, safe read-only attempt, no silent ignoring of unknown required structures, explicit available/unavailable capability lists, and the downgrade prohibition.
- ADR-013 does not redefine `.odcamp`/`manifest.json` format or the snapshot/journal contract.
- ADR-013 does not decide owner key storage, SQLite provider library selection, or the concrete future migration list.

## 8. Deliverables

- Production code: None
- Tests: None
- Scripts / CI: None
- Configuration: None
- Documentation: `docs/adr/ADR-013_Migration_Runner_v1.0.md`, this task contract, the governing ExecPlan, and the `ODY-S01-003` row update in `docs/tasks/SLICE-01_BACKLOG.md`.
- Generated evidence or build artifacts: validation command output recorded in §17.
- Migration / recovery material: None (this ADR describes but does not implement the migration runner)

## 9. Acceptance criteria

1. `docs/adr/ADR-013_Migration_Runner_v1.0.md` exists with `**Статус:** Proposed` and contains all required normative content listed in §7's invariants.
2. The ADR does not redefine any content already decided by `ADR-011` (physical format, version dimensions) or `ADR-012` (snapshot definition/trigger set/creation flow) — verified by review against both.
3. The ADR does not decide owner key storage, SQLite provider library selection (remains explicitly `[OPEN]`), or the concrete future migration list — verified by review of ADR-013 §11–12.
4. `docs/tasks/SLICE-01_BACKLOG.md` §3 shows the `ODY-S01-003` row updated to a non-`Done` status with a determined Planning mode, and rows for `ODY-S01-004`–`005` are byte-for-byte unchanged.
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

- Cross-read ADR-013 against `05_Persistence_Odyssey_VTT_v0.8.md` §6, §25, §26 to confirm no contradiction and no silent narrowing of the source document's invariants.
- Cross-read ADR-013 against `ADR-011`/`ADR-012` to confirm no redefinition of physical format, version dimensions, or snapshot contract.
- Cross-read ADR-013 against `ADR-004` to confirm the migration-failure user-facing error is expressed as a safe/typed error, not a raw exception.

### Required environments / profiles

- OS / architecture: Not applicable (documentation-only)
- Unity editor or Player profile: Not applicable
- Scripting backend: Not applicable
- Network topology or database fixture: Not applicable
- Other: None

### Validation not required by this task

- Build, EditMode/PlayMode tests, or Player smoke: not required — no code is touched by this task, matching the precedent set by `ODY-S01-001`/`ODY-S01-002`.

## 11. Compatibility, migration, and rollback

Not applicable. This task produces a `Proposed` ADR and its task contract; it does not itself change any persisted format, schema, contract, protocol, package, or deployable artifact. Compatibility impact is assessed and recorded only when this ADR's content is implemented in a future task.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: none directly; the ADR discusses the *design* of migration failure reporting (safe error, diagnostic log) without embedding actual campaign or diagnostic content.
- Trust boundaries: Persistence (migration runner) vs. Rules Engine/Content Domain (ruleset migration) — this ADR reaffirms the existing module boundary from `ADR-001`, does not move it.
- Authorization / audience checks: Not applicable to this documentation task.
- Redaction requirements: Not applicable — no event/log redaction content introduced by this ADR beyond referencing `ADR-004`'s safe-error contract for migration failure.
- Log-safe fields: ADR-013 §7.2 requires a diagnostic log and a safe error on failure, consistent with `ADR-004`/`ADR-010` (Logging, Diagnostics and Redaction) intent, though `ADR-010` is not independently re-verified in this task beyond the safe-error framing.
- Abuse / malformed input limits: Not applicable.
- Security tests: None (no code).

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: Per `PLANS.md` §1.2, an ExecPlan is required when a task "introduces or changes an Application port, public DTO, event, command, schema, protocol, manifest, package, build profile, **or migration**" and when a task "affects **authoritative state, persistence**... or diagnostics." This task is a direct, literal match to the "migration" trigger word itself — the strongest possible match among the three ADR tasks completed so far in `SLICE-01` (`ADR-011` matched via "schema"/"manifest", `ADR-012` matched via "schema"/"authoritative state, persistence"; this task matches "migration" verbatim, in addition to introducing the `SchemaHistory` schema and affecting authoritative-state safety during migration failure). A Brief plan is disqualified: `PLANS.md` §1.1 requires that a Brief-eligible change "does not change a public contract, persisted format, protocol, permissions, redaction, dependency graph, ... or migration" — this task's entire subject is migration behavior, disqualifying it outright. ExecPlan mode is therefore independently required by the same rule, evaluated fresh against this task's actual content, not presumed by analogy to `ODY-S01-001`/`002`.
- ExecPlan path: `docs/plans/completed/ODY-S01-003_ADR_Migration_Runner.md`
- Expected pull request count: 1 (single Draft PR covering ADR authoring; a second PR will later record owner acceptance and status/backlog closure, mirroring the `ODY-S01-001`/`ODY-S01-002` pattern).
- Milestone or sequencing constraints: Must not begin before `ODY-S01-002`'s closure (PR #23) is merged into `main` — verified in §4. Has a practical, non-blocking relationship with `ODY-S01-002` per `SLICE-01_BACKLOG.md` §5; since both `ADR-011` and `ADR-012` are now `Accepted`, no unresolved reconciliation risk remains at the time this task begins.

## 15. Documentation and versioning impact

- Documents that must change: `docs/adr/ADR-013_Migration_Runner_v1.0.md` (new), `docs/tasks/SLICE-01_BACKLOG.md` (`ODY-S01-003` row only).
- Documents that must not change: `ADR-011`, `ADR-012`, `ODY-S01-001`/`002` task/ExecPlan (already `completed/`), `ODY-S01-004`/`005` backlog rows, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything under `Documentation/`.
- Application version change: No — this task does not touch `Odyssey.*` code or `BuildIdentity`.
- Schema / format / contract / protocol / ruleset version change: None yet — ADR-013 is `Proposed`, not implemented; no schema is created in code by this task.
- Documentation version changes: ADR-013 is created at v1.0, `Proposed`. No other document's version changes.
- Changelog or release-note requirement: None — pre-implementation ADR, consistent with the `ADR-011`/`ADR-012` precedent.

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

- `docs/adr/ADR-013_Migration_Runner_v1.0.md` — new ADR, authored at `Proposed`, reviewed and accepted by product owner as-is, Status moved to `Accepted` with acceptance recorded in §17 Нормативное действие (date 2026-08-20, no content changes).
- `docs/tasks/active/ODY-S01-003_ADR_Migration_Runner.md` (this file) — moved to `docs/tasks/completed/` as part of formal closure.
- `docs/plans/active/ODY-S01-003_ADR_Migration_Runner.md` — governing ExecPlan, moved to `docs/plans/completed/` with final progress-log entry recorded.
- `docs/tasks/SLICE-01_BACKLOG.md` — `ODY-S01-003` row updated `In Review (ADR Proposed, pending owner acceptance)` → `Done (ADR-013 Accepted)`.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed` (authoring PR #24, 2026-08-20) |
| `.\scripts\check-repository-policy.ps1` | Passed | All `REPO-POLICY-00x`/`TC-CI-0xx` checks passed, `Repository policy check passed.` (authoring PR #24, 2026-08-20) |
| `.\scripts\verify-format.ps1` (closure) | Passed | Re-run for closure diff — see closure PR evidence |
| `.\scripts\check-repository-policy.ps1` (closure) | Passed | Re-run for closure diff — see closure PR evidence |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `ADR-013` created with `Status: Proposed`, all normative content per §7 present — confirmed by review during authoring PR #24. |
| AC-2 | Passed | ADR-013 reviewed against `ADR-011`/`ADR-012`; §11 explicitly confirms no redefinition of physical format, version dimensions, or snapshot contract. |
| AC-3 | Passed | ADR-013 §11 explicitly excludes owner key storage, concrete future migration list, ruleset migration workflow; §12 carries forward the SQLite-provider and backup-encryption questions as `[OPEN]`, not silently decided. |
| AC-4 | Passed | `SLICE-01_BACKLOG.md` `ODY-S01-003` row updated to `Done (ADR-013 Accepted)`; rows for `ODY-S01-004`–`005` unchanged, confirmed via diff-scope check. |
| AC-5 | Passed | `verify-format.ps1` and `check-repository-policy.ps1` both passed (authoring and closure runs). |
| AC-6 | Passed | `git diff --name-status` against `main` limited to `ADR-013`, task/plan files (`active`→`completed` move), and `SLICE-01_BACKLOG.md`. |
| AC-7 | Passed | Draft PR #24 opened, all 4 required CI checks green (`repository-policy-format-structure`, `dotnet-restore-build-test`, `unity-project-package-static`, `buildidentity-provenance`); remained Draft through formal closure — not moved to Ready without separate confirmation. |

## 18. Blockers, risks, and open decisions

- Blocker (resolved): task could not begin until `ODY-S01-002`'s closure PR (#23) was merged into `main`. Verified directly against `main` before branching (§4).
- Open decision (deliberate, not a blocker): ADR-013 §12 carries forward `ADR-011`/`ADR-012`'s `[OPEN]` SQLite provider-library and backup-encryption-at-rest questions unresolved, and adds one new open question (headless/batch migration confirmation mechanism, §12.3). All three are intentional non-decisions, not omissions.
- Risk: none identified beyond the standard risk that the owner may request content changes during review before `Accepted`, matching the `ODY-S01-001`/`ODY-S01-002` precedent (both accepted as-is).
- Closure (2026-08-20): Product owner reviewed `ADR-013` and accepted it as-is, no content changes requested. `ADR-013` Status `Proposed` → `Accepted`; acceptance recorded in the ADR's own §17 Нормативное действие. Task Status moved to `Done`, moved to `docs/tasks/completed/`. This ExecPlan moved to `docs/plans/completed/`. `docs/tasks/SLICE-01_BACKLOG.md` `ODY-S01-003` row moved to `Done (ADR-013 Accepted)`.
