# ExecPlan — ODY-S10-115 Obstacles Filtered By Fog Of War

## 1. Purpose
Close a gap found at Block 6's own close-out: obstacles (geometry, open/closed door state) are drawn unconditionally for every actor, relying only on the fog overlay's own translucency to hide them from a non-MainGm player. Filter obstacles by map memory the same way tokens already are.

## 2. Scope
One new server-side method (`PlayerVisibilityService.ListExploredObstacles` + `ListExploredObstaclesRequest`), its server test file (point-edit), one `BoardScreenPresenter.cs` point-edit, one new client EditMode test file, the catalogue, the backlog, this plan and the contract.

## 3. Non-goals
A "frozen at last observation" obstacle-state memory model; any change to `IObstacleRepository.ListObstacles` or its own internal callers (`ComputeLineOfSight`, `SuggestCover`); partial obstacle visibility.

## 4. Architecture
`ListExploredObstacles` is a thin filter: the exact `CheckSelfScopedOrMainGm` gate every other read in `PlayerVisibilityService.cs` already uses, then either the unfiltered `IObstacleRepository.ListObstacles` result (MainGm) or that same result filtered against `ListExploredReveals` (reused, never a direct `IFogOfWarRepository.ListReveals` read) using a three-sample-point per-obstacle test. `IObstacleRepository.ListObstacles` itself is never modified -- `TokenVisionService`/`CoverSuggestionService` keep reading it directly, unaffected. `BoardScreenPresenter.Refresh()` gains a second, additive read for the non-MainGm branch, mirroring the existing `ListTokens`/`ComputeVisibleTokens` relationship in the same method exactly.

## 5. Milestones
1. Recon: confirm `ListExploredReveals`'s exact signature/authorization, confirm `ObstacleRecord`'s field shape, confirm `PlayerVisibilityServiceTests.cs`'s test-writing convention and current max `TC-PERSIST-*`/`TC-BOARD-*`. 2. `PlayerVisibilityService.ListExploredObstacles` + its six `PlayerVisibilityServiceTests.cs` tests; `dotnet build`/`dotnet test`; a mutation (three-point check collapsed to midpoint-only) proving the two endpoint tests bite, reverted. 3. `BoardScreenPresenter.cs` point-edit. 4. `BoardScreenPresenterObstacleFogTests.cs` (2 EditMode tests). 5. Catalogue, backlog, contract, plan. 6. Full Unity batchmode compile + EditMode + PlayMode validation, full `dotnet test` regression run, and Draft PR.

## 6-8. State/flow, error handling, test strategy
See task contract section 9 and section 18.

## 9. Validation and acceptance evidence
`dotnet test` (`Odyssey.Tests.Persistence`, full suite, 947/948 passing -- the 1 pre-existing, unrelated `SqliteBackupRepositoryTests` harness-build-ordering failure independently confirmed and disclosed); a server-side mutation on the three-sample-point check; Unity 6000.4.0f1 batchmode (`scripts/test-unity.ps1`): compile, EditMode, PlayMode, recorded in the task's own PR; `verify-format`/`verify-repository`/`verify-test-structure`; `git diff --name-status origin/main` reviewed against the forbidden-path list.

## 10. Recovery and rollback
Revert the PR. The one server-side change (`ListExploredObstacles`) is additive (a new method, no existing method/schema touched), so rollback is a pure code revert with no data-migration concern.
