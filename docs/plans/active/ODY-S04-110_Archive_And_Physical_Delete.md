# ODY-S04-110 — Archive & Dependency-Aware Physical Delete

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s04-110-archive-physical-delete`
**Pull request:** [#94](https://github.com/odyssey-services/Odyssey_VTT/pull/94)
**Last updated:** 2026-09-02 UTC

## 1. Purpose and user-visible outcome

Implement `ADR-025` §5: `ArchiveCharacter` as an ordinary `Lifecycle`-section transition; `DeleteCharacterPermanently` — MainGM-only, host-authoritative (extensible, currently empty) dependency check, real backup reuse, live-row-only removal, and proof that `CharacterHistoryProjection` continues to render the deleted Character's past purely from `ADR-022`'s already-required historical event snapshots. Tenth implementation task of `SLICE-04`.

## 2. Task contract

- Goal: implement both commands per `ADR-025` §5, resolving the two design questions the ТЗ explicitly delegates (§1.1's extensible dependency-checker shape, §1.3's `ArchiveCharacter` actor decision) and reusing the existing `IBackupRepository` (§1.2) rather than inventing new mechanisms.
- Acceptance criteria: `ArchiveCharacter` transitions any legal source status to `Archived` via the existing `CharacterLifecycleTransitions.IsValidTransition` table, rejects `Archived → Archived`, and is gated by MainGM-or-assigned (not MainGM-only); `DeleteCharacterPermanently` is MainGM-only, requires a `ReasonCode`, checks an extensible (currently empty) dependency-checker list that actually influences the decision when non-empty, creates a real campaign backup via `IBackupRepository.CreateBackup` before committing, removes only the live `Character` row, never deletes any `DomainEvents` row, and leaves `GetCharacterHistory` fully intact with correct historical snapshots after the delete; both commands are `CommandId`-idempotent, verified against real state; `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1` all pass; no `ADR-022`/`025` content change; no edit to Board/GameLog/Backup persistence code beyond using `IBackupRepository` as-is.
- Requirement IDs: `ODY-S04-110`, `ADR-025` §5, `ADR-022` §7–8, product §22, requirements 47/61.
- In scope: `ICharacterDeletionDependencyChecker` (new Application-port interface), `ICharacterRepository`/`SqliteCharacterRepository` extension (`ArchiveCharacter`/`DeleteCharacterPermanently`), `SqliteCharacterRepository` constructor extended with optional `IBackupRepository`/checker-list parameters, `HistoryEventTypes` extended with the two new event types, tests, error registry/test-catalog additions, backlog status update.
- Out of scope: real Board/Item/GameLog dependency checking (no such cross-reference exists anywhere in this codebase — confirmed by search, for all three named sources at once, a stronger gap than `108`/`109`'s own single-source stub); Dead/`CharacterRestored` (`ODY-S04-111`); `.odchar` Export/Import, Ruleset migration (`ODY-S04-112`/`113`); `RestoreFromArchive` (not named in this task's own backlog row — explicitly not added); any Unity/UI code; any change to `ADR-022`/`025`/backlog beyond the status row; any change to Board/GameLog/Backup persistence code beyond calling `IBackupRepository` as it already exists.
- Required authorities: `ADR-025` §5 (full read), `ADR-022` §7–8 (historical snapshot/projection), product §22 (full read)/§22.3/§7.1/requirements 47/61, `BackupRepositoryContracts.cs`/`SqliteBackupRepository.cs` (full read), `SLICE-04_IMPLEMENTATION_BACKLOG.md` §5–7, `ODY-S04-101`–`104`'s own code (`ApproveCharacterDraft` as the `Lifecycle`-transition template).
- Required validation commands: `dotnet build`; `dotnet test`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`.

## 3. Current state

- Local `main` fast-forwarded to `593d496` (PR #93, `ODY-S04-109`'s merge commit), independently verified via `git merge-base --is-ancestor`.
- Direct search across `Packages/com.odyssey.persistence` confirms no Board/Scene, GameLog, or any other existing persistence implementation stores a `CharacterId` anywhere — all three of ADR-025 §5.2's named dependency sources are simultaneously unimplementable today, a stronger gap than ODY-S04-108/109's own single-source item-dependency stub.
- `IBackupRepository`/`SqliteBackupRepository` (`ODY-S01-011`) already implement exactly product §22.2's "создание резервной копии" step — confirmed by full read.
- `CharacterLifecycleTransitions.IsValidTransition` (from `ODY-S04-101`) already encodes every `-> Archived` edge product §7.1 names (`Draft|Active|Inactive|Retired|Dead -> Archived`) and already rejects `Archived -> Archived` (the `from == to` short-circuit) — no Domain-layer change needed at all for this task.
- `GetCharacterHistory`'s own `HistoryEventTypes` array is a hand-maintained filter whitelist that every prior task extends with its own new event type(s) — an established, expected touch point, not a modification of prior tasks' own logic.

Assumptions: none.

## 4. Proposed approach

- **§1.1 dependency-checker shape:** `ICharacterDeletionDependencyChecker` (new interface, `Odyssey.Application.Persistence`) with one method, `string? CheckBlockingDependency(CampaignHandle, CharacterId)` — returns a description string if blocked, `null` if not. `SqliteCharacterRepository`'s constructor gains an optional `IReadOnlyList<ICharacterDeletionDependencyChecker>? deletionDependencyCheckers = null`, defaulting to `Array.Empty<...>()`. This is a real, invokable extension point (not a hard-coded "no dependencies" literal) — a future Board/Item/GameLog task registers its own real checker here without changing `DeleteCharacterPermanently`'s own shape, and the extensibility is proven by a test that injects a checker reporting a dependency and confirms it actually blocks the delete.
- **§1.2 backup reuse:** `SqliteCharacterRepository`'s constructor also gains an optional `IBackupRepository? backupRepository = null`, defaulting to `new SqliteBackupRepository(clock)` — mirrors `_pipeline`'s own self-construction convention exactly, so the existing single-argument constructor (every current caller/test) is unaffected. `DeleteCharacterPermanently` calls `_backupRepository.CreateBackup(campaign, "pre-delete-character:<CharacterId>", correlationId)` before the delete transaction opens (on its own connection, so it never contends with this repository's own write lock).
- **§1.3 `ArchiveCharacter` actor:** MainGM-or-assigned (`CharacterOwnershipAssignment.IsAssignedCharacter`, `PurchaseAttributeIncrease`'s own convention) — NOT MainGM-only. `ADR-025` §5.1's own text explicitly declines to restrict `Character.Archive` beyond the existing permission model, and product §26's MVP MainGM-exclusive list (`GrantDevelopment`/`Respec`/`ManageOwnership`/`RestoreDead`) conspicuously omits it, unlike `DeleteCharacterPermanently`, which product §22.2 states is MainGM-only in so many words.
- **Persistence:** `ArchiveCharacter` is a direct structural copy of `ApproveCharacterDraft`'s own `Lifecycle`-transition shape (load, revision check, `IsValidTransition` gate, one `UPDATE`, one event) — the only difference is the permission check (loaded-then-evaluated MainGM-or-assigned, instead of hoisted MainGM-only) and the target status. `DeleteCharacterPermanently` has its own dedicated method: cheap gates first, then (unless this `CommandId` was already applied — checked directly against `AppliedCommands` to avoid mis-reporting `CharacterNotFound` for a legitimate replay after the row is gone) a non-authoritative pre-check plus the real backup, then the actual delete transaction (host-authoritative re-check of the same conditions, `DELETE FROM Character`, one `CharacterDeleted` event carrying `ADR-022` §7's minimum snapshot). Returns non-generic `Result` (via the already-existing `Unit`/`Result` types) since no live `CharacterRecord` remains to return.
- Tests: `ArchiveCharacter` from every legal source status, the illegal `Archived → Archived` repeat, both actor outcomes, duplicate-`CommandId`; `DeleteCharacterPermanently` permission/reason gates, empty-checker-list success, real backup creation (verified via `ListBackups`), post-delete `GetCharacter`=not-found plus intact `GetCharacterHistory` with correct snapshots, a direct `DomainEvents` row-count check (never decreases), duplicate-`CommandId`, and the blocking-checker extensibility proof.

No Unity/UI code, no `ADR-022`/`025` content change, no Board/GameLog/Backup persistence-code change beyond using `IBackupRepository` as-is.

## 5. A real bug found and fixed during this task

The first version of `DeleteCharacterPermanently`'s duplicate-`CommandId` handling ran its own non-authoritative pre-check (a plain `GetCharacter` call) unconditionally, before ever reaching `SqliteSavingPipeline.Execute`'s own `AppliedCommands`-based replay detection. On a genuine duplicate delivery, the live `Character` row is already gone from the first, successful delete — so the pre-check itself returned `CharacterNotFound` and the whole method failed, instead of replaying the stored success. Caught directly by `DeleteCharacterPermanently_DuplicateCommandId_DoesNotDuplicateEffect` on the first test run. Fixed by checking `AppliedCommands` for this exact `CommandId` first (`IsCommandAlreadyApplied`) and skipping the pre-check/backup entirely when it is already-applied, letting the pipeline's own `tryReplay` (`ReplayCharacterDeleted`, checking `DomainEvents` directly since there is no live row to look up) handle the replay as it already does for every other command.

## 6. Milestones

### M1 — Domain/Application extension

- [x] `ICharacterDeletionDependencyChecker` (Application); `ICharacterRepository.ArchiveCharacter`/`DeleteCharacterPermanently`; `PersistenceFailures`/`ErrorCodes` additions (four new entries).
- [x] Confirmed via `Grep`: no Board/GameLog/other persistence code stores `CharacterId`; `CharacterLifecycleTransitions` already covers every `-> Archived` edge with no Domain change needed.

### M2 — Persistence and tests

- [x] `SqliteCharacterRepository` constructor extended (optional `IBackupRepository`/checker-list params, self-constructing defaults); `HistoryEventTypes` extended with the two new event types.
- [x] `ArchiveCharacter`/`DeleteCharacterPermanently` implemented; `IsCommandAlreadyApplied`/`ReplayCharacterDeleted` helpers.
- [x] A real duplicate-`CommandId` bug found by the first test run and fixed (section 5 above) before any further work.
- [x] 14 new tests in `CharacterArchivePhysicalDeleteTests.cs`, all passing after the fix.
- [x] `dotnet build`/`dotnet test` full suite green (205/205 persistence tests, no regression).

### M3 — Validation and review readiness

- [x] `.\scripts\verify-format.ps1`.
- [x] `docs/errors/ERROR_CODES.md` + `Tests/Metadata/test-catalog.json` entries (`TC-CHAR-115`–`128`).
- [x] This task contract/ExecPlan, created before the final validation pass.
- [x] `.\scripts\check-repository-policy.ps1` final green run.
- [ ] `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- [ ] Diff-scope check against §9's own expectations.
- [ ] Commit, push, and open Draft PR.
- [ ] Record CI status.

## 7. Progress log

- 2026-09-02 -- Preflight confirmed PR #93's merge commit is a real ancestor of `origin/main`; created branch `feat/ody-s04-110-archive-physical-delete`.
- 2026-09-02 -- Read `ADR-025` §5, `ADR-022` §7–8, product §22/§7.1/requirements 47/61, `BackupRepositoryContracts.cs`/`SqliteBackupRepository.cs` in full; re-read `ApproveCharacterDraft`. Confirmed via `Grep` that no persistence code stores `CharacterId` outside `Character` itself, and that `CharacterLifecycleTransitions` already fully covers `-> Archived`.
- 2026-09-02 -- Resolved all three delegated design questions (§1.1/§1.2/§1.3) directly from `ADR-025`'s own text and this codebase's own existing conventions -- no open question needed escalating.
- 2026-09-02 -- Implemented `ICharacterDeletionDependencyChecker`, `ArchiveCharacter`, `DeleteCharacterPermanently`; `dotnet build` passed on first attempt.
- 2026-09-02 -- Wrote 14 tests; one failed on first run (`DeleteCharacterPermanently_DuplicateCommandId_DoesNotDuplicateEffect`), diagnosed the pre-check/replay-ordering bug (section 5), fixed it, all 14 passed after the fix.
- 2026-09-02 -- Full suite green (205/205 persistence tests, no regression); added `ERROR_CODES.md`/`test-catalog.json` entries; `check-repository-policy.ps1`/`verify-format.ps1` both green.

## 8. Decisions

- 2026-09-02 -- Decision: `ICharacterDeletionDependencyChecker` is a plain single-method interface, list-injected via an optional constructor parameter defaulting to empty. Authority: this task's own explicit §1.1 instruction -- extensible, not hard-coded, and no stub instances needed since the default IS an empty list (nothing to check today, for real).
- 2026-09-02 -- Decision: `IBackupRepository` is injected into `SqliteCharacterRepository`'s constructor (Persistence layer holds it), self-constructing a real `SqliteBackupRepository` by default. Authority: `DeleteCharacterPermanently` itself must invoke the backup as one of its own transactional preconditions (ADR-025 §5.2's own "before committing" framing) -- pushing this into caller-orchestrated two-step discipline would risk a caller forgetting it; mirrors `_pipeline`'s own established self-construction convention, so zero existing callers break.
- 2026-09-02 -- Decision: `ArchiveCharacter`'s actor is MainGM-or-assigned, not MainGM-only. Authority: `ADR-025` §5.1's own explicit text and product §26's own MVP MainGM-exclusive list (which omits `Character.Archive`) -- see this task's own interface doc comment for the full citation.
- 2026-09-02 -- Decision: the backup call happens outside any open transaction on this repository's own connection (before `_pipeline.Execute` even opens one), specifically to avoid SQLite write-lock contention between the backup's own connection and the delete transaction's connection to the same database file.
- 2026-09-02 -- Decision: a non-authoritative pre-check (existence/revision/dependency) runs before the backup, purely to avoid the cost of a real campaign backup for an already-doomed request -- but is skipped entirely for an already-applied `CommandId`, so a duplicate delivery never re-runs it or re-creates a backup.

## 9. Discoveries and deviations

- A real duplicate-`CommandId` bug was found by this task's own first test run and fixed before any other work continued -- see section 5.
- No open architectural question was found that `ADR-022`/`ADR-025` do not already answer; all three of this task's own delegated design questions (§1.1/§1.2/§1.3) were resolved directly from those ADRs' own text.

## 10. Validation and acceptance evidence

- `dotnet build Odyssey.Core.sln`: 0 warnings, 0 errors.
- `dotnet test Odyssey.Core.sln`: 205/205 `Odyssey.Tests.Persistence` (14 new), zero regression across the full solution.
- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed, `Repository policy check passed.`

## 11. Recovery and rollback

Rollback is a normal revert of this branch/PR -- no schema change at all (no new column/table); `SqliteCharacterRepository`'s constructor extension is fully backward-compatible (both new parameters are optional with self-constructing defaults); `DeleteCharacterPermanently` is the only genuinely destructive operation this task introduces, and it is itself protected by a mandatory real backup before it commits.

## 12. Open questions and blockers

None.

## 13. Outcome and follow-up

Draft PR: [#94](https://github.com/odyssey-services/Odyssey_VTT/pull/94). CI pending. `ODY-S04-111` (Dead & `CharacterRestored`) is the next task in the backlog; a future Item/Inventory/Board/GameLog task would be the first real occasion to register a genuine `ICharacterDeletionDependencyChecker` implementation.
