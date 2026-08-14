# Odyssey VTT — SLICE-00 Technical Skeleton Backlog

**Status:** Approved execution backlog
**Slice:** `SLICE-00 — Technical Skeleton`
**Parent task:** `docs/tasks/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
**ExecPlan:** `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`
**Created:** 2026-07-28
**Last updated:** 2026-08-14 UTC

## 1. Purpose

This backlog converts the accepted Stage 1 architecture into small, reviewable repository tasks. It does not add product features. Its only outcome is a reliable private authoritative repository, a minimal Unity client, clean Core modules, deterministic contracts, tests, CI, and a Windows development build that can support `SLICE-01`.

The delivery-group labels `PR-000–PR-005` come from Technical Development Baseline section 30. They are architectural delivery groups, not guaranteed GitHub pull-request numbers. A delivery group may be split into more than one pull request when that keeps review and rollback safe, provided that:

- the final group outcome remains unchanged;
- every intermediate pull request compiles or is documentation-only;
- dependencies remain in the order defined below;
- scope is not moved into another slice;
- the parent ExecPlan and affected task contracts are updated before the split.

## 2. Slice exit criteria

`SLICE-00` is complete only when all of the following are proven:

1. A single private authoritative code repository exists and private product documentation is absent from its Git history.
2. Unity `6000.4.0f1` opens from a clean checkout with the locked package graph and no import or compile errors.
3. Core production source has one physical copy and compiles in both Unity and pure .NET.
4. ADR-001 dependency direction is enforced automatically.
5. At least one test operation uses the accepted command, result, event, idempotency, clock, RNG, and serialization contracts.
6. Stable error codes and safe user-facing failure data exist.
7. Startup, shutdown, diagnostics, and redaction scaffolds are functional without creating authoritative gameplay state in Unity objects.
8. Canonical JSON and deterministic compatibility vectors pass in pure .NET, Unity Mono, and Windows IL2CPP x64.
9. A Windows Development-Debug build is created by repository scripts and exposes BuildIdentity in the client and logs.
10. Required CI checks block an invalid pull request.
11. The `SLICE-00` quality report and traceability evidence are complete and owner-reviewed.

## 3. Ordered backlog

| Order | Task ID | Delivery group | Title | Status | Depends on | Planning mode | Primary result |
|---:|---|---|---|---|---|---|---|
| 1 | `ODY-S00-001` | PR-000 | Repository Foundation | Done | None | Brief plan | Private authoritative repository policy, repository-safe documentation subset, Git/LFS baseline and contribution/security files |
| 2 | `ODY-S00-002` | PR-001 | Unity Project Foundation | Done | 001 | ExecPlan update | Unity 6000.4/HDRP project, package lock, settings, Bootstrap and AppShell assets |
| 3 | `ODY-S00-003` | PR-002 | Module and Test Skeleton | Done | 002 | ExecPlan update | Embedded modules, `.asmdef`, dual .NET compilation, test projects and architecture guard |
| 4 | `ODY-S00-004` | PR-003A | Identity, Version and Result Primitives | Done | 003 | Brief plan | Typed IDs, version value objects, `Result/Error`, registries and unit tests |
| 5 | `ODY-S00-005` | PR-003B | Command, Event, Clock and RNG Contracts | Done | 004 | ExecPlan update | Deterministic test operation, idempotency contracts, virtual time and RNG vectors |
| 6 | `ODY-S00-006` | PR-003C | Runtime Composition and Diagnostic Shell | Done | 005 | ExecPlan update | Manual composition, process lifecycle, minimal UI shell, structured safe diagnostics |
| 7 | `ODY-S00-007` | PR-004 | Serialization and AOT Compatibility Spike | Done | 005, 006 | ExecPlan update | Explicit canonical JSON codecs, invalid-input tests and Mono/IL2CPP parity evidence |
| 8 | `ODY-S00-008` | PR-005A | Fast CI and Build Identity | Done | 003–007 | ExecPlan update | No-secret CI gates, mandatory local Unity merge validation, version generation, provenance, repository-policy gates, diagnostic bundle hardening, PR #12 and corrective PR #13 owner-merged |
| 9 | `ODY-S00-009` | PR-005B | Windows Development Build and Player Smoke | Ready | 008 | ExecPlan update | Scripted Windows x64 build, artifact/checksum, startup/shutdown and diagnostics smoke; owner-approved `TC-PLAYER-001` through `TC-PLAYER-010` mapping registered; implementation not started |
| 10 | `ODY-S00-010` | Gate | SLICE-00 Acceptance and M1 Closure | Draft | 001–009 | Brief plan | Traceability matrix, quality report, clean-checkout rehearsal and owner acceptance |

## 4. Task boundaries

### ODY-S00-001 — Repository Foundation

Align the private authoritative repository and repository-safe policy/documentation scaffold. Do not create the Unity project, .NET projects, production code, CI workflows, or product features. The owner-controlled foundation bootstrap commit `82de52e9cb47bd7a1fa8952ac5cba2b9c88456f5` entered `main` directly as a recorded one-time deviation; no history rewrite or retroactive PR is required. All subsequent substantive changes use branch → pull request → owner review → owner merge.

### ODY-S00-002 — Unity Project Foundation

Create only the exact Unity project baseline from ADR-009: editor pin, package lock, HDRP/UI Toolkit/Input System configuration, quality assets, settings, and minimal scenes. Do not create Core business contracts.

### ODY-S00-003 — Module and Test Skeleton

Create the ADR-001 module graph, shared-source dual compilation, NUnit/Unity test assemblies, architecture checks, and initial scripts. Do not implement game rules, persistence, or network behavior.

### ODY-S00-004 — Identity, Version and Result Primitives

Create foundational value objects and Application result/error contracts without command processing, RNG algorithms, persistence, transport, or UI localization implementation.

### ODY-S00-005 — Command, Event, Clock and RNG Contracts

Implement the minimum deterministic command pipeline and test operation required by M1. Use in-memory test adapters only. Do not introduce SQLite or network transports.

### ODY-S00-006 — Runtime Composition and Diagnostic Shell

Create explicit manual composition, lifecycle ownership, minimal Developer Shell, logging/redaction runtime, crash marker baseline, and clean shutdown. Do not add a DI container, telemetry, or campaign persistence.

### ODY-S00-007 — Serialization and AOT Compatibility Spike

Prove ADR-003 v1.1 compatibility with explicit DTOs, canonical JSON, hand-written Newtonsoft streaming codecs, parser ceilings, golden vectors, Mono and IL2CPP x64. The spike may not silently select a persistence or networking implementation.

### ODY-S00-008 — Fast CI and Build Identity

Create required no-secret pull-request checks, exact toolchain validation, source-inventory parity, formatting/tests, version/build identity generation, package integrity checks, local Unity merge evidence, and artifact provenance. Do not publish a Release.

### ODY-S00-009 — Windows Development Build and Player Smoke

Create and run the repository-controlled Windows Development-Debug build, package the artifact, verify startup/AppShell/version/logging/shutdown behavior, and record checksums. Release-Candidate distribution remains out of scope.

### ODY-S00-010 — SLICE-00 Acceptance and M1 Closure

Perform a clean-checkout rehearsal, reconcile all acceptance criteria and TestCase IDs, complete the quality report, record unrun/non-required checks, and obtain owner review. This task does not add missing functionality inline; failures create or reopen explicit tasks.

## 5. Dependency rules

- Tasks execute in order unless the parent ExecPlan records a safe parallelization decision.
- `ODY-S00-004` may begin only after the module graph and test host exist.
- `ODY-S00-006` may not invent persistence/network adapters; only explicitly permitted in-memory/developer adapters are allowed.
- `ODY-S00-007` must finish before the slice can claim IL2CPP compatibility.
- `ODY-S00-008` may be developed incrementally, but required status checks become authoritative only after the commands they invoke exist.
- `ODY-S00-008` owns ADR-010 `TC-DIAG-033`, `TC-DIAG-034`, `TC-DIAG-035`, `TC-DIAG-036`, `TC-DIAG-037`, `TC-DIAG-038`, `TC-DIAG-039`, and `TC-DIAG-040` after BuildIdentity is available; `ODY-S00-010` only reconciles final evidence and must not implement missing diagnostics work inline.
- `ODY-S00-010` cannot waive failed criteria. It may only close them, defer a non-blocking check already marked non-required by authority, or create a follow-up task with owner approval.

## 6. Global non-goals

The entire `SLICE-00` excludes:

- SQLite provider selection and persistent campaign state;
- `.odcamp` physical implementation beyond version/serialization scaffolding;
- network transport, relay, accounts, authentication, E2EE, or permissions runtime;
- map editor, tokens, combat, dice UI, character system, content tools, chat, or audio features;
- Addressables, installer/updater, distribution channel, remote telemetry, or crash-upload service;
- external DI, mocking, versioning, logging, or serialization frameworks unless separately approved by task and authority; ADR-003 v1.1 approves only the pinned Newtonsoft JSON codec baseline for ODY-S00-007 serialization work;
- public release or compatibility promises to end users.

## 7. Backlog change control

- New work requires a new `ODY-S00-XXX` task contract.
- A task may be split before implementation by updating this backlog and the parent ExecPlan.
- A task may not be merged with unrelated cleanup merely to reduce task count.
- Completed task files move to `docs/tasks/completed/` only after required review.
- The backlog does not replace task acceptance criteria or the parent ExecPlan.
