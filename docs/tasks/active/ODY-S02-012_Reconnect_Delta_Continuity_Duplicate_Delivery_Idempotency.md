# ODY-S02-012 — Reconnect, Delta Continuity & Duplicate-Delivery Idempotency

**Status:** In Review
**Roadmap stage / slice:** SLICE-02, roadmap §11.6 steps 8–10
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s02-012-reconnect-delta-continuity-duplicate-delivery-idempotency`
**Pull request:** Draft — [#49](https://github.com/odyssey-services/Odyssey_VTT/pull/49)
**ExecPlan:** `docs/plans/active/ODY-S02-012_Reconnect_Delta_Continuity_Duplicate_Delivery_Idempotency.md`
**Created:** 2026-08-26
**Last updated:** 2026-08-26 UTC

## 1. Goal

Deliver a real reconnect flow — bounded host-side delta buffer, catch-up-vs-full-snapshot fallback, redaction always recomputed from current (not saved) permissions, and client-side dedup of a redelivered range — so a player who loses and regains connection ends up with the same authoritative state as if they had never disconnected, without any command re-applying (roadmap §11.7 exit criteria 3 and 4).

## 2. Why this task exists

- Problem or dependency being addressed: roadmap §11.6 steps 8–10 (disconnect → reconnect → resume without replay) has no production implementation yet.
- Value or risk reduction: proves the last piece of the vertical slice's continuity story before `ODY-S02-013` integrates all of `009`–`012` into one end-to-end scenario.
- Blocking or enabling relationship: depends on `ODY-S02-011` (delta broadcast must exist before continuity/catch-up can build on it); blocks `ODY-S02-013` (integration proof needs every prior deliverable, including this one).

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` §1 point 8 (redaction always by current permissions), §6 (dedup by `SequenceFrom`/`SequenceTo`, distinct from `AppliedCommands`), §8 (delta buffer, catch-up-vs-fallback rule), §9 (10-step reconnect flow, this task's narrowed portion: steps 4–7)
- `docs/adr/ADR-016_Rendezvous_Relay_Strategy_v1.0.md` §5 (transport-level reconnect model — not redefined here, this task's reconnect is application-level over `InProcessSessionTransport`)
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md` §6.6
- `docs/adr/ADR-003_Serialization_Strategy_v1.1.md` §3
- `docs/adr/ADR-004_Result_and_Error_Model_v1.0.md`
- `docs/adr/ADR-015_Transport_Abstraction_v1.0.md` §5.1/§6.1
- `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md` §5 (`ODY-S02-012` boundary, fixed, not redefined here)

### Requirement and test IDs

- Requirement IDs: `SLICE-02` (implementation), backlog `ODY-S02-012`.
- Existing test IDs: `None` — first reconnect/delta-buffer production implementation.
- New test IDs to introduce: `TC-NET-021`–`TC-NET-024` (registered in `Tests/Metadata/test-catalog.json`), plus additional pure-logic tests not individually catalog-registered (see §10).

### Task-safe private context

- Approved summary / references: roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` §11.6 steps 8–10 ("player теряет соединение" → "player переподключается" → "player получает текущее состояние без повторного применения команды") — private local reference, summarized only, not pasted verbatim.

## 4. Verified current state

### Verified facts

- `ODY-S02-011` is merged to `main` (`7cb64c7`): `MoveTokenService`/`TokenMoveHostChannel`/`Odyssey.Application.Networking.Command` exist and are real, tested over `InProcessSessionTransport`.
- `ODY-S02-011`'s own task contract §18 records its in-memory command-receipt store as an explicit non-durable stand-in for `AppliedCommands` — verified by reading that section; confirmed not a blocker here since no test in this task's single-process suite crashes the host process.
- `InProcessSessionTransport.Disconnect` (`Packages/com.odyssey.networking/Runtime/InProcess/InProcessSessionTransport.cs`) only nulls the caller's own `_connection` field; it does not stop the peer from continuing to enqueue into the caller's inbox — verified by reading the full file.
- `Odyssey.Application.Networking.Projection.SceneProjectionBuilder.BuildSnapshot`/`ProjectionSnapshotWireCodec` (`ODY-S02-010`) exist and are reused unmodified for this task's snapshot-fallback path.

### Assumptions

- `None`.

## 5. Scope

### In scope

- `Odyssey.Application.Networking.Reconnect`: `BufferedDelta`, `SessionDeltaBuffer`, `ReconnectSessionState`, `ReconnectPlan`/`ReconnectPathKind`/`ReconnectPlanner`, `ContinuityBroadcastPlanner`, `ClientProjectionState`, `ReconnectWireCodec`.
- `Odyssey.Networking.Reconnect`: `ContinuityClientChannel`, `ContinuityHostChannel.BroadcastLiveMoveAsync`/`ProcessReconnectRequestsAsync`.
- Tests proving: buffer within-capacity/exceeded/already-caught-up range queries; unknown-audience typed rejection; catch-up filtered by current role; reconnect within buffer (no snapshot); reconnect outside buffer (no catch-up deltas); redelivered range not applied twice; role revoked while disconnected excludes the now-invisible entity from catch-up.
- 4 new `test-catalog.json` entries.
- This task contract, its ExecPlan, `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md`'s `ODY-S02-012` row.

### Out of scope

- Real transport-level reconnect (`ODY-S02-014`, blocked behind `ADR-016` §14's empirical gate).
- Full campaign persistence — this task stays at the same in-memory level `ODY-S02-009`–`011` already established.
- Any new identity/permissions mechanism — reuses `ODY-S02-009`'s `SessionAdmissionService`/`BaselineRole` and `ODY-S02-010`'s `VisibilityPolicy` unmodified.
- Editing `ADR-015`–`019`, `ODY-S02-009`/`010`/`011`'s own files (only their public API is consumed).
- Live position data in the snapshot-fallback payload (§18 decision log — `ProjectionSnapshot` was never extended with a position field, and this task must not edit `ODY-S02-010`'s file to add one).

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Networking/Reconnect/
Packages/com.odyssey.networking/Runtime/Reconnect/
DotNet/Tests/Odyssey.Tests.Networking/Reconnect/
Tests/Metadata/test-catalog.json
docs/tasks/active/ODY-S02-012_Reconnect_Delta_Continuity_Duplicate_Delivery_Idempotency.md
docs/plans/active/ODY-S02-012_Reconnect_Delta_Continuity_Duplicate_Delivery_Idempotency.md
docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: `Odyssey.Application` decides (buffer bookkeeping, catch-up-vs-fallback, redaction); `Odyssey.Networking` only transports already-decided payloads (`ADR-001` §6.6, `ADR-017` §11).
- Authoritative-state and transaction boundary: one `SessionDeltaBuffer` per session (not per connection); `LastAcknowledgedSequence` is host-tracked, never client-reported (`ADR-002` §6.5's principle, applied here).
- Serialization / compatibility boundary: hand-written canonical JSON (`ADR-003` §3); the snapshot-fallback payload reuses `ODY-S02-010`'s existing codec verbatim, not a re-encoding.
- Time / RNG rule: `IWallClock`-injected throughout; no RNG used by this task.
- Unity / thread / lifetime rule: Not applicable — pure .NET Core code.
- Dependency / licensing rule: no new dependency introduced.
- Security / privacy / redaction rule: redaction is always recomputed from the audience's *current* `SessionMember.Role` at the moment a buffered entry is replayed or a fallback snapshot is built — never from a value cached when the entry was originally recorded or when the audience was last connected (`ADR-017` §1 point 8).
- Performance or platform constraint: Not applicable at this scale (in-memory, `InProcessSessionTransport` only, buffer capacity `3`).
- Other: `Not applicable`.

## 7. Expected behavior

### Scenario 1 — Reconnect within the buffer

**Given** a Player who received one live delta, then disconnected while two more moves occurred (buffer capacity `3`, nothing evicted)
**When** the Player reconnects on a new `InProcessSessionTransport` pair for the same `UserId`
**Then** they receive exactly the two missed buffered deltas and no full `ProjectionSnapshot`

### Scenario 2 — Reconnect outside the buffer

**Given** the same setup, but five more moves occur while disconnected (exceeding the buffer's capacity, evicting the missed range)
**When** the Player reconnects
**Then** they receive a full, freshly-redacted `ProjectionSnapshot` containing every currently-visible entity, and zero buffered catch-up deltas

### Scenario 3 — Redelivered range is not applied twice

**Given** a buffered delta already delivered once
**When** the host redelivers the identical entry a second time (simulating an at-least-once network retry)
**Then** both copies arrive on the wire, but `ClientProjectionState.TryApply` accepts only the first and ignores the second (same `BufferSequence`)

### Scenario 4 — Revoked visibility during disconnect is respected on reconnect

**Given** a Player assigned to a `HiddenGameplay` entity, who disconnects, and is then downgraded to `Observer` by the host while offline
**When** the Player reconnects, with the entry still within the buffer
**Then** the catch-up delivery excludes that entity entirely — the client never sees data it is no longer entitled to, even though the buffer still holds the entry

### Required invariants

- `Odyssey.Networking.Reconnect` never decides catch-up-vs-fallback itself — that decision is entirely `ReconnectPlanner`'s.
- A reconnect response is always either buffered catch-up entries or a full snapshot, never both, never neither (when there is anything to deliver).
- `ClientProjectionState` never regresses `LastAppliedSequence` — an entry at or below the current value is always ignored, regardless of its content.

## 8. Deliverables

- Production code: `ReconnectContracts.cs`, `ReconnectWireCodec.cs` (`Odyssey.Application.Networking.Reconnect`); `ReconnectChannels.cs` (`Odyssey.Networking.Reconnect`).
- Tests: `ReconnectServiceTests.cs` (5 pure-logic tests), `ReconnectTransportTests.cs` (4 transport-level tests, `InProcessSessionTransport`).
- Scripts / CI: `None` — no changes.
- Configuration: `None`.
- Documentation: this task contract; its ExecPlan; `SLICE-02_IMPLEMENTATION_BACKLOG.md`'s `ODY-S02-012` row.
- Generated evidence or build artifacts: `None` beyond the PR/CI record.
- Migration / recovery material: `None` — no persisted format introduced.

## 9. Acceptance criteria

1. `SessionDeltaBuffer.TryGetRangeSince` returns every entry after the given sequence when the full range is still held.
2. `SessionDeltaBuffer.TryGetRangeSince` returns `false` (fallback required) when any part of the requested range has been evicted.
3. `SessionDeltaBuffer.TryGetRangeSince` returns an empty success when the caller is already fully caught up.
4. `ReconnectPlanner.Plan` for an unknown audience returns the existing typed `networking.session.member_not_found` failure (`ODY-S02-009`, reused).
5. `ReconnectPlanner.Plan`'s catch-up entries are filtered by the audience's current role/assignment, not by whatever was true when each entry was recorded.
6. A reconnect within the buffer, delivered over real `InProcessSessionTransport`, produces exactly the missed buffered deltas and zero full snapshots.
7. A reconnect outside the buffer, delivered over real `InProcessSessionTransport`, produces exactly one full snapshot (with the correct currently-visible entity set) and zero buffered deltas.
8. A buffered delta redelivered a second time over real transport arrives on the wire both times, but `ClientProjectionState` applies it only once.
9. A Player downgraded to Observer while disconnected does not receive a buffered catch-up delta for a now-invisible `HiddenGameplay` entity on reconnect, even though the entry remains within the buffer.
10. No new `SafeReasonCode` or `ErrorCode` is introduced (validation criterion — the one typed failure path reuses `ODY-S02-009`'s existing `networking.session.member_not_found`).
11. `git status --porcelain` shows only files listed in §5's Allowed paths — no `ADR-015`–`019` or `ODY-S02-009`/`010`/`011` file touched.
12. Draft PR opened; CI green on all required checks (validation criterion, confirmed via `gh pr view`).

The task is not complete while any mandatory criterion is unverified, failed, or silently deferred.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-NET-021` | `.NET / dotnet test` | Reconnect within buffer receives only missed deltas, no snapshot | Pass |
| `TC-NET-022` | `.NET / dotnet test` | Reconnect outside buffer receives a full, freshly-redacted snapshot, no deltas | Pass |
| `TC-NET-023` | `.NET / dotnet test` | Redelivered buffered delta is applied only once, by `BufferSequence` | Pass |
| `TC-NET-024` | `.NET / dotnet test` | Role revoked while disconnected excludes the now-invisible entity from catch-up | Pass |
| (uncatalogued) | `.NET / dotnet test` | `ReconnectServiceTests.cs`'s remaining 5 pure-logic cases (buffer range queries, unknown-audience rejection, current-role filtering) | Pass |

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
- Network topology or database fixture: `InProcessSessionTransport` only; reconnect modeled via a fresh transport pair for the same `UserId`, no real network.
- Other: `None`.

### Validation not required by this task

- Unity Editor/PlayMode compile or test run — no Unity-side file changed.
- Real network/relay reconnect integration — blocked behind `ADR-016` §14 (`ODY-S02-014`), not this task's concern.
- Migration rehearsal — no persisted format introduced.
- Position fidelity in the snapshot-fallback payload — explicitly out of scope (§18 decision log).

## 11. Compatibility, migration, and rollback

- Compatibility impact: introduces two new wire contracts (`odyssey.reconnect.request`, `odyssey.reconnect.buffered_delta`, both `contractVersion` 1) — no prior version to migrate from; the fallback path reuses `ODY-S02-010`'s existing `odyssey.projection.snapshot` contract unchanged.
- Version fields affected: `None` at the application/package level.
- Migration or upcaster: `None`.
- Forward / backward behavior: `Not applicable` — no deployed clients depend on this new contract yet.
- Rollback method: revert this task's commits; nothing outside this task's own files and not-yet-started `ODY-S02-013` depends on it.
- Data-loss risk and protection: `None` — no persisted state; the delta buffer is in-memory, bounded, and scoped to a session's lifetime.
- Recovery rehearsal required: `No`.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: buffered move history (position/revision per entity, already-classified `Public`/`HiddenGameplay` content, `ODY-S02-010`).
- Trust boundaries: host (authoritative, owns the buffer and every audience's true `LastAcknowledgedSequence`) vs. each reconnecting client (submits only its own `UserId`, never a claimed acknowledgment position).
- Authorization / visibility checks: `ReconnectPlanner.Plan` rebuilds `ActorVisibilityContext` from the audience's current `SessionMember.Role` on every call — never from a value cached at disconnect time or when a buffered entry was recorded.
- Redaction requirements: a buffered entry about an entity the reconnecting audience can no longer see is excluded from catch-up entirely, not delivered with fields stripped (`TC-NET-024`).
- Log-safe fields: `None` new — no logging added by this task.
- Abuse / malformed input limits: wire codec bounded by 4096 bytes, rejects malformed/oversized/unsupported-contract payloads via `SerializationFailures`, matching every other production codec in this repository.
- Security tests: `TC-NET-024` (revoked visibility never leaks via catch-up) is this task's direct security-relevant regression test, extending `ODY-S02-011`'s `TC-NET-020` and `ODY-S02-007`'s (SP-04) hidden-data-boundary lineage to the reconnect path specifically.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: matches multiple explicit `PLANS.md` §1.2 triggers — changes more than one production module (`Odyssey.Application` and `Odyssey.Networking`); affects networking, security, and permissions directly (redaction-at-reconnect is a direct security property); required real design judgment (buffer capacity, offline-modeling approach, host- vs. client-tracked acknowledgment, snapshot-fallback position-fidelity gap) documented as five real decisions in the ExecPlan, plus one real bug found and fixed during implementation (destructive double-drain).
- ExecPlan path: `docs/plans/active/ODY-S02-012_Reconnect_Delta_Continuity_Duplicate_Delivery_Idempotency.md`
- Expected pull request count: 1 (single Draft PR covering all production code, tests, and registry updates).
- Milestone or sequencing constraints: depends on `ODY-S02-011` (merged); blocks `ODY-S02-013` (`SLICE-02_IMPLEMENTATION_BACKLOG.md` §6).

## 15. Documentation and versioning impact

- Documents that must change: this task contract, its ExecPlan, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md` (`ODY-S02-012` row only).
- Documents that must not change: `ADR-001`–`019`, `docs/tasks/SLICE-02_BACKLOG.md`, `docs/tasks/completed/*`, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, `docs/errors/ERROR_CODES.md` (no new `ErrorCode`), anything under `Documentation/`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: introduces two new wire contracts (`contractVersion` 1, new); reuses `ODY-S02-010`'s existing snapshot contract unchanged.
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

- `Packages/com.odyssey.application/Runtime/Networking/Reconnect/ReconnectContracts.cs` — new.
- `Packages/com.odyssey.application/Runtime/Networking/Reconnect/ReconnectWireCodec.cs` — new.
- `Packages/com.odyssey.networking/Runtime/Reconnect/ReconnectChannels.cs` — new.
- `DotNet/Tests/Odyssey.Tests.Networking/Reconnect/ReconnectServiceTests.cs`, `ReconnectTransportTests.cs` — new.
- `Tests/Metadata/test-catalog.json` — 4 new entries (`TC-NET-021`–`024`).
- `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md` — `ODY-S02-012` row status.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet/Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test DotNet/Tests/Odyssey.Tests.Networking/Odyssey.Tests.Networking.csproj --filter "FullyQualifiedName~Reconnect"` | Passed | 10/10 new tests, 0 failed (after fixing the destructive-double-drain bug found on first run — see ExecPlan §8). |
| `dotnet test DotNet/Odyssey.Core.sln` (full suite) | Passed | 199/199, 0 failed (1 Contracts + 1 Domain + 66 Networking [56 pre-existing + 10 new] + 84 Unit + 2 Architecture + 45 Persistence), including `RepositoryStructurePassesArchitectureGuard`. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS`. |
| `.\scripts\check-repository-policy.ps1` | Passed | All checks pass, including `REPO-POLICY-005`. |
| `.\scripts\verify-test-structure.ps1` | Passed | `TC-ARCH-001 PASS`, all `TC-ARCH-002` controlled-fixture checks pass; catalog cross-check for `TC-NET-021`–`024` resolves now that this task contract exists. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `ReconnectServiceTests.SessionDeltaBuffer_WithinCapacity_TryGetRangeSince_ReturnsAllMissedEntries`. |
| AC-2 | Passed | `ReconnectServiceTests.SessionDeltaBuffer_RangeExceedsCapacity_TryGetRangeSince_ReturnsFalse`. |
| AC-3 | Passed | `ReconnectServiceTests.SessionDeltaBuffer_AlreadyCaughtUp_TryGetRangeSince_ReturnsEmptySuccess`. |
| AC-4 | Passed | `ReconnectServiceTests.ReconnectPlanner_Plan_UnknownAudience_ReturnsTypedMemberNotFound`. |
| AC-5 | Passed | `ReconnectServiceTests.ReconnectPlanner_Plan_FiltersCatchupEntriesByCurrentVisibility_NotStale`. |
| AC-6 | Passed | `TC-NET-021`, `ReconnectTransportTests.Reconnect_WithinBuffer_ReceivesMissingDeltas_NoFullSnapshot`. |
| AC-7 | Passed | `TC-NET-022`, `ReconnectTransportTests.Reconnect_OutsideBuffer_ReceivesFullSnapshot_NoCatchupDeltas`. |
| AC-8 | Passed | `TC-NET-023`, `ReconnectTransportTests.RedeliveredSameBufferedDelta_IsNotAppliedTwice_OverRealTransport`. |
| AC-9 | Passed | `TC-NET-024`, `ReconnectTransportTests.Reconnect_AfterRoleRevokedWhileDisconnected_DoesNotDeliverNowInvisibleEntity`. |
| AC-10 | Passed | `ReconnectPlanner`'s only typed failure reuses `SessionAdmissionFailures.MemberNotFound` (`ODY-S02-009`, already registered); `git diff --stat` shows no change to `ErrorCodes.cs`/`ERROR_CODES.md`. |
| AC-11 | Passed | `git status --porcelain` shows only: `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md`, `DotNet/Tests/Odyssey.Tests.Networking/Reconnect/`, `Packages/com.odyssey.application/Runtime/Networking/Reconnect/`, `Packages/com.odyssey.networking/Runtime/Reconnect/`, this task contract, and its ExecPlan — all within §5's Allowed paths; no `ADR-015`–`019` or `ODY-S02-009`/`010`/`011` file touched. |
| AC-12 | Pending | Draft PR not yet opened; CI status to be confirmed. |

## 18. Blockers, risks, and open decisions

- Blocker: `None`.
- Open decision (deliberate, not a blocker): the snapshot-fallback path does not carry live position data — `ODY-S02-010`'s `ProjectionSnapshot`/`SceneEntity` were never extended with a position field, and this task must not edit that file. A future task reconciling `ODY-S02-010`'s identity model with `ODY-S02-011`'s mutable position model would need to decide how (or whether) to enrich `ProjectionSnapshot` itself, rather than this task inventing a parallel position-carrying snapshot type.
- Open decision (deliberate, not a blocker): `LastAcknowledgedSequence` is host-tracked only, updated exclusively by successful sends — if a real deployment's host process restarted mid-session, this bookkeeping (like `ODY-S02-011`'s command receipts) would be lost, forcing every reconnecting client onto the snapshot-fallback path. Acceptable for this prototype (no persistence anywhere else in this session either), but a real limitation a future persistence-integration task must resolve.
- Risk: none identified beyond what is already named as future scope (real transport reconnect, campaign persistence, snapshot position enrichment) in `SLICE-02_IMPLEMENTATION_BACKLOG.md` §5.
