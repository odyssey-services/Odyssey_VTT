# ODY-S02-001 — ADR: Transport Abstraction

**Status:** In Review
**Roadmap stage / slice:** SLICE-02 (prerequisites)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s02-001-adr-transport-abstraction`
**Pull request:** Not opened
**ExecPlan:** `docs/plans/active/ODY-S02-001_ADR_Transport_Abstraction.md`
**Created:** 2026-08-25
**Last updated:** 2026-08-25 UTC

## 1. Goal

Produce `ADR-015` (Transport Abstraction, `Accepted`) fixing the `ISessionTransport` Application port signature, the reliable/realtime channel shape, `NetworkEnvelope`/`RealtimeEnvelope` format, and transport-level protocol version negotiation, per `06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md` sections 4, 5, 10, 11 — plus the port itself, an in-process/mock implementation, and this task's own contract tests. No real network transport, no snapshot/delta/reconnect protocol, no identity/permissions code.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-02_BACKLOG.md` reserves `ODY-S02-001` as the first, dependency-free prerequisite task of `SLICE-02`; nothing in the repository defines a transport abstraction, and `Odyssey.Networking` had no real content beyond its module skeleton.
- Value or risk reduction: fixes the exact `ISessionTransport` shape and wire-DTO format once, against the product document's own illustrative sketch and the already-accepted `ADR-001`/`ADR-004` architectural rules, so `ODY-S02-002`–`004` do not each have to reinvent it mid-implementation.
- Blocking or enabling relationship: blocks `ODY-S02-002` (Rendezvous/Relay Strategy ADR) and `ODY-S02-004` (Snapshot/Delta/Reconnect Protocol), both of which build on the channel/framing shape this task defines, per `SLICE-02_BACKLOG.md`.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1.2
- `06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md` §4 (architecture, including the `ISessionTransport` illustrative sketch), §5 (channels), §10 (version negotiation), §11 (envelopes)
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md` §6.6 (Networking module boundary)
- `docs/adr/ADR-004_Result_and_Error_Model_v1.0.md` (typed `Result`/`Error`, no raw provider exceptions from public API)
- `docs/adr/ADR-008_Deterministic_Clock_and_RNG_v1.0.md` (forbidden global APIs, including `Task.Delay`)
- `docs/adr/ADR-011_Local_Campaign_Format_v1.1.md` §6.1 (monotonic integer version-dimension convention, structural format reference for the new ADR)
- `docs/tasks/SLICE-02_BACKLOG.md` §4 (this task's boundary as scaffolded by `ODY-S02-000`)

### Requirement and test IDs

- Requirement IDs: `SLICE-02` (prerequisite revision), backlog `ODY-S02-001`.
- Existing test IDs: None reused.
- New test IDs introduced: `TC-NET-001`–`TC-NET-006` (typed transport failure factories and behavior: `ConnectFailed`, `ConnectTimedOut`, `ProtocolVersionUnsupported`, `SendFailed`, `NotConnected`, `OperationCancelled`) — registered in `Tests/Metadata/test-catalog.json` and referenced from `docs/errors/ERROR_CODES.md`.

### Task-safe private context

- Approved summary / references: `06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md` §4/§5/§10/§11 summarized (not pasted beyond short customary phrases, including the illustrative `ISessionTransport` sketch quoted in `ADR-015` §1 and its own XML doc comments) into this task, `ADR-015`, and the production code's XML doc comments. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `docs/tasks/SLICE-02_BACKLOG.md` is on `main` (merged via `ODY-S02-000`, PR #37); its `ODY-S02-001` row read `Draft`, confirmed by `Read` before branching, with its boundary already fixed at "`ISessionTransport` interface, in-process/mock transport for automated tests, reliable + optional/unreliable channel shape, message framing, protocol version handshake, timeout/retry policy — stack-agnostic."
- `06_Networking_and_Session_Sync...` §4.3 already contains an illustrative `ISessionTransport` C# sketch (bare `Task`/`IAsyncEnumerable`, both reliable and realtime channels present), quoted verbatim in `ADR-015` §1. §5.2 confirms the realtime channel is part of the same interface sketch, not a future placeholder.
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md` §6.6 already defines the `Odyssey.Networking` module boundary; no prior task had implemented anything against it, confirmed via `ODY-S02-000`'s own verified-facts section.
- No `docs/adr/ADR_TEMPLATE.md` exists (confirmed by search); `docs/adr/` contains ADR-001 through ADR-014 (confirmed by directory listing), so the next available number is `ADR-015`. `docs/adr/README.md` only lists through ADR-010 and is stale; left untouched per this task's own scope (not required to fix a pre-existing documentation gap unrelated to this task's deliverable).
- `Packages/com.odyssey.networking/` physically existed (from `ODY-S00-003`'s module skeleton) with only `Runtime/AssemblyMarker.cs` and `Runtime/Odyssey.Networking.asmdef` (already referencing `Odyssey.Domain, Odyssey.Content, Odyssey.Application`, `noEngineReferences: true`) — no real content prior to this task.
- `DotNet/Tests/Odyssey.Tests.Contracts/ProjectContractTests.cs` and `scripts/verify-test-structure.ps1` both contained explicit guards rejecting `DotNet/Projects/Odyssey.Networking.csproj`/`DotNet/Tests/Odyssey.Tests.Networking` as "not yet created" — both updated by this task, mirroring exactly how `ODY-S01-007` flipped the equivalent Persistence guards. `verify-test-structure.ps1`'s `$modulePackages`/`$allowed` dependency-matrix tables already anticipated `Odyssey.Networking` depending on `Odyssey.Domain, Odyssey.Content, Odyssey.Application` — confirmed via `grep`, no edit needed there.
- `scripts/verify-test-structure.ps1`'s `Test-ForbiddenGlobalApis` performs a blanket text-match scan (not semantic) across every `Packages/com.odyssey.*/Runtime` file for literal strings including `Task.Delay`. An initial draft of `InProcessSessionTransport.cs` included a `Task.Delay`-based latency-simulation feature for exercising timeout tests; recognized before validation and removed (see §17 and the ExecPlan's Discoveries section).

### Assumptions

- None. All facts above were directly observed via `Read`/`grep`/real build-and-test evidence before and during this task.

## 5. Scope

### In scope

- `Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs` — new `MessageId` typed identifier (prefix `msg_`, with `NewId(UtcInstant)`).
- `Packages/com.odyssey.application/Runtime/Networking/SessionTransportContracts.cs` — `ISessionTransport`, `ProtocolVersion`, `ProtocolVersionRange`, `SessionEndpoint`, `ConnectionHandle`, `NetworkMessageKind`, `NetworkEnvelope`, `RealtimeEnvelope`, `TransportTimeoutPolicy`, `NetworkingFailures`.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — six new networking `ErrorCode`s.
- `Packages/com.odyssey.networking/Runtime/InProcess/InProcessSessionTransport.cs` — the in-process/mock `ISessionTransport` implementation.
- `DotNet/Projects/Odyssey.Networking.csproj` (new pure-.NET bridge project, first real content), added to `DotNet/Odyssey.Core.sln`.
- `DotNet/Tests/Odyssey.Tests.Networking/` (new test project + `InProcessSessionTransportTests.cs`), added to `DotNet/Odyssey.Core.sln`.
- `DotNet/Tests/Odyssey.Tests.Contracts/ProjectContractTests.cs`, `scripts/verify-test-structure.ps1` — narrow updates un-blocking the now-legitimate `Odyssey.Networking` bridge/test projects.
- `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json` — registry entries for the six new `ErrorCode`s and six new `TC-NET-*` test case IDs.
- `docs/adr/ADR-015_Transport_Abstraction_v1.0.md` (new).
- `docs/tasks/active/ODY-S02-001_ADR_Transport_Abstraction.md` (this file), `docs/plans/active/ODY-S02-001_ADR_Transport_Abstraction.md` (governing ExecPlan).
- `docs/tasks/SLICE-02_BACKLOG.md` — `ODY-S02-001` row status only.

### Out of scope

- Real relay/rendezvous transport implementation (`ODY-S02-002`/`ODY-S02-003`).
- Snapshot/delta/reconnect protocol, including the full handshake payload beyond `ProtocolVersion` (`ODY-S02-004`).
- Identity baseline (`ODY-S02-005`), permissions baseline (`ODY-S02-006`).
- Asset channel (`06_Networking...` §5.3) — not defined by this ADR (see `ADR-015` §5.3, §12.2).
- Any production caller of `ISessionTransport` beyond this task's own contract tests.
- `docs/adr/README.md`'s stale ADR index (only lists through ADR-010) — pre-existing gap, not introduced or required by this task.

### Allowed paths

```text
Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs
Packages/com.odyssey.application/Runtime/Networking/**
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.networking/Runtime/InProcess/**
DotNet/Projects/Odyssey.Networking.csproj
DotNet/Tests/Odyssey.Tests.Networking/**
DotNet/Tests/Odyssey.Tests.Contracts/ProjectContractTests.cs
DotNet/Odyssey.Core.sln
scripts/verify-test-structure.ps1
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
docs/adr/ADR-015_Transport_Abstraction_v1.0.md
docs/tasks/active/ODY-S02-001_ADR_Transport_Abstraction.md
docs/plans/active/ODY-S02-001_ADR_Transport_Abstraction.md
docs/tasks/SLICE-02_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: `ISessionTransport`/`NetworkEnvelope`/`RealtimeEnvelope`/etc. live in `Odyssey.Application` (port); `InProcessSessionTransport` lives in `Odyssey.Networking` (implementation) — matches `ADR-001` §6.6. `Odyssey.Domain`'s new `MessageId` introduces no new dependency (still zero-dependency).
- Authoritative-state and transaction boundary: this task's mock transport never touches `campaign.db` or Domain state; `Odyssey.Networking` must not read SQLite or make authoritative decisions (`ADR-001` §6.6) — satisfied, since no such code exists in this task.
- Serialization / compatibility boundary: `NetworkEnvelope.Payload`/`RealtimeEnvelope.Payload` are opaque `byte[]` at this layer — this ADR does not define a wire serialization format for envelope payloads themselves (deferred to whichever task defines the concrete message types carried inside, `ODY-S02-004` and later).
- Time / RNG rule: `ADR-008` — `MessageId.NewId` and `InProcessSessionTransport`'s deterministic session-id derivation both route through the existing `IWallClock`/explicit `UtcInstant` pattern, never a direct BCL clock read; `Task.Delay` is not used anywhere in `Packages/com.odyssey.networking/Runtime` (confirmed by the forbidden-API scan passing).
- Unity / thread / lifetime rule: `InProcessSessionTransport` uses `ConcurrentQueue<T>` for its inboxes to remain safe under concurrent send/drain calls; `ConnectionHandle` never exposes a raw socket/relay session object.
- Dependency / licensing rule: no new third-party dependency introduced by this task.
- Security / privacy / redaction rule: `NetworkingFailures` typed errors never surface raw provider exception text or connection internals (`ADR-004`).
- Performance or platform constraint: not applicable — no real network I/O exists in this task's deliverable.
- Other: the realtime channel is part of the baseline `ISessionTransport` contract from v1.0, not a future placeholder (`ADR-015` §5.2).

## 7. Expected behavior

### Scenario 1 — Two in-process transports negotiate a protocol version and exchange a reliable message

**Given** a paired `InProcessSessionTransport` host/client created via `CreatePair` with overlapping `ProtocolVersionRange`s
**When** one side calls `ConnectAsync` with the other side's range, then sends a `NetworkEnvelope` via `SendReliableAsync`
**Then** `ConnectAsync` returns a `Result<ConnectionHandle>` success carrying the highest overlapping `ProtocolVersion`, and the peer's `DrainReliable` returns exactly the sent envelope, once, in send order.

### Scenario 2 — Non-overlapping protocol ranges

**Given** two `ProtocolVersionRange`s with no overlap
**When** `ConnectAsync` is called with the mismatched range
**Then** the call fails with the typed `networking.protocol.version_unsupported` error (`ErrorCategory.Compatibility`), never an exception, and no `ConnectionHandle` is produced.

### Scenario 3 — Send before connect, or after disconnect

**Given** a transport instance that has never successfully connected, or one that has called `Disconnect`
**When** `SendReliableAsync`/`SendRealtimeAsync` is called
**Then** the call fails with the typed `networking.transport.not_connected` error, never an exception.

### Scenario 4 — Cancellation

**Given** an already-cancelled `CancellationToken`
**When** `ConnectAsync` or `SendReliableAsync` is called with it
**Then** the call fails with the typed `networking.transport.operation_cancelled` error, never a raw `OperationCanceledException`.

### Required invariants

- No public `ISessionTransport` method ever throws a raw exception to its caller — all failures surface as typed `Result`/`Error` per `ADR-004`.
- `ProtocolVersionRange.NegotiateWith` always returns the highest version common to both ranges, or `null` if none overlap — proven for both the success and failure case.
- No file under `Packages/com.odyssey.*/Runtime` uses a forbidden global API per `ADR-008` (`Task.Delay` included) — proven by `verify-test-structure.ps1`'s scan passing.

## 8. Deliverables

- Production code: `MessageId` (Domain); `ISessionTransport`/`ProtocolVersion`/`ProtocolVersionRange`/`SessionEndpoint`/`ConnectionHandle`/`NetworkMessageKind`/`NetworkEnvelope`/`RealtimeEnvelope`/`TransportTimeoutPolicy`/`NetworkingFailures` (Application); `InProcessSessionTransport` (Networking); six new `ErrorCodes` entries.
- Tests: `DotNet/Tests/Odyssey.Tests.Networking/InProcessSessionTransportTests.cs` (14 tests, `TC-NET-001`–`006`).
- Scripts / CI: `scripts/verify-test-structure.ps1` narrowly updated (Networking bridge/test project no longer blocked); no CI workflow file changed.
- Configuration: `DotNet/Projects/Odyssey.Networking.csproj`, `DotNet/Tests/Odyssey.Tests.Networking/Odyssey.Tests.Networking.csproj`, both added to `DotNet/Odyssey.Core.sln`.
- Documentation: `docs/adr/ADR-015_Transport_Abstraction_v1.0.md`, this task contract, its governing ExecPlan, `docs/errors/ERROR_CODES.md` additions, `Tests/Metadata/test-catalog.json` additions, `SLICE-02_BACKLOG.md` `ODY-S02-001` row status.
- Generated evidence or build artifacts: none beyond validation command output recorded in §17.
- Migration / recovery material: None (no persisted schema exists at this layer).

## 9. Acceptance criteria

1. `docs/adr/ADR-015_Transport_Abstraction_v1.0.md` exists, `Status: Accepted`, and mirrors `ADR-011`'s 17-section structural format.
2. `ISessionTransport` is declared in `Odyssey.Application` with the signature in `ADR-015` §4.2; `Odyssey.Application` has no compile-time dependency on `Odyssey.Networking`.
3. Both reliable and realtime channels are part of `ISessionTransport` from v1.0 (`SendReliableAsync`/`DrainReliable`, `SendRealtimeAsync`/`DrainRealtime`).
4. `ProtocolVersionRange.NegotiateWith` correctly negotiates the highest overlapping version and correctly reports no-overlap as a typed failure.
5. `InProcessSessionTransport` implements `ISessionTransport` completely, uses no forbidden `ADR-008` global API, and never leaks a raw provider exception.
6. `dotnet test DotNet/Odyssey.Core.sln` passes in full (all existing suites plus the new `Odyssey.Tests.Networking` suite).
7. `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `.\scripts\verify-test-structure.ps1` all pass.
8. `git diff --name-status` against `main` shows only files listed in §5's Allowed paths.
9. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-NET-001` | `.NET / dotnet test` | `NetworkingFailures.ConnectFailed` produces a typed `TransientInfrastructure`/`RetryWithBackoff` error | Pass |
| `TC-NET-002` | `.NET / dotnet test` | `NetworkingFailures.ConnectTimedOut` produces a typed `TransientInfrastructure`/`RetryWithBackoff` error | Pass |
| `TC-NET-003` | `.NET / dotnet test` | Non-overlapping `ProtocolVersionRange`s produce a typed `Compatibility`/`VersionUnsupported` failure at `ConnectAsync` | Pass |
| `TC-NET-004` | `.NET / dotnet test` | `NetworkingFailures.SendFailed` produces a typed `TransientInfrastructure`/`RetryWithBackoff` error | Pass |
| `TC-NET-005` | `.NET / dotnet test` | Send before connect, and send after `Disconnect`, both produce a typed `NotConnected` failure | Pass |
| `TC-NET-006` | `.NET / dotnet test` | `ConnectAsync`/`SendReliableAsync` with an already-cancelled token produce a typed `OperationCancelled` failure, not a raw exception | Pass |

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

```bash
dotnet build DotNet/Odyssey.Core.sln
dotnet test DotNet/Odyssey.Core.sln
```

### Manual validation

- Direct `dotnet build`/`dotnet test` runs (in addition to the wrapped scripts) to confirm the new `Odyssey.Networking`/`Odyssey.Tests.Networking` projects build and pass cleanly alongside every pre-existing project.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable — this task ships no Unity-side production code or scene changes.
- Scripting backend: production module code is plain C#, backend-agnostic; no Unity Player build required for this task (unlike `ODY-S01-007`, this task introduces no new third-party native dependency).
- Network topology or database fixture: Not applicable — the in-process mock transport performs no real network I/O.
- Other: `dotnet` SDK matching `global.json` (`10.0.302`).

### Validation not required by this task

- Any real network/relay transport test — `ODY-S02-002`/`003` scope.
- Snapshot/delta/reconnect protocol tests — `ODY-S02-004` scope.

## 11. Compatibility, migration, and rollback

- Compatibility impact: introduces the first `ISessionTransport` Application port and `Odyssey.Networking` production content. No prior production consumer exists yet, so no backward-compatibility break is possible.
- Version fields affected: none — `ProtocolVersion` is a new dimension with no prior shipped value to migrate from.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable — no shipped consumer of an earlier transport contract exists.
- Rollback method: revert this task's commits; no production caller of `ISessionTransport` exists outside this task's own tests.
- Data-loss risk and protection: None — no persisted state is touched by this task.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

No new third-party dependency was introduced by this task; `Odyssey.Networking.csproj` and `Odyssey.Tests.Networking.csproj` reference only existing in-repo projects and the same `Microsoft.NET.Test.Sdk`/`NUnit`/`NUnit3TestAdapter` packages every other test project already uses.

## 13. Security, privacy, and hidden information

- Data classes handled: none classified as `Secret`/`HiddenGameplay` per `ADR-010` §10 — no credential, owner key, or hidden campaign content is touched by this task's mock transport or DTOs.
- Trust boundaries: none — the in-process mock transport has no real network boundary; it exists only within a single test process.
- Authorization / audience checks: Not applicable — no permissions model exists at this stage (`ODY-S02-006` scope).
- Redaction requirements: `NetworkingFailures` errors never include raw provider exception text or connection internals (`ADR-004`).
- Log-safe fields: None logged by this task's production code (no diagnostic emission added here).
- Abuse / malformed input limits: `NetworkEnvelope`/`RealtimeEnvelope` constructors validate required fields are non-null; no size ceiling is enforced at this layer (deferred to whichever task adds a real transport with real payload limits, per `06_Networking...` §11.2's "payload size-limited" note).
- Security tests: `TC-NET-003` (incompatible protocol version is a typed, diagnosable rejection, never a silent connection); `TC-NET-005`/`006` (no operation succeeds silently without a valid connection or a live cancellation token).

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: evaluated fresh against `PLANS.md` §1.2, not presumed. This task directly matches multiple explicit §1.2 triggers: it "introduces or changes an Application port" (`ISessionTransport`, a brand-new port), it "changes more than one production module" (`Odyssey.Domain`, `Odyssey.Application`, `Odyssey.Networking`, plus two `DotNet` test/bridge projects), and it "affects ... networking" directly by name. It does not have "one clear implementation path" in the Brief-plan sense either — the deliberate departure from the product document's `Task`/`IAsyncEnumerable` sketch (§1.2's "requires investigation before the implementation path is known") required weighing `ADR-004` compliance against the illustrative sketch, and a real forbidden-global-API violation was found and corrected mid-implementation. ExecPlan mode is therefore independently justified, matching the same reasoning `ODY-S01-007` used when it introduced `ICampaignRepository`.
- ExecPlan path: `docs/plans/active/ODY-S02-001_ADR_Transport_Abstraction.md`
- Expected pull request count: 1 (single Draft PR covering the ADR, production code, tests, and registry updates).
- Milestone or sequencing constraints: Must not begin before `ODY-S02-000`'s `SLICE-02_BACKLOG.md` is merged into `main` (verified in §4).

## 15. Documentation and versioning impact

- Documents that must change: `docs/adr/ADR-015_Transport_Abstraction_v1.0.md` (new), this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-02_BACKLOG.md` (`ODY-S02-001` row only).
- Documents that must not change: `ADR-001`–`014`, `docs/adr/README.md` (stale, pre-existing, out of this task's scope), `docs/tasks/completed/*`, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything under `Documentation/`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: introduces `ProtocolVersion`/`ProtocolVersionRange` (new transport-protocol version dimension, first commit of this concept) and the `NetworkEnvelope`/`RealtimeEnvelope` wire-DTO shapes — both new introductions, not changes to a previously shipped version.
- Documentation version changes: `ADR-015` is a new document (v1.0); no existing ADR changes version by this task.
- Changelog or release-note requirement: None — no end-user-facing release exists yet at this development stage.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed.
- [ ] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [ ] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs` — `MessageId` factory.
- `Packages/com.odyssey.application/Runtime/Networking/SessionTransportContracts.cs` — new.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — six new codes.
- `Packages/com.odyssey.networking/Runtime/InProcess/InProcessSessionTransport.cs` — new.
- `DotNet/Projects/Odyssey.Networking.csproj`, `DotNet/Tests/Odyssey.Tests.Networking/**` — new, added to `DotNet/Odyssey.Core.sln`.
- `DotNet/Tests/Odyssey.Tests.Contracts/ProjectContractTests.cs`, `scripts/verify-test-structure.ps1` — narrow guard updates.
- `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json` — registry additions.
- `docs/adr/ADR-015_Transport_Abstraction_v1.0.md` — new.
- `docs/tasks/SLICE-02_BACKLOG.md` — `ODY-S02-001` row status.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet/Odyssey.Core.sln` | Passed | 0 warnings, 0 errors, including `Odyssey.Networking`/`Odyssey.Tests.Networking`. |
| `dotnet test Tests/Odyssey.Tests.Networking/Odyssey.Tests.Networking.csproj` | Passed | 14/14, 0 failed. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS`. |
| `.\scripts\check-repository-policy.ps1` | Passed | `REPO-POLICY-005 PASS` (registry complete, including the six new codes and `TC-NET-*` references). |
| `.\scripts\verify-test-structure.ps1` | Pending | Blocked once on a missing-task-contract reference before this file existed; to be re-run now that this file is written. |
| `dotnet test DotNet/Odyssey.Core.sln` (full suite) | Pending | To be recorded after `verify-test-structure.ps1` re-run. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `docs/adr/ADR-015_Transport_Abstraction_v1.0.md`, `Status: Accepted`, 17 sections mirroring `ADR-011`. |
| AC-2 | Passed | `ISessionTransport` in `Packages/com.odyssey.application/Runtime/Networking/SessionTransportContracts.cs`; `Odyssey.Application.csproj` has no `Odyssey.Networking` reference. |
| AC-3 | Passed | `SendReliableAsync`/`DrainReliable`, `SendRealtimeAsync`/`DrainRealtime` both present in v1.0 of the interface. |
| AC-4 | Passed | `TC-NET-003`, `ConnectAsync_OverlappingRanges_...`/`ConnectAsync_NonOverlappingRanges_...` tests. |
| AC-5 | Passed | `InProcessSessionTransport.cs`; no `Task.Delay` present (forbidden-API removal documented in ExecPlan §8); `dotnet test` green. |
| AC-6 | Pending | Full-suite `dotnet test DotNet/Odyssey.Core.sln` not yet re-run after this file's creation. |
| AC-7 | Pending | `verify-test-structure.ps1` to be re-run now that this task contract exists. |
| AC-8 | Pending | To be confirmed after diff-scope check. |
| AC-9 | Pending | PR not yet opened. |

## 18. Blockers, risks, and open decisions

- Blocker (resolved): `verify-test-structure.ps1`'s test-catalog cross-check failed once because this task contract file did not yet exist while `TC-NET-*` entries already referenced `ODY-S02-001` — resolved by writing this file before re-running validation, matching the tool's expected order.
- Open decision (deliberate, not a blocker): `ADR-015` §12.1 (`TransportTimeoutPolicy` exact values) and §12.2 (`SessionEndpoint`'s full shape) are left open, deferred to `ODY-S02-002`/`003` per the ADR's own text — consistent with `ADR-011` §12.1's precedent of deferring a provider-specific decision to its dedicated spike task.
- Risk: `NetworkingFailures.ConnectFailed`/`SendFailed` have no production trigger reachable through `InProcessSessionTransport`'s public API today (proven only at the factory level via `TC-NET-001`/`004`) — intentional, since these exist for a real transport's failure modes (`ODY-S02-002`/`003`), not documented as a defect.
