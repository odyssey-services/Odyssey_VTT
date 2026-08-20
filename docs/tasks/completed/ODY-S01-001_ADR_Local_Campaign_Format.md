# ODY-S01-001 - ADR: Local Campaign Format

**Status:** Done  
**Roadmap stage / slice:** SLICE-01  
**Owner:** Unassigned  
**Requested by:** Product owner  
**Branch:** `feat/ody-s01-001-adr-local-campaign-format`  
**Pull request:** Draft — [#22](https://github.com/odyssey-services/Odyssey_VTT/pull/22)  
**ExecPlan:** `docs/plans/completed/ODY-S01-001_ADR_Local_Campaign_Format.md`  
**Created:** 2026-08-20  
**Last updated:** 2026-08-20 UTC (ADR-011 accepted by product owner as-is)

## 1. Goal

Produce and propose `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md`, the normative ADR defining the local campaign's physical folder/`.odcamp` structure, `manifest.json` schema and authority, independent campaign version dimensions, the SQLite runtime profile, the base data-schema principle, and domain identifiers — the first of four prerequisite ADRs required before `SLICE-01` vertical-slice implementation can begin.

## 2. Why this task exists

- Problem or dependency being addressed: `05_Persistence_Odyssey_VTT_v0.8.md` describes the intended campaign format but is a product document, not a normative ADR; `SLICE-01` cannot begin implementation without an accepted technical contract for the campaign format, and `ODY-S01-002` (Snapshot/Journal) and `ODY-S01-003` (Migration Runner) both depend on it per `docs/tasks/SLICE-01_BACKLOG.md` section 5.
- Value or risk reduction: Prevents implementation from starting on an undecided physical format, and gives `ODY-S01-002`/`003` a stable foundation to build on without re-deciding the container/manifest/version/SQLite-profile questions themselves.
- Blocking or enabling relationship: Blocks `ODY-S01-002`, `ODY-S01-003`, and the future `SLICE-01` vertical-slice implementation backlog. Does not block `ODY-S01-004` (Owner Key Storage Baseline), which is independent per the backlog's dependency rules.

## 3. Authorities and requirement references

### Required authorities

- `05_Persistence_Odyssey_VTT_v0.8.md`, sections 3 (invariants, especially `PE-INV-001`, `PE-INV-002`, `PE-INV-006`, `PE-INV-009`, `PE-INV-010`), 4 (physical campaign structure), 5 (`manifest.json`), 6 (campaign versions), 7 (SQLite runtime profile), 8 (base data schema), 9 (identifiers) — primary content source.
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md` — Persistence module ownership and dependency-direction constraints the new ADR must not violate.
- `docs/adr/ADR-003_Serialization_Strategy_v1.1.md` — canonical JSON codec baseline that `manifest.json`/schema JSON columns must remain compatible with.
- `docs/adr/ADR-007_Versioning_and_Build_Identity_v1.0.md` — for aligning `CampaignFormatVersion` with `ApplicationVersion`/`BuildIdentity` independence rules.
- `docs/tasks/SLICE-01_BACKLOG.md` section 4 (task boundary for `ODY-S01-001`) and section 5 (dependency rules).
- `docs/tasks/active/ODY-S01-000_SLICE_01_Local_Campaign_Prerequisites.md` (parent task).
- `docs/tasks/TASK_TEMPLATE.md`, `PLANS.md`, `AGENTS.md`, `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`.

### Requirement and test IDs

- Requirement IDs: `SLICE-01`, roadmap section 10.2 prerequisite list, backlog `ODY-S01-001`.
- Existing test IDs: None owned by this task; no code is written.
- New test IDs to introduce: None. This ADR does not introduce automated tests; implementation tests belong to the future implementation backlog revision that codes against this ADR.

### Task-safe private context

- Approved summary / references: None. No private product documentation is referenced by this task.

## 4. Verified current state

### Verified facts

- `SLICE-00`/`M1` is closed (merge commit `7fbc9b0b7af242e6400538baf35a419536805872`).
- `docs/tasks/active/ODY-S01-000_SLICE_01_Local_Campaign_Prerequisites.md` and `docs/tasks/SLICE-01_BACKLOG.md` exist on `main` (merged via PR #21), listing `ODY-S01-001` as the first ordered prerequisite child task with no dependency.
- No ADR numbered `ADR-011` or higher exists in `docs/adr/` as of this activation; the next free ADR number is `ADR-011`.
- No campaign-format implementation code exists anywhere in the repository.

### Assumptions

- None.

## 5. Scope

### In scope

- Authoring `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md`, covering: physical folder structure, relative-path rules, folder-move behavior, `manifest.json` schema/authority/atomic-write rules, independent campaign version dimensions, SQLite runtime profile (WAL/single-writer/read-connections/checkpoint), the base data-schema principle and minimum required system tables (at the principle level, not full DDL), and domain identifiers.
- This governing ExecPlan.
- Updating `docs/tasks/SLICE-01_BACKLOG.md` section 3 status for `ODY-S01-001` only.

### Out of scope

- Any implementation code (C#, SQL DDL, Unity assets) realizing this ADR — deferred to the future `SLICE-01` implementation backlog revision.
- Deciding a specific .NET SQLite provider library — left as an explicit open question in the ADR (see ADR section 12.1), resolved later by `SP-02` findings or a separate owner decision.
- Snapshot/append-only journal contract, migration runner, and owner key storage mechanism content — owned by `ODY-S01-002`, `ODY-S01-003`, and `ODY-S01-004` respectively; this ADR only marks the boundary.
- Marking the ADR `Accepted` — that status change requires explicit product owner approval, recorded separately from this task's own completion.

### Allowed paths

```text
docs/adr/ADR-011_Local_Campaign_Format_v1.0.md
docs/tasks/active/ODY-S01-001_ADR_Local_Campaign_Format.md
docs/plans/active/ODY-S01-001_ADR_Local_Campaign_Format.md
docs/tasks/SLICE-01_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
Any other docs/adr/*.md (ADR-001 through ADR-010 remain unchanged)
docs/tasks/completed/ODY-S00-*.md and docs/plans/completed/ODY-S00-*.md
Documentation/** (private, non-tracked)
Any production code, test code, script, Unity, or package file
```

## 6. Technical constraints

- Module ownership and dependency direction: The ADR must remain implementable within `ADR-001`'s existing `Persistence` module boundaries (depends on `Domain`/`Content`/`Application`; forbidden from depending on `Networking`). Verified in ADR section 10.
- Authoritative-state and transaction boundary: The ADR must not contradict `PE-INV-003`/`PE-INV-005` (atomic command, journal+projection committed together); it references but does not redefine them.
- Serialization / compatibility boundary: Any JSON persisted per this ADR (manifest, JSON columns) must use `ADR-003` v1.1 explicit canonical codecs, not reflection/auto-mapping.
- Time / RNG rule: Not applicable; this ADR does not introduce authoritative clock/RNG behavior.
- Unity / thread / lifetime rule: Not applicable; no Unity/runtime code is introduced.
- Dependency / licensing rule: No SQLite provider library or other dependency is selected or pinned by this ADR (open question, section 12.1).
- Security / privacy / redaction rule: The ADR must reaffirm `PE-INV-010` (secrets never enter the campaign) without redefining the storage mechanism, which belongs to `ODY-S01-004`.
- Performance or platform constraint: Not applicable beyond the SQLite PRAGMA profile already specified in `05_Persistence` section 7.
- Other: None.

## 7. Expected behavior

### Scenario 1 - ADR exists and is internally consistent with its sources

**Given** `05_Persistence` sections 3–9 and the accepted `ADR-001`/`ADR-003`/`ADR-007`  
**When** `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md` is authored  
**Then** it covers physical structure, `manifest.json`, version dimensions, SQLite runtime profile, base schema principle, and identifiers as binding decisions, explicitly excludes snapshot/journal, migration runner, and owner key storage content, and flags the SQLite provider library choice as an open question rather than silently deciding it.

### Required invariants

- No implementation code is introduced.
- No ADR content decides matters explicitly reserved for `ODY-S01-002`, `ODY-S01-003`, or `ODY-S01-004`.
- The ADR's `Status` is not set to `Accepted` by this task; it is `Proposed` pending explicit owner review.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md`; this task contract; its ExecPlan; `docs/tasks/SLICE-01_BACKLOG.md` status update for `ODY-S01-001`.
- Generated evidence or build artifacts: None.
- Migration / recovery material: Not applicable.

## 9. Acceptance criteria

1. `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md` exists, uses the next free ADR number, and its `Status` is `Proposed` (not `Accepted`) pending owner review.
2. The ADR covers, as binding normative decisions: physical folder structure (`.odcamp` working-folder tree), relative-path rules, folder-move behavior, `manifest.json` schema/field authority/atomic-write rule, at least the three independent version dimensions (`CampaignFormatVersion`, `DatabaseSchemaVersion`, `RulesetVersion`), the SQLite PRAGMA profile and writer/read-connection/checkpoint rules, the base data-schema principle with the minimum required system table list, and domain identifier rules.
3. The ADR explicitly excludes snapshot/journal contract detail, migration runner detail, and owner key storage mechanism detail, each with a named forward reference to `ODY-S01-002`/`003`/`004`.
4. The ADR does not pin a specific SQLite provider library; that choice is recorded as an explicit open question.
5. The ADR does not conflict with `ADR-001` module boundaries, `ADR-003` serialization baseline, or `ADR-007` version-independence rules; each is explicitly cross-referenced.
6. `docs/tasks/SLICE-01_BACKLOG.md` section 3 reflects `ODY-S01-001`'s real status honestly (not marked `Done`/`Accepted` unless the owner has actually approved the ADR by the time this task closes).
7. `scripts/verify-format.ps1` and `scripts/check-repository-policy.ps1` both pass without modification to either script.
8. No file outside the allowed paths (section 5) is created or modified.

The task is not complete while any mandatory criterion is unverified, failed, or silently deferred.

## 10. Tests and validation

### Required automated tests

None. This is a documentation-only ADR-authoring task; no new test IDs are introduced.

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- Product owner review and explicit `Accepted`/`Rejected`/`Needs changes` decision on `ADR-011`. This task's own completion does not itself constitute ADR acceptance.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 (PowerShell validation only).
- Unity editor or Player profile: Not applicable; no Unity/.NET code is touched.
- Scripting backend: Not applicable.
- Network topology or database fixture: None.
- Other: None.

### Validation not required by this task

- `dotnet build`/`dotnet test`, Unity compile/EditMode/PlayMode, `verify-ci.ps1`, `verify-unity-project.ps1`, `verify-repository.ps1`, `verify-build-identity.ps1`, `test-serialization-aot.ps1`, `test-unity.ps1`, `build-dev.ps1`, `test-player-smoke.ps1`: none of these are affected because no production code, test code, script, Unity asset, package, or CI workflow file is touched by this task.

## 11. Compatibility, migration, and rollback

Not applicable. This task authors an ADR proposal only; it introduces no runtime persisted state, migration, or rollback surface. (The ADR itself will govern future persisted state once implemented, but that implementation is out of scope here.)

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | - | - | - | - |

No new dependency, GitHub Action, Unity package, executable, or download is approved by this contract. The ADR explicitly defers the SQLite provider library choice rather than selecting one.

## 13. Security, privacy, and hidden information

- Data classes handled: None directly; the ADR discusses where secrets must *not* be stored (`PE-INV-010`) without handling any secret itself.
- Trust boundaries: Not applicable.
- Authorization / audience checks: Not applicable.
- Redaction requirements: The ADR must not redefine or weaken `PE-INV-010`; it only reaffirms the existing boundary and defers the mechanism to `ODY-S01-004`.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable; no code is introduced.
- Security tests: None; deferred to implementation.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: Per `PLANS.md` section 1.2, an ExecPlan is required when a task "introduces or changes ... a schema ... manifest." This task's ADR introduces the `manifest.json` schema and the campaign's base SQLite schema principle (new persisted-format contracts), matching that trigger directly — the same reasoning that placed the original `ADR-003` (Serialization Strategy) under an ExecPlan-governed task (`ODY-S00-007`). Choosing `Brief plan` here would understate that this ADR is the foundational contract two other prerequisite ADRs (`ODY-S01-002`, `ODY-S01-003`) depend on; an incorrect or under-specified decision here would require amending multiple downstream ADRs. This is not chosen for the task's own execution complexity (it is, mechanically, one document and one PR) but because of what the decision itself introduces and what depends on it.
- ExecPlan path: `docs/plans/completed/ODY-S01-001_ADR_Local_Campaign_Format.md`
- Expected pull request count: 1 (this ADR authoring activation). ADR acceptance/rejection and any resulting revision is tracked as a follow-up within the same ExecPlan, not a new task, unless the owner requests material scope changes.
- Milestone or sequencing constraints: `ODY-S01-002` and `ODY-S01-003` should not begin content authoring until `ADR-011` reaches `Accepted`, per `docs/tasks/SLICE-01_BACKLOG.md` section 5. `ODY-S01-004` may proceed independently.

## 15. Documentation and versioning impact

- Documents that must change: `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md` (new); this task contract; its ExecPlan; `docs/tasks/SLICE-01_BACKLOG.md` (status only).
- Documents that must not change: `05_Persistence_Odyssey_VTT_v0.8.md`, `ADR-001`, `ADR-003`, `ADR-007`, any `ODY-S00-*` file, `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`, `README.md`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: `ADR-011` v1.0 is a new document; it does not itself bump `version.json` or `config/compatibility.json` — those remain a future implementation task's responsibility once the format is coded.
- Documentation version changes: New ADR at v1.0; no other document's version changes.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [ ] Required automated tests pass. (Not applicable — none exist for this task; see "Required commands" instead.)
- [x] Required manual checks are completed. (Product owner reviewed and accepted `ADR-011` as-is on 2026-08-20.)
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable (Not applicable, confirmed).
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [x] Pull request explains changes, evidence, limitations, and follow-up work. (PR #22.)
- [x] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`. (Product owner accepted `ADR-011` as-is 2026-08-20; PR #22 remains Draft and unmerged by Codex.)

## 17. Completion evidence

### Changed files / areas

- `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md` (new, then Status `Proposed` → `Accepted`): ADR content per sections 4–13 of the ADR itself.
- `docs/tasks/active/ODY-S01-001_ADR_Local_Campaign_Format.md` (this file, new, then moved to `docs/tasks/completed/`).
- `docs/plans/active/ODY-S01-001_ADR_Local_Campaign_Format.md` (new, then moved to `docs/plans/completed/`): governing ExecPlan.
- `docs/tasks/SLICE-01_BACKLOG.md`: `ODY-S01-001` row status updated twice (Draft → In Review → Done).

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Passed | See final report. |
| `.\scripts\check-repository-policy.ps1` | Passed | See final report; no new required-path expectation is introduced. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 through AC-8 | Passed | `ADR-011` created with all required content and explicit exclusions/open questions, then accepted by the product owner as-is on 2026-08-20 (Status `Proposed` → `Accepted`, no content changed); backlog updated honestly at each stage; validation commands pass; diff scope confirmed limited to allowed paths throughout. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: Not applicable.

### Known limitations

- The SQLite provider library choice remains an open question (ADR section 12.1), intentionally deferred to `SP-02` findings or a separate owner decision. Acceptance of `ADR-011` does not resolve this; it remains open by design.
- `CampaignPublicId`'s exact contract remains an open question (ADR section 12.2), not blocking this ADR's other decisions.

### Follow-up tasks

- `ODY-S01-002` (ADR: Snapshot and Append-Only Journal) — next per backlog dependency order; `ADR-011` is now `Accepted`, so this may begin.
- `ODY-S01-004` (ADR: Owner Key Storage Baseline) — may proceed independently per backlog section 5.

### Self-review summary

- Scope review: ADR content stays within the boundary defined by `docs/tasks/SLICE-01_BACKLOG.md` section 4 for `ODY-S01-001`; snapshot/journal, migration runner, and owner key storage content are explicitly excluded, not silently decided.
- Architecture review: ADR content is checked against `ADR-001` module boundaries (section 10 of the ADR) and does not introduce a conflicting dependency direction.
- Test review: No new TestCase IDs are introduced; this is intentional for an ADR-only task.
- Security/privacy review: `PE-INV-010` is reaffirmed, not redefined; no secret-handling mechanism is decided here.
- Documentation/version review: Only the files listed above changed; no ADR-001–010, `05_Persistence`, or `ODY-S00-*` file was modified.

## 18. Blockers, decisions, and change control

### Blockers

- None. `ADR-011` was reviewed and accepted by the product owner as-is on 2026-08-20.

### Decisions made during execution

- 2026-08-20 - Selected `ExecPlan` planning mode over `Brief plan` because this task introduces a new persisted-format schema (`manifest.json`, base SQLite schema principle), matching `PLANS.md` section 1.2's explicit trigger, following the same precedent as `ADR-003`'s original `ODY-S00-007` task - Authority / approval: `PLANS.md` section 1.2, product owner instruction to select and justify the mode.
- 2026-08-20 - Selected `ADR-011` as the ADR number, confirmed free by inspecting `docs/adr/` before writing - Authority / approval: repository state at task creation.
- 2026-08-20 - Left the concrete SQLite provider library selection as an explicit open question rather than deciding it, since `05_Persistence` section 7 only specifies the PRAGMA/behavioral profile, and `SP-02` (`ODY-S01-005`) is designed to test exactly the reliability characteristics that should inform that choice - Authority / approval: product owner instruction ("не выбирай и не пинуй конкретную SQLite provider-библиотеку... обоснуй отдельно и явно вынеси как открытый вопрос").
- 2026-08-20 - Product owner reviewed `ADR-011` and accepted it as-is, with no content changes requested; ADR Status moved `Proposed` → `Accepted`, task Status moved to `Done`, and this task/ExecPlan moved to `completed/` - Authority / approval: product owner.

### Approved task changes

- 2026-08-20 - Closed `ODY-S01-001`: ADR-011 accepted, task/ExecPlan moved to `completed/`, backlog status updated to `Done` - Approved by: product owner.
