# SP-02 — Persistence Reliability spike harness

**This is not production code.** It does not implement, select, or wire in a production SQLite provider, migration runner, snapshot writer, or backup mechanism. It exists only to generate reproducible empirical evidence for `docs/tasks/active/ODY-S01-005_SP-02_Persistence_Reliability.md` and its report, `docs/tasks/active/ODY-S01-005_SP-02_Persistence_Reliability_Report.md`.

- Not referenced by `DotNet/Odyssey.Core.sln`.
- Not referenced by any `Packages/com.odyssey.*` module.
- Not wired into `.github/workflows/ci.yml` or any repository script.
- Uses `Microsoft.Data.Sqlite` only as a spike-scope evidence-generation dependency (see `THIRD_PARTY_NOTICES.md`), not as a production dependency decision. The spike's own recommendation on provider-library choice (see the report) is a recommendation for the product owner / a future `ADR-011` §12.1 amendment, not a binding selection made by this code.
- Safe to delete in its entirety without affecting any production build, test, or CI job.

## What it does

`SP02.Harness` is a standalone `net10.0` console application that empirically exercises the SQLite PRAGMA profile fixed by `ADR-011` §7 (`journal_mode=WAL`, `foreign_keys=ON`, `synchronous=FULL`, `busy_timeout=5000`) against the six roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` §10.4 scenarios:

1. WAL/transaction mode under normal load.
2. A simulated crash (hard process kill) during a critical operation.
3. An interrupted backup (killed mid-copy), validating the `ADR-012` §8.4/8.8 temp-copy → validate → atomic-move pattern.
4. A simulated migration failure mid-chain, validating the `ADR-013` §7 temp-copy pattern protects the working database and the pre-migration snapshot.
5. Snapshot size/speed measurements across a range of synthetic database volumes.
6. A simulated main-database corruption (byte-level file corruption) and recovery into a separate restored copy.

Each scenario prints concrete measured numbers (row counts, elapsed milliseconds, throughput, integrity-check results, file hashes) to stdout, ending in an explicit `PASS`/`SUMMARY` line per scenario.

## How to reproduce

```powershell
cd Tools\Spikes\SP-02-PersistenceReliability\SP02.Harness
dotnet build -c Release
.\..\..\..\..\artifacts\bin\SP02.Harness\release\SP02.Harness.exe
```

(The build uses the repository-wide `Directory.Build.props` `UseArtifactsOutput=true` convention, so build output lands under the repository's `artifacts/` directory, already excluded from Git and from `REPO-POLICY-002`'s tracked-file scan — nothing under `artifacts/` is or should be committed.)

The program creates its own temporary work directory under the OS temp folder (path printed at the top of its output) and does not touch any file under the repository. Each run is self-contained; no fixture setup is required beyond `dotnet build`.

Raw stdout from two independent runs used to produce the report's numbers is saved under [`evidence/`](evidence/) for reproducibility comparison (`run-2026-08-24-01.log`, `run-2026-08-24-02.log`). Re-running the harness will reproduce the same pass/fail outcomes; exact timings will vary with machine load, which is expected and is why the report treats timing numbers as illustrative measurements, not fixed guarantees.

## Scope and limitations (read before citing this evidence elsewhere)

- Scenario 3 (interrupted backup) uses `Microsoft.Data.Sqlite`'s `SqliteConnection.BackupDatabase` one-shot call rather than manually stepping the SQLite Backup API page-by-page. It validates the harness-level **temp → validate → atomic-move** safety pattern under a hard kill, not a byte-exact reproduction of `sqlite3_backup_step` interruption at an arbitrary page boundary. See the report for why this is treated as sufficient evidence for the pattern under test.
- Scenario 5's "realistic MVP campaign volume" is an explicit stated assumption, not sourced from an authoritative number in `05_Persistence_Odyssey_VTT_v0.8.md` or `02_MVP_Scope_Odyssey_VTT_v0.10.md` (neither document gives one) — see the report for the exact assumption and its justification.
- All scenarios run on a single Windows development machine, single-run-at-a-time (no concurrent multi-process contention beyond what each scenario explicitly sets up). No cross-machine, cross-filesystem (e.g. network share), or antivirus-interference conditions are exercised.
- This harness does not exercise Unity/IL2CPP; it is a pure .NET console app, consistent with `ADR-011`/`ADR-012`/`ADR-013` being Persistence-layer contracts independent of the Unity client.

## Retention

This directory is retained as spike evidence per roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` §23 ("Spike закрывается не кодом как таковым, а принятым решением и воспроизводимым доказательством") — the code stays as the reproducibility proof backing the report's claims, not as a stepping stone toward a production implementation. A future implementation task for the actual migration runner / snapshot writer / persistence layer must not `ProjectReference` or otherwise depend on this project.
