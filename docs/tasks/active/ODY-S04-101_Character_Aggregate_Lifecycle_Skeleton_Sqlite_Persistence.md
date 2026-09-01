# ODY-S04-101 — Character Aggregate, Lifecycle Skeleton & SQLite Persistence

**Status:** In Review
**Roadmap stage / slice:** SLICE-04
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s04-101-character-aggregate-skeleton`
**Pull request:** <to be filled after `gh pr create`>
**ExecPlan:** `docs/plans/active/ODY-S04-101_Character_Aggregate_Lifecycle_Skeleton_Sqlite_Persistence.md`
**Created:** 2026-09-01
**Last updated:** 2026-09-01 UTC

## 1. Goal

Implement the `Character` aggregate skeleton — identity/presentation/custom-field sections, six-state `LifecycleStatus`/`ApprovalState` structural values, `CharacterRevision`/all twelve `ADR-022` section revisions with narrow section locks, a SQLite projection, and a minimal `CharacterHistoryProjection` rebuildable from `DomainEvents` — with zero business logic for ownership, drafts, purchases, or lifecycle-boundary operations.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-04_IMPLEMENTATION_BACKLOG.md` §5–7 names `ODY-S04-101` as the first implementation task and the sole dependency of all 14 remaining backlog tasks — none of them can start without a real `Character` aggregate, section-revision mechanism, and persistence layer to build on.
- Value or risk reduction: proves `ADR-022`'s aggregate/section-revision/lock/history-projection model compiles and works against a real SQLite database before any later task builds business logic on top of an unverified foundation; establishes the exact structural convention (mirroring `SqliteSceneRepository`) every later `ODY-S04-1XX` persistence task will also follow.
- Blocking or enabling relationship: this is the first task in `SLICE-04` with any code at all (all five prior `ODY-S04-00X` tasks were ADR/docs-only). It directly unblocks `ODY-S04-102` (Ownership), `ODY-S04-103` (Drafts), `ODY-S04-105`/`108`/`109`/`110`/`111` (all depend on it directly), and transitively every other backlog task.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`.
- `AGENTS.md`, `PLANS.md` §1.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` §5 (row 1), §6 ("ODY-S04-101"), §7 (dependency rules) — the binding scope definition for this task.
- `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md` (full read — the aggregate boundary, section revisions/locks, and history-projection contract this task implements).
- `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md`, `docs/adr/ADR-003_Serialization_Strategy_v1.1.md`, `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` (substrate reused unmodified — command/event/idempotency model, versioned DTO/JSON codec rules, append-only journal/transaction boundary).
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §6 (Character aggregate/`CharacterKind`), §7.1–7.3 (lifecycle/`ApprovalState`/Draft-Active, structural transition table only), §10 (custom fields), §20.1–20.3 (narrow locks/local forms/system-change-during-form), §21 (Character history), §29 (persistence contract).
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSceneRepository.cs`, `Packages/com.odyssey.application/Runtime/Persistence/SceneRepositoryContracts.cs`, `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSavingPipeline.cs`, `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteGameLogRepository.cs` — read in full as the binding structural precedent per this task's own explicit instruction (§2 of the ТЗ).

### Requirement and test IDs

- Requirement IDs: `ODY-S04-101`, `ADR-022` §4–8.
- Existing test IDs: None reused (new aggregate, new tests).
- New test IDs introduced: `TC-CHAR-001` through `TC-CHAR-008` (`Tests/Metadata/test-catalog.json`).

### Task-safe private context

- Approved summary / references: non-tracked product documentation was read as local authority and summarized by section/topic only. No private prose, secret, personal data, or hidden campaign content is copied into this task, the plan, or production code.

## 4. Verified current state

### Verified facts

- `git fetch origin main`, `git checkout main`, and `git merge --ff-only origin/main` advanced local `main` to `8f178f8`, the merge commit for PR #84 (`ODY-S04-005`, implementation backlog scaffold).
- `gh pr view 84 --json state,mergedAt,mergeCommit` confirmed `state: MERGED`, and `git merge-base --is-ancestor 8f178f8... origin/main` independently confirmed the merge commit is a real ancestor of `origin/main`.
- `Odyssey.Domain.Identity.CharacterId` already existed (added incidentally for `DomainEvents.cs`'s `DomainActor.ActorCharacterId` in an earlier task) but lacked `NewId(UtcInstant)`/`==`/`!=`, which every other aggregate-root ID type (`SceneId`/`TokenId`/`AssetId`/`BackupId`) already has — confirmed by `Read`.
- `SqliteSceneRepository.cs`/`ISceneRepository`(`SceneRepositoryContracts.cs`)/`SqliteSavingPipeline.cs`/`SqliteGameLogRepository.cs`(`GameLogRepositoryContracts.cs`) were read in full: per-call short-lived `SqliteConnection` under the `ADR-011` §7.1 PRAGMA profile, `EnsureXTables` with `CREATE TABLE IF NOT EXISTS`, `SqliteSavingPipeline.Execute` committing the projection row + `DomainEvent` + `AppliedCommands` receipt in one transaction, a shared `SelectColumns`/`ReadXRecord` convention for consistent column ordering, and `Newtonsoft.Json.Linq` (`JObject`/`JArray`) already used directly in production Persistence code (`SqliteGameLogRepository.cs`) as `ADR-003`'s approved low-level JSON API — not merely declared permissible in the abstract.
- The shared `DomainEvents` table (created by `SqliteCampaignRepository.cs`) has columns `EventSequence, CampaignId, EventType, CommandId, PayloadJson, PayloadHash, CreatedAtHost` — no `AggregateId`/`AggregateType` column. Every existing repository (Scene/Token/GameLog) selects its own events by `EventType` prefix and payload content, never by an indexed per-aggregate column — confirmed by `Read`; `GetCharacterHistory` follows the identical convention.
- `docs/errors/ERROR_CODES.md` and `Tests/Metadata/test-catalog.json` form a real, cross-checked registry enforced by `check-repository-policy.ps1`'s `REPO-POLICY-005` — a new `ErrorCode` requires both a registry row and a real `TC-*` catalog entry its `TestReference` column names; discovered only when the first `check-repository-policy.ps1` run after adding the three new Character `ErrorCode`s failed with "Production ErrorCode is missing from docs/errors/ERROR_CODES.md."
- `SLICE-04_IMPLEMENTATION_BACKLOG.md`'s ordered-task table (authored by `ODY-S04-005`) had no `Status` column, unlike `SLICE-03_IMPLEMENTATION_BACKLOG.md`'s own table — confirmed by `Read`.

### Assumptions

- None. All facts above were directly observed via `Read`/`Grep`/`gh`/`git` during this task.

## 5. Scope

### In scope

- `Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs` — add `CharacterId.NewId`/`==`/`!=` (additive, matching sibling ID types; no existing member changed).
- `Packages/com.odyssey.domain/Runtime/Character/CharacterLifecycle.cs` (new) — `CharacterKind`, `CharacterLifecycleStatus`, `CharacterApprovalState`, `CharacterLifecycleTransitions.IsValidTransition`, `CharacterSectionRevisions`.
- `Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs` (new) — `ICharacterRepository`, `CharacterRecord`, `CreateCharacterRequest`, `CharacterHistoryEntry`.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` (edit) — add `PersistenceFailures.CharacterNotFound`/`CharacterIoFailed`/`CharacterRevisionConflict`.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` (edit) — add the three corresponding `ErrorCode` entries.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` (new) — `ICharacterRepository` implementation.
- `DotNet/Tests/Odyssey.Tests.Domain/Character/CharacterLifecycleTransitionsTests.cs` (new).
- `DotNet/Tests/Odyssey.Tests.Persistence/SqliteCharacterRepositoryTests.cs` (new).
- `docs/errors/ERROR_CODES.md` (edit) — three new registry rows.
- `Tests/Metadata/test-catalog.json` (edit) — `TC-CHAR-001`–`008`.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (edit) — add a `Status` column, mark row 1 `Done` with the real PR link.
- This task contract and its ExecPlan.

### Out of scope

- Drafts/templates/submit/review/approve (`ODY-S04-103`/`104`).
- Ownership/control commands (`ODY-S04-102`).
- Development economy/purchases (`ODY-S04-105`–`107`).
- Ability/resource/anatomy (`ODY-S04-108`/`109`).
- Archive/physical delete, Dead/restore, `.odchar`, Ruleset migration (`ODY-S04-110`–`113`).
- Any Unity/UI code — this task is purely Domain/Application/Persistence.
- Any change to `ADR-022`–`025` content, or to `SLICE-04_BACKLOG.md` (the closed prerequisite backlog).

### Allowed paths

```text
Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs
Packages/com.odyssey.domain/Runtime/Character/**
Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
DotNet/Tests/Odyssey.Tests.Domain/Character/**
DotNet/Tests/Odyssey.Tests.Persistence/SqliteCharacterRepositoryTests.cs
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S04-101_Character_Aggregate_Lifecycle_Skeleton_Sqlite_Persistence.md
docs/plans/active/ODY-S04-101_Character_Aggregate_Lifecycle_Skeleton_Sqlite_Persistence.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/ADR-001* through docs/adr/ADR-025*
docs/tasks/SLICE-04_BACKLOG.md
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: `Odyssey.Domain` owns the pure enums/transition table/section-revision value type (no serializer, no Unity, no SQLite reference); `Odyssey.Application` owns the repository port/records; `Odyssey.Persistence` owns the SQLite implementation. Matches `ADR-001` exactly, mirroring Scene/Token's own module split.
- Authoritative-state and transaction boundary: every mutating method commits through the existing, unmodified `SqliteSavingPipeline` (`ADR-012` §5 one-transaction journal/projection/`AppliedCommands` commit) — no parallel transaction mechanism.
- Serialization / compatibility boundary: event payloads use `Newtonsoft.Json.Linq` (`JObject`), the same low-level, explicit-field API `SqliteGameLogRepository.cs` already uses in production — no automatic `JsonConvert.Serialize/DeserializeObject` reflection-based mapping (`ADR-003`).
- Time / RNG rule: `IWallClock` injected exactly as `SqliteSceneRepository`/`SqliteGameLogRepository` already do; no direct wall-clock access.
- Unity / thread / lifetime rule: Not applicable — no Unity code in this task.
- Dependency / licensing rule: no new dependency; `Newtonsoft.Json` is already an approved, in-use dependency (`ADR-003`).
- Security / privacy / redaction rule: Not applicable at this skeleton stage — no hidden-field/audience model exists yet for Character (later tasks' concern).
- Performance or platform constraint: `GetCharacterHistory`'s full-table scan over `DomainEvents` filtered by event-type and payload content is the same access pattern every other repository in this codebase already uses for its own aggregate; indexing/scaling is explicitly not required by `ADR-022` at this stage.
- Other: narrow section locks (`ADR-022` §6) are satisfied for this task's scope by transaction-scoped optimistic revision checks only (`ADR-022` §6.4's explicit allowance) — no durable `SectionLocks` table, since no durable pending workflow exists in this task's scope.

## 7. Expected behavior

### Scenario 1 — Character creation for every kind

**Given** a valid `CampaignHandle` and a `CharacterKind` (`PlayerCharacter`/`NonPlayerCharacter`/`Creature`)
**When** `CreateCharacter` is called
**Then** a `Draft`/`Draft`-approval-state Character is created at `CharacterRevision = 1` with all twelve section revisions initialized to 1.

### Scenario 2 — unrelated sections do not false-conflict

**Given** an existing Character
**When** `UpdateIdentity` and `UpdatePresentation` are both called, each declaring only its own section's current (unstale) expected revision
**Then** both commit successfully and `CharacterRevision` reflects both committed changes.

### Scenario 3 — a stale same-section revision is rejected

**Given** an existing Character whose `IdentityRevision` has already advanced past a caller's held value
**When** `UpdateIdentity` is called with that now-stale expected revision
**Then** the command is rejected with `CharacterRevisionConflict` and no state change occurs.

### Scenario 4 — history rebuilds from `DomainEvents`, not an incrementally-maintained table

**Given** a Character created and then renamed twice
**When** `GetCharacterHistory` is called from a fresh repository instance with no in-memory state of its own
**Then** it returns three correctly-ordered entries, each showing the display name as of that historical event, not the Character's current name.

### Required invariants

- No update ever checks a section revision the caller did not declare.
- `DomainEvents` rows are never updated or deleted by any operation in this task.
- A duplicate `CommandId` never creates a second Character or a second history entry.
- No `ADR-022`–`025` file content changes.

## 8. Deliverables

- Production code: `CharacterLifecycle.cs` (Domain), `CharacterRepositoryContracts.cs` (Application), `SqliteCharacterRepository.cs` (Persistence), `PersistenceFailures`/`ErrorCodes` additions.
- Tests: `CharacterLifecycleTransitionsTests.cs` (29 cases), `SqliteCharacterRepositoryTests.cs` (10 tests) — 8 registered as `TC-CHAR-001`–`008`.
- Scripts / CI: None new.
- Configuration: None.
- Documentation: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- Generated evidence or build artifacts: None committed (test/build output only).
- Migration / recovery material: None — additive `CREATE TABLE IF NOT EXISTS` only, no existing schema altered.

## 9. Acceptance criteria

1. `Character` aggregate compiles with `CharacterKind`/`LifecycleStatus`/`ApprovalState`/identity/presentation/custom-field sections and all twelve `ADR-022` §5 section revisions.
2. `CharacterLifecycleTransitions.IsValidTransition` returns true for exactly product §7.1's table edges and false for every other pair, including same-status no-ops.
3. `UpdateIdentity`/`UpdatePresentation` each check only their own section's expected revision; a concurrent edit to the other section never blocks either.
4. A stale expected section revision is rejected with `PersistenceCharacterRevisionConflict` and produces no state change.
5. `GetCharacterHistory` rebuilds correctly-ordered entries purely from `DomainEvents`, from a fresh repository instance, and never mixes two different Characters' events.
6. Created/edited Character state and its history survive a real close/reopen cycle against the same on-disk campaign.
7. A duplicate `CreateCharacter`/`UpdateIdentity` command (same `CommandId`) replays the stored result and does not reapply the effect or append a second event.
8. No change to `ADR-022`–`025` or `SLICE-04_BACKLOG.md`; no Unity/UI code.
9. `dotnet build`, `dotnet test` (full suite, no regression), `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1` all pass.
10. `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` row 1 marked `Done` with a real PR link.
11. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-CHAR-001` | .NET (`Odyssey.Tests.Persistence`) | `CreateCharacter` for every `CharacterKind` returns Draft at revision 1 with all twelve section revisions initialized | Pass |
| `TC-CHAR-002` | .NET (`Odyssey.Tests.Persistence`) | `GetCharacter` on a non-existent `CharacterId` returns typed `CharacterNotFound` | Pass |
| `TC-CHAR-003` | .NET (`Odyssey.Tests.Persistence`) | A stale `expectedIdentityRevision` is rejected with `CharacterRevisionConflict`, no state change | Pass |
| `TC-CHAR-004` | .NET (`Odyssey.Tests.Persistence`) | Identity and Presentation edits commit concurrently with no false conflict | Pass |
| `TC-CHAR-005` | .NET (`Odyssey.Tests.Persistence`) | `GetCharacterHistory` rebuilds correctly from `DomainEvents` from scratch | Pass |
| `TC-CHAR-006` | .NET (`Odyssey.Tests.Persistence`) | Character state and history survive close/reopen | Pass |
| `TC-CHAR-007` | .NET (`Odyssey.Tests.Persistence`) | Duplicate `CreateCharacter` `CommandId` replays, does not duplicate | Pass |
| `TC-CHAR-008` | .NET (`Odyssey.Tests.Domain`) | `CharacterLifecycleTransitions.IsValidTransition` matches product §7.1's table exactly | Pass |

### Required commands

```bash
cd DotNet
dotnet build Odyssey.Core.sln
dotnet test Odyssey.Core.sln
```

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- None beyond the automated tests above.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable — no Unity/UI code in this task.
- Scripting backend: Not applicable.
- Network topology or database fixture: real temp-directory SQLite campaign per test (`SqliteCampaignRepository.Create`), matching `SqliteSceneRepositoryTests`'s exact fixture convention.
- Other: `dotnet` SDK matching `global.json`.

### Validation not required by this task

- `scripts/test-unity.ps1` — this task adds no Unity-facing behavior; the new Domain/Application/Persistence files compile in the pure .NET path exactly like Scene/Token/GameLog's own equivalents, and this task's own ТЗ scoped validation to `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1` only.

## 11. Compatibility, migration, and rollback

- Compatibility impact: additive only — a new `Character` table (`CREATE TABLE IF NOT EXISTS`) alongside already-existing tables; no existing table or column altered.
- Version fields affected: None.
- Migration or upcaster: None — new table creation is idempotent and does not require `ADR-013`'s schema migration runner (no existing schema version changes).
- Forward / backward behavior: Not applicable — no existing persisted Character data exists to migrate.
- Rollback method: revert this task's commits; the new table is simply unused by any other code path if reverted.
- Data-loss risk and protection: None — no existing data touched.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

`Newtonsoft.Json` is already an approved, in-use dependency (`ADR-003`); no new package reference is added.

## 13. Security, privacy, and hidden information

- Data classes handled: Character identity/presentation display data only — no hidden GM fields, no secrets, no personal data at this skeleton stage.
- Trust boundaries: Not applicable — no cross-user visibility model exists yet for Character (a later task's concern).
- Authorization / audience checks: Not applicable — this task exposes no command surface a Player/GM role distinction would gate; that begins with `ODY-S04-102`/`104`.
- Redaction requirements: Not applicable.
- Log-safe fields: `PersistenceFailures.CharacterIoFailed`/`CharacterNotFound`/`CharacterRevisionConflict` never expose raw SQLite/IO exception text or local paths, matching the existing Scene/Token/GameLog error convention exactly.
- Abuse / malformed input limits: `DisplayName` length/whitespace validated (128 chars, matching `SceneRecord.Name`'s own limit).
- Security tests: Not applicable at this stage.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 1 already names `ExecPlan` for this task, and `PLANS.md` §1 independently confirms it — this task introduces a new public Application-layer contract (`ICharacterRepository`), new persisted schema, and the first real implementation of `ADR-022`'s authoritative lifecycle/section-revision semantics.
- ExecPlan path: `docs/plans/active/ODY-S04-101_Character_Aggregate_Lifecycle_Skeleton_Sqlite_Persistence.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: no dependency (first implementation task). Blocks `ODY-S04-102`, `103`, `105`, `108`, `109`, `110`, `111` directly, and transitively every other backlog task.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (status only).
- Documents that must not change: `ADR-001` through `ADR-025`, `docs/tasks/SLICE-04_BACKLOG.md`, `Documentation/**`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: a new `Character` table is added; no existing `DatabaseSchemaVersion`/`CampaignFormatVersion` dimension changes (additive `CREATE TABLE IF NOT EXISTS`, not a versioned schema migration).
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed (none required).
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [x] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs` — `CharacterId.NewId`/`==`/`!=` added.
- `Packages/com.odyssey.domain/Runtime/Character/CharacterLifecycle.cs` — new.
- `Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs` — new.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` — `PersistenceFailures.CharacterNotFound`/`CharacterIoFailed`/`CharacterRevisionConflict` added.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — three `ErrorCode` entries added.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` — new.
- `DotNet/Tests/Odyssey.Tests.Domain/Character/CharacterLifecycleTransitionsTests.cs` — new.
- `DotNet/Tests/Odyssey.Tests.Persistence/SqliteCharacterRepositoryTests.cs` — new.
- `docs/errors/ERROR_CODES.md` — three new rows.
- `Tests/Metadata/test-catalog.json` — `TC-CHAR-001`–`008` added.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` — `Status` column added, row 1 marked `Done`.
- This task contract and its ExecPlan.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test Odyssey.Core.sln` | Passed | Full suite: Contracts 1, Domain 56 (27 pre-existing + 29 new `CharacterLifecycleTransitionsTests`), Networking 67, Unit 105, Architecture 2, Persistence 70 (60 pre-existing + 10 new `SqliteCharacterRepositoryTests`) — 301 total, 0 failures, 0 regressions. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | First run failed on missing `ERROR_CODES.md`/test-catalog entries for the three new `ErrorCode`s; fixed by adding both; second run: `Repository policy check passed`, including `REPO-POLICY-005 PASS ErrorCode registry is complete and machine-checkable`. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `CharacterLifecycle.cs`/`CharacterRepositoryContracts.cs` compile; `CharacterRecord` carries all twelve section revisions (`TC-CHAR-001`). |
| AC-2 | Passed | `TC-CHAR-008`, 29 test cases covering every table edge and every non-table pair. |
| AC-3 | Passed | `TC-CHAR-004`. |
| AC-4 | Passed | `TC-CHAR-003`. |
| AC-5 | Passed | `TC-CHAR-005`, plus a dedicated "history never crosses Characters" test. |
| AC-6 | Passed | `TC-CHAR-006`. |
| AC-7 | Passed | `TC-CHAR-007`. |
| AC-8 | Passed | `git status --porcelain` confirms no `ADR-*`/`SLICE-04_BACKLOG.md`/`Assets/**` file touched. |
| AC-9 | Passed | See Validation results above. |
| AC-10 | Passed | `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 1 status/PR link updated. |
| AC-11 | Passed | Draft PR link and CI status recorded once opened (see final report). |

### Build and artifact evidence

- Build identity: Not applicable (no build-identity-tagged artifact produced by this task).
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: `dotnet test` console output (see Validation results).

### Known limitations

- `GetCharacterHistory`'s scan-and-filter-by-payload approach over the shared `DomainEvents` table has no dedicated index for Character lookups — matches every other existing repository's own current approach (Scene/Token/GameLog); performance/indexing was explicitly not required at this stage by `ADR-022`.
- Narrow section locks are satisfied only via transaction-scoped optimistic revision checks in this task — a durable `SectionLocks` table is deferred to whichever future task first needs a durable pending workflow (`ODY-S04-106`).

### Follow-up tasks

- `ODY-S04-102` — Character Ownership — Primary Owner Assignment, Co-Owners & Control Grants.
- `ODY-S04-103` — Local Draft, Templates & Independent Copy.

### Self-review summary

- Scope review: limited to allowed files; no `ADR-022`–`025` or `SLICE-04_BACKLOG.md` change; no Unity/UI code.
- Architecture review: follows `SqliteSceneRepository`/`ISceneRepository`'s exact structural convention; reuses `ADR-002`/`ADR-012`'s command/event/transaction model unmodified; introduces no new consistency primitive beyond what `ADR-022` already specifies.
- Test review: every acceptance criterion has a real, non-stubbed test against a genuine temp-directory SQLite campaign — no mocked repository, no bypassed transaction pipeline.
- Security/privacy review: no hidden data, no new permission surface; error messages redact raw exception/path detail exactly like existing Scene/Token/GameLog failures.
- Documentation/version review: task contract, ExecPlan, error registry, test catalog, and backlog status all updated; no ADR or app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task.
- `ODY-S04-102`–`115` remain to be picked up one at a time per the backlog.

### Decisions made during execution

- 2026-09-01 — Decision: follow `SqliteSceneRepository`/`ISceneRepository`'s exact structural convention rather than inventing a new persistence pattern — Authority/approval: this task's own explicit ТЗ §2 instruction; `SqliteSceneRepository.cs`/`SqliteSavingPipeline.cs`/`SqliteGameLogRepository.cs` read in full first.
- 2026-09-01 — Decision: create all twelve `ADR-022` §5 section revisions as real columns from creation, even though this task's own commands touch only two of them — Authority/approval: `ADR-022` §5's own fixed list; avoids a future schema migration merely to start using an already-reserved section.
- 2026-09-01 — Decision: narrow section locks satisfied via transaction-scoped optimistic revision checks only, no durable lock table — Authority/approval: `ADR-022` §6.4's explicit allowance for ordinary synchronous commands; no durable pending workflow exists in this task's own scope.
- 2026-09-01 — Decision: `GetCharacterHistory` reads only the shared `DomainEvents` table, filtered by the event payload's own `characterId` field — Authority/approval: `ADR-022` §8's "rebuildable from events" requirement, and the shared table's own actual schema (no `AggregateId` column exists), verified directly rather than assumed.
- 2026-09-01 — Decision (discovered mid-task, not anticipated by the ТЗ): register the three new `ErrorCode`s in `docs/errors/ERROR_CODES.md` and add real `TC-CHAR-001`–`008` entries to `Tests/Metadata/test-catalog.json`, after `check-repository-policy.ps1`'s `REPO-POLICY-005` check failed on the first run — Authority/approval: the repository's own enforced policy, not a discretionary choice; fixed by following the exact existing registry format, not by relaxing or bypassing the check.
- 2026-09-01 — Decision: add a `Status` column to `SLICE-04_IMPLEMENTATION_BACKLOG.md`'s ordered-task table (it had none, unlike `SLICE-03_IMPLEMENTATION_BACKLOG.md`'s own table) rather than recording status in free-text prose — Authority/approval: this task's own explicit instruction to update the row's status to `Done` with a real PR link; a proper column keeps the convention consistent for all 14 remaining rows.

### Approved task changes

- None.
