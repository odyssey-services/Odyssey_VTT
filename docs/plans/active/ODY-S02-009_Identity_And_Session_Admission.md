# ODY-S02-009 — Identity & Session Admission

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s02-009-identity-and-session-admission`
**Pull request:** Not opened
**Last updated:** 2026-08-26 UTC

## 1. Purpose and user-visible outcome

Implements the first real (not test-only) piece of `SLICE-02`'s vertical slice: a host starts a session, a player joins by a human-typeable code, and the host assigns a role — roadmap §11.6 steps 1–3 — over the already-accepted `InProcessSessionTransport`. Unblocks `ODY-S02-010` (scene delivery), which needs an admitted, role-assigned actor to deliver a scene to.

## 2. Task contract

- Goal: real `Odyssey.Application`/`Odyssey.Networking` admission/lobby code (not the SP-04 harness's test-only types), a dev/mock identity provider per `ADR-018` §5, a minimal session directory per `06_Networking` §6.3, a Lobby state machine, and host-only role assignment restricted to `ADR-019`'s three baseline roles.
- Acceptance criteria: see task contract §9.
- Requirement IDs: `SLICE-02` (implementation), backlog `ODY-S02-009`.
- In scope: `Odyssey.Application.Networking.Session`, `Odyssey.Application.Identity.DevIdentityProvider`, `Odyssey.Networking.Session` adapter, tests, 5 new `ErrorCode`s, task contract, ExecPlan, backlog row update.
- Out of scope: scene delivery, command/delta handling, reconnect, real Supabase Auth, real network transport, `AssistantGM`/delegation.
- Required authorities: `SLICE-02_IMPLEMENTATION_BACKLOG.md` §5, `ADR-018` §5, `ADR-019` §5, `06_Networking...` §6.3/§37.1, `ADR-015`, `ADR-001` §6.6, `ADR-003` (canonical codecs), `ADR-004` (Result/Error), `ADR-008` (RNG boundary).
- Required validation commands: `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `dotnet build`/`dotnet test` on `DotNet/Odyssey.Core.sln`.

## 3. Current state

- `ODY-S02-008` merged; `SLICE-02_IMPLEMENTATION_BACKLOG.md` §5's `ODY-S02-009` boundary is the authoritative scope for this task.
- No admission/lobby/identity-assignment production code exists yet anywhere in the repository.
- `06_Networking...` §37.1 ("Новый approved user получает Observer preset") directly answers the default-role-on-admission design question — used, not invented.
- `ADR-019` §5.1/PERM-INV-001 §7.2 ("не может назначить другого MainGM") directly answers the role-assignment-restriction design question.
- `UserId`/`SessionId` have no `NewId()` factory (`ADR-018` §4: "externally assigned") — dev identities are fixed canonical literals; `SessionId` generation reuses the deterministic-hash pattern `InProcessSessionTransport` already established.
- `ADR-003` §3 requires hand-written canonical JSON codecs for production wire content, not reflection-based serialization — confirmed against `ManifestAndDiagnosticCodecs.cs`'s existing pattern; the SP-04 harness's `System.Text.Json` usage is explicitly test-only and not reusable here (`SLICE-02_IMPLEMENTATION_BACKLOG.md` §2.3).

## 4. Proposed approach

Split cleanly along `ADR-001` §6.6's boundary: pure, transport-independent admission logic (`SessionAdmissionService`) in `Odyssey.Application`, directly unit-testable; a thin `Odyssey.Networking` adapter that only encodes/decodes wire messages and drives the transport, making no admission decision itself. Reuse existing `SafeReasonCode`/`ErrorCategory` vocabulary for the four new failure modes (invalid code, capacity reached, role-assignment denied, member not found) plus one for the dev-identity provider — five new `ErrorCode` literals, zero new `SafeReasonCode`/`ErrorCategory` values. See the task contract and the code's own comments for full reasoning.

## 5. Milestones

### M1 — Pure admission logic, real and fully tested

- [x] `SessionAdmissionContracts.cs`, `SessionAdmissionService.cs` written in `Odyssey.Application.Networking.Session`.
- [x] `DevIdentityProvider.cs` written in `Odyssey.Application.Identity`.
- [x] 9 pure-logic tests pass (create/join/role-assign, all four rejection paths, idempotent re-join).

### M2 — Real wire codecs and transport adapter, exercised over InProcessSessionTransport

- [x] `SessionAdmissionWireCodecs.cs` written (hand-written canonical JSON, `ADR-003`-compliant).
- [x] `SessionAdmissionChannels.cs` written in `Odyssey.Networking.Session`.
- [x] 3 transport-level tests pass (full flow, invalid-code rejection, invalid-role rejection), all over real `InProcessSessionTransport` delivery.

### M3 — Registries, validation, task contract complete

- [x] 5 new `ErrorCode`s registered in `ERROR_CODES.md`/`test-catalog.json` (`TC-NET-007`–`011`).
- [ ] Full solution build/test green; `verify-format.ps1`/`check-repository-policy.ps1` pass.
- [ ] Task contract, backlog row, Draft PR.

## 6. Progress log

- 2026-08-26 — Preflight confirmed `ODY-S02-008` merged; branched cleanly.
- 2026-08-26 — Read `SLICE-02_IMPLEMENTATION_BACKLOG.md` §5, `ADR-018` §5, `ADR-019` §5, `06_Networking...` §6.3/§37.1, existing `ADR-003` codec patterns (`ManifestAndDiagnosticCodecs.cs`), `Odyssey.Application.Random`'s `RngContracts.cs` (determined not applicable to join-code generation — see Decisions).
- 2026-08-26 — Wrote pure admission logic, dev identity provider, wire codecs, transport adapter; fixed a `Result<T>.notnull` constraint violation (an early `Result<AdmissionOutcomeMessage?>` design) by switching `DrainOutcomes` to a list-returning API matching `DrainReliable`'s own contract.
- 2026-08-26 — All 37 `Odyssey.Tests.Networking` tests pass (22 pre-existing + 9 service + 3 transport + 3 dev-identity).
- 2026-08-26 — `RepositoryStructurePassesArchitectureGuard` failed once because the new `TC-NET-007`–`011` catalog entries referenced this task contract before it existed — resolved by writing this file, matching `ODY-S02-001`'s earlier precedent for the same validation-ordering requirement.

## 7. Decisions

- 2026-08-26 — Decision: join codes are generated via `RandomNumberGenerator` directly, not `Odyssey.Application.Random`'s authoritative/deterministic gameplay RNG stream. Rationale: that stream is drawIndex-based, designed for replayable dice/authoritative-decision RNG persisted in the event log; a join code is a local, opaque, non-gameplay access token — the same exemption `ADR-008` already grants `Guid`-derived identifiers elsewhere in this codebase. Authority: precedent recorded in `ODY-S01-007`'s own reasoning for `CampaignId`/`Guid.NewGuid()`.
- 2026-08-26 — Decision: re-joining with an already-admitted `UserId` is idempotent (returns the existing member, including its already-assigned role), not an error. Rationale: a real second connection attempt by the same dev/mock actor (e.g. a client retry) must not fork session state or duplicate membership; this is not `ODY-S02-012`'s reconnect flow (no delta/state resume here) — only "don't duplicate membership." Authority: this task's own explicit instruction to decide and test this behavior.
- 2026-08-26 — Decision: `MaxParticipants` is included in `SessionDirectoryEntry` even though the backlog's named minimal field subset was `SessionId`/`HostUserId`/`JoinCodeHash`/`Status` only. Rationale: the task's own required test scenario ("session заполнена") needs a capacity concept to be meaningful; `06_Networking` §6.4's already-established MVP cap of 12 is reused, not invented. Authority: `06_Networking...` §6.4; this task's own required test list.
- 2026-08-26 — Decision: wire messages use hand-written `CanonicalJsonWriter`/`JsonObjectReader` codecs, not `System.Text.Json`. Rationale: `ADR-003` §3 bans reflection-based/auto-mapping serialization for production wire content; the SP-04 harness's `System.Text.Json` usage was explicitly test-only-exempt, not a precedent for production code. Authority: `ADR-003` §3.

## 8. Discoveries and deviations

- Discovery: `Result<T>`'s `notnull` generic constraint rejects a nullable reference type argument (`AdmissionOutcomeMessage?`) even when only used as a "nothing received yet" sentinel — resolved by switching to a list-returning `DrainOutcomes` API, which is also more consistent with `ISessionTransport.DrainReliable`'s own established "never null, empty list means nothing yet" pattern.
- Discovery: `scripts/verify-test-structure.ps1`'s catalog cross-check requires a task contract file to exist before any `test-catalog.json` entry can reference its `taskId` — the same ordering requirement first encountered in `ODY-S02-001`; anticipated this time, but the validation step was still run and did fail once before this file existed, confirming the requirement is enforced consistently.

## 9. Validation and acceptance evidence

Recorded in the task contract's §17 once the full validation suite is run.

## 10. Recovery and rollback

Reverting this task's commits removes all new admission/identity code and tests with no compatibility or data-loss risk — nothing outside this task's own files depends on it yet (it is the first implementation task in `SLICE-02_IMPLEMENTATION_BACKLOG.md`).

## 11. Open questions and blockers

- No blockers.
- Deferred, not blocking: real Supabase Auth integration, scene delivery, reconnect, real transport — all explicitly out of this task's scope per `SLICE-02_IMPLEMENTATION_BACKLOG.md` §5.

## 12. Outcome and follow-up

Pending — this plan is updated to `Completed` once the task contract's remaining acceptance items are confirmed and the Draft PR is opened with green CI.
