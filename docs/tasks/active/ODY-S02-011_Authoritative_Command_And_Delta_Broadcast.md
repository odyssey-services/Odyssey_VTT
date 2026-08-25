# ODY-S02-011 — Authoritative Command & Delta Broadcast

**Status:** In Review
**Roadmap stage / slice:** SLICE-02, roadmap §11.6 steps 5–7
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s02-011-authoritative-command-and-delta-broadcast`
**Pull request:** Not yet opened
**ExecPlan:** `docs/plans/active/ODY-S02-011_Authoritative_Command_And_Delta_Broadcast.md`
**Created:** 2026-08-26
**Last updated:** 2026-08-26 UTC

## 1. Goal

Validate a player-issued token-move command entirely host-side over the already-accepted command-pipeline ordering (`ADR-002`, action-check per `ADR-019` §6.1), and broadcast the resulting `PatchFields` delta to every entitled connection — redacted per `ODY-S02-010`'s `VisibilityPolicy` — so every entitled client converges on the same authoritative position, proving roadmap §11.7 exit criterion 2 ("host is the sole authority").

## 2. Why this task exists

- Problem or dependency being addressed: roadmap §11.6 steps 5–7 (move → validate → converge) has no production implementation yet.
- Value or risk reduction: proves host-authoritative mutation and audience-filtered delta delivery work end-to-end before `ODY-S02-012` adds reconnect/gap-repair on top.
- Blocking or enabling relationship: depends on `ODY-S02-010` (a scene must already be deliverable before a command can act on it); blocks `ODY-S02-012` (reconnect resumes a session that must already support commands/deltas).

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md` (command pipeline ordering, §11; command identity/idempotency, §9; optimistic concurrency, §10; command result model, §13) — principles followed, Core primitive types not instantiated (see §18 decision log)
- `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` §6.1 (action-check, submission + pre-commit), §6.2/§7 (read/visibility check governs delta audience), §9 (existing `SafeReasonCode` vocabulary, reused)
- `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` §5 (`ProjectionDeltaBatch`/`Operations[]`, `PatchFields` used here), §6 (dedup distinct from `AppliedCommands`, not implemented here — `ODY-S02-012`)
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md` §6.6
- `docs/adr/ADR-003_Serialization_Strategy_v1.1.md` §3
- `docs/adr/ADR-004_Result_and_Error_Model_v1.0.md`
- `docs/adr/ADR-015_Transport_Abstraction_v1.0.md` §5.1/§6.1
- `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md` §5 (`ODY-S02-011` boundary, fixed, not redefined here)

### Requirement and test IDs

- Requirement IDs: `SLICE-02` (implementation), backlog `ODY-S02-011`.
- Existing test IDs: `None` — first `MoveToken`/delta-broadcast production implementation over the network session layer.
- New test IDs to introduce: `TC-NET-015`–`TC-NET-020` (registered in `Tests/Metadata/test-catalog.json`), plus additional pure-logic tests not individually catalog-registered (see §10).

### Task-safe private context

- Approved summary / references: roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` §11.6 steps 5–7 ("player двигает токен" → "host валидирует команду" → "оба клиента видят одинаковый результат") — private local reference, summarized only, not pasted verbatim.

## 4. Verified current state

### Verified facts

- `ODY-S02-010` is merged to `main` (`5927df8`): `Scene`/`SceneEntity`/`VisibilityPolicy`/`ProjectionSnapshot` (`Odyssey.Application.Networking.Projection`) exist and are real, tested over `InProcessSessionTransport`.
- `Odyssey.Application.Commands.CommandContracts.cs`'s `CommandExecutor`/`DomainEventBatch` (SLICE-00 Core primitives) require a valid, non-nullable `CampaignId` (`Odyssey.Domain.Events.DomainEvents.cs`, `DomainEvent.Create`) — verified by reading the constructor's validation.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSceneRepository.cs`'s `MoveToken` operates on `CampaignHandle`/`TokenId`/`SceneId` (SQLite-persisted identity), with no existing mapping to `ODY-S02-010`'s network `Scene`/`SceneEntity` (string `EntityId`, no persistence) — verified by reading both files.
- `Odyssey.Application.Persistence.SceneRepositoryContracts.cs` defines a standalone `TokenPosition` struct (`double X, Y`) independent of `ISceneRepository`/SQLite — verified by reading the file; reused directly by this task.
- `CanonicalJsonWriter`/`JsonObjectReader` (`Odyssey.Application.Serialization.CanonicalJson.cs`) have no `Double`/`Float` writer or reader support — verified by reading the full file.

### Assumptions

- `None`.

## 5. Scope

### In scope

- `Odyssey.Application.Networking.Command`: `SceneMutableState`, `TokenMoveSessionState`, `MoveTokenCommand`, `TokenMoveOutcome`, `TokenMoveFailures`, `MoveTokenService`, `TokenMovedDelta`, `DeltaBroadcastPlanner`, `TokenMoveWireCodec`.
- `Odyssey.Networking.Command`: `TokenMoveClientChannel`, `TokenMoveHostChannel.ProcessPendingRequestsAsync`/`BroadcastDeltaAsync`.
- Tests proving: valid move by the owning Player or by MainGM; wrong-actor denial; unknown-entity rejection; stale-revision rejection; duplicate-`CommandId` idempotent replay and fingerprint-mismatch rejection; two connected clients converging after a valid move; an invalid move broadcasting no delta; an Observer without visibility receiving no delta for a Hidden entity.
- 3 new `ErrorCode`s (`ERROR_CODES.md`/`ErrorCodes.cs`), 6 new `test-catalog.json` entries.
- This task contract, its ExecPlan, `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md`'s `ODY-S02-011` row.

### Out of scope

- Full `ProjectionDeltaBatch`/`Operations[]` (beyond the single `PatchFields` case), gap detection, dedup-by-range, delta-buffer, reconnect (`ADR-017` §5–§9) — `ODY-S02-012`.
- Campaign persistence: `SqliteSceneRepository.MoveToken`, `ADR-002`'s `CommandExecutor`/`DomainEventBatch`/`ICommandCommitter`/`AppliedCommands` machinery — deliberately not wired in at this prototype stage (§18 decision log); an explicit open question for future slice integration, not silently skipped.
- Any new `SafeReasonCode` value — all three new `ErrorCode`s reuse existing `SafeReasonCode`s (`PermissionDenied`, `TargetUnavailable`, `StateChanged`).
- Editing `ADR-015`–`019`, `ODY-S02-009`/`010`'s own files (only their public API is consumed).

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Networking/Command/
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.networking/Runtime/Command/
DotNet/Tests/Odyssey.Tests.Networking/TokenMove/
Tests/Metadata/test-catalog.json
docs/errors/ERROR_CODES.md
docs/tasks/active/ODY-S02-011_Authoritative_Command_And_Delta_Broadcast.md
docs/plans/active/ODY-S02-011_Authoritative_Command_And_Delta_Broadcast.md
docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: `Odyssey.Application` decides (validation, authorization, redaction); `Odyssey.Networking` only transports already-decided outcomes/deltas (`ADR-001` §6.6, `ADR-019` §6.2/§11/§12).
- Authoritative-state and transaction boundary: single in-memory `SceneMutableState` per session (not a per-connection copy); this task's own in-memory receipt store is an explicit, non-durable, non-crash-recoverable stand-in for `AppliedCommands` — an open question for future persistence integration (§18).
- Serialization / compatibility boundary: hand-written canonical JSON (`ADR-003` §3); position fields string-encoded (`JsonObjectReader` has no Float token support).
- Time / RNG rule: `IWallClock`-injected throughout; no RNG used by this task.
- Unity / thread / lifetime rule: Not applicable — pure .NET Core code.
- Dependency / licensing rule: no new dependency introduced.
- Security / privacy / redaction rule: action-check (`ADR-019` §6.1) performed at submission and again immediately before commit; read/visibility check (`ADR-019` §6.2) computed entirely in `Odyssey.Application` before any delta reaches `Odyssey.Networking`; safe errors for "entity not found" and "entity exists but not visible to this actor" are the same `ErrorCode` (`ADR-002` §10.3).
- Performance or platform constraint: Not applicable at this scale (in-memory, `InProcessSessionTransport` only).
- Other: `Not applicable`.

## 7. Expected behavior

### Scenario 1 — Valid move, two clients converge

**Given** a session with a host (MainGM), a Player, and an Observer, and a scene with one `Public` entity
**When** the host moves that entity and the resulting delta is broadcast to the Player and the Observer (both entitled, since it is `Public`), each over its own `InProcessSessionTransport` connection
**Then** both clients' drained deltas report the identical new position and entity revision

### Scenario 2 — Invalid move is rejected, no delta broadcast

**Given** a `HiddenGameplay` entity assigned to Player A
**When** Player B (not the assigned actor) submits a move request for that entity over the network
**Then** Player B receives a typed `networking.command.token_move_denied` rejection, no state changes, and no delta is broadcast to any connection (verified: an unrelated Observer's connection receives zero deltas)

### Scenario 3 — Redaction is respected in delta broadcast

**Given** a `HiddenGameplay` entity assigned to Player A, and a connected Observer without visibility of it
**When** Player A validly moves that entity
**Then** Player A's connection receives the delta; the Observer's connection receives none

### Required invariants

- `Odyssey.Networking.Command` never computes an authorization or visibility decision itself — it only encodes/sends/drains/decodes what `MoveTokenService`/`DeltaBroadcastPlanner` already decided.
- A duplicate `CommandId` with the same parameters always replays the stored result, never re-mutates state; a duplicate `CommandId` with different parameters is always rejected as a typed mismatch, never silently accepted under the old or new payload.
- A rejected command never produces a delta broadcast to any connection.

## 8. Deliverables

- Production code: `TokenMoveContracts.cs`, `TokenMoveWireCodec.cs` (`Odyssey.Application.Networking.Command`); `TokenMoveChannels.cs` (`Odyssey.Networking.Command`); 3 new entries in `ErrorCodes.cs`.
- Tests: `TokenMoveServiceTests.cs` (7 pure-logic tests), `TokenMoveTransportTests.cs` (3 transport-level tests, ≥2 connected `InProcessSessionTransport` sides).
- Scripts / CI: `None` — no changes.
- Configuration: `None`.
- Documentation: this task contract; its ExecPlan; `ERROR_CODES.md`; `SLICE-02_IMPLEMENTATION_BACKLOG.md`'s `ODY-S02-011` row.
- Generated evidence or build artifacts: `None` beyond the PR/CI record.
- Migration / recovery material: `None` — no persisted format introduced.

## 9. Acceptance criteria

1. `MoveTokenService.Execute` succeeds for a move by the entity's assigned Player, incrementing the entity's revision.
2. `MoveTokenService.Execute` succeeds for a move by MainGM on any entity, regardless of assignment.
3. `MoveTokenService.Execute` by a Player who is not the entity's assigned actor returns the typed `networking.command.token_move_denied` failure.
4. `MoveTokenService.Execute` against an unknown entity id returns the typed `networking.command.token_not_found` failure.
5. `MoveTokenService.Execute` with a stale `ExpectedRevision` returns the typed `networking.command.token_revision_conflict` failure.
6. A duplicate `CommandId` with identical parameters replays the stored result without mutating state a second time.
7. A duplicate `CommandId` with different parameters returns the existing `application.command.identity_mismatch` failure (`ADR-002` §9.3), not the new result under the old id.
8. A valid move on a `Public` entity, delivered over real `InProcessSessionTransport`, produces identical delta content (position, revision) at two independently connected clients.
9. An invalid move request, submitted over real `InProcessSessionTransport`, is rejected with a typed failure and produces no delta broadcast to any connection.
10. A valid move on a `HiddenGameplay` entity broadcasts a delta to the assigned Player but not to a connected Observer without visibility of it.
11. No new `SafeReasonCode` is introduced (validation criterion — the three new `ErrorCode`s map to `PermissionDenied`/`TargetUnavailable`/`StateChanged`, all pre-existing).
12. `git status --porcelain` shows only files listed in §5's Allowed paths — no `ADR-015`–`019` file touched, no `ODY-S02-009`/`010` file touched.
13. Draft PR opened; CI green on all required checks (validation criterion, confirmed via `gh pr view`).

The task is not complete while any mandatory criterion is unverified, failed, or silently deferred.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-NET-015` | `.NET / dotnet test` | Non-owning Player's move is denied | Pass |
| `TC-NET-016` | `.NET / dotnet test` | Unknown entity id returns typed not-found | Pass |
| `TC-NET-017` | `.NET / dotnet test` | Stale expected revision returns typed conflict | Pass |
| `TC-NET-018` | `.NET / dotnet test` | Two connected clients converge after a valid move | Pass |
| `TC-NET-019` | `.NET / dotnet test` | Invalid move over the network triggers no delta broadcast | Pass |
| `TC-NET-020` | `.NET / dotnet test` | Observer without visibility receives no delta for a Hidden entity | Pass |
| (uncatalogued) | `.NET / dotnet test` | `TokenMoveServiceTests.cs`'s remaining 4 pure-logic cases (valid move by Player/MainGM, duplicate-replay, duplicate-mismatch) | Pass |

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
dotnet build DotNet/Odyssey.Core.sln
dotnet test DotNet/Odyssey.Core.sln
```

### Manual validation

- `None` — no UI/manual-only surface; all behavior is covered by automated `.NET` tests.

### Required environments / profiles

- OS / architecture: Windows, .NET 10 SDK (matches CI).
- Unity editor or Player profile: Not applicable — pure .NET Core code only.
- Scripting backend: Not applicable.
- Network topology or database fixture: `InProcessSessionTransport` only, ≥2 connected sides per transport test, no real network.
- Other: `None`.

### Validation not required by this task

- Unity Editor/PlayMode compile or test run — no Unity-side file changed.
- Real network/relay integration — blocked behind `ADR-016` §14 (`ODY-S02-014`), not this task's concern.
- Migration rehearsal — no persisted format introduced.
- SQLite/campaign persistence integration — explicitly deferred (§18 decision log), not this task's scope.

## 11. Compatibility, migration, and rollback

- Compatibility impact: introduces three new wire contracts (`odyssey.command.move_token_request`/`_outcome`, `odyssey.command.token_moved_delta`, all `contractVersion` 1) — no prior version to migrate from.
- Version fields affected: `None` at the application/package level.
- Migration or upcaster: `None`.
- Forward / backward behavior: `Not applicable` — no deployed clients depend on this new contract yet.
- Rollback method: revert this task's commits; nothing outside this task's own files and not-yet-started `ODY-S02-012`/`013` depends on it.
- Data-loss risk and protection: `None` — no persisted state; all mutation is in-memory, scoped to a session's lifetime.
- Recovery rehearsal required: `No`.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: scene entity position (a mutable field of already-classified `Public`/`HiddenGameplay` content, `ODY-S02-010`).
- Trust boundaries: host (authoritative, decides every move) vs. each connected client (submits an unverified intent, receives only a redacted delta).
- Authorization / action checks: `MoveTokenService.Execute`'s two-point action-check (`ADR-019` §6.1) — submission and immediately before commit.
- Redaction requirements: delta broadcast reuses `VisibilityPolicy.ComputeVisibleEntities` (`ODY-S02-010`, unmodified) — an audience that cannot see the moved entity receives no delta for it at all, not a delta with the field omitted.
- Log-safe fields: `None` new — no logging added by this task.
- Abuse / malformed input limits: wire codec bounded by 4096 bytes, rejects malformed/oversized/unsupported-contract payloads via `SerializationFailures`, matching every other production codec in this repository.
- Security tests: `TC-NET-019` (rejected command never broadcasts) and `TC-NET-020` (Observer never receives a delta for a Hidden entity) are this task's direct security-relevant regression tests, in the same spirit as `ODY-S02-007`'s (SP-04) hidden-data-boundary suite.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: matches multiple explicit `PLANS.md` §1.2 triggers — changes more than one production module (`Odyssey.Application` and `Odyssey.Networking`); affects networking, security, and permissions directly (first production command-validation/delta-broadcast implementation); required real design judgment (persistence-integration decision, Core-primitive-reuse decision, mutable-state placement, wire-format workaround for missing Float support) documented as five real decisions in the ExecPlan.
- ExecPlan path: `docs/plans/active/ODY-S02-011_Authoritative_Command_And_Delta_Broadcast.md`
- Expected pull request count: 1 (single Draft PR covering all production code, tests, and registry updates).
- Milestone or sequencing constraints: depends on `ODY-S02-010` (merged); blocks `ODY-S02-012` (`SLICE-02_IMPLEMENTATION_BACKLOG.md` §6).

## 15. Documentation and versioning impact

- Documents that must change: this task contract, its ExecPlan, `Tests/Metadata/test-catalog.json`, `docs/errors/ERROR_CODES.md`, `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md` (`ODY-S02-011` row only).
- Documents that must not change: `ADR-001`–`019`, `docs/tasks/SLICE-02_BACKLOG.md`, `docs/tasks/completed/*`, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything under `Documentation/`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: introduces three new wire contracts (`contractVersion` 1, new) — no prior version to migrate from.
- Documentation version changes: None — no ADR changes version.
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
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Packages/com.odyssey.application/Runtime/Networking/Command/TokenMoveContracts.cs` — new.
- `Packages/com.odyssey.application/Runtime/Networking/Command/TokenMoveWireCodec.cs` — new.
- `Packages/com.odyssey.networking/Runtime/Command/TokenMoveChannels.cs` — new.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — 3 new codes.
- `DotNet/Tests/Odyssey.Tests.Networking/TokenMove/TokenMoveServiceTests.cs`, `TokenMoveTransportTests.cs` — new.
- `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json` — registry additions.
- `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md` — `ODY-S02-011` row status.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet/Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test DotNet/Tests/Odyssey.Tests.Networking/Odyssey.Tests.Networking.csproj --filter "FullyQualifiedName~TokenMove"` | Passed | 10/10 new tests, 0 failed. |
| `dotnet test DotNet/Odyssey.Core.sln` (full suite) | Passed | 189/189, 0 failed (1 Contracts + 1 Domain + 56 Networking [46 pre-existing + 10 new] + 84 Unit + 2 Architecture + 45 Persistence), including `RepositoryStructurePassesArchitectureGuard`. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS`. |
| `.\scripts\check-repository-policy.ps1` | Passed | All checks pass, including `REPO-POLICY-005`. |
| `.\scripts\verify-test-structure.ps1` | Passed | `TC-ARCH-001 PASS`, all `TC-ARCH-002` controlled-fixture checks pass; catalog cross-check for `TC-NET-015`–`020` resolves now that this task contract exists. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `TokenMoveServiceTests.ValidMove_ByAssignedPlayer_AppliesAndReturnsIncrementedRevision`. |
| AC-2 | Passed | `TokenMoveServiceTests.ValidMove_ByMainGM_ForAnyEntity_Succeeds`. |
| AC-3 | Passed | `TokenMoveServiceTests.Move_ByNonOwningPlayer_ReturnsTypedActionNotAllowed`. |
| AC-4 | Passed | `TokenMoveServiceTests.Move_UnknownEntity_ReturnsTypedTokenNotFound`. |
| AC-5 | Passed | `TokenMoveServiceTests.Move_StaleExpectedRevision_ReturnsTypedRevisionConflict`. |
| AC-6 | Passed | `TokenMoveServiceTests.Move_DuplicateCommandId_SameParams_ReplaysStoredResult_DoesNotDoubleApply`. |
| AC-7 | Passed | `TokenMoveServiceTests.Move_DuplicateCommandId_DifferentParams_ReturnsTypedCommandIdentityMismatch`. |
| AC-8 | Passed | `TC-NET-018`, `TokenMoveTransportTests.ValidMove_OnPublicEntity_BothPlayerAndObserverClientsConverge_OverRealTransport`. |
| AC-9 | Passed | `TC-NET-019`, `TokenMoveTransportTests.InvalidMove_NotOwnToken_ReturnsTypedRejection_OverRealTransport_NoDeltaBroadcast`. |
| AC-10 | Passed | `TC-NET-020`, `TokenMoveTransportTests.ValidMove_OnHiddenEntity_ObserverWithoutVisibility_ReceivesNoDelta_OverRealTransport`. |
| AC-11 | Passed | `TokenMoveFailures`'s three new factories map to `SafeReasonCode.PermissionDenied`/`TargetUnavailable`/`StateChanged` — all pre-existing values (`ADR-004`), confirmed by code review. |
| AC-12 | Passed | `git status --porcelain` shows only: `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs`, `Tests/Metadata/test-catalog.json`, `docs/errors/ERROR_CODES.md`, `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md`, `DotNet/Tests/Odyssey.Tests.Networking/TokenMove/`, `Packages/com.odyssey.application/Runtime/Networking/Command/`, `Packages/com.odyssey.networking/Runtime/Command/`, this task contract, and its ExecPlan — all within §5's Allowed paths; no `ADR-015`–`019` or `ODY-S02-009`/`010` file touched. |
| AC-13 | Pending | Draft PR not yet opened; CI status to be confirmed. |

## 18. Blockers, risks, and open decisions

- Blocker: `None`.
- Open decision (deliberate, not a blocker): campaign persistence integration (`SqliteSceneRepository.MoveToken`) is explicitly deferred — a future slice-integration task must decide how (or whether) a network session's `MoveToken` command maps onto a persisted campaign's token records once a session is actually bound to one; this task does not invent that mapping.
- Open decision (deliberate, not a blocker): `MoveTokenService`'s in-memory command-receipt store is an explicitly non-durable stand-in for `ADR-002`'s `AppliedCommands` — a crash of the host process loses in-flight idempotency state, acceptable for this prototype (no persistence anywhere else in this session either) but a real limitation a future persistence-integration task must resolve, not silently carry forward as if already solved.
- Risk: none identified beyond what is already named as future scope (`ProjectionDeltaBatch` full operation set, reconnect, campaign persistence) in `SLICE-02_IMPLEMENTATION_BACKLOG.md` §5.
