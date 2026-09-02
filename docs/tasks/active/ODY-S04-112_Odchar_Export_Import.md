# ODY-S04-112 — `.odchar` Export & Import

**Status:** In Review
**Roadmap stage / slice:** SLICE-04
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s04-112-odchar-export-import`
**Pull request:** [#96](https://github.com/odyssey-services/Odyssey_VTT/pull/96)
**ExecPlan:** `docs/plans/active/ODY-S04-112_Odchar_Export_Import.md`
**Created:** 2026-09-02
**Last updated:** 2026-09-03 UTC

## 1. Goal

Implement `ExportCharacter` (producing a redacted `.odchar` bundle per `ADR-026`) and `ImportCharacter` (creating a fresh local Draft from that bundle through `ODY-S04-103`'s unmodified `BindDraftToCampaign` pipeline, `RulesetVersion` re-pinned to the target campaign per `ADR-025` §7.6) — a full, real export-then-import round trip that never carries `CharacterOwnership`/`CharacterId`/`CampaignId` across campaigns.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-04_IMPLEMENTATION_BACKLOG.md` names `ODY-S04-112` as the twelfth implementation task. Its two open design questions (the `.odchar` file format, and the export field-visibility rule) had no ADR until `ADR-026` closed them — this task is the first to implement against that ADR.
- Value or risk reduction: gives a Character a portable, permission-safe file representation without leaking ownership/account identity across campaigns; proves `ADR-023`'s local-Draft/`BindDraftToCampaign` pipeline generalizes to a second seed source (an imported file, not only a `CharacterTemplate`) without modification.
- Blocking or enabling relationship: unblocks `ODY-S04-113` (Character Ruleset Migration) only in backlog order, not by a real technical dependency; no later task requires `ODY-S04-112` to exist first.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`.
- `AGENTS.md`, `PLANS.md` §1.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (row 12) — the binding scope definition for this task.
- `docs/adr/ADR-026_Character_Export_Import_File_Format_And_Redaction_v1.0.md` (full read — the governing ADR for file format and export redaction).
- `docs/adr/ADR-025_Character_Ownership_Lifecycle_And_Ruleset_Migration_Operations_v1.0.md` §7.6 (full read — import's Draft-creation/`RulesetVersion`-pinning, unmodified by this task).
- `docs/adr/ADR-023_Character_Drafts_Templates_And_Approval_Workflow_v1.0.md` §5–6 (full read — the local-Draft/`BindDraftToCampaign`/compatibility-validation pipeline this task reuses as-is).
- `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md` (full read — `CharacterRecord`'s exact shape, since `RedactCharacterForExport` must account for every field on it).
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §24 (full read).
- `Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs`'s own `ICharacterRepository`/`CharacterRecord` (`ODY-S04-101`) and `BindDraftToCampaignRequest`/`CreateLocalCharacterDraft` (`ODY-S04-103`) — the binding structural precedent for both the export payload shape and the import seed-source contract.

### Requirement and test IDs

- Requirement IDs: `ODY-S04-112`, `ADR-026` (all sections), `ADR-025` §7.6, `ADR-023` §5–6, product §24.
- Existing test IDs reused: None directly reused. All pre-existing `TC-CHAR-*` tests through `TC-CHAR-143` must continue passing unmodified.
- New test IDs introduced: `TC-CHAR-144` through `TC-CHAR-155` (`Tests/Metadata/test-catalog.json`) — exact count confirmed against the scenarios in section 7; do not under- or over-report the real count actually written.

### Task-safe private context

- Approved summary / references: non-tracked product documentation was read as local authority and summarized by section/topic only. No private prose, secret, personal data, or hidden campaign content is copied into this task, its plan, or production code.

## 4. Verified current state

### Verified facts

- `ADR-026` is `Accepted` (2026-09-02) and fixes the `.odchar` structure (`manifest.json`/`character.json`/`portrait/`/`referenced-assets/`), the `RedactCharacterForExport` mechanism, and the rule that `CharacterOwnership`/`CharacterId`/`CampaignId` are never serialized into `character.json`.
- Direct inspection of `CharacterRecord`'s constructor (`CharacterRepositoryContracts.cs`) confirms no field is currently classified as GM-only-visible or secret/credential-shaped — `ADR-026` §5 already records this and requires export to reflect it honestly (full payload today) rather than fabricate a redaction test against non-existent data.
- `BindDraftToCampaignRequest`/`CreateLocalCharacterDraft` (`ODY-S04-103`) already accept a seed source with fresh nested identifiers and perform `RulesetVersion`/compatibility validation at bind time — no signature change is anticipated; the seed source for import is the deserialized `.odchar` payload in place of a `CharacterTemplate`.
- `Tests/Metadata/test-catalog.json`'s last assigned Character test ID is `TC-CHAR-143` (`ODY-S04-111`).

### Assumptions

- None. All facts above were directly observed via `Read`/`Grep` during this task's authoring; the implementing agent must re-verify them against the repository state at the time it starts (`ADR-026`/backlog content may not have changed, but must not be assumed unchanged without a fresh read).

## 5. Scope

### In scope

- `Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs` (edit) — `ICharacterRepository.ExportCharacter`/`ImportCharacter` (or an `Odyssey.Application`-level service if a repository-port shape does not fit an I/O-touching, non-authoritative-state operation — decide and record under Decisions, section 18, at implementation time); `CharacterExportPayload`/`CharacterExportManifest` DTOs (new, per `ADR-026` §4).
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` (edit, if new `PersistenceFailures` entries are needed for import rejection paths beyond what `ODY-S04-103`'s `BindDraftToCampaign` already returns).
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` (edit, only if new error codes are actually needed — do not pre-register speculative codes).
- A new `RedactCharacterForExport` function/class in `Odyssey.Application` (per `ADR-026` §3.2/§8) with the single named extension point `ADR-026` §8 rule 4 requires.
- `.odchar` bundle read/write (manifest + character payload serialization) — `Odyssey.Persistence` or `Odyssey.Application`, whichever matches this codebase's existing boundary for file I/O outside `campaign.db` (check `ADR-012`'s Backup API placement as precedent before deciding).
- Tests (new file, e.g. `DotNet/Tests/Odyssey.Tests.Persistence/CharacterExportImportTests.cs` or `Odyssey.Tests.Application`, whichever matches where the export/import logic actually lands).
- `docs/errors/ERROR_CODES.md` (edit, only if new codes are introduced).
- `Tests/Metadata/test-catalog.json` (edit) — `TC-CHAR-144`–`155` (or the real final count).
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (edit) — row 12 marked `Done` with the real PR link.
- This task contract and its ExecPlan.

### Out of scope

- `.odchar` file format `FormatVersion` 2+, bundle compression/container mechanics (zip vs. directory) — `ADR-026` §6/§11 explicitly defer these; pick the simplest correct mechanism (a directory is sufficient) and record the choice under Decisions rather than treating it as open.
- Any concrete GM-only or secret Character field — `ADR-026` §5/§10.2 explicitly reserve this to a future task; do not add a placeholder field to `CharacterRecord` to manufacture redaction test coverage.
- `ADR-025` §7.6's Draft-creation/`RulesetVersion`-pinning behavior itself — already implemented by `ODY-S04-103`/`ODY-S04-104`'s pipeline; this task only supplies the seed source, it does not modify `BindDraftToCampaign`'s own logic.
- Character Ruleset Migration (`ODY-S04-113`), Vertical Slice Integration (`ODY-S04-114`), Acceptance/Closure Gate (`ODY-S04-115`).
- Any Unity/UI code (file picker, save dialog) — this task is purely Domain/Application/Persistence.
- Any change to `ADR-022`/`023`/`025`/`026` content, or to `SLICE-04_IMPLEMENTATION_BACKLOG.md` beyond this task's own status row.

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/CharacterExportImportTests.cs
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S04-112_Odchar_Export_Import.md
docs/plans/active/ODY-S04-112_Odchar_Export_Import.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/ADR-001* through docs/adr/ADR-026*
docs/tasks/SLICE-04_BACKLOG.md
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: `Odyssey.Application` owns `RedactCharacterForExport`, the export payload/manifest DTOs, and `ImportCharacter`'s mapping from the deserialized payload to `BindDraftToCampaignRequest`'s seed-source shape. `Odyssey.Persistence` owns reading the `CharacterRecord` row for export and (if this is where prior file I/O for this codebase lives — verify against `ADR-012`'s Backup API before deciding) the bundle's file I/O. Matches `ADR-001`.
- Authoritative-state and transaction boundary: `ExportCharacter` is read-only against `campaign.db` — no event, no transaction. `ImportCharacter` produces exactly the transaction `ODY-S04-103`'s `CreateLocalCharacterDraft`/`BindDraftToCampaign` already commits — this task must not add a second, parallel transaction path.
- Serialization / compatibility boundary: `character.json`/`manifest.json` use `Newtonsoft.Json.Linq` directly (`ADR-003`'s approved low-level API), matching every prior `SLICE-04` task; `manifest.json`'s `FormatVersion` starts at `"1.0"` and is additive-only per `ADR-026` §4.
- Time / RNG rule: `IWallClock` injected exactly as `ODY-S04-101`–`111` already do for `ExportedAt`; no direct wall-clock access.
- Unity / thread / lifetime rule: Not applicable — no Unity code in this task.
- Dependency / licensing rule: no new dependency; if a zip container is chosen for the bundle, use only an already-approved dependency or the .NET BCL's own `System.IO.Compression` — record the choice under Decisions, do not add a new third-party package without approval.
- Security / privacy / redaction rule: `RedactCharacterForExport` must never serialize `CharacterOwnership`/`CharacterId`/`CampaignId`; any new `PersistenceFailures` entries must never expose raw file-path/IO exception text.
- Performance or platform constraint: unchanged from `ODY-S04-101`–`111`'s own established pattern.
- Other: `ExportCharacter` performs no permission check beyond what `ADR-026` §5 already resolves (full payload for every actor today, since no field is currently redacted); `ImportCharacter` performs no permission check beyond what `ODY-S04-104`'s existing Draft/approval pipeline already requires downstream.

## 7. Expected behavior

### Scenario 1 — Export produces a well-formed `.odchar` bundle

**Given** an Active Character with initialized `CharacterAnatomy`/`CharacterResource`
**When** `ExportCharacter` is called by its owner
**Then** the result contains `manifest.json` (`FormatVersion="1.0"`, `ExportedAt`, `ExportedByRole`, `SourceRulesetVersion`) and `character.json` (`CharacterExportPayload`) with no `CharacterOwnership`/`CharacterId`/`CampaignId` field present anywhere in the serialized output.

### Scenario 2 — Export is identical regardless of actor role today

**Given** the same Character
**When** `ExportCharacter` is called once by MainGM and once by the Character's own owner
**Then** the two `character.json` payloads are byte-for-byte identical — proving the redaction filter actually ran and reached the same (currently unfiltered) conclusion for both, not that the filter was skipped.

### Scenario 3 — Import creates a fresh Draft, never reusing the source identity

**Given** a `.odchar` bundle previously exported from Character A in Campaign X
**When** `ImportCharacter` is called against a different Campaign Y
**Then** a new `CharacterId` distinct from A's is created, the resulting Draft's `RulesetVersion` equals Campaign Y's current Ruleset (not the file's own `SourceRulesetVersion`, unless they happen to match), and the Draft requires fresh GM approval before becoming Active.

### Scenario 4 — Round trip preserves mechanics values

**Given** a Character with non-default attributes/skills/resources/anatomy
**When** it is exported then immediately imported into a compatible-Ruleset campaign
**Then** every mechanics/anatomy/resource value `ADR-025` §7.6 does not otherwise require to change is preserved exactly, verified value-by-value on the resulting Draft.

### Scenario 5 — Incompatible-Ruleset import is rejected exactly as `ODY-S04-103` already defines

**Given** a `.odchar` bundle whose Ruleset is incompatible with the target campaign
**When** `ImportCharacter` is called
**Then** `BindDraftToCampaign`'s existing compatibility rejection applies unchanged — this task adds no second, import-specific compatibility check.

### Required invariants

- `character.json` never contains `CharacterOwnership`, `CharacterId`, or `CampaignId`.
- `ExportCharacter` never mutates the exported Character or writes a `DomainEvents` row.
- `ImportCharacter` never bypasses `ODY-S04-103`'s `BindDraftToCampaign`/`ODY-S04-104`'s approval requirement.
- No `ADR-022`/`023`/`025`/`026` file content changes.

## 8. Deliverables

- Production code: `RedactCharacterForExport`, `ExportCharacter`/`ImportCharacter` (Application, and Persistence if file I/O lands there), `CharacterExportPayload`/`CharacterExportManifest` DTOs.
- Tests: new tests in a new test file, registered as `TC-CHAR-144` onward (exact count from what is actually written).
- Scripts / CI: None new.
- Configuration: None.
- Documentation: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md` (only if new codes added), `Tests/Metadata/test-catalog.json`, `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- Generated evidence or build artifacts: None committed (test/build output only).
- Migration / recovery material: None — no schema change anticipated (confirm at implementation time; record under Decisions if one becomes necessary).

## 9. Acceptance criteria

1. All pre-existing `TC-CHAR-*` tests through `TC-CHAR-143` continue passing with their own assertions unmodified.
2. `ExportCharacter` produces a `.odchar` bundle matching `ADR-026` §4's structure; `character.json` never contains `CharacterOwnership`/`CharacterId`/`CampaignId`, verified directly against the serialized payload.
3. `ExportCharacter` by MainGM and by the Character's own owner produce identical `character.json` output, verified directly (Scenario 2).
4. `ImportCharacter` against a previously exported bundle produces a fresh `CharacterId`, a Draft requiring approval, and a `RulesetVersion` pinned to the target campaign — not the file's own `SourceRulesetVersion` when they differ.
5. A round trip (export then import into a compatible campaign) preserves every mechanics/anatomy/resource value not otherwise required to change by `ADR-025` §7.6.
6. An incompatible-Ruleset import is rejected through `ODY-S04-103`'s existing compatibility check, with no second import-specific check added.
7. No change to `ADR-022`/`023`/`025`/`026`/`SLICE-04_BACKLOG.md`; no Unity/UI code; no GM-only/secret Character field invented.
8. `dotnet build`, `dotnet test` (full suite, no regression), `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1` all pass.
9. `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` row 12 marked `Done` with a real PR link.
10. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-CHAR-144`–`149` (indicative) | .NET (`Odyssey.Tests.Persistence` or `.Application`) | `ExportCharacter`: bundle structure, no ownership/identity leakage, role-invariant output, redaction extension point exists but is currently a no-op by honest design | Pass |
| `TC-CHAR-150`–`155` (indicative) | .NET | `ImportCharacter`: fresh `CharacterId`, `RulesetVersion` re-pinning, Draft-requires-approval, round-trip value preservation, incompatible-Ruleset rejection, duplicate-`CommandId` idempotency if `ImportCharacter` is modeled as a command | Pass |

The exact IDs/count must be confirmed against the tests actually written, not assumed from this table.

### Required commands

```bash
cd DotNet
dotnet build Odyssey.Core.sln
dotnet test Odyssey.Core.sln
```

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- None beyond the automated tests above.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable — no Unity/UI code in this task.
- Scripting backend: Not applicable.
- Network topology or database fixture: real temp-directory SQLite campaign per test, matching `ODY-S04-101`–`111`'s own fixture convention; import tests need two temp-directory campaigns (source and target) to prove cross-campaign identity handling.
- Other: `dotnet` SDK matching `global.json`.

### Validation not required by this task

- `scripts/test-unity.ps1` — this task adds no Unity-facing behavior.

## 11. Compatibility, migration, and rollback

- Compatibility impact: none anticipated — no schema change; both operations are additive.
- Version fields affected: `.odchar`'s own `manifest.json` `FormatVersion`, starting at `1.0` (new, not a change to an existing version field).
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable for `FormatVersion` `1.0` (first version).
- Rollback method: revert this task's commits.
- Data-loss risk and protection: `ExportCharacter` is read-only; `ImportCharacter` only ever creates a new Draft, never mutates an existing Character.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

If a bundle container format needs compression, prefer the .NET BCL's `System.IO.Compression` (already part of the SDK, no new dependency) over a third-party package; record the decision if made.

## 13. Security, privacy, and hidden information

- Data classes handled: Character mechanics/anatomy/resource values, Ruleset/definition references — no account credentials, no secrets (none exist on `CharacterRecord` today, `ADR-026` §5).
- Trust boundaries: `ExportCharacter` performs no MainGM-only gate (full payload for every actor today, per `ADR-026` §5); `ImportCharacter`'s downstream Draft/approval gate is `ODY-S04-104`'s existing MainGM-only approval, not reopened here.
- Authorization / audience checks: caller-supplied actor context reused per `ADR-026` §3.2's `ExportActorContext`.
- Redaction requirements: `CharacterOwnership`/`CharacterId`/`CampaignId` never serialized; any new `PersistenceFailures` entries never expose raw file-path/IO exception text.
- Log-safe fields: export/import event or diagnostic logs (if any) carry only Character/Ruleset identifiers, not raw file contents.
- Abuse / malformed input limits: `ImportCharacter` must reject a malformed/truncated `.odchar` bundle gracefully (a `Result.Failure`, not an unhandled exception) — add this as an explicit test if not already implied by scenario 5's compatibility check.
- Security tests: Scenario 1/2's direct assertion that no ownership/identity field ever appears in `character.json`.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: extends a public Application-layer contract, introduces new DTOs and a new cross-cutting redaction mechanism (`ADR-026`), and spans export (read path) and import (write/transaction path) together.
- ExecPlan path: `docs/plans/active/ODY-S04-112_Odchar_Export_Import.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: depends on `ODY-S04-101` (done) and `ODY-S04-103` (done); no dependency on `ODY-S04-104`/`105`–`111` beyond what full round-trip test fixtures may reuse for realistic Character state.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md` (only if new codes added), `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (status only).
- Documents that must not change: `ADR-001` through `ADR-026`, `docs/tasks/SLICE-04_BACKLOG.md`, `Documentation/**`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: `.odchar` `FormatVersion` `1.0` is introduced (new, not a change to an existing version).
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed (none required).
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

See section 5's "In scope" file list, plus two new Application-layer files (`CharacterExportContracts.cs`, `RedactCharacterForExport.cs`) implied by that same section's own text but not itemized in the "Allowed paths" code block — see this task's own ExecPlan §4 Decisions for the precedent this follows.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test Odyssey.Core.sln` | Passed | Full suite: Contracts 1, Domain 56, Networking 67, Unit 105, Architecture 2, Persistence 229 (220 pre-existing + 9 new) — 460 total, 0 failures, 0 regressions. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | `Repository policy check passed.` |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | Every earlier test file unmodified, all still pass. |
| AC-2 | Passed | `TC-CHAR-144`. |
| AC-3 | Passed | `TC-CHAR-145`. |
| AC-4 | Passed | `TC-CHAR-147`. |
| AC-5 | Passed | `TC-CHAR-150`. |
| AC-6 | Passed | `TC-CHAR-148`. |
| AC-7 | Passed | `git status --porcelain` confirms no `ADR-022`/`023`/`025`/`SLICE-04_BACKLOG.md`/`Assets/**` file touched (`ADR-026` itself is new, added by the product owner as this task's own governing ADR, not modified); no GM-only/secret field invented. |
| AC-8 | Passed | See Validation results above. |
| AC-9 | Passed | `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 12 status/PR link updated. |
| AC-10 | Passed | Draft PR link and CI status recorded once opened (see final report). |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: `dotnet test` console output (see Validation results).

### Known limitations

- `HistoryEventTypes`/`GetCharacterHistory` render `odyssey.persistence.character_import_state_applied` alongside `odyssey.persistence.character_draft_bound` for the imported Character's own history, exactly like every other multi-step creation flow in this codebase — no separate limitation there.
- `ImportCharacter` does not carry `DevelopmentPool.Reserved` or any pending `AdvancementRecommendation` across, by design (ExecPlan Decisions) — a future task that wants to preserve in-flight recommendations across import would need its own explicit mechanism.
- No `portrait/`/`referenced-assets/` bundle content exists yet — the `.odchar` bundle in this task is `manifest.json`+`character.json` only, matching `ADR-026`'s own "optional" framing for the other two entries.

### Follow-up tasks

- `ODY-S04-113` — Character Ruleset Migration.
- A future task introducing a real GM-only/secret Character field would be the first real occasion to extend `RedactCharacterForExport`'s own named extension point.
- A future task wanting `.odchar` `FormatVersion` 2+/bundle compression would need its own superseding-ADR-level decision, per `ADR-026` §4's own bump rule.

### Self-review summary

- Scope review: limited to allowed files plus the two new Application-layer files implied by the task's own §5 "In scope" text; no `ADR-022`/`023`/`025`/`SLICE-04_BACKLOG.md` change; no Unity/UI code; no GM-only/secret field invented; `BindDraftToCampaign`/`CharacterCreationSeed` (`ODY-S04-103`) left byte-for-byte unmodified.
- Architecture review: `RedactCharacterForExport` is a pure, connection-free Application function with one named extension point; `ImportCharacter` reuses `BindDraftToCampaign` unmodified and adds its own dedicated cross-section `ApplyImportedCharacterState` step, following `RestoreDeadCharacter`/`ApplyCharacterRespec`'s own precedent; the Ruleset-compatibility check reuses the exact same underlying pure function `BindDraftToCampaign` itself calls, without touching that method.
- Test review: every acceptance criterion has a real, non-stubbed test against genuine temp-directory SQLite campaigns (two, for the cross-campaign export/import scenarios) — no mocked repository, no bypassed transaction pipeline; three real defects (two architectural gaps, one replay bug) were found and fixed during this task, not glossed over — see ExecPlan §5.
- Security/privacy review: `character.json` never contains `CharacterOwnership`/`CharacterId`/`CampaignId`, verified directly against the serialized text in two separate tests; `ExportedByRole` resolution reuses existing ownership-relationship data, no new permission mechanism introduced.
- Documentation/version review: task contract, ExecPlan, error registry, test catalog, and backlog status all updated; no ADR content changed (`ADR-026` itself was authored by the product owner before this task started, per its own §13).

## 18. Blockers, decisions, and change control

### Blockers

- None for this task — `ADR-026` closed the prior blocking gap on 2026-09-02.

### Decisions made during execution

- 2026-09-02 — Decision: `ExportCharacter`/`ImportCharacter` fold directly into `ICharacterRepository` (no new mini-interface) — Authority/approval: matches every prior `SLICE-04` task's own convention.
- 2026-09-02 — Decision: bundle file I/O lives in `SqliteCharacterRepository.cs`, not a new layer — Authority/approval: `SqliteBackupRepository`'s own precedent; `ADR-026` §7's own module-boundary text.
- 2026-09-02 — Decision: a plain directory bundle, no compression — Authority/approval: `ADR-026` §3.1/§6/§11 explicitly defer this to the implementation.
- 2026-09-03 — Decision: `ImportCharacter`'s Ruleset-compatibility check calls `CharacterTemplateCompatibility.IsCompatible` directly, never touching `BindDraftToCampaign`/`CharacterCreationSeed`'s own code — Authority/approval: this task's own ExecPlan §5 (the second architectural gap found); keeps `ODY-S04-103` genuinely unmodified per `ADR-026` §8 rule 6.
- 2026-09-03 — Decision: mechanics/anatomy/resource reconstruction is a second, dedicated cross-section step (`ApplyImportedCharacterState`) after `BindDraftToCampaign` — Authority/approval: this task's own ExecPlan §5 (the first architectural gap found); matches `RestoreDeadCharacter`/`ApplyCharacterRespec`'s own precedent.
- 2026-09-03 — Decision: fixed a real replay bug found by this task's own first duplicate-`CommandId` test run (`BindDraftToCampaign`'s own live-row replay lookup broken by `ApplyImportedCharacterState`'s later `LastCommandId` overwrite) — Authority/approval: this task's own test-driven discovery; fixed by resolving a replayed `bindCommandId` from the `character_draft_bound` `DomainEvents` row's own `CommandId` column instead, touching only `ImportCharacter`'s own code.
- 2026-09-03 — Decision: `DevelopmentPool.Reserved` is never exported/imported (always `0` on import) — Authority/approval: `Reserved` has no meaning without its corresponding, non-exported pending `AdvancementRecommendationRecord` rows.

### Approved task changes

- None.
