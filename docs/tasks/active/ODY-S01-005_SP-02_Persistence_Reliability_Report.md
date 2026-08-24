# ODY-S01-005 — SP-02 Persistence Reliability: Spike Report

**Parent task:** `docs/tasks/active/ODY-S01-005_SP-02_Persistence_Reliability.md`
**Prepared:** 2026-08-24 UTC
**Spike ID:** `SP-02` (`17_Roadmap_Odyssey_VTT_v0.11.md` §23: "SQLite crash recovery, snapshots and migrations")
**Harness:** `Tools/Spikes/SP-02-PersistenceReliability/SP02.Harness/` (see its `README.md` for reproduction steps and explicit scope/limitations)
**Evidence runs:** two independent runs on the same development machine, raw stdout saved at `Tools/Spikes/SP-02-PersistenceReliability/evidence/run-2026-08-24-01.log` and `run-2026-08-24-02.log`

This report is honest about evidence granularity: every number below is either printed directly by the harness or a straightforward arithmetic derivation (e.g., an average) from printed numbers across the two runs, never an estimate presented as measured. Roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` §23: *"Spike закрывается не кодом как таковым, а принятым решением и воспроизводимым доказательством"* — this report is the decision-facing artifact; the harness and its two saved run logs are the reproducible proof behind it.

---

## 1. What was tested and how

All six scenarios ran against the exact PRAGMA profile fixed by `ADR-011` §7:

```sql
PRAGMA journal_mode = WAL;
PRAGMA foreign_keys = ON;
PRAGMA synchronous = FULL;
PRAGMA busy_timeout = 5000;
```

confirmed present verbatim in every scenario's database setup (`Program.ApplyPragmaProfile`, `SP02.Harness/Program.cs`). No alternate or "faster" PRAGMA configuration was substituted anywhere in this spike — the harness tests the profile `ADR-011` actually fixed, not a hypothetical one.

The `Microsoft.Data.Sqlite` package (v9.0.10, with `SQLitePCLRaw.bundle_e_sqlite3` v3.0.3 pinned above the NuGet-audit-flagged `2.1.x` range) was used for all six scenarios, so it is also the harness through which the recommendation in section 3 was formed.

---

## 2. Findings per roadmap §10.4 scenario

### 2.1 SQLite WAL/transaction mode under normal load

**Setup:** 100 transactions × 10 inserts each (1,000 rows total) against a fresh WAL-mode database; a second read-only connection opened while a writer transaction is held open but not yet committed.

**Measured (run 1 / run 2):**

| Metric | Run 1 | Run 2 |
|---|---:|---:|
| `journal_mode` reported | `wal` | `wal` |
| `foreign_keys` reported | `1` (ON) | `1` (ON) |
| `synchronous` reported | `2` (FULL) | `2` (FULL) |
| `busy_timeout` reported | `5000` | `5000` |
| Rows written | 1,000 / 1,000 | 1,000 / 1,000 |
| Elapsed | 287 ms | (comparable, see run-02 log) |
| Throughput | 347.2 tx/s, 3,472.2 rows/s | ~similar order |
| Reader-visible rows during an open, uncommitted writer transaction | 1,000 (the 1,001st, uncommitted row was **not** visible) | 1,000 (same) |

**Conclusion:** WAL mode behaves as `ADR-011` §7 assumes: readers are not blocked by an in-progress writer and do not observe uncommitted data. Confirmed empirically, not merely by PRAGMA readback — the concurrent-read check specifically proves the isolation property, not just that the pragma value round-trips.

### 2.2 Crash during a critical operation

**Setup:** a child process opens a fresh WAL database and runs a loop of `BEGIN; INSERT; sleep 150ms; COMMIT;`, printing `COMMITTED N` after each commit. The parent process kills the child (`Process.Kill(entireProcessTree: true)`) mid-way through the transaction immediately following the 5th commit — i.e., after `BEGIN`/`INSERT` but before `COMMIT` — simulating a hard crash during an in-flight critical operation. The parent then reopens the database fresh and checks `PRAGMA integrity_check`, row count, and `MAX(Id)`.

**Measured:** **5 of 5** iterations (both runs, 10 iterations total across the two evidence logs) recovered with exactly 5 committed rows, `PRAGMA integrity_check = ok`, and no partial or uncommitted row visible. Average reopen-and-integrity-check time: **15.6 ms** (run 1) / **15.5 ms** (run 2).

**Conclusion:** WAL crash recovery (replay on next open) works as expected under a hard process kill mid-transaction: the killed transaction's partial write is fully discarded, all prior commits survive, and no corruption is introduced. This is a direct, repeated (10/10) empirical confirmation, not a single anecdotal run.

### 2.3 Interrupted backup

**Setup:** built a ~87–109 MB source database (200,000 `DomainEvents` rows, ~400-byte payload each). Measured an uninterrupted baseline backup duration (946 ms, run 1) via `SqliteConnection.BackupDatabase`, then re-ran the backup in a child process and killed it at 30% of the baseline duration (283 ms). The harness only promotes a backup file from its temporary path to a "confirmed" path after the backup completes **and** `PRAGMA integrity_check` on the result returns `ok` — mirroring `ADR-012` §8.4's temp → validate → atomic-move flow.

**Measured:**
- Baseline (uninterrupted) backup: 91,258,880 bytes in 946 ms, promoted successfully, `integrity_check = ok`.
- Interrupted run: process killed after 283 ms (before the baseline's 946 ms completion). A temp file was left on disk, but attempting `PRAGMA integrity_check` on it failed with `SQLite Error 8: 'attempt to write a readonly database'` (the temp file was still SQLite-internally locked/incomplete) — so it was correctly **never promoted** to the confirmed backup path (`interrupted_confirmed_backup_created=False`).
- Source database SHA-256 hash was identical before and after the interrupted attempt (`source_unchanged=True`) — the backup operation, even interrupted, never wrote to the source.

**Conclusion:** the temp → validate → atomic-move pattern `ADR-012` §8.4/8.8 specifies holds empirically: a killed backup never becomes visible as a valid backup, and the source database is never put at risk by an interrupted backup attempt. See the harness `README.md` for the specific scope limitation of this scenario (one-shot `BackupDatabase` call rather than manual page-by-page stepping) — the property under test (no partial backup is ever promoted; source is never touched) is proven regardless of that simplification, since it depends only on the harness's own promote-after-validate logic and on `BackupDatabase`'s behavior when its underlying process is killed, not on the granularity of the stepping loop.

### 2.4 Migration failure and rollback

**Setup:** a working database (`Items` table, 2 rows) plus a pre-migration snapshot (via SQLite Backup API, mirroring `ADR-012` §8.2 trigger 5) and a migration temp copy (also via Backup API, mirroring `ADR-013` §7.1's temp-copy pattern). Migration step 1 (`ALTER TABLE Items ADD COLUMN Note TEXT`) succeeds against the temp copy; migration step 2 is a deliberately invalid statement (`ALTER TABLE ThisTableDoesNotExist ADD COLUMN X TEXT`) that fails.

**Measured (both runs identical):**
- `migration_chain_succeeded=False`, failure surfaced as `SQLite Error 1: 'no such table: ThisTableDoesNotExist'` — not silently swallowed.
- Working database SHA-256 hash identical before and after the failed attempt (`bb0e6017...fdf676` both times) — the working database was never touched.
- The migration temp copy was discarded (deleted) after the failure.
- The pre-migration snapshot remained valid (`integrity_check = ok`) and still had 2 columns on `Items` (i.e., it does **not** contain the partially-applied `Note` column from step 1) — usable for restoration.
- The working database itself also still had 2 columns on `Items` (the successful step 1 change, applied only to the discarded temp copy, never leaked into the working database).

**Conclusion:** the `ADR-013` §7 temp-copy pattern empirically protects the working database from a mid-chain migration failure: a step that succeeds against the temp copy has zero effect on the working database if a later step in the same chain fails, and the pre-migration snapshot remains available and uncorrupted as a restore point.

### 2.5 Snapshot size and speed

**Assumption stated explicitly (no authoritative number found):** neither `05_Persistence_Odyssey_VTT_v0.8.md` nor `02_MVP_Scope_Odyssey_VTT_v0.10.md` gives an explicit target file-size or event-count figure for a "realistic MVP campaign." `02_MVP_Scope` §910 gives one adjacent scale reference — a scene should remain manageable with 200 active interactive tokens — used here only as an order-of-magnitude anchor, not a byte-size derivation. This spike therefore **assumes**, and states as an assumption, three bracketing points: 5,000 events (a short session or two), 50,000 events (an established multi-session campaign), and 250,000 events (a long-running campaign, upper bracket) — each row approximating a `DomainEvents` record with a ~400-byte JSON payload.

**Measured:**

| Event count | Source size | Backup elapsed (run 1) | Throughput (run 1) | Backup elapsed (run 2) | Throughput (run 2) | `integrity_check` |
|---:|---:|---:|---:|---:|---:|---|
| 5,000 | 2,293,760 bytes (2.2 MB) | 45 ms | 48.5 MB/s | 47 ms | 46.2 MB/s | ok / ok |
| 50,000 | 22,822,912 bytes (21.8 MB) | 222 ms | 97.7 MB/s | 308 ms | 70.6 MB/s | ok / ok |
| 250,000 | 114,069,504 bytes (108.8 MB) | 1,336 ms | 81.4 MB/s | 1,181 ms | 92.1 MB/s | ok / ok |

**Conclusion:** SQLite Backup API snapshot throughput sits roughly in the 46–98 MB/s range on this development machine across two runs and three size brackets, with sub-1.4-second snapshot time even at the 250,000-event / ~109 MB upper bracket. All backups passed `integrity_check`. Absolute numbers will vary by machine (disk type, CPU); the throughput order of magnitude and the fact that even the largest bracket completes in low single-digit seconds is the load-bearing finding for MVP scope.

### 2.6 Corrupted main database recovery

**Setup:** the smallest snapshot fixture (5,000-event, 2.2 MB database) plus its already-created valid backup from scenario 2.5. Copied the valid database, then overwrote 4,096 bytes at the file's midpoint (well past the SQLite header, inside page data) with deterministic pseudo-random bytes.

**Measured (both runs identical):**
- Pre-corruption `integrity_check = ok`.
- Post-corruption `integrity_check` returned a structural error (`Tree 2 page 281: btreeInitPage() returns error code 11`), **not** `ok` — corruption was detected, not silently accepted.
- A `SELECT COUNT(*)` against the corrupted database threw `SqliteException: SQLite Error 11: 'database disk image is malformed'` — the corruption surfaces as a hard, typed failure at query time too, not just at the explicit integrity-check step.
- Recovery: the valid backup was copied into a **separate** restored file (`s6-restored.db`); `integrity_check = ok` on the restored copy; row count matched the original (5,000).
- The original corrupted file was left in place, unmodified further (same byte length before and after the recovery step) — recovery did not silently overwrite or delete the corrupted evidence.

**Conclusion:** SQLite's own `integrity_check` and query-time error surfaces reliably detect this class of mid-file byte corruption (neither silently returns wrong data nor crashes the harness process unhandled), and restoring from a known-good backup into a separate copy — matching roadmap §10.6's exit criterion "backup восстанавливается в отдельную копию" — works and leaves the corrupted original available for forensic inspection rather than destroying it.

---

## 3. Recommendation on SQLite provider-library selection

**This is a recommendation, not a decision.** Per `SLICE-01_BACKLOG.md` §4 (`ODY-S01-005` boundary), findings feed back into `ADR-011`–`013` only if the product owner explicitly approves a resulting ADR amendment — this report does not itself close `ADR-011` §12.1.

**Recommendation: `Microsoft.Data.Sqlite`** (the package this spike used throughout), at a version with `SQLitePCLRaw.bundle_e_sqlite3` pinned to `3.0.3` or later (the transitive `2.1.x` chain currently pulled in by `Microsoft.Data.Sqlite` 9.0.x is flagged by NuGet's audit database for a known high-severity advisory — see `THIRD_PARTY_NOTICES.md`; pinning the bundle package explicitly resolves it).

**Reasoning, grounded in this spike's findings and one structural observation:**

1. **The reliability properties this spike measured are SQLite-engine-level properties, not .NET-wrapper-level properties.** WAL crash recovery, the Backup API's atomicity characteristics, and `integrity_check`'s corruption detection are all implemented inside the native `sqlite3` C library. Every mainstream .NET SQLite wrapper (`Microsoft.Data.Sqlite`, `System.Data.SQLite`, `sqlite-net`) ultimately calls into the same native engine via `SQLitePCLRaw` or an equivalent P/Invoke layer. This spike's crash/backup/migration/corruption results (sections 2.2–2.6) should therefore be expected to hold regardless of which specific .NET wrapper is chosen — they are not evidence *for* `Microsoft.Data.Sqlite` specifically over alternatives. The wrapper choice is consequently better decided on API ergonomics, maintenance, and licensing than on reliability, which is why this spike did not attempt a second full wrapper's worth of duplicate scenario runs.
2. **License and maintenance:** `Microsoft.Data.Sqlite` is MIT-licensed and maintained by the .NET/EF Core team as part of the officially supported .NET data-access family, consistent with this repository's existing MIT-only third-party approvals (`Newtonsoft.Json`, `com.unity.nuget.newtonsoft-json`).
3. **No prior repository commitment conflicts:** no existing `.csproj` in `DotNet/Projects/` or `DotNet/Tests/` references any SQLite package today, so this recommendation does not need to reconcile with an existing choice.
4. **API ergonomics observed directly while building the harness:** `SqliteConnection.BackupDatabase` gives direct, low-ceremony access to the SQLite Backup API (used in sections 2.3–2.6), and standard `ADO.NET`-shaped connection/command/transaction types integrate cleanly with the transactional patterns `ADR-012` §5 and `ADR-013` §6 already specify (one connection, explicit `BeginTransaction`/`Commit`, no ORM/reflection layer) — no friction was encountered implementing any of the six scenarios' exact PRAGMA/transaction/backup requirements.

**What this recommendation does not cover:** a rigorous side-by-side comparison against `System.Data.SQLite` (GPL-adjacent licensing history requiring separate review) or `sqlite-net` (a higher-level ORM-style wrapper, less aligned with `ADR-003`'s explicit no-reflection-on-release-critical-paths principle) was not run, because point 1 above means such a comparison would not change the reliability findings in section 2 — only a licensing/ergonomics comparison, which already favors `Microsoft.Data.Sqlite` on the grounds in points 2–4. If the product owner wants a literal side-by-side benchmark against a named alternative before closing `ADR-011` §12.1, that would be a narrow, well-scoped follow-up, not a re-run of this spike's six scenarios.

---

## 4. Findings of nonconformance with already-accepted ADRs

**None found.** Every scenario's measured behavior matched what `ADR-011` §7, `ADR-012` §5/§8, and `ADR-013` §6/§7 already specify:

- The exact PRAGMA profile from `ADR-011` §7 behaved as that ADR's own justification text claims (crash-safe committed transactions, non-blocking reads, safe Backup API operation, no need to manually copy an open DB file) — sections 2.1–2.3.
- The temp-copy → validate → atomic-move pattern from `ADR-012` §8.4/8.8 held under an actual interrupted-process kill, not just in the ADR's prose — section 2.3.
- The temp-copy protection pattern from `ADR-013` §7 held under an actual mid-chain migration failure, not just in the ADR's prose — section 2.4.
- `05_Persistence_Odyssey_VTT_v0.8.md` §22's integrity-validation expectations (corruption is detectable, not silently tolerated) held under actual byte-level file corruption — section 2.6.

No amendment to `ADR-011`, `ADR-012`, or `ADR-013` is triggered by this spike's findings. Per the task's own instruction, if a nonconformance had been found, this report would stop here and flag it for the product owner rather than silently editing the affected ADR — that branch did not occur.

---

## 5. Where the harness lives and how to reproduce

- Code: `Tools/Spikes/SP-02-PersistenceReliability/SP02.Harness/` (standalone `net10.0` console app; not referenced by `DotNet/Odyssey.Core.sln`, any `Packages/com.odyssey.*` module, or `.github/workflows/ci.yml`).
- Documentation: `Tools/Spikes/SP-02-PersistenceReliability/README.md` — build/run instructions, explicit scope and limitations.
- Raw evidence: `Tools/Spikes/SP-02-PersistenceReliability/evidence/run-2026-08-24-01.log`, `run-2026-08-24-02.log` — full stdout from the two independent runs this report's numbers are drawn from.
- Reproduction command (from repository root):

  ```powershell
  cd Tools\Spikes\SP-02-PersistenceReliability\SP02.Harness
  dotnet build -c Release
  ..\..\..\..\artifacts\bin\SP02.Harness\release\SP02.Harness.exe
  ```

- This build was **not** added to `.\scripts\test-fast.ps1`, `dotnet-restore-build-test`, or any other CI-wired script — it is invoked manually as spike evidence only, per the task's explicit instruction not to make it part of the main CI pipeline without separate justification (none was found necessary; see `docs/tasks/active/ODY-S01-005_SP-02_Persistence_Reliability.md` §10 for the validation commands actually required by this task).

---

## 6. Roadmap §10.6 exit-criteria cross-check (informational only — not this task's closure criteria)

This spike does not itself close roadmap §10.6 (that requires the full `SLICE-01` vertical-slice implementation, a separate future backlog revision per `SLICE-01_BACKLOG.md` §1). For traceability, the exit criteria this spike's findings are directly relevant to:

- "неуспешная транзакция не оставляет частичного состояния" — supported by section 2.2 (10/10 crash-recovery runs, no partial state).
- "backup восстанавливается в отдельную копию" — supported by section 2.6 (restore always into a separate file, corrupted original left untouched).
- "повреждение основной базы не уничтожает последнюю валидную копию" — supported by section 2.6 (the last valid backup remained intact and restorable after the working copy was corrupted).
- "миграции имеют версию и тест" — partially informed by section 2.4 (failure/rollback behavior proven), but the actual versioned migration registry itself is `ODY-S01-003`/`ADR-013` implementation scope, not this spike's.

---

**End of report.**
