# ODY-UI-01-005 — Override and Audience-Aware Result Display

**Status:** Active  
**Owner:** Codex (agent)  
**Branch:** `feat/ody-ui-01-005-override-and-audience-aware-display`  
**Pull request:** Not opened  
**Last updated:** 2026-08-27 23:05 UTC

## 1. Purpose and user-visible outcome

The roll panel can choose an audience, display roll results only through `DiceRollVisibilityPolicy`, and let MainGM apply an override with a mandatory reason.

## 2. Task contract

- Goal: extend `RollPanelPresenter` with override controls and audience-aware display.
- Acceptance criteria: see `docs/tasks/active/ODY-UI-01-005_Override_And_Audience_Aware_Display.md` §9.
- Requirement IDs: `ODY-UI-01-005`; roadmap §12.6 steps 6-7.
- In scope: audience dropdown, default `PlayerAndGM`, minimal `SelectedParticipants` fixture, MainGM-only override, safe-denial display, EditMode tests.
- Out of scope: persistence, game log, reroll/cancel, group management UI, networking, package/ADR changes.
- Required authorities: Active Baseline v2.2, `AGENTS.md`, `PLANS.md`, UI backlog §5, `ODY-S03-008`, dice service/visibility/audience contracts, current roll/role presenters.
- Required validation commands: `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `dotnet build DotNet\Odyssey.Core.sln`, `dotnet test DotNet\Odyssey.Core.sln`, `.\scripts\test-unity.ps1`.

## 3. Current state

- `main` contains merged `ODY-UI-01-002` through `ODY-UI-01-004`.
- `RollPanelPresenter` currently submits rolls with `DiceRollAudience.Public()` and formats `LastRoll` directly into `roll-result`.
- `DiceRollVisibilityPolicy` already implements the required all-or-nothing display decision.
- `ApplyOverride` already enforces MainGM-only and mandatory reason behavior.
- Owner clarification removed the original `Public`-only constraint and approved an audience selector.

## 4. Proposed approach

Extend `RollPanelPresenter` because it already owns the roll state and role subscription. Add one `DropdownField` for audience, defaulting to `PlayerAndGM`; create `DiceRollAudience` from the selected value at submit time. Use `InMemoryCampaignUserGroupDirectory` as an injected/default fixture for `TryGetVisibleRoll`; `SelectedParticipants` selects the known `PlayerUserId` and uses one active group containing that user.

Replace direct result formatting with `RefreshResultDisplay()`, which handles three states: no roll yet, no access, and visible roll. Call it after successful roll/modifier/override operations and when role selection changes. Add a MainGM-only override reason field/button that calls `DiceRollService.ApplyOverride` and refreshes `LastRoll` from the store by applying the returned override's roll status through the existing service side effect.

No Application, Domain, Persistence, ADR, dependency, package, or Unity version changes are needed.

## 5. Milestones

### M1 — Contract and plan

- [x] Task contract created with owner clarification recorded in §18.
- [x] ExecPlan created before production edits.

### M2 — Presenter behavior

- [ ] Audience selector is wired into `SubmitRoll`.
- [ ] Result display calls `DiceRollVisibilityPolicy.TryGetVisibleRoll`.
- [ ] Override control calls `DiceRollService.ApplyOverride` and follows role state.

### M3 — Tests and local validation

- [ ] EditMode tests cover default audience, selected participants, safe denial, override success/denial, and role-state buttons.
- [ ] Required local commands pass.
- [ ] Unity-generated drift, if any, is removed from the working tree.

### M4 — PR and evidence

- [ ] Draft PR is opened.
- [ ] CI passes.
- [ ] Task contract, ExecPlan, and `SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` record final evidence.

## 6. Progress log

- 2026-08-27 23:05 UTC — Preflight completed: `main` fast-forwarded to `origin/main`, PR #71 present, branch `feat/ody-ui-01-005-override-and-audience-aware-display` created.
- 2026-08-27 23:05 UTC — Read required sources: UI backlog, dice service, visibility policy, audience contracts, current role/roll presenters, task template, Active Baseline, and vertical slice steps 6-7.
- 2026-08-27 23:05 UTC — Created task contract and ExecPlan with owner clarification recorded.

## 7. Decisions

- 2026-08-27 — Decision: remove the original `Public`-only constraint and add audience selection. Rationale: `Public` is correctly visible to Observer, so safe denial requires a non-`Public` audience. Authority: product owner clarification via PM.
- 2026-08-27 — Decision: default to `PlayerAndGM`. Rationale: it proves Observer exclusion immediately with less fixture state than `SelectedParticipants`. Authority: product owner clarification via PM.
- 2026-08-27 — Decision: extend `RollPanelPresenter`. Rationale: the existing class already owns `LastRoll`, role updates, and dice service dependencies. Authority: current code ownership and minimal-change approach.

## 8. Discoveries and deviations

- Original task wording conflicted: fixed `Public` audience cannot produce Observer safe denial. The owner clarified that the task should add audience selection and default to `PlayerAndGM`.

## 9. Validation and acceptance evidence

Pending implementation.

## 10. Recovery and rollback

Rollback is a normal branch/PR revert. No persisted state, schema, contracts, package versions, or migrations are changed.

## 11. Open questions and blockers

None.

## 12. Outcome and follow-up

Pending implementation. Follow-up tasks remain `ODY-UI-01-006` and `ODY-UI-01-007`.
