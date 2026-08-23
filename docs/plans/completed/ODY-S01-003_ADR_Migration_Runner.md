# ODY-S01-003 — ADR-013 Migration Runner Authored and Proposed

**Status:** Done  
**Owner:** Codex  
**Branch:** `feat/ody-s01-003-adr-migration-runner`  
**Pull request:** Draft — [#24](https://github.com/odyssey-services/Odyssey_VTT/pull/24)  
**Last updated:** 2026-08-20

## 1. Purpose and user-visible outcome

When this plan is complete, `docs/adr/ADR-013_Migration_Runner_v1.0.md` exists as a `Proposed` normative decision defining the database schema migration registry, the migration-run workflow, step transactionality, the migration-failure behavior, `SchemaHistory` schema, the boundary with ruleset migration, and read-only compatibility mode for a newer campaign — giving `ODY-S01-005` (`SP-02` spike) a stable contract for its migration-failure/rollback scenario, and giving the product owner a concrete document to accept, reject, or request changes on.

No implementation code is delivered. The observable outcome is a reviewable ADR proposal, not a running feature.

## 2. Task contract

Governing task: `docs/tasks/active/ODY-S01-003_ADR_Migration_Runner.md`.

- Goal: Author `ADR-013_Migration_Runner_v1.0.md` per that task's section 1.
- Acceptance criteria: That task's section 9, AC-1 through AC-7.
- Requirement IDs: `SLICE-01`, roadmap section 10.2, backlog `ODY-S01-003`.
- In scope: ADR content (migration registry, run workflow, transactionality, failure rule, `SchemaHistory`, ruleset migration boundary, read-only compatibility mode); this ExecPlan; backlog status update.
- Out of scope: Any implementation code; `.odcamp`/`manifest.json` format content; snapshot/journal content; owner key storage content; SQLite provider library selection; concrete future migration list; ruleset migration workflow itself; marking the ADR `Accepted`.
- Required authorities: `05_Persistence_Odyssey_VTT_v0.8.md` sections 6, 25, 26; `ADR-011`, `ADR-012`, `ADR-004`; `docs/tasks/SLICE-01_BACKLOG.md`.
- Required validation commands: `scripts/verify-format.ps1`, `scripts/check-repository-policy.ps1`.

## 3. Current state

### Verified facts

- `ADR-011` and `ADR-012` are both `Accepted` on `main` (merge commit `93bcc38`); `ODY-S01-001`/`002` backlog rows are both `Done`.
- `docs/adr/` contains ADR-001 through ADR-012; `ADR-013` is the next free number.
- No migration-runner implementation code exists anywhere in the repository.

### Assumptions

- None.

## 4. Proposed approach

Author `ADR-013` directly from `05_Persistence` sections 6 (context only), 25, and 26, translating the product document's descriptive rules into binding ADR decisions, in the same structural style as `ADR-011`/`ADR-012` (numbered decision list, context, terms, normative sections, explicit exclusions, open questions, Codex rules, Definition of Done, rejected alternatives, traceability, normative effect). Cross-check every decision against `ADR-011` (version dimensions, `manifest.json` — must not be redefined), `ADR-012` (snapshot contract and its existing pre-migration trigger — must be referenced, not redefined), and `ADR-004` (safe-error contract for migration failure reporting). Elevate the descriptive failure-behavior list from `05_Persistence` §25.4 into a hard normative rule, mirroring how `ADR-012` §5 elevated the transactional-boundary rule. Explicitly draw the boundary against ruleset migration (§25.6) rather than deciding its workflow. Carry forward `ADR-011`/`ADR-012`'s open SQLite-provider-library and backup-encryption questions unresolved, and add one new explicit open question for headless/batch migration confirmation.

No code changes are made; no module ownership or dependency direction changes as a result of this plan.

## 5. Milestones

### M1 — `ADR-013` drafted and internally consistent with its sources

- [x] `docs/adr/ADR-013_Migration_Runner_v1.0.md` created, `Status: Proposed`.
- [x] Content covers migration registry, run workflow, transactionality, failure rule, `SchemaHistory`, ruleset migration boundary, and read-only compatibility mode as binding decisions.
- [x] `.odcamp`/`manifest.json` format, snapshot/journal, owner key storage, and ruleset migration workflow content explicitly excluded with forward references.
- [x] SQLite provider library and backup encryption open questions carried forward, not silently decided.

### M2 — Task/backlog evidence and validation recorded

- [x] `docs/tasks/active/ODY-S01-003_ADR_Migration_Runner.md` completion evidence section drafted.
- [x] `docs/tasks/SLICE-01_BACKLOG.md` `ODY-S01-003` status updated (Draft → In Review, Planning mode `ExecPlan`).
- [x] `scripts/verify-format.ps1` and `scripts/check-repository-policy.ps1` both pass with real recorded results.

### M3 — Draft PR opened for owner review

- [x] Draft PR opened (#24); CI green on all four required checks.
- [x] PR not moved to Ready for Review without separate confirmation (remained Draft throughout).

### M4 — ADR accepted and task closed

- [x] Product owner reviewed and accepted `ADR-013` as-is, no content changes.
- [x] `ADR-013` Status `Proposed` → `Accepted`, with acceptance date recorded in the ADR's own Normative Effect (§17) section.
- [x] Task and this ExecPlan moved to `completed/`; backlog status updated to `Done (ADR-013 Accepted)`.

## 6. Progress log

- 2026-08-20 UTC - Confirmed `ODY-S01-002` closure (PR #23) merged into `main`; verified `ADR-011`/`ADR-012` both `Accepted` and `ODY-S01-001`/`002` backlog rows both `Done` directly against `main` before branching. Read `05_Persistence` sections 25 (Database migrations, §25.1–25.6) and 26 (Newer campaign / read-only mode, §26.1–26.3), plus section 6 for version-dimension context. Authored `docs/adr/ADR-013_Migration_Runner_v1.0.md` covering the migration registry, the 7-step run workflow, step transactionality, the elevated normative failure-behavior rule (temp-copy pattern, rollback, snapshot retention, safe error, version-write block), `SchemaHistory` schema, the ruleset migration boundary with one explicit integration point, and read-only compatibility mode for a newer campaign, with explicit exclusions and inherited open questions. Created the governing task contract and this ExecPlan.
- 2026-08-20 UTC - `verify-format.ps1` and `check-repository-policy.ps1` passed. Updated `docs/tasks/SLICE-01_BACKLOG.md` `ODY-S01-003` row to `In Review (ADR Proposed, pending owner acceptance)`, Planning mode `ExecPlan`. Committed (`d2039812db8c29c13a2fa7fc4a10dfd6613cf4d4`), pushed, Draft PR #24 opened; all four required CI checks passed (`repository-policy-format-structure`, `dotnet-restore-build-test`, `unity-project-package-static`, `buildidentity-provenance`). PR remained Draft.
- 2026-08-20 UTC - Product owner reviewed `ADR-013` and accepted it as-is, with no content changes requested. `ADR-013` Status moved `Proposed` → `Accepted` (content unchanged); acceptance date recorded in the ADR's own Normative Effect section. Task Status moved to `Done`, `docs/tasks/SLICE-01_BACKLOG.md` `ODY-S01-003` row moved to `Done (ADR-013 Accepted)`. This task and its ExecPlan moved to `completed/`.

## 7. Decisions

- 2026-08-20 — Decision: Use `ExecPlan` planning mode. Rationale: `PLANS.md` section 1.2 explicitly names "migration" as a trigger word alongside schema/manifest; this task's entire subject is migration behavior, making it the most direct match of the three `SLICE-01` ADR tasks so far. `PLANS.md` section 1.1 also explicitly disqualifies Brief plan for changes involving migration. Authority: `PLANS.md` section 1.1/1.2, evaluated fresh against this task's content (not presumed by analogy to `ODY-S01-001`/`002`).
- 2026-08-20 — Decision: Elevate `05_Persistence` §25.4's descriptive failure-behavior list into a hard normative rule with equal force to `ADR-012` §5's transactional-boundary rule. Rationale: task instruction explicitly requested this framing "по аналогии с тем, как ADR-012 §5 зафиксировала транзакционную границу," and the underlying risk (half-migrated campaign state) is comparable in severity to a partial journal/projection commit. Authority: `05_Persistence_Odyssey_VTT_v0.8.md` section 25.4; task instruction.
- 2026-08-20 — Decision: Fix exactly one integration point between database schema migration and ruleset migration (shared snapshot mechanism), and decide nothing else about the ruleset migration workflow. Rationale: `05_Persistence` §25.6 clearly separates the two by ownership and workflow; deciding more here would preempt a future ADR/task that Rules Engine/Content Domain has not yet reached. Authority: `05_Persistence_Odyssey_VTT_v0.8.md` section 25.6; task instruction to justify explicitly if fixing an integration point, not to silently decide either way.
- 2026-08-20 — Decision: Accept `ADR-013` as-is, no content changes. Rationale: product owner reviewed the full ADR and found it complete and correct for `ODY-S01-003`'s scope. Authority: product owner ("Владелец продукта принял ADR-013 as-is").

## 8. Discoveries and deviations

- None so far. `05_Persistence` sections 6, 25, 26 were internally consistent with each other and with `ADR-011`/`ADR-012`/`ADR-004`, and did not require reconciling conflicting guidance.

## 9. Validation and acceptance evidence

See the governing task's section 17 (`docs/tasks/completed/ODY-S01-003_ADR_Migration_Runner.md`) for the authoritative record: `verify-format.ps1` and `check-repository-policy.ps1` both passed on the authoring run and on the closure diff; all four required CI checks passed on Draft PR #24.

## 10. Recovery and rollback

Not applicable. This plan produces a documentation-only ADR proposal; no persisted state, migration, or runtime behavior is created. If `ADR-013` is rejected or requires material revision, the plan is updated in place (new decision entry, revised milestones) rather than abandoned, unless the owner directs a full restart.

## 11. Open questions and blockers

- None remaining for this plan. `ADR-013` was reviewed and accepted by the product owner.
- SQLite provider library selection (carried from `ADR-011`/`ADR-012`), backup encryption at rest (carried from `ADR-012`), and headless/batch migration confirmation (new, ADR-013 section 12.3) remain open questions carried by the ADR itself, by design — they are not blockers to this plan's own completion and are not resolved by acceptance.

## 12. Outcome and follow-up

Current outcome: `ADR-013_Migration_Runner_v1.0.md` is `Accepted` (accepted by the product owner as-is on 2026-08-20, no content changes). `ODY-S01-003` task and this ExecPlan are `Done` and moved to `completed/`. `docs/tasks/SLICE-01_BACKLOG.md` `ODY-S01-003` row is `Done (ADR-013 Accepted)`.

Next action: `ODY-S01-004` (ADR: Owner Key Storage Baseline) may proceed independently per `docs/tasks/SLICE-01_BACKLOG.md` section 5 (no dependency on `ODY-S01-001`–`003`). `ODY-S01-005` (`SP-02` spike) depends on `ODY-S01-001` and `ODY-S01-002`, both `Accepted`; per backlog §5 it benefits from at least a working draft of `ODY-S01-003`'s design for its migration-failure scenario, now satisfied since `ADR-013` is `Accepted`.
