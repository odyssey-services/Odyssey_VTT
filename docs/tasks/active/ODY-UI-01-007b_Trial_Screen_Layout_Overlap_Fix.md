# ODY-UI-01-007b — Trial Screen Layout Overlap Fix

**Status:** In Review  
**Roadmap stage / slice:** SLICE-UI-01  
**Owner:** Codex (agent)  
**Requested by:** Product owner  
**Branch:** `fix/ody-ui-01-007b-trial-screen-layout-overlap`  
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/76  
**ExecPlan:** `docs/plans/active/ODY-UI-01-007b_Trial_Screen_Layout_Overlap_Fix.md`  
**Created:** 2026-08-29  
**Last updated:** 2026-08-28 23:55 UTC

## 1. Goal

Make the composed Trial screen controls column lay out readable UI rows without visual overlap between Roll Panel and Game Log elements.

## 2. Why this task exists

- Problem or dependency being addressed: owner manual validation after `ODY-UI-01-007a` confirmed clicks work but found overlapping controls/text in the Trial screen right column.
- Value or risk reduction: restores readable manual walkthrough evidence for `ODY-S03-008` and prevents coordinate-only existence tests from missing layout regressions.
- Blocking or enabling relationship: blocks owner review of PR #74's full Trial UI walkthrough.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/active/ODY-UI-01-007_Reroll_Cancel_And_Full_Walkthrough.md`
- `docs/tasks/active/ODY-UI-01-007a_Runtime_UI_Click_Routing_Gap_Fix.md`
- `Assets/Odyssey/Client/Runtime/TrialScreenPresenter.cs`
- `Assets/Odyssey/Client/Runtime/RollPanelPresenter.cs`
- `Assets/Odyssey/Client/Runtime/GameLogPresenter.cs`
- `Assets/Odyssey/Client/Runtime/DeveloperShellPresenter.cs`
- `Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs`
- `Assets/Odyssey/Client/Runtime/RoleSelectorPresenter.cs`
- `Assets/Odyssey/Client/UI/OdysseyPanelSettings.asset`
- `Assets/Odyssey/Client/UI/AppShell.uxml`
- `Assets/Odyssey/Client/UI/AppShell.uss`
- `Assets/Odyssey/Client/Scenes/AppShell.unity`

### Requirement and test IDs

- Requirement IDs: `ODY-UI-01-007b`
- Existing test IDs: `TC-UNITY-TEST-001`
- New test IDs to introduce: None. Unity PlayMode regression is recorded by method name.

### Task-safe private context

- Approved summary / references: owner found Trial screen layout overlap during manual check after `ODY-UI-01-007a`; screenshot was referenced by the task but not present in the attachment directory.

## 4. Verified current state

### Verified facts

- Current branch was created from local `ODY-UI-01-007a` commit `119a0db` because `007a` is not pushed or merged yet.
- `Assets/Odyssey/Client/UI/OdysseyPanelSettings.asset` has a runtime theme assigned (`themeUss` points to `UnityDefaultRuntimeTheme.tss`) and uses scale mode 1 with reference resolution 1200x800.
- `Assets/Odyssey/Client/UI/AppShell.uss` already exists and contains shell layout styles.
- `Assets/Odyssey/Client/UI/AppShell.uxml` does not reference `AppShell.uss`, so custom `.app-root` and future Trial screen spacing rules are not applied by the runtime document.
- `RollPanelPresenter` creates densely stacked rows (`modifier-row`, `modifier-decision-row`, `override-row`, `roll-result`, `roll-status`, `roll-lifecycle-row`) without explicit spacing/min-height rules.
- The owner screenshot file was not present next to the pasted task; local Game View/screenshot validation will be used as replacement visual evidence.

### Assumptions

- Adding the existing stylesheet to `AppShell.uxml` and minimal USS layout rules is enough to prevent overlap for current Trial screen controls. This will be verified by PlayMode `worldBound` assertions and a local screenshot.

## 5. Scope

### In scope

- Connect existing `AppShell.uss` to `AppShell.uxml`.
- Add minimal USS layout rules for Trial screen, Roll Panel, and Game Log readability.
- Add/extend PlayMode coverage that opens Trial UI through a real click and asserts neighboring controls-column rows do not overlap.
- Record local validation and visual evidence in this task contract.

### Out of scope

- Visual polish beyond readability, new UI behavior, responsive redesign, new dependencies, ADR changes, Unity/package version changes, lower package changes, persistence/domain/application behavior changes.

### Allowed paths

```text
Assets/Odyssey/Client/UI/AppShell.uxml
Assets/Odyssey/Client/UI/AppShell.uss
Assets/Odyssey/Client/Tests/PlayMode/OdysseyPlayModeFoundationSmokeTests.cs
docs/tasks/active/ODY-UI-01-007b_Trial_Screen_Layout_Overlap_Fix.md
docs/plans/active/ODY-UI-01-007b_Trial_Screen_Layout_Overlap_Fix.md
```

### Paths requiring explicit approval before editing

```text
Packages/**
docs/adr/**
ProjectSettings/**
Assets/Odyssey/Client/Scenes/**
```

## 6. Technical constraints

- Module ownership and dependency direction: Unity Client-only visual layout/test change.
- Authoritative-state and transaction boundary: Not applicable.
- Serialization / compatibility boundary: no persisted or network contract changes.
- Time / RNG rule: Not applicable.
- Unity / thread / lifetime rule: UI Toolkit runtime document only; do not change Unity/package versions.
- Dependency / licensing rule: no new dependencies.
- Security / privacy / redaction rule: no hidden-data behavior changes.
- Performance or platform constraint: Unity `6000.4.0f1`, Windows Editor Play Mode default Game View.
- Other: avoid hand-editing scene YAML for this layout fix.

## 7. Expected behavior

### Scenario 1 — Trial controls are readable

**Given** the Developer Shell opens the Trial UI in Play Mode  
**When** the Roll Panel controls render in the right column  
**Then** neighboring controls-column rows have positive size, top-to-bottom order, and non-overlapping `worldBound` rectangles.

### Scenario 2 — Game Log remains separated

**Given** the Roll Panel is followed by the Game Log in the same controls column  
**When** the composed Trial UI renders  
**Then** the Game Log begins below the Roll Panel and its visible controls do not overlap Roll Panel rows.

### Required invariants

- No new gameplay functionality.
- No change to click routing or presenter command behavior.
- No lower module or persistence/domain/application edits.

## 8. Deliverables

- Production code: minimal USS/UXML layout fix.
- Tests: PlayMode layout overlap regression.
- Scripts / CI: None.
- Configuration: None.
- Documentation: task contract and ExecPlan.
- Generated evidence or build artifacts: validation output and local screenshot evidence.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. The composed Trial screen uses a stylesheet that applies explicit spacing/min-height rules to the controls column and rows.
2. Roll Panel rows named by the task render top-to-bottom without overlapping `worldBound` rectangles in Play Mode.
3. Game Log visible controls render below Roll Panel rows without overlap in Play Mode.
4. The fix does not change gameplay behavior, lower package code, ADRs, dependencies, Unity/package versions, or persistence/domain/application modules.
5. Local visual evidence confirms the overlap is gone.
6. Required validation commands pass and results are recorded.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `RealMouseClick_TrialControlsColumnRowsDoNotOverlap` | Unity PlayMode | Real click opens Trial UI, then controls-column rows have non-overlapping `worldBound` layout | Pass |

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\test-unity.ps1
```

### Manual validation

- Capture/inspect a Game View screenshot of the Trial UI after opening it through the runtime shell; confirm Roll Panel rows and Game Log controls are readable and not overlapping.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Unity `6000.4.0f1`, Play Mode.
- Scripting backend: Editor default for tests.
- Network topology or database fixture: local test/runtime trial campaign.
- Other: .NET SDK per `global.json`.

### Validation not required by this task

- Windows Player build, IL2CPP, release build, migration rehearsal, and responsive layout beyond the default Editor Game View are outside scope.

## 11. Compatibility, migration, and rollback

- Compatibility impact: runtime UI layout only.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: existing Trial UI becomes readable; no data contract behavior changes.
- Rollback method: revert this branch/PR.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: synthetic local Trial UI state only.
- Trust boundaries: unchanged.
- Authorization / audience checks: unchanged.
- Redaction requirements: unchanged.
- Log-safe fields: no new diagnostics.
- Abuse / malformed input limits: no new external input parser.
- Security tests: existing coverage unchanged.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: task requires investigation, multiple UI files, a PlayMode geometry regression, and visual evidence.
- ExecPlan path: `docs/plans/active/ODY-UI-01-007b_Trial_Screen_Layout_Overlap_Fix.md`
- Expected pull request count: 1
- Milestone or sequencing constraints: stacked on local `007a` because `007a` is not pushed/merged yet.

## 15. Documentation and versioning impact

- Documents that must change: this task contract and ExecPlan.
- Documents that must not change: ADRs and private documentation.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [ ] Required manual checks are completed.
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

- `Assets/Odyssey/Client/UI/AppShell.uxml` — links existing `AppShell.uss` stylesheet.
- `Assets/Odyssey/Client/UI/AppShell.uss` — adds minimal Trial/Roll/GameLog layout spacing, row wrapping, and min-height rules.
- `Assets/Odyssey/Client/Tests/PlayMode/OdysseyPlayModeFoundationSmokeTests.cs` — adds `RealMouseClick_TrialControlsColumnRowsDoNotOverlap`.
- Task contract and ExecPlan updated.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Pass | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Pass | Repository policy check passed. |
| `dotnet build DotNet\Odyssey.Core.sln` | Pass | Build succeeded, 0 warnings, 0 errors. Required escalation because sandbox denied local SDK cache. |
| `dotnet test DotNet\Odyssey.Core.sln` | Pass | Contracts 1, Domain 27, Networking 67, Unit 105, Architecture 2, Persistence 60; all passed. |
| `.\scripts\test-fast.ps1` | Pass | Architecture checks and .NET TRX totals passed. |
| `.\scripts\verify-repository.ps1` | Pass | `REPOSITORY-VERIFY PASS repository checks passed`. |
| `.\scripts\test-unity.ps1` | Pass | BuildId `odyssey-local-20260828t234118z-g119a0db89431-dirty`; EditMode 62/62 passed, PlayMode 5/5 passed. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Pass | `AppShell.uxml` now links `AppShell.uss`; USS applies explicit Trial controls column spacing/min-height rules. |
| AC-2 | Pass | `RealMouseClick_TrialControlsColumnRowsDoNotOverlap` verifies named Roll Panel rows have positive size, top-to-bottom order, and no overlapping `worldBound` rectangles. |
| AC-3 | Pass | Same PlayMode test verifies Game Log visible controls render after Roll Panel rows without overlap. |
| AC-4 | Pass | Diff is limited to UI stylesheet/UXML, PlayMode test, and task docs. |
| AC-5 | Pending | Codex screenshot capture attempts did not produce a PNG; owner/manual visual review remains pending. |
| AC-6 | Pass | Required automated validation commands passed and results are recorded above. |

### Build and artifact evidence

- Build identity: `odyssey-local-20260828t234118z-g119a0db89431-dirty`.
- Artifact path / name: Pending.
- Checksums: Pending.
- Test or quality report: `Logs/ODY-S00-008/editmode-results.xml`, `Logs/ODY-S00-008/playmode-results.xml`, and .NET TRX logs.
- Commit: local branch HEAD; exact SHA is reported at handoff.

### Known limitations

- Owner-provided screenshot was not present in the attachment directory.
- Codex attempted batchmode and GUI screenshot capture; neither produced a PNG in this environment, so visual confirmation remains a manual review item despite passing geometry regression.
- Draft PR opened: https://github.com/odyssey-services/Odyssey_VTT/pull/76.

### Follow-up tasks

- None expected.

### Self-review summary

- Scope review: Complete; no lower modules, ADRs, dependencies, scenes, or ProjectSettings changes are included.
- Architecture review: Complete; Unity Client UI-only layout change.
- Test review: Complete for automated coverage; visual owner review remains pending.
- Security/privacy review: Complete; no hidden-data, audience, networking, persistence, or diagnostics behavior changed.
- Documentation/version review: Complete; no versioned contract/schema/protocol changes.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-08-29 — Decision: stack `ODY-UI-01-007b` on local `ODY-UI-01-007a` commit `119a0db`. Authority / approval: task preflight requires starting from the current `007a` state; `007a` could not be pushed because the previous push was blocked by account usage limit.
- 2026-08-29 — Decision: treat the missing owner screenshot as unavailable evidence and replace it with local Game View/screenshot validation. Authority / approval: task says to request it if missing; implementation can still verify the described defect through code and local visual evidence.
- 2026-08-29 — Finding attribution: the overlap was found by the product owner during their own manual check after `ODY-UI-01-007a`, from the referenced screenshot.
- 2026-08-29 — Decision: use existing `AppShell.uss` plus `AppShell.uxml` stylesheet linkage instead of inline per-control style edits. Authority / approval: existing stylesheet already owns runtime shell layout and the root cause is missing/applied-insufficient stylesheet rules.
- 2026-08-29 — Decision: do not keep temporary screenshot capture code. Authority / approval: batchmode `ScreenCapture` failed to write PNG and the GUI executeMethod route exited before project load; keeping unused capture plumbing would expand scope without reliable evidence.
- 2026-08-29 — Decision: open `007b` as a stacked draft PR on `007a` / PR #75. Authority / approval: user explicitly authorized push to `origin` and PR creation.

### Approved task changes

- None.
