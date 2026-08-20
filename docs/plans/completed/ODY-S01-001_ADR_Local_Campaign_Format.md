# ODY-S01-001 — ADR-011 Local Campaign Format Authored and Proposed

**Status:** Done  
**Owner:** Codex  
**Branch:** `feat/ody-s01-001-adr-local-campaign-format`  
**Pull request:** Draft — [#22](https://github.com/odyssey-services/Odyssey_VTT/pull/22)  
**Last updated:** 2026-08-20

## 1. Purpose and user-visible outcome

When this plan is complete, `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md` exists as a `Proposed` normative decision defining the local campaign's physical structure, `manifest.json` schema, independent version dimensions, SQLite runtime profile, base data-schema principle, and domain identifiers — giving `ODY-S01-002` (Snapshot and Append-Only Journal) and `ODY-S01-003` (Migration Runner) a stable foundation to build on, and giving the product owner a concrete document to accept, reject, or request changes on.

No implementation code is delivered. The observable outcome is a reviewable ADR proposal, not a running feature.

## 2. Task contract

Governing task: `docs/tasks/completed/ODY-S01-001_ADR_Local_Campaign_Format.md`.

- Goal: Author `ADR-011_Local_Campaign_Format_v1.0.md` per that task's section 1.
- Acceptance criteria: That task's section 9, AC-1 through AC-8.
- Requirement IDs: `SLICE-01`, roadmap section 10.2, backlog `ODY-S01-001`.
- In scope: ADR content (physical structure, manifest, versions, SQLite profile, base schema principle, identifiers); this ExecPlan; backlog status update.
- Out of scope: Any implementation code; snapshot/journal, migration runner, and owner key storage ADR content; pinning a SQLite provider library; marking the ADR `Accepted`.
- Required authorities: `05_Persistence_Odyssey_VTT_v0.8.md` sections 3–9; `ADR-001`, `ADR-003`, `ADR-007`; `docs/tasks/SLICE-01_BACKLOG.md`.
- Required validation commands: `scripts/verify-format.ps1`, `scripts/check-repository-policy.ps1`.

## 3. Current state

### Verified facts

- `SLICE-00`/`M1` is closed (merge commit `7fbc9b0b7af242e6400538baf35a419536805872`).
- `ODY-S01-000` (parent task) and `docs/tasks/SLICE-01_BACKLOG.md` are merged to `main` via PR #21.
- `docs/adr/` contains ADR-001 through ADR-010 (with superseding minor versions for ADR-003, ADR-009, ADR-010); `ADR-011` is the next free number.
- No campaign-format implementation code exists anywhere in the repository.

### Assumptions

- None.

## 4. Proposed approach

Author `ADR-011` directly from `05_Persistence` sections 3–9, translating product-document description into binding ADR decisions, in the structural style of `ADR-007`/`ADR-010` (numbered decision list, context, terms, normative sections, explicit exclusions, open questions, Codex rules, Definition of Done, rejected alternatives, traceability, normative effect). Cross-check every decision against `ADR-001` module boundaries, `ADR-003` serialization baseline, and `ADR-007` version-independence rules so the new ADR does not silently conflict with already-accepted authorities. Explicitly carve out and forward-reference the three adjacent ADRs (`ODY-S01-002`/`003`/`004`) rather than deciding their content here. Leave the SQLite provider library choice as an explicit open question rather than a silent decision, since `SP-02` (`ODY-S01-005`) is designed to produce the reliability evidence that should inform it.

No code changes are made; no module ownership, transaction boundary, or dependency direction changes as a result of this plan.

## 5. Milestones

### M1 — `ADR-011` drafted and internally consistent with its sources

- [x] `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md` created, `Status: Proposed`.
- [x] Content covers physical structure, `manifest.json`, version dimensions, SQLite runtime profile, base schema principle, and identifiers as binding decisions.
- [x] Snapshot/journal, migration runner, and owner key storage content explicitly excluded with forward references.
- [x] SQLite provider library choice recorded as an open question, not decided.

### M2 — Task/backlog evidence and validation recorded

- [x] `docs/tasks/active/ODY-S01-001_ADR_Local_Campaign_Format.md` completion evidence filled honestly.
- [x] `docs/tasks/SLICE-01_BACKLOG.md` `ODY-S01-001` status updated to reflect the real state at each stage (Draft → In Review while `Proposed` → Done once `Accepted`).
- [x] `scripts/verify-format.ps1` and `scripts/check-repository-policy.ps1` both pass with real recorded results.

### M3 — Draft PR opened for owner review

- [x] Draft PR opened (#22); CI green on all four required checks.
- [x] PR not moved to Ready for Review without separate confirmation (remained Draft throughout).

### M4 — ADR accepted and task closed

- [x] Product owner reviewed and accepted `ADR-011` as-is, no content changes.
- [x] `ADR-011` Status `Proposed` → `Accepted`, with acceptance date recorded in the ADR's own Normative Effect section.
- [x] Task and this ExecPlan moved to `completed/`; backlog status updated to `Done`.

## 6. Progress log

- 2026-08-20 UTC - Read `05_Persistence` sections 3–9, `ADR-001` module boundaries, and relevant `ADR-003`/`ADR-007` sections. Confirmed `ADR-011` is the next free ADR number. Authored `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md` covering physical structure, `manifest.json`, version dimensions, SQLite runtime profile, base schema principle, and identifiers, with explicit exclusions for `ODY-S01-002`/`003`/`004` content and an explicit open question for the SQLite provider library choice and `CampaignPublicId`. Created this task contract and ExecPlan.
- 2026-08-20 UTC - `verify-format.ps1` and `check-repository-policy.ps1` passed. Draft PR #22 opened; all four required CI checks passed (`repository-policy-format-structure`, `dotnet-restore-build-test`, `unity-project-package-static`, `buildidentity-provenance`). PR remained Draft.
- 2026-08-20 UTC - Product owner reviewed `ADR-011` and accepted it as-is, with no content changes requested. `ADR-011` Status moved `Proposed` → `Accepted` (content unchanged); acceptance date recorded in the ADR's own Normative Effect section. Task Status moved to `Done`, `docs/tasks/SLICE-01_BACKLOG.md` `ODY-S01-001` row moved to `Done`. This task and its ExecPlan moved to `completed/`.

## 7. Decisions

- 2026-08-20 — Decision: Use `ExecPlan` planning mode. Rationale: `PLANS.md` section 1.2 requires an ExecPlan when a task "introduces or changes ... a schema ... manifest"; this ADR introduces the `manifest.json` schema and base SQLite schema principle, matching that trigger, consistent with `ADR-003`'s original task (`ODY-S00-007`) also using ExecPlan. Authority: `PLANS.md` section 1.2.
- 2026-08-20 — Decision: Do not pin a specific SQLite provider library in `ADR-011`. Rationale: `05_Persistence` section 7 only specifies the PRAGMA/behavioral profile, not a .NET library; `SP-02` (`ODY-S01-005`) exists specifically to produce reliability evidence (crash, corrupted-db recovery, migration rollback) that should inform this choice. Authority: product owner instruction in the `ODY-S01-001` activation ТЗ.
- 2026-08-20 — Decision: Accept `ADR-011` as-is, no content changes. Rationale: product owner reviewed the full ADR and found it complete and correct for `ODY-S01-001`'s scope. Authority: product owner ("Владелец продукта принял ADR-011 as-is").

## 8. Discoveries and deviations

- None so far. `05_Persistence` sections 3–9 were internally consistent and did not require reconciling conflicting guidance.

## 9. Validation and acceptance evidence

To be filled with real command output before this plan's M2/M3 milestones are checked off (see the governing task's section 17 for the authoritative record; this section will not duplicate it beyond a pointer once validation runs).

## 10. Recovery and rollback

Not applicable. This plan produces a documentation-only ADR proposal; no persisted state, migration, or runtime behavior is created. If `ADR-011` is rejected or requires material revision, the plan is updated in place (new decision entry, revised milestones) rather than abandoned, unless the owner directs a full restart.

## 11. Open questions and blockers

- None remaining for this plan. `ADR-011` was reviewed and accepted by the product owner.
- SQLite provider library selection (ADR section 12.1) and `CampaignPublicId` contract (ADR section 12.2) remain open questions carried by the ADR itself, by design — they are not blockers to this plan's own completion and are not resolved by acceptance.

## 12. Outcome and follow-up

Current outcome: `ADR-011_Local_Campaign_Format_v1.0.md` is `Accepted` (accepted by the product owner as-is on 2026-08-20, no content changes). `ODY-S01-001` task and this ExecPlan are `Done` and moved to `completed/`. `docs/tasks/SLICE-01_BACKLOG.md` `ODY-S01-001` row is `Done`.

Next action: `ODY-S01-002` (ADR: Snapshot and Append-Only Journal) may now begin per `docs/tasks/SLICE-01_BACKLOG.md` section 5, since its dependency on `ADR-011` `Accepted` is satisfied. `ODY-S01-004` (Owner Key Storage Baseline) may also proceed independently.
