# ODY-S01-014 - Traceability Matrix and Quality Report

**Parent task:** `docs/tasks/active/ODY-S01-014_Acceptance_And_Closure_Gate.md`
**Prepared:** 2026-08-25 UTC
**Rehearsal method:** Full validation sequence and `dotnet test` re-run against the working checkout at commit `dc0887c` (`main`, includes owner-merged PR #35 — the last of `ODY-S01-007`–`013`), performed fresh for this report rather than assumed from prior task reports. Not a separate fresh `git clone` (unlike `ODY-S00-010`'s M1 rehearsal) — the working checkout was already a clean, unmodified fast-forward of `origin/main` at the moment this rehearsal ran (`git status --short` empty, `git log -1` = `dc0887c`), so re-running the same commands against it is equivalent evidence without the extra clone step; this difference from the `ODY-S00-010` precedent is deliberate and stated here, not silently substituted.

This report does not accept any of `ODY-S01-007`–`013`'s own task-contract "Validation results" tables on faith — every Pass below cites either a specific test method run in this rehearsal or a specific script's PASS line printed in this rehearsal.

## 1. SLICE-01 exit-criteria checklist (roadmap section 10.6, quoted verbatim)

| # | Exit criterion (verbatim, translated) | Owning task(s) | Status | Evidence |
|---|---|---|---|---|
| 1 | Scene state survives a restart. | `007`, `008`, `009`, `013` | Pass | `VerticalSliceIntegrationTests.NineStepSlice_CreateImportSceneTokensMoveCloseReopenVerifyRestore_AllStepsSucceed` (`ODY-S01-013`) — re-run in this rehearsal, green — closes a real campaign, reopens it with a fresh `SqliteCampaignRepository` instance, and asserts both tokens' post-move positions and the registered asset's hash are unchanged. Also: `SqliteCampaignRepositoryTests.CreateThenOpen_ManifestRoundTrips` (`007`), `SqliteSceneRepositoryTests.CreateTwoTokens_ThenMoveThem_PersistsIndependentPositions` (`008`). |
| 2 | A confirmed transaction is not lost. | `009` | Pass | `SqliteSavingPipelineTests.CreateScene_CommitsProjectionEventAndAppliedCommand_AsOneConsistentGroup` — asserts the projection row, its `DomainEvents` row, and its `AppliedCommands` row all exist together after a successful commit. `SqliteSavingPipelineTests.Open_AfterHardKillMidTransaction_RecoversCleanly_KilledWriteNeverAppears` — a real `Process.Kill()` mid-transaction proves the *already-committed* baseline write survives the kill (the criterion's "confirmed" half); the killed, uncommitted write is separately proven absent (criterion 3). |
| 3 | A failed transaction leaves no partial state. | `009` | Pass | `SqliteSavingPipelineTests.CreateToken_OnRejectedCommand_LeavesNoEventOrAppliedCommandRow` — a rejected `CreateToken` (nonexistent scene) is asserted to leave zero rows in `AppliedCommands`, `DomainEvents`, and `Token`. |
| 4 | A backup restores into a separate copy. | `011`, `013` | Pass | `SqliteBackupRepositoryTests.CreateBackup_ThenRestoreIntoSeparateCopy_DataMatchesOriginal_OriginalUntouched` — asserts the restored path is not the original path, the restored copy's data matches, and the original remains openable and untouched. Also exercised end-to-end (not just in isolation) by `VerticalSliceIntegrationTests`' step 9. |
| 5 | Corruption of the main database does not destroy the last valid copy. | `011` | Pass | `SqliteBackupRepositoryTests.CorruptedMainDatabase_LastValidBackupStillRestorable_BackupFilesUntouchedByCorruption` — corrupts `campaign.db` bytes in place, asserts `Open()` fails its quick integrity check (`PersistenceIntegrityCheckFailed`), then asserts `ListBackups`/`RestoreBackup` (filesystem-based, independent of the corrupted database) still find and successfully restore the last valid backup. |
| 6 | Migrations are versioned and tested. | `010` | Pass, narrow scope explicitly carried forward, not reopened | `MigrationRegistryTests.Registry_IsWellFormed_NoDuplicateIds_MonotonicOrder_ChecksumPresent` and `Registry_InitialEntry_IsIdentityMigration_FromEqualsToEqualsCurrentSchemaVersion` prove the registered-migrations list itself is well-formed and versioned; `Create_InsertsExactlyOneSchemaHistoryRow_MatchingDatabaseSchemaVersionInManifest`/`Open_DoesNotDuplicateOrRewriteInitialSchemaHistoryRow` prove `SchemaHistory` records a real, versioned migration (`0001_Initial`) on every campaign. **This is the narrow interpretation `SLICE-01_IMPLEMENTATION_BACKLOG.md` section 2.1 already decided and `ODY-S01-010`'s task contract already recorded**: a migration registry baseline with one identity migration, not the full `ADR-013` runner (temp-copy execution, rollback, read-only compatibility mode — none of that exists yet). This report does not reopen that decision; it states it explicitly here because the exit criterion's wording ("миграции имеют версию и тест") is satisfied at exactly the level this decision scoped it to, and a reader of this report should not have to cross-reference `010`'s contract to know that. |
| 7 | Windows paths are not required as part of the portable format. | `008`, `011`, `012`, `013` | Pass | `SqliteSceneRepositoryTests.RegisterAsset_CopiesFileIntoAssetsObjects_ComputesHashAndSize_StoresOnlyRelativePath` asserts the recorded `RelativePath` does not contain the absolute import source path (`ADR-011` section 4.2). `VerticalSliceIntegrationTests` re-asserts the same property end-to-end. The `.odcamp` container (`012`) and backup (`011`) formats both carry only `manifest.json`/`campaign.db`/relative asset paths — no drive letters, UNC paths, or OS-specific path separators are persisted in any authoritative record (confirmed by code inspection of `SqliteExportRepository`/`SqliteBackupRepository`: all `RelativePath` values are built with the forward-slash convention `ADR-011` mandates, converted to the local `Path.DirectorySeparatorChar` only at the filesystem-I/O boundary, never persisted that way). |
| 8 | `GATE-A — Architecture Ready` is closed for the local-storage portion. | `007`–`013` | Pass, scoped exactly to "local storage works" | `02_MVP_Scope_Odyssey_VTT_v0.10.md` section 7 defines `GATE-A` as five sub-criteria: Core independent of Unity; command/event model defined; **local storage works**; transport abstracted; hidden data filtered before sending. Roadmap section 10.6's criterion 8 explicitly narrows to "в части локального хранения" (the local-storage portion) — the other four `GATE-A` sub-criteria are `ODY-S00-*`/future-slice scope, not re-verified or re-claimed here. "Local storage works" is evidenced by the full `TC-PERSIST-001`–`031` suite (see section 2) and, concretely, by `VerticalSliceIntegrationTests` proving create/import/scene/tokens/move/close/reopen/verify/restore all work together end-to-end with zero new production code needed to make them do so. |

All 8 of 8 exit criteria are Pass, with criterion 6 explicitly annotated as carrying forward an already-accepted narrow-scope decision (not reopened) and criterion 8 explicitly scoped to only the local-storage portion of `GATE-A` (per the exit criterion's own wording).

**No gap was found under section 6 of this task's own ТЗ.** Every criterion has direct, re-run evidence; none required inventing coverage or accepting an old report on faith.

## 2. TestCase traceability matrix (`ODY-S01-007`–`013` entries in `Tests/Metadata/test-catalog.json`)

All entries below were introduced by `ODY-S01-007`–`013` (`TC-PERSIST-001` through `TC-PERSIST-031`). This rehearsal re-ran the full `Odyssey.Tests.Persistence` suite fresh (not reconciled from a prior report) at commit `dc0887c`: **45/45 passed, 0 failed** (`dotnet test DotNet/Odyssey.Core.sln --no-build`, this rehearsal). Every TestCaseId below maps to Pass (aggregate) unless individually re-run and noted otherwise.

| TestCaseId | Owning task | Behavior proven | Status |
|---|---|---|---|
| `TC-PERSIST-001`–`004` | `007` | Campaign create/open, PRAGMA profile, manifest round-trip, manifest/db conflict detection | Pass (aggregate) |
| `TC-PERSIST-005`–`007` | `008` | Scene/token creation, move, list, asset registration | Pass (aggregate) |
| `TC-PERSIST-008`–`011` | `009` | Atomic commit group, idempotent redelivery, safe-close WAL checkpoint, real kill recovery | Pass (aggregate); `TC-PERSIST-011` individually re-run in this rehearsal (`Open_AfterHardKillMidTransaction_RecoversCleanly_KilledWriteNeverAppears`, ~1s, real `Process.Kill`) |
| `TC-PERSIST-012`–`013` | `010` | Migration registry well-formedness, `SchemaHistory` row on Create/Open | Pass (aggregate) |
| `TC-PERSIST-014`–`022` | `011` | Backup round-trip, Backup-API usage, real kill test, rotation, corruption fixture, typed failure paths | Pass (aggregate); `TC-PERSIST-016` individually re-run in this rehearsal (`CreateBackup_KilledMidCopy_NeverPromotesPartialBackup_SourceUntouched`, real `Process.Kill` against a ~150K-row seeded database) |
| `TC-PERSIST-023`–`030` | `012` | Export/import round-trip, snapshot reuse proof, no-merge, path-traversal rejection, manifest/version validation, no-overwrite | Pass (aggregate) |
| `TC-PERSIST-031` | `013` | The full roadmap section 10.5 nine-step sequence, end-to-end, in order | Pass (aggregate); individually re-run in this rehearsal in isolation (`dotnet test --filter VerticalSliceIntegrationTests`, 1/1 passed) |

Coverage: **31 of 31 `ODY-S01-007`–`013` TestCase IDs (100%) map to Pass** in this rehearsal. 0 Not run, 0 Deferred, 0 Failed.

## 3. Quality report — commands run in this rehearsal

All commands below were run against the working checkout at commit `dc0887c` (`main`, clean, unmodified at the time of the run).

| Command | Result | Key evidence |
|---|---|---|
| `.\scripts\restore.ps1` | Pass | All projects restored, exit 0 |
| `.\scripts\verify-format.ps1` | Pass | `FORMAT-001 PASS repository text formatting checks passed` |
| `.\scripts\verify-test-structure.ps1` | Pass | `TC-ARCH-001 PASS`; four controlled-invalid fixtures correctly rejected |
| `dotnet build DotNet\Odyssey.Core.sln -c Debug` | Pass | 0 warnings, 0 errors |
| `dotnet test DotNet\Odyssey.Core.sln --no-build -c Debug` | Pass | 133/133 passed, 0 failed (Contracts 1, Domain 1, Unit 84, Architecture 2, Persistence 45) |
| `.\scripts\check-repository-policy.ps1` | Pass | `REPO-POLICY-001` through `REPO-POLICY-005` PASS; `TC-CI-001`–`012` PASS; `Repository policy check passed` |
| `.\scripts\verify-repository.ps1` | Pass | `REPOSITORY-VERIFY PASS repository checks passed`; SDK `10.0.302` |

No finding, no drift, and no rehearsal failure occurred during this run (unlike `ODY-S00-010`'s rehearsal, which hit two Unity-batchmode-drift-related findings not applicable here since this rehearsal never invokes the Unity Editor).

## 4. Unrun / non-required checks

- A separate fresh `git clone` rehearsal (the `ODY-S00-010` precedent's method): not performed — see this report's header for why the already-clean working checkout is equivalent evidence here, stated explicitly rather than silently substituted.
- Unity Editor / IL2CPP re-verification: not re-run in this rehearsal. `ODY-S01-007`'s own task contract already performed and recorded a real, dedicated IL2CPP compatibility preflight for the `Microsoft.Data.Sqlite`/`SQLitePCLRaw.bundle_e_sqlite3` dependency chain every later `007`–`013` task built on without needing to repeat it (no new NuGet dependency was introduced by `008`–`013`). Re-verified by inspection of each task's own "Validation not required by this task" section, not by re-running Unity here.
- A Unity Play Mode run of the roadmap section 10.5 nine-step sequence: not performed. `ODY-S01-013`'s own task contract already flagged this as an open question (its own "Out of scope" section 5) rather than silently assuming a `dotnet test` NUnit run satisfies a literal Unity-context requirement — this report does not resolve that open question either; it is carried forward as-is, not reopened or silently closed.

## 5. SLICE-01 exit-criteria final checklist

| # | Criterion | Result |
|---|---|---|
| 1 | Scene state survives a restart | ✅ Pass |
| 2 | A confirmed transaction is not lost | ✅ Pass |
| 3 | A failed transaction leaves no partial state | ✅ Pass |
| 4 | A backup restores into a separate copy | ✅ Pass |
| 5 | Corruption of the main database does not destroy the last valid copy | ✅ Pass |
| 6 | Migrations are versioned and tested | ✅ Pass (narrow scope explicitly carried forward, not reopened) |
| 7 | Windows paths are not required as part of the portable format | ✅ Pass |
| 8 | `GATE-A — Architecture Ready` closed for the local-storage portion | ✅ Pass (scoped exactly to local storage) |

All 8 of 8 `SLICE-01` exit criteria are Pass with real, re-run evidence.

## 6. Owner acceptance

**Not yet recorded in this document.**

Per this task's own ТЗ instruction (section 10): the formal owner-acceptance statement (date, explicit confirmation) is deliberately not written here. It will be added by a separate, small, point-fix commit after the product owner explicitly confirms acceptance of this report and the parent task contract — the same pattern `ODY-S01-007`'s status-sync fix and `ODY-S00-010`'s section 7 both used, but sequenced here so the acceptance statement is never written ahead of the actual confirmation.
