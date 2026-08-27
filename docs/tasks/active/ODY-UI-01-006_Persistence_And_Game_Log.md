# ODY-UI-01-006 — Persistence and Game Log

**Status:** In Review  
**Roadmap stage / slice:** SLICE-UI-01  
**Owner:** Codex (agent)  
**Requested by:** Product owner  
**Branch:** `feat/ody-ui-01-006-persistence-and-game-log`  
**Pull request:** [#73](https://github.com/odyssey-services/Odyssey_VTT/pull/73)  
**ExecPlan:** `docs/plans/active/ODY-UI-01-006_Persistence_And_Game_Log.md`  
**Created:** 2026-08-27  
**Last updated:** 2026-08-27 23:51 UTC

## 1. Goal

Add a minimal Unity UI Toolkit game-log presenter that saves the current roll through `SqliteGameLogRepository.SaveDiceRollEntry`, simulates reopening the campaign with a new repository instance, lists persisted entries, and filters them for the selected role through `GameLogReconnectService.GetVisibleEntries`.

## 2. Why this task exists

- Problem or dependency being addressed: `RollPanelPresenter` currently keeps only in-memory `LastRoll`; there is no UI path that proves a roll survives a reopen.
- Value or risk reduction: connects the minimal UI to the already-tested persistence/game-log contracts without inventing a second filtering path.
- Blocking or enabling relationship: enables `ODY-UI-01-007` to run the full manual walkthrough including reroll/cancel.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1
- `docs/tasks/SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` §5
- `docs/tasks/active/ODY-S03-008_Vertical_Slice_Integration.md` steps 8-9
- `docs/tasks/TASK_TEMPLATE.md`
- `Packages/com.odyssey.application/Runtime/Persistence/GameLogRepositoryContracts.cs`
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteGameLogRepository.cs`
- `Packages/com.odyssey.application/Runtime/GameLog/GameLogReconnectService.cs`
- `Assets/Odyssey/Client/Runtime/RollPanelPresenter.cs`
- `Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs`

### Requirement and test IDs

- Requirement IDs: `ODY-UI-01-006`; roadmap §12.6 steps 8-9 as summarized by `ODY-S03-008`.
- Existing test IDs: `TC-PERSIST-*` game-log repository and reconnect tests; `ODY-UI-01-004`/`005` roll panel tests.
- New test IDs to introduce: None. `verify-test-structure.ps1` currently accepts only `ODY-SNN-NNN` task IDs in `Tests/Metadata/test-catalog.json`, so this UI task records Unity EditMode coverage by method name in this contract.

### Task-safe private context

- Approved summary / references: add only the minimal save/reopen/list UI described by this task; do not copy private task text.

## 4. Verified current state

### Verified facts

- `main` was fast-forwarded to `origin/main` before branching; recent log includes merged PR #67, #69, #70, #71, and #72.
- `RollPanelPresenter` exposes `LastRoll` and `LastRollChanged`, and its default audience is now `PlayerAndGM`.
- `BoardScreenPresenter` and `BoardScreenDemoCampaign` already use and return an Application `CampaignHandle`.
- `SqliteGameLogRepository.SaveDiceRollEntry` persists a roll and log entry in one transaction and replays the existing entry when called again with the same `CommandId`.
- `SqliteGameLogRepository.ListGameLog` returns unredacted entries ordered by authoritative sequence.
- `GameLogReconnectService.GetVisibleEntries` is the existing wrapper for role/group filtering of game-log entries.
- `Odyssey.Unity.Client` already references `Odyssey.Persistence`; no asmdef change is required to instantiate `SqliteGameLogRepository`.

### Assumptions

- A separate `GameLogPresenter` subscribed to `RollPanelPresenter.LastRollChanged` is the smallest maintainable structure because the roll presenter is already growing and the task explicitly asks to prefer a separate log presenter.

## 5. Scope

### In scope

- New `GameLogPresenter` under Unity Client runtime.
- A "Save & Reopen Campaign" button that saves the current `LastRoll`, then constructs a new `SqliteGameLogRepository` and calls `ListGameLog`.
- A simple scrollable list rendering `GameLogEntryRecord.SummaryPayload` values visible to the current selected role.
- Filtering via `GameLogReconnectService.GetVisibleEntries`.
- Reuse the same `CampaignHandle` used by board/demo composition when supplied; no second independent campaign database inside the presenter.
- Unity EditMode coverage for save/reopen, role-filtered list visibility, and same-`CommandId` no-duplicate replay through the UI path.

### Out of scope

- Reroll/cancel, final manual walkthrough, full-text search, archive/export, real networking, style polish, Application/Domain/Persistence package changes, and ADR changes.

### Allowed paths

```text
Assets/Odyssey/Client/Runtime/GameLogPresenter.cs
Assets/Odyssey/Client/Runtime/GameLogPresenter.cs.meta
Assets/Odyssey/Client/Tests/EditMode/GameLogPresenterTests.cs
Assets/Odyssey/Client/Tests/EditMode/GameLogPresenterTests.cs.meta
docs/tasks/active/ODY-UI-01-006_Persistence_And_Game_Log.md
docs/plans/active/ODY-UI-01-006_Persistence_And_Game_Log.md
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

- Module ownership and dependency direction: Unity Client may compose Application and Persistence implementations; Application/Domain/Persistence must not depend on Unity.
- Authoritative-state and transaction boundary: saving must go through `SqliteGameLogRepository.SaveDiceRollEntry`, not direct SQLite writes.
- Serialization / compatibility boundary: no persisted contract changes; consume existing SQLite schema/codecs only.
- Time / RNG rule: no new authoritative RNG; persistence timestamps use the injected/provided `IWallClock`.
- Unity / thread / lifetime rule: subscriptions are owned by `PresentationRuntime`; presenters release subscriptions on dispose.
- Dependency / licensing rule: no new dependencies.
- Security / privacy / redaction rule: list rendering must use `GameLogReconnectService.GetVisibleEntries`, not unfiltered `ListGameLog` output.
- Performance or platform constraint: Windows Unity EditMode for this task.
- Other: do not add unsupported `ODY-UI-*` rows to `Tests/Metadata/test-catalog.json`.

## 7. Expected behavior

### Scenario 1 — save and reopen current roll

**Given** a roll exists in `RollPanelPresenter.LastRoll`  
**When** the user presses "Save & Reopen Campaign"  
**Then** the presenter saves it through `SaveDiceRollEntry`, creates a new game-log repository instance, lists the same campaign, and renders the restored summary payload.

### Scenario 2 — audience-filtered log list

**Given** a persisted default `PlayerAndGM` roll exists  
**When** the selected role is Player or MainGM  
**Then** the log list shows the entry.  
**When** the selected role changes to Observer  
**Then** the log list shows no entry.

### Scenario 3 — idempotent resave

**Given** a current roll was saved with a command id  
**When** the same save command id is reused  
**Then** the repository replay returns the existing log entry and the visible list is not duplicated.

### Required invariants

- The persisted list is filtered only through `GameLogReconnectService.GetVisibleEntries`.
- The presenter uses the supplied `CampaignHandle`; it does not create its own unrelated campaign.
- Reopen means a new `SqliteGameLogRepository` instance against the same campaign path, not shared in-memory state.

## 8. Deliverables

- Production code: `GameLogPresenter`.
- Tests: Unity EditMode game-log presenter tests.
- Scripts / CI: None.
- Configuration: None.
- Documentation: this task contract, ExecPlan, backlog status after PR/CI.
- Generated evidence or build artifacts: validation outputs recorded in section 17.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. A separate game-log presenter provides a "Save & Reopen Campaign" action.
2. The presenter saves the current `RollPanelPresenter.LastRoll` through `SqliteGameLogRepository.SaveDiceRollEntry`.
3. The reopen step constructs a new `SqliteGameLogRepository` instance and calls `ListGameLog` for the same `CampaignHandle`.
4. The list renders `GameLogEntryRecord.SummaryPayload` for visible entries.
5. The list is filtered with `GameLogReconnectService.GetVisibleEntries`.
6. Player and MainGM see a persisted default `PlayerAndGM` roll; Observer does not.
7. Reusing the same `CommandId` through the UI-presenter path does not duplicate a log entry.
8. The game-log presenter reuses a supplied `CampaignHandle`, allowing board and log UI to point at the same demo campaign database.
9. No Application/Domain/Persistence package, ADR, serialization contract, dependency, or Unity/package version changes are made.
10. Required validation commands pass and real results are recorded.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `SaveAndReopen_CurrentRoll_RestoresIdenticalLogEntry` | Unity EditMode | Save current roll, reopen with new repository instance, render restored payload | Pass |
| `RefreshLog_CurrentRoleFiltersPlayerAndGmEntry` | Unity EditMode | Player/MainGM see a `PlayerAndGM` entry; Observer does not | Pass |
| `SaveAndReopen_SameCommandId_DoesNotDuplicateEntry` | Unity EditMode | UI-presenter save path preserves repository idempotency | Pass |
| `Presenter_UsesSuppliedCampaignHandle` | Unity EditMode | Board/demo and log can share one campaign root instead of separate databases | Pass |

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
- Network topology or database fixture: real SQLite campaign under a temporary directory.
- Other: .NET SDK per `global.json`.

### Validation not required by this task

- Reroll/cancel, full manual walkthrough, real networking, full-text search, archive/export, and IL2CPP release build are outside this task.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None expected; existing SQLite game-log schema/codecs are reused.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert the branch/PR.
- Data-loss risk and protection: no schema change; tests use temporary campaign directories.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: synthetic dice roll data, persisted game-log summaries, safe role ids.
- Trust boundaries: UI-to-Persistence direct call under `SLICE-UI-01` direct-call convention.
- Authorization / audience checks: game-log visibility uses `GameLogReconnectService.GetVisibleEntries`.
- Redaction requirements: never render unfiltered `ListGameLog` output.
- Log-safe fields: no new logging.
- Abuse / malformed input limits: repository owns persistence validation and I/O failure mapping.
- Security tests: role-filtered list visibility test.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: the task adds UI behavior over persistence and audience-filtered reconnect state, spans multiple files, and requires a concrete decision about sharing the demo campaign handle.
- ExecPlan path: `docs/plans/active/ODY-UI-01-006_Persistence_And_Game_Log.md`
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
- [x] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Assets/Odyssey/Client/Runtime/GameLogPresenter.cs`
- `Assets/Odyssey/Client/Runtime/GameLogPresenter.cs.meta`
- `Assets/Odyssey/Client/Tests/EditMode/GameLogPresenterTests.cs`
- `Assets/Odyssey/Client/Tests/EditMode/GameLogPresenterTests.cs.meta`
- `docs/plans/active/ODY-UI-01-006_Persistence_And_Game_Log.md`
- `docs/tasks/active/ODY-UI-01-006_Persistence_And_Game_Log.md`

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Pass | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Pass | `Repository policy check passed`. |
| `dotnet build DotNet\Odyssey.Core.sln` | Pass | 0 warnings, 0 errors. |
| `dotnet test DotNet\Odyssey.Core.sln` | Pass | Contracts 1/1, Domain 27/27, Networking 67/67, Unit 105/105, Architecture 2/2, Persistence 60/60. |
| `.\scripts\test-fast.ps1` | Pass | 0 warnings, 0 errors; `TC-DOTNET-001 PASS` for all six fast test assemblies. |
| `.\scripts\test-unity.ps1` | Pass | Unity compile exit 0; EditMode 59/59, PlayMode 2/2. |
| `.\scripts\build-dev.ps1` | Pass | BuildId `odyssey-development-1787874513.1-gadda59cd8e0d`; executable `artifacts\builds\odyssey-development-1787874513.1-gadda59cd8e0d\Windows-x64\Odyssey.exe`. |
| `.\scripts\verify-repository.ps1` | Pass | `REPOSITORY-VERIFY PASS repository checks passed`. |
| PR CI | Pass | Run [33127771319](https://github.com/odyssey-services/Odyssey_VTT/actions/runs/33127771319): `buildidentity-provenance`, `dotnet-restore-build-test`, `repository-policy-format-structure`, and `unity-project-package-static` passed. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Pass | `GameLogPresenter` builds `game-log-save-reopen-button`. |
| AC-2 | Pass | `GameLogPresenter.SaveAndReopen` persists `RollPanelPresenter.LastRoll` through `SqliteGameLogRepository.SaveDiceRollEntry`. |
| AC-3 | Pass | Save path constructs a second `SqliteGameLogRepository` and calls `ListGameLog` for the supplied `CampaignHandle`. |
| AC-4 | Pass | Visible entries render `GameLogEntryRecord.SummaryPayload` labels in `game-log-list`. |
| AC-5 | Pass | Render path calls `GameLogReconnectService.GetVisibleEntries`. |
| AC-6 | Pass | `RefreshLog_CurrentRoleFiltersPlayerAndGmEntry`; Unity EditMode passed. |
| AC-7 | Pass | `SaveAndReopen_SameCommandId_DoesNotDuplicateEntry`; Unity EditMode passed. |
| AC-8 | Pass | `Presenter_UsesSuppliedCampaignHandle`; Unity EditMode passed. |
| AC-9 | Pass | Diff limited to Unity Client presenter/tests and task docs; no package, ADR, schema, dependency, or version changes. |
| AC-10 | Pass | Required validation commands passed; see table above. |

### Build and artifact evidence

- Build identity: `odyssey-local-20260827t234329z-gc808c21dfdf9-dirty`.
- Artifact path / name: `artifacts\builds\odyssey-development-1787874513.1-gadda59cd8e0d\Windows-x64\Odyssey.exe`.
- Checksums: None.
- Test or quality report: `.\scripts\test-unity.ps1` produced 59/59 EditMode and 2/2 PlayMode; `.\scripts\test-fast.ps1` produced `TC-DOTNET-001 PASS` for all fast assemblies.

### Known limitations

- Reroll/cancel and full manual walkthrough remain assigned to `ODY-UI-01-007`.

### Follow-up tasks

- `ODY-UI-01-007` — reroll/cancel and full manual walkthrough.

### Self-review summary

- Scope review: Passed; no out-of-scope reroll/cancel, style polish, schema, ADR, dependency, or package changes.
- Architecture review: Passed; Unity Client composes Application and Persistence, with no package dependency direction changes.
- Test review: Passed; Unity EditMode covers persistence/reopen, role-filtered visibility, idempotency replay, and supplied campaign handle.
- Security/privacy review: Passed; persisted entries are rendered only after `GameLogReconnectService.GetVisibleEntries`, and status text does not expose hidden entry counts to excluded roles.
- Documentation/version review: Passed; task contract and ExecPlan updated, no application/schema/protocol version change required.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-08-27 — Decision: implement a separate `GameLogPresenter`, not more `RollPanelPresenter` growth. Authority / approval: task instruction and current code shape.
- 2026-08-27 — Decision: `GameLogPresenter` requires a supplied `CampaignHandle`; board and log can therefore use the same handle returned by `BoardScreenDemoCampaign` instead of creating two independent campaign databases. Authority / approval: task instruction to reuse `CampaignHandle` if technically possible.
- 2026-08-27 — Decision: reopen simulation is a new `SqliteGameLogRepository` instance against the same `CampaignHandle`, not a new campaign open/close cycle. Authority / approval: `ODY-S03-007` repository tests and task wording.

### Approved task changes

- None.
