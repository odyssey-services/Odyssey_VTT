# ODY-S01-011 — Backups

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s01-011-backups`
**Pull request:** Not yet opened
**Last updated:** 2026-08-24 UTC

## 1. Purpose and user-visible outcome

A campaign can be manually snapshotted at any time via the SQLite Backup API, retained under a recent/daily/weekly rotation policy, and restored into a brand-new campaign copy — including after the working database has been corrupted.

## 2. Task contract

- Goal / acceptance criteria / requirement IDs / scope / authorities / validation commands: see `docs/tasks/active/ODY-S01-011_Backups.md` sections 1, 3, 5, 9, 10.

## 3. Current state

- `BackupRecords` existed only as `ODY-S01-007` placeholder DDL, no row ever written.
- `Backups/Fast|Daily|Weekly|Full|Emergency` directories already created by `Create()`.
- `ADR-012` section 8's snapshot contract had only been proven at the `SP-02` spike level, never in production code.

## 4. Proposed approach

`SqliteBackupRepository.CreateBackup`: allocate `BackupId` → `SqliteConnection.BackupDatabase` from a read-only source connection into a temp directory under `Backups/Fast/.tmp-<id>/` → open the copy read-only, run `PRAGMA quick_check` and read `CampaignRevision`/`MAX(EventSequence)` → compute SHA-256 hash + size → copy `manifest.json` alongside → write `backup-manifest.json` via a new hand-written `BackupManifestV1Codec` → `Directory.Move` the temp directory to its final `Backups/Fast/<id>/` name (atomic on the same volume) → insert a `BackupRecords` audit row → promote into `Backups/Daily/`/`Backups/Weekly/` if no backup exists yet for the current UTC calendar day / ISO week → prune each tier to its configured retention count.

`ListBackups`/`RestoreBackup` deliberately read `backup-manifest.json` files directly from the `Backups/` tree rather than querying `BackupRecords` — proven necessary once the corruption-fixture test needed backup discovery to work even when `campaign.db` itself is unreadable.

Rotation policy defaults (10 Fast / 7 Daily / 4 Weekly) come verbatim from `05_Persistence` section 21.3, passed as a constructor parameter (`BackupRotationPolicy`), not yet wired into `CampaignSettings` (documented as a known limitation).

## 5. Milestones

### M1 — Contracts and DDL compile

- [x] `IBackupRepository`, `BackupRecord`, `BackupRotationPolicy`, `BackupManifest`/`BackupManifestV1Codec`, `BackupId` written and compiling.
- [x] `BackupRecords` DDL filled in to the full ADR-012 section 8.7 column set.

### M2 — Core backup/restore/rotation behavior proven

- [x] `TC-PERSIST-014`/`015` (round-trip, Backup API usage) pass.
- [x] `TC-PERSIST-016` (real kill) passes.
- [x] `TC-PERSIST-017`/`018` (rotation) pass.
- [x] `TC-PERSIST-019` (corruption fixture) passes.
- [x] `TC-PERSIST-020`–`022` (typed failure paths) pass.

### M3 — Repository policy and registries consistent

- [x] `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json` updated; all 6 validation scripts pass.
- [x] `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` row 5 updated.

## 6. Progress log

- 2026-08-24 — Preflight confirmed `007`–`010` merged to `main` (`6300579`).
- 2026-08-24 — Read `ADR-012` sections 8/9, `05_Persistence` section 21, `SP-02` report sections 2.3/2.5; confirmed rotation baseline numbers and interrupted-backup methodology to reuse.
- 2026-08-24 — Designed and implemented `IBackupRepository`/`SqliteBackupRepository`; decided against Full backup composition and automatic `Open`/`Close` hooking (documented in task contract section 18).
- 2026-08-24 — Built `Odyssey.Tests.Persistence.BackupKillHarness`; wrote `SqliteBackupRepositoryTests.cs`. First test run: 6/9 new tests failed with file-lock `IOException`s traced to Microsoft.Data.Sqlite connection pooling; fixed by adding `Pooling=False` to all new connection strings and, as a necessary point-fix, to `SqliteCampaignRepository.OpenConnectionWithPragmaProfile`.
- 2026-08-24 — Fixed a self-contradictory assertion in the kill test (asserted no `.tmp-` directory could remain, contradicting the test's own comment that this is acceptable litter); removed the incorrect assertion, kept the real one (no *promoted* partial backup).
- 2026-08-24 — All 36 Persistence tests green (27 pre-existing + 9 new); ran full validation sequence, fixed a CRLF issue from a scripted edit, filled registries, wrote this task contract and ExecPlan.

## 7. Decisions

- 2026-08-24 — Decision: filesystem-based (not `BackupRecords`-based) backup discovery for `ListBackups`/`RestoreBackup`. Rationale: disaster recovery must not depend on the database it is recovering from. Authority: task contract section 18.
- 2026-08-24 — Decision: no Full backup composition, no automatic Open/Close hook. Rationale: not requested by the backlog's scope text; both are legitimate future extensions, not silently dropped. Authority: task contract section 5/18.

## 8. Discoveries and deviations

- Discovered mid-test-development that Microsoft.Data.Sqlite's default connection pooling holds a native file handle open past `Dispose()` on Windows, racing any file-level operation (read bytes, delete directory) performed immediately after closing a connection. This is the same class of issue `SP-02`'s harness had already worked around with `Pooling=False;` — applied the same fix here, plus to `SqliteCampaignRepository`'s connection-opening helper since this task's own tests exercised that exact race for the first time in this codebase's history.
- A self-authored test assertion initially contradicted its own inline comment (asserted no `.tmp-` directory could survive a kill, while the comment next to it said that was acceptable) — caught by the test actually failing, not by review; fixed by removing the incorrect assertion.

## 9. Validation and acceptance evidence

See task contract section 17 for full command output summaries and the acceptance-criteria table.

## 10. Recovery and rollback

Revert the branch; no persisted campaign data exists yet that depends on the filled-in `BackupRecords` DDL or the new `Backups/` tree contents, so no data migration or rollback procedure beyond a normal git revert is needed. The kill test (`TC-PERSIST-016`) and corruption fixture (`TC-PERSIST-019`) are themselves the recovery rehearsal this task required.

## 11. Open questions and blockers

- None blocking. Whether to wire `BackupRotationPolicy` into `CampaignSettings` JSON, and whether to implement Full backup composition, are open product questions for a future task.

## 12. Outcome and follow-up

Delivered: manual backup via the SQLite Backup API with a real temp→validate→atomic-rename flow, Fast/Daily/Weekly rotation with configurable retention, restore-into-a-new-copy, and a real corruption fixture proving backups survive a corrupted working database. All prior Persistence tests remain green. Follow-up: none assigned yet (see task contract section 17's "Follow-up tasks").
