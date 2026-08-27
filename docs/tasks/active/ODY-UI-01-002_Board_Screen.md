# ODY-UI-01-002 — Board Screen

**Status:** In Review — code and tests complete and correct by review; a pre-existing, out-of-scope Unity/`Odyssey.Persistence` compile gap blocks running the new EditMode tests for real (see §4/§18); flagged as a needed separate follow-up task, not fixed here.
**Roadmap stage / slice:** SLICE-UI-01 (minimal trial UI)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-ui-01-002-board-screen`
**Pull request:** Draft — link recorded once opened
**ExecPlan:** See §14
**Created:** 2026-08-27
**Last updated:** 2026-08-27 UTC

## 1. Goal

Build the first screen of the minimal trial UI: a board view rendering the active scene's tokens at their real, persisted `TokenPosition` coordinates, letting a click-to-select-then-click-destination gesture call `BoardMovementService.MoveToken` (`ODY-S03-004`) directly, with a single hardcoded local actor (no role selector yet — that is `ODY-UI-01-003`).

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` decomposed the trial UI into six tasks; this is the first, and the first task in the entire project to write real Unity (`Odyssey.Unity.Client`) code rather than pure .NET. Every prior `SLICE-00`–`03` mechanic exists only behind tests — nobody can click a token yet.
- Value or risk reduction: proves `ADR-001` §6.7's already-`Accepted` UI↔Application boundary (direct calls, no adapter, no service locator) holds for a genuine game-mechanic screen, not just the existing `DeveloperShellPresenter` diagnostics probe — the first real test of that boundary under actual gameplay logic.
- Blocking or enabling relationship: `SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` §6 — no dependency on any other child task. Blocks `ODY-UI-01-003` (retrofits this task's hardcoded actor).

## 3. Authorities and requirement references

### Required authorities

- `docs/tasks/SLICE-UI-01_BACKLOG.md` §3.1–3.5 (UI Toolkit, direct-call boundary, persistence, role-switching convention — all cited, none reopened).
- `docs/tasks/SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` §5 (`ODY-UI-01-002`'s own fixed boundary — not reopened).
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md` §6.7 (`Odyssey.Unity.Client`).
- `docs/adr/ADR-020_Board_Geometry_And_Movement_Determinism_v1.0.md` (`GridType=None`, `TokenPosition`, epsilon — used for coordinate rendering, not reopened).
- `Assets/Odyssey/Client/Runtime/DeveloperShellPresenter.cs`, `Assets/Odyssey/Client/Tests/EditMode/RuntimeCompositionAndDiagnosticsTests.cs` (its `DeveloperShellDisplaysBuildIdentityAndUnavailableFallback` test) — the only prior UI screen and its only prior test, read in full as the structural precedent this task follows, not a new pattern.
- `Packages/com.odyssey.application/Runtime/Board/BoardContracts.cs`/`BoardMovementService.cs` (`ODY-S03-004`) — exact `MoveToken`/`MoveTokenRequest` signatures.
- `Packages/com.odyssey.application/Runtime/Persistence/SceneRepositoryContracts.cs`, `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSceneRepository.cs`/`SqliteCampaignRepository.cs` — exact `ISceneRepository`/`CreateCampaignRequest` signatures.
- `Assets/Odyssey/Client/Runtime/Odyssey.Unity.Client.Runtime.asmdef` — confirmed already references `Odyssey.Application`/`Odyssey.Persistence`/`Odyssey.Domain` directly; no new project reference needed.
- `AGENTS.md`/`PLANS.md` — checked for required Unity Editor batch-validation commands for `Odyssey.Unity.Client` changes; `scripts/test-unity.ps1` (batch compile + EditMode + PlayMode tests against Unity 6000.4.0f1) is the established gate, run in this task (§17).

### Requirement and test IDs

- Requirement IDs: `SLICE-UI-01` roadmap §12.6 step 1 (by hand); `SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` row 1.
- Existing test IDs: None duplicated — this task exercises `BoardMovementService`/`ISceneRepository` through a new UI path, it does not re-test their own already-covered edge cases (`TC-BOARD-*` remain the module-level source of truth).
- New test IDs to introduce: None formal (`TC-*` catalog) — per this task's own throwaway-quality framing and `SLICE-UI-01_IMPLEMENTATION_BACKLOG.md`'s own statement that most `SLICE-UI-01` verification is manual/UI, these are Unity EditMode tests verified by real Unity batchmode run, not catalog-registered `TC-*` IDs (mirroring `DeveloperShellPresenter`'s own tests, which are also not catalog-registered).

### Task-safe private context

- Approved summary / references: None.

## 4. Verified current state

### Verified facts

- `SLICE-UI-01_IMPLEMENTATION_BACKLOG.md`/`ODY-UI-01-001` (PR #66) are merged into `main` — confirmed via `git fetch origin main && git merge --ff-only` and `git log --oneline -10` before this task's branch was created.
- A real Unity project already exists (`Assets/`, `ProjectSettings/`), with `Odyssey.Unity.Client.Runtime.asmdef` already referencing `Odyssey.Domain`/`Odyssey.Rules`/`Odyssey.Content`/`Odyssey.Application`/`Odyssey.Persistence`/`Odyssey.Networking` directly — confirmed by `Read`.
- `DeveloperShellPresenter.cs` is the only prior UI screen in the repository: a plain C# class (not `MonoBehaviour`), constructor-injected, building its entire view from code-created `VisualElement`s over an already-configured `UIDocument`, with no dedicated `.uxml` of its own beyond reusing `AppShell.uxml`'s root document/panel settings — confirmed by `Read` in full.
- No existing EditMode test in this repository simulates a UI Toolkit pointer/click event; `DeveloperShellDisplaysBuildIdentityAndUnavailableFallback` (the only presenter test) only constructs a `GameObject`+`UIDocument`, calls `Initialize()`, and asserts on rendered `Label` text — confirmed by `Read` and `Grep` (zero matches for `ClickEvent`/`SendEvent`/`.clicked()` in the EditMode test assembly).
- Unity Editor 6000.4.0f1 is installed locally (`C:\Program Files\Unity\Hub\Editor\6000.4.0f1`), matching `ProjectSettings/ProjectVersion.txt`'s pinned version and `scripts/test-unity.ps1`'s own hard version check — confirmed by directory listing; real Unity batchmode validation is therefore possible in this environment, not merely aspirational.
- **Significant finding, discovered while running `scripts/test-unity.ps1` for this task's own validation, unrelated to this task's own diff:** the Unity Editor batch compile fails with `CS0234`/`CS0246` errors across every file in `Packages/com.odyssey.persistence/Runtime/Sqlite/**` (`SqliteCampaignRepository.cs`, `SqliteSceneRepository.cs`, `SqliteSavingPipeline.cs`, etc.) — `Microsoft.Data.Sqlite`/`SqliteConnection`/`SqliteTransaction` are unresolved inside the Unity Editor's own compilation, even though the identical code compiles and passes 60/60 tests under `dotnet build`/`dotnet test`. Root cause: `Microsoft.Data.Sqlite`/`SQLitePCLRaw` are consumed via a .NET-SDK-style NuGet `PackageReference` in `DotNet/Projects/Odyssey.Persistence.csproj` — a mechanism Unity's own compiler does not understand. `Packages/com.odyssey.persistence/Runtime/Odyssey.Persistence.asmdef`'s `precompiledReferences` is empty and no `Plugins/` folder anywhere in the repository carries the `Microsoft.Data.Sqlite.dll`/native `SQLitePCLRaw` binaries Unity would need. Confirmed pre-existing and unrelated to this task's own diff by removing this task's two new files and observing the identical failure against a clean `main` checkout; also confirmed that Unity auto-generated dozens of previously-uncommitted `.meta` files for `Packages/**` during this run (removed from this task's own diff via `git clean -f -- Packages/`, not committed here) — direct evidence that **no prior `SLICE-00`–`03` task ever successfully opened this project in the real Unity Editor**; `.\scripts\check-repository-policy.ps1`'s own `TC-CI-006` line ("static Unity project/package/toolchain source validation passed; Unity Editor compile is not claimed") independently confirms CI itself has never required a real compile either. This blocks `scripts/test-unity.ps1`'s full pass for this task and for every future `SLICE-UI-01` task that touches `Odyssey.Persistence` (i.e., essentially all of them, per `SLICE-UI-01_BACKLOG.md` §3.5's real-SQLite decision) — not something this task's own scope permits fixing (§4/§5: no change to `Packages/com.odyssey.persistence` is in scope here). Recorded honestly in §18 as a blocker to AC-8 specifically, not smoothed over or silently worked around.

### Assumptions

- None. All facts above were directly observed via `Read`/`Grep`/directory listing during this task.

## 5. Scope

### In scope

- `Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs` (new) — the board screen presenter, plus `BoardScreenDemoCampaign`/`BoardScreenDemoCampaignHandle` (a throwaway demo-campaign factory so a human can press Play with no manual setup step).
- `Assets/Odyssey/Client/Tests/EditMode/BoardScreenPresenterTests.cs` (new) — presenter-level EditMode tests, following `DeveloperShellPresenter`'s own tested precedent.
- `.meta` files for both new `.cs` files, matching the established `Assets/Odyssey/Client/**` convention.
- This task contract.

### Out of scope

- The role selector (`ODY-UI-01-003`) — this screen uses one hardcoded local actor identity, deliberately not yet wired to a switchable role.
- Drawing, ruler, drag-and-drop polish, animation, sound, hex-grid rendering, pan/zoom polish, localization — all already excluded by `SLICE-UI-01_BACKLOG.md` §3.4, not reopened.
- Any real network.
- Any change to `Packages/com.odyssey.application`/`Odyssey.Domain`/`Odyssey.Persistence` — this screen only calls already-published public contracts, it does not modify them.
- Any new ADR.

### Allowed paths

```text
Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs
Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs.meta
Assets/Odyssey/Client/Tests/EditMode/BoardScreenPresenterTests.cs
Assets/Odyssey/Client/Tests/EditMode/BoardScreenPresenterTests.cs.meta
docs/tasks/active/ODY-UI-01-002_Board_Screen.md
docs/plans/active/ODY-UI-01-002_Board_Screen.md
docs/tasks/SLICE-UI-01_IMPLEMENTATION_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
Packages/**
docs/adr/**
Assets/Odyssey/Client/UI/**
Assets/Odyssey/Client/Scenes/**
Any other production code, test code, or Unity asset outside the Allowed paths above
```

## 6. Technical constraints

- Module ownership and dependency direction: `BoardScreenPresenter` lives in `Odyssey.Unity.Client` (namespace `Odyssey.Unity.Client`), calling `Odyssey.Application.Board`/`Odyssey.Application.Persistence`/`Odyssey.Persistence.Sqlite` directly, per `ADR-001` §6.7's already-permitted "thin integration code для вызова Application" — no adapter layer, no new project reference (the asmdef already lists every needed assembly).
- Authoritative-state and transaction boundary: This screen holds no authoritative state of its own — every read (`ListTokens`/`GetToken`) and write (`MoveToken`) goes through the real `ISceneRepository`, the same durable, transactional boundary `ODY-S03-004`'s own tests already exercise. No `authoritative campaign state в MonoBehaviour`, per `ADR-001` §6.7's explicit prohibition — `BoardScreenPresenter` is a plain C# class, not a `MonoBehaviour`, holding only transient UI selection state (`_selectedTokenId`), never a cached copy of token positions across renders.
- Serialization / compatibility boundary: Not applicable — no new persisted format.
- Time / RNG rule: `UnityWallClock` (the existing `Odyssey.Unity.Client`-owned `IWallClock` adapter, `DiagnosticsRuntime.cs`) — no new time source.
- Unity / thread / lifetime rule: All work is synchronous, on the main/Editor thread, matching `DeveloperShellPresenter`'s own convention — no async/await, no background thread touches Unity API or UI Toolkit elements.
- Dependency / licensing rule: No new dependency, GitHub Action, Unity package, executable, or downloadable tool. `UnityEngine.UIElements` is already used by `DeveloperShellPresenter`.
- Security / privacy / redaction rule: Not applicable — no hidden data path exists yet for board state (that is `ODY-S03-006`'s dice/log-specific concern, not touched here).
- Performance or platform constraint: Not applicable at trial-UI scale (a handful of tokens).
- Other: `LocalActorUserId`/`LocalActorIsMainGm` are mutable, public, caller-settable properties, not constructor-fixed — `ODY-UI-01-003`'s future role selector is expected to set these directly.

## 7. Expected behavior

### Scenario 1 — the controlling actor moves their own token

**Given** a token controlled by the presenter's current `LocalActorUserId`
**When** the token is selected then a destination is clicked (or, in tests, `SelectToken`/`TryMoveSelectedTokenTo` called directly)
**Then** `BoardMovementService.MoveToken` succeeds, the token's persisted position updates, the selection clears, and the render reflects the new coordinates.

### Scenario 2 — a non-controlling, non-MainGM actor cannot move a foreign token

**Given** a token controlled by a different `UserId`, and `LocalActorIsMainGm == false`
**When** the token is selected and a move is attempted
**Then** `BoardMovementService.MoveToken` returns a failure, the token's persisted position is unchanged, and the render reflects the original coordinates.

### Scenario 3 — a MainGM actor can move any token

**Given** `LocalActorIsMainGm == true`
**When** a foreign token is selected and moved
**Then** the move succeeds regardless of `ControllerUserId`.

### Required invariants

- No successful move ever occurs for a non-controller, non-MainGM actor — `BoardMovementService`'s own authorization is never bypassed or duplicated by this screen.
- The render always reflects the repository's own current, persisted state after `Refresh()` — never a UI-cached, potentially stale position.
- `LocalActorUserId`/`LocalActorIsMainGm` remain mutable at all times (never captured as an immutable constructor value elsewhere in the class).

## 8. Deliverables

- Production code: `BoardScreenPresenter.cs` (`BoardScreenPresenter`, `BoardScreenDemoCampaign`, `BoardScreenDemoCampaignHandle`, `BoardScreenErrors`).
- Tests: `BoardScreenPresenterTests.cs` (4 EditMode tests).
- Scripts / CI: None new — `scripts/test-unity.ps1` already exists and is run, not created.
- Configuration: None.
- Documentation: This task contract, `SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` (row 1 status).
- Generated evidence or build artifacts: Unity batchmode logs/test-result XML (§17).
- Migration / recovery material: Not applicable.

## 9. Acceptance criteria

1. `BoardScreenPresenter` renders every token in the active scene at coordinates derived from its real, persisted `TokenPosition`.
2. Selecting the presenter's own controlled token, then moving it, succeeds, updates the persisted position, and clears the selection.
3. Selecting a foreign (non-controlled) token and attempting to move it while `LocalActorIsMainGm == false` fails, with no state change.
4. Setting `LocalActorIsMainGm = true` allows moving a foreign token.
5. `LocalActorUserId`/`LocalActorIsMainGm` are public, mutable properties, not constructor-fixed.
6. No change to `Packages/com.odyssey.application`/`Odyssey.Domain`/`Odyssey.Persistence` and no new ADR.
7. `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `dotnet build`, `dotnet test` all pass.
8. `scripts/test-unity.ps1` (Unity batch compile + EditMode + PlayMode tests) passes with 0 failures.
9. `git diff --name-status` against `main` shows only files listed in §5's Allowed paths.
10. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test | Layer / runner | Behavior proven | Required result |
|---|---|---|---|
| `ControllingActor_SelectsOwnToken_MovesIt_PositionUpdatesAndRendersCorrectly` | Unity EditMode | Own-token move succeeds, persists, re-renders | Pass |
| `NonControllingActor_SelectsForeignToken_MoveIsDenied_PositionUnchanged` | Unity EditMode | Foreign-token move denied, no state change | Pass |
| `MainGmActor_MovesForeignToken_Succeeds` | Unity EditMode | MainGM bypasses control-ownership check | Pass |
| `Initialize_RendersAllExistingTokensAtTheirRealPersistedCoordinates` | Unity EditMode | Initial render reflects real persisted state | Pass |

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

```bash
dotnet build DotNet/Odyssey.Core.sln
dotnet test DotNet/Odyssey.Core.sln
```

```powershell
.\scripts\test-unity.ps1
```

### Manual validation

- None beyond what the EditMode tests already cover — no manual Play-mode click-through was performed in this task's own validation (Unity Editor batchmode ran the tests headlessly); a human is expected to actually press Play and try it at their own convenience, since that is this whole slice's purpose, but it is not this task's own required evidence.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Unity 6000.4.0f1 (exact version pinned by `ProjectSettings/ProjectVersion.txt` and `scripts/test-unity.ps1`), Editor batchmode.
- Scripting backend: Not applicable to this task's own validation (no IL2CPP/Player build required, only Editor batchmode compile + tests).
- Network topology or database fixture: Real SQLite via `Microsoft.Data.Sqlite`, temp-directory campaign per test.
- Other: `dotnet` SDK matching `global.json`.

### Validation not required by this task

- Any Player/IL2CPP build — no new platform-compatibility surface is introduced (`UnityEngine.UIElements` is already used by `DeveloperShellPresenter`).
- Any manual human Play-mode session — welcome, but not this task's own required evidence.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None — new UI code only, no existing contract changed.
- Version fields affected: None.
- Migration or upcaster: Not applicable.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits.
- Data-loss risk and protection: None — `BoardScreenDemoCampaign.CreateFresh` creates a new, isolated temp-directory campaign each run; no existing campaign data is touched.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

`UnityEngine.UIElements` is already referenced by the existing `Odyssey.Unity.Client.Runtime.asmdef` (used by `DeveloperShellPresenter`); no new package or assembly reference is added.

## 13. Security, privacy, and hidden information

- Data classes handled: Synthetic demo campaign data (temp-directory only) or a real campaign a human opens by hand — no secrets, no personal data.
- Trust boundaries: Not applicable — this screen introduces no new authorization decision; `BoardMovementService`'s own authorization (`ODY-S03-004`) is the sole trust boundary, unmodified.
- Authorization / audience checks: Not applicable to this task's own code — it calls the existing checks, does not add new ones.
- Redaction requirements: Not applicable — no hidden-token/fog model exists yet for board state.
- Log-safe fields: Not applicable — the one new `Error` factory (`NoTokenSelected`) reuses the existing `ErrorCodes.ApplicationValidationInvalid` registry entry, no new code.
- Abuse / malformed input limits: Not applicable — a local single-user trial screen.
- Security tests: Scenario 2 above is this task's own security-relevant test — a non-controller, non-MainGM actor's move attempt produces zero persisted state change.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: evaluated fresh against `PLANS.md` §1.2. This is the first task in the entire project writing real `Odyssey.Unity.Client` game-mechanic code (not pure .NET, not a diagnostics probe) — it required real investigation before the implementation path was known: confirming no existing EditMode test simulates UI Toolkit click events (ruling out that approach and requiring the public-method-testability design instead), confirming the exact rendering technique against `ADR-001` §6.7's already-fixed constraints, and confirming Unity Editor batchmode validation is actually possible in this environment (a real Unity install exists) rather than merely aspirational. This matches §1.2's "requires investigation before the implementation path is known" and "first use of an existing but never-yet-exercised architectural boundary" triggers — even though no Application/Domain contract itself changes.
- ExecPlan path: `docs/plans/active/ODY-UI-01-002_Board_Screen.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: Depends on `SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` (merged). Blocks `ODY-UI-01-003` (role selector retrofit).

## 15. Documentation and versioning impact

- Documents that must change: This task contract, its ExecPlan, `SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` (row 1 status).
- Documents that must not change: Any ADR, `SLICE-UI-01_BACKLOG.md`, `ODY-UI-01-000`/`001` task contracts, `Assets/Odyssey/Client/UI/**`, `Assets/Odyssey/Client/Scenes/**`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied — AC-1–5/AC-8 are blocked by a pre-existing, out-of-scope Unity/`Odyssey.Persistence` compile gap (§18); not silently marked done.
- [ ] Required automated tests pass — written correctly by code review, but not run-verified (§17).
- [x] Required manual checks are completed (none required).
- [x] Required commands and their real results are recorded, including the honest `Blocked` result.
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

- `Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs` (+ `.meta`) — new.
- `Assets/Odyssey/Client/Tests/EditMode/BoardScreenPresenterTests.cs` (+ `.meta`) — new.
- This task contract, `SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` (row 1 status).

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet/Odyssey.Core.sln` | Passed | 0 warnings, 0 errors — confirms the pure .NET solution is unaffected by `Assets/` changes. |
| `dotnet test DotNet/Odyssey.Core.sln` | Passed | 261/261 passed, 0 failed (Contracts 1, Domain 27, Networking 67, Unit 105, Architecture 2, Persistence 60) — no regression. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | All `REPO-POLICY-*`/`TC-CI-*` checks passed; `Repository policy check passed.` |
| `.\scripts\test-unity.ps1` (Unity 6000.4.0f1 batch compile + EditMode + PlayMode) | **Blocked** | Real run performed. Batch compile fails with `CS0234`/`CS0246` across every `Packages/com.odyssey.persistence/Runtime/Sqlite/**` file — `Microsoft.Data.Sqlite` is not resolvable inside Unity's own compiler (no Unity `Plugins/` DLL, no `precompiledReferences` entry). Confirmed pre-existing, unrelated to this task's own two new files (see §4's full finding). This task's own `BoardScreenPresenter.cs`/`BoardScreenPresenterTests.cs` cannot be Unity-compiled or Unity-tested until that pre-existing gap is fixed by a separate task — not attempted here (out of this task's own scope, §5). |
| CI — Draft PR | Pending | To be recorded once the PR is opened and CI completes. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 through AC-5 | **Not directly verified by an automated run** | The four EditMode tests (§10) are written to prove exactly these criteria and are believed correct by code review (they mirror `ODY-S03-004`'s own already-passing `BoardMovementServiceTests` assertions applied through the presenter's public `SelectToken`/`TryMoveSelectedTokenTo` methods, which call the identical, already-tested `BoardMovementService.MoveToken`/`ISceneRepository` code paths) — but they could not actually be *run* in this task, since Unity Editor batch compilation itself fails for the pre-existing, unrelated reason recorded in §4/§18. This is stated honestly, not marked Passed on code-review confidence alone. |
| AC-6 | Passed | `git status --porcelain` before commit confirms no `Packages/`/ADR file touched (the auto-generated `Packages/**.meta` files Unity produced as a side effect of the batch run were removed via `git clean -f -- Packages/`, not committed). |
| AC-7 | Passed | `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1` all pass — see Validation results above. |
| AC-8 | **Blocked** | `scripts/test-unity.ps1` cannot complete — see §4/§17's full finding. Not this task's own gap to fix (§5). |
| AC-9 | Passed | `git diff --name-status` matches §5's Allowed paths exactly. |
| AC-10 | Pending | Draft PR not yet opened. |

### Known limitations

- No `TC-*` catalog entry is registered for these tests, matching `DeveloperShellPresenter`'s own precedent — `SLICE-UI-01`'s trial-UI verification is Unity-batchmode-run, not `test-catalog.json`-tracked.
- No manual human Play-mode session was performed as part of this task's own evidence (the EditMode tests run headlessly) — a human trying the actual scene is welcome but not required here.

### Follow-up tasks

- `ODY-UI-01-003` (role selector), to retrofit this task's hardcoded `LocalActorUserId`.

### Self-review summary

- Scope review: One new presenter class, its demo-campaign factory, and its tests — no `Packages/` or ADR change.
- Architecture review: `ADR-001` §6.7's boundary followed exactly as `DeveloperShellPresenter` already established; no adapter layer, no service locator.
- Test review: All four scenarios (own-move, foreign-move-denied, MainGM-override, initial-render) covered with real `SqliteSceneRepository`, not a mock.
- Security/privacy review: The one security-relevant property (non-controller cannot move) is directly tested.
- Documentation/version review: Task contract, ExecPlan, and one backlog row updated.

## 18. Blockers, decisions, and change control

### Blockers

- **`scripts/test-unity.ps1` cannot complete**, blocking a real, run-verified confirmation of this task's own EditMode tests (AC-1–5, AC-8). Root cause (§4): `Odyssey.Persistence`'s `Microsoft.Data.Sqlite`/`SQLitePCLRaw` NuGet dependency has no Unity-side equivalent (no `Plugins/` DLL, no `precompiledReferences` entry) — a pre-existing gap, confirmed unrelated to this task's own two new files, that has apparently existed since `SLICE-01` first introduced SQLite usage and was never caught because CI's own Unity check is static-only (`TC-CI-006`, confirmed by its own log line) and, by direct evidence (dozens of previously-uncommitted `Packages/**.meta` files Unity auto-generated during this run), no prior task ever actually opened this project in the real Unity Editor. This is not a blocker this task's own scope permits fixing (§5 explicitly excludes any `Packages/com.odyssey.persistence` change) — it blocks this task's own full closure and will block every subsequent `SLICE-UI-01` task that touches `Odyssey.Persistence` (essentially all of them). Flagged to the product owner as needing a dedicated, separate follow-up task before further `SLICE-UI-01` work can get real Unity Editor validation, not resolved here.

### Decisions made during execution

- 2026-08-27 — Decision: render tokens as plain absolutely-positioned `VisualElement`s inside the existing `UIDocument`, not a separate GameObject/SpriteRenderer scene hierarchy — Authority: `ADR-001` §6.7 already names "UI Toolkit views" as this module's expected pattern; a second rendering technology would need its own camera/coordinate-space setup for a screen `SLICE-UI-01_BACKLOG.md` §3.4 keeps deliberately minimal.
- 2026-08-27 — Decision: expose `SelectToken`/`TryMoveSelectedTokenTo` as public methods, with UI Toolkit click callbacks as thin wrappers over them, rather than testing through simulated pointer events — Authority: no existing EditMode test in this repository simulates a UI Toolkit click event (confirmed by `Grep`); this separation lets tests exercise the presenter's own logic directly and robustly, matching this task's own ТЗ suggestion to test presenter logic separately from Unity event-plumbing infrastructure where possible.
- 2026-08-27 — Decision: `BoardScreenDemoCampaign.CreateFresh` creates a throwaway campaign/scene/two-tokens fixture programmatically, so a human can press Play with no manual setup step — Authority: this task's own ТЗ §3 explicit instruction to decide this; a fresh, isolated temp-directory campaign avoids any risk to a real campaign and needs no manual operator step.
- 2026-08-27 — Decision: `LocalActorUserId`/`LocalActorIsMainGm` are mutable public properties, not constructor-fixed values — Authority: this task's own ТЗ §3 explicit instruction, so `ODY-UI-01-003`'s future role selector can set them directly without reconstructing the presenter.
- 2026-08-27 — Finding, not fixed here (per this task's own explicit "stop and report" discipline, consistent with `ODY-S03-008`'s own precedent): `Odyssey.Persistence` cannot compile inside the real Unity Editor at all — `Microsoft.Data.Sqlite`/`SQLitePCLRaw` are NuGet-only, with no Unity-side Plugin/`precompiledReferences` equivalent. Confirmed pre-existing (unrelated to this task's own diff) and confirmed that no prior task ever successfully opened this project in Unity Editor (dozens of previously-uncommitted `Packages/**.meta` files were freshly auto-generated by this run). Authority: real `scripts/test-unity.ps1` run performed for this task's own validation; `git clean -f -- Packages/` used to keep the auto-generated `.meta` churn out of this task's own diff without deciding whether those files should eventually be committed by whichever future task fixes the underlying SQLite-Unity-plugin gap.

### Approved task changes

- None.
