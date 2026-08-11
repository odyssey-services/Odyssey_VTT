# ODY-S00-006 - Runtime Composition and Diagnostic Shell

**Status:** Ready
**Roadmap stage / slice:** SLICE-00
**Owner:** Codex
**Requested by:** Product owner
**Branch:** `feat/ody-s00-006-runtime-composition-diagnostic-shell`
**Pull request:** Not opened
**ExecPlan:** `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
**Created:** 2026-08-11
**Last updated:** 2026-08-11 12:31 UTC

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
- New test IDs to introduce: `TC-COMP-001`, `TC-LIFE-001`, `TC-LIFE-002`, `TC-LIFE-003`, `TC-LIFE-004`, `TC-DIAG-001`, `TC-DIAG-002`, `TC-DIAG-003`, `TC-DIAG-004`, `TC-CRASH-001`, `TC-UNITY-SHELL-001`

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

- The minimal Developer Shell may reuse the existing synthetic non-gameplay operation from ODY-S00-005 to visibly prove runtime composition without creating gameplay.
- BuildIdentity is not available until ODY-S00-008; diagnostics must represent that state explicitly instead of fabricating a version string.

## 5. Scope

### In scope

- One production composition root in `Odyssey.Unity.Client`.
- One Unity runtime host/bootstrap owner for the process runtime.
- Explicit process lifecycle with deterministic startup phases and deterministic/idempotent shutdown.
- Startup phase sequence covering bootstrap/runtime host start, configuration/profile validation, diagnostics availability, Application/runtime graph composition, presentation shell initialization, and Ready.
- Startup failure behavior that returns safe `Result`/`Error`, records diagnostic evidence when diagnostics exist, cleans up partially owned resources, and displays a safe failure state in Developer Shell.
- Minimal Process and Presentation lifetimes only; each disposable resource created by the process root has one clear owner.
- Application-owned diagnostics contracts/runtime baseline: `IOdysseyLogger`, `LogEventV1`, `LogLevel`, `EventCode`, message template key or approved equivalent, `SafeProperty`, diagnostic context/incident contracts where needed.
- `LogLevel` vocabulary exactly: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`.
- Log event baseline fields accounting for `SchemaVersion`, `TimestampUtc`, `Level`, `EventCode`, `Subsystem`, optional/unavailable BuildId boundary, optional `CorrelationId`, optional `DiagnosticId`, `MessageTemplateKey`, `SafeProperties`, and optional safe `ExceptionSummary`.
- Allowlist-first typed/bounded safe properties and rejection of unsafe diagnostic payloads.
- Bounded in-memory diagnostic ring buffer.
- Development/Editor Unity Console diagnostic sink where appropriate.
- Minimal emergency/crash marker semantics that are intentionally trivial and do not depend on ADR-003 serialization ownership.
- Minimal UI Toolkit Developer Shell showing runtime state and diagnostic visibility.
- Minimal interactive controls to prove composition, such as executing one existing synthetic non-gameplay command, displaying safe Accepted/Rejected result, showing diagnostic ring-buffer entries, triggering a safe synthetic diagnostic event, and requesting clean runtime stop where practical in Editor.
- Lifecycle and diagnostics tests in existing .NET and Unity test assemblies.
- Updates to `Tests/Metadata/test-catalog.json`, parent ExecPlan progress/evidence, task Completion Evidence, README status if needed, and repository policy/architecture guards only where required by the task.

### Out of scope

- ODY-S00-007 implementation, serialization DTOs, canonical JSON, source-generated JSON contexts, diagnostic JSONL final format, diagnostic bundle manifest serialization, or IL2CPP diagnostic serialization proof.
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
Assets/Odyssey/Client/Tests/EditMode/**
Assets/Odyssey/Client/Tests/PlayMode/**
DotNet/Tests/Odyssey.Tests.Unit/**
DotNet/Tests/Odyssey.Tests.Architecture/**
Tests/Metadata/test-catalog.json
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
Assets/Odyssey/Client/Runtime/**/*.unity
Assets/Odyssey/Client/Runtime/**/*.prefab
version.json
config/compatibility.json
.github/**
```

Owner approval for this activation step permits only the operational `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.8.md` active-task pointer update from ODY-S00-005 to ODY-S00-006. Further edits to paths listed above require separate approval.

## 6. Technical constraints

- Module ownership and dependency direction: Follow ADR-001 exactly. Application owns diagnostics contracts/runtime baseline; Unity Client owns the production composition root and presentation shell; Domain and Rules do not depend on diagnostics/logging.
- Composition and lifecycle: Follow ADR-005. Exactly one production composition root lives in `Odyssey.Unity.Client`; use constructor injection by default; no DI container, `IServiceProvider`, `Resolve<T>()`, service locator, static mutable service registry, global `Instance` manager pattern, assembly scanning/reflection registration, or independent object graph in `Awake`/`Start`.
- Unity ownership: Unity Bootstrap may own one root runtime host. `DontDestroyOnLoad` is allowed only for the approved root runtime host or explicitly approved platform adapters. Do not use `FindObjectOfType`, `GameObject.Find`, Resources lookup, or ScriptableObject registries for application dependencies.
- Lifetime scope: Implement only minimal Process and Presentation lifetime needed by Developer Shell. Process root owns every disposable resource it creates. Shutdown runs in reverse ownership order, is safe to call more than once, and cleans up partial startup after failure.
- Startup contract: Application may not report Ready before mandatory checks succeed. Do not invent persistence/network startup phases.
- Result/error boundary: Follow ADR-004. Startup failure and unexpected exception boundaries produce safe `Result`/`Error`; public errors do not expose stack traces, absolute paths, secrets, or raw exception text.
- Time/RNG rule: Follow ADR-008. Runtime lifecycle and diagnostics use injected clocks where authoritative or test-deterministic behavior matters; no global time/random in authoritative Core logic.
- Diagnostics/redaction: Follow ADR-010. Diagnostics are allowlist-first. Strings are unsafe by default unless they pass an explicit sanitizer/trust transition. Redact before every sink, not after writing.
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

## 8. Deliverables

- Production code: Unity Client composition root/runtime host, minimal Developer Shell runtime, Application diagnostics contracts/runtime baseline, minimal crash marker baseline.
- Tests: Existing .NET and Unity test assemblies updated with ODY-S00-006 lifecycle/diagnostics coverage.
- Scripts / CI: No new CI. Existing repository checks may be extended only if required to enforce ADR-005/ADR-010 guards.
- Configuration: No Unity package/settings baseline changes.
- Documentation: Task Completion Evidence, parent ExecPlan, test catalog, and status pointers.
- Generated evidence or build artifacts: Test logs/results only.
- Migration / recovery material: Minimal local crash marker cleanup behavior; no persistent campaign migration.

## 9. Acceptance criteria

1. Exactly one production composition root exists in `Odyssey.Unity.Client`, and architecture/tests reject service locator, DI container, `IServiceProvider`, `Resolve<T>()`, mutable global registry, global `Instance`, assembly scanning registration, and application dependency lookup through Unity scene/resource search APIs.
2. Runtime startup has explicit deterministic phases and reaches Ready only after mandatory validation, diagnostics availability, runtime graph composition, and Developer Shell initialization succeed.
3. Startup failure returns safe Result/Error data, records diagnostic evidence when diagnostics exist, and cleans up partially created resources.
4. Shutdown is deterministic, reverse-order, idempotent, and disposes each owned resource at most once.
5. Developer Shell is visible/interactable in Unity Editor and shows Starting, Ready, Startup Failed, and Shutting Down states where applicable.
6. Developer Shell can prove composition through at least one existing synthetic non-gameplay command path and safe diagnostic event visibility without adding gameplay UI.
7. Application diagnostics baseline exposes the required ADR-010 fields, exact `LogLevel` vocabulary, optional/unavailable BuildId contract state, and canonical shared `CorrelationId`, `DiagnosticId`, and `UtcInstant` usage without duplicate semantic identity types.
8. Diagnostics are allowlist-first: safe typed/bounded properties pass; unsafe strings/objects/payloads/secrets/raw exceptions/stack traces/absolute paths are rejected or sanitized before every sink.
9. Bounded in-memory diagnostic ring buffer behavior is deterministic and tested.
10. Unexpected exception boundary can create a `DiagnosticId` and safe internal `ExceptionSummary` while outward Error remains ADR-004-safe.
11. Minimal crash marker detects previous unclean startup/shutdown state, clean shutdown clears/finalizes it, repeated shutdown is safe, and marker content is not authoritative application state or secret/private payload.
12. No serialization DTOs, canonical JSON, SQLite, Networking, Persistence runtime, BuildIdentity generation, telemetry, GitHub Actions, Player/IL2CPP build, gameplay, or Unity package/settings baseline changes are introduced.
13. Required validation commands have real results recorded in Completion Evidence.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-COMP-001` | Architecture / .NET | Single composition root and no service locator/DI/container/global registry/Unity lookup violations | Pass |
| `TC-LIFE-001` | .NET Unit or Unity EditMode | Startup success reaches Ready only after required phases | Pass |
| `TC-LIFE-002` | .NET Unit or Unity EditMode | Startup failure cleans partial resources and returns safe failure | Pass |
| `TC-LIFE-003` | .NET Unit or Unity EditMode | Shutdown is idempotent | Pass |
| `TC-LIFE-004` | .NET Unit or Unity EditMode | Reverse ownership disposal order is enforced | Pass |
| `TC-DIAG-001` | .NET Unit | Diagnostics allowlisted/redacted safe fields are emitted correctly | Pass |
| `TC-DIAG-002` | .NET Unit | Unsafe diagnostic data is rejected or sanitized before sinks | Pass |
| `TC-DIAG-003` | .NET Unit | Bounded ring buffer capacity/eviction is deterministic | Pass |
| `TC-DIAG-004` | .NET Unit | Unexpected boundary creates DiagnosticId and safe ExceptionSummary while outward Error is safe | Pass |
| `TC-CRASH-001` | .NET Unit or Unity EditMode | Crash marker detects unclean state, clean shutdown clears/finalizes it, repeated shutdown is safe | Pass |
| `TC-UNITY-SHELL-001` | Unity EditMode or PlayMode | Developer Shell lifecycle smoke proves visible runtime state without gameplay | Pass |

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
- SQLite, Persistence, Networking, telemetry, remote crash upload, diagnostic bundle final implementation, BuildIdentity generation, and gameplay validation: out of scope.

## 11. Compatibility, migration, and rollback

- Compatibility impact: Introduces initial runtime/diagnostic in-memory contracts only; no persisted campaign data or network protocol.
- Version fields affected: None. BuildId remains unavailable/not-yet-composed until ODY-S00-008.
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
- Security tests: `TC-DIAG-001`, `TC-DIAG-002`, `TC-DIAG-004`, `TC-CRASH-001`, plus repository policy checks.

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

- [ ] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [ ] Required automated tests pass.
- [ ] Required manual checks are completed or honestly marked not required.
- [ ] Required commands and their real results are recorded.
- [ ] Architecture and dependency rules remain valid.
- [ ] Security, privacy, redaction, and audience rules are verified.
- [ ] Compatibility, migration, rollback, and versioning obligations are complete.
- [ ] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [ ] Documentation is updated only where materially required.
- [ ] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- Activation only at task creation; production implementation has not started.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-repository-policy.ps1` | Failed then Passed | First run failed because required-path policy still pointed at active ODY-S00-005; after the activation-required path update to completed 005 and active 006, `REPO-POLICY-001` through `REPO-POLICY-005` passed. |
| `git diff --check` | Passed | No whitespace errors. |
| `git diff --name-only -- Assets/Odyssey/Client/Runtime Assets/Odyssey/Client/Editor Assets/Odyssey/Client/Tests Packages/com.odyssey.application/Runtime Packages/com.odyssey.domain/Runtime Packages/com.odyssey.rules/Runtime Packages/com.odyssey.content/Runtime Packages/com.odyssey.persistence Packages/com.odyssey.networking DotNet/Tests Tests/Metadata ProjectSettings Packages/manifest.json Packages/packages-lock.json` | Passed | No output; no ODY-S00-006 production/test/Unity implementation files or package/settings files changed during activation. |
| `Test-Path docs\tasks\active\ODY-S00-005_Command_Event_Clock_and_RNG_Contracts.md; Test-Path docs\tasks\completed\ODY-S00-005_Command_Event_Clock_and_RNG_Contracts.md` | Passed | `False`, then `True`; ODY-S00-005 exists only in completed. |
| `rg -n "ODY-S00-005.*\| Done|ODY-S00-006.*\| Ready|ODY-S00-007.*\| Draft" docs/tasks/SLICE-00_BACKLOG.md` | Passed | Backlog statuses are 005 Done, 006 Ready, 007 Draft. |
| `rg -n "docs/tasks/active/ODY-S00-005|docs/tasks/active/ODY-S00-006|current active task" ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.8.md` | Passed | Active Baseline points at `docs/tasks/active/ODY-S00-006_Runtime_Composition_and_Diagnostic_Shell.md`; no active 005 path remains. |
| `git diff --cached --check` | Passed | No staged diff errors; command printed only the local inaccessible global ignore warning. |
| `git status --short --branch` | Passed | Branch `feat/ody-s00-006-runtime-composition-diagnostic-shell`; only activation docs and required policy-path update changed. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 through AC-13 | Not started | This activation commit defines the contract only; implementation has not begun. Targeted diff check confirmed no production/test/Unity implementation files changed. |

### Build and artifact evidence

- Build identity: Not created.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: None.

### Known limitations

- Implementation has not started.
- BuildIdentity is intentionally unavailable until ODY-S00-008.
- Canonical diagnostic JSONL and source-generated diagnostic serialization are deferred to ODY-S00-007 or later owning tasks.

### Follow-up tasks

- ODY-S00-006 implementation PR after owner approval.
- ODY-S00-007 serialization/AOT compatibility after ODY-S00-006 reaches the required state.

### Self-review summary

- Scope review: Activation contract only; no production/test implementation included.
- Architecture review: Contract follows ADR-001/004/005/008/009/010 and does not introduce new architecture.
- Test review: TestCase IDs and validation plan are reserved; no tests are claimed as implemented.
- Security/privacy review: Diagnostics/redaction boundaries are explicit; unsafe data classes are prohibited.
- Documentation/version review: Operational pointer updates only; no ADR/TDB/version bump.

## 18. Blockers, decisions, and change control

### Blockers

- None for activation. Implementation requires separate owner approval after this contract is reviewed.

### Decisions made during execution

- 2026-08-11 - Activate ODY-S00-006 only after owner merge of ODY-S00-005 PR #9; do not begin production implementation in the activation commit - Authority / approval: product owner instruction.
- 2026-08-11 - BuildId must remain unavailable/not-yet-composed until ODY-S00-008 instead of using a fabricated version string - Authority / approval: product owner instruction and ADR-007/ADR-010 sequencing.

### Approved task changes

- 2026-08-11 - Owner approved ODY-S00-005 closure and ODY-S00-006 activation contract creation on branch `feat/ody-s00-006-runtime-composition-diagnostic-shell` - Approved by: product owner.
