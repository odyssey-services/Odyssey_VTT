# ODY-S01-009 — Saving Pipeline

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s01-009-saving-pipeline`
**Pull request:** Not yet opened
**Last updated:** 2026-08-24 UTC

## 1. Purpose and user-visible outcome

Campaign creation, scene/token creation, token movement, and asset registration each become durable, atomic, and safely retryable operations: their projection write, `DomainEvents` entry, and `AppliedCommands` idempotency record land together in one SQLite transaction, and a redelivered command replays instead of double-applying.

## 2. Task contract

- Goal / acceptance criteria / requirement IDs / scope / authorities / validation commands: see `docs/tasks/active/ODY-S01-009_Saving_Pipeline.md` sections 1, 3, 5, 9, 10.

## 3. Current state

- `SqliteCampaignRepository`/`SqliteSceneRepository` write projections directly, no transaction, no event, no idempotency (verified by reading both files before this task).
- `Odyssey.Application.Commands.CommandContracts.cs` and `Odyssey.Domain.Events.DomainEvents.cs` already define a full command/event object graph, unused by any production repository code; `CommandPayload` carries no real argument data, ruling it out as a drop-in fit without a larger redesign (see task contract section 4).
- The `DomainEvents`/`AggregateRevisions`/`AppliedCommands` system tables already exist as `ODY-S01-007` placeholder DDL, explicitly deferring their full contract to this task.

## 4. Proposed approach

Introduce `SqliteSavingPipeline` (internal to `Odyssey.Persistence`) with one method, `Execute<T>`, taking: the open `SqliteConnection`, the `CampaignId`, the caller-supplied `CommandId`, a `tryReplay` callback (runs only when `AppliedCommands` already has a `Completed` row for this `CommandId`; re-reads the existing projection row via a new `LastCommandId` column) and an `apply` callback (runs the projection INSERT/UPDATE inside the transaction, returns the result plus event type/payload/aggregate info). `Execute` begins a `SqliteTransaction`, checks idempotency, invokes exactly one of the two callbacks, and on the `apply` path additionally inserts the `DomainEvents` row (SHA-256 `PayloadHash`), upserts `AggregateRevisions`, inserts the `AppliedCommands` row, and commits. Any exception or `Result.Failure` from `apply` propagates without committing anything (SQLite transaction rollback on dispose).

`ICampaignRepository.Create` and all five mutating `ISceneRepository` methods gain a `CommandId commandId` parameter (breaking change, blast radius confirmed limited to `Odyssey.Persistence` + its tests). `SqliteCampaignRepository.Open` additionally runs `PRAGMA quick_check` and an incomplete-`SchemaHistory` check before returning a handle (`05_Persistence` section 22.1).

Recovery is proven with a real process kill: a small standalone console project (`Odyssey.Tests.Persistence.RecoveryHarness`) opens the same `campaign.db`, stages an insert into `Scene`/`DomainEvents`/`AppliedCommands` inside an open transaction, prints `STAGED`, sleeps, then commits. The NUnit test spawns it, waits for `STAGED`, kills it hard mid-sleep (well before commit), then reopens the campaign and asserts the killed write is absent while a pre-kill baseline write survives.

## 5. Milestones

### M1 — Pipeline compiles and existing tests still pass

- [x] `SqliteSavingPipeline.cs` written and compiling against `Result<T> where T : notnull`.
- [x] `SqliteCampaignRepository`/`SqliteSceneRepository` routed through it; `TC-PERSIST-001`–`007` still pass (23/23 including new tests).

### M2 — New pipeline behavior proven

- [x] `TC-PERSIST-008` (atomic commit / rejected-command-leaves-nothing) passes.
- [x] `TC-PERSIST-009` (idempotent redelivery) passes.
- [x] `TC-PERSIST-010` (safe-close WAL truncation) passes.
- [x] `TC-PERSIST-011` (real kill recovery) passes.

### M3 — Repository policy and registries consistent

- [x] `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json` updated; `check-repository-policy.ps1`/`verify-repository.ps1` pass.
- [x] `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` row 3 updated.

## 6. Progress log

- 2026-08-24 — Preflight confirmed `007`/`008` merged to `main` (`aac676e`), `SqliteCampaignRepository.cs`/`SqliteSceneRepository.cs` present.
- 2026-08-24 — Read `ADR-012` full text, `ADR-011` section 8.2, `05_Persistence` section 22, existing `CommandContracts.cs`/`DomainEvents.cs`; decided against full `ApplicationCommand` integration (see task contract section 18).
- 2026-08-24 — Implemented `SqliteSavingPipeline`, updated both repositories and their contracts, fixed `T : notnull` constraint, fixed existing test call sites (all 4 mutating call shapes).
- 2026-08-24 — Built `Odyssey.Tests.Persistence.RecoveryHarness`, wrote `SqliteSavingPipelineTests.cs` (6 tests including the real kill test), all 23 tests green.
- 2026-08-24 — Ran full validation sequence (`restore`/`verify-format`/`verify-test-structure`/`test-fast`/`check-repository-policy`/`verify-repository`), fixed a CRLF issue from a scripted edit, filled registries, wrote this task contract and ExecPlan.

## 7. Decisions

- 2026-08-24 — Decision: do not integrate with `CommandContracts.cs`'s full object graph. Rationale: `CommandPayload` carries no real argument data; forcing these five operations through it would require redesigning a foundation file outside this task's scope. Authority: task contract section 5/18.
- 2026-08-24 — Decision: reuse `Odyssey.Application.Commands.CommandId`, not a new type. Rationale: one idempotency-key type across the codebase. Authority: task contract section 18.

## 8. Discoveries and deviations

- Discovered `CommandContracts.cs`/`DomainEvents.cs` already exist and are unused — not anticipated by the task's own ТЗ text, which assumed a from-scratch pipeline. Documented instead of silently building a duplicate idempotency-key type.
- `Result<T>` requires `T : notnull` — `SqliteSavingPipeline.Execute<T>` needed the same constraint, straightforward fix once the compiler surfaced it.
- A scripted (Python regex) edit to the two existing test files introduced CRLF line endings, caught by `verify-format.ps1`; fixed with `sed -i 's/\r$//'`, consistent with this session's established fix for the same recurring issue in `007`/`008`.

## 9. Validation and acceptance evidence

See task contract section 17 for full command output summaries and the acceptance-criteria table.

## 10. Recovery and rollback

Revert the branch; no persisted campaign data exists yet that depends on the new `DomainEvents.EventType`/`CommandId` columns or the `LastCommandId` columns, so no data migration or rollback procedure beyond a normal git revert is needed.

## 11. Open questions and blockers

- None blocking. Whether to eventually migrate these five operations onto the full `ApplicationCommand`/`ICommandHandler` graph is an open product/architecture question for a future task, not a blocker here.

## 12. Outcome and follow-up

Delivered: `SqliteSavingPipeline`, both repositories routed through it, quick integrity check on `Open`, safe-close WAL truncation verified by file evidence, real process-kill recovery test, all `007`/`008` tests still green, registries updated. Follow-up: none assigned yet; see task contract section 17 "Follow-up tasks" for the unassigned future consideration.
