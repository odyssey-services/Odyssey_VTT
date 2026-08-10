# Odyssey VTT — Repository Instructions for Codex

This file applies to the entire repository. Keep it concise. Detailed architecture lives in `docs/adr/` and must be read when a task touches the corresponding area.

## 1. Mission

Build Odyssey VTT as a Windows 10/11 x64 application using Unity `6000.4.0f1`, HDRP, UI Toolkit, and the Input System.

Implement only the task that was assigned. Do not expand MVP scope, invent product behavior, or turn an implementation task into an architectural redesign.

## 2. Authority and context

Use sources in this order:

1. Explicit current decision from the product owner in the task.
2. `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`.
3. Accepted ADR for the technical question.
4. `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.3.md`.
5. Task-specific requirement excerpts and acceptance criteria.
6. Public subsystem documentation explicitly named by the task.
7. Product Requirements, MVP Scope, Domain Model, Vision, Roadmap, Test Strategy.

`AGENTS.md` summarizes operational rules. It does not override an accepted ADR.

Never use these as requirements unless the task explicitly names them:

- `DOCUMENTATION_ALIGNMENT_CHANGELOG_*`;
- handoff files;
- `LegacyReference/**`;
- templates or placeholder evidence;
- private documentation not included in the task bundle.

Private product documentation is stored outside the authoritative code repository. Do not copy private excerpts into commits, issues, pull requests, CI logs, test snapshots, or generated artifacts.

## 3. Task contract

Create repository task contracts from `docs/tasks/TASK_TEMPLATE.md`. Filled tasks live under `docs/tasks/active/` and move to `docs/tasks/completed/` after review. A task contract is subordinate to the Active Baseline and ADRs; it cannot grant an architectural or scope exception.

Before editing, identify:

- Task ID;
- goal and acceptance criteria;
- in-scope paths;
- out-of-scope work;
- relevant Requirement IDs and ADRs;
- required validation commands.

Create or update an execution plan before coding when the task:

- touches more than one production module;
- changes a public contract;
- adds or changes persistence, migration, network, security, permissions, or redaction behavior;
- adds a dependency;
- changes Unity or package versions;
- has multiple logical stages.

For a small, single-module change, a brief plan in the task response is sufficient unless `PLANS.md` requires more.

If a required product decision is genuinely missing, stop before inventing it. Record the unresolved point and the smallest safe next step.

## 4. Repository layout

Expected top-level areas:

```text
Assets/Odyssey/                 Unity client, UI, scenes, settings and Unity tests
Packages/com.odyssey.domain/   Domain
Packages/com.odyssey.rules/    Rules
Packages/com.odyssey.content/  Content contracts and execution
Packages/com.odyssey.application/ Application use cases and ports
Packages/com.odyssey.persistence/ Persistence adapters
Packages/com.odyssey.networking/ Networking adapters
DotNet/                         Pure .NET solution and tests
Tests/                          Shared fixtures, contracts and evidence
scripts/                        Repository entry-point scripts
config/                         Compatibility and build configuration
docs/adr/                       Accepted architectural decisions
docs/tasks/                     Task template, active contracts and completed history
```

Production source has one physical copy. Do not duplicate Core source between Unity and .NET projects.

## 5. Module boundaries

Follow ADR-001 exactly.

Allowed production dependency direction:

```text
Domain
Rules        -> Domain
Content      -> Domain, Rules
Application  -> Domain, Rules, Content
Persistence  -> Domain, Content, Application
Networking   -> Domain, Content, Application
Unity Client -> all modules as composition root
```

Hard rules:

- No cyclic references.
- Domain has no Unity, database, network, file-system, serializer, logging, or infrastructure dependency.
- Rules has no UI, persistence, networking, or Unity dependency.
- Persistence and Networking never reference each other.
- Unity Client does not own authoritative campaign state.
- Do not create generic `Common`, `Shared`, `Utils`, or service-locator modules to bypass ownership.
- A boundary change requires an ADR, not an incidental code edit.

## 6. Commands and authoritative state

Follow ADR-002.

- All authoritative mutations enter through an Application command.
- The host is authoritative.
- `CommandId` is the idempotency key.
- Reusing a `CommandId` with different semantic content or actor is an error.
- One authoritative transaction has one root command.
- A command handler must not invoke another command handler.
- A command may atomically produce an ordered event batch and update multiple aggregates.
- `Accepted`, `Pending`, and `Rejected` are durable command outcomes.
- A technical failure before a durable outcome is an outer `Result` failure, not `Rejected`.
- Never mutate or delete persisted DomainEvents. Correct state with a compensating command and new events.
- Never send raw DomainEvents to clients. Build audience-filtered transport projections first.

## 7. Serialization and persistence boundaries

Follow ADR-003.

- Use `System.Text.Json` with explicit, versioned DTOs and source-generated contexts for production contracts.
- Do not serialize Domain aggregates directly as file, database, or network contracts.
- Do not use CLR type or assembly names as contract identifiers.
- Keep database schema, event payload, network protocol, manifest, and application versions independent.
- Persist old event payloads unchanged; read them through pure upcasters.
- Use SQLite constraints and typed columns for searchable/invariant data; JSON is not a replacement for relational integrity.
- Do not embed binary assets as base64 JSON.
- Enforce input size, depth, count, duplicate-property, checksum, and compatibility limits.
- Serialization must pass Mono, pure .NET, and Windows IL2CPP compatibility tests where applicable.

## 8. Result and error model

Follow ADR-004.

- Application boundaries return `Result` or `Result<T>`.
- Do not use `null`, `false`, a string, or an exception as a normal failure contract.
- Expected validation, authorization, rule, conflict, compatibility, and not-found outcomes are typed errors.
- Exceptions are for unexpected faults and are translated at an outer boundary.
- Keep internal `ErrorCode` separate from safe client-facing `SafeReasonCode`.
- Never expose stack traces, SQL, absolute paths, secrets, hidden gameplay data, or internal exception text to users.
- Respect the exact `RetryDirective`; do not retry blindly.

## 9. Dependency composition and Unity lifecycle

Follow ADR-005.

- The only production composition root is in `Odyssey.Unity.Client`.
- Use explicit constructor injection by default.
- Do not add a DI framework without an accepted ADR and an explicitly approved dependency.
- No mutable global service registries, static `Instance`, or service locator.
- Do not resolve services with `FindObjectOfType`, `GameObject.Find`, or ScriptableObject registries.
- Respect Process, Campaign, Session, Operation, and Presentation lifetimes.
- Every disposable or asynchronous resource has one clear owner.
- Startup failure must clean up already-created resources in reverse order.
- Shutdown must be safe to call more than once.
- Scene presenters and ViewModels release subscriptions when the scene/presentation scope closes.

## 10. Time and randomness

Follow ADR-008.

- Do not use `DateTime.Now`, direct wall-clock calls, `UnityEngine.Time`, `Task.Delay`, `System.Random`, or `UnityEngine.Random` in authoritative logic.
- Use injected host UTC clock, monotonic clock, scheduler, WorldClock, and authoritative RNG ports.
- Persist durable deadlines as UTC instants and re-check state through a new command when a timer fires.
- Timer callbacks never mutate domain state directly.
- Authoritative random decisions use independent streams derived from the campaign RNG key, command identity, decision ordinal, purpose, and ruleset version.
- Duplicate command handling and event replay must never reroll.
- Never log, serialize to clients, or expose the campaign RNG secret.

## 11. Logging and diagnostics

Follow ADR-010.

- Domain and Rules do not depend on logging.
- Use structured events with registered `EventCode` and allowlisted typed properties.
- Never log arbitrary objects or call `ToString()` on commands, events, DTOs, exceptions, or user content for diagnostics.
- Redact before every sink, not after writing.
- Never log secrets, tokens, owner keys, RNG keys, private messages, hidden GM data, hidden tokens, fog data, personal data, or full local paths.
- Propagate `CorrelationId` and use `DiagnosticId` for unexpected failures.
- Diagnostic logging is not the Game Log, event journal, audit history, or campaign state.
- Remote telemetry and automatic crash upload are outside MVP.

## 12. Unity and package baseline

Follow ADR-009.

- Required Editor: Unity `6000.4.0f1` (`8cf496087c8f`).
- Target: Windows Standalone x86-64.
- Render pipeline: HDRP only.
- Runtime UI: UI Toolkit.
- Input: Input System package; legacy Input Manager is disabled.
- Graphics API order: D3D12, then D3D11 fallback. Do not enable Auto Graphics API.
- `Bootstrap.unity` has build index 0 and creates the single application runtime.
- `AppShell.unity` is loaded by Bootstrap.
- Development builds may use Mono; RC and Release use IL2CPP x64.
- Do not update Unity, packages, HDRP assets, build profiles, or scripting backend as unrelated cleanup.
- Package versions must be pinned. Preview, experimental, floating, or unsigned/unverifiable packages are prohibited.

## 13. Dependencies and licensing

The authoritative repository is Private and the code is All Rights Reserved.

- Do not add a production or development dependency, GitHub Action, executable, or downloadable tool unless the task explicitly allows it or an ADR approves it.
- Allowed licenses by default: MIT, BSD, Apache-2.0, Unity Companion License.
- GPL, AGPL, unclear, custom-restrictive, or incompatible licenses require explicit owner approval.
- Update `THIRD_PARTY_NOTICES.md` for every accepted third-party dependency.
- Never commit paid, proprietary, or redistribution-restricted assets.
- Never commit credentials, secrets, tokens, private keys, local machine configuration, or private task bundles.

## 14. Testing and dual compilation

Follow ADR-006 and the Test Strategy.

Minimum maintained suites:

```text
Odyssey.Tests.Unit
Odyssey.Tests.Domain
Odyssey.Tests.Contracts
Odyssey.Tests.Architecture
Odyssey.Tests.Unity.EditMode
Odyssey.Tests.Unity.PlayMode
```

Rules:

- Core production source must compile both through Unity `.asmdef` and the pure .NET bridge projects.
- Do not create a second copy of production source for tests.
- Add or update tests for changed behavior.
- Deterministic tests use fake clocks, schedulers, RNG vectors, and isolated temporary storage.
- Do not use arbitrary sleep/delay to make tests pass.
- Do not auto-update golden files or snapshots merely to obtain a green build.
- A flaky failure must be investigated; automatic retries must not hide the first failure.
- Test assemblies, TestKit, mocks, and fixtures must not enter the Player build.
- New mandatory tests receive stable TestCase IDs when required by traceability rules.

## 15. Required repository commands

Use repository scripts as the canonical entry points. Do not replace them with private one-off commands in CI.

```powershell
./scripts/bootstrap.ps1
./scripts/restore.ps1
./scripts/format.ps1
./scripts/verify-format.ps1
./scripts/test-fast.ps1
./scripts/test-all.ps1
./scripts/test-unity.ps1
./scripts/build-dev.ps1
./scripts/build-release.ps1
./scripts/verify-docs.ps1
./scripts/verify-repository.ps1
```

For normal implementation work, run the task-required commands plus at least:

```powershell
./scripts/verify-format.ps1
./scripts/test-fast.ps1
./scripts/verify-repository.ps1
```

Run broader checks when relevant:

- Unity/package/scene change: `test-unity.ps1` and `build-dev.ps1`.
- Serialization, RNG, AOT, linker, or Player-runtime change: Windows IL2CPP validation required by the task/ADR.
- Documentation/ADR change: `verify-docs.ps1`.
- Release work: `test-all.ps1` and `build-release.ps1` plus release gates.

During initial repository scaffolding, a task may create these scripts. If a required script does not exist yet, report it as not run; never claim success.

## 16. Git and pull requests

- Work in a task branch. Never commit directly to `main`.
- Never force-push or delete `main`.
- One pull request should solve one approved task.
- Do not perform unrelated cleanup, package upgrades, mass formatting, or speculative refactoring.
- Commit messages and PR descriptions must not contain private requirement text.
- Explain binary changes and verify Git LFS pointer integrity.
- Codex may create commits and open a pull request when requested, but must never merge it.
- A green CI run is required but does not replace owner review.

## 17. Definition of done

Before handoff:

1. Re-read the task acceptance criteria and relevant ADRs.
2. Review the complete diff for scope, architecture, hidden-data leakage, and accidental files.
3. Run all required validation commands.
4. State exactly which checks passed, failed, or were not run.
5. Do not claim a test passed without command evidence.
6. Summarize behavior changed, files changed, migrations/contracts affected, risks, and remaining limitations.
7. Provide the pull request or patch for owner review.
8. Do not merge.

## 18. Code review rules

Flag as blocking:

- module dependency violations or cycles;
- authoritative state in Unity components;
- direct Persistence↔Networking coupling;
- command handler calling another command handler;
- missing idempotency or reroll on retry/replay;
- direct Domain serialization;
- hidden/private data in transport, logs, tests, or artifacts;
- unapproved dependencies or license risk;
- global clock/RNG/service locator usage;
- swallowed exceptions, unsafe retry loops, or user-visible internal details;
- tests removed, weakened, skipped, or made flaky to accommodate a change;
- Unity/package/version changes outside task scope;
- private documentation committed to the authoritative repository;
- claims of validation without evidence.

When a safe correction is clear and in scope, implement it. Otherwise report the issue with the relevant ADR and do not invent a new architecture.
