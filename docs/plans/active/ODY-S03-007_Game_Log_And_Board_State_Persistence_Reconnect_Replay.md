# ExecPlan — ODY-S03-007: Game Log & Board State Persistence, Reconnect Replay

**Governing task contract:** `docs/tasks/active/ODY-S03-007_Game_Log_And_Board_State_Persistence_Reconnect_Replay.md`
**Status:** Complete (deliverable produced; PR pending CI/review)
**Created:** 2026-08-26
**Last updated:** 2026-08-26 UTC

## Authorities

- `09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md` section 23 (`GameLogEntry` structure), section 35 (Persistence contract -- transaction boundary, recommended tables/indexes).
- `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` -- full document, especially section 4.1 (`EventSequence` as sole authoritative order), section 5 (one-transaction journal/projection commit boundary), section 7 (`AppliedCommands` idempotency).
- `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` -- full document, especially section 1 point 8/section 9 step 5 (redaction always by current, not saved, permissions) and section 3.1's terminology split (`ProjectionSnapshot`, network, vs. `Snapshot`, persistence/`ADR-012`).
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSceneRepository.cs`/`SqliteSavingPipeline.cs` (`SLICE-01`, extended `ODY-S03-004`) -- the existing journal-pipeline, read in full as the direct structural template.
- `Packages/com.odyssey.application/Runtime/Dice/DiceRollStore.cs`/`DiceRollService.cs` (`ODY-S03-005`) -- the in-memory store this task adds a durable counterpart to.
- `Packages/com.odyssey.application/Runtime/Dice/DiceRollVisibilityPolicy.cs`, `Packages/com.odyssey.application/Runtime/Audience/AudienceContracts.cs` (`ODY-S03-006`) -- reused unmodified for reconnect-time audience-aware reading.
- `Packages/com.odyssey.application/Runtime/Networking/Reconnect/ReconnectContracts.cs` (`ODY-S02-012`) -- read to decide non-reuse (see Investigation performed, point 5).
- `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` section 5 (`ODY-S03-007`'s already-fixed task boundary, not reopened).

## Investigation performed

1. Read `09_Dice_And_Game_Log` section 23 (`GameLogEntry`'s full field list) and section 35 (persistence transaction boundary, recommended `DiceRolls`/`GameLogEntries` tables and indexes, `AuthoritativeSequence` as the ordering column).
2. Read `ADR-012` in full: confirmed `EventSequence` (section 4.1) is the sole authoritative order (never a timestamp), the one-transaction journal/projection commit boundary (section 5) that must cover projection rows + `DomainEvent` + `GameLogEntry`/`CalculationTrace` + `AppliedCommands` together, and the `AppliedCommands` exactly-once-effect mechanism (section 7) this task's idempotency test relies on.
3. Read `ADR-017` in full: confirmed it defines a **network-level** application protocol (`ProjectionSnapshot`/`ProjectionDeltaBatch` over `ISessionTransport`, section 1) with its own `SessionDeltaBuffer` (`ODY-S02-012`'s `ReconnectContracts.cs`) scoped to one live session and incapable of surviving a process restart -- section 3.1 explicitly forbids confusing this with `ADR-012`'s persistence-level `Snapshot`. Decided (task contract section 3) that this task's own "reconnect" (reopening a campaign database, no live network in this revision) is a different mechanism entirely: it reuses only `ADR-017` section 1 point 8's underlying *principle* (redaction always computed by current, not saved, permissions) applied outside networking, never `SessionDeltaBuffer`/`ProjectionSnapshot` themselves.
4. Read `SqliteSceneRepository.cs`/`SqliteSavingPipeline.cs` in full: confirmed the existing `SqliteSavingPipeline.Execute<T>` generic already implements ADR-012 section 5's one-transaction commit (current-state row + `DomainEvent` + `AppliedCommands`) and section 7's replay-by-`CommandId` idempotency, reusable as-is for a new aggregate pair.
5. Read `ReconnectContracts.cs` (`ODY-S02-012`) far enough to confirm `SessionDeltaBuffer`/`BufferedDelta` are in-memory, per-session, transport-coupled constructs with no persistence of their own -- directly confirming point 3's non-reuse decision, by the same reasoning `ODY-S03-004` used to decide `SqliteSceneRepository` over `ODY-S02-011`'s in-memory command path for a different aggregate.
6. Decided (task contract section 3) `DiceRoll`/`GameLogEntry` persistence form: a new `IGameLogRepository` port (`Odyssey.Application.Persistence`) plus `SqliteGameLogRepository` implementation (`Odyssey.Persistence.Sqlite`), mirroring `ISceneRepository`/`SqliteSceneRepository`'s exact port/implementation split for a different aggregate pair, reusing the shared `SqliteSavingPipeline` rather than duplicating its transaction/idempotency logic.
7. Decided (task contract section 3) to narrow `GameLogEntryRecord` to the single entry kind this task actually produces (`DiceRollResolved`), carrying the full re-hydrated `DiceRoll` (including its own `Audience`) rather than a second, independently-drifting `VisibilityAudience` field -- `ActionLogGroup`, disclosure-change commands, comments/tags, and full-text search (product doc sections 24/26-27) are out of scope per the backlog's own boundary.
8. Decided (task contract section 3) to extend `SqliteSavingPipeline.PipelineWrite<T>` with two small, backward-compatible optional hooks (`OnEventSequenceAssigned`, an in-transaction pre-commit callback; `WithEventSequence`, a post-commit result finalizer) -- necessary because `GameLogEntries.AuthoritativeSequence` must store `ADR-012`'s own `EventSequence`, which is not known until after the pipeline's internal `AppendDomainEvent` call, a genuine gap in the existing pipeline's contract that no prior caller (`SqliteSceneRepository`) needed to close.
9. Decided (task contract section 3) not to persist `DiceRoll.RngProofs` -- non-secret diagnostic/audit evidence (per `RngContracts.cs`'s own doc comment), not required to explain an outcome to a player (exit criterion 5 is satisfied by `NaturalResults`/`ModifierEntries`/`FinalTotal` alone) -- a documented, deliberate scope narrowing, not a silently dropped requirement.
10. Confirmed via `Grep` that no `IGameLogRepository`/`SqliteGameLogRepository`/`GameLogReconnectService` existed anywhere in the repository prior to this task.

## Intended change

- New: `Packages/com.odyssey.application/Runtime/Persistence/GameLogRepositoryContracts.cs` -- `IGameLogRepository`, `GameLogEntryRecord`.
- Changed: `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` (`PersistenceFailures.GameLogIoFailed`), `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` (1 new code).
- New: `Packages/com.odyssey.application/Runtime/GameLog/GameLogReconnectService.cs` -- reconnect-time audience-aware filtering, reusing `DiceRollVisibilityPolicy` unmodified.
- New: `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteGameLogRepository.cs` -- `DiceRolls`/`GameLogEntries` tables, `IGameLogRepository` implementation via the shared pipeline.
- Changed: `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSavingPipeline.cs` -- two new optional `PipelineWrite<T>` hooks (see Investigation point 8), fully backward compatible with `SqliteSceneRepository`'s existing usage.
- New tests: `DotNet/Tests/Odyssey.Tests.Persistence/SqliteGameLogRepositoryTests.cs` (`TC-PERSIST-032`-`035`): restart-restores-identical-roll, idempotent redelivery, revoked-group-membership-hides-entry-at-reconnect (with safe-denial/MainGM-always-sees confirmation), and a token-persists-across-a-new-`SqliteSceneRepository`-instance board-restart proof.
- Registry updates: `docs/errors/ERROR_CODES.md` (1 new row), `Tests/Metadata/test-catalog.json` (4 new `TC-PERSIST-*` entries).
- New: this task's contract, this ExecPlan, `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` (`ODY-S03-006` row -- fixing its placeholder Draft-PR-link text to the real merged PR #60 link and `Done` status per the same rule already applied to `ODY-S03-001`/`004`/`005`; `ODY-S03-007` row -- new `In Review` status).

## Tests or validation commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

```bash
dotnet build DotNet/Odyssey.Core.sln
dotnet test DotNet/Odyssey.Core.sln
```

## Explicit non-goals

- No real network -- this task is campaign-persistence-only; `ODY-S02-012`'s `ReconnectContracts.cs`/`SessionDeltaBuffer` mechanism is not reopened, not duplicated, and not touched.
- No full-text search, session archive/export (`SLICE-03_IMPLEMENTATION_BACKLOG.md` section 2.2, not reopened).
- No board features beyond `ODY-S03-004`'s already-implemented scope (drawing, ruler, etc. remain out of scope).
- No `ActionLogGroup`, disclosure-change commands, comments, tags, or full-text search over the game log (product doc sections 24/26-27).
- No persisted `DiceRoll.RngProofs` (Investigation point 9) -- a documented limitation, not silently dropped.
- No edit to `ADR-012`/`ADR-017`.
