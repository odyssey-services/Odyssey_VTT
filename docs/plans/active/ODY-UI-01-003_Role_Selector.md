# ODY-UI-01-003 — Role Selector

**Status:** Completed
**Owner:** Codex (agent)
**Branch:** `feat/ody-ui-01-003-role-selector`
**Pull request:** [#70](https://github.com/odyssey-services/Odyssey_VTT/pull/70) (Draft; CI green)
**Last updated:** 2026-08-27 UTC

## 1. Purpose and user-visible outcome

The minimal trial UI gets a persistent "Playing as" control. Selecting Player, MainGM, or Observer updates one shared role state that the board screen consumes now and later roll/log presenters can consume without inventing their own role booleans.

## 2. Task contract

- Goal: implement `ODY-UI-01-003_Role_Selector.md`.
- Acceptance criteria: task contract section 9.
- Requirement IDs: `ODY-UI-01-003`, `SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` row 2.
- In scope: `Odyssey.Unity.Client` role state, selector presenter, board wiring, EditMode tests, task/backlog docs.
- Out of scope: Application/Domain/Persistence changes, real session/identity model, roll/override/log UI, ADR changes.
- Required authorities: `SLICE-UI-01_BACKLOG.md`, `SLICE-UI-01_IMPLEMENTATION_BACKLOG.md`, `ODY-UI-01-002_Board_Screen.md`, `ADR-001` section 6.7, `BoardScreenPresenter.cs`, `DeveloperShellPresenter.cs`, `BaselineRole`.
- Required validation commands: `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `dotnet build`, `dotnet test`, `.\scripts\test-unity.ps1`.

## 3. Current state

Observed facts:

- `main` is fast-forwarded to `origin/main` and contains PR #67, #68, and #69.
- `BoardScreenPresenter` has mutable public `LocalActorUserId` and `LocalActorIsMainGm` properties specifically prepared for the role selector.
- `DeveloperShellPresenter` establishes the presenter pattern: plain C# class, UI Toolkit elements created in code, dependencies supplied by constructor, subscriptions owned by `PresentationRuntime`.
- `BaselineRole` already has exactly the roles needed by this task: `Player`, `MainGM`, `Observer`.

Assumption:

- A presenter/state-level EditMode test is sufficient evidence for this task because no scene-level composition is requested and the previous board screen task used the same pattern.

## 4. Proposed approach

Add `RoleSelection` as a small mutable Client-layer object with three fixed actor IDs and one selected `BaselineRole`. It exposes derived values:

- `ActorUserId`
- `ActorIsMainGm`
- `ActorCanCreateRoll`
- `Role`

Add `RoleSelectorPresenter` that renders a compact UI Toolkit control and updates `RoleSelection`. It notifies the board presenter through an ordinary event/subscription, not a DI container or service locator.

Update `BoardScreenPresenter` with an overload accepting `RoleSelection`, subscribe to state changes through `PresentationRuntime`, and keep the existing constructor/properties usable for tests and compatibility.

## 5. Milestones

### M1 — Contract and plan

- [x] Create task contract and ExecPlan.
- [x] Record initial decisions and validation commands.

### M2 — Role state and selector

- [x] Add role state and selector presenter in `Assets/Odyssey/Client/Runtime`.
- [x] Add `.meta` files for new Unity assets.

### M3 — Board integration and tests

- [x] Retrofit `BoardScreenPresenter` to consume role state.
- [x] Add Unity EditMode tests for Player, MainGM, Observer, and stale-value switching.

### M4 — Validation and handoff

- [x] Run all required validation commands.
- [x] Update completion evidence.
- [x] Create Draft PR and record commit/CI status.

## 6. Progress log

- 2026-08-27 UTC — Ran preflight, fast-forwarded `main` to `origin/main`, confirmed PR #67/#68/#69 in `git log --oneline -10`, and created `feat/ody-ui-01-003-role-selector`.
- 2026-08-27 UTC — Read required backlog, board task contract, board presenter, developer shell presenter, ADR-001, task template, and `PLANS.md`.
- 2026-08-27 UTC — Created task contract and ExecPlan.
- 2026-08-27 UTC — Implemented `RoleSelection`, `RoleSelectorPresenter`, board wiring, and four EditMode tests.
- 2026-08-27 UTC — Validation passed: format, repository policy, `dotnet build DotNet\Odyssey.Core.sln`, `dotnet test DotNet\Odyssey.Core.sln`, `scripts/test-unity.ps1`, `scripts/test-fast.ps1`, and `scripts/verify-repository.ps1`. Literal root `dotnet build`/`dotnet test` failed with `MSB1003` because no root solution exists.
- 2026-08-27 UTC — Opened Draft PR #70 and confirmed CI run 33094205756 passed all 4 checks.

## 7. Decisions

- 2026-08-27 — Decision: use a compact menu-style UI Toolkit selector. Rationale: this is one current role, not three independent commands. Authority: task delegates display technique; `DeveloperShellPresenter` uses code-created UI Toolkit controls.
- 2026-08-27 — Decision: use existing `BaselineRole` directly. Rationale: avoids a parallel role classification. Authority: task section 4.
- 2026-08-27 — Decision: `ActorCanCreateRoll` is true for `Player` and `MainGM`, false for `Observer`. Rationale: observers are the non-acting safe-denial case; both player and GM can initiate rolls in the trial UI. Authority: task section 4 and `SLICE-UI-01_BACKLOG.md` section 3.3.
- 2026-08-27 — Decision: pass shared mutable state by constructor. Rationale: explicit dependency, no service locator. Authority: ADR-001 section 6.7.

## 8. Discoveries and deviations

- Literal `dotnet build` and `dotnet test` from repository root are not valid commands in this repository because there is no root project or solution file. The established solution target `DotNet\Odyssey.Core.sln` passed for both.
- Unity generated untracked `.meta` files under `Packages/**` and whitespace-only `ProjectSettings/ProjectSettings.asset` churn during `scripts/test-unity.ps1`; both were removed/reverted as generated drift outside task scope.

## 9. Validation and acceptance evidence

| Command / check | Result | Evidence |
|---|---|---|
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | Repository policy check passed. |
| `dotnet build` | Failed as root shorthand | `MSB1003`, no project/solution in repo root. |
| `dotnet build DotNet\Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test` | Failed as root shorthand | `MSB1003`, no project/solution in repo root. |
| `dotnet test DotNet\Odyssey.Core.sln` | Passed | 262/262 total across six test assemblies. |
| `.\scripts\test-unity.ps1` | Passed | Compile/EditMode/PlayMode exit code 0; 44/44 EditMode, 2/2 PlayMode. |
| `.\scripts\test-fast.ps1` | Passed | Architecture and .NET fast tests passed. |
| `.\scripts\verify-repository.ps1` | Passed | `REPOSITORY-VERIFY PASS repository checks passed`. |

## 10. Recovery and rollback

No migration or data recovery is needed. Revert this branch's commits to roll back the UI-client code and documentation changes.

## 11. Open questions and blockers

None.

## 12. Outcome and follow-up

Implemented, locally validated, and opened as Draft PR #70 with green CI. `ODY-UI-01-004` is the next consumer after this task.
