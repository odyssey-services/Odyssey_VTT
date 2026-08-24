# ODY-S01-011 — Backups

**Status:** In Review
**Roadmap stage / slice:** SLICE-01 (implementation)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s01-011-backups`
**Pull request:** Not yet opened
**ExecPlan:** `docs/plans/active/ODY-S01-011_Backups.md`
**Created:** 2026-08-24
**Last updated:** 2026-08-24 UTC

## 1. Goal

A campaign can be manually backed up via the SQLite Backup API into a rotation-managed `Backups/{Fast,Daily,Weekly}` tree, and any retained backup can be restored into a brand-new, separate campaign copy — even when the campaign's own working `campaign.db` is corrupted.

## 2. Why this task exists

- Problem: `ADR-012` section 8's snapshot contract had never been exercised in production code — only at the isolated `SP-02` spike level.
- Value: closes roadmap §10.6 exit criterion 5 (backup survives corruption of the working database) with real, running code and real tests, not a spike.
- Enabling relationship: `ODY-S01-010` (migration registry) already established the pattern of filling in a section-8.2-reserved system table (`BackupRecords`); `ODY-S01-013`'s future full migration runner will depend on the snapshot-before-migration trigger this task's `CreateBackup` makes callable.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`
- `AGENTS.md`
- `PLANS.md`
- `ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` sections 8 (snapshot contract), 9 (`PE-INV-008`) — sections 8.2 items 5-7 (migration/GM-Override triggers) and Full backup composition are explicitly out of scope
- `ADR-011_Local_Campaign_Format_v1.1.md` section 4.1 (`Backups/Fast|Daily|Weekly|Full|Emergency` physical tree, already reserved)
- `05_Persistence_Odyssey_VTT_v0.8.md` section 21 (backup kinds, `BackupRecord`, rotation baseline 10/7/4, space-shortage handling, post-creation check)
- `docs/tasks/completed/ODY-S01-005_SP-02_Persistence_Reliability_Report.md` sections 2.3 (interrupted backup), 2.5 (snapshot size/speed) — empirical baseline reused, not re-derived
- `ADR-014_Owner_Key_Storage_Baseline_v1.0.md` section 8 (backup encryption already closed as "none in MVP" — not reopened)

### Requirement and test IDs

- Requirement IDs: roadmap §10.6 exit criterion 5
- Existing test IDs: `TC-PERSIST-001`–`013`
- New test IDs to introduce: `TC-PERSIST-014`–`022`

### Task-safe private context

- Approved summary / references: None

## 4. Verified current state

### Verified facts

- `007`–`010` are `Done`/merged on `main` (`git log` shows `6300579` = merge of PR #32 for `010`).
- `BackupRecords` existed only as `ODY-S01-007` placeholder DDL (`BackupId`, `BackupKind`, `CreatedAt`, `RelativePath`) with no row ever written by any prior task.
- `Backups/Fast|Daily|Weekly|Full|Emergency` directories are already created by `Create()`'s existing `DirectoryTree` list (`ODY-S01-007`) — no new directory-tree wiring needed for the three tiers this task uses.
- `IWallClock` is already threaded through `SqliteCampaignRepository`/`SqliteSceneRepository`; reusing it for `SqliteBackupRepository` makes rotation testable deterministically (advance a fake clock across simulated days) without waiting real time.
- `docs/tasks/active/ODY-S01-008_Scene_And_Token_Minimal_Model.md` and `ODY-S01-009_Saving_Pipeline.md` still show pre-merge `Pull request` header text even though PR #29/#31 are both merged — the same stale-status desync fixed before for `007`/`008`, not addressed by this task (out of scope, not requested).

### Assumptions

- None — the exact rotation numbers (10/7/4) come directly from `05_Persistence` section 21.3, not invented.

## 5. Scope

### In scope

- `IBackupRepository` (Application port): `CreateBackup(CampaignHandle, reason, correlationId)`, `ListBackups(campaignFolderPath, correlationId)`, `RestoreBackup(campaignFolderPath, backupId, destinationParentDirectory, correlationId)`.
- `SqliteBackupRepository`: SQLite Backup API copy → temp directory → integrity validation on the copy → atomic `Directory.Move` to final path (`ADR-012` section 8.4 steps 1-8), Fast/Daily/Weekly rotation with a configurable `BackupRotationPolicy` (defaults 10/7/4 per `05_Persistence` section 21.3), restore always into a brand-new directory.
- `BackupManifest`/`BackupManifestV1Codec` (`backup-manifest.json`, `ADR-003` explicit hand-written codec discipline).
- `BackupId` typed identifier (`Odyssey.Domain.Identity`, prefix `bkup_`, same `Uuid7` pattern as `SceneId`/`TokenId`/`AssetId`).
- `BackupRecords` DDL filled in to the full `ADR-012` section 8.7 column set (point-fix in `SqliteCampaignRepository.CreateSystemTables`, the table's DDL was reserved-but-empty since `007`, same pattern `010` already used for `SchemaHistory`).
- A real interrupted-backup test via a new standalone harness project (`Odyssey.Tests.Persistence.BackupKillHarness`), and a real corruption fixture.

### Out of scope, and why

- **Full backup composition** (`Assets/` + `checksums.json`, `ADR-012` section 8.6): the backlog's scope text for this task never mentions Full, only "snapshot creation via SQLite Backup API" and the recent/daily/weekly rotation of that snapshot. Every backup this task creates is Fast-composition.
- **Automatic backup on every `Open`/`Close`**: decided explicitly NOT to hook this into `Open`/`Close` (see section 18's decision log) — `CreateBackup` is an explicit API a future session-lifecycle/UI layer calls "at session start/end," which this revision does not write. Hooking it into `Open`/`Close` unconditionally would also silently change `ODY-S01-009`'s already-tested Close-path WAL-checkpoint behavior's performance characteristics without an explicit product decision to do so.
- **Snapshot-before-migration/GM-Override triggers** (`ADR-012` section 8.2 items 5-7): no migration runner or GM Override exists yet to trigger from; `CreateBackup` is directly callable by a future task once those exist, but this task does not wire an automatic call.
- **Emergency backup tier**: not populated by anything in this task's scope; the directory exists (already created by `007`) but nothing writes to it.
- **.odcamp export** (`ODY-S01-012`): different format/purpose, not touched.
- **Migration runner** (`ODY-S01-010` already closed narrowly; still not the full runner here).
- **Owner key storage / backup encryption**: already closed by `ADR-014` section 8 ("no backup encryption in MVP") — not reopened.
- **`SqliteSceneRepository.cs`/`SqliteSavingPipeline.cs`**: untouched — `BackupRecords` writes go through `SqliteBackupRepository`'s own short-lived connections, not the `ADR-012` section 5 event pipeline (backups are not part of that transactional group per section 5's own explicit list).

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Persistence/BackupRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/BackupManifest.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteBackupRepository.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCampaignRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/SqliteBackupRepositoryTests.cs
DotNet/Tests/Odyssey.Tests.Persistence.BackupKillHarness/*
DotNet/Odyssey.Core.sln
Tests/Metadata/test-catalog.json
docs/errors/ERROR_CODES.md
docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S01-011_Backups.md
docs/plans/active/ODY-S01-011_Backups.md
```

### Paths requiring explicit approval before editing

```text
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSceneRepository.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSavingPipeline.cs
docs/tasks/active/ODY-S01-007_Campaign_Storage_Foundation.md
docs/tasks/active/ODY-S01-008_Scene_And_Token_Minimal_Model.md
docs/tasks/active/ODY-S01-009_Saving_Pipeline.md
docs/tasks/active/ODY-S01-010_Migration_Registry_Baseline.md
```

## 6. Technical constraints

- Module ownership and dependency direction: `IBackupRepository` is an `Odyssey.Application.Persistence` port; `SqliteBackupRepository` is the `Odyssey.Persistence` implementation (`ADR-001` section 6.5, same pattern as `ICampaignRepository`/`ISceneRepository`).
- Authoritative-state and transaction boundary: backups are explicitly outside the `ADR-012` section 5 journal-projection transaction group; `BackupRecords` is written via its own short-lived connection, not the pipeline.
- Time / RNG rule: `IWallClock` only; `BackupId.NewId` is a pure function of `UtcInstant` (same `Uuid7` pattern as existing typed IDs).
- Unity / thread / lifetime rule: no new production dependency; `SqliteConnection.BackupDatabase` is part of `Microsoft.Data.Sqlite`, already IL2CPP-proven by `ODY-S01-007`'s preflight.
- Dependency / licensing rule: no new production dependency. `Odyssey.Tests.Persistence.BackupKillHarness` references only already-approved project references (`Odyssey.Domain`/`Odyssey.Application`/`Odyssey.Persistence`), no new NuGet package.
- Security / privacy / redaction rule: `backup-manifest.json` fields mirror `BackupRecord` (version strings, hashes, sizes) — no player/GM-private content.
- Performance or platform constraint: `Pooling=False` added to every SQLite connection string this task touches (production `SqliteBackupRepository` and, as a necessary point-fix, `SqliteCampaignRepository.OpenConnectionWithPragmaProfile`) — Microsoft.Data.Sqlite's default connection pooling was observed, during this task's own test development, to hold a native file handle open briefly past `Dispose()`, which raced this task's own corruption-fixture and restore-cleanup tests on Windows. See section 18's decision log.
- Other: Not applicable.

## 7. Expected behavior

### Scenario 1 — Manual backup and restore round-trip

**Given** an open campaign with a scene in it
**When** `CreateBackup` then `RestoreBackup` into a new directory
**Then** the restored copy has the same scene, and the original campaign is untouched.

### Scenario 2 — Interrupted backup never appears valid

**Given** a campaign large enough that a backup takes a measurable time
**When** the process performing `CreateBackup` is hard-killed mid-copy
**Then** no new Fast-tier backup directory is promoted, and the source database is unaffected.

### Scenario 3 — Rotation bounds storage

**Given** a rotation policy with small retention counts
**When** more backups are created than the policy retains
**Then** older backups (and their `BackupRecords` rows, for the Fast tier) are pruned.

### Scenario 4 — Corruption does not take backups down with it

**Given** a valid backup already exists
**When** the working `campaign.db` is corrupted in place
**Then** `Open` fails its integrity check, but `ListBackups`/`RestoreBackup` (filesystem-based, not dependent on the corrupted database) still find and restore the last valid backup.

### Required invariants

- A backup is never visible under its final name until it has passed integrity validation (temp → validate → atomic rename).
- `RestoreBackup` never writes into or overwrites the source campaign's own directory.

## 8. Deliverables

- Production code: `BackupRepositoryContracts.cs`, `BackupManifest.cs` (new); `SqliteBackupRepository.cs` (new); `SqliteCampaignRepository.cs` (point-fix: `BackupRecords` DDL, `Pooling=False`); `DomainIdentity.cs` (point-fix: `BackupId`); 3 new `ErrorCodes`.
- Tests: `SqliteBackupRepositoryTests.cs` (9 tests, `TC-PERSIST-014`–`022`); `Odyssey.Tests.Persistence.BackupKillHarness` (new console project, kill-test support only).
- Scripts / CI: None (existing scripts cover the new project).
- Configuration: `DotNet/Odyssey.Core.sln` gains the new harness project.
- Documentation: `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` (row 5), this task contract, its ExecPlan.
- Generated evidence or build artifacts: None persisted beyond section 17's recorded test output.
- Migration / recovery material: None — `BackupRecords`' filled-in DDL affects no existing shipped data.

## 9. Acceptance criteria

1. `CreateBackup` produces a backup via the SQLite Backup API (never `File.Copy` on the live database) that passes `PRAGMA quick_check`, with `backup-manifest.json`/`manifest.json`/`campaign.db` present under the final path and no leftover temp directory (`TC-PERSIST-015`).
2. `RestoreBackup` reproduces the campaign's data into a brand-new directory and never modifies the source campaign (`TC-PERSIST-014`).
3. A real hard-killed mid-copy backup never appears as a promoted Fast-tier directory, and the source campaign remains intact (`TC-PERSIST-016`).
4. Fast/Daily/Weekly rotation prunes to configured retention counts, not unbounded growth; Daily/Weekly promote at most once per calendar bucket (`TC-PERSIST-017`, `TC-PERSIST-018`).
5. After corrupting the working `campaign.db`, `Open` fails its integrity check while `ListBackups`/`RestoreBackup` still succeed off the last valid backup (`TC-PERSIST-019`).
6. `RestoreBackup`/`CreateBackup` failure paths (`unknown BackupId`, `destination already exists`, `invalid reason`) return typed `Error`s, not raw exceptions (`TC-PERSIST-020`–`022`).
7. All prior `TC-PERSIST-*` tests continue to pass unmodified.
8. `SqliteSceneRepository.cs`/`SqliteSavingPipeline.cs` are untouched.
9. All required validation commands (section 10) pass.

The task is not complete while any mandatory criterion is unverified, failed, or silently deferred.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-PERSIST-014` | .NET / `dotnet test` | Backup → restore round-trip; source untouched | Pass |
| `TC-PERSIST-015` | .NET / `dotnet test` | Backup API usage; no leftover temp; integrity-clean final copy | Pass |
| `TC-PERSIST-016` | .NET / `dotnet test` | Real `Process.Kill()` mid-copy never promotes a partial backup | Pass |
| `TC-PERSIST-017` | .NET / `dotnet test` | Fast-tier rotation pruning | Pass |
| `TC-PERSIST-018` | .NET / `dotnet test` | Daily/Weekly promotion-once-per-bucket + pruning | Pass |
| `TC-PERSIST-019` | .NET / `dotnet test` | Corruption fixture: last valid backup still restorable | Pass |
| `TC-PERSIST-020` | .NET / `dotnet test` | Unknown `BackupId` → typed `NotFound` | Pass |
| `TC-PERSIST-021` | .NET / `dotnet test` | Non-empty destination → typed `RestoreFailed` | Pass |
| `TC-PERSIST-022` | .NET / `dotnet test` | Invalid reason → typed `CreateFailed` | Pass |

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
- Unity editor or Player profile: Not applicable — no new dependency, no new platform-sensitive API beyond `SqliteConnection.BackupDatabase`, already covered by `ODY-S01-007`'s IL2CPP preflight (same `Microsoft.Data.Sqlite` package).
- Scripting backend: Not applicable.
- Network topology or database fixture: Local SQLite files per test, `Path.GetTempPath()`-based, cleaned up per test; the kill test seeds ~150,000 `DomainEvents` rows directly via raw SQL (bypassing the pipeline, fixture setup only) to give the SQLite Backup API copy a real, measurable duration to kill mid-way through, matching `SP-02`'s own interrupted-backup methodology.
- Other: None.

### Validation not required by this task

- A second IL2CPP compatibility preflight — no new NuGet dependency, no new platform-sensitive API beyond one already proven.
- Full backup composition testing (Assets/checksums.json round-trip) — Full backup is not implemented in this revision (see section 5).

## 11. Compatibility, migration, and rollback

- Compatibility impact: `IBackupRepository` is an entirely new port — no existing interface signature changed. `BackupRecords`' DDL is filled in from `007`'s reserved-but-empty placeholder; no shipped campaign data exists to migrate.
- Version fields affected: None.
- Migration or upcaster: None required.
- Forward / backward behavior: Not applicable — pre-release, no compatibility surface yet.
- Rollback method: Revert the branch; no persisted data depends on the new columns/tables.
- Data-loss risk and protection: None introduced; `CreateBackup` only ever writes new files, `RestoreBackup` only ever writes into a brand-new directory.
- Recovery rehearsal required: Yes — performed via `TC-PERSIST-016` (real kill) and `TC-PERSIST-019` (real corruption fixture).

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None (new project references only) | — | `Odyssey.Tests.Persistence.BackupKillHarness` references `Odyssey.Domain`/`Odyssey.Application`/`Odyssey.Persistence` project outputs, not a new NuGet package | — | No new license review needed — same already-approved production assemblies |

## 13. Security, privacy, and hidden information

- Data classes handled: Backup/version/hash metadata only — no player/GM-private content in this vertical slice.
- Trust boundaries: Not applicable — local filesystem only, no network boundary.
- Authorization / audience checks: Not applicable.
- Redaction requirements: Not applicable.
- Log-safe fields: `BackupRecord`/`BackupManifest` fields are non-sensitive by `ADR-012` section 8.7's own definition; error factories follow the established no-raw-exception/no-raw-path convention.
- Abuse / malformed input limits: `reason` string length-checked (max 96 chars) like existing `sceneName`/`campaignName` checks.
- Security tests: Not applicable beyond the existing typed-error tests.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: Checked against `PLANS.md` section 1.2's triggers individually. This task introduces a new Application port (`IBackupRepository`) and a new persisted JSON contract (`backup-manifest.json`/`BackupManifestV1Codec`) — both explicit ExecPlan triggers on their own. It also introduces a genuinely new file-I/O path (temp-copy → validate → atomic-rename across two directory levels) with real data-loss/corruption risk if mishandled, which `PLANS.md` section 1.2 separately flags ("meaningful data-loss... risk"). This is more architecturally invasive than `ODY-S01-010` (which touched only one existing table's DDL and stayed within `Odyssey.Persistence`) though less invasive than `ODY-S01-009` (no existing port's signature changes here, `IBackupRepository` is additive). `PLANS.md` section 1.1's Brief-plan bar ("does not change... persisted format" and "no migration or recovery procedure is required") is not met: this task both adds a new persisted format (`backup-manifest.json`) and requires a real recovery rehearsal (the kill test and corruption fixture).
- ExecPlan path: `docs/plans/active/ODY-S01-011_Backups.md`
- Expected pull request count: 1
- Milestone or sequencing constraints: None beyond `009` already merged (backlog's stated dependency).

## 15. Documentation and versioning impact

- Documents that must change: `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` (row 5 only).
- Documents that must not change: any ADR, `007`–`010` task contracts.
- Application version change: No.
- Schema / format / contract / manifest / protocol / ruleset version change: `backup-manifest.json` is a new contract type (`odyssey.persistence.backupmanifest`, version 1) — new, not a change to an existing one; `CampaignFormatVersion`/`DatabaseSchemaVersion` themselves are unchanged.
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

- `Packages/com.odyssey.application/Runtime/Persistence/BackupRepositoryContracts.cs` — new: `IBackupRepository`, `BackupRecord`, `BackupRotationPolicy`.
- `Packages/com.odyssey.application/Runtime/Persistence/BackupManifest.cs` — new: `BackupManifest`, `BackupManifestV1Codec`.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` — 3 new `PersistenceFailures` factories.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — 3 new codes.
- `Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs` — new `BackupId`.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteBackupRepository.cs` — new implementation.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCampaignRepository.cs` — `BackupRecords` DDL filled in; `Pooling=False` added.
- `DotNet/Tests/Odyssey.Tests.Persistence/SqliteBackupRepositoryTests.cs` — new, 9 tests.
- `DotNet/Tests/Odyssey.Tests.Persistence.BackupKillHarness/` — new console project.
- `DotNet/Odyssey.Core.sln` — new project added.
- `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json` — registry additions.
- `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` — row 5 status.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\restore.ps1` | Passed | All 12 projects restored, including the new harness. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS` after normalizing CRLF a scripted Python edit introduced in `SqliteBackupRepository.cs` (same recurring class of issue as prior tasks in this session). |
| `.\scripts\verify-test-structure.ps1` | Passed | `TC-ARCH-001`/`TC-ARCH-002` all PASS once this task contract existed (expected sequencing — the catalog entries reference `ODY-S01-011` by task ID). |
| `.\scripts\test-fast.ps1` | Passed | `Odyssey.Tests.Persistence.dll`: 36/36 (up from 27); `Odyssey.Tests.Unit.dll`: 84/84; `Odyssey.Tests.Architecture.dll`: 2/2; `Odyssey.Tests.Domain.dll`/`Odyssey.Tests.Contracts.dll`: 1/1 each. |
| `.\scripts\check-repository-policy.ps1` | Passed | `REPO-POLICY-005 PASS ErrorCode registry is complete and machine-checkable` (all 3 new codes registered with real test references). |
| `.\scripts\verify-repository.ps1` | Passed | — |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `CreateBackup_UsesSqliteBackupApi_NotRawFileCopy_TempNeverVisibleUnderFinalName` |
| AC-2 | Passed | `CreateBackup_ThenRestoreIntoSeparateCopy_DataMatchesOriginal_OriginalUntouched` |
| AC-3 | Passed | `CreateBackup_KilledMidCopy_NeverPromotesPartialBackup_SourceUntouched` (real `Process.Kill`, seeded ~150K-row DB, killed at 30% of a measured baseline duration) |
| AC-4 | Passed | `Rotation_FastTier_PrunesBeyondRetentionCount`, `Rotation_DailyAndWeeklyTiers_PromoteOncePerBucket_AndPruneBeyondRetentionCount` |
| AC-5 | Passed | `CorruptedMainDatabase_LastValidBackupStillRestorable_BackupFilesUntouchedByCorruption` |
| AC-6 | Passed | `RestoreBackup_UnknownBackupId_ReturnsTypedNotFound_NoRawException`, `RestoreBackup_DestinationAlreadyExistsAndNonEmpty_ReturnsTypedRestoreFailed`, `CreateBackup_WithInvalidReason_ReturnsTypedCreateFailed_NoRawException` |
| AC-7 | Passed | 27 pre-existing `TC-PERSIST-*` tests all still pass (36 total, 0 failed) |
| AC-8 | Passed | `git diff --name-status` confirms neither file touched |
| AC-9 | Passed | See Validation results above |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: `artifacts/bin/Odyssey.Persistence/debug/Odyssey.Persistence.dll`, `artifacts/bin/Odyssey.Tests.Persistence.BackupKillHarness/debug/Odyssey.Tests.Persistence.BackupKillHarness.dll`.
- Checksums: Not recorded — debug local build.
- Test or quality report: `dotnet test` console output (section above).

### Known limitations

- Full backup composition (`Assets/` + `checksums.json`) is not implemented — see section 5.
- Automatic backup on `Open`/`Close` is not wired — `CreateBackup` is an explicit API; a future session-lifecycle task decides when to call it "at session start/end."
- The Daily/Weekly "promote once per calendar bucket" logic scans every existing directory in that tier and re-reads its `backup-manifest.json` on every `CreateBackup` call — fine at the retention counts this task defaults to (7/4), but would need an index if retention counts grew much larger.
- `BackupRotationPolicy` is a constructor parameter on `SqliteBackupRepository`, not yet wired into `CampaignSettings` JSON as `05_Persistence` section 21.3 envisions ("Политика может быть изменена в CampaignSettings") — the policy is configurable, just not through that specific storage location yet.

### Follow-up tasks

- None assigned — Full backup composition and `CampaignSettings`-driven rotation policy remain candidates for a future task if the product owner wants them before `SLICE-01` closes.

### Self-review summary

- Scope review: Manual backup, rotation, restore-into-copy, and a corruption fixture — nothing from the explicit "не входит" list implemented.
- Architecture review: New port lives in Application, implementation in Persistence; no existing port's signature changed; `SqliteSceneRepository.cs`/`SqliteSavingPipeline.cs` untouched.
- Test review: Every acceptance criterion has a dedicated, real (not simulated) test; the kill test uses an actual subprocess and `Process.Kill`, mirroring `SP-02`'s and `ODY-S01-009`'s established methodology.
- Security/privacy review: No sensitive data touched; existing no-raw-exception conventions preserved.
- Documentation/version review: Only the registries and one backlog row required updates; no ADR or version field touched.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-08-24 — Decision: `CreateBackup` is an explicit API only, not hooked into `Open`/`Close`. Rationale: the backlog's "backup at test-session start/end" phrasing describes *when* a caller should invoke it, not that persistence itself must invoke it automatically; no session-lifecycle/UI layer exists yet in this revision to own that decision, and unconditionally hooking it into `Close` would silently change `ODY-S01-009`'s already-tested close-path behavior without an explicit product decision to do so. Authority: task contract section 5.
- 2026-08-24 — Decision: `ListBackups`/`RestoreBackup` read `backup-manifest.json` from the filesystem, not the campaign's own `BackupRecords` table. Rationale: a disaster-recovery listing method that depends on the very database a corruption scenario would make unreadable defeats its own purpose — proven necessary by this task's own corruption-fixture test. `BackupRecords` is still written (ADR-012 section 8.4 step 8 compliance) as an in-app audit trail. Authority: `SqliteBackupRepository.cs` class remarks.
- 2026-08-24 — Decision: added `Pooling=False` to `SqliteCampaignRepository.OpenConnectionWithPragmaProfile`'s connection string, beyond the originally-planned `SqliteBackupRepository`-only change. Rationale: this task's own corruption-fixture and restore-cleanup tests genuinely raced Microsoft.Data.Sqlite's default connection pooling holding a native file handle open past `Dispose()` on Windows — the same class of issue `SP-02`'s harness and the `ODY-S01-009` recovery harness already worked around with the same flag. A minimal, necessary point-fix to a file already in this task's allowed-paths list.
- 2026-08-24 — Decision: rotation numbers (10 Fast / 7 Daily / 4 Weekly) taken verbatim from `05_Persistence` section 21.3, not invented, per the task's own instruction to prefer the documented baseline over a fresh choice.

### Approved task changes

- None.
