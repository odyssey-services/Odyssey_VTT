# ODY-S00-005 - Command, Event, Clock and RNG Contracts

**Status:** In Review
**Roadmap stage / slice:** SLICE-00
**Owner:** Codex
**Requested by:** Product owner
**Branch:** `feat/ody-s00-005-command-event-clock-rng-primitives`
**Pull request:** Not opened
**ExecPlan:** `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
**Created:** 2026-08-10
**Last updated:** 2026-08-11 02:12 UTC

## 1. Goal

Create the minimum deterministic command, event, clock, scheduler, and RNG contracts needed by SLICE-00 M1, with one synthetic in-memory test operation proving accepted, rejected, duplicate/idempotent, ordered-event, clock, and RNG behavior.

This task does not introduce SQLite, network transports, serialization DTOs, runtime composition, diagnostics runtime, gameplay features, Unity UI behavior, build identity, CI, or Player builds.

## 2. Why this task exists

- Problem or dependency being addressed: ODY-S00-004 created primitive identity/version/result contracts, but SLICE-00 still lacks the accepted command/event/idempotency and deterministic clock/RNG contracts required before runtime composition and serialization work.
- Value or risk reduction: Prevents later tasks from inventing ad hoc command envelopes, duplicate behavior, mutable event records, global clocks, or global random streams.
- Blocking or enabling relationship: Depends on owner-merged `ODY-S00-004`; blocks `ODY-S00-006`, `ODY-S00-007`, and final SLICE-00 M1 acceptance evidence.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.8.md`
- `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.3.md`, applicable module, command/event, clock/RNG, .NET, testing, and repository-command sections
- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/TASK_TEMPLATE.md`
- `docs/tasks/SLICE-00_BACKLOG.md`
- `docs/tasks/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
- `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
- `docs/tasks/completed/ODY-S00-004_Identity_Version_and_Result_Primitives.md`
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`
- `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md`
- `docs/adr/ADR-003_Serialization_Strategy_v1.0.md`, command fingerprint and DTO non-goals only
- `docs/adr/ADR-004_Result_and_Error_Model_v1.0.md`
- `docs/adr/ADR-006_Test_Project_Structure_and_Dual_Unity_DotNet_Compilation_v1.0.md`
- `docs/adr/ADR-008_Deterministic_Clock_and_RNG_v1.0.md`
- `docs/adr/ADR-009_Unity_Project_and_Build_Baseline_v1.1.md`, existing Unity/package baseline only
- `docs/adr/ADR-010_Logging_Diagnostics_and_Redaction_v1.0.md`, diagnostic IDs and RNG secret redaction constraints only

### Requirement and test IDs

- Requirement IDs: `SLICE-00`, Milestone `M1`, PR-003B delivery group
- Existing test IDs: `TC-ARCH-001`, `TC-ARCH-002`, `TC-DOTNET-001`, `TC-UNITY-ASM-001`, `TC-UNITY-TEST-001`, `TC-REPO-001`, `TC-RESULT-001` through `TC-RESULT-004`
- New test IDs to introduce: `TC-CMD-001`, `TC-CMD-002`, `TC-CMD-003`, `TC-CMD-004`, `TC-EVENT-001`, `TC-CLOCK-001`, `TC-CLOCK-002`, `TC-RNG-001`, `TC-RNG-002`, `TC-RNG-003`, `TC-RNG-004`

### Task-safe private context

- Approved summary / references: Build only the public-safe deterministic Core contracts required by the technical skeleton. Do not copy private product documents, hidden campaign content, local handoff text, secrets, personal paths, or private task bundles into repository artifacts.

## 4. Verified current state

### Verified facts

- PR #8 for ODY-S00-004 was merged into `main`; local `main` fast-forwarded from `8616246` to `4fb20e9`.
- Current task branch is `feat/ody-s00-005-command-event-clock-rng-primitives`.
- The repository contains the ODY-S00-004 Application identity primitives, Application Result/Error primitives, Application/Rules/Content version primitives, and `docs/errors/ERROR_CODES.md`.
- `DotNet/Odyssey.Core.sln` exists and compiles Domain, Rules, Content, and Application bridge projects from package source.
- Existing repository scripts include restore, format, test structure, fast tests, Unity tests, repository verification, and repository policy checks.
- The parent backlog defines ODY-S00-005 as PR-003B: "Command, Event, Clock and RNG Contracts" with the primary result "Deterministic test operation, idempotency contracts, virtual time and RNG vectors."
- The parent ExecPlan M3 requires one synthetic command operation to exercise accepted/rejected/duplicate behavior and ordered events, plus deterministic virtual clocks/scheduler and authoritative RNG vectors without global APIs.

### Assumptions

- No new owner decision is required for the minimal contracts explicitly described by ADR-002 and ADR-008 when implemented as in-memory/test-only scaffolding without SQLite, network, gameplay, serialization DTOs, or runtime composition.
- If implementation discovers that a command/event/clock/RNG type lacks clear ownership, required fields, or safe scope under ADR-001/002/008, the task stops and records the missing decision before adding that type.

## 5. Scope

### In scope

- Application-owned command identity and envelope primitives required by ADR-002 for state-changing Application commands:
  - `CommandId`
  - `CommandType`
  - `CommandVersion`
  - `CommandFingerprint`
  - `ApplicationCommand`
  - `CommandResult`
  - `CommandResultStatus` with exactly `Accepted`, `Pending`, and `Rejected`
  - `RootCommandId`
  - optional `ParentCommandId`
- Domain-owned immutable event identity/envelope primitives required by ADR-002:
  - `DomainEventId`
  - `DomainEventType`
  - `DomainEventVersion`
  - `DomainEvent`
  - ordered `DomainEventBatch`
  - `TransactionId`
  - `CausationCommandId`
- Application-owned ports and minimal in-memory test contracts needed to prove one command transaction without a real database or network transport:
  - command receipt lookup/store abstraction;
  - application transaction boundary abstraction;
  - event append/outbox projection abstraction only as in-memory test-facing contracts;
  - command dispatcher/gateway shape sufficient for the synthetic operation.
- Deterministic duplicate handling for the synthetic operation:
  - same `CommandId` and same fingerprint returns the stored result;
  - duplicate does not call the command handler, clock sampling for new events, or RNG again;
  - same `CommandId` with different semantic fingerprint returns a safe rejection/failure without leaking the stored result.
- One synthetic, non-gameplay test command operation under test scope to prove `Accepted`, `Rejected`, duplicate replay, mismatch, ordered events, and result envelope behavior.
- Application-owned clock/scheduler contracts and deterministic test doubles required by ADR-008:
  - `IWallClock`
  - `UtcInstant`
  - `IMonotonicClock`
  - `MonotonicInstant`
  - `IDelayScheduler`
  - fixed/manual/virtual test implementations where they stay test-only unless ADR-008 requires production interfaces.
- Application-owned RNG contracts and deterministic production algorithm scaffolding required by ADR-008:
  - campaign RNG key/epoch value contracts without persistence storage;
  - HMAC-SHA-256 stream derivation v1;
  - xoshiro256** v1;
  - rejection sampling for inclusive integer ranges without modulo bias;
  - non-secret `RngProofData`;
  - deterministic contract vectors.
- Architecture/repository checks preventing global time/random APIs in authoritative Core packages:
  - no `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now`, `DateTimeOffset.UtcNow`, `Stopwatch`, `Environment.TickCount`, `Task.Delay`, `System.Random`, `UnityEngine.Time`, or `UnityEngine.Random` in Domain, Rules, Content, Application, Persistence, or Networking production paths except explicitly approved adapter paths.
- Updates to `Tests/Metadata/test-catalog.json` for ODY-S00-005 TestCase IDs.
- Updates to task Completion Evidence, parent ExecPlan progress/evidence, and status references needed for PR-003B.

### Out of scope

- SQLite provider selection, SQLite schema, durable `AppliedCommands`, real database transactions, migrations, persistence integration, crash recovery, or durable outbox implementation.
- Network transports, relay, session sync, authentication, authorization runtime, E2EE, transport DTOs, or audience-filtered network projections.
- ADR-003 serialization DTOs, source-generated JSON contexts, canonical JSON fingerprints, upcasters, persisted event payload format, `.odcamp` physical implementation, or IL2CPP serialization/AOT spike.
- Runtime composition, Developer Shell, diagnostic runtime, log sinks, redaction runtime, crash markers, lifecycle wiring, Unity UI, scene behavior, or Player build.
- Gameplay commands, map/tokens/combat/dice UI/characters/content tools/audio/chat behavior, real campaign state, or product rules.
- WorldClock aggregate implementation or game-time mechanics beyond enforcing separation from host wall clock.
- Production generation policy for IDs beyond deterministic parse/format/value semantics unless ADR-002 or ADR-008 explicitly requires it for the synthetic operation.
- Separate Core `IdempotencyKey`; ADR-002 defines `CommandId` as the canonical idempotency key.
- New production modules, new test projects, `Common`/`Shared`/`Utils` modules, third-party dependencies, GitHub Actions, ADR changes, Technical Baseline changes, Unity package/version changes, ProjectSettings changes, or application/schema/format/contract/protocol/ruleset version bumps.

### Allowed paths

```text
Packages/com.odyssey.domain/Runtime/**
Packages/com.odyssey.application/Runtime/**
DotNet/Tests/Odyssey.Tests.Unit/**
DotNet/Tests/Odyssey.Tests.Domain/**
DotNet/Tests/Odyssey.Tests.Contracts/**
DotNet/Tests/Odyssey.Tests.Architecture/**
Assets/Odyssey/Client/Tests/EditMode/**
Assets/Odyssey/Client/Tests/PlayMode/**
Tests/Metadata/test-catalog.json
Tests/Vectors/**
docs/tasks/active/ODY-S00-005_Command_Event_Clock_and_RNG_Contracts.md
docs/tasks/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md
docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md
docs/tasks/SLICE-00_BACKLOG.md
scripts/verify-test-structure.ps1
scripts/test-fast.ps1
scripts/test-unity.ps1
scripts/verify-repository.ps1
scripts/check-repository-policy.ps1
README.md
```

`Packages/com.odyssey.rules/Runtime/**` and `Packages/com.odyssey.content/Runtime/**` may be edited only if required to prove that Rules/Content do not use global time/random APIs or to consume explicit RNG outcomes through already-approved boundaries. They do not authorize rules formulas, content definitions, content execution, or package publishing behavior.

### Paths requiring explicit approval before editing

```text
docs/adr/**
TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v*.md
ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v*.md
Packages/manifest.json
Packages/packages-lock.json
ProjectSettings/**
DotNet/Odyssey.Core.sln
DotNet/Projects/**
Packages/com.odyssey.persistence/**
Packages/com.odyssey.networking/**
Assets/Odyssey/Client/Runtime/**
Assets/Odyssey/Client/Editor/**
.github/**
version.json
config/compatibility.json
```

Owner approval for this activation step permits only the operational `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.8.md` active-task pointer update from ODY-S00-004 to ODY-S00-005. Further edits to paths listed above require separate approval.

## 6. Technical constraints

- Module ownership and dependency direction: Follow ADR-001 exactly. Domain owns DomainEvent identity/envelope/value meaning. Application owns command contracts, command orchestration ports, Result/Error use, clock/RNG ports, and use-case coordination. Do not create `Common`, `Shared`, `Utils`, or another production module.
- Authoritative-state and transaction boundary: Follow ADR-002. One state-changing command has one root transaction boundary. Command handlers must not call other command handlers. `CommandId` is the canonical idempotency key. `Accepted`, `Pending`, and `Rejected` are durable command outcomes; technical failure before a durable outcome is outer `Result` failure.
- Event immutability: DomainEvents are immutable facts named in the past tense. Tests must reject update/delete semantics and prove ordered event batches for the synthetic operation.
- Serialization / compatibility boundary: Do not create ADR-003 DTOs or persisted formats in this task. `CommandFingerprint` may be a stable in-memory/test abstraction; canonical JSON/fingerprint serialization belongs to ODY-S00-007 unless this task records an explicit owner decision.
- Time / RNG rule: Follow ADR-008. Authoritative Core code must not call global wall-clock, monotonic, delay, or random APIs. Use injected `IWallClock`, `IMonotonicClock`, scheduler, and RNG contracts. Duplicate command handling and event replay must not reroll or sample fresh event time.
- RNG secret rule: Raw campaign RNG key material must never be logged, serialized to public projections, stored in test snapshots as production data, or included in `RngProofData`. Test vectors may use explicit synthetic keys marked as fixtures.
- Unity / thread / lifetime rule: Core contracts must not depend on UnityEngine, scene lifecycle, MonoBehaviour, ScriptableObject, Unity time, or Unity random APIs. Unity validation only proves compile/test parity.
- Dependency / licensing rule: Do not add dependencies, packages, GitHub Actions, executables, or downloaded tools.
- Security / privacy / redaction rule: Rejections and mismatch handling must use ADR-004 safe errors and must not expose stored results, fingerprints, secrets, hidden gameplay data, raw exception text, stack traces, SQL, absolute paths, or private content.
- Performance or platform constraint: Production Core bridge projects remain `netstandard2.1`; tests run under the pinned .NET 10 test host; Unity baseline remains `6000.4.0f1`.

## 7. Expected behavior

### Scenario 1 - New synthetic command is accepted once

**Given** an in-memory command execution context with no stored receipt
**When** a valid synthetic command is submitted
**Then** the handler runs once, authoritative time/RNG are obtained only after duplicate and pre-RNG validation, an ordered DomainEvent batch is produced, and `Result<CommandResult>` returns outer Success with terminal `Accepted`.

### Scenario 2 - Exact duplicate replays stored result

**Given** the same synthetic command `CommandId` and semantic fingerprint already has a stored result
**When** the command is submitted again
**Then** the stored `CommandResult` is returned, no new DomainEvent is created, the handler is not invoked again, and clock/RNG counters do not advance for new work.

### Scenario 3 - CommandId mismatch is safe

**Given** a stored receipt exists for a `CommandId`
**When** another command with the same `CommandId` but different semantic fingerprint is submitted
**Then** execution is rejected through a safe Application error/result path without revealing the original stored result or creating DomainEvents.

### Scenario 4 - Deterministic clock and RNG vectors are stable

**Given** fixed clock and RNG vector inputs
**When** the contract vector tests run under pure .NET and Unity test assemblies where required
**Then** UTC instants, virtual scheduler order, HMAC-derived stream state, xoshiro outputs, rejection counts, bounded integer outputs, and non-secret proof data match the expected vectors.

### Required invariants

- `CommandId` is the only Core idempotency key for Application commands.
- Duplicate command handling never re-enters the command handler, creates events, consumes RNG, or samples time for new events.
- `CommandResult.Status` has exactly `Accepted`, `Pending`, and `Rejected`.
- `Pending` is a committed terminal result for the original command and is not mutated by a future continuation.
- DomainEvents are immutable, ordered, and never used as raw network DTOs.
- Domain and Rules do not depend on Application Result/Error, Application command pipeline, persistence, networking, logging, serializers, Unity, or global time/random APIs.
- Test helpers, fakes, fixtures, and vectors do not enter Player runtime assemblies.

## 8. Deliverables

- Production code: Minimal command/event/clock/RNG contracts and value primitives under `Packages/com.odyssey.domain/Runtime/**` and `Packages/com.odyssey.application/Runtime/**` only where ADR ownership requires them.
- Tests: Focused .NET Unit/Domain/Contracts/Architecture tests for command results, duplicate behavior, ordered events, clock/scheduler contracts, RNG algorithm/vector behavior, and global API guards; Unity EditMode/PlayMode compatibility tests only if needed to prove compile/vector parity.
- Scripts / CI: Updates to existing repository scripts only if required to validate ODY-S00-005 contracts. No GitHub Actions.
- Configuration: Test catalog metadata and deterministic vector fixtures if required.
- Documentation: ODY-S00-005 completion evidence, parent ExecPlan PR-003B progress/evidence, backlog/README status if materially affected.
- Generated evidence or build artifacts: Command output summaries and local test report paths only; no tracked generated build artifacts.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. Every new production contract has documented owner-module placement and is implemented only in the owning package path allowed by ADR-001.
2. `CommandId` is implemented as the sole Core idempotency key for Application commands; no separate Core `IdempotencyKey` is added.
3. The minimal command envelope and `CommandResult` model follow ADR-002 and ADR-004: terminal statuses are exactly `Accepted`, `Pending`, and `Rejected`; outer `Result<CommandResult>` distinguishes durable terminal outcomes from infrastructure/technical failure.
4. The synthetic command operation proves: first valid submission is accepted, rejected validation creates no DomainEvents/RNG consumption, exact duplicate returns the stored result without re-execution, same `CommandId` with different fingerprint is safely rejected, and in-flight or sequential duplicates do not create multiple effects.
5. DomainEvent identity/envelope and ordered batch contracts are immutable, Domain-owned, causally linked to command/transaction identity, and never treated as network DTOs or mutable audit records.
6. Clock contracts use injected wall-clock, monotonic-clock, and scheduler abstractions; tests prove fixed/manual/virtual behavior without real waiting or global time APIs.
7. RNG contracts implement or scaffold the ADR-008 v1 algorithm set required by this task: HMAC-SHA-256 stream derivation, xoshiro256** output, rejection mapping without modulo bias, deterministic vectors, and non-secret `RngProofData`.
8. Architecture/repository checks reject forbidden global time/random APIs in authoritative Core production paths and prove Domain/Rules remain free of Application command/result, Unity, persistence, networking, logging, serializer, clock, and RNG implementation dependencies.
9. New stable TestCase IDs for ODY-S00-005 are registered in `Tests/Metadata/test-catalog.json` and covered by real tests or repository checks.
10. No SQLite, network transport, serialization DTO/upcaster/source-generated context, runtime composition, diagnostics runtime, gameplay feature, Unity package/settings change, GitHub Action, third-party dependency, ADR change, Technical Baseline change, or version bump is introduced.
11. Completion evidence records real pass/fail/not-run results for all required validation commands before the task moves to `In Review`.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-CMD-001` | .NET Unit/Contracts | Command identity, type/version, root/parent identity, fingerprint, and envelope validation are canonical and reject invalid/default values | Pass |
| `TC-CMD-002` | .NET Unit/Contracts | `CommandResult` has exactly Accepted/Pending/Rejected terminal states and maps to outer `Result<CommandResult>` semantics | Pass |
| `TC-CMD-003` | .NET Unit/Application test fixture | Exact duplicate command returns stored result without handler re-entry, event creation, clock sampling, or RNG consumption | Pass |
| `TC-CMD-004` | .NET Unit/Application test fixture | Same `CommandId` with different semantic fingerprint is rejected safely and does not reveal the original stored result | Pass |
| `TC-EVENT-001` | .NET Domain/Contracts | DomainEvent envelope and ordered batch are immutable, Domain-owned, causally linked, and deterministic | Pass |
| `TC-CLOCK-001` | .NET Unit/Contracts | Wall-clock and monotonic clock contracts use injected values and reject global-clock assumptions | Pass |
| `TC-CLOCK-002` | .NET Unit/Contracts | Virtual scheduler completes deterministic order without `Task.Delay` or real waiting | Pass |
| `TC-RNG-001` | .NET Unit/Contracts | HMAC-SHA-256 stream derivation v1 produces stable state vectors from synthetic fixture keys/context | Pass |
| `TC-RNG-002` | .NET Unit/Contracts | xoshiro256** v1 produces stable raw output vectors | Pass |
| `TC-RNG-003` | .NET Unit/Contracts | Rejection mapping for inclusive integer ranges is unbiased and records rejection count evidence | Pass |
| `TC-RNG-004` | .NET Unit/Contracts | `RngProofData` contains non-secret reproducibility metadata and never contains raw campaign RNG key material | Pass |
| `TC-ARCH-001` | Architecture script / .NET test | ADR-001 dependency graph still passes after adding command/event/clock/RNG contracts | Pass |
| `TC-DOTNET-001` | .NET build/test | Core bridge projects compile the same package source under `netstandard2.1` with C# 9 parity | Pass |
| `TC-UNITY-ASM-001` | Unity batchmode | Unity assembly graph compiles with the new contracts | Pass |
| `TC-UNITY-TEST-001` | Unity Test Framework | Existing Unity EditMode/PlayMode tests still run with nonzero tests and test-only assemblies; add vector parity checks only if required by implementation | Pass |
| `TC-REPO-001` | Repository script | Repository policy, forbidden global APIs, generated/private path exclusions, registry requirements, and SDK configuration remain enforced | Pass |

### Required commands

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\restore.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-format.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-test-structure.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-fast.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-unity.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-repository.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-repository-policy.ps1
dotnet build DotNet/Odyssey.Core.sln --no-restore
dotnet test DotNet/Odyssey.Core.sln --no-build --no-restore
git diff --check
git diff --cached --check
git status --short --branch
```

### Manual validation

- Review every new public type against ADR-001 ownership before accepting it as public API.
- Review the diff for duplicate production source, gameplay behavior, persisted format/protocol creation, real persistence/network adapters, Unity package drift, private/local paths, secrets, RNG key leakage, and ODY-S00-006/007 scope bleed.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Unity Editor `6000.4.0f1 (8cf496087c8f)` for Unity compile/test validation; no Player profile required.
- Scripting backend: Editor/Mono-compatible checks only; IL2CPP Player validation is out of scope.
- Network topology or database fixture: None; in-memory test adapters only.
- Other: .NET SDK `10.0.302` with repository `global.json`, production bridge projects targeting `netstandard2.1`, and test projects targeting `net10.0`.

### Validation not required by this task

- Windows Player build, IL2CPP smoke, GitHub Actions, release/build identity generation, ADR-003 serialization/AOT spike, SQLite/persistence integration, networking integration, runtime composition, diagnostics runtime, gameplay feature tests, migration rehearsal, and clean-checkout M1 rehearsal.

## 11. Compatibility, migration, and rollback

- Compatibility impact: Adds in-memory Core contracts and deterministic vectors only; no persisted state, serializer DTO, public protocol, save format, network contract, or gameplay feature is introduced.
- Version fields affected: No application/schema/format/contract/protocol/ruleset version is bumped.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable until ADR-003/ADR-007 serialization and compatibility fixtures are implemented in later tasks.
- Rollback method: Revert the ODY-S00-005 pull request and rerun repository policy, fast tests, Unity tests, and diff checks.
- Data-loss risk and protection: None.
- Recovery rehearsal required: None.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | - | - | - | - |

Do not add production or development dependencies, GitHub Actions, executables, downloaded tools, package version updates, or Unity package changes in this task.

## 13. Security, privacy, and hidden information

- Data classes handled: Public-safe command identities, command/result metadata, event metadata, deterministic synthetic test data, clock/RNG vector fixtures, and non-secret `RngProofData`.
- Trust boundaries: Application command admission and result boundary; Domain event fact boundary; local in-memory test adapters.
- Authorization / audience checks: No runtime permissions, networking, or audience projection is implemented; command mismatch handling must not expose stored results.
- Redaction requirements: No stack trace, SQL, absolute path, secret, token, key, raw campaign RNG key, hidden gameplay data, private content, raw exception text, arbitrary object, unrestricted dictionary, or raw rejected user value in public errors, vectors, logs, or task evidence.
- Log-safe fields: `CommandId`, `RootCommandId`, `ParentCommandId`, `DomainEventId`, `TransactionId`, `CorrelationId`, `DiagnosticId`, ErrorCode/SafeReasonCode/UserMessageKey, command/result status, non-secret RNG algorithm/version identifiers, non-secret RNG key epoch IDs or hashes allowed by ADR-008 only.
- Abuse / malformed input limits: Parsers reject malformed, empty, overlong, non-canonical, wrong-kind, default, mismatched, unsupported-version, and duplicate-key-equivalent command values deterministically.
- Security tests: Duplicate/mismatch tests, no-secret `RngProofData` tests, parser rejection tests, repository policy checks, and manual diff review.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: ODY-S00-005 changes public Core contracts across Domain and Application and touches command/event/idempotency, time, randomness, security, redaction, and deterministic test vectors.
- ExecPlan path: `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
- Expected pull request count: 1
- Milestone or sequencing constraints: Do not implement production code or tests until this task contract and implementation plan are owner-confirmed. Do not start ODY-S00-006 or ODY-S00-007 inside this task.

## 15. Documentation and versioning impact

- Documents that must change: ODY-S00-005 task completion evidence, parent ExecPlan PR-003B progress/evidence, `Tests/Metadata/test-catalog.json`, vector/evidence docs if created, backlog/README status if materially affected.
- Documents that must not change: ADR-001 through ADR-010, Technical Development Baseline v0.3, Active Documentation Baseline v1.8 except owner-approved operational active-task pointer update, private product documents, changelogs, and handoff/context bundles.
- Application version change: No - command/event/clock/RNG contracts do not bump the application version source of truth.
- Schema / format / contract / protocol / ruleset version change: None in source-of-truth files; in-memory command/event version primitives may exist only as contract values required by ADR-002.
- Documentation version changes: None expected; operational task/status updates only.
- Changelog or release-note requirement: Task/ExecPlan evidence only; no user-facing release note.

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

Implementation is complete and ready for owner review/commit permission. No commit, push, PR creation, or merge has been performed.

### Changed files / areas

- `docs/tasks/active/ODY-S00-005_Command_Event_Clock_and_RNG_Contracts.md` - task contract activated.
- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.8.md` - operational active-task pointer updated from ODY-S00-004 to ODY-S00-005.
- `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md` - PR-003B/current action updated for ODY-S00-005 activation.
- `Packages/com.odyssey.application/Runtime/Commands/**` - Application command identity, envelope, fingerprint, command result, receipt store, and executor contracts.
- `Packages/com.odyssey.application/Runtime/Time/**` - injected wall-clock, monotonic-clock, and scheduler contracts.
- `Packages/com.odyssey.application/Runtime/Random/**` - ADR-008 deterministic RNG contracts, HMAC derivation, xoshiro256** stream, rejection mapping, and non-secret proof data.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` and `docs/errors/ERROR_CODES.md` - registered safe `application.command.identity_mismatch` ErrorCode.
- `Packages/com.odyssey.domain/Runtime/Events/**` - DomainEvent identity/envelope and ordered immutable batch contracts.
- `DotNet/Tests/Odyssey.Tests.Unit/CommandEventClockRngContractTests.cs` - synthetic in-memory operation, duplicate replay, mismatch rejection, clock/scheduler, event batch, and RNG vector tests.
- `Tests/Metadata/test-catalog.json` - ODY-S00-005 TestCase IDs.
- `scripts/verify-test-structure.ps1` - ODY-S00-005 required IDs and forbidden global time/random API guard.
- `scripts/check-repository-policy.ps1` - required path update after moving ODY-S00-004 and activating ODY-S00-005.
- `scripts/test-fast.ps1` - ODY-S00-005 TRX output path/prefix.
- `scripts/test-unity.ps1` - ODY-S00-005 Unity XML/log output path.
- `docs/tasks/completed/ODY-S00-004_Identity_Version_and_Result_Primitives.md` - moved from active by owner confirmation.
- `README.md` - repository status updated.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `git diff --check` | Passed | No whitespace errors in tracked diffs for this activation step. |
| `rg -n "[ \t]$" docs/tasks/active/ODY-S00-005_Command_Event_Clock_and_RNG_Contracts.md` | Passed | No trailing whitespace after correcting template hard-break spacing. |
| `rg -n "docs/tasks/active/ODY-S00-004_Identity_Version_and_Result_Primitives|ODY-S00-004.*текущей active task|current active task.*ODY-S00-004" ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.8.md` | Passed | No stale ODY-S00-004 active-task pointer remains in Active Baseline v1.8. |
| `git status --short --branch` | Passed | Activation-only checkpoint before implementation confirmed branch `feat/ody-s00-005-command-event-clock-rng-primitives`; implementation changes are listed below. |

Implementation validation:

| Command / check | Result | Evidence / notes |
|---|---|---|
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\restore.ps1` | Passed | Projects restored; output reported projects up to date/restored under repository cache settings. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-test-structure.ps1` | Passed | `TC-ARCH-001 PASS`; controlled invalid Domain->Rules, package version mismatch, and duplicate catalog ownership fixtures rejected. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-fast.ps1` | Passed | Build passed with 0 warnings/errors; TRX under `Logs/ODY-S00-005/dotnet/`: totals 1 Domain, 1 Contracts, 34 Unit, 2 Architecture; all failed 0. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-unity.ps1` | Failed then Passed | First sandboxed run failed before project compile on Unity user cache `CurlRequestCache.db`; rerun with escalated Unity cache access passed: batch compile exit 0, EditMode exit 0, PlayMode exit 0, EditMode total 1 passed 1 failed 0 skipped 0, PlayMode total 1 passed 1 failed 0 skipped 0. XML reports are under `Logs/ODY-S00-005/`. Unity-generated ProjectSettings whitespace churn was restored and is not part of this task. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-repository.ps1` | Passed | Repository policy, architecture guard, and SDK check passed; configured/selected SDK `10.0.302`; registry fixtures passed. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-repository-policy.ps1` | Passed | `REPO-POLICY-001` through `REPO-POLICY-005` pass; registry lifecycle/literal/SafeReason/SemVer/UserMessageKey/length fixtures pass. |
| `dotnet build DotNet\Odyssey.Core.sln --no-restore` | Passed | 0 warnings, 0 errors. |
| `dotnet test DotNet\Odyssey.Core.sln --no-build --no-restore` | Passed | Unit 34, Domain 1, Contracts 1, Architecture 2; failed 0, skipped 0. |
| `git diff --check` | Passed | No whitespace errors after restoring Unity-generated `ProjectSettings/ProjectSettings.asset` churn. |
| `git diff --cached --check` | Passed | No staged diff errors; command reports only the local inaccessible global ignore warning when applicable. |
| `git status --short --branch` | Passed | Branch `feat/ody-s00-005-command-event-clock-rng-primitives`; changes are unstaged as requested; no commit, push, PR, or merge performed. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | New contracts are placed under owning package paths: Application command/time/RNG, Domain events. |
| AC-2 | Passed | `CommandId` is the only Core command idempotency key; no `IdempotencyKey` type/field was added. |
| AC-3 | Passed | `CommandResultStatus` has exactly `Accepted`, `Pending`, `Rejected`; `CommandExecutor.Submit` returns `Result<CommandResult>`. |
| AC-4 | Passed | Synthetic tests cover accepted command, rejected command with no events/RNG, exact duplicate replay without re-execution, and safe mismatch rejection. |
| AC-5 | Passed | DomainEvent envelope/batch are Domain-owned, immutable/read-only, ordered, and causally linked through transaction and causation ids. |
| AC-6 | Passed | Clock/scheduler contracts are injected; tests use fixed/virtual implementations without real waiting. |
| AC-7 | Passed | HMAC-SHA-256 derivation, xoshiro256**, rejection mapping, deterministic vector outputs, and non-secret `RngProofData` are tested. |
| AC-8 | Passed | Architecture guard rejects forbidden global time/random APIs in authoritative Core production paths; dependency graph remains valid. |
| AC-9 | Passed | ODY-S00-005 TestCase IDs are registered in `Tests/Metadata/test-catalog.json` and covered by tests/checks. |
| AC-10 | Passed | No SQLite, networking, serialization DTO/upcaster/context, runtime composition, diagnostics runtime, gameplay, Unity package/settings, GitHub Action, dependency, ADR/TDB change, or version bump was introduced. |
| AC-11 | Passed | Required validation commands have real pass/fail/pass-after-rerun evidence recorded. |

### Build and artifact evidence

- Build identity: Not created.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: `Logs/ODY-S00-005/dotnet/*.trx`, `Logs/ODY-S00-005/editmode-results.xml`, `Logs/ODY-S00-005/playmode-results.xml`.

### Known limitations

- `docs/tasks/completed/ODY-S00-004_Identity_Version_and_Result_Primitives.md` was moved from `docs/tasks/active/` by owner confirmation before implementation.
- `CommandFingerprint` is intentionally an opaque stable in-memory/test abstraction. Canonical JSON command serialization and canonical fingerprint computation remain ODY-S00-007.
- Command receipt storage and transaction/outbox behavior are in-memory contracts only; no durable persistence or network transport exists in this task.

### Follow-up tasks

- `ODY-S00-006` continues runtime composition and diagnostic shell after ODY-S00-005 is owner-reviewed and merged.
- `ODY-S00-007` continues serialization/AOT compatibility after ODY-S00-005 and ODY-S00-006 reach their required state.

### Self-review summary

- Scope review: Implementation stayed within command/event/idempotency, injected clock/scheduler, RNG vectors, in-memory synthetic operation, tests, guards, and required docs/status updates.
- Architecture review: Domain owns events and remains Application-free; Application owns command/time/RNG orchestration contracts; no Common/Shared/Utils module, dependency edge, persistence/network adapter, or Unity dependency was added.
- Test review: ODY-S00-005 TestCase IDs are registered and covered by focused .NET tests, architecture guard, repository policy, and Unity compile/test validation.
- Security/privacy review: CommandId mismatch returns a safe Error without stored result/fingerprint/payload disclosure; `RngProofData` excludes raw campaign RNG key material; no secrets/private paths were added.
- Documentation/version review: Active Baseline pointer and parent ExecPlan were updated operationally; no ADR/TDB/application/schema/format/contract/protocol/ruleset version was changed.

## 18. Blockers, decisions, and change control

### Blockers

- None currently. ODY-S00-005 implementation is ready for owner review and commit permission.

### Decisions made during execution

- 2026-08-10 - Activate ODY-S00-005 task contract on branch `feat/ody-s00-005-command-event-clock-rng-primitives` without production code or tests - Authority / approval: explicit product owner instruction.
- 2026-08-10 - Use `CommandId` as the sole Core command idempotency key; do not create a separate Core `IdempotencyKey` - Authority / approval: ADR-002.
- 2026-08-10 - Use in-memory test adapters only; do not introduce SQLite or network transports - Authority / approval: SLICE-00 backlog and explicit ODY-S00-005 scope.
- 2026-08-10 - Keep `CommandFingerprint` as a stable opaque in-memory/test abstraction for CommandId mismatch detection; do not use `GetHashCode()`, object identity, or process/runtime-dependent values; defer canonical JSON command serialization and canonical fingerprint computation to ODY-S00-007 - Authority / approval: product owner instruction and ADR-003 sequencing.

### Approved task changes

- 2026-08-10 - Owner approved preparing and activating ODY-S00-005 task contract, updating Active Baseline v1.8 active-task pointer, and updating the parent ExecPlan where required before implementation - Approved by: product owner.
