# ODY-UI-01-004 — Roll Panel and Modifiers

**Status:** In Progress  
**Roadmap stage / slice:** SLICE-UI-01  
**Owner:** Codex (agent)  
**Requested by:** Product owner  
**Branch:** `feat/ody-ui-01-004-roll-panel-and-modifiers`  
**Pull request:** Not opened  
**ExecPlan:** `docs/plans/active/ODY-UI-01-004_Roll_Panel_And_Modifiers.md`  
**Created:** 2026-08-27  
**Last updated:** 2026-08-27 18:51 UTC

## 1. Goal

Add a minimal Unity UI Toolkit roll panel that lets the selected Player/MainGM role submit a dice formula through `DiceRollService.SubmitRoll`, propose a modifier, and lets MainGM accept, change, or reject that modifier through `DiceRollService.DecideModifier`.

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-UI-01-003` supplies current actor identity, but the UI still has no way to exercise `ODY-S03-008` roadmap steps 2-5 by hand.
- Value or risk reduction: proves the Client presentation layer can call the existing dice application service with role-derived permission values.
- Blocking or enabling relationship: enables `ODY-UI-01-005` result visibility/override UI and later persistence/game-log tasks.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`
- `AGENTS.md`
- `PLANS.md` §1
- `docs/tasks/SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` §5
- `docs/tasks/active/ODY-S03-008_Vertical_Slice_Integration.md`
- `docs/tasks/TASK_TEMPLATE.md`
- `Packages/com.odyssey.application/Runtime/Dice/DiceContracts.cs`
- `Packages/com.odyssey.application/Runtime/Dice/DiceRollService.cs`
- `Assets/Odyssey/Client/Runtime/RoleSelection.cs`
- `Assets/Odyssey/Client/Runtime/RoleSelectorPresenter.cs`
- `Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs`

### Requirement and test IDs

- Requirement IDs: `ODY-UI-01-004`; roadmap §12.6 steps 2-5 as summarized by `ODY-S03-008`.
- Existing test IDs: `TC-DICE-*` service tests from `ODY-S03-005`; `ODY-UI-01-003` role selector tests.
- New test IDs to introduce: None. `verify-test-structure.ps1` currently accepts only `ODY-SNN-NNN` task IDs in `Tests/Metadata/test-catalog.json`, so this UI task records its new Unity EditMode coverage by test method name in this contract instead of adding unsupported catalog rows.

### Task-safe private context

- Approved summary / references: add only a minimal roll/modifier presentation for `DiceRollService`; do not copy private task text.

## 4. Verified current state

### Verified facts

- `main` was fast-forwarded to `origin/main` before branching; recent log includes merges for PR #67, #69, and #70.
- `ODY-UI-01-003` is merged on `main`; `RoleSelectionSnapshot` exposes `ActorUserId`, `ActorIsMainGm`, and `ActorCanCreateRoll`.
- `DiceRollService.SubmitRoll` rejects requests when `actorCanCreateRoll` is false, parses the submitted formula, uses `IAuthoritativeRandomStreamFactory`, and stores the resulting `DiceRoll` in `DiceRollStore`.
- `DiceRollService.ProposeModifier` stores a proposed modifier with `AppliedValue == 0`.
- `DiceRollService.DecideModifier` requires `decidedByUserIsMainGm`; `Accepted` applies the original value, `Changed` applies `changedValue`, and `Rejected` applies 0.
- Unity Client runtime and EditMode test asmdefs already reference `Odyssey.Application`, so no assembly reference change is expected.

### Assumptions

- A task-local in-memory `DiceRollStore` is sufficient because persistence/game-log behavior is assigned to `ODY-UI-01-006`, and `DiceRollStore` is already the dice service's current store-of-record.

## 5. Scope

### In scope

- A UI Toolkit roll panel presenter under `Assets/Odyssey/Client/Runtime`.
- Formula field plus Roll button calling `DiceRollService.SubmitRoll`.
- Modifier label/value fields plus Propose button calling `DiceRollService.ProposeModifier`.
- MainGM-only Accept, Change, and Reject controls calling `DiceRollService.DecideModifier`.
- Visible status/error text for denied observer rolls, invalid input, and non-MainGM modifier decisions.
- A simple `LastRoll` property and change event for later UI tasks.
- Unity EditMode coverage for successful role roll, observer rejection, modifier accept/change/reject behavior, and non-GM decision denial.

### Out of scope

- Override UI, result visibility policy display, save/reopen, game-log list, reroll, cancel, drag/drop polish, localization, or networking.
- Any change under `Packages/com.odyssey.application`, `Packages/com.odyssey.domain`, `Packages/com.odyssey.persistence`, or ADRs.
- Durable persistence or roll-history browsing beyond `LastRoll`.
- Every `DiceRollAudienceKind`; audience rendering is assigned to `ODY-UI-01-005`.

### Allowed paths

```text
Assets/Odyssey/Client/Runtime/**
Assets/Odyssey/Client/Tests/EditMode/**
docs/tasks/active/ODY-UI-01-004_Roll_Panel_And_Modifiers.md
docs/plans/active/ODY-UI-01-004_Roll_Panel_And_Modifiers.md
docs/tasks/SLICE-UI-01_IMPLEMENTATION_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
Packages/**
docs/adr/**
ProjectSettings/**
Packages/manifest.json
```

## 6. Technical constraints

- Module ownership and dependency direction: Unity Client may call Application services; Application/Domain packages must not depend on Unity.
- Authoritative-state and transaction boundary: UI submits application service requests directly using role-derived booleans; it must not mutate dice state except through `DiceRollService`.
- Serialization / compatibility boundary: Not applicable; no persisted or transport contract changes.
- Time / RNG rule: pass injected/constructed `IWallClock` and `IAuthoritativeRandomStreamFactory`; do not call global random for dice outcomes.
- Unity / thread / lifetime rule: UI callbacks and subscriptions are owned by `PresentationRuntime`; presenters release subscriptions on dispose.
- Dependency / licensing rule: no new dependencies.
- Security / privacy / redaction rule: do not log or expose hidden data; only visible roll summary/status is shown.
- Performance or platform constraint: Windows Unity EditMode only for this task.
- Other: task stays within UI Toolkit runtime/edit tests.

## 7. Expected behavior

### Scenario 1 — Player/MainGM roll succeeds

**Given** the role selector is Player or MainGM  
**When** the user enters a valid formula and presses Roll  
**Then** the presenter calls `SubmitRoll`, stores the resulting roll in `LastRoll`, and displays the base/final total.

### Scenario 2 — Observer roll is denied

**Given** the role selector is Observer  
**When** the user presses Roll  
**Then** `SubmitRoll` returns the existing dice denied error and the presenter displays that safe reason without creating `LastRoll`.

### Scenario 3 — MainGM decides a proposed modifier

**Given** a roll exists and a modifier is proposed  
**When** MainGM accepts, changes, or rejects the modifier  
**Then** `LastRoll.FinalTotal` reflects the service decision and status text is updated.

### Scenario 4 — Non-MainGM decision is denied

**Given** a roll exists and a modifier is proposed  
**When** Player tries to accept the modifier through the presenter API  
**Then** `DecideModifier` returns the existing non-GM denial and the displayed total remains unchanged.

### Required invariants

- `LastRoll` is only updated after a successful dice service call.
- Dice outcomes use `DiceRollService` and its RNG factory, never caller-supplied values.
- MainGM buttons reflect the current `RoleSelection` snapshot.

## 8. Deliverables

- Production code: `RollPanelPresenter`.
- Tests: Unity EditMode roll panel tests.
- Scripts / CI: None.
- Configuration: None.
- Documentation: task contract, ExecPlan, backlog status after PR/CI.
- Generated evidence or build artifacts: validation outputs recorded in section 17.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. The roll panel has a formula field and Roll button that call `DiceRollService.SubmitRoll` using current `RoleSelectionSnapshot.ActorUserId` and `ActorCanCreateRoll`.
2. Player and MainGM can submit a valid formula and `LastRoll` stores the returned `DiceRoll`.
3. Observer roll submission is rejected through the dice service and shows a safe visible error.
4. The modifier controls call `ProposeModifier`; MainGM Accept/Change/Reject controls call `DecideModifier`.
5. Accepted and Changed modifier decisions update `LastRoll.FinalTotal`; Rejected leaves the proposed value unapplied.
6. A non-MainGM attempt to decide a modifier is rejected by `DecideModifier` and surfaced visibly.
7. Default `DiceRollAudience` is documented and justified.
8. No Application/Domain/Persistence package, ADR, serialization contract, dependency, or Unity/package version changes are made.
9. Required validation commands pass and real results are recorded.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `PlayerRoll_ValidFormula_StoresLastRoll` | Unity EditMode | Player roll succeeds via real service/RNG and stores `LastRoll` | Pass |
| `MainGmRoll_ValidFormula_StoresLastRoll` | Unity EditMode | MainGM roll succeeds via real service/RNG and stores `LastRoll` | Pass |
| `ObserverRoll_ValidFormula_ShowsDeniedError` | Unity EditMode | Observer roll is rejected with visible error | Pass |
| `MainGmDecisions_ProposedModifier_UpdatesFinalTotal` | Unity EditMode | Proposed modifier accepted/changed/rejected by MainGM updates `FinalTotal` correctly | Pass |
| `PlayerAcceptModifier_ProposedModifier_ShowsDeniedError` | Unity EditMode | Non-MainGM modifier decision is rejected | Pass |
| `RoleSwitch_UpdatesMainGmDecisionButtons` | Unity EditMode | MainGM decision buttons follow the current role selection | Pass |

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\test-unity.ps1
```

### Manual validation

- None required before PR; the slice's full manual walkthrough belongs to `ODY-UI-01-007`.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Unity `6000.4.0f1`, EditMode tests.
- Scripting backend: editor default; no Player build change.
- Network topology or database fixture: None.
- Other: .NET SDK per `global.json`.

### Validation not required by this task

- PlayMode-specific new behavior, Windows Player build, IL2CPP release build, persistence migration, and real networking because this task only adds presentation-layer EditMode behavior.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None expected.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert the branch/PR.
- Data-loss risk and protection: None; in-memory UI state only.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: synthetic dice roll formula, safe role ids, totals, and safe service error reason codes.
- Trust boundaries: UI-to-Application direct call, as already scoped by `SLICE-UI-01`.
- Authorization / audience checks: roll permission and modifier decision permission are enforced by existing `DiceRollService` request booleans.
- Redaction requirements: no audience-filtered result display yet; `ODY-UI-01-005` owns that.
- Log-safe fields: no new logging.
- Abuse / malformed input limits: formula validation remains in `DiceRollService`; UI trims empty modifier labels.
- Security tests: observer denial and non-GM decision denial EditMode tests.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: the task changes Unity UI behavior that exercises permissions and authoritative RNG through Application services, has multiple logical stages, and is expected to be reviewed/resumed safely.
- ExecPlan path: `docs/plans/active/ODY-UI-01-004_Roll_Panel_And_Modifiers.md`
- Expected pull request count: 1
- Milestone or sequencing constraints: create contract/plan before production edits; update backlog only after PR and CI evidence.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, ExecPlan, `docs/tasks/SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` after PR/CI.
- Documents that must not change: ADRs and private documentation.
- Application version change: No — UI task only.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

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

### Changed files / areas

- `Assets/Odyssey/Client/Runtime/RollPanelPresenter.cs` — new UI Toolkit presenter for roll submit, modifier proposal, MainGM decisions, visible safe status text, and `LastRoll`.
- `Assets/Odyssey/Client/Runtime/RollPanelPresenter.cs.meta` — Unity metadata for the new presenter.
- `Assets/Odyssey/Client/Tests/EditMode/RollPanelPresenterTests.cs` — six EditMode tests covering the roll panel scenarios in §10.
- `Assets/Odyssey/Client/Tests/EditMode/RollPanelPresenterTests.cs.meta` — Unity metadata for the new tests.
- `docs/plans/active/ODY-UI-01-004_Roll_Panel_And_Modifiers.md` — ExecPlan.
- This task contract.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | Repository policy check passed; `REPO-POLICY-001` through `005` and `TC-CI-001` through `012` pass. |
| `.\scripts\verify-test-structure.ps1` | Passed | `TC-ARCH-001`/`TC-ARCH-002` pass after removing unsupported `ODY-UI-*` catalog rows. |
| `.\scripts\test-fast.ps1` | Passed | `TC-DOTNET-001` pass; Contracts 1/1, Domain 27/27, Networking 67/67, Unit 105/105, Architecture 2/2, Persistence 60/60. Initial sandbox run failed on SDK directory access; approved rerun passed. |
| `.\scripts\verify-repository.ps1` | Passed | `REPOSITORY-VERIFY PASS repository checks passed`, SDK configured/selected `10.0.302`. |
| `dotnet build DotNet\Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. Initial sandbox run failed because `C:\Users\alexx\AppData\Local\Microsoft SDKs` was denied; approved rerun passed. |
| `dotnet test DotNet\Odyssey.Core.sln` | Passed | Contracts 1/1, Domain 27/27, Networking 67/67, Unit 105/105, Architecture 2/2, Persistence 60/60. |
| `.\scripts\test-unity.ps1` | Passed | `TC-UNITY-ASM-001` compile/EditMode/PlayMode pass; EditMode total=50 passed=50 failed=0 skipped=0; PlayMode total=2 passed=2 failed=0 skipped=0. |
| Initial CI run [33105124685](https://github.com/odyssey-services/Odyssey_VTT/actions/runs/33105124685) | Failed then fixed locally | `repository-policy-format-structure` and `dotnet-restore-build-test` failed because unsupported `ODY-UI-01-004` test-catalog rows violated `verify-test-structure.ps1`; rows were removed and local `verify-test-structure`/`test-fast` now pass. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `RollPanelPresenter.SubmitRoll` builds `SubmitRollRequest` from `RoleSelection.Current`; covered by `TC-UI-ROLL-001`/`002`/`003`. |
| AC-2 | Passed | Player and MainGM tests store successful service result in `LastRoll`. |
| AC-3 | Passed | Observer test gets `ErrorCodes.DiceRollDenied` and visible `PermissionDenied`. |
| AC-4 | Passed | `ProposeModifier`/`AcceptLatestModifier`/`ChangeLatestModifier`/`RejectLatestModifier` call `DiceRollService` paths; covered by `TC-UI-ROLL-004`. |
| AC-5 | Passed | Accepted and Changed add applied values to `FinalTotal`; Rejected leaves the proposed value unapplied. |
| AC-6 | Passed | Player accept attempt returns `ErrorCodes.DiceModifierDecisionDenied` and visible `PermissionDenied`. |
| AC-7 | Passed | `DiceRollAudience.Public()` documented in §18 and used in `SubmitRoll`. |
| AC-8 | Passed | Diff touches no Application/Domain/Persistence package source, ADR, dependency, or version file. Unity-generated `ProjectSettings.asset` whitespace drift and package `.meta` drift were removed before commit. |
| AC-9 | Passed | Required commands passed with real results above. |

### Build and artifact evidence

- Build identity: `odyssey-local-20260827t183856z-g5febd054ec0f-dirty` from the successful Unity run.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: `Logs/ODY-S00-008/editmode-results.xml`, `Logs/ODY-S00-008/playmode-results.xml`.

### Known limitations

- Audience-aware display, persistence, override, reroll, and cancel remain for later `SLICE-UI-01` tasks.

### Follow-up tasks

- `ODY-UI-01-005` — override control and audience-aware result display.
- `ODY-UI-01-006` — persistence and game log.
- `ODY-UI-01-007` — reroll/cancel and full manual walkthrough.

### Self-review summary

- Scope review: diff is limited to Unity Client presenter/tests, test catalog, task contract, and ExecPlan.
- Architecture review: Unity Client calls Application service contracts directly; no package boundary changes or new dependency.
- Test review: new EditMode tests exercise the success/denial/decision paths required by the task.
- Security/privacy review: no logging or transport projection added; visible errors use safe reason codes.
- Documentation/version review: no version/schema/ADR updates; only task/plan/test metadata changed.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-08-27 — Decision: default roll audience for this UI stage is `DiceRollAudience.Public()`. Authority / approval: `SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` assigns audience-aware display to `ODY-UI-01-005`; Public is the smallest visible default that lets every selected role exercise submit/deny behavior without inventing display policy.
- 2026-08-27 — Decision: store only the most recent successful `DiceRoll` as `LastRoll` plus a change event. Authority / approval: task scope asks for a simple current/last roll handoff for future tasks and excludes roll-history browsing.
- 2026-08-27 — Finding: Unity's first EditMode run failed because the new test fixed clock used a non-canonical `UtcInstant` string; fixed the test to use `yyyy-MM-ddTHH:mm:ss.fffffffZ`. Authority / approval: existing `UtcInstant.Parse` contract.
- 2026-08-27 — Finding: initial PR CI failed because `Tests/Metadata/test-catalog.json` does not accept `ODY-UI-*` task IDs; removed the unsupported catalog rows and kept coverage documented by method name. Authority / approval: `scripts/verify-test-structure.ps1`.

### Approved task changes

- None.
