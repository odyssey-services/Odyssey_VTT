# ODY-S02-011 — Authoritative Command & Delta Broadcast

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s02-011-authoritative-command-and-delta-broadcast`
**Pull request:** Draft — [#48](https://github.com/odyssey-services/Odyssey_VTT/pull/48)
**Last updated:** 2026-08-26 UTC

## 1. Purpose and user-visible outcome

Implements roadmap §11.6 steps 5–7: a player moves a token, the host validates the move entirely host-side, and every entitled connection converges on the same authoritative result (roadmap §11.7 exit criterion 2, "host is the sole authority"). Builds on `ODY-S02-009` (membership/role) and `ODY-S02-010` (scene identity/visibility) — the first task in this revision to mutate state after admission and redeliver it as a delta rather than a full snapshot.

## 2. Task contract

- Goal: host-side `MoveTokenCommand` validation following `ADR-002`'s pipeline ordering (narrowed to this in-memory prototype) with `ADR-019` §6.1's two-point action-check, and `ProjectionDeltaBatch`-style broadcast (`ADR-017` §5, `PatchFields`-only) to every entitled connection, redacted per `ODY-S02-010`'s `VisibilityPolicy`.
- Acceptance criteria: see task contract §9.
- Requirement IDs: `SLICE-02` (implementation), backlog `ODY-S02-011`.
- In scope: `Odyssey.Application.Networking.Command` (mutable scene state, command/outcome/delta contracts, `MoveTokenService`, `DeltaBroadcastPlanner`), `Odyssey.Networking.Command` adapter, tests, task contract, ExecPlan, backlog row update.
- Out of scope: full `ProjectionDeltaBatch`/`Operations[]`, gap detection, dedup, reconnect (`ADR-017` §5–§9, `ODY-S02-012`); campaign persistence (`SLICE-01`'s `SqliteSceneRepository`/`ADR-002`'s full `CommandExecutor`/`DomainEventBatch` machinery); new `SafeReasonCode` values.
- Required authorities: `SLICE-02_IMPLEMENTATION_BACKLOG.md` §5, `ADR-002` (command pipeline), `ADR-019` §6.1/§6.2/§7, `ADR-017` §5/§6, `ADR-015`, `ADR-001` §6.6, `ADR-003`, `ADR-004`.
- Required validation commands: `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `dotnet build`/`dotnet test` on `DotNet/Odyssey.Core.sln`.

## 3. Current state

- `ODY-S02-009`/`010` merged to `main`: `SessionAdmissionState`/`SessionMember`/`BaselineRole` (Application.Networking.Session), `Scene`/`SceneEntity`/`VisibilityPolicy`/`ProjectionSnapshot` (Application.Networking.Projection) exist and are real, tested over `InProcessSessionTransport`.
- `ADR-002`'s SLICE-00 Core primitives (`Odyssey.Application.Commands.CommandContracts.cs`) already provide `CommandId`, `CommandExecutor`, `DomainEventBatch`, etc. — but `DomainEvent.Create` requires a non-nullable, valid `CampaignId` (`Odyssey.Domain.Events.DomainEvents.cs` line ~300), tying that machinery to a persisted campaign this network-only prototype does not have.
- `SqliteSceneRepository.MoveToken` (`Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSceneRepository.cs`) already exists as a `CampaignHandle`-backed, SQLite-persisted token-move operation, keyed by `TokenId`/`SceneId` (persistence identity), unrelated to `ODY-S02-010`'s network `Scene`/`SceneEntity` (string `EntityId`, no persistence).
- `Odyssey.Application.Persistence.SceneRepositoryContracts.cs` already defines a standalone `TokenPosition` value type (`double X, Y`), independent of `ISceneRepository`/`SqliteSceneRepository` — reusable without pulling in any SQLite/`CampaignHandle` dependency.
- `CanonicalJsonWriter`/`JsonObjectReader` (Application.Serialization) have no `Double`/`Float` support — confirmed by inspection; string-encoded doubles are this task's workaround (see decision log).

## 4. Proposed approach

Do not instantiate `ADR-002`'s `CommandExecutor`/`DomainEventBatch`/`ICommandCommitter` (campaign-coupled, would require a fabricated `CampaignId`); do not call `SqliteSceneRepository.MoveToken` (a different identity space, different persistence layer, not this task's scope). Instead, mirror `ODY-S02-009`'s own precedent: a fresh, purpose-specific, in-memory `MoveTokenService` that follows `ADR-002` §11's *ordering* (duplicate-CommandId check → action-check → load/validate → action-check again → mutate) using `Odyssey.Application.Commands.CommandId` directly (genuine reuse of the one non-campaign-coupled identity primitive) but its own lightweight, explicitly non-durable receipt store — not `ICommandReceiptStore`/`AppliedCommands`. `SceneEntity` (`ODY-S02-010`) stays untouched; a new `SceneMutableState` tracks position/revision per entity id, reusing `Odyssey.Application.Persistence.TokenPosition` for the value shape. Delta broadcast reuses `ODY-S02-010`'s `VisibilityPolicy.ComputeVisibleEntities` unmodified to decide, per connected audience, whether they receive a delta for the moved entity at all.

## 5. Milestones

### M1 — Pure command validation logic, real and fully tested

- [x] `TokenMoveContracts.cs` written in `Odyssey.Application.Networking.Command` (`SceneMutableState`, `TokenMoveSessionState`, `MoveTokenCommand`, `TokenMoveOutcome`, `TokenMoveFailures`, `MoveTokenService`, `TokenMovedDelta`, `DeltaBroadcastPlanner`).
- [x] 7 pure-logic tests pass (valid move by owning Player/MainGM, wrong-actor denial, unknown entity, stale revision, duplicate-CommandId replay, duplicate-CommandId fingerprint mismatch).

### M2 — Real wire codec and transport adapter, exercised over InProcessSessionTransport

- [x] `TokenMoveWireCodec.cs` written (hand-written canonical JSON, `ADR-003`-compliant; string-encoded doubles, see decision log).
- [x] `TokenMoveChannels.cs` written in `Odyssey.Networking.Command` (`TokenMoveClientChannel`, `TokenMoveHostChannel.ProcessPendingRequestsAsync`/`BroadcastDeltaAsync`).
- [x] 3 required transport-level tests pass, all over ≥2 connected `InProcessSessionTransport` sides: valid move on a Public entity → two independently connected clients converge; invalid move (not own token) → typed rejection, zero delta broadcast; valid move on a Hidden entity → assigned Player receives the delta, an unrelated Observer does not.

### M3 — Registries, validation, task contract complete

- [x] 3 new `ErrorCode`s registered in `ERROR_CODES.md`/`ErrorCodes.cs`; 6 new `test-catalog.json` entries (`TC-NET-015`–`020`).
- [x] Full solution build/test green; `verify-format.ps1`/`check-repository-policy.ps1` pass.
- [x] Task contract, backlog row, Draft PR ([#48](https://github.com/odyssey-services/Odyssey_VTT/pull/48)).

## 6. Progress log

- 2026-08-26 — Preflight confirmed `ODY-S02-010` merged; branched cleanly from `main`.
- 2026-08-26 — Read `SLICE-02_IMPLEMENTATION_BACKLOG.md` §5, `ADR-002` (full), `ADR-019` §6.1 (full ADR re-read for context), `ADR-017` §5/§6, `SqliteSceneRepository.cs`/`SceneRepositoryContracts.cs` (SLICE-01 persistence reference), `CommandContracts.cs`/`DomainEvents.cs` (SLICE-00 Core primitives — determined `DomainEvent`'s mandatory `CampaignId` makes full reuse a poor fit here).
- 2026-08-26 — Wrote pure command-validation logic, wire codec, transport adapter; all 10 new tests pass on first full run (no compile-error iteration).
- 2026-08-26 — Full solution: 189 tests pass (179 pre-existing + 10 new).

## 7. Decisions

- 2026-08-26 — Decision: campaign persistence (`SqliteSceneRepository.MoveToken`, `SLICE-01`) is **not** wired into this task. Rationale: this task is about the network session layer (`Odyssey.Application.Networking`), building on `ODY-S02-009`/`010`, both of which are deliberately in-memory-only prototypes with no `CampaignHandle`/SQLite dependency; `SqliteSceneRepository.MoveToken` operates on a completely different identity space (`CampaignHandle`-backed SQLite rows, `TokenId`/`SceneId` primary keys) with no existing mapping to `ODY-S02-010`'s network `Scene`/`SceneEntity` (string `EntityId`, no DB). Wiring the two together would require inventing a `SessionId ↔ CampaignHandle` mapping and a `SceneEntity ↔ TokenRecord` mapping that no prior task or ADR defines — a materially larger, unapproved scope expansion this task's own instruction explicitly permits deferring. Recorded as an explicit open question for a future slice-integration task (§11), not silently skipped. Authority: this task's own explicit "реши сам, но обоснуй" instruction; `SLICE-02_IMPLEMENTATION_BACKLOG.md` §5's narrow boundary.
- 2026-08-26 — Decision: `ADR-002`'s SLICE-00 `CommandExecutor`/`DomainEventBatch`/`ICommandCommitter` machinery is **not** instantiated; `MoveTokenService` is a fresh, purpose-specific pipeline instead, reusing only `CommandId` (the one identity primitive with no campaign coupling). Rationale: `DomainEvent.Create` requires a valid, non-nullable `CampaignId` — fabricating a placeholder `CampaignId` for a session that has none would misrepresent the model and violate `ADR-012`'s campaign/event semantics; this mirrors `ODY-S02-009`'s own precedent (which also implements `ADR-002`-style command *ordering* — validate before mutate, single decision point, typed rejection — without adopting SLICE-00's Core primitive types) for the same underlying reason. `MoveTokenService`'s own in-memory receipt store is this task's explicitly non-durable, non-crash-recoverable stand-in for `AppliedCommands` (`ADR-002` §4.4/§22–24) — an open question for future persistence integration, not claimed as satisfying those sections. Authority: `ADR-002` §5.4/§5.6 (module ownership, followed in spirit); this task's own instruction to decide and justify, not silently duplicate.
- 2026-08-26 — Decision: `SceneEntity` (`ODY-S02-010`) is not modified to add a position field; a new `SceneMutableState` tracks position/revision per `EntityId` string, reusing `Odyssey.Application.Persistence.TokenPosition` (already-existing value type, `SLICE-01`) for the position shape without any dependency on `ISceneRepository`/SQLite. Rationale: the diff-scope constraint explicitly forbids editing `ODY-S02-010`'s code beyond its public API; identity/visibility (owned by `Scene`/`SceneEntity`) and mutable field state (owned by this task) are genuinely separable concerns, matching `ADR-017` §5's own `PatchFields` operation being a *field* patch, not an identity change. Reusing `TokenPosition` (rather than inventing a parallel `X`/`Y` struct) avoids exactly the "duplicate logic bezdumno" this task's own instruction warns against. Authority: `SLICE-02_IMPLEMENTATION_BACKLOG.md` §5's diff-scope boundary; this task's own reuse-vs-duplicate instruction.
- 2026-08-26 — Decision: wire-carried position fields are string-encoded doubles (`CultureInfo.InvariantCulture`, round-trip `"R"` format), not raw JSON numbers. Rationale: `JsonObjectReader`'s flat reader recognizes only String/Integer/Boolean/Null tokens, not Float — the same constraint `ODY-S02-010` hit for its entity array (there, solved with a hand-rolled array reader; here, solved by not needing a numeric JSON type at all, a smaller, equally valid fix for a flat payload). Authority: direct inspection of `JsonObjectReader.Read`'s token-type switch.
- 2026-08-26 — Decision: delta broadcast is a `PatchFields`-only, single-operation `TokenMovedDelta` (flattened fields, not a general `Operations[]` list). Rationale: this task's own instruction names `PatchFields` as the operation to use, and exactly one occurs per accepted move — introducing the full closed `Operations[]` list (`AddEntity`/`RemoveFromProjection`/etc., `ADR-017` §5) before any task needs those other kinds would be speculative, unrequested scope. Authority: `ADR-017` §5 (`Operations[]`'s closed-but-extensible list, not required to be exhausted); this task's own explicit hint.

## 8. Discoveries and deviations

- Discovery: `ADR-002`'s SLICE-00 Core primitives (`CommandExecutor` et al.) are tightly coupled to a persisted `CampaignId` via `DomainEvent`'s mandatory field — not a drop-in fit for a network-only, pre-persistence prototype session. This confirms (and generalizes) `ODY-S02-009`'s implicit precedent of implementing `ADR-002`'s *principles* freshly rather than its concrete Core types, for any command that does not yet have a backing campaign.
- Discovery: `Odyssey.Application.Persistence.TokenPosition` already exists as a standalone, dependency-free value type — reused directly, avoiding a parallel `X`/`Y` struct this task would otherwise have needed to invent.

## 9. Validation and acceptance evidence

Recorded in the task contract's §17 once the full validation suite is run.

## 10. Recovery and rollback

Reverting this task's commits removes all new command/delta code and tests with no compatibility or data-loss risk — nothing outside this task's own files and `ODY-S02-012`/`013` (not yet started) depends on it; no persisted state is touched.

## 11. Open questions and blockers

- No blockers.
- Open question, explicitly deferred, not resolved by this task: how (or whether) this network-session `MoveToken` command should eventually integrate with `SLICE-01`'s campaign-persisted `SqliteSceneRepository.MoveToken` once a real campaign is bound to a network session — a future slice-integration task's concern, not invented here.
- Deferred, not blocking: full `ProjectionDeltaBatch`/`Operations[]`, gap detection, dedup, reconnect (`ADR-017` §5–§9, `ODY-S02-012`).

## 12. Outcome and follow-up

Pending — this plan is updated to `Completed` once the task contract's remaining acceptance items are confirmed and the Draft PR is opened with green CI.
