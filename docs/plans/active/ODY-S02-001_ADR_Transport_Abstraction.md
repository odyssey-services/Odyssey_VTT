# ODY-S02-001 — ADR: Transport Abstraction

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s02-001-adr-transport-abstraction`
**Pull request:** Not opened
**Last updated:** 2026-08-25 UTC

## 1. Purpose and user-visible outcome

When complete, `SLICE-02`'s later tasks (`ODY-S02-002`–`004`) have an accepted `ISessionTransport` Application port, a defined `NetworkEnvelope`/`RealtimeEnvelope` wire-DTO shape, and a working in-process/mock implementation to write their own tests against — without needing to invent any of that by themselves mid-implementation. No user-visible product behavior changes; this is purely an architectural foundation task.

## 2. Task contract

- Goal: produce `docs/adr/ADR-015_Transport_Abstraction_v1.0.md` (Accepted), the `ISessionTransport` Application port + supporting DTOs, an in-process/mock transport implementation for tests, and this task's own contract tests — nothing more.
- Acceptance criteria: see task contract §9 (`docs/tasks/active/ODY-S02-001_ADR_Transport_Abstraction.md`).
- Requirement IDs: `SLICE-02` (prerequisite revision), backlog `ODY-S02-001`.
- In scope: ADR-015, `ISessionTransport`/`NetworkEnvelope`/`RealtimeEnvelope`/`ProtocolVersion(Range)`/`ConnectionHandle`/`SessionEndpoint`/`TransportTimeoutPolicy`/`NetworkingFailures` (Application), `InProcessSessionTransport` (Networking), `Odyssey.Networking.csproj`/`Odyssey.Tests.Networking.csproj`, the two Stage-3 guard flips, `MessageId` (Domain), 6 new `ErrorCodes`, this task's own contract tests.
- Out of scope: real network/relay transport (`ODY-S02-002`/`003`), snapshot/delta/reconnect protocol (`ODY-S02-004`), identity/permissions code, asset channel, any production caller of `ISessionTransport` beyond this task's own tests.
- Required authorities: `06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md` §4/§5/§10/§11, `ADR-001` §6.6, `ADR-004`, `ADR-008`, `ADR-011` (structural/version-dimension reference), `docs/tasks/SLICE-02_BACKLOG.md` §4.
- Required validation commands: `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `.\scripts\verify-test-structure.ps1`, `dotnet build DotNet/Odyssey.Core.sln`, `dotnet test DotNet/Odyssey.Core.sln`.

## 3. Current state

Observed facts (confirmed by `Read`/`grep`/build before writing production code):

- `SLICE-02_BACKLOG.md` on `main` (from `ODY-S02-000`, PR #37) lists `ODY-S02-001` as the first, dependency-free prerequisite task, boundary already fixed at "ISessionTransport signature, framing/handshake/version format decision, in-process/mock transport."
- `06_Networking_and_Session_Sync...` §4.3 already contains an illustrative `ISessionTransport` C# sketch (bare `Task`/`IAsyncEnumerable`, both channels present) — read in full, used as direct basis.
- `ADR-001` §6.6 already defines the `Odyssey.Networking` module boundary; no prior task had implemented anything against it (`ODY-S02-000` verified-facts).
- `Packages/com.odyssey.networking/` physically existed (from `ODY-S00-003` skeleton) with only `AssemblyMarker.cs` and an `.asmdef` — no real content.
- Two "Stage 3 gate" guards existed specifically to be flipped at this task: `ProjectContractTests.cs`'s `NetworkingBridgeProjectIsNotCreated` test, and `verify-test-structure.ps1`'s two `Odyssey.Networking.csproj`/`Odyssey.Tests.Networking` blocklist loops — both mirror exactly how `ODY-S01-007` flipped the equivalent Persistence guards.
- No `docs/adr/ADR_TEMPLATE.md` exists (confirmed by search); `ADR-011`'s own 17-section structure (v1.0 and v1.1) used as the format reference, per this task's own instruction.
- Next available ADR number is 015 (`docs/adr/` contains ADR-001 through ADR-014; `docs/adr/README.md` is stale at ADR-010 and intentionally not touched, out of scope).

## 4. Proposed approach

`ISessionTransport` is declared in `Odyssey.Application` (a port, per `ADR-001` §6.6) and implemented first by an in-process/mock transport in `Odyssey.Networking` (`InProcessSessionTransport`) — no real transport implementation is added by this task. All I/O operations return `Task<Result<T>>`/`Task<Result>` per `ADR-004`, departing deliberately from the product document's bare-`Task`/`IAsyncEnumerable` sketch; reads use synchronous `DrainReliable`/`DrainRealtime` polling instead of `IAsyncEnumerable`, since no real push-based transport exists yet to make that abstraction's semantics pull their weight. `ProtocolVersion` is a monotonic integer (not SemVer), matching `ADR-011` §6.1's existing version-dimension convention. See `ADR-015` §1, §4, §7, §8 for the full normative decision and its justification.

## 5. Milestones

### M1 — `ISessionTransport` port and DTOs compile cleanly

- [x] `MessageId` added to `Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs`.
- [x] `Packages/com.odyssey.application/Runtime/Networking/SessionTransportContracts.cs` written (interface + `ProtocolVersion`/`ProtocolVersionRange`/`SessionEndpoint`/`ConnectionHandle`/`NetworkMessageKind`/`NetworkEnvelope`/`RealtimeEnvelope`/`TransportTimeoutPolicy`/`NetworkingFailures`).
- [x] 6 new `ErrorCodes` entries added.
- Evidence: `dotnet build DotNet/Odyssey.Core.sln` succeeded with `SessionTransportContracts.cs` alone, 0 warnings/0 errors, before any Networking-side code was written.

### M2 — `Odyssey.Networking` becomes a real project with a working mock transport

- [x] `DotNet/Projects/Odyssey.Networking.csproj` created, added to `DotNet/Odyssey.Core.sln`.
- [x] `InProcessSessionTransport` written under `Packages/com.odyssey.networking/Runtime/InProcess/`.
- [x] `ADR-008` forbidden-global-API violation (`Task.Delay`-based latency simulation) found and removed before it could reach CI; cancellation implemented via a synchronous `CancellationToken.IsCancellationRequested` check instead.
- [x] Both Stage-3 guards flipped (`ProjectContractTests.cs`, `verify-test-structure.ps1`).
- Evidence: `dotnet build DotNet/Odyssey.Core.sln` — 0 warnings, 0 errors, `Odyssey.Networking.dll` produced alongside every other project.

### M3 — Contract tests prove the mock transport's send/receive, negotiation, timeout, and failure behavior

- [x] `DotNet/Tests/Odyssey.Tests.Networking/` created, added to `DotNet/Odyssey.Core.sln`.
- [x] `InProcessSessionTransportTests.cs` written: 12 tests covering protocol negotiation (success/failure), reliable send/drain ordering, realtime send/drain, send-before-connect (`NotConnected`), already-cancelled-token cancellation (`ConnectAsync` and `SendReliableAsync`), disconnect-then-send, and `ProtocolVersionRange` construction validation.
- Evidence: `dotnet test Tests/Odyssey.Tests.Networking/Odyssey.Tests.Networking.csproj` — 12/12 passed.

### M4 — ADR-015 accepted and task contract complete

- [x] `docs/adr/ADR-015_Transport_Abstraction_v1.0.md` written, mirroring `ADR-011`'s 17-section structure, Status `Accepted`.
- [ ] `docs/tasks/active/ODY-S02-001_ADR_Transport_Abstraction.md` (this plan's companion task contract) completed with real evidence.
- [ ] `docs/errors/ERROR_CODES.md` updated with the 6 new networking codes.
- [ ] `SLICE-02_BACKLOG.md`'s `ODY-S02-001` row updated (status only).
- [ ] Full validation suite run and recorded.
- [ ] Draft PR opened, CI green, task contract header updated with the PR link.

## 6. Progress log

- 2026-08-25 — `MessageId`, `SessionTransportContracts.cs`, `ErrorCodes.cs` additions written and confirmed compiling in isolation.
- 2026-08-25 — `Odyssey.Networking.csproj` created and added to the solution; `InProcessSessionTransport.cs` written (initially with a `Task.Delay`-based latency simulation).
- 2026-08-25 — Recognized the `Task.Delay` usage would trip `ADR-008`'s forbidden-global-API scan; removed it, converted `ConnectAsync` back to a synchronous `Task.FromResult`-returning method. Full-solution build re-confirmed clean (0 warnings, 0 errors) with `Odyssey.Networking` included.
- 2026-08-25 — `Odyssey.Tests.Networking.csproj` created and added to the solution; `InProcessSessionTransportTests.cs` written (12 tests). Two tests initially failed due to a test-authoring misunderstanding of `ConnectAsync`'s negotiation semantics (it negotiates the *called* instance's own local range against the passed-in range parameter, not against the paired peer's range) — fixed by calling `host.ConnectAsync(..., clientRange)` instead of `client.ConnectAsync(..., ownRange)`. All 12 tests pass after the fix.
- 2026-08-25 — `docs/adr/ADR-015_Transport_Abstraction_v1.0.md` written, mirroring `ADR-011`'s structure, Status `Accepted`.

## 7. Decisions

- 2026-08-25 — Decision: `ISessionTransport` uses `Task<Result<T>>`/`Task<Result>` for I/O and synchronous `DrainReliable`/`DrainRealtime` for reads, departing from `06_Networking...` §4.3's bare `Task`/`IAsyncEnumerable` sketch. Rationale: `ADR-004` forbids raw provider exceptions escaping a public API; no real push-based transport exists yet to justify `IAsyncEnumerable`'s backpressure/cancellation machinery. Authority: `ADR-004`; recorded in `ADR-015` §8.
- 2026-08-25 — Decision: `ProtocolVersion` is a monotonic positive integer, not SemVer. Rationale: matches the existing `CampaignFormatVersion`/`DatabaseSchemaVersion` convention (`ADR-011` §6.1); the transport wire format is a flat sequence of incompatible revisions, not a product feature with major/minor/patch semantics. Authority: `ADR-011` §6.1; recorded in `ADR-015` §7.1.
- 2026-08-25 — Decision: the realtime/unreliable preview channel is part of the baseline `ISessionTransport` contract from v1.0, not a future placeholder. Rationale: `06_Networking...` §4.3's own illustrative interface sketch already includes it. Authority: `06_Networking...` §4.3/§5.2; recorded in `ADR-015` §5.2.
- 2026-08-25 — Decision: `ADR-015`'s Status is `Accepted` immediately, unlike `ODY-S02-003` which must wait for `SP-03`/`SP-04` spike evidence. Rationale: this ADR only formalizes an interface signature already sketched in the accepted product document and already-accepted architectural ADRs (`ADR-001`, `ADR-004`, `ADR-011`) — it does not depend on any unresolved empirical question. Authority: recorded in `ADR-015` §17.

## 8. Discoveries and deviations

- Discovery: `scripts/verify-test-structure.ps1`'s `Test-ForbiddenGlobalApis` performs a blanket text-match scan (not semantic) across every `Packages/com.odyssey.*/Runtime` file, including the newly real `Odyssey.Networking` package — a `Task.Delay`-based test-support latency simulator in `InProcessSessionTransport.cs` would have been flagged. Removed before it reached validation; no scope change resulted (the removed feature was not part of this task's required minimum test list).
- Deviation (test-authoring only, not production code): initial contract tests for protocol-version negotiation called `ConnectAsync` on the wrong side of the `CreatePair` pair, producing a self-negotiation result instead of exercising true two-sided negotiation. Corrected; production code required no change.

## 9. Validation and acceptance evidence

Recorded in the companion task contract §10/§17 once the full validation suite is run (pending as of this update — `dotnet build`/`dotnet test` for the affected projects confirmed clean; full-suite scripted validation not yet run).

## 10. Recovery and rollback

Not applicable — this task introduces no persisted schema, no migration, and no production consumer of `ISessionTransport` beyond its own tests. Reverting this task's commits removes the ADR, the port, the mock implementation, and its tests with no data-loss or compatibility risk, since nothing outside this task's own branch depends on any of it yet.

## 11. Open questions and blockers

- `ADR-015` §12.1 (exact `TransportTimeoutPolicy` values) and §12.2 (`SessionEndpoint`'s full shape beyond `EndpointId`) remain open, deferred to `ODY-S02-002`/`003` per the ADR's own text — not a blocker for this task's own closure.
- No blockers.

## 12. Outcome and follow-up

Pending — this plan is updated to `Completed` once the task contract's remaining acceptance items (§9 in this file's Milestones) are confirmed and the Draft PR is opened with green CI.
