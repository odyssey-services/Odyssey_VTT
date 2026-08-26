# ODY-S03-007 — Game Log & Board State Persistence, Reconnect Replay

**Status:** In Review
**Roadmap stage / slice:** SLICE-03 (vertical slice implementation)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s03-007-game-log-and-board-state-persistence-reconnect-replay`
**Pull request:** Draft — link recorded once opened
**ExecPlan:** `docs/plans/active/ODY-S03-007_Game_Log_And_Board_State_Persistence_Reconnect_Replay.md`
**Created:** 2026-08-26
**Last updated:** 2026-08-26 UTC

## 1. Goal

Give `DiceRoll`/`GameLogEntry` real durable persistence via `ADR-012`'s already-accepted append-only journal (the same one-transaction commit pipeline `SqliteSceneRepository` already uses for Scene/Token), and audience-aware reading of the persisted log after a campaign is reopened ("reconnect" in this task's own, campaign-persistence sense — see section 3). Covers roadmap §12.6 steps 8–9 (event persisted → reopening restores the visible journal). Closes exit criteria 1 ("Board state одинаков после restart и reconnect") and 5 ("журнал объясняет итог") from §12.7.

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-S03-005`'s `DiceRollStore` is deliberately in-memory only — its own task contract §3 explicitly deferred persistence to this task. Without it, a resolved roll and its game-log entry vanish the moment the process ends, so exit criteria 1/5 have no implementation to satisfy them.
- Value or risk reduction: `09_Dice_And_Game_Log` §35.1 requires one SQLite transaction to commit a resolved gameplay action's projection, `DomainEvent`, `DiceRoll`, and `GameLogEntry` together — fixing this now, atop the already-proven `SqliteSavingPipeline`, avoids a future task inventing an ad hoc, non-atomic persistence path for the same data.
- Blocking or enabling relationship: `SLICE-03_IMPLEMENTATION_BACKLOG.md` §5/§6 — depends on `ODY-S03-004` (board state to persist, already merged), `ODY-S03-005` (roll/log entities to persist, already merged), and `ODY-S03-006` (reconnect replay must reuse its already-established audience-aware redaction, already merged). Blocks `ODY-S03-008` (integration proof exercising every prior deliverable together).

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1.2
- `09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md` §23 (`GameLogEntry`), §35 (Persistence contract)
- `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` (full document, not reopened)
- `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` (full document, not reopened — its network-level mechanism is explicitly not reused, see below)
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSceneRepository.cs`/`SqliteSavingPipeline.cs` (`SLICE-01`, extended `ODY-S03-004`) — structural template
- `Packages/com.odyssey.application/Runtime/Dice/DiceRollStore.cs`/`DiceRollService.cs` (`ODY-S03-005`) — the store this task adds a durable counterpart to
- `Packages/com.odyssey.application/Runtime/Dice/DiceRollVisibilityPolicy.cs`, `Packages/com.odyssey.application/Runtime/Audience/AudienceContracts.cs` (`ODY-S03-006`) — reused unmodified
- `Packages/com.odyssey.application/Runtime/Networking/Reconnect/ReconnectContracts.cs` (`ODY-S02-012`) — read and explicitly not reused (§3 below)
- `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` §5 (this task's fixed boundary — not reopened)

### Requirement and test IDs

- Requirement IDs: `SLICE-03` (vertical slice implementation), backlog `ODY-S03-007`, roadmap §12.6 steps 8–9, §12.7 exit criteria 1/5.
- Existing test IDs: `TC-BOARD-*`/`TC-DICE-*`/`TC-PERSIST-001`–`031` reused unmodified in behavior; none of their production code is edited except the additive `SqliteSavingPipeline.PipelineWrite<T>` hooks (backward compatible, see §6).
- New test IDs introduced: `TC-PERSIST-032` through `TC-PERSIST-035`.

### Task-safe private context

- Approved summary / references: `09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md` §23/§35 and `ADR-012`/`ADR-017` are summarized/quoted (short customary phrases and direct quotes clearly attributed) into this task and the production code's doc comments. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `ODY-S03-004` (PR #58), `ODY-S03-005` (PR #59), `ODY-S03-006` (PR #60) are all merged into `main` — confirmed via `git log --oneline -10` after `git fetch origin main && git merge --ff-only`.
- `DiceRollStore` (`ODY-S03-005`) is in-memory only; that task's own task contract §3 text explicitly assigns durable persistence to `ODY-S03-007` — confirmed by `Read`.
- `SqliteSavingPipeline.Execute<T>` already implements `ADR-012` §5's one-transaction commit (current-state row + `DomainEvent` + `AppliedCommands`) and §7's `CommandId`-replay idempotency, generically over any `T` — confirmed by `Read` in full; prior to this task, no caller needed the assigned `EventSequence` back inside its own result, so the pipeline had no hook to expose it.
- `ADR-017` defines a **network-level** application protocol (`ProjectionSnapshot`/`ProjectionDeltaBatch` over `ISessionTransport`) with its own in-memory, per-session `SessionDeltaBuffer` (`ReconnectContracts.cs`, `ODY-S02-012`) — confirmed by `Read` of both documents in full; §3.1 of `ADR-017` itself explicitly forbids confusing this network-level `Snapshot` concept with `ADR-012`'s persistence-level one.
- No `IGameLogRepository`, `SqliteGameLogRepository`, or `GameLogReconnectService` existed anywhere in the repository prior to this task — confirmed by `Grep`.
- `Odyssey.Persistence`'s existing `Sqlite/` files (`SqliteSceneRepository.cs`, `SqliteCampaignRepository.cs`, etc.) and `Odyssey.Application`'s existing `Persistence/` files have no per-file `.meta` companions — confirmed by `Glob`/`find`; the `.meta`-per-new-namespace-folder convention from `ODY-S03-005`/`006` (`Dice/`, `Audience/`) does not extend to these two pre-existing directories, so this task's new files there follow the actual local convention (no new `.meta`s), while the one brand-new namespace folder this task adds (`GameLog/`) does get one, per the more recent precedent for a genuinely new folder.

### Assumptions

- None. All facts above were directly observed via `Read`/`Grep`/`git log` before and during this task.

## 5. Scope

### In scope

- `Packages/com.odyssey.application/Runtime/Persistence/GameLogRepositoryContracts.cs` (new) — `IGameLogRepository`, `GameLogEntryRecord`.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` — `PersistenceFailures.GameLogIoFailed` added.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — 1 new code.
- `Packages/com.odyssey.application/Runtime/GameLog/GameLogReconnectService.cs` (new) — audience-aware reconnect reading, reusing `DiceRollVisibilityPolicy`.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteGameLogRepository.cs` (new) — `DiceRolls`/`GameLogEntries` tables, `IGameLogRepository` implementation.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSavingPipeline.cs` — two new optional, backward-compatible `PipelineWrite<T>` hooks.
- New test file: `DotNet/Tests/Odyssey.Tests.Persistence/SqliteGameLogRepositoryTests.cs`.
- `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json` registry updates.
- This task contract, its ExecPlan, `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` (`ODY-S03-006` row fix — real PR #60 link/`Done`; `ODY-S03-007` row).

### Out of scope

- Any real network — this task is campaign-persistence-only; `ODY-S02-012`'s `ReconnectContracts.cs`/`SessionDeltaBuffer` is read, not reused, not duplicated, not touched.
- Full-text search, session archive/export (`SLICE-03_IMPLEMENTATION_BACKLOG.md` §2.2).
- Board features beyond `ODY-S03-004`'s already-implemented scope (drawing, ruler, etc.).
- `ActionLogGroup`, disclosure-change commands, comments, tags (product doc §24/§26).
- Persisted `DiceRoll.RngProofs` — a documented, deliberate limitation (§6).
- Any edit to `ADR-012`/`ADR-017`.

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Persistence/GameLogRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.application/Runtime/GameLog/**
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteGameLogRepository.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSavingPipeline.cs
DotNet/Tests/Odyssey.Tests.Persistence/SqliteGameLogRepositoryTests.cs
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
docs/tasks/active/ODY-S03-007_Game_Log_And_Board_State_Persistence_Reconnect_Replay.md
docs/plans/active/ODY-S03-007_Game_Log_And_Board_State_Persistence_Reconnect_Replay.md
docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: `IGameLogRepository`/`GameLogEntryRecord`/`GameLogReconnectService` live in `Odyssey.Application` (ports and pure logic, `ADR-001` §5); `SqliteGameLogRepository` lives in `Odyssey.Persistence` (depends on Application, matching `SqliteSceneRepository`'s exact placement). No new project reference — `Newtonsoft.Json` (used for the internal JSON storage blobs described below) already flows transitively into `Odyssey.Persistence` via its existing `Odyssey.Application` project reference; confirmed by a successful build, no new `PackageReference` added.
- Authoritative-state and transaction boundary: `SaveDiceRollEntry` commits the `DiceRolls` row, the `GameLogEntries` row, the `DomainEvent`, and the `AppliedCommands` idempotency record in one SQLite transaction via the shared `SqliteSavingPipeline` (`ADR-012` §5) — the same boundary `SqliteSceneRepository` already uses, not a second, parallel commit path.
- Serialization / compatibility boundary: `DiceRoll.NaturalResults`/`ModifierEntries`/`Audience.SelectedUserIds`/`SelectedGroupIds` are stored as JSON array text columns (`Newtonsoft.Json.Linq.JArray`/`JObject`, already a project dependency) rather than normalized child tables — a deliberate scope narrowing: this task's own read/write pair is the only consumer of these columns, no cross-service wire contract or trust boundary crosses them, so the validation-oriented `CanonicalJsonWriter`/`JsonObjectReader` machinery (built for command/event payloads entering from outside the process) is not a fit; a future task requiring queryable sub-structure (e.g. full-text search, explicitly out of scope here) may normalize these into the product document §35.2's recommended child tables without changing this task's own port contract.
- Time / RNG rule: `DiceRoll.RngProofs` is not persisted (§6/§9 of the ExecPlan's investigation) — non-secret diagnostic evidence, not required to explain an outcome to a player; a documented limitation.
- Unity / thread / lifetime rule: not applicable — no Unity-side code.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: `IGameLogRepository.ListGameLog` returns the full, unredacted set for every persisted entry — the same "Persistence stores everything, Application decides visibility" split `ADR-012` §4.4 already fixes for `DomainEvents`; `GameLogReconnectService.GetVisibleEntries` is the caller-side audience filter, reusing `DiceRollVisibilityPolicy` unmodified, never a second parallel visibility mechanism.
- Performance or platform constraint: not applicable at this scale (single-campaign, test-sized data volumes).
- Other: `SqliteSavingPipeline.PipelineWrite<T>`'s two new optional constructor parameters (`onEventSequenceAssigned`, `withEventSequence`) default to `null` and are unused by `SqliteSceneRepository`'s existing call sites — verified backward compatible by the full, unmodified `TC-BOARD-*` suite continuing to pass.

## 7. Expected behavior

### Scenario 1 — a resolved roll and its log entry persist across a restart

**Given** a resolved `DiceRoll` saved via `SqliteGameLogRepository.SaveDiceRollEntry`
**When** a brand-new `SqliteGameLogRepository` instance (no shared in-memory state — the same shape a process restart or reopened campaign produces) lists the campaign's game log
**Then** the same roll (`NaturalResults`, `ModifierEntries`, `BaseTotal`/`FinalTotal`, `Status`, `Audience.Kind`) and its `GameLogEntry` (`EntryType`, `SummaryPayload`, `ActorUserId`, a real `AuthoritativeSequence` ≥ 1) are restored identically.

### Scenario 2 — redelivery with the same CommandId does not duplicate

**Given** a `SaveDiceRollEntry` call that has already committed
**When** the same call is redelivered with the identical `CommandId`
**Then** the stored `GameLogEntryRecord` (same `LogEntryId`) is replayed, not re-created — `ListGameLog` still shows exactly one entry (`ADR-012` §7.2's exactly-once effect).

### Scenario 3 — a permission revoked between recording and reconnect hides the entry

**Given** a `SelectedParticipants` roll visible to a player because their `CampaignUserGroup` membership was active at read time
**When** that membership is removed from the group and the repository is reopened (simulated reconnect)
**Then** `GameLogReconnectService.GetVisibleEntries`, evaluated against the group's *current* (post-removal) state, no longer includes the entry for that player — while MainGM continues to see it unconditionally (§16.2) — with no distinguishable trace (no null placeholder, no error) that a hidden entry exists for the excluded player.

### Scenario 4 — board state is identical across a new repository instance

**Given** a scene and a token created and moved via `SqliteSceneRepository`
**When** a brand-new `SqliteSceneRepository` instance (simulated restart) lists the scene's tokens
**Then** the token's position, revision, and controller are identical to what was last persisted.

### Required invariants

- `DiceRolls`/`GameLogEntries` rows are never `UPDATE`d or `DELETE`d by this task's production code outside the pipeline's own idempotency-replay path (`ADR-012` §4.2's append-only guarantee).
- `GameLogEntries.AuthoritativeSequence` always reflects `ADR-012`'s real `EventSequence`, never a placeholder value, once a transaction commits.
- `GameLogReconnectService` never mutates its inputs and never returns a distinguishable signal (null entry, error) for a non-entitled participant — omission only.
- `ADR-012`/`ADR-017` files are unmodified.
- No new `Odyssey.Networking` reference is introduced anywhere in this task's diff.

## 8. Deliverables

- Production code: `GameLogRepositoryContracts.cs`, `GameLogReconnectService.cs`, `SqliteGameLogRepository.cs`, extended `CampaignRepositoryContracts.cs`/`ErrorCodes.cs`/`SqliteSavingPipeline.cs`.
- Tests: `SqliteGameLogRepositoryTests.cs` (4 test methods, `TC-PERSIST-032`–`035`).
- Scripts / CI: None.
- Configuration: None.
- Documentation: this task contract, its ExecPlan, `ERROR_CODES.md`, `test-catalog.json`, `SLICE-03_IMPLEMENTATION_BACKLOG.md` (`ODY-S03-006`/`007` rows).
- Generated evidence or build artifacts: None.
- Migration / recovery material: not applicable — new tables created via `CREATE TABLE IF NOT EXISTS`, no existing schema altered.

## 9. Acceptance criteria

1. `SqliteGameLogRepository.SaveDiceRollEntry` commits `DiceRolls`+`GameLogEntries`+`DomainEvent`+`AppliedCommands` in one transaction, and a fresh repository instance against the same `campaign.db` restores an identical roll and log entry with a real `AuthoritativeSequence` (`TC-PERSIST-032`).
2. Redelivery with the same `CommandId` replays the stored result without duplicating the row (`TC-PERSIST-033`).
3. A `SelectedParticipants` roll's persisted entry is hidden from a participant whose group membership was revoked before reconnect, while MainGM still sees it unconditionally, with safe denial (`TC-PERSIST-034`).
4. Board (token) state is identical across a brand-new `SqliteSceneRepository` instance (`TC-PERSIST-035`).
5. `docs/errors/ERROR_CODES.md` and `Tests/Metadata/test-catalog.json` are updated for the new error code and all 4 new test cases.
6. `ADR-012`/`ADR-017` files are unmodified by this task's diff.
7. `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `dotnet build`, `dotnet test` all pass.
8. `git diff --name-status` against `main` shows only files listed in §5's Allowed paths.
9. `SLICE-03_IMPLEMENTATION_BACKLOG.md`'s `ODY-S03-006` row is corrected to `Done` with the real merged PR #60 link.
10. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-PERSIST-032` | `.NET` / `dotnet test` | Roll + log entry restored identically after a simulated restart, real `AuthoritativeSequence` assigned | Pass |
| `TC-PERSIST-033` | `.NET` / `dotnet test` | Redelivered `CommandId` does not duplicate the row | Pass |
| `TC-PERSIST-034` | `.NET` / `dotnet test` | Revoked group membership hides the entry at reconnect; MainGM always sees; safe denial | Pass |
| `TC-PERSIST-035` | `.NET` / `dotnet test` | Board (token) state identical across a new `SqliteSceneRepository` instance | Pass |

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

```bash
dotnet build DotNet/Odyssey.Core.sln
dotnet test DotNet/Odyssey.Core.sln
```

### Manual validation

- Read `SqliteGameLogRepository.cs` end-to-end to confirm every write routes through `SqliteSavingPipeline.Execute`, and that `AuthoritativeSequence` is never left at its pre-commit placeholder value in a successfully committed row.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: not applicable — no Unity-side code.
- Network topology or database fixture: real SQLite via `Microsoft.Data.Sqlite`, temp-directory campaigns per test (mirroring `SqliteSceneRepositoryTests`'s own fixture pattern) — not a fake/in-memory substitute.
- Other: `dotnet` SDK matching `global.json` (`10.0.302`).

### Validation not required by this task

- Unity Editor compile/EditMode/PlayMode — no Unity-side code.
- Any real network/transport test — out of scope (§5).
- Any test of `RngProofs` persistence — not implemented by this task (§6).

## 11. Compatibility, migration, and rollback

- Compatibility impact: `SqliteSavingPipeline.PipelineWrite<T>`'s two new constructor parameters are optional and default to `null` — no existing caller signature changes; `SqliteSceneRepository`'s full existing test suite (`TC-BOARD-*`) continues to pass unmodified, confirming backward compatibility.
- Version fields affected: None — no `DatabaseSchemaVersion` bump; new tables are additive (`CREATE TABLE IF NOT EXISTS`).
- Migration or upcaster: not applicable — no existing table altered.
- Forward / backward behavior: not applicable.
- Rollback method: revert this task's commits.
- Data-loss risk and protection: None — additive schema only.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

`Newtonsoft.Json` is already an approved, existing dependency (used elsewhere in `Odyssey.Application`); this task wires it into `Odyssey.Persistence` via the already-existing transitive project reference, introducing no new package.

## 13. Security, privacy, and hidden information

- Data classes handled: dice-roll results, formula text, game-log summaries, audience membership references — no secret, credential, or personal data.
- Trust boundaries: `SqliteGameLogRepository` is a pure storage boundary (stores everything, redacts nothing); `GameLogReconnectService` is the Application-layer trust boundary deciding what a specific reader may see of the persisted log, mirroring `ADR-019` §6.2's existing read/visibility-check point.
- Authorization / audience checks: entirely delegated to `DiceRollVisibilityPolicy` (`ODY-S03-006`, unmodified) — this task introduces no new visibility rule, only a new place (post-restart read) where the existing rule is re-applied.
- Redaction requirements: all-or-nothing per the roll's own `Audience` (§16.5's baseline, unchanged from `ODY-S03-006`).
- Log-safe fields: the one new `Error` (`persistence.gamelog.io_failed`) uses only the existing `SafeReasonCode`/`UserMessageKey` vocabulary, mirroring `persistence.scene.io_failed`'s exact convention.
- Abuse / malformed input limits: not applicable — internal storage of already-validated in-process domain objects, no untrusted input path.
- Security tests: `TC-PERSIST-034` confirms denial is total (entry entirely absent from the reconnect view, not merely flagged) and that MainGM's own unconditional visibility is unaffected by the same revocation.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: evaluated fresh against `PLANS.md` §1.2. This task introduces a new Application port (`IGameLogRepository`) and a new Persistence implementation, makes a small but real backward-compatible change to an already-shared low-level primitive (`SqliteSavingPipeline`), and required real investigation before the implementation path was known — specifically, resolving whether `ODY-S02-012`'s network reconnect mechanism applied here at all (resolved: no, different mechanism, same underlying principle), and how to expose `ADR-012`'s `EventSequence` to a caller's own result without inventing a parallel, non-authoritative counter. This matches §1.2's "introduces or changes an Application port," "affects an already-shared component," and "requires investigation before the implementation path is known" triggers.
- ExecPlan path: `docs/plans/active/ODY-S03-007_Game_Log_And_Board_State_Persistence_Reconnect_Replay.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: depends on `ODY-S03-004`/`005`/`006` (all confirmed merged). Blocks `ODY-S03-008` (integration proof).

## 15. Documentation and versioning impact

- Documents that must change: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` (`ODY-S03-006`/`007` rows).
- Documents that must not change: `docs/adr/ADR-012`/`ADR-017`, `docs/tasks/active/ODY-S03-000`–`006_*` (read only), `docs/tasks/completed/*`, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything under `Documentation/`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: adds two new SQLite tables (`DiceRolls`, `GameLogEntries`) additively — no `DatabaseSchemaVersion` bump per `ADR-011`'s dimension (no existing table altered, no migration required).
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed.
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

- `Packages/com.odyssey.application/Runtime/Persistence/GameLogRepositoryContracts.cs` — new.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` — extended (`GameLogIoFailed`).
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — extended (1 new code).
- `Packages/com.odyssey.application/Runtime/GameLog/GameLogReconnectService.cs` — new.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteGameLogRepository.cs` — new.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSavingPipeline.cs` — extended (2 new optional hooks).
- `DotNet/Tests/Odyssey.Tests.Persistence/SqliteGameLogRepositoryTests.cs` — new (4 tests).
- `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json` — 1 new registry row, 4 new test-catalog entries.
- This task contract, its ExecPlan, `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` (`ODY-S03-006`/`007` rows).

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet/Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test DotNet/Odyssey.Core.sln` | Passed | All test projects passed: Contracts 1/1, Domain 27/27, Networking 67/67, Unit 105/105, Architecture 2/2, Persistence 59/59 (55 pre-existing + 4 new `TC-PERSIST-032`–`035`). |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS`. |
| `.\scripts\check-repository-policy.ps1` | Passed | All checks pass, including `REPO-POLICY-005` (1 new error code) and all `TC-CI-*` workflow checks. |
| CI — Draft PR | Pending | To be recorded once the PR is opened and CI completes. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `TC-PERSIST-032`. |
| AC-2 | Passed | `TC-PERSIST-033`. |
| AC-3 | Passed | `TC-PERSIST-034`. |
| AC-4 | Passed | `TC-PERSIST-035`. |
| AC-5 | Passed | `ERROR_CODES.md` (1 row), `test-catalog.json` (4 entries) both updated. |
| AC-6 | Passed | `git status --porcelain` before commit confirmed no `ADR-012`/`ADR-017` file touched. |
| AC-7 | Passed | See Validation results table above. |
| AC-8 | Pending | To be confirmed via `git diff --name-status` before commit. |
| AC-9 | Pending | To be confirmed once the backlog edit is made. |
| AC-10 | Pending | Draft PR not yet opened. |

## 18. Blockers, decisions, and change control

### Blockers

- None for this task's own closure.

### Decisions made during execution

- 2026-08-26 — This task's "reconnect" means reopening a persisted campaign after a process restart, not `ODY-S02-012`'s networked reconnect protocol — Authority: `ADR-017` §3.1's own explicit terminology split; no real network exists in this revision; `SessionDeltaBuffer` is in-memory/per-session and cannot itself survive a restart.
- 2026-08-26 — `DiceRoll`/`GameLogEntry` persistence via a new `IGameLogRepository`/`SqliteGameLogRepository` pair reusing the shared `SqliteSavingPipeline`, mirroring `ISceneRepository`/`SqliteSceneRepository`'s exact split — Authority: `ADR-012` §5's single-transaction commit boundary, already correctly implemented once; duplicating it for a second aggregate pair would risk drift.
- 2026-08-26 — `GameLogEntryRecord` carries the full re-hydrated `DiceRoll` rather than its own independent `VisibilityAudience` field — Authority: avoids two independently-drifting audience sources for the same underlying roll; `DiceRollVisibilityPolicy` (`ODY-S03-006`) is reused completely unmodified.
- 2026-08-26 — Extend `SqliteSavingPipeline.PipelineWrite<T>` with two optional, backward-compatible hooks rather than duplicating the pipeline — Authority: `GameLogEntries.AuthoritativeSequence` must be `ADR-012`'s real `EventSequence`; the existing pipeline had no way to hand that value back to a caller's own result before this task, a genuine, narrowly-scoped gap, not an architectural reopening.
- 2026-08-26 — `DiceRoll.RngProofs` is not persisted — Authority: non-secret diagnostic evidence per `RngContracts.cs`'s own doc comment, not required by exit criterion 5; documented as a known limitation.
- 2026-08-26 — New files under `Odyssey.Persistence/Sqlite/` and `Odyssey.Application/Persistence/` get no `.meta` companion (matching the actual, already-established local convention in those two directories); the one genuinely new namespace folder this task adds (`GameLog/`) does get one, matching `ODY-S03-005`/`006`'s more recent precedent for new folders.

### Approved task changes

- None.
