# ODY-UI-01-007b — Trial Screen Layout Overlap Fix

**Status:** In Review  
**Owner:** Codex (agent)  
**Branch:** `fix/ody-ui-01-007b-trial-screen-layout-overlap`  
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/76  
**Last updated:** 2026-08-29 00:05 UTC

## 1. Purpose and user-visible outcome

The Trial screen right column becomes readable: Roll Panel rows and Game Log controls render one below another instead of visually overlapping.

## 2. Task contract

- Goal: remove visual overlap in the composed Trial screen controls column.
- Acceptance criteria: see `docs/tasks/active/ODY-UI-01-007b_Trial_Screen_Layout_Overlap_Fix.md` §9.
- Requirement IDs: `ODY-UI-01-007b`.
- In scope: existing `AppShell.uss`/`AppShell.uxml`, PlayMode geometry regression, task/plan docs.
- Out of scope: visual polish, new gameplay behavior, responsive redesign, dependencies, ADRs, lower package changes.
- Required authorities: Active Baseline v2.2, `AGENTS.md`, `PLANS.md`, `ODY-UI-01-007`/`007a` task contracts, Trial/Roll/GameLog/Board/Role/Developer presenters, `OdysseyPanelSettings.asset`, `AppShell.uxml`, `AppShell.uss`.
- Required validation commands: `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `dotnet build DotNet\Odyssey.Core.sln`, `dotnet test DotNet\Odyssey.Core.sln`, `.\scripts\test-unity.ps1`.

## 3. Current state

- Branch is stacked on `007a` commit `119a0db`, now pushed as PR #75.
- `OdysseyPanelSettings.asset` has the default runtime theme assigned, so the no-theme hypothesis is not the root cause.
- `AppShell.uss` exists with runtime shell classes, but `AppShell.uxml` does not reference it.
- The Trial controls column stacks dense Roll Panel and Game Log elements without explicit gap/min-height/wrap rules.
- The owner screenshot referenced by the task is unavailable in the attachment directory; local screenshot evidence will replace it.

## 4. Proposed approach

Attach the existing stylesheet to `AppShell.uxml` and add minimal USS rules for `.trial-screen`, `.trial-layout`, `.trial-controls-column`, `.roll-panel`, `.game-log`, and known row classes. Keep behavior in presenters unchanged. Add one PlayMode test that opens Trial UI through the real click route and checks adjacent `worldBound` rectangles for positive size, monotonic vertical order, and no overlap.

## 5. Milestones

### M1 — Contract and diagnosis

- [x] Create task contract and ExecPlan before production edits.
- [x] Confirm PanelSettings theme exists and custom USS is not linked from UXML.

### M2 — Minimal layout fix

- [x] Link `AppShell.uss` from `AppShell.uxml`.
- [x] Add minimal spacing/min-height/wrap rules for Trial controls column and rows.

### M3 — Regression and visual evidence

- [x] Add PlayMode `worldBound` no-overlap regression.
- [ ] Capture/inspect local Game View screenshot.

### M4 — Validation and PR

- [x] Required validation commands pass.
- [ ] Commit is created.
- [x] Draft PR is opened.

## 6. Progress log

- 2026-08-28 23:40 UTC — Created branch `fix/ody-ui-01-007b-trial-screen-layout-overlap` from local `007a` commit `119a0db`.
- 2026-08-28 23:40 UTC — Confirmed owner screenshot is not present in the task attachment directory.
- 2026-08-28 23:40 UTC — Confirmed `OdysseyPanelSettings.asset` has a runtime theme; confirmed `AppShell.uxml` does not link existing `AppShell.uss`.
- 2026-08-28 23:40 UTC — Created task contract and ExecPlan.
- 2026-08-28 23:50 UTC — Linked `AppShell.uss`, added minimal controls-column layout rules, and added PlayMode no-overlap regression.
- 2026-08-28 23:50 UTC — Required automated validation passed: format, repository policy, .NET build/test, test-fast, verify-repository, Unity EditMode/PlayMode.
- 2026-08-28 23:55 UTC — Created local branch commit; push/PR creation remained pending because policy review blocked exporting private repository contents to `origin` without explicit in-chat authorization.
- 2026-08-29 00:05 UTC — After explicit user authorization, pushed stacked branches to `origin`; opened PR #75 for `007a` and PR #76 for `007b`.

## 7. Decisions

- 2026-08-29 — Decision: stack on local `007a`. Rationale: `007b` depends on the click routing fix and `007a` is not pushed/merged yet. Authority: task preflight.
- 2026-08-29 — Decision: use the existing USS file rather than inline style edits. Rationale: one stylesheet rule set fixes current and future rows in the column. Authority: task §4.
- 2026-08-29 — Decision: no PanelSettings change. Rationale: the default runtime theme is already assigned. Authority: verified `OdysseyPanelSettings.asset`.
- 2026-08-29 — Decision: do not keep temporary screenshot capture code. Rationale: batchmode `ScreenCapture` did not write PNG and the GUI executeMethod route exited before project load; unused capture plumbing would add scope without reliable evidence. Authority: task scope and failed validation history.
- 2026-08-29 — Decision: open `007b` as a stacked draft PR on `007a` / PR #75. Rationale: `007b` depends on the runtime click-routing branch. Authority: user explicitly authorized push to `origin` and PR creation.

## 8. Discoveries and deviations

- The owner screenshot file was not available beside the pasted task. Local Game View/screenshot validation will be recorded instead.
- The no-theme hypothesis was disproved: `OdysseyPanelSettings.asset` references `UnityDefaultRuntimeTheme.tss`.
- Local screenshot capture was attempted but not completed in this environment. Automated `worldBound` evidence passed; visual owner review remains pending.

## 9. Validation and acceptance evidence

- `.\scripts\verify-format.ps1`: PASS.
- `.\scripts\check-repository-policy.ps1`: PASS.
- `dotnet build DotNet\Odyssey.Core.sln`: PASS, 0 warnings, 0 errors.
- `dotnet test DotNet\Odyssey.Core.sln`: PASS, Contracts 1, Domain 27, Networking 67, Unit 105, Architecture 2, Persistence 60.
- `.\scripts\test-fast.ps1`: PASS.
- `.\scripts\verify-repository.ps1`: PASS.
- `.\scripts\test-unity.ps1`: PASS, build identity `odyssey-local-20260828t234118z-g119a0db89431-dirty`, EditMode 62/62, PlayMode 5/5.

## 10. Recovery and rollback

Rollback is a normal branch/PR revert. No data migration, schema change, dependency change, or lower-package change is involved.

## 11. Open questions and blockers

- Visual screenshot/Game View confirmation remains pending because the owner screenshot was unavailable and local screenshot capture did not produce a PNG.
- Draft PR #76 is open.

## 12. Outcome and follow-up

Implementation and automated validation are complete in PR #76. Manual/owner visual confirmation remains pending.
