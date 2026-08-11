# ODY-S00-000 — Establish the SLICE-00 Technical Skeleton

**Status:** Active
**Owner:** Codex
**Branch:** `feat/ody-s00-007-serialization-aot-compatibility-spike`
**Pull request:** Not opened
**Last updated:** 2026-08-11 23:33 UTC

## 1. Purpose and user-visible outcome

When this plan is complete, Odyssey VTT has a trustworthy technical starting point rather than a collection of disconnected prototypes. A developer can clone the Private authoritative repository on Windows, restore the pinned toolchain, run fast Core tests without Unity, open the exact Unity project, run Unity tests, create a versioned Windows development build, inspect safe diagnostics, and prove that invalid architecture changes are blocked before merge.

No user-facing game feature is delivered. The observable outcome is readiness to begin `SLICE-01 — Local Campaign` without redesigning repository, module, contract, test, or build foundations inside that feature slice.

## 2. Task contract

- Goal: Deliver the complete technical skeleton defined by `SLICE-00` and Milestone M1.
- Acceptance criteria: Parent task section 9 and `docs/tasks/SLICE-00_BACKLOG.md` section 2.
- Requirement IDs: `SLICE-00`, `M1`, `TDB-DEC-001–027`, ADR-defined `SLICE-00` test IDs.
- In scope: Child tasks `ODY-S00-001–010`.
- Out of scope: Persistence, networking, accounts, permissions runtime and all gameplay/product features.
- Required authorities: Active Baseline v1.8, Technical Baseline v0.3, AGENTS, PLANS, Task Template, ADR-001–010, MVP Scope SLICE-00, Roadmap Stage 1, Test Strategy.
- Required validation commands: Introduced incrementally; final canonical set must match `AGENTS.md` and actual repository scripts.

Governing task contract: `docs/tasks/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`.

## 3. Current state

### Verified facts

- Architecture and operational documentation are prepared and internally consistent through ADR-010, AGENTS, PLANS, and the task workflow.
- Exact platform/toolchain decisions exist: Windows x64, Unity `6000.4.0f1`, HDRP, UI Toolkit, Input System, pure .NET host, GitHub Actions, Git LFS, All Rights Reserved.
- Repository foundation exists in owner bootstrap `82de52e9cb47bd7a1fa8952ac5cba2b9c88456f5`; no Unity project, package lock, `.asmdef`, `.csproj`, CI run, or Player build exists.
- The full current documentation bundle contains private product material and must not be copied wholesale into the authoritative code repository.

### Assumptions to verify during execution

- The owner-selected Private authoritative repository is `odyssey-services/Odyssey_VTT`; visibility changes require a separate owner decision.
- Windows access and a valid Unity `6000.4.0f1` installation/license are available for Unity and IL2CPP validation.
- Git LFS is available on contributor and CI machines.

## 4. Proposed approach

Execute ten child tasks in six accepted delivery groups.

1. Establish repository policy and a repository-safe technical documentation subset before any generated project files.
2. Create the exact Unity project without Core behavior so project settings and package changes remain reviewable.
3. Add embedded module packages and the pure .NET test host using one physical source set.
4. Introduce foundational contracts in three review-safe increments: values/results, deterministic command/time/RNG contracts, then runtime composition/diagnostics.
5. Prove serialization and AOT compatibility before CI treats the stack as stable.
6. Add required CI, BuildIdentity, Windows build and Player smoke, then perform clean-checkout M1 acceptance.

The approach follows these ownership boundaries:

- Domain remains pure and owns stable domain identities/value semantics.
- Application owns commands, results, ports and orchestration contracts.
- Infrastructure modules remain adapter shells only; no SQLite or real transport is introduced.
- Unity Client is the sole production composition root and presentation shell.
- TestKit and compatibility vectors remain test-only.

Every child task produces a compilable or documentation-only repository state. Any need to add a dependency, change a module edge, modify an accepted contract, or expand into later slices updates the task and this plan before code changes.

## 5. Milestones

### M0 — Repository is safe to develop in

Child task: `ODY-S00-001`.

- [x] Owner created the foundation directly on `main` in commit `82de52e9cb47bd7a1fa8952ac5cba2b9c88456f5`; repository `odyssey-services/Odyssey_VTT` remains Private by owner decision.
- [x] Closeout alignment was merged through PR #1; merge commit `9c7a61893b107624c29ecaa0af34335a715b11e3`; the original owner bootstrap remains a documented one-time deviation.
- [x] All Rights Reserved, contribution/security policy, Git LFS and repository ignores are present.
- [x] Private product documents, handoffs, changelogs, archives, secrets and local paths are absent from Git history.
- [x] Owner merge evidence is recorded. Exact branch protection/ruleset settings were inaccessible and are an owner-accepted limitation; no setting is claimed as Passed.
- Evidence: repository URL, merged PR #1, merge commit `9c7a61893b107624c29ecaa0af34335a715b11e3`, repository-policy checks, and explicit owner-accepted settings limitation.

### M1 — Exact Unity project opens cleanly

Child task: `ODY-S00-002`.

- [x] Unity `6000.4.0f1` project files and package lock exist.
- [x] HDRP, UI Toolkit, Input System, graphics APIs, serialization modes and quality assets match ADR-009 v1.1.
- [x] `Bootstrap` and `AppShell` scenes exist with no business behavior.
- [x] Clean Unity open/import/compile succeeds.
- Evidence: project settings inspection, package-lock diff, Unity Editor log, EditMode compile smoke, owner-merged PR #4, merge commit `70e7d49e217d4aecb7a2e873d31787d26001f47f`.

### M2 — Core modules compile and test outside Unity

Child task: `ODY-S00-003`.

- [x] Embedded packages and exact `.asmdef` graph exist.
- [x] Pure .NET solution compiles the same source files under `netstandard2.1` bridges.
- [x] Unit, Domain, Contracts, Architecture, EditMode and PlayMode test assemblies exist only when meaningful.
- [x] Forbidden dependency test fails against an intentional fixture and passes against production graph.
- [x] Canonical restore/format/test scripts exist.
- Evidence: source inventory parity, `dotnet` build/test, Unity compile/EditMode/PlayMode smoke, repository policy, architecture guard results, owner-merged PR #6, and merge commit `5e6f5e03ef022c5d7b0e6fef559c2383796d95be` recorded in `docs/tasks/completed/ODY-S00-003_Module_and_Test_Skeleton.md`.

### M3 — Foundational contracts and runtime shell are deterministic and safe

Child tasks: `ODY-S00-004`, `ODY-S00-005`, `ODY-S00-006`.

- [x] Typed IDs, version values, `Result/Error`, safe reason and retry contracts pass unit tests.
- [x] One synthetic command operation exercises accepted/rejected/duplicate behavior and ordered events.
- [x] Virtual clocks/scheduler and authoritative RNG vectors are deterministic without global APIs.
- [x] Manual composition creates and disposes the Developer Shell without service location or hidden state.
- [x] Structured diagnostics produce allowlisted/redacted records, crash markers and clean shutdown evidence.
- Evidence: ADR-specific test IDs, architecture guards, .NET/Unity vector parity and PlayMode lifecycle smoke.

### M4 — Serialization works across .NET, Mono and IL2CPP

Child task: `ODY-S00-007`.

- [ ] Explicit versioned DTOs and source-generated contexts exist for the synthetic operation.
- [ ] Canonical JSON/fingerprint/hash vectors are stable.
- [ ] Invalid/oversized/duplicate-property/unsupported-version payloads fail safely.
- [ ] Pure .NET, Unity Mono and Windows IL2CPP x64 produce matching compatibility results.
- [ ] Spike conclusions and retained/removed experimental files are recorded.
- Evidence: golden vector hashes, test reports and IL2CPP Player output.

### M5 — Pull requests are gated and Windows build is reproducible by script

Child tasks: `ODY-S00-008`, `ODY-S00-009`.

- [ ] Fast CI runs repository policy, formatting, .NET build/tests, architecture and Unity compile/EditMode checks.
- [ ] BuildIdentity is generated from canonical sources and exposed to client/logs/artifact metadata.
- [ ] After BuildIdentity is available, ODY-S00-008 owns ADR-010 diagnostic session/bundle evidence for `TC-DIAG-033`, `TC-DIAG-034`, `TC-DIAG-035`, `TC-DIAG-036`, `TC-DIAG-037`, `TC-DIAG-038`, `TC-DIAG-039`, and `TC-DIAG-040`: session expiry, secret-field guard, bundle manifest include/exclude categories, bundle checksums, 50 MiB truncation report, campaign database absence, closed/private documentation absence, and machine name / persistent device ID absence from system summary.
- [ ] Windows Development-Debug build is produced by repository entry point.
- [ ] Player smoke verifies startup, AppShell, build identity, diagnostics and idempotent shutdown.
- [ ] Artifacts have checksums and bounded retention.
- Evidence: GitHub Actions runs, `build-identity.json`, artifact hash and Player smoke report.

### M6 — SLICE-00 and Milestone M1 are accepted

Child task: `ODY-S00-010`.

- [ ] Separate clean-checkout rehearsal runs the required entry points.
- [ ] Test traceability matrix maps slice criteria to real evidence.
- [ ] Release Quality Report records pass/fail/not-run status honestly.
- [ ] No blocking task, failed criterion, unapproved dependency, private file, or hidden manual step remains.
- [ ] Product owner accepts M1.
- Evidence: completed task contracts, completed ExecPlan, quality report, traceability matrix and owner review.

## 6. Progress log

- 2026-07-28 08:55 UTC — Created parent task, SLICE-00 backlog, child task sequence and this ExecPlan. No repository implementation or build validation has been performed.
- 2026-07-29 08:15 UTC — Executed local portion of `ODY-S00-001`: copied public-safe technical authorities to canonical paths, added root All Rights Reserved/contribution/security/LFS/editor policy files, added `scripts/check-repository-policy.ps1`, initialized a local Git repository on `chore/ody-s00-001-repository-foundation`, staged 36 public-safe files, and ran repository policy/LFS/diff validation. This historical entry predates the owner bootstrap and is superseded by the 2026-08-01 closeout evidence.

- 2026-08-01 — Began controlled closeout after owner decision: authoritative repository is `odyssey-services/Odyssey_VTT`, visibility is Private, and foundation bootstrap is `82de52e9cb47bd7a1fa8952ac5cba2b9c88456f5`. No history rewrite or retroactive PR; all subsequent substantive changes use branch → PR → owner review → owner merge.
- 2026-08-01 19:26 UTC — Owner merged PR #1 with merge-commit method as `9c7a61893b107624c29ecaa0af34335a715b11e3`. ODY-S00-001 completed; ODY-S00-002 task contract activated as Ready. Exact branch protection/ruleset settings remain an owner-accepted limitation.
- 2026-08-10 02:51 UTC — Verified PR #2 is merged into `main` as merge commit `e790af79fcbfa549231c50b7fd9e3a90c52719b4`, fast-forwarded local `main`, confirmed a clean worktree, created branch `feat/ody-s00-002-unity-project-foundation`, and moved ODY-S00-002 to In Progress before Unity preflight. Pull request is not opened.
- 2026-08-10 02:52 UTC — Mandatory ODY-S00-002 preflight blocked Unity project creation: only Unity `6000.4.0f1` was found at `C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe`; required Unity `6000.3.20f1 (c9ba695d4f07)` is absent, so required IL2CPP for that exact Editor cannot be verified. Git `2.54.0.windows.1`, Git LFS `3.7.1`, and official Unity package registry connectivity passed.
- 2026-08-10 03:05 UTC — Inspected owner-created external project at `D:\Game_Dev\Odyssey_VTT\Odyssey_VTT`; it records Unity `6000.4.0f1 (8cf496087c8f)` and a URP/2D package graph. It was not copied into the repository because ODY-S00-002 requires Unity `6000.3.20f1 (c9ba695d4f07)` and HDRP.
- 2026-08-10 03:12 UTC — Product owner clarified that Unity `6000.3` versus `6000.4` is acceptable for the development process. The project may proceed with recorded `6000.4.0f1` local evidence, but the URP/2D external project still must be converted to the required HDRP baseline before repository import.
- 2026-08-10 03:20 UTC — Re-inspected external project: manifest now contains HDRP `17.4.0` and no direct URP/`com.unity.2d.*` entries. Remaining issues before repository import are template/sample content, lack of Odyssey `Bootstrap`/`AppShell` scenes and paths, template quality names, and extra root packages.

- 2026-08-10 11:35 UTC — Formalized owner-approved Unity baseline amendment as ADR-009 v1.1 / Technical Baseline v0.3 / Active Baseline v1.8, imported only `Assets/`, `Packages/`, and `ProjectSettings/` into the authoritative repository, and validated repository Unity batchmode open/import/compile with Unity `6000.4.0f1` ExitCode 0. ODY-S00-002 entered owner review; ODY-S00-003 was not yet activated.
- 2026-08-10 14:21 UTC — Owner merged PR #4, `ODY-S00-002 — Establish Unity 6000.4 project foundation`, into `main` with GitHub merge-commit method as `70e7d49e217d4aecb7a2e873d31787d26001f47f`. ODY-S00-002 is Done; ODY-S00-003 is activated as Ready only. Implementation of ODY-S00-003 waits for the post-merge closure PR to be owner-reviewed and merged.
- 2026-08-10 15:37 UTC — Owner merged PR #5, `ODY-S00-002 — Complete Unity foundation and activate ODY-S00-003`, into `main` with GitHub merge-commit method as `16ce536b7649fbbf617008b946b6ec33a1dd3a12`. Created branch `feat/ody-s00-003-module-test-skeleton` and began ODY-S00-003 preflight.
- 2026-08-10 15:41 UTC — ODY-S00-003 preflight found only .NET SDK `9.0.308`; required stable .NET 10 LTS SDK is absent. Per owner instruction, implementation stopped before creating `global.json`, `DotNet/Odyssey.Core.sln`, modules, tests, or scripts. ODY-S00-003 is Blocked.
- 2026-08-10 15:58 UTC — Owner resolved the .NET 10 blocker by installing stable .NET SDK `10.0.302` x64. Repeated preflight selected SDK `10.0.302`, host/runtime `10.0.10`, and Unity baseline remained `6000.4.0f1 (8cf496087c8f)`. ODY-S00-003 resumed as In Progress on `feat/ody-s00-003-module-test-skeleton`.
- 2026-08-10 16:23 UTC — ODY-S00-003 implementation completed on `feat/ody-s00-003-module-test-skeleton`: six embedded Unity packages, Unity Client assembly boundaries, `DotNet/Odyssey.Core.sln`, four pure .NET bridge projects, four .NET test projects, Unity EditMode/PlayMode smoke tests, and real repository validation scripts now exist. Validation passed for repository policy, format, architecture guard including negative fixture, .NET restore/build/test, Unity batch compile, Unity EditMode, Unity PlayMode, and `git diff --check`; ODY-S00-003 is In Review and ODY-S00-004 remains Draft.
- 2026-08-10 16:33 UTC — Opened Draft PR #6 for ODY-S00-003 owner review: https://github.com/odyssey-services/Odyssey_VTT/pull/6. Do not merge and do not start ODY-S00-004.
- 2026-08-10 17:25 UTC — Addressed PR #6 owner review corrections: C# language parity is now `LangVersion` `9.0`; production `.asmdef` files are not auto-referenced and remain engine-free; architecture guard validates exact package/asmdef/csproj graphs, catalog metadata, complete valid fixture, and controlled `Odyssey.Domain -> Odyssey.Rules` invalid fixture; repository restore uses root `NuGet.Config` plus ignored local package/cache paths; `test-fast.ps1` exports TRX evidence; `test-unity.ps1` accepts `-UnityEditorPath`/`UNITY_EDITOR_PATH` with exact Unity version validation. ODY-S00-003 remains In Review and ODY-S00-004 remains Draft.
- 2026-08-10 18:38 UTC — Addressed final PR #6 review corrections: test package versions are centralized in `Directory.Build.props` and referenced by all four test projects; architecture guard validates exactly three approved test `PackageReference` entries, central version property values, internal package versions/dependency version parity, duplicate catalog ownership by `runner|path|check`, and controlled negative fixtures for package version mismatch and duplicate catalog ownership; `test-unity.ps1` verifies the selected Unity executable reports `6000.4.0f1` before opening the project. ODY-S00-003 remains In Review and ODY-S00-004 remains Draft.
- 2026-08-10 19:10 UTC — Owner merged PR #6, `ODY-S00-003 — Establish module and dual-test skeleton`, into `main` as merge commit `5e6f5e03ef022c5d7b0e6fef559c2383796d95be` using the GitHub merge-commit method. ODY-S00-003 is Done and moved to `docs/tasks/completed/`; ODY-S00-004 is activated as Ready only. Implementation of ODY-S00-004 waits for the post-merge closure PR to be owner-reviewed and merged.
- 2026-08-10 19:18 UTC — Opened Draft PR #7 for ODY-S00-003 post-merge closure and ODY-S00-004 activation: https://github.com/odyssey-services/Odyssey_VTT/pull/7. Do not merge and do not start ODY-S00-004 implementation.
- 2026-08-10 19:53 UTC — Refined ODY-S00-004 activation contract in existing Draft PR #7: added future `docs/errors/ERROR_CODES.md` registry requirement, multi-task test catalog validation requirement, full ADR-004 Error shape semantics, explicit ApplicationVersion/RulesetVersion/ContentPackageVersion scope, identity preflight, canonical PowerShell validation commands, and ADR-004/ADR-007 partial-implementation wording. No ODY-S00-004 implementation files were created.
- 2026-08-10 20:22 UTC — Owner merged PR #7 into `main` as merge commit `8616246903f7316bfcf767c05a8e2b252d30321b` using the GitHub merge-commit method. Local `main` fast-forwarded to that commit, branch `feat/ody-s00-004-identity-version-result-primitives` was created, and ODY-S00-004 moved to In Progress with identity preflight recorded before production code.
- 2026-08-10 20:43 UTC — ODY-S00-004 implementation completed and moved to In Review: Application `CorrelationId`/`DiagnosticId`, Application/Rules/Content SemVer primitives, Application Result/Error primitives, `docs/errors/ERROR_CODES.md`, multi-task test catalog validation, registry policy checks, and focused .NET tests are in place. Validation passed for restore, format, test structure, test-fast, Unity batch/EditMode/PlayMode, verify-repository, repository policy, dotnet build/test, and final diff checks.
- 2026-08-10 20:52 UTC — Draft PR #8 opened for ODY-S00-004: https://github.com/odyssey-services/Odyssey_VTT/pull/8.
- 2026-08-10 21:55 UTC — Addressed PR #8 foundation review blockers: true read-only Error/ValidationDetail collections, Error reference identity, enum fail-fast validation, ADR-004 SafeReason vocabulary, ValidationSeverity, indexed FieldPath validation, per-ErrorCode metadata allowlist, explicit safe message argument trust factories, and corrected ErrorCode registry lifecycle/version/message metadata policy. Full validation rerun passed with 28 .NET tests and Unity batch/EditMode/PlayMode exit code 0.
- 2026-08-10 22:15 UTC — Addressed final PR #8 guard/parser hardening: repository policy now scans all production literal `ErrorCode.Parse`/`TryParse` usages for Active registry rows, enforces canonical no-leading-zero SemVer and runtime length limits, and covers registered Active literal, unregistered literal, Deprecated literal, leading-zero version, overlong ErrorCode, and overlong metadata-key fixtures. FieldPath and UserMessageKey parser regressions are covered by unit tests. Full validation rerun passed with 30 .NET tests and Unity batch/EditMode/PlayMode exit code 0.
- 2026-08-10 22:57 UTC — Owner merged PR #8 into `main`; local `main` fast-forwarded to `4fb20e9`, branch `feat/ody-s00-005-command-event-clock-rng-primitives` was created, and owner approved preparing/activating ODY-S00-005 without production code or tests. ODY-S00-005 task contract was created as Ready and Active Baseline v1.8 active-task pointer was updated operationally.
- 2026-08-10 23:29 UTC — ODY-S00-005 implementation completed without commit/push/PR: Application command/idempotency contracts, DomainEvent envelope/batch, injected clock/scheduler contracts, ADR-008 deterministic RNG contracts/vectors, one synthetic in-memory test operation, duplicate replay, safe mismatch rejection, global time/random architecture guard, and ODY-S00-005 test catalog entries are in place. Validation passed for restore, verify-format, verify-test-structure, test-fast, Unity batch/EditMode/PlayMode after rerun outside sandbox, verify-repository, repository policy, dotnet build/test, and diff checks.
- 2026-08-11 02:12 UTC — Final self-review moved ODY-S00-005 to In Review and kept ODY-S00-006 Draft/not activated. Branch remains based on owner-merged PR #8 / `main` at `4fb20e935c00d3c5e88c2e7244fd8525e4771819`; Draft PR was not opened at this checkpoint.
- 2026-08-11 02:25 UTC — Opened Draft PR #9 for ODY-S00-005 owner review: https://github.com/odyssey-services/Odyssey_VTT/pull/9. Do not merge and do not start ODY-S00-006.
- 2026-08-11 — Addressed Draft PR #9 owner review corrections for ADR-002/ADR-008 alignment: command/result/event envelopes now expose the required semantic metadata, command commits use an Application-owned in-memory transaction port, duplicate single-flight behavior is covered, clock contracts use `MonotonicTimestamp`/`IDelayScheduler.DelayAsync`, and RNG derivation/proof behavior follows ADR-008 stream derivation v1 with canonical vectors, draw accounting, zero-state fallback, and non-secret proof data. ODY-S00-005 remains In Review; ODY-S00-006 remains Draft/not activated.
- 2026-08-11 — Addressed final PR #9 ownership/invariant hardening: shared CampaignId/CorrelationId/UtcInstant and related typed IDs are Domain-owned, CommandResult no longer exposes raw DomainEvents, result/batch/commit coherence guards were added, in-memory commit evidence stores event batches and receipts atomically, and post-commit CompletedAtHost durability is explicitly deferred to Persistence. ODY-S00-005 remains In Review; ODY-S00-006 remains Draft/not activated.
- 2026-08-11 — Addressed final PR #9 fail-closed validation corrections: default Application/Domain value structs are rejected at command/event factory boundaries, default RNG proof data is rejected by RandomEvidence, `CommandResult.WithCompletedAtHost` is internal Application API only, and regression tests cover the public API and default `UtcInstant` policy. Full validation rerun passed; ODY-S00-006 remains Draft/not activated.
- 2026-08-11 12:31 UTC — Owner merged PR #9, `ODY-S00-005 — Establish command, event, clock and RNG contracts`, into `main` with GitHub merge-commit method as `7aa5cc972c48d9af6509895bb6d9ed1e18899fdf`. Local `main` was fast-forwarded to that commit, branch `feat/ody-s00-006-runtime-composition-diagnostic-shell` was created, ODY-S00-005 moved to `Done` and `docs/tasks/completed/`, and ODY-S00-006 was activated as Ready only. No ODY-S00-006 production implementation has started.
- 2026-08-11 13:10 UTC — Corrected ODY-S00-006 task contract alignment without starting implementation: TestCase IDs now preserve ADR-005 `CMP-001..020` and ADR-010 `DIAG-001..050` meanings, `ProcessInstanceId` is mandatory in the logical log event contract, EventCode registry path/scope is explicit, diagnostic ring buffer/queue limits match ADR-010, Unity UI/scene permissions are exact, and deferred ADR-010 ownership is recorded. ODY-S00-006 remains Ready; no PR is opened.
- 2026-08-11 14:15 UTC — ODY-S00-006 production implementation completed without commit/push/PR: Application diagnostics contracts and EventCode registry, one Unity Client composition root, process/presentation lifecycle, bounded diagnostics queue and ring buffer, emergency sink, crash marker, Developer Shell UI Toolkit presenter, and DeveloperShell-only non-gameplay probe command path are implemented. Validation passed for restore after escalated NuGet rerun, verify-format, verify-test-structure, test-fast, Unity batch/EditMode/PlayMode after corrections, verify-repository, repository policy, dotnet build/test, and diff checks. ODY-S00-006 is In Review.
- 2026-08-11 — Addressed ODY-S00-006 owner review hardening without starting ODY-S00-007: diagnostic ErrorCode semantics were aligned to the registry, rejected DeveloperShell probe uses a dedicated code, public secret-producing log factories were removed, queue pressure now compares incoming priority, PresentationRuntime has one AppRuntime owner with scene-unload detach evidence, incident dedup/fatal hooks/shutdown budgets/partial-startup cleanup/disposal order/structured duplicate host rejection have executable evidence, production emergency sink writes an append-only emergency file with validated tokens, crash marker completion reports real success/failure before `diagnostics.crash.marker_completed`, mid-start cancellation cleans owned resources before diagnostics closes last, `BoundedText` preserves scalar limits, and crash marker filename is exactly `process-started.json`. Corrective validation is recorded in the child task evidence.
- 2026-08-11 18:52 UTC - Owner merged PR #10, `ODY-S00-006 - Runtime Composition and Diagnostic Shell`, into `main` with GitHub merge-commit method as `abb139c3c93115c468d020db3eb423c47cfdd83b`; merged head `b695bc09f344a36b45adb30ed7c0186bf71902d9`. Local `main` was fast-forwarded to that commit, branch `feat/ody-s00-007-serialization-aot-compatibility-spike` was created, ODY-S00-006 moved to `Done` and `docs/tasks/completed/`, and ODY-S00-007 was activated as Ready only. No ODY-S00-007 production implementation has started.
- 2026-08-11 20:14 UTC - ODY-S00-007 was marked Blocked after a zero-commit feasibility probe showed `System.Text.Json` / `JsonSerializerContext` are unavailable in both the current pure .NET `Odyssey.Application` bridge and Unity `Odyssey.Application` compile contours. The blocker requires a shared runtime plus source-generator dependency/reference decision before serialization implementation can start. ODY-S00-008 and ODY-S00-009 remain Draft.
- 2026-08-11 20:52 UTC - Applied the permanent .NET SDK build-layout correction proven by the zero-commit isolation probe: `Directory.Build.props` now sets `UseArtifactsOutput=true`, and repository structure validation guards that setting. The pure .NET `System.Text.Json` `10.0.11` plus `JsonSerializerContext` probe passed with isolated project artifacts; ODY-S00-007 remains Blocked because Unity `6000.4.0f1` Mono/source-generation and Windows x64 IL2CPP compatibility are not yet proven.
- 2026-08-11 23:33 UTC - Recorded final `System.Text.Json` `10.0.11` blocker evidence without production/dependency changes: pure .NET passed, Unity Editor/Mono passed with the application-scoped roslyn4.0 source generator and coherent runtime closure, and Windows Standalone x64 Player managed compilation failed before IL2CPP conversion because the Player compile received the analyzer but not the required STJ runtime references. The blocker is classified as `UNITY PLAYER MANAGED REFERENCE/GENERATOR BLOCKER`; STJ `10.0.11` is not approved as production dependency, no STJ `6.0.0-preview` version is selected, and ODY-S00-008/009 remain Draft.

## 7. Decisions

- 2026-07-28 — Decision: Keep Technical Baseline `PR-000–PR-005` as delivery groups while splitting Core and CI work into smaller task/PR units. Rationale: reduces review and rollback risk without changing required outcomes. Authority: Technical Baseline section 30 plus PLANS scope-control rules.
- 2026-07-28 — Decision: Use a parent task `ODY-S00-000` for the slice-level ExecPlan and separate child task contracts for implementation. Rationale: preserves PLANS requirement that every ExecPlan has a governing task while keeping each pull request independently reviewable.
- 2026-07-28 — Decision: The one-time initial commit that creates `main` is owner-controlled; all substantive repository foundation content goes through a branch and review. Rationale: GitHub requires a base branch before normal protected-branch PR workflow can begin; Codex still never merges to `main`.
- 2026-07-28 — Decision: Do not copy the full current context bundle into the authoritative repository. Rationale: hybrid documentation policy keeps full product documents private; only repository-safe technical authorities and sanitized task artifacts are committed.

## 8. Discoveries and deviations

- The accepted Technical Baseline groups a large amount of foundational work under PR-003 and PR-005. The execution backlog splits these into PR-003A/B/C and PR-005A/B while preserving dependencies and exit criteria.
- Canonical repository scripts do not exist yet. Plans and tasks must state them as future deliverables and cannot claim they were run.
- Repository identity is verified as `odyssey-services/Odyssey_VTT`; Unity runner availability remains an execution-time fact.
- Owner added the foundation directly to `main` as commit `82de52e9cb47bd7a1fa8952ac5cba2b9c88456f5` in Private repository `odyssey-services/Odyssey_VTT`. No separate foundation branch or PR existed. This is a recorded one-time deviation, not a precedent; no history rewrite or retroactive PR is required.

## 9. Validation and acceptance evidence

Current execution evidence:

- Documentation files were generated from the active template and PLANS structure.
- Required authorities and non-goals are mapped to child tasks.
- The bundle manifest and internal file references are validated during packaging.
- `ODY-S00-001` completion evidence exists in `docs/tasks/completed/ODY-S00-001_Repository_Foundation.md`: `REPO-POLICY-001` through `REPO-POLICY-004` pass under Windows PowerShell, negative fixture rejection passes, staged tracked-file inventory excludes private/local bundle paths, and Git LFS attributes are active for approved binary candidates.

Not run because the repository implementation was not yet available at plan creation:

- .NET restore/build/test;
- Unity import, EditMode or PlayMode tests;
- Mono or IL2CPP Player builds;
- GitHub Actions;
- clean-checkout rehearsal.

Post-merge limitation:

- Exact branch protection/ruleset settings were not accessible through the connector, and browser inspection failed before page access because the Windows sandbox helper could not initialize. Owner accepted this limitation; no setting is claimed as Passed.
- ODY-S00-002 preflight evidence: required Unity `6000.3.20f1 (c9ba695d4f07)` was not installed; by owner decision and accepted baseline amendment, Unity `6000.4.0f1 (8cf496087c8f)` is the repository Unity baseline for ODY-S00-002.
- Repository import evidence: the cleaned Unity `6000.4.0f1 (8cf496087c8f)` HDRP `17.4.0` foundation is copied into `Assets/`, `Packages/`, and `ProjectSettings/` in the authoritative repository and validates with Unity batchmode ExitCode 0.
- Owner merge evidence: PR #4 was merged into `main` on `2026-08-10T16:21:33+02:00` as merge commit `70e7d49e217d4aecb7a2e873d31787d26001f47f` using the GitHub merge-commit method.
- ODY-S00-003 evidence: after owner-installed .NET SDK `10.0.302`, `global.json` selects stable SDK `10.0.302`; `.\scripts\restore.ps1`, `.\scripts\verify-format.ps1`, `.\scripts\verify-test-structure.ps1`, `.\scripts\test-fast.ps1`, `.\scripts\test-unity.ps1`, `.\scripts\verify-repository.ps1`, `.\scripts\check-repository-policy.ps1`, `dotnet build DotNet/Odyssey.Core.sln --no-restore`, `dotnet test DotNet/Odyssey.Core.sln --no-build --no-restore`, and `git diff --check` passed. Architecture guard validates the normal graph plus controlled negative `Domain -> Rules`, package version mismatch, and duplicate catalog ownership fixtures. The selected Unity executable reports `6000.4.0f1`; Unity batch compile, EditMode, and PlayMode each returned exit code `0`; EditMode and PlayMode each ran `1` test with `1` passed, `0` failed, `0` skipped. `test-fast.ps1` exported four .NET TRX files under `Logs/ODY-S00-003/dotnet/`. Owner merged PR #6 into `main` as merge commit `5e6f5e03ef022c5d7b0e6fef559c2383796d95be` on `2026-08-10T19:07:16Z`.
- ODY-S00-004 evidence: PR #8 was owner-merged into `main`; local `main` fast-forwarded to `4fb20e9`. ODY-S00-004 implementation evidence recorded restore, verify-format, verify-test-structure, test-fast, test-unity, verify-repository, check-repository-policy, dotnet build/test, and diff checks as passed; Unity batch compile, EditMode, and PlayMode each reported exit code `0`; .NET tests totalled 30 passed, 0 failed, 0 skipped.
- ODY-S00-005 evidence: command/event/clock/RNG contracts compile in pure .NET and Unity. Latest .NET validation includes Domain 1, Contracts 1, Unit 46, Architecture 2, all failed 0. test-unity.ps1 has passed after escalated Unity cache access: batch compile, EditMode, and PlayMode exit code 0; EditMode/PlayMode each ran 1 test with 1 passed, 0 failed, 0 skipped. Repository policy, architecture guard, SDK check, dotnet build/test, and diff checks are rerun after review corrections and recorded in the child task evidence. Owner merged PR #9 into `main` at `2026-08-11T12:31:50Z` as merge commit `7aa5cc972c48d9af6509895bb6d9ed1e18899fdf`.
- ODY-S00-006 corrective evidence: Application diagnostics contracts compile in pure .NET and Unity; runtime starts `Starting` and reaches `Ready` only after AppShell entry point/presentation initialization; Developer Shell uses a narrow facade with accepted/rejected technical probe actions; EventCode registry uses canonical 3-segment codes plus `log.` message template keys; diagnostics properties split data classification from value kind; production emergency diagnostics use a minimal append-only file sink while tests use in-memory sinks; crash markers use the persistent diagnostics directory and exact `process-started.json` marker. Final validation results are recorded in `docs/tasks/completed/ODY-S00-006_Runtime_Composition_and_Diagnostic_Shell.md`; owner merge evidence is PR #10 / `abb139c3c93115c468d020db3eb423c47cfdd83b`.
- ODY-S00-007 activation evidence: task contract `docs/tasks/active/ODY-S00-007_Serialization_and_AOT_Compatibility_Spike.md` is Blocked on `feat/ody-s00-007-serialization-aot-compatibility-spike`. Scope is limited to ADR-003 serialization/AOT compatibility proof and explicitly excludes ODY-S00-009 Windows Development-Debug artifact ownership.
- ODY-S00-007 correction evidence: focused Windows x64 IL2CPP serialization/AOT smoke is mandatory and must be labelled `serialization-aot-smoke` or equivalent; `TC-DIAG-001` keeps the exact ADR-010 meaning, JSONL sink/rotation/retention is required under the narrow Persistence diagnostics adapter scope, and `TC-DIAG-033`, `TC-DIAG-034`, `TC-DIAG-035`, `TC-DIAG-036`, `TC-DIAG-037`, `TC-DIAG-038`, `TC-DIAG-039`, and `TC-DIAG-040` are assigned to ODY-S00-008 after BuildIdentity is available.
- ODY-S00-007 .NET build-layout evidence: sibling bridge projects now use standard SDK artifacts output instead of sharing `DotNet/Projects/obj/project.assets.json`; normal restore creates project-isolated `artifacts/obj/Odyssey.Domain`, `artifacts/obj/Odyssey.Rules`, `artifacts/obj/Odyssey.Content`, and `artifacts/obj/Odyssey.Application` intermediates. No permanent `System.Text.Json` dependency is added in this correction.
- ODY-S00-007 final STJ feasibility evidence: pure .NET `System.Text.Json` `10.0.11` source-generation passed; Unity Editor/Mono passed on Unity `6000.4.0f1 (8cf496087c8f)` with Unity Roslyn `4.3`, the STJ roslyn4.0 analyzer scoped to `Odyssey.Application`, coherent runtime closure, `CS0534` absent, generated-context serialize/deserialize PASS, semantic equality PASS, and no reflection fallback. Windows Build Support IL2CPP, Windows x64 IL2CPP variations, native C++ toolchain, and Windows SDK `10.0.26100.0` are present. Windows Standalone x64 Player managed compilation failed before IL2CPP conversion: the compile response included `System.Text.Json.SourceGeneration`, but did not receive required STJ runtime references, producing `CS0234` / `CS0246`. Classification: `UNITY PLAYER MANAGED REFERENCE/GENERATOR BLOCKER`.

Record real commands, outputs and artifact paths here as child tasks complete.

## 10. Recovery and rollback

- Documentation-only planning changes can be reverted without product-data impact.
- Each implementation task must leave a reviewable repository state and define its own rollback.
- Before the first public release, rollback is by reverting the relevant pull request and returning required status checks to green.
- Unity/package changes are reverted together with `manifest.json`, `packages-lock.json`, `ProjectVersion.txt`, settings and generated compatibility evidence.
- No task may repair a failed milestone by deleting evidence, rewriting history, or silently disabling a required check.

## 11. Open questions and blockers

- Repository identity, Private visibility, and PR #1 merge are verified. Exact branch protection/ruleset settings remain an owner-accepted limitation.
- Unity `6000.4.0f1` is acceptable for local ODY-S00-002 development by owner decision.
- ODY-S00-002 through ODY-S00-006 are Done. ODY-S00-007 is the current Blocked child task on `feat/ody-s00-007-serialization-aot-compatibility-spike`; no pull request is opened and no production implementation has started.
- GitHub plan/settings may affect exact branch-protection options; the task must apply the strongest supported equivalent and record any unavailable setting.

- ODY-S00-007 blocker: pure .NET and Unity Editor/Mono `System.Text.Json` `10.0.11` feasibility passed, but Windows Standalone x64 Player managed compilation failed before IL2CPP conversion because the Player compile did not receive the required STJ runtime references. The blocker is `UNITY PLAYER MANAGED REFERENCE/GENERATOR BLOCKER`; STJ `10.0.11` is not approved as a production dependency, no exact STJ `6.0.0-preview` version is selected, and no further STJ `10.0.11` DLL-location / AutoReference / precompiledReference experimentation is authorized under the current blocker.

## 12. Outcome and follow-up

Current outcome: ODY-S00-001 is Done after owner merge of PR #1. ODY-S00-002 is Done after owner merge of PR #4 and closure PR #5. ODY-S00-003 is Done after owner merge of PR #6 and closure PR #7. ODY-S00-004 is owner-merged through PR #8. ODY-S00-005 is owner-merged through PR #9. ODY-S00-006 is owner-merged through PR #10.

Next action: owner must choose and approve a serialization/toolchain direction for ODY-S00-007. Do not open a PR, merge, run more STJ dependency-layout probes, or implement serialization code while ODY-S00-007 is Blocked.
