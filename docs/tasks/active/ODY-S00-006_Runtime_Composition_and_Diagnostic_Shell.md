# ODY-S00-006 - Runtime Composition and Diagnostic Shell

**Status:** In Review
**Roadmap stage / slice:** SLICE-00
**Owner:** Codex
**Requested by:** Product owner
**Branch:** `feat/ody-s00-006-runtime-composition-diagnostic-shell`
**Pull request:** Draft PR #10 - https://github.com/odyssey-services/Odyssey_VTT/pull/10
**ExecPlan:** `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
**Created:** 2026-08-11
**Last updated:** 2026-08-11 14:15 UTC

## 1. Goal

Create the first real Unity runtime shell for SLICE-00 using explicit manual dependency composition and safe diagnostics.

The resulting Unity Editor application should be interactable, but it is still a technical Developer Shell, not VTT gameplay.

## 2. Why this task exists

- Problem or dependency being addressed: ODY-S00-005 delivered deterministic command/event/clock/RNG contracts, but SLICE-00 still lacks the ADR-005 process runtime shell and ADR-010 safe diagnostics baseline needed before serialization, CI, and Player smoke work.
- Value or risk reduction: Establishes one explicit Unity composition root, deterministic lifecycle ownership, safe startup/shutdown behavior, and allowlisted diagnostic visibility before gameplay or persistence exists.
- Blocking or enabling relationship: Depends on owner-merged ODY-S00-005; blocks ODY-S00-007 serialization/AOT compatibility and later build/Player validation tasks.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.8.md`
- `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.3.md`
- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/TASK_TEMPLATE.md`
- `docs/tasks/SLICE-00_BACKLOG.md`
- `docs/tasks/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
- `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
- `docs/tasks/completed/ODY-S00-005_Command_Event_Clock_and_RNG_Contracts.md`
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`
- `docs/adr/ADR-004_Result_and_Error_Model_v1.0.md`
- `docs/adr/ADR-005_Dependency_Composition_v1.0.md`
- `docs/adr/ADR-008_Deterministic_Clock_and_RNG_v1.0.md`
- `docs/adr/ADR-009_Unity_Project_and_Build_Baseline_v1.1.md`
- `docs/adr/ADR-010_Logging_Diagnostics_and_Redaction_v1.0.md`

### Requirement and test IDs

- Requirement IDs: `SLICE-00`, Milestone `M1`, PR-003C delivery group
- Existing test IDs: `TC-ARCH-001`, `TC-ARCH-002`, `TC-DOTNET-001`, `TC-UNITY-ASM-001`, `TC-UNITY-TEST-001`, `TC-REPO-001`, ODY-S00-004 result/error IDs, ODY-S00-005 command/event/clock/RNG IDs
- New test IDs to introduce: `TC-CMP-001`, `TC-CMP-002`, `TC-CMP-003`, `TC-CMP-004`, `TC-CMP-009`, `TC-CMP-010`, `TC-CMP-011`, `TC-CMP-015`, `TC-CMP-016`, `TC-CMP-018`, `TC-CMP-020`, task-specific extension `TC-CMP-021`, `TC-DIAG-002`, `TC-DIAG-003`, `TC-DIAG-004`, `TC-DIAG-005`, `TC-DIAG-006`, `TC-DIAG-011`, `TC-DIAG-012`, `TC-DIAG-013`, `TC-DIAG-015`, `TC-DIAG-016`, `TC-DIAG-017`, `TC-DIAG-018`, `TC-DIAG-019`, `TC-DIAG-020`, `TC-DIAG-021`, `TC-DIAG-022`, `TC-DIAG-023`, `TC-DIAG-024`, `TC-DIAG-025`, `TC-DIAG-026`, `TC-DIAG-027`, `TC-DIAG-028`, `TC-DIAG-046`, `TC-DIAG-051`, `TC-UNITY-SHELL-001`

### Task-safe private context

- Approved summary / references: Build only the public-safe runtime composition and diagnostics shell needed by the technical skeleton. Do not copy private product documents, hidden campaign content, local handoff text, secrets, personal paths, or private task bundles into repository artifacts.

## 4. Verified current state

### Verified facts

- PR #9 for ODY-S00-005 was owner-merged into `main` at `2026-08-11T12:31:50Z` as GitHub merge commit `7aa5cc972c48d9af6509895bb6d9ed1e18899fdf`.
- Local `main` was fast-forwarded to `7aa5cc972c48d9af6509895bb6d9ed1e18899fdf`, and this task branch was created from that commit.
- Existing Unity baseline includes `Bootstrap.unity` and `AppShell.unity`, Unity `6000.4.0f1`, HDRP, UI Toolkit, and Input System.
- Existing Core modules include Domain, Rules, Content, Application, Persistence, Networking, and Unity Client package/assembly skeletons.
- ODY-S00-005 provides Application command/result/executor contracts, injected clock/scheduler contracts, deterministic RNG contracts, and one synthetic in-memory test operation.
- No production diagnostics runtime, Developer Shell composition root, process runtime host, crash marker, or UI shell lifecycle implementation exists yet for ODY-S00-006.

### Assumptions

- The ODY-S00-005 `SyntheticOperationHandler` exists only under `DotNet/Tests/Odyssey.Tests.Unit/**` and is not reusable production code. ODY-S00-006 may implement a minimal DeveloperShell-only non-gameplay probe adapter/handler under `Odyssey.Unity.Client`, explicitly selected only by the `DeveloperShell` profile, using production Application command/result contracts and in-memory development adapters.
- BuildIdentity is not available until ODY-S00-008; diagnostics must represent that state explicitly instead of fabricating a version string.

## 5. Scope

### In scope

- One production composition root in `Odyssey.Unity.Client`.
- One Unity runtime host/bootstrap owner for the process runtime.
- Explicit process lifecycle with deterministic startup phases and deterministic/idempotent shutdown.
- Startup phase sequence covering bootstrap/runtime host start, configuration/profile validation, diagnostics availability, Application/runtime graph composition, presentation shell initialization, and Ready.
- Startup failure behavior that returns safe `Result`/`Error`, records diagnostic evidence when diagnostics exist, cleans up partially owned resources, and displays a safe failure state in Developer Shell.
- Minimal Process and Presentation lifetimes only; each disposable resource created by the process root has one clear owner.
- Explicit runtime profile `DeveloperShell`, using the same production composition mechanism while explicitly selecting development adapters. Non-developer/production profiles must be rejected if they request DeveloperShell fake adapters.
- Application-owned diagnostics contracts: `IOdysseyLogger`, `LogEventV1` logical contract, `LogLevel`, `EventCode`, `ProcessInstanceId`, `MessageTemplateKey`, `SafeLogProperty` / `SafeLogValue`, `DiagnosticContext`, `ExceptionSummary`, and typed diagnostic builders/contracts.
- `LogLevel` vocabulary exactly: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`.
- `LogEventV1` logical fields: `SchemaVersion = 1`, `TimestampUtc`, `Level`, `EventCode`, `Subsystem`, `BuildId` when available, mandatory `ProcessInstanceId`, optional `CorrelationId`, optional `DiagnosticId`, optional `CommandId`, optional `SessionReference`, `MessageTemplateKey`, `SafeProperties`, and optional safe `ExceptionSummary`.
- `ProcessInstanceId` value primitive owned by `Application.Diagnostics`: generated once per process startup, not persistent, not user/device identity, does not encode username, device id, path, or secret, and has a deterministic fake/injected generator for tests.
- BuildIdentity/BuildId generation remains ODY-S00-008. ODY-S00-006 must represent BuildId absence honestly as unavailable/not-yet-composed and must not create a competing canonical BuildId primitive or fabricated version/hash/string.
- Machine-readable EventCode registry at `config/diagnostics/event-codes.json`, with runtime/code registry and file registry semantic parity checks. Registry rows include EventCode, owner subsystem, default LogLevel, allowed property keys, property classifications, purpose, and status `Active` / `Deprecated` / `Reserved`.
- Register only EventCodes actually used by ODY-S00-006; do not pre-register persistence, networking, or gameplay events. Validation must reject unknown EventCode, active code missing registry entry, unregistered property key, property classification mismatch, and reuse of Deprecated/Reserved code.
- Allowlist-first typed/bounded safe properties and rejection of unsafe diagnostic payloads. Strings are unsafe by default.
- Safe bounded string baseline: maximum 256 Unicode scalar values unless registry says smaller; truncation must use an explicit marker. Lists are maximum 20 items unless registry says smaller.
- Explicit safe representations for at least bounded public/operational text, safe code/enum, integer/count, duration, UTC timestamp, byte count, safe technical identifier/fingerprint, sanitized path representation, and sanitized endpoint representation.
- Unity Client-owned concrete diagnostic process runtime/sinks: bounded queue, in-memory ring-buffer sink, Development/Editor Console sink, emergency sink, crash marker platform adapter, fatal/unexpected Unity/platform hook adapter, and Developer Shell diagnostic presenter. One process root owns this runtime; no static global logger.
- Bounded in-memory ring buffer with maximum 2000 events or 8 MiB estimated logical payload, first reached limit wins. Eviction removes oldest cleaned events first. Logical-size estimation is deterministic and does not depend on ODY-S00-007 JSON serialization. No raw/unredacted value may enter the buffer.
- Bounded diagnostics queue/backpressure with maximum 4096 events or 16 MiB estimated logical payload, first reached limit wins. Under pressure, drop `Trace`, then `Debug`, then `Information`; `Warning`/`Error`/`Critical` use priority path or emergency fallback. After recovery, emit a bounded drop-counter event.
- Diagnostics runtime constraints: no unbounded main-thread blocking, sink failure cannot recurse into itself, diagnostics failure must not change successful Application/Domain outcome, disabled level must not evaluate expensive/lazy property, concurrent enqueue must not corrupt ordering/state, normal shutdown drains within up to 2 seconds, fatal shutdown is best effort up to 500 ms.
- Minimal emergency/crash marker semantics with canonical provisional file name `process-started.json`; format is intentionally trivial and does not depend on ADR-003 serialization ownership. Crash marker completion reports a real success/failure outcome; `diagnostics.crash.marker_completed` is emitted only after successful completion.
- Minimal UI Toolkit Developer Shell showing runtime state and diagnostic visibility.
- Minimal interactive controls to prove composition, such as executing one DeveloperShell-only non-gameplay probe command path, displaying safe Accepted/Rejected result, showing diagnostic ring-buffer entries, triggering a safe synthetic diagnostic event, and requesting clean runtime stop where practical in Editor.
- Lifecycle and diagnostics tests in existing .NET and Unity test assemblies.
- Updates to `Tests/Metadata/test-catalog.json`, parent ExecPlan progress/evidence, task Completion Evidence, README status if needed, and repository policy/architecture guards only where required by the task.

### Out of scope

- ODY-S00-007 implementation, `TC-DIAG-001` LogEventV1 JSON serialization, serialization DTOs, canonical JSON, source-generated JSON contexts, JSONL file sink, JSONL secret vectors, log-schema compatibility parsing, duplicate JSON property handling, diagnostic JSONL final format, diagnostic bundle manifest serialization, or .NET/Unity serialization parity proof.
- SQLite, database connections, real outbox, Persistence runtime implementation, Networking runtime implementation, relay, accounts/auth, telemetry, remote crash upload, or diagnostic bundle final implementation.
- Real `CampaignRuntime`, `SessionRuntime`, Operation transaction scope, Persistence runtime, or Networking runtime instantiation.
- BuildIdentity generation, Git metadata, GitHub Actions, Windows Player build, IL2CPP build, or release artifacts.
- Gameplay, campaign runtime behavior, map/tokens/combat/dice/characters/content/chat/audio UI or behavior.
- New Unity package dependencies, external logging frameworks, DI containers, service provider frameworks, or mocking frameworks.
- Unity version/package/ProjectSettings baseline changes unless a required Unity shell file references an existing setting without changing the baseline.

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/**
Packages/com.odyssey.application/Tests/**
Assets/Odyssey/Client/Runtime/**
Assets/Odyssey/Client/Editor/**
Assets/Odyssey/Client/UI/**
Assets/Odyssey/Client/Scenes/Bootstrap.unity
Assets/Odyssey/Client/Scenes/AppShell.unity
Assets/Odyssey/Client/Tests/EditMode/**
Assets/Odyssey/Client/Tests/PlayMode/**
DotNet/Tests/Odyssey.Tests.Unit/**
DotNet/Tests/Odyssey.Tests.Architecture/**
Tests/Metadata/test-catalog.json
config/diagnostics/event-codes.json
docs/errors/ERROR_CODES.md
scripts/verify-test-structure.ps1
scripts/test-fast.ps1
scripts/test-unity.ps1
scripts/verify-repository.ps1
scripts/check-repository-policy.ps1
docs/tasks/active/ODY-S00-006_Runtime_Composition_and_Diagnostic_Shell.md
docs/tasks/completed/ODY-S00-005_Command_Event_Clock_and_RNG_Contracts.md
docs/tasks/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md
docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md
docs/tasks/SLICE-00_BACKLOG.md
README.md
```

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
Assets/Odyssey/Client/Runtime/**/*.prefab
Assets/Odyssey/Client/UI/OdysseyPanelSettings.asset
version.json
config/compatibility.json
.github/**
```

Owner approval for this activation step permits only the operational `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.8.md` active-task pointer update from ODY-S00-005 to ODY-S00-006. Further edits to paths listed above require separate approval.

Implementation note: The exact scene edits above are owner-approved only for minimal changes required to attach one RuntimeHost/bootstrap component and one AppShell scene entry point / presentation initializer. Do not create a second composition root in AppShell. Do not edit `ProjectSettings/**`, `Packages/manifest.json`, `Packages/packages-lock.json`, or `Assets/Odyssey/Client/UI/OdysseyPanelSettings.asset` without a new explicit blocker/approval.

## 6. Technical constraints

- Module ownership and dependency direction: Follow ADR-001 exactly. Application owns diagnostics contracts/runtime baseline; Unity Client owns the production composition root and presentation shell; Domain and Rules do not depend on diagnostics/logging.
- Composition and lifecycle: Follow ADR-005. Exactly one production composition root lives in `Odyssey.Unity.Client`; use constructor injection by default; no DI container, `IServiceProvider`, `Resolve<T>()`, service locator, static mutable service registry, global `Instance` manager pattern, assembly scanning/reflection registration, or independent object graph in `Awake`/`Start`.
- Unity ownership: Unity Bootstrap may own one root runtime host. `DontDestroyOnLoad` is allowed only for the approved root runtime host or explicitly approved platform adapters. Do not use `FindObjectOfType`, `GameObject.Find`, Resources lookup, or ScriptableObject registries for application dependencies.
- Lifetime scope: Implement only minimal Process and Presentation lifetime needed by Developer Shell. Process root owns every disposable resource it creates. Shutdown runs in reverse ownership order, is safe to call more than once, and cleans up partial startup after failure.
- Startup contract: Application may not report Ready before mandatory checks succeed. Do not invent persistence/network startup phases.
- Result/error boundary: Follow ADR-004. Startup failure and unexpected exception boundaries produce safe `Result`/`Error`; public errors do not expose stack traces, absolute paths, secrets, or raw exception text.
- Time/RNG rule: Follow ADR-008. Runtime lifecycle and diagnostics use injected clocks where authoritative or test-deterministic behavior matters; no global time/random in authoritative Core logic.
- Diagnostics/redaction: Follow ADR-010. Diagnostics are allowlist-first. Strings are unsafe by default unless they pass an explicit sanitizer/trust transition. Redact before every sink, not after writing.
- Safe property model: Do not expose `object`, `params object[]`, `Dictionary<string, object>`, arbitrary JSON, `Exception` as property, DomainEvent or command payloads, UnityEngine.Object, byte arrays, or streams through logging APIs.
- EventCode registry: No production diagnostic EventCode may be used without an Active registry row. Deprecated and Reserved codes remain reserved and cannot be reused. Runtime production does not need to deserialize `config/diagnostics/event-codes.json` through `System.Text.Json` in this task.
- Diagnostic runtime ownership: Application owns diagnostics contracts. Unity Client composition/platform boundary owns concrete process diagnostic runtime and sinks. Domain, Rules, and Application must not hide a global logger singleton.
- Developer profile: `DeveloperShell` is the current runtime profile. It uses production composition with explicit development adapters, not hidden environment switches, fake fallbacks, test assembly fallbacks, or editor singletons.
- Test composition: Tests must use explicit test composition/builders with deterministic clocks, deterministic `ProcessInstanceId`/`DiagnosticId` generators where needed, isolated temporary crash-marker paths, explicit typed adapter overrides, no `Dictionary<Type, object>`, no production fallback, and a fresh owned graph per lifecycle test. Production/PlayMode tests preserve production wiring order and replace only explicitly selected adapters.
- Developer probe command: If implemented, it is a Unity Client DeveloperShell-only non-gameplay probe adapter/handler. Production assemblies must not reference test source, Application must not gain a fake/developer repository implementation, and any development probe fingerprint remains opaque/development-only, not ADR-003/ODY-S00-007 evidence.
- Crash marker: The canonical provisional file name is `process-started.json`. A leftover unfinished marker on next startup means suspected crash only; it does not prove crash cause. Correct shutdown clears, replaces, or marks it completed. Marker content contains no authoritative application state, secret, personal, hidden payload, or logged absolute path.
- Serialization boundary: Do not implement canonical diagnostic JSONL or bundle serialization. ODY-S00-007 owns source-generated serialization work.
- Dependency/licensing: No new dependencies, packages, GitHub Actions, executable tools, or downloadable artifacts.

## 7. Expected behavior

### Scenario 1 - Runtime starts to Ready

**Given** the Unity Bootstrap scene starts the approved runtime host
**When** configuration/profile validation, diagnostics setup, runtime graph composition, and Developer Shell initialization all succeed
**Then** the Developer Shell displays Ready, the diagnostics ring buffer is available, and the runtime owns exactly one process graph.

### Scenario 2 - Startup failure cleans up safely

**Given** a startup phase fails after some resources were created
**When** startup returns a safe failure result
**Then** the Developer Shell displays Startup Failed, diagnostic evidence is recorded when possible, and already-created disposable resources are cleaned up in reverse ownership order.

### Scenario 3 - Shutdown is idempotent

**Given** the runtime is Ready or partially started
**When** shutdown is requested more than once
**Then** owned resources are disposed at most once in reverse order and no new application graph is created.

### Scenario 4 - Diagnostics are safe and visible

**Given** safe diagnostic events and unsafe candidate values
**When** diagnostics are written
**Then** allowlisted typed/bounded values reach the in-memory ring buffer and allowed development console sink, while unsafe objects, strings, payloads, secrets, raw exceptions, stack traces, and absolute paths are rejected or sanitized before any sink.

### Scenario 5 - Crash marker lifecycle is local and non-authoritative

**Given** a previous process did not cleanly shut down
**When** the next startup checks the minimal marker
**Then** it detects the unclean marker without reading secret/private payload, and a later clean shutdown clears or finalizes the marker safely.

### Required invariants

- There is exactly one production composition root in `Odyssey.Unity.Client`.
- The composition root does not contain business rules.
- Developer Shell does not own authoritative campaign state.
- No real CampaignRuntime, SessionRuntime, Operation transaction scope, Persistence runtime, or Networking runtime is created in this task.
- Diagnostics never log arbitrary objects, DomainEvents, command payloads, serialized DTOs, raw exceptions, stack traces, absolute Windows paths, SQL, connection strings, credentials, RNG keys, private keys, relay secrets, hidden gameplay state, private chat, or GM-only payloads.
- BuildId is represented as unavailable/not-yet-composed until ODY-S00-008; no fake BuildIdentity is created.
- `ProcessInstanceId` is mandatory on every logical `LogEventV1`.
- The EventCode registry and runtime/code EventCode definitions remain semantically aligned.

## 8. Deliverables

- Production code: Unity Client composition root/runtime host, minimal Developer Shell runtime, Application diagnostics contracts/runtime baseline, minimal crash marker baseline.
- Tests: Existing .NET and Unity test assemblies updated with ODY-S00-006 lifecycle/diagnostics coverage.
- Scripts / CI: No new CI. Existing repository checks may be extended only to register/enforce ODY-S00-006 test IDs, architecture/locator diagnostics guards, `Logs/ODY-S00-006` output paths, and EventCode registry validation integration.
- Configuration: `config/diagnostics/event-codes.json` EventCode registry only; no Unity package/settings baseline changes.
- Documentation: Task Completion Evidence, parent ExecPlan, test catalog, and status pointers.
- Generated evidence or build artifacts: Test logs/results only.
- Migration / recovery material: Minimal local crash marker cleanup behavior; no persistent campaign migration.

## 9. Acceptance criteria

1. Exactly one production composition root exists in `Odyssey.Unity.Client`, and architecture/tests reject service locator, DI container, `IServiceProvider`, `Resolve<T>()`, mutable global registry, global `Instance`, assembly scanning registration, and application dependency lookup through Unity scene/resource search APIs.
2. Runtime startup has explicit deterministic phases and reaches Ready only after mandatory validation, diagnostics availability, runtime graph composition, and Developer Shell initialization succeed.
3. Startup failure returns safe Result/Error data, records diagnostic evidence when diagnostics exist, and cleans up partially created resources.
4. Shutdown is deterministic, reverse-order, idempotent, and disposes each owned resource at most once.
5. Developer Shell is visible/interactable in Unity Editor and shows Starting, Ready, Startup Failed, and Shutting Down states where applicable.
6. Developer Shell can prove composition through at least one DeveloperShell-only non-gameplay probe command path and safe diagnostic event visibility without adding gameplay UI.
7. Application diagnostics contracts expose the required ADR-010 logical fields, exact `LogLevel` vocabulary, mandatory `ProcessInstanceId`, optional/unavailable BuildId contract state, and canonical shared `CorrelationId`, `DiagnosticId`, `CommandId`, and `UtcInstant` usage without duplicate semantic identity types.
8. `ProcessInstanceId` is generated once per process startup, is not persistent, does not identify user/device, encodes no username/device/path/secret, and has deterministic fake/injected generation for tests.
9. EventCode registry at `config/diagnostics/event-codes.json` is machine-readable and semantically checked against runtime/code registry. Unknown EventCode, active code missing registry entry, unregistered property key, property classification mismatch, and Deprecated/Reserved reuse fail validation.
10. Diagnostics are allowlist-first: safe typed/bounded properties pass; unsafe strings/objects/payloads/secrets/raw exceptions/stack traces/absolute paths are rejected or sanitized before every sink.
11. Bounded in-memory ring buffer enforces 2000 events or 8 MiB estimated logical payload, deterministic oldest-cleaned-event eviction, deterministic logical-size estimation independent of JSON serialization, and secret fixture absence.
12. Bounded queue/backpressure enforces 4096 events or 16 MiB estimated payload, Trace/Debug/Information pressure drop order, Warning/Error/Critical priority or emergency path, recovery drop-counter event, non-recursive sink failure, disabled-level lazy property non-evaluation, concurrent enqueue safety, and bounded shutdown budgets.
13. Unexpected exception boundary can create a `DiagnosticId` and safe internal `ExceptionSummary` while outward Error remains ADR-004-safe and duplicate exceptions are not fully recorded repeatedly.
14. Minimal crash marker `process-started.json` detects suspected previous unclean state, clean shutdown clears/finalizes it, repeated cleanup is safe, and marker content is not authoritative application state or secret/private payload.
15. Domain and Rules have no diagnostics/logger dependency.
16. No serialization DTOs, canonical JSON, JSONL file sink, SQLite, Networking, Persistence runtime, BuildIdentity generation, telemetry, GitHub Actions, Player/IL2CPP build, gameplay, or Unity package/settings baseline changes are introduced.
17. Required validation commands have real results recorded in Completion Evidence.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-CMP-001` | .NET / Unity | Build minimal process graph -> Success, exactly one AppRuntime in Starting; Ready gated by presentation initialization | Pass |
| `TC-CMP-002` | .NET / Unity | Invalid bootstrap/composition configuration -> safe Failure, graph unpublished | Pass |
| `TC-CMP-003` | Unity EditMode | Crash marker store fails after ownership -> graph unpublished, cleanup continues, original safe failure preserved | Pass |
| `TC-CMP-004` | Unity PlayMode | Duplicate RuntimeHost rejected, structured diagnostic recorded, accepted graph remains one, clean shutdown releases lease | Pass |
| `TC-CMP-009` | Unity PlayMode | Real AppShell scene unload -> PresentationRuntime detached/disposed once | Pass |
| `TC-CMP-010` | .NET / Unity | Shutdown called twice -> no duplicate side effects | Pass |
| `TC-CMP-011` | .NET / Unity | Mid-start process cancellation after owned resources exist -> bounded cleanup + cancelled Result | Pass |
| `TC-CMP-015` | Unity EditMode | Typed test adapter override changes only requested adapter; deterministic defaults otherwise preserved | Pass |
| `TC-CMP-016` | .NET / Unity | Non-developer/production composition cannot silently request developer fake | Pass |
| `TC-CMP-018` | Architecture / .NET | Static locator/container pattern -> architecture validation fails | Pass |
| `TC-CMP-020` | Unity EditMode | Shutdown while PresentationRuntime active -> presentation subscription before marker before diagnostics | Pass |
| `TC-CMP-021` | Unity EditMode | Task-specific extension: composition-invalid failure uses exact registered safe Error semantics | Pass |
| `TC-DIAG-002` | .NET Unit | Disabled Debug event does not evaluate lazy property | Pass |
| `TC-DIAG-003` | .NET Unit / policy | EventCode outside registry rejected | Pass |
| `TC-DIAG-004` | .NET Unit | Arbitrary object property rejected | Pass |
| `TC-DIAG-005` | .NET Unit | Safe bounded string truncates with explicit marker | Pass |
| `TC-DIAG-006` | Unity EditMode | Secret fixture rejected and absent from ring, emergency, and capturing sinks | Pass |
| `TC-DIAG-011` | .NET Unit | Absolute Windows path sanitized | Pass |
| `TC-DIAG-012` | .NET Unit | Windows username absent from path representation | Pass |
| `TC-DIAG-013` | .NET Unit | Network endpoint representation generalized/fingerprinted without networking runtime | Pass |
| `TC-DIAG-015` | .NET Unit | CommandId remains distinct from CorrelationId | Pass |
| `TC-DIAG-016` | .NET Unit | Unexpected exception creates DiagnosticId | Pass |
| `TC-DIAG-017` | .NET Unit | Public Error contains no stack trace/raw exception | Pass |
| `TC-DIAG-018` | Unity EditMode | Bounded incident dedup records full safe summary once, repeats counter-only | Pass |
| `TC-DIAG-019` | Unity EditMode | Diagnostic queue accepts concurrent Task producers without corruption | Pass |
| `TC-DIAG-020` | Unity EditMode | Queue pressure compares incoming priority and drops Trace/Debug/Information in order | Pass |
| `TC-DIAG-021` | Unity EditMode | Exact per-level drop counters emitted after recovery and reset | Pass |
| `TC-DIAG-022` | Unity EditMode | Protected Warning is not sacrificed; incoming high-priority event uses emergency `queue_full` fallback | Pass |
| `TC-DIAG-023` | .NET Unit | Sink failure does not recurse infinitely | Pass |
| `TC-DIAG-024` | Unity EditMode | Failing diagnostic sink does not change Accepted developer probe result or execute command twice | Pass |
| `TC-DIAG-025` | Unity EditMode | Normal shutdown drains diagnostic queue within 2s fake-monotonic budget | Pass |
| `TC-DIAG-026` | Unity EditMode | Fatal/bounded shutdown stops at 500ms fake-monotonic budget and writes emergency evidence | Pass |
| `TC-DIAG-027` | .NET Unit or Unity EditMode | Crash marker detected on next startup | Pass |
| `TC-DIAG-028` | .NET Unit or Unity EditMode | Correct shutdown clears/completes process marker | Pass |
| `TC-DIAG-046` | Architecture / .NET | Domain and Rules have no diagnostics/logger dependency | Pass |
| `TC-DIAG-051` | .NET Unit | Task-specific extension: bounded ring buffer count/byte capacity and deterministic oldest eviction | Pass |
| `TC-UNITY-SHELL-001` | Unity PlayMode | Bootstrap -> AppShell -> Ready -> technical actions -> duplicate host rejection -> Stopped + lease released | Pass |

### Required commands

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\restore.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-format.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-test-structure.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-fast.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-unity.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-repository.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-repository-policy.ps1
dotnet build DotNet\Odyssey.Core.sln --no-restore
dotnet test DotNet\Odyssey.Core.sln --no-build --no-restore
git diff --check
git diff --cached --check
git status --short --branch
```

### Manual validation

- Inspect Unity Editor Developer Shell state visually or through Unity test evidence sufficient for the implementation path.
- Confirm no gameplay/product UI commitment was introduced.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64
- Unity editor or Player profile: Unity `6000.4.0f1`, Editor validation only unless a later owner instruction adds Player work
- Scripting backend: Editor/Mono development path
- Network topology or database fixture: None
- Other: Existing .NET SDK `10.0.302`

### Validation not required by this task

- GitHub Actions: not present yet and assigned to ODY-S00-008.
- Windows Player build / IL2CPP: assigned to later SLICE-00 tasks.
- Serialization/AOT/canonical JSON/source-generated diagnostic serialization: assigned to ODY-S00-007.
- ADR-010 deferred to ODY-S00-007: `TC-DIAG-001` LogEventV1 JSON serialization, JSONL file sink, source-generated `JsonSerializerContext`, JSONL secret vectors, log-schema compatibility parsing, duplicate JSON property handling, and serialization parity in .NET/Unity.
- ADR-010 deferred to later SLICE-00 owners: BuildId integration to ODY-S00-008, Release/Player/IL2CPP profile evidence to ODY-S00-009, final traceability/applicability reconciliation to ODY-S00-010.
- SQLite, Persistence, Networking, telemetry, remote crash upload, diagnostic bundle final implementation, BuildIdentity generation, and gameplay validation: out of scope.

## 11. Compatibility, migration, and rollback

- Compatibility impact: Introduces initial runtime/diagnostic in-memory contracts and machine-readable EventCode registry only; no persisted campaign data or network protocol.
- Version fields affected: None. BuildId remains unavailable/not-yet-composed until ODY-S00-008; ODY-S00-006 must not create a competing canonical BuildId primitive.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable; no persisted user data.
- Rollback method: Revert the ODY-S00-006 pull request. Minimal local crash markers are non-authoritative and must be safely ignored/cleaned by startup.
- Data-loss risk and protection: No user campaign data is created. Diagnostics/crash marker must not be authoritative state.
- Recovery rehearsal required: Startup failure cleanup, idempotent shutdown, and crash marker lifecycle tests.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | - | - | - | - |

No new dependency, package, GitHub Action, executable, or downloadable tool is approved for this task.

## 13. Security, privacy, and hidden information

- Data classes handled: Diagnostic event metadata, safe typed properties, safe exception summaries, local crash marker state, synthetic command/result summaries.
- Trust boundaries: Unity Editor runtime, Application diagnostics runtime, in-memory diagnostic buffer, development/editor console sink, local crash marker path.
- Authorization / audience checks: No product permissions runtime exists; Developer Shell is technical only and must not expose hidden gameplay data.
- Redaction requirements: Allowlist-first; strings unsafe by default; redact/sanitize before every sink.
- Log-safe fields: Bounded typed values and explicitly trusted/sanitized values only.
- Abuse / malformed input limits: Bounded property names/values, bounded ring buffer capacity, unsafe object/string/payload rejection.
- Security tests: `TC-DIAG-004`, `TC-DIAG-005`, `TC-DIAG-006`, `TC-DIAG-011`, `TC-DIAG-012`, `TC-DIAG-013`, `TC-DIAG-016`, `TC-DIAG-017`, `TC-DIAG-027`, `TC-DIAG-028`, `TC-DIAG-046`, `TC-DIAG-051`, plus repository policy checks.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: This task touches Unity composition, Application diagnostics public contracts, lifecycle ownership, redaction behavior, startup/shutdown, crash markers, and Unity tests.
- ExecPlan path: `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
- Expected pull request count: 1 implementation PR after this activation commit.
- Milestone or sequencing constraints: This activation commit creates the contract only. Production implementation begins only after owner approval.

## 15. Documentation and versioning impact

- Documents that must change: This task contract, parent ExecPlan, backlog status, Active Baseline operational pointer, README status if used as active-task pointer, test catalog during implementation, Completion Evidence.
- Documents that must not change: ADR-001 through ADR-010, Technical Development Baseline v0.3, application/schema/format/contract/protocol/ruleset version documents unless an explicit owner decision is made.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: Active Baseline v1.8 is not bumped for operational pointer updates.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed or honestly marked not required.
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified.
- [x] Compatibility, migration, rollback, and versioning obligations are complete.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [x] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- Application diagnostics contracts under `Packages/com.odyssey.application/Runtime/Diagnostics/**`.
- Unity Client runtime composition, diagnostics runtime, crash marker, Developer Shell presenter, and DeveloperShell-only probe under `Assets/Odyssey/Client/Runtime/**`.
- Minimal approved scene wiring in `Assets/Odyssey/Client/Scenes/Bootstrap.unity` and `Assets/Odyssey/Client/Scenes/AppShell.unity`.
- Developer Shell UI styling in `Assets/Odyssey/Client/UI/AppShell.uss`.
- Machine-readable EventCode registry at `config/diagnostics/event-codes.json`.
- .NET diagnostics contract tests plus Unity EditMode composition/diagnostics tests and PlayMode Developer Shell lifecycle test.
- Test catalog and repository guard/script updates for ODY-S00-006 TestCase IDs, composition guards, diagnostics registry validation, and `Logs/ODY-S00-006` evidence paths.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\restore.ps1` | Failed then Passed | First sandbox run failed with `NU1900` while loading NuGet vulnerability data from `https://api.nuget.org/v3/index.json`; escalated rerun passed and restored/confirmed projects. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-test-structure.ps1` | Passed | `TC-ARCH-001` passed; controlled invalid Domain->Rules, package version mismatch, and duplicate catalog ownership fixtures were rejected with exit code 1. ODY-S00-006 composition/diagnostics guards also passed. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-fast.ps1` | Passed | Structure guard passed; `dotnet build` succeeded with 0 warnings / 0 errors; TRX evidence under `Logs/ODY-S00-006/dotnet/`: Unit 54/54, Domain 1/1, Contracts 1/1, Architecture 2/2, failed 0. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-unity.ps1` | Failed then Passed | Sandbox run failed before compile on Unity global cache access; escalated corrective runs exposed a PlayMode test assembly reference gap and a mid-start cancellation evidence gap. After fixes, final run passed Unity `6000.4.0f1`, batch compile exit code 0, EditMode total 27 passed 27 failed 0 skipped 0, PlayMode total 2 passed 2 failed 0 skipped 0. Logs under `Logs/ODY-S00-006/`. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-repository.ps1` | Passed | Repository policy passed, test structure passed, SDK configured `10.0.302`, selected `10.0.302`, rollForward `latestPatch`, allowPrerelease `False`; `REPOSITORY-VERIFY PASS`. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-repository-policy.ps1` | Passed | `REPO-POLICY-001` through `REPO-POLICY-005` passed, including controlled ErrorCode registry fixtures and the `TC-CMP-021` composition-invalid mapping. |
| `dotnet build DotNet\Odyssey.Core.sln --no-restore` | Passed | Build succeeded with 0 warnings / 0 errors. |
| `dotnet test DotNet\Odyssey.Core.sln --no-build --no-restore` | Passed | Contracts 1, Domain 1, Unit 54, Architecture 2; total 58 passed, 0 failed, 0 skipped. |
| `git diff --check` | Failed then Passed | Initial final run failed due Unity-generated `ProjectSettings/ProjectSettings.asset` whitespace churn and task-doc blank EOF; ProjectSettings was restored to HEAD and EOF corrected. Final rerun exited 0 with only the CRLF normalization warning for this task document. |
| `git diff --cached --check` | Passed | No staged diff errors; command printed only the local inaccessible global ignore warning. |
| `git status --short --branch` | Passed | Branch `feat/ody-s00-006-runtime-composition-diagnostic-shell`; ProjectSettings, manifest, package lock, ADRs, Technical Baseline, Active Baseline, Persistence, and Networking are not modified. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Satisfied | One `OdysseyRuntimeCompositionRoot` exists in `Odyssey.Unity.Client`; `verify-test-structure.ps1` rejects DI/service locator/search patterns and duplicate composition-root classes. |
| AC-2 | Satisfied | `OdysseyRuntimeCompositionRoot.Start` publishes `Starting`; runtime reaches `Ready` only after AppShell entry point and presentation initialization succeed. |
| AC-3 | Satisfied | Startup failures return safe `Result<Error>`, can emit diagnostic evidence when diagnostics exist, attempt all partial cleanup in reverse order, preserve the original safe failure, cover real mid-start cancellation after owned resources exist, and render scene-local StartupFailed fallback where exactly one UIDocument exists. |
| AC-4 | Satisfied | `AppRuntime.Shutdown` detaches presentation first, attempts process resources in reverse order even when one cleanup throws, emits crash-marker completed only after successful marker cleanup, records safe evidence when marker completion fails, shuts diagnostics last, reaches `Stopped`, and remains idempotent. |
| AC-5 | Satisfied | `AppShellEntryPoint` initializes the UI Toolkit Developer Shell; PlayMode `TC-UNITY-SHELL-001` verifies visible Ready, profile, build identity unavailable, accepted/rejected probes, diagnostic emission, structured duplicate host rejection, Stopped shutdown, and released host lease. |
| AC-6 | Satisfied | DeveloperShell-only probe uses Application command/result contracts and an in-memory commit adapter that records receipt plus accepted event batch; UI exposes separate accepted and rejected probe actions. |
| AC-7 | Satisfied | Application diagnostics contracts expose required `LogEventV1` fields, exact `LogLevel` vocabulary, `ProcessInstanceId`, unavailable BuildId state, and shared `CorrelationId`/`DiagnosticId`/`CommandId`/`UtcInstant`. |
| AC-8 | Satisfied | `ProcessInstanceId` is generated per startup; deterministic typed test composition is internal/friend-only; no username/device/path/secret is encoded. |
| AC-9 | Satisfied | `config/diagnostics/event-codes.json` exists and is checked against runtime registry parity by .NET tests and repository guard. |
| AC-10 | Satisfied | Safe property API is allowlist-first with split classification/valueKind; tests cover object/exception API absence, Unicode bounded truncation with small scalar limits, path username removal, endpoint generalization, secret rejection, and emergency token injection rejection. |
| AC-11 | Satisfied | `InMemoryDiagnosticRingBuffer` enforces count/byte limits with oldest-event eviction and rejects oversized events; Unity EditMode covers count/byte capacity. |
| AC-12 | Satisfied | `BoundedDiagnosticRuntime` covers lazy filtering by level/event code, incoming-priority-aware Trace/Debug/Information pressure policy, exact per-level drop counters, high-priority emergency fallback without sacrificing protected Warning, sink failure result isolation, concurrent producers, and bounded shutdown paths. |
| AC-13 | Satisfied | Startup and fatal-hook unexpected failures attach `DiagnosticId`, emit safe normalized incident evidence, deduplicate repeats without second full summary, and keep public `Error` ADR-004-safe without stack/raw exception text. |
| AC-14 | Satisfied | `CrashMarkerStore` uses exact `process-started.json` in `Application.persistentDataPath/Diagnostics/`; Unity EditMode covers valid started, exact completed, truncated JSON, invalid ProcessInstanceId, extra malformed suffix, malformed marker states, repeated best-effort clean finalization, and absence of `diagnostics.crash.marker_completed` when completion fails. |
| AC-15 | Satisfied | Repository guard scans Domain and Rules runtime source for diagnostics/logger dependencies; no violations. |
| AC-16 | Satisfied | No serialization DTOs/JSONL, SQLite, Networking/Persistence runtime, BuildIdentity generation, telemetry, CI, Player/IL2CPP build, gameplay, Unity package, manifest, lock, or ProjectSettings baseline changes are introduced. |
| AC-17 | Satisfied | Required validation commands have real pass/fail/pass evidence recorded above. |

### Build and artifact evidence

- Build identity: Not created.
- Artifact path / name: Test logs only under `Logs/ODY-S00-006/` and TRX files under `Logs/ODY-S00-006/dotnet/`; no build artifact.
- Checksums: None.
- Test or quality report: Unity `editmode-results.xml`, `playmode-results.xml`, and four .NET TRX files.

### Known limitations

- BuildIdentity is intentionally unavailable until ODY-S00-008.
- Canonical diagnostic JSONL and source-generated diagnostic serialization are deferred to ODY-S00-007 or later owning tasks.
- Crash marker format is intentionally provisional and non-authoritative; only valid `state=started` is treated as suspected previous unclean shutdown.
- Manual interactive Editor validation was not run in this non-interactive batch workflow.
- Developer Shell is a technical shell only; no gameplay, campaign runtime, persistence runtime, networking runtime, telemetry, CI, Player, or IL2CPP work was added.

### Follow-up tasks

- ODY-S00-007 serialization/AOT compatibility after ODY-S00-006 reaches the required state.
- ODY-S00-008 BuildIdentity/CI ownership remains deferred.

### Self-review summary

- Scope review: Changes stay inside ODY-S00-006 allowed paths; out-of-scope Unity `ProjectSettings/ProjectSettings.asset` churn from batchmode was restored to HEAD.
- Architecture review: Composition root remains in `Odyssey.Unity.Client`; no DI container, `IServiceProvider`, service locator, Unity dependency lookup APIs, Domain/Rules diagnostics dependency, Persistence/Networking runtime, or gameplay ownership was introduced.
- Test review: Required ODY-S00-006 TestCase IDs are registered and covered by .NET/Unity tests plus repository guards; final `test-fast` and `test-unity` are green.
- Security/privacy review: Diagnostics are allowlisted, bounded, sanitized before sinks, and do not record raw paths, usernames, endpoints, arbitrary objects, raw exceptions, secrets, DomainEvents, or command payloads.
- Documentation/version review: No ADR, Technical Baseline, Active Baseline, package manifest/lock, ProjectSettings baseline, or version document changes were made.

## 18. Blockers, decisions, and change control

### Blockers

- None for activation. Implementation requires separate owner approval after this contract is reviewed.

### Decisions made during execution

- 2026-08-11 - Activate ODY-S00-006 only after owner merge of ODY-S00-005 PR #9; do not begin production implementation in the activation commit - Authority / approval: product owner instruction.
- 2026-08-11 - BuildId must remain unavailable/not-yet-composed until ODY-S00-008 instead of using a fabricated version string - Authority / approval: product owner instruction and ADR-007/ADR-010 sequencing.

### Approved task changes

- 2026-08-11 - Owner approved ODY-S00-005 closure and ODY-S00-006 activation contract creation on branch `feat/ody-s00-006-runtime-composition-diagnostic-shell` - Approved by: product owner.
