# ODY-S00-007 - Serialization and AOT Compatibility Spike

**Status:** Ready
**Roadmap stage / slice:** SLICE-00
**Owner:** Codex
**Requested by:** Product owner
**Branch:** `feat/ody-s00-007-serialization-aot-compatibility-spike`
**Pull request:** Not opened
**ExecPlan:** `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
**Created:** 2026-08-11
**Last updated:** 2026-08-11 19:30 UTC

## 1. Goal

Prove the SLICE-00 serialization contract and serialization-specific AOT compatibility required by ADR-003, using explicit DTOs, canonical UTF-8 JSON, source-generated `System.Text.Json` contexts, compatibility fixtures, and focused .NET/Unity/IL2CPP evidence.

This is a compatibility spike only. It must not implement Persistence, Networking, gameplay, full `.odcamp` import/export, or the ODY-S00-009 Windows Development-Debug artifact.

## 2. Why this task exists

- Problem or dependency being addressed: ODY-S00-005 introduced opaque command fingerprint and event payload contracts, and ODY-S00-006 introduced diagnostics contracts, but ADR-003 serialization, canonical hash, upcasting, parser-limit, and AOT evidence is not implemented.
- Value or risk reduction: Establishes deterministic boundary serialization before Persistence, Networking, CI BuildIdentity, or Player packaging treat those contracts as stable.
- Blocking or enabling relationship: Depends on owner-merged ODY-S00-006 PR #10 and blocks ODY-S00-008/009 from claiming SLICE-00 compatibility evidence.

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
- `docs/tasks/completed/ODY-S00-006_Runtime_Composition_and_Diagnostic_Shell.md`
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`
- `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md`
- `docs/adr/ADR-003_Serialization_Strategy_v1.0.md`
- `docs/adr/ADR-004_Result_and_Error_Model_v1.0.md`
- `docs/adr/ADR-005_Dependency_Composition_v1.0.md`
- `docs/adr/ADR-006_Test_Project_Structure_and_Dual_Unity_DotNet_Compilation_v1.0.md`
- `docs/adr/ADR-007_Versioning_and_Build_Identity_v1.0.md`
- `docs/adr/ADR-008_Deterministic_Clock_and_RNG_v1.0.md`
- `docs/adr/ADR-009_Unity_Project_and_Build_Baseline_v1.1.md`
- `docs/adr/ADR-010_Logging_Diagnostics_and_Redaction_v1.0.md`
- Existing production contracts from ODY-S00-004/005/006 must be inspected before adding primitives; do not duplicate existing ID, version, result, command, event, clock, RNG, or diagnostic types.

### Requirement and test IDs

- Requirement IDs: `SLICE-00`, Milestone `M1`, PR-004 delivery group.
- Existing test IDs: `TC-CMD-*`, `TC-EVENT-001`, `TC-CLOCK-*`, `TC-RNG-*`, `TC-DIAG-*`, architecture/repository IDs from prior tasks.
- New test IDs to introduce: `TC-SER-001` through `TC-SER-024` as ODY-S00-007 task-specific serialization extensions, plus ADR-010 diagnostic IDs `TC-DIAG-001`, `TC-DIAG-007`, `TC-DIAG-029`, `TC-DIAG-030`, `TC-DIAG-031`, `TC-DIAG-032`, `TC-DIAG-041`, `TC-DIAG-042`, `TC-DIAG-043`, and `TC-DIAG-044`. Do not repurpose existing IDs.

### Task-safe private context

- Approved summary / references: Build only the public-safe serialization/AOT compatibility proof. Do not copy private product documents, hidden campaign data, local handoff text, secrets, personal paths, or private task bundles into repository artifacts.

## 4. Verified current state

### Verified facts

- PR #10 for ODY-S00-006 was owner-merged into `main` at `2026-08-11T18:52:47Z` as merge commit `abb139c3c93115c468d020db3eb423c47cfdd83b`, with merged head `b695bc09f344a36b45adb30ed7c0186bf71902d9`.
- Local `main` was fast-forwarded to `abb139c3c93115c468d020db3eb423c47cfdd83b`, and this task branch was created from that commit.
- ODY-S00-004 provides existing identity, version, Result/Error, ErrorCode, SafeReasonCode, RetryDirective, validation detail, and diagnostic ID primitives.
- ODY-S00-005 provides `ApplicationCommand`, `CommandId`, `CommandType`, `CommandVersion`, `CommandPayloadVersion`, opaque `CommandFingerprint`, `CommandResult`, `DomainEvent`, `DomainEventBatch`, clock/scheduler contracts, and RNG vector contracts.
- ODY-S00-006 provides `LogEventV1`, `EventCode`, `MessageTemplateKey`, `SafeLogProperty`, `SafeLogValue`, `ProcessInstanceId`, `ExceptionSummary`, EventCode registry, and runtime diagnostics contracts.
- `Tests/Metadata/test-catalog.json` currently has no `TC-SER-*` entries and preserves ADR-010 diagnostic IDs including `TC-DIAG-001` for `LogEventV1` JSON serialization.
- No production serialization DTOs, canonical JSON writer, source-generated contexts, contract registry, upcaster chain, JSONL diagnostic sink, or serialization fixtures exist yet.
- Repository-safe search found no `16_Test_Strategy_Odyssey_VTT_*` file in this repository. Until an accepted repository-accessible Test Strategy source proves exact `TC-SER-*` meanings, `TC-SER-001` through `TC-SER-024` are task-specific ODY-S00-007 serialization extensions, not ADR-defined IDs.

### Assumptions

- ADR-003 requires focused Windows x64 IL2CPP serialization/AOT smoke evidence for ODY-S00-007. The harness/artifact must be named `serialization-aot-smoke` or an equivalent name that cannot be confused with the ODY-S00-009 Development-Debug application build artifact.

## 5. Scope

### In scope

- `System.Text.Json` as the only default JSON serializer.
- Explicit boundary DTOs for the spike; do not serialize Domain aggregates or complete `ApplicationCommand` object graphs directly.
- Stable `ContractType` and `ContractVersion` semantics where required by ADR-003.
- Centralized serializer profiles for at least authoritative payload, diagnostics, interchange/fixture, and test fixture use.
- Source-generated `JsonSerializerContext` for release-critical serialization paths; no reflection fallback on those paths.
- Stable typed-ID JSON conversion using existing typed IDs rather than duplicating them.
- Stable UTC timestamp conversion and validation using existing `UtcInstant`.
- Stable enum-token example.
- Canonical UTF-8 JSON writer with deterministic explicit property order, no BOM, no insignificant canonical whitespace, canonical null/default semantics, duplicate-property rejection, trailing-comma/comment rejection for authoritative JSON, max depth, payload byte ceilings, NaN/Infinity rejection, `-0` normalization where applicable, and SHA-256 lowercase canonical hashes.
- ODY-S00-007 canonical implementation behind ODY-S00-005 `CommandFingerprint`.
- `CommandFingerprintMaterialV1` that excludes `CommandId`, connection IDs, retry counters, transport timestamps, compression/relay metadata, and does not serialize the complete `ApplicationCommand` graph. `ExpectedAggregateRevisions` order must be canonical.
- Minimal synthetic technical event DTO proving `ContractType`, `ContractVersion`, canonical `PayloadJson`, SHA-256 `PayloadHash`, round-trip, and hash verification. It must not be a gameplay event.
- Stored/canonical event bytes are immutable; reading/upcasting must not rewrite original fixture bytes.
- Explicit pure upcaster interface plus sample synthetic v1-to-v2 DTO fixture. Upcasters must be deterministic, pure, and use no I/O, Clock, RNG, service resolution, or runtime composition.
- Missing upcast path returns a controlled compatibility failure before mutation.
- Invalid input/parser limit tests: duplicate property, unknown `ContractType`, unknown mandatory `ContractVersion`, missing required field, invalid enum token, invalid typed ID, invalid timestamp, trailing comma, comment, depth greater than max, payload larger than ceiling, NaN/Infinity, invalid UTF-8 where applicable, and BOM policy.
- Initial parser ceilings: command payload 256 KiB, single event payload 1 MiB, manifest JSON 4 MiB, diagnostic JSON record 1 MiB.
- Diagnostic serialization ownership: `TC-DIAG-001` is exactly "Information event serializes as `LogEventV1`".
- Diagnostic parity and compatibility ownership: `TC-DIAG-041` owns .NET/Unity Mono diagnostic vector parity, `TC-DIAG-042` owns Windows x64 IL2CPP diagnostic vector parity, `TC-DIAG-043` owns unknown future major log schema compatibility error, and `TC-DIAG-044` owns duplicate JSON property rejection by the diagnostic reader.
- Rolling JSONL diagnostic sink completion is required by ADR-010 and owned by ODY-S00-007 on top of the ODY-S00-006 diagnostic runtime. Baseline: one UTF-8 JSON record per line, no BOM, UTC date-change rotation, 10 MiB size rotation, new process rotation after a previous unclean active file where applicable, and retention by the first reached limit among 10 files, 14 days, or 100 MiB total. No telemetry or upload.
- Narrow JSONL storage adapter scope is allowed only under `Packages/com.odyssey.persistence/Runtime/Diagnostics/**` for rolling JSONL diagnostic sink, rotation, retention, and diagnostic file ownership helpers. This does not authorize SQLite, database, campaign persistence, event store, schema, migration, repository implementation, outbox, backup, or general Persistence runtime work.
- `.odcamp` spike scope: versioned manifest DTO, `InterchangeJson` profile, source-generated serialization, fixture/round-trip, no secrets/absolute paths, and path-safety validation. Use the owning interchange/persistence boundary defined by accepted authorities; if exact production module ownership cannot be determined without contradicting ADR-001/ADR-003, stop and report the ownership blocker before implementation.
- Focused serialization/AOT compatibility proof in .NET, Unity Mono/EditMode, and mandatory serialization-specific Windows x64 IL2CPP smoke.
- Before substantive implementation, verify that accepted production source can compile `System.Text.Json` and `JsonSerializerContext` source generation in both `DotNet/Projects/Odyssey.Application.csproj` (`netstandard2.1`) and the Unity `6000.4.0f1` compile path. Do not add `PackageReference`, NuGet package, Unity package, DLL, analyzer, or .NET project/reference changes silently. If current references are insufficient, stop and report the exact missing assembly/reference/analyzer, affected project/Unity assembly, minimal pinned dependency/reference change, and Unity/.NET compatibility impact for owner approval.
- Contract registry: compile-time `(ContractType, ContractVersion) -> DTO metadata -> JsonTypeInfo -> validator -> mapper/upcaster`; no CLR name lookup, assembly-qualified names, reflection scanning, `IServiceProvider`, `Resolve<T>`, or arbitrary `object` deserialization.
- Test catalog entries, architecture guards, parent ExecPlan evidence, task Completion Evidence, README status if needed, and repository policy checks required by this task.

### Out of scope

- SQLite provider, database schema, migrations, current-state tables, real Persistence runtime beyond the narrow ADR-010 JSONL diagnostic storage adapter, and database round-trip implementation.
- Networking runtime, transport codec, relay, accounts/auth, permissions, protocol implementation, or network DTO finalization.
- Gameplay events, gameplay commands, campaign runtime, session runtime, operation runtime, WorldClock gameplay mechanics, map/tokens/combat/dice/characters/content/chat/audio behavior.
- Full `.odcamp` importer/exporter/archive codec/campaign DB/SQLite backup/asset streaming.
- BuildIdentity generation, GitHub Actions CI, required status checks, release artifact, installer/updater, telemetry, remote crash upload, or diagnostic upload service.
- New serializer dependency, Newtonsoft.Json, external JSON libraries, new Unity package/version changes, ProjectSettings changes, or package lock changes.
- ODY-S00-009 Windows Development-Debug build artifact, packaging, checksum, startup/shutdown Player smoke, or release build profile proof.

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Serialization/**
Packages/com.odyssey.application/Runtime/Diagnostics/**
Packages/com.odyssey.application/Runtime/Commands/**
Packages/com.odyssey.content/Runtime/Serialization/**
Packages/com.odyssey.persistence/Runtime/Diagnostics/**
Assets/Odyssey/Client/Runtime/Serialization/**
Assets/Odyssey/Client/Runtime/Diagnostics/**
Assets/Odyssey/Client/Editor/Serialization/**
Assets/Odyssey/Client/Tests/EditMode/**
Assets/Odyssey/Client/Tests/SerializationAot/**
DotNet/Tests/Odyssey.Tests.Unit/**
DotNet/Tests/Odyssey.Tests.Contracts/**
DotNet/Tests/Odyssey.Tests.Architecture/**
Tests/Fixtures/Serialization/**
Tests/Metadata/test-catalog.json
config/diagnostics/**
scripts/verify-test-structure.ps1
scripts/test-fast.ps1
scripts/test-unity.ps1
scripts/test-serialization-aot.ps1
scripts/verify-repository.ps1
scripts/check-repository-policy.ps1
docs/tasks/active/ODY-S00-007_Serialization_and_AOT_Compatibility_Spike.md
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
Packages/com.odyssey.persistence/** except Packages/com.odyssey.persistence/Runtime/Diagnostics/**
Packages/com.odyssey.networking/**
Assets/Odyssey/Client/Scenes/**
Assets/Odyssey/Client/UI/**
version.json
config/compatibility.json
.github/**
```

Owner approval for this activation step permits only the operational `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.8.md` active-task pointer update from ODY-S00-006 to ODY-S00-007. Production implementation starts only after separate owner approval.

## 6. Technical constraints

- Module ownership and dependency direction: Follow ADR-001. Domain must not acquire serializer annotations, serializer converters, `System.Text.Json` dependencies, persistence annotations, or logging dependencies.
- Serialization / compatibility boundary: Follow ADR-003. Boundary-owning modules own their DTOs and contexts; Domain aggregates are not serialized directly; CLR names are not contract identifiers.
- Command/event boundary: Follow ADR-002. `CommandFingerprint` semantic meaning remains stable; ODY-S00-007 provides canonical bytes behind the ODY-S00-005 opaque abstraction.
- Result/error boundary: Follow ADR-004. Parser, compatibility, validation, and size/depth failures return typed safe failures, not raw exceptions or internal details.
- Composition/lifetime boundary: Follow ADR-005. Registry construction is compile-time/explicit and not service-locator or reflection-scan based.
- Test boundary: Follow ADR-006. Production source has one physical copy; tests/fixtures do not enter Player builds.
- Version boundary: Follow ADR-007. Do not change application/schema/format/contract/protocol/ruleset versions except explicit task-owned synthetic contract versions.
- Time/RNG rule: Follow ADR-008. Upcasters and serialization mappers are pure and do not use injected or global clock/RNG unless explicitly serializing already-provided values.
- Unity/AOT boundary: Follow ADR-009. Focused IL2CPP evidence is mandatory for serialization compatibility and must not be labeled as the ODY-S00-009 build artifact. Use `serialization-aot-smoke` or equivalent naming.
- Domain read-only rule: ODY-S00-007 may read/reuse Domain types, but must not add `JsonPropertyName`, `JsonConverter`, `JsonSerializable`, `System.Text.Json` dependency, serialization DTO, manifest DTO, or serializer helper to `Odyssey.Domain`.
- Persistence boundary rule: Only the ADR-010 JSONL diagnostic file adapter sub-scope under `Packages/com.odyssey.persistence/Runtime/Diagnostics/**` is allowed; all other Persistence runtime/database work remains out of scope.
- Diagnostics/redaction: Follow ADR-010. Redaction happens before diagnostic serialization; `DiagnosticJson` must not expose secrets, hidden gameplay data, raw exception text, absolute paths, or arbitrary objects.
- Dependencies/licensing: No new dependency, package, GitHub Action, executable, or downloadable tool is approved.

## 7. Expected behavior

### Scenario 1 - Canonical command fingerprint

**Given** the same synthetic command semantic material with stable IDs, issuer, type/version, payload contract, and aggregate revision expectations
**When** `CommandFingerprintMaterialV1` is serialized canonically
**Then** the same lowercase SHA-256 fingerprint is produced across .NET, Unity Mono, and the focused serialization AOT proof.

### Scenario 2 - Event payload integrity

**Given** a synthetic technical event payload with `ContractType`, `ContractVersion`, canonical `PayloadJson`, and stored `PayloadHash`
**When** the payload is read and mapped/upcast in memory
**Then** the original fixture bytes remain unchanged and hash verification succeeds or fails with a controlled compatibility error.

### Scenario 3 - Invalid authoritative JSON

**Given** malformed or unsupported authoritative JSON
**When** the parser encounters duplicate properties, comments, trailing commas, invalid UTF-8, depth/size excess, unknown mandatory type/version, missing required fields, invalid typed IDs/timestamps/enums, or NaN/Infinity
**Then** parsing fails before mutation with a typed safe failure.

### Scenario 4 - Diagnostic JSON remains redacted

**Given** an allowlisted `LogEventV1` and unsafe secret/path/exception vectors
**When** the DiagnosticJson profile serializes records
**Then** only redacted allowlisted values appear, duplicate properties and ceilings are enforced, and .NET/Unity parity vectors match.

### Required invariants

- No Domain aggregate or complete `ApplicationCommand` graph is serialized directly.
- No `GetHashCode()`, object identity, runtime type name, assembly-qualified name, reflection scan, or process/runtime-dependent value is used for canonical hashes or fingerprints.
- Unknown mandatory contract type/version blocks mutation.
- Stored canonical fixture bytes are not rewritten by read/upcast paths.

## 8. Deliverables

- Production code: Minimal serialization contracts, profiles, converters, registry, canonical writer/hash helpers, mappers/upcasters, and diagnostic serialization integration needed for the spike.
- Tests: .NET, Unity EditMode/Mono, architecture, parser-limit, fixture, parity, and mandatory focused IL2CPP serialization evidence.
- Scripts / CI: Existing scripts only; may extend repository policy/architecture guards. No GitHub Actions.
- Configuration: Test fixtures and registry/config entries required by the spike only.
- Documentation: Task Completion Evidence, parent ExecPlan evidence, test catalog, README/active pointers if needed.
- Generated evidence or build artifacts: Golden fixture hashes, test logs, and mandatory focused `serialization-aot-smoke` output; no ODY-S00-009 build artifact.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. `System.Text.Json` is the only default JSON serializer used by new serialization code; no Newtonsoft.Json or external serializer is added.
2. Boundary serialization uses explicit DTOs, explicit profiles, source-generated contexts, and compile-time registry metadata without CLR type-name discriminators, assembly scanning, unrestricted polymorphism, or reflection fallback on release-critical paths.
3. Existing ODY-S00-004/005/006 primitives are reused; no duplicate ID/version/result/diagnostic primitive is introduced.
4. Canonical UTF-8 JSON produces deterministic property order, no BOM, no insignificant whitespace, canonical null/default behavior, normalized numeric edge cases, and lowercase SHA-256 hashes.
5. `CommandFingerprintMaterialV1` produces stable canonical bytes and fingerprint vectors, excludes `CommandId` and transport/retry/runtime metadata, and canonicalizes `ExpectedAggregateRevisions` order without serializing the complete `ApplicationCommand` graph.
6. A minimal synthetic technical event payload proves `ContractType`, `ContractVersion`, immutable canonical `PayloadJson`, `PayloadHash`, round-trip, and hash verification without gameplay semantics or SQLite.
7. The upcaster spike has a pure explicit interface, synthetic v1-to-v2 fixture, unchanged raw fixture bytes, deterministic transform, and controlled failure for missing paths.
8. Parser limits and invalid-input cases listed in scope fail safely before mutation.
9. `TC-DIAG-001` preserves the ADR-010 meaning exactly: Information event serializes as `LogEventV1`.
10. Diagnostic redaction/JSONL/parity/compatibility cases are proven by their own diagnostic IDs: `TC-DIAG-007`, `TC-DIAG-029`, `TC-DIAG-030`, `TC-DIAG-031`, `TC-DIAG-032`, `TC-DIAG-041`, `TC-DIAG-042`, `TC-DIAG-043`, and `TC-DIAG-044`.
11. `.odcamp` work is limited to a spike manifest DTO/fixture/path-safety proof with no full importer/exporter/archive codec or campaign data.
12. Mandatory Windows x64 IL2CPP AOT evidence is explicitly scoped to focused serialization compatibility and named `serialization-aot-smoke` or equivalent. The task does not claim the ODY-S00-009 Windows Development-Debug artifact, packaging, checksum, startup/shutdown smoke, or release build result.
13. Architecture guards prevent direct Domain serialization annotations/dependencies, arbitrary object graph serialization, service-locator registry resolution, and test fixtures entering Player builds where applicable.
14. Required validation commands and real results are recorded; unrun full .NET/Unity/IL2CPP checks are listed honestly.
15. No SQLite, Persistence runtime beyond the narrow JSONL diagnostic storage adapter, Networking runtime, gameplay, BuildIdentity, CI, Unity package/version, committed ProjectSettings change, ADR, or Technical Baseline changes are introduced.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-SER-001` | .NET Unit | ODY-S00-007 task-specific: System.Text.Json-only default and centralized profile existence | Pass |
| `TC-SER-002` | .NET Unit | ODY-S00-007 task-specific: ContractType/ContractVersion parsing and registry lookup semantics | Pass |
| `TC-SER-003` | .NET Unit | ODY-S00-007 task-specific: source-generated context is used for registered DTO roots | Pass |
| `TC-SER-004` | .NET Unit | ODY-S00-007 task-specific: stable typed-ID conversion for existing IDs | Pass |
| `TC-SER-005` | .NET Unit | ODY-S00-007 task-specific: UTC timestamp conversion/validation for `UtcInstant` | Pass |
| `TC-SER-006` | .NET Unit | ODY-S00-007 task-specific: stable enum token conversion example | Pass |
| `TC-SER-007` | .NET Unit | ODY-S00-007 task-specific: canonical UTF-8 writer property order/no BOM/no whitespace/null/default semantics | Pass |
| `TC-SER-008` | .NET Unit | ODY-S00-007 task-specific: duplicate properties, comments, trailing commas, invalid UTF-8/BOM policy rejected as specified | Pass |
| `TC-SER-009` | .NET Unit | ODY-S00-007 task-specific: max depth and payload byte ceilings enforced | Pass |
| `TC-SER-010` | .NET Unit | ODY-S00-007 task-specific: NaN/Infinity rejected and `-0` normalized where applicable | Pass |
| `TC-SER-011` | .NET Unit | ODY-S00-007 task-specific: SHA-256 lowercase canonical hash fixture stable | Pass |
| `TC-SER-012` | .NET Unit | ODY-S00-007 task-specific: `CommandFingerprintMaterialV1` inclusion/exclusion and expected revision ordering | Pass |
| `TC-SER-013` | .NET Unit | ODY-S00-007 task-specific: CommandFingerprint vector stable and not based on runtime identity/GetHashCode | Pass |
| `TC-SER-014` | .NET Unit | ODY-S00-007 task-specific: synthetic event payload canonical JSON/hash round-trip | Pass |
| `TC-SER-015` | .NET Unit | ODY-S00-007 task-specific: event read/upcast preserves original fixture bytes | Pass |
| `TC-SER-016` | .NET Unit | ODY-S00-007 task-specific: pure v1-to-v2 upcaster and missing-path controlled compatibility failure | Pass |
| `TC-SER-017` | .NET Unit | ODY-S00-007 task-specific: unknown ContractType/version and missing required fields rejected before mutation | Pass |
| `TC-SER-018` | .NET Unit | ODY-S00-007 task-specific: `.odcamp` spike manifest fixture/path safety/no secrets/no absolute paths | Pass |
| `TC-SER-019` | Architecture / script | ODY-S00-007 task-specific: Domain has no serializer annotations/dependencies and no Domain aggregate root registration | Pass |
| `TC-SER-020` | Architecture / script | ODY-S00-007 task-specific: contract registry has no CLR type-name lookup, scanning, service locator, or object graph fallback | Pass |
| `TC-SER-021` | Unity EditMode | ODY-S00-007 task-specific: Unity Mono serialization vectors match .NET vectors | Pass |
| `TC-SER-022` | Player / IL2CPP focused smoke | ODY-S00-007 task-specific: serialization-specific Windows x64 IL2CPP vectors match .NET/Mono vectors | Pass |
| `TC-SER-023` | .NET Unit | ODY-S00-007 task-specific: test fixtures/golden hashes are stable and not auto-updated | Pass |
| `TC-SER-024` | Repository policy | ODY-S00-007 task-specific: no disallowed serializer dependency, Unity package change, or production/test boundary violation | Pass |
| `TC-DIAG-001` | .NET Unit | Information event serializes as `LogEventV1` | Pass |
| `TC-DIAG-007` | JSONL sink | Secret fixture is absent from JSONL sink | Pass |
| `TC-DIAG-029` | JSONL sink | Daily UTC rotation creates a new file | Pass |
| `TC-DIAG-030` | JSONL sink | Size rotation occurs at configured 10 MiB threshold | Pass |
| `TC-DIAG-031` | JSONL sink | Retention removes oldest closed files when a limit is reached | Pass |
| `TC-DIAG-032` | JSONL sink | Active file is not deleted by retention | Pass |
| `TC-DIAG-041` | .NET + Unity | same diagnostic contract vector serializes identically in .NET and Unity Mono | Pass |
| `TC-DIAG-042` | IL2CPP focused smoke | same diagnostic contract vector serializes identically in Windows x64 IL2CPP | Pass |
| `TC-DIAG-043` | .NET Unit | unknown future major log schema returns compatibility error | Pass |
| `TC-DIAG-044` | .NET Unit | duplicate JSON property rejected by diagnostic reader | Pass |

### Required commands

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\restore.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-format.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-test-structure.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-fast.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-unity.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-serialization-aot.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-repository.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-repository-policy.ps1
dotnet build DotNet\Odyssey.Core.sln --no-restore
dotnet test DotNet\Odyssey.Core.sln --no-build --no-restore
git diff --check
git diff --cached --check
git status --short --branch
```

If the implementation uses a different repository entry point for the focused Windows x64 IL2CPP serialization smoke, record the exact command, output path, and result here before review. The output/artifact naming must include `serialization-aot-smoke` or equivalent.

### Manual validation

- Review retained fixtures and hashes to confirm they are synthetic, public-safe, and not auto-updated.
- Confirm any focused IL2CPP output is labelled ODY-S00-007 serialization evidence and not ODY-S00-009 build evidence.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Unity `6000.4.0f1`; Unity Editor/Mono plus focused serialization IL2CPP.
- Scripting backend: .NET test host, Unity Mono/EditMode, focused IL2CPP for serialization compatibility.
- Network topology or database fixture: None.
- Other: Existing .NET SDK `10.0.302`.

### Validation not required by this task

- Full ODY-S00-009 Windows Development-Debug build artifact, packaging, checksum, startup/shutdown Player smoke, and release packaging.
- GitHub Actions CI and branch protection checks; assigned to ODY-S00-008.
- SQLite database implementation, migrations, Persistence runtime outside `Packages/com.odyssey.persistence/Runtime/Diagnostics/**`, Networking runtime, transport codec, gameplay behavior, full `.odcamp` import/export, telemetry/upload, installer/updater, and BuildIdentity.

## 11. Compatibility, migration, and rollback

- Compatibility impact: Introduces initial synthetic serialization contract fixtures, canonical hashes, profile/registry behavior, and diagnostic JSON compatibility evidence for SLICE-00.
- Version fields affected: Synthetic `ContractVersion` values only where introduced by this task. Do not change application/schema/format/protocol/ruleset versions.
- Migration or upcaster: Synthetic v1-to-v2 upcaster spike only; no database migration and no user data migration.
- Forward / backward behavior: Unknown mandatory contracts fail before mutation; supported synthetic fixtures are read/upcast deterministically without rewriting original bytes.
- Rollback method: Revert the ODY-S00-007 pull request and remove task-owned fixtures/registry entries.
- Data-loss risk and protection: No user campaign data exists; fixtures are synthetic and public-safe.
- Recovery rehearsal required: Compatibility failure and invalid input tests.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | - | - | - | - |

No new dependency, package, GitHub Action, executable, or downloadable tool is approved for this task.

## 13. Security, privacy, and hidden information

- Data classes handled: Synthetic command/event payloads, synthetic manifest fixtures, diagnostic JSON records, hashes, and parser failure evidence.
- Trust boundaries: Repository fixtures, local JSON parsing, diagnostic serialization, test Player/Editor logs.
- Authorization / audience checks: No product permissions runtime exists; no gameplay, transport projection, or campaign state is serialized.
- Redaction requirements: Diagnostic serialization must use already safe/allowlisted/redacted values and must not log raw commands, DomainEvents, exceptions, hidden data, secrets, absolute paths, user names, tokens, or RNG secrets.
- Log-safe fields: Existing ADR-010 `SafeLogValue`/`SafeLogProperty` values only.
- Abuse / malformed input limits: Depth, payload byte ceilings, duplicate properties, invalid encoding, unsupported type/version, and invalid primitive tokens.
- Security tests: `TC-SER-008`, `TC-SER-009`, `TC-SER-017`, `TC-SER-018`, `TC-SER-019`, `TC-SER-020`, `TC-DIAG-001`, `TC-DIAG-007`, `TC-DIAG-043`, `TC-DIAG-044`, plus repository policy checks.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: This task changes public serialization contracts, canonical hashes/fingerprints, diagnostic serialization, compatibility fixtures, parser limits, and AOT validation.
- ExecPlan path: `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
- Expected pull request count: One implementation PR after this activation commit.
- Milestone or sequencing constraints: This activation commit creates the contract only. Production implementation begins only after owner approval.

## 15. Documentation and versioning impact

- Documents that must change: This task contract, parent ExecPlan, backlog status, Active Baseline operational pointer, README status if used as active-task pointer, test catalog during implementation, Completion Evidence.
- Documents that must not change: ADR-001 through ADR-010, Technical Development Baseline v0.3, application/schema/format/protocol/ruleset version documents unless explicit owner approval is recorded.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: No global version change; only task-owned synthetic `ContractVersion` fixtures may be introduced.
- Documentation version changes: Active Baseline v1.8 is not bumped for operational pointer updates.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [ ] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [ ] Required automated tests pass.
- [ ] Required manual checks are completed or honestly marked not required.
- [ ] Required commands and their real results are recorded.
- [ ] Architecture and dependency rules remain valid.
- [ ] Security, privacy, redaction, parser-limit, and audience rules are verified.
- [ ] Compatibility, migration, rollback, and versioning obligations are complete.
- [ ] No unapproved dependency, tool, GitHub Action, Unity package/version, or license was introduced.
- [ ] Documentation is updated only where materially required.
- [ ] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains contract changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- ODY-S00-006 task contract moved from `docs/tasks/active/` to `docs/tasks/completed/` and closure metadata updated.
- ODY-S00-007 active task contract created at `docs/tasks/active/ODY-S00-007_Serialization_and_AOT_Compatibility_Spike.md`.
- Operational pointers updated in Active Baseline v1.8, SLICE-00 backlog, parent task, parent ExecPlan, README, and repository policy required-path list.
- Contract correction updated ODY-S00-007 IL2CPP, diagnostic JSONL, TestCase ownership, Domain read-only, focused AOT harness, and System.Text.Json/source-generation feasibility requirements. Parent task, parent ExecPlan, and backlog record future ODY-S00-008 ownership for `TC-DIAG-033`, `TC-DIAG-034`, `TC-DIAG-035`, `TC-DIAG-036`, `TC-DIAG-037`, `TC-DIAG-038`, `TC-DIAG-039`, and `TC-DIAG-040`.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-repository-policy.ps1` | Passed | `REPO-POLICY-001` through `REPO-POLICY-005` passed, including controlled ErrorCode registry fixtures. |
| `git diff --check` | Passed | Exited 0; printed CRLF normalization warnings for Active Baseline and backlog only. |
| `git diff --cached --check` | Passed | Exited 0 with no staged diff errors; printed inaccessible global ignore warning only. |
| Targeted activation assertions | Passed | Verified 006 active path absent, 006 completed/Done, PR #10 merge SHA recorded, 007 active/Ready, backlog 006 Done / 007 Ready / 008 Draft, Active Baseline pointer to 007, and no production/test/config serialization implementation paths changed. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-repository-policy.ps1` | Passed | Contract-correction rerun passed `REPO-POLICY-001` through `REPO-POLICY-005`, including controlled ErrorCode registry fixtures. |
| `git diff --check` | Passed | Contract-correction rerun exited 0; printed CRLF normalization warning for `docs/tasks/SLICE-00_BACKLOG.md` only. |
| `git diff --cached --check` | Passed | Contract-correction pre-stage run exited 0 with no staged diff errors; printed inaccessible global ignore warning only. |
| Targeted contract-correction assertions | Passed | Verified 007 remains Ready, no implementation paths changed, IL2CPP is mandatory, `TC-DIAG-001` exact meaning is preserved, `TC-DIAG-007`, `TC-DIAG-029`, `TC-DIAG-030`, `TC-DIAG-031`, and `TC-DIAG-032` are assigned to 007, `TC-DIAG-033` through `TC-DIAG-040` future ownership is recorded for 008, `TC-SER-001` through `TC-SER-024` are task-specific, Domain edit paths are removed, focused IL2CPP harness paths are recorded, and System.Text.Json/source-generation dependency changes require blocker plus owner approval. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 through AC-14 | Pending | Production implementation has not started. Activation-only scope is satisfied. |

### Build and artifact evidence

- Build identity: Not created.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: None for activation.

### Known limitations

- Production serialization code, tests, fixtures, contexts, canonical writer, JSONL sink, and focused IL2CPP serialization evidence are not implemented in this activation commit.
- Focused IL2CPP serialization evidence is mandatory for implementation, but it must remain a `serialization-aot-smoke` harness/evidence path and must not claim the ODY-S00-009 application build artifact.
- Full .NET restore/build/test, Unity batch/EditMode/PlayMode, and IL2CPP validation were not run for this docs-only activation because no `Assets/`, `Packages/`, `DotNet/`, test, or serialization implementation files changed.

### Follow-up tasks

- ODY-S00-007 production implementation after owner approval.
- ODY-S00-008 BuildIdentity/CI and ODY-S00-009 Windows Development-Debug artifact remain deferred.

### Self-review summary

- Scope review: Activation-only contract; no production or test serialization implementation.
- Architecture review: Contract preserves ADR-001 ownership and does not grant Domain serializer dependencies.
- Test review: TestCase IDs are reserved/proposed without modifying tests yet.
- Security/privacy review: Fixture and diagnostic redaction requirements are explicit.
- Documentation/version review: No ADR, Technical Baseline, application/schema/format/protocol/ruleset, package, or Unity baseline change is authorized.

## 18. Blockers, decisions, and change control

### Blockers

- None for activation. Production implementation requires separate owner approval.

### Decisions made during execution

- 2026-08-11 - Activate ODY-S00-007 only after owner merge of ODY-S00-006 PR #10; do not begin production implementation in the activation commit - Authority / approval: product owner instruction.
- 2026-08-11 - ODY-S00-007 owns canonical implementation behind ODY-S00-005 `CommandFingerprint`, while full command serialization and canonical command DTO evolution beyond the synthetic spike remain later work - Authority / approval: product owner instruction and ADR-003.
- 2026-08-11 - ODY-S00-007 may provide focused serialization/AOT compatibility evidence but must not claim the ODY-S00-009 Windows Development-Debug artifact - Authority / approval: product owner instruction and ADR-003/ADR-009 sequencing.
- 2026-08-11 - Focused Windows x64 IL2CPP serialization/AOT smoke, command/event/canonical vectors, and diagnostic vector `TC-DIAG-042` are mandatory for ODY-S00-007; ODY-S00-009 retains ownership of the real Windows Development-Debug application artifact, packaging, checksum, and startup/shutdown Player smoke - Authority / approval: product owner instruction and ADR-003.
- 2026-08-11 - `TC-DIAG-001` preserves its ADR-010 meaning exactly - Authority / approval: product owner instruction and ADR-010.
- 2026-08-11 - `TC-DIAG-007`, `TC-DIAG-029` through `TC-DIAG-032`, and `TC-DIAG-041` through `TC-DIAG-044` cover the additional JSONL/parity/compatibility diagnostic scope - Authority / approval: product owner instruction and ADR-010.
- 2026-08-11 - `TC-SER-001` through `TC-SER-024` are ODY-S00-007 task-specific serialization extensions because no repository-accessible accepted Test Strategy source currently defines those exact IDs - Authority / approval: product owner instruction and repository-safe authority audit.
- 2026-08-11 - `Packages/com.odyssey.persistence/Runtime/Diagnostics/**` is allowed only for ADR-010 rolling JSONL diagnostic sink, rotation, retention, and diagnostic file ownership helpers; all other Persistence runtime/database work remains out of scope - Authority / approval: product owner instruction and ADR-010.

### Approved task changes

- 2026-08-11 - Owner approved ODY-S00-006 closure and ODY-S00-007 activation contract creation on branch `feat/ody-s00-007-serialization-aot-compatibility-spike` - Approved by: product owner.
