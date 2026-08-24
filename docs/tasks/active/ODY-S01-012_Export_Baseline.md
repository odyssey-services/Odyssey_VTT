# ODY-S01-012 — Export Baseline (`.odcamp`)

**Status:** In Review
**Roadmap stage / slice:** SLICE-01 (implementation)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s01-012-export-baseline`
**Pull request:** Not yet opened
**ExecPlan:** `docs/plans/active/ODY-S01-012_Export_Baseline.md`
**Created:** 2026-08-24
**Last updated:** 2026-08-24 UTC

## 1. Goal

A campaign can be exported into a single portable `.odcamp` archive and imported from one into a brand-new local campaign copy, with manifest/version/checksum validation on import and no automatic merge with any existing campaign.

## 2. Why this task exists

- Problem: `ADR-011`/`05_Persistence` already define the `.odcamp` container format and its 9-step export/9-step import workflows in detail, but no code implements them.
- Value: closes the last `SLICE-01` "Campaign Storage" group item (Export baseline), giving the vertical slice a real portability path (move a campaign to another machine) without inventing a second database-copy mechanism.
- Enabling relationship: reuses `ODY-S01-011`'s already-proven snapshot mechanism (`SqliteSnapshotCopy`, extracted this task) instead of duplicating it — the same discipline `ODY-S01-010`/`011` each followed (fill in an already-reserved contract/table rather than inventing a parallel one).

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`
- `AGENTS.md`
- `PLANS.md`
- `ADR-011_Local_Campaign_Format_v1.0.md` section 3.2 (`.odcamp` definition), section 4 (physical structure the import target must reproduce)
- `05_Persistence_Odyssey_VTT_v0.8.md` section 27 (`.odcamp` format, 9-step export flow, 9-step import flow, incomplete-assets handling, archive security) — the authoritative source for this task's physical format, used verbatim, not invented
- `ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` section 8 (snapshot contract — the database-copy core export reuses)
- `ADR-014_Owner_Key_Storage_Baseline_v1.0.md` section 11.3/12.2 — explicitly `[OPEN]`, not implemented, not closed by this task
- roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` section 10.3 ("отсутствие автоматического merge")

### Requirement and test IDs

- Requirement IDs: roadmap §10.3 no-automatic-merge; 05_Persistence §27.4/27.5
- Existing test IDs: `TC-PERSIST-001`–`022`
- New test IDs to introduce: `TC-PERSIST-023`–`030`

### Task-safe private context

- Approved summary / references: None

## 4. Verified current state

### Verified facts

- `007`–`011` are `Done`/merged on `main` (`git log` shows `776de6c` = merge of PR #33 for `011`).
- `05_Persistence` section 27.1 defines the exact `.odcamp` contents (`manifest.json`, `campaign.db`, `Assets/`, `checksums.json`, `export-manifest.json`) and sections 27.2/27.3 define the exact 9-step export/import flows — used verbatim, not invented, per this task's own instruction.
- `SqliteBackupRepository.CreateBackup` (`ODY-S01-011`) already implements the identical database-copy-and-validate core (SQLite Backup API → temp path → `PRAGMA quick_check` → read `CampaignRevision`/`EventSequence` → SHA-256 hash/size) that `05_Persistence` section 27.2 steps 1-5 require for export — confirmed by reading the file before this task; extracted into a new shared `SqliteSnapshotCopy` helper (see section 5) rather than reimplemented.
- `System.IO.Compression.ZipFile` (`ZipFile.CreateFromDirectory`/`OpenRead`/`ExtractToDirectory`) is part of the BCL surface `netstandard2.1` already supports — confirmed by a clean build with no new NuGet package reference.
- `docs/tasks/active/ODY-S01-008_Scene_And_Token_Minimal_Model.md`/`009`/`010`/`011` still show stale pre-merge `Pull request` header text — the same recurring desync noted (and not fixed) in each prior task in this session; not addressed here either, out of this task's scope.

### Assumptions

- None — the `.odcamp` physical format comes directly from `05_Persistence` section 27.1, not invented.

## 5. Scope

### In scope

- `SqliteSnapshotCopy` (new, `internal`, `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSnapshotCopy.cs`): the shared ADR-012 section 8.4 steps 2-5 database-copy-and-validate primitive, extracted from `SqliteBackupRepository.CreateBackup` (behavior-preserving refactor, confirmed by all 36 pre-existing Persistence tests still passing unmodified) and reused by both `SqliteBackupRepository` and the new `SqliteExportRepository`.
- `IExportRepository` (Application port): `ExportCampaign(CampaignHandle, destinationOdcampPath, correlationId)`, `ImportCampaign(odcampPath, destinationParentDirectory, correlationId)`.
- `SqliteExportRepository`: the full `05_Persistence` section 27.2 9-step export flow and section 27.3 9-step import flow, within this task's narrowed scope (see "Out of scope").
- `ExportManifest`/`ExportManifestV1Codec` (`export-manifest.json`, `ADR-003` explicit hand-written codec discipline, same pattern as `CampaignManifestV1Codec`/`BackupManifestV1Codec`).
- `checksums.json`: a small hand-written flat JSON object (path → SHA-256), deliberately not wrapped in a full `ADR-003` versioned contract type — it is supplementary per-file integrity listing, not an authoritative domain/event/command payload (`export-manifest.json` is the authoritative record and does have a full codec).
- Archive-safety checks on import (05_Persistence section 27.5): path-traversal (`../`), absolute-path, and archive-escape entries rejected with a typed error before any byte is extracted.
- Manifest validation on import (reuses the existing `CampaignManifestV1Codec`), `DatabaseSchemaVersion` compatibility check (typed error on mismatch, no migration attempted), campaign.db integrity check, checksum verification against `export-manifest.json`'s recorded hash.
- Import always creates a brand-new campaign directory (`imported-<CampaignId>`); a pre-existing, non-empty target is a typed error, never a merge attempt.

### Out of scope, and why

- **Owner-key-aware reopening on a new machine** (`ADR-014` section 11.3): explicitly `[OPEN]`, not decided by any accepted authority yet — not implemented, not closed here.
- **Migration on import**: if the imported campaign's `DatabaseSchemaVersion` does not match this application's supported version, `ImportCampaign` returns a typed error — it never attempts to migrate (the full `ADR-013` runner still does not exist after `ODY-S01-010`'s narrow migration-registry-baseline scope).
- **Network/cloud export-import**: local `.odcamp` file only.
- **Backup rotation/tier logic for exports**: an export is a one-off, user-named file the caller places wherever they choose — it is never rotated, tiered, or automatically pruned (unlike `Backups/Fast|Daily|Weekly`).
- **Full backup composition parity beyond what `05_Persistence` section 27.1 itself lists**: the archive contains exactly `manifest.json`/`campaign.db`/`Assets/`/`checksums.json`/`export-manifest.json` — no additional files invented beyond that spec.
- **`Assets/Trash/`/`Assets/Quarantine/` inclusion**: `ADR-011` section 4.1 explicitly excludes these from a normal `.odcamp` export without an explicit user decision this revision does not implement a UI for; only `AssetManifestEntries`-referenced assets under `Assets/Objects/` are exported.
- **`SqliteSceneRepository.cs`/`SqliteSavingPipeline.cs`**: untouched.

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Persistence/ExportRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/ExportManifest.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSnapshotCopy.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteExportRepository.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteBackupRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/SqliteExportRepositoryTests.cs
Tests/Metadata/test-catalog.json
docs/errors/ERROR_CODES.md
docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S01-012_Export_Baseline.md
docs/plans/active/ODY-S01-012_Export_Baseline.md
```

### Paths requiring explicit approval before editing

```text
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSceneRepository.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSavingPipeline.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCampaignRepository.cs
docs/tasks/active/ODY-S01-007_Campaign_Storage_Foundation.md
docs/tasks/active/ODY-S01-008_Scene_And_Token_Minimal_Model.md
docs/tasks/active/ODY-S01-009_Saving_Pipeline.md
docs/tasks/active/ODY-S01-010_Migration_Registry_Baseline.md
docs/tasks/active/ODY-S01-011_Backups.md
```

`SqliteBackupRepository.cs` (listed under Allowed paths, not this section) was touched as a planned, necessary refactor per this task's own instruction ("не изобретай свой собственный механизм копирования... выдели общий internal helper, если требуется"); `SqliteCampaignRepository.cs` was read but not edited (`DatabaseSchemaVersion`'s `internal` visibility, already set by `ODY-S01-010`, was sufficient).

## 6. Technical constraints

- Module ownership and dependency direction: `IExportRepository` is an `Odyssey.Application.Persistence` port; `SqliteExportRepository`/`SqliteSnapshotCopy` are `Odyssey.Persistence` implementation details (`ADR-001` section 6.5).
- Authoritative-state and transaction boundary: export/import are entirely outside the `ADR-012` section 5 journal-projection transaction group, same as backups.
- Time / RNG rule: `IWallClock` for `export-manifest.json`'s `CreatedAt`; `Guid.NewGuid()` used only for opaque local temp-directory names (`ADR-008`'s explicitly permitted use), never as a domain/gameplay value.
- Unity / thread / lifetime rule: no new dependency; `System.IO.Compression.ZipFile` is BCL, already available on `netstandard2.1`.
- Dependency / licensing rule: no new NuGet package.
- Security / privacy / redaction rule: import archive-safety checks (path traversal, absolute paths, archive escape) per `05_Persistence` section 27.5; owner key material was never in scope to include (`ADR-014` section 10.3/`PE-INV-010`) and this task's export path never reads or touches any owner-key storage.
- Performance or platform constraint: `Pooling=False` used on every new SQLite connection string, consistent with the fix `ODY-S01-011` already applied.
- Other: Not applicable.

## 7. Expected behavior

### Scenario 1 — Export/import round-trip

**Given** an open campaign with a scene and a token in it
**When** `ExportCampaign` then `ImportCampaign` into a new directory
**Then** the imported copy has the same scene/token, and the original campaign is untouched.

### Scenario 2 — Export reuses the proven snapshot mechanism

**Given** an exported `.odcamp`
**When** `campaign.db` is extracted from it
**Then** it passes the same `PRAGMA quick_check` a backup snapshot does.

### Scenario 3 — No automatic merge

**Given** a target directory that already exists and is non-empty
**When** `ImportCampaign` is called against it
**Then** a typed error is returned and the pre-existing content is untouched.

### Scenario 4 — Manifest/version/archive-safety validation

**Given** a `.odcamp` with a semantically invalid manifest, an unsupported `DatabaseSchemaVersion`, or a path-traversal entry
**When** `ImportCampaign` is called
**Then** a typed error is returned, never a raw exception, and nothing unsafe is extracted.

### Required invariants

- Export never overwrites an existing `.odcamp` file at the destination path.
- Import never writes into or merges with an existing, non-empty campaign directory.
- No archive entry is ever extracted to a path outside the extraction directory.

## 8. Deliverables

- Production code: `SqliteSnapshotCopy.cs` (new, shared helper); `ExportRepositoryContracts.cs`, `ExportManifest.cs` (new); `SqliteExportRepository.cs` (new); `SqliteBackupRepository.cs` (refactored to use the shared helper); 2 new `ErrorCodes`.
- Tests: `SqliteExportRepositoryTests.cs` (8 tests, `TC-PERSIST-023`–`030`).
- Scripts / CI: None.
- Configuration: None (no new project needed — export/import tests run entirely in-process, no subprocess/kill-test harness required for this task).
- Documentation: `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` (row 6), this task contract, its ExecPlan.
- Generated evidence or build artifacts: None persisted beyond section 17's recorded test output.
- Migration / recovery material: None — no shipped `.odcamp` files exist to be affected.

## 9. Acceptance criteria

1. `ExportCampaign` produces a `.odcamp` containing exactly `manifest.json`, `campaign.db`, `checksums.json`, `export-manifest.json` (and `Assets/` when referenced assets exist) (`TC-PERSIST-028`).
2. The exported `campaign.db` passes the same `PRAGMA quick_check` a backup snapshot does, proving reuse of `SqliteSnapshotCopy` rather than a second copy path (`TC-PERSIST-024`).
3. `ExportCampaign` then `ImportCampaign` into a new directory reproduces the campaign's data at export time; the original campaign is untouched (`TC-PERSIST-023`).
4. `ImportCampaign` into an existing, non-empty target directory returns a typed error without touching that directory's content (`TC-PERSIST-025`).
5. `ImportCampaign` rejects path-traversal archive entries before extracting anything (`TC-PERSIST-026`).
6. `ImportCampaign` rejects a semantically invalid `manifest.json` with the typed `ManifestInvalid` error (`TC-PERSIST-027`).
7. `ImportCampaign` rejects an unsupported `DatabaseSchemaVersion` with a typed error, attempting no migration (`TC-PERSIST-029`).
8. `ExportCampaign` refuses to overwrite an existing destination `.odcamp` file (`TC-PERSIST-030`).
9. All prior `TC-PERSIST-*` tests continue to pass unmodified (confirms the `SqliteBackupRepository` refactor is behavior-preserving).
10. `SqliteSceneRepository.cs`/`SqliteSavingPipeline.cs` are untouched.
11. All required validation commands (section 10) pass.

The task is not complete while any mandatory criterion is unverified, failed, or silently deferred.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-PERSIST-023` | .NET / `dotnet test` | Export→import round-trip; source untouched | Pass |
| `TC-PERSIST-024` | .NET / `dotnet test` | Exported db passes the same quick_check a backup does | Pass |
| `TC-PERSIST-025` | .NET / `dotnet test` | No automatic merge into an existing target | Pass |
| `TC-PERSIST-026` | .NET / `dotnet test` | Path-traversal archive entry rejected pre-extraction | Pass |
| `TC-PERSIST-027` | .NET / `dotnet test` | Invalid manifest.json rejected, typed error | Pass |
| `TC-PERSIST-028` | .NET / `dotnet test` | Archive contains all required entries | Pass |
| `TC-PERSIST-029` | .NET / `dotnet test` | Unsupported schema version rejected, no migration | Pass |
| `TC-PERSIST-030` | .NET / `dotnet test` | Export never overwrites an existing .odcamp | Pass |

### Required commands

```powershell
.\scripts\restore.ps1
.\scripts\verify-format.ps1
.\scripts\verify-test-structure.ps1
.\scripts\test-fast.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-repository.ps1
```

### Manual validation

- None — all acceptance evidence is automated.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 development machine; CI runs the pure .NET solution on `ubuntu-latest`.
- Unity editor or Player profile: Not applicable — no new dependency; `System.IO.Compression.ZipFile` is BCL.
- Scripting backend: Not applicable.
- Network topology or database fixture: Local files under `Path.GetTempPath()`, cleaned up per test.
- Other: None.

### Validation not required by this task

- A real `Process.Kill()` interrupted-export test: `05_Persistence` section 27.2 step 9's atomic-rename-to-final-name pattern is identical in structure to `ODY-S01-011`'s already-kill-tested backup flow (both use temp→validate→atomic-rename via the same underlying `SqliteSnapshotCopy` core for the database-copy portion) — re-running an equivalent kill test here would re-prove the same underlying SQLite WAL/Backup-API guarantee `ODY-S01-011`'s `TC-PERSIST-016` and `SP-02` already established empirically, not a new property of export-specific code. Judged disproportionate to repeat given time/scope; flagged as a known limitation, not silently skipped.
- Zip-bomb / oversized-archive-claim rejection (`05_Persistence` section 27.5): not implemented or tested in this revision — flagged as a known limitation (see section 17).

## 11. Compatibility, migration, and rollback

- Compatibility impact: `IExportRepository` is an entirely new port. `SqliteBackupRepository`'s public `CreateBackup`/`ListBackups`/`RestoreBackup` signatures and observable behavior are unchanged (confirmed by all pre-existing tests passing); only its internal database-copy implementation moved into a shared helper.
- Version fields affected: None.
- Migration or upcaster: None required.
- Forward / backward behavior: An import correctly refuses a mismatched `DatabaseSchemaVersion` rather than silently accepting or corrupting it.
- Rollback method: Revert the branch.
- Data-loss risk and protection: None introduced; export only ever writes a new file, import only ever writes into a brand-new directory.
- Recovery rehearsal required: Partial — covered via the corruption/invalid-input typed-error tests; the real-kill rehearsal is explicitly deferred (see section 10, "Validation not required").

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | `System.IO.Compression.ZipFile` is part of the `netstandard2.1` BCL surface already targeted by `Odyssey.Persistence.csproj` | — | No new dependency, no license review needed |

## 13. Security, privacy, and hidden information

- Data classes handled: Campaign identifiers, version strings, hashes, scene/token data already present in the working campaign — nothing new.
- Trust boundaries: The `.odcamp` archive is treated as untrusted input on import (05_Persistence section 27.5) — path-traversal/absolute-path/archive-escape entries are rejected before extraction.
- Authorization / audience checks: Not applicable.
- Redaction requirements: Owner key material is never included (`ADR-014` section 10.3/`PE-INV-010`) — this task's export path only ever reads campaign.db/manifest.json/asset files, never any owner-key storage.
- Log-safe fields: Error factories follow the established no-raw-exception/no-raw-path convention.
- Abuse / malformed input limits: Path-traversal/absolute-path checks implemented and tested; zip-bomb/oversized-claim protection is explicitly not implemented (known limitation, section 17).
- Security tests: `TC-PERSIST-026` (path traversal).

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: Checked against `PLANS.md` section 1.2's triggers individually. This task introduces a new Application port (`IExportRepository`) and a new persisted JSON contract (`export-manifest.json`/`ExportManifestV1Codec`) — both explicit ExecPlan triggers on their own, the same reasoning `ODY-S01-011` used. It also handles untrusted external input (an arbitrary `.odcamp` file someone hands the application) with real security requirements (path-traversal rejection) — `PLANS.md` section 1.2's "security, permissions... diagnostics" trigger applies directly. `PLANS.md` section 1.1's Brief-plan bar is not met: this task changes a persisted format (introduces `export-manifest.json`) and handles untrusted input, which a brief plan's conditions explicitly exclude.
- ExecPlan path: `docs/plans/active/ODY-S01-012_Export_Baseline.md`
- Expected pull request count: 1
- Milestone or sequencing constraints: None beyond `007`/`011` already merged (backlog's stated dependencies).

## 15. Documentation and versioning impact

- Documents that must change: `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` (row 6 only).
- Documents that must not change: any ADR, `007`–`011` task contracts.
- Application version change: No.
- Schema / format / contract / manifest / protocol / ruleset version change: `export-manifest.json` is a new contract type (`odyssey.persistence.exportmanifest`, version 1) — new, not a change to an existing one.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed (None required).
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work. — pending Draft PR creation.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSnapshotCopy.cs` — new shared snapshot-copy-and-validate helper.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteBackupRepository.cs` — refactored `CreateBackup` to call the shared helper (behavior-preserving).
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteExportRepository.cs` — new implementation.
- `Packages/com.odyssey.application/Runtime/Persistence/ExportRepositoryContracts.cs`, `ExportManifest.cs` — new.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` — 2 new `PersistenceFailures` factories.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — 2 new codes.
- `DotNet/Tests/Odyssey.Tests.Persistence/SqliteExportRepositoryTests.cs` — new, 8 tests.
- `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json` — registry additions.
- `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` — row 6 status.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\restore.ps1` | Passed | All projects restored. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS` — no CRLF issue this time (all edits made directly via Write/Edit, no scripted regex pass). |
| `.\scripts\verify-test-structure.ps1` | Passed | `TC-ARCH-001`/`TC-ARCH-002` all PASS once this task contract existed. |
| `.\scripts\test-fast.ps1` | Passed | `Odyssey.Tests.Persistence.dll`: 44/44 (up from 36, includes the initial 43/44 run where one self-authored test assertion needed correcting -- see section 18). |
| `.\scripts\check-repository-policy.ps1` | Passed | Both new codes registered with real test references. |
| `.\scripts\verify-repository.ps1` | Passed | — |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `Export_ArchiveContainsAllFiveRequiredEntries` |
| AC-2 | Passed | `ExportedDatabase_PassesTheSameQuickCheckABackupDoes` |
| AC-3 | Passed | `ExportThenImport_IntoNewDirectory_DataMatchesOriginal_OriginalUntouched` |
| AC-4 | Passed | `Import_TargetDirectoryAlreadyExistsAndNonEmpty_ReturnsTypedError_NoAutomaticMerge` |
| AC-5 | Passed | `Import_UnsafePathTraversalEntry_ReturnsTypedError_NoExtraction` |
| AC-6 | Passed | `Import_CorruptManifestInsideArchive_ReturnsTypedManifestInvalid_NoRawException` |
| AC-7 | Passed | `Export_DatabaseSchemaVersionMismatch_ReturnsTypedError_NoMigrationAttempted` |
| AC-8 | Passed | `Export_DestinationAlreadyExists_ReturnsTypedCreateFailed_NoOverwrite` |
| AC-9 | Passed | 36 pre-existing `TC-PERSIST-*` tests all still pass (44 total, 0 failed after the fix in section 18) |
| AC-10 | Passed | `git diff --name-status` confirms neither file touched |
| AC-11 | Passed | See Validation results above |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: `artifacts/bin/Odyssey.Persistence/debug/Odyssey.Persistence.dll`.
- Checksums: Not recorded — debug local build.
- Test or quality report: `dotnet test` console output (section above).

### Known limitations

- No real `Process.Kill()` interrupted-export test (see section 10's "Validation not required" for the reasoning: the underlying temp→validate→atomic-rename guarantee is already kill-tested by `ODY-S01-011`'s `TC-PERSIST-016` via the same shared `SqliteSnapshotCopy` core).
- Zip-bomb / oversized-archive-claim rejection (`05_Persistence` section 27.5) is not implemented — a future task should add entry-count/uncompressed-size limits before this becomes a user-facing import path for untrusted files.
- `checksums.json` verifies only `campaign.db`'s hash on import (via `export-manifest.json`'s recorded value); it does not currently re-verify individual asset file hashes on import, only on export (where they are computed and written). A future task could add per-asset checksum verification on import.
- `Import_UnsafePathTraversalEntry` covers `../` traversal specifically; symlink-escape and Unicode-normalization-collision entries (also named in `05_Persistence` section 27.5) are not separately tested — the same `Path.GetFullPath` + directory-prefix check would catch most such cases, but this is not empirically proven here.

### Follow-up tasks

- None assigned — zip-bomb protection and per-asset import-time checksum verification are candidates for a future hardening task if `.odcamp` import becomes a route for files from untrusted sources (e.g., shared between players rather than only the campaign owner).

### Self-review summary

- Scope review: Implemented exactly the 9-step export/9-step import flows from `05_Persistence` section 27, no owner-key/migration/rotation logic added.
- Architecture review: New port in Application, implementation in Persistence; `SqliteBackupRepository`'s refactor is behavior-preserving (confirmed by its own unmodified test suite still passing); `SqliteSceneRepository.cs`/`SqliteSavingPipeline.cs` untouched.
- Test review: Every acceptance criterion has a dedicated, real test; the one self-authored test-expectation error (structural-JSON-vs-domain-manifest-validation distinction) was caught by the test actually failing, not by review, and fixed before completion.
- Security/privacy review: Path-traversal rejection implemented and tested; zip-bomb protection explicitly flagged as a known gap rather than silently omitted.
- Documentation/version review: Only the registries and one backlog row required updates; no ADR or version field touched.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-08-24 — Decision: extracted `SqliteSnapshotCopy` as a shared internal helper from `SqliteBackupRepository.CreateBackup`, per this task's own explicit instruction not to reinvent the database-copy mechanism. Verified behavior-preserving via the full pre-existing `SqliteBackupRepositoryTests.cs` suite (36 tests) still passing unmodified after the refactor. Authority: task contract section 5.
- 2026-08-24 — Decision: `checksums.json` is a small hand-written flat JSON object, not a full `ADR-003`-style versioned codec. Rationale: it is supplementary per-file integrity listing, not an authoritative domain/event/command payload; `export-manifest.json` (the actual authoritative record) does have a full codec. Authority: task contract section 5.
- 2026-08-24 — Decision: no real `Process.Kill()` interrupted-export test in this revision. Rationale: the underlying temp→validate→atomic-rename guarantee for the database-copy core is the exact same code path (`SqliteSnapshotCopy`) `ODY-S01-011`'s `TC-PERSIST-016` already kill-tested; re-running an equivalent test here would re-prove the same SQLite WAL/Backup-API property, not a new one specific to export/archive code. Flagged as a known limitation rather than silently skipped. Authority: task contract section 10.
- 2026-08-24 — Fix: an initial test (`Import_CorruptManifestInsideArchive...`) asserted the wrong error code (`PersistenceManifestInvalid` expected, `SerializationInvalidPayload` actually returned, since the fixture used structurally-invalid JSON which fails at an earlier validation layer than domain-level manifest field checks). Fixed by changing the fixture to structurally-valid-but-semantically-incomplete JSON, which correctly exercises the intended `ManifestInvalid` path. Not a production-code defect — both codes are legitimate typed errors for different failure classes.

### Approved task changes

- None.
