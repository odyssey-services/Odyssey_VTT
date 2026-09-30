# ExecPlan — ODY-S10-112 Point Fix: `ToggleObstacleDoor` Stale Revision

## 1. Purpose
Fix `BoardScreenPresenter.ToggleObstacleDoor` to re-read the obstacle's `Revision`/`IsOpen` fresh via `IObstacleRepository.ListObstacles` immediately before calling `ToggleDoorState`, instead of reusing the `RenderObstacles`-cached `ObstacleRecord` from the last `Refresh()` -- the one place in the file that broke its own "always re-read fresh before a revision-gated write" rule.

## 2. Scope
One point-edit (`BoardScreenPresenter.cs`), one new test (`BoardScreenPresenterObstacleTests.cs`), the catalogue, this plan and the contract. Same branch/PR as `ODY-S10-111`.

## 3. Non-goals
Any server-layer change; any change to `ToggleDoorState`'s own authorization; any behavior change to any other command.

## 4. Architecture
`ToggleObstacleDoor` now calls `IObstacleRepository.ListObstacles` for the obstacle's own Scene (identified from the cached record, used only for scene lookup, never for its `Revision`/`IsOpen`) and scans the result for the matching `ObstacleId`, mirroring the exact same "fresh read immediately before write" pattern `TryApplyObstacleDamage`'s own `GetObstacleDurability` call already established in the very same file.

## 5. Milestones
1. Recon confirmation that `IObstacleRepository` has no single-obstacle-by-id read (only `ListObstacles`/`GetObstacleDurability`). 2. The point-edit itself. 3. The new race-reproducing test. 4. A mutation check. 5. Catalogue, contract, plan. 6. Full Unity batchmode validation and push to the existing PR.

## 6-8. State/flow, error handling, test strategy
See task contract section 9 and section 18.

## 9. Validation and acceptance evidence
Unity 6000.4.0f1 batchmode: compile 0 errors, EditMode 137/137, PlayMode 6/6 (`scripts/test-unity.ps1`); a mutation proving the new race test bites; `git diff --name-status` reviewed (client files plus `test-catalog.json`/docs only, no server-layer file touched).

## 10. Recovery and rollback
Revert the commit; no schema or server-side change at all.
