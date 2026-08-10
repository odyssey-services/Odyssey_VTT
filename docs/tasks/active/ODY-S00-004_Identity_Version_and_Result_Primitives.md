# ODY-S00-004 - Identity, Version and Result Primitives

**Status:** In Review  
**Roadmap stage / slice:** SLICE-00  
**Owner:** Codex  
**Requested by:** Product owner  
**Branch:** `feat/ody-s00-004-identity-version-result-primitives`  
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/8  
**ExecPlan:** `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`  
**Created:** 2026-08-10  
**Last updated:** 2026-08-10 21:55 UTC

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
- Canonical `Error` semantics must account for ADR-004 fields: `ErrorCode`, `ErrorCategory`, `SafeReasonCode`, `UserMessageKey`, bounded allowlisted `SafeMessageArguments[]`, `RetryDirective`, `CorrelationId`, bounded `ValidationDetails[]`, optional `DiagnosticId`, and optional bounded allowlisted machine-safe `Metadata[]`. A separate public type is not required for every field unless the clean model needs it, but these semantics must not be silently dropped.
- ADR-004 ErrorCode registry contract:
  - create `docs/errors/ERROR_CODES.md` during the ODY-S00-004 implementation PR, not during this activation PR;
  - every actually introduced `ErrorCode` is registered;
  - every registry entry records at least Code, Owner module, Category, default SafeReasonCode, default RetryDirective, Introduced version, Deprecated/reserved status, security notes, and test reference;
  - duplicate `ErrorCode` values are forbidden;
  - deprecated or reserved codes are not reused;
  - an `ErrorCode` cannot be added without registry and test updates;
  - do not pre-register future persistence, networking, or gameplay codes that ODY-S00-004 does not actually use.
- ADR-007 value-level SemVer primitives only:
  - `ApplicationVersion` in `Packages/com.odyssey.application/Runtime/**`;
  - `RulesetVersion` in `Packages/com.odyssey.rules/Runtime/**`;
  - `ContentPackageVersion` in `Packages/com.odyssey.content/Runtime/**`.
- `RulesetVersion` and `ContentPackageVersion` scope is limited to value primitives and minimal validation. It does not allow Rules behavior, formulas, content definitions, content execution, package publishing behavior, or content/rules runtime implementation.
- Integer compatibility dimensions are deferred and must not be implemented in ODY-S00-004: `DatabaseSchemaVersion`, `CampaignFormatVersion`, `ManifestSchemaVersion`, `AssetManifestVersion`, `ContractVersion`, `FingerprintVersion`, `NetworkProtocolVersion`, `AssetProtocolVersion`, and `AudioProtocolVersion`.
- Registries and validation directly required by these primitives, such as minimal ErrorCode/SafeReasonCode/UserMessageKey registration and version/identity deterministic vectors.
- Generalize test catalog validation during the ODY-S00-004 implementation PR. The guard must not hardcode a single `taskId`; every catalog entry must have a `taskId` whose task contract exists under `docs/tasks/active/<TASK>.md` or `docs/tasks/completed/<TASK>.md`, and both ODY-S00-003 and ODY-S00-004 catalog entries must remain valid together.
- Pure .NET unit/contract/domain tests for construction, invalid/default behavior, equality, hash code, canonical string formatting, parse success/failure, Result invariants, Error safe fields, retry directives, validation detail shape, and version parsing.
- Unity compatibility validation through existing Unity EditMode/PlayMode test assemblies when required to prove compile parity.
- Updates to `Tests/Metadata/test-catalog.json` for the new stable TestCase IDs.

### Out of scope

- Command dispatcher, command handlers, command gateway, command receipt store, command fingerprint implementation, `CommandResult` processing, command/event lifecycle, DomainEvent envelope, event batching, transaction boundaries, and duplicate command behavior; these belong to ODY-S00-005.
- `CommandId`, `DomainEventId`/`EventId`, and `TransactionId` lifecycle, generation policy, or value primitive implementation; these belong to ODY-S00-005.
- A separate Core `IdempotencyKey` for Application commands; ADR-002 defines `CommandId` as the canonical idempotency key.
- Clock, scheduler, RNG, retry timers, backoff algorithms, or deterministic RNG vectors.
- Persistence, networking, SQLite, transport DTOs, JSON serialization contracts, source-generated JSON contexts, upcasters, protocol envelopes, and wire/persistence mappings.
- `version.json` generation, `config/compatibility.json` generation, Git metadata readers, build numbers, release tags, artifact names, checksums, generated C# identity, runtime `build-identity.json`, `BuildIdentity` generation, Player presentation, startup log identity, CI build identity, or Windows Player build.
- Integer compatibility dimensions: `DatabaseSchemaVersion`, `CampaignFormatVersion`, `ManifestSchemaVersion`, `AssetManifestVersion`, `ContractVersion`, `FingerprintVersion`, `NetworkProtocolVersion`, `AssetProtocolVersion`, and `AudioProtocolVersion`.
- Runtime composition, `AppRuntime`, Developer Shell, diagnostics runtime, log sinks, redaction runtime, localization implementation, UI behavior, gameplay behavior, and content/rules behavior.
- Rules behavior, formulas, content definitions, content execution, and package publishing behavior.
- New test projects, new production modules, `Common`/`Shared`/`Utils` modules, new third-party dependencies, GitHub Actions, ADR changes, Technical Baseline changes, Unity package/version changes, or ProjectSettings changes.

### Allowed paths

```text
Packages/com.odyssey.domain/Runtime/**
Packages/com.odyssey.application/Runtime/**
Packages/com.odyssey.rules/Runtime/**
Packages/com.odyssey.content/Runtime/**
DotNet/Tests/Odyssey.Tests.Unit/**
DotNet/Tests/Odyssey.Tests.Domain/**
DotNet/Tests/Odyssey.Tests.Contracts/**
DotNet/Tests/Odyssey.Tests.Architecture/**
Assets/Odyssey/Client/Tests/EditMode/**
Assets/Odyssey/Client/Tests/PlayMode/**
Tests/Metadata/test-catalog.json
docs/errors/ERROR_CODES.md
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

`Packages/com.odyssey.rules/Runtime/**` and `Packages/com.odyssey.content/Runtime/**` are allowed only for `RulesetVersion`, `ContentPackageVersion`, and their minimal validation. They do not authorize rules formulas, content definitions, content execution, or package publishing behavior.

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

## 6. Technical constraints

- Module ownership and dependency direction: Follow ADR-001 exactly. Domain owns only domain meaning and invariants. Application owns `Result/Error` and application boundary primitives. Do not create `Common`, `Shared`, `Utils`, or another production module.
- Public API: Keep types `internal` by default. Make a type `public` only when it is a real intermodule contract required by the module graph. Do not add public markers or broad `InternalsVisibleTo` between production modules.
- Authoritative-state and transaction boundary: No authoritative mutation, command processing, transaction handling, persistence, networking, or gameplay state change is introduced.
- Serialization / compatibility boundary: Do not create ADR-003 DTOs, converters, source-generated JSON contexts, golden serialization snapshots, or persistence/network contracts. Future serialization implications may be documented in code comments/tests only when needed to protect canonical parse/format behavior.
- Identity rule: A typed ID value type is separate from any generator. Do not call `Guid.NewGuid` or define production generation policy unless an accepted authority explicitly assigns it to this task.
- Identity preflight rule: Before creating typed IDs, implementation must add a table to task evidence with columns `Candidate`, `Authority`, `Owner module`, `Implement / Defer`, and `Reason`, using TDB section 15.1 and accepted ADRs. `CommandId`, `DomainEventId`/`EventId`, and `TransactionId` remain ODY-S00-005. A separate Core `IdempotencyKey` must not be created for Application commands because ADR-002 defines `CommandId` as the canonical idempotency key. `CorrelationId` and `DiagnosticId` remain in scope. If a required typed ID has no unambiguous owner, stop before adding the type.
- Version rule: Implement only `ApplicationVersion`, `RulesetVersion`, and `ContentPackageVersion` value-level parsing/formatting/comparison primitives allowed by ADR-007. Do not create or mutate version sources of truth, integer compatibility version dimensions, generated build identity artifacts, or compatibility config files in this task.
- Result/Error rule: `Result<T>` has exactly two states, `Success` and `Failure`; `Failure` always has an `Error`; `null`, empty strings, `false`, and exceptions are not normal expected-failure contracts. Safe message arguments, validation details, and optional metadata must be bounded and allowlisted; unrestricted dictionary/object payloads are forbidden.
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
- `Error` does not contain raw rejected values, arbitrary objects, unrestricted dictionaries, stack traces, SQL, full paths, secrets, hidden IDs, or raw exception text.
- `SafeMessageArguments[]`, `ValidationDetails[]`, and optional `Metadata[]` are bounded and allowlisted.
- Version primitives do not generate or mutate `version.json`, compatibility config, BuildIdentity, release tags, or CI artifacts.
- Test assemblies and test helpers do not enter Player runtime assemblies.

## 8. Deliverables

- Production code: Minimal primitives under `Packages/com.odyssey.domain/Runtime/**`, `Packages/com.odyssey.application/Runtime/**`, `Packages/com.odyssey.rules/Runtime/**`, and/or `Packages/com.odyssey.content/Runtime/**` according to explicit ownership and this task's version-primitive limits.
- Tests: Focused .NET tests in existing Unit/Domain/Contracts projects and architecture tests only when needed to prove a boundary rule; Unity compatibility tests only in existing EditMode/PlayMode assemblies when needed.
- Scripts / CI: Updates to existing repository scripts only if required for real validation of the new primitives; no new CI. During implementation, `scripts/verify-test-structure.ps1` must generalize catalog validation for multiple task IDs, and after `docs/errors/ERROR_CODES.md` is created, `scripts/check-repository-policy.ps1` must count it as a required repository path.
- Configuration: Test catalog metadata for new TestCase IDs; no Unity package or ProjectSettings changes.
- Documentation: Create `docs/errors/ERROR_CODES.md` during implementation and register each actually introduced ErrorCode; update ODY-S00-004 completion evidence when implementation finishes, plus parent task, backlog, ExecPlan, and README status if materially affected.
- Generated evidence or build artifacts: Command output summaries and local test report paths only; no tracked generated build artifacts.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. Every new production primitive has a documented owner module before implementation and lives only in that owner's `Packages/com.odyssey.<module>/Runtime/**` path.
2. Before adding identity primitives, task evidence contains the required candidate preflight table and the implemented identity primitives cover only the authority-required ODY-S00-004 set, define underlying representation, equality, hash code, canonical string format, parse success/failure, invalid/default behavior, and future serialization implications without adding generators, speculative IDs, `CommandId`, `DomainEventId`/`EventId`, `TransactionId`, or a separate command `IdempotencyKey`.
3. Version primitives implement only `ApplicationVersion`, `RulesetVersion`, and `ContentPackageVersion` value-level SemVer-compatible behavior and do not create integer compatibility dimensions, `version.json`, compatibility config generation, Git metadata reading, BuildIdentity generation, release tagging, artifact naming, Player presentation, startup logging, or CI behavior.
4. `Result`, `Result<T>`, `Unit`, `Error`, `ErrorCode`, `ErrorCategory`, `SafeReasonCode`, `UserMessageKey`, `RetryDirective`, and required validation detail support follow ADR-004 invariants and expose no unsafe internal details. `Error` semantics include bounded allowlisted `SafeMessageArguments[]`, bounded `ValidationDetails[]`, optional `DiagnosticId`, and optional bounded allowlisted machine-safe `Metadata[]` without unrestricted dictionary/object payloads.
5. Domain and Rules remain free of Application `Result/Error`, Unity, persistence, networking, logging, serializer, clock, RNG, and infrastructure dependencies.
6. `docs/errors/ERROR_CODES.md` exists by the end of the ODY-S00-004 implementation PR, `scripts/check-repository-policy.ps1` counts it as a required repository path after creation, every actually introduced `ErrorCode` is registered with the required fields, duplicate codes are rejected, deprecated/reserved codes are not reused, and ErrorCode additions without registry/test updates are rejected.
7. Test catalog validation supports multiple task IDs without rewriting historical ODY-S00-003 entries, verifies unique TestCaseId, valid taskId, referenced task existence, referenced path existence, unique ownership, and required current test IDs.
8. No new production module, test project, third-party dependency, GitHub Action, ADR amendment, Technical Baseline amendment, Unity package/version change, ProjectSettings change, schema/format/contract/protocol/ruleset version bump, diagnostics runtime, or ODY-S00-005 implementation is introduced.
9. New stable TestCase IDs are registered in `Tests/Metadata/test-catalog.json` and are covered by real tests or repository checks.
10. Completion evidence states `ADR-004 primitive foundation implemented` and `ADR-007 value primitive subset implemented`, not that ADR-004 or ADR-007 are fully implemented.
11. Required validation commands run with real pass/fail/not-run evidence recorded before the task moves to `In Review`.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-ID-001` | .NET Unit/Domain | Identity construction, equality, hash code, canonical string, and parse success/failure for each implemented identity primitive | Pass |
| `TC-ID-002` | .NET Unit/Domain | Default, empty, malformed, non-canonical, or wrong-kind identity values are rejected deterministically | Pass |
| `TC-VERSION-001` | .NET Unit/Contracts | `ApplicationVersion`, `RulesetVersion`, and `ContentPackageVersion` have SemVer-compatible value semantics and canonical parse/format behavior | Pass |
| `TC-VERSION-002` | .NET Unit/Contracts | `ApplicationVersion`, `RulesetVersion`, and `ContentPackageVersion` are not interchangeable, do not trigger automatic bumps, and do not create `version.json`, compatibility config, or BuildIdentity | Pass |
| `TC-RESULT-001` | .NET Unit/Contracts | `Result` and `Result<T>` have exactly Success/Failure states and reject default/invalid state | Pass |
| `TC-RESULT-002` | .NET Unit/Contracts | `Error` requires code/category/safe reason/message key/safe arguments/retry/correlation/validation fields and excludes unsafe details | Pass |
| `TC-RESULT-003` | .NET Unit/Contracts | `RetryDirective` vocabulary is exact and cannot be weakened by boolean retry shortcuts | Pass |
| `TC-RESULT-004` | .NET Unit/Contracts | `ValidationDetail`, safe arguments, and optional metadata are bounded/allowlisted and reject raw values, unrestricted dictionaries, and arbitrary objects | Pass |
| `TC-ARCH-001` | Architecture script / .NET test | ADR-001 dependency graph still passes after adding primitives | Pass |
| `TC-DOTNET-001` | .NET build/test | Core bridge projects compile the same package source under `netstandard2.1` with C# 9 parity | Pass |
| `TC-UNITY-ASM-001` | Unity batchmode | Unity assembly graph compiles with the new primitives | Pass |
| `TC-UNITY-TEST-001` | Unity Test Framework | Existing Unity EditMode/PlayMode tests still run with nonzero tests and test-only assemblies | Pass |
| `TC-REPO-001` | Repository script | Repository policy, generated/private path exclusions, and SDK configuration remain enforced | Pass |

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

- Documents that must change: ODY-S00-004 task completion evidence, `docs/errors/ERROR_CODES.md`, parent task, ExecPlan, Slice-00 backlog, README status, and test catalog if materially affected by implementation.
- Documents that must not change: ADR-001 through ADR-010, Technical Development Baseline v0.3, Active Documentation Baseline v1.8 except owner-approved operational active-task pointer update, private product documents, changelogs, and handoff/context bundles.
- Application version change: No - version value primitives do not bump or create the application version source of truth.
- Schema / format / contract / protocol / ruleset version change: None.
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

ODY-S00-004 implementation is complete and in owner review. `ADR-004 primitive foundation implemented` and `ADR-007 value primitive subset implemented`. Remaining ADR-004 and ADR-007 scope stays assigned to later SLICE-00 tasks.

PR #8 review corrections are addressed: `Error` no longer has partial structural equality; public collection properties expose read-only wrappers instead of backing arrays; enum vocabularies fail fast on undefined/default values; `SafeReasonCode` is restricted to the ADR-004 core-safe vocabulary with `InvalidRequest`; `ValidationDetail` includes `ValidationSeverity`; metadata keys are checked per `ErrorCode`; `SafeMessageArgument` uses explicit trust factories; registry lifecycle/status/version/message metadata policy is machine-checked.

### Identity candidate preflight

| Candidate | Authority | Owner module | Implement / Defer | Reason |
|---|---|---|---|---|
| `CorrelationId` | TDB section 15.1; ADR-004 sections 4.3, 23.1; ODY-S00-004 scope | `Odyssey.Application` | Implement | Required on every Application boundary `Error` to correlate expected failures without exposing diagnostics internals. Typed ID only; no generator policy is introduced. |
| `DiagnosticId` | TDB section 15.1; ADR-004 sections 4.9, 23.2; ADR-010 diagnostic reference rules; ODY-S00-004 scope | `Odyssey.Application` | Implement | Required as optional opaque diagnostic reference on `Error` when a separate diagnostic record exists. Typed ID only; diagnostics runtime remains out of scope. |
| `CommandId` | ADR-002 command identity/idempotency contract; ODY-S00-004 out of scope | `Odyssey.Application` in ODY-S00-005 | Defer | Belongs to command/event/idempotency lifecycle in ODY-S00-005. |
| `DomainEventId` / `EventId` | ADR-002 event envelope contract; ODY-S00-004 out of scope | `Odyssey.Domain` / `Odyssey.Application` decision in ODY-S00-005 | Defer | Belongs to command/event envelope and event lifecycle in ODY-S00-005. |
| `TransactionId` | ADR-002 transaction boundary contract; ODY-S00-004 out of scope | `Odyssey.Application` / persistence decision in later task | Defer | Belongs to authoritative transaction handling, not primitive Result/Error foundation. |
| Command `IdempotencyKey` | ADR-002 states `CommandId` is the canonical idempotency key; ODY-S00-004 out of scope | None | Defer | Do not create a separate Core idempotency key for Application commands. |

### Changed files / areas

- `Packages/com.odyssey.application/Runtime/Identity/**`
- `Packages/com.odyssey.application/Runtime/Results/**`
- `Packages/com.odyssey.application/Runtime/Versions/**`
- `Packages/com.odyssey.rules/Runtime/Versions/**`
- `Packages/com.odyssey.content/Runtime/Versions/**`
- `DotNet/Tests/Odyssey.Tests.Unit/**`
- `DotNet/Tests/Odyssey.Tests.Architecture/**`
- `Tests/Metadata/test-catalog.json`
- `docs/errors/ERROR_CODES.md`
- `scripts/verify-test-structure.ps1`
- `scripts/check-repository-policy.ps1`
- `scripts/test-fast.ps1`
- `scripts/test-unity.ps1`
- Operational task/backlog/plan/README status updates.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\restore.ps1` | Passed | First sandbox run failed with `NU1900` because NuGet.org vulnerability index was inaccessible; rerun with approved network/escalated access passed. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-test-structure.ps1` | Passed | `TC-ARCH-001 PASS`; controlled invalid Domain->Rules, package version mismatch, and duplicate catalog ownership fixtures rejected. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-fast.ps1` | Passed | Build passed with 0 warnings/errors; TRX: `Logs/ODY-S00-004/dotnet/ody-s00-004_net10.0_20260810235209.trx` total 24, `...235210.trx` total 1, `...235211.trx` total 1, `...235213.trx` total 2; all failed 0. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-unity.ps1` | Passed | Unity `6000.4.0f1`; batch compile exit 0; EditMode total 1 passed 1 failed 0 skipped 0; PlayMode total 1 passed 1 failed 0 skipped 0. Unity-generated ProjectSettings whitespace churn was restored and is not part of this PR. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-repository.ps1` | Passed | Repository policy, architecture guard, and SDK check passed; configured/selected SDK `10.0.302`; registry lifecycle fixtures passed. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-repository-policy.ps1` | Passed | `REPO-POLICY-001` through `REPO-POLICY-005` pass; ErrorCode registry complete and machine-checkable; controlled Deprecated row/no-production PASS; production use of Deprecated row FAIL fixture; invalid SafeReason FAIL fixture; non-SemVer Introduced version FAIL fixture; missing UserMessageKey mapping FAIL fixture. |
| `dotnet build DotNet\Odyssey.Core.sln --no-restore` | Passed | 0 warnings, 0 errors. |
| `dotnet test DotNet\Odyssey.Core.sln --no-build --no-restore` | Passed | Unit 24, Contracts 1, Domain 1, Architecture 2; failed 0, skipped 0. |
| `git diff --check` | Passed | Final whitespace check passed after restoring Unity-generated ProjectSettings churn. |
| `git diff --cached --check` | Passed | Final staged whitespace check passed. |
| `git status --short --branch` | Passed | Branch `feat/ody-s00-004-identity-version-result-primitives`; final tracked diff contains only ODY-S00-004 correction files. Local Git may warn that `C:\Users\alexx/.config/git/ignore` is inaccessible; this does not indicate repository diff failure. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 through AC-11 | Passed | Identity preflight recorded; implemented `CorrelationId`/`DiagnosticId`, three SemVer value primitives, Application Result/Error foundation, ErrorCode registry, multi-task test catalog validation, focused tests, Unity compatibility, and repository policy checks. ODY-S00-005 scope was not implemented. |

### Build and artifact evidence

- Build identity: Not created.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: `Logs/ODY-S00-004/dotnet/*.trx`, `Logs/ODY-S00-004/editmode-results.xml`, `Logs/ODY-S00-004/playmode-results.xml`.

### Known limitations

- `CorrelationId` and `DiagnosticId` are value primitives only; no production generation policy is introduced.
- `CommandId`, `DomainEventId`/`EventId`, `TransactionId`, command/result lifecycle, and event envelopes remain ODY-S00-005.
- Integer compatibility version dimensions remain deferred to the corresponding owning SLICE-00 tasks.
- ADR-004 diagnostics runtime, mappings, command results, localization implementation, persistence/network adapters, and DTO serialization remain future scope.

### Follow-up tasks

- `ODY-S00-005` continues with command, event, clock, and RNG contracts after ODY-S00-004 is owner-reviewed and merged.

### Self-review summary

- Scope review: Implementation stayed within identity/version/Result/Error primitives, registry, tests, and validation scripts; ODY-S00-005 and runtime/persistence/network scope were not started.
- Architecture review: Domain and Rules remain free of Application Result/Error and Unity; no new Common/Shared/Utils module or dependency was added.
- Test review: Required ODY-S00-004 TestCase IDs are in `Tests/Metadata/test-catalog.json` and covered by .NET tests or repository checks.
- Security/privacy review: Error safe fields are bounded/allowlisted; no raw rejected values, arbitrary objects, unrestricted dictionaries, stack traces, SQL, full paths, secrets, or hidden payloads are exposed.
- Documentation/version review: No ADR, Technical Baseline, Active Baseline, Unity package/version, ProjectSettings, schema/format/contract/protocol/ruleset source, `version.json`, compatibility config, BuildIdentity, Git metadata, or build pipeline source was changed.

## 18. Blockers, decisions, and change control

### Blockers

- None currently. ODY-S00-004 is ready for owner review.

### Decisions made during execution

- 2026-08-10 - Activate ODY-S00-004 only as `Ready` during ODY-S00-003 post-merge closure; do not begin implementation until the closure PR is merged - Authority / approval: product owner instruction.
- 2026-08-10 - Implement only `CorrelationId` and `DiagnosticId` from the identity preflight; defer `CommandId`, `DomainEventId`/`EventId`, `TransactionId`, and separate command `IdempotencyKey` to ODY-S00-005 - Authority / approval: ODY-S00-004 contract, ADR-002, ADR-004.

### Approved task changes

- None.
