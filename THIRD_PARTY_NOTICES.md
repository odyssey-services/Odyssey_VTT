# Third-Party Notices

This repository is private and All Rights Reserved. The following approved third-party packages are used by the repository.

| Package | Version | Purpose | License | Scope | Approval |
|---|---:|---|---|---|---|
| Newtonsoft.Json | 13.0.2 | Explicit deterministic release-critical JSON streaming codecs | MIT | Pure .NET `Odyssey.Application` bridge | ADR-003 v1.1 / Technical Development Baseline v0.4 / ODY-S00-007 |
| com.unity.nuget.newtonsoft-json | 3.2.2 | Unity package distribution of Newtonsoft.Json 13.0.2 (`AssemblyVersion` 13.0.0.0) for explicit streaming codecs | MIT / Unity package distribution | Unity `Odyssey.Application` package | ADR-003 v1.1 / Technical Development Baseline v0.4 / ODY-S00-007 |
| Microsoft.Data.Sqlite | 9.0.10 (approved) | The accepted SQLite provider library for Odyssey VTT persistence, per `ADR-011` v1.1 §1, closing `ADR-011` v1.0 §12.1 on the `SP-02` spike's recommendation. First referenced by real production code in `ODY-S01-007` (`Odyssey.Persistence`); also still present in the `SP-02` evidence-generation harness (`Tools/Spikes/SP-02-PersistenceReliability/`) | MIT | Production use in `DotNet/Projects/Odyssey.Persistence.csproj` (`Packages/com.odyssey.persistence`), added to `DotNet/Odyssey.Core.sln` by `ODY-S01-007`; also present in `Tools/Spikes/SP-02-PersistenceReliability/` (evidence-only, unrelated reference) | `ADR-011` v1.1 / `SP-02` report (`docs/tasks/completed/ODY-S01-005_SP-02_Persistence_Reliability_Report.md` §3) / `ODY-S01-007` |
| SQLitePCLRaw.bundle_e_sqlite3 | >= 3.0.3 (approved, mandatory floor) | Transitive native SQLite bundle; `ADR-011` v1.1 §1 mandates this floor because `Microsoft.Data.Sqlite` 9.0.x otherwise pulls a `2.1.x` chain flagged by NuGet audit for a known high-severity vulnerability (`GHSA-2m69-gcr7-jv3q`) | MIT / Apache-2.0 | Same as above | `ADR-011` v1.1 / `SP-02` report / `ODY-S01-007` |

Approved tooling referenced by repository policy:

| Tool | Purpose | Notes |
|---|---|---|
| Git | Version control | Existing approved tooling |
| Git LFS | Large binary pointer management | Required by Technical Development Baseline |
| GitHub repository features | Hosting, pull requests, branch protection | Owner-controlled repository configuration |
| actions/checkout | GitHub Actions source checkout pinned to `d23441a48e516b6c34aea4fa41551a30e30af803` | MIT; approved by Technical Development Baseline v0.5 / ODY-S00-008 |
| actions/setup-dotnet | GitHub Actions .NET SDK setup pinned to `26b0ec14cb23fa6904739307f278c14f94c95bf1` | MIT; approved by Technical Development Baseline v0.5 / ODY-S00-008 |
| actions/upload-artifact | GitHub Actions bounded evidence upload pinned to `330a01c490aca151604b8cf639adc76d48f6c5d4` | MIT; approved by Technical Development Baseline v0.5 / ODY-S00-008 |

Future dependencies must be approved by the active task or ADR and recorded here with license evidence.
