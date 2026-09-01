# ODY-S04-101 — Character Aggregate, Lifecycle Skeleton & SQLite Persistence

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s04-101-character-aggregate-skeleton`
**Pull request:** <to be filled after `gh pr create`>
**Last updated:** 2026-09-01 UTC

## 1. Purpose and user-visible outcome

Implement the `Character` aggregate skeleton (`ADR-022` §4): identity/presentation/custom-field sections, the six-state `LifecycleStatus`/`ApprovalState` structural values, `CharacterRevision`/all twelve section revisions, and a SQLite projection plus a minimal `CharacterHistoryProjection` rebuildable from `DomainEvents` — with zero business logic for ownership, drafts, purchases, or lifecycle-boundary operations (all later tasks). This is the first real code in `SLICE-04` and unblocks all 14 remaining implementation-backlog tasks.

## 2. Task contract

- Goal: a compiling, tested Character aggregate skeleton in Domain/Application/Persistence, following the exact structural conventions `SqliteSceneRepository`/`ISceneRepository` already established for Scene/Token.
- Acceptance criteria: aggregate + lifecycle/approval structural values + section revisions/locks + SQLite projection + history-rebuild-from-events all implemented and covered by real tests; no ownership/draft/purchase/lifecycle-operation business logic; `ADR-022`–`025` and `SLICE-04_IMPLEMENTATION_BACKLOG.md` unmodified in content (only the backlog's own status row/line updated); `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1` all pass.
- Requirement IDs: `ODY-S04-101`, `ADR-022` §4–8.
- In scope: `Odyssey.Domain.Character` (enums + lifecycle transition table + section-revisions value type), `CharacterId.NewId`/`==`/`!=` (matching sibling ID types), `ICharacterRepository`/`CharacterRecord`/`CharacterHistoryEntry` (Application), `SqliteCharacterRepository` (Persistence), `PersistenceFailures`/`ErrorCodes` additions, `docs/errors/ERROR_CODES.md`/`Tests/Metadata/test-catalog.json` additions, real tests, `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update, this task's own contract/plan.
- Out of scope: Drafts/templates/approval (`ODY-S04-103`/`104`), ownership/control commands (`ODY-S04-102`), development economy (`ODY-S04-105`–`107`), ability/resource/anatomy (`ODY-S04-108`/`109`), archive/delete/Dead/restore/Ruleset migration (`ODY-S04-110`–`113`), any Unity/UI code, any `ADR-022`–`025` content change.
- Required authorities: `ADR-022` (full read), `ADR-002`/`003`/`012` (substrate, not reopened), `10_Characters_And_Progression` §6/§7/§10/§20/§21/§29, `SLICE-04_IMPLEMENTATION_BACKLOG.md` §5–7, and `SqliteSceneRepository.cs`/`ISceneRepository`/`SqliteSavingPipeline.cs`/`SqliteGameLogRepository.cs` as the binding structural precedent.
- Required validation commands: `dotnet build`; `dotnet test`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`.

## 3. Current state

- Local `main` was fast-forwarded to `origin/main` at `8f178f8` (PR #84, `ODY-S04-005` implementation backlog), independently verified via `git merge-base --is-ancestor`.
- `CharacterId` already exists in `Odyssey.Domain.Identity` (added in an earlier session for `DomainActor`) but lacked `NewId`/`==`/`!=` — the only sibling-ID-type gap found.
- `SqliteSceneRepository`/`ISceneRepository`/`SqliteSavingPipeline`/`SqliteGameLogRepository` were read in full as the binding structural precedent: per-call short-lived `SqliteConnection`, `EnsureXTables` with `CREATE TABLE IF NOT EXISTS`, `SqliteSavingPipeline.Execute` for the one-transaction journal/projection/`AppliedCommands` commit, a shared `SelectColumns`/`ReadXRecord` convention, and `Newtonsoft.Json.Linq` (`JObject`/`JArray`) as the already-approved low-level JSON API for structured payload columns (used directly in `SqliteGameLogRepository.cs`, not merely declared permissible by `ADR-003`).
- The shared `DomainEvents` table (created by `SqliteCampaignRepository`) has no `AggregateId`/`AggregateType` column of its own — every existing repository selects its own events by payload content, not by an indexed aggregate-id column; `GetCharacterHistory` follows the same convention.
- `docs/errors/ERROR_CODES.md`/`Tests/Metadata/test-catalog.json` are a real, machine-checked registry (`REPO-POLICY-005`) — new `ErrorCode`s require both a registry row and a real `TC-*` catalog entry the row's `TestReference` column points to; discovered this only after the first `check-repository-policy.ps1` run failed, then added both correctly.

Assumptions: none.

## 4. Proposed approach

- Domain: `CharacterKind`/`CharacterLifecycleStatus`/`CharacterApprovalState` enums, `CharacterLifecycleTransitions.IsValidTransition` (a pure, generic adjacency-table function over product §7.1's table, deliberately not deciding which command may take which edge), `CharacterSectionRevisions` (all twelve `ADR-022` §5 counters, `Initial()` factory).
- Application: `ICharacterRepository` (`CreateCharacter`/`GetCharacter`/`UpdateIdentity`/`UpdatePresentation`/`GetCharacterHistory`), `CharacterRecord`/`CreateCharacterRequest`/`CharacterHistoryEntry`, `PersistenceFailures.CharacterNotFound`/`CharacterIoFailed`/`CharacterRevisionConflict` plus their `ErrorCodes` entries.
- Persistence: `SqliteCharacterRepository` — one `Character` table carrying all twelve section revisions from creation (even the ten this task never touches), `UpdateIdentity`/`UpdatePresentation` each checking only their own section's expected revision (proving cross-section parallel editing and same-section conflict rejection), `GetCharacterHistory` reading only the shared `DomainEvents` table and filtering by the event payload's own `characterId` field (no dedicated, separately-maintained history table exists).
- Tests: `CharacterLifecycleTransitionsTests` (pure Domain, every table edge true, every non-table edge false) and `SqliteCharacterRepositoryTests` (real SQLite: creation per kind, cross-section no-false-conflict, same-section stale-revision rejection, history rebuild from scratch, history never crosses Characters, close/reopen persistence, not-found, duplicate-command idempotency).
- Registry: three new `ErrorCode`s registered in both `ErrorCodes.cs` and `docs/errors/ERROR_CODES.md`, referencing eight new `TC-CHAR-001`–`008` entries added to `Tests/Metadata/test-catalog.json`.
- Backlog: add a `Status` column to `SLICE-04_IMPLEMENTATION_BACKLOG.md`'s ordered-task table (it did not have one), marking row 1 `Done` with the real PR link; rows 2–15 `Not started`.

No Unity/UI code, no ownership/draft/purchase/lifecycle-operation business logic, no `ADR-022`–`025` content change.

## 5. Milestones

### M1 — Domain/Application/Persistence skeleton

- [x] `Odyssey.Domain.Character` (enums, transition table, section revisions).
- [x] `CharacterId.NewId`/`==`/`!=` added to `DomainIdentity.cs`.
- [x] `ICharacterRepository`/`CharacterRecord`/`CreateCharacterRequest`/`CharacterHistoryEntry`.
- [x] `PersistenceFailures`/`ErrorCodes` Character entries.
- [x] `SqliteCharacterRepository` implementation.

### M2 — Tests and registry

- [x] `CharacterLifecycleTransitionsTests` (Domain).
- [x] `SqliteCharacterRepositoryTests` (Persistence, real SQLite).
- [x] `docs/errors/ERROR_CODES.md` + `Tests/Metadata/test-catalog.json` entries.
- [x] `dotnet build`/`dotnet test` full suite green, no regression.

### M3 — Validation and review readiness

- [x] `.\scripts\verify-format.ps1`.
- [x] `.\scripts\check-repository-policy.ps1`.
- [x] `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- [ ] Commit, push, and open Draft PR.
- [ ] Record CI status.

## 6. Progress log

- 2026-09-01 — Preflight confirmed PR #84's merge commit is a real ancestor of `origin/main` at `8f178f8`; created branch `feat/ody-s04-101-character-aggregate-skeleton`.
- 2026-09-01 — Read `ADR-022` in full (re-confirmed), `SqliteSceneRepository.cs`/`ISceneRepository`/`SqliteSavingPipeline.cs`/`SqliteGameLogRepository.cs`/`GameLogRepositoryContracts.cs` in full as the binding structural precedent, `DomainIdentity.cs`/`DomainEvents.cs` in full, `10_Characters_And_Progression` §6/§7/§10/§20/§21/§29.
- 2026-09-01 — Implemented Domain/Application/Persistence skeleton and both test files; `dotnet build`/`dotnet test` passed on first full run after fixing compile order.
- 2026-09-01 — `check-repository-policy.ps1` first run failed on missing `ERROR_CODES.md`/test-catalog entries for the three new `ErrorCode`s; added both, second run passed.
- 2026-09-01 — Added a `Status` column to `SLICE-04_IMPLEMENTATION_BACKLOG.md`'s ordered-task table (it had none) and marked row 1 `Done`.

## 7. Decisions

- 2026-09-01 — Decision: use ExecPlan, per `SLICE-04_IMPLEMENTATION_BACKLOG.md`'s own row for this task and `PLANS.md` §1 (new public contract, persistence behavior, and authoritative lifecycle semantics). Authority: `PLANS.md` §1, backlog row 1.
- 2026-09-01 — Decision: follow `SqliteSceneRepository`/`ISceneRepository`'s exact structural convention (per-call connection, `EnsureXTables`, `SqliteSavingPipeline`, shared `SelectColumns`) rather than inventing a new persistence pattern for Character. Authority: this task's own explicit ТЗ §2 instruction to find and follow an existing similar aggregate's conventions literally.
- 2026-09-01 — Decision: create all twelve `ADR-022` §5 section revisions as real columns from day one, even though this task's own commands only ever touch two of them. Authority: `ADR-022` §5 ("every Character has... these first-version section revisions") and this task's own goal of not requiring a schema migration for every later task that starts using a different section.
- 2026-09-01 — Decision: narrow section locks (`ADR-022` §6) are satisfied for this task's own scope by transaction-scoped optimistic revision checks (the same pattern `SqliteSceneRepository.MoveToken` already uses) — no durable `SectionLocks` table is introduced, since no durable pending workflow exists in this task's own scope. Authority: `ADR-022` §6.4's explicit allowance for "ordinary synchronous commands" to use transaction-scoped locks only; a durable lock table becomes necessary only once a durable pending workflow exists (`ODY-S04-106`'s skill-5+ recommendation reservation), out of this task's scope.
- 2026-09-01 — Decision: `GetCharacterHistory` reads only the shared `DomainEvents` table, filtered by the event payload's own `characterId` field (no `AggregateId` column exists on that shared table) — proving `ADR-022` §8's "rebuildable from events, not a second source of truth" property for real, not merely by declared intent. Authority: `ADR-022` §8, and the shared `DomainEvents` table's own actual schema (verified by reading `SqliteCampaignRepository.cs`'s table-creation SQL).

## 8. Discoveries and deviations

- `CharacterId` already existed (added incidentally for `DomainActor.ActorCharacterId` in an earlier ADR-002-era task) but was missing the `NewId`/`==`/`!=` members every other aggregate-root ID type already has — added them, matching the sibling pattern exactly, not inventing a new one.
- `docs/errors/ERROR_CODES.md` and `Tests/Metadata/test-catalog.json` form a real, cross-checked registry (`REPO-POLICY-005`) that was not obvious from the ТЗ's own required-validation list — discovered only when `check-repository-policy.ps1` failed on the first run after adding the three new `ErrorCode`s. Fixed by adding both a registry row and real `TC-CHAR-001`–`008` catalog entries pointing at the actual new tests, not placeholder references.
- `SLICE-04_IMPLEMENTATION_BACKLOG.md`'s ordered-task table (authored by `ODY-S04-005`) had no `Status` column at all, unlike `SLICE-03_IMPLEMENTATION_BACKLOG.md`'s own table — added one now rather than inventing an out-of-table status note, so all future `ODY-S04-1XX` tasks have a consistent place to record their own status.
- No open architectural question was found during implementation that `ADR-022` does not already answer — no ADR was touched or extended.

## 9. Validation and acceptance evidence

- `dotnet build Odyssey.Core.sln`: 0 warnings, 0 errors.
- `dotnet test Odyssey.Core.sln`: full suite passed, including 8 new `TC-CHAR-*`-covered tests (29 Domain `CharacterLifecycleTransitionsTests` cases + 10 Persistence `SqliteCharacterRepositoryTests`), no regression in any other test project.
- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed with `Repository policy check passed` (after adding the `ERROR_CODES.md`/test-catalog entries the first run's failure required).

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR — no schema migration exists to roll back beyond `CREATE TABLE IF NOT EXISTS`, which is itself idempotent and additive; no existing table or column is altered.

## 11. Open questions and blockers

None. No architectural question was found that `ADR-022` does not already answer.

## 12. Outcome and follow-up

Draft PR: <to be filled after `gh pr create`>. CI pending. Unblocks `ODY-S04-102` through `ODY-S04-113` (all depend directly or transitively on this task).
