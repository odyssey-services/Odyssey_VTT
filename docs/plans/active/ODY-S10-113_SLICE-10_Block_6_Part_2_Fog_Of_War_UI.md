# ExecPlan — ODY-S10-113 SLICE-10 Block 6 Part 2: Fog of War UI

## 1. Purpose
Give the client its first viewer-scoped server read: render permanent map memory (a darkening overlay cut out for explored regions) and live token visibility (an out-of-sight token is not rendered) for a non-MainGm actor, backed entirely by real, authorized server data -- never a client-side heuristic.

## 2. Scope
One server-side method (`PlayerVisibilityService.ListExploredReveals` + `ListExploredRevealsRequest`), one new client presenter (`BoardFogOfWarPresenter.cs`), point-edits to `BoardScreenPresenter.cs`/`TrialScreenPresenter.cs`, mechanical fixes to 19 existing `BoardScreenPresenter` construction call sites, a new EditMode test file, point-edits to the server test file and the PlayMode smoke test file, the catalogue, the backlog, this plan and the contract.

## 3. Non-goals
Cover preview UI, facing/FOV configuration UI (Block 6 part 3); a third "explored but not visible" visual state; automatic `RecordExploration` on facing change; wiring `RecordExploration` into `ApplyDroppedAsset` (no token-creation branch exists there, see contract §18); pixel-perfect fog boundaries; any change to `IFogOfWarRepository`/`SqliteFogOfWarRepository`, `TokenVisionService.cs`, `ObstacleAuthoringService.cs`, `CoverSuggestionService.cs`, or any combat-pipeline file.

## 4. Architecture
`ListExploredReveals` is a thin pass-through: the exact `CheckSelfScopedOrMainGm` gate `IsPointExplored`/`ComputeVisibleTokens` already use, then `IFogOfWarRepository.ListReveals` as-is -- no new authorization primitive, no schema change. `BoardFogOfWarPresenter` owns one `VisualElement` with a `Painter2D`-based `generateVisualContent` callback; it holds no repository/authorization logic itself, only whatever reveal list its caller (`BoardScreenPresenter`) already obtained through the authorized service call. `BoardScreenPresenter.Refresh()` gains two new self-scoped calls (`ComputeVisibleTokens`, `ListExploredReveals`), both skipped outright for a MainGm actor (mirroring the toolbar's own MainGm-only visibility gate); `RenderTokens` gains a `visibleTokenIds` parameter and hides (not merely darkens) any token outside it. `RecordExploration` is invoked, best-effort (its `Result` never inspected), from both real move-commit methods after a successful `BoardMovementService.MoveToken` call.

## 5. Milestones
1. Recon: confirm `ListExploredReveals`'s exact authorization shape against `IsPointExplored`, confirm `PlayerVisibilityServiceTests.cs`'s test-writing convention, confirm the real `TryMoveTokenTo`/`TryMoveSelectedTokenTo`/`ApplyDroppedAsset` call graph against the ТЗ's own stated facts (surfaced two findings, see contract §18). 2. `PlayerVisibilityService.ListExploredReveals` + its four `PlayerVisibilityServiceTests.cs` tests; `dotnet build`/`dotnet test` (this method's own tests, then the full `Odyssey.Tests.Persistence` suite); a server-side mutation (the new authorization check removed) proving the denial test bites, reverted. 3. `BoardFogOfWarPresenter.cs`. 4. `BoardScreenPresenter.cs`/`TrialScreenPresenter.cs` point-edits (constructor parameters, `Refresh()`, `RenderTokens`, `RecordExplorationBestEffort`). 5. The 19 existing-constructor-call-site mechanical fixes. 6. `BoardScreenPresenterFogOfWarTests.cs` (5 EditMode tests) and the PlayMode role-switch smoke test; a client-side mutation, reverted. 7. Catalogue, backlog, contract, plan. 8. Full Unity batchmode compile + EditMode + PlayMode validation and Draft PR.

## 6-8. State/flow, error handling, test strategy
See task contract section 9 and section 18.

## 9. Validation and acceptance evidence
`dotnet test` (`Odyssey.Tests.Persistence`, full 942-test suite, 940 passing -- the 2 pre-existing, unrelated `SqliteSavingPipelineTests` harness-build-ordering failures independently confirmed and disclosed in the contract); a server-side mutation on `ListExploredReveals`'s own authorization check; Unity 6000.4.0f1 batchmode (`scripts/test-unity.ps1`): compile, EditMode, PlayMode, recorded in the task's own PR; a client-side mutation on `RenderTokens`'s visibility condition; `verify-format`/`verify-repository`/`verify-test-structure`; `git diff --name-status origin/main` reviewed against the forbidden-path list.

## 10. Recovery and rollback
Revert the PR. The one server-side change (`ListExploredReveals`) is additive (a new method, no existing method/schema touched), so rollback is a pure code revert with no data migration.
