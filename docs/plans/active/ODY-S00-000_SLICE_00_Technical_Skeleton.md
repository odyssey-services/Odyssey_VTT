# ODY-S00-000 — Establish the SLICE-00 Technical Skeleton

**Status:** Active  
**Owner:** Codex  
**Branch:** `chore/ody-s00-001-repository-foundation` local unborn branch  
**Pull request:** Not opened  
**Last updated:** 2026-07-29 08:15 UTC

## 1. Purpose and user-visible outcome

When this plan is complete, Odyssey VTT has a trustworthy technical starting point rather than a collection of disconnected prototypes. A developer can clone the public repository on Windows, restore the pinned toolchain, run fast Core tests without Unity, open the exact Unity project, run Unity tests, create a versioned Windows development build, inspect safe diagnostics, and prove that invalid architecture changes are blocked before merge.

No user-facing game feature is delivered. The observable outcome is readiness to begin `SLICE-01 — Local Campaign` without redesigning repository, module, contract, test, or build foundations inside that feature slice.

## 2. Task contract

- Goal: Deliver the complete technical skeleton defined by `SLICE-00` and Milestone M1.
- Acceptance criteria: Parent task section 9 and `docs/tasks/SLICE-00_BACKLOG.md` section 2.
- Requirement IDs: `SLICE-00`, `M1`, `TDB-DEC-001–027`, ADR-defined `SLICE-00` test IDs.
- In scope: Child tasks `ODY-S00-001–010`.
- Out of scope: Persistence, networking, accounts, permissions runtime and all gameplay/product features.
- Required authorities: Active Baseline v1.6, Technical Baseline v0.1, AGENTS, PLANS, Task Template, ADR-001–010, MVP Scope SLICE-00, Roadmap Stage 1, Test Strategy.
- Required validation commands: Introduced incrementally; final canonical set must match `AGENTS.md` and actual repository scripts.

Governing task contract: `docs/tasks/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`.

## 3. Current state

### Verified facts

- Architecture and operational documentation are prepared and internally consistent through ADR-010, AGENTS, PLANS, and the task workflow.
- Exact platform/toolchain decisions exist: Windows x64, Unity `6000.3.20f1`, HDRP, UI Toolkit, Input System, pure .NET host, GitHub Actions, Git LFS, All Rights Reserved.
- No repository implementation artifact is currently evidenced: no Git commit, Unity project, package lock, `.asmdef`, `.csproj`, script output, CI run, or Player build.
- The full current documentation bundle contains private product material and must not be copied wholesale into the public code repository.

### Assumptions to verify during execution

- A public GitHub repository can be created in the owner-selected namespace.
- Windows access and a valid Unity `6000.3.20f1` installation/license are available for Unity and IL2CPP validation.
- Git LFS is available on contributor and CI machines.

## 4. Proposed approach

Execute ten child tasks in six accepted delivery groups.

1. Establish repository policy and a public-safe technical documentation subset before any generated project files.
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

- [ ] Owner creates the one-time initial `main` bootstrap and enables public visibility.
- [ ] Public-safe policy and technical documentation scaffold is committed through a reviewed branch.
- [ ] All Rights Reserved, contribution/security policy, Git LFS and repository ignores are present.
- [ ] Private product documents, handoffs, changelogs, archives, secrets and local paths are absent from Git history.
- [ ] Branch protection and owner-review rules are recorded and enabled where GitHub permits.
- Evidence: repository URL, first reviewed pull request, repository-policy check, owner settings capture/checklist.

### M1 — Exact Unity project opens cleanly

Child task: `ODY-S00-002`.

- [ ] Unity `6000.3.20f1` project files and package lock exist.
- [ ] HDRP, UI Toolkit, Input System, graphics APIs, serialization modes and quality assets match ADR-009.
- [ ] `Bootstrap` and `AppShell` scenes exist with no business behavior.
- [ ] Clean Unity open/import/compile succeeds.
- Evidence: project settings inspection, package-lock diff, Unity Editor log, EditMode compile smoke.

### M2 — Core modules compile and test outside Unity

Child task: `ODY-S00-003`.

- [ ] Embedded packages and exact `.asmdef` graph exist.
- [ ] Pure .NET solution compiles the same source files under `netstandard2.1` bridges.
- [ ] Unit, Domain, Contracts, Architecture, EditMode and PlayMode test assemblies exist only when meaningful.
- [ ] Forbidden dependency test fails against an intentional fixture and passes against production graph.
- [ ] Canonical restore/format/test scripts exist.
- Evidence: source inventory parity, `dotnet` build/test, Unity compile/EditMode/PlayMode smoke.

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
- 2026-07-29 08:15 UTC — Executed local portion of `ODY-S00-001`: copied public-safe technical authorities to canonical paths, added root All Rights Reserved/contribution/security/LFS/editor policy files, added `scripts/check-repository-policy.ps1`, initialized a local Git repository on `chore/ody-s00-001-repository-foundation`, staged 36 public-safe files, and ran repository policy/LFS/diff validation. External GitHub repository/protection/PR evidence remains blocked on owner setup.

## 7. Decisions

- 2026-07-28 — Decision: Keep Technical Baseline `PR-000–PR-005` as delivery groups while splitting Core and CI work into smaller task/PR units. Rationale: reduces review and rollback risk without changing required outcomes. Authority: Technical Baseline section 30 plus PLANS scope-control rules.
- 2026-07-28 — Decision: Use a parent task `ODY-S00-000` for the slice-level ExecPlan and separate child task contracts for implementation. Rationale: preserves PLANS requirement that every ExecPlan has a governing task while keeping each pull request independently reviewable.
- 2026-07-28 — Decision: The one-time initial commit that creates `main` is owner-controlled; all substantive repository foundation content goes through a branch and review. Rationale: GitHub requires a base branch before normal protected-branch PR workflow can begin; Codex still never merges to `main`.
- 2026-07-28 — Decision: Do not copy the full current context bundle into the public repository. Rationale: hybrid documentation policy keeps full product documents private; only public-safe technical authorities and sanitized task artifacts are committed.

## 8. Discoveries and deviations

- The accepted Technical Baseline groups a large amount of foundational work under PR-003 and PR-005. The execution backlog splits these into PR-003A/B/C and PR-005A/B while preserving dependencies and exit criteria.
- Canonical repository scripts do not exist yet. Plans and tasks must state them as future deliverables and cannot claim they were run.
- GitHub namespace, final repository URL and Unity runner availability are execution-time facts, not planning evidence.
- During `ODY-S00-001`, the workspace root initially contained only a private/local `Documentation/` bundle and no Git repository. Codex prepared a public-safe local repository scaffold at the root and initialized a local Git index on `chore/ody-s00-001-repository-foundation`, but the owner-controlled public GitHub repository, protected `main`, and pull request still do not exist.

## 9. Validation and acceptance evidence

Current planning evidence only:

- Documentation files were generated from the active template and PLANS structure.
- Required authorities and non-goals are mapped to child tasks.
- The bundle manifest and internal file references are validated during packaging.
- `ODY-S00-001` local policy evidence exists in `docs/tasks/active/ODY-S00-001_Repository_Foundation.md`: `REPO-POLICY-001` through `REPO-POLICY-004` pass under Windows PowerShell, negative fixture rejection passes, staged tracked-file inventory excludes private/local bundle paths, and Git LFS attributes are active for approved binary candidates.

Not run because the repository implementation was not yet available at plan creation:

- .NET restore/build/test;
- Unity import, EditMode or PlayMode tests;
- Mono or IL2CPP Player builds;
- GitHub Actions;
- clean-checkout rehearsal.

Not run because owner-controlled GitHub setup does not yet exist:

- public repository visibility inspection;
- branch protection/ruleset inspection;
- reviewed pull request creation/evidence.

Record real commands, outputs and artifact paths here as child tasks complete.

## 10. Recovery and rollback

- Documentation-only planning changes can be reverted without product-data impact.
- Each implementation task must leave a reviewable repository state and define its own rollback.
- Before the first public release, rollback is by reverting the relevant pull request and returning required status checks to green.
- Unity/package changes are reverted together with `manifest.json`, `packages-lock.json`, `ProjectVersion.txt`, settings and generated compatibility evidence.
- No task may repair a failed milestone by deleting evidence, rewriting history, or silently disabling a required check.

## 11. Open questions and blockers

- GitHub namespace and final repository URL: still required to finish `ODY-S00-001`.
- Availability of Unity `6000.3.20f1` Windows editor/license and IL2CPP module: must be verified before `ODY-S00-002` is In Progress.
- GitHub plan/settings may affect exact branch-protection options; the task must apply the strongest supported equivalent and record any unavailable setting.

These items do not block completion of the planning package.

## 12. Outcome and follow-up

Current outcome: execution package prepared; `ODY-S00-001` local repository foundation scaffold exists and passes local repository policy checks, but remote GitHub repository/protection/PR evidence remains blocked.

Next action: owner creates/selects the public GitHub repository and protected `main`, then the staged `ODY-S00-001` scaffold is pushed through a reviewed pull request. After owner review, update this plan, move the task to completed, and activate `ODY-S00-002`.
