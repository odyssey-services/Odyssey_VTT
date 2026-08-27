# ODY-UI-01-003 — Role Selector

**Status:** In Review
**Roadmap stage / slice:** SLICE-UI-01 (minimal trial UI)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-ui-01-003-role-selector`
**Pull request:** [#70](https://github.com/odyssey-services/Odyssey_VTT/pull/70) (Draft; CI green)
**ExecPlan:** `docs/plans/active/ODY-UI-01-003_Role_Selector.md`
**Created:** 2026-08-27
**Last updated:** 2026-08-27 UTC

## 1. Goal

Add a persistent UI Toolkit "Playing as: Player / MainGM / Observer" selector in `Odyssey.Unity.Client` that supplies the caller-side actor identity, `actorIsMainGm`, baseline role, and `ActorCanCreateRoll` values used by the board screen and later minimal trial UI presenters.

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-UI-01-002` deliberately left `BoardScreenPresenter` with caller-set mutable actor properties until this selector existed.
- Value or risk reduction: later roll, override, audience, and log UI work can consume one shared role state instead of inventing per-screen role booleans.
- Blocking or enabling relationship: enables `ODY-UI-01-004` through `ODY-UI-01-007`; retrofits `ODY-UI-01-002`'s hardcoded actor simplification.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/SLICE-UI-01_BACKLOG.md` sections 3.1-3.5, especially 3.3
- `docs/tasks/SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` sections 2.2 and 5
- `docs/tasks/active/ODY-UI-01-002_Board_Screen.md`
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md` section 6.7
- `Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs`
- `Assets/Odyssey/Client/Runtime/DeveloperShellPresenter.cs`
- `Packages/com.odyssey.application/Runtime/Networking/Session/SessionAdmissionContracts.cs` (`BaselineRole`)

### Requirement and test IDs

- Requirement IDs: `SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` row 2, `ODY-UI-01-003`.
- Existing test IDs: None.
- New test IDs to introduce: None formal; Unity EditMode presenter tests only, matching `ODY-UI-01-002` precedent.

### Task-safe private context

- Approved summary / references: The pasted task contract request is summarized here by public task ID and scope only; no private product text is copied.

## 4. Verified current state

### Verified facts

- Preflight was run: `git fetch origin main`, `git checkout main`, `git merge --ff-only origin/main`, and `git log --oneline -10`; `main` contains PR #67, PR #68, and PR #69.
- Working branch `feat/ody-ui-01-003-role-selector` exists.
- `BoardScreenPresenter` is a plain C# presenter that currently accepts a `UserId localActorUserId` constructor argument and exposes mutable `LocalActorUserId`/`LocalActorIsMainGm` properties.
- `DeveloperShellPresenter` is the existing UI Toolkit presenter precedent: constructor-injected, plain C#, programmatic UI, subscriptions owned through `PresentationRuntime`.
- `BaselineRole` already exists in `Odyssey.Application.Networking.Session` with `MainGM`, `Player`, and `Observer`.
- `AppShellEntryPoint` currently owns only `DeveloperShellPresenter`; no board composition change is required by this task.

### Assumptions

- The selector can be tested at presenter/state level without adding a PlayMode scene flow, because `ODY-UI-01-002` established this exact EditMode presenter-test style.

## 5. Scope

### In scope

- Add a small shared role state object/service in `Odyssey.Unity.Client`.
- Add a UI Toolkit presenter for "Playing as" selection.
- Retrofit `BoardScreenPresenter` to consume the shared role state and keep its public actor properties synchronized for compatibility.
- Add Unity EditMode tests for Player, MainGM, Observer, and repeated switching.
- Update this task contract, its ExecPlan, and `SLICE-UI-01_IMPLEMENTATION_BACKLOG.md`.

### Out of scope

- Roll panel, modifiers, override, journal, persistence/reopen UI, reroll/cancel.
- Real session, identity, permissions, or networking model.
- Changes to `Packages/com.odyssey.application`, `Packages/com.odyssey.domain`, or `Packages/com.odyssey.persistence`.
- New ADR, dependency, Unity package, scene, UXML, or USS file.

### Allowed paths

```text
Assets/Odyssey/Client/Runtime/RoleSelection.cs
Assets/Odyssey/Client/Runtime/RoleSelection.cs.meta
Assets/Odyssey/Client/Runtime/RoleSelectorPresenter.cs
Assets/Odyssey/Client/Runtime/RoleSelectorPresenter.cs.meta
Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs
Assets/Odyssey/Client/Tests/EditMode/RoleSelectorPresenterTests.cs
Assets/Odyssey/Client/Tests/EditMode/RoleSelectorPresenterTests.cs.meta
Assets/Odyssey/Client/Tests/EditMode/BoardScreenPresenterTests.cs
docs/tasks/active/ODY-UI-01-003_Role_Selector.md
docs/plans/active/ODY-UI-01-003_Role_Selector.md
docs/tasks/SLICE-UI-01_IMPLEMENTATION_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
Packages/**
docs/adr/**
Assets/Odyssey/Client/UI/**
Assets/Odyssey/Client/Scenes/**
```

## 6. Technical constraints

- Module ownership and dependency direction: all new production types live in `Odyssey.Unity.Client`; ADR-001 section 6.7 permits presenters/view models and thin Application integration here.
- Authoritative-state and transaction boundary: selected role is presentation state only, not authoritative campaign state; Application services still enforce their own rules.
- Serialization / compatibility boundary: Not applicable.
- Time / RNG rule: Not applicable; no clock or randomness is introduced.
- Unity / thread / lifetime rule: UI Toolkit callbacks are registered by the presenter and released through `PresentationRuntime`.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: no hidden-data transport is added; the selector only supplies existing caller-side role values.
- Performance or platform constraint: Windows/Unity Editor trial UI only.
- Other: `ActorCanCreateRoll` is derived from role: `Player` and `MainGM` can create rolls; `Observer` cannot.

## 7. Expected behavior

### Scenario 1 — Player

**Given** a role selector connected to `BoardScreenPresenter`  
**When** the selected role is `Player`  
**Then** the shared state reports `BaselineRole.Player`, `ActorCanCreateRoll == true`, `ActorIsMainGm == false`, and the board presenter uses the player actor.

### Scenario 2 — MainGM

**Given** a role selector connected to `BoardScreenPresenter`  
**When** the selected role is `MainGM`  
**Then** the shared state reports `BaselineRole.MainGM`, `ActorCanCreateRoll == true`, `ActorIsMainGm == true`, and the board presenter uses the MainGM actor.

### Scenario 3 — Observer

**Given** a role selector connected to `BoardScreenPresenter`  
**When** the selected role is `Observer`  
**Then** the shared state reports `BaselineRole.Observer`, `ActorCanCreateRoll == false`, `ActorIsMainGm == false`, and the board presenter uses the observer actor.

### Required invariants

- Switching roles repeatedly leaves no stale `actorUserId`, `actorIsMainGm`, role, or roll-creation permission from the previous role.
- No real permission or session model is introduced.

## 8. Deliverables

- Production code: `RoleSelection`, `RoleSelectorPresenter`, and board presenter wiring.
- Tests: Unity EditMode tests for role selection and board synchronization.
- Scripts / CI: None.
- Configuration: None.
- Documentation: task contract, ExecPlan, backlog row.
- Generated evidence or build artifacts: validation logs/results from required scripts.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. A persistent UI Toolkit "Playing as: Player / MainGM / Observer" selector exists in `Odyssey.Unity.Client`.
2. The selector/state maps directly to existing `BaselineRole` values, not a parallel role enum.
3. Selecting `Player` updates a connected board presenter to the player `actorUserId` and `LocalActorIsMainGm == false`.
4. Selecting `MainGM` updates a connected board presenter to the MainGM `actorUserId` and `LocalActorIsMainGm == true`.
5. Selecting `Observer` exposes `ActorCanCreateRoll == false`, `ActorIsMainGm == false`, and `BaselineRole.Observer`.
6. Switching roles back and forth leaves no stale actor or permission values.
7. No changes are made to `Packages/com.odyssey.application`, `Odyssey.Domain`, `Odyssey.Persistence`, or ADR files.
8. `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `dotnet build`, `dotnet test`, and `.\scripts\test-unity.ps1` are run and pass.
9. A Draft PR exists, CI is green, and Codex does not merge it.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `RoleSelection_SelectPlayer_UpdatesBoardPresenterActor` | Unity EditMode | Player maps actor and MainGM flag into board presenter | Pass |
| `RoleSelection_SelectMainGm_UpdatesBoardPresenterActor` | Unity EditMode | MainGM maps actor and MainGM flag into board presenter | Pass |
| `RoleSelection_SelectObserver_ExposesObserverRoleAndCannotCreateRoll` | Unity EditMode | Observer role and roll permission are correct | Pass |
| `RoleSelection_SwitchingRoles_DoesNotLeaveStaleValues` | Unity EditMode | repeated role switches update all values | Pass |

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
dotnet build
dotnet test
.\scripts\test-unity.ps1
```

### Manual validation

- None required beyond the automated EditMode presenter tests.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Unity 6000.4.0f1, Editor batchmode via `scripts/test-unity.ps1`.
- Scripting backend: Editor validation only.
- Network topology or database fixture: local single-process trial UI.
- Other: .NET SDK from `global.json`.

### Validation not required by this task

- IL2CPP Player build; no Player-runtime or AOT contract changes are introduced.
- Manual full ten-step UI walkthrough; this is reserved for `ODY-UI-01-007`.

## 11. Compatibility, migration, and rollback

- Compatibility impact: new UI-client API only; no persisted or wire format changes.
- Version fields affected: None.
- Migration or upcaster: Not applicable.
- Forward / backward behavior: existing board constructor remains available.
- Rollback method: revert this task's commits.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | - | - | - | - |

## 13. Security, privacy, and hidden information

- Data classes handled: local role-selection state and synthetic test actor IDs.
- Trust boundaries: no new trust boundary; Application services remain authoritative.
- Authorization / audience checks: role-to-boolean mapping only; no real authorization model.
- Redaction requirements: Not applicable.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: Observer cannot create rolls and MainGM flag does not stick after switching away.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: the task creates cross-presenter role state consumed by later UI tasks and retrofits an existing presenter, so it should be resumable and explicitly validated even though it remains inside one production module.
- ExecPlan path: `docs/plans/active/ODY-UI-01-003_Role_Selector.md`
- Expected pull request count: 1
- Milestone or sequencing constraints: follows `ODY-UI-01-002`; enables `ODY-UI-01-004`-`007`.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, `docs/plans/active/ODY-UI-01-003_Role_Selector.md`, and `docs/tasks/SLICE-UI-01_IMPLEMENTATION_BACKLOG.md`.
- Documents that must not change: ADRs, active documentation baseline, product docs.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed (none required).
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [x] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Assets/Odyssey/Client/Runtime/RoleSelection.cs` (+ `.meta`) — shared Client-layer role state using existing `BaselineRole`.
- `Assets/Odyssey/Client/Runtime/RoleSelectorPresenter.cs` (+ `.meta`) — compact UI Toolkit `DropdownField` presenter for "Playing as".
- `Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs` — constructor overload and subscription wiring so the board presenter follows the shared role state.
- `Assets/Odyssey/Client/Tests/EditMode/RoleSelectorPresenterTests.cs` (+ `.meta`) — four EditMode tests for role mapping and stale-value prevention.
- This task contract and `docs/plans/active/ODY-UI-01-003_Role_Selector.md`.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | All `REPO-POLICY-*`/`TC-CI-*` checks passed; `Repository policy check passed.` |
| `dotnet build` | Failed as literal root shorthand; solution build passed | Literal repo-root command failed with `MSB1003` because the root directory has no project or solution file. `dotnet build DotNet\Odyssey.Core.sln` passed after sandbox escalation: 0 warnings, 0 errors. |
| `dotnet test` | Failed as literal root shorthand; solution tests passed | Literal repo-root command failed with `MSB1003` for the same no-root-solution reason. `dotnet test DotNet\Odyssey.Core.sln` passed: Contracts 1/1, Domain 27/27, Networking 67/67, Unit 105/105, Architecture 2/2, Persistence 60/60. |
| `.\scripts\test-unity.ps1` | Passed | Real Unity 6000.4.0f1 batch run: compile exit code 0, EditMode exit code 0, PlayMode exit code 0; `editmode-results.xml total=44 passed=44 failed=0 skipped=0`, `playmode-results.xml total=2 passed=2 failed=0 skipped=0`. |
| `.\scripts\test-fast.ps1` | Passed | Canonical fast gate from `AGENTS.md`: architecture checks passed, build passed with 0 warnings/errors, .NET test TRX totals 1/27/67/105/2/60 all failed=0. |
| `.\scripts\verify-repository.ps1` | Passed | `REPOSITORY-VERIFY PASS repository checks passed`; selected SDK 10.0.302. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `RoleSelectorPresenter` renders `role-selector-dropdown` labeled "Playing as" with `Player`, `MainGM`, and `Observer`. |
| AC-2 | Passed | `RoleSelection.Role` is `BaselineRole`; no parallel role enum added. |
| AC-3 | Passed | `RoleSelection_SelectPlayer_UpdatesBoardPresenterActor` passed in real Unity EditMode. |
| AC-4 | Passed | `RoleSelection_SelectMainGm_UpdatesBoardPresenterActor` passed in real Unity EditMode. |
| AC-5 | Passed | `RoleSelection_SelectObserver_ExposesObserverRoleAndCannotCreateRoll` passed in real Unity EditMode. |
| AC-6 | Passed | `RoleSelection_SwitchingRoles_DoesNotLeaveStaleValues` passed in real Unity EditMode. |
| AC-7 | Passed | `git status --short`/diff scope show only `Assets/Odyssey/Client/**` role-selector files plus this task's docs; no `Packages/**` or ADR edits. |
| AC-8 | Passed with command-shorthand note | Required scripts and solution-level .NET build/test pass; literal root `dotnet build`/`dotnet test` are not valid in this repo because no root solution exists. |
| AC-9 | Passed | Draft PR [#70](https://github.com/odyssey-services/Odyssey_VTT/pull/70) opened; CI run [33094205756](https://github.com/odyssey-services/Odyssey_VTT/actions/runs/33094205756) passed all 4 checks. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: Not applicable.
- Checksums: Not applicable.
- Test or quality report: `Logs/ODY-S00-008/editmode-results.xml` includes all four `RoleSelectorPresenterTests` passed; `Logs/ODY-S00-008/playmode-results.xml` passed 2/2.

### Known limitations

- No known runtime limitations inside this task's scope. The root `dotnet build`/`dotnet test` shorthand limitation is a repository layout fact, not a role-selector defect.

### Follow-up tasks

- `ODY-UI-01-004` will consume this selector for the roll panel.

### Self-review summary

- Scope review: diff is limited to `Odyssey.Unity.Client` runtime/tests and task docs; generated Unity drift was removed.
- Architecture review: no Application/Domain/Persistence/ADR changes; state is constructor-passed, not global or service-located.
- Test review: four new EditMode tests passed inside the real Unity batch run; prior board tests still pass as part of 44/44 EditMode.
- Security/privacy review: no hidden data or real authorization model added; Observer cannot create rolls and MainGM flag does not stick after switching.
- Documentation/version review: no version/schema/contract bump required.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-08-27 — Use a compact UI Toolkit menu-style selector rather than three independent buttons. Authority / approval: task delegates selector display technique; this matches the single-current-role state and keeps the cross-cutting control small.
- 2026-08-27 — Derive `ActorCanCreateRoll` as true for `Player` and `MainGM`, false for `Observer`. Authority / approval: task asks for a logical mapping; observers are included specifically to exercise safe denial.
- 2026-08-27 — Store the selected role in a small explicit mutable object passed by constructor. Authority / approval: ADR-001 section 6.7 permits presenter/view-model state and forbids service locator as composition.

### Approved task changes

- None.

### Pull request and CI

- Draft PR: [#70](https://github.com/odyssey-services/Odyssey_VTT/pull/70).
- CI: run [33094205756](https://github.com/odyssey-services/Odyssey_VTT/actions/runs/33094205756), all 4 checks passed: `repository-policy-format-structure`, `unity-project-package-static`, `dotnet-restore-build-test`, `buildidentity-provenance`.
