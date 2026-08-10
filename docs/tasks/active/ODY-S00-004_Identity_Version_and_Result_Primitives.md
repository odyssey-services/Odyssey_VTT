# ODY-S00-004 - Identity, Version and Result Primitives

**Status:** Ready  
**Roadmap stage / slice:** SLICE-00  
**Owner:** Unassigned  
**Requested by:** Product owner  
**Branch:** Not created  
**Pull request:** Not opened  
**ExecPlan:** `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`  
**Created:** 2026-08-10  
**Last updated:** 2026-08-10 19:10 UTC

## 1. Goal

Create the minimal production primitives required by later command, event, runtime, and compatibility tasks: strongly typed identity value types, value-level version primitives, and the ADR-004 Application `Result/Error` model.

This task does not implement command dispatch, domain events, gameplay, serialization DTOs, persistence, networking, runtime composition, UI behavior, build identity generation, or build pipeline automation.

## 2. Why this task exists

- Problem or dependency being addressed: ODY-S00-003 created the module/test skeleton, but later Core tasks still lack the stable primitive contracts they must share.
- Value or risk reduction: Establishes small typed contracts before command/event/runtime work can accidentally use strings, nullable states, exceptions, or ad hoc version values.
- Blocking or enabling relationship: Depends on completed `ODY-S00-003`; blocks `ODY-S00-005` and later command/event/runtime tasks.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.8.md`
- `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.3.md`, applicable typed ID, version, result/error, .NET, testing, and repository-command sections
- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/TASK_TEMPLATE.md`
- `docs/tasks/SLICE-00_BACKLOG.md`
- `docs/tasks/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
- `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
- `docs/tasks/completed/ODY-S00-003_Module_and_Test_Skeleton.md`
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`
- `docs/adr/ADR-004_Result_and_Error_Model_v1.0.md`
- `docs/adr/ADR-006_Test_Project_Structure_and_Dual_Unity_DotNet_Compilation_v1.0.md`
- `docs/adr/ADR-007_Versioning_and_Build_Identity_v1.0.md`
- `docs/adr/ADR-009_Unity_Project_and_Build_Baseline_v1.1.md`, existing Unity/package baseline only

### Requirement and test IDs

- Requirement IDs: `SLICE-00`, Milestone `M1`, PR-003A delivery group
- Existing test IDs: `TC-ARCH-001`, `TC-ARCH-002`, `TC-DOTNET-001`, `TC-UNITY-ASM-001`, `TC-UNITY-TEST-001`, `TC-REPO-001`
- New test IDs to introduce: `TC-ID-001`, `TC-ID-002`, `TC-VERSION-001`, `TC-VERSION-002`, `TC-RESULT-001`, `TC-RESULT-002`, `TC-RESULT-003`, `TC-RESULT-004`

### Task-safe private context

- Approved summary / references: Build only the public-safe primitive contracts needed by the technical skeleton. Do not copy private product documents, hidden campaign content, local handoff text, secrets, or personal paths into repository artifacts.

## 4. Verified current state

### Verified facts

- ODY-S00-003 was owner-merged through PR #6 into `main` as merge commit `5e6f5e03ef022c5d7b0e6fef559c2383796d95be` on `2026-08-10T19:07:16Z` using the GitHub merge-commit method.
- The repository contains six embedded production packages and the ADR-001 module graph from ODY-S00-003.
- `DotNet/Odyssey.Core.sln` exists and includes only the Domain, Rules, Content, and Application bridge projects.
- The existing bridge projects compile package `Runtime/**/*.cs` source directly; production source is not copied into `DotNet/`.
- Existing .NET test projects are `Odyssey.Tests.Unit`, `Odyssey.Tests.Domain`, `Odyssey.Tests.Contracts`, and `Odyssey.Tests.Architecture`.
- Existing Unity test assemblies are `Odyssey.Tests.Unity.EditMode` and `Odyssey.Tests.Unity.PlayMode`.
- The accepted Unity baseline is Unity `6000.4.0f1`, changeset `8cf496087c8f`.

### Assumptions

- No new owner decision is required to implement the minimal primitives explicitly assigned by ADR-001, ADR-004, ADR-006, ADR-007, the Technical Development Baseline, and this task.
- If implementation discovers that a candidate primitive lacks a clear owner or authority, the task stops before adding the type and records the missing decision.

## 5. Scope

### In scope

- Minimal Domain-owned typed identity/value primitives only when their business meaning belongs in `Odyssey.Domain`.
- Application-owned identity primitives required directly by ADR-004 error/result flow:
  - `CorrelationId`
  - `DiagnosticId`
- Application-owned Result/Error primitives required by ADR-004:
  - `Unit`
  - `Result`
  - `Result<T>`
  - `Error`
  - `ErrorCode`
  - `ErrorCategory`
  - `SafeReasonCode`
  - `UserMessageKey`
  - `RetryDirective`
  - `ValidationDetail`
- ADR-007 value-level version primitives only:
  - `ApplicationVersion`
  - monotonic positive integer version value types required for compatibility dimensions, with names and ownership matching ADR-007 where implemented
  - `RulesetVersion` and `ContentPackageVersion` SemVer value types only if the implementation can assign their owner without adding content/rules behavior
- Registries and validation directly required by these primitives, such as minimal ErrorCode/SafeReasonCode/UserMessageKey registration and version/identity deterministic vectors.
- Pure .NET unit/contract/domain tests for construction, invalid/default behavior, equality, hash code, canonical string formatting, parse success/failure, Result invariants, Error safe fields, retry directives, validation detail shape, and version parsing.
- Unity compatibility validation through existing Unity EditMode/PlayMode test assemblies when required to prove compile parity.
- Updates to `Tests/Metadata/test-catalog.json` for the new stable TestCase IDs.

### Out of scope

- Command dispatcher, command handlers, command gateway, command receipt store, command fingerprint implementation, `CommandResult` processing, command/event lifecycle, DomainEvent envelope, event batching, transaction boundaries, and duplicate command behavior; these belong to ODY-S00-005.
- `CommandId`, `DomainEventId`, and `TransactionId` lifecycle or generation policy. A bare value primitive may be added only if implementation proves it is directly required by an accepted authority for ODY-S00-004 before command processing exists.
- Clock, scheduler, RNG, retry timers, backoff algorithms, or deterministic RNG vectors.
- Persistence, networking, SQLite, transport DTOs, JSON serialization contracts, source-generated JSON contexts, upcasters, protocol envelopes, and wire/persistence mappings.
- `version.json` generation, `config/compatibility.json` generation, Git metadata readers, build numbers, release tags, artifact names, checksums, generated C# identity, runtime `build-identity.json`, `BuildIdentity` generation, Player presentation, startup log identity, CI build identity, or Windows Player build.
- Runtime composition, `AppRuntime`, Developer Shell, diagnostics runtime, log sinks, redaction runtime, localization implementation, UI behavior, gameplay behavior, and content/rules behavior.
- New test projects, new production modules, `Common`/`Shared`/`Utils` modules, new third-party dependencies, GitHub Actions, ADR changes, Technical Baseline changes, Unity package/version changes, or ProjectSettings changes.

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
scripts/restore.ps1
scripts/verify-format.ps1
scripts/verify-test-structure.ps1
scripts/test-fast.ps1
scripts/test-unity.ps1
scripts/verify-repository.ps1
scripts/check-repository-policy.ps1
docs/tasks/active/ODY-S00-004_Identity_Version_and_Result_Primitives.md
docs/tasks/SLICE-00_BACKLOG.md
docs/tasks/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md
docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md
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
Packages/com.odyssey.rules/**
Packages/com.odyssey.content/**
Packages/com.odyssey.persistence/**
Packages/com.odyssey.networking/**
Assets/Odyssey/Client/Runtime/**
Assets/Odyssey/Client/Editor/**
.github/**
version.json
config/compatibility.json
```

## 6. Technical constraints

- Module ownership and dependency direction: Follow ADR-001 exactly. Domain owns only domain meaning and invariants. Application owns `Result/Error` and application boundary primitives. Do not create `Common`, `Shared`, `Utils`, or another production module.
- Public API: Keep types `internal` by default. Make a type `public` only when it is a real intermodule contract required by the module graph. Do not add public markers or broad `InternalsVisibleTo` between production modules.
- Authoritative-state and transaction boundary: No authoritative mutation, command processing, transaction handling, persistence, networking, or gameplay state change is introduced.
- Serialization / compatibility boundary: Do not create ADR-003 DTOs, converters, source-generated JSON contexts, golden serialization snapshots, or persistence/network contracts. Future serialization implications may be documented in code comments/tests only when needed to protect canonical parse/format behavior.
- Identity rule: A typed ID value type is separate from any generator. Do not call `Guid.NewGuid` or define production generation policy unless an accepted authority explicitly assigns it to this task.
- Version rule: Implement only value-level parsing/formatting/comparison primitives allowed by ADR-007. Do not create or mutate version sources of truth, generated build identity artifacts, or compatibility config files in this task.
- Result/Error rule: `Result<T>` has exactly two states, `Success` and `Failure`; `Failure` always has an `Error`; `null`, empty strings, `false`, and exceptions are not normal expected-failure contracts.
- Unity / thread / lifetime rule: Core primitives must not depend on `UnityEngine`, Unity packages, scene lifecycle, MonoBehaviour, ScriptableObject, or Unity time/random APIs.
- Dependency / licensing rule: Do not add dependencies, packages, GitHub Actions, executables, or downloaded tools.
- Security / privacy / redaction rule: Error and validation detail values must not expose stack traces, SQL, absolute paths, secrets, hidden gameplay data, private content, raw exception text, arbitrary objects, or unrestricted dictionaries.
- Performance or platform constraint: Production Core bridge projects remain `netstandard2.1`, tests run under pinned .NET 10 test host, and Unity baseline remains `6000.4.0f1`.

## 7. Expected behavior

### Scenario 1 - Typed identities reject ambiguous values

**Given** a caller has a raw string for an identity primitive  
**When** the primitive parser receives an invalid, empty, whitespace, wrong-cased, or non-canonical value  
**Then** parsing fails deterministically without throwing for expected invalid input and without accepting a default/empty identity as valid.

### Scenario 2 - Version values are not interchangeable

**Given** version values from different ADR-007 dimensions  
**When** code compares or formats them  
**Then** each value keeps its own type, canonical string/integer representation, and validation rules without automatically bumping or inferring another version dimension.

### Scenario 3 - Result failure is explicit and safe

**Given** an Application operation cannot complete for an expected reason  
**When** it returns a failure  
**Then** the result contains one immutable `Error` with stable code/category/safe reason/message key/retry directive/correlation information and no unsafe internal details.

### Required invariants

- Production source has one physical copy under the owning package.
- `Odyssey.Domain` and `Odyssey.Rules` do not depend on Application `Result/Error`.
- `Result<T>` never has a third state and never represents failure as `null`, `false`, empty string, or exception.
- `ErrorCode`, `SafeReasonCode`, and `UserMessageKey` are separate concepts.
- Version primitives do not generate or mutate `version.json`, compatibility config, BuildIdentity, release tags, or CI artifacts.
- Test assemblies and test helpers do not enter Player runtime assemblies.

## 8. Deliverables

- Production code: Minimal primitives under `Packages/com.odyssey.domain/Runtime/**` and/or `Packages/com.odyssey.application/Runtime/**` according to ownership.
- Tests: Focused .NET tests in existing Unit/Domain/Contracts projects and architecture tests only when needed to prove a boundary rule; Unity compatibility tests only in existing EditMode/PlayMode assemblies when needed.
- Scripts / CI: Updates to existing repository scripts only if required for real validation of the new primitives; no new CI.
- Configuration: Test catalog metadata for new TestCase IDs; no Unity package or ProjectSettings changes.
- Documentation: ODY-S00-004 completion evidence when implementation finishes, plus parent task, backlog, ExecPlan, and README status if materially affected.
- Generated evidence or build artifacts: Command output summaries and local test report paths only; no tracked generated build artifacts.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. Every new production primitive has a documented owner module before implementation and lives only in that owner's `Packages/com.odyssey.<module>/Runtime/**` path.
2. The implemented identity primitives cover only the authority-required ODY-S00-004 set, define underlying representation, equality, hash code, canonical string format, parse success/failure, invalid/default behavior, and future serialization implications without adding generators or speculative IDs.
3. Version primitives implement only ADR-007 value-level behavior and do not create `version.json`, compatibility config generation, Git metadata reading, BuildIdentity generation, release tagging, artifact naming, Player presentation, startup logging, or CI behavior.
4. `Result`, `Result<T>`, `Unit`, `Error`, `ErrorCode`, `ErrorCategory`, `SafeReasonCode`, `UserMessageKey`, `RetryDirective`, and required validation detail support follow ADR-004 invariants and expose no unsafe internal details.
5. Domain and Rules remain free of Application `Result/Error`, Unity, persistence, networking, logging, serializer, clock, RNG, and infrastructure dependencies.
6. No new production module, test project, third-party dependency, GitHub Action, ADR amendment, Technical Baseline amendment, Unity package/version change, ProjectSettings change, schema/format/contract/protocol/ruleset version bump, or ODY-S00-005 implementation is introduced.
7. New stable TestCase IDs are registered in `Tests/Metadata/test-catalog.json` and are covered by real tests or repository checks.
8. Required validation commands run with real pass/fail/not-run evidence recorded before the task moves to `In Review`.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-ID-001` | .NET Unit/Domain | Identity construction, equality, hash code, canonical string, and parse success/failure for each implemented identity primitive | Pass |
| `TC-ID-002` | .NET Unit/Domain | Default, empty, malformed, non-canonical, or wrong-kind identity values are rejected deterministically | Pass |
| `TC-VERSION-001` | .NET Unit/Contracts | ApplicationVersion and implemented compatibility version values parse/format/compare according to ADR-007 | Pass |
| `TC-VERSION-002` | .NET Unit/Contracts | Different version dimensions are not interchangeable and do not trigger automatic bumps/generation | Pass |
| `TC-RESULT-001` | .NET Unit/Contracts | `Result` and `Result<T>` have exactly Success/Failure states and reject default/invalid state | Pass |
| `TC-RESULT-002` | .NET Unit/Contracts | `Error` requires code/category/safe reason/message key/retry/correlation fields and excludes unsafe details | Pass |
| `TC-RESULT-003` | .NET Unit/Contracts | `RetryDirective` vocabulary is exact and cannot be weakened by boolean retry shortcuts | Pass |
| `TC-RESULT-004` | .NET Unit/Contracts | `ValidationDetail` supports safe structured validation details without raw rejected values | Pass |
| `TC-ARCH-001` | Architecture script / .NET test | ADR-001 dependency graph still passes after adding primitives | Pass |
| `TC-DOTNET-001` | .NET build/test | Core bridge projects compile the same package source under `netstandard2.1` with C# 9 parity | Pass |
| `TC-UNITY-ASM-001` | Unity batchmode | Unity assembly graph compiles with the new primitives | Pass |
| `TC-UNITY-TEST-001` | Unity Test Framework | Existing Unity EditMode/PlayMode tests still run with nonzero tests and test-only assemblies | Pass |
| `TC-REPO-001` | Repository script | Repository policy, generated/private path exclusions, and SDK configuration remain enforced | Pass |

### Required commands

```powershell
.\scripts\restore.ps1
.\scripts\verify-format.ps1
.\scripts\verify-test-structure.ps1
.\scripts\test-fast.ps1
.\scripts\test-unity.ps1
.\scripts\verify-repository.ps1
.\scripts\check-repository-policy.ps1
dotnet build DotNet/Odyssey.Core.sln --no-restore
dotnet test DotNet/Odyssey.Core.sln --no-build --no-restore
git diff --check
git status --short --branch
```

### Manual validation

- Review every new public type against ADR-001 ownership before accepting it as public API.
- Review the diff for duplicated production source, speculative IDs, build identity generation, serialization DTOs, persistence/networking behavior, Unity package drift, private/local paths, and ODY-S00-005 scope bleed.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Unity Editor `6000.4.0f1 (8cf496087c8f)` for Unity compile/test validation; no Player profile required.
- Scripting backend: Editor/Mono-compatible checks only; IL2CPP Player validation is out of scope.
- Network topology or database fixture: None.
- Other: .NET SDK `10.0.302` with repository `global.json`, production bridge projects targeting `netstandard2.1`, and test projects targeting `net10.0`.

### Validation not required by this task

- Windows Player build, IL2CPP smoke, GitHub Actions, release/build identity generation, serialization/AOT spike, persistence integration, networking integration, runtime composition, diagnostics runtime, gameplay feature tests, migration rehearsal, and clean-checkout M1 rehearsal.

## 11. Compatibility, migration, and rollback

- Compatibility impact: Adds initial in-memory production primitives only; no persisted state, serializer DTO, public protocol, save format, or gameplay contract is introduced.
- Version fields affected: No version source of truth is changed; no application/schema/format/contract/protocol/ruleset version is bumped.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable until ADR-003/ADR-007 serialization and compatibility fixtures are implemented in later tasks.
- Rollback method: Revert the ODY-S00-004 pull request and rerun repository policy, fast tests, Unity tests, and diff checks.
- Data-loss risk and protection: None.
- Recovery rehearsal required: None.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | - | - | - | - |

Do not add production or development dependencies, GitHub Actions, executables, downloaded tools, or package/version updates in this task.

## 13. Security, privacy, and hidden information

- Data classes handled: Public-safe technical primitives, synthetic tests, safe error metadata, and repository metadata only.
- Trust boundaries: Application boundary result/error values; local repository validation scripts.
- Authorization / audience checks: No runtime permissions or audience projection is implemented; safe reason separation must not leak hidden details.
- Redaction requirements: No stack trace, SQL, absolute path, secret, token, key, hidden gameplay data, private content, raw exception text, arbitrary object, unrestricted dictionary, or raw rejected user value in public Error values.
- Log-safe fields: ErrorCode only when safe, SafeReasonCode, ErrorCategory, RetryDirective, DiagnosticId, CorrelationId, safe validation detail codes/counts, and bounded safe arguments.
- Abuse / malformed input limits: Parsers reject malformed, empty, overlong, non-canonical, wrong-kind, and default values deterministically.
- Security tests: Error safe-field tests, parser rejection tests, repository policy checks, and manual diff review.

## 14. Planning and execution mode

- Planning mode: `Brief plan`
- Reason for selected mode: The task changes foundational public primitives, but the parent SLICE-00 ExecPlan already governs sequencing; implementation should remain a single focused PR with no persistence/network/build pipeline behavior.
- ExecPlan path: `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
- Expected pull request count: 1 implementation PR after this activation PR is merged
- Milestone or sequencing constraints: Do not start ODY-S00-004 implementation until the ODY-S00-003 closure PR is owner-reviewed and merged. Work later in `feat/ody-s00-004-identity-version-result-primitives`; do not merge.

## 15. Documentation and versioning impact

- Documents that must change: ODY-S00-004 task completion evidence, parent task, ExecPlan, Slice-00 backlog, README status, and test catalog if materially affected by implementation.
- Documents that must not change: ADR-001 through ADR-010, Technical Development Baseline v0.3, Active Documentation Baseline v1.8 except owner-approved operational active-task pointer update, private product documents, changelogs, and handoff/context bundles.
- Application version change: No - version value primitives do not bump or create the application version source of truth.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: None expected; operational task/status updates only.
- Changelog or release-note requirement: Task/ExecPlan evidence only; no user-facing release note.

## 16. Definition of Done

- [ ] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [ ] Required automated tests pass.
- [ ] Required manual checks are completed.
- [ ] Required commands and their real results are recorded.
- [ ] Architecture and dependency rules remain valid.
- [ ] Security, privacy, redaction, and audience rules are verified where applicable.
- [ ] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [ ] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [ ] Documentation is updated only where materially required.
- [ ] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

ODY-S00-004 has not started. This section must be filled with real implementation evidence before the task moves to `In Review`.

### Changed files / areas

- None yet.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| Implementation validation | Not run | ODY-S00-004 implementation has not started. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 through AC-8 | Deferred | Must be proven by the ODY-S00-004 implementation PR. |

### Build and artifact evidence

- Build identity: Not created.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: Not created.

### Known limitations

- The exact minimal identity primitive set must be re-confirmed against the listed authorities before implementation; speculative IDs are not allowed.
- `CommandId`, `DomainEventId`, `TransactionId`, command/result lifecycle, and event envelopes remain ODY-S00-005 unless a bare value primitive is explicitly proven in scope before implementation.

### Follow-up tasks

- `ODY-S00-005` continues with command, event, clock, and RNG contracts after ODY-S00-004 is owner-reviewed and merged.

### Self-review summary

- Scope review: Contract only; no implementation has started.
- Architecture review: Uses ADR-001, ADR-004, ADR-006, ADR-007, and ADR-009 without introducing a new architecture rule.
- Test review: Required test IDs and commands are defined; no test pass is claimed.
- Security/privacy review: Error and validation detail safe-field constraints are explicit; private content remains prohibited.
- Documentation/version review: No version bump, ADR change, Technical Baseline change, Unity change, schema/format/contract/protocol/ruleset change, or BuildIdentity generation is authorized.

## 18. Blockers, decisions, and change control

### Blockers

- None currently. Implementation must not start until this activation PR is owner-reviewed and merged.

### Decisions made during execution

- 2026-08-10 - Activate ODY-S00-004 only as `Ready` during ODY-S00-003 post-merge closure; do not begin implementation until the closure PR is merged - Authority / approval: product owner instruction.

### Approved task changes

- None.
