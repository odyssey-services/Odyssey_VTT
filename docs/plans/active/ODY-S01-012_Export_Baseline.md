# ODY-S01-012 — Export Baseline (`.odcamp`)

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s01-012-export-baseline`
**Pull request:** Draft — [#34](https://github.com/odyssey-services/Odyssey_VTT/pull/34)
**Last updated:** 2026-08-24 UTC

## 1. Purpose and user-visible outcome

A campaign can be exported into a single `.odcamp` file and imported from one into a brand-new local campaign copy, with the same integrity guarantees `ODY-S01-011`'s backups already have, and no risk of silently merging into or overwriting an existing campaign.

## 2. Task contract

- Goal / acceptance criteria / requirement IDs / scope / authorities / validation commands: see `docs/tasks/active/ODY-S01-012_Export_Baseline.md` sections 1, 3, 5, 9, 10.

## 3. Current state

- `05_Persistence` section 27 already fully specifies the `.odcamp` format and its export/import workflows — used verbatim.
- `SqliteBackupRepository.CreateBackup` already implements the identical database-copy-and-validate core export needs.

## 4. Proposed approach

Extract `SqliteSnapshotCopy.CreateValidated(sourceDbPath, destinationDbPath)` from `SqliteBackupRepository.CreateBackup` — SQLite Backup API copy, `PRAGMA quick_check`, read `CampaignRevision`/`EventSequence`, SHA-256 hash/size. Refactor `CreateBackup` to call it (verified behavior-preserving by its own full test suite still passing). Build `SqliteExportRepository.ExportCampaign` on top of the same helper: stage `campaign.db` (via the helper) + `manifest.json` + referenced assets (read from `AssetManifestEntries` in the just-validated snapshot, not the live db) + `checksums.json` + `export-manifest.json` into a temp staging directory, zip it to a temp filename, verify the zip's `campaign.db` entry hash matches, atomically rename to the final `.odcamp` path. `ImportCampaign` extracts to a temp folder with path-traversal/absolute-path guards checked before extraction, validates `manifest.json` (existing codec), checks `DatabaseSchemaVersion` compatibility (typed error, no migration), validates `campaign.db` (`quick_check`), verifies the checksum against `export-manifest.json`, then copies into a brand-new `imported-<CampaignId>` directory (never merging with an existing one).

## 5. Milestones

### M1 — Shared helper extracted, refactor verified

- [x] `SqliteSnapshotCopy` written; `SqliteBackupRepository.CreateBackup` refactored to use it.
- [x] All 36 pre-existing Persistence tests still pass unmodified.

### M2 — Export/import core behavior proven

- [x] `TC-PERSIST-023`/`024` (round-trip, shared-snapshot-reuse proof) pass.
- [x] `TC-PERSIST-025`/`026`/`027`/`029` (typed failure paths: no-merge, path-traversal, invalid manifest, version mismatch) pass.
- [x] `TC-PERSIST-028`/`030` (archive contents, no-overwrite) pass.

### M3 — Repository policy and registries consistent

- [x] `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json` updated; all 6 validation scripts pass.
- [x] `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` row 6 updated.

## 6. Progress log

- 2026-08-24 — Preflight confirmed `007`–`011` merged to `main` (`776de6c`).
- 2026-08-24 — Read `ADR-011` section 3.2, `05_Persistence` section 27 in full (exact 5-file archive contents, 9-step export, 9-step import, security requirements) -- used verbatim per the task's own instruction.
- 2026-08-24 — Extracted `SqliteSnapshotCopy` from `SqliteBackupRepository.CreateBackup`; confirmed the refactor was behavior-preserving via the existing 36-test suite before writing any new code.
- 2026-08-24 — Implemented `IExportRepository`/`SqliteExportRepository`/`ExportManifest`/`ExportManifestV1Codec`; used `System.IO.Compression.ZipFile` (BCL, no new dependency).
- 2026-08-24 — Wrote `SqliteExportRepositoryTests.cs` (8 tests); first run 43/44 (one test asserted the wrong typed-error code due to a fixture that failed structural JSON validation instead of domain manifest validation); fixed the fixture, all 44 green.
- 2026-08-24 — Ran full validation sequence; filled registries; wrote this task contract and ExecPlan.

## 7. Decisions

- 2026-08-24 — Decision: reuse `SqliteSnapshotCopy` rather than duplicate the database-copy logic. Rationale: explicit task instruction; also avoids two parallel implementations of the same SQLite Backup API + integrity-check pattern drifting apart over time. Authority: task contract section 5/18.
- 2026-08-24 — Decision: no real kill test for export in this revision. Rationale: the same underlying temp→validate→atomic-rename core is already kill-tested by `ODY-S01-011`. Authority: task contract section 10/18.

## 8. Discoveries and deviations

- A self-authored test initially asserted the wrong typed-error code because its fixture (raw truncated JSON) failed at an earlier structural-validation layer than the domain-level manifest-field check it meant to exercise. Caught by the test failing, not by review; fixed by using a structurally-valid-but-field-incomplete JSON fixture instead.
- No CRLF/line-ending issue occurred this task (unlike several prior tasks in this session) since no scripted regex edit was used -- all files were written/edited directly via the Write/Edit tools.

## 9. Validation and acceptance evidence

See task contract section 17 for full command output summaries and the acceptance-criteria table.

## 10. Recovery and rollback

Revert the branch; no persisted `.odcamp` files or campaign data exist yet that depend on this task's code, so no data migration or rollback procedure beyond a normal git revert is needed.

## 11. Open questions and blockers

- None blocking. Zip-bomb protection and per-asset checksum verification on import are open hardening questions for a future task if `.odcamp` import becomes a route for untrusted files.

## 12. Outcome and follow-up

Delivered: `.odcamp` export/import per `05_Persistence` section 27's exact format and workflow, reusing `ODY-S01-011`'s snapshot mechanism via a new shared helper, with manifest/version/archive-safety validation and a strict no-automatic-merge import policy. All prior Persistence tests remain green. Follow-up: none assigned yet (see task contract section 17's "Follow-up tasks").
