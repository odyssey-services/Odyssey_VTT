# ODY-S02-012 — Reconnect, Delta Continuity & Duplicate-Delivery Idempotency

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s02-012-reconnect-delta-continuity-duplicate-delivery-idempotency`
**Pull request:** Draft — [#49](https://github.com/odyssey-services/Odyssey_VTT/pull/49)
**Last updated:** 2026-08-26 UTC

## 1. Purpose and user-visible outcome

Implements roadmap §11.6 steps 8–10: a player loses connection, reconnects, and receives the current state without any command re-applying — satisfying roadmap §11.7 exit criteria 3 (duplicate delivery) and 4 (reconnect restores scene/role). Builds on `ODY-S02-010` (`ProjectionSnapshot`, the fallback path) and `ODY-S02-011` (`MoveTokenService`/delta broadcast, the continuity this task extends with buffering and reconnect catch-up).

## 2. Task contract

- Goal: a bounded host-side delta buffer (`ADR-017` §8), a reconnect flow choosing buffered catch-up vs. full-snapshot fallback (`ADR-017` §9 steps 4–7, narrowed to this task's scope), redaction always recomputed from current permissions at replay time (`ADR-017` §1 point 8), and client-side dedup of a redelivered range (`ADR-017` §6).
- Acceptance criteria: see task contract §9.
- Requirement IDs: `SLICE-02` (implementation), backlog `ODY-S02-012`.
- In scope: `Odyssey.Application.Networking.Reconnect` (delta buffer, reconnect planner, continuity broadcast planner, client-side dedup tracker), `Odyssey.Networking.Reconnect` adapter, tests, task contract, ExecPlan, backlog row update.
- Out of scope: real transport-level reconnect (`ODY-S02-014`, blocked behind `ADR-016` §14); campaign persistence; new identity/permissions mechanism.
- Required authorities: `SLICE-02_IMPLEMENTATION_BACKLOG.md` §5, `ADR-017` §1 point 8/§6/§8/§9, `ADR-016` §5 (not redefined here), `ADR-015`, `ADR-001` §6.6, `ADR-003`, `ADR-004`.
- Required validation commands: `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `dotnet build`/`dotnet test` on `DotNet/Odyssey.Core.sln`.

## 3. Current state

- `ODY-S02-011` merged to `main`: `MoveTokenService`, `TokenMoveHostChannel`, `Odyssey.Application.Networking.Command` exist and are real, tested over `InProcessSessionTransport`; its own task contract §18 records that its in-memory command-receipt store is an explicitly non-durable stand-in for `AppliedCommands` — a crash of the host process loses it, but no test in this task's own single-process suite crashes the host process, so this limitation does not block reconnect testing here.
- `ODY-S02-010`'s `SceneProjectionBuilder.BuildSnapshot`/`ProjectionSnapshotWireCodec` (`Odyssey.Application.Networking.Projection`) are reused unmodified for the snapshot-fallback path.
- `InProcessSessionTransport.Disconnect` sets only the caller's own `_connection` to null; it does not stop the peer from continuing to enqueue into the caller's inbox (verified by reading the full file) — so "player offline" cannot be modeled as "delivery blocked at the transport" in this mock; it must be modeled at the Application layer (an audience simply absent from the host's connected-audience set for that round of live broadcast).

## 4. Proposed approach

Introduce a session-wide, bounded `SessionDeltaBuffer` (host-side, one per session, not per connection) that records every committed move regardless of who is currently connected. Live broadcast (`ContinuityBroadcastPlanner`) sends immediately only to audiences that are both entitled (`VisibilityPolicy`, `ODY-S02-010`, unmodified) and currently in the connected set, updating each successfully-sent audience's host-tracked `LastAcknowledgedSequence`. A disconnected-then-reconnecting audience's `LastAcknowledgedSequence` stays frozen at whatever it last actually received; `ReconnectPlanner` computes the missed range against the buffer, returning either buffered catch-up entries (re-filtered by the audience's *current* role, never a cached one) or a full snapshot when the buffer no longer covers the gap. A minimal `ClientProjectionState` gives tests (and, in spirit, a future real client) a place to detect and ignore a redelivered range by `BufferSequence`.

## 5. Milestones

### M1 — Pure buffer/planner logic, real and fully tested

- [x] `ReconnectContracts.cs` written in `Odyssey.Application.Networking.Reconnect` (`BufferedDelta`, `SessionDeltaBuffer`, `ReconnectSessionState`, `ReconnectPlan`/`ReconnectPlanner`, `ContinuityBroadcastPlanner`, `ClientProjectionState`).
- [x] 5 pure-logic tests pass (buffer within-capacity range, buffer-exceeded range, already-caught-up range, unknown-audience typed failure, catch-up filtered by current role).

### M2 — Real wire codec and transport adapter, exercised over InProcessSessionTransport

- [x] `ReconnectWireCodec.cs` written (hand-written canonical JSON, `ADR-003`-compliant; reuses `ODY-S02-010`'s `ProjectionSnapshotWireCodec` for the fallback payload rather than re-encoding it).
- [x] `ReconnectChannels.cs` written in `Odyssey.Networking.Reconnect` (`ContinuityClientChannel`, `ContinuityHostChannel.BroadcastLiveMoveAsync`/`ProcessReconnectRequestsAsync`).
- [x] 4 required transport-level tests pass, all over `InProcessSessionTransport` with a fresh pair simulating reconnect on the same `UserId`: within-buffer catch-up (no snapshot); outside-buffer snapshot fallback (no catch-up deltas); redelivered range not applied twice; role revoked while disconnected excludes the now-invisible entity from catch-up.

### M3 — Registries, validation, task contract complete

- [x] 4 new `test-catalog.json` entries (`TC-NET-021`–`024`).
- [x] Full solution build/test green; `verify-format.ps1`/`check-repository-policy.ps1` pass.
- [x] Task contract, backlog row, Draft PR ([#49](https://github.com/odyssey-services/Odyssey_VTT/pull/49)).

## 6. Progress log

- 2026-08-26 — Preflight confirmed `ODY-S02-011` merged; branched cleanly from `main`.
- 2026-08-26 — Read `SLICE-02_IMPLEMENTATION_BACKLOG.md` §5, `ADR-017` §1/§6/§8/§9 (re-read in full context), `ADR-016` §5 (confirmed not redefined), `InProcessSessionTransport.cs` (confirmed `Disconnect` does not gate peer delivery — informs the "offline" modeling decision), `ODY-S02-011`'s task contract §18 (confirmed non-durable receipt store doesn't block this task's single-process reconnect tests).
- 2026-08-26 — Wrote buffer/planner logic, wire codec, transport adapter. First full test run found one real bug: `ContinuityClientChannel.DrainCatchupDeltas` then `DrainSnapshots` called back-to-back on the same connection double-drains `ISessionTransport.DrainReliable` (destructive per call) -- the first call silently discarded the snapshot meant for the second. Fixed by replacing both with a single combined `DrainReconnectPayloads` that partitions one `DrainReliable` call's results into both buckets. All 10 tests pass after the fix.
- 2026-08-26 — Full solution: 199 tests pass (189 pre-existing + 10 new).

## 7. Decisions

- 2026-08-26 — Decision: delta-buffer capacity is `3` (`SessionDeltaBuffer.DefaultCapacity`). Rationale: `ADR-017` §8 point 4 explicitly leaves the exact size unfixed, an implementation parameter; `3` is the smallest value that lets a single test deterministically exercise both paths without an unwieldy move count (2 missed = within buffer, 5 missed = unambiguously exceeds it) while remaining trivially readable. No empirical basis exists yet for a production number (mirrors `ADR-017` §8 point 4's own reasoning for why it declines to fix one) — a future task revisiting real-network reconnect timing (`ODY-S02-014`) would tune this, not treat it as final. Authority: `ADR-017` §8 point 4's explicit deferral; this task's own "реши сам, но обоснуй" instruction.
- 2026-08-26 — Decision: "player loses connection" is modeled by removing their `UserId` from the host's connected-audience set (an `ISet<UserId>`/`Dictionary<UserId, ...>` the test/caller maintains), not by calling `ISessionTransport.Disconnect`. Rationale: verified by reading `InProcessSessionTransport.cs` that `Disconnect` only nulls the caller's own `_connection` field — it does not stop the peer from continuing to enqueue envelopes into the disconnected side's inbox, so it cannot represent "this audience is currently unreachable" in this mock transport. Modeling offline-ness at the Application layer (identity-keyed, per `ADR-018`) is also more faithful to `ADR-016` §5's own reconnect model, which is about resuming the SAME `UserId`'s session via a NEW allocation, not resuming the same `ConnectionHandle`. Authority: this task's own explicit design hint; direct inspection of `InProcessSessionTransport.cs`.
- 2026-08-26 — Decision: `LastAcknowledgedSequence` is tracked host-side (`ReconnectSessionState`), updated only when the host itself successfully sends a delta, never accepted as a client-reported value in the reconnect request. Rationale: `ADR-002` §6.5 ("client-provided fields are claims, not proof") applies directly — a lying or buggy client claiming a stale/future `LastAcknowledgedSequence` could either miss data or fabricate having-seen data it never received; the host already knows exactly what it sent, so it never needs to ask. This is a strengthening of `ADR-017` §9 step 4's literal wording ("client передаёт LastAcknowledgedSequence"), not a contradiction of its intent. Authority: `ADR-002` §6.5's already-accepted principle, applied to this task's own new surface.
- 2026-08-26 — Decision: the full-snapshot fallback (`ODY-S02-010`'s `ProjectionSnapshot`) does not carry live position data, since `SceneEntity`/`ProjectionSnapshot` (`ODY-S02-010`) were never extended with a position field and this task must not edit that file. This task's fallback-path tests verify the correct *entity set* is restored (proving the fallback triggers and redaction is fresh), not position fidelity in the same payload — an explicit, named limitation (§18), not a silent gap, mirroring `ODY-S02-011`'s own persistence-integration deferral pattern.
- 2026-08-26 — Decision: `ContinuityBroadcastPlanner`/`MoveTokenService` (`ODY-S02-011`) are not merged into one call path; this task's tests drive the buffer/broadcast planner directly with `(entityId, position, revision)` rather than going through `MoveTokenService.Execute` first. Rationale: `MoveTokenService`'s own validation (authorization, revision conflict) is already fully tested by `ODY-S02-011`; this task's concern is what happens to an *already-accepted* move's delta over time (buffering, catch-up, dedup) — re-deriving it from a freshly-validated command on every test would duplicate `ODY-S02-011`'s own test coverage without adding assurance specific to reconnect. A real host implementation would naturally call `MoveTokenService.Execute` first and feed its `TokenMoveOutcome` into `ContinuityBroadcastPlanner`; nothing in this task's design prevents that composition.

## 8. Discoveries and deviations

- Discovery/bug found and fixed: `ISessionTransport.DrainReliable` is destructive per call (empties the whole inbox), and a reconnect exchange can legitimately deliver either buffered-delta or full-snapshot messages (never both) to the same connection. Two single-purpose `Drain*` methods called back-to-back on the same connection therefore silently lose whichever payload the first call's filter discarded. Fixed during this task by replacing the two separate Drain methods with one combined `DrainReconnectPayloads` that partitions a single `DrainReliable` call's results — caught by this task's own `Reconnect_OutsideBuffer_...` test failing on first run, not by inspection alone.

## 9. Validation and acceptance evidence

Recorded in the task contract's §17 once the full validation suite is run.

## 10. Recovery and rollback

Reverting this task's commits removes all new reconnect/buffer code and tests with no compatibility or data-loss risk — nothing outside this task's own files and `ODY-S02-013` (not yet started) depends on it; no persisted state is touched.

## 11. Open questions and blockers

- No blockers.
- Open question, explicitly deferred, not resolved by this task: full-snapshot fallback does not currently carry live position data (§7 decision log) — a future task enriching `ProjectionSnapshot` with mutable field state (or otherwise reconciling `ODY-S02-010`'s identity model with `ODY-S02-011`'s position model) would need to revisit this.
- Deferred, not blocking: real transport-level reconnect semantics (`ODY-S02-014`, blocked behind `ADR-016` §14).

## 12. Outcome and follow-up

Pending — this plan is updated to `Completed` once the task contract's remaining acceptance items are confirmed and the Draft PR is opened with green CI.
