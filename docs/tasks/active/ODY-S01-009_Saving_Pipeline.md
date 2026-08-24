# ODY-S01-009 — Saving Pipeline

**Status:** In Review
**Roadmap stage / slice:** SLICE-01 (implementation)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s01-009-saving-pipeline`
**Pull request:** Draft — [#31](https://github.com/odyssey-services/Odyssey_VTT/pull/31) (open, awaiting owner review)
**ExecPlan:** `docs/plans/active/ODY-S01-009_Saving_Pipeline.md`
**Created:** 2026-08-24
**Last updated:** 2026-08-24 UTC

## 1. Goal

Every mutating operation `ODY-S01-007`/`008` introduced (campaign creation, scene creation, token creation, token move, asset registration) commits its projection row, its `DomainEvents` entry, and its `AppliedCommands` idempotency record in one SQLite transaction (`ADR-012` section 5), survives a hard process kill mid-transaction without leaving a partial state, and is safe to redeliver with the same `CommandId`.

## 2. Why this task exists

- Problem: `ODY-S01-007`/`008` deliberately deferred the `ADR-012` transactional journal-projection boundary; each repository method committed its projection write alone, with no event, no idempotency record, and no atomicity guarantee across projection+event.
- Value: closes the gap between the documented deferral and the actual Domain Event Store contract before any further vertical-slice task (`ODY-S01-010`+) builds on top of unrecorded history.
- Enabling relationship: `ODY-S01-010` (migration registry) needs `SchemaHistory`; `ODY-S01-011` (backups) needs `EventsSinceLastSnapshot`/event-sequence counters — both assume a real, populated `DomainEvents`/`AppliedCommands` pipeline exists first.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`
- `AGENTS.md`
- `PLANS.md`
- `ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` sections 4, 5, 7 (Domain Event Store contract, transactional boundary, idempotency)
- `ADR-011_Local_Campaign_Format_v1.1.md` sections 7.1, 7.4, 8.2
- `05_Persistence_Odyssey_VTT_v0.8.md` section 22 (integrity validation), section 23.1 (crash recovery)
- `ADR-004_Result_and_Error_Model_v1.0.md`
- `ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`
- `Tools/Spikes/SP-02-PersistenceReliability/` section 2.2 (WAL crash-recovery reference behavior, not re-tested from scratch here)

### Requirement and test IDs

- Requirement IDs: `PE-INV-004`, `PE-INV-005` (`05_Persistence` section 3)
- Existing test IDs: `TC-PERSIST-001`–`007`
- New test IDs to introduce: `TC-PERSIST-008`–`011`

### Task-safe private context

- Approved summary / references: None

## 4. Verified current state

### Verified facts

- `SqliteCampaignRepository.Create`/`Open`/`Close` and `SqliteSceneRepository.CreateScene`/`CreateToken`/`MoveToken`/`RegisterAsset` (both under `Packages/com.odyssey.persistence/Runtime/Sqlite/`) each committed their projection write with no explicit transaction, no `DomainEvents` row, and no `AppliedCommands` row, as documented in both files' own XML doc comments before this task.
- The section 8.2 system tables (`DomainEvents`, `AggregateRevisions`, `AppliedCommands`, and the rest) already existed as minimal placeholder DDL, created by `ODY-S01-007`'s `CreateSystemTables`, explicitly deferring their full contract to this task.
- `Packages/com.odyssey.application/Runtime/Commands/CommandContracts.cs` (pre-existing, from an earlier `ODY-S00-*` foundation task) already defines `CommandId`, `ApplicationCommand`, `ICommandHandler`, `ICommandCommitter`, `ICommandReceiptStore`, `CommandExecutor` — a full command-dispatch object graph with no SQLite-backed implementation anywhere in the repository (only in-memory test doubles in `CommandEventClockRngContractTests.cs` and `DeveloperShellProbe.cs`).
- `CommandContracts.cs`'s `CommandPayload` struct carries only a `PayloadType` marker string, no actual argument data — grep confirmed no production code anywhere constructs an `ApplicationCommand` for a real operation; the type appears designed as a foundation for a future networked command-dispatch layer.
- `Odyssey.Domain.Events.DomainEvents.cs` (also pre-existing) defines `DomainEvent`, `DomainEventBatch`, `EventSequence`, `CampaignRevision`, `TransactionId`, `CausationCommandId`, `AggregateRevision` — a full event-sourcing object model, also unused by any production repository code before this task.
- Only `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCampaignRepository.cs`, `SqliteSceneRepository.cs`, their two contract files, and their tests reference `ICampaignRepository`/`ISceneRepository` — confirmed via repository-wide grep, so changing those two interfaces' signatures has no blast radius outside `Odyssey.Persistence` and its own test project.

### Assumptions

- None.

## 5. Scope

### In scope

- A `SqliteSavingPipeline` (`Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSavingPipeline.cs`) implementing the `ADR-012` section 5 single-transaction group for one command: idempotency check against `AppliedCommands`, the caller's projection write, a `DomainEvents` append, an `AggregateRevisions` upsert, and an `AppliedCommands` insert, committed or rolled back together.
- Routing `SqliteCampaignRepository.Create` and `SqliteSceneRepository.CreateScene`/`CreateToken`/`MoveToken`/`RegisterAsset` through the pipeline.
- Extending the `DomainEvents` table with `EventType`/`CommandId` columns (the section 8.2 placeholder DDL already allowed its full definition to be filled in by this task) and adding a `LastCommandId` tracking column to `Campaign`/`Scene`/`Token`/`AssetManifestEntries` so a redelivered command can locate the row it already produced.
- `SqliteCampaignRepository.Open` running the `05_Persistence` section 22.1 quick check (`PRAGMA quick_check` plus "no incomplete migration state") before handing out a handle.
- A real `Process.Kill()` recovery test (`DotNet/Tests/Odyssey.Tests.Persistence.RecoveryHarness/`, a small standalone console project the test spawns and kills mid-transaction).

### Out of scope

- Full integration with `CommandContracts.cs`'s `ApplicationCommand`/`ICommandHandler`/`ICommandCommitter`/`DomainEventBatch` object graph. `CommandPayload` cannot carry these operations' real arguments without redesigning `CommandContracts.cs` itself (owned by an earlier, already-closed foundation task); forcing that redesign here would be a much larger change than "route existing repository methods through a transaction," and is not required by `ADR-012` section 5's actual text (a SQLite transactional boundary, not a specific object graph). Reusing the existing `Odyssey.Application.Commands.CommandId` type (not inventing a duplicate) keeps the door open for a future task to build the full command-dispatch layer on the same idempotency-key type.
- The compensating-event mechanism (`ADR-012` section 6) beyond what these five operations need — none of them are compensations.
- Snapshot/backup (`ODY-S01-011`), migration registry (`ODY-S01-010`), `.odcamp` export (`ODY-S01-012`).
- `ADR-011` section 7.2's full write-queue-per-campaign infrastructure as a standalone multithreaded API — only the pipeline's own SQLite transaction serializes a single command's write, which is what section 5 requires; `SqliteCampaignRepository` and `SqliteSceneRepository` still open independent connections per call, unchanged from `ODY-S01-008`.
- `GameLogEntries`/`CalculationTrace`/`NetworkOutbox` entries in the transaction group — none of these five operations produce a game-log entry or a networked notification in this offline, single-player vertical slice.
- Idempotent redelivery of `Create` against an *already-created* campaign folder: the `ODY-S01-007` empty-directory precondition rejects that case (`CampaignIoFailed`) before the pipeline ever runs, and weakening that guard to manufacture a redelivery path with no real caller yet was judged out of proportion to this task.

### Allowed paths

```text
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSavingPipeline.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCampaignRepository.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSceneRepository.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/SceneRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
DotNet/Tests/Odyssey.Tests.Persistence/*
DotNet/Tests/Odyssey.Tests.Persistence.RecoveryHarness/*
DotNet/Odyssey.Core.sln
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S01-009_Saving_Pipeline.md
docs/plans/active/ODY-S01-009_Saving_Pipeline.md
```

### Paths requiring explicit approval before editing

```text
Packages/com.odyssey.application/Runtime/Commands/CommandContracts.cs
Packages/com.odyssey.domain/Runtime/Events/DomainEvents.cs
docs/tasks/active/ODY-S01-007_Campaign_Storage_Foundation.md
docs/tasks/active/ODY-S01-008_Scene_And_Token_Minimal_Model.md
```

## 6. Technical constraints

- Module ownership and dependency direction: repository ports stay in `Odyssey.Application.Persistence`; the pipeline implementation is `internal` to `Odyssey.Persistence` (`ADR-001` section 6.5) — nothing outside `Odyssey.Persistence` calls `SqliteSavingPipeline` directly.
- Authoritative-state and transaction boundary: `ADR-012` section 5 — projection + `DomainEvents` + `AggregateRevisions` + `AppliedCommands` commit as one SQLite transaction; no partial commit.
- Time / RNG rule: all timestamps come from the existing `IWallClock` already threaded through both repositories (`ADR-008`); the pipeline introduces no new time or RNG source. `Guid.NewGuid()` is used only for opaque test `CommandId` values in the test project (`ADR-008`'s explicitly permitted use), never in production code.
- Unity / thread / lifetime rule: unchanged from `ODY-S01-007`/`008` — short-lived connections, no persistent background thread.
- Dependency / licensing rule: no new package dependency in production code; the new `Odyssey.Tests.Persistence.RecoveryHarness` console project references the same already-approved `Microsoft.Data.Sqlite`/`SQLitePCLRaw.bundle_e_sqlite3` packages, test-only.
- Security / privacy / redaction rule: `DomainEvents.PayloadJson` for these five events carries only identifiers/coordinates already public within campaign scope (`ADR-012` section 4.4 does not apply — nothing here is GM-private).
- Performance or platform constraint: Not applicable.
- Other: Not applicable.

## 7. Expected behavior

### Scenario 1 — Atomic commit

**Given** an open campaign
**When** `CreateScene` succeeds
**Then** the `Scene` row, its `DomainEvents` row, its `AggregateRevisions` row, and its `AppliedCommands` row all exist, mutually consistent (`TC-PERSIST-008`).

### Scenario 2 — Rejected command leaves nothing

**Given** an open campaign
**When** `CreateToken` is called against a nonexistent `SceneId`
**Then** no row exists in `Token`, `DomainEvents`, or `AppliedCommands` for that `CommandId` (`TC-PERSIST-008`).

### Scenario 3 — Idempotent redelivery

**Given** a `CreateScene` or `MoveToken` command already committed under `CommandId` X
**When** the same call is redelivered with the same `CommandId` X
**Then** the stored outcome is returned and no second row/event is created (`TC-PERSIST-009`).

### Scenario 4 — Safe close

**Given** an open campaign with pending WAL writes
**When** `Close` is called
**Then** `campaign.db-wal` is truncated to zero bytes (`TC-PERSIST-010`).

### Scenario 5 — Hard-kill recovery

**Given** a transaction staged but not committed
**When** the writing process is hard-killed mid-transaction
**Then** reopening the campaign passes the quick integrity check, the killed write never appears, and state committed before the kill survives intact (`TC-PERSIST-011`).

### Required invariants

- No successful projection write without its `DomainEvents` row in the same transaction, and vice versa (`PE-INV-005`).
- `EventSequence` is the sole ordering source; no code path uses a timestamp for ordering (`ADR-012` section 4.1).

## 8. Deliverables

- Production code: `SqliteSavingPipeline.cs`; updated `SqliteCampaignRepository.cs`/`SqliteSceneRepository.cs`; updated `ICampaignRepository`/`ISceneRepository` signatures; two new `ErrorCodes`.
- Tests: `SqliteSavingPipelineTests.cs` (`TC-PERSIST-008`–`011`); updated `SqliteCampaignRepositoryTests.cs`/`SqliteSceneRepositoryTests.cs` call sites.
- Scripts / CI: None (existing scripts cover the new project).
- Configuration: `DotNet/Odyssey.Core.sln` gains `Odyssey.Tests.Persistence.RecoveryHarness`.
- Documentation: `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md`, this task contract, its ExecPlan.
- Generated evidence or build artifacts: None persisted; test run output recorded in section 17.
- Migration / recovery material: None — no shipped campaign data exists yet, so the `DomainEvents`/`Campaign`/`Scene`/`Token`/`AssetManifestEntries` schema additions need no upcaster.

## 9. Acceptance criteria

1. `SqliteSavingPipeline.Execute` commits projection + `DomainEvents` + `AggregateRevisions` (when supplied) + `AppliedCommands` as one SQLite transaction; a rejected `apply` callback leaves no row in any of the three tables (`TC-PERSIST-008`).
2. Redelivering `CreateScene` or `MoveToken` with the same `CommandId` returns the original outcome without a second `DomainEvents` row or a second effect (`TC-PERSIST-009`).
3. `Close` truncates `campaign.db-wal` to zero bytes (`TC-PERSIST-010`).
4. `Open` runs `PRAGMA quick_check` and an incomplete-migration-state check before returning a handle; a real hard-killed mid-transaction write never appears after reopening, while state committed before the kill survives (`TC-PERSIST-011`).
5. All `ODY-S01-007`/`008` tests (`TC-PERSIST-001`–`007`) continue to pass unmodified in behavior (only call sites gained a `CommandId` argument).
6. `007`'s and `008`'s task contract files, branches, and PRs are untouched by this task's diff except through an already-merged `main` fast-forward.
7. All required validation commands (section 10) pass.

The task is not complete while any mandatory criterion is unverified, failed, or silently deferred.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-PERSIST-008` | .NET / `dotnet test` | Atomic commit group; rejected command leaves no partial row | Pass |
| `TC-PERSIST-009` | .NET / `dotnet test` | Idempotent redelivery via `AppliedCommands` | Pass |
| `TC-PERSIST-010` | .NET / `dotnet test` | Safe-close WAL checkpoint | Pass |
| `TC-PERSIST-011` | .NET / `dotnet test` | Real `Process.Kill()` recovery + quick_check | Pass |

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

- OS / architecture: Windows 10/11 x64 (development machine; CI runs on `ubuntu-latest` per the existing workflow, which the pure .NET `DotNet/Odyssey.Core.sln` build/test targets support).
- Unity editor or Player profile: Not applicable — no Unity/IL2CPP-specific code added; `Odyssey.Persistence` already passed its `ODY-S01-007` IL2CPP preflight, and this task adds no new dependency or platform-sensitive API.
- Scripting backend: Not applicable.
- Network topology or database fixture: Local SQLite file per test, `Path.GetTempPath()`-based, cleaned up per test.
- Other: None.

### Validation not required by this task

- A second, independent IL2CPP compatibility preflight — `SqliteSavingPipeline` uses only `Microsoft.Data.Sqlite` APIs already proven IL2CPP-compatible by `ODY-S01-007`'s preflight, and introduces no new NuGet dependency.

## 11. Compatibility, migration, and rollback

- Compatibility impact: `ICampaignRepository.Create` and all five `ISceneRepository` mutating methods gain a `CommandId commandId` parameter — a breaking signature change. Verified blast radius is limited to `Odyssey.Persistence`'s own implementations and `Odyssey.Tests.Persistence`; no other module calls these interfaces yet.
- Version fields affected: None — `CampaignFormatVersion`/`DatabaseSchemaVersion` are unchanged; the `DomainEvents`/`Campaign`/`Scene`/`Token`/`AssetManifestEntries` DDL changes only fill in previously-placeholder columns on tables no shipped campaign has data in yet.
- Migration or upcaster: None required (no existing campaign data to migrate).
- Forward / backward behavior: Not applicable — pre-release, no compatibility surface yet.
- Rollback method: Revert the branch; no persisted data depends on the new columns.
- Data-loss risk and protection: None — new columns only, no destructive schema change.
- Recovery rehearsal required: Yes — performed via `TC-PERSIST-011`'s real `Process.Kill()` test.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| `Microsoft.Data.Sqlite` / `SQLitePCLRaw.bundle_e_sqlite3` | `9.0.10` / `3.0.3` (test-only reference in `Odyssey.Tests.Persistence.RecoveryHarness`) | Recovery-test harness needs the same SQLite driver as production to write a realistic uncommitted transaction | MIT / MIT | Already approved for production use by `ADR-011` v1.1 section 1; this is a test-only additional reference to the same already-approved package/version, no new license review needed |

## 13. Security, privacy, and hidden information

- Data classes handled: Campaign/scene/token identifiers and numeric positions only — no player-private or GM-private content in this vertical slice.
- Trust boundaries: Not applicable — single local process, no network boundary crossed.
- Authorization / audience checks: Not applicable.
- Redaction requirements: Not applicable (`ADR-012` section 4.4 visibility/redaction concerns Networking projections, not introduced here).
- Log-safe fields: `DomainEvents.PayloadJson` contains only identifiers and coordinates, consistent with the existing `PersistenceFailures` error messages' no-raw-path/no-raw-exception convention.
- Abuse / malformed input limits: Unchanged from `ODY-S01-007`/`008` (existing `sceneName`/`campaignName` length checks).
- Security tests: Not applicable beyond the existing typed-error tests.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: This task changes an Application port (`ICampaignRepository`/`ISceneRepository` signatures — `PLANS.md` section 1.2 trigger), touches authoritative state and the transaction boundary (`ADR-012` section 5), and spans two production modules (`Odyssey.Application`, `Odyssey.Persistence`) plus a new test-support project — not eligible for a brief plan under `PLANS.md` section 1.1.
- ExecPlan path: `docs/plans/active/ODY-S01-009_Saving_Pipeline.md`
- Expected pull request count: 1
- Milestone or sequencing constraints: None beyond `007`/`008` already being merged.

## 15. Documentation and versioning impact

- Documents that must change: `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` (row 3 only).
- Documents that must not change: `007`/`008` task contracts, any ADR.
- Application version change: No — `manifest.json`'s `CoreApplicationVersion` field is a build-identity value, not touched by a persistence-layer task.
- Schema / format / contract / manifest / protocol / ruleset version change: None — `CampaignFormatVersion`/`DatabaseSchemaVersion` unchanged (see section 11).
- Documentation version changes: None.
- Changelog or release-note requirement: None (no release process active yet for `SLICE-01`).

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
- [x] Pull request explains changes, evidence, limitations, and follow-up work. — [PR #31](https://github.com/odyssey-services/Odyssey_VTT/pull/31).
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSavingPipeline.cs` — new ADR-012 section 5 transactional pipeline.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCampaignRepository.cs` — `Create` routed through the pipeline; `Open` runs the quick integrity check; `DomainEvents`/`Campaign` DDL extended.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSceneRepository.cs` — `CreateScene`/`CreateToken`/`MoveToken`/`RegisterAsset` routed through the pipeline; `Scene`/`Token`/`AssetManifestEntries` DDL extended.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs`, `SceneRepositoryContracts.cs` — `CommandId` parameter added; two new `PersistenceFailures` factories.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — `PersistenceIntegrityCheckFailed`, `PersistenceCommandReplayFailed`.
- `DotNet/Tests/Odyssey.Tests.Persistence/SqliteSavingPipelineTests.cs` — new, 6 tests.
- `DotNet/Tests/Odyssey.Tests.Persistence/SqliteCampaignRepositoryTests.cs`, `SqliteSceneRepositoryTests.cs` — call sites updated with `CommandId`.
- `DotNet/Tests/Odyssey.Tests.Persistence.RecoveryHarness/` — new console project, kill-test support only.
- `DotNet/Odyssey.Core.sln` — new project added.
- `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json` — registry additions.
- `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` — row 3 status.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\restore.ps1` | Passed | All 11 projects restored, including the new harness. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS` after normalizing CRLF introduced by a scripted edit. |
| `.\scripts\verify-test-structure.ps1` | Passed | `TC-ARCH-001`/`TC-ARCH-002` all PASS; no forbidden-global-API hits in the new files. |
| `.\scripts\test-fast.ps1` | Passed | `Odyssey.Tests.Persistence.dll`: 23/23 (up from 17); `Odyssey.Tests.Unit.dll`: 84/84; `Odyssey.Tests.Architecture.dll`: 2/2; `Odyssey.Tests.Domain.dll`/`Odyssey.Tests.Contracts.dll`: 1/1 each. |
| `.\scripts\check-repository-policy.ps1` | Passed | `REPO-POLICY-005 PASS ErrorCode registry is complete and machine-checkable` (both new codes registered). |
| `.\scripts\verify-repository.ps1` | Passed (after this contract file was created) | `TC-ARCH-001` initially FAILed with "Test catalog entry TC-PERSIST-008 references missing task contract: ODY-S01-009" until this file existed — expected sequencing, not a defect. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `CreateScene_CommitsProjectionEventAndAppliedCommand_AsOneConsistentGroup`, `CreateToken_OnRejectedCommand_LeavesNoEventOrAppliedCommandRow` |
| AC-2 | Passed | `CreateScene_RedeliveredWithSameCommandId_ReplaysStoredOutcome_DoesNotDuplicateEffect`, `MoveToken_RedeliveredWithSameCommandId_ReplaysStoredPosition_DoesNotMoveTwice` |
| AC-3 | Passed | `Close_TruncatesWalFile_SafeCloseCheckpoint` |
| AC-4 | Passed | `Open_AfterHardKillMidTransaction_RecoversCleanly_KilledWriteNeverAppears` (real `Process.Kill(entireProcessTree: true)`, ~1s test duration) |
| AC-5 | Passed | `TC-PERSIST-001`–`007` all still pass (23 total tests, 0 failed) |
| AC-6 | Passed | `git diff --name-status` confirms no `007`/`008` branch touched; `007`'s task contract file changed only via an already-merged `main` fast-forward, not by this task's own commits |
| AC-7 | Passed | See Validation results above |

### Build and artifact evidence

- Build identity: Not applicable (no Unity Player build in this task).
- Artifact path / name: `artifacts/bin/Odyssey.Persistence/debug/Odyssey.Persistence.dll`, `artifacts/bin/Odyssey.Tests.Persistence.RecoveryHarness/debug/Odyssey.Tests.Persistence.RecoveryHarness.dll`.
- Checksums: Not recorded — debug local build, not a release artifact.
- Test or quality report: `dotnet test` console output (section above); no `.trx` archived separately beyond `test-fast.ps1`'s own `Logs/ODY-S00-008/dotnet/*.trx` (script's own log naming, unrelated to this task's number).

### Known limitations

- No full integration with `CommandContracts.cs`'s `ApplicationCommand`/`ICommandHandler`/`ICommandCommitter` object graph — see section 5 "Out of scope" for the reasoning. A future task should decide whether to extend `CommandPayload` to carry real argument data and migrate these five operations onto it, or keep them on the lighter `SqliteSavingPipeline` permanently.
- `RegisterAsset`'s file copy is not part of the SQLite transaction (filesystem operations cannot join a SQLite transaction); a process killed between the file copy and the transactional insert leaves an orphaned file in `Assets/Objects/` with no `AssetManifestEntries` row. This is a pre-existing characteristic from `ODY-S01-008`, not introduced by this task, and is not covered by an automated test here.
- `Create`'s `CommandId` parameter cannot exercise a real idempotent-replay path today (see section 5's explicit note); it is accepted and validated but the `tryReplay` branch is unreachable given the current empty-directory precondition.

### Follow-up tasks

- Consider a future task (no ID assigned) to decide whether `CommandContracts.cs`'s full command-dispatch graph should absorb these five operations once real argument-carrying payloads are designed.

### Self-review summary

- Scope review: Stayed within the five named operations and the transactional boundary; did not implement compensating events, snapshots, migrations, or the full write-queue.
- Architecture review: `SqliteSavingPipeline` is `internal` to `Odyssey.Persistence`; no new Application-layer abstraction was introduced since nothing outside `Odyssey.Persistence` needs one (`ADR-001` section 6.5 respected).
- Test review: All new behavior has a real, executable test; the recovery test uses an actual `Process.Kill()`, not an in-memory simulation, per the task instruction.
- Security/privacy review: No new data class introduced; existing no-raw-exception/no-raw-path error conventions preserved in the two new `PersistenceFailures` factories.
- Documentation/version review: Only the registries and backlog row required updates; no ADR or version field touched.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-08-24 — Did not route through `CommandContracts.cs`'s `ApplicationCommand`/`ICommandHandler`/`ICommandCommitter` graph; built a lighter `internal` `SqliteSavingPipeline` instead. Authority: this task's own scope judgment, recorded in section 5 "Out of scope" and section 17 "Known limitations" with full reasoning.
- 2026-08-24 — Reused `Odyssey.Application.Commands.CommandId` (not a new type) as the idempotency-key parameter added to `ICampaignRepository`/`ISceneRepository`. Authority: avoids two parallel idempotency-key types in the same codebase.
- 2026-08-24 — Left `Create`'s idempotent-replay path effectively unreachable rather than weakening the `ODY-S01-007` empty-directory precondition. Authority: no real caller need identified; documented as a known limitation instead of expanding scope to manufacture one.

### Approved task changes

- None.
