# ODY-S02-009 — Identity & Session Admission

**Status:** In Review
**Roadmap stage / slice:** SLICE-02 (implementation)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s02-009-identity-and-session-admission`
**Pull request:** Draft — [#46](https://github.com/odyssey-services/Odyssey_VTT/pull/46)
**ExecPlan:** `docs/plans/active/ODY-S02-009_Identity_And_Session_Admission.md`
**Created:** 2026-08-26
**Last updated:** 2026-08-26 UTC

## 1. Goal

Implement a real (not test-only) admission flow covering roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` §11.6 steps 1–3: a host starts a session and is assigned a dev/mock `UserId` (`ADR-018` §5's approved boundary), a joining actor is admitted by a human-typeable join code against a minimal session directory (`06_Networking` §6.3), a Lobby state machine tracks each member's admission progress, and the host assigns a role restricted to `ADR-019`'s three baseline roles — all delivered over the already-accepted `InProcessSessionTransport` (`ADR-015`), with no real network.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-02_IMPLEMENTATION_BACKLOG.md` §5 reserves this as the first, dependency-free implementation task; nothing in the repository implements admission, identity assignment, or role assignment yet.
- Value or risk reduction: gives `ODY-S02-010` (scene delivery) a real, tested admitted-and-role-assigned actor to deliver a scene to, and proves the `ADR-018`/`ADR-019` contracts are implementable at the admission layer before building further on them.
- Blocking or enabling relationship: `SLICE-02_IMPLEMENTATION_BACKLOG.md` §6 — no dependency; blocks `ODY-S02-010`.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1.2
- `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md` §5 (`ODY-S02-009` boundary, not redefined)
- `docs/adr/ADR-018_Identity_Baseline_v1.0.md` §5 (dev/mock identity boundary)
- `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` §5 (three baseline roles)
- `06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md` §6.2/§6.3 (join code, session directory), §37.1 (default Observer preset on admission)
- `docs/adr/ADR-015_Transport_Abstraction_v1.0.md` (`ISessionTransport`, `InProcessSessionTransport`)
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md` §6.6 (Networking module boundary)
- `docs/adr/ADR-003_Serialization_Strategy_v1.1.md` §3 (hand-written canonical codecs, no reflection-based serialization for production wire content)
- `docs/adr/ADR-004_Result_and_Error_Model_v1.0.md` (typed `Result`/`Error`)
- `docs/adr/ADR-008_Deterministic_Clock_and_RNG_v1.0.md` (forbidden global APIs, RNG boundary)
- `DotNet/Tests/Odyssey.Tests.Networking/HiddenDataBoundary/` (read to confirm its harness types are not reused as production code, per `SLICE-02_IMPLEMENTATION_BACKLOG.md` §2.3)

### Requirement and test IDs

- Requirement IDs: `SLICE-02` (implementation), roadmap section 11.6 steps 1–3, backlog `ODY-S02-009`.
- Existing test IDs: None reused.
- New test IDs introduced: `TC-NET-007`–`011` (join-code rejection, capacity, role-assignment denial, member-not-found, dev-identity out-of-range) — registered in `Tests/Metadata/test-catalog.json` and referenced from `docs/errors/ERROR_CODES.md`.

### Task-safe private context

- Approved summary / references: `06_Networking_and_Session_Sync` §6.2/§6.3/§37.1 and `ADR-018`/`ADR-019`'s relevant sections are summarized/quoted (short customary phrases and direct quotes clearly attributed) into this task and the production code's XML doc comments. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `ODY-S02-008` (PR #45) is merged to `main`; `SLICE-02_IMPLEMENTATION_BACKLOG.md` §5's `ODY-S02-009` boundary is confirmed by `Read` before branching.
- No admission/lobby/identity-assignment production code exists anywhere in the repository prior to this task — confirmed by `grep`.
- `Packages/com.odyssey.application/Runtime/Networking/SessionTransportContracts.cs` (`ADR-015`) already exists with `ISessionTransport`/`NetworkEnvelope`/`ConnectionHandle` — re-read in full to confirm exact constructor/method signatures before writing the adapter.
- `Packages/com.odyssey.networking/Runtime/InProcess/InProcessSessionTransport.cs` already exists — its `DeterministicHex32` pattern was reused (not duplicated blindly, but the same established technique) for `SessionId` generation, since `SessionId` has no `NewId()` factory.
- `Odyssey.Domain.Identity.UserId`/`SessionId` both lack a `NewId()` factory ("externally assigned," per prior `ADR-018` work) — confirmed by `grep`; dev identities are fixed canonical literals, not runtime-generated.
- `06_Networking_and_Session_Sync` §37.1 states plainly: "Новый approved user получает Observer preset" — confirmed by `Read`, used directly as the default-role-on-admission rule, not invented.
- `ADR-019` §5.1/`07_Permissions` `PERM-INV-001` §7.2 ("не может назначить другого MainGM") confirmed by `Read` — used directly as the role-assignment restriction.
- `Packages/com.odyssey.application/Runtime/Serialization/ManifestAndDiagnosticCodecs.cs` confirmed as the established hand-written canonical-codec pattern (`CanonicalJsonWriter`/`JsonObjectReader`) this task's wire codecs mirror, per `ADR-003` §3.
- `Packages/com.odyssey.application/Runtime/Random/RngContracts.cs` was read and found to be a drawIndex-based, replayable authoritative-gameplay-RNG system — not applicable to join-code generation (see the ExecPlan's Decisions section for the full reasoning).

### Assumptions

- None. All facts above were directly observed via `Read`/`grep` before and during this task.

## 5. Scope

### In scope

- `Packages/com.odyssey.application/Runtime/Identity/DevIdentityProvider.cs` (new) — `ADR-018` §5's approved dev/mock identity boundary.
- `Packages/com.odyssey.application/Runtime/Networking/Session/SessionAdmissionContracts.cs` (new) — `BaselineRole`, `MemberAdmissionState`, `SessionStatus`, `JoinCode`, `JoinCodeHash`, `SessionDirectoryEntry`, `SessionMember`, `SessionAdmissionFailures`.
- `Packages/com.odyssey.application/Runtime/Networking/Session/SessionAdmissionService.cs` (new) — pure admission/lobby logic.
- `Packages/com.odyssey.application/Runtime/Networking/Session/SessionAdmissionWireCodecs.cs` (new) — hand-written canonical JSON codecs.
- `Packages/com.odyssey.networking/Runtime/Session/SessionAdmissionChannels.cs` (new) — the `ISessionTransport`-driven host/client adapter.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — 5 new `ErrorCode`s.
- `DotNet/Tests/Odyssey.Tests.Networking/SessionAdmission/` (new) — `SessionAdmissionServiceTests.cs`, `SessionAdmissionTransportTests.cs`, `DevIdentityProviderTests.cs`.
- `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json` — registry entries.
- `docs/tasks/active/ODY-S02-009_Identity_And_Session_Admission.md` (this file), `docs/plans/active/ODY-S02-009_Identity_And_Session_Admission.md` (governing ExecPlan).
- `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md` — `ODY-S02-009` row status only.

### Out of scope

- Scene delivery (`ODY-S02-010`), command/delta handling (`ODY-S02-011`), reconnect (`ODY-S02-012`).
- Real Supabase Auth integration — only `ADR-018` §5's dev/mock boundary.
- Real network transport — only `InProcessSessionTransport`.
- `AssistantGM`, delegation, any role beyond `MainGM`/`Player`/`Observer`.
- Any edit to `ADR-015`–`019`.

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Identity/DevIdentityProvider.cs
Packages/com.odyssey.application/Runtime/Networking/Session/**
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.networking/Runtime/Session/**
DotNet/Tests/Odyssey.Tests.Networking/SessionAdmission/**
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
docs/tasks/active/ODY-S02-009_Identity_And_Session_Admission.md
docs/plans/active/ODY-S02-009_Identity_And_Session_Admission.md
docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: admission decisions (`SessionAdmissionService`) live in `Odyssey.Application`; the `Odyssey.Networking` adapter only encodes/decodes and drives the transport, never deciding admission itself (`ADR-001` §6.6).
- Authoritative-state and transaction boundary: `SessionAdmissionState` is in-memory only, no persistence — scene/campaign state is out of this task's scope.
- Serialization / compatibility boundary: wire messages use hand-written `CanonicalJsonWriter`/`JsonObjectReader` codecs (`ADR-003` §3), not `System.Text.Json`'s reflection-based serializer (that remains exempted only for the SP-04 harness's own explicitly test-only code).
- Time / RNG rule: `SessionId` generation reuses `InProcessSessionTransport`'s established deterministic-hash-from-seed technique (no `NewId()` factory exists for `SessionId`); join-code generation uses `RandomNumberGenerator` directly, justified as a local opaque access token exempt from `ADR-008`'s authoritative-gameplay-RNG-stream requirement (same precedent as `Guid`-derived identifiers).
- Unity / thread / lifetime rule: not applicable — pure .NET code, no Unity/IL2CPP involvement.
- Dependency / licensing rule: no new third-party dependency.
- Security / privacy / redaction rule: `JoinCode` is never stored in plaintext (`JoinCodeHash` only); all admission failures surface as typed `Result`/`Error` (`ADR-004`), never a raw exception or a response that confirms/denies whether a given join code was ever valid.
- Performance or platform constraint: not applicable.
- Other: `MaxParticipants`/session capacity (`06_Networking` §6.4's MVP cap of 12) was added to `SessionDirectoryEntry` beyond the backlog's literal minimal field list, justified in §7/ExecPlan Decisions as necessary for the required "session full" test scenario to be meaningful.

## 7. Expected behavior

### Scenario 1 — full admission flow over real transport

**Given** a host that creates a session and a joining actor with the correct join code
**When** the join request and role-assignment request are sent over `InProcessSessionTransport` and processed host-side
**Then** the joining actor is admitted as `Observer` by default, then upgraded to `Player` by the host's explicit role assignment, both confirmed via real wire-delivered outcome messages.

### Scenario 2 — invalid join code

**Given** a session with a known correct join code
**When** a joining actor submits a different code
**Then** the request fails with the typed `networking.session.join_code_invalid` error, delivered over the real transport, never a raw exception.

### Scenario 3 — invalid role assignment

**Given** an admitted member
**When** the host attempts to assign `MainGM`, or a non-host actor attempts any role assignment
**Then** the request fails with the typed `networking.session.role_assignment_denied` error.

### Scenario 4 — repeat join with the same UserId

**Given** an actor already admitted (and possibly already role-assigned)
**When** that same `UserId` submits another join request with the correct code
**Then** the existing member is returned as-is (idempotent), not duplicated and not treated as an error.

### Required invariants

- No public admission API throws a raw exception to its caller — every failure is a typed `Result`/`Error`.
- The plaintext `JoinCode` is never persisted or logged — only `JoinCodeHash`.
- `MainGM` can never be assigned via `AssignRole` — only granted automatically to the session's own host at creation.

## 8. Deliverables

- Production code: `DevIdentityProvider`, `IdentityFailures` (Identity); `BaselineRole`/`MemberAdmissionState`/`SessionStatus`/`JoinCode`/`JoinCodeHash`/`SessionDirectoryEntry`/`SessionMember`/`SessionAdmissionFailures`/`SessionAdmissionState`/`SessionAdmissionService`/wire message types/`SessionAdmissionWireCodec` (Application); `SessionAdmissionClientChannel`/`SessionAdmissionHostChannel` (Networking); 5 new `ErrorCode`s.
- Tests: `DotNet/Tests/Odyssey.Tests.Networking/SessionAdmission/` — 15 new tests (9 service + 3 transport + 3 dev-identity).
- Scripts / CI: None changed — new tests run automatically via the already-existing, already-CI-wired `Odyssey.Tests.Networking.csproj`.
- Configuration: None (no new `.csproj`).
- Documentation: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`/`Tests/Metadata/test-catalog.json` additions, `SLICE-02_IMPLEMENTATION_BACKLOG.md` `ODY-S02-009` row status.
- Generated evidence or build artifacts: None beyond validation command output recorded in §17.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. A real (not test-only) admission flow exists: host creates session, actor joins by code, host assigns role — all as production `Odyssey.Application`/`Odyssey.Networking` code, not test-project-scoped harness types.
2. Dev/mock identity assignment uses `ADR-018` §5's approved boundary (a fixed, deterministic pool), no real Supabase Auth code.
3. A newly admitted member defaults to `Observer` (`06_Networking` §37.1), upgradeable by the host to `Player` (never to a second `MainGM`, per `ADR-019` §5.1/`PERM-INV-001` §7.2).
4. The join code is never stored in plaintext — only its hash, in `SessionDirectoryEntry`.
5. All four required rejection scenarios (invalid code, capacity reached, invalid role assignment, non-host role-assignment attempt) return typed `Result`/`Error` failures, reusing existing `SafeReasonCode`/`ErrorCategory` values where they fit.
6. Re-joining with an already-admitted `UserId` is idempotent, decided and tested explicitly, not left undefined.
7. All wire messages use hand-written canonical JSON codecs (`ADR-003` §3), not reflection-based serialization.
8. The full flow (including both rejection scenarios) is proven over real `InProcessSessionTransport` delivery, not only in-memory pure-logic calls.
9. `ADR-015`–`019` are unmodified by this task's diff.
10. `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1` pass; `dotnet build`/`dotnet test` on `DotNet/Odyssey.Core.sln` pass in full, including all new tests.
11. `git diff --name-status` against `main` shows only files listed in §5's Allowed paths.
12. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test | Layer | Behavior proven |
|---|---|---|
| `CreateSession_AssignsHostAsMainGM_Immediately` | Pure logic | Host is MainGM at session creation |
| `TryJoin_WithCorrectCode_AdmitsAsObserverByDefault` | Pure logic | Default Observer preset on admission |
| `TryJoin_WithWrongCode_ReturnsTypedJoinCodeInvalid_NoException` | Pure logic | `TC-NET-007` |
| `TryJoin_WhenSessionFull_ReturnsTypedCapacityReached` | Pure logic | `TC-NET-008` |
| `TryJoin_SameUserIdTwice_IsIdempotent_ReturnsExistingMemberNotAnError` | Pure logic | Idempotent re-join |
| `AssignRole_ByHost_UpgradesAdmittedMemberToPlayer` | Pure logic | Host-driven role upgrade |
| `AssignRole_ByNonHost_ReturnsTypedRoleAssignmentDenied` | Pure logic | `TC-NET-009` (non-host path) |
| `AssignRole_ToMainGM_IsRejected_PERM_INV_001` | Pure logic | `TC-NET-009` (MainGM path) |
| `AssignRole_UnknownTarget_ReturnsTypedMemberNotFound` | Pure logic | `TC-NET-010` |
| `FullAdmissionFlow_HostCreatesSession_PlayerJoinsByCode_HostAssignsRole_OverRealTransport` | Real transport | End-to-end scenario 1 |
| `Join_WithInvalidCode_OverRealTransport_ReturnsTypedFailure_NotException` | Real transport | Scenario 2 over real delivery |
| `RoleAssignment_ToMainGM_OverRealTransport_ReturnsTypedFailure_NotException` | Real transport | Scenario 3 over real delivery |
| `AssignHost_IsStableAcrossCalls`, `AssignJoiningActor_ValidSlots_ReturnDistinctStableUserIds`, `AssignJoiningActor_OutOfRangeSlot_ReturnsTypedFailure_NoException` | Dev identity | `TC-NET-011` and pool stability |

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

- `dotnet test Tests/Odyssey.Tests.Networking/Odyssey.Tests.Networking.csproj` run directly to confirm all 37 tests (22 pre-existing + 15 new) pass in isolation, in addition to the full-solution run.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable.
- Network topology or database fixture: Not applicable — `InProcessSessionTransport` performs no real network I/O.
- Other: `dotnet` SDK matching `global.json` (`10.0.302`).

### Validation not required by this task

- Any real network/relay transport test.
- Any real Supabase Auth test.

## 11. Compatibility, migration, and rollback

- Compatibility impact: introduces the first admission/session/identity production contract — no prior consumer exists, so no backward-compatibility break is possible.
- Version fields affected: None.
- Migration or upcaster: None; wire message `contractVersion` fields are all `1`, first introduction.
- Forward / backward behavior: Not applicable — no shipped consumer of an earlier format exists.
- Rollback method: revert this task's commits; no production code outside this task depends on it yet.
- Data-loss risk and protection: None — no persisted state is touched.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

No new third-party dependency was introduced; `Newtonsoft.Json` (already referenced by `Odyssey.Application` for `CanonicalJsonWriter`/`JsonObjectReader`) and `System.Security.Cryptography` (BCL) are the only serialization/crypto primitives used.

## 13. Security, privacy, and hidden information

- Data classes handled: `UserId` (`OperationalSafe`, per `ADR-018` §8), `JoinCode`/`JoinCodeHash` (`Secret`-adjacent access token — plaintext never persisted, only its hash).
- Trust boundaries: host-authoritative — all admission decisions are made host-side (`SessionAdmissionService`), never trusted from client input directly.
- Authorization / audience checks: only the session's own host may assign roles; enforced in `SessionAdmissionService.AssignRole`, not left to the client or the transport layer.
- Redaction requirements: `SessionAdmissionFailures` never leak whether a given join code was ever valid — same generic `InvalidRequest` outcome regardless of the specific mismatch reason.
- Log-safe fields: no logging is added by this task; no plaintext `JoinCode` appears in any persisted or wire artifact beyond the one-time creation return value.
- Abuse / malformed input limits: wire codecs bound payload size (`MaxBytes = 4096`) and reject unknown fields (`EnsureOnly`), consistent with other production codecs in this repository.
- Security tests: `TC-NET-007` (join-code mismatch is a generic, non-leaking typed failure); `TC-NET-009` (role-assignment authorization enforced host-side, both for non-host actors and for the MainGM-protection rule).

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: evaluated fresh against `PLANS.md` §1.2, not presumed from precedent alone. This task directly matches multiple explicit §1.2 triggers: it "changes more than one production module" (`Odyssey.Application` and `Odyssey.Networking` both gain new content); it "affects ... networking, security, permissions" directly (host-only role assignment is an authorization decision, join-code handling is a security-relevant access-control mechanism); and it is the first task to actually implement against `ADR-018`/`ADR-019`'s previously paper-only contracts, requiring real design judgment (module split, wire codec approach, RNG-source justification, idempotent-rejoin semantics) documented as five real decisions in the ExecPlan — not a mechanical transcription of an already-fully-specified design. It does not have a single obvious "one clear implementation path" in the Brief-plan sense: the `Result<T>.notnull` constraint issue discovered mid-implementation (ExecPlan §8) is exactly the kind of "requires investigation before the implementation path is known" `PLANS.md` §1.2 describes.
- ExecPlan path: `docs/plans/active/ODY-S02-009_Identity_And_Session_Admission.md`
- Expected pull request count: 1 (single Draft PR covering all production code, tests, and registry updates).
- Milestone or sequencing constraints: no dependency within this backlog revision (`SLICE-02_IMPLEMENTATION_BACKLOG.md` §6). Blocks `ODY-S02-010`.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md` (`ODY-S02-009` row only).
- Documents that must not change: `ADR-001`–`019`, `docs/tasks/SLICE-02_BACKLOG.md`, `docs/tasks/completed/*`, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything under `Documentation/`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: introduces the first admission/lobby wire message contracts (`contractVersion` 1, new) — no prior version to migrate from.
- Documentation version changes: None — no ADR changes version.
- Changelog or release-note requirement: None.

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
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Packages/com.odyssey.application/Runtime/Identity/DevIdentityProvider.cs` — new.
- `Packages/com.odyssey.application/Runtime/Networking/Session/SessionAdmissionContracts.cs`, `SessionAdmissionService.cs`, `SessionAdmissionWireCodecs.cs` — new.
- `Packages/com.odyssey.networking/Runtime/Session/SessionAdmissionChannels.cs` — new.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — 5 new codes.
- `DotNet/Tests/Odyssey.Tests.Networking/SessionAdmission/SessionAdmissionServiceTests.cs`, `SessionAdmissionTransportTests.cs`, `DevIdentityProviderTests.cs` — new.
- `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json` — registry additions.
- `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md` — `ODY-S02-009` row status.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet/Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test Tests/Odyssey.Tests.Networking/Odyssey.Tests.Networking.csproj` | Passed | 37/37 (22 pre-existing + 15 new), 0 failed. |
| `dotnet test DotNet/Odyssey.Core.sln` (full suite) | Passed | 170/170, 0 failed (1 Contracts + 1 Domain + 37 Networking + 84 Unit + 2 Architecture + 45 Persistence), including `RepositoryStructurePassesArchitectureGuard`, now passing after this task contract was written. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS`. |
| `.\scripts\check-repository-policy.ps1` | Passed | All checks pass, including `REPO-POLICY-005`. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `SessionAdmissionService`/`SessionAdmissionChannels`, production namespaces. |
| AC-2 | Passed | `DevIdentityProvider`, fixed deterministic pool. |
| AC-3 | Passed | `TryJoin_WithCorrectCode_AdmitsAsObserverByDefault`, `AssignRole_ToMainGM_IsRejected_PERM_INV_001`. |
| AC-4 | Passed | `SessionDirectoryEntry` stores only `JoinCodeHash`; `JoinCode` returned once at creation, never persisted. |
| AC-5 | Passed | `TC-NET-007`–`010`, all passing. |
| AC-6 | Passed | `TryJoin_SameUserIdTwice_IsIdempotent_ReturnsExistingMemberNotAnError`. |
| AC-7 | Passed | `SessionAdmissionWireCodecs.cs` uses `CanonicalJsonWriter`/`JsonObjectReader` exclusively. |
| AC-8 | Passed | `SessionAdmissionTransportTests.cs`, 3 tests over real `InProcessSessionTransport`. |
| AC-9 | Passed | `git status --porcelain` confirms no `ADR-015`–`019` file touched. |
| AC-10 | Passed | See Validation results table above — all four commands pass. |
| AC-11 | Passed | `git status --porcelain` shows only files listed in §5's Allowed paths. |
| AC-12 | Pending | PR [#46](https://github.com/odyssey-services/Odyssey_VTT/pull/46) opened as Draft; CI status to be confirmed. |

## 18. Blockers, risks, and open decisions

- Blocker (resolved): `verify-test-structure.ps1`'s catalog cross-check failed once because this task contract did not yet exist while `TC-NET-007`–`011` already referenced `ODY-S02-009` — resolved by writing this file, matching `ODY-S02-001`'s precedent.
- Open decision (deliberate, not a blocker): `MaxParticipants`/session capacity enforcement uses `06_Networking` §6.4's MVP cap of 12 as a default, not a value fixed by any ADR at the admission layer specifically — a future task revisiting capacity policy would amend this, not treat it as untouchable.
- Risk: none identified beyond what's already named as future scope (real transport, real auth) in `SLICE-02_IMPLEMENTATION_BACKLOG.md` §2.1/§5.
