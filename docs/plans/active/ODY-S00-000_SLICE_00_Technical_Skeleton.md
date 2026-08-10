# ODY-S00-000 — Establish the SLICE-00 Technical Skeleton

**Status:** Active
**Owner:** Codex
**Branch:** `feat/ody-s00-003-module-test-skeleton`
**Pull request:** Not opened
**Last updated:** 2026-08-10

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
- Evidence: source inventory parity, `dotnet` build/test, Unity compile/EditMode/PlayMode smoke, repository policy, and architecture guard results recorded in `docs/tasks/active/ODY-S00-003_Module_and_Test_Skeleton.md`.

### M3 — Foundational contracts and runtime shell are deterministic and safe

Child tasks: `ODY-S00-004`, `ODY-S00-005`, `ODY-S00-006`.

- [ ] Typed IDs, version values, `Result/Error`, safe reason and retry contracts pass unit tests.
- [ ] One synthetic command operation exercises accepted/rejected/duplicate behavior and ordered events.
- [ ] Virtual clocks/scheduler and authoritative RNG vectors are deterministic without global APIs.
- [ ] Manual composition creates and disposes the Developer Shell without service location or hidden state.
- [ ] Structured diagnostics produce allowlisted/redacted records, crash markers and clean shutdown evidence.
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
- ODY-S00-003 evidence: after owner-installed .NET SDK `10.0.302`, `global.json` selects `10.0.302`; `.\scripts\restore.ps1`, `.\scripts\verify-format.ps1`, `.\scripts\verify-test-structure.ps1`, `.\scripts\test-fast.ps1`, `.\scripts\test-unity.ps1`, `.\scripts\verify-repository.ps1`, `.\scripts\check-repository-policy.ps1`, `dotnet build DotNet/Odyssey.Core.sln`, `dotnet test DotNet/Odyssey.Core.sln`, and `git diff --check` passed. Unity batch compile, EditMode, and PlayMode each returned exit code `0`; EditMode and PlayMode each ran `1` test with `1` passed, `0` failed, `0` skipped.

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
- ODY-S00-002 is Done. ODY-S00-003 is In Review after owner-installed stable .NET SDK `10.0.302` resolved the preflight blocker and local validation passed.
- GitHub plan/settings may affect exact branch-protection options; the task must apply the strongest supported equivalent and record any unavailable setting.

No current blocker is recorded for ODY-S00-003 implementation.

## 12. Outcome and follow-up

Current outcome: ODY-S00-001 is Done after owner merge of PR #1. ODY-S00-002 is Done after owner merge of PR #4 and closure PR #5. ODY-S00-003 is In Review with local validation complete on .NET SDK `10.0.302`; ODY-S00-004 remains Draft and is not started.

Next action: open a draft PR for ODY-S00-003 owner review. Do not merge and do not start ODY-S00-004.
