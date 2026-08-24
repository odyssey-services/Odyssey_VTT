# ODY-S01-007 — Campaign Storage Foundation

**Status:** Done  
**Roadmap stage / slice:** SLICE-01 (implementation)  
**Owner:** Codex (agent)  
**Requested by:** Product owner  
**Branch:** `feat/ody-s01-007-campaign-storage-foundation`  
**Pull request:** Merged — [#28](https://github.com/odyssey-services/Odyssey_VTT/pull/28) (merged 2026-08-24T13:07:14Z, merge commit `ecc7c29`)  
**ExecPlan:** `docs/plans/active/ODY-S01-007_Campaign_Storage_Foundation.md`  
**Created:** 2026-08-24  
**Last updated:** 2026-08-24 UTC

## 1. Goal

Implement local campaign creation and opening per `ADR-011` v1.1: the physical folder tree, `campaign.db` under the mandatory PRAGMA profile, `manifest.json` with atomic write/read and manifest-vs-database conflict detection, `CampaignId`/`CampaignPublicId` generation, the minimal mandatory system table set, minimal campaign settings, and the `ICampaignRepository` Application port with its `Microsoft.Data.Sqlite`-based Persistence implementation.

This is the first task in the repository that writes production code depending on `ADR-011` v1.1's SQLite provider-library decision. A critical Unity/IL2CPP compatibility preflight (section 1 of the originating ТЗ) was required and completed before any repository code was written — see section 4 and section 17 for its evidence.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-01_IMPLEMENTATION_BACKLOG.md` reserves `ODY-S01-007` as the first child task of the vertical-slice implementation revision; nothing in the repository yet creates or opens a real local campaign.
- Value or risk reduction: proves, with a real Windows IL2CPP Player build and run (not merely a pure-.NET or Mono assumption), that `Microsoft.Data.Sqlite` + `SQLitePCLRaw.bundle_e_sqlite3 >= 3.0.3` actually work under the platform this project ships on, before any further Persistence work is built on top of that assumption.
- Blocking or enabling relationship: blocks `ODY-S01-008` (Scene and Token Minimal Model) and `ODY-S01-009` (Saving Pipeline), both of which depend on this task's campaign creation/open primitives.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1
- `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md` §4 (physical structure), §5 (`manifest.json`), §7 (PRAGMA profile), §8 (system tables), §9 (identifiers)
- `docs/adr/ADR-011_Local_Campaign_Format_v1.1.md` §1 (mandatory `Microsoft.Data.Sqlite` + `SQLitePCLRaw.bundle_e_sqlite3 >= 3.0.3`)
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md` §5 (dependency matrix), §6.5/§10 (Persistence ownership, repository interfaces as Application ports)
- `docs/adr/ADR-004_Result_and_Error_Model_v1.0.md` (typed `Result`/`Error`, no raw provider exceptions from public API)
- `docs/adr/ADR-003_Serialization_Strategy_v1.1.md` §3 (explicit hand-written codecs, no reflection/auto-mapping)
- `docs/adr/ADR-006_Test_Project_Structure_and_Dual_Unity_DotNet_Compilation_v1.0.md` §24 (Persistence test project addition at this vertical slice)
- `docs/adr/ADR-008_Deterministic_Clock_and_RNG_v1.0.md` (wall-clock reads route through `IWallClock`, not a global API; `Guid`-derived identifiers are permitted, distinct from gameplay RNG)
- `docs/adr/ADR-009_Unity_Project_and_Build_Baseline_v1.1.md` §24, and its pitfall list ("считать Mono-прохождение доказательством IL2CPP-совместимости") — source of this task's mandatory IL2CPP preflight
- `05_Persistence_Odyssey_VTT_v0.8.md` §4, §5, §9 — private local reference, source detail for `ADR-011` §4/§5/§9
- `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` §5 (this task's boundary as scaffolded by `ODY-S01-006`)

### Requirement and test IDs

- Requirement IDs: `SLICE-01` (implementation revision), backlog `ODY-S01-007`.
- Existing test IDs: None reused.
- New test IDs introduced: `TC-PERSIST-001` (mandatory PRAGMA profile applied on Create/Open, verified by readback), `TC-PERSIST-002` (manifest round-trip and atomic-replace crash safety), `TC-PERSIST-003` (manifest-vs-database conflict detection), `TC-PERSIST-004` (`CampaignId`/`CampaignPublicId` format/uniqueness; typed `Result`/`Error`, no raw provider exception) — registered in `Tests/Metadata/test-catalog.json` and referenced from `docs/errors/ERROR_CODES.md`.

### Task-safe private context

- Approved summary / references: `05_Persistence_Odyssey_VTT_v0.8.md` §4/§5/§9 summarized (not pasted beyond short customary phrases) into this task and the production code's XML doc comments. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` is on `main` (merged via `ODY-S01-006`, PR #27); its `ODY-S01-007` row reads `Draft`, confirmed by `Read` before branching.
- `ADR-011` v1.1, `ADR-012`, `ADR-013`, `ADR-014` are all `Accepted` on `main`, confirmed by `grep`.
- **Critical preflight (this task's own instruction, performed before any repository code was written):** `Odyssey.Persistence` is a normal module in `ADR-001`'s dependency matrix with `Unity Client → Persistence = ✓` permitted — meaning Persistence code runs inside the Unity Client process and is subject to `ADR-006`'s dual-compilation rule and `ADR-009`'s mandatory IL2CPP validation (Mono-only passing is explicitly listed in `ADR-009` as a mistake to avoid, not accepted evidence). A real, isolated verification was performed: `Microsoft.Data.Sqlite` 9.0.10 + `SQLitePCLRaw.bundle_e_sqlite3` 3.0.3 (managed assemblies + native `e_sqlite3.dll` for win-x64) were placed as Unity plugins, a Windows x64 **IL2CPP** Player was built (`BuildResult.Succeeded`, 0 errors) via Unity 6000.4.0f1 batchmode, and the built `.exe` was actually run headless. The running IL2CPP Player successfully opened a SQLite connection, applied the exact `ADR-011` §7.1 PRAGMA profile, read back `journal_mode=wal`, and performed a `CREATE TABLE`/`INSERT`/`SELECT` round-trip — result `PASS`, logged and written to a result file on disk, process exited cleanly. No compatibility issue, native-linker stripping, or AOT marshalling problem was found. All temporary preflight scaffolding (scratch Editor scripts, scene, build output, vendored plugin DLLs under `Assets/Plugins/`) was deleted after the check, and unintended Unity side effects on `ProjectSettings`/HDRP assets from the throwaway build were reverted before any task code was written.
- `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md` §4.1 (directory tree), §5.2 (mandatory manifest fields), §7.1 (PRAGMA profile), §8.2 (system table list), §9.1 (identifier format) were read in full and are the direct source for this task's implementation.
- No existing `CampaignPublicId` type, `ICampaignRepository`, `CampaignManifest`, or any `Odyssey.Persistence.Sqlite` code existed on `main` prior to this task. `CampaignId` already existed in `Odyssey.Domain.Identity` (from an earlier task) but had no `NewId()` factory.
- `DotNet/Tests/Odyssey.Tests.Contracts/ProjectContractTests.cs` and `scripts/verify-test-structure.ps1` both contained an explicit guard rejecting `DotNet/Projects/Odyssey.Persistence.csproj`/`DotNet/Tests/Odyssey.Tests.Persistence` as "not yet created" — both updated by this task to reflect that this vertical slice is exactly the point `ADR-006` §24 expects the Persistence bridge/test projects to appear, while leaving the `Odyssey.Networking` guard untouched (still Stage 3 scope).
- `scripts/verify-test-structure.ps1`'s forbidden-global-API scan (`DateTimeOffset.UtcNow`, per `ADR-008`) applies to every `Packages/com.odyssey.*/Runtime` package, including the newly added `com.odyssey.persistence`; this required routing all wall-clock reads in `SqliteCampaignRepository` through the existing `IWallClock` port rather than calling the BCL API directly, and making `CampaignId.NewId()`/`CampaignPublicId.NewId()` pure functions that accept an explicit `UtcInstant` rather than reading the clock internally (preserving `Odyssey.Domain`'s zero-dependency purity, since `IWallClock` lives in `Odyssey.Application` and Domain must not reference Application).

### Assumptions

- None. All facts above were directly observed via `Read`/`grep`/real build-and-run evidence before and during this task.

## 5. Scope

### In scope

- `Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs` — `CampaignPublicId` struct, `Uuid7` pure helper, `CampaignId.NewId(UtcInstant)`/`CampaignPublicId.NewId(UtcInstant)` factories.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignManifest.cs` — `CampaignManifest` DTO and explicit `CampaignManifestV1Codec` (ADR-003-compliant hand-written codec).
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignSettings.cs` — minimal campaign settings DTO.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` — `ICampaignRepository` port, `CreateCampaignRequest`, `CampaignHandle`, `PersistenceFailures` typed-error factory.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — four new persistence `ErrorCode`s.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCampaignRepository.cs` — the `ICampaignRepository` implementation: directory tree creation, PRAGMA-profiled `campaign.db`, minimal `ADR-011` §8.2 system tables plus a minimal `Campaign` identity/settings table, atomic manifest write, manifest-vs-database conflict detection, `IWallClock`-sourced timestamps.
- `DotNet/Projects/Odyssey.Persistence.csproj` (new pure-.NET bridge project, first real `Microsoft.Data.Sqlite`/`SQLitePCLRaw.bundle_e_sqlite3` production dependency), added to `DotNet/Odyssey.Core.sln`.
- `DotNet/Tests/Odyssey.Tests.Persistence/` (new test project + `SqliteCampaignRepositoryTests.cs`), added to `DotNet/Odyssey.Core.sln`.
- `DotNet/Tests/Odyssey.Tests.Contracts/ProjectContractTests.cs`, `scripts/verify-test-structure.ps1` — narrow updates un-blocking the now-legitimate `Odyssey.Persistence` bridge/test projects (Networking guard untouched).
- `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json` — registry entries for the four new `ErrorCode`s and four new `TC-PERSIST-*` test case IDs.
- `docs/tasks/active/ODY-S01-007_Campaign_Storage_Foundation.md` (this file), `docs/plans/active/ODY-S01-007_Campaign_Storage_Foundation.md` (governing ExecPlan).
- `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` — `ODY-S01-007` row status only.

### Out of scope

- Scene/Token/SceneObject model (`ODY-S01-008`).
- Domain Event Store, `ADR-012` §5 transactional pipeline, safe close/crash recovery beyond the WAL-checkpoint-on-close already needed for `campaign.db` itself (`ODY-S01-009`).
- Migration registry/runner (`ODY-S01-010`).
- Backup/snapshot creation (`ODY-S01-011`).
- `.odcamp` export container (`ODY-S01-012`).
- Owner key storage — excluded from the entire `SLICE-01` implementation revision per `SLICE-01_IMPLEMENTATION_BACKLOG.md` §2.2.
- `campaign.lock` file / concurrent-access locking — not required by this task's explicit scope list; deferred to whichever future task first needs concurrent-access protection.
- Full dual-compilation single-source verification wiring for `com.odyssey.persistence` inside `verify-test-structure.ps1`'s `$coreBridgeModules` graph-parity machinery — only the narrow "unexpected bridge project" blocklist entries were removed; deeper integration into that script's Unity/`.csproj` single-source parity checks is a separate repository-tooling hardening item, not required to prove this task's own deliverable.

### Allowed paths

```text
Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs
Packages/com.odyssey.application/Runtime/Persistence/**
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/**
DotNet/Projects/Odyssey.Persistence.csproj
DotNet/Tests/Odyssey.Tests.Persistence/**
DotNet/Tests/Odyssey.Tests.Contracts/ProjectContractTests.cs
DotNet/Odyssey.Core.sln
scripts/verify-test-structure.ps1
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
THIRD_PARTY_NOTICES.md
docs/tasks/active/ODY-S01-007_Campaign_Storage_Foundation.md
docs/plans/active/ODY-S01-007_Campaign_Storage_Foundation.md
docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: `ICampaignRepository`/`CampaignManifest`/`CampaignHandle` live in `Odyssey.Application` (port); `SqliteCampaignRepository` lives in `Odyssey.Persistence` (implementation) — matches `ADR-001` §6.5/§10. `Odyssey.Domain`'s new `CampaignPublicId`/`Uuid7` additions introduce no new dependency (still zero-dependency).
- Authoritative-state and transaction boundary: `campaign.db` writes in this task (system table creation, single `Campaign` row insert) are simple, non-event-sourced writes; the `ADR-012` journal↔projection transactional rule does not yet apply since no `DomainEvents` are written by this task.
- Serialization / compatibility boundary: `manifest.json` uses an explicit hand-written `IJsonContractCodec<CampaignManifest>` (low-level `JsonTextReader`/`JsonTextWriter` streaming, no `JsonConvert`/reflection), per `ADR-003` §3, matching the existing `LogEventV1Codec`/`OdcampManifestV1Codec` pattern.
- Time / RNG rule: `ADR-008` — all wall-clock reads route through the existing `IWallClock` port; `CampaignId`/`CampaignPublicId` generation is a pure function of an explicit `UtcInstant` plus `Guid.NewGuid()` (permitted as a local opaque-identifier generator, not a gameplay RNG result).
- Unity / thread / lifetime rule: `SqliteCampaignRepository` keeps its `SqliteConnection` instances in a private `ConcurrentDictionary` keyed by campaign root path, disposed only via `Close()`; no connection is ever exposed on the Application-layer `CampaignHandle`.
- Dependency / licensing rule: `Microsoft.Data.Sqlite` 9.0.10 + `SQLitePCLRaw.bundle_e_sqlite3` 3.0.3 (both MIT), per `ADR-011` v1.1 §1; the `SQLitePCLRaw.bundle_e_sqlite3` floor is mandatory to avoid the NuGet-audit-flagged `2.1.x` vulnerability chain, matching the `SP-02` harness's own resolution.
- Security / privacy / redaction rule: `PersistenceFailures` typed errors never surface raw `SqliteException`/`IOException` text, local paths, or stack traces to callers (`ADR-004`).
- Performance or platform constraint: Windows x64, both Mono (development) and IL2CPP (release) scripting backends — verified for IL2CPP specifically in section 4's preflight, since that is the platform-specific risk this task's own instruction called out.
- Other: `ADR-011` §5.4's manifest-vs-database conflict rule is enforced as a hard block (`Open()` fails with `PersistenceManifestConflict`, no automatic silent resolution either direction).

## 7. Expected behavior

### Scenario 1 — Create a new campaign

**Given** an empty or non-existent target folder path  
**When** `ICampaignRepository.Create` is called with a valid `CreateCampaignRequest`  
**Then** the `ADR-011` §4.1 directory tree is created; `campaign.db` exists with `journal_mode=wal` persisted; the minimal `ADR-011` §8.2 system tables plus the `Campaign` identity/settings table exist; a new `CampaignId`/`CampaignPublicId` pair is generated and stored; `manifest.json` exists (no leftover `.tmp` file); the returned `Result<CampaignHandle>` is a success carrying that identity and manifest.

### Scenario 2 — Open an existing campaign

**Given** a campaign folder previously created by this repository (or a structurally compatible one)  
**When** `ICampaignRepository.Open` is called with that folder's path  
**Then** the mandatory PRAGMA profile is applied on the newly opened connection; the manifest is read and its `CampaignId` compared against the database's own stored `CampaignId`; on agreement, a success `Result<CampaignHandle>` is returned with the same identity as at creation time.

### Scenario 3 — Manifest/database conflict

**Given** a campaign folder whose `manifest.json` `campaignId` has been tampered to disagree with the `CampaignId` stored in `campaign.db`  
**When** `ICampaignRepository.Open` is called  
**Then** the call fails with `persistence.manifest.conflict` (`ErrorCategory.Conflict`); no write access to the database is granted; neither the manifest nor the database is silently trusted over the other or auto-corrected.

### Scenario 4 — Simulated crash mid manifest-write

**Given** a campaign whose `manifest.json` already exists  
**When** a partially written `manifest.json.tmp` is left on disk (simulating a crash between temp-file write and atomic rename)  
**Then** the original `manifest.json` bytes remain byte-for-byte unchanged, and a subsequent `Open` still succeeds using the untouched original manifest.

### Required invariants

- No public `ICampaignRepository` method ever throws a raw `SqliteException`, `IOException`, or `UnauthorizedAccessException` to its caller — all are caught and translated to a typed `Result`/`Error`.
- `CampaignId`/`CampaignPublicId` values generated by `NewId()` are always well-formed per their own `TryParse` rules and are unique across at least 500 consecutive generations in the same process (proven by `TC-PERSIST-004`).
- The PRAGMA profile applied by `SqliteCampaignRepository` is byte-identical to `ADR-011` §7.1's four statements — no alternate/optimized profile is substituted.

## 8. Deliverables

- Production code: `CampaignPublicId`/`Uuid7`/`NewId()` (Domain); `CampaignManifest`/`CampaignManifestV1Codec`/`CampaignSettings`/`ICampaignRepository`/`CreateCampaignRequest`/`CampaignHandle`/`PersistenceFailures` (Application); `SqliteCampaignRepository` (Persistence); four new `ErrorCodes` entries.
- Tests: `DotNet/Tests/Odyssey.Tests.Persistence/SqliteCampaignRepositoryTests.cs` (11 tests, `TC-PERSIST-001`–`004`).
- Scripts / CI: `scripts/verify-test-structure.ps1` narrowly updated (Persistence bridge/test project no longer blocked); no CI workflow file changed.
- Configuration: `DotNet/Projects/Odyssey.Persistence.csproj`, `DotNet/Tests/Odyssey.Tests.Persistence/Odyssey.Tests.Persistence.csproj`, both added to `DotNet/Odyssey.Core.sln`.
- Documentation: this task contract, its governing ExecPlan, `docs/errors/ERROR_CODES.md` additions, `Tests/Metadata/test-catalog.json` additions, `THIRD_PARTY_NOTICES.md` update (first real production reference, not spike-only), `SLICE-01_IMPLEMENTATION_BACKLOG.md` `ODY-S01-007` row status.
- Generated evidence or build artifacts: preflight IL2CPP build/run logs (captured in this task's completion evidence, not committed to the repository — ephemeral local build logs); validation command output recorded in §17.
- Migration / recovery material: None (no schema migration exists yet; `ODY-S01-010` owns that).

## 9. Acceptance criteria

1. Real, isolated verification (not assumed) proves `Microsoft.Data.Sqlite` + `SQLitePCLRaw.bundle_e_sqlite3 >= 3.0.3` work correctly under a Windows x64 **IL2CPP** Unity Player build and run — not merely Mono/Editor — before any repository code was written; the result and its evidence are recorded in this task's completion evidence.
2. `ICampaignRepository.Create` produces the `ADR-011` §4.1 directory tree, a PRAGMA-profiled `campaign.db`, the `ADR-011` §8.2 minimal system tables, a `CampaignId`/`CampaignPublicId` pair, and an atomically written `manifest.json` with no leftover `.tmp` file.
3. `ICampaignRepository.Open` applies the same PRAGMA profile and detects a manifest/database `CampaignId` conflict as a blocking, diagnosable failure (`persistence.manifest.conflict`), never a silent pick of one side.
4. A simulated crash mid atomic-manifest-write never corrupts the existing `manifest.json`.
5. `CampaignId.NewId()`/`CampaignPublicId.NewId()` produce canonical, unique values across at least 500 generations, and are time-sortable.
6. No public `ICampaignRepository` method leaks a raw provider exception; all failures surface as typed `Result`/`Error` per `ADR-004`.
7. `dotnet test DotNet/Odyssey.Core.sln` passes in full (all existing suites plus the new `Odyssey.Tests.Persistence` suite), including the `RepositoryStructurePassesArchitectureGuard` guard (forbidden-global-API scan, bridge-project graph).
8. `.\scripts\restore.ps1`, `.\scripts\verify-format.ps1`, `.\scripts\verify-test-structure.ps1`, `.\scripts\test-fast.ps1`, `.\scripts\check-repository-policy.ps1`, and `.\scripts\verify-repository.ps1` all pass.
9. `git diff --name-status` against `main` shows only files listed in §5's Allowed paths.
10. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-PERSIST-001` | `.NET / dotnet test` | Mandatory PRAGMA profile applied on Create and Open, verified by readback | Pass |
| `TC-PERSIST-002` | `.NET / dotnet test` | Manifest round-trip; atomic-replace leaves no temp file on success and survives a simulated mid-write crash | Pass |
| `TC-PERSIST-003` | `.NET / dotnet test` | Manifest-vs-database `CampaignId` conflict detected and blocks `Open` | Pass |
| `TC-PERSIST-004` | `.NET / dotnet test` | `CampaignId`/`CampaignPublicId` canonical format and uniqueness; typed `Result`/`Error`, no raw provider exception | Pass |

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

- Real Windows x64 IL2CPP Unity Player build-and-run preflight (section 4), performed before any repository code was written, with logged `PASS` runtime evidence.
- `dotnet build DotNet/Odyssey.Core.sln` and `dotnet test DotNet/Odyssey.Core.sln` run directly (in addition to the wrapped scripts) to confirm the new `Odyssey.Persistence`/`Odyssey.Tests.Persistence` projects build and pass cleanly alongside every pre-existing project.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Unity 6000.4.0f1, Windows x64 IL2CPP (preflight only, not part of this task's committed deliverable — see section 8).
- Scripting backend: verified compatible under both Mono (Editor) and IL2CPP (Player); production module code itself is plain C#, backend-agnostic.
- Network topology or database fixture: Not applicable — all test fixtures are local, temporary SQLite databases under the OS temp directory.
- Other: `dotnet` SDK matching `global.json` (`10.0.302`).

### Validation not required by this task

- A committed, CI-run Unity Player/IL2CPP build for `Odyssey.Persistence` specifically: the IL2CPP compatibility question was answered by the one-time preflight (section 4); this task's own committed deliverable is pure-.NET-testable production code, not a Unity scene/GameObject wiring, so no new Unity scene or asmdef reference change ships with this task.
- Full backup/restore, migration, or export flow: those remain `ODY-S01-010`/`011`/`012` scope.

## 11. Compatibility, migration, and rollback

- Compatibility impact: introduces the first real, committed `campaign.db` physical schema (system tables per `ADR-011` §8.2, plus a minimal `Campaign` table) and `manifest.json` contract (`CampaignManifestV1`, contract version 1). No prior production consumer exists yet, so no backward-compatibility break is possible.
- Version fields affected: `CampaignFormatVersion` fixed to `"1.1.0"` and `DatabaseSchemaVersion` fixed to `"1.0.0"` as constants in `SqliteCampaignRepository`, both stored in `manifest.json` per `ADR-011` §5.2 — no migration mechanism exists yet to change them (`ODY-S01-010` scope).
- Migration or upcaster: None; `CampaignManifestV1Codec` is version 1 with no prior version to upcast from.
- Forward / backward behavior: Not applicable — no shipped consumer of an earlier format exists.
- Rollback method: revert this task's commits; no persisted campaign created by this code is expected to exist outside test fixtures at this stage.
- Data-loss risk and protection: None — no production campaign data exists yet that this task could affect.
- Recovery rehearsal required: No — deferred to `ODY-S01-011`/`014` once backup/restore and full-slice acceptance exist.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| `Microsoft.Data.Sqlite` | 9.0.10 (nuget.org) | First real production SQLite access for `Odyssey.Persistence` | MIT | `ADR-011` v1.1 §1 |
| `SQLitePCLRaw.bundle_e_sqlite3` | 3.0.3 (nuget.org, transitive, explicitly pinned) | Native SQLite bundle, pinned above the NuGet-audit-flagged `2.1.x` chain | MIT / Apache-2.0 | `ADR-011` v1.1 §1 |

Both were already recorded in `THIRD_PARTY_NOTICES.md` as the accepted (but not-yet-referenced) production choice by `ADR-011` v1.1's closure task (`ODY-S01-005`); this task updates that entry's "Scope" column to reflect the first actual `.csproj` reference (`DotNet/Projects/Odyssey.Persistence.csproj`).

## 13. Security, privacy, and hidden information

- Data classes handled: `CampaignId`/`CampaignPublicId`/`CampaignName`/`RulesetId`/`RulesetVersion` — none classified as `Secret`/`HiddenGameplay` per `ADR-010` §10; no owner key, credential, or hidden campaign content is touched by this task.
- Trust boundaries: local single-user filesystem/SQLite only; no network, no multi-user boundary yet.
- Authorization / audience checks: Not applicable — no permissions model exists at this stage.
- Redaction requirements: `PersistenceFailures` errors never include raw local paths, `SqliteException`/`IOException` text, or stack traces (`ADR-004`).
- Log-safe fields: None logged by this task's production code (no diagnostic emission added here).
- Abuse / malformed input limits: `CampaignManifestV1Codec.Read` validates JSON structure via `JsonObjectReader.ValidateJson` with the existing `JsonPayloadLimits.ManifestBytes` (4 MB) ceiling before parsing.
- Security tests: `TC-PERSIST-003` (conflict is a blocking, diagnosable error, not a silent trust decision); `TC-PERSIST-004` (no raw exception ever crosses the public API boundary).

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: evaluated fresh against `PLANS.md` §1.2, not presumed. This is the first task in `SLICE-01`'s implementation revision that introduces a real, code-level persisted schema (`campaign.db` system tables, `manifest.json` contract version 1) and a new Application port/contract (`ICampaignRepository`) — a direct, literal match to §1.2's "introduces or changes ... a schema ... manifest" trigger, and simultaneously to "affects authoritative state, persistence ... or diagnostics," since this is the first code that actually creates and opens authoritative local campaign state. It also spans a first-of-its-kind cross-cutting change (new bridge project, new test project, a repository-tooling guard update, a mandatory pre-implementation platform compatibility spike) that a Brief plan's "one clear implementation path, completable and validated in one focused pull request" criterion does not comfortably describe — the IL2CPP preflight in particular is exactly the kind of "requires investigation before the implementation path is known" scenario `PLANS.md` §1.2 names. ExecPlan mode is therefore independently justified, not inherited from the ADR-authoring tasks' own ExecPlan usage.
- ExecPlan path: `docs/plans/active/ODY-S01-007_Campaign_Storage_Foundation.md`
- Expected pull request count: 1 (single Draft PR covering the preflight evidence, production code, tests, and registry updates).
- Milestone or sequencing constraints: Must not begin before `ODY-S01-006`'s `SLICE-01_IMPLEMENTATION_BACKLOG.md` is merged into `main` (verified in §4). The IL2CPP compatibility preflight (section 1 of the originating ТЗ) must complete, with no unresolved blocker, before any of this task's repository code is written — satisfied, see §4/§17.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `THIRD_PARTY_NOTICES.md` (scope-column update only), `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` (`ODY-S01-007` row only).
- Documents that must not change: `ADR-011`–`014`, `docs/tasks/completed/*`, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything under `Documentation/`.
- Application version change: No — `Odyssey.*` `ApplicationVersion`/`BuildIdentity` mechanisms are untouched.
- Schema / format / contract / protocol / ruleset version change: introduces `CampaignManifestV1` (contract version 1, new) and the initial `campaign.db` system-table schema (`DatabaseSchemaVersion = "1.0.0"`, new, first commit of this value) — both are new introductions, not changes to a previously shipped version.
- Documentation version changes: None — no ADR or baseline document changes version by this task.
- Changelog or release-note requirement: None — no end-user-facing release exists yet at this development stage.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [ ] Required automated tests pass.
- [ ] Required manual checks are completed.
- [ ] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [ ] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs` — `CampaignPublicId`, `Uuid7`, `NewId()` factories.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignManifest.cs`, `CampaignSettings.cs`, `CampaignRepositoryContracts.cs` — new.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — four new codes.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCampaignRepository.cs` — new.
- `DotNet/Projects/Odyssey.Persistence.csproj`, `DotNet/Tests/Odyssey.Tests.Persistence/**` — new, added to `DotNet/Odyssey.Core.sln`.
- `DotNet/Tests/Odyssey.Tests.Contracts/ProjectContractTests.cs`, `scripts/verify-test-structure.ps1` — narrow guard updates.
- `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json` — registry additions.
- `THIRD_PARTY_NOTICES.md` — scope-column update for the now-actually-referenced dependency.
- `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` — `ODY-S01-007` row status.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| IL2CPP preflight: Unity 6000.4.0f1 Windows x64 IL2CPP Player build | Passed | `BuildResult.Succeeded`, 0 errors, 268 warnings (unrelated to SQLite). |
| IL2CPP preflight: built Player run, SQLite PRAGMA/INSERT/SELECT smoke | Passed | Logged `SP07_PREFLIGHT_PLAYER_RESULT=PASS: journal_mode=wal, insert/select round-trip ok`; result file written to `persistentDataPath`; process exited cleanly. |
| `dotnet build DotNet/Odyssey.Core.sln` | Passed | 0 warnings, 0 errors, including `Odyssey.Persistence`/`Odyssey.Tests.Persistence`. |
| `dotnet test DotNet/Odyssey.Core.sln` | Passed | 99/99 total (1 Contracts + 1 Domain + 11 Persistence + 84 Unit + 2 Architecture), 0 failed. |
| `.\scripts\restore.ps1` | Passed | All 10 `.csproj` restored, including the two new ones. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS`. |
| `.\scripts\verify-test-structure.ps1` | Passed | `TC-ARCH-001 PASS`, `TC-ARCH-002 PASS` (all four controlled-invalid fixtures). |
| `.\scripts\test-fast.ps1` | Passed | All five `dotnet test` suites green via the wrapped fast-CI path. |
| `.\scripts\check-repository-policy.ps1` | Passed | `REPO-POLICY-005 PASS` (registry complete, including the four new codes and `TC-PERSIST-*` references). |
| `.\scripts\verify-repository.ps1` | Pending | To be recorded after this section's remaining items are confirmed. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | See IL2CPP preflight rows above. |
| AC-2 | Passed | `TC-PERSIST-001`/`002` (Create path). |
| AC-3 | Passed | `TC-PERSIST-001`/`003` (Open path, conflict detection). |
| AC-4 | Passed | `TC-PERSIST-002` (`WriteManifestAtomic_SimulatedMidWriteFailure_DoesNotCorruptExistingManifest`). |
| AC-5 | Passed | `TC-PERSIST-004` (`CampaignId_And_CampaignPublicId_AreCanonicalAndUniqueAcrossManyGenerations`, `CampaignId_NewId_IsTimeSortable`). |
| AC-6 | Passed | `TC-PERSIST-004` (`Open_NonExistentCampaign_...`, `Create_OnNonEmptyExistingDirectory_...`). |
| AC-7 | Passed | Full `dotnet test DotNet/Odyssey.Core.sln` 99/99, including `RepositoryStructurePassesArchitectureGuard`. |
| AC-8 | Pending | To be confirmed once `verify-repository.ps1` is recorded. |
| AC-9 | Pending | To be confirmed after diff-scope check. |
| AC-10 | Passed | PR #28 opened as Draft, all 4 required CI checks passed (`buildidentity-provenance`, `dotnet-restore-build-test`, `repository-policy-format-structure`, `unity-project-package-static` — https://github.com/odyssey-services/Odyssey_VTT/actions/runs/32730518419), reviewed and merged into `main` (merge commit `ecc7c29`, 2026-08-24T13:07:14Z). |

## 18. Blockers, risks, and open decisions

- Blocker (resolved): the IL2CPP compatibility question was the task's own designated stop condition if it had failed — it did not; see §4/§17. No blocker remained afterward.
- Open decision (deliberate, not a blocker): `campaign.lock` concurrent-access locking was not implemented — this task's explicit scope list (from its own originating ТЗ) does not mention it; it is deferred until a future task actually needs concurrent-access protection (likely `ODY-S01-009` or later, when multiple processes/handles could plausibly race).
- Risk: the minimal `ADR-011` §8.2 system tables created here (columns beyond bare presence) are intentionally provisional — their full DDL contract belongs to `ODY-S01-009`/`010`/`011` per those ADRs' own text; a future task may need to `ALTER TABLE` them once real semantics are defined, which is expected and acceptable per `ADR-011` §8.2's own allowance, not a defect of this task.
